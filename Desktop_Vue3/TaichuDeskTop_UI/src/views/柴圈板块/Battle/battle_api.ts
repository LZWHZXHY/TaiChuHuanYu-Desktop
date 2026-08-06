import request from '@/utils/request'

// ===== 类型定义 =====

export interface BattleParticipant {
  id: string
  userId: string
  userName: string
  ocIds: string[]           // ⭐ 改为数组
  ocNames: string[]         // ⭐ 改为数组
  teamName?: string
  teamNumber?: number
  status: 'registered' | 'submitted' | 'eliminated' | 'finished'
  result?: 'win' | 'lose' | 'draw'
  joinedAt: string
  submittedAt?: string
}

export interface BattleSubmission {
  id: string
  participantId: string
  title: string
  description?: string
  contentUrl: string
  contentType?: string
  createdAt: string
  updatedAt?: string
  participant?: BattleParticipant
}

// ⭐ 重构 Battle 接口，完全依赖 participants
export interface Battle {
  id: string
  title: string
  isPublic: boolean   // ⭐ 新增
  opponentOcIds?: Record<string, string[]>
  description?: string
  coverUrl?: string

  battleType: string
  rules: string
  judgmentType: 'vote' | 'internal'
  status: 'open' | 'ongoing' | 'judging' | 'finished' | 'cancelled'
  createdAt: string
  registrationDeadline?: string
  submissionDeadline?: string
  finishedAt?: string
  surveyId?: string
  result?: string
  resultDescription?: string
  participants: BattleParticipant[]   // 所有参与者
  submissions: BattleSubmission[]
  participantCount?: number
  submissionCount?: number
  // 为了向后兼容（前端组件可能还在使用），保留但标记为可选
  initiatorId?: string
  initiatorName?: string
  challengerName?: string
  opponentName?: string
  challengerOcId?: string
  opponentOcId?: string
  challengerOcName?: string
  opponentOcName?: string
}

export interface CreateBattleRequest {
  title: string
  content?: string                    // 对应后端 Content
  coverUrl?: string
  battleType?: string
  rules?: string
  judgmentType?: 'vote' | 'internal'
  // ⭐ 发起方 OC 列表（必填）
  challengerOcIds: string[]
  // ⭐ 对手 OC 字典：用户ID → OC ID 列表
  opponentOcIds?: Record<string, string[]>
  battleConfigJson?: string
}

export interface UpdateBattleRequest {
  title?: string
  content?: string
  coverUrl?: string
  battleType?: string
  rules?: string
  judgmentType?: 'vote' | 'internal'
}

export interface RegisterBattleRequest {
  ocIds: string[]           // ⭐ 改为数组
  remark?: string
}

export interface SubmitWorkRequest {
  title: string
  description?: string
  contentUrl: string
  contentType?: string
}

export interface InternalResultRequest {
  winnerIds: string[]
  resultDescription?: string
}

export interface BattleStats {
  totalBattles: number
  wins: number
  losses: number
  draws: number
  winRate: number
  battleHistory: {
    battleId: string
    battleTitle: string
    result: 'win' | 'lose' | 'draw'
    finishedAt: string
  }[]
}

// ===== API 方法 =====
export const battleApi = {
  list: (params?: { page?: number; pageSize?: number; status?: string; keyword?: string }) =>
    request.get<{ items: Battle[]; total: number }>('/Battle', { params }),

  detail: (id: string) =>
    request.get<Battle>(`/Battle/${id}`),

  create: (data: CreateBattleRequest) =>
    request.post<Battle>('/Battle', data),

     reject: (id: string) =>
    request.post(`/Battle/${id}/reject`),


  update: (id: string, data: UpdateBattleRequest) =>
    request.put<Battle>(`/Battle/${id}`, data),

  delete: (id: string) =>
    request.delete(`/Battle/${id}`),

  register: (id: string, data: RegisterBattleRequest) =>
    request.post(`/Battle/${id}/register`, data),

  unregister: (id: string) =>
    request.post(`/Battle/${id}/unregister`),

  closeRegistration: (id: string) =>
    request.post(`/Battle/${id}/close-registration`),

  closeCreation: (id: string) =>
    request.post(`/Battle/${id}/close-creation`),

  submit: (id: string, data: SubmitWorkRequest) =>
    request.post(`/Battle/${id}/submit`, data),

  updateSubmission: (battleId: string, submissionId: string, data: { contentUrl: string }) =>
    request.put(`/Battle/${battleId}/submission/${submissionId}`, data),

  setInternalResult: (id: string, data: InternalResultRequest) =>
    request.post(`/Battle/${id}/set-internal-result`, data),

  publishResult: (id: string) =>
    request.post(`/Battle/${id}/publish-result`),

  cancel: (id: string) =>
    request.post(`/Battle/${id}/cancel`),

  my: (params?: { status?: string }) =>
    request.get<Battle[]>('/Battle/my', { params }),

  getUserStats: (userId: string) =>
    request.get<BattleStats>(`/Battle/stats/user/${userId}`),

  getOcStats: (ocId: string) =>
    request.get<BattleStats>(`/Battle/stats/oc/${ocId}`),
}