<template>
  <div class="data-panel-wrapper">
    <div v-if="loading" class="panel-loading">
      <div class="skeleton-item" v-for="i in 6" :key="'sk-'+i"></div>
    </div>
    
    <div v-else class="data-grid">
      <div class="data-card" v-for="item in statsItems" :key="item.label">
        <div class="card-inner">
          <span class="data-label">
            <span class="prefix">//</span> {{ item.label }}
          </span>
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
import request from '@/utils/request'; 

// 🌟 接口扩展：全面接轨后端 DataPanelController 传输对象
interface StatsOverviewDto {
  userCount: number;
  workCount: number;
  projectCount: number;
  blogCount: number;
  postCount: number;
  wikiCount: number;
}

const loading = ref(false);
const rawData = ref<StatsOverviewDto>({
  userCount: 0,
  workCount: 0,
  projectCount: 0,
  blogCount: 0,
  postCount: 0,
  wikiCount: 0
});

// 格式化展示数据流：整合 6 个核心指标
const statsItems = computed(() => [
  { label: 'EXPLORER // 太初行者', value: rawData.value.userCount.toLocaleString(), unit: '位' },
  { label: 'CREATIONS // 寰宇作品', value: rawData.value.workCount.toLocaleString(), unit: '幅' },
  { label: 'PROJECTS // 灵脉项目', value: rawData.value.projectCount.toLocaleString(), unit: '个' },
  { label: 'CHRONICLES // 太初博客', value: rawData.value.blogCount.toLocaleString(), unit: '篇' },
  { label: 'ECHOES // 闲聊碎帖', value: rawData.value.postCount.toLocaleString(), unit: '条' },
  { label: 'WIKI // 知识百科', value: rawData.value.wikiCount.toLocaleString(), unit: '条' }
]);

const fetchStats = async () => {
  loading.value = true;
  try {
    // 🌟 请求后端同步更新的 overview 路由
    const res = await request.get<StatsOverviewDto>('/DataPanel/overview');
    if (res) {
      rawData.value = res;
    }
  } catch (error) {
    console.error('获取寰宇综合灵脉指标失败:', error);
  } finally {
    loading.value = false;
  }
};

onMounted(fetchStats);
</script>

<style scoped>
.data-panel-wrapper {
  width: 100%;
}

/* 侧边栏完美适配的网格，这里保持 2 列，保证卡片宽度适中 */
.data-grid, .panel-loading {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 12px;
}

.data-card {
  background: #f6f8fa;
  border: 1px solid #e1e4e8;
  border-radius: 6px;
  padding: 14px 12px;
  transition: all 0.3s cubic-bezier(0.16, 1, 0.3, 1);
  position: relative;
  overflow: hidden;
}

.data-card:hover {
  background: #ffffff;
  border-color: #1f2328;
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.03);
  transform: translateY(-2px);
}

.card-inner {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.data-label {
  font-size: 0.65rem; /* 针对 6 个卡片，微调字号防止折行 */
  color: #8c959f;
  font-weight: 600;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  white-space: nowrap;
}
.prefix {
  color: #6e7781;
  font-weight: bold;
}
.data-card:hover .prefix {
  color: #1f2328;
}

.data-value-wrapper {
  display: flex;
  align-items: baseline;
  gap: 3px;
}

.data-value {
  font-size: 1.25rem; /* 适应侧边栏密集布局 */
  font-weight: 400;
  color: #1f2328;
  font-family: 'JetBrains Mono', ui-monospace, monospace;
  line-height: 1;
  letter-spacing: -0.02em;
}

.data-unit {
  font-size: 0.65rem;
  color: #8c959f;
  font-weight: 500;
}

.skeleton-item {
  height: 60px;
  background: linear-gradient(90deg, #f6f8fa 25%, #eaecef 37%, #f6f8fa 63%);
  background-size: 400% 100%;
  animation: skeleton-loading 1.4s ease infinite;
  border-radius: 6px;
  border: 1px solid #e1e4e8;
}

@keyframes skeleton-loading {
  0% { background-position: 100% 50%; }
  100% { background-position: 0% 50%; }
}

@media (max-width: 480px) {
  .data-grid, .panel-loading {
    grid-template-columns: 1fr; /* 极小屏单列 */
  }
}
</style>