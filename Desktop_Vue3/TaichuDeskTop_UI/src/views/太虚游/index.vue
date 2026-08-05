<template>
  <div class="game-hub" :class="{ 'theme-aged': isAged }">
    <!-- 顶部品牌栏 -->
    <header class="hub-header">
      <div class="brand">
        <span class="brand-icon">⚡</span>
        <span class="brand-name">太虚游 · 游戏集所</span>
      </div>
      <div class="header-controls">
        <button class="btn-line" @click="togglePaper">
          {{ isAged ? '新纸' : '旧纸' }}
        </button>
        <span v-if="isConnected" class="connection-status online">● 已连接</span>
        <span v-else class="connection-status offline">○ 离线</span>
      </div>
    </header>

    <div class="hub-body">
      <!-- 左侧导航 – 分类分组 -->
      <aside class="hub-sidebar">
        <nav class="game-nav">
          <div v-for="(group, groupKey) in groupedModules" :key="groupKey" class="nav-group">
            <div class="nav-group-title">{{ group.label }}</div>
            <div
              v-for="item in group.items"
              :key="item.key"
              class="nav-item"
              :class="{ active: currentGameKey === item.key }"
              @click="loadGame(item.key)"
            >
              <span class="nav-icon">{{ item.icon }}</span>
              <span class="nav-label">{{ item.label }}</span>
              <span v-if="item.isNew" class="badge-new">新</span>
            </div>
          </div>
        </nav>

        <!-- 侧边统计信息 -->
        <div class="sidebar-stats">
          <div class="stat-item">
            <span class="stat-value">{{ gameCount }}</span>
            <span class="stat-label">游戏总数</span>
          </div>
          <div class="stat-item">
            <span class="stat-value">{{ onlineCount }}</span>
            <span class="stat-label">在线道友</span>
          </div>
        </div>
      </aside>

      <!-- 主内容区 -->
      <main class="hub-main">
        <div v-if="!currentGameComponent" class="welcome-placeholder">
          <div class="welcome-icon">🎮</div>
          <h2>欢迎来到太虚游</h2>
          <p>请从左侧选择一款游戏，开启你的修行之旅</p>
          <div class="welcome-hint">—— 线条无界 · 呼吸自然 ——</div>
        </div>

        <Suspense v-else>
          <component
            ref="currentGameRef"
            :is="currentGameComponent"
            :key="currentGameKey"
          />
          <template #fallback>
            <div class="loading-state">
              <div class="loading-spinner"></div>
              <span>游戏加载中 ...</span>
            </div>
          </template>
        </Suspense>
      </main>
    </div>

    <!-- 页脚 -->
    <footer class="hub-footer">
      <span>太虚游 · 墨划风格设计</span>
      <span class="footer-sub">v2.0 · 自在随心</span>
    </footer>
  </div>
</template>

<script setup>
import { ref, shallowRef, defineAsyncComponent, onMounted, onUnmounted, provide, computed } from 'vue'
import * as signalR from '@microsoft/signalr'

// ===== 游戏模块注册 =====
const gameModules = {
  // ----- 棋类 -----
  Gobang: {
    label: '五子棋',
    category: 'chess',
    icon: '⬛',
    description: '经典五子棋对弈，支持多人联机',
    status: '已上线',
    component: defineAsyncComponent(() => import('./games/Gobang/index.vue'))
  },
  // ----- 经典小游戏 -----
  Tetris: {
    label: '俄罗斯方块',
    category: 'classic',
    icon: '🧩',
    description: '经典的俄罗斯方块，考验你的反应速度',
    status: '开发中',
    component: defineAsyncComponent(() => import('./games/Tetris/index.vue'))
  },
  Snake: {
    label: '贪吃蛇',
    category: 'classic',
    icon: '🐍',
    description: '经典贪吃蛇游戏，操作简单乐趣无穷',
    status: '开发中',
    component: defineAsyncComponent(() => import('./games/TempHolder.vue'))
  },
  // ----- 测试游戏 -----
  TestGame: {
    label: '测试类问卷游戏',
    category: 'test',
    icon: '🖌️',
    description: '通过情境抉择，探寻你的内在特质',
    status: '已上线',
    component: defineAsyncComponent(() => import('./games/Question/index.vue'))
  },
  // ----- 自定义工坊 ✅ 已上线 -----
  CustomStudio: {
    label: '自定义设计',
    category: 'custom',
    icon: '✏️',
    isNew: true,
    description: '自由创作你的专属游戏',
    status: '已上线',  // ← 改为 已上线
    component: defineAsyncComponent(() => import('./Custom/index.vue'))
  }
}

// 分类定义
const categoryMap = {
  chess: { label: '棋类对弈', order: 1 },
  classic: { label: '经典小游戏', order: 2 },
  test: { label: '测试问卷', order: 3 },
  custom: { label: '自定义工坊', order: 4 }
}

// 分组后的导航数据
const groupedModules = computed(() => {
  const groups = {}
  for (const [key, mod] of Object.entries(gameModules)) {
    const cat = mod.category || 'other'
    if (!groups[cat]) {
      groups[cat] = {
        label: categoryMap[cat]?.label || cat,
        items: []
      }
    }
    groups[cat].items.push({ key, ...mod })
  }
  const sorted = Object.keys(groups).sort((a, b) => (categoryMap[a]?.order || 99) - (categoryMap[b]?.order || 99))
  const result = {}
  for (const key of sorted) {
    result[key] = groups[key]
  }
  return result
})

// ===== 页面状态 =====
const currentGameKey = ref(null)
const currentGameComponent = shallowRef(null)
const currentGameRef = shallowRef(null)

// ===== 纸色切换 =====
const isAged = ref(false)
function togglePaper() {
  isAged.value = !isAged.value
  const root = document.documentElement
  if (isAged.value) {
    root.style.setProperty('--paper-bg', '#EAE4D6')
    root.style.setProperty('--paper-card', '#F0EBDF')
    root.style.setProperty('--paper-sub', '#E5DFD0')
    root.style.setProperty('--ink-black', '#2A2826')
    root.style.setProperty('--ink-gray', '#7A7570')
    root.style.setProperty('--line-raw', '#C8BFB3')
  } else {
    root.style.setProperty('--paper-bg', '#F7F4EE')
    root.style.setProperty('--paper-card', '#FCFAF7')
    root.style.setProperty('--paper-sub', '#F0EBE1')
    root.style.setProperty('--ink-black', '#2A2826')
    root.style.setProperty('--ink-gray', '#7A7570')
    root.style.setProperty('--line-raw', '#D5CEC5')
  }
}

// ===== SignalR 连接（五子棋联机） =====
const connection = ref(null)
const isConnected = ref(false)
const currentRoomId = ref(null)

const createRoom = async (roomName, mode, boardSize, timeLimit = 30) => {
  if (!connection.value || !isConnected.value) {
    alert('未连接到服务器')
    return
  }
  try {
    await connection.value.invoke('CreateRoom', roomName, mode, boardSize, timeLimit)
  } catch (err) {
    console.error('创建房间失败：', err)
    alert('创建房间失败：' + err.message)
  }
}

const joinRoom = async (roomId) => {
  if (!connection.value || !isConnected.value) {
    alert('未连接到服务器')
    return
  }
  try {
    await connection.value.invoke('JoinRoom', roomId)
  } catch (err) {
    console.error('加入房间失败：', err)
    alert('加入房间失败：' + err.message)
  }
}

const startGame = async (roomId) => {
  if (!connection.value || !isConnected.value) {
    alert('未连接到服务器')
    return
  }
  try {
    await connection.value.invoke('StartGame', roomId)
  } catch (err) {
    console.error('开始游戏失败：', err)
    alert('开始游戏失败：' + err.message)
  }
}

const makeMove = async (roomId, row, col) => {
  if (!connection.value || !isConnected.value) {
    alert('未连接到服务器')
    return
  }
  try {
    await connection.value.invoke('MakeMove', roomId, row, col)
  } catch (err) {
    console.error('落子失败：', err)
  }
}

const initSignalR = async () => {
  const hubUrl = 'https://bianyuzhou.com/signalr/game'
  connection.value = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: () => localStorage.getItem('token') || ''
    })
    .withAutomaticReconnect()
    .build()

  connection.value.on('RoomCreated', (data) => {
    console.log('✅ 房间创建成功：', data)
    currentRoomId.value = data.roomId
    window.dispatchEvent(new CustomEvent('room-created', { detail: data }))
  })
  connection.value.on('PlayerJoined', (data) => {
    console.log('👤 玩家加入：', data)
    window.dispatchEvent(new CustomEvent('player-joined', { detail: data }))
  })
  connection.value.on('MoveMade', (data) => {
    console.log('♟️ 落子：', data)
    window.dispatchEvent(new CustomEvent('move-made', { detail: data }))
  })
  connection.value.on('GameStarted', (data) => {
    console.log('🎮 游戏开始：', data)
    window.dispatchEvent(new CustomEvent('game-started', { detail: data }))
  })
  connection.value.on('Error', (data) => {
    console.error('❌ 服务器错误：', data)
    alert(data.message || '发生错误')
  })
  connection.value.on('PlayerLeft', (data) => {
    console.log('👋 玩家离开：', data)
    alert('对手已离开房间')
    window.dispatchEvent(new CustomEvent('player-left', { detail: data }))
  })

  try {
    await connection.value.start()
    isConnected.value = true
    console.log('✅ SignalR 连接成功')
  } catch (err) {
    console.error('❌ SignalR 连接失败：', err)
  }
}

provide('signalR', {
  connection,
  isConnected,
  currentRoomId,
  createRoom,
  joinRoom,
  startGame,
  makeMove
})

// ===== 游戏切换逻辑 =====
const loadGame = async (key) => {
  const instance = currentGameRef.value
  if (instance && typeof instance.handleBeforeLeave === 'function') {
    const canLeave = await instance.handleBeforeLeave()
    if (canLeave === false) return
  }

  const mod = gameModules[key]
  if (mod) {
    currentGameKey.value = key
    currentGameComponent.value = mod.component
  }
}

// ===== 统计信息 =====
const gameCount = computed(() => Object.keys(gameModules).length)
const onlineCount = ref(1280)

// ===== 生命周期 =====
onMounted(() => {
  initSignalR()
  togglePaper()
})

onUnmounted(() => {
  if (connection.value) {
    connection.value.stop()
  }
})
</script>


<style scoped>
/* ===== 全局 CSS 变量（墨划风格） ===== */
.game-hub {
  --paper-bg: #F7F4EE;
  --paper-card: #FCFAF7;
  --paper-sub: #F0EBE1;
  --ink-black: #2A2826;
  --ink-gray: #7A7570;
  --line-raw: #D5CEC5;
  --cinnabar: #9E2A2B;
  --font-family: 'Noto Serif SC', 'Source Han Serif SC', 'Songti SC', 'SimSun', serif;

  background-color: var(--paper-bg);
  color: var(--ink-black);
  font-family: var(--font-family);
  min-height: 100vh;
  display: flex;
  flex-direction: column;
  transition: background-color 0.5s ease, color 0.5s ease;
}

/* ===== 顶部品牌栏 ===== */
.hub-header {
  height: 64px;
  border-bottom: 1px solid var(--line-raw);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 40px;
  background: var(--paper-bg);
  flex-shrink: 0;
  transition: background 0.5s ease;
}

.brand {
  display: flex;
  align-items: center;
  gap: 12px;
  font-size: 20px;
  letter-spacing: 0.3em;
  font-weight: 400;
  color: var(--ink-black);
}
.brand-icon {
  font-size: 24px;
}
.brand-name {
  letter-spacing: 0.15em;
}

.header-controls {
  display: flex;
  align-items: center;
  gap: 20px;
}
.btn-line {
  background: none;
  border: 1px solid var(--line-raw);
  color: var(--ink-black);
  padding: 4px 16px;
  font-family: inherit;
  font-size: 13px;
  letter-spacing: 0.15em;
  cursor: pointer;
  transition: all 0.3s ease;
}
.btn-line:hover {
  border-color: var(--cinnabar);
  color: var(--cinnabar);
}
.connection-status {
  font-size: 13px;
  letter-spacing: 0.1em;
}
.connection-status.online {
  color: var(--cinnabar);
}
.connection-status.offline {
  color: var(--ink-gray);
}

/* ===== 主体布局 ===== */
.hub-body {
  display: flex;
  flex: 1;
  overflow: hidden;
}

/* ===== 左侧导航 ===== */
.hub-sidebar {
  width: 220px;
  background: var(--paper-sub);
  border-right: 1px solid var(--line-raw);
  padding: 20px 0;
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  transition: background 0.5s ease;
}

.game-nav {
  flex: 1;
  overflow-y: auto;
  padding: 0 12px;
}
.nav-group {
  margin-bottom: 20px;
}
.nav-group-title {
  font-size: 12px;
  letter-spacing: 0.2em;
  color: var(--ink-gray);
  padding: 8px 16px 4px 16px;
  border-bottom: 1px dashed var(--line-raw);
  margin-bottom: 6px;
  text-transform: uppercase;
}
.nav-item {
  display: flex;
  align-items: center;
  padding: 10px 16px;
  margin: 2px 0;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 14px;
  color: var(--ink-gray);
  letter-spacing: 0.1em;
  gap: 10px;
  position: relative;
}
.nav-item:hover {
  background: var(--paper-card);
  color: var(--ink-black);
}
.nav-item.active {
  background: var(--paper-card);
  color: var(--ink-black);
  box-shadow: inset 3px 0 0 var(--cinnabar);
  font-weight: 500;
}
.nav-icon {
  font-size: 18px;
  width: 24px;
  text-align: center;
}
.nav-label {
  flex: 1;
}
.badge-new {
  background: var(--cinnabar);
  color: #fff;
  font-size: 10px;
  padding: 0 8px;
  border-radius: 10px;
  line-height: 18px;
  letter-spacing: 0.05em;
}

/* 侧边统计 */
.sidebar-stats {
  border-top: 1px solid var(--line-raw);
  padding: 16px 20px;
  display: flex;
  justify-content: space-around;
  background: var(--paper-sub);
}
.stat-item {
  text-align: center;
}
.stat-value {
  display: block;
  font-size: 20px;
  font-weight: 400;
  color: var(--ink-black);
  letter-spacing: 0.1em;
}
.stat-label {
  font-size: 11px;
  color: var(--ink-gray);
  letter-spacing: 0.15em;
  margin-top: 4px;
}

/* ===== 主内容区 ===== */
.hub-main {
  flex: 1;
  padding: 30px 40px;
  background: var(--paper-bg);
  display: flex;
  justify-content: center;
  align-items: center;
  position: relative;
  transition: background 0.5s ease;
  overflow-y: auto;
}

/* 欢迎占位 */
.welcome-placeholder {
  text-align: center;
  color: var(--ink-gray);
}
.welcome-icon {
  font-size: 64px;
  margin-bottom: 20px;
}
.welcome-placeholder h2 {
  font-size: 28px;
  font-weight: 400;
  letter-spacing: 0.3em;
  color: var(--ink-black);
  margin: 0 0 8px;
}
.welcome-placeholder p {
  font-size: 16px;
  letter-spacing: 0.15em;
  margin-bottom: 16px;
}
.welcome-hint {
  font-size: 13px;
  opacity: 0.6;
  letter-spacing: 0.2em;
}

/* 加载状态 */
.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px;
  color: var(--ink-gray);
  font-size: 16px;
  letter-spacing: 0.15em;
}
.loading-spinner {
  width: 40px;
  height: 40px;
  border: 2px solid var(--line-raw);
  border-top-color: var(--cinnabar);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}
@keyframes spin {
  to { transform: rotate(360deg); }
}

/* ===== 页脚 ===== */
.hub-footer {
  border-top: 1px solid var(--line-raw);
  padding: 16px 40px;
  text-align: center;
  font-size: 12px;
  color: var(--ink-gray);
  letter-spacing: 0.2em;
  background: var(--paper-bg);
  flex-shrink: 0;
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.footer-sub {
  opacity: 0.6;
}

/* ===== 响应式 ===== */
@media (max-width: 768px) {
  .hub-header {
    padding: 0 16px;
    flex-wrap: wrap;
    height: auto;
    padding: 12px 16px;
    gap: 8px;
  }
  .brand {
    font-size: 16px;
  }
  .hub-body {
    flex-direction: column;
  }
  .hub-sidebar {
    width: 100%;
    border-right: none;
    border-bottom: 1px solid var(--line-raw);
    padding: 12px 0;
    max-height: 200px;
    overflow-y: auto;
  }
  .game-nav {
    display: flex;
    flex-wrap: wrap;
    gap: 4px 8px;
    padding: 0 12px;
  }
  .nav-group {
    width: 100%;
    margin-bottom: 8px;
  }
  .nav-group-title {
    padding: 4px 8px;
    font-size: 11px;
  }
  .nav-item {
    padding: 6px 12px;
    font-size: 13px;
    flex: 0 0 auto;
  }
  .sidebar-stats {
    display: none;
  }
  .hub-main {
    padding: 20px 16px;
  }
  .hub-footer {
    padding: 12px 16px;
    flex-direction: column;
    gap: 4px;
  }
}
</style>