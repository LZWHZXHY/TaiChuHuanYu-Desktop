import request from '@/utils/request';

export interface ChaiComment {
  id: string;
  userId: string;
  userName: string;
  content: string;
  parentId: string | null;
  createdAt: string;
  likeCount?: number;
  liked?: boolean;
}

export const chaiCommentApi = {
  list: (targetType: string, targetId: string) =>
    request.get<ChaiComment[]>(`/ChaiComment/${targetType}/${targetId}`, { silentError: true }),
  create: (payload: { targetType: string; targetId: string; content: string; parentId?: string }) =>
    request.post<ChaiComment>('/ChaiComment', payload, { silentError: true }),
  like: (id: string) =>
    request.post<{ liked: boolean; count: number }>(`/ChaiComment/${id}/like`, undefined, { silentError: true }),
  remove: (id: string) => request.delete(`/ChaiComment/${id}`)
};
