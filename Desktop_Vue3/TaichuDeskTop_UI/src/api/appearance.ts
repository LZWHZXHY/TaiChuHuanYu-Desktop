import request from '@/utils/request'

/** 网站背景与赞助特权 */
export type BgSource = 'default' | 'preset' | 'custom'
export type BgKind = 'image' | 'video'
export type BgFocus = 'center' | 'top' | 'bottom' | 'left' | 'right'

export interface BgMedia { url: string; kind: BgKind; poster: string | null }
export interface BgPreset extends BgMedia {
  id: number; title: string; sortOrder: number; enabled: boolean
  /** 交易行在卖的背景：要先兑换才能保存使用 */
  locked?: boolean
  /** 自己已经兑换过 */
  owned?: boolean
  storeItemId?: number | null
  /** 原作者（社区投稿被采用的图） */
  author?: string | null
  /** 后台用 */
  authorId?: string | null
  authorName?: string | null
}

export interface SponsorInfo {
  active: boolean
  until: string | null
  /** 当前赞助等级（觉醒者 / 超凡者 / 大超凡者 / 君者） */
  tier: string | null
  tierRank: number
  tiers: string[]
  afdianBound: boolean
  /** 每笔赞助开通的天数 */
  days: number
  minAmount: number
  /** 特权 → 最低等级与是否已解锁，例如 custom-background */
  features: Record<string, { minRank: number; unlocked: boolean }>
}

/** 服务器保存的背景设置 */
export interface AppearanceView {
  source: BgSource
  presetId: number | null
  custom: BgMedia | null
  veil: number
  blur: number
  grid: boolean
  focus: BgFocus
  /** 左侧导航栏不透明度 30–100（%） */
  nav?: number
  sponsor: SponsorInfo
  /** 选的是自定义背景但特权已到期 */
  customLocked: boolean
}

export interface SaveAppearance {
  source: BgSource
  presetId: number | null
  url: string | null
  kind: BgKind
  poster: string | null
  veil: number
  blur: number
  grid: boolean
  focus: BgFocus
  nav: number
}

export const appearanceApi = {
  get: () => request.get<AppearanceView>('/Users/Appearance', { silentError: true }),
  presets: () => request.get<BgPreset[]>('/Users/Appearance/presets', { silentError: true }),
  save: (body: SaveAppearance) => request.put<AppearanceView>('/Users/Appearance', body, { silentError: true }),
  reset: () => request.delete<AppearanceView>('/Users/Appearance', { silentError: true }),
  bind: (orderNo: string) => request.post<{ message: string; sponsor: SponsorInfo }>('/Users/Appearance/sponsor/bind', { orderNo }, { silentError: true }),
  unbind: () => request.delete<{ message: string; sponsor: SponsorInfo }>('/Users/Appearance/sponsor/bind', { silentError: true }),
}

// ===== 后台（超级管理员） =====
export interface SavePreset { title: string; kind: BgKind; url: string; poster: string | null; sortOrder: number; enabled: boolean; authorId?: string | null; slot?: ProfileSlot }
export interface PerkItem { userId: string; name: string; avatar?: string | null; until: string | null; active: boolean; tier: string | null; afdianBound: boolean; updatedAt: string }
export interface PerkList { tiers: string[]; features: Record<string, number>; days: number; minAmount: number; items: PerkItem[] }
export interface PerkLogRow { id: number; userId: string; name: string; source: 'afdian' | 'manual' | 'revoke'; refId?: string | null; days: number; tier?: string | null; untilAfter?: string | null; note?: string | null; createdAt: string }
export interface UserHit { userId: string; name: string; avatar?: string | null }

export const adminAppearanceApi = {
  presets: () => request.get<BgPreset[]>('/Admin/Appearance/presets'),
  savePreset: (body: SavePreset, id?: number) => request.put<{ id: number }>('/Admin/Appearance/presets', body, { params: id ? { id } : undefined }),
  deletePreset: (id: number) => request.delete(`/Admin/Appearance/presets/${id}`),
  perks: () => request.get<PerkList>('/Admin/Appearance/perks'),
  log: (userId?: string) => request.get<PerkLogRow[]>('/Admin/Appearance/perks/log', { params: userId ? { userId } : undefined }),
  findUsers: (q: string) => request.get<UserHit[]>('/Admin/Appearance/users', { params: { q } }),
  grant: (userId: string, days: number, tier: string, note?: string) => request.post<{ until: string }>('/Admin/Appearance/perks/grant', { userId, days, tier, note }),
  revoke: (userId: string, note?: string) => request.post('/Admin/Appearance/perks/revoke', { userId, note }),
  /** 个人主页素材（横幅 / 整页背景，独立素材库） */
  profilePresets: () => request.get<ProfilePreset[]>('/Admin/Appearance/profile-presets'),
  saveProfilePreset: (body: SavePreset, id?: number) => request.put<{ id: number }>('/Admin/Appearance/profile-presets', body, { params: id ? { id } : undefined }),
  deleteProfilePreset: (id: number) => request.delete(`/Admin/Appearance/profile-presets/${id}`),
}

// ===== 个人主页装扮（横幅 + 整页背景，别人访问主页时看到） =====
export type ProfileSlot = 'banner' | 'page'
export interface ProfilePreset extends BgPreset { slot: ProfileSlot }
export interface ProfilePart {
  source: BgSource
  presetId: number | null
  custom: BgMedia | null
  focus: BgFocus
  /** 整页背景的遮罩 0–90（%） */
  veil: number
  /** 整页背景的模糊 0–20（px） */
  blur: number
}
export interface ProfileDecorView { banner: ProfilePart; page: ProfilePart; canCustom: boolean; sponsor: SponsorInfo; updatedAt?: string | null }
export interface SavePart { source: BgSource; presetId: number | null; url: string | null; kind: BgKind; poster: string | null; focus: BgFocus; veil: number; blur: number }
/** 某人主页实际显示的素材 */
export interface ShownMedia extends BgMedia { focus: BgFocus; veil: number; blur: number }
export interface PublicDecor { banner: ShownMedia | null; page: ShownMedia | null }

export const profileDecorApi = {
  get: () => request.get<ProfileDecorView>('/Users/ProfileDecor', { silentError: true }),
  presets: () => request.get<ProfilePreset[]>('/Users/ProfileDecor/presets', { silentError: true }),
  save: (banner: SavePart, page: SavePart) => request.put<ProfileDecorView>('/Users/ProfileDecor', { banner, page }, { silentError: true }),
  of: (userId: string) => request.get<PublicDecor>(`/Users/ProfileDecor/${userId}`, { silentError: true }),
}

/** 从接口错误里取出提示文字 */
export function apiMessage(e: unknown, fallback: string) {
  return (e as { response?: { data?: { message?: string } } })?.response?.data?.message || fallback
}

export const SOURCE_LABEL: Record<PerkLogRow['source'], string> = { afdian: '爱发电', manual: '手动开通', revoke: '收回' }
