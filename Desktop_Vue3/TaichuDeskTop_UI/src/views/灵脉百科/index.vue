<template>
  <div class="codex-index" :class="{ 'is-space-mode': currentSpaceId !== 'all' }">
    <header class="codex-header">
      <div class="header-inner">
        <div class="brand-path" @click="resetToGlobal">
          <span class="brand-name">灵脉百科</span>
          <span class="path-sep">/</span>
          <span class="current-space">{{ activeSpaceName }}</span>
        </div>
        <div class="search-box">
          <input ref="searchInput" v-model="searchQuery" placeholder="搜索词条..." spellcheck="false" />
          <span class="k-hint">⌘ K</span>
        </div>
      </div>
    </header>

    <div class="codex-container">
      
      <aside v-if="currentSpaceId !== 'all'" class="codex-sidebar">
        <div class="sidebar-header">设定目录</div>
        <div class="directory-tree">
          <div v-for="cat in MAJOR_CATEGORIES.slice(1)" :key="cat.id" class="dir-group">
            <label class="dir-label">{{ cat.label }}</label>
            <div class="dir-items">
              <div 
                v-for="item in getEntriesByCat(cat.id)" 
                :key="item.id"
                class="dir-item"
                @click="goDetail(item.id)"
              >
                <span class="dir-dot"></span> {{ item.title }}
              </div>
            </div>
          </div>
        </div>
      </aside>

      <main class="codex-content">
        <nav class="space-axis" v-if="currentSpaceId === 'all'">
          <div class="axis-items">
            <span class="axis-btn" :class="{ active: currentSpaceId === 'all' }" @click="handleSpaceChange('all')">全域</span>
            <span v-for="s in allSpaces" :key="s.id" class="axis-btn" @click="handleSpaceChange(s.id)">{{ s.name }}</span>
          </div>
        </nav>

        <div class="filter-layer" v-if="currentSpaceId === 'all'">
          <nav class="major-nav">
            <div class="nav-links">
              <span v-for="c in MAJOR_CATEGORIES" :key="c.id" :class="['nav-link', { active: currentCategory === c.id }]" @click="handleCategoryChange(c.id)">{{ c.label }}</span>
            </div>
          </nav>
          <div class="tag-bar" v-if="availableTags.length > 0">
            <div class="tag-scroll">
              <span v-for="tag in availableTags" :key="tag" :class="['tag-pill', { active: selectedTags.includes(tag) }]" @click="toggleTag(tag)">#{{ tag }}</span>
            </div>
          </div>
        </div>

        <div :class="['entry-display', currentSpaceId !== 'all' ? 'list-view' : 'grid-view']">
          <div v-for="entry in filteredEntries" :key="entry.id" class="entry-item" @click="goDetail(entry.id)">
            <div class="entry-meta">
              <span class="space-tag" v-if="currentSpaceId === 'all'">{{ entry.spaceName }}</span>
              <span class="type-tag" :class="entry.category">{{ getCategoryLabel(entry.category) }}</span>
            </div>
            <h2 class="entry-title">{{ entry.title }}</h2>
            <p class="entry-excerpt">{{ entry.excerpt }}</p>
            <div class="entry-footer">
              <span class="date">{{ formatDate(entry.publishedAt) }}</span>
            </div>
          </div>
        </div>

        <div v-if="filteredEntries.length === 0" class="codex-empty">暂无编织迹象</div>
      </main>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
// 🌟 1. 引入你刚刚确认的 API 封装
import { lingmaiApi } from '../../api/lingmai'; 

// --- 基础配置 ---
const router = useRouter();
const searchInput = ref<HTMLInputElement | null>(null);
const MAJOR_CATEGORIES = [
  { id: 'all', label: '全部' },
  { id: 'wiki', label: '世界观' }, // 🌟 这里的 ID 必须和发布时的 type='wiki' 对应
  { id: 'char', label: '角色' },
  { id: 'community', label: '社区知识' }
];

// --- 状态 ---
const currentSpaceId = ref('all');
const currentCategory = ref('all');
const selectedTags = ref<string[]>([]);
const searchQuery = ref('');
const allSpaces = ref<any[]>([]);
const allEntries = ref<any[]>([]);
const isLoading = ref(false); // 🌟 增加加载状态

// --- 🌟 核心逻辑：接入真实数据大脑 ---

/**
 * 🌟 拉取并编织真实的百科数据流
 */
const loadWikiData = async () => {
  isLoading.value = true;
  try {
    // 同时请求“公开流”和“空间列表”
    const [entriesRes, spacesRes]: any = await Promise.all([
      lingmaiApi.getPublicStream('wiki'), // 传入 'wiki' 参数
      lingmaiApi.getSpaces()
    ]);
    

    const publishedSpaceIds = new Set(entriesRes.map((e: any) => e.spaceId));
    allSpaces.value = spacesRes.filter((s: any) => publishedSpaceIds.has(s.id));

    // 🌟 数据归一化：将后端字段映射到前端模板使用的 category, excerpt 等
    allEntries.value = (entriesRes || []).map((n: any) => ({
      id: n.id,
      title: n.title,
      excerpt: n.excerpt || '灵脉深处暂无文字回响...',
      category: n.type, // 🌟 关键：后端返回的 type (wiki/char) 对应前端的 category
      spaceId: n.spaceId,
      spaceName: spacesRes.find((s: any) => s.id === n.spaceId)?.name || '未知位面',
      publishedAt: n.publishedAt,
      tags: n.tags ? n.tags.split(',') : [] // 如果后端存的是逗号分隔字符串，则拆分
    }));

   
  } catch (err) {
    console.error('灵脉百科感应失败:', err);
  } finally {
    isLoading.value = false;
  }
};

// --- 其他计算属性与交互逻辑保持不变 ---
const activeSpaceName = computed(() => {
  if (currentSpaceId.value === 'all') return '广场';
  return allSpaces.value.find(s => s.id === currentSpaceId.value)?.name || '未知位面';
});

const getEntriesByCat = (catId: string) => {
  return allEntries.value.filter(e => e.spaceId === currentSpaceId.value && e.category === catId);
};

const availableTags = computed(() => {
  let list = allEntries.value.filter(e => currentSpaceId.value === 'all' || e.spaceId === currentSpaceId.value);
  const tags = new Set<string>();
  list.forEach(e => e.tags?.forEach((t: string) => tags.add(t)));
  return Array.from(tags).sort();
});

/**
 * 🌟 1. 补全标签切换逻辑
 * 实现点击标签时，在 selectedTags 数组中进行“添加/移除”的切换
 */
const toggleTag = (t: string) => {
  const index = selectedTags.value.indexOf(t);
  if (index > -1) {
    selectedTags.value.splice(index, 1); // 如果已存在则移除
  } else {
    selectedTags.value.push(t); // 如果不存在则添加
  }
};

/**
 * 🌟 2. 增强过滤逻辑
 * 确保 filteredEntries 不仅过滤空间、分类和搜索，还要过滤选中的标签
 */
const filteredEntries = computed(() => {
  return allEntries.value.filter(e => {
    // 空间匹配
    const mSpace = currentSpaceId.value === 'all' || e.spaceId === currentSpaceId.value;
    // 分类匹配
    const mCat = currentCategory.value === 'all' || e.category === currentCategory.value;
    // 搜索匹配
    const mSearch = !searchQuery.value || e.title.includes(searchQuery.value);
    
    // 🌟 标签匹配逻辑：
    // 如果没有选中任何标签，则全选；如果选中了标签，则要求词条必须包含所有选中的标签
    const mTags = selectedTags.value.length === 0 || 
                   selectedTags.value.every(tag => e.tags?.includes(tag));
    
    return mSpace && mCat && mSearch && mTags;
  });
});

const getCategoryLabel = (id: string) => MAJOR_CATEGORIES.find(c => c.id === id)?.label || '词条';
const formatDate = (s: string) => s ? s.substring(0, 10).replace(/-/g, '.') : '';
const goDetail = (id: string) => router.push({ name: 'WikiDetail', params: { id } });
const handleSpaceChange = (id: string) => { currentSpaceId.value = id; selectedTags.value = []; };
const handleCategoryChange = (id: string) => { currentCategory.value = id; };
const resetToGlobal = () => { currentSpaceId.value = 'all'; };

onMounted(() => {
  // 🌟 彻底告别死数据，开始感应真实的彼岸宇宙
  loadWikiData();
  
  window.addEventListener('keydown', e => { 
    if ((e.metaKey || e.ctrlKey) && e.key === 'k') { 
      e.preventDefault(); 
      searchInput.value?.focus(); 
    } 
  });
});
</script>

<style scoped>
.codex-index { min-height: 100vh; background: #ffffff; color: #1d1d1f; font-family: sans-serif; }

/* 头部样式 */
.codex-header { padding: 60px 40px 20px; }
.header-inner { display: flex; justify-content: space-between; border-bottom: 1.5px solid #1d1d1f; padding-bottom: 12px; }
.brand-name { font-size: 20px; font-weight: 700; cursor: pointer; }
.path-sep { margin: 0 10px; color: #d2d2d7; }
.current-space { font-size: 14px; color: #86868b; }
.search-box input { border: none; outline: none; text-align: right; width: 150px; font-size: 13px; }

/* 核心布局切换 */
.codex-container { display: flex; padding: 0 40px; }

/* 🌟 侧边栏：典型的 Wiki 目录感 */
.codex-sidebar {
  width: 240px; border-right: 1px solid #f2f2f7; 
  padding: 40px 24px 40px 0; height: calc(100vh - 120px);
  position: sticky; top: 120px; overflow-y: auto;
}
.sidebar-header { font-size: 11px; color: #c7c7cc; text-transform: uppercase; letter-spacing: 0.1em; margin-bottom: 24px; }
.dir-group { margin-bottom: 32px; }
.dir-label { font-size: 13px; font-weight: 700; color: #1d1d1f; display: block; margin-bottom: 12px; }
.dir-item { font-size: 13px; color: #86868b; padding: 6px 0; cursor: pointer; transition: color 0.2s; display: flex; align-items: center; }
.dir-item:hover { color: #0066cc; }
.dir-dot { width: 4px; height: 4px; background: #d2d2d7; border-radius: 50%; margin-right: 8px; }

/* 主内容区 */
.codex-content { flex: 1; padding: 40px 0 40px 40px; }
.is-space-mode .codex-content { padding-left: 60px; }

/* 空间轴 */
.space-axis { margin-bottom: 40px; border-bottom: 1px solid #f2f2f7; padding-bottom: 20px; }
.axis-items { display: flex; gap: 24px; }
.axis-btn { font-size: 14px; color: #86868b; cursor: pointer; }
.axis-btn.active { color: #1d1d1f; font-weight: 700; }

/* 列表展现切换 */
.grid-view { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 40px; }
.list-view { max-width: 800px; }
.list-view .entry-item { border-bottom: 1px solid #f9f9f9; padding: 24px 0; margin: 0; }
.list-view .entry-title { font-size: 22px; }

.entry-item { cursor: pointer; }
.entry-meta { display: flex; justify-content: space-between; margin-bottom: 12px; font-size: 10px; font-weight: 700; text-transform: uppercase; }
.space-tag { color: #0066cc; }
.entry-title { font-size: 18px; font-weight: 700; margin-bottom: 12px; }
.entry-excerpt { font-size: 14px; color: #86868b; line-height: 1.6; }
.entry-footer { margin-top: 16px; font-size: 10px; color: #c7c7cc; }
</style>