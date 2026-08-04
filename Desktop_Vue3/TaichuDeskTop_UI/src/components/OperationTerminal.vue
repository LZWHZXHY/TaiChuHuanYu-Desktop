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
        <!-- ===== 所有菜单都由 v-for 统一渲染（包括管理菜单） ===== -->
        <div 
          v-for="item in filteredMenuItems" 
          :key="item.name"
          :class="['menu-item', { 
            active: activeName === item.name,
            'admin-item': item.isAdmin 
          }]"
          @click="handleSelect(item)"
        >
          <span class="name">{{ item.name }}</span>
          <span v-if="['太初灵脉'].includes(item.name)" style="margin-left:auto; opacity:0.5;">↗</span>
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
import { useUserStore } from '@/stores/user'
import { onMounted } from 'vue'

// ============================================================
// Props & Emits
// ============================================================
const props = defineProps<{
  menuItems: any[],
  activeName: string
}>()

const emit = defineEmits(['navigate'])

// ============================================================
// Store
// ============================================================
const userStore = useUserStore()

// ============================================================
// 状态
// ============================================================
const isExpanded = ref(false)

// ✅ 根据权限过滤菜单
const filteredMenuItems = computed(() => {
  const perms = userStore.permissions || []
  const isSuperAdmin = perms.includes('SuperAdmin')

  return props.menuItems.filter(item => {
    // 基础过滤：隐藏不需要的项
    if (['', '身份认证', '个人中心'].includes(item.name)) return false

    // ✅ 管理类菜单：只有 SuperAdmin 能看见
    if (item.isAdmin || item.name === '管理面板' || item.name === '用户治理中枢') {
      return isSuperAdmin
    }

    return true
  })
})

// ============================================================
// 用户状态
// ============================================================
const isLogin = computed(() => !!localStorage.getItem('token'))
const userName = computed(() => localStorage.getItem('username'))
const currentUserId = computed(() => localStorage.getItem('userId'))

// ============================================================
// 方法
// ============================================================
const toggleTerminal = () => {
  isExpanded.value = !isExpanded.value
}

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
  const newTabPlugins = ['太初灵脉']

  if (newTabPlugins.includes(item.name)) {
    const cleanPath = item.url.startsWith('/') ? item.url : `/${item.url}`
    const fullUrl = `${window.location.origin}/#${cleanPath}`

    const win = window.open(fullUrl, '_blank')
    if (win) {
      win.focus()
    } else {
      console.warn('弹窗被拦截，正在尝试页面内跳转...')
      emit('navigate', item)
    }
  } else {
    emit('navigate', item)
  }

  if (window.innerWidth <= 768) {
    isExpanded.value = false
  }
}

// ✅ 调试：打印过滤后的菜单
onMounted(() => {
  console.log('UserInfo:', userStore.userInfo)
  console.log('Permissions:', userStore.permissions)
  console.log('Filtered Menu Items:', filteredMenuItems.value)
})
</script>

<style scoped>
/* ... 所有样式保持不变 ... */
.terminal-wrapper {
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Helvetica, Arial, sans-serif;
}

.terminal-trigger-btn {
  position: fixed;
  left: 24px;
  bottom: 32px;
  width: 48px;
  height: 48px;
  background: #ffffff;
  border: 1px solid #d0d7de;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05);
  transition: all 0.3s cubic-bezier(0.2, 1, 0.3, 1);
  z-index: 10001;
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
  overflow-y: auto;
}

.expanded .side-panel {
  left: 0;
}

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

.panel-header {
  margin-bottom: 16px;
}

.index-label {
  font-size: 11px;
  color: #8c959f;
  text-transform: uppercase;
  letter-spacing: 0.1em;
  border-bottom: 1px solid #f0f2f4;
  padding-bottom: 8px;
  display: block;
}

.menu-list {
  flex: 1;
  display: flex;
  flex-direction: column;
}

.menu-item {
  font-size: 14px;
  padding: 10px 12px;
  color: #57606a;
  cursor: pointer;
  border-radius: 6px;
  transition: all 0.2s;
  margin-bottom: 2px;
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

/* 管理菜单项样式 */
.menu-item.admin-item {
  color: #1f2328;
  font-weight: 500;
}

.menu-item.admin-item:hover {
  background: #f6f8fa;
  color: #0969da;
}

.menu-item.admin-item.active {
  background: #f0f7ff;
  color: #0969da;
}

.terminal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(255, 255, 255, 0.7);
  backdrop-filter: blur(4px);
  z-index: 9999;
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

.side-panel::-webkit-scrollbar {
  width: 4px;
}
.side-panel::-webkit-scrollbar-track {
  background: #f6f8fa;
}
.side-panel::-webkit-scrollbar-thumb {
  background: #d0d7de;
  border-radius: 2px;
}
.side-panel::-webkit-scrollbar-thumb:hover {
  background: #b0b8c4;
}

@media (max-width: 768px) {
  .side-panel {
    width: 260px;
    padding: 28px 16px;
  }
  .terminal-trigger-btn {
    left: 16px;
    bottom: 20px;
    width: 44px;
    height: 44px;
  }
}
</style>