<template>
  <div class="joint-page">
    <!-- 页面头部 -->
    <div class="page-header">
      <div class="header-left">
        <button class="back-btn" @click="goHome">← 返回</button>
        <div>
          <h1 class="page-title">联合活动</h1>
          <p class="page-subtitle">共收录 {{ total }} 场联合</p>
        </div>
      </div>
      <router-link to="/joint/create" class="btn-line btn-primary">
        ＋ 发起联合
      </router-link>
    </div>

    <!-- 搜索与筛选栏 -->
    <div class="filter-bar">
      <div class="search-box">
        <input
          v-model="keyword"
          type="text"
          placeholder="搜索联合名称..."
          @input="onSearch"
        />
      </div>
      <div class="filter-group">
        <div class="filter-tabs">
          <button
            v-for="tab in statusTabs"
            :key="tab.value"
            :class="['btn-line', { active: currentStatus === tab.value }]"
            @click="switchStatus(tab.value)"
          >
            {{ tab.label }}
          </button>
        </div>
        <div class="filter-tabs">
          <button
            v-for="tab in typeTabs"
            :key="tab.value"
            :class="['btn-line', { active: currentType === tab.value }]"
            @click="switchType(tab.value)"
          >
            {{ tab.label }}
          </button>
        </div>
      </div>
    </div>

    <!-- 加载中 -->
    <div v-if="loading" class="loading-grid">
      <div v-for="i in 6" :key="i" class="skeleton-card">
        <div class="skeleton-image shimmer"></div>
        <div class="skeleton-line"></div>
        <div class="skeleton-line short"></div>
      </div>
    </div>

    <!-- 空状态 -->
    <div v-else-if="!activities.length" class="empty-state">
      <p>暂无联合活动</p>
      <router-link to="/joint/create" class="empty-link">发起第一个联合</router-link>
    </div>

    <!-- 活动网格 -->
    <div v-else class="joint-grid">
      <JointCard
        v-for="item in activities"
        :key="item.id"
        :activity="item"
        @click="goDetail"
      />
    </div>

    <!-- 分页 -->
    <div v-if="totalPages > 1" class="pagination">
      <button
        v-for="page in totalPages"
        :key="page"
        :class="['page-btn', { active: currentPage === page }]"
        @click="goPage(page)"
      >
        {{ page }}
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useJointStore } from './joint_store'
import JointCard from './components/JointCard.vue'
import type { JointStatus, JointType } from './joint'

const router = useRouter()
const store = useJointStore()

const activities = computed(() => store.activities)
const loading = computed(() => store.loading)
const total = computed(() => store.total)

const keyword = ref('')
const currentStatus = ref<JointStatus | 'all'>('all')
const currentType = ref<JointType | 'all'>('all')
const currentPage = ref(1)
const pageSize = 12

const statusTabs = [
  { label: '全部', value: 'all' },
  { label: '报名中', value: 'open' },
  { label: '已截止', value: 'closed' },
  { label: '已结束', value: 'ended' },
  { label: '已封禁', value: 'banned' },
  { label: '暴毙', value: 'abandoned' },
] as const  // ← 添加 as const

const typeTabs = [
  { label: '全部', value: 'all' },
  { label: '联合', value: 'joint' },
  { label: '接力', value: 'relay' },
  { label: '企划', value: 'project' },
  { label: '自由', value: 'free' },
  { label: '其他', value: 'other' },
] as const  // ← 添加 as const

const totalPages = computed(() => Math.ceil(total.value / pageSize))

async function fetchData() {
  await store.fetchList({
    page: currentPage.value,
    pageSize,
    keyword: keyword.value || undefined,
    status: currentStatus.value === 'all' ? undefined : currentStatus.value,
    type: currentType.value === 'all' ? undefined : currentType.value,
  })
}

function onSearch() {
  currentPage.value = 1
  fetchData()
}

function switchStatus(status: JointStatus | 'all') {
  currentStatus.value = status
  currentPage.value = 1
  fetchData()
}

function switchType(type: JointType | 'all') {
  currentType.value = type
  currentPage.value = 1
  fetchData()
}

function goPage(page: number) {
  currentPage.value = page
  fetchData()
  window.scrollTo({ top: 0, behavior: 'smooth' })
}

function goDetail(id: string) {
  router.push(`/joint/${id}`)
}

function goHome() {
  router.push('/Chai')
}

onMounted(fetchData)
</script>

<style scoped>
.joint-page {
  max-width: 1280px;
  margin: 0 auto;
  padding: 32px 24px 60px;
  background: #F4F1EA !important;
}

/* ===== 页面头部 ===== */
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  padding-bottom: 16px;
  border-bottom: 1px solid var(--line-raw);
  margin-bottom: 28px;
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

/* ===== 筛选栏 ===== */
.filter-bar {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin-bottom: 28px;
}

.search-box {
  position: relative;
  max-width: 340px;
}

.search-box input {
  width: 100%;
  padding: 8px 16px;
  border: 1px solid var(--line-raw);
  background: var(--paper-card);
  color: var(--ink-black);
  font-family: var(--font-family);
  font-size: 14px;
  letter-spacing: 0.1em;
  outline: none;
  transition: border-color 0.3s;
}

.search-box input:focus {
  border-color: var(--ink-black);
}

.search-box input::placeholder {
  color: var(--ink-light);
}

.filter-group {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 12px;
  align-items: center;
}

.filter-tabs {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
}

.filter-tabs .btn-line {
  padding: 4px 14px;
  font-size: 12px;
}

/* ===== 活动网格 ===== */
.joint-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 20px;
  margin-bottom: 40px;
}

/* ===== 加载骨架 ===== */
.loading-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 20px;
}

.skeleton-card {
  border: 1px solid var(--line-raw);
  padding: 16px;
  background: var(--paper-card);
}

.skeleton-image {
  width: 100%;
  height: 120px;
  background: var(--paper-sub);
  margin-bottom: 12px;
}

.skeleton-line {
  height: 14px;
  background: var(--paper-sub);
  margin: 6px 0;
}

.skeleton-line.short {
  width: 60%;
}

.shimmer {
  animation: shimmer 1.8s ease-in-out infinite;
}

@keyframes shimmer {
  0%,
  100% {
    opacity: 1;
  }
  50% {
    opacity: 0.5;
  }
}

/* ===== 空状态 ===== */
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

/* ===== 分页 ===== */
.pagination {
  display: flex;
  justify-content: center;
  gap: 6px;
  padding-top: 20px;
  border-top: 1px solid var(--line-raw);
}

.page-btn {
  width: 36px;
  height: 36px;
  border: 1px solid transparent;
  background: transparent;
  color: var(--ink-gray);
  font-family: var(--font-family);
  font-size: 14px;
  cursor: pointer;
  transition: all 0.3s ease;
}

.page-btn:hover {
  border-color: var(--line-raw);
}

.page-btn.active {
  border-color: var(--ink-black);
  color: var(--ink-black);
}

/* ===== 响应式 ===== */
@media (max-width: 768px) {
  .joint-page {
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

  .filter-group {
    flex-direction: column;
    align-items: stretch;
  }

  .search-box {
    max-width: 100%;
  }

  .joint-grid {
    grid-template-columns: 1fr 1fr;
    gap: 14px;
  }

  .loading-grid {
    grid-template-columns: 1fr 1fr;
  }
}

@media (max-width: 480px) {
  .joint-grid {
    grid-template-columns: 1fr;
  }

  .loading-grid {
    grid-template-columns: 1fr;
  }

  .skeleton-image {
    height: 100px;
  }
}
</style>