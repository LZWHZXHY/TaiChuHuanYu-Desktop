<template>
  <header class="page-header">
    <button class="back-btn" @click="$emit('back')">← 返回</button>

    <div class="header-main">
      <h1>{{ project?.name || '加载中...' }}</h1>
      <div class="header-meta">
        <span>{{ project?.isPublic ? '公开' : '私有' }}</span>
        <span class="dot">·</span>
        <span>{{ cardCount }} 个条目</span>
        <span class="dot">·</span>
        <span v-if="isPublicView && project?.ownerName">
          作者 {{ project.ownerName }}
        </span>
        <span v-else>
          更新于 {{ formatTime(project?.updatedAt || '') }}
        </span>
      </div>
      <p v-if="project?.description" class="header-desc">
        {{ project.description }}
      </p>
    </div>

    <div v-if="isOwner" class="header-actions">
      <button class="btn-ghost" @click="$emit('create')">
        <span class="btn-icon">+</span> 新建
      </button>
      <button
        class="visibility-btn"
        :class="{ 'is-public': project?.isPublic }"
        :disabled="toggling"
        :title="project?.isPublic ? '切换为私有' : '切换为公开'"
        @click="togglePublic"
      >
        {{ toggling ? '…' : project?.isPublic ? '🔓 公开' : '🔒 私有' }}
      </button>
    </div>
  </header>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import type { WorldProject } from '@/api/types/world'
import { useWorldStore } from '@/stores/world'
import { formatWorldUpdatedAt as formatTime } from '@/utils/worldPresentation'
const props = defineProps<{ project: WorldProject | null; cardCount: number; isOwner: boolean }>()
defineEmits<{ back: []; create: [] }>()
const store = useWorldStore()
const toggling = ref(false)
const isPublicView = computed(() => Boolean(props.project?.isPublic))

async function togglePublic() {
  if (!props.project || toggling.value) return
  toggling.value = true
  try {
    await store.updateProject(props.project.id, { isPublic: !props.project.isPublic })
  } finally {
    toggling.value = false
  }
}
</script>

<style scoped>
* { box-sizing: border-box; }
.page-header {
  flex-shrink: 0;
  padding: 20px 40px 16px;
  background: var(--paper-card);
  border-bottom: 1px solid var(--border-line);
  display: flex;
  align-items: flex-start;
  gap: 24px;
  position: relative;
}
.back-btn {
  background: none;
  border: none;
  color: var(--text-secondary);
  font-size: 13px;
  cursor: pointer;
  padding: 4px 0;
  flex-shrink: 0;
  margin-top: 4px;
}
.back-btn:hover { color: var(--text-primary); }
.header-main {
  flex: 1;
  min-width: 0;
}
.header-main h1 {
  font-size: 22px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 6px;
  letter-spacing: -0.3px;
}
.header-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: var(--text-secondary);
  margin-bottom: 8px;
}
.header-meta .dot { color: var(--text-secondary); }
.header-desc {
  margin: 0;
  font-size: 14px;
  line-height: 1.6;
  color: var(--text-secondary);
  max-width: 720px;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
.header-actions {
  flex-shrink: 0;
  margin-top: 4px;
}
.btn-ghost {
  background: none;
  border: none;
  color: var(--text-secondary);
  font-size: 14px;
  cursor: pointer;
  padding: 6px 12px;
  border-radius: 6px;
  transition: background 0.1s;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}
.btn-ghost:hover {
  background: var(--bg-sub);
  color: var(--text-primary);
}
.btn-icon {
  font-size: 16px;
  line-height: 1;
}
.visibility-btn {
  background: none;
  border: 1px solid var(--border-line);
  color: var(--text-secondary);
  font-size: 13px;
  cursor: pointer;
  padding: 5px 12px;
  border-radius: 6px;
  transition: background 0.15s, color 0.15s, border-color 0.15s;
  white-space: nowrap;
}
.visibility-btn:hover:not(:disabled) {
  background: var(--bg-sub);
  color: var(--text-primary);
}
.visibility-btn.is-public {
  color: var(--success);
  border-color: color-mix(in srgb, var(--success) 40%, transparent);
}
.visibility-btn:disabled {
  opacity: 0.5;
  cursor: default;
}
@media (max-width: 820px) {
  .page-header {
    padding: 16px 20px;
    flex-wrap: wrap;
  }
  .header-main h1 { font-size: 18px; }
}
</style>
