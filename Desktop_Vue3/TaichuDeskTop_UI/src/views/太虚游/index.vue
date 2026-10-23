<template>
  <div class="game-hub content-page">
    <header class="content-header"><div><h1>太虚游</h1><p class="intro">玩游戏，也分享游戏里的故事。</p></div><RouterLink v-if="currentGameKey !== 'MinecraftServer'" class="game-discussion" to="/stream?tag=我的世界">我的世界讨论 →</RouterLink><span v-if="currentGameKey === 'Gobang'" class="connection-status" :class="isConnected ? 'online' : 'offline'">{{ isConnected ? '● 联机服务已连接' : '○ 联机服务未连接' }}</span></header>
    <div class="hub-body">
      <!-- 左侧导航 – 分类分组 -->
      <aside v-if="currentGameKey" class="hub-sidebar">
        <nav class="game-nav" aria-label="游戏分类">
          <button class="nav-item" :class="{ active: !currentGameKey }" @click="loadGame(null)">全部游戏</button>
          <div v-for="(group, groupKey) in groupedModules" :key="groupKey" class="nav-group">
            <div class="nav-group-title">{{ group.label }}</div>
            <button type="button"
              v-for="item in group.items"
              :key="item.key"
              class="nav-item"
              :class="{ active: currentGameKey === item.key }"
              @click="loadGame(item.key)"
            >
              <span class="nav-icon">{{ item.icon }}</span>
              <span class="nav-label">{{ item.label }}</span>
              <span v-if="item.isNew" class="badge-new">新</span>
            </button>
          </div>
        </nav>

        <!-- 侧边统计信息 -->
        <div class="sidebar-stats">
          <div class="stat-item">
            <span class="stat-value">{{ gameCount }}</span>
            <span class="stat-label">游戏总数</span>
          </div>

        </div>
      </aside>

      <!-- 主内容区 -->
      <main class="hub-main">
        <div v-if="!currentGameComponent" class="welcome-placeholder">
          <h2>选择你的下一场冒险</h2><p>按兴趣挑选游戏；联机功能需要连接服务器。</p>
          <div class="game-cards"><button v-for="(game, key) in gameModules" :key="key" class="game-card" @click="loadGame(key)"><span class="game-card-icon" aria-hidden="true">{{ game.icon }}</span><strong>{{ game.label }}</strong><small>{{ game.description }}</small><span class="game-status">{{ game.status }}</span></button></div>
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
      <span>太虚游 · 社区游乐场</span>
      <span class="footer-sub">v2.0 · 自在随心</span>
    </footer>
  </div>
</template>

<script setup>
import { useUserStore } from '@/stores/user'
import { ref, shallowRef, defineAsyncComponent, onMounted, onUnmounted, provide, computed, watch } from 'vue'
import { useRoute, useRouter, onBeforeRouteUpdate, onBeforeRouteLeave } from 'vue-router'
import * as signalR from '@microsoft/signalr'

// ===== 游戏模块注册 =====
const gameModules = {
  // ----- 联机服务器专区 ✅ 新增 -----
  MinecraftServer: {
    label: '我的世界',
    category: 'server',
    icon: '⛏️',
    description: '社区服务器、客户端与模组下载',
    status: '已上线',
    isNew: true,
    component: defineAsyncComponent(() => import('./games/Minecraft/index.vue'))
  },

  // ----- 棋类 -----
  Gobang: {
    label: '五子棋',
    category: 'chess',
    icon: '⬛',
    description: '经典五子棋对弈，支持多人联机',
    status: '已上线',
    component: defineAsyncComponent(() => import('./games/Gobang/index.vue'))
  },
  Go: {
    label: '围棋',
    category: 'chess',
    icon: '⚫',
    description: '9 路围棋，提子、禁自杀，支持对局记录',
    status: '已上线',
    isNew: true,
    component: defineAsyncComponent(() => import('./games/Go/index.vue'))
  },
  Xiangqi: {
    label: '象棋',
    category: 'chess',
    icon: '♟️',
    description: '中国象棋，完整走法，支持对局记录',
    status: '已上线',
    isNew: true,
    component: defineAsyncComponent(() => import('./games/Xiangqi/index.vue'))
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
  server: { label: 'MC服务器', order: 1 }, // 调整到最前或合适的位置
  chess: { label: '棋类对弈', order: 2 },
  classic: { label: '经典小游戏', order: 3 },
  test: { label: '测试问卷', order: 4 },
  custom: { label: '自定义工坊', order: 5 }
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
  const hubUrl = new URL('/signalr/game', new URL(import.meta.env.VITE_API_BASE_URL || '/api', window.location.origin)).href
  connection.value = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: () => useUserStore().token || ''
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
const route = useRoute(), router = useRouter()
const canLeaveGame = async () => {
  const instance = currentGameRef.value
  if (instance && typeof instance.handleBeforeLeave === 'function') {
    return await instance.handleBeforeLeave() !== false
  }
  return true
}
const loadGame = key => router.push({ query: { ...route.query, game: key || undefined } })
onBeforeRouteUpdate((to, from) => to.query.game === from.query.game || canLeaveGame())
onBeforeRouteLeave(canLeaveGame)
watch(() => route.query.game, key => {
  const mod = typeof key === 'string' && Object.hasOwn(gameModules, key) ? gameModules[key] : null
  currentGameKey.value = mod ? key : null
  currentGameComponent.value = mod?.component || null
}, { immediate: true })

// ===== 统计信息 =====
const gameCount = computed(() => Object.keys(gameModules).length)


// ===== 生命周期 =====
onMounted(() => {
  initSignalR()
})

onUnmounted(() => {
  if (connection.value) {
    connection.value.stop()
  }
})
</script>


<style scoped>
.game-hub { font-family: var(--font-sans); }
.game-discussion { color: var(--accent-color); text-decoration: none; padding: 10px 14px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); }
.connection-status { font-size: 12px; color: var(--text-secondary); }
.connection-status.online { color: var(--success); }
.hub-body { display: flex; gap: 24px; align-items: flex-start; }
.hub-sidebar { width: 190px; flex-shrink: 0; padding: 16px 10px; background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 12px; }
.nav-group { margin-bottom: 18px; }
.nav-group-title { font-size: 11px; color: var(--text-secondary); padding: 6px 10px; margin-bottom: 6px; }
.nav-item { width: 100%; display: flex; align-items: center; gap: 8px; text-align: left; border: 0; padding: 10px; border-radius: 8px; font-size: 13px; background: transparent; color: var(--text-primary); }
.nav-item:hover { background: var(--bg-sub); }
.nav-item.active { background: var(--accent-soft); color: var(--accent-color); }
.nav-icon { width: 20px; text-align: center; }
.badge-new { margin-left: auto; background: var(--accent-soft); color: var(--accent-color); padding: 1px 5px; border-radius: 5px; font-size: 10px; }
.sidebar-stats { border-top: 1px solid var(--border-line); padding: 14px 10px 0; }
.stat-value { font-size: 22px; margin-right: 8px; }
.stat-label { font-size: 11px; color: var(--text-secondary); }
.hub-main { flex: 1; min-width: 0; }
.welcome-placeholder h2 { font-size: 22px; margin: 0 0 10px; }
.welcome-placeholder p { font-size: 14px; color: var(--text-secondary); }
.game-cards { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 16px; margin-top: 24px; }
.game-card { display: flex; flex-direction: column; align-items: flex-start; text-align: left; gap: 10px; padding: 24px; border: 1px solid var(--border-line); border-radius: 12px; color: var(--text-primary); background: var(--paper-card); }
.game-card:hover { border-color: var(--accent-color); }
.game-card-icon { font-size: 28px; }
.game-card small { color: var(--text-secondary); line-height: 1.7; flex: 1; }
.game-status { color: var(--accent-color); background: var(--accent-soft); border-radius: 6px; padding: 3px 8px; font-size: 12px; }
.loading-state { display: grid; place-content: center; gap: 16px; padding: 64px; color: var(--text-secondary); }
.loading-spinner { width: 30px; height: 30px; border: 2px solid var(--border-line); border-top-color: var(--accent-color); border-radius: 50%; animation: spin .8s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }
.hub-footer { display: flex; justify-content: space-between; flex-wrap: wrap; gap: 8px; margin-top: 32px; padding: 20px 0; border-top: 1px solid var(--border-line); color: var(--text-secondary); font-size: 11px; }
@media(max-width: 1100px) {
 .hub-body { flex-direction: column; }
 .hub-sidebar, .hub-main { width: 100%; }
 .hub-sidebar { padding: 10px; }
 .game-nav { display: flex; flex-wrap: wrap; gap: 6px; }
 .nav-group { display: contents; }
 .nav-group-title, .sidebar-stats, .badge-new { display: none; }
 .nav-item { width: auto; padding: 8px 10px; background: var(--bg-sub); }
}
@media(max-width: 560px) { .game-cards { grid-template-columns: minmax(0,1fr); } }
</style>
