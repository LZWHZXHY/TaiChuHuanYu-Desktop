<template>
  <div>
    <h2>🔄 等待对手加入...</h2>
    <p v-if="roomName">房间名称：<strong>{{ roomName }}</strong></p>
    <p>房间ID：<strong>{{ roomId }}</strong></p>
    <p>模式：<strong>{{ roomMode === 'classic' ? '⚔️ 传统模式' : '⚙️ 客制化模式' }}</strong></p>

    <!-- 对手状态 -->
    <div class="opponent-status">
      <p v-if="!opponentJoined">⏳ 等待对手加入...</p>
      <p v-else style="color: #28a745; font-weight: bold;">✅ 对手已加入！</p>
    </div>

    <!-- 配置选项区域 -->
    <div class="config-area" v-if="roomMode === 'classic'">
      <div class="config-item">
        <label>先手选择：</label>
        <select v-model="localFirstPlayer" :disabled="!isOwner">
          <option value="random">🎲 随机</option>
          <option value="black">⚫ 黑棋（我先）</option>
          <option value="white">⚪ 白棋（对方先）</option>
        </select>
        <span v-if="!isOwner" style="color: #888; font-size: 13px;">（仅房主可修改）</span>
      </div>
      <div class="config-item">
        <label>每步时间：</label>
        <select v-model="localTimeLimit" :disabled="!isOwner">
          <option value="30">30 秒</option>
          <option value="60">60 秒</option>
          <option value="90">90 秒</option>
          <option value="120">120 秒</option>
        </select>
        <span v-if="!isOwner" style="color: #888; font-size: 13px;">（仅房主可修改）</span>
      </div>
    </div>

    <div class="config-area" v-else>
      <div class="config-item">
        <label>棋盘大小：</label>
        <select v-model="localBoardSize" :disabled="!isOwner">
          <option value="13">13 × 13</option>
          <option value="15" selected>15 × 15</option>
          <option value="19">19 × 19</option>
        </select>
        <span v-if="!isOwner" style="color: #888; font-size: 13px;">（仅房主可修改）</span>
      </div>
      <div class="config-item">
        <label>先手选择：</label>
        <select v-model="localFirstPlayer" :disabled="!isOwner">
          <option value="random">🎲 随机</option>
          <option value="black">⚫ 黑棋（我先）</option>
          <option value="white">⚪ 白棋（对方先）</option>
        </select>
        <span v-if="!isOwner" style="color: #888; font-size: 13px;">（仅房主可修改）</span>
      </div>
      <div class="config-item">
        <label>每步时间：</label>
        <select v-model="localTimeLimit" :disabled="!isOwner">
          <option value="30">30 秒</option>
          <option value="60">60 秒</option>
          <option value="90">90 秒</option>
          <option value="120">120 秒</option>
        </select>
        <span v-if="!isOwner" style="color: #888; font-size: 13px;">（仅房主可修改）</span>
      </div>
    </div>

    <!-- 按钮区域 -->
    <div class="button-group">
      <button 
        v-if="isOwner && opponentJoined" 
        @click="handleStartGame" 
        style="background: #28a745; color: white;"
      >
        🚀 开始游戏
      </button>
      <button 
        v-else-if="!isOwner && opponentJoined" 
        disabled 
        style="background: #ffc107; color: #333; cursor: not-allowed;"
      >
        ⏳ 等待房主开始...
      </button>
      <button 
        v-else 
        disabled 
        style="background: #ccc; color: #666; cursor: not-allowed;"
      >
        ⏳ 等待对手加入...
      </button>
      <button @click="$emit('cancel')" style="background: #dc3545; color: white;">
        ❌ 取消等待
      </button>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'

defineProps({
  roomName: String,
  roomMode: String,
  roomId: String,
  isOwner: Boolean
})

const emit = defineEmits(['cancel', 'startGame'])

const localFirstPlayer = ref('random')
const localBoardSize = ref(15)
const localTimeLimit = ref(30)
const opponentJoined = ref(false)

const onPlayerJoined = (event) => {
  const data = event.detail
  if (data.playerCount >= 2) {
    opponentJoined.value = true
    console.log('👤 对手已加入！', data)
  }
}

const handleStartGame = () => {
  emit('startGame', {
    firstPlayer: localFirstPlayer.value,
    boardSize: localBoardSize.value,
    timeLimit: localTimeLimit.value
  })
}

onMounted(() => {
  window.addEventListener('player-joined', onPlayerJoined)
})

onUnmounted(() => {
  window.removeEventListener('player-joined', onPlayerJoined)
})
</script>

<style scoped>
.config-area {
  margin: 16px 0 20px 0;
  padding: 16px;
  background: #f8f9fa;
  border-radius: 12px;
}
.config-item {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 12px;
  margin: 8px 0;
}
.config-item label {
  font-weight: 500;
  font-size: 15px;
  color: #333;
}
.config-item select {
  padding: 6px 12px;
  border: 1px solid #ccc;
  border-radius: 8px;
  font-size: 14px;
}
.config-item select:disabled {
  background: #e9ecef;
  cursor: not-allowed;
}
.button-group {
  display: flex;
  gap: 16px;
  justify-content: center;
  margin-top: 16px;
}
button {
  padding: 10px 24px;
  font-size: 16px;
  cursor: pointer;
  border: none;
  border-radius: 8px;
  transition: 0.2s;
}
button:disabled {
  cursor: not-allowed;
}
</style>