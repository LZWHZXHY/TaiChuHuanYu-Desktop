<template>
  <aside class="spirit-sidebar">
    <div class="sidebar-header">
      <span class="index-label">灵脉索引 / INDEX</span>
      <button class="new-note-btn" @click="$emit('create')">
        <span class="plus">+</span>
      </button>
    </div>

    <div class="sidebar-search">
      <input type="text" placeholder="搜索灵感碎片..." />
    </div>

    <nav class="note-list">
      <div 
        v-for="note in notes" 
        :key="note.id" 
        :class="['note-item', { active: activeId === note.id }]"
        @click="$emit('select', note.id)"
      >
        <div class="item-icon">
          <span v-if="note.isPublished" class="published-glow">🌐</span>
          <span v-else-if="activeId === note.id">✨</span>
          <span v-else>📄</span>
        </div>
        
        <div class="item-info">
          <div class="item-title">
            {{ note.title || '无标题碎片' }}
          </div>
          <div class="item-meta">
            {{ formatDate(note.updateAt) }}
            <span v-if="note.isPublished" class="status-tag">已发布</span>
          </div>
        </div>

        <div v-if="activeId === note.id" class="active-indicator"></div>
      </div>
    </nav>

    <div class="sidebar-footer">
      <div class="sync-status">
        <span class="pulse-dot"></span>
        已连接至太初灵脉
      </div>
    </div>
  </aside>
</template>

<script setup lang="ts">
import type { SpiritNote } from '../../../composables/useSpiritData';

defineProps<{
  notes: SpiritNote[];
  activeId: string;
}>();

defineEmits(['select', 'create']);

// 格式化时间
const formatDate = (timestamp: number) => {
  const date = new Date(timestamp);
  return `${date.getMonth() + 1}/${date.getDate()}`;
};
</script>

<style scoped>
.spirit-sidebar {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #fbfbfb;
  border-right: 1px solid #f2f2f2;
}

.sidebar-header {
  padding: 24px 20px 10px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.index-label {
  font-size: 11px;
  font-weight: 700;
  color: #86868b;
  letter-spacing: 0.1em;
}

.new-note-btn {
  background: #1d1d1f;
  color: white;
  border: none;
  width: 24px;
  height: 24px;
  border-radius: 6px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
}
.new-note-btn:hover { transform: scale(1.1); background: #000; }

.sidebar-search {
  padding: 10px 20px;
}
.sidebar-search input {
  width: 100%;
  background: #efeff0;
  border: none;
  padding: 8px 12px;
  border-radius: 8px;
  font-size: 13px;
  outline: none;
}

.note-list {
  flex: 1;
  overflow-y: auto;
  padding: 10px;
}

.note-item {
  display: flex;
  align-items: center;
  padding: 12px;
  margin-bottom: 4px;
  border-radius: 10px;
  cursor: pointer;
  position: relative;
  transition: all 0.2s ease;
}

.note-item:hover { background: #f0f0f2; }
.note-item.active { background: #ffffff; box-shadow: 0 4px 12px rgba(0,0,0,0.05); }

.item-icon {
  width: 32px;
  font-size: 16px;
  display: flex;
  align-items: center;
}

/* 🌟 发布图标的微光效果 */
.published-glow {
  font-size: 14px;
  filter: drop-shadow(0 0 2px rgba(0, 102, 204, 0.4));
}

.item-info {
  flex: 1;
  min-width: 0;
}

.item-title {
  font-size: 14px;
  font-weight: 500;
  color: #1d1d1f;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.item-meta {
  font-size: 11px;
  color: #86868b;
  margin-top: 2px;
  display: flex;
  align-items: center;
}

/* 🌟 发布状态小标签 */
.status-tag {
  margin-left: 8px;
  color: #0066cc;
  font-weight: 600;
  font-size: 10px;
  background: rgba(0, 102, 204, 0.05);
  padding: 1px 4px;
  border-radius: 3px;
}

.active-indicator {
  position: absolute;
  left: 0;
  width: 3px;
  height: 16px;
  background: #0066cc;
  border-radius: 0 4px 4px 0;
}

.sidebar-footer {
  padding: 16px 20px;
  border-top: 1px solid #f2f2f2;
}

.sync-status {
  font-size: 11px;
  color: #86868b;
  display: flex;
  align-items: center;
  gap: 6px;
}

.pulse-dot {
  width: 6px;
  height: 6px;
  background: #34c759;
  border-radius: 50%;
  animation: pulse 2s infinite;
}

@keyframes pulse {
  0% { opacity: 1; }
  50% { opacity: 0.4; }
  100% { opacity: 1; }
}
</style>