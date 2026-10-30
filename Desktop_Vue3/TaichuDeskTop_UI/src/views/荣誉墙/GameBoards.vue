<template>
  <!-- 游戏区的榜：土豆榜、常胜将军榜、常败将军榜 -->
  <section class="game-boards" :aria-label="TITLES[board]">
    <div class="gb-head">
      <h3>{{ TITLES[board] }}</h3>
      <p>{{ board === 'potato'
        ? '按斗地主对局净赢的土豆排名，不算每日领取、破产补助和经验兑换，靠牌技说话。'
        : board === 'wins'
          ? '按所有对战游戏（斗地主、联机五子棋等）的胜利总场次排名，同场次按胜率。'
          : '按所有对战游戏的失败总场次排名，同场次按败率。屡败屡战，也是一种荣誉。' }}</p>
      <div class="gb-range" role="group" aria-label="时间范围">
        <button v-for="r in RANGES" :key="r.key" type="button" :class="{ on: range === r.key }" :aria-pressed="range === r.key" @click="range = r.key">{{ r.label }}</button>
      </div>
    </div>
    <p v-if="loading" class="gb-empty">读取中…</p>
    <p v-else-if="!rows.length" class="gb-empty">{{ board === 'potato' ? '还没有人上榜，去太虚游打一局斗地主吧。' : board === 'wins' ? '还没有人赢过对局。' : '还没有人输过对局。' }}</p>
    <ol v-else class="gb-list">
      <li v-for="row in rows" :key="row.userId" :class="{ top: row.rank <= 3 }">
        <span class="gb-rank">{{ row.rank <= 3 ? (board === 'losses' ? ['🫠', '😵', '🥲'] : ['👑', '🥈', '🥉'])[row.rank - 1] : String(row.rank).padStart(2, '0') }}</span>
        <AvatarFrame :user-id="row.userId" size="sm" class="gb-frame"><img v-if="row.avatar" :src="row.avatar" alt="" class="gb-avatar" loading="lazy" /><span v-else class="gb-avatar ph">{{ row.name.slice(0, 1) }}</span></AvatarFrame>
        <span class="gb-who"><UserLink :id="row.userId" :name="row.name" class="gb-name" /><UserTitle :user-id="row.userId" /></span>
        <span class="gb-value">{{ row.main }}</span>
        <span class="gb-sub">{{ row.sub }}</span>
      </li>
    </ol>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import UserLink from '@/components/UserLink.vue'
import AvatarFrame from '@/components/AvatarFrame.vue'
import UserTitle from '@/components/UserTitle.vue'
import { potatoApi } from '@/api/potato'
import { recordsApi } from '@/api/records'

const props = defineProps<{ board: 'potato' | 'wins' | 'losses' }>()
const TITLES = { potato: '🥔 土豆榜', wins: '🏅 常胜将军榜', losses: '🫠 常败将军榜' } as const
type Range = 'total' | 'week' | 'month'
const RANGES: { key: Range; label: string }[] = [{ key: 'total', label: '总榜' }, { key: 'week', label: '本周' }, { key: 'month', label: '本月' }]
const range = ref<Range>('total')
const loading = ref(false)
const rows = ref<{ rank: number; userId: string; name: string; avatar?: string | null; main: string; sub: string }[]>([])

async function load() {
  loading.value = true
  try {
    if (props.board === 'potato') {
      const list = await potatoApi.leaderboard(range.value)
      rows.value = list.map(r => ({ ...r, main: `+${r.value.toLocaleString()} 🥔`, sub: '对局净赢' }))
    } else if (props.board === 'losses') {
      const list = await recordsApi.lossesLeaderboard(range.value)
      rows.value = list.map(r => ({ ...r, main: `${r.losses} 败`, sub: `${r.total} 局 · 败率 ${r.lossRate}%` }))
    } else {
      const list = await recordsApi.winsLeaderboard(range.value)
      rows.value = list.map(r => ({ ...r, main: `${r.wins} 胜`, sub: `${r.total} 局 · 胜率 ${r.winRate}%` }))
    }
  } catch { rows.value = [] } finally { loading.value = false }
}
watch(() => [props.board, range.value], load)
onMounted(load)
</script>

<style scoped>
.game-boards { display: grid; gap: 16px; }
.gb-head { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: end; gap: 6px 16px; }
.gb-head h3 { margin: 0; font-size: 18px; }
.gb-head p { grid-column: 1; margin: 0; color: var(--text-secondary); font-size: 13px; line-height: 1.6; }
.gb-range { grid-column: 2; grid-row: 1 / 3; display: flex; border: 1px solid var(--border-line); }
.gb-range button { padding: 6px 12px; border: 0; border-right: 1px solid var(--border-line); background: transparent; color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.gb-range button:last-child { border-right: 0; }
.gb-range button.on { background: var(--accent-color); color: var(--on-accent); }
.gb-empty { padding: 24px 0; color: var(--text-secondary); text-align: center; }
.gb-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.gb-list li { display: grid; grid-template-columns: 44px 40px minmax(0, 1fr) auto auto; align-items: center; gap: 14px; padding: 12px 18px; background: var(--paper-card); border-bottom: 1px solid var(--border-line); }
.gb-list li.top { border-left: 3px solid var(--signal-mint); }
.gb-rank { font: 600 15px var(--font-mono, monospace); color: var(--text-secondary); text-align: center; }
.top .gb-rank { font-size: 20px; }
.gb-avatar { width: 36px; height: 36px; border-radius: 50%; object-fit: cover; }
.gb-avatar.ph { display: grid; place-items: center; background: var(--accent-soft, var(--bg-sub)); color: var(--accent-color); font-weight: 600; }
.gb-frame { justify-self: start; }
.gb-who { display: flex; align-items: center; min-width: 0; }
.gb-name { font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.gb-value { font-size: 17px; font-weight: 700; font-variant-numeric: tabular-nums; }
.gb-sub { min-width: 120px; color: var(--text-secondary); font-size: 12px; text-align: right; }
@media (max-width: 700px) {
  .gb-head { grid-template-columns: minmax(0, 1fr); }
  .gb-range { grid-column: 1; grid-row: auto; justify-self: start; }
  .gb-list li { grid-template-columns: 32px 32px minmax(0, 1fr) auto; padding: 10px 12px; gap: 10px; }
  .gb-sub { display: none; }
}
</style>
