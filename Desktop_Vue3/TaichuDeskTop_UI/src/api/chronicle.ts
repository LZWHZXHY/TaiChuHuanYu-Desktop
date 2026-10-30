// src/api/chronicle.ts：世界编年史（多条时间线、分叉汇合、时期、时间跳跃、自定义历法）
import request from '@/utils/request'
import type { ChronicleEvent, ChronicleTimeline, EventKind, TimelineStatus } from '@/utils/chronicle'

export interface ChronicleData {
  calendar: unknown
  timelines: ChronicleTimeline[]
  events: ChronicleEvent[]
  canEdit: boolean
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
}
