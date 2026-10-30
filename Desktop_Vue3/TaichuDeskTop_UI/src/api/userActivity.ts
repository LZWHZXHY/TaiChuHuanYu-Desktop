// src/api/userActivity.ts：个人主页的「社区活跃」统计（只有次数，不含内容）
import request from '@/utils/request'
/** 活跃日历的一天：n = 那天做了几件事，k = 各类分别几件 */
export interface ActivityDay { d: string; n: number; k?: Record<string, number> }

export interface UserActivity {
  joinedAt: string
  /** 加入第几天 */
  days: number
  firstActiveAt: string | null
  totals: {
    events: number
    activeDays: number
    currentStreak: number
    longestStreak: number
    last7: number
    last30: number
    likes: number
    followers: number
    maxSignStreak: number
    /** 上站天数（只有本人能拿到） */
    visitDays: number | null
  }
  /** 各类活动的次数（多的在前） */
  kinds: { key: string; label: string; count: number }[]
  heatmap: ActivityDay[]
  /** 在岁月史书里记的：总共几段 + 最近三段（旧后端没有这一项） */
  history?: {
    count: number
    recent: { id: number; kind: string; title: string; happenedOn: string; precision: 'day' | 'month' | 'year'; cover: string | null }[]
  }
  hours: number[]
  weekdays: number[]
  isSelf: boolean
}

export const userActivityApi = {
  get: (userId: string) => request.get<UserActivity>(`/users/${userId}/activity`, { silentError: true }),
}
