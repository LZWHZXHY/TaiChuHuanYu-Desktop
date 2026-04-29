<template>
  <div class="spirit-editor-wrapper">
    <bubble-menu 
      v-if="editor" 
      :editor="editor" 
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
          v-for="c in spiritColors" 
          :key="c.color" 
          :style="{ backgroundColor: c.color }"
          @click="editor.chain().focus().setColor(c.color).run()"
          class="color-dot"
        ></button>
      </div>
    </bubble-menu>

    <editor-content :editor="editor" class="spirit-typography-engine" />

    <transition name="menu-pop">
      <div v-if="showSlashMenu" class="spirit-floating-menu" :style="menuStyle">
        <div class="menu-header">灵脉指令</div>
        <div v-for="cmd in slashCommands" :key="cmd.label" class="menu-item" @click="runSlashCommand(cmd)">
          <div class="item-icon">{{ cmd.icon }}</div>
          <div class="item-text">{{ cmd.label }}</div>
        </div>
      </div>
    </transition>

    <transition name="menu-pop">
      <div v-if="showLinkSelector" class="spirit-floating-menu" :style="menuStyle">
        <div class="menu-header">关联灵脉碎片...</div>
        <div class="menu-scroll-area">
          <div 
            v-for="note in availableNotes" 
            :key="note.id" 
            class="menu-item" 
            @click="insertBiLink(note)"
          >
            <div class="item-icon">📄</div>
            <div class="item-text">
              <div class="main-title">{{ note.title || '无标题碎片' }}</div>
              <div class="sub-info">{{ note.isPublished ? '公开' : '私有' }}</div>
            </div>
          </div>
        </div>
        <div v-if="availableNotes.length === 0" class="menu-empty">未找到相关碎片</div>
      </div>
    </transition>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, watch } from 'vue'
import { EditorContent, useEditor } from '@tiptap/vue-3'
import { BubbleMenu } from '@tiptap/vue-3/menus'
import { spiritExtensions, spiritColors, slashCommands } from '../utils/editorConfig'
import { useSpiritData } from '../composables/useSpiritData'

// 1. 接入灵脉数据大脑
const { notes, currentNoteId, activeNote, updateNoteContent } = useSpiritData()
const { selectNote } = useSpiritData();

// SpiritText.vue 脚本
const handleLinkNavigation = (e: MouseEvent) => {
  const target = e.target as HTMLElement;
  // 查找带有我们自定义属性的节点
  const node = target.closest('[data-spirit-id]');

  if (node) {
    // 🛑 物理切断冒泡和默认行为
    e.preventDefault();
    e.stopPropagation();
    e.stopImmediatePropagation();

    const noteId = node.getAttribute('data-spirit-id');
    if (noteId) {
      //console.log("🔥 核心拦截：切换 ID 到", noteId);
      selectNote(noteId);
    }
  }
};


// 状态控制
const showSlashMenu = ref(false)
const showLinkSelector = ref(false)
const menuPos = ref({ top: 0, left: 0 })

const menuStyle = computed(() => ({
  top: `${menuPos.value.top}px`,
  left: `${menuPos.value.left}px`
}))

// 2. 筛选可引用的笔记（排除当前正在写的这一篇）
const availableNotes = computed(() => {
  return notes.value.filter(n => n.id !== currentNoteId.value)
})

const editor = useEditor({
  extensions: spiritExtensions,
  content: activeNote.value?.content || '',
  onUpdate: ({ editor }) => {
    const { state, view } = editor
    const { $from } = state.selection
    
    // 实时同步内容到“虚拟数据库”
    updateNoteContent(currentNoteId.value, editor.getJSON())

    // 检测触发字符
    const textBefore = state.doc.textBetween($from.before(), $from.pos)
    const coords = view.coordsAtPos($from.pos)
    menuPos.value = { top: coords.bottom + 10, left: coords.left }

    // 逻辑：斜杠指令
    showSlashMenu.value = textBefore.endsWith('/')
    
    // 逻辑：双向链接 [[
    showLinkSelector.value = textBefore.endsWith('[[')
  }
})

watch(
  () => currentNoteId.value,
  (newId) => {
    //console.log("检测到 ID 切换:", newId); // 👈 看看控制台有没有打印
    if (editor.value) {
      const targetNote = notes.value.find(n => n.id === newId);
      const newContent = targetNote?.content || '';
      
      //console.log("准备填充内容:", newContent); // 👈 看看内容是不是空的
      // 强制更新编辑器
      editor.value.commands.setContent(newContent, { 
        emitUpdate: false 
      });
    }
  },
  { immediate: true, deep: true }
);





// 执行斜杠命令
const runSlashCommand = (cmd: any) => {
  if (!editor.value) return
  cmd.command(editor.value)
  showSlashMenu.value = false
}

// SpiritText.vue 里的 insertBiLink
const insertBiLink = (note: any) => {
  if (!editor.value) return
  
  editor.value.chain()
    .focus()
    .deleteRange({ from: editor.value.state.selection.$from.pos - 2, to: editor.value.state.selection.$from.pos })
    // 🌟 插入 Node 节点
    .insertContent({
      type: 'spiritLink',
      attrs: {
        id: note.id,
        title: note.title || '未命名'
      }
    })
    .insertContent(' ') // 加空格
    .run()

  showLinkSelector.value = false
}

// 点击外部关闭
const closeMenus = (e: MouseEvent) => {
  if (!(e.target as HTMLElement).closest('.spirit-floating-menu')) {
    showSlashMenu.value = false
    showLinkSelector.value = false
  }
}

onMounted(() => {
  window.addEventListener('mousedown', closeMenus);
  // 🌟 使用捕获阶段拦截，确保在事件到达 Tiptap 核心前被我们接管
  document.addEventListener('click', handleLinkNavigation, { capture: true });
});

onUnmounted(() => {
  window.removeEventListener('mousedown', closeMenus);
  document.removeEventListener('click', handleLinkNavigation, { capture: true });
});
// 暴露给父组件的方法
defineExpose({ getJSON: () => editor.value?.getJSON() })




</script>

<style scoped>
.spirit-editor-wrapper {
  position: relative;
  width: 100%;
}

/* 浮动菜单通用样式 (Notion 风格) */
.spirit-floating-menu {
  position: fixed;
  width: 260px;
  max-height: 320px;
  background: white;
  border: 1px solid #f0f0f0;
  border-radius: 12px;
  box-shadow: 0 12px 30px rgba(0,0,0,0.12);
  padding: 6px;
  z-index: 9999;
  display: flex;
  flex-direction: column;
}

.menu-header {
  font-size: 10px;
  color: #a1a1a6;
  padding: 6px 10px;
  text-transform: uppercase;
  letter-spacing: 0.1em;
  font-weight: 700;
}

.menu-scroll-area { overflow-y: auto; flex: 1; }

.menu-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 10px;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
}
.menu-item:hover { background: #f5f5f7; }

.item-icon {
  width: 28px; height: 28px; background: #f0f0f2;
  border-radius: 6px; display: flex; align-items: center;
  justify-content: center; font-size: 12px; font-weight: bold;
}

.item-text { flex: 1; }
.main-title { font-size: 14px; font-weight: 500; color: #1d1d1f; }
.sub-info { font-size: 10px; color: #86868b; }

.menu-empty { padding: 20px; text-align: center; color: #d2d2d7; font-size: 13px; }

/* 气泡菜单样式 */
.spirit-bubble-menu {
  display: flex; align-items: center; background: #1a1a1a;
  border-radius: 8px; padding: 6px 10px; gap: 8px;
  box-shadow: 0 8px 24px rgba(0,0,0,0.15);
}
.toolbar-btns button { background: none; border: none; color: #fff; padding: 4px 8px; cursor: pointer; border-radius: 4px; }
.toolbar-btns button.is-active { color: #0066cc; background: #333; }
.color-dot { width: 16px; height: 16px; border-radius: 50%; border: 1px solid #444; cursor: pointer; }

/* 编辑器正文排版 */
:deep(.spirit-typography-engine .tiptap) {
  outline: none; min-height: 500px; font-size: 1.1rem; line-height: 1.8; color: #1d1d1f;
}

/* 🌟 双链节点样式：让它在编辑器里看起来很专业 */
:deep(.spirit-link-node) {
  color: #0066cc;
  background: rgba(0, 102, 204, 0.05);
  text-decoration: none;
  padding: 0 4px;
  border-radius: 4px;
  font-weight: 500;
  border-bottom: 1px dashed rgba(0, 102, 204, 0.4);
}

.menu-pop-enter-active { transition: all 0.2s ease-out; }
.menu-pop-enter-from { opacity: 0; transform: scale(0.95) translateY(-10px); }
</style>