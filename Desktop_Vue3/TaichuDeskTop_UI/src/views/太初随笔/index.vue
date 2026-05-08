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
            @click="changeFilter('essay')" 
            :class="['mode-item', { active: currentFilter === 'essay' }]"
          >
            长文随笔
          </button>
          <button 
            @click="changeFilter('thought')" 
            :class="['mode-item', { active: currentFilter === 'thought' }]"
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
      <div v-if="loading" class="curated-loading">
        <div class="loading-pulse"></div>
        <span class="loading-text">正在共鸣太初视界...</span>
      </div>

      <div v-else-if="filteredContent.length === 0" class="curated-empty">
        <p class="empty-text">视界中空无一物，待你落笔生花。</p>
      </div>

      <div v-else class="curated-stream-grid">
        <div 
          v-for="item in filteredContent" :key="item.id" 
          class="stream-card" 
          :class="[item.type === 'essay' ? 'is-blog' : 'is-post']"
          @click="openArtwork(item.id)"
        >
          
          <template v-if="item.type === 'essay'">
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

          <template v-else>
            <div class="card-meta">
              <span class="meta-tag post-tag">FRAGMENT</span>
              <span class="meta-dot">/</span>
              <span class="meta-author">{{ item.author }}</span>
            </div>
            <div class="card-body">
              <p class="post-text">
                “ {{ getSnippet(item.excerpt || item.content, 160) }} ”
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

        </div>
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
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';

import PublicNoteDetail from './PublicNoteDetail.vue';
import type { PublishedNoteItem } from '../../api/NotePublish';
import { notePublishApi } from '../../api/NotePublish';

interface FrontMixedPost extends Omit<PublishedNoteItem, 'type'> {
  type: 'essay' | 'thought';
  content?: string;
  author: string;
}

const router = useRouter();
const currentFilter = ref<'all' | 'essay' | 'thought'>('all');
const posts = ref<FrontMixedPost[]>([]);
const loading = ref(false);

const activePostId = ref<string | null>(null);

const fetchStream = async () => {
  loading.value = true;
  try {
    const typeQuery = currentFilter.value === 'all' 
      ? undefined 
      : (currentFilter.value === 'essay' ? 'note' : 'thought');

    const res = await notePublishApi.getPublicStream(typeQuery);
    
    if (res && Array.isArray(res)) {
      posts.value = res.map((item: any) => ({
        ...item,
        type: item.type === 'note' ? 'essay' : 'thought',
        author: '太初隐者',
        content: item.excerpt
      }));
    }
  } catch (err) {
    console.error('广场数据感应失败:', err);
  } finally {
    loading.value = false;
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
      if (node.type === 'text') return node.text || '';
      if (Array.isArray(node.content)) return node.content.map(parseNode).join('');
      if (node.content && typeof node.content === 'object') return parseNode(node.content);
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

const changeFilter = (type: 'all' | 'essay' | 'thought') => {
  currentFilter.value = type;
  fetchStream();
};

const filteredContent = computed(() => {
  if (currentFilter.value === 'all') return posts.value;
  return posts.value.filter(item => item.type === currentFilter.value);
});

const formatTime = (timeStr: string | undefined) => {
  if (!timeStr) return '刚刚';
  const date = new Date(timeStr);
  return `${date.getMonth() + 1}月${date.getDate()}日`;
};

onMounted(async () => {
  await fetchStream();

  const path = window.location.pathname;
  if (path.startsWith('/posts/')) {
    const directId = path.split('/').pop();
    if (directId && directId !== 'posts') {
      activePostId.value = directId;
    }
  }
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

/* 🌟 顶部导航 - 响应式优化 */
.curated-nav {
  display: flex;
  justify-content: space-between;
  align-items: center;
  height: 104px; /* 桌面端高度 */
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

/* 🌟 视界主体布局 */
.curated-view { max-width: 1400px; margin: 0 auto; padding: 72px 5% 120px; }
.curated-stream-grid {
  display: grid;
  grid-template-columns: repeat(12, 1fr);
  gap: 40px 32px;
}

/* 🌟 卡片通用规范 */
.stream-card {
  display: flex; flex-direction: column; justify-content: space-between;
  cursor: pointer; border-bottom: 1px solid var(--color-line); padding-bottom: 40px;
}

.card-meta { display: flex; align-items: center; gap: 10px; font-size: 11px; font-weight: 700; letter-spacing: 0.15em; margin-bottom: 24px; }
.meta-tag { color: #0066cc; text-transform: uppercase; }
.post-tag { color: #86868b; }
.meta-dot { color: var(--color-line); }
.meta-author { color: var(--color-ink); opacity: 0.85; }

/* 🌟 博客排版（长文） */
.is-blog { grid-column: span 8; padding-right: 48px; }
.is-blog .blog-title {
  font-family: var(--font-serif); font-size: 2rem; font-weight: 700;
  color: var(--color-ink); margin: 0 0 16px 0; letter-spacing: -0.03em; line-height: 1.25;
}
.is-blog .blog-excerpt {
  font-family: var(--font-serif); font-size: 1.1rem; line-height: 1.85; color: #333336; margin: 0;
}

/* 🌟 帖子排版（短语） */
.is-post {
  grid-column: span 4;
  background: var(--color-card-bg);
  padding: 32px;
  border-radius: 12px;
  border-bottom: none;
}
.is-post .post-text { font-size: 1.05rem; font-weight: 400; line-height: 1.75; color: #2c2c2e; margin: 0; }

.card-footer {
  display: flex; justify-content: space-between; align-items: center;
  font-size: 12px; font-weight: 500; color: var(--color-slate); margin-top: 32px;
}
.resonance-stat { display: flex; align-items: center; gap: 4px; }

/* 📱 移动端适配核心代码 (Mobile First Adjustments) */
@media (max-width: 768px) {
  /* 1. 缩小 Header 并处理溢出 */
  .curated-nav {
    height: 72px; /* 移动端减小高度 */
    padding: 0 20px;
    flex-direction: row;
    justify-content: space-between;
  }

  .nav-left { gap: 20px; width: 70%; }
  
  /* 隐藏副标题以节省空间 */
  .brand-sub { display: none; }
  .brand-title { font-size: 1.2rem; }

  /* 模式选择器改为水平滑动，不换行 */
  .mode-selectors {
    gap: 20px;
    overflow-x: auto;
    padding-bottom: 4px;
    -webkit-overflow-scrolling: touch;
    scrollbar-width: none; /* 隐藏进度条 */
  }
  .mode-selectors::-webkit-scrollbar { display: none; }
  .mode-item { font-size: 0.85rem; }

  /* 灵脉按钮在手机端简化 */
  .lingmai-link {
    padding: 8px 14px;
    font-size: 0.75rem;
  }
  .lingmai-link span { display: none; } /* 隐藏箭头 */

  /* 2. 网格调整为单列流 */
  .curated-view {
    padding: 32px 20px 80px;
  }
  
  .curated-stream-grid {
    display: flex;
    flex-direction: column;
    gap: 48px; /* 增加卡片垂直间距 */
  }

  .is-blog, .is-post {
    width: 100%;
    grid-column: auto;
    padding-right: 0;
  }

  /* 3. 字体微调 */
  .is-blog .blog-title {
    font-size: 1.5rem; /* 减小标题字号防止断行太碎 */
    margin-bottom: 12px;
  }
  .is-blog .blog-excerpt {
    font-size: 1rem;
    line-height: 1.7;
  }

  .is-post {
    padding: 24px; /* 减小内边距 */
  }
  
  .card-meta { margin-bottom: 16px; }
  .card-footer { margin-top: 24px; }
}

/* 针对极窄屏幕（如 iPhone SE） */
@media (max-width: 380px) {
  .mode-selectors { display: none; } /* 屏幕太小时隐藏分类，或可考虑放入汉堡菜单 */
}

/* 加载与动画保持不变 */
.curated-loading, .curated-empty { padding: 120px 0; text-align: center; color: var(--color-slate); }
.loading-pulse {
  width: 28px; height: 28px; border: 2px solid var(--color-line); border-top-color: var(--color-ink);
  border-radius: 50%; margin: 0 auto 16px; animation: spin 0.85s linear infinite;
}
@keyframes spin { to { transform: rotate(360deg); } }
.animate-fade-in { animation: fadeIn 0.4s cubic-bezier(0.16, 1, 0.3, 1); }
@keyframes fadeIn {
  from { opacity: 0; transform: translateY(12px); }
  to { opacity: 1; transform: translateY(0); }
}
</style>