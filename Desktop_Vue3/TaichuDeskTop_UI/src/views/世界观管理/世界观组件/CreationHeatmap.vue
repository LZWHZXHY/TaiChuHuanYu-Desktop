<!-- 已改用公共组件 @/components/ActivityHeatmap.vue（世界观统计、个人主页共用），这个文件只是兼容旧引用 -->
<template>
  <ActivityHeatmap :days="days" :unit="unit" :verb="verb" :kind-labels="kindLabels" />
</template>

<script setup lang="ts">
import ActivityHeatmap from '@/components/ActivityHeatmap.vue'

defineProps<{ days: { d: string; n: number; k?: Record<string, number> }[]; unit?: string; verb?: string; kindLabels?: Record<string, string> }>()
</script>
