// patchCache.ts：茶绘「贴图」（油漆桶的结果图）缓存。图片走 HTTP（SignalR 单条消息只有 32KB），按哈希取一次就缓存。
// 画布引擎通过 setPatchProvider 拿图；图还没到时先不画，到了以后通知大家重画。
import request from '@/utils/request'
import { setPatchProvider } from './paintEngine'

const images = new Map<string, CanvasImageSource>()
const loading = new Map<string, Promise<void>>()
const listeners = new Set<() => void>()
let currentRoom = ''
let notifyFrame = 0

function notify() {
  // 一次来好几张图时只重画一次
  if (notifyFrame) return
  notifyFrame = requestAnimationFrame(() => { notifyFrame = 0; listeners.forEach(fn => fn()) })
}

setPatchProvider(hash => {
  const img = images.get(hash)
  if (img) return img
  if (currentRoom && !hash.startsWith('local-')) void loadPatch(currentRoom, hash)
  return null
})

/** 进画室 / 打开回放时设置：之后缺图会自动从这个画室取 */
export function usePatchRoom(roomId: string) { currentRoom = roomId }

/** 自己刚做出来的图：不用等上传和下载，直接用 */
export function registerPatch(hash: string, img: CanvasImageSource) { images.set(hash, img) }
export function forgetPatch(hash: string) { images.delete(hash) }

export function loadPatch(roomId: string, hash: string): Promise<void> {
  if (images.has(hash)) return Promise.resolve()
  const pending = loading.get(hash)
  if (pending) return pending
  const task = (async () => {
    try {
      const blob = await request.get<Blob>(`/Paint/${roomId}/patches/${hash}`, { responseType: 'blob', silentError: true })
      images.set(hash, await createImageBitmap(blob))
      loading.delete(hash)
      notify()
    } catch {
      // 取失败：过一会儿允许再试
      window.setTimeout(() => loading.delete(hash), 5000)
    }
  })()
  loading.set(hash, task)
  return task
}

/** 回放前把要用到的图都取回来 */
export async function ensurePatches(roomId: string, hashes: Iterable<string>) {
  await Promise.all([...new Set(hashes)].map(h => loadPatch(roomId, h)))
}

export function onPatchLoaded(fn: () => void) {
  listeners.add(fn)
  return () => { listeners.delete(fn) }
}

/** 上传一张贴图（PNG），返回服务器算的哈希 */
export async function uploadPatch(roomId: string, canvas: HTMLCanvasElement, png?: Blob | null): Promise<string> {
  const blob = png ?? await new Promise<Blob | null>(r => canvas.toBlob(r, 'image/png'))
  if (!blob) throw new Error('图片生成失败')
  const body = new FormData()
  body.append('file', blob, 'patch.png')
  const res = await request.post<{ hash: string }>(`/Paint/${roomId}/patches`, body, { silentError: true })
  registerPatch(res.hash, canvas)
  return res.hash
}
