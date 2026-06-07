<template>
  <header class="editor-header">
    <div class="header-left">
      <button v-if="isMobile" class="menu-toggle-btn" @click="$emit('toggle-sidebar')">
        <span class="icon">☰</span>
      </button>
      
      <div class="breadcrumb" v-else>
        <span class="root">{{ isWikiMode ? 'Wiki 修订工作台' : '我的灵脉' }}</span>
        <span class="sep">/</span>
        <span class="current">{{ isWikiMode ? (wikiEditData?.title || '编辑 Wiki') : (activeNote?.title || '未命名碎片') }}</span>
      </div>
    </div>
    
    <div class="action-btns">
      <button v-if="!isWikiMode" class="action-btn graph-btn" @click="$emit('open-graph')" title="全屏网状星图">
        <span class="icon">🕸️</span> <span v-if="!isMobile">全屏图谱</span>
      </button>

      <button class="action-btn" @click="$emit('open-settings')" title="碎片设定" :disabled="!activeNote && !isWikiMode">
        <span class="icon">⚙️</span>
      </button>

      <button class="wiki-access-btn" :class="{ 'is-active': isWikiMode }" @click="isWikiMode ? $emit('exit-wiki') : $emit('import-wiki')">
        <span class="icon">{{ isWikiMode ? '✕' : '🌌' }}</span>
        {{ isWikiMode ? '退出 Wiki' : '接入百科' }}
      </button>
    </div>
  </header>
</template>

<script setup lang="ts">
defineProps<{
  isMobile: boolean;
  isWikiMode: boolean;
  activeNote: any;
  wikiEditData: any;
}>();

defineEmits([
  'toggle-sidebar', 
  'open-graph', 
  'open-settings', 
  'import-wiki', 
  'exit-wiki'
]);
</script>

<style scoped>
.editor-header {
  height: 60px; padding: 0 40px; display: flex; justify-content: space-between; align-items: center;
  border-bottom: 1px solid #f2f2f2; background: #ffffff; flex-shrink: 0;
}

.header-left { display: flex; align-items: center; }
.breadcrumb { font-size: 13px; color: #86868b; display: flex; gap: 8px; }
.breadcrumb .current { color: #1d1d1f; font-weight: 500; }

.action-btns { display: flex; align-items: center; gap: 12px; flex-shrink: 0; }

.action-btn { background: none; border: 1px solid #d2d2d7; width: 32px; height: 32px; border-radius: 50%; cursor: pointer; display: flex; align-items: center; justify-content: center; transition: all 0.2s; color: #86868b; }
.action-btn:hover:not(:disabled) { background: #f5f5f7; border-color: #1d1d1f; color: #1d1d1f; }

.graph-btn { background: rgba(0, 102, 204, 0.05); border: 1px solid #0066cc; color: #0066cc; width: auto; padding: 0 14px; border-radius: 40px; font-size: 13px; font-weight: 600; }
.graph-btn:hover { background: rgba(0, 102, 204, 0.1); }

.wiki-access-btn { background: #fff; border: 1px solid #d2d2d7; color: #1d1d1f; padding: 0 14px; height: 32px; border-radius: 40px; font-size: 13px; font-weight: 500; cursor: pointer; display: flex; align-items: center; gap: 6px; }
.wiki-access-btn.is-active { border-color: #ff3b30; color: #ff3b30; background: rgba(255, 59, 48, 0.05); }

.menu-toggle-btn { background: none; border: none; font-size: 20px; cursor: pointer; color: #1d1d1f; }

@media (max-width: 1024px) {
  .editor-header { padding: 0 16px; }
  .action-btns { gap: 8px; }
}
</style>