<!-- CardRefEmbed.vue：正文里「显示全部内容」的引用（!{CARD:id}）：把那个条目整个展开在这里。
     套娃太深（超过 3 层）或者绕回自己时，只显示一个可以点开的小卡片。 -->
<template>
  <div class="ref-embed" :class="{ compact: tooDeep }">
    <div class="re-bar">
      <span class="re-icon" aria-hidden="true">⧉</span>
      <button type="button" class="re-title" :title="`在右边查看《${title}》`" @click="$emit('peek', cardId)">{{ title }}</button>
      <span v-if="summary" class="re-type">{{ getTypeLabel(summary.type) }}</span>
      <span class="re-actions">
        <button type="button" title="在右边查看" @click="$emit('peek', cardId)">在右侧查看</button>
        <button type="button" title="打开这个条目的页面" @click="$emit('select', cardId)">打开 →</button>
      </span>
    </div>
    <p v-if="tooDeep" class="re-note">{{ loop ? '这里又引用回了上层条目，点上面的名字查看。' : '引用层数太多了，点上面的名字查看全部内容。' }}</p>
    <p v-else-if="failed" class="re-note">这个条目不存在、已删除，或者你没有权限查看。</p>
    <p v-else-if="!detail" class="re-note">正在读取…</p>
    <ProjectCardDocument v-else :card="detail" :project-id="projectId" mode="embed" :trail="[...trail, cardId]"
      @peek="id => $emit('peek', id)" @select="id => $emit('select', id)" />
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { CardDetail } from '@/api/types/world'
import { useWorldStore } from '@/stores/world'
import { getCardTypeLabel as getTypeLabel } from '@/utils/worldPresentation'
import ProjectCardDocument from './ProjectCardDocument.vue'

const props = defineProps<{ cardId: string; projectId: string; trail: string[] }>()
defineEmits<{ peek: [id: string]; select: [id: string] }>()
const store = useWorldStore()
const MAX_DEPTH = 3
const loop = computed(() => props.trail.includes(props.cardId))
const tooDeep = computed(() => loop.value || props.trail.length >= MAX_DEPTH)
const summary = computed(() => store.getCardById(props.cardId, props.projectId))
const detail = ref<CardDetail | null>(null)
const failed = ref(false)
const title = computed(() => detail.value?.title || summary.value?.title || '引用的条目')

let version = 0
watch(() => [props.projectId, props.cardId, tooDeep.value] as const, async ([pid, id, deep]) => {
  const v = ++version
  failed.value = false
  detail.value = deep ? null : store.getCardDetailById(id, pid) ?? null
  if (deep || detail.value) return
  try {
    const card = await store.loadCardDetail(pid, id)
    if (v === version) detail.value = card
  } catch { if (v === version) failed.value = true }
}, { immediate: true })
</script>

<style scoped>
.ref-embed { margin: 12px 0; border: 1px solid var(--border-line); border-left: 3px solid var(--accent-color); background: var(--paper-card); }
.re-bar { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; padding: 8px 12px; border-bottom: 1px solid var(--border-line); background: var(--bg-sub); font-size: 13px; }
.compact .re-bar { border-bottom: 0; }
.re-icon { color: var(--accent-color); }
.re-title { border: 0; padding: 0; background: none; color: var(--text-primary); font: inherit; font-weight: 600; cursor: pointer; }
.re-title:hover { color: var(--accent-color); }
.re-type { font-size: 11px; padding: 0 6px; color: var(--accent-color); background: var(--accent-soft); }
.re-actions { margin-left: auto; display: flex; gap: 6px; }
.re-actions button { padding: 2px 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.re-actions button:hover { color: var(--accent-color); border-color: var(--accent-color); }
.re-note { margin: 0; padding: 10px 12px; font-size: 13px; color: var(--text-secondary); }
</style>
