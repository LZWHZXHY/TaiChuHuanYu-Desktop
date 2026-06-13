<template>
  <div class="spirit-canvas-wrapper">
    <VueFlow
      v-model:nodes="flowNodes"
      v-model:edges="flowEdges"
      @nodeDragStop="onNodeDragStop"
      @nodeDoubleClick="onNodeDoubleClick"
      :default-zoom="1"
      :min-zoom="0.2"
      :max-zoom="4"
      fit-view-on-init
    >
      <Background pattern-color="#c7c7cc" :gap="24" :size="2" />
      
      <Controls position="bottom-right" />
    </VueFlow>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { VueFlow } from '@vue-flow/core'
import { Background } from '@vue-flow/background'
import { Controls } from '@vue-flow/controls'

// 必须引入的底层样式
import '@vue-flow/core/dist/style.css'
import '@vue-flow/core/dist/theme-default.css'

import { useSpiritData } from '../composables/useSpiritData'

const emit = defineEmits(['open-editor', 'canvas-change'])

// 🌟 新增：接收从 WorkspaceCanvas (或者服务器) 传进来的 blocks 坐标数据
const props = defineProps<{
  noteId: string;
  blocks?: any[]; 
}>()

const { notes, selectNote } = useSpiritData()

// Vue Flow 需要的独立状态容器
const flowNodes = ref<any[]>([])
const flowEdges = ref<any[]>([])

/**
 * 🌟 核心引擎 1：深度递归 Tiptap JSON，榨取所有精神链接（spiritLink）
 */
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

/**
 * 🌟 核心引擎 2：将你的 notes 转换成图谱节点和连线
 */
const syncFlowData = () => {
  const newNodes: any[] = []
  const newEdges: any[] = []

  // 【关键修复 1】：把传进来的 blocks 解析成一个“坐标字典 (Map)”
  const positionMap = new Map();
  if (props.blocks) {
    props.blocks.forEach(b => {
      if (b.type === 'canvas-node' && b.data) {
        try {
          const parsed = JSON.parse(b.data);
          // 解析出真正的本体 ID 和对应的 XY 坐标
          if (parsed.attrs && parsed.attrs.refNoteId) {
            positionMap.set(parsed.attrs.refNoteId, { x: parsed.attrs.x, y: parsed.attrs.y });
          }
        } catch (e) {
          console.warn('坐标解析失败', e)
        }
      }
    });
  }

  // 【关键修复 2】：遍历所有的笔记，去上面的字典里找坐标
  notes.value.forEach(note => {
    // 如果字典里有保存的坐标，就用保存的；如果没有（比如新建的笔记），才给个初始随机位置
    const savedPos = positionMap.get(note.id);
    const finalPos = savedPos || { x: Math.random() * 400, y: Math.random() * 400 };

    newNodes.push({
      id: note.id,
      label: note.title || '无标题碎片',
      position: finalPos,
      class: 'spirit-node-card', // 绑定自定义 CSS 类
    })

    // 生成连线（双向链接可视化）
    const links = extractLinks(note.content)
    links.forEach(targetId => {
      if (notes.value.some(n => n.id === targetId)) {
        newEdges.push({
          id: `edge-${note.id}-${targetId}`,
          source: note.id,
          target: targetId,
          animated: true, 
          style: { stroke: '#0066cc', strokeWidth: 2, opacity: 0.6 }
        })
      }
    })
  })

  flowNodes.value = newNodes
  flowEdges.value = newEdges
}

// 【关键修复 3】：既监听 notes 本体内容的变化，也监听外部 blocks 坐标的载入
watch(
  () => [notes.value, props.blocks], 
  () => {
    syncFlowData();
  }, 
  { deep: true, immediate: true }
)

/**
 * 🌟 交互逻辑 1：拖拽节点结束时，直接抛出最新数组，由外层接管保存
 */
const onNodeDragStop = (event: any) => {
  // 【关键修复 4】：不再把坐标写回 note.extraData，保持 Note 本体的纯净
  emit('canvas-change', flowNodes.value)
}

/**
 * 🌟 交互逻辑 2：双击节点，通知主布局打开 Tiptap 编辑器抽屉
 */
const onNodeDoubleClick = (event: any) => {
  const { node } = event
  selectNote(node.id) // 切换全局选中的碎片
  emit('open-editor', node.id) // 抛出事件
}
</script>

<style scoped>
.spirit-canvas-wrapper {
  width: 100%;
  height: 100vh; /* 撑满屏幕 */
  background-color: #fcfcfd;
}

/* 覆盖 Vue Flow 默认的极简节点样式，让它看起来像一张 VVD 卡片 */
:deep(.spirit-node-card) {
  background: rgba(255, 255, 255, 0.95);
  backdrop-filter: blur(10px);
  border: 1px solid rgba(0, 0, 0, 0.08);
  border-radius: 12px;
  padding: 12px 20px;
  font-size: 14px;
  font-weight: 600;
  color: #1d1d1f;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.04);
  transition: box-shadow 0.2s ease, border-color 0.2s ease;
  cursor: grab;
}

:deep(.spirit-node-card:hover) {
  box-shadow: 0 8px 24px rgba(0, 102, 204, 0.12);
}

:deep(.vue-flow__node.selected .spirit-node-card) {
  border-color: #0066cc;
  box-shadow: 0 0 0 2px rgba(0, 102, 204, 0.2);
}

:deep(.spirit-node-card:active) {
  cursor: grabbing;
}
</style>