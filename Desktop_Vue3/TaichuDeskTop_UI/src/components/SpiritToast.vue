<template>
  <div class="spirit-toast-container">
    <transition-group name="toast-slide" tag="div" class="toast-list">
      <div 
        v-for="toast in toasts" 
        :key="toast.id" 
        class="spirit-toast"
      >
        <div class="toast-icon">✨</div>
        <div class="toast-content">
          <span class="toast-message">{{ toast.message }}</span>
        </div>
      </div>
    </transition-group>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';

interface Toast {
  id: string;
  message: string;
}

const toasts = ref<Toast[]>([]);

// 暴露给外部调用的触发方法
const show = (message: string = '作品已固化至灵脉', duration: number = 3000) => {
  const id = `toast_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`;
  
  toasts.value.push({ id, message });

  // 定时自动移除
  setTimeout(() => {
    const index = toasts.value.findIndex(t => t.id === id);
    if (index !== -1) {
      toasts.value.splice(index, 1);
    }
  }, duration);
};

// 将 show 方法暴露给父组件引用
defineExpose({ show });
</script>

<style scoped>
.spirit-toast-container {
  position: fixed;
  bottom: 32px; /* 放在右下角角落 */
  right: 32px;
  z-index: 10000; /* 确保在最顶层 */
  pointer-events: none; /* 防止遮挡用户的鼠标操作 */
  display: flex;
  flex-direction: column;
  align-items: flex-end;
}

.toast-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.spirit-toast {
  display: flex;
  align-items: center;
  gap: 12px;
  background: rgba(255, 255, 255, 0.85);
  backdrop-filter: blur(20px) saturate(180%);
  border: 1px solid rgba(0, 0, 0, 0.05);
  padding: 12px 20px;
  border-radius: 100px; /* 极致圆角，契合 Apple 风格 */
  box-shadow: 0 10px 40px rgba(0, 102, 204, 0.1), 0 2px 8px rgba(0, 0, 0, 0.05);
  color: #1d1d1f;
  font-size: 14px;
  font-weight: 600;
  pointer-events: auto; /* 恢复自身元素的鼠标交互 */
}

.toast-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  background: #f0f5ff;
  border-radius: 50%;
  font-size: 14px;
  color: #0066cc;
}

.toast-message {
  letter-spacing: 0.2px;
}

/* 进出动画 */
.toast-slide-enter-active,
.toast-slide-leave-active {
  transition: all 0.4s cubic-bezier(0.16, 1, 0.3, 1);
}

.toast-slide-enter-from {
  opacity: 0;
  transform: translateX(40px) scale(0.9);
}

.toast-slide-leave-to {
  opacity: 0;
  transform: translateY(-20px) scale(0.9);
}

/* 确保移除元素时其他元素能平滑移动补位 */
.toast-slide-leave-active {
  position: absolute;
}
</style>