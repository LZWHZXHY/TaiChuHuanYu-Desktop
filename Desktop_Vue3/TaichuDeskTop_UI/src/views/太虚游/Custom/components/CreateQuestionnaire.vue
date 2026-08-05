<template>
  <div class="create-questionnaire">
    <header class="create-header">
      <button class="btn-back" @click="$emit('cancel')">← 返回</button>
      <h2>📋 {{ isEditMode ? '编辑问卷测试' : '创建问卷测试' }}</h2>
      <p>{{ isEditMode ? '修改已有游戏的配置' : '支持多种题型，可配图，自由组合' }}</p>
    </header>

    <form @submit.prevent="submitForm" class="create-form">
      <!-- ===== 基础信息 ===== -->
      <div class="form-group">
        <label>游戏图标（Emoji）</label>
        <input v-model="form.icon" placeholder="例如：🌟" maxlength="2" />
      </div>

      <div class="form-group">
        <label>游戏标题 <span class="required">*</span></label>
        <input v-model="form.title" placeholder="输入标题" required />
      </div>

      <div class="form-group">
        <label>简短描述</label>
        <textarea v-model="form.desc" rows="2" placeholder="描述这个测试的特点"></textarea>
      </div>

      <div class="form-group">
        <label>计分方式</label>
        <select v-model="form.scoring">
          <option value="sum">总分（各题分值累加）</option>
          <option value="average">平均分（总分 ÷ 题数）</option>
        </select>
      </div>

      <!-- ===== 题目列表 ===== -->
      <div class="form-group questions-group">
        <div class="group-header">
          <label>题目配置 <span class="required">*</span></label>
          <button type="button" class="btn-add-question" @click="addQuestion">+ 添加题目</button>
        </div>

        <div v-for="(q, qIdx) in form.questions" :key="qIdx" class="question-item">
          <div class="question-header">
            <span class="q-number">第 {{ qIdx + 1 }} 题</span>
            <select v-model="q.type" class="q-type-select" @change="onTypeChange(q)">
              <option value="single">单选题</option>
              <option value="yesno">是非题</option>
              <option value="likert">量表题</option>
              <option value="multiple">多选题</option>
            </select>
            <button type="button" class="btn-remove" @click="removeQuestion(qIdx)">✕</button>
          </div>

          <input v-model="q.text" class="q-text" placeholder="请输入题目内容" required />

          <div class="q-image-group">
            <input v-model="q.image" placeholder="题目配图链接（可选）" />
            <span v-if="q.image" class="image-preview">🖼️ 已添加图片</span>
          </div>

          <div class="options-config">
            <template v-if="q.type === 'yesno'">
              <div class="yesno-config">
                <div class="option-item">
                  <span>是</span>
                  <input v-model.number="q.options[0].value" type="number" placeholder="分值" step="1" />
                </div>
                <div class="option-item">
                  <span>否</span>
                  <input v-model.number="q.options[1].value" type="number" placeholder="分值" step="1" />
                </div>
              </div>
            </template>

            <template v-else-if="q.type === 'likert'">
              <div class="likert-config">
                <div class="likert-labels">
                  <label>程度标签（用逗号分隔，最少3个）</label>
                  <input v-model="q.scaleLabels" placeholder="例如：非常不像,不太像,一般,比较像,非常像" />
                  <span class="hint">每个标签对应的分值从 1 开始递增</span>
                </div>
                <div v-if="getScaleArray(q).length > 0" class="likert-preview">
                  <div v-for="(label, idx) in getScaleArray(q)" :key="idx" class="scale-item">
                    <span>{{ label }}</span>
                    <span class="scale-value">→ {{ idx + 1 }} 分</span>
                  </div>
                </div>
              </div>
            </template>

            <template v-else>
              <div v-for="(opt, oIdx) in q.options" :key="oIdx" class="option-item">
                <input v-model="opt.label" placeholder="选项文字" />
                <input v-model.number="opt.value" type="number" placeholder="分值" step="1" />
                <input v-model="opt.image" placeholder="选项配图链接（可选）" />
                <button type="button" class="btn-remove-opt" @click="removeOption(qIdx, oIdx)">✕</button>
              </div>
              <button type="button" class="btn-add-opt" @click="addOption(qIdx)">+ 添加选项</button>
            </template>
          </div>
        </div>
      </div>

      <!-- ===== 结果配置 ===== -->
      <div class="form-group results-group">
        <div class="group-header">
          <label>结果映射（根据总分区间）</label>
          <button type="button" class="btn-add-result" @click="addResult">+ 添加结果</button>
        </div>

        <div v-for="(r, rIdx) in form.results" :key="rIdx" class="result-item">
          <div class="result-header">
            <span>结果 {{ rIdx + 1 }}</span>
            <button type="button" class="btn-remove" @click="removeResult(rIdx)">✕</button>
          </div>
          <div class="result-row">
            <input v-model.number="r.min" type="number" placeholder="最低分" />
            <span>~</span>
            <input v-model.number="r.max" type="number" placeholder="最高分" />
          </div>
          <input v-model="r.title" placeholder="结果标题" />
          <input v-model="r.desc" placeholder="结果描述" />
          <input v-model="r.icon" placeholder="结果图标（Emoji）" maxlength="2" />
          <input v-model="r.image" placeholder="结果配图链接（可选）" />
        </div>
      </div>

      <div class="form-actions">
        <button type="button" class="btn-cancel" @click="$emit('cancel')">取消</button>
        <button type="submit" class="btn-submit">{{ isEditMode ? '更新' : '发布（草稿）' }}</button>
      </div>
    </form>
  </div>
</template>

<script setup>
import { reactive, computed, watch, onMounted } from 'vue'

const props = defineProps({
  editData: { type: Object, default: null },
  editGameId: { type: Number, default: null }
})

const emit = defineEmits(['cancel', 'success'])

// ===== 计算是否为编辑模式 =====
const isEditMode = computed(() => !!props.editData)

// ===== 默认选项工厂 =====
const createDefaultOptions = (type) => {
  if (type === 'yesno') {
    return [
      { label: '是', value: 1 },
      { label: '否', value: 0 }
    ]
  }
  return [
    { label: '选项1', value: 0, image: '' },
    { label: '选项2', value: 1, image: '' }
  ]
}

// ===== 获取量表标签数组 =====
const getScaleArray = (q) => {
  if (!q.scaleLabels || !q.scaleLabels.trim()) return []
  return q.scaleLabels.split(/[,，、\s]+/).filter(s => s.trim())
}

// ===== 表单数据 =====
const defaultForm = () => ({
  icon: '🎮',
  title: '',
  desc: '',
  scoring: 'sum',
  questions: [
    {
      type: 'single',
      text: '',
      image: '',
      options: [
        { label: '选项1', value: 0, image: '' },
        { label: '选项2', value: 1, image: '' }
      ],
      scaleLabels: ''
    }
  ],
  results: [
    { min: 0, max: 5, title: '结果A', desc: '描述A', icon: '🏅', image: '' },
    { min: 6, max: 10, title: '结果B', desc: '描述B', icon: '🌟', image: '' }
  ]
})

// ===== 从编辑数据填充表单 =====
const fillFormFromEditData = (data) => {
  if (!data) return defaultForm()

  const q = data.questionnaire
  if (!q) return defaultForm()

  return {
    icon: data.icon || '🎮',
    title: data.title || '',
    desc: data.description || '',
    scoring: q.scoring || 'sum',
    questions: q.questions.map((qItem) => ({
      type: qItem.type || 'single',
      text: qItem.text || '',
      image: qItem.image || '',
      options: qItem.options.map((opt) => ({
        label: opt.label || '',
        value: opt.value || 0,
        image: opt.image || ''
      })),
      scaleLabels: '' // 量表标签暂不支持编辑恢复
    })),
    results: q.results.map((r) => ({
      min: r.min || 0,
      max: r.max || 0,
      title: r.title || '',
      desc: r.description || '',
      icon: r.icon || '🏷️',
      image: r.image || ''
    }))
  }
}

// ===== 初始化/重置表单 =====
const form = reactive(defaultForm())

const resetForm = () => {
  const data = fillFormFromEditData(props.editData)
  Object.keys(data).forEach(key => {
    form[key] = data[key]
  })
}

// ===== 监听编辑数据变化 =====
watch(() => props.editData, () => {
  resetForm()
}, { immediate: true })

// ===== 题目操作 =====
function addQuestion() {
  form.questions.push({
    type: 'single',
    text: '',
    image: '',
    options: createDefaultOptions('single'),
    scaleLabels: ''
  })
}

function removeQuestion(idx) {
  if (form.questions.length > 1) {
    form.questions.splice(idx, 1)
  } else {
    alert('至少保留一个题目')
  }
}

function onTypeChange(q) {
  if (q.type === 'yesno') {
    q.options = createDefaultOptions('yesno')
    q.scaleLabels = ''
  } else if (q.type === 'likert') {
    q.options = []
    q.scaleLabels = '非常不像,不太像,一般,比较像,非常像'
  } else {
    q.options = createDefaultOptions('single')
    q.scaleLabels = ''
  }
}

function addOption(qIdx) {
  form.questions[qIdx].options.push({ label: '', value: 0, image: '' })
}

function removeOption(qIdx, oIdx) {
  const q = form.questions[qIdx]
  if (q.type === 'yesno') {
    alert('是非题不能删除选项')
    return
  }
  if (q.options.length > 2) {
    q.options.splice(oIdx, 1)
  } else {
    alert('至少保留两个选项')
  }
}

// ===== 结果操作 =====
function addResult() {
  form.results.push({ min: 0, max: 0, title: '', desc: '', icon: '', image: '' })
}

function removeResult(idx) {
  if (form.results.length > 1) {
    form.results.splice(idx, 1)
  } else {
    alert('至少保留一个结果')
  }
}

// ===== 提交 =====
function submitForm() {
  if (!form.title.trim()) {
    alert('请输入游戏标题')
    return
  }

  for (const q of form.questions) {
    if (!q.text.trim()) {
      alert('请完善所有题目内容')
      return
    }

    if (q.type === 'likert') {
      const labels = getScaleArray(q)
      if (labels.length < 3) {
        alert(`第 ${form.questions.indexOf(q) + 1} 题：量表至少需要3个程度标签`)
        return
      }
    } else {
      for (const opt of q.options) {
        if (!opt.label.trim()) {
          alert(`第 ${form.questions.indexOf(q) + 1} 题：请完善所有选项文字`)
          return
        }
      }
    }
  }

  const payload = {
    type: 'questionnaire',
    icon: form.icon || '🎮',
    title: form.title,
    description: form.desc,
    scoring: form.scoring,
    questions: form.questions.map(q => {
      const base = {
        type: q.type,
        text: q.text,
        image: q.image || ''
      }
      if (q.type === 'likert') {
        const labels = getScaleArray(q)
        return {
          ...base,
          options: labels.map((label, idx) => ({
            label: label.trim(),
            value: idx + 1,
            image: ''
          })),
          scaleLabels: labels
        }
      } else {
        return {
          ...base,
          options: q.options.map(opt => ({
            label: opt.label,
            value: opt.value || 0,
            image: opt.image || ''
          }))
        }
      }
    }),
    results: form.results.map(r => ({
      min: r.min || 0,
      max: r.max || 0,
      title: r.title || '未命名',
      description: r.desc || '',
      icon: r.icon || '🏷️',
      image: r.image || ''
    }))
  }

  emit('success', payload)
}
</script>



<style scoped>
.create-questionnaire {
  max-width: 800px;
  margin: 0 auto;
  padding: 20px 0;
}

/* ===== 头部 ===== */
.create-header {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
  margin-bottom: 24px;
  border-bottom: 1px solid var(--line-raw);
  padding-bottom: 12px;
}
.btn-back {
  background: none;
  border: none;
  color: var(--ink-gray);
  font-family: inherit;
  font-size: 14px;
  cursor: pointer;
  padding: 4px 8px;
  transition: color 0.3s;
}
.btn-back:hover {
  color: var(--cinnabar);
}
.create-header h2 {
  font-size: 24px;
  font-weight: 400;
  letter-spacing: 0.2em;
  color: var(--ink-black);
  margin: 0;
}
.create-header p {
  font-size: 14px;
  color: var(--ink-gray);
  letter-spacing: 0.1em;
  margin: 0 0 0 auto;
}

/* ===== 表单 ===== */
.create-form {
  display: flex;
  flex-direction: column;
  gap: 20px;
}
.form-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.form-group label {
  font-size: 14px;
  letter-spacing: 0.1em;
  color: var(--ink-black);
}
.required {
  color: var(--cinnabar);
  margin-left: 4px;
}
.form-group input,
.form-group textarea,
.form-group select {
  padding: 8px 12px;
  border: 1px solid var(--line-raw);
  background: var(--paper-card);
  font-family: inherit;
  font-size: 14px;
  color: var(--ink-black);
  transition: border-color 0.3s;
}
.form-group input:focus,
.form-group textarea:focus,
.form-group select:focus {
  outline: none;
  border-color: var(--cinnabar);
}
.form-group textarea {
  resize: vertical;
}
.form-group select {
  cursor: pointer;
}

/* ===== 题目区域 ===== */
.questions-group,
.results-group {
  border-top: 1px dashed var(--line-raw);
  padding-top: 16px;
}
.group-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.group-header label {
  margin-bottom: 0;
}

/* ===== 单个题目 ===== */
.question-item {
  border: 1px solid var(--line-raw);
  padding: 14px 16px;
  margin-bottom: 14px;
  background: var(--paper-sub);
}
.question-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 10px;
}
.q-number {
  font-size: 14px;
  color: var(--ink-gray);
  font-weight: 500;
}
.q-type-select {
  padding: 4px 8px;
  border: 1px solid var(--line-raw);
  background: var(--paper-card);
  font-family: inherit;
  font-size: 13px;
  color: var(--ink-black);
}
.q-text {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid var(--line-raw);
  background: var(--paper-card);
  font-family: inherit;
  font-size: 14px;
}
.q-image-group {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-top: 6px;
}
.q-image-group input {
  flex: 1;
  padding: 6px 10px;
  border: 1px solid var(--line-raw);
  background: var(--paper-card);
  font-family: inherit;
  font-size: 13px;
}
.image-preview {
  font-size: 13px;
  color: var(--cinnabar);
}

/* ===== 选项配置 ===== */
.options-config {
  margin-top: 10px;
}
.yesno-config {
  display: flex;
  gap: 16px;
  align-items: center;
  flex-wrap: wrap;
}
.yesno-config .option-item {
  display: flex;
  align-items: center;
  gap: 8px;
}
.yesno-config .option-item span {
  font-size: 14px;
  color: var(--ink-black);
}
.yesno-config .option-item input {
  width: 60px;
  padding: 4px 8px;
  border: 1px solid var(--line-raw);
  background: var(--paper-card);
}

/* 量表配置 */
.likert-config {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.likert-labels label {
  font-size: 13px;
  color: var(--ink-gray);
}
.likert-labels input {
  width: 100%;
  padding: 6px 10px;
  border: 1px solid var(--line-raw);
  background: var(--paper-card);
  margin-top: 4px;
}
.hint {
  font-size: 12px;
  color: var(--ink-gray);
  opacity: 0.6;
}
.likert-preview {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  margin-top: 6px;
}
.scale-item {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 4px 10px;
  border: 1px dashed var(--line-raw);
  background: var(--paper-card);
  font-size: 13px;
}
.scale-value {
  font-size: 11px;
  color: var(--ink-gray);
}

/* 选项列表（单选/多选） */
.option-item {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 6px;
  flex-wrap: wrap;
}
.option-item input:first-child {
  flex: 1;
  min-width: 120px;
}
.option-item input[type="number"] {
  width: 60px;
}
.option-item input[placeholder*="配图"] {
  flex: 0.5;
  min-width: 100px;
}
.btn-remove-opt {
  background: none;
  border: none;
  color: #b03a3a;
  font-size: 16px;
  cursor: pointer;
  padding: 0 4px;
}
.btn-remove-opt:hover {
  color: #7a2222;
}

/* 按钮 */
.btn-remove {
  background: none;
  border: none;
  color: #b03a3a;
  font-size: 18px;
  cursor: pointer;
  padding: 0 4px;
  margin-left: auto;
}
.btn-remove:hover {
  color: #7a2222;
}
.btn-add-opt,
.btn-add-question,
.btn-add-result {
  background: none;
  border: 1px dashed var(--line-raw);
  padding: 6px 14px;
  font-family: inherit;
  font-size: 13px;
  cursor: pointer;
  transition: border-color 0.3s, color 0.3s;
  color: var(--ink-gray);
  align-self: flex-start;
}
.btn-add-opt:hover,
.btn-add-question:hover,
.btn-add-result:hover {
  border-color: var(--cinnabar);
  color: var(--cinnabar);
}

/* ===== 结果 ===== */
.result-item {
  border: 1px solid var(--line-raw);
  padding: 12px 14px;
  margin-bottom: 12px;
  background: var(--paper-sub);
}
.result-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
  font-size: 14px;
  color: var(--ink-gray);
}
.result-row {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 6px;
}
.result-row input {
  width: 80px;
  padding: 4px 8px;
  border: 1px solid var(--line-raw);
  background: var(--paper-card);
}
.result-item > input {
  width: 100%;
  padding: 6px 10px;
  margin-bottom: 4px;
  border: 1px solid var(--line-raw);
  background: var(--paper-card);
}

/* ===== 提交按钮 ===== */
.form-actions {
  display: flex;
  gap: 12px;
  justify-content: flex-end;
  border-top: 1px solid var(--line-raw);
  padding-top: 16px;
}
.btn-cancel,
.btn-submit {
  padding: 8px 24px;
  font-family: inherit;
  font-size: 14px;
  letter-spacing: 0.1em;
  cursor: pointer;
  border: 1px solid var(--line-raw);
  background: none;
  transition: all 0.3s;
}
.btn-cancel:hover {
  border-color: var(--ink-gray);
}
.btn-submit {
  background: var(--cinnabar);
  color: #fff;
  border-color: var(--cinnabar);
}
.btn-submit:hover {
  background: #7a2222;
  border-color: #7a2222;
}

/* ===== 响应式 ===== */
@media (max-width: 640px) {
  .create-header {
    flex-direction: column;
    align-items: flex-start;
  }
  .create-header p {
    margin: 0;
  }
  .option-item {
    flex-direction: column;
    align-items: stretch;
  }
  .option-item input[type="number"] {
    width: 100%;
  }
  .yesno-config {
    flex-direction: column;
    align-items: stretch;
  }
}
</style>