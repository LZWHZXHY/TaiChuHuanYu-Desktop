// 转盘用到的小工具：调色板、扇区角度、按权重随机、颜色对比、缩略图
import type { WheelSegment } from '@/api/wheel'

/** 默认调色板（和后端 WheelService.Palette 一致：没选颜色的扇区按顺序用这些） */
export const PALETTE = ['#E4572E', '#F3A712', '#29335C', '#669BBC', '#A8C686', '#7D5BA6', '#D1495B', '#00798C', '#EDAE49', '#30638E', '#B56576', '#3D9970']

/** 和后端一致的默认上限（真正的上限以 /wheel/pricing 返回为准） */
export const DEFAULT_LIMITS = { minSegments: 2, maxSegments: 24, titleMax: 40, introMax: 300, segmentTextMax: 20, weightMax: 1000, potatoPrizeMax: 100000, expPrizeMax: 10000 }

export interface Arc { start: number; end: number; mid: number }

/**
 * 每个扇区占多少角度（从 12 点方向开始顺时针，单位：度）。
 * 大小按权重分，但每块至少有平分时的一半，免得权重小的扇区细得看不见、字也写不下。
 * 所以扇区大小只是「大致」反映概率，真正的概率看 probabilities()。
 */
export function layoutArcs(segments: Pick<WheelSegment, 'weight'>[]): Arc[] {
  const n = segments.length
  if (n === 0) return []
  const total = segments.reduce((s, x) => s + Math.max(1, x.weight || 1), 0)
  const minShare = 0.5 / n
  const raw = segments.map(x => Math.max(Math.max(1, x.weight || 1) / total, minShare))
  const sum = raw.reduce((a, b) => a + b, 0)
  let at = 0
  return raw.map(r => {
    const size = (r / sum) * 360
    const arc = { start: at, end: at + size, mid: at + size / 2 }
    at += size
    return arc
  })
}

/** 每个扇区被转到的概率（0–1），完全按权重算 */
export function probabilities(segments: Pick<WheelSegment, 'weight'>[]): number[] {
  const total = segments.reduce((s, x) => s + Math.max(1, x.weight || 1), 0)
  return segments.map(x => (total ? Math.max(1, x.weight || 1) / total : 0))
}

export const percent = (p: number) => (p >= 0.1 ? `${(p * 100).toFixed(0)}%` : p >= 0.01 ? `${(p * 100).toFixed(1)}%` : `${(p * 100).toFixed(2)}%`)

/** 0 到 max-1 的均匀随机整数（用浏览器的加密随机数，拒绝采样避免取模偏差） */
export function secureInt(max: number): number {
  if (max <= 1) return 0
  const limit = Math.floor(0x100000000 / max) * max
  const buf = new Uint32Array(1)
  for (;;) {
    crypto.getRandomValues(buf)
    const v = buf[0] ?? 0
    if (v < limit) return v % max
  }
}

/** 按权重抽一个扇区（用户转盘在浏览器里抽；官方转盘由服务器抽） */
export function pickWeighted(segments: Pick<WheelSegment, 'weight'>[]): number {
  const weights = segments.map(x => Math.max(1, Math.round(x.weight || 1)))
  let roll = secureInt(weights.reduce((a, b) => a + b, 0))
  for (let i = 0; i < weights.length; i++) {
    roll -= weights[i] ?? 1
    if (roll < 0) return i
  }
  return weights.length - 1
}

/** 背景色上用白字还是黑字（按亮度算） */
export function textOn(hex: string): string {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex)
  if (!m || !m[1]) return '#fff'
  const v = parseInt(m[1], 16)
  const r = (v >> 16) & 255, g = (v >> 8) & 255, b = v & 255
  const lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255
  return lum > 0.62 ? '#1b1b1b' : '#ffffff'
}

/** 列表卡片上的小转盘：用 CSS 圆锥渐变画，几十个也不卡 */
export function conicOf(colors: { color: string; weight: number }[]): string {
  if (!colors.length) return 'var(--bg-sub)'
  const arcs = layoutArcs(colors)
  const stops = arcs.map((a, i) => `${colors[i]?.color ?? '#999'} ${a.start.toFixed(2)}deg ${a.end.toFixed(2)}deg`)
  return `conic-gradient(${stops.join(', ')})`
}

/** 新扇区的默认颜色：挑调色板里还没用过的，用完了就按序号轮 */
export function nextColor(used: string[]): string {
  const set = new Set(used.map(c => c.toUpperCase()))
  return PALETTE.find(c => !set.has(c)) ?? PALETTE[used.length % PALETTE.length] ?? '#E4572E'
}

export function prizeText(s: Pick<WheelSegment, 'prizeType' | 'prizeAmount'>): string {
  if (s.prizeType === 'potato') return `${s.prizeAmount.toLocaleString()} 土豆`
  if (s.prizeType === 'exp') return `${s.prizeAmount.toLocaleString()} 经验`
  return ''
}

/** 转盘页的地址参数（仅自己可见的带上分享码 k） */
export function wheelQuery(id: number, code?: string | null): Record<string, string> {
  return code ? { game: 'Wheel', wheel: String(id), k: code } : { game: 'Wheel', wheel: String(id) }
}

/** 把路由给的相对地址变成完整链接（复制给别人用） */
export const absoluteUrl = (href: string) => new URL(href, window.location.href).href
