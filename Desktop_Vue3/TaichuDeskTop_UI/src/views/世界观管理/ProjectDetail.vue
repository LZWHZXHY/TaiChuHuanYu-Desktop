<!-- src/views/柴圈板块/World/pages/ProjectDetail.vue -->
<template>
  <div class="project-detail">
    <!-- ========== 顶部 ========== -->
    <header class="page-header">
      <button class="back-btn" @click="goBack">← 返回</button>

      <div class="header-main">
        <h1>{{ project?.name || '加载中...' }}</h1>
        <div class="header-meta">
          <span>{{ project?.isPublic ? '公开' : '私有' }}</span>
          <span class="dot">·</span>
          <span>{{ cards.length }} 个条目</span>
          <span class="dot">·</span>
          <span v-if="isPublicView && project?.ownerName">
            作者 {{ project.ownerName }}
          </span>
          <span v-else>
            更新于 {{ formatTime(project?.updatedAt || '') }}
          </span>
        </div>
        <p v-if="project?.description" class="header-desc">
          {{ project.description }}
        </p>
      </div>

      <div v-if="isOwner" class="header-actions">
        <button class="btn-ghost" @click="openCreateDialog">
          <span class="btn-icon">+</span> 新建
        </button>
      </div>
    </header>

    <!-- ========== 主体 ========== -->
    <div class="layout">
      <!-- 左侧栏 -->
      <aside class="sidebar">
        <div class="search">
          <input
            v-model="searchQuery"
            type="text"
            placeholder="搜索..."
          />
        </div>

        <nav class="tree" v-loading="loading">
          <div
            v-for="group in groupedCards"
            :key="group.type"
            class="tree-group"
          >
            <div class="tree-group-label">
              <span>{{ group.label }}</span>
              <span class="group-count">{{ group.cards.length }}</span>
            </div>
            <div class="tree-items">
              <button
                v-for="card in group.cards"
                :key="card.id"
                class="tree-item"
                :class="{ active: selectedCardId === card.id }"
                @click="selectCard(card.id)"
              >
                {{ card.title }}
              </button>
            </div>
          </div>

          <div v-if="!groupedCards.length && !loading" class="tree-empty">
            {{ searchQuery ? '没有匹配的卡片' : '还没有卡片' }}
          </div>
        </nav>

        <div class="sidebar-footer">
          <button
            class="graph-btn"
            :class="{ active: view === 'graph' }"
            @click="switchToGraph"
          >
            <span class="btn-icon">🕸️</span> 关系图谱
          </button>
        </div>
      </aside>

      <!-- 主区域 -->
      <main class="main">
        <!-- 图谱视图 -->
        <div v-if="view === 'graph'" class="graph-view">
          <RelationGraph
            ref="graphRef"
            :project-id="projectId"
            @card-inserted="onCardInserted"
          />
        </div>

        <!-- 编辑器视图 -->
        <div
          v-else-if="isOwner && (isCreating || selectedCard)"
          class="editor-view"
        >
          <div class="editor-topbar">
            <button class="editor-back" @click="closePanel">← 返回</button>
            <span class="editor-title">
              {{ isCreating ? '新建卡片' : selectedCard?.title }}
            </span>
          </div>
          <div class="editor-body">
            <CardEditor
              :project-id="projectId"
              :card-data="isCreating ? null : selectedCard"
              @saved="onCardSaved"
              @deleted="onCardDeleted"
            />
          </div>
        </div>

        <!-- 只读详情 -->
        <div v-else-if="selectedCardDetail" class="doc-view">
          <article class="doc">
            <!-- 标题 -->
            <header class="doc-header">
              <h1 class="doc-title">{{ selectedCardDetail.title }}</h1>
              <div class="doc-meta">
                <span class="doc-type">
                  {{ getTypeLabel(selectedCardDetail.type) }}
                </span>
                <span class="dot">·</span>
                <span>更新于 {{ formatDate(selectedCardDetail.updatedAt) }}</span>
              </div>
            </header>

            <!-- 封面 -->
            <div v-if="selectedCardDetail.coverImage" class="doc-cover">
              <img
                :src="getCoverImage(selectedCardDetail.coverImage)"
                :alt="selectedCardDetail.title"
                @click="previewCover"
              />
            </div>

            <!-- 描述 -->
            <section class="doc-section">
              <div
                v-if="selectedCardDetail.description"
                class="prose"
                v-html="renderedDescription"
              ></div>
              <p v-else class="empty-note">暂无描述</p>
            </section>

            <!-- 图库 -->
            <section
              v-if="selectedCardDetail.galleryImages?.length"
              class="doc-section"
            >
              <h2 class="section-title">图库</h2>
              <div class="gallery">
                <img
                  v-for="(img, idx) in selectedCardDetail.galleryImages"
                  :key="idx"
                  :src="img"
                  :alt="`图 ${idx + 1}`"
                  loading="lazy"
                  @click="previewGallery(idx)"
                />
              </div>
            </section>

            <!-- 属性 -->
            <section class="doc-section">
              <h2 class="section-title">属性</h2>
              <div v-if="selectedCardDetail.attributes?.length" class="attr-list">
                <div
                  v-for="attr in selectedCardDetail.attributes"
                  :key="attr.key"
                  class="attr-item"
                >
                  <span class="attr-key">{{ attr.key }}</span>
                  <span class="attr-value">{{ attr.value }}</span>
                </div>
              </div>
              <p v-else class="empty-note">暂无属性</p>
            </section>

            <!-- 关联内容（内联展开） -->
            <section class="doc-section">
              <h2 class="section-title">
                关联内容
                <span v-if="embeddedCards.length" class="section-count">
                  {{ embeddedCards.length }}
                </span>
              </h2>
              <div v-if="embeddedCards.length" class="embedded-list">
                <EmbeddedCard
                  v-for="item in embeddedCards"
                  :key="item.id"
                  :card="item"
                  :project-id="projectId"
                  clickable
                  @click="goToCard(item.id)"
                />
              </div>
              <p v-else class="empty-note">暂无关联内容</p>
            </section>

            <!-- 标签 -->
            <section v-if="cardTags.length" class="doc-section">
              <h2 class="section-title">标签</h2>
              <div class="tags">
                <span v-for="tag in cardTags" :key="tag" class="tag">
                  #{{ tag }}
                </span>
              </div>
            </section>
          </article>
        </div>

        <!-- 空状态 -->
        <div v-else class="empty-state">
          <template v-if="cards.length">
            <p>从左侧选择一张卡片</p>
          </template>
          <template v-else>
            <p>这个项目还没有内容</p>
            <button
              v-if="isOwner"
              class="btn-primary"
              @click="openCreateDialog"
            >
              创建第一张卡片
            </button>
          </template>
        </div>
      </main>
    </div>

    <!-- 图片预览 -->
    <el-image-viewer
      v-if="previewVisible"
      :url-list="previewList"
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
import EmbeddedCard from '@/views/世界观管理/世界观组件/EmbeddedCard.vue';
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

const router = useRouter();
const route = useRoute();
const store = useWorldStore();
const userStore = useUserStore();

const projectId = route.params.id as string;
const loading = ref(false);
const searchQuery = ref('');
const selectedCardId = ref<string | null>(null);
const isPublicView = ref(false);
const view = ref<'card' | 'graph'>('card');
const graphRef = ref<any>(null);

// 图片预览
const previewVisible = ref(false);
const previewList = ref<string[]>([]);
const previewIndex = ref(0);

// 只读卡片完整数据
const selectedCardDetail = ref<CardDetail | null>(null);
const detailLoading = ref(false);

const project = computed(() => store.currentProject);
const cards = computed(() => store.cards);

const isCreating = computed(() => selectedCardId.value === 'new');
// ===== 选中的卡片（列表中的精简数据） =====
const selectedCard = computed(() => {
  if (!selectedCardId.value) return null;
  if (selectedCardId.value === 'new') return null;
  return cards.value.find(c => c.id === selectedCardId.value) || null;
});
const isOwner = computed(() => {
  const userId = userStore.userInfo?.id;
  const ownerId = store.currentProject?.ownerId;
  if (!userId || !ownerId) return false;
  return userId === ownerId;
});

// ===== 分组顺序 =====
const TYPE_ORDER = [
  'character', 'location', 'item', 'event',
  'faction', 'species', 'occupation', 'organization',
  'creature', 'skill', 'climate', 'concept',
];

// ===== 搜索过滤 =====
const filteredCards = computed(() => {
  if (!searchQuery.value.trim()) return cards.value;
  const q = searchQuery.value.toLowerCase().trim();
  return cards.value.filter(c => c.title.toLowerCase().includes(q));
});

// ===== 按类型分组 =====
const groupedCards = computed(() => {
  const groups: Record<string, any[]> = {};
  TYPE_ORDER.forEach(type => (groups[type] = []));

  filteredCards.value.forEach(card => {
    if (groups[card.type]) {
      groups[card.type].push(card);
    }
  });

  return TYPE_ORDER
    .map(type => ({
      type,
      label: TYPE_LABELS[type] || type,
      cards: groups[type] || [],
    }))
    .filter(g => g.cards.length > 0);
});

const getTypeLabel = (type: string) => TYPE_LABELS[type] || type;

// ===== 封面解析 =====
const getCoverImage = (coverImage: string | null): string => {
  if (!coverImage) return '';
  try {
    const arr = JSON.parse(coverImage);
    return Array.isArray(arr) && arr.length > 0 ? arr[0] : '';
  } catch {
    return coverImage;
  }
};

// ===== 时间格式化 =====
const formatTime = (dateStr: string) => {
  if (!dateStr) return '';
  const d = new Date(dateStr);
  const now = new Date();
  const diff = Math.floor((now.getTime() - d.getTime()) / 1000);
  if (diff < 60) return '刚刚';
  if (diff < 3600) return Math.floor(diff / 60) + ' 分钟前';
  if (diff < 86400) return Math.floor(diff / 3600) + ' 小时前';
  if (diff < 604800) return Math.floor(diff / 86400) + ' 天前';
  return d.toLocaleDateString('zh-CN');
};

const formatDate = (dateStr: string) => {
  if (!dateStr) return '';
  return new Date(dateStr).toLocaleDateString('zh-CN');
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

// ===== 关联卡片列表 =====
const embeddedCards = computed<CardDetail[]>(() => {
  if (!selectedCardDetail.value) return [];
  const ids = extractEmbeddedCardIds(selectedCardDetail.value);
  const result: CardDetail[] = [];

  for (const id of ids) {
    const detail = store.getCardDetailById(id);
    if (detail) {
      result.push(detail);
      continue;
    }
    const summary = store.getCardById(id);
    if (summary) {
      result.push({
        id: summary.id,
        projectId: summary.projectId,
        title: summary.title,
        type: summary.type,
        coverImage: summary.coverImage,
        updatedAt: summary.updatedAt,
        outRelationCount: summary.outRelationCount,
        inRelationCount: summary.inRelationCount,
        description: '',
        content: '{}',
        contentBlocks: [],
        timelineEvents: [],
        aliases: [],
        attributes: [],
        tags: [],
        embeddedCards: [],
        galleryImages: [],
        outRelations: [],
        inRelations: [],
      });
    }
  }
  return result;
});

// ===== 描述渲染 =====
const renderedDescription = computed(() => {
  const desc = selectedCardDetail.value?.description || '';
  if (!desc) return '';

  return desc.replace(/\{CARD:([^}]+)\}/g, (_match: string, id: string) => {
    const target = store.getCardById(id);
    if (target) {
      return `<a class="inline-ref" data-card-id="${id}">@${getTypeLabel(target.type)} ${target.title}</a>`;
    }
    return `<span class="inline-ref broken">⚠️ 已删除的卡片</span>`;
  });
});

// ===== 标签 =====
const cardTags = computed(() => {
  const tags = selectedCardDetail.value?.tags;
  if (!tags) return [];
  if (Array.isArray(tags)) return tags;
  try {
    const parsed = JSON.parse(tags as any);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
});

// ===== 图片预览 =====
const previewCover = () => {
  if (!selectedCardDetail.value?.coverImage) return;
  previewList.value = [getCoverImage(selectedCardDetail.value.coverImage)];
  previewIndex.value = 0;
  previewVisible.value = true;
};

const previewGallery = (idx: number) => {
  const imgs = selectedCardDetail.value?.galleryImages || [];
  if (!imgs.length) return;
  previewList.value = imgs;
  previewIndex.value = idx;
  previewVisible.value = true;
};

// ===== 选中卡片 =====
const selectCard = async (id: string) => {
  view.value = 'card';

  // 同一张：强制刷新
  if (selectedCardId.value === id) {
    detailLoading.value = true;
    try {
      selectedCardDetail.value = await store.fetchCardDetail(projectId, id, true);
    } catch (error) {
      console.error('刷新卡片失败:', error);
    } finally {
      detailLoading.value = false;
    }
    return;
  }

  // 不同：切换 + 强制刷新
  selectedCardId.value = id;
  selectedCardDetail.value = null;
  detailLoading.value = true;
  try {
    selectedCardDetail.value = await store.fetchCardDetail(projectId, id, true);
  } catch (error) {
    console.error('加载卡片详情失败:', error);
    ElMessage.error('加载卡片失败');
  } finally {
    detailLoading.value = false;
  }
};

const closePanel = () => {
  selectedCardId.value = null;
  selectedCardDetail.value = null;
};

// ===== 图谱切换 =====
const switchToGraph = () => {
  view.value = 'graph';
  closePanel();
  nextTick(() => {
    if (graphRef.value) graphRef.value.refresh?.();
  });
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

// ===== 删除 =====
const handleDeleteCard = async (cardId: string) => {
  try {
    await ElMessageBox.confirm('确定删除吗？', '', {
      type: 'warning',
      confirmButtonText: '删除',
      cancelButtonText: '取消',
    });
    await store.deleteCard(cardId);
    ElMessage.success('已删除');
    if (selectedCardId.value === cardId) closePanel();
    loadData();
    if (graphRef.value) graphRef.value.refresh();
  } catch (e) {
    if (e !== 'cancel') console.error(e);
  }
};

// ===== 新建 =====
const openCreateDialog = () => {
  selectedCardId.value = 'new';
  selectedCardDetail.value = null;
  view.value = 'card';
};

// ===== 保存回调 =====
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

const onCardInserted = () => {
  loadData();
};

// ===== 跳转 =====
const goBack = () => router.push('/world');
const goToCard = (targetId: string) => {
  if (targetId === selectedCardId.value) return;
  selectCard(targetId);
};

// ===== 生命周期 =====
onMounted(loadData);
</script>

<style scoped>
* { box-sizing: border-box; }

/* ============================================================
   页面骨架
   ============================================================ */
.project-detail {
  height: 100vh;
  display: flex;
  flex-direction: column;
  background: #fafafa;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  color: #1a1a1a;
  overflow: hidden;
}

/* ============================================================
   顶部
   ============================================================ */
.page-header {
  flex-shrink: 0;
  padding: 20px 40px 16px;
  background: #fff;
  border-bottom: 1px solid #f0f0f0;
  display: flex;
  align-items: flex-start;
  gap: 24px;
  position: relative;
}

.back-btn {
  background: none;
  border: none;
  color: #999;
  font-size: 13px;
  cursor: pointer;
  padding: 4px 0;
  flex-shrink: 0;
  margin-top: 4px;
}
.back-btn:hover { color: #1a1a1a; }

.header-main {
  flex: 1;
  min-width: 0;
}

.header-main h1 {
  font-size: 22px;
  font-weight: 600;
  color: #111;
  margin: 0 0 6px;
  letter-spacing: -0.3px;
}

.header-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: #999;
  margin-bottom: 8px;
}
.header-meta .dot { color: #ddd; }

.header-desc {
  margin: 0;
  font-size: 14px;
  line-height: 1.6;
  color: #666;
  max-width: 720px;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.header-actions {
  flex-shrink: 0;
  margin-top: 4px;
}

.btn-ghost {
  background: none;
  border: none;
  color: #666;
  font-size: 14px;
  cursor: pointer;
  padding: 6px 12px;
  border-radius: 6px;
  transition: background 0.1s;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}
.btn-ghost:hover {
  background: #f5f5f5;
  color: #1a1a1a;
}
.btn-icon {
  font-size: 16px;
  line-height: 1;
}

/* ============================================================
   主体布局
   ============================================================ */
.layout {
  flex: 1;
  display: flex;
  min-height: 0;
  overflow: hidden;
}

/* ============================================================
   左侧栏
   ============================================================ */
.sidebar {
  width: 260px;
  flex-shrink: 0;
  background: #fff;
  border-right: 1px solid #f0f0f0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.search {
  padding: 16px 16px 8px;
  flex-shrink: 0;
}
.search input {
  width: 100%;
  border: none;
  background: #f5f5f5;
  border-radius: 6px;
  padding: 7px 12px;
  font-size: 13px;
  color: #1a1a1a;
  transition: background 0.15s;
}
.search input::placeholder { color: #aaa; }
.search input:focus {
  outline: none;
  background: #ececec;
}

.tree {
  flex: 1;
  overflow-y: auto;
  padding: 8px 8px 16px;
}

.tree-group {
  margin-bottom: 20px;
}

.tree-group-label {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 4px 8px 6px;
  font-size: 11px;
  font-weight: 600;
  color: #999;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  user-select: none;
}
.group-count {
  font-weight: 400;
  color: #ccc;
}

.tree-items {
  display: flex;
  flex-direction: column;
}

.tree-item {
  display: block;
  width: 100%;
  text-align: left;
  padding: 5px 8px;
  font-size: 13px;
  color: #555;
  background: none;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: background 0.1s, color 0.1s;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  line-height: 1.5;
}
.tree-item:hover {
  background: #f5f5f5;
  color: #1a1a1a;
}
.tree-item.active {
  background: #ececec;
  color: #1a1a1a;
  font-weight: 500;
}

.tree-empty {
  padding: 32px 16px;
  text-align: center;
  font-size: 13px;
  color: #ccc;
}

.sidebar-footer {
  flex-shrink: 0;
  padding: 8px;
  border-top: 1px solid #f0f0f0;
}

.graph-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  width: 100%;
  padding: 8px 10px;
  font-size: 13px;
  color: #666;
  background: none;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: background 0.1s, color 0.1s;
  text-align: left;
}
.graph-btn:hover {
  background: #f5f5f5;
  color: #1a1a1a;
}
.graph-btn.active {
  background: #ececec;
  color: #1a1a1a;
  font-weight: 500;
}

/* ============================================================
   主区域
   ============================================================ */
.main {
  flex: 1;
  min-width: 0;
  overflow-y: auto;
  background: #fafafa;
}

/* -------- 图谱视图 -------- */
.graph-view {
  height: 100%;
  padding: 20px;
  background: #fff;
}

/* -------- 编辑器视图 -------- */
.editor-view {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: #fff;
}
.editor-topbar {
  flex-shrink: 0;
  padding: 12px 32px;
  border-bottom: 1px solid #f0f0f0;
  display: flex;
  align-items: center;
  gap: 16px;
}
.editor-back {
  background: none;
  border: none;
  color: #999;
  font-size: 13px;
  cursor: pointer;
  padding: 4px 0;
}
.editor-back:hover { color: #1a1a1a; }
.editor-title {
  font-size: 13px;
  color: #666;
}
.editor-body {
  flex: 1;
  overflow-y: auto;
  padding: 32px 40px 80px;
}
.editor-body :deep(.card-editor-inline) {
  max-width: 720px;
  margin: 0 auto;
}

/* -------- 只读文档视图 -------- */
.doc-view {
  padding: 48px 40px 120px;
}

.doc {
  max-width: 720px;
  margin: 0 auto;
}

.doc-header {
  margin-bottom: 32px;
}

.doc-title {
  font-size: 32px;
  font-weight: 600;
  color: #111;
  margin: 0 0 10px;
  letter-spacing: -0.5px;
  line-height: 1.25;
}

.doc-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: #999;
}
.doc-meta .dot { color: #ddd; }

.doc-type {
  color: #4f46e5;
  font-weight: 500;
}

.doc-cover {
  margin-bottom: 32px;
  border-radius: 8px;
  overflow: hidden;
  cursor: zoom-in;
  background: #f5f5f5;
}
.doc-cover img {
  width: 100%;
  height: auto;
  display: block;
  transition: opacity 0.15s;
}
.doc-cover:hover img { opacity: 0.95; }

/* -------- Section -------- */
.doc-section {
  margin-bottom: 40px;
}

.section-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  font-weight: 600;
  color: #999;
  text-transform: uppercase;
  letter-spacing: 1px;
  margin: 0 0 14px;
  padding-bottom: 8px;
  border-bottom: 1px solid #f0f0f0;
}

.section-count {
  font-size: 11px;
  color: #bbb;
  font-weight: 400;
  letter-spacing: 0;
  text-transform: none;
}

/* -------- 描述（prose） -------- */
.prose {
  font-size: 15px;
  line-height: 1.85;
  color: #1a1a1a;
  white-space: pre-wrap;
  word-break: break-word;
}

.prose :deep(.inline-ref) {
  display: inline-block;
  color: #4f46e5;
  background: #eef2ff;
  padding: 1px 8px;
  border-radius: 4px;
  font-size: 13px;
  cursor: pointer;
  margin: 0 2px;
  font-weight: 500;
}
.prose :deep(.inline-ref:hover) {
  background: #e0e7ff;
}
.prose :deep(.inline-ref.broken) {
  background: #fef2f2;
  color: #ef4444;
}

/* -------- 空状态提示 -------- */
.empty-note {
  font-size: 13px;
  color: #c0c0c0;
  font-style: italic;
  margin: 0;
  padding: 4px 0;
}

/* -------- 图库 -------- */
.gallery {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
  gap: 10px;
}
.gallery img {
  width: 100%;
  aspect-ratio: 1;
  object-fit: cover;
  border-radius: 6px;
  cursor: zoom-in;
  background: #f5f5f5;
  transition: opacity 0.15s;
}
.gallery img:hover { opacity: 0.9; }

/* -------- 属性 -------- */
.attr-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.attr-item {
  display: grid;
  grid-template-columns: 120px 1fr;
  gap: 24px;
  font-size: 14px;
  line-height: 1.6;
}
.attr-key {
  color: #999;
}
.attr-value {
  color: #1a1a1a;
  word-break: break-word;
  white-space: pre-wrap;
}

/* -------- 关联内容（内联展开） -------- */
.embedded-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

/* -------- 标签 -------- */
.tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.tag {
  font-size: 13px;
  color: #4f46e5;
  cursor: default;
  transition: opacity 0.15s;
}
.tag:hover { opacity: 0.7; }

/* ============================================================
   空状态
   ============================================================ */
.empty-state {
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 16px;
  color: #bbb;
  font-size: 14px;
}
.empty-state p {
  margin: 0;
}

.btn-primary {
  padding: 8px 20px;
  background: #111;
  color: #fff;
  border: none;
  border-radius: 6px;
  font-size: 13px;
  cursor: pointer;
  transition: background 0.15s;
}
.btn-primary:hover {
  background: #333;
}

/* ============================================================
   滚动条
   ============================================================ */
.tree::-webkit-scrollbar,
.main::-webkit-scrollbar,
.editor-body::-webkit-scrollbar {
  width: 6px;
}
.tree::-webkit-scrollbar-track,
.main::-webkit-scrollbar-track,
.editor-body::-webkit-scrollbar-track {
  background: transparent;
}
.tree::-webkit-scrollbar-thumb,
.main::-webkit-scrollbar-thumb,
.editor-body::-webkit-scrollbar-thumb {
  background: #e0e0e0;
  border-radius: 3px;
}
.tree::-webkit-scrollbar-thumb:hover,
.main::-webkit-scrollbar-thumb:hover,
.editor-body::-webkit-scrollbar-thumb:hover {
  background: #d0d0d0;
}

/* ============================================================
   响应式
   ============================================================ */
@media (max-width: 820px) {
  .page-header {
    padding: 16px 20px;
    flex-wrap: wrap;
  }
  .header-main h1 { font-size: 18px; }
  .layout { flex-direction: column; }
  .sidebar {
    width: 100%;
    height: 200px;
    border-right: none;
    border-bottom: 1px solid #f0f0f0;
  }
  .doc-view { padding: 24px 20px 80px; }
  .doc-title { font-size: 24px; }
  .attr-item {
    grid-template-columns: 1fr;
    gap: 2px;
  }
  .attr-key {
    font-size: 12px;
    color: #bbb;
  }
  .editor-body { padding: 20px 20px 60px; }
}
</style>