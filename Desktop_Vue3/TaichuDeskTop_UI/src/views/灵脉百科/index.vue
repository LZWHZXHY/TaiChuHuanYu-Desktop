<template>
  <div class="premium-layout">
    <aside class="premium-sidebar">
      <div class="sidebar-header">
        <div class="brand-logo" @click="resetToGlobal">
          <div class="logo-box">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M12 2L2 12h3v8h14v-8h3L12 2z"/></svg>
          </div>
          <span class="brand-text">太初百科</span>
        </div>
      </div>

      <div class="sidebar-scroll">
        <div class="nav-section">
          <a class="nav-link root-link" :class="{ active: currentCategoryId === 'all' }" @click="handleCategoryChange('all')">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="link-icon"><rect x="3" y="3" width="18" height="18" rx="2" ry="2"/><line x1="3" y1="9" x2="21" y2="9"/><line x1="9" y1="21" x2="9" y2="9"/></svg>
            全知矩阵 (全部)
          </a>
        </div>

        <div class="nav-section" v-for="rootCat in rootCategories" :key="rootCat.id">
          <a 
            class="nav-link root-link" 
            :class="{ active: currentCategoryId === rootCat.id }" 
            @click="handleCategoryChange(rootCat.id)"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="link-icon"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/></svg>
            {{ rootCat.name }}
          </a>
          
          <div class="sub-links-group">
            <a 
              v-for="sub in getSubCategories(rootCat.id)" 
              :key="sub.id"
              class="nav-link sub-link"
              :class="{ active: currentCategoryId === sub.id }"
              @click="handleCategoryChange(sub.id)"
            >
              <span class="link-dot"></span>
              <span class="sub-text">{{ sub.name }}</span>
            </a>
          </div>
        </div>
      </div>

      <div class="sidebar-footer">
        <button class="btn-create" @click="showApplyModal = true">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
          提案新域
        </button>
      </div>
    </aside>

    <main class="premium-main">
      <header class="main-header">
        <div class="header-breadcrumbs">
          <span class="crumb">Codex</span>
          <span class="crumb-sep">/</span>
          <span class="crumb active">{{ currentCategoryName }}</span>
        </div>
        
        <div class="header-actions">
          <div class="search-box">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="search-icon"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg>
            <input v-model="searchQuery" placeholder="搜索全域节点..." />
            <kbd class="shortcut">⌘K</kbd>
          </div>
        </div>
      </header>

      <div class="content-scroll-area">
        <div class="page-title-box">
          <h1 class="page-title">{{ currentCategoryName }}</h1>
          <p class="page-subtitle">当前视界下共归档 {{ filteredEntries.length }} 份知识卷宗</p>
        </div>

        <component 
          :is="currentViewComponent" 
          :entries="filteredEntries" 
          @go-detail="goDetail" 
        />
      </div>
    </main>

    <Teleport to="body">
      <Transition name="drawer">
        <div v-if="showApplyModal" class="drawer-overlay" @mousedown.self="showApplyModal = false">
          <div class="drawer-content">
            <div class="drawer-header">
              <h3>提案新域</h3>
              <button class="btn-icon" @click="showApplyModal = false">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M18 6 6 18M6 6l12 12"/></svg>
              </button>
            </div>
            <div class="drawer-body">
              <div class="form-item">
                <label>领域代号</label>
                <input v-model="applyForm.name" placeholder="请输入分类名称" />
              </div>
              <div class="form-item">
                <label>收录协议 (缘由)</label>
                <textarea v-model="applyForm.reason" placeholder="描述该领域的内容边界..." rows="5"></textarea>
              </div>
            </div>
            <div class="drawer-footer">
              <button class="btn-cancel" @click="showApplyModal = false">取消</button>
              <button class="btn-primary" :disabled="isSubmitting" @click="submitApply">
                {{ isSubmitting ? '同步中...' : '提交申请' }}
              </button>
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { wikiApi } from '@/api/Wiki';

// 🌟 导入解耦后的视图子组件
import CommunityView from './components/CommunityView.vue';
import WorldviewView from './components/WorldviewView.vue';

const router = useRouter();
const searchQuery = ref('');
const showApplyModal = ref(false);
const isSubmitting = ref(false);
const applyForm = reactive({ name: '', reason: '' });

const allCategories = ref<any[]>([]);
const allEntries = ref<any[]>([]);
const currentCategoryId = ref<number | 'all'>('all');

const loadData = async () => {
  try {
    const [cats, arts] = await Promise.all([
      wikiApi.getCategories(),
      wikiApi.getAllArticles()
    ]);
    allCategories.value = cats || [];
    allEntries.value = arts || [];
  } catch (e) { console.error('Data Load Error', e); }
};

const submitApply = async () => {
  if (!applyForm.name.trim() || !applyForm.reason.trim()) return;
  isSubmitting.value = true;
  try {
    await wikiApi.applyCategory({ name: applyForm.name, reason: applyForm.reason, parentId: null, sortOrder: 0 });
    showApplyModal.value = false;
    applyForm.name = ''; applyForm.reason = '';
  } finally { isSubmitting.value = false; }
};

const handleCategoryChange = (id: number | 'all') => currentCategoryId.value = id;
const goDetail = (id: string) => router.push({ name: 'WikiDetail', params: { id } });
const resetToGlobal = () => currentCategoryId.value = 'all';

const rootCategories = computed(() => allCategories.value.filter(c => !c.parentId));
const getSubCategories = (parentId: number) => allCategories.value.filter(c => c.parentId === parentId);

const currentCategoryName = computed(() => {
  if (currentCategoryId.value === 'all') return '全知矩阵';
  return allCategories.value.find(c => c.id === currentCategoryId.value)?.name || '未命名分类';
});

const filteredEntries = computed(() => {
  let list = allEntries.value;
  
  if (currentCategoryId.value !== 'all') {
    const currentCat = allCategories.value.find(c => c.id === currentCategoryId.value);
    if (currentCat) {
      const subCatIds = getSubCategories(currentCat.id).map(c => c.id);
      list = list.filter(e => e.categoryId === currentCategoryId.value || subCatIds.includes(e.categoryId));
    }
  }
  
  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase();
    list = list.filter(e => e.title.toLowerCase().includes(q));
  }
  return list;
});

// 🌟 多态渲染核心逻辑：控制当前视界下挂载什么模版的排版布局
const currentViewComponent = computed(() => {
  // 1. 如果是在全局矩阵下，或者开启了模糊搜索，回归经典扁平列表视图
  if (currentCategoryId.value === 'all' || searchQuery.value) {
    return CommunityView;
  }

  // 2. 检索当前激活的目标节点
  const currentCat = allCategories.value.find(c => c.id === currentCategoryId.value);
  if (!currentCat) return CommunityView;

  // 3. 业务标识路由：如果大分类 ID 是 1 (世界观)，或者当前节点的上级属于世界观分区
  if (currentCat.id === 1 || currentCat.parentId === 1) {
    return WorldviewView;
  }

  // 4. 其余未特殊指定的板块（如社区知识、未分类等）默认采用普通列表
  return CommunityView;
});

onMounted(loadData);
</script>

<style scoped>
.premium-layout {
  /* 全新配色基调 */
  --c-app-bg: #f5f5f7; /* 外部背景色 */
  --c-panel-bg: #ffffff; /* 面板背景色 */
  --c-sidebar: #f5f5f7;
  --c-border: #e5e5ea;
  --c-border-hover: #d1d1d6;
  --c-text-main: #1d1d1f;
  --c-text-muted: #86868b;
  --c-text-light: #aeaeb2;
  --c-brand: #000000;
  --c-brand-hover: #333333;
  --font-sans: 'Inter', -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  --font-mono: 'JetBrains Mono', ui-monospace, monospace;

  display: flex;
  height: 100%;
  background: var(--c-app-bg);
  color: var(--c-text-main);
  font-family: var(--font-sans);
  overflow: hidden;
}

/* 侧边栏：取消右边框，依靠背景色区分 */
.premium-sidebar {
  width: 280px;
  background: var(--c-sidebar);
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  padding: 16px 12px;
}

.sidebar-header {
  height: 48px;
  display: flex;
  align-items: center;
  padding: 0 12px;
  margin-bottom: 24px;
}
.brand-logo { display: flex; align-items: center; gap: 12px; cursor: pointer; }
.logo-box { 
  width: 32px; height: 32px; 
  background: var(--c-brand); color: #fff;
  border-radius: 8px; display: flex; align-items: center; justify-content: center;
}
.logo-box svg { width: 18px; height: 18px; }
.brand-text { font-weight: 700; font-size: 1.1rem; letter-spacing: -0.02em; }

.sidebar-scroll { flex: 1; overflow-y: auto; }
.sidebar-scroll::-webkit-scrollbar { display: none; }

.nav-section { margin-bottom: 20px; }

.root-link {
  font-weight: 600; font-size: 0.9rem; color: var(--c-text-main) !important;
  padding: 8px 12px; border-radius: 8px; margin-bottom: 4px;
}
.root-link .link-icon { width: 18px; height: 18px; color: var(--c-text-muted); }
.root-link.active { background: #e8e8ed; }
.root-link.active .link-icon { color: var(--c-brand); }

.nav-link {
  display: flex; align-items: center; gap: 10px;
  cursor: pointer; transition: all 0.2s ease; user-select: none;
}
.nav-link:hover:not(.active) { background: rgba(0,0,0,0.03); }

.sub-links-group {
  padding-left: 20px; margin-top: 4px; border-left: 1px solid #d1d1d6; margin-left: 20px;
  display: flex; flex-direction: column; gap: 4px;
}
.sub-link { 
  padding: 6px 12px; font-size: 0.85rem; border-radius: 6px;
  color: var(--c-text-muted); position: relative;
}
.sub-link.active { color: var(--c-text-main); font-weight: 600; background: #e8e8ed;}

.sidebar-footer { margin-top: auto; padding-top: 16px; }
.btn-create {
  width: 100%; display: flex; align-items: center; justify-content: center; gap: 8px;
  background: var(--c-brand); color: #fff; padding: 10px 0;
  border-radius: 8px; font-size: 0.9rem; font-weight: 600;
  cursor: pointer; border: none; transition: background 0.2s;
}
.btn-create:hover { background: var(--c-brand-hover); }
.btn-create svg { width: 16px; height: 16px; }

/* 主工作区：悬浮画板设计 */
.premium-main {
  flex: 1; 
  display: flex; flex-direction: column; 
  background: var(--c-panel-bg);
  margin: 16px 16px 16px 0; /* 四周留白 */
  border-radius: 20px;      /* 大圆角 */
  box-shadow: 0 4px 24px rgba(0,0,0,0.04); /* 柔和阴影 */
  border: 1px solid rgba(0,0,0,0.05);
  overflow: hidden;
}

.main-header {
  height: 72px; padding: 0 40px;
  display: flex; align-items: center; justify-content: space-between;
  border-bottom: 1px solid rgba(0,0,0,0.04);
}

.header-breadcrumbs { display: flex; align-items: center; gap: 8px; font-size: 0.85rem; color: var(--c-text-muted); font-weight: 500;}
.crumb-sep { color: var(--c-border); }
.crumb.active { color: var(--c-text-main); }

.search-box {
  display: flex; align-items: center; background: #f5f5f7;
  border: 1px solid transparent; border-radius: 8px; padding: 0 12px;
  width: 240px; height: 36px; transition: all 0.2s;
}
.search-box:focus-within { border-color: var(--c-brand); background: #fff; box-shadow: 0 0 0 3px rgba(0,0,0,0.05); width: 280px; }
.search-icon { width: 16px; height: 16px; color: var(--c-text-muted); }
.search-box input { flex: 1; border: none; background: transparent; padding: 0 10px; font-size: 0.9rem; outline: none; }
.shortcut { font-family: var(--font-mono); font-size: 0.7rem; color: var(--c-text-muted); border: 1px solid var(--c-border); padding: 2px 6px; border-radius: 4px; background: #fff; }

.content-scroll-area { 
  flex: 1; overflow-y: auto; 
  padding: 60px 80px 100px; /* 内容区大内边距 */
}

.page-title-box { margin-bottom: 48px; }
.page-title { font-size: 2.5rem; font-weight: 800; letter-spacing: -0.04em; margin: 0 0 12px; }
.page-subtitle { font-size: 1rem; color: var(--c-text-muted); margin: 0; font-weight: 500; }

/* 抽屉样式稍作微调以匹配新主题... (保持原样即可，或者改改圆角) */
</style>