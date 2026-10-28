<template><div class="spatial"><button @click="toggleRotate">{{ autoRotate ? '暂停旋转' : '自动旋转' }}</button><div ref="container" class="scene" /></div></template>
<script setup lang="ts">
import { ref, toRef } from 'vue';
import type { WorldGraphNode, WorldGraphEdge } from '@/utils/worldGraph';
import { useWorldGraph } from '@/composables/世界观管理/useWorldGraph';
const props = defineProps<{
  data: {
    nodes: WorldGraphNode[];
    links: WorldGraphEdge[];
  };
}>();
const emit = defineEmits<{
  select: [
    id: string
  ];
}>();
const container = ref<HTMLElement | null>(null);
const { resetCamera, toggleRotate, autoRotate } = useWorldGraph(container, toRef(props, 'data'), id => emit('select', id));
defineExpose({ resetCamera });
</script>
<style scoped>
.spatial,.scene{position:absolute;inset:0}.spatial>button{position:absolute;top:12px;left:12px;z-index:2;padding:8px;border:1px solid var(--border-line);border-radius:6px;background:var(--paper-card);color:var(--text-primary);cursor:pointer}
.scene :deep(.world-graph-label){position:absolute;top:0;left:0;height:32px;box-sizing:border-box;pointer-events:auto;padding:4px 10px;border:1px solid;border-left-width:3px;border-radius:5px;background:var(--paper-card);color:var(--text-primary);font:600 14px/22px sans-serif;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;cursor:pointer}
</style>
