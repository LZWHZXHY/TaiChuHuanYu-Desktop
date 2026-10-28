<template>
  <main class="stream-page content-page">
    <header class="content-header">
      <div><p class="eyebrow">社区创作 · 太初视界</p><h1>博客与帖子</h1><p class="intro">读一篇长文，分享一个想法，与社区一起交流。</p></div>
      <nav class="content-actions" aria-label="创作入口"><RouterLink class="primary" :to="composeRoute('blog')">写博客</RouterLink><RouterLink :to="composeRoute('post')">发帖子</RouterLink></nav>
    </header>
    <div class="section-toolbar">
      <div class="content-filters" aria-label="内容类型">
        <button v-for="(label, key) in filters" :key="key" :aria-pressed="currentFilter === key" @click="changeFilter(key)">{{ label }}</button>
      </div>
      <label class="topic-filter">分类<select aria-label="讨论分类" :value="currentTag" @change="changeTag(($event.target as HTMLSelectElement).value)"><option value="">全部分类</option><option value="我的世界">我的世界</option><option v-if="currentTag && currentTag !== '我的世界'" :value="currentTag">{{ currentTag }}</option></select></label>
    </div>
    <p v-if="currentTag" class="topic-context">{{ currentTag }} · 博客与帖子<RouterLink v-if="currentTag === '我的世界'" to="/games?game=MinecraftServer">服务器与下载 →</RouterLink><button @click="changeTag('')">查看全部分类</button></p>
    <p v-if="loading" class="content-state" role="status">正在加载社区内容…</p>
    <div v-else-if="!posts.length && !error" class="content-state"><h2>这里还没有内容</h2><p>写一篇博客，或分享你的第一条帖子。</p></div>
    <div v-else class="stream-grid">
      <article v-for="item in posts" :key="item.id" class="stream-card">
        <RouterLink v-if="item.cardCover" class="card-cover" :to="detailRoute(item.id)" tabindex="-1" aria-hidden="true"><img :src="item.cardCover" alt="" loading="lazy" @error="($event.target as HTMLImageElement).hidden = true" /></RouterLink>
        <div class="card-content">
          <div class="card-meta"><span class="content-badge">{{ item.type === 'post' ? '帖子' : '博客' }}</span><span>{{ item.authorName || '太初隐者' }}</span></div>
          <h2><RouterLink :to="detailRoute(item.id)">{{ item.title || (item.type === 'post' ? '社区动态' : '无标题随笔') }}</RouterLink></h2>
          <p class="card-excerpt">{{ wikiText(item.content) || '打开内容继续阅读。' }}</p>
          <footer><time>{{ formatTime(item.publishedAt || item.createdAt) }}</time><span>{{ item.resonance || 0 }} 灵动</span><span>{{ item.commentsCount || 0 }} 评论</span></footer>
        </div>
      </article>
    </div>
    <div v-if="error" class="content-state" role="alert"><p>{{ error }}</p><button @click="fetchStream(posts.length > 0)">重新加载</button></div>
    <div ref="loadMoreTrigger" class="load-more" role="status">
      <span v-if="loadingMore">正在加载更多…</span>
      <button v-else-if="hasMore && posts.length && !error" @click="fetchStream(true)">加载更多</button>
      <span v-else-if="posts.length && !error && !hasMore">已显示全部内容</span>
    </div>
    <PublicNoteDetail v-if="activePostId" :key="activePostId" :id="activePostId" @close="closeArtwork" />
  </main>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import PublicNoteDetail from './PublicNoteDetail.vue';
import { notePublishApi, type PublishedNoteItem } from '../../api/NotePublish';
import { wikiText } from '@/utils/wikiContent';

const route = useRoute(), router = useRouter();
const filters = { all: '全部内容', blog: '博客', post: '帖子' };
type Filter = keyof typeof filters;
const currentFilter = computed<Filter>(() => route.query.type === 'blog' || route.query.type === 'post' ? route.query.type : 'all');
const currentTag = computed(() => typeof route.query.tag === 'string' ? route.query.tag.trim().slice(0, 50) : '');
const composeRoute = (type: 'blog' | 'post') => ({ path: `/write/${type}`, query: currentTag.value ? { tag: currentTag.value } : {} });
const changeTag = (tag: string) => router.push({ query: { ...route.query, tag: tag || undefined, post: undefined } });
const activePostId = computed(() => typeof route.query.post === 'string' ? route.query.post : null);
const posts = ref<(PublishedNoteItem & { content: string; cardCover: string })[]>([]);
const loading = ref(false), loadingMore = ref(false), hasMore = ref(true), error = ref('');
const loadMoreTrigger = ref<HTMLElement | null>(null);
let page = 1, generation = 0;
let observer: IntersectionObserver | null = null;
const pageSize = 20;

// 兼容旧博客的 JSON 摘要和帖子的配图元数据。
function formatPost(item: PublishedNoteItem & { extraData?: string }) {
  let content = item.excerpt || '', cardCover = '';
  try {
    if (item.type === 'post') cardCover = JSON.parse(item.extraData || '{}').cardCover || '';
    else if (content.trim().startsWith('{')) {
      const excerpt = JSON.parse(content);
      if ('text' in excerpt || 'coverUrl' in excerpt) {
        content = excerpt.text || '';
        cardCover = excerpt.coverUrl || '';
      }
    }
  } catch { /* 旧的纯文本摘要保持可读。 */ }
  return { ...item, content, cardCover };
}
async function fetchStream(more = false) {
  if (more && (loading.value || loadingMore.value || !hasMore.value)) return;
  const current = more ? generation : ++generation;
  if (more) {
    loadingMore.value = true;
  } else {
    page = 1;
    posts.value = [];
    hasMore.value = true;
    loading.value = true;
    loadingMore.value = false;
  }
  error.value = '';
  try {
    const result = await notePublishApi.getPublicStream({ type: currentFilter.value === 'all' ? undefined : currentFilter.value, tag: currentTag.value || undefined, page, pageSize });
    if (current !== generation) return;
    const existing = new Set(posts.value.map(item => item.id));
    posts.value.push(...result.filter(item => !existing.has(item.id)).map(formatPost));
    hasMore.value = result.length === pageSize;
    page++;
  } catch {
    if (current === generation) error.value = '内容加载失败，请重试；已加载的内容仍保留。';
  } finally {
    if (current === generation) {
      loading.value = false;
      loadingMore.value = false;
    }
  }
}
const detailRoute = (id: string) => ({ path: route.path, query: { ...route.query, post: id } });
const closeArtwork = () => router.replace({ query: { ...route.query, post: undefined } });
const changeFilter = (type: Filter) => router.push({ query: { ...route.query, type: type === 'all' ? undefined : type, post: undefined } });
const formatTime = (value?: string) => value ? new Date(value).toLocaleDateString('zh-CN') : '时间未知';
watch([currentFilter, currentTag], () => void fetchStream(), { immediate: true });
onMounted(() => {
  observer = new IntersectionObserver(entries => {
    if (entries[0]?.isIntersecting && posts.value.length && !error.value) void fetchStream(true);
  }, { rootMargin: '200px' });
  if (loadMoreTrigger.value) observer.observe(loadMoreTrigger.value);
});
onUnmounted(() => { generation++; observer?.disconnect(); });
</script>

<style scoped>
.topic-filter, .topic-context { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; color: var(--text-secondary); font-size: 14px; }
.topic-filter select, .topic-context button { padding: 8px 12px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); }
.topic-context { margin: 0 0 24px; }
.topic-context a { color: var(--accent-color); }
.stream-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 24px; align-items: stretch; }
.stream-card { display: flex; background: var(--paper-card); border: 1px solid var(--border-line); border-radius: var(--content-radius); overflow: hidden; transition: border-color .2s; }
.stream-card:hover, .stream-card:focus-within { border-color: var(--accent-color); }
.card-cover { display: block; width: 30%; flex-shrink: 0; background: var(--paper-sub); }
.card-cover img { width: 100%; height: 100%; object-fit: cover; }
.card-content { padding: 24px; min-width: 0; flex: 1; display: flex; flex-direction: column; }
.card-meta, footer { display: flex; align-items: center; flex-wrap: wrap; gap: 12px; color: var(--text-secondary); font-size: .82rem; }
h2 { font-size: 1.35rem; line-height: 1.5; margin: 16px 0 12px; overflow-wrap: anywhere; }
h2 a { color: var(--text-primary); text-decoration: none; }
h2 a:hover { color: var(--accent-color); }
.card-excerpt { margin-bottom: 24px; color: var(--text-secondary); line-height: 1.85; white-space: pre-line; overflow-wrap: anywhere; display: -webkit-box; -webkit-line-clamp: 4; -webkit-box-orient: vertical; overflow: hidden; }
footer { border-top: 1px solid var(--border-line); margin-top: auto; padding-top: 16px; }
footer a { margin-left: auto; color: var(--accent-color); text-decoration: none; }
.load-more { text-align: center; padding: 32px 0; color: var(--text-secondary); }
@media(max-width: 720px) { .stream-grid { grid-template-columns: 1fr; gap: 16px; } .card-content { padding: 20px; } .stream-card { display: block; } .card-cover { width: 100%; aspect-ratio: 21 / 9; } }
</style>
