// src/api/history.ts：岁月史书（社区日常：群聊名场面、截图、生活、成果进步）
import request from '@/utils/request'

export type HistoryKind = 'funny' | 'chat' | 'life' | 'progress' | 'event' | 'other'
export type HistoryPrecision = 'day' | 'month' | 'year'
export type ReactionKind = 'lol' | 'touched' | 'awesome' | 'hug'

export interface HistoryComment {
  id: number
  entryId: number
  content: string
  createdAt: string
  author: { id: string; username: string; avatar: string | null }
  replyToId: number | null
  replyTo: { id: string; username: string } | null
  /** 评论里的图片（最多 4 张） */
  images: string[]
  canDelete: boolean
}
export interface HistoryEntry {
  id: number
  kind: HistoryKind
  title: string
  content: string | null
  images: string[]
  /** 发生在哪天 yyyy-MM-dd（只记得月 / 年时是那个月 / 那年的第一天） */
  happenedOn: string
  precision: HistoryPrecision
  /** 上传时间 */
  createdAt: string
  author: { id: string; username: string; avatar: string | null }
  hidden: boolean
  hiddenReason: string | null
  editedAt: string | null
  editedByName: string | null
  reactions: Partial<Record<ReactionKind, number>>
  myReactions: ReactionKind[]
  canDelete: boolean
  commentCount: number
  /** 最近两条评论（列表里先显示） */
  recentComments: HistoryComment[]
  /** 这一段 @ 了谁 */
  mentions: { id: string; username: string }[]
}
export interface HistoryPage {
  items: HistoryEntry[]
  hasMore: boolean
  years: { year: number; count: number }[]
  onThisDay: HistoryEntry[]
  canPost: boolean
  canManage: boolean
  dailyLimit: number
  /** 按人筛选时：是谁 */
  person: { id: string; username: string } | null
}
export interface OnThisDay {
  /** day = 往年的今天；month = 今天没有，给的是往年的这个月 */
  mode: 'day' | 'month'
  today: string
  items: HistoryEntry[]
}
export interface Yearbook {
  year: number
  items: HistoryEntry[]
  years: { year: number; count: number }[]
  contributors: number
}
export interface HistoryInput {
  kind: HistoryKind
  title: string
  content: string
  images: string[]
  happenedOn: string
  precision: HistoryPrecision
  /** @ 了谁（用户编号） */
  mentions?: string[]
}

export const historyApi = {
  list: (params: { kind?: HistoryKind; year?: number; q?: string; mine?: boolean; author?: string; person?: string; page?: number }) =>
    request.get<HistoryPage>('/history', { params, silentError: true }),
  get: (id: number) => request.get<HistoryEntry>(`/history/${id}`, { silentError: true }),
  onThisDay: () => request.get<OnThisDay>('/history/on-this-day', { silentError: true }),
  yearbook: (year: number) => request.get<Yearbook>('/history/yearbook', { params: { year }, silentError: true }),
  create: (body: HistoryInput) => request.post<HistoryEntry>('/history', body, { silentError: true }),
  edit: (id: number, body: HistoryInput) => request.put<HistoryEntry>(`/history/${id}`, body, { silentError: true }),
  remove: (id: number) => request.delete(`/history/${id}`, { silentError: true }),
  hide: (id: number, reason: string) => request.post<HistoryEntry>(`/history/${id}/hide`, { reason }, { silentError: true }),
  unhide: (id: number) => request.post<HistoryEntry>(`/history/${id}/unhide`, {}, { silentError: true }),
  react: (id: number, kind: ReactionKind) =>
    request.post<{ reactions: HistoryEntry['reactions']; myReactions: ReactionKind[] }>(`/history/${id}/react`, { kind }, { silentError: true }),
  comments: (id: number) => request.get<HistoryComment[]>(`/history/${id}/comments`, { silentError: true }),
  addComment: (id: number, content: string, replyToId?: number | null, images: string[] = []) =>
    request.post<HistoryComment>(`/history/${id}/comments`, { content, replyToId: replyToId ?? null, images }, { silentError: true }),
  deleteComment: (commentId: number) => request.delete(`/history/comments/${commentId}`, { silentError: true }),
}

export const KIND_META: Record<HistoryKind, { label: string; icon: string; hint: string }> = {
  funny: { label: '趣事', icon: '😂', hint: '好笑的事、名场面、梗的由来' },
  chat: { label: '群聊截图', icon: '💬', hint: '群里的发言截图' },
  life: { label: '生活', icon: '🌱', hint: '日常、出去玩、吃了什么、见面' },
  progress: { label: '成果进步', icon: '🏆', hint: '做出了什么、学会了什么、考过了什么' },
  event: { label: '社区大事', icon: '📌', hint: '开服、上线、活动、联动' },
  other: { label: '其他', icon: '✨', hint: '' },
}
export const REACTION_META: Record<ReactionKind, { icon: string; label: string }> = {
  lol: { icon: '😂', label: '笑死' },
  touched: { icon: '🥹', label: '泪目' },
  awesome: { icon: '👍', label: '牛' },
  hug: { icon: '❤️', label: '抱抱' },
}
/** 显示错误信息：后端给的 message 优先 */
export const errorText = (e: any, fallback: string) => e?.response?.data?.message || e?.friendlyMessage || fallback
