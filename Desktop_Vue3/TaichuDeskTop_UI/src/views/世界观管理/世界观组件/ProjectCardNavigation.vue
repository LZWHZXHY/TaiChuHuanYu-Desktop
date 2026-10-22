<template>
  <aside class="sidebar">
    <div class="search">
      <input
        v-model="searchQuery"
        type="text"
        placeholder="搜索..."
      />
    </div>

    <nav class="tree" v-loading="loading">
      <div
        v-for="group in groupedCards"
        :key="group.type"
        class="tree-group"
      >
        <div class="tree-group-label">
          <span>{{ group.label }}</span>
          <span class="group-count">{{ group.cards.length }}</span>
        </div>
        <div class="tree-items">
          <button
            v-for="card in group.cards"
            :key="card.id"
            class="tree-item"
            :class="{ active: selectedCardId === card.id }"
            @click="$emit('select', card.id)"
          >
            {{ card.title }}
          </button>
        </div>
      </div>

      <div v-if="!groupedCards.length && !loading" class="tree-empty">
        {{ searchQuery ? '没有匹配的卡片' : '还没有卡片' }}
      </div>
    </nav>

    <div class="sidebar-footer">
      <button
        class="graph-btn"
        :class="{ active: view === 'graph' }"
        @click="$emit('graph')"
      >
        <span class="btn-icon">🕸️</span> 关系图谱
      </button>
    </div>
  </aside>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { CardSummary } from '@/api/types/world'
import { groupWorldCards } from '@/utils/worldPresentation'
const props = defineProps<{ cards: CardSummary[]; loading: boolean; selectedCardId: string | null; view: 'card' | 'graph' }>()
const searchQuery = defineModel<string>('search', { default: '' })
defineEmits<{ select: [id: string]; graph: [] }>()
const groupedCards = computed(() => groupWorldCards(props.cards, searchQuery.value))
</script>

<style scoped>
* { box-sizing: border-box; }
.btn-icon {
  font-size: 16px;
  line-height: 1;
}
.sidebar {
  width: 260px;
  flex-shrink: 0;
  background: var(--paper-card);
  border-right: 1px solid var(--border-line);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.search {
  padding: 16px 16px 8px;
  flex-shrink: 0;
}
.search input {
  width: 100%;
  border: none;
  background: var(--bg-sub);
  border-radius: 6px;
  padding: 7px 12px;
  font-size: 13px;
  color: var(--text-primary);
  transition: background 0.15s;
}
.search input::placeholder { color: var(--text-secondary); }
.search input:focus {
  outline: none;
  background: var(--bg-sub);
}
.tree {
  flex: 1;
  overflow-y: auto;
  padding: 8px 8px 16px;
}
.tree-group {
  margin-bottom: 20px;
}
.tree-group-label {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 4px 8px 6px;
  font-size: 11px;
  font-weight: 600;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  user-select: none;
}
.group-count {
  font-weight: 400;
  color: var(--text-secondary);
}
.tree-items {
  display: flex;
  flex-direction: column;
}
.tree-item {
  display: block;
  width: 100%;
  text-align: left;
  padding: 5px 8px;
  font-size: 13px;
  color: var(--text-secondary);
  background: none;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: background 0.1s, color 0.1s;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  line-height: 1.5;
}
.tree-item:hover {
  background: var(--bg-sub);
  color: var(--text-primary);
}
.tree-item.active {
  background: var(--bg-sub);
  color: var(--text-primary);
  font-weight: 500;
}
.tree-empty {
  padding: 32px 16px;
  text-align: center;
  font-size: 13px;
  color: var(--text-secondary);
}
.sidebar-footer {
  flex-shrink: 0;
  padding: 8px;
  border-top: 1px solid var(--border-line);
}
.graph-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  width: 100%;
  padding: 8px 10px;
  font-size: 13px;
  color: var(--text-secondary);
  background: none;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: background 0.1s, color 0.1s;
  text-align: left;
}
.graph-btn:hover {
  background: var(--bg-sub);
  color: var(--text-primary);
}
.graph-btn.active {
  background: var(--bg-sub);
  color: var(--text-primary);
  font-weight: 500;
}
.tree::-webkit-scrollbar {
  width: 6px;
}
.tree::-webkit-scrollbar-track {
  background: transparent;
}
.tree::-webkit-scrollbar-thumb {
  background: var(--bg-sub);
  border-radius: 3px;
}
.tree::-webkit-scrollbar-thumb:hover {
  background: var(--bg-sub);
}
@media (max-width: 820px) {
  .sidebar {
    width: 100%;
    height: 200px;
    border-right: none;
    border-bottom: 1px solid var(--border-line);
  }
}
</style>
