import request from '@/utils/request'

export interface BilibiliStats {
  follower: number
  mid: string
  url: string
}

export const bilibiliApi = {
  stats: () => request.get<BilibiliStats>('/Bilibili/stats', { silentError: true })
}
