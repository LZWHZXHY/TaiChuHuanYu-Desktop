<!-- 「我的世界」列表上方：我的创作档案（创作天数、连续创作、活跃日历……），可以收起。
     一进页面就显示：先用上次的结果（存在浏览器里），没有就先显示占位，后台读到新数据再换上，不用等。 -->
<template>
  <section v-if="stats || !failed" class="mcp" :class="{ pending: !stats }" aria-label="我的创作档案" :aria-busy="loading">
    <header class="mcp-head">
      <h2>我的创作档案</h2>
      <span v-if="stats && stats.days > 0" class="mcp-sub">第一次创作是 {{ day(stats.firstAt) }}，今天是第 <b>{{ fmt(stats.days) }}</b> 天</span>
      <span v-else-if="stats" class="mcp-sub">写下第一个词条后，这里会记下你的创作天数、连续创作和活跃日历</span>
      <span v-else class="mcp-sub">{{ failed ? '暂时读不到创作数据' : '正在统计你的创作…' }}</span>
      <span v-if="stats && loading" class="mcp-refresh" aria-hidden="true">更新中…</span>
      <button type="button" class="mcp-toggle" :aria-expanded="!collapsed" @click="toggle">{{ collapsed ? '展开日历' : '收起日历' }}</button>
    </header>
    <div class="mcp-tiles">
      <div><b>{{ num(stats?.totals.activeDays) }}</b><span>创作天数</span></div>
      <div><b>{{ num(stats?.totals.currentStreak) }}</b><span>连续创作{{ stats ? `（最长 ${fmt(stats.totals.longestStreak)}）` : '' }}</span></div>
      <div><b>{{ num(stats?.totals.cards) }}</b><span>词条{{ stats ? `（${stats.totals.worlds} 个世界${stats.totals.joined ? ` · 参与 ${stats.totals.joined} 个` : ''}）` : '' }}</span></div>
      <div><b>{{ stats ? fmtWords(stats.totals.words) : '—' }}</b><span>设定字数</span></div>
      <div><b>{{ num(stats?.totals.edits) }}</b><span>修改次数</span></div>
      <div><b>{{ num(stats?.totals.last7) }}</b><span>最近 7 天</span></div>
    </div>
    <template v-if="!collapsed">
      <CreationHeatmap v-if="stats" class="mcp-heat" :days="stats.heatmap" />
      <div v-else class="mcp-heat-skeleton" aria-hidden="true"></div>
    </template>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { worldStatsApi, type MyWorldStats } from '@/api/worldStats'
import { useUserStore } from '@/stores/user'
import CreationHeatmap from '@/components/ActivityHeatmap.vue'

const KEY = 'taichu.world.myStatsCollapsed'
const userStore = useUserStore()
// 上次读到的结果按账号存在浏览器里，下次进来先显示它
const CACHE_KEY = `taichu.world.myStats:${userStore.userId ?? ''}`
const stats = ref<MyWorldStats | null>(null)
const loading = ref(false)
const failed = ref(false)
try { const raw = localStorage.getItem(CACHE_KEY); if (raw) stats.value = JSON.parse(raw) as MyWorldStats } catch { /* 忽略 */ }
const collapsed = ref(false)
try { collapsed.value = localStorage.getItem(KEY) === '1' } catch { /* 忽略 */ }
function toggle() {
  collapsed.value = !collapsed.value
  try { localStorage.setItem(KEY, collapsed.value ? '1' : '0') } catch { /* 忽略 */ }
}
const fmt = (n: number) => Number(n || 0).toLocaleString('zh-CN')
const num = (n: number | undefined) => n === undefined ? '—' : fmt(n)
const fmtWords = (n: number) => n >= 10000 ? `${(n / 10000).toFixed(n >= 100000 ? 0 : 1)} 万` : fmt(n)
const day = (v: string | null) => v ? new Date(v).toLocaleDateString('zh-CN', { year: 'numeric', month: 'long', day: 'numeric' }) : ''
onMounted(async () => {
  loading.value = true
  try {
    const fresh = await worldStatsApi.me()
    stats.value = fresh
    failed.value = false
    try { localStorage.setItem(CACHE_KEY, JSON.stringify(fresh)) } catch { /* 存不下就算了 */ }
  } catch { failed.value = !stats.value } finally { loading.value = false }
})
</script>

<style scoped>
.mcp { display: grid; gap: 14px; margin: 0 0 20px; padding: 16px 18px; background: var(--paper-card); border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); }
.mcp > * { min-width: 0; }
.mcp-head { display: flex; flex-wrap: wrap; align-items: baseline; gap: 6px 12px; }
.mcp-head h2 { margin: 0; font-size: 16px; }
.mcp-sub { font-size: 13px; color: var(--text-secondary); }
.mcp-sub b { color: var(--text-primary); }
.mcp-refresh { font-size: 11px; color: var(--text-secondary); opacity: .7; }
.mcp-toggle { margin-left: auto; padding: 3px 12px; border: 1px solid var(--border-line); background: none; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.mcp-tiles { display: grid; grid-template-columns: repeat(auto-fit, minmax(120px, 1fr)); gap: 8px; }
.mcp-tiles div { display: grid; gap: 2px; padding: 8px 10px; background: var(--bg-sub); min-width: 0; }
.mcp-tiles b { font-size: 22px; line-height: 1.1; font-variant-numeric: tabular-nums; }
.mcp-tiles span { font-size: 12px; color: var(--text-secondary); }
/* 还没读到数据：数字位置轻轻闪，日历先占好位置，读到后不会把下面的世界列表往下挤 */
.mcp.pending .mcp-tiles b { color: var(--text-secondary); animation: mcp-pulse 1.2s ease-in-out infinite; }
.mcp-heat-skeleton { height: 128px; background: repeating-linear-gradient(90deg, var(--bg-sub) 0 11px, transparent 11px 14px); opacity: .6; animation: mcp-pulse 1.2s ease-in-out infinite; }
@keyframes mcp-pulse { 50% { opacity: .35; } }
@media (prefers-reduced-motion: reduce) { .mcp.pending .mcp-tiles b, .mcp-heat-skeleton { animation: none; } }
</style>
