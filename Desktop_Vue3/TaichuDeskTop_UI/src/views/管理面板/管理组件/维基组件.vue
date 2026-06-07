<template>
  <div class="wiki-admin-layout">
    <header class="module-header">
      <div class="header-content">
        <h2 class="page-title">维基治理中枢</h2>
        <p class="md-subtitle">全量内容管理、分类治理与版本回溯</p>
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

    <component 
  :is="currentComponent" 
  :data="currentData" 
  @refresh="loadAllData" 
  />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { adminWikiApi, wikiReviewApi } from '@/api/Admin';

// 严格按照目录结构引入子组件
import CategoryManager from './维基组件子类/CategoryManager.vue';
import CategoryRequests from './维基组件子类/CategoryRequests.vue';
import RevisionReview from './维基组件子类/RevisionReview.vue';
import ArticleGovernance from './维基组件子类/ArticleGovernance.vue';

interface WikiStore {
  categories: any[];
  requests: any[];
  revisions: any[];
  articles: any[];
}

const activeTab = ref('categories');
const store = ref<WikiStore>({ 
  categories: [], 
  requests: [], 
  revisions: [], 
  articles: [] 
});

const TABS = [
  { id: 'categories', label: '分类管理', component: CategoryManager },
  { id: 'requests', label: '分类申请', component: CategoryRequests },
  { id: 'revisions', label: '内容审核', component: RevisionReview },
  { id: 'articles', label: '文章治理', component: ArticleGovernance }
];

const currentComponent = computed(() => TABS.find(t => t.id === activeTab.value)?.component);
const currentData = computed(() => store.value[activeTab.value as keyof WikiStore]);

// 统一数据加载逻辑
const loadAllData = async () => {
  try {
    const [c, r, rev, arts] = await Promise.all([
      adminWikiApi.getAllCategories(),
      adminWikiApi.getCategoryRequests(),
      wikiReviewApi.getPending(),
      adminWikiApi.getAllArticlesForManagement()
    ]);
    store.value = { 
      categories: c || [], 
      requests: r || [], 
      revisions: rev || [], 
      articles: arts || [] 
    };
  } catch (err) {
    console.error("数据加载失败:", err);
  }
};

onMounted(loadAllData);
</script>

<style scoped>
.wiki-admin-layout { padding: 40px; background: #fff; min-height: 100vh; color: #1a1a1a; }
.module-header { display: flex; justify-content: space-between; align-items: flex-end; margin-bottom: 40px; }
.page-title { font-size: 1.8rem; font-weight: 700; color: #000; margin: 0; }
.md-subtitle { font-size: 0.9rem; color: #86868b; margin: 8px 0 0; }

.md-tabs { 
  display: flex; 
  gap: 32px; 
  border-bottom: 1px solid #f2f2f7; 
  margin-bottom: 30px; 
}

.tab-item { 
  cursor: pointer; 
  color: #86868b; 
  padding-bottom: 12px; 
  font-size: 0.9rem; 
  font-weight: 500;
  transition: all 0.2s ease;
  position: relative;
}

.tab-item.active { color: #000; }
.tab-item.active::after {
  content: '';
  position: absolute;
  left: 0;
  right: 0;
  bottom: -1px;
  height: 2px;
  background: #000;
}
</style>