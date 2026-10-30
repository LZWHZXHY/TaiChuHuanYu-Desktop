<script setup lang="ts">
// 点击迸发：点主要按钮、点赞、收藏、关注时，从点击位置迸出一小簇方形粒子。
// 全局只挂一个透明画布，不拦截点击；只在有粒子时才运行动画；「减少动态效果」时不显示。
import { onBeforeUnmount, onMounted, ref } from 'vue'

const TRIGGERS = [
  '[data-burst]', '.btn-primary', '.hero-primary', '.btn-save', '.create-btn', 'button[type=submit]',
  '.interact-actions button', '.like-btn', '.favorite-btn', '.follow-btn', '.vote-btn',
].join(',')
const COLORS = ['--signal-mint', '--accent-color', '--signal-color']

interface Spark { x: number; y: number; vx: number; vy: number; life: number; max: number; size: number; color: string }

const canvas = ref<HTMLCanvasElement>()
let ctx: CanvasRenderingContext2D | null = null
let sparks: Spark[] = []
let frame = 0, dpr = 1
let reduced: MediaQueryList | undefined

function fit() {
  const el = canvas.value
  if (!el) return
  dpr = Math.min(window.devicePixelRatio || 1, 2)
  el.width = Math.round(window.innerWidth * dpr); el.height = Math.round(window.innerHeight * dpr)
  ctx = el.getContext('2d')
  ctx?.setTransform(dpr, 0, 0, dpr, 0, 0)
}

function burst(x: number, y: number, big: boolean) {
  const style = getComputedStyle(document.documentElement)
  const colors = COLORS.map(c => style.getPropertyValue(c).trim() || '#3fe0c5')
  const count = big ? 40 : 26
  for (let i = 0; i < count; i++) {
    const a = Math.random() * Math.PI * 2, speed = (big ? 3 : 2.2) + Math.random() * (big ? 5.5 : 4)
    const max = 38 + Math.random() * 28
    sparks.push({ x, y, vx: Math.cos(a) * speed, vy: Math.sin(a) * speed - 1.2, life: max, max, size: 2.5 + Math.random() * 3, color: colors[i % colors.length] })
  }
  if (!frame) frame = requestAnimationFrame(tick)
}

function tick() {
  frame = 0
  if (!ctx) return
  ctx.clearRect(0, 0, window.innerWidth, window.innerHeight)
  sparks = sparks.filter(s => s.life > 0)
  for (const s of sparks) {
    s.vx *= 0.94; s.vy = s.vy * 0.94 + 0.12
    s.x += s.vx; s.y += s.vy; s.life--
    const t = s.life / s.max
    ctx.globalAlpha = t
    ctx.fillStyle = s.color
    const size = s.size * (0.4 + t * 0.6)
    ctx.fillRect(s.x - size / 2, s.y - size / 2, size, size)
  }
  ctx.globalAlpha = 1
  if (sparks.length) frame = requestAnimationFrame(tick)
}

function onClick(event: MouseEvent) {
  if (reduced?.matches) return
  const target = (event.target as Element | null)?.closest?.(TRIGGERS) as HTMLElement | null
  if (!target || (target as HTMLButtonElement).disabled) return
  // 键盘触发的点击没有坐标，从元素中心迸发。
  let x = event.clientX, y = event.clientY
  if (!x && !y) { const r = target.getBoundingClientRect(); x = r.left + r.width / 2; y = r.top + r.height / 2 }
  burst(x, y, target.matches('.interact-actions button, .like-btn, .favorite-btn, .vote-btn, [data-burst="big"]'))
}

onMounted(() => {
  reduced = window.matchMedia('(prefers-reduced-motion: reduce)')
  fit()
  window.addEventListener('resize', fit)
  document.addEventListener('click', onClick, true)
})
onBeforeUnmount(() => {
  window.removeEventListener('resize', fit)
  document.removeEventListener('click', onClick, true)
  if (frame) cancelAnimationFrame(frame)
})
</script>

<template>
  <canvas ref="canvas" class="particle-burst" aria-hidden="true"></canvas>
</template>

<style>
.particle-burst { position: fixed; inset: 0; width: 100vw; height: 100vh; pointer-events: none; z-index: 5000; }
</style>
