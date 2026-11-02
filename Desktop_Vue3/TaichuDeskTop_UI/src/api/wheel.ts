// src/api/wheel.ts：太虚游 · 转盘（用户自己做的转盘 + 官方抽奖转盘）
import request from '@/utils/request'

/** 奖品类型：无 / 土豆 / 经验（只有官方转盘能设奖品） */
export type WheelPrizeType = 'none' | 'potato' | 'exp'
export type WheelKind = 'user' | 'official'
export type WheelVisibility = 'public' | 'private'
export type WheelStatus = 'active' | 'hidden'

export interface WheelSegment {
  text: string
  color: string
  weight: number
  prizeType: WheelPrizeType
  prizeAmount: number
  /** 后端给的奖品文字，例如「100 土豆」；没奖品是空字符串 */
  prizeLabel?: string
}

export interface WheelOwner { id: string; name: string }

/** 官方转盘的一次抽奖记录 */
export interface WheelSpinRecord {
  id: number
  wheelId: number
  wheelTitle: string
  segmentIndex: number
  text: string
  prizeType: WheelPrizeType
  prizeAmount: number
  prizeLabel: string
  /** 北京时间的日期，例如 2026-10-31（只有日期，按字符串用） */
  spinDate: string
  createdAt: string
}

/** 列表里的卡片 */
export interface WheelCard {
  id: number
  kind: WheelKind
  title: string
  intro: string
  visibility: WheelVisibility
  status: WheelStatus
  segmentCount: number
  colors: { color: string; weight: number }[]
  hasPrize: boolean
  spinCount: number
  owner: WheelOwner | null
  createdAt: string
  /** 官方转盘：今天转过的话是今天的结果 */
  today?: WheelSpinRecord | null
}

/** 转盘完整信息 */
export interface WheelDetail {
  id: number
  kind: WheelKind
  title: string
  intro: string
  visibility: WheelVisibility
  status: WheelStatus
  hiddenReason: string | null
  removeOnHit: boolean
  segments: WheelSegment[]
  spinCount: number
  owner: WheelOwner | null
  isMine: boolean
  shareCode: string | null
  sortOrder: number
  createdAt: string
  updatedAt: string
}

export interface WheelInput {
  title: string
  intro: string
  visibility: WheelVisibility
  removeOnHit: boolean
  segments: WheelSegment[]
  sortOrder?: number
}

export type WheelPayWith = 'exp' | 'potato'

export interface WheelLimits {
  minSegments: number
  maxSegments: number
  titleMax: number
  introMax: number
  segmentTextMax: number
  weightMax: number
  potatoPrizeMax: number
  expPrizeMax: number
}

export interface WheelPricing {
  /** 负数 = 站长关掉了这种付法，0 = 免费 */
  costExp: number
  costPotato: number
  count: number
  max: number
  experience: number
  potato: number
  limits: WheelLimits
}

export interface WheelSpinResponse {
  segmentIndex: number
  segment: WheelSegment
  segments: WheelSegment[]
  message: string
}

export interface WheelMineItem { card: WheelCard; shareCode: string; hiddenReason: string | null }

export const wheelApi = {
  list: (sort: 'hot' | 'new', page = 1, q = '') =>
    request.get<{ official: WheelCard[]; items: WheelCard[]; hasMore: boolean }>('/wheel/list', { params: { sort, page, q: q || undefined }, silentError: true }),
  mine: () => request.get<{ items: WheelMineItem[]; count: number; max: number }>('/wheel/mine', { silentError: true }),
  pricing: () => request.get<WheelPricing>('/wheel/pricing', { silentError: true }),
  get: (id: number, k?: string) =>
    request.get<{ wheel: WheelDetail; today: WheelSpinRecord | null; canModerate: boolean; canManageOfficial: boolean }>(`/wheel/${id}`, { params: { k: k || undefined }, silentError: true }),
  create: (body: WheelInput & { payWith: WheelPayWith }) =>
    request.post<{ id: number; shareCode: string | null; message: string }>('/wheel', body, { silentError: true }),
  update: (id: number, body: WheelInput) => request.put<{ message: string }>(`/wheel/${id}`, body, { silentError: true }),
  remove: (id: number) => request.delete<{ message: string }>(`/wheel/${id}`, { silentError: true }),
  countSpin: (id: number) => request.post<{ counted: boolean }>(`/wheel/${id}/spin-count`, undefined, { silentError: true }),

  // ----- 官方转盘 -----
  spinOfficial: (id: number) => request.post<WheelSpinResponse>(`/wheel/official/${id}/spin`, undefined, { silentError: true }),
  officialMe: () => request.get<{ today: WheelSpinRecord[]; records: WheelSpinRecord[] }>('/wheel/official/me', { silentError: true }),
  feed: () => request.get<{ id: number; name: string; prize: string }[]>('/wheel/official/feed', { silentError: true }),

  // ----- 管理 -----
  adminOfficialList: () => request.get<WheelDetail[]>('/wheel/admin/official', { silentError: true }),
  adminOfficialCreate: (body: WheelInput) => request.post<{ id: number; message: string }>('/wheel/admin/official', body, { silentError: true }),
  adminOfficialUpdate: (id: number, body: WheelInput) => request.put<{ message: string }>(`/wheel/admin/official/${id}`, body, { silentError: true }),
  adminOfficialDelete: (id: number) => request.delete<{ message: string }>(`/wheel/admin/official/${id}`, { silentError: true }),
  adminSetStatus: (id: number, status: WheelStatus, reason?: string) =>
    request.post<{ message: string }>(`/wheel/admin/${id}/status`, { status, reason }, { silentError: true }),
  adminSettings: () =>
    request.get<{ costExp: number; costPotato: number; keys: { exp: string; potato: string }; defaults: { exp: number; potato: number } }>('/wheel/admin/settings', { silentError: true }),
  adminSaveSettings: (costExp: number, costPotato: number) =>
    request.put<{ message: string }>('/wheel/admin/settings', { costExp, costPotato }, { silentError: true }),
}

/** 从接口错误里取出后端给的中文提示 */
export function wheelError(e: unknown, fallback = '出错了，稍后再试'): string {
  const data = (e as { response?: { data?: unknown } })?.response?.data
  if (data && typeof data === 'object' && typeof (data as { message?: unknown }).message === 'string') return (data as { message: string }).message
  if (typeof data === 'string' && data.trim()) return data
  return fallback
}
