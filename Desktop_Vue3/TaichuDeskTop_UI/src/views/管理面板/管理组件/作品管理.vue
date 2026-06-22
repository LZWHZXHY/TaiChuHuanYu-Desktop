<template>
  <div class="works-layout">
    <header class="module-header">
      <div class="header-content">
        <h2 class="page-title">全域作品治理</h2>
        <p class="md-subtitle">长篇图文、视觉画廊与光影映像的分型管理</p>
      </div>
    </header>

    <nav class="md-tabs">
      <span 
        v-for="tab in TABS" 
        :key="tab.id" 
        class="tab-item" 
        :class="{ active: activeTab === tab.id }" 
        @click="activeTab = tab.id"
      >
        {{ tab.label }}
      </span>
    </nav>

    <div class="works-content-body table-card">
      <transition name="fade-transform" mode="out-in">
        <component :is="currentComponent" />
      </transition>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';

// 引入刚刚创建的画廊子组件
import GalleryGovernance from './作品管理组件/画廊治理.vue';

// 预留其他类型组件的引入位置（后续步骤实现）
// import ArticleGovernance from './作品组件子类/图文治理.vue';
// import VideoGovernance from './作品组件子类/光影治理.vue';

const activeTab = ref('gallery'); // 默认先展示画廊

// 定义 Tabs 结构
const TABS = [
  { id: 'gallery', label: '视觉画廊 (Gallery)', component: GalleryGovernance },
  // { id: 'article', label: '长篇图文 (Article)', component: ArticleGovernance },
  // { id: 'video', label: '光影映像 (Video)', component: VideoGovernance }
];

const currentComponent = computed(() => {
  return TABS.find(t => t.id === activeTab.value)?.component;
});
</script>

<style scoped>
.works-layout { display: flex; flex-direction: column; animation: slideIn 0.35s cubic-bezier(0.16, 1, 0.3, 1); }
.module-header { margin-bottom: 30px; }
.page-title { font-size: 1.6rem; font-weight: 700; color: #111; margin: 0; }
.md-subtitle { font-size: 0.85rem; color: #888; margin: 6px 0 0; }

/* 极其优雅的墨水风 Tabs 设计 (复刻你的维基组件) */
.md-tabs { 
  display: flex; 
  gap: 32px; 
  border-bottom: 1px solid #f2f2f7; 
  margin-bottom: 24px; 
}

.tab-item { 
  cursor: pointer; 
  color: #86868b; 
  padding-bottom: 12px; 
  font-size: 0.95rem; 
  font-weight: 600;
  transition: all 0.2s ease;
  position: relative;
  letter-spacing: 0.5px;
}

.tab-item:hover { color: #333; }
.tab-item.active { color: #111; }
.tab-item.active::after {
  content: '';
  position: absolute;
  left: 0;
  right: 0;
  bottom: -1px;
  height: 2px;
  background: #111;
}

/* 统一卡片底色 */
.table-card { 
  background: #fff; 
  border: 1px solid #f0f0f0; 
  border-radius: 8px; 
  padding: 24px;
  box-shadow: 0 4px 20px rgba(0,0,0,0.01); 
  min-height: 500px;
}

/* 切换动画 */
.fade-transform-enter-active,
.fade-transform-leave-active {
  transition: all 0.25s ease;
}
.fade-transform-enter-from { opacity: 0; transform: translateY(10px); }
.fade-transform-leave-to { opacity: 0; transform: translateY(-10px); }

@keyframes slideIn { from { opacity: 0; transform: translateY(8px); } to { opacity: 1; transform: translateY(0); } }
</style>