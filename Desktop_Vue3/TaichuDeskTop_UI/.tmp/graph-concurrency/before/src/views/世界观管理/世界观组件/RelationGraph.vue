<template>
  <section class="relation-graph-wrapper" :class="{'has-selection': selectedNode}">
    <header class="graph-toolbar">
      <div><strong>关系图谱</strong><span class="graph-count">{{ displayedNodes.length }} 个条目 · {{ displayedEdges.length }} 条关系</span></div>
      <div class="graph-actions">
        <span v-if="saveStatus" class="graph-save" role="status">{{ saveStatus === 'saving' ? '正在保存位置…' : saveStatus === 'error' ? '位置保存失败' : '位置已保存' }}</span>
        <button v-if="saveStatus === 'error'" @click="$emit('retry-save')">重试保存</button>
        <button @click="renderer?.resetCamera(focusNodeId ? 'selection' : 'all')">适应画布</button>
      </div>
    </header>
    <div class="graph-filter-bar">
      <input v-model="searchQuery" type="search" placeholder="搜索条目名称" aria-label="搜索图谱条目" />
      <select v-model="typeFilter" aria-label="筛选条目类型" @change="exitFocus"><option value="">全部类型</option><option v-for="type in availableTypes" :key="type" :value="type">{{ getTypeLabel(type) }}</option></select>
      <button v-if="focusNodeId" @click="showAll">返回全部关系</button>
    </div>
    <p v-if="error" role="alert" class="graph-message">{{ error }} <button @click="refresh">重试</button></p>
    <p v-else class="graph-message" role="status">{{ detailsLoading ? '正在读取引用连线…' : focusNodeId ? `正在查看「${focusNodeTitle}」的直接关系` : `${canSave ? '拖动节点自动保存位置' : '拖动仅调整当前视图'}；悬停突出关联，点击聚焦；滚轮缩放查看完整名称。` }}</p>
    <div class="graph-body">
      <div class="graph-canvas"><component :key="projectId" :is="spatial ? SpatialWorldGraph : FlatWorldGraph" ref="renderer" :data="filteredData" :layout-data="graphData" :focus-id="focusNodeId" :positions="positions" @select="selectNode" @update:positions="$emit('update:positions', $event)" />
      <p v-if="!displayedNodes.length" class="graph-empty">没有匹配的条目</p></div>
      <aside v-if="selectedNode" class="node-popup">
        <div class="popup-heading"><span>{{ getTypeLabel(selectedNode.type) }}</span><button aria-label="关闭条目预览" @click="showAll">×</button></div>
        <strong>{{ selectedNode.title }}</strong>
        <p>{{ getNodePreview(selectedNode.id) || '选择查看详情，阅读完整设定。' }}</p>
        <div class="popup-tags"><span v-for="tag in getNodeTags(selectedNode.id)" :key="tag">#{{ tag }}</span></div>
        <button class="detail-button" @click="openCardDetail(selectedNode.id)">查看详情 →</button>
      </aside>
    </div>
    <footer class="graph-legend"><span>— 关联</span><span>┄ 引用</span><span>点击条目后仅显示直接关系</span></footer>
  </section>
</template>
<script setup lang="ts">
import { computed, ref, watch, defineAsyncComponent } from 'vue'
import { useWorldStore } from '@/stores/world'
import type { CardSummary, CardDetail } from '@/api/types/world'
import { getCardTypeLabel as getTypeLabel, parseStringArray } from '@/utils/worldPresentation'
import { buildWorldGraph } from '@/utils/worldGraph'
import FlatWorldGraph from './FlatWorldGraph.vue'
const SpatialWorldGraph = defineAsyncComponent(() => import('./SpatialWorldGraph.vue'))

const props = defineProps<{ projectId: string; canSave?: boolean; saveStatus?: '' | 'saving' | 'saved' | 'error'; positions?: Record<string, { x: number; y: number }> }>()
const emit = defineEmits<{ select: [id: string]; 'retry-save': []; 'update:positions': [positions: Record<string, { x: number; y: number }>] }>()
const store = useWorldStore()
const selectedNode = ref<CardSummary | null>(null)
const spatial = ref(false)
const renderer = ref<{ resetCamera: (scope?: 'all' | 'selection') => void } | null>(null)
const references = ref<Record<string, string[]>>({})
const detailsLoading = ref(false)
const error = ref('')
let requestVersion = 0
const getDetail = (id: string) => store.getCardDetailById(id, props.projectId)
async function refresh() {
  const version = ++requestVersion
  detailsLoading.value = true; error.value = ''
  try {
    const result = await store.loadReferences(props.projectId)
    if (version === requestVersion) references.value = result
  } catch { if (version === requestVersion) error.value = '引用连线加载失败，请重试。' }
  finally { if (version === requestVersion) detailsLoading.value = false }
}
watch(() => [props.projectId, ...store.cards.map(card => `${card.id}:${card.updatedAt}`)].join('|'), () => { references.value = {}; selectedNode.value = null; void refresh() }, { immediate: true })
const graphData = computed(() => buildWorldGraph(store.cards, store.allRelations, id => ({
  description: '', contentBlocks: (references.value[id] || []).map(cardId => ({ cardId })),
}) as CardDetail))

// ===== 类型筛选 + 搜索 + 聚焦探索 =====
const typeFilter = ref<string>('')
const searchQuery = ref('')
const focusNodeId = ref<string>('')
watch(() => props.projectId, () => { focusNodeId.value = ''; searchQuery.value = ''; typeFilter.value = '' })
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
function showAll() { searchQuery.value = ''; typeFilter.value = ''; selectedNode.value = null; exitFocus() }
function exitFocus() {
  selectedNode.value = null
  focusNodeId.value = ''
}

const focusNodeTitle = computed(() => {
  if (!focusNodeId.value) return ''
  return graphData.value.nodes.find(n => n.id === focusNodeId.value)?.title ?? ''
})

const displayedNodes = computed(() => filteredData.value.nodes)
const displayedEdges = computed(() => filteredData.value.links)
watch(searchQuery, exitFocus)
watch(displayedNodes, nodes => {
  if (selectedNode.value && !nodes.some(node => node.id === selectedNode.value?.id)) selectedNode.value = null
})

function selectNode(id: string) {
  selectedNode.value = store.getCardById(id, props.projectId) ?? null
  focusNodeId.value = id || ''
  if (id) void store.loadCardDetail(props.projectId, id).catch(() => {
    if (selectedNode.value?.id === id) error.value = '条目详情加载失败，请重新选择。'
  })
}
const getNodePreview = (id: string) => (getDetail(id)?.description ?? '').replace(/\{CARD:([^}]+)\}/g,
  (_, target: string) => `@${store.getCardById(target, props.projectId)?.title || '不可用的条目'}`)
const getNodeTags = (id: string) => parseStringArray(getDetail(id)?.tags)
function openCardDetail(id: string) {
  selectedNode.value = null
  emit('select', id)
}
defineExpose({ refresh })
</script>
<style scoped>
.relation-graph-wrapper{height:100%;min-height:480px;display:flex;flex-direction:column;background:var(--paper-card);color:var(--text-primary);position:relative}
.graph-toolbar,.graph-filter-bar{display:flex;align-items:center;justify-content:space-between;gap:12px;padding:16px 20px 0;flex-wrap:wrap}.graph-toolbar strong{font-size:16px}.graph-count{margin-left:12px;color:var(--text-secondary);font-size:12px}.graph-actions{display:flex;gap:8px;align-items:center}.graph-save{font-size:12px;color:var(--text-secondary)}
button,select,input{font:inherit;font-size:13px;border:1px solid var(--border-line);background:var(--paper-card);color:var(--text-primary);border-radius:6px;padding:8px 10px}button,select{cursor:pointer}button:hover{background:var(--bg-sub)}button:focus-visible,input:focus-visible,select:focus-visible{outline:2px solid var(--accent-color);outline-offset:2px}
.graph-filter-bar{justify-content:flex-start;padding-top:12px}.graph-filter-bar input{width:min(240px,100%);box-sizing:border-box}.graph-message{font-size:12px;line-height:1.6;color:var(--text-secondary);margin:10px 20px}.graph-body{display:flex;position:relative;flex:1;min-height:320px;overflow:hidden;border-top:1px solid var(--border-line)}.graph-canvas{position:relative;flex:1;min-width:0}.graph-empty{position:absolute;top:40%;width:100%;text-align:center;pointer-events:none;color:var(--text-secondary)}
.node-popup{width:240px;flex-shrink:0;max-height:100%;box-sizing:border-box;overflow:auto;padding:16px;background:var(--paper-card);border:1px solid var(--border-line);border-radius:0;box-shadow:none}.popup-heading{display:flex;align-items:center;justify-content:space-between;margin-bottom:8px;color:var(--text-secondary);font-size:12px}.popup-heading button{border:0;padding:0 4px;font-size:20px}.node-popup strong{overflow-wrap:anywhere}.node-popup p{font-size:13px;line-height:1.7;display:-webkit-box;-webkit-line-clamp:4;-webkit-box-orient:vertical;overflow:hidden}.popup-tags{display:flex;flex-wrap:wrap;gap:6px;font-size:12px;color:var(--text-secondary)}.detail-button{margin-top:14px;width:100%}.graph-legend{display:flex;flex-wrap:wrap;gap:16px;padding:10px 20px;border-top:1px solid var(--border-line);color:var(--text-secondary);font-size:11px}
@media(max-width:600px){.relation-graph-wrapper{height:auto;min-height:65dvh}.has-selection .graph-body{height:560px}.graph-toolbar,.graph-filter-bar{padding-left:12px;padding-right:12px}.graph-count{display:block;margin:4px 0 0}.graph-message{margin-left:12px;margin-right:12px}.graph-body{flex:none;flex-direction:column;height:380px;min-height:380px}.graph-canvas{min-height:260px}.node-popup{width:100%;max-height:210px;border-width:1px 0 0}.graph-filter-bar input{flex:1;min-width:120px;width:120px}}
</style>
