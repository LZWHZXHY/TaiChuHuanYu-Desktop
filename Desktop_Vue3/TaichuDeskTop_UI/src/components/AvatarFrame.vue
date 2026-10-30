<!-- AvatarFrame.vue：给头像套上头像框（样式见 utils/avatarFrames.ts）。头像本身放在默认插槽里，框画在外面，不改变头像大小。
     两种框：CSS 画的（af-xxx），和后台上传的图片框（img-xxx，按 scale 居中放大套在头像外面，跟着头像大小缩放）。
     用法：传 frame 直接指定；或者只传 user-id，自动读取这个人佩戴的头像框（全站合并请求、有缓存）。
     size="sm" 给 40px 以下的小头像用（评论、聊天、榜单），框会相应变细变小。 -->
<template>
  <span class="avatar-frame" :class="[current && !image ? `af-${current}` : current ? 'af-image' : 'af-none', size === 'sm' ? 'af-sm' : '']" :title="title">
    <slot />
    <img v-if="image" class="af-img" :src="image.url" alt="" aria-hidden="true" draggable="false" :style="{ '--af-scale': String(image.scale || 1.4) }" />
    <template v-else-if="current && !isImageFrame(current)">
      <i class="af-layer af-a" aria-hidden="true"></i>
      <i class="af-layer af-b" aria-hidden="true"></i>
    </template>
  </span>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { frameName, imageFrameOf, isImageFrame } from '@/utils/avatarFrames'
import { appearanceOf } from '@/utils/appearanceCache'

const props = defineProps<{ frame?: string | null; userId?: string | number | null; size?: 'sm' | 'md' }>()
// 传了 frame（哪怕是 null）就用它；否则按 userId 去查
const current = computed(() => props.frame !== undefined ? props.frame : appearanceOf(props.userId)?.frame ?? null)
const image = computed(() => imageFrameOf(current.value))
const title = computed(() => (current.value ? `头像框：${frameName(current.value)}` : undefined))
</script>

<style scoped>
.avatar-frame { --afu: 1px; position: relative; display: inline-grid; isolation: isolate; flex-shrink: 0; }
/* 小头像：框按比例缩小 */
.avatar-frame.af-sm { --afu: .55px; }
.af-layer { position: absolute; pointer-events: none; z-index: 2; }
/* 图片框：以头像中心为准放大 scale 倍 */
.af-img { position: absolute; left: 50%; top: 50%; z-index: 2; width: calc(100% * var(--af-scale, 1.4)); height: calc(100% * var(--af-scale, 1.4)); max-width: none; transform: translate(-50%, -50%); object-fit: contain; pointer-events: none; user-select: none; }

/* 莱茵标识：双线框 + 四角定位标 */
.af-rhine-ring .af-a { inset: calc(-5 * var(--afu)); border: calc(2 * var(--afu)) solid #1fb5d6; box-shadow: inset 0 0 0 calc(2 * var(--afu)) var(--paper-card, #fff), inset 0 0 0 calc(3 * var(--afu)) #1fb5d6; }
.af-rhine-ring .af-b { inset: calc(-11 * var(--afu)); background:
  linear-gradient(#1fb5d6, #1fb5d6) 0 0 / calc(12 * var(--afu)) calc(2 * var(--afu)), linear-gradient(#1fb5d6, #1fb5d6) 0 0 / calc(2 * var(--afu)) calc(12 * var(--afu)),
  linear-gradient(#1fb5d6, #1fb5d6) 100% 0 / calc(12 * var(--afu)) calc(2 * var(--afu)), linear-gradient(#1fb5d6, #1fb5d6) 100% 0 / calc(2 * var(--afu)) calc(12 * var(--afu)),
  linear-gradient(#1fb5d6, #1fb5d6) 0 100% / calc(12 * var(--afu)) calc(2 * var(--afu)), linear-gradient(#1fb5d6, #1fb5d6) 0 100% / calc(2 * var(--afu)) calc(12 * var(--afu)),
  linear-gradient(#1fb5d6, #1fb5d6) 100% 100% / calc(12 * var(--afu)) calc(2 * var(--afu)), linear-gradient(#1fb5d6, #1fb5d6) 100% 100% / calc(2 * var(--afu)) calc(12 * var(--afu)); background-repeat: no-repeat; }

/* 警示条纹 */
.af-hazard .af-a { inset: calc(-7 * var(--afu)); z-index: -1; background: repeating-linear-gradient(135deg, #f5b301 0 calc(7 * var(--afu)), #16181b calc(7 * var(--afu)) calc(14 * var(--afu))); }
.af-hazard .af-b { inset: calc(-2 * var(--afu)); border: calc(2 * var(--afu)) solid #16181b; }

/* 电路节点 */
.af-circuit .af-a { inset: calc(-6 * var(--afu)); border: calc(2 * var(--afu)) solid #2dd4bf; clip-path: polygon(0 0, 40% 0, 40% calc(3 * var(--afu)), 60% calc(3 * var(--afu)), 60% 0, 100% 0, 100% 100%, 0 100%); }
.af-circuit .af-b { inset: calc(-10 * var(--afu)); background:
  radial-gradient(circle, #2dd4bf 0 calc(3 * var(--afu)), transparent calc(4 * var(--afu))) 0 0 / calc(10 * var(--afu)) calc(10 * var(--afu)) no-repeat,
  radial-gradient(circle, #2dd4bf 0 calc(3 * var(--afu)), transparent calc(4 * var(--afu))) 100% 100% / calc(10 * var(--afu)) calc(10 * var(--afu)) no-repeat,
  radial-gradient(circle, #a7f3d0 0 calc(2 * var(--afu)), transparent calc(3 * var(--afu))) 100% 0 / calc(8 * var(--afu)) calc(8 * var(--afu)) no-repeat; animation: af-blink 1.6s steps(2) infinite; }
@keyframes af-blink { 50% { opacity: .35; } }

/* 扫描中 */
.af-scan .af-a { inset: calc(-4 * var(--afu)); border: calc(2 * var(--afu)) solid #22d3ee; }
.af-scan .af-b { inset: 0; overflow: hidden; background: linear-gradient(180deg, transparent 0 42%, rgb(34 211 238 / 55%) 50%, transparent 58% 100%) 0 0 / 100% 220%; animation: af-scan 2.4s linear infinite; mix-blend-mode: screen; }
@keyframes af-scan { from { background-position: 0 100%; } to { background-position: 0 -120%; } }

/* 金辉 */
.af-gold .af-a { inset: calc(-6 * var(--afu)); z-index: -1; background: linear-gradient(120deg, #8a5a00, #f8d36a, #fff4c2, #d49a1f, #8a5a00) 0 0 / 300% 300%; animation: af-flow 4s ease-in-out infinite alternate; box-shadow: 0 0 calc(10 * var(--afu)) rgb(248 211 106 / 45%); }
@keyframes af-flow { to { background-position: 100% 100%; } }

/* 樱色 */
.af-sakura .af-a { inset: calc(-6 * var(--afu)); z-index: -1; background: linear-gradient(135deg, #fbcfe8, #f472b6 55%, #fde2f3); }
.af-sakura .af-b { right: calc(-12 * var(--afu)); top: calc(-12 * var(--afu)); width: calc(22 * var(--afu)); height: calc(22 * var(--afu)); background:
  radial-gradient(circle at 50% 50%, #fff 0 calc(2 * var(--afu)), transparent calc(3 * var(--afu))),
  conic-gradient(from 0deg, #f9a8d4 0 15%, transparent 15% 20%, #f9a8d4 20% 35%, transparent 35% 40%, #f9a8d4 40% 55%, transparent 55% 60%, #f9a8d4 60% 75%, transparent 75% 80%, #f9a8d4 80% 95%, transparent 95%);
  border-radius: 50%; filter: drop-shadow(0 calc(1 * var(--afu)) calc(1 * var(--afu)) rgb(0 0 0 / 25%)); }

/* 源石结晶 */
.af-originium .af-a { inset: calc(-4 * var(--afu)); border: calc(2 * var(--afu)) solid #ea580c; }
.af-originium .af-b { inset: calc(-14 * var(--afu)); background:
  conic-gradient(from 200deg at 0 0, transparent 0 0, #fb923c 0 35deg, transparent 35deg) 0 0 / calc(22 * var(--afu)) calc(22 * var(--afu)) no-repeat,
  conic-gradient(from 20deg at 100% 100%, #ef4444 0 35deg, transparent 35deg) 100% 100% / calc(22 * var(--afu)) calc(22 * var(--afu)) no-repeat,
  linear-gradient(135deg, transparent 45%, #fdba74 46% 54%, transparent 55%) 100% 0 / calc(16 * var(--afu)) calc(16 * var(--afu)) no-repeat,
  linear-gradient(45deg, transparent 45%, #f97316 46% 54%, transparent 55%) 0 100% / calc(16 * var(--afu)) calc(16 * var(--afu)) no-repeat; filter: drop-shadow(0 0 calc(3 * var(--afu)) rgb(249 115 22 / 60%)); }

/* 深空 */
.af-void .af-a { inset: calc(-9 * var(--afu)); border-radius: 50%; border: calc(2 * var(--afu)) dashed #a78bfa; animation: af-spin 9s linear infinite; }
.af-void .af-b { inset: calc(-4 * var(--afu)); border: calc(2 * var(--afu)) solid #6d28d9; box-shadow: 0 0 calc(12 * var(--afu)) rgb(139 92 246 / 55%); }
@keyframes af-spin { to { transform: rotate(360deg); } }

@media (prefers-reduced-motion: reduce) { .af-layer { animation: none !important; } }
</style>
