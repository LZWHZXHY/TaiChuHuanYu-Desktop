// gifEncoder.ts：在浏览器里把一串画面编码成 GIF 动图（茶绘回放导出用，不依赖第三方库）。
// · 调色板：用中位切分从样本画面里挑 256 色（全局调色板，所有帧共用）；
// · 每一帧只编码和上一帧不同的矩形区域（回放是一笔笔往上加，变化区域通常很小），文件小、编码快；
// · 不做抖动，渐变多的画面会有一点色带，作为「体积小、方便分享」的格式够用。

/** 由样本像素（RGBA）生成最多 256 色的调色板，返回 [r,g,b, r,g,b, ...]，长度固定 256*3 */
export function buildPalette(samples: Uint8ClampedArray[], maxColors = 256): Uint8Array {
  // RGB555 直方图
  const hist = new Uint32Array(32768)
  for (const px of samples) {
    for (let i = 0; i < px.length; i += 4) hist[((px[i]! >> 3) << 10) | ((px[i + 1]! >> 3) << 5) | (px[i + 2]! >> 3)]!++
  }
  const colors: number[] = []
  for (let c = 0; c < 32768; c++) if (hist[c]) colors.push(c)
  type Box = { items: number[]; count: number }
  const comp = (c: number, axis: number) => axis === 0 ? c >> 10 : axis === 1 ? (c >> 5) & 31 : c & 31
  const boxes: Box[] = [{ items: colors, count: colors.reduce((s, c) => s + hist[c]!, 0) }]
  while (boxes.length < maxColors) {
    // 挑「颜色范围 × 像素数」最大的盒子切
    let best = -1, bestScore = 0, bestAxis = 0
    boxes.forEach((b, i) => {
      if (b.items.length < 2) return
      for (let axis = 0; axis < 3; axis++) {
        let lo = 31, hi = 0
        for (const c of b.items) { const v = comp(c, axis); if (v < lo) lo = v; if (v > hi) hi = v }
        const score = (hi - lo) * Math.sqrt(b.count)
        if (score > bestScore) { bestScore = score; best = i; bestAxis = axis }
      }
    })
    if (best < 0) break
    const box = boxes[best]!
    box.items.sort((a, b) => comp(a, bestAxis) - comp(b, bestAxis))
    let half = box.count / 2, acc = 0, cut = 1
    for (let i = 0; i < box.items.length - 1; i++) { acc += hist[box.items[i]!]!; if (acc >= half) { cut = i + 1; break } }
    const a = box.items.slice(0, cut), b = box.items.slice(cut)
    boxes.splice(best, 1,
      { items: a, count: a.reduce((s, c) => s + hist[c]!, 0) },
      { items: b, count: b.reduce((s, c) => s + hist[c]!, 0) })
  }
  const palette = new Uint8Array(256 * 3)
  boxes.forEach((b, i) => {
    let r = 0, g = 0, bl = 0, n = 0
    for (const c of b.items) { const w = hist[c]!; r += (c >> 10) * w; g += ((c >> 5) & 31) * w; bl += (c & 31) * w; n += w }
    n = Math.max(1, n)
    palette[i * 3] = Math.round((r / n) * 255 / 31)
    palette[i * 3 + 1] = Math.round((g / n) * 255 / 31)
    palette[i * 3 + 2] = Math.round((bl / n) * 255 / 31)
  })
  return palette
}

/** RGB555 → 最近的调色板下标（查表，32768 项） */
export function buildLookup(palette: Uint8Array): Uint8Array {
  const lut = new Uint8Array(32768)
  for (let c = 0; c < 32768; c++) {
    const r = ((c >> 10) << 3) | 4, g = (((c >> 5) & 31) << 3) | 4, b = ((c & 31) << 3) | 4
    let best = 0, bestD = Infinity
    for (let i = 0; i < 256; i++) {
      const dr = r - palette[i * 3]!, dg = g - palette[i * 3 + 1]!, db = b - palette[i * 3 + 2]!
      const d = dr * dr * 3 + dg * dg * 4 + db * db * 2
      if (d < bestD) { bestD = d; best = i; if (d === 0) break }
    }
    lut[c] = best
  }
  return lut
}

class ByteWriter {
  private chunks: Uint8Array[] = []
  private buf = new Uint8Array(1 << 16)
  private pos = 0
  byte(b: number) { if (this.pos === this.buf.length) this.flush(); this.buf[this.pos++] = b & 255 }
  word(w: number) { this.byte(w); this.byte(w >> 8) }
  bytes(arr: ArrayLike<number>) { for (let i = 0; i < arr.length; i++) this.byte(arr[i]!) }
  text(s: string) { for (let i = 0; i < s.length; i++) this.byte(s.charCodeAt(i)) }
  flush() { if (this.pos) { this.chunks.push(this.buf.slice(0, this.pos)); this.pos = 0 } }
  blob(type: string) { this.flush(); return new Blob(this.chunks as BlobPart[], { type }) }
}

/** 标准 GIF LZW 压缩，直接写成 255 字节一块的数据子块 */
function writeLzw(out: ByteWriter, pixels: Uint8Array, minCodeSize = 8) {
  const clear = 1 << minCodeSize, eoi = clear + 1
  let codeSize = minCodeSize + 1, next = eoi + 1
  const dict = new Map<number, number>()
  const block = new Uint8Array(255)
  let blockLen = 0, bitBuf = 0, bitCount = 0
  const emitByte = (b: number) => { block[blockLen++] = b; if (blockLen === 255) { out.byte(255); out.bytes(block); blockLen = 0 } }
  const emit = (code: number) => {
    bitBuf |= code << bitCount; bitCount += codeSize
    while (bitCount >= 8) { emitByte(bitBuf & 255); bitBuf >>>= 8; bitCount -= 8 }
  }
  out.byte(minCodeSize)
  emit(clear)
  let prefix = pixels[0]!
  for (let i = 1; i < pixels.length; i++) {
    const k = pixels[i]!
    const key = (prefix << 8) | k
    const found = dict.get(key)
    if (found !== undefined) { prefix = found; continue }
    emit(prefix)
    if (next < 4096) {
      dict.set(key, next++)
      if (next > (1 << codeSize) && codeSize < 12) codeSize++
    } else {
      emit(clear)
      dict.clear(); codeSize = minCodeSize + 1; next = eoi + 1
    }
    prefix = k
  }
  emit(prefix)
  emit(eoi)
  if (bitCount > 0) emitByte(bitBuf & 255)
  if (blockLen) { out.byte(blockLen); out.bytes(block.subarray(0, blockLen)) }
  out.byte(0)
}

export class GifWriter {
  private out = new ByteWriter()
  private prev: Uint8Array | null = null
  private lut: Uint8Array
  readonly frames: number[] = []

  constructor(private width: number, private height: number, palette: Uint8Array) {
    this.lut = buildLookup(palette)
    const o = this.out
    o.text('GIF89a')
    o.word(width); o.word(height)
    o.byte(0xf7) // 有全局调色板，256 色
    o.byte(0); o.byte(0)
    o.bytes(palette)
    // 循环播放
    o.byte(0x21); o.byte(0xff); o.byte(11); o.text('NETSCAPE2.0'); o.byte(3); o.byte(1); o.word(0); o.byte(0)
  }

  /** 加一帧（RGBA，尺寸和 GIF 一致），delayCs 是停留时间（百分之一秒） */
  addFrame(rgba: Uint8ClampedArray, delayCs: number) {
    const { width: w, height: h } = this
    const idx = new Uint8Array(w * h)
    const lut = this.lut
    for (let i = 0, p = 0; i < idx.length; i++, p += 4) idx[i] = lut[((rgba[p]! >> 3) << 10) | ((rgba[p + 1]! >> 3) << 5) | (rgba[p + 2]! >> 3)]!
    // 只编码变化的矩形
    let x0 = 0, y0 = 0, x1 = w - 1, y1 = h - 1
    if (this.prev) {
      x0 = w; y0 = h; x1 = -1; y1 = -1
      for (let y = 0; y < h; y++) {
        const row = y * w
        for (let x = 0; x < w; x++) {
          if (idx[row + x] !== this.prev[row + x]) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y }
        }
      }
      if (x1 < 0) { x0 = 0; y0 = 0; x1 = 0; y1 = 0 } // 没变化：写一个 1×1 的帧，只为了停留时间
    }
    const fw = x1 - x0 + 1, fh = y1 - y0 + 1
    const sub = new Uint8Array(fw * fh)
    for (let y = 0; y < fh; y++) sub.set(idx.subarray((y0 + y) * w + x0, (y0 + y) * w + x0 + fw), y * fw)
    const o = this.out
    o.byte(0x21); o.byte(0xf9); o.byte(4); o.byte(1 << 2); o.word(Math.max(2, Math.round(delayCs))); o.byte(0); o.byte(0) // 不清除，保留上一帧
    o.byte(0x2c); o.word(x0); o.word(y0); o.word(fw); o.word(fh); o.byte(0)
    writeLzw(o, sub)
    this.prev = idx
    this.frames.push(delayCs)
  }

  finish(): Blob {
    this.out.byte(0x3b)
    return this.out.blob('image/gif')
  }
}
