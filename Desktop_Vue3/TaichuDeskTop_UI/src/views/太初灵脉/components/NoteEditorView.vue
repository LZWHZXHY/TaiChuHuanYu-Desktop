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
import { computed, inject, onUnmounted, ref } from 'vue';
import { useRoute } from 'vue-router';
import MainWorkspace from './MainWorkspace.vue';
import { useSpiritData } from '@/composables/useSpiritData';
import { lingmaiApi } from '@/api/lingmai';
import { checkHasImage } from '@/utils/editorHelpers';

const showToast = inject('showToast') as ((msg: string, duration?: number) => void) | undefined;



const route = useRoute();

// ---------- 从父组件注入数据 ----------
const displayNote = inject('currentDisplayNote') as any;
const workspaceBlocks = inject('currentBlocks') as any;
const isContentLoading = inject('isContentLoading') as any;

// 注入打开快捷编辑器的方法
const openQuickEditor = inject('openQuickEditor') as (id: string) => void;

// ---------- 全局方法 ----------
const { updateNoteTitle, updateNoteContent } = useSpiritData();

// ---------- 计算属性 ----------
const hasArtImage = computed(() => {
  if (!displayNote.value) return false;
  return checkHasImage(displayNote.value.content);
});

// ---------- 同步定时器 ----------
let syncTimer: any = null;

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
    id: b.attrs?.id || Math.random().toString(36).substring(2, 11),
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

// ---------- 同步逻辑 ----------
const triggerDebouncedSync = () => {
  if (syncTimer) clearTimeout(syncTimer);
  syncTimer = setTimeout(async () => {
    await executeSync();
  }, 2000);
};

const executeSync = async () => {
  const noteId = route.params.id as string;
  if (!noteId || !displayNote.value) return;

  const payload = {
    noteId,
    title: displayNote.value.title || '',
    extraData: displayNote.value.extraData || '[]',
    tags: displayNote.value.tags || [],
    blocks: workspaceBlocks.value,
  };

  try {
    await lingmaiApi.updateNoteContent(noteId, payload);
    showToast?.('☁️ 已自动同步', 1500); // ✅ 使用可选链
  } catch (e) {
    console.error('同步失败', e);
    showToast?.('❌ 同步失败', 2000);
  }
};

// ---------- 清理 ----------
onUnmounted(() => {
  if (syncTimer) clearTimeout(syncTimer);
});

defineExpose({
  save: executeSync,
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