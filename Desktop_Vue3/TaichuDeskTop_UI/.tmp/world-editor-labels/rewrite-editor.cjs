const fs=require('node:fs');
const p='src/views/世界观管理/世界观组件/CardRefTextarea.vue';
const old=fs.readFileSync(p,'utf8');
const css=old.slice(old.indexOf('<style scoped>'))
 .replace('.doc-desc-input {','.doc-desc-input :deep(.tiptap) {')
 .replace('  resize: vertical;','  overflow-wrap: anywhere;\n  white-space: pre-wrap;')
 .replace('.doc-desc-input:focus {','.doc-desc-input :deep(.tiptap:focus) {');
const source=`<template>
  <div class="card-ref-textarea">
    <EditorContent :editor="editor" class="doc-desc-input" />
    <p class="ref-help">输入 @ 或 [[ 选择设定。引用显示名称，重命名后仍指向原条目。</p>
    <div v-if="dropdownOpen" class="ref-dropdown" aria-label="选择正文引用">
      <div class="ref-dropdown-hint">选择要引用的设定 · ↑↓ 选择，Enter 插入，Esc 取消</div>
      <button v-for="(card, idx) in matches" :key="card.id" class="ref-item" type="button"
        :class="{ active: idx === activeIndex }" @mousedown.prevent="selectCard(card)" @mouseenter="activeIndex = idx">
        <span class="ref-type">{{ getTypeLabel(card.type) }}</span>
        <span class="ref-title">{{ card.title }}</span>
      </button>
      <div v-if="!matches.length" class="ref-empty">没有匹配的设定</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { EditorContent, useEditor } from '@tiptap/vue-3'
import { Node } from '@tiptap/core'
import StarterKit from '@tiptap/starter-kit'
import { useWorldStore } from '@/stores/world'
import { getCardTypeLabel as getTypeLabel } from '@/utils/worldPresentation'
import { parseWorldEditorText, serializeWorldEditorText, worldReferenceLabel } from '@/utils/worldEditorText'
import type { CardSummary } from '@/api/types/world'

const props = defineProps<{ modelValue: string; projectId: string; excludeCardId?: string; placeholder?: string; rows?: number; disabled?: boolean }>()
const emit = defineEmits<{ (e: 'update:modelValue', value: string): void }>()
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
  addAttributes: () => ({ id: { default: '' }, label: { default: '' } }),
  parseHTML: () => [{ tag: 'span[data-world-reference]', getAttrs: element => referenceAttrs(element.getAttribute('data-world-reference') || '') }],
  renderHTML: ({ node }) => ['span', { 'data-world-reference': node.attrs.id, class: 'world-reference', contenteditable: 'false' }, node.attrs.label],
  renderText: ({ node }) => node.attrs.label,
})
const editor = useEditor({
  extensions: [StarterKit.configure({ bold: false, italic: false, underline: false, strike: false, code: false,
    codeBlock: false, blockquote: false, heading: false, horizontalRule: false, link: false,
    bulletList: false, orderedList: false, listItem: false, listKeymap: false, trailingNode: false }), reference],
  content: parseWorldEditorText(props.modelValue, cards.value),
  editable: !props.disabled,
  editorProps: {
    attributes: { role: 'textbox', 'aria-label': '设定正文', 'aria-multiline': 'true', 'data-placeholder': props.placeholder || '写下设定，输入 @ 引用其他条目…' },
    handleKeyDown: (_view, event) => {
      if (event.isComposing || _view.composing || !dropdownOpen.value) return false
      if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
        activeIndex.value = Math.max(0, Math.min(matches.value.length - 1, activeIndex.value + (event.key === 'ArrowDown' ? 1 : -1)))
      } else if (event.key === 'Enter') {
        const card = matches.value[activeIndex.value]
        if (card) selectCard(card)
      } else if (event.key === 'Escape') closeDropdown()
      else return false
      return true
    },
    handlePaste: (_view, event) => {
      const data = event.clipboardData
      if (!data || data.getData('text/html').includes('data-world-reference')) return false
      const text = data.getData('text/plain')
      if (!text) return false
      editor.value?.commands.insertContent(parseWorldEditorText(text, cards.value).content || [])
      return true
    },
  },
  onUpdate: ({ editor }) => { emit('update:modelValue', serializeWorldEditorText(editor.getJSON())); detectTrigger() },
  onSelectionUpdate: () => detectTrigger(),
  onBlur: () => closeDropdown(),
})
function closeDropdown() { dropdownOpen.value = false; triggerStart.value = -1 }
function detectTrigger() {
  const current = editor.value
  if (!current || props.disabled || current.view.composing || !current.state.selection.empty) return closeDropdown()
  const { $from, from } = current.state.selection
  const before = $from.parent.textBetween(0, $from.parentOffset, undefined, '\\ufffc')
  const match = before.match(/(?:@|\\[\\[)([^@\\[\\]\\n\\ufffc]*)$/)
  if (!match) return closeDropdown()
  triggerStart.value = from - match[0].length
  if (query.value !== match[1]) activeIndex.value = 0
  query.value = match[1] || ''
  dropdownOpen.value = true
}
function selectCard(card: CardSummary) {
  const current = editor.value
  if (!current || props.disabled || triggerStart.value < 0) return
  current.chain().focus().insertContentAt({ from: triggerStart.value, to: current.state.selection.to },
    { type: 'worldReference', attrs: referenceAttrs(card.id) }).run()
  closeDropdown()
}
watch(() => props.modelValue, value => {
  if (editor.value && serializeWorldEditorText(editor.value.getJSON()) !== value) {
    editor.value.commands.setContent(parseWorldEditorText(value, cards.value), { emitUpdate: false })
    closeDropdown()
  }
})
watch(() => props.disabled, disabled => { editor.value?.setEditable(!disabled); if (disabled) closeDropdown() })
watch(cards, () => {
  const current = editor.value
  if (!current) return
  const tr = current.state.tr
  current.state.doc.descendants((node, pos) => {
    if (node.type.name === 'worldReference') {
      const attrs = referenceAttrs(node.attrs.id)
      if (attrs.label !== node.attrs.label) tr.setNodeMarkup(pos, undefined, attrs)
    }
  })
  if (tr.docChanged) current.view.dispatch(tr.setMeta('addToHistory', false))
}, { deep: true })
</script>

`;
fs.writeFileSync(p+'.tmp',source+css.replace('</style>',`.doc-desc-input :deep(.tiptap p) { margin: 0; min-height: 1.7em; }
.doc-desc-input :deep(.tiptap p:only-child:empty)::before { content: attr(data-placeholder); }
.doc-desc-input :deep(.world-reference) { display: inline; padding: 2px 6px; border-radius: 5px; background: var(--accent-soft); color: var(--accent-color); box-decoration-break: clone; }
.doc-desc-input :deep(.ProseMirror-selectednode) { outline: 2px solid var(--accent-color); }
.doc-desc-input :deep([contenteditable=false]) { cursor: default; }
.ref-help { margin: 8px 0 0; font-size: 12px; color: var(--text-secondary); line-height: 1.6; }
</style>`));fs.renameSync(p+'.tmp',p);
