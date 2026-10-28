<template>
  <article class="gallery-container content-page">
    <p v-if="publishMessage" class="publish-message" role="status">{{ publishMessage }}</p>
    <header class="gallery-nav content-header">
      <div class="nav-left">
        <div><p class="eyebrow">社区创作 · 画廊</p><h1 class="brand-title">太初艺术馆</h1><p class="intro">分享作品与练习，用标签发现同好。</p></div>

      </div>

      <!-- 右侧：直接在画布布展发布作品入口 -->
      <div class="nav-right content-actions">
        <button class="upload-link" @click="handleUpload">
          发布作品
        </button>
      </div>
    </header>

    <form class="gallery-search" @submit.prevent="applySearch"><input v-model="searchInput" aria-label="搜索画作" placeholder="搜索标题、说明、作者或标签" maxlength="100" /><button>搜索</button><button type="button" v-if="route.query.q" @click="searchInput = ''; applySearch()">清除</button></form>
    <div class="section-toolbar">        <div class="content-filters" aria-label="画廊视图">
          <button 
            :aria-pressed="viewMode === 'explore'" 
            @click="switchMode('explore')"
          >全部作品</button>
          <button 
            :aria-pressed="viewMode === 'ranking'" 
            @click="switchMode('ranking')"
          >作品榜单</button>
        </div><span class="section-hint">发现社区创作者的作品</span></div>
    <main class="gallery-view">
      <transition name="fade" mode="out-in">
        <div v-if="viewMode === 'explore'" key="explore" class="view-wrapper">
          <div class="view-header">
            <span class="view-label">{{ route.query.q ? '搜索结果' : '全部作品' }}</span>
          </div>
          <!-- 绑定 key：发布新作品后递增 key，无感重置并重新加载瀑布流列表 -->
          <ArtworkInfiniteGrid :key="gridKey" :search="String(route.query.q || '')" @on-click="openArtwork" />
        </div>

        <div v-else-if="viewMode === 'ranking'" key="ranking" class="view-wrapper">
          <div class="view-header">
            <span class="view-label">作品榜单 / {{ currentTab }}榜</span>
            <nav class="content-filters" aria-label="榜单周期">
              <button 
                v-for="t in ['总', '日', '周', '月', '年']" 
                :key="t"
                :aria-pressed="currentTab === t"
                @click="currentTab = t"
              >{{ t }}榜</button>
            </nav>
          </div>
          <ArtworkRanking @open="openArtwork" :period="currentTab" @explore="switchMode('explore')" />
        </div>
      </transition>
    </main>

    <!-- 详情抽屉弹窗 -->
    <transition name="slide-up">
      <ArtworkDetail 
        v-if="activeArtworkId" 
        :id="activeArtworkId" 
        @close="closeArtworkDetail"
        @updated="gridKey++"
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
import type { ArtworkWriteResult } from '../../api/artwork';
import { useUserStore } from '../../stores/user';
import ArtworkRanking from './ArtworkRanking.vue';
import ArtworkInfiniteGrid from './ArtworkInfiniteGrid.vue';
import ArtworkDetail from './ArtworkDetail.vue';
import ArtworkPublishModal from './ArtworkPublishModal.vue';

const userStore = useUserStore();
const route = useRoute();
const router = useRouter();
const searchInput = ref(String(route.query.q || ''));
const applySearch = () => { viewMode.value = 'explore'; return router.replace({ query: { ...route.query, q: searchInput.value.trim() || undefined } }); };
watch(() => route.query.q, q => searchInput.value = String(q || ''));

// 原有状态
const viewMode = ref<'explore' | 'ranking'>('explore');
const currentTab = ref('总');

// 控制发布弹窗显隐与瀑布流刷新
const isPublishModalOpen = ref(false);
const publishMessage = ref('');
const gridKey = ref(0);

// --- 🌟 路由同步逻辑 ---
const activeArtworkId = ref<string | null>(null);

const openArtwork = (id: number | string) => {
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
  if (!userStore.isLoggedIn) {
    publishMessage.value = '请先登录后再发布作品';
    void router.push({ name: '身份认证', query: { redirect: router.resolve({ name: 'GalleryHome', query: { ...route.query, publish: '1' } }).fullPath } });
    return;
  }
  isPublishModalOpen.value = true;
  if (route.query.publish) void router.replace({ query: { ...route.query, publish: undefined } });
};

watch(() => route.query.publish, value => { if (value === '1') handleUpload(); }, { immediate: true });
watch(isPublishModalOpen, value => { if (!value && route.query.publish) void router.replace({ query: { ...route.query, publish: undefined } }); });

// 发布成功后的回调：切回探索视图并重载瀑布流
const onArtworkPublished = (createdArtwork: ArtworkWriteResult) => {
  publishMessage.value = createdArtwork.message || '作品发布成功';
  viewMode.value = 'explore';
  gridKey.value += 1;
  window.scrollTo({ top: 0, behavior: 'smooth' });

  // 如果后端返回了新生成的作品ID，可自动打开该作品详情预览
  void router.push({ query: { ...route.query, q: undefined, workId: createdArtwork.artworkId ? String(createdArtwork.artworkId) : undefined } });
};
</script>

<style scoped>

.gallery-container { min-height: 70vh; }
.gallery-search { display: flex; gap: 10px; margin: 0 0 28px; padding: 0; background: transparent; }
.gallery-search input { flex: 1; min-width: 0; }
.gallery-search input, .gallery-search button { padding: 10px 14px; color: var(--text-primary); background: var(--paper-bg); border: 1px solid var(--border-line); border-radius: var(--radius-sm); }
.publish-message { color: var(--accent-color); padding: 12px 0; }
.nav-left { display: flex; flex-wrap: wrap; align-items: center; gap: 28px; }
.view-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
.view-label { font-size: .9rem; color: var(--text-secondary); }
.fade-enter-active, .fade-leave-active, .slide-up-enter-active, .slide-up-leave-active { transition: opacity .2s ease; }
.fade-enter-from, .fade-leave-to, .slide-up-enter-from, .slide-up-leave-to { opacity: 0; }
@media (max-width: 720px) { .gallery-search { flex-wrap: wrap; } .nav-left { gap: 16px; } }

</style>
