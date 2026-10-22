<template>
  <div class="md-detail-mask" role="dialog" aria-modal="true" aria-label="文章详情" @click.self="$emit('close')">
    <div v-if="loading" class="md-loading-bar"></div>

    <div class="md-viewport animate-fade-up content-page">
      <nav class="md-controls content-actions">
        <button class="back-btn" @click="$emit('close')">
          <span class="arrow">←</span> 返回
        </button>
        <div class="doc-type">
          {{ post?.type === 'post' ? '帖子' : '博客与文章' }}
        </div>
        <RouterLink v-if="post?.canEdit && post.originalNoteId && (post.type === 'blog' || post.type === 'post')" :to="{ name: 'Compose', params: { type: post.type }, query: { draft: post.originalNoteId } }">编辑内容</RouterLink>
        <button class="back-btn" @click="copyReference">复制引用链接</button>
      </nav><p v-if="referenceMessage" role="status">{{ referenceMessage }}</p>

      <div class="md-wrapper content-panel" v-if="post">
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
            <p class="end-mark">阅读完毕</p>
            <p class="footer-sign">喜欢这篇内容？可以复制引用链接分享给社区。</p>
          </footer>
        </article>
      </div>

      <div class="md-wrapper empty-state" v-else-if="!loading">
<p role="alert">{{ error || '内容不存在或已下架' }}</p><button @click="fetchDetail">重新加载</button>
      </div>
    </div>

  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue';
import { useEditor, EditorContent } from '@tiptap/vue-3';
import { spiritExtensions } from '../../utils/editorConfig'; // 🌟 引入编辑器扩展
import { notePublishApi } from '../../api/NotePublish';
import type { PublishedNoteDetail } from '../../api/NotePublish';

const props = defineProps<{
  id: string | number;
}>();

const emit = defineEmits(['close']);
const closeOnEscape = (event: KeyboardEvent) => { if (event.key === 'Escape') emit('close'); };

let generation = 0;
const error = ref(''), referenceMessage = ref('');
async function copyReference() { try { const url = new URL(window.location.href); url.hash = '/knowledge?detailType=' + (post.value?.type === 'post' ? 'post' : 'blog') + '&detail=' + encodeURIComponent(String(props.id)); await navigator.clipboard.writeText(url.href); referenceMessage.value = '引用链接已复制'; } catch { referenceMessage.value = '复制失败，请复制浏览器地址栏中的链接'; } }
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
  const current = ++generation; loading.value = true; error.value = ''; post.value = null; extractedCoverUrl.value = '';
  try {
    const res = await notePublishApi.getPublicBlog(String(props.id));
    if (current === generation && res && editor.value && !editor.value.isDestroyed) {
      post.value = res;
      
      // 🌟 调用升级后的清洗器，还原 Tiptap 文档树
      const tiptapJson = rebuildTiptapContent(res.blocks);
      editor.value.commands.setContent(tiptapJson, { emitUpdate: false });
    }
  } catch (err) {
    if (current === generation) error.value = '内容读取失败，请重试。';
  } finally {
    if (current === generation) loading.value = false;
  }
};

const formatTime = (timeStr: string | undefined) => {
  if (!timeStr) return '刚刚';
  const date = new Date(timeStr);
  return `${date.getFullYear()}年${date.getMonth() + 1}月${date.getDate()}日`;
};

onMounted(async () => {
  window.addEventListener('keydown', closeOnEscape);
  // 阻止底层列表背景滚动
  document.body.style.overflow = 'hidden';
  await fetchDetail();
});

watch(() => props.id, fetchDetail);
onUnmounted(() => {
  window.removeEventListener('keydown', closeOnEscape);
  generation++;
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
  background: var(--paper-bg); overflow-y: auto;
  cursor: zoom-out;
}


.md-viewport {
  width: 100%; max-width: 940px; margin: 0 auto;
  padding: 0 24px 40px; box-sizing: border-box; position: relative; cursor: default;
}

.md-controls {
  position: sticky; top: 0; z-index: 10; background: var(--paper-bg);
  display: flex; flex-wrap: wrap; gap: 12px; align-items: center;
  min-height: 72px; padding: 16px 0; border-bottom: 1px solid var(--border-line);
}
.back-btn {
  background: none; border: none; font-size: 11px; font-weight: 700;
  letter-spacing: 0.15em; color: var(--text-primary); cursor: pointer;
  display: flex; align-items: center; gap: 8px; transition: opacity 0.3s;
}
.back-btn:hover { opacity: 0.6; }
.doc-type { font-size: 10px; font-weight: 700; color: var(--text-secondary); letter-spacing: 0.2em; }

/* 🌟 新增：详情页大面积铺开的长文博客视觉封面样式 */
.blog-detail-cover-wrapper {
  width: 100%;
  aspect-ratio: 21 / 9;
  border-radius: 12px;
  overflow: hidden;
  margin-top: 0;
  border: 1px solid rgba(0,0,0,0.03);
  box-shadow: 0 10px 30px rgba(0,0,0,0.02);
}
.blog-detail-cover-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.md-wrapper { padding: 32px 40px; margin-top: 24px; }
.article-header { margin-bottom: 28px; margin-top: 24px; }
.title {
  font-size: 2.5rem; font-weight: 700; color: var(--text-primary);
  letter-spacing: -0.03em; line-height: 1.2; margin: 0 0 24px 0;
}
.metadata { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; color: var(--text-secondary); font-size: 14px; }
.author-block { display: flex; align-items: center; gap: 8px; }
.mini-avatar-placeholder { width: 20px; height: 20px; border-radius: 50%; background: #e5e5ea; }
.divider { color: #d2d2d7; }
.resonance { display: flex; align-items: center; gap: 4px; }

/* Tiptap 容器松紧度样式 */
.article-body { margin-bottom: 60px; }
:deep(.spirit-typography-engine .tiptap) {
  outline: none; font-size: 1.05rem; line-height: 1.9;
  color: var(--text-primary); text-align: justify;
}
:deep(.spirit-link-node) {
  color: var(--accent-color); background: var(--paper-sub);
  padding: 0 4px; border-radius: 4px; font-weight: 500;
}
:deep(.spirit-image-node) {
  display: block; height: auto; border-radius: 12px; margin: 32px 0;
}

.article-footer { margin-top: 0; padding-top: 24px; text-align: center; }
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

.empty-state { text-align: center; color: #86868b; font-size: 14px; }

.animate-fade-up { animation: fadeUp 0.4s cubic-bezier(0.16, 1, 0.3, 1); }
@keyframes fadeUp {
  from { opacity: 0; transform: translateY(16px); }
  to { opacity: 1; transform: translateY(0); }
}
@media(max-width: 720px) { .md-viewport { padding: 0 16px 60px; } .md-wrapper { padding: 20px; } .metadata { font-size: .8rem; gap: 8px; } }
</style>
