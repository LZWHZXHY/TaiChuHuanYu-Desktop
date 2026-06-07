<template>
  <div class="md-detail-mask" @click.self="$emit('close')">
    <div v-if="loading" class="md-loading-bar"></div>

    <div class="md-viewport">
      <nav class="md-controls">
        <button class="back-btn" @click="$emit('close')">
          <span class="arrow">←</span> INDEX
        </button>
        <div class="doc-type">FRAGMENT / ARTWORK</div>
      </nav>

      <div class="md-wrapper" v-if="artwork">
        <section class="md-visual">
          <div v-if="steps.length === 0" class="img-placeholder">灵气汇聚中...</div>

          <div v-else-if="activeMode === 'timeline'" class="process-timeline">
            <div v-for="(step, idx) in steps" :key="idx" class="process-step">
              <div class="step-marker">
                <span class="step-num">{{ String(idx + 1).padStart(2, '0') }}</span>
                <span class="step-label">{{ step.title }}</span>
              </div>
              <div class="step-image">
                <img :src="step.imageUrl" :loading="idx === 0 ? undefined : 'lazy'" @load="onFirstImgLoad(idx)" @error="onImgError" class="process-img" />
                <p v-if="step.description" class="image-description">{{ step.description }}</p>
              </div>
            </div>
          </div>

          <div v-else-if="activeMode === 'masonry'" class="masonry-gallery">
            <div v-for="(step, idx) in steps" :key="idx" class="masonry-item">
              <img :src="step.imageUrl" :loading="idx === 0 ? undefined : 'lazy'" @load="onFirstImgLoad(idx)" @error="onImgError" />
              <p v-if="step.description" class="masonry-description">{{ step.description }}</p>
            </div>
          </div>

          <div v-else-if="activeMode === 'carousel'" class="carousel-gallery">
            <div class="carousel-stage">
              <img :src="currentStep.imageUrl" @load="loading = false" />
              <p v-if="currentStep.description" class="carousel-description">{{ currentStep.description }}</p>
            </div>
            <div v-if="steps.length > 1" class="carousel-thumbs">
              <button v-for="(item, idx) in steps" :key="idx"
                :class="['thumb-btn', { active: idx === currentCarouselIndex }]"
                @click="currentCarouselIndex = idx">
                <img :src="item.imageUrl" />
              </button>
            </div>
          </div>

          <div v-else class="single-final">
            <img :src="steps[0].imageUrl" class="final-img" @load="loading = false" @error="onImgError" />
            <p v-if="steps[0].description" class="single-description">{{ steps[0].description }}</p>
          </div>
        </section>

        <article class="md-article">
          <header class="article-header">
            <h1 class="title">{{ artwork.title }}</h1>
            <div class="metadata">
              <div class="author-block">
                <img :src="artwork.author?.avatar || '/default-avatar.png'" class="mini-avatar" />
                <span class="name">{{ artwork.author?.username || '无名漫游者' }}</span>
              </div>
              <span class="divider">/</span>
              <span class="date">{{ artwork.uploadAt }}</span>
            </div>
          </header>

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
              <InteractActions v-if="artwork.id" :target-id="Number(artwork.id)" target-type="Artwork" :initial-stats="{ likesCount: 0 }" />
            </div>
            <p class="end-mark">END OF FRAGMENT</p>
          </footer>
        </article>
      </div>
    </div>
    <button class="floating-close" @click="$emit('close')">✕</button>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { artworkApi, type ArtworkDetail } from '../../api/artwork'
import InteractActions from '../../components/InteractActions.vue'

const props = defineProps<{ id: string | number, mode?: 'timeline' | 'masonry' | 'carousel' | 'auto' }>()
const emit = defineEmits(['close'])

const numericId = computed(() => Number(props.id))
const artwork = ref<ArtworkDetail | null>(null)
const loading = ref(true)
const currentCarouselIndex = ref(0)

interface NormalizedStep { imageUrl: string; description: string; title: string; }

const steps = computed<NormalizedStep[]>(() => {
  const images = artwork.value?.images || [];
  return images.map((item: any, idx: number) => {
    if (typeof item === 'object' && item !== null) {
      return { imageUrl: item.url || '', description: item.caption || '', title: '画幅' };
    }
    return { imageUrl: String(item), description: '', title: `画幅 ${idx + 1}` };
  });
});

const currentStep = computed(() => steps.value[currentCarouselIndex.value] || { imageUrl: '', description: '', title: '' });

const parsedContent = computed(() => {
  if (!artwork.value?.description) return null
  try {
    const descObj = JSON.parse(artwork.value.description)
    return Array.isArray(descObj.content) ? descObj.content : [{ text: artwork.value.description }]
  } catch { return [{ text: artwork.value.description }] }
})

const activeMode = computed(() => {
  if (props.mode && props.mode !== 'auto') return props.mode
  const len = steps.value.length
  if (len === 1) return 'single'
  if (len <= 3) return 'timeline'
  return 'masonry'
})

const onFirstImgLoad = (idx: number) => { if (idx === 0) loading.value = false }
const onImgError = (e: Event) => { (e.target as HTMLImageElement).style.display = 'none' }

onMounted(async () => {
  document.body.style.overflow = 'hidden'
  try {
    const res = await artworkApi.getDetail(numericId.value)
    artwork.value = res as any
  } catch (error) { console.error('获取灵脉详情失败:', error) }
  finally { if (steps.value.length === 0) loading.value = false }
})

onUnmounted(() => { document.body.style.overflow = '' })
watch(() => artwork.value?.images, () => { currentCarouselIndex.value = 0 })
</script>

<style scoped>
/* 你的完整样式定义 */
.md-detail-mask { position: fixed; inset: 0; z-index: 9999; background: #ffffff; overflow-y: auto; scrollbar-width: none; }
.md-detail-mask::-webkit-scrollbar { display: none; }
.md-viewport { width: 100%; max-width: 1000px; margin: 0 auto; padding: 0 40px; position: relative; }
.md-controls { display: flex; justify-content: space-between; align-items: center; padding: 40px 0; }
.back-btn { background: none; border: none; font-size: 13px; font-weight: 600; letter-spacing: 0.1em; cursor: pointer; }
.doc-type { font-size: 11px; color: #86868b; letter-spacing: 0.2em; }
.md-visual { margin-bottom: 80px; }
.process-timeline { position: relative; padding-left: 120px; }
.process-timeline::before { content: ''; position: absolute; left: 58px; top: 0; bottom: 0; width: 1px; background: #e0e0e0; }
.process-step { position: relative; margin-bottom: 80px; opacity: 0; animation: fadeUp 0.6s ease forwards; }
@keyframes fadeUp { from { opacity: 0; transform: translateY(20px); } to { opacity: 1; transform: translateY(0); } }
.step-marker { position: absolute; left: -120px; top: 0; width: 100px; text-align: right; display: flex; flex-direction: column; align-items: flex-end; }
.step-num { font-size: 2rem; font-weight: 200; color: #1a1a1a; }
.step-label { margin-top: 4px; font-size: 11px; text-transform: uppercase; letter-spacing: 0.2em; color: #86868b; writing-mode: vertical-rl; }
.step-image { background: #fbfbfb; border-radius: 4px; overflow: hidden; box-shadow: 0 30px 60px rgba(0,0,0,0.04); transition: transform 0.3s; }
.step-image:hover { transform: translateY(-4px); box-shadow: 0 40px 80px rgba(0,0,0,0.08); }
.process-img { width: 100%; height: auto; display: block; }
.image-description { margin-top: 12px; font-size: 0.95rem; color: #555; line-height: 1.8; padding: 0 12px 12px; }
.masonry-gallery { display: columns; columns: 2; gap: 24px; }
.masonry-item { break-inside: avoid; margin-bottom: 24px; border-radius: 4px; overflow: hidden; box-shadow: 0 20px 50px rgba(0,0,0,0.04); background: #fbfbfb; }
.masonry-item img { width: 100%; display: block; }
.masonry-description { margin: 8px 0; font-size: 0.85rem; color: #666; padding: 0 8px 8px; }
.carousel-gallery { display: flex; flex-direction: column; align-items: center; gap: 32px; }
.carousel-stage { width: 100%; background: #fbfbfb; border-radius: 4px; overflow: hidden; box-shadow: 0 40px 100px rgba(0,0,0,0.06); text-align: center; }
.carousel-stage img { width: 100%; display: block; }
.carousel-description { margin-top: 16px; font-size: 0.95rem; color: #555; text-align: center; }
.carousel-thumbs { display: flex; gap: 12px; overflow-x: auto; margin-top: 16px; }
.thumb-btn { width: 60px; height: 60px; padding: 0; border: 1px solid transparent; border-radius: 4px; opacity: 0.5; cursor: pointer; }
.thumb-btn.active { opacity: 1; border-color: #1a1a1a; }
.thumb-btn img { width: 100%; height: 100%; object-fit: cover; }
.single-final { display: flex; flex-direction: column; align-items: center; background: #fbfbfb; }
.final-img { max-width: 100%; height: auto; box-shadow: 0 40px 100px rgba(0,0,0,0.06); }
.title { font-size: 3.5rem; font-weight: 800; margin-bottom: 24px; }
.metadata { display: flex; align-items: center; gap: 16px; color: #86868b; font-size: 14px; }
.mini-avatar { width: 20px; height: 20px; border-radius: 50%; filter: grayscale(1); }
.article-body { font-size: 1.25rem; line-height: 2.2; color: #333; margin-bottom: 60px; white-space: pre-wrap; }
.text-paragraph { margin-bottom: 1.2em; }
.article-footer { margin-top: 120px; border-top: 1px solid #f2f2f2; padding-top: 60px; text-align: center; }
.end-mark { font-size: 10px; letter-spacing: 0.4em; color: #d2d2d7; margin-top: 60px; }
.md-loading-bar { position: fixed; top: 0; left: 0; height: 2px; background: #000; width: 100%; z-index: 10000; animation: loading-flow 2s infinite linear; }
@keyframes loading-flow { 0% { transform: translateX(-100%); } 100% { transform: translateX(100%); } }
.floating-close { position: fixed; top: 40px; right: 40px; border: none; background: none; font-size: 20px; cursor: pointer; opacity: 0.2; }
.floating-close:hover { opacity: 1; }
@media (max-width: 768px) {
  .md-viewport { padding: 0 24px; }
  .title { font-size: 2.2rem; }
  .masonry-gallery { columns: 1; }
}
</style>