// 生活管理用到的日期计算、重复事项展开、导出 .ics。
// 日期一律用 "YYYY-MM-DD" 字符串在 UTC 下算天数，避免时区和夏令时把日期算错一天。
import type { PlannerEvent, PlannerEventInput, PlannerChannel, PlannerRepeat } from '@/api/planner'

export const pad = (n: number) => String(n).padStart(2, '0')

/** Date（本地时间）→ "YYYY-MM-DD" */
export const toDateStr = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
export const todayStr = () => toDateStr(new Date())

export const parseDateStr = (s: string) => {
  const [y = 1970, m = 1, d = 1] = s.split('-').map(Number)
  return { y, m, d }
}
/** 从 1970-01-01 起第几天，用来比较、相减 */
export const dayIndex = (s: string) => {
  const { y, m, d } = parseDateStr(s)
  return Math.round(Date.UTC(y, m - 1, d) / 86400000)
}
export const fromDayIndex = (n: number) => {
  const t = new Date(n * 86400000)
  return `${t.getUTCFullYear()}-${pad(t.getUTCMonth() + 1)}-${pad(t.getUTCDate())}`
}
export const addDays = (s: string, n: number) => fromDayIndex(dayIndex(s) + n)
export const daysInMonth = (y: number, m: number) => new Date(Date.UTC(y, m, 0)).getUTCDate()
/** 星期几：0 = 周一 … 6 = 周日 */
export const weekdayMon0 = (s: string) => (new Date(dayIndex(s) * 86400000).getUTCDay() + 6) % 7
export const isValidDate = (s: string | null | undefined): s is string => !!s && /^\d{4}-\d{2}-\d{2}$/.test(s)

const WEEK = ['一', '二', '三', '四', '五', '六', '日']
/** 「11月1日 周六」 */
export const formatDayLabel = (s: string) => {
  const { m, d } = parseDateStr(s)
  return `${m}月${d}日 周${WEEK[weekdayMon0(s)]}`
}
export const WEEK_HEADERS = WEEK

export const timeToMin = (t: string | null | undefined) => {
  if (!t || !/^\d{2}:\d{2}$/.test(t)) return null
  const [h = 0, m = 0] = t.split(':').map(Number)
  return h * 60 + m
}

/** 这条安排在某一天有没有（重复事项按规则算；按月重复遇到小月落在月底，和后端一致） */
export function occursOn(ev: PlannerEvent, date: string): boolean {
  if (date < ev.date) return false
  if (ev.repeat !== 'none' && ev.repeatUntil && date > ev.repeatUntil) return false
  switch (ev.repeat) {
    case 'daily': return true
    case 'weekly': return (dayIndex(date) - dayIndex(ev.date)) % 7 === 0
    case 'monthly': {
      const a = parseDateStr(ev.date)
      const b = parseDateStr(date)
      return b.d === Math.min(a.d, daysInMonth(b.y, b.m))
    }
    default: return date === ev.date
  }
}

export const isDoneOn = (ev: PlannerEvent, date: string) =>
  ev.isDone || (ev.repeat !== 'none' && ev.doneDates.includes(date))

/** 某一天里的一次安排（重复事项每天展开成一条） */
export interface Occurrence {
  key: string
  ev: PlannerEvent
  date: string
  done: boolean
}

/** 全天的排前面，其余按开始时间排 */
export const compareOcc = (a: Occurrence, b: Occurrence) => {
  if (a.ev.allDay !== b.ev.allDay) return a.ev.allDay ? -1 : 1
  const ta = timeToMin(a.ev.startTime) ?? 0
  const tb = timeToMin(b.ev.startTime) ?? 0
  return ta - tb || a.ev.title.localeCompare(b.ev.title, 'zh-CN')
}

/** 把一段日期里的安排展开成「日期 → 当天的安排」 */
export function expandRange(events: PlannerEvent[], from: string, to: string): Record<string, Occurrence[]> {
  const map: Record<string, Occurrence[]> = {}
  const start = dayIndex(from)
  const end = dayIndex(to)
  if (end < start || end - start > 400) return map
  for (let i = start; i <= end; i++) {
    const date = fromDayIndex(i)
    const list: Occurrence[] = []
    for (const ev of events) {
      if (occursOn(ev, date)) list.push({ key: `${ev.id}@${date}`, ev, date, done: isDoneOn(ev, date) })
    }
    if (list.length) map[date] = list.sort(compareOcc)
  }
  return map
}

export interface DayCell { date: string; dayNum: number; inMonth: boolean; isToday: boolean; weekend: boolean }

/** 月历 6 行 × 7 列（周一开头） */
export function buildMonthGrid(year: number, month0: number): DayCell[] {
  const first = `${year}-${pad(month0 + 1)}-01`
  const start = dayIndex(first) - weekdayMon0(first)
  const today = todayStr()
  const cells: DayCell[] = []
  for (let i = 0; i < 42; i++) {
    const date = fromDayIndex(start + i)
    const { m, d } = parseDateStr(date)
    cells.push({ date, dayNum: d, inMonth: m === month0 + 1, isToday: date === today, weekend: i % 7 >= 5 })
  }
  return cells
}

// ---------------- 选项 ----------------

export const REPEAT_OPTIONS: { value: PlannerRepeat; label: string }[] = [
  { value: 'none', label: '不重复' },
  { value: 'daily', label: '每天' },
  { value: 'weekly', label: '每周' },
  { value: 'monthly', label: '每月' },
]

/** 提醒选项：数字 = 提前多少分钟 */
export type RemindChoice = 'none' | 'custom' | `${number}`
export const REMIND_OPTIONS: { value: RemindChoice; label: string }[] = [
  { value: 'none', label: '不提醒' },
  { value: '0', label: '准时' },
  { value: '5', label: '提前 5 分钟' },
  { value: '15', label: '提前 15 分钟' },
  { value: '30', label: '提前 30 分钟' },
  { value: '60', label: '提前 1 小时' },
  { value: '1440', label: '提前 1 天' },
  { value: 'custom', label: '自定义时间' },
]

export const CHANNEL_OPTIONS: { value: PlannerChannel; label: string; hint: string }[] = [
  { value: 'site', label: '站内通知', hint: '右上角铃铛里会有一条' },
  { value: 'browser', label: '浏览器弹窗', hint: '网页开着时弹系统通知' },
  { value: 'email', label: '邮件', hint: '发到账号绑定的邮箱' },
]

/** 预设分类和默认颜色；用户也可以自己写分类 */
export const CATEGORY_PRESETS: { name: string; color: string }[] = [
  { name: '生活', color: '#2f9e6e' },
  { name: '工作', color: '#2463eb' },
  { name: '学习', color: '#7c4dde' },
  { name: '健康', color: '#e8590c' },
  { name: '娱乐', color: '#d6336c' },
  { name: '其他', color: '#868e96' },
]
export const COLOR_SWATCHES = ['#2463eb', '#2f9e6e', '#7c4dde', '#e8590c', '#d6336c', '#0c8599', '#c49a00', '#868e96']
export const DEFAULT_COLOR = '#2463eb'

export const colorOf = (ev: PlannerEvent) =>
  ev.color || CATEGORY_PRESETS.find(c => c.name === ev.category)?.color || DEFAULT_COLOR

/** 「09:00–10:30」/「全天」 */
export const timeLabel = (ev: PlannerEvent) =>
  ev.allDay || !ev.startTime ? '全天' : ev.endTime ? `${ev.startTime}–${ev.endTime}` : ev.startTime

export function remindLabel(ev: PlannerEvent) {
  if (ev.remindAt) return `提醒：${ev.remindAt.replace('T', ' ')}`
  if (ev.remindOffset == null) return ''
  const hit = REMIND_OPTIONS.find(o => o.value === String(ev.remindOffset))
  return `提醒：${hit ? hit.label : `提前 ${ev.remindOffset} 分钟`}`
}

export const repeatLabel = (r: PlannerRepeat) => REPEAT_OPTIONS.find(o => o.value === r)?.label || '不重复'

/** 浏览器所在时区比 UTC 快多少分钟（北京时间 = 480） */
export const browserTzOffset = () => -new Date().getTimezoneOffset()

/** 已有安排 → 编辑表单的初始值 */
export function toInput(ev: PlannerEvent): PlannerEventInput {
  return {
    title: ev.title, date: ev.date, startTime: ev.startTime, endTime: ev.endTime, allDay: ev.allDay,
    color: ev.color, category: ev.category, memo: ev.memo, location: ev.location, isDone: ev.isDone,
    repeat: ev.repeat, repeatUntil: ev.repeatUntil, remindOffset: ev.remindOffset, remindAt: ev.remindAt,
    channels: [...ev.channels], tzOffset: browserTzOffset(),
  }
}

// ---------------- 导出 .ics（手机日历、Outlook、Google 日历都能导入） ----------------

const icsText = (s: string) => s.replace(/\\/g, '\\\\').replace(/;/g, '\\;').replace(/,/g, '\\,').replace(/\r?\n/g, '\\n')
const icsDate = (s: string) => s.replace(/-/g, '')
const icsDateTime = (date: string, time: string) => `${icsDate(date)}T${time.replace(':', '')}00`
// 每行最长 75 个字符，超出的折行（下一行以空格开头）
const fold = (line: string) => {
  const out: string[] = []
  for (let i = 0; i < line.length; i += 70) out.push((i ? ' ' : '') + line.slice(i, i + 70))
  return out.join('\r\n')
}

export function toIcs(events: PlannerEvent[]): string {
  const now = new Date()
  const stamp = `${now.getUTCFullYear()}${pad(now.getUTCMonth() + 1)}${pad(now.getUTCDate())}T${pad(now.getUTCHours())}${pad(now.getUTCMinutes())}${pad(now.getUTCSeconds())}Z`
  const lines = ['BEGIN:VCALENDAR', 'VERSION:2.0', 'PRODID:-//TaiChu//LifePlanner//ZH', 'CALSCALE:GREGORIAN', 'X-WR-CALNAME:太初灵脉 · 生活管理']
  for (const ev of events) {
    lines.push('BEGIN:VEVENT', `UID:${ev.id}@taichu-planner`, `DTSTAMP:${stamp}`)
    if (ev.allDay || !ev.startTime) {
      lines.push(`DTSTART;VALUE=DATE:${icsDate(ev.date)}`, `DTEND;VALUE=DATE:${icsDate(addDays(ev.date, 1))}`)
    } else {
      // 不带时区的「本地时间」：导入到哪台设备就按那台设备的时区显示
      lines.push(`DTSTART:${icsDateTime(ev.date, ev.startTime)}`)
      if (ev.endTime) lines.push(`DTEND:${icsDateTime(ev.date, ev.endTime)}`)
    }
    if (ev.repeat !== 'none') {
      const freq = ev.repeat === 'daily' ? 'DAILY' : ev.repeat === 'weekly' ? 'WEEKLY' : 'MONTHLY'
      lines.push(`RRULE:FREQ=${freq}${ev.repeatUntil ? `;UNTIL=${icsDate(ev.repeatUntil)}` : ''}`)
    }
    lines.push(fold(`SUMMARY:${icsText(ev.title)}`))
    if (ev.location) lines.push(fold(`LOCATION:${icsText(ev.location)}`))
    const desc = [ev.category ? `分类：${ev.category}` : '', ev.memo].filter(Boolean).join('\n')
    if (desc) lines.push(fold(`DESCRIPTION:${icsText(desc)}`))
    if (ev.repeat === 'none' && ev.isDone) lines.push('STATUS:COMPLETED')
    lines.push('END:VEVENT')
  }
  lines.push('END:VCALENDAR')
  return lines.join('\r\n') + '\r\n'
}

export function downloadText(filename: string, text: string, type = 'text/calendar;charset=utf-8') {
  const url = URL.createObjectURL(new Blob([text], { type }))
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  a.remove()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}
