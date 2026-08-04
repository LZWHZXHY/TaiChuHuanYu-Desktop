<template>
  <div class="survey-editor-container">
    <div class="form-card">
      <div class="form-header">
        <h2>
          <i :class="isEditMode ? 'fas fa-edit' : 'fas fa-plus-circle'"></i>
          {{ isEditMode ? '编辑问卷' : '创建问卷' }}
        </h2>
        <p class="subtitle">
          {{ isEditMode ? '修改问卷内容，保存后将更新草稿' : '创建一份新的问卷调查，支持多种题型' }}
        </p>
      </div>

      <!-- 基本信息 -->
      <div class="section">
        <div class="section-title">基本信息</div>
        <div class="form-group">
          <label>问卷标题 <span class="required">*</span></label>
          <input
            type="text"
            v-model="form.title"
            placeholder="请输入问卷标题"
            maxlength="200"
          />
        </div>
        <div class="form-group">
          <label>问卷描述</label>
          <textarea
            v-model="form.description"
            placeholder="请输入问卷描述（可选）"
            rows="2"
            maxlength="1000"
          ></textarea>
        </div>
        <div class="form-row">
          <div class="form-group">
            <label>开始时间 <span class="required">*</span></label>
            <input
              type="datetime-local"
              v-model="form.startTime"
              :min="minDateTime"
            />
          </div>
          <div class="form-group">
            <label>结束时间 <span class="required">*</span></label>
            <input
              type="datetime-local"
              v-model="form.endTime"
              :min="form.startTime || minDateTime"
            />
          </div>
        </div>
        <div class="form-row">
          <div class="form-group">
            <label class="checkbox-label">
              <input type="checkbox" v-model="form.isPublic" />
              公开结果（用户提交后可查看统计）
            </label>
          </div>
          <div class="form-group">
            <label class="checkbox-label">
              <input type="checkbox" v-model="form.allowAnonymous" />
              允许匿名提交
            </label>
          </div>
        </div>
      </div>

      <!-- 题目列表 -->
      <div class="section">
        <div class="section-title">
          题目列表
          <span class="question-count">{{ questions.length }} 题</span>
        </div>

        <div v-if="questions.length === 0" class="empty-questions">
          <i class="fas fa-file-alt"></i>
          <p>还没有题目，点击下方按钮添加</p>
        </div>

        <draggable
          v-model="questions"
          item-key="id"
          handle=".drag-handle"
          class="question-list"
          :animation="200"
        >
          <template #item="{ element, index }">
            <div class="question-item">
              <div class="question-header">
                <div class="drag-handle">
                  <i class="fas fa-grip-lines"></i>
                </div>
                <span class="q-number">#{{ Number(index) + 1 }}</span>
                <span class="q-type">{{ getTypeLabel(element.questionType) }}</span>
                <input
                  v-model="element.title"
                  placeholder="输入题目内容..."
                  class="q-title-input"
                />
                <div class="q-actions">
                  <label class="required-toggle">
                    <input type="checkbox" v-model="element.isRequired" />
                    必填
                  </label>
                  <button
                    class="btn-copy"
                    @click="copyQuestion(index)"
                    title="复制题目"
                  >
                    <i class="fas fa-copy"></i>
                  </button>
                  <button
                    class="btn-delete"
                    @click="deleteQuestion(index)"
                    title="删除题目"
                  >
                    <i class="fas fa-trash"></i>
                  </button>
                </div>
              </div>

              <input
                v-model="element.description"
                placeholder="题目补充说明（可选）"
                class="q-desc-input"
              />

              <div class="question-body">
                <!-- 单选/多选 -->
                <template v-if="element.questionType === 1 || element.questionType === 2">
                  <div class="options-list">
                    <div
                      v-for="(opt, oi) in element.options"
                      :key="oi"
                      class="option-item"
                    >
                      <span class="option-label">{{ String.fromCharCode(65 + Number(oi)) }}</span>
                      <input
                        v-model="opt.optionText"
                        placeholder="输入选项内容"
                        class="option-input"
                      />
                      <button
                        class="btn-remove-option"
                        @click="removeOption(index, Number(oi))"
                        :disabled="element.options.length <= 2"
                      >
                        <i class="fas fa-times"></i>
                      </button>
                    </div>
                  </div>
                  <button
                    class="btn-add-option"
                    @click="addOption(index)"
                    :disabled="element.options.length >= 10"
                  >
                    <i class="fas fa-plus"></i> 添加选项（{{ element.options.length }}/10）
                  </button>
                </template>

                <!-- 填空 -->
                <template v-else-if="element.questionType === 3">
                  <div class="fill-hint">
                    <i class="fas fa-info-circle"></i>
                    填空题，用户将自由输入文本
                  </div>
                </template>

                <!-- 评分 -->
                <template v-else-if="element.questionType === 4">
                  <div class="rating-config">
                    <label>评分范围</label>
                    <div class="rating-range">
                      <input
                        type="number"
                        v-model.number="element.config.minScore"
                        placeholder="1"
                        min="1"
                        max="9"
                      />
                      <span>到</span>
                      <input
                        type="number"
                        v-model.number="element.config.maxScore"
                        placeholder="5"
                        min="2"
                        max="10"
                      />
                      <span>星</span>
                    </div>
                    <div class="rating-preview">
                      <i
                        v-for="star in (Number(element.config.maxScore) || 5)"
                        :key="star"
                        class="fas fa-star"
                        :class="{ active: star <= (Number(element.config.maxScore) || 5) }"
                      ></i>
                      <span class="preview-label">
                        {{ Number(element.config.minScore) || 1 }} ~ {{ Number(element.config.maxScore) || 5 }} 星
                      </span>
                    </div>
                  </div>
                </template>

                <!-- 排序 -->
                <template v-else-if="element.questionType === 5">
                  <div class="options-list sort-options">
                    <div
                      v-for="(opt, oi) in element.options"
                      :key="oi"
                      class="option-item sort-item"
                    >
                      <i class="fas fa-grip-vertical sort-icon"></i>
                      <span class="option-label">{{ String.fromCharCode(65 + Number(oi)) }}</span>
                      <input
                        v-model="opt.optionText"
                        placeholder="输入选项内容"
                        class="option-input"
                      />
                      <button
                        class="btn-remove-option"
                        @click="removeOption(index, Number(oi))"
                        :disabled="element.options.length <= 2"
                      >
                        <i class="fas fa-times"></i>
                      </button>
                    </div>
                  </div>
                  <button
                    class="btn-add-option"
                    @click="addOption(index)"
                    :disabled="element.options.length >= 10"
                  >
                    <i class="fas fa-plus"></i> 添加选项（{{ element.options.length }}/10）
                  </button>
                </template>

                <!-- 矩阵 -->
                <template v-else-if="element.questionType === 6">
                  <div class="matrix-config">
                    <div class="matrix-section">
                      <label>行（评价维度）</label>
                      <div class="matrix-items">
                        <div
                          v-for="(row, ri) in element.config.rows"
                          :key="ri"
                          class="matrix-item"
                        >
                          <input
                            v-model="element.config.rows[ri]"
                            placeholder="输入行名称"
                          />
                          <button
                            class="btn-remove-matrix"
                            @click="removeMatrixRow(index, Number(ri))"
                            :disabled="element.config.rows.length <= 1"
                          >
                            <i class="fas fa-times"></i>
                          </button>
                        </div>
                      </div>
                      <button
                        class="btn-add-matrix"
                        @click="addMatrixRow(index)"
                        :disabled="element.config.rows.length >= 10"
                      >
                        <i class="fas fa-plus"></i> 添加行
                      </button>
                    </div>
                    <div class="matrix-section">
                      <label>列（评价维度）</label>
                      <div class="matrix-items">
                        <div
                          v-for="(col, ci) in element.config.cols"
                          :key="ci"
                          class="matrix-item"
                        >
                          <input
                            v-model="element.config.cols[ci]"
                            placeholder="输入列名称"
                          />
                          <button
                            class="btn-remove-matrix"
                            @click="removeMatrixCol(index, Number(ci))"
                            :disabled="element.config.cols.length <= 2"
                          >
                            <i class="fas fa-times"></i>
                          </button>
                        </div>
                      </div>
                      <button
                        class="btn-add-matrix"
                        @click="addMatrixCol(index)"
                        :disabled="element.config.cols.length >= 10"
                      >
                        <i class="fas fa-plus"></i> 添加列
                      </button>
                    </div>
                  </div>
                </template>
              </div>
            </div>
          </template>
        </draggable>

        <div class="add-question-area">
          <button
            v-for="type in questionTypes"
            :key="type.value"
            class="add-question-btn"
            @click="addQuestion(type.value)"
          >
            <i :class="type.icon"></i>
            {{ type.label }}
          </button>
        </div>
      </div>

      <!-- 底部操作栏 -->
      <div class="form-actions">
        <button class="btn-cancel" @click="$router.push('/activity/survey')">
          取消
        </button>
        <!-- 草稿模式（创建时保存草稿，编辑时更新） -->
        <button class="btn-save-draft" @click="saveDraft" :disabled="submitting">
          {{ isEditMode ? '更新草稿' : '保存草稿' }}
        </button>
        <!-- 发布按钮（创建时：创建+发布；编辑时：更新+发布） -->
        <button class="btn-publish" @click="publishSurvey" :disabled="submitting || !isValid">
          <i v-if="submitting" class="fas fa-spinner fa-spin"></i>
          {{ isEditMode ? '更新并发布' : '发布问卷' }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import draggable from 'vuedraggable'
import { 
  createSurvey, 
  getSurveyDetail, 
  updateSurvey as updateSurveyApi,
  publishSurvey as publishSurveyApi 
} from './api'

const route = useRoute()
const router = useRouter()

// ===== 判断模式 =====
const surveyId = computed(() => {
  const id = route.params.id
  return id ? Number(id) : 0
})
const isEditMode = computed(() => surveyId.value > 0)

// ===== 题型定义 =====
const questionTypes = [
  { value: 1, label: '单选题', icon: 'fas fa-dot-circle' },
  { value: 2, label: '多选题', icon: 'fas fa-check-square' },
  { value: 3, label: '填空题', icon: 'fas fa-pen' },
  { value: 4, label: '评分题', icon: 'fas fa-star' },
  { value: 5, label: '排序题', icon: 'fas fa-arrows-alt-v' },
  { value: 6, label: '矩阵题', icon: 'fas fa-table' },
]

// ===== 表单数据 =====
const form = ref({
  title: '',
  description: '',
  startTime: '',
  endTime: '',
  isPublic: true,
  allowAnonymous: false,
})

const questions = ref<any[]>([])
const submitting = ref(false)
const loading = ref(true)

// ===== 计算属性 =====
const minDateTime = computed(() => {
  const now = new Date()
  now.setMinutes(now.getMinutes() - now.getTimezoneOffset())
  return now.toISOString().slice(0, 16)
})

const isValid = computed(() => {
  if (!form.value.title.trim()) return false
  if (!form.value.startTime || !form.value.endTime) return false
  if (new Date(form.value.endTime) <= new Date(form.value.startTime)) return false
  if (questions.value.length === 0) return false

  for (const q of questions.value) {
    if (!q.title.trim()) return false
    if ([1, 2, 5].includes(q.questionType)) {
      if (!q.options || q.options.length < 2) return false
      if (q.options.some((o: any) => !o.optionText.trim())) return false
    }
    if (q.questionType === 6) {
      if (!q.config?.rows || q.config.rows.length < 1) return false
      if (!q.config?.cols || q.config.cols.length < 2) return false
      if (q.config.rows.some((r: string) => !r.trim())) return false
      if (q.config.cols.some((c: string) => !c.trim())) return false
    }
    if (q.questionType === 4) {
      if (!q.config?.maxScore || q.config.maxScore < 2) return false
    }
  }
  return true
})

// ===== 加载数据（编辑模式） =====
const loadSurvey = async () => {
  if (!isEditMode.value) {
    loading.value = false
    return
  }

  loading.value = true
  try {
    const res = await getSurveyDetail(surveyId.value)
    form.value.title = res.title || ''
    form.value.description = res.description || ''
    form.value.isPublic = res.isPublic ?? true
    form.value.allowAnonymous = res.allowAnonymous ?? false

    const start = new Date(res.startTime)
    const end = new Date(res.endTime)
    start.setMinutes(start.getMinutes() - start.getTimezoneOffset())
    end.setMinutes(end.getMinutes() - end.getTimezoneOffset())
    form.value.startTime = start.toISOString().slice(0, 16)
    form.value.endTime = end.toISOString().slice(0, 16)

    questions.value = res.questions.map((q: any) => {
      const question: any = {
        id: q.id,
        questionType: q.questionType,
        title: q.title,
        description: q.description || '',
        isRequired: q.isRequired,
        sortOrder: q.sortOrder || 0,
        config: q.config ? JSON.parse(q.config) : {},
        options: q.options.map((o: any) => ({
          id: o.id,
          optionText: o.optionText,
          optionValue: o.optionValue || '',
          sortOrder: o.sortOrder || 0,
        })),
      }

      if (q.questionType === 6 && q.config) {
        try { question.config = JSON.parse(q.config) } 
        catch { question.config = { rows: [''], cols: ['非常好', '好', '一般', '差'] } }
      }
      if (q.questionType === 4 && q.config) {
        try { question.config = JSON.parse(q.config) } 
        catch { question.config = { minScore: 1, maxScore: 5 } }
      }
      return question
    })
  } catch (error: any) {
    console.error('加载问卷失败:', error)
    alert(error.response?.data?.message || '加载问卷失败，请重试')
    router.push('/activity/survey')
  } finally {
    loading.value = false
  }
}

// ===== 方法 =====
const getTypeLabel = (type: number) => {
  const found = questionTypes.find(t => t.value === type)
  return found ? found.label : '未知'
}

const addQuestion = (type: number) => {
  const newQuestion: any = {
    id: isEditMode.value ? 0 : Date.now() + Math.random() * 1000,
    questionType: type,
    title: '',
    description: '',
    isRequired: true,
    sortOrder: questions.value.length,
    options: [],
    config: {},
  }

  switch (type) {
    case 1:
    case 2:
      newQuestion.options = [{ id: 0, optionText: '' }, { id: 0, optionText: '' }]
      break
    case 3:
      break
    case 4:
      newQuestion.config = { minScore: 1, maxScore: 5 }
      break
    case 5:
      newQuestion.options = [{ id: 0, optionText: '' }, { id: 0, optionText: '' }]
      break
    case 6:
      newQuestion.config = { rows: [''], cols: ['非常好', '好', '一般', '差'] }
      break
  }
  questions.value.push(newQuestion)
}

const deleteQuestion = (index: number) => {
  if (confirm('确定要删除这道题吗？')) {
    questions.value.splice(index, 1)
  }
}

const copyQuestion = (index: number) => {
  const original = questions.value[index]
  const copy = JSON.parse(JSON.stringify(original))
  copy.id = isEditMode.value ? 0 : Date.now() + Math.random() * 1000
  copy.sortOrder = questions.value.length
  if (copy.title) copy.title = copy.title + ' (副本)'
  questions.value.splice(index + 1, 0, copy)
}

const addOption = (qIndex: number) => {
  if (questions.value[qIndex].options.length < 10) {
    questions.value[qIndex].options.push({ id: 0, optionText: '' })
  }
}

const removeOption = (qIndex: number, oIndex: number) => {
  if (questions.value[qIndex].options.length > 2) {
    questions.value[qIndex].options.splice(oIndex, 1)
  }
}

const addMatrixRow = (qIndex: number) => {
  if (questions.value[qIndex].config.rows.length < 10) {
    questions.value[qIndex].config.rows.push('')
  }
}

const removeMatrixRow = (qIndex: number, index: number) => {
  if (questions.value[qIndex].config.rows.length > 1) {
    questions.value[qIndex].config.rows.splice(index, 1)
  }
}

const addMatrixCol = (qIndex: number) => {
  if (questions.value[qIndex].config.cols.length < 10) {
    questions.value[qIndex].config.cols.push('')
  }
}

const removeMatrixCol = (qIndex: number, index: number) => {
  if (questions.value[qIndex].config.cols.length > 2) {
    questions.value[qIndex].config.cols.splice(index, 1)
  }
}

// 构建请求数据
const buildPayload = () => {
  const payload: any = {
    title: form.value.title.trim(),
    description: form.value.description?.trim() || undefined,
    startTime: new Date(form.value.startTime).toISOString(),
    endTime: new Date(form.value.endTime).toISOString(),
    isPublic: form.value.isPublic,
    allowAnonymous: form.value.allowAnonymous,
  }

  // 如果是编辑模式，需要传 Questions
  if (isEditMode.value) {
    payload.questions = questions.value.map((q, index) => {
      const question: any = {
        id: q.id || 0,
        questionType: q.questionType,
        title: q.title.trim(),
        description: q.description?.trim() || undefined,
        isRequired: q.isRequired,
        sortOrder: index,
        config: null,
        options: [],
      }

      if ([1, 2, 5].includes(q.questionType)) {
        question.options = q.options
          .filter((o: any) => o.optionText.trim())
          .map((o: any) => ({
            id: o.id || 0,
            optionText: o.optionText.trim(),
            optionValue: o.optionValue || '',
            sortOrder: 0,
          }))
      }

      if (q.questionType === 4 && q.config) {
        question.config = JSON.stringify({ minScore: q.config.minScore || 1, maxScore: q.config.maxScore || 5 })
      }
      if (q.questionType === 6 && q.config) {
        question.config = JSON.stringify({
          rows: q.config.rows.filter((r: string) => r.trim()),
          cols: q.config.cols.filter((c: string) => c.trim()),
        })
      }
      return question
    })
  } else {
    // 创建模式：使用 CreateQuestionDto 格式
    payload.questions = questions.value.map((q, index) => {
      const question: any = {
        questionType: q.questionType,
        title: q.title.trim(),
        description: q.description?.trim() || undefined,
        isRequired: q.isRequired,
        sortOrder: index,
        config: null,
        options: [],
      }

      if ([1, 2, 5].includes(q.questionType)) {
        question.options = q.options
          .filter((o: any) => o.optionText.trim())
          .map((o: any) => ({ optionText: o.optionText.trim(), sortOrder: 0 }))
      }

      if (q.questionType === 4 && q.config) {
        question.config = JSON.stringify({ minScore: q.config.minScore || 1, maxScore: q.config.maxScore || 5 })
      }
      if (q.questionType === 6 && q.config) {
        question.config = JSON.stringify({
          rows: q.config.rows.filter((r: string) => r.trim()),
          cols: q.config.cols.filter((c: string) => c.trim()),
        })
      }
      return question
    })
  }

  return payload
}

// ===== 保存草稿 =====
const saveDraft = async () => {
  if (!form.value.title.trim()) {
    alert('请填写问卷标题')
    return
  }
  if (questions.value.length === 0) {
    alert('请至少添加一道题目')
    return
  }

  submitting.value = true
  try {
    if (isEditMode.value) {
      await updateSurveyApi(surveyId.value, buildPayload())
      alert('草稿更新成功！')
    } else {
      await createSurvey(buildPayload())
      alert('草稿保存成功！')
    }
    router.push('/activity/survey')
  } catch (error: any) {
    alert(error.response?.data?.message || '保存失败，请重试')
  } finally {
    submitting.value = false
  }
}

// ===== 发布问卷 =====
const publishSurvey = async () => {
  if (!isValid.value) {
    alert('请完善问卷信息（标题、题目内容、选项等）')
    return
  }

  if (!confirm(isEditMode.value ? '确认更新并发布此问卷吗？' : '确认发布此问卷吗？发布后将不可修改内容。')) return

  submitting.value = true
  try {
    let id = surveyId.value

    if (isEditMode.value) {
      // 编辑模式：先更新，再发布
      await updateSurveyApi(id, buildPayload())
      await publishSurveyApi(id)
    } else {
      // 创建模式：先创建，再发布
      const response = await createSurvey(buildPayload())
      id = response.surveyId
      await publishSurveyApi(id)
    }

    alert('问卷发布成功！')
    router.push('/activity/survey')
  } catch (error: any) {
    alert(error.response?.data?.message || '发布失败，请重试')
  } finally {
    submitting.value = false
  }
}

// ===== 生命周期 =====
onMounted(() => {
  if (isEditMode.value) {
    loadSurvey()
  } else {
    loading.value = false
    // 初始化默认时间
    const now = new Date()
    const start = new Date(now.getTime() + 60 * 60 * 1000)
    start.setMinutes(start.getMinutes() - start.getTimezoneOffset())
    form.value.startTime = start.toISOString().slice(0, 16)
    const end = new Date(start.getTime() + 7 * 24 * 60 * 60 * 1000)
    end.setMinutes(end.getMinutes() - end.getTimezoneOffset())
    form.value.endTime = end.toISOString().slice(0, 16)
  }
})
</script>

<style scoped>
/* 样式与之前 CreateSurvey.vue 完全相同，这里只复制一次 */
.survey-editor-container {
  max-width: 900px;
  margin: 0 auto;
  padding: 20px 0;
}
.form-card {
  background: #fff;
  border-radius: 16px;
  padding: 32px 40px;
  border: 1px solid #eee;
  box-shadow: 0 1px 3px rgba(0,0,0,0.04);
}
.form-header {
  margin-bottom: 28px;
  padding-bottom: 20px;
  border-bottom: 1px solid #f0f0f0;
}
.form-header h2 {
  font-size: 1.5rem;
  font-weight: 700;
  color: #1f2937;
  display: flex;
  align-items: center;
  gap: 10px;
  margin: 0 0 4px;
}
.form-header h2 i { color: #6366f1; }
.form-header .subtitle {
  color: #9ca3af;
  font-size: 0.9rem;
  margin: 0;
}
.section {
  margin-bottom: 32px;
}
.section-title {
  font-weight: 600;
  font-size: 1rem;
  color: #1f2937;
  margin-bottom: 16px;
  display: flex;
  align-items: center;
  gap: 8px;
}
.question-count {
  font-size: 0.8rem;
  font-weight: 400;
  color: #9ca3af;
  background: #f3f4f6;
  padding: 2px 12px;
  border-radius: 12px;
}
.form-group {
  margin-bottom: 16px;
}
.form-group label {
  display: block;
  font-weight: 500;
  font-size: 0.9rem;
  color: #374151;
  margin-bottom: 4px;
}
.form-group label .required { color: #ef4444; margin-left: 2px; }
.form-group input[type="text"],
.form-group input[type="datetime-local"],
.form-group textarea {
  width: 100%;
  padding: 10px 14px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  font-size: 0.95rem;
  font-family: inherit;
  transition: border 0.2s;
  background: #fafafa;
  color: #1f2937;
}
.form-group input:focus,
.form-group textarea:focus {
  outline: none;
  border-color: #6366f1;
  background: #fff;
  box-shadow: 0 0 0 3px rgba(99,102,241,0.1);
}
.form-group textarea { resize: vertical; min-height: 60px; }
.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}
.checkbox-label {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 400 !important;
  cursor: pointer;
}
.checkbox-label input[type="checkbox"] {
  width: 18px;
  height: 18px;
  accent-color: #6366f1;
}

.empty-questions {
  text-align: center;
  padding: 40px 20px;
  color: #9ca3af;
  border: 2px dashed #e5e7eb;
  border-radius: 12px;
}
.empty-questions i {
  font-size: 2.5rem;
  color: #d1d5db;
  display: block;
  margin-bottom: 8px;
}

.question-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin-bottom: 16px;
}
.question-item {
  background: #f9fafb;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  padding: 16px 20px;
  transition: all 0.2s;
}
.question-item:hover {
  border-color: #d1d5db;
  background: #fff;
}

.question-header {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}
.drag-handle {
  cursor: grab;
  color: #9ca3af;
  padding: 4px;
}
.drag-handle:hover { color: #6b7280; }
.q-number {
  font-weight: 600;
  color: #6366f1;
  font-size: 0.85rem;
  min-width: 30px;
}
.q-type {
  font-size: 0.7rem;
  background: #e5e7eb;
  color: #6b7280;
  padding: 2px 10px;
  border-radius: 12px;
  white-space: nowrap;
}
.q-title-input {
  flex: 1;
  min-width: 150px;
  padding: 6px 12px;
  border: 1px solid transparent;
  border-radius: 6px;
  font-size: 0.95rem;
  font-weight: 500;
  background: transparent;
  transition: all 0.2s;
  color: #1f2937;
}
.q-title-input:hover {
  background: #fff;
  border-color: #e5e7eb;
}
.q-title-input:focus {
  outline: none;
  background: #fff;
  border-color: #6366f1;
  box-shadow: 0 0 0 3px rgba(99,102,241,0.1);
}
.q-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-left: auto;
}
.required-toggle {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 0.75rem;
  color: #6b7280;
  cursor: pointer;
}
.required-toggle input[type="checkbox"] {
  width: 14px;
  height: 14px;
  accent-color: #6366f1;
}
.btn-copy, .btn-delete {
  background: none;
  border: none;
  padding: 4px 8px;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
  color: #9ca3af;
}
.btn-copy:hover { background: #e5e7eb; color: #6366f1; }
.btn-delete:hover { background: #fee2e2; color: #ef4444; }

.q-desc-input {
  width: 100%;
  margin-top: 6px;
  padding: 4px 0;
  border: none;
  border-bottom: 1px dashed #e5e7eb;
  font-size: 0.85rem;
  color: #6b7280;
  background: transparent;
}
.q-desc-input:focus { outline: none; border-bottom-color: #6366f1; }

.question-body {
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px solid #f0f0f0;
}

.options-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.option-item {
  display: flex;
  align-items: center;
  gap: 10px;
}
.option-label {
  font-weight: 500;
  font-size: 0.8rem;
  color: #6b7280;
  min-width: 20px;
}
.option-input {
  flex: 1;
  padding: 6px 12px;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
  font-size: 0.9rem;
  background: #fff;
  transition: border 0.2s;
  color: #1f2937;
}
.option-input:focus {
  outline: none;
  border-color: #6366f1;
  box-shadow: 0 0 0 3px rgba(99,102,241,0.1);
}
.btn-remove-option {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  border: 1px solid #e5e7eb;
  background: #fff;
  color: #9ca3af;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
}
.btn-remove-option:hover:not(:disabled) {
  background: #fee2e2;
  border-color: #fecaca;
  color: #ef4444;
}
.btn-remove-option:disabled { opacity: 0.4; cursor: not-allowed; }
.btn-add-option {
  padding: 6px 16px;
  border: 1px dashed #e5e7eb;
  border-radius: 6px;
  background: transparent;
  color: #6b7280;
  font-size: 0.8rem;
  cursor: pointer;
  transition: all 0.2s;
  margin-top: 4px;
}
.btn-add-option:hover:not(:disabled) {
  border-color: #6366f1;
  color: #6366f1;
  background: #f8f7ff;
}
.btn-add-option:disabled { opacity: 0.4; cursor: not-allowed; }

.fill-hint {
  padding: 12px 16px;
  background: #f0fdf4;
  border: 1px solid #bbf7d0;
  border-radius: 8px;
  color: #166534;
  font-size: 0.9rem;
}
.fill-hint i { margin-right: 8px; }

.rating-config {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.rating-config label {
  font-size: 0.85rem;
  font-weight: 500;
  color: #374151;
}
.rating-range {
  display: flex;
  align-items: center;
  gap: 8px;
}
.rating-range input {
  width: 60px;
  padding: 6px 10px;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
  text-align: center;
}
.rating-range input:focus { outline: none; border-color: #6366f1; }
.rating-preview {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 8px 12px;
  background: #fafafa;
  border-radius: 8px;
}
.rating-preview i {
  color: #d1d5db;
  font-size: 1.2rem;
}
.rating-preview i.active { color: #f59e0b; }
.rating-preview .preview-label {
  margin-left: 8px;
  font-size: 0.8rem;
  color: #6b7280;
}

.sort-options .sort-item {
  background: #fafafa;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
  padding: 4px 12px;
}
.sort-icon {
  color: #9ca3af;
  font-size: 0.8rem;
}

.matrix-config {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}
.matrix-section label {
  font-size: 0.85rem;
  font-weight: 500;
  color: #374151;
  display: block;
  margin-bottom: 6px;
}
.matrix-items {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.matrix-item {
  display: flex;
  align-items: center;
  gap: 8px;
}
.matrix-item input {
  flex: 1;
  padding: 6px 10px;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
  font-size: 0.85rem;
}
.matrix-item input:focus { outline: none; border-color: #6366f1; }
.btn-remove-matrix {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  border: 1px solid #e5e7eb;
  background: #fff;
  color: #9ca3af;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}
.btn-remove-matrix:hover:not(:disabled) {
  background: #fee2e2;
  border-color: #fecaca;
  color: #ef4444;
}
.btn-remove-matrix:disabled { opacity: 0.4; cursor: not-allowed; }
.btn-add-matrix {
  margin-top: 4px;
  padding: 4px 14px;
  border: 1px dashed #e5e7eb;
  border-radius: 6px;
  background: transparent;
  color: #6b7280;
  font-size: 0.75rem;
  cursor: pointer;
  transition: all 0.2s;
}
.btn-add-matrix:hover:not(:disabled) {
  border-color: #6366f1;
  color: #6366f1;
}
.btn-add-matrix:disabled { opacity: 0.4; cursor: not-allowed; }

.add-question-area {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  padding-top: 8px;
  border-top: 1px solid #f0f0f0;
}
.add-question-btn {
  padding: 8px 16px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: #fafafa;
  color: #374151;
  font-size: 0.8rem;
  cursor: pointer;
  transition: all 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.add-question-btn:hover {
  background: #f3f4f6;
  border-color: #d1d5db;
  transform: translateY(-2px);
}
.add-question-btn i { font-size: 0.9rem; }

.form-actions {
  display: flex;
  gap: 12px;
  justify-content: flex-end;
  margin-top: 28px;
  padding-top: 20px;
  border-top: 1px solid #f0f0f0;
}
.btn-cancel {
  padding: 10px 24px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: #fff;
  color: #6b7280;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}
.btn-cancel:hover { background: #f3f4f6; border-color: #d1d5db; }
.btn-save-draft {
  padding: 10px 24px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: #f9fafb;
  color: #374151;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}
.btn-save-draft:hover:not(:disabled) { background: #f3f4f6; border-color: #d1d5db; }
.btn-publish {
  padding: 10px 32px;
  border: none;
  border-radius: 8px;
  background: linear-gradient(135deg, #6366f1, #8b5cf6);
  color: #fff;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 8px;
}
.btn-publish:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 8px 25px rgba(99,102,241,0.3);
}
.btn-publish:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
  box-shadow: none;
}

@media (max-width: 768px) {
  .form-card { padding: 20px; }
  .form-row { grid-template-columns: 1fr; }
  .question-header { flex-wrap: wrap; }
  .q-title-input { min-width: 100%; order: 10; }
  .q-actions {
    margin-left: 0;
    width: 100%;
    justify-content: flex-end;
    padding-top: 6px;
    border-top: 1px solid #f0f0f0;
  }
  .matrix-config { grid-template-columns: 1fr; }
  .add-question-area { justify-content: center; }
  .form-actions { flex-direction: column; }
  .btn-cancel, .btn-save-draft, .btn-publish { width: 100%; justify-content: center; }
}
</style>