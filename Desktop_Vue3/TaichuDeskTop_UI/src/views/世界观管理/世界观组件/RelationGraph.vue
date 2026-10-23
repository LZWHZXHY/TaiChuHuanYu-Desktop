<template>
  <div class="relation-graph-wrapper">
    <!-- 工具栏 -->
    <div class="graph-toolbar">
      <div class="toolbar-left">
        <span class="graph-title">关系图谱</span>
        <span class="graph-info">
          {{ displayedNodes.length }} 节点 · {{ displayedEdges.length }} 连接
        </span>
      </div>
      <div class="toolbar-right">
        <div class="legend">
          <span class="legend-item">
            <span class="legend-line solid"></span>
            <span>显式关系</span>
          </span>
          <span class="legend-item">
            <span class="legend-line dashed"></span>
            <span>嵌入引用</span>
          </span>
          <span
            v-for="rt in relationTypes"
            :key="rt.label"
            class="legend-item relation-item"
            :title="rt.label"
          >
            <span class="legend-dot" :style="{ background: rt.color }"></span>
            <span>{{ rt.label }}</span>
          </span>
        </div>
        <button class="toolbar-btn" @click="toggleRotate">
          {{ autoRotate ? '暂停旋转' : '自动旋转' }}
        </button>
        <button class="toolbar-btn" @click="resetCamera">重置视角</button>
      </div>
    </div>

    <!-- 筛选栏 -->
    <div class="graph-filter-bar">
      <input v-model="searchQuery" class="graph-search" type="search" placeholder="搜索卡片…" />
      <button
        v-for="type in availableTypes"
        :key="type"
        class="type-chip"
        :class="{ active: typeFilter === type }"
        :style="{ color: getTypeColor(type), borderColor: typeFilter === type ? getTypeColor(type) : 'transparent' }"
        @click="toggleTypeFilter(type)"
      >
        {{ getTypeLabel(type) }}
      </button>
      <button v-if="focusNodeId" class="focus-badge" @click="exitFocus">
        🎯 聚焦中：{{ focusNodeTitle }} ✕
      </button>
    </div>

    <!-- 3D 容器 -->
    <div ref="graphContainer" class="graph-container" v-loading="graphLoading"></div>

    <!-- 空状态 -->
    <div v-if="!displayedNodes.length && !graphLoading" class="graph-empty">
      <p>暂无卡片</p>
    </div>

    <!-- 节点浮窗 -->
    <div v-if="selectedNode" class="node-popup">
      <div class="popup-header">
        <span
          class="popup-type"
          :style="{
            background: getTypeColor(selectedNode.type) + '20',
            color: getTypeColor(selectedNode.type),
          }"
        >
          {{ getTypeLabel(selectedNode.type) }}
        </span>
        <span class="popup-title">{{ selectedNode.title }}</span>
        <button class="popup-close" @click="selectedNode = null">×</button>
      </div>
      <div class="popup-body">
        <p v-if="getNodePreview(selectedNode.id)" class="popup-desc">
          {{ getNodePreview(selectedNode.id) }}
        </p>
        <div v-if="getNodeTags(selectedNode.id).length" class="popup-tags">
          <span v-for="tag in getNodeTags(selectedNode.id)" :key="tag" class="popup-tag">
            #{{ tag }}
          </span>
        </div>
        <div class="popup-actions">
          <button class="popup-btn" @click="openCardDetail(selectedNode.id)">
            查看详情
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useWorldStore } from '@/stores/world'
import type { CardSummary } from '@/api/types/world'
import { getCardTypeLabel as getTypeLabel, parseStringArray } from '@/utils/worldPresentation'
import { buildWorldGraph, getGraphTypeColor as getTypeColor, getRelationTypeColor } from '@/utils/worldGraph'
import { useCardPreload } from '@/composables/世界观管理/useCardPreload'
import { useWorldGraph } from '@/composables/世界观管理/useWorldGraph'

const props = defineProps<{ projectId: string }>()
defineEmits<{ cardInserted: [] }>()
const store = useWorldStore()
const router = useRouter()
const graphContainer = ref<HTMLElement | null>(null)
const selectedNode = ref<CardSummary | null>(null)
const { getDetail } = useCardPreload(() => props.projectId, () => store.cards.map(card => card.id))
const graphData = computed(() => buildWorldGraph(store.cards, store.allRelations, getDetail))

// ===== 类型筛选 + 搜索 + 聚焦探索 =====
const typeFilter = ref<string>('')
const searchQuery = ref('')
const focusNodeId = ref<string>('')
const availableTypes = computed(() => [...new Set(graphData.value.nodes.map(n => n.type))])
const filteredData = computed(() => {
  let nodes = graphData.value.nodes
  if (typeFilter.value) nodes = nodes.filter(n => n.type === typeFilter.value)
  const q = searchQuery.value.trim().toLowerCase()
  if (q) nodes = nodes.filter(n => n.title.toLowerCase().includes(q))

  let allowed = new Set(nodes.map(n => n.id))
  if (focusNodeId.value) {
    // 聚焦模式：只显示焦点节点 + 其直接邻居
    const neighborIds = new Set<string>()
    for (const l of graphData.value.links) {
      if (l.source === focusNodeId.value) neighborIds.add(l.target)
      if (l.target === focusNodeId.value) neighborIds.add(l.source)
    }
    allowed = new Set([focusNodeId.value, ...neighborIds])
    nodes = graphData.value.nodes.filter(n => allowed.has(n.id))
  }
  return {
    nodes,
    links: graphData.value.links.filter(l => allowed.has(l.source) && allowed.has(l.target))
  }
})
function toggleTypeFilter(type: string) {
  typeFilter.value = typeFilter.value === type ? '' : type
  focusNodeId.value = ''
}
function exitFocus() {
  focusNodeId.value = ''
}

const focusNodeTitle = computed(() => {
  if (!focusNodeId.value) return ''
  return graphData.value.nodes.find(n => n.id === focusNodeId.value)?.title ?? ''
})

const displayedNodes = computed(() => filteredData.value.nodes)
const displayedEdges = computed(() => filteredData.value.links)

// 关系类型图例（去重 + 稳定配色）
const relationTypes = computed(() => {
  const map = new Map<string, string>()
  for (const l of graphData.value.links) {
    if (l.isEmbed || !l.label) continue
    if (!map.has(l.label)) map.set(l.label, getRelationTypeColor(l.label))
  }
  return [...map.entries()].map(([label, color]) => ({ label, color }))
})
const { loading: graphLoading, autoRotate, toggleRotate, resetCamera, refresh } = useWorldGraph(graphContainer, filteredData,
  id => {
    selectedNode.value = store.getCardById(id, props.projectId) ?? null
    // 点击节点进入聚焦探索，点空白处退出聚焦
    focusNodeId.value = id || ''
  })
const getNodePreview = (id: string) => getDetail(id)?.description ?? ''
const getNodeTags = (id: string) => parseStringArray(getDetail(id)?.tags)
function openCardDetail(id: string) {
  selectedNode.value = null
  router.push('/world/project/' + props.projectId + '/card/' + id)
}
defineExpose({ refresh })
</script>

<style scoped>
/* ============================================================
   容器：全屏 3D
   ============================================================ */
.relation-graph-wrapper {
  position: relative;
  width: 100%;
  height: 100%;
  min-height: 600px;
  background: var(--bg-main);
  overflow: hidden;
}

/* ============================================================
   工具栏（浮在图上）
   ============================================================ */
.graph-toolbar {
  position: absolute;
  top: 16px;
  left: 16px;
  right: 16px;
  z-index: 5;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  pointer-events: none;
}

.toolbar-left,
.toolbar-right {
  display: flex;
  align-items: center;
  gap: 10px;
  pointer-events: auto;
}

.toolbar-left {
  background: var(--paper-card);
  backdrop-filter: blur(8px);
  padding: 8px 14px;
  border-radius: 8px;
  border: 1px solid var(--border-line);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
}

.graph-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
}
.graph-info {
  font-size: 12px;
  color: var(--text-secondary);
}

.legend {
  display: flex;
  gap: 12px;
  font-size: 11px;
  color: var(--text-secondary);
  background: var(--paper-card);
  backdrop-filter: blur(8px);
  padding: 6px 12px;
  border-radius: 6px;
  border: 1px solid var(--border-line);
}
.legend-item {
  display: inline-flex;
  align-items: center;
  gap: 5px;
}
.legend-line {
  display: inline-block;
  width: 18px;
  height: 0;
  border-top: 2px solid var(--text-secondary);
}
.legend-line.dashed {
  border-top: 2px dashed var(--border-line);
}
.legend-dot {
  display: inline-block;
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.toolbar-btn {
  padding: 6px 14px;
  border: 1px solid var(--border-line);
  background: var(--paper-card);
  backdrop-filter: blur(8px);
  border-radius: 6px;
  font-size: 12px;
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 0.15s;
  font-family: inherit;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
}
.toolbar-btn:hover {
  background: var(--paper-card);
  border-color: var(--border-line);
  color: var(--text-primary);
}

/* ============================================================
   筛选栏（搜索 + 类型筛选）
   ============================================================ */
.graph-filter-bar {
  position: absolute;
  top: 60px;
  left: 16px;
  right: 16px;
  z-index: 5;
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  pointer-events: none;
}
.graph-search {
  pointer-events: auto;
  padding: 6px 12px;
  border: 1px solid var(--border-line);
  border-radius: 6px;
  background: var(--paper-card);
  backdrop-filter: blur(8px);
  color: var(--text-primary);
  font-size: 12px;
  width: 160px;
}
.graph-search:focus { outline: none; border-color: var(--accent-color); }
.type-chip {
  pointer-events: auto;
  padding: 5px 12px;
  border: 1px solid transparent;
  border-radius: 14px;
  background: var(--paper-card);
  backdrop-filter: blur(8px);
  font-size: 12px;
  cursor: pointer;
  transition: all 0.15s;
  font-family: inherit;
}
.type-chip:hover { opacity: 0.85; }
.type-chip.active { background: var(--bg-sub); font-weight: 600; }
.focus-badge {
  pointer-events: auto;
  padding: 5px 12px;
  border: 1px solid var(--accent-color);
  border-radius: 14px;
  background: var(--paper-card);
  backdrop-filter: blur(8px);
  color: var(--accent-color);
  font-size: 12px;
  cursor: pointer;
  font-family: inherit;
  max-width: 240px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.focus-badge:hover { background: var(--accent-soft); }

/* ============================================================
   3D 容器
   ============================================================ */
.graph-container {
  width: 100%;
  height: 100%;
  position: relative;
}
.graph-container :deep(canvas) {
  display: block;
}

.graph-empty {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  pointer-events: none;
  color: var(--text-secondary);
  font-size: 14px;
}
.graph-empty p { margin: 0; }

/* ============================================================
   节点浮窗
   ============================================================ */
.node-popup {
  position: absolute;
  top: 80px;
  right: 16px;
  width: 280px;
  background: var(--paper-card);
  border-radius: 10px;
  border: 1px solid var(--border-line);
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
  overflow: hidden;
  z-index: 10;
}
.popup-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 14px;
  border-bottom: 1px solid var(--border-line);
}
.popup-type {
  font-size: 11px;
  padding: 2px 8px;
  border-radius: 4px;
  font-weight: 500;
  flex-shrink: 0;
}
.popup-title {
  flex: 1;
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.popup-close {
  background: none;
  border: none;
  font-size: 18px;
  color: var(--text-secondary);
  cursor: pointer;
  padding: 0 4px;
  line-height: 1;
}
.popup-close:hover { color: var(--text-secondary); }

.popup-body {
  padding: 12px 14px;
}
.popup-desc {
  font-size: 13px;
  line-height: 1.6;
  color: var(--text-secondary);
  margin: 0 0 10px;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
.popup-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-bottom: 10px;
}
.popup-tag {
  font-size: 11px;
  color: var(--accent-color);
  background: var(--bg-sub);
  padding: 1px 8px;
  border-radius: 3px;
}
.popup-actions {
  display: flex;
  justify-content: flex-end;
}
.popup-btn {
  padding: 6px 14px;
  background: var(--accent-color);
  color: var(--on-accent);
  border: none;
  border-radius: 6px;
  font-size: 12px;
  cursor: pointer;
  font-family: inherit;
  transition: background 0.15s;
}
.popup-btn:hover { background: var(--text-primary); }

/* ============================================================
   响应式
   ============================================================ */
@media (max-width: 640px) {
  .graph-toolbar {
    flex-direction: column;
    align-items: stretch;
  }
  .toolbar-right {
    flex-wrap: wrap;
  }
  .legend { display: none; }
  .node-popup {
    top: auto;
    bottom: 16px;
    left: 16px;
    right: 16px;
    width: auto;
  }
}
</style>
