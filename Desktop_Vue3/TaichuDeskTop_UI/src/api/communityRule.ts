import request from '@/utils/request'

export interface CommunityRuleItem {
  id: number
  parentId: number | null
  code: string | null
  title: string
  content: string | null
  penalty: string | null
  sortOrder: number
  isActive: boolean
  version: string
  createdAt: string
  updatedAt: string
}

export interface CommunityRulePayload {
  parentId: number | null
  code: string | null
  title: string
  content: string | null
  penalty: string | null
  sortOrder: number
  isActive: boolean
  version: string
}

export const communityRuleApi = {
  list: () => request.get<CommunityRuleItem[]>('/CommunityRule', { silentError: true }),
  meta: () => request.get<{ version: string; updatedAt: string }>('/CommunityRule/meta', { silentError: true }),
  create: (payload: CommunityRulePayload) => request.post<{ id: number }>('/CommunityRule', payload, { silentError: true }),
  update: (id: number, payload: CommunityRulePayload) => request.put<{ id: number }>(`/CommunityRule/${id}`, payload, { silentError: true }),
  remove: (id: number) => request.delete(`/CommunityRule/${id}`)
}
