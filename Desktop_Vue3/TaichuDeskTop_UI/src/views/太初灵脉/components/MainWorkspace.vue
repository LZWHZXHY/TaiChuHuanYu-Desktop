<script setup lang="ts">
import { defineAsyncComponent, computed } from 'vue';
import SpiritEditor from '@/components/SpiritText.vue'; 

// 1. 定义从 index.vue 接收的数据 (Props)
const props = defineProps<{
  isContentLoading: boolean;
  displayNote: any;
  isWikiMode: boolean;
  hasArtImage: boolean;
  workspaceBlocks: any[];
}>();

// 2. 定义要向 index.vue 汇报的事件 (Emits)
const emit = defineEmits(['update:title', 'change', 'open-sub-drawer', 'editor-auto-sync']);

// 3. 把 12 个异步组件全部粘贴过来！
const WorkspaceNote = defineAsyncComponent(() => import('./WorkspaceNote.vue'));
const WorkspaceWiki = defineAsyncComponent(() => import('./WorkspaceWiki.vue'));
const WorkspaceArt = defineAsyncComponent(() => import('./WorkspaceArt.vue'));
const WorkspaceCanvas = defineAsyncComponent(() => import('./WorkspaceCanvas.vue'));
const WorkspaceMap = defineAsyncComponent(() => import('./WorkspaceMap.vue'));
const WorkspaceBlog = defineAsyncComponent(() => import('./WorkspaceBlog.vue'));
const WorkspacePost = defineAsyncComponent(() => import('./WorkspacePost.vue'));
const WorkspaceExcel = defineAsyncComponent(() => import('./WorkspaceExcel.vue'));
const WorkspaceChar = defineAsyncComponent(() => import('./WorkspaceChar.vue'));
const WorkspaceDoc = defineAsyncComponent(() => import('./WorkspaceDoc.vue'));
const WorkspaceSchedule = defineAsyncComponent(() => import('./WorkspaceSchedule.vue'));

// 4. 映射表也搬过来
const workspaceMap: Record<string, any> = {
  note: WorkspaceNote, wiki: WorkspaceWiki, art: WorkspaceArt, char: WorkspaceChar, schedule: WorkspaceSchedule,
  folder: WorkspaceNote, canvas: WorkspaceCanvas, map: WorkspaceMap, blog: WorkspaceBlog, post: WorkspacePost, excel: WorkspaceExcel, doc: WorkspaceDoc, 
};

// 5. 计算当前到底该用哪个组件
const CurrentWorkspaceComponent = computed(() => {
  return workspaceMap[props.displayNote?.type || 'note'] || WorkspaceNote;
});
</script>

<template>
  <div class="editor-scroll-body">
    <div v-if="props.isContentLoading" class="content-loading-state">
      <div class="mini-spinner"></div>
      <p>正在感应灵脉碎片...</p>
    </div>

    <template v-else-if="props.displayNote">
      <component 
        :is="CurrentWorkspaceComponent"
        :title="props.displayNote.title"
        :readonly="props.isWikiMode"
        :has-image="props.hasArtImage"
        :extra-data="props.displayNote?.extraData" 
        :note-id="props.displayNote.id" 
        :blocks="props.displayNote?.blocks || props.workspaceBlocks" 
        @update:title="emit('update:title', $event)"
        @change="emit('change', $event)"
        @open-sub-drawer="emit('open-sub-drawer', $event)" 
      >
        <template #editor>
          <SpiritEditor 
             :key="props.isWikiMode ? props.displayNote.id : props.displayNote.id" 
             @change="emit('editor-auto-sync', $event)"
          />
        </template>
      </component>
    </template>
  </div>
</template>