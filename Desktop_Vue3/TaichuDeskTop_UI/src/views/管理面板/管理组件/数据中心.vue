<template>
  <div class="data-center-panel fade-in">
    <div class="panel-header">
      <div class="header-title">
        <h2>社区数据</h2>
        <span class="subtitle">注册用户与近期活跃情况</span>
      </div>
      <button class="btn-primary" :disabled="loading" @click="fetchMetrics">{{ loading ? '刷新中…' : '刷新指标' }}</button>
    </div>

    <!-- 核心指标卡片 (四宫格) -->
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="loading && !loaded" role="status">正在读取社区数据…</p>
    <div v-if="loaded" class="metrics-grid">
      <!-- 1. 总注册道友 -->
      <div class="metric-card">
        <div class="card-top">
          <span class="metric-name">总注册用户  (Total Users)</span>
          <span class="metric-icon">👥</span>
        </div>
        <div class="metric-value">{{ metrics.totalUsers.value.toLocaleString() }}</div>
        <div class="metric-trend" :class="metrics.totalUsers.isUp ? 'up' : 'down'">
          <span class="trend-arrow">{{ metrics.totalUsers.isUp ? '↑' : '↓' }}</span> 
          {{ metrics.totalUsers.trendValue }} 本周新增
        </div>
      </div>

      <!-- 2. 月度活跃 -->
      <div class="metric-card">
        <div class="card-top">
          <span class="metric-name">月度活跃 (MAU)</span>
          <span class="metric-icon">🌕</span>
        </div>
        <div class="metric-value">{{ metrics.mau.value.toLocaleString() }}</div>
        <div class="metric-trend" :class="metrics.mau.isUp ? 'up' : 'down'">
          <span class="trend-arrow">{{ metrics.mau.isUp ? '↑' : '↓' }}</span> 
          {{ metrics.mau.trendPercent }}% 较上月
        </div>
      </div>

      <!-- 3. 周度活跃 -->
      <div class="metric-card">
        <div class="card-top">
          <span class="metric-name">周度活跃 (WAU)</span>
          <span class="metric-icon">🌓</span>
        </div>
        <div class="metric-value">{{ metrics.wau.value.toLocaleString() }}</div>
        <div class="metric-trend" :class="metrics.wau.isUp ? 'up' : 'down'">
          <span class="trend-arrow">{{ metrics.wau.isUp ? '↑' : '↓' }}</span> 
          {{ metrics.wau.trendPercent }}% 较上周
        </div>
      </div>

      <!-- 4. 今日留痕 -->
      <div class="metric-card">
        <div class="card-top">
          <span class="metric-name">今日留痕 (DAU)</span>
          <span class="metric-icon">🌑</span>
        </div>
        <div class="metric-value">{{ metrics.dau.value.toLocaleString() }}</div>
        <div class="metric-trend" :class="metrics.dau.isUp ? 'up' : 'down'">
          <span class="trend-arrow">{{ metrics.dau.isUp ? '↑' : '↓' }}</span> 
          {{ metrics.dau.trendPercent }}% 较昨日
        </div>
      </div>
    </div>

  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import request from '@/utils/request'; // 确保 request.ts 的路径正确

const loading = ref(false), loaded = ref(false), error = ref('');
const metrics = ref({
  totalUsers: { value: 0, trendValue: 0, isUp: true },
  dau: { value: 0, trendPercent: 0, isUp: true },
  wau: { value: 0, trendPercent: 0, isUp: true },
  mau: { value: 0, trendPercent: 0, isUp: true }
});

// 从后端获取真实指标数据
const fetchMetrics = async () => {
  if (loading.value) return;
  loading.value = true; error.value = '';
  try {
    const res: any = await request.get('/Admin/Data/Metrics');
    if (res) {
      metrics.value = res; loaded.value = true;
    }
  } catch {
    error.value = '指标读取失败，请重试。';
  } finally { loading.value = false; }
};

onMounted(fetchMetrics);
</script>

<style scoped>
.data-center-panel { display: grid; gap: 24px; color: var(--text-primary); }
.panel-header { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 20px; padding: 24px; background: var(--paper-card); border: 1px solid var(--border-line); border-radius: var(--radius-md); }
h2 { margin: 0 0 8px; font-size: 24px; }
.subtitle { color: var(--text-secondary); font-size: 14px; }
.btn-primary { background: var(--accent-color); color: var(--on-accent); border: 0; border-radius: var(--radius-sm); padding: 10px 16px; cursor: pointer; }
.metrics-grid { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 20px; }
.metric-card { background: var(--paper-card); border: 1px solid var(--border-line); border-radius: var(--radius-md); padding: 24px; }
.card-top { display: flex; justify-content: space-between; gap: 12px; color: var(--text-secondary); font-size: 14px; }
.metric-value { font-size: 36px; font-weight: 650; margin: 20px 0 12px; }
.metric-trend { font-size: 13px; color: var(--text-secondary); }
.metric-trend.up { color: var(--success); }
.metric-trend.down { color: var(--danger); }
@media(max-width:600px) { .metrics-grid { grid-template-columns: 1fr; } }
</style>
