import request from '@/utils/request'

/** 世界观 / 设定条目 / 小说的点赞、收藏、评论数（条目只有评论数） */
export type SocialKind = 'world' | 'card' | 'novel'

export interface SocialStats {
  likeCount: number
  favoriteCount: number
  commentCount: number
  liked: boolean
  favorited: boolean
}

export interface FavoriteWorld {
  id: string
  name: string
  description?: string | null
  coverImage?: string | null
  isPublic: boolean
  ownerId: string
  ownerName?: string | null
  favoritedAt: string
}

export interface FavoriteNovel {
  id: string
  title: string
  description?: string | null
  coverUrl?: string | null
  authorId: string
  authorName: string
  wordCount: number
  status: string
  favoritedAt: string
}

export const worldSocialApi = {
  stats: (kind: SocialKind, id: string) =>
    request.get<SocialStats>(`/world/social/${kind}/${id}`, { silentError: true }),
  toggle: (kind: 'world' | 'novel', id: string, action: 'like' | 'favorite') =>
    request.post<{ active: boolean; count: number }>(`/world/social/${kind}/${id}/${action}`, undefined, { silentError: true }),
  favorites: () =>
    request.get<{ worlds: FavoriteWorld[]; novels: FavoriteNovel[] }>('/world/social/favorites', { silentError: true }),
}
