<template>
  <div class="survey-list-container">
    <div class="list-header">
      <h2><i class="fas fa-clipboard-list"></i> 问卷列表</h2>
      <button v-if="userStore.canManageSurvey" class="btn-create" @click="$router.push('/activity/survey/create')">
        <i class="fas fa-plus"></i> 创建问卷
      </button>
    </div>

    <!-- 状态筛选 -->
    <div class="filter-tabs">
      <button
        v-for="tab in filterOptions"
        :key="tab.value"
        class="filter-tab"
        :class="{ active: currentFilter === tab.value }"
        @click="setFilter(tab.value)"
      >
        {{ tab.label }}
      </button>
    </div>

    <!-- 加载状态 -->
    <div v-if="loading" class="loading-state">
      <i class="fas fa-spinner fa-spin"></i> 加载中...
    </div>

    <!-- 问卷列表 -->
    <div v-else-if="surveys.length > 0" class="survey-grid">
      <div
        v-for="survey in surveys"
        :key="survey.id"
        class="survey-card"
        @click="goToFill(survey.id)"
      >
        <div
          class="card-cover"
          :style="{ backgroundImage: `url(${survey.coverImage || defaultCover})` }"
        >
          <span class="status-badge" :class="statusClass(survey.status)">
            {{ statusMap[survey.status] }}
          </span>
        </div>
        <div class="card-body">
          <h3>{{ survey.title }}</h3>
          <p class="desc">{{ survey.description || '暂无描述' }}</p>
          <div class="card-meta">
            <span><i class="far fa-clock"></i> {{ formatDate(survey.startTime) }}</span>
            <span><i class="fas fa-file-alt"></i> {{ survey.questionCount }} 题</span>
            <span><i class="fas fa-users"></i> {{ survey.totalSubmissions }} 人已答</span>
          </div>
          <p class="creator"><i class="fas fa-user-circle"></i> {{ survey.creatorName }}</p>
        </div>
        <div class="card-footer">
          <span class="time-range">
            {{ formatDate(survey.startTime) }} ~ {{ formatDate(survey.endTime) }}
          </span>

          <button
              v-if="survey.status === 2 || (survey.status === 1 && survey.totalSubmissions > 0)"
              class="btn-result"
              @click.stop="goToResult(survey.id)"
            >
              <i class="fas fa-chart-bar"></i> 查看结果
            </button>
           <button
              v-if="survey.status === 0 && userStore.canManageSurvey"
              class="btn-edit"
              @click.stop="goToEdit(survey.id)"
            >
              <i class="fas fa-edit"></i> 编辑
            </button>

          <button class="btn-fill" @click.stop="goToFill(survey.id)">
            去填写 <i class="fas fa-arrow-right"></i>
          </button>
        </div>
      </div>
    </div>

    <div v-else class="empty-state">
      <i class="fas fa-inbox"></i>
      <p>暂无问卷</p>
      <button v-if="userStore.canManageSurvey" class="btn-primary" @click="$router.push('/activity/survey/create')">
        创建第一个问卷
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useUserStore } from '@/stores/user'
import { getSurveyList, type SurveyListItem } from './api'

const router = useRouter()
const userStore = useUserStore()

const surveys = ref<SurveyListItem[]>([])
const loading = ref(false)
const currentFilter = ref('all')

const defaultCover = 'https://images.unsplash.com/photo-1434030216411-0b793f4b4173?w=600&h=300&fit=crop'

const filterOptions = [
  { label: '全部', value: 'all' },
  { label: '草稿', value: '0' },
  { label: '发布中', value: '1' },
  { label: '已结束', value: '2' },
]

const statusMap: Record<number, string> = {
  0: '草稿',
  1: '发布中',
  2: '已结束',
}

const statusClass = (status: number) => ({
  'status-draft': status === 0,
  'status-published': status === 1,
  'status-ended': status === 2,
})

const formatDate = (dateStr: string) => {
  const d = new Date(dateStr)
  return `${d.getMonth() + 1}/${d.getDate()}`
}

const setFilter = (value: string) => {
  currentFilter.value = value
  loadSurveys()
}

const goToFill = (id: number) => {
  router.push(`/activity/survey/${id}`)
}

const goToResult = (id: number) => {
  router.push(`/activity/survey/${id}/result`)
}

const goToEdit = (id: number) => {
  router.push(`/activity/survey/edit/${id}`)
}
const loadSurveys = async () => {
  loading.value = true
  try {
    const status = currentFilter.value === 'all' ? undefined : currentFilter.value
    const res = await getSurveyList(status)
    surveys.value = res || []
  } catch (error) {
    console.error('加载问卷列表失败:', error)
  } finally {
    loading.value = false
  }
}

onMounted(loadSurveys)
</script>

<style scoped>
.btn-edit {
  padding: 6px 16px;
  border: none;
  border-radius: 0;
  background: var(--bg-sub);
  color: var(--info);
  font-weight: 500;
  font-size: 0.8rem;
  cursor: pointer;
  transition: all 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.btn-edit:hover {
  background: var(--info-soft);
}
.survey-list-container {
  max-width: 1000px;
  margin: 0 auto;
  padding: 20px 0;
}

.list-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.list-header h2 {
  font-size: 1.5rem;
  font-weight: 700;
  color: var(--text-primary);
}

.list-header h2 i {
  color: var(--info);
  margin-right: 8px;
}

.btn-create {
  padding: 10px 24px;
  border: none;
  border-radius: 0;
  background: var(--info);
  color: var(--on-accent);
  font-weight: 500;
  font-size: 0.9rem;
  cursor: pointer;
  transition: all 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.btn-create:hover {
  background: var(--info);
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(99, 102, 241, 0.3);
}

.filter-tabs {
  display: flex;
  gap: 8px;
  margin-bottom: 24px;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--border-line);
}

.filter-tab {
  padding: 6px 16px;
  border-radius: 0;
  border: none;
  background: transparent;
  font-size: 0.85rem;
  font-weight: 500;
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 0.2s;
}

.filter-tab:hover {
  background: var(--bg-sub);
  color: var(--text-primary);
}

.filter-tab.active {
  background: var(--bg-sub);
  color: var(--text-primary);
}

.loading-state {
  text-align: center;
  padding: 60px 20px;
  color: var(--text-secondary);
}

.loading-state i {
  font-size: 2rem;
  display: block;
  margin-bottom: 12px;
}

.survey-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 24px;
}

.survey-card {
  background: var(--paper-card);
  border-radius: 0;
  overflow: hidden;
  border: 1px solid var(--border-line);
  cursor: pointer;
  transition: all 0.2s;
}

.survey-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 8px 25px rgba(0, 0, 0, 0.06);
  border-color: var(--border-line);
}

.card-cover {
  height: 140px;
  background-size: cover;
  background-position: center;
  position: relative;
  padding: 12px 16px;
  display: flex;
  justify-content: flex-end;
}

.status-badge {
  padding: 4px 14px;
  border-radius: 0;
  font-size: 0.7rem;
  font-weight: 500;
  color: var(--text-primary);
}

.status-draft {
  background: var(--bg-sub);
}

.status-published {
  background: var(--success);
}

.status-ended {
  background: var(--danger);
}

.card-body {
  padding: 16px 20px 12px;
}

.card-body h3 {
  font-size: 1.05rem;
  font-weight: 600;
  margin: 0 0 4px;
  color: var(--text-primary);
}

.card-body .desc {
  font-size: 0.85rem;
  color: var(--text-secondary);
  margin: 0 0 10px;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.card-meta {
  display: flex;
  gap: 14px;
  font-size: 0.75rem;
  color: var(--text-secondary);
}

.card-meta span {
  display: flex;
  align-items: center;
  gap: 4px;
}

.creator {
  font-size: 0.75rem;
  color: var(--text-secondary);
  margin: 6px 0 0;
}

.card-footer {
  padding: 10px 20px 16px;
  border-top: 1px solid var(--border-line);
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.time-range {
  font-size: 0.7rem;
  color: var(--text-secondary);
}

.btn-fill {
  padding: 6px 16px;
  border: none;
  border-radius: 0;
  background: var(--bg-sub);
  font-weight: 500;
  font-size: 0.8rem;
  color: var(--text-primary);
  cursor: pointer;
  transition: all 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.btn-fill:hover {
  background: var(--info);
  color: var(--on-accent);
}

.empty-state {
  grid-column: 1 / -1;
  text-align: center;
  padding: 60px 20px;
  color: var(--text-secondary);
}

.empty-state i {
  font-size: 3rem;
  color: var(--text-secondary);
  display: block;
  margin-bottom: 12px;
}

.btn-primary {
  margin-top: 12px;
  padding: 10px 24px;
  border: none;
  border-radius: 0;
  background: var(--info);
  color: var(--on-accent);
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-primary:hover {
  background: var(--info);
  transform: translateY(-2px);
}

@media (max-width: 768px) {
  .survey-grid {
    grid-template-columns: 1fr;
  }
}
</style>
