<!-- 颜色图例：时间线区块、编年史时间轴旁边显示「哪种颜色代表什么」。
     点一种颜色只看这一类事件，再点一次（或点「全部」）恢复。没有定义过含义的颜色不显示。 -->
<template>
  <div v-if="entries.length" class="clg" role="group" :aria-label="hint ? `颜色图例：${hint}` : '颜色图例'" :title="hint || undefined">
    <span class="clg-title" aria-hidden="true">图例</span>
    <button v-for="e in entries" :key="e.key" type="button" class="clg-item" :aria-pressed="active === e.key"
      :title="active === e.key ? '再点一次显示全部事件' : `只看「${e.label}」（${e.name}色）的事件`" @click="emit('update:active', active === e.key ? null : e.key)">
      <span class="clg-swatch" :style="{ background: e.swatch }" aria-hidden="true"></span>
      <span class="clg-label">{{ e.label }}</span>
    </button>
    <button v-if="active" type="button" class="clg-all" @click="emit('update:active', null)">全部</button>
  </div>
</template>

<script setup lang="ts">
import type { LegendEntry } from '@/utils/worldLegend'

defineProps<{ entries: LegendEntry[]; active?: string | null; hint?: string }>()
const emit = defineEmits<{ 'update:active': [key: string | null] }>()
</script>

<style scoped>
.clg { display: flex; flex-wrap: wrap; align-items: center; gap: 4px 6px; font-size: 12px; color: var(--text-secondary); }
.clg-title { margin-right: 2px; }
.clg-item, .clg-all { display: inline-flex; align-items: center; gap: 5px; max-width: 100%; padding: 2px 8px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.clg-item:hover, .clg-all:hover { border-color: var(--accent-color); }
.clg-item[aria-pressed=true] { border-color: var(--accent-color); background: var(--accent-soft); font-weight: 600; }
.clg-all { color: var(--accent-color); border-style: dashed; }
.clg-swatch { flex-shrink: 0; width: 10px; height: 10px; }
.clg-label { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.clg-item:focus-visible, .clg-all:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 1px; }
</style>
