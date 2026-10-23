import request from '@/utils/request'

export interface GameRecordDto {
  id: string
  gameType: string
  player1Name: string
  player2Name: string
  winner: string
  movesCount: number
  createdAt: string
}

export const gameRecordApi = {
  save: (payload: {
    gameType: string
    player1Name?: string
    player2Name?: string
    winner: string
    movesCount: number
    movesJson?: string
  }) => request.post<{ id: string }>('/GameRecord', payload, { silentError: true }),

  my: (type?: string) => request.get<GameRecordDto[]>('/GameRecord/my', { params: { type }, silentError: true }),

  recent: (type?: string, limit = 20) =>
    request.get<GameRecordDto[]>('/GameRecord/recent', { params: { type, limit }, silentError: true })
}
