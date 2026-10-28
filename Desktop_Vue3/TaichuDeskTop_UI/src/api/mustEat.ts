import request from '@/utils/request'

export interface MustEatItem {
  id: number
  category: string
  title: string
  coverImage: string | null
  description: string | null
  creator: string | null
  link: string | null
  source: string | null
  sortOrder: number
  isVisible: boolean
  status: string
  submittedByUserId: string | null
  createdAt: string
  ratingCount: number
  averageScore: number
}

export interface MustEatRatingInfo {
  averageScore: number
  ratingCount: number
  myScore: number | null
}

export interface MustEatPayload {
  category: string
  title: string
  coverImage?: string | null
  description?: string | null
  creator?: string | null
  link?: string | null
  source?: string | null
  sortOrder: number
  isVisible: boolean
}

export const mustEatApi = {
  list: () => request.get<MustEatItem[]>('/MustEat', { silentError: true }),
  create: (payload: MustEatPayload) => request.post<{ id: number }>('/MustEat', payload, { silentError: true }),
  update: (id: number, payload: MustEatPayload) => request.put<{ id: number }>(`/MustEat/${id}`, payload, { silentError: true }),
  remove: (id: number) => request.delete(`/MustEat/${id}`),
  getRating: (id: number) => request.get<MustEatRatingInfo>(`/MustEat/${id}/rating`, { silentError: true }),
  rate: (id: number, score: number) => request.post<MustEatRatingInfo>(`/MustEat/${id}/rating`, { score }, { silentError: true }),
  submit: (payload: MustEatPayload) => request.post<{ id: number; message: string }>('/MustEat/submit', payload, { silentError: true }),
  pending: () => request.get<MustEatItem[]>('/MustEat/pending', { silentError: true }),
  approve: (id: number) => request.post<{ message: string }>(`/MustEat/${id}/approve`, {}, { silentError: true }),
  reject: (id: number) => request.post<{ message: string }>(`/MustEat/${id}/reject`, {}, { silentError: true }),
  fetchCover: (url: string) => request.get<{ coverImage: string | null; title: string | null }>(`/MustEat/fetch-cover?url=${encodeURIComponent(url)}`, { silentError: true })
}
