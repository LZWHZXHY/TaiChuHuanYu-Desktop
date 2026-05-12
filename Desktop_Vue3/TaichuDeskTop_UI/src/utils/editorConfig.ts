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
import TaskList from '@tiptap/extension-task-list' // 🌟 新增
import TaskItem from '@tiptap/extension-task-item' // 🌟 新增
import Mention from '@tiptap/extension-mention'

const SpiritNode = Node.create({
  name: 'spiritLink',
  group: 'inline',
  inline: true,
  selectable: true,
  atom: true, // 设置为原子节点，内部不可编辑

  addAttributes() {
    return {
      id: { default: null },
      title: { default: '' }
    }
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
  content: 'summary (paragraph|taskList|orderedList|bulletList|codeBlock|image)+', // 🌟 第一个必须是 summary，后面是内容
  addAttributes() { return { open: { default: true } } },
  parseHTML() { return [{ tag: 'details' }] },
  renderHTML({ HTMLAttributes }) { return ['details', mergeAttributes(HTMLAttributes), 0] },
})
const SummaryNode = Node.create({
  name: 'summary',
  content: 'text*', // 🌟 标题只允许纯文本
  group: 'block',
  parseHTML() { return [{ tag: 'summary' }] },
  renderHTML() { return ['summary', {}, 0] },
})

export const spiritExtensions = [
  Mention.configure({
    HTMLAttributes: {
      class: 'spirit-mention-node',
    },
    // 🌟 核心：告诉 Tiptap 如何渲染数据中的 label
    renderLabel({ node }) {
      return `${node.attrs.label ?? node.attrs.id}`
    },
  }),
  DetailsNode,
  SummaryNode,
  TaskList, 
  TaskItem.configure({
    nested: true, // 🌟 必须开启，因为你的“网站开发计划”数据中存在嵌套任务列表
    HTMLAttributes: {
      class: 'spirit-task-item', // 可以根据需要添加自定义类名
    },
  }),
  Image.extend({
    // 1. 添加自定义属性
    addAttributes() {
      return {
        ...this.parent?.(),
        align: {
          default: 'center', // 默认居中
          renderHTML: attributes => ({
            'data-align': attributes.align,
          })
        },
        width: {
          default: '100%', // 默认撑满宽度
          renderHTML: attributes => ({
            style: `width: ${attributes.width}; height: auto;`
          })
        }
      }
    }
  }).configure({
    inline: false,
    HTMLAttributes: {
      class: 'spirit-image-node',
    },
  }),
  SpiritNode,
  StarterKit.configure({
    heading: { levels: [1, 2, 3] },
    codeBlock: {
      HTMLAttributes: { class: 'spirit-code-block' },
    },
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
        // 🌟 强力覆盖：强制去掉 target 属性，防止新开页面
        target: {
          default: null,
          renderHTML: () => ({}) 
        }
      }
    }
  }).configure({
    openOnClick: false, // 必须为 false
    autolink: false,    // 禁用自动识别链接
    HTMLAttributes: {
      class: 'spirit-link-node',
      rel: null,        // 去掉 rel 属性
    },
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

/**
 * 统一定义颜色盘
 */
export const spiritColors = [
  { name: '太初红', color: '#e63946' },
  { name: '灵脉蓝', color: '#0066cc' },
  { name: '混沌灰', color: '#86868b' },
  { name: '深邃黑', color: '#1a1a1a' },
]

/**
 * 斜杠菜单命令定义
 * 每个命令在执行前都会先删掉触发它的那个 "/" 字符
 */
export const slashCommands = [
  { 
    label: '一级标题', 
    icon: 'H1', 
    command: (editor: any) => {
      const { from, to } = editor.state.selection;
      editor.chain().focus().deleteRange({ from: from - 1, to }).setNode('heading', { level: 1 }).run();
    }
  },
  { 
    label: '二级标题', 
    icon: 'H2', 
    command: (editor: any) => {
      const { from, to } = editor.state.selection;
      editor.chain().focus().deleteRange({ from: from - 1, to }).setNode('heading', { level: 2 }).run();
    }
  },
  { 
    label: '引用块', 
    icon: '“”', 
    command: (editor: any) => {
      const { from, to } = editor.state.selection;
      editor.chain().focus().deleteRange({ from: from - 1, to }).toggleBlockquote().run();
    }
  },
  { 
    label: '有序列表', 
    icon: '1.', 
    command: (editor: any) => {
      const { from, to } = editor.state.selection;
      editor.chain().focus().deleteRange({ from: from - 1, to }).toggleOrderedList().run();
    }
  },
  { 
    label: '代码块', 
    icon: '</>', 
    command: (editor: any) => {
      const { from, to } = editor.state.selection;
      editor.chain().focus().deleteRange({ from: from - 1, to }).toggleCodeBlock().run();
    }
  },
]