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

      <!-- 牌桌人气：在斗地主里被别人点赞 / 砸番茄（每次都记下来） -->
      <section class="pop-box" aria-labelledby="pop-title">
        <div class="pop-head">
          <h3 id="pop-title">牌桌人气</h3>
          <small>在牌桌上被别人点赞、砸番茄的次数（点头像就能赞 / 砸，要花土豆）</small>
        </div>
        <div class="stat-grid">
          <div class="stat"><small>👍 累计被点赞</small><b>{{ reactions?.likes ?? 0 }}</b><em>近 30 天 {{ reactions?.likes30 ?? 0 }}</em></div>
          <div class="stat"><small>🍅 累计被砸番茄</small><b>{{ reactions?.tomatoes ?? 0 }}</b><em>近 30 天 {{ reactions?.tomatoes30 ?? 0 }}</em></div>
          <div class="stat"><small>我送出的赞</small><b>{{ reactions?.givenLikes ?? 0 }}</b></div>
          <div class="stat"><small>我砸出去的番茄</small><b>{{ reactions?.givenTomatoes ?? 0 }}</b></div>
        </div>
        <div class="rank-bar">
          <span>小排行</span>
          <div class="seg" role="group" aria-label="排行时间范围">
            <button type="button" :class="{ on: rankRange === 'total' }" :aria-pressed="rankRange === 'total'" @click="rankRange = 'total'">全部时间</button>
            <button type="button" :class="{ on: rankRange === '30d' }" :aria-pressed="rankRange === '30d'" @click="rankRange = '30d'">最近 30 天</button>
          </div>
        </div>
        <div class="rank-grid">
          <div v-for="board in RANK_BOARDS" :key="board.kind" class="rank" :class="'rank-' + board.kind">
            <h4><span aria-hidden="true">{{ board.icon }}</span>{{ board.title }}<small>{{ board.sub }}</small></h4>
            <ol v-if="ranks[board.kind].length">
              <li v-for="r in ranks[board.kind]" :key="r.userId" :class="{ me: r.userId === user.userId }">
                <span class="rk">{{ r.rank }}</span>
                <span class="rn">{{ r.name }}<em v-if="r.userId === user.userId">我</em></span>
                <b>{{ r.count }}</b>
              </li>
            </ol>
            <p v-else class="muted">{{ rankLoading ? '读取中…' : '还没有人上榜，去牌桌上互动一下吧。' }}</p>
          </div>
        </div>
      </section>

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
          <span class="m-game">{{ ledgerKind(l) }}</span>
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
import { GAME_NAMES, recordsApi, type GameStats, type MatchItem, type ReactionRankRow, type ReactionStats } from '@/api/records'
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

// ---------- 牌桌人气：被点赞 / 被砸番茄、两个小排行 ----------
type RankKind = 'like' | 'tomato'
const RANK_BOARDS: { kind: RankKind; icon: string; title: string; sub: string }[] = [
  { kind: 'like', icon: '👍', title: '最受欢迎', sub: '被赞最多' },
  { kind: 'tomato', icon: '🍅', title: '番茄收割机', sub: '被砸最多' },
]
const reactions = ref<ReactionStats | null>(null)
const rankRange = ref<'total' | '30d'>('total')
const ranks = ref<Record<RankKind, ReactionRankRow[]>>({ like: [], tomato: [] })
const rankLoading = ref(false)
async function loadRanks() {
  rankLoading.value = true
  try {
    const [like, tomato] = await Promise.all([
      recordsApi.reactionsLeaderboard('like', rankRange.value).catch(() => [] as ReactionRankRow[]),
      recordsApi.reactionsLeaderboard('tomato', rankRange.value).catch(() => [] as ReactionRankRow[]),
    ])
    ranks.value = { like, tomato }
  } finally { rankLoading.value = false }
}
watch(rankRange, loadRanks)

/** 土豆收支的分类：对局、台费、互动（点赞 / 砸番茄）、其他花销、领取 */
function ledgerKind(l: PotatoLedgerRow) {
  if (l.refType === 'game' || l.refType === 'game-bot') return '对局'
  if (l.refType === 'fee') return '台费'
  if (l.refType === 'ddz-react' || l.refType === 'board-react') return '互动'
  return l.delta < 0 ? '花费' : '领取'
}

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
  recordsApi.myReactions().then(r => { reactions.value = r }).catch(() => { reactions.value = null })
  void loadRanks()
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

/* 牌桌人气 */
.pop-box { display: grid; gap: 12px; padding: 14px 16px 16px; border: 1px solid var(--border-line); background: var(--paper-card); }
.pop-head { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 12px; }
.pop-head h3 { margin: 0; font-size: 16px; }
.pop-head small { color: var(--text-secondary); font-size: 12px; }
.pop-box .stat { background: var(--bg-sub); }
.stat em { color: var(--text-secondary); font-style: normal; font-size: 11px; }
.rank-bar { display: flex; align-items: center; justify-content: space-between; gap: 10px; font-size: 13px; color: var(--text-secondary); }
.seg { display: flex; border: 1px solid var(--border-line); }
.seg button { padding: 5px 12px; border: 0; border-right: 1px solid var(--border-line); background: transparent; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.seg button:last-child { border-right: 0; }
.seg button.on { background: var(--accent-color); color: var(--on-accent, #fff); }
.rank-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
.rank { padding: 10px 12px; border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); }
.rank.rank-tomato { border-top-color: #e5484d; }
.rank h4 { display: flex; align-items: baseline; gap: 6px; margin: 0 0 8px; font-size: 14px; }
.rank h4 small { color: var(--text-secondary); font-size: 11px; font-weight: 400; }
.rank ol { display: grid; gap: 4px; margin: 0; padding: 0; list-style: none; }
.rank li { display: grid; grid-template-columns: 22px minmax(0, 1fr) auto; align-items: center; gap: 8px; padding: 3px 0; font-size: 13px; }
.rank li.me { color: var(--accent-color); }
.rk { display: grid; place-items: center; height: 20px; background: var(--bg-sub); font: 600 11px var(--font-mono, monospace); }
.rank li:nth-child(-n+3) .rk { background: var(--accent-color); color: var(--on-accent, #fff); }
.rank.rank-tomato li:nth-child(-n+3) .rk { background: #e5484d; }
.rn { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.rn em { margin-left: 6px; padding: 0 4px; border: 1px solid currentColor; font-style: normal; font-size: 10px; }
.rank li b { font-variant-numeric: tabular-nums; }
@media (max-width: 760px) {
  .stat-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .pg-row { grid-template-columns: 60px 1fr auto; }
  .pg-row .bar, .pg-row > span:nth-child(2) { display: none; }
  .match { grid-template-columns: auto minmax(0, 1fr) auto; }
  .match .m-game, .match time { display: none; }
  .rank-grid { grid-template-columns: minmax(0, 1fr); }
}
</style>
