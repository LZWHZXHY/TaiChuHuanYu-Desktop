<template>

    <div>
        <h2>创建自定义五子棋房间</h2>

        <div>
           <label>房间名称：</label> <input v-model="localRoomName" placeholder="输入房间名" />
        </div>

        <div class = "mode-selector">
            <p>选择模式:</p>
            <div class = mode-cards>
                <div class = "mode-card" :class = "{active: selectedMode === 'classic'}" @click="selectedMode = 'classic'">
                    <h3>传统模式</h3>
                    <p>15x15 标准棋盘，五子连珠获胜</p>
                </div>
            </div>
            <div class="mode-card" :class="{ active: selectedMode === 'custom' }" @click="selectedMode = 'custom'">
                <h3>⚙️ 客制化模式</h3>
                <p>自定义棋盘大小和胜利条件</p>
            </div>

        </div>



        <div>
            <button @click = "handleCreate">创建房间</button>
            <button @click = "$emit('back')">返回大厅</button>
        </div>

    </div>

</template>

<script setup>

import {ref} from 'vue'

const localRoomName = ref('')
const emit = defineEmits(['create', 'back'])
const selectedMode = ref('classic')


// 在创建组件中修改 emit 传参
const handleCreate = () => {
  if (!localRoomName.value.trim()) {
    alert('请先输入房间名称！')
    return
  }
  
  // 补齐 boardSize (例如默认 15) 和 timeLimit (例如默认 30)
  emit('create', { 
    roomName: localRoomName.value, 
    mode: selectedMode.value,
    boardSize: 15,     // 视你的表单而定，如果客制化模式可选大小，可绑定变量
    timeLimit: 30 
  })
}

</script>

<style>
.mode-selector {
  margin: 20px 0;
}
.mode-cards {
  display: flex;
  gap: 16px;
  justify-content: center;
}
.mode-card {
  border: 2px solid #ddd;
  border-radius: 12px;
  padding: 16px 24px;
  cursor: pointer;
  transition: all 0.2s;
  flex: 1;
  max-width: 180px;
  background: #fff;
}
.mode-card:hover {
  border-color: #888;
}
.mode-card.active {
  border-color: #0d6efd;
  background: #e7f1ff;
  box-shadow: 0 0 0 3px rgba(13, 110, 253, 0.2);
}
.mode-card h3 {
  margin: 0 0 6px 0;
  font-size: 16px;
}
.mode-card p {
  margin: 0;
  font-size: 13px;
  color: #666;
}
</style>