// src/utils/editorConfig.ts
import StarterKit from '@tiptap/starter-kit'
import { TextStyle } from '@tiptap/extension-text-style'
import { Color } from '@tiptap/extension-color'
import { Underline } from '@tiptap/extension-underline'
import { Highlight } from '@tiptap/extension-highlight'
import { Placeholder } from '@tiptap/extension-placeholder'
import BubbleMenuExtension from '@tiptap/extension-bubble-menu'
import Link from '@tiptap/extension-link'
import { Node, Extension, mergeAttributes } from '@tiptap/core'
import { Plugin, PluginKey } from '@tiptap/pm/state'
import { nanoid } from 'nanoid'
import Image from '@tiptap/extension-image'
import TaskList from '@tiptap/extension-task-list'
import TaskItem from '@tiptap/extension-task-item'
import Mention from '@tiptap/extension-mention'
import { VueNodeViewRenderer } from '@tiptap/vue-3'
import PanelGraphBlock from '@/components/SpiritTextComponents/PanelGraphBlock.vue'
import katex from 'katex'
import MathBlockView from '@/components/SpiritTextComponents/MathBlockView.vue'
import MathInlineView from '@/components/SpiritTextComponents/MathInlineView.vue'
import PdfEmbedView from '@/components/SpiritTextComponents/PdfEmbedView.vue'






// ========================================================================
// 🌟 BlockIdExtension：给每个块分配稳定的 Nanoid，一次生成，永久不变
// ========================================================================
export const BlockIdExtension = Extension.create({
  name: 'blockId',

  addGlobalAttributes() {
    return [
      {
        types: [
          'paragraph', 'heading', 'blockquote', 'codeBlock',
          'bulletList', 'orderedList', 'taskList', 'taskItem',
          'image', 'spirit-link', 'panelGraph', 'details',
          'mathBlock', 'mathInline',
          'pdfEmbed',
        ],
        attributes: {
          id: {
            default: null,
            parseHTML: el => el.getAttribute('data-block-id'),
            renderHTML: attrs => attrs.id ? { 'data-block-id': attrs.id } : {},
          },
        },
      },
    ]
  },

  addProseMirrorPlugins() {
    return [
      new Plugin({
        key: new PluginKey('blockIdAssigner'),
        appendTransaction: (transactions, oldState, newState) => {
          if (!transactions.some(tr => tr.docChanged)) return null

          const seenIds = new Set<string>()
          const needsFix: { pos: number, attrs: any }[] = []

          newState.doc.descendants((node, pos) => {
            if (node.attrs.id === undefined) return

            const id = node.attrs.id

            if (!id) {
              needsFix.push({ pos, attrs: node.attrs })
              return
            }

            if (seenIds.has(id)) {
              needsFix.push({ pos, attrs: node.attrs })
              return
            }

            seenIds.add(id)
          })

          if (needsFix.length === 0) return null

          let tr = newState.tr
          needsFix.sort((a, b) => b.pos - a.pos)
          needsFix.forEach(({ pos, attrs }) => {
            tr = tr.setNodeMarkup(pos, undefined, { ...attrs, id: nanoid(21) })
          })

          return tr
        },
      }),
    ]
  },
})



const PanelGraphNode = Node.create({
  name: 'panelGraph',
  group: 'block',
  atom: true,
  selectable: true,
  draggable: true,

  addAttributes() {
    return {
      attributesList: {
        default: [
          { name: '属性A', value: 50, min: 0, max: 100 },
          { name: '属性B', value: 50, min: 0, max: 100 },
          { name: '属性C', value: 50, min: 0, max: 100 },
        ]
      }
    }
  },

  parseHTML() {
    return [{ tag: 'div[data-type="panel-graph"]' }]
  },

  renderHTML({ HTMLAttributes, node }) {
    return ['div', mergeAttributes(HTMLAttributes, { 'data-type': 'panel-graph' }), JSON.stringify(node.attrs.attributesList)]
  },

  addNodeView() {
    return VueNodeViewRenderer(PanelGraphBlock)
  }
})



// ========================================================================
// 🌟 MathBlock：块级数学公式
// ========================================================================
const MathBlock = Node.create({
  name: 'mathBlock',
  group: 'block',
  atom: true,
  selectable: true,
  draggable: true,

  addAttributes() {
    return {
      latex: { default: '' },
      assetId: { default: null },
    }
  },

  parseHTML() {
    return [{ tag: 'div[data-type="math-block"]' }]
  },

  renderHTML({ HTMLAttributes, node }) {
    let html = ''
    try {
      html = katex.renderToString(node.attrs.latex || '', {
        displayMode: true,
        throwOnError: false,
      })
    } catch (e) {
      html = '<span style="color:#ff3b30">公式错误</span>'
    }
    return ['div', mergeAttributes(HTMLAttributes, {
      'data-type': 'math-block',
      'data-latex': node.attrs.latex || '',
      class: 'spirit-math-block',
    }), ['div', { innerHTML: html }]]
  },

  addNodeView() {
    return VueNodeViewRenderer(MathBlockView)
  },
})

// ========================================================================
// 🌟 MathInline：行内数学公式
// ========================================================================
const MathInline = Node.create({
  name: 'mathInline',
  group: 'inline',
  inline: true,
  atom: true,
  selectable: true,

  addAttributes() {
    return {
      latex: { default: '' },
      assetId: { default: null },
    }
  },

  parseHTML() {
    return [{ tag: 'span[data-type="math-inline"]' }]
  },

  renderHTML({ HTMLAttributes, node }) {
    let html = ''
    try {
      html = katex.renderToString(node.attrs.latex || '', {
        displayMode: false,
        throwOnError: false,
      })
    } catch (e) {
      html = '<span style="color:#ff3b30">?</span>'
    }
    return ['span', mergeAttributes(HTMLAttributes, {
      'data-type': 'math-inline',
      'data-latex': node.attrs.latex || '',
      class: 'spirit-math-inline',
    }), ['span', { innerHTML: html }]]
  },

  addNodeView() {
    return VueNodeViewRenderer(MathInlineView)
  },
})

// ========================================================================
// 🌟 MathInputExtension：统一处理 $...$ 和 $$...$$ 输入转换
// ========================================================================
const MathInputExtension = Extension.create({
  name: 'mathInput',
  addProseMirrorPlugins() {
    return [
      new Plugin({
        key: new PluginKey('mathInput'),
        props: {
          handleTextInput(view, from, _to, text) {
            if (text !== '$') return false

            const doc = view.state.doc
            const mathBlockType = view.state.schema.nodes.mathBlock
            const mathInlineType = view.state.schema.nodes.mathInline
            if (!mathBlockType || !mathInlineType) return false

            const $pos = doc.resolve(from)
            const parentStart = $pos.start()
            const scanStart = Math.max(parentStart, from - 300)
            const before = doc.textBetween(scanStart, from, undefined, '\ufffc')

            // ---------- 先看是不是 $$...$$ 闭合 ----------
            const doubleOpenIdx = before.lastIndexOf('$$')
            if (doubleOpenIdx !== -1) {
              const inner = before.slice(doubleOpenIdx + 2)
              if (inner && !inner.includes('$') && !inner.includes('\n')) {
                const absStart = scanStart + doubleOpenIdx
                const verify = doc.textBetween(absStart, from, undefined, '\ufffc')
                if (verify === before.slice(doubleOpenIdx)) {
                  const node = mathBlockType.create({ latex: inner.trim() })
                  view.dispatch(view.state.tr.replaceWith(absStart, from, node))
                  return true
                }
              }
            }

            // ---------- 再看是不是 $...$ 闭合 ----------
            const lastDollar = before.lastIndexOf('$')
            if (lastDollar === -1) return false

            if (lastDollar >= 1 && before[lastDollar - 1] === '$') return false

            const inner = before.slice(lastDollar + 1)
            if (!inner || inner.includes('$') || inner.includes('\n')) return false

            const absStart = scanStart + lastDollar
            const verify = doc.textBetween(absStart, from, undefined, '\ufffc')
            if (verify !== before.slice(lastDollar)) return false

            const node = mathInlineType.create({ latex: inner.trim() })
            view.dispatch(view.state.tr.replaceWith(absStart, from, node))
            return true
          }
        }
      })
    ]
  },
})



// ========================================================================
// 🌟 PdfEmbed：PDF 嵌入节点
// ========================================================================
const PdfEmbed = Node.create({
  name: 'pdfEmbed',
  group: 'block',
  atom: true,
  selectable: true,
  draggable: true,

  addAttributes() {
    return {
      assetId: { default: null },
      url: { default: null },
      fileName: { default: '' },
      height: { default: '600px' },
    }
  },

  parseHTML() {
    return [{ tag: 'div[data-type="pdf-embed"]' }]
  },

  renderHTML({ HTMLAttributes, node }) {
    return ['div', mergeAttributes(HTMLAttributes, {
      'data-type': 'pdf-embed',
      'data-asset-id': node.attrs.assetId || '',
      'data-url': node.attrs.url || '',
    }), `PDF: ${node.attrs.fileName || ''}`]
  },

  addNodeView() {
    return VueNodeViewRenderer(PdfEmbedView)
  },
})





const SpiritNode = Node.create({
  name: 'spirit-link',
  group: 'inline',
  inline: true,
  selectable: true,
  atom: true,
  addAttributes() {
    return {
      id: { default: null },
      blockId: { default: null },
      title: {
        default: '',
        renderHTML: () => ({}),
      },
    }
  },
  parseHTML() {
    return [{ tag: 'span[data-spirit-id]' }]
  },
  renderHTML({ node }) {
    const display = node.attrs.blockId
      ? `[[${node.attrs.title}#${String(node.attrs.blockId).slice(0, 6)}]]`
      : `[[${node.attrs.title}]]`

    return [
      'span',
      {
        'data-spirit-id': node.attrs.id,
        'data-block-id': node.attrs.blockId || '',
        class: 'spirit-link-node',
      },
      display,
    ]
  },
})

const DetailsNode = Node.create({
  name: 'details',
  group: 'block',
  content: 'summary (paragraph|taskList|orderedList|bulletList|codeBlock|image)+',
  addAttributes() { return { open: { default: true } } },
  parseHTML() { return [{ tag: 'details' }] },
  renderHTML({ HTMLAttributes }) { return ['details', mergeAttributes(HTMLAttributes), 0] },
})

const SummaryNode = Node.create({
  name: 'summary',
  content: 'text*',
  group: 'block',
  parseHTML() { return [{ tag: 'summary' }] },
  renderHTML() { return ['summary', {}, 0] },
})

export const spiritExtensions = [
  BlockIdExtension,
  Mention.configure({
    HTMLAttributes: { class: 'spirit-mention-node' },
    renderLabel({ node }) {
      return `${node.attrs.label ?? node.attrs.id}`
    },
  }),
  DetailsNode,
  SummaryNode,
  TaskList,
  TaskItem.configure({
    nested: true,
    HTMLAttributes: { class: 'spirit-task-item' },
  }),
  PanelGraphNode,
  MathInputExtension,
  MathBlock,
  MathInline,
  PdfEmbed,
  Image.extend({
    addAttributes() {
      return {
        ...this.parent?.(),
        align: {
          default: 'center',
          renderHTML: attributes => ({ 'data-align': attributes.align }),
        },
        width: {
          default: '100%',
          renderHTML: attributes => ({ style: `width: ${attributes.width}; height: auto;` }),
        },
        caption: {
          default: '',
          renderHTML: attributes => ({ 'data-caption': attributes.caption }),
        },
        assetId: {
          default: null,
          renderHTML: attributes => attributes.assetId ? { 'data-asset-id': attributes.assetId } : {},
        },
      }
    },

    renderHTML({ HTMLAttributes }) {
      return [
        'figure', 
        { style: 'margin: 0; text-align: center;' },
        ['img', mergeAttributes(this.options.HTMLAttributes, HTMLAttributes, {
          style: `width: ${HTMLAttributes.width || '100%'}; height: auto; display: block;`
        })],
        ['figcaption', { 
          style: 'text-align: center; font-size: 0.9em; color: #86868b; padding: 12px 0 0;' 
        }, HTMLAttributes.caption || '']
      ]
    },

    addNodeView() {
      return ({ node, editor }) => {
        const container = document.createElement('figure')
        container.style.margin = '0'

        const img = document.createElement('img')
        img.src = node.attrs.src
        if (node.attrs.alt) img.alt = node.attrs.alt
        if (node.attrs.title) img.title = node.attrs.title
        img.setAttribute('data-align', node.attrs.align)
        img.style.width = node.attrs.width || '100%'
        img.style.height = 'auto'
        img.style.display = 'block'
        container.appendChild(img)

        const caption = document.createElement('figcaption')
        caption.setAttribute('contenteditable', 'true')
        caption.setAttribute('data-placeholder', '添加题注…')
        caption.style.cssText = `
          text-align: center; font-size: 0.9em; color: #86868b;
          padding: 12px 0 0; outline: none; min-height: 1.2em;
        `
        caption.innerHTML = node.attrs.caption || ''
        caption.addEventListener('input', () => {
          editor.commands.updateAttributes('image', { caption: caption.innerText })
        })
        caption.addEventListener('click', (e) => e.stopPropagation())
        caption.addEventListener('mousedown', (e) => e.stopPropagation())
        container.appendChild(caption)

        return {
          dom: container,
          stopEvent: (event) => {
            const target = event.target
            if (target && caption.contains(target as globalThis.Node)) {
              return ['input', 'click', 'mousedown', 'keydown', 'keyup', 'paste', 'cut', 'copy'].includes(event.type)
            }
            return false
          }
        }
      }
    },
  }).configure({
    inline: false,
    HTMLAttributes: { class: 'spirit-image-node' },
  }),







  SpiritNode,
  StarterKit.configure({
    heading: { levels: [1, 2, 3] },
    codeBlock: { HTMLAttributes: { class: 'spirit-code-block' } },
  }),
  Link.extend({
    addAttributes() {
      return {
        ...this.parent?.(),
        'data-target-id': {
          default: null,
          parseHTML: element => element.getAttribute('data-target-id'),
          renderHTML: attributes => {
            if (!attributes['data-target-id']) return {}
            return { 'data-target-id': attributes['data-target-id'] }
          }
        },
        target: { default: null, renderHTML: () => ({}) }
      }
    }
  }).configure({
    openOnClick: false,
    autolink: false,
    HTMLAttributes: { class: 'spirit-link-node', rel: null },
  }),
  TextStyle.configure(),
  Underline.configure(),
  BubbleMenuExtension,
  Color.configure({ types: [TextStyle.name, 'listing'] }),
  Highlight.configure({ multicolor: true }),
  Placeholder.configure({
    placeholder: '输入 / 唤起灵脉指令...',
    emptyEditorClass: 'is-editor-empty',
  }),
]

export const spiritColors = [
  { name: '太初红', color: '#e63946' },
  { name: '灵脉蓝', color: '#0066cc' },
  { name: '混沌灰', color: '#86868b' },
  { name: '深邃黑', color: '#1a1a1a' },
]

export const slashCommands = [
  {
    label: '一级标题', icon: 'H1',
    command: (editor: any) => {
      const { from, to } = editor.state.selection
      editor.chain().focus().deleteRange({ from: from - 1, to }).setNode('heading', { level: 1 }).run()
    }
  },
  {
    label: '二级标题', icon: 'H2',
    command: (editor: any) => {
      const { from, to } = editor.state.selection
      editor.chain().focus().deleteRange({ from: from - 1, to }).setNode('heading', { level: 2 }).run()
    }
  },
  {
    label: '引用块', icon: '“”',
    command: (editor: any) => {
      const { from, to } = editor.state.selection
      editor.chain().focus().deleteRange({ from: from - 1, to }).toggleBlockquote().run()
    }
  },
  {
    label: '待办事项', 
    icon: '☑️',
    command: (editor: any) => {
      const { from, to } = editor.state.selection
      editor.chain().focus().deleteRange({ from: from - 1, to }).toggleTaskList().run()
    }
  },
  {
    label: '有序列表', icon: '1.',
    command: (editor: any) => {
      const { from, to } = editor.state.selection
      editor.chain().focus().deleteRange({ from: from - 1, to }).toggleOrderedList().run()
    }
  },
  {
    label: '代码块', icon: '</>',
    command: (editor: any) => {
      const { from, to } = editor.state.selection
      editor.chain().focus().deleteRange({ from: from - 1, to }).toggleCodeBlock().run()
    }
  },
  {
    label: '自定义属性面板图',
    icon: '📊',
    command: (editor: any) => {
      const { from, to } = editor.state.selection
      editor.chain()
        .focus()
        .deleteRange({ from: from - 1, to })
        .insertContent({
          type: 'panelGraph',
          attrs: {
            attributesList: [
              { name: '力量', value: 10, min: 0, max: 100 },
              { name: '敏捷', value: 10, min: 0, max: 100 },
              { name: '智力', value: 10, min: 0, max: 100 }
            ]
          }
        })
        .run()
    }
  },
  
  {
    label: '插入原图',
    icon: '🖼️',
    command: (editor: any) => {
      const { from, to } = editor.state.selection
      editor.chain().focus().deleteRange({ from: from - 1, to }).run()

      const input = document.createElement('input')
      input.type = 'file'
      input.accept = 'image/*'
      
      input.onchange = async () => {
        if (input.files && input.files[0]) {
          const file = input.files[0]
          const currentPos = editor.state.selection.$from.pos
          const event = new CustomEvent('spirit-insert-image', {
            detail: { file, pos: currentPos }
          })
          editor.view.dom.dispatchEvent(event)
        }
      }
      
      input.click()
    }
  },
  {
    label: '数学公式块',
    icon: '∑',
    command: (editor: any) => {
      const { from, to } = editor.state.selection
      editor.chain()
        .focus()
        .deleteRange({ from: from - 1, to })
        .insertContent({ type: 'mathBlock', attrs: { latex: '' } })
        .run()
    }
  },
  {
    label: '行内公式',
    icon: 'ƒ',
    command: (editor: any) => {
      const { from, to } = editor.state.selection
      editor.chain()
        .focus()
        .deleteRange({ from: from - 1, to })
        .insertContent({ type: 'mathInline', attrs: { latex: '' } })
        .run()
    }
  },
    {
    label: '插入 PDF',
    icon: '📄',
    command: (editor: any) => {
      const { from, to } = editor.state.selection
      editor.chain().focus().deleteRange({ from: from - 1, to }).run()

      const input = document.createElement('input')
      input.type = 'file'
      input.accept = 'application/pdf'

      input.onchange = async () => {
        if (input.files && input.files[0]) {
          const file = input.files[0]
          const currentPos = editor.state.selection.$from.pos
          const event = new CustomEvent('spirit-insert-pdf', {
            detail: { file, pos: currentPos }
          })
          editor.view.dom.dispatchEvent(event)
        }
      }

      input.click()
    }
  }
]