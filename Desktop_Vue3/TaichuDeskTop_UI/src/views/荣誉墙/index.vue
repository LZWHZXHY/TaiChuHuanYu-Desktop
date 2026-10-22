<!-- index.vue -->
<template>
  <div class="honor-wall-root content-page">
    <header class="content-header">
      <div class="header-title-area">
        <p class="eyebrow">社区共建</p><h1>排行榜 · 荣誉墙</h1>
        <p>致敬所有为「太初寰宇」世界观、开源工具与社区生态注入心血的同行者</p>
      </div>
      <button class="btn-line" :disabled="isLoading" @click="loadHonorData">{{ isLoading ? '正在加载…' : '刷新榜单' }}</button>
    </header>

    <div v-if="loadError" class="content-state" role="alert">榜单加载失败，请点击上方刷新重试。</div>
    <div class="honor-content" v-if="!isLoading && !loadError">
      <!-- 宏观统计总览：展示总贡献、总经验与总人数 -->
      <div class="overview-banner">
        <div class="overview-item">
          <span class="label">社区全员总贡献值</span>
          <span class="value">⚡ {{ stats.totalPoints ?? 0 }} pts</span>
        </div>
        <div class="overview-item">
          <span class="label">社区全员总经验值</span>
          <span class="value">✨ {{ stats.totalExp ?? 0 }} exp</span>
        </div>
        <div class="overview-item">
          <span class="label">参与共建总人数</span>
          <span class="value">👥 {{ honorList.length }} 位</span>
        </div>
      </div>

      <!-- 模块标题 -->
      <div class="section-title">
        <h3>🏆 经验值 TOP 10 排行榜</h3>
      </div>

      <!-- 荣誉排行榜网格 (按经验值排名前10名) -->
      <div v-if="!topTenByExp.length" class="content-state">暂时还没有公开的贡献记录。</div>
      <div class="honor-grid">
        <div 
          v-for="(user, index) in topTenByExp" 
          :key="user.id" 
          class="honor-card"
          :class="{ 'top-tier': index < 3 }"
        >
          <div class="rank-badge" :class="'rank-' + (index + 1)">
            {{ index === 0 ? '👑 01' : index === 1 ? '🥈 02' : index === 2 ? '🥉 03' : String(index + 1).padStart(2, '0') }}
          </div>
          
          <div class="user-info">
            <h4 class="user-name">@{{ user.name || user.username }}</h4>
            <span class="user-title">{{ user.title || '社区共建者' }}</span>
          </div>

          <div class="metrics-area">
            <div class="metric-pill exp" title="个人总经验值">
              ✨ {{ user.totalExp ?? 0 }} exp
            </div>
            <div class="metric-pill points" title="个人总贡献点数">
              ⚡ {{ user.totalPoints ?? 0 }} pts
            </div>
            <div class="metric-pill hours" title="累计研发/共建工时">
              ⏱ {{ user.totalHours ?? 0 }}h
            </div>
          </div>
        </div>
      </div>
    </div>

    <div v-if="isLoading" class="honor-loading" role="status" aria-label="加载榜单">
      <div class="loading-bar"></div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import request from '@/utils/request';

const isLoading = ref(true);
const loadError = ref(false);
const honorList = ref<any[]>([]);
const stats = ref({ totalPoints: 0, totalExp: 0 });

// 计算属性：将成员按经验值 (totalExp) 从高到低排序并截取前 10 名
const topTenByExp = computed(() => {
  const sorted = [...honorList.value].sort((a, b) => (b.totalExp ?? 0) - (a.totalExp ?? 0));
  return sorted.slice(0, 10);
});

const loadHonorData = async () => {
  isLoading.value = true;
  loadError.value = false;
  try {
    const res: any = await request.get('/community/honor-wall');
    honorList.value = res.list || res.data || [];
    stats.value = res.stats || { totalPoints: 0, totalExp: 0 };
  } catch (err) {
    loadError.value = true;
    console.error("加载荣誉墙数据失败:", err);
  } finally {
    isLoading.value = false;
  }
};

onMounted(loadHonorData);
</script>

<style scoped>
.overview-banner { display: grid; grid-template-columns: repeat(3,minmax(0,1fr)); gap: 16px; margin-bottom: 32px; }
.overview-item { display: grid; gap: 12px; padding: 24px; border: 1px solid var(--border-line); border-radius: 12px; background: var(--paper-card); }
.label { font-size: 12px; color: var(--text-secondary); }
.value { font-size: clamp(18px,2vw,26px); font-weight: 650; }
.section-title { margin-bottom: 16px; }
.section-title h3 { font-size: 18px; }
.honor-grid { display: grid; gap: 10px; }
.honor-card { display: grid; grid-template-columns: 64px minmax(0,1fr) auto; align-items: center; gap: 16px; padding: 20px 24px; border: 1px solid var(--border-line); border-radius: 12px; background: var(--paper-card); }
.honor-card.top-tier { border-left: 3px solid var(--accent-color); }
.rank-badge { font: 600 16px var(--font-mono); color: var(--text-secondary); }
.top-tier .rank-badge { color: var(--accent-color); }
.user-name { font-size: 16px; overflow-wrap: anywhere; }
.user-title { display: block; font-size: 12px; color: var(--text-secondary); margin-top: 6px; }
.metrics-area { display: flex; flex-wrap: wrap; gap: 8px; }
.metric-pill { padding: 5px 10px; border-radius: 6px; font-size: 12px; color: var(--text-secondary); background: var(--bg-sub); }
.metric-pill.exp { color: var(--accent-color); background: var(--accent-soft); }
.honor-loading { padding: 64px; text-align: center; }
.loading-bar { width: 60px; height: 3px; margin: auto; background: var(--accent-color); }
@media(max-width: 700px) { .overview-banner { grid-template-columns: minmax(0,1fr); gap: 10px; } .overview-item { display: flex; align-items: center; justify-content: space-between; padding: 16px; } .honor-card { grid-template-columns: 48px minmax(0,1fr); padding: 16px; } .metrics-area { grid-column: 2; } }
</style>
