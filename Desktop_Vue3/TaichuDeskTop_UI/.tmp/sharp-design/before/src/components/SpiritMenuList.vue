<template>
  <div class="spirit-floating-menu">
    <div class="menu-header">灵脉指令</div>
    <div 
      v-for="(cmd, index) in items" 
      :key="index" 
      :class="['menu-item', { 'is-active': index === selectedIndex }]" 
      @click="selectItem(index)"
      @mouseenter="selectedIndex = index"
    >
      <div class="item-icon">{{ cmd.icon }}</div>
      <div class="item-text">{{ cmd.label }}</div>
    </div>
  </div>
</template>

<script setup>
import { ref, watch } from 'vue'

// 接收 Tiptap 传过来的数据
const props = defineProps({
  items: { type: Array, required: true },
  command: { type: Function, required: true }
})

const selectedIndex = ref(0)

// 当菜单内容刷新时，高亮自动归零
watch(() => props.items, () => {
  selectedIndex.value = 0
})

// 🌟 这里是精髓：提供给外部（Tiptap）调用的公开方法，处理键盘
const onKeyDown = ({ event }) => {
  if (event.key === 'ArrowUp') {
    selectedIndex.value = (selectedIndex.value + props.items.length - 1) % props.items.length
    scrollIntoView()
    return true // 告诉系统：我处理了这个按键，不要让页面滚动了
  }
  if (event.key === 'ArrowDown') {
    selectedIndex.value = (selectedIndex.value + 1) % props.items.length
    scrollIntoView()
    return true
  }
  if (event.key === 'Enter') {
    selectItem(selectedIndex.value)
    return true
  }
  return false
}

// 执行选中的命令
const selectItem = (index) => {
  const item = props.items[index]
  if (item) {
    props.command(item) // 把选中的命令丢回给 Tiptap
  }
}

const scrollIntoView = () => {
  // 简单的随动滚动逻辑
  setTimeout(() => {
    const activeEl = document.querySelector('.spirit-floating-menu .is-active')
    if (activeEl) activeEl.scrollIntoView({ block: 'nearest' })
  }, 10)
}

// 暴露出这个方法，让 Tiptap 能“遥控”这个组件的键盘事件
defineExpose({ onKeyDown })
</script>

<style scoped>
.spirit-floating-menu {
  position: fixed; 
  width: 280px;
  max-height: 260px;
  background: rgba(255, 255, 255, 0.95);
  backdrop-filter: blur(20px);
  border: 1px solid rgba(0, 0, 0, 0.08);
  border-radius: 14px;
  box-shadow: 0 12px 40px rgba(0, 0, 0, 0.1);
  padding: 8px;
  z-index: 9999;
  display: flex;
  flex-direction: column;
  overflow-y: auto;
  overscroll-behavior: contain;
}

.menu-header {
  font-size: 11px;
  color: #a1a1a6;
  padding: 8px 12px;
  text-transform: uppercase;
  letter-spacing: 0.1em;
  font-weight: 700;
}

.menu-scroll-area {
  overflow-y: auto;
  flex: 1;
  scrollbar-width: none;
}
.menu-scroll-area::-webkit-scrollbar { display: none; }

.menu-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  border-radius: 10px;
  cursor: pointer;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
}

.menu-item:hover,
.menu-item.is-active {
  background: rgba(0, 102, 204, 0.06);
  color: #0066cc;
}

.menu-item:hover .item-icon,
.menu-item.is-active .item-icon {
  background: #0066cc;
  color: #ffffff;
}

.item-icon {
  width: 32px;
  height: 32px;
  background: #f2f2f7;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  color: #1d1d1f;
  transition: all 0.2s;
}

.item-text {
  flex: 1;
  display: flex;
  flex-direction: column;
}

.main-title {
  font-size: 14px;
  font-weight: 600;
  color: #1d1d1f;
}

.sub-info {
  font-size: 11px;
  color: #86868b;
  margin-top: 2px;
}

.menu-empty {
  padding: 30px 20px;
  text-align: center;
  color: #c7c7cc;
  font-size: 13px;
}

.menu-pop-enter-active,
.menu-pop-leave-active {
  transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
}

.menu-pop-enter-from,
.menu-pop-leave-to {
  opacity: 0;
  transform: scale(0.95) translateY(-10px);
}

.line-fade-enter-active,
.line-fade-leave-active {
  transition: opacity 0.3s ease;
}
.line-fade-enter-from,
.line-fade-leave-to {
  opacity: 0;
}

@media (max-width: 768px) {
  .spirit-floating-menu {
    width: calc(100vw - 32px);
    left: 16px !important;
    right: 16px !important;
  }
}
</style>