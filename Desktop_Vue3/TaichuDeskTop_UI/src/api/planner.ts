// src/api/planner.ts
// 太初灵脉 · 生活管理（个人日历）的接口。
// 日期都是 "YYYY-MM-DD"、时间都是 "HH:mm"（用户自己这边的本地时间，和看到的一样）；
// 后端给的 nextRemindAt / eventAt 这些是 UTC 时间（带 Z），用 new Date(x) 解析。
import request from '@/utils/request'

export type PlannerRepeat = 'none' | 'daily' | 'weekly' | 'monthly'
export type PlannerChannel = 'site' | 'browser' | 'email'

export interface PlannerEvent {
  id: string
  title: string
  date: string
  startTime: string | null
  endTime: string | null
  allDay: boolean
  color: string
  category: string
  memo: string
  location: string
  isDone: boolean
  /** 重复事项：哪几天已经勾了完成 */
  doneDates: string[]
  repeat: PlannerRepeat
  repeatUntil: string | null
  /** 提前多少分钟提醒；null = 不提醒 */
  remindOffset: number | null
  /** 自定义提醒时间（本地 "YYYY-MM-DDTHH:mm"） */
  remindAt: string | null
  channels: PlannerChannel[]
  nextRemindAt: string | null
  createdAt: string
  updatedAt: string
}

export interface PlannerEventInput {
  title: string
  date: string
  startTime: string | null
  endTime: string | null
  allDay: boolean
  color: string
  category: string
  memo: string
  location: string
  isDone?: boolean
  repeat: PlannerRepeat
  repeatUntil: string | null
  remindOffset: number | null
  remindAt: string | null
  channels: PlannerChannel[]
  /** 浏览器所在时区比 UTC 快多少分钟，后端用它算提醒的 UTC 时间 */
  tzOffset: number
}

export interface PlannerAccess {
  /** 能进来看（有特权，或者以前用过、有数据） */
  canUse: boolean
  /** 能新建 / 修改 / 删除 / 勾选，提醒照常 */
  canEdit: boolean
  /** 能用邮件提醒 */
  canEmail: boolean
  /** 赞助到期：只能看和导出 */
  readOnly: boolean
  needTier: string
  emailTier: string
  until: string | null
  tier: string | null
  isAdmin: boolean
  hasData: boolean
  hasEmailAddress: boolean
}

export interface PlannerSaveResult {
  item: PlannerEvent
  /** 比如「邮件提醒是君者赞助专属，这次先去掉了邮件」 */
  notice?: string | null
}

export interface PlannerUpcoming {
  id: string
  title: string
  location: string
  allDay: boolean
  eventAt: string | null
  remindAt: string
  date: string
}

// 错误提示由页面自己显示（silentError），不弹全局错误框
const quiet = { silentError: true }

export const plannerApi = {
  access: () => request.get<PlannerAccess>('/planner/access', quiet),
  /** from / to 都不传 = 全部（导出用） */
  list: (from?: string, to?: string) => {
    const q = from && to ? `?from=${from}&to=${to}` : ''
    return request.get<PlannerEvent[]>(`/planner/events${q}`, quiet)
  },
  create: (input: PlannerEventInput) => request.post<PlannerSaveResult>('/planner/events', input, quiet),
  update: (id: string, input: PlannerEventInput) => request.put<PlannerSaveResult>(`/planner/events/${id}`, input, quiet),
  /** 重复事项带 date = 只勾那一天 */
  setDone: (id: string, done: boolean, date?: string) =>
    request.patch<{ item: PlannerEvent }>(`/planner/events/${id}/done`, { done, date: date || null }, quiet),
  /** 软删除，可以用 restore 撤销 */
  remove: (id: string) => request.delete<{ ok: boolean }>(`/planner/events/${id}`, quiet),
  restore: (id: string) => request.post<{ item: PlannerEvent }>(`/planner/events/${id}/restore`, {}, quiet),
  upcoming: () => request.get<PlannerUpcoming[]>('/planner/reminders/upcoming', quiet),
}
