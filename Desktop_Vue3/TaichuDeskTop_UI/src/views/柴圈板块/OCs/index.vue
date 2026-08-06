<template>
  <div class="ocs-page">
    <!-- 页面头部 -->
    <div class="page-header">
      <div class="header-left">
        <button class="back-btn" @click="goHome">← 返回</button>
        <div>
          <h1 class="page-title">OC 画阁</h1>
          <p class="page-subtitle">共收录 {{ total }} 位火柴人角色</p>
        </div>
      </div>
      <router-link to="/ocs/create" class="btn-line btn-primary">
        ＋ 投稿新 OC
      </router-link>
    </div>

    <!-- 搜索与筛选栏 -->
    <div class="filter-bar">
      <div class="search-box">
        <input
          v-model="keyword"
          type="text"
          placeholder="搜索角色名称..."
          @input="onSearch"
        />
      </div>
      <div class="filter-tabs">
        <button
          v-for="tab in tabs"
          :key="tab.value"
          :class="['btn-line', { active: currentTab === tab.value }]"
          @click="switchTab(tab.value)"
        >
          {{ tab.label }}
        </button>
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
    <div v-else-if="!characters.length" class="empty-state">
      <p>暂无 OC 角色</p>
      <router-link to="/ocs/create" class="empty-link">创建第一个角色</router-link>
    </div>

    <!-- 角色网格 -->
    <div v-else class="oc-grid">
      <CharacterCard
        v-for="char in characters"
        :key="char.id"
        :character="char"
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
import { useStickmanStore } from '../stickman_store'
import CharacterCard from '../components/CharacterCard.vue'

const router = useRouter()
const store = useStickmanStore()

const characters = computed(() => store.characters)
const loading = computed(() => store.loading)
const total = computed(() => store.total)

const keyword = ref('')
const currentTab = ref('latest')
const currentPage = ref(1)
const pageSize = 12

const tabs = [
  { label: '最新', value: 'latest' },
  { label: '热门', value: 'hot' },
  { label: '最多收藏', value: 'favorites' },
]

const totalPages = computed(() => Math.ceil(total.value / pageSize))

function fetchData() {
  store.fetchList({
    page: currentPage.value,
    pageSize,
    keyword: keyword.value || undefined,
  })
}

function onSearch() {
  currentPage.value = 1
  fetchData()
}

function switchTab(tab: string) {
  currentTab.value = tab
  currentPage.value = 1
  fetchData()
}

function goPage(page: number) {
  currentPage.value = page
  fetchData()
  window.scrollTo({ top: 0, behavior: 'smooth' })
}

function goDetail(id: string) {
  router.push(`/ocs/${id}`)
}

// ===== 返回柴圈首页 =====
function goHome() {
  router.push('/Chai')
}

watch(currentTab, () => {
  currentPage.value = 1
  fetchData()
})

onMounted(fetchData)
</script>

<style scoped>
.ocs-page {
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
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 16px;
  margin-bottom: 28px;
}

.search-box {
  position: relative;
  flex: 1;
  max-width: 300px;
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

.filter-tabs {
  display: flex;
  gap: 4px;
}

.filter-tabs .btn-line {
  padding: 6px 18px;
  font-size: 13px;
}

/* ===== 角色网格 ===== */
.oc-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(190px, 1fr));
  gap: 20px;
  margin-bottom: 40px;
}

/* ===== 加载骨架 ===== */
.loading-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(190px, 1fr));
  gap: 20px;
}

.skeleton-card {
  border: 1px solid var(--line-raw);
  padding: 16px;
  background: var(--paper-card);
}

.skeleton-image {
  width: 100%;
  height: 140px;
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
  .ocs-page {
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

  .filter-bar {
    flex-direction: column;
    align-items: stretch;
  }

  .search-box {
    max-width: 100%;
  }

  .oc-grid {
    grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
    gap: 14px;
  }

  .loading-grid {
    grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
  }
}

@media (max-width: 480px) {
  .oc-grid {
    grid-template-columns: repeat(2, 1fr);
    gap: 12px;
  }

  .loading-grid {
    grid-template-columns: repeat(2, 1fr);
  }

  .skeleton-image {
    height: 100px;
  }
}
</style>