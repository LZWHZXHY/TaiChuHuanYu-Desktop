// 观战：四个联机游戏（斗地主、象棋、围棋、五子棋）共用的类型和小工具。

/** 服务器状态里的观战信息（后端 SpectatorRegistry.View，四个游戏字段一样） */
export interface WatchInfo {
  /** 房主是否允许观战（默认允许） */
  allow: boolean
  /** 每桌最多多少人观战 */
  max: number
  /** 正在观战的人 */
  list: { userId: string; name: string }[]
}

export const EMPTY_WATCH: WatchInfo = { allow: true, max: 20, list: [] }

/** 大厅房间列表里「观战」按钮的状态和提示 */
export function watchButton(room: { spectators?: number; allowSpectate?: boolean }, max: number, loggedIn: boolean): { disabled: boolean; title: string } {
  if (!loggedIn) return { disabled: true, title: '登录后才能观战' }
  if (room.allowSpectate === false) return { disabled: true, title: '房主没开观战' }
  if ((room.spectators ?? 0) >= max) return { disabled: true, title: `观战的人满了（最多 ${max} 人）` }
  return { disabled: false, title: '进去看看，只能看不能动' }
}
