import request from '@/utils/request'
import type {
  WorldProject, CreateProjectPayload, UpdateProjectPayload, CardSummary, CardDetail,
  CreateCardPayload, UpdateCardPayload, Relation, CardTypeDto, WorldQuota,
  ProjectQuotaCheck, CardQuotaCheck, QuotaUpgradeType, QuotaUpgradeResult, QuotaHistory,
} from './types/world'

export type * from './types/world'

// 请求层已经解包 HTTP 响应；业务 API 直接返回 DTO，不再增加 { data }。
export const worldApi = {
  getCardTypes: () => request.get<CardTypeDto[]>('/world/card-types'),
  getProjects: () => request.get<WorldProject[]>('/world/projects'),
  getPublicProjects: () => request.get<WorldProject[]>('/world/projects/public'),
  getProject: (id: string) => request.get<WorldProject>(`/world/projects/${id}`),
  createProject: (payload: CreateProjectPayload) => request.post<WorldProject>('/world/projects', payload),
  updateProject: (id: string, payload: UpdateProjectPayload) =>
    request.put<WorldProject>(`/world/projects/${id}`, payload),
  deleteProject: async (id: string): Promise<void> => { await request.delete(`/world/projects/${id}`) },

  getCards: (projectId: string, type?: string) =>
    request.get<CardSummary[]>(`/world/projects/${projectId}/cards`, { params: { type } }),
  getCard: (projectId: string, cardId: string) =>
    request.get<CardDetail>(`/world/projects/${projectId}/cards/${cardId}`),
  createCard: (projectId: string, payload: CreateCardPayload) =>
    request.post<CardDetail>(`/world/projects/${projectId}/cards`, payload),
  updateCard: (projectId: string, cardId: string, payload: UpdateCardPayload) =>
    request.put<CardDetail>(`/world/projects/${projectId}/cards/${cardId}`, payload),
  deleteCard: async (projectId: string, cardId: string): Promise<void> => {
    await request.delete(`/world/projects/${projectId}/cards/${cardId}`)
  },

  addRelation: (cardId: string, targetCardId: string, relationType: string) =>
    request.post<Relation>(`/world/cards/${cardId}/relations`, { targetCardId, relationType }),
  removeRelation: async (cardId: string, relationId: string): Promise<void> => {
    await request.delete(`/world/cards/${cardId}/relations/${relationId}`)
  },
  getCardRelations: (cardId: string) => request.get<Relation[]>(`/world/cards/${cardId}/relations`),
  getProjectRelations: (projectId: string) => request.get<Relation[]>(`/world/projects/${projectId}/relations`),

  getQuota: () => request.get<WorldQuota>('/world/quota/my'),
  canCreateProject: () => request.get<ProjectQuotaCheck>('/world/quota/can-create-project'),
  canAddCard: (projectId: string) => request.get<CardQuotaCheck>(`/world/quota/can-add-card/${projectId}`),
  upgradeQuota: (upgradeType: QuotaUpgradeType) =>
    request.post<QuotaUpgradeResult>('/world/quota/upgrade', { upgradeType }),
  getQuotaHistory: (limit = 20) => request.get<QuotaHistory[]>('/world/quota/history', { params: { limit } }),
}
