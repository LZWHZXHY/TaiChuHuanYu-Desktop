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
  name: 'spirit-link',
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
        // 🌟 必须加上 caption 属性声明，不然静态渲染或更新时，数据无法落进 attributes 
        caption: {
          default: '',
          renderHTML: attributes => ({ 'data-caption': attributes.caption }),
        }
      }
    },

    // 🌟 核心修复点：为静态 preview（generateHTML）提供专属的静态 HTML 标签蓝图
    // 当在预览环境下运行时，Tiptap 会直接读取这个结构，而不会去走下面崩溃的 addNodeView
    renderHTML({ HTMLAttributes }) {
      return [
        'figure', 
        { style: 'margin: 0; text-align: center;' },
        ['img', mergeAttributes(this.options.HTMLAttributes, HTMLAttributes, {
          style: `width: ${HTMLAttributes.width || '100%'}; height: auto; display: block;`
        })],
        ['figcaption', { 
          style: 'text-align: center; font-size: 0.9em; color: #86868b; padding: 12px 0 0;' 
        }, HTMLAttributes.caption || ''] // 把题注静态渲染出来
      ]
    },

    // 下面你原有的 addNodeView 保持原样不动
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
    label: '待办事项', 
    icon: '☑️',
    command: (editor: any) => {
      const { from, to } = editor.state.selection
      // 删除触发指令的 '/' 符号，并切换为待办事项列表
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
    label: '插入原图',
    icon: '🖼️',
    command: (editor: any) => {
      // 1. 获取当前输入斜杠 '/' 的位置并将其删掉，保持行文干净
      const { from, to } = editor.state.selection
      editor.chain().focus().deleteRange({ from: from - 1, to }).run()

      // 2. 创建隐藏的 file input 触发原生文件选择器
      const input = document.createElement('input')
      input.type = 'file'
      input.accept = 'image/*'
      
      input.onchange = async () => {
        if (input.files && input.files[0]) {
          const file = input.files[0]
          
          // 3. 获取删除 '/' 后光标所在的新位置
          const currentPos = editor.state.selection.$from.pos
          
          // 4. 向编辑器的 DOM 节点派发一个自定义事件，将文件和位置传给宿主 Vue 组件
          const event = new CustomEvent('spirit-insert-image', {
            detail: { file, pos: currentPos }
          })
          editor.view.dom.dispatchEvent(event)
        }
      }
      
      input.click()
    }
  }
]