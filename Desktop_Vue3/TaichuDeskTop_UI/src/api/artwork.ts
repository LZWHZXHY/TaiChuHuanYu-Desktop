// artwork.ts
import request from '../utils/request';

export interface ArtworkItemDto {
  id: number
  title: string
  coverImageUrl?: string
  authorName: string
  authorAvatar?: string
  uploadAt: string
  imageCount: number
  likesCount: number
  commentsCount: number
  viewCount: number
}

/**
 * 分页返回的包装结构
 */
export interface ArtworkListResponse {
  total: number;
  data: ArtworkItemDto[];
  hasMore: boolean;
}

// ========== 🆕 扩展详情类型，包含水印配置 ==========
export interface ArtworkDetail {
  id: number
  title: string
  description?: string
  uploadAt: string
  author: {
    username: string
    avatar?: string
    bio?: string
  }
  images: Array<{
    url: string
    caption?: string
  }>

  // ---------- 水印配置 ----------
  watermarkType: 'text' | 'image' | 'both'
  
  // 文字水印
  watermarkEnabled: boolean
  watermarkText: string
  watermarkPosition: 'top-left' | 'top-center' | 'top-right' | 'center-left' | 'center' | 'center-right' | 'bottom-left' | 'bottom-center' | 'bottom-right'
  watermarkFontSize: number
  watermarkOpacity: number
  watermarkColor: string
  watermarkRotation: number

  // 图片水印
  watermarkImageUrl?: string
  watermarkImageWidth: number
  watermarkImageHeight: number
  watermarkImageScale: number
  watermarkImageOpacity: number
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