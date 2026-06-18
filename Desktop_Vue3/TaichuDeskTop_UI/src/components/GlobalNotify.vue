<template>
  <Transition name="notify-fade">
    <div v-if="visible" class="error-toast" :class="type">
      <div class="error-header">
        <div class="header-left">
          <span class="icon">{{ type === 'error' ? '⚠️' : '✅' }}</span>
          <span class="title">{{ title }}</span>
        </div>
        <button @click="visible = false" class="close-btn">×</button>
      </div>
      
      <div class="error-body">
        <p class="main-msg">{{ message }}</p>
        
        <details v-if="details" class="error-details">
          <summary>查看灵脉异常详情 (Debug)</summary>
          <div class="pre-wrapper">
            <pre><code>{{ details }}</code></pre>
          </div>
        </details>
      </div>

      <div v-if="autoClose" class="progress-bar" :style="{ animationDuration: duration + 'ms' }"></div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import { bus } from '../utils/bus';

// --- 状态定义 ---
const visible = ref(false);
const title = ref('');
const message = ref('');
const details = ref('');
const type = ref('error');
const autoClose = ref(false);
const duration = ref(8000); // 默认 8 秒

// --- 核心逻辑：响应总线事件 ---
const handleApiError = (data: { msg: string; detail?: string; type?: string; title?: string }) => {
  // 填充数据
  title.value = data.title || '检测报错 (Debug)';
  message.value = data.msg || '未知网络波动';
  details.value = data.detail || '';
  type.value = data.type || 'error';
  
  // 显示组件
  visible.value = true;

  // 逻辑：如果是 error 类型，为了方便复制错误信息，不自动关闭；如果是 success/info，则自动关闭
  if (type.value !== 'error') {
    autoClose.value = true;
    setTimeout(() => {
      visible.value = false;
    }, duration.value);
  } else {
    autoClose.value = false;
  }
};

// --- 生命周期 ---
onMounted(() => {
  bus.on('api-error', handleApiError);
});

onUnmounted(() => {
  bus.off('api-error', handleApiError);
});
</script>

<style scoped>
/* 容器基础样式 */
.error-toast {
  position: fixed;
  top: 24px;
  right: 24px;
  z-index: 10000;
  background: #ffffff;
  border-radius: 8px;
  box-shadow: 0 6px 16px -8px rgba(0,0,0,0.08), 0 9px 28px 0 rgba(0,0,0,0.05), 0 12px 48px 16px rgba(0,0,0,0.03);
  padding: 16px;
  width: 380px;
  overflow: hidden;
  border: 1px solid #f0f0f0;
}

/* 类型区分 */
.error-toast.error { border-left: 4px solid #ff4d4f; background: #fff2f0; }
.error-toast.success { border-left: 4px solid #52c41a; background: #f6ffed; }

/* 头部样式 */
.error-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}
.header-left { display: flex; align-items: center; gap: 8px; }
.title { font-weight: 600; font-size: 14px; color: #262626; }
.close-btn {
  background: transparent;
  border: none;
  font-size: 18px;
  cursor: pointer;
  color: #8c8c8c;
  line-height: 1;
}
.close-btn:hover { color: #262626; }

/* 主体样式 */
.main-msg {
  font-size: 14px;
  color: #595959;
  line-height: 1.5;
  margin: 0 0 12px 0;
}

/* Debug 详情面板 */
.error-details {
  margin-top: 8px;
  border: 1px solid #ffccc7;
  border-radius: 4px;
  background: #ffffff;
}
.error-details summary {
  padding: 4px 8px;
  font-size: 12px;
  color: #ff4d4f;
  cursor: pointer;
  user-select: none;
  background: #fff1f0;
}
.pre-wrapper {
  max-height: 200px;
  overflow: auto;
  padding: 8px;
  background: #1e1e1e; /* 黑色背景更有极客感 */
}
.error-details pre {
  margin: 0;
  font-family: 'Consolas', monospace;
  font-size: 11px;
  color: #dcdcdc;
  white-space: pre-wrap;
  word-break: break-all;
}

/* 进度条动画 */
.progress-bar {
  position: absolute;
  bottom: 0;
  left: 0;
  height: 2px;
  background: #52c41a;
  width: 100%;
  transform-origin: left;
  animation: progress-linear linear forwards;
}

@keyframes progress-linear {
  from { transform: scaleX(1); }
  to { transform: scaleX(0); }
}

/* 过渡动画 */
.notify-fade-enter-active { transition: all 0.4s cubic-bezier(0.23, 1, 0.32, 1); }
.notify-fade-leave-active { transition: all 0.3s ease; }
.notify-fade-enter-from { opacity: 0; transform: translateX(100px); }
.notify-fade-leave-to { opacity: 0; transform: scale(0.9); }
</style>