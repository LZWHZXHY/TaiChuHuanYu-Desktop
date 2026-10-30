<template>
  <div class="card-ref-textarea">
    <EditorContent :editor="editor" class="doc-desc-input" />
    <!-- 选中了一个引用：切换显示方式 -->
    <div v-if="selectedRef && !disabled" class="ref-modebar" role="toolbar" aria-label="引用的显示方式">
      <span>「{{ selectedRef.label.replace(/^@/, '') }}」阅读时：</span>
      <button type="button" :aria-pressed="selectedRef.mode !== 'embed'" @mousedown.prevent="setRefMode('link')">只显示名字<small>点了在右边看详情</small></button>
      <button type="button" :aria-pressed="selectedRef.mode === 'embed'" @mousedown.prevent="setRefMode('embed')">显示全部内容<small>直接展开整个条目</small></button>
    </div>
    <p class="ref-help">输入 @ 或 [[ 选择设定（Shift+Enter 直接显示全部内容）；点一下已插入的引用可以切换显示方式。<template v-if="commands?.length">空行输入 / 添加图库、属性、时间线等。</template></p>
    <!-- / 命令：添加图库、属性、时间线…… -->
    <div v-if="cmdOpen" class="ref-dropdown" aria-label="添加内容">
      <div class="ref-dropdown-hint">添加内容 · ↑↓ 选择，Enter 添加，Esc 取消</div>
      <button v-for="(cmd, idx) in cmdMatches" :key="cmd.id" class="ref-item" type="button"
        :class="{ active: idx === activeIndex }" @mousedown.prevent="runCommand(cmd.id)" @mouseenter="activeIndex = idx">
        <span class="cmd-icon" aria-hidden="true">{{ cmd.icon }}</span>
        <span class="ref-title">{{ cmd.label }}</span>
        <span class="cmd-hint">{{ cmd.group === '内容' ? cmd.hint : cmd.group }}</span>
      </button>
      <button v-if="!cmdMatches.length && query.trim()" class="ref-item" type="button" @mousedown.prevent="runCommand('attr:' + query.trim())">
        <span class="cmd-icon" aria-hidden="true">≡</span><span class="ref-title">添加属性「{{ query.trim() }}」</span>
      </button>
    </div>
    <div v-if="dropdownOpen" class="ref-dropdown" aria-label="选择正文引用">
      <div class="ref-dropdown-hint">选择要引用的设定 · Enter 只插入名字，Shift+Enter 显示全部内容，Esc 取消</div>
      <div v-for="(card, idx) in matches" :key="card.id" class="ref-item" role="button"
        :class="{ active: idx === activeIndex }" @mousedown.prevent="selectCard(card)" @mouseenter="activeIndex = idx">
        <span class="ref-type">{{ getTypeLabel(card.type) }}</span>
        <span class="ref-title">{{ card.title }}</span>
        <button type="button" class="ref-embed" title="阅读时直接显示这个条目的全部内容" @mousedown.prevent.stop="selectCard(card, 'embed')">显示全部</button>
      </div>
      <div v-if="!matches.length" class="ref-empty">没有匹配的设定</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { EditorContent, useEditor } from '@tiptap/vue-3'
import { Node } from '@tiptap/core'
import { Fragment, Slice } from '@tiptap/pm/model'
import { NodeSelection } from '@tiptap/pm/state'
import StarterKit from '@tiptap/starter-kit'
import Placeholder from '@tiptap/extension-placeholder'
import { useWorldStore } from '@/stores/world'
import { getCardTypeLabel as getTypeLabel } from '@/utils/worldPresentation'
import { parseWorldEditorText, serializeWorldEditorText, worldReferenceLabel } from '@/utils/worldEditorText'
import type { CardSummary } from '@/api/types/world'

export interface SlashCommand { id: string; label: string; hint: string; icon: string; group: string; keywords?: string }
const props = defineProps<{ modelValue: string; projectId: string; excludeCardId?: string; placeholder?: string; rows?: number; disabled?: boolean; commands?: SlashCommand[] }>()
const emit = defineEmits<{ (e: 'update:modelValue', value: string): void; (e: 'command', id: string): void }>()
/** / 命令菜单（只有传了 commands 才有） */
const cmdOpen = ref(false)
const cmdMatches = computed(() => {
  const q = query.value.trim().toLowerCase()
  const list = props.commands ?? []
  return (q ? list.filter(c => `${c.label} ${c.hint} ${c.keywords ?? ''}`.toLowerCase().includes(q)) : list).slice(0, 12)
})
const store = useWorldStore()
const cards = computed(() => store.cards.filter(card => card.projectId === props.projectId))
const dropdownOpen = ref(false)
const triggerStart = ref(-1)
const activeIndex = ref(0)
const query = ref('')
const matches = computed(() => cards.value.filter(card => card.id !== props.excludeCardId &&
  card.title.toLowerCase().includes(query.value.trim().toLowerCase())).slice(0, 8))
const referenceAttrs = (id: string) => ({ id, label: worldReferenceLabel(id, cards.value) })
const reference = Node.create({
  name: 'worldReference', inline: true, group: 'inline', atom: true,
  // mode：link = 只显示名字；embed = 阅读时显示全部内容（存成单独一行的 !{CARD:id}）
  addAttributes: () => ({ id: { default: '' }, label: { default: '' }, mode: { default: 'link' } }),
  parseHTML: () => [{ tag: 'span[data-world-reference]', getAttrs: element => ({ ...referenceAttrs(element.getAttribute('data-world-reference') || ''), mode: element.getAttribute('data-mode') === 'embed' ? 'embed' : 'link' }) }],
  renderHTML: ({ node }) => ['span', { 'data-world-reference': node.attrs.id, 'data-mode': node.attrs.mode, class: node.attrs.mode === 'embed' ? 'world-reference is-embed' : 'world-reference', contenteditable: 'false' },
    node.attrs.mode === 'embed' ? `⧉ ${String(node.attrs.label).replace(/^@/, '')} · 阅读时显示全部内容` : node.attrs.label],
  renderText: ({ node }) => node.attrs.label,
})
const editor = useEditor({
  extensions: [StarterKit.configure({ bold: false, italic: false, underline: false, strike: false, code: false,
    codeBlock: false, blockquote: false, heading: false, horizontalRule: false, link: false,
    bulletList: false, orderedList: false, listItem: false, listKeymap: false, trailingNode: false }), reference,
    Placeholder.configure({ placeholder: props.placeholder || '写下设定，输入 @ 引用其他条目…' })],
  content: parseWorldEditorText(props.modelValue, cards.value),
  editable: !props.disabled,
  editorProps: {
    attributes: { role: 'textbox', 'aria-label': '设定正文', 'aria-multiline': 'true', 'data-placeholder': props.placeholder || '写下设定，输入 @ 引用其他条目…' },
    handleKeyDown: (_view, event) => {
      if (event.isComposing || _view.composing) return false
      if (cmdOpen.value) {
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
          activeIndex.value = Math.max(0, Math.min(cmdMatches.value.length - 1, activeIndex.value + (event.key === 'ArrowDown' ? 1 : -1)))
        } else if (event.key === 'Enter') {
          const cmd = cmdMatches.value[activeIndex.value]
          // 没有匹配就当普通回车（比如正文本来就以 / 开头），不吞掉换行
          if (!cmd) { closeDropdown(); return false }
          runCommand(cmd.id)
        } else if (event.key === 'Escape') closeDropdown()
        else return false
        return true
      }
      if (!dropdownOpen.value) return false
      if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
        activeIndex.value = Math.max(0, Math.min(matches.value.length - 1, activeIndex.value + (event.key === 'ArrowDown' ? 1 : -1)))
      } else if (event.key === 'Enter') {
        const card = matches.value[activeIndex.value]
        if (card) selectCard(card, event.shiftKey ? 'embed' : 'link')
      } else if (event.key === 'Escape') closeDropdown()
      else return false
      return true
    },
    handlePaste: (_view, event) => {
      const data = event.clipboardData
      if (!data || data.getData('text/html').includes('data-world-reference')) return false
      const text = data.getData('text/plain')
      if (!text) return false
      const paragraphs = parseWorldEditorText(text, cards.value).content || []
      const slice = new Slice(Fragment.fromArray(paragraphs.map(node => _view.state.schema.nodeFromJSON(node))), 1, 1)
      _view.dispatch(_view.state.tr.replaceSelection(slice).scrollIntoView())
      return true
    },
  },
  // 只把真正的内容变化同步出去：切换可编辑状态等操作也会触发 update，
  // 那时编辑器里可能还是旧内容（比如空白），直接同步会把刚载入的正文清空。
  onUpdate: ({ editor }) => {
    const text = serializeWorldEditorText(editor.getJSON())
    if (text !== props.modelValue && editor.isEditable) emit('update:modelValue', text)
    detectTrigger()
  },
  onSelectionUpdate: () => { detectTrigger(); detectSelectedRef() },
  onBlur: () => { closeDropdown(); selectedRef.value = null },
})
function closeDropdown() { dropdownOpen.value = false; cmdOpen.value = false; triggerStart.value = -1 }
function detectTrigger() {
  const current = editor.value
  if (!current || props.disabled || current.view.composing || !current.state.selection.empty) return closeDropdown()
  const { $from, from } = current.state.selection
  const before = $from.parent.textBetween(0, $from.parentOffset, undefined, '\ufffc')
  // / 命令：在行首（或空格后）输入 /；中文输入法下 / 常常打出「、」，行首的「、」也算
  const slash = props.commands?.length ? before.match(/(?:^|\s)([/／]|^、)([^\s/／\ufffc]{0,12})$/) : null
  if (slash) {
    const text = slash[1] + slash[2]
    triggerStart.value = from - text.length
    if (query.value !== slash[2]) activeIndex.value = 0
    query.value = slash[2] || ''
    dropdownOpen.value = false
    cmdOpen.value = true
    return
  }
  cmdOpen.value = false
  const match = before.match(/(?:@|\[\[)([^@\[\]\n\ufffc]*)$/)
  if (!match) return closeDropdown()
  triggerStart.value = from - match[0].length
  if (query.value !== match[1]) activeIndex.value = 0
  query.value = match[1] || ''
  dropdownOpen.value = true
}
function selectCard(card: CardSummary, mode: 'link' | 'embed' = 'link') {
  const current = editor.value
  if (!current || props.disabled || triggerStart.value < 0) return
  current.chain().focus().insertContentAt({ from: triggerStart.value, to: current.state.selection.to },
    { type: 'worldReference', attrs: { ...referenceAttrs(card.id), mode } }).run()
  closeDropdown()
}
// 点中了一个已插入的引用：显示切换条
const selectedRef = ref<{ pos: number; label: string; mode: string } | null>(null)
function detectSelectedRef() {
  const sel = editor.value?.state.selection
  selectedRef.value = sel instanceof NodeSelection && sel.node.type.name === 'worldReference'
    ? { pos: sel.from, label: String(sel.node.attrs.label), mode: String(sel.node.attrs.mode) } : null
}
function setRefMode(mode: 'link' | 'embed') {
  const current = editor.value
  const at = selectedRef.value
  if (!current || !at || props.disabled) return
  const node = current.state.doc.nodeAt(at.pos)
  if (!node || node.type.name !== 'worldReference') return
  const tr = current.state.tr.setNodeMarkup(at.pos, undefined, { ...node.attrs, mode })
  current.view.dispatch(tr.setSelection(NodeSelection.create(tr.doc, at.pos)))
}
function runCommand(id: string) {
  const current = editor.value
  if (!current || props.disabled || triggerStart.value < 0) return
  // 删掉输入的「/xxx」，再交给外面去添加对应的内容
  current.chain().deleteRange({ from: triggerStart.value, to: current.state.selection.to }).run()
  closeDropdown()
  emit('command', id)
}
watch(() => props.modelValue, value => {
  if (editor.value && serializeWorldEditorText(editor.value.getJSON()) !== value) {
    editor.value.commands.setContent(parseWorldEditorText(value, cards.value), { emitUpdate: false })
    closeDropdown()
  }
})
// 第二个参数 false：切换可编辑状态时不要触发 update（tiptap 默认会触发，导致用旧内容覆盖正文）
watch(() => props.disabled, disabled => { editor.value?.setEditable(!disabled, false); if (disabled) closeDropdown() })
watch(cards, () => {
  const current = editor.value
  if (!current) return
  const tr = current.state.tr
  current.state.doc.descendants((node, pos) => {
    if (node.type.name === 'worldReference') {
      const attrs = referenceAttrs(node.attrs.id)
      if (attrs.label !== node.attrs.label) tr.setNodeMarkup(pos, undefined, { ...node.attrs, ...attrs })
    }
  })
  if (tr.docChanged) current.view.dispatch(tr.setMeta('addToHistory', false))
}, { deep: true })
</script>

<style scoped>
.card-ref-textarea { position: relative; }
.doc-desc-input :deep(.tiptap) {
  width: 100%;
  min-height: 96px;
  padding: 10px 12px;
  border: 1px solid var(--border-line);
  border-radius: 0;
  font: inherit;
  font-size: 14px;
  line-height: 1.7;
  color: var(--text-primary);
  background: var(--paper-card);
  overflow-wrap: anywhere;
  white-space: pre-wrap;
  box-sizing: border-box;
}
.doc-desc-input :deep(.tiptap:focus) {
  outline: none;
  border-color: var(--accent-color);
}
.ref-dropdown {
  position: absolute;
  left: 0;
  right: 0;
  top: 100%;
  margin-top: 4px;
  z-index: 100;
  background: var(--paper-card);
  border: 1px solid var(--border-line);
  border-radius: 0;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.08);
  max-height: 240px;
  overflow-y: auto;
}
.ref-dropdown-hint {
  padding: 6px 12px;
  font-size: 11px;
  color: var(--text-secondary);
  border-bottom: 1px solid var(--border-line);
}
.ref-item {
  width: 100%; border: 0; background: transparent; text-align: left; font: inherit;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 7px 12px;
  cursor: pointer;
}
.ref-item.active,
.ref-item:hover {
  background: var(--accent-soft);
}
.ref-type {
  font-size: 11px;
  color: var(--accent-color);
  background: var(--accent-soft);
  padding: 0 8px;
  border-radius: 0;
  flex-shrink: 0;
}
.ref-title {
  font-size: 13px;
  color: var(--text-primary);
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.ref-empty {
  padding: 10px 12px;
  font-size: 12px;
  color: var(--text-secondary);
  text-align: center;
}
.doc-desc-input :deep(.tiptap p) { margin: 0; min-height: 1.7em; }
.doc-desc-input :deep(.tiptap p.is-editor-empty:first-child)::before { content: attr(data-placeholder); float: left; height: 0; color: var(--text-secondary); pointer-events: none; }
.doc-desc-input :deep(.world-reference) { display: inline; padding: 2px 6px; border-radius: 0; background: var(--accent-soft); color: var(--accent-color); box-decoration-break: clone; }
.doc-desc-input :deep(.ProseMirror-selectednode) { outline: 2px solid var(--accent-color); }
.doc-desc-input :deep([contenteditable=false]) { cursor: default; }
.ref-item .ref-embed { flex-shrink: 0; padding: 1px 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); font: inherit; font-size: 11px; cursor: pointer; }
.ref-item .ref-embed:hover { color: var(--accent-color); border-color: var(--accent-color); }
.doc-desc-input :deep(.world-reference.is-embed) { display: block; margin: 4px 0; padding: 8px 12px; border: 1px dashed var(--accent-color); background: var(--accent-soft); font-size: 13px; }
.doc-desc-input :deep(.world-reference.ProseMirror-selectednode) { outline: 2px solid var(--accent-color); outline-offset: 1px; }
.ref-modebar { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; margin-top: 6px; padding: 6px 10px; border: 1px solid var(--border-line); background: var(--bg-sub); font-size: 12px; color: var(--text-secondary); }
.ref-modebar button { display: grid; padding: 4px 10px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 12px; text-align: left; cursor: pointer; }
.ref-modebar button small { font-size: 10px; color: var(--text-secondary); }
.ref-modebar button[aria-pressed=true] { border-color: var(--accent-color); color: var(--accent-color); background: var(--accent-soft); }
.cmd-icon { width: 20px; flex-shrink: 0; text-align: center; color: var(--accent-color); }
.cmd-hint { flex-shrink: 0; max-width: 45%; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 11px; color: var(--text-secondary); }
.ref-help { margin: 8px 0 0; font-size: 12px; color: var(--text-secondary); line-height: 1.6; }
</style>
