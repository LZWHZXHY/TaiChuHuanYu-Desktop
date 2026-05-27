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
            <span class="root">{{ isWikiMode ? 'Wiki 修订工作台' : '我的灵脉' }}</span>
            <span class="sep">/</span>
            <span class="current">{{ isWikiMode ? (wikiEditData?.title || '编辑 Wiki') : (activeNote?.title || '未命名碎片') }}</span>
          </div>
        </div>
        
        <div class="action-btns">
          <template v-if="!isWikiMode">
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
          </template>

          <button class="graph-trigger-btn" @click="handleImportWiki" style="background:#fff; border-color:#d2d2d7; color:#1d1d1f">
            {{ isWikiMode ? '更换 Wiki' : '🔗 导入 Wiki' }}
          </button>
          
          <button v-if="isWikiMode" class="publish-btn" @click="exitWikiMode">
            退出编辑
          </button>

          <button class="save-btn" @click="handleSave" :disabled="!isWikiMode && !activeNote">
            {{ isWikiMode ? (isWikiAuthor ? '更新 Wiki' : '提交修订') : (isMobile ? '同步' : '同步至灵脉') }}
          </button>
        </div>
      </header>

      <div class="editor-scroll-body">
        <div v-if="isContentLoading" class="content-loading-state">
          <div class="mini-spinner"></div>
          <p>正在感应灵脉碎片...</p>
        </div>

        <template v-else-if="displayNote">
          <input 
            :value="displayNote.title" 
            @input="e => !isWikiMode && updateNoteTitle(currentNoteId, (e.target as HTMLInputElement).value)"
            class="title-input" 
            placeholder="无标题灵感" 
            spellcheck="false"
            :readonly="isWikiMode"
          />
          
          <SpiritEditor ref="editorRef" :key="isWikiMode ? displayNote.id : currentNoteId" />
        </template>
      </div>
    </main>

    <template v-if="!isWikiMode">
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
    </template>

    <transition name="fade">
      <div v-if="showImportWikiModal" class="loading-overlay" @click.self="showImportWikiModal = false">
        <div class="spirit-modal-content pop-enter-active">
          <h3 class="modal-title">🌌 接入百科宇宙</h3>
          <p class="modal-desc">请输入你要修订的 Wiki 词条 ID</p>
          
          <input 
            v-model="importWikiId" 
            type="text" 
            class="spirit-id-input" 
            placeholder="例如: a1b2c3d4-..."
            @keyup.enter="confirmImportWiki"
            autofocus
          />
          
          <div class="modal-actions">
            <button class="cancel-btn" @click="showImportWikiModal = false">取消</button>
            <button class="save-btn" @click="confirmImportWiki" :disabled="!importWikiId.trim()">
              开始感应
            </button>
          </div>
        </div>
      </div>
    </transition>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick, watch } from 'vue';
import SidebarIndex from './components/SidebarIndex.vue';
import BacklinksPanel from './components/BacklinksPanel.vue';
import SpiritEditor from '../../components/SpiritText.vue'; 
import HistoryPanel from './components/HistoryPanel.vue';
import GraphView from './components/GraphView.vue';
import PublishModal from './components/PublishModal.vue';
import NoteSettingsPanel from './components/NoteSettingsPanel.vue';

import { useSpiritData } from '../../composables/useSpiritData';
import { lingmaiApi } from '../../api/lingmai';
import { wikiApi } from '@/api/Wiki'; 

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
  updateNoteContent,
  isWikiMode, 
  wikiEditData, 
  enterWikiMode, 
  exitWikiMode
} = useSpiritData();

const isMobile = ref(false);
const isSidebarOpen = ref(false);
const isHistoryOpen = ref(false); 
const isGraphViewOpen = ref(false);
const editorRef = ref();
const isContentLoading = ref(false);
const showPublishModal = ref(false);
const spaces = ref<any[]>([]); 

// 🌟 新增：Wiki 导入弹窗的状态
const showImportWikiModal = ref(false);
const importWikiId = ref('');

// 🌟 新增：存放待注入的 Wiki 数据，解决生命周期冲突
const pendingWikiContent = ref<any>(null);

const displayFilters = ref({
  wiki: true,
  char: true,
  art: true,
  note: true,
  thought: true,
  folder: true 
});

const isSettingsOpen = ref(false); 

// --- 🌟 Wiki 专属逻辑区 ---
const currentUserId = ref('current_user_id'); // TODO: 记得以后替换为真实 Auth ID

// 🌟 修复 Bug 1：使用类型断言 (as any) 绕过 authorId 的类型检查
const isWikiAuthor = computed(() => {
  const data = wikiEditData.value as any; 
  return data?.authorId === currentUserId.value;
});

const displayNote = computed(() => isWikiMode.value ? wikiEditData.value : activeNote.value);

// 🌟 改造：只负责打开弹窗
const handleImportWiki = () => {
  importWikiId.value = ''; 
  showImportWikiModal.value = true;
};

// 🌟 确认导入的请求逻辑（解耦数据处理与注入过程）
const confirmImportWiki = async () => {
  const wikiId = importWikiId.value.trim();
  if (!wikiId) return;
  
  showImportWikiModal.value = false;
  isContentLoading.value = true;
  pendingWikiContent.value = null; // 清空旧数据
  
  try {
    await enterWikiMode(wikiId);
    
    // 准备一个标准的 TipTap 文档外壳，默认带个空段落兜底
    let finalContent: any = { type: 'doc', content: [{ type: 'paragraph' }] };
    
    if (wikiEditData.value?.content) {
      // 1. 剥离 Vue 的 Proxy 响应式外衣
      const rawData = JSON.parse(JSON.stringify(wikiEditData.value.content));
      
      // 2. 如果是字符串 (可能存在多个 JSON 块用 \n 拼接的情况)
      if (typeof rawData === 'string') {
        const blocks = rawData.split('\n').filter((b: string) => b.trim());
        if (blocks.length > 0) {
          finalContent.content = blocks.map((blockStr: string) => {
            try {
              const parsed = JSON.parse(blockStr);
              return {
                type: parsed.type || 'paragraph', // 强制补上 paragraph
                ...(parsed.attrs ? { attrs: parsed.attrs } : {}),
                ...(parsed.content ? { content: parsed.content } : {})
              };
            } catch {
              return { type: 'paragraph', content: [{ type: 'text', text: blockStr }] };
            }
          });
        }
      } 
      // 3. 如果已经是对象了
      else if (typeof rawData === 'object') {
        if (rawData.type === 'doc') {
          finalContent = rawData;
        } else {
          finalContent.content = [{
            type: rawData.type || 'paragraph',
            ...(rawData.attrs ? { attrs: rawData.attrs } : {}),
            ...(rawData.content ? { content: rawData.content } : {})
          }];
        }
      }
    }

    // 🌟 核心：存入临时保险箱，不再直接注入
    pendingWikiContent.value = finalContent;

    // 4. 关闭 Loading，让编辑器在 DOM 中重生。接下来交给 watch 监听器。
    isContentLoading.value = false;

  } catch (e) {
    console.error("加载 Wiki 异常:", e);
    alert("Wiki 感应失败，请检查 ID");
    isContentLoading.value = false;
  }
};

// 🌟 核心修复器：监听编辑器初始化状态，安全注入数据
watch(
  () => editorRef.value?.isInitialized, // 监听你组件内部的初始化状态
  (isReady) => {
    // 只有当编辑器 Ready，且我们有待办数据时，才进行注入
    if (isReady && pendingWikiContent.value && editorRef.value?.editor) {
      try {
        editorRef.value.editor.commands.setContent(pendingWikiContent.value);
        // 注入完成后立即清空，防止被重复触发
        pendingWikiContent.value = null; 
      } catch (e) {
        console.error("向编辑器注入数据时发生内部错误:", e);
      }
    }
  },
  { immediate: true }
);

const handleSave = async () => {
  if (isWikiMode.value && wikiEditData.value) {
    // 1. 直接获取当前编辑器里最新的 JSON 内容
    const content = editorRef.value.getJSON(); 
    try {
      const data = wikiEditData.value as any;
      
      if(typeof (wikiApi as any).updateFromNote === 'function') {
         // 2. 🌟 核心修改：直接把内容转成字符串发过去，不再依赖虚无的 noteId
         await (wikiApi as any).updateFromNote({
          articleId: data.id,
          content: JSON.stringify(content), 
          summary: isWikiAuthor.value ? "原作者自主更新" : "提交协作修改",
          baseRevisionId: data.currentRevisionId || data.revisionId 
        });
        alert("提交成功！");
      } else {
         console.warn("请在 API 文件中实现 updateFromNote 方法！");
      }
      
      exitWikiMode();
    } catch (e: any) {
      if (e.response && e.response.status === 409) {
        alert("⚠️ 提交失败：词条已被更新，请备份您的修改并重新获取最新内容。");
      } else {
        console.error("提交失败:", e);
        alert("操作失败，请重试");
      }
    }
  } else {
    // 正常笔记保存逻辑
    if (!currentNoteId.value || !editorRef.value) return;
    const content = editorRef.value.getJSON();
    await updateNoteContent(currentNoteId.value, content);
  }
};
// --- Wiki 逻辑结束 ---

const handleUpdateNoteMeta = async (updates: any) => {
  if (!currentNoteId.value || !activeNote.value) return;
  try {
    await lingmaiApi.updateNoteMeta(currentNoteId.value, updates);
    Object.assign(activeNote.value, updates);
  } catch (e) {
    console.error('元数据同步失败:', e);
  }
};

const handleUpdateSpaceMeta = async (updates: any) => {
  const { id, ...data } = updates;
  if (!id) return;
  try {
    await lingmaiApi.updateSpaceMeta(id, data);
    const index = spaces.value.findIndex(s => s.id === id);
    if (index !== -1) {
      spaces.value[index] = { ...spaces.value[index], ...data };
    }
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

/* --- 🌟 新增：Wiki 导入弹窗专属样式 --- */
.spirit-modal-content {
  background: #ffffff;
  width: 90%;
  max-width: 400px;
  padding: 30px;
  border-radius: 16px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.1);
  text-align: center;
  position: relative;
  z-index: 10000;
}

.modal-title {
  font-size: 1.2rem;
  font-weight: 600;
  color: #1d1d1f;
  margin: 0 0 8px;
}

.modal-desc {
  font-size: 13px;
  color: #86868b;
  margin-bottom: 24px;
}

.spirit-id-input {
  width: 100%;
  padding: 12px 16px;
  border: 1px solid #d2d2d7;
  border-radius: 10px;
  font-size: 14px;
  margin-bottom: 24px;
  outline: none;
  transition: all 0.2s;
  box-sizing: border-box;
}

.spirit-id-input:focus {
  border-color: #0066cc;
  box-shadow: 0 0 0 3px rgba(0, 102, 204, 0.1);
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

.cancel-btn {
  background: #f5f5f7;
  border: none;
  padding: 8px 20px;
  border-radius: 40px;
  color: #1d1d1f;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s;
}

.cancel-btn:hover {
  background: #e5e5ea;
}
</style>