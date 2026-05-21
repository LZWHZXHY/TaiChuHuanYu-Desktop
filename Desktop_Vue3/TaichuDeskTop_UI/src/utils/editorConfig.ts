// src/utils/editorConfig.ts
import StarterKit from '@tiptap/starter-kit'
import { TextStyle } from '@tiptap/extension-text-style'
import { Color } from '@tiptap/extension-color'
import { Underline } from '@tiptap/extension-underline'
import { Highlight } from '@tiptap/extension-highlight'
import { Placeholder } from '@tiptap/extension-placeholder'
import BubbleMenuExtension from '@tiptap/extension-bubble-menu'
import Link from '@tiptap/extension-link'
import { Node, mergeAttributes } from '@tiptap/core'
import Image from '@tiptap/extension-image'
import TaskList from '@tiptap/extension-task-list'
import TaskItem from '@tiptap/extension-task-item'
import Mention from '@tiptap/extension-mention'

const SpiritNode = Node.create({
  name: 'spiritLink',
  group: 'inline',
  inline: true,
  selectable: true,
  atom: true,
  addAttributes() {
    return { id: { default: null }, title: { default: '' } }
  },
  parseHTML() {
    return [{ tag: 'span[data-spirit-id]' }]
  },
  renderHTML({ HTMLAttributes, node }) {
    return [
      'span',
      mergeAttributes(HTMLAttributes, {
        'data-spirit-id': node.attrs.id,
        class: 'spirit-link-node'
      }),
      `[[${node.attrs.title}]]`
    ]
  }
})

const DetailsNode = Node.create({
  name: 'details',
  group: 'block',
  // 🌟 保持使用 'image' 节点名（因为我们没有改名）
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
  // 🌟🌟🌟 核心修改：直接扩展 Image，保留节点名 'image'，添加 caption 和 NodeView
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
      // 不再添加 caption
    }
  },
    // 在 editorConfig.ts 的 Image.extend(...) 中，替换原来的 addNodeView 为：

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
    // 阻止事件冒泡，防止编辑器误删节点
    caption.addEventListener('click', (e) => e.stopPropagation())
    caption.addEventListener('mousedown', (e) => e.stopPropagation())
    container.appendChild(caption)

    return {
      dom: container,
      // 关键：允许题注内部事件正常运作
      stopEvent: (event) => {
        const target = event.target
        // 使用 globalThis.Node 避免与 ProseMirror Node 冲突
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
  Underline.configure(),       // 如果警告重复，可尝试删除此行（StarterKit 可能已包含）
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
]