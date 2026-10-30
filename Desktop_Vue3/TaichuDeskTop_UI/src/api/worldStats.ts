// src/api/worldStats.ts：世界观创作统计（只有创建者和协作者能看）
import request from '@/utils/request'

/** 活跃日历的一天：d = 北京时间日期 yyyy-MM-dd，n = 那天的创作次数（新建 + 修改 + 删除 + 恢复） */
export interface HeatDay { d: string; n: number }

export interface StreakTotals {
  activeDays: number
  /** 当前连续创作天数（今天还没写不算断） */
  currentStreak: number
  longestStreak: number
  last7: number
  last30: number
}

export interface WorldStatsTreeNode {
  type: string
  count: number
  words: number
  subTypes: { name: string; count: number }[]
  cards: { id: string; title: string; subType: string | null; words: number }[]
}

export interface WorldStats {
  name: string
  createdAt: string
  /** 世界建立到今天第几天 */
  days: number
  /** 修改记录从什么时候开始有（之前的修改没有记录） */
  trackingSince: string | null
  totals: StreakTotals & {
    cards: number
    words: number
    novelWords: number
    novels: number
    relations: number
    timelineEvents: number
    images: number
    edits: number
    creates: number
    updates: number
    deletes: number
    restores: number
    lastActiveAt: string | null
  }
  tree: WorldStatsTreeNode[]
  heatmap: HeatDay[]
  /** 0~23 点（北京时间）各有多少次 */
  hours: number[]
  /** 周一 ~ 周日 */
  weekdays: number[]
  contributors: { userId: string; username: string | null; avatar: string | null; isOwner: boolean; edits: number; creates: number; activeDays: number; lastAt: string }[]
  topEdited: { id: string; title: string; exists: boolean; count: number }[]
  longest: { id: string; title: string; words: number }[]
}

export interface MyWorldStats {
  firstAt: string | null
  /** 从第一次创作到今天第几天 */
  days: number
  totals: StreakTotals & { worlds: number; joined: number; cards: number; words: number; edits: number; creates: number }
  heatmap: HeatDay[]
  hours: number[]
  weekdays: number[]
  worlds: { id: string; name: string; isOwner: boolean; edits: number }[]
}

export const worldStatsApi = {
  project: (projectId: string) => request.get<WorldStats>(`/world/projects/${projectId}/stats`, { silentError: true }),
  me: () => request.get<MyWorldStats>('/world/stats/me', { silentError: true }),
}
