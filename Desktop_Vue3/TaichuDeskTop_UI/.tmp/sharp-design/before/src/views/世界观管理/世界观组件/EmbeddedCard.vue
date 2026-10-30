<template>
  <div
    class="embedded-card"
    :class="{
      'is-collapsible': collapsible,
      'is-expanded': expanded,
      'is-nested': nested,
    }"
  >
    <!-- ===== 头部 ===== -->
    <div
      class="ec-header"
      :class="{ 'is-clickable': collapsible || clickable }"
      :role="collapsible || clickable ? 'button' : undefined"
      :tabindex="collapsible || clickable ? 0 : undefined"
      :aria-expanded="collapsible ? expanded : undefined"
      @keydown.enter.self.prevent="onHeaderClick"
      @keydown.space.self.prevent="onHeaderClick"
      @click="onHeaderClick"
    >
      <span class="ec-type">{{ typeLabel }}</span>
      <span v-if="contextLabel" class="ec-context">{{ contextLabel }} →</span>
      <span class="ec-title">{{ displayTitle }}</span>

      <span v-if="collapsible" class="ec-toggle">
        <template v-if="nested">{{ expanded ? '▾' : '▸' }}</template>
        <template v-else>{{ expanded ? '▾ 收起' : '▸ 展开' }}</template>
      </span>
      <span v-else-if="clickable" class="ec-arrow">→</span>

      <button
        v-if="removable"
        class="ec-remove"
        title="移除"
        @click.stop="$emit('remove')"
      >✕</button>
    </div>

    <!-- ===== 内容区 ===== -->
    <div v-if="shouldShowBody" class="ec-body">
      <div v-if="loading" class="ec-loading">内容加载中...</div>

      <template v-else>
        <!-- 封面 -->
        <div v-if="coverUrl" class="ec-cover" @click.stop="previewCover">
          <img :src="coverUrl" :alt="displayTitle" />
        </div>

        <!-- 图库 -->
        <div v-if="galleryImages.length" class="ec-gallery">
          <div
            v-for="(img, i) in galleryImages.slice(0, 6)"
            :key="i"
            class="ec-gallery-item"
            :class="{ 'is-clickable': clickable }"
            @click="onBodyClick"
          >
            <img :src="img" :alt="`图 ${i + 1}`" loading="lazy" />
          </div>
        </div>

        <!-- 属性（全部显示） -->
        <div v-if="displayAttributes.length" class="ec-attributes">
          <div v-for="attr in displayAttributes" :key="attr.key" class="ec-attr">
            <span class="ec-attr-key">{{ attr.key }}</span>
            <span class="ec-attr-value">{{ attr.value }}</span>
          </div>
        </div>

        <!-- 描述（完整显示） -->
        <div v-if="description" class="ec-description">
          {{ description }}
        </div>

        <!-- 标签（全部显示） -->
        <div v-if="allTags.length" class="ec-tags">
          <span v-for="tag in allTags" :key="tag" class="ec-tag">#{{ tag }}</span>
        </div>

        <!-- 嵌套嵌入 -->
        <div v-if="nestedBlocks.length" class="ec-nested">
          <div class="ec-nested-label">包含内容</div>
          <EmbeddedCard
            v-for="child in nestedBlocks"
            :key="child.blockId"
            :card="getChildDetail(child.cardId)"
            :context-label="child.contextLabel"
            :fallback-title="child.cardTitle"
            :fallback-type="child.cardType"
            :loading="!getChildDetail(child.cardId) && childLoading"
            :project-id="projectId"
            nested
            collapsible
            :expanded="isChildExpanded(child.blockId)"
            :depth="(depth || 0) + 1"
            :max-depth="maxDepth"
            :ancestor-ids="nextAncestorIds"
            @update:expanded="(v: boolean) => toggleChildExpand(child.blockId, v)"
          />
        </div>

        <!-- 查看详情（仅嵌套层） -->
        <div
          v-if="nested && card && projectId"
          class="ec-view-more"
          @click.stop="goToDetail"
        >
          查看详情 →
        </div>
      </template>
    </div>

    <!-- 图片查看器 -->
    <el-image-viewer
      v-if="coverPreviewVisible"
      :url-list="previewImages"
      @close="closePreview"
    />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useRouter } from 'vue-router';
import { useWorldStore } from '@/stores/world';
import { ElImageViewer } from 'element-plus';
import { getCardTypeLabel, parseStringArray, parseCoverImage, getNestedBlocks } from '@/utils/worldPresentation';
import { withThumbnail } from '@/utils/imageThumb';
import { useImagePreview } from '@/composables/世界观管理/useImagePreview';
import { useCardPreload } from '@/composables/世界观管理/useCardPreload';
import { useExpandedItems } from '@/composables/世界观管理/useExpandedItems';
import type { CardDetail } from '@/api/worldApi';

const props = defineProps<{
  card?: CardDetail | null;
  contextLabel?: string | null;
  collapsible?: boolean;
  expanded?: boolean;
  clickable?: boolean;
  removable?: boolean;
  loading?: boolean;
  projectId?: string;
  nested?: boolean;
  depth?: number;
  maxDepth?: number;
  ancestorIds?: string[];
  fallbackTitle?: string;
  fallbackType?: string;
  fallbackCover?: string;
  fallbackAttributes?: { key: string; value: any }[];
}>();

const emit = defineEmits<{
  (e: 'click'): void;
  (e: 'remove'): void;
  (e: 'update:expanded', value: boolean): void;
}>();

const router = useRouter();
const worldStore = useWorldStore();

const { visible: coverPreviewVisible, images: previewImages, open: openPreview, close: closePreview } = useImagePreview();
const previewCover = () => { if (coverUrl.value) openPreview([coverUrl.value]); };

// ===== 基础展示数据 =====
const displayTitle = computed(() =>
  props.card?.title || props.fallbackTitle || '未命名'
);

const typeLabel = computed(() => {
  const type = props.card?.type || props.fallbackType || '';
  return getCardTypeLabel(type) || '未知';
});

const coverUrl = computed(() => {
  if (props.card?.coverImage) return withThumbnail(parseCoverImage(props.card.coverImage), 400);
  if (props.fallbackCover) return withThumbnail(parseCoverImage(props.fallbackCover), 400);
  return '';
});

const galleryImages = computed(() => {
  if (!props.card) return [];
  return parseStringArray(props.card.galleryImages).map(url => withThumbnail(url, 400));
});

const displayAttributes = computed(() => {
  return (props.card?.attributes ?? props.fallbackAttributes ?? [])
    .filter(attr => String(attr.value ?? '').trim());
});

const description = computed(() => (props.card?.description || '').replace(/\{CARD:([^}]+)\}/g,
  (_, id: string) => `「${worldStore.getCardById(id, props.projectId)?.title || '不可用的条目'}」`));

const allTags = computed(() => {
  if (!props.card) return [];
  return parseStringArray(props.card.tags);
});

const shouldShowBody = computed(() => {
  if (props.collapsible) return props.expanded === true;
  return true;
});

// ===== 嵌套嵌入逻辑 =====
const MAX_DEPTH = computed(() => props.maxDepth ?? 2);
const currentDepth = computed(() => props.depth ?? 0);

const nextAncestorIds = computed(() => {
  const list = [...(props.ancestorIds || [])];
  if (props.card?.id) list.push(props.card.id);
  return list;
});

const nestedBlocks = computed(() => getNestedBlocks(props.card?.contentBlocks ?? [], props.card?.id,
  nextAncestorIds.value, currentDepth.value, MAX_DEPTH.value));
const { isExpanded: isChildExpanded, toggle: toggleChildExpand } = useExpandedItems();
const { loading: childLoading, getDetail: getChildDetail } = useCardPreload(
  () => props.projectId, () => nestedBlocks.value.map(block => block.cardId));

// ===== 交互 =====
const onHeaderClick = () => {
  if (props.collapsible) {
    emit('update:expanded', !props.expanded);
    return;
  }
  if (props.clickable) {
    emit('click');
  }
};

const onBodyClick = () => {
  if (props.clickable) {
    emit('click');
  }
};

const goToDetail = () => {
  if (!props.card?.id || !props.projectId) return;
  router.push(`/world/project/${props.projectId}/card/${props.card.id}`);
};
</script>

<style scoped>
/* ============================================================
   容器
   ============================================================ */
.embedded-card {
  border: 1px solid var(--border-line);
  border-radius: var(--radius-sm);
  border-left: 3px solid var(--surface-color, var(--accent-color));
  background: var(--paper-card);
  overflow: hidden;
  transition: border-color 0.15s, box-shadow 0.15s;
}
.embedded-card:hover {
  border-color: var(--border-line);
}
.embedded-card.is-expanded {
  border-color: var(--border-line);
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.03);
}

.embedded-card.is-nested {
  border-radius: 6px;
  background: var(--paper-card);
  border-color: var(--border-line);
  box-shadow: none;
}
.embedded-card.is-nested:hover {
  border-color: var(--border-line);
  background: var(--paper-card);
}
.embedded-card.is-nested.is-expanded {
  background: var(--paper-card);
  border-color: var(--border-line);
}

/* ============================================================
   头部
   ============================================================ */
.ec-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 14px;
  background: linear-gradient(115deg,var(--surface-wash,var(--bg-sub)),var(--paper-card));
  user-select: none;
  min-height: 40px;
}
.ec-header.is-clickable {
  cursor: pointer;
  transition: background 0.12s;
}
.ec-header.is-clickable:is(:hover,:focus-visible) { background: var(--accent-soft); outline: 2px solid var(--accent-color); outline-offset: -2px; }
.embedded-card.is-expanded .ec-header {
  background: var(--bg-main);
  border-bottom: 1px solid var(--border-line);
}

.is-nested .ec-header {
  padding: 7px 12px;
  min-height: 32px;
  background: transparent;
  gap: 6px;
}
.is-nested.is-expanded .ec-header {
  background: var(--bg-main);
  border-bottom: 1px solid var(--border-line);
}

.ec-type {
  font-size: 11px;
  color: var(--accent-color);
  background: var(--accent-soft);
  padding: 1px 8px;
  border-radius: 4px;
  flex-shrink: 0;
  font-weight: 500;
  line-height: 16px;
}
.is-nested .ec-type {
  font-size: 10px;
  padding: 0 6px;
  line-height: 15px;
  background: var(--bg-sub);
  color: var(--accent-color);
}

.ec-context {
  font-size: 12px;
  font-weight: 500;
  color: var(--accent-color);
  flex-shrink: 0;
}
.is-nested .ec-context {
  font-size: 11px;
  color: var(--text-secondary);
  font-weight: 400;
}

.ec-title {
  font-weight: 500;
  font-size: 14px;
  color: var(--text-primary);
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  letter-spacing: -0.1px;
}
.is-nested .ec-title {
  font-size: 13px;
  color: var(--text-primary);
}

.ec-toggle {
  font-size: 12px;
  color: var(--text-secondary);
  flex-shrink: 0;
  transition: color 0.15s;
  white-space: nowrap;
}
.ec-header.is-clickable:hover .ec-toggle {
  color: var(--accent-color);
}
.is-nested .ec-toggle {
  font-size: 14px;
  color: var(--text-secondary);
  width: 16px;
  text-align: center;
}
.is-nested .ec-header.is-clickable:hover .ec-toggle {
  color: var(--accent-color);
}

.ec-arrow {
  color: var(--text-secondary);
  flex-shrink: 0;
  transition: transform 0.15s, color 0.15s;
}
.ec-header.is-clickable:hover .ec-arrow {
  transform: translateX(2px);
  color: var(--accent-color);
}

.ec-remove {
  flex-shrink: 0;
  background: none;
  border: none;
  color: var(--text-secondary);
  cursor: pointer;
  font-size: 13px;
  padding: 3px 6px;
  border-radius: 4px;
  transition: all 0.12s;
  line-height: 1;
}
.ec-remove:hover {
  color: var(--danger);
  background: var(--danger-soft);
}

/* ============================================================
   内容区
   ============================================================ */
.ec-body {
  padding: 14px 16px;
  background: var(--paper-card);
}
.is-nested .ec-body {
  padding: 10px 12px 12px;
  background: var(--paper-card);
}

.ec-loading {
  padding: 8px;
  text-align: center;
  font-size: 12px;
  color: var(--text-secondary);
  animation: ec-pulse 1.5s ease-in-out infinite;
}
@keyframes ec-pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.45; }
}

/* ============================================================
   封面：图片完整显示 + max-height 兜底
   ============================================================ */
.ec-cover {
  border-radius: 6px;
  overflow: hidden;
  margin-bottom: 12px;
  background: var(--bg-main);
  cursor: zoom-in;
  display: flex;
  justify-content: center;
  align-items: flex-start;
}
.ec-cover img {
  max-width: 100%;
  max-height: 420px;
  width: auto;
  height: auto;
  object-fit: contain;
  display: block;
  transition: transform 0.25s;
}
.ec-cover:hover img {
  transform: scale(1.01);
}

.is-nested .ec-cover {
  margin-bottom: 8px;
  border-radius: 5px;
}
.is-nested .ec-cover img {
  max-height: 280px;
}

/* ============================================================
   图库
   ============================================================ */
.ec-gallery {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(72px, 1fr));
  gap: 6px;
  margin-bottom: 12px;
}
.ec-gallery-item {
  aspect-ratio: 1;
  border-radius: 5px;
  overflow: hidden;
  background: var(--bg-sub);
  border: 1px solid var(--border-line);
}
.ec-gallery-item.is-clickable {
  cursor: pointer;
}
.ec-gallery-item img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
  transition: transform 0.15s;
}
.ec-gallery-item.is-clickable:hover img {
  transform: scale(1.03);
}

.is-nested .ec-gallery {
  grid-template-columns: repeat(auto-fill, minmax(52px, 1fr));
  gap: 4px;
  margin-bottom: 8px;
}

/* ============================================================
   属性（全部显示，无截断）
   ============================================================ */
.ec-attributes {
  display: grid;
  grid-template-columns: max-content 1fr;
  gap: 3px 14px;
  margin-bottom: 12px;
}
.is-nested .ec-attributes {
  gap: 2px 10px;
  margin-bottom: 8px;
}
.ec-attr {
  display: contents;
}
.ec-attr-key {
  font-size: 12px;
  color: var(--text-secondary);
  padding: 1px 0;
  white-space: nowrap;
}
.ec-attr-value {
  font-size: 12.5px;
  color: var(--text-primary);
  padding: 1px 0;
  word-break: break-word;
  white-space: pre-wrap;
}
.is-nested .ec-attr-key {
  font-size: 11px;
}
.is-nested .ec-attr-value {
  font-size: 11.5px;
  color: var(--text-secondary);
}

/* ============================================================
   描述（完整显示，无截断）
   ============================================================ */
.ec-description {
  font-size: 13px;
  line-height: 1.7;
  color: var(--text-primary);
  margin-bottom: 12px;
  white-space: pre-wrap;
  word-break: break-word;
}
.is-nested .ec-description {
  font-size: 12px;
  line-height: 1.6;
  color: var(--text-secondary);
  margin-bottom: 8px;
}

/* ============================================================
   标签（全部显示）
   ============================================================ */
.ec-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}
.ec-tag {
  font-size: 11px;
  color: var(--accent-color);
  background: var(--bg-sub);
  padding: 1px 7px;
  border-radius: 3px;
  line-height: 16px;
}
.is-nested .ec-tag {
  font-size: 10px;
  padding: 0 6px;
  line-height: 14px;
}

/* ============================================================
   嵌套容器
   ============================================================ */
.ec-nested {
  margin-top: 12px;
  padding-top: 10px;
  border-top: 1px dashed var(--border-line);
  display: flex;
  flex-direction: column;
  gap: 5px;
}
.is-nested .ec-nested {
  margin-top: 8px;
  padding-top: 8px;
  gap: 4px;
}
.ec-nested-label {
  font-size: 10.5px;
  color: var(--text-secondary);
  font-weight: 500;
  letter-spacing: 0.5px;
  text-transform: uppercase;
  margin-bottom: 1px;
}

/* ============================================================
   查看详情
   ============================================================ */
.ec-view-more {
  margin-top: 8px;
  font-size: 11.5px;
  color: var(--accent-color);
  cursor: pointer;
  text-align: right;
  transition: color 0.12s;
  user-select: none;
}
.ec-view-more:hover {
  color: var(--accent-color);
  text-decoration: underline;
}

/* ============================================================
   响应式
   ============================================================ */
@media (max-width: 640px) {
  .ec-body {
    padding: 12px 14px;
  }
  .ec-cover img {
    max-height: 320px;
  }
  .is-nested .ec-cover img {
    max-height: 220px;
  }
  .ec-attributes {
    gap: 2px 10px;
  }
  .ec-gallery {
    grid-template-columns: repeat(auto-fill, minmax(64px, 1fr));
    gap: 5px;
  }
  .is-nested .ec-body {
    padding: 8px 10px 10px;
  }
  .is-nested .ec-gallery {
    grid-template-columns: repeat(auto-fill, minmax(44px, 1fr));
  }
}
</style>
