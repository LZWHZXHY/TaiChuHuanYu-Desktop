import request from '@/utils/request'
import type {
  CreateGameDto,
  UpdateGameDto,
  Game,
  PaginatedResponse,
  SessionSummary,
  SessionDetail,
  SaveSessionDto,
  SubmitSessionDto,
  SubmitSessionResult
} from './game_types'

// ============================================================
//  游戏管理 API
// ============================================================

/**
 * 创建游戏（消耗经验）
 */
export const createGame = (data: CreateGameDto): Promise<Game> => {
  return request.post<Game>('/games', data)
}

/**
 * 获取游戏列表（支持分页和筛选）
 */
export const getGames = (params?: {
  type?: string
  status?: string
  page?: number
  pageSize?: number
  /** hot = 按被玩次数；不传 = 最新 */
  sort?: 'hot'
  q?: string
}): Promise<PaginatedResponse<Game>> => {
  return request.get<PaginatedResponse<Game>>('/games', { params })
}

/**
 * 获取当前用户创建的游戏
 */
export const getMyGames = (params?: {
  status?: string
  page?: number
  pageSize?: number
}): Promise<PaginatedResponse<Game>> => {
  return request.get<PaginatedResponse<Game>>('/games/my', { params })
}

/**
 * 获取单个游戏详情
 */
export const getGame = (id: number): Promise<Game> => {
  return request.get<Game>(`/games/${id}`)
}

/**
 * 更新游戏
 */
export const updateGame = (id: number, data: UpdateGameDto): Promise<Game> => {
  return request.put<Game>(`/games/${id}`, data)
}

/**
 * 删除游戏
 */
export const deleteGame = (id: number): Promise<void> => {
  return request.delete<void>(`/games/${id}`)
}

// ============================================================
//  试玩记录 API
// ============================================================

/** 创建要花多少经验、我现在有多少经验 */
export const getGameConfig = () =>
  request.get<{ createCost: number; experience: number | null; maxGames: number }>('/games/config', { silentError: true })

/** 交卷：服务器算分、找结果、记成绩（被玩次数 +1） */
export const submitSession = (data: SubmitSessionDto): Promise<SubmitSessionResult> =>
  request.post<SubmitSessionResult>('/gamesessions', data, { silentError: true })

/**
 * 保存试玩成绩（旧接口格式，保留兼容）
 */
export const saveSession = (data: SaveSessionDto): Promise<{ id: number }> => {
  return request.post<{ id: number }>('/gamesessions', data)
}

/**
 * 获取当前用户的试玩记录列表
 */
export const getMySessions = (params?: {
  gameId?: number
  page?: number
  pageSize?: number
}): Promise<PaginatedResponse<SessionSummary>> => {
  return request.get<PaginatedResponse<SessionSummary>>('/gamesessions/my', { params })
}

/**
 * 获取单条试玩记录详情（含答案）
 */
export const getSessionDetail = (id: number): Promise<SessionDetail> => {
  return request.get<SessionDetail>(`/gamesessions/${id}`)
}

