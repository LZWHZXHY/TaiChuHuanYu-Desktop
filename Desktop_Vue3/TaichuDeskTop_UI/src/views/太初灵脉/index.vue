<template>
  <div class="spirit-link-app" :class="{ 'is-mobile': isMobile }">
    <transition name="fade">
      <div 
        v-if="isMobile && isSidebarOpen" 
        class="mobile-overlay" 
        @click="isSidebarOpen = false"
      ></div>
    </transition>

    <SidebarIndex 
      :notes="notes" 
      :active-id="currentNoteId"
      :class="['sidebar-layer', { 'open': isSidebarOpen || !isMobile }]"
      @select="handleSelectNote"
      @create="createNewNote"
    />

    <main class="spirit-main-editor">
      <header class="editor-header">
        <div class="header-left">
          <button v-if="isMobile" class="menu-toggle-btn" @click="isSidebarOpen = true">
            <span class="icon">☰</span>
          </button>
          <div class="breadcrumb" v-else>
            我的灵脉 / {{ activeNote?.title || '未命名' }}
          </div>
        </div>
        
        <div class="action-btns">
          <transition name="pop">
            <span v-if="activeNote?.isPublished" class="publish-status-tag">
              <span class="dot"></span> 已发布至博客
            </span>
          </transition>

          <button 
            class="publish-btn" 
            :class="{ 'is-active': activeNote?.isPublished }"
            @click="handleTogglePublish"
          >
            {{ activeNote?.isPublished ? '管理发布' : '发布至博客' }}
          </button>

          <button class="save-btn" @click="handleSave">
            {{ isMobile ? '同步' : '同步至灵脉' }}
          </button>
        </div>
      </header>

      <div class="editor-scroll-body">
        <input 
          :value="activeNote?.title" 
          @input="e => updateNoteTitle(currentNoteId, (e.target as HTMLInputElement).value)"
          class="title-input" 
          placeholder="无标题灵感" 
          spellcheck="false"
        />
        
        <SpiritEditor  ref="editorRef" />
      </div>
    </main>

    <BacklinksPanel v-if="!isMobile" />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import SidebarIndex from './components/SidebarIndex.vue';
import BacklinksPanel from './components/BacklinksPanel.vue';
import SpiritEditor from '../../components/SpiritText.vue'; // 注意：确保这是你的编辑器组件路径
import { useSpiritData } from '../../composables/useSpiritData';

// 1. 引入增强后的逻辑层
const { 
  notes, 
  currentNoteId, 
  activeNote, 
  selectNote, 
  createNewNote, 
  updateNoteTitle, 
  updateNoteContent,
  togglePublish // 🌟 注入新方法
} = useSpiritData();

// 2. 状态控制
const isMobile = ref(false);
const isSidebarOpen = ref(false);
const editorRef = ref();

// 3. 手机端适配逻辑
const checkScreen = () => {
  isMobile.value = window.innerWidth <= 1024;
};

const handleSelectNote = (id: string) => {
  selectNote(id);
  if (isMobile.value) isSidebarOpen.value = false;
};

// 🌟 4. 发布处理逻辑
const handleTogglePublish = () => {
  if (currentNoteId.value) {
    togglePublish(currentNoteId.value);
  }
};

const handleSave = () => {
  const content = editorRef.value?.getJSON();
  updateNoteContent(currentNoteId.value, content);
  console.log('灵脉数据已固化同步');
};

onMounted(() => {
  checkScreen();
  window.addEventListener('resize', checkScreen);
});

onUnmounted(() => {
  window.removeEventListener('resize', checkScreen);
});
</script>

<style scoped>
.spirit-link-app {
  display: flex;
  width: 100%;
  height: 100vh;
  background: #ffffff;
  overflow: hidden;
  position: relative;
}

/* 侧边栏层级与动画 */
.sidebar-layer {
  width: 280px;
  flex-shrink: 0;
  transition: transform 0.4s cubic-bezier(0.16, 1, 0.3, 1);
  z-index: 2000;
}

/* 手机端抽屉逻辑 */
.is-mobile .sidebar-layer {
  position: absolute;
  top: 0;
  left: 0;
  bottom: 0;
  transform: translateX(-100%);
  background: #fafafa;
  box-shadow: 20px 0 50px rgba(0,0,0,0.05);
}

.is-mobile .sidebar-layer.open {
  transform: translateX(0);
}

.mobile-overlay {
  position: absolute;
  inset: 0;
  background: rgba(255,255,255,0.7);
  backdrop-filter: blur(4px);
  z-index: 1999;
}

/* 编辑器区域 */
.spirit-main-editor {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.editor-header {
  height: 60px;
  padding: 0 40px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-bottom: 1px solid #f2f2f2;
}

.editor-scroll-body {
  flex: 1;
  overflow-y: auto;
  padding: 80px 10% 100px;
}

.title-input {
  width: 100%;
  border: none;
  font-size: 3rem;
  font-weight: 800;
  margin-bottom: 40px;
  outline: none;
  background: transparent;
  letter-spacing: -0.04em;
  color: #1d1d1f;
}

.action-btns {
  display: flex;
  align-items: center;
  gap: 12px;
}

/* 🌟 新增：发布状态标签样式 */
.publish-status-tag {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 11px;
  color: #34c759;
  background: rgba(52, 199, 89, 0.08);
  padding: 4px 10px;
  border-radius: 6px;
}
.publish-status-tag .dot {
  width: 6px;
  height: 6px;
  background: #34c759;
  border-radius: 50%;
  box-shadow: 0 0 6px rgba(52, 199, 89, 0.4);
}

/* 🌟 新增：发布按钮样式 */
.publish-btn {
  background: none;
  border: 1px solid #d2d2d7;
  color: #1d1d1f;
  padding: 7px 16px;
  border-radius: 40px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.3s ease;
}
.publish-btn:hover { background: #f5f5f7; }
.publish-btn.is-active {
  border-color: #0066cc;
  color: #0066cc;
  background: rgba(0, 102, 204, 0.05);
}

.save-btn {
  background: #1d1d1f;
  color: #fff;
  border: none;
  padding: 8px 20px;
  border-radius: 40px;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
}

.menu-toggle-btn {
  background: none;
  border: none;
  font-size: 20px;
  cursor: pointer;
  padding: 10px;
  margin-left: -10px;
}

/* 动画效果 */
.fade-enter-active, .fade-leave-active { transition: opacity 0.3s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

.pop-enter-active { transition: all 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275); }
.pop-enter-from { transform: scale(0.8); opacity: 0; }

@media (max-width: 768px) {
  .editor-header { padding: 0 20px; }
  .editor-scroll-body { padding: 40px 20px; }
  .title-input { font-size: 2rem; margin-bottom: 20px; }
}
</style>