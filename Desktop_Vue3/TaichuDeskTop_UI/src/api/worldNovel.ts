import request from '@/utils/request'

export interface NovelBrief {
  id: string
  title: string
  description: string | null
  coverUrl: string | null
  status: string
  authorName: string
  authorId: string
  projectId: string | null
  projectName?: string | null
  views: number
  wordCount: number
  isPublic: boolean
  updatedAt: string
  tags: string[]
  chapterCount: number
}

export interface NovelChapterBrief {
  id: string
  number: number
  title: string
  wordCount: number
  updatedAt: string
}

export interface NovelDetail {
  id: string
  title: string
  description: string | null
  coverUrl: string | null
  status: string
  authorName: string
  authorId: string
  projectId: string | null
  projectName?: string | null
  views: number
  wordCount: number
  isPublic: boolean
  createdAt: string
  updatedAt: string
  tags: string[]
  isOwner: boolean
  chapters: NovelChapterBrief[]
}

export interface ChapterContent {
  id: string
  number: number
  title: string
  content: string
  wordCount: number
  updatedAt: string
  prevId: string | null
  nextId: string | null
  index: number
  total: number
}

export interface NovelPayload {
  title: string
  description?: string | null
  coverUrl?: string | null
  status?: string
  projectId?: string | null
  tags?: string[]
  isPublic?: boolean
}

export const worldNovelApi = {
  list: (params: { projectId?: string; mine?: boolean } = {}) =>
    request.get<NovelBrief[]>('/world/novels', { params, silentError: true }),
  detail: (id: string) => request.get<NovelDetail>(`/world/novels/${id}`, { silentError: true }),
  chapter: (id: string, chapterId: string) =>
    request.get<ChapterContent>(`/world/novels/${id}/chapters/${chapterId}`, { silentError: true }),
  create: (payload: NovelPayload) => request.post<{ id: string }>('/world/novels', payload, { silentError: true }),
  update: (id: string, payload: NovelPayload) =>
    request.put<{ id: string; isPublic: boolean }>(`/world/novels/${id}`, payload, { silentError: true }),
  remove: (id: string) => request.delete(`/world/novels/${id}`),
  addChapter: (id: string, payload: { title: string; content: string; number?: number }) =>
    request.post<{ id: string; number: number }>(`/world/novels/${id}/chapters`, payload, { silentError: true }),
  updateChapter: (id: string, chapterId: string, payload: { title: string; content: string; number?: number }) =>
    request.put<{ id: string }>(`/world/novels/${id}/chapters/${chapterId}`, payload, { silentError: true }),
  removeChapter: (id: string, chapterId: string) =>
    request.delete(`/world/novels/${id}/chapters/${chapterId}`)
}
