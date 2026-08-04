<template>
  <div class="survey-result-container" v-if="!loading">
    <div class="result-header">
      <button class="btn-back" @click="$router.push('/activity/survey')">
        <i class="fas fa-arrow-left"></i> 返回列表
      </button>
      <h1>{{ surveyTitle }}</h1>
      <div class="stats-overview">
        <div class="stat-item">
          <span class="stat-number">{{ totalSubmissions }}</span>
          <span class="stat-label">总提交数</span>
        </div>
        <div class="stat-item">
          <span class="stat-number">{{ avgCompletionTime }}s</span>
          <span class="stat-label">平均完成时间</span>
        </div>
        <div class="stat-item">
          <span class="stat-number">{{ questionStats.length }}</span>
          <span class="stat-label">题目数</span>
        </div>
      </div>
    </div>

    <div class="questions-stats">
      <div
        v-for="(qStat, index) in questionStats"
        :key="qStat.questionId"
        class="question-stat-card"
      >
        <h3>
          <span class="q-number">{{ Number(index) + 1 }}.</span>
          {{ qStat.title }}
          <span class="q-type">{{ getTypeLabel(qStat.questionType) }}</span>
        </h3>
        <p class="q-meta">
          已答 {{ qStat.totalAnswers }} 人
          <span v-if="qStat.skipCount > 0">| 跳过 {{ qStat.skipCount }} 人</span>
        </p>

        <!-- 选择题：柱状图 -->
        <div v-if="[1, 2].includes(qStat.questionType)" class="chart-area">
          <div
            v-for="opt in qStat.optionStats"
            :key="opt.optionId"
            class="bar-item"
          >
            <span class="bar-label">{{ opt.optionText }}</span>
            <div class="bar-track">
              <div
                class="bar-fill"
                :style="{ width: opt.percentage + '%' }"
              ></div>
            </div>
            <span class="bar-value">{{ opt.count }} 票 ({{ opt.percentage }}%)</span>
          </div>
        </div>

        <!-- 评分题 -->
        <div v-else-if="qStat.questionType === 4" class="rating-stats">
          <div class="avg-score">
            <span class="big-number">{{ qStat.averageScore }}</span>
            <span class="avg-label">平均分</span>
          </div>
          <div class="score-distribution">
            <span
              v-for="(count, score) in qStat.scoreDistribution"
              :key="score"
              class="score-bar"
            >
              <span class="score-label">{{ score }} 分</span>
              <div class="score-track">
                <div
                  class="score-fill"
                  :style="{ width: (Number(count) / totalSubmissions * 100) + '%' }"
                ></div>
              </div>
              <span class="score-count">{{ count }} 人</span>
            </span>
          </div>
        </div>

        <!-- 填空题 -->
        <div v-else-if="qStat.questionType === 3" class="text-answers">
          <div v-if="qStat.textAnswers && qStat.textAnswers.length > 0">
            <div
              v-for="(text, idx) in qStat.textAnswers"
              :key="idx"
              class="text-item"
            >
              <span class="text-index">{{ Number(idx) + 1 }}.</span>
              <span>{{ text }}</span>
            </div>
          </div>
          <div v-else class="empty-text">
            暂无回答
          </div>
        </div>

        <!-- 排序题 -->
        <div v-else-if="qStat.questionType === 5" class="rank-stats">
          <div
            v-for="(avgRank, optId) in qStat.avgRank"
            :key="optId"
            class="rank-item"
          >
            <span class="rank-label">{{ getOptionText(Number(optId)) }}</span>
            <div class="rank-track">
              <div
                class="rank-fill"
                :style="{ width: (Number(avgRank) / getMaxRank() * 100) + '%' }"
              ></div>
            </div>
            <span class="rank-value">平均排名 {{ avgRank }}</span>
          </div>
        </div>

        <!-- 矩阵题 -->
        <div v-else-if="qStat.questionType === 6" class="matrix-stats">
          <table class="matrix-table">
            <thead>
              <tr>
                <th></th>
                <th v-for="col in getMatrixCols(qStat)" :key="col">
                  {{ col }}
                </th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in getMatrixRows(qStat)" :key="row">
                <td class="matrix-label">{{ row }}</td>
                <td
                  v-for="col in getMatrixCols(qStat)"
                  :key="col"
                  class="matrix-cell"
                >
                  {{ getMatrixValue(qStat, row, col) }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  </div>

  <div v-else class="loading-state">
    <i class="fas fa-spinner fa-spin"></i> 加载中...
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getSurveyStats } from './api'

const route = useRoute()
const router = useRouter()

const loading = ref(false)
const surveyTitle = ref('')
const totalSubmissions = ref(0)
const avgCompletionTime = ref(0)
const questionStats = ref<any[]>([])

const surveyId = computed(() => Number(route.params.id))

const typeLabels: Record<number, string> = {
  1: '单选',
  2: '多选',
  3: '填空',
  4: '评分',
  5: '排序',
  6: '矩阵',
}

const getTypeLabel = (type: number) => typeLabels[type] || '未知'

// 获取选项文本（需要从问题中查找）
const getOptionText = (optionId: number) => {
  for (const q of questionStats.value) {
    if (q.optionStats) {
      const found = q.optionStats.find((o: any) => Number(o.optionId) === optionId)
      if (found) return found.optionText
    }
  }
  return `选项 ${optionId}`
}

// 获取最大排名（用于归一化显示）
const getMaxRank = () => {
  let max = 1
  for (const q of questionStats.value) {
    if (q.avgRank) {
      for (const rank of Object.values(q.avgRank) as number[]) {
        if (Number(rank) > max) max = Number(rank)
      }
    }
  }
  return max
}

// 矩阵题辅助
const getMatrixRows = (qStat: any) => {
  if (!qStat.matrixAverages) return []
  return Object.keys(qStat.matrixAverages)
    .map((key: string) => key.split('_')[0])
    .filter((v, i, a) => a.indexOf(v) === i)
}

const getMatrixCols = (qStat: any) => {
  if (!qStat.matrixAverages) return []
  return Object.keys(qStat.matrixAverages)
    .map((key: string) => key.split('_')[1])
    .filter((v, i, a) => a.indexOf(v) === i)
}

const getMatrixValue = (qStat: any, row: string, col: string) => {
  const key = `${row}_${col}`
  const val = qStat.matrixAverages?.[key]
  return val !== undefined ? Number(val).toFixed(1) : '-'
}

const fetchStats = async () => {
  loading.value = true
  try {
    const res = await getSurveyStats(surveyId.value)
    surveyTitle.value = res.title || '问卷统计'
    totalSubmissions.value = res.totalSubmissions || 0
    avgCompletionTime.value = res.avgCompletionTime || 0
    questionStats.value = res.questionStats || []
  } catch (error: any) {
    console.error('获取统计失败:', error)
    alert(error.response?.data?.message || '加载统计失败')
    router.push('/activity/survey')
  } finally {
    loading.value = false
  }
}

onMounted(fetchStats)
</script>

<style scoped>
/* 样式保持不变，与之前相同，省略重复内容以节省篇幅 */
.survey-result-container {
  max-width: 900px;
  margin: 0 auto;
  padding: 20px 0;
}

.result-header {
  background: #fff;
  border-radius: 12px;
  padding: 24px 32px;
  border: 1px solid #eee;
  margin-bottom: 24px;
}

.btn-back {
  background: none;
  border: none;
  color: #6b7280;
  cursor: pointer;
  font-size: 0.9rem;
  margin-bottom: 12px;
}

.btn-back:hover {
  color: #1f2937;
}

.result-header h1 {
  font-size: 1.5rem;
  font-weight: 700;
  color: #1f2937;
  margin: 0 0 16px;
}

.stats-overview {
  display: flex;
  gap: 40px;
}

.stat-item {
  display: flex;
  flex-direction: column;
  align-items: center;
}

.stat-number {
  font-size: 1.8rem;
  font-weight: 700;
  color: #6366f1;
}

.stat-label {
  font-size: 0.8rem;
  color: #9ca3af;
}

.questions-stats {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.question-stat-card {
  background: #fff;
  border-radius: 12px;
  padding: 20px 24px;
  border: 1px solid #eee;
}

.question-stat-card h3 {
  font-size: 1rem;
  font-weight: 600;
  color: #1f2937;
  margin: 0 0 4px;
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.q-number {
  color: #6366f1;
}

.q-type {
  font-size: 0.7rem;
  background: #e5e7eb;
  color: #6b7280;
  padding: 2px 10px;
  border-radius: 12px;
  font-weight: 400;
}

.q-meta {
  font-size: 0.8rem;
  color: #9ca3af;
  margin: 0 0 16px;
}

.chart-area {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.bar-item {
  display: flex;
  align-items: center;
  gap: 12px;
}

.bar-label {
  min-width: 80px;
  font-size: 0.85rem;
  color: #374151;
}

.bar-track {
  flex: 1;
  height: 24px;
  background: #f3f4f6;
  border-radius: 4px;
  overflow: hidden;
}

.bar-fill {
  height: 100%;
  background: linear-gradient(90deg, #6366f1, #8b5cf6);
  border-radius: 4px;
  transition: width 0.6s ease;
}

.bar-value {
  min-width: 100px;
  font-size: 0.8rem;
  color: #6b7280;
  text-align: right;
}

.rating-stats {
  display: flex;
  gap: 32px;
  align-items: flex-start;
}

.avg-score {
  display: flex;
  flex-direction: column;
  align-items: center;
  min-width: 80px;
}

.big-number {
  font-size: 2.5rem;
  font-weight: 700;
  color: #f59e0b;
}

.avg-label {
  font-size: 0.8rem;
  color: #9ca3af;
}

.score-distribution {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.score-bar {
  display: flex;
  align-items: center;
  gap: 10px;
}

.score-label {
  min-width: 40px;
  font-size: 0.8rem;
  color: #6b7280;
}

.score-track {
  flex: 1;
  height: 16px;
  background: #f3f4f6;
  border-radius: 4px;
  overflow: hidden;
}

.score-fill {
  height: 100%;
  background: #10b981;
  border-radius: 4px;
  transition: width 0.6s ease;
}

.score-count {
  min-width: 40px;
  font-size: 0.75rem;
  color: #9ca3af;
}

.text-answers {
  display: flex;
  flex-direction: column;
  gap: 4px;
  max-height: 200px;
  overflow-y: auto;
}

.text-item {
  font-size: 0.9rem;
  color: #374151;
  padding: 4px 0;
  border-bottom: 1px solid #f3f4f6;
}

.text-index {
  color: #9ca3af;
  margin-right: 8px;
}

.empty-text {
  color: #9ca3af;
  font-size: 0.9rem;
  padding: 12px 0;
}

.rank-stats {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.rank-item {
  display: flex;
  align-items: center;
  gap: 12px;
}

.rank-label {
  min-width: 100px;
  font-size: 0.85rem;
  color: #374151;
}

.rank-track {
  flex: 1;
  height: 20px;
  background: #f3f4f6;
  border-radius: 4px;
  overflow: hidden;
}

.rank-fill {
  height: 100%;
  background: linear-gradient(90deg, #10b981, #34d399);
  border-radius: 4px;
  transition: width 0.6s ease;
}

.rank-value {
  min-width: 100px;
  font-size: 0.8rem;
  color: #6b7280;
  text-align: right;
}

.matrix-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.85rem;
}

.matrix-table th,
.matrix-table td {
  padding: 8px 12px;
  border: 1px solid #e5e7eb;
  text-align: center;
}

.matrix-table thead th {
  background: #f9fafb;
  font-weight: 600;
  color: #374151;
}

.matrix-label {
  font-weight: 500;
  color: #374151;
  text-align: left !important;
}

.matrix-cell {
  color: #1f2937;
  font-weight: 500;
}

.loading-state {
  text-align: center;
  padding: 60px;
  color: #9ca3af;
}

.loading-state i {
  font-size: 2rem;
  display: block;
  margin-bottom: 12px;
}

@media (max-width: 768px) {
  .result-header {
    padding: 16px;
  }
  .stats-overview {
    gap: 20px;
    flex-wrap: wrap;
  }
  .question-stat-card {
    padding: 16px;
  }
  .bar-item {
    flex-wrap: wrap;
  }
  .bar-label {
    min-width: 60px;
  }
  .rating-stats {
    flex-direction: column;
  }
}
</style>