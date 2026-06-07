<template>
  <div class="table-card">
    <table class="ink-table">
      <thead>
        <tr>
          <th>标题</th>
          <th>状态</th>
          <th>最后更新</th>
          <th class="text-right">操作</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="art in processedArticles" :key="art.id">
          <td>
            <div style="font-weight: 500;">{{ art.title }}</div>
            <div style="font-size: 0.7rem; color: #888;">
              分类ID: {{ art.categoryId === 0 ? '⚠️ 未设置' : art.categoryId }}
            </div>
          </td>
          <td>
            <span :class="getStatusClass(art)">
              {{ isDeleted(art) ? '已下架' : '公开中' }}
            </span>
          </td>
          <td class="text-gray">{{ new Date(art.updatedAt).toLocaleDateString() }}</td>
          <td class="text-right actions">
            <button class="btn-s" @click="handleToggle(art)" :disabled="loading">
              {{ isDeleted(art) ? '[恢复]' : '[下架]' }}
            </button>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';
import { wikiReviewApi } from '@/api/Admin'; // 确保引入正确的 API

// 接收父组件传入的数据
const props = defineProps<{ data: any[] }>();
// 仅通知父组件刷新列表
const emit = defineEmits(['refresh']); 

const loading = ref(false);

// 1. 数据清洗：去重、过滤脏数据、保持更新时间最新
const processedArticles = computed(() => {
  if (!props.data || props.data.length === 0) return [];
  
  const map = new Map();
  
  // 先按更新时间从新到旧排序
  const sortedData = [...props.data].sort((a, b) => 
    new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime()
  );

  sortedData.forEach(art => {
    // 过滤掉标题为空的项，且通过 map 保证每个标题只保留最新的一条
    if (art.title && !map.has(art.title)) {
      map.set(art.title, art);
    }
  });

  return Array.from(map.values());
});

// 2. 下架/恢复处理逻辑
const handleToggle = async (art: any) => {
  if (loading.value) return;
  loading.value = true;
  
  try {
    // 调用修正后的 API 接口
    // 确保你的 Admin.ts 中 wikiReviewApi.toggleArticleArchive 已添加 /api 前缀
    await wikiReviewApi.toggleArticleArchive(art.id);
    
    // 操作成功，触发父组件刷新数据
    emit('refresh'); 
  } catch (err) {
    console.error("切换状态失败:", err);
    alert("操作失败，请检查网络权限");
  } finally {
    loading.value = false;
  }
};

// 3. 健壮的属性访问：兼容后端序列化的大小写差异
const isDeleted = (art: any) => {
  return art.isDeleted === true || art.IsDeleted === true;
};

// 状态样式计算
const getStatusClass = (art: any) => {
  return isDeleted(art) ? 'status-archived' : 'status-live';
};
</script>

<style>
@import './Wiki子组件风格.css';
</style>