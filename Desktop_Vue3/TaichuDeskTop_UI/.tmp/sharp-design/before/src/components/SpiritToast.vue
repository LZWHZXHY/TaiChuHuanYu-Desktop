<template>
  <div class="spirit-toast-container">
    <transition-group name="toast-slide" tag="div" class="toast-list">
      <div
        v-for="toast in toasts"
        :key="toast.id"
        class="spirit-toast"
        :class="toast.type ? `toast-${toast.type}` : ''"
      >
        <div class="toast-icon">{{ toast.icon || '✨' }}</div>
        <div class="toast-content">
          <span class="toast-message">{{ toast.message }}</span>
        </div>
      </div>
    </transition-group>
  </div>
</template>

<script setup lang="ts">
import { ref, shallowRef } from 'vue';

interface ToastItem {
  id: string;
  message: string;
  icon?: string;
  type?: 'success' | 'error' | 'info';
}

const toasts = ref<ToastItem[]>([]);

/**
 * 显示一个 Toast 提示
 * @param message 提示文本
 * @param duration 显示时长（毫秒），默认 3000
 * @param icon 自定义图标，默认 '✨'
 * @param type 可选类型，用于样式差异化
 */
const show = (
  message: string = '作品已固化至灵脉',
  duration: number = 3000,
  icon: string = '✨',
  type?: 'success' | 'error' | 'info'
) => {
  const id = `toast_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`;
  const toast: ToastItem = { id, message, icon, type };
  toasts.value.push(toast);

  setTimeout(() => {
    const index = toasts.value.findIndex((t) => t.id === id);
    if (index !== -1) {
      toasts.value.splice(index, 1);
    }
  }, duration);
};

// 暴露给父组件
defineExpose({ show });
</script>

<style scoped>
.spirit-toast-container {
  position: fixed;
  bottom: 32px;
  right: 32px;
  z-index: 10000;
  pointer-events: none;
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
  border-radius: 100px;
  box-shadow: 0 10px 40px rgba(0, 102, 204, 0.1), 0 2px 8px rgba(0, 0, 0, 0.05);
  color: #1d1d1f;
  font-size: 14px;
  font-weight: 600;
  pointer-events: auto;
  transition: all 0.2s;
}

/* 可选：不同颜色的类型提示 */
.toast-success .toast-icon {
  background: #e8f5e9;
  color: #2e7d32;
}
.toast-error .toast-icon {
  background: #ffebee;
  color: #c62828;
}
.toast-info .toast-icon {
  background: #e3f2fd;
  color: #0d47a1;
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
  flex-shrink: 0;
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

.toast-slide-leave-active {
  position: absolute;
}
</style>