import request from '@/utils/request'

/** 太虚游大厅展示的「太初寰宇出品」游戏（Steam 信息由后端抓取并缓存） */
export interface ShowcaseGame {
  id: number
  appId: number | null
  title: string
  /** Steam 上的原名（可能是英文） */
  steamName?: string | null
  tagline: string
  description: string
  status: 'released' | 'coming' | 'dev'
  featured: boolean
  sortOrder: number
  visible: boolean
  cover?: string | null
  /** 管理员填写的自定义封面（为空表示用 Steam 的） */
  customCover?: string | null
  capsule?: string | null
  background?: string | null
  screenshots: string[]
  screenshotThumbs: string[]
  movie?: string | null
  moviePoster?: string | null
  isFree: boolean
  price?: string | null
  originalPrice?: string | null
  discountPercent: number
  releaseDate?: string | null
  comingSoon: boolean
  genres: string[]
  developers: string[]
  platforms: string[]
  storeUrl?: string | null
  devProjectId?: string | null
  discussionTag?: string | null
  steamFetchedAt?: string | null
}

export interface SaveShowcase {
  steam: string
  title: string
  tagline: string
  status: ShowcaseGame['status']
  featured: boolean
  sortOrder: number
  coverUrl: string
  devProjectId: string
  discussionTag: string
  visible: boolean
}

export interface SteamLookup {
  appId: number
  info: { name: string; shortDescription: string; headerImage?: string | null; releaseDate?: string | null; price?: string | null; genres: string[]; comingSoon: boolean }
}

/** 后端还没部署或接口出错时的兜底数据，保证大厅里始终能看到我们的 Steam 游戏 */
const FALLBACK: ShowcaseGame[] = [{
  id: 0, appId: 3654500, title: '不喝水就无法离开的房间', steamName: "The room where you can't leave without drinking water",
  tagline: '被愤怒的古希腊水神困在房间里——不喝水，就别想离开。太初寰宇的第一款 Steam 游戏。',
  description: '关卡制的第一人称游戏：每一关都有独立的地图和目标，解谜、平台跳跃、射击和搜索，完成目标才能前往下一关。',
  status: 'released', featured: true, sortOrder: 0, visible: true,
  cover: 'https://cdn.cloudflare.steamstatic.com/steam/apps/3654500/header.jpg', customCover: null, capsule: null, background: null,
  screenshots: [], screenshotThumbs: [], isFree: false, price: null, originalPrice: null, discountPercent: 0,
  releaseDate: '2026 年 3 月 26 日', comingSoon: false, genres: ['休闲', '独立'], developers: ['太初寰宇'], platforms: ['windows'],
  storeUrl: 'https://store.steampowered.com/app/3654500/', devProjectId: null, discussionTag: null,
}]

export const showcaseApi = {
  list: async () => {
    try { return await request.get<ShowcaseGame[]>('/GameShowcase', { silentError: true }) }
    catch { return FALLBACK }
  },
}

export const adminShowcaseApi = {
  list: () => request.get<ShowcaseGame[]>('/Admin/Showcase'),
  lookup: (steam: string) => request.get<SteamLookup>('/Admin/Showcase/lookup', { params: { steam }, silentError: true }),
  save: (g: SaveShowcase, id?: number) => request.put<ShowcaseGame>('/Admin/Showcase', g, { params: { id }, silentError: true }),
  refresh: (id: number) => request.post<ShowcaseGame>(`/Admin/Showcase/${id}/refresh`, undefined, { silentError: true }),
  remove: (id: number) => request.delete(`/Admin/Showcase/${id}`),
}

export const statusLabel = (s: ShowcaseGame['status']) => ({ released: '已发售', coming: '即将推出', dev: '开发中' })[s] ?? s

/** 打开外部链接：桌面客户端里交给宿主程序打开，浏览器里新开标签页 */
export function openExternal(url: string) {
  const host = (window as unknown as { chrome?: { webview?: { postMessage: (m: unknown) => void } } }).chrome?.webview
  if (host) host.postMessage({ cmd: 'load_external_url', url })
  else window.open(url, '_blank', 'noopener,noreferrer')
}
