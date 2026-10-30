// artwork.ts
import request from '../utils/request';

export interface ArtworkStats {
  likesCount: number
  favoritesCount: number
  commentsCount: number
  isLiked: boolean
  isFavorited: boolean
}

export interface ArtworkItemDto extends ArtworkStats {
  rankingLikes?: number
  id: number
  title: string
  coverImageUrl?: string
  authorName: string
  authorId?: string
  authorAvatar?: string
  uploadAt: string
  imageCount: number
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
export interface ArtworkDetail extends ArtworkStats {
  canInteract?: boolean
  canEdit?: boolean
  originalNoteId?: string
  /** 只有作者拿得到：public 公开中 / withdrawn 自己下架了 / reviewing 审核中 / hidden 被管理员隐藏 / rejected 没通过 */
  ownerState?: string | null
  id: number
  title: string
  tags?: string[]
  description?: string
  uploadAt: string
  author: {
    id?: string
    username: string
    avatar?: string
    bio?: string
  }
  images: Array<{
    isCover?: boolean
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

export interface ArtworkComment {
  id: string
  userId: string
  authorName: string
  avatar?: string
  content: string
  parentId?: string
  replyToAuthor?: string
  createdAt: string
  isDeleted: boolean
  canDelete: boolean
}

export interface CommentPage {
  items: ArtworkComment[]
  total: number
  hasMore: boolean
}

export interface ArtworkWriteResult { success: boolean; artworkId: number; imagesCount: number; message?: string }
export interface ArtworkEdit { tags?: string[]; title: string; description: string; captions: string[]; coverIndex: number }

export const artworkApi = {
  publish: (data: FormData, onProgress?: (percent: number) => void) => request.post<ArtworkWriteResult>('/NewArtwork/direct-upload', data, {
    silentError: true,
    onUploadProgress: event => { if (event.total) onProgress?.(Math.round(event.loaded * 100 / event.total)) }
  }),
  /** 作者自己下架 / 重新上架 */
  setVisibility: (id: number, visible: boolean) =>
    request.post<{ ownerState: string; message: string }>(`/Artwork/${id}/visibility`, { visible }, { silentError: true }),
  edit: (id: number, data: ArtworkEdit) => request.put<ArtworkWriteResult>(`/Artwork/${id}`, data, { silentError: true }),
  /**
   * 获取瀑布流列表
   * @param offset 跳过的数量
   * @param limit 每次加载的数量 (默认20)
   */
  getGallery: (offset: number, limit: number = 20, search?: string, rankPeriod?: string) =>
    // 只保留一个类型参数：ArtworkListResponse
    request.get<ArtworkListResponse>('/Artwork', {
      params: { offset, limit, rankPeriod, search: search?.trim() || undefined }, silentError: true
    }),

  /** 我收藏的作品（按收藏时间倒序） */
  getFavorites: (offset: number, limit: number = 20) =>
    request.get<ArtworkListResponse>('/Artwork/favorites', { params: { offset, limit }, silentError: true }),

  /**
   * 获取作品详情
   */
  getDetail: (id: number) =>
    // 只保留一个类型参数：ArtworkDetail
    request.get<ArtworkDetail>(`/Artwork/${id}`, { silentError: true }),
  getComments: (id: number, offset = 0) =>
    request.get<CommentPage>(`/Artwork/${id}/comments`, { params: { offset, limit: 20 }, silentError: true }),
  addComment: (id: number, content: string, parentId?: string) =>
    request.post<{ id: string; stats: ArtworkStats & { id: number } }>(`/Artwork/${id}/comments`, { content, parentId }, { silentError: true }),
  deleteComment: (id: number, commentId: string) =>
    request.delete<{ stats: ArtworkStats & { id: number } }>(`/Artwork/${id}/comments/${commentId}`, { silentError: true })
};
