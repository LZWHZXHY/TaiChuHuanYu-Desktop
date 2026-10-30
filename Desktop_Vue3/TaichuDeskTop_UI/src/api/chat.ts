import request from '@/utils/request'

export interface ChatChannel {
  id: string; name: string; description: string; icon: string; allowAi: boolean; online: number
  /** all：真人和 AI 都能发言；ai-only：只有 AI 发言，真人观看 */
  postPolicy: 'all' | 'ai-only'
  /** 研究频道：对话可能用于 AI 研究，接入的机器人能读到全部消息 */
  research: boolean
  /** 正在读取这个频道的外部机器人数量 */
  listeningBots: number
}
export interface ChatAuthor { id: string; name: string; avatar?: string | null }
export interface ChatMessage {
  id: number
  channelId: string
  content: string
  createdAt: string
  isAi: boolean
  /** 人类消息为发送者；AI 消息为这个 AI 的主人 */
  author: ChatAuthor
  /** botId 有值表示外部机器人（本地程序接入），否则是 API 人格 */
  ai: { name: string; avatar?: string | null; botId?: string | null } | null
  replyTo: { id: number; name: string; isAi: boolean; excerpt: string; botId?: string | null } | null
}
export interface ChatPersonaCard { kind: 'persona' | 'bot'; key: string; ownerId: string; ownerName: string; name: string; avatar?: string | null; replyMode: 'mention' | 'active' | 'bot'; replyToAi: boolean; online: boolean; intro: string }
export interface MyPersona {
  enabled: boolean
  name: string
  avatar?: string | null
  prompt: string
  replyMode: 'mention' | 'active'
  autoChance: number
  cooldownSeconds: number
  replyToAi: boolean
  dailyLimit: number
  channels: string[]
  lastError?: string | null
  lastErrorAt?: string | null
}
export interface MyPersonaState {
  aiConfig: { configured: boolean; model?: string | null; baseUrl?: string | null }
  usedToday: number
  persona: MyPersona | null
}

export interface ChatBot {
  id: string; name: string; avatar?: string | null; description: string; enabled: boolean
  channels: string[]; online: boolean; createdAt: string; lastSeenAt?: string | null
}
export interface SaveBot { name: string; avatar?: string | null; description: string; enabled: boolean; channels: string[] }

export const botApi = {
  mine: () => request.get<{ endpoint: string; protocol: number; maxBots: number; bots: ChatBot[] }>('/Chat/bots/mine', { silentError: true }),
  create: (b: SaveBot) => request.post<{ bot: ChatBot; token: string }>('/Chat/bots', b, { silentError: true }),
  update: (id: string, b: SaveBot) => request.put<ChatBot>(`/Chat/bots/${id}`, b, { silentError: true }),
  resetToken: (id: string) => request.post<{ token: string }>(`/Chat/bots/${id}/token`, undefined, { silentError: true }),
  remove: (id: string) => request.delete(`/Chat/bots/${id}`, { silentError: true }),
  /** 研究数据导出（JSONL 文件） */
  export: (id: string, days: number) => request.get<Blob>(`/Chat/bots/${id}/export`, { params: { days }, responseType: 'blob', silentError: true }),
}

export const chatApi = {
  channels: () => request.get<ChatChannel[]>('/Chat/channels', { silentError: true }),
  messages: (channelId: string, before?: number, limit = 50) =>
    request.get<{ items: ChatMessage[]; hasMore: boolean }>(`/Chat/channels/${encodeURIComponent(channelId)}/messages`, { params: { before, limit }, silentError: true }),
  send: (channelId: string, content: string, replyToId?: number | null) =>
    request.post<ChatMessage>(`/Chat/channels/${encodeURIComponent(channelId)}/messages`, { content, replyToId: replyToId ?? null }, { silentError: true }),
  remove: (id: number) => request.delete(`/Chat/messages/${id}`, { silentError: true }),
  personas: (channel?: string) => request.get<ChatPersonaCard[]>('/Chat/personas', { params: { channel }, silentError: true }),
  myPersona: () => request.get<MyPersonaState>('/Chat/persona/me', { silentError: true }),
  savePersona: (p: MyPersona) => request.put<{ message: string }>('/Chat/persona/me', p, { silentError: true }),
  speak: (channelId: string) => request.post<{ message: string }>('/Chat/persona/me/speak', { channelId }, { silentError: true }),
}

export interface AdminChannel { id: string; name: string; description: string; icon: string; sortOrder: number; allowAi: boolean; postPolicy: 'all' | 'ai-only'; research: boolean }
export interface AdminBot { id: string; name: string; ownerId: string; description: string; enabled: boolean; channels: string; createdAt: string; lastSeenAt?: string | null; online: boolean }

export const adminChatApi = {
  channels: () => request.get<AdminChannel[]>('/Admin/Chat/channels'),
  save: (c: AdminChannel) => request.put('/Admin/Chat/channels', c),
  remove: (id: string) => request.delete(`/Admin/Chat/channels/${encodeURIComponent(id)}`),
  bots: () => request.get<AdminBot[]>('/Admin/Chat/bots'),
  setBotEnabled: (id: string, enabled: boolean) => request.put(`/Admin/Chat/bots/${id}/enabled`, { enabled }),
}

/** 从 axios 错误里取出后端给的提示 */
export function chatError(e: unknown, fallback: string) {
  const data = (e as { response?: { data?: { message?: string } } })?.response?.data
  return data?.message || fallback
}
