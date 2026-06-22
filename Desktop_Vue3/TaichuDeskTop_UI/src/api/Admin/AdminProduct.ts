// services/Admin/AdminProduct.ts
import request from '@/utils/request';

/**
 * 太初域：画廊实体数据结构
 */
export interface GalleryDto {
  id: string;
  title: string;
  authorId: string;
  authorName: string;
  coverUrl?: string;
  views: number;
  likes: number;
  favorites: number;
  status: 'published' | 'reviewing' | 'rejected' | 'hidden' | string;
  isFeatured: boolean;
  createdAt: string;
}

/**
 * 太初域：深层干涉指令载荷
 */
export interface GalleryGovernanceDto {
  views: number;
  likes: number;
  isFeatured: boolean;
  status: string;
}

/**
 * 分页基础响应结构
 */
export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
}

/**
 * 画廊大盘查询参数
 */
export interface GalleryQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: string;
}

// ----------------------------------------------------------------------------
// 模块 API 导出
// ----------------------------------------------------------------------------

export const adminProductApi = {
  /**
   * 检索画廊大盘数据 (聚合查询)
   * @param params 包含分页、搜索关键词与状态的查询参数
   */
  getGalleryWorks(params: GalleryQuery) {
    // 泛型参数 <PaginatedResponse<GalleryDto>> 帮助你在 Vue 组件中获得完美的类型推导
    return request.get<PaginatedResponse<GalleryDto>>('/admin/product/gallery', { 
      params 
    });
  },

  /**
   * 提交画廊深度干涉裁决
   * @param id 实体追踪码 (例如 "GAL-000012")
   * @param data 干涉指令载荷
   */
  updateGalleryGovernance(id: string, data: GalleryGovernanceDto) {
    return request.put<{ message: string }>(`/admin/product/gallery/${id}/governance`, data);
  },

  deleteGalleryWork(id: string) {
    return request.delete<{ message: string }>(`/admin/product/gallery/${id}`);
  }
};