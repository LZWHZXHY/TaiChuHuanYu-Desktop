// src/api/members.ts：社区成员列表 + 在线人数（不用登录也能看）
import request from '@/utils/request'

/** members = 看得见的在线成员；hidden = 隐身的在线成员（只算人数）；total = 全部在线（含隐身和游客） */
export interface OnlineCount { members: number; hidden?: number; guests: number; total: number }
export interface MemberItem {
  id: string
  username: string
  avatar: string | null
  level: number
  joinedAt: string
  online: boolean
  /** 在线的人：最近一次动静；不在线的人：最后一次来（统计开始前没来过的是 null） */
  lastSeenAt: string | null
  /** 正在看哪个板块（chat、gallery……，见 utils/presence 的 SECTION_LABELS）；不在线或隐身的人没有 */
  section?: string | null
  /** 只有我自己那张卡片会是 true：我开着隐身 */
  invisible?: boolean
}
export interface MemberPage {
  total: number
  online: OnlineCount & { matched: number }
  /** 我是不是隐身（没登录是 null） */
  meInvisible: boolean | null
  items: MemberItem[]
  hasMore: boolean
}
export type MemberSort = 'joined' | 'old' | 'level' | 'name'

export const membersApi = {
  onlineCount: () => request.get<OnlineCount>('/members/online-count', { silentError: true }),
  list: (params: { q?: string; sort?: MemberSort; onlineOnly?: boolean; page?: number; pageSize?: number }) =>
    request.get<MemberPage>('/members', { params, silentError: true }),
  /** 隐身开关 */
  setInvisible: (invisible: boolean) => request.put<{ invisible: boolean; message: string }>('/Presence/me', { invisible }, { silentError: true }),
}
