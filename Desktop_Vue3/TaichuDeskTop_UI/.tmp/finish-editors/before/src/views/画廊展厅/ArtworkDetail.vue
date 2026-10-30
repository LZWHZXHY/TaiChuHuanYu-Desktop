<template>
  <div ref="dialog" tabindex="-1" class="md-detail-mask" @keydown="onDialogKey" role="dialog" aria-modal="true" aria-label="作品详情" @click.self="$emit('close')">
    <div v-if="loading" class="md-loading-bar"></div>

    <div class="md-viewport content-page community-surface tone-gallery detail-surface">
      <nav class="md-controls content-actions">
        <button class="back-btn" @click="$emit('close')">
          <span class="arrow">←</span> 返回列表
        </button>
        <label v-if="steps.length > 1" class="exhibit-layout">展示方式<select v-model="displayMode" aria-label="作品展示方式"><option value="auto">展台</option><option value="timeline">创作过程</option><option value="masonry">全部画幅</option></select></label><button class="back-btn" @click="copyReference">复制引用链接</button>
        <template v-if="artwork?.canEdit">
          <RouterLink v-if="artwork.originalNoteId" :to="{ name: 'SpiritNote', params: { id: artwork.originalNoteId } }">编辑灵脉原稿</RouterLink>
          <button v-else class="back-btn" @click="editing = true">编辑作品</button>
        </template>
      </nav>

      <div v-if="error" class="detail-state" role="alert">{{ error }} <button @click="loadArtwork">重新加载</button></div>
      <p v-else-if="loading" class="detail-state" role="status">正在加载作品…</p>
      <p v-if="imageError" class="detail-state">部分图片加载失败，可重新加载作品。<button @click="loadArtwork">重试</button></p>
      <div class="md-wrapper" v-if="artwork">
        <section class="md-visual" aria-label="作品展台">
          <div v-if="steps.length === 0" class="img-placeholder">暂无可展示的图片</div>
          <template v-else-if="activeMode === 'timeline' || activeMode === 'masonry'">
            <div :class="activeMode === 'timeline' ? 'process-timeline' : 'masonry-gallery'">
              <figure v-for="(step, idx) in steps" :key="idx" class="exhibit-frame">
                <img :src="step.imageUrl" :alt="step.description || `${artwork.title} · 画幅 ${idx + 1}`" :loading="idx === 0 ? undefined : 'lazy'" @error="onImgError" />
                <figcaption><span>{{ String(idx + 1).padStart(2, '0') }}</span>{{ step.description || artwork.title }}</figcaption>
              </figure>
            </div>
          </template>
          <div v-else class="exhibit-stage">
            <div class="exhibit-image">
              <img :src="currentStep.imageUrl" :alt="currentStep.description || artwork.title" @error="onImgError" />
            </div>
            <div class="exhibit-caption">
              <div class="exhibit-label">
                <p class="exhibit-index">{{ String(currentCarouselIndex + 1).padStart(2, '0') }} / {{ String(steps.length).padStart(2, '0') }}</p>
                <h1>{{ artwork.title }}</h1>
                <p class="exhibit-author">{{ artwork.author?.username || '无名漫游者' }}<span v-if="artwork.watermarkEnabled"> · 水印保护</span></p>
                <p v-if="currentStep.description" class="exhibit-note">{{ currentStep.description }}</p>
                <p v-else-if="parsedContent?.length" class="exhibit-note exhibit-summary">{{ parsedContent.map(block => block.text).join(' ') }}</p>
                <a class="exhibit-read" href="#artwork-description" @click.prevent="descriptionPanel?.scrollIntoView({ block: 'start' })">作品说明与评论 ↓</a>
              </div>
              <div v-if="steps.length > 1" class="exhibit-paging" aria-label="切换画幅">
                <button :disabled="currentCarouselIndex === 0" aria-label="上一张画幅" @click="changeSlide(-1)">←</button>
                <button :disabled="currentCarouselIndex === steps.length - 1" aria-label="下一张画幅" @click="changeSlide(1)">→</button>
              </div>
            </div>
            <div v-if="steps.length > 1" class="carousel-thumbs">
              <button v-for="(item, idx) in steps" :key="idx" :aria-label="`查看第 ${idx + 1} 张画幅`" :aria-pressed="idx === currentCarouselIndex" :class="['thumb-btn', { active: idx === currentCarouselIndex }]" @click="currentCarouselIndex = idx">
                <img :src="item.imageUrl" alt="" loading="lazy" />
              </button>
            </div>
          </div>
        </section>

        <article ref="descriptionPanel" id="artwork-description" class="md-article content-panel">
          <header class="article-header">
      <SectionTrace />
            <h2 class="title">{{ artwork.title }}</h2>
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
import { ref, computed, onMounted, onUnmounted, watch, nextTick } from 'vue'
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
const dialog = ref<HTMLElement>()
const descriptionPanel = ref<HTMLElement>()
let previousFocus: HTMLElement | null = null
const defaultDisplayMode = () => props.mode === 'carousel' ? 'auto' : props.mode || 'auto'
const displayMode = ref(defaultDisplayMode())
watch(() => props.mode, () => { displayMode.value = defaultDisplayMode() })
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

const parsedContent = computed<{ text: string }[] | null>(() => {
  if (!artwork.value?.description) return null
  try {
    const descObj = JSON.parse(artwork.value.description)
    return Array.isArray(descObj.content) ? descObj.content : [{ text: artwork.value.description }]
  } catch {
    return [{ text: artwork.value.description }]
  }
})

const activeMode = computed(() => displayMode.value === 'auto' ? 'carousel' : displayMode.value)
function changeSlide(direction: number) {
  currentCarouselIndex.value = Math.max(0, Math.min(steps.value.length - 1, currentCarouselIndex.value + direction))
  nextTick(() => { if (document.activeElement === document.body) dialog.value?.focus() })
}
function onDialogKey(event: KeyboardEvent) {
  if (editing.value) return
  if (event.key === 'Escape') { event.preventDefault(); emit('close'); return }
  const target = event.target as HTMLElement
  if (event.key === 'Tab' && dialog.value) {
    const controls = Array.from(dialog.value.querySelectorAll<HTMLElement>('button:not(:disabled), a[href], input:not(:disabled), textarea:not(:disabled), select:not(:disabled), [tabindex="0"]')).filter(el => el.getClientRects().length)
    const first = controls[0], last = controls[controls.length - 1]
    if (event.shiftKey && (target === first || target === dialog.value)) { event.preventDefault(); last?.focus() }
    else if (!event.shiftKey && target === last) { event.preventDefault(); first?.focus() }
  }
  if (target.closest('input,textarea,select,[contenteditable="true"]') || activeMode.value !== 'carousel') return
  if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') { event.preventDefault(); changeSlide(event.key === 'ArrowLeft' ? -1 : 1) }
}

const onImgError = () => { imageError.value = true }
async function loadArtwork() {
  const version = ++requestVersion, snapshot = store.capture()
  editing.value = false; artwork.value = null; loading.value = true; error.value = ''; imageError.value = false
  currentCarouselIndex.value = 0; displayMode.value = defaultDisplayMode()
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
onMounted(() => { previousOverflow = document.body.style.overflow; document.body.style.overflow = 'hidden'; previousFocus = document.activeElement as HTMLElement | null; nextTick(() => dialog.value?.focus()) })
onUnmounted(() => { requestVersion++; document.body.style.overflow = previousOverflow; previousFocus?.focus() })
</script>

<style scoped>
/* 展台固定暗色以还原作品；说明与评论跟随用户日夜设置。背景模糊只绘制一次，不参与动画。 */
.md-detail-mask { position: fixed; inset: 0; z-index: 9999; overflow-y: auto; overscroll-behavior: contain; background: rgb(4 8 12 / .94); backdrop-filter: blur(8px); outline: none; }
.md-viewport { width: 100%; max-width: 1440px; padding: 0 32px 48px; margin: auto; box-sizing: border-box; }
.md-viewport .md-controls { position: sticky; top: 0; z-index: 10; display: flex; align-items: center; flex-wrap: wrap; gap: 12px; padding: 14px 0; margin-bottom: 16px; background: #0b1017; border: 0; border-bottom: 1px solid #33404e; border-radius: 0; color: #f4f8fc; }
.md-controls :is(button,a,select) { font: inherit; font-size: 13px; color: #f4f8fc; background: #17232d; border: 1px solid #4b5b69; padding: 8px 12px; border-radius: 2px; cursor: pointer; text-decoration: none; }
.md-controls :is(button,a,select):focus-visible, .md-visual :is(button,a):focus-visible { outline: 2px solid #77dbec; outline-offset: 3px; }
.exhibit-layout { display: flex; align-items: center; gap: 8px; font-size: 12px; color: #b7c9d5; margin-left: auto; }
.md-visual { color: #f4f8fc; margin-bottom: 32px; }
.exhibit-image { min-height: 260px; height: min(65vh, 780px); display: grid; place-items: center; background: radial-gradient(ellipse at center,#17202b 0%,#090d13 75%); }
.exhibit-image img { width: 100%; height: 100%; max-height: min(65vh, 780px); object-fit: contain; }
.exhibit-caption { display: flex; justify-content: space-between; align-items: end; gap: 24px; padding: 24px 0 16px; border-bottom: 1px solid #33404e; }
.exhibit-label { min-width: 0; max-width: 780px; border-left: 2px solid #77dbec; padding-left: 18px; }
.exhibit-index { font: 10px var(--font-mono); letter-spacing: .18em; color: #96b8c8; margin: 0 0 8px; }
.exhibit-label h1 { font-size: clamp(20px, 2.6vw, 30px); color: #fff; margin: 0 0 8px; overflow-wrap: anywhere; }
.exhibit-author, .exhibit-note { font-size: 13px; line-height: 1.8; color: #c7d1dc; white-space: pre-wrap; overflow-wrap: anywhere; margin: 6px 0; }
.exhibit-summary { display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
.exhibit-read { display: inline-block; font-size: 12px; color: #8ce2f0; margin-top: 10px; text-underline-offset: 4px; }
.exhibit-paging { display: flex; gap: 8px; flex-shrink: 0; }
.exhibit-paging button { width: 44px; height: 44px; border: 1px solid #526474; background: #17232d; color: #fff; font-size: 20px; cursor: pointer; transition: background-color .18s; }
.exhibit-paging button:hover:not(:disabled) { background: #28505c; }.exhibit-paging button:disabled { opacity: .35; cursor: default; }
.carousel-thumbs { display: flex; gap: 10px; overflow-x: auto; padding: 16px 2px 8px; }
.thumb-btn { width: 64px; height: 56px; flex-shrink: 0; padding: 3px; border: 1px solid #526474; background: #101820; opacity: .65; cursor: pointer; }
.thumb-btn.active { border-color: #8ce2f0; opacity: 1; }.thumb-btn img { width: 100%; height: 100%; object-fit: contain; }
.exhibit-frame { margin: 0 0 24px; break-inside: avoid; background: #0b1017; }.exhibit-frame img { display: block; max-width: 100%; max-height: 85vh; object-fit: contain; margin: auto; }
.exhibit-frame figcaption { display: flex; gap: 16px; padding: 16px; font-size: 13px; color: #c7d1dc; white-space: pre-wrap; }.exhibit-frame figcaption span { color: #8ce2f0; font-family: var(--font-mono); }
.masonry-gallery { columns: 2; gap: 24px; }.masonry-gallery img { width: 100%; max-height: none; }
.md-article { max-width: 960px; margin: auto; padding: 24px; scroll-margin-top: 90px; background: var(--paper-card); color: var(--text-primary); }
.title { font-size: clamp(22px,3vw,32px); margin: 0 0 18px; overflow-wrap: anywhere; }.metadata { display: flex; gap: 12px; flex-wrap: wrap; align-items: center; font-size: 13px; color: var(--text-secondary); }
.author-block { display: flex; gap: 8px; align-items: center; }.mini-avatar { width: 24px; height: 24px; border-radius: 50%; }.avatar-placeholder { display: grid; place-items: center; background: var(--paper-card); }
.article-body { line-height: 1.9; white-space: pre-wrap; overflow-wrap: anywhere; }.text-paragraph { margin: 20px 0; }.artwork-tags { margin: 20px 0; color: var(--accent-color); }
.article-footer { margin-top: 32px; border-top: 1px solid var(--border-line); padding-top: 24px; }.interaction-wrap { margin-bottom: 24px; }
.detail-state, .img-placeholder { padding: 24px 0; color: #d5e0eb; }.md-article .detail-state { color: var(--text-secondary); }.detail-state button { font: inherit; margin-left: 8px; cursor: pointer; }
.md-loading-bar { position: fixed; top: 0; height: 2px; width: 100%; background: #77dbec; z-index: 10000; }
@media(max-width:640px) { .md-viewport { padding: 0 12px 24px; }.md-viewport .md-controls { gap: 8px; }.exhibit-layout { order: 2; margin: 0; width: 100%; }.exhibit-image { height: 48vh; min-height: 200px; }.exhibit-image img { max-height: 48vh; }.exhibit-caption { gap: 12px; align-items: start; }.exhibit-label { padding-left: 12px; }.exhibit-paging { flex-direction: column; }.md-article { padding: 16px; scroll-margin-top: 130px; }.masonry-gallery { columns: 1; } }
@media(prefers-reduced-motion:reduce) { .exhibit-paging button { transition: none; } }
</style>
