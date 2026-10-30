<!-- index.vue -->
<template>
  <div class="honor-wall-root content-page community-surface tone-profile">
    <header class="content-header surface-heading"><SectionTrace particles="honor" />
      <div class="header-title-area">
        <p class="eyebrow">社区共建</p><h1>排行榜 · 荣誉墙</h1>
        <p>致敬所有为「太初寰宇」世界观、开源工具与社区生态注入心血的同行者</p>
      </div>
      <button class="btn-line" :disabled="isLoading" @click="loadHonorData">{{ isLoading ? '正在加载…' : '刷新榜单' }}</button>
    </header>

    <!-- 榜单切换：经验榜（原有）、土豆榜、常胜将军榜、常败将军榜 -->
    <nav class="board-tabs" role="tablist" aria-label="榜单">
      <button v-for="b in BOARDS" :key="b.key" type="button" role="tab" :aria-selected="board === b.key" :class="{ on: board === b.key }" @click="board = b.key">{{ b.label }}</button>
    </nav>
    <GameBoards v-if="board !== 'exp'" :board="board" />

    <template v-else>
    <div v-if="loadError" class="content-state" role="alert">榜单加载失败，请点击上方刷新重试。</div>
    <div class="honor-content" v-if="!isLoading && !loadError">
      <!-- 宏观统计总览 -->
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

      <div v-if="!topTenByExp.length" class="content-state">暂时还没有公开的贡献记录。</div>

      <template v-else>
        <!-- 前三尊位 -->
        <div v-if="podium.length" class="honor-podium">
          <article
            v-for="slot in podium"
            :key="slot.user.id"
            class="podium-card"
            :class="'is-' + slot.cls"
          >
            <span class="podium-medal" :class="'is-' + slot.cls" aria-hidden="true">{{ slot.badge }}</span>
            <h4 class="podium-name"><UserLink :id="slot.user.id" :name="slot.user.name || slot.user.username">@{{ slot.user.name || slot.user.username }}</UserLink></h4>
            <span class="podium-title">{{ slot.user.title || '社区共建者' }}</span>
            <div class="podium-metrics">
              <span class="metric exp">✨ {{ slot.user.totalExp ?? 0 }} exp</span>
              <span class="metric points">⚡ {{ slot.user.totalPoints ?? 0 }} pts</span>
              <span class="metric hours">⏱ {{ slot.user.totalHours ?? 0 }}h</span>
            </div>
          </article>
        </div>

        <!-- 4~10 名 -->
        <div class="honor-grid">
          <div
            v-for="(user, index) in rest"
            :key="user.id"
            class="honor-card"
          >
            <div class="rank-badge">{{ String(restStart + index).padStart(2, '0') }}</div>
            <div class="user-info">
              <h4 class="user-name"><UserLink :id="user.id" :name="user.name || user.username">@{{ user.name || user.username }}</UserLink></h4>
              <span class="user-title">{{ user.title || '社区共建者' }}</span>
            </div>
            <div class="metrics-area">
              <div class="metric-pill exp" title="个人总经验值">✨ {{ user.totalExp ?? 0 }} exp</div>
              <div class="metric-pill points" title="个人总贡献点数">⚡ {{ user.totalPoints ?? 0 }} pts</div>
              <div class="metric-pill hours" title="累计研发/共建工时">⏱ {{ user.totalHours ?? 0 }}h</div>
            </div>
          </div>
        </div>
      </template>
    </div>

    <div v-if="isLoading" class="honor-loading" role="status" aria-label="加载榜单">
      <div class="loading-bar"></div>
    </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import UserLink from '@/components/UserLink.vue'
import SectionTrace from '@/components/SectionTrace.vue'
import GameBoards from './GameBoards.vue'
import { ref, computed, onMounted } from 'vue';
import request from '@/utils/request';

const BOARDS = [
  { key: 'exp', label: '✨ 经验榜' },
  { key: 'potato', label: '🥔 土豆榜' },
  { key: 'wins', label: '🏅 常胜将军榜' },
  { key: 'losses', label: '🫠 常败将军榜' },
] as const;
const board = ref<'exp' | 'potato' | 'wins' | 'losses'>('exp');

const isLoading = ref(true);
const loadError = ref(false);
const honorList = ref<any[]>([]);
const stats = ref({ totalPoints: 0, totalExp: 0 });

// 按经验值从高到低排序并截取前 10 名
const topTenByExp = computed(() => {
  const sorted = [...honorList.value].sort((a, b) => (b.totalExp ?? 0) - (a.totalExp ?? 0));
  return sorted.slice(0, 10);
});

const medals = [
  { cls: 'gold', badge: '👑' },
  { cls: 'silver', badge: '🥈' },
  { cls: 'bronze', badge: '🥉' },
] as const;

// 前三领奖台：左二、中一、右三
const podium = computed(() => {
  const top = topTenByExp.value.slice(0, 3);
  if (top.length < 3) return [];
  return ([1, 0, 2] as const).map(rank => ({ user: top[rank], ...medals[rank] }));
});
const rest = computed(() => (topTenByExp.value.length >= 3 ? topTenByExp.value.slice(3) : topTenByExp.value));
const restStart = computed(() => (topTenByExp.value.length >= 3 ? 4 : 1));

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
.board-tabs { display: flex; flex-wrap: wrap; gap: 0; margin-bottom: 24px; border-bottom: 1px solid var(--border-line); }
.board-tabs button { padding: 10px 18px; border: 0; border-bottom: 2px solid transparent; background: none; color: var(--text-secondary); font: inherit; font-size: 15px; cursor: pointer; }
.board-tabs button.on { border-bottom-color: var(--accent-color); color: var(--accent-color); font-weight: 600; }
.overview-banner { display: grid; grid-template-columns: repeat(3,minmax(0,1fr)); gap: 16px; margin-bottom: 32px; }
.overview-item { display: grid; gap: 12px; padding: 24px; border: 0; border-left: 2px solid var(--signal-mint); border-radius: 0; background: var(--paper-card); }
.label { font-size: 12px; color: var(--text-secondary); }
.value { font-size: clamp(18px,2vw,26px); font-weight: 650; }
.section-title { margin-bottom: 20px; }
.section-title h3 { font-size: 18px; }

/* ===== 前三领奖台 ===== */
.honor-podium {
  display: grid;
  grid-template-columns: 1fr 1.18fr 1fr;
  gap: 18px;
  align-items: end;
  margin-bottom: 24px;
}
.podium-card {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
  padding: 28px 16px 22px;
  border: 1px solid var(--border-line);
  border-radius: 0;
  background: var(--paper-card);
  text-align: center;
  transition: transform .2s, box-shadow .2s;
}
.podium-card:hover { border-bottom-color: var(--signal-mint); }
.podium-card.is-gold { padding-top: 40px; border-color: var(--accent-color); background: linear-gradient(145deg,var(--accent-soft),var(--paper-card) 75%); }
.podium-card.is-silver { border-color: var(--border-line); }
.podium-card.is-bronze { border-color: var(--accent-mint); }

.podium-medal {
  position: absolute;
  top: -15px;
  left: 50%;
  transform: translateX(-50%);
  min-width: 34px;
  height: 34px;
  display: grid;
  place-items: center;
  padding: 0 6px;
  border-radius: 0;
  font-size: .9rem;
  font-weight: 700;
  color: #fff;
  background: var(--text-secondary);
  box-shadow: 0 3px 10px rgba(0, 0, 0, .18);
}
.podium-medal.is-gold { background: linear-gradient(135deg, #f9dc82, #d4af37); }
.podium-medal.is-silver { background: linear-gradient(135deg, #eef1f5, #aeb7c2); color: #5a6470; }
.podium-medal.is-bronze { background: linear-gradient(135deg, #e4b077, #b87333); }

.podium-name { margin: 0; font-size: 1.15rem; overflow-wrap: anywhere; }
.podium-card.is-gold .podium-name { font-size: 1.3rem; }
.podium-title { font-size: .78rem; color: var(--text-secondary); }
.podium-metrics { display: flex; flex-wrap: wrap; justify-content: center; gap: 6px; margin-top: 4px; }
.metric { padding: 4px 9px; border-radius: 0; font-size: 12px; color: var(--text-secondary); background: var(--bg-sub); }
.metric.exp { color: var(--accent-color); background: var(--accent-soft); }

/* ===== 4~10 名 ===== */
.honor-grid { display: grid; gap: 10px; }
.honor-card { display: grid; grid-template-columns: 48px minmax(0,1fr) auto; align-items: center; gap: 16px; padding: 20px 24px; border: 0; border-bottom: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); }
.rank-badge { font: 600 15px var(--font-mono); color: var(--text-secondary); text-align: center; }
.user-name { font-size: 15px; overflow-wrap: anywhere; margin: 0; }
.user-title { display: block; font-size: 12px; color: var(--text-secondary); margin-top: 4px; }
.metrics-area { display: flex; flex-wrap: wrap; gap: 8px; }
.metric-pill { padding: 5px 10px; border-radius: 0; font-size: 12px; color: var(--text-secondary); background: var(--bg-sub); }
.metric-pill.exp { color: var(--accent-color); background: var(--accent-soft); }

.honor-loading { padding: 64px; text-align: center; }
.loading-bar { width: 60px; height: 3px; margin: auto; background: var(--accent-color); }
.content-state { padding: 24px 0; color: var(--text-secondary); text-align: center; }

@media(max-width: 700px) {
  .overview-banner { grid-template-columns: minmax(0,1fr); gap: 10px; }
  .overview-item { display: flex; align-items: center; justify-content: space-between; padding: 16px; }
  .honor-podium { gap: 8px; }
  .podium-card { padding: 24px 8px 14px; gap: 6px; }
  .podium-card.is-gold { padding-top: 32px; }
  .podium-name, .podium-card.is-gold .podium-name { font-size: .95rem; }
  .podium-title { font-size: .7rem; }
  .metric { padding: 3px 6px; font-size: 10px; }
  .honor-card { grid-template-columns: 40px minmax(0,1fr); padding: 14px; }
  .metrics-area { grid-column: 2; }
}
</style>
