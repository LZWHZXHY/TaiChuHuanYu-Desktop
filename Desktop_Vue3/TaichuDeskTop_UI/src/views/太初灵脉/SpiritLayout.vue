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
        <TopBar
          :is-mobile="isMobile"
          :active-note="activeNote"
          @toggle-sidebar="isSidebarOpen = true"
          @open-graph="isGraphViewOpen = true"
          @open-settings="isSettingsOpen = true"
        />
        <router-view />
      </main>

      <!-- ✨ 保留的右侧面板 -->
      <RightSidePanel
        v-if="!isMobile && activeNote && currentNoteId"
        :note-id="currentNoteId"
        v-model:tags="activeNote.tags"
        @select="handleSelectNote"
        @change="triggerDebouncedSync"
      />
    </div>

    <!-- ========== 全局浮层 ========== -->
    <transition name="fade">
      <GraphView v-if="isGraphViewOpen" @close="isGraphViewOpen = false" @select-note="handleSelectNote" />
    </transition>

    <NoteSettingsPanel
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

    <HistoryPanel 
      v-model="isHistoryOpen" 
      :note-id="currentNoteId" 
      @rollback="onRollback" 
      @manual-save="handleManualSave" 
    />

    <PublishModal
      v-model="showPublishModal"
      :note-id="currentNoteId"
      :note-type="activeNote?.type || 'note'"
      :space-name="activeSpaceName"
      @success="onPublishSuccess"
    />

    <QuickEditorDrawer
      v-model="isQuickEditorOpen"
      :note-id="quickEditorNoteId"
      :note-meta="quickEditorNoteMeta"
      :is-loading="isQuickEditorLoading"
    />

    <SpiritToast ref="toastRef" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch, provide } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import SidebarIndex from './components/SidebarIndex.vue';
import RightSidePanel from './components/RightSidePanel.vue';
import HistoryPanel from './components/HistoryPanel.vue';
import GraphView from './components/GraphView.vue';
import PublishModal from './components/PublishModal.vue';
import NoteSettingsPanel from './components/NoteSettingsPanel.vue';
import TopBar from './components/TopBar.vue';
import QuickEditorDrawer from './components/QuickEditorDrawer.vue';
import SpiritToast from '@/components/SpiritToast.vue';
import { useSpiritData } from '../../composables/useSpiritData';
import { lingmaiApi } from '../../api/lingmai';
import { useAutoSave } from '@/composables/useAutoSave'; // 🌟 引入全局单例保存引擎

type NoteType = 'note' | 'blog' | 'post' | 'folder' | 'wiki' | 'canvas' | 'schedule';

const route = useRoute();
const router = useRouter();

const {
  notes, activeNote, isLoading, currentSpaceId, fetchAllNotes, selectNote, createNewNote, updateNoteTitle
} = useSpiritData();

const { syncToCloud } = useAutoSave(); // 🌟 激活引擎

const isMobile = ref(false);
const isSidebarOpen = ref(false);
const isGraphViewOpen = ref(false);
const isSettingsOpen = ref(false);
const isHistoryOpen = ref(false);
const showPublishModal = ref(false);
const toastRef = ref();

const displayFilters = ref<Record<string, boolean>>({
  note: true, post: true, blog: true, wiki: true, canvas: true, folder: true, schedule: true,
});
const spaces = ref<any[]>([]);

const isQuickEditorOpen = ref(false);
const quickEditorNoteId = ref('');
const quickEditorNoteMeta = ref<any>({});
const isQuickEditorLoading = ref(false);

const showToast = (message: string, duration?: number) => {
  if (toastRef.value) toastRef.value.show(message, duration);
};
provide('showToast', showToast);

const currentNoteId = computed(() => route.params.id as string || '');
const activeSpaceName = computed(() => spaces.value.find(s => s.id === currentSpaceId.value)?.name || '未知位面');

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
  if (['note', 'folder', 'schedule', 'canvas'].includes(activeNote.value.type)) return false;
  return activeNote.value.type === 'post' ? currentTextLength.value <= 500 : true;
});

const handleSelectNote = (id: string) => {
  if (isMobile.value) isSidebarOpen.value = false;
  if (id) router.push({ name: 'SpiritNote', params: { id } });
};

const handleCreateNote = async (type: string = 'note', folderId: string | null = null) => {
 const newNote = await createNewNote({ type: type as NoteType, folderId });
  if (newNote) {
    if (isMobile.value && type !== 'folder') isSidebarOpen.value = false;
    router.replace({ name: 'SpiritNote', params: { id: newNote.id } });
  }
};

const handleOpenQuickEditor = async (targetId: string) => {
  isQuickEditorLoading.value = true;
  try {
    const targetNote: any = await lingmaiApi.getNote(targetId);
    if (['canvas', 'folder', 'schedule'].includes(targetNote.type)) {
      isQuickEditorOpen.value = false;
      handleSelectNote(targetId);
      return;
    }
    quickEditorNoteId.value = targetId;
    quickEditorNoteMeta.value = targetNote || {};
    isQuickEditorOpen.value = true;
  } catch (e) {
    showToast('抽取本体失败');
  } finally {
    isQuickEditorLoading.value = false;
  }
};
provide('openQuickEditor', handleOpenQuickEditor);

const handleUpdateNoteMeta = async (updates: any) => {
  if (!currentNoteId.value || !activeNote.value) return;
  try {
    await lingmaiApi.updateNoteMeta(currentNoteId.value, updates);
    Object.assign(activeNote.value, updates);
  } catch (e) {}
};

const currentDisplayNote = ref<any>(null);
const currentBlocks = ref<any[]>([]);
const isContentLoading = ref(false);
const pinnedNoteIds = ref<string[]>([]);   // 🌟 常驻预览（多槽位）
const loadNote = async (id: string) => {
  if (!id) {
    currentDisplayNote.value = null;
    currentBlocks.value = [];
    isContentLoading.value = false;
    return;
  }
  isContentLoading.value = true;
  try {
    const note = await selectNote(id, true);
    if (note) {
      currentDisplayNote.value = { ...note };
      currentBlocks.value = note.blocks || [];
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

const handleUpdateFilters = (val: Record<string, boolean>) => displayFilters.value = val;

const handleDeleteNote = async (id: string) => {
  if (confirm('此操作不可逆，是否确定？')) {
    await lingmaiApi.deleteNote(id);
    await fetchAllNotes();
    if (currentNoteId.value === id) {
      router.push({ name: 'SpiritNote', params: { id: undefined } });
    }
    isSettingsOpen.value = false;
  }
};

const handlePublishClick = () => {
  if (!activeNote.value) return;
  if (activeNote.value.isPublic) {
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

const onRollback = async (revision: any) => {
  try {
    await lingmaiApi.rollbackTo(currentNoteId.value, revision.id);
    await selectNote(currentNoteId.value, true);
    isHistoryOpen.value = false;
  } catch (e) {}
};

const handleManualSave = async () => {
  showToast('手动保存功能已移交给编辑器');
};

// 🌟 统一触发单例保存
const triggerDebouncedSync = () => {
  if (currentNoteId.value && activeNote.value) {
    const payload = {
      noteId: currentNoteId.value,
      title: activeNote.value.title || '',
      extraData: activeNote.value.extraData || '[]',
      tags: activeNote.value.tags || [],
      blocks: activeNote.value.blocks || [],
    };
    syncToCloud(currentNoteId.value, payload, showToast); 
  }
};

const initSpaces = async () => {
  try { spaces.value = await lingmaiApi.getSpaces() as any; } catch (e) {}
};

const checkScreen = () => { isMobile.value = window.innerWidth <= 1024; };

watch(() => route.params.id, async (newId) => { await loadNote(newId as string); }, { immediate: true });

provide('currentDisplayNote', currentDisplayNote);
provide('currentBlocks', currentBlocks);
provide('isContentLoading', isContentLoading);
provide('reloadNote', loadNote);
provide('pinnedNoteIds', pinnedNoteIds);   // 🌟 常驻预览（多槽位）  // 🌟 常驻预览
let timer: ReturnType<typeof setTimeout> | null = null;

onMounted(async () => {
  (window as any).onFileChange = (type: string, path: string) => {
    if (timer) clearTimeout(timer);
    timer = setTimeout(async () => {
      try {
        if (type === 'CREATED' && path.toLowerCase().endsWith('.md')) {
           const fullFileName = path.split('\\').pop() || '';
           const fileName = fullFileName.replace(/\.md$/i, '') || '本地新文件';
           await lingmaiApi.createNote({ title: fileName, spaceId: currentSpaceId.value, type: 'note', folderId: null });
           showToast(`已捕获本地文件: ${fileName}`);
        }
        await fetchAllNotes();
      } catch (error) { showToast(`文件同步出现异常`); }
    }, 500); 
  };

  checkScreen();
  window.addEventListener('resize', checkScreen);
  try {
    await initSpaces();
    await fetchAllNotes();
  } catch (e) {}
});

onUnmounted(() => {
  if (timer) clearTimeout(timer);
  window.removeEventListener('resize', checkScreen);
});
</script>

<style scoped>
.spirit-link-app { display: flex; width: 100%; height: 94vh; background: #ffffff; overflow: hidden; position: relative; }
.editor-workspace-layout { display: flex; flex: 1; width: 100%; height: 100%; overflow: hidden; position: relative; }
.spirit-main-editor { flex: 1; display: flex; flex-direction: column; min-width: 0; background: #fafafa; }
.loading-overlay { position: fixed; inset: 0; background: rgba(255, 255, 255, 0.9); backdrop-filter: blur(10px); z-index: 9999; display: flex; align-items: center; justify-content: center; }
.spirit-loading-content { text-align: center; color: #86868b; }
.spirit-spinner { width: 32px; height: 32px; border: 2px solid #f3f3f3; border-top: 2px solid #0066cc; border-radius: 50%; margin: 0 auto 16px; animation: spin 1s linear infinite; }
@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
.fade-enter-active, .fade-leave-active { transition: opacity 0.3s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
@media (max-width: 1024px) { .editor-workspace-layout { flex-direction: column; } }
</style>