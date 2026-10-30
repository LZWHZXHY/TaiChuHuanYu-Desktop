<!-- 个人主页 / 个人中心：社区活跃（活跃日历 + 关键数字 + 各类活动 + 几点活跃） -->
<template>
  <section v-if="data" class="ap" aria-label="社区活跃">
    <header class="ap-head">
      <h2>社区活跃</h2>
      <span class="ap-sub">加入太初寰宇的第 <b>{{ fmt(data.days) }}</b> 天</span>
    </header>
    <div class="ap-tiles">
      <div><b>{{ fmt(data.totals.activeDays) }}</b><span>活跃天数</span></div>
      <div><b>{{ fmt(data.totals.currentStreak) }}</b><span>连续活跃（最长 {{ fmt(data.totals.longestStreak) }}）</span></div>
      <div><b>{{ fmt(data.totals.last30) }}</b><span>最近 30 天做的事</span></div>
      <div><b>{{ fmt(data.totals.likes) }}</b><span>获赞</span></div>
      <div><b>{{ fmt(data.totals.followers) }}</b><span>粉丝</span></div>
      <div v-if="data.totals.visitDays !== null" class="ap-private" title="只有你自己看得到"><b>{{ fmt(data.totals.visitDays) }}</b><span>上站天数 · 仅自己可见</span></div>
    </div>
    <p v-if="data.kinds.length" class="ap-kinds">
      <template v-for="k in data.kinds" :key="k.key">
        <RouterLink v-if="k.key === 'history'" class="ap-kind-link" :to="{ path: '/history', query: { author: userId } }" title="看看记了些什么">{{ k.label }} <b>{{ fmt(k.count) }}</b></RouterLink>
        <span v-else>{{ k.label }} <b>{{ fmt(k.count) }}</b></span>
      </template>
    </p>
    <!-- 岁月史书：最近记的几段 -->
    <div v-if="data.history?.count" class="ap-history">
      <div class="ap-history-head">
        <h3>📖 {{ data.isSelf ? '我' : 'TA' }}在岁月史书里记的</h3>
        <RouterLink :to="{ path: '/history', query: { author: userId } }">全部 {{ fmt(data.history.count) }} 段 →</RouterLink>
      </div>
      <ul>
        <li v-for="h in data.history.recent" :key="h.id">
          <RouterLink :to="{ path: '/history', query: { entry: String(h.id) } }" class="ap-history-item">
            <img v-if="h.cover" :src="withThumbnail(h.cover, 120)" alt="" loading="lazy" />
            <span v-else class="ap-history-icon" aria-hidden="true">{{ KIND_ICON[h.kind] ?? '✨' }}</span>
            <span class="ap-history-text"><b>{{ h.title || '（截图）' }}</b><small>{{ historyDate(h) }}</small></span>
          </RouterLink>
        </li>
      </ul>
    </div>
    <ActivityHeatmap :days="data.heatmap" unit="件事" verb="活动" :kind-labels="kindLabels" />
    <details v-if="hasTimes" class="ap-more">
      <summary>{{ habit || '什么时候最活跃' }}</summary>
      <div class="ap-bars">
        <div><h3>一天中的时段（北京时间）</h3><StatBars title="一天中的时段" :values="data.hours" :labels="HOUR_LABELS" :short="i => `${i}点`" :tick-every="6" /></div>
        <div><h3>一周里的哪天</h3><StatBars title="一周里的哪天" :values="data.weekdays" :labels="WEEK_LABELS" :short="i => WEEK_SHORT[i] ?? ''" /></div>
      </div>
    </details>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import ActivityHeatmap from '@/components/ActivityHeatmap.vue'
import StatBars from '@/views/世界观管理/世界观组件/StatBars.vue'
import { userActivityApi, type UserActivity } from '@/api/userActivity'
import { withThumbnail } from '@/utils/imageThumb'

const KIND_ICON: Record<string, string> = { funny: '😂', chat: '💬', life: '🌱', progress: '🏆', event: '📌', other: '✨' }
type HistoryItem = NonNullable<UserActivity['history']>['recent'][number]
const historyDate = (h: HistoryItem) => {
  const [y, m, d] = h.happenedOn.split('-').map(Number)
  return h.precision === 'year' ? `${y} 年` : h.precision === 'month' ? `${y} 年 ${m} 月` : `${y} 年 ${m} 月 ${d} 日`
}

const props = defineProps<{ userId: string }>()
const data = ref<UserActivity | null>(null)
let version = 0
// 读取失败就不显示，不影响主页其他内容
watch(() => props.userId, async id => {
  const v = ++version
  data.value = null
  if (!id) return
  try { const res = await userActivityApi.get(id); if (v === version) data.value = res } catch { /* 忽略 */ }
}, { immediate: true })

const fmt = (n: number) => Number(n || 0).toLocaleString('zh-CN')
const kindLabels = computed(() => Object.fromEntries((data.value?.kinds ?? []).map(k => [k.key, k.label])))
const hasTimes = computed(() => (data.value?.hours.reduce((a, b) => a + b, 0) ?? 0) >= 5)
const HOUR_LABELS = Array.from({ length: 24 }, (_, h) => `${h}:00 – ${h}:59`)
const WEEK_LABELS = ['周一', '周二', '周三', '周四', '周五', '周六', '周日']
const WEEK_SHORT = ['一', '二', '三', '四', '五', '六', '日']
const habit = computed(() => {
  const s = data.value
  if (!s || !hasTimes.value) return ''
  const h = s.hours.indexOf(Math.max(...s.hours))
  const w = s.weekdays.indexOf(Math.max(...s.weekdays))
  const period = h < 5 ? '深夜' : h < 9 ? '清晨' : h < 12 ? '上午' : h < 14 ? '中午' : h < 18 ? '下午' : h < 22 ? '晚上' : '深夜'
  return `最常在${WEEK_LABELS[w]}的${period} ${h} 点出没`
})
</script>

<style scoped>
.ap { display: grid; gap: 14px; margin: 20px 0; padding: 16px 18px; background: var(--paper-card); border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); }
.ap > * { min-width: 0; }
.ap-head { display: flex; flex-wrap: wrap; align-items: baseline; gap: 6px 12px; }
.ap-head h2 { margin: 0; font-size: 16px; }
.ap-sub { font-size: 13px; color: var(--text-secondary); }
.ap-sub b { color: var(--text-primary); }
.ap-tiles { display: grid; grid-template-columns: repeat(auto-fit, minmax(120px, 1fr)); gap: 8px; }
.ap-tiles div { display: grid; gap: 2px; padding: 8px 10px; background: var(--bg-sub); min-width: 0; }
.ap-tiles b { font-size: 22px; line-height: 1.1; font-variant-numeric: tabular-nums; }
.ap-tiles span { font-size: 12px; color: var(--text-secondary); }
.ap-tiles .ap-private { background: none; border: 1px dashed var(--border-line); }
.ap-kinds { display: flex; flex-wrap: wrap; gap: 6px; margin: 0; }
.ap-kinds span { padding: 2px 10px; font-size: 12px; color: var(--text-secondary); border: 1px solid var(--border-line); }
.ap-kinds b { margin-left: 2px; color: var(--text-primary); font-variant-numeric: tabular-nums; }
.ap-kind-link { padding: 2px 10px; font-size: 12px; color: var(--accent-color); border: 1px solid var(--accent-color); text-decoration: none; }
.ap-kind-link b { margin-left: 2px; font-variant-numeric: tabular-nums; }
.ap-kind-link:hover { background: color-mix(in srgb, var(--accent-color) 10%, transparent); }
.ap-history-head { display: flex; flex-wrap: wrap; align-items: baseline; justify-content: space-between; gap: 6px; margin-bottom: 8px; }
.ap-history-head h3 { margin: 0; font-size: 13px; }
.ap-history-head a { font-size: 12px; color: var(--accent-color); text-decoration: none; }
.ap-history ul { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: 6px; }
.ap-history-item { display: flex; align-items: center; gap: 8px; min-width: 0; padding: 6px; background: var(--bg-sub); color: var(--text-primary); text-decoration: none; }
.ap-history-item:hover { outline: 1px solid var(--accent-color); }
.ap-history-item img, .ap-history-icon { flex-shrink: 0; width: 40px; height: 40px; object-fit: cover; display: flex; align-items: center; justify-content: center; background: var(--paper-card); font-size: 18px; }
.ap-history-text { min-width: 0; display: flex; flex-direction: column; }
.ap-history-text b { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 13px; font-weight: 500; }
.ap-history-text small { font-size: 11px; color: var(--text-secondary); }
.ap-more summary { cursor: pointer; font-size: 13px; color: var(--text-secondary); }
.ap-more summary:hover { color: var(--accent-color); }
.ap-bars { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 18px; margin-top: 12px; }
.ap-bars h3 { margin: 0 0 8px; font-size: 12px; font-weight: 500; color: var(--text-secondary); }
@media (max-width: 720px) { .ap-bars { grid-template-columns: 1fr; } }
</style>
