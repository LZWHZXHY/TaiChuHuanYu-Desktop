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

export interface SponsorPayload {
  name: string
  tier: string
  amount: number
  message?: string | null
  isAnonymous: boolean
  isVisible: boolean
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
  create: (payload: SponsorPayload) => request.post<{ id: number }>('/Sponsor', payload, { silentError: true }),
  update: (id: number, payload: SponsorPayload) => request.put<{ id: number }>(`/Sponsor/${id}`, payload, { silentError: true }),
  remove: (id: number) => request.delete(`/Sponsor/${id}`),
  getAfdianConfig: () => request.get<AfdianConfigInfo>('/Sponsor/config', { silentError: true }),
  saveAfdianConfig: (payload: { userId: string; token?: string; enabled: boolean }) =>
    request.put<{ message: string }>('/Sponsor/config', payload, { silentError: true }),
  syncAfdian: () => request.post<{ ordersCreated: number; sponsorsSynced: number }>('/Sponsor/sync', {}, { silentError: true })
}
