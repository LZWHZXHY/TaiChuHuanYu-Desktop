// 视频链接的判断（战后记录、编年史「约战记录」卡片用）：
//   B 站链接 → 用必吃榜的「抓取封面」接口拿封面和标题，显示封面卡片，点开去 B 站；
//   站内视频（柴圈作品页 /works/xxx，或者直接是 mp4 等视频文件）→ 页面里直接播放；
//   其他链接 → 普通链接卡片（显示域名）。
// 链接只认 http / https，别的（javascript: 之类）一律当作没有链接。
import { mustEatApi } from '@/api/mustEat'

/** 是 http / https 链接就返回去掉首尾空格的链接，否则 null */
export function safeWebLink(url?: string | null): string | null {
  const text = (url ?? '').trim()
  if (!text) return null
  try {
    const u = new URL(text)
    return u.protocol === 'http:' || u.protocol === 'https:' ? text : null
  } catch {
    return null
  }
}

/** 链接的域名（去掉 www.），给普通链接卡片显示 */
export function hostOf(url: string): string {
  try { return new URL(url).hostname.replace(/^www\./, '') } catch { return '' }
}

/** B 站的链接（含 b23.tv 短链） */
export function isBilibiliLink(url: string): boolean {
  const host = hostOf(url).toLowerCase()
  return host === 'bilibili.com' || host.endsWith('.bilibili.com') || host === 'b23.tv'
}

/** 链接里带 BV 号 / av 号：必吃榜的接口只认这两种，短链抓不到封面（显示占位图） */
export function hasBilibiliVideoId(url: string): boolean {
  return /BV[0-9A-Za-z]{10}/.test(url) || /\/av\d+/i.test(url)
}

/** 站内柴圈作品页的作品编号（哈希路由 /#/works/xxx 和 /works/xxx 都认），不是就 null */
export function siteWorkIdOf(url: string): string | null {
  const m = /\/works\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})(?:[/?#]|$)/i.exec(url)
  return m ? m[1]!.toLowerCase() : null
}

/** 链接是不是本站（和当前页面同一个域名） */
export function isSameSite(url: string): boolean {
  try { return typeof window !== 'undefined' && new URL(url).host === window.location.host } catch { return false }
}

/** 直接是视频文件（站内上传到对象存储的 mp4 之类），可以用 <video> 播放 */
export function isVideoFile(url: string): boolean {
  try { return /\.(mp4|webm|ogg|ogv|mov|m4v)$/i.test(new URL(url).pathname) } catch { return false }
}

export interface BilibiliMeta { coverImage: string | null; title: string | null }

// 同一个链接只抓一次（列表里同一个视频出现多次、来回切换都不重复请求）
const coverCache = new Map<string, Promise<BilibiliMeta | null>>()

/** 用必吃榜那套接口抓 B 站视频的封面和标题；不是带 BV / av 号的 B 站链接、或者抓不到时返回 null */
export function fetchBilibiliMeta(url: string): Promise<BilibiliMeta | null> {
  const link = safeWebLink(url)
  if (!link || !isBilibiliLink(link) || !hasBilibiliVideoId(link)) return Promise.resolve(null)
  let task = coverCache.get(link)
  if (!task) {
    task = mustEatApi.fetchCover(link)
      .then(res => ({ coverImage: safeWebLink(res.coverImage), title: res.title }))
      .catch(() => {
        coverCache.delete(link) // 失败了下次还可以再试
        return null
      })
    coverCache.set(link, task)
  }
  return task
}
