<!-- OcSyncPreview.vue：「从另一边更新」的预览 + 确认（OC 详情页和世界观条目编辑器共用）。
     先调预览接口，列出会新增 / 修改 / 保留 / 跳过的每一项，用户看清楚了再点「确认更新」。
     确认时把预览的指纹带回去：预览之后两边内容有变化，后端会拒绝，这里自动重新预览。 -->
<template>
  <el-dialog :model-value="open" :title="title" width="min(720px, calc(100vw - 32px))" append-to-body
    :close-on-click-modal="!syncing" :close-on-press-escape="!syncing" :show-close="!syncing" @update:model-value="(v: boolean) => { if (!v) close() }">
    <div class="osp">
      <p v-if="loading" class="osp-tip" role="status">正在对比两边的内容…</p>
      <p v-else-if="error" class="osp-error" role="alert">{{ error }} <button type="button" class="osp-link" @click="load">重新对比</button></p>
      <template v-else-if="preview">
        <p class="osp-tip">
          {{ direction === 'card-to-oc' ? `用世界观条目《${preview.cardTitle}》更新柴圈 OC《${preview.ocTitle}》` : `用柴圈 OC《${preview.ocTitle}》更新世界观条目《${preview.cardTitle}》` }}。
          只改能对应上的部分，{{ direction === 'card-to-oc' ? 'OC' : '条目' }}里独有的内容保留不动。
        </p>
        <p class="osp-safe">{{ direction === 'card-to-oc'
          ? '更新前会自动把 OC 现在的样子备份一份，之后在 OC 详情页「备份」里能查看和恢复。'
          : '这次更新会记进世界观的「修改记录」，之后可以在修改记录里撤销。' }}</p>
        <p v-if="notice" class="osp-error" role="status">{{ notice }}</p>
        <p v-if="!preview.hasChanges" class="osp-same">两边能对应上的内容已经一样了，没有要更新的。</p>

        <section v-for="group in groups" :key="group.action" class="osp-group" :class="'is-' + group.action">
          <h4>{{ group.label }} <small>{{ group.items.length }} 项</small></h4>
          <ul>
            <li v-for="(c, i) in group.items" :key="group.action + i">
              <b class="osp-field">{{ c.field }}</b>
              <template v-if="c.action === 'update'">
                <span class="osp-before">{{ c.before || '（空）' }}</span>
                <span class="osp-arrow" aria-label="改成">→</span>
                <span class="osp-after">{{ c.after || '（空）' }}</span>
              </template>
              <span v-else-if="c.action === 'add'" class="osp-after">{{ c.after }}</span>
              <span v-else-if="c.before" class="osp-before keep">{{ c.before }}</span>
              <small v-if="c.note" class="osp-note">{{ c.note }}</small>
            </li>
          </ul>
        </section>
      </template>
    </div>
    <template #footer>
      <div class="osp-actions">
        <button type="button" class="osp-btn" :disabled="syncing" @click="close">取消</button>
        <button type="button" class="osp-btn primary" :disabled="!preview?.hasChanges || loading || syncing" @click="confirm">
          {{ syncing ? '更新中…' : '确认更新' }}
        </button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import axios from 'axios'
import { getErrorMessage } from '@/utils/apiError'
import { ocWorldApi, type SyncChange, type SyncDirection, type SyncPreview } from '../ocWorld'

const props = defineProps<{ open: boolean; ocId: string; cardId: string; direction: SyncDirection }>()
const emit = defineEmits<{ (e: 'update:open', v: boolean): void; (e: 'done', message: string): void }>()

const preview = ref<SyncPreview | null>(null)
const loading = ref(false)
const syncing = ref(false)
const error = ref('')
const notice = ref('')
const title = computed(() => props.direction === 'card-to-oc' ? '从世界观条目更新 OC' : '从柴圈 OC 更新条目')

const LABELS: { action: SyncChange['action']; label: string }[] = [
  { action: 'update', label: '会修改' },
  { action: 'add', label: '会新增' },
  { action: 'keep', label: '保留不动' },
  { action: 'skip', label: '跳过（不会同步）' },
]
const groups = computed(() => LABELS.map(g => ({ ...g, items: (preview.value?.changes ?? []).filter(c => c.action === g.action) })).filter(g => g.items.length))

let version = 0
async function load() {
  const v = ++version
  loading.value = true
  error.value = ''
  try {
    const res = await ocWorldApi.preview(props.ocId, props.cardId, props.direction)
    if (v === version) preview.value = res
  } catch (e) {
    if (v === version) { preview.value = null; error.value = getErrorMessage(e, '对比失败，请稍后再试') }
  } finally {
    if (v === version) loading.value = false
  }
}
watch(() => [props.open, props.ocId, props.cardId, props.direction], () => {
  notice.value = ''
  if (props.open) void load()
}, { immediate: true })

function close() { if (!syncing.value) emit('update:open', false) }

async function confirm() {
  if (!preview.value || syncing.value) return
  syncing.value = true
  notice.value = ''
  try {
    const res = await ocWorldApi.sync(props.ocId, props.cardId, props.direction, preview.value.fingerprint)
    emit('done', res.message)
    emit('update:open', false)
  } catch (e) {
    // 预览之后有人改过：重新对比一次，让用户看最新的再确认
    if (axios.isAxiosError(e) && e.response?.status === 409) {
      await load()
      notice.value = getErrorMessage(e, '内容有变化，已经重新对比，请再看一遍')
    } else notice.value = getErrorMessage(e, '更新失败，请稍后再试')
  } finally {
    syncing.value = false
  }
}
</script>

<style scoped>
.osp { display: grid; gap: 12px; max-height: min(64vh, 640px); overflow-y: auto; padding-right: 4px; }
.osp-tip { margin: 0; color: var(--text-primary); line-height: 1.7; font-size: 14px; }
.osp-safe { margin: 0; padding: 8px 12px; background: var(--accent-soft); color: var(--text-primary); font-size: 13px; line-height: 1.6; border-left: 3px solid var(--accent-color); }
.osp-same { margin: 0; color: var(--text-secondary); font-size: 13px; }
.osp-error { margin: 0; color: var(--danger, #c0392b); font-size: 13px; line-height: 1.6; }
.osp-link { border: 0; background: none; color: var(--accent-color); cursor: pointer; font: inherit; padding: 0; text-decoration: underline; }
.osp-group { border: 1px solid var(--border-line); background: var(--paper-card); }
.osp-group h4 { margin: 0; padding: 8px 12px; font-size: 13px; font-weight: 600; color: var(--text-primary); background: var(--bg-sub); border-bottom: 1px solid var(--border-line); }
.osp-group h4 small { font-weight: 400; color: var(--text-secondary); margin-left: 6px; }
.osp-group.is-update h4, .osp-group.is-add h4 { border-left: 3px solid var(--accent-color); }
.osp-group ul { list-style: none; margin: 0; padding: 0; }
.osp-group li { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 8px; padding: 8px 12px; border-top: 1px solid var(--border-line); font-size: 13px; line-height: 1.6; }
.osp-group li:first-child { border-top: 0; }
.osp-field { color: var(--text-primary); font-weight: 600; flex: 0 0 auto; }
.osp-before, .osp-after { white-space: pre-wrap; overflow-wrap: anywhere; min-width: 0; }
.osp-before { color: var(--text-secondary); text-decoration: line-through; }
.osp-before.keep { text-decoration: none; }
.osp-after { color: var(--text-primary); }
.osp-arrow { color: var(--accent-color); }
.osp-note { flex-basis: 100%; color: var(--text-secondary); font-size: 12px; }
.osp-actions { display: flex; justify-content: flex-end; gap: 8px; }
.osp-btn { border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); padding: 7px 18px; font: inherit; font-size: 13px; cursor: pointer; }
.osp-btn.primary { border-color: var(--accent-color); background: var(--accent-color); color: #fff; }
.osp-btn:disabled { opacity: .5; cursor: not-allowed; }
</style>
