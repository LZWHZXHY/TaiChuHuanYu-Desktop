<template>
  <div class="spirit-editor-wrapper">
    <transition name="line-fade">
      <div 
        v-if="isUploadingImage" 
        class="spirit-top-progress-loader" 
        :style="{ width: cosProgress + '%' }"
      ></div>
    </transition>

    <EditorBubbleMenu 
      v-if="editor" 
      :editor="editor" 
      :colors="spiritColors" 
    />

    <editor-content :editor="editor" class="spirit-typography-engine" />
    <transition name="menu-pop">
      <div v-if="showLinkSelector" class="spirit-floating-menu" :style="menuStyle">
        <div class="menu-header">关联灵脉碎片...</div>
        <div class="menu-scroll-area">
          <div v-for="note in availableNotes" :key="note.id" class="menu-item" @click="insertBiLink(note)">
            <div class="item-icon">📄</div>
            <div class="item-text">
              <div class="main-title">{{ note.title || '无标题碎片' }}</div>
              <div class="sub-info">{{ note.isPublic ? '公开' : '私有' }}</div> 
            </div>
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
import { spiritExtensions, spiritColors} from '../utils/editorConfig'
import { useSpiritData } from '../composables/useSpiritData'
import { useEditorImageUpload} from '@/composables/useEditorImageUpload.ts'
import { SlashMenuExtension } from '@/composables/slashExtension.ts'

const emit = defineEmits(['change'])
const { notes, currentNoteId, updateNoteContent, selectNote } = useSpiritData()
const targetNote = notes.value.find(n => n.id === currentNoteId.value);
const initialContent = targetNote?.content || { type: 'doc', content: [] };
const isInitialized = ref(true) // 数据肯定是有的，直接就是 true
let lastSyncedJson = JSON.stringify(initialContent) // 初始防抖对比值
const showLinkSelector = ref(false)
const menuPos = ref({ top: 0, left: 0 })
const { cosProgress, isUploadingImage, handleImageProcess } = useEditorImageUpload(currentNoteId, updateNoteContent, emit)  //提炼后的图片上传功能 已模块化
const menuStyle = computed(() => ({top: `${menuPos.value.top}px`, left: `${menuPos.value.left}px` }))
const availableNotes = computed(() => {
  return notes.value.filter(n => n.id !== currentNoteId.value)
})

const editor = useEditor({
  extensions: [
    ...spiritExtensions, 
    SlashMenuExtension 
  ],
  content: initialContent, 
  editorProps: {
    handleDrop: (view, event, slice, moved) => {
      if (!moved && event.dataTransfer?.files?.length) {
        const file = event.dataTransfer.files[0];
        const coordinates = view.posAtCoords({ left: event.clientX, top: event.clientY });
        handleImageProcess(editor, view, file, coordinates?.pos);
        return true; 
      }
      return false;
    },
    handlePaste: (view, event) => {
      const items = event.clipboardData?.items;
      if (items) {
        for (const item of items) {
          if (item.type.startsWith('image/')) {
            const file = item.getAsFile();
            if (file) {
              handleImageProcess(editor, view, file);
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
    if (isUploadingImage.value) return;

    const currentJson = editor.getJSON();
    const currentJsonStr = JSON.stringify(currentJson);
    
    if (currentJsonStr === lastSyncedJson) return;

    const { state, view } = editor;
    const { $from, empty } = state.selection;
    
    if (empty && $from.depth > 0) {
      try {
        const textBefore = state.doc.textBetween($from.before(), $from.pos);
        const coords = view.coordsAtPos($from.pos);
        
        menuPos.value = { top: coords.bottom + 10, left: coords.left };
        showLinkSelector.value = textBefore.endsWith('[[');
      } catch (e) {
        showLinkSelector.value = false;
      }
    } else {
      showLinkSelector.value = false;
    }

    // 🌟 原生事件广播 (保持不变)
    editor.view.dom.dispatchEvent(new CustomEvent('change-content', {
      bubbles: true, 
      detail: currentJson
    }));

    updateNoteContent(currentNoteId.value, currentJson);
    lastSyncedJson = currentJsonStr; 
    
    emit('change', currentJson);
  }
})


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
    showLinkSelector.value = false
  }
}

const handleSlashImageInsert = (e: Event) => {
  const customEvent = e as CustomEvent;
  if (editor.value && customEvent.detail) {
    const { file, pos } = customEvent.detail;
    
    handleImageProcess(editor, editor.value.view, file, pos);
  }
};

onMounted(() => {
  window.addEventListener('mousedown', closeMenus);
  document.addEventListener('click', handleLinkNavigation, { capture: true });
 
  if (editor.value && editor.value.view) {
    editor.value.view.dom.addEventListener('spirit-insert-image', handleSlashImageInsert);
  }
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

<style>
@import "./SpiritTextComponents/spirit-typography.css";
</style>

<style scoped>
.spirit-editor-wrapper {
  position: relative;
  width: 100%;
  min-height: 500px; 
}

.spirit-top-progress-loader {
  position: fixed;
  top: 0;
  left: 0;
  height: 4px;
  background: linear-gradient(90deg, #0066cc, #34c759); 
  z-index: 10000;
  box-shadow: 0 1px 6px rgba(0, 102, 204, 0.3);
  transition: width 0.2s cubic-bezier(0.1, 0.8, 0.1, 1);
}

:deep(img[alt^="spirit_img_loading_"]) {
  width: 100%;
  height: 180px; 
  border-radius: 12px;
  background: linear-gradient(90deg, #f5f5f7 25%, #e8e8ed 37%, #f5f5f7 63%);
  background-size: 400% 100%;
  animation: spiritSkeletonShimmer 1.4s ease infinite;
  content: "" !important; 
  display: block;
}

@keyframes spiritSkeletonShimmer {
  0% { background-position: 100% 50%; }
  100% { background-position: 0% 50%; }
}

:deep(.tiptap) {
  outline: none;
}

:deep(.ProseMirror-dropcursor) {
  color: #0066cc;
  width: 2px;
}

:deep(.ProseMirror-selectednode) {
  outline: 2px solid #0066cc;
  box-shadow: 0 4px 20px rgba(0, 102, 204, 0.15);
}

.spirit-floating-menu {
  position: fixed; 
  width: 280px;
  max-height: 260px;
  background: rgba(255, 255, 255, 0.95);
  backdrop-filter: blur(20px);
  border: 1px solid rgba(0, 0, 0, 0.08);
  border-radius: 14px;
  box-shadow: 0 12px 40px rgba(0, 0, 0, 0.1);
  padding: 8px;
  z-index: 9999;
  display: flex;
  flex-direction: column;
  overflow-y: auto;
  overscroll-behavior: contain;
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

.menu-item:hover,
.menu-item.is-active {
  background: rgba(0, 102, 204, 0.06);
  color: #0066cc;
}

.menu-item:hover .item-icon,
.menu-item.is-active .item-icon {
  background: #0066cc;
  color: #ffffff;
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
  transition: all 0.2s;
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

.menu-empty {
  padding: 30px 20px;
  text-align: center;
  color: #c7c7cc;
  font-size: 13px;
}

.menu-pop-enter-active,
.menu-pop-leave-active {
  transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
}

.menu-pop-enter-from,
.menu-pop-leave-to {
  opacity: 0;
  transform: scale(0.95) translateY(-10px);
}

.line-fade-enter-active,
.line-fade-leave-active {
  transition: opacity 0.3s ease;
}
.line-fade-enter-from,
.line-fade-leave-to {
  opacity: 0;
}

@media (max-width: 768px) {
  .spirit-floating-menu {
    width: calc(100vw - 32px);
    left: 16px !important;
    right: 16px !important;
  }
}
</style>