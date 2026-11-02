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
  /** 所在的部 / 卷 / 番外（没分组为空） */
  sectionId?: string | null
  /** 第几章（按阅读顺序；番外里的章为空） */
  displayNo?: number | null
}

/** part 部 / volume 卷 / extra 番外（作者可以改名成外传、后记……） */
export type SectionKind = 'part' | 'volume' | 'extra'
export interface NovelSection {
  id: string
  parentId?: string | null
  kind: SectionKind
  title: string
  note?: string | null
  sortOrder: number
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
  /** 按阅读顺序排好 */
  chapters: NovelChapterBrief[]
  /** 部 / 卷 / 番外（后端没更新时没有） */
  sections?: NovelSection[]
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
  sectionId?: string | null
  /** 「第一部 · 第二卷」 */
  sectionPath?: string | null
  displayNo?: number | null
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
  addChapter: (id: string, payload: { title: string; content: string; number?: number; sectionId?: string | null }) =>
    request.post<{ id: string; number: number }>(`/world/novels/${id}/chapters`, payload, { silentError: true }),
  updateChapter: (id: string, chapterId: string, payload: { title: string; content: string; number?: number }) =>
    request.put<{ id: string }>(`/world/novels/${id}/chapters/${chapterId}`, payload, { silentError: true }),
  removeChapter: (id: string, chapterId: string) =>
    request.delete(`/world/novels/${id}/chapters/${chapterId}`),

  // ===== 部 / 卷 / 番外（fix175） =====
  createSection: (id: string, payload: { kind: SectionKind; title: string; note?: string | null; parentId?: string | null }) =>
    request.post<{ id: string }>(`/world/novels/${id}/sections`, payload, { silentError: true }),
  updateSection: (id: string, sectionId: string, payload: { title: string; note?: string | null }) =>
    request.put(`/world/novels/${id}/sections/${sectionId}`, payload, { silentError: true }),
  moveSection: (id: string, sectionId: string, direction: -1 | 1) =>
    request.post(`/world/novels/${id}/sections/${sectionId}/move`, { direction }, { silentError: true }),
  removeSection: (id: string, sectionId: string) =>
    request.delete(`/world/novels/${id}/sections/${sectionId}`, { silentError: true }),
  /** 把一章放进某个分组（null = 拿出来） */
  setChapterSection: (id: string, chapterId: string, sectionId: string | null) =>
    request.put(`/world/novels/${id}/chapters/${chapterId}/section`, { sectionId }, { silentError: true }),
  /** 在同一个分组里上移 / 下移 */
  moveChapter: (id: string, chapterId: string, direction: -1 | 1) =>
    request.post(`/world/novels/${id}/chapters/${chapterId}/move`, { direction }, { silentError: true }),
}
