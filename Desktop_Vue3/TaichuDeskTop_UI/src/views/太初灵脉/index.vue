<template>
  <div class="spirit-link-app" :class="{ 'is-mobile': isMobile }">
    <transition name="fade">
      <GraphView 
        v-if="isGraphViewOpen" 
        @close="isGraphViewOpen = false" 
        @select-note="handleSelectNote"
      />
    </transition>

    <transition name="fade">
      <div v-if="isLoading && notes.length === 0" class="loading-overlay">
        <div class="spirit-loading-content">
          <div class="spirit-spinner"></div>
          <p>正在感应灵脉数据...</p>
        </div>
      </div>
    </transition>

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
      @create="handleCreateNote"
    />

    <main class="spirit-main-editor">
      <header class="editor-header">
        <div class="header-left">
          <button v-if="isMobile" class="menu-toggle-btn" @click="isSidebarOpen = true">
            <span class="icon">☰</span>
          </button>
          <div class="breadcrumb" v-else>
            <span class="root">我的灵脉</span>
            <span class="sep">/</span>
            <span class="current">{{ activeNote?.title || '未命名碎片' }}</span>
          </div>
        </div>
        
        <div class="action-btns">
          <button class="graph-trigger-btn" @click="isGraphViewOpen = true" title="全屏网状星图">
            <span class="icon">🕸️</span> 全屏图谱
          </button>

          <button 
            class="history-trigger-btn" 
            @click="isHistoryOpen = true" 
            title="查看时间线"
            :disabled="!activeNote"
          >
            <span class="icon">🕒</span>
          </button>

          <transition name="pop">
            <span v-if="activeNote?.isPublic" class="publish-status-tag">
              <span class="dot"></span> 已发布至{{ activeNote?.type === 'thought' ? '简语广场' : '博客' }}
            </span>
          </transition>

          <button 
            class="publish-btn" 
            :class="{ 'is-active': activeNote?.isPublic }"
            :disabled="!activeNote"
            @click="handlePublishClick"
          >
            {{ activeNote?.isPublic ? '取消发布' : '发布至广场' }}
          </button>

          <button class="save-btn" @click="handleSave" :disabled="!activeNote">
            {{ isMobile ? '同步' : '同步至灵脉' }}
          </button>
        </div>
      </header>

      <div class="editor-scroll-body">
        <template v-if="activeNote">
          <input 
            :value="activeNote.title" 
            @input="e => updateNoteTitle(currentNoteId, (e.target as HTMLInputElement).value)"
            class="title-input" 
            placeholder="无标题灵感" 
            spellcheck="false"
          />
          
          <SpiritEditor ref="editorRef" />
        </template>
        
        <div v-else-if="!isLoading" class="no-note-selected">
          <div class="empty-state-content">
            <span class="empty-icon">✨</span>
            <p>选择一篇碎片，或者创建新的灵脉</p>
            <button @click="() => handleCreateNote()" class="create-first-btn">创建新笔记</button>
          </div>
        </div>
      </div>
    </main>

    <BacklinksPanel 
      v-if="!isMobile && activeNote && currentNoteId" 
      :note-id="currentNoteId"
      @select="handleSelectNote"
    />

    <HistoryPanel 
      v-model="isHistoryOpen" 
      :note-id="currentNoteId" 
      @rollback="onRollback"
      @manual-save="handleManualSave"
    />

    <transition name="pop">
      <div v-if="showPublishModal" class="spirit-publish-modal-overlay" @click.self="showPublishModal = false">
        <div class="spirit-publish-modal">
          <div class="modal-header">
            <h3>选择发布形态</h3>
            <p>选择此灵感碎片在广场视界中的折射形态</p>
          </div>
          
          <div class="publish-type-options">
            <div 
              class="option-card" 
              :class="{ active: selectedPublishType === 'note' }"
              @click="selectedPublishType = 'note'"
            >
              <div class="card-icon">📝</div>
              <div class="card-text">
                <span class="title">随笔博客 (Blog)</span>
                <span class="desc">适合长篇大论、深度思考的内容。保留在左侧目录树视界中。</span>
              </div>
            </div>

            <div 
              class="option-card" 
              :class="{ active: selectedPublishType === 'thought' }"
              @click="selectedPublishType = 'thought'"
            >
              <div class="card-icon">💬</div>
              <div class="card-text">
                <span class="title">日常简语 (Post)</span>
                <span class="desc">适合随笔记录、日常灵感与吐槽。不再占用侧边栏目录空间。</span>
              </div>
            </div>
          </div>

          <div class="modal-footer">
            <button class="modal-btn-cancel" @click="showPublishModal = false">取消</button>
            <button class="modal-btn-confirm" @click="confirmPublish">确认发布</button>
          </div>
        </div>
      </div>
    </transition>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import SidebarIndex from './components/SidebarIndex.vue';
import BacklinksPanel from './components/BacklinksPanel.vue';
import SpiritEditor from '../../components/SpiritText.vue'; 
import HistoryPanel from './components/HistoryPanel.vue';
import GraphView from './components/GraphView.vue';
import { useSpiritData } from '../../composables/useSpiritData';
import { lingmaiApi } from '../../api/lingmai';

const { 
  notes, 
  currentNoteId, 
  activeNote, 
  isLoading,
  fetchAllNotes, 
  selectNote, 
  createNewNote, 
  updateNoteTitle, 
  updateNoteContent,
  togglePublish   
} = useSpiritData();

const isMobile = ref(false);
const isSidebarOpen = ref(false);
const isHistoryOpen = ref(false); 
const isGraphViewOpen = ref(false);
const editorRef = ref();

// 🌟 新增：发布流程控制状态
const showPublishModal = ref(false);
const selectedPublishType = ref<'note' | 'thought'>('note');

const checkScreen = () => {
  isMobile.value = window.innerWidth <= 1024;
};

const handleSelectNote = async (id: string) => {
  if (isMobile.value) isSidebarOpen.value = false;
  await selectNote(id);
};

const handleCreateNote = async (type: 'note' | 'folder' = 'note', folderId: string | null = null) => {
  const newNote = await createNewNote({ type: type, folderId: folderId });
  if (newNote && isMobile.value && type === 'note') {
    isSidebarOpen.value = false;
  }
};

// 🌟 确认发布（同步覆盖到发布表）
const confirmPublish = async () => {
  if (!currentNoteId.value) return;

  try {
    // 调用物理隔离发布接口
    await lingmaiApi.publishNote(currentNoteId.value, selectedPublishType.value);
    
    // 更新本地内存中的状态，触发 UI 变化
    if (activeNote.value) {
      activeNote.value.isPublic = true;
      activeNote.value.type = selectedPublishType.value;
    }
    
    showPublishModal.value = false;
  } catch (err) {
    console.error('发布失败:', err);
  }
};

// 🌟 取消发布（从广场物理下线）
const handleUnpublish = async () => {
  if (!currentNoteId.value) return;

  try {
    // 调用物理隔离下线接口
    await lingmaiApi.unpublishNote(currentNoteId.value);
    
    // 更新本地内存状态
    if (activeNote.value) {
      activeNote.value.isPublic = false;
    }
  } catch (err) {
    console.error('取消发布失败:', err);
  }
};

// 🌟 统一发布按钮点击事件拦截
const handlePublishClick = () => {
  if (!activeNote.value) return;

  // 如果已经发布，点击直接调用 handleUnpublish 物理下线
  if (activeNote.value.isPublic) {
    handleUnpublish(); 
  } else {
    // 未发布状态，呼出选择弹窗
    selectedPublishType.value = (activeNote.value.type === 'thought' || activeNote.value.type === 'note') 
      ? activeNote.value.type as 'note' | 'thought' 
      : 'note';
    showPublishModal.value = true;
  }
};

const handleSave = async () => {
  if (!currentNoteId.value || !editorRef.value) return;
  const content = editorRef.value.getJSON();
  await updateNoteContent(currentNoteId.value, content);
};

const onRollback = async (revision: any) => {
  try {
    await lingmaiApi.rollbackTo(currentNoteId.value, revision.id);
    const freshNote = await selectNote(currentNoteId.value, true) as any;

    if (editorRef.value && freshNote?.tiptapContent) {
      editorRef.value.isInitialized = false;
      const editorInstance = editorRef.value.editor;
      
      if (editorInstance) {
        editorInstance.commands.setContent(freshNote.tiptapContent);
        editorRef.value.lastSyncedJson = JSON.stringify(freshNote.tiptapContent);
        console.log('✅ 编辑器已强制刷新为历史版本');
      }

      setTimeout(() => {
        if (editorRef.value) editorRef.value.isInitialized = true;
      }, 500);
    }
    isHistoryOpen.value = false;
  } catch (e) {
    console.error('回溯逻辑执行失败:', e);
    alert('同步回滚内容失败，请尝试刷新页面');
  }
};

const handleManualSave = async () => {
  if (!editorRef.value || !currentNoteId.value) return;
  const content = editorRef.value.getJSON();
  
  try {
    await lingmaiApi.createSnapshot(currentNoteId.value, content, "用户手动固化");
    console.log('✨ 灵脉节点已固化');
  } catch (e) {
    console.error('固化失败:', e);
  }
};

onMounted(async () => {
  checkScreen();
  window.addEventListener('resize', checkScreen);

  await fetchAllNotes();
  if (currentNoteId.value) {
    await selectNote(currentNoteId.value);
  }
});

onUnmounted(() => {
  window.removeEventListener('resize', checkScreen);
});
</script>

<style scoped>
.spirit-link-app {
  display: flex;
  width: 100%;
  height: 100%;
  background: #ffffff;
  overflow: hidden;
  position: relative;
}

.sidebar-layer {
  width: 280px;
  flex-shrink: 0;
  transition: transform 0.4s cubic-bezier(0.16, 1, 0.3, 1);
  z-index: 2000;
  border-right: 1px solid #f2f2f2;
}

.loading-overlay {
  position: fixed;
  inset: 0;
  background: rgba(255, 255, 255, 0.9);
  backdrop-filter: blur(10px);
  z-index: 9999;
  display: flex;
  align-items: center;
  justify-content: center;
}
.spirit-loading-content { text-align: center; color: #86868b; }
.spirit-spinner {
  width: 32px;
  height: 32px;
  border: 2px solid #f3f3f3;
  border-top: 2px solid #0066cc;
  border-radius: 50%;
  margin: 0 auto 16px;
  animation: spin 1s linear infinite;
}

.graph-trigger-btn {
  background: rgba(0, 102, 204, 0.05);
  border: 1px solid #0066cc;
  color: #0066cc;
  padding: 0 14px;
  height: 32px;
  border-radius: 40px;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 6px;
  transition: all 0.2s;
  flex-shrink: 0;
}
.graph-trigger-btn:hover {
  background: rgba(0, 102, 204, 0.1);
}

.history-trigger-btn {
  background: none;
  border: 1px solid #d2d2d7;
  width: 32px;
  height: 32px;
  border-radius: 50%;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
  color: #86868b;
  margin-right: 4px;
  flex-shrink: 0;
}
.history-trigger-btn:hover:not(:disabled) {
  background: #f5f5f7;
  border-color: #1d1d1f;
  color: #1d1d1f;
  transform: rotate(-15deg);
}
.history-trigger-btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.is-mobile .sidebar-layer {
  position: absolute;
  top: 0; left: 0; bottom: 0;
  transform: translateX(-100%);
  background: #fafafa;
  box-shadow: 20px 0 50px rgba(0,0,0,0.05);
}
.is-mobile .sidebar-layer.open { transform: translateX(0); }

.mobile-overlay {
  position: absolute;
  inset: 0;
  background: rgba(255,255,255,0.7);
  backdrop-filter: blur(4px);
  z-index: 1999;
}

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
  background: #ffffff;
  flex-shrink: 0;
}

.breadcrumb { 
  font-size: 13px; 
  color: #86868b; 
  display: flex; 
  gap: 8px; 
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.breadcrumb .current { color: #1d1d1f; font-weight: 500; }

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
  flex-shrink: 0;
}

.publish-status-tag {
  display: flex; align-items: center; gap: 6px;
  font-size: 11px; color: #34c759;
  background: rgba(52, 199, 89, 0.08);
  padding: 4px 10px; border-radius: 6px;
  white-space: nowrap;
}
.publish-status-tag .dot {
  width: 6px; height: 6px; background: #34c759;
  border-radius: 50%; box-shadow: 0 0 6px rgba(52, 199, 89, 0.4);
}

.publish-btn {
  background: none; border: 1px solid #d2d2d7;
  color: #1d1d1f; padding: 7px 16px; border-radius: 40px;
  font-size: 13px; font-weight: 500; cursor: pointer;
  transition: all 0.3s ease;
  white-space: nowrap;
  flex-shrink: 0;
}
.publish-btn.is-active { border-color: #0066cc; color: #0066cc; background: rgba(0, 102, 204, 0.05); }

.save-btn {
  background: #1d1d1f; color: #fff; border: none;
  padding: 8px 20px; border-radius: 40px;
  font-size: 13px; font-weight: 600; cursor: pointer;
  white-space: nowrap;
  flex-shrink: 0;
}
.save-btn:disabled { background: #d2d2d7; cursor: not-allowed; }

.no-note-selected {
  height: 100%; display: flex; align-items: center; justify-content: center;
  text-align: center; color: #86868b;
}
.empty-icon { font-size: 40px; display: block; margin-bottom: 16px; }
.create-first-btn {
  margin-top: 20px; background: #0066cc; color: white;
  border: none; padding: 8px 24px; border-radius: 20px; cursor: pointer;
}

/* ========================================================================== */
/* 🌟 新增：发布弹窗样式 */
/* ========================================================================== */
.spirit-publish-modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.4);
  backdrop-filter: blur(8px);
  z-index: 3000;
  display: flex;
  align-items: center;
  justify-content: center;
}

.spirit-publish-modal {
  background: white;
  width: 420px;
  border-radius: 16px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.15);
  padding: 24px;
  display: flex;
  flex-direction: column;
}

.modal-header h3 {
  font-size: 18px;
  font-weight: 700;
  color: #1d1d1f;
  margin: 0 0 4px 0;
}
.modal-header p {
  font-size: 12px;
  color: #86868b;
  margin: 0;
}

.publish-type-options {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin: 20px 0;
}

.option-card {
  display: flex;
  align-items: flex-start;
  gap: 14px;
  padding: 14px;
  border: 2px solid #f2f2f7;
  border-radius: 12px;
  cursor: pointer;
  transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
}
.option-card:hover {
  background: #fdfdfd;
  border-color: #d2d2d7;
}
.option-card.active {
  border-color: #0066cc;
  background: rgba(0, 102, 204, 0.03);
}

.card-icon {
  font-size: 24px;
  line-height: 1;
}
.card-text {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.card-text .title {
  font-size: 14px;
  font-weight: 600;
  color: #1d1d1f;
}
.card-text .desc {
  font-size: 11px;
  color: #86868b;
  line-height: 1.4;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}
.modal-btn-cancel {
  background: #f5f5f7;
  color: #1d1d1f;
  border: none;
  padding: 8px 18px;
  border-radius: 40px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
}
.modal-btn-confirm {
  background: #0066cc;
  color: white;
  border: none;
  padding: 8px 18px;
  border-radius: 40px;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
}

@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }

@keyframes pop {
  0% { transform: scale(0.9); opacity: 0; }
  100% { transform: scale(1); opacity: 1; }
}

@media (max-width: 768px) {
  .editor-header { 
    padding: 0 16px;
    height: 56px; 
  }
  .breadcrumb { display: none; }
  .action-btns { 
    gap: 6px;
    width: 100%;
    justify-content: flex-end;
  }
  .graph-trigger-btn {
    padding: 0 10px;
    font-size: 12px;
    gap: 4px;
  }
  .publish-btn {
    padding: 6px 12px;
    font-size: 12px;
  }
  .save-btn {
    padding: 6px 14px;
    font-size: 12px;
  }
  .publish-status-tag { display: none; }
  .editor-scroll-body { padding: 40px 20px; }
  .title-input { font-size: 2.2rem; }
  .spirit-publish-modal { width: 90%; }
}
</style>