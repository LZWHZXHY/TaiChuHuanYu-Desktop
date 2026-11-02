import request from '@/utils/request'

// ===== 类型定义 =====

/** 约战结果种类：win 一方胜 / pyrrhic 惨胜（战绩同胜利）/ draw 平手 / mutual 同归于尽（战绩按平局）/ nocontest 无结果·中止（不计战绩） */
export type BattleOutcome = 'win' | 'pyrrhic' | 'draw' | 'mutual' | 'nocontest'

export interface BattleParticipant {
  id: string
  userId: string
  userName: string
  ocIds: string[]           // ⭐ 改为数组
  ocNames: string[]         // ⭐ 改为数组
  teamName?: string
  teamNumber?: number
  status: 'registered' | 'submitted' | 'eliminated' | 'finished'
  /** none = 这场无结果 / 中止，不计战绩；还没出结果时是空 */
  result?: 'win' | 'lose' | 'draw' | 'none' | null
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
  /** 审核状态：没通过时别人看不到链接（contentUrl 为空） */
  reviewStatus?: 'pending' | 'approved' | 'rejected'
  /** 打回原因（只有作者和审核员看得到） */
  reviewNote?: string | null
}

/** 待审核的约战作品（审核员用） */
export interface PendingBattleWork {
  id: string
  battleId: string
  battleTitle: string
  battleStatus?: string
  userId?: string
  userName: string
  title: string
  description?: string | null
  contentUrl: string
  submittedAt: string
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
  /** completed（分出胜负）/ draw（平手、同归于尽）/ nocontest（无结果） */
  result?: string
  /** 结果种类（后端 fix147 新增）；还没出结果是空字符串，老约战后端按 result 推断 */
  outcome?: BattleOutcome | ''
  resultDescription?: string
  /** 战斗配置（里面有 teams：用户编号 → 1 / 2 队），用来把还没应战的受邀者放到对应的一边 */
  battleConfigJson?: string
  participants: BattleParticipant[]   // 所有参与者
  submissions: BattleSubmission[]
  participantCount?: number
  submissionCount?: number
  /** 当前用户能不能审核作品 */
  canReview?: boolean
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

/** 投票制约战的投票情况（存在问卷系统里） */
export interface BattleVotes {
  open: boolean
  total: number
  myOptionId?: number | null
  canVote?: boolean
  reason?: string | null
  options: { id: number; text: string; value?: string | null; count: number }[]
}

export interface SubmitWorkRequest {
  title: string
  description?: string
  contentUrl: string
  contentType?: string
}

export interface InternalResultRequest {
  /** 结果种类；一方胜 / 惨胜时 winnerIds 必须有人（团队战传该队任意一个参与者编号就算整队赢） */
  outcome: BattleOutcome
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
    request.post<Battle>(`/Battle/${id}/reject`),


  update: (id: string, data: UpdateBattleRequest) =>
    request.put<Battle>(`/Battle/${id}`, data),

  delete: (id: string) =>
    request.delete(`/Battle/${id}`),

  register: (id: string, data: RegisterBattleRequest) =>
    request.post<Battle>(`/Battle/${id}/register`, data),

  unregister: (id: string) =>
    request.post<Battle>(`/Battle/${id}/unregister`),

  closeRegistration: (id: string) =>
    request.post<Battle>(`/Battle/${id}/close-registration`),

  closeCreation: (id: string) =>
    request.post<Battle>(`/Battle/${id}/close-creation`),

  submit: (id: string, data: SubmitWorkRequest) =>
    request.post<Battle>(`/Battle/${id}/submit`, data),

  /** 投票制约战：投票情况 / 投一票 / 发起人开启投票（老约战用） */
  votes: (id: string) => request.get<BattleVotes>(`/Battle/${id}/votes`, { silentError: true }),
  vote: (id: string, optionId: number) => request.post<{ message: string }>(`/Battle/${id}/vote`, { optionId }, { silentError: true }),
  startVote: (id: string) => request.post<{ message: string }>(`/Battle/${id}/start-vote`, null, { silentError: true }),

  /** 作品审核（超级管理员 / 用户审核权限） */
  pendingReviews: () => request.get<{ total: number; items: PendingBattleWork[] }>('/Battle/reviews/pending', { silentError: true }),
  reviewSubmission: (submissionId: string, approve: boolean, reason?: string) =>
    request.post<{ message: string }>(`/Battle/submissions/${submissionId}/review`, { approve, reason }, { silentError: true }),

  /** 修改自己已提交的作品（创作期内）。以前这里指向一个不存在的接口 */
  updateSubmission: (id: string, data: SubmitWorkRequest) =>
    request.put<Battle>(`/Battle/${id}/submit`, data),

  setInternalResult: (id: string, data: InternalResultRequest) =>
    request.post<Battle>(`/Battle/${id}/set-internal-result`, data),

  /** 管理员修正已结束约战的结果（会自动调整 OC 战绩、通知参与者、留修正记录）；只有 canReview 为 true 的人能用 */
  correctResult: (id: string, data: InternalResultRequest) =>
    request.post<Battle>(`/Battle/${id}/correct-result`, data),

  publishResult: (id: string) =>
    request.post<Battle>(`/Battle/${id}/publish-result`),

  cancel: (id: string) =>
    request.post<Battle>(`/Battle/${id}/cancel`),

  my: (params?: { status?: string }) =>
    request.get<Battle[]>('/Battle/my', { params }),

  getUserStats: (userId: string) =>
    request.get<BattleStats>(`/Battle/stats/user/${userId}`),

  getOcStats: (ocId: string) =>
    request.get<BattleStats>(`/Battle/stats/oc/${ocId}`),
}