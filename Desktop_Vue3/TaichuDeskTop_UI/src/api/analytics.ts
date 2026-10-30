import request from '@/utils/request'

export interface OnlineUser { id: string; name: string; avatar?: string | null; secondsAgo: number }
export interface DayCounts { active: number; signed: number; newUsers: number }
export interface LiveStats {
  now: string
  onlineWindowMinutes: number
  online: { total: number; members: number; guests: number }
  peak: { total: number; at: string }
  onlineUsers: OnlineUser[]
  totalUsers: number
  today: DayCounts & { visitors: number }
  yesterdaySoFar: DayCounts
  yesterday: DayCounts
}

export interface TrendRow {
  date: string
  /** 开始记录之前的日期为 null（没有数据，而不是 0） */
  active: number | null
  visitors: number | null
  peakOnline: number | null
  signed: number
  newUsers: number
}
export interface PeriodSummary { uniqueActive: number | null; uniqueSigners: number; newUsers: number; partial: boolean }
export interface TrendStats {
  days: number
  trackingSince: string | null
  current: TrendRow[]
  previous: TrendRow[]
  summary: { current: PeriodSummary; previous: PeriodSummary }
  retention: { d1: { users: number; returned: number }; d7: { users: number; returned: number } }
}

export interface CurvePoint { minute: number; total: number; members: number }
export interface OnlineCurve { date: string; points: CurvePoint[] }
export interface OnlineCurves { day: OnlineCurve; previousDay: OnlineCurve; lastWeek: OnlineCurve }

export interface SignStats {
  activeStreakUsers: number
  distribution: { label: string; count: number }[]
  hours: number[]
  weekdays: { label: string; average: number }[]
  topStreaks: { id: string; name: string; avatar?: string | null; streak: number; best: number }[]
}

/** 某个板块的内容统计 */
export interface ContentModule {
  key: string
  label: string
  group: string
  note: string | null
  /** 全部（含下架 / 私密 / 审核中） */
  total: number
  /** 大家看得到的（这个板块没有「公开」概念时为 null） */
  visible: number | null
  /** 最近 N 天新增 / 之前 N 天新增 / 今天 */
  current: number
  previous: number
  today: number
  /** 最近 N 天每天新增几条（和 dates 对应） */
  series: number[]
  error: string | null
}
export interface ContentStats { days: number; dates: string[]; modules: ContentModule[] }

export const analyticsApi = {
  live: () => request.get<LiveStats>('/Admin/Analytics/live', { silentError: true }),
  trend: (days: number) => request.get<TrendStats>('/Admin/Analytics/trend', { params: { days }, silentError: true }),
  onlineCurve: (date?: string) => request.get<OnlineCurves>('/Admin/Analytics/online-curve', { params: { date }, silentError: true }),
  sign: () => request.get<SignStats>('/Admin/Analytics/sign', { silentError: true }),
  content: (days: number) => request.get<ContentStats>('/Admin/Analytics/content', { params: { days }, silentError: true }),
}
