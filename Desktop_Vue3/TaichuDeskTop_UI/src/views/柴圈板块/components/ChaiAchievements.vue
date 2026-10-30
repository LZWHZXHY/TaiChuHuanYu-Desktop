<template>
  <section class="achievements-card" aria-label="成就与新手引导">
    <h3 class="card-title">🏅 我的成就 <span v-if="loggedIn" class="unlocked-count">{{ unlockedCount }}/{{ achievements.length }}</span></h3>

    <!-- 成就徽章 -->
    <div v-if="loggedIn && achievements.length" class="badges">
      <div
        v-for="a in achievements"
        :key="a.key"
        class="badge"
        :class="{ locked: !a.unlocked }"
        :title="`${a.label}（${a.progress}/${a.target}）`"
      >
        <span class="badge-icon">{{ a.icon }}</span>
        <span class="badge-label">{{ a.label }}</span>
      </div>
    </div>
    <p v-else-if="loggedIn" class="empty">加载成就中…</p>
    <p v-else class="empty">登录后可查看你的成就。</p>

    <!-- 新手引导 -->
    <h3 class="card-title guide-title">🧭 新手引导</h3>
    <ol class="guide-list">
      <li v-for="step in guideSteps" :key="step.to">
        <RouterLink :to="step.to">{{ step.icon }} {{ step.label }}</RouterLink>
      </li>
    </ol>
  </section>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { chaiAchievementApi, type ChaiAchievement } from '@/api/chaiAchievement'
import { useUserStore } from '@/stores/user'

const userStore = useUserStore()
const achievements = ref<ChaiAchievement[]>([])
const unlockedCount = ref(0)
const loggedIn = computed(() => Boolean(userStore.userInfo))

const guideSteps = [
  { icon: '🎭', label: '创建你的第一个 OC 角色', to: '/ocs/create' },
  { icon: '🎬', label: '上传火柴人动画作品', to: '/works/create' },
  { icon: '⚔️', label: '发起或参加一场约战', to: '/battles' },
  { icon: '🤝', label: '发起联合 · 关联协作项目', to: '/joint/create' },
]

async function load() {
  if (!loggedIn.value) return
  try {
    const res = await chaiAchievementApi.my()
    achievements.value = res.achievements
    unlockedCount.value = res.unlockedCount
  } catch { /* 静默 */ }
}
onMounted(load)
</script>

<style scoped>
.achievements-card { border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); padding: 16px; }
.card-title { margin: 0 0 12px; font-size: .9rem; display: flex; align-items: center; justify-content: space-between; }
.unlocked-count { font-size: .75rem; color: var(--text-secondary); font-weight: 400; }
.badges { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 8px; }
.badge { display: flex; flex-direction: column; align-items: center; gap: 4px; padding: 8px 4px; border-radius: 0; background: var(--bg-sub); text-align: center; }
.badge.locked { opacity: .4; filter: grayscale(1); }
.badge-icon { font-size: 1.2rem; }
.badge-label { font-size: .68rem; color: var(--text-secondary); }
.empty { color: var(--text-secondary); font-size: .82rem; padding: 8px 0; }
.guide-title { margin-top: 20px; }
.guide-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 6px; }
.guide-list a { color: var(--text-primary); text-decoration: none; font-size: .82rem; display: block; padding: 6px 8px; border-radius: 0; }
.guide-list a:hover { background: var(--bg-sub); color: var(--accent-color); }
</style>
