import request from '@/utils/request';

export interface ChaiWorkBrief {
  id: string;
  title: string;
  coverUrl: string | null;
  mediaType: string;
  authorName: string;
  authorId: string;
  status: string;
  views: number;
  likeCount: number;
  favoriteCount: number;
  commentCount: number;
  tags?: string[];
  createdAt: string;
}

export interface ChaiWork extends ChaiWorkBrief {
  description: string | null;
  mediaUrl: string;
  tags: string[];
  relatedCharacterIds: string[];
  relatedBattleId: string | null;
  relatedJointId: string | null;
  updatedAt: string | null;
}

export interface ChaiWorkListResponse {
  items: ChaiWorkBrief[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ChaiWorkPayload {
  title: string;
  description?: string;
  mediaUrl: string;
  coverUrl?: string;
  mediaType?: string;
  status?: string;
  tags?: string[];
  relatedCharacterIds?: string[];
  relatedBattleId?: string | null;
  relatedJointId?: string | null;
}

export const chaiWorkApi = {
  getList: (params: { page?: number; pageSize?: number; keyword?: string; tag?: string; sort?: string } = {}) =>
    request.get<ChaiWorkListResponse>('/ChaiWork', { params, silentError: true }),
  getDetail: (id: string) => request.get<ChaiWork>(`/ChaiWork/${id}`, { silentError: true }),
  create: (payload: ChaiWorkPayload) => request.post<ChaiWork>('/ChaiWork', payload, { silentError: true }),
  update: (id: string, payload: Partial<ChaiWorkPayload>) => request.put<ChaiWork>(`/ChaiWork/${id}`, payload, { silentError: true }),
  remove: (id: string) => request.delete(`/ChaiWork/${id}`),
  toggle: (id: string, action: 'like' | 'favorite') =>
    request.post<{ active: boolean; count: number }>(`/ChaiWork/${id}/${action}`, undefined, { silentError: true })
};
