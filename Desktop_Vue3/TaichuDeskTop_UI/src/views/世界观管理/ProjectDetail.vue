<template>
  <div class="project-detail">
    <!-- ===== 返回 + 项目信息 ===== -->
    <div class="detail-header">
      <button class="back-btn" @click="goBack">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="20" height="20">
          <line x1="19" y1="12" x2="5" y2="12"/>
          <polyline points="12 19 5 12 12 5"/>
        </svg>
        返回
      </button>

      <div class="project-info">
        <div class="info-left">
          <h1>{{ project?.name || '加载中...' }}</h1>
          <div class="meta-tags">
            <span class="tag" :class="project?.isPublic ? 'public' : 'private'">
              {{ project?.isPublic ? '🌐 公开' : '🔒 私有' }}
            </span>
            <span class="meta-item">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="16" height="16">
                <rect x="2" y="3" width="20" height="18" rx="2" ry="2"/>
                <line x1="8" y1="21" x2="16" y2="21"/>
                <line x1="12" y1="17" x2="12" y2="21"/>
              </svg>
              {{ cards.length }} 个条目
            </span>
            <span class="meta-item" v-if="project?.ownerName && isPublicView">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="16" height="16">
                <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/>
                <circle cx="12" cy="7" r="4"/>
              </svg>
              {{ project?.ownerName }}
            </span>
          </div>
        </div>
        <div class="info-right">
          <button class="btn-outline-small" @click="handleEditProject">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="16" height="16">
              <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
              <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
            </svg>
            编辑
          </button>
          <button class="btn-primary-small" @click="openCreateDialog">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" width="16" height="16">
              <line x1="12" y1="5" x2="12" y2="19"/>
              <line x1="5" y1="12" x2="19" y2="12"/>
            </svg>
            新建卡片
          </button>
        </div>
      </div>
      <p v-if="project?.description" class="project-desc">{{ project.description }}</p>
    </div>

    <!-- ===== 视图切换 ===== -->
    <div class="view-toggle">
      <button class="view-tab" :class="{ active: viewMode === 'list' }" @click="viewMode = 'list'">
        <span class="view-icon">📋</span> 列表视图
      </button>
      <button class="view-tab" :class="{ active: viewMode === 'graph' }" @click="viewMode = 'graph'">
        <span class="view-icon">🕸️</span> 关系图谱
      </button>
    </div>

    <!-- ===== 列表视图 ===== -->
    <div v-if="viewMode === 'list'">
      <!-- 筛选栏 -->
      <div class="filter-bar">
        <button
          v-for="tab in tabs"
          :key="tab.value"
          class="filter-tab"
          :class="{ active: activeTab === tab.value }"
          @click="activeTab = tab.value"
        >
          <span class="tab-icon">{{ tab.icon }}</span>
          {{ tab.label }}
          <span class="tab-count">{{ getCountByType(tab.value) }}</span>
        </button>
      </div>

      <!-- 卡片网格 -->
      <div class="card-grid" v-loading="loading">
        <div
          v-for="(card, index) in filteredCards"
          :key="card.id"
          class="card-item"
          :style="{ animationDelay: `${index * 30}ms` }"
          @click="openCardDetail(card)"
        >
          <div class="card-type-icon">{{ getTypeIcon(card.type) }}</div>
          <div class="card-body">
            <div class="card-top">
              <h4>{{ card.title }}</h4>
              <span class="card-type-tag">{{ getTypeLabel(card.type) }}</span>
            </div>
            <span v-if="card.subType" class="card-subtype">· {{ card.subType }}</span>
            <p class="card-preview">{{ getCardPreview(card.content) }}</p>
            <div v-if="card.relations && card.relations.length" class="card-relations">
              <span v-for="rel in card.relations" :key="rel.id" class="relation-badge">
                <span class="rel-target">{{ getCardTitle(rel.targetCardId) }}</span>
                <span class="rel-type">「{{ rel.relationType }}」</span>
              </span>
            </div>
            <div class="card-tags">
              <span v-for="tag in getCardTags(card.tags)" :key="tag" class="card-tag">#{{ tag }}</span>
            </div>
            <div class="card-time">
              <span>创建 {{ formatTime(card.createdAt) }}</span>
              <span>· 更新 {{ formatTime(card.updatedAt) }}</span>
            </div>
          </div>
          <div class="card-actions" @click.stop>
            <button class="action-btn" @click="editCard(card)" title="编辑">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="16" height="16">
                <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
              </svg>
            </button>
            <button class="action-btn delete" @click="handleDeleteCard(card.id)" title="删除">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="16" height="16">
                <polyline points="3 6 5 6 21 6"/>
                <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>
              </svg>
            </button>
          </div>
        </div>

        <div v-if="!loading && filteredCards.length === 0" class="empty-state">
          <div class="empty-icon">📋</div>
          <p>{{ activeTab === 'all' ? '还没有卡片，开始创建吧' : '暂无此类型的卡片' }}</p>
          <button class="btn-outline" @click="openCreateDialog">创建第一个卡片</button>
        </div>
      </div>
    </div>

    <!-- ===== 关系图谱视图 ===== -->
    <div v-else class="graph-view">
      <RelationGraph
        ref="graphRef"
        :project-id="projectId"
        @card-inserted="onCardInserted"
      />
    </div>

    <!-- ===== 创建/编辑卡片弹窗 ===== -->
    <CardEditor
      v-model:visible="showCardDialog"
      :project-id="projectId"
      :card-data="editingCard"
      @saved="onCardSaved"
      @deleted="onCardDeleted"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed, watch, nextTick } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { ElMessage, ElMessageBox } from 'element-plus';
import { useWorldStore } from '../../stores/world';
import CardEditor from './世界观组件/CardEditor.vue';
import RelationGraph from './世界观组件/RelationGraph.vue';

const TYPE_LABELS: Record<string, string> = {
  character: '角色',
  location: '地点',
  item: '物品',
  event: '事件',
  ecology: '生态',
  faction: '派系',
  species: '物种',
  lore: '背景设定',
};

const TYPE_ICONS: Record<string, string> = {
  character: '🧙',
  location: '📍',
  item: '⚔️',
  event: '📖',
  ecology: '🌿',
  faction: '🏛️',
  species: '🐉',
  lore: '📜',
};

const router = useRouter();
const route = useRoute();
const store = useWorldStore();

const projectId = route.params.id as string;
const loading = ref(false);
const activeTab = ref('all');
const showCardDialog = ref(false);
const editingCard = ref<any>(null);
const isPublicView = ref(false);
const viewMode = ref<'list' | 'graph'>('list');
const graphRef = ref<any>(null);

const project = computed(() => store.currentProject);
const cards = computed(() => store.cards);

const tabs = [
  { label: '全部', value: 'all', icon: '📚' },
  { label: '角色', value: 'character', icon: '🧙' },
  { label: '地点', value: 'location', icon: '📍' },
  { label: '物品', value: 'item', icon: '⚔️' },
  { label: '事件', value: 'event', icon: '📖' },
  { label: '生态', value: 'ecology', icon: '🌿' },
  { label: '派系', value: 'faction', icon: '🏛️' },
  { label: '物种', value: 'species', icon: '🐉' },
  { label: '背景设定', value: 'lore', icon: '📜' },
];

const filteredCards = computed(() => {
  if (activeTab.value === 'all') return cards.value;
  return cards.value.filter(c => c.type === activeTab.value);
});

const getCountByType = (type: string) => {
  if (type === 'all') return cards.value.length;
  return cards.value.filter(c => c.type === type).length;
};

const getTypeIcon = (type: string) => TYPE_ICONS[type] || '📄';
const getTypeLabel = (type: string) => TYPE_LABELS[type] || type;

const getCardPreview = (content: string) => {
  try {
    const data = JSON.parse(content);
    return data.description || data.summary || '';
  } catch {
    return '';
  }
};

const getCardTags = (tagsStr?: string | string[]) => {
  if (Array.isArray(tagsStr)) return tagsStr;
  try {
    return JSON.parse(tagsStr || '[]');
  } catch {
    return [];
  }
};

const getCardTitle = (cardId: string) => store.getCardTitle(cardId);

const formatTime = (dateStr: string) => {
  const d = new Date(dateStr);
  const now = new Date();
  const diff = Math.floor((now.getTime() - d.getTime()) / 1000);
  if (diff < 60) return '刚刚';
  if (diff < 3600) return Math.floor(diff / 60) + '分钟前';
  if (diff < 86400) return Math.floor(diff / 3600) + '小时前';
  if (diff < 604800) return Math.floor(diff / 86400) + '天前';
  return d.toLocaleDateString('zh-CN');
};

const loadData = async () => {
  loading.value = true;
  try {
    await store.fetchCards(projectId);
    const publicProj = store.publicProjects.find(p => p.id === projectId);
    if (publicProj) isPublicView.value = true;
  } catch (error) {
    console.error('加载失败:', error);
    ElMessage.error('加载项目失败');
  } finally {
    loading.value = false;
  }
};

const goBack = () => router.push('/world');
const handleEditProject = () => ElMessage.info('项目编辑功能开发中');

const openCardDetail = (card: any) => {
  router.push(`/world/project/${projectId}/card/${card.id}`);
};

// ===== 新建卡片 =====
const openCreateDialog = () => {
  console.log('🚀 点击新建卡片按钮');
  editingCard.value = null;
  showCardDialog.value = true;
  console.log('📌 showCardDialog 设置为：', showCardDialog.value);
  nextTick(() => {
    console.log('📌 强制更新后 showCardDialog:', showCardDialog.value);
  });
};

// ===== 编辑卡片 =====
const editCard = (card: any) => {
  console.log('✏️ 编辑卡片：', card);
  editingCard.value = card;
  showCardDialog.value = true;
  console.log('📌 showCardDialog 设置为：', showCardDialog.value);
  nextTick(() => {
    console.log('📌 强制更新后 showCardDialog:', showCardDialog.value);
  });
};

const handleDeleteCard = async (cardId: string) => {
  try {
    await ElMessageBox.confirm('确定要删除这张卡片吗？此操作不可恢复。', '确认删除', {
      confirmButtonText: '确定删除',
      cancelButtonText: '取消',
      type: 'warning',
    });
    await store.deleteCard(cardId);
    ElMessage.success('卡片已删除');
  } catch (error) {
    if (error !== 'cancel') {
      console.error('删除失败:', error);
      ElMessage.error('删除失败');
    }
  }
};

const onCardSaved = () => {
  console.log('✅ 卡片保存成功，关闭弹窗并刷新');
  showCardDialog.value = false;
  editingCard.value = null;
  loadData();
  if (graphRef.value) graphRef.value.refresh();
};

const onCardDeleted = () => {
  console.log('🗑️ 卡片删除成功，关闭弹窗并刷新');
  showCardDialog.value = false;
  editingCard.value = null;
  loadData();
  if (graphRef.value) graphRef.value.refresh();
};

const onCardInserted = () => {
  loadData();
};

watch(showCardDialog, (val) => {
  console.log('🔄 showCardDialog 变化:', val);
  if (!val) {
    editingCard.value = null;
  }
});

onMounted(() => loadData());
</script>

<style scoped>
.project-detail {
  min-height: 100vh;
  background: #f8f9fc;
  padding: 24px;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}
.back-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: none;
  border: none;
  color: #64748b;
  font-size: 14px;
  cursor: pointer;
  padding: 8px 12px;
  border-radius: 10px;
  transition: all 0.2s;
  margin-bottom: 16px;
}
.back-btn:hover {
  background: #eef2f6;
  color: #1e293b;
}
.detail-header {
  background: white;
  border-radius: 20px;
  padding: 28px 32px;
  margin-bottom: 24px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.02);
  border: 1px solid #f1f3f5;
}
.project-info {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 20px;
  flex-wrap: wrap;
}
.info-left {
  flex: 1;
}
.info-left h1 {
  margin: 0 0 8px 0;
  font-size: 26px;
  font-weight: 700;
  color: #0f172a;
  letter-spacing: -0.5px;
}
.meta-tags {
  display: flex;
  align-items: center;
  gap: 16px;
  flex-wrap: wrap;
}
.tag {
  font-size: 12px;
  font-weight: 500;
  padding: 2px 12px;
  border-radius: 20px;
  height: 26px;
  line-height: 26px;
}
.tag.public {
  background: #dcfce7;
  color: #16a34a;
}
.tag.private {
  background: #f1f3f5;
  color: #64748b;
}
.meta-item {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 14px;
  color: #94a3b8;
}
.meta-item svg {
  stroke: #94a3b8;
}
.info-right {
  display: flex;
  gap: 10px;
  flex-shrink: 0;
}
.btn-outline-small {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: transparent;
  border: 1px solid #d1d5db;
  color: #374151;
  padding: 8px 16px;
  border-radius: 10px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}
.btn-outline-small:hover {
  background: #f3f4f6;
  border-color: #9ca3af;
}
.btn-primary-small {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: #4f46e5;
  color: white;
  border: none;
  padding: 8px 18px;
  border-radius: 10px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}
.btn-primary-small:hover {
  background: #4338ca;
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(79, 70, 229, 0.25);
}
.btn-primary-small:active {
  transform: scale(0.97);
}
.project-desc {
  margin: 12px 0 0 0;
  color: #64748b;
  font-size: 15px;
  line-height: 1.6;
}

.view-toggle {
  display: flex;
  gap: 4px;
  background: white;
  padding: 4px;
  border-radius: 14px;
  margin-bottom: 20px;
  border: 1px solid #f1f3f5;
  width: fit-content;
}
.view-tab {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 20px;
  border: none;
  border-radius: 10px;
  background: transparent;
  color: #64748b;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}
.view-tab:hover {
  background: #f1f5f9;
  color: #1e293b;
}
.view-tab.active {
  background: #eef2ff;
  color: #4f46e5;
}
.view-icon {
  font-size: 16px;
}

.filter-bar {
  display: flex;
  gap: 4px;
  background: white;
  padding: 6px;
  border-radius: 14px;
  margin-bottom: 16px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.02);
  border: 1px solid #f1f3f5;
  flex-wrap: wrap;
}
.filter-tab {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
  border: none;
  border-radius: 10px;
  background: transparent;
  color: #64748b;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}
.filter-tab:hover {
  background: #f1f5f9;
  color: #1e293b;
}
.filter-tab.active {
  background: #eef2ff;
  color: #4f46e5;
}
.tab-icon {
  font-size: 16px;
}
.tab-count {
  font-size: 12px;
  background: #f1f3f5;
  padding: 0 8px;
  border-radius: 10px;
  color: #94a3b8;
}
.filter-tab.active .tab-count {
  background: #c7d2fe;
  color: #4f46e5;
}

.card-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 16px;
}
.card-item {
  background: white;
  border-radius: 16px;
  padding: 18px 20px;
  border: 1px solid #f1f3f5;
  display: flex;
  gap: 14px;
  align-items: flex-start;
  cursor: pointer;
  transition: all 0.25s cubic-bezier(0.25, 0.46, 0.45, 0.94);
  opacity: 0;
  animation: fadeUp 0.35s ease forwards;
  position: relative;
}
.card-item:hover {
  transform: translateY(-3px);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.04);
  border-color: #dbe0e8;
}
.card-item:active {
  transform: scale(0.98);
}
.card-type-icon {
  font-size: 28px;
  flex-shrink: 0;
  width: 40px;
  text-align: center;
  margin-top: 2px;
}
.card-body {
  flex: 1;
  min-width: 0;
}
.card-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
}
.card-top h4 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: #0f172a;
}
.card-type-tag {
  font-size: 11px;
  color: #4f46e5;
  background: #eef2ff;
  padding: 1px 10px;
  border-radius: 12px;
  flex-shrink: 0;
}
.card-subtype {
  font-size: 12px;
  color: #94a3b8;
  margin-top: -2px;
}
.card-preview {
  margin: 4px 0 0 0;
  font-size: 14px;
  color: #64748b;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  line-height: 1.5;
}
.card-relations {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 8px;
  margin-top: 8px;
}
.relation-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  background: #f1f5f9;
  padding: 2px 10px 2px 8px;
  border-radius: 12px;
  color: #475569;
}
.relation-badge .rel-target {
  font-weight: 500;
  color: #0f172a;
}
.relation-badge .rel-type {
  color: #4f46e5;
}
.card-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 8px;
}
.card-tag {
  font-size: 11px;
  color: #4f46e5;
  background: #eef2ff;
  padding: 1px 10px;
  border-radius: 12px;
}
.card-time {
  font-size: 11px;
  color: #c0c4cc;
  margin-top: 8px;
  display: flex;
  gap: 8px;
}
.card-actions {
  display: flex;
  flex-direction: column;
  gap: 4px;
  opacity: 0;
  transition: opacity 0.2s;
  flex-shrink: 0;
}
.card-item:hover .card-actions {
  opacity: 1;
}
.action-btn {
  width: 28px;
  height: 28px;
  border: none;
  border-radius: 8px;
  background: transparent;
  color: #94a3b8;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
}
.action-btn:hover {
  background: #f1f5f9;
  color: #4f46e5;
}
.action-btn.delete:hover {
  background: #fef2f2;
  color: #ef4444;
}
.empty-state {
  grid-column: 1 / -1;
  text-align: center;
  padding: 60px 20px;
}
.empty-icon {
  font-size: 48px;
  margin-bottom: 16px;
}
.empty-state p {
  color: #94a3b8;
  font-size: 16px;
  margin: 0 0 20px 0;
}
.graph-view {
  min-height: 500px;
}

@keyframes fadeUp {
  0% { opacity: 0; transform: translateY(12px); }
  100% { opacity: 1; transform: translateY(0); }
}

@media (max-width: 768px) {
  .project-detail { padding: 12px; }
  .detail-header { padding: 20px; }
  .project-info { flex-direction: column; }
  .info-right { width: 100%; }
  .info-right button { flex: 1; justify-content: center; }
  .filter-bar { gap: 4px; }
  .filter-tab { padding: 6px 12px; font-size: 13px; }
  .card-grid { grid-template-columns: 1fr; }
  .card-actions { opacity: 1; flex-direction: row; }
  .view-toggle { width: 100%; }
  .view-tab { flex: 1; justify-content: center; }
  .graph-view { min-height: 350px; }
}
</style>