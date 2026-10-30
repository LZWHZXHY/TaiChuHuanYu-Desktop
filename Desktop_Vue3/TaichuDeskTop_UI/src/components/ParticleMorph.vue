<script setup lang="ts">
// 粒子变形：粒子聚成一个图形，定时散开再重组为下一个图形；鼠标靠近会把粒子推开。
// 不在视口内、页面隐藏时暂停；系统设置「减少动态效果」时只显示第一个图形，不做动画。
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import type { ParticleShape } from '@/utils/particleShapes'

const props = withDefaults(defineProps<{
  shapes: ParticleShape[]
  /** 每个图形停留的毫秒数 */
  interval?: number
  /** 粒子颜色，填 CSS 变量名或颜色值，按比例随机分配 */
  colors?: string[]
  /** 粒子密度：每多少平方像素一个粒子，越小越密 */
  density?: number
  maxParticles?: number
  /** 图形占画布的比例 */
  fill?: number
  /** 鼠标交互：self 只在画布上响应；parent 在父元素上响应（画布不拦截点击，适合叠在按钮下面）；none 不响应 */
  interactive?: 'self' | 'parent' | 'none'
}>(), {
  interactive: 'self',
  interval: 5200,
  colors: () => ['--inverse-text', '--signal-mint', '--signal-color'],
  density: 52,
  maxParticles: 2200,
  fill: 0.86,
})

const canvas = ref<HTMLCanvasElement>()
const current = ref(0)
defineExpose({ current })

interface Particle { x: number; y: number; vx: number; vy: number; tx: number; ty: number; size: number; tone: number; phase: number }

let ctx: CanvasRenderingContext2D | null = null
let width = 0, height = 0, dpr = 1
let particles: Particle[] = []
let palette: string[] = []
let frame = 0, morphTimer = 0
let visible = true, pageVisible = true, reduced = false, disposed = false
let pointer = { x: -9999, y: -9999, active: false }
const targetCache = new Map<string, { x: number; y: number }[]>()
const imageCache = new Map<string, Promise<HTMLImageElement>>()
let resizeObserver: ResizeObserver | undefined
let intersection: IntersectionObserver | undefined
let motionQuery: MediaQueryList | undefined
let pointerHost: HTMLElement | null = null

function readPalette() {
  const style = canvas.value ? getComputedStyle(canvas.value) : null
  palette = props.colors.map(c => c.startsWith('--') ? (style?.getPropertyValue(c).trim() || '#9be7e0') : c)
}

function loadImage(src: string) {
  if (!imageCache.has(src)) {
    imageCache.set(src, new Promise((resolve, reject) => {
      const img = new Image()
      img.crossOrigin = 'anonymous'
      img.onload = () => resolve(img)
      img.onerror = reject
      img.src = src
    }))
  }
  return imageCache.get(src)!
}

// 把图形画到离屏画布上，再按网格取样得到目标点。
async function sampleShape(index: number): Promise<{ x: number; y: number }[]> {
  const shape = props.shapes[index]
  const key = `${index}:${width}x${height}`
  if (targetCache.has(key)) return targetCache.get(key)!
  const off = document.createElement('canvas')
  off.width = Math.max(1, Math.round(width)); off.height = Math.max(1, Math.round(height))
  const g = off.getContext('2d', { willReadFrequently: true })
  if (!g) return []
  const box = Math.min(width, height) * props.fill
  const bx = (width - box) / 2, by = (height - box) / 2
  let useLuminance = false

  if (shape.kind === 'image') {
    try {
      const img = await loadImage(shape.src)
      const s = box * (shape.scale ?? 1)
      const ratio = img.width / img.height
      const dw = ratio >= 1 ? s : s * ratio, dh = ratio >= 1 ? s / ratio : s
      g.drawImage(img, (width - dw) / 2, (height - dh) / 2, dw, dh)
      // 不透明的图片（例如黑底白字的标识）按亮度取样，带透明的图片按透明度取样。
      const probe = g.getImageData(0, 0, off.width, off.height).data
      let transparent = 0, inside = 0
      for (let i = 3; i < probe.length; i += 16) { if (probe[i] > 0) { inside++; if (probe[i] < 128) transparent++ } }
      useLuminance = inside > 0 && transparent / inside < 0.02
    } catch { return [] }
  } else if (shape.kind === 'text') {
    const family = getComputedStyle(canvas.value!).fontFamily || 'sans-serif'
    let size = box * (shape.scale ?? 0.42)
    g.fillStyle = '#fff'; g.textAlign = 'center'; g.textBaseline = 'middle'
    const font = (px: number) => `${shape.weight ?? 800} ${px}px ${shape.mono ? 'ui-monospace, monospace' : family}`
    g.font = font(size)
    const measured = g.measureText(shape.text).width
    if (measured > width * 0.9) { size *= (width * 0.9) / measured; g.font = font(size) }
    g.fillText(shape.text, width / 2, height / 2)
  } else {
    g.save(); g.translate(bx, by); shape.draw(g, box, box); g.restore()
  }

  const data = g.getImageData(0, 0, off.width, off.height).data
  const area = width * height
  // 取样网格比粒子更密，再随机挑点，避免排成整齐的网格。
  const step = Math.max(1, Math.round(Math.sqrt(area / ((particles.length || 1) * 6))))
  const points: { x: number; y: number }[] = []
  for (let y = 0; y < off.height; y += step) {
    for (let x = 0; x < off.width; x += step) {
      const i = (y * off.width + x) * 4
      const hit = useLuminance
        ? (data[i] * 0.299 + data[i + 1] * 0.587 + data[i + 2] * 0.114) > 128 && data[i + 3] > 128
        : data[i + 3] > 128
      if (hit) points.push({ x: x + (Math.random() - 0.5) * step, y: y + (Math.random() - 0.5) * step })
    }
  }
  targetCache.set(key, points)
  return points
}

async function morphTo(index: number, burst = true) {
  const points = await sampleShape(index)
  if (disposed || !points.length) return
  current.value = index
  readPalette()
  // 打乱后分配，粒子数多于取样点时复用并加一点抖动。
  const order = points.slice().sort(() => Math.random() - 0.5)
  particles.forEach((p, i) => {
    const t = order[i % order.length]
    const jitter = i >= order.length ? 2.2 : 0.6
    p.tx = t.x + (Math.random() - 0.5) * jitter
    p.ty = t.y + (Math.random() - 0.5) * jitter
    if (burst) {
      const a = Math.random() * Math.PI * 2, f = 3 + Math.random() * 7
      p.vx += Math.cos(a) * f; p.vy += Math.sin(a) * f
    }
  })
  if (reduced) { particles.forEach(p => { p.x = p.tx; p.y = p.ty; p.vx = p.vy = 0 }); draw(0) }
}

function scheduleMorph() {
  window.clearTimeout(morphTimer)
  if (reduced || props.shapes.length < 2) return
  morphTimer = window.setTimeout(async () => {
    if (visible && pageVisible) await morphTo((current.value + 1) % props.shapes.length)
    scheduleMorph()
  }, props.interval)
}

function setupParticles() {
  const count = Math.min(props.maxParticles, Math.max(300, Math.round((width * height) / props.density)))
  const old = particles
  particles = Array.from({ length: count }, (_, i) => old[i] ?? {
    x: Math.random() * width, y: Math.random() * height, vx: 0, vy: 0,
    tx: width / 2, ty: height / 2,
    size: 1 + Math.random() * 1.4,
    tone: Math.random() < 0.62 ? 0 : Math.random() < 0.7 ? 1 : 2,
    phase: Math.random() * Math.PI * 2,
  })
}

function resize() {
  const el = canvas.value
  if (!el) return
  // 用布局尺寸（clientWidth/Height），不用 getBoundingClientRect：
  // 后者会把进场动画、3D 倾斜、缩放等变换算进去，导致画布内部坐标和实际显示对不上，图形错位、变形。
  const w = el.clientWidth, h = el.clientHeight
  if (w < 2 || h < 2) return
  if (w === width && h === height && ctx) return
  dpr = Math.min(window.devicePixelRatio || 1, 1.5)
  width = w; height = h
  el.width = Math.round(width * dpr); el.height = Math.round(height * dpr)
  ctx = el.getContext('2d')
  ctx?.setTransform(dpr, 0, 0, dpr, 0, 0)
  targetCache.clear()
  setupParticles()
  void morphTo(current.value, false)
}

function draw(time: number) {
  if (!ctx) return
  ctx.clearRect(0, 0, width, height)
  const groups: Particle[][] = palette.map(() => [])
  for (const p of particles) (groups[p.tone] ?? groups[0]).push(p)
  groups.forEach((group, tone) => {
    ctx!.fillStyle = palette[tone] || palette[0]
    ctx!.globalAlpha = tone === 0 ? 0.92 : 0.85
    for (const p of group) {
      const twinkle = reduced ? 1 : 0.75 + 0.25 * Math.sin(time * 0.003 + p.phase)
      const s = p.size * twinkle
      ctx!.fillRect(p.x - s / 2, p.y - s / 2, s, s)
    }
  })
  ctx.globalAlpha = 1
}

function step(time: number) {
  frame = 0
  if (disposed || !visible || !pageVisible || reduced) return
  const radius = Math.min(width, height) * 0.16, r2 = radius * radius
  for (const p of particles) {
    // 目标点上加一点呼吸般的漂移
    const tx = p.tx + Math.sin(time * 0.0012 + p.phase) * 0.7
    const ty = p.ty + Math.cos(time * 0.0011 + p.phase) * 0.7
    p.vx += (tx - p.x) * 0.045
    p.vy += (ty - p.y) * 0.045
    if (pointer.active) {
      const dx = p.x - pointer.x, dy = p.y - pointer.y, d2 = dx * dx + dy * dy
      if (d2 < r2 && d2 > 0.01) {
        const d = Math.sqrt(d2), f = (1 - d / radius) * 2.6
        p.vx += (dx / d) * f; p.vy += (dy / d) * f
      }
    }
    p.vx *= 0.84; p.vy *= 0.84
    p.x += p.vx; p.y += p.vy
  }
  draw(time)
  frame = requestAnimationFrame(step)
}

function start() {
  if (!frame && visible && pageVisible && !reduced && !disposed) frame = requestAnimationFrame(step)
}
function stop() { if (frame) cancelAnimationFrame(frame); frame = 0 }

function onPointerMove(event: PointerEvent) {
  if (props.interactive === 'none' || !canvas.value) return
  // 鼠标坐标换算回画布坐标（画布被缩放或倾斜时按比例换算）
  const rect = canvas.value.getBoundingClientRect()
  if (!rect.width || !rect.height) return
  pointer = { x: (event.clientX - rect.left) * (width / rect.width), y: (event.clientY - rect.top) * (height / rect.height), active: true }
}
function onPointerLeave() { pointer.active = false }
function onVisibility() { pageVisible = !document.hidden; pageVisible ? start() : stop() }
function onMotionChange() {
  reduced = Boolean(motionQuery?.matches)
  if (reduced) { stop(); void morphTo(0, false) } else { start(); scheduleMorph() }
}

onMounted(() => {
  motionQuery = window.matchMedia('(prefers-reduced-motion: reduce)')
  reduced = motionQuery.matches
  motionQuery.addEventListener('change', onMotionChange)
  document.addEventListener('visibilitychange', onVisibility)
  resizeObserver = new ResizeObserver(() => resize())
  resizeObserver.observe(canvas.value!)
  intersection = new IntersectionObserver(entries => {
    visible = entries.some(e => e.isIntersecting)
    visible ? start() : stop()
  })
  intersection.observe(canvas.value!)
  if (props.interactive === 'parent') {
    pointerHost = canvas.value!.parentElement
    pointerHost?.addEventListener('pointermove', onPointerMove)
    pointerHost?.addEventListener('pointerleave', onPointerLeave)
  }
  readPalette()
  resize()
  start()
  scheduleMorph()
})

watch(() => props.shapes, () => { targetCache.clear(); current.value = 0; void morphTo(0) })

onBeforeUnmount(() => {
  disposed = true
  stop()
  window.clearTimeout(morphTimer)
  resizeObserver?.disconnect()
  intersection?.disconnect()
  motionQuery?.removeEventListener('change', onMotionChange)
  document.removeEventListener('visibilitychange', onVisibility)
  pointerHost?.removeEventListener('pointermove', onPointerMove)
  pointerHost?.removeEventListener('pointerleave', onPointerLeave)
})
</script>

<template>
  <canvas ref="canvas" class="particle-morph" :class="{ 'is-passive': interactive !== 'self' }" aria-hidden="true" @pointermove="onPointerMove" @pointerleave="onPointerLeave"></canvas>
</template>

<style>
/* :where 把优先级降到 0，调用处给的 class（定位、尺寸）总能覆盖这里的默认值。 */
:where(.particle-morph) { display: block; width: 100%; height: 100%; touch-action: pan-y; }
:where(.particle-morph.is-passive) { pointer-events: none; }
</style>
