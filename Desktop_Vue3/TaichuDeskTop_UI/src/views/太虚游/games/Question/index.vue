<template>
  <div class="question-home">
    <!-- 答题中 -->
    <div v-if="current" class="test-play">
      <QuestionPlay :game="current" @back="current = null" />
    </div>

    <div v-else class="test-list">
      <div class="test-list-header">
        <h2>📜 心性测试集</h2>
        <p>选择一种心境，探索内在特质。也可以去「自定义工坊」出一套自己的测试</p>
        <div class="test-divider"></div>
      </div>

      <!-- 搜索 / 排序 -->
      <div class="test-tools">
        <input v-model="keyword" type="search" maxlength="30" placeholder="搜测试名字…" aria-label="搜索测试" @keyup.enter="fetchTests" />
        <div class="test-sort" role="group" aria-label="排序">
          <button type="button" :class="{ on: sort === 'new' }" @click="setSort('new')">最新</button>
          <button type="button" :class="{ on: sort === 'hot' }" @click="setSort('hot')">最热</button>
        </div>
      </div>

      <div v-if="loading" class="loading-state">
        <div class="loading-spinner"></div>
        <span>加载中...</span>
      </div>

      <p v-else-if="loadError" class="empty-state" role="alert">{{ loadError }} <button type="button" class="btn-back" @click="fetchTests">重试</button></p>

      <div v-else-if="testList.length === 0" class="empty-state">
        <div class="empty-icon">📭</div>
        <h3>{{ keyword.trim() ? '没有找到相关测试' : '暂无测试' }}</h3>
        <p>{{ keyword.trim() ? '换个关键词试试' : '敬请期待更多心性测试' }}</p>
      </div>

      <div v-else class="test-grid">
        <button v-for="test in testList" :key="test.id" type="button" class="test-card" :disabled="opening === test.id" @click="open(test)">
          <div class="test-card-icon">{{ test.icon || '📋' }}</div>
          <h3 class="test-card-title">{{ test.title }}</h3>
          <p class="test-card-desc">{{ test.description }}</p>
          <span class="test-card-status">{{ opening === test.id ? '正在打开…' : `${test.questionCount ?? 0} 题 · ${test.playCount} 人次测过` }}</span>
          <span v-if="test.creatorName" class="test-card-author">{{ test.creatorName }} 出题</span>
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
// 心性测试大厅：列表只有标题等信息（不带题目），点开时再按编号取完整题目（以前直接用列表里的题目，永远是空的，所以每个测试都打不开）。
import { ref, onMounted } from 'vue'
import { getErrorMessage } from '@/utils/apiError'
import { getGame, getGames } from '../../games_api'
import type { Game } from '../../game_types'
import QuestionPlay from './QuestionPlay.vue'

const testList = ref<Game[]>([])
const current = ref<Game | null>(null)
const loading = ref(false)
const loadError = ref('')
const opening = ref<number | null>(null)
const keyword = ref('')
const sort = ref<'new' | 'hot'>('new')

async function fetchTests() {
  loading.value = true
  loadError.value = ''
  try {
    const res = await getGames({ type: 'questionnaire', status: '已发布', page: 1, pageSize: 100, sort: sort.value === 'hot' ? 'hot' : undefined, q: keyword.value.trim() || undefined })
    testList.value = res.items
  } catch (err) {
    loadError.value = getErrorMessage(err, '测试列表加载失败')
  } finally {
    loading.value = false
  }
}
function setSort(v: 'new' | 'hot') { if (sort.value !== v) { sort.value = v; void fetchTests() } }

async function open(test: Game) {
  opening.value = test.id
  try {
    const full = await getGame(test.id)
    if (!full.questionnaire?.questions?.length) { alert('这个测试还没有题目，敬请期待'); return }
    current.value = full
  } catch (err) {
    alert(getErrorMessage(err, '打开失败，请稍后再试'))
  } finally {
    opening.value = null
  }
}

onMounted(fetchTests)
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

/* 搜索 / 排序 / 卡片补充 */
.test-tools { display: flex; flex-wrap: wrap; gap: 10px; justify-content: space-between; margin-bottom: 20px; }
.test-tools input { flex: 1; min-width: 180px; padding: 8px 12px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 14px; }
.test-sort { display: flex; }
.test-sort button { padding: 8px 14px; border: 1px solid var(--border-line); background: transparent; color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.test-sort button + button { border-left: 0; }
.test-sort button.on { background: var(--accent-color); border-color: var(--accent-color); color: var(--on-accent, #fff); }
button.test-card { font: inherit; color: inherit; width: 100%; }
.test-card-author { display: block; margin-top: 8px; font-size: 12px; color: var(--text-secondary); }
</style>
