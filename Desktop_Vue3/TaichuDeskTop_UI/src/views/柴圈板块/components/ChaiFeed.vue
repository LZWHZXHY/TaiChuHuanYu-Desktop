<template>
  <section class="chai-feed" aria-label="柴圈动态">
    <h3 class="feed-title">🔥 柴圈动态</h3>
    <p v-if="loading" class="feed-state">正在加载…</p>
    <ul v-else-if="items.length" class="feed-list">
      <li v-for="item in items" :key="item.type + item.id" @click="go(item)">
        <span class="feed-icon">{{ icon(item.type) }}</span>
        <span class="feed-body">
          <strong>{{ item.title }}</strong>
          <small><UserLink nested :id="item.authorId" :name="item.authorName">@{{ item.authorName }}</UserLink> · {{ relTime(item.createdAt) }}</small>
        </span>
      </li>
    </ul>
    <p v-else class="feed-state">还没有动态，去创作第一个作品吧。</p>
    <button v-if="hasMore" class="load-more" @click="loadMore">加载更多</button>
  </section>
</template>

<script setup lang="ts">
import UserLink from '@/components/UserLink.vue'
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { chaiFeedApi, type ChaiFeedItem } from '@/api/chaiFeed'

const router = useRouter()
const items = ref<ChaiFeedItem[]>([])
const hasMore = ref(false)
const loading = ref(false)
const offset = ref(0)
const PAGE = 8

const icon = (type: string) => ({ work: '▶️', oc: '🎭', battle: '⚔️', joint: '🤝' } as Record<string, string>)[type] || '📌'

function go(item: ChaiFeedItem) {
  const path = { work: '/works', oc: '/ocs', battle: '/battles', joint: '/joint' }[item.type] || '/community'
  router.push(`${path}/${item.id}`)
}

function relTime(v: string) {
  const diff = Date.now() - new Date(v).getTime()
  const m = Math.floor(diff / 60000)
  if (m < 1) return '刚刚'
  if (m < 60) return `${m} 分钟前`
  const h = Math.floor(m / 60)
  if (h < 24) return `${h} 小时前`
  const d = Math.floor(h / 24)
  return `${d} 天前`
}

async function load() {
  loading.value = true
  try {
    const res = await chaiFeedApi.get({ offset: offset.value, limit: PAGE })
    items.value = items.value.concat(res.items)
    hasMore.value = res.hasMore
  } finally {
    loading.value = false
  }
}
function loadMore() {
  offset.value += PAGE
  load()
}
onMounted(load)
</script>

<style scoped>
.chai-feed { border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); padding: 16px; }
.feed-title { margin: 0 0 12px; font-size: .9rem; }
.feed-state { color: var(--text-secondary); font-size: .85rem; text-align: center; padding: 12px 0; }
.feed-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 2px; }
.feed-list li { display: flex; gap: 10px; align-items: center; padding: 8px 6px; border-radius: 0; cursor: pointer; }
.feed-list li:hover { background: var(--bg-sub); }
.feed-icon { font-size: 1rem; }
.feed-body { min-width: 0; }
.feed-body strong { display: block; font-size: .85rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.feed-body small { display: block; font-size: .72rem; color: var(--text-secondary); margin-top: 2px; }
.load-more { width: 100%; margin-top: 10px; padding: 8px; border: none; background: none; color: var(--accent-color); font-size: .82rem; cursor: pointer; }
.load-more:hover { text-decoration: underline; }
</style>
