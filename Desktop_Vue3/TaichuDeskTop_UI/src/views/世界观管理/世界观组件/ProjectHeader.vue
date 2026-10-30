<template>
  <header class="page-header surface-heading"><SectionTrace />
    <button class="back-btn" @click="$emit('back')">← 返回</button>

    <div class="header-main">
      <h1>
        <span>{{ project?.name || '世界观' }}</span>
        <button v-if="isOwner && project" class="rename-btn" title="修改名字和简介" aria-label="修改世界名字和简介" @click="$emit('settings')">✎</button>
      </h1>
      <div v-if="project" class="header-meta">
        <span>{{ project?.isPublic ? '公开' : '私有' }}</span>
        <span class="dot">·</span>
        <span>{{ cardCount }} 个条目</span>
        <span class="dot">·</span>
        <span v-if="(isPublicView || !isOwner) && project?.ownerName">
          作者 {{ project.ownerName }}
        </span>
        <span v-else>
          更新于 {{ formatTime(project?.updatedAt || '') }}
        </span>
        <template v-if="roleLabel || project?.collaboratorCount">
          <span class="dot">·</span>
          <button class="members-link" @click="$emit('members')">
            <template v-if="roleLabel">{{ roleLabel }}</template>
            <template v-else>{{ project?.collaboratorCount }} 位协作者</template>
          </button>
        </template>
      </div>
    </div>

    <div v-if="project" class="header-actions">
      <button v-if="canEdit" class="create-btn" @click="$emit('create')">＋ 新建条目</button>
      <details class="world-tools">
        <summary>更多操作</summary>
        <div class="tools-menu" @click="closeTools">
          <button @click="$emit('transfer', 'export')">导出 / 完整备份</button>
          <button v-if="isOwner" @click="$emit('transfer', 'import')">导入 / 恢复备份</button>
          <button v-if="canOrganize" @click="$emit('organize')">AI 整理</button>
          <button v-if="canEdit" @click="$emit('import')">AI 导入设定</button>
          <button v-if="isOwner || roleLabel" @click="$emit('members')">{{ isOwner ? '协作者' : '成员 / 退出协作' }}</button>
          <button v-if="isOwner || roleLabel" @click="$emit('history')">修改记录</button>
          <template v-if="isOwner">
            <button @click="$emit('settings')">重命名 / 简介 / 删除</button>
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
const props = defineProps<{ project: WorldProject | null; cardCount: number; isOwner: boolean; canEdit?: boolean; canOrganize?: boolean }>()
defineEmits<{ back: []; create: []; organize: []; import: []; transfer: [mode: 'import' | 'export']; settings: []; members: []; history: [] }>()
const store = useWorldStore()
const toggling = ref(false)
const isPublicView = computed(() => Boolean(props.project?.isPublic))
// 被拉进来协作时显示自己的身份
const roleLabel = computed(() => props.isOwner ? '' : props.project?.myRole === 'editor' ? '你是协作编写者' : props.project?.myRole === 'viewer' ? '你是只读成员' : '')

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
.header-main { flex: 1; min-width: 0; }h1 { margin: 0 0 8px; font-size: 22px; overflow-wrap: anywhere; display: flex; align-items: center; gap: 8px; }
.rename-btn { flex-shrink: 0; width: 28px; height: 28px; border: 1px solid transparent; background: none; color: var(--text-secondary); font-size: 14px; opacity: .55; }
.rename-btn:hover, .rename-btn:focus-visible { opacity: 1; border-color: var(--border-line); color: var(--accent-color); }
.members-link { border: 0; padding: 0; background: none; color: var(--accent-color); font-size: 12px; }
.members-link:hover { text-decoration: underline; text-underline-offset: 3px; }
.header-meta { display: flex; flex-wrap: wrap; gap: 7px; color: var(--text-secondary); font-size: 12px; }.header-desc { margin: 10px 0 0; color: var(--text-secondary); font-size: 13px; line-height: 1.7; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
.header-actions { display: flex; align-items: center; gap: 12px; flex-shrink: 0; }
.create-btn { padding: 9px 16px; border: 0; border-radius: 0; background: var(--accent-color); color: var(--on-accent); }
.world-tools { position: relative; }.world-tools summary { padding: 9px 6px; color: var(--text-secondary); font-size: 13px; }
.tools-menu { position: absolute; top: calc(100% + 8px); right: 0; width: 180px; z-index: 30; padding: 6px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); box-shadow: 0 10px 30px #0002; }.tools-menu button { display: block; width: 100%; padding: 12px; border: 0; border-radius: 0; text-align: left; background: none; color: var(--text-primary); }.tools-menu button:hover { background: var(--bg-sub); }.tools-menu button:disabled { opacity: .6; }
button:focus-visible,summary:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 3px; }
@media(max-width: 700px) { .page-header { flex-wrap: wrap; gap: 14px; padding: 20px; }.header-main { flex-basis: calc(100% - 80px); }.header-actions { width: 100%; justify-content: flex-end; } }
</style>
