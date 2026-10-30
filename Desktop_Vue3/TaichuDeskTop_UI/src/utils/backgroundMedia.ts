/**
 * 网站背景素材的上传前处理（个人中心的自定义背景和后台的官方背景共用）：
 * - 图片：在浏览器里转成 WebP 并缩到 2560 像素以内；10MB 以内的 GIF 原样保留动画。
 * - 视频：只接受浏览器能播放的 MP4（H.264）/ WebM，并截一帧做封面。
 */
export type MediaKind = 'image' | 'video'
export interface UploadedMedia { url: string; kind: MediaKind; poster: string | null; note: string }
export type Uploader = (file: File) => Promise<{ url: string }>

/** 上传到服务器的图片大小上限 */
export const MAX_IMAGE_UPLOAD = 10 * 1024 * 1024
/** 可以选择的原图大小上限（会先在浏览器里转成 WebP） */
export const MAX_IMAGE_INPUT = 80 * 1024 * 1024
/** 视频大小上限：每次打开网站都要加载（之后有缓存），太大会很慢 */
export const MAX_VIDEO = 30 * 1024 * 1024

export const IMAGE_ACCEPT = 'image/jpeg,image/png,image/webp,image/gif,image/avif,image/bmp'
export const VIDEO_ACCEPT = 'video/mp4,video/webm'

export class MediaError extends Error {}

export const fmtSize = (bytes: number) => bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)}MB` : `${Math.max(1, Math.round(bytes / 1024))}KB`

const baseName = (name: string, fallback: string) => name.replace(/\.[^.]+$/, '') || fallback

/**
 * 转成 WebP：依次尝试「2560 边长 / 高画质」→ 逐步降低，取第一个不超过 3MB 的结果；实在压不下来就取最小的。
 * 浏览器不支持 WebP 编码时自动退回 JPG。
 */
const TARGET_BYTES = 3 * 1024 * 1024
const STEPS = [{ edge: 2560, q: 0.86 }, { edge: 2560, q: 0.76 }, { edge: 1920, q: 0.76 }, { edge: 1600, q: 0.7 }]
export async function convertToWebp(file: File): Promise<{ file: File; width: number; height: number } | null> {
  const bitmap = await createImageBitmap(file).catch(() => null)
  if (!bitmap) return null
  try {
    let best: { blob: Blob; width: number; height: number } | null = null
    for (const step of STEPS) {
      const scale = Math.min(1, step.edge / Math.max(bitmap.width, bitmap.height))
      const canvas = document.createElement('canvas')
      canvas.width = Math.max(1, Math.round(bitmap.width * scale))
      canvas.height = Math.max(1, Math.round(bitmap.height * scale))
      canvas.getContext('2d')?.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
      const toBlob = (type: string) => new Promise<Blob | null>(resolve => canvas.toBlob(resolve, type, step.q))
      const blob = (await toBlob('image/webp')) ?? (await toBlob('image/jpeg'))
      if (!blob) continue
      if (!best || blob.size < best.blob.size) best = { blob, width: canvas.width, height: canvas.height }
      if (blob.size <= TARGET_BYTES) break
    }
    if (!best) return null
    const ext = best.blob.type === 'image/webp' ? 'webp' : 'jpg'
    return { file: new File([best.blob], `${baseName(file.name, 'background')}.${ext}`, { type: best.blob.type }), width: best.width, height: best.height }
  } finally { bitmap.close() }
}

/** 读取视频信息并截一帧做封面。浏览器解不了的编码会返回 null。 */
export async function readVideo(file: File): Promise<{ poster: File | null; width: number; height: number; duration: number } | null> {
  const src = URL.createObjectURL(file)
  const video = document.createElement('video')
  video.muted = true
  video.playsInline = true
  video.preload = 'auto'
  video.src = src
  try {
    await new Promise<void>((resolve, reject) => {
      const timer = window.setTimeout(() => reject(new Error('timeout')), 15000)
      video.onloadeddata = () => { window.clearTimeout(timer); resolve() }
      video.onerror = () => { window.clearTimeout(timer); reject(new Error('decode')) }
    })
    const width = video.videoWidth, height = video.videoHeight, duration = Number.isFinite(video.duration) ? video.duration : 0
    if (!width || !height) return null
    await new Promise<void>(resolve => {
      const timer = window.setTimeout(resolve, 4000)
      video.onseeked = () => { window.clearTimeout(timer); resolve() }
      video.currentTime = Math.min(0.5, duration / 2 || 0)
    })
    const scale = Math.min(1, 1920 / Math.max(width, height))
    const canvas = document.createElement('canvas')
    canvas.width = Math.round(width * scale)
    canvas.height = Math.round(height * scale)
    canvas.getContext('2d')?.drawImage(video, 0, 0, canvas.width, canvas.height)
    const toBlob = (type: string) => new Promise<Blob | null>(resolve => canvas.toBlob(resolve, type, 0.8))
    const blob = (await toBlob('image/webp')) ?? (await toBlob('image/jpeg'))
    const poster = blob ? new File([blob], `${baseName(file.name, 'video')}-poster.${blob.type === 'image/webp' ? 'webp' : 'jpg'}`, { type: blob.type }) : null
    return { poster, width, height, duration }
  } catch {
    return null
  } finally {
    video.removeAttribute('src')
    video.load()
    URL.revokeObjectURL(src)
  }
}

/** 处理并上传一张图片。onStage 用来更新界面上的「正在转换 / 上传中」。 */
export async function uploadImage(original: File, upload: Uploader, onStage?: (stage: 'convert' | 'upload') => void): Promise<UploadedMedia> {
  if (!/^image\/(jpeg|png|webp|gif|avif|bmp)$/.test(original.type)) throw new MediaError('只支持 JPG、PNG、WebP、GIF、AVIF、BMP 图片')
  if (original.size > MAX_IMAGE_INPUT) throw new MediaError(`原图不能超过 ${fmtSize(MAX_IMAGE_INPUT)}`)
  let file = original
  let note = ''
  const isGif = original.type === 'image/gif'
  // 小动图原样上传以保留动画；其他图片一律转 WebP
  if (!isGif || original.size > MAX_IMAGE_UPLOAD) {
    onStage?.('convert')
    const converted = await convertToWebp(original)
    if (converted && (converted.file.size < original.size || original.size > MAX_IMAGE_UPLOAD)) {
      file = converted.file
      const kind = file.type === 'image/webp' ? 'WebP' : 'JPG'
      note = `已转成 ${kind}：${fmtSize(original.size)} → ${fmtSize(file.size)}（${converted.width}×${converted.height}）${isGif ? '。动图太大，只保留了第一帧' : ''}。`
    }
  }
  if (file.size > MAX_IMAGE_UPLOAD) throw new MediaError(`压缩后仍超过 ${fmtSize(MAX_IMAGE_UPLOAD)}，请换一张图片`)
  onStage?.('upload')
  const { url } = await upload(file)
  return { url, kind: 'image', poster: null, note }
}

/** 检查并上传一段视频（附带封面帧）。 */
export async function uploadVideo(file: File, upload: Uploader, onStage?: (stage: 'convert' | 'upload') => void): Promise<UploadedMedia> {
  if (!/^video\/(mp4|webm)$/.test(file.type)) throw new MediaError('视频只支持 MP4（H.264）和 WebM')
  if (file.size > MAX_VIDEO) throw new MediaError(`视频不能超过 ${fmtSize(MAX_VIDEO)}（这个有 ${fmtSize(file.size)}）。可以用剪映、HandBrake 等工具剪短一点或降到 1080p 再传。`)
  onStage?.('convert')
  const info = await readVideo(file)
  if (!info) throw new MediaError('浏览器播放不了这个视频。请换成 H.264 编码的 MP4 或 WebM 再试（手机拍的 HEVC 视频需要先转码）。')
  onStage?.('upload')
  const { url } = await upload(file)
  const poster = info.poster ? (await upload(info.poster).catch(() => null))?.url ?? null : null
  const note = `视频已上传（${info.width}×${info.height}，${Math.round(info.duration)} 秒，${fmtSize(file.size)}），会静音循环播放。${info.duration > 60 ? '视频比较长，建议剪到 30 秒以内的循环片段。' : ''}`
  return { url, kind: 'video', poster, note }
}
