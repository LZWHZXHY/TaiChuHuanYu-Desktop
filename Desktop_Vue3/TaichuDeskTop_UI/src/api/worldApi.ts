import request from '@/utils/request'
import type {
  WorldProject, CreateProjectPayload, UpdateProjectPayload, CardSummary, CardDetail,
  CreateCardPayload, UpdateCardPayload, Relation, CardTypeDto, WorldQuota,
  ProjectQuotaCheck, CardQuotaCheck, QuotaHistory,
  TimelineItem, WorldSearchResult, StartWorldRestore, WorldRestoreStatus,
  WorldMembers, WorldMemberCandidate, CollaboratorRole,
  WorldRevision, WorldRevisionDetail, WorldLastEditor, WorldCardBlame,
} from './types/world'

export type * from './types/world'

// 请求层已经解包 HTTP 响应；业务 API 直接返回 DTO，不再增加 { data }。
export const worldApi = {
  listRestores: (projectId: string) => request.get<WorldRestoreStatus[]>('/world/restores', { params: { projectId }, silentError: true }),
  startRestore: (payload: StartWorldRestore) => request.post<WorldRestoreStatus>('/world/restores', payload, { silentError: true }),
  getRestore: (id: string) => request.get<WorldRestoreStatus>(`/world/restores/${id}`, { silentError: true }),
  uploadRestoreImage: (id: string, name: string, file: File, signal?: AbortSignal) => {
    const data = new FormData(); data.append('file', file)
    return request.put<WorldRestoreStatus>(`/world/restores/${id}/images/${encodeURIComponent(name)}`, data, { timeout: 300000, silentError: true, signal })
  },
  commitRestore: (id: string, payload: { cards: CreateCardPayload[]; description?: string | null }) =>
    request.post<WorldRestoreStatus>(`/world/restores/${id}/commit`, payload, { timeout: 300000, silentError: true }),
  cancelRestore: (id: string) => request.delete<WorldRestoreStatus>(`/world/restores/${id}`, { timeout: 300000, silentError: true }),
  getCardTypes: () => request.get<CardTypeDto[]>('/world/card-types'),
  getProjects: () => request.get<WorldProject[]>('/world/projects'),
  getPublicProjects: () => request.get<WorldProject[]>('/world/projects/public'),
  getProject: (id: string) => request.get<WorldProject>(`/world/projects/${id}`, { silentError: true }),
  createProject: (payload: CreateProjectPayload) => request.post<WorldProject>('/world/projects', payload),
  updateProject: (id: string, payload: UpdateProjectPayload) =>
    request.put<WorldProject>(`/world/projects/${id}`, payload),
  deleteProject: async (id: string): Promise<void> => { await request.delete(`/world/projects/${id}`) },

  // 修改记录：查看、撤销某次修改、一键撤销某人的修改
  getHistory: (id: string, params: { cardId?: string; userId?: string; action?: string; before?: number; take?: number } = {}) =>
    request.get<{ items: WorldRevision[]; hasMore: boolean }>(`/world/projects/${id}/history`, { params, silentError: true }),
  getRevision: (id: string, revisionId: number) =>
    request.get<WorldRevisionDetail>(`/world/projects/${id}/history/${revisionId}`, { silentError: true }),
  undoRevision: (id: string, revisionId: number) =>
    request.post<{ message: string }>(`/world/projects/${id}/history/${revisionId}/undo`, {}, { silentError: true }),
  revertUser: (id: string, userId: string, hours: number) =>
    request.post<{ count: number; message: string }>(`/world/projects/${id}/history/revert-user`, { userId, hours }, { silentError: true }),
  getCardBlame: (id: string, cardId: string) =>
    request.get<WorldCardBlame>(`/world/projects/${id}/history/card/${cardId}/blame`, { silentError: true }),
  getLastEditors: (id: string) =>
    request.get<WorldLastEditor[]>(`/world/projects/${id}/history/last-editors`, { silentError: true }),

  // 协作者：创建者管理，协作者可自己退出
  getMembers: (id: string) => request.get<WorldMembers>(`/world/projects/${id}/collaborators`, { silentError: true }),
  searchMemberCandidates: (id: string, keyword: string) =>
    request.get<WorldMemberCandidate[]>(`/world/projects/${id}/collaborators/candidates`, { params: { keyword }, silentError: true }),
  addMember: (id: string, userId: string, role: CollaboratorRole) =>
    request.post<{ userId: string; username: string; role: CollaboratorRole }>(`/world/projects/${id}/collaborators`, { userId, role }, { silentError: true }),
  updateMemberRole: (id: string, userId: string, role: CollaboratorRole) =>
    request.put<{ userId: string; role: CollaboratorRole }>(`/world/projects/${id}/collaborators/${userId}`, { role }, { silentError: true }),
  removeMember: async (id: string, userId: string): Promise<void> => {
    await request.delete(`/world/projects/${id}/collaborators/${userId}`, { silentError: true })
  },

  saveGraphPositions: (id: string, positions: string, expectedPositions: string) =>
    request.put<void>(`/world/projects/${id}/graph-positions`, { positions, expectedPositions }, { silentError: true }),

  getCards: (projectId: string, type?: string) =>
    request.get<CardSummary[]>(`/world/projects/${projectId}/cards`, { params: { type }, silentError: true }),
  getCard: (projectId: string, cardId: string) =>
    request.get<CardDetail>(`/world/projects/${projectId}/cards/${cardId}`, { silentError: true }),
  getCardBacklinks: (projectId: string, cardId: string) =>
    request.get<CardSummary[]>(`/world/projects/${projectId}/cards/${cardId}/backlinks`),

  getReferences: (projectId: string) =>
    request.get<Record<string, string[]>>(`/world/projects/${projectId}/cards/references`, { silentError: true }),
  getCardsBatch: (projectId: string, ids: string[]) =>
    request.post<CardDetail[]>(`/world/projects/${projectId}/cards/batch`, ids, { silentError: true }),

  getTimeline: (projectId: string) =>
    request.get<TimelineItem[]>(`/world/projects/${projectId}/cards/timeline`, { silentError: true }),

  searchCards: (q: string, limit = 20) =>
    request.get<WorldSearchResult[]>(`/world/search`, { params: { q, limit }, silentError: true }),
  createCard: (projectId: string, payload: CreateCardPayload) =>
    request.post<CardDetail>(`/world/projects/${projectId}/cards`, payload),
  updateCard: (projectId: string, cardId: string, payload: UpdateCardPayload) =>
    request.put<CardDetail>(`/world/projects/${projectId}/cards/${cardId}`, payload, { silentError: true }),
  deleteCard: async (projectId: string, cardId: string): Promise<void> => {
    await request.delete(`/world/projects/${projectId}/cards/${cardId}`)
  },

  addRelation: (cardId: string, targetCardId: string, relationType: string) =>
    request.post<Relation>(`/world/cards/${cardId}/relations`, { targetCardId, relationType }),
  removeRelation: async (cardId: string, relationId: string): Promise<void> => {
    await request.delete(`/world/cards/${cardId}/relations/${relationId}`)
  },
  getCardRelations: (cardId: string) => request.get<Relation[]>(`/world/cards/${cardId}/relations`),
  getProjectRelations: (projectId: string) => request.get<Relation[]>(`/world/projects/${projectId}/relations`, { silentError: true }),

  getQuota: () => request.get<WorldQuota>('/world/quota/my'),
  canCreateProject: () => request.get<ProjectQuotaCheck>('/world/quota/can-create-project'),
  canAddCard: (projectId: string) => request.get<CardQuotaCheck>(`/world/quota/can-add-card/${projectId}`),
  getQuotaHistory: (limit = 20) => request.get<QuotaHistory[]>('/world/quota/history', { params: { limit } }),
}
