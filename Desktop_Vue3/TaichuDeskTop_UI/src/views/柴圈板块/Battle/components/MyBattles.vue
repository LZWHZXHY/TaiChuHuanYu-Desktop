<template>
  <div class="my-battles content-page community-surface tone-chai">
    <header class="content-header">
      <div>
        <button class="back-btn" @click="goBack">← 约战广场</button>
        <h1>我的约战</h1>
        <p class="intro">你发起和参与的约战都在这里：看看哪场该交作品、哪场等你录结果。</p>
      </div>
      <div class="content-actions">
        <router-link to="/battles/create" class="primary">发起约战</router-link>
      </div>
    </header>

    <!-- Tab 切换 -->
    <div class="tab-bar" role="tablist" aria-label="我发起的 / 我参与的">
      <button
        v-for="tab in tabs"
        :key="tab.key"
        role="tab"
        :aria-selected="currentTab === tab.key"
        :class="['tab-btn', { active: currentTab === tab.key }]"
        @click="switchTab(tab.key)"
      >
        {{ tab.label }}
        <span class="tab-count">{{ tab.key === 'initiated' ? initiatedList.length : participatedList.length }}</span>
      </button>
    </div>

    <!-- 状态筛选：和约战广场一样的一排按钮 -->
    <div class="filter-bar">
      <div class="content-filters" role="group" aria-label="按阶段筛选">
        <button v-for="f in filters" :key="f.value" type="button" :aria-pressed="filterStatus === f.value" @click="filterStatus = f.value">{{ f.label }}</button>
      </div>
    </div>

    <!-- 加载中 -->
    <div v-if="loading && !allBattles.length" class="state">
      <div class="spinner"></div>
      <span>加载中...</span>
    </div>

    <!-- 空状态 -->
    <div v-else-if="!currentList.length" class="state">
      <template v-if="filterStatus">
        <p>这个阶段没有{{ currentTab === 'initiated' ? '你发起的' : '你参与的' }}约战。</p>
        <button class="state-btn" @click="filterStatus = ''">看全部</button>
      </template>
      <template v-else>
        <p v-if="currentTab === 'initiated'">你还没有发起过约战。</p>
        <p v-else>你还没有参与别人的约战。</p>
        <router-link v-if="currentTab === 'initiated'" to="/battles/create" class="state-btn">发起第一场约战</router-link>
        <router-link v-else to="/battles" class="state-btn">去约战广场看看</router-link>
      </template>
    </div>

    <!-- 列表 -->
    <div v-else class="battle-list">
      <BattleCard
        v-for="item in currentList"
        :key="item.id"
        :battle="item"
        :role="isMine(item) ? '我发起的' : '我参与的'"
        :my-result="myResult(item)"
        @open="goDetail"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useUserStore } from '@/stores/user'
import { useBattleStore } from '../battle_Store'
import type { Battle } from '../battle_api'
import BattleCard from './BattleCard.vue'
import { STATUS_LABEL, outcomeOf, resultLabel } from '../battleView'

const router = useRouter()
const userStore = useUserStore()
const battleStore = useBattleStore()

// 「我」统一用 userStore.userId，不分大小写比较（以前用 userInfo.id 严格相等，个别情况下「我发起的」会是空的）
const userId = computed(() => (userStore.userId || '').toLowerCase())
const same = (id?: string | null) => !!userId.value && (id || '').toLowerCase() === userId.value
/** 是不是我发起的：有 initiatorId 用它，没有就看第一个参与者 */
const isMine = (b: Battle) => same(b.initiatorId || b.participants?.[0]?.userId)

const currentTab = ref<'initiated' | 'participated'>('initiated')
const filterStatus = ref('')

const tabs = [
  { key: 'initiated', label: '我发起的' },
  { key: 'participated', label: '我参与的' },
] as const

// 状态名和约战广场、详情页统一
const filters = [
  { value: '', label: '全部' },
  ...(['open', 'ongoing', 'judging', 'finished', 'cancelled'] as const).map(v => ({ value: v, label: STATUS_LABEL[v] })),
]

const allBattles = computed(() => battleStore.myBattles)
const loading = computed(() => battleStore.myBattlesLoading)

// ===== 当前列表（根据 Tab + 状态筛选） =====
const currentList = computed(() => {
  let list = currentTab.value === 'initiated' ? initiatedList.value : participatedList.value
  // 根据状态筛选
  if (filterStatus.value) {
    list = list.filter(item => item.status === filterStatus.value)
  }
  return [...list].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
})

// ===== 计算各 Tab 数量 =====
const initiatedList = computed(() => allBattles.value.filter(item => isMine(item)))
const participatedList = computed(() => allBattles.value.filter(item => !isMine(item)))

/** 我在这场的结果（结束了才显示） */
function myResult(b: Battle): string {
  if (b.status !== 'finished') return ''
  const me = b.participants?.find(p => same(p.userId))
  return me ? resultLabel(me.result, outcomeOf(b)) : ''
}

// ===== 加载数据 =====
const fetchData = async () => {
  await battleStore.fetchMyBattles()
}

function switchTab(tab: 'initiated' | 'participated') {
  currentTab.value = tab
  fetchData()
}

function goBack() {
  router.push('/battles')
}

function goDetail(id: string) {
  router.push(`/battles/${id}`)
}

onMounted(() => {
  fetchData()
})
</script>

<style scoped>
.my-battles { padding: 0 20px 60px; }
.back-btn { padding: 0; min-height: 0; border: 0; background: none; color: var(--text-secondary); font: inherit; font-size: 14px; cursor: pointer; }
.back-btn:hover { color: var(--accent-color); }

.tab-bar { display: flex; border-bottom: 1px solid var(--border-line); margin-bottom: 14px; }
.tab-btn { display: flex; align-items: center; gap: 8px; padding: 10px 20px; border: 0; border-bottom: 2px solid transparent; margin-bottom: -1px; background: none; font: inherit; font-size: 15px; color: var(--text-secondary); cursor: pointer; }
.tab-btn:hover { color: var(--text-primary); }
.tab-btn.active { color: var(--text-primary); font-weight: 600; border-bottom-color: var(--accent-color); }
.tab-count { padding: 0 7px; font: 600 12px/1.6 var(--font-display); color: var(--text-secondary); background: var(--bg-sub); }
.tab-btn.active .tab-count { background: var(--accent-color); color: var(--on-accent); }

.filter-bar { margin-bottom: 16px; }
.filter-bar .content-filters { flex-wrap: wrap; }

.state { display: flex; flex-direction: column; align-items: center; gap: 12px; padding: 48px 20px; border: 1px dashed var(--border-line); background: var(--paper-card); color: var(--text-secondary); font-size: 14px; text-align: center; }
.state p { margin: 0; }
.state-btn { padding: 8px 18px; border: 1px solid var(--accent-color); background: var(--accent-color); color: var(--on-accent); font: inherit; font-size: 14px; text-decoration: none; cursor: pointer; }
.spinner { width: 32px; height: 32px; border: 2px solid var(--border-line); border-top-color: var(--text-primary); border-radius: 50%; animation: spin .8s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }

.battle-list { display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 400px), 1fr)); gap: 14px; }

@media (max-width: 640px) {
  .my-battles { padding: 0 12px 40px; }
  .tab-btn { flex: 1; justify-content: center; padding: 10px 8px; }
  .filter-bar .content-filters button { flex: 1 1 auto; padding: 7px 10px; }
}
</style>
