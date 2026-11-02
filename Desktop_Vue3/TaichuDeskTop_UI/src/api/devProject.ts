import request from '@/utils/request'

export interface Milestone {
  title: string
  done: boolean
}

export interface Reference {
  id: string
  title: string
  authors?: string | null
  venue?: string | null
  year?: string | null
  url?: string | null
  note?: string | null
}

export interface DataPoint {
  id: string
  name: string
  value?: string | null
  unit?: string | null
  note?: string | null
}

export interface ResearchLog {
  id: string
  date?: string | null
  title: string
  content?: string | null
  images?: string[]
  /** 写这条日志时的项目进度（可空） */
  progress?: number | null
}

export interface DevProject {
  id: string
  name: string
  description: string | null
  content: string | null
  category: string
  status: string
  progress: number
  icon: string
  sortOrder: number
  parentId: string | null
  updatedAt: string
  milestones: Milestone[]
  /** dev（开发）/ research（研究） */
  type: string
  references: Reference[]
  dataPoints: DataPoint[]
  logs: ResearchLog[]
  /** 没有权限时为 true：只有名字、图标、状态、进度，详细内容都是空的 */
  locked?: boolean
}

/** 开发进度详情谁能看：管理员（reason = admin）和赞助会员（reason = sponsor） */
export interface DevAccess {
  canView: boolean
  reason: 'admin' | 'sponsor' | null
  loggedIn: boolean
  /** 赞助特权到期时间（没赞助过为空） */
  sponsorUntil: string | null
  /** 赞助过但已到期 */
  expired: boolean
  /** 当前赞助等级 */
  tier?: string | null
  /** 赞助有效但等级不够（觉醒者） */
  tierTooLow?: boolean
  /** 需要的最低等级（超凡者） */
  needTier?: string
}

export interface DevProjectPayload {
  name: string
  description?: string | null
  content?: string | null
  category: string
  status: string
  progress: number
  milestones: Milestone[]
  type: string
  references: Reference[]
  dataPoints: DataPoint[]
  logs: ResearchLog[]
  icon: string
  sortOrder: number
  parentId?: string | null
}

export const devProjectApi = {
  list: () => request.get<DevProject[]>('/DevProject', { silentError: true }),
  access: () => request.get<DevAccess>('/DevProject/access', { silentError: true }),
  create: (payload: DevProjectPayload) => request.post<{ id: string }>('/DevProject', payload, { silentError: true }),
  update: (id: string, payload: DevProjectPayload) => request.put<{ id: string }>(`/DevProject/${id}`, payload, { silentError: true }),
  remove: (id: string) => request.delete(`/DevProject/${id}`)
}
