<template>
  <div class="md-detail-mask" @click.self="$emit('close')">
    <div v-if="loading" class="md-loading-bar"></div>

    <div class="md-viewport">
      <nav class="md-controls">
        <button class="back-btn" @click="$emit('close')">
          <span class="arrow">←</span> INDEX
        </button>
        <div class="doc-type">FRAGMENT / ARTWORK</div>
      </nav>

      <div class="md-wrapper" v-if="artwork">
        <section class="md-visual">
          <div class="image-container">
            <img 
              v-if="artwork.images && artwork.images.length > 0"
              :src="artwork.images[0]" 
              class="main-img" 
              @load="loading = false" 
            />
            <div v-else class="img-placeholder">灵气汇聚中...</div>
          </div>
        </section>

        <article class="md-article">
          <header class="article-header">
            <h1 class="title">{{ artwork.title }}</h1>
            <div class="metadata">
              <div class="author-block">
                <img :src="artwork.author?.avatar || '/default-avatar.png'" class="mini-avatar" />
                <span class="name">{{ artwork.author?.username || '无名漫游者' }}</span>
              </div>
              <span class="divider">/</span>
              <span class="date">{{ artwork.uploadAt }}</span>
            </div>
          </header>

          <section class="article-body">
            <div class="text-content">
              {{ artwork.description || '这一卷画作，画师未曾留下文字描述。' }}
            </div>
          </section>

          <footer class="article-footer">
            <div class="interaction-wrap">
              <InteractActions 
                v-if="artwork.id"
                :target-id="artwork.id" 
                target-type="Artwork" 
                :initial-stats="{ likesCount: 0 }" 
              />
            </div>
            <p class="end-mark">END OF FRAGMENT</p>
          </footer>
        </article>
      </div>
    </div>

    <button class="floating-close" @click="$emit('close')">✕</button>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import { artworkApi, type ArtworkDetail } from '../../api/artwork'; // 使用封装好的接口
import InteractActions from '../../components/InteractActions.vue';

const props = defineProps<{
  id: string | number;
}>();

const emit = defineEmits(['close']);

const artwork = ref<ArtworkDetail | null>(null);
const loading = ref(true);

onMounted(async () => {
  // 1. 锁定底层的瀑布流背景滚动，增加阅读沉浸感
  document.body.style.overflow = 'hidden';
  
  try {
    // 2. 使用 API 模块获取数据（已自动处理 baseURL 路径问题）
    const res = await artworkApi.getDetail(Number(props.id));
    artwork.value = res as any;
  } catch (error) {
    console.error('获取灵脉详情失败:', error);
  } finally {
    // 若无图片加载，则直接关闭 loading
    if (!artwork.value?.images?.length) {
      loading.value = false;
    }
  }
});

onUnmounted(() => {
  // 3. 销毁组件时释放滚动条
  document.body.style.overflow = '';
});
</script>

<style scoped>
/* 极致留白布局 */
.md-detail-mask {
  position: fixed;
  inset: 0;
  z-index: 9999;
  background: #ffffff;
  overflow-y: auto;
  scrollbar-width: none; /* 隐藏原生滚动条 */
}
.md-detail-mask::-webkit-scrollbar { display: none; }

.md-viewport {
  width: 100%;
  max-width: 1000px; /* 限制阅读宽度 */
  margin: 0 auto;
  padding: 0 40px;
  position: relative;
}

/* 顶部控制栏 */
.md-controls {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 40px 0;
}
.back-btn {
  background: none;
  border: none;
  font-size: 13px;
  font-weight: 600;
  letter-spacing: 0.1em;
  color: #1a1a1a;
  cursor: pointer;
}
.doc-type {
  font-size: 11px;
  color: #86868b;
  letter-spacing: 0.2em;
}

/* 画作区 */
.md-visual {
  margin-bottom: 80px;
  display: flex;
  justify-content: center;
}
.image-container {
  width: 100%;
  background: #fbfbfb;
  display: flex;
  justify-content: center;
}
.main-img {
  max-width: 100%;
  height: auto;
  box-shadow: 0 40px 100px rgba(0,0,0,0.06); /* 柔和深邃投影 */
}

/* MD 风格排版区 */
.article-header {
  margin-bottom: 60px;
}
.title {
  font-size: 3.5rem;
  font-weight: 800;
  letter-spacing: -0.04em;
  line-height: 1.1;
  margin-bottom: 24px;
  color: #1a1a1a;
}
.metadata {
  display: flex;
  align-items: center;
  gap: 16px;
  color: #86868b;
  font-size: 14px;
}
.author-block {
  display: flex;
  align-items: center;
  gap: 8px;
}
.mini-avatar {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  filter: grayscale(1);
}

.article-body {
  font-size: 1.25rem;
  line-height: 2.2; /* 极致宽阔的行高 */
  color: #333333;
  text-align: justify;
  margin-bottom: 60px;
  white-space: pre-wrap;
}

/* 页脚 */
.article-footer {
  margin-top: 120px;
  padding-bottom: 100px;
  border-top: 1px solid #f2f2f2;
  padding-top: 60px;
  text-align: center;
}
.end-mark {
  margin-top: 60px;
  font-size: 10px;
  letter-spacing: 0.4em;
  color: #d2d2d7;
}

/* 顶部进度条动画 */
.md-loading-bar {
  position: fixed;
  top: 0;
  left: 0;
  height: 2px;
  background: #000;
  width: 100%;
  z-index: 10000;
  animation: loading-flow 2s infinite linear;
}
@keyframes loading-flow {
  0% { transform: translateX(-100%); }
  100% { transform: translateX(100%); }
}

.floating-close {
  position: fixed;
  top: 40px;
  right: 40px;
  width: 40px;
  height: 40px;
  border: none;
  background: none;
  font-size: 20px;
  cursor: pointer;
  opacity: 0.2;
  transition: opacity 0.3s;
}
.floating-close:hover { opacity: 1; }

/* 移动端适配 */
@media (max-width: 768px) {
  .md-viewport { padding: 0 24px; }
  .title { font-size: 2.2rem; }
  .article-body { font-size: 1.1rem; line-height: 1.8; }
  .floating-close { display: none; }
}
</style>