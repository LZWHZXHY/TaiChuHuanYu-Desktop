// artwork.ts
import request from '../utils/request';

/**
 * 单个作品在列表中的简要信息 (DTO)
 */
export interface ArtworkItemDto {
  id: number;
  title: string;
  description?: string;
  coverImageUrl?: string;
  authorName: string;
  authorAvatar?: string;
  uploadAt: string;
  imageCount: number;
  // 统计数值
  likesCount: number;
  commentsCount: number;
  viewCount: number;
  
}

/**
 * 分页返回的包装结构
 */
export interface ArtworkListResponse {
  total: number;
  data: ArtworkItemDto[];
  hasMore: boolean;
}

/**
 * 作品详情结构
 */
export interface ArtworkDetail {
  id: number;
  title: string;
  description?: string;
  uploadAt: string;
  author: {
    username: string;
    avatar?: string;
    bio?: string;
  };
  images: string[]; // 所有图片的 URL 数组
  blocks?: any[];
}

export const artworkApi = {
  /**
   * 获取瀑布流列表
   * @param offset 跳过的数量
   * @param limit 每次加载的数量 (默认20)
   */
  getGallery: (offset: number, limit: number = 20) =>
    // 只保留一个类型参数：ArtworkListResponse
    request.get<ArtworkListResponse>('/Artwork', {
      params: { offset, limit }
    }),

  /**
   * 获取作品详情
   */
  getDetail: (id: number) =>
    // 只保留一个类型参数：ArtworkDetail
    request.get<ArtworkDetail>(`/Artwork/${id}`)
};