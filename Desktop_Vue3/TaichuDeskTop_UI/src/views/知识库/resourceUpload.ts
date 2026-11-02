/**
 * 资源分片直传：浏览器把文件切成 64MB 一片，拿着服务器给的临时链接直接 PUT 到 NAS 上的 MinIO
 * （不经过网站服务器，也绕开 Cloudflare 免费版单个请求 100MB 的限制）。
 * · 同时传 3 片；每片失败自动重试 3 次（等 1、2、4 秒）；链接过期（403）会先向服务器要新链接再试；
 * · 每传完一片告诉服务器一声（记进度，页面刷新后可以只传剩下的）；
 * · 进度：已传字节、速度（最近几秒的平均）、剩余时间。
 */

export interface PartLink { partNumber: number; url: string }

export interface UploadProgress {
  /** 已经传上去的字节（含正在传的片已发出的部分） */
  loaded: number
  total: number
  /** 字节 / 秒（最近几秒的平均） */
  speed: number
  /** 预计还要多少秒；算不出来时为 null */
  eta: number | null
  doneParts: number
  partCount: number
  /** 正在重试的片号（给界面提示用） */
  retrying: number[]
}

export interface UploadPlan {
  file: Blob
  partSize: number
  partCount: number
  concurrency: number
  /** 片号 → 上传链接 */
  urls: Map<number, string>
  /** 已经传完的片号（继续上传时由服务器告诉我们；本次会话里传完的也会加进来） */
  done: Set<number>
}

export interface UploadDeps {
  /** 向服务器要新的上传链接 */
  refreshUrls: (parts: number[]) => Promise<PartLink[]>
  /** 告诉服务器某一片传完了（失败也不要紧） */
  reportPart: (partNumber: number, size: number) => Promise<unknown>
  /** 真正发 PUT 的函数（默认用 XMLHttpRequest，测试时可以换掉） */
  put?: (url: string, body: Blob, onProgress: (loaded: number) => void, signal: AbortSignal) => Promise<void>
  sleep?: (ms: number) => Promise<void>
}

export class UploadHttpError extends Error {
  status: number
  constructor(status: number) {
    super(status ? `上传失败（${status}）` : '网络中断')
    this.status = status
  }
}

export class UploadCancelled extends Error {
  constructor() { super('上传已取消') }
}

export class PartFailedError extends Error {
  partNumber: number
  reason: unknown
  constructor(partNumber: number, reason: unknown) {
    super(`第 ${partNumber} 片重试 3 次还是没传上去`)
    this.partNumber = partNumber
    this.reason = reason
  }
}

/** 每片最多重试几次（不算第一次） */
export const PART_RETRIES = 3

/** 默认用 XMLHttpRequest：fetch 拿不到上传进度 */
export function xhrPut(url: string, body: Blob, onProgress: (loaded: number) => void, signal: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    if (signal.aborted) { reject(new UploadCancelled()); return }
    const xhr = new XMLHttpRequest()
    xhr.open('PUT', url)
    xhr.upload.onprogress = e => onProgress(e.loaded)
    xhr.onload = () => (xhr.status >= 200 && xhr.status < 300 ? resolve() : reject(new UploadHttpError(xhr.status)))
    xhr.onerror = () => reject(new UploadHttpError(0))
    xhr.ontimeout = () => reject(new UploadHttpError(0))
    xhr.onabort = () => reject(new UploadCancelled())
    signal.addEventListener('abort', () => xhr.abort(), { once: true })
    // 片是 file.slice() 切出来的，没有类型，浏览器不会加 Content-Type 头
    xhr.send(body)
  })
}

export function partBounds(plan: Pick<UploadPlan, 'file' | 'partSize'>, partNumber: number) {
  const start = (partNumber - 1) * plan.partSize
  const end = Math.min(plan.file.size, start + plan.partSize)
  return { start, end, size: end - start }
}

export class ChunkUploader {
  private controller = new AbortController()
  private inflight = new Map<number, number>()
  private retrying = new Set<number>()
  private samples: { t: number; loaded: number }[] = []
  private lastEmit = 0
  private readonly put: NonNullable<UploadDeps['put']>
  private readonly sleep: NonNullable<UploadDeps['sleep']>

  private readonly plan: UploadPlan
  private readonly deps: UploadDeps
  private readonly onProgress: (p: UploadProgress) => void

  constructor(plan: UploadPlan, deps: UploadDeps, onProgress: (p: UploadProgress) => void) {
    this.plan = plan
    this.deps = deps
    this.onProgress = onProgress
    this.put = deps.put ?? xhrPut
    this.sleep = deps.sleep ?? (ms => new Promise(resolve => setTimeout(resolve, ms)))
  }

  /** 已经传完的片的总字节 */
  private doneBytes() {
    let sum = 0
    for (const n of this.plan.done) sum += partBounds(this.plan, n).size
    return sum
  }

  private emit(force = false) {
    const now = performance.now()
    if (!force && now - this.lastEmit < 200) return
    this.lastEmit = now
    let loaded = this.doneBytes()
    for (const v of this.inflight.values()) loaded += v
    const total = this.plan.file.size
    loaded = Math.min(loaded, total)
    // 速度：最近 6 秒的平均（太短会跳来跳去）
    this.samples.push({ t: now, loaded })
    while (this.samples.length > 2 && now - (this.samples[0]?.t ?? now) > 6000) this.samples.shift()
    const first = this.samples[0] ?? { t: now, loaded }
    const seconds = (now - first.t) / 1000
    const speed = seconds >= 0.5 ? Math.max(0, (loaded - first.loaded) / seconds) : 0
    this.onProgress({
      loaded,
      total,
      speed,
      eta: speed > 0 ? Math.ceil((total - loaded) / speed) : null,
      doneParts: this.plan.done.size,
      partCount: this.plan.partCount,
      retrying: [...this.retrying].sort((a, b) => a - b),
    })
  }

  cancel() { this.controller.abort() }

  get cancelled() { return this.controller.signal.aborted }

  /** 传所有还没传完的片。全部成功时正常结束；取消时抛 UploadCancelled；某片重试用完抛 PartFailedError。 */
  async start(): Promise<void> {
    const queue: number[] = []
    for (let n = 1; n <= this.plan.partCount; n++) if (!this.plan.done.has(n)) queue.push(n)
    this.emit(true)
    let failure: unknown = null
    const worker = async () => {
      while (queue.length && !failure && !this.cancelled) {
        const n = queue.shift()!
        try { await this.uploadPart(n) }
        catch (e) {
          if (!failure) failure = e
          // 一片彻底失败：停掉其他正在传的，让用户决定要不要重试（已经传完的片不会丢）
          this.controller.abort()
        }
      }
    }
    const workers = Array.from({ length: Math.max(1, Math.min(this.plan.concurrency, queue.length)) }, worker)
    await Promise.all(workers)
    this.inflight.clear()
    this.retrying.clear()
    this.emit(true)
    if (failure) throw failure
    if (this.cancelled) throw new UploadCancelled()
  }

  private async uploadPart(n: number) {
    const { start, end, size } = partBounds(this.plan, n)
    const blob = this.plan.file.slice(start, end)
    let lastError: unknown = null
    for (let attempt = 0; attempt <= PART_RETRIES; attempt++) {
      if (this.cancelled) throw new UploadCancelled()
      if (attempt > 0) {
        this.retrying.add(n)
        this.emit(true)
        await this.sleep(1000 * 2 ** (attempt - 1))
        if (this.cancelled) throw new UploadCancelled()
      }
      try {
        let url = this.plan.urls.get(n)
        // 没有链接，或者上次是 403（链接过期 / 签名失效）：先要新的
        if (!url || (lastError instanceof UploadHttpError && lastError.status === 403)) {
          const fresh = await this.deps.refreshUrls([n])
          for (const p of fresh) this.plan.urls.set(p.partNumber, p.url)
          url = this.plan.urls.get(n)
          if (!url) throw new Error('没拿到上传链接')
        }
        this.inflight.set(n, 0)
        await this.put(url, blob, loaded => { this.inflight.set(n, loaded); this.emit() }, this.controller.signal)
        this.inflight.delete(n)
        this.retrying.delete(n)
        this.plan.done.add(n)
        this.emit(true)
        void this.deps.reportPart(n, size).catch(() => { /* 只是记进度，失败不影响上传 */ })
        return
      } catch (e) {
        this.inflight.delete(n)
        if (e instanceof UploadCancelled || this.cancelled) throw new UploadCancelled()
        lastError = e
      }
    }
    this.retrying.delete(n)
    throw new PartFailedError(n, lastError)
  }
}

/** 秒数写成「x 分 y 秒」 */
export function formatDuration(seconds: number | null): string {
  if (seconds == null || !Number.isFinite(seconds)) return '计算中…'
  if (seconds < 60) return `${Math.max(1, Math.round(seconds))} 秒`
  if (seconds < 3600) return `${Math.floor(seconds / 60)} 分 ${Math.round(seconds % 60)} 秒`
  return `${Math.floor(seconds / 3600)} 小时 ${Math.round((seconds % 3600) / 60)} 分`
}

// ---------- 页面刷新后继续上传：记住「哪个资源对应哪个本地文件」 ----------

const RESUME_KEY = 'taichu-resource-uploads'

export interface ResumeFingerprint { name: string; size: number; lastModified: number }

function readResume(): Record<string, ResumeFingerprint> {
  try { return JSON.parse(localStorage.getItem(RESUME_KEY) || '{}') as Record<string, ResumeFingerprint> } catch { return {} }
}

export function rememberUpload(resourceId: string, file: File) {
  try {
    const all = readResume()
    all[resourceId] = { name: file.name, size: file.size, lastModified: file.lastModified }
    localStorage.setItem(RESUME_KEY, JSON.stringify(all))
  } catch { /* 隐私模式等存不了：继续上传时只按大小核对 */ }
}

export function forgetUpload(resourceId: string) {
  try {
    const all = readResume()
    delete all[resourceId]
    localStorage.setItem(RESUME_KEY, JSON.stringify(all))
  } catch { /* 同上 */ }
}

/** 继续上传时选的文件是不是当初那个：大小必须一样；记得当初的文件名和修改时间的话也要一样 */
export function matchesResume(resourceId: string, expectedSize: number, file: File): { ok: boolean; reason?: string } {
  if (file.size !== expectedSize) return { ok: false, reason: '文件大小和当初选的不一样，请选同一个文件' }
  const saved = readResume()[resourceId]
  if (saved && (saved.name !== file.name || saved.lastModified !== file.lastModified))
    return { ok: false, reason: `请选当初的文件「${saved.name}」（文件被改动过的话只能重新上传）` }
  return { ok: true }
}
