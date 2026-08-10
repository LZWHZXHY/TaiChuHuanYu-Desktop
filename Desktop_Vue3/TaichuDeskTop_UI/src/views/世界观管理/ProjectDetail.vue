<!-- src/views/柴圈板块/World/pages/ProjectDetail.vue -->
<template>
  <div class="project-detail">
    <!-- ===== 头部 ===== -->
    <header class="detail-header">
      <div class="header-top">
        <button class="back-link" @click="goBack">← 返回</button>
        <div class="header-actions">
          <button class="action-link" @click="handleEditProject">编辑项目</button>
          <button class="btn-primary" @click="openCreateDialog">+ 新建卡片</button>
        </div>
      </div>
      <div class="header-bottom">
        <h1>{{ project?.name || '加载中...' }}</h1>
        <div class="header-meta">
          <span class="status" :class="project?.isPublic ? 'public' : 'private'">
            <span class="dot"></span>
            {{ project?.isPublic ? '公开' : '私有' }}
          </span>
          <span class="sep">·</span>
          <span>{{ cards.length }} 个条目</span>
          <span class="sep">·</span>
          <span v-if="project?.ownerName && isPublicView">作者 {{ project?.ownerName }}</span>
          <span v-else>更新于 {{ formatTime(project?.updatedAt || '') }}</span>
        </div>
        <p v-if="project?.description" class="project-desc">{{ project.description }}</p>
      </div>
    </header>

    <!-- ===== 主体：双栏 ===== -->
    <div class="detail-body">
      <!-- 左栏：卡片列表 -->
      <div class="left-panel">
        <!-- 类型筛选 -->
        <div class="filter-row">
          <button
            v-for="tab in tabs"
            :key="tab.value"
            class="filter-chip"
            :class="{ active: activeTab === tab.value }"
            @click="activeTab = tab.value"
          >
            {{ tab.label }}
            <span class="count">{{ getCountByType(tab.value) }}</span>
          </button>
        </div>

        <!-- 卡片列表（表格风格） -->
        <div class="table-wrap" v-loading="loading">
          <div v-if="filteredCards.length" class="table">
            <div class="table-head">
              <span class="col-title">标题</span>
              <span class="col-type">类型</span>
              <span class="col-rels">关联</span>
              <span class="col-time">更新</span>
            </div>
            <div
              v-for="card in filteredCards"
              :key="card.id"
              class="table-row"
              :class="{ active: selectedCardId === card.id }"
              @click="selectCard(card.id)"
            >
              <span class="col-title">{{ card.title }}</span>
              <span class="col-type">{{ getTypeLabel(card.type) }}</span>
              <span class="col-rels">{{ (card.relations?.length || 0) }}</span>
              <span class="col-time">{{ formatTime(card.updatedAt) }}</span>
              <div class="row-actions" @click.stop>
                <button class="row-action" @click="editCard(card)">编辑</button>
                <button class="row-action danger" @click="handleDeleteCard(card.id)">删除</button>
              </div>
            </div>
          </div>
          <div v-else class="empty-state">
            <p>{{ activeTab === 'all' ? '还没有卡片' : '暂无此类型' }}</p>
            <button class="btn-outline" @click="openCreateDialog">+ 创建卡片</button>
          </div>
        </div>

        <!-- 图谱折叠 -->
        <div class="graph-toggle" @click="showGraph = !showGraph">
          <span>关系图谱</span>
          <span class="toggle-arrow">{{ showGraph ? '▾' : '▸' }}</span>
        </div>
        <div v-if="showGraph" class="graph-wrapper">
          <RelationGraph ref="graphRef" :project-id="projectId" @card-inserted="onCardInserted" />
        </div>
      </div>

      <!-- 右栏：编辑面板 -->
      <div class="right-panel" :class="{ open: selectedCardId }">
        <!-- 🔧 修复：当 isCreating 为 true 时也显示编辑器 -->
        <div v-if="selectedCard || isCreating" class="panel-inner">
          <!-- 新建模式时，panel-title 显示 "新建卡片" -->
          <div class="panel-header">
            <span class="panel-title">{{ isCreating ? '新建卡片' : selectedCard?.title }}</span>
            <button class="panel-close" @click="selectedCardId = null">✕</button>
          </div>
          <div class="panel-body">
            <CardEditor
              :project-id="projectId"
              :card-data="isCreating ? null : selectedCard"
              @saved="onCardSaved"
              @deleted="onCardDeleted"
            />
          </div>
        </div>
        <div v-else class="panel-empty">
          <span>← 选择卡片开始编辑</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { ElMessage, ElMessageBox } from 'element-plus';
import { useWorldStore } from '../../stores/world';
import CardEditor from './世界观组件/CardEditor.vue';
import RelationGraph from './世界观组件/RelationGraph.vue';

// ===== 🔥 更新类型标签映射 =====
const TYPE_LABELS: Record<string, string> = {
  character: '角色',
  location: '地点',
  item: '物品',
  event: '事件',
  ecology: '生态',
  faction: '派系',
  species: '物种',
  lore: '背景设定',
  occupation: '职业',
  nation: '国家',
  continent: '大陆',
  organization: '组织',
  creature: '生物',
  building: '建筑',
  weapon: '武器',
  deity: '神祇',
  skill: '技能',
  climate: '气候', // ✅ 新增
};

const router = useRouter();
const route = useRoute();
const store = useWorldStore();

const projectId = route.params.id as string;
const loading = ref(false);
const activeTab = ref('all');
const selectedCardId = ref<string | null>(null);
const isPublicView = ref(false);
const showGraph = ref(false);
const graphRef = ref<any>(null);

const project = computed(() => store.currentProject);
const cards = computed(() => store.cards);

// ===== 🔥 判断当前是否新建模式 =====
const isCreating = computed(() => selectedCardId.value === 'new');

// ===== 🔥 更新 tabs =====
const tabs = [
  { label: '全部', value: 'all' },
  { label: '角色', value: 'character' },
  { label: '地点', value: 'location' },
  { label: '物品', value: 'item' },
  { label: '事件', value: 'event' },
  { label: '派系', value: 'faction' },
  { label: '物种', value: 'species' },
  { label: '生态', value: 'ecology' },
  { label: '背景', value: 'lore' },
  { label: '职业', value: 'occupation' },
  { label: '国家', value: 'nation' },
  { label: '大陆', value: 'continent' },
  { label: '组织', value: 'organization' },
  { label: '生物', value: 'creature' },
  { label: '建筑', value: 'building' },
  { label: '武器', value: 'weapon' },
  { label: '神祇', value: 'deity' },
  { label: '技能', value: 'skill' },
  { label: '气候', value: 'climate' }, // ✅ 新增
];

const filteredCards = computed(() => {
  if (activeTab.value === 'all') return cards.value;
  return cards.value.filter(c => c.type === activeTab.value);
});

// ===== 选中的卡片（用于编辑） =====
const selectedCard = computed(() => {
  if (!selectedCardId.value) return null;
  // 如果是新建模式，返回 null
  if (selectedCardId.value === 'new') return null;
  return cards.value.find(c => c.id === selectedCardId.value) || null;
});

const getTypeLabel = (type: string) => TYPE_LABELS[type] || type;
const getCountByType = (type: string) => {
  if (type === 'all') return cards.value.length;
  return cards.value.filter(c => c.type === type).length;
};

const formatTime = (dateStr: string) => {
  if (!dateStr) return '';
  const d = new Date(dateStr);
  const now = new Date();
  const diff = Math.floor((now.getTime() - d.getTime()) / 1000);
  if (diff < 60) return '刚刚';
  if (diff < 3600) return Math.floor(diff / 60) + 'm';
  if (diff < 86400) return Math.floor(diff / 3600) + 'h';
  if (diff < 604800) return Math.floor(diff / 86400) + 'd';
  return d.toLocaleDateString('zh-CN');
};

// ===== 工具函数：提取关联卡片 ID =====
const extractEmbeddedCardIds = (c: any): string[] => {
  const ids: string[] = [];
  if (c.contentBlocks) {
    c.contentBlocks.forEach((block: any) => {
      if (block.cardId && !ids.includes(block.cardId)) {
        ids.push(block.cardId);
      }
    });
  }
  const desc = c.description || '';
  const matches = desc.match(/\{CARD:([^}]+)\}/g);
  if (matches) {
    matches.forEach((m: string) => {
      const id = m.slice(6, -1);
      if (!ids.includes(id)) ids.push(id);
    });
  }
  return ids;
};

// ===== 数据加载 =====
const loadData = async () => {
  loading.value = true;
  try {
    await store.fetchCards(projectId);
    const publicProj = store.publicProjects.find(p => p.id === projectId);
    if (publicProj) isPublicView.value = true;
  } finally {
    loading.value = false;
  }
};

// ===== 选择卡片（加载关联卡片） =====
const selectCard = async (id: string) => {
  if (selectedCardId.value === id) {
    selectedCardId.value = null;
    return;
  }

  selectedCardId.value = id;

  // 🔧 加载关联卡片
  const card = cards.value.find(c => c.id === id);
  if (card) {
    const ids = extractEmbeddedCardIds(card);
    if (ids.length > 0) {
      const existingIds = cards.value.map(c => c.id);
      const missingIds = ids.filter(cid => !existingIds.includes(cid));
      if (missingIds.length > 0) {
        await store.fetchCardsByIds(projectId, missingIds);
      }
    }
  }
};

// ===== 编辑卡片 =====
const editCard = (card: any) => { selectedCardId.value = card.id; };

const handleDeleteCard = async (cardId: string) => {
  try {
    await ElMessageBox.confirm('确定删除吗？', '', { type: 'warning', confirmButtonText: '删除', cancelButtonText: '取消' });
    await store.deleteCard(cardId);
    ElMessage.success('已删除');
    if (selectedCardId.value === cardId) selectedCardId.value = null;
    loadData();
    if (graphRef.value) graphRef.value.refresh();
  } catch (e) { if (e !== 'cancel') console.error(e); }
};

// ===== 新建卡片 =====
const openCreateDialog = () => {
  selectedCardId.value = 'new';
};

const onCardSaved = () => {
  selectedCardId.value = null;
  loadData();
  if (graphRef.value) graphRef.value.refresh();
};

const onCardDeleted = () => {
  selectedCardId.value = null;
  loadData();
  if (graphRef.value) graphRef.value.refresh();
};

const onCardInserted = () => { loadData(); };

const goBack = () => router.push('/world');
const handleEditProject = () => ElMessage.info('编辑功能开发中');

onMounted(loadData);
</script>

<style scoped>
/* 样式保持不变，与之前相同 */
.project-detail {
  min-height: 100vh;
  background: #fafbfc;
  padding: 0;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}

.detail-header {
  background: #fff;
  padding: 20px 0 16px;
  border-bottom: 1px solid #eef2f6;
  margin-bottom: 20px;
}
.header-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}
.back-link {
  background: none;
  border: none;
  color: #94a3b8;
  font-size: 14px;
  cursor: pointer;
  padding: 4px 0;
}
.back-link:hover { color: #1e293b; }
.header-actions {
  display: flex;
  gap: 16px;
  align-items: center;
}
.action-link {
  background: none;
  border: none;
  color: #94a3b8;
  font-size: 13px;
  cursor: pointer;
}
.action-link:hover { color: #1e293b; }
.btn-primary {
  background: #0f172a;
  color: #fff;
  border: none;
  padding: 5px 14px;
  border-radius: 6px;
  font-size: 13px;
  cursor: pointer;
}
.btn-primary:hover { background: #1e293b; }

.header-bottom h1 {
  font-size: 26px;
  font-weight: 600;
  margin: 0 0 6px;
  color: #0f172a;
  letter-spacing: -0.3px;
}
.header-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: #94a3b8;
}
.header-meta .sep { color: #e2e8f0; }
.status {
  display: inline-flex;
  align-items: center;
  gap: 5px;
}
.status .dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
}
.status.public .dot { background: #22c55e; }
.status.private .dot { background: #94a3b8; }
.project-desc {
  margin: 8px 0 0;
  color: #64748b;
  font-size: 14px;
  line-height: 1.6;
}

.detail-body {
  display: flex;
  gap: 24px;
  min-height: 500px;
}

.left-panel {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.filter-row {
  display: flex;
  gap: 4px;
  margin-bottom: 12px;
  flex-wrap: wrap;
}
.filter-chip {
  padding: 4px 12px;
  border: none;
  border-radius: 4px;
  background: transparent;
  font-size: 13px;
  color: #94a3b8;
  cursor: pointer;
  transition: 0.15s;
}
.filter-chip:hover { background: #f1f5f9; color: #1e293b; }
.filter-chip.active {
  background: #eef2ff;
  color: #4f46e5;
}
.filter-chip .count {
  font-size: 11px;
  color: #c0c4cc;
  margin-left: 4px;
}
.filter-chip.active .count { color: #4f46e5; }

.table-wrap {
  background: #fff;
  border-radius: 8px;
  border: 1px solid #eef2f6;
  overflow: hidden;
  flex: 1;
}
.table {
  width: 100%;
}
.table-head {
  display: grid;
  grid-template-columns: 3fr 80px 60px 100px 100px;
  padding: 6px 16px;
  background: #f8fafc;
  font-size: 11px;
  font-weight: 500;
  color: #94a3b8;
  border-bottom: 1px solid #eef2f6;
  text-transform: uppercase;
  letter-spacing: 0.3px;
}
.table-row {
  display: grid;
  grid-template-columns: 3fr 80px 60px 100px 100px;
  padding: 8px 16px;
  font-size: 14px;
  color: #1e293b;
  cursor: pointer;
  border-bottom: 1px solid #f4f6f8;
  align-items: center;
  transition: background 0.1s;
}
.table-row:hover { background: #fafbfc; }
.table-row.active {
  background: #f0f4ff;
  border-left: 2px solid #4f46e5;
}
.table-row:last-child { border-bottom: none; }

.col-title {
  font-weight: 500;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.col-type { font-size: 13px; color: #64748b; }
.col-rels { font-size: 13px; color: #94a3b8; }
.col-time { font-size: 13px; color: #c0c4cc; }

.row-actions {
  display: flex;
  gap: 8px;
  opacity: 0;
  transition: opacity 0.15s;
}
.table-row:hover .row-actions { opacity: 1; }
.row-action {
  background: none;
  border: none;
  font-size: 12px;
  color: #94a3b8;
  cursor: pointer;
  padding: 0;
}
.row-action:hover { color: #1e293b; }
.row-action.danger:hover { color: #ef4444; }

.empty-state {
  padding: 40px 20px;
  text-align: center;
  color: #94a3b8;
}
.empty-state p { margin: 0 0 12px; }
.btn-outline {
  background: transparent;
  border: 1px solid #e2e8f0;
  padding: 4px 14px;
  border-radius: 6px;
  font-size: 13px;
  cursor: pointer;
}
.btn-outline:hover { background: #f1f5f9; }

.graph-toggle {
  display: flex;
  justify-content: space-between;
  padding: 8px 4px;
  margin-top: 12px;
  font-size: 13px;
  color: #94a3b8;
  cursor: pointer;
  border-top: 1px solid #eef2f6;
}
.graph-toggle:hover { color: #1e293b; }
.toggle-arrow { font-size: 12px; }
.graph-wrapper {
  margin-top: 8px;
  background: #fff;
  border-radius: 8px;
  border: 1px solid #eef2f6;
  padding: 8px;
  height: 260px;
}

.right-panel {
  width: 0;
  overflow: hidden;
  background: #fff;
  border-radius: 8px;
  border: 1px solid #eef2f6;
  transition: width 0.3s cubic-bezier(0.22, 1, 0.36, 1), padding 0.3s, margin 0.3s;
  flex-shrink: 0;
}
.right-panel.open {
  width: 50%;
  padding: 0;
  margin-left: 24px;
}
.panel-inner {
  height: 100%;
  display: flex;
  flex-direction: column;
}
.panel-header {
  display: flex;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid #eef2f6;
  flex-shrink: 0;
}
.panel-title {
  font-weight: 500;
  font-size: 15px;
}
.panel-close {
  background: none;
  border: none;
  font-size: 18px;
  color: #94a3b8;
  cursor: pointer;
}
.panel-close:hover { color: #1e293b; }
.panel-body {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
}
.panel-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #94a3b8;
  font-size: 14px;
}

@media (max-width: 820px) {
  .detail-body { flex-direction: column; }
  .right-panel { width: 100% !important; margin-left: 0 !important; height: 400px; }
  .right-panel.open { width: 100% !important; height: 400px; }
  .table-head, .table-row {
    grid-template-columns: 2fr 60px 50px 60px;
  }
  .col-time { display: none; }
  .row-actions { opacity: 1; }
}
</style>