<template>
  <div class="spirit-link-app" :class="{ 'is-mobile': isMobile }">
    <transition name="fade">
      <GraphView v-if="isGraphViewOpen" @close="isGraphViewOpen = false" @select-note="handleSelectNote" />
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
      <div v-if="isMobile && isSidebarOpen" class="mobile-overlay" @click="isSidebarOpen = false"></div>
    </transition>

    <SidebarIndex 
      :notes="notes" 
      :active-id="currentNoteId"
      :filters="displayFilters"
      :class="['sidebar-layer', { 'open': isSidebarOpen || !isMobile }]"
      @select="handleSelectNote"
      @create="handleCreateNote"
    />

    <div class="editor-workspace-layout">
      <main class="spirit-main-editor">
        <TopBar 
          :is-mobile="isMobile"
          :is-wiki-mode="isWikiMode"
          :active-note="activeNote"
          :wiki-edit-data="wikiEditData"
          @toggle-sidebar="isSidebarOpen = true"
          @open-graph="isGraphViewOpen = true"
          @open-settings="isSettingsOpen = true"
          @import-wiki="showImportWikiModal = true"
          @exit-wiki="exitWikiMode"
        />

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
          @save="handleSave"
        />

        <MainWorkspace 
          :is-content-loading="isContentLoading"
          :display-note="displayNote"
          :is-wiki-mode="isWikiMode"
          :has-art-image="hasArtImage"
          :workspace-blocks="workspaceBlocks"
          @update:title="handleUpdateTitle"
          @change="handleWorkspaceChange"
          @open-sub-drawer="handleOpenQuickEditor"
          @editor-auto-sync="handleEditorAutoSync"
        />
      </main>

      <RightSidePanel 
        v-if="!isMobile && activeNote && currentNoteId" 
        :note-id="currentNoteId" 
        v-model:extraData="activeNote.extraData"
        v-model:tags="activeNote.tags"      
        @select="handleSelectNote" 
        @change="triggerDebouncedSync"
      />

      <QuickEditorDrawer 
        v-model="isQuickEditorOpen"
        :note-id="quickEditorNoteId"
        :note-meta="quickEditorNoteMeta"
        :is-loading="isQuickEditorLoading"
      />
    </div>

    <HistoryPanel v-model="isHistoryOpen" :note-id="currentNoteId" @rollback="onRollback" @manual-save="handleManualSave" />
    <PublishModal 
      v-model="showPublishModal" 
      :note-id="currentNoteId" 
      :note-type="activeNote?.type || 'note'"  
      :space-name="activeSpaceName" 
      @success="onPublishSuccess" 
    />
    
    <WikiImportModal 
      v-model="showImportWikiModal" 
      @confirm="confirmImportWiki" 
    />

    <SpiritToast ref="toastRef" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch} from 'vue';
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
import MainWorkspace from './components/MainWorkspace.vue';
import { useSpiritData } from '../../composables/useSpiritData';
import { lingmaiApi } from '../../api/lingmai';
import { wikiApi } from '@/api/Wiki'; 
import { checkHasImage, getTextLength } from '@/utils/editorHelpers';

type NoteType = 'note' | 'post' | 'wiki' | 'char' | 'art' | 'folder' | 'canvas' | 'map' | 'excel' | 'blog' | 'doc' | 'schedule';

const { 
  notes, currentNoteId, activeNote, isLoading, currentSpaceId,
  fetchAllNotes, selectNote, createNewNote, updateNoteTitle, updateNoteContent,
  isWikiMode, wikiEditData, enterWikiMode, exitWikiMode
} = useSpiritData();

const isMobile = ref(false);
const isSidebarOpen = ref(false);
const isGraphViewOpen = ref(false);
const editorRef = ref();
const toastRef = ref(); 
const isContentLoading = ref(false);
const showPublishModal = ref(false);
const spaces = ref<any[]>([]); 
const showImportWikiModal = ref(false);
const importWikiId = ref('');
const pendingWikiContent = ref<any>(null);
const isHistoryOpen = ref(false);
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
  schedule:true,
});


const isSettingsOpen = ref(false); 
const currentEditorJson = ref<any>(null);
const currentWikiProperties = ref<any[]>([]);

let syncDebounceTimer: any = null;
const workspaceBlocks = ref<any[]>([]);


// 🌟 补全缺失项 2：计算属性 displayNote，处理百科（Wiki）模式与常规形态的数据切换流
const displayNote = computed<any>(() => isWikiMode.value && wikiEditData.value ? { ...(wikiEditData.value as any), type: 'wiki' } : activeNote.value);

// 🌟 补全缺失项 3：用户鉴权及 Wiki 创作者标记计算属性
const currentUserId = ref('current_user_id'); 
const isWikiAuthor = computed(() => (wikiEditData.value as any)?.authorId === currentUserId.value);

// ✅ 替换成这段极简代码：
const isQuickEditorOpen = ref(false);
const quickEditorNoteId = ref('');
const quickEditorNoteMeta = ref<any>({}); 
const isQuickEditorLoading = ref(false);

const handleOpenQuickEditor = async (targetId: string) => {
  isQuickEditorLoading.value = true;
  try {
    const targetNote: any = await lingmaiApi.getNote(targetId); 
    // 如果是画板、文件夹、地图，直接在主视图打开，不弹抽屉
    if (['canvas', 'folder', 'map'].includes(targetNote.type)) {
       isQuickEditorOpen.value = false;
       selectNote(targetId, true); 
       return;
    }
    // 正常笔记，只负责把数据喂给抽屉，剩下的让抽屉自己去解析和渲染！
    quickEditorNoteId.value = targetId;
    quickEditorNoteMeta.value = targetNote || {}; 
    isQuickEditorOpen.value = true;
  } catch (e) {
    console.error("抽取数据失败", e);
    toastRef.value?.show("抽取本体失败");
  } finally {
    isQuickEditorLoading.value = false;
  }
};



// 🌟 高内聚组件契约达成：主控接收全量积木链快照，不做多余的过滤、拆分或重刷
const handleWorkspaceChange = (payload: any) => {
  if (payload && Array.isArray(payload.blocks)) {
    workspaceBlocks.value = payload.blocks;
    if (activeNote.value) {
      activeNote.value.blocks = payload.blocks;
    }
    triggerDebouncedSync();
  }
};

const handleEditorChange = (json: any) => { currentEditorJson.value = json; };



const hasArtImage = computed(() => checkHasImage(currentEditorJson.value || displayNote.value?.content));
const currentTextLength = computed(() => getTextLength(currentEditorJson.value || displayNote.value?.content));

// index.vue 里的 canPublishDynamic 修改后：
const canPublishDynamic = computed(() => {
  if (!activeNote.value) return false;

  // 🌟【核心拦截】：封锁普通的 note（笔记）和 folder（文件夹）形态，使其绝不能发布
  if (activeNote.value.type === 'note' || activeNote.value.type === 'folder' || activeNote.value.type === 'schedule' || activeNote.value.type === 'char' ||activeNote.value.type === 'canvas' || activeNote.value.type === 'map') {
    return false;
  }

  switch (activeNote.value.type) {
    case 'art': 
      return hasArtImage.value; // 画廊：必须包含至少一张图片
    case 'post': 
      return currentTextLength.value <= 500; // 简语：限制在 500 字以内
    default: 
      return true; // 随笔 (blog)、词条 (wiki)、角色 (char) 等衍生多态组件允许发布
  }
});

// 🌟 补全缺失项 4：百科召唤与接入方法
const handleImportWiki = () => { importWikiId.value = ''; showImportWikiModal.value = true; };

const confirmImportWiki = async () => {
  const wikiId = importWikiId.value.trim();
  if (!wikiId) return;
  showImportWikiModal.value = false;
  isContentLoading.value = true;
  pendingWikiContent.value = null; 
  
  try {
    await enterWikiMode(wikiId);
    let finalContent: any = { type: 'doc', content: [{ type: 'paragraph' }] };
    if (wikiEditData.value?.content) {
      const rawData = JSON.parse(JSON.stringify(wikiEditData.value.content));
      if (typeof rawData === 'string') {
        const blocks = rawData.split('\n').filter((b: string) => b.trim());
        if (blocks.length > 0) {
          finalContent.content = blocks.map((blockStr: string) => {
            try {
              const parsed = JSON.parse(blockStr);
              return { type: parsed.type || 'paragraph', ...(parsed.attrs ? { attrs: parsed.attrs } : {}), ...(parsed.content ? { content: parsed.content } : {}) };
            } catch {
              return { type: 'paragraph', content: [{ type: 'text', text: blockStr }] };
            }
          });
        }
      } else if (typeof rawData === 'object') {
        finalContent = rawData.type === 'doc' ? rawData : { content: [{ type: rawData.type || 'paragraph', ...(rawData.attrs ? { attrs: rawData.attrs } : {}), ...(rawData.content ? { content: rawData.content } : {}) }] };
      }
    }
    pendingWikiContent.value = finalContent;
    isContentLoading.value = false;
  } catch (e) {
    console.error("加载 Wiki 异常:", e);
    alert("Wiki 感应失败，请检查 ID");
    isContentLoading.value = false;
  }
};

watch(currentNoteId, () => { 
  currentEditorJson.value = null; 
  workspaceBlocks.value = [];

  if (syncDebounceTimer) clearTimeout(syncDebounceTimer);
  if (!activeNote.value) return;

  const currentBlocks = activeNote.value.blocks || [];

  if (activeNote.value.type === 'char') {
    const textBlocks = currentBlocks.filter((b: any) => b.type !== 'char-layout-block');
    const parsedNodes = textBlocks.map((b: any) => {
      try { return typeof b.data === 'string' ? JSON.parse(b.data) : b.data; } catch { return { type: 'paragraph', content: [] }; }
    });
    activeNote.value.content = { type: 'doc', content: parsedNodes.length > 0 ? parsedNodes : [{ type: 'paragraph' }] };
  } 
  else if (activeNote.value.type === 'blog') {
    const textBlocks = currentBlocks.filter(
      (b: any) => b.type !== 'blog_fixed_cover' && b.type !== 'blog_fixed_excerpt'
    ).sort((a: any, b: any) => (a.sortOrder || 0) - (b.sortOrder || 0));

    const parsedNodes = textBlocks.map((b: any) => {
      try { return typeof b.data === 'string' ? JSON.parse(b.data) : b.data; } catch { return { type: 'paragraph', content: [] }; }
    });
    activeNote.value.content = { type: 'doc', content: parsedNodes.length > 0 ? parsedNodes : [{ type: 'paragraph' }] };
  }
  
  if (activeNote.value?.extraData) {
    try { currentWikiProperties.value = JSON.parse(activeNote.value.extraData); } catch { currentWikiProperties.value = []; }
  } else {
    currentWikiProperties.value = [];
  }
}, { immediate: true });

const handlePropertiesChange = (properties: any[]) => {
  currentWikiProperties.value = properties;
  if (activeNote.value) {
    activeNote.value.extraData = JSON.stringify(properties);
  }
  triggerDebouncedSync();
};

const handleEditorAutoSync = (latestJson: any) => {
  handleEditorChange(latestJson);
  triggerDebouncedSync();
};

const triggerDebouncedSync = () => {
  if (syncDebounceTimer) clearTimeout(syncDebounceTimer);
  syncDebounceTimer = setTimeout(() => {
    executeNetworkSync(editorRef.value?.getJSON());
  }, 2000); 
};

const executeNetworkSync = async (editorJson: any) => {
  const safeNoteId = currentNoteId.value; 
  if (!safeNoteId || !activeNote.value) return;

  let finalBlocks: any[] = [];
  let finalExtraData = activeNote.value.extraData || '[]';

  if (workspaceBlocks.value && workspaceBlocks.value.length) {
    finalBlocks = workspaceBlocks.value;
  }
  else if (editorJson && editorJson.content) {
    finalBlocks = editorJson.content.map((b: any, i: number) => ({
      id: b.attrs?.id || Math.random().toString(36).substring(2, 11),
      ownerId: safeNoteId,                             
      ownerType: activeNote.value?.type || 'note',   
      type: b.type,
      sortOrder: i,
      data: JSON.stringify(b)
    }));
  }

  if (finalBlocks.length) {
    activeNote.value.blocks = finalBlocks;
  }

  const syncPayload = {
    noteId: safeNoteId,
    title: activeNote.value.title || '', 
    extraData: finalExtraData,
    幕后数据: "taichu-universe",
    tags: activeNote.value.tags || [], 
    blocks: finalBlocks
  };

  try {
    await lingmaiApi.updateNoteContent(safeNoteId, syncPayload);
    toastRef.value?.show("☁️ 已自动同步", 1500); 
  } catch (e) {
    console.error("同步失败:", e);
  }
};

const handleSave = async () => {
  if (isWikiMode.value && wikiEditData.value) {
    const content = editorRef.value.getJSON(); 
    try {
      const data = wikiEditData.value as any;
      if(typeof (wikiApi as any).updateFromNote === 'function') {
         await (wikiApi as any).updateFromNote({ 
           articleId: data.id, 
           content: JSON.stringify(content), 
           summary: isWikiAuthor.value ? "原作者更新" : "协作修改", 
           baseRevisionId: data.currentRevisionId || data.revisionId 
         });
         toastRef.value?.show("✨ 百科词条提交成功！");
      }
      exitWikiMode();
    } catch (e: any) {
      if (e.response && e.response.status === 409) {
        toastRef.value?.show("⚠️ 提交失败：词条已被更新。");
      } else {
        toastRef.value?.show("❌ 操作失败，请重试");
      }
    }
  } else {
    if (!editorRef.value) return;
    if (syncDebounceTimer) clearTimeout(syncDebounceTimer); 
    await executeNetworkSync(editorRef.value.getJSON());
    toastRef.value?.show("✨ 灵脉同步成功！");
  }
};

const handleUpdateTitle = (val: string) => { 
  if (!isWikiMode.value && currentNoteId.value) {
    updateNoteTitle(currentNoteId.value, val); 
    if (activeNote.value) activeNote.value.title = val;
    triggerDebouncedSync();
  } 
};

const handleUpdateFilters = (val: Record<string, boolean>) => { displayFilters.value = val; };
const handleUpdateNoteMeta = async (updates: any) => { if (!currentNoteId.value || !activeNote.value) return; try { await lingmaiApi.updateNoteMeta(currentNoteId.value, updates); Object.assign(activeNote.value, updates); } catch (e) {} };
const handleUpdateSpaceMeta = async (updates: any) => { const { id, ...data } = updates; if (!id) return; try { await lingmaiApi.updateSpaceMeta(id, data); const index = spaces.value.findIndex(s => s.id === id); if (index !== -1) spaces.value[index] = { ...spaces.value[index], ...data }; } catch (e) {} };
const handleDeleteNote = async (id: string) => { if (confirm('此操作不可逆，是否确定？')) { await lingmaiApi.deleteNote(id); await fetchAllNotes(); currentNoteId.value = ''; isSettingsOpen.value = false; } };
const activeSpaceName = computed(() => spaces.value.find(s => s.id === currentSpaceId.value)?.name || '未知位面');
const checkScreen = () => { isMobile.value = window.innerWidth <= 1024; };
const handleSelectNote = async (id: string) => {
  if (isMobile.value) isSidebarOpen.value = false;
  isContentLoading.value = true;
  try { await selectNote(id, true); } finally { setTimeout(() => { isContentLoading.value = false; }, 200); }
};
const handleCreateNote = async (type: NoteType = 'note', folderId: string | null = null) => { const newNote = await createNewNote({ type: type, folderId: folderId }); if (newNote && isMobile.value && type !== 'folder') isSidebarOpen.value = false; };
const onPublishSuccess = (newType: string) => { if (activeNote.value) { activeNote.value.isPublic = true; activeNote.value.type = newType as NoteType; } };
const handleUnpublish = async () => { if (!currentNoteId.value) return; try { await lingmaiApi.unpublishNote(currentNoteId.value); if (activeNote.value) activeNote.value.isPublic = false; } catch (err) {} };
const handlePublishClick = () => { if (!activeNote.value) return; activeNote.value.isPublic ? handleUnpublish() : showPublishModal.value = true; };
const onRollback = async (revision: any) => { try { await lingmaiApi.rollbackTo(currentNoteId.value, revision.id); const freshNote = await selectNote(currentNoteId.value, true) as any; if (editorRef.value && freshNote?.tiptapContent) { editorRef.value.isInitialized = false; editorRef.value.editor?.commands.setContent(freshNote.tiptapContent); setTimeout(() => { if (editorRef.value) editorRef.value.isInitialized = true; }, 500); } isHistoryOpen.value = false; } catch (e) {} };
const handleManualSave = async () => { if (!editorRef.value || !currentNoteId.value) return; try { await lingmaiApi.createSnapshot(currentNoteId.value, editorRef.value.getJSON(), "用户手动固化"); } catch (e) {} };
const initSpaces = async () => { try { spaces.value = await lingmaiApi.getSpaces() as any; } catch (e) {} };

onMounted(async () => { checkScreen(); window.addEventListener('resize', checkScreen); await initSpaces(); await fetchAllNotes(); if (currentNoteId.value) await handleSelectNote(currentNoteId.value); });
onUnmounted(() => { window.removeEventListener('resize', checkScreen); if (syncDebounceTimer) clearTimeout(syncDebounceTimer); });
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