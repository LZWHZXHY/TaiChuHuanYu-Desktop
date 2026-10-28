from pathlib import Path
p=Path('src/views/世界观管理/世界观组件/WorldTransfer.vue');s=p.read_text(encoding='utf-8')
s=s.replace('支持 UTF-8 编码的 TXT、Markdown。按二级标题分成条目；没有二级标题时，整份文件作为一个条目。仅新建，不覆盖已有内容；文件内的标题链接会转换为条目引用。本站新导出的 MD 会恢复原类型、属性、图片、时间线和关系；图片仍使用原地址。','支持世界观 ZIP 备份包和 UTF-8 的 TXT、Markdown。备份包包含原图；恢复时会上传图片并重新关联条目。TXT/MD 最大 64MB，ZIP 最大 256MB；条目数量按账号配额检查，不再限制 50 条。仅新建，不覆盖已有条目。')
s=s.replace('accept=".txt,.md,text/plain,text/markdown"','accept=".zip,.txt,.md,application/zip,text/plain,text/markdown"').replace(':disabled="busy || created.size > 0"',':disabled="busy || reading || started"')
s=s.replace('<p v-if="restoring">已识别本站导出文件，将恢复 {{ items.length }} 个条目及其完整字段。目标世界的名称、简介和公开状态不变。</p>', '''<p v-if="restoring">已识别本站导出文件，将恢复 {{ items.length }} 个条目及其完整字段。</p>
      <template v-if="backup">
        <p>备份：{{ backup.project.name }} · {{ backup.images.size }} 张原图</p>
        <label><input v-model="restoreNew" type="checkbox" :disabled="busy || started" /> 恢复为新的私密世界（包含原名称和简介）</label>
        <p v-if="!restoreNew">仅向当前世界新增条目；名称、简介和公开状态不变。</p>
      </template>''')
s=s.replace('v-for="(item,index) in items"','v-for="(item,index) in items.slice(0,20)"')
s=s.replace('</li></ol>','</li></ol><p v-if="items.length > 20">预览前 20 个，共 {{ items.length }} 个条目；确认后会恢复全部条目。</p>')
s=s.replace('busy || uncertain || !items.length || linked.size===items.length || (!restoring && !type)',"busy || reading || uncertain || completed || (!items.length && !backup) || (!restoring && !backup && !type)")
s=s.replace("items.length && linked.size===items.length?'导入完成'","completed?'导入完成'")
s=s.replace('导出当前世界的正文、属性、关系和时间线。Markdown 保留图片地址和用于再次导入的条目数据，请勿删除文件末尾的恢复数据。PDF 会加载图片并按 A4 分页。请在打印窗口选择“另存为 PDF”；文件只保存在你的设备。','完整备份包含世界名称、简介、全部条目、引用关系和原图，解压后可阅读 MD，重新导入 ZIP 可恢复。普通 Markdown 仍保留原图地址；需要离线保存图片时请选择完整备份。PDF 使用现有打印流程。')
s=s.replace('<div class="export-actions">','<div class="export-actions"><button class="primary" :disabled="busy" @click="exportFile(\'zip\')">下载完整备份（含图片）</button>')
s=s.replace('<p v-if="status" role="status">','<a v-if="completed && restoredProject" :href="`#/world/project/${restoredProject.id}`" @click="emit(\'close\')">打开恢复的世界 →</a>\n    <p v-if="reading" role="status">正在读取和校验备份…</p><p v-if="status" role="status">')
s=s.replace("import { computed, ref, reactive, onMounted, watch, onBeforeUnmount }", "import { computed, ref, shallowRef, reactive, onMounted, watch, onBeforeUnmount }")
s=s.replace("import { getCardTypeLabel } from '@/utils/worldPresentation'", "import { getCardTypeLabel } from '@/utils/worldPresentation'\nimport { createWorldBackup, readWorldBackup, replaceWorldImages, worldTextBytes, worldArchiveBytes, type WorldBackup } from '@/utils/worldBackup'\nimport { useCos } from '@/composables/useCos'")
s=s.replace("const busy = ref(false)","const busy = ref(false)")
s=s.replace("const items = ref<WorldTextItem[]>([])","""const items = ref<WorldTextItem[]>([])
const backup = shallowRef<WorldBackup | null>(null), restoreNew = ref(true), restoredProject = ref<WorldProject | null>(null)
const uploadedImages = new Map<string,string>(), completed = ref(false), reading = ref(false)
const started = computed(() => created.size > 0 || uploadedImages.size > 0 || !!restoredProject.value)
const { uploadFile } = useCos()
const cancellation = new AbortController()""")
s=s.replace("onBeforeUnmount(() => { disposed = true; previewVersion++ })", "onBeforeUnmount(() => { disposed = true; previewVersion++; cancellation.abort() })")
s=s.replace("fileVersion++; fileText = '';", "cancellation.abort(); fileVersion++; fileText = ''; backup.value = null; uploadedImages.clear();")
s=s.replace("plainAvailable.value = false; plainMode.value = false; fileText = ''; fileName = ''", "plainAvailable.value = false; plainMode.value = false; fileText = ''; fileName = ''; backup.value = null; restoredProject.value = null; completed.value = false; uploadedImages.clear()")
s=s.replace("  try {\n    if (!/\\.(txt|md)$/i.test(file.name) || file.size > 1024 * 1024)\n      throw new Error('请选择不超过 1MB 的 TXT 或 Markdown 文件。')\n    const text = new TextDecoder('utf-8', { fatal: true }).decode(await file.arrayBuffer())", """  reading.value = true
  try {
    const zipped = /\\.zip$/i.test(file.name)
    if (!/\\.(txt|md|zip)$/i.test(file.name) || file.size > (zipped ? worldArchiveBytes : worldTextBytes))
      throw new Error('请选择不超过 64MB 的 TXT/MD，或不超过 256MB 的世界观 ZIP 备份。')
    const bytes = new Uint8Array(await file.arrayBuffer())
    if (zipped) {
      const result = await readWorldBackup(bytes)
      if (!valid() || version !== fileVersion) return
      backup.value = result; items.value = result.items; restoreNew.value = true
      return
    }
    const text = new TextDecoder('utf-8', { fatal: true }).decode(bytes)""")
s=s.replace('    plainAvailable.value = hasWorldRestoreData(fileText)\n  }','    plainAvailable.value = hasWorldRestoreData(fileText)\n  } finally { if (version === fileVersion) reading.value = false }')
a=s.index('async function importFile()');b=s.index('\nasync function preparePreview()',a)
s=s[:a]+'''async function importFile() {
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
''' +s[b:]
s=s.replace("async function exportFile(format: 'md' | 'pdf')", "async function exportFile(format: 'md' | 'pdf' | 'zip')")
s=s.replace("    const summaries = await worldApi.getCards(projectId), cards: CardDetail[] = []", "    const sourceProject = await worldApi.getProject(projectId)\n    const summaries = await worldApi.getCards(projectId), cards: CardDetail[] = []")
s=s.replace("    const markdown = worldMarkdown(props.project, cards, format === 'md')", """    const latest = await worldApi.getCards(projectId)
    if (latest.length !== summaries.length || latest.some(c => !summaries.some(s => s.id===c.id && s.updatedAt===c.updatedAt))) throw new Error('导出期间条目发生变化，请重新导出。')
    if (format === 'zip') {
      const result = await createWorldBackup(sourceProject,cards,message => { if(valid()) status.value=message },{signal:cancellation.signal})
      if (!valid()) return
      download(new Blob([result.bytes],{type:'application/zip'}), sourceProject.name+'.zip')
      status.value=`完整备份已生成：${cards.length} 个条目，${result.imageCount} 张原图。`
      return
    }
    const markdown = worldMarkdown(sourceProject, cards, format === 'md')""")
start=s.index("      const url = URL.createObjectURL(new Blob([markdown]");end=s.index("      status.value = 'Markdown 已生成。'",start)
s=s[:start]+"      download(new Blob([markdown],{type:'text/markdown;charset=utf-8'}),sourceProject.name+'.md')\n"+s[end:]
s=s.replace('</script>', '''function download(blob: Blob, name: string) {
  const url=URL.createObjectURL(blob),a=document.createElement('a')
  a.href=url; a.download=name.replace(/[<>:"/\\\\|?*]/g,'_'); a.click()
  setTimeout(()=>URL.revokeObjectURL(url),1000)
}
</script>''')
s=s.replace('.error{color:var(--danger)}','.error{color:var(--danger);overflow-wrap:anywhere}a{display:inline-block;margin-top:16px;color:var(--accent-color)}input[type=checkbox]{width:auto;margin-right:8px}')
p.write_text(s,encoding='utf-8')
