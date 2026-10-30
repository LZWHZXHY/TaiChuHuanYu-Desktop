<template>
  <main class="stream-page content-page community-surface tone-stream">
    <header class="content-header">
      <SectionTrace particles="forum" />
      <div>
        <p class="eyebrow">社区创作 · {{ FORUM_NAME }}</p>
        <h1>{{ board ? `${board.icon} ${board.tag}` : FORUM_NAME }}</h1>
        <p class="intro">{{ board ? board.desc : '读长文、看帖子、聊话题。选择版块，只看你关心的内容。' }}</p>
      </div>
      <nav class="content-actions" aria-label="创作入口"><RouterLink class="primary" :to="composeRoute('post')">发帖子</RouterLink><RouterLink :to="composeRoute('blog')">写博客</RouterLink></nav>
    </header>
    <!-- 版块页签：「全部」看所有内容，其余每个版块一页。 -->
    <nav class="community-nav board-nav" aria-label="论坛版块">
      <RouterLink :to="boardRoute('')" :class="{ selected: !currentTag }" :aria-current="!currentTag ? 'page' : undefined">全部</RouterLink>
      <RouterLink v-for="item in FORUM_BOARDS" :key="item.tag" :to="boardRoute(item.tag)" :class="{ selected: currentTag === item.tag }" :aria-current="currentTag === item.tag ? 'page' : undefined">{{ item.icon }} {{ item.tag }}</RouterLink>
    </nav>
    <div v-if="board" class="board-bar">
      <div class="content-filters sub-filters" aria-label="版块小类">
        <button :aria-pressed="!questView && !currentSub" @click="changeSub('')">全部</button>
        <button v-for="item in board.subs" :key="item.tag" :aria-pressed="!questView && currentSub === item.tag" :title="item.desc" @click="changeSub(item.tag)">{{ item.tag }}</button>
        <button v-if="board.quests" class="quest-tab" :aria-pressed="questView" title="全服一起完成目标，完成后解锁新 mod 和内容" @click="openQuests">⛏ 世界任务</button>
      </div>
      <RouterLink v-if="board.link" class="board-link" :to="board.link.to">{{ board.link.label }}</RouterLink>
    </div>
    <p v-else-if="currentTag" class="topic-context">标签「{{ currentTag }}」<button @click="changeTag('')">查看全部</button></p>
    <!-- 「我的世界」版块的世界任务页签 -->
    <ServerQuests v-if="questView" embedded />
    <template v-else>
    <!-- 版块首页顶部：世界任务进度（有任务时才显示） -->
    <ServerQuestPanel v-if="board?.quests && !currentSub" class="board-quest" />
    <div class="section-toolbar">
      <div class="content-filters" aria-label="内容类型">
        <button v-for="(label, key) in filters" :key="key" :aria-pressed="currentFilter === key" @click="changeFilter(key)">{{ label }}</button>
      </div>
      <span v-if="board && currentSubInfo" class="section-hint">{{ currentSubInfo.desc }}</span>
    </div>
    <section v-if="pinned.length" class="pinned-list" aria-label="置顶公告">
      <RouterLink v-for="item in pinned" :key="item.id" :to="detailRoute(item.id)" class="pinned-item">
        <span class="pin-badge">📌 {{ pinnedSub?.tag }}</span><strong>{{ item.title || '公告' }}</strong><time>{{ formatTime(item.publishedAt || item.createdAt) }}</time>
      </RouterLink>
    </section>
    <ParticleState v-if="loading" class="content-state" text="正在加载社区内容" />
    <div v-else-if="!visiblePosts.length && !error" class="content-state"><ParticleState kind="empty" :preset="board?.tag === '我的世界' ? 'minecraft' : 'forum'" compact /><h2>{{ board ? '这个版块还没有内容' : '这里还没有内容' }}</h2><p>{{ board ? '点右上角「发帖子」，成为第一个发言的人。' : '写一篇博客，或分享你的第一条帖子。' }}</p></div>
    <div v-else class="stream-grid">
      <article v-for="item in visiblePosts" :key="item.id" class="stream-card">
        <RouterLink v-if="item.cardCover" class="card-cover" data-depth :to="detailRoute(item.id)" tabindex="-1" aria-hidden="true"><img :src="item.cardCover" alt="" loading="lazy" @error="($event.target as HTMLImageElement).hidden = true" /></RouterLink>
        <div class="card-content">
          <div class="card-meta"><span class="content-badge">{{ item.type === 'post' ? '帖子' : '博客' }}</span><span v-if="boardLabel(item)" class="board-badge">{{ boardLabel(item) }}</span><RouterLink v-if="item.authorId" class="author-link" :to="{ name: 'UserCenter', params: { id: item.authorId } }">{{ item.authorName || '太初隐者' }}</RouterLink><span v-else>{{ item.authorName || '太初隐者' }}</span><UserTitle v-if="item.authorId" :user-id="item.authorId" /></div>
          <h2><RouterLink :to="detailRoute(item.id)">{{ item.title || (item.type === 'post' ? '社区动态' : '无标题随笔') }}</RouterLink></h2>
          <p class="card-excerpt">{{ wikiText(item.content) || '打开内容继续阅读。' }}</p>
          <footer><time>{{ formatTime(item.publishedAt || item.createdAt) }}</time><span>{{ item.resonance || 0 }} 灵动</span><span>{{ item.commentsCount || 0 }} 评论</span></footer>
        </div>
      </article>
    </div>
    </template>
    <div v-if="error && !questView" class="content-state" role="alert"><p>{{ error }}</p><button @click="fetchStream(posts.length > 0)">重新加载</button></div>
    <div v-show="!questView" ref="loadMoreTrigger" class="load-more" role="status">
      <span v-if="loadingMore">正在加载更多…</span>
      <button v-else-if="hasMore && posts.length && !error" @click="fetchStream(true)">加载更多</button>
      <span v-else-if="posts.length && !error && !hasMore">已显示全部内容</span>
    </div>
    <PublicNoteDetail v-if="activePostId" :key="activePostId" :id="activePostId" @close="closeArtwork" />
  </main>
</template>

<script setup lang="ts">
import ParticleState from '@/components/ParticleState.vue'
import SectionTrace from '@/components/SectionTrace.vue'
import UserTitle from '@/components/UserTitle.vue'
import { ref, computed, watch, onMounted, onUnmounted, defineAsyncComponent } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import PublicNoteDetail from './PublicNoteDetail.vue';
import ServerQuestPanel from '@/views/推送首页/components/ServerQuestPanel.vue';
const ServerQuests = defineAsyncComponent(() => import('@/views/世界任务/index.vue'));
import { notePublishApi, type PublishedNoteItem } from '../../api/NotePublish';
import { wikiText } from '@/utils/wikiContent';
import { FORUM_BOARDS, FORUM_NAME, findBoard, boardOf } from '@/utils/forumBoards';

const route = useRoute(), router = useRouter();
const filters = { all: '全部内容', blog: '博客', post: '帖子' };
type Filter = keyof typeof filters;
const currentFilter = computed<Filter>(() => route.query.type === 'blog' || route.query.type === 'post' ? route.query.type : 'all');
const currentTag = computed(() => typeof route.query.tag === 'string' ? route.query.tag.trim().slice(0, 50) : '');
const currentSub = computed(() => typeof route.query.sub === 'string' ? route.query.sub.trim().slice(0, 50) : '');
const board = computed(() => findBoard(currentTag.value));
const currentSubInfo = computed(() => board.value?.subs.find(s => s.tag === currentSub.value));
const pinnedSub = computed(() => board.value?.subs.find(s => s.adminOnly));
// 「我的世界」版块里的世界任务页签（?view=quests）
const questView = computed(() => !!board.value?.quests && route.query.view === 'quests');
const openQuests = () => router.push({ query: { ...route.query, view: 'quests', sub: undefined, post: undefined } });
const composeRoute = (type: 'blog' | 'post') => ({ path: `/write/${type}`, query: {
  ...(currentTag.value ? { tag: currentTag.value } : {}),
  ...(currentSub.value && !currentSubInfo.value?.adminOnly ? { sub: currentSub.value } : {}),
} });
const boardRoute = (tag: string) => ({ path: route.path, query: { ...route.query, tag: tag || undefined, sub: undefined, post: undefined, view: undefined, tab: undefined } });
const changeTag = (tag: string) => router.push(boardRoute(tag));
const changeSub = (sub: string) => router.push({ query: { ...route.query, sub: sub || undefined, post: undefined, view: undefined, tab: undefined } });
// 卡片标记：在「全部」里显示版块，在版块里只显示小类。
function boardLabel(item: { tags?: string | null }) {
  const { board: itemBoard, sub } = boardOf(item.tags);
  if (!itemBoard) return '';
  if (board.value) return sub?.tag || '';
  return sub ? `${itemBoard.tag} · ${sub.tag}` : itemBoard.tag;
}
const activePostId = computed(() => typeof route.query.post === 'string' ? route.query.post : null);
const posts = ref<(PublishedNoteItem & { content: string; cardCover: string })[]>([]);
// 版块首页（未选小类）在顶部置顶最近的公告，列表里不再重复显示。
const pinned = ref<PublishedNoteItem[]>([]);
const visiblePosts = computed(() => {
  const ids = new Set(pinned.value.map(item => item.id));
  return ids.size ? posts.value.filter(item => !ids.has(item.id)) : posts.value;
});
async function fetchPinned(current: number) {
  pinned.value = [];
  if (!board.value || currentSub.value || !pinnedSub.value) return;
  try {
    const result = await notePublishApi.getPublicStream({ tag: board.value.tag, sub: pinnedSub.value.tag, page: 1, pageSize: 3 });
    if (current === generation) pinned.value = Array.isArray(result) ? result : [];
  } catch { /* 置顶加载失败不影响列表 */ }
}
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
    if (!more) void fetchPinned(current);
    const result = await notePublishApi.getPublicStream({ type: currentFilter.value === 'all' ? undefined : currentFilter.value, tag: currentTag.value || undefined, sub: board.value ? currentSub.value || undefined : undefined, page, pageSize });
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
// 世界任务页签不加载帖子列表；切回帖子时再加载
watch([currentFilter, currentTag, currentSub, questView], () => { if (!questView.value) void fetchStream(); else { generation++; loading.value = false; loadingMore.value = false; } }, { immediate: true });
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
.topic-filter select, .topic-context button { padding: 8px 12px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); }
.topic-context { margin: 0 0 24px; }
.board-nav { margin-bottom: 16px; }
.board-bar { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 8px; }
.quest-tab { margin-left: 8px; }
.board-quest { margin: 4px 0 20px; }
.board-link { color: var(--accent-color); font-size: .88rem; text-decoration: none; }
.board-badge { padding: 2px 8px; font-size: .75rem; color: var(--accent-mint); border: 1px solid color-mix(in srgb, var(--accent-mint) 40%, transparent); }
.pinned-list { display: grid; gap: 1px; margin: 0 0 24px; border: 1px solid var(--border-line); border-left: 3px solid var(--caution, var(--warning)); background: var(--border-line); }
.pinned-item { display: flex; align-items: center; gap: 12px; padding: 12px 16px; background: var(--paper-card); color: var(--text-primary); text-decoration: none; }
.pinned-item:hover { background: var(--accent-soft); }
.pinned-item strong { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 600; }
.pinned-item time { font-size: .8rem; color: var(--text-secondary); }
.pin-badge { flex-shrink: 0; font-size: .75rem; color: var(--warning); }
.topic-context a { color: var(--accent-color); }
.stream-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 24px; align-items: stretch; }
.stream-card { display: flex; background: var(--paper-card); border: 0; border-bottom: 1px solid var(--border-line); border-radius: 0; overflow: hidden; transition: border-color .2s; }
.stream-card:hover, .stream-card:focus-within { border-color: var(--accent-color); }
.card-cover { display: block; width: 30%; flex-shrink: 0; background: var(--paper-sub); }
.card-cover img { width: 100%; height: 100%; object-fit: cover; }
.card-content { padding: 24px; min-width: 0; flex: 1; display: flex; flex-direction: column; }
.author-link { color: inherit; text-decoration: none; }
.author-link:hover { color: var(--accent-color); text-decoration: underline; }
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
