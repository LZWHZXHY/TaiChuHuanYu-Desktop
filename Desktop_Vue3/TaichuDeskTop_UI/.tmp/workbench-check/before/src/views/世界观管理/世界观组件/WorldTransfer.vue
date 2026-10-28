<template>
  <Teleport to="body"><div class="transfer-mask" @click.self="close"><section role="dialog" aria-modal="true" :aria-label="mode==='import'?'导入世界观文件':'导出世界观'" class="transfer-dialog" @keydown.esc="close">
    <header><h2>{{ mode==='import'?'导入世界观文件':'导出世界观' }}</h2><button aria-label="关闭导入导出" @click="close">×</button></header>
    <template v-if="mode==='import' && canEdit">
      <p>支持世界观 ZIP 备份包和 UTF-8 的 TXT、Markdown。备份包包含原图；恢复时会上传图片并重新关联条目。TXT/MD 最大 64MB，ZIP 最大 256MB；条目数量按账号配额检查，不再限制 50 条。仅新建，不覆盖已有条目。关闭窗口后重新选择同一文件可继续；进度保留 7 天，过期自动清理未提交的图片。</p>
      <div v-if="pending.length" class="restore-pending">
        <p>未完成的恢复</p>
        <div v-for="entry in pending" :key="entry.id">
          <span>{{ entry.plan.source.fileName }} · {{ Object.keys(entry.images).length }} / {{ entry.plan.source.images.length }} 张图片{{ entry.state === 'cleaning' ? ' · 正在清理' : '' }}</span>
          <button :disabled="busy || reading || entry.state === 'cleaning'" @click="selectRestore(entry)">继续此恢复</button>
          <button :disabled="busy || reading" @click="cancelRestore(entry)">{{ entry.state === 'cleaning' ? '重试清理' : '取消并清理' }}</button>
        </div>
      </div>
      <p v-if="job && !completed" role="status">已保存恢复进度：{{ job.plan.source.fileName }}。{{ items.length || backup ? '文件已匹配，可以继续。' : '请选择原文件继续，已上传的图片不会重复上传。' }}</p>
      <input type="file" accept=".zip,.txt,.md,application/zip,text/plain,text/markdown" aria-label="选择世界观文件" :disabled="busy || reading" @change="readFile" />
      <label v-if="items.some(item => !item.payload)">普通条目类型 <select v-model="type" :disabled="busy || reading || started"><option v-for="option in store.cardTypes" :key="option.value" :value="option.value">{{ option.label }}</option></select></label>
      <p v-if="restoring">已识别本站导出文件，将恢复 {{ items.length }} 个条目及其完整字段。</p>
      <template v-if="backup">
        <p>备份：{{ backup.project.name }} · {{ backup.images.size }} 张原图</p>
        <label><input v-model="restoreNew" type="checkbox" :disabled="busy || started" /> 恢复为新的私密世界（包含原名称和简介）</label>
        <p v-if="!restoreNew">仅向当前世界新增条目；名称、简介和公开状态不变。</p>
      </template>
      <button v-if="plainAvailable && !busy" @click="readPlainFile">按普通 Markdown 导入（不恢复属性和关系）</button>
      <p v-if="plainMode" role="status">正在按普通 Markdown 导入，仅保留阅读内容和图片；原类型、属性和关联关系不会恢复。</p>
      <ol><li v-for="(item,index) in items.slice(0,20)" :key="index"><strong>{{ item.title }}</strong><span>{{ `${item.description.length} 字${item.payload ? ' · ' + getCardTypeLabel(item.payload.type) : ''}` }}</span><p>{{ previewDescriptions[index] }}</p></li></ol><p v-if="items.length > 20">预览前 20 个，共 {{ items.length }} 个条目；确认后会恢复全部条目。</p>
      <button class="primary" :disabled="busy || reading || completed || job?.state === 'cleaning' || (!items.length && !backup) || (!restoring && !backup && !type)" @click="importFile">{{ busy?'正在导入…':completed?'导入完成':job?'继续恢复':`确认新建 ${items.length} 个条目` }}</button>
    </template>
    <template v-else-if="mode==='export'">
      <p>完整备份包含世界名称、简介、全部条目、引用关系和原图，解压后可阅读 MD，重新导入 ZIP 可恢复。普通 Markdown 仍保留原图地址；需要离线保存图片时请选择完整备份。PDF 使用现有打印流程。</p>
      <div class="export-actions"><button class="primary" :disabled="busy" @click="exportFile('zip')">下载完整备份（含图片）</button><button :disabled="busy" @click="exportFile('md')">下载 Markdown</button><button :disabled="busy" @click="exportFile('pdf')">打印 / 保存 PDF</button></div>
    </template>
    <template v-if="printHtml"><iframe ref="printFrame" :srcdoc="printHtml" title="PDF 导出预览" sandbox="allow-same-origin allow-modals" @load="preparePreview" /><button :disabled="!printReady || busy" @click="printPdf">{{ printReady ? '打印 / 另存为 PDF' : '正在准备图片和字体…' }}</button><p>建议关闭打印窗口中的页眉和页脚，使用 A4 纸张、100% 缩放。</p></template>
    <a v-if="completed && restoredProject && !busy" :href="`#/world/project/${restoredProject.id}`" @click="emit('close')">打开恢复的世界 →</a>
    <p v-if="reading" role="status">正在读取和校验备份…</p><p v-if="status" role="status">{{ status }}</p><p v-if="error" role="alert" class="error">{{ error }}</p>
  </section></div></Teleport>
</template>
<script setup lang="ts">
import { computed, ref, shallowRef, onMounted, watch, onBeforeUnmount } from 'vue'
import type { WorldProject, CardDetail, WorldRestoreStatus } from '@/api/types/world'
import { worldApi } from '@/api/worldApi'
import { useWorldStore } from '@/stores/world'
import { useUserStore } from '@/stores/user'
import { parseWorldText, resolveWorldImport, hasWorldRestoreData, worldMarkdown, type WorldTextItem } from '@/utils/worldTransfer'
import { worldPrintHtml, prepareWorldPrint } from '@/utils/worldPrint'
import { getCardTypeLabel } from '@/utils/worldPresentation'
import { createWorldBackup, readWorldBackup, replaceWorldImages, worldTextBytes, worldArchiveBytes, type WorldBackup } from '@/utils/worldBackup'

const props = defineProps<{ project: WorldProject; mode: 'import' | 'export'; canEdit: boolean }>()
const emit = defineEmits<{ close: [] }>()
const store = useWorldStore(), user = useUserStore()
const busy = ref(false), error = ref(''), status = ref(''), type = ref('')
const items = ref<WorldTextItem[]>([])
const backup = shallowRef<WorldBackup | null>(null), restoreNew = ref(true), restoredProject = ref<WorldProject | null>(null)
const completed = ref(false), reading = ref(false)
const job = shallowRef<WorldRestoreStatus | null>(null), pending = ref<WorldRestoreStatus[]>([])
const started = computed(() => !!job.value)
let fingerprint = ''
const cancellation = new AbortController()
const restoring = computed(() => items.value.length > 0 && items.value.every(item => item.payload))
const previewDescriptions = computed(() => {
  const titles = new Map(items.value.map(item => [item.sourceId, item.title]))
  return items.value.slice(0,20).map(item => item.description.replace(/\{CARD:([^}]+)\}/g,
    (_, id: string) => `「${titles.get(id) || '未匹配的条目'}」`).slice(0, 160))
})
const plainAvailable = ref(false), plainMode = ref(false)
let fileText = '', fileName = '', fileVersion = 0
const printReady = ref(false), printHtml = ref('')
const printFrame = ref<HTMLIFrameElement | null>(null)
const account = user.userId, projectId = props.project.id
let disposed = false, previewVersion = 0
const valid = () => !disposed && user.userId === account && props.project.id === projectId
onBeforeUnmount(() => { disposed = true; previewVersion++; cancellation.abort() })
watch([() => user.userId, () => props.project.id], () => {
  cancellation.abort(); fileVersion++; fileText = ''; backup.value = null; items.value = []; job.value = null; pending.value = []; printHtml.value = ''; emit('close')
})
function close() { emit('close') }
onMounted(async () => {
  if (props.mode !== 'import') return
  await store.fetchCardTypes()
  if (valid()) type.value = store.cardTypes.find(t => t.value === 'lore')?.value || store.cardTypes[0]?.value || ''
  await refreshRestores()
})

async function readFile(event: Event) {
  const version = ++fileVersion
  error.value = ''; status.value = ''; items.value = []
  plainAvailable.value = false; plainMode.value = false; fileText = ''; fileName = ''; backup.value = null; restoredProject.value = null; completed.value = false; fingerprint = ''
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  reading.value = true
  try {
    const zipped = /\.zip$/i.test(file.name)
    if (!/\.(txt|md|zip)$/i.test(file.name) || file.size > (zipped ? worldArchiveBytes : worldTextBytes))
      throw new Error('请选择不超过 64MB 的 TXT/MD，或不超过 256MB 的世界观 ZIP 备份。')
    const bytes = new Uint8Array(await file.arrayBuffer())
    const hash = Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',bytes)), b => b.toString(16).padStart(2,'0')).join('')
    if (job.value && job.value.plan.source.fingerprint !== hash) throw new Error('文件与待恢复记录不一致。请选择原文件，或先取消并清理该恢复。')
    fingerprint = hash; fileName = file.name
    if (zipped) {
      const result = await readWorldBackup(bytes)
      if (!valid() || version !== fileVersion) return
      backup.value = result; items.value = result.items; restoreNew.value = job.value?.plan.source.newProject ?? true
      return
    }
    const text = new TextDecoder('utf-8', { fatal: true }).decode(bytes)
    if (!valid() || version !== fileVersion) return
    fileText = text; fileName = file.name
    items.value = parseWorldText(text, file.name,job.value?.plan.source.plainText)
    plainMode.value = job.value?.plan.source.plainText ?? false
  } catch (e) {
    if (!valid() || version !== fileVersion) return
    error.value = e instanceof Error ? e.message : '文件读取失败'
    plainAvailable.value = hasWorldRestoreData(fileText)
  } finally { if (version === fileVersion) reading.value = false }
}

function readPlainFile() {
  try {
    items.value = parseWorldText(fileText, fileName, true)
    plainMode.value = true; plainAvailable.value = false; error.value = ''
  } catch (e) { error.value = e instanceof Error ? e.message : '文件读取失败' }
}

async function refreshRestores() {
  try {
    const result = await worldApi.listRestores(projectId)
    if (valid()) pending.value = result
  } catch { if (valid()) error.value = '恢复记录读取失败，请重新打开窗口后重试。' }
}
function selectRestore(entry: WorldRestoreStatus) {
  job.value = entry; items.value = []; backup.value = null; fingerprint = ''; completed.value = false
  type.value = entry.plan.source.defaultType; restoreNew.value = entry.plan.source.newProject
  plainMode.value = entry.plan.source.plainText; plainAvailable.value = false
  status.value = '请选择原备份文件；会从服务器确认的进度继续。'; error.value = ''
}
async function applyStatus(result: WorldRestoreStatus) {
  if (!valid()) return
  job.value = result
  if (result.state === 'committed') {
    completed.value = true
    status.value = `恢复完成：${result.plan.cardIds.length} 个条目，${Object.keys(result.images).length} 张原图。重复请求不会再次创建。`
    restoredProject.value = await worldApi.getProject(result.targetProjectId)
    if (result.targetProjectId === projectId) await store.fetchCards(projectId,true)
  } else if (result.state === 'cleaning') status.value = '恢复未提交，正在清理本次上传的图片；清理失败时服务器会自动重试。'
  else if (result.state === 'cancelled') { status.value = '本次恢复已取消，条目未写入，上传图片已清理。'; job.value = null }
}
async function cancelRestore(entry: WorldRestoreStatus) {
  busy.value = true; error.value = ''
  try { await applyStatus(await worldApi.cancelRestore(entry.id)); await refreshRestores() }
  catch { if(valid()) error.value = '清理结果暂时无法确认，请重试；不会删除已完成恢复或已有条目。' }
  finally { busy.value = false }
}
async function importFile() {
  if (busy.value || !props.canEdit || !valid() || completed.value || !fingerprint) return
  busy.value = true; error.value = ''
  try {
    status.value = '正在核对服务端恢复进度…'
    const result = await worldApi.startRestore({
      sourceProjectId: projectId, fingerprint, fileName, newProject: !!backup.value && restoreNew.value,
      name: backup.value?.project.name || props.project.name, cardCount: items.value.length,
      defaultType: restoring.value ? '' : type.value, plainText: plainMode.value,
      images: [...(backup.value?.images || [])].map(([path,image]) => ({name:path.split('/').pop()!,size:image.bytes.length})),
    })
    await applyStatus(result)
    if (!valid() || result.state !== 'pending') return
    for (const [path,image] of backup.value?.images || []) {
      if (job.value!.images[path]) continue
      if (!valid()) return
      status.value = `正在上传原图 ${Object.keys(job.value!.images).length + 1} / ${backup.value!.images.size}；可关闭窗口稍后继续。`
      const name = path.split('/').pop()!
      await applyStatus(await worldApi.uploadRestoreImage(result.id,name,new File([image.bytes],name,{type:image.type}),cancellation.signal))
      if (!valid() || job.value?.state !== 'pending') return
    }
    if (!valid()) return
    const images = new Map(Object.entries(job.value!.images))
    const source = replaceWorldImages(items.value,images)
    const resolved = resolveWorldImport(source,result.plan.cardIds)
    status.value = '图片已齐全，正在一次性保存条目与引用…'
    await applyStatus(await worldApi.commitRestore(result.id,{
      cards:resolved.payloads.map(p => ({...p,type:p.type || type.value})),
      description: backup.value ? replaceWorldImages(backup.value.project,images).description : null,
    }))
    if (completed.value && resolved.unresolved) status.value += ` 有 ${resolved.unresolved} 处普通 Markdown 引用未匹配，请在编辑页核对。`
  } catch (e) {
    if (!valid()) return
    const code = (e as {response?:{status:number}}).response?.status
    if (job.value) {
      try {
        // 业务拒绝会清理；断网/超时只查询结果，绝不盲目重建或回滚已经成功的提交。
        const result = code === 400 ? await worldApi.cancelRestore(job.value.id) : await worldApi.getRestore(job.value.id)
        await applyStatus(result)
      } catch { /* 保留原记录，下次用同一文件从服务端核对。 */ }
    }
    if (!completed.value) error.value = (e as {friendlyMessage?:string}).friendlyMessage || (e instanceof Error ? e.message : '恢复暂停')
    if (job.value?.state === 'pending') status.value = '恢复已暂停，已有条目不受影响。可以关闭窗口，稍后选择同一文件继续。'
  } finally {
    busy.value = false
    if (valid()) await refreshRestores()
  }
}

async function preparePreview() {
  const doc = printFrame.value?.contentDocument, version = ++previewVersion
  if (!doc) return
  printReady.value = false
  const failed = await prepareWorldPrint(doc)
  if (!valid() || version !== previewVersion) return
  printReady.value = true
  status.value = failed ? `有 ${failed} 张图片未能加载，预览中已保留原图链接。请核对后打印，或重新生成预览。`
    : '中文排版和图片已准备好。点击打印，在打印窗口选择“另存为 PDF”。'
}
function printPdf() {
  if (!valid() || !printReady.value) return
  try {
    printFrame.value?.contentWindow?.focus()
    printFrame.value?.contentWindow?.print()
    status.value = '已请求打开打印窗口；只有完成“另存为 PDF”才会生成文件。若当前内置浏览器无反应，请在 Edge 或 Chrome 中打开网站后重试。'
  } catch { error.value = '当前浏览器无法打开打印窗口，请使用 Edge 或 Chrome 重试。' }
}

async function exportFile(format: 'md' | 'pdf' | 'zip') {
  if (busy.value || !valid()) return
  busy.value = true; error.value = ''; status.value = '正在读取条目…'
  printReady.value = false; printHtml.value = ''; previewVersion++
  try {
    const sourceProject = await worldApi.getProject(projectId)
    const summaries = await worldApi.getCards(projectId), cards: CardDetail[] = []
    for (let i = 0; i < summaries.length; i += 100) {
      if (!valid()) return
      const ids = summaries.slice(i, i + 100).map(c => c.id)
      const batch = await worldApi.getCardsBatch(projectId, ids)
      if (batch.length !== ids.length) throw new Error('条目已变化，请重新导出')
      const byId = new Map(batch.map(card => [card.id, card]))
      for (const summary of summaries.slice(i, i + 100)) {
        const card = byId.get(summary.id)
        if (!card || card.updatedAt !== summary.updatedAt) throw new Error('导出期间内容发生变化，请重新导出。')
        cards.push(card)
      }
      status.value = `已读取 ${cards.length} / ${summaries.length} 个条目`
    }
    if (!valid()) return
    const latest = await worldApi.getCards(projectId), versions = new Map(summaries.map(c => [c.id,c.updatedAt]))
    if (latest.length !== summaries.length || latest.some(c => versions.get(c.id)!==c.updatedAt)) throw new Error('导出期间条目发生变化，请重新导出。')
    if (format === 'zip') {
      const result = await createWorldBackup(sourceProject,cards,message => { if(valid()) status.value=message },{signal:cancellation.signal})
      if (!valid()) return
      download(new Blob([result.bytes],{type:'application/zip'}), sourceProject.name+'.zip')
      status.value=`完整备份已生成：${cards.length} 个条目，${result.imageCount} 张原图。`
      return
    }
    const markdown = worldMarkdown(sourceProject, cards, format === 'md')
    if (format === 'md') {
      download(new Blob([markdown],{type:'text/markdown;charset=utf-8'}),sourceProject.name+'.md')
      status.value = 'Markdown 已生成。'
    } else {
      printHtml.value = worldPrintHtml(props.project.name, markdown)
      status.value = '正在准备图片和字体…'
    }
  } catch (e) { error.value = e instanceof Error ? e.message : '导出失败，请重试' }
  finally { busy.value = false }
}
function download(blob: Blob, name: string) {
  const url=URL.createObjectURL(blob),a=document.createElement('a')
  a.href=url; a.download=name.replace(/[<>:"/\\|?*]/g,'_'); a.click()
  setTimeout(()=>URL.revokeObjectURL(url),1000)
}
</script>
<style scoped>
.restore-pending{padding:12px;border:1px solid var(--border-line);border-radius:8px;background:var(--bg-main)}.restore-pending>div{display:flex;align-items:center;gap:8px;flex-wrap:wrap;margin:8px 0}.restore-pending span{flex:1;min-width:160px;font-size:13px;overflow-wrap:anywhere}
iframe{width:100%;height:380px;border:1px solid var(--border-line);margin:16px 0;background:white}
.transfer-mask{position:fixed;inset:0;z-index:2000;background:#0006;display:grid;place-items:center;padding:16px}.transfer-dialog{box-sizing:border-box;width:min(640px,100%);max-height:85vh;overflow:auto;padding:24px;background:var(--paper-card);color:var(--text-primary);border:1px solid var(--border-line);border-radius:10px}.transfer-dialog header{display:flex;align-items:center;justify-content:space-between}h2{margin:0;font-size:20px}p{font-size:13px;line-height:1.8;color:var(--text-secondary)}button,select,input{font:inherit;max-width:100%;box-sizing:border-box}button,select{padding:9px 12px;border:1px solid var(--border-line);border-radius:6px;background:var(--bg-main);color:var(--text-primary);cursor:pointer}button:disabled{opacity:.5;cursor:default}.export-actions{display:flex;gap:12px;flex-wrap:wrap}label{display:block;margin-top:16px}ol{padding-left:22px;max-height:32vh;overflow:auto;overscroll-behavior:contain}li{padding:12px 0;border-bottom:1px solid var(--border-line)}li span{margin-left:12px;font-size:12px;color:var(--text-secondary)}li p{white-space:pre-wrap;overflow-wrap:anywhere}.primary{background:var(--accent-color);color:var(--on-accent)}.error{color:var(--danger);overflow-wrap:anywhere}a{display:inline-block;margin-top:16px;color:var(--accent-color)}input[type=checkbox]{width:auto;margin-right:8px}
</style>
