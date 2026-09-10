<template>
  <div class="lingmai-hub">
    <!-- 1. 全局顶部导航 (吸顶) -->
    <nav class="hub-navbar">
      <div class="nav-container">
        <div class="nav-left" @click="resetToGlobal">
          <div class="logo-mark">✦</div>
          <span class="brand-name">太初灵脉</span>
        </div>
        
        
      </div>
    </nav>

    <!-- 2. 巨幕探索区 (Hero Section) -->
    <header class="hub-hero">
      <div class="hero-content">
        <h1 class="hero-title">探索太初宇宙的无尽设定</h1>
        <p class="hero-subtitle">在这里检索、阅读与共同编织灵脉百科</p>
        
        <div class="hero-search-box">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="search-icon"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg>
          <input v-model="searchQuery" placeholder="搜索神明、纪元事件、功法设定..." />
          <div class="shortcut-hint">⌘K</div>
        </div>
      </div>
    </header>

    <!-- 3. 横向胶囊导航 -->
    <div class="hub-categories-sticky">
      <div class="categories-container">
        <button 
          class="cat-pill" 
          :class="{ active: currentCategoryId === 'all' }" 
          @click="handleCategoryChange('all')"
        >
          全知矩阵
        </button>
        
        <button 
          v-for="cat in allCategories" 
          :key="cat.id"
          class="cat-pill"
          :class="{ active: currentCategoryId === cat.id }"
          @click="handleCategoryChange(cat.id)"
        >
          {{ cat.name }}
        </button>
      </div>
    </div>

    <!-- 4. 居中内容流 -->
    <main class="hub-main">
      <div class="main-container">
        <component 
          :is="currentViewComponent" 
          :entries="filteredEntries" 
          @go-detail="goDetail" 
        />
      </div>
    </main>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { wikiApi } from '@/api/Wiki';
import CommunityView from './components/CommunityView.vue';
import WorldviewView from './components/WorldviewView.vue';

const router = useRouter();
const searchQuery = ref('');
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
  } catch (e) { 
    console.error('Data Load Error', e); 
  }
};

const pendingFeature = () => {
  // TODO: 后续接入 Note 系统的跳转逻辑
  console.log('跳转至 Note 发布引擎功能开发中...');
};

const handleCategoryChange = (id: number | 'all') => {
  currentCategoryId.value = id;
  searchQuery.value = '';
};

const goDetail = (id: string) => router.push({ name: 'WikiDetail', params: { id } });
const resetToGlobal = () => currentCategoryId.value = 'all';

const filteredEntries = computed(() => {
  let list = allEntries.value;
  if (currentCategoryId.value !== 'all') {
    const subCatIds = allCategories.value.filter(c => c.parentId === currentCategoryId.value).map(c => c.id);
    list = list.filter(e => e.categoryId === currentCategoryId.value || subCatIds.includes(e.categoryId));
  }
  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase();
    list = list.filter(e => e.title.toLowerCase().includes(q));
  }
  return list;
});

const currentViewComponent = computed(() => {
  if (currentCategoryId.value === 'all' || searchQuery.value) return CommunityView;
  const currentCat = allCategories.value.find(c => c.id === currentCategoryId.value);
  if (currentCat && (currentCat.id === 1 || currentCat.parentId === 1)) return WorldviewView;
  return CommunityView;
});

onMounted(loadData);
</script>

<style scoped>
/* 保持上一版本的所有样式不变 */
.lingmai-hub {
  --bg-main: #ffffff;
  --bg-offset: #f9fafb;
  --text-h: #111827;
  --text-p: #4b5563;
  --border: #e5e7eb;
  --brand: #000000;
  --brand-hover: #374151;
  --max-width: 1100px; 
  
  min-height: 100vh;
  background: var(--bg-main);
  color: var(--text-h);
  font-family: 'Inter', -apple-system, sans-serif;
  display: flex;
  flex-direction: column;
}

.hub-navbar {
  position: sticky;
  top: 0;
  z-index: 50;
  background: rgba(255, 255, 255, 0.8);
  backdrop-filter: blur(12px);
  border-bottom: 1px solid var(--border);
}
.nav-container {
  max-width: var(--max-width);
  margin: 0 auto;
  height: 64px;
  padding: 0 24px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.nav-left { display: flex; align-items: center; gap: 12px; cursor: pointer; }
.logo-mark { background: var(--brand); color: #fff; width: 28px; height: 28px; display: flex; align-items: center; justify-content: center; border-radius: 6px; font-size: 14px; }
.brand-name { font-weight: 700; font-size: 1.1rem; letter-spacing: -0.02em; }
.nav-right { display: flex; align-items: center; gap: 24px; }
.user-stats { font-size: 0.85rem; color: var(--text-p); font-weight: 500; }
.btn-create {
  background: var(--brand); color: #fff; border: none; padding: 8px 16px; 
  border-radius: 20px; font-size: 0.85rem; font-weight: 600; cursor: pointer;
  display: flex; align-items: center; gap: 6px; transition: transform 0.2s;
}
.btn-create:hover { transform: translateY(-1px); box-shadow: 0 4px 12px rgba(0,0,0,0.1); }

.hub-hero {
  padding: 80px 24px 60px;
  background: linear-gradient(180deg, var(--bg-offset) 0%, var(--bg-main) 100%);
  text-align: center;
}
.hero-content { max-width: 680px; margin: 0 auto; }
.hero-title { font-size: 3rem; font-weight: 800; letter-spacing: -0.04em; margin: 0 0 16px; font-family: "Noto Serif SC", serif; }
.hero-subtitle { font-size: 1.1rem; color: var(--text-p); margin: 0 0 40px; }
.hero-search-box {
  position: relative; display: flex; align-items: center;
  background: #fff; border: 1px solid var(--border); border-radius: 16px;
  padding: 8px 16px; box-shadow: 0 8px 24px rgba(0,0,0,0.04);
  transition: all 0.2s;
}
.hero-search-box:focus-within { border-color: var(--brand); box-shadow: 0 8px 32px rgba(0,0,0,0.08); transform: translateY(-2px); }
.hero-search-box .search-icon { width: 20px; height: 20px; color: #9ca3af; margin-right: 12px; }
.hero-search-box input { flex: 1; border: none; font-size: 1.1rem; padding: 12px 0; outline: none; color: var(--text-h); background: transparent; }
.shortcut-hint { font-size: 0.75rem; color: #9ca3af; border: 1px solid var(--border); padding: 4px 8px; border-radius: 6px; background: var(--bg-offset); }

.hub-categories-sticky {
  position: sticky; top: 64px; z-index: 40;
  background: rgba(255,255,255,0.9); backdrop-filter: blur(8px);
  border-bottom: 1px solid var(--border);
}
.categories-container {
  max-width: var(--max-width); margin: 0 auto; padding: 0 24px;
  display: flex; gap: 8px; overflow-x: auto; scrollbar-width: none;
  align-items: center; height: 60px;
}
.categories-container::-webkit-scrollbar { display: none; }
.cat-pill {
  background: transparent; border: 1px solid transparent; color: var(--text-p);
  padding: 6px 16px; border-radius: 20px; font-size: 0.9rem; font-weight: 500;
  cursor: pointer; white-space: nowrap; transition: all 0.2s;
}
.cat-pill:hover { background: var(--bg-offset); color: var(--text-h); }
.cat-pill.active { background: var(--brand); color: #fff; }

.hub-main { flex: 1; padding: 40px 24px 100px; }
.main-container { max-width: var(--max-width); margin: 0 auto; }
</style>