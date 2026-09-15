<template>
  <div class="note-editor-view">
    <div v-if="isContentLoading" class="content-loading-state">
      <div class="mini-spinner"></div>
      <p>正在感应灵脉碎片...</p>
    </div>

    <MainWorkspace
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
      <p>请从侧边栏选择或新建一个碎片</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, inject, ref } from 'vue';
import { useRoute } from 'vue-router';
import MainWorkspace from './MainWorkspace.vue';
import { useSpiritData } from '@/composables/useSpiritData';
import { useAutoSave } from '@/composables/useAutoSave'; // 🌟 引入全局单例保存引擎
import { checkHasImage } from '@/utils/editorHelpers';
import { nanoid } from 'nanoid'



const showToast = inject('showToast') as ((msg: string, duration?: number) => void) | undefined;

const route = useRoute();

// ---------- 从父组件注入数据 ----------
const displayNote = inject('currentDisplayNote') as any;
const workspaceBlocks = inject('currentBlocks') as any;
const isContentLoading = inject('isContentLoading') as any;

// 注入打开快捷编辑器的方法
const openQuickEditor = inject('openQuickEditor') as (id: string) => void;

// ---------- 全局方法 ----------
const { updateNoteTitle } = useSpiritData();
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
    }
    triggerDebouncedSync();
  }
};

const handleEditorAutoSync = (json: any) => {
  if (!displayNote.value || !route.params.id) return;

  const blocks = json.content.map((b: any, i: number) => ({
    id: b.attrs?.id || nanoid(21), 
    ownerId: route.params.id as string,
    ownerType: displayNote.value?.type || 'note',
    type: b.type,
    sortOrder: i,
    data: JSON.stringify(b),
  }));
  workspaceBlocks.value = blocks;
  displayNote.value.blocks = blocks;
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

  // 1. 组装数据快照
  const payload = {
    noteId,
    title: displayNote.value.title || '',
    extraData: displayNote.value.extraData || '[]',
    tags: displayNote.value.tags || [],
    blocks: workspaceBlocks.value,
  };

  // 2. 丢给全局单例去排队处理，彻底避免并发写冲突
  syncToCloud(noteId, payload, showToast); 
};

defineExpose({
  save: () => triggerDebouncedSync(),
});
</script>

<style scoped>
/* 样式保持不变 */
.note-editor-view {
  width: 100%;
  height: 100%;
  overflow-y: auto;
  padding: 0 20px 20px;
}
.content-loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 300px;
  color: #86868b;
  gap: 12px;
}
.mini-spinner {
  width: 24px;
  height: 24px;
  border: 2px solid #f2f2f7;
  border-top-color: #0066cc;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}
.empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #c7c7cc;
  font-size: 16px;
}
@keyframes spin {
  to { transform: rotate(360deg); }
}
</style>