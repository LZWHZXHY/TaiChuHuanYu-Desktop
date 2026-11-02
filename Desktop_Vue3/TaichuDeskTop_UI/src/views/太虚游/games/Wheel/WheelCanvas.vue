<!-- 大转盘：用 canvas 画扇区和文字，转的时候只旋转整张画布（不重画），手机上也流畅。
     指针固定在正上方；转动时每经过一个扇区指针抖一下（有界面音效的话会「咔」一声）。
     用法：<WheelCanvas ref="wheel" :segments="segs" :size="440"><template #center>中间放按钮</template></WheelCanvas>
     然后 await wheel.value.spinTo(扇区序号) 等它停下。 -->
<template>
  <div ref="box" class="wheel-box" :style="{ maxWidth: `${size}px` }">
    <div class="wheel-stage">
      <canvas ref="cv" class="wheel-canvas" role="img" :aria-label="ariaLabel"></canvas>
      <svg ref="pointer" class="wheel-pointer" viewBox="0 0 40 54" aria-hidden="true">
        <path d="M20 52 4 14a17 17 0 1 1 32 0Z" fill="var(--accent-color)" stroke="#fff" stroke-width="2.5" stroke-linejoin="round" />
        <circle cx="20" cy="17" r="6" fill="#fff" />
      </svg>
      <div class="wheel-center"><slot name="center" /></div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { uiSound } from '@/utils/uiSound'
import { layoutArcs, textOn } from './wheelShared'

interface DrawSegment { text: string; color: string; weight: number }

const props = withDefaults(defineProps<{
  segments: DrawSegment[]
  /** 最大显示尺寸（像素），会随容器缩小 */
  size?: number
  /** 转完后高亮的扇区（其他扇区变暗），null 不高亮 */
  highlight?: number | null
  /** 经过扇区时要不要「咔」的声音 */
  sound?: boolean
}>(), { size: 440, highlight: null, sound: true })

const box = ref<HTMLDivElement | null>(null)
const cv = ref<HTMLCanvasElement | null>(null)
const pointer = ref<SVGSVGElement | null>(null)
const arcs = computed(() => layoutArcs(props.segments))
const ariaLabel = computed(() => `转盘，共 ${props.segments.length} 个扇区：${props.segments.map(s => s.text).join('、')}`)

const rad = (deg: number) => (deg * Math.PI) / 180
const FONT = '"PingFang SC", "Microsoft YaHei", system-ui, sans-serif'

/** 把文字截到放得下（放不下就加省略号） */
function fit(ctx: CanvasRenderingContext2D, text: string, maxWidth: number): string {
  if (ctx.measureText(text).width <= maxWidth) return text
  let t = text
  while (t.length > 1 && ctx.measureText(t + '…').width > maxWidth) t = t.slice(0, -1)
  return t + '…'
}

function draw() {
  const c = cv.value
  if (!c) return
  const css = Math.max(140, Math.min(props.size, box.value?.clientWidth || props.size))
  const dpr = Math.min(window.devicePixelRatio || 1, 3)
  c.width = Math.round(css * dpr)
  c.height = Math.round(css * dpr)
  c.style.width = `${css}px`
  c.style.height = `${css}px`
  const ctx = c.getContext('2d')
  if (!ctx) return
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0)
  ctx.clearRect(0, 0, css, css)
  const r = css / 2
  const rim = Math.max(8, css * 0.035)
  const R = r - rim

  // 外圈：深色边框 + 一圈小灯
  ctx.beginPath(); ctx.arc(r, r, r - 1, 0, Math.PI * 2); ctx.fillStyle = '#20252b'; ctx.fill()
  const bulbs = Math.max(12, Math.min(32, Math.round(css / 18)))
  for (let i = 0; i < bulbs; i++) {
    const a = (i / bulbs) * Math.PI * 2
    ctx.beginPath()
    ctx.arc(r + Math.cos(a) * (r - rim / 2), r + Math.sin(a) * (r - rim / 2), Math.max(1.6, rim * 0.22), 0, Math.PI * 2)
    ctx.fillStyle = i % 2 ? '#fde68a' : '#fbbf24'
    ctx.fill()
  }

  const segs = props.segments
  if (!segs.length) {
    ctx.beginPath(); ctx.arc(r, r, R, 0, Math.PI * 2); ctx.fillStyle = '#e5e7eb'; ctx.fill()
  }
  // 扇区
  arcs.value.forEach((a, i) => {
    const seg = segs[i]
    if (!seg) return
    ctx.beginPath()
    ctx.moveTo(r, r)
    ctx.arc(r, r, R, rad(a.start - 90), rad(a.end - 90))
    ctx.closePath()
    ctx.fillStyle = seg.color
    ctx.fill()
    if (props.highlight != null && props.highlight !== i) { ctx.fillStyle = 'rgba(10, 12, 16, .55)'; ctx.fill() }
  })
  // 分隔线
  if (segs.length > 1) {
    ctx.strokeStyle = 'rgba(255, 255, 255, .9)'
    ctx.lineWidth = Math.max(1, css / 300)
    arcs.value.forEach(a => {
      ctx.beginPath(); ctx.moveTo(r, r)
      ctx.lineTo(r + Math.cos(rad(a.start - 90)) * R, r + Math.sin(rad(a.start - 90)) * R)
      ctx.stroke()
    })
  }
  // 文字：从圆心往外，靠外圈右对齐
  const hub = R * 0.17
  arcs.value.forEach((a, i) => {
    const seg = segs[i]
    if (!seg) return
    const size = a.end - a.start
    const textR = R * 0.6
    const room = (Math.PI * 2 * textR * size) / 360 // 文字所在位置扇区有多宽
    const fs = Math.max(9, Math.min(R * 0.085, room * 0.5, 22))
    // 左半边的扇区把字翻过来，免得字是倒着的
    const flip = a.mid > 180
    ctx.save()
    ctx.translate(r, r)
    ctx.rotate(rad(a.mid - 90) + (flip ? Math.PI : 0))
    ctx.font = `600 ${fs}px ${FONT}`
    ctx.textAlign = flip ? 'left' : 'right'
    ctx.textBaseline = 'middle'
    const dim = props.highlight != null && props.highlight !== i
    ctx.fillStyle = dim ? 'rgba(255, 255, 255, .55)' : textOn(seg.color)
    const x = R - fs * 0.7
    ctx.fillText(fit(ctx, seg.text, R - hub - fs * 1.2), flip ? -x : x, 0)
    ctx.restore()
  })
  // 中心
  ctx.beginPath(); ctx.arc(r, r, hub, 0, Math.PI * 2); ctx.fillStyle = '#fff'; ctx.fill()
  ctx.lineWidth = Math.max(2, css / 110); ctx.strokeStyle = '#20252b'; ctx.stroke()
}

// ===== 转动 =====
let rotation = 0
let raf = 0
function setRotation(deg: number) {
  rotation = deg
  if (cv.value) cv.value.style.transform = `rotate(${deg}deg)`
}
/** 现在指针指着第几个扇区 */
function indexUnderPointer(): number {
  const theta = (((-rotation) % 360) + 360) % 360
  const i = arcs.value.findIndex(a => theta >= a.start && theta < a.end)
  return i < 0 ? arcs.value.length - 1 : i
}
function tick() {
  pointer.value?.animate?.([{ transform: 'rotate(0deg)' }, { transform: 'rotate(-18deg)' }, { transform: 'rotate(0deg)' }], { duration: 140, easing: 'ease-out' })
  if (props.sound) uiSound.hover()
}

/**
 * 转到第 index 个扇区并停下（停在扇区里随机的位置，不会每次都停正中间）。
 * 系统开了「减少动态效果」时转得短一些。
 */
function spinTo(index: number, opts: { duration?: number } = {}): Promise<void> {
  const a = arcs.value[index]
  if (!a) return Promise.resolve()
  cancelAnimationFrame(raf)
  const reduce = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false
  const span = a.end - a.start
  const land = a.start + span * (0.15 + Math.random() * 0.7)
  const turns = reduce ? 1 : 5 + Math.floor(Math.random() * 3)
  const from = rotation
  const extra = ((((-land - from) % 360) + 360) % 360)
  const to = from + turns * 360 + extra
  const duration = opts.duration ?? (reduce ? 900 : 4600 + Math.random() * 900)
  return new Promise(resolve => {
    const t0 = performance.now()
    let last = indexUnderPointer()
    const step = (now: number) => {
      const t = Math.min(1, (now - t0) / duration)
      const eased = 1 - Math.pow(1 - t, 4) // 先快后慢
      setRotation(from + (to - from) * eased)
      const under = indexUnderPointer()
      if (under !== last) { last = under; tick() }
      if (t < 1) raf = requestAnimationFrame(step)
      else { setRotation(((to % 360) + 360) % 360); resolve() }
    }
    raf = requestAnimationFrame(step)
  })
}

let ro: ResizeObserver | null = null
onMounted(() => {
  draw()
  if (typeof ResizeObserver !== 'undefined' && box.value) {
    ro = new ResizeObserver(() => draw())
    ro.observe(box.value)
  }
})
watch(() => [props.segments.map(s => `${s.text}|${s.color}|${s.weight}`).join('\n'), props.highlight, props.size], () => { void nextTick(draw) })
onBeforeUnmount(() => { cancelAnimationFrame(raf); ro?.disconnect() })

defineExpose({ spinTo, redraw: draw })
</script>

<style scoped>
.wheel-box { width: 100%; margin: 0 auto; }
.wheel-stage { position: relative; width: 100%; aspect-ratio: 1; display: grid; place-items: center; }
.wheel-canvas { display: block; border-radius: 50%; box-shadow: 0 14px 40px rgba(0, 0, 0, .28); will-change: transform; }
.wheel-pointer { position: absolute; top: -10px; left: 50%; width: clamp(28px, 9%, 44px); margin-left: calc(clamp(28px, 9%, 44px) / -2); transform-origin: 50% 30%; filter: drop-shadow(0 3px 4px rgba(0, 0, 0, .35)); pointer-events: none; z-index: 2; }
.wheel-center { position: absolute; inset: 0; display: grid; place-items: center; pointer-events: none; z-index: 1; }
.wheel-center > :deep(*) { pointer-events: auto; }
</style>
