<template>
  <div class="question-home">
    <!-- 加载状态 -->
    <div v-if="loading" class="loading-state">
      <div class="loading-spinner"></div>
      <span>加载中...</span>
    </div>

    <!-- 未选择测试时显示列表 -->
    <div v-else-if="!currentTest" class="test-list">
      <div class="test-list-header">
        <h2>📜 心性测试集</h2>
        <p>选择一种心境，探索内在特质</p>
        <div class="test-divider"></div>
      </div>

      <div v-if="testList.length === 0" class="empty-state">
        <div class="empty-icon">📭</div>
        <h3>暂无测试</h3>
        <p>敬请期待更多心性测试</p>
      </div>

      <div v-else class="test-grid">
        <div
          v-for="test in testList"
          :key="test.id"
          class="test-card"
          @click="selectTest(test)"
        >
          <div class="test-card-icon">{{ test.icon }}</div>
          <h3 class="test-card-title">{{ test.title }}</h3>
          <p class="test-card-desc">{{ test.desc }}</p>
          <span class="test-card-status">{{ test.status }}</span>
        </div>
      </div>
    </div>

    <!-- 进入具体测试 -->
    <div v-else class="test-play">
      <button class="btn-back" @click="currentTest = null">← 返回列表</button>
      <TestGame
        :config="currentTest.config"
        :questions="currentTest.questions"
        :results="currentTest.results"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { getGames } from '../../games_api.ts'
import type { Game, GameQuestion, GameResult } from '../../game_types.ts'
import TestGame from './components/TestGame.vue'

// ===== 类型定义 =====
interface TestItem {
  id: number
  icon: string
  title: string
  desc: string
  status: '已发布' | '草稿'
  config: {
    icon: string
    title: string
    desc: string
  }
  questions: (GameQuestion & { id: number; order: number })[]
  results: (GameResult & { id: number; order: number })[]
}

// ===== 响应式状态 =====
const testList = ref<TestItem[]>([])
const currentTest = ref<TestItem | null>(null)
const loading = ref(false)

// ===== 获取数据 =====
const fetchTests = async () => {
  loading.value = true
  try {
    const res = await getGames({
      type: 'questionnaire',
      status: '已发布',
      page: 1,
      pageSize: 100
    })
    testList.value = res.items.map((game: Game) => ({
      id: game.id,
      icon: game.icon || '📋',
      title: game.title,
      desc: game.description || '',
      status: game.status,
      config: {
        icon: game.icon || '📋',
        title: game.title,
        desc: game.description || ''
      },
      questions: game.questionnaire?.questions || [],
      results: game.questionnaire?.results || []
    }))
  } catch (err) {
    console.error('加载测试列表失败', err)
  } finally {
    loading.value = false
  }
}

// ===== 选择测试 =====
function selectTest(test: TestItem) {
  if (test.questions.length === 0) {
    alert('该测试尚未完善，敬请期待')
    return
  }
  currentTest.value = test
}

// ===== 生命周期 =====
onMounted(() => {
  fetchTests()
})
</script>

<style scoped>
/* ===== 容器 ===== */
.question-home {
  width: 100%;
  max-width: 800px;
  margin: 0 auto;
  padding: 20px;
  font-family: var(--font-family, 'Noto Serif SC', serif);
}

/* ===== 加载状态 ===== */
.loading-state {
  text-align: center;
  padding: 60px 0;
  color: var(--ink-gray);
}
.loading-spinner {
  width: 36px;
  height: 36px;
  border: 2px solid var(--line-raw);
  border-top-color: var(--cinnabar);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin: 0 auto 12px;
}
@keyframes spin {
  to { transform: rotate(360deg); }
}

/* ===== 空状态 ===== */
.empty-state {
  text-align: center;
  padding: 60px 0;
  color: var(--ink-gray);
}
.empty-icon {
  font-size: 64px;
  margin-bottom: 16px;
}
.empty-state h3 {
  font-size: 22px;
  font-weight: 400;
  color: var(--ink-black);
  margin: 0 0 8px;
}
.empty-state p {
  font-size: 14px;
  letter-spacing: 0.1em;
}

/* ===== 列表样式 ===== */
.test-list-header {
  text-align: center;
  margin-bottom: 32px;
}
.test-list-header h2 {
  font-size: 26px;
  font-weight: 400;
  letter-spacing: 0.2em;
  color: var(--ink-black, #2A2826);
  margin: 0 0 8px;
}
.test-list-header p {
  font-size: 14px;
  color: var(--ink-gray, #7A7570);
  letter-spacing: 0.1em;
  margin: 0 0 12px;
}
.test-divider {
  width: 60px;
  height: 1px;
  background: var(--line-raw, #D5CEC5);
  margin: 0 auto;
}

.test-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 24px;
}
.test-card {
  border: 1px solid var(--line-raw, #D5CEC5);
  padding: 24px 16px;
  text-align: center;
  background: var(--paper-card, #FCFAF7);
  cursor: pointer;
  transition: all 0.3s ease;
}
.test-card:hover {
  border-color: var(--cinnabar, #9E2A2B);
  transform: translateY(-4px);
  box-shadow: 0 4px 12px rgba(0,0,0,0.04);
}
.test-card-icon {
  font-size: 40px;
  margin-bottom: 12px;
  display: block;
}
.test-card-title {
  font-size: 18px;
  font-weight: 400;
  letter-spacing: 0.15em;
  color: var(--ink-black, #2A2826);
  margin: 0 0 8px;
}
.test-card-desc {
  font-size: 13px;
  color: var(--ink-gray, #7A7570);
  letter-spacing: 0.1em;
  line-height: 1.6;
  margin: 0 0 12px;
}
.test-card-status {
  font-size: 12px;
  padding: 2px 12px;
  border: 1px solid var(--line-raw);
  color: var(--ink-gray);
  letter-spacing: 0.1em;
  display: inline-block;
}

/* ===== 答题界面 ===== */
.test-play {
  width: 100%;
}
.btn-back {
  background: none;
  border: none;
  color: var(--ink-gray, #7A7570);
  font-family: inherit;
  font-size: 14px;
  letter-spacing: 0.15em;
  cursor: pointer;
  padding: 8px 0;
  margin-bottom: 16px;
  transition: color 0.3s;
}
.btn-back:hover {
  color: var(--cinnabar, #9E2A2B);
}
</style>