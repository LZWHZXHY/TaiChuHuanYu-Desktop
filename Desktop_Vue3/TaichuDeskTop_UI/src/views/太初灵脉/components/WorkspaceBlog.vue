<template>
  <div class="workspace-blog-frame">
    <div class="blog-cover-wrapper">
      <div class="blog-cover-area">
        <img v-if="localCoverUrl" :src="localCoverUrl" class="cover-image" alt="文章封面" />
        <div v-else class="cover-placeholder">
          <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="#ccc">
            <rect x="3" y="3" width="18" height="18" rx="2" stroke-width="1.5" />
            <circle cx="8.5" cy="8.5" r="2.5" stroke-width="1.5" />
            <path d="M21 15L16 10L5 21" stroke-width="1.5" />
          </svg>
          <span class="placeholder-tip">添加引人入胜的视觉封面</span>
        </div>
        
        <button class="cover-upload-btn" @click="triggerCoverUpload">
          <span>{{ localCoverUrl ? '更换封面' : '上传封面图' }}</span>
        </button>
        <button v-if="localCoverUrl" class="remove-cover-btn" @click="removeCover">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor">
            <path d="M18 6L6 18M6 6l12 12" stroke-width="1.5" />
          </svg>
        </button>
      </div>
    </div>

    <header class="blog-header">
      <input 
        :value="props.title" 
        @input="onTitleInput" 
        class="blog-title-input" 
        placeholder="在这里输入文章标题..." 
      />
      
      <div class="blog-excerpt-section">
        <textarea 
          :value="localExcerpt" 
          @input="onExcerptInput" 
          class="excerpt-textarea"
          placeholder="这里是文章的第一段，也是对外的简短摘要..." 
          rows="2" 
        />
      </div>
    </header>

    <article class="blog-main-content">
      <slot name="editor"></slot>
    </article>

    <input ref="fileInputRef" type="file" accept="image/*" style="display: none" @change="handleFileSelected" />
  </div>
</template>

<script setup lang="ts">
import { ref, watch, nextTick, onMounted, onUnmounted } from 'vue';
import { useSpiritData } from '@/composables/useSpiritData';
import { useCos } from '@/composables/useCos';

const props = defineProps<{
  title: string;
  noteId?: string;
  extraData?: string; 
}>();

const emit = defineEmits(['update:title', 'change']);

const { activeNote } = useSpiritData();
const { uploadFile } = useCos();

const localCoverUrl = ref('');
const localExcerpt = ref('');
const fileInputRef = ref<HTMLInputElement>();
let saveTimer: any = null;
let isInitialized = false; 

// 🌟 深度递归榨取 Tiptap 段落节点里的纯文本
const extractTextFromNode = (node: any): string => {
  if (!node) return '';
  if (node.text) return node.text;
  if (node.content && Array.isArray(node.content)) {
    return node.content.map(extractTextFromNode).join('');
  }
  return '';
};

// 🌟 从当前激活的灵脉节点中恢复数据
const loadBlogMeta = () => {
  const note = activeNote.value as any;
  if (!note) return;

  // 1. 封面图依然走独立的 extraData 或者是基础配置解析
  if (props.extraData && props.extraData !== '[]' && props.extraData !== '{}') {
    try {
      const meta = JSON.parse(props.extraData);
      if (meta && typeof meta === 'object' && !Array.isArray(meta)) {
        localCoverUrl.value = meta.coverUrl || '';
      }
    } catch (e) {}
  }

  // 2. ✨【核心变动】：不再读取自定义块，直接寻找富文本正文中的第一个有效 paragraph
  if (note.blocks && Array.isArray(note.blocks)) {
    const firstPara = note.blocks.find((b: any) => b.type === 'paragraph');
    if (firstPara) {
      try {
        const blockData = typeof firstPara.data === 'string' ? JSON.parse(firstPara.data) : firstPara.data;
        // 把段落深层嵌套的文字榨取出来，同步给摘要输入框
        localExcerpt.value = extractTextFromNode(blockData);
      } catch (e) {
        localExcerpt.value = '';
      }
    } else {
      localExcerpt.value = '';
    }
  }
};

// 🌟 当用户在摘要框打字时，反向去修改富文本编辑器的第一段内容
const onExcerptInput = (e: Event) => {
  const target = e.target as HTMLTextAreaElement;
  localExcerpt.value = target.value;

  // 1. 获取外层插槽传进来的 Tiptap 编辑器实例
  // 通过当前项目的架构，Tiptap 挂载在外层的组件或者 DOM 上，我们可以直接利用系统的全局 activeNote 的数据联动，
  // 或者直接去精准重组第一个 block 的数据向外抛出：
  const note = activeNote.value as any;
  if (!note || !note.blocks || !Array.isArray(note.blocks)) return;

  // 2. 找到第一个段落块
  let firstPara = note.blocks.find((b: any) => b.type === 'paragraph');
  
  // 3. 重新组装该 Tiptap 节点的内部 JSON 树结构
  const newParagraphData = {
    type: 'paragraph',
    content: target.value ? [{ type: 'text', text: target.value }] : []
  };

  if (firstPara) {
    firstPara.data = JSON.stringify(newParagraphData);
  } else {
    // 如果正文是空的，我们主动帮它创建一个初始段落块
    firstPara = {
      id: Math.random().toString(36).substring(2, 11),
      ownerId: props.noteId,
      ownerType: 'blog',
      type: 'paragraph',
      sortOrder: 0,
      data: JSON.stringify(newParagraphData)
    };
    note.blocks.unshift(firstPara);
  }

  // 4. 重新刷一遍排序序号，确保万无一失
  note.blocks.forEach((b: any, idx: number) => b.sortOrder = idx);

  // 5. 触发外层统一同步通道，顺便把封面数据通过 extraData 的形式抛出去
  if (saveTimer) clearTimeout(saveTimer);
  saveTimer = setTimeout(() => {
    emit('change', { 
      blocks: note.blocks, 
      type: 'blog-layout' 
    });
  }, 300);
};

const onTitleInput = (e: Event) => {
  const target = e.target as HTMLInputElement;
  emit('update:title', target.value);
};

// 封面图处理逻辑
const triggerCoverUpload = () => { fileInputRef.value?.click(); };
const handleFileSelected = async (e: Event) => {
  const input = e.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file || !file.type.startsWith('image/')) return;
  try {
    const result = await uploadFile(file, 'blog_cover');
    if (result?.url) {
      localCoverUrl.value = result.url;
      // 将封面图序列化存进 extraData 里
      const currentNote = activeNote.value as any;
      if (currentNote) {
        currentNote.extraData = JSON.stringify({ coverUrl: result.url });
        emit('change', { blocks: currentNote.blocks, type: 'blog-layout' });
      }
    }
  } catch (err) { console.error(err); }
};
const removeCover = () => {
  if (confirm('确定要移除此文章封面吗？')) {
    localCoverUrl.value = '';
    const currentNote = activeNote.value as any;
    if (currentNote) {
      currentNote.extraData = '[]';
      emit('change', { blocks: currentNote.blocks, type: 'blog-layout' });
    }
  }
};

// 数据状态感应
watch(
  () => activeNote.value,
  (newNote) => {
    if (!newNote) return;
    if ((newNote as any).blocks !== undefined) {
      loadBlogMeta();
      if (!isInitialized) {
        isInitialized = true;
      }
    }
  },
  { immediate: true, deep: true }
);

onMounted(() => {
  if (activeNote.value && !isInitialized) {
    loadBlogMeta();
    isInitialized = true;
  }
});

onUnmounted(() => {
  if (saveTimer) clearTimeout(saveTimer);
});
</script>

<style scoped>
/* 样式保持不变，与此前美学规范完全一致 */
.workspace-blog-frame { max-width: 820px; margin: 0 auto; padding: 24px 24px 100px; background: #ffffff; }
.blog-cover-wrapper { margin-bottom: 36px; }
.blog-cover-area { position: relative; width: 100%; aspect-ratio: 21 / 9; border-radius: 20px; overflow: hidden; background: #f5f5f7; display: flex; align-items: center; justify-content: center; border: 1px solid rgba(0, 0, 0, 0.03); transition: box-shadow 0.3s ease; }
.blog-cover-area:hover { box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05); }
.cover-image { width: 100%; height: 100%; object-fit: cover; transition: transform 0.4s cubic-bezier(0.16, 1, 0.3, 1); }
.blog-cover-area:hover .cover-image { transform: scale(1.01); }
.cover-placeholder { display: flex; flex-direction: column; align-items: center; gap: 12px; color: #b0b0b5; }
.placeholder-tip { font-size: 13px; letter-spacing: 0.02em; }
.cover-upload-btn { position: absolute; bottom: 16px; right: 16px; background: rgba(255, 255, 255, 0.85); backdrop-filter: blur(16px); border: 1px solid rgba(0, 0, 0, 0.06); color: #1d1d1f; padding: 8px 16px; border-radius: 30px; font-size: 13px; font-weight: 500; cursor: pointer; opacity: 0; transform: translateY(4px); transition: all 0.25s cubic-bezier(0.16, 1, 0.3, 1); z-index: 5; box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04); }
.blog-cover-area:hover .cover-upload-btn, .cover-upload-btn:focus-within { opacity: 1; transform: translateY(0); }
.cover-upload-btn:hover { background: #ffffff; color: #0066cc; }
.remove-cover-btn { position: absolute; top: 16px; right: 16px; background: rgba(0, 0, 0, 0.5); backdrop-filter: blur(8px); border: none; color: white; width: 32px; height: 32px; border-radius: 50%; display: flex; align-items: center; justify-content: center; cursor: pointer; opacity: 0; transition: opacity 0.2s, background-color 0.2s; z-index: 5; }
.blog-cover-area:hover .remove-cover-btn { opacity: 1; }
.remove-cover-btn:hover { background: rgba(229, 72, 77, 0.9); }
.blog-header { margin-bottom: 40px; }
.blog-title-input { width: 100%; font-size: 2.6rem; font-weight: 800; border: none; background: transparent; outline: none; color: #1d1d1f; line-height: 1.25; letter-spacing: -0.03em; padding: 6px 0; }
.blog-excerpt-section { margin-top: 16px; border-left: 3px solid #e2e2e7; padding-left: 16px; }
.excerpt-textarea { width: 100%; border: none; background: transparent; outline: none; font-size: 1.05rem; line-height: 1.6; color: #515154; font-family: inherit; resize: none; }
.blog-main-content { width: 100%; min-height: 450px; }
@media (max-width: 768px) {
  .workspace-blog-frame { padding: 12px 16px 60px; }
  .blog-title-input { font-size: 1.95rem; }
  .blog-cover-area { aspect-ratio: 16 / 9; }
  .cover-upload-btn, .remove-cover-btn { opacity: 1; }
}
</style>