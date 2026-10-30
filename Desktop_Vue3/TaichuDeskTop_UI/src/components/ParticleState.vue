<script setup lang="ts">
// 加载中 / 空状态 / 出错时的粒子插图：粒子在几个图形之间变形，下面是说明文字。
// 用法：<ParticleState text="正在加载…" />、<ParticleState kind="empty" preset="forum" text="还没有帖子" />
import { computed } from 'vue'
import ParticleMorph from '@/components/ParticleMorph.vue'
import { SECTION_PARTICLES } from '@/utils/particleShapes'

const props = withDefaults(defineProps<{
  kind?: 'loading' | 'empty' | 'error'
  /** 空状态时用哪个板块的图形，见 SECTION_PARTICLES */
  preset?: string
  text?: string
  compact?: boolean
}>(), { kind: 'loading', preset: '', text: '', compact: false })

const shapes = computed(() => {
  if (props.kind === 'loading') return SECTION_PARTICLES.loading
  if (props.kind === 'error') return SECTION_PARTICLES.error
  return SECTION_PARTICLES[props.preset] ?? SECTION_PARTICLES.empty
})
const interval = computed(() => props.kind === 'loading' ? 1500 : 4200)
</script>

<template>
  <div class="particle-state" :class="['is-' + kind, { compact }]" :role="kind === 'error' ? 'alert' : 'status'">
    <ParticleMorph class="ps-canvas" :shapes="shapes" :interval="interval" :density="34" :colors="['--accent-color', '--signal-mint', '--signal-color']" />
    <p v-if="text" class="ps-text">{{ text }}</p>
    <slot />
  </div>
</template>

<style scoped>
.particle-state { display: grid; justify-items: center; gap: 8px; padding: 20px 12px; text-align: center; color: var(--text-secondary); }
.ps-canvas { width: 150px !important; height: 104px !important; }
.compact .ps-canvas { width: 96px !important; height: 68px !important; }
.compact { padding: 10px 8px; gap: 4px; }
.ps-text { margin: 0; font-size: 13px; }
.is-loading .ps-text::after { content: ''; display: inline-block; width: 1.2em; text-align: left; animation: ps-dots 1.2s steps(4) infinite; }
@keyframes ps-dots { 0% { content: ''; } 25% { content: '.'; } 50% { content: '..'; } 75% { content: '...'; } }
@media (prefers-reduced-motion: reduce) { .is-loading .ps-text::after { animation: none; } }
</style>
