<!-- 用户自定义网站背景：图片 / 视频 → 遮罩（跟随白天、夜间的底色）→ 可选坐标纸网格。氛围线条和粒子在更上层，由 App.vue 绘制。 -->
<template>
  <div v-if="bg" class="app-backdrop" aria-hidden="true">
    <video
      v-if="bg.kind === 'video'"
      ref="videoEl"
      :key="bg.src"
      class="backdrop-media backdrop-video"
      :src="bg.src"
      :poster="bg.poster"
      :style="bg.video"
      :autoplay="!reducedMotion"
      muted
      loop
      playsinline
      disablepictureinpicture
      preload="auto"
      tabindex="-1"
    ></video>
    <div v-else class="backdrop-media backdrop-image" :style="bg.image"></div>
    <div class="backdrop-veil" :class="{ 'with-grid': bg.grid }" :style="bg.veil"></div>
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useSiteBackground } from '@/composables/useSiteBackground'

const bg = useSiteBackground().style
const videoEl = ref<HTMLVideoElement | null>(null)
const motionQuery = typeof window !== 'undefined' && window.matchMedia ? window.matchMedia('(prefers-reduced-motion: reduce)') : null
const reducedMotion = ref(Boolean(motionQuery?.matches))

/** 页面切到后台时暂停视频省电；系统开启「减少动态效果」时只显示封面帧。 */
function sync() {
  const v = videoEl.value
  if (!v) return
  // muted 属性在部分浏览器里不会同步到 DOM 属性上，手动再设一次，保证可以自动播放
  v.muted = true
  if (document.hidden || reducedMotion.value) v.pause()
  else void v.play().catch(() => { /* 浏览器拒绝自动播放时保持封面帧 */ })
}
const onMotionChange = (e: MediaQueryListEvent) => { reducedMotion.value = e.matches; sync() }

watch(videoEl, () => sync(), { flush: 'post' })
onMounted(() => {
  document.addEventListener('visibilitychange', sync)
  motionQuery?.addEventListener?.('change', onMotionChange)
})
onBeforeUnmount(() => {
  document.removeEventListener('visibilitychange', sync)
  motionQuery?.removeEventListener?.('change', onMotionChange)
})
</script>

<style scoped>
.app-backdrop { position: absolute; inset: 0; z-index: 0; overflow: hidden; pointer-events: none; }
.backdrop-media { position: absolute; inset: 0; }
.backdrop-image { background-size: cover; background-repeat: no-repeat; }
.backdrop-video { display: block; width: 100%; height: 100%; object-fit: cover; }
.backdrop-veil { position: absolute; inset: 0; background-color: color-mix(in srgb, var(--bg-main) var(--bg-veil, 55%), transparent); }
.backdrop-veil.with-grid {
  background-image:
    linear-gradient(var(--lab-grid-major) 1px, transparent 1px),
    linear-gradient(90deg, var(--lab-grid-major) 1px, transparent 1px),
    linear-gradient(var(--lab-grid-minor) 1px, transparent 1px),
    linear-gradient(90deg, var(--lab-grid-minor) 1px, transparent 1px);
  background-size: 40px 40px, 40px 40px, 8px 8px, 8px 8px;
}
</style>
