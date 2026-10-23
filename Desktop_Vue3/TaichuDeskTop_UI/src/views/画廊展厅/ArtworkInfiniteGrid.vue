<template>
  <div class="artwork-masonry-wrapper">
    <div class="masonry-grid">
      <div v-for="(column, colIndex) in computedColumns" :key="colIndex" class="masonry-column">
        <div 
          v-for="art in column" 
          :key="art.id" 
          class="masonry-item"
        >
          <button class="item-visual" :aria-label="'查看作品：' + art.title" @click="$emit('on-click', art.id)">
            <img v-if="art.coverImageUrl && !failedImages.has(art.id)" :src="coverSrc(art)" class="item-image" :alt="art.title" loading="lazy" @error="onImageError(art)" /><span v-else class="image-fallback">图片暂不可用 · 打开作品查看</span>
            <div class="item-overlay">
              <span class="badge">{{ art.imageCount }} 张</span>
            </div>
          </button>
          
          <div class="item-details">
            <h2 class="item-title"><button @click="$emit('on-click', art.id)">{{ art.title }}</button></h2>

            <div class="item-footer">
              <div class="author-info">
                <img v-if="art.authorAvatar" :src="getFullImageUrl(art.authorAvatar, 100)" class="avatar" alt="作者头像" />
                <span>{{ art.authorName }}</span>
              </div>
              
              <InteractActions show-count 
                :target-id="art.id" 
                target-type="Artwork" 
                :initial-stats="art" 
              />
            </div>
          </div>
        </div>
      </div>
    </div>

    <div ref="loadMoreRef" class="status-bar">
      <div v-if="loading" class="loading-text">加载中...</div>
      <div v-else-if="error" role="alert">{{ error }} <button @click="loadData">重新加载</button></div>
      <div v-else-if="noMore" class="end-text">{{ artworks.length ? '已显示全部作品' : '没有找到匹配的作品' }}</div>
      <button v-if="!loading && !error && !noMore" @click="loadData">加载更多</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, watch } from 'vue'; // 🌟 修改：引入 computed
import { artworkApi, type ArtworkItemDto } from '../../api/artwork'; 
import InteractActions from '../../components/InteractActions.vue';
import { useArtworkStore } from '../../stores/artwork';
import { useUserStore } from '../../stores/user';
import { withThumbnail, toCdnUrl } from '../../utils/imageThumb';
const props = defineProps<{ search?: string }>();
const store = useArtworkStore(), user = useUserStore();
const error = ref('');
const failedImages = ref(new Set<number>());
const retriedImages = ref(new Set<number>());
let generation = 0;

// 使用后端返回的 DTO 类型
const artworks = ref<ArtworkItemDto[]>([]);
const loading = ref(false);
const noMore = ref(false);
const loadMoreRef = ref<HTMLElement | null>(null);

// 分页偏移量
const offset = ref(0);
const pageSize = 20;

defineEmits(['on-click']);


// 明确 url 的类型为 string，或者是可能为空的 null/undefined
const originalUrl = (url: string | null | undefined): string => {
  if (!url) return '/default-cover.jpg';
  const full = url.startsWith('http') ? url : `https://img.bianyuzhou.com/uploads/${url}`;
  return toCdnUrl(full);
};
const getFullImageUrl = (url: string | null | undefined, width = 400): string => {
  const full = originalUrl(url);
  // 追加数据万象缩略参数（COS/CDN 域名才生效）
  return withThumbnail(full, width);
};

// 图片加载失败处理：缩略图失败 → 回退原图；原图也失败 → 才显示「暂不可用」
function onImageError(art: ArtworkItemDto) {
  if (!retriedImages.value.has(art.id)) {
    retriedImages.value.add(art.id)
    return
  }
  failedImages.value.add(art.id)
}
function coverSrc(art: ArtworkItemDto): string {
  const full = originalUrl(art.coverImageUrl)
  return retriedImages.value.has(art.id) ? full : withThumbnail(full, 400)
}



// 响应式列数控制
const columnCount = ref(3);
const updateColumnCount = () => {
  const width = window.innerWidth;
  if (width < 600) columnCount.value = 1;
  else if (width < 1000) columnCount.value = 2;
  else columnCount.value = 3;
};

// 🌟 修改：将原有的 getColumnItems 方法重构为高效的计算属性
// 这样每次页面中有微小响应式状态改变（例如：点赞、悬停动画）时，都不会触发全量数据的重复过滤操作
const computedColumns = computed(() => {
  const count = columnCount.value;
  // 预先初始化好对应列数的容器数组
  const cols = Array.from({ length: count }, () => [] as ArtworkItemDto[]);
  
  // 单次线性遍历分发数据，算法复杂度由 O(N * K) 降为 O(N)
  artworks.value.forEach((art, index) => {
    cols[index % count].push(art);
  });
  
  return cols;
});

// --- 实战化数据加载 ---
const loadData = async () => {
  if (loading.value || noMore.value) return;
  loading.value = true;
  error.value = '';
  const current = generation, snapshot = store.capture();
  try {
    const res = await artworkApi.getGallery(offset.value, pageSize, props.search);
    
    if (current !== generation || snapshot.session !== store.capture().session) return;
    store.seed(res.data, snapshot);
    // 追加数据
    artworks.value.push(...res.data);
    
    // 更新偏移量用于下一次请求
    offset.value += res.data.length;
    
    // 判断是否加载完毕
    if (!res.hasMore || res.data.length === 0) {
      noMore.value = true;
    }
  } catch (error) {
    if (current === generation) loadError(error);
  } finally {
    if (current === generation) loading.value = false;
  }
};
const loadError = (cause: any) => { error.value = cause.friendlyMessage || '作品加载失败，请重试'; };
watch(() => [user.token, props.search], () => {
  generation++; failedImages.value.clear(); artworks.value = []; offset.value = 0; noMore.value = false; loading.value = false;
  void loadData();
}, { immediate: true });

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
  generation++;
  window.removeEventListener('resize', updateColumnCount);
  if (observer) observer.disconnect();
});
</script>

<style scoped>
.artwork-masonry-wrapper { width: 100%; }
.masonry-grid { display: flex; gap: 24px; align-items: flex-start; }
.masonry-column { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 24px; }
.masonry-item { background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 12px; overflow: hidden; transition: border-color .2s; }
.masonry-item:hover, .masonry-item:focus-within { border-color: var(--accent-color); }
.item-visual { position: relative; display: block; width: 100%; padding: 0; border: 0; background: var(--paper-sub); cursor: pointer; }
.item-image { display: block; width: 100%; height: auto; }
.image-fallback { display: grid; place-items: center; min-height: 200px; padding: 20px; color: var(--text-secondary); font-size: .85rem; }
.item-overlay { position: absolute; right: 12px; bottom: 12px; pointer-events: none; }
.badge { background: #0009; color: #fff; font-size: .75rem; border-radius: 6px; padding: 4px 8px; }
.item-details { padding: 16px 18px; }
.item-title { margin: 0 0 12px; font-size: 1.1rem; line-height: 1.5; }
.item-title button { background: none; color: var(--text-primary); border: 0; padding: 0; text-align: left; font: inherit; font-weight: 600; overflow-wrap: anywhere; }
.item-footer { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 12px; }
.author-info { display: flex; align-items: center; gap: 8px; min-width: 0; color: var(--text-secondary); font-size: .82rem; overflow-wrap: anywhere; }
.avatar { width: 24px; height: 24px; object-fit: cover; border-radius: 50%; }
.status-bar { text-align: center; color: var(--text-secondary); padding: 32px 0; }
.status-bar button { color: var(--text-primary); background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 8px; padding: 8px 16px; }
@media(max-width: 720px) { .masonry-grid, .masonry-column { gap: 16px; } }
</style>
