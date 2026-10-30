<script setup lang="ts">
// 全站背景粒子：铺在页面最底层，缓慢漂浮。
// · 鼠标经过时，附近的粒子之间连出细线（像星座），并被轻轻推开；
// · 每隔一段时间，一部分粒子在页面空白处聚成一个图形（站标、菱形等），停留几秒后再散开。
// 画布不拦截任何点击；页面隐藏时暂停；系统开启「减少动态效果」时只画静止的粒子。
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { diamondShape, hexShape, targetShape, type ParticleShape } from '@/utils/particleShapes'

const props = withDefaults(defineProps<{
  /** 聚形间隔（毫秒） */
  formInterval?: number
  colors?: string[]
}>(), {
  formInterval: 18000,
  colors: () => ['--signal-mint', '--accent-color', '--text-secondary'],
})

const FORMATIONS: ParticleShape[] = [
  { kind: 'image', src: '/taichu-logo.png' },
  { kind: 'draw', draw: diamondShape },
  { kind: 'text', text: '太初', scale: 0.5 },
  { kind: 'draw', draw: hexShape },
  { kind: 'draw', draw: targetShape },
]

interface Dot { x: number; y: number; vx: number; vy: number; size: number; tone: number; alpha: number; tx: number | null; ty: number | null
  /** 聚形时临时召集的粒子：先淡入聚成图形，散开后淡出并移除 */
  transient?: boolean; dying?: boolean; fade?: number }

const canvas = ref<HTMLCanvasElement>()
let ctx: CanvasRenderingContext2D | null = null
let width = 0, height = 0, dpr = 1
let dots: Dot[] = []
let palette: string[] = []
let frame = 0, formTimer = 0, releaseTimer = 0, formationIndex = 0
let running = false, reduced = false, disposed = false, forming = false
const mouse = { x: -9999, y: -9999, active: false }
let resizeObserver: ResizeObserver | undefined
let motionQuery: MediaQueryList | undefined
const logo = new Image()
logo.src = '/taichu-logo.png'

function readPalette() {
  const style = canvas.value ? getComputedStyle(canvas.value) : null
  palette = props.colors.map(c => c.startsWith('--') ? (style?.getPropertyValue(c).trim() || '#3fe0c5') : c)
}

function seed() {
  const count = Math.round(Math.min(240, Math.max(70, (width * height) / 8500)))
  dots = Array.from({ length: count }, () => ({
    x: Math.random() * width, y: Math.random() * height,
    vx: (Math.random() - 0.5) * 0.3, vy: (Math.random() - 0.5) * 0.3,
    size: 1 + Math.random() * 1.6, tone: Math.random() < 0.55 ? 0 : Math.random() < 0.6 ? 1 : 2,
    alpha: 0.25 + Math.random() * 0.45, tx: null, ty: null,
  }))
}

function resize() {
  const el = canvas.value
  if (!el) return
  // 用布局尺寸，避免被变换（缩放、倾斜）影响而导致画布坐标与显示错位
  const w = el.clientWidth, h = el.clientHeight
  if (w < 2 || h < 2) return
  dpr = Math.min(window.devicePixelRatio || 1, 1.5)
  const first = !width
  width = w; height = h
  el.width = Math.round(width * dpr); el.height = Math.round(height * dpr)
  ctx = el.getContext('2d')
  ctx?.setTransform(dpr, 0, 0, dpr, 0, 0)
  if (first || !dots.length) seed()
  else dots.forEach(d => { d.x = Math.min(d.x, width); d.y = Math.min(d.y, height) })
  readPalette()
  if (reduced) draw()
}

// 在离屏画布上画出图形并取样，返回页面坐标下的点。
function sampleFormation(shape: ParticleShape, box: number, ox: number, oy: number) {
  const off = document.createElement('canvas')
  off.width = off.height = Math.round(box)
  const g = off.getContext('2d', { willReadFrequently: true })
  if (!g) return []
  let luminance = false
  if (shape.kind === 'image') {
    if (!logo.complete || !logo.naturalWidth) return []
    g.drawImage(logo, 0, 0, box, box)
    luminance = true
  } else if (shape.kind === 'text') {
    g.fillStyle = '#fff'; g.textAlign = 'center'; g.textBaseline = 'middle'
    g.font = `800 ${box * (shape.scale ?? 0.45)}px ${getComputedStyle(canvas.value!).fontFamily || 'sans-serif'}`
    g.fillText(shape.text, box / 2, box / 2)
  } else {
    shape.draw(g, box, box)
  }
  const data = g.getImageData(0, 0, off.width, off.height).data
  const step = Math.max(3, Math.round(box / 46))
  const points: { x: number; y: number }[] = []
  for (let y = 0; y < off.height; y += step) {
    for (let x = 0; x < off.width; x += step) {
      const i = (y * off.width + x) * 4
      const hit = luminance ? data[i] + data[i + 1] + data[i + 2] > 384 && data[i + 3] > 128 : data[i + 3] > 128
      if (hit) points.push({ x: ox + x + (Math.random() - 0.5) * step, y: oy + y + (Math.random() - 0.5) * step })
    }
  }
  return points
}

function form() {
  if (forming || !running || width < 400) return
  const shape = FORMATIONS[formationIndex++ % FORMATIONS.length]
  // 图形放在页面左右两侧的空白区域，避开正文中间。
  const box = Math.min(260, height * 0.4, width * 0.28)
  const onLeft = formationIndex % 2 === 0
  const ox = onLeft ? width * 0.04 : width - box - width * 0.04
  const oy = height * (0.2 + Math.random() * 0.45)
  const points = sampleFormation(shape, box, ox, Math.min(oy, height - box - 10))
  if (points.length < 20) return
  forming = true
  // 召集一批临时粒子：从现有背景粒子附近出发，飞向图形上的点，同时淡入。
  const order = points.sort(() => Math.random() - 0.5).slice(0, 520)
  const anchors = dots.filter(d => !d.transient)
  for (const t of order) {
    const from = anchors[Math.floor(Math.random() * anchors.length)] ?? { x: Math.random() * width, y: Math.random() * height }
    dots.push({
      x: from.x + (Math.random() - 0.5) * 60, y: from.y + (Math.random() - 0.5) * 60, vx: 0, vy: 0,
      size: 1.1 + Math.random() * 1.3, tone: Math.random() < 0.6 ? 0 : Math.random() < 0.6 ? 1 : 2,
      alpha: 0.55 + Math.random() * 0.35, tx: t.x, ty: t.y, transient: true, fade: 0,
    })
  }
  window.clearTimeout(releaseTimer)
  releaseTimer = window.setTimeout(release, 6500)
}

function release() {
  forming = false
  for (const d of dots) {
    if (!d.transient || d.dying) continue
    d.tx = d.ty = null
    d.dying = true
    const a = Math.random() * Math.PI * 2, f = 0.6 + Math.random() * 1.8
    d.vx += Math.cos(a) * f; d.vy += Math.sin(a) * f
  }
}

function draw() {
  if (!ctx) return
  ctx.clearRect(0, 0, width, height)
  // 鼠标附近的粒子互相连线
  if (mouse.active) {
    const near = dots.filter(d => !d.transient && Math.abs(d.x - mouse.x) < 150 && Math.abs(d.y - mouse.y) < 150)
    ctx.lineWidth = 0.6
    ctx.strokeStyle = palette[0]
    for (let i = 0; i < near.length; i++) {
      for (let j = i + 1; j < near.length; j++) {
        const a = near[i], b = near[j], dist = Math.hypot(a.x - b.x, a.y - b.y)
        if (dist < 80) {
          ctx.globalAlpha = (1 - dist / 80) * 0.45
          ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.stroke()
        }
      }
    }
  }
  for (const d of dots) {
    ctx.globalAlpha = d.transient ? d.alpha * (d.fade ?? 1) : d.alpha
    ctx.fillStyle = palette[d.tone] || palette[0]
    ctx.fillRect(d.x - d.size / 2, d.y - d.size / 2, d.size, d.size)
  }
  ctx.globalAlpha = 1
}

function step() {
  frame = 0
  if (!running || disposed) return
  // 临时粒子：聚形时淡入，散开后淡出，完全透明后移除。
  if (dots.some(d => d.transient)) {
    for (const d of dots) {
      if (!d.transient) continue
      d.fade = d.dying ? (d.fade ?? 1) - 0.012 : Math.min(1, (d.fade ?? 0) + 0.025)
    }
    dots = dots.filter(d => !d.transient || (d.fade ?? 0) > 0)
  }
  for (const d of dots) {
    if (d.tx !== null && d.ty !== null) {
      d.vx += (d.tx - d.x) * 0.02; d.vy += (d.ty - d.y) * 0.02
      d.vx *= 0.86; d.vy *= 0.86
    } else {
      // 自由漂浮：速度慢慢回落到很小的漂移
      d.vx = d.vx * 0.985 + (Math.random() - 0.5) * 0.02
      d.vy = d.vy * 0.985 + (Math.random() - 0.5) * 0.02
    }
    if (mouse.active) {
      const dx = d.x - mouse.x, dy = d.y - mouse.y, d2 = dx * dx + dy * dy
      if (d2 < 6400 && d2 > 1) { const dist = Math.sqrt(d2), f = (1 - dist / 80) * 0.35; d.vx += (dx / dist) * f; d.vy += (dy / dist) * f }
    }
    d.x += d.vx; d.y += d.vy
    if (d.tx === null && !d.transient) {
      if (d.x < -10) d.x = width + 10; else if (d.x > width + 10) d.x = -10
      if (d.y < -10) d.y = height + 10; else if (d.y > height + 10) d.y = -10
    }
  }
  draw()
  frame = requestAnimationFrame(step)
}

function start() {
  if (running || reduced || disposed || document.hidden) return
  running = true
  frame = requestAnimationFrame(step)
  window.clearInterval(formTimer)
  formTimer = window.setInterval(form, props.formInterval)
  window.setTimeout(form, 2500)
}
function stop() {
  running = false
  if (frame) cancelAnimationFrame(frame)
  frame = 0
  window.clearInterval(formTimer)
}

function onPointerMove(event: PointerEvent) {
  const rect = canvas.value?.getBoundingClientRect()
  if (!rect) return
  mouse.x = event.clientX - rect.left; mouse.y = event.clientY - rect.top
  mouse.active = mouse.x >= 0 && mouse.y >= 0 && mouse.x <= rect.width && mouse.y <= rect.height
}
function onPointerLeave() { mouse.active = false }
function onVisibility() { document.hidden ? stop() : start() }
function onMotionChange() { reduced = Boolean(motionQuery?.matches); if (reduced) { stop(); forming = false; dots = dots.filter(d => !d.transient); draw() } else start() }

onMounted(() => {
  motionQuery = window.matchMedia('(prefers-reduced-motion: reduce)')
  reduced = motionQuery.matches
  motionQuery.addEventListener('change', onMotionChange)
  document.addEventListener('visibilitychange', onVisibility)
  window.addEventListener('pointermove', onPointerMove, { passive: true })
  document.documentElement.addEventListener('pointerleave', onPointerLeave)
  resizeObserver = new ResizeObserver(resize)
  resizeObserver.observe(canvas.value!)
  resize()
  reduced ? draw() : start()
})

onBeforeUnmount(() => {
  disposed = true
  stop()
  window.clearTimeout(releaseTimer)
  resizeObserver?.disconnect()
  motionQuery?.removeEventListener('change', onMotionChange)
  document.removeEventListener('visibilitychange', onVisibility)
  window.removeEventListener('pointermove', onPointerMove)
  document.documentElement.removeEventListener('pointerleave', onPointerLeave)
})
</script>

<template>
  <canvas ref="canvas" class="particle-field" aria-hidden="true"></canvas>
</template>

<style>
:where(.particle-field) { position: absolute; inset: 0; width: 100%; height: 100%; pointer-events: none; z-index: 0; }
</style>
