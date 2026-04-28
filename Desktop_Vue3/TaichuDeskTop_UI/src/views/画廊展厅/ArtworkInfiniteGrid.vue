<template>
  <div class="artwork-masonry-wrapper">
    <div class="masonry-grid">
      <div v-for="colIndex in columnCount" :key="colIndex" class="masonry-column">
        <div 
          v-for="art in getColumnItems(colIndex - 1)" 
          :key="art.id" 
          class="masonry-item group"
        >
          <div class="item-visual">
            <img :src="art.cover" class="item-image" loading="lazy" />
            <div class="item-overlay">
              <span class="badge">{{ art.imageCount }}P</span>
            </div>
          </div>
          
          <div class="item-details">
            <h4 class="item-title">{{ art.title }}</h4>
            <div class="item-footer">
              <div class="author-info">
                <img :src="art.authorAvatar" class="avatar" />
                <span>{{ art.authorName }}</span>
              </div>
              <button class="love-btn">❤️</button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div ref="loadMoreRef" class="status-bar">
      <div v-if="loading" class="loading-text">灵脉搬运中...</div>
      <div v-else-if="noMore" class="end-text">已触达维度尽头</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue';

interface Artwork {
  id: number;
  title: string;
  cover: string;
  authorName: string;
  authorAvatar: string;
  imageCount: number;
}

const artworks = ref<Artwork[]>([]);
const loading = ref(false);
const noMore = ref(false);
const loadMoreRef = ref<HTMLElement | null>(null);

// 响应式列数控制
const columnCount = ref(5);
const updateColumnCount = () => {
  const width = window.innerWidth;
  if (width < 640) columnCount.value = 2;
  else if (width < 1024) columnCount.value = 3;
  else if (width < 1440) columnCount.value = 4;
  else columnCount.value = 5;
};

// 将数据分发到对应的列
const getColumnItems = (colIndex: number) => {
  return artworks.value.filter((_, index) => index % columnCount.value === colIndex);
};

const loadData = async () => {
  if (loading.value || noMore.value) return;
  loading.value = true;
  await new Promise(resolve => setTimeout(resolve, 800));
  
  const newData: Artwork[] = Array.from({ length: 15 }).map((_, i) => {
    const currentId = artworks.value.length + i;
    // 关键：随机高度区间锁定在高级比例
    const randomHeight = Math.floor(Math.random() * 200 + 300); 
    return {
      id: currentId,
      title: `维度碎片 #${currentId}`,
      cover: `https://picsum.photos/400/${randomHeight}?random=${currentId}`,
      authorName: '隐世道友',
      authorAvatar: `https://api.dicebear.com/7.x/avataaars/svg?seed=${currentId}`,
      imageCount: Math.floor(Math.random() * 5 + 1)
    };
  });

  artworks.value.push(...newData);
  if (artworks.value.length >= 60) noMore.value = true;
  loading.value = false;
};

let observer: IntersectionObserver | null = null;

onMounted(() => {
  updateColumnCount();
  window.addEventListener('resize', updateColumnCount);

  observer = new IntersectionObserver((entries) => {
    if (entries[0].isIntersecting) loadData();
  }, { rootMargin: '400px' });

  if (loadMoreRef.value) observer.observe(loadMoreRef.value);
});

onUnmounted(() => {
  window.removeEventListener('resize', updateColumnCount);
  if (observer) observer.disconnect();
});
</script>

<style scoped>
.artwork-masonry-wrapper {
  max-width: 1600px;
  margin: 0 auto;
  padding: 0 20px;
}

.masonry-grid {
  display: flex;
  gap: 24px; /* 列间距 */
  align-items: flex-start;
}

.masonry-column {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 24px; /* 行间距 */
}

/* 沉浸式卡片 */
.masonry-item {
  background: #fff;
  border-radius: 8px;
  transition: transform 0.4s ease;
}

.item-visual {
  position: relative;
  border-radius: 8px;
  overflow: hidden;
  background: #f9f9f9;
}

.item-image {
  width: 100%;
  height: auto;
  display: block;
  transition: transform 0.8s cubic-bezier(0.2, 0, 0.2, 1);
}

.masonry-item:hover .item-image {
  transform: scale(1.04);
}

.item-overlay {
  position: absolute;
  inset: 0;
  background: rgba(0,0,0,0.02);
  opacity: 0;
  transition: opacity 0.3s;
  padding: 12px;
  display: flex;
  justify-content: flex-end;
}

.masonry-item:hover .item-overlay {
  opacity: 1;
}

.badge {
  background: rgba(0,0,0,0.7);
  color: #fff;
  font-size: 10px;
  padding: 2px 6px;
  border-radius: 4px;
  align-self: flex-start;
}

.item-details {
  padding: 12px 4px;
}

.item-title {
  font-size: 0.9rem;
  font-weight: 600;
  color: #1a1a1a;
  margin-bottom: 8px;
}

.item-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.author-info {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 0.75rem;
  color: #86868b;
}

.avatar {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  filter: grayscale(100%);
}

.love-btn {
  background: none;
  border: none;
  font-size: 0.8rem;
  opacity: 0.3;
  cursor: pointer;
}

/* 状态栏 */
.status-bar {
  padding: 80px 0;
  text-align: center;
  font-size: 0.85rem;
  color: #c7c7c7;
  letter-spacing: 0.2em;
}

.loading-text { animation: breathe 1.5s infinite; }

@keyframes breathe {
  0%, 100% { opacity: 0.3; }
  50% { opacity: 1; }
}

@media (max-width: 640px) {
  .masonry-grid { gap: 12px; }
  .masonry-column { gap: 12px; }
}
</style>