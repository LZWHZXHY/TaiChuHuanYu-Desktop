<!-- src/views/柴圈板块/World/pages/ProjectDetail.vue -->
<template>
  <div class="project-detail">
    <!-- ===== 头部 ===== -->
    <header class="detail-header">
      <div class="header-top">
        <button class="back-link" @click="goBack">← 返回</button>
        <div v-if="isOwner" class="header-actions">
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
      <!-- 左栏：搜索 + 分类折叠面板 -->
      <div class="left-panel">
        <!-- 🔍 搜索框 -->
        <div class="search-row">
          <input
            v-model="searchQuery"
            type="text"
            class="search-input"
            placeholder="🔍 搜索卡片标题..."
          />
          <span v-if="searchQuery" class="search-count">
            找到 {{ filteredCards.length }} 个结果
          </span>
          <button v-if="searchQuery" class="clear-search" @click="searchQuery = ''">✕</button>
        </div>

        <!-- 类型快捷筛选（辅助快速跳转） -->
        <div class="filter-row">
          <button
            v-for="tab in tabs"
            :key="tab.value"
            class="filter-chip"
            :class="{ active: activeType === tab.value }"
            @click="scrollToType(tab.value)"
          >
            {{ tab.label }}
            <span class="count">{{ getCountByType(tab.value) }}</span>
          </button>
        </div>

        <!-- 📂 分类折叠面板 -->
        <div class="collapse-wrap" v-loading="loading">
          <el-collapse v-model="activeNames" accordion>
            <el-collapse-item
              v-for="group in groupedCards"
              :key="group.type"
              :name="group.type"
            >
              <template #title>
                <div class="collapse-header">
                  <span class="type-icon">{{ getTypeIcon(group.type) }}</span>
                  <span class="type-label">{{ group.label }}</span>
                  <span class="type-count">{{ group.cards.length }}</span>
                </div>
              </template>

              <!-- 卡片网格 -->
              <div v-if="group.cards.length" class="card-grid">
                <div
                  v-for="card in group.cards"
                  :key="card.id"
                  class="card-item"
                  :class="{ selected: selectedCardId === card.id }"
                  @click="selectCard(card.id)"
                >
                  <!-- 封面图 -->
                  <div class="card-cover">
                    <img
                      v-if="card.coverImage"
                      :src="getCoverImage(card.coverImage)"
                      :alt="card.title"
                      loading="lazy"
                    />
                    <div v-else class="card-cover-placeholder">
                      <span>{{ getTypeIcon(card.type) }}</span>
                    </div>
                    <!-- 关联数徽标 -->
                    <span class="card-rels-badge">
                      {{ card.outRelationCount || 0 }} 🔗
                    </span>
                  </div>
                  <!-- 卡片信息 -->
                  <div class="card-info">
                    <span class="card-title">{{ card.title }}</span>
                    <span class="card-type-tag">{{ getTypeLabel(card.type) }}</span>
                    <span class="card-time">{{ formatTime(card.updatedAt) }}</span>
                  </div>
                  <!-- 操作按钮（仅所有者） -->
                  <div v-if="isOwner" class="card-actions" @click.stop>
                    <button class="card-action edit" @click="editCard(card)">✎</button>
                    <button class="card-action delete" @click="handleDeleteCard(card.id)">✕</button>
                  </div>
                </div>
              </div>
              <div v-else class="empty-group">
                <span>暂无 {{ group.label }}</span>
              </div>
            </el-collapse-item>
          </el-collapse>

          <!-- 无搜索结果 -->
          <div v-if="searchQuery && !filteredCards.length" class="empty-state">
            <p>🔍 没有找到 "{{ searchQuery }}" 相关的卡片</p>
            <button class="btn-outline" @click="searchQuery = ''">清除搜索</button>
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

      <!-- 右栏：编辑/详情面板 -->
      <div class="right-panel" :class="{ open: selectedCardId }">
        <!-- 编辑模式（所有者） -->
        <div v-if="(selectedCard || isCreating) && isOwner" class="panel-inner">
          <div class="panel-header">
            <span class="panel-title">{{ isCreating ? '新建卡片' : selectedCard?.title }}</span>
            <button class="panel-close" @click="closePanel">✕</button>
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

        <!-- 只读模式（非所有者） -->
        <div v-else-if="selectedCardId && !isOwner" class="panel-inner panel-readonly">
          <div class="panel-header">
            <span class="panel-title">{{ selectedCardDetail?.title || '加载中...' }}</span>
            <button class="panel-close" @click="closePanel">✕</button>
          </div>
          <div class="panel-body" v-loading="detailLoading">
            <div v-if="selectedCardDetail" class="readonly-card">
              <div v-if="selectedCardDetail.coverImage" class="readonly-cover">
                <img :src="getCoverImage(selectedCardDetail.coverImage)" :alt="selectedCardDetail.title" />
              </div>
              <div v-if="selectedCardDetail.galleryImages?.length" class="readonly-gallery">
                <div class="readonly-gallery-grid">
                  <img
                    v-for="(img, idx) in selectedCardDetail.galleryImages"
                    :key="idx"
                    :src="img"
                    :alt="`图 ${idx + 1}`"
                    @click="previewImage(idx)"
                  />
                </div>
              </div>
              <div v-if="selectedCardDetail.attributes?.length" class="readonly-attributes">
                <div v-for="attr in selectedCardDetail.attributes" :key="attr.key" class="readonly-attr">
                  <span class="attr-key">{{ attr.key }}</span>
                  <span class="attr-value">{{ attr.value }}</span>
                </div>
              </div>
              <div v-if="selectedCardDetail.description" class="readonly-description">
                {{ selectedCardDetail.description }}
              </div>
              <div v-if="selectedCardDetail.tags?.length" class="readonly-tags">
                <span v-for="tag in selectedCardDetail.tags" :key="tag" class="readonly-tag">#{{ tag }}</span>
              </div>
            </div>
            <div v-else-if="!detailLoading" class="empty-state">
              无法加载卡片详情
            </div>
          </div>
        </div>

        <div v-else class="panel-empty">
          <span>← 选择卡片查看详情</span>
        </div>
      </div>
    </div>

    <!-- 图片预览弹窗 -->
    <el-image-viewer
      v-if="previewVisible"
      :url-list="selectedCardDetail?.galleryImages || []"
      :initial-index="previewIndex"
      @close="previewVisible = false"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed, watch, nextTick } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { ElMessage, ElMessageBox, ElImageViewer } from 'element-plus';
import { useWorldStore } from '../../stores/world';
import { useUserStore } from '@/stores/user';
import CardEditor from './世界观组件/CardEditor.vue';
import RelationGraph from './世界观组件/RelationGraph.vue';
import type { CardDetail } from '@/api/worldApi';

// ===== 类型标签映射 =====
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
  concept: '设定',
};

// ===== 类型图标映射 =====
const TYPE_ICONS: Record<string, string> = {
  character: '🧙',
  location: '📍',
  item: '⚔️',
  event: '📖',
  faction: '🏛️',
  species: '🐉',
  occupation: '🔧',
  organization: '🏢',
  creature: '🦁',
  skill: '⚡',
  climate: '🌤️',
  concept: '📜',
};

const router = useRouter();
const route = useRoute();
const store = useWorldStore();
const userStore = useUserStore();

const projectId = route.params.id as string;
const loading = ref(false);
const searchQuery = ref('');
const activeType = ref('all');
const selectedCardId = ref<string | null>(null);
const isPublicView = ref(false);
const showGraph = ref(false);
const graphRef = ref<any>(null);

// 折叠面板：默认展开第一个有数据的类型
const activeNames = ref<string[]>([]);

// 图片预览
const previewVisible = ref(false);
const previewIndex = ref(0);

// 只读卡片完整数据
const selectedCardDetail = ref<CardDetail | null>(null);
const detailLoading = ref(false);

const project = computed(() => store.currentProject);
const cards = computed(() => store.cards);

const isCreating = computed(() => selectedCardId.value === 'new');

const isOwner = computed(() => {
  const userId = userStore.userInfo?.id;
  const ownerId = store.currentProject?.ownerId;
  if (!userId || !ownerId) return false;
  return userId === ownerId;
});

// ===== tabs 定义 =====
const tabs = [
  { label: '全部', value: 'all' },
  { label: '角色', value: 'character' },
  { label: '地点', value: 'location' },
  { label: '物品', value: 'item' },
  { label: '事件', value: 'event' },
  { label: '派系', value: 'faction' },
  { label: '物种', value: 'species' },
  { label: '职业', value: 'occupation' },
  { label: '组织', value: 'organization' },
  { label: '生物', value: 'creature' },
  { label: '技能', value: 'skill' },
  { label: '气候', value: 'climate' },
  { label: '设定', value: 'concept' },
];

// ===== 搜索过滤 =====
const filteredCards = computed(() => {
  if (!searchQuery.value.trim()) return cards.value;
  const q = searchQuery.value.toLowerCase().trim();
  return cards.value.filter(c => c.title.toLowerCase().includes(q));
});

// ===== 按类型分组 =====
const groupedCards = computed(() => {
  // 先过滤搜索
  let source = filteredCards.value;

  // 如果有类型筛选，只显示该类型
  if (activeType.value !== 'all') {
    source = source.filter(c => c.type === activeType.value);
  }

  // 按类型分组
  const groups: Record<string, any[]> = {};
  tabs.forEach(tab => {
    if (tab.value === 'all') return;
    groups[tab.value] = [];
  });

  source.forEach(card => {
    if (groups[card.type]) {
      groups[card.type].push(card);
    }
  });

  // 转为数组，过滤空组
  return tabs
    .filter(t => t.value !== 'all')
    .map(t => ({
      type: t.value,
      label: t.label,
      cards: groups[t.value] || [],
    }))
    .filter(g => g.cards.length > 0);
});

const getTypeLabel = (type: string) => TYPE_LABELS[type] || type;
const getTypeIcon = (type: string) => TYPE_ICONS[type] || '📄';
const getCountByType = (type: string) => {
  if (type === 'all') return cards.value.length;
  return cards.value.filter(c => c.type === type).length;
};

// ===== 获取封面图 =====
const getCoverImage = (coverImage: string | null) => {
  if (!coverImage) return '';
  try {
    const arr = JSON.parse(coverImage);
    return Array.isArray(arr) && arr.length > 0 ? arr[0] : '';
  } catch {
    return coverImage;
  }
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

// ===== 滚动到指定类型 =====
const scrollToType = (type: string) => {
  activeType.value = type;
  // 如果类型有数据，展开该面板
  const group = groupedCards.value.find(g => g.type === type);
  if (group) {
    activeNames.value = [type];
  }
  // 滚动到面板区域
  const collapseWrap = document.querySelector('.collapse-wrap');
  if (collapseWrap) {
    collapseWrap.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
};

// ===== 提取关联卡片 ID =====
const extractEmbeddedCardIds = (c: any): string[] => {
  const ids: string[] = [];
  if (c.contentBlocks) {
    c.contentBlocks.forEach((block: any) => {
      if (block.cardId && !ids.includes(block.cardId)) {
        ids.push(block.cardId);
      }
    });
  }
  return ids;
};

// ===== 预览图片 =====
const previewImage = (index: number) => {
  previewIndex.value = index;
  previewVisible.value = true;
};

// ===== 关闭面板 =====
const closePanel = () => {
  selectedCardId.value = null;
  selectedCardDetail.value = null;
};

// ===== 数据加载 =====
const loadData = async () => {
  loading.value = true;
  try {
    await store.fetchCards(projectId);
    const publicProj = store.publicProjects.find(p => p.id === projectId);
    if (publicProj) isPublicView.value = true;

    // 初始化折叠面板：展开第一个有数据的类型
    nextTick(() => {
      const groups = groupedCards.value;
      if (groups.length > 0 && activeNames.value.length === 0) {
        activeNames.value = [groups[0].type];
      }
    });
  } finally {
    loading.value = false;
  }
};

// ===== 选中的卡片（列表中的精简数据） =====
const selectedCard = computed(() => {
  if (!selectedCardId.value) return null;
  if (selectedCardId.value === 'new') return null;
  return cards.value.find(c => c.id === selectedCardId.value) || null;
});


// ===== 选择卡片 =====
const selectCard = async (id: string) => {
  if (selectedCardId.value === id) {
    // 同一卡片：强制刷新（不关闭面板）
    detailLoading.value = true;
    try {
      await store.fetchCardDetail(projectId, id, true);
    } catch (error) {
      console.error('刷新卡片失败:', error);
    } finally {
      detailLoading.value = false;
    }
    return;
  }

  // 不同卡片：切换并强制刷新
  selectedCardId.value = id;
  detailLoading.value = true;
  try {
    await store.fetchCardDetail(projectId, id, true);
  } catch (error) {
    console.error('加载卡片详情失败:', error);
  } finally {
    detailLoading.value = false;
  }
};

// ===== 编辑卡片 =====
const editCard = (card: any) => {
  selectedCardId.value = card.id;
};

const handleDeleteCard = async (cardId: string) => {
  try {
    await ElMessageBox.confirm('确定删除吗？', '', { type: 'warning', confirmButtonText: '删除', cancelButtonText: '取消' });
    await store.deleteCard(cardId);
    ElMessage.success('已删除');
    if (selectedCardId.value === cardId) closePanel();
    loadData();
    if (graphRef.value) graphRef.value.refresh();
  } catch (e) { if (e !== 'cancel') console.error(e); }
};

// ===== 新建卡片 =====
const openCreateDialog = () => {
  selectedCardId.value = 'new';
  selectedCardDetail.value = null;
};

const onCardSaved = () => {
  closePanel();
  loadData();
  if (graphRef.value) graphRef.value.refresh();
};

const onCardDeleted = () => {
  closePanel();
  loadData();
  if (graphRef.value) graphRef.value.refresh();
};

const onCardInserted = () => { loadData(); };

const goBack = () => router.push('/world');
const handleEditProject = () => ElMessage.info('编辑功能开发中');

// ===== 监听搜索变化，自动展开匹配的类型 =====
watch(searchQuery, (val) => {
  if (val.trim()) {
    // 搜索时，展开所有有结果的类型
    const groups = groupedCards.value;
    if (groups.length > 0) {
      activeNames.value = groups.map(g => g.type);
    }
  } else {
    // 清除搜索时，恢复只展开第一个
    const groups = groupedCards.value;
    if (groups.length > 0) {
      activeNames.value = [groups[0].type];
    }
  }
});

onMounted(loadData);
</script>

<style scoped>
/* ===== 基础布局 ===== */
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

/* ===== 主体 ===== */
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

/* ===== 搜索框 ===== */
.search-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
  position: relative;
}
.search-input {
  flex: 1;
  padding: 8px 14px;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  font-size: 14px;
  background: #fff;
  transition: border-color 0.2s, box-shadow 0.2s;
}
.search-input:focus {
  outline: none;
  border-color: #4f46e5;
  box-shadow: 0 0 0 3px rgba(79, 70, 229, 0.1);
}
.search-count {
  font-size: 13px;
  color: #94a3b8;
  white-space: nowrap;
}
.clear-search {
  background: none;
  border: none;
  font-size: 16px;
  color: #94a3b8;
  cursor: pointer;
  padding: 0 4px;
}
.clear-search:hover { color: #1e293b; }

/* ===== 筛选标签 ===== */
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

/* ===== 折叠面板 ===== */
.collapse-wrap {
  background: #fff;
  border-radius: 8px;
  border: 1px solid #eef2f6;
  overflow: hidden;
  flex: 1;
  max-height: 600px;
  overflow-y: auto;
}

.collapse-wrap :deep(.el-collapse) {
  border: none;
}
.collapse-wrap :deep(.el-collapse-item) {
  border-bottom: 1px solid #f4f6f8;
}
.collapse-wrap :deep(.el-collapse-item:last-child) {
  border-bottom: none;
}
.collapse-wrap :deep(.el-collapse-item__header) {
  padding: 10px 16px;
  background: #fafbfc;
  border: none;
  font-size: 14px;
  font-weight: 500;
}
.collapse-wrap :deep(.el-collapse-item__header:hover) {
  background: #f1f5f9;
}
.collapse-wrap :deep(.el-collapse-item__wrap) {
  border: none;
}
.collapse-wrap :deep(.el-collapse-item__content) {
  padding: 12px 16px;
}

.collapse-header {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
}
.type-icon { font-size: 18px; }
.type-label { font-size: 14px; color: #1e293b; }
.type-count {
  font-size: 12px;
  color: #94a3b8;
  background: #f1f5f9;
  padding: 0 8px;
  border-radius: 10px;
  margin-left: auto;
}

/* ===== 卡片网格 ===== */
.card-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 12px;
}

.card-item {
  background: #fff;
  border: 1px solid #eef2f6;
  border-radius: 10px;
  overflow: hidden;
  cursor: pointer;
  transition: all 0.2s ease;
  position: relative;
}
.card-item:hover {
  border-color: #cbd5e1;
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.06);
}
.card-item.selected {
  border-color: #4f46e5;
  box-shadow: 0 0 0 2px rgba(79, 70, 229, 0.2);
}

.card-cover {
  position: relative;
  aspect-ratio: 16/10;
  background: #f1f5f9;
  overflow: hidden;
}
.card-cover img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}
.card-cover-placeholder {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 32px;
  color: #cbd5e1;
}

.card-rels-badge {
  position: absolute;
  top: 6px;
  right: 6px;
  background: rgba(0, 0, 0, 0.6);
  color: #fff;
  font-size: 11px;
  padding: 2px 8px;
  border-radius: 10px;
  backdrop-filter: blur(4px);
}

.card-info {
  padding: 8px 10px;
  display: flex;
  flex-direction: column;
  gap: 3px;
}
.card-title {
  font-weight: 500;
  font-size: 14px;
  color: #0f172a;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.card-type-tag {
  font-size: 11px;
  color: #4f46e5;
  background: #eef2ff;
  padding: 0 8px;
  border-radius: 3px;
  align-self: flex-start;
}
.card-time {
  font-size: 11px;
  color: #c0c4cc;
}

.card-actions {
  position: absolute;
  top: 6px;
  right: 32px;
  display: flex;
  gap: 4px;
  opacity: 0;
  transition: opacity 0.2s;
}
.card-item:hover .card-actions {
  opacity: 1;
}
.card-action {
  width: 24px;
  height: 24px;
  border: none;
  border-radius: 50%;
  font-size: 12px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(255, 255, 255, 0.9);
  color: #64748b;
  transition: all 0.2s;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.1);
}
.card-action.edit:hover {
  background: #eef2ff;
  color: #4f46e5;
}
.card-action.delete:hover {
  background: #fee2e2;
  color: #ef4444;
}

.empty-group {
  padding: 20px;
  text-align: center;
  color: #94a3b8;
  font-size: 13px;
}
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

/* ===== 图谱 ===== */
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

/* ===== 右侧面板 ===== */
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

/* ===== 只读卡片样式 ===== */
.readonly-card {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.readonly-cover img {
  width: 100%;
  max-height: 200px;
  object-fit: cover;
  border-radius: 8px;
}
.readonly-gallery-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(80px, 1fr));
  gap: 8px;
}
.readonly-gallery-grid img {
  aspect-ratio: 1;
  object-fit: cover;
  border-radius: 6px;
  cursor: pointer;
  border: 1px solid #eef2f6;
  transition: transform 0.2s;
}
.readonly-gallery-grid img:hover {
  transform: scale(1.03);
}
.readonly-attributes {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.readonly-attr {
  display: flex;
  gap: 12px;
  padding: 4px 0;
  border-bottom: 1px solid #f4f6f8;
}
.readonly-attr .attr-key {
  font-weight: 500;
  color: #64748b;
  font-size: 13px;
  min-width: 80px;
}
.readonly-attr .attr-value {
  color: #1e293b;
  font-size: 13px;
}
.readonly-description {
  font-size: 14px;
  line-height: 1.8;
  color: #1e293b;
  white-space: pre-wrap;
}
.readonly-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}
.readonly-tag {
  font-size: 12px;
  color: #4f46e5;
  background: #eef2ff;
  padding: 2px 12px;
  border-radius: 4px;
}

/* ===== 滚动条 ===== */
.collapse-wrap::-webkit-scrollbar {
  width: 4px;
}
.collapse-wrap::-webkit-scrollbar-track {
  background: transparent;
}
.collapse-wrap::-webkit-scrollbar-thumb {
  background: #d1d5db;
  border-radius: 4px;
}
.panel-body::-webkit-scrollbar {
  width: 4px;
}
.panel-body::-webkit-scrollbar-track {
  background: transparent;
}
.panel-body::-webkit-scrollbar-thumb {
  background: #d1d5db;
  border-radius: 4px;
}

/* ===== 响应式 ===== */
@media (max-width: 820px) {
  .detail-body { flex-direction: column; }
  .right-panel { width: 100% !important; margin-left: 0 !important; height: 400px; }
  .right-panel.open { width: 100% !important; height: 400px; }
  .card-grid {
    grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
  }
  .card-actions {
    opacity: 1;
  }
}

@media (max-width: 640px) {
  .card-grid {
    grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
  }
  .readonly-gallery-grid {
    grid-template-columns: repeat(auto-fill, minmax(60px, 1fr));
  }
  .search-row {
    flex-wrap: wrap;
  }
  .search-count {
    font-size: 12px;
  }
}
</style>