import request from '@/utils/request'

/** 土豆：游戏区专用的娱乐积分（不能充值、转让或兑换） */
export interface PotatoWallet {
  balance: number
  dailyClaimed: boolean
  signBonusClaimed: boolean
  signedToday: boolean
  reliefLeft: number
  canRelief: boolean
  rules: { welcome: number; daily: number; signBonus: number; relief: number; reliefThreshold: number; reliefPerDay: number; botWinDailyCap: number; expRate?: number; expDailyCap?: number }
}
export interface PotatoLedgerRow { id: number; delta: number; balance: number; reason: string; refType: string; refId?: string | null; createdAt: string }
export interface ExpExchangeInfo { rate: number; dailyCap: number; usedToday: number; leftToday: number; experience: number; level: number }
export interface PotatoRankRow { rank: number; userId: string; name: string; avatar?: string | null; value: number }

export const potatoApi = {
  me: () => request.get<PotatoWallet>('/Potato/me', { silentError: true }),
  daily: () => request.post<{ gained: number; message: string; wallet: PotatoWallet }>('/Potato/daily', undefined, { silentError: true }),
  relief: () => request.post<{ gained: number; message: string; wallet: PotatoWallet }>('/Potato/relief', undefined, { silentError: true }),
  exchangeInfo: () => request.get<ExpExchangeInfo>('/Potato/exchange', { silentError: true }),
  exchange: (exp: number) => request.post<{ message: string; gained: number; wallet: PotatoWallet }>('/Potato/exchange', { exp }, { silentError: true }),
  ledger: (limit = 50) => request.get<PotatoLedgerRow[]>('/Potato/ledger', { params: { limit }, silentError: true }),
  leaderboard: (range: 'total' | 'week' | 'month' = 'total') => request.get<PotatoRankRow[]>('/Potato/leaderboard', { params: { range }, silentError: true }),
}

/** 让页面上的土豆余额在对局结算、领取后一起刷新 */
export const POTATO_CHANGED = 'taichu-potato-changed'
export const notifyPotatoChanged = () => window.dispatchEvent(new Event(POTATO_CHANGED))
