/**
 * 世界编年史：自定义历法 + 时间换算 + 轴的分段（时间跳跃折叠）。
 *
 * 历法（每个世界自己定）：
 *  - 纪元 eras：名字、起点（这个纪元的第 1 年对应「绝对年」几）、正数还是倒数（像公元前，越往前数字越大）
 *  - 月份 months：每个月叫什么、有几天（一年几个月随便定）
 *  - 也可以不用月日，只写年份；还可以只写模糊时间（「第三纪元末」），这种不上轴，放在「时间未定」
 *
 * 「绝对时间」= 绝对年 + 一年里过了多少（0～1），用来排序和画轴。
 */

export interface Era {
  id: string
  name: string
  /** 这个纪元第 1 年 = 绝对年几 */
  start: number
  /** 倒着数（像公元前：1 年在最后，越早数字越大） */
  backward?: boolean
  color?: string
}
export interface CalMonth { name: string; days: number }
export interface Calendar {
  eras: Era[]
  months: CalMonth[]
  /** 年的叫法：年 / 载 / 周期…… */
  yearUnit?: string
  /** 没有月份时只显示年 */
  useMonths?: boolean
  /** 轴上自动把大段空白折叠起来 */
  autoCompress?: boolean
}

export type EventKind = 'event' | 'period' | 'skip' | 'lost'
export type TimelineStatus = 'canon' | 'alt' | 'whatif' | 'draft'

export interface ChronicleTimeline {
  id: string
  name: string
  description?: string | null
  color?: string | null
  status: TimelineStatus
  parentId?: string | null
  forkEventId?: string | null
  mergeEventId?: string | null
  cardId?: string | null
  sortOrder: number
  hidden?: boolean
  /** 前端生成的「条目事件」线（来自各条目自己的时间线，只读） */
  virtual?: boolean
}
export interface ChronicleEvent {
  id: string
  timelineId: string
  kind: EventKind
  title: string
  description?: string | null
  eraId?: string | null
  year?: number | null
  month?: number | null
  day?: number | null
  endEraId?: string | null
  endYear?: number | null
  endMonth?: number | null
  endDay?: number | null
  dateText?: string | null
  color?: string | null
  images: string[]
  tags: string[]
  cardIds: string[]
  locationCardId?: string | null
  hidden?: boolean
  sortOrder: number
  updatedAt?: string
  /** 「条目事件」：来自哪个条目（只读，点了打开那个条目） */
  sourceCardId?: string
}

export const STATUS_META: Record<TimelineStatus, { label: string; hint: string }> = {
  canon: { label: '正史', hint: '确定发生过的' },
  alt: { label: '支线', hint: '另一条走向，和正史并存' },
  whatif: { label: '假如', hint: '「如果当年……」的推演' },
  draft: { label: '废案', hint: '暂时不用的设定' },
}
export const KIND_META: Record<EventKind, { label: string; hint: string; icon: string }> = {
  event: { label: '事件', hint: '某个时间点发生的事', icon: '●' },
  period: { label: '时期', hint: '持续一段时间：战争、朝代、旅程', icon: '▬' },
  skip: { label: '时间跳跃', hint: '中间省略的一段，轴上折叠起来', icon: '⋯' },
  lost: { label: '失落的历史', hint: '故意标成未知的一段', icon: '▧' },
}
export const LINE_COLORS = ['#2a7f8f', '#c0504d', '#d99a2b', '#5b8c3a', '#7a5cc4', '#3d6fb0', '#c46a9a', '#8a6a3a', '#4f5b66']

export const DEFAULT_MONTHS: CalMonth[] = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31].map((days, i) => ({ name: `${i + 1}月`, days }))
export function defaultCalendar(): Calendar {
  return {
    eras: [
      { id: 'era-ad', name: '公元', start: 1 },
      { id: 'era-bc', name: '公元前', start: 0, backward: true },
    ],
    months: DEFAULT_MONTHS.map(m => ({ ...m })),
    yearUnit: '年',
    useMonths: true,
    autoCompress: true,
  }
}

/** 整理服务器 / 用户给的历法，缺的补默认值 */
export function normalizeCalendar(raw: any): Calendar {
  const def = defaultCalendar()
  if (!raw || typeof raw !== 'object') return def
  const eras: Era[] = (Array.isArray(raw.eras) ? raw.eras : []).filter((e: any) => e && typeof e.name === 'string' && Number.isFinite(Number(e.start)))
    .map((e: any, i: number) => ({ id: String(e.id || `era-${i}`), name: e.name.slice(0, 30), start: Math.trunc(Number(e.start)), backward: Boolean(e.backward), color: typeof e.color === 'string' ? e.color : undefined }))
  const months: CalMonth[] = (Array.isArray(raw.months) ? raw.months : []).filter((m: any) => m && typeof m.name === 'string')
    .map((m: any) => ({ name: m.name.slice(0, 20), days: Math.min(10000, Math.max(1, Math.trunc(Number(m.days) || 30))) })).slice(0, 1000)
  return {
    eras: eras.length ? eras.slice(0, 50) : def.eras,
    months: months.length ? months : def.months,
    yearUnit: typeof raw.yearUnit === 'string' && raw.yearUnit.trim() ? raw.yearUnit.slice(0, 6) : '年',
    useMonths: raw.useMonths !== false,
    autoCompress: raw.autoCompress !== false,
  }
}

export const daysPerYear = (cal: Calendar) => cal.months.reduce((s, m) => s + m.days, 0) || 365
export const eraOf = (cal: Calendar, id?: string | null) => cal.eras.find(e => e.id === id) ?? defaultEra(cal)
/** 没选纪元时用第一个正着数的纪元 */
export const defaultEra = (cal: Calendar) => cal.eras.find(e => !e.backward) ?? cal.eras[0]!

/** 纪元年 → 绝对年 */
export function absYear(cal: Calendar, eraId: string | null | undefined, year: number) {
  const era = eraOf(cal, eraId)
  return era.backward ? era.start - (year - 1) : era.start + (year - 1)
}
/** 绝对年 → 用哪个纪元、第几年（显示刻度用） */
export function eraYearOf(cal: Calendar, abs: number): { era: Era; year: number } {
  const fw = cal.eras.filter(e => !e.backward).sort((a, b) => b.start - a.start).find(e => abs >= e.start)
  if (fw) return { era: fw, year: abs - fw.start + 1 }
  const bw = cal.eras.filter(e => e.backward).sort((a, b) => a.start - b.start).find(e => abs <= e.start)
  if (bw) return { era: bw, year: bw.start - abs + 1 }
  // 比所有纪元都早：用最早的正向纪元算出 0 或负数年
  const first = cal.eras.slice().sort((a, b) => a.start - b.start)[0] ?? defaultEra(cal)
  return { era: first, year: first.backward ? first.start - abs + 1 : abs - first.start + 1 }
}

/** 一个时间点的绝对时间（年为单位，带小数）；没有年份返回 null */
export function timeValue(cal: Calendar, eraId?: string | null, year?: number | null, month?: number | null, day?: number | null): number | null {
  if (year === null || year === undefined || !Number.isFinite(year)) return null
  const base = absYear(cal, eraId, year)
  if (!month || !cal.useMonths) return base
  const dpy = daysPerYear(cal)
  const m = Math.min(Math.max(1, month), cal.months.length)
  let before = 0
  for (let i = 0; i < m - 1; i++) before += cal.months[i]!.days
  const d = day ? Math.min(Math.max(1, day), cal.months[m - 1]!.days) - 1 : 0
  return base + (before + d) / dpy
}
export const eventStart = (cal: Calendar, e: ChronicleEvent) => timeValue(cal, e.eraId, e.year, e.month, e.day)
/** 结束时间：时期 / 跳跃 / 失落的历史才有；结束那天算进去 */
export function eventEnd(cal: Calendar, e: ChronicleEvent) {
  if (e.kind === 'event') return eventStart(cal, e)
  const v = timeValue(cal, e.endEraId ?? e.eraId, e.endYear, e.endMonth, e.endDay)
  if (v === null) return eventStart(cal, e)
  // 只写了年：算到那年年底
  return e.endMonth ? v + 1 / daysPerYear(cal) : v + 1
}

/** 显示时间：「公元前 300 年 3月 5日」 */
export function formatDate(cal: Calendar, eraId?: string | null, year?: number | null, month?: number | null, day?: number | null) {
  if (year === null || year === undefined) return ''
  const era = eraOf(cal, eraId)
  let s = `${era.name} ${year} ${cal.yearUnit || '年'}`
  if (month && cal.useMonths) {
    s += ` ${cal.months[month - 1]?.name ?? `${month}月`}`
    if (day) s += ` ${day}日`
  }
  return s
}
export function formatEventDate(cal: Calendar, e: ChronicleEvent) {
  // 条目里的旧时间线事件：显示原来写的文字
  if (e.sourceCardId) return e.dateText || '时间未定'
  const start = formatDate(cal, e.eraId, e.year, e.month, e.day)
  if (!start) return e.dateText || '时间未定'
  let s = start
  if (e.kind !== 'event' && e.endYear !== null && e.endYear !== undefined) {
    const end = formatDate(cal, e.endEraId ?? e.eraId, e.endYear, e.endMonth, e.endDay)
    // 同一纪元就不重复写纪元名
    s += ' — ' + (eraOf(cal, e.endEraId ?? e.eraId).id === eraOf(cal, e.eraId).id ? end.replace(eraOf(cal, e.eraId).name + ' ', '') : end)
  }
  return e.dateText ? `${s}（${e.dateText}）` : s
}
/** 刻度上的年份：「公元 1200」 */
export function formatAbsYear(cal: Calendar, abs: number) {
  const { era, year } = eraYearOf(cal, Math.floor(abs))
  return `${era.name} ${year}`
}
/** 两个绝对时间相差多少，说人话 */
export function formatSpan(cal: Calendar, years: number) {
  const unit = cal.yearUnit || '年'
  if (years >= 1) return `${Math.round(years).toLocaleString('zh-CN')} ${unit}`
  const days = Math.round(years * daysPerYear(cal))
  return days >= 1 ? `${days} 天` : '不到一天'
}

// ================= 轴：把时间映射到横坐标，时间跳跃 / 大段空白折叠成固定宽度 =================

export interface AxisSegment { t0: number; t1: number; x0: number; x1: number; folded: boolean; label?: string }
export interface Axis { segments: AxisSegment[]; width: number; min: number; max: number; x: (t: number) => number; t: (x: number) => number }

export const FOLD_WIDTH = 64

/**
 * @param points  所有要上轴的时间点（事件开始、结束）
 * @param skips   明确的时间跳跃 [起, 止, 名称]
 * @param pxPerYear 缩放：一年多少像素
 * @param autoCompress 自动折叠：两个相邻时间点的空白超过「总跨度的 20%」且至少是中位间隔的 8 倍
 */
export function buildAxis(points: number[], skips: { t0: number; t1: number; label: string }[], pxPerYear: number, autoCompress: boolean, cal: Calendar, pad = 60, padRight = 240): Axis {
  const pts = [...new Set(points.filter(Number.isFinite))].sort((a, b) => a - b)
  if (!pts.length) pts.push(0, 1)
  if (pts.length === 1) pts.push(pts[0]! + 1)
  const min = pts[0]!, max = pts[pts.length - 1]!
  // 要折叠的区间
  const folds: { t0: number; t1: number; label: string }[] = skips.filter(s => s.t1 > s.t0).map(s => ({ ...s }))
  if (autoCompress && pts.length > 2) {
    const gaps = pts.slice(1).map((p, i) => p - pts[i]!).filter(g => g > 0).sort((a, b) => a - b)
    const median = gaps[Math.floor(gaps.length / 2)] ?? 0
    const span = max - min
    for (let i = 1; i < pts.length; i++) {
      const g = pts[i]! - pts[i - 1]!
      if (g > span * 0.2 && g > median * 8 && g * pxPerYear > FOLD_WIDTH * 3) {
        const margin = Math.min(g * 0.05, 40 / pxPerYear)
        folds.push({ t0: pts[i - 1]! + margin, t1: pts[i]! - margin, label: `⋯ ${formatSpan(cal, g)} ⋯` })
      }
    }
  }
  // 合并重叠的折叠区间
  folds.sort((a, b) => a.t0 - b.t0)
  const merged: typeof folds = []
  for (const f of folds) {
    const last = merged[merged.length - 1]
    if (last && f.t0 <= last.t1) { last.t1 = Math.max(last.t1, f.t1); last.label = `⋯ ${formatSpan(cal, last.t1 - last.t0)} ⋯` }
    else merged.push({ ...f })
  }
  const lo = Math.min(min, ...merged.map(f => f.t0)), hi = Math.max(max, ...merged.map(f => f.t1))
  const segments: AxisSegment[] = []
  let x = pad, cursor = lo
  for (const f of merged) {
    const a = Math.max(cursor, f.t0)
    if (a > cursor) { segments.push({ t0: cursor, t1: a, x0: x, x1: x + (a - cursor) * pxPerYear, folded: false }); x += (a - cursor) * pxPerYear }
    const b = Math.max(a, f.t1)
    segments.push({ t0: a, t1: b, x0: x, x1: x + FOLD_WIDTH, folded: true, label: f.label })
    x += FOLD_WIDTH; cursor = b
  }
  if (hi > cursor || !segments.length) { segments.push({ t0: cursor, t1: Math.max(hi, cursor + 1e-9), x0: x, x1: x + (hi - cursor) * pxPerYear, folded: false }); x += (hi - cursor) * pxPerYear }
  // 右边多留点，最后一件事的文字放得下
  const width = x + padRight
  const toX = (t: number) => {
    if (t <= segments[0]!.t0) return segments[0]!.x0 - (segments[0]!.t0 - t) * pxPerYear
    for (const s of segments) if (t <= s.t1) return s.t1 === s.t0 ? s.x0 : s.x0 + (t - s.t0) / (s.t1 - s.t0) * (s.x1 - s.x0)
    const last = segments[segments.length - 1]!
    return last.x1 + (t - last.t1) * pxPerYear
  }
  const toT = (px: number) => {
    for (const s of segments) if (px <= s.x1) return s.x1 === s.x0 ? s.t0 : s.t0 + (px - s.x0) / (s.x1 - s.x0) * (s.t1 - s.t0)
    const last = segments[segments.length - 1]!
    return last.t1 + (px - last.x1) / pxPerYear
  }
  return { segments, width, min: lo, max: hi, x: toX, t: toT }
}

/** 刻度：每段里按合适的步长（1、2、5 × 10ⁿ 年）标年份 */
export function axisTicks(axis: Axis, pxPerYear: number): { x: number; t: number }[] {
  const target = 110 / pxPerYear // 大约每 110px 一个刻度
  const pow = Math.pow(10, Math.floor(Math.log10(Math.max(target, 1e-6))))
  const step = [1, 2, 5, 10].map(m => m * pow).find(s => s >= target) ?? pow * 10
  const out: { x: number; t: number }[] = []
  for (const s of axis.segments) {
    if (s.folded) continue
    const first = Math.ceil(s.t0 / step) * step
    for (let t = first; t <= s.t1 + 1e-9 && out.length < 400; t += step) {
      const x = axis.x(t)
      // 紧挨着折叠带的刻度不标，免得文字挤在一起
      if (axis.segments.some(f => f.folded && x > f.x0 - 36 && x < f.x1 + 36)) continue
      out.push({ x, t })
    }
  }
  return out
}

/** 纪元分界线：每个正向纪元的起点、每个倒数纪元的终点（第 1 年结束） */
export function eraBoundaries(cal: Calendar): { t: number; era: Era }[] {
  const out: { t: number; era: Era }[] = []
  for (const e of cal.eras) out.push({ t: e.backward ? e.start + 1 : e.start, era: e })
  return out.sort((a, b) => a.t - b.t).filter((b, i, arr) => i === 0 || Math.abs(b.t - arr[i - 1]!.t) > 1e-9 || !b.era.backward)
}

/** 时间线排成一行行：主线按顺序，支线紧跟在它的父线下面 */
export function orderLanes(lines: ChronicleTimeline[]): { line: ChronicleTimeline; depth: number }[] {
  const byParent = new Map<string, ChronicleTimeline[]>()
  const ids = new Set(lines.map(l => l.id))
  for (const l of lines) {
    const key = l.parentId && ids.has(l.parentId) ? l.parentId : ''
    if (!byParent.has(key)) byParent.set(key, [])
    byParent.get(key)!.push(l)
  }
  for (const list of byParent.values()) list.sort((a, b) => a.sortOrder - b.sortOrder)
  const out: { line: ChronicleTimeline; depth: number }[] = []
  const seen = new Set<string>()
  const walk = (key: string, depth: number) => {
    for (const l of byParent.get(key) ?? []) {
      if (seen.has(l.id)) continue
      seen.add(l.id); out.push({ line: l, depth }); walk(l.id, depth + 1)
    }
  }
  walk('', 0)
  for (const l of lines) if (!seen.has(l.id)) out.push({ line: l, depth: 0 }) // 防御：绕圈的也显示出来
  return out
}

export function newLocalId(prefix = 'x') {
  return `${prefix}${Date.now().toString(36)}${Math.random().toString(36).slice(2, 8)}`
}

// ================= 标题里的引用：「{CARD:某人} 写下了 {CARD:某书}」 =================

export type TitlePart = { text: string; cardId?: string; broken?: boolean }
const REF = /!?\{CARD:([^}]+)\}/g
/** 拆成一段段：普通文字 / 引用（点了打开那个条目） */
export function titleParts(title: string, findCard: (id: string) => { title: string } | undefined): TitlePart[] {
  const out: TitlePart[] = []
  let last = 0
  for (const m of title.matchAll(REF)) {
    if (m.index! > last) out.push({ text: title.slice(last, m.index) })
    const card = findCard(m[1]!)
    out.push(card ? { text: card.title, cardId: m[1]! } : { text: '已删除的条目', broken: true })
    last = m.index! + m[0].length
  }
  if (last < title.length) out.push({ text: title.slice(last) })
  return out
}
export const plainTitle = (title: string, findCard: (id: string) => { title: string } | undefined) =>
  titleParts(title, findCard).map(p => p.text).join('')
/** 文字里引用了哪些条目 */
export const refIds = (text?: string | null) => [...(text ?? '').matchAll(REF)].map(m => m[1]!)

// ================= 导出 / 备份 / 复制世界时带上编年史 =================

/** 导出文件里的编年史：历法 + 时间线 + 事件（Id 是原世界里的，恢复时服务器全部换成新 Id） */
export interface ChronicleExport {
  calendar: unknown
  timelines: ChronicleTimeline[]
  events: ChronicleEvent[]
}

/** 从接口读到的编年史整理成导出用的：去掉只给页面用的字段（更新时间、条目里的虚拟线）；什么都没写过就返回 null */
export function toChronicleExport(data: { calendar: unknown; timelines: ChronicleTimeline[]; events: ChronicleEvent[] }): ChronicleExport | null {
  const timelines = data.timelines.filter(l => !l.virtual)
  if (!timelines.length && data.calendar == null) return null
  return {
    calendar: data.calendar ?? null,
    timelines: timelines.map(l => ({
      id: l.id, name: l.name, description: l.description ?? null, color: l.color ?? null, status: l.status,
      parentId: l.parentId ?? null, forkEventId: l.forkEventId ?? null, mergeEventId: l.mergeEventId ?? null,
      cardId: l.cardId ?? null, sortOrder: l.sortOrder, hidden: Boolean(l.hidden),
    })),
    events: data.events.filter(e => !e.sourceCardId).map(e => ({
      id: e.id, timelineId: e.timelineId, kind: e.kind, title: e.title, description: e.description ?? null,
      eraId: e.eraId ?? null, year: e.year ?? null, month: e.month ?? null, day: e.day ?? null,
      endEraId: e.endEraId ?? null, endYear: e.endYear ?? null, endMonth: e.endMonth ?? null, endDay: e.endDay ?? null,
      dateText: e.dateText ?? null, color: e.color ?? null, images: [...e.images], tags: [...e.tags], cardIds: [...e.cardIds],
      locationCardId: e.locationCardId ?? null, hidden: Boolean(e.hidden), sortOrder: e.sortOrder,
    })),
  }
}
