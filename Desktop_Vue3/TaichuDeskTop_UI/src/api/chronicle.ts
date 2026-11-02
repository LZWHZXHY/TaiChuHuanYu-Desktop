// src/api/chronicle.ts：世界编年史（多条时间线、分叉汇合、时期、时间跳跃、自定义历法）
import request from '@/utils/request'
import { toChronicleExport, type ChronicleEvent, type ChronicleExport, type ChronicleTimeline, type EventKind, type TimelineStatus } from '@/utils/chronicle'

export interface ChronicleData {
  calendar: unknown
  timelines: ChronicleTimeline[]
  events: ChronicleEvent[]
  canEdit: boolean
  /** 创建者和协作者能看修改记录（老后端没有这一项） */
  canViewHistory?: boolean
}
export interface TimelineInput {
  name: string
  description?: string | null
  color?: string | null
  status?: TimelineStatus
  parentId?: string | null
  forkEventId?: string | null
  mergeEventId?: string | null
  cardId?: string | null
  sortOrder?: number | null
  hidden?: boolean
}
export interface EventInput {
  timelineId: string
  kind: EventKind
  title: string
  description?: string | null
  eraId?: string | null
  year?: number | null
  month?: number | null
  day?: number | null
  endEraId?: string | null
  endYear?: number | null
  endMonth?: number | null
  endDay?: number | null
  dateText?: string | null
  color?: string | null
  images: string[]
  tags: string[]
  cardIds: string[]
  locationCardId?: string | null
  hidden?: boolean
  sortOrder?: number | null
}
export interface CardChronicle {
  calendar: unknown
  events: ChronicleEvent[]
  timelines: { id: string; name: string; color: string | null; status: TimelineStatus }[]
  /** 关于这个条目的时间线（人物生平……） */
  ownLines: string[]
}

// ===== 修改记录（时间线 / 事件 / 历法的新建、修改、删除，可以撤销） =====
export type ChronicleTarget = 'timeline' | 'event' | 'calendar'
export type ChronicleAction = 'create' | 'update' | 'delete' | 'restore'
export interface ChronicleRevision {
  id: number
  targetType: ChronicleTarget
  targetId: string
  userId: string | null
  username: string | null
  avatar: string | null
  action: ChronicleAction
  /** 当时的名称（事件名可能带 {CARD:id} 引用） */
  title: string
  /** 这次改了哪些字段（name、year……），新建 / 删除时为空 */
  fields: string[]
  note: string | null
  createdAt: string
  /** 这条线 / 这件事现在还在不在 */
  exists: boolean
}
/** 改之前 / 改之后的完整内容（时间线、事件的各个字段，或整个历法） */
export interface ChronicleRevisionDetail {
  id: number
  targetType: ChronicleTarget
  targetId: string
  action: ChronicleAction
  before: Record<string, any> | null
  after: Record<string, any> | null
}
export interface ChronicleHistoryQuery {
  targetType?: ChronicleTarget
  targetId?: string
  userId?: string
  action?: ChronicleAction
  before?: number
  take?: number
}

const base = (pid: string) => `/world/projects/${pid}/chronicle`

export const chronicleApi = {
  get: (pid: string) => request.get<ChronicleData>(base(pid), { silentError: true }),
  byCard: (pid: string, cardId: string) => request.get<CardChronicle>(`${base(pid)}/by-card/${cardId}`, { silentError: true }),
  saveCalendar: (pid: string, calendar: unknown) => request.put<{ calendar: unknown }>(`${base(pid)}/calendar`, calendar),
  createTimeline: (pid: string, body: TimelineInput) => request.post<ChronicleTimeline>(`${base(pid)}/timelines`, body),
  updateTimeline: (pid: string, id: string, body: TimelineInput) => request.put<ChronicleTimeline>(`${base(pid)}/timelines/${id}`, body),
  deleteTimeline: (pid: string, id: string) => request.delete(`${base(pid)}/timelines/${id}`),
  reorderTimelines: (pid: string, ids: string[]) => request.post(`${base(pid)}/timelines/reorder`, { ids }),
  createEvent: (pid: string, body: EventInput) => request.post<ChronicleEvent>(`${base(pid)}/events`, body),
  updateEvent: (pid: string, id: string, body: EventInput) => request.put<ChronicleEvent>(`${base(pid)}/events/${id}`, body),
  deleteEvent: (pid: string, id: string) => request.delete(`${base(pid)}/events/${id}`),
  /** 导出 / 备份用：整本编年史（成员能拿到隐藏的线和事件）；没写过编年史返回 null。读不到就报错，免得备份里悄悄少了编年史 */
  exportData: async (pid: string): Promise<ChronicleExport | null> => {
    try { return toChronicleExport(await request.get<ChronicleData>(base(pid), { silentError: true })) }
    catch { throw new Error('编年史读取失败，请重试导出。') }
  },
  // 修改记录：列表（新的在前）、某一次的改前改后、撤销某一次
  history: (pid: string, params: ChronicleHistoryQuery = {}) =>
    request.get<{ items: ChronicleRevision[]; hasMore: boolean }>(`${base(pid)}/history`, { params, silentError: true }),
  revision: (pid: string, id: number) => request.get<ChronicleRevisionDetail>(`${base(pid)}/history/${id}`, { silentError: true }),
  undo: (pid: string, id: number) => request.post<{ message: string }>(`${base(pid)}/history/${id}/undo`, {}, { silentError: true }),
}
