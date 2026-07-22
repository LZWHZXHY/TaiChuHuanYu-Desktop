<template>
  <div class="game-container">
    <div class="game-header">
      <div class="player-info">
        <span class="badge black-badge">● 黑棋</span>
        <span class="username">{{ myColor === 'black' ? '（我）' : '' }}</span>
        <span v-if="currentPlayer === 'black' && !isGameOver" class="turn-indicator">👈 当前回合</span>
      </div>
      <div class="timer" :class="{ warning: remainingTime <= 5 }">
        ⏱️ {{ remainingTime }}s
      </div>
      <div class="vs-text">VS</div>
      <div class="player-info">
        <span class="badge white-badge">○ 白棋</span>
        <span class="username">{{ myColor === 'white' ? '（我）' : '' }}</span>
        <span v-if="currentPlayer === 'white' && !isGameOver" class="turn-indicator">👈 当前回合</span>
      </div>
    </div>

    <canvas
      ref="boardCanvas"
      :width="canvasSize"
      :height="canvasSize"
      @click="onCanvasClick"
    ></canvas>

    <div class="button-group">
      <button @click="$emit('endGame')" class="exit-btn">返回大厅</button>
    </div>
  </div>
</template>

<script setup>
import { ref, nextTick, watch, onMounted, onUnmounted, inject, computed } from 'vue'

const props = defineProps({
  boardSize: { type: Number, default: 15 },
  firstPlayer: { type: String, default: 'random' },
  mode: { type: String, default: 'classic' },
  roomName: { type: String, default: '' },
  roomId: { type: String, default: null },
  isOwner: { type: Boolean, default: false },
  timeLimit: { type: Number, default: 30 }
})

const emit = defineEmits(['endGame'])

const signalR = inject('signalR', null)
const connection = signalR?.connection
const makeMove = signalR?.makeMove

const myColor = computed(() => props.isOwner ? 'black' : 'white')

const boardCanvas = ref(null)
const canvasSize = 600

const boardData = ref([])
const currentPlayer = ref('black')
const isGameOver = ref(false)
const gameWinner = ref(null)
const remainingTime = ref(30)
let countdownInterval = null

// 存储 playerId -> color 映射
const playerMap = ref({})

// 初始化棋盘
const initBoard = () => {
  const size = props.boardSize
  boardData.value = Array.from({ length: size }, () => Array(size).fill(null))
  currentPlayer.value = 'black'
  isGameOver.value = false
  gameWinner.value = null
  remainingTime.value = props.timeLimit || 30
  playerMap.value = {}
  console.log('♟️ 棋盘初始化，我的颜色：', myColor.value)
}

// 绘制网格（必须调用以保证线条出现）
const drawBoard = () => {
  const canvas = boardCanvas.value
  if (!canvas) return
  const ctx = canvas.getContext('2d')
  const size = canvasSize
  const margin = 30
  const boardSize = props.boardSize
  const gridSize = (size - 2 * margin) / (boardSize - 1)

  ctx.fillStyle = '#DEB887'
  ctx.fillRect(0, 0, size, size)

  ctx.strokeStyle = '#000000'
  ctx.lineWidth = 1.5
  for (let i = 0; i < boardSize; i++) {
    const pos = margin + i * gridSize
    ctx.beginPath()
    ctx.moveTo(pos, margin)
    ctx.lineTo(pos, size - margin)
    ctx.stroke()
    ctx.beginPath()
    ctx.moveTo(margin, pos)
    ctx.lineTo(size - margin, pos)
    ctx.stroke()
  }

  // 星标
  if (boardSize >= 15) {
    const stars = []
    if (boardSize === 15) stars.push([3, 3], [11, 3], [7, 7], [3, 11], [11, 11])
    else if (boardSize === 19) stars.push([3, 3], [15, 3], [9, 9], [3, 15], [15, 15])
    ctx.fillStyle = '#000000'
    stars.forEach(([row, col]) => {
      const x = margin + col * gridSize
      const y = margin + row * gridSize
      ctx.beginPath()
      ctx.arc(x, y, 4, 0, 2 * Math.PI)
      ctx.fill()
    })
  }
}

// 画棋子
const drawStone = (row, col, color) => {
  const canvas = boardCanvas.value
  const ctx = canvas.getContext('2d')
  const size = canvasSize
  const margin = 30
  const boardSize = props.boardSize
  const gridSize = (size - 2 * margin) / (boardSize - 1)

  const x = margin + col * gridSize
  const y = margin + row * gridSize
  const radius = gridSize * 0.42

  const gradient = ctx.createRadialGradient(
    x - radius * 0.3, y - radius * 0.3, radius * 0.1,
    x, y, radius
  )
  if (color === 'black') {
    gradient.addColorStop(0, '#666')
    gradient.addColorStop(1, '#000')
  } else {
    gradient.addColorStop(0, '#fff')
    gradient.addColorStop(1, '#ddd')
  }

  ctx.beginPath()
  ctx.arc(x, y, radius, 0, 2 * Math.PI)
  ctx.fillStyle = gradient
  ctx.fill()
  if (color === 'white') {
    ctx.strokeStyle = '#aaa'
    ctx.lineWidth = 0.5
    ctx.stroke()
  }
}

// 重绘（先画网格，再画所有棋子）
const redrawBoard = () => {
  drawBoard()
  const data = boardData.value
  for (let row = 0; row < data.length; row++) {
    for (let col = 0; col < data[row].length; col++) {
      const stone = data[row][col]
      if (stone) drawStone(row, col, stone)
    }
  }
}

// 胜利检测
const checkWin = (row, col, color) => {
  const directions = [[1,0],[0,1],[1,1],[1,-1]]
  for (const [dx, dy] of directions) {
    let count = 1
    for (let i = 1; i < 5; i++) {
      const nr = row + dx * i, nc = col + dy * i
      if (nr < 0 || nr >= props.boardSize || nc < 0 || nc >= props.boardSize) break
      if (boardData.value[nr][nc] === color) count++
      else break
    }
    for (let i = 1; i < 5; i++) {
      const nr = row - dx * i, nc = col - dy * i
      if (nr < 0 || nr >= props.boardSize || nc < 0 || nc >= props.boardSize) break
      if (boardData.value[nr][nc] === color) count++
      else break
    }
    if (count >= 5) return true
  }
  return false
}

// 落子（仅更新棋盘，不改变回合）
const placeStone = (row, col, color) => {
  if (isGameOver.value || boardData.value[row][col]) return
  boardData.value[row][col] = color
  drawStone(row, col, color)

  if (checkWin(row, col, color)) {
    isGameOver.value = true
    gameWinner.value = color
    clearInterval(countdownInterval)
    alert(`🎉 ${color === 'black' ? '黑棋' : '白棋'} 获胜！`)
    return
  }
  const total = boardData.value.flat().filter(s => s).length
  if (total === props.boardSize * props.boardSize) {
    isGameOver.value = true
    clearInterval(countdownInterval)
    alert('🤝 平局！')
  }
}

// 获取点击坐标
const getBoardPosition = (event) => {
  const canvas = boardCanvas.value
  const rect = canvas.getBoundingClientRect()
  const scaleX = canvas.width / rect.width
  const scaleY = canvas.height / rect.height
  const x = (event.clientX - rect.left) * scaleX
  const y = (event.clientY - rect.top) * scaleY
  const margin = 30, size = canvasSize, boardSize = props.boardSize
  const gridSize = (size - 2 * margin) / (boardSize - 1)
  let col = Math.round((x - margin) / gridSize)
  let row = Math.round((y - margin) / gridSize)
  if (row < 0 || row >= boardSize || col < 0 || col >= boardSize) return null
  const dx = Math.abs(x - (margin + col * gridSize))
  const dy = Math.abs(y - (margin + row * gridSize))
  if (dx > gridSize * 0.4 || dy > gridSize * 0.4) return null
  return { row, col }
}

// ===== 点击事件 =====
const onCanvasClick = (event) => {
  if (isGameOver.value) {
    alert('游戏已结束')
    return
  }
  if (!makeMove || !props.roomId) {
    alert('未连接')
    return
  }
  
  // 1. 严格判断是否自己的回合
  if (currentPlayer.value !== myColor.value) {
    alert(`还没轮到你！当前是 ${currentPlayer.value === 'black' ? '黑棋' : '白棋'} 的回合`)
    return
  }

  const pos = getBoardPosition(event)
  if (!pos) return
  const { row, col } = pos
  
  // 2. 判断是否有棋子
  if (boardData.value[row][col]) {
    alert('已有棋子')
    return
  }

  // 3. 发送落子，本地立即绘制
  makeMove(props.roomId, row, col)
  placeStone(row, col, myColor.value)
  
  // 【核心修复】：本地立刻交出回合权，防止网络延迟导致的连续点击
  currentPlayer.value = myColor.value === 'black' ? 'white' : 'black'
  
  // 重置倒计时
  resetTimer()
}

// ===== 计时器 =====
const startCountdown = () => {
  if (countdownInterval) clearInterval(countdownInterval)
  remainingTime.value = props.timeLimit || 30
  countdownInterval = setInterval(() => {
    if (remainingTime.value > 0) {
      remainingTime.value--
    } else {
      clearInterval(countdownInterval)
      countdownInterval = null
      if (!isGameOver.value) {
        isGameOver.value = true
        alert(`⏰ ${currentPlayer.value === 'black' ? '黑棋' : '白棋'} 超时，对方获胜！`)
      }
    }
  }, 1000)
}
const resetTimer = () => {
  if (countdownInterval) clearInterval(countdownInterval)
  remainingTime.value = props.timeLimit || 30
  startCountdown()
}

// ===== 事件监听 =====
const onGameStarted = (event) => {
  const data = event.detail
  console.log('🎮 游戏开始：', data)

  // 建立 playerId -> color 映射（适配多种字段名）
  const players = data.players || []
  const map = {}
  players.forEach(p => {
    const id = p.id || p.connectionId || p.playerId
    if (id && p.color) {
      map[id] = p.color
    }
  })
  playerMap.value = map
  console.log('🗺️ 玩家映射：', playerMap.value)

  // 设置初始回合
  let turnColor = null
  if (data.currentTurn) {
    turnColor = playerMap.value[data.currentTurn]
  }
  if (!turnColor) {
    if (props.firstPlayer === 'black') turnColor = 'black'
    else if (props.firstPlayer === 'white') turnColor = 'white'
    else turnColor = Math.random() < 0.5 ? 'black' : 'white'
  }
  currentPlayer.value = turnColor
  console.log('🎯 初始回合：', currentPlayer.value)

  remainingTime.value = props.timeLimit || 30
  startCountdown()
}



const onMoveMade = (event) => {
  const data = event.detail
  console.log('📨 收到落子广播：', data)

  const { row, col } = data

  // 【核心修复】：彻底抛弃对 playerMap 的依赖，避免因生命周期导致的空数据问题。
  
  // 1. 如果该位置已经有棋子，说明是我们自己刚才点击落子的（本地已经提前绘制），直接忽略这个服务器的回音。
  if (boardData.value[row][col]) {
    return
  }

  // 2. 如果该位置为空，说明必然是对手落子。绘制对手的棋子。
  const opponentColor = myColor.value === 'black' ? 'white' : 'black'
  placeStone(row, col, opponentColor)

  // 3. 切换回合：对手落子完毕，将控制权交还给我方
  currentPlayer.value = myColor.value
  console.log('✅ 回合切换到：', currentPlayer.value)

  resetTimer()
}

const onGameOver = (event) => {
  const data = event.detail
  isGameOver.value = true
  gameWinner.value = data.winner
  clearInterval(countdownInterval)
  alert(`🎉 ${data.winnerColor === 'black' ? '黑棋' : '白棋'} 获胜！`)
}

const initGame = () => {
  initBoard()
  setTimeout(redrawBoard, 50)
}

// 生命周期
onMounted(() => {
  console.log('🚀 GameBoard mounted')
  window.addEventListener('game-started', onGameStarted)
  window.addEventListener('move-made', onMoveMade)
  window.addEventListener('game-over', onGameOver)
})

onUnmounted(() => {
  window.removeEventListener('game-started', onGameStarted)
  window.removeEventListener('move-made', onMoveMade)
  window.removeEventListener('game-over', onGameOver)
  if (countdownInterval) clearInterval(countdownInterval)
})

watch(() => props.boardSize, () => nextTick(initGame), { immediate: true })
watch(boardCanvas, () => { if (boardCanvas.value) nextTick(initGame) })
</script>

<style scoped>
/* 样式保持不变 */
.game-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  width: 100%;
}
.game-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
  max-width: 620px;
  padding: 8px 0 16px 0;
}
.player-info {
  display: flex;
  align-items: center;
  gap: 10px;
  flex: 1;
}
.player-info:first-child {
  justify-content: flex-start;
}
.player-info:last-child {
  justify-content: flex-end;
}
.badge {
  padding: 4px 12px;
  border-radius: 20px;
  font-size: 13px;
  font-weight: 600;
  color: #fff;
}
.black-badge { background: #212529; }
.white-badge { background: #ced4da; color: #212529; }
.username { font-weight: 500; color: #343a40; }
.turn-indicator {
  color: #0d6efd;
  font-weight: bold;
  font-size: 14px;
  margin-left: 4px;
}
.vs-text {
  font-weight: 900;
  color: #dee2e6;
  font-size: 20px;
  padding: 0 10px;
}
.timer {
  font-size: 20px;
  font-weight: bold;
  color: #333;
  padding: 0 10px;
  min-width: 70px;
  text-align: center;
}
.timer.warning {
  color: #dc3545;
  animation: blink 0.5s infinite;
}
@keyframes blink {
  0% { opacity: 1; }
  50% { opacity: 0.3; }
  100% { opacity: 1; }
}
.game-container canvas {
  border-radius: 12px;
  box-shadow: 0 8px 30px rgba(0, 0, 0, 0.12);
  max-width: 100%;
  background: #DEB887;
  cursor: pointer;
}
.button-group {
  margin-top: 16px;
}
.exit-btn {
  padding: 8px 24px;
  background: #6c757d;
  color: white;
  border: none;
  border-radius: 8px;
  cursor: pointer;
  font-size: 14px;
}
.exit-btn:hover {
  background: #5a6268;
}
</style>