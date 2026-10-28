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
  create: (payload: DevProjectPayload) => request.post<{ id: string }>('/DevProject', payload, { silentError: true }),
  update: (id: string, payload: DevProjectPayload) => request.put<{ id: string }>(`/DevProject/${id}`, payload, { silentError: true }),
  remove: (id: string) => request.delete(`/DevProject/${id}`)
}
