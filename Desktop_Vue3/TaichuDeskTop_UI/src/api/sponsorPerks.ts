import request from '@/utils/request'

/** 一个赞助等级的介绍（等级名字固定：觉醒者 / 超凡者 / 大超凡者 / 君者） */
export interface PerkTier {
  rank: number
  name: string
  /** 价格说明，比如「每月 ¥10」 */
  price: string
  blurb: string
}

/** 一条赞助福利 */
export interface PerkItem {
  id: string
  /** 有 key 的是网站真正判断的特权（改等级马上生效、不能删）；没有的只是展示文字 */
  key: string | null
  title: string
  description: string
  /** 最低等级 1～4，高等级自动包含低等级的福利 */
  minTier: number
  /** 是否在赞助墙公开显示（隐藏了照样生效） */
  visible: boolean
}

export interface PerkCatalog {
  version: number
  intro: string
  tiers: PerkTier[]
  perks: PerkItem[]
  /** 只有超级管理员拿得到：代码里真正判断的特权和默认等级 */
  builtIns: { key: string; title: string; defaultTier: number }[] | null
  canEdit: boolean
  /** 登录了才有：自己的赞助状态 */
  mine: { active: boolean; tier: string | null; rank: number; until: string | null } | null
}

export interface PerkCatalogPayload {
  intro: string
  tiers: { rank: number; price: string; blurb: string }[]
  perks: { id: string; key: string | null; title: string; description: string; minTier: number; visible: boolean }[]
}

export const sponsorPerksApi = {
  get: () => request.get<PerkCatalog>('/SponsorPerks', { silentError: true }),
  save: (data: PerkCatalogPayload) => request.put<PerkCatalog>('/SponsorPerks', data, { silentError: true }),
}
