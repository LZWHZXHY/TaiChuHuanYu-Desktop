<template>
  <div class="test-game">
    <!-- 开始页 -->
    <div v-if="!started" class="test-start">
      <div class="test-icon">{{ config.icon || '🔮' }}</div>
      <h2>{{ config.title }}</h2>
      <p>{{ config.desc }}</p>
      <button class="btn-line btn-start" @click="started = true">开始测试</button>
    </div>

    <!-- 答题页 -->
    <div v-else-if="!finished" class="test-questions">
      <div class="test-progress">
        <div class="progress-bar">
          <div class="progress-fill" :style="{ width: progressPercent + '%' }"></div>
        </div>
        <span class="progress-text">{{ currentIndex + 1 }} / {{ questions.length }}</span>
      </div>

      <div class="question-card">
        <div class="question-text">{{ currentQuestion.text }}</div>
        <div v-if="currentQuestion.image" class="question-image">
          <img :src="currentQuestion.image" alt="题目配图" />
        </div>
        <div class="options">
          <button
            v-for="(opt, idx) in currentQuestion.options"
            :key="idx"
            class="option-btn"
            :class="{ selected: selectedOption === idx }"
            @click="selectOption(idx)"
          >
            {{ opt.label }}
          </button>
        </div>
        <div class="question-nav">
          <button
            class="btn-line"
            :disabled="currentIndex === 0"
            @click="prevQuestion"
          >
            上一题
          </button>
          <button
            class="btn-line btn-next"
            :disabled="selectedOption === null"
            @click="nextQuestion"
          >
            {{ currentIndex === questions.length - 1 ? '查看结果' : '下一题' }}
          </button>
        </div>
      </div>
    </div>

    <!-- 结果页 -->
    <div v-else class="test-result">
      <div class="result-content">
        <div class="result-icon">{{ result.icon }}</div>
        <h2>{{ result.title }}</h2>
        <p class="result-desc">{{ result.desc }}</p>
        <div v-if="result.image" class="result-image">
          <img :src="result.image" alt="结果配图" />
        </div>
        <button class="btn-line btn-restart" @click="resetTest">重新测试</button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue'

// ===== Props =====
const props = defineProps({
  config: { type: Object, default: () => ({ icon: '🔮', title: '测试', desc: '' }) },
  questions: { type: Array, required: true },
  results: { type: Array, required: true },
  // 可选的 gameId，如果传了则会在完成时发射 completed 事件
  gameId: { type: Number, default: null }
})

// ===== Emits =====
const emit = defineEmits(['completed'])

// ===== 状态 =====
const started = ref(false)
const finished = ref(false)
const currentIndex = ref(0)
const selectedOption = ref(null) // 当前题目选中的选项索引
const answers = ref([]) // 存储每道题的分值（数值）
const finalResult = ref(null)

// ===== 计算属性 =====
const currentQuestion = computed(() => props.questions[currentIndex.value])
const progressPercent = computed(() => ((currentIndex.value + 1) / props.questions.length) * 100)
const result = computed(() => finalResult.value)

// ===== 方法 =====
function selectOption(idx) {
  selectedOption.value = idx
}

function nextQuestion() {
  if (selectedOption.value === null) return

  // 保存当前题目的分值（使用 .value 或 .score，优先 .value）
  const opt = currentQuestion.value.options[selectedOption.value]
  const score = opt.value !== undefined ? opt.value : (opt.score || 0)
  answers.value[currentIndex.value] = score

  if (currentIndex.value === props.questions.length - 1) {
    finishTest()
  } else {
    currentIndex.value++
    selectedOption.value = null
  }
}

function prevQuestion() {
  if (currentIndex.value > 0) {
    currentIndex.value--
    // 恢复之前选择的选项（根据之前保存的分值反向查找）
    const prevScore = answers.value[currentIndex.value]
    if (prevScore !== undefined) {
      const optIndex = currentQuestion.value.options.findIndex(o => (o.value ?? o.score) === prevScore)
      selectedOption.value = optIndex !== -1 ? optIndex : null
    } else {
      selectedOption.value = null
    }
  }
}

function finishTest() {
  // 计算总分（所有已保存的分值之和）
  const totalScore = answers.value.reduce((sum, s) => sum + s, 0)
  console.log('总分：', totalScore)

  // 根据总分匹配结果
  const matched = props.results.find(r => totalScore >= r.min && totalScore <= r.max)
  finalResult.value = matched || props.results[0] || { title: '未定义', desc: '请检查结果配置', icon: '❓' }

  // 🎯 关键逻辑：如果有 gameId，则发射 completed 事件给父组件保存成绩
  // 否则（官方测试）直接显示结果页
  if (props.gameId) {
    emit('completed', {
      totalScore: totalScore,
      result: finalResult.value,
      answers: answers.value
    })
  } else {
    // 官方测试：直接显示结果
    finished.value = true
  }
}

function resetTest() {
  started.value = false
  finished.value = false
  currentIndex.value = 0
  selectedOption.value = null
  answers.value = []
  finalResult.value = null
}

// 监听开始，初始化 answers 数组长度
watch(started, (val) => {
  if (val) {
    answers.value = new Array(props.questions.length).fill(undefined)
  }
})
</script>

<style scoped>
/* ===== 样式（墨划风格） ===== */
.test-game {
  width: 100%;
  max-width: 600px;
  margin: 0 auto;
  font-family: var(--font-family, 'Noto Serif SC', serif);
}
.test-start {
  text-align: center;
  padding: 40px 20px;
  border: 1px solid var(--line-raw, #D5CEC5);
  background: var(--paper-card, #FCFAF7);
}
.test-icon {
  font-size: 64px;
  margin-bottom: 16px;
}
.test-start h2 {
  font-size: 24px;
  font-weight: 400;
  letter-spacing: 0.2em;
  color: var(--ink-black, #2A2826);
  margin: 0 0 8px;
}
.test-start p {
  font-size: 14px;
  color: var(--ink-gray, #7A7570);
  letter-spacing: 0.1em;
  margin: 0 0 24px;
}
.btn-start {
  border-color: var(--cinnabar, #9E2A2B);
  color: var(--cinnabar);
  padding: 8px 32px;
  font-size: 16px;
}
.btn-start:hover {
  background: var(--cinnabar);
  color: #fff;
}
.test-questions {
  display: flex;
  flex-direction: column;
  gap: 20px;
}
.test-progress {
  display: flex;
  align-items: center;
  gap: 12px;
}
.progress-bar {
  flex: 1;
  height: 3px;
  background: var(--line-raw, #D5CEC5);
  border-radius: 2px;
  overflow: hidden;
}
.progress-fill {
  height: 100%;
  background: var(--cinnabar, #9E2A2B);
  transition: width 0.3s ease;
}
.progress-text {
  font-size: 13px;
  color: var(--ink-gray, #7A7570);
  letter-spacing: 0.1em;
}
.question-card {
  border: 1px solid var(--line-raw, #D5CEC5);
  padding: 24px 20px;
  background: var(--paper-card, #FCFAF7);
}
.question-text {
  font-size: 18px;
  line-height: 1.6;
  letter-spacing: 0.15em;
  color: var(--ink-black, #2A2826);
  margin-bottom: 12px;
}
.question-image {
  margin-bottom: 16px;
}
.question-image img {
  max-width: 100%;
  max-height: 200px;
  border: 1px solid var(--line-raw);
  border-radius: 4px;
}
.options {
  display: flex;
  flex-direction: column;
  gap: 10px;
  margin-bottom: 24px;
}
.option-btn {
  background: none;
  border: 1px solid var(--line-raw, #D5CEC5);
  padding: 12px 16px;
  text-align: left;
  font-size: 14px;
  font-family: inherit;
  letter-spacing: 0.1em;
  color: var(--ink-black, #2A2826);
  cursor: pointer;
  transition: all 0.2s;
}
.option-btn:hover {
  border-color: var(--ink-black);
}
.option-btn.selected {
  border-color: var(--cinnabar, #9E2A2B);
  background: rgba(158, 42, 43, 0.05);
}
.question-nav {
  display: flex;
  justify-content: space-between;
  gap: 12px;
}
.btn-line {
  background: none;
  border: 1px solid var(--line-raw, #D5CEC5);
  color: var(--ink-black, #2A2826);
  padding: 6px 20px;
  font-family: inherit;
  font-size: 13px;
  letter-spacing: 0.15em;
  cursor: pointer;
  transition: all 0.3s;
}
.btn-line:hover:not(:disabled) {
  border-color: var(--cinnabar);
  color: var(--cinnabar);
}
.btn-line:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}
.btn-next {
  border-color: var(--cinnabar);
  color: var(--cinnabar);
}
.btn-next:hover:not(:disabled) {
  background: var(--cinnabar);
  color: #fff;
}
.test-result {
  text-align: center;
  padding: 40px 20px;
  border: 1px solid var(--line-raw, #D5CEC5);
  background: var(--paper-card, #FCFAF7);
}
.result-content {
  max-width: 400px;
  margin: 0 auto;
}
.result-icon {
  font-size: 72px;
  margin-bottom: 12px;
}
.test-result h2 {
  font-size: 26px;
  font-weight: 400;
  letter-spacing: 0.2em;
  color: var(--ink-black, #2A2826);
  margin: 0 0 8px;
}
.result-desc {
  font-size: 15px;
  color: var(--ink-gray, #7A7570);
  letter-spacing: 0.1em;
  line-height: 1.8;
  margin: 0 0 12px;
}
.result-image {
  margin: 16px 0;
}
.result-image img {
  max-width: 100%;
  max-height: 200px;
  border: 1px solid var(--line-raw);
  border-radius: 4px;
}
.btn-restart {
  border-color: var(--line-raw);
}
.btn-restart:hover {
  border-color: var(--cinnabar);
  color: var(--cinnabar);
}
</style>