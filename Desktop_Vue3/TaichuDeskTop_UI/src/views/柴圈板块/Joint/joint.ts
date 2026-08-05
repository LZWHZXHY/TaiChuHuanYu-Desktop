import request from '@/utils/request'

// ============================================================
// 类型定义
// ============================================================

export type JointStatus = 'open' | 'closed' | 'ended' | 'banned' | 'abandoned'
export type JointType = 'joint' | 'relay' | 'project' | 'free' | 'other'
export type ParticipantStatus = 'pending' | 'approved' | 'rejected'

export interface JointParticipant {
  id: string
  userId: string
  userName: string
  status: ParticipantStatus
  remark?: string
  createdAt: string
}

export interface JointActivity {
  id: string
  title: string
  description: string
  requirements?: string
  contact?: string
  type: JointType
  status: JointStatus
  auditRequired: boolean
  coverUrl?: string
  organizerId: string
  organizerName: string
  participantCount: number
  createdAt: string
  updatedAt?: string
  participants?: JointParticipant[]
}

export interface CreateJointRequest {
  title: string
  description: string
  requirements?: string
  contact?: string
  type: JointType
  status: JointStatus
  auditRequired: boolean
  coverUrl?: string
}

export interface UpdateJointRequest {
  title?: string
  description?: string
  requirements?: string
  contact?: string
  type?: JointType
  status?: JointStatus
  auditRequired?: boolean
  coverUrl?: string
}

export interface JointListResponse {
  items: JointActivity[]
  total: number
  page: number
  pageSize: number
}

// ============================================================
// API 函数
// ============================================================

export const jointApi = {
  // ===== 活动 CRUD =====
  getList: (params?: {
    page?: number
    pageSize?: number
    keyword?: string
    status?: JointStatus
    type?: JointType
  }) => request.get<JointListResponse>('/Joint', { params }),

  getDetail: (id: string) => request.get<JointActivity>(`/Joint/${id}`),

  create: (data: CreateJointRequest) => request.post<JointActivity>('/Joint', data),

  update: (id: string, data: UpdateJointRequest) =>
    request.put<JointActivity>(`/Joint/${id}`, data),

  delete: (id: string) => request.delete(`/Joint/${id}`),

  // ===== 我的活动 =====
  getMyOrganized: (params?: { status?: JointStatus }) =>
    request.get<JointActivity[]>('/Joint/my/organized', { params }),

  getMyParticipated: (params?: { status?: JointStatus }) =>
    request.get<JointActivity[]>('/Joint/my/participated', { params }),

  // ===== 参与者操作 =====
  join: (id: string, remark?: string) =>
    request.post<JointActivity>(`/Joint/${id}/join`, { remark }),

  cancelJoin: (id: string) =>
    request.post<JointActivity>(`/Joint/${id}/cancel`),

  auditParticipant: (activityId: string, userId: string, status: ParticipantStatus) =>
    request.post<JointActivity>(`/Joint/${activityId}/audit`, { userId, status }),

  kickParticipant: (activityId: string, userId: string) =>
    request.post<JointActivity>(`/Joint/${activityId}/kick`, { userId }),

  // ===== 管理员操作 =====
  toggleBan: (id: string) =>
    request.post<JointActivity>(`/Joint/${id}/toggle-ban`),
}