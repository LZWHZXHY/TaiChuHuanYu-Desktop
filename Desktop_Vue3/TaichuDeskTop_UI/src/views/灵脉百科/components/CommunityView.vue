<template>
  <div class="community-list">
    <div class="list-header" v-if="entries.length > 0">
      <div class="col-ref">索引序列</div>
      <div class="col-main">提案主题</div>
      <div class="col-action"></div>
    </div>

    <div 
      v-for="entry in entries" 
      :key="entry.id" 
      class="list-row"
      @click="$emit('go-detail', entry.id)"
    >
      <div class="col-ref">
        <span class="ref-badge">{{ String(entry.id).substring(0, 6).toUpperCase() }}</span>
      </div>
      
      <div class="col-main">
        <h3 class="row-title">{{ entry.title }}</h3>
        <div class="row-excerpt">
          <SpiritPreview :modelValue="parseJson(entry.excerpt)" />
        </div>
      </div>
      
      <div class="col-action">
        <button class="read-btn">
          <span>阅读</span>
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M5 12h14M12 5l7 7-7 7"/></svg>
        </button>
      </div>
    </div>

    <div v-if="entries.length === 0" class="empty-state">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M4 19.5v-15A2.5 2.5 0 0 1 6.5 2H20v20H6.5a2.5 2.5 0 0 1 0-5H20"/></svg>
      <p>该节点或其下辖子域暂无任何公开文档</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import SpiritPreview from '@/components/SpiritTextComponents/SpiritPreview.vue';

defineProps<{ entries: any[] }>();
defineEmits(['go-detail']);

const parseJson = (content: any) => {
  if (!content) return '';
  if (typeof content === 'string') {
    try { return JSON.parse(content); } catch { return content; }
  }
  return content;
};
</script>

<style scoped>
.community-list {
  display: flex;
  flex-direction: column;
}

.list-header {
  display: flex;
  padding: 0 16px 12px;
  border-bottom: 2px solid var(--c-text-main);
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--c-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.col-ref { width: 120px; flex-shrink: 0; }
.col-main { flex: 1; min-width: 0; padding-right: 24px; }
.col-action { width: 100px; display: flex; justify-content: flex-end; align-items: center; }

.list-row {
  display: flex;
  padding: 24px 16px;
  border-bottom: 1px solid var(--c-border);
  cursor: pointer;
  transition: all 0.2s ease;
  align-items: center;
}

.list-row:hover {
  background: #fcfcfd;
  box-shadow: inset 4px 0 0 0 var(--c-text-main);
}

.ref-badge {
  font-family: var(--font-mono);
  font-size: 0.8rem;
  color: var(--c-text-light);
  background: #f4f4f5;
  padding: 4px 8px;
  border-radius: 4px;
}

.row-title {
  font-size: 1.1rem;
  font-weight: 600;
  margin: 0 0 6px;
  color: var(--c-text-main);
}

.row-excerpt {
  font-size: 0.9rem;
  color: var(--c-text-muted);
  max-height: 22px; /* 单行截断 */
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
}
/* 覆盖内部组件的 p 标签 */
.row-excerpt :deep(p) { margin: 0; text-overflow: ellipsis; overflow: hidden; }

.read-btn {
  background: transparent;
  border: none;
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 0.85rem;
  font-weight: 500;
  color: var(--c-text-light);
  cursor: pointer;
  transition: color 0.2s;
}

.read-btn svg { width: 14px; height: 14px; transition: transform 0.2s; }
.list-row:hover .read-btn { color: var(--c-brand); }
.list-row:hover .read-btn svg { transform: translateX(4px); }

.empty-state { text-align: center; padding: 100px 0; color: var(--c-text-muted); }
.empty-state svg { width: 48px; height: 48px; margin-bottom: 16px; color: var(--c-border-hover); }
</style>