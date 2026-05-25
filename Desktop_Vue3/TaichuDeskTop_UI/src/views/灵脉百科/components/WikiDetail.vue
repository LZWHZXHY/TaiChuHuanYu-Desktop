<template>
  <article class="wiki-minimalist-page" v-if="entry">
    <header class="header">
      <button class="back-btn" @click="router.back()">←</button>
      <h1 class="title">{{ entry.title }}</h1>
      <div class="meta">
        <span>{{ formatDate(entry.publishedAt) }}</span>
        <span v-if="tags.length">· {{ tags.join(', ') }}</span>
      </div>
    </header>

    <section class="content">
      <SpiritPreview v-if="entry.content" :modelValue="entry.content" />
    </section>

    <footer class="footer" v-if="entry.contributors?.length > 0">
      <div class="contributors">
        <span class="label">编织者:</span>
        <span v-for="c in entry.contributors" :key="c" class="c-item">{{ c }}</span>
      </div>
    </footer>
  </article>

  <div v-else class="loading-state">正在载入灵脉碎片...</div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { wikiApi } from '@/api/Wiki'; 
// 🌟 确保路径正确：如果依然报错，请检查文件实际目录是否为 src/components/SpiritTextComponents/SpiritPreview.vue
import SpiritPreview from '@/components/SpiritTextComponents/SpiritPreview.vue';

const route = useRoute();
const router = useRouter();
const entry = ref<any>(null);

const tags = computed(() => {
  if (!entry.value?.tags) return [];
  return typeof entry.value.tags === 'string' ? entry.value.tags.split(',') : entry.value.tags;
});

const loadDetail = async () => {
  try {
    const res = await wikiApi.getArticleDetail(route.params.id as string);
    
    // 🌟 关键修复：API 返回的 content 是字符串，必须手动解析为对象供组件使用
    if (res.content && typeof res.content === 'string') {
      try {
        res.content = JSON.parse(res.content);
      } catch (e) {
        console.error("Content 解析异常:", e);
      }
    }
    entry.value = res;
  } catch (err) {
    console.error('词条读取失败:', err);
  }
};

const formatDate = (d: string) => d ? d.substring(0, 10).replace(/-/g, '/') : '';

onMounted(loadDetail);
</script>

<style scoped>
/* 极简风格：去掉了所有装饰性盒子和线条 */
.wiki-minimalist-page { 
  max-width: 700px; /* 窄版阅读宽度，MD文档感 */
  margin: 100px auto; 
  padding: 0 20px;
  color: #1a1a1a;
  line-height: 1.7;
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Oxygen, Ubuntu, Cantarell, "Open Sans", "Helvetica Neue", sans-serif;
}

.header { margin-bottom: 40px; }
.back-btn { 
  background: none; border: none; font-size: 16px; cursor: pointer; 
  padding: 0; margin-bottom: 24px; color: #999;
}
.back-btn:hover { color: #000; }

.title { 
  font-size: 2.5rem; font-weight: 700; margin: 0 0 16px; 
  letter-spacing: -0.02em; line-height: 1.2;
}

.meta { font-size: 0.9rem; color: #86868b; }

.content { 
  font-size: 1.15rem; /* 适当放大，阅读更舒适 */
  margin-bottom: 80px;
}

.footer { 
  border-top: 1px solid #f0f0f0; 
  padding-top: 24px;
  font-size: 0.85rem; color: #86868b;
}

.contributors { display: flex; gap: 10px; }
.loading-state { text-align: center; margin-top: 200px; color: #999; }
</style>