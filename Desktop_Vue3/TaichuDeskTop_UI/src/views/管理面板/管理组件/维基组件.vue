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

    <p v-if="loadError" class="load-error" role="alert">{{ loadError }} <button type="button" @click="loadAllData">重新加载</button></p>

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
import { getErrorMessage } from '@/utils/apiError';

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
const loadError = ref('');
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
  // 四块数据分开加载：以前用 Promise.all，任何一个接口出错，整个页面都是空的
  loadError.value = '';
  const [c, r, rev, arts] = await Promise.allSettled([
    adminWikiApi.getAllCategories(),
    adminWikiApi.getCategoryRequests(),
    wikiReviewApi.getPending(),
    adminWikiApi.getAllArticlesForManagement()
  ]);
  const pick = (res: PromiseSettledResult<any>, label: string) => {
    if (res.status === 'fulfilled') return res.value || [];
    loadError.value += `${label}读取失败：${getErrorMessage(res.reason, '请稍后再试')}。`;
    return [];
  };
  store.value = {
    categories: pick(c, '分类'),
    requests: pick(r, '分类申请'),
    revisions: pick(rev, '待审核内容'),
    articles: pick(arts, '文章列表')
  };
};

onMounted(loadAllData);
</script>

<style scoped>
.load-error { margin: 0 0 16px; padding: 10px 12px; border-left: 3px solid var(--danger); background: var(--bg-sub); color: var(--text-primary); font-size: 13px; }
.load-error button { margin-left: 8px; border: 1px solid var(--border-line); background: transparent; color: var(--accent-color); cursor: pointer; font: inherit; }
.wiki-admin-layout { padding: 40px; background: var(--paper-card); min-height: 100vh; color: var(--text-primary); }
.module-header { display: flex; justify-content: space-between; align-items: flex-end; margin-bottom: 40px; }
.page-title { font-size: 1.8rem; font-weight: 700; color: var(--text-primary); margin: 0; }
.md-subtitle { font-size: 0.9rem; color: var(--text-secondary); margin: 8px 0 0; }

.md-tabs { 
  display: flex; 
  gap: 32px; 
  border-bottom: 1px solid var(--border-line); 
  margin-bottom: 30px; 
}

.tab-item { 
  cursor: pointer; 
  color: var(--text-secondary); 
  padding-bottom: 12px; 
  font-size: 0.9rem; 
  font-weight: 500;
  transition: all 0.2s ease;
  position: relative;
}

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
</style>