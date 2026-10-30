// particleLayouts.ts：粒子星图的「形状」和「阵型」。
// · 形状：一个条目的粒子团长什么样（按类型）；
// · 阵型：所有条目摆在哪里。除了几何阵型（星系、星球、螺旋、关系网），还有按世界设计自动组合的结构：
//   世界体系（派系 / 组织 / 地点是恒星，相关条目绕着它们转）、星座（按类型）、层级树、编年长河（按时间线），以及关系路径。
// 每个阵型返回条目位置，加上可选的「分组标题」和「参考线」（轨道、树枝、长河）。

export interface PWNode { id: string; title: string; type: string; color: string; cover?: string }
export interface PWLink { source: string; target: string; label: string; color: string; isEmbed: boolean }
export type Formation = 'system' | 'neural' | 'galaxy' | 'constellation' | 'sphere' | 'network' | 'tree' | 'timeline' | 'helix'
export interface TimelineMark { date: string; era: string }
export interface LayoutCtx {
  nodes: PWNode[]; links: PWLink[]
  degree: Map<string, number>; neighbors: Map<string, Set<string>>
  focusId: string
  timeline: Map<string, TimelineMark>
  typeLabel: (type: string) => string
  /** 能当「恒星」的类型（世界体系），默认派系、组织、地点 */
  containers?: Set<string>
  /** 「脉络」结构的各层（从源头到末端）；types 为空的一层收其余类型 */
  neuralLayers?: NeuralLayer[]
}
export interface NeuralLayer { label: string; types: string[]; /** 最后产出的一层（标成「输出」），比如知识库里可以下载的资源 */ output?: boolean }
export interface GroupLabel { text: string; sub?: string; pos: V3; color: string; /** 标题放在点的下方（默认上方） */ below?: boolean }
export interface Guide { a: V3; b: V3; color: V3; alpha: number }
/** 「脉络」的光脉冲：沿 segs 一层层往后传，经过的条目会闪一下 */
export interface Wave { segs: { a: V3; b: V3; layer: number }[]; nodeLayer: Map<string, number>; layers: number }
export interface LayoutResult { anchors: Map<string, V3>; groups: GroupLabel[]; guides: Guide[]; wave?: Wave }

export type V3 = [number, number, number]

// ================= 小工具 =================

/** 由字符串得到 0~1 的稳定随机数生成器（同一个条目每次布局一样） */
export function rng(seed: string | number) {
  let h = 2166136261
  const s = String(seed)
  for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 16777619) }
  let t = h >>> 0
  return () => { t += 0x6d2b79f5; let r = Math.imul(t ^ (t >>> 15), 1 | t); r ^= r + Math.imul(r ^ (r >>> 7), 61 | r); return ((r ^ (r >>> 14)) >>> 0) / 4294967296 }
}
export const gauss = (r: () => number) => { let u = 0; while (u === 0) u = r(); return Math.sqrt(-2 * Math.log(u)) * Math.cos(2 * Math.PI * r()) }
export function fib(i: number, n: number): V3 {
  const y = 1 - ((i + 0.5) / n) * 2, r = Math.sqrt(1 - y * y), a = i * 2.399963229728653
  return [Math.cos(a) * r, y, Math.sin(a) * r]
}
/** #rrggbb → 0~1（着色器直接输出，不做色彩空间转换） */
export const hexToRgb = (hex: string): V3 => {
  const n = parseInt((hex || '#64748b').replace('#', '').slice(0, 6), 16)
  return Number.isFinite(n) ? [((n >> 16) & 255) / 255, ((n >> 8) & 255) / 255, (n & 255) / 255] : [0.4, 0.45, 0.55]
}

// ================= 条目的粒子形状（单位半径内的偏移） =================

type ShapeFn = (r: () => number, k: number, n: number) => V3
export const SHAPES: Record<string, ShapeFn> = {
  /** 星球：外壳 + 内核 */
  sphere: (r, k, n) => {
    if (r() < 0.3) { const s = 0.38; return [gauss(r) * s, gauss(r) * s, gauss(r) * s] }
    const p = fib(k, n), j = 0.92 + r() * 0.1
    return [p[0] * j, p[1] * j, p[2] * j]
  },
  /** 行星 + 倾斜的光环 */
  planet: (r, k, n) => {
    if (r() < 0.55) { const p = fib(k, n); return [p[0] * 0.58, p[1] * 0.58, p[2] * 0.58] }
    const a = r() * Math.PI * 2, d = 1.05 + r() * 0.45, y = gauss(r) * 0.02
    const x = Math.cos(a) * d, z = Math.sin(a) * d
    const tilt = 0.45
    return [x, y * Math.cos(tilt) - z * Math.sin(tilt), y * Math.sin(tilt) + z * Math.cos(tilt)]
  },
  /** 八面体晶体（上下拉长） */
  crystal: r => {
    const v = [gauss(r), gauss(r), gauss(r)]
    const l1 = Math.abs(v[0]!) + Math.abs(v[1]!) + Math.abs(v[2]!) || 1
    const s = r() < 0.85 ? 1 : r()
    return [(v[0]! / l1) * s * 0.75, (v[1]! / l1) * s * 1.35, (v[2]! / l1) * s * 0.75]
  },
  /** 爆发：十几条射线 + 亮核 */
  burst: (r) => {
    if (r() < 0.25) { const s = 0.25; return [gauss(r) * s, gauss(r) * s, gauss(r) * s] }
    const ray = Math.floor(r() * 14), p = fib(ray, 14), d = 0.2 + Math.pow(r(), 0.7) * 1.15
    return [p[0] * d + gauss(r) * 0.03, p[1] * d + gauss(r) * 0.03, p[2] * d + gauss(r) * 0.03]
  },
  /** 立方体的棱 */
  cube: r => {
    const axis = Math.floor(r() * 3), a = r() < 0.5 ? -1 : 1, b = r() < 0.5 ? -1 : 1, t = r() * 2 - 1
    const s = 0.72, j = () => gauss(r) * 0.025
    if (axis === 0) return [t * s + j(), a * s + j(), b * s + j()]
    if (axis === 1) return [a * s + j(), t * s + j(), b * s + j()]
    return [a * s + j(), b * s + j(), t * s + j()]
  },
  /** 双螺旋 */
  helix: r => {
    const t = r() * 2 - 1, strand = r() < 0.5 ? 0 : Math.PI, a = t * Math.PI * 3 + strand
    if (r() < 0.12) { const k = r(); return [Math.cos(a) * 0.45 * (1 - 2 * k), t * 1.2, Math.sin(a) * 0.45 * (1 - 2 * k)] }
    return [Math.cos(a) * 0.45, t * 1.2, Math.sin(a) * 0.45]
  },
  /** 星云：几团高斯 */
  cloud: r => {
    const lobe = Math.floor(r() * 3), c = [[0.3, 0.1, 0], [-0.25, -0.15, 0.2], [0, 0.25, -0.25]][lobe]!
    return [c[0]! + gauss(r) * 0.32, c[1]! + gauss(r) * 0.32, c[2]! + gauss(r) * 0.32]
  },
  /** 圆环 */
  torus: r => {
    const a = r() * Math.PI * 2, b = r() * Math.PI * 2, R = 0.78, rr = 0.24
    return [(R + rr * Math.cos(b)) * Math.cos(a), rr * Math.sin(b), (R + rr * Math.cos(b)) * Math.sin(a)]
  },
  /** 旋涡盘 */
  disc: r => {
    const arm = Math.floor(r() * 3), d = Math.pow(r(), 0.6), a = arm * (Math.PI * 2 / 3) + d * 4 + gauss(r) * 0.25
    return [Math.cos(a) * d * 1.2, gauss(r) * 0.05, Math.sin(a) * d * 1.2]
  },
}
const TYPE_SHAPE: Record<string, string> = {
  character: 'sphere', location: 'planet', item: 'crystal', event: 'burst', skill: 'burst',
  faction: 'cube', organization: 'cube', species: 'helix', creature: 'cloud',
  occupation: 'torus', concept: 'torus', climate: 'disc',
}
export const shapeOfType = (type: string) => TYPE_SHAPE[type] ?? 'sphere'

// ================= 阵型 =================



function byTypeThenDegree(c: LayoutCtx) {
  const types = [...new Set(c.nodes.map(n => n.type))]
  return [...c.nodes].sort((a, b) => types.indexOf(a.type) - types.indexOf(b.type) || (c.degree.get(b.id) ?? 0) - (c.degree.get(a.id) ?? 0) || a.title.localeCompare(b.title))
}

/** 星系：几条对数螺旋旋臂，关系多的条目在核心附近，同类型的条目在旋臂上挨在一起 */
export function galaxyLayout(c: LayoutCtx): Map<string, V3> {
  const out = new Map<string, V3>()
  const n = c.nodes.length
  const types = [...new Set(c.nodes.map(x => x.type))]
  // 按关系数分几档，同一档里按类型排，这样旋臂上会出现一段一段的同色星团
  const tier = (id: string) => Math.floor(Math.log2((c.degree.get(id) ?? 0) + 1))
  const list = [...c.nodes].sort((a, b) => tier(b.id) - tier(a.id) || types.indexOf(a.type) - types.indexOf(b.type) || a.title.localeCompare(b.title))
  const arms = n < 12 ? 2 : n < 80 ? 3 : 4
  const rMax = 90 + 32 * Math.sqrt(n)
  const step = (rMax - 40) / Math.max(1, Math.ceil(n / arms))
  list.forEach((node, i) => {
    const r = rng(node.id)
    const arm = i % arms, k = Math.floor(i / arms)
    const d = 40 + k * step + (r() - 0.5) * step * 0.5
    const a = (arm / arms) * Math.PI * 2 + d * 0.011 + (r() - 0.5) * 0.18
    const h = gauss(r) * (4 + 26 * Math.exp(-d / 120))
    out.set(node.id, [Math.cos(a) * d, h, Math.sin(a) * d])
  })
  return out
}

/** 星球：节点按类型分区铺在球面上 */
export function sphereLayout(c: LayoutCtx): Map<string, V3> {
  const out = new Map<string, V3>()
  const list = byTypeThenDegree(c)
  const R = Math.max(70, Math.sqrt(list.length) * 22)
  list.forEach((n, i) => { const p = fib(i, list.length); out.set(n.id, [p[0] * R, p[1] * R, p[2] * R]) })
  return out
}

/** 螺旋：按类型排成一条双螺旋长卷 */
export function helixLayout(c: LayoutCtx): Map<string, V3> {
  const out = new Map<string, V3>()
  const list = byTypeThenDegree(c)
  const n = list.length
  const pitch = Math.max(5, 26 - n / 20)
  list.forEach((node, i) => {
    const a = i * 0.62 + (i % 2) * Math.PI
    const R = 70 + Math.min(80, n / 4)
    out.set(node.id, [Math.cos(a) * R, (i - n / 2) * pitch * 0.55, Math.sin(a) * R])
  })
  return out
}

/** 关系网：三维力导向（节点互相排斥，有关系的互相吸引） */
export function networkLayout(c: LayoutCtx): Map<string, V3> {
  const ids = c.nodes.map(n => n.id)
  const index = new Map(ids.map((id, i) => [id, i]))
  const n = ids.length
  const pos = new Float64Array(n * 3), vel = new Float64Array(n * 3)
  const start = galaxyLayout(c)
  ids.forEach((id, i) => { const p = start.get(id)!; pos[i * 3] = p[0] * 0.6; pos[i * 3 + 1] = p[1] + (rng(id + 'y')() - 0.5) * 80; pos[i * 3 + 2] = p[2] * 0.6 })
  const edges = c.links.map(l => [index.get(l.source), index.get(l.target)] as const).filter((e): e is readonly [number, number] => e[0] !== undefined && e[1] !== undefined && e[0] !== e[1])
  const iters = n > 900 ? 60 : n > 400 ? 120 : 220
  const cell = 70
  for (let it = 0; it < iters; it++) {
    const alpha = 1 - it / iters
    // 排斥：节点多时只算相邻格子
    if (n <= 400) {
      for (let i = 0; i < n; i++) for (let j = i + 1; j < n; j++) repel(i, j, alpha)
    } else {
      const grid = new Map<string, number[]>()
      for (let i = 0; i < n; i++) {
        const key = `${Math.floor(pos[i * 3]! / cell)},${Math.floor(pos[i * 3 + 1]! / cell)},${Math.floor(pos[i * 3 + 2]! / cell)}`
        let list = grid.get(key); if (!list) grid.set(key, list = []); list.push(i)
      }
      for (let i = 0; i < n; i++) {
        const gx = Math.floor(pos[i * 3]! / cell), gy = Math.floor(pos[i * 3 + 1]! / cell), gz = Math.floor(pos[i * 3 + 2]! / cell)
        for (let dx = -1; dx <= 1; dx++) for (let dy = -1; dy <= 1; dy++) for (let dz = -1; dz <= 1; dz++) {
          for (const j of grid.get(`${gx + dx},${gy + dy},${gz + dz}`) ?? []) if (j > i) repel(i, j, alpha)
        }
      }
    }
    for (const [a, b] of edges) {
      const dx = pos[b * 3]! - pos[a * 3]!, dy = pos[b * 3 + 1]! - pos[a * 3 + 1]!, dz = pos[b * 3 + 2]! - pos[a * 3 + 2]!
      const d = Math.hypot(dx, dy, dz) || 1, f = (d - 55) * 0.04 * alpha / d
      vel[a * 3] += dx * f; vel[a * 3 + 1] += dy * f; vel[a * 3 + 2] += dz * f
      vel[b * 3] -= dx * f; vel[b * 3 + 1] -= dy * f; vel[b * 3 + 2] -= dz * f
    }
    for (let i = 0; i < n * 3; i++) { vel[i] -= pos[i]! * 0.006 * alpha; vel[i] *= 0.62; pos[i] += vel[i]! }
  }
  function repel(i: number, j: number, alpha: number) {
    const dx = pos[j * 3]! - pos[i * 3]!, dy = pos[j * 3 + 1]! - pos[i * 3 + 1]!, dz = pos[j * 3 + 2]! - pos[i * 3 + 2]!
    const d2 = Math.max(25, dx * dx + dy * dy + dz * dz)
    if (d2 > 90000) return
    const f = (900 * alpha) / (d2 * Math.sqrt(d2))
    vel[i * 3] -= dx * f; vel[i * 3 + 1] -= dy * f; vel[i * 3 + 2] -= dz * f
    vel[j * 3] += dx * f; vel[j * 3 + 1] += dy * f; vel[j * 3 + 2] += dz * f
  }
  // 中心对齐
  let cx = 0, cy = 0, cz = 0
  for (let i = 0; i < n; i++) { cx += pos[i * 3]!; cy += pos[i * 3 + 1]!; cz += pos[i * 3 + 2]! }
  cx /= n || 1; cy /= n || 1; cz /= n || 1
  return new Map(ids.map((id, i) => [id, [pos[i * 3]! - cx, pos[i * 3 + 1]! - cy, pos[i * 3 + 2]! - cz] as V3]))
}

/** 聚焦：选中的条目在中心，直接相关的条目围成一圈星环 */
export function focusLayout(c: LayoutCtx): Map<string, V3> {
  const out = new Map<string, V3>()
  out.set(c.focusId, [0, 0, 0])
  const rest = byTypeThenDegree(c).filter(n => n.id !== c.focusId)
  const R = Math.max(140, Math.sqrt(rest.length) * 38)
  rest.forEach((n, i) => {
    if (rest.length <= 14) {
      // 少的时候排成一个略微起伏的圆环，更好看
      const a = (i / rest.length) * Math.PI * 2
      out.set(n.id, [Math.cos(a) * R, Math.sin(a * 3) * 14, Math.sin(a) * R])
    } else { const p = fib(i, rest.length); out.set(n.id, [p[0] * R, p[1] * R * 0.75, p[2] * R]) }
  })
  return out
}

// ================= 按世界设计组合的结构 =================

const plain = (anchors: Map<string, V3>): LayoutResult => ({ anchors, groups: [], guides: [] })
const rgbOf = (hex: string) => hexToRgb(hex)
/** 圆（在一个倾斜的平面上），用线段近似 */
function circleGuides(center: V3, r: number, tiltX: number, tiltZ: number, color: V3, alpha: number, segs = 64): Guide[] {
  const out: Guide[] = []
  let prev: V3 | null = null
  for (let i = 0; i <= segs; i++) {
    const p = tilt([Math.cos((i / segs) * Math.PI * 2) * r, 0, Math.sin((i / segs) * Math.PI * 2) * r], tiltX, tiltZ)
    const q: V3 = [p[0] + center[0], p[1] + center[1], p[2] + center[2]]
    if (prev) out.push({ a: prev, b: q, color, alpha })
    prev = q
  }
  return out
}
function tilt(p: V3, ax: number, az: number): V3 {
  // 先绕 x 轴再绕 z 轴转
  const y1 = p[1] * Math.cos(ax) - p[2] * Math.sin(ax), z1 = p[1] * Math.sin(ax) + p[2] * Math.cos(ax)
  const x2 = p[0] * Math.cos(az) - y1 * Math.sin(az), y2 = p[0] * Math.sin(az) + y1 * Math.cos(az)
  return [x2, y2, z1]
}

/** 能当「恒星」的类型：别的条目会绕着它们转 */
const CONTAINERS = new Set(['faction', 'organization', 'location'])

/**
 * 世界体系：派系、组织、地点是恒星；和它们有关系的条目在轨道上环绕（选关系最多的那颗恒星），
 * 小的派系 / 地点也可以绕着更大的恒星转，形成「恒星—行星—卫星」三层。没有归属的条目在最外面的游离星带上。
 */
export function systemLayout(c: LayoutCtx): LayoutResult {
  const deg = (id: string) => c.degree.get(id) ?? 0
  const byId = new Map(c.nodes.map(n => [n.id, n]))
  const C = c.containers ?? CONTAINERS
  const isC = (id: string) => C.has(byId.get(id)?.type ?? '')
  const containers = c.nodes.filter(n => C.has(n.type))
  if (!containers.length) {
    const g = galaxyLayout(c)
    return { anchors: g, groups: [{ text: '还没有派系、组织或地点', sub: '建几个这类条目并和角色连上关系，就会组成星系体系', pos: [0, 120, 0], color: '#94a3b8' }], guides: [] }
  }
  // 选父节点：关系最多的相邻恒星（恒星只认比自己大的恒星当父亲，避免绕圈）
  const bigger = (a: string, b: string) => deg(a) > deg(b) || (deg(a) === deg(b) && a < b)
  const parent = new Map<string, string>()
  for (const n of c.nodes) {
    let best = ''
    for (const m of c.neighbors.get(n.id) ?? []) {
      if (!isC(m) || m === n.id) continue
      if (isC(n.id) && !bigger(m, n.id)) continue
      if (!best || bigger(m, best)) best = m
    }
    if (best) parent.set(n.id, best)
  }
  // 最多三层：恒星的父亲如果自己也有父亲，就直接挂到最上面那颗
  for (const n of containers) {
    let p = parent.get(n.id), guard = 0
    while (p && parent.get(p) && guard++ < 20) {
      const pp = parent.get(p)!
      if (!parent.get(pp)) { parent.set(n.id, pp); break }
      p = pp
    }
  }
  for (const [id, p] of parent) if (!isC(id) && parent.get(p) && parent.get(parent.get(p)!)) parent.set(id, parent.get(p)!)
  const children = new Map<string, string[]>()
  for (const [id, p] of parent) { let l = children.get(p); if (!l) children.set(p, l = []); l.push(id) }
  const types = [...new Set(c.nodes.map(n => n.type))]
  for (const l of children.values()) l.sort((a, b) => Number(isC(b)) - Number(isC(a)) || types.indexOf(byId.get(a)!.type) - types.indexOf(byId.get(b)!.type) || deg(b) - deg(a) || a.localeCompare(b))

  // 自底向上算每个体系的半径，再自顶向下摆位置
  const size = new Map<string, number>()
  const rings = new Map<string, { r: number; ids: string[] }[]>()
  const measure = (id: string): number => {
    const hit = size.get(id)
    if (hit !== undefined) return hit
    const kids = children.get(id) ?? []
    if (!kids.length) { size.set(id, 0); return 0 }
    const list: { r: number; ids: string[] }[] = []
    let r = 34 + Math.sqrt(deg(id) + 1) * 4, cur: string[] = [], used = 0, maxS = 0
    for (const k of kids) {
      const s = measure(k), need = 2 * s + 26
      if (cur.length && used + need > Math.PI * 2 * r) {
        list.push({ r, ids: cur })
        r += maxS + 28 + s
        cur = []; used = 0; maxS = 0
      }
      cur.push(k); used += need; maxS = Math.max(maxS, s)
    }
    list.push({ r, ids: cur })
    rings.set(id, list)
    const total = r + maxS
    size.set(id, total)
    return total
  }
  const tops = c.nodes.filter(n => !parent.has(n.id) && (isC(n.id) || children.has(n.id))).map(n => n.id)
  tops.forEach(measure)
  tops.sort((a, b) => (size.get(b) ?? 0) - (size.get(a) ?? 0))

  const anchors = new Map<string, V3>()
  const guides: Guide[] = []
  const place = (id: string, center: V3, depth: number) => {
    anchors.set(id, center)
    const r = rng(id + ':sys')
    const tx = (r() - 0.5) * (depth ? 0.9 : 0.45), tz = (r() - 0.5) * (depth ? 0.9 : 0.45)
    const col = rgbOf(byId.get(id)!.color)
    for (const [ri, ring] of (rings.get(id) ?? []).entries()) {
      guides.push(...circleGuides(center, ring.r, tx, tz, col, depth ? 0.12 : 0.18, Math.min(96, 32 + Math.round(ring.r / 4))))
      const off = r() * Math.PI * 2 + ri * 0.7
      ring.ids.forEach((k, i) => {
        const a = off + (i / ring.ids.length) * Math.PI * 2
        const p = tilt([Math.cos(a) * ring.r, 0, Math.sin(a) * ring.r], tx, tz)
        place(k, [center[0] + p[0], center[1] + p[1], center[2] + p[2]], depth + 1)
      })
    }
  }
  // 顶层体系：向日葵螺旋排开
  const maxSize = Math.max(40, ...tops.map(t => size.get(t) ?? 0))
  let extent = 0
  tops.forEach((t, i) => {
    const d = i === 0 ? 0 : Math.sqrt(i) * (maxSize * 1.15 + 70)
    const a = i * 2.39996
    const center: V3 = [Math.cos(a) * d, (rng(t + 'h')() - 0.5) * 40, Math.sin(a) * d]
    place(t, center, 0)
    extent = Math.max(extent, d + (size.get(t) ?? 0))
  })
  // 游离星带
  const orphans = c.nodes.filter(n => !anchors.has(n.id))
  const groups: GroupLabel[] = []
  if (orphans.length) {
    const Rb = extent + 110 + orphans.length * 1.5
    orphans.forEach((n, i) => {
      const r = rng(n.id + ':belt')
      const a = (i / orphans.length) * Math.PI * 2 + (r() - 0.5) * 0.08
      const d = Rb + (r() - 0.5) * 50
      anchors.set(n.id, [Math.cos(a) * d, (r() - 0.5) * 24, Math.sin(a) * d])
    })
    guides.push(...circleGuides([0, 0, 0], Rb, 0, 0, [0.55, 0.62, 0.7], 0.08, 128))
    groups.push({ text: '游离星带', sub: `${orphans.length} 个还没归属派系 / 组织 / 地点的条目`, pos: [0, 40, -Rb], color: '#94a3b8' })
  }
  return { anchors, groups, guides }
}

/** 星座：每种类型聚成一个星座，星座内部用细线连成星图 */
export function constellationLayout(c: LayoutCtx): LayoutResult {
  const byType = new Map<string, PWNode[]>()
  for (const n of c.nodes) { let l = byType.get(n.type); if (!l) byType.set(n.type, l = []); l.push(n) }
  const types = [...byType.keys()].sort((a, b) => byType.get(b)!.length - byType.get(a)!.length)
  const T = types.length
  const anchors = new Map<string, V3>()
  const groups: GroupLabel[] = []
  const guides: Guide[] = []
  const radii = types.map(t => 26 + 24 * Math.cbrt(byType.get(t)!.length))
  const ringR = T <= 1 ? 0 : Math.max(...radii) * 1.2 + radii.reduce((a, b) => a + b, 0) * 2.2 / (Math.PI * 2) + 40
  types.forEach((type, ti) => {
    const list = byType.get(type)!.sort((a, b) => (c.degree.get(b.id) ?? 0) - (c.degree.get(a.id) ?? 0))
    const cr = radii[ti]!
    let center: V3
    if (T <= 8) { const a = (ti / Math.max(1, T)) * Math.PI * 2; center = [Math.cos(a) * ringR, Math.sin(a * 2) * 30, Math.sin(a) * ringR] }
    else { const p = fib(ti, T); const R = ringR * 0.8; center = [p[0] * R, p[1] * R * 0.6, p[2] * R] }
    const pts: V3[] = list.map((n, i) => {
      const r = rng(n.id + ':cst')
      if (i === 0) return center
      const p = fib(i, list.length), d = cr * (0.45 + 0.55 * Math.sqrt(r()))
      return [center[0] + p[0] * d, center[1] + p[1] * d * 0.7, center[2] + p[2] * d]
    })
    list.forEach((n, i) => anchors.set(n.id, pts[i]!))
    // 星座连线：每颗星连到离它最近的、已经连上的星（像星座图）
    if (list.length <= 400) {
      const col = rgbOf(list[0]!.color)
      for (let i = 1; i < pts.length; i++) {
        let best = 0, bd = Infinity
        for (let j = 0; j < i; j++) { const d = Math.hypot(pts[i]![0] - pts[j]![0], pts[i]![1] - pts[j]![1], pts[i]![2] - pts[j]![2]); if (d < bd) { bd = d; best = j } }
        guides.push({ a: pts[best]!, b: pts[i]!, color: col, alpha: 0.22 })
      }
    }
    groups.push({ text: c.typeLabel(type), sub: `${list.length} 个`, pos: [center[0], center[1] + cr + 26, center[2]], color: list[0]!.color })
  })
  return { anchors, groups, guides }
}

/** 层级树：从关系最多的条目开始一层层往下展开（像族谱 / 组织架构），不连通的部分各自成树 */
export function treeLayout(c: LayoutCtx): LayoutResult {
  const deg = (id: string) => c.degree.get(id) ?? 0
  const level = new Map<string, number>()
  const parent = new Map<string, string>()
  const order = [...c.nodes].sort((a, b) => deg(b.id) - deg(a.id) || a.title.localeCompare(b.title))
  const roots: string[] = []
  for (const n of order) {
    if (level.has(n.id)) continue
    roots.push(n.id)
    level.set(n.id, 0)
    const queue = [n.id]
    while (queue.length) {
      const id = queue.shift()!
      const kids = [...(c.neighbors.get(id) ?? [])].filter(k => !level.has(k)).sort((a, b) => deg(b) - deg(a))
      for (const k of kids) { level.set(k, level.get(id)! + 1); parent.set(k, id); queue.push(k) }
    }
  }
  const byLevel: string[][] = []
  for (const [id, l] of level) (byLevel[l] ??= []).push(id)
  const anchors = new Map<string, V3>()
  const angle = new Map<string, number>()
  const gap = 90
  byLevel.forEach((ids, L) => {
    if (L > 0) ids.sort((a, b) => (angle.get(parent.get(a)!) ?? 0) - (angle.get(parent.get(b)!) ?? 0) || deg(b) - deg(a))
    const R = L === 0 ? (ids.length > 1 ? Math.max(60, ids.length * 34 / Math.PI) : 0) : Math.max(70 + L * 70, (ids.length * 38) / (Math.PI * 2))
    ids.forEach((id, i) => {
      const a = L === 0 && ids.length === 1 ? 0 : ((i + 0.5) / ids.length) * Math.PI * 2 + L * 0.35
      angle.set(id, a)
      anchors.set(id, [Math.cos(a) * R, -L * gap + (byLevel.length - 1) * gap / 2, Math.sin(a) * R])
    })
  })
  const byId = new Map(c.nodes.map(n => [n.id, n]))
  const guides: Guide[] = []
  for (const [id, p] of parent) guides.push({ a: anchors.get(p)!, b: anchors.get(id)!, color: rgbOf(byId.get(p)!.color), alpha: 0.28 })
  const groups: GroupLabel[] = roots.length > 1 ? [{ text: `${roots.length} 棵树`, sub: '没有关系连起来的条目各自成树', pos: [0, (byLevel.length - 1) * gap / 2 + 70, 0], color: '#94a3b8' }] : []
  return { anchors, groups, guides }
}

/** 编年长河：有时间线事件的条目按时间顺序排在一条长河上，按纪元分段；没有时间的条目悬浮在上方 */
export function timelineLayout(c: LayoutCtx): LayoutResult {
  const dated = c.nodes.filter(n => c.timeline.has(n.id))
    .sort((a, b) => c.timeline.get(a.id)!.date.localeCompare(c.timeline.get(b.id)!.date, 'zh-CN', { numeric: true }) || a.title.localeCompare(b.title))
  const undated = c.nodes.filter(n => !c.timeline.has(n.id))
  const anchors = new Map<string, V3>()
  const groups: GroupLabel[] = []
  const guides: Guide[] = []
  const spacing = Math.max(46, Math.min(96, 2400 / Math.max(1, dated.length)))
  const eraGap = spacing * 1.6
  let x = 0, era: string | null = null
  const xs: number[] = []
  const river = (x: number): V3 => [x, Math.sin(x / 260) * 26, Math.sin(x / 170) * 70]
  for (const n of dated) {
    const e = c.timeline.get(n.id)!.era
    if (era !== null && e !== era) x += eraGap
    if (e !== era) { era = e; if (e) groups.push({ text: e, sub: '纪元', pos: [x, 0, 0], color: '#5eead4' }) }
    xs.push(x)
    x += spacing
  }
  const shift = dated.length ? -(xs[xs.length - 1]! / 2) : 0
  dated.forEach((n, i) => anchors.set(n.id, river(xs[i]! + shift)))
  for (const g of groups) { const p = river(g.pos[0] + shift); g.pos = [p[0], p[1] + 70, p[2]] }
  if (dated.length) {
    // 长河本身：比首尾各多出一段
    const x0 = xs[0]! + shift - spacing * 2, x1 = xs[xs.length - 1]! + shift + spacing * 2
    const steps = Math.max(24, Math.round((x1 - x0) / 14))
    for (let i = 0; i < steps; i++) {
      const a = river(x0 + ((x1 - x0) * i) / steps), b = river(x0 + ((x1 - x0) * (i + 1)) / steps)
      const fade = Math.sin((i / steps) * Math.PI)
      guides.push({ a, b, color: [0.37, 0.92, 0.83], alpha: 0.35 * fade + 0.05 })
    }
    // 纪元分界：竖线
    for (const g of groups) guides.push({ a: [g.pos[0], g.pos[1] - 120, g.pos[2]], b: [g.pos[0], g.pos[1] - 20, g.pos[2]], color: [0.37, 0.92, 0.83], alpha: 0.4 })
  }
  // 没有时间的条目：上方的一团云
  const span = Math.max(300, dated.length ? xs[xs.length - 1]! : 300)
  undated.forEach((n, i) => {
    const r = rng(n.id + ':tl')
    const p = fib(i, Math.max(1, undated.length))
    anchors.set(n.id, dated.length
      ? [p[0] * span * 0.45, 280 + p[1] * 55, p[2] * 110 - 230 + gauss(r) * 6]
      : [p[0] * span * 0.45, p[1] * 60, p[2] * 120])
  })
  if (undated.length) {
    groups.push(dated.length
      ? { text: '未标注时间', sub: `${undated.length} 个条目还没有时间线事件`, pos: [0, 370, -230], color: '#94a3b8' }
      : { text: '这个世界还没有时间线', sub: '在条目里添加「时间线事件」，它们就会按时间排进这条长河', pos: [0, 120, 0], color: '#94a3b8' })
  }
  return { anchors, groups, guides }
}

/** 关系路径：路径上的条目在中间排成一条，其余的退到后面 */
export function applyPath(base: Map<string, V3>, path: string[]): LayoutResult {
  const anchors = new Map<string, V3>()
  let cx = 0, cy = 0, cz = 0
  for (const p of base.values()) { cx += p[0]; cy += p[1]; cz += p[2] }
  const n = Math.max(1, base.size)
  cx /= n; cy /= n; cz /= n
  for (const [id, p] of base) anchors.set(id, [(p[0] - cx) * 1.7, (p[1] - cy) * 1.7, (p[2] - cz) * 1.7 - 260])
  const L = path.length, spacing = Math.max(90, 520 / Math.max(1, L - 1))
  path.forEach((id, i) => anchors.set(id, [(i - (L - 1) / 2) * spacing, Math.sin((i / Math.max(1, L - 1)) * Math.PI) * 30, 80]))
  return { anchors, groups: [], guides: [] }
}

/**
 * 脉络：世界基石（设定、气候、物种）→ 舞台（地点、派系、组织）→ 角色 → 能力与器物 → 事件，
 * 一层层排开：每层是一面条目方阵，层与层之间有细密的连接，光脉冲顺着层层往下流。
 */
export const NN_LAYERS: NeuralLayer[] = [
  { label: '世界基石', types: ['concept', 'climate', 'species'] },
  { label: '舞台', types: ['location', 'faction', 'organization'] },
  { label: '角色', types: ['character', 'creature', 'occupation'] },
  { label: '能力与器物', types: ['skill', 'item'] },
  { label: '其他设定', types: [] },
  { label: '故事与事件', types: ['event', 'novel'], output: true },
]
export function neuralLayout(c: LayoutCtx): LayoutResult {
  const defs = c.neuralLayers ?? NN_LAYERS
  const known = new Set(defs.flatMap(l => l.types))
  const deg = (id: string) => c.degree.get(id) ?? 0
  const layers = defs.map(l => ({ label: l.label, nodes: c.nodes.filter(n => l.types.length ? l.types.includes(n.type) : !known.has(n.type)) }))
    .filter(l => l.nodes.length)
  const L = layers.length
  const maxCount = Math.max(1, ...layers.map(l => l.nodes.length))
  const cell = Math.max(48, Math.min(76, 560 / Math.sqrt(maxCount)))
  const gap = Math.max(250, Math.sqrt(maxCount) * cell * 0.72)
  const anchors = new Map<string, V3>()
  const groups: GroupLabel[] = []
  const guides: Guide[] = []
  const nodeLayer = new Map<string, number>()
  const frame: V3 = [0.37, 0.92, 0.83]
  const planes: V3[][] = []
  layers.forEach((layer, li) => {
    const m = layer.nodes.length
    const cols = Math.max(1, Math.ceil(Math.sqrt(m * 1.25))), rows = Math.ceil(m / cols)
    // 方阵格子按离中心的距离排序，关系多的条目放在中间
    const cells: [number, number][] = []
    for (let r = 0; r < rows; r++) for (let q = 0; q < cols; q++) cells.push([(r - (rows - 1) / 2) * cell, (q - (cols - 1) / 2) * cell])
    cells.sort((a, b) => Math.hypot(a[0], a[1] * 0.8) - Math.hypot(b[0], b[1] * 0.8))
    const x = (li - (L - 1) / 2) * gap
    const list = [...layer.nodes].sort((a, b) => deg(b.id) - deg(a.id) || a.title.localeCompare(b.title))
    const pts: V3[] = []
    list.forEach((n, i) => {
      const [y, z] = cells[i]!
      const p: V3 = [x, -y, z]
      anchors.set(n.id, p)
      nodeLayer.set(n.id, li)
      pts.push(p)
    })
    planes.push(pts)
    // 层的边框（四角折线）
    const h = (rows - 1) / 2 * cell + cell * 0.8, w = (cols - 1) / 2 * cell + cell * 0.8, k = Math.min(h, w) * 0.35
    for (const [sy, sz] of [[1, 1], [1, -1], [-1, 1], [-1, -1]] as const) {
      guides.push({ a: [x, sy * h, sz * w], b: [x, sy * (h - k), sz * w], color: frame, alpha: 0.5 })
      guides.push({ a: [x, sy * h, sz * w], b: [x, sy * h, sz * (w - k)], color: frame, alpha: 0.5 })
    }
    for (const [a, b] of [[[x, h, w], [x, h, -w]], [[x, -h, w], [x, -h, -w]], [[x, h, w], [x, -h, w]], [[x, h, -w], [x, -h, -w]]] as [V3, V3][]) guides.push({ a, b, color: frame, alpha: 0.08 })
    const def = defs.find(d => d.label === layer.label)
    const name = def?.output ? `输出 · ${layer.label}` : `${String(li + 1).padStart(2, '0')} · ${layer.label}`
    // 标题上下交错，层多的时候不会挤在一起
    groups.push({ text: name, sub: `${m} 个条目`, pos: [x, li % 2 ? -h - 30 : h + 46, 0], below: li % 2 === 1, color: li === 0 ? '#5eead4' : li === L - 1 ? '#f472b6' : '#93c5fd' })
  })
  // 层间连接：每个条目连到下一层离它最近的几个（全部互连太密，看不清）
  const segs: Wave['segs'] = []
  const k = maxCount > 200 ? 2 : 3
  for (let li = 0; li + 1 < planes.length; li++) {
    const next = planes[li + 1]!
    for (const p of planes[li]!) {
      const near = next.map(q => ({ q, d: Math.hypot(q[1] - p[1], q[2] - p[2]) })).sort((a, b) => a.d - b.d).slice(0, k)
      for (const { q } of near) { guides.push({ a: p, b: q, color: [0.55, 0.75, 1], alpha: 0.07 }); segs.push({ a: p, b: q, layer: li }) }
    }
    // 下一层没被连到的条目，也从上一层最近的连一条
    for (const q of next) {
      if (segs.some(s => s.b === q)) continue
      const best = planes[li]!.map(p => ({ p, d: Math.hypot(q[1] - p[1], q[2] - p[2]) })).sort((a, b) => a.d - b.d)[0]
      if (best) { guides.push({ a: best.p, b: q, color: [0.55, 0.75, 1], alpha: 0.07 }); segs.push({ a: best.p, b: q, layer: li }) }
    }
  }
  return { anchors, groups, guides, wave: { segs, nodeLayer, layers: L } }
}

/** 科幻感的底盘：布局下方一圈淡淡的极坐标网格 */
export function hudGuides(anchors: Map<string, V3>): Guide[] {
  if (!anchors.size) return []
  let minY = Infinity, cx = 0, cz = 0, R = 40
  for (const p of anchors.values()) { minY = Math.min(minY, p[1]); cx += p[0]; cz += p[2] }
  cx /= anchors.size; cz /= anchors.size
  for (const p of anchors.values()) R = Math.max(R, Math.hypot(p[0] - cx, p[2] - cz))
  const y = minY - 50, col: V3 = [0.3, 0.85, 0.8]
  const out: Guide[] = []
  for (const [k, a] of [[0.4, 0.05], [0.75, 0.06], [1.08, 0.09]] as const) out.push(...circleGuides([cx, y, cz], R * k, 0, 0, col, a, 96))
  for (let i = 0; i < 48; i++) {
    const t = (i / 48) * Math.PI * 2, r0 = R * 1.08, r1 = R * (i % 4 === 0 ? 1.16 : 1.12)
    out.push({ a: [cx + Math.cos(t) * r0, y, cz + Math.sin(t) * r0], b: [cx + Math.cos(t) * r1, y, cz + Math.sin(t) * r1], color: col, alpha: i % 4 === 0 ? 0.16 : 0.08 })
  }
  for (let i = 0; i < 6; i++) {
    const t = (i / 6) * Math.PI * 2
    out.push({ a: [cx + Math.cos(t) * R * 0.4, y, cz + Math.sin(t) * R * 0.4], b: [cx + Math.cos(t) * R * 1.08, y, cz + Math.sin(t) * R * 1.08], color: col, alpha: 0.035 })
  }
  return out
}

export function layoutFor(f: Formation, c: LayoutCtx): LayoutResult {
  if (c.focusId) return plain(focusLayout(c))
  const withHud = (r: LayoutResult) => ({ ...r, guides: [...r.guides, ...hudGuides(r.anchors)] })
  switch (f) {
    case 'neural': return neuralLayout(c)
    case 'system': return withHud(systemLayout(c))
    case 'constellation': return withHud(constellationLayout(c))
    case 'tree': return treeLayout(c)
    case 'timeline': return timelineLayout(c)
    case 'galaxy': return withHud(plain(galaxyLayout(c)))
    case 'sphere': return plain(sphereLayout(c))
    case 'network': return withHud(plain(networkLayout(c)))
    case 'helix': return plain(helixLayout(c))
  }
}

/** 每种阵型最好看的观察方向 */
export const VIEW_DIR: Record<Formation | 'focus' | 'path', V3> = {
  system: [0.3, 0.72, 0.62], neural: [0.5, 0.26, 0.83], galaxy: [0.3, 0.78, 0.55], constellation: [0.45, 0.42, 0.78], sphere: [0.5, 0.35, 0.79],
  network: [0.55, 0.4, 0.73], tree: [0.55, 0.35, 0.76], timeline: [0.08, 0.3, 0.95], helix: [0.9, 0.12, 0.42],
  focus: [0.45, 0.5, 0.74], path: [0, 0.18, 1],
}
