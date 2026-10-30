<template>
  <div class="boids-fullscreen">
    <!-- 左上：返回 + 基础参数 -->
    <div class="overlay-panel top-left">
      <button class="btn-back" @click="goBack" title="返回工具箱">←</button>
      <div class="panel-body">
        <label>
          数量
          <input type="number" v-model.number="N" min="10" max="2000" />
        </label>
        <label>
          速度
          <input type="number" v-model.number="speed" min="0.1" max="3" step="0.1" />
        </label>
        <button class="btn-reset" @click="restart">重置</button>
      </div>
    </div>

    <!-- 左下：三条规则的滑条 -->
    <div class="overlay-panel bottom-left">
      <div class="sliders-title">规则权重</div>

      <div class="slider-row">
        <span class="slider-label">
          <span class="dot dot-sep"></span>
          分离
        </span>
        <input
          type="range"
          min="0.1" max="3" step="0.05"
          v-model.number="sepWeight"
        />
        <span class="slider-value">{{ sepWeight.toFixed(2) }}</span>
      </div>

      <div class="slider-row">
        <span class="slider-label">
          <span class="dot dot-ali"></span>
          对齐
        </span>
        <input
          type="range"
          min="0.1" max="3" step="0.05"
          v-model.number="aliWeight"
        />
        <span class="slider-value">{{ aliWeight.toFixed(2) }}</span>
      </div>

      <div class="slider-row">
        <span class="slider-label">
          <span class="dot dot-coh"></span>
          聚合
        </span>
        <input
          type="range"
          min="0.1" max="3" step="0.05"
          v-model.number="cohWeight"
        />
        <span class="slider-value">{{ cohWeight.toFixed(2) }}</span>
      </div>

      <div class="presets">
        <button @click="applyPreset('flow')">河流</button>
        <button @click="applyPreset('cloud')">云团</button>
        <button @click="applyPreset('flock')">鱼群</button>
      </div>
    </div>

    <!-- 底部中间提示 -->
    <div class="overlay-hint">
      {{ N }} 只 · 分离 / 对齐 / 聚合
    </div>

    <canvas ref="canvasRef"></canvas>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()

const N = ref(200)
const speed = ref(1.0)

// 三个权重（0.1 ~ 3.0），界面上可调
const sepWeight = ref(1.0)
const aliWeight = ref(1.0)
const cohWeight = ref(1.0)

const canvasRef = ref<HTMLCanvasElement | null>(null)

interface Bird {
  x: number
  y: number
  vx: number
  vy: number
}

let birds: Bird[] = []
let rafId: number | null = null
let resizeTimer: number | null = null

// ============ 相对比例参数 ============
const PERCEPTION_RATIO = 0.045
const SEPARATION_RATIO = 0.018
const MAX_SPEED_RATIO = 0.0028
const MIN_SPEED_RATIO = 0.0012
const BASE_SPEED_RATIO = 0.0018

// ============ 基础力系数（滑条乘这个） ============
const BASE_SEP_FORCE = 0.35
const BASE_ALI_FORCE = 0.08
const BASE_COH_FORCE = 0.001

let PERCEPTION = 0
let SEPARATION = 0
let MAX_SPEED = 0
let MIN_SPEED = 0

function refreshScale(width: number) {
  PERCEPTION = width * PERCEPTION_RATIO
  SEPARATION = width * SEPARATION_RATIO
  MAX_SPEED = width * MAX_SPEED_RATIO
  MIN_SPEED = width * MIN_SPEED_RATIO
}

function rand(min: number, max: number) {
  return Math.random() * (max - min) + min
}

function spawnBirds(n: number, width: number, height: number): Bird[] {
  const result: Bird[] = []
  const base = width * BASE_SPEED_RATIO
  for (let i = 0; i < n; i++) {
    const angle = Math.random() * Math.PI * 2
    const s = rand(base * 0.7, base * 1.3)
    result.push({
      x: rand(0, width),
      y: rand(0, height),
      vx: Math.cos(angle) * s,
      vy: Math.sin(angle) * s,
    })
  }
  return result
}

// ============ 三条规则 ============
function updateBirds(width: number, height: number) {
  refreshScale(width)

  const SEP_FORCE = BASE_SEP_FORCE * sepWeight.value
  const ALI_FORCE = BASE_ALI_FORCE * aliWeight.value
  const COH_FORCE = BASE_COH_FORCE * cohWeight.value

  const ax: number[] = new Array(birds.length).fill(0)
  const ay: number[] = new Array(birds.length).fill(0)

  for (let i = 0; i < birds.length; i++) {
    const a = birds[i]

    let sepX = 0, sepY = 0, sepCount = 0
    let aliX = 0, aliY = 0, aliCount = 0
    let cohX = 0, cohY = 0, cohCount = 0

    for (let j = 0; j < birds.length; j++) {
      if (i === j) continue
      const b = birds[j]
      const dx = b.x - a.x
      const dy = b.y - a.y
      const dist = Math.hypot(dx, dy)

      if (dist > 0 && dist < SEPARATION) {
        const strength = (SEPARATION - dist) / SEPARATION
        sepX -= (dx / dist) * strength
        sepY -= (dy / dist) * strength
        sepCount++
      }

      if (dist < PERCEPTION) {
        aliX += b.vx
        aliY += b.vy
        aliCount++

        cohX += b.x
        cohY += b.y
        cohCount++
      }
    }

    if (sepCount > 0) {
      ax[i] += (sepX / sepCount) * SEP_FORCE
      ay[i] += (sepY / sepCount) * SEP_FORCE
    }
    if (aliCount > 0) {
      const avgVx = aliX / aliCount
      const avgVy = aliY / aliCount
      ax[i] += (avgVx - a.vx) * ALI_FORCE
      ay[i] += (avgVy - a.vy) * ALI_FORCE
    }
    if (cohCount > 0) {
      const avgX = cohX / cohCount
      const avgY = cohY / cohCount
      ax[i] += (avgX - a.x) * COH_FORCE
      ay[i] += (avgY - a.y) * COH_FORCE
    }
  }

  for (let i = 0; i < birds.length; i++) {
    const b = birds[i]
    b.vx += ax[i]
    b.vy += ay[i]

    const sp = Math.hypot(b.vx, b.vy)
    if (sp > MAX_SPEED) {
      b.vx = (b.vx / sp) * MAX_SPEED
      b.vy = (b.vy / sp) * MAX_SPEED
    } else if (sp > 0 && sp < MIN_SPEED) {
      b.vx = (b.vx / sp) * MIN_SPEED
      b.vy = (b.vy / sp) * MIN_SPEED
    }

    b.x += b.vx * speed.value
    b.y += b.vy * speed.value

    if (b.x < 0) b.x += width
    if (b.x > width) b.x -= width
    if (b.y < 0) b.y += height
    if (b.y > height) b.y -= height
  }
}

function draw() {
  const canvas = canvasRef.value
  if (!canvas) return
  const ctx = canvas.getContext('2d')
  if (!ctx) return

  const W = canvas.width
  const H = canvas.height

  ctx.fillStyle = '#0d1117'
  ctx.fillRect(0, 0, W, H)

  const birdSize = W * 0.0035

  ctx.fillStyle = '#ffb428'
  for (const b of birds) {
    const angle = Math.atan2(b.vy, b.vx)

    ctx.beginPath()
    ctx.moveTo(
      b.x + Math.cos(angle) * birdSize,
      b.y + Math.sin(angle) * birdSize,
    )
    ctx.lineTo(
      b.x + Math.cos(angle + 2.5) * birdSize * 0.7,
      b.y + Math.sin(angle + 2.5) * birdSize * 0.7,
    )
    ctx.lineTo(
      b.x + Math.cos(angle - 2.5) * birdSize * 0.7,
      b.y + Math.sin(angle - 2.5) * birdSize * 0.7,
    )
    ctx.closePath()
    ctx.fill()
  }
}

// ============ 尺寸 ============
const LOGICAL_WIDTH = 1600

function resizeCanvas() {
  const canvas = canvasRef.value
  if (!canvas) return

  const vw = window.innerWidth
  const vh = window.innerHeight
  const logicalHeight = Math.round(LOGICAL_WIDTH * vh / vw)

  canvas.width = LOGICAL_WIDTH
  canvas.height = logicalHeight
  canvas.style.width = vw + 'px'
  canvas.style.height = vh + 'px'
}

function step() {
  const canvas = canvasRef.value
  if (!canvas) return

  updateBirds(canvas.width, canvas.height)
  draw()
  rafId = requestAnimationFrame(step)
}

function restart() {
  const canvas = canvasRef.value
  if (!canvas) return
  birds = spawnBirds(N.value, canvas.width, canvas.height)
}

function stop() {
  if (rafId !== null) {
    cancelAnimationFrame(rafId)
    rafId = null
  }
}

function goBack() {
  router.push('/toolBox')
}

// ============ 预设 ============
function applyPreset(name: 'flow' | 'cloud' | 'flock') {
  if (name === 'flow') {
    // 河流：对齐强，聚合中，分离中
    sepWeight.value = 0.8
    aliWeight.value = 1.8
    cohWeight.value = 1.0
  } else if (name === 'cloud') {
    // 云团：聚合强，对齐中，分离中
    sepWeight.value = 1.0
    aliWeight.value = 0.6
    cohWeight.value = 2.2
  } else if (name === 'flock') {
    // 鱼群：三个均衡
    sepWeight.value = 1.0
    aliWeight.value = 1.0
    cohWeight.value = 1.0
  }
}

function handleResize() {
  if (resizeTimer !== null) clearTimeout(resizeTimer)
  resizeTimer = window.setTimeout(() => {
    resizeCanvas()
    const canvas = canvasRef.value
    if (canvas) {
      for (const b of birds) {
        b.x = Math.min(b.x, canvas.width)
        b.y = Math.min(b.y, canvas.height)
      }
    }
  }, 120)
}

watch(N, () => {
  restart()
})

onMounted(() => {
  resizeCanvas()
  restart()
  step()
  window.addEventListener('resize', handleResize)
})

onUnmounted(() => {
  stop()
  window.removeEventListener('resize', handleResize)
  if (resizeTimer !== null) clearTimeout(resizeTimer)
})
</script>

<style scoped>
.boids-fullscreen {
  position: fixed;
  inset: 0;
  z-index: 100;
  background: #0d1117;
  overflow: hidden;
}

canvas {
  display: block;
  width: 100%;
  height: 100%;
}

/* ============ 浮层通用 ============ */
.overlay-panel {
  position: absolute;
  z-index: 10;
  pointer-events: none;
  display: flex;
  gap: 10px;
  margin-bottom: 5%;
}

.overlay-panel > * {
  pointer-events: auto;
}

.top-left {
  top: 16px;
  left: 16px;
  align-items: center;
}

.bottom-left {
  bottom: 20px;
  left: 16px;
  flex-direction: column;
  padding: 14px 16px;
  border-radius: 12px;
  background: rgba(22, 27, 34, 0.75);
  backdrop-filter: blur(10px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  min-width: 280px;
}

.panel-body {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 14px;
  border-radius: 10px;
  background: rgba(22, 27, 34, 0.75);
  backdrop-filter: blur(10px);
  border: 1px solid rgba(255, 255, 255, 0.08);
}

.btn-back {
  width: 40px;
  height: 40px;
  border-radius: 10px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  background: rgba(22, 27, 34, 0.75);
  backdrop-filter: blur(10px);
  color: #e6edf3;
  font-size: 18px;
  cursor: pointer;
  transition: 0.15s;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0;
}

.btn-back:hover {
  background: rgba(22, 27, 34, 0.95);
  border-color: rgba(255, 255, 255, 0.25);
}

.panel-body label {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: #8b949e;
}

.panel-body input {
  width: 70px;
  padding: 4px 8px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  background: rgba(13, 17, 23, 0.6);
  color: #e6edf3;
  font-size: 13px;
  outline: none;
  font-variant-numeric: tabular-nums;
}

.panel-body input:focus {
  border-color: #2f6feb;
}

.btn-reset {
  padding: 5px 12px;
  border: 1px solid rgba(255, 255, 255, 0.15);
  border-radius: 6px;
  background: transparent;
  color: #e6edf3;
  font-size: 13px;
  cursor: pointer;
  transition: 0.15s;
}

.btn-reset:hover {
  background: rgba(255, 255, 255, 0.05);
  border-color: rgba(255, 255, 255, 0.3);
}

/* ============ 滑条 ============ */
.sliders-title {
  font-size: 11px;
  font-weight: 600;
  color: #8b949e;
  text-transform: uppercase;
  letter-spacing: 0.6px;
  margin-bottom: 10px;
}

.slider-row {
  display: grid;
  grid-template-columns: 60px 1fr 40px;
  align-items: center;
  gap: 10px;
  margin-bottom: 8px;
}

.slider-label {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: #8b949e;
}

.slider-label .dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.dot-sep { background: #ff6b6b; }
.dot-ali { background: #56b4e9; }
.dot-coh { background: #4dd07a; }

.slider-row input[type="range"] {
  -webkit-appearance: none;
  appearance: none;
  height: 4px;
  border-radius: 2px;
  background: rgba(255, 255, 255, 0.1);
  outline: none;
  cursor: pointer;
}

.slider-row input[type="range"]::-webkit-slider-thumb {
  -webkit-appearance: none;
  appearance: none;
  width: 14px;
  height: 14px;
  border-radius: 50%;
  background: #e6edf3;
  border: 2px solid #161b22;
  cursor: pointer;
  transition: 0.15s;
}

.slider-row input[type="range"]::-webkit-slider-thumb:hover {
  background: #2f6feb;
}

.slider-row input[type="range"]::-moz-range-thumb {
  width: 14px;
  height: 14px;
  border-radius: 50%;
  background: #e6edf3;
  border: 2px solid #161b22;
  cursor: pointer;
}

.slider-value {
  font-size: 12px;
  color: #e6edf3;
  text-align: right;
  font-variant-numeric: tabular-nums;
}

/* ============ 预设按钮 ============ */
.presets {
  display: flex;
  gap: 6px;
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px solid rgba(255, 255, 255, 0.06);
}

.presets button {
  flex: 1;
  padding: 5px 8px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  background: transparent;
  color: #8b949e;
  font-size: 12px;
  cursor: pointer;
  transition: 0.15s;
}

.presets button:hover {
  background: rgba(255, 255, 255, 0.05);
  border-color: rgba(255, 255, 255, 0.25);
  color: #e6edf3;
}

/* ============ 底部提示 ============ */
.overlay-hint {
  position: absolute;
  bottom: 20px;
  left: 50%;
  transform: translateX(-50%);
  font-size: 12px;
  color: rgba(230, 237, 243, 0.5);
  padding: 6px 14px;
  border-radius: 999px;
  background: rgba(22, 27, 34, 0.6);
  backdrop-filter: blur(8px);
  border: 1px solid rgba(255, 255, 255, 0.06);
  font-variant-numeric: tabular-nums;
  pointer-events: none;
  user-select: none;
}

@media (max-width: 700px) {
  .top-left {
    top: 10px;
    left: 10px;
  }
  .bottom-left {
    bottom: 10px;
    left: 10px;
    min-width: 240px;
    padding: 10px 12px;
  }
  .overlay-hint {
    display: none;
  }
}
</style>