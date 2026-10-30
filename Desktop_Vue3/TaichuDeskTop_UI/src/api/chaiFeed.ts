import request from '@/utils/request';

export interface ChaiFeedItem {
  id: string;
  type: 'work' | 'oc' | 'battle' | 'joint';
  title: string;
  authorName: string;
  authorId?: string | null;
  coverUrl: string | null;
  createdAt: string;
}

export const chaiFeedApi = {
  get: (params: { offset?: number; limit?: number } = {}) =>
    request.get<{ items: ChaiFeedItem[]; hasMore: boolean }>('/ChaiFeed', { params, silentError: true })
};
