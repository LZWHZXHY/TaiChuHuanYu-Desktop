<!-- 管理面板 · 举报：用户在内容旁点「举报」提交的记录。管理员打开原内容核实后，标记「已处理」（已下架 / 已警告等）或「不处理」，举报人会收到通知。 -->
<template>
  <div class="rp">
    <div class="rp-head">
      <div>
        <h2>举报</h2>
        <p>点标题打开被举报的内容核实。下架、删除请在原内容页或对应模块操作，然后回来标记结果；同一内容的多条举报会一起标记，并通知每位举报人。</p>
      </div>
    </div>

    <div class="rp-tabs" role="tablist" aria-label="举报状态">
      <button v-for="t in tabs" :key="t.id" type="button" role="tab" :aria-selected="status === t.id" :class="{ on: status === t.id }" @click="switchTab(t.id)">
        {{ t.label }}<b v-if="t.id !== 'all' && counts[t.id]">{{ counts[t.id] }}</b>
      </button>
    </div>

    <p v-if="error" class="rp-error" role="alert">{{ error }}</p>
    <p v-else-if="!loading && !items.length" class="rp-empty">{{ status === 'pending' ? '没有待处理的举报 🎉' : '这里还没有记录' }}</p>

    <ul v-if="items.length" class="rp-list">
      <li v-for="r in items" :key="r.id" class="rp-item" :class="`is-${r.status}`">
        <div class="rp-top">
          <span class="rp-type">{{ r.typeLabel }}</span>
          <a v-if="r.targetUrl" :href="r.targetUrl" target="_blank" rel="noopener" class="rp-title">{{ r.targetTitle || `#${r.targetId}` }} ↗</a>
          <span v-else class="rp-title">{{ r.targetTitle || `#${r.targetId}` }}</span>
          <span v-if="r.sameTargetPending > 1" class="rp-many" title="这个内容还有其他人举报">{{ r.sameTargetPending }} 人举报</span>
        </div>
        <p class="rp-reason"><b>{{ REASON_LABELS[r.reason] || r.reason }}</b><span v-if="r.detail">：{{ r.detail }}</span></p>
        <p class="rp-meta">
          <UserLink :id="r.reporter.id" :name="r.reporter.username">{{ r.reporter.username }}</UserLink> 举报于 <time :datetime="r.createdAt">{{ time(r.createdAt) }}</time>
          <template v-if="r.status !== 'pending'"> · {{ r.handledByName || '管理员' }} {{ r.status === 'resolved' ? '已处理' : '不处理' }}<template v-if="r.handledAt">（{{ time(r.handledAt) }}）</template><template v-if="r.handleNote">：{{ r.handleNote }}</template></template>
        </p>
        <div v-if="r.status === 'pending'" class="rp-ops">
          <button type="button" class="ok" :disabled="busy === r.id" @click="handle(r, 'resolved')">已处理</button>
          <button type="button" :disabled="busy === r.id" @click="handle(r, 'dismissed')">不处理</button>
        </div>
      </li>
    </ul>

    <button v-if="hasMore" type="button" class="rp-more" :disabled="loading" @click="load(page + 1)">{{ loading ? '读取中…' : '加载更多' }}</button>
    <p v-else-if="loading && !items.length" class="rp-empty">读取中…</p>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { reportApi, REASON_LABELS, type AdminReport, type ReportStatus } from '@/api/reports'
import UserLink from '@/components/UserLink.vue'

type Tab = ReportStatus | 'all'
const tabs: { id: Tab; label: string }[] = [
  { id: 'pending', label: '待处理' }, { id: 'resolved', label: '已处理' }, { id: 'dismissed', label: '不处理' }, { id: 'all', label: '全部' },
]
const status = ref<Tab>('pending')
const counts = reactive<Record<string, number>>({ pending: 0, resolved: 0, dismissed: 0 })
const items = ref<AdminReport[]>([])
const page = ref(1), hasMore = ref(false), loading = ref(false), error = ref('')
const busy = ref<number | null>(null)

const errText = (e: any, fb: string) => e?.response?.data?.message || e?.friendlyMessage || fb

async function load(p = 1) {
  loading.value = true; error.value = ''
  try {
    const res = await reportApi.adminList(status.value, p)
    Object.assign(counts, res.counts)
    items.value = p === 1 ? res.items : [...items.value, ...res.items]
    page.value = p; hasMore.value = res.hasMore
  } catch (e) { error.value = errText(e, '举报列表读不出来，稍后再试') }
  finally { loading.value = false }
}
function switchTab(t: Tab) { if (status.value === t) return; status.value = t; items.value = []; void load(1) }

async function handle(r: AdminReport, to: 'resolved' | 'dismissed') {
  const note = window.prompt(to === 'resolved' ? '处理说明（会发给举报人，可留空）：例如「已下架」「已警告作者」' : '不处理的原因（会发给举报人，可留空）', '')
  if (note === null) return
  busy.value = r.id
  try {
    const res = await reportApi.adminHandle(r.id, { status: to, note: note.trim() || undefined, all: true })
    ElMessage.success(res.message)
    await load(1)
  } catch (e) { ElMessage.error(errText(e, '操作失败')) }
  finally { busy.value = null }
}

const p2 = (n: number) => String(n).padStart(2, '0')
function time(iso: string) {
  const t = new Date(new Date(iso).getTime() + 8 * 3600_000)
  return `${t.getUTCFullYear()}-${p2(t.getUTCMonth() + 1)}-${p2(t.getUTCDate())} ${p2(t.getUTCHours())}:${p2(t.getUTCMinutes())}`
}

onMounted(() => load(1))
</script>

<style scoped>
.rp { display: grid; gap: 16px; }
.rp-head h2 { margin: 0 0 6px; font-size: 20px; }
.rp-head p { margin: 0; max-width: 760px; color: var(--text-secondary); font-size: 13px; line-height: 1.7; }
.rp-tabs { display: flex; flex-wrap: wrap; gap: 4px; border-bottom: 1px solid var(--border-line); }
.rp-tabs button { display: inline-flex; align-items: center; gap: 6px; padding: 8px 14px; border: 0; border-bottom: 2px solid transparent; background: none; color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.rp-tabs button.on { color: var(--text-primary); border-bottom-color: var(--accent-color); font-weight: 600; }
.rp-tabs b { min-width: 18px; padding: 0 5px; background: var(--bg-sub); font-size: 11px; font-weight: 600; text-align: center; }
.rp-tabs button:first-child b { background: #dc2626; color: #fff; }
.rp-error { margin: 0; padding: 10px 14px; border-left: 3px solid var(--danger, #dc2626); color: var(--danger, #dc2626); background: var(--paper-card); }
.rp-empty { margin: 0; padding: 32px 0; text-align: center; color: var(--text-secondary); font-size: 14px; }
.rp-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 10px; }
.rp-item { padding: 14px 16px; border: 1px solid var(--border-line); border-left: 3px solid #f59e0b; background: var(--paper-card); }
.rp-item.is-resolved { border-left-color: #16a34a; }
.rp-item.is-dismissed { border-left-color: var(--border-line); opacity: .85; }
.rp-top { display: flex; flex-wrap: wrap; align-items: baseline; gap: 8px; }
.rp-type { padding: 1px 6px; background: var(--bg-sub); color: var(--text-secondary); font-size: 12px; }
.rp-title { min-width: 0; font-weight: 600; color: var(--text-primary); overflow-wrap: anywhere; }
a.rp-title:hover { color: var(--accent-color); }
.rp-many { padding: 1px 6px; background: color-mix(in srgb, #dc2626 14%, transparent); color: #dc2626; font-size: 12px; }
.rp-reason { margin: 8px 0 0; font-size: 14px; line-height: 1.6; white-space: pre-wrap; overflow-wrap: anywhere; }
.rp-meta { margin: 6px 0 0; color: var(--text-secondary); font-size: 12px; line-height: 1.6; }
.rp-meta :deep(a) { color: var(--text-primary); }
.rp-ops { display: flex; gap: 8px; margin-top: 10px; }
.rp-ops button, .rp-more { padding: 5px 14px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.rp-ops button.ok { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent, #fff); }
.rp-ops button:disabled, .rp-more:disabled { opacity: .5; cursor: default; }
.rp-more { justify-self: center; }
</style>
