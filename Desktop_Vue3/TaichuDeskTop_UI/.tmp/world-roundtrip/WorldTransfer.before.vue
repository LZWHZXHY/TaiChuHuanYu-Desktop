<template>
  <Teleport to="body"><div class="transfer-mask" @click.self="close"><section role="dialog" aria-modal="true" :aria-label="mode==='import'?'导入世界观文件':'导出世界观'" class="transfer-dialog" @keydown.esc="close">
    <header><h2>{{ mode==='import'?'导入世界观文件':'导出世界观' }}</h2><button :disabled="busy" aria-label="关闭导入导出" @click="close">×</button></header>
    <template v-if="mode==='import' && canEdit">
      <p>支持 UTF-8 编码的 TXT、Markdown。按二级标题分成条目；没有二级标题时，整份文件作为一个条目。仅新建，不覆盖已有内容；文件内的标题链接会转换为条目引用。</p>
      <input type="file" accept=".txt,.md,text/plain,text/markdown" aria-label="选择世界观文件" :disabled="busy || created.size > 0" @change="readFile" />
      <label v-if="items.length">新条目类型 <select v-model="type" :disabled="busy || created.size > 0"><option v-for="option in store.cardTypes" :key="option.value" :value="option.value">{{ option.label }}</option></select></label>
      <ol><li v-for="(item,index) in items" :key="index"><strong>{{ item.title }}</strong><span>{{ linked.has(index)?'已完成':created.has(index)?'已创建，待处理引用':`${item.description.length} 字` }}</span><p>{{ item.description.slice(0,160) }}</p></li></ol>
      <button class="primary" :disabled="busy || uncertain || !items.length || linked.size===items.length || !type" @click="importFile">{{ busy?'正在导入…':items.length && linked.size===items.length?'导入完成':created.size?'继续处理未完成内容':`确认新建 ${items.length} 个条目` }}</button>
    </template>
    <template v-else-if="mode==='export'">
      <p>导出当前世界的正文、属性、关系和时间线。Markdown 保留图片地址；PDF 会加载图片并按 A4 分页。请在打印窗口选择“另存为 PDF”；文件只保存在你的设备。</p>
      <div class="export-actions"><button :disabled="busy" @click="exportFile('md')">下载 Markdown</button><button :disabled="busy" @click="exportFile('pdf')">打印 / 保存 PDF</button></div>
    </template>
    <template v-if="printHtml"><iframe ref="printFrame" :srcdoc="printHtml" title="PDF 导出预览" sandbox="allow-same-origin allow-modals" @load="preparePreview" /><button :disabled="!printReady || busy" @click="printPdf">{{ printReady ? '打印 / 另存为 PDF' : '正在准备图片和字体…' }}</button><p>建议关闭打印窗口中的页眉和页脚，使用 A4 纸张、100% 缩放。</p></template>
    <p v-if="status" role="status">{{ status }}</p><p v-if="error" role="alert" class="error">{{ error }}</p>
  </section></div></Teleport>
</template>
<script setup lang="ts">
import { ref, reactive, onMounted, watch, onBeforeUnmount } from 'vue'
import type { WorldProject, CardDetail } from '@/api/types/world'
import { worldApi } from '@/api/worldApi'
import { useWorldStore } from '@/stores/world'
import { useUserStore } from '@/stores/user'
import { parseWorldText, resolveWorldImport, worldMarkdown, type WorldTextItem } from '@/utils/worldTransfer'
import { worldPrintHtml, prepareWorldPrint } from '@/utils/worldPrint'

const props = defineProps<{ project: WorldProject; mode: 'import' | 'export'; canEdit: boolean }>()
const emit = defineEmits<{ close: [] }>()
const store = useWorldStore(), user = useUserStore()
const busy = ref(false), error = ref(''), status = ref(''), type = ref('')
const items = ref<WorldTextItem[]>([])
const created = reactive(new Map<number, CardDetail>()), linked = reactive(new Set<number>())
const uncertain = ref(false), printReady = ref(false), printHtml = ref('')
const printFrame = ref<HTMLIFrameElement | null>(null)
const account = user.userId, projectId = props.project.id
let disposed = false, previewVersion = 0
const valid = () => !disposed && user.userId === account && props.project.id === projectId
onBeforeUnmount(() => { disposed = true; previewVersion++ })
watch([() => user.userId, () => props.project.id], () => {
  items.value = []; created.clear(); linked.clear(); printHtml.value = ''; emit('close')
})
function close() { if (!busy.value) emit('close') }
onMounted(async () => {
  if (props.mode !== 'import') return
  await store.fetchCardTypes()
  if (valid()) type.value = store.cardTypes.find(t => t.value === 'lore')?.value || store.cardTypes[0]?.value || ''
})

async function readFile(event: Event) {
  error.value = ''; status.value = ''; items.value = []; uncertain.value = false
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  try {
    if (!/\.(txt|md)$/i.test(file.name) || file.size > 1024 * 1024)
      throw new Error('请选择不超过 1MB 的 TXT 或 Markdown 文件。')
    const text = new TextDecoder('utf-8', { fatal: true }).decode(await file.arrayBuffer())
    if (valid()) items.value = parseWorldText(text, file.name)
  } catch (e) { error.value = e instanceof Error ? e.message : '文件读取失败' }
}

async function importFile() {
  if (busy.value || uncertain.value || !props.canEdit || !valid()) return
  busy.value = true; error.value = ''
  try {
    // 先创建全部条目，获得新 ID 后再写入引用，支持前向引用与互相引用。
    for (const [index, item] of items.value.entries()) {
      if (created.has(index)) continue
      if (!valid()) return
      const card = await worldApi.createCard(projectId, {
        title: item.title, description: item.description, type: type.value, galleryImages: item.images || [],
      })
      if (!valid()) return
      created.set(index, card)
      status.value = `已创建 ${created.size} / ${items.value.length} 个条目，正在处理引用…`
    }
    const resolved = resolveWorldImport(items.value, items.value.map((_, index) => created.get(index)!.id))
    for (const [index, description] of resolved.descriptions.entries()) {
      if (linked.has(index)) continue
      if (!valid()) return
      const card = created.get(index)!
      if (description !== card.description) {
        // 只更新本次新建条目；其他标签页已改动时由后端拒绝覆盖。
        await worldApi.updateCard(projectId, card.id, { description, expectedUpdatedAt: card.updatedAt })
      }
      if (!valid()) return
      linked.add(index)
    }
    status.value = `导入完成：${created.size} 个条目。` + (resolved.unresolved
      ? `有 ${resolved.unresolved} 处引用未匹配，已保留提示文字，请在编辑页核对。` : '文件内引用已处理。')
  } catch (e) {
    const code = (e as { response?: { status: number } }).response?.status
    uncertain.value = !code || code >= 500 || code === 409
    error.value = code === 409 ? '新条目已被其他操作修改，已停止写入引用。请关闭窗口核对，未覆盖新内容。'
      : uncertain.value ? '保存结果不确定，已停止导入。请关闭窗口核对列表与引用，避免重复创建。'
      : '导入已暂停，已成功的条目不会重复创建。检查权限和容量后可继续。'
  } finally {
    if (valid()) await store.fetchCards(projectId, true).catch(() => { error.value += ' 列表刷新失败，请重新加载。' })
    busy.value = false
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

async function exportFile(format: 'md' | 'pdf') {
  if (busy.value || !valid()) return
  busy.value = true; error.value = ''; status.value = '正在读取条目…'
  printReady.value = false; printHtml.value = ''; previewVersion++
  try {
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
    const markdown = worldMarkdown(props.project, cards)
    if (format === 'md') {
      const url = URL.createObjectURL(new Blob([markdown], { type: 'text/markdown;charset=utf-8' }))
      const a = document.createElement('a')
      a.href = url
      a.download = props.project.name.replace(/[<>:"/\\|?*]/g, '_') + '.md'
      a.click()
      setTimeout(() => URL.revokeObjectURL(url), 1000)
      status.value = 'Markdown 已生成。'
    } else {
      printHtml.value = worldPrintHtml(props.project.name, markdown)
      status.value = '正在准备图片和字体…'
    }
  } catch (e) { error.value = e instanceof Error ? e.message : '导出失败，请重试' }
  finally { busy.value = false }
}
</script>
<style scoped>
iframe{width:100%;height:380px;border:1px solid var(--border-line);margin:16px 0;background:white}
.transfer-mask{position:fixed;inset:0;z-index:2000;background:#0006;display:grid;place-items:center;padding:16px}.transfer-dialog{box-sizing:border-box;width:min(640px,100%);max-height:85vh;overflow:auto;padding:24px;background:var(--paper-card);color:var(--text-primary);border:1px solid var(--border-line);border-radius:10px}.transfer-dialog header{display:flex;align-items:center;justify-content:space-between}h2{margin:0;font-size:20px}p{font-size:13px;line-height:1.8;color:var(--text-secondary)}button,select,input{font:inherit;max-width:100%;box-sizing:border-box}button,select{padding:9px 12px;border:1px solid var(--border-line);border-radius:6px;background:var(--bg-main);color:var(--text-primary);cursor:pointer}button:disabled{opacity:.5;cursor:default}.export-actions{display:flex;gap:12px;flex-wrap:wrap}label{display:block;margin-top:16px}ol{padding-left:22px}li{padding:12px 0;border-bottom:1px solid var(--border-line)}li span{margin-left:12px;font-size:12px;color:var(--text-secondary)}li p{white-space:pre-wrap;overflow-wrap:anywhere}.primary{background:var(--accent-color);color:var(--on-accent)}.error{color:var(--danger)}
</style>
