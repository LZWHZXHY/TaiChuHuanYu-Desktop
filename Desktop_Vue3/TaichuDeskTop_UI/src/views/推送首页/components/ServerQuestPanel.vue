<!-- 首页：我的世界服务器「世界任务」当前进度 + 最近一次解锁的内容 -->
<template>
  <section v-if="data && (data.active || last || data.canManage)" class="sq-panel" aria-label="世界任务">
    <header class="sq-head">
      <h2>⛏ 世界任务</h2>
      <RouterLink :to="questRoute()" class="sq-all">查看 →</RouterLink>
    </header>
    <RouterLink v-if="data.active" :to="questRoute()" class="sq-active">
      <strong>{{ data.active.line === 'side' ? '支线 · ' : '' }}{{ data.active.title }}</strong>
      <span v-if="data.active.kind === 'goal'" class="sq-goal">🎯 一次性目标 · 做到就完成，等你来挑战</span>
      <template v-else>
        <span class="sq-bar"><span :style="{ width: pct + '%' }"></span></span>
        <span class="sq-nums">{{ fmt(data.active.progress) }} / {{ fmt(data.active.target) }} {{ data.active.unit }}<em>{{ pct.toFixed(pct < 10 ? 1 : 0) }}%</em></span>
      </template>
      <span v-if="data.active.rewardTeaser" class="sq-teaser">完成后解锁：{{ data.active.rewardTeaser }}</span>
    </RouterLink>
    <RouterLink v-if="sideCount" :to="questRoute()" class="sq-sides">另有 {{ sideCount }} 个支线任务在进行 →</RouterLink>
    <RouterLink v-if="!data.active && !last" :to="questRoute('manage')" class="sq-empty">还没有世界任务（只有管理员能看到这条提示）：去创建第一个 →</RouterLink>
    <RouterLink v-if="last" :to="questRoute()" class="sq-last">
      <span class="sq-badge">已解锁</span>
      <span><b>{{ last.title }}</b> 完成了{{ unlock ? '：' + unlock : '' }}</span>
    </RouterLink>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { questApi, type QuestOverview } from '@/api/quests'
import { questRoute } from '@/utils/forumBoards'

const data = ref<QuestOverview | null>(null)
const fmt = (n: number) => Number(n || 0).toLocaleString('zh-CN')
const pct = computed(() => {
  const q = data.value?.active
  return q && q.target > 0 ? Math.min(100, (q.progress / q.target) * 100) : 0
})
// 主线之外同时在进行的支线
const sideCount = computed(() => Math.max(0, (data.value?.actives?.length ?? 0) - 1))
const last = computed(() => data.value?.completed[0] ?? null)
const unlock = computed(() => {
  const first = (last.value?.rewardText ?? '').split('\n').map(l => l.trim()).find(Boolean) ?? ''
  return first.length > 40 ? first.slice(0, 40) + '…' : first
})
onMounted(async () => { try { data.value = await questApi.overview() } catch { data.value = null } })
</script>

<style scoped>
.sq-panel { padding: 20px; border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); background: var(--paper-card); display: grid; gap: 12px; }
.sq-head { display: flex; align-items: baseline; gap: 10px; }
.sq-head h2 { margin: 0; font-size: 1.05rem; }
.sq-all { margin-left: auto; font-size: 13px; color: var(--accent-color); text-decoration: none; }
.sq-active { display: grid; gap: 6px; color: inherit; text-decoration: none; }
.sq-active strong { font-size: 15px; }
.sq-bar { height: 10px; background: var(--bg-sub); border: 1px solid var(--border-line); overflow: hidden; }
.sq-bar span { display: block; height: 100%; background: var(--accent-color); transition: width .6s ease; }
.sq-nums { display: flex; font-size: 12px; color: var(--text-secondary); }
.sq-nums em { margin-left: auto; font-style: normal; font-weight: 600; color: var(--accent-color); }
.sq-sides { font-size: 13px; color: var(--text-secondary); text-decoration: none; }
.sq-sides:hover { color: var(--accent-color); }
.sq-goal { font-size: 13px; color: var(--accent-color); }
.sq-teaser { font-size: 12px; color: var(--text-secondary); }
.sq-last { display: flex; gap: 8px; align-items: flex-start; padding-top: 10px; border-top: 1px dashed var(--border-line); font-size: 13px; color: inherit; text-decoration: none; line-height: 1.6; }
.sq-badge { flex-shrink: 0; padding: 0 6px; font-size: 11px; font-weight: 600; color: var(--on-accent); background: var(--accent-color); }
.sq-empty { font-size: 13px; color: var(--text-secondary); text-decoration: none; }
.sq-empty:hover { color: var(--accent-color); }
.sq-active:hover strong, .sq-last:hover b { color: var(--accent-color); }
</style>
