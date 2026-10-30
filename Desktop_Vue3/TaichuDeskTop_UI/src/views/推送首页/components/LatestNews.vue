<template>
  <div class="latest-news-wrapper">
    <header class="md-header">
      <h2 class="md-display-title">最新动态</h2>
      <div class="md-pulse-loader" v-if="loading"></div>
    </header>

    <div v-if="activeNews.length === 0 && !loading" class="md-empty-state">
      <p>暂无发布记录</p>
    </div>

    <template v-else>
      <!-- 🔥 所有生效中的公告都高亮展示（常驻优先，定时其次），不再只顶最新一条 -->
      <article v-for="item in activeNews" :key="item.id" class="news-featured" :class="'imp-' + (item.importance || 'normal')" @click="openDetail(item)">
        <span class="featured-badge">
          <span class="pulse-dot"></span>
          {{ impPrefix(item.importance) }} · {{ item.noticeKind === 'scheduled' ? '⏰ 定时公告' : '📌 常驻公告' }}
        </span>
        <h3 class="featured-title">{{ item.title }}</h3>
        <div class="featured-foot">
          <span class="featured-meta">
            {{ item.type }} · {{ formatDisplayDate(item.createdAt) }}
            <template v-if="item.noticeKind === 'scheduled' && item.expireAt"> · {{ remainingText(item.expireAt) }}</template>
          </span>
          <span class="featured-cta">查看详情 →</span>
        </div>
        <img v-if="item.imageUrl" :src="item.imageUrl" class="featured-img" alt="" />
      </article>
    </template>

    <Teleport to="body">
      <Transition name="fade">
        <div v-if="isDetailOpen" class="md-drawer-overlay" @click="closeDetail"></div>
      </Transition>

      <Transition name="slide-drawer">
        <aside v-if="isDetailOpen" class="md-side-drawer">
          <div class="drawer-header">
            <button class="close-btn" @click="closeDetail">✕</button>
          </div>

          <div class="drawer-scroll-body">
            <header class="detail-header">
              <div class="detail-meta">
                <span class="item-tag">{{ selectedNews?.type }}</span>
                <span class="item-date">{{ formatDisplayDate(selectedNews?.createdAt) }}</span>
              </div>
              <h1 class="detail-title">{{ selectedNews?.title }}</h1>
            </header>

            <div v-if="selectedNews?.imageUrl" class="detail-hero">
              <img :src="selectedNews.imageUrl" class="detail-hero-img" />
            </div>

            <div class="detail-content md-rich-text" v-html="renderContent(selectedNews?.content || '')"></div>
            
            <footer class="detail-footer">
              <div class="footer-line"></div>
              <p>— END —</p>
            </footer>
          </div>
        </aside>
      </Transition>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import DOMPurify from 'dompurify';
import { newsApi } from '@/api/news';

const newsList = ref<any[]>([]);
const loading = ref(false);
const isDetailOpen = ref(false);
const selectedNews = ref<any>(null);

// 正文渲染：保留换行 + 自动识别链接（外部 URL / 站内路径）
function renderContent(text: string): string {
  if (!text) return '';
  let html = text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
  // 外部链接
  html = html.replace(/(https?:\/\/[^\s<]+)/g, '<a href="$1" target="_blank" rel="noopener noreferrer">$1</a>');
  // 站内 hash 路由（#/games?game=...）
  html = html.replace(/(^|[\s(])(#\/[^\s<]*)/g, '$1<a href="$2">$2</a>');
  // 站内路径（/xxx，避免匹配到 https:// 里的斜杠）
  html = html.replace(/(^|[\s(])(\/[A-Za-z0-9_\-][A-Za-z0-9_\-\/?=&%#.]*)/g, '$1<a href="$2">$2</a>');
  // 换行
  html = html.replace(/\n/g, '<br>');
  return DOMPurify.sanitize(html, { ADD_ATTR: ['target', 'rel'] });
}

// 只展示仍有效的公告（定时公告到期后自动隐藏）
function isActive(n: any): boolean {
  if (!n) return false;
  if (n.noticeKind === 'scheduled' && n.expireAt) return new Date(n.expireAt).getTime() > Date.now();
  return true;
}
// 排序：重要等级（紧急 > 重要 > 普通）→ 常驻优先 → 发布时间倒序
const impWeight = (v: string | undefined) => v === 'urgent' ? 2 : v === 'important' ? 1 : 0;
const impPrefix = (v: string | undefined) => v === 'urgent' ? '🔴 紧急' : v === 'important' ? '🟠 重要' : '📢';
const activeNews = computed(() => {
  const list = newsList.value.filter(isActive);
  return [...list].sort((a, b) => {
    const ia = impWeight(a.importance), ib = impWeight(b.importance);
    if (ia !== ib) return ib - ia;
    const pa = a.noticeKind === 'scheduled' ? 1 : 0;
    const pb = b.noticeKind === 'scheduled' ? 1 : 0;
    if (pa !== pb) return pa - pb;
    return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
  });
});

function remainingText(expireAt: string): string {
  const ms = new Date(expireAt).getTime() - Date.now();
  if (ms <= 0) return '已结束';
  const days = Math.floor(ms / 86400000);
  const hours = Math.floor((ms % 86400000) / 3600000);
  if (days > 0) return `剩余 ${days} 天`;
  if (hours > 0) return `剩余 ${hours} 小时`;
  return `剩余 ${Math.max(1, Math.floor(ms / 60000))} 分钟`;
}

const fetchNews = async () => {
  loading.value = true;
  try {
    const data = await newsApi.getAllNews();
    newsList.value = Array.isArray(data) ? data.filter((item: any) => item?.isPublished) : [];
  } catch (error) {
    console.error('Fetch News Failed', error);
  } finally {
    loading.value = false;
  }
};

onMounted(fetchNews);

const openDetail = (item: any) => {
  selectedNews.value = item;
  isDetailOpen.value = true;
  document.body.style.overflow = 'hidden'; // 锁定底层滚动
};

const closeDetail = () => {
  isDetailOpen.value = false;
  document.body.style.overflow = '';
  // 延迟清空数据，防止抽屉收回时内容闪烁消失
  setTimeout(() => { selectedNews.value = null; }, 400); 
};

const formatDisplayDate = (dateString: string) => {
  if (!dateString) return '';
  const date = new Date(dateString);
  return date.toLocaleDateString('en-US', { 
    month: 'short', 
    day: 'numeric', 
    year: 'numeric' 
  });
};
</script>

<style scoped>
/* ==========================================
   嵌入式列表样式 (适应 index.vue 的局部布局)
   ========================================== */
.latest-news-wrapper {
  margin-bottom: 56px;
  color: var(--text-primary);
}

.md-header {
  display: flex;
  align-items: center;
  gap: 20px;
  margin-bottom: 40px;
}

.md-display-title {
  font-size: 2rem; /* 减小字号以适应侧边栏存在时的比例 */
  font-weight: 300;
  letter-spacing: -1px;
  margin: 0;
}

/* 🔥 置顶公告大卡片 */
.news-featured {
  position: relative;
  display: block;
  margin-bottom: 28px;
  padding: 24px 26px;
  border: 1px solid var(--accent-color);
  border-radius: 0;
  background: linear-gradient(135deg, var(--accent-soft), var(--paper-card) 70%);
  cursor: pointer;
  overflow: hidden;
  transition: transform .25s, box-shadow .25s, border-color .25s;
}
.news-featured:hover {
  transform: translateY(-2px);
  box-shadow: 0 10px 30px rgba(0, 0, 0, .08);
}
/* 重要等级：重要（橙）/ 紧急（红） */
.news-featured.imp-important {
  border-color: #f59e0b;
  background: linear-gradient(135deg, rgba(245, 158, 11, .12), var(--paper-card) 70%);
}
.news-featured.imp-urgent {
  border-color: #ef4444;
  background: linear-gradient(135deg, rgba(239, 68, 68, .14), var(--paper-card) 70%);
}
.featured-badge {
  display: inline-flex; align-items: center; gap: 6px;
  font-size: .72rem; font-weight: 700; letter-spacing: 1px;
  color: var(--accent-color);
  background: var(--paper-card);
  border: 1px solid var(--accent-color);
  border-radius: 0;
  padding: 3px 12px;
}
.pulse-dot {
  width: 7px; height: 7px; border-radius: 50%;
  background: #e63946;
  animation: pulse 1.6s infinite;
}
@keyframes pulse {
  0% { box-shadow: 0 0 0 0 rgba(230, 57, 70, .6); }
  70% { box-shadow: 0 0 0 8px rgba(230, 57, 70, 0); }
  100% { box-shadow: 0 0 0 0 rgba(230, 57, 70, 0); }
}
.featured-title {
  margin: 14px 0 12px;
  font-size: 1.45rem;
  font-weight: 500;
  line-height: 1.4;
  color: var(--text-primary);
}
.featured-foot { display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-wrap: wrap; }
.featured-meta { font-size: .8rem; color: var(--text-secondary); }
.featured-cta { font-size: .85rem; font-weight: 600; color: var(--accent-color); white-space: nowrap; }
.featured-img { width: 100%; max-height: 220px; object-fit: cover; border-radius: 0; margin-top: 16px; display: block; }

/* 列表项 */
.md-news-item {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 32px 0;
  border-bottom: 1px solid var(--bg-sub);
  cursor: pointer;
  transition: border-color 0.4s ease;
  gap: 24px;
}

.md-news-item:hover {
  border-bottom-color: var(--text-primary);
}

.item-content { flex: 1; min-width: 0; }

.item-meta {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 8px;
  font-family: ui-monospace, monospace;
}

.item-tag { font-size: 0.7rem; font-weight: 700; text-transform: uppercase; letter-spacing: 2px; }
.item-dot { width: 4px; height: 4px; background: var(--border-line); border-radius: 50%; }
.item-date { font-size: 0.8rem; color: var(--text-secondary); }

.item-title {
  font-size: 1.25rem;
  font-weight: 400;
  line-height: 1.5;
  margin: 0;
  transition: color 0.3s;
}

.md-news-item:hover .item-title { color: var(--text-secondary); }

/* 列表缩略图 */
.item-visual { flex-shrink: 0; }
.img-wrapper {
  width: 140px; height: 88px; overflow: hidden; background: var(--bg-sub);
  transition: transform 0.6s cubic-bezier(0.16, 1, 0.3, 1);
}
.md-cover-img { width: 100%; height: 100%; object-fit: cover; filter: grayscale(100%); transition: filter 0.6s; }
.md-news-item:hover .md-cover-img { filter: grayscale(0%); transform: scale(1.05); }

/* 加载动画 */
.md-pulse-loader {
  width: 30px; height: 2px; background: var(--text-primary);
  animation: md-load 1s infinite ease-in-out alternate;
}
@keyframes md-load { from { opacity: 0; transform: scaleX(0.2); } to { opacity: 1; transform: scaleX(1); } }

/* 空状态 */
.md-empty-state { padding: 40px 0; color: var(--border-line); font-style: italic; }

/* ==========================================
   🌟 侧边抽屉样式 (抽离出文档流)
   ========================================== */
.md-drawer-overlay {
  position: fixed;
  inset: 0;
  background: var(--overlay); /* 半透明白底 */
  backdrop-filter: blur(8px); /* 毛玻璃 */
  z-index: 2000;
}

.md-side-drawer {
  position: fixed;
  top: 0;
  right: 0;
  width: 100%;
  max-width: 650px; /* 抽屉宽度 */
  height: 100vh;
  background: var(--paper-card);
  z-index: 2001;
  box-shadow: -10px 0 40px rgba(0, 0, 0, 0.08); /* 极度柔和的左侧阴影 */
  display: flex;
  flex-direction: column;
}

.drawer-header {
  padding: 24px 32px;
  display: flex;
  justify-content: flex-end;
  background: var(--paper-card); /* 确保不透明 */
}

.close-btn {
  background: none; border: none; font-size: 1.5rem; color: var(--text-secondary);
  cursor: pointer; font-weight: 200; transition: color 0.2s, transform 0.2s;
}
.close-btn:hover { color: var(--text-primary); transform: rotate(90deg); }

.drawer-scroll-body {
  flex: 1;
  overflow-y: auto;
  padding: 20px 60px 100px; /* 左右留足呼吸空间 */
  scrollbar-width: none;
}
.drawer-scroll-body::-webkit-scrollbar { display: none; }

.detail-header { margin-bottom: 40px; }
.detail-meta { display: flex; gap: 16px; margin-bottom: 16px; font-family: monospace; color: var(--text-secondary); }
.detail-title { font-size: 2.2rem; font-weight: 300; line-height: 1.3; margin: 0; letter-spacing: -0.5px; }

.detail-hero { margin-bottom: 40px; }
.detail-hero-img { width: 100%; border-radius: 0; object-fit: cover; }

/* 🌟 富文本正文 */
.md-rich-text {
  font-size: 1.05rem; line-height: 2; color: var(--text-primary); font-weight: 400;
  white-space: pre-wrap; /* 保留后台录入的换行 */
  word-break: break-word;
}
.md-rich-text :deep(img) { max-width: 100%; height: auto; margin: 30px 0; border-radius: 0; }
.md-rich-text :deep(p) { margin-bottom: 24px; }

.detail-footer { margin-top: 80px; text-align: center; color: var(--border-line); }
.footer-line { width: 40px; height: 1px; background: var(--border-line); margin: 0 auto 20px; }

/* ==========================================
   动画特效
   ========================================== */
.fade-enter-active, .fade-leave-active { transition: opacity 0.4s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

.slide-drawer-enter-active, .slide-drawer-leave-active {
  transition: transform 0.5s cubic-bezier(0.16, 1, 0.3, 1); /* 高级弹簧缓动 */
}
.slide-drawer-enter-from, .slide-drawer-leave-to {
  transform: translateX(100%); /* 从右侧滑入/滑出 */
}

/* 移动端适配 */
@media (max-width: 600px) {
  .md-news-item { flex-direction: column-reverse; gap: 16px; }
  .img-wrapper { width: 100%; height: 160px; }
  .drawer-scroll-body { padding: 20px 24px 80px; }
  .detail-title { font-size: 1.8rem; }
}
</style>