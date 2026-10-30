// src/api/members.ts：社区成员列表 + 在线人数（不用登录也能看）
import request from '@/utils/request'

export interface OnlineCount { members: number; guests: number; total: number }
export interface MemberItem {
  id: string
  username: string
  avatar: string | null
  level: number
  joinedAt: string
  online: boolean
  /** 在线的人：最近一次动静；不在线的人：最后一次来（统计开始前没来过的是 null） */
  lastSeenAt: string | null
}
export interface MemberPage {
  total: number
  online: OnlineCount & { matched: number }
  items: MemberItem[]
  hasMore: boolean
}
export type MemberSort = 'joined' | 'old' | 'level' | 'name'

export const membersApi = {
  onlineCount: () => request.get<OnlineCount>('/members/online-count', { silentError: true }),
  list: (params: { q?: string; sort?: MemberSort; onlineOnly?: boolean; page?: number; pageSize?: number }) =>
    request.get<MemberPage>('/members', { params, silentError: true }),
}
