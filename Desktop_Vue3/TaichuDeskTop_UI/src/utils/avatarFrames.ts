/**
 * 头像框有两种：
 * 1. 代码里画好的 CSS 框（下面 AVATAR_FRAMES 这 8 种），样子在 components/AvatarFrame.vue 里；
 * 2. 后台上传的图片框（透明 PNG / GIF / WebP，key 是 img-{Id}），在「管理面板 → 交易行 → 头像框素材」里管理，不用改代码。
 * 后台上架「头像框」商品时两种都能选（商品里存的是 key）。
 */
import { reactive } from 'vue'
import request from '@/utils/request'

export interface AvatarFrameDef { key: string; name: string; desc: string }
/** 后台上传的图片框；scale 是框图相对头像的大小（1.4 = 比头像大 40%） */
export interface ImageFrameDef { key: string; name: string; url: string; scale: number; enabled: boolean; author?: string | null }

export const AVATAR_FRAMES: AvatarFrameDef[] = [
  { key: 'rhine-ring', name: '莱茵标识', desc: '实验室蓝的双线框，四角定位标' },
  { key: 'hazard', name: '警示条纹', desc: '黄黑警戒线，危险品搬运员专用' },
  { key: 'circuit', name: '电路节点', desc: '薄荷绿电路走线，角上有节点灯' },
  { key: 'scan', name: '扫描中', desc: '一道扫描光不停地扫过头像' },
  { key: 'gold', name: '金辉', desc: '流动的金色描边' },
  { key: 'sakura', name: '樱色', desc: '粉色渐变边框，角上一朵樱花' },
  { key: 'originium', name: '源石结晶', desc: '四角长出橙红色晶簇' },
  { key: 'void', name: '深空', desc: '紫色虚线光环缓缓转动' },
]

const registry = reactive({ list: [] as ImageFrameDef[], loaded: false, loading: null as Promise<void> | null })

/** 读取图片框列表（全站只请求一次；force 用于后台修改之后刷新） */
export function loadImageFrames(force = false): Promise<void> {
  if (registry.loaded && !force) return Promise.resolve()
  if (registry.loading && !force) return registry.loading
  registry.loading = request.get<ImageFrameDef[]>('/trade/frames', { silentError: true })
    .then(list => { registry.list = Array.isArray(list) ? list : []; registry.loaded = true })
    .catch(() => { /* 读不到就只显示 CSS 框 */ })
    .finally(() => { registry.loading = null })
  return registry.loading
}

export const isImageFrame = (key?: string | null) => !!key && key.startsWith('img-')

/** 图片框的定义；还没加载时会触发加载，返回 null，加载完自动刷新 */
export function imageFrameOf(key?: string | null): ImageFrameDef | null {
  if (!isImageFrame(key)) return null
  if (!registry.loaded) void loadImageFrames()
  return registry.list.find(f => f.key === key) ?? null
}

/** 后台选样式用：CSS 框 + 上架中的图片框 */
export function frameOptions(): { key: string; name: string; desc: string }[] {
  if (!registry.loaded) void loadImageFrames()
  return [
    ...AVATAR_FRAMES,
    ...registry.list.filter(f => f.enabled).map(f => ({ key: f.key, name: f.name, desc: f.author ? `图片框 · 作者 ${f.author}` : '图片框' })),
  ]
}

export const frameName = (key?: string | null) =>
  AVATAR_FRAMES.find(f => f.key === key)?.name ?? imageFrameOf(key)?.name ?? (isImageFrame(key) ? '头像框' : key ?? '')
