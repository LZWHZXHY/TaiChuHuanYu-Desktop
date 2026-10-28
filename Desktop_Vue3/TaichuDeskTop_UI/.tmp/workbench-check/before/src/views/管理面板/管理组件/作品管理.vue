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
        <component :is="currentComponent" :type="activeTab === 'post' ? 'post' : 'blog'" />
      </transition>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';

// 引入刚刚创建的画廊子组件
import GalleryGovernance from './作品管理组件/画廊治理.vue';
import 博客治理 from './作品管理组件/博客治理.vue';

// 预留其他类型组件的引入位置（后续步骤实现）
// import ArticleGovernance from './作品组件子类/图文治理.vue';
// import VideoGovernance from './作品组件子类/光影治理.vue';

const activeTab = ref('gallery'); // 默认先展示画廊

// 定义 Tabs 结构
const TABS = [
  { id: 'gallery', label: '画廊治理' },
  { id: 'blog', label: '博客管理' },
  { id: 'post', label: '帖子管理' }
];

const currentComponent = computed(() => {
  if (activeTab.value === 'blog' || activeTab.value === 'post') return 博客治理;
  return GalleryGovernance;
});
</script>

<style scoped>
.works-layout { display: flex; flex-direction: column; animation: slideIn 0.35s cubic-bezier(0.16, 1, 0.3, 1); }
.module-header { margin-bottom: 30px; }
.page-title { font-size: 1.6rem; font-weight: 700; color: var(--text-primary); margin: 0; }
.md-subtitle { font-size: 0.85rem; color: var(--text-secondary); margin: 6px 0 0; }

/* 极其优雅的墨水风 Tabs 设计 (复刻你的维基组件) */
.md-tabs { 
  display: flex; 
  gap: 32px; 
  border-bottom: 1px solid var(--border-line); 
  margin-bottom: 24px; 
}

.tab-item { 
  cursor: pointer; 
  color: var(--text-secondary); 
  padding-bottom: 12px; 
  font-size: 0.95rem; 
  font-weight: 600;
  transition: all 0.2s ease;
  position: relative;
  letter-spacing: 0.5px;
}

.tab-item:hover { color: var(--text-primary); }
.tab-item.active { color: var(--text-primary); }
.tab-item.active::after {
  content: '';
  position: absolute;
  left: 0;
  right: 0;
  bottom: -1px;
  height: 2px;
  background: var(--text-primary);
}

/* 统一卡片底色 */
.table-card { 
  background: var(--paper-card); 
  border: 1px solid var(--border-line); 
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