import request from '../utils/request';

export interface CreateNewsDto {
  title: string;
  type: string;
  imageUrl?: string | null;
  content?: string | null;
}

export const newsApi = {
  getAllNews: () => request.get('/news').then((res: any) => res.data?.data || res.data || []),
  createNews: (data: any) => request.post('/news', data),
  updateNews: (id: string, data: any) => request.put(`/news/${id}`, data),
  deleteNews: (id: string) => request.delete(`/news/${id}`),
  // 🌟 状态切换接口
  togglePublish: (id: string, isPublished: boolean) => request.patch(`/news/${id}/publish`, { isPublished })
};