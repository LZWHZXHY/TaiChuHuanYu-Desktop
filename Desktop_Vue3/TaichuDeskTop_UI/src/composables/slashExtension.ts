// 文件：composables/slashExtension.ts
import { Extension } from '@tiptap/core'
import Suggestion from '@tiptap/suggestion'
import { VueRenderer } from '@tiptap/vue-3'
import tippy from 'tippy.js'
import SlashMenuList from '@/components/SpiritMenuList.vue' // 路径根据你的实际情况改一下
import { slashCommands } from '../utils/editorConfig' // 你的配置项

export const SlashMenuExtension = Extension.create({
  name: 'slashMenu',

  addProseMirrorPlugins() {
    return [
      Suggestion({
        editor: this.editor,
        char: '/', // 告诉雷达：只要敲 '/' 就激活
        
        // 当用户回车选中某一项时，执行这块代码
        command: ({ editor, range, props }) => {
          // 1. 先把刚才输入的 '/' 从屏幕上删掉，保持优雅
          editor.chain().focus().deleteRange(range).run()
          
          // 2. 这里的 props 就是 slashCommands 里的一项，执行它的命令！
          props.command(editor)
        },

        // 数据源：提供菜单列表
        items: ({ query }) => {
          return slashCommands.filter(item => 
            item.label.toLowerCase().includes(query.toLowerCase())
          )
        },

        // 渲染引擎：连接 Vue 和 Tippy
        render: () => {
          let component: any
          let popup: any

          return {
            onStart: (props) => {
              // 把我们写的 Vue 文件转换成网页 DOM
              component = new VueRenderer(SlashMenuList, {
                props,
                editor: props.editor,
              })

              if (!props.clientRect) return

              // 召唤 Tippy，让菜单死死黏在光标坐标 (clientRect) 上
              popup = tippy('body', {
                getReferenceClientRect: props.clientRect as any,
                appendTo: () => document.body,
                content: component.element,
                showOnCreate: true,
                interactive: true,
                trigger: 'manual',
                placement: 'bottom-start',
              })
            },

            onUpdate(props) {
              // 用户继续敲字（比如敲 /img），更新坐标和菜单列表
              component.updateProps(props)
              if (!props.clientRect) return
              popup[0].setProps({
                getReferenceClientRect: props.clientRect,
              })
            },

            onKeyDown(props) {
              if (props.event.key === 'Escape') {
                popup[0].hide()
                return true
              }
              // 把键盘事件传递给我们的 Vue 组件处理！
              return component.ref?.onKeyDown(props)
            },

            onExit() {
              // 菜单关闭时，销毁一切，防止内存泄漏
              popup[0].destroy()
              component.destroy()
            },
          }
        }
      })
    ]
  }
})