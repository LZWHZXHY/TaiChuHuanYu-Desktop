<script setup lang="ts">
import ParticleState from '@/components/ParticleState.vue'
import UserLink from '@/components/UserLink.vue'
import { computed, onMounted, ref } from 'vue'
import { notePublishApi } from '@/api/NotePublish'
import { formatPostCard, relativeTime, type PostCard } from '@/utils/postCard'
import { boardOf, FORUM_NAME } from '@/utils/forumBoards'

// 首页的论坛窗口：带图的帖子做成大卡片，其余新帖排成列表。
const posts = ref<PostCard[]>([])
const loading = ref(true), failed = ref(false)

async function load() {
  loading.value = true; failed.value = false
  try { posts.value = (await notePublishApi.getPublicStream({ page: 1, pageSize: 20 }) || []).map(formatPostCard) }
  catch { failed.value = true }
  finally { loading.value = false }
}
onMounted(load)

const visual = computed(() => posts.value.filter(post => post.cardCover).slice(0, 3))
const latest = computed(() => {
  const shown = new Set(visual.value.map(post => post.id))
  return posts.value.filter(post => !shown.has(post.id)).slice(0, visual.value.length ? 6 : 8)
})
const route = (id: string) => ({ name: 'CommunityStream', query: { post: id } })
const typeLabel = (type: string) => type === 'blog' ? '博客' : '帖子'
const place = (post: PostCard) => {
  const { board, sub } = boardOf(post.tags)
  return board ? [board.tag, sub?.tag].filter(Boolean).join(' · ') : FORUM_NAME
}
const hideImage = (event: Event) => { (event.target as HTMLImageElement).closest('.visual-card')?.classList.add('no-image') }
</script>

<template>
  <section class="forum-highlights" aria-label="论坛新帖">
    <header class="forum-heading">
      <div>
        <p>FORUM / LATEST TRANSMISSION</p>
        <h2>论坛新帖<span aria-hidden="true">↗</span></h2>
      </div>
      <RouterLink to="/stream" class="forum-more">进入论坛 →</RouterLink>
      <i class="heading-scan" aria-hidden="true"></i>
    </header>

    <ParticleState v-if="loading" class="forum-state" text="正在接收新帖" />
    <p v-else-if="failed" class="forum-state" role="alert">新帖暂时没能加载。<button @click="load">重试</button></p>
    <p v-else-if="!posts.length" class="forum-state">论坛还没有帖子，<RouterLink to="/write/post">来发第一帖</RouterLink>。</p>

    <div v-else class="forum-grid" :class="{ 'text-only': !visual.length }">
      <div v-if="visual.length" class="visual-column" :class="'count-' + visual.length">
        <RouterLink v-for="(post, index) in visual" :key="post.id" :to="route(post.id)" class="visual-card" data-depth :aria-label="`查看${typeLabel(post.type)}：${post.title || '无标题'}`">
          <img :src="post.cardCover" alt="" loading="lazy" decoding="async" @error="hideImage" />
          <span class="visual-index" aria-hidden="true">IMG.{{ String(index + 1).padStart(2, '0') }}</span>
          <span class="visual-caption">
            <small class="visual-place">{{ place(post) }}</small>
            <strong>{{ post.title || '无标题' }}</strong>
            <small><UserLink nested :id="post.authorId" :name="post.authorName || '社区成员'" /> · {{ relativeTime(post.publishedAt || post.createdAt) }}</small>
          </span>
        </RouterLink>
      </div>

      <ol v-if="latest.length" class="latest-list">
        <li v-for="(post, index) in latest" :key="post.id">
          <RouterLink :to="route(post.id)" class="latest-item">
            <span class="latest-no" aria-hidden="true">{{ String(index + 1).padStart(2, '0') }}</span>
            <span class="latest-body">
              <span class="latest-meta"><b :class="'type-' + post.type">{{ typeLabel(post.type) }}</b>{{ place(post) }}</span>
              <strong>{{ post.title || '无标题' }}</strong>
              <small v-if="post.content">{{ post.content }}</small>
            </span>
            <span class="latest-side">
              <time :datetime="post.publishedAt">{{ relativeTime(post.publishedAt || post.createdAt) }}</time>
              <span v-if="post.commentsCount" class="latest-comments">{{ post.commentsCount }} 评论</span>
            </span>
          </RouterLink>
        </li>
      </ol>
    </div>
  </section>
</template>

<style scoped>
.forum-highlights { margin: 8px 0 36px; min-width: 0; }
.forum-heading { position: relative; display: flex; justify-content: space-between; align-items: end; gap: 16px; padding-bottom: 14px; margin-bottom: 18px; border-bottom: 1px solid var(--border-line); }
.forum-heading p { font: 10px var(--font-mono); letter-spacing: .12em; color: var(--accent-color); margin: 0 0 8px; }
.forum-heading h2 { margin: 0; font-size: clamp(22px, 2.2vw, 30px); letter-spacing: -.03em; }
.forum-heading h2 span { color: var(--accent-color); margin-left: 12px; }
.forum-more { color: var(--text-secondary); font-size: 13px; text-decoration: none; flex-shrink: 0; }
.forum-more:hover { color: var(--accent-color); }
/* 标题下方的扫描光标：沿分隔线来回移动的一小段信号色。 */
.heading-scan { position: absolute; left: 0; bottom: -1px; width: 72px; height: 2px; background: var(--signal-mint); }

.forum-grid { display: grid; grid-template-columns: minmax(0, 1.25fr) minmax(0, 1fr); gap: 20px; align-items: start; }
.forum-grid.text-only { grid-template-columns: minmax(0, 1fr); }
.forum-grid.text-only .latest-list { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); column-gap: 28px; }

.visual-column { display: grid; gap: 12px; grid-template-columns: 1fr 1fr; grid-auto-rows: clamp(150px, 15vw, 200px); }
.visual-column.count-1 { grid-template-columns: 1fr; grid-auto-rows: clamp(260px, 28vw, 380px); }
.visual-column.count-2 .visual-card { grid-row: span 2; }
.visual-column.count-3 .visual-card:first-child { grid-column: span 2; grid-row: span 2; }
.visual-card { position: relative; display: block; overflow: hidden; background: var(--diagram-paper); color: #fff; text-decoration: none; isolation: isolate; clip-path: polygon(18px 0, 100% 0, 100% calc(100% - 18px), calc(100% - 18px) 100%, 0 100%, 0 18px); }
.visual-card img { width: 100%; height: 100%; object-fit: cover; display: block; transition: transform .65s cubic-bezier(.2,.7,.2,1); }
.visual-card::after { content: ''; position: absolute; inset: 40% 0 0; z-index: 0; background: linear-gradient(transparent, rgb(4 14 24 / .88)); pointer-events: none; }
.visual-card::before { content: ''; position: absolute; inset: auto 0 0; height: 3px; z-index: 2; background: var(--signal-mint); transform: scaleX(0); transform-origin: left; transition: transform .35s ease; }
.visual-card:is(:hover, :focus-visible)::before { transform: scaleX(1); }
.visual-card:is(:hover, :focus-visible) img { transform: scale(1.05); }
.visual-card.no-image img { display: none; }
.visual-card.no-image { background: linear-gradient(135deg, var(--inverse-bg), var(--accent-color)); }
.visual-index { position: absolute; top: 12px; left: 24px; z-index: 1; font: 11px var(--font-mono); padding: 4px 7px; background: rgb(4 14 24 / .65); letter-spacing: .06em; }
.visual-caption { position: absolute; inset: auto 18px 16px 18px; z-index: 1; display: grid; gap: 4px; }
.visual-caption strong { font-size: 17px; line-height: 1.35; overflow-wrap: anywhere; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
.visual-column.count-3 .visual-card:first-child .visual-caption strong, .visual-column.count-1 .visual-caption strong { font-size: 22px; }
.visual-caption small { color: #d7e8ef; font-size: 12px; }
.visual-caption .visual-place { width: fit-content; padding: 2px 6px; background: rgb(4 14 24 / .6); border-left: 2px solid var(--signal-mint); font: 10px/1.5 var(--font-mono); letter-spacing: .06em; color: #fff; }

.latest-list { list-style: none; margin: 0; padding: 0; min-width: 0; }
.latest-list li { border-bottom: 1px solid var(--border-line); }
.latest-item { position: relative; display: grid; grid-template-columns: auto minmax(0, 1fr) auto; gap: 12px; align-items: start; padding: 13px 4px 13px 10px; color: var(--text-primary); text-decoration: none; transition: background .2s; }
.latest-item::before { content: ''; position: absolute; left: 0; top: 10px; bottom: 10px; width: 2px; background: var(--signal-mint); transform: scaleY(0); transition: transform .25s ease; }
.latest-item:is(:hover, :focus-visible) { background: linear-gradient(90deg, var(--accent-soft), transparent 70%); }
.latest-item:is(:hover, :focus-visible)::before { transform: scaleY(1); }
.latest-item:is(:hover, :focus-visible) strong { color: var(--accent-color); }
.latest-no { font: 11px var(--font-mono); color: var(--text-secondary); padding-top: 3px; }
.latest-body { display: grid; gap: 3px; min-width: 0; }
.latest-meta { display: flex; align-items: center; gap: 8px; font: 10px/1.4 var(--font-mono); color: var(--text-secondary); letter-spacing: .04em; min-width: 0; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.latest-meta b { font-weight: 600; padding: 1px 5px; background: var(--bg-sub); color: var(--text-secondary); }
.latest-meta b.type-post { background: var(--accent-soft); color: var(--accent-color); }
.latest-body strong { font-size: 15px; font-weight: 600; line-height: 1.45; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; transition: color .2s; }
.latest-body small { font-size: 12px; color: var(--text-secondary); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.latest-side { display: grid; justify-items: end; gap: 3px; font: 11px var(--font-mono); color: var(--text-secondary); white-space: nowrap; padding-top: 2px; }
.latest-comments { color: var(--accent-color); }

.forum-state { padding: 30px 20px; margin: 0; background: var(--paper-card); border: 1px solid var(--border-line); color: var(--text-secondary); }
.forum-state button { font: inherit; color: var(--accent-color); background: none; border: 0; cursor: pointer; text-decoration: underline; }
.forum-state a { color: var(--accent-color); }

@media (prefers-reduced-motion: no-preference) {
  .heading-scan { animation: forum-scan 6s cubic-bezier(.6,0,.4,1) infinite alternate; }
}
@keyframes forum-scan { from { left: 0; } to { left: calc(100% - 72px); } }
@media (max-width: 860px) {
  .forum-grid { grid-template-columns: minmax(0, 1fr); }
  .forum-grid.text-only .latest-list { grid-template-columns: minmax(0, 1fr); }
}
@media (max-width: 640px) {
  .heading-scan { display: none; }
  .visual-column { grid-auto-rows: 150px; }
  .visual-caption strong { font-size: 15px; }
  .latest-side .latest-comments { display: none; }
}
@media (prefers-reduced-motion: reduce) { .visual-card img, .visual-card::before, .latest-item::before { transition: none; } }
</style>
