<!-- CardOcSync.vue：条目编辑器顶部的「柴圈 OC」一条。
     - 条目对应着柴圈 OC：显示是哪个 OC；OC 作者（同时能编辑这个世界观）可以「从柴圈 OC 更新」条目（先预览再确认），也可以断开对应。
     - 「人物」条目还没对应 OC：能编辑的人可以「发布成柴圈 OC」，生成一个自己名下的 OC 草稿。
     同步、发布都按服务器上已保存的内容来，所以编辑器里有没保存的修改时先不让点，免得两边对不上。 -->
<template>
  <div v-if="info && (info.items.length || (info.isCharacter && info.canEdit))" class="cos" role="region" aria-label="柴圈 OC">
    <span class="cos-icon" aria-hidden="true">🧍</span>
    <template v-if="info.items.length">
      <div v-for="p in info.items" :key="p.ocId" class="cos-pair">
        <span>对应柴圈 OC <RouterLink :to="`/ocs/${p.ocId}`" target="_blank">《{{ p.title }}》</RouterLink><em v-if="p.status === 'draft'">草稿</em></span>
        <small>{{ p.mine ? '' : `作者 ${p.authorName} · ` }}{{ describeSync(p.lastSyncAt, p.lastSyncDirection) }}</small>
        <button v-if="p.canSync" type="button" class="cos-btn" :disabled="dirty || busy" :title="dirty ? '先保存当前的修改，再同步' : '先看看会改哪些，确认后才改'" @click="openSync(p)">从柴圈 OC 更新</button>
        <button v-if="p.mine || info.canEdit" type="button" class="cos-link" :disabled="busy" @click="disconnect(p)">断开对应</button>
      </div>
    </template>
    <template v-else>
      <span class="cos-text">这个人物还没有对应的柴圈 OC</span>
      <button type="button" class="cos-btn" :disabled="dirty || busy" :title="dirty ? '先保存当前的修改，再发布' : ''" @click="publish">{{ busy ? '生成中…' : '发布成柴圈 OC' }}</button>
    </template>
    <small v-if="dirty" class="cos-hint">有没保存的修改：先保存，再同步或发布</small>
    <OcSyncPreview v-if="syncTarget" v-model:open="syncOpen" :oc-id="syncTarget.ocId" :card-id="cardId" direction="oc-to-card" @done="onSynced" />
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getErrorMessage } from '@/utils/apiError'
import { ocWorldApi, describeSync, type CardOcInfo, type CardOcPair } from '@/views/柴圈板块/OCs/ocWorld'
import OcSyncPreview from '@/views/柴圈板块/OCs/components/OcSyncPreview.vue'

const props = defineProps<{ cardId: string; dirty?: boolean }>()
const emit = defineEmits<{ (e: 'synced'): void }>()
const router = useRouter()

const info = ref<CardOcInfo | null>(null)
const busy = ref(false)

let version = 0
async function load() {
  const v = ++version
  try {
    const res = await ocWorldApi.cardInfo(props.cardId)
    if (v === version) info.value = res
  } catch {
    if (v === version) info.value = null
  }
}
watch(() => props.cardId, id => { info.value = null; if (id) void load() }, { immediate: true })

const syncTarget = ref<CardOcPair | null>(null)
const syncOpen = ref(false)
function openSync(p: CardOcPair) { syncTarget.value = p; syncOpen.value = true }
function onSynced(message: string) {
  ElMessage.success(message)
  void load()
  // 条目在服务器上已经改了：让编辑器重新读一遍（和保存后一样）
  emit('synced')
}

async function publish() {
  try {
    await ElMessageBox.confirm('用这个条目生成一个你名下的柴圈 OC 草稿：正文 → 简介，属性和文本区块 → OC 属性，第一个能力面板 → 展示面板，封面和图库一起带上。对访客隐藏的区块不会带过去。条目本身不会变。',
      '发布成柴圈 OC', { confirmButtonText: '生成草稿', cancelButtonText: '取消', type: 'info' })
  } catch { return }
  busy.value = true
  try {
    const res = await ocWorldApi.publishCard(props.cardId)
    if (res.warning) ElMessage.warning(res.warning)
    await load()
    try {
      await ElMessageBox.confirm(`${res.message}。要现在去看看吗？`, '生成好了', { confirmButtonText: '去看看', cancelButtonText: '留在这里', type: 'success' })
      void router.push(`/ocs/${res.ocId}`)
    } catch { /* 留在这里 */ }
  } catch (e) {
    ElMessage.error(getErrorMessage(e, '发布失败'))
  } finally {
    busy.value = false
  }
}

async function disconnect(p: CardOcPair) {
  try {
    await ElMessageBox.confirm(`断开和柴圈 OC《${p.title}》的对应？只删对应关系，条目和 OC 的内容都不会变，以后就不能直接「从另一边更新」了。`, '断开对应',
      { confirmButtonText: '断开', cancelButtonText: '不了', type: 'warning' })
  } catch { return }
  busy.value = true
  try { const res = await ocWorldApi.disconnect(p.ocId, props.cardId); ElMessage.success(res.message); await load() }
  catch (e) { ElMessage.error(getErrorMessage(e, '断开失败')) }
  finally { busy.value = false }
}
</script>

<style scoped>
.cos { display: flex; flex-wrap: wrap; align-items: center; gap: 6px 12px; margin: 0 0 16px; padding: 8px 12px; border: 1px solid var(--border-line); border-left: 3px solid var(--accent-color); background: var(--paper-card); font-size: 13px; color: var(--text-primary); }
.cos-icon { font-size: 16px; }
.cos-pair { display: flex; flex-wrap: wrap; align-items: center; gap: 4px 10px; }
.cos-pair a { color: var(--accent-color); text-decoration: none; }
.cos-pair em { font-style: normal; font-size: 11px; margin-left: 4px; padding: 0 6px; border: 1px solid var(--border-line); color: var(--text-secondary); }
.cos-pair small, .cos-hint { color: var(--text-secondary); font-size: 12px; }
.cos-text { color: var(--text-secondary); }
.cos-btn { border: 1px solid var(--accent-color); border-radius: 0; background: var(--paper-card); color: var(--accent-color); padding: 3px 12px; font: inherit; font-size: 12px; cursor: pointer; }
.cos-btn:hover:not(:disabled) { background: var(--accent-soft); }
.cos-link { border: 0; background: none; color: var(--text-secondary); font: inherit; font-size: 12px; padding: 0; cursor: pointer; text-decoration: underline; }
.cos-btn:disabled, .cos-link:disabled { opacity: .5; cursor: not-allowed; }
</style>
