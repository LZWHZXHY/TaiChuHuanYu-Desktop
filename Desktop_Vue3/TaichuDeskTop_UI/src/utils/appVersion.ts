// src/utils/appVersion.ts：当前网站是哪个版本（自动判断，不用手填）
//
// 打包前端时会记下「打包时间」（vite.config.ts 里的 __APP_BUILD_TIME__）。
// 版本记录按版本号从新到旧排，发布日期在打包时间之前的、最新的那个公开版本，就是这份网站代码对应的版本：
//   · 发布日期晚于打包时间的 → 还没打包上线，是「内测版 · 未上架」（本地在做、更新说明先写好了）；
//   · 之后重新打包部署，打包时间晚于它的发布日期，它就自动变成「当前版本」，上一个变成历史版本。
// 本地开发（npm run dev）时，打包时间 = 启动开发服务器的时间。
import type { ReleaseItem } from '@/api/release'
import { serverDate } from '@/utils/serverTime'

export const BUILD_TIME: string = typeof __APP_BUILD_TIME__ === 'string' ? __APP_BUILD_TIME__ : new Date().toISOString()
export const IS_LOCAL_DEV: boolean = import.meta.env.DEV

/** current = 线上正在用的版本；beta = 更新说明写好了、还没上线；past = 以前的版本；draft = 没公开的草稿 */
export type ReleaseStage = 'current' | 'beta' | 'past' | 'draft'

/** list 需要已经按版本号从新到旧排好（后端返回的顺序就是） */
export function releaseStages(list: ReleaseItem[]): Map<number, ReleaseStage> {
  const built = Date.parse(BUILD_TIME)
  const stages = new Map<number, ReleaseStage>()
  let currentFound = false
  for (const r of list) {
    if (!r.isPublished) stages.set(r.id, 'draft')
    else if (currentFound) stages.set(r.id, 'past')
    else if (serverDate(r.releasedAt).getTime() > built) stages.set(r.id, 'beta')
    else { stages.set(r.id, 'current'); currentFound = true }
  }
  return stages
}

export function versionSummary(list: ReleaseItem[]) {
  const stages = releaseStages(list)
  const current = list.find(r => stages.get(r.id) === 'current') ?? null
  // 最新的内测版（列表里最靠前的那个）
  const beta = list.find(r => stages.get(r.id) === 'beta') ?? null
  return { stages, current, beta }
}

export const formatBuildTime = () => serverDate(BUILD_TIME).toLocaleString('zh-CN', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })
