<template>
  <section class="lb-card" aria-label="约战排行榜">
    <h3 class="lb-title">🏆 约战排行榜</h3>
    <ol v-if="items.length" class="lb-list">
      <li v-for="(item, i) in items" :key="item.id" @click="go(item.id)">
        <span class="lb-rank" :class="'r' + (i + 1)">{{ i === 0 ? '👑' : i === 1 ? '🥈' : i === 2 ? '🥉' : i + 1 }}</span>
        <span class="lb-name">{{ item.title }}</span>
        <span class="lb-score">{{ item.battlePoints }} 分 · {{ item.battleTier || '青铜' }}</span>
      </li>
    </ol>
    <p v-else class="lb-empty">暂无排名，去打一场约战吧。</p>
    <RouterLink to="/battles/leaderboard" class="lb-more">查看完整榜单 →</RouterLink>
  </section>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { stickmanApi, type StickmanBrief } from '../OCs/stickman'

const router = useRouter()
const items = ref<StickmanBrief[]>([])

async function load() {
  try {
    items.value = await stickmanApi.getLeaderboard(3)
  } catch { /* 静默 */ }
}
function go(id: string) { router.push(`/ocs/${id}`) }
onMounted(load)
</script>

<style scoped>
.lb-card { border: 1px solid var(--border-line); border-radius: 12px; background: var(--paper-card); padding: 16px; }
.lb-title { margin: 0 0 12px; font-size: .9rem; }
.lb-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.lb-list li { display: flex; align-items: center; gap: 10px; cursor: pointer; padding: 6px 4px; border-radius: 8px; }
.lb-list li:hover { background: var(--bg-sub); }
.lb-rank { width: 26px; text-align: center; font-size: 1rem; }
.lb-rank.r1 { font-size: 1.2rem; }
.lb-name { flex: 1; min-width: 0; font-size: .85rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.lb-score { font-size: .72rem; color: var(--text-secondary); white-space: nowrap; }
.lb-empty { color: var(--text-secondary); font-size: .82rem; padding: 8px 0; }
.lb-more { display: block; margin-top: 12px; text-align: center; color: var(--accent-color); text-decoration: none; font-size: .82rem; }
.lb-more:hover { text-decoration: underline; }
</style>
