<template>
  <div v-if="editor">
    <bubble-menu 
      :editor="editor" 
      :should-show="showTextMenu"
      :tippy-options="{ duration: 100, animation: 'shift-away' }"
      class="spirit-bubble-menu"
    >
      <div class="toolbar-btns">
        <button @click="editor.chain().focus().toggleBold().run()" :class="{ 'is-active': editor.isActive('bold') }">B</button>
        <button @click="editor.chain().focus().toggleItalic().run()" :class="{ 'is-active': editor.isActive('italic') }">I</button>
        <button @click="editor.chain().focus().toggleUnderline().run()" :class="{ 'is-active': editor.isActive('underline') }">U</button>
      </div>
      <div class="toolbar-divider"></div>
      <div class="toolbar-colors">
        <button 
          v-for="c in colors" 
          :key="c.color" 
          :style="{ backgroundColor: c.color }"
          @click="editor.chain().focus().setColor(c.color).run()"
          class="color-dot"
        ></button>
      </div>
    </bubble-menu>

    <bubble-menu 
      :editor="editor" 
      :should-show="showImageMenu"
      class="spirit-image-bubble"
      :tippy-options="{ duration: 100, offset: [0, 15] }"
    >
      <div class="image-toolbar">
        <div class="group">
          <button @click="setAlign('left')" :class="{ 'active': getAttr('align') === 'left' }">左对齐</button>
          <button @click="setAlign('center')" :class="{ 'active': getAttr('align') === 'center' }">居中</button>
          <button @click="setAlign('right')" :class="{ 'active': getAttr('align') === 'right' }">右对齐</button>
        </div>
        <div class="toolbar-divider"></div>
        <div class="group">
          <button @click="setWidth('25%')" :class="{ 'active': getAttr('width') === '25%' }">小</button>
          <button @click="setWidth('50%')" :class="{ 'active': getAttr('width') === '50%' }">中</button>
          <button @click="setWidth('100%')" :class="{ 'active': getAttr('width') === '100%' }">大</button>
        </div>
      </div>
    </bubble-menu>
  </div>
</template>

<script setup lang="ts">
// 🌟 按照你的要求使用具体的子路径引用
import { BubbleMenu } from '@tiptap/vue-3/menus'
import type { Editor } from '@tiptap/vue-3'

const props = defineProps<{
  editor: Editor | null;
  colors: Array<{ name: string, color: string }>;
}>()

/**
 * 🌟 核心修复：使用 any 绕过 BubbleMenu 内部复杂的接口类型检查
 * 确保函数能够接收 Tiptap 传入的所有 context 属性
 */
const showTextMenu = (props: any) => {
  const { editor } = props;
  // 仅在非图片被选中，且选区不为空时显示
  return editor && !editor.isActive('image') && !editor.state.selection.empty;
}

const showImageMenu = (props: any) => {
  const { editor } = props;
  // 仅在图片被选中时显示
  return editor && editor.isActive('image');
}

// 工具函数：安全获取图片属性
const getAttr = (name: string) => {
  return props.editor?.getAttributes('image')[name];
}

// 工具函数：更新对齐
const setAlign = (align: string) => {
  props.editor?.chain().focus().updateAttributes('image', { align }).run();
}

// 工具函数：更新宽度
const setWidth = (width: string) => {
  props.editor?.chain().focus().updateAttributes('image', { width }).run();
}
</script>

<style scoped>
/* 文字菜单基础容器 */
.spirit-bubble-menu {
  display: flex;
  align-items: center;
  background: #1a1a1a;
  border-radius: 8px;
  padding: 6px 10px;
  gap: 8px;
  box-shadow: 0 8px 24px rgba(0,0,0,0.15);
}

/* 图片菜单容器 */
.spirit-image-bubble {
  background: #1a1a1a;
  border-radius: 8px;
  padding: 4px;
  display: flex;
  box-shadow: 0 10px 30px rgba(0,0,0,0.2);
  z-index: 9999;
}

/* 通用按钮样式 */
.toolbar-btns button, .image-toolbar button {
  background: none;
  border: none;
  color: #fff;
  padding: 6px 10px;
  font-size: 11px;
  cursor: pointer;
  border-radius: 4px;
  transition: all 0.2s;
}

.toolbar-btns button.is-active, .image-toolbar button.active {
  color: #0066cc;
  background: rgba(0,102,204,0.15);
  font-weight: bold;
}

.toolbar-divider {
  width: 1px;
  height: 16px;
  background: #333;
  margin: 0 4px;
}

.color-dot {
  width: 16px;
  height: 16px;
  border-radius: 50%;
  border: 1px solid #444;
  cursor: pointer;
}

.image-toolbar {
  display: flex;
  align-items: center;
  gap: 4px;
}

.image-toolbar .group {
  display: flex;
  gap: 2px;
}
</style>