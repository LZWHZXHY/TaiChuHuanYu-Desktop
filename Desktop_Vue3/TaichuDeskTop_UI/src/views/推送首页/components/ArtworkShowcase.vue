<script setup lang="ts">
import ParticleState from '@/components/ParticleState.vue'
import UserLink from '@/components/UserLink.vue'
import { onMounted, onUnmounted, ref, watch } from 'vue'
import { artworkApi, type ArtworkItemDto } from '@/api/artwork'

const works = ref<ArtworkItemDto[]>([])
const strip = ref<HTMLElement>()
const loading = ref(true), failed = ref(false), position = ref(0), atEnd = ref(false)
async function load() {
  loading.value = true; failed.value = false
  try { works.value = (await artworkApi.getGallery(0, 8)).data.filter(work => work.coverImageUrl) }
  catch { failed.value = true }
  finally { loading.value = false }
}
let resize: ResizeObserver | undefined
watch(strip, el => {
  resize?.disconnect()
  if (el) { resize = new ResizeObserver(syncPosition); resize.observe(el); syncPosition() }
})
onUnmounted(() => resize?.disconnect())
function syncPosition() {
  const el = strip.value
  if (!el) return
  position.value = el.scrollLeft
  atEnd.value = el.scrollLeft + el.clientWidth >= el.scrollWidth - 2
}
function move(direction: number) {
  const el = strip.value
  if (!el) return
  el.scrollBy({ left: direction * el.clientWidth * .8, behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth' })
}
onMounted(load)
</script>

<template>
  <section class="art-showcase" aria-label="社区作品展览">
    <header class="showcase-heading">
      <div><p>COMMUNITY / GALLERY</p><h2>看见不同的想象<span aria-hidden="true">↗</span></h2></div>
      <div class="showcase-actions">
        <RouterLink to="/gallery">全部作品</RouterLink>
        <button v-if="works.length > 1" :disabled="position < 2" aria-label="向左浏览作品" @click="move(-1)">←</button>
        <button v-if="works.length > 1" :disabled="atEnd" aria-label="向右浏览作品" @click="move(1)">→</button>
      </div>
    </header>
    <ParticleState v-if="loading" class="showcase-state" text="正在布置展览" />
    <p v-else-if="failed" class="showcase-state" role="alert">作品暂时未能加载。<button @click="load">重试</button></p>
    <ParticleState v-else-if="!works.length" class="showcase-state" kind="empty" preset="gallery" text="新的作品，即将在这里相遇。" />
    <div v-else ref="strip" class="showcase-strip" tabindex="0" aria-label="左右滚动浏览作品" @scroll.passive="syncPosition" @keydown.left.prevent="move(-1)" @keydown.right.prevent="move(1)">
      <RouterLink v-for="(work, index) in works" :key="work.id" class="art-slice" data-depth :to="{ path: '/gallery', query: { workId: work.id } }" :aria-label="`查看作品：${work.title}`">
        <img :src="work.coverImageUrl" alt="" loading="lazy" decoding="async" @error="($event.target as HTMLImageElement).hidden = true" />
        <span class="slice-number" aria-hidden="true">{{ String(index + 1).padStart(2, '0') }}</span>
        <span class="slice-caption"><strong>{{ work.title }}</strong><small><UserLink nested :id="work.authorId" :name="work.authorName" /></small><span class="slice-arrow" aria-hidden="true">↗</span></span>
      </RouterLink>
    </div>
  </section>
</template>

<style scoped>
.art-showcase { margin: 36px 0 28px; min-width: 0; }
.showcase-heading { display: flex; justify-content: space-between; align-items: end; gap: 16px; margin-bottom: 18px; }
.showcase-heading p { font: 10px var(--font-mono); letter-spacing: .12em; color: var(--accent-color); margin: 0 0 8px; }
.showcase-heading h2 { margin: 0; font-size: clamp(22px, 2.2vw, 30px); letter-spacing: -.03em; }
.showcase-heading h2 span { color: var(--accent-color); margin-left: 12px; }
.showcase-actions { display: flex; align-items: center; gap: 8px; flex-shrink: 0; }
.showcase-actions a { color: var(--text-secondary); font-size: 13px; margin-right: 8px; }
.showcase-actions button, .showcase-state button { width: 40px; height: 40px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); cursor: pointer; transition: background .2s, color .2s; }
.showcase-actions button:not(:disabled):hover { background: var(--accent-color); color: var(--on-accent); }
.showcase-actions button:disabled { opacity: .3; cursor: default; }
.showcase-strip { display: flex; align-items: stretch; gap: 14px; overflow-x: auto; scroll-snap-type: x mandatory; scrollbar-width: thin; scrollbar-color: var(--surface-color, var(--accent-color)) transparent; padding: 4px 0 14px; }
.art-slice { position: relative; flex: 0 0 40%; height: clamp(260px, 30vw, 380px); overflow: hidden; scroll-snap-align: start; background: var(--diagram-paper); color: #fff; clip-path: polygon(28px 0, 100% 0, calc(100% - 28px) 100%, 0 100%); isolation: isolate; }
.art-slice:nth-child(even) { margin-top: 28px; }
.art-slice img { width: 100%; height: 100%; object-fit: cover; transition: transform .65s cubic-bezier(.2,.7,.2,1); }
.art-slice::after { content: ''; position: absolute; inset: 45% 0 0; z-index: 0; background: linear-gradient(transparent, rgb(4 14 24 / .9)); pointer-events: none; }
.art-slice:is(:hover,:focus-visible) img { transform: scale(1.055); }
.art-slice:is(:hover,:focus-visible) { box-shadow: inset 0 -4px var(--signal-color); }
.slice-number { position: absolute; top: 16px; left: 40px; font: 12px var(--font-mono); padding: 5px 8px; background: rgb(4 14 24 / .65); }
.slice-caption { position: absolute; inset: auto 40px 24px 24px; z-index: 1; padding-right: 25px; }
.slice-caption strong { display: block; font-size: 20px; line-height: 1.4; overflow-wrap: anywhere; }
.slice-caption small { display: block; margin-top: 8px; color: #d7e8ef; }
.slice-arrow { position: absolute; right: 0; bottom: 0; font-size: 24px; transition: transform .25s; }
.art-slice:is(:hover,:focus-visible) .slice-arrow { transform: translate(3px,-3px); }
.showcase-strip:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 4px; }
.art-slice:focus-visible::before { content: ''; position: absolute; inset: 0; border-block: 4px solid var(--signal-color); z-index: 2; pointer-events: none; }
.showcase-state { padding: 30px 20px; background: var(--paper-card); border: 1px solid var(--border-line); color: var(--text-secondary); }
@media(max-width: 640px) { .showcase-heading { align-items: start; flex-direction: column; }.showcase-actions { align-self: end; }.art-slice { flex-basis: 82%; height: 300px; }.art-slice:nth-child(even) { margin-top: 18px; } }
@media(prefers-reduced-motion: reduce) { .art-slice img, .slice-arrow { transition: none; transform: none !important; } }
</style>
