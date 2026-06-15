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
  blocks?: any[]; // 接收从数据库里拉取出来的坐标数据
}>();

const emit = defineEmits(['update:title', 'change', 'open-sub-drawer']);

const onTitleInput = (e: Event) => {
  const target = e.target as HTMLInputElement;
  emit('update:title', target.value);
};

// 🌟 核心修复：接收由 { nodes, edges } 组成的对象 payload
const handleCanvasDataChange = (payload: { nodes: any[], edges: any[] }) => {
  const { nodes, edges } = payload;

  // 1. 处理节点数据
  const nodeBlocks = nodes.map((node, idx) => {
    const canvasNodeData = {
      attrs: {
        id: node.id,
        refNoteId: node.id, 
        x: node.position.x,
        y: node.position.y
      }
    };

    return {
      id: `canvas_node_${node.id}`, 
      ownerId: props.noteId,
      ownerType: 'canvas', 
      type: 'canvas-node', 
      data: JSON.stringify(canvasNodeData),
      sortOrder: idx
    };
  });

  // 2. 处理连线数据（只过滤出用户手动连的线，排除自动生成的线）
  const manualEdges = edges.filter(e => e.id.startsWith('manual-'));
  
  // 🌟 在 WorkspaceCanvas.vue 的 handleCanvasDataChange 函数中：
const edgeBlocks = manualEdges.map((edge, idx) => ({
    id: `canvas_edge_${edge.source}_${edge.target}`,
    ownerId: props.noteId,
    ownerType: 'canvas',
    type: 'canvas-edge', 
    data: JSON.stringify({ 
      source: edge.source, 
      target: edge.target,
      
      // 👇 必须补上这两行！否则刷新后数据库不知道线连在了哪个方向！
      sourceHandle: edge.sourceHandle, 
      targetHandle: edge.targetHandle, 
      
      style: edge.style,   
      type: edge.type,     
      label: edge.label    
    }),
    sortOrder: nodeBlocks.length + idx
}));

  // 3. 将节点和线合并后抛给外层存储
  emit('change', { blocks: [...nodeBlocks, ...edgeBlocks] });
};

const handleNodeDoubleClick = (targetNoteId: string) => {
  emit('open-sub-drawer', targetNoteId);
};
</script>

<style scoped>
.workspace-canvas-frame { width: 100%; height: 100%; display: flex; flex-direction: column; background: #fbfbfd; }
.canvas-header { padding: 30px 40px 10px; background: #ffffff; border-bottom: 1px solid #f2f2f7; }
.canvas-title-input { width: 100%; font-size: 2.2rem; font-weight: 700; border: none; background: transparent; outline: none; }
.canvas-subtitle { font-size: 12px; color: #a1a1a6; margin-top: 4px; }
.canvas-container { flex: 1; position: relative; width: 100%; }
</style>