<template>
  <div class="spirit-editor-wrapper">
    <EditorBubbleMenu 
      v-if="editor" 
      :editor="editor" 
      :colors="spiritColors" 
    />

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
        <div v-for="note in availableNotes" :key="note.id" class="menu-item" @click="insertBiLink(note)">
  <div class="item-icon">📄</div>
  <div class="item-text">
    <div class="main-title">{{ note.title || '无标题碎片' }}</div>
    <div class="sub-info">{{ note.isPublic ? '公开' : '私有' }}</div> </div>
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
import EditorBubbleMenu from './SpiritTextComponents/EditorBubbleMenu.vue' 
import { spiritExtensions, spiritColors, slashCommands } from '../utils/editorConfig'
import { useSpiritData } from '../composables/useSpiritData'
import { useCos } from '../composables/useCos'

// 1. 接入数据大脑
const { notes, currentNoteId, updateNoteContent, selectNote } = useSpiritData()
const { uploadFile } = useCos()

const targetNote = notes.value.find(n => n.id === currentNoteId.value);
const initialContent = targetNote?.content || { type: 'doc', content: [] };

// --- 状态控制 ---
const isInitialized = ref(true) // 数据肯定是有的，直接就是 true
let lastSyncedJson = JSON.stringify(initialContent) // 初始防抖对比值




const showSlashMenu = ref(false)
const showLinkSelector = ref(false)
const menuPos = ref({ top: 0, left: 0 })

const menuStyle = computed(() => ({
  top: `${menuPos.value.top}px`,
  left: `${menuPos.value.left}px`
}))

const availableNotes = computed(() => {
  return notes.value.filter(n => n.id !== currentNoteId.value)
})

/**
 * 🌟 核心图片处理：COS 上传 + 节点插入
 */
const handleImageProcess = async (view: any, file: File, pos?: number) => {
  if (!file.type.startsWith('image/')) return;
  try {
    const result = await uploadFile(file, 'lingmai');
    const { schema } = view.state;
    // 插入时带上默认属性
    const node = schema.nodes.image.create({ 
      src: result.url,
      align: 'center',
      width: '100%'
    });
    
    const transaction = pos 
      ? view.state.tr.insert(pos, node)
      : view.state.tr.replaceSelectionWith(node);
      
    view.dispatch(transaction);
  } catch (err) {
    console.error('灵脉图片处理失败:', err);
  }
};

// --- 🌟 编辑器核心配置 ---
const editor = useEditor({
  extensions: spiritExtensions,
  content: initialContent, 
  editorProps: {
    // 拦截拖拽
    handleDrop: (view, event, slice, moved) => {
      if (!moved && event.dataTransfer?.files?.length) {
        const file = event.dataTransfer.files[0];
        const coordinates = view.posAtCoords({ left: event.clientX, top: event.clientY });
        handleImageProcess(view, file, coordinates?.pos);
        return true; 
      }
      return false;
    },
    // 拦截粘贴
    handlePaste: (view, event) => {
      const items = event.clipboardData?.items;
      if (items) {
        for (const item of items) {
          if (item.type.startsWith('image/')) {
            const file = item.getAsFile();
            if (file) {
              handleImageProcess(view, file);
              return true;
            }
          }
        }
      }
      return false;
    }
  },
  onUpdate: ({ editor }) => {
    if (!isInitialized.value) return;

    const currentJson = editor.getJSON();
    const currentJsonStr = JSON.stringify(currentJson);
    
    // 🌟 如果内容完全没变（比如仅仅是点击或选择文本），直接拦截，不发请求
    if (currentJsonStr === lastSyncedJson) return;

    // 浮动菜单逻辑计算
    const { state, view } = editor
    const { $from } = state.selection
    const textBefore = state.doc.textBetween($from.before(), $from.pos)
    const coords = view.coordsAtPos($from.pos)
    
    menuPos.value = { top: coords.bottom + 10, left: coords.left }
    showSlashMenu.value = textBefore.endsWith('/')
    showLinkSelector.value = textBefore.endsWith('[[')

    // 🌟 核心修改：不再内部设置 setTimeout
    // 直接调用 Composables 的 updateNoteContent，由它的 lodash 防抖来保障安全
    updateNoteContent(currentNoteId.value, currentJson);
    lastSyncedJson = currentJsonStr; 
  }
})


// --- 交互逻辑 ---
const handleLinkNavigation = (e: MouseEvent) => {
  const target = e.target as HTMLElement;
  const node = target.closest('[data-spirit-id]');
  if (node) {
    e.preventDefault();
    e.stopPropagation();
    const noteId = node.getAttribute('data-spirit-id');
    if (noteId) selectNote(noteId);
  }
};

const runSlashCommand = (cmd: any) => {
  if (!editor.value) return
  cmd.command(editor.value)
  showSlashMenu.value = false
}

const insertBiLink = (note: any) => {
  if (!editor.value) return
  editor.value.chain()
    .focus()
    .deleteRange({ from: editor.value.state.selection.$from.pos - 2, to: editor.value.state.selection.$from.pos })
    .insertContent({
      type: 'spiritLink',
      attrs: { id: note.id, title: note.title || '未命名' }
    })
    .insertContent(' ')
    .run()
  showLinkSelector.value = false
}

const closeMenus = (e: MouseEvent) => {
  if (!(e.target as HTMLElement).closest('.spirit-floating-menu')) {
    showSlashMenu.value = false
    showLinkSelector.value = false
  }
}

onMounted(() => {
  window.addEventListener('mousedown', closeMenus);
  document.addEventListener('click', handleLinkNavigation, { capture: true });
});

onUnmounted(() => {
  window.removeEventListener('mousedown', closeMenus);
  document.removeEventListener('click', handleLinkNavigation, { capture: true });
});

defineExpose({ 
  editor: editor,           
  isInitialized: isInitialized, 
  lastSyncedJson: lastSyncedJson,
  getJSON: () => editor.value?.getJSON() 
});
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

/* 编辑器正文排版 */
:deep(.spirit-typography-engine .tiptap) {
  outline: none; min-height: 500px; font-size: 1.1rem; line-height: 1.8; color: #1d1d1f;
}

/* 双链节点样式 */
:deep(.spirit-link-node) {
  color: #0066cc;
  background: rgba(0, 102, 204, 0.05);
  text-decoration: none;
  padding: 0 4px;
  border-radius: 4px;
  font-weight: 500;
  border-bottom: 1px dashed rgba(0, 102, 204, 0.4);
}

/* 🌟 图片排版与对齐支持 */
:deep(.spirit-image-node) {
  display: block;
  height: auto;
  border-radius: 12px;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  cursor: grab;
  border: 2px solid transparent;
}

:deep(.ProseMirror-selectednode.spirit-image-node) {
  border-color: #0066cc;
  box-shadow: 0 4px 20px rgba(0,102,204,0.1);
}

/* 核心对齐逻辑 */
:deep(.spirit-image-node[data-align="left"]) { margin-left: 0; margin-right: auto; }
:deep(.spirit-image-node[data-align="center"]) { margin-left: auto; margin-right: auto; }
:deep(.spirit-image-node[data-align="right"]) { margin-left: auto; margin-right: 0; }

:deep(.ProseMirror-dropcursor) {
  color: #0066cc;
  width: 2px;
}

.menu-pop-enter-active { transition: all 0.2s ease-out; }
.menu-pop-enter-from { opacity: 0; transform: scale(0.95) translateY(-10px); }

/* SpiritText.vue 的 style 部分 */
:deep(.tiptap ul[data-type="taskList"]) {
  list-style: none;
  padding: 0;
}

:deep(.tiptap li[data-type="taskItem"]) {
  display: flex;
  gap: 0.5rem;
  align-items: flex-start;
}

:deep(.tiptap li[data-type="taskItem"] > label) {
  flex: 0 0 auto;
  user-select: none;
  margin-top: 0.25rem;
}

:deep(.tiptap li[data-type="taskItem"] > div) {
  flex: 1 1 auto;
}
/* SpiritText.vue 的 style 区域 */

:deep(.tiptap details) {
  border: 1px solid #f2f2f7;
  border-radius: 12px;
  margin: 1.5rem 0;
  padding: 0;
  background: #ffffff;
  overflow: hidden;
}

:deep(.tiptap summary) {
  padding: 12px 20px;
  background: #fbfbfd;
  border-bottom: 1px solid #f2f2f7;
  cursor: pointer;
  font-weight: 700;
  color: #1d1d1f;
  outline: none;
  list-style: none; /* 隐藏默认箭头 */
}

/* 自定义漂亮的箭头 */
:deep(.tiptap summary::before) {
  content: '▼';
  font-size: 10px;
  margin-right: 12px;
  color: #0066cc;
  display: inline-block;
  transition: transform 0.2s ease;
}

:deep(.tiptap details:not([open]) summary::before) {
  transform: rotate(-90deg);
}

:deep(.tiptap details > p), 
:deep(.tiptap details > ul), 
:deep(.tiptap details > ol) {
  margin: 16px 20px !important;
}
/* SpiritText.vue 的样式区域 */

:deep(.tiptap .spirit-mention-node) {
  background: rgba(0, 102, 204, 0.1);
  color: #0066cc;
  border-radius: 4px;
  padding: 0 4px;
  font-weight: 600;
  text-decoration: none;
  cursor: pointer;
  transition: all 0.2s;
}

:deep(.tiptap .spirit-mention-node:hover) {
  background: rgba(0, 102, 204, 0.2);
  box-shadow: 0 2px 8px rgba(0, 102, 204, 0.1);
}

/* 模拟 @ 符号的效果（如果需要的话） */
:deep(.tiptap .spirit-mention-node::before) {
  content: '◈ ';
  font-size: 10px;
  opacity: 0.6;
}
</style>