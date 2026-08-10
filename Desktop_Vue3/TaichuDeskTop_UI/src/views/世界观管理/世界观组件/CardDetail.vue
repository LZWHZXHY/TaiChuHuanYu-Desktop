<!-- src/views/柴圈板块/World/pages/CardDetail.vue -->
<template>
  <div class="card-detail" v-loading="loading">
    <div v-if="card" class="detail-container">
      <!-- 返回 -->
      <button class="back-btn" @click="goBack">← 返回项目</button>

      <!-- 卡片头部 -->
      <div class="card-header">
        <div class="header-left">
          <h1>{{ card.title }}</h1>
          <div class="meta-row">
            <span class="type-badge">{{ getTypeLabel(card.type) }}</span>
            <span>· 更新于 {{ formatDate(card.updatedAt) }}</span>
          </div>
        </div>
        <div class="header-right">
          <button class="action-btn" @click="editCard">编辑</button>
          <button class="action-btn danger" @click="handleDelete">删除</button>
        </div>
      </div>

      <!-- 封面图 -->
      <div v-if="card.coverImage" class="cover-banner">
        <img :src="card.coverImage" :alt="card.title" />
      </div>

      <!-- 属性 -->
      <div v-if="card.attributes?.length" class="section">
        <div class="attr-grid">
          <div v-for="attr in card.attributes" :key="attr.key" class="attr-row">
            <span class="attr-key">{{ attr.key }}</span>
            <span class="attr-value">{{ attr.value }}</span>
          </div>
        </div>
      </div>

      <!-- ===== 描述（渲染嵌入式卡片） ===== -->
      <div class="section">
        <div class="description-body" v-html="renderedDescription"></div>
      </div>

      <!-- ===== 关联卡片展开显示 ===== -->
      <div v-if="embeddedCards.length" class="section embedded-section">
        <div class="embedded-header">
          <span class="embedded-label">关联内容</span>
          <span class="embedded-count">{{ embeddedCards.length }}</span>
        </div>
        <div class="embedded-list">
          <div
            v-for="item in embeddedCards"
            :key="item.id"
            class="embedded-card-block"
            @click="goToCard(item.id)"
          >
            <div class="block-left">
              <span class="block-type">{{ getTypeLabel(item.type) }}</span>
              <span class="block-title">{{ item.title }}</span>
            </div>
            <div class="block-right">
              <span class="block-updated">{{ formatDate(item.updatedAt) }}</span>
              <span class="block-arrow">→</span>
            </div>
            <!-- 展开的内容预览 -->
            <div class="block-preview">
              <div v-if="item.coverImage" class="block-cover">
                <img :src="item.coverImage" />
              </div>
              <div class="block-detail">
                <p class="block-desc">{{ getCardSummary(item) }}</p>
                <div v-if="item.attributes?.length" class="block-attrs">
                  <span v-for="attr in item.attributes.slice(0, 3)" :key="attr.key" class="block-attr">
                    {{ attr.key }}: {{ attr.value }}
                  </span>
                  <span v-if="item.attributes.length > 3" class="block-attr-more">+{{ item.attributes.length - 3 }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- 标签 -->
      <div v-if="cardTags.length" class="section">
        <div class="tag-list">
          <span v-for="tag in cardTags" :key="tag" class="tag-item">#{{ tag }}</span>
        </div>
      </div>
    </div>

    <div v-else-if="loading" class="loading-state">加载中...</div>
    <div v-else class="not-found">卡片不存在</div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { ElMessage, ElMessageBox } from 'element-plus';
import { useWorldStore } from '@/stores/world';
import type { WorldCard } from '@/stores/world';

// ===== 类型标签映射 =====
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

// ===== 路由 =====
const router = useRouter();
const route = useRoute();
const store = useWorldStore();

// ===== 状态 =====
const cardId = route.params.cardId as string;
const projectId = route.params.projectId as string;
const loading = ref(false);
const card = ref<WorldCard | null>(null);

// ===== 工具函数：提取关联卡片 ID =====
const extractEmbeddedCardIds = (c: WorldCard): string[] => {
  const ids: string[] = [];

  // 1. 从 contentBlocks 提取
  if (c.contentBlocks) {
    c.contentBlocks.forEach((block: any) => {
      if (block.cardId && !ids.includes(block.cardId)) {
        ids.push(block.cardId);
      }
    });
  }

  // 2. 从描述中的 {CARD:xxx} 提取
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

// ===== 方法 =====
const getTypeLabel = (type: string) => TYPE_LABELS[type] || type;

// ===== 计算属性 =====
const cardTags = computed(() => {
  const tags = card.value?.tags;
  if (!tags) return [];
  if (Array.isArray(tags)) return tags;
  try {
    const parsed = JSON.parse(tags);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
});

// 关联卡片列表（从 store.cards 中获取完整数据）
const embeddedCards = computed<WorldCard[]>(() => {
  if (!card.value) return [];
  const ids = extractEmbeddedCardIds(card.value);
  return ids
    .map(id => store.cards.find((c: WorldCard) => c.id === id))
    .filter((c): c is WorldCard => c !== undefined);
});

const getCardSummary = (item: WorldCard) => {
  if (item.description) return item.description;
  try {
    const data = JSON.parse(item.content || '{}');
    return data.description || data.summary || '';
  } catch {
    return '';
  }
};

// 渲染描述（将 {CARD:xxx} 替换为内联标签）
const renderedDescription = computed(() => {
  const desc = card.value?.description || '';
  if (!desc) return '';

  return desc.replace(/\{CARD:([^}]+)\}/g, (_match: string, id: string) => {
    const target = store.cards.find((c: WorldCard) => c.id === id);
    if (target) {
      return `<span class="inline-ref">@${getTypeLabel(target.type)}: ${target.title}</span>`;
    }
    return `<span class="inline-ref broken">⚠️ 已删除的卡片</span>`;
  });
});

const formatDate = (dateStr: string) => {
  if (!dateStr) return '';
  const d = new Date(dateStr);
  return d.toLocaleString('zh-CN');
};

// ===== 数据加载 =====
const loadData = async () => {
  loading.value = true;
  try {
    // 1. 加载当前卡片
    await store.fetchCards(projectId);
    const found = store.cards.find((c: WorldCard) => c.id === cardId);
    card.value = found || null;

    // 2. 🔧 加载所有关联卡片的详情（如果尚未加载）
    if (card.value) {
      const ids = extractEmbeddedCardIds(card.value);
      if (ids.length > 0) {
        const existingIds = store.cards.map(c => c.id);
        const missingIds = ids.filter(id => !existingIds.includes(id));
        if (missingIds.length > 0) {
          await store.fetchCardsByIds(projectId, missingIds);
          // 更新当前卡片（因为 store 中可能新增了卡片，但 card.value 仍是原对象，不影响）
          // 但 embeddedCards 会重新计算，无需额外操作
        }
      }
    }
  } catch (error) {
    console.error('加载卡片失败:', error);
    ElMessage.error('加载失败');
  } finally {
    loading.value = false;
  }
};

// ===== 操作 =====
const goBack = () => router.push(`/world/project/${projectId}`);

const goToCard = (targetId: string) => {
  if (targetId === cardId) return;
  router.push(`/world/project/${projectId}/card/${targetId}`);
};

const editCard = () => router.push(`/world/project/${projectId}?edit=${cardId}`);

const handleDelete = async () => {
  try {
    await ElMessageBox.confirm('确定删除这张卡片吗？', '确认删除', {
      type: 'warning',
      confirmButtonText: '删除',
      cancelButtonText: '取消',
    });
    await store.deleteCard(cardId);
    ElMessage.success('已删除');
    router.push(`/world/project/${projectId}`);
  } catch (error) {
    if (error !== 'cancel') {
      console.error('删除失败:', error);
      ElMessage.error('删除失败');
    }
  }
};

// ===== 生命周期 =====
onMounted(loadData);
</script>

<style scoped>
/* ===== 页面布局 ===== */
.card-detail {
  min-height: 100vh;
  background: #fafbfc;
  padding: 32px 40px;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}

.detail-container {
  max-width: 820px;
  margin: 0 auto;
}

/* ===== 返回按钮 ===== */
.back-btn {
  background: none;
  border: none;
  color: #94a3b8;
  font-size: 14px;
  cursor: pointer;
  padding: 4px 0;
  margin-bottom: 16px;
  transition: color 0.15s;
}
.back-btn:hover {
  color: #1e293b;
}

/* ===== 卡片头部 ===== */
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding-bottom: 16px;
  border-bottom: 1px solid #eef2f6;
  margin-bottom: 20px;
}

.header-left h1 {
  font-size: 26px;
  font-weight: 600;
  margin: 0 0 6px;
  color: #0f172a;
  letter-spacing: -0.3px;
}

.meta-row {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: #94a3b8;
}

.type-badge {
  color: #4f46e5;
  background: #eef2ff;
  padding: 1px 12px;
  border-radius: 4px;
  font-size: 12px;
}

.header-right {
  display: flex;
  gap: 8px;
}

.action-btn {
  background: none;
  border: 1px solid #e2e8f0;
  padding: 4px 14px;
  border-radius: 6px;
  font-size: 13px;
  cursor: pointer;
  transition: 0.15s;
}
.action-btn:hover {
  background: #f1f5f9;
}
.action-btn.danger:hover {
  background: #fef2f2;
  border-color: #fecaca;
  color: #ef4444;
}

/* ===== 封面图 ===== */
.cover-banner {
  border-radius: 8px;
  overflow: hidden;
  margin-bottom: 20px;
}
.cover-banner img {
  width: 100%;
  max-height: 320px;
  object-fit: cover;
  display: block;
}

/* ===== 区块 ===== */
.section {
  background: #fff;
  border-radius: 8px;
  padding: 16px 20px;
  margin-bottom: 12px;
  border: 1px solid #eef2f6;
}

/* ===== 属性 ===== */
.attr-grid {
  display: grid;
  grid-template-columns: 120px 1fr;
  gap: 4px 16px;
}
.attr-key {
  font-weight: 500;
  color: #64748b;
  font-size: 13px;
  padding: 4px 0;
  border-bottom: 1px solid #f4f6f8;
}
.attr-value {
  color: #1e293b;
  font-size: 13px;
  padding: 4px 0;
  border-bottom: 1px solid #f4f6f8;
}
.attr-row:last-child .attr-key,
.attr-row:last-child .attr-value {
  border-bottom: none;
}

/* ===== 描述 ===== */
.description-body {
  font-size: 15px;
  line-height: 1.8;
  color: #1e293b;
  white-space: pre-wrap;
}

.inline-ref {
  display: inline-block;
  padding: 0 8px;
  background: #eef2ff;
  color: #4f46e5;
  border-radius: 4px;
  font-size: 13px;
}
.inline-ref.broken {
  background: #fef2f2;
  color: #ef4444;
}

/* ===== 嵌入卡片展开 ===== */
.embedded-section {
  padding: 0;
  border: 1px solid #eef2f6;
  overflow: hidden;
}

.embedded-header {
  display: flex;
  justify-content: space-between;
  padding: 12px 20px;
  background: #f8fafc;
  border-bottom: 1px solid #eef2f6;
}
.embedded-label {
  font-size: 13px;
  font-weight: 500;
  color: #1e293b;
}
.embedded-count {
  font-size: 12px;
  color: #94a3b8;
}

.embedded-list {
  display: flex;
  flex-direction: column;
  gap: 0;
}

.embedded-card-block {
  padding: 14px 20px;
  cursor: pointer;
  border-bottom: 1px solid #f4f6f8;
  transition: background 0.12s;
}
.embedded-card-block:last-child {
  border-bottom: none;
}
.embedded-card-block:hover {
  background: #fafbfc;
}

.block-left {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 6px;
}
.block-type {
  font-size: 11px;
  color: #4f46e5;
  background: #eef2ff;
  padding: 1px 10px;
  border-radius: 4px;
}
.block-title {
  font-weight: 500;
  font-size: 15px;
  color: #0f172a;
}

.block-right {
  display: flex;
  align-items: center;
  gap: 8px;
  float: right;
}
.block-updated {
  font-size: 12px;
  color: #94a3b8;
}
.block-arrow {
  color: #c0c4cc;
}

.block-preview {
  display: flex;
  gap: 14px;
  margin-top: 6px;
  padding-top: 6px;
  border-top: 1px solid #f4f6f8;
}

.block-cover {
  flex-shrink: 0;
  width: 80px;
  height: 60px;
  border-radius: 4px;
  overflow: hidden;
  background: #f1f5f9;
}
.block-cover img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.block-detail {
  flex: 1;
  min-width: 0;
}
.block-desc {
  font-size: 13px;
  color: #64748b;
  margin: 0 0 4px;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  line-height: 1.5;
}

.block-attrs {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}
.block-attr {
  font-size: 12px;
  color: #94a3b8;
  background: #f1f5f9;
  padding: 0 8px;
  border-radius: 4px;
}
.block-attr-more {
  font-size: 12px;
  color: #c0c4cc;
}

/* ===== 标签 ===== */
.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}
.tag-item {
  font-size: 13px;
  color: #4f46e5;
  background: #eef2ff;
  padding: 2px 12px;
  border-radius: 4px;
}

/* ===== 状态 ===== */
.loading-state,
.not-found {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 200px;
  color: #94a3b8;
  font-size: 16px;
}

/* ===== 响应式 ===== */
@media (max-width: 640px) {
  .card-detail {
    padding: 16px;
  }
  .card-header {
    flex-direction: column;
    gap: 12px;
  }
  .header-right {
    width: 100%;
  }
  .header-right .action-btn {
    flex: 1;
    text-align: center;
  }
  .attr-grid {
    grid-template-columns: 1fr;
    gap: 2px;
  }
  .attr-row {
    display: flex;
    gap: 8px;
    padding: 2px 0;
    border-bottom: 1px solid #f4f6f8;
  }
  .attr-key {
    border-bottom: none;
    font-weight: 500;
    min-width: 70px;
  }
  .attr-value {
    border-bottom: none;
  }
  .block-preview {
    flex-direction: column;
  }
  .block-cover {
    width: 100%;
    height: 100px;
  }
  .block-right {
    display: none;
  }
}
</style>