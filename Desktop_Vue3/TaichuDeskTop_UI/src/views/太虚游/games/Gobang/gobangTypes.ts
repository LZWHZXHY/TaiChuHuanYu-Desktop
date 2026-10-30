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
export interface GobangStart { currentTurn: string; startedAt: number }
