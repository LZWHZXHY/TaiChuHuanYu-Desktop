<template>
  <div class="data-panel-wrapper">
    <div v-if="loading" class="panel-loading">
      <div class="skeleton-item" v-for="i in 4" :key="i"></div>
    </div>
    
    <div v-else class="data-grid">
      <div class="data-card" v-for="item in statsItems" :key="item.label">
        <div class="card-inner">
          <span class="data-label">{{ item.label }}</span>
          <div class="data-value-wrapper">
            <span class="data-value">{{ item.value }}</span>
            <span v-if="item.unit" class="data-unit">{{ item.unit }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
// 假设你未来会有一个全局或具体的统计API，这里先模拟
// import { statsApi } from '@/api/stats';

const loading = ref(false);
const rawData = ref({
  userCount: 0,
  workCount: 0,
  blogCount: 0,
  postCount: 0
});

// 格式化展示数据流
const statsItems = computed(() => [
  { label: '探索行者', value: rawData.value.userCount.toLocaleString(), unit: '位' },
  { label: '寰宇作品', value: rawData.value.workCount.toLocaleString(), unit: '件' },
  { label: '太初方志 (博客)', value: rawData.value.blogCount.toLocaleString(), unit: '篇' },
  { label: '众鸣心声 (帖子)', value: rawData.value.postCount.toLocaleString(), unit: '条' }
]);

const fetchStats = async () => {
  loading.value = true;
  try {
    // 模拟从后端 API 获取真实数据
    // const res = await statsApi.getOverview();
    // rawData.value = res;
    
    // 暂时的 Mock 数据
    await new Promise(resolve => setTimeout(resolve, 600));
    rawData.value = {
      userCount: 12450,
      workCount: 842,
      blogCount: 156,
      postCount: 3891
    };
  } catch (error) {
    console.error('获取统计数据失败', error);
  } finally {
    loading.value = false;
  }
};

onMounted(fetchStats);
</script>

<style scoped>
/* 修改 DataPanel.vue 中的样式部分 */
.data-panel-wrapper {
  /* 移除 margin-bottom，交给父级布局控制 */
}

/* 默认在右侧边栏（或者窄屏下）显示为 2行2列 */
.data-grid, .panel-loading {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 12px;
}

.data-card {
  background: #f6f8fa;
  border: 1px solid #e1e4e8;
  border-radius: 6px;
  padding: 16px 12px; /* 适当缩小内边距 */
  transition: all 0.3s cubic-bezier(0.16, 1, 0.3, 1);
}

.data-card:hover {
  background: #fff;
  border-color: #1f2328;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.03);
  transform: translateY(-2px);
}

.card-inner {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.data-label {
  font-size: 0.75rem; /* 稍微精简字体 */
  color: #6e7781;
  font-weight: 500;
  letter-spacing: 0.05em;
}

.data-value-wrapper {
  display: flex;
  align-items: baseline;
  gap: 4px;
}

.data-value {
  font-size: 1.4rem; /* 降低字号以适应 400px 的侧边栏 */
  font-weight: 300;
  color: #1f2328;
  font-family: ui-monospace, SFMono-Regular, "SF Mono", Menlo, Consolas, monospace;
  line-height: 1;
}

.data-unit {
  font-size: 0.7rem;
  color: #8c959f;
}

/* 骨架屏 */
.skeleton-item {
  height: 70px;
  background: linear-gradient(90deg, #f6f8fa 25%, #eaecef 37%, #f6f8fa 63%);
  background-size: 400% 100%;
  animation: skeleton-loading 1.4s ease infinite;
  border-radius: 6px;
}

@keyframes skeleton-loading {
  0% { background-position: 100% 50%; }
  100% { background-position: 0% 50%; }
}

/* 响应式：在极小屏幕（手机）下切换为单列 */
@media (max-width: 480px) {
  .data-grid, .panel-loading {
    grid-template-columns: 1fr;
  }
}
</style>