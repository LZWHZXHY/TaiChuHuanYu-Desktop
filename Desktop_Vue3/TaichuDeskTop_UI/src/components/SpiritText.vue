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

    <!-- 🌟 搜索菜单：笔记 + 块 -->
    <transition name="menu-pop">
      <div v-if="showLinkSelector" class="spirit-floating-menu" :style="menuStyle">
        <div class="menu-header">关联灵脉碎片...</div>
        <div class="menu-scroll-area">

          <template v-if="searchResults.notes.length > 0">
            <div class="menu-section-label">📄 笔记</div>
            <div
              v-for="note in searchResults.notes"
              :key="'n-' + note.id"
              class="menu-item"
              @click="insertLink(note, false)"
            >
              <div class="item-icon">📄</div>
              <div class="item-text">
                <div class="main-title">{{ note.title || '无标题碎片' }}</div>
              </div>
            </div>
          </template>

          <template v-if="searchResults.blocks.length > 0">
            <div class="menu-section-label">🔖 块</div>
            <div
              v-for="blk in searchResults.blocks"
              :key="'b-' + blk.id"
              class="menu-item"
              @click="insertLink(blk, true)"
            >
              <div class="item-icon">🔖</div>
              <div class="item-text">
                <div class="main-title">{{ blk.noteTitle }}</div>
                <div class="sub-info">{{ truncateText(blk.latex || blk.text, 40) }}</div>
              </div>
            </div>
          </template>

          <div
            v-if="searchResults.notes.length === 0 && searchResults.blocks.length === 0"
            class="menu-empty"
          >未找到相关碎片</div>

        </div>
      </div>
    </transition>

    <!-- 🌟 悬浮预览卡片 -->
    <Teleport to="body">
      <transition name="preview-fade">
        <div
          v-if="previewVisible"
          class="spirit-preview-card"
          :style="previewStyle"
          @mouseenter="cancelHidePreview"
          @mouseleave="scheduleHidePreview"
        >
          <div class="preview-header">
            <div class="preview-title">{{ previewData?.title || '无标题' }}</div>
            <button class="preview-pin-btn" @click.stop="pinCurrent" title="固定到右侧栏">📌</button>
          </div>
                    <div class="preview-excerpt">{{ previewData?.excerpt || '（空笔记）' }}</div>
          <div v-if="previewData?.backlinkCount > 0" class="preview-meta">
            🔗 被引用 {{ previewData.backlinkCount }} 次
          </div>
        </div>
      </transition>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, inject, watch } from 'vue'
import { useRouter } from 'vue-router'
import { EditorContent, useEditor } from '@tiptap/vue-3'
import { nanoid } from 'nanoid'
import EditorBubbleMenu from './SpiritTextComponents/EditorBubbleMenu.vue' 
import { spiritExtensions, spiritColors} from '../utils/editorConfig'
import { useSpiritData } from '../composables/useSpiritData'
import { useEditorImageUpload} from '@/composables/useEditorImageUpload.ts'
import { SlashMenuExtension } from '@/composables/slashExtension.ts'
import { lingmaiApi } from '@/api/lingmai'

const emit = defineEmits(['change'])
const { notes, currentNoteId, updateNoteContent, selectNote } = useSpiritData()
const router = useRouter()
const targetNote = notes.value.find(n => n.id === currentNoteId.value);

// ========================================================================
// 🌟 加载时给缺失 ID 的块补上 Nanoid
// ========================================================================
const assignMissingIds = (node: any) => {
  if (!node || typeof node !== 'object') return

  const idAbleTypes = [
    'paragraph', 'heading', 'blockquote', 'codeBlock',
    'bulletList', 'orderedList', 'taskList', 'taskItem',
    'image', 'spirit-link', 'panelGraph', 'details',
    'mathBlock', 'mathInline', 'pdfEmbed',
  ]

  if (node.type && idAbleTypes.includes(node.type)) {
    node.attrs = node.attrs || {}
    if (!node.attrs.id) node.attrs.id = nanoid(21)
  }

  if (Array.isArray(node.content)) {
    node.content.forEach(assignMissingIds)
  }
}

const rawInitial = targetNote?.content || { type: 'doc', content: [] }
const initialContent = JSON.parse(JSON.stringify(rawInitial))
assignMissingIds(initialContent)
// ========================================================================

const isInitialized = ref(true)
let lastSyncedJson = JSON.stringify(initialContent)
const showLinkSelector = ref(false)
const menuPos = ref({ top: 0, left: 0 })

const { cosProgress, isUploadingImage, handleImageProcess, handlePdfProcess } = useEditorImageUpload(currentNoteId, updateNoteContent, emit)

const menuStyle = computed(() => ({top: `${menuPos.value.top}px`, left: `${menuPos.value.left}px` }))

// ========================================================================
// 🌟 搜索菜单状态
// ========================================================================
const searchQuery = ref('')
const searchResults = ref<{ notes: any[], blocks: any[] }>({ notes: [], blocks: [] })

const truncateText = (text: string, len: number) => {
  if (!text) return ''
  return text.length > len ? text.slice(0, len) + '…' : text
}

let searchTimer: ReturnType<typeof setTimeout> | null = null

watch(searchQuery, (q) => {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(async () => {
    if (!q || !q.trim()) {
      // 空查询：显示所有本地笔记
      searchResults.value = {
        notes: notes.value.filter(n => n.id !== currentNoteId.value).slice(0, 20),
        blocks: [],
      }
      return
    }
    try {
      const res: any = await lingmaiApi.search(q.trim())
      searchResults.value = {
        notes: res.notes || [],
        blocks: res.blocks || [],
      }
    } catch (e) {
      console.error('搜索失败', e)
    }
  }, 200)
})
// ========================================================================

// ========================================================================
// 🌟 悬浮预览状态
// ========================================================================
const previewVisible = ref(false)
const previewData = ref<any>(null)
const previewPosition = ref({ x: 0, y: 0 })

let previewTimer: ReturnType<typeof setTimeout> | null = null
let hideTimer: ReturnType<typeof setTimeout> | null = null
let currentHoverKey: string | null = null

const pinnedNoteIds = inject<any>('pinnedNoteIds', ref([]))

const previewStyle = computed(() => ({
  top: `${previewPosition.value.y + 16}px`,
  left: `${previewPosition.value.x + 16}px`,
}))

const scheduleHidePreview = () => {
  if (hideTimer) clearTimeout(hideTimer)
  hideTimer = setTimeout(() => {
    previewVisible.value = false
  }, 200)
}

const cancelHidePreview = () => {
  if (hideTimer) clearTimeout(hideTimer)
}

const handleLinkHover = (e: MouseEvent) => {
  const target = e.target as HTMLElement
  const linkEl = target.closest('[data-spirit-id]') as HTMLElement | null
  if (!linkEl) return

  const noteId = linkEl.getAttribute('data-spirit-id')
  const blockId = linkEl.getAttribute('data-block-id') || null
  if (!noteId) return

  const hoverKey = blockId ? `${noteId}#${blockId}` : noteId
  if (currentHoverKey === hoverKey && previewVisible.value) return

  currentHoverKey = hoverKey

  if (previewTimer) clearTimeout(previewTimer)
  cancelHidePreview()

  previewPosition.value = { x: e.clientX, y: e.clientY }

  previewTimer = setTimeout(async () => {
    try {
           if (blockId) {
        const res: any = await lingmaiApi.getBlockPreview(blockId) as any
        if (currentHoverKey !== hoverKey) return
        previewData.value = {
          id: res.noteId,
          title: res.noteTitle || '无标题',
          excerpt: res.latex || res.text || '（空块）',
          backlinkCount: res.backlinkCount || 0,
        }
      } else {
        const res: any = await lingmaiApi.getNotePreview(noteId) as any
        if (currentHoverKey !== hoverKey) return
        previewData.value = {
          ...res,
          backlinkCount: res.backlinkCount || 0,
        }
      }
      previewVisible.value = true
    } catch (err) {
      console.error('预览失败', err)
    }
  }, 300)
}

const handleMouseMove = (e: MouseEvent) => {
  if (!previewVisible.value) return
  const target = e.target as HTMLElement
  const linkEl = target.closest('[data-spirit-id]')
  if (linkEl) {
    previewPosition.value = { x: e.clientX, y: e.clientY }
  }
}

const handleLinkLeave = (e: MouseEvent) => {
  const target = e.target as HTMLElement
  const linkEl = target.closest('[data-spirit-id]')
  if (!linkEl) return

  currentHoverKey = null
  if (previewTimer) clearTimeout(previewTimer)
  scheduleHidePreview()
}

const pinCurrent = () => {
  if (!previewData.value?.id) return
  const id = previewData.value.id
  if (!pinnedNoteIds.value.includes(id)) {
    pinnedNoteIds.value = [...pinnedNoteIds.value, id]
  }
  previewVisible.value = false
  currentHoverKey = null
}
// ========================================================================

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

        if (file.type === 'application/pdf') {
          handlePdfProcess(editor, view, file, coordinates?.pos);
          return true;
        }
        if (file.type.startsWith('image/')) {
          handleImageProcess(editor, view, file, coordinates?.pos);
          return true;
        }
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

        // 🌟 匹配 [[ 后面的内容
        const match = textBefore.match(/\[\[([^\[\]]*)$/);
        if (match) {
          showLinkSelector.value = true;
          searchQuery.value = match[1];
        } else {
          showLinkSelector.value = false;
        }
      } catch (e) {
        showLinkSelector.value = false;
      }
    } else {
      showLinkSelector.value = false;
    }

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
    const blockId = node.getAttribute('data-block-id');
    if (noteId) {
      router.push({
        name: 'SpiritNote',
        params: { id: noteId },
        query: blockId ? { block: blockId } : {},
      });
    }
  }
};

// 🌟 插入引用：isBlock 决定是整篇还是块
const insertLink = (item: any, isBlock: boolean) => {
  if (!editor.value) return

  const attrs = isBlock
    ? { id: item.noteId, blockId: item.id, title: item.noteTitle || '未命名' }
    : { id: item.id, blockId: null, title: item.title || '未命名' }

  const deleteLength = 2 + searchQuery.value.length
  const from = editor.value.state.selection.$from.pos - deleteLength
  const to = editor.value.state.selection.$from.pos

  editor.value.chain()
    .focus()
    .deleteRange({ from, to })
    .insertContent({
      type: 'spirit-link',
      attrs,
    })
    .insertContent(' ')
    .run()

  showLinkSelector.value = false
  searchQuery.value = ''
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

const handleSlashPdfInsert = (e: Event) => {
  const customEvent = e as CustomEvent;
  if (editor.value && customEvent.detail) {
    const { file, pos } = customEvent.detail;
    handlePdfProcess(editor, editor.value.view, file, pos);
  }
};

let editorDom: HTMLElement | null = null

onMounted(() => {
  window.addEventListener('mousedown', closeMenus);
  document.addEventListener('click', handleLinkNavigation, { capture: true });
 
  if (editor.value && editor.value.view) {
    editorDom = editor.value.view.dom as HTMLElement
    editorDom.addEventListener('spirit-insert-image', handleSlashImageInsert);
    editorDom.addEventListener('spirit-insert-pdf', handleSlashPdfInsert);
    editorDom.addEventListener('mouseover', handleLinkHover);
    editorDom.addEventListener('mouseout', handleLinkLeave);
    editorDom.addEventListener('mousemove', handleMouseMove);
  }
});

onUnmounted(() => {
  window.removeEventListener('mousedown', closeMenus);
  document.removeEventListener('click', handleLinkNavigation, { capture: true });

  if (editorDom) {
    editorDom.removeEventListener('spirit-insert-image', handleSlashImageInsert);
    editorDom.removeEventListener('spirit-insert-pdf', handleSlashPdfInsert);
    editorDom.removeEventListener('mouseover', handleLinkHover);
    editorDom.removeEventListener('mouseout', handleLinkLeave);
    editorDom.removeEventListener('mousemove', handleMouseMove);
    editorDom = null
  }
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
  width: 300px;
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

.menu-section-label {
  font-size: 10px;
  color: #a1a1a6;
  padding: 8px 12px 4px;
  font-weight: 700;
  letter-spacing: 0.05em;
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
  padding: 8px 12px;
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
  width: 28px;
  height: 28px;
  background: #f2f2f7;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  color: #1d1d1f;
  transition: all 0.2s;
  flex-shrink: 0;
}

.item-text {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.main-title {
  font-size: 13px;
  font-weight: 600;
  color: #1d1d1f;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sub-info {
  font-size: 11px;
  color: #86868b;
  margin-top: 2px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
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

/* 🌟 悬浮预览卡片样式 */
.spirit-preview-card {
  position: fixed;
  z-index: 99999;
  width: 340px;
  max-height: 260px;
  background: rgba(255, 255, 255, 0.98);
  backdrop-filter: blur(20px) saturate(180%);
  border: 1px solid rgba(0, 0, 0, 0.08);
  border-radius: 12px;
  box-shadow: 0 12px 40px rgba(0, 0, 0, 0.12), 0 2px 8px rgba(0, 0, 0, 0.04);
  padding: 14px 16px;
  pointer-events: auto;
  overflow: hidden;
  box-sizing: border-box;
}

.preview-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}

.preview-title {
  font-size: 14px;
  font-weight: 700;
  color: #1d1d1f;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  flex: 1;
}

.preview-pin-btn {
  background: none;
  border: none;
  font-size: 14px;
  cursor: pointer;
  padding: 2px 4px;
  border-radius: 4px;
  transition: background 0.15s;
  flex-shrink: 0;
}

.preview-pin-btn:hover {
  background: rgba(0, 102, 204, 0.08);
}

.preview-excerpt {
  font-size: 12px;
  line-height: 1.6;
  color: #515154;
  display: -webkit-box;
  -webkit-line-clamp: 8;
  -webkit-box-orient: vertical;
  overflow: hidden;
  white-space: pre-wrap;
  word-break: break-word;
}

.preview-fade-enter-active,
.preview-fade-leave-active {
  transition: opacity 0.15s ease, transform 0.15s ease;
}
.preview-fade-enter-from,
.preview-fade-leave-to {
  opacity: 0;
  transform: translateY(4px);
}

.preview-meta {
  margin-top: 8px;
  padding-top: 8px;
  border-top: 1px dashed #e5e5ea;
  font-size: 11px;
  color: #86868b;
  font-weight: 500;
}

@media (max-width: 768px) {
  .spirit-floating-menu {
    width: calc(100vw - 32px);
    left: 16px !important;
    right: 16px !important;
  }
}
</style>