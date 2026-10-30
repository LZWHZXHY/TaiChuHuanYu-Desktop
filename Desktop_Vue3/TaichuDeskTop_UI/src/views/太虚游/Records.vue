<template>
  <div class="records">
    <header class="rec-head">
      <div>
        <h2>我的战绩</h2>
        <p>联机对局（斗地主、联机五子棋）的胜负由服务器判定；本地双人的围棋、象棋只记录，不算胜负。</p>
      </div>
      <PotatoWallet />
    </header>

    <p v-if="!user.isLoggedIn" class="muted">登录后才能查看战绩。</p>
    <template v-else>
      <!-- 概况 -->
      <div class="stat-grid">
        <div class="stat"><small>对局</small><b>{{ stats?.totalGames ?? 0 }}</b></div>
        <div class="stat"><small>胜场</small><b>{{ stats?.totalWins ?? 0 }}</b></div>
        <div class="stat"><small>胜率</small><b>{{ stats ? stats.winRate + '%' : '—' }}</b></div>
        <div class="stat"><small>当前</small><b>{{ streakText }}</b></div>
      </div>
      <div v-if="stats?.games.length" class="per-game">
        <div v-for="g in stats.games" :key="g.game" class="pg-row">
          <b>{{ GAME_NAMES[g.game] ?? g.game }}</b>
          <span>{{ g.total }} 局 · {{ g.wins }} 胜 {{ g.losses }} 负{{ g.draws ? ` ${g.draws} 平` : '' }}</span>
          <span class="bar" :title="`胜率 ${g.winRate}%`"><span :style="{ width: g.winRate + '%' }"></span></span>
          <span class="rate">{{ g.winRate }}%</span>
          <span v-if="g.potatoNet" class="potato" :class="g.potatoNet > 0 ? 'up' : 'down'">{{ g.potatoNet > 0 ? '+' : '' }}{{ g.potatoNet }} 🥔</span>
        </div>
      </div>

      <!-- 筛选 -->
      <div class="tabs" role="tablist" aria-label="战绩分类">
        <button v-for="t in TABS" :key="t.key" type="button" role="tab" :aria-selected="tab === t.key" :class="{ on: tab === t.key }" @click="tab = t.key">{{ t.label }}</button>
      </div>

      <!-- 测试问卷 -->
      <ul v-if="tab === 'question'" class="match-list">
        <li v-if="!sessions.length" class="muted">还没有做过测试问卷。</li>
        <li v-for="s in sessions" :key="s.id" class="match">
          <span class="m-game">测试问卷</span>
          <div class="m-main"><b>{{ s.gameIcon }} {{ s.gameTitle }}</b><span>结果：{{ s.resultIcon }} {{ s.resultTitle ?? '—' }} · 得分 {{ s.totalScore }}</span></div>
          <time>{{ fmt(s.createdAt) }}</time>
        </li>
      </ul>

      <!-- 土豆收支 -->
      <ul v-else-if="tab === 'potato'" class="match-list">
        <li v-if="!ledger.length" class="muted">还没有土豆收支记录。</li>
        <li v-for="l in ledger" :key="l.id" class="match">
          <span class="m-game">{{ l.refType === 'game' || l.refType === 'game-bot' ? '对局' : '领取' }}</span>
          <div class="m-main"><b>{{ l.reason }}</b><span>余额 {{ l.balance.toLocaleString() }}</span></div>
          <b class="potato" :class="l.delta >= 0 ? 'up' : 'down'">{{ l.delta >= 0 ? '+' : '' }}{{ l.delta }} 🥔</b>
          <time>{{ fmt(l.createdAt) }}</time>
        </li>
      </ul>

      <!-- 对局 -->
      <ul v-else class="match-list">
        <li v-if="loading" class="muted">读取中…</li>
        <li v-else-if="!matches.length" class="muted">这里还没有对局记录。</li>
        <li v-for="m in matches" :key="m.id" class="match" :class="'r-' + m.result">
          <span class="m-result">{{ RESULT[m.result] }}</span>
          <span class="m-game">{{ GAME_NAMES[m.game] ?? m.game }}</span>
          <div class="m-main">
            <b>{{ m.summary }}<em v-if="m.role">{{ m.role }}</em></b>
            <span>{{ m.mode }} · {{ m.players.filter(p => !p.self).map(p => p.isBot ? `${p.name}` : p.name).join('、') || '—' }}<template v-if="m.moves"> · {{ m.moves }} 手</template></span>
          </div>
          <b v-if="m.potatoChange" class="potato" :class="m.potatoChange > 0 ? 'up' : 'down'">{{ m.potatoChange > 0 ? '+' : '' }}{{ m.potatoChange }} 🥔</b>
          <time>{{ fmt(m.time) }}</time>
        </li>
      </ul>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useUserStore } from '@/stores/user'
import PotatoWallet from './PotatoWallet.vue'
import { GAME_NAMES, recordsApi, type GameStats, type MatchItem } from '@/api/records'
import { potatoApi, type PotatoLedgerRow } from '@/api/potato'
import { getMySessions } from './games_api'
import type { SessionSummary } from './game_types'

const TABS = [
  { key: 'all', label: '全部对局' }, { key: 'doudizhu', label: '斗地主' }, { key: 'gobang', label: '五子棋' },
  { key: 'go', label: '围棋' }, { key: 'xiangqi', label: '象棋' }, { key: 'question', label: '测试问卷' }, { key: 'potato', label: '土豆收支' },
]
const RESULT: Record<string, string> = { win: '胜', lose: '负', draw: '平', local: '本地' }

const user = useUserStore()
const tab = ref('all')
const stats = ref<GameStats | null>(null)
const matches = ref<MatchItem[]>([])
const sessions = ref<SessionSummary[]>([])
const ledger = ref<PotatoLedgerRow[]>([])
const loading = ref(false)

const streakText = computed(() => {
  const s = stats.value?.streak
  if (!s || s.kind === 'none' || !s.count) return '—'
  return s.kind === 'win' ? `${s.count} 连胜` : s.kind === 'lose' ? `${s.count} 连败` : `${s.count} 连平`
})
const fmt = (iso: string) => new Date(iso).toLocaleString('zh-CN', { month: 'numeric', day: 'numeric', hour: '2-digit', minute: '2-digit' })

async function loadTab() {
  if (!user.isLoggedIn) return
  loading.value = true
  try {
    if (tab.value === 'question') sessions.value = (await getMySessions({ page: 1, pageSize: 50 })).items
    else if (tab.value === 'potato') ledger.value = await potatoApi.ledger(100)
    else matches.value = await recordsApi.mine(tab.value === 'all' ? undefined : tab.value, 100)
  } catch { /* 后端未部署时为空 */ } finally { loading.value = false }
}
watch(tab, loadTab)
onMounted(async () => {
  if (!user.isLoggedIn) return
  try { stats.value = await recordsApi.myStats() } catch { stats.value = null }
  await loadTab()
})
</script>

<style scoped>
.records { display: grid; gap: 18px; color: var(--text-primary); }
.rec-head { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-end; gap: 12px; }
.rec-head h2 { margin: 0 0 6px; font-size: 22px; }
.rec-head p, .muted { margin: 0; color: var(--text-secondary); font-size: 13px; }
.stat-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 12px; }
.stat { display: grid; gap: 6px; padding: 14px 16px; border: 1px solid var(--border-line); background: var(--paper-card); }
.stat small { color: var(--text-secondary); font-size: 12px; }
.stat b { font-size: 24px; font-variant-numeric: tabular-nums; }
.per-game { display: grid; gap: 6px; padding: 12px 16px; border: 1px solid var(--border-line); background: var(--paper-card); }
.pg-row { display: grid; grid-template-columns: 70px minmax(140px, auto) minmax(80px, 1fr) 54px auto; align-items: center; gap: 12px; font-size: 13px; }
.pg-row > span:nth-child(2) { color: var(--text-secondary); }
.bar { height: 6px; background: var(--bg-sub); overflow: hidden; }
.bar span { display: block; height: 100%; background: var(--accent-color); }
.rate { text-align: right; font-variant-numeric: tabular-nums; }
.tabs { display: flex; flex-wrap: wrap; border-bottom: 1px solid var(--border-line); }
.tabs button { padding: 8px 14px; border: 0; border-bottom: 2px solid transparent; background: none; color: var(--text-secondary); font: inherit; font-size: 14px; cursor: pointer; }
.tabs button.on { border-bottom-color: var(--accent-color); color: var(--accent-color); }
.match-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.match { display: grid; grid-template-columns: auto 64px minmax(0, 1fr) auto auto; align-items: center; gap: 12px; padding: 10px 14px; border: 1px solid var(--border-line); border-left: 3px solid var(--border-line); background: var(--paper-card); font-size: 13px; }
.match.r-win { border-left-color: var(--success); }
.match.r-lose { border-left-color: var(--danger); }
.m-result { display: grid; place-items: center; width: 30px; height: 30px; font-weight: 700; background: var(--bg-sub); }
.r-win .m-result { background: color-mix(in srgb, var(--success) 18%, transparent); color: var(--success); }
.r-lose .m-result { background: color-mix(in srgb, var(--danger) 14%, transparent); color: var(--danger); }
.m-game { color: var(--text-secondary); font-size: 12px; }
.m-main { display: grid; gap: 3px; min-width: 0; }
.m-main b { font-size: 14px; }
.m-main b em { margin-left: 6px; padding: 0 5px; border: 1px solid var(--border-line); font-style: normal; font-weight: 400; font-size: 11px; color: var(--text-secondary); }
.m-main span { color: var(--text-secondary); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.match time { color: var(--text-secondary); font-size: 12px; white-space: nowrap; }
.potato { font-variant-numeric: tabular-nums; white-space: nowrap; }
.potato.up { color: var(--success); }
.potato.down { color: var(--danger); }
.match-list > .muted { padding: 16px; }
@media (max-width: 760px) {
  .stat-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .pg-row { grid-template-columns: 60px 1fr auto; }
  .pg-row .bar, .pg-row > span:nth-child(2) { display: none; }
  .match { grid-template-columns: auto minmax(0, 1fr) auto; }
  .match .m-game, .match time { display: none; }
}
</style>
