<template>
  <div ref="dialog" tabindex="-1" class="md-detail-mask" @keydown="onDialogKey" role="dialog" aria-modal="true" aria-label="作品详情" @click.self="$emit('close')">
    <div v-if="loading" class="md-loading-bar"></div>

    <div :inert="viewerIndex !== null" :aria-hidden="viewerIndex !== null" class="md-viewport content-page community-surface tone-gallery detail-surface">
      <nav class="md-controls content-actions">
        <button class="back-btn" @click="$emit('close')">
          <span class="arrow">←</span> 返回列表
        </button>
        <label v-if="steps.length > 1" class="exhibit-layout">展示方式<select v-model="displayMode" aria-label="作品展示方式"><option value="auto">自动</option><option value="carousel">逐张查看</option><option value="timeline">创作过程</option><option value="masonry">全部画幅</option></select></label><button class="back-btn" @click="copyReference">复制引用链接</button>
        <template v-if="artwork?.canEdit">
          <RouterLink v-if="artwork.originalNoteId" :to="{ name: 'SpiritNote', params: { id: artwork.originalNoteId } }">编辑灵脉原稿</RouterLink>
          <button v-else class="back-btn" @click="editing = true">编辑作品</button>
          <button v-if="artwork.ownerState === 'public'" class="back-btn" :disabled="toggling" @click="toggleVisible(false)">下架作品</button>
          <button v-else-if="artwork.ownerState === 'withdrawn'" class="back-btn" :disabled="toggling" @click="toggleVisible(true)">重新上架</button>
        </template>
      </nav>
      <p v-if="artwork?.canEdit && artwork.ownerState && artwork.ownerState !== 'public'" class="owner-state" role="status">
        {{ OWNER_STATE[artwork.ownerState] ?? '这件作品现在没有公开，只有你自己能看到。' }}
      </p>
      <p v-if="toggleMessage" class="owner-state" role="status">{{ toggleMessage }}</p>

      <div v-if="error" class="detail-state" role="alert">{{ error }} <button @click="loadArtwork">重新加载</button></div>
      <p v-else-if="loading" class="detail-state" role="status">正在加载作品…</p>
      <p v-if="imageError" class="detail-state">部分图片加载失败，可重新加载作品。<button @click="loadArtwork">重试</button></p>
      <div class="md-wrapper" v-if="artwork">
        <section class="md-visual" aria-label="作品图片">
          <div v-if="!steps.length" class="img-placeholder">暂无可展示的图片</div>
          <div v-else-if="activeMode === 'timeline' || activeMode === 'masonry'" :class="activeMode === 'timeline' ? 'process-timeline' : 'masonry-gallery'">
            <figure v-for="(step, idx) in steps" :key="idx" class="exhibit-frame">
              <button class="image-open" :aria-label="`全屏查看第 ${idx + 1} 张图片`" @click="openImage(idx, $event)"><img :src="step.imageUrl" :alt="step.description || artwork.title" :loading="idx ? 'lazy' : undefined" @error="onImgError" /></button>
              <figcaption v-if="step.description">{{ step.description }}</figcaption>
            </figure>
          </div>
          <div v-else>
            <button class="image-open" aria-label="全屏查看当前图片" @click="openImage(currentCarouselIndex, $event)"><img :src="currentStep.imageUrl" :alt="currentStep.description || artwork.title" @error="onImgError" /></button>
            <p v-if="currentStep.description" class="image-caption">{{ currentStep.description }}</p>
            <div v-if="steps.length > 1" class="carousel-thumbs">
              <button v-for="(item, idx) in steps" :key="idx" :aria-label="`查看第 ${idx + 1} 张画幅`" :aria-pressed="idx === currentCarouselIndex" :class="['thumb-btn', { active: idx === currentCarouselIndex }]" @click="currentCarouselIndex = idx"><img :src="item.imageUrl" alt="" loading="lazy" /></button>
            </div>
          </div>
        </section>

        <article ref="descriptionPanel" id="artwork-description" class="md-article content-panel">
          <header class="article-header">
      <SectionTrace />
            <h1 class="title">{{ artwork.title }}</h1>
            <div class="metadata">
              <UserLink class="author-block" :id="artwork.author?.id" :name="artwork.author?.username">
                <img v-if="artwork.author?.avatar" :src="artwork.author.avatar" class="mini-avatar" alt="作者头像" />
                <span v-else class="mini-avatar avatar-placeholder" aria-hidden="true">{{ artwork.author?.username?.[0] || '作' }}</span>
                <span class="name">{{ artwork.author?.username || '无名漫游者' }}</span>
              </UserLink>
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
    <div v-if="viewerIndex !== null && artwork" ref="imageViewer" class="art-image-viewer" role="dialog" aria-modal="true" aria-label="全屏图片" tabindex="-1" @click.self="closeImage">
      <img class="viewer-image" :src="viewerStep?.imageUrl" :alt="viewerStep?.description || artwork.title" @error="onImgError" />
      <button class="viewer-close" aria-label="关闭全屏图片" @click="closeImage">×</button>
      <div class="viewer-caption"><span>{{ (viewerIndex + 1).toString().padStart(2, '0') }} / {{ steps.length.toString().padStart(2, '0') }}</span><strong>{{ artwork.title }}</strong><small>{{ artwork.author?.username || '无名漫游者' }}<template v-if="artwork.watermarkEnabled"> · 水印保护</template></small><p>{{ viewerStep?.description || parsedContent?.map(block => block.text).join(' ') }}</p></div>
      <div v-if="steps.length > 1" class="viewer-paging"><button aria-label="上一张图片" :disabled="viewerIndex === 0" @click="changeImage(-1)">←</button><button aria-label="下一张图片" :disabled="viewerIndex === steps.length - 1" @click="changeImage(1)">→</button></div>
    </div>
    <ArtworkPublishModal v-if="editing && artwork" v-model="editing" :artwork="artwork" @published="onEdited" />
  </div>
</template>

<script setup lang="ts">
import UserLink from '@/components/UserLink.vue'
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
const defaultDisplayMode = () => props.mode || 'auto'
const displayMode = ref(defaultDisplayMode())
watch(() => props.mode, () => { displayMode.value = defaultDisplayMode() })
const editing = ref(false)
// ---------- 作者自己下架 / 重新上架 ----------
const OWNER_STATE: Record<string, string> = {
  withdrawn: '这件作品已经下架：画廊、排行、搜索和你的主页里都看不到，只有你自己能打开。点「重新上架」可以恢复。',
  reviewing: '这件作品正在审核，审核通过后才会出现在画廊里。',
  hidden: '这件作品被管理员隐藏了，只有你自己能看到。有疑问可以联系管理员。',
  rejected: '这件作品没有通过审核，只有你自己能看到。',
}
const toggling = ref(false)
const toggleMessage = ref('')
async function toggleVisible(visible: boolean) {
  if (!artwork.value) return
  if (!visible && !window.confirm('下架后画廊、排行、搜索和你的主页里都看不到这件作品（评论和点赞会保留），之后可以随时重新上架。确定下架吗？')) return
  toggling.value = true; toggleMessage.value = ''
  try {
    const res = await artworkApi.setVisibility(numericId.value, visible)
    artwork.value.ownerState = res.ownerState
    toggleMessage.value = res.message
    emit('updated')
  } catch (e: any) {
    toggleMessage.value = e?.response?.data?.message || e?.friendlyMessage || '操作失败，请重试'
  } finally { toggling.value = false }
}
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

const activeMode = computed(() => displayMode.value !== 'auto' ? displayMode.value : steps.value.length <= 1 ? 'single' : steps.value.length <= 3 ? 'timeline' : 'masonry')
const viewerIndex = ref<number | null>(null)
const imageViewer = ref<HTMLElement>()
const viewerStep = computed(() => viewerIndex.value === null ? null : steps.value[viewerIndex.value])
let imageTrigger: HTMLElement | null = null
function openImage(index: number, event?: MouseEvent) {
  imageTrigger = event?.currentTarget as HTMLElement | null
  viewerIndex.value = index
  nextTick(() => imageViewer.value?.focus())
}
function closeImage() {
  viewerIndex.value = null
  nextTick(() => imageTrigger?.isConnected ? imageTrigger.focus() : dialog.value?.focus())
}
function changeImage(direction: number) {
  if (viewerIndex.value === null) return
  viewerIndex.value = Math.max(0, Math.min(steps.value.length - 1, viewerIndex.value + direction))
  nextTick(() => imageViewer.value?.focus())
  nextTick(() => { if (document.activeElement === document.body) imageViewer.value?.focus() })
}
function changeSlide(direction: number) {
  currentCarouselIndex.value = Math.max(0, Math.min(steps.value.length - 1, currentCarouselIndex.value + direction))
  nextTick(() => { if (document.activeElement === document.body) dialog.value?.focus() })
}
function onDialogKey(event: KeyboardEvent) {
  if (editing.value) return
  if (event.key === 'Escape') { event.preventDefault(); viewerIndex.value !== null ? closeImage() : emit('close'); return }
  const target = event.target as HTMLElement
  const activeDialog = viewerIndex.value !== null ? imageViewer.value : dialog.value
  if (event.key === 'Tab' && activeDialog) {
    const controls = Array.from(activeDialog.querySelectorAll<HTMLElement>('button:not(:disabled), a[href], input:not(:disabled), textarea:not(:disabled), select:not(:disabled), [tabindex="0"]')).filter(el => el.getClientRects().length)
    const first = controls[0], last = controls[controls.length - 1]
    if (event.shiftKey && (target === first || target === activeDialog)) { event.preventDefault(); last?.focus() }
    else if (!event.shiftKey && target === last) { event.preventDefault(); first?.focus() }
  }
  if (target.closest('input,textarea,select,[contenteditable="true"]') || (viewerIndex.value === null && activeMode.value !== 'carousel')) return
  if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') { event.preventDefault(); (viewerIndex.value !== null ? changeImage : changeSlide)(event.key === 'ArrowLeft' ? -1 : 1) }
}

const onImgError = () => { imageError.value = true }
async function loadArtwork() {
  const version = ++requestVersion, snapshot = store.capture()
  viewerIndex.value = null; editing.value = false; artwork.value = null; loading.value = true; error.value = ''; imageError.value = false
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
.md-detail-mask { position: fixed; inset: 0; z-index: 9999; overflow-y: auto; overscroll-behavior: contain; background: var(--bg-main); outline: none; }
.md-detail-mask:has(.art-image-viewer) { overflow: hidden; }
.md-viewport { width: 100%; max-width: 1200px; padding: 0 24px 48px; margin: auto; box-sizing: border-box; }
.md-viewport .md-controls { position: sticky; top: 0; z-index: 10; display: flex; align-items: center; flex-wrap: wrap; gap: 12px; padding: 14px 0; margin-bottom: 24px; background: var(--bg-main); border-bottom: 1px solid var(--border-line); }
.owner-state { margin: 0 0 12px; padding: 10px 14px; font-size: 13px; line-height: 1.6; background: var(--bg-sub); border-left: 3px solid var(--accent-color); color: var(--text-primary); }
.md-controls :is(button,a,select) { font: inherit; font-size: 13px; color: var(--text-primary); background: var(--paper-card); border: 1px solid var(--border-line); padding: 8px 12px; border-radius: 0; cursor: pointer; text-decoration: none; }
.md-controls :is(button,a,select):focus-visible, .md-visual button:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 3px; }
.exhibit-layout { display: flex; align-items: center; gap: 8px; font-size: 12px; color: var(--text-secondary); margin-left: auto; }
.md-visual { max-width: 960px; margin: 0 auto 32px; }
.image-open { display: block; border: 0; padding: 0; width: 100%; background: var(--bg-sub); cursor: zoom-in; }
.image-open img { display: block; width: 100%; max-height: 80vh; object-fit: contain; margin: auto; }
.exhibit-frame { margin: 0 0 24px; break-inside: avoid; }.image-caption,.exhibit-frame figcaption { padding: 10px 0; font-size: 13px; color: var(--text-secondary); white-space: pre-wrap; }
.masonry-gallery { columns: 2; gap: 24px; }.masonry-gallery img { max-height: none; }
.carousel-thumbs { display: flex; gap: 10px; overflow-x: auto; padding: 12px 2px; }
.thumb-btn { width: 64px; height: 56px; flex-shrink: 0; padding: 3px; border: 1px solid var(--border-line); background: var(--paper-card); cursor: pointer; }.thumb-btn.active { border-color: var(--accent-color); }.thumb-btn img { width: 100%; height: 100%; object-fit: contain; }
.art-image-viewer { position: fixed; inset: 0; z-index: 30; background: rgb(3 7 11 / .94); backdrop-filter: blur(8px); display: grid; place-items: center; outline: none; color: #fff; }
.viewer-image { width: 100%; height: 100%; max-height: 100dvh; object-fit: contain; }
.viewer-close,.viewer-paging button { border: 1px solid #667b88; background: rgb(10 20 28 / .8); color: #fff; width: 44px; height: 44px; cursor: pointer; font-size: 22px; }
.viewer-close { position: absolute; right: 20px; top: 20px; }.viewer-paging { position: absolute; right: 24px; bottom: 24px; display: flex; gap: 10px; }.viewer-paging button:disabled { opacity: .35; cursor: default; }
.art-image-viewer button:focus-visible { outline: 2px solid #77dbec; outline-offset: 3px; }
.art-image-viewer button:not(:disabled):hover { background: #254d5a; }
.viewer-caption { position: absolute; left: 0; bottom: 0; padding: 40px 28px 24px; max-width: min(600px,calc(100% - 156px)); background: linear-gradient(transparent,rgb(0 0 0 / .8)); pointer-events: none; overflow-wrap: anywhere; }
.viewer-caption span,.viewer-caption small { display: block; font-size: 11px; color: #d2dce4; }.viewer-caption strong { display: block; font-size: 17px; margin: 8px 0; }.viewer-caption p { font-size: 12px; line-height: 1.7; margin: 8px 0 0; display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; overflow: hidden; }
.md-article { max-width: 960px; margin: auto; padding: 24px; scroll-margin-top: 90px; background: var(--paper-card); color: var(--text-primary); }
.title { font-size: clamp(22px,3vw,32px); margin: 0 0 18px; overflow-wrap: anywhere; }.metadata { display: flex; gap: 12px; flex-wrap: wrap; align-items: center; font-size: 13px; color: var(--text-secondary); }
.author-block { display: flex; gap: 8px; align-items: center; }.mini-avatar { width: 24px; height: 24px; border-radius: 0; }.avatar-placeholder { display: grid; place-items: center; background: var(--paper-card); }
.article-body { line-height: 1.9; white-space: pre-wrap; overflow-wrap: anywhere; }.text-paragraph { margin: 20px 0; }.artwork-tags { margin: 20px 0; color: var(--accent-color); }
.article-footer { margin-top: 32px; border-top: 1px solid var(--border-line); padding-top: 24px; }.interaction-wrap { margin-bottom: 24px; }
.detail-state, .img-placeholder { padding: 24px 0; color: var(--text-secondary); }.md-article .detail-state { color: var(--text-secondary); }.detail-state button { font: inherit; margin-left: 8px; cursor: pointer; }
.md-loading-bar { position: fixed; top: 0; height: 2px; width: 100%; background: var(--accent-color); z-index: 10000; }
@media(max-width:640px) { .md-viewport { padding: 0 12px 24px; }.exhibit-layout { order: 2; margin: 0; width: 100%; }.md-article { padding: 16px; }.masonry-gallery { columns: 1; }.viewer-caption { padding: 30px 16px 16px; }.viewer-close { right: 12px; top: 12px; }.viewer-paging { right: 12px; bottom: 16px; } }
</style>
