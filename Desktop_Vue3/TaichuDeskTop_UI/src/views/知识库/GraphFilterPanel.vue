<!-- src/components/KnowledgeGraph/GraphFilterPanel.vue -->
<template>
  <div class="filter-panel">
    <div class="panel-header">
      <span class="main-label">视界过滤器</span>
      <span class="sub-label">DIMENSION FILTER</span>
    </div>
    
    <div class="filter-list custom-scrollbar">
      <button
        v-for="type in availableTypes"
        :key="type"
        class="filter-btn"
        :class="{ active: activeTypes.includes(type) }"
        @click="toggleType(type)"
        :style="{ '--theme-color': getTheme(type).color, '--theme-bg': hexToRgba(getTheme(type).color, 0.1) }"
      >
        <span class="indicator"></span>
        <span class="type-name">{{ getTheme(type).typeLabel }}</span>
      </button>
    </div>

    <div class="filter-actions">
      <button class="action-btn" @click="$emit('update:activeTypes', [])">重置</button>
      <button class="action-btn highlight" @click="selectAll">全选</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useKnowledgeGraphData } from './useKnowledgeGraphData'

const props = defineProps<{
  availableTypes: string[];
  activeTypes: string[];
}>()

const emit = defineEmits<{
  'update:activeTypes': [types: string[]];
}>()

const { getTheme, hexToRgba } = useKnowledgeGraphData()

const toggleType = (type: string) => {
  const newTypes = [...props.activeTypes]
  const index = newTypes.indexOf(type)
  if (index === -1) {
    newTypes.push(type)
  } else {
    newTypes.splice(index, 1)
  }
  emit('update:activeTypes', newTypes)
}

const selectAll = () => {
  emit('update:activeTypes', [...props.availableTypes])
}
</script>

<style scoped>
.filter-panel {
  position: absolute;
  bottom: 32px;
  left: 32px;
  z-index: 10;
  width: 240px;
  background: linear-gradient(135deg, rgba(15, 23, 42, 0.75) 0%, rgba(2, 6, 23, 0.85) 100%);
  border: 1px solid rgba(56, 189, 248, 0.15);
  border-radius: 12px;
  backdrop-filter: blur(16px);
  -webkit-backdrop-filter: blur(16px);
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.panel-header {
  padding: 14px 18px;
  border-bottom: 1px solid rgba(56, 189, 248, 0.2);
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.main-label { font-size: 13px; color: #e2e8f0; font-weight: 500; letter-spacing: 1px; }
.sub-label { font-size: 9px; color: #64748b; letter-spacing: 1.5px; }

.filter-list {
  padding: 12px;
  max-height: 300px;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.filter-btn {
  display: flex;
  align-items: center;
  gap: 12px;
  width: 100%;
  padding: 8px 12px;
  background: transparent;
  border: 1px solid transparent;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
  text-align: left;
}
.filter-btn:hover {
  background: rgba(255, 255, 255, 0.05);
}
.filter-btn.active {
  background: var(--theme-bg);
  border-color: rgba(255, 255, 255, 0.1);
}

.indicator {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  border: 1px solid var(--theme-color);
  transition: all 0.2s ease;
}
.filter-btn.active .indicator {
  background: var(--theme-color);
  box-shadow: 0 0 8px var(--theme-color);
}

.type-name {
  color: #94a3b8;
  font-size: 11px;
  letter-spacing: 1px;
  transition: color 0.2s ease;
}
.filter-btn.active .type-name {
  color: #f8fafc;
  font-weight: 500;
}

.filter-actions {
  display: flex;
  padding: 10px;
  gap: 8px;
  border-top: 1px solid rgba(148, 163, 184, 0.1);
}
.action-btn {
  flex: 1;
  padding: 6px 0;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid transparent;
  color: #94a3b8;
  font-size: 11px;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
}
.action-btn:hover { background: rgba(255, 255, 255, 0.1); color: #e2e8f0; }
.action-btn.highlight { color: #38bdf8; border-color: rgba(56, 189, 248, 0.3); }
.action-btn.highlight:hover { background: rgba(56, 189, 248, 0.1); }

.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: rgba(56, 189, 248, 0.3); border-radius: 2px; }
</style>