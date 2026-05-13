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

          <button 
            class="settings-trigger-btn" 
            @click="isSettingsOpen = true" 
            title="碎片设定"
            :disabled="!activeNote"
          >
            <span class="icon">⚙️</span>
          </button>

          <NoteSettingsPanel 
            v-model="isSettingsOpen"
            :note="activeNote"
            :spaces="spaces"
            :current-space-id="currentSpaceId" 
            :filters="displayFilters"
            @update-note-meta="handleUpdateNoteMeta"
            @update-space-meta="handleUpdateSpaceMeta"
            @update-filters="val => displayFilters = val"
            @delete="handleDeleteNote"
          />


          <transition name="pop">
            <span v-if="activeNote?.isPublic" class="publish-status-tag">
              <span class="dot"></span> 已发布至{{ getPublishTypeLabel(activeNote?.type) }}
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
        <div v-if="isContentLoading" class="content-loading-state">
          <div class="mini-spinner"></div>
          <p>正在感应灵脉碎片...</p>
        </div>

        <template v-else-if="activeNote">
          <input 
            :value="activeNote.title" 
            @input="e => updateNoteTitle(currentNoteId, (e.target as HTMLInputElement).value)"
            class="title-input" 
            placeholder="无标题灵感" 
            spellcheck="false"
          />
          
          <SpiritEditor ref="editorRef" :key="currentNoteId" />
        </template>
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

    <PublishModal 
      v-model="showPublishModal"
      :note-id="currentNoteId"
      :space-name="activeSpaceName"
      :initial-type="activeNote?.type"
      @success="onPublishSuccess"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue';
import SidebarIndex from './components/SidebarIndex.vue';
import BacklinksPanel from './components/BacklinksPanel.vue';
import SpiritEditor from '../../components/SpiritText.vue'; 
import HistoryPanel from './components/HistoryPanel.vue';
import GraphView from './components/GraphView.vue';
import PublishModal from './components/PublishModal.vue';
import { useSpiritData } from '../../composables/useSpiritData';
import { lingmaiApi } from '../../api/lingmai';
import NoteSettingsPanel from './components/NoteSettingsPanel.vue';



// 🌟 严格笔记类型定义
type NoteType = 'note' | 'thought' | 'wiki' | 'char' | 'folder';







const { 
  notes, 
  currentNoteId, 
  activeNote, 
  isLoading,
  currentSpaceId,
  fetchAllNotes, 
  selectNote, 
  createNewNote, 
  updateNoteTitle, 
  updateNoteContent 
} = useSpiritData();

const isMobile = ref(false);
const isSidebarOpen = ref(false);
const isHistoryOpen = ref(false); 
const isGraphViewOpen = ref(false);
const editorRef = ref();
const isContentLoading = ref(false);
const showPublishModal = ref(false);
const spaces = ref<any[]>([]); 

// 🌟 扩展过滤器，加入 folder
const displayFilters = ref({
  wiki: true,
  char: true,
  art: true,
  note: true,
  thought: true,
  folder: true // 👈 补上这一行，消除 'folder' does not exist 报错
});



const isSettingsOpen = ref(false); // 🌟 状态

const filteredNotes = computed(() => {
  return notes.value.filter(n => {
    // 1. 显式断言类型，告诉 TS：n.type 肯定属于过滤器的 key 之一
    const typeKey = n.type as keyof typeof displayFilters.value;
    
    // 2. 获取该维度的显示状态
    // 如果该类型在过滤器中被关掉（false），则隐藏
    const isTypeAllowed = displayFilters.value[typeKey] !== false;
    
    // 3. 检查碎片自己的显示勾选（上一节实现的设置）
    const isSidebarAllowed = n.showInSidebar !== false;

    // 🌟 特殊保护逻辑：
    // 如果是文件夹，通常我们让它始终显示（或者跟随 folder 开关）
    if (n.type === 'folder') return displayFilters.value.folder;

    // 如果是当前正在编辑的碎片，强制显示，防止感应中断
    if (n.id === currentNoteId.value) return true;

    return isTypeAllowed && isSidebarAllowed;
  });
});

// 🌟 元数据更新逻辑
const handleUpdateNoteMeta = async (updates: any) => {
  // 1. 增加非空校验，确保 currentNoteId 和 activeNote 存在
  if (!currentNoteId.value || !activeNote.value) return;

  try {
    // 2. 调用刚刚在 api 里的新增方法
    await lingmaiApi.updateNoteMeta(currentNoteId.value, updates);
    
    // 3. 安全更新本地状态
    // 通过上面的 if 判断，TS 现在知道 activeNote.value 不为 null 了
    Object.assign(activeNote.value, updates);
    
    console.log('灵脉感应同步成功');
  } catch (e) {
    console.error('元数据同步失败:', e);
  }
};

// 🌟 处理位面层级的元数据更新（改名、公开状态等）
const handleUpdateSpaceMeta = async (updates: any) => {
  const { id, ...data } = updates;
  if (!id) return;

  try {
    // 1. 调用 API 同步至服务器
    // 注意：确保你的 lingmaiApi 中已经定义了 updateSpaceMeta
    await lingmaiApi.updateSpaceMeta(id, data);
    
    // 2. 实时刷新本地位面列表，让面包屑和设置面板立即感应变化
    const index = spaces.value.findIndex(s => s.id === id);
    if (index !== -1) {
      spaces.value[index] = { ...spaces.value[index], ...data };
    }
    
    console.log('位面维度信息已同步');
  } catch (e) {
    console.error('位面感应失败:', e);
  }
};




const handleDeleteNote = async (id: string) => {
  if (confirm('此操作不可逆，是否确定？')) {
    await lingmaiApi.deleteNote(id);
    await fetchAllNotes();
    currentNoteId.value = '';
    isSettingsOpen.value = false;
  }
};








const activeSpaceName = computed(() => {
  const space = spaces.value.find(s => s.id === currentSpaceId.value);
  return space?.name || '未知位面';
});

const checkScreen = () => {
  isMobile.value = window.innerWidth <= 1024;
};

const handleSelectNote = async (id: string) => {
  if (isMobile.value) isSidebarOpen.value = false;
  isContentLoading.value = true;
  try {
    await selectNote(id);
  } finally {
    setTimeout(() => { isContentLoading.value = false; }, 200);
  }
};

const handleCreateNote = async (type: 'note' | 'folder' = 'note', folderId: string | null = null) => {
  const newNote = await createNewNote({ type: type, folderId: folderId });
  if (newNote && isMobile.value && type === 'note') {
    isSidebarOpen.value = false;
  }
};

const onPublishSuccess = (newType: string) => {
  if (activeNote.value) {
    activeNote.value.isPublic = true;
    activeNote.value.type = newType as NoteType;
  }
};

const handleUnpublish = async () => {
  if (!currentNoteId.value) return;
  try {
    await lingmaiApi.unpublishNote(currentNoteId.value);
    if (activeNote.value) activeNote.value.isPublic = false;
  } catch (err) {
    console.error('取消发布失败:', err);
  }
};

const handlePublishClick = () => {
  if (!activeNote.value) return;
  if (activeNote.value.isPublic) {
    handleUnpublish(); 
  } else {
    showPublishModal.value = true;
  }
};

const getPublishTypeLabel = (type?: string) => {
  switch(type) {
    case 'thought': return '简语广场';
    case 'wiki': return '百科宇宙';
    case 'char': return '人物志';
    default: return '随笔博客';
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
      }
      setTimeout(() => { if (editorRef.value) editorRef.value.isInitialized = true; }, 500);
    }
    isHistoryOpen.value = false;
  } catch (e) {
    console.error('回溯逻辑执行失败:', e);
  }
};

const handleManualSave = async () => {
  if (!editorRef.value || !currentNoteId.value) return;
  const content = editorRef.value.getJSON();
  try {
    await lingmaiApi.createSnapshot(currentNoteId.value, content, "用户手动固化");
  } catch (e) {
    console.error('固化失败:', e);
  }
};

const initSpaces = async () => {
  try {
    const res: any = await lingmaiApi.getSpaces();
    spaces.value = res;
  } catch (e) {
    console.error('空间数据加载失败', e);
  }
};

onMounted(async () => {
  checkScreen();
  window.addEventListener('resize', checkScreen);
  await initSpaces();
  await fetchAllNotes();
  if (currentNoteId.value) await handleSelectNote(currentNoteId.value);
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

/* 侧边栏层 */
.sidebar-layer {
  width: 280px;
  flex-shrink: 0;
  transition: transform 0.4s cubic-bezier(0.16, 1, 0.3, 1);
  z-index: 2000;
  border-right: 1px solid #f2f2f2;
}

/* 加载动画 */
.loading-overlay {
  position: fixed; inset: 0; background: rgba(255, 255, 255, 0.9);
  backdrop-filter: blur(10px); z-index: 9999;
  display: flex; align-items: center; justify-content: center;
}
.spirit-loading-content { text-align: center; color: #86868b; }
.spirit-spinner {
  width: 32px; height: 32px; border: 2px solid #f3f3f3;
  border-top: 2px solid #0066cc; border-radius: 50%;
  margin: 0 auto 16px; animation: spin 1s linear infinite;
}

/* 主编辑区 */
.spirit-main-editor {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
}

/* 头部 Header */
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
  font-size: 13px; color: #86868b; display: flex; gap: 8px; 
}
.breadcrumb .current { color: #1d1d1f; font-weight: 500; }

.action-btns { 
  display: flex; align-items: center; gap: 12px; flex-shrink: 0;
}

/* 🌟 图谱按钮样式 */
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
}
.graph-trigger-btn:hover { background: rgba(0, 102, 204, 0.1); }

/* 🌟 历史记录按钮样式 */
.history-trigger-btn {
  background: none; border: 1px solid #d2d2d7;
  width: 32px; height: 32px; border-radius: 50%;
  cursor: pointer; display: flex; align-items: center; justify-content: center;
  transition: all 0.2s; color: #86868b;
}
.history-trigger-btn:hover:not(:disabled) {
  background: #f5f5f7; border-color: #1d1d1f; color: #1d1d1f;
}
.history-trigger-btn:disabled { opacity: 0.4; cursor: not-allowed; }

/* 发布状态标签 */
.publish-status-tag {
  display: flex; align-items: center; gap: 6px;
  font-size: 11px; color: #34c759;
  background: rgba(52, 199, 89, 0.08);
  padding: 4px 10px; border-radius: 6px;
  white-space: nowrap;
}
.publish-status-tag .dot {
  width: 6px; height: 6px; background: #34c759; border-radius: 50%;
}

/* 发布按钮 */
.publish-btn {
  background: none; border: 1px solid #d2d2d7;
  color: #1d1d1f; padding: 7px 16px; border-radius: 40px;
  font-size: 13px; font-weight: 500; cursor: pointer;
  transition: all 0.3s ease; white-space: nowrap;
}
.publish-btn.is-active { 
  border-color: #0066cc; color: #0066cc; background: rgba(0, 102, 204, 0.05); 
}

/* 同步按钮 */
.save-btn {
  background: #1d1d1f; color: #fff; border: none;
  padding: 8px 20px; border-radius: 40px;
  font-size: 13px; font-weight: 600; cursor: pointer;
  white-space: nowrap;
}
.save-btn:disabled { background: #d2d2d7; cursor: not-allowed; }

/* 移动端菜单按钮 */
.menu-toggle-btn {
  background: none; border: none; font-size: 20px; cursor: pointer; color: #1d1d1f;
}

/* 编辑滚动区 */
.editor-scroll-body {
  flex: 1; overflow-y: auto; padding: 80px 10% 100px;
}

.title-input {
  width: 100%; border: none; font-size: 3rem; font-weight: 800;
  margin-bottom: 40px; outline: none; background: transparent;
  letter-spacing: -0.04em; color: #1d1d1f;
}

/* 内容加载状态 */
.content-loading-state {
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  height: 40vh; color: #86868b; gap: 12px; font-size: 13px;
}
.mini-spinner {
  width: 24px; height: 24px; border: 2px solid #f2f2f7;
  border-top-color: #0066cc; border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

/* 移动端覆盖 */
.mobile-overlay {
  position: absolute; inset: 0; background: rgba(255,255,255,0.7);
  backdrop-filter: blur(4px); z-index: 1999;
}

@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }

@media (max-width: 1024px) {
  .sidebar-layer {
    position: absolute; top: 0; left: 0; bottom: 0;
    transform: translateX(-100%); background: #ffffff;
    box-shadow: 20px 0 50px rgba(0,0,0,0.05);
  }
  .sidebar-layer.open { transform: translateX(0); }
  .editor-header { padding: 0 16px; height: 56px; }
  .breadcrumb { display: none; }
  .action-btns { gap: 8px; width: 100%; justify-content: flex-end; }
  .graph-trigger-btn { padding: 0 10px; font-size: 12px; }
  .editor-scroll-body { padding: 40px 20px; }
  .title-input { font-size: 2.2rem; }
}

/* 基础动画 */
.fade-enter-active, .fade-leave-active { transition: opacity 0.3s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

.pop-enter-active { animation: pop 0.3s cubic-bezier(0.16, 1, 0.3, 1); }
@keyframes pop {
  0% { transform: scale(0.9); opacity: 0; }
  100% { transform: scale(1); opacity: 1; }
}


.settings-trigger-btn {
  background: none; border: 1px solid #d2d2d7;
  width: 32px; height: 32px; border-radius: 50%;
  cursor: pointer; display: flex; align-items: center; justify-content: center;
  transition: all 0.2s; color: #86868b;
}
.settings-trigger-btn:hover:not(:disabled) {
  background: #f5f5f7; color: #1d1d1f; border-color: #1d1d1f;
}
.settings-trigger-btn:disabled { opacity: 0.4; }
</style>