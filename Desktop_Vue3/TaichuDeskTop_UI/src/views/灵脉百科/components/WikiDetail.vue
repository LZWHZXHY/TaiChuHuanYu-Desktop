<template>
  <div class="wiki-detail-page">
    <header class="detail-header">
      <button class="back-btn" @click="router.back()">← 返回百科</button>
    </header>

    <main class="wiki-content-container" v-if="entry">
      <h1 class="wiki-title">{{ entry.title }}</h1>
      
      <div class="wiki-meta-info">
        <span class="author">作者：{{ entry.authorName || '匿名编织者' }}</span>
        <span class="sep">|</span>
        <span class="date">发布于：{{ formatDate(entry.publishedAt) }}</span>
      </div>

      <hr class="title-sep" />

      <SpiritPreview :modelValue="entry.content" class="full-content" />
      
      <div class="wiki-tags" v-if="tags.length">
        <span v-for="tag in tags" :key="tag" class="tag">#{{ tag }}</span>
      </div>
    </main>

    <div v-else class="loading-state">
      <div class="loading-spinner"></div>
      正在共鸣灵脉碎片...
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
// 🌟 1. 使用新的路径别名和 API 模块
import { wikiApi } from '@/api/Wiki'; 
import SpiritPreview from '@/components/SpiritTextComponents/SpiritPreview.vue';

const route = useRoute();
const router = useRouter();
const entry = ref<any>(null);

// 解析标签（后端存的是逗号分隔的字符串）
const tags = computed(() => {
  if (!entry.value?.tags) return [];
  return typeof entry.value.tags === 'string' 
    ? entry.value.tags.split(',') 
    : entry.value.tags;
});

/**
 * 🌟 2. 核心逻辑：感应词条完整详情
 */
const loadDetail = async () => {
  const id = route.params.id as string;
  try {
    // 🌟 使用 wikiApi 的详情接口
    // 因为你的 request.ts 已经处理了 response.data，这里直接拿到的是对象
    const res: any = await wikiApi.getArticleDetail(id);
    entry.value = res;
  } catch (err) {
    console.error('词条读取失败:', err);
  }
};

const formatDate = (d: string) => d ? d.substring(0, 10).replace(/-/g, '.') : '';

onMounted(loadDetail);
</script>

<style scoped>
/* 🌟 3. 使用路径别名引入样式 */
@import "@/components/SpiritTextComponents/spirit-typography.css";

.wiki-detail-page { 
  max-width: 800px; 
  margin: 0 auto; 
  padding: 80px 20px; 
  min-height: 100vh;
}

.detail-header { margin-bottom: 60px; }
.back-btn { 
  background: none; 
  border: none; 
  color: #86868b; 
  cursor: pointer; 
  font-size: 14px; 
  padding: 0;
  transition: color 0.2s;
}
.back-btn:hover { color: #0066cc; }

.wiki-title { 
  font-size: 3.5rem; 
  font-weight: 800; 
  margin-bottom: 24px; 
  color: #1d1d1f; 
  letter-spacing: -0.03em;
  line-height: 1.1;
}

.wiki-meta-info { 
  font-size: 13px; 
  color: #86868b; 
  display: flex; 
  gap: 12px; 
  margin-bottom: 48px; 
}

.title-sep { 
  border: none; 
  border-top: 1px solid #f2f2f7; 
  margin-bottom: 48px; 
}

.full-content { 
  min-height: 400px; 
  /* 详情页文字稍微放大一点，增加阅读舒适度 */
  font-size: 1.2rem; 
}

.wiki-tags { 
  margin-top: 80px; 
  padding-top: 40px;
  border-top: 1px solid #f2f2f7;
  display: flex; 
  gap: 12px; 
}

.tag { 
  font-size: 12px; 
  color: #0066cc; 
  background: rgba(0, 102, 204, 0.05); 
  padding: 6px 16px; 
  border-radius: 40px; 
}

.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 60vh;
  color: #86868b;
  font-size: 15px;
}
</style>