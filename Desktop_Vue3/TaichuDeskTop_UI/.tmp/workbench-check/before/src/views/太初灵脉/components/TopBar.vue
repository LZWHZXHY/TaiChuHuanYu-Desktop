<template>
  <header class="editor-header">
    <div class="header-left">
      <button v-if="isMobile" class="menu-toggle-btn" @click="$emit('toggle-sidebar')">
        <span class="icon">☰</span>
      </button>
      
      <div class="breadcrumb" v-else>
        <span class="root">我的灵脉</span>
        <span class="sep">/</span>
        <span class="current">{{ activeNote?.title || '未命名碎片' }}</span>
        <span v-if="destText" class="dest-badge" :title="'发布后会出现在这里'">发布 → {{ destText }}</span>
      </div>
    </div>
    
    <div class="action-btns">
      <button class="action-btn graph-btn" @click="$emit('open-graph')" title="全屏网状星图">
        <span class="icon">🕸️</span> <span v-if="!isMobile">全屏图谱</span>
      </button>

      <button class="action-btn" @click="$emit('open-settings')" title="碎片设定" :disabled="!activeNote">
        <span class="icon">⚙️</span>
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
.editor-header {
  height: 60px; padding: 0 clamp(12px, 2vw, 32px); display: flex; gap: 12px; justify-content: space-between; align-items: center;
  border-bottom: 1px solid var(--border-line); background: var(--paper-card); flex-shrink: 0;
}

.header-left { display: flex; align-items: center; min-width: 0; }
.breadcrumb { font-size: 13px; color: var(--text-secondary); display: flex; gap: 8px; min-width: 0; white-space: nowrap; }
.breadcrumb .current { color: var(--text-primary); font-weight: 500; overflow: hidden; text-overflow: ellipsis; }
.dest-badge { font-size: 11px; color: var(--accent-color); background: var(--accent-soft); padding: 2px 8px; border-radius: 10px; white-space: nowrap; flex-shrink: 0; }

.action-btns { display: flex; align-items: center; gap: 12px; flex-shrink: 0; }

.action-btn { background: none; border: 1px solid var(--border-line); width: 32px; height: 32px; border-radius: 50%; cursor: pointer; display: flex; align-items: center; justify-content: center; transition: all 0.2s; color: var(--text-secondary); }
.action-btn:hover:not(:disabled) { background: var(--bg-sub); border-color: var(--border-line); color: var(--text-primary); }

.graph-btn { background: rgba(0, 102, 204, 0.05); border: 1px solid var(--accent-color); color: var(--accent-color); width: auto; padding: 0 14px; border-radius: 40px; font-size: 13px; font-weight: 600; }
.graph-btn:hover { background: rgba(0, 102, 204, 0.1); }

.menu-toggle-btn { background: none; border: none; font-size: 20px; cursor: pointer; color: var(--text-primary); }

@media (max-width: 1024px) {
  .editor-header { padding: 0 16px; }
  .action-btns { gap: 8px; }
}
</style>
