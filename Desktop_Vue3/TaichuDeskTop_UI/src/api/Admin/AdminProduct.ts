import request from '@/utils/request';

export interface GalleryDto {
  id: string;
  title: string;
  authorId: string;
  authorName: string;
  coverUrl?: string;
  views: number;
  likes: number; // 按实际点赞用户去重统计，只读。
  favorites: number;
  status: string;
  isFeatured: boolean;
  createdAt: string;
}

export interface GalleryGovernanceDto {
  views: number;
  isFeatured: boolean;
  status: string;
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
}

export interface GalleryQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: string;
}

export const adminProductApi = {
  getGalleryWorks(params: GalleryQuery) {
    return request.get<PaginatedResponse<GalleryDto>>('/admin/product/gallery', { params });
  },

  updateGalleryGovernance(id: string, data: GalleryGovernanceDto) {
    return request.put<{ message: string }>(`/admin/product/gallery/${id}/governance`, data);
  },

  deleteGalleryWork(id: string) {
    return request.delete<{ message: string }>(`/admin/product/gallery/${id}`);
  }
};
