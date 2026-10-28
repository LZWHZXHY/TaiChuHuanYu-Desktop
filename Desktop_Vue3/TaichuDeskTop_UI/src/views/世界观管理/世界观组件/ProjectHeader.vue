<template>
  <header class="page-header surface-heading"><SectionTrace />
    <button class="back-btn" @click="$emit('back')">← 返回</button>

    <div class="header-main">
      <h1>{{ project?.name || '世界观' }}</h1>
      <div v-if="project" class="header-meta">
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
    </div>

    <div v-if="project" class="header-actions">
      <button v-if="isOwner" class="create-btn" @click="$emit('create')">＋ 新建条目</button>
      <details class="world-tools">
        <summary>更多操作</summary>
        <div class="tools-menu" @click="closeTools">
          <button @click="$emit('transfer', 'export')">导出 / 完整备份</button>
          <button v-if="isOwner" @click="$emit('transfer', 'import')">导入 / 恢复备份</button>
          <button v-if="canOrganize" @click="$emit('organize')">AI 整理</button>
          <template v-if="isOwner">
            <button @click="$emit('import')">AI 导入设定</button>
            <button class="visibility-btn" :disabled="toggling" @click="togglePublic">{{ toggling ? '保存中…' : project?.isPublic ? '改为私有世界' : '公开这个世界' }}</button>
          </template>
        </div>
      </details>
    </div>
  </header>
</template>
<script setup lang="ts">
import SectionTrace from '@/components/SectionTrace.vue'
import { computed, ref } from 'vue'
import { ElMessage } from 'element-plus'
import type { WorldProject } from '@/api/types/world'
import { useWorldStore } from '@/stores/world'
import { formatWorldUpdatedAt as formatTime } from '@/utils/worldPresentation'
const props = defineProps<{ project: WorldProject | null; cardCount: number; isOwner: boolean; canOrganize?: boolean }>()
defineEmits<{ back: []; create: []; organize: []; import: []; transfer: [mode: 'import' | 'export'] }>()
const store = useWorldStore()
const toggling = ref(false)
const isPublicView = computed(() => Boolean(props.project?.isPublic))

function closeTools(event: MouseEvent) { (event.currentTarget as HTMLElement).closest('details')?.removeAttribute('open') }

async function togglePublic() {
  if (!props.project || toggling.value) return
  toggling.value = true
  try {
    await store.updateProject(props.project.id, { isPublic: !props.project.isPublic })
  } catch {
    ElMessage.error('可见性修改失败，请重试')
  } finally {
    toggling.value = false
  }
}
</script>

<style scoped>
.page-header { display: flex; align-items: flex-start; gap: 24px;  }
button, summary { font: inherit; cursor: pointer; }
.back-btn { border: 0; background: none; color: var(--text-secondary); padding: 6px 0; white-space: nowrap; }
.header-main { flex: 1; min-width: 0; }h1 { margin: 0 0 8px; font-size: 22px; overflow-wrap: anywhere; }
.header-meta { display: flex; flex-wrap: wrap; gap: 7px; color: var(--text-secondary); font-size: 12px; }.header-desc { margin: 10px 0 0; color: var(--text-secondary); font-size: 13px; line-height: 1.7; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
.header-actions { display: flex; align-items: center; gap: 12px; flex-shrink: 0; }
.create-btn { padding: 9px 16px; border: 0; border-radius: var(--radius-sm); background: var(--accent-color); color: var(--on-accent); }
.world-tools { position: relative; }.world-tools summary { padding: 9px 6px; color: var(--text-secondary); font-size: 13px; }
.tools-menu { position: absolute; top: calc(100% + 8px); right: 0; width: 180px; z-index: 30; padding: 6px; border: 1px solid var(--border-line); border-radius: var(--radius-sm); background: var(--paper-card); box-shadow: 0 10px 30px #0002; }.tools-menu button { display: block; width: 100%; padding: 12px; border: 0; border-radius: var(--radius-sm); text-align: left; background: none; color: var(--text-primary); }.tools-menu button:hover { background: var(--bg-sub); }.tools-menu button:disabled { opacity: .6; }
button:focus-visible,summary:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 3px; }
@media(max-width: 700px) { .page-header { flex-wrap: wrap; gap: 14px; padding: 20px; }.header-main { flex-basis: calc(100% - 80px); }.header-actions { width: 100%; justify-content: flex-end; } }
</style>
