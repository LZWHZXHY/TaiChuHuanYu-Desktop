<template>
  <header class="editor-header studio-heading">
    <div class="header-left">
      <button v-if="isMobile" class="menu-toggle-btn" aria-label="打开灵脉目录" @click="$emit('toggle-sidebar')">
        <span class="icon">☰</span>
      </button>
      
      <div class="breadcrumb">
        <span class="root">我的灵脉</span>
        <span class="sep">/</span>
        <span class="current">{{ activeNote?.title || '未命名笔记' }}</span>
        <span v-if="destText" class="dest-badge" :title="'发布后会出现在这里'">{{ activeNote?.isPublic ? '已发布到' : '发布到' }} {{ destText }}</span>
      </div>
    </div>
    
    <div class="action-btns">
      <button class="action-btn graph-btn" @click="$emit('open-graph')" title="查看笔记之间的引用关系" aria-label="关系图">
        <span class="icon" aria-hidden="true">🕸️</span><span v-if="!isMobile" class="btn-label">关系图</span>
      </button>

      <button class="action-btn" @click="$emit('open-settings')" title="笔记设置：空间、类型、发布" aria-label="笔记设置" :disabled="!activeNote">
        <span class="icon" aria-hidden="true">⚙</span><span v-if="!isMobile" class="btn-label">设置</span>
      </button>
    </div>
  </header>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { noteKind } from '@/utils/NoteType'
import { boardOf } from '@/utils/forumBoards'

const props = defineProps<{
  isMobile: boolean;
  activeNote: any;
}>();

defineEmits([
  'toggle-sidebar', 
  'open-graph', 
  'open-settings'
]);

// 类型 → 发布后出现的板块（顶部常驻提示），名称统一取自 NoteType。
const destText = computed(() => {
  const dest = props.activeNote?.type ? noteKind(props.activeNote.type).destination || '' : '';
  const { board, sub } = boardOf(props.activeNote?.tags || []);
  return dest && board ? `${dest} · ${board.tag}${sub ? ' · ' + sub.tag : ''}` : dest;
});
</script>

<style scoped>
/* 按钮文字不允许逐字换行；空间不足时整组按钮换到下一行，而不是被挤成竖排。 */
.editor-header { min-height: 60px; padding: 10px 20px; display: flex; flex-wrap: wrap; gap: 8px 12px; justify-content: space-between; align-items: center; border-bottom: 1px solid var(--border-line); background: var(--paper-card); flex-shrink: 0; }
.header-left { display: flex; align-items: center; flex: 1 1 240px; min-width: 0; gap: 10px; }
.breadcrumb { font-size: 13px; color: var(--text-secondary); display: flex; align-items: center; gap: 8px; min-width: 0; white-space: nowrap; }
.breadcrumb .current { color: var(--text-primary); font-weight: 600; overflow: hidden; text-overflow: ellipsis; }
.dest-badge { font-size: 11px; color: var(--accent-color); background: var(--accent-soft); padding: 3px 8px; border-left: 2px solid var(--signal-color); white-space: nowrap; }
.action-btns { display: flex; align-items: center; gap: 8px; flex: 0 0 auto; margin-left: auto; }
.action-btn, .menu-toggle-btn { flex: 0 0 auto; width: auto; height: auto; background: var(--paper-card); border: 1px solid var(--border-line); min-width: 36px; min-height: 36px; padding: 0 12px; border-radius: 0; cursor: pointer; display: inline-flex; gap: 6px; align-items: center; justify-content: center; color: var(--text-secondary); font-size: 13px; line-height: 1; white-space: nowrap; word-break: keep-all; overflow-wrap: normal; writing-mode: horizontal-tb; }
.action-btn .icon { font-size: 14px; }
.action-btn:hover:not(:disabled) { background: var(--accent-soft); border-color: var(--accent-color); color: var(--accent-color); }
.graph-btn { color: var(--accent-color); border-color: var(--accent-color); }
@media (max-width: 1200px) { .dest-badge { display: none; } }
@media (max-width: 1024px) { .editor-header { padding: 10px 12px; }.breadcrumb .root, .breadcrumb .sep { display: none; } }
</style>
