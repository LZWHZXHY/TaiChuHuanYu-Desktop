<template>
  <div class="terminal-wrapper" :class="{ 'expanded': isExpanded }">
    <div class="terminal-trigger-btn" @click.stop="toggleTerminal">
      <span class="trigger-icon">{{ isExpanded ? '✕' : '☰' }}</span>
      <div v-if="!isExpanded" class="pulse-hint"></div>
    </div>

    <nav class="side-panel">
      <div class="user-profile" @click="handleAuthClick">
        <div class="avatar">
          <div class="avatar-placeholder">{{ userName?.[0]?.toUpperCase() || '?' }}</div>
        </div>
        <div class="user-info">
          <span class="user-name">{{ userName || '未认证漫游者' }}</span>
          <span class="user-status">{{ isLogin ? '已接入灵脉' : '点击进行身份筑基' }}</span>
        </div>
      </div>

      <div class="panel-header">
        <span class="index-label">索引 / Index</span>
      </div>

      <div class="menu-list">
        <div 
          v-for="item in menuItems" 
          :key="item.name"
          :class="['menu-item', { active: activeName === item.name }]"
          @click="handleSelect(item)"
        >
          <span class="name">{{ item.name }}</span>
        </div>
      </div>
    </nav>

    <Transition name="fade">
      <div v-if="isExpanded" class="terminal-overlay" @click="toggleTerminal"></div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'

const props = defineProps<{
  menuItems: any[],
  activeName: string
}>()

const emit = defineEmits(['navigate'])

const isExpanded = ref(false)

// 身份状态判定（对接 localStorage）
const isLogin = computed(() => !!localStorage.getItem('token'))
const userName = computed(() => localStorage.getItem('username'))
const currentUserId = computed(() => localStorage.getItem('userId'))


const toggleTerminal = () => {
  isExpanded.value = !isExpanded.value
}

// 点击头像跳转
const handleAuthClick = () => {
  if (isLogin.value) {
    emit('navigate', { 
      name: '个人中心', 
      url: `/user/${currentUserId.value}` 
    })
  } else {
    emit('navigate', { 
      name: '身份认证', 
      url: '/LoginRegister' 
    })
  }
  isExpanded.value = false
}

const handleSelect = (item: any) => {
  emit('navigate', item)
  // 移动端点击后自动关闭
  if (window.innerWidth <= 768) {
    isExpanded.value = false
  }
}
</script>

<style scoped>
.terminal-wrapper {
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Helvetica, Arial, sans-serif;
}

/* 悬浮按钮样式 */
.terminal-trigger-btn {
  position: fixed;
  left: 24px;
  bottom: 32px;
  width: 48px;
  height: 48px;
  background: #ffffff;
  border: 1px solid #d0d7de; /* MD 风格浅灰边框 */
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05);
  transition: all 0.3s cubic-bezier(0.2, 1, 0.3, 1);
  z-index: 10001; /* 确保最高层级 */
}

.terminal-trigger-btn:hover {
  transform: translateY(-2px);
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.08);
  border-color: #0969da;
}

.trigger-icon {
  font-size: 18px;
  color: #1f2328;
}

/* 呼吸提示点 */
.pulse-hint {
  position: absolute;
  inset: -1px;
  border: 2px solid #0969da;
  border-radius: 50%;
  animation: pulse 2.5s infinite;
  pointer-events: none;
}

@keyframes pulse {
  0% { transform: scale(1); opacity: 0.5; }
  100% { transform: scale(1.4); opacity: 0; }
}

/* 侧边面板 */
.side-panel {
  position: fixed;
  left: -300px;
  top: 0;
  bottom: 0;
  width: 280px;
  background: #ffffff;
  border-right: 1px solid #d0d7de;
  transition: left 0.4s cubic-bezier(0.2, 1, 0.3, 1);
  padding: 40px 24px;
  z-index: 10000;
  display: flex;
  flex-direction: column;
}

.expanded .side-panel {
  left: 0;
}

/* 头像区域样式 */
.user-profile {
  display: flex;
  align-items: center;
  padding: 12px;
  margin-bottom: 32px;
  cursor: pointer;
  border-radius: 8px;
  transition: background 0.2s;
  border: 1px solid transparent;
}

.user-profile:hover {
  background: #f6f8fa;
  border-color: #d0d7de;
}

.avatar {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: #eff1f3;
  border: 1px solid #d0d7de;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.avatar-placeholder {
  font-family: ui-monospace, SFMono-Regular, monospace;
  color: #57606a;
  font-weight: 600;
}

.user-info {
  margin-left: 12px;
  display: flex;
  flex-direction: column;
}

.user-name {
  font-size: 14px;
  font-weight: 600;
  color: #1f2328;
}

.user-status {
  font-size: 11px;
  color: #8c959f;
  margin-top: 2px;
}

.index-label {
  font-size: 11px;
  color: #8c959f;
  text-transform: uppercase;
  letter-spacing: 0.1em;
  margin-bottom: 16px;
  display: block;
  border-bottom: 1px solid #f0f2f4;
  padding-bottom: 8px;
}

/* 菜单项 */
.menu-item {
  font-size: 14px;
  padding: 10px 12px;
  color: #57606a;
  cursor: pointer;
  border-radius: 6px;
  transition: all 0.2s;
  margin-bottom: 4px;
}

.menu-item:hover {
  background: #f6f8fa;
  color: #0969da;
}

.menu-item.active {
  color: #1f2328;
  font-weight: 600;
  background: #f0f7ff;
}

/* 遮罩 */
.terminal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(255, 255, 255, 0.7);
  backdrop-filter: blur(4px);
  z-index: 9999;
}

.fade-enter-active, .fade-leave-active { transition: opacity 0.3s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>