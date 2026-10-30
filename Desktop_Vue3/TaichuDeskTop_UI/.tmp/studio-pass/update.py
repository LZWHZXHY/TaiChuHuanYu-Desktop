from pathlib import Path
import re, shutil
r=Path.cwd(); backup=r/'.tmp/studio-pass/before'
def edit(path, transform):
    p=r/path; b=backup/path; b.parent.mkdir(parents=True,exist_ok=True)
    if not b.exists(): shutil.copy2(p,b)
    s=p.read_text(encoding='utf-8-sig'); p.write_text(transform(s),encoding='utf-8')
def gallery(s):
    s=s.replace('class="md-detail-mask" role=', 'ref="dialog" tabindex="-1" class="md-detail-mask" @keydown="onDialogKey" role=')
    s=s.replace('<div class="doc-type">作品详情</div>', '<label v-if="steps.length > 1" class="exhibit-layout">展示方式<select v-model="displayMode" aria-label="作品展示方式"><option value="auto">展台</option><option value="timeline">创作过程</option><option value="masonry">全部画幅</option></select></label>')
    a=s.index('        <section class="md-visual">'); b=s.index('        <article class="md-article',a)
    s=s[:a]+'''        <section class="md-visual" aria-label="作品展台">
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

'''+s[b:]
    s=s.replace('<article class="md-article content-panel">','<article ref="descriptionPanel" id="artwork-description" class="md-article content-panel">')
    s=s.replace('<h1 class="title">{{ artwork.title }}</h1>','<h2 class="title">{{ artwork.title }}</h2>')
    s=s.replace('onUnmounted, watch }','onUnmounted, watch, nextTick }')
    s=s.replace("const editing = ref(false)", "const dialog = ref<HTMLElement>()\nconst descriptionPanel = ref<HTMLElement>()\nlet previousFocus: HTMLElement | null = null\nconst displayMode = ref(props.mode || 'auto')\nwatch(() => props.mode, mode => { displayMode.value = mode || 'auto' })\nconst editing = ref(false)")
    a=s.index('const activeMode = computed'); b=s.index('const onImgError',a)
    s=s[:a]+'''const activeMode = computed(() => displayMode.value === 'auto' ? 'carousel' : displayMode.value)
function changeSlide(direction: number) {
  currentCarouselIndex.value = Math.max(0, Math.min(steps.value.length - 1, currentCarouselIndex.value + direction))
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

'''+s[b:]
    s=s.replace("currentCarouselIndex.value = 0\n  try", "currentCarouselIndex.value = 0; displayMode.value = props.mode || 'auto'\n  try")
    s=s.replace("onMounted(() => { previousOverflow = document.body.style.overflow; document.body.style.overflow = 'hidden' })", "onMounted(() => { previousOverflow = document.body.style.overflow; document.body.style.overflow = 'hidden'; previousFocus = document.activeElement as HTMLElement | null; nextTick(() => dialog.value?.focus()) })")
    s=s.replace("document.body.style.overflow = previousOverflow })", "document.body.style.overflow = previousOverflow; previousFocus?.focus() })")
    s=s[:s.index('<style scoped>')]+'''<style scoped>
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
'''
    return s
edit('src/views/画廊展厅/ArtworkDetail.vue', gallery)
