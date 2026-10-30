/**
 * 茶绘画布引擎：管理图层画布、笔画回放、实时预览、合并底图和导出。只做画图，不管网络。
 *
 * 图层画布 = 底图（合并掉的旧笔画）+ 按服务器编号顺序画上的笔画（撤销掉的跳过）。
 * · 别人正在画的笔画画在最上面的「实时层」上；
 * · 自己正在画、或已经抬笔但服务器还没确认的笔画，画在该图层的「工作画布」上：
 *   工作画布 = 图层画布 + 这些笔画，画的时候用它替换图层显示，所以橡皮和不透明度都是所见即所得。
 *
 * 笔画点是扁平整数数组：x*10, y*10, 压感*100, …
 *
 * 除了画笔 / 橡皮，「笔画」还可以是这些操作（同样由服务器编号、能撤销、会被合并进底图、能回放）：
 * · blur / smudge：模糊、涂抹笔刷，读取图层当前像素计算；
 * · polyfill：多边形填充（实心形状、填充选区；带 FLAG_ERASE 时是删除选区内容）；
 * · xform：把多边形里的内容做仿射变换（移动、翻转选区），带 FLAG_COPY 时保留原处；
 * · gradient：线性 / 径向渐变；
 * · patch：贴一张服务器上的图片（油漆桶的结果），points 第一个点是左上角。
 * 带 clip（选区多边形，x*10, y*10, …）的操作只画在选区里。
 */
import { boxBlur, fromPremul, smudge, toPremul } from './pixelOps'

export type Tool = 'brush' | 'eraser' | 'blur' | 'smudge' | 'polyfill' | 'xform' | 'gradient' | 'patch'
export type BrushKind = 'pen' | 'ink' | 'pencil' | 'marker' | 'soft' | 'airbrush' | 'custom'
/** 自制笔刷参数（和后端 PaintBrushParams 一致） */
export interface BrushParams {
  tip: 'round' | 'image'; tipHash?: string | null
  hardness: number; roundness: number; angle: number; followDirection: boolean
  spacing: number; scatter: number; count: number
  sizeJitter: number; opacityJitter: number; angleJitter: number
  flow: number; minSize: number; pressureSize: boolean; pressureOpacity: boolean
  taperStart: number; taperEnd: number
  size: number; opacity: number; maxSize: number
}
export const DEFAULT_PARAMS: BrushParams = {
  tip: 'round', tipHash: null, hardness: 0.8, roundness: 1, angle: 0, followDirection: false,
  spacing: 0.1, scatter: 0, count: 1, sizeJitter: 0, opacityJitter: 0, angleJitter: 0,
  flow: 1, minSize: 0.2, pressureSize: true, pressureOpacity: false, taperStart: 0, taperEnd: 0,
  size: 16, opacity: 1, maxSize: 200,
}
/** 笔刷开关：压感控制粗细 / 压感控制浓淡 */
export const FLAG_PRESSURE_SIZE = 1
export const FLAG_PRESSURE_OPACITY = 2
/** 渐变：径向（否则线性） */
export const FLAG_RADIAL = 4
/** 变换：复制（原处的内容保留） */
export const FLAG_COPY = 8
/** 多边形填充：擦除（删除选区内容） */
export const FLAG_ERASE = 16
export interface StrokeStyle {
  tool: Tool; color: string; size: number; opacity: number; brush: BrushKind; hardness: number; flags: number; custom?: BrushParams | null
  /** 选区：只画在这个多边形里（x*10, y*10, …） */
  clip?: number[] | null
  /** xform 的仿射矩阵 a, b, c, d, e, f */
  matrix?: number[] | null
  /** 渐变终点颜色；没有时渐变到透明 */
  color2?: string | null
  /** patch 的图片哈希 */
  patch?: string | null
}
export interface StrokeData extends StrokeStyle { points: number[] }
export interface Stroke extends StrokeData { seq: number; layerId: number; userId: string; undone: boolean }
export interface LayerMeta { id: number; name: string; creatorId: string; creatorName: string; opacity: number; locked: boolean; hasBase: boolean; baseVersion: number }

export interface BrushPreset { kind: BrushKind; name: string; desc: string; size: number; opacity: number; hardness: number; flags: number; maxSize: number }
/** 笔刷预设（服务器只认这几种 kind） */
export const BRUSHES: BrushPreset[] = [
  { kind: 'pen', name: '圆头笔', desc: '干净利落，压感控制粗细，日常线稿', size: 6, opacity: 1, hardness: 1, flags: FLAG_PRESSURE_SIZE, maxSize: 160 },
  { kind: 'ink', name: 'G 笔', desc: '描线专用，起笔收笔会变尖', size: 5, opacity: 1, hardness: 1, flags: FLAG_PRESSURE_SIZE, maxSize: 80 },
  { kind: 'pencil', name: '铅笔', desc: '带颗粒感，压感控制浓淡，适合起稿', size: 3, opacity: 0.85, hardness: 1, flags: FLAG_PRESSURE_OPACITY, maxSize: 40 },
  { kind: 'marker', name: '马克笔', desc: '粗细不变、半透明叠色，铺平涂', size: 20, opacity: 0.55, hardness: 1, flags: 0, maxSize: 200 },
  { kind: 'soft', name: '柔边笔', desc: '边缘柔和，上色、过渡、晕染', size: 40, opacity: 0.8, hardness: 0.35, flags: FLAG_PRESSURE_SIZE | FLAG_PRESSURE_OPACITY, maxSize: 300 },
  { kind: 'airbrush', name: '喷枪', desc: '越涂越浓，打光和大面积渐变', size: 90, opacity: 0.7, hardness: 0, flags: FLAG_PRESSURE_OPACITY, maxSize: 300 },
]
export const brushOf = (kind: string | undefined) => BRUSHES.find(b => b.kind === kind) ?? BRUSHES[0]!

// ================= 笔尖图 =================
/** 外部提供：根据哈希取已经载入的笔尖蒙版（白底黑字已经转成透明度），没载入返回 null 并开始载入 */
let tipProvider: (hash: string) => CanvasImageSource | null = () => null
export function setTipProvider(fn: (hash: string) => CanvasImageSource | null) { tipProvider = fn }
/** 外部提供：根据哈希取已经载入的贴图（油漆桶结果），没载入返回 null 并开始载入 */
let patchProvider: (hash: string) => CanvasImageSource | null = () => null
export function setPatchProvider(fn: (hash: string) => CanvasImageSource | null) { patchProvider = fn }
/** 这一笔要用的贴图有没有载入（没有贴图的笔画总是 true） */
export function patchReady(s: Pick<StrokeStyle, 'tool' | 'patch'>) { return s.tool !== 'patch' || !s.patch || !!patchProvider(s.patch) }

/** 旧笔画（没有笔刷字段）按圆头笔处理 */
export function normalizeStyle<T extends Partial<StrokeStyle>>(s: T): T & Pick<StrokeStyle, 'brush' | 'hardness' | 'flags'> {
  const custom = s.brush === 'custom' && s.custom ? { ...DEFAULT_PARAMS, ...s.custom } : null
  const kind: BrushKind = custom ? 'custom' : brushOf(s.brush).kind
  return { ...s, brush: kind, custom, hardness: typeof s.hardness === 'number' ? s.hardness : 1, flags: typeof s.flags === 'number' ? s.flags : FLAG_PRESSURE_SIZE }
}

interface LayerState {
  meta: LayerMeta
  canvas: HTMLCanvasElement
  ctx: CanvasRenderingContext2D
  work: HTMLCanvasElement
  workCtx: CanvasRenderingContext2D
  base: CanvasImageSource | null
  /** 已经画进图层画布的最大编号（用来判断能不能直接叠加） */
  drawnSeq: number
  hidden: boolean
}

interface LiveStroke extends StrokeData { layerId: number }

const ctx2d = (c: HTMLCanvasElement) => c.getContext('2d', { willReadFrequently: false }) as CanvasRenderingContext2D
const clamp01 = (v: number) => Math.max(0, Math.min(1, v))

/** 某个压感（0~100）下的笔刷直径 */
export function widthOf(s: Pick<StrokeStyle, 'brush' | 'size' | 'flags' | 'custom'>, p100: number) {
  const p = clamp01(p100 / 100)
  if (s.brush === 'custom' && s.custom) return s.custom.pressureSize ? s.size * (s.custom.minSize + (1 - s.custom.minSize) * p) : s.size
  if (!(s.flags & FLAG_PRESSURE_SIZE) || s.brush === 'marker') return s.size
  switch (s.brush) {
    case 'ink': return s.size * (0.05 + 0.95 * Math.pow(p, 1.4))
    case 'pencil': return s.size * (0.5 + 0.5 * p)
    case 'soft': case 'airbrush': return s.size * (0.3 + 0.7 * p)
    default: return s.size * (0.2 + 0.8 * p)
  }
}

/** 笔画的外接矩形（文档坐标，已含线宽） */
export function strokeBounds(s: StrokeData, w: number, h: number) {
  const pts = s.points
  let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity
  for (let i = 0; i < pts.length; i += 3) {
    const x = pts[i]! / 10, y = pts[i + 1]! / 10
    if (x < x0) x0 = x; if (x > x1) x1 = x
    if (y < y0) y0 = y; if (y > y1) y1 = y
  }
  const pad = s.size * (0.5 + (s.brush === 'custom' && s.custom ? s.custom.scatter : 0)) + 2
  const bx = Math.max(0, Math.floor(x0 - pad)), by = Math.max(0, Math.floor(y0 - pad))
  const bw = Math.min(w, Math.ceil(x1 + pad)) - bx, bh = Math.min(h, Math.ceil(y1 + pad)) - by
  return bw > 0 && bh > 0 ? { x: bx, y: by, w: bw, h: bh } : null
}

/** 用中点二次曲线把点连起来，每段线宽由 width(压感) 决定（硬边、不随压感变浓淡的笔刷用这个，又快又清晰） */
export function tracePath(c: CanvasRenderingContext2D, pts: number[], width: (p: number) => number) {
  const n = pts.length / 3
  if (n === 0) return
  const X = (i: number) => pts[i * 3]! / 10, Y = (i: number) => pts[i * 3 + 1]! / 10, P = (i: number) => pts[i * 3 + 2]!
  c.lineCap = 'round'
  c.lineJoin = 'round'
  if (n === 1) {
    c.beginPath()
    c.arc(X(0), Y(0), width(P(0)) / 2, 0, Math.PI * 2)
    c.fill()
    return
  }
  if (n === 2) {
    c.lineWidth = width((P(0) + P(1)) / 2)
    c.beginPath(); c.moveTo(X(0), Y(0)); c.lineTo(X(1), Y(1)); c.stroke()
    return
  }
  // 第一段：起点 → 第 0、1 点中点
  let mx = (X(0) + X(1)) / 2, my = (Y(0) + Y(1)) / 2
  c.lineWidth = width(P(0))
  c.beginPath(); c.moveTo(X(0), Y(0)); c.lineTo(mx, my); c.stroke()
  for (let i = 1; i < n - 1; i++) {
    const nx = (X(i) + X(i + 1)) / 2, ny = (Y(i) + Y(i + 1)) / 2
    c.lineWidth = width(P(i))
    c.beginPath(); c.moveTo(mx, my); c.quadraticCurveTo(X(i), Y(i), nx, ny); c.stroke()
    mx = nx; my = ny
  }
  c.lineWidth = width(P(n - 1))
  c.beginPath(); c.moveTo(mx, my); c.lineTo(X(n - 1), Y(n - 1)); c.stroke()
}

// ================= 笔刷印章（柔边、喷枪、铅笔用一个个「印章」沿路径盖出来） =================

const STAMP = 128
const stampCache = new Map<string, HTMLCanvasElement>()

/** 固定种子的伪随机数：每个人生成的铅笔颗粒完全一样 */
function seeded(seed: number) {
  let t = seed >>> 0
  return () => { t += 0x6d2b79f5; let r = Math.imul(t ^ (t >>> 15), 1 | t); r ^= r + Math.imul(r ^ (r >>> 7), 61 | r); return ((r ^ (r >>> 14)) >>> 0) / 4294967296 }
}

function stampOf(kind: BrushKind, color: string, hardness: number): HTMLCanvasElement {
  const h = Math.round(clamp01(hardness) * 20) / 20
  const key = `${kind}|${color}|${h}`
  const hit = stampCache.get(key)
  if (hit) return hit
  const c = document.createElement('canvas')
  c.width = c.height = STAMP
  const x = ctx2d(c)
  const r = STAMP / 2
  if (kind === 'pencil') {
    // 铅笔：圆内随机分布的颗粒
    const rnd = seeded(20261028)
    x.fillStyle = color
    for (let i = 0; i < 900; i++) {
      const a = rnd() * Math.PI * 2, d = Math.sqrt(rnd()) * r * 0.96
      x.globalAlpha = 0.25 + rnd() * 0.75
      x.fillRect(r + Math.cos(a) * d - 2, r + Math.sin(a) * d - 2, 4, 4)
    }
  } else {
    const g = x.createRadialGradient(r, r, 0, r, r, r)
    const inner = kind === 'airbrush' ? 0 : h * 0.98
    g.addColorStop(0, color)
    g.addColorStop(inner, color)
    if (kind === 'airbrush' || h < 0.5) g.addColorStop(inner + (1 - inner) * 0.5, hexAlpha(color, 0.45))
    g.addColorStop(1, hexAlpha(color, 0))
    x.fillStyle = g
    x.fillRect(0, 0, STAMP, STAMP)
  }
  if (stampCache.size > 48) stampCache.delete(stampCache.keys().next().value!)
  stampCache.set(key, c)
  return c
}

function hexAlpha(hex: string, a: number) {
  const n = parseInt(hex.slice(1), 16)
  return `rgba(${(n >> 16) & 255},${(n >> 8) & 255},${n & 255},${a})`
}

/** 这种笔刷要不要用印章画（否则走矢量路径） */
export function usesStamps(s: Pick<StrokeStyle, 'brush' | 'hardness' | 'flags'>) {
  return s.brush === 'custom' || s.brush === 'pencil' || s.brush === 'airbrush' || s.brush === 'soft' || s.hardness < 0.98 || !!(s.flags & FLAG_PRESSURE_OPACITY)
}

/** 沿路径按间距盖印章。不透明度会累积，所以普通笔刷按间距换算每个印章的浓度，让一条直线正好达到目标浓度；喷枪故意越涂越浓 */
export function stampPath(c: CanvasRenderingContext2D, s: StrokeData) {
  const pts = s.points
  const n = pts.length / 3
  if (!n) return
  const hard = s.brush !== 'pencil' && s.brush !== 'airbrush' && s.hardness >= 0.98
  const stamp = hard ? null : stampOf(s.brush, s.color, s.hardness)
  const spacingRatio = s.brush === 'airbrush' ? 0.08 : s.brush === 'pencil' ? 0.25 : 0.1
  const pressureAlpha = (p: number) => (s.flags & FLAG_PRESSURE_OPACITY ? 0.08 + 0.92 * clamp01(p / 100) : 1)
  const dab = (x: number, y: number, p: number, spacing: number) => {
    const w = Math.max(0.6, widthOf(s, p))
    const target = pressureAlpha(p)
    let flow: number
    if (s.brush === 'airbrush') flow = 0.07 * target
    else {
      const overlap = Math.max(1, w / spacing)
      flow = 1 - Math.pow(1 - Math.min(0.999, target), 1 / overlap)
      if (s.brush === 'pencil') flow = Math.min(1, flow * 1.6)
    }
    c.globalAlpha = clamp01(flow)
    if (stamp) c.drawImage(stamp, x - w / 2, y - w / 2, w, w)
    else { c.beginPath(); c.arc(x, y, w / 2, 0, Math.PI * 2); c.fill() }
  }
  c.save()
  c.fillStyle = s.color
  let px = pts[0]! / 10, py = pts[1]! / 10, pp = pts[2]!
  const sp0 = Math.max(0.5, widthOf(s, pp) * spacingRatio)
  dab(px, py, pp, sp0)
  let carry = 0
  for (let i = 1; i < n; i++) {
    const x = pts[i * 3]! / 10, y = pts[i * 3 + 1]! / 10, p = pts[i * 3 + 2]!
    const dist = Math.hypot(x - px, y - py)
    let t = 0
    while (true) {
      const pNow = pp + (p - pp) * (t / Math.max(dist, 1e-6))
      const spacing = Math.max(0.5, widthOf(s, pNow) * spacingRatio)
      const need = spacing - carry
      if (t + need > dist) { carry += dist - t; break }
      t += need
      carry = 0
      const k = t / dist
      dab(px + (x - px) * k, py + (y - py) * k, pp + (p - pp) * k, spacing)
    }
    px = x; py = y; pp = p
  }
  c.restore()
}

// ================= 自制笔刷 =================

const tipColorCache = new Map<string, HTMLCanvasElement>()
/** 图案笔尖上色：用笔尖蒙版的透明度，填上当前颜色 */
function coloredTip(hash: string, color: string): HTMLCanvasElement | null {
  const key = `${hash}|${color}`
  const hit = tipColorCache.get(key)
  if (hit) return hit
  const mask = tipProvider(hash)
  if (!mask) return null
  const c = document.createElement('canvas')
  c.width = c.height = STAMP
  const x = ctx2d(c)
  x.fillStyle = color
  x.fillRect(0, 0, STAMP, STAMP)
  x.globalCompositeOperation = 'destination-in'
  x.drawImage(mask, 0, 0, STAMP, STAMP)
  if (tipColorCache.size > 64) tipColorCache.delete(tipColorCache.keys().next().value!)
  tipColorCache.set(key, c)
  return c
}

/** 用笔画第一个点 + 参数算随机种子：实时预览、确认后、回放、别人那边，抖动和散布都一模一样 */
function strokeSeed(s: StrokeData) {
  let h = 2166136261
  const mix = (v: number) => { h ^= v & 0xffff; h = Math.imul(h, 16777619); h ^= v >>> 16; h = Math.imul(h, 16777619) }
  mix(s.points[0] ?? 0); mix(s.points[1] ?? 0); mix(Math.round(s.size * 10))
  const c = s.custom
  if (c) { mix(Math.round(c.spacing * 1000)); mix(Math.round(c.scatter * 1000)); mix(c.count) }
  return h >>> 0
}

/** 自制笔刷：沿路径按间距盖印章，带散布、数量、角度、扁圆、大小 / 浓度 / 角度抖动、起收笔渐细 */
export function customStampPath(c: CanvasRenderingContext2D, s: StrokeData) {
  const p = s.custom!
  const pts = s.points
  const n = pts.length / 3
  if (!n) return
  const stamp = (p.tip === 'image' && p.tipHash ? coloredTip(p.tipHash, s.color) : null) ?? stampOf('soft', s.color, p.hardness)
  const rnd = seeded(strokeSeed(s))
  // 路径总长（收笔渐细要用）
  let total = 0
  for (let i = 1; i < n; i++) total += Math.hypot(pts[i * 3]! / 10 - pts[i * 3 - 3]! / 10, pts[i * 3 + 1]! / 10 - pts[i * 3 - 2]! / 10)
  const baseAngle = (p.angle * Math.PI) / 180
  // 印章太密时自动放宽间距，避免卡顿
  const avgDiam = Math.max(1, s.size * (p.pressureSize ? (1 + p.minSize) / 2 : 1))
  let spacingMul = 1
  const estimate = (total / Math.max(0.5, avgDiam * p.spacing)) * p.count
  if (estimate > 40000) spacingMul = estimate / 40000
  c.save()
  const dab = (x: number, y: number, pr: number, dist: number, dir: number) => {
    const diam = widthOf(s, pr)
    let taper = 1
    if (p.taperStart > 0) taper = Math.min(taper, dist / p.taperStart)
    if (p.taperEnd > 0) taper = Math.min(taper, (total - dist) / p.taperEnd)
    taper = Math.max(0.05, Math.min(1, taper))
    const alphaBase = p.flow * (p.pressureOpacity ? 0.08 + 0.92 * clamp01(pr / 100) : 1)
    for (let k = 0; k < p.count; k++) {
      const size = Math.max(0.5, diam * taper * (1 - p.sizeJitter * rnd()))
      const alpha = alphaBase * (1 - p.opacityJitter * rnd())
      const ang = baseAngle + (p.followDirection ? dir : 0) + p.angleJitter * (rnd() - 0.5) * Math.PI * 2
      let ox = 0, oy = 0
      if (p.scatter > 0) { const r = p.scatter * diam * Math.sqrt(rnd()), t = rnd() * Math.PI * 2; ox = Math.cos(t) * r; oy = Math.sin(t) * r }
      c.globalAlpha = clamp01(alpha)
      if (ang === 0 && p.roundness >= 0.999) c.drawImage(stamp, x + ox - size / 2, y + oy - size / 2, size, size)
      else {
        c.setTransform(1, 0, 0, 1, 0, 0)
        c.translate(x + ox, y + oy)
        c.rotate(ang)
        c.scale(1, p.roundness)
        c.drawImage(stamp, -size / 2, -size / 2, size, size)
        c.setTransform(1, 0, 0, 1, 0, 0)
      }
    }
  }
  let px = pts[0]! / 10, py = pts[1]! / 10, pp = pts[2]!
  let dir = n > 1 ? Math.atan2(pts[4]! / 10 - py, pts[3]! / 10 - px) : 0
  dab(px, py, pp, 0, dir)
  let carry = 0, walked = 0
  for (let i = 1; i < n; i++) {
    const x = pts[i * 3]! / 10, y = pts[i * 3 + 1]! / 10, pr = pts[i * 3 + 2]!
    const dist = Math.hypot(x - px, y - py)
    if (dist > 0.01) dir = Math.atan2(y - py, x - px)
    let t = 0
    while (true) {
      const pNow = pp + (pr - pp) * (t / Math.max(dist, 1e-6))
      const spacing = Math.max(0.5, widthOf(s, pNow) * p.spacing * spacingMul)
      const need = spacing - carry
      if (t + need > dist) { carry += dist - t; break }
      t += need
      carry = 0
      const k = t / dist
      dab(px + (x - px) * k, py + (y - py) * k, pp + (pr - pp) * k, walked + t, dir)
    }
    walked += dist
    px = x; py = y; pp = pr
  }
  c.restore()
}

// ================= 选区、形状、变换、渐变、贴图、模糊、涂抹 =================

const pathCache = new WeakMap<number[], Path2D>()
/** 扁平数组 → 闭合路径。step = 2：x*10, y*10, …（选区）；step = 3：笔画点 x*10, y*10, 压感 */
function polyPath(pts: number[], step: 2 | 3): Path2D {
  const hit = pathCache.get(pts)
  if (hit) return hit
  const p = new Path2D()
  for (let i = 0; i + 1 < pts.length; i += step) {
    if (i === 0) p.moveTo(pts[i]! / 10, pts[i + 1]! / 10)
    else p.lineTo(pts[i]! / 10, pts[i + 1]! / 10)
  }
  p.closePath()
  pathCache.set(pts, p)
  return p
}
const hasClip = (s: Pick<StrokeStyle, 'clip'>) => !!s.clip && s.clip.length >= 6
function applyClip(c: CanvasRenderingContext2D, s: Pick<StrokeStyle, 'clip'>) { if (hasClip(s)) c.clip(polyPath(s.clip!, 2)) }

/** 多边形（points 每三个一组）的外接矩形，夹在画布里 */
function polyBounds(pts: number[], step: 2 | 3, w: number, h: number, pad = 1) {
  let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity
  for (let i = 0; i + 1 < pts.length; i += step) {
    const x = pts[i]! / 10, y = pts[i + 1]! / 10
    if (x < x0) x0 = x; if (x > x1) x1 = x
    if (y < y0) y0 = y; if (y > y1) y1 = y
  }
  const bx = Math.max(0, Math.floor(x0 - pad)), by = Math.max(0, Math.floor(y0 - pad))
  const bw = Math.min(w, Math.ceil(x1 + pad)) - bx, bh = Math.min(h, Math.ceil(y1 + pad)) - by
  return bw > 0 && bh > 0 ? { x: bx, y: by, w: bw, h: bh } : null
}
/** 两个矩形的交集 */
function intersect(a: { x: number; y: number; w: number; h: number } | null, b: { x: number; y: number; w: number; h: number } | null) {
  if (!a || !b) return a ?? b
  const x = Math.max(a.x, b.x), y = Math.max(a.y, b.y)
  const w = Math.min(a.x + a.w, b.x + b.w) - x, h = Math.min(a.y + a.h, b.y + b.h) - y
  return w > 0 && h > 0 ? { x, y, w, h } : null
}

let maskCanvas: HTMLCanvasElement | null = null
/**
 * 多边形的「硬边」蒙版（不抗锯齿，像素要么全选要么不选），画在 b 大小的画布上（左上角对应 b.x, b.y）。
 * 移动、翻转、删除选区内容用它：挖掉和画回去的像素正好互补，边缘不会留下半透明的残影。
 */
function hardMask(pts: number[], step: 2 | 3, b: { x: number; y: number; w: number; h: number }) {
  const c = (maskCanvas ??= document.createElement('canvas'))
  if (c.width < b.w || c.height < b.h) { c.width = Math.max(c.width, b.w); c.height = Math.max(c.height, b.h) }
  const x = c.getContext('2d', { willReadFrequently: true }) as CanvasRenderingContext2D
  x.setTransform(1, 0, 0, 1, 0, 0)
  x.globalCompositeOperation = 'source-over'
  x.clearRect(0, 0, c.width, c.height)
  x.translate(-b.x, -b.y)
  x.fillStyle = '#000000'
  x.fill(polyPath(pts, step))
  x.setTransform(1, 0, 0, 1, 0, 0)
  const img = x.getImageData(0, 0, b.w, b.h)
  const d = img.data
  for (let i = 3; i < d.length; i += 4) d[i] = d[i]! >= 128 ? 255 : 0
  x.putImageData(img, 0, 0)
  return c
}

function drawPolyfill(target: CanvasRenderingContext2D, s: StrokeData, w: number, h: number) {
  if (s.points.length < 9) return
  if (s.flags & FLAG_ERASE) {
    // 删除选区内容：硬边
    const b = polyBounds(s.points, 3, w, h)
    if (!b) return
    const m = hardMask(s.points, 3, b)
    target.save()
    applyClip(target, s)
    target.globalAlpha = clamp01(s.opacity)
    target.globalCompositeOperation = 'destination-out'
    target.drawImage(m, 0, 0, b.w, b.h, b.x, b.y, b.w, b.h)
    target.restore()
    return
  }
  target.save()
  applyClip(target, s)
  target.globalAlpha = clamp01(s.opacity)
  target.fillStyle = s.color
  target.fill(polyPath(s.points, 3))
  target.restore()
}

function drawGradient(target: CanvasRenderingContext2D, s: StrokeData, w: number, h: number) {
  const p = s.points
  if (p.length < 6) return
  const x0 = p[0]! / 10, y0 = p[1]! / 10, x1 = p[3]! / 10, y1 = p[4]! / 10
  const g = s.flags & FLAG_RADIAL
    ? target.createRadialGradient(x0, y0, 0, x0, y0, Math.max(1, Math.hypot(x1 - x0, y1 - y0)))
    : target.createLinearGradient(x0, y0, x1, y1)
  g.addColorStop(0, s.color)
  g.addColorStop(1, s.color2 ?? hexAlpha(s.color, 0))
  target.save()
  applyClip(target, s)
  target.globalAlpha = clamp01(s.opacity)
  target.fillStyle = g
  target.fillRect(0, 0, w, h)
  target.restore()
}

/** 移动 / 翻转：把多边形里的内容拷到草稿，原处挖掉（复制时不挖），再按矩阵画回去 */
function drawXform(target: CanvasRenderingContext2D, s: StrokeData, scratch: CanvasRenderingContext2D, w: number, h: number) {
  const m = s.matrix
  if (!m || m.length !== 6 || s.points.length < 9) return
  const b = polyBounds(s.points, 3, w, h)
  if (!b) return
  const mask = hardMask(s.points, 3, b)
  scratch.clearRect(b.x, b.y, b.w, b.h)
  scratch.save()
  scratch.drawImage(target.canvas, b.x, b.y, b.w, b.h, b.x, b.y, b.w, b.h)
  scratch.globalCompositeOperation = 'destination-in'
  scratch.drawImage(mask, 0, 0, b.w, b.h, b.x, b.y, b.w, b.h)
  scratch.restore()
  target.save()
  if (!(s.flags & FLAG_COPY)) {
    target.globalCompositeOperation = 'destination-out'
    target.drawImage(mask, 0, 0, b.w, b.h, b.x, b.y, b.w, b.h)
  }
  target.globalCompositeOperation = 'source-over'
  target.globalAlpha = 1
  // 只是平移 / 翻转时像素一一对应，不需要插值
  const exact = m.every(v => Number.isInteger(v))
  target.imageSmoothingEnabled = !exact
  target.transform(m[0]!, m[1]!, m[2]!, m[3]!, m[4]!, m[5]!)
  target.drawImage(scratch.canvas, b.x, b.y, b.w, b.h, b.x, b.y, b.w, b.h)
  target.restore()
  scratch.clearRect(b.x, b.y, b.w, b.h)
}

function drawPatch(target: CanvasRenderingContext2D, s: StrokeData) {
  const img = s.patch ? patchProvider(s.patch) : null
  if (!img || s.points.length < 3) return
  target.save()
  applyClip(target, s)
  target.globalAlpha = clamp01(s.opacity)
  target.drawImage(img, Math.round(s.points[0]! / 10), Math.round(s.points[1]! / 10))
  target.restore()
}

/** 选区蒙版（区域内每个像素 0~255），没有选区返回 null */
function clipMask(s: StrokeData, scratch: CanvasRenderingContext2D, b: { x: number; y: number; w: number; h: number }) {
  if (!hasClip(s)) return null
  scratch.clearRect(b.x, b.y, b.w, b.h)
  scratch.save()
  scratch.fillStyle = '#000000'
  scratch.fill(polyPath(s.clip!, 2))
  scratch.restore()
  const d = scratch.getImageData(b.x, b.y, b.w, b.h).data
  scratch.clearRect(b.x, b.y, b.w, b.h)
  const out = new Uint8ClampedArray(b.w * b.h)
  for (let i = 0; i < out.length; i++) out[i] = d[i * 4 + 3]!
  return out
}

/** 模糊半径（像素）由笔刷大小决定 */
export const blurRadiusOf = (size: number) => Math.max(1, Math.min(24, Math.round(size / 7)))

let filterOk: boolean | null = null
/** 浏览器支不支持画布滤镜（ctx.filter = 'blur()'，用显卡算，快得多）；不支持时用 JS 算 */
function canvasBlurSupported() {
  if (filterOk !== null) return filterOk
  try {
    const c = document.createElement('canvas')
    c.width = c.height = 9
    const x = c.getContext('2d', { willReadFrequently: true })!
    if (typeof x.filter !== 'string') return (filterOk = false)
    x.filter = 'blur(2px)'
    x.fillStyle = '#000000'
    x.fillRect(4, 4, 1, 1)
    filterOk = x.getImageData(1, 4, 1, 1).data[3]! > 0
  } catch { filterOk = false }
  return filterOk
}
let blurCanvas: HTMLCanvasElement | null = null

/** 模糊笔刷：把笔画覆盖的区域模糊一次，再按覆盖程度（柔边）和强度混回去 */
function drawBlur(target: CanvasRenderingContext2D, s: StrokeData, scratch: CanvasRenderingContext2D, w: number, h: number) {
  const b = intersect(strokeBounds(s, w, h), hasClip(s) ? polyBounds(s.clip!, 2, w, h) : null)
  if (!b) return
  const r = blurRadiusOf(s.size)
  // 覆盖程度：用柔边笔刷画在草稿上（带选区）
  scratch.clearRect(b.x, b.y, b.w, b.h)
  scratch.save()
  scratch.beginPath(); scratch.rect(b.x, b.y, b.w, b.h); scratch.clip()
  applyClip(scratch, s)
  stampPath(scratch, { ...s, tool: 'brush', brush: 'soft', color: '#000000', opacity: 1, flags: 0, hardness: Math.min(0.9, s.hardness) })
  scratch.restore()
  const strength = clamp01(s.opacity)
  // 取样区域比覆盖区域大一圈，边缘才模糊得自然
  const sx = Math.max(0, b.x - r * 3), sy = Math.max(0, b.y - r * 3)
  const sw = Math.min(w, b.x + b.w + r * 3) - sx, sh = Math.min(h, b.y + b.h + r * 3) - sy
  if (canvasBlurSupported()) {
    // 显卡算：模糊后的图 × 覆盖程度，原图挖掉同样的程度，两者相加 = 按覆盖程度混合
    const c = (blurCanvas ??= document.createElement('canvas'))
    if (c.width < b.w || c.height < b.h) { c.width = Math.max(c.width, b.w); c.height = Math.max(c.height, b.h) }
    const x = ctx2d(c)
    x.save()
    x.globalCompositeOperation = 'copy'
    x.filter = `blur(${r}px)`
    x.drawImage(target.canvas, sx, sy, sw, sh, sx - b.x, sy - b.y, sw, sh)
    x.filter = 'none'
    x.globalCompositeOperation = 'destination-in'
    x.drawImage(scratch.canvas, b.x, b.y, b.w, b.h, 0, 0, b.w, b.h)
    x.restore()
    target.save()
    target.globalAlpha = strength
    target.globalCompositeOperation = 'destination-out'
    target.drawImage(scratch.canvas, b.x, b.y, b.w, b.h, b.x, b.y, b.w, b.h)
    target.globalCompositeOperation = 'lighter'
    target.drawImage(c, 0, 0, b.w, b.h, b.x, b.y, b.w, b.h)
    target.restore()
    scratch.clearRect(b.x, b.y, b.w, b.h)
    return
  }
  const cov = scratch.getImageData(b.x, b.y, b.w, b.h).data
  scratch.clearRect(b.x, b.y, b.w, b.h)
  const src = target.getImageData(sx, sy, sw, sh)
  const orig = toPremul(src.data)
  const blurred = orig.slice()
  boxBlur(blurred, sw, sh, r)
  for (let y = 0; y < b.h; y++) {
    for (let x = 0; x < b.w; x++) {
      const c = (cov[(y * b.w + x) * 4 + 3]! / 255) * strength
      if (c <= 0) continue
      const q = ((y + b.y - sy) * sw + (x + b.x - sx)) * 4
      for (let k = 0; k < 4; k++) orig[q + k] = orig[q + k]! + (blurred[q + k]! - orig[q + k]!) * c
    }
  }
  fromPremul(orig, src.data)
  target.putImageData(src, sx, sy, b.x - sx, b.y - sy, b.w, b.h)
}

/** 涂抹笔刷 */
function drawSmudge(target: CanvasRenderingContext2D, s: StrokeData, scratch: CanvasRenderingContext2D, w: number, h: number) {
  const b = intersect(strokeBounds(s, w, h), hasClip(s) ? polyBounds(s.clip!, 2, w, h) : null)
  if (!b) return
  const mask = clipMask(s, scratch, b)
  const img = target.getImageData(b.x, b.y, b.w, b.h)
  const p = toPremul(img.data)
  smudge(p, b.w, b.h, b.x, b.y, s.points, s.size, clamp01(s.opacity), s.hardness, mask)
  fromPremul(p, img.data)
  target.putImageData(img, b.x, b.y)
}

/**
 * 把一笔画到目标上：先在草稿画布上用不透明的颜色画（印章或矢量路径），再按笔画不透明度合上去，
 * 这样同一笔里重叠的地方不会变深。橡皮用 destination-out 合成。草稿画布要和目标一样大。
 * 其他操作（填充、变换、渐变、贴图、模糊、涂抹）各有各的画法。
 */
export function renderStroke(target: CanvasRenderingContext2D, s: StrokeData, scratch: CanvasRenderingContext2D, width: number, height: number) {
  switch (s.tool) {
    case 'polyfill': return drawPolyfill(target, s, width, height)
    case 'gradient': return drawGradient(target, s, width, height)
    case 'xform': return drawXform(target, s, scratch, width, height)
    case 'patch': return drawPatch(target, s)
    case 'blur': return drawBlur(target, s, scratch, width, height)
    case 'smudge': return drawSmudge(target, s, scratch, width, height)
  }
  const b = strokeBounds(s, width, height)
  if (!b) return
  scratch.clearRect(b.x, b.y, b.w, b.h)
  scratch.save()
  scratch.beginPath(); scratch.rect(b.x, b.y, b.w, b.h); scratch.clip()
  applyClip(scratch, s)
  const style = s.tool === 'eraser' ? { ...s, color: '#000000' } : s
  if (style.brush === 'custom' && style.custom) customStampPath(scratch, style)
  else if (usesStamps(style)) stampPath(scratch, style)
  else {
    scratch.strokeStyle = scratch.fillStyle = style.color
    tracePath(scratch, style.points, p => widthOf(style, p))
  }
  scratch.restore()
  target.save()
  target.globalAlpha = s.opacity
  target.globalCompositeOperation = s.tool === 'eraser' ? 'destination-out' : 'source-over'
  target.drawImage(scratch.canvas, b.x, b.y, b.w, b.h, b.x, b.y, b.w, b.h)
  target.restore()
  scratch.clearRect(b.x, b.y, b.w, b.h)
}

export class PaintEngine {
  readonly width: number
  readonly height: number
  private stage: HTMLElement
  private layers = new Map<number, LayerState>()
  private order: number[] = []
  private strokes = new Map<number, Stroke>()
  private scratch: HTMLCanvasElement
  private scratchCtx: CanvasRenderingContext2D
  private live: HTMLCanvasElement
  private liveCtx: CanvasRenderingContext2D
  private remote = new Map<string, LiveStroke>()
  /** 自己的：正在画的（key = 'active'）和等确认的 */
  private mine = new Map<string, LiveStroke>()
  private frame = 0
  private dirtyLive = false
  private dirtyWork = new Set<number>()

  constructor(stage: HTMLElement, width: number, height: number) {
    this.stage = stage
    this.width = width
    this.height = height
    this.scratch = this.makeCanvas('paint-scratch')
    this.scratchCtx = ctx2d(this.scratch)
    this.live = this.makeCanvas('paint-live')
    this.liveCtx = ctx2d(this.live)
    stage.appendChild(this.live)
  }

  private makeCanvas(cls: string) {
    const c = document.createElement('canvas')
    c.width = this.width
    c.height = this.height
    c.className = cls
    return c
  }

  destroy() {
    cancelAnimationFrame(this.frame)
    this.layers.forEach(l => { l.canvas.remove(); l.work.remove() })
    this.live.remove()
    this.layers.clear(); this.strokes.clear(); this.remote.clear(); this.mine.clear()
  }

  // ================= 图层 =================

  /** 按服务器给的图层列表（下 → 上）同步：新建、删除、排序、不透明度 */
  setLayers(list: LayerMeta[]) {
    const ids = new Set(list.map(l => l.id))
    for (const [id, l] of this.layers) {
      if (ids.has(id)) continue
      l.canvas.remove(); l.work.remove()
      this.layers.delete(id)
      for (const [seq, s] of this.strokes) if (s.layerId === id) this.strokes.delete(seq)
    }
    for (const meta of list) {
      let l = this.layers.get(meta.id)
      if (!l) {
        const canvas = this.makeCanvas('paint-layer')
        const work = this.makeCanvas('paint-layer paint-work')
        work.style.display = 'none'
        l = { meta, canvas, ctx: ctx2d(canvas), work, workCtx: ctx2d(work), base: null, drawnSeq: 0, hidden: false }
        this.layers.set(meta.id, l)
      }
      l.meta = meta
      l.canvas.style.opacity = l.work.style.opacity = String(meta.opacity)
    }
    this.order = list.map(l => l.id)
    // 按顺序插到实时层下面
    for (const id of this.order) {
      const l = this.layers.get(id)!
      this.stage.insertBefore(l.canvas, this.live)
      this.stage.insertBefore(l.work, this.live)
    }
    this.syncVisibility()
  }

  layerIds() { return [...this.order] }
  hasLayer(id: number) { return this.layers.has(id) }

  /** 只影响自己看到的画面 */
  setHidden(id: number, hidden: boolean) {
    const l = this.layers.get(id)
    if (!l) return
    l.hidden = hidden
    this.syncVisibility()
  }
  isHidden(id: number) { return !!this.layers.get(id)?.hidden }

  private syncVisibility() {
    for (const l of this.layers.values()) {
      const working = this.hasMine(l.meta.id)
      l.canvas.style.display = l.hidden || working ? 'none' : ''
      l.work.style.display = !l.hidden && working ? '' : 'none'
    }
  }

  private hasMine(layerId: number) {
    for (const s of this.mine.values()) if (s.layerId === layerId) return true
    return false
  }

  setBase(layerId: number, image: CanvasImageSource | null) {
    const l = this.layers.get(layerId)
    if (!l) return
    l.base = image
    this.renderLayer(layerId)
  }

  // ================= 笔画 =================

  /** 进入画室时一次性载入（之后调用 renderAll） */
  loadStrokes(list: Stroke[]) {
    this.strokes.clear()
    for (const s of list) this.strokes.set(s.seq, normalizeStyle(s))
  }

  renderAll() { for (const id of this.order) this.renderLayer(id) }

  /** 有笔尖图载入完成：所有图层、实时层、工作画布都重画一遍 */
  refreshAll() {
    this.renderAll()
    for (const id of this.order) this.markWork(id)
    this.markLive()
  }

  /** 图层整张重画：底图 + 没撤销的笔画（按编号） */
  renderLayer(layerId: number) {
    const l = this.layers.get(layerId)
    if (!l) return
    l.ctx.globalCompositeOperation = 'source-over'
    l.ctx.globalAlpha = 1
    l.ctx.clearRect(0, 0, this.width, this.height)
    if (l.base) l.ctx.drawImage(l.base, 0, 0, this.width, this.height)
    let max = 0
    for (const s of this.layerStrokes(layerId)) {
      if (!s.undone) this.applyStroke(l.ctx, s)
      if (s.seq > max) max = s.seq
    }
    l.drawnSeq = max
    this.markWork(layerId)
  }

  private layerStrokes(layerId: number, upto = Infinity) {
    const list: Stroke[] = []
    for (const s of this.strokes.values()) if (s.layerId === layerId && s.seq <= upto) list.push(s)
    return list.sort((a, b) => a.seq - b.seq)
  }

  /** 服务器确认的一笔：编号比图层里画过的都大就直接叠加，否则整层重画 */
  commit(raw: Stroke) {
    const s = normalizeStyle(raw)
    this.strokes.set(s.seq, s)
    const l = this.layers.get(s.layerId)
    if (!l) return
    if (s.seq > l.drawnSeq) {
      if (!s.undone) this.applyStroke(l.ctx, s)
      l.drawnSeq = s.seq
      this.markWork(s.layerId)
    } else this.renderLayer(s.layerId)
  }

  setUndone(seq: number, undone: boolean) {
    const s = this.strokes.get(seq)
    if (!s || s.undone === undone) return
    s.undone = undone
    this.renderLayer(s.layerId)
  }

  clearLayer(layerId: number) {
    for (const [seq, s] of this.strokes) if (s.layerId === layerId) this.strokes.delete(seq)
    const l = this.layers.get(layerId)
    if (l) l.base = null
    this.renderLayer(layerId)
  }

  /** 编号 ≤ upto 的笔画用到的贴图是不是都载入了（没载入时合成出来的底图会缺东西） */
  canBake(layerId: number, upto: number) {
    return this.layerStrokes(layerId, upto).every(s => s.undone || patchReady(s))
  }

  /** 把编号 ≤ upto 的笔画合成新的底图，返回这张底图（用来上传）；有贴图还没载入时返回 null */
  bake(layerId: number, upto: number): HTMLCanvasElement | null {
    const l = this.layers.get(layerId)
    if (!l || !this.canBake(layerId, upto)) return null
    const c = this.makeCanvas('')
    const cx = ctx2d(c)
    if (l.base) cx.drawImage(l.base, 0, 0, this.width, this.height)
    for (const s of this.layerStrokes(layerId, upto)) if (!s.undone) this.applyStroke(cx, s)
    return c
  }

  /**
   * 本地也做同样的合并：底图换成合好的图，删掉合进去的笔画（画面不变）。
   * baked 是现成的底图（自己上传的那张，或者从服务器取回的）；没有时本地合成，合成不了返回 false。
   */
  applyBake(layerId: number, upto: number, baked?: CanvasImageSource | null): boolean {
    const c = baked ?? this.bake(layerId, upto)
    const l = this.layers.get(layerId)
    if (!c || !l) return false
    l.base = c
    for (const [seq, s] of this.strokes) if (s.layerId === layerId && seq <= upto) this.strokes.delete(seq)
    return true
  }

  private applyStroke(target: CanvasRenderingContext2D, s: StrokeData) {
    renderStroke(target, s, this.scratchCtx, this.width, this.height)
  }

  // ================= 别人的实时笔画 =================

  liveBegin(key: string, layerId: number, style: StrokeStyle, points: number[]) {
    this.remote.set(key, { ...normalizeStyle(style), layerId, points: [...points] })
    this.markLive()
  }
  livePoints(key: string, points: number[]) {
    const s = this.remote.get(key)
    if (!s) return
    for (const v of points) s.points.push(v)
    this.markLive()
  }
  liveEnd(key: string) {
    if (this.remote.delete(key)) this.markLive()
  }

  // ================= 自己的笔画 =================

  /** 开始 / 继续自己正在画的一笔（key 是客户端笔画编号） */
  mineBegin(key: string, layerId: number, style: StrokeStyle) {
    this.mine.set(key, { ...style, layerId, points: [] })
    this.syncVisibility()
    this.markWork(layerId)
  }
  mineAdd(key: string, points: number[]) {
    const s = this.mine.get(key)
    if (!s) return
    for (const v of points) s.points.push(v)
    this.markWork(s.layerId)
  }
  /** 改正在画的这一笔（形状、渐变、移动选区：拖动时整笔替换） */
  mineUpdate(key: string, patch: Partial<LiveStroke>) {
    const s = this.mine.get(key)
    if (!s) return
    Object.assign(s, patch)
    this.markWork(s.layerId)
  }
  /** 服务器确认（seq > 0）或作废（seq = 0） */
  mineDone(key: string, stroke: Stroke | null) {
    const s = this.mine.get(key)
    if (!s) return
    this.mine.delete(key)
    if (stroke) this.commit(stroke)
    this.syncVisibility()
    this.markWork(s.layerId)
  }

  // ================= 刷新 =================

  private markLive() { this.dirtyLive = true; this.schedule() }
  private markWork(layerId: number) { if (this.hasMine(layerId)) { this.dirtyWork.add(layerId); this.schedule() } }
  private schedule() { if (!this.frame) this.frame = requestAnimationFrame(() => this.flush()) }

  private flush() {
    this.frame = 0
    if (this.dirtyLive) {
      this.dirtyLive = false
      const c = this.liveCtx
      c.clearRect(0, 0, this.width, this.height)
      for (const s of this.remote.values()) {
        if (s.tool !== 'brush' && s.tool !== 'eraser' && s.tool !== 'blur' && s.tool !== 'smudge') continue // 一次性的操作，等确认后直接画进图层
        if (s.tool !== 'brush') {
          // 别人的橡皮、模糊、涂抹：画一条淡淡的虚线示意，抬笔后才真正生效
          c.save()
          c.strokeStyle = c.fillStyle = 'rgba(120,130,140,.45)'
          c.setLineDash([6, 6])
          tracePath(c, s.points, () => Math.max(2, s.size * 0.25))
          c.restore()
        } else this.applyStroke(c, s)
      }
    }
    for (const id of this.dirtyWork) {
      const l = this.layers.get(id)
      if (!l) continue
      l.workCtx.globalCompositeOperation = 'source-over'
      l.workCtx.globalAlpha = 1
      l.workCtx.clearRect(0, 0, this.width, this.height)
      l.workCtx.drawImage(l.canvas, 0, 0)
      for (const s of this.mine.values()) if (s.layerId === id) this.applyStroke(l.workCtx, s)
    }
    this.dirtyWork.clear()
  }

  // ================= 导出 / 取色 =================

  /** 合成看得见的图层（白底），用于导出、发到画廊、吸管 */
  compose(background = '#ffffff'): HTMLCanvasElement {
    const c = this.makeCanvas('')
    const cx = ctx2d(c)
    if (background) { cx.fillStyle = background; cx.fillRect(0, 0, this.width, this.height) }
    for (const id of this.order) {
      const l = this.layers.get(id)!
      if (l.hidden) continue
      cx.globalAlpha = l.meta.opacity
      cx.drawImage(l.canvas, 0, 0)
    }
    cx.globalAlpha = 1
    return c
  }

  /** 某个图层画布（只读：油漆桶「只看当前图层」时取样用） */
  layerCanvas(layerId: number): HTMLCanvasElement | null { return this.layers.get(layerId)?.canvas ?? null }

  pickColor(x: number, y: number): string | null {
    if (x < 0 || y < 0 || x >= this.width || y >= this.height) return null
    const d = this.compose().getContext('2d')!.getImageData(Math.floor(x), Math.floor(y), 1, 1).data
    return '#' + [d[0], d[1], d[2]].map(v => (v ?? 0).toString(16).padStart(2, '0')).join('')
  }

  /** 图层里还有多少笔（含撤销的） */
  strokeCount(layerId: number) { let n = 0; for (const s of this.strokes.values()) if (s.layerId === layerId) n++; return n }
}
