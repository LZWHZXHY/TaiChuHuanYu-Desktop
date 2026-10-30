<!-- CardPeek.vue：点正文里的引用名字时，在右边弹出那个条目的详细内容（不离开当前页面）。
     在弹出的内容里继续点引用，会一层层往下看，可以返回。 -->
<template>
  <Teleport to="body">
    <aside class="card-peek" role="dialog" aria-modal="false" :aria-label="`条目详情：${detail?.title || ''}`" @keydown.esc="$emit('close')">
      <header class="cp-head">
        <button v-if="stack.length > 1" type="button" class="cp-btn" title="返回上一个" aria-label="返回上一个" @click="stack.pop()">‹</button>
        <span class="cp-title">{{ detail?.title || summary?.title || '条目' }}</span>
        <button type="button" class="cp-btn wide pane-only" title="固定到旁边的分屏窗格，和当前条目一起看" @click="$emit('pane', currentId)">⧉ 分屏</button>
        <button type="button" class="cp-btn wide" title="打开这个条目的页面" @click="$emit('select', currentId)">打开页面</button>
        <button ref="closeBtn" type="button" class="cp-btn" title="关闭" aria-label="关闭" @click="$emit('close')">×</button>
      </header>
      <div class="cp-body">
        <p v-if="failed" class="cp-note">这个条目不存在、已删除，或者你没有权限查看。</p>
        <p v-else-if="!detail" class="cp-note">正在读取…</p>
        <ProjectCardDocument v-else :key="detail.id" :card="detail" :project-id="projectId" mode="peek" :trail="[]"
          @peek="open" @select="id => $emit('select', id)" />
      </div>
    </aside>
  </Teleport>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import type { CardDetail } from '@/api/types/world'
import { useWorldStore } from '@/stores/world'
import ProjectCardDocument from './ProjectCardDocument.vue'

const props = defineProps<{ cardId: string; projectId: string }>()
defineEmits<{ close: []; select: [id: string]; pane: [id: string] }>()
const store = useWorldStore()
const stack = ref<string[]>([props.cardId])
// 外面又点了另一个引用：从头开始看那个
watch(() => props.cardId, id => { stack.value = [id] })
const currentId = computed(() => stack.value[stack.value.length - 1]!)
function open(id: string) { if (id !== currentId.value) stack.value = [...stack.value, id].slice(-20) }

const summary = computed(() => store.getCardById(currentId.value, props.projectId))
const detail = ref<CardDetail | null>(null)
const failed = ref(false)
let version = 0
watch([currentId, () => props.projectId], async ([id, pid]) => {
  const v = ++version
  failed.value = false
  detail.value = store.getCardDetailById(id, pid) ?? null
  if (detail.value) return
  try {
    const card = await store.loadCardDetail(pid, id)
    if (v === version) detail.value = card
  } catch { if (v === version) failed.value = true }
}, { immediate: true })

const closeBtn = ref<HTMLButtonElement | null>(null)
onMounted(() => closeBtn.value?.focus({ preventScroll: true }))
</script>

<style scoped>
.card-peek { position: fixed; top: 0; right: 0; bottom: 0; z-index: 1500; width: min(480px, 100vw); display: flex; flex-direction: column; background: var(--paper-card); border-left: 1px solid var(--border-line); box-shadow: -16px 0 40px rgba(0, 0, 0, .18); color: var(--text-primary); animation: peek-in .18s ease-out; }
@keyframes peek-in { from { transform: translateX(24px); opacity: 0; } }
@media (prefers-reduced-motion: reduce) { .card-peek { animation: none; } }
.cp-head { display: flex; align-items: center; gap: 8px; padding: 10px 12px; border-bottom: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); }
.cp-title { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 600; }
.cp-btn { min-width: 30px; height: 30px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); font: inherit; font-size: 15px; cursor: pointer; }
.cp-btn.wide { padding: 0 10px; font-size: 12px; }
/* 屏幕窄的时候没法分屏 */
@media (max-width: 999px) { .pane-only { display: none; } }
.cp-btn:hover { color: var(--accent-color); border-color: var(--accent-color); }
.cp-body { flex: 1; overflow-y: auto; overscroll-behavior: contain; }
.cp-note { margin: 0; padding: 24px 18px; font-size: 13px; color: var(--text-secondary); }
</style>
