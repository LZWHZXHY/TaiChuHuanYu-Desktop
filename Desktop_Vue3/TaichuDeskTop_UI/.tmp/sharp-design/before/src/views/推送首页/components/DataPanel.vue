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
  { label: '成员数量', value: rawData.value.userCount.toLocaleString(), unit: '位' },
  { label: '画廊作品', value: rawData.value.workCount.toLocaleString(), unit: '幅' },
  { label: '灵脉项目', value: rawData.value.projectCount.toLocaleString(), unit: '个' },
  { label: '太初博客', value: rawData.value.blogCount.toLocaleString(), unit: '篇' },
  { label: '闲聊碎帖', value: rawData.value.postCount.toLocaleString(), unit: '条' },
  { label: '知识百科', value: rawData.value.wikiCount.toLocaleString(), unit: '条' }
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
.data-panel-wrapper { width: 100%; }
.data-grid, .panel-loading { display: grid; grid-template-columns: repeat(2, minmax(0,1fr)); gap: 10px; }
.data-card { --stat-color: var(--signal-mint); position: relative; overflow: hidden; padding: 16px 12px; border: 1px solid color-mix(in srgb, var(--stat-color) 25%, var(--border-line)); border-radius: var(--radius-sm); background: linear-gradient(135deg, color-mix(in srgb, var(--stat-color) 5%, var(--paper-card)), color-mix(in srgb, var(--stat-color) 18%, var(--paper-card))); background-size: 170% 170%; transition: background-position .6s, border-color .3s; }
.data-card:nth-child(2) { --stat-color: #8595f5; }.data-card:nth-child(3) { --stat-color: var(--signal-mint); }.data-card:nth-child(4) { --stat-color: #c79b52; }.data-card:nth-child(5) { --stat-color: #5aade7; }.data-card:nth-child(6) { --stat-color: #9e8dcc; }
.data-card::before { content: ''; position: absolute; inset: 0 0 auto; height: 2px; background: linear-gradient(90deg, var(--stat-color), transparent); }
.data-card:hover { background-position: 100% 100%; border-color: color-mix(in srgb, var(--stat-color) 55%, var(--border-line)); }
.card-inner { display: flex; flex-direction: column; gap: 12px; }.data-label { font-size: 11px; color: var(--text-secondary); font-weight: 500; white-space: nowrap; }.prefix { color: var(--accent-color); font-weight: 700; margin-right: 2px; }
.data-value-wrapper { display: flex; align-items: baseline; gap: 5px; }.data-value { font-size: 28px; font-weight: 600; color: var(--text-primary); font-family: var(--font-mono); line-height: 1; letter-spacing: -.05em; }.data-unit { font-size: 11px; color: var(--text-secondary); }
.skeleton-item { height: 92px; background: linear-gradient(90deg, var(--bg-sub) 25%, var(--border-line) 37%, var(--bg-sub) 63%); background-size: 400% 100%; border-radius: var(--radius-sm); border: 1px solid var(--border-line); }
@media(prefers-reduced-motion: no-preference) { .skeleton-item { animation: skeleton-loading 1.4s ease infinite; } }
@keyframes skeleton-loading { from { background-position: 100% 50%; } to { background-position: 0% 50%; } }
@media(prefers-reduced-motion: reduce) { .data-card { transition: none; } }
@media(max-width: 340px) { .data-grid, .panel-loading { grid-template-columns: 1fr; } }
</style>
