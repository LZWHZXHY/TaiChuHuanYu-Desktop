<template>
  <div class="data-center-panel fade-in">
    <div class="panel-header">
      <div class="header-title">
        <h2>太初气象局 (数据中枢)</h2>
        <span class="subtitle">全局掌控社区的潮汐涨落与生态繁荣度</span>
      </div>
      <div class="header-actions">
        <select class="time-filter ink-input">
          <option value="7">最近 7 天</option>
          <option value="30">最近 30 天</option>
          <option value="90">本季度</option>
        </select>
        <button class="btn-primary">导出报表</button>
      </div>
    </div>

    <!-- 核心指标卡片 (四宫格) -->
    <div class="metrics-grid">
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

    <!-- 图表区域 (左右分栏) -->
    <div class="charts-grid">
      <div class="chart-box main-chart">
        <div class="box-header">
          <h3>活跃趋势图</h3>
          <span class="box-tag">每日登录人次</span>
        </div>
        <!-- 未来这里将挂载 ECharts 实例 -->
        <div class="chart-placeholder">
          <div class="mock-line-chart"></div>
          <p>趋势折线图渲染区 (待接入 ECharts)</p>
        </div>
      </div>

      <div class="chart-box side-chart">
        <div class="box-header">
          <h3>内容产出漏斗</h3>
          <span class="box-tag">转化率</span>
        </div>
        <!-- 未来这里将挂载 ECharts 实例 -->
        <div class="chart-placeholder">
          <div class="mock-funnel-chart"></div>
          <p>转化漏斗渲染区 (待接入 ECharts)</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import request from '@/utils/request'; // 确保 request.ts 的路径正确

// 初始化响应式数据结构，防止初始渲染报错
const metrics = ref({
  totalUsers: { value: 0, trendValue: 0, isUp: true },
  dau: { value: 0, trendPercent: 0, isUp: true },
  wau: { value: 0, trendPercent: 0, isUp: true },
  mau: { value: 0, trendPercent: 0, isUp: true }
});

// 从后端获取真实指标数据
const fetchMetrics = async () => {
  try {
    const res: any = await request.get('/Admin/Data/Metrics');
    if (res) {
      metrics.value = res; // 假设 axios 拦截器已经处理了 res.data
    }
  } catch (error) {
    console.error('获取数据中枢指标失败:', error);
  }
};

// 未来可在这里初始化 ECharts (echarts.init)
onMounted(() => {
  fetchMetrics();
  console.log('数据中心已加载，图表区待接入 ECharts...');
});
</script>

<style scoped>
.data-center-panel {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  background: #fff;
  padding: 24px 32px;
  border-radius: 12px;
  border: 1px solid #f0f0f0;
  box-shadow: 0 2px 10px rgba(0,0,0,0.02);
}

.header-title h2 {
  font-size: 1.4rem;
  font-weight: 600;
  color: #111;
  margin: 0 0 6px 0;
}

.header-title .subtitle {
  font-size: 0.85rem;
  color: #888;
}

.header-actions {
  display: flex;
  gap: 12px;
}

.ink-input {
  padding: 8px 16px;
  border: 1px solid #ddd;
  border-radius: 6px;
  font-size: 0.9rem;
  background: #fafafa;
  cursor: pointer;
  outline: none;
}

.btn-primary {
  background: #111;
  color: #fff;
  border: none;
  padding: 8px 20px;
  border-radius: 6px;
  font-size: 0.9rem;
  font-weight: 600;
  cursor: pointer;
  transition: opacity 0.3s;
}

.btn-primary:hover {
  opacity: 0.8;
}

/* 核心指标四宫格 */
.metrics-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 20px;
}

.metric-card {
  background: #fff;
  padding: 24px;
  border-radius: 12px;
  border: 1px solid #f0f0f0;
  box-shadow: 0 2px 10px rgba(0,0,0,0.02);
  display: flex;
  flex-direction: column;
  gap: 12px;
  transition: transform 0.3s;
}

.metric-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 6px 20px rgba(0,0,0,0.04);
}

.card-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.metric-name {
  font-size: 0.85rem;
  color: #666;
  font-weight: 500;
}

.metric-icon {
  font-size: 1.2rem;
  opacity: 0.8;
}

.metric-value {
  font-size: 2rem;
  font-weight: 700;
  color: #111;
  font-family: monospace;
}

.metric-trend {
  font-size: 0.8rem;
  font-weight: 500;
  display: flex;
  align-items: center;
  gap: 4px;
}

.metric-trend.up {
  color: #15803d;
}

.metric-trend.down {
  color: #b91c1c;
}

/* 图表区域 */
.charts-grid {
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: 20px;
}

.chart-box {
  background: #fff;
  padding: 24px;
  border-radius: 12px;
  border: 1px solid #f0f0f0;
  box-shadow: 0 2px 10px rgba(0,0,0,0.02);
  display: flex;
  flex-direction: column;
}

.box-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.box-header h3 {
  font-size: 1.1rem;
  font-weight: 600;
  margin: 0;
  color: #111;
}

.box-tag {
  font-size: 0.75rem;
  background: #f6f8fa;
  padding: 4px 10px;
  border-radius: 20px;
  color: #666;
}

.chart-placeholder {
  flex: 1;
  min-height: 300px;
  background: #fafbfc;
  border: 1px dashed #eaeef2;
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  color: #999;
  font-size: 0.9rem;
}

/* 骨架屏图形占位 */
.mock-line-chart {
  width: 80%;
  height: 2px;
  background: #24292f;
  position: relative;
  margin-bottom: 20px;
  opacity: 0.2;
}
.mock-line-chart::before {
  content: ''; position: absolute; top: -20px; left: 20%; width: 2px; height: 40px; background: #24292f; transform: rotate(45deg);
}
.mock-line-chart::after {
  content: ''; position: absolute; top: 0px; right: 20%; width: 2px; height: 40px; background: #24292f; transform: rotate(-30deg);
}

.mock-funnel-chart {
  width: 0;
  height: 0;
  border-left: 40px solid transparent;
  border-right: 40px solid transparent;
  border-top: 60px solid #eaeef2;
  margin-bottom: 20px;
}

.fade-in {
  animation: fadeIn 0.4s ease;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: translateY(0); }
}

@media (max-width: 1024px) {
  .charts-grid {
    grid-template-columns: 1fr;
  }
}
</style>