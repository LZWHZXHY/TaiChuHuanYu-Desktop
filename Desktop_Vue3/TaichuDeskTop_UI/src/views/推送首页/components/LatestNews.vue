<template>
  <div class="latest-news-wrapper">
    <header class="md-header">
      <h2 class="md-display-title">最新动态</h2>
      <div class="md-pulse-loader" v-if="loading"></div>
    </header>

    <div v-if="newsList.length === 0 && !loading" class="md-empty-state">
      <p>暂无发布记录</p>
    </div>

    <div v-else class="md-news-list">
      <article 
        v-for="item in newsList" 
        :key="item.id" 
        class="md-news-item" 
        @click="openDetail(item)"
      >
        <div class="item-content">
          <div class="item-meta">
            <span class="item-tag">{{ item.type }}</span>
            <span class="item-dot"></span>
            <span class="item-date">{{ formatDisplayDate(item.createdAt) }}</span>
          </div>
          <h3 class="item-title">{{ item.title }}</h3>
        </div>

        <div v-if="item.imageUrl" class="item-visual">
          <div class="img-wrapper">
            <img :src="item.imageUrl" :alt="item.title" class="md-cover-img" />
          </div>
        </div>
      </article>
    </div>

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

            <div class="detail-content md-rich-text" v-html="selectedNews?.content"></div>
            
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
import { ref, onMounted } from 'vue';
import { newsApi } from '@/api/news';

const newsList = ref<any[]>([]);
const loading = ref(false);
const isDetailOpen = ref(false);
const selectedNews = ref<any>(null);

const fetchNews = async () => {
  loading.value = true;
  try {
    const data = await newsApi.getAllNews();
    newsList.value = data.filter((item: any) => item.isPublished);
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
  color: #1d1d1f;
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

/* 列表项 */
.md-news-item {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 32px 0;
  border-bottom: 1px solid #f2f2f2;
  cursor: pointer;
  transition: border-color 0.4s ease;
  gap: 24px;
}

.md-news-item:hover {
  border-bottom-color: #000;
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
.item-dot { width: 4px; height: 4px; background: #d2d2d7; border-radius: 50%; }
.item-date { font-size: 0.8rem; color: #86868b; }

.item-title {
  font-size: 1.25rem;
  font-weight: 400;
  line-height: 1.5;
  margin: 0;
  transition: color 0.3s;
}

.md-news-item:hover .item-title { color: #86868b; }

/* 列表缩略图 */
.item-visual { flex-shrink: 0; }
.img-wrapper {
  width: 140px; height: 88px; overflow: hidden; background: #f5f5f7;
  transition: transform 0.6s cubic-bezier(0.16, 1, 0.3, 1);
}
.md-cover-img { width: 100%; height: 100%; object-fit: cover; filter: grayscale(100%); transition: filter 0.6s; }
.md-news-item:hover .md-cover-img { filter: grayscale(0%); transform: scale(1.05); }

/* 加载动画 */
.md-pulse-loader {
  width: 30px; height: 2px; background: #000;
  animation: md-load 1s infinite ease-in-out alternate;
}
@keyframes md-load { from { opacity: 0; transform: scaleX(0.2); } to { opacity: 1; transform: scaleX(1); } }

/* 空状态 */
.md-empty-state { padding: 40px 0; color: #ccc; font-style: italic; }

/* ==========================================
   🌟 侧边抽屉样式 (抽离出文档流)
   ========================================== */
.md-drawer-overlay {
  position: fixed;
  inset: 0;
  background: rgba(255, 255, 255, 0.7); /* 半透明白底 */
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
  background: #fff;
  z-index: 2001;
  box-shadow: -10px 0 40px rgba(0, 0, 0, 0.08); /* 极度柔和的左侧阴影 */
  display: flex;
  flex-direction: column;
}

.drawer-header {
  padding: 24px 32px;
  display: flex;
  justify-content: flex-end;
  background: #fff; /* 确保不透明 */
}

.close-btn {
  background: none; border: none; font-size: 1.5rem; color: #a1aebb;
  cursor: pointer; font-weight: 200; transition: color 0.2s, transform 0.2s;
}
.close-btn:hover { color: #000; transform: rotate(90deg); }

.drawer-scroll-body {
  flex: 1;
  overflow-y: auto;
  padding: 20px 60px 100px; /* 左右留足呼吸空间 */
  scrollbar-width: none;
}
.drawer-scroll-body::-webkit-scrollbar { display: none; }

.detail-header { margin-bottom: 40px; }
.detail-meta { display: flex; gap: 16px; margin-bottom: 16px; font-family: monospace; color: #86868b; }
.detail-title { font-size: 2.2rem; font-weight: 300; line-height: 1.3; margin: 0; letter-spacing: -0.5px; }

.detail-hero { margin-bottom: 40px; }
.detail-hero-img { width: 100%; border-radius: 4px; object-fit: cover; }

/* 🌟 富文本正文 */
.md-rich-text {
  font-size: 1.05rem; line-height: 2; color: #333; font-weight: 400;
}
.md-rich-text :deep(img) { max-width: 100%; height: auto; margin: 30px 0; border-radius: 6px; }
.md-rich-text :deep(p) { margin-bottom: 24px; }

.detail-footer { margin-top: 80px; text-align: center; color: #ccc; }
.footer-line { width: 40px; height: 1px; background: #eee; margin: 0 auto 20px; }

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