<template>
  <Teleport to="body">
    <TransitionGroup name="exp-float">
      <div 
        v-for="item in queue" 
        :key="item.id" 
        class="exp-toast"
        :style="{ top: 20 + item.index * 60 + 'px' }"
      >
        <div class="exp-content">
          <span class="exp-icon">✨</span>
          <span class="exp-text">经验 <span class="exp-num">+{{ item.amount }}</span></span>
        </div>
        <div class="exp-glimmer"></div>
      </div>
    </TransitionGroup>
  </Teleport>
</template>

<script setup lang="ts">
import { ref } from 'vue'

interface ExpItem {
  id: number
  amount: number
  index: number
}

const queue = ref<ExpItem[]>([])
let count = 0

// 暴露给外部调用的方法
const show = (amount: number) => {
  const id = count++
  const item = { id, amount, index: queue.value.length }
  queue.value.push(item)

  // 3秒后移除
  setTimeout(() => {
    queue.value = queue.value.filter(i => i.id !== id)
    // 重新计算索引以平滑移动
    queue.value.forEach((i, idx) => i.index = idx)
  }, 3000)
}

defineExpose({ show })
</script>

<style scoped>
.exp-toast {
  position: fixed;
  right: 20px;
  z-index: 9999;
  background: rgba(36, 41, 47, 0.9);
  backdrop-filter: blur(8px);
  border: 1px solid rgba(207, 138, 5, 0.3);
  padding: 12px 20px;
  border-radius: 8px;
  color: #fff;
  display: flex;
  align-items: center;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  transition: all 0.5s cubic-bezier(0.18, 0.89, 0.32, 1.28);
}

.exp-content {
  display: flex;
  align-items: center;
  gap: 10px;
  font-weight: bold;
}

.exp-icon { font-size: 1.2rem; }
.exp-num { color: #cf8a05; font-family: 'Monaco', monospace; }

/* 灵光微动动画 */
.exp-glimmer {
  position: absolute;
  top: 0; left: 0; width: 100%; height: 100%;
  background: linear-gradient(90deg, transparent, rgba(207, 138, 5, 0.2), transparent);
  animation: glimmer 2s infinite;
}

@keyframes glimmer {
  0% { transform: translateX(-100%); }
  100% { transform: translateX(100%); }
}

/* 过渡动画 */
.exp-float-enter-from { opacity: 0; transform: translateX(30px) scale(0.9); }
.exp-float-leave-to { opacity: 0; transform: translateY(-20px); }
</style>