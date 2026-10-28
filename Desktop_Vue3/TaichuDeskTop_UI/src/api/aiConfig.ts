import request from '@/utils/request'

export interface AiConfigInfo {
  baseUrl: string
  model: string
  maskedKey: string
  hasKey: boolean
}

export interface ImportCardItem {
  title: string
  type: string
  subType: string | null
  description: string | null
  attributes: { key: string; value: string }[]
  tags: string[]
}

export const aiConfigApi = {
  get: () => request.get<AiConfigInfo>('/AiConfig', { silentError: true }),
  save: (payload: { baseUrl: string; model: string; apiKey?: string }) =>
    request.put<{ message: string }>('/AiConfig', payload, { silentError: true }),
  organizeWorld: (projectId: string) =>
    request.post<{ result: string }>('/AiConfig/organize-world', { projectId }, { silentError: true }),
  importWorld: (projectId: string, text: string) =>
    request.post<{ cards: ImportCardItem[] }>('/AiConfig/import-world', { projectId, text }, { silentError: true }),
  confirmImport: (projectId: string, cards: ImportCardItem[]) =>
    request.post<{ created: number; errors: string[] }>('/AiConfig/import-world/confirm', { projectId, cards }, { silentError: true })
}
