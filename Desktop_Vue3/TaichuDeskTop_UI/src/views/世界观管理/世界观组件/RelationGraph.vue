<template>
  <div class="relation-graph-wrapper">
    <!-- 左侧：卡片库 -->
    <div class="card-library">
      <div class="library-header">
        <span class="library-title">📚 卡片库</span>
        <span class="library-count">{{ allCards.length }} 张卡片</span>
      </div>
      <div class="library-search">
        <input v-model="searchQuery" placeholder="搜索卡片..." />
      </div>
      <div class="library-filter">
        <button
          v-for="type in filterTypes"
          :key="type.value"
          class="filter-chip"
          :class="{ active: libraryFilter === type.value }"
          @click="libraryFilter = type.value"
        >
          {{ type.label }}
        </button>
      </div>
      <div class="library-list">
        <div
          v-for="card in filteredLibraryCards"
          :key="card.id"
          class="library-item"
          :class="{ inserted: insertedIds.includes(card.id) }"
          @click="toggleCard(card.id)"
        >
          <span class="lib-type-label">{{ getTypeLabel(card.type) }}</span>
          <span class="lib-title">{{ card.title }}</span>
          <span v-if="insertedIds.includes(card.id)" class="lib-badge">✓</span>
        </div>
      </div>
    </div>

    <!-- 右侧：图谱 -->
    <div class="graph-area">
      <!-- 工具栏 -->
      <div class="graph-toolbar">
        <div class="toolbar-left">
          <span class="graph-title">🕸️ 关系图谱</span>
          <span class="graph-info">{{ displayedNodes.length }} 个节点 · {{ displayedEdges.length }} 条关联</span>
        </div>
        <div class="toolbar-right">
          <span class="hint-text">💡 点击卡片库中的卡片插入/移除 · 拖拽节点建立关联</span>
          <button class="toolbar-btn" @click="resetGraph">重置</button>
          <button class="toolbar-btn" @click="togglePhysics">
            {{ physicsEnabled ? '关闭物理' : '开启物理' }}
          </button>
        </div>
      </div>

      <!-- 类型筛选 -->
      <div class="graph-filter">
        <button
          v-for="type in filterTypes"
          :key="type.value"
          class="graph-filter-chip"
          :class="{ active: graphFilter === type.value }"
          @click="graphFilter = type.value"
        >
          {{ type.label }}
        </button>
      </div>

      <!-- 图谱容器 -->
      <div ref="networkContainer" class="network-container"></div>

      <!-- 节点详情浮窗 -->
      <div v-if="selectedNode" class="node-popup" :style="popupStyle">
        <div class="popup-header">
          <span class="popup-type-label">{{ getTypeLabel(selectedNode.type) }}</span>
          <span class="popup-title">{{ selectedNode.title }}</span>
          <button class="popup-close" @click="selectedNode = null">×</button>
        </div>
        <div class="popup-body">
          <p class="popup-desc">{{ getNodePreview(selectedNode) }}</p>
          <div class="popup-tags">
            <span v-for="tag in getNodeTags(selectedNode)" :key="tag" class="popup-tag">#{{ tag }}</span>
          </div>
          <div class="popup-actions">
            <button class="popup-btn" @click="openCardDetail(selectedNode.id)">查看</button>
            <button class="popup-btn secondary" @click="editCardFromGraph(selectedNode)">编辑</button>
          </div>
        </div>
      </div>

      <!-- 建立关系对话框 -->
      <div v-if="showRelationDialog" class="dialog-overlay" @click.self="showRelationDialog = false">
        <div class="dialog">
          <header class="dialog-header">
            <h2>建立关联</h2>
            <button class="close-btn" @click="showRelationDialog = false">✕</button>
          </header>
          <div class="dialog-body">
            <div class="relation-preview">
              <span class="preview-source">{{ relationSource?.title || '源' }}</span>
              <span class="preview-arrow">→</span>
              <span class="preview-target">{{ relationTarget?.title || '目标' }}</span>
            </div>
            <div class="field">
              <label>关系描述 <span class="required">*</span></label>
              <input v-model="relationForm.relationType" placeholder="如：是...的师父、持有" />
            </div>
          </div>
          <footer class="dialog-footer">
            <button class="btn-outline" @click="showRelationDialog = false">取消</button>
            <button class="btn-primary" @click="handleCreateRelation" :disabled="creatingRelation">
              {{ creatingRelation ? '创建中...' : '建立关联' }}
            </button>
          </footer>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount, nextTick } from 'vue';
import { ElMessage } from 'element-plus';
import { useWorldStore } from '../../../stores/world';
import { Network } from 'vis-network';
import { DataSet } from 'vis-data';

const props = defineProps<{ projectId: string }>();
const emit = defineEmits<{ (e: 'cardInserted'): void }>();

const store = useWorldStore();

// ===== 筛选类型 =====
const TYPE_LABELS: Record<string, string> = {
  character: '角色',
  location: '地点',
  item: '物品',
  event: '事件',
  faction: '派系',
  species: '物种',
  occupation: '职业',
  organization: '组织',
  creature: '生物',
  skill: '技能',
  climate: '气候',
  concept: '设定',  // ✅ 新增
};

const filterTypes = [
  { value: 'all', label: '全部' },
  { value: 'character', label: '角色' },
  { value: 'location', label: '地点' },
  { value: 'item', label: '物品' },
  { value: 'event', label: '事件' },
  { value: 'faction', label: '派系' },
  { value: 'species', label: '物种' },
  { value: 'occupation', label: '职业' },
  { value: 'organization', label: '组织' },
  { value: 'creature', label: '生物' },
  { value: 'skill', label: '技能' },
  { value: 'climate', label: '气候' },
  { value: 'concept', label: '设定' },  // ✅ 新增
];

// ===== 状态 =====
const networkContainer = ref<HTMLElement | null>(null);
let network: Network | null = null;
const selectedNode = ref<any>(null);
const popupStyle = ref({});
const physicsEnabled = ref(true);
const dragStartNode = ref<string | null>(null);

const searchQuery = ref('');
const libraryFilter = ref('all');
const graphFilter = ref('all');
const insertedIds = ref<string[]>([]);

const showRelationDialog = ref(false);
const creatingRelation = ref(false);
const relationSource = ref<any>(null);
const relationTarget = ref<any>(null);
const relationForm = ref({ relationType: '' });

// ===== 数据 =====
const allCards = computed(() => store.cards);
const allRelations = computed(() => store.allRelations);

const getTypeLabel = (type: string) => TYPE_LABELS[type] || type;

const filteredLibraryCards = computed(() => {
  let list = allCards.value;
  if (libraryFilter.value !== 'all') {
    list = list.filter(c => c.type === libraryFilter.value);
  }
  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase();
    list = list.filter(c => c.title.toLowerCase().includes(q));
  }
  return list;
});

const displayedNodes = computed(() => {
  let cards = allCards.value.filter(c => insertedIds.value.includes(c.id));
  if (graphFilter.value !== 'all') {
    cards = cards.filter(c => c.type === graphFilter.value);
  }
  return cards.map(card => ({
    id: card.id,
    label: `${getTypeLabel(card.type)} ${card.title}`,
    type: card.type,
    content: card.content,
    tags: card.tags,
    color: getTypeColor(card.type),
    shape: 'box',
    font: { color: '#ffffff', face: 'sans-serif' },
    size: 40,
  }));
});

const displayedEdges = computed(() => {
  let edges = allRelations.value.filter(rel => 
    insertedIds.value.includes(rel.sourceCardId) && 
    insertedIds.value.includes(rel.targetCardId)
  );
  if (graphFilter.value !== 'all') {
    const filteredCardIds = allCards.value
      .filter(c => c.type === graphFilter.value)
      .map(c => c.id);
    edges = edges.filter(rel => 
      filteredCardIds.includes(rel.sourceCardId) && 
      filteredCardIds.includes(rel.targetCardId)
    );
  }
  return edges.map(rel => ({
    id: rel.id,
    from: rel.sourceCardId,
    to: rel.targetCardId,
    label: rel.relationType,
    font: { align: 'middle', size: 12, color: '#4f46e5' },
    arrows: 'to',
    color: '#94a3b8',
    width: 2,
  }));
});

const getTypeColor = (type: string) => {
  const map: Record<string, string> = {
    character: '#4f46e5',
    location: '#059669',
    item: '#d97706',
    event: '#dc2626',
    ecology: '#10b981',
    faction: '#8b5cf6',
    species: '#f59e0b',
    lore: '#6366f1',
  };
  return map[type] || '#64748b';
};

const getNodePreview = (node: any) => {
  try {
    const data = JSON.parse(node.content || '{}');
    return data.description || data.summary || '';
  } catch {
    return '';
  }
};

const getNodeTags = (node: any) => {
  try {
    return JSON.parse(node.tags || '[]');
  } catch {
    return [];
  }
};

// ===== 卡片插入/移除 =====
const toggleCard = (cardId: string) => {
  const index = insertedIds.value.indexOf(cardId);
  if (index > -1) {
    insertedIds.value.splice(index, 1);
  } else {
    insertedIds.value.push(cardId);
    emit('cardInserted');
  }
  refreshNetwork();
};

// ===== 初始化图谱 =====
const initNetwork = () => {
  if (!networkContainer.value) return;

  const data = {
    nodes: new DataSet(displayedNodes.value),
    edges: new DataSet(displayedEdges.value),
  };

  const options: any = {
    layout: { improvedLayout: true, hierarchical: false },
    physics: {
      enabled: physicsEnabled.value,
      stabilization: { iterations: 200 },
      forceAtlas2Based: {
        gravitationalConstant: -100,
        centralGravity: 0.01,
        springLength: 200,
        springConstant: 0.08,
        damping: 0.4,
      },
    },
    interaction: { hover: true, navigationButtons: false, dragNodes: true, dragView: true },
    nodes: {
      shape: 'box',
      margin: 10,
      widthConstraint: { minimum: 80 },
      borderWidth: 2,
      shadow: { enabled: true },
      font: { face: 'sans-serif', size: 14 },
    },
    edges: {
      smooth: true,
      width: 2,
      color: { color: '#94a3b8', highlight: '#4f46e5' },
      font: { face: 'sans-serif', size: 12, color: '#4f46e5' },
    },
  };

  network = new Network(networkContainer.value, data, options);

  network.on('click', (params) => {
    if (params.nodes.length > 0) {
      const nodeId = params.nodes[0];
      const node = allCards.value.find(c => c.id === nodeId);
      if (node) {
        selectedNode.value = node;
        const pos = network!.getPosition(nodeId);
        if (pos && networkContainer.value) {
          const rect = networkContainer.value.getBoundingClientRect();
          popupStyle.value = {
            left: Math.min(pos.x + rect.width / 2, rect.width - 160) + 'px',
            top: Math.min(pos.y + rect.height / 2 - 100, rect.height - 200) + 'px',
          };
        }
      }
    } else {
      selectedNode.value = null;
    }
  });

  network.on('dragStart', (params) => {
    if (params.nodes.length > 0) dragStartNode.value = params.nodes[0];
  });

  network.on('dragEnd', (params) => {
    if (!dragStartNode.value) return;
    const pointer = params.pointer;
    if (!pointer) { dragStartNode.value = null; return; }
    const nodeAtPointer = (network as any).getNodeAt(pointer);
    if (nodeAtPointer && nodeAtPointer !== dragStartNode.value) {
      const source = allCards.value.find(c => c.id === dragStartNode.value);
      const target = allCards.value.find(c => c.id === nodeAtPointer);
      if (source && target) {
        relationSource.value = source;
        relationTarget.value = target;
        relationForm.value.relationType = '';
        showRelationDialog.value = true;
      }
    }
    dragStartNode.value = null;
  });
};

const resetGraph = () => {
  if (network) network.fit({ animation: true });
};

const togglePhysics = () => {
  physicsEnabled.value = !physicsEnabled.value;
  if (network) network.setOptions({ physics: { enabled: physicsEnabled.value } });
};

const refreshNetwork = () => {
  if (network) {
    network.setData({
      nodes: new DataSet(displayedNodes.value),
      edges: new DataSet(displayedEdges.value),
    });
    network.fit({ animation: true });
  }
};

const handleCreateRelation = async () => {
  if (!relationForm.value.relationType.trim()) {
    ElMessage.warning('请输入关系描述');
    return;
  }
  creatingRelation.value = true;
  try {
    await store.addRelation(
      relationSource.value.id,
      relationTarget.value.id,
      relationForm.value.relationType.trim()
    );
    ElMessage.success('关联已建立');
    showRelationDialog.value = false;
    relationSource.value = null;
    relationTarget.value = null;
    relationForm.value.relationType = '';
    refreshNetwork();
  } catch (error) {
    console.error('建立关联失败:', error);
    ElMessage.error('建立关联失败');
  } finally {
    creatingRelation.value = false;
  }
};

const editCardFromGraph = (card: any) => {
  selectedNode.value = null;
  window.dispatchEvent(new CustomEvent('edit-card', { detail: { cardId: card.id } }));
};

const openCardDetail = (cardId: string) => {
  const card = allCards.value.find(c => c.id === cardId);
  if (card) ElMessage.info(`查看卡片：${card.title}`);
  selectedNode.value = null;
};

onMounted(() => {
  window.addEventListener('edit-card', ((e: CustomEvent) => {
    const cardId = e.detail?.cardId;
    if (cardId) {
      const card = allCards.value.find(c => c.id === cardId);
      if (card) {
        window.dispatchEvent(new CustomEvent('open-card-editor', { detail: { card } }));
      }
    }
  }) as EventListener);

  nextTick(() => initNetwork());
});

onBeforeUnmount(() => {
  window.removeEventListener('edit-card', (() => {}) as EventListener);
  if (network) { network.destroy(); network = null; }
});

defineExpose({ refresh: refreshNetwork });
</script>

<style scoped>
.relation-graph-wrapper {
  display: flex;
  gap: 16px;
  height: 600px;
}

.card-library {
  flex: 0 0 260px;
  background: white;
  border-radius: 16px;
  border: 1px solid #f1f3f5;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.library-header {
  display: flex;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid #f1f3f5;
  background: #fafbfc;
  flex-shrink: 0;
}
.library-title {
  font-weight: 600;
  font-size: 14px;
  color: #0f172a;
}
.library-count {
  font-size: 12px;
  color: #94a3b8;
}
.library-search {
  padding: 10px 12px;
  border-bottom: 1px solid #f1f3f5;
  flex-shrink: 0;
}
.library-search input {
  width: 100%;
  padding: 6px 12px;
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  font-size: 13px;
  background: #fafbfc;
}
.library-search input:focus {
  outline: none;
  border-color: #4f46e5;
}
.library-filter {
  display: flex;
  gap: 4px;
  padding: 8px 12px;
  border-bottom: 1px solid #f1f3f5;
  flex-wrap: wrap;
  flex-shrink: 0;
}
.filter-chip {
  padding: 2px 10px;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  background: white;
  font-size: 11px;
  cursor: pointer;
  transition: all 0.2s;
}
.filter-chip:hover {
  background: #f1f5f9;
}
.filter-chip.active {
  border-color: #4f46e5;
  background: #eef2ff;
  color: #4f46e5;
}
.library-list {
  flex: 1;
  overflow-y: auto;
  padding: 8px 8px 8px 0;
}
.library-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 12px;
  margin: 2px 8px 2px 0;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.15s;
}
.library-item:hover {
  background: #f1f5f9;
}
.library-item.inserted {
  background: #eef2ff;
  border-left: 3px solid #4f46e5;
}
.lib-type-label {
  font-size: 11px;
  color: #4f46e5;
  background: #eef2ff;
  padding: 1px 8px;
  border-radius: 10px;
  font-weight: 500;
  flex-shrink: 0;
}
.lib-title {
  flex: 1;
  font-size: 13px;
  color: #1e293b;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.lib-badge {
  color: #4f46e5;
  font-weight: 700;
  font-size: 14px;
}

.graph-area {
  flex: 1;
  background: white;
  border-radius: 16px;
  border: 1px solid #f1f3f5;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  min-width: 0;
}
.graph-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 16px;
  border-bottom: 1px solid #f1f3f5;
  flex-shrink: 0;
  background: #fafbfc;
  flex-wrap: wrap;
  gap: 6px;
}
.toolbar-left {
  display: flex;
  align-items: center;
  gap: 12px;
}
.graph-title {
  font-weight: 600;
  font-size: 14px;
  color: #0f172a;
}
.graph-info {
  font-size: 12px;
  color: #94a3b8;
}
.toolbar-right {
  display: flex;
  align-items: center;
  gap: 6px;
}
.hint-text {
  font-size: 11px;
  color: #94a3b8;
}
.toolbar-btn {
  padding: 4px 12px;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  background: white;
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s;
}
.toolbar-btn:hover {
  background: #f1f5f9;
}
.graph-filter {
  display: flex;
  gap: 4px;
  padding: 6px 12px;
  border-bottom: 1px solid #f1f3f5;
  flex-wrap: wrap;
  flex-shrink: 0;
  background: #fafbfc;
}
.graph-filter-chip {
  padding: 2px 12px;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  background: white;
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s;
}
.graph-filter-chip:hover {
  background: #f1f5f9;
}
.graph-filter-chip.active {
  border-color: #4f46e5;
  background: #eef2ff;
  color: #4f46e5;
}
.network-container {
  flex: 1;
  min-height: 400px;
  position: relative;
}
.network-container :deep(.vis-network) {
  width: 100%;
  height: 100%;
}
.network-container :deep(.vis-network) .vis-node {
  cursor: pointer;
}

.node-popup {
  position: absolute;
  background: white;
  border-radius: 16px;
  box-shadow: 0 16px 48px rgba(0, 0, 0, 0.15);
  border: 1px solid #f1f3f5;
  padding: 14px 18px;
  width: 240px;
  z-index: 100;
  pointer-events: auto;
  transform: translate(-50%, -100%);
  margin-top: -12px;
}
.popup-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}
.popup-type-label {
  font-size: 11px;
  color: #4f46e5;
  background: #eef2ff;
  padding: 1px 10px;
  border-radius: 10px;
  font-weight: 500;
}
.popup-title {
  font-weight: 600;
  font-size: 15px;
  color: #0f172a;
  flex: 1;
}
.popup-close {
  background: none;
  border: none;
  font-size: 20px;
  color: #94a3b8;
  cursor: pointer;
}
.popup-close:hover { color: #1e293b; }
.popup-desc {
  font-size: 13px;
  color: #64748b;
  line-height: 1.5;
  margin: 0 0 6px 0;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
.popup-tags { display: flex; flex-wrap: wrap; gap: 4px; margin-bottom: 6px; }
.popup-tag { font-size: 10px; color: #4f46e5; background: #eef2ff; padding: 1px 8px; border-radius: 10px; }
.popup-actions { display: flex; gap: 6px; }
.popup-btn {
  padding: 4px 14px;
  border: none;
  border-radius: 8px;
  font-size: 12px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  background: #4f46e5;
  color: white;
}
.popup-btn:hover { background: #4338ca; transform: translateY(-1px); }
.popup-btn.secondary { background: #f1f5f9; color: #475569; }
.popup-btn.secondary:hover { background: #e2e8f0; }

.dialog-overlay {
  position: fixed;
  inset: 0;
  background: rgba(15, 23, 42, 0.4);
  backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}
.dialog {
  background: white;
  border-radius: 24px;
  width: 420px;
  max-width: 94%;
  padding: 24px 28px 20px;
  box-shadow: 0 32px 64px rgba(0, 0, 0, 0.12);
}
.dialog-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}
.dialog-header h2 { margin: 0; font-size: 20px; font-weight: 600; }
.close-btn {
  background: none;
  border: none;
  font-size: 24px;
  color: #94a3b8;
  cursor: pointer;
}
.close-btn:hover { background: #f1f3f5; border-radius: 8px; }
.dialog-body { display: flex; flex-direction: column; gap: 14px; }
.field { display: flex; flex-direction: column; gap: 4px; }
.field label { font-weight: 600; font-size: 14px; color: #334155; }
.field label .required { color: #ef4444; }
.field input {
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 8px 14px;
  font-size: 14px;
  background: #fafbfc;
}
.field input:focus { outline: none; border-color: #4f46e5; background: white; }
.relation-preview {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 10px;
  background: #f8f9fc;
  border-radius: 12px;
  font-size: 14px;
}
.preview-source,
.preview-target { font-weight: 600; color: #0f172a; }
.preview-arrow { color: #94a3b8; font-size: 18px; }
.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 18px;
  padding-top: 14px;
  border-top: 1px solid #f1f3f5;
}
.btn-outline {
  padding: 8px 20px;
  background: transparent;
  border: 1px solid #d1d5db;
  border-radius: 10px;
  font-size: 14px;
  cursor: pointer;
}
.btn-outline:hover { background: #f3f4f6; }
.btn-primary {
  padding: 8px 24px;
  background: #4f46e5;
  color: white;
  border: none;
  border-radius: 10px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
}
.btn-primary:hover:not(:disabled) { background: #4338ca; }
.btn-primary:disabled { opacity: 0.6; cursor: not-allowed; }

@media (max-width: 820px) {
  .relation-graph-wrapper { flex-direction: column; height: auto; }
  .card-library { flex: none; height: 200px; }
  .library-list { height: 100px; }
  .graph-area { height: 450px; }
}
</style>