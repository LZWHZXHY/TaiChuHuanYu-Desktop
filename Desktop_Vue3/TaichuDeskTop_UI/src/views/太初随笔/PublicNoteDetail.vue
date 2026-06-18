<template>
  <div class="md-detail-mask" @click.self="$emit('close')">
    <div v-if="loading" class="md-loading-bar"></div>

    <div class="md-viewport animate-fade-up">
      <nav class="md-controls">
        <button class="back-btn" @click="$emit('close')">
          <span class="arrow">←</span> INDEX
        </button>
        <div class="doc-type">
          {{ post?.type === 'post' ? 'FRAGMENT / THOUGHT' : 'ESSAY / NOTE' }}
        </div>
      </nav>

      <div class="md-wrapper" v-if="post">
        <div v-if="post.type === 'blog' && extractedCoverUrl" class="blog-detail-cover-wrapper">
          <img :src="extractedCoverUrl" class="blog-detail-cover-img" alt="文章封面" />
        </div>

        <article class="md-article">
          <header class="article-header">
            <h1 v-if="post.type === 'note' || post.title" class="title">
              {{ post.title || '无标题碎片' }}
            </h1>
            
            <div class="metadata">
              <div class="author-block">
                <div class="mini-avatar-placeholder"></div>
                <span class="name">{{ post.authorName || '太初隐者' }}</span>
              </div>
              <span class="divider">/</span>
              <time class="date">{{ formatTime(post.publishedAt) }}</time>
              <span class="divider">/</span>
              <span class="resonance">
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"/>
                </svg>
                {{ post.resonance || 0 }} 灵动
              </span>
            </div>
          </header>

          <section class="article-body">
            <editor-content :editor="editor" class="spirit-typography-engine readonly" />
          </section>

          <footer class="article-footer">
            <div class="footer-divider"></div>
            <p class="end-mark">EOF / THE END</p>
            <p class="footer-sign">落笔于太初之境 · 视界广场</p>
          </footer>
        </article>
      </div>

      <div class="md-wrapper empty-state" v-else-if="!loading">
        <p>此思维碎片似乎已沉入虚无...</p>
      </div>
    </div>

    <button class="floating-close" @click="$emit('close')">✕</button>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import { useEditor, EditorContent } from '@tiptap/vue-3';
import { spiritExtensions } from '../../utils/editorConfig'; // 🌟 引入编辑器扩展
import { notePublishApi } from '../../api/NotePublish';
import type { PublishedNoteDetail } from '../../api/NotePublish';

const props = defineProps<{
  id: string | number;
}>();

defineEmits(['close']);

const post = ref<PublishedNoteDetail | null>(null);
const loading = ref(true);
const extractedCoverUrl = ref(''); // 🌟 用于捕获博客封面链接

// 🌟 只读 Tiptap 引擎
const editor = useEditor({
  extensions: spiritExtensions,
  content: '',
  editable: false,
});

/**
 * 🌟 核心升级：面向组件自治大协议的“业务/内容清洗拆分器”
 * 过滤剔除前端各组件扔出来的非富文本自定义系统区块，防止 Tiptap 发生解析白屏崩溃
 */
const rebuildTiptapContent = (blocks: any[]) => {
  const safeBlocks = blocks || [];

  // 1. 抓取并提取出博客专用的固定封面，交给原生 HTML 区域去大面积精美铺开渲染
  const blogCoverBlock = safeBlocks.find(b => b.type === 'blog_fixed_cover');
  if (blogCoverBlock?.data) {
    try {
      extractedCoverUrl.value = JSON.parse(blogCoverBlock.data).url || '';
    } catch (e) {}
  }

  // 2. 🌟 严格清洗：过滤剔除掉所有非富文本编辑器的纯业务属性积木块，只保留纯文本正文块
  const pureTextContentBlocks = safeBlocks.filter(b => 
    b.type !== 'blog_fixed_cover' && 
    b.type !== 'blog_fixed_excerpt' && 
    b.type !== 'char-layout-block' &&
    b.type !== 'canvas-node' &&
    b.type !== 'canvas-edge' &&
    b.type !== 'map-layout-block'
  );

  // 3. 将剩下干净的正文文本重新组装返回给只读编辑器
  return {
    type: 'doc',
    content: pureTextContentBlocks.map(b => {
      try {
        const parsedData = typeof b.data === 'string' ? JSON.parse(b.data) : b.data;
        return {
          type: b.type,
          attrs: { ...parsedData?.attrs, id: b.id },
          content: parsedData?.content
        };
      } catch (e) {
        console.warn("解析内容 Block 出错", b);
        return { type: 'paragraph', content: [] };
      }
    })
  };
};

const fetchDetail = async () => {
  if (!props.id) {
    loading.value = false;
    return;
  }
  loading.value = true;
  try {
    const res = await notePublishApi.getPublicBlog(String(props.id));
    if (res && editor.value) {
      post.value = res;
      
      // 🌟 调用升级后的清洗器，还原 Tiptap 文档树
      const tiptapJson = rebuildTiptapContent(res.blocks);
      editor.value.commands.setContent(tiptapJson, { emitUpdate: false });
    }
  } catch (err) {
    console.error('获取灵脉详情失败:', err);
  } finally {
    loading.value = false;
  }
};

const formatTime = (timeStr: string | undefined) => {
  if (!timeStr) return '刚刚';
  const date = new Date(timeStr);
  return `${date.getFullYear()}年${date.getMonth() + 1}月${date.getDate()}日`;
};

onMounted(async () => {
  // 阻止底层列表背景滚动
  document.body.style.overflow = 'hidden';
  await fetchDetail();
});

onUnmounted(() => {
  // 组件卸载时还原滚动条
  document.body.style.overflow = '';
  if (editor.value) {
    editor.value.destroy();
  }
});
</script>

<style scoped>
.md-detail-mask {
  position: fixed; inset: 0; z-index: 2000;
  background: #ffffff; overflow-y: auto; scrollbar-width: none;
  cursor: zoom-out;
}
.md-detail-mask::-webkit-scrollbar { display: none; }

.md-viewport {
  width: 100%; max-width: 800px; margin: 0 auto;
  padding: 0 24px; position: relative; cursor: default;
}

.md-controls {
  display: flex; justify-content: space-between; align-items: center;
  height: 80px; border-bottom: 1px solid #f2f2f2;
}
.back-btn {
  background: none; border: none; font-size: 11px; font-weight: 700;
  letter-spacing: 0.15em; color: #1d1d1f; cursor: pointer;
  display: flex; align-items: center; gap: 8px; transition: opacity 0.3s;
}
.back-btn:hover { opacity: 0.6; }
.doc-type { font-size: 10px; font-weight: 700; color: #86868b; letter-spacing: 0.2em; }

/* 🌟 新增：详情页大面积铺开的长文博客视觉封面样式 */
.blog-detail-cover-wrapper {
  width: 100%;
  aspect-ratio: 21 / 9;
  border-radius: 24px;
  overflow: hidden;
  margin-top: 40px;
  border: 1px solid rgba(0,0,0,0.03);
  box-shadow: 0 10px 30px rgba(0,0,0,0.02);
}
.blog-detail-cover-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.md-wrapper { padding: 40px 0 120px; }
.article-header { margin-bottom: 60px; margin-top: 20px; }
.title {
  font-size: 2.5rem; font-weight: 700; color: #1d1d1f;
  letter-spacing: -0.03em; line-height: 1.2; margin: 0 0 24px 0;
}
.metadata { display: flex; align-items: center; gap: 16px; color: #86868b; font-size: 14px; }
.author-block { display: flex; align-items: center; gap: 8px; }
.mini-avatar-placeholder { width: 20px; height: 20px; border-radius: 50%; background: #e5e5ea; }
.divider { color: #d2d2d7; }
.resonance { display: flex; align-items: center; gap: 4px; }

/* Tiptap 容器松紧度样式 */
.article-body { margin-bottom: 60px; }
:deep(.spirit-typography-engine .tiptap) {
  outline: none; font-size: 1.25rem; line-height: 2.2;
  color: #333333; text-align: justify;
}
:deep(.spirit-link-node) {
  color: #0066cc; background: rgba(0, 102, 204, 0.05);
  padding: 0 4px; border-radius: 4px; font-weight: 500;
}
:deep(.spirit-image-node) {
  display: block; height: auto; border-radius: 12px; margin: 32px 0;
}

.article-footer { margin-top: 100px; padding-top: 60px; text-align: center; }
.footer-divider { height: 1px; background: #f2f2f2; margin-bottom: 40px; }
.end-mark { font-size: 10px; letter-spacing: 0.4em; color: #d2d2d7; margin-top: 20px; }
.footer-sign { font-size: 12px; color: #86868b; margin-top: 8px; }

.md-loading-bar {
  position: fixed; top: 0; left: 0; height: 2px;
  background: #000; width: 100%; z-index: 2100;
  animation: loading-flow 2s infinite linear;
}
@keyframes loading-flow {
  0% { transform: translateX(-100%); }
  100% { transform: translateX(100%); }
}

.floating-close {
  position: fixed; top: 32px; right: 32px;
  width: 40px; height: 40px; border: none; background: none;
  font-size: 20px; cursor: pointer; opacity: 0.2; transition: opacity 0.3s;
}
.floating-close:hover { opacity: 1; }

.empty-state { text-align: center; color: #86868b; font-size: 14px; }

.animate-fade-up { animation: fadeUp 0.4s cubic-bezier(0.16, 1, 0.3, 1); }
@keyframes fadeUp {
  from { opacity: 0; transform: translateY(16px); }
  to { opacity: 1; transform: translateY(0); }
}
</style>