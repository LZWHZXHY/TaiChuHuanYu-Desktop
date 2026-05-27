<template>
  <article class="wiki-minimalist-page" v-if="entry">
    <header class="header">
      <div class="header-actions">
        <button class="back-btn" @click="router.back()">← 返回</button>
        <button class="edit-btn" @click="handleEditRequest">申请编辑</button>
      </div>

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
import SpiritPreview from '@/components/SpiritTextComponents/SpiritPreview.vue';

const route = useRoute();
const router = useRouter();
const entry = ref<any>(null);

const tags = computed(() => {
  if (!entry.value?.tags) return [];
  return typeof entry.value.tags === 'string' ? entry.value.tags.split(',') : entry.value.tags;
});

// 🌟 这里是修改的关键
const handleEditRequest = () => {
  router.push({
    path: '/lingmai', // 确保这个路径指向你的 index.vue (编辑器)
    query: { 
      id: route.params.id as string, 
      mode: 'wiki' 
    }
  });
};

const loadDetail = async () => {
  try {
    const res = await wikiApi.getArticleDetail(route.params.id as string);
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
/* 样式保持不变 */
.wiki-minimalist-page { max-width: 700px; margin: 100px auto; padding: 0 20px; color: #1a1a1a; line-height: 1.7; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; }
.header { margin-bottom: 40px; }
.header-actions { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
.back-btn { background: none; border: none; font-size: 16px; cursor: pointer; padding: 0; color: #999; }
.back-btn:hover { color: #000; }
.edit-btn { background: none; border: 1px solid #eee; padding: 6px 14px; font-size: 0.85rem; cursor: pointer; color: #333; border-radius: 4px; transition: all 0.2s; }
.edit-btn:hover { background: #f0f0f0; border-color: #ccc; }
.title { font-size: 2.5rem; font-weight: 700; margin: 0 0 16px; letter-spacing: -0.02em; line-height: 1.2; }
.meta { font-size: 0.9rem; color: #86868b; }
.content { font-size: 1.15rem; margin-bottom: 80px; }
.footer { border-top: 1px solid #f0f0f0; padding-top: 24px; font-size: 0.85rem; color: #86868b; }
.loading-state { text-align: center; margin-top: 200px; color: #999; }
</style>