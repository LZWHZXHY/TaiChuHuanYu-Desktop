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
          <p class="feed-line"><UserLink nested :id="item.actorId" :name="item.actorName"><strong>{{ item.actorName }}</strong></UserLink> {{ verb(item.kind) }}<strong v-if="item.targetTitle" class="feed-title">{{ item.kind.startsWith('world') ? `《${item.targetTitle}》` : item.targetTitle }}</strong><span v-if="item.detail" class="feed-detail">{{ item.kind === 'world-edit' ? '里' : item.kind.startsWith('quest') ? '，' : '' }}{{ item.detail }}</span></p>
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
import { useRouter } from 'vue-router'
import { communityApi, type CommunityFeedItem } from '@/api/community'
import { questRoute } from '@/utils/forumBoards'

const PAGE_SIZE = 6
const router = useRouter()
const items = ref<CommunityFeedItem[]>([])
const loading = ref(false)
const hasMore = ref(false)
const error = ref('')
let offset = 0

const badgeText = (kind: string) => kind.startsWith('quest') ? '世界任务' : kind.startsWith('world') ? '世界观' : kind.startsWith('wiki') ? '百科' : kind === 'post' ? '帖子' : kind === 'blog' ? '博客' : kind === 'artwork' ? '作品' : kind === 'user-joined' ? '新成员' : kind === 'signin' ? '签到' : kind.startsWith('comment') ? '评论' : kind === 'joint' ? '联合' : kind === 'survey' ? '问卷' : kind === 'dev-project' ? '开发进度' : '世界观'
const badgeKind = (kind: string) => kind.startsWith('quest') ? 'quest' : kind.startsWith('world') ? 'world' : kind.startsWith('wiki') ? 'wiki' : kind.startsWith('comment') ? 'comment' : kind === 'dev-project' ? 'dev' : kind
const verb = (kind: string) => kind === 'quest-start' ? '开启了世界任务' : kind === 'quest-complete' ? '宣布世界任务完成：' : kind === 'world-new' ? '创建了世界观' : kind === 'world-edit' ? '在世界观' : kind === 'wiki-new' ? '创建了百科词条' : kind === 'wiki-update' ? '更新了百科词条' : kind === 'artwork' ? '发布了作品' : kind === 'post' ? '发布了帖子' : kind === 'user-joined' ? '加入了社区' : kind === 'signin' ? '签到打卡了' : kind.startsWith('comment') ? '评论了' : kind === 'joint' ? '发起了联合活动' : kind === 'survey' ? '发起了问卷调查' : kind === 'dev-project' ? '更新了开发进度' : '发布了博客'

function open(item: CommunityFeedItem) {
  if (item.kind === 'blog' || item.kind === 'post' || item.kind === 'comment-blog' || item.kind === 'comment-post') {
    router.push({ name: 'CommunityStream', query: { post: item.targetId } })
  } else if (item.kind === 'artwork' || item.kind === 'comment-artwork') {
    router.push({ name: 'KnowledgeHome', query: { detailType: 'artwork', detail: item.targetId } })
  } else if (item.kind === 'user-joined' || item.kind === 'signin') {
    router.push({ name: 'UserCenter', params: { id: item.targetId } })
  } else if (item.kind === 'joint') {
    router.push('/joint/' + item.targetId)
  } else if (item.kind === 'survey') {
    router.push({ name: 'FillSurvey', params: { id: item.targetId } })
  } else if (item.kind === 'quest-start' || item.kind === 'quest-complete') {
    router.push(questRoute())
  } else if (item.kind === 'world-new' || item.kind === 'world-edit') {
    router.push('/world/project/' + item.targetId)
  } else if (item.kind === 'dev-project') {
    document.getElementById('dev-progress')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  } else {
    router.push({ name: 'WikiDetail', params: { id: item.targetId } })
  }
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

onMounted(() => load(true))
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
.badge-world { background: var(--accent-soft); color: var(--accent-color); }
.feed-detail { color: var(--text-secondary); }
.badge-dev { background: var(--accent-soft); color: var(--accent-color); }
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
