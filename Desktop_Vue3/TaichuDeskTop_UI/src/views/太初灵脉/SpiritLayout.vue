<template>
  <div class="spirit-link-app" :class="{ 'is-mobile': isMobile }">
    <!-- 全局加载遮罩（首次加载） -->
    <transition name="fade">
      <div v-if="isLoading && notes.length === 0" class="loading-overlay">
        <div class="spirit-loading-content">
          <div class="spirit-spinner"></div>
          <p>正在感应灵脉数据...</p>
        </div>
      </div>
    </transition>

    <!-- 移动端侧边栏遮罩 -->
    <transition name="fade">
      <div v-if="isMobile && isSidebarOpen" class="mobile-overlay" @click="isSidebarOpen = false"></div>
    </transition>

    <!-- 侧边栏（永远存在） -->
    <SidebarIndex
      :notes="notes"
      :active-id="currentNoteId"
      :filters="displayFilters"
      :class="['sidebar-layer', { open: isSidebarOpen || !isMobile }]"
      @select="handleSelectNote"
      @create="handleCreateNote"
    />

    <!-- 主工作区 -->
    <div class="editor-workspace-layout">
      <main class="spirit-main-editor">
        <!-- TopBar -->
        <TopBar
          :is-mobile="isMobile"
          :is-wiki-mode="isWikiMode"
          :active-note="activeNote"
          :wiki-edit-data="wikiEditData"
          @toggle-sidebar="isSidebarOpen = true"
          @open-graph="isGraphViewOpen = true"
          @open-settings="isSettingsOpen = true"
          @import-wiki="handleImportWiki"
          @exit-wiki="exitWikiMode"
        />

        <!-- 路由出口（内容区域） -->
        <router-view />
      </main>

      <!-- 右侧面板 -->
      <RightSidePanel
        v-if="!isMobile && activeNote && currentNoteId"
        :note-id="currentNoteId"
        v-model:extraData="activeNote.extraData"
        v-model:tags="activeNote.tags"
        @select="handleSelectNote"
        @change="triggerDebouncedSync"
      />
    </div>

    <!-- ========== 全局浮层 ========== -->
    <!-- 图谱 -->
    <transition name="fade">
      <GraphView v-if="isGraphViewOpen" @close="isGraphViewOpen = false" @select-note="handleSelectNote" />
    </transition>

    <!-- 笔记设置 -->
    <NoteSettingsPanel
      v-if="!isWikiMode"
      v-model="isSettingsOpen"
      :note="activeNote"
      :spaces="spaces"
      :current-space-id="currentSpaceId"
      :filters="displayFilters"
      :can-publish="canPublishDynamic"
      @update-note-meta="handleUpdateNoteMeta"
      @update-space-meta="handleUpdateSpaceMeta"
      @update-filters="handleUpdateFilters"
      @delete="handleDeleteNote"
      @open-history="isHistoryOpen = true"
      @publish-click="handlePublishClick"
      
    />

    <!-- 历史面板 -->
    <HistoryPanel v-model="isHistoryOpen" :note-id="currentNoteId" @rollback="onRollback" @manual-save="handleManualSave" />

    <!-- 发布弹窗 -->
    <PublishModal
      v-model="showPublishModal"
      :note-id="currentNoteId"
      :note-type="activeNote?.type || 'note'"
      :space-name="activeSpaceName"
      @success="onPublishSuccess"
    />

    <!-- Wiki 导入弹窗 -->
    <WikiImportModal v-model="showImportWikiModal" @confirm="confirmImportWiki" />

    <!-- 快捷编辑器抽屉 -->
    <QuickEditorDrawer
      v-model="isQuickEditorOpen"
      :note-id="quickEditorNoteId"
      :note-meta="quickEditorNoteMeta"
      :is-loading="isQuickEditorLoading"
    />

    <!-- Toast -->
    <SpiritToast ref="toastRef" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import SidebarIndex from './components/SidebarIndex.vue';
import RightSidePanel from './components/RightSidePanel.vue';
import HistoryPanel from './components/HistoryPanel.vue';
import GraphView from './components/GraphView.vue';
import PublishModal from './components/PublishModal.vue';
import NoteSettingsPanel from './components/NoteSettingsPanel.vue';
import TopBar from './components/TopBar.vue';
import QuickEditorDrawer from './components/QuickEditorDrawer.vue';
import WikiImportModal from './components/WikiImportModal.vue';
import SpiritToast from '@/components/SpiritToast.vue';
import { useSpiritData } from '../../composables/useSpiritData';
import { lingmaiApi } from '../../api/lingmai';
import { wikiApi } from '@/api/Wiki';
import { checkHasImage, getTextLength } from '@/utils/editorHelpers';

type NoteType = 'note' | 'post' | 'wiki' | 'char' | 'art' | 'folder' | 'canvas' | 'map' | 'excel' | 'blog' | 'doc' | 'schedule';

// ---------- 路由 ----------
const route = useRoute();
const router = useRouter();

// ---------- 数据 ----------
const {
  notes,
  currentNoteId: storeCurrentNoteId, // 不再直接使用，我们用路由的
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
  exitWikiMode,
} = useSpiritData();

// ---------- 本地状态 ----------
const isMobile = ref(false);
const isSidebarOpen = ref(false);
const isGraphViewOpen = ref(false);
const isSettingsOpen = ref(false);
const isHistoryOpen = ref(false);
const showPublishModal = ref(false);
const showImportWikiModal = ref(false);
const importWikiId = ref('');
const pendingWikiContent = ref<any>(null);
const toastRef = ref(); // 明确类型
const displayFilters = ref<Record<string, boolean>>({
  note: true,
  post: true,
  blog: true,
  wiki: true,
  char: true,
  art: true,
  canvas: true,
  map: true,
  excel: true,
  thought: true,
  folder: true,
  doc: true,
  schedule: true,
});
const spaces = ref<any[]>([]);

// 快捷编辑器
const isQuickEditorOpen = ref(false);
const quickEditorNoteId = ref('');
const quickEditorNoteMeta = ref<any>({});
const isQuickEditorLoading = ref(false);



const showToast = (message: string, duration?: number) => {
  if (toastRef.value) {
    toastRef.value.show(message, duration);
  } else {
    console.warn('Toast 组件尚未挂载，消息:', message);
  }
};
provide('showToast', showToast);




// 同步防抖
let syncDebounceTimer: any = null;

// ---------- 计算属性 ----------
// 当前笔记 ID 直接从路由获取
const currentNoteId = computed(() => route.params.id as string || '');

// 空间名称
const activeSpaceName = computed(() => spaces.value.find(s => s.id === currentSpaceId.value)?.name || '未知位面');

// 发布能力
const hasArtImage = computed(() => {
  if (!activeNote.value) return false;
  // 从 activeNote 的 blocks 中判断是否有图片
  const blocks = activeNote.value.blocks || [];
  return blocks.some((b: any) => b.type === 'image' || b.type === 'artwork');
});
const currentTextLength = computed(() => {
  if (!activeNote.value) return 0;
  const content = activeNote.value.content;
  if (content && content.content) {
    return content.content.reduce((acc: number, node: any) => acc + (node.text?.length || 0), 0);
  }
  return 0;
});
const canPublishDynamic = computed(() => {
  if (!activeNote.value) return false;
  if (['note', 'folder', 'schedule', 'char', 'canvas', 'map'].includes(activeNote.value.type)) {
    return false;
  }
  switch (activeNote.value.type) {
    case 'art':
      return hasArtImage.value;
    case 'post':
      return currentTextLength.value <= 500;
    default:
      return true;
  }
});

// ---------- 方法 ----------
// 选择笔记（路由跳转）
const handleSelectNote = (id: string) => {
  if (isMobile.value) isSidebarOpen.value = false;
  if (id) {
    router.push({ name: 'SpiritNote', params: { id } });
  }
};

// 创建笔记
const handleCreateNote = async (type: string = 'note', folderId: string | null = null) => {
 const newNote = await createNewNote({ type: type as NoteType, folderId });
  if (newNote) {
    if (isMobile.value && type !== 'folder') isSidebarOpen.value = false;
    router.replace({ name: 'SpiritNote', params: { id: newNote.id } });
  }
};

// 打开快捷编辑器（由子组件触发）
const handleOpenQuickEditor = async (targetId: string) => {
  isQuickEditorLoading.value = true;
  try {
    const targetNote: any = await lingmaiApi.getNote(targetId);
    if (['canvas', 'folder', 'map'].includes(targetNote.type)) {
      isQuickEditorOpen.value = false;
      handleSelectNote(targetId);
      return;
    }
    quickEditorNoteId.value = targetId;
    quickEditorNoteMeta.value = targetNote || {};
    isQuickEditorOpen.value = true;
  } catch (e) {
    console.error('抽取数据失败', e);
    toastRef.value?.show('抽取本体失败');
  } finally {
    isQuickEditorLoading.value = false;
  }
};

// 提供 openQuickEditor 给子组件（通过 provide）
import { provide } from 'vue';
provide('openQuickEditor', handleOpenQuickEditor);

// 其他方法（复用原有逻辑）
const handleUpdateNoteMeta = async (updates: any) => {
  if (!currentNoteId.value || !activeNote.value) return;
  try {
    await lingmaiApi.updateNoteMeta(currentNoteId.value, updates);
    Object.assign(activeNote.value, updates);
  } catch (e) {}
};
// ---------- 新增：本地数据管理 ----------
const currentDisplayNote = ref<any>(null);
const currentBlocks = ref<any[]>([]);
const isContentLoading = ref(false);

// 加载笔记的函数
const loadNote = async (id: string) => {
  if (!id) {
    currentDisplayNote.value = null;
    currentBlocks.value = [];
    isContentLoading.value = false;
    return;
  }

  isContentLoading.value = true;
  try {
    // 使用全局的 selectNote 拉取数据（它内部会更新 activeNote，但我们不管）
    const note = await selectNote(id, true);
    if (note) {
      // 将数据复制到我们自己的 ref 中
      currentDisplayNote.value = { ...note };
      currentBlocks.value = note.blocks || [];
      // 确保 content 存在
      if (!currentDisplayNote.value.content) {
        currentDisplayNote.value.content = { type: 'doc', content: [{ type: 'paragraph' }] };
      }
    }
  } catch (e) {
    console.error('加载笔记失败', e);
  } finally {
    isContentLoading.value = false;
  }
};
const handleUpdateSpaceMeta = async (updates: any) => {
  const { id, ...data } = updates;
  if (!id) return;
  try {
    await lingmaiApi.updateSpaceMeta(id, data);
    const index = spaces.value.findIndex(s => s.id === id);
    if (index !== -1) spaces.value[index] = { ...spaces.value[index], ...data };
  } catch (e) {}
};

const handleUpdateFilters = (val: Record<string, boolean>) => {
  displayFilters.value = val;
};

const handleDeleteNote = async (id: string) => {
  if (confirm('此操作不可逆，是否确定？')) {
    await lingmaiApi.deleteNote(id);
    await fetchAllNotes();
    // 如果删除的是当前笔记，跳转到首页或图谱
    if (currentNoteId.value === id) {
      router.push({ name: 'SpiritNote', params: { id: undefined } });
    }
    isSettingsOpen.value = false;
  }
};

const handlePublishClick = () => {
  if (!activeNote.value) return;
  if (activeNote.value.isPublic) {
    // 取消发布
    lingmaiApi.unpublishNote(currentNoteId.value).then(() => {
      if (activeNote.value) activeNote.value.isPublic = false;
    });
  } else {
    showPublishModal.value = true;
  }
};

const onPublishSuccess = (newType: string) => {
  if (activeNote.value) {
    activeNote.value.isPublic = true;
    activeNote.value.type = newType as any;
  }
};

const handleImportWiki = () => {
  importWikiId.value = '';
  showImportWikiModal.value = true;
};

const confirmImportWiki = async () => {
  const wikiId = importWikiId.value.trim();
  if (!wikiId) return;
  showImportWikiModal.value = false;
  try {
    await enterWikiMode(wikiId);
    // 注意：enterWikiMode 会将 isWikiMode 置为 true，但路由模式下我们可能想跳转到 wiki 路由
    // 这里我们暂时保留原有逻辑，实际可以跳转到专门 wiki 编辑路由
    toastRef.value?.show('Wiki 加载成功');
  } catch (e) {
    console.error(e);
    alert('Wiki 感应失败，请检查 ID');
  }
};

const onRollback = async (revision: any) => {
  try {
    await lingmaiApi.rollbackTo(currentNoteId.value, revision.id);
    // 刷新当前笔记
    const freshNote = await selectNote(currentNoteId.value, true);
    if (freshNote) {
      // 更新 activeNote 由 selectNote 内部处理，我们只需触发视图更新
      // 这里可以强制刷新组件，但更好的方式是在 NoteEditorView 中 watch activeNote
    }
    isHistoryOpen.value = false;
  } catch (e) {}
};

const handleManualSave = async () => {
  // 手动保存由 NoteEditorView 处理，这里留空或触发全局事件
  toastRef.value?.show('手动保存功能已移交给编辑器');
};

// 同步防抖（右侧面板修改 extraData/tags 时触发）
const triggerDebouncedSync = () => {
  if (syncDebounceTimer) clearTimeout(syncDebounceTimer);
  syncDebounceTimer = setTimeout(() => {
    // 直接调用 updateNoteContent 更新当前笔记的 extraData 和 tags
    if (currentNoteId.value && activeNote.value) {
      const payload = {
        noteId: currentNoteId.value,
        title: activeNote.value.title || '',
        extraData: activeNote.value.extraData || '[]',
        tags: activeNote.value.tags || [],
        blocks: activeNote.value.blocks || [],
      };
      lingmaiApi.updateNoteContent(currentNoteId.value, payload).catch(console.error);
    }
  }, 2000);
};

// 初始化空间
const initSpaces = async () => {
  try {
    spaces.value = await lingmaiApi.getSpaces() as any;
  } catch (e) {}
};

// 移动端检测
const checkScreen = () => {
  isMobile.value = window.innerWidth <= 1024;
};
watch(
  () => route.params.id,
  async (newId) => {
    await loadNote(newId as string);
  },
  { immediate: true }
);

// 提供数据给子组件（包括 loading 状态）
provide('currentDisplayNote', currentDisplayNote);
provide('currentBlocks', currentBlocks);
provide('isContentLoading', isContentLoading);
provide('reloadNote', loadNote); // 可选，供子组件手动触发
// 生命周期
onMounted(async () => {
  checkScreen();
  window.addEventListener('resize', checkScreen);
  await initSpaces();
  await fetchAllNotes();
  // 如果 URL 中有 id，会自动触发 NoteEditorView 的 watch 加载
});

onUnmounted(() => {
  window.removeEventListener('resize', checkScreen);
  if (syncDebounceTimer) clearTimeout(syncDebounceTimer);
});

// 注意：我们没有提供 handleSave，由 NoteEditorView 自行处理自动保存
</script>

<style scoped>
/* ========================================== */
/* 1. 全局基础骨架 (Layout Skeleton)          */
/* ========================================== */
.spirit-link-app { 
  display: flex; 
  width: 100%; 
  height: 94vh; 
  background: #ffffff; 
  overflow: hidden; 
  position: relative; 
}
.editor-workspace-layout { 
  display: flex; 
  flex: 1; 
  width: 100%; 
  height: 100%; 
  overflow: hidden; 
  position: relative; 
}
.spirit-main-editor { 
  flex: 1; 
  display: flex; 
  flex-direction: column; 
  min-width: 0; 
  background: #fafafa; 
}

/* ========================================== */
/* 2. 全局加载与图谱遮罩 (Global Overlays)     */
/* ========================================== */
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

/* ========================================== */
/* 3. 全局通用动画 (Global Animations)         */
/* ========================================== */
@keyframes spin { 
  from { transform: rotate(0deg); } 
  to { transform: rotate(360deg); } 
}
.fade-enter-active, .fade-leave-active { transition: opacity 0.3s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

/* ========================================== */
/* 4. 移动端宏观布局 (Mobile Layout)           */
/* ========================================== */
@media (max-width: 1024px) { 
  .editor-workspace-layout { flex-direction: column; } 
}
</style>