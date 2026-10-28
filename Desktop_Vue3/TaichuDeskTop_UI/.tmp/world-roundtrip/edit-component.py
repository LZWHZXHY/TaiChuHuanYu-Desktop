from pathlib import Path
p=Path('src/views/世界观管理/世界观组件/WorldTransfer.vue');s=p.read_text(encoding='utf-8')
s=s.replace("import { ref, reactive, onMounted, watch, onBeforeUnmount }", "import { computed, ref, reactive, onMounted, watch, onBeforeUnmount }")
s=s.replace('parseWorldText, resolveWorldImport, worldMarkdown, type WorldTextItem','parseWorldText, resolveWorldImport, createWorldImportPayload, hasWorldRestoreData, worldMarkdown, type WorldTextItem')
s=s.replace('文件内的标题链接会转换为条目引用。','文件内的标题链接会转换为条目引用。本站新导出的 MD 会恢复原类型、属性、图片、时间线和关系；图片仍使用原地址。')
s=s.replace('<label v-if="items.length">新条目类型','<label v-if="items.some(item => !item.payload)">普通条目类型')
s=s.replace('<ol><li', '<p v-if="restoring">已识别本站导出文件，将恢复 {{ items.length }} 个条目及其完整字段。目标世界的名称、简介和公开状态不变。</p>\n      <button v-if="plainAvailable && !busy" @click="readPlainFile">按普通 Markdown 导入（不恢复属性和关系）</button>\n      <p v-if="plainMode" role="status">正在按普通 Markdown 导入，仅保留阅读内容和图片；原类型、属性和关联关系不会恢复。</p>\n      <ol><li')
s=s.replace("${item.description.length} 字", "${item.description.length} 字${item.payload ? ' · ' + item.payload.type : ''}")
s=s.replace('Markdown 保留图片地址；PDF','Markdown 保留图片地址和用于再次导入的条目数据，请勿删除文件末尾的恢复数据。PDF')
s=s.replace("const items = ref<WorldTextItem[]>([])", "const items = ref<WorldTextItem[]>([])\nconst restoring = computed(() => items.value.length > 0 && items.value.every(item => item.payload))\nconst plainAvailable = ref(false), plainMode = ref(false)\nlet fileText = '', fileName = '', fileVersion = 0")
s=s.replace("items.value = []; created.clear(); linked.clear(); printHtml.value = ''; emit('close')", "fileVersion++; fileText = ''; items.value = []; created.clear(); linked.clear(); printHtml.value = ''; emit('close')")
s=s.replace("  error.value = ''; status.value = ''; items.value = []; uncertain.value = false\n", "  const version = ++fileVersion\n  error.value = ''; status.value = ''; items.value = []; uncertain.value = false\n  plainAvailable.value = false; plainMode.value = false; fileText = ''; fileName = ''\n")
s=s.replace("    if (valid()) items.value = parseWorldText(text, file.name)\n  } catch (e) { error.value = e instanceof Error ? e.message : '文件读取失败' }", "    if (!valid() || version !== fileVersion) return\n    fileText = text; fileName = file.name\n    items.value = parseWorldText(text, file.name)\n  } catch (e) {\n    if (!valid() || version !== fileVersion) return\n    error.value = e instanceof Error ? e.message : '文件读取失败'\n    plainAvailable.value = hasWorldRestoreData(fileText)\n  }")
s=s.replace('\nasync function importFile() {', '''
function readPlainFile() {
  try {
    items.value = parseWorldText(fileText, fileName, true)
    plainMode.value = true; plainAvailable.value = false; error.value = ''
  } catch (e) { error.value = e instanceof Error ? e.message : '文件读取失败' }
}

async function importFile() {''')
s=s.replace("      const card = await worldApi.createCard(projectId, {\n        title: item.title, description: item.description, type: type.value, galleryImages: item.images || [],\n      })", "      const card = await worldApi.createCard(projectId, createWorldImportPayload(item, type.value))")
s=s.replace("    for (const [index, description] of resolved.descriptions.entries()) {", "    for (const [index, payload] of resolved.payloads.entries()) {")
s=s.replace("      if (description !== card.description) {", "      if (items.value[index]?.payload || payload.description !== card.description) {")
s=s.replace("await worldApi.updateCard(projectId, card.id, { description, expectedUpdatedAt: card.updatedAt })", "await worldApi.updateCard(projectId, card.id, { ...payload, type: payload.type || type.value, expectedUpdatedAt: card.updatedAt })")
s=s.replace("const markdown = worldMarkdown(props.project, cards)", "const markdown = worldMarkdown(props.project, cards, format === 'md')")
p.write_text(s,encoding='utf-8')
