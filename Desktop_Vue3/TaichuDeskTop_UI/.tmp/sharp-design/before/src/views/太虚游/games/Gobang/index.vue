<template>
  <div class="gobang-box">

    <!-- 大厅 -->
    <div v-if="pageStatus === 'lobby'">
      <h2>🏠 五子棋大厅</h2>
      <div class="button-group">
        <button @click="pageStatus = 'createForm'">🏠 自定义房间</button>
        <button @click="quickMatch">🎯 快速匹配</button>
      </div>

      <!-- 加入房间区域 -->
      <div class="join-room-area">
        <p>或输入房间ID加入：</p>
        <div class="join-input-group">
          <input v-model="joinRoomId" placeholder="请输入房间ID（如 a1b2c3d4）" />
          <button @click="handleJoinRoom" style="background: #17a2b8;">🔗 加入房间</button>
        </div>
      </div>
    </div>

    <!-- 创建房间 -->
    <CreateRoom
      v-else-if="pageStatus === 'createForm'"
      @create="handleCreateRoom"
      @back="pageStatus = 'lobby'"
    />

    <!-- 等待房间 -->
    <WaitingRoom
      v-else-if="pageStatus === 'waitingRoom'"
      :room-name="roomName"
      :room-mode="roomMode"
      :room-id="currentRoomId"
      :is-owner="isRoomOwner"
      @cancel="cancelWaiting"
      @startGame="handleStartGame"
    />

    <!-- 棋盘对局 -->
    <GameBoard
      v-else-if="pageStatus === 'playing'"
      :board-size="roomBoardSize"
      :first-player="roomFirstPlayer"
      :mode="roomMode"
      :room-name="roomName"
      :room-id="currentRoomId"
      :is-owner="isRoomOwner"
      :time-limit="roomTimeLimit"
      @endGame="endGame"
    />

  </div>
</template>

<script setup>
import { ref, inject, onMounted, onUnmounted } from 'vue'
import CreateRoom from './components/CreateRoom.vue'
import WaitingRoom from './components/WaitingRoom.vue'
import GameBoard from './components/GameBoard.vue'

// ===== 注入 SignalR =====
const signalR = inject('signalR', null)
const { createRoom, joinRoom, startGame, makeMove, isConnected } = signalR || {}

// ===== 页面状态 =====
const pageStatus = ref('lobby')

// ===== 房间数据 =====
const roomName = ref('')
const roomMode = ref('classic')
const roomFirstPlayer = ref('random')
const roomBoardSize = ref(15)
const currentRoomId = ref(null)
const isRoomOwner = ref(false)
const roomTimeLimit = ref(30)

// ===== 加入房间的输入 =====
const joinRoomId = ref('')

// ===== 事件监听器引用 =====
let eventListeners = []

// ===== 快速匹配 =====
const quickMatch = () => {
  if (!isConnected?.value) {
    alert('未连接到游戏服务器')
    return
  }
  const name = '快速匹配_' + Date.now()
  roomName.value = name
  roomMode.value = 'classic'
  roomFirstPlayer.value = 'random'
  roomBoardSize.value = 15
  isRoomOwner.value = true
  roomTimeLimit.value = 30
  pageStatus.value = 'waitingRoom'
  createRoom(name, 'classic', 15, 30)
}

// ===== 创建房间 =====
const handleCreateRoom = (data) => {
  if (!isConnected?.value) {
    alert('未连接到游戏服务器')
    return
  }
  roomName.value = data.roomName
  roomMode.value = data.mode
  roomFirstPlayer.value = 'random'
  roomBoardSize.value = 15
  isRoomOwner.value = true
  roomTimeLimit.value = data.timeLimit || 30
  pageStatus.value = 'waitingRoom'
  createRoom(data.roomName, data.mode, 15, roomTimeLimit.value)
}

// ===== 加入房间 =====
const handleJoinRoom = () => {
  if (!isConnected?.value) {
    alert('未连接到游戏服务器')
    return
  }
  const roomId = joinRoomId.value.trim()
  if (!roomId) {
    alert('请输入房间ID')
    return
  }
  currentRoomId.value = roomId
  joinRoom(roomId)
  roomName.value = ''
  roomMode.value = 'classic'
  isRoomOwner.value = false
  roomTimeLimit.value = 30
  pageStatus.value = 'waitingRoom'
  joinRoomId.value = ''
}

// ===== 取消等待 =====
const cancelWaiting = () => {
  roomName.value = ''
  roomMode.value = 'classic'
  isRoomOwner.value = false
  pageStatus.value = 'lobby'
}

// ===== 开始游戏 =====
const handleStartGame = (config) => {
  roomFirstPlayer.value = config.firstPlayer
  roomBoardSize.value = config.boardSize
  roomTimeLimit.value = config.timeLimit || 30
  if (currentRoomId.value) {
    startGame(currentRoomId.value)
  } else {
    alert('房间 ID 丢失，无法开始游戏')
  }
}

// ===== 返回大厅 =====
const endGame = () => {
  roomName.value = ''
  roomMode.value = 'classic'
  roomFirstPlayer.value = 'random'
  roomBoardSize.value = 15
  currentRoomId.value = null
  isRoomOwner.value = false
  roomTimeLimit.value = 30
  pageStatus.value = 'lobby'
  console.log('🏠 已返回大厅，数据已清空')
}

// ===== 监听后端事件 =====
const setupEventListeners = () => {
  const onRoomCreated = (event) => {
    const data = event.detail
    currentRoomId.value = data.roomId
    console.log('✅ 房间创建成功，RoomId:', data.roomId)
  }
  window.addEventListener('room-created', onRoomCreated)
  eventListeners.push({ event: 'room-created', handler: onRoomCreated })

  const onGameStarted = (event) => {
    const data = event.detail
    console.log('🎮 游戏开始事件：', data)
    pageStatus.value = 'playing'
  }
  window.addEventListener('game-started', onGameStarted)
  eventListeners.push({ event: 'game-started', handler: onGameStarted })

  const onPlayerLeft = (event) => {
    alert('对手已离开房间')
    pageStatus.value = 'lobby'
  }
  window.addEventListener('player-left', onPlayerLeft)
  eventListeners.push({ event: 'player-left', handler: onPlayerLeft })
}

onMounted(() => {
  setupEventListeners()
})

onUnmounted(() => {
  eventListeners.forEach(({ event, handler }) => {
    window.removeEventListener(event, handler)
  })
  eventListeners = []
})
</script>

<style scoped>
.gobang-box {
  padding: 40px;
  text-align: center;
  border: 2px dashed #ccc;
  min-height: 300px;
  background: #fafafa;
  border-radius: 16px;
  max-width: 700px;
  margin: 0 auto;
}
.button-group {
  display: flex;
  gap: 16px;
  justify-content: center;
  margin-top: 20px;
  flex-wrap: wrap;
}
.join-room-area {
  margin-top: 24px;
  padding-top: 20px;
  border-top: 1px solid #e0e0e0;
}
.join-input-group {
  display: flex;
  gap: 12px;
  justify-content: center;
  align-items: center;
  margin-top: 8px;
}
.join-input-group input {
  padding: 10px 16px;
  font-size: 16px;
  border: 1px solid #ccc;
  border-radius: 8px;
  width: 200px;
}
.join-input-group button {
  padding: 10px 24px;
  font-size: 16px;
  cursor: pointer;
  border: none;
  color: white;
  border-radius: 8px;
  transition: 0.2s;
}
button {
  padding: 10px 24px;
  font-size: 16px;
  cursor: pointer;
  border: none;
  background: #0d6efd;
  color: white;
  border-radius: 8px;
  transition: 0.2s;
}
button:hover {
  background: #0b5ed7;
}
button:last-child {
  background: #28a745;
}
button:last-child:hover {
  background: #218838;
}
</style>