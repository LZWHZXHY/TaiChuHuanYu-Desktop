import request from '@/utils/request'

export interface SponsorItem {
  id: number
  name: string
  tier: string
  amount: number
  message: string | null
  isAnonymous: boolean
  sponsoredAt: string
}

export interface SponsorWallData {
  totalAmount: number
  totalCount: number
  sponsors: SponsorItem[]
}

export type SponsorPlatform = 'azz' | 'afdian' | 'other'
export interface SponsorPayload {
  name: string
  tier: string
  /** 到手金额（平台选「其他」时直接用这个） */
  amount: number
  message?: string | null
  isAnonymous: boolean
  isVisible: boolean
  /** 赞助平台：爱赞助 / 爱发电按抽成算到手金额 */
  platform?: SponsorPlatform
  /** 赞助者实际付的钱 */
  paidAmount?: number | null
  /** 赞助日期 yyyy-mm-dd（补录用） */
  sponsoredAt?: string | null
  /** 同时给这个网站账号开通赞助特权 */
  perkUserId?: string | null
  perkDays?: number | null
}

export interface AfdianConfigInfo {
  userId: string
  maskedToken: string
  hasToken: boolean
  enabled: boolean
  lastSyncAt: string | null
}

export const sponsorApi = {
  list: () => request.get<SponsorWallData>('/Sponsor', { silentError: true }),
  fees: () => request.get<{ azz: number; afdian: number }>('/Sponsor/fees', { silentError: true }),
  searchUsers: (keyword: string) => request.get<{ id: string; username: string }[]>('/User/search', { params: { keyword }, silentError: true }),
  create: (payload: SponsorPayload) => request.post<{ id: number; amount: number; perkUntil?: string | null; warning?: string }>('/Sponsor', payload, { silentError: true }),
  update: (id: number, payload: SponsorPayload) => request.put<{ id: number }>(`/Sponsor/${id}`, payload, { silentError: true }),
  remove: (id: number) => request.delete(`/Sponsor/${id}`),
  getAfdianConfig: () => request.get<AfdianConfigInfo>('/Sponsor/config', { silentError: true }),
  saveAfdianConfig: (payload: { userId: string; token?: string; enabled: boolean }) =>
    request.put<{ message: string }>('/Sponsor/config', payload, { silentError: true }),
  syncAfdian: () => request.post<{ ordersCreated: number; sponsorsSynced: number }>('/Sponsor/sync', {}, { silentError: true })
}
