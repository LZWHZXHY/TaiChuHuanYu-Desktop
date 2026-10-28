<template>
  <div class="doc-view">
    <article class="doc">
      <!-- 标题 -->
      <header class="doc-header">
        <h1 class="doc-title">{{ card.title }}</h1>
        <div class="doc-meta">
          <span class="doc-type">
            {{ getTypeLabel(card.type) }}
          </span>
          <span class="dot">·</span>
          <span>更新于 {{ formatDate(card.updatedAt) }}</span>
        </div>
      </header>

      <!-- 封面 -->
      <div v-if="card.coverImage" class="doc-cover">
        <img
          :src="coverUrl"
          :alt="card.title"
          @click="openPreview([coverUrl])"
        />
      </div>

      <!-- 描述 -->
      <section class="doc-section">
        <div
          v-if="card.description"
          class="prose"
          v-html="renderedDescription"
          @click="onProseClick"
        ></div>
        <p v-else class="empty-note">暂无描述</p>
      </section>

      <!-- 图库 -->
      <section
        v-if="card.galleryImages?.length"
        class="doc-section"
      >
        <h2 class="section-title">图库</h2>
        <div class="gallery">
          <img
            v-for="(img, idx) in card.galleryImages"
            :key="idx"
            :src="img"
            :alt="`图 ${idx + 1}`"
            loading="lazy"
            @click="openPreview(galleryImages, idx)"
          />
        </div>
      </section>

      <!-- 属性 -->
      <section v-if="visibleAttributes.length || contentFields.length" class="doc-section">
        <h2 class="section-title">属性</h2>
        <div class="attr-list">
          <div v-for="field in contentFields" :key="field.key" class="attr-item">
            <span class="attr-key">{{ field.label }}</span>
            <button v-if="field.cardId" class="content-ref" @click="$emit('select', field.cardId)">{{ field.value }}</button>
            <span v-else class="attr-value">{{ field.value }}</span>
          </div>
          <div
            v-for="attr in visibleAttributes"
            :key="attr.key"
            class="attr-item"
          >
            <span class="attr-key">{{ attr.key }}</span>
            <span class="attr-value">{{ attr.value }}</span>
          </div>
        </div>
      </section>

      <!-- 关联内容（内联展开） -->
      <section v-if="embeddedCards.length" class="doc-section">
        <h2 class="section-title">
          引用设定
          <span v-if="embeddedCards.length" class="section-count">
            {{ embeddedCards.length }}
          </span>
        </h2>
        <div v-if="embeddedCards.length" class="embedded-list">
          <EmbeddedCard
            v-for="item in embeddedCards"
            :key="item.id"
            :card="item"
            :context-label="card.contentBlocks?.find(block => block.cardId === item.id)?.contextLabel"
            :project-id="projectId"
            clickable
            @click="$emit('select', item.id)"
          />
        </div>
      </section>

      <!-- 被引用 -->
      <section v-if="backlinks.length" class="doc-section">
        <h2 class="section-title">
          被引用
          <span class="section-count">{{ backlinks.length }}</span>
        </h2>
        <div class="backlink-list">
          <button
            v-for="bk in backlinks"
            :key="bk.id"
            class="backlink-item"
            @click="$emit('select', bk.id)"
          >
            <span class="backlink-type">{{ getTypeLabel(bk.type) }}</span>
            <span class="backlink-title">{{ bk.title }}</span>
            <span class="backlink-arrow">→</span>
          </button>
        </div>
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
    <el-image-viewer v-if="previewVisible" :url-list="previewImages" :initial-index="previewIndex" @close="closePreview" />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useWorldStore } from '@/stores/world'
import { cardSchemas } from '../card-schemas'
import type { CardType } from '../card_type'
import { ElImageViewer } from 'element-plus'
import type { CardDetail } from '@/api/types/world'
import EmbeddedCard from './EmbeddedCard.vue'
import { getCardTypeLabel as getTypeLabel } from '@/utils/worldPresentation'
import { useCardPresentation } from '@/composables/世界观管理/useCardPresentation'
import { useCardPreload } from '@/composables/世界观管理/useCardPreload'
import { useCardBacklinks } from '@/composables/世界观管理/useCardBacklinks'
import { useImagePreview } from '@/composables/世界观管理/useImagePreview'
const props = defineProps<{ card: CardDetail; projectId: string }>()
const store = useWorldStore()
const visibleAttributes = computed(() => (props.card.attributes || []).filter(attr => String(attr.value ?? '').trim()))
const contentFields = computed(() => {
  let content: Record<string, unknown>
  try { content = JSON.parse(props.card.content || '{}') } catch { return [] }
  if (!content || typeof content !== 'object' || Array.isArray(content)) return []
  return Object.entries(content).filter(([, value]) => value != null && String(value).trim() !== '').map(([key, value]) => {
    const field = cardSchemas[props.card.type as CardType]?.find(field => field.key === key)
    const cardId = field?.type === 'card-ref' && typeof value === 'string' ? value : null
    const text = cardId ? store.getCardById(cardId, props.projectId)?.title || '查看关联条目'
      : Array.isArray(value) ? value.join('、') : typeof value === 'object' ? JSON.stringify(value) : String(value)
    return { key, label: field?.label || key, value: text, cardId }
  })
})
const emit = defineEmits<{ select: [id: string] }>()
const { embeddedIds, embeddedCards, cardTags, galleryImages, coverUrl, renderedDescription } =
  useCardPresentation(() => props.card, () => props.projectId, 'document')
useCardPreload(() => props.projectId, embeddedIds)
const { backlinks } = useCardBacklinks(() => props.projectId, () => props.card.id)
const { visible: previewVisible, images: previewImages, index: previewIndex, open: openPreview, close: closePreview } = useImagePreview()
const formatDate = (value: string) => value ? new Date(value).toLocaleDateString('zh-CN') : ''

function onProseClick(event: MouseEvent) {
  const target = (event.target as HTMLElement).closest('[data-card-id]') as HTMLElement | null
  const id = target?.getAttribute('data-card-id')
  if (id) emit('select', id)
}
</script>

<style scoped>
.content-ref { border: 0; padding: 0; text-align: left; background: none; color: var(--accent-color); cursor: pointer; font: inherit; }
.attr-value { white-space: pre-wrap; overflow-wrap: anywhere; }
* { box-sizing: border-box; }
.doc-view {
  padding: clamp(24px, 4vw, 48px) clamp(20px, 4vw, 40px) 32px;
}
.doc {
  max-width: 720px;
  margin: 0 auto;
}
.doc-header {
  margin-bottom: 32px;
}
.doc-title {
  font-size: clamp(24px, 3vw, 32px);
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 10px;
  letter-spacing: -0.5px;
  line-height: 1.25;
  overflow-wrap: anywhere;
}
.doc-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: var(--text-secondary);
}
.doc-meta .dot { color: var(--text-secondary); }
.doc-type {
  color: var(--accent-color);
  font-weight: 500;
}
.doc-cover {
  margin-bottom: 32px;
  border-radius: 8px;
  overflow: hidden;
  cursor: zoom-in;
  background: var(--bg-sub);
}
.doc-cover img {
  width: 100%;
  height: auto;
  max-height: 320px;
  object-fit: contain;
  display: block;
  transition: opacity 0.15s;
}
.doc-cover:hover img { opacity: 0.95; }
.doc-section {
  margin-bottom: 40px;
}
.section-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  font-weight: 600;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 1px;
  margin: 0 0 14px;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--border-line);
}
.section-count {
  font-size: 11px;
  color: var(--text-secondary);
  font-weight: 400;
  letter-spacing: 0;
  text-transform: none;
}
.prose {
  font-size: 16px;
  line-height: 1.95;
  color: var(--text-primary);
  white-space: pre-wrap;
  word-break: break-word;
}
.prose :deep(.inline-ref) {
  display: inline-block;
  color: var(--accent-color);
  background: var(--accent-soft);
  padding: 1px 8px;
  border-radius: 4px;
  font-size: 13px;
  cursor: pointer;
  margin: 0 2px;
  font-weight: 500;
}
.prose :deep(.inline-ref:hover) {
  background: var(--accent-soft);
}
.prose :deep(.inline-ref.broken) {
  background: var(--danger-soft);
  color: var(--danger);
}
.empty-note {
  font-size: 13px;
  color: var(--text-secondary);
  font-style: italic;
  margin: 0;
  padding: 4px 0;
}
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
  background: var(--bg-sub);
  transition: opacity 0.15s;
}
.gallery img:hover { opacity: 0.9; }
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
  color: var(--text-secondary);
}
.attr-value {
  color: var(--text-primary);
  word-break: break-word;
  white-space: pre-wrap;
}
.embedded-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.tag {
  font-size: 13px;
  color: var(--accent-color);
  cursor: default;
  transition: opacity 0.15s;
}
.tag:hover { opacity: 0.7; }

.backlink-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.backlink-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 8px;
  border: none;
  background: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 14px;
  color: var(--text-primary);
  text-align: left;
  transition: background 0.15s;
}
.backlink-item:hover {
  background: var(--bg-sub);
}
.backlink-type {
  font-size: 11px;
  color: var(--accent-color);
  background: var(--accent-soft);
  padding: 0 8px;
  border-radius: 4px;
  flex-shrink: 0;
}
.backlink-title {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.backlink-arrow {
  color: var(--text-secondary);
}
@media (max-width: 820px) {
  .doc-view { padding: 24px 20px 80px; }
  .doc-title { font-size: 24px; }
  .attr-item {
    grid-template-columns: 1fr;
    gap: 2px;
  }
  .attr-key {
    font-size: 12px;
    color: var(--text-secondary);
  }
}
</style>
