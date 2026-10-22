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
        <router-view v-slot="{ Component }">
          <component :is="Component" ref="noteEditorRef" />
        </router-view>
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
      v-if="isSettingsOpen"
      v-model="isSettingsOpen"
      :note="activeNote"
      :spaces="spaces"
      :current-space-id="currentSpaceId"
      :filters="displayFilters"
      :can-publish="canPublishDynamic"
      :publishing="publicationBusy"
      @update-note-meta="handleUpdateNoteMeta"
      @update-space-meta="handleUpdateSpaceMeta"
      @update-filters="handleUpdateFilters"
      @delete="handleDeleteNote"
      @open-history="isHistoryOpen = true"
      @publish-click="handlePublishClick"
      @unshare="handlePublishClick(true)"
      @refresh-publish="showPublishModal = true"
      @save="saveCurrentNote"
    />

    <HistoryPanel
      v-if="isHistoryOpen"
      ref="historyPanelRef"
      :busy="historyBusy || isContentLoading"
      v-model="isHistoryOpen" 
      :note-id="currentNoteId" 
      @rollback="handleHistoryAction" 
      @manual-save="handleHistoryAction()" 
    />

    <PublishModal
      v-if="showPublishModal"
      v-model="showPublishModal"
      :note-id="currentNoteId"
      :note-type="activeNote?.type || 'note'"
      :space-name="activeSpaceName"
      :before-publish="preparePublish"
      @success="onPublishSuccess"
    />

    <QuickEditorDrawer
      v-if="isQuickEditorOpen"
      v-model="isQuickEditorOpen"
      :note-id="quickEditorNoteId"
      :note-meta="quickEditorNoteMeta"
      :is-loading="isQuickEditorLoading"
    />

    <SpiritToast ref="toastRef" />
  </div>
</template>

<script setup lang="ts">
import { defineAsyncComponent, ref, computed, onMounted, onUnmounted, watch, provide, nextTick } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import SidebarIndex from './components/SidebarIndex.vue';
import RightSidePanel from './components/RightSidePanel.vue';
const HistoryPanel = defineAsyncComponent(() => import('./components/HistoryPanel.vue'));
const GraphView = defineAsyncComponent(() => import('./components/GraphView.vue'));
const PublishModal = defineAsyncComponent(() => import('./components/PublishModal.vue'));
const NoteSettingsPanel = defineAsyncComponent(() => import('./components/NoteSettingsPanel.vue'));
import TopBar from './components/TopBar.vue';
const QuickEditorDrawer = defineAsyncComponent(() => import('./components/QuickEditorDrawer.vue'));
import SpiritToast from '@/components/SpiritToast.vue';
import { useSpiritData } from '../../composables/useSpiritData';
import { lingmaiApi } from '../../api/lingmai';
import { createNoteSyncPayload } from '@/utils/editorHelpers';
import { useAutoSave } from '@/composables/useAutoSave'; // 🌟 引入全局单例保存引擎

type NoteType = 'note' | 'blog' | 'post' | 'folder' | 'wiki' | 'canvas' | 'schedule';

const route = useRoute();
const router = useRouter();

const {
  notes, activeNote, isLoading, currentSpaceId, fetchAllNotes, selectNote, createNewNote
} = useSpiritData();

const { syncToCloud, saveNow, pauseNote, resumeNote } = useAutoSave(); // 🌟 激活引擎

const isMobile = ref(false);
const isSidebarOpen = ref(false);
const isGraphViewOpen = ref(false);
const isSettingsOpen = ref(false);
const isHistoryOpen = ref(false);
const showPublishModal = ref(false);
const publicationBusy = ref(false);
const toastRef = ref();
const noteEditorRef = ref<any>();
const historyPanelRef = ref<any>();
const historyBusy = ref(false);

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
  const note = activeNote.value, noteId = currentNoteId.value;
  try {
    await lingmaiApi.updateNoteMeta(noteId, updates);
    Object.assign(note, updates);
  } catch (e: any) { showToast(e.friendlyMessage || '设置保存失败，请重试'); }
};

const currentDisplayNote = ref<any>(null);
const currentBlocks = ref<any[]>([]);
const isContentLoading = ref(false);
const pinnedNoteIds = ref<string[]>([]);   // 🌟 常驻预览（多槽位）
let loadVersion = 0;
const loadNote = async (id: string) => {
  const version = ++loadVersion;
  if (!id) {
    currentDisplayNote.value = null;
    currentBlocks.value = [];
    isContentLoading.value = false;
    return;
  }
  isContentLoading.value = true;
  try {
    const note = await selectNote(id, true);
    if (version !== loadVersion) return;
    if (!note) throw new Error('笔记不存在或加载失败');
    currentDisplayNote.value = { ...note };
    currentBlocks.value = note.blocks || [];
    if (!currentDisplayNote.value.content) {
      currentDisplayNote.value.content = { type: 'doc', content: [{ type: 'paragraph' }] };
    }
  } catch (e) {
    console.error('加载笔记失败', e);
    throw e;
  } finally {
    if (version === loadVersion) isContentLoading.value = false;
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

async function preparePublish(noteId: string) {
  if (noteId !== currentNoteId.value || isContentLoading.value || historyBusy.value || currentDisplayNote.value?.id !== noteId)
    throw new Error('当前内容尚未准备好，请稍后重试');
  const snapshot = noteEditorRef.value?.getSnapshot();
  if (!snapshot) throw new Error('编辑器尚未就绪，请稍后重试');
  await saveNow(noteId, createNoteSyncPayload(noteId, activeNote.value, { blocks: snapshot.blocks }));
}
async function saveCurrentNote() {
  try { await preparePublish(currentNoteId.value); showToast('当前内容已保存'); }
  catch (e: any) { showToast(e.friendlyMessage || e.message || '保存失败，请重试'); }
}
async function handlePublishClick(unshare = false) {
  if (!activeNote.value || publicationBusy.value) return;
  if (!unshare && (activeNote.value.type === 'doc' || !activeNote.value.isPublic)) { showPublishModal.value = true; return; }
  const noteId = currentNoteId.value;
  publicationBusy.value = true;
  try {
    await lingmaiApi.unpublishNote(noteId);
    const note = notes.value.find(n => n.id === noteId);
    if (note && note.type !== 'doc') note.isPublic = false;
    showToast(note?.type === 'doc' ? '已取消所有项目分享' : '已取消发布');
  } catch (e: any) { showToast(e.friendlyMessage || '取消发布失败，请重试'); }
  finally { publicationBusy.value = false; }
}
const onPublishSuccess = (result: { noteId: string; message?: string }) => {
  const note = notes.value.find(n => n.id === result.noteId);
  if (note && note.type !== 'doc') note.isPublic = true;
  showToast(result.message || '发布成功');
};

// 创建和恢复共用互斥状态；操作开始时固定笔记 ID，避免切换笔记后写错目标。
const handleHistoryAction = async (revision?: { id: string }, sourceNoteId = currentNoteId.value) => {
  const noteId = sourceNoteId;
  if (!noteId || noteId !== currentNoteId.value || historyBusy.value || isContentLoading.value || currentDisplayNote.value?.id !== noteId) return;
  let restored = false;
  historyBusy.value = true;
  try {
    const snapshot = noteEditorRef.value?.getSnapshot();
    if (!snapshot) throw new Error('编辑器尚未就绪，请稍后重试');
    if (revision) {
      isContentLoading.value = true;
      if (quickEditorNoteId.value === noteId) isQuickEditorOpen.value = false;
      await nextTick();
      await pauseNote(noteId);
      await lingmaiApi.rollbackTo(noteId, revision.id);
      restored = true;
      if (currentNoteId.value === noteId) {
        await loadNote(noteId);
        isHistoryOpen.value = false;
      }
    } else {
      await saveNow(noteId, createNoteSyncPayload(noteId, activeNote.value, { blocks: snapshot.blocks }));
      await lingmaiApi.createSnapshot(noteId, snapshot);
      if (currentNoteId.value === noteId) await historyPanelRef.value?.refresh();
    }
    if (currentNoteId.value === noteId) showToast(revision ? '已恢复所选版本' : '当前版本已保存');
  } catch (e: any) {
    showToast(restored ? '版本已恢复，但重新加载失败，请刷新页面' : (e.friendlyMessage || e.message || '版本操作失败，请重试'));
  } finally {
    if (revision) resumeNote(noteId, restored);
    if (currentNoteId.value === noteId) isContentLoading.value = false;
    historyBusy.value = false;
  }
};

// 🌟 统一触发单例保存
const triggerDebouncedSync = () => {
  if (currentNoteId.value && activeNote.value) {
    const payload = createNoteSyncPayload(currentNoteId.value, activeNote.value, { blocks: activeNote.value.blocks || [] });
    syncToCloud(currentNoteId.value, payload, showToast); 
  }
};

const initSpaces = async () => {
  try { spaces.value = await lingmaiApi.getSpaces() as any; } catch (e) {}
};

const checkScreen = () => { isMobile.value = window.innerWidth <= 1024; };

watch(() => route.params.id, async (newId) => { showPublishModal.value = false; await loadNote(newId as string).catch(() => {}); }, { immediate: true });

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
.spirit-link-app { display: flex; width: 100%; height: 94vh; background: var(--paper-card); overflow: hidden; position: relative; }
.editor-workspace-layout { display: flex; flex: 1; width: 100%; height: 100%; overflow: hidden; position: relative; }
.spirit-main-editor { flex: 1; display: flex; flex-direction: column; min-width: 0; background: var(--bg-main); }
.loading-overlay { position: absolute; inset: 0; background: var(--paper-card); backdrop-filter: blur(10px); z-index: 9999; display: flex; align-items: center; justify-content: center; }
.spirit-loading-content { text-align: center; color: var(--text-secondary); }
.spirit-spinner { width: 32px; height: 32px; border: 2px solid var(--border-line); border-top: 2px solid var(--accent-color); border-radius: 50%; margin: 0 auto 16px; animation: spin 1s linear infinite; }
@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
.fade-enter-active, .fade-leave-active { transition: opacity 0.3s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
@media (max-width: 1024px) { .editor-workspace-layout { flex-direction: column; } }
</style>
