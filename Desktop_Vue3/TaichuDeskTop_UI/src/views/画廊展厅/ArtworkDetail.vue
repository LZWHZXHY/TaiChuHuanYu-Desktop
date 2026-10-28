<template>
  <div class="md-detail-mask" role="dialog" aria-modal="true" aria-label="作品详情" @click.self="$emit('close')">
    <div v-if="loading" class="md-loading-bar"></div>

    <div class="md-viewport content-page community-surface tone-gallery detail-surface">
      <nav class="md-controls content-actions">
        <button class="back-btn" @click="$emit('close')">
          <span class="arrow">←</span> 返回列表
        </button>
        <div class="doc-type">作品详情</div><button class="back-btn" @click="copyReference">复制引用链接</button>
        <template v-if="artwork?.canEdit">
          <RouterLink v-if="artwork.originalNoteId" :to="{ name: 'SpiritNote', params: { id: artwork.originalNoteId } }">编辑灵脉原稿</RouterLink>
          <button v-else class="back-btn" @click="editing = true">编辑作品</button>
        </template>
      </nav>

      <div v-if="error" class="detail-state" role="alert">{{ error }} <button @click="loadArtwork">重新加载</button></div>
      <p v-else-if="loading" class="detail-state" role="status">正在加载作品…</p>
      <p v-if="imageError" class="detail-state">部分图片加载失败，可重新加载作品。<button @click="loadArtwork">重试</button></p>
      <div class="md-wrapper" v-if="artwork">
        <section class="md-visual">
          <div v-if="steps.length === 0" class="img-placeholder">灵气汇聚中...</div>

          <!-- ========== Timeline 模式 ========== -->
          <div v-else-if="activeMode === 'timeline'" class="process-timeline">
            <div v-for="(step, idx) in steps" :key="idx" class="process-step">
              <div class="step-marker">
                <span class="step-num">{{ String(idx + 1).padStart(2, '0') }}</span>
                <span class="step-label">{{ step.title }}</span>
              </div>
              <div class="step-image">
                <img
                  :src="step.imageUrl"
                  :loading="idx === 0 ? undefined : 'lazy'"
                  @error="onImgError"
                  class="process-img"
                />
                <p v-if="step.description" class="image-description">{{ step.description }}</p>
              </div>
            </div>
          </div>

          <!-- ========== Masonry 模式 ========== -->
          <div v-else-if="activeMode === 'masonry'" class="masonry-gallery">
            <div v-for="(step, idx) in steps" :key="idx" class="masonry-item">
              <div class="masonry-image-wrapper">
                <img
                  :src="step.imageUrl"
                  :loading="idx === 0 ? undefined : 'lazy'"
                  @error="onImgError"
                />
              </div>
              <p v-if="step.description" class="masonry-description">{{ step.description }}</p>
            </div>
          </div>

          <!-- ========== Carousel 模式 ========== -->
          <div v-else-if="activeMode === 'carousel'" class="carousel-gallery">
            <div class="carousel-stage">
              <div class="carousel-image-wrapper">
                <img :src="currentStep.imageUrl" @error="onImgError" />
              </div>
              <p v-if="currentStep.description" class="carousel-description">{{ currentStep.description }}</p>
            </div>
            <div v-if="steps.length > 1" class="carousel-thumbs">
              <button
                v-for="(item, idx) in steps"
                :key="idx"
                :class="['thumb-btn', { active: idx === currentCarouselIndex }]"
                @click="currentCarouselIndex = idx"
              >
                <img :src="item.imageUrl" />
              </button>
            </div>
          </div>

          <!-- ========== Single 模式 ========== -->
          <div v-else class="single-final">
            <div class="single-image-wrapper">
              <img :src="steps[0].imageUrl" class="final-img" @error="onImgError" />
            </div>
            <p v-if="steps[0].description" class="single-description">{{ steps[0].description }}</p>
          </div>
        </section>

        <article class="md-article content-panel">
          <header class="article-header">
      <SectionTrace />
            <h1 class="title">{{ artwork.title }}</h1>
            <div class="metadata">
              <div class="author-block">
                <img v-if="artwork.author?.avatar" :src="artwork.author.avatar" class="mini-avatar" alt="作者头像" />
                <span v-else class="mini-avatar avatar-placeholder" aria-hidden="true">{{ artwork.author?.username?.[0] || '作' }}</span>
                <span class="name">{{ artwork.author?.username || '无名漫游者' }}</span>
              </div>
              <span class="divider">/</span>
              <span class="date">{{ new Date(artwork.uploadAt).toLocaleString() }}</span>
              <span v-if="artwork.watermarkEnabled" class="watermark-badge">🔒 水印保护</span>
            </div>
          </header>

          <p v-if="referenceMessage" role="status">{{ referenceMessage }}</p><p v-if="artwork.tags?.length" class="artwork-tags"><span v-for="tag in artwork.tags" :key="tag">#{{ tag }} </span></p>
          <section class="article-body">
            <div class="text-content">
              <template v-if="parsedContent?.length">
                <p v-for="(block, i) in parsedContent" :key="i" class="text-paragraph">{{ block.text }}</p>
              </template>
              <span v-else class="empty-tip">这一卷画作，画师未曾留下文字描述。</span>
            </div>
          </section>

          <footer class="article-footer">
            <div class="interaction-wrap">
              <InteractActions show-count
                v-if="artwork.id && artwork.canInteract !== false"
                :target-id="Number(artwork.id)"
                target-type="Artwork"
                :initial-stats="artwork"
              />
            </div>
            <p v-if="artwork.canInteract === false" class="detail-state">作品尚未公开，暂不开放互动和评论。</p>
            <ArtworkComments v-else :key="artwork.id" :artwork-id="artwork.id" :initial-count="artwork.commentsCount" />
          </footer>
        </article>
      </div>
    </div>
    <ArtworkPublishModal v-if="editing && artwork" v-model="editing" :artwork="artwork" @published="onEdited" />
  </div>
</template>

<script setup lang="ts">
import SectionTrace from '@/components/SectionTrace.vue'
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { artworkApi } from '../../api/artwork'
import type { ArtworkDetail } from '../../api/artwork'
import InteractActions from '../../components/InteractActions.vue'
import ArtworkPublishModal from './ArtworkPublishModal.vue'
import ArtworkComments from './ArtworkComments.vue'
import { useArtworkStore } from '../../stores/artwork'
import { useUserStore } from '../../stores/user'
import { toCdnUrl } from '../../utils/imageThumb'

const referenceMessage = ref('');
async function copyReference() { try { const url = new URL(window.location.href); url.hash = '/knowledge?detailType=artwork&detail=' + encodeURIComponent(String(props.id)); await navigator.clipboard.writeText(url.href); referenceMessage.value = '引用链接已复制'; } catch { referenceMessage.value = '复制失败，请复制浏览器地址栏中的链接'; } }
const props = defineProps<{ id: string | number; mode?: 'timeline' | 'masonry' | 'carousel' | 'auto' }>()
const emit = defineEmits(['close', 'updated'])
const editing = ref(false)
async function onEdited() { editing.value = false; emit('updated'); await loadArtwork() }

const numericId = computed(() => Number(props.id))
const artwork = ref<ArtworkDetail | null>(null)
const loading = ref(true), error = ref(''), imageError = ref(false)
const store = useArtworkStore(), user = useUserStore()
let requestVersion = 0
let previousOverflow = ''
const currentCarouselIndex = ref(0)

interface NormalizedStep {
  imageUrl: string
  description: string
  title: string
}

const steps = computed<NormalizedStep[]>(() => {
  const images = artwork.value?.images || []
  return images.map((item: any, idx: number) => {
    if (typeof item === 'object' && item !== null) {
      return { imageUrl: toCdnUrl(item.url), description: item.caption || '', title: '画幅' }
    }
    return { imageUrl: toCdnUrl(String(item)), description: '', title: `画幅 ${idx + 1}` }
  })
})

const currentStep = computed(
  () => steps.value[currentCarouselIndex.value] || { imageUrl: '', description: '', title: '' }
)

const parsedContent = computed(() => {
  if (!artwork.value?.description) return null
  try {
    const descObj = JSON.parse(artwork.value.description)
    return Array.isArray(descObj.content) ? descObj.content : [{ text: artwork.value.description }]
  } catch {
    return [{ text: artwork.value.description }]
  }
})

const activeMode = computed(() => {
  if (props.mode && props.mode !== 'auto') return props.mode
  const len = steps.value.length
  if (len === 1) return 'single'
  if (len <= 3) return 'timeline'
  return 'masonry'
})

const onImgError = () => { imageError.value = true }
async function loadArtwork() {
  const version = ++requestVersion, snapshot = store.capture()
  editing.value = false; artwork.value = null; loading.value = true; error.value = ''; imageError.value = false
  currentCarouselIndex.value = 0
  try {
    if (!Number.isInteger(numericId.value) || numericId.value <= 0) throw new Error('作品编号无效')
    const result = await artworkApi.getDetail(numericId.value)
    if (version !== requestVersion || snapshot.session !== store.capture().session) return
    artwork.value = result
    store.seed([result], snapshot)
  } catch (e: any) {
    if (version === requestVersion) error.value = e.friendlyMessage || e.message || '作品加载失败，请重试'
  } finally {
    if (version === requestVersion) loading.value = false
  }
}
watch(() => [numericId.value, user.token], loadArtwork, { immediate: true })
onMounted(() => { previousOverflow = document.body.style.overflow; document.body.style.overflow = 'hidden' })
onUnmounted(() => { requestVersion++; document.body.style.overflow = previousOverflow })
</script>

<style scoped>
.avatar-placeholder { display: inline-flex; align-items: center; justify-content: center; background: var(--paper-card); color: var(--text-secondary); }
.author-block { display: flex; align-items: center; gap: 6px; }
.detail-state { padding: 24px 0; color: var(--text-secondary); }
.detail-state button { font: inherit; color: var(--accent-color); background: var(--paper-card); border: 1px solid var(--border-line); padding: 6px 12px; cursor: pointer; }
/* 原有样式保持不变，已移除水印层样式 */
.md-detail-mask {
  position: fixed;
  inset: 0;
  z-index: 9999;
  background: var(--paper-bg);
  overflow-y: auto;

}

.md-viewport {
  width: 100%;
  max-width: 1000px;
  margin: 0 auto;
  padding: 0 32px 60px;
  box-sizing: border-box;
  position: relative;
}
.md-controls {
  position: sticky; top: 0; z-index: 10; background: var(--paper-bg);
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px 0;
  flex-wrap: wrap;
  gap: 10px;
}
.back-btn {
  background: none;
  border: none;
  font-size: 13px;
  font-weight: 600;
  letter-spacing: 0.1em;
  cursor: pointer;
}
.doc-type {
  font-size: 11px;
  color: var(--text-secondary);
  letter-spacing: 0.2em;
}
.md-visual {
  margin-bottom: 32px;
}

/* ===== Timeline ===== */
.process-timeline {
  position: relative;
  padding-left: 120px;
}
.process-timeline::before {
  content: '';
  position: absolute;
  left: 58px;
  top: 0;
  bottom: 0;
  width: 1px;
  background: var(--border-line);
}
.process-step {
  position: relative;
  margin-bottom: 32px;
  opacity: 1;
  animation: fadeUp 0.6s ease forwards;
}
@keyframes fadeUp {
  from {
    opacity: 0;
    transform: translateY(20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}
.step-marker {
  position: absolute;
  left: -120px;
  top: 0;
  width: 100px;
  text-align: right;
  display: flex;
  flex-direction: column;
  align-items: flex-end;
}
.step-num {
  font-size: 2rem;
  font-weight: 200;
  color: var(--text-primary);
}
.step-label {
  margin-top: 4px;
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.2em;
  color: var(--text-secondary);
  writing-mode: vertical-rl;
}
.step-image {
  background: var(--paper-card);
  border-radius: 4px;
  overflow: hidden;
  box-shadow: 0 30px 60px rgba(0, 0, 0, 0.04);
  transition: transform 0.3s;
  position: relative;
}
.step-image:hover {
  transform: translateY(-4px);
  box-shadow: 0 40px 80px rgba(0, 0, 0, 0.08);
}
.process-img {
  width: 100%;
  height: auto;
  display: block;
}
.image-description {
  margin-top: 12px;
  font-size: 0.95rem;
  color: var(--text-secondary);
  line-height: 1.8;
  padding: 0 12px 12px;
}

/* ===== Masonry ===== */
.masonry-gallery {
  columns: 2;
  gap: 24px;
}
.masonry-item {
  break-inside: avoid;
  margin-bottom: 24px;
  border-radius: 4px;
  overflow: hidden;
  box-shadow: 0 20px 50px rgba(0, 0, 0, 0.04);
  background: var(--paper-card);
}
.masonry-image-wrapper {
  position: relative;
}
.masonry-item img {
  width: 100%;
  display: block;
}
.masonry-description {
  margin: 8px 0;
  font-size: 0.85rem;
  color: var(--text-secondary);
  padding: 0 8px 8px;
}

/* ===== Carousel ===== */
.carousel-gallery {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 32px;
}
.carousel-stage {
  width: 100%;
  background: var(--paper-card);
  border-radius: 4px;
  overflow: hidden;
  box-shadow: 0 40px 100px rgba(0, 0, 0, 0.06);
  text-align: center;
}
.carousel-image-wrapper {
  position: relative;
}
.carousel-stage img {
  width: 100%;
  display: block;
}
.carousel-description {
  margin-top: 16px;
  font-size: 0.95rem;
  color: var(--text-secondary);
  text-align: center;
}
.carousel-thumbs {
  display: flex;
  gap: 12px;
  overflow-x: auto;
  margin-top: 16px;
}
.thumb-btn {
  width: 60px;
  height: 60px;
  padding: 0;
  border: 1px solid transparent;
  border-radius: 4px;
  opacity: 0.5;
  cursor: pointer;
}
.thumb-btn.active {
  opacity: 1;
  border-color: var(--text-primary);
}
.thumb-btn img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

/* ===== Single ===== */
.single-final {
  display: flex;
  flex-direction: column;
  align-items: center;
  background: var(--paper-card);
}
.single-image-wrapper {
  position: relative;
  width: 100%;
}
.final-img {
  max-width: 100%;
  height: auto;
  box-shadow: 0 40px 100px rgba(0, 0, 0, 0.06);
}
.single-description {
  margin-top: 16px;
  font-size: 0.95rem;
  color: var(--text-secondary);
  padding: 0 12px 12px;
}

/* ===== 其他 ===== */
.title {
  font-size: clamp(1.6rem, 4vw, 3rem);
  font-weight: 800;
  margin-bottom: 24px;
}
.metadata {
  display: flex;
  align-items: center;
  gap: 16px;
  color: var(--text-secondary);
  font-size: 14px;
  flex-wrap: wrap;
}
.mini-avatar {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  filter: grayscale(1);
}
.watermark-badge {
  font-size: 11px;
  background: rgba(0, 0, 0, 0.05);
  padding: 2px 8px;
  border-radius: 12px;
  color: var(--text-secondary);
}
.article-body {
  font-size: 1rem;
  line-height: 1.85;
  color: var(--text-primary);
  margin-bottom: 24px;
  white-space: pre-wrap;
}
.text-paragraph {
  margin-bottom: 1.2em;
}
.article-footer {
  margin-top: 48px;
  border-top: 1px solid var(--border-line);
  padding-top: 24px;
  text-align: center;
}
.md-loading-bar {
  position: fixed;
  top: 0;
  left: 0;
  height: 2px;
  background: #000;
  width: 100%;
  z-index: 10000;
  animation: loading-flow 2s infinite linear;
}
@keyframes loading-flow {
  0% {
    transform: translateX(-100%);
  }
  100% {
    transform: translateX(100%);
  }
}
@media (max-width: 768px) {
  .process-timeline { padding-left: 0; }
  .process-timeline::before { display: none; }
  .step-marker { position: static; width: auto; flex-direction: row; gap: 8px; margin-bottom: 12px; }
  .step-label { writing-mode: horizontal-tb; }
  .md-viewport {
    padding: 0 16px 60px;
  }
  .title {
    font-size: 2.2rem;
  }
  .masonry-gallery {
    columns: 1;
  }
}
</style>