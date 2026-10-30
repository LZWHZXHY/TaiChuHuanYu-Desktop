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
  </svg>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useVisibleMotion } from '@/composables/useVisibleMotion'
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
.motion-paused * { animation-play-state: paused !important; }
@media (prefers-reduced-motion: no-preference) {
  .trace-flow { animation: trace-travel 5.2s linear infinite; }
  .trace-draw { stroke-dasharray: 100; animation: trace-reveal 1.1s ease-out both; }
  .trace-orbit { animation: trace-orbit 24s linear infinite; }
  .trace-plane { animation: trace-plane 12s ease-in-out infinite alternate; }
}
@keyframes trace-plane { to { transform: translate(12px, -8px); } }
@keyframes trace-orbit { to { transform: rotate(360deg); } }
@keyframes trace-travel { 0% { opacity: 0; stroke-dashoffset: 112; } 12%, 85% { opacity: .85; } 100% { opacity: 0; stroke-dashoffset: -100; } }
@keyframes trace-reveal { from { stroke-dashoffset: 100; opacity: 0; } to { stroke-dashoffset: 0; opacity: .4; } }
@media(max-width: 640px) { .section-trace { width: 48%; right: 0; opacity: .6; } }
</style>
