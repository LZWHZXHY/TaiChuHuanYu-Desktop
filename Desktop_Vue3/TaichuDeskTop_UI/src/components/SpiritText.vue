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
      <div v-if="showSlashMenu" class="spirit-floating-menu" :style="menuStyle">
        <div class="menu-header">灵脉指令</div>
        <div 
          v-for="(cmd, index) in slashCommands" 
          :key="cmd.label" 
          :class="['menu-item', { 'is-active': index === activeCommandIndex }]" 
          @click="runSlashCommand(cmd)"
          @mouseenter="activeCommandIndex = index"
        >
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
import { ref, onMounted, onUnmounted, computed, watch } from 'vue'
import { EditorContent, useEditor } from '@tiptap/vue-3'
import EditorBubbleMenu from './SpiritTextComponents/EditorBubbleMenu.vue' 
import { spiritExtensions, spiritColors, slashCommands } from '../utils/editorConfig'
import { useSpiritData } from '../composables/useSpiritData'
import { useCos } from '../composables/useCos'

// 🌟 新增：向外广播内容变化的事件
const emit = defineEmits(['change'])

// 1. 接入数据大脑
const { notes, currentNoteId, updateNoteContent, selectNote } = useSpiritData()

// 🌟 引入 progress 并重命名为 cosProgress，方便在模板中无缝绑定进度
const { uploadFile, progress: cosProgress } = useCos()

const targetNote = notes.value.find(n => n.id === currentNoteId.value);
const initialContent = targetNote?.content || { type: 'doc', content: [] };

// --- 状态控制 ---
const isInitialized = ref(true) // 数据肯定是有的，直接就是 true
let lastSyncedJson = JSON.stringify(initialContent) // 初始防抖对比值

const activeCommandIndex = ref(0)
const showSlashMenu = ref(false)
const showLinkSelector = ref(false)
const menuPos = ref({ top: 0, left: 0 })

// 🔒 核心状态锁：拦截异步上传期间的高频自动云端同步
const isUploadingImage = ref(false)

const menuStyle = computed(() => ({
  top: `${menuPos.value.top}px`,
  left: `${menuPos.value.left}px`
}))

const availableNotes = computed(() => {
  return notes.value.filter(n => n.id !== currentNoteId.value)
})

// 监听斜杠菜单开启，自动将高亮索引归零
watch(showSlashMenu, (visible) => {
  if (visible) {
    activeCommandIndex.value = 0
  }
})

/**
 * 🌟 核心图片处理：COS 上传 + 节点插入
 */
const handleImageProcess = async (view: any, file: File, pos?: number) => {
  if (!file.type.startsWith('image/')) return;
  
  // 提前生成一个独一无二的占位 ID，用于等下精准替换节点
  const placeholderId = `spirit_img_loading_${Date.now()}`;
  
  try {
    // 1. 锁死自动同步：防止异步上传空窗期内，其他 DOM 节点扰动触发高频 SaveChanges
    isUploadingImage.value = true;

    // 🌟 极致体验优化：先在光标处插入一个带有极简优雅动画的假图节点作为“占位骨架屏”
    const { schema } = view.state;
    const placeholderNode = schema.nodes.image.create({
      src: 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg"/>', // 空白骨架兜底
      align: 'center',
      width: '100%',
      alt: placeholderId // 用 alt 属性作为一个暂时的影子暗号
    });

    let tr = pos ? view.state.tr.insert(pos, placeholderNode) : view.state.tr.replaceSelectionWith(placeholderNode);
    view.dispatch(tr);

    // 2. 扔给 useCos 开始异步向北京地域的 Bucket 传输
    const result = await uploadFile(file, 'lingmai');
    
    // 3. 异步传完，拿到真实 https://img.bianyuzhou.com 链接后，去文档里精准捕获刚刚那个暗号并替换它
    view.state.doc.descendants((node: any, nodePos: number) => {
      if (node.type.name === 'image' && node.attrs.alt === placeholderId) {
        const realImageNode = schema.nodes.image.create({
          src: result.url,
          align: 'center',
          width: '100%',
          caption: '' // 清空题注占位
        });
        
        // 执行就地解构与无缝替换
        const replaceTr = view.state.tr.replaceWith(nodePos, nodePos + node.nodeSize, realImageNode);
        view.dispatch(replaceTr);
        return false;
      }
    });

    // 4. 解除状态锁：此时 Tiptap 的 DOM 已经彻底稳定
    isUploadingImage.value = false;

    // 5. 单次确定性后置同步：打包包含新插入图片在内的整篇内容，仅向后端发送唯一一次保存请求
    const finalJson = editor.value?.getJSON();
    if (finalJson) {
      updateNoteContent(currentNoteId.value, finalJson);
      lastSyncedJson = JSON.stringify(finalJson);
      
      // 🌟 图片上传完毕后，也要通知父组件内容变了，以便更新画廊状态校验
      emit('change', finalJson);
    }

  } catch (err) {
    // 6. 异常容错清理：若上传失败，必须把刚刚的假占位暗号节点在文档里彻底抹去
    view.state.doc.descendants((node: any, nodePos: number) => {
      if (node.type.name === 'image' && node.attrs.alt === placeholderId) {
        const deleteTr = view.state.tr.delete(nodePos, nodePos + node.nodeSize);
        view.dispatch(deleteTr);
        return false;
      }
    });
    
    isUploadingImage.value = false;
    console.error('灵脉图片处理失败:', err);
  }
};

// 响应并处理从 editorConfig 发送过来的自定义事件信号
const handleSlashImageInsert = (e: Event) => {
  const customEvent = e as CustomEvent;
  if (editor.value && customEvent.detail) {
    const { file, pos } = customEvent.detail;
    handleImageProcess(editor.value.view, file, pos);
  }
};

// 🌟 核心键盘劫持处理器（在捕获阶段拦截上下键与回车）
const handleKeyDown = (e: KeyboardEvent) => {
  if (!showSlashMenu.value) return

  if (e.key === 'ArrowDown') {
    e.preventDefault()
    e.stopPropagation()
    activeCommandIndex.value = (activeCommandIndex.value + 1) % slashCommands.length
    scrollActiveItemIntoView()
  } 
  else if (e.key === 'ArrowUp') {
    e.preventDefault()
    e.stopPropagation()
    activeCommandIndex.value = (activeCommandIndex.value - 1 + slashCommands.length) % slashCommands.length
    scrollActiveItemIntoView()
  } 
  else if (e.key === 'Enter') {
    e.preventDefault()
    e.stopPropagation()
    const targetCmd = slashCommands[activeCommandIndex.value]
    if (targetCmd) {
      runSlashCommand(targetCmd)
    }
  } 
  else if (e.key === 'Escape') {
    showSlashMenu.value = false
  }
}

// 自动随按键滚动菜单可视区域
const scrollActiveItemIntoView = () => {
  const menuEl = document.querySelector('.spirit-floating-menu')
  if (!menuEl) return
  const activeItem = menuEl.querySelectorAll('.menu-item')[activeCommandIndex.value] as HTMLElement
  if (activeItem) {
    activeItem.scrollIntoView({ block: 'nearest' })
  }
}

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

    // 🔒 拦截逻辑：若图片处于异步上传中，强行切断自动防抖同步，保护后端 EF 实体跟踪不发生位移
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
        showSlashMenu.value = textBefore.endsWith('/');
        showLinkSelector.value = textBefore.endsWith('[[');
      } catch (e) {
        showSlashMenu.value = false;
        showLinkSelector.value = false;
      }
    } else {
      showSlashMenu.value = false;
      showLinkSelector.value = false;
    }

    updateNoteContent(currentNoteId.value, currentJson);
    lastSyncedJson = currentJsonStr; 
    
    // 🌟 核心变动：将最新的数据抛给外部，供 index.vue 进行多态类型校验
    emit('change', currentJson);
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
  document.addEventListener('keydown', handleKeyDown, true); // 在捕获阶段拦截键盘按键

  if (editor.value) {
    editor.value.view.dom.addEventListener('spirit-insert-image', handleSlashImageInsert);
  }
});

onUnmounted(() => {
  window.removeEventListener('mousedown', closeMenus);
  document.removeEventListener('click', handleLinkNavigation, { capture: true });
  document.removeEventListener('keydown', handleKeyDown, true);
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
/* 1. 引入公共排版引擎样式 */


/* 2. 编辑器外层容器 */
.spirit-editor-wrapper {
  position: relative;
  width: 100%;
  min-height: 500px; 
}

/* 🌟 3. 极致简约风格的顶部微光流关进度条 */
.spirit-top-progress-loader {
  position: fixed;
  top: 0;
  left: 0;
  height: 4px;
  background: linear-gradient(90deg, #0066cc, #34c759); /* 灵脉蓝到治愈绿的星图渐变 */
  z-index: 10000;
  box-shadow: 0 1px 6px rgba(0, 102, 204, 0.3);
  transition: width 0.2s cubic-bezier(0.1, 0.8, 0.1, 1);
}

/* 🌟 4. 当富文本检测到含有暗号的占位图节点时，赋予其专属的微光骨架屏样式 */
:deep(img[alt^="spirit_img_loading_"]) {
  width: 100%;
  height: 180px; /* 优雅的预留高度占位 */
  border-radius: 12px;
  background: linear-gradient(90deg, #f5f5f7 25%, #e8e8ed 37%, #f5f5f7 63%);
  background-size: 400% 100%;
  animation: spiritSkeletonShimmer 1.4s ease infinite;
  content: "" !important; /* 隐藏破碎图图标 */
  display: block;
}

@keyframes spiritSkeletonShimmer {
  0% { background-position: 100% 50%; }
  100% { background-position: 0% 50%; }
}

/* 交互式编辑器内容区 */
:deep(.tiptap) {
  outline: none;
}

/* 编辑器特有：光标样式 */
:deep(.ProseMirror-dropcursor) {
  color: #0066cc;
  width: 2px;
}

/* 被选中的节点（如图片被选中时的外框） */
:deep(.ProseMirror-selectednode) {
  outline: 2px solid #0066cc;
  box-shadow: 0 4px 20px rgba(0, 102, 204, 0.15);
}

/* 浮动菜单通用样式 */
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