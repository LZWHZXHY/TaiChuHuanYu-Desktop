<template>
  <div class="my-joint">
    <!-- 页面头部 -->
    <div class="page-header">
      <div class="header-left">
        <button class="back-btn" @click="goHome">← 返回</button>
        <div>
          <h1 class="page-title">📂 我的联合活动</h1>
          <p class="page-subtitle">管理你举办和参与的活动</p>
        </div>
      </div>
      <router-link to="/joint/create" class="btn-line btn-primary">
        ＋ 发起联合
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
        <span class="tab-count">{{ tab.key === 'organized' ? organizedList.length : participatedList.length }}</span>
      </button>
    </div>

    <!-- 加载中 -->
    <div v-if="loading" class="loading-state">
      <div class="spinner"></div>
      <span>加载中...</span>
    </div>

    <!-- 空状态 -->
    <div v-else-if="!currentList.length" class="empty-state">
      <p v-if="currentTab === 'organized'">还没有举办的活动</p>
      <p v-else>还没有参与的活动</p>
      <router-link v-if="currentTab === 'organized'" to="/joint/create" class="empty-link">
        发起第一个联合
      </router-link>
      <router-link v-else to="/joint" class="empty-link">
        浏览联合活动
      </router-link>
    </div>

    <!-- 活动列表 -->
    <div v-else class="joint-grid">
      <JointCard
        v-for="item in currentList"
        :key="item.id"
        :activity="item"
        @click="goDetail"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useJointStore } from '../joint_store'
import JointCard from '../components/JointCard.vue'

const router = useRouter()
const store = useJointStore()

const currentTab = ref<'organized' | 'participated'>('organized')

// ✅ 添加 as const，让 TypeScript 推断字面量类型
const tabs = [
  { key: 'organized', label: '我举办的' },
  { key: 'participated', label: '我参与的' },
] as const

const organizedList = computed(() => store.myOrganized)
const participatedList = computed(() => store.myParticipated)
const loading = computed(() => store.loading)

const currentList = computed(() => {
  return currentTab.value === 'organized' ? organizedList.value : participatedList.value
})

function switchTab(tab: 'organized' | 'participated') {
  currentTab.value = tab
  if (tab === 'organized') {
    store.fetchMyOrganized()
  } else {
    store.fetchMyParticipated()
  }
}

function goDetail(id: string) {
  router.push(`/joint/${id}`)
}

function goHome() {
  router.push('/')
}

onMounted(() => {
  store.fetchMyOrganized()
})
</script>

<style scoped>
/* 样式保持不变，与之前一致 */
.my-joint {
  max-width: 1280px;
  margin: 0 auto;
  padding: 32px 24px 60px;
  background: var(--paper-bg);
  min-height: 100vh;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  padding-bottom: 16px;
  border-bottom: 1px solid var(--line-raw);
  margin-bottom: 24px;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 16px;
}

.back-btn {
  background: none;
  border: none;
  color: var(--ink-gray);
  font-size: 14px;
  letter-spacing: 0.15em;
  cursor: pointer;
  padding: 4px 8px 4px 0;
  font-family: var(--font-family);
  transition: color 0.3s;
  white-space: nowrap;
}

.back-btn:hover {
  color: var(--ink-black);
}

.page-title {
  font-size: 24px;
  font-weight: 400;
  letter-spacing: 0.25em;
  margin: 0 0 4px 0;
  color: var(--ink-black);
}

.page-subtitle {
  font-size: 13px;
  color: var(--ink-gray);
  letter-spacing: 0.15em;
  margin: 0;
}

.btn-primary {
  padding: 8px 24px;
  border-color: var(--ink-black);
}

.tab-bar {
  display: flex;
  gap: 0;
  margin-bottom: 28px;
  border-bottom: 1px solid var(--line-raw);
}

.tab-btn {
  padding: 10px 24px;
  background: none;
  border: none;
  font-family: var(--font-family);
  font-size: 14px;
  letter-spacing: 0.15em;
  color: var(--ink-gray);
  cursor: pointer;
  transition: all 0.3s;
  position: relative;
  display: flex;
  align-items: center;
  gap: 8px;
}

.tab-btn:hover {
  color: var(--ink-black);
}

.tab-btn.active {
  color: var(--ink-black);
  border-bottom: 2px solid var(--cinnabar);
}

.tab-count {
  font-size: 12px;
  color: var(--ink-light);
  background: var(--paper-sub);
  padding: 0 8px;
  border-radius: 10px;
}

.tab-btn.active .tab-count {
  background: var(--cinnabar);
  color: #fff;
}

.joint-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 20px;
}

.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 0;
  gap: 16px;
  color: var(--ink-gray);
}

.spinner {
  width: 32px;
  height: 32px;
  border: 2px solid var(--line-raw);
  border-top-color: var(--ink-black);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.empty-state {
  padding: 60px 0;
  text-align: center;
  color: var(--ink-gray);
  letter-spacing: 0.15em;
}

.empty-state p {
  font-size: 15px;
  margin: 0 0 12px 0;
}

.empty-link {
  color: var(--cinnabar);
  text-decoration: none;
  border-bottom: 1px solid var(--line-raw);
  padding-bottom: 2px;
  transition: border-color 0.3s;
}

.empty-link:hover {
  border-color: var(--cinnabar);
}

@media (max-width: 768px) {
  .my-joint {
    padding: 20px 16px 40px;
  }

  .page-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 12px;
  }

  .header-left {
    flex-wrap: wrap;
  }

  .tab-btn {
    padding: 8px 16px;
    font-size: 13px;
  }

  .joint-grid {
    grid-template-columns: 1fr 1fr;
    gap: 14px;
  }
}

@media (max-width: 480px) {
  .joint-grid {
    grid-template-columns: 1fr;
  }
}
</style>