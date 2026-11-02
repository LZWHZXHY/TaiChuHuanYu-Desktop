import request from '@/utils/request'

export interface RecordPlayer { name: string; isBot: boolean; role: string; result: string; userId?: string | null; self: boolean }
export interface MatchItem {
  id: string
  /** doudizhu / gobang / go / xiangqi */
  game: string
  /** online 服务器判定的对局；local 本地双人 */
  source: 'online' | 'local'
  mode: string
  summary: string
  time: string
  /** win / lose / draw；本地双人为 local */
  result: 'win' | 'lose' | 'draw' | 'local'
  role?: string | null
  potatoChange: number
  hasBots: boolean
  moves?: number | null
  players: RecordPlayer[]
}
export interface GameStats {
  totalGames: number
  totalWins: number
  winRate: number
  streak: { count: number; kind: 'win' | 'lose' | 'draw' | 'none' }
  games: { game: string; total: number; wins: number; losses: number; draws: number; winRate: number; potatoNet: number }[]
  local: { game: string; count: number }[]
}
export interface LossesRankRow { rank: number; userId: string; name: string; avatar?: string | null; losses: number; total: number; lossRate: number }
export interface WinsRankRow { rank: number; userId: string; name: string; avatar?: string | null; wins: number; total: number; winRate: number }

/** 牌桌上被点赞 / 被砸番茄（全部 + 最近 30 天），以及送出去的 */
export interface ReactionStats { likes: number; tomatoes: number; likes30: number; tomatoes30: number; givenLikes: number; givenTomatoes: number }
export interface ReactionRankRow { rank: number; userId: string; name: string; avatar?: string | null; count: number }

export const GAME_NAMES: Record<string, string> = { doudizhu: '斗地主', gobang: '五子棋', go: '围棋', xiangqi: '象棋', question: '测试问卷' }

export const recordsApi = {
  mine: (game?: string, limit = 50) => request.get<MatchItem[]>('/Records/me', { params: { game, limit }, silentError: true }),
  myStats: () => request.get<GameStats>('/Records/stats/me', { silentError: true }),
  userStats: (userId: string) => request.get<GameStats>(`/Records/stats/${userId}`, { silentError: true }),
  winsLeaderboard: (range: 'total' | 'week' | 'month' = 'total') => request.get<WinsRankRow[]>('/Records/leaderboard/wins', { params: { range }, silentError: true }),
  lossesLeaderboard: (range: 'total' | 'week' | 'month' = 'total') => request.get<LossesRankRow[]>('/Records/leaderboard/losses', { params: { range }, silentError: true }),
  /** 我被点赞 / 被砸番茄多少次 */
  myReactions: () => request.get<ReactionStats>('/Records/reactions/me', { silentError: true }),
  /** 「最受欢迎」（like）/「番茄收割机」（tomato）；range：total 全部、30d 最近 30 天 */
  reactionsLeaderboard: (kind: 'like' | 'tomato', range: 'total' | '30d' = 'total', limit = 10) =>
    request.get<ReactionRankRow[]>('/Records/leaderboard/reactions', { params: { kind, range, limit }, silentError: true }),
}
