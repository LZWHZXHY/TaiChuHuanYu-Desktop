<!-- index.vue -->
<template>
  <div class="honor-wall-root">
    <div class="honor-header">
      <div class="header-title-area">
        <h2>共建荣誉殿堂</h2>
        <p>致敬所有为「太初寰宇」世界观、开源工具与社区生态注入心血的同行者</p>
      </div>
    </div>

    <div class="honor-content" v-if="!isLoading">
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

    <div v-else class="honor-loading">
      <div class="loading-bar"></div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import request from '@/utils/request';

const isLoading = ref(true);
const honorList = ref<any[]>([]);
const stats = ref({ totalPoints: 0, totalExp: 0 });

// 计算属性：将成员按经验值 (totalExp) 从高到低排序并截取前 10 名
const topTenByExp = computed(() => {
  const sorted = [...honorList.value].sort((a, b) => (b.totalExp ?? 0) - (a.totalExp ?? 0));
  return sorted.slice(0, 10);
});

const loadHonorData = async () => {
  isLoading.value = true;
  try {
    const res: any = await request.get('/community/honor-wall');
    honorList.value = res.list || res.data || [];
    stats.value = res.stats || { totalPoints: 0, totalExp: 0 };
  } catch (err) {
    console.error("加载荣誉墙数据失败:", err);
  } finally {
    isLoading.value = false;
  }
};

onMounted(loadHonorData);
</script>

<style scoped>
.honor-wall-root { width: 100%; max-width: 1200px; margin: 0 auto; padding: 20px 0; }
.honor-header { margin-bottom: 32px; border-bottom: 1px solid #eee; padding-bottom: 20px; }
.header-title-area h2 { font-size: 1.5rem; font-weight: 500; color: #1a1a1a; margin: 0 0 8px 0; letter-spacing: 0.5px; }
.header-title-area p { font-size: 0.85rem; color: #777; margin: 0; }

.overview-banner { display: flex; gap: 24px; margin-bottom: 32px; }
.overview-item { background: #fafafa; border: 1px solid #eee; padding: 20px 24px; flex: 1; display: flex; flex-direction: column; gap: 8px; }
.overview-item .label { font-size: 0.7rem; color: #aaa; text-transform: uppercase; letter-spacing: 1px; }
.overview-item .value { font-size: 1.4rem; font-weight: 500; font-family: monospace; color: #1a1a1a; }

.section-title { margin-bottom: 16px; }
.section-title h3 { font-size: 1rem; font-weight: 500; color: #333; margin: 0; letter-spacing: 0.5px; }

.honor-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 20px; }
.honor-card { background: #fff; border: 1px solid #eee; padding: 24px; display: flex; flex-direction: column; gap: 16px; position: relative; transition: transform 0.2s, border-color 0.2s; }
.honor-card:hover { border-color: #1a1a1a; transform: translateY(-2px); box-shadow: 0 10px 30px rgba(0,0,0,0.02); }
.honor-card.top-tier { border-left: 3px solid #1a1a1a; background: #fcfcfc; }

.rank-badge { font-family: monospace; font-size: 0.75rem; color: #bbb; font-weight: 600; }
.rank-badge.rank-1 { color: #d4af37; }
.rank-badge.rank-2 { color: #858585; }
.rank-badge.rank-3 { color: #b08d57; }

.user-info { display: flex; flex-direction: column; gap: 4px; }
.user-name { font-size: 1rem; font-weight: 500; color: #1a1a1a; margin: 0; }
.user-title { font-size: 0.75rem; color: #888; }

.metrics-area { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; margin-top: auto; }
.metric-pill { font-size: 0.68rem; font-family: monospace; padding: 3px 8px; border-radius: 2px; }
.metric-pill.exp { background: #6f42c1; color: #fff; }
.metric-pill.points { background: #1a1a1a; color: #fff; }
.metric-pill.hours { background: #f0f0f0; color: #555; }

.honor-loading { height: 200px; display: flex; align-items: center; justify-content: center; }
.loading-bar { width: 60px; height: 1px; background: #1a1a1a; animation: pulse 1.5s infinite; }
@keyframes pulse { 0% { transform: scaleX(0.5); opacity: 0.2; } 50% { transform: scaleX(1.5); opacity: 1; } 100% { transform: scaleX(0.5); opacity: 0.2; } }
</style>