import request from '@/utils/request'

/** 点赞 / 砸番茄 */
export type ReactKind = 'like' | 'tomato'

/** 留言板里的一条回复 */
export interface BoardReply {
  id: number
  userId?: string | null
  name: string
  avatar?: string | null
  content: string
  replyToUserId?: string | null
  replyToName?: string | null
  createdAt: string
  canDelete: boolean
}
/** 留言板里的一条主留言（deleted 为 true 时只剩个壳，下面还有回复） */
export interface BoardMessage {
  id: number
  userId?: string | null
  name: string
  avatar?: string | null
  content: string
  deleted: boolean
  createdAt: string
  canDelete: boolean
  replies: BoardReply[]
}
export interface BoardPage { canManage: boolean; hasMore: boolean; items: BoardMessage[] }
export interface BoardPosted extends BoardReply { parentId?: number | null }

/** 给「这个游戏」点赞 / 砸番茄的统计 */
export interface GameReactions {
  likes: number
  tomatoes: number
  myLikes: number
  myTomatoes: number
  likeCost: number
  tomatoCost: number
  recent: { userId: string; name: string; avatar?: string | null; kind: ReactKind; at: string }[]
}

/** 游戏留言板（按游戏 key 区分，现在只有斗地主 ddz） */
export const gameBoardApi = {
  messages: (game: string, beforeId?: number) =>
    request.get<BoardPage>(`/game-board/${game}/messages`, { params: { beforeId }, silentError: true }),
  post: (game: string, content: string, parentId?: number) =>
    request.post<BoardPosted>(`/game-board/${game}/messages`, { content, parentId }, { silentError: true }),
  remove: (game: string, id: number) => request.delete(`/game-board/${game}/messages/${id}`, { silentError: true }),
  reactions: (game: string) => request.get<GameReactions>(`/game-board/${game}/reactions`, { silentError: true }),
  react: (game: string, kind: ReactKind) =>
    request.post<{ balance: number; reactions: GameReactions }>(`/game-board/${game}/react`, { kind }, { silentError: true }),
}

/** 从后端报错里拿出给人看的那句话（后端统一返回 { message }） */
export function errorText(error: unknown, fallback: string): string {
  const e = error as { response?: { data?: { message?: unknown } }; friendlyMessage?: unknown } | null
  const msg = e?.response?.data?.message
  if (typeof msg === 'string' && msg) return msg
  return fallback
}
