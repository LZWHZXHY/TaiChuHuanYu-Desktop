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
            <img :src="art.coverImageUrl || '/default-cover.jpg'" class="item-image" loading="lazy" />
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
      <div v-if="loading" class="loading-text">加载中...</div>
      <div v-else-if="noMore" class="end-text">已抵达尽头</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue';
import { artworkApi, type ArtworkItemDto } from '../../api/artwork'; // 引入刚才写的API

// 使用后端返回的 DTO 类型
const artworks = ref<ArtworkItemDto[]>([]);
const loading = ref(false);
const noMore = ref(false);
const loadMoreRef = ref<HTMLElement | null>(null);

// 分页偏移量
const offset = ref(0);
const pageSize = 20;

// 响应式列数控制 (保持不变)
const columnCount = ref(5);
const updateColumnCount = () => {
  const width = window.innerWidth;
  if (width < 640) columnCount.value = 2;
  else if (width < 1024) columnCount.value = 3;
  else if (width < 1440) columnCount.value = 4;
  else columnCount.value = 5;
};

const getColumnItems = (colIndex: number) => {
  return artworks.value.filter((_, index) => index % columnCount.value === colIndex);
};

// --- 实战化数据加载 ---
const loadData = async () => {
  if (loading.value || noMore.value) return;
  loading.value = true;
  
  try {
    const res = await artworkApi.getGallery(offset.value, pageSize);
    
    // 追加数据
    artworks.value.push(...res.data);
    
    // 更新偏移量用于下一次请求
    offset.value += res.data.length;
    
    // 判断是否加载完毕
    if (!res.hasMore || res.data.length === 0) {
      noMore.value = true;
    }
  } catch (error) {
    console.error('灵脉阻塞，获取作品失败:', error);
  } finally {
    loading.value = false;
  }
};

let observer: IntersectionObserver | null = null;

onMounted(() => {
  updateColumnCount();
  window.addEventListener('resize', updateColumnCount);

  observer = new IntersectionObserver((entries) => {
    // rootMargin: '400px' 表示距离底部还有400px时就开始预加载
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
  box-sizing: border-box; /* 确保 padding 不会额外增加宽度 */
  width: 100%;           /* 必须显式声明宽度 */
  overflow-x: hidden;    /* 防止意外的横向滚动条 */
}

.masonry-grid {
  display: flex;
  gap: 24px;
  align-items: flex-start;
  width: 100%;           /* 撑满父容器 */
  box-sizing: border-box;
}

.masonry-column {
  flex: 1;
  min-width: 0;          /* 关键！防止 Flex 子元素被内部长内容撑开 */
  display: flex;
  flex-direction: column;
  gap: 24px;
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
  .artwork-masonry-wrapper {
    padding: 0 10px; /* 手机端边距减小，留给内容更多空间 */
  }
  .masonry-grid { 
    gap: 10px;    /* 列间距减小 */
  }
  .masonry-column { 
    gap: 10px;    /* 行间距减小 */
  }
  
  /* 调整标题字号，防止长标题撑开卡片 */
  .item-title {
    font-size: 0.8rem;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
}
</style>