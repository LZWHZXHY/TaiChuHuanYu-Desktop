<template>
  <div class="workspace-canvas-frame">
    <header class="canvas-header">
      <input 
        :value="props.title" 
        @input="onTitleInput" 
        class="canvas-title-input" 
        placeholder="未命名图谱 / Canvas Title" 
      />
      <p class="canvas-subtitle">多维交织的灵脉思绪星图</p>
    </header>

    <div class="canvas-container">
      <SpiritCanvas 
        :note-id="props.noteId" 
        :blocks="props.blocks" 
        @open-editor="handleNodeDoubleClick"
        @canvas-change="handleCanvasDataChange"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import SpiritCanvas from '@/components/SpiritCanvas.vue';

const props = defineProps<{
  title: string;
  noteId: string;
  blocks?: any[]; // 🌟 新增：接收从数据库里拉取出来的坐标数据
}>();

const emit = defineEmits(['update:title', 'change', 'open-sub-drawer']);

const onTitleInput = (e: Event) => {
  const target = e.target as HTMLInputElement;
  emit('update:title', target.value);
};

const handleCanvasDataChange = (flowNodes: any[]) => {
  const blocks = flowNodes.map((node, idx) => {
    const canvasNodeData = {
      attrs: {
        id: node.id,
        refNoteId: node.id, 
        x: node.position.x,
        y: node.position.y
      }
    };

    return {
      id: `canvas_node_${node.id}`, // 保持稳定ID，不加时间戳
      ownerId: props.noteId,
      ownerType: 'canvas', 
      type: 'canvas-node', 
      data: JSON.stringify(canvasNodeData),
      sortOrder: idx
    };
  });

  emit('change', { blocks });
};

const handleNodeDoubleClick = (targetNoteId: string) => {
  emit('open-sub-drawer', targetNoteId);
};
</script>

<style scoped>
/* 原有样式保持不变 */
.workspace-canvas-frame { width: 100%; height: 100%; display: flex; flex-direction: column; background: #fbfbfd; }
.canvas-header { padding: 30px 40px 10px; background: #ffffff; border-bottom: 1px solid #f2f2f7; }
.canvas-title-input { width: 100%; font-size: 2.2rem; font-weight: 700; border: none; background: transparent; outline: none; }
.canvas-subtitle { font-size: 12px; color: #a1a1a6; margin-top: 4px; }
.canvas-container { flex: 1; position: relative; width: 100%; }
</style>