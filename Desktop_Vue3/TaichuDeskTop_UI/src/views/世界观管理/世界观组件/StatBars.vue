<!-- 简单的单序列柱状图（几点创作、周几创作），悬停显示数值 -->
<template>
  <div class="bars">
    <div class="bars-plot" role="img" :aria-label="ariaLabel" @mouseleave="hover = -1">
      <div v-for="(v, i) in values" :key="i" class="bar-slot" :class="{ on: hover === i }" @mouseenter="hover = i">
        <span class="bar" :class="{ peak: v > 0 && v === max }" :style="{ height: max ? Math.max(v ? 3 : 0, (v / max) * 100) + '%' : '0' }"></span>
      </div>
      <div v-if="hover >= 0" class="bars-tip" :style="{ left: ((hover + 0.5) / values.length) * 100 + '%' }" role="tooltip">
        <b>{{ values[hover] }} 次</b><span>{{ labels[hover] }}</span>
      </div>
    </div>
    <div class="bars-axis" aria-hidden="true">
      <span v-for="(l, i) in labels" :key="i" :class="{ shown: tickEvery <= 1 || i % tickEvery === 0 }">{{ short ? short(i) : l }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'

const props = defineProps<{
  values: number[]
  /** 每根柱子的完整说明（悬停时显示） */
  labels: string[]
  /** 坐标轴上的短标签 */
  short?: (index: number) => string
  /** 每隔几根标一次坐标 */
  tickEvery?: number
  title: string
}>()
const hover = ref(-1)
const max = computed(() => Math.max(0, ...props.values))
const tickEvery = computed(() => props.tickEvery ?? 1)
const ariaLabel = computed(() => {
  if (!max.value) return `${props.title}：还没有数据`
  const peak = props.values.indexOf(max.value)
  return `${props.title}：最多的是${props.labels[peak]}，${max.value} 次`
})
</script>

<style scoped>
.bars { display: grid; grid-template-columns: minmax(0, 1fr); gap: 6px; min-width: 0; }
.bars-plot { position: relative; height: 96px; display: flex; align-items: flex-end; gap: 2px; border-bottom: 1px solid var(--border-line); }
.bar-slot { flex: 1 1 0; min-width: 0; height: 100%; display: flex; align-items: flex-end; cursor: default; }
.bar { display: block; width: 100%; border-radius: 4px 4px 0 0; background: color-mix(in srgb, var(--accent-color) 55%, var(--paper-card)); transition: background .15s; }
.bar.peak { background: var(--accent-color); }
.bar-slot.on .bar { background: var(--text-primary); }
.bars-axis { display: flex; gap: 2px; font-size: 10px; color: var(--text-secondary); }
.bars-axis span { flex: 1 1 0; min-width: 0; text-align: center; visibility: hidden; white-space: nowrap; overflow: visible; }
.bars-axis span.shown { visibility: visible; }
.bars-tip { position: absolute; bottom: calc(100% + 6px); transform: translateX(-50%); display: grid; gap: 2px; padding: 5px 9px; font-size: 12px; white-space: nowrap; pointer-events: none; background: var(--text-primary); color: var(--paper-card); z-index: 5; }
.bars-tip span { font-size: 11px; opacity: .8; }
</style>
