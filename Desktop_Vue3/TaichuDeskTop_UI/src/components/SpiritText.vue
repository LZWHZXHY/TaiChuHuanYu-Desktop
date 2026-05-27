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
import { ref, onMounted, onUnmounted, computed} from 'vue'
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
  // 替换 SpiritText.vue 里的 onUpdate
  onUpdate: ({ editor }) => {
    if (!isInitialized.value) return;

    const currentJson = editor.getJSON();
    const currentJsonStr = JSON.stringify(currentJson);
    
    // 如果内容完全没变，直接拦截
    if (currentJsonStr === lastSyncedJson) return;

    // 🌟 修复部分：安全的浮动菜单逻辑计算
    const { state, view } = editor;
    const { $from, empty } = state.selection;
    
    // 必须确保 selection 存在，且光标是在某个节点内部 (depth > 0)
    if (empty && $from.depth > 0) {
      try {
        const textBefore = state.doc.textBetween($from.before(), $from.pos);
        const coords = view.coordsAtPos($from.pos);
        
        menuPos.value = { top: coords.bottom + 10, left: coords.left };
        showSlashMenu.value = textBefore.endsWith('/');
        showLinkSelector.value = textBefore.endsWith('[[');
      } catch (e) {
        // 极端情况下的兜底，防止菜单计算崩溃导致编辑器卡死
        showSlashMenu.value = false;
        showLinkSelector.value = false;
      }
    } else {
      // 刚导入数据或全选时，关闭悬浮菜单
      showSlashMenu.value = false;
      showLinkSelector.value = false;
    }

    // 更新内容到上层
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
/* 1. 引入公共排版引擎样式 */
@import "./SpiritTextComponents/spirit-typography.css";

/* 2. 编辑器外层容器 */
.spirit-editor-wrapper {
  position: relative;
  width: 100%;
  /* 确保内容区域能够撑开 */
  min-height: 500px; 
}

/* 3. 交互式编辑器内容区（针对 Tiptap 运行时的特殊处理） */
:deep(.tiptap) {
  /* 继承我们在 spirit-typography.css 中定义的排版类 */
  outline: none;
}

/* 🌟 编辑器特有：光标样式 */
:deep(.ProseMirror-dropcursor) {
  color: #0066cc;
  width: 2px;
}

/* 🌟 编辑器特有：被选中的节点（如图片被选中时的外框） */
:deep(.ProseMirror-selectednode) {
  outline: 2px solid #0066cc;
  box-shadow: 0 4px 20px rgba(0, 102, 204, 0.15);
}

/* 4. 浮动菜单通用样式 (Notion 风格斜杠菜单 & 关联选择器) */
.spirit-floating-menu {
  position: fixed; /* 由 computePosition 计算位置 */
  width: 280px;
  max-height: 320px;
  background: rgba(255, 255, 255, 0.95);
  backdrop-filter: blur(20px);
  border: 1px solid rgba(0, 0, 0, 0.08);
  border-radius: 14px;
  box-shadow: 0 12px 40px rgba(0, 0, 0, 0.1);
  padding: 8px;
  z-index: 9999;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.menu-header {
  font-size: 11px;
  color: #a1a1a6;
  padding: 8px 12px;
  text-transform: uppercase;
  letter-spacing: 0.1em;
  font-weight: 700;
}

.menu-scroll-area {
  overflow-y: auto;
  flex: 1;
  /* 隐藏滚动条但保留功能 */
  scrollbar-width: none;
}
.menu-scroll-area::-webkit-scrollbar { display: none; }

.menu-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  border-radius: 10px;
  cursor: pointer;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
}

.menu-item:hover {
  background: rgba(0, 102, 204, 0.05);
}

.item-icon {
  width: 32px;
  height: 32px;
  background: #f2f2f7;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  color: #1d1d1f;
}

.item-text {
  flex: 1;
  display: flex;
  flex-direction: column;
}

.main-title {
  font-size: 14px;
  font-weight: 600;
  color: #1d1d1f;
}

.sub-info {
  font-size: 11px;
  color: #86868b;
  margin-top: 2px;
}

/* 5. 菜单空状态 */
.menu-empty {
  padding: 30px 20px;
  text-align: center;
  color: #c7c7cc;
  font-size: 13px;
}

/* 6. 动画：菜单弹出效果 */
.menu-pop-enter-active,
.menu-pop-leave-active {
  transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
}

.menu-pop-enter-from,
.menu-pop-leave-to {
  opacity: 0;
  transform: scale(0.95) translateY(-10px);
}

/* 7. 响应式适配 */
@media (max-width: 768px) {
  .spirit-floating-menu {
    width: calc(100vw - 32px);
    left: 16px !important;
    right: 16px !important;
  }
}
</style>

