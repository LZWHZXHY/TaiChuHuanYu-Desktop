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
          @import-wiki="handleImportWiki"
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

        <div class="editor-scroll-body">
          <div v-if="isContentLoading" class="content-loading-state">
            <div class="mini-spinner"></div>
            <p>正在感应灵脉碎片...</p>
          </div>

          <template v-else-if="displayNote">
            <component 
              :is="CurrentWorkspaceComponent"
              :title="displayNote.title"
              :readonly="isWikiMode"
              :has-image="hasArtImage"
              :extra-data="displayNote?.extraData" 
              :note-id="currentNoteId" 
              @update:title="handleUpdateTitle"
            >
              <template #editor>
                <SpiritEditor 
                  ref="editorRef" 
                  :key="isWikiMode ? displayNote.id : currentNoteId" 
                  @change="handleEditorAutoSync"
                />
              </template>
            </component>
          </template>
        </div>
      </main>

      <RightSidePanel 
        v-if="!isMobile && activeNote && currentNoteId" 
        :note-id="currentNoteId" 
        v-model:extraData="activeNote.extraData"
        @select="handleSelectNote" 
        @change="triggerDebouncedSync"
      />
    </div>

    <HistoryPanel v-model="isHistoryOpen" :note-id="currentNoteId" @rollback="onRollback" @manual-save="handleManualSave" />
    <PublishModal 
      v-model="showPublishModal" 
      :note-id="currentNoteId" 
      :note-type="activeNote?.type || 'note'"  :space-name="activeSpaceName" 
      @success="onPublishSuccess" 
    />

    <transition name="fade">
      <div v-if="showImportWikiModal" class="loading-overlay" @click.self="showImportWikiModal = false">
        <div class="spirit-modal-content pop-enter-active">
          <h3 class="modal-title">🌌 接入百科宇宙</h3>
          <input v-model="importWikiId" type="text" class="spirit-id-input" placeholder="请输入 Wiki ID..." @keyup.enter="confirmImportWiki" autofocus />
          <div class="modal-actions">
            <button class="cancel-btn" @click="showImportWikiModal = false">取消</button>
            <button class="save-btn" @click="confirmImportWiki" :disabled="!importWikiId.trim()">开始感应</button>
          </div>
        </div>
      </div>
    </transition>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
import SidebarIndex from './components/SidebarIndex.vue';
// 🌟 核心引入替换：直接将双链面板升级为大融合右侧面板
import RightSidePanel from './components/RightSidePanel.vue';
import SpiritEditor from '../../components/SpiritText.vue'; 
import HistoryPanel from './components/HistoryPanel.vue';
import GraphView from './components/GraphView.vue';
import PublishModal from './components/PublishModal.vue';
import NoteSettingsPanel from './components/NoteSettingsPanel.vue';
import TopBar from './components/TopBar.vue'; 

import WorkspaceNote from './components/WorkspaceNote.vue';
import WorkspaceWiki from './components/WorkspaceWiki.vue';
import WorkspaceArt from './components/WorkspaceArt.vue';

import { useSpiritData } from '../../composables/useSpiritData';
import { lingmaiApi } from '../../api/lingmai';
import { wikiApi } from '@/api/Wiki'; 

type NoteType = 'note' | 'thought' | 'wiki' | 'char' | 'art' | 'folder';

const { 
  notes, currentNoteId, activeNote, isLoading, currentSpaceId,
  fetchAllNotes, selectNote, createNewNote, updateNoteTitle, updateNoteContent,
  isWikiMode, wikiEditData, enterWikiMode, exitWikiMode
} = useSpiritData();

const isMobile = ref(false);
const isSidebarOpen = ref(false);
const isHistoryOpen = ref(false); 
const isGraphViewOpen = ref(false);
const editorRef = ref();
const isContentLoading = ref(false);
const showPublishModal = ref(false);
const spaces = ref<any[]>([]); 
const showImportWikiModal = ref(false);
const importWikiId = ref('');
const pendingWikiContent = ref<any>(null);

const displayFilters = ref<Record<string, boolean>>({
  wiki: true, char: true, art: true, note: true, thought: true, folder: true 
});

const isSettingsOpen = ref(false); 
const currentEditorJson = ref<any>(null);

// 缓存当前正在操作的属性数组
const currentWikiProperties = ref<any[]>([]);

let syncDebounceTimer: any = null;

const handleEditorChange = (json: any) => { currentEditorJson.value = json; };

const checkHasImage = (node: any): boolean => {
  if (!node) return false;
  if (node.type === 'image') return true;
  if (node.content && Array.isArray(node.content)) return node.content.some(checkHasImage);
  return false;
};

const getTextLength = (node: any): number => {
  if (!node) return 0;
  let len = 0;
  if (node.text) len += node.text.length;
  if (node.content && Array.isArray(node.content)) {
    node.content.forEach((child: any) => len += getTextLength(child));
  }
  return len;
};

const hasArtImage = computed(() => checkHasImage(currentEditorJson.value || displayNote.value?.content));
const currentTextLength = computed(() => getTextLength(currentEditorJson.value || displayNote.value?.content));

const canPublishDynamic = computed(() => {
  if (!activeNote.value) return false;
  switch (activeNote.value.type) {
    case 'art': return hasArtImage.value;
    case 'thought': return currentTextLength.value <= 500;
    default: return true; 
  }
});

// 🌟 核心加固：切换/加载笔记时，将基准 extraData 深度锁定进缓存数组
watch(currentNoteId, () => { 
  currentEditorJson.value = null; 
  if (syncDebounceTimer) clearTimeout(syncDebounceTimer);
  
  if (activeNote.value?.extraData) {
    try {
      currentWikiProperties.value = JSON.parse(activeNote.value.extraData);
    } catch {
      currentWikiProperties.value = [];
    }
  } else {
    currentWikiProperties.value = [];
  }
}, { immediate: true });

const workspaceMap: Record<string, any> = {
  note: WorkspaceNote, wiki: WorkspaceWiki, art: WorkspaceArt,
  thought: WorkspaceNote, char: WorkspaceNote, folder: WorkspaceNote
};

const CurrentWorkspaceComponent = computed(() => workspaceMap[displayNote.value?.type || 'note'] || WorkspaceNote);

const currentUserId = ref('current_user_id'); 
const isWikiAuthor = computed(() => (wikiEditData.value as any)?.authorId === currentUserId.value);
const displayNote = computed<any>(() => isWikiMode.value && wikiEditData.value ? { ...(wikiEditData.value as any), type: 'wiki' } : activeNote.value);

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

watch(() => editorRef.value?.isInitialized, (isReady) => {
  if (isReady && pendingWikiContent.value && editorRef.value?.editor) {
    try {
      editorRef.value.editor.commands.setContent(pendingWikiContent.value);
      pendingWikiContent.value = null; 
    } catch (e) { console.error("内部错误:", e); }
  }
}, { immediate: true });

// 🌟 接住从 RightSidePanel 抛上来的属性变动事件并触发固化流
const handlePropertiesChange = (properties: any[]) => {
  currentWikiProperties.value = properties;
  if (activeNote.value) {
    activeNote.value.extraData = JSON.stringify(properties);
  }
  triggerDebouncedSync();
};

// 接住富文本自动保存信号
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

// 🌟 找到 index.vue 中的 executeNetworkSync 方法，替换为以下安全固化逻辑
const executeNetworkSync = async (editorJson: any) => {
  const safeNoteId = currentNoteId.value; 
  if (!safeNoteId || !editorJson) return;

  const flatBlocksPayload = editorJson.content?.map((b: any, i: number) => ({
    id: b.attrs?.id || Math.random().toString(36).substring(2, 11),
    type: b.type,
    sortOrder: i,
    data: JSON.stringify(b)
  })) || [];

  // 🌟【核心修复：三重防御机制】
  // 坚决不使用极易被抢跑洗掉的 currentWikiProperties.value 兜底
  let finalExtraData = "[]";
  
  if (activeNote.value?.extraData && activeNote.value.extraData !== "[]") {
    // 第一层防御：如果当前内存中的 activeNote 已经有了有效的属性字符串，直接用它
    finalExtraData = activeNote.value.extraData;
  } else if (currentWikiProperties.value && currentWikiProperties.value.length > 0) {
    // 第二层防御：如果缓存数组里确实有新增加的有效属性，将其序列化
    finalExtraData = JSON.stringify(currentWikiProperties.value);
  } else if (displayNote.value?.extraData && displayNote.value.extraData !== "[]") {
    // 第三层防御：百科或视图多态模型下的快照兜底
    finalExtraData = displayNote.value.extraData;
  }

  const syncPayload = {
    noteId: safeNoteId,
    title: activeNote.value?.title || displayNote.value?.title || '', 
    extraData: finalExtraData, // 🌟 此时发送的绝对不会再被抢跑重置为 "[]"
    blocks: flatBlocksPayload
  };

  try {
    await lingmaiApi.updateNoteContent(safeNoteId, syncPayload);
  } catch (e) {
    console.error("太初内核群异步固化失败:", e);
  }
};

const handleSave = async () => {
  if (isWikiMode.value && wikiEditData.value) {
    const content = editorRef.value.getJSON(); 
    try {
      const data = wikiEditData.value as any;
      if(typeof (wikiApi as any).updateFromNote === 'function') {
         await (wikiApi as any).updateFromNote({ articleId: data.id, content: JSON.stringify(content), summary: isWikiAuthor.value ? "原作者更新" : "协作修改", baseRevisionId: data.currentRevisionId || data.revisionId });
        alert("提交成功！");
      }
      exitWikiMode();
    } catch (e: any) {
      if (e.response && e.response.status === 409) alert("⚠️ 提交失败：词条已被更新。");
      else alert("操作失败，请重试");
    }
  } else {
    if (!editorRef.value) return;
    if (syncDebounceTimer) clearTimeout(syncDebounceTimer); 
    await executeNetworkSync(editorRef.value.getJSON());
    alert("灵脉同步成功！");
  }
};

const handleUpdateTitle = (val: string) => { 
  if (!isWikiMode.value && currentNoteId.value) {
    updateNoteTitle(currentNoteId.value, val); 
    if (activeNote.value) {
      activeNote.value.title = val;
    }
    triggerDebouncedSync();
  } 
};

const handleUpdateFilters = (val: Record<string, boolean>) => { displayFilters.value = val; };
const handleUpdateNoteMeta = async (updates: any) => { if (!currentNoteId.value || !activeNote.value) return; try { await lingmaiApi.updateNoteMeta(currentNoteId.value, updates); Object.assign(activeNote.value, updates); } catch (e) {} };
const handleUpdateSpaceMeta = async (updates: any) => { const { id, ...data } = updates; if (!id) return; try { await lingmaiApi.updateSpaceMeta(id, data); const index = spaces.value.findIndex(s => s.id === id); if (index !== -1) spaces.value[index] = { ...spaces.value[index], ...data }; } catch (e) {} };
const handleDeleteNote = async (id: string) => { if (confirm('此操作不可逆，是否确定？')) { await lingmaiApi.deleteNote(id); await fetchAllNotes(); currentNoteId.value = ''; isSettingsOpen.value = false; } };
const activeSpaceName = computed(() => spaces.value.find(s => s.id === currentSpaceId.value)?.name || '未知位面');
const checkScreen = () => { isMobile.value = window.innerWidth <= 1024; };
const handleSelectNote = async (id: string) => { if (isMobile.value) isSidebarOpen.value = false; isContentLoading.value = true; try { await selectNote(id); } finally { setTimeout(() => { isContentLoading.value = false; }, 200); } };
const handleCreateNote = async (type: NoteType = 'note', folderId: string | null = null) => { const newNote = await createNewNote({ type: type, folderId: folderId }); if (newNote && isMobile.value && type !== 'folder') isSidebarOpen.value = false; };
const onPublishSuccess = (newType: string) => { if (activeNote.value) { activeNote.value.isPublic = true; activeNote.value.type = newType as NoteType; } };
const handleUnpublish = async () => { if (!currentNoteId.value) return; try { await lingmaiApi.unpublishNote(currentNoteId.value); if (activeNote.value) activeNote.value.isPublic = false; } catch (err) {} };
const handlePublishClick = () => { if (!activeNote.value) return; activeNote.value.isPublic ? handleUnpublish() : showPublishModal.value = true; };
const onRollback = async (revision: any) => { try { await lingmaiApi.rollbackTo(currentNoteId.value, revision.id); const freshNote = await selectNote(currentNoteId.value, true) as any; if (editorRef.value && freshNote?.tiptapContent) { editorRef.value.isInitialized = false; editorRef.value.editor?.commands.setContent(freshNote.tiptapContent); setTimeout(() => { if (editorRef.value) editorRef.value.isInitialized = true; }, 500); } isHistoryOpen.value = false; } catch (e) {} };
const handleManualSave = async () => { if (!editorRef.value || !currentNoteId.value) return; try { await lingmaiApi.createSnapshot(currentNoteId.value, editorRef.value.getJSON(), "用户手动固化"); } catch (e) {} };
const initSpaces = async () => { try { spaces.value = await lingmaiApi.getSpaces() as any; } catch (e) {} };

onMounted(async () => { checkScreen(); window.addEventListener('resize', checkScreen); await initSpaces(); await fetchAllNotes(); if (currentNoteId.value) await handleSelectNote(currentNoteId.value); });
onUnmounted(() => {
  window.removeEventListener('resize', checkScreen);
  if (syncDebounceTimer) clearTimeout(syncDebounceTimer);
});
</script>

<style scoped>
.spirit-link-app { display: flex; width: 100%; height: 100%; background: #ffffff; overflow: hidden; position: relative; }
.sidebar-layer { width: 280px; flex-shrink: 0; transition: transform 0.4s cubic-bezier(0.16, 1, 0.3, 1); z-index: 2000; border-right: 1px solid #f2f2f2; }

/* 🌟 核心重构样式：为工作区提供全新的横向 flex 排布 */
.editor-workspace-layout {
  display: flex;
  flex: 1;
  width: 100%;
  height: 100%;
  overflow: hidden;
}

.spirit-main-editor { flex: 1; display: flex; flex-direction: column; min-width: 0; background: #fafafa; }
.editor-scroll-body { flex: 1; overflow-y: auto; padding: 40px 5%; }

.loading-overlay { position: fixed; inset: 0; background: rgba(255, 255, 255, 0.9); backdrop-filter: blur(10px); z-index: 9999; display: flex; align-items: center; justify-content: center; }
.spirit-loading-content { text-align: center; color: #86868b; }
.spirit-spinner { width: 32px; height: 32px; border: 2px solid #f3f3f3; border-top: 2px solid #0066cc; border-radius: 50%; margin: 0 auto 16px; animation: spin 1s linear infinite; }
.content-loading-state { display: flex; flex-direction: column; align-items: center; justify-content: center; height: 40vh; color: #86868b; gap: 12px; font-size: 13px; }
.mini-spinner { width: 24px; height: 24px; border: 2px solid #f2f2f7; border-top-color: #0066cc; border-radius: 50%; animation: spin 0.8s linear infinite; }
.mobile-overlay { position: absolute; inset: 0; background: rgba(255,255,255,0.7); backdrop-filter: blur(4px); z-index: 1999; }
@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
@media (max-width: 1024px) { 
  .editor-workspace-layout { flex-direction: column; }
  .sidebar-layer { position: absolute; top: 0; left: 0; bottom: 0; transform: translateX(-100%); background: #ffffff; box-shadow: 20px 0 50px rgba(0,0,0,0.05); } 
  .sidebar-layer.open { transform: translateX(0); } 
  .editor-scroll-body { padding: 20px; } 
}
.fade-enter-active, .fade-leave-active { transition: opacity 0.3s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
.pop-enter-active { animation: pop 0.3s cubic-bezier(0.16, 1, 0.3, 1); }
@keyframes pop { 0% { transform: scale(0.9); opacity: 0; } 100% { transform: scale(1); opacity: 1; } }
.spirit-modal-content { background: #ffffff; width: 90%; max-width: 400px; padding: 30px; border-radius: 16px; box-shadow: 0 20px 60px rgba(0, 0, 0, 0.1); text-align: center; position: relative; z-index: 10000; }
.modal-title { font-size: 1.2rem; font-weight: 600; color: #1d1d1f; margin: 0 0 8px; }
.modal-desc { font-size: 13px; color: #86868b; margin-bottom: 24px; }
.spirit-id-input { width: 100%; padding: 12px 16px; border: 1px solid #d2d2d7; border-radius: 10px; font-size: 14px; margin-bottom: 24px; outline: none; transition: all 0.2s; box-sizing: border-box; }
.spirit-id-input:focus { border-color: #0066cc; box-shadow: 0 0 0 3px rgba(0, 102, 204, 0.1); }
.modal-actions { display: flex; justify-content: flex-end; gap: 12px; }
.cancel-btn { background: #f5f5f7; border: none; padding: 8px 20px; border-radius: 40px; color: #1d1d1f; font-size: 13px; font-weight: 500; cursor: pointer; transition: background 0.2s; }
.cancel-btn:hover { background: #e5e5ea; }
</style>