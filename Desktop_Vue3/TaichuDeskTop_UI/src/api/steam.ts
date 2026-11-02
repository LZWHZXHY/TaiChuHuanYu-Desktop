import request from '@/utils/request'

/** Steam 绑定（fix173）：用 Steam 官方登录绑定，资料由后端用 Steam Web API 读、存一份快照 */
export interface SteamGame {
  appId: number
  name: string
  /** 总时长（分钟）；主人设置了不显示时长时为 0 */
  minutes: number
  /** 最近两周（分钟） */
  minutes2Weeks?: number | null
  icon?: string | null
}

export interface SteamCardData {
  steamId: string
  name?: string | null
  avatar?: string | null
  profileUrl?: string | null
  /** 0 离线 1 在线 2 忙碌 3 离开 4 打盹 5 想交易 6 想玩游戏；null = 主人设置了不显示 */
  state?: number | null
  playing?: string | null
  playingAppId?: number | null
  profilePublic: boolean
  level?: number | null
  createdAt?: number | null
  gameCount?: number | null
  totalMinutes?: number | null
  top?: SteamGame[] | null
  recent?: SteamGame[] | null
  featured?: SteamGame | null
  summaryAt?: string | null
  gamesAt?: string | null
}

export interface SteamSettings { showStatus: boolean; showGames: boolean; showPlaytime: boolean }

export type MySteam =
  | { bound: false; keyReady: boolean }
  | { bound: true; keyReady: boolean; steamId: string; boundAt: string; settings: SteamSettings; card: SteamCardData | null }

export const steamApi = {
  mine: () => request.get<MySteam>('/steam/me', { silentError: true }),
  /** 返回 Steam 登录页地址；back = 绑完跳回的页面 */
  start: (back: string) => request.post<{ url: string }>('/steam/bind/start', { back }, { silentError: true }),
  saveSettings: (s: Partial<SteamSettings>) => request.put('/steam/me/settings', s, { silentError: true }),
  refresh: () => request.post<SteamCardData>('/steam/me/refresh', undefined, { silentError: true }),
  unbind: () => request.delete('/steam/me', { silentError: true }),
  /** 别人主页上的卡片；没绑定时返回空 */
  ofUser: (userId: string) => request.get<{ steamId: string; showPlaytime: boolean; card: SteamCardData } | ''>(`/steam/user/${userId}`, { silentError: true }),
}

export const STEAM_STATE: Record<number, string> = { 0: '离线', 1: '在线', 2: '忙碌', 3: '离开', 4: '打盹', 5: '想交易', 6: '想玩游戏' }

/** 分钟 → 「12.5 小时」「40 分钟」 */
export function fmtPlaytime(min?: number | null): string {
  if (!min) return '0 小时'
  if (min < 60) return `${min} 分钟`
  const h = min / 60
  return `${h >= 100 ? Math.round(h).toLocaleString() : h.toFixed(1).replace(/\.0$/, '')} 小时`
}
