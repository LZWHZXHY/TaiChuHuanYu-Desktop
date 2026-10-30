<template>
  <header class="editor-header studio-heading">
    <div class="header-left">
      <button v-if="isMobile" class="menu-toggle-btn" aria-label="打开灵脉目录" @click="$emit('toggle-sidebar')">
        <span class="icon">☰</span>
      </button>
      
      <div class="breadcrumb">
        <span class="root">我的灵脉</span>
        <span class="sep">/</span>
        <span class="current">{{ activeNote?.title || '未命名碎片' }}</span>
        <span v-if="destText" class="dest-badge" :title="'发布后会出现在这里'">发布 → {{ destText }}</span>
      </div>
    </div>
    
    <div class="action-btns">
      <button class="action-btn graph-btn" @click="$emit('open-graph')" title="全屏关系图谱" aria-label="全屏关系图谱">
        <span class="icon">🕸️</span> <span v-if="!isMobile">全屏图谱</span>
      </button>

      <button class="action-btn" @click="$emit('open-settings')" title="碎片设定" aria-label="碎片设定" :disabled="!activeNote">
        <span class="icon" aria-hidden="true">⚙</span><span v-if="!isMobile">设置</span>
      </button>
    </div>
  </header>
</template>

<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  isMobile: boolean;
  activeNote: any;
}>();

defineEmits([
  'toggle-sidebar', 
  'open-graph', 
  'open-settings'
]);

// 碎片形态 → 发布后出现的板块（顶部常驻提示）
const destMap: Record<string, string> = {
  blog: '太初随笔 · 博客',
  post: '太初随笔 · 帖子',
  wiki: '灵脉百科',
  art: '画廊展厅',
  doc: '协作项目',
  char: '柴圈板块'
};
const destText = computed(() => {
  const t = props.activeNote?.type as string;
  return t ? (destMap[t] || '') : '';
});
</script>

<style scoped>
.editor-header { min-height: 60px; padding: 10px 20px; display: flex; gap: 12px; justify-content: space-between; align-items: center; border-bottom: 1px solid var(--border-line); background: var(--paper-card); flex-shrink: 0; }
.header-left { display: flex; align-items: center; min-width: 0; gap: 10px; }
.breadcrumb { font-size: 13px; color: var(--text-secondary); display: flex; align-items: center; gap: 8px; min-width: 0; white-space: nowrap; }
.breadcrumb .current { color: var(--text-primary); font-weight: 600; overflow: hidden; text-overflow: ellipsis; }
.dest-badge { font-size: 11px; color: var(--accent-color); background: var(--accent-soft); padding: 3px 8px; border-left: 2px solid var(--signal-color); white-space: nowrap; }
.action-btns { display: flex; align-items: center; gap: 8px; flex-shrink: 0; }
.action-btn, .menu-toggle-btn { background: var(--paper-card); border: 1px solid var(--border-line); min-width: 36px; min-height: 36px; padding: 0 10px; border-radius: var(--radius-sm); cursor: pointer; display: inline-flex; gap: 6px; align-items: center; justify-content: center; color: var(--text-secondary); }
.action-btn:hover:not(:disabled) { background: var(--accent-soft); border-color: var(--accent-color); color: var(--accent-color); }
.graph-btn { color: var(--accent-color); border-color: var(--accent-color); }
@media (max-width: 1200px) { .dest-badge { display: none; } }
@media (max-width: 1024px) { .editor-header { padding: 10px 12px; }.breadcrumb .root, .breadcrumb .sep { display: none; } }
</style>
