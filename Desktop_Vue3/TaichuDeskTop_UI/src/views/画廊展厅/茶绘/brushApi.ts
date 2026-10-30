// 茶绘笔刷库接口 + 笔尖图载入 / 生成
import request from '@/utils/request'
import { DEFAULT_PARAMS, renderStroke, setTipProvider, type BrushParams } from './paintEngine'

export interface BrushView {
  id: string; ownerId: string; ownerName: string; name: string; params: BrushParams
  isPublic: boolean; favorites: number; favorited: boolean; mine: boolean; createdAt: string; updatedAt: string
}
export interface BrushSave { name: string; params: BrushParams; isPublic: boolean }

export const brushApi = {
  mine: () => request.get<BrushView[]>('/PaintBrush/mine', { silentError: true }),
  square: (params: { sort?: 'hot' | 'new'; q?: string; offset?: number; limit?: number }) =>
    request.get<{ items: BrushView[]; total: number }>('/PaintBrush/square', { params, silentError: true }),
  create: (data: BrushSave) => request.post<BrushView>('/PaintBrush', data, { silentError: true }),
  update: (id: string, data: BrushSave) => request.put<BrushView>(`/PaintBrush/${id}`, data, { silentError: true }),
  remove: (id: string) => request.delete(`/PaintBrush/${id}`, { silentError: true }),
  favorite: (id: string, on: boolean) => (on
    ? request.post<{ favorites: number; favorited: boolean }>(`/PaintBrush/${id}/favorite`, null, { silentError: true })
    : request.delete<{ favorites: number; favorited: boolean }>(`/PaintBrush/${id}/favorite`, { silentError: true })),
  uploadTip: (png: Blob) => {
    const body = new FormData()
    body.append('file', png, 'tip.png')
    return request.post<{ hash: string }>('/PaintBrush/tips', body, { silentError: true })
  },
}

export const errorText = (e: any, fallback: string) => e?.response?.data?.message || e?.friendlyMessage || fallback

// ================= 笔尖图 =================
export const TIP_SIZE = 128
const masks = new Map<string, HTMLCanvasElement>()
const loading = new Set<string>()
const listeners = new Set<() => void>()

/** 取笔尖蒙版；没有就去服务器取，取到后通知所有监听者重画 */
export function tipMask(hash: string): HTMLCanvasElement | null {
  const hit = masks.get(hash)
  if (hit) return hit
  if (!loading.has(hash) && /^[0-9a-f]{64}$/.test(hash)) {
    loading.add(hash)
    void request.get<Blob>(`/PaintBrush/tips/${hash}`, { responseType: 'blob', silentError: true, skipAuth: true })
      .then(blob => createImageBitmap(blob))
      .then(img => {
        const c = document.createElement('canvas')
        c.width = c.height = TIP_SIZE
        c.getContext('2d')!.drawImage(img, 0, 0, TIP_SIZE, TIP_SIZE)
        masks.set(hash, c)
        listeners.forEach(fn => fn())
      })
      .catch(() => undefined)
      .finally(() => loading.delete(hash))
  }
  return null
}
/** 编辑器里还没上传的笔尖先用临时名字登记，预览时就能用 */
export function registerTip(key: string, mask: HTMLCanvasElement) { masks.set(key, mask); listeners.forEach(fn => fn()) }
export function onTipLoaded(fn: () => void) { listeners.add(fn); return () => listeners.delete(fn) }
setTipProvider(tipMask)

/**
 * 把任意图片做成笔尖蒙版：等比缩放居中到 128×128，墨量 = (1 - 亮度) × 透明度（白底黑图、透明底黑图都能用），
 * invert 时反过来（黑底白图）。输出黑色 + 透明度的 PNG。
 */
export function imageToMask(img: CanvasImageSource & { width: number; height: number }, invert = false): HTMLCanvasElement {
  const c = document.createElement('canvas')
  c.width = c.height = TIP_SIZE
  const x = c.getContext('2d', { willReadFrequently: true })!
  const scale = Math.min(TIP_SIZE / img.width, TIP_SIZE / img.height)
  const w = img.width * scale, h = img.height * scale
  x.drawImage(img, (TIP_SIZE - w) / 2, (TIP_SIZE - h) / 2, w, h)
  const data = x.getImageData(0, 0, TIP_SIZE, TIP_SIZE)
  const d = data.data
  for (let i = 0; i < d.length; i += 4) {
    const lum = (0.299 * d[i]! + 0.587 * d[i + 1]! + 0.114 * d[i + 2]!) / 255
    const a = d[i + 3]! / 255
    const ink = (invert ? lum : 1 - lum) * a
    d[i] = d[i + 1] = d[i + 2] = 0
    d[i + 3] = Math.round(ink * 255)
  }
  x.putImageData(data, 0, 0)
  return c
}

export const canvasToPng = (c: HTMLCanvasElement) => new Promise<Blob>((resolve, reject) => c.toBlob(b => (b ? resolve(b) : reject(new Error('toBlob'))), 'image/png'))

/** 在一个小画布上画一段示意笔画（笔刷按钮、笔刷库卡片用） */
export function drawBrushPreview(canvas: HTMLCanvasElement, params: BrushParams, color = '#1b2128', opacity?: number) {
  const ctx = canvas.getContext('2d')
  if (!ctx) return
  const W = canvas.width, H = canvas.height
  ctx.clearRect(0, 0, W, H)
  const p = { ...DEFAULT_PARAMS, ...params }
  const size = Math.max(3, Math.min(H * 0.55, p.size))
  const pts: number[] = []
  const N = 48
  for (let i = 0; i <= N; i++) {
    const t = i / N
    pts.push(Math.round((W * 0.08 + t * W * 0.84) * 10), Math.round((H / 2 + Math.sin(t * Math.PI * 2) * H * 0.22) * 10), Math.round(Math.sin(t * Math.PI) * 100))
  }
  const sc = document.createElement('canvas'); sc.width = W; sc.height = H
  // 预览按画布大小缩短渐细长度，否则小卡片里整条线都是渐细
  const k = W / 600
  renderStroke(ctx, {
    tool: 'brush', color, size, opacity: opacity ?? p.opacity, brush: 'custom', hardness: 1, flags: 0,
    custom: { ...p, taperStart: p.taperStart * k, taperEnd: p.taperEnd * k }, points: pts,
  }, sc.getContext('2d')!, W, H)
}
