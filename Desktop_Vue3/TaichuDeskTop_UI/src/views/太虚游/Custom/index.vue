<template>
  <div class="custom-studio workbench-skin editor-surface community-surface tone-play">
    <!-- ========== 列表视图 ========== -->
    <template v-if="view === 'list'">
      <header class="studio-header studio-heading">
  <div class="header-left">
    <h2>✏️ 自定义工坊</h2>
    <p>在这里，你可以创作、编辑和管理自己的专属游戏</p>
  </div>
  <div class="header-right">
    <!-- ✅ 经验显示 -->
    <div class="exp-display" v-if="userExp !== null">
      <span class="exp-icon">✨</span>
      <span class="exp-value">{{ userExp.toLocaleString() }}</span>
      <span class="exp-label">经验</span>
      <span class="exp-cost">
        创建消耗 <strong>{{ createCost }}</strong>
      </span>
    </div>
    <button class="btn-create" @click="view = 'select-type'">+ 创建新游戏</button>
  </div>
</header>

      <div v-if="loading" class="loading-state">
        <div class="loading-spinner"></div>
        <span>加载中...</span>
      </div>

      <div v-else-if="gameList.length === 0" class="empty-state">
        <div class="empty-icon">🛠️</div>
        <h3>尚未创建任何游戏</h3>
        <p>点击「创建新游戏」开始你的第一个作品</p>
      </div>

      <div v-else class="game-grid">
        <div v-for="game in gameList" :key="game.id" class="game-card">
          <div class="card-header">
            <span class="game-icon">{{ game.icon || '🎮' }}</span>
            <span class="game-status" :class="game.status === '已发布' ? 'status-published' : 'status-draft'">
              {{ game.status }}
            </span>
          </div>
          <h3 class="game-title">{{ game.title }}</h3>
          <p class="game-desc">{{ game.description }}</p>
          <div class="card-meta" v-if="game.updatedAt">
            <span>更新于 {{ formatDate(game.updatedAt) }} · {{ game.questionCount ?? 0 }} 题 · 被测 {{ game.playCount }} 次</span>
          </div>
          <div class="card-actions">
            <button class="btn-line btn-edit" @click="editGame(game.id)">编辑</button>
            <!-- 发布按钮：仅草稿状态显示 -->
            <button v-if="game.status === '草稿'" class="btn-line btn-publish" @click="publishGame(game.id)">发布</button>
            <button class="btn-line btn-delete" @click="deleteGame(game.id)">删除</button>
            <button class="btn-line btn-play" @click="playGame(game.id)">试玩</button>
          </div>
        </div>
      </div>
    </template>

    <!-- ========== 选择类型视图 ========== -->
    <template v-else-if="view === 'select-type'">
      <div class="select-type">
        <header class="select-header studio-heading">
          <button class="btn-back" @click="view = 'list'">← 返回</button>
          <h2>选择游戏类型</h2>
          <p>目前支持以下类型，请选择一项</p>
        </header>
        <div class="type-grid">
          <button type="button"
            v-for="type in availableTypes"
            :key="type.value"
            class="type-card"
            :class="{ 'coming-soon': type.comingSoon }" :disabled="type.comingSoon"
            @click="!type.comingSoon && selectType(type.value)"
          >
            <div class="type-icon">{{ type.icon }}</div>
            <h3 class="type-title">{{ type.label }}</h3>
            <p class="type-desc">{{ type.desc }}</p>
            <span v-if="type.comingSoon" class="type-badge">即将支持</span>
          </button>
        </div>
      </div>
    </template>

    <!-- ========== 创建视图（动态组件） ========== -->
    <template v-else-if="view === 'create'">
      <component
        :is="createComponent"
        :edit-data="editGameData"
        :edit-game-id="editGameId"
        @cancel="cancelEdit"
        @success="handleCreateSuccess"
      />
    </template>

    <!-- ========== 试玩模态框 ========== -->
    <Teleport to="body">
      <div v-if="showPlayModal" class="modal-overlay" @click.self="showPlayModal = false">
        <div class="modal-content">
          <button class="modal-close" @click="showPlayModal = false">✕</button>
          <QuestionPlay v-if="playingGame" :game="playingGame" :show-back="false" @completed="onGameCompleted" />
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed, defineAsyncComponent } from 'vue'
import { getMyGames, getGame, createGame, updateGame, deleteGame as deleteGameApi, getGameConfig } from '../games_api.ts'
import type { Game, CreateGameDto, UpdateGameDto } from '../game_types.ts'
// 答题页换成新的 QuestionPlay（会自己交卷记成绩）；旧的 TestGame.vue 不再使用
import QuestionPlay from '../games/Question/QuestionPlay.vue'
import { getErrorMessage } from '@/utils/apiError'
import { TradeApi } from '@/api/trade'  // 根据实际路径调整

const userExp = ref<number>(0)
const createCost = ref<number>(50)

// ===== 类型定义 =====
interface AvailableType {
  value: string
  icon: string
  label: string
  desc: string
  comingSoon: boolean
}


// ===== 动态组件映射 =====
const componentMap: Record<string, any> = {
  questionnaire: defineAsyncComponent(() => import('./components/CreateQuestionnaire.vue')),
  simulation: defineAsyncComponent(() => import('./components/CreateSimulation.vue'))
}

// ===== 响应式状态 =====
const view = ref<'list' | 'select-type' | 'create'>('list')
const selectedType = ref<string>('')
const editGameId = ref<number | null>(null)
const editGameData = ref<Game | null>(null)
const createComponent = computed(() => componentMap[selectedType.value] || null)

const gameList = ref<Game[]>([])
const loading = ref(false)

// 试玩相关
const showPlayModal = ref(false)
const playingGame = ref<Game | null>(null)

// ===== 可用类型 =====
const availableTypes: AvailableType[] = [
  {
    value: 'questionnaire',
    icon: '📋',
    label: '问卷测试',
    desc: '创建选择题形式的测试游戏，可自定义题目、选项和结果',
    comingSoon: false
  },
  {
    value: 'simulation',
    icon: '🌱',
    label: '模拟游戏',
    desc: '创建文字模拟类游戏，如人生模拟器、经营模拟等',
    comingSoon: true
  }
]

// ===== 方法 =====

const fetchGames = async () => {
  loading.value = true
  try {
    const res = await getMyGames({ page: 1, pageSize: 20 })
    gameList.value = res.items
  } catch (err) {
    console.error('加载失败', err)
  } finally {
    loading.value = false
  }
}

const selectType = (type: string) => {
  selectedType.value = type
  editGameId.value = null
  editGameData.value = null
  view.value = 'create'
}

// ===== 编辑游戏 =====
const editGame = async (id: number) => {
  try {
    // 从列表中找到基础数据（仅用于显示，不用于编辑）
    const baseGame = gameList.value.find(g => g.id === id)
    if (!baseGame) {
      alert('游戏不存在')
      return
    }

    // 调用详情接口获取完整数据（包含 questionnaire、questions、options、results）
    const fullGame = await getGame(id)

    if (!fullGame.questionnaire) {
      alert('游戏数据不完整，无法编辑')
      return
    }

    // 保存编辑数据，切换到创建视图
    editGameId.value = id
    editGameData.value = fullGame
    selectedType.value = fullGame.type || 'questionnaire'
    view.value = 'create'
  } catch (err) {
    console.error('加载游戏详情失败', err)
    alert('加载游戏详情失败，请稍后重试')
  }
}

// ===== 取消编辑 =====
const cancelEdit = () => {
  view.value = 'list'
  editGameId.value = null
  editGameData.value = null
}

// ===== 创建/更新成功回调 =====
const handleCreateSuccess = async (newGame: CreateGameDto) => {
  try {
    if (editGameId.value) {
      // 🔄 编辑模式：调用更新接口
      // 题目和结果一起提交（以前只提交标题，题目的修改会被丢掉）
      const updateData: UpdateGameDto = {
        title: newGame.title,
        description: newGame.description,
        icon: newGame.icon,
        scoring: newGame.scoring,
        questions: newGame.questions,
        results: newGame.results
      }
      const updated = await updateGame(editGameId.value, updateData)
      const index = gameList.value.findIndex(g => g.id === editGameId.value)
      if (index !== -1) {
        gameList.value[index] = updated
      }
    } else {
      // ➕ 创建模式
      await createGame(newGame)
      void fetchUserExp()
    }
    view.value = 'list'
    editGameId.value = null
    editGameData.value = null
    await fetchGames()
  } catch (err: any) {
    // 以前失败只打在控制台，用户以为保存了
    alert(getErrorMessage(err, '保存失败，请稍后再试'))
  }
}

// ===== 发布游戏 =====
const publishGame = async (id: number) => {
  if (!confirm('确认发布该游戏吗？发布后玩家可以试玩。')) return

  try {
    const updated = await updateGame(id, { status: '已发布' })
    const index = gameList.value.findIndex(g => g.id === id)
    if (index !== -1) {
      gameList.value[index] = updated
    }
    alert('✅ 发布成功！')
  } catch (err) {
    alert(getErrorMessage(err, '发布失败'))
  }
}

// ===== 删除游戏 =====
const deleteGame = async (id: number) => {
  if (confirm('⚠️ 确认删除该游戏吗？此操作不可恢复。')) {
    try {
      await deleteGameApi(id)
      gameList.value = gameList.value.filter((g: Game) => g.id !== id)
    } catch (err) {
      alert(getErrorMessage(err, '删除失败'))
    }
  }
}

// ===== 试玩 =====
// 列表里没有题目，试玩前按编号取完整内容
const playGame = async (id: number) => {
  try {
    const game = await getGame(id)
    if (!game.questionnaire?.questions?.length) {
      alert('还没有题目，先编辑加几道题吧')
      return
    }
    playingGame.value = game
    showPlayModal.value = true
  } catch (err) {
    alert(getErrorMessage(err, '打开失败'))
  }
}

// ===== 试玩完成保存成绩 =====
// 成绩由 QuestionPlay 交卷时保存，这里只刷新列表（更新被测次数）
const onGameCompleted = async () => {
  await fetchGames()
}

const formatDate = (dateStr: string): string => {
  const d = new Date(dateStr)
  return d.toLocaleDateString('zh-CN')
}

// ===== 获取用户经验（通过 TradeApi） =====
const fetchUserExp = async () => {
  // 创建价格以后台配置为准（以前写死 50）
  try {
    const cfg = await getGameConfig()
    createCost.value = cfg.createCost
    if (cfg.experience != null) { userExp.value = cfg.experience; return }
  } catch { /* 后端还没更新时用下面的旧办法 */ }
  try {
    const res = await TradeApi.getAccountStatus()
    userExp.value = res.experience || 0
  } catch (err) {
    console.error('获取经验失败', err)
  }
}


// ===== 生命周期 =====
onMounted(() => {
  fetchGames()
  fetchUserExp()
})
</script>

<style scoped>
/* ===== 全局容器 ===== */
.custom-studio {
  width: 100%;
  max-width: 960px;
  margin: 0 auto;
  padding: 24px;
  font-family: inherit;
}

/* ===== 列表视图 ===== */
.studio-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  border-bottom: 1px solid var(--line-raw, var(--border-line));
  padding-bottom: 18px;
  margin-bottom: 32px;
  flex-wrap: wrap;
  gap: 12px 20px;
}
.header-left h2 {
  font-size: 26px;
  font-weight: 400;
  letter-spacing: .02em;
  color: var(--ink-black, var(--text-primary));
  margin: 0 0 4px;
}
.header-left p {
  font-size: 14px;
  color: var(--ink-gray, var(--text-secondary));
  letter-spacing: 0.1em;
  margin: 0;
}

/* ===== 头部右侧（经验 + 创建按钮） ===== */
.header-right {
  display: flex;
  align-items: center;
  gap: 16px;
  flex-wrap: wrap;
  margin-top: 4px;
}

/* ===== 经验显示卡片 ===== */
.exp-display {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 16px;
  border: 1px solid var(--line-raw, var(--border-line));
  border-radius: 0;
  background: var(--paper-card, var(--bg-main));
  font-size: 14px;
  white-space: nowrap;
  transition: border-color 0.3s, background 0.3s;
}
.exp-display:hover {
  border-color: var(--accent-color);
}
.exp-icon {
  font-size: 18px;
  line-height: 1;
}
.exp-value {
  font-weight: 600;
  color: var(--accent-color);
  font-variant-numeric: tabular-nums;
}
.exp-label {
  color: var(--ink-gray, var(--text-secondary));
  font-size: 13px;
  margin-left: 2px;
}
.exp-cost {
  color: var(--ink-gray, var(--text-secondary));
  font-size: 13px;
  margin-left: 4px;
  padding-left: 8px;
  border-left: 1px solid var(--line-raw, var(--border-line));
}
.exp-cost strong {
  color: var(--danger);
  font-weight: 600;
}

/* ===== 创建按钮 ===== */
.btn-create {
  background: var(--accent-color);
  color: var(--on-accent);
  border: none;
  padding: 8px 28px;
  font-family: inherit;
  font-size: 15px;
  letter-spacing: 0.15em;
  cursor: pointer;
  transition: background 0.3s, transform 0.15s;
  flex-shrink: 0;
  border-radius: 0;
}
.btn-create:hover {
  background: var(--danger);
}
.btn-create:active {
  transform: scale(0.97);
}

/* ===== 加载状态 ===== */
.loading-state {
  text-align: center;
  padding: 60px 0;
  color: var(--text-secondary);
}
.loading-spinner {
  width: 36px;
  height: 36px;
  border: 2px solid var(--border-line);
  border-top-color: var(--accent-color);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin: 0 auto 12px;
}
@keyframes spin {
  to { transform: rotate(360deg); }
}
.empty-state {
  text-align: center;
  padding: 60px 0;
  color: var(--text-secondary);
}
.empty-icon {
  font-size: 64px;
  margin-bottom: 16px;
}
.empty-state h3 {
  font-size: 22px;
  font-weight: 400;
  color: var(--text-primary);
  margin: 0 0 8px;
}
.empty-state p {
  font-size: 14px;
  letter-spacing: 0.1em;
}

/* ===== 游戏卡片网格 ===== */
.game-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 24px;
}
.game-card {
  border: 1px solid var(--line-raw, var(--border-line));
  padding: 18px 16px 16px;
  background: var(--paper-card, var(--bg-main));
  transition: border-color 0.3s, box-shadow 0.3s, transform 0.2s;
  display: flex;
  flex-direction: column;
  border-radius: 0;
}
.game-card:hover {
  border-color: var(--accent-color);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.04);
  transform: translateY(-2px);
}
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
}
.game-icon {
  font-size: 28px;
  line-height: 1;
}
.game-status {
  font-size: 11px;
  padding: 0 10px;
  border: 1px solid var(--border-line);
  border-radius: 0;
  letter-spacing: 0.1em;
  color: var(--text-secondary);
  line-height: 20px;
}
.status-published {
  border-color: var(--success);
  color: var(--success);
}
.status-draft {
  border-color: var(--border-line);
  color: var(--text-secondary);
}
.game-title {
  font-size: 18px;
  font-weight: 400;
  letter-spacing: 0.15em;
  color: var(--text-primary);
  margin: 0 0 6px;
}
.game-desc {
  font-size: 13px;
  color: var(--text-secondary);
  letter-spacing: 0.1em;
  line-height: 1.6;
  margin: 0 0 10px;
  flex: 1;
}
.card-meta {
  font-size: 11px;
  color: var(--text-secondary);
  opacity: 0.6;
  letter-spacing: 0.05em;
  margin-bottom: 12px;
}

/* ===== 卡片操作按钮组 ===== */
.card-actions {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}
.btn-line {
  background: none;
  border: 1px solid var(--line-raw, var(--border-line));
  color: var(--text-primary);
  padding: 4px 12px;
  font-family: inherit;
  font-size: 12px;
  letter-spacing: 0.1em;
  cursor: pointer;
  transition: all 0.2s;
  flex: 1;
  text-align: center;
  border-radius: 0;
  min-width: 50px;
}
.btn-line:hover {
  border-color: var(--accent-color);
  color: var(--accent-color);
}
.btn-edit:hover {
  border-color: var(--border-line);
  color: var(--text-secondary);
}
.btn-delete:hover {
  border-color: var(--danger);
  color: var(--danger);
}
.btn-play:hover {
  border-color: var(--success);
  color: var(--success);
}
.btn-publish {
  border-color: var(--success);
  color: var(--success);
}
.btn-publish:hover {
  background: var(--success);
  color: var(--on-accent);
}

/* ===== 选择类型视图 ===== */
.select-type {
  max-width: 600px;
  margin: 0 auto;
}
.select-header {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 24px;
  border-bottom: 1px solid var(--border-line);
  padding-bottom: 12px;
}
.btn-back {
  background: none;
  border: none;
  color: var(--text-secondary);
  font-family: inherit;
  font-size: 14px;
  cursor: pointer;
  padding: 4px 8px;
  transition: color 0.3s;
}
.btn-back:hover {
  color: var(--accent-color);
}
.select-header h2 {
  font-size: 22px;
  font-weight: 400;
  letter-spacing: .02em;
  color: var(--text-primary);
  margin: 0;
}
.select-header p {
  font-size: 14px;
  color: var(--text-secondary);
  letter-spacing: 0.1em;
  margin: 0 0 0 auto;
}
.type-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
}
.type-card {
  border: 1px solid var(--border-line);
  padding: 24px 20px;
  background: var(--paper-card);
  text-align: center;
  cursor: pointer;
  transition: all 0.3s;
  position: relative;
  border-radius: 0;
}
.type-card:hover:not(.coming-soon) {
  border-color: var(--accent-color);
  transform: translateY(-4px);
  box-shadow: 0 4px 12px rgba(0,0,0,0.04);
}
.type-card.coming-soon {
  opacity: 0.6;
  cursor: not-allowed;
  filter: grayscale(0.3);
}
.type-card.coming-soon:hover {
  transform: none;
  border-color: var(--border-line);
  box-shadow: none;
}
.type-icon {
  font-size: 40px;
  margin-bottom: 12px;
  display: block;
}
.type-title {
  font-size: 18px;
  font-weight: 400;
  letter-spacing: 0.15em;
  color: var(--text-primary);
  margin: 0 0 6px;
}
.type-desc {
  font-size: 13px;
  color: var(--text-secondary);
  letter-spacing: 0.1em;
  margin: 0;
}
.type-badge {
  position: absolute;
  top: 8px;
  right: 8px;
  background: var(--accent-color);
  color: var(--text-primary);
  font-size: 10px;
  padding: 0 8px;
  border-radius: 0;
  line-height: 18px;
  letter-spacing: 0.05em;
}

/* ===== 试玩模态框 ===== */
.modal-overlay {
  position: fixed;
  top: 0; left: 0;
  width: 100%; height: 100%;
  background: var(--overlay);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 999;
  backdrop-filter: blur(2px);
}
.modal-content {
  background: var(--paper-bg, var(--bg-sub));
  border: 1px solid var(--border-line);
  max-width: 800px;
  width: 92%;
  max-height: 90vh;
  overflow-y: auto;
  padding: 24px 30px 30px;
  position: relative;
  border-radius: 0;
  box-shadow: 0 8px 32px rgba(0,0,0,0.12);
}
.modal-close {
  position: absolute;
  top: 12px;
  right: 16px;
  background: none;
  border: none;
  font-size: 24px;
  color: var(--text-secondary);
  cursor: pointer;
  transition: color 0.3s;
  padding: 0 4px;
}
.modal-close:hover {
  color: var(--accent-color);
}

/* ===== 响应式适配 ===== */
@media (max-width: 640px) {
  .studio-header {
    flex-direction: column;
    align-items: stretch;
    gap: 12px;
  }
  .header-left h2 {
    font-size: 22px;
  }
  .header-left p {
    font-size: 13px;
  }
  .header-right {
    width: 100%;
    justify-content: flex-start;
    gap: 12px;
  }
  .exp-display {
    font-size: 13px;
    padding: 4px 12px;
    flex-wrap: wrap;
  }
  .exp-cost {
    border-left: none;
    padding-left: 0;
    margin-left: 0;
  }
  .btn-create {
    font-size: 14px;
    padding: 6px 20px;
  }
  .type-grid {
    grid-template-columns: 1fr;
  }
  .game-grid {
    grid-template-columns: 1fr 1fr;
    gap: 16px;
  }
  .modal-content {
    padding: 16px;
    width: 96%;
  }
}
@media (max-width: 420px) {
  .game-grid {
    grid-template-columns: 1fr;
  }
  .exp-display {
    font-size: 12px;
    padding: 4px 10px;
    gap: 4px;
  }
  .exp-icon {
    font-size: 16px;
  }
}
.custom-studio { box-sizing: border-box; }.type-card { font: inherit; text-align: left; color: var(--text-primary); }.type-card:disabled { cursor: default; }
@media(max-width:640px) { .custom-studio { padding: 12px; }.header-right { width: 100%; }.exp-display { flex-wrap: wrap; white-space: normal; }.type-grid { grid-template-columns: 1fr; } }
</style>
