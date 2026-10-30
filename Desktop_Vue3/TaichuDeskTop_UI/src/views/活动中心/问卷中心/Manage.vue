<template>
  <div class="manage-container">
    <!-- 头部 -->
    <div class="manage-header">
      <h2><i class="fas fa-cog"></i> 问卷管理</h2>
      <button class="btn-create" @click="$router.push('/activity/survey/create')">
        <i class="fas fa-plus"></i> 创建问卷
      </button>
    </div>

    <!-- 统计卡片 -->
    <div class="stats-row">
      <div class="stat-card">
        <span class="stat-number">{{ stats.total }}</span>
        <span class="stat-label">全部问卷</span>
      </div>
      <div class="stat-card">
        <span class="stat-number">{{ stats.draft }}</span>
        <span class="stat-label">草稿</span>
      </div>
      <div class="stat-card">
        <span class="stat-number">{{ stats.published }}</span>
        <span class="stat-label">已发布</span>
      </div>
      <div class="stat-card">
        <span class="stat-number">{{ stats.ended }}</span>
        <span class="stat-label">已结束</span>
      </div>
    </div>

    <!-- 筛选 -->
    <div class="filter-tabs">
      <button
        v-for="tab in filterOptions"
        :key="tab.value"
        class="filter-tab"
        :class="{ active: currentFilter === tab.value }"
        @click="setFilter(tab.value)"
      >
        {{ tab.label }} ({{ tabCounts[tab.value] || 0 }})
      </button>
    </div>

    <!-- 加载 -->
    <div v-if="loading" class="loading-state">
      <i class="fas fa-spinner fa-spin"></i> 加载中...
    </div>

    <!-- 表格 -->
    <div v-else-if="surveys.length > 0" class="table-wrapper">
      <table class="survey-table">
        <thead>
          <tr>
            <th>ID</th>
            <th>标题</th>
            <th>状态</th>
            <th>提交数</th>
            <th>创建时间</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="survey in surveys" :key="survey.id">
            <td>#{{ survey.id }}</td>
            <td>
              <span class="survey-title">{{ survey.title }}</span>
              <span class="survey-desc">{{ survey.description || '' }}</span>
            </td>
            <td>
              <span class="status-badge" :class="statusClass(survey.status)">
                {{ statusMap[survey.status] }}
              </span>
            </td>
            <td>{{ survey.totalSubmissions }}</td>
            <td>{{ formatDate(survey.createdAt) }}</td>
            <td>
              <div class="action-buttons">
                <!-- 草稿：编辑 + 发布 + 删除 -->
                <template v-if="survey.status === 0">
                  <button class="btn-edit" @click="goToEdit(survey.id)" title="编辑">
                    <i class="fas fa-edit"></i>
                  </button>
                  <button class="btn-publish" @click="handlePublish(survey.id)" title="发布">
                    <i class="fas fa-rocket"></i>
                  </button>
                  <button class="btn-delete" @click="handleDelete(survey.id)" title="删除">
                    <i class="fas fa-trash"></i>
                  </button>
                </template>
                <!-- 发布中：查看结果 + 结束 -->
                <template v-else-if="survey.status === 1">
                  <button class="btn-result" @click="goToResult(survey.id)" title="查看结果">
                    <i class="fas fa-chart-bar"></i>
                  </button>
                  <button class="btn-close" @click="handleClose(survey.id)" title="结束">
                    <i class="fas fa-stop"></i>
                  </button>
                </template>
                <!-- 已结束：查看结果 + 删除 -->
                <template v-else-if="survey.status === 2">
                  <button class="btn-result" @click="goToResult(survey.id)" title="查看结果">
                    <i class="fas fa-chart-bar"></i>
                  </button>
                  <button class="btn-delete" @click="handleDelete(survey.id)" title="删除">
                    <i class="fas fa-trash"></i>
                  </button>
                </template>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- 空状态 -->
    <div v-else class="empty-state">
      <i class="fas fa-inbox"></i>
      <p>暂无问卷</p>
      <button class="btn-primary" @click="$router.push('/activity/survey/create')">
        创建第一个问卷
      </button>
    </div>

    <!-- 分页 -->
    <div v-if="totalCount > 0" class="pagination-footer">
      <span class="page-info">共 {{ totalCount }} 条，每页 {{ pageSize }} 条</span>
      <div class="page-controls">
        <button class="btn-page" :disabled="currentPage === 1" @click="changePage(currentPage - 1)">上一页</button>
        <span class="current-page">{{ currentPage }} / {{ totalPages }}</span>
        <button class="btn-page" :disabled="currentPage >= totalPages" @click="changePage(currentPage + 1)">下一页</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getSurveyList, publishSurvey, closeSurvey, deleteSurvey } from './api'

const router = useRouter()

const surveys = ref<any[]>([])
const loading = ref(false)
const currentFilter = ref('all')
const currentPage = ref(1)
const pageSize = ref(30)
const totalCount = ref(0)

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
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

const stats = computed(() => {
  const total = surveys.value.length
  const draft = surveys.value.filter(s => s.status === 0).length
  const published = surveys.value.filter(s => s.status === 1).length
  const ended = surveys.value.filter(s => s.status === 2).length
  return { total, draft, published, ended }
})

const tabCounts = computed(() => {
  const counts: Record<string, number> = { all: surveys.value.length }
  surveys.value.forEach(s => {
    const key = String(s.status)
    counts[key] = (counts[key] || 0) + 1
  })
  return counts
})

const totalPages = computed(() => Math.ceil(totalCount.value / pageSize.value) || 1)

const setFilter = (value: string) => {
  currentFilter.value = value
  currentPage.value = 1
  loadSurveys()
}

const changePage = (page: number) => {
  if (page < 1 || page > totalPages.value) return
  currentPage.value = page
  loadSurveys()
}

const loadSurveys = async () => {
  loading.value = true
  try {
    const status = currentFilter.value === 'all' ? undefined : currentFilter.value
    const res = await getSurveyList(status)
    surveys.value = res || []
    totalCount.value = surveys.value.length
  } catch (error) {
    console.error('加载问卷列表失败:', error)
  } finally {
    loading.value = false
  }
}

const goToEdit = (id: number) => {
  router.push(`/activity/survey/edit/${id}`)
}

const goToResult = (id: number) => {
  router.push(`/activity/survey/${id}/result`)
}

const handlePublish = async (id: number) => {
  if (!confirm('确认发布此问卷吗？发布后用户即可填写。')) return
  try {
    await publishSurvey(id)
    alert('发布成功！')
    loadSurveys()
  } catch (error: any) {
    alert(error.response?.data?.message || '发布失败，请重试')
  }
}

const handleClose = async (id: number) => {
  if (!confirm('确认结束此问卷吗？结束后用户将无法继续填写。')) return
  try {
    await closeSurvey(id)
    alert('已结束！')
    loadSurveys()
  } catch (error: any) {
    alert(error.response?.data?.message || '操作失败，请重试')
  }
}

const handleDelete = async (id: number) => {
  if (!confirm('确定要删除此问卷吗？此操作不可撤销！')) return
  try {
    await deleteSurvey(id)
    alert('删除成功！')
    loadSurveys()
  } catch (error: any) {
    alert(error.response?.data?.message || '删除失败，请重试')
  }
}

onMounted(loadSurveys)
</script>

<style scoped>
.manage-container {
  max-width: 1200px;
  margin: 0 auto;
  padding: 20px 0;
}

.manage-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.manage-header h2 {
  font-size: 1.5rem;
  font-weight: 700;
  color: var(--text-primary);
}

.manage-header h2 i {
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
}

.btn-create:hover {
  background: var(--info);
  transform: translateY(-2px);
}

.stats-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 16px;
  margin-bottom: 24px;
}

.stat-card {
  background: var(--paper-card);
  border-radius: 0;
  padding: 16px 20px;
  border: 1px solid var(--border-line);
  text-align: center;
}

.stat-number {
  font-size: 1.8rem;
  font-weight: 700;
  color: var(--text-primary);
  display: block;
}

.stat-label {
  font-size: 0.8rem;
  color: var(--text-secondary);
}

.filter-tabs {
  display: flex;
  gap: 8px;
  margin-bottom: 20px;
  flex-wrap: wrap;
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

.table-wrapper {
  background: var(--paper-card);
  border-radius: 0;
  border: 1px solid var(--border-line);
  overflow: hidden;
}

.survey-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

.survey-table th {
  background: var(--paper-card);
  text-align: left;
  padding: 12px 16px;
  font-weight: 600;
  color: var(--text-primary);
  border-bottom: 1px solid var(--border-line);
}

.survey-table td {
  padding: 12px 16px;
  border-bottom: 1px solid var(--border-line);
  vertical-align: middle;
}

.survey-table tr:hover td {
  background: var(--paper-card);
}

.survey-title {
  font-weight: 500;
  color: var(--text-primary);
}

.survey-desc {
  font-size: 0.75rem;
  color: var(--text-secondary);
  display: block;
}

.status-badge {
  padding: 2px 12px;
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

.action-buttons {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
}

.action-buttons button {
  width: 32px;
  height: 32px;
  border-radius: 0;
  border: none;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
}

.btn-edit {
  background: var(--bg-sub);
  color: var(--info);
}
.btn-edit:hover { background: var(--info-soft); }

.btn-publish {
  background: var(--success-soft);
  color: var(--success);
}
.btn-publish:hover { background: var(--success); color: var(--on-accent); }

.btn-delete {
  background: var(--bg-main);
  color: var(--danger);
}
.btn-delete:hover { background: var(--bg-sub); }

.btn-result {
  background: var(--warning-soft);
  color: var(--warning);
}
.btn-result:hover { background: var(--warning); color: var(--on-accent); }

.btn-close {
  background: var(--warning-soft);
  color: var(--warning);
}
.btn-close:hover { background: var(--warning); color: var(--on-accent); }

.loading-state,
.empty-state {
  text-align: center;
  padding: 60px 20px;
  color: var(--text-secondary);
}
.loading-state i,
.empty-state i {
  font-size: 2rem;
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

.pagination-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  border-top: 1px solid var(--border-line);
  background: var(--paper-card);
  border-radius: 0;
}

.page-info {
  font-size: 0.8rem;
  color: var(--text-secondary);
}

.page-controls {
  display: flex;
  align-items: center;
  gap: 12px;
}

.btn-page {
  background: var(--paper-card);
  border: 1px solid var(--border-line);
  padding: 6px 14px;
  font-size: 0.8rem;
  border-radius: 0;
  cursor: pointer;
  transition: 0.2s;
}

.btn-page:not(:disabled):hover {
  border-color: var(--border-line);
  color: var(--text-primary);
}

.btn-page:disabled {
  opacity: 0.4;
  cursor: not-allowed;
  background: var(--bg-main);
}

.current-page {
  font-size: 0.85rem;
  font-family: monospace;
  font-weight: 600;
  color: var(--text-primary);
}

@media (max-width: 768px) {
  .table-wrapper {
    overflow-x: auto;
  }
  .stats-row {
    grid-template-columns: repeat(2, 1fr);
  }
  .action-buttons {
    flex-wrap: wrap;
  }
}
</style>
