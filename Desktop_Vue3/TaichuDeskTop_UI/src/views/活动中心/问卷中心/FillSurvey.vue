<template>
  <div class="fill-survey-container" v-if="!loading && survey">
    <!-- 已提交状态 -->
    <div v-if="survey.hasSubmitted" class="submitted-state">
      <i class="fas fa-check-circle"></i>
      <h2>您已提交过此问卷</h2>
      <p>感谢您的参与！</p>
      <button class="btn-back" @click="$router.push('/activity/survey')">
        返回问卷列表
      </button>
    </div>

    <!-- 问卷内容 -->
    <div v-else>
      <div class="survey-header">
        <h1>{{ survey.title }}</h1>
        <p class="desc">{{ survey.description }}</p>
        <div class="progress-info">
          <span>进度：{{ answeredCount }} / {{ survey.questions.length }}</span>
          <div class="progress-bar">
            <div class="progress-fill" :style="{ width: progressPercent + '%' }"></div>
          </div>
        </div>
      </div>

      <div class="questions-area">
        <div
          v-for="(question, index) in survey.questions"
          :key="question.id"
          class="question-item"
          :class="{ required: question.isRequired }"
        >
          <div class="question-header">
            <span class="q-number">{{ Number(index) + 1 }}.</span>
            <span class="q-title">{{ question.title }}</span>
            <span v-if="question.isRequired" class="required-badge">必填</span>
          </div>
          <p v-if="question.description" class="q-desc">{{ question.description }}</p>

          <!-- 单选题 -->
          <div v-if="question.questionType === 1" class="options-group">
            <label
              v-for="option in question.options"
              :key="option.id"
              class="option-radio"
            >
              <input
                type="radio"
                :name="`q_${question.id}`"
                :value="option.id"
                v-model="answers[question.id]"
              />
              <span>{{ option.optionText }}</span>
            </label>
          </div>

          <!-- 多选题 -->
          <div v-else-if="question.questionType === 2" class="options-group">
            <label
              v-for="option in question.options"
              :key="option.id"
              class="option-checkbox"
            >
              <input
                type="checkbox"
                :value="option.id"
                v-model="multiAnswers[question.id]"
              />
              <span>{{ option.optionText }}</span>
            </label>
          </div>

          <!-- 填空题 -->
          <div v-else-if="question.questionType === 3" class="text-area">
            <textarea
              v-model="textAnswers[question.id]"
              placeholder="请输入你的回答..."
              rows="3"
            ></textarea>
          </div>

          <!-- 评分题 -->
          <div v-else-if="question.questionType === 4" class="rating-group">
            <button
              v-for="star in 5"
              :key="star"
              class="star-btn"
              @click="ratingAnswers[question.id] = star"
            >
              <i
                class="fas fa-star"
                :class="{ active: star <= (Number(ratingAnswers[question.id]) || 0) }"
              ></i>
            </button>
            <span class="rating-label">
              {{ Number(ratingAnswers[question.id]) || 0 }} / 5 分
            </span>
          </div>

          <!-- 排序题 -->
          <div v-else-if="question.questionType === 5" class="sort-group">
            <div
              v-for="(option, idx) in getSortOptions(question.id)"
              :key="option.id"
              class="sort-item"
              draggable="true"
              @dragstart="onDragStart($event, question.id, Number(idx))"
              @dragover.prevent
              @drop="onDrop($event, question.id, Number(idx))"
            >
              <i class="fas fa-grip-lines"></i>
              <span>{{ option.optionText }}</span>
              <span class="sort-rank">{{ Number(idx) + 1 }}</span>
            </div>
          </div>

          <!-- 矩阵题 -->
          <div v-else-if="question.questionType === 6" class="matrix-group">
            <div class="matrix-table">
              <table>
                <thead>
                  <tr>
                    <th></th>
                    <th v-for="col in getMatrixCols(question)" :key="col">
                      {{ col }}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="row in getMatrixRows(question)" :key="row">
                    <td class="matrix-row-label">{{ row }}</td>
                    <td v-for="col in getMatrixCols(question)" :key="col">
                      <input
                        type="radio"
                        :name="`matrix_${question.id}_${row}`"
                        :value="col"
                        @change="onMatrixChange(question.id, row, col)"
                      />
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>

      <div class="submit-area">
        <button class="btn-submit" @click="submitSurvey" :disabled="submitting">
          {{ submitting ? '提交中...' : '提交问卷' }}
        </button>
      </div>
    </div>
  </div>

  <div v-else-if="loading" class="loading-state">
    <i class="fas fa-spinner fa-spin"></i> 加载中...
  </div>
  <div v-else class="error-state">
    <i class="fas fa-exclamation-circle"></i>
    <p>问卷不存在或已被删除</p>
    <button class="btn-back" @click="$router.push('/activity/survey')">
      返回问卷列表
    </button>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getSurveyFill, submitSurvey as submitSurveyApi, type SurveyFillResponse, type QuestionFillDto, type OptionDto } from './api'

const route = useRoute()
const router = useRouter()

const survey = ref<SurveyFillResponse | null>(null)
const loading = ref(false)
const submitting = ref(false)

// 答案存储
const answers = ref<Record<number, number>>({}) // 单选
const multiAnswers = ref<Record<number, number[]>>({}) // 多选
const textAnswers = ref<Record<number, string>>({}) // 填空
const ratingAnswers = ref<Record<number, number>>({}) // 评分
const sortAnswers = ref<Record<number, number[]>>({}) // 排序
const matrixAnswers = ref<Record<number, Record<string, string>>>({}) // 矩阵题：{ questionId: { row: col } }

const surveyId = computed(() => Number(route.params.id))

// ===== 计算属性 =====
const answeredCount = computed(() => {
  if (!survey.value) return 0
  let count = 0
  for (const q of survey.value.questions) {
    if (isQuestionAnswered(q)) count++
  }
  return count
})

const progressPercent = computed(() => {
  if (!survey.value || survey.value.questions.length === 0) return 0
  return Math.round((Number(answeredCount.value) / survey.value.questions.length) * 100)
})

// ===== 判断题目是否已回答 =====
const isQuestionAnswered = (q: QuestionFillDto): boolean => {
  switch (q.questionType) {
    case 1:
      return answers.value[q.id] !== undefined
    case 2:
      return (multiAnswers.value[q.id] || []).length > 0
    case 3:
      return (textAnswers.value[q.id] || '').trim().length > 0
    case 4:
      return (Number(ratingAnswers.value[q.id]) || 0) > 0
    case 5:
      return (sortAnswers.value[q.id] || []).length === q.options.length
    case 6:
      if (!matrixAnswers.value[q.id]) return false
      const rows = getMatrixRows(q)
      return rows.every(row => matrixAnswers.value[q.id]?.[row] !== undefined)
    default:
      return false
  }
}

// ===== 矩阵题辅助方法 =====
const getMatrixRows = (question: QuestionFillDto): string[] => {
  if (!question.config) return []
  try {
    const config = JSON.parse(question.config)
    return config.rows || []
  } catch {
    return []
  }
}

const getMatrixCols = (question: QuestionFillDto): string[] => {
  if (!question.config) return []
  try {
    const config = JSON.parse(question.config)
    return config.cols || []
  } catch {
    return []
  }
}

const onMatrixChange = (questionId: number, row: string, col: string) => {
  if (!matrixAnswers.value[questionId]) {
    matrixAnswers.value[questionId] = {}
  }
  matrixAnswers.value[questionId][row] = col
}

// ===== 排序题 =====
const getSortOptions = (questionId: number): OptionDto[] => {
  if (!survey.value) return []
  const q = survey.value.questions.find(q => q.id === questionId)
  if (!q) return []
  const currentOrder = sortAnswers.value[questionId] || q.options.map(o => o.id)
  return currentOrder
    .map(id => q.options.find(o => o.id === id))
    .filter((item): item is OptionDto => item !== undefined)
}

let dragData: { questionId: number; fromIndex: number } | null = null

const onDragStart = (e: DragEvent, questionId: number, index: number) => {
  dragData = { questionId, fromIndex: index }
  if (e.dataTransfer) {
    e.dataTransfer.effectAllowed = 'move'
  }
}

const onDrop = (e: DragEvent, questionId: number, toIndex: number) => {
  if (!dragData || dragData.questionId !== questionId) return
  const { fromIndex } = dragData
  if (fromIndex === toIndex) return

  if (!survey.value) return
  const q = survey.value.questions.find(q => q.id === questionId)
  if (!q) return

  const currentOrder = sortAnswers.value[questionId] || q.options.map(o => o.id)
  const [removed] = currentOrder.splice(fromIndex, 1)
  currentOrder.splice(toIndex, 0, removed)
  sortAnswers.value[questionId] = currentOrder
  dragData = null
}

// ===== 加载问卷 =====
const fetchSurvey = async () => {
  loading.value = true
  try {
    const res = await getSurveyFill(surveyId.value)
    survey.value = res

    // 初始化所有答案结构
    if (res?.questions) {
      res.questions.forEach((q: QuestionFillDto) => {
        // 多选 → 初始化为数组
        if (q.questionType === 2) {
          multiAnswers.value[q.id] = []
        }
        // 矩阵 → 初始化为对象
        if (q.questionType === 6 && q.config) {
          matrixAnswers.value[q.id] = {}
        }
      })
    }
  } catch (error: any) {
    console.error('获取问卷失败:', error)
    alert(error.response?.data?.message || '加载问卷失败')
    router.push('/activity/survey')
  } finally {
    loading.value = false
  }
}

// ===== 提交问卷 =====
const submitSurvey = async () => {
  if (!survey.value) return

  // 校验必填
  for (const q of survey.value.questions) {
    if (q.isRequired && !isQuestionAnswered(q)) {
      alert(`请回答必答题：${q.title}`)
      return
    }
  }

  if (!confirm('确认提交问卷吗？提交后不可修改。')) return

  submitting.value = true
  try {
    // ✅ 方法一：显式声明 q 的类型
    const answersData = survey.value.questions.map((q: QuestionFillDto) => {
      const answer: any = { questionId: q.id }

      switch (q.questionType) {
        case 1:
          answer.selectedOptionIds = answers.value[q.id] ? [answers.value[q.id]] : []
          break
        case 2:
          answer.selectedOptionIds = multiAnswers.value[q.id] || []
          break
        case 3:
          answer.answerText = textAnswers.value[q.id] || ''
          break
        case 4:
          answer.answerText = String(ratingAnswers.value[q.id] || 0)
          break
        case 5:
          answer.sortResult = sortAnswers.value[q.id] || []
          break
        case 6:
          answer.matrixResult = matrixAnswers.value[q.id] || {}
          break
      }
      return answer
    })

   // 提交成功后，跳转到结果页
    const response = await submitSurveyApi(surveyId.value, { answers: answersData })
    alert('提交成功！感谢您的参与！')

    // 如果问卷公开，跳转到结果页
    if (response.canViewResult) {
    router.push(`/activity/survey/${surveyId.value}/result`)
    } else {
    router.push('/activity/survey')
    }



   
  } catch (error: any) {
    alert(error.response?.data?.message || '提交失败，请重试')
  } finally {
    submitting.value = false
  }
}

onMounted(fetchSurvey)
</script>

<style scoped>
/* 保留你原有的所有样式，新增矩阵样式 */

.fill-survey-container {
  max-width: 800px;
  margin: 0 auto;
  padding: 20px 0;
}

.submitted-state {
  text-align: center;
  padding: 60px 20px;
  background: #fff;
  border-radius: 12px;
  border: 1px solid #e5e7eb;
}

.submitted-state i {
  font-size: 4rem;
  color: #10b981;
  display: block;
  margin-bottom: 16px;
}

.submitted-state h2 {
  font-size: 1.5rem;
  color: #1f2937;
}

.submitted-state p {
  color: #6b7280;
  margin-bottom: 20px;
}

.btn-back {
  padding: 10px 28px;
  border: none;
  border-radius: 8px;
  background: #6366f1;
  color: #fff;
  font-weight: 500;
  cursor: pointer;
}

.survey-header {
  background: #fff;
  border-radius: 12px;
  padding: 28px 32px;
  border: 1px solid #eee;
  margin-bottom: 24px;
}

.survey-header h1 {
  font-size: 1.5rem;
  font-weight: 700;
  color: #1f2937;
  margin: 0 0 8px;
}

.survey-header .desc {
  color: #6b7280;
  margin: 0 0 16px;
}

.progress-info {
  display: flex;
  align-items: center;
  gap: 14px;
  font-size: 0.85rem;
  color: #6b7280;
}

.progress-bar {
  flex: 1;
  height: 6px;
  background: #f0f0f0;
  border-radius: 3px;
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background: #6366f1;
  border-radius: 3px;
  transition: width 0.3s;
}

.questions-area {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.question-item {
  background: #fff;
  border-radius: 12px;
  padding: 20px 24px;
  border: 1px solid #eee;
}

.question-item.required {
  border-left: 3px solid #ef4444;
}

.question-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
}

.q-number {
  font-weight: 600;
  color: #6366f1;
}

.q-title {
  font-weight: 500;
  color: #1f2937;
}

.required-badge {
  font-size: 0.65rem;
  color: #ef4444;
  background: #fef2f2;
  padding: 2px 10px;
  border-radius: 12px;
}

.q-desc {
  font-size: 0.85rem;
  color: #9ca3af;
  margin: 0 0 12px;
}

.options-group {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.option-radio,
.option-checkbox {
  display: flex;
  align-items: center;
  gap: 10px;
  cursor: pointer;
  padding: 6px 0;
}

.option-radio input,
.option-checkbox input {
  width: 18px;
  height: 18px;
  accent-color: #6366f1;
}

.text-area textarea {
  width: 100%;
  padding: 10px 14px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  font-size: 0.95rem;
  font-family: inherit;
  resize: vertical;
}

.text-area textarea:focus {
  outline: none;
  border-color: #6366f1;
}

.rating-group {
  display: flex;
  gap: 8px;
  align-items: center;
}

.star-btn {
  background: none;
  border: none;
  font-size: 1.8rem;
  cursor: pointer;
  color: #d1d5db;
  transition: color 0.2s;
}

.star-btn i.active {
  color: #f59e0b;
}

.rating-label {
  margin-left: 12px;
  font-size: 0.9rem;
  color: #6b7280;
}

.sort-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.sort-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 14px;
  background: #f9fafb;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  cursor: grab;
  transition: all 0.2s;
}

.sort-item:hover {
  background: #f3f4f6;
}

.sort-item i {
  color: #9ca3af;
  cursor: grab;
}

.sort-rank {
  margin-left: auto;
  background: #e5e7eb;
  padding: 2px 10px;
  border-radius: 12px;
  font-size: 0.75rem;
  color: #6b7280;
}

/* ===== 新增：矩阵题样式 ===== */
.matrix-group {
  overflow-x: auto;
}

.matrix-table {
  width: 100%;
}

.matrix-table table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
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

.matrix-row-label {
  font-weight: 500;
  color: #374151;
  text-align: left !important;
}

.matrix-table input[type="radio"] {
  width: 16px;
  height: 16px;
  accent-color: #6366f1;
  cursor: pointer;
}

.submit-area {
  margin-top: 24px;
  text-align: center;
}

.btn-submit {
  padding: 12px 48px;
  border: none;
  border-radius: 8px;
  background: linear-gradient(135deg, #6366f1, #8b5cf6);
  color: #fff;
  font-size: 1rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-submit:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 8px 25px rgba(99, 102, 241, 0.3);
}

.btn-submit:disabled {
  opacity: 0.6;
  cursor: not-allowed;
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

.error-state {
  text-align: center;
  padding: 60px 20px;
  color: #9ca3af;
}

.error-state i {
  font-size: 3rem;
  color: #ef4444;
  display: block;
  margin-bottom: 16px;
}

.error-state p {
  margin-bottom: 20px;
}

@media (max-width: 768px) {
  .survey-header {
    padding: 20px;
  }
  .question-item {
    padding: 16px;
  }
  .matrix-table {
    font-size: 0.75rem;
  }
  .matrix-table th,
  .matrix-table td {
    padding: 4px 6px;
  }
}
</style>