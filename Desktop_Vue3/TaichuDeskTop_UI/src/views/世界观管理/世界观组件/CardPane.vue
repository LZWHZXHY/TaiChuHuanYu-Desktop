<!-- 分屏窗格：在主窗口旁边再开一个条目一起看（像 vvd 的多窗口）。
     窗格里点引用就在这个窗格里往下看，可以返回；要编辑就「在主窗口打开」。 -->
<template>
  <section class="cpane" :aria-label="`分屏：${detail?.title || summary?.title || '条目'}`">
    <header class="cpane-head">
      <button v-if="stack.length > 1" type="button" class="cpane-btn" title="返回上一个" aria-label="返回上一个" @click="stack.pop()">‹</button>
      <span class="cpane-title" :title="detail?.title || summary?.title || ''">{{ detail?.title || summary?.title || '条目' }}</span>
      <button type="button" class="cpane-btn wide" title="在主窗口打开（可以编辑）" @click="$emit('main', currentId)">主窗口</button>
      <!-- wide：点过「加宽」，或者拖过分隔线调了宽度；这时按钮就是「恢复默认宽度」 -->
      <button type="button" class="cpane-btn" :title="wide ? '恢复默认宽度' : '加宽这个窗格（也可以拖左边的分隔线）'" :aria-label="wide ? '恢复默认宽度' : '加宽这个窗格'" :aria-pressed="wide" @click="$emit('toggle-wide')">{{ wide ? '⇤' : '⇥' }}</button>
      <button type="button" class="cpane-btn" title="关闭这个窗格" aria-label="关闭这个窗格" @click="$emit('close')">×</button>
    </header>
    <div class="cpane-body">
      <p v-if="failed" class="cpane-note">这个条目不存在、已删除，或者你没有权限查看。<button type="button" @click="$emit('close')">关闭窗格</button></p>
      <p v-else-if="!detail" class="cpane-note">正在读取…</p>
      <ProjectCardDocument v-else :key="detail.id" :card="detail" :project-id="projectId" mode="peek" :trail="[]"
        @peek="open" @select="open" />
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { CardDetail } from '@/api/types/world'
import { useWorldStore } from '@/stores/world'
import ProjectCardDocument from './ProjectCardDocument.vue'

const props = defineProps<{ cardId: string; projectId: string; wide?: boolean }>()
const emit = defineEmits<{ close: []; main: [id: string]; 'toggle-wide': []; navigate: [id: string] }>()
const store = useWorldStore()
const stack = ref<string[]>([props.cardId])
watch(() => props.cardId, id => { if (id !== currentId.value) stack.value = [id] })
const currentId = computed(() => stack.value[stack.value.length - 1]!)
function open(id: string) { if (id !== currentId.value) stack.value = [...stack.value, id].slice(-20) }
// 窗格里看到哪个就记哪个，刷新页面后还在
watch(currentId, id => emit('navigate', id))

const summary = computed(() => store.getCardById(currentId.value, props.projectId))
const detail = ref<CardDetail | null>(null)
const failed = ref(false)
let version = 0
async function load(force = false) {
  const v = ++version
  const id = currentId.value, pid = props.projectId
  failed.value = false
  const cached = force ? null : store.getCardDetailById(id, pid)
  if (cached) { detail.value = cached; return }
  if (!detail.value || detail.value.id !== id) detail.value = null
  try {
    const card = await store.loadCardDetail(pid, id)
    if (v === version) detail.value = card
  } catch { if (v === version) failed.value = true }
}
watch([currentId, () => props.projectId], () => void load(), { immediate: true })
// 主窗口保存了这个条目：窗格跟着刷新
defineExpose({ refresh: (id?: string) => { if (!id || id === currentId.value) void load(true) } })
</script>

<style scoped>
.cpane { display: flex; flex-direction: column; min-width: 0; min-height: 0; background: var(--paper-card); border-left: 1px solid var(--border-line); }
.cpane-head { display: flex; align-items: center; gap: 6px; padding: 8px 10px; border-bottom: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); flex-shrink: 0; }
.cpane-title { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 600; font-size: 14px; }
.cpane-btn { min-width: 28px; height: 28px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); font: inherit; font-size: 14px; cursor: pointer; }
.cpane-btn.wide { padding: 0 8px; font-size: 12px; }
.cpane-btn:hover, .cpane-btn[aria-pressed=true] { color: var(--accent-color); border-color: var(--accent-color); }
.cpane-body { flex: 1; overflow-y: auto; overscroll-behavior: contain; }
.cpane-note { margin: 0; padding: 24px 16px; font-size: 13px; color: var(--text-secondary); }
.cpane-note button { margin-left: 8px; }
</style>
