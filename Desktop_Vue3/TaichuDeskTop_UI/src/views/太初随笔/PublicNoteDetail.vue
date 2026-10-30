<template>
  <div class="md-detail-mask" role="dialog" aria-modal="true" aria-label="文章详情" @click.self="$emit('close')" @scroll.passive="trackProgress">
    <div v-if="loading" class="md-loading-bar"></div>
    <div v-else class="read-progress" :style="{ transform: `scaleX(${progress})` }" aria-hidden="true"></div>

    <!-- 阅读页自带的实验室背景：定位标记、折线电路与扫描线，不截获点击。 -->
    <svg class="detail-atmosphere" viewBox="0 0 1200 900" preserveAspectRatio="xMidYMid slice" aria-hidden="true" focusable="false">
      <g fill="none" stroke="var(--text-primary)" opacity=".2">
        <path d="M60 60H96M78 42V78M1104 60H1140M1122 42V78M60 840H96M78 822V858M1104 840H1140M1122 822V858" />
        <circle cx="78" cy="60" r="9" /><circle cx="1122" cy="840" r="9" />
      </g>
      <path class="atmo-plane" d="M900 -120H1240L860 260H520Z" fill="var(--accent-color)" opacity=".05" />
      <path d="M0 230H110L190 310V560L110 640H0M1200 120H1060L980 200V420L1060 500H1200" fill="none" stroke="var(--accent-color)" opacity=".13" />
      <path class="atmo-circuit" d="M0 230H110L190 310V560L110 640H0M1200 120H1060L980 200V420L1060 500H1200" fill="none" stroke="var(--signal-mint)" stroke-width="2" opacity=".55" pathLength="100" stroke-dasharray="7 43" />
      <path d="M0 820H420L520 720H1200" fill="none" stroke="var(--accent-color)" opacity=".12" />
      <path class="atmo-trace" d="M0 820H420L520 720H1200" fill="none" stroke="var(--signal-mint)" opacity=".5" pathLength="100" stroke-dasharray="5 45" />
      <g class="atmo-orbit" fill="none" stroke="var(--signal-mint)"><circle cx="1120" cy="700" r="240" opacity=".18" stroke-dasharray="300 90 60 40" /><circle cx="1120" cy="460" r="4" fill="var(--signal-mint)" opacity=".5" /></g>
      <rect class="atmo-scan" x="0" y="0" width="1200" height="2" fill="var(--signal-mint)" opacity=".18" />
    </svg>

    <div class="md-viewport animate-fade-up content-page community-surface tone-stream detail-surface">
      <nav class="md-controls content-actions">
        <button class="back-btn" @click="$emit('close')">
          <span class="arrow">←</span> 返回
        </button>
        <div class="doc-type">
          <span>{{ FORUM_NAME }}</span>
          <template v-if="place.board"><i aria-hidden="true">/</i><span>{{ place.board.tag }}</span></template>
          <template v-if="place.sub"><i aria-hidden="true">/</i><span>{{ place.sub.tag }}</span></template>
          <i aria-hidden="true">/</i><b>{{ post?.type === 'post' ? '帖子' : '博客' }}</b>
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
            <SectionTrace />
            <p class="record-plate"><span>RECORD</span>{{ post.type === 'post' ? 'POST' : 'BLOG' }} · No.{{ recordNo }}</p>
            <h1 v-if="post.type === 'note' || post.title" class="title">
              {{ post.title || '无标题碎片' }}
            </h1>
            
            <div class="metadata">
              <UserLink class="author-block" :id="post.authorId" :name="post.authorName">
                <div class="mini-avatar-placeholder"></div>
                <span class="name">{{ post.authorName || '太初隐者' }}</span>
              </UserLink>
              <UserTitle v-if="post.authorId" :user-id="post.authorId" />
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
            <div class="footer-divider" aria-hidden="true"><i></i></div>
            <p class="end-mark">— 阅读完毕 · END OF RECORD —</p>
            <p class="footer-sign">喜欢这篇内容？可以复制引用链接分享给社区。</p>
            <p v-if="!post.canEdit" class="footer-report"><ReportButton :target-type="post.type === 'blog' ? 'blog' : 'post'" :target-id="post.id" :target-title="post.title || '短帖'" text="这篇内容有问题？举报" /></p>
          </footer>
        </article>

        <NoteComments v-if="post" :key="post.id" :note-id="String(post.id)" />
      </div>

      <div class="md-wrapper empty-state" v-else-if="!loading">
<p role="alert">{{ error || '内容不存在或已下架' }}</p><button @click="fetchDetail">重新加载</button>
      </div>
    </div>

  </div>
</template>

<script setup lang="ts">
import UserLink from '@/components/UserLink.vue'
import ReportButton from '@/components/ReportButton.vue'
import UserTitle from '@/components/UserTitle.vue'
import SectionTrace from '@/components/SectionTrace.vue'
import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
import { useEditor, EditorContent } from '@tiptap/vue-3';
import { spiritExtensions } from '../../utils/editorConfig'; // 🌟 引入编辑器扩展
import { notePublishApi } from '../../api/NotePublish';
import type { PublishedNoteDetail } from '../../api/NotePublish';
import NoteComments from './NoteComments.vue';
import { upgradeLegacyImages } from '@/utils/legacyContent';
import { boardOf, FORUM_NAME } from '@/utils/forumBoards';

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
const place = computed(() => boardOf(post.value?.tags));
const recordNo = computed(() => String(post.value?.id || '').replace(/-/g, '').slice(0, 6).toUpperCase());

// 顶部阅读进度线。
const progress = ref(0);
function trackProgress(event: Event) {
  const el = event.currentTarget as HTMLElement;
  const max = el.scrollHeight - el.clientHeight;
  progress.value = max > 0 ? Math.min(1, el.scrollTop / max) : 0;
}

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
      // 旧版 Markdown 图片（![描述](地址)）还原为图片显示。
      const tiptapJson = upgradeLegacyImages(rebuildTiptapContent(res.blocks));
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
  background-color: var(--paper-bg); overflow-y: auto;
  background-image: linear-gradient(var(--grid-line) 1px, transparent 1px), linear-gradient(90deg, var(--grid-line) 1px, transparent 1px);
  background-size: 48px 48px; background-attachment: local;
  cursor: zoom-out;
}
.detail-atmosphere { position: fixed; inset: 0; width: 100%; height: 100%; z-index: 0; pointer-events: none; }
.atmo-orbit { transform-origin: 1120px 700px; }
.read-progress { position: fixed; top: 0; left: 0; right: 0; height: 3px; z-index: 2100; background: linear-gradient(90deg, var(--accent-color), var(--signal-mint)); transform-origin: left; transform: scaleX(0); pointer-events: none; }


.md-viewport {
  width: 100%; max-width: 940px; margin: 0 auto;
  padding: 0 24px 40px; box-sizing: border-box; position: relative; z-index: 1; cursor: default;
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
.doc-type { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; font: 11px/1.4 var(--font-mono); color: var(--text-secondary); letter-spacing: .08em; }
.doc-type i { font-style: normal; opacity: .5; }
.doc-type b { padding: 2px 7px; background: var(--inverse-bg); color: var(--inverse-text); border-left: 2px solid var(--signal-mint); font-weight: 600; }
.record-plate { display: inline-flex; align-items: center; gap: 8px; margin: 0 0 14px; font: 10px/1.5 var(--font-mono); letter-spacing: .12em; color: var(--text-secondary); }
.record-plate span { padding: 2px 7px; background: var(--inverse-bg); color: var(--inverse-text); border-left: 2px solid var(--signal-mint); }

/* 🌟 新增：详情页大面积铺开的长文博客视觉封面样式 */
.blog-detail-cover-wrapper {
  width: 100%;
  aspect-ratio: 21 / 9;
  border-radius: 0;
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

.md-wrapper { padding: 32px 40px; margin-top: 24px; background-color: var(--paper-card); border-top: 2px solid var(--accent-color); }
.article-header { margin-bottom: 28px; margin-top: 24px; }
.title {
  font-size: 2.5rem; font-weight: 700; color: var(--text-primary);
  letter-spacing: -0.03em; line-height: 1.2; margin: 0 0 24px 0;
  padding-left: 16px; border-left: 4px solid var(--accent-color);
}
.metadata { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; color: var(--text-secondary); font-size: 13px; font-family: var(--font-mono); }
.author-block { display: flex; align-items: center; gap: 8px; }
.mini-avatar-placeholder { width: 20px; height: 20px; border-radius: 0; background: linear-gradient(135deg, var(--accent-color) 50%, var(--signal-mint) 50%); clip-path: polygon(0 0, 100% 0, 100% 70%, 70% 100%, 0 100%); }
.divider { color: var(--text-secondary); }
.author-link { color: var(--text-primary); text-decoration: none; border-bottom: 1px solid var(--signal-mint); }
.author-link:hover { color: var(--accent-color); }
.resonance { display: flex; align-items: center; gap: 4px; }

/* Tiptap 容器松紧度样式 */
.article-body { margin-bottom: 60px; }
:deep(.spirit-typography-engine .tiptap) {
  outline: none; font-size: 1.05rem; line-height: 1.9;
  color: var(--text-primary); text-align: justify;
}
:deep(.spirit-link-node) {
  color: var(--accent-color); background: var(--paper-sub);
  padding: 0 4px; border-radius: 0; font-weight: 500;
}
:deep(.spirit-image-node) {
  display: block; height: auto; border-radius: 0; margin: 32px 0;
}
/* 正文内图片：细框加信号色底边，像实验记录里的附图。 */
:deep(.spirit-typography-engine img) { max-width: 100%; height: auto; border: 1px solid var(--border-line); border-bottom: 3px solid var(--signal-mint); padding: 6px; background: var(--paper-sub); box-sizing: border-box; }
:deep(.spirit-typography-engine :is(h1, h2, h3)) { position: relative; padding-left: 14px; }
:deep(.spirit-typography-engine :is(h1, h2, h3))::before { content: ''; position: absolute; left: 0; top: .35em; width: 6px; height: 6px; background: var(--signal-mint); }
:deep(.spirit-typography-engine blockquote) { margin: 24px 0; padding: 12px 18px; border-left: 3px solid var(--signal-mint); background: linear-gradient(90deg, var(--accent-soft), transparent); }
:deep(.spirit-typography-engine hr) { border: 0; height: 1px; background: repeating-linear-gradient(90deg, var(--border-line) 0 6px, transparent 6px 12px); margin: 32px 0; }

.article-footer { margin-top: 0; padding-top: 24px; text-align: center; }
.footer-divider { position: relative; height: 1px; background: var(--border-line); margin-bottom: 40px; overflow: hidden; }
.footer-divider i { position: absolute; inset: 0 auto 0 0; width: 22%; background: var(--signal-mint); }
.end-mark { font: 10px var(--font-mono); letter-spacing: 0.3em; color: var(--text-secondary); margin-top: 20px; }
.footer-sign { font-size: 12px; color: var(--text-secondary); margin-top: 8px; }

.md-loading-bar {
  position: fixed; top: 0; left: 0; height: 2px;
  background: var(--accent-color); width: 100%; z-index: 2100;
  animation: loading-flow 2s infinite linear;
}
@keyframes loading-flow {
  0% { transform: translateX(-100%); }
  100% { transform: translateX(100%); }
}

.empty-state { text-align: center; color: var(--text-secondary); font-size: 14px; }

.animate-fade-up { animation: fadeUp 0.4s cubic-bezier(0.16, 1, 0.3, 1); }
@media (prefers-reduced-motion: no-preference) {
  .atmo-circuit { animation: atmo-trace 6s linear infinite; }
  .atmo-trace { animation: atmo-trace 16s linear infinite; }
  .atmo-orbit { animation: atmo-orbit 60s linear infinite; }
  .atmo-plane { animation: atmo-plane 16s ease-in-out infinite alternate; }
  .atmo-scan { animation: atmo-scan 9s linear infinite; }
  .footer-divider i { animation: divider-run 3.2s cubic-bezier(.6,0,.4,1) infinite alternate; }
  .title { animation: title-in .6s cubic-bezier(.2,.7,.2,1) both; }
}
@keyframes atmo-trace { to { stroke-dashoffset: -100; } }
@keyframes atmo-orbit { to { transform: rotate(360deg); } }
@keyframes atmo-plane { to { transform: translate(-24px, 16px); } }
@keyframes atmo-scan { from { transform: translateY(-10px); } to { transform: translateY(910px); } }
@keyframes divider-run { to { transform: translateX(355%); } }
@keyframes title-in { from { opacity: 0; clip-path: inset(0 100% 0 0); } to { opacity: 1; clip-path: inset(0 0 0 0); } }
@keyframes fadeUp {
  from { opacity: 0; transform: translateY(16px); }
  to { opacity: 1; transform: translateY(0); }
}
@media(max-width: 720px) { .md-viewport { padding: 0 16px 60px; } .md-wrapper { padding: 20px; } .metadata { font-size: .8rem; gap: 8px; } }
@media(max-width: 720px) { .detail-atmosphere { opacity: .5; } .title { font-size: 1.8rem; padding-left: 12px; } }
@media(prefers-reduced-motion: reduce) { .animate-fade-up, .md-loading-bar { animation: none; } }
.footer-report { margin: 8px 0 0; text-align: center; }
</style>
