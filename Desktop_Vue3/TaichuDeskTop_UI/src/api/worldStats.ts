// src/api/worldStats.ts：世界观创作统计（创建者和协作者看完整的；公开世界的访客看公开的那部分）
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

/** 里程碑种类：词条数 / 设定字数 / 小说字数 / 最长连续创作 / 累计创作天数 / 世界建立多久 */
export type MilestoneKind = 'cards' | 'words' | 'novel' | 'streak' | 'active' | 'age'

/** 一个创作里程碑（里程碑墙上的一块徽章） */
export interface WorldMilestone {
  key: string
  kind: MilestoneKind
  /** 完整名字：「第 100 个词条」「设定 10 万字」「建立满 1 年」 */
  title: string
  /** 徽章上的大字：「100」「10 万」「1 年」 */
  label: string
  threshold: number
  achieved: boolean
  /** 哪天达成的；上线前就达成、又推算不出日期的为 null */
  achievedAt: string | null
  /** 里程碑功能上线前就达成了（静默补记，没发通知；日期是推算的） */
  backfilled: boolean
  /** 刚刚达成，后台还没记下来（最多 30 分钟） */
  pending: boolean
  /** 还差多少：词条个数 / 字数 / 天数；连续创作 = 按当前连续还差几天；建立满 N = 还有几天 */
  remaining: number
  /** 进度 0～1 */
  progress: number
}

export interface WorldStats {
  /** 成员看到的完整统计（老缓存里没有这个字段，也当成员版） */
  mode?: 'member'
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
  /** 里程碑墙（全部里程碑，达成的点亮）；老缓存里可能没有 */
  milestones?: WorldMilestone[]
}

/** 公开世界的访客看到的统计：只算对访客公开的内容，不显示是谁写的、不显示修改记录 */
export interface PublicWorldStats {
  mode: 'public'
  name: string
  createdAt: string
  /** 世界建立到今天第几天 */
  days: number
  /** 这份统计是什么时候算的（后端缓存 10 分钟） */
  generatedAt: string
  totals: {
    /** 词条数（访客能看到的） */
    cards: number
    /** 设定字数：对访客隐藏的区块、时间线事件不算 */
    words: number
    /** 公开小说的字数 */
    novelWords: number
    novels: number
    activeDays: number
    lastActiveAt: string | null
  }
  /** 活跃日历：只有日期和次数 */
  heatmap: HeatDay[]
  /** 只有已达成的里程碑 */
  milestones: WorldMilestone[]
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
  /** 成员拿到完整统计（mode = 'member'），公开世界的访客拿到公开的那部分（mode = 'public'） */
  project: (projectId: string) => request.get<WorldStats | PublicWorldStats>(`/world/projects/${projectId}/stats`, { silentError: true }),
  me: () => request.get<MyWorldStats>('/world/stats/me', { silentError: true }),
}
