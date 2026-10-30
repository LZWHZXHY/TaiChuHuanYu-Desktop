// src/api/quests.ts：我的世界服务器「世界任务」
import request from '../utils/request'

export type QuestStatus = 'draft' | 'active' | 'completed'
/** count = 数量目标（收集 10000 块木头）；goal = 一次性目标（做出一台刷石机，做到就完成） */
export type QuestKind = 'count' | 'goal'
/** main = 主线（同时只有一个在进行）；side = 支线（可以同时进行好几个） */
export type QuestLine = 'main' | 'side'
export interface QuestRef { id: number; title: string; line: QuestLine }
export interface ServerQuest {
  id: number
  title: string
  kind?: QuestKind
  line?: QuestLine
  /** 前置任务：它完成后自动开启这个任务 */
  prereqId?: number | null
  prereqTitle?: string | null
  /** 完成后还会开启几个任务（不剧透名字） */
  followUpCount?: number
  /** 因为这个任务完成而开启了的任务 */
  unlocked?: QuestRef[]
  /** 管理员：完成后会开启哪些 */
  followUps?: QuestRef[] | null
  description: string | null
  unit: string
  target: number
  progress: number
  status: QuestStatus
  sortOrder: number
  /** 完成前公开的预告 */
  rewardTeaser: string | null
  /** 完成后公布的内容（完成前只有管理员拿得到） */
  rewardText: string | null
  rewardExp: number
  rewardPotato: number
  coverImage: string | null
  startedAt: string | null
  completedAt: string | null
  contributors: number
}
export interface QuestOverview {
  canManage: boolean
  /** 主线优先的第一个进行中任务 */
  active: ServerQuest | null
  /** 所有进行中的任务（主线在前，然后是支线） */
  actives?: ServerQuest[]
  completed: ServerQuest[]
  upcomingCount: number
  upcoming: ServerQuest[] | null
  pendingCount: number | null
  myPendingCount: number
}
export interface QuestBoardRow { userId: string | null; username: string | null; playerName: string | null; amount: number; times: number }
export interface QuestContribution {
  id: number
  questId: number
  questTitle?: string | null
  unit?: string | null
  questStatus?: QuestStatus
  questKind?: QuestKind
  userId?: string | null
  username?: string | null
  playerName: string | null
  amount: number
  note: string | null
  images: string[]
  /** 论坛里的报告帖 */
  postId?: string | null
  status?: 'pending' | 'approved' | 'rejected'
  source?: 'player' | 'admin'
  reviewNote?: string | null
  createdAt: string
  reviewedAt?: string | null
}
export interface QuestPayload {
  title: string
  kind: QuestKind
  line: QuestLine
  prereqId: number | null
  description?: string | null
  unit: string
  target: number
  rewardTeaser?: string | null
  rewardText?: string | null
  rewardExp: number
  rewardPotato: number
  coverImage?: string | null
}

export const questApi = {
  overview: () => request.get<QuestOverview>('/quests', { silentError: true }),
  leaderboard: (id: number) => request.get<QuestBoardRow[]>(`/quests/${id}/leaderboard`, { silentError: true }),
  mine: () => request.get<QuestContribution[]>('/quests/mine', { silentError: true }),
  contribute: (id: number, body: { amount: number; note?: string; playerName?: string; images?: string[]; postId?: string }) =>
    request.post<{ id: number; message: string }>(`/quests/${id}/contributions`, body, { silentError: true }),
  // ===== 管理员 =====
  pending: () => request.get<QuestContribution[]>('/quests/pending', { silentError: true }),
  review: (cid: number, body: { approve: boolean; amount?: number; note?: string }) =>
    request.post<{ message: string; completed?: boolean }>(`/quests/contributions/${cid}/review`, body, { silentError: true }),
  record: (id: number, body: { amount: number; username?: string; playerName?: string; note?: string }) =>
    request.post<{ message: string; completed?: boolean }>(`/quests/${id}/record`, body, { silentError: true }),
  setProgress: (id: number, progress: number) => request.put<{ progress: number; completed: boolean }>(`/quests/${id}/progress`, { progress }, { silentError: true }),
  create: (body: QuestPayload) => request.post<{ id: number; status: QuestStatus }>('/quests', body, { silentError: true }),
  update: (id: number, body: QuestPayload) => request.put<{ completed: boolean }>(`/quests/${id}`, body, { silentError: true }),
  remove: (id: number) => request.delete(`/quests/${id}`, { silentError: true }),
  start: (id: number) => request.post<{ message: string }>(`/quests/${id}/start`, {}, { silentError: true }),
  complete: (id: number) => request.post<{ message: string }>(`/quests/${id}/complete`, {}, { silentError: true }),
  reorder: (ids: number[]) => request.post('/quests/reorder', { ids }, { silentError: true }),
}
