<!-- 首页「那年今日」：往年的今天（北京时间），社区里发生过的事（来自岁月史书）。今天没有就显示往年的这个月；都没有就不显示这一块。 -->
<template>
  <section v-if="items.length" class="otd" aria-label="那年今日">
    <header class="otd-head">
      <h2>🕰 {{ mode === 'day' ? '那年今日' : '那年这个月' }}</h2>
      <span>{{ mode === 'day' ? `往年的 ${todayText}，社区里发生了这些事` : `往年的 ${monthText}，社区里发生了这些事` }}</span>
    </header>
    <ul class="otd-list">
      <li v-for="e in items" :key="e.id">
        <RouterLink :to="{ name: 'CommunityHistory', query: { entry: String(e.id) } }" class="otd-item">
          <span class="otd-ago">{{ agoText(e) }}</span>
          <img v-if="e.images.length" :src="withThumbnail(e.images[0], 240)" class="otd-thumb" alt="" loading="lazy" />
          <span class="otd-body">
            <b class="otd-title">{{ KIND_META[e.kind].icon }} {{ titleOf(e) }}</b>
            <small class="otd-meta">{{ e.author.username }} 记 · {{ dateText(e) }}<template v-if="e.commentCount"> · 💬 {{ e.commentCount }}</template></small>
          </span>
        </RouterLink>
      </li>
    </ul>
    <RouterLink to="/history" class="otd-all">翻开岁月史书 →</RouterLink>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { historyApi, KIND_META, type HistoryEntry } from '@/api/history'
import { withThumbnail } from '@/utils/imageThumb'

const items = ref<HistoryEntry[]>([])
const mode = ref<'day' | 'month'>('day')
const today = ref('')

onMounted(async () => {
  try {
    const res = await historyApi.onThisDay()
    items.value = res.items
    mode.value = res.mode
    today.value = res.today
  } catch { /* 读不出来就不显示 */ }
})

const todayText = computed(() => { const [, m, d] = today.value.split('-'); return m ? `${Number(m)} 月 ${Number(d)} 日` : '今天' })
const monthText = computed(() => { const m = today.value.split('-')[1]; return m ? `${Number(m)} 月` : '这个月' })
const thisYear = computed(() => Number(today.value.slice(0, 4)) || new Date().getFullYear())
function agoText(e: HistoryEntry) {
  const n = thisYear.value - Number(e.happenedOn.slice(0, 4))
  return n <= 0 ? '今年' : `${n} 年前`
}
function dateText(e: HistoryEntry) {
  const [y, m, d] = e.happenedOn.split('-').map(Number)
  return e.precision === 'day' ? `${y}.${m}.${d}` : e.precision === 'month' ? `${y}.${m}` : `${y}`
}
function titleOf(e: HistoryEntry) {
  const t = e.title || (e.content ?? '').replace(/\s+/g, ' ').trim()
  return t ? (t.length > 40 ? t.slice(0, 40) + '…' : t) : `${e.images.length} 张截图`
}
</script>

<style scoped>
.otd { padding: 18px 20px; border: 1px solid var(--border-line); border-top: 2px solid #d97706; background: linear-gradient(160deg, color-mix(in srgb, #f59e0b 9%, var(--paper-card)), var(--paper-card) 55%); }
.otd-head { display: flex; align-items: baseline; justify-content: space-between; gap: 12px; flex-wrap: wrap; margin-bottom: 10px; }
.otd-head h2 { margin: 0; font-size: 1.05rem; }
.otd-head span { color: var(--text-secondary); font-size: 12px; }
.otd-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 2px; }
.otd-item { display: flex; align-items: center; gap: 12px; padding: 10px 6px; color: var(--text-primary); text-decoration: none; border-bottom: 1px dashed var(--border-line); }
.otd-list li:last-child .otd-item { border-bottom: 0; }
.otd-item:hover .otd-title { color: var(--accent-color); }
.otd-ago { flex-shrink: 0; min-width: 52px; padding: 2px 6px; background: color-mix(in srgb, #d97706 16%, transparent); color: #b45309; font-size: 12px; font-weight: 700; text-align: center; }
.otd-thumb { width: 44px; height: 44px; object-fit: cover; flex-shrink: 0; }
.otd-body { min-width: 0; display: grid; gap: 2px; }
.otd-title { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 14px; font-weight: 600; transition: color .15s; }
.otd-meta { color: var(--text-secondary); font-size: 12px; }
.otd-all { display: inline-block; margin-top: 10px; color: var(--accent-color); font-size: 13px; text-decoration: none; }
.otd-all:hover { text-decoration: underline; }
</style>
