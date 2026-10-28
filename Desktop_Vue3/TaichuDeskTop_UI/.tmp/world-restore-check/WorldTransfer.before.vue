<template>
  <Teleport to="body"><div class="transfer-mask" @click.self="close"><section role="dialog" aria-modal="true" :aria-label="mode==='import'?'导入世界观文件':'导出世界观'" class="transfer-dialog" @keydown.esc="close">
    <header><h2>{{ mode==='import'?'导入世界观文件':'导出世界观' }}</h2><button :disabled="busy" aria-label="关闭导入导出" @click="close">×</button></header>
    <template v-if="mode==='import' && canEdit">
      <p>支持世界观 ZIP 备份包和 UTF-8 的 TXT、Markdown。备份包包含原图；恢复时会上传图片并重新关联条目。TXT/MD 最大 64MB，ZIP 最大 256MB；条目数量按账号配额检查，不再限制 50 条。仅新建，不覆盖已有条目。</p>
      <input type="file" accept=".zip,.txt,.md,application/zip,text/plain,text/markdown" aria-label="选择世界观文件" :disabled="busy || reading || started" @change="readFile" />
      <label v-if="items.some(item => !item.payload)">普通条目类型 <select v-model="type" :disabled="busy || reading || started"><option v-for="option in store.cardTypes" :key="option.value" :value="option.value">{{ option.label }}</option></select></label>
      <p v-if="restoring">已识别本站导出文件，将恢复 {{ items.length }} 个条目及其完整字段。</p>
      <template v-if="backup">
        <p>备份：{{ backup.project.name }} · {{ backup.images.size }} 张原图</p>
        <label><input v-model="restoreNew" type="checkbox" :disabled="busy || started" /> 恢复为新的私密世界（包含原名称和简介）</label>
        <p v-if="!restoreNew">仅向当前世界新增条目；名称、简介和公开状态不变。</p>
      </template>
      <button v-if="plainAvailable && !busy" @click="readPlainFile">按普通 Markdown 导入（不恢复属性和关系）</button>
      <p v-if="plainMode" role="status">正在按普通 Markdown 导入，仅保留阅读内容和图片；原类型、属性和关联关系不会恢复。</p>
      <ol><li v-for="(item,index) in items.slice(0,20)" :key="index"><strong>{{ item.title }}</strong><span>{{ linked.has(index)?'已完成':created.has(index)?'已创建，待处理引用':`${item.description.length} 字${item.payload ? ' · ' + getCardTypeLabel(item.payload.type) : ''}` }}</span><p>{{ previewDescriptions[index] }}</p></li></ol><p v-if="items.length > 20">预览前 20 个，共 {{ items.length }} 个条目；确认后会恢复全部条目。</p>
      <button class="primary" :disabled="busy || reading || uncertain || completed || (!items.length && !backup) || (!restoring && !backup && !type)" @click="importFile">{{ busy?'正在导入…':completed?'导入完成':created.size?'继续处理未完成内容':`确认新建 ${items.length} 个条目` }}</button>
    </template>
    <template v-else-if="mode==='export'">
      <p>完整备份包含世界名称、简介、全部条目、引用关系和原图，解压后可阅读 MD，重新导入 ZIP 可恢复。普通 Markdown 仍保留原图地址；需要离线保存图片时请选择完整备份。PDF 使用现有打印流程。</p>
      <div class="export-actions"><button class="primary" :disabled="busy" @click="exportFile('zip')">下载完整备份（含图片）</button><button :disabled="busy" @click="exportFile('md')">下载 Markdown</button><button :disabled="busy" @click="exportFile('pdf')">打印 / 保存 PDF</button></div>
    </template>
    <template v-if="printHtml"><iframe ref="printFrame" :srcdoc="printHtml" title="PDF 导出预览" sandbox="allow-same-origin allow-modals" @load="preparePreview" /><button :disabled="!printReady || busy" @click="printPdf">{{ printReady ? '打印 / 另存为 PDF' : '正在准备图片和字体…' }}</button><p>建议关闭打印窗口中的页眉和页脚，使用 A4 纸张、100% 缩放。</p></template>
    <a v-if="restoredProject && !busy" :href="`#/world/project/${restoredProject.id}`" @click="emit('close')">{{ completed ? '打开恢复的世界' : '检查已创建的世界' }} →</a>
    <p v-if="reading" role="status">正在读取和校验备份…</p><p v-if="status" role="status">{{ status }}</p><p v-if="error" role="alert" class="error">{{ error }}</p>
  </section></div></Teleport>
</template>
<script setup lang="ts">
import { computed, ref, shallowRef, reactive, onMounted, watch, onBeforeUnmount } from 'vue'
import { onBeforeRouteLeave, onBeforeRouteUpdate } from 'vue-router'
import type { WorldProject, CardDetail } from '@/api/types/world'
import { worldApi } from '@/api/worldApi'
import { useWorldStore } from '@/stores/world'
import { useUserStore } from '@/stores/user'
import { parseWorldText, resolveWorldImport, createWorldImportPayload, hasWorldRestoreData, worldMarkdown, type WorldTextItem } from '@/utils/worldTransfer'
import { worldPrintHtml, prepareWorldPrint } from '@/utils/worldPrint'
import { getCardTypeLabel } from '@/utils/worldPresentation'
import { createWorldBackup, readWorldBackup, replaceWorldImages, worldTextBytes, worldArchiveBytes, type WorldBackup } from '@/utils/worldBackup'
import { useCos } from '@/composables/useCos'

const props = defineProps<{ project: WorldProject; mode: 'import' | 'export'; canEdit: boolean }>()
const emit = defineEmits<{ close: [] }>()
const store = useWorldStore(), user = useUserStore()
const busy = ref(false), error = ref(''), status = ref(''), type = ref('')
const items = ref<WorldTextItem[]>([])
const backup = shallowRef<WorldBackup | null>(null), restoreNew = ref(true), restoredProject = ref<WorldProject | null>(null)
const uploadedImages = reactive(new Map<string,string>()), completed = ref(false), reading = ref(false)
const started = computed(() => created.size > 0 || uploadedImages.size > 0 || !!restoredProject.value)
const { uploadFile } = useCos()
const cancellation = new AbortController()
const restoring = computed(() => items.value.length > 0 && items.value.every(item => item.payload))
const previewDescriptions = computed(() => {
  const titles = new Map(items.value.map(item => [item.sourceId, item.title]))
  return items.value.slice(0,20).map(item => item.description.replace(/\{CARD:([^}]+)\}/g,
    (_, id: string) => `「${titles.get(id) || '未匹配的条目'}」`).slice(0, 160))
})
const plainAvailable = ref(false), plainMode = ref(false)
let fileText = '', fileName = '', fileVersion = 0
const created = reactive(new Map<number, CardDetail>()), linked = reactive(new Set<number>())
const uncertain = ref(false), printReady = ref(false), printHtml = ref('')
const printFrame = ref<HTMLIFrameElement | null>(null)
const account = user.userId, projectId = props.project.id
let disposed = false, previewVersion = 0
const valid = () => !disposed && user.userId === account && props.project.id === projectId
const holdRestore = () => !(busy.value && props.mode === 'import')
onBeforeRouteLeave(holdRestore)
onBeforeRouteUpdate(holdRestore)
function warnBeforeUnload(event: BeforeUnloadEvent) { if (!holdRestore()) { event.preventDefault(); event.returnValue = '' } }
onBeforeUnmount(() => { disposed = true; previewVersion++; cancellation.abort(); window.removeEventListener('beforeunload',warnBeforeUnload) })
watch([() => user.userId, () => props.project.id], () => {
  cancellation.abort(); fileVersion++; fileText = ''; backup.value = null; uploadedImages.clear(); items.value = []; created.clear(); linked.clear(); printHtml.value = ''; emit('close')
})
function close() { if (!busy.value) emit('close') }
onMounted(async () => {
  window.addEventListener('beforeunload',warnBeforeUnload)
  if (props.mode !== 'import') return
  await store.fetchCardTypes()
  if (valid()) type.value = store.cardTypes.find(t => t.value === 'lore')?.value || store.cardTypes[0]?.value || ''
})

async function readFile(event: Event) {
  const version = ++fileVersion
  error.value = ''; status.value = ''; items.value = []; uncertain.value = false
  plainAvailable.value = false; plainMode.value = false; fileText = ''; fileName = ''; backup.value = null; restoredProject.value = null; completed.value = false; uploadedImages.clear()
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  reading.value = true
  try {
    const zipped = /\.zip$/i.test(file.name)
    if (!/\.(txt|md|zip)$/i.test(file.name) || file.size > (zipped ? worldArchiveBytes : worldTextBytes))
      throw new Error('请选择不超过 64MB 的 TXT/MD，或不超过 256MB 的世界观 ZIP 备份。')
    const bytes = new Uint8Array(await file.arrayBuffer())
    if (zipped) {
      const result = await readWorldBackup(bytes)
      if (!valid() || version !== fileVersion) return
      backup.value = result; items.value = result.items; restoreNew.value = true
      return
    }
    const text = new TextDecoder('utf-8', { fatal: true }).decode(bytes)
    if (!valid() || version !== fileVersion) return
    fileText = text; fileName = file.name
    items.value = parseWorldText(text, file.name)
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

async function importFile() {
  if (busy.value || uncertain.value || !props.canEdit || !valid() || completed.value) return
  busy.value = true; error.value = ''
  let writePending = false, stage = 'check'
  let targetId = restoredProject.value?.id || projectId
  try {
    // 容量和权限不足时，在创建世界、上传图片和写条目之前停止。
    status.value = '正在检查权限与剩余容量…'
    if (backup.value && restoreNew.value && !restoredProject.value) {
      const quota = await worldApi.getQuota()
      if (!valid()) return
      if (quota.remainingWorldCount < 1) throw new Error('世界数量已达账号上限；可取消“恢复为新世界”，导入有剩余容量的现有世界。')
      if (items.value.length > quota.maxCardsPerWorld) throw new Error(`备份有 ${items.value.length} 个条目，当前账号每个世界最多 ${quota.maxCardsPerWorld} 个，尚未写入。`)
      writePending = true
      const project = await store.createProject({ name: backup.value.project.name, description: null, isPublic: false })
      if (!valid()) return
      restoredProject.value = project; targetId = project.id; writePending = false
    }
    const quota = await worldApi.canAddCard(targetId)
    if (!valid()) return
    if (items.value.length - created.size > quota.maxCount - quota.currentCount)
      throw new Error(`还需 ${items.value.length - created.size} 个条目位置，目标世界仅剩 ${quota.maxCount - quota.currentCount} 个；已暂停写入。`)
    stage = 'images'
    if (backup.value) {
      for (const [path,image] of backup.value.images) {
        if (uploadedImages.has(path)) continue
        if (!valid()) return
        status.value = `正在恢复原图 ${uploadedImages.size + 1} / ${backup.value.images.size}…`
        const result = await uploadFile(new File([image.bytes], path.split('/').pop()!, { type:image.type }), 'world/restored')
        if (!valid()) return
        uploadedImages.set(path,result.url)
      }
    }
    const source = backup.value ? replaceWorldImages(items.value, uploadedImages) : items.value
    stage = 'cards'
    for (const [index,item] of source.entries()) {
      if (created.has(index)) continue
      if (!valid()) return
      writePending = true
      const card = await worldApi.createCard(targetId, createWorldImportPayload(item,type.value))
      if (!valid()) return
      created.set(index,card); writePending = false
      status.value = `已创建 ${created.size} / ${source.length} 个条目，正在恢复引用…`
    }
    const resolved = resolveWorldImport(source,source.map((_,index)=>created.get(index)!.id))
    for (const [index,payload] of resolved.payloads.entries()) {
      if (linked.has(index)) continue
      if (!valid()) return
      const card=created.get(index)!
      if (source[index]?.payload || payload.description !== card.description) {
        writePending = true
        await worldApi.updateCard(targetId,card.id,{...payload,type:payload.type||type.value,expectedUpdatedAt:card.updatedAt})
        writePending = false
      }
      if (!valid()) return
      linked.add(index)
      status.value = `已恢复引用 ${linked.size} / ${source.length} 个条目…`
    }
    if (backup.value && restoredProject.value) {
      writePending = true
      await worldApi.updateProject(targetId,{ description:replaceWorldImages(backup.value.project,uploadedImages).description })
      writePending = false
    }
    if (!valid()) return
    completed.value = true
    status.value = `导入完成：${created.size} 个条目，${uploadedImages.size} 张原图。` + (resolved.unresolved
      ? `有 ${resolved.unresolved} 处引用未匹配，请在编辑页核对。` : '引用和关系已恢复。')
  } catch(e) {
    if (!valid()) return
    const code=(e as {response?:{status:number}}).response?.status
    uncertain.value=writePending && (!code||code>=500||code===409)
    error.value=code===409?'新条目已被其他操作修改，已停止恢复，请关闭窗口核对；未覆盖新内容。'
      :uncertain.value?'保存结果不确定，已停止恢复。请关闭窗口核对已创建的世界和条目，避免重复导入。'
      :stage==='images'?'原图上传失败，尚未开始写入条目。保留此窗口重试会复用已上传的图片。'
      :e instanceof Error?e.message:'导入已暂停；保留此窗口可继续，已成功的条目不会重复创建。'
  } finally {
    if (valid() && targetId===projectId) await store.fetchCards(projectId,true).catch(()=>{error.value+=' 列表刷新失败，请重新加载。'})
    busy.value=false
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
iframe{width:100%;height:380px;border:1px solid var(--border-line);margin:16px 0;background:white}
.transfer-mask{position:fixed;inset:0;z-index:2000;background:#0006;display:grid;place-items:center;padding:16px}.transfer-dialog{box-sizing:border-box;width:min(640px,100%);max-height:85vh;overflow:auto;padding:24px;background:var(--paper-card);color:var(--text-primary);border:1px solid var(--border-line);border-radius:10px}.transfer-dialog header{display:flex;align-items:center;justify-content:space-between}h2{margin:0;font-size:20px}p{font-size:13px;line-height:1.8;color:var(--text-secondary)}button,select,input{font:inherit;max-width:100%;box-sizing:border-box}button,select{padding:9px 12px;border:1px solid var(--border-line);border-radius:6px;background:var(--bg-main);color:var(--text-primary);cursor:pointer}button:disabled{opacity:.5;cursor:default}.export-actions{display:flex;gap:12px;flex-wrap:wrap}label{display:block;margin-top:16px}ol{padding-left:22px;max-height:32vh;overflow:auto;overscroll-behavior:contain}li{padding:12px 0;border-bottom:1px solid var(--border-line)}li span{margin-left:12px;font-size:12px;color:var(--text-secondary)}li p{white-space:pre-wrap;overflow-wrap:anywhere}.primary{background:var(--accent-color);color:var(--on-accent)}.error{color:var(--danger);overflow-wrap:anywhere}a{display:inline-block;margin-top:16px;color:var(--accent-color)}input[type=checkbox]{width:auto;margin-right:8px}
</style>
