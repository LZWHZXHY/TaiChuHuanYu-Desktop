// src/api/pomodoro.ts
// 太初灵脉 · 番茄钟的接口（网页版和桌面程序共用一份计时状态和专注记录）。
// 时间一律是毫秒时间戳（Date.now() 那种），和桌面程序约好的，字段名不要改。
import request from '@/utils/request'

/** '' 待命 / focus 专注 / short 短休息 / long 长休息 */
export type PomoPhase = '' | 'focus' | 'short' | 'long'
export type PomoSource = 'desktop' | 'web'

export interface PomoState {
  phase: PomoPhase
  /** 在做的事（最多 60 字） */
  task: string
  /** 这一段开始的时间；专注记录的 clientId = "f" + 它 */
  startedMs: number
  /** 到点时间；暂停时为 0 */
  endsMs: number
  /** 暂停时还剩多少毫秒；没暂停为 0 */
  pausedLeftMs: number
  /** 这一段一共多长 */
  totalMs: number
  /** 这一组已经完成了几轮专注 */
  round: number
  /** 待命时建议的下一段 */
  next: PomoPhase
  /** 关联的生活管理事项 */
  plannerEventId: string | null
  /** 重复事项是哪一天 "YYYY-MM-DD" */
  plannerDate: string | null
  updatedMs: number
  source: string
}

export interface PomoSettings {
  focusMin: number
  shortMin: number
  longMin: number
  /** 几轮专注后来一次长休息；0 = 不要长休息 */
  longEvery: number
  autoBreak: boolean
  autoFocus: boolean
}

export interface PomoSession {
  clientId: string
  startMs: number
  endMs: number
  /** 实际专注了多少秒（不含暂停） */
  focusSec: number
  task: string
  /** true 完成 / false 中途放弃 */
  done: boolean
  plannerEventId: string | null
  plannerDate: string | null
  source: string
}

export interface PomoStateResponse {
  state: PomoState
  /** 还没设置过时是 null，用默认值 */
  settings: PomoSettings | null
  version: number
  serverNowMs: number
  /** 桌面程序 90 秒内来问过 = 在线，到点由桌面提醒 */
  desktopOnline: boolean
  /** 只有 409（另一端刚改过）时才有 */
  conflict?: boolean
}

export interface PomoPutBody {
  baseVersion: number
  state: PomoState
  /** null = 设置不变 */
  settings: PomoSettings | null
  source: PomoSource
}

// 错误提示由页面自己显示（silentError），不弹全局错误框
const quiet = { silentError: true }

export const pomodoroApi = {
  getState: (client: PomoSource = 'web') =>
    request.get<PomoStateResponse>(`/pomodoro/state?client=${client}`, quiet),
  /**
   * 409（另一端刚改过）不当成出错：直接返回服务器现在的状态（带 conflict: true），页面照着更新。
   */
  putState: (body: PomoPutBody) =>
    request.put<PomoStateResponse>('/pomodoro/state', body, {
      ...quiet,
      validateStatus: (s: number) => (s >= 200 && s < 300) || s === 409,
    }),
  postSessions: (sessions: PomoSession[]) =>
    request.post<{ saved: number; clientIds: string[] }>('/pomodoro/sessions', { sessions }, quiet),
  /** 按开始时间查（包含 fromMs，不包含 toMs）；都不传 = 最近 30 天 */
  listSessions: (fromMs?: number, toMs?: number) => {
    const q = fromMs != null && toMs != null ? `?fromMs=${Math.floor(fromMs)}&toMs=${Math.floor(toMs)}` : ''
    return request.get<PomoSession[]>(`/pomodoro/sessions${q}`, quiet)
  },
  removeSession: (clientId: string) =>
    request.delete<{ ok: boolean }>(`/pomodoro/sessions/${encodeURIComponent(clientId)}`, quiet),
}
