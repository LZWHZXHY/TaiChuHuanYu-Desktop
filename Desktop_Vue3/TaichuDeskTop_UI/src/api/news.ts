import request from '../utils/request';

export interface CreateNewsDto {
  title: string;
  type: string;
  imageUrl?: string | null;
  content?: string | null;
  /** persistent（常驻，直到手动下线）/ scheduled（定时，到期自动隐藏） */
  noticeKind?: string;
  /** normal（普通）/ important（重要）/ urgent（紧急） */
  importance?: string;
  /** 定时公告截止时间（ISO 字符串） */
  expireAt?: string | null;
}

export const newsApi = {
  getAllNews: () => request.get('/news').then((res: any) => res.data?.data || res.data || []),
  createNews: (data: any) => request.post('/news', data),
  updateNews: (id: string, data: any) => request.put(`/news/${id}`, data),
  deleteNews: (id: string) => request.delete(`/news/${id}`),
  // 🌟 状态切换接口
  togglePublish: (id: string, isPublished: boolean) => request.patch(`/news/${id}/publish`, { isPublished })
};