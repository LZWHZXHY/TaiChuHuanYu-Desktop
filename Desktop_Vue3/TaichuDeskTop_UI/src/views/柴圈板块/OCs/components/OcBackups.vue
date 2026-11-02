<!-- OcBackups.vue：OC 的备份（只有作者看得到）。
     从世界观条目更新 OC、从备份恢复之前，后端都会把 OC 当时的样子整份存一份。
     这里能看每份备份里有什么，也能一键恢复（恢复前会再自动备份一次现在的样子，恢复错了还能恢复回来）。 -->
<template>
  <el-dialog :model-value="open" title="OC 的备份" width="min(680px, calc(100vw - 32px))" append-to-body
    :close-on-click-modal="!restoring" :show-close="!restoring" @update:model-value="(v: boolean) => { if (!v) close() }">
    <div class="ocb">
      <p class="ocb-tip">从世界观条目更新 OC、从备份恢复之前，都会自动备份一份。恢复会换回备份里的名字、简介、封面、全部属性、图库和能力面板（发布状态不变）。</p>
      <p v-if="loading" class="ocb-tip" role="status">正在读取备份…</p>
      <p v-else-if="error" class="ocb-error" role="alert">{{ error }} <button type="button" class="ocb-link" @click="load">重试</button></p>
      <p v-else-if="!items.length" class="ocb-tip">还没有备份。</p>
      <ul v-else class="ocb-list">
        <li v-for="b in items" :key="b.id" :class="{ active: detail?.id === b.id }">
          <div class="ocb-main">
            <b>#{{ b.id }} · {{ fmt(b.createdAt) }}</b>
            <span>{{ b.reason || '备份' }}</span>
            <small v-if="b.readable">《{{ b.title }}》· {{ b.attributeCount }} 个属性 · {{ b.imageCount }} 张图{{ b.hasShowPanel ? ' · 有能力面板' : '' }}{{ b.createdBy ? ' · ' + b.createdBy : '' }}</small>
            <small v-else class="ocb-error">这份备份读不出来</small>
          </div>
          <div class="ocb-ops">
            <button type="button" class="ocb-btn" :disabled="!b.readable || restoring" @click="view(b.id)">{{ detail?.id === b.id ? '收起' : '查看' }}</button>
            <button type="button" class="ocb-btn warn" :disabled="!b.readable || restoring" @click="restore(b.id)">{{ restoringId === b.id ? '恢复中…' : '恢复' }}</button>
          </div>
          <div v-if="detail?.id === b.id" class="ocb-detail">
            <p><em>名字</em>{{ detail.title }}</p>
            <p v-if="detail.description"><em>简介</em>{{ detail.description }}</p>
            <p v-if="detail.coverUrl"><em>封面</em><img :src="detail.coverUrl" alt="备份里的封面" /></p>
            <dl v-if="detail.attributes.length">
              <template v-for="(a, i) in detail.attributes" :key="i">
                <dt>{{ a.key }}<small v-if="a.type === 'long'">（长）</small></dt>
                <dd>{{ a.value || '（空）' }}</dd>
              </template>
            </dl>
            <p v-if="detail.images.length"><em>图库</em>{{ detail.images.length }} 张</p>
            <StatPanelChart v-if="detail.showPanel" :panel="detail.showPanel" />
            <p v-if="detail.hasBattlePanel" class="ocb-tip">（还有一份约战面板，恢复时一起换回来）</p>
          </div>
        </li>
      </ul>
    </div>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import StatPanelChart from '@/components/StatPanelChart.vue'
import { cleanStatPanel } from '@/utils/statPanel'
import { getErrorMessage } from '@/utils/apiError'
import { ocWorldApi, type OcBackupBrief, type OcBackupDetail } from '../ocWorld'

const props = defineProps<{ open: boolean; ocId: string }>()
const emit = defineEmits<{ (e: 'update:open', v: boolean): void; (e: 'restored'): void }>()

const items = ref<OcBackupBrief[]>([])
const loading = ref(false)
const error = ref('')
const detail = ref<OcBackupDetail | null>(null)
const restoring = ref(false)
const restoringId = ref<number | null>(null)

const fmt = (at: string) => new Date(at).toLocaleString('zh-CN')

async function load() {
  loading.value = true
  error.value = ''
  try { items.value = (await ocWorldApi.backups(props.ocId)).items }
  catch (e) { error.value = getErrorMessage(e, '读取备份失败') }
  finally { loading.value = false }
}
watch(() => [props.open, props.ocId], () => { detail.value = null; if (props.open) void load() }, { immediate: true })

function close() { if (!restoring.value) emit('update:open', false) }

async function view(id: number) {
  if (detail.value?.id === id) { detail.value = null; return }
  try {
    const d = await ocWorldApi.backup(props.ocId, id)
    detail.value = { ...d, showPanel: d.showPanel ? cleanStatPanel(d.showPanel) : null }
  } catch (e) { ElMessage.error(getErrorMessage(e, '读取备份失败')) }
}

async function restore(id: number) {
  try {
    await ElMessageBox.confirm('把 OC 换回这份备份里的样子？现在的样子会先自动备份一份，恢复错了还能再恢复回来。', '从备份恢复',
      { confirmButtonText: '恢复', cancelButtonText: '取消', type: 'warning' })
  } catch { return }
  restoring.value = true
  restoringId.value = id
  try {
    const res = await ocWorldApi.restore(props.ocId, id)
    ElMessage.success(res.message)
    emit('restored')
    await load()
  } catch (e) { ElMessage.error(getErrorMessage(e, '恢复失败，OC 没有变化')) }
  finally { restoring.value = false; restoringId.value = null }
}
</script>

<style scoped>
.ocb { display: grid; gap: 10px; max-height: min(66vh, 660px); overflow-y: auto; }
.ocb-tip { margin: 0; color: var(--text-secondary); font-size: 13px; line-height: 1.7; }
.ocb-error { color: var(--danger, #c0392b); font-size: 13px; }
.ocb-link { border: 0; background: none; color: var(--accent-color); cursor: pointer; font: inherit; padding: 0; text-decoration: underline; }
.ocb-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.ocb-list > li { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 8px; padding: 10px 12px; border: 1px solid var(--border-line); background: var(--paper-card); }
.ocb-list > li.active { border-color: var(--accent-color); }
.ocb-main { display: grid; gap: 2px; min-width: 0; font-size: 13px; color: var(--text-primary); }
.ocb-main small { color: var(--text-secondary); overflow-wrap: anywhere; }
.ocb-ops { display: flex; gap: 6px; align-items: flex-start; }
.ocb-btn { border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); padding: 4px 12px; font: inherit; font-size: 12px; cursor: pointer; }
.ocb-btn.warn { border-color: var(--accent-color); color: var(--accent-color); }
.ocb-btn:disabled { opacity: .5; cursor: not-allowed; }
.ocb-detail { grid-column: 1 / -1; border-top: 1px dashed var(--border-line); padding-top: 8px; font-size: 13px; color: var(--text-primary); display: grid; gap: 6px; }
.ocb-detail p { margin: 0; white-space: pre-wrap; overflow-wrap: anywhere; line-height: 1.7; }
.ocb-detail em { font-style: normal; color: var(--text-secondary); margin-right: 8px; }
.ocb-detail img { max-width: 120px; max-height: 120px; vertical-align: top; border: 1px solid var(--border-line); }
.ocb-detail dl { display: grid; grid-template-columns: max-content minmax(0, 1fr); gap: 4px 12px; margin: 0; }
.ocb-detail dt { color: var(--text-secondary); }
.ocb-detail dd { margin: 0; white-space: pre-wrap; overflow-wrap: anywhere; }
</style>
