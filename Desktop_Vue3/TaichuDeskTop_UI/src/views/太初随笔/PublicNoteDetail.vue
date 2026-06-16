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
        <article class="md-article">
          <header class="article-header">
            <h1 v-if="post.type === 'note' || post.title" class="title">
              {{ post.title || '无标题碎片' }}
            </h1>
            
            <div class="metadata">
              <div class="author-block">
                <div class="mini-avatar-placeholder"></div>
                <span class="name">太初隐者</span>
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
import { spiritExtensions } from '../../utils/editorConfig'; // 🌟 引入你的编辑器扩展
import { notePublishApi } from '../../api/NotePublish';
import type { PublishedNoteDetail } from '../../api/NotePublish';

const props = defineProps<{
  id: string | number;
}>();

defineEmits(['close']);

const post = ref<PublishedNoteDetail | null>(null);
const loading = ref(true);

// 🌟 只读 Tiptap 引擎
const editor = useEditor({
  extensions: spiritExtensions,
  content: '',
  editable: false,
});

/**
 * 🌟 重组：完美还原你 lingmai.ts 中的 rebuildTiptapJson 算法
 * 解析后端返回的扁平化 block 的 data 字符串，转回树状富文本文档
 */
const rebuildTiptapContent = (blocks: any[]) => {
  return {
    type: 'doc',
    content: (blocks || []).map(b => {
      try {
        const parsedData = JSON.parse(b.data);
        return {
          type: b.type,
          attrs: { ...parsedData.attrs, id: b.id },
          content: parsedData.content
        };
      } catch (e) {
        console.warn("解析 Block 出错，作为空段落降级处理", b);
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
      
      // 🌟 解析并写入 Tiptap 文档树
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

.md-wrapper { padding: 80px 0 120px; }
.article-header { margin-bottom: 60px; }
.title {
  font-size: 2.5rem; font-weight: 700; color: #1d1d1f;
  letter-spacing: -0.03em; line-height: 1.2; margin: 0 0 24px 0;
}
.metadata { display: flex; align-items: center; gap: 16px; color: #86868b; font-size: 14px; }
.author-block { display: flex; align-items: center; gap: 8px; }
.mini-avatar-placeholder { width: 20px; height: 20px; border-radius: 50%; background: #e5e5ea; }
.divider { color: #d2d2d7; }
.resonance { display: flex; align-items: center; gap: 4px; }

/* 🌟 Tiptap 容器松紧度样式 */
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