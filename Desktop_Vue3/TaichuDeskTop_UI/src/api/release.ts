import request from '@/utils/request'

export interface ReleaseItem {
  id: number
  version: string
  title: string
  content: string | null
  releasedAt: string
  isPublished: boolean
  createdAt: string
}

export interface ReleasePayload {
  version: string
  title: string
  content?: string | null
  releasedAt?: string | null
  isPublished: boolean
}

export const releaseApi = {
  list: () => request.get<ReleaseItem[]>('/Release', { silentError: true }),
  latest: () => request.get<ReleaseItem | null>('/Release/latest', { silentError: true }),
  create: (payload: ReleasePayload) => request.post<{ id: number }>('/Release', payload, { silentError: true }),
  update: (id: number, payload: ReleasePayload) => request.put<{ id: number }>(`/Release/${id}`, payload, { silentError: true }),
  remove: (id: number) => request.delete(`/Release/${id}`)
}
