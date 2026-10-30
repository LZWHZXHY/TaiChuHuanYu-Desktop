import request from '@/utils/request'

export type RenamePay = 'card' | 'exp' | 'potato'

/** 改名：用改名卡、经验或土豆付一次 */
export interface RenameInfo {
  /** 直接付经验的价格 */
  cost: number
  /** 直接付土豆的价格 */
  potatoCost: number
  potatoes: number
  /** 手里的改名卡（交易行买的） */
  cards: number
  username: string
  experience: number
  canAfford: boolean
  level: number
  levelAfter: number
  rules: { minLength: number; maxLength: number; reserveDays: number }
  history: { oldName: string; newName: string; cost: number; pay?: RenamePay; at: string }[]
}
export interface RenameResult { message: string; username: string; experience: number; level: number; token: string }

export const renameApi = {
  info: () => request.get<RenameInfo>('/Users/Rename', { silentError: true }),
  check: (name: string) => request.get<{ available: boolean; message: string }>('/Users/Rename/check', { params: { name }, silentError: true }),
  rename: (newName: string, pay: RenamePay) => request.post<RenameResult>('/Users/Rename', { newName, pay }, { silentError: true }),
}
