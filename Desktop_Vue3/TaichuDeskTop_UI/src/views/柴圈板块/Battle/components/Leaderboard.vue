<template>
  <div class="leaderboard-page content-page">
    <header class="content-header">
      <div>
        <p class="eyebrow">约战竞技</p>
        <h1>约战排行榜</h1>
        <p class="intro">按约战积分排名（胜 +3 · 平 +1），积分越高段位越高。</p>
      </div>
      <RouterLink to="/battles" class="btn-line">返回约战广场</RouterLink>
    </header>

    <p v-if="loading" class="state">正在读取排行榜…</p>
    <p v-else-if="error" class="state" role="alert">{{ error }} <button @click="load">重试</button></p>
    <div v-else-if="!items.length" class="state">还没有可排名的角色，去打一场约战吧。</div>

    <template v-else>
      <!-- 前三领奖台 -->
      <div v-if="podium.length" class="podium">
        <article v-for="slot in podium" :key="slot.item.id" class="podium-card" :class="'is-' + slot.cls" @click="go(slot.item.id)">
          <span class="medal" :class="'is-' + slot.cls">{{ slot.badge }}</span>
          <div class="cover">
            <img v-if="slot.item.coverUrl" :src="withThumbnail(slot.item.coverUrl, 400)" :alt="slot.item.title" loading="lazy" @error="($event.target as HTMLImageElement).hidden = true" />
            <span v-else class="cover-ph">🎭</span>
          </div>
          <h3 class="name">{{ slot.item.title }}</h3>
          <span class="author">@{{ slot.item.authorName }}</span>
          <span class="tier">{{ slot.item.battleTier || '青铜' }}</span>
          <span class="score">{{ slot.item.battlePoints }} 分</span>
          <span class="record">{{ slot.item.battleWins }}胜 {{ slot.item.battleLosses }}负 {{ slot.item.battleDraws }}平</span>
        </article>
      </div>

      <!-- 4 名以后 -->
      <ol class="rank-list">
        <li v-for="(item, index) in rest" :key="item.id" @click="go(item.id)">
          <span class="rank">{{ restStart + index }}</span>
          <img v-if="item.coverUrl" :src="withThumbnail(item.coverUrl, 400)" alt="" loading="lazy" @error="($event.target as HTMLImageElement).hidden = true" />
          <span class="rank-info">
            <strong>{{ item.title }}</strong>
            <small>@{{ item.authorName }} · {{ item.battleTier || '青铜' }}</small>
          </span>
          <span class="rank-score">{{ item.battlePoints }} 分 <small>{{ item.battleWins }}胜 {{ item.battleLosses }}负</small></span>
        </li>
      </ol>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { stickmanApi, type StickmanBrief } from '../../OCs/stickman'
import { withThumbnail } from '@/utils/imageThumb'

const router = useRouter()
const items = ref<StickmanBrief[]>([])
const loading = ref(false)
const error = ref('')

const medals = [
  { cls: 'gold', badge: '👑' },
  { cls: 'silver', badge: '🥈' },
  { cls: 'bronze', badge: '🥉' },
] as const

const podium = computed(() => {
  const top = items.value.slice(0, 3)
  if (top.length < 3) return []
  return ([1, 0, 2] as const).map(rank => ({ item: top[rank], ...medals[rank] }))
})
const rest = computed(() => (items.value.length >= 3 ? items.value.slice(3) : items.value))
const restStart = computed(() => (items.value.length >= 3 ? 4 : 1))

async function load() {
  loading.value = true
  error.value = ''
  try {
    items.value = await stickmanApi.getLeaderboard(20)
  } catch (e: any) {
    error.value = e.friendlyMessage || '排行榜加载失败，请重试'
  } finally {
    loading.value = false
  }
}

function go(id: string) { router.push(`/ocs/${id}`) }
onMounted(load)
</script>

<style scoped>
.eyebrow { color: var(--text-secondary); font-size: .78rem; letter-spacing: .15em; }
.content-header { display: flex; justify-content: space-between; align-items: center; }
.content-header h1 { margin: 8px 0; }
.intro { color: var(--text-secondary); }
.btn-line { padding: 8px 16px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); cursor: pointer; text-decoration: none; font-size: .85rem; }
.state { padding: 40px 0; text-align: center; color: var(--text-secondary); }
.podium { display: grid; grid-template-columns: 1fr 1.18fr 1fr; gap: 18px; align-items: end; margin: 24px 0 28px; }
.podium-card { position: relative; display: flex; flex-direction: column; align-items: center; gap: 8px; padding: 28px 14px 18px; border: 1px solid var(--border-line); border-radius: 14px; background: var(--paper-card); cursor: pointer; text-align: center; transition: transform .2s, box-shadow .2s; }
.podium-card:hover { transform: translateY(-4px); }
.podium-card.is-gold { padding-top: 38px; border-color: #d4af37; box-shadow: 0 12px 34px rgba(212,175,55,.20); }
.podium-card.is-silver { border-color: #aeb7c2; }
.podium-card.is-bronze { border-color: #b87333; }
.medal { position: absolute; top: -15px; left: 50%; transform: translateX(-50%); width: 34px; height: 34px; display: grid; place-items: center; border-radius: 50%; font-size: .9rem; font-weight: 700; color: #fff; background: var(--text-secondary); box-shadow: 0 3px 10px rgba(0,0,0,.18); }
.medal.is-gold { background: linear-gradient(135deg,#f9dc82,#d4af37); }
.medal.is-silver { background: linear-gradient(135deg,#eef1f5,#aeb7c2); color: #5a6470; }
.medal.is-bronze { background: linear-gradient(135deg,#e4b077,#b87333); }
.cover { width: 100%; aspect-ratio: 1/1; border-radius: 10px; overflow: hidden; background: var(--paper-sub); }
.cover img { width: 100%; height: 100%; object-fit: cover; }
.cover-ph { display: grid; place-items: center; height: 100%; font-size: 2rem; }
.name { margin: 0; font-size: 1rem; overflow-wrap: anywhere; }
.podium-card.is-gold .name { font-size: 1.15rem; }
.author { font-size: .75rem; color: var(--text-secondary); }
.tier { font-size: .78rem; color: #d4af37; font-weight: 600; }
.score { font-size: .95rem; font-weight: 700; color: var(--accent-color); }
.record { font-size: .72rem; color: var(--text-secondary); }
.rank-list { list-style: none; margin: 0; padding: 0; }
.rank-list li { display: flex; align-items: center; gap: 16px; padding: 14px 8px; border-bottom: 1px solid var(--border-line); cursor: pointer; }
.rank-list li:hover strong { color: var(--accent-color); }
.rank { width: 28px; text-align: center; font-size: 1.1rem; color: var(--accent-color); font-variant-numeric: tabular-nums; }
.rank-list img { width: 52px; height: 52px; object-fit: cover; border-radius: 8px; }
.rank-info { flex: 1; min-width: 0; }
.rank-info strong { display: block; overflow-wrap: anywhere; }
.rank-info small { display: block; margin-top: 3px; font-size: .75rem; color: var(--text-secondary); }
.rank-score { text-align: right; font-variant-numeric: tabular-nums; }
.rank-score small { display: block; font-size: .7rem; color: var(--text-secondary); }
@media (max-width: 560px) {
  .podium { gap: 8px; }
  .podium-card { padding: 22px 8px 12px; gap: 6px; }
  .podium-card.is-gold { padding-top: 30px; }
  .name, .podium-card.is-gold .name { font-size: .85rem; }
}
</style>
