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
        <!-- ✅ 只有项目所有者才能看到编辑/删除按钮 -->
        <div v-if="isOwner" class="header-right">
          <button class="action-btn" @click="editCard">编辑</button>
          <button class="action-btn danger" @click="handleDelete">删除</button>
        </div>
      </div>

      <!-- 封面图 -->
      <div v-if="card.coverImage" class="cover-banner">
        <img :src="coverUrl" :alt="card.title" />
      </div>

      <!-- 图库 -->
      <div v-if="card.galleryImages && card.galleryImages.length" class="section gallery-section">
        <div class="gallery-grid">
          <div
            v-for="(img, idx) in card.galleryImages"
            :key="idx"
            class="gallery-thumb"
            @click="openPreview(galleryImages, idx)"
          >
            <img :src="img" :alt="`${card.title} 图 ${idx + 1}`" loading="lazy" />
          </div>
        </div>
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

      <!-- 描述 -->
      <div class="section">
        <div class="description-body" v-html="renderedDescription"></div>
      </div>

      <!-- ✅ 关联内容：使用 EmbeddedCard 组件渲染 -->
      <div v-if="embeddedCards.length" class="section embedded-section">
        <div class="embedded-header">
          <span class="embedded-label">关联内容</span>
          <span class="embedded-count">{{ embeddedCards.length }}</span>
        </div>
        <div class="embedded-list">
          <EmbeddedCard
            v-for="item in embeddedCards"
            :key="item.id"
            :card="item"
            :project-id="projectId"
            clickable
            @click="goToCard(item.id)"
          />
        </div>
      </div>

      <!-- 标签 -->
      <div v-if="cardTags.length" class="section">
        <div class="tag-list">
          <span v-for="tag in cardTags" :key="tag" class="tag-item">#{{ tag }}</span>
        </div>
      </div>
    </div>

    <!-- 图片预览弹窗 -->
    <el-image-viewer
      v-if="previewVisible"
      :url-list="previewImages"
      :initial-index="previewIndex"
      @close="closePreview"
    />

    <div v-if="!card && loading" class="loading-state">加载中...</div>
    <div v-else-if="!card" class="not-found">卡片不存在</div>
  </div>
</template>

<script setup lang="ts">
import { useCardPresentation } from '@/composables/世界观管理/useCardPresentation';
import { useCardPreload } from '@/composables/世界观管理/useCardPreload';
import { useImagePreview } from '@/composables/世界观管理/useImagePreview';
import { getCardTypeLabel as getTypeLabel } from '@/utils/worldPresentation';
import { ref, computed, watch, onScopeDispose } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { ElMessage, ElMessageBox, ElImageViewer } from 'element-plus';
import { useWorldStore } from '@/stores/world';
import { useUserStore } from '@/stores/user';
import type { CardDetail } from '@/api/worldApi';
import EmbeddedCard from '@/views/世界观管理/世界观组件/EmbeddedCard.vue';

// ===== 路由 =====
const router = useRouter();
const route = useRoute();
const store = useWorldStore();
const userStore = useUserStore();

// ===== 状态 =====
const cardId = computed(() => route.params.cardId as string);
const projectId = computed(() => route.params.projectId as string);
const loading = ref(false);

const card = ref<CardDetail | null>(null);

const { visible: previewVisible, images: previewImages, index: previewIndex, open: openPreview, close: closePreview } = useImagePreview();
const { embeddedIds, embeddedCards, cardTags, galleryImages, coverUrl, renderedDescription } = useCardPresentation(card, projectId, 'detail');
const { load: preload } = useCardPreload(projectId);

// ===== 判断当前用户是否是项目所有者 =====
const isOwner = computed(() => {
  const userId = userStore.userInfo?.id;
  const ownerId = store.currentProject?.ownerId;
  if (!userId || !ownerId) return false;
  return userId === ownerId;
});

const formatDate = (dateStr: string) => {
  if (!dateStr) return '';
  const d = new Date(dateStr);
  return d.toLocaleString('zh-CN');
};

// ===== 数据加载 =====
let loadVersion = 0;
const loadData = async () => {
  const version = ++loadVersion;
  const pid = projectId.value;
  const id = cardId.value;
  card.value = null;
  closePreview();
  loading.value = true;
  try {
    await store.fetchCards(pid);
    if (version !== loadVersion) return;
    const detail = await store.fetchCardDetail(pid, id);
    if (version !== loadVersion) return;
    card.value = detail;
    await preload(embeddedIds.value);
  } catch (error) {
    if (version !== loadVersion) return;
    console.error('加载卡片失败:', error);
    ElMessage.error('加载失败');
  } finally {
    if (version === loadVersion) loading.value = false;
  }
};

// ===== 操作 =====
const goBack = () => router.push(`/world/project/${projectId.value}`);

const goToCard = (targetId: string) => {
  if (targetId === cardId.value) return;
  router.push(`/world/project/${projectId.value}/card/${targetId}`);
};

const editCard = () => router.push(`/world/project/${projectId.value}?edit=${cardId.value}`);

const handleDelete = async () => {
  try {
    await ElMessageBox.confirm('确定删除这张卡片吗？', '确认删除', {
      type: 'warning',
      confirmButtonText: '删除',
      cancelButtonText: '取消',
    });
    await store.deleteCard(cardId.value);
    ElMessage.success('已删除');
    router.push(`/world/project/${projectId.value}`);
  } catch (error) {
    if (error !== 'cancel') {
      console.error('删除失败:', error);
      ElMessage.error('删除失败');
    }
  }
};

// ===== 生命周期 =====
watch([projectId, cardId], loadData, { immediate: true });
onScopeDispose(() => { loadVersion++; });
</script>

<style scoped>
.card-detail {
  min-height: 100vh;
  background: var(--paper-card);
  padding: 32px 40px;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}

.detail-container {
  max-width: 820px;
  margin: 0 auto;
}

.back-btn {
  background: none;
  border: none;
  color: var(--text-secondary);
  font-size: 14px;
  cursor: pointer;
  padding: 4px 0;
  margin-bottom: 16px;
  transition: color 0.15s;
}
.back-btn:hover {
  color: var(--text-primary);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding-bottom: 16px;
  border-bottom: 1px solid var(--border-line);
  margin-bottom: 20px;
}

.header-left h1 {
  font-size: 26px;
  font-weight: 600;
  margin: 0 0 6px;
  color: var(--text-primary);
  letter-spacing: -0.3px;
}

.meta-row {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: var(--text-secondary);
}

.type-badge {
  color: var(--accent-color);
  background: var(--accent-soft);
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
  border: 1px solid var(--border-line);
  padding: 4px 14px;
  border-radius: 6px;
  font-size: 13px;
  cursor: pointer;
  transition: 0.15s;
}
.action-btn:hover {
  background: var(--bg-sub);
}
.action-btn.danger:hover {
  background: var(--danger-soft);
  border-color: var(--danger-soft);
  color: var(--danger);
}

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

.gallery-section {
  padding: 16px 20px;
}

.gallery-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
  gap: 12px;
}

.gallery-thumb {
  aspect-ratio: 1;
  border-radius: 8px;
  overflow: hidden;
  cursor: pointer;
  border: 1px solid var(--border-line);
  transition: transform 0.2s, box-shadow 0.2s;
  background: var(--bg-sub);
}

.gallery-thumb:hover {
  transform: scale(1.02);
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.1);
}

.gallery-thumb img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

.section {
  background: var(--paper-card);
  border-radius: 8px;
  padding: 16px 20px;
  margin-bottom: 12px;
  border: 1px solid var(--border-line);
}

.attr-grid {
  display: grid;
  grid-template-columns: 120px 1fr;
  gap: 4px 16px;
}
.attr-key {
  font-weight: 500;
  color: var(--text-secondary);
  font-size: 13px;
  padding: 4px 0;
  border-bottom: 1px solid var(--border-line);
}
.attr-value {
  color: var(--text-primary);
  font-size: 13px;
  padding: 4px 0;
  border-bottom: 1px solid var(--border-line);
}
.attr-row:last-child .attr-key,
.attr-row:last-child .attr-value {
  border-bottom: none;
}

.description-body {
  font-size: 15px;
  line-height: 1.8;
  color: var(--text-primary);
  white-space: pre-wrap;
}

.inline-ref {
  display: inline-block;
  padding: 0 8px;
  background: var(--accent-soft);
  color: var(--accent-color);
  border-radius: 4px;
  font-size: 13px;
}
.inline-ref.broken {
  background: var(--danger-soft);
  color: var(--danger);
}

/* ===== 关联内容：外层容器（内层由 EmbeddedCard 组件渲染） ===== */
.embedded-section {
  padding: 0;
  border: 1px solid var(--border-line);
  overflow: hidden;
}

.embedded-header {
  display: flex;
  justify-content: space-between;
  padding: 12px 20px;
  background: var(--bg-main);
  border-bottom: 1px solid var(--border-line);
}
.embedded-label {
  font-size: 13px;
  font-weight: 500;
  color: var(--text-primary);
}
.embedded-count {
  font-size: 12px;
  color: var(--text-secondary);
}

.embedded-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 14px 16px;
  background: var(--paper-card);
}

.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}
.tag-item {
  font-size: 13px;
  color: var(--accent-color);
  background: var(--accent-soft);
  padding: 2px 12px;
  border-radius: 4px;
}

.loading-state,
.not-found {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 200px;
  color: var(--text-secondary);
  font-size: 16px;
}

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
    border-bottom: 1px solid var(--border-line);
  }
  .attr-key {
    border-bottom: none;
    font-weight: 500;
    min-width: 70px;
  }
  .attr-value {
    border-bottom: none;
  }
  .gallery-grid {
    grid-template-columns: repeat(auto-fill, minmax(100px, 1fr));
    gap: 8px;
  }
  .embedded-list {
    padding: 10px 12px;
  }
}
</style>
