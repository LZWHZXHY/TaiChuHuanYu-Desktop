// 番茄钟的计时规则（和桌面程序一致），全是不碰网络的纯函数，方便两端对照。
// 状态的含义：
// · 待命：phase = ''，其它时间都是 0，next 是建议的下一段；
// · 计时中：endsMs = 到点时间，pausedLeftMs = 0；
// · 已暂停：endsMs = 0，pausedLeftMs = 还剩多少毫秒（继续时 endsMs = 现在 + 它）。
// startedMs 在暂停 / 继续时不变，所以一段专注的记录编号（"f" + startedMs）两端算出来是一样的。
import type { PomoPhase, PomoSession, PomoSettings, PomoState } from '@/api/pomodoro'

export const DEFAULT_SETTINGS: PomoSettings = { focusMin: 25, shortMin: 5, longMin: 15, longEvery: 4, autoBreak: false, autoFocus: false }

/** 预设：专注 / 短休息（分钟），长休息不动 */
export const PRESETS: { focus: number; short: number }[] = [
  { focus: 25, short: 5 },
  { focus: 50, short: 10 },
  { focus: 90, short: 15 },
]

export const PHASE_LABEL: Record<PomoPhase, string> = { '': '待命', focus: '专注', short: '短休息', long: '长休息' }
/** 专注满这么久才在「放弃」时记一条（不到 1 分钟多半是点错了） */
export const MIN_ABANDON_SEC = 60
export const MAX_TASK = 60

const clampInt = (v: unknown, min: number, max: number, fallback: number) => {
  const n = Math.round(Number(v))
  return Number.isFinite(n) ? Math.min(max, Math.max(min, n)) : fallback
}

/** 设置的范围和后端一致 */
export function normalizeSettings(s: Partial<PomoSettings> | null | undefined): PomoSettings {
  const v = s || {}
  return {
    focusMin: clampInt(v.focusMin, 1, 240, DEFAULT_SETTINGS.focusMin),
    shortMin: clampInt(v.shortMin, 1, 120, DEFAULT_SETTINGS.shortMin),
    longMin: clampInt(v.longMin, 1, 240, DEFAULT_SETTINGS.longMin),
    longEvery: clampInt(v.longEvery, 0, 12, DEFAULT_SETTINGS.longEvery),
    autoBreak: !!v.autoBreak,
    autoFocus: !!v.autoFocus,
  }
}

const PHASES: PomoPhase[] = ['', 'focus', 'short', 'long']
const asPhase = (p: unknown, fallback: PomoPhase): PomoPhase => (PHASES.includes(p as PomoPhase) ? (p as PomoPhase) : fallback)

export const idleState = (): PomoState => ({
  phase: '', task: '', startedMs: 0, endsMs: 0, pausedLeftMs: 0, totalMs: 0, round: 0, next: 'focus',
  plannerEventId: null, plannerDate: null, updatedMs: 0, source: 'web',
})

/** 服务器给的状态再过一遍，缺字段的补上，防止页面因为脏数据出错 */
export function normalizeState(s: Partial<PomoState> | null | undefined): PomoState {
  const base = idleState()
  if (!s) return base
  const num = (x: unknown) => (Number.isFinite(Number(x)) ? Math.max(0, Number(x)) : 0)
  return {
    phase: asPhase(s.phase, ''),
    task: typeof s.task === 'string' ? s.task : '',
    startedMs: num(s.startedMs),
    endsMs: num(s.endsMs),
    pausedLeftMs: num(s.pausedLeftMs),
    totalMs: num(s.totalMs),
    round: Math.floor(num(s.round)),
    next: asPhase(s.next, 'focus'),
    plannerEventId: s.plannerEventId || null,
    plannerDate: s.plannerDate || null,
    updatedMs: num(s.updatedMs),
    source: typeof s.source === 'string' ? s.source : base.source,
  }
}

export const phaseMinutes = (settings: PomoSettings, phase: PomoPhase) =>
  phase === 'focus' ? settings.focusMin : phase === 'short' ? settings.shortMin : phase === 'long' ? settings.longMin : 0

export const isRunning = (s: PomoState) => s.phase !== '' && s.endsMs > 0
export const isPaused = (s: PomoState) => s.phase !== '' && s.endsMs === 0
export const remainingMs = (s: PomoState, now: number) =>
  isRunning(s) ? Math.max(0, s.endsMs - now) : isPaused(s) ? s.pausedLeftMs : 0

/** 这一段专注实际专注了多少秒（不含暂停的时间） */
export const focusedSec = (s: PomoState, now: number) =>
  Math.max(0, Math.round((s.totalMs - remainingMs(s, now)) / 1000))

export const sessionClientId = (startedMs: number) => `f${startedMs}`

/** 关联到生活管理的哪件事 */
export interface PlannerLink { eventId: string; date: string | null; title: string }

/** 开始一段（专注 / 短休息 / 长休息） */
export function startPhase(s: PomoState, phase: Exclude<PomoPhase, ''>, settings: PomoSettings, now: number): PomoState {
  const totalMs = phaseMinutes(settings, phase) * 60_000
  return { ...s, phase, startedMs: now, endsMs: now + totalMs, pausedLeftMs: 0, totalMs }
}

export function pause(s: PomoState, now: number): PomoState {
  if (!isRunning(s)) return s
  // 至少留 1 毫秒，不然「暂停」和「到点」分不清
  return { ...s, endsMs: 0, pausedLeftMs: Math.max(1, s.endsMs - now) }
}

export function resume(s: PomoState, now: number): PomoState {
  if (!isPaused(s)) return s
  return { ...s, endsMs: now + Math.max(1, s.pausedLeftMs), pausedLeftMs: 0 }
}

/** 回到待命（保留任务、关联事项和轮数，方便接着来） */
const toIdle = (s: PomoState, round: number, next: PomoPhase): PomoState =>
  ({ ...s, phase: '', startedMs: 0, endsMs: 0, pausedLeftMs: 0, totalMs: 0, round, next })

export interface FinishResult {
  state: PomoState
  /** 专注结束时要记的一条 */
  session: PomoSession | null
  /** 刚结束的是哪一段 */
  ended: PomoPhase
  /** 有没有按设置自动开始下一段 */
  autoStarted: boolean
}

/**
 * 一段结束（到点，或者点了「提前完成」）。规则和桌面一致：
 * · 专注结束：轮数 +1，记一条完成的记录；下一段 = 满 longEvery 轮就长休息，否则短休息；
 * · 长休息结束：轮数清零；休息结束：下一段 = 专注；
 * · 开了「自动开始休息 / 专注」就直接开始下一段。
 */
export function finishPhase(s: PomoState, settings: PomoSettings, now: number, early: boolean, source: string): FinishResult {
  const ended = s.phase
  let session: PomoSession | null = null
  let idle: PomoState
  if (ended === 'focus') {
    const round = s.round + 1
    const next: PomoPhase = settings.longEvery > 0 && round % settings.longEvery === 0 ? 'long' : 'short'
    session = {
      clientId: sessionClientId(s.startedMs),
      startMs: s.startedMs,
      // 到点结束按原定的到点时间算（网页可能等了桌面十几秒才接手）；提前完成按现在
      endMs: early || !s.endsMs ? now : Math.min(now, s.endsMs),
      focusSec: early ? focusedSec(s, now) : Math.round(s.totalMs / 1000),
      task: s.task,
      done: true,
      plannerEventId: s.plannerEventId,
      plannerDate: s.plannerDate,
      source,
    }
    idle = toIdle(s, round, next)
  } else if (ended === 'short' || ended === 'long') {
    idle = toIdle(s, ended === 'long' ? 0 : s.round, 'focus')
  } else {
    return { state: s, session: null, ended, autoStarted: false }
  }
  const auto = idle.next !== '' && (ended === 'focus' ? settings.autoBreak : settings.autoFocus)
  if (auto && idle.next !== '') return { state: startPhase(idle, idle.next, settings, now), session, ended, autoStarted: true }
  return { state: idle, session, ended, autoStarted: false }
}

/**
 * 放弃 / 先停一下：不算一轮。专注满 1 分钟的记一条「放弃」的记录。
 * 休息被停掉时，下一段还是这段休息（想休息时再点开始）。
 */
export function abandon(s: PomoState, now: number, source: string): { state: PomoState; session: PomoSession | null } {
  if (s.phase === '') return { state: s, session: null }
  if (s.phase !== 'focus') return { state: toIdle(s, s.round, s.phase), session: null }
  const sec = focusedSec(s, now)
  const session: PomoSession | null = sec >= MIN_ABANDON_SEC
    ? {
        clientId: sessionClientId(s.startedMs), startMs: s.startedMs, endMs: now, focusSec: sec, task: s.task, done: false,
        plannerEventId: s.plannerEventId, plannerDate: s.plannerDate, source,
      }
    : null
  return { state: toIdle(s, s.round, 'focus'), session }
}

// ---------------- 显示用 ----------------

const pad = (n: number) => String(n).padStart(2, '0')

/** 倒计时显示：「24:59」，一小时以上「1:29:59」（向上取整，到 0 才算到点） */
export function fmtClock(ms: number) {
  const total = Math.max(0, Math.ceil(ms / 1000))
  const h = Math.floor(total / 3600)
  const m = Math.floor((total % 3600) / 60)
  const sec = total % 60
  return h > 0 ? `${h}:${pad(m)}:${pad(sec)}` : `${pad(m)}:${pad(sec)}`
}

/** 「1 小时 25 分」/「25 分钟」 */
export function fmtMinutes(sec: number) {
  const min = Math.round(sec / 60)
  if (min < 60) return `${min} 分钟`
  const h = Math.floor(min / 60)
  const m = min % 60
  return m ? `${h} 小时 ${m} 分` : `${h} 小时`
}

export const fmtTime = (ms: number) => {
  const d = new Date(ms)
  return `${pad(d.getHours())}:${pad(d.getMinutes())}`
}

/** 本地日期的 0 点（毫秒） */
export const dayStartMs = (ms: number) => {
  const d = new Date(ms)
  return new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime()
}
/** 往前 / 往后挪几天的 0 点（用 Date 算，夏令时也不会错一天） */
export const shiftDayStart = (startMs: number, days: number) => {
  const d = new Date(startMs)
  return new Date(d.getFullYear(), d.getMonth(), d.getDate() + days).getTime()
}
/** "YYYY-MM-DD"（本地日期）的 0 点 */
export const dateStrStartMs = (date: string) => {
  const [y = 1970, m = 1, d = 1] = date.split('-').map(Number)
  return new Date(y, m - 1, d).getTime()
}

export interface DayStat { start: number; label: string; minutes: number; done: number; isToday: boolean }

const WEEK = ['日', '一', '二', '三', '四', '五', '六']

/** 最近 7 天每天专注了多少分钟、完成了几轮（今天在最右边） */
export function last7Days(sessions: PomoSession[], now: number): DayStat[] {
  const today = dayStartMs(now)
  const days: DayStat[] = []
  for (let i = 6; i >= 0; i--) {
    const start = shiftDayStart(today, -i)
    const end = shiftDayStart(start, 1)
    let sec = 0
    let done = 0
    for (const s of sessions) {
      if (s.startMs < start || s.startMs >= end) continue
      sec += s.focusSec
      if (s.done) done++
    }
    const d = new Date(start)
    days.push({ start, label: i === 0 ? '今天' : `周${WEEK[d.getDay()]}`, minutes: Math.round(sec / 60), done, isToday: i === 0 })
  }
  return days
}

// ---------------- 提示音（WebAudio 现场合成，不用音频文件） ----------------

let audioCtx: AudioContext | null = null

/** 浏览器要求「用户点过」才能出声：点开始按钮时先把声音通道打开 */
export function unlockAudio() {
  try {
    const Ctor = window.AudioContext || (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
    if (!Ctor) return
    if (!audioCtx) audioCtx = new Ctor()
    if (audioCtx.state === 'suspended') void audioCtx.resume()
  } catch { /* 不支持就算了，只是没声音 */ }
}

/** 到点提示音：三声由低到高的「叮」 */
export function playChime() {
  unlockAudio()
  const ctx = audioCtx
  if (!ctx) return
  try {
    const t0 = ctx.currentTime + 0.02
    ;[660, 880, 1175].forEach((freq, i) => {
      const osc = ctx.createOscillator()
      const gain = ctx.createGain()
      osc.type = 'sine'
      osc.frequency.value = freq
      const t = t0 + i * 0.28
      gain.gain.setValueAtTime(0.0001, t)
      gain.gain.exponentialRampToValueAtTime(0.25, t + 0.02)
      gain.gain.exponentialRampToValueAtTime(0.0001, t + 0.9)
      osc.connect(gain).connect(ctx.destination)
      osc.start(t)
      osc.stop(t + 0.95)
    })
  } catch { /* 出不了声就只显示横幅 */ }
}
