<template>
  <div class="sim">
    <header class="sim-header">
      <div class="title-block">
        <h1>渗流模型</h1>
        <p class="sub">
          在 L×L 格子上随机点亮 p% 的格子，看是否有连通路径从顶行到底行。
        </p>
        <p class="sub sub-2">
          理论临界点 p<sub>c</sub> ≈ <b>0.5927</b>（仅当 L→∞ 时精确）。
        </p>
      </div>
      <div class="status-badge" :class="statusClass">
        <span class="status-dot"></span>
        {{ statusText }}
      </div>
    </header>

    <!-- 参数面板 -->
    <section class="control-panel">
      <div class="control-group">
        <span class="group-label">参数</span>
        <label>
          边长
          <input type="number" v-model.number="L" min="2" max="2000" />
        </label>
        <label>
          概率
          <input type="number" v-model.number="p" min="0" max="100" step="1" />
          <span class="unit">%</span>
        </label>
        <label>
          种子
          <input type="number" v-model.number="seed" placeholder="留空" class="input-seed" />
        </label>
        <button @click="rollSeed" class="btn-icon" title="随机种子">🎲</button>
      </div>

      <div class="control-group">
        <span class="group-label">单点</span>
        <button @click="startSingle" :disabled="busy">看一次</button>
        <label>
          重复
          <select v-model.number="trials">
            <option :value="100">100</option>
            <option :value="500">500</option>
            <option :value="1000">1000</option>
            <option :value="5000">5000</option>
          </select>
        </label>
        <button @click="startBatch" :disabled="busy" class="btn-secondary">跑统计</button>
      </div>

      <div class="control-group">
        <span class="group-label">扫描</span>
        <label>
          <input type="number" v-model.number="pMin" min="0" max="100" class="input-p" />
          <span class="unit">%</span>
          <span class="tilde">~</span>
          <input type="number" v-model.number="pMax" min="0" max="100" class="input-p" />
          <span class="unit">%</span>
        </label>
        <button @click="startScan" :disabled="busy" class="btn-secondary">
          + 扫描一条（L={{ L }}）
        </button>
        <button @click="clearAllSeries" :disabled="busy || scanSeries.length === 0" class="btn-icon" title="清空所有曲线">✕</button>
      </div>
    </section>

    <!-- 主视图 -->
    <div class="main-view">
      <div class="canvas-card">
        <canvas ref="canvasRef"></canvas>
        <div class="canvas-caption">
          <span>L = {{ L }}</span>
          <span class="sep">·</span>
          <span>p = {{ p }}%</span>
          <template v-if="lastEntry">
            <span class="sep">·</span>
            <span>seed = {{ lastEntry.seed }}</span>
          </template>
        </div>
      </div>

      <aside class="side-panel">
        <div class="card stats-card">
          <h3>当前状态</h3>
          <div class="stat-row">
            <span>最近贯通率</span>
            <b>{{ lastEntry ? (lastEntry.rate * 100).toFixed(1) + '%' : '—' }}</b>
          </div>
          <div class="stat-row">
            <span>理论临界点</span>
            <b>59.27%</b>
          </div>
          <div class="stat-row">
            <span>距离 p<sub>c</sub></span>
            <b :class="pcDistanceClass">{{ pcDistanceText }}</b>
          </div>
        </div>

        <div class="card history-card" v-if="history.length > 0">
          <div class="card-head">
            <h3>历史记录</h3>
            <button class="btn-clear" @click="history = []" :disabled="busy">清空</button>
          </div>
          <div class="history-scroll">
            <table class="history-table">
              <thead>
                <tr>
                  <th>#</th>
                  <th>p</th>
                  <th>n</th>
                  <th>贯通率</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(h, i) in history" :key="h.id">
                  <td class="dim">{{ i + 1 }}</td>
                  <td class="mono">{{ h.p }}%</td>
                  <td class="dim mono">{{ h.trials }}</td>
                  <td>
                    <div class="rate-cell">
                      <div class="mini-bar">
                        <div class="mini-fill" :style="{ width: (h.rate * 100) + '%' }"></div>
                      </div>
                      <span class="rate-num">{{ (h.rate * 100).toFixed(0) }}%</span>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </aside>
    </div>

    <!-- 曲线卡片 -->
    <div class="card curve-card">
      <div class="card-head">
        <h3>贯通率 vs 概率</h3>
        <div class="curve-legend">
          <button
            v-for="s in scanSeries"
            :key="s.id"
            class="legend-chip"
            @click="removeSeries(s.id)"
            :disabled="busy"
            :title="'点击移除这条曲线'"
          >
            <span class="chip-dot" :style="{ background: s.color }"></span>
            L={{ s.L }}
            <span class="chip-x">×</span>
          </button>
          <span v-if="scanSeries.length === 0 && history.length === 0" class="legend-empty">
            还没有数据 —— 点「扫描一条」或「跑统计」开始
          </span>
          <template v-if="history.length > 0">
            <span class="dot dot-history"></span>
            <span class="legend-label">手动点</span>
          </template>
        </div>
      </div>
      <canvas ref="curveRef"></canvas>
    </div>

    <!-- 底部说明 -->
    <section class="card explain-card">
      <h3>怎么读这张图</h3>
      <ul class="explain-list">
        <li>
          <b>反复扫描不同 L 的曲线，把它们叠在一起。</b>
          你会看到所有曲线都在 <b>p ≈ 59.27%</b> 附近穿过 50%。
          这就是二维座渗流的临界点。
        </li>
        <li>
          <b>L 越大，曲线越陡。</b>
          小系统的"临界"是模糊的过渡带，大系统的"临界"是几乎垂直的跃变。
          理论上，只有当 L → ∞ 时，临界点才真正精确。
        </li>
        <li>
          <b>试试 L=20、L=100、L=500 各扫一条，叠起来看。</b>
          陡峭程度的差异，就是"有限尺寸标度"最直观的体现。
        </li>
      </ul>
    </section>

    <div v-if="resultText" class="result">{{ resultText }}</div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch, nextTick } from 'vue'

const L = ref(200)
const p = ref(58)
const seed = ref<number | string | null>(null)
const pMin = ref(50)
const pMax = ref(68)
const trials = ref(500)

const canvasRef = ref<HTMLCanvasElement | null>(null)
const curveRef = ref<HTMLCanvasElement | null>(null)
const running = ref(false)
const scanning = ref(false)
const resultText = ref('')

interface HistoryEntry {
  id: number
  L: number
  p: number
  trials: number
  passed: number
  rate: number
  seed: number
}
const history = ref<HistoryEntry[]>([])
let historyIdCounter = 0

interface CurvePoint { p: number; rate: number }
interface CurveSeries {
  id: number
  L: number
  color: string
  points: CurvePoint[]
}
const scanSeries = ref<CurveSeries[]>([])
let seriesIdCounter = 0

// ★ 深色背景友好的数据色板（色相拉开，明度接近）
const SERIES_COLORS = [
  '#ff9e2c',  // 橙
  '#56b4e9',  // 天蓝
  '#4dd07a',  // 绿
  '#e37fb8',  // 粉紫
  '#d4b400',  // 黄
  '#b184ff',  // 紫
  '#ff6b6b',  // 红
  '#4adcd8',  // 青
]

const FRAME_DELAY = 60
const TARGET_FRAMES = 40
const P_C = 0.5927

// ============ 状态派生 ============
const busy = computed(() => running.value || scanning.value)

const statusText = computed(() => {
  if (running.value) return '运行中'
  if (scanning.value) return '扫描中'
  return '就绪'
})

const statusClass = computed(() => {
  if (running.value || scanning.value) return 'running'
  return 'ready'
})

const lastEntry = computed(() => 
  history.value.length > 0 ? history.value[history.value.length - 1] : null
)

const pcDistanceText = computed(() => {
  if (!lastEntry.value) return '—'
  const diff = lastEntry.value.p - P_C * 100
  const sign = diff >= 0 ? '+' : ''
  return `${sign}${diff.toFixed(2)}%`
})

const pcDistanceClass = computed(() => {
  if (!lastEntry.value) return ''
  const diff = Math.abs(lastEntry.value.p - P_C * 100)
  if (diff < 1) return 'near'
  if (diff < 3) return 'mid'
  return 'far'
})

// ============ 颜色 ============
const COLOR_BG: [number, number, number]      = [22, 27, 34]
const COLOR_LIT: [number, number, number]     = [90, 55, 15]
const COLOR_BURNING: [number, number, number] = [255, 165, 40]
const COLOR_GRID = 'rgba(255, 255, 255, 0.06)'
const COLOR_CURVE_BG = '#161b22'
const COLOR_CURVE_AXIS = '#30363d'
const COLOR_CURVE_TEXT = '#8b949e'
const COLOR_CURVE_PC = '#cf222e'
const COLOR_HISTORY_DOT = '#ffffff'

let isLit: Uint8Array = new Uint8Array(0)
let isBurning: Uint8Array = new Uint8Array(0)
let animTimer: number | null = null

// ============ 工具 ============
function resolveSeed(): number {
  const v = seed.value
  if (v === '' || v === null || v === undefined || Number.isNaN(Number(v))) {
    return Math.floor(Math.random() * 1000000)
  }
  return Number(v)
}

function rollSeed() {
  seed.value = Math.floor(Math.random() * 1000000)
}

function createRng(seedValue: number) {
  let s = seedValue >>> 0
  return function () {
    s = (s + 0x6d2b79f5) | 0
    let t = Math.imul(s ^ (s >>> 15), 1 | s)
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

function generateLit(size: number, prob: number, rng: () => number): Uint8Array {
  const N = size * size
  const grid = new Uint8Array(N)
  for (let i = 0; i < N; i++) {
    grid[i] = rng() < prob ? 1 : 0
  }
  return grid
}

function makeEmpty(size: number): Uint8Array {
  return new Uint8Array(size * size)
}

function percolates(lit: Uint8Array, size: number): boolean {
  const N = size * size
  const visited = new Uint8Array(N)
  const queue = new Int32Array(N)
  let head = 0
  let tail = 0

  for (let col = 0; col < size; col++) {
    if (lit[col]) {
      visited[col] = 1
      queue[tail++] = col
    }
  }

  const lastRowStart = (size - 1) * size

  while (head < tail) {
    const idx = queue[head++]
    if (idx >= lastRowStart) return true

    const r = (idx / size) | 0
    const c = idx - r * size

    if (r < size - 1) {
      const nidx = idx + size
      if (lit[nidx] && !visited[nidx]) { visited[nidx] = 1; queue[tail++] = nidx }
    }
    if (r > 0) {
      const nidx = idx - size
      if (lit[nidx] && !visited[nidx]) { visited[nidx] = 1; queue[tail++] = nidx }
    }
    if (c < size - 1) {
      const nidx = idx + 1
      if (lit[nidx] && !visited[nidx]) { visited[nidx] = 1; queue[tail++] = nidx }
    }
    if (c > 0) {
      const nidx = idx - 1
      if (lit[nidx] && !visited[nidx]) { visited[nidx] = 1; queue[tail++] = nidx }
    }
  }
  return false
}

// ============ 格子绘制 ============
function drawFrame() {
  const canvas = canvasRef.value
  if (!canvas) return
  const ctx = canvas.getContext('2d')
  if (!ctx) return

  const dpr = window.devicePixelRatio || 1
  const displaySize = canvas.clientWidth || 400
  const size = L.value
  const canvasSize = Math.max(Math.round(displaySize * dpr), size)

  if (canvas.width !== canvasSize) canvas.width = canvasSize
  if (canvas.height !== canvasSize) canvas.height = canvasSize

  const cellPixels = canvasSize / size
  const imageData = ctx.createImageData(canvasSize, canvasSize)
  const data = imageData.data

  for (let row = 0; row < size; row++) {
    const rowOffsetIdx = row * size
    const y0 = Math.round(row * cellPixels)
    const y1 = Math.round((row + 1) * cellPixels)

    for (let col = 0; col < size; col++) {
      const idx = rowOffsetIdx + col
      let rgb: [number, number, number]
      if (isBurning[idx]) rgb = COLOR_BURNING
      else if (isLit[idx]) rgb = COLOR_LIT
      else rgb = COLOR_BG

      const x0 = Math.round(col * cellPixels)
      const x1 = Math.round((col + 1) * cellPixels)

      for (let y = y0; y < y1; y++) {
        const pixelRowOffset = y * canvasSize * 4
        for (let x = x0; x < x1; x++) {
          const pidx = pixelRowOffset + x * 4
          data[pidx] = rgb[0]
          data[pidx + 1] = rgb[1]
          data[pidx + 2] = rgb[2]
          data[pidx + 3] = 255
        }
      }
    }
  }

  ctx.putImageData(imageData, 0, 0)

  if (cellPixels >= 6) {
    ctx.strokeStyle = COLOR_GRID
    ctx.lineWidth = Math.max(1, Math.floor(dpr * 0.5))
    for (let row = 0; row < size; row++) {
      for (let col = 0; col < size; col++) {
        ctx.strokeRect(col * cellPixels, row * cellPixels, cellPixels, cellPixels)
      }
    }
  }
}

// ============ 单次动画 ============
function stopAnim() {
  if (animTimer !== null) {
    clearTimeout(animTimer)
    animTimer = null
  }
}

function startSingle() {
  if (busy.value) return
  stopAnim()

  running.value = true
  resultText.value = ''

  L.value = Math.min(2000, Math.max(2, Math.round(L.value)))
  p.value = Math.min(100, Math.max(0, p.value))

  const size = L.value
  const prob = p.value / 100
  const usedSeed = resolveSeed()
  const rng = createRng(usedSeed)

  isLit = generateLit(size, prob, rng)
  isBurning = makeEmpty(size)

  const stepsPerFrame = Math.max(1, Math.round(size / TARGET_FRAMES))
  const lastRowStart = (size - 1) * size

  let frontier: number[] = []
  for (let col = 0; col < size; col++) {
    if (isLit[col]) {
      isBurning[col] = 1
      frontier.push(col)
    }
  }

  drawFrame()

  function step() {
    if (frontier.length === 0) {
      let reached = false
      for (let col = 0; col < size; col++) {
        if (isBurning[lastRowStart + col]) { reached = true; break }
      }
      resultText.value = reached
        ? `✅ 贯通！L=${size}, p=${p.value}%, seed=${usedSeed}`
        : `❌ 未贯通。L=${size}, p=${p.value}%, seed=${usedSeed}`
      running.value = false
      animTimer = null
      return
    }

    for (let s = 0; s < stepsPerFrame; s++) {
      if (frontier.length === 0) break
      const next: number[] = []
      for (let k = 0; k < frontier.length; k++) {
        const idx = frontier[k]
        const r = (idx / size) | 0
        const c = idx - r * size

        if (r < size - 1) {
          const nidx = idx + size
          if (isLit[nidx] && !isBurning[nidx]) { isBurning[nidx] = 1; next.push(nidx) }
        }
        if (r > 0) {
          const nidx = idx - size
          if (isLit[nidx] && !isBurning[nidx]) { isBurning[nidx] = 1; next.push(nidx) }
        }
        if (c < size - 1) {
          const nidx = idx + 1
          if (isLit[nidx] && !isBurning[nidx]) { isBurning[nidx] = 1; next.push(nidx) }
        }
        if (c > 0) {
          const nidx = idx - 1
          if (isLit[nidx] && !isBurning[nidx]) { isBurning[nidx] = 1; next.push(nidx) }
        }
      }
      frontier = next
    }

    drawFrame()
    animTimer = window.setTimeout(step, FRAME_DELAY)
  }

  animTimer = window.setTimeout(step, FRAME_DELAY)
}

// ============ 批量统计 ============
function startBatch() {
  if (busy.value) return
  stopAnim()

  running.value = true
  resultText.value = ''

  L.value = Math.min(2000, Math.max(2, Math.round(L.value)))
  p.value = Math.min(100, Math.max(0, p.value))

  const size = L.value
  const prob = p.value / 100
  const N = trials.value
  const pVal = p.value
  const baseSeed = resolveSeed()

  resultText.value = `正在跑 ${N} 次...`

  window.setTimeout(() => {
    const rng = createRng(baseSeed)
    let passed = 0
    for (let i = 0; i < N; i++) {
      const lit = generateLit(size, prob, rng)
      if (percolates(lit, size)) passed++
    }

    history.value.push({
      id: ++historyIdCounter,
      L: size,
      p: pVal,
      trials: N,
      passed,
      rate: passed / N,
      seed: baseSeed,
    })

    resultText.value = `L=${size}, p=${pVal}%, seed=${baseSeed} → 贯通率 ${((passed / N) * 100).toFixed(1)}%`
    running.value = false
    redrawCurve()
  }, 30)
}

// ============ 曲线绘制（多系列） ============
function drawCurve(
  series: CurveSeries[],
  historyPts: HistoryEntry[],
  pMinV: number,
  pMaxV: number,
) {
  const canvas = curveRef.value
  if (!canvas) return
  const ctx = canvas.getContext('2d')
  if (!ctx) return

  const dpr = window.devicePixelRatio || 1
  const cssW = canvas.clientWidth || 700
  const cssH = canvas.clientHeight || Math.round(cssW * 3 / 8)
  const W = Math.round(cssW * dpr)
  const H = Math.round(cssH * dpr)

  if (canvas.width !== W) canvas.width = W
  if (canvas.height !== H) canvas.height = H

  ctx.setTransform(dpr, 0, 0, dpr, 0, 0)
  const w = cssW
  const h = cssH
  const pad = 48

  ctx.fillStyle = COLOR_CURVE_BG
  ctx.fillRect(0, 0, w, h)

  const xOf = (pVal: number) => pad + (pVal - pMinV) / (pMaxV - pMinV) * (w - 2 * pad)
  const yOf = (rate: number) => h - pad - rate * (h - 2 * pad)

  // 坐标轴
  ctx.strokeStyle = COLOR_CURVE_AXIS
  ctx.lineWidth = 1
  ctx.beginPath()
  ctx.moveTo(pad, pad)
  ctx.lineTo(pad, h - pad)
  ctx.lineTo(w - pad, h - pad)
  ctx.stroke()

  // 水平网格
  ctx.strokeStyle = '#252b33'
  for (const r of [0.25, 0.5, 0.75]) {
    const y = yOf(r)
    ctx.beginPath()
    ctx.moveTo(pad, y)
    ctx.lineTo(w - pad, y)
    ctx.stroke()
  }

  // p_c 竖直虚线
  if (P_C >= pMinV && P_C <= pMaxV) {
    const xc = xOf(P_C)
    ctx.strokeStyle = COLOR_CURVE_PC
    ctx.setLineDash([4, 4])
    ctx.beginPath()
    ctx.moveTo(xc, pad)
    ctx.lineTo(xc, h - pad)
    ctx.stroke()
    ctx.setLineDash([])

    // p_c 标签用深色底板增加可读性
    const label = 'p_c ≈ 0.5927'
    ctx.font = '11px sans-serif'
    const tw = ctx.measureText(label).width
    ctx.fillStyle = 'rgba(22, 27, 34, 0.9)'
    ctx.fillRect(xc + 4, pad + 2, tw + 8, 16)
    ctx.fillStyle = COLOR_CURVE_PC
    ctx.textAlign = 'left'
    ctx.fillText(label, xc + 8, pad + 14)
  }

  // ===== 扫描曲线（多系列） =====
  for (const s of series) {
    if (s.points.length === 0) continue

    ctx.strokeStyle = s.color
    ctx.lineWidth = 2
    ctx.beginPath()
    s.points.forEach((pt, i) => {
      const x = xOf(pt.p)
      const y = yOf(pt.rate)
      if (i === 0) ctx.moveTo(x, y)
      else ctx.lineTo(x, y)
    })
    ctx.stroke()

    // 数据点
    ctx.fillStyle = s.color
    for (const pt of s.points) {
      ctx.beginPath()
      ctx.arc(xOf(pt.p), yOf(pt.rate), 2.5, 0, Math.PI * 2)
      ctx.fill()
    }

    // L 标签放在曲线右端
    const last = s.points[s.points.length - 1]
    if (last) {
      const lx = xOf(last.p) + 8
      const ly = yOf(last.rate)
      ctx.font = 'bold 12px sans-serif'
      const labelText = `L=${s.L}`
      const tw = ctx.measureText(labelText).width
      ctx.fillStyle = 'rgba(22, 27, 34, 0.85)'
      ctx.fillRect(lx - 2, ly - 8, tw + 6, 16)
      ctx.fillStyle = s.color
      ctx.textAlign = 'left'
      ctx.fillText(labelText, lx + 1, ly + 4)
    }
  }

  // ===== 手动点 =====
  const visibleHistory = historyPts
    .map(h => ({ p: h.p / 100, rate: h.rate }))
    .filter(hp => hp.p >= pMinV && hp.p <= pMaxV)

  ctx.fillStyle = COLOR_HISTORY_DOT
  ctx.strokeStyle = '#161b22'
  ctx.lineWidth = 2

  for (const hp of visibleHistory) {
    const x = xOf(hp.p)
    const y = yOf(hp.rate)
    ctx.beginPath()
    ctx.arc(x, y, 5, 0, Math.PI * 2)
    ctx.fill()
    ctx.stroke()
  }

  ctx.fillStyle = COLOR_HISTORY_DOT
  ctx.font = 'bold 11px sans-serif'
  ctx.textAlign = 'left'
  for (const hp of visibleHistory) {
    const x = xOf(hp.p)
    const y = yOf(hp.rate)
    const label = `${Math.round(hp.rate * 100)}%`
    const labelY = y - 10 < pad ? y + 17 : y - 8
    ctx.fillText(label, x + 7, labelY)
  }

  // 轴标签
  ctx.fillStyle = COLOR_CURVE_TEXT
  ctx.font = '12px sans-serif'
  ctx.textAlign = 'left'
  ctx.fillText('贯通率', 8, pad + 4)
  ctx.fillText('p', w - pad - 6, h - pad + 22)
  ctx.fillText('0', pad - 16, h - pad + 4)
  ctx.fillText('1', pad - 12, pad + 4)
  ctx.fillText(`${(pMinV * 100).toFixed(0)}%`, pad - 20, h - pad + 22)
  ctx.fillText(`${(pMaxV * 100).toFixed(0)}%`, w - pad - 28, h - pad + 22)
}

function redrawCurve() {
  drawCurve(
    scanSeries.value,
    history.value,
    pMin.value / 100,
    pMax.value / 100,
  )
}

function removeSeries(id: number) {
  if (busy.value) return
  scanSeries.value = scanSeries.value.filter(s => s.id !== id)
  redrawCurve()
}

function clearAllSeries() {
  if (busy.value) return
  scanSeries.value = []
  redrawCurve()
}

// ============ 扫描 ============
async function startScan() {
  if (busy.value) return
  stopAnim()

  scanning.value = true
  resultText.value = ''

  L.value = Math.min(2000, Math.max(2, Math.round(L.value)))
  pMin.value = Math.min(100, Math.max(0, pMin.value))
  pMax.value = Math.min(100, Math.max(0, pMax.value))
  if (pMin.value >= pMax.value) {
    resultText.value = '⚠️ 扫描范围的下限必须小于上限'
    scanning.value = false
    return
  }

  const size = L.value
  const N = trials.value
  const pMinV = pMin.value / 100
  const pMaxV = pMax.value / 100
  const STEPS = 25

  const baseSeed = resolveSeed()
  const color = SERIES_COLORS[scanSeries.value.length % SERIES_COLORS.length]

  const newSeries: CurveSeries = {
    id: ++seriesIdCounter,
    L: size,
    color,
    points: [],
  }
  scanSeries.value.push(newSeries)

  resultText.value = `扫描 L=${size} ... 0 / ${STEPS + 1}`
  const t0 = performance.now()

  for (let i = 0; i <= STEPS; i++) {
    const pVal = pMinV + (pMaxV - pMinV) * i / STEPS
    const rng = createRng((baseSeed + i * 7919) >>> 0)

    let passed = 0
    for (let t = 0; t < N; t++) {
      const lit = generateLit(size, pVal, rng)
      if (percolates(lit, size)) passed++
    }
    newSeries.points.push({ p: pVal, rate: passed / N })

    redrawCurve()

    const elapsed = ((performance.now() - t0) / 1000).toFixed(1)
    const remain = ((performance.now() - t0) / (i + 1) * (STEPS - i) / 1000).toFixed(1)
    resultText.value = `扫描 L=${size} ... ${i + 1} / ${STEPS + 1}（p=${(pVal * 100).toFixed(1)}%）已用 ${elapsed}s，预计剩余 ${remain}s`

    await new Promise(r => setTimeout(r, 0))
  }

  resultText.value = `扫描完成：L=${size}，共 ${STEPS + 1} 个点，每个点 ${N} 次。可以改 L 再扫一条叠加。`
  scanning.value = false
}

// ============ 生命周期 ============
watch(L, async () => {
  if (busy.value) return
  isLit = makeEmpty(L.value)
  isBurning = makeEmpty(L.value)
  await nextTick()
  drawFrame()
})

onMounted(() => {
  isLit = makeEmpty(L.value)
  isBurning = makeEmpty(L.value)
  nextTick(() => {
    drawFrame()
    redrawCurve()
  })
})
</script>

<style scoped>
.sim {
  max-width: 1200px;
  margin: 0 auto;
  padding: 32px 24px 60px;
  display: flex;
  flex-direction: column;
  gap: 20px;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
}

/* ============ 头部 ============ */
.sim-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 16px;
  padding-bottom: 4px;
}

.sim-header h1 {
  font-size: 22px;
  font-weight: 600;
  margin: 0 0 6px;
  color: var(--tb-text);
  letter-spacing: -0.3px;
}

.sim-header .sub {
  font-size: 13px;
  color: var(--tb-text-muted);
  margin: 0 0 4px;
  line-height: 1.5;
}

.sim-header .sub-2 {
  font-size: 12px;
  color: var(--tb-text-dim);
}

.sim-header .sub-2 b {
  color: #ff9e2c;
  font-weight: 600;
}

.sim-header .sub sub {
  font-size: 0.75em;
}

.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  padding: 5px 12px;
  border-radius: 0;
  background: var(--tb-card-bg);
  border: 1px solid var(--tb-border);
  color: var(--tb-text-muted);
  flex-shrink: 0;
}

.status-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: #8b949e;
}

.status-badge.running .status-dot {
  background: #d48400;
  animation: pulse 1.2s ease-in-out infinite;
}

.status-badge.ready .status-dot {
  background: #1a7f37;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.3; }
}

/* ============ 参数面板 ============ */
.control-panel {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px 28px;
  padding: 14px 20px;
  background: var(--tb-card-bg);
  border: 1px solid var(--tb-border);
  border-radius: 0;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.03);
}

.control-group {
  display: flex;
  align-items: center;
  gap: 10px;
}

.group-label {
  font-size: 11px;
  font-weight: 600;
  color: var(--tb-text-dim);
  text-transform: uppercase;
  letter-spacing: 0.6px;
  padding-right: 10px;
  border-right: 1px solid var(--tb-border);
  margin-right: 2px;
}

.control-group label {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: var(--tb-text-muted);
}

.unit {
  color: var(--tb-text-dim);
  font-size: 13px;
}

.tilde {
  color: var(--tb-text-dim);
  margin: 0 2px;
}

.control-panel input,
.control-panel select {
  padding: 6px 10px;
  border: 1px solid var(--tb-border);
  border-radius: 0;
  background: var(--tb-bg);
  color: var(--tb-text);
  font-size: 13px;
  font-variant-numeric: tabular-nums;
  outline: none;
  transition: border-color 0.15s;
}

.control-panel input {
  width: 80px;
}

.control-panel input.input-seed {
  width: 90px;
}

.control-panel input.input-p {
  width: 60px;
}

.control-panel input:focus,
.control-panel select:focus {
  border-color: var(--tb-accent);
}

.control-panel button {
  padding: 7px 14px;
  border: 1px solid var(--tb-accent);
  border-radius: 0;
  background: var(--tb-accent);
  color: #fff;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: 0.15s;
}

.control-panel button:hover:not(:disabled) {
  background: var(--tb-accent-hover);
  border-color: var(--tb-accent-hover);
}

.control-panel button:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

.control-panel .btn-icon {
  padding: 6px 10px;
  background: transparent;
  color: var(--tb-text-muted);
  border-color: var(--tb-border);
}

.control-panel .btn-icon:hover:not(:disabled) {
  background: var(--tb-bg);
  border-color: var(--tb-accent);
  color: var(--tb-accent);
}

.control-panel .btn-secondary {
  background: transparent;
  color: var(--tb-accent);
}

.control-panel .btn-secondary:hover:not(:disabled) {
  background: var(--tb-accent);
  color: #fff;
}

/* ============ 主视图 ============ */
.main-view {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 340px;
  gap: 20px;
  align-items: start;
}

@media (max-width: 900px) {
  .main-view {
    grid-template-columns: 1fr;
  }
}

.canvas-card {
  background: var(--tb-card-bg);
  border: 1px solid var(--tb-border);
  border-radius: 0;
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.03);
}

.canvas-card canvas {
  width: 100%;
  aspect-ratio: 1 / 1;
  height: auto;
  border-radius: 0;
  display: block;
  background: #161b22;
}

.canvas-caption {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: var(--tb-text-dim);
  font-variant-numeric: tabular-nums;
}

.canvas-caption .sep {
  color: var(--tb-border);
}

/* ============ 侧面板 ============ */
.side-panel {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.card {
  background: var(--tb-card-bg);
  border: 1px solid var(--tb-border);
  border-radius: 0;
  padding: 16px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.03);
}

.card h3 {
  margin: 0 0 12px;
  font-size: 12px;
  font-weight: 600;
  color: var(--tb-text-dim);
  text-transform: uppercase;
  letter-spacing: 0.6px;
}

.card-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
  gap: 12px;
  flex-wrap: wrap;
}

.card-head h3 {
  margin: 0;
}

/* 统计卡片 */
.stat-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 0;
  font-size: 13px;
  border-bottom: 1px solid var(--tb-bg);
}

.stat-row:last-child {
  border-bottom: none;
  padding-bottom: 0;
}

.stat-row:first-of-type {
  padding-top: 0;
}

.stat-row span {
  color: var(--tb-text-muted);
}

.stat-row span sub {
  font-size: 0.75em;
}

.stat-row b {
  color: var(--tb-text);
  font-variant-numeric: tabular-nums;
  font-weight: 600;
}

.stat-row b.near {
  color: #d48400;
}

.stat-row b.mid {
  color: #bf8700;
}

.stat-row b.far {
  color: var(--tb-text-muted);
}

/* 历史记录 */
.btn-clear {
  font-size: 11px;
  padding: 3px 10px;
  border-radius: 0;
  border: 1px solid var(--tb-border);
  background: transparent;
  color: var(--tb-text-dim);
  cursor: pointer;
  transition: 0.15s;
}

.btn-clear:hover:not(:disabled) {
  border-color: var(--tb-danger);
  color: var(--tb-danger);
}

.btn-clear:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.history-scroll {
  max-height: 380px;
  overflow-y: auto;
  margin: 0 -4px;
}

.history-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}

.history-table th {
  text-align: left;
  font-weight: 500;
  color: var(--tb-text-dim);
  padding: 4px 8px;
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.4px;
  position: sticky;
  top: 0;
  background: var(--tb-card-bg);
}

.history-table td {
  padding: 8px;
  color: var(--tb-text);
  border-top: 1px solid var(--tb-bg);
}

.history-table .dim {
  color: var(--tb-text-dim);
}

.history-table .mono {
  font-variant-numeric: tabular-nums;
}

.rate-cell {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 90px;
}

.mini-bar {
  flex: 1;
  height: 5px;
  background: #30363d;
  border-radius: 0;
  overflow: hidden;
  min-width: 32px;
}

.mini-fill {
  height: 100%;
  background: linear-gradient(90deg, #d48400, #ffb428);
  transition: width 0.3s;
}

.rate-num {
  font-variant-numeric: tabular-nums;
  font-weight: 500;
  font-size: 12px;
  min-width: 34px;
  text-align: right;
}

/* ============ 曲线卡片 ============ */
.curve-card {
  padding: 16px;
}

.curve-legend {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px 12px;
  font-size: 12px;
  color: var(--tb-text-muted);
}

.legend-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 3px 10px 3px 8px;
  border-radius: 0;
  background: var(--tb-bg);
  border: 1px solid var(--tb-border);
  font-size: 12px;
  color: var(--tb-text);
  cursor: pointer;
  transition: 0.15s;
  font-variant-numeric: tabular-nums;
}

.legend-chip:hover:not(:disabled) {
  border-color: var(--tb-danger);
}

.legend-chip:hover:not(:disabled) .chip-x {
  color: var(--tb-danger);
}

.legend-chip:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.chip-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  flex-shrink: 0;
}

.chip-x {
  color: var(--tb-text-dim);
  font-size: 14px;
  line-height: 1;
  transition: 0.15s;
}

.legend-empty {
  font-size: 12px;
  color: var(--tb-text-dim);
  font-style: italic;
}

.curve-legend .dot {
  display: inline-block;
  width: 10px;
  height: 10px;
  border-radius: 50%;
}

.curve-legend .dot-history {
  background: #ffffff;
  border: 2px solid #161b22;
  margin-left: 4px;
}

.legend-label {
  margin-left: 2px;
}

.curve-card canvas {
  width: 100%;
  aspect-ratio: 8 / 3;
  height: auto;
  border-radius: 0;
  display: block;
  background: #161b22;
}

/* ============ 说明卡片 ============ */
.explain-card {
  padding: 20px 24px;
}

.explain-card h3 {
  color: var(--tb-text);
  font-size: 14px;
  text-transform: none;
  letter-spacing: 0;
  margin-bottom: 14px;
}

.explain-list {
  margin: 0;
  padding-left: 20px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.explain-list li {
  font-size: 13.5px;
  line-height: 1.65;
  color: var(--tb-text-muted);
}

.explain-list b {
  color: var(--tb-text);
  font-weight: 600;
}

/* ============ 结果文字 ============ */
.result {
  font-size: 13px;
  color: var(--tb-text-muted);
  padding: 10px 18px;
  border-radius: 0;
  background: var(--tb-card-bg);
  border: 1px solid var(--tb-border);
  text-align: center;
  font-variant-numeric: tabular-nums;
}
</style>