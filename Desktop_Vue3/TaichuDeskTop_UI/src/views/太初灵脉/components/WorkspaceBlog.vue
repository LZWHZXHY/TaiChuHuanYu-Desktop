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
          placeholder="这里是文章的简短摘要..." 
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
import { ref, watch, onMounted, onUnmounted } from 'vue';
import { useSpiritData } from '@/composables/useSpiritData';
import { useCos } from '@/composables/useCos';

const props = defineProps<{
  title: string;
  noteId?: string;
  extraData?: string; // 🌟 彻底安全释放：右侧面板属性专属通道，不在此组件产生任何污染
}>();

const emit = defineEmits(['update:title', 'change']);

const { activeNote } = useSpiritData();
const { uploadFile } = useCos();

const localCoverUrl = ref('');
const localExcerpt = ref('');
const fileInputRef = ref<HTMLInputElement>();
let saveTimer: any = null;
let isInitialized = false; 

// 🌟 核心控制层：无缝重组与编排全量数据块
const dispatchSystemBlocks = (coverValue: string, excerptValue: string) => {
  const note = activeNote.value as any;
  if (!note) return;

  // 1. 保障底层的 blocks 容器处于就绪状态
  if (!note.blocks || !Array.isArray(note.blocks)) {
    note.blocks = [];
  }

  // 2. 剥离出现有的“非系统固定块”（即原本用户在编辑器里书写、需要排在 block 2 之后的正文块）
  const userContentBlocks = note.blocks.filter(
    (b: any) => b.type !== 'blog_fixed_cover' && b.type !== 'blog_fixed_excerpt'
  );

  // 3. 构建或修正固定的 Block 0：封面图块
  const coverBlock = {
    id: 'blog_cover_fixed_id', // 固定特殊 ID 或随机 ID
    ownerId: props.noteId,
    ownerType: 'blog',
    type: 'blog_fixed_cover',  // 固定的特殊封面形态标识
    sortOrder: 0,
    data: JSON.stringify({ url: coverValue })
  };

  // 4. 构建或修正固定的 Block 1：摘要文本块
  const excerptBlock = {
    id: 'blog_excerpt_fixed_id',
    ownerId: props.noteId,
    ownerType: 'blog',
    type: 'blog_fixed_excerpt', // 固定的特殊摘要形态标识
    sortOrder: 1,
    data: JSON.stringify({ text: excerptValue })
  };

  // 5. 重新约束用户内容块的序号，让它们强制从第 2 位向后无限顺延排列
  userContentBlocks.forEach((b: any, index: number) => {
    b.sortOrder = index + 2; 
  });

  // 6. 合流重组全量数据块链条
  note.blocks = [coverBlock, excerptBlock, ...userContentBlocks];

  // 7. 🌟 顺手把摘要和封面明文属性更新给顶层实体，以便后端发布 Handler 提取摘要时瞬间捕捉
  note.excerpt = excerptValue || '深度博客，静候回响...';
  note.coverUrl = coverValue;

  // 8. 触发数据异步贯穿与云端自动保存同步机制
  if (saveTimer) clearTimeout(saveTimer);
  saveTimer = setTimeout(() => {
    emit('change', { 
      blocks: note.blocks, 
      type: 'blog-layout' 
    });
  }, 300);
};

// 从当前激活的灵脉节点中恢复恢复数据状态
const loadBlogMeta = () => {
  const note = activeNote.value as any;
  if (!note || !note.blocks || !Array.isArray(note.blocks)) return;

  // 🌟 从固定的块形态中拉取数据还原到本地输入框里
  const coverBlock = note.blocks.find((b: any) => b.type === 'blog_fixed_cover');
  const excerptBlock = note.blocks.find((b: any) => b.type === 'blog_fixed_excerpt');

  if (coverBlock) {
    try {
      const parsed = JSON.parse(coverBlock.data);
      localCoverUrl.value = parsed.url || '';
    } catch (e) {}
  }

  if (excerptBlock) {
    try {
      const parsed = JSON.parse(excerptBlock.data);
      localExcerpt.value = parsed.text || '';
    } catch (e) {}
  }
};

const onExcerptInput = (e: Event) => {
  const target = e.target as HTMLTextAreaElement;
  localExcerpt.value = target.value;
  dispatchSystemBlocks(localCoverUrl.value, target.value);
};

const onTitleInput = (e: Event) => {
  const target = e.target as HTMLInputElement;
  emit('update:title', target.value);
};

// 封面图处理核心逻辑
const triggerCoverUpload = () => { fileInputRef.value?.click(); };
const handleFileSelected = async (e: Event) => {
  const input = e.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file || !file.type.startsWith('image/')) return;
  try {
    const result = await uploadFile(file, 'blog_cover');
    if (result?.url) {
      localCoverUrl.value = result.url;
      dispatchSystemBlocks(result.url, localExcerpt.value);
    }
  } catch (err) { console.error('封面上传感应异常:', err); }
};

const removeCover = () => {
  if (confirm('确定要移除此文章封面吗？')) {
    localCoverUrl.value = '';
    dispatchSystemBlocks('', localExcerpt.value);
  }
};

// 数据状态感应生命周期
// 🌟 完美修复：拿掉多余的冒候，恢复标准 TypeScript 响应式监听
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