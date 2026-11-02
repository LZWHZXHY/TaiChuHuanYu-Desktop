/** 联机五子棋的房间与对局数据（来自 GameHub 推送） */
export interface GobangPlayer { id: string; name: string; isOwner?: boolean }
export interface GobangRoom {
  roomId: string
  roomName: string
  boardSize: number
  timeLimit: number
  /** 房主执黑先行 */
  ownerId: string
  players: GobangPlayer[]
}
export interface GobangStart {
  currentTurn: string
  startedAt: number
  /** 观战中途进来时服务器给的当前棋盘（0 空 / 1 黑 / 2 白）；正常开局时没有 */
  board?: number[][] | null
  /** 观战中途进来时这一步还剩几秒 */
  remainingTime?: number
  /** 观战中途进来时已经下了几手（棋谱编号接着往下数） */
  moveCount?: number
}
