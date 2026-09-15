// src/views/太初俱乐部/api/operators.ts
import request from '@/utils/request'

const BASE = '/club/operators'
const GAME_BASE = '/club/games'

// ==================================================
// 类型定义
// ==================================================

export interface GameField {
  key: string
  label: string
  type: 'select' | 'text' | 'textarea' | 'number'
  required: boolean
  placeholder?: string
  options: string[]
}

export interface GameDef {
  code: string
  name: string
  description?: string
  fields: GameField[]
}

export interface MyGameSkill {
  gameCode: string
  gameName: string
  metrics: Record<string, string>
  auditStatus: 'pending' | 'reviewing' | 'approved' | 'rejected' | 'banned'
  auditNote?: string
  code?: string
  level?: string
  recheckStatus: string
  appliedAt: string
  reviewedAt?: string
}

export interface MyOperatorStatus {
  hasApplied: boolean
  nickname?: string
  contactType?: string
  contactValue?: string
  onlineTime?: string
  intro?: string
  games?: MyGameSkill[]
}

export interface OperatorApplyPayload {
  nickname: string
  contactType: string
  contactValue: string
  onlineTime: string
  intro?: string
  gameCode: string
  metrics: Record<string, string>
}

// ==================================================
// 接口
// ==================================================

/** 获取游戏列表 + 字段配置 */
export function fetchGames() {
  return request.get<GameDef[]>(GAME_BASE)
}

/** 查询我的打手状态 */
export function fetchMyOperatorStatus() {
  return request.get<MyOperatorStatus>(`${BASE}/me`)
}

/** 首次申请（含第一个游戏） */
export function submitOperatorApply(payload: OperatorApplyPayload) {
  return request.post<{ message: string }>(`${BASE}/apply`, payload)
}

/** 追加申请新游戏 */
export function submitGameApply(payload: { gameCode: string; metrics: Record<string, string> }) {
  return request.post<{ message: string }>(`${BASE}/games/apply`, payload)
}
// ==================================================
// 公开接口（无需登录）
// ==================================================

/** 获取所有上架游戏 */
export function fetchPublicGames() {
  return request.get<GameDef[]>(GAME_BASE)
}

/** 获取指定游戏的订单类型（含参数结构 + 价格映射） */
export function fetchPublicOrderTypes(gameCode: string) {
  return request.get(`/club/order-types`, {
    params: { gameCode }
  })
}