<template>
  <div class="spirit-canvas-wrapper">
    <VueFlow
      v-model:nodes="flowNodes"
      v-model:edges="flowEdges"
      @nodeDragStop="onNodeDragStop"
      @nodeDoubleClick="onNodeDoubleClick"
      @connect="onConnect"
      @edgeClick="onEdgeClick" 
      @paneClick="onPaneClick"
      :default-zoom="1"
      :min-zoom="0.2"
      :max-zoom="4"
      fit-view-on-init
    >
      <Background pattern-color="#c7c7cc" :gap="24" :size="2" />
      <Controls position="bottom-right" />

      <!-- 🌟 核心升级：自定义节点模板，支持四个方向连线 & 动态万能块渲染 -->
      <template #node-custom="nodeProps">
        <div :class="['spirit-node-card', `node-type-${nodeProps.data.type}`]">
          <!-- 四个方向的连接点 (Handle) -->
         <Handle id="top" type="source" :position="Position.Top" class="custom-handle" />
          <Handle id="top" type="target" :position="Position.Top" class="custom-handle" />
          
          <Handle id="right" type="source" :position="Position.Right" class="custom-handle" />
          <Handle id="right" type="target" :position="Position.Right" class="custom-handle" />
          
          <Handle id="bottom" type="source" :position="Position.Bottom" class="custom-handle" />
          <Handle id="bottom" type="target" :position="Position.Bottom" class="custom-handle" />
          
          <Handle id="left" type="source" :position="Position.Left" class="custom-handle" />
          <Handle id="left" type="target" :position="Position.Left" class="custom-handle" />

          <!-- 节点头部：标题与图标 -->
          <header class="node-header">
            <span class="node-icon">{{ nodeProps.data.type === 'folder' ? '📁' : '📄' }}</span>
            <span class="node-title">{{ nodeProps.label }}</span>
          </header>

          <!-- 节点内容区：循环渲染灵脉万能块 -->
          <div class="node-blocks-area">
            <!-- 过滤掉画板自身的坐标块，仅渲染真正的内容块 -->
            <template v-if="nodeProps.data.blocks && nodeProps.data.blocks.length">
              <SpiritBlock 
                v-for="block in nodeProps.data.blocks.filter((b: any) => b.type !== 'canvas-node' && b.type !== 'canvas-edge')" 
                :key="block.id" 
                :block="block" 
              />
            </template>
            <div v-else class="node-empty-hint">双击注入灵魂...</div>
          </div>
        </div>
      </template>
    </VueFlow>

    <!-- 连线编辑器面板 -->
    <transition name="fade">
      <div v-if="selectedEdge" class="edge-editor-panel">
        <header class="editor-header">
          <h4>调整连线</h4>
          <button class="close-btn" @click="onPaneClick">✕</button>
        </header>
        
        <div class="editor-body">
          <div class="control-group">
            <label>关系标签</label>
            <input v-model="edgeLabel" @input="applyEdgeUpdate" placeholder="例如：属于、关联..." />
          </div>

          <div class="control-group">
            <label>连线颜色</label>
            <div class="color-picker">
              <div v-for="color in presetColors" :key="color" 
                   class="color-btn" 
                   :style="{ backgroundColor: color }"
                   :class="{ active: edgeColor === color }"
                   @click="edgeColor = color; applyEdgeUpdate()">
              </div>
            </div>
          </div>

          <div class="control-group">
            <label>线条造型</label>
            <select v-model="edgeType" @change="applyEdgeUpdate">
              <option value="default">贝塞尔曲线 (默认)</option>
              <option value="smoothstep">圆角折线</option>
              <option value="step">直角折线</option>
              <option value="straight">直线</option>
            </select>
          </div>

          <div class="control-group">
            <label>线条样式</label>
            <select v-model="edgeDash" @change="applyEdgeUpdate">
              <option value="none">实线</option>
              <option value="5,5">虚线</option>
            </select>
          </div>

          <button class="btn-delete" @click="deleteSelectedEdge">✂️ 断开连线</button>
        </div>
      </div>
    </transition>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
// 🌟 引入 VueFlow 核心以及 Handle 句柄组件
import { VueFlow, Handle, Position } from '@vue-flow/core'
import { Background } from '@vue-flow/background'
import { Controls } from '@vue-flow/controls'

import '@vue-flow/core/dist/style.css'
import '@vue-flow/core/dist/theme-default.css'
import '@vue-flow/controls/dist/style.css' 

import { useSpiritData } from '../composables/useSpiritData'
// 🌟 引入我们刚刚创建的万能块渲染引擎
import SpiritBlock from './SpiritBlock.vue'

const emit = defineEmits(['open-editor', 'canvas-change'])

const props = defineProps<{
  noteId: string;
  blocks?: any[]; 
}>()

const { notes, selectNote } = useSpiritData()

const flowNodes = ref<any[]>([])
const flowEdges = ref<any[]>([])

// --- 连线编辑器状态 ---
const selectedEdge = ref<any>(null)
const edgeLabel = ref('')
const edgeColor = ref('#34c759')
const edgeType = ref('default')
const edgeDash = ref('5,5')
const presetColors = ['#34c759', '#ff3b30', '#007aff', '#ff9500', '#af52de', '#8e8e93']

const extractLinks = (nodeObj: any, links = new Set<string>()) => {
  if (!nodeObj) return links;
  if (nodeObj.type === 'spiritLink' && nodeObj.attrs?.id) {
    links.add(nodeObj.attrs.id);
  }
  if (Array.isArray(nodeObj.content)) {
    nodeObj.content.forEach((child: any) => extractLinks(child, links));
  }
  return links;
}

const syncFlowData = () => {
  const newNodes: any[] = []
  const newEdges: any[] = []
  const positionMap = new Map();
  const manualEdges: any[] = []; 

  if (props.blocks) {
    props.blocks.forEach(b => {
      if (b.type === 'canvas-node' && b.data) {
        try {
          const parsed = JSON.parse(b.data);
          if (parsed.attrs && parsed.attrs.refNoteId) {
            positionMap.set(parsed.attrs.refNoteId, { x: parsed.attrs.x, y: parsed.attrs.y });
          }
        } catch (e) {}
      } 
      else if (b.type === 'canvas-edge' && b.data) {
        try {
          const parsed = JSON.parse(b.data);
          manualEdges.push({
            id: `manual-${parsed.source}-${parsed.target}`,
            source: parsed.source,
            target: parsed.target,
            sourceHandle: parsed.sourceHandle, // 确保恢复时连在正确的点上
            targetHandle: parsed.targetHandle,
            animated: false,
            label: parsed.label || '',
            type: parsed.type || 'default',
            style: parsed.style || { stroke: '#34c759', strokeWidth: 2, strokeDasharray: '5,5' }
          });
        } catch (e) {}
      }
    });
  }

  notes.value.forEach(note => {
    const savedPos = positionMap.get(note.id);
    const finalPos = savedPos || { x: Math.random() * 400, y: Math.random() * 400 };

    newNodes.push({
      id: note.id,
      type: 'custom', // 🌟 指定使用自定义的 node-custom 模板
      label: note.title || '无标题碎片',
      position: finalPos,
      data: { 
        type: note.type,
        blocks: note.blocks || [] // 🌟 将这篇笔记真实的 Blocks 喂给节点
      } 
    })

    const links = extractLinks(note.content)
    links.forEach(targetId => {
      if (notes.value.some(n => n.id === targetId)) {
        newEdges.push({
          id: `edge-${note.id}-${targetId}`,
          source: note.id,
          target: targetId,
          sourceHandle: 'bottom', // 自动引用的蓝线默认底出顶进，更整洁
          targetHandle: 'top',
          animated: true, 
          style: { stroke: '#0066cc', strokeWidth: 2, opacity: 0.6 }
        })
      }
    })
  })

  flowNodes.value = newNodes
  flowEdges.value = [...newEdges, ...manualEdges] 
}

watch(
  () => [notes.value, props.blocks], 
  () => { syncFlowData(); }, 
  { deep: true, immediate: true }
)

const onNodeDragStop = () => {
  emit('canvas-change', { nodes: flowNodes.value, edges: flowEdges.value })
}

const onNodeDoubleClick = (event: any) => {
  const { node } = event
  emit('open-editor', node.id) 
}

const onConnect = (params: any) => {
  const newEdge = {
    id: `manual-${params.source}-${params.target}`,
    source: params.source,
    target: params.target,
    sourceHandle: params.sourceHandle, // 记录是从哪个 Handle 连出来的
    targetHandle: params.targetHandle,
    animated: false,
    label: '',
    type: 'default',
    style: { stroke: '#34c759', strokeWidth: 2, strokeDasharray: '5,5' }
  };
  flowEdges.value = [...flowEdges.value, newEdge];
  emit('canvas-change', { nodes: flowNodes.value, edges: flowEdges.value });
}

// --- 连线交互与编辑逻辑 ---

const onEdgeClick = (event: any) => {
  if (event.edge.id.startsWith('manual-')) {
    selectedEdge.value = event.edge;
    edgeLabel.value = event.edge.label || '';
    edgeColor.value = event.edge.style?.stroke || '#34c759';
    edgeType.value = event.edge.type || 'default';
    edgeDash.value = event.edge.style?.strokeDasharray || 'none';
  }
}

const onPaneClick = () => {
  selectedEdge.value = null;
}

const applyEdgeUpdate = () => {
  if (!selectedEdge.value) return;
  
  const idx = flowEdges.value.findIndex(e => e.id === selectedEdge.value.id);
  if (idx !== -1) {
    flowEdges.value[idx] = {
      ...flowEdges.value[idx],
      label: edgeLabel.value,
      type: edgeType.value,
      style: {
        ...flowEdges.value[idx].style,
        stroke: edgeColor.value,
        strokeDasharray: edgeDash.value === 'none' ? undefined : edgeDash.value
      }
    };
    flowEdges.value = [...flowEdges.value];
    selectedEdge.value = flowEdges.value[idx];
    
    emit('canvas-change', { nodes: flowNodes.value, edges: flowEdges.value });
  }
}

const deleteSelectedEdge = () => {
  if (!selectedEdge.value) return;
  flowEdges.value = flowEdges.value.filter(e => e.id !== selectedEdge.value.id);
  selectedEdge.value = null;
  emit('canvas-change', { nodes: flowNodes.value, edges: flowEdges.value });
}
</script>

<style scoped>
.spirit-canvas-wrapper {
  width: 100%;
  height: 100vh;
  background-color: #fcfcfd;
  position: relative;
}

/* 🌟 新增：自定义 Handle 连接点的样式 */
:deep(.custom-handle) {
  width: 10px;
  height: 10px;
  background-color: #0066cc;
  border: 2px solid white;
  opacity: 0; /* 默认隐藏，不打扰视觉 */
  transition: opacity 0.2s ease, transform 0.2s ease;
  cursor: crosshair;
}

/* 鼠标悬浮或选中时显示连线锚点 */
:deep(.spirit-node-card:hover .custom-handle),
:deep(.vue-flow__node.selected .custom-handle) {
  opacity: 1;
}
:deep(.custom-handle:hover) { transform: scale(1.3); }

/* 🌟 核心节点卡片样式 */
:deep(.spirit-node-card) {
  position: relative; /* 确保 Handle 正确吸附在边缘 */
  background: rgba(255, 255, 255, 0.95);
  backdrop-filter: blur(10px);
  border: 1px solid rgba(0, 0, 0, 0.08);
  border-radius: 12px;
  padding: 12px 16px;
  min-width: 180px;
  max-width: 260px; /* 限制最大宽度以免文本太长 */
  font-size: 14px;
  color: #1d1d1f;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.04);
  transition: box-shadow 0.2s ease, border-color 0.2s ease;
  cursor: grab;
}
:deep(.spirit-node-card:hover) { box-shadow: 0 8px 24px rgba(0, 102, 204, 0.12); }
:deep(.vue-flow__node.selected .spirit-node-card) { border-color: #0066cc; box-shadow: 0 0 0 2px rgba(0, 102, 204, 0.2); }
:deep(.spirit-node-card:active) { cursor: grabbing; }

/* 节点内部排版优化 */
:deep(.node-header) {
  display: flex;
  align-items: center;
  gap: 6px;
  border-bottom: 1px solid rgba(0, 0, 0, 0.05);
  padding-bottom: 8px;
  margin-bottom: 10px;
}
:deep(.node-icon) { font-size: 14px; }
:deep(.node-title) { font-weight: 700; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }

:deep(.node-blocks-area) {
  min-height: 20px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}
:deep(.node-empty-hint) {
  font-size: 12px;
  color: #c7c7cc;
  font-style: italic;
  text-align: center;
  padding: 8px 0;
}

/* 文件夹节点特殊主题 */
:deep(.node-type-folder) { background: rgba(250, 245, 255, 0.95); border-color: #e0d4f5; border-radius: 16px; }
:deep(.node-type-folder:hover) { box-shadow: 0 8px 24px rgba(103, 58, 183, 0.12); }
:deep(.vue-flow__node.selected .node-type-folder) { border-color: #673ab7; box-shadow: 0 0 0 2px rgba(103, 58, 183, 0.2); }

/* 连线编辑器面板样式 */
.edge-editor-panel {
  position: absolute;
  top: 24px;
  right: 24px;
  width: 260px;
  background: rgba(255, 255, 255, 0.85);
  backdrop-filter: blur(20px) saturate(180%);
  border: 1px solid rgba(0, 0, 0, 0.08);
  border-radius: 16px;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.1);
  z-index: 1000;
  overflow: hidden;
}

.editor-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  background: rgba(0, 0, 0, 0.02);
  border-bottom: 1px solid rgba(0, 0, 0, 0.05);
}

.editor-header h4 { margin: 0; font-size: 14px; color: #1d1d1f; }
.close-btn { background: none; border: none; cursor: pointer; color: #86868b; font-size: 14px; }
.close-btn:hover { color: #1d1d1f; }

.editor-body { padding: 16px; display: flex; flex-direction: column; gap: 16px; }
.control-group { display: flex; flex-direction: column; gap: 6px; }
.control-group label { font-size: 12px; color: #86868b; font-weight: 500; }

.control-group input, .control-group select {
  width: 100%;
  padding: 8px 12px;
  border-radius: 8px;
  border: 1px solid #d2d2d7;
  font-size: 13px;
  outline: none;
  background: white;
  box-sizing: border-box;
}
.control-group input:focus, .control-group select:focus { border-color: #0066cc; }

.color-picker { display: flex; gap: 8px; }
.color-btn {
  width: 24px; height: 24px; border-radius: 50%; cursor: pointer;
  border: 2px solid transparent; transition: transform 0.2s;
}
.color-btn.active { border-color: #1d1d1f; transform: scale(1.1); box-shadow: 0 2px 8px rgba(0,0,0,0.2); }

.btn-delete {
  margin-top: 8px; padding: 10px; border-radius: 8px; border: none;
  background: #ffeeea; color: #ff3b30; font-size: 13px; font-weight: 600; cursor: pointer; transition: background 0.2s;
}
.btn-delete:hover { background: #ffd3cc; }

.fade-enter-active, .fade-leave-active { transition: opacity 0.2s, transform 0.2s; }
.fade-enter-from, .fade-leave-to { opacity: 0; transform: translateY(-10px); }
</style>