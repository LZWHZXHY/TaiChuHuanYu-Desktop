// pixelOps.ts：茶绘里需要逐像素计算的工具——模糊、涂抹、油漆桶，以及选区（多边形）的几个小工具。
// 都是纯 JS 计算：同样的输入在每个人的浏览器里算出同样的结果（多人同屏要求大家画面一致）。

// ================= 预乘透明度 =================

/** RGBA8 → Float32 预乘（颜色乘上透明度，模糊 / 混色时透明边缘不会发灰） */
export function toPremul(src: Uint8ClampedArray): Float32Array {
  const out = new Float32Array(src.length)
  for (let i = 0; i < src.length; i += 4) {
    const a = src[i + 3]! / 255
    out[i] = src[i]! * a; out[i + 1] = src[i + 1]! * a; out[i + 2] = src[i + 2]! * a; out[i + 3] = src[i + 3]!
  }
  return out
}

/** Float32 预乘 → RGBA8 */
export function fromPremul(p: Float32Array, out: Uint8ClampedArray) {
  for (let i = 0; i < p.length; i += 4) {
    const a = p[i + 3]!
    if (a < 0.5) { out[i] = out[i + 1] = out[i + 2] = out[i + 3] = 0; continue }
    const k = 255 / a
    out[i] = p[i]! * k; out[i + 1] = p[i + 1]! * k; out[i + 2] = p[i + 2]! * k; out[i + 3] = a
  }
}

// ================= 模糊 =================

function blurH(src: Float32Array, dst: Float32Array, w: number, h: number, r: number) {
  const norm = 1 / (2 * r + 1)
  for (let y = 0; y < h; y++) {
    const row = y * w
    for (let c = 0; c < 4; c++) {
      let acc = 0
      for (let k = -r; k <= r; k++) acc += src[(row + Math.min(w - 1, Math.max(0, k))) * 4 + c]!
      for (let x = 0; x < w; x++) {
        dst[(row + x) * 4 + c] = acc * norm
        acc += src[(row + Math.min(w - 1, x + r + 1)) * 4 + c]! - src[(row + Math.max(0, x - r)) * 4 + c]!
      }
    }
  }
}
function blurV(src: Float32Array, dst: Float32Array, w: number, h: number, r: number) {
  const norm = 1 / (2 * r + 1)
  for (let x = 0; x < w; x++) {
    for (let c = 0; c < 4; c++) {
      let acc = 0
      for (let k = -r; k <= r; k++) acc += src[(Math.min(h - 1, Math.max(0, k)) * w + x) * 4 + c]!
      for (let y = 0; y < h; y++) {
        dst[(y * w + x) * 4 + c] = acc * norm
        acc += src[(Math.min(h - 1, y + r + 1) * w + x) * 4 + c]! - src[(Math.max(0, y - r) * w + x) * 4 + c]!
      }
    }
  }
}

/** 三次盒式模糊（近似高斯），原地修改 */
export function boxBlur(p: Float32Array, w: number, h: number, r: number) {
  if (r < 1 || w < 1 || h < 1) return
  const tmp = new Float32Array(p.length)
  for (let i = 0; i < 3; i++) { blurH(p, tmp, w, h, r); blurV(tmp, p, w, h, r) }
}

// ================= 涂抹 =================

/**
 * 涂抹：沿路径按间距「盖章」，笔刷里带着上一处的颜色，盖下去和画布混合，再从画布沾一点颜色。
 * p 是区域（左上角 ox, oy，宽 rw）的预乘像素，原地修改；pts 是笔画点（x*10, y*10, 压感*100）。
 * strength 0~1：越大颜色被带得越远。clip 是区域内的选区蒙版（0~255），没有选区为 null。
 */
export function smudge(p: Float32Array, rw: number, rh: number, ox: number, oy: number, pts: number[], size: number, strength: number, hardness: number, clip: Uint8ClampedArray | null) {
  const n = pts.length / 3
  if (!n) return
  const r = Math.max(1, size / 2)
  const D = Math.ceil(size) + 2
  const buf = new Float32Array(D * D * 4)
  const fall = new Float32Array(D * D)
  const hard = Math.min(0.95, Math.max(0, hardness))
  const half = D / 2
  for (let j = 0; j < D; j++) for (let i = 0; i < D; i++) {
    const d = Math.hypot(i + 0.5 - half, j + 0.5 - half) / r
    if (d >= 1) continue
    const t = d <= hard ? 1 : (1 - d) / (1 - hard)
    fall[j * D + i] = t * t * (3 - 2 * t)
  }
  const pick = 0.06 + 0.9 * (1 - strength)
  let first = true
  const dab = (cx: number, cy: number) => {
    const tx = Math.round(cx - ox - half), ty = Math.round(cy - oy - half)
    for (let j = 0; j < D; j++) {
      const y = ty + j
      if (y < 0 || y >= rh) {
        if (first) for (let i = 0; i < D; i++) { const b = (j * D + i) * 4; buf[b] = buf[b + 1] = buf[b + 2] = buf[b + 3] = 0 }
        continue
      }
      for (let i = 0; i < D; i++) {
        const x = tx + i
        const b = (j * D + i) * 4
        if (x < 0 || x >= rw) { if (first) buf[b] = buf[b + 1] = buf[b + 2] = buf[b + 3] = 0; continue }
        const q = (y * rw + x) * 4
        if (first) { buf[b] = p[q]!; buf[b + 1] = p[q + 1]!; buf[b + 2] = p[q + 2]!; buf[b + 3] = p[q + 3]!; continue }
        let f = fall[j * D + i]!
        if (!f) continue
        if (clip) f *= clip[y * rw + x]! / 255
        if (!f) continue
        const w = f * strength
        for (let c = 0; c < 4; c++) {
          const v = p[q + c]! + (buf[b + c]! - p[q + c]!) * w
          p[q + c] = v
          buf[b + c] = buf[b + c]! + (v - buf[b + c]!) * pick * f
        }
      }
    }
    first = false
  }
  const spacing = Math.max(1, size * 0.08)
  let px = pts[0]! / 10, py = pts[1]! / 10
  dab(px, py)
  let carry = 0
  for (let k = 1; k < n; k++) {
    const x = pts[k * 3]! / 10, y = pts[k * 3 + 1]! / 10
    const dist = Math.hypot(x - px, y - py)
    let t = spacing - carry
    while (t <= dist) { dab(px + (x - px) * (t / dist), py + (y - py) * (t / dist)); t += spacing }
    carry = dist - (t - spacing)
    px = x; py = y
  }
}

// ================= 油漆桶 =================

export interface FloodResult { mask: Uint8Array; x: number; y: number; w: number; h: number }

/**
 * 从 (sx, sy) 开始找颜色相近的连通区域（4 邻接），再向外扩 expand 像素盖住线稿的抗锯齿边。
 * tolerance 0~255：和起点颜色每个通道的差都不超过它才算「相近」。返回整张图大小的蒙版和外接矩形。
 */
export function floodFill(data: Uint8ClampedArray, w: number, h: number, sx: number, sy: number, tolerance: number, expand: number): FloodResult | null {
  if (sx < 0 || sy < 0 || sx >= w || sy >= h) return null
  const s = (sy * w + sx) * 4
  const r0 = data[s]!, g0 = data[s + 1]!, b0 = data[s + 2]!, a0 = data[s + 3]!
  const match = (i: number) => {
    const q = i * 4
    const a = data[q + 3]!
    if (a0 === 0 && a === 0) return true
    if (Math.abs(a - a0) > tolerance) return false
    // 颜色差按两者中较不透明的那个折算：几乎透明的像素颜色不重要
    const k = Math.max(a, a0) / 255
    return Math.abs(data[q]! - r0) * k <= tolerance && Math.abs(data[q + 1]! - g0) * k <= tolerance && Math.abs(data[q + 2]! - b0) * k <= tolerance
  }
  let mask = new Uint8Array(w * h)
  let x0 = sx, x1 = sx, y0 = sy, y1 = sy
  const stack: number[] = [sx, sy]
  while (stack.length) {
    const y = stack.pop()!, xs = stack.pop()!
    let x = xs
    const row = y * w
    if (mask[row + x] || !match(row + x)) continue
    while (x > 0 && !mask[row + x - 1] && match(row + x - 1)) x--
    let up = false, down = false
    for (; x < w && !mask[row + x] && match(row + x); x++) {
      mask[row + x] = 1
      if (x < x0) x0 = x; if (x > x1) x1 = x
      if (y > 0) {
        const m = !mask[row - w + x] && match(row - w + x)
        if (m && !up) { stack.push(x, y - 1); up = true } else if (!m) up = false
      }
      if (y < h - 1) {
        const m = !mask[row + w + x] && match(row + w + x)
        if (m && !down) { stack.push(x, y + 1); down = true } else if (!m) down = false
      }
    }
    if (y < y0) y0 = y; if (y > y1) y1 = y
  }
  // 向外扩（8 邻接）
  for (let it = 0; it < expand; it++) {
    const next = mask.slice()
    const ny0 = Math.max(0, y0 - 1), ny1 = Math.min(h - 1, y1 + 1), nx0 = Math.max(0, x0 - 1), nx1 = Math.min(w - 1, x1 + 1)
    for (let y = ny0; y <= ny1; y++) for (let x = nx0; x <= nx1; x++) {
      const i = y * w + x
      if (mask[i]) continue
      if ((x > 0 && mask[i - 1]) || (x < w - 1 && mask[i + 1]) || (y > 0 && mask[i - w]) || (y < h - 1 && mask[i + w])
        || (x > 0 && y > 0 && mask[i - w - 1]) || (x < w - 1 && y > 0 && mask[i - w + 1]) || (x > 0 && y < h - 1 && mask[i + w - 1]) || (x < w - 1 && y < h - 1 && mask[i + w + 1])) next[i] = 1
    }
    mask = next
    x0 = nx0; x1 = nx1; y0 = ny0; y1 = ny1
  }
  return { mask, x: x0, y: y0, w: x1 - x0 + 1, h: y1 - y0 + 1 }
}

// ================= 选区（多边形，扁平数组 x, y, x, y, …，文档坐标） =================

export function pointInPolygon(poly: number[], x: number, y: number) {
  let inside = false
  const n = poly.length / 2
  for (let i = 0, j = n - 1; i < n; j = i++) {
    const xi = poly[i * 2]!, yi = poly[i * 2 + 1]!, xj = poly[j * 2]!, yj = poly[j * 2 + 1]!
    if ((yi > y) !== (yj > y) && x < ((xj - xi) * (y - yi)) / (yj - yi) + xi) inside = !inside
  }
  return inside
}

export function polygonBounds(poly: number[]) {
  let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity
  for (let i = 0; i < poly.length; i += 2) {
    const x = poly[i]!, y = poly[i + 1]!
    if (x < x0) x0 = x; if (x > x1) x1 = x
    if (y < y0) y0 = y; if (y > y1) y1 = y
  }
  return { x0, y0, x1, y1 }
}

/** 道格拉斯-普克简化：去掉几乎在一条直线上的点（套索一笔可能有上千个点） */
export function simplifyPolygon(poly: number[], epsilon: number): number[] {
  const n = poly.length / 2
  if (n <= 3) return poly
  const keep = new Uint8Array(n)
  keep[0] = keep[n - 1] = 1
  const stack: [number, number][] = [[0, n - 1]]
  while (stack.length) {
    const [a, b] = stack.pop()!
    const ax = poly[a * 2]!, ay = poly[a * 2 + 1]!, bx = poly[b * 2]!, by = poly[b * 2 + 1]!
    const len = Math.hypot(bx - ax, by - ay)
    let best = -1, bestD = epsilon
    for (let i = a + 1; i < b; i++) {
      // 首尾重合（套索圈回起点）时按到这个点的距离算
      const d = len < 1e-6
        ? Math.hypot(poly[i * 2]! - ax, poly[i * 2 + 1]! - ay)
        : Math.abs((bx - ax) * (ay - poly[i * 2 + 1]!) - (ax - poly[i * 2]!) * (by - ay)) / len
      if (d > bestD) { bestD = d; best = i }
    }
    if (best > 0) { keep[best] = 1; stack.push([a, best], [best, b]) }
  }
  const out: number[] = []
  for (let i = 0; i < n; i++) if (keep[i]) out.push(poly[i * 2]!, poly[i * 2 + 1]!)
  return out
}

/** 多边形面积（判断套索是不是只点了一下） */
export function polygonArea(poly: number[]) {
  let s = 0
  const n = poly.length / 2
  for (let i = 0, j = n - 1; i < n; j = i++) s += poly[j * 2]! * poly[i * 2 + 1]! - poly[i * 2]! * poly[j * 2 + 1]!
  return Math.abs(s / 2)
}
