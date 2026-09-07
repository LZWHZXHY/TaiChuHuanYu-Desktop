<template>
  <article class="gallery-container">
    <header class="gallery-nav">
      <div class="nav-left">
        <h1 class="brand-title">太初艺术馆</h1>
        <div class="mode-selectors">
          <button 
            :class="['mode-item', { active: viewMode === 'explore' }]" 
            @click="switchMode('explore')"
          >探索</button>
          <button 
            :class="['mode-item', { active: viewMode === 'ranking' }]" 
            @click="switchMode('ranking')"
          >荣誉</button>
        </div>
      </div>

      <!-- 右侧：直接在画布布展发布作品入口 -->
      <div class="nav-right">
        <button class="upload-link" @click="handleUpload">
          ✦ 展开画卷
        </button>
      </div>
    </header>

    <main class="gallery-view">
      <transition name="fade" mode="out-in">
        <div v-if="viewMode === 'explore'" key="explore" class="view-wrapper">
          <div class="view-header">
            <span class="view-label">维度碎片 / 全部</span>
          </div>
          <!-- 绑定 key：发布新作品后递增 key，无感重置并重新加载瀑布流列表 -->
          <ArtworkInfiniteGrid :key="gridKey" @on-click="openArtwork" />
        </div>

        <div v-else-if="viewMode === 'ranking'" key="ranking" class="view-wrapper">
          <div class="view-header">
            <span class="view-label">灵脉巅峰 / {{ currentTab }}榜</span>
            <nav class="time-filter">
              <span 
                v-for="t in ['日', '周', '月', '年']" 
                :key="t"
                :class="{ active: currentTab === t }"
                @click="currentTab = t"
              >{{ t }}</span>
            </nav>
          </div>
          <ArtworkRanking :period="currentTab" />
        </div>
      </transition>
    </main>

    <!-- 详情抽屉弹窗 -->
    <transition name="slide-up">
      <ArtworkDetail 
        v-if="activeArtworkId" 
        :id="activeArtworkId" 
        @close="closeArtworkDetail"
      />
    </transition>

    <!-- 🌟 画布直接发布弹窗 -->
    <ArtworkPublishModal 
      v-model="isPublishModalOpen" 
      @published="onArtworkPublished" 
    />
  </article>
</template>

<script setup lang="ts">
import { ref, nextTick, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useUserStore } from '../../stores/user';
import ArtworkRanking from './ArtworkRanking.vue';
import ArtworkInfiniteGrid from './ArtworkInfiniteGrid.vue';
import ArtworkDetail from './ArtworkDetail.vue';
import ArtworkPublishModal from './ArtworkPublishModal.vue';

const userStore = useUserStore();
const route = useRoute();
const router = useRouter();

// 原有状态
const viewMode = ref<'explore' | 'ranking'>('explore');
const currentTab = ref('月');

// 控制发布弹窗显隐与瀑布流刷新
const isPublishModalOpen = ref(false);
const gridKey = ref(0);

// --- 🌟 路由同步逻辑 ---
const activeArtworkId = ref<string | null>(null);

const openArtwork = (id: number | string) => {
  console.log('正在开启灵脉详情，作品ID:', id);
  router.push({
    query: {
      ...route.query,
      workId: id.toString()
    }
  });
};

watch(
  () => route.query.workId,
  (newId) => {
    activeArtworkId.value = (newId as string) || null;
  },
  { immediate: true }
);

// 关闭详情：仅仅移除 URL 里的参数，不破坏当前的 viewMode 状态
const closeArtworkDetail = () => {
  router.push({
    query: { ...route.query, workId: undefined }
  });
};

const switchMode = (mode: 'explore' | 'ranking') => {
  viewMode.value = mode;
  nextTick(() => {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  });
};

// 触发发布流程，如果已登录则开启弹窗
// 触发发布流程：通过 userInfo 判定是否已连接灵脉
const handleUpload = () => {
  if (!userStore.userInfo?.id) {
    alert('请先连接灵脉（登录）后再行布展');
    return;
  }
  isPublishModalOpen.value = true;
};

// 发布成功后的回调：切回探索视图并重载瀑布流
const onArtworkPublished = (createdArtwork?: any) => {
  viewMode.value = 'explore';
  gridKey.value += 1;
  window.scrollTo({ top: 0, behavior: 'smooth' });

  // 如果后端返回了新生成的作品ID，可自动打开该作品详情预览
  if (createdArtwork?.id) {
    openArtwork(createdArtwork.id);
  }
};
</script>

<style scoped>
.gallery-container {
  width: 100%;
  min-height: 100vh;
  background: #fff;
}

.gallery-nav {
  display: flex;
  justify-content: space-between;
  align-items: center;
  height: 100px;
  padding: 0 4%;
  border-bottom: 1px solid #f2f2f2;
  position: sticky;
  top: 0;
  z-index: 100;
  background: rgba(255, 255, 255, 0.8);
  backdrop-filter: blur(20px);
}

.nav-left { display: flex; align-items: baseline; gap: 40px; }
.brand-title { font-size: 1.5rem; font-weight: 700; letter-spacing: -0.03em; margin: 0; }
.mode-selectors { display: flex; gap: 24px; }
.mode-item { background: none; border: none; font-size: 1rem; font-weight: 500; color: #86868b; cursor: pointer; transition: color 0.3s; }
.mode-item.active { color: #1a1a1a; font-weight: 600; }

.nav-right { display: flex; align-items: center; }
.upload-link { 
  background: #000; 
  color: #fff; 
  border: none; 
  padding: 10px 24px; 
  border-radius: 40px; 
  font-size: 0.9rem; 
  font-weight: 500; 
  cursor: pointer; 
  transition: opacity 0.2s ease, transform 0.2s ease;
}
.upload-link:hover { 
  opacity: 0.85; 
  transform: translateY(-1px);
}

.gallery-view { padding: 60px 4%; }
.view-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 40px; }
.view-label { font-size: 0.85rem; text-transform: uppercase; letter-spacing: 0.1em; color: #86868b; }
.time-filter { display: flex; gap: 20px; }
.time-filter span { font-size: 0.9rem; color: #86868b; cursor: pointer; }
.time-filter span.active { color: #1a1a1a; font-weight: 600; text-decoration: underline; text-underline-offset: 8px; }

/* 基础淡入动画 */
.fade-enter-active, .fade-leave-active { transition: opacity 0.4s ease, transform 0.4s ease; }
.fade-enter-from { opacity: 0; transform: translateY(10px); }
.fade-leave-to { opacity: 0; transform: translateY(-10px); }

/* 详情弹窗专用的滑入动画 */
.slide-up-enter-active, 
.slide-up-leave-active {
  transition: all 0.6s cubic-bezier(0.16, 1, 0.3, 1);
}
.slide-up-enter-from {
  opacity: 0;
  transform: translateY(40px);
}
.slide-up-leave-to {
  opacity: 0;
  transform: scale(0.98);
}

@media (max-width: 768px) {
  .nav-left { gap: 20px; }
  .brand-title { font-size: 1.2rem; }
  .upload-link { padding: 6px 14px; font-size: 0.8rem; }
}
</style>