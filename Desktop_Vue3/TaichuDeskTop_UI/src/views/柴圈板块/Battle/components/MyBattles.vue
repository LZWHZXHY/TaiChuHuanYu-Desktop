<template>
  <div class="my-battles">
    <div class="page-header">
      <div class="header-left">
        <button class="back-btn" @click="goBack">← 返回</button>
        <div>
          <h1 class="page-title">📋 我的约战</h1>
          <p class="page-subtitle">管理你发起和参与的约战</p>
        </div>
      </div>
      <router-link to="/battles/create" class="btn-primary">
        ＋ 发起约战
      </router-link>
    </div>

    <!-- Tab 切换 -->
    <div class="tab-bar">
      <button
        v-for="tab in tabs"
        :key="tab.key"
        :class="['tab-btn', { active: currentTab === tab.key }]"
        @click="switchTab(tab.key)"
      >
        {{ tab.label }}
        <span class="tab-count">{{ tab.key === 'initiated' ? initiatedList.length : participatedList.length }}</span>
      </button>
    </div>

    <!-- 状态筛选 -->
    <div class="filter-bar">
      <select v-model="filterStatus" @change="fetchData">
        <option value="">全部状态</option>
        <option value="open">待应战</option>
        <option value="ongoing">创作中</option>
        <option value="judging">定夺中</option>
        <option value="finished">已了结</option>
        <option value="cancelled">已罢战</option>
      </select>
    </div>

    <!-- 加载中 -->
    <div v-if="loading" class="loading-state">
      <div class="spinner"></div>
      <span>加载中...</span>
    </div>

    <!-- 空状态 -->
    <div v-else-if="!currentList.length" class="empty-state">
      <p v-if="currentTab === 'initiated'">还没有发起的约战</p>
      <p v-else>还没有参与的约战</p>
      <router-link v-if="currentTab === 'initiated'" to="/battles/create" class="empty-link">
        发起第一场约战
      </router-link>
      <router-link v-else to="/battles" class="empty-link">
        浏览约战大厅
      </router-link>
    </div>

    <!-- 列表 -->
    <div v-else class="battle-list">
      <div
        v-for="item in currentList"
        :key="item.id"
        class="battle-item"
        @click="goDetail(item.id)"
      >
        <div class="item-cover">
          <img v-if="item.coverUrl" :src="item.coverUrl" alt="" />
          <span v-else class="placeholder">⚔</span>
        </div>
        <div class="item-info">
          <h3>{{ item.title }}</h3>
          <div class="item-meta">
            <span class="status" :class="item.status">
              {{ statusMap[item.status] || item.status }}
            </span>
            <span class="type">{{ item.battleType || '自定义' }}</span>
            <span class="judgment">
              {{ item.judgmentType === 'vote' ? '投票制' : '内定制' }}
            </span>
            <!-- ⭐ 修改：从 participants 判断 -->
            <span class="role">
              {{ item.participants?.[0]?.userId === userId ? '👤 我发起的' : '✋ 我参与的' }}
            </span>
          </div>
          <!-- ⭐ 修改：参与者信息 -->
          <div class="item-opponents">
            <span>参与者：{{ item.participants?.map(p => p.userName).join('、') || '无' }}</span>
          </div>
          <div class="item-time">{{ formatTime(item.createdAt) }}</div>
        </div>
        <div class="item-action">
          <span>查看 →</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useUserStore } from '@/stores/user'
import { useBattleStore } from '../battle_Store'
import type { Battle } from '../battle_api'

const router = useRouter()
const userStore = useUserStore()
const battleStore = useBattleStore()

const userId = computed(() => userStore.userInfo?.id)

const currentTab = ref<'initiated' | 'participated'>('initiated')
const filterStatus = ref('')

const tabs = [
  { key: 'initiated', label: '我发起的' },
  { key: 'participated', label: '我参与的' },
] as const

const allBattles = computed(() => battleStore.myBattles)
const loading = computed(() => battleStore.myBattlesLoading)

const statusMap: Record<string, string> = {
  open: '待应战',
  ongoing: '创作中',
  judging: '定夺中',
  finished: '已了结',
  cancelled: '已罢战',
}

// ===== 当前列表（根据 Tab + 状态筛选） =====
const currentList = computed(() => {
  let list = allBattles.value

  // 根据 Tab 筛选：判断第一个参与者是否是当前用户
  if (currentTab.value === 'initiated') {
    list = list.filter(item => item.participants?.[0]?.userId === userId.value)
  } else {
    list = list.filter(item => item.participants?.[0]?.userId !== userId.value)
  }

  // 根据状态筛选
  if (filterStatus.value) {
    list = list.filter(item => item.status === filterStatus.value)
  }

  return [...list].sort((a, b) => {
    return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
  })
})

// ===== 计算各 Tab 数量 =====
const initiatedList = computed(() =>
  allBattles.value.filter(item => item.participants?.[0]?.userId === userId.value)
)

const participatedList = computed(() =>
  allBattles.value.filter(item => item.participants?.[0]?.userId !== userId.value)
)

// ===== 加载数据 =====
const fetchData = async () => {
  await battleStore.fetchMyBattles()
}

function switchTab(tab: 'initiated' | 'participated') {
  currentTab.value = tab
  fetchData()
}

function formatTime(iso: string) {
  const d = new Date(iso)
  const now = new Date()
  const diff = Math.floor((now.getTime() - d.getTime()) / 86400000)
  if (diff === 0) return '今天'
  if (diff === 1) return '昨天'
  if (diff < 7) return `${diff} 天前`
  return d.toLocaleDateString('zh-CN', { month: 'short', day: 'numeric' })
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
.my-battles {
  max-width: 960px;
  margin: 0 auto;
  padding: 32px 20px 60px;
  background: #f5f0eb;
  min-height: 100vh;
  color: #2c2a29;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  padding-bottom: 16px;
  border-bottom: 2px solid #d8d0c4;
  margin-bottom: 24px;
  flex-wrap: wrap;
  gap: 12px;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 16px;
}

.back-btn {
  background: none;
  border: none;
  font-size: 15px;
  color: #999;
  cursor: pointer;
  font-family: inherit;
  padding: 4px 8px 4px 0;
  transition: color 0.25s;
}
.back-btn:hover {
  color: #2c2a29;
}

.page-title {
  font-size: 24px;
  font-weight: 400;
  letter-spacing: 0.15em;
  margin: 0 0 4px 0;
  color: #2c2a29;
}

.page-subtitle {
  font-size: 14px;
  color: #999;
  letter-spacing: 0.08em;
  margin: 0;
}

.btn-primary {
  padding: 8px 20px;
  border: 1px solid #2c2a29;
  background: #2c2a29;
  color: #f5f0eb;
  text-decoration: none;
  font-size: 13px;
  letter-spacing: 0.1em;
  transition: all 0.3s;
}
.btn-primary:hover {
  background: transparent;
  color: #2c2a29;
}

.tab-bar {
  display: flex;
  gap: 0;
  border-bottom: 1px solid #d8d0c4;
  margin-bottom: 16px;
}

.tab-btn {
  padding: 10px 24px;
  background: none;
  border: none;
  font-family: inherit;
  font-size: 14px;
  letter-spacing: 0.1em;
  color: #999;
  cursor: pointer;
  transition: all 0.3s;
  display: flex;
  align-items: center;
  gap: 8px;
}
.tab-btn:hover {
  color: #2c2a29;
}
.tab-btn.active {
  color: #2c2a29;
  border-bottom: 2px solid #9e2a2b;
}

.tab-count {
  font-size: 12px;
  color: #999;
  background: #e8e2da;
  padding: 0 8px;
  border-radius: 10px;
}
.tab-btn.active .tab-count {
  background: #9e2a2b;
  color: #fff;
}

.filter-bar {
  margin-bottom: 20px;
}

.filter-bar select {
  padding: 6px 14px;
  border: 1px solid #d8d0c4;
  background: #fcfaf7;
  font-family: inherit;
  font-size: 13px;
  color: #2c2a29;
  outline: none;
  appearance: none;
  background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='10' height='6' viewBox='0 0 10 6'%3E%3Cpath fill='%23999' d='M5 6L0 0h10z'/%3E%3C/svg%3E");
  background-repeat: no-repeat;
  background-position: right 12px center;
  padding-right: 32px;
  cursor: pointer;
}
.filter-bar select:focus {
  border-color: #2c2a29;
}

.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 0;
  gap: 16px;
  color: #999;
}

.spinner {
  width: 32px;
  height: 32px;
  border: 2px solid #d8d0c4;
  border-top-color: #2c2a29;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.empty-state {
  padding: 60px 0;
  text-align: center;
  color: #999;
  font-size: 14px;
}
.empty-state p {
  margin: 0 0 12px 0;
}

.empty-link {
  color: #9e2a2b;
  text-decoration: none;
  border-bottom: 1px solid #d8d0c4;
  padding-bottom: 2px;
}
.empty-link:hover {
  border-color: #9e2a2b;
}

.battle-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.battle-item {
  display: flex;
  gap: 16px;
  padding: 14px 18px;
  border: 1px solid #d8d0c4;
  background: #fcfaf7;
  cursor: pointer;
  transition: all 0.2s;
}
.battle-item:hover {
  border-color: #2c2a29;
  transform: translateY(-2px);
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
}

.item-cover {
  flex-shrink: 0;
  width: 64px;
  height: 64px;
  background: #ede8e2;
  border-radius: 4px;
  overflow: hidden;
  display: flex;
  align-items: center;
  justify-content: center;
}
.item-cover img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}
.item-cover .placeholder {
  font-size: 24px;
  color: #9e2a2b;
}

.item-info {
  flex: 1;
  min-width: 0;
}
.item-info h3 {
  font-size: 16px;
  font-weight: 400;
  letter-spacing: 0.08em;
  margin: 0 0 4px 0;
  color: #2c2a29;
}

.item-meta {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  font-size: 12px;
}
.item-meta .status {
  padding: 0 10px;
  border: 1px solid #d8d0c4;
}
.item-meta .status.open { border-color: #4CAF50; color: #4CAF50; }
.item-meta .status.ongoing { border-color: #FF9800; color: #FF9800; }
.item-meta .status.judging { border-color: #FF9800; color: #FF9800; }
.item-meta .status.finished { border-color: #9E9E9E; color: #9E9E9E; }
.item-meta .status.cancelled { border-color: #F44336; color: #F44336; }

.item-meta .type {
  color: #888;
}
.item-meta .judgment {
  color: #9e2a2b;
}
.item-meta .role {
  color: #999;
}

.item-opponent {
  font-size: 13px;
  color: #888;
  margin-top: 2px;
}

.item-time {
  font-size: 12px;
  color: #bbb;
  margin-top: 2px;
}

.item-action {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  padding-left: 12px;
  font-size: 13px;
  color: #ccc;
  transition: color 0.2s;
}
.battle-item:hover .item-action {
  color: #2c2a29;
}

@media (max-width: 640px) {
  .my-battles { padding: 20px 12px 40px; }
  .page-header { flex-direction: column; align-items: flex-start; }
  .header-left { flex-wrap: wrap; }
  .btn-primary { width: 100%; text-align: center; }
  .battle-item { flex-direction: column; }
  .item-cover { width: 100%; height: 80px; }
  .item-action { padding-left: 0; padding-top: 8px; border-top: 1px solid #eee; justify-content: flex-end; }
  .tab-btn { padding: 8px 16px; font-size: 13px; }
}
</style>