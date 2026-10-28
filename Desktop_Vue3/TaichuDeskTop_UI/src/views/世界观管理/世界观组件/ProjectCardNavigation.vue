<template>
  <aside class="sidebar" :class="{ 'mobile-expanded': mobileExpanded }">
    <button class="overview-btn" :class="{ active: view === 'card' && !selectedCardId }" @click="$emit('overview')">世界概览</button>
    <button class="mobile-directory" :aria-expanded="mobileExpanded" @click="mobileExpanded = !mobileExpanded">{{ mobileExpanded ? '收起目录' : `目录 · ${cards.length} 个条目` }}</button>
    <div class="search">
      <input
        v-model="searchQuery"
        type="text"
        placeholder="查找这个世界的设定"
        aria-label="查找这个世界的设定"
      />
    </div>

    <div class="tree-toolbar">
      <span class="tree-total">{{ cards.length }} 个条目</span>
      <button class="toggle-all" :disabled="Boolean(searchQuery.trim())" @click="toggleAll">{{ allCollapsed ? '展开全部' : '收起全部' }}</button>
    </div>

    <nav class="tree" v-loading="loading">
      <div
        v-for="group in groupedCards"
        :key="group.type"
        class="tree-group"
      >
        <button
          class="tree-group-label"
          :aria-expanded="!isCollapsed(group.type)"
          @click="toggle(group.type)"
        >
          <span class="chevron">{{ isCollapsed(group.type) ? '▸' : '▾' }}</span>
          <span class="group-name">{{ group.label }}</span>
          <span class="group-count">{{ group.cards.length }}</span>
        </button>
        <div v-if="!isCollapsed(group.type)" class="tree-items">
          <button
            v-for="card in group.cards"
            :key="card.id"
            class="tree-item"
            :class="{ active: selectedCardId === card.id }"
            @click="mobileExpanded = false; $emit('select', card.id)"
          >
            {{ card.title }}
          </button>
        </div>
      </div>

      <div v-if="!groupedCards.length && !loading" class="tree-empty">
        {{ searchQuery ? '没有匹配的设定' : '还没有设定' }}
      </div>
    </nav>

    <div class="sidebar-footer">
      <button
        class="graph-btn"
        :class="{ active: view === 'timeline' }"
        @click="$emit('timeline')"
      >
        <span class="btn-icon">🕐</span> 时间线
      </button>
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
import { computed, ref, watch } from 'vue'
import type { CardSummary } from '@/api/types/world'
import { groupWorldCards } from '@/utils/worldPresentation'

const props = defineProps<{ cards: CardSummary[]; loading: boolean; selectedCardId: string | null; view: 'card' | 'graph' | 'timeline' }>()
const searchQuery = defineModel<string>('search', { default: '' })
defineEmits<{ select: [id: string]; graph: []; timeline: []; overview: [] }>()

const groupedCards = computed(() => groupWorldCards(props.cards, searchQuery.value))

const collapsed = ref<Set<string>>(new Set())
const mobileExpanded = ref(false)

watch(() => props.selectedCardId, (id) => {
  if (!id) return
  const card = props.cards.find(c => c.id === id)
  if (card) collapsed.value.delete(card.type)
})

const allCollapsed = computed(() =>
  groupedCards.value.length > 0 && groupedCards.value.every(g => collapsed.value.has(g.type))
)

function isCollapsed(type: string) { return !searchQuery.value.trim() && collapsed.value.has(type) }
function toggle(type: string) {
  const next = new Set(collapsed.value)
  next.has(type) ? next.delete(type) : next.add(type)
  collapsed.value = next
}
function toggleAll() {
  if (allCollapsed.value) {
    collapsed.value = new Set()
  } else {
    collapsed.value = new Set(props.cards.map(c => c.type))
  }
}
</script>

<style scoped>
* { box-sizing: border-box; }
.mobile-directory { display: none; }
.overview-btn { margin: 12px 12px 0; padding: 12px; text-align: left; border: 0; border-radius: 8px; background: transparent; color: var(--text-primary); cursor: pointer; font: inherit; }
.overview-btn.active { background: var(--accent-soft); color: var(--accent-color); }
.toggle-all:disabled { opacity: .5; cursor: default; }
.btn-icon { font-size: 16px; line-height: 1; }
.sidebar {
  width: 260px;
  flex-shrink: 0;
  background: var(--paper-card);
  border-right: 1px solid var(--border-line);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.search { padding: 16px 16px 8px; flex-shrink: 0; }
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
.search input:focus { outline: none; background: var(--bg-sub); }

.tree-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 4px 16px 8px;
  flex-shrink: 0;
}
.tree-total { font-size: 11px; color: var(--text-secondary); }
.toggle-all { border: none; background: none; padding: 0; font-size: 11px; color: var(--accent-color); cursor: pointer; }
.toggle-all:hover { text-decoration: underline; }

.tree { flex: 1; overflow-y: auto; padding: 4px 8px 16px; }
.tree-group { margin-bottom: 6px; }
.tree-group-label {
  display: flex;
  align-items: center;
  gap: 6px;
  width: 100%;
  padding: 7px 8px;
  border: none;
  background: none;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 600;
  color: var(--text-secondary);
  letter-spacing: 0.4px;
  cursor: pointer;
  text-align: left;
  user-select: none;
  transition: background 0.1s;
}
.tree-group-label:hover { background: var(--bg-sub); color: var(--text-primary); }
.chevron { width: 12px; font-size: 10px; flex-shrink: 0; }
.group-name { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.group-count {
  font-size: 11px;
  font-weight: 400;
  color: var(--text-secondary);
  background: var(--bg-sub);
  padding: 1px 7px;
  border-radius: 8px;
}
.tree-items { display: flex; flex-direction: column; padding: 2px 0 6px 20px; }
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
.tree-item:hover { background: var(--bg-sub); color: var(--text-primary); }
.tree-item.active { background: var(--bg-sub); color: var(--text-primary); font-weight: 500; }
.tree-empty { padding: 32px 16px; text-align: center; font-size: 13px; color: var(--text-secondary); }

.sidebar-footer { flex-shrink: 0; padding: 8px; border-top: 1px solid var(--border-line); }
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
.graph-btn:hover { background: var(--bg-sub); color: var(--text-primary); }
.graph-btn.active { background: var(--bg-sub); color: var(--text-primary); font-weight: 500; }

.tree::-webkit-scrollbar { width: 6px; }
.tree::-webkit-scrollbar-track { background: transparent; }
.tree::-webkit-scrollbar-thumb { background: var(--bg-sub); border-radius: 3px; }
.tree::-webkit-scrollbar-thumb:hover { background: var(--bg-sub); }

@media (max-width: 820px) {
  .sidebar {
    width: 100%;
    height: auto;
    display: grid;
    grid-template-columns: 1fr 1fr;
    border-right: none;
    border-bottom: 1px solid var(--border-line);
  }
  .mobile-directory { display: block; border: 0; background: none; color: var(--accent-color); cursor: pointer; font: inherit; font-size: 13px; padding: 12px; }
  .search, .tree-toolbar, .tree, .sidebar-footer { grid-column: 1 / -1; }
  .sidebar:not(.mobile-expanded) :is(.search, .tree-toolbar, .tree) { display: none; }
  .tree { max-height: 240px; }
  .sidebar-footer { display: flex; border-top: 0; }
}
</style>
