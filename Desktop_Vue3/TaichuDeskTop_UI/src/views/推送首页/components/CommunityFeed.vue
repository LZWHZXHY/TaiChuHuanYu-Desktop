<template>
  <div class="community-feed">
    <header class="feed-header">
      <h2>社区动态</h2>
      <span>大家在创造什么</span>
    </header>

    <ParticleState v-if="loading && !items.length" class="feed-state" text="正在汇聚社区回响" compact />
    <p v-else-if="error && !items.length" class="feed-state feed-error" role="alert">{{ error }} <button @click="load(true)">重试</button></p>
    <ul v-else-if="items.length" class="feed-list">
      <li v-for="item in items" :key="item.id" class="feed-item" role="link" tabindex="0" @click="open(item)" @keydown.enter="open(item)">
        <span class="feed-badge" :class="'badge-' + badgeKind(item.kind)">{{ badgeText(item.kind) }}</span>
        <div class="feed-body">
          <p class="feed-line"><UserLink nested :id="item.actorId" :name="item.actorName"><strong>{{ item.actorName }}</strong></UserLink> {{ verb(item.kind) }}<strong v-if="item.targetTitle" class="feed-title">{{ meta(item.kind).book ? `《${item.targetTitle}》` : item.targetTitle }}</strong><span v-if="item.detail" class="feed-detail">{{ meta(item.kind).joiner ?? '' }}{{ item.detail }}</span></p>
          <time class="feed-time" :datetime="item.createdAt">{{ formatTime(item.createdAt) }}</time>
        </div>
      </li>
    </ul>
    <p v-else class="feed-state">社区还很安静，来发布第一条内容吧。</p>

    <p v-if="error && items.length" class="feed-error" role="alert">{{ error }}</p>
    <button v-if="hasMore" class="load-more" :disabled="loading" @click="load()">{{ loading ? '加载中…' : '加载更多' }}</button>
  </div>
</template>

<script setup lang="ts">
import ParticleState from '@/components/ParticleState.vue'
import UserLink from '@/components/UserLink.vue'
import { onMounted, ref } from 'vue'
import { useRouter, type RouteLocationRaw } from 'vue-router'
import { communityApi, type CommunityFeedItem } from '@/api/community'
import { questRoute } from '@/utils/forumBoards'

const PAGE_SIZE = 6
const router = useRouter()
const items = ref<CommunityFeedItem[]>([])
const loading = ref(false)
const hasMore = ref(false)
const error = ref('')
let offset = 0

/**
 * 每种动态怎么显示、点了去哪：badge = 左边的小标签，tone = 标签配色，verb = 名字后面那句话，
 * book = 标题加书名号，joiner = 补充说明前面接的字，go = 点击后去的页面。
 */
type Meta = { badge: string; tone: string; verb: string; book?: boolean; joiner?: string; go: (item: CommunityFeedItem) => RouteLocationRaw | (() => void) }
const stream = (item: CommunityFeedItem): RouteLocationRaw => ({ name: 'CommunityStream', query: { post: item.targetId } })
const artwork = (item: CommunityFeedItem): RouteLocationRaw => ({ name: 'KnowledgeHome', query: { detailType: 'artwork', detail: item.targetId } })
const historyEntry = (item: CommunityFeedItem): RouteLocationRaw => ({ name: 'CommunityHistory', query: { entry: item.targetId } })
const checkin = (item: CommunityFeedItem): RouteLocationRaw => `/activity/checkin/detail/${item.targetId}`
const world = (item: CommunityFeedItem): RouteLocationRaw => '/world/project/' + item.targetId
const META: Record<CommunityFeedItem['kind'], Meta> = {
  blog: { badge: '博客', tone: 'blog', verb: '发布了博客', go: stream },
  post: { badge: '帖子', tone: 'post', verb: '发布了帖子', go: stream },
  artwork: { badge: '作品', tone: 'artwork', verb: '发布了作品', go: artwork },
  'wiki-new': { badge: '百科', tone: 'wiki', verb: '创建了百科词条', go: item => ({ name: 'WikiDetail', params: { id: item.targetId } }) },
  'wiki-update': { badge: '百科', tone: 'wiki', verb: '更新了百科词条', go: item => ({ name: 'WikiDetail', params: { id: item.targetId } }) },
  'user-joined': { badge: '新成员', tone: 'user', verb: '加入了社区', go: item => ({ name: 'UserCenter', params: { id: item.targetId } }) },
  signin: { badge: '签到', tone: 'user', verb: '签到打卡了', go: item => ({ name: 'UserCenter', params: { id: item.targetId } }) },
  'comment-blog': { badge: '评论', tone: 'comment', verb: '评论了', go: stream },
  'comment-post': { badge: '评论', tone: 'comment', verb: '评论了', go: stream },
  'comment-artwork': { badge: '评论', tone: 'comment', verb: '评论了', go: artwork },
  'comment-work': { badge: '评论', tone: 'comment', verb: '评论了柴圈作品', go: item => '/works/' + item.targetId },
  'comment-character': { badge: '评论', tone: 'comment', verb: '评论了角色', go: item => '/ocs/' + item.targetId },
  'comment-history': { badge: '评论', tone: 'comment', verb: '评论了岁月史书里的', go: historyEntry },
  'comment-activity': { badge: '评论', tone: 'comment', verb: '在打卡活动的讨论区留言：', go: checkin },
  joint: { badge: '联合', tone: 'joint', verb: '发起了联合活动', go: item => '/joint/' + item.targetId },
  survey: { badge: '问卷', tone: 'survey', verb: '发起了问卷调查', go: item => ({ name: 'FillSurvey', params: { id: item.targetId } }) },
  'dev-project': { badge: '开发进度', tone: 'dev', verb: '更新了开发进度', go: () => () => document.getElementById('dev-progress')?.scrollIntoView({ behavior: 'smooth', block: 'start' }) },
  'world-new': { badge: '世界观', tone: 'world', verb: '创建了世界观', book: true, go: world },
  'world-edit': { badge: '世界观', tone: 'world', verb: '在世界观', book: true, joiner: '里', go: world },
  chronicle: { badge: '编年史', tone: 'world', verb: '在世界观', book: true, joiner: '的编年史里', go: item => ({ path: '/world/project/' + item.targetId, query: { view: 'timeline' } }) },
  'novel-new': { badge: '小说', tone: 'world', verb: '开始连载小说', book: true, go: item => '/novels/' + item.targetId },
  'novel-chapter': { badge: '小说', tone: 'world', verb: '更新了小说', book: true, joiner: '：', go: item => '/novels/' + item.targetId },
  'quest-start': { badge: '世界任务', tone: 'quest', verb: '开启了世界任务', joiner: '，', go: () => questRoute() },
  'quest-complete': { badge: '世界任务', tone: 'quest', verb: '宣布世界任务完成：', joiner: '，', go: () => questRoute() },
  'quest-report': { badge: '世界任务', tone: 'quest', verb: '为世界任务', joiner: ' ', go: () => questRoute() },
  history: { badge: '岁月史书', tone: 'history', verb: '在岁月史书里记了一笔：', go: historyEntry },
  'chai-work': { badge: '柴圈', tone: 'chai', verb: '发布了柴圈作品', go: item => '/works/' + item.targetId },
  oc: { badge: '柴圈', tone: 'chai', verb: '创建了角色', go: item => '/ocs/' + item.targetId },
  battle: { badge: '柴圈', tone: 'chai', verb: '发起了对战', go: item => '/battles/' + item.targetId },
  activity: { badge: '打卡', tone: 'joint', verb: '发起了打卡活动', go: checkin },
  checkin: { badge: '打卡', tone: 'joint', verb: '完成了打卡：', go: checkin },
  news: { badge: '公告', tone: 'official', verb: '发布了公告', go: item => ({ name: 'root', query: { news: item.targetId } }) },
  release: { badge: '版本', tone: 'official', verb: '发布了版本更新', go: item => ({ path: '/changelog', query: { release: item.targetId } }) },
  'trade-item': { badge: '交易行', tone: 'official', verb: '交易行上新：', go: () => '/trade' },
  'must-eat': { badge: '必吃榜', tone: 'official', verb: '在必吃榜收录了', go: () => '/must-eat' },
  sponsor: { badge: '赞助', tone: 'history', verb: '赞助了社区，成为', go: () => '/sponsors' },
  // 知识库资源：审核通过时进动态，点开直接看资源详情
  resource: { badge: '资源', tone: 'wiki', verb: '上传了资源', book: true, go: item => ({ name: 'KnowledgeHome', query: { type: 'resource', res: item.targetId } }) },
}
const FALLBACK: Meta = { badge: '动态', tone: 'user', verb: '更新了', go: () => '/' }
const meta = (kind: string) => META[kind as CommunityFeedItem['kind']] ?? FALLBACK
const badgeText = (kind: string) => meta(kind).badge
const badgeKind = (kind: string) => meta(kind).tone
const verb = (kind: string) => meta(kind).verb

function open(item: CommunityFeedItem) {
  const target = meta(item.kind).go(item)
  if (typeof target === 'function') target()
  else void router.push(target)
}

const formatTime = (value: string) => {
  if (!value) return ''
  const date = new Date(value)
  const now = Date.now()
  const diff = now - date.getTime()
  const min = Math.floor(diff / 60000)
  if (min < 1) return '刚刚'
  if (min < 60) return `${min} 分钟前`
  const hour = Math.floor(min / 60)
  if (hour < 24) return `${hour} 小时前`
  const day = Math.floor(hour / 24)
  if (day < 7) return `${day} 天前`
  return date.toLocaleDateString('zh-CN', { year: 'numeric', month: 'short', day: 'numeric' })
}

async function load(reset = false) {
  if (loading.value) return
  const nextOffset = reset ? 0 : offset
  loading.value = true
  error.value = ''
  try {
    const page = await communityApi.getFeed(nextOffset, PAGE_SIZE)
    if (reset) {
      items.value = page.items
      saveCache(page.items, page.hasMore)
    } else {
      const seen = new Set(items.value.map(i => i.id))
      items.value = items.value.concat(page.items.filter(i => !seen.has(i.id)))
    }
    offset = nextOffset + page.items.length
    hasMore.value = page.hasMore
  } catch (e: any) {
    error.value = e.friendlyMessage || '动态加载失败，请重试'
  } finally {
    loading.value = false
  }
}

// 上次看到的第一页先存在浏览器里：下次打开首页先把它显示出来（不用干等），新的拿到后再换上
const CACHE_KEY = 'tc-community-feed-v1'
function saveCache(list: CommunityFeedItem[], more: boolean) {
  try { localStorage.setItem(CACHE_KEY, JSON.stringify({ at: Date.now(), items: list, hasMore: more })) } catch { /* 存不了就算了 */ }
}
function readCache() {
  try {
    const raw = localStorage.getItem(CACHE_KEY)
    if (!raw) return
    const data = JSON.parse(raw) as { at?: number; items?: CommunityFeedItem[]; hasMore?: boolean }
    // 太旧的（超过 3 天）不显示，免得一打开看到一堆过期的
    if (!Array.isArray(data.items) || !data.items.length || Date.now() - (data.at ?? 0) > 3 * 86400_000) return
    items.value = data.items
    hasMore.value = Boolean(data.hasMore)
    offset = data.items.length
  } catch { /* 坏了就当没有 */ }
}

onMounted(() => { readCache(); void load(true) })
</script>

<style scoped>
.community-feed { color: var(--text-primary); }
.feed-header { display: flex; align-items: baseline; justify-content: space-between; gap: 12px; margin-bottom: 12px; }
.feed-header h2 { margin: 0; font-size: 1.05rem; }
.feed-header span { color: var(--text-secondary); font-size: 12px; }
.feed-list { list-style: none; margin: 0; padding: 0; }
.feed-item { display: flex; align-items: flex-start; gap: 14px; padding: 18px 0; cursor: pointer; border-bottom: 1px solid var(--border-line); }
.feed-item:last-child { border-bottom: 0; }
.feed-item:hover .feed-title { color: var(--accent-color); }
.feed-badge { flex: 0 0 auto; min-width: 38px; padding: 2px 6px; border-radius: 0; font-size: 11px; font-weight: 600; text-align: center; background: var(--bg-sub); color: var(--text-secondary); }
.badge-post { background: var(--accent-soft); color: var(--accent-color); }
.badge-blog { background: var(--accent-soft); color: var(--accent-color); }
.badge-artwork { background: var(--bg-sub); color: var(--text-secondary); }
.badge-wiki { background: var(--bg-sub); color: var(--text-secondary); }
.badge-user { background: var(--bg-sub); color: var(--text-secondary); }
.badge-comment { background: var(--bg-sub); color: var(--text-secondary); }
.badge-joint { background: var(--bg-sub); color: var(--text-secondary); }
.badge-survey { background: var(--bg-sub); color: var(--text-secondary); }
.badge-quest { background: var(--accent-color); color: var(--on-accent); }
.badge-history { background: color-mix(in srgb, #f59e0b 16%, transparent); color: var(--text-primary); }
.badge-world { background: var(--accent-soft); color: var(--accent-color); }
.feed-detail { color: var(--text-secondary); }
.badge-dev { background: var(--accent-soft); color: var(--accent-color); }
.badge-chai { background: color-mix(in srgb, #ea580c 14%, transparent); color: var(--text-primary); }
.badge-official { background: var(--inverse-bg, #111); color: var(--inverse-text, #fff); }
.feed-body { flex: 1; min-width: 0; }
.feed-line { margin: 0; font-size: 14px; line-height: 1.6; overflow-wrap: anywhere; }
.feed-line strong { font-weight: 600; }
.feed-title { transition: color .2s; }
.feed-time { display: block; margin-top: 3px; font-size: 12px; color: var(--text-secondary); }
.feed-state { padding: 16px 0; margin: 0; text-align: center; color: var(--text-secondary); font-size: .82rem; }
.feed-error { color: var(--accent-color); margin: 8px 0 0; font-size: .8rem; }
.feed-state button { font: inherit; color: var(--accent-color); background: none; border: 0; cursor: pointer; text-decoration: underline; }
.load-more { display: block; width: 100%; margin-top: 12px; padding: 8px; color: var(--text-secondary); background: var(--bg-sub); border: 1px solid var(--border-line); border-radius: 0; font-size: .78rem; cursor: pointer; }
.load-more:hover:not(:disabled) { color: var(--accent-color); border-color: var(--accent-color); }
.load-more:disabled { opacity: .5; cursor: default; }
</style>
