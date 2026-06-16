<template>
  <article class="curated-gallery-container animate-fade-in">
    <header class="curated-nav">
      <div class="nav-left">
        <div class="brand-group">
          <h1 class="brand-title" @click="changeFilter('all')">太初视界</h1>
          <span class="brand-sub">TAICHU CURATED STREAM</span>
        </div>
        <div class="mode-selectors">
          <button 
            @click="changeFilter('all')" 
            :class="['mode-item', { active: currentFilter === 'all' }]"
          >
            全部
          </button>
          <button 
            @click="changeFilter('note')" 
            :class="['mode-item', { active: currentFilter === 'note' }]"
          >
            长文随笔
          </button>
          <button 
            @click="changeFilter('post')" 
            :class="['mode-item', { active: currentFilter === 'post' }]"
          >
            短篇简语
          </button>
        </div>
      </div>
      <div class="nav-right">
        <button class="lingmai-link" @click="goToLingMai">
          进入灵脉空间 <span class="arrow">→</span>
        </button>
      </div>
    </header>

    <main class="curated-view">
      <div v-if="loading && posts.length === 0" class="curated-loading">
        <div class="loading-pulse"></div>
        <span class="loading-text">正在共鸣太初视界...</span>
      </div>

      <div v-else-if="posts.length === 0" class="curated-empty">
        <p class="empty-text">视界中空无一物，待你落笔生花。</p>
      </div>

      <div v-else class="curated-stream-grid">
        <div 
          v-for="item in posts" :key="item.id" 
          class="stream-card" 
          :class="[item.type === 'note' ? 'is-blog' : 'is-post', { 'has-image-hero': item.cardCover }]"
          @click="openArtwork(item.id)"
        >
          <template v-if="item.type === 'note'">
            <div class="card-meta">
              <span class="meta-tag">ESSAY</span>
              <span class="meta-dot">/</span>
              <span class="meta-author">{{ item.author }}</span>
            </div>
            <div class="card-body">
              <h2 class="blog-title">{{ item.title || '无标题随笔' }}</h2>
              <p class="blog-excerpt">
                {{ getSnippet(item.excerpt || item.content, 140) || '暂无摘要' }}
              </p>
            </div>
            <div class="card-footer">
              <time class="post-time">{{ formatTime(item.publishedAt || item.createdAt) }}</time>
              <div class="resonance-stat">
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"/></svg>
                <span>{{ item.resonance || 0 }}</span>
              </div>
            </div>
          </template>

          <template v-else-if="item.type === 'post'">
            <div v-if="item.cardCover" class="post-card-hero">
              <img :src="item.cardCover" alt="动态配图" class="hero-img" loading="lazy" />
            </div>

            <div class="post-card-content-wrapper">
              <div class="card-meta">
                <span class="meta-tag post-tag">FRAGMENT</span>
                <span class="meta-dot">/</span>
                <span class="meta-author">{{ item.author }}</span>
              </div>
              <div class="card-body">
                <p class="post-text">
                  “ {{ getSnippet(item.excerpt || item.content, 160) || item.title || '灵脉波动中...' }} ”
                </p>
              </div>
              <div class="card-footer">
                <time class="post-time">{{ formatTime(item.publishedAt || item.createdAt) }}</time>
                <div class="resonance-stat">
                  <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"/></svg>
                  <span>{{ item.resonance || 0 }}</span>
                </div>
              </div>
            </div>
          </template>
        </div>
      </div>

      <div v-if="posts.length > 0" ref="loadMoreTrigger" class="load-more-trigger">
        <div v-if="loadingMore" class="loading-pulse-small"></div>
        <span v-else-if="!hasMore" class="end-text">—— 视界已达边界 ——</span>
      </div>
    </main>

    <transition name="fade">
      <PublicNoteDetail
        v-if="activePostId"
        :id="activePostId"
        @close="closeArtwork"
      />
    </transition>
  </article>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import { useRouter } from 'vue-router';

import PublicNoteDetail from './PublicNoteDetail.vue';
import type { PublishedNoteItem } from '../../api/NotePublish';
import { notePublishApi } from '../../api/NotePublish';

interface FrontMixedPost extends PublishedNoteItem {
  content?: string;
  author: string;
  cardCover?: string; // ✨ 挂载提取出来的首图 URL
}

const router = useRouter();
const currentFilter = ref<'all' | 'note' | 'post'>('all');
const posts = ref<FrontMixedPost[]>([]);
const loading = ref(false);
const activePostId = ref<string | null>(null);

const page = ref(1);
const pageSize = ref(20);
const hasMore = ref(true);
const loadingMore = ref(false);
const loadMoreTrigger = ref<HTMLElement | null>(null);
let observer: IntersectionObserver | null = null;

const fetchStream = async (isLoadMore = false) => {
  if (isLoadMore) {
    loadingMore.value = true;
  } else {
    loading.value = true;
    page.value = 1; 
    hasMore.value = true;
  }

  try {
    const typeQuery = currentFilter.value === 'all' ? undefined : currentFilter.value;

    const res = await notePublishApi.getPublicStream({
      type: typeQuery,
      page: page.value,
      pageSize: pageSize.value
    });
    
    if (res && Array.isArray(res)) {
      const formattedData = res.map((item: any) => {
  let extractedCover = '';
  
  // 1. 优先从后端的 ExtraData 元数据包中反序列化提取
  if (item.extraData && item.extraData.startsWith('{')) {
    try {
      const meta = JSON.parse(item.extraData);
      extractedCover = meta.cardCover || '';
    } catch(e) {}
  }

  // 2. ✨【前端高容错兜底】：如果后端没有注入 cardCover，但发现 excerpt 本身包含图片特征，强行穿透提取！
  if (!extractedCover && item.excerpt && item.excerpt.includes('"type":"image"')) {
    try {
      const scanTiptapImage = (node: any): string => {
        if (!node) return '';
        if (node.type === 'image' && node.attrs?.src) return node.attrs.src;
        if (Array.isArray(node.content)) {
          for (const child of node.content) {
            const src = scanTiptapImage(child);
            if (src) return src;
          }
        }
        return '';
      };
      const parsedJson = JSON.parse(item.excerpt);
      extractedCover = scanTiptapImage(parsedJson);
    } catch(e) {}
  }

  return {
    ...item,
    author: item.authorName || '太初隐者', 
    content: item.excerpt,
    cardCover: extractedCover // 绑定给卡片大图渲染层
  }
});

      if (isLoadMore) {
        posts.value.push(...formattedData); 
      } else {
        posts.value = formattedData; 
      }

      if (res.length < pageSize.value) {
        hasMore.value = false;
      } else {
        page.value++; 
      }
    } else {
      hasMore.value = false;
    }
  } catch (err) {
    console.error('广场数据感应失败:', err);
    hasMore.value = false;
  } finally { 
    loading.value = false;
    loadingMore.value = false;
  }
};

const setupObserver = () => {
  if (observer) observer.disconnect();

  observer = new IntersectionObserver((entries) => {
    if (entries[0].isIntersecting && !loading.value && !loadingMore.value && hasMore.value) {
      fetchStream(true);
    }
  }, {
    rootMargin: '200px', 
  });

  if (loadMoreTrigger.value) {
    observer.observe(loadMoreTrigger.value);
  }
};

const extractTiptapText = (rawStr: string | undefined): string => {
  if (!rawStr) return '';
  const trimmed = rawStr.trim();
  
  if (!trimmed.startsWith('{') && !trimmed.startsWith('[')) {
    return rawStr;
  }

  try {
    const obj = JSON.parse(rawStr);
    const parseNode = (node: any): string => {
      if (!node) return '';
      if (typeof node === 'string') return node;
      if (node.type === 'text') return node.text || '';
      if (Array.isArray(node.content)) {
        return node.content.map(parseNode).join('');
      }
      if (node.type === 'doc' && Array.isArray(node.content)) {
        return node.content.map(parseNode).join('');
      }
      return '';
    };
    return parseNode(obj);
  } catch (e) {
    return rawStr;
  }
};

const getSnippet = (rawStr: string | undefined, maxLength: number): string => {
  const plainText = extractTiptapText(rawStr);
  if (!plainText) return '';
  if (plainText.length <= maxLength) return plainText;
  return plainText.substring(0, maxLength) + '...';
};

const openArtwork = (id: string) => {
  activePostId.value = id;
  window.history.pushState(null, '', `/posts/${id}`);
};

const closeArtwork = () => {
  activePostId.value = null;
  window.history.pushState(null, '', '/posts');
};

const goToLingMai = () => {
  router.push('/lingmai');
};

const changeFilter = (type: 'all' | 'note' | 'post') => {
  if (currentFilter.value === type) return; 
  currentFilter.value = type;
  window.scrollTo({ top: 0, behavior: 'smooth' }); 
  fetchStream(false); 
};

const formatTime = (timeStr: string | undefined) => {
  if (!timeStr) return '刚刚';
  const date = new Date(timeStr);
  return `${date.getMonth() + 1}月${date.getDate()}日`;
};

onMounted(async () => {
  await fetchStream(false);
  setupObserver(); 

  const path = window.location.pathname;
  if (path.startsWith('/posts/')) {
    const directId = path.split('/').pop();
    if (directId && directId !== 'posts') {
      activePostId.value = directId;
    }
  }
});

onUnmounted(() => {
  if (observer) observer.disconnect();
});
</script>

<style scoped>
.curated-gallery-container {
  --font-serif: "Georgia", "Nimbus Roman No9 L", "Songti SC", "Noto Serif CJK SC", serif;
  --font-sans: -apple-system, BlinkMacSystemFont, "SF Pro Text", "Helvetica Neue", Arial, sans-serif;
  --color-ink: #111111;
  --color-slate: #6e6e73;
  --color-bg-ivory: #ffffff;
  --color-card-bg: #fafafa;
  --color-line: #ededf0;

  width: 100%;
  min-height: 100vh;
  background: var(--color-bg-ivory);
  color: var(--color-ink);
  font-family: var(--font-sans);
  -webkit-font-smoothing: antialiased;
}

.curated-nav {
  display: flex;
  justify-content: space-between;
  align-items: center;
  height: 104px;
  padding: 0 5%;
  border-bottom: 1px solid var(--color-line);
  position: sticky; 
  top: 0; 
  z-index: 100;
  background: rgba(255, 255, 255, 0.85);
  backdrop-filter: blur(20px) saturate(180%);
  transition: height 0.3s ease;
}

.nav-left { display: flex; align-items: center; gap: 64px; }
.brand-group { display: flex; flex-direction: column; flex-shrink: 0; }
.brand-title { font-size: 1.5rem; font-weight: 800; letter-spacing: -0.05em; margin: 0; cursor: pointer; }
.brand-sub { font-size: 10px; font-weight: 700; letter-spacing: 0.2em; color: var(--color-slate); margin-top: 2px; }

.mode-selectors { display: flex; gap: 32px; }
.mode-item {
  background: none; border: none; padding: 0;
  font-size: 0.92rem; font-weight: 500; color: var(--color-slate); cursor: pointer;
  white-space: nowrap;
  transition: color 0.25s ease;
}
.mode-item:hover, .mode-item.active { color: var(--color-ink); }
.mode-item.active { font-weight: 600; }

.lingmai-link {
  background: var(--color-ink); color: #ffffff; border: none;
  padding: 10px 24px; border-radius: 40px; font-size: 0.85rem; font-weight: 600;
  cursor: pointer; display: flex; align-items: center; gap: 6px;
  transition: opacity 0.2s ease;
  flex-shrink: 0;
}

.curated-view { max-width: 1400px; margin: 0 auto; padding: 72px 5% 120px; }
.curated-stream-grid {
  display: grid;
  grid-template-columns: repeat(12, 1fr);
  gap: 40px 32px;
}

.stream-card {
  display: flex; flex-direction: column; justify-content: space-between;
  cursor: pointer; border-bottom: 1px solid var(--color-line); padding-bottom: 40px;
}

.card-meta { display: flex; align-items: center; gap: 10px; font-size: 11px; font-weight: 700; letter-spacing: 0.15em; margin-bottom: 24px; }
.meta-tag { color: #0066cc; text-transform: uppercase; }
.post-tag { color: #86868b; }
.meta-dot { color: var(--color-line); }
.meta-author { color: var(--color-ink); opacity: 0.85; }

.is-blog { grid-column: span 8; padding-right: 48px; }
.is-blog .blog-title {
  font-family: var(--font-serif); font-size: 2rem; font-weight: 700;
  color: var(--color-ink); margin: 0 0 16px 0; letter-spacing: -0.03em; line-height: 1.25;
}
.is-blog .blog-excerpt {
  font-family: var(--font-serif); font-size: 1.1rem; line-height: 1.85; color: #333336; margin: 0;
}

/* 短篇简语卡片通用配置 */
.is-post {
  grid-column: span 4;
  background: var(--color-card-bg);
  border-radius: 16px;
  border-bottom: none;
  overflow: hidden;
  transition: transform 0.3s cubic-bezier(0.16, 1, 0.3, 1), box-shadow 0.3s ease;
}

.is-post:hover {
  transform: translateY(-2px);
  box-shadow: 0 12px 30px rgba(0,0,0,0.04);
}

.post-card-content-wrapper {
  padding: 32px;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  flex: 1;
}

/* ✨【新增】：置顶大图英雄区样式 */
.post-card-hero {
  width: 100%;
  aspect-ratio: 16 / 10;
  overflow: hidden;
  background: #f5f5f7;
}

.hero-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  transition: transform 0.5s ease;
}

.is-post:hover .hero-img {
  transform: scale(1.02);
}

.is-post .post-text { font-size: 1.05rem; font-weight: 400; line-height: 1.75; color: #2c2c2e; margin: 0; }

.card-footer {
  display: flex; justify-content: space-between; align-items: center;
  font-size: 12px; font-weight: 500; color: var(--color-slate); margin-top: 32px;
}
.resonance-stat { display: flex; align-items: center; gap: 4px; }

@media (max-width: 768px) {
  .curated-nav {
    height: 72px;
    padding: 0 20px;
    flex-direction: row;
    justify-content: space-between;
  }
  .nav-left { gap: 20px; width: 70%; }
  .brand-sub { display: none; }
  .brand-title { font-size: 1.2rem; }
  .mode-selectors {
    gap: 20px;
    overflow-x: auto;
    padding-bottom: 4px;
    -webkit-overflow-scrolling: touch;
    scrollbar-width: none;
  }
  .mode-selectors::-webkit-scrollbar { display: none; }
  .mode-item { font-size: 0.85rem; }
  .lingmai-link { padding: 8px 14px; font-size: 0.75rem; }
  .lingmai-link span { display: none; }
  .curated-view { padding: 32px 20px 80px; }
  .curated-stream-grid { display: flex; flex-direction: column; gap: 48px; }
  .is-blog, .is-post { width: 100%; grid-column: auto; padding-right: 0; }
  .is-blog .blog-title { font-size: 1.5rem; margin-bottom: 12px; }
  .is-blog .blog-excerpt { font-size: 1rem; line-height: 1.7; }
  .post-card-content-wrapper { padding: 24px; }
}

@media (max-width: 380px) {
  .mode-selectors { display: none; }
}

.curated-loading, .curated-empty { padding: 120px 0; text-align: center; color: var(--color-slate); }
.loading-pulse { width: 28px; height: 28px; border: 2px solid var(--line); border-top-color: var(--color-ink); border-radius: 50%; margin: 0 auto 16px; animation: spin 0.85s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }
.animate-fade-in { animation: fadeIn 0.4s cubic-bezier(0.16, 1, 0.3, 1); }
@keyframes fadeIn { from { opacity: 0; transform: translateY(12px); } to { opacity: 1; transform: translateY(0); } }
</style>