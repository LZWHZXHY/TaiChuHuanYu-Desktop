<template>
    <div class="game-hub">
        <aside>
            <div 
                v-for="(item, key) in gameModules" 
                :key="key"
                @click="loadGame(key)"
                :class="{ active: currentGameKey === key }"
            >
                {{ item.label }}
            </div>
        </aside>

        <main>
            <div v-if="!currentGameComponent" class="welcome-placeholder">
                🎮 欢迎来到太虚游<br />
                <span>请从左侧选择一款游戏</span>
            </div>

            <Suspense v-else>
                <component 
                    ref="currentGameRef"
                    :is="currentGameComponent" 
                />
                <template #fallback>
                    <div class="loading-state">⏳ 游戏加载中...</div>
                </template>
            </Suspense>
        </main>
    </div>
</template>

<script setup>
import { ref, shallowRef, defineAsyncComponent, onMounted, onUnmounted, provide } from 'vue'
import * as signalR from '@microsoft/signalr'

// ===== 游戏模块注册 =====
const gameModules = {
  Gobang: {
    label: '五子棋',
    component: defineAsyncComponent(() => import('./games/Gobang/index.vue'))
  },
  Tetris: {
    label: '俄罗斯方块',
    component: defineAsyncComponent(() => import('./games/Tetris/index.vue'))
  }
}

// ===== 页面状态 =====
const currentGameKey = ref(null)
const currentGameComponent = shallowRef(null)
const currentGameRef = shallowRef(null)

// ===== SignalR 连接 =====
const connection = ref(null)
const isConnected = ref(false)
const currentRoomId = ref(null)

// ===== 提供给子组件的 SignalR 方法 =====
const createRoom = async (roomName, mode, boardSize, timeLimit = 30) => {
  if (!connection.value || !isConnected.value) {
    alert('未连接到服务器')
    return
  }
  try {
    // 确保这里的调用顺序和参数个数与 C# 后端完全一致
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

// ===== 初始化 SignalR =====
const initSignalR = async () => {
  // 使用你的后端地址
  const hubUrl = 'https://bianyuzhou.com/signalr/game';
  
  connection.value = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: () => {
        // 从 localStorage 获取 JWT Token
        return localStorage.getItem('token') || ''
      }
    })
    .withAutomaticReconnect()
    .build()

  // ===== 监听后端事件 =====
  
  connection.value.on('RoomCreated', (data) => {
    console.log('✅ 房间创建成功：', data)
    currentRoomId.value = data.roomId
    // 通过自定义事件通知子组件
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

// ===== 提供给子组件的信号和方法 =====
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

  const module = gameModules[key]
  if (module) {
    currentGameKey.value = key
    currentGameComponent.value = module.component
  }
}

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
.game-hub {
  display: flex;
  height: 100%;
  min-height: 600px;
  background: #fff;
  border-radius: 12px;
  overflow: hidden;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.06);
}

aside {
  width: 180px;
  background: #f8f9fa;
  padding: 20px 0;
  border-right: 1px solid #e9ecef;
  flex-shrink: 0;
}

aside div {
  padding: 14px 24px;
  margin: 4px 12px;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 15px;
  color: #495057;
  font-weight: 500;
}

aside div:hover {
  background: #e9ecef;
  color: #212529;
}

aside div.active {
  background: #e7f1ff;
  color: #0d6efd;
  font-weight: 600;
  box-shadow: inset 3px 0 0 #0d6efd;
}

main {
  flex: 1;
  padding: 30px;
  display: flex;
  justify-content: center;
  align-items: center;
  background: #ffffff;
  position: relative;
}

.welcome-placeholder {
  text-align: center;
  font-size: 28px;
  color: #adb5bd;
  line-height: 1.6;
}

.welcome-placeholder span {
  font-size: 16px;
  color: #ced4da;
  display: block;
  margin-top: 8px;
}

.loading-state {
  font-size: 18px;
  color: #6c757d;
  padding: 40px;
  background: #f8f9fa;
  border-radius: 12px;
}
</style>