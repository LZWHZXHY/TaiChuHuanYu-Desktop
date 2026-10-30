<template>
  <svg ref="trace" class="section-trace" :class="{ 'motion-paused': !motionActive }" viewBox="0 0 400 104" fill="none" aria-hidden="true" focusable="false">
    <path class="trace-plane" d="M260 0H372L292 80H180Z" fill="var(--signal-mint)" opacity=".1" />
    <g class="trace-orbit" stroke="var(--signal-mint)" opacity=".42"><circle cx="340" cy="52" r="45" stroke-dasharray="58 16 5 40" /><circle cx="340" cy="7" r="2.5" fill="var(--signal-mint)" stroke="none" /></g>
    <g class="trace-guide" stroke="currentColor">
      <path d="M12 80H238L294 24H388M100 96H254L310 40H388M204 8H368V88" />
      <path d="M20 16H60M40 8V24M340 72H380M360 64V80" />
      <circle cx="294" cy="24" r="4" />
    </g>
    <path class="trace-flow" d="M12 80H238L294 24H388" stroke="currentColor" stroke-width="2" pathLength="100" />
    <path class="trace-draw" d="M100 96H254L310 40H388" stroke="var(--signal-mint)" pathLength="100" />
    <path d="M96 12H208L252 56H396" stroke="var(--signal-mint)" opacity=".28" />
    <path class="trace-return" d="M96 12H208L252 56H396" stroke="var(--signal-mint)" stroke-width="2" stroke-linecap="square" pathLength="100" stroke-dasharray="10 40" />
    <g class="trace-kite" stroke="var(--signal-mint)" stroke-width="1.5"><path d="M210 18 230 38 210 58 190 38Z" fill="var(--signal-mint)" fill-opacity=".14" /><path d="M210 28 220 38 210 48 200 38Z" /></g>
    <g class="trace-blocks" fill="currentColor"><path d="M50 68H74L86 56H62Z" opacity=".65" /><rect x="82" y="68" width="8" height="8" opacity=".4" /></g>
  </svg>
  <!-- 可选的粒子徽记：传入 particles="gallery" 等预设名即可，在页头右侧变形，不拦截点击。 -->
  <ParticleMorph v-if="shapes" class="section-trace section-particles" :shapes="shapes" :colors="['--accent-color', '--signal-mint', '--signal-color']" :interval="5600" :density="46" interactive="parent" />
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import ParticleMorph from '@/components/ParticleMorph.vue'
import { SECTION_PARTICLES } from '@/utils/particleShapes'
import { useVisibleMotion } from '@/composables/useVisibleMotion'
const props = defineProps<{ particles?: string }>()
const shapes = computed(() => props.particles ? SECTION_PARTICLES[props.particles] : undefined)
const trace = ref<SVGElement>()
const motionActive = useVisibleMotion(trace)
</script>

<style scoped>
/* 装饰固定在页头内，不截获点击，不驱动正文排版。 */
.section-trace { position: absolute; right: 12px; top: 0; width: min(42%, 400px); height: 100%; max-height: 170px; color: var(--surface-color, var(--accent-color)); pointer-events: none; overflow: hidden; z-index: 0; }
.trace-guide { opacity: .24; }
.trace-flow { opacity: 0; stroke-dasharray: 12 100; stroke-dashoffset: 112; }
.trace-draw { opacity: .4; }
.trace-orbit { transform-origin: 340px 52px; }
.trace-kite { transform-origin: 210px 38px; opacity: .65; }
.motion-paused * { animation-play-state: paused !important; }
@media (prefers-reduced-motion: no-preference) {
  .trace-guide { animation: trace-breathe 7s ease-in-out infinite alternate; }
  .trace-flow { animation: trace-travel 5.2s linear infinite; }
  .trace-draw { stroke-dasharray: 100; animation: trace-reveal 1.1s ease-out both; }
  .trace-orbit { animation: trace-orbit 24s linear infinite; }
  .trace-plane { animation: trace-plane 12s ease-in-out infinite alternate; }
  .trace-return { animation: trace-return 3s linear infinite; }
  .trace-kite { animation: trace-kite 6s ease-in-out infinite alternate; }
  .trace-blocks { animation: trace-blocks 3.5s ease-in-out infinite alternate; }
}
@keyframes trace-return { to { stroke-dashoffset: 100; } }
@keyframes trace-breathe { from { opacity: .16; } to { opacity: .44; } }
@keyframes trace-kite { to { transform: translate(24px,-8px) rotate(45deg); } }
@keyframes trace-blocks { to { transform: translate(38px,0); } }
@keyframes trace-plane { to { transform: translate(12px, -8px); } }
@keyframes trace-orbit { to { transform: rotate(360deg); } }
@keyframes trace-travel { 0% { opacity: 0; stroke-dashoffset: 112; } 12%, 85% { opacity: .85; } 100% { opacity: 0; stroke-dashoffset: -100; } }
@keyframes trace-reveal { from { stroke-dashoffset: 100; opacity: 0; } to { stroke-dashoffset: 0; opacity: .4; } }
@media(max-width: 640px) { .section-trace { width: 48%; right: 0; opacity: .6; } }
/* 粒子徽记叠在线条装饰的右侧，与之共用 .section-trace 的层级规则。 */
.section-trace.section-particles { left: auto; right: min(26%, 250px); top: 50%; width: min(16%, 150px); height: min(76%, 120px); max-height: none; transform: translateY(-50%); opacity: .9; }
@media(max-width: 720px) { .section-trace.section-particles { display: none; } }
</style>
