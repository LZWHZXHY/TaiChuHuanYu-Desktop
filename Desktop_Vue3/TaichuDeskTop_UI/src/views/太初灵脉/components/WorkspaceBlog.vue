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
      <!-- 🔥 传递 cleanContent 和 editorKey，父组件可将 editorKey 绑定到编辑器组件的 :key 上 -->
      <slot name="editor" :clean-content="cleanContent" :editor-key="editorKey"></slot>
    </article>

    <input ref="fileInputRef" type="file" accept="image/*" style="display: none" @change="handleFileSelected" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import { useSpiritData } from '@/composables/useSpiritData';
import { useCos } from '@/composables/useCos';

const props = defineProps<{
  title: string;
  noteId?: string;
}>();

const emit = defineEmits(['update:title', 'change', 'refresh']);
const { activeNote } = useSpiritData();
const { uploadFile } = useCos();

const localCoverUrl = ref('');
const localExcerpt = ref('');
const fileInputRef = ref<HTMLInputElement>();
let isInitialized = false;

// 🔥 强制刷新编辑器的 key，每次变化都会导致父组件中绑定了该 key 的编辑器组件重新创建
const editorKey = ref(0);

// 🔥 递归修复节点 type，确保每个节点都有合法的 type 和 content
function fixNode(node: any): any {
  if (!node) return null;
  // 如果节点没有 type，默认设为 'paragraph'（保留其 content）
  if (!node.type) {
    if (Array.isArray(node.content)) {
      return { type: 'paragraph', content: node.content.map(fixNode).filter(Boolean) };
    }
    return null;
  }
  // 递归修复 content 中的子节点
  if (Array.isArray(node.content)) {
    node.content = node.content.map(fixNode).filter(Boolean);
  }
  return node;
}

// 🔥 清理后的文档内容，供 Tiptap 编辑器使用
const cleanContent = computed(() => {
  const note = activeNote.value as any;
  if (!note || !Array.isArray(note.blocks)) {
    return { type: 'doc', content: [] };
  }

  const validBlocks = note.blocks
    .filter((b: any) => b.type && b.type !== 'blog_fixed_cover' && b.type !== 'blog_fixed_excerpt')
    .map((b: any) => {
      try {
        let data = typeof b.data === 'string' ? JSON.parse(b.data) : b.data;
        return fixNode(data);
      } catch {
        return null;
      }
    })
    .filter(Boolean);

  return { type: 'doc', content: validBlocks };
});

// 🔥 刷新编辑器（通过更新 key 并发送事件）
function refreshEditor() {
  editorKey.value += 1;
  emit('refresh');
}

// 存储封面和摘要的系统块
const dispatchSystemBlocks = (coverValue: string, excerptValue: string) => {
  const note = activeNote.value as any;
  if (!note || !Array.isArray(note.blocks)) return;

  const COVER_ID = 'blog_cover_fixed_id';
  const EXCERPT_ID = 'blog_excerpt_fixed_id';

  const coverBlock = { id: COVER_ID, ownerId: props.noteId, ownerType: 'blog', type: 'blog_fixed_cover', sortOrder: 0, data: JSON.stringify({ url: coverValue }) };
  const excerptBlock = { id: EXCERPT_ID, ownerId: props.noteId, ownerType: 'blog', type: 'blog_fixed_excerpt', sortOrder: 1, data: JSON.stringify({ text: excerptValue || '' }) };

  const userContentBlocks = note.blocks.filter((b: any) => b.id !== COVER_ID && b.id !== EXCERPT_ID);
  
  note.blocks = [coverBlock, excerptBlock, ...userContentBlocks];
  note.coverUrl = coverValue;
  note.excerpt = excerptValue;

  emit('change', { blocks: note.blocks });
  refreshEditor(); // 内容变化后刷新编辑器
};

const loadBlogMeta = () => {
  const note = activeNote.value as any;
  if (!note || !Array.isArray(note.blocks)) return;

  const coverBlock = note.blocks.find((b: any) => b.type === 'blog_fixed_cover');
  const excerptBlock = note.blocks.find((b: any) => b.type === 'blog_fixed_excerpt');

  if (coverBlock) try { localCoverUrl.value = JSON.parse(coverBlock.data).url; } catch {}
  if (excerptBlock) try { localExcerpt.value = JSON.parse(excerptBlock.data).text; } catch {}
};

const onExcerptInput = (e: Event) => {
  const val = (e.target as HTMLTextAreaElement).value;
  localExcerpt.value = val;
  dispatchSystemBlocks(localCoverUrl.value, val);
};

const onTitleInput = (e: Event) => emit('update:title', (e.target as HTMLInputElement).value);

const triggerCoverUpload = () => fileInputRef.value?.click();
const handleFileSelected = async (e: Event) => {
  const file = (e.target as HTMLInputElement).files?.[0];
  if (!file || !file.type.startsWith('image/')) return;
  const result = await uploadFile(file, 'blog_cover');
  if (result?.url) {
    localCoverUrl.value = result.url;
    dispatchSystemBlocks(result.url, localExcerpt.value);
  }
};

const removeCover = () => {
  if (confirm('确定移除封面？')) {
    localCoverUrl.value = '';
    dispatchSystemBlocks('', localExcerpt.value);
  }
};

// 🔥 监听 activeNote 变化，加载元数据并刷新编辑器（保证首次加载也能正确渲染）
watch(() => activeNote.value, (newNote) => {
  if (newNote) {
    loadBlogMeta();
    isInitialized = true;
    refreshEditor(); // 强制刷新编辑器以加载最新内容
  }
}, { immediate: true, deep: true });

onMounted(() => {
  if (activeNote.value && !isInitialized) {
    loadBlogMeta();
    refreshEditor();
  }
});

// 暴露刷新方法供父组件调用（备用）
defineExpose({ refreshEditor, editorKey });
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