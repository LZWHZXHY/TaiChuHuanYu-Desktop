<template>
  <div class="note-editor-view">
    <div v-if="isContentLoading" class="content-loading-state">
      <div class="mini-spinner"></div>
      <p>正在打开笔记…</p>
    </div>

    <MainWorkspace
      ref="workspaceRef"
      v-else-if="displayNote"
      :is-content-loading="isContentLoading"
      :display-note="displayNote"
      :is-wiki-mode="false"
      :has-art-image="hasArtImage"
      :workspace-blocks="workspaceBlocks"
      @update:title="handleUpdateTitle"
      @change="handleWorkspaceChange"
      @open-sub-drawer="handleOpenSubDrawer"
      @editor-auto-sync="handleEditorAutoSync"
    />

    <div v-else class="empty-state">
      <p>从左侧选择一篇笔记，或点「＋ 新建」开始写</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, inject, ref } from 'vue';
import { useRoute } from 'vue-router';
import MainWorkspace from './MainWorkspace.vue';
import { useSpiritData } from '@/composables/useSpiritData';
import { useAutoSave } from '@/composables/useAutoSave'; // 🌟 引入全局单例保存引擎
import { checkHasImage, createEditorBlocks, createNoteSyncPayload } from '@/utils/editorHelpers';



const showToast = inject('showToast') as ((msg: string, duration?: number) => void) | undefined;

const route = useRoute();
const workspaceRef = ref<any>();

// ---------- 从父组件注入数据 ----------
const displayNote = inject('currentDisplayNote') as any;
const workspaceBlocks = inject('currentBlocks') as any;
const isContentLoading = inject('isContentLoading') as any;

// 注入打开快捷编辑器的方法
const openQuickEditor = inject('openQuickEditor') as (id: string) => void;

// ---------- 全局方法 ----------
const { updateNoteTitle, activeNote } = useSpiritData();
const { syncToCloud } = useAutoSave(); // 🌟 激活全局防抖同步引擎

// ---------- 计算属性 ----------
const hasArtImage = computed(() => {
  if (!displayNote.value) return false;
  return checkHasImage(displayNote.value.content);
});

// ---------- 事件处理 ----------
const handleUpdateTitle = (val: string) => {
  if (displayNote.value && route.params.id) {
    updateNoteTitle(route.params.id as string, val);
    displayNote.value.title = val;
    triggerDebouncedSync();
  }
};

const handleWorkspaceChange = (payload: any) => {
  if (payload && Array.isArray(payload.blocks)) {
    workspaceBlocks.value = payload.blocks;
    if (displayNote.value) {
      displayNote.value.blocks = payload.blocks;
      if (activeNote.value && activeNote.value.id === displayNote.value.id) activeNote.value.blocks = payload.blocks;
    }
    triggerDebouncedSync();
  }
};

const handleEditorAutoSync = (json: any) => {
  if (!displayNote.value || !route.params.id) return;

  const blocks = createEditorBlocks(json, route.params.id as string, displayNote.value.type || 'note');
  workspaceBlocks.value = blocks;
  displayNote.value.blocks = blocks;
  if (activeNote.value && activeNote.value.id === displayNote.value.id) activeNote.value.blocks = blocks;
  displayNote.value.content = json;

  triggerDebouncedSync();
};

const handleOpenSubDrawer = (targetId: string) => {
  if (openQuickEditor) {
    openQuickEditor(targetId);
  }
};

// ---------- 🌟 同步逻辑 (重构极简版) ----------
const triggerDebouncedSync = () => {
  const noteId = route.params.id as string;
  if (!noteId || !displayNote.value) return;

  const payload = createNoteSyncPayload(noteId, displayNote.value, { blocks: workspaceBlocks.value });
  syncToCloud(noteId, payload, showToast); 
};

// 快照保留原始块，避免白板、日程和博客系统块在 Tiptap 转换中丢失。
const getSnapshot = () => {
  const note = displayNote.value;
  if (!note || isContentLoading.value) return;
  const doc = workspaceRef.value?.getDocument();
  if (!doc && !['canvas', 'map', 'schedule'].includes(note.type)) throw new Error('编辑器尚未就绪，请稍后重试');
  const storedBlocks = activeNote.value && activeNote.value.id === note.id ? activeNote.value.blocks : note.blocks;
  const blocks = doc ? createEditorBlocks(doc, note.id, note.type) : (storedBlocks || workspaceBlocks.value);
  const systemBlocks = (storedBlocks || []).filter((b: any) => b.type?.startsWith('blog_fixed_'));
  return JSON.parse(JSON.stringify({ type: 'doc', content: doc?.content || [],
    blocks: doc ? [...systemBlocks, ...blocks].map((block, index) => ({ ...block, sortOrder: index })) : blocks }));
};

defineExpose({
  getSnapshot,
  save: () => triggerDebouncedSync(),
});
</script>

<style scoped>
/* 样式保持不变 */
.note-editor-view {
  width: 100%;
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: 0 0 20px;
}
.content-loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 300px;
  color: var(--text-secondary);
  gap: 12px;
}
.mini-spinner {
  width: 24px;
  height: 24px;
  border: 2px solid var(--border-line);
  border-top-color: var(--accent-color);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}
.empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: var(--text-secondary);
  font-size: 16px;
}
@keyframes spin {
  to { transform: rotate(360deg); }
}
</style>