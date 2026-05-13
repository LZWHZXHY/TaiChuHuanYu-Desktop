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
          <input ref="searchInput" v-model="searchQuery" placeholder="搜索..." spellcheck="false" />
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
            <div class="dir-items-wrapper"> <div 
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
        <nav class="scroll-nav-wrapper" v-if="currentSpaceId === 'all'">
          <div class="axis-items">
            <span 
              class="axis-btn" 
              :class="{ active: currentSpaceId === 'all' }" 
              @click="handleSpaceChange('all')"
            >全域</span>
            <span 
              v-for="s in allSpaces" 
              :key="s.id" 
              class="axis-btn" 
              :class="{ active: currentSpaceId === s.id }"
              @click="handleSpaceChange(s.id)"
            >{{ s.name }}</span>
          </div>
        </nav>

        <div class="filter-layer">
          <nav class="major-nav">
            <div class="nav-links">
              <span 
                v-for="c in MAJOR_CATEGORIES" 
                :key="c.id" 
                :class="['nav-link', { active: currentCategory === c.id }]" 
                @click="handleCategoryChange(c.id)"
              >{{ c.label }}</span>
            </div>
          </nav>
          
          <div class="tag-bar" v-if="availableTags.length > 0">
            <div class="tag-scroll">
              <span 
                v-for="tag in availableTags" 
                :key="tag" 
                :class="['tag-pill', { active: selectedTags.includes(tag) }]" 
                @click="toggleTag(tag)"
              >#{{ tag }}</span>
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
            
            <div class="entry-excerpt-container">
              <SpiritPreview :modelValue="entry.excerpt" class="mini-renderer" />
            </div>

            <div class="entry-footer">
              <span class="date">{{ formatDate(entry.publishedAt) }}</span>
            </div>
          </div>
        </div>

        <div v-if="filteredEntries.length === 0 && !isLoading" class="codex-empty">
          暂无编织迹象
        </div>
      </main>
    </div>
  </div>
</template>

<script setup lang="ts">
/* 原有 JS 逻辑完全保持不变 */
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { wikiApi } from '@/api/Wiki'; 
import SpiritPreview from '@/components/SpiritTextComponents/SpiritPreview.vue';

const router = useRouter();
const searchInput = ref<HTMLInputElement | null>(null);
const MAJOR_CATEGORIES = [
  { id: 'all', label: '全部' },
  { id: 'wiki', label: '世界观' },
  { id: 'char', label: '角色' },
  { id: 'community', label: '社区知识' }
];

const currentSpaceId = ref('all');
const currentCategory = ref('all');
const selectedTags = ref<string[]>([]);
const searchQuery = ref('');
const allSpaces = ref<any[]>([]);
const allEntries = ref<any[]>([]);
const isLoading = ref(false);

const loadWikiData = async () => {
  isLoading.value = true;
  try {
    const [entriesRes, spacesRes]: any = await Promise.all([
      wikiApi.getPublicStream('wiki'),
      wikiApi.getWikiSpaces()
    ]);
    const publishedSpaceIds = new Set(entriesRes.map((e: any) => e.spaceId));
    allSpaces.value = spacesRes.filter((s: any) => publishedSpaceIds.has(s.id));
    allEntries.value = (entriesRes || []).map((n: any) => ({
      id: n.id,
      title: n.title,
      excerpt: n.excerpt,
      category: n.type,
      spaceId: n.spaceId,
      spaceName: spacesRes.find((s: any) => s.id === n.spaceId)?.name || '未知位面',
      publishedAt: n.publishedAt,
      tags: n.tags ? n.tags.split(',') : []
    }));
  } catch (err) { console.error(err); } finally { isLoading.value = false; }
};

const activeSpaceName = computed(() => {
  if (currentSpaceId.value === 'all') return '广场';
  return allSpaces.value.find(s => s.id === currentSpaceId.value)?.name || '未知位面';
});

const getEntriesByCat = (catId: string) => allEntries.value.filter(e => e.spaceId === currentSpaceId.value && e.category === catId);
const availableTags = computed(() => {
  let list = allEntries.value.filter(e => currentSpaceId.value === 'all' || e.spaceId === currentSpaceId.value);
  const tags = new Set<string>();
  list.forEach(e => e.tags?.forEach((t: string) => tags.add(t)));
  return Array.from(tags).sort();
});
const toggleTag = (t: string) => {
  const index = selectedTags.value.indexOf(t);
  if (index > -1) selectedTags.value.splice(index, 1);
  else selectedTags.value.push(t);
};
const filteredEntries = computed(() => allEntries.value.filter(e => {
  const mSpace = currentSpaceId.value === 'all' || e.spaceId === currentSpaceId.value;
  const mCat = currentCategory.value === 'all' || e.category === currentCategory.value;
  const mSearch = !searchQuery.value || e.title.includes(searchQuery.value);
  const mTags = selectedTags.value.length === 0 || selectedTags.value.every(tag => e.tags?.includes(tag));
  return mSpace && mCat && mSearch && mTags;
}));
const getCategoryLabel = (id: string) => MAJOR_CATEGORIES.find(c => c.id === id)?.label || '词条';
const formatDate = (s: string) => s ? s.substring(0, 10).replace(/-/g, '.') : '';
const goDetail = (id: string) => router.push({ name: 'WikiDetail', params: { id } });
const handleSpaceChange = (id: string) => { currentSpaceId.value = id; selectedTags.value = []; };
const handleCategoryChange = (id: string) => { currentCategory.value = id; };
const resetToGlobal = () => { currentSpaceId.value = 'all'; };

onMounted(() => {
  loadWikiData();
  window.addEventListener('keydown', e => { 
    if ((e.metaKey || e.ctrlKey) && e.key === 'k') { e.preventDefault(); searchInput.value?.focus(); } 
  });
});
</script>

<style scoped>
@import "@/components/SpiritTextComponents/spirit-typography.css";

/* --- 1. 核心设计语言：白纸黑字 --- */
.codex-index { 
  min-height: 100vh; 
  background: #ffffff; 
  color: #1a1a1a;
  line-height: 1.6;
  -webkit-font-smoothing: antialiased; /* 极致清晰度 */
}

/* --- 2. 头部：极细线条与留白 --- */
.codex-header { 
  padding: 60px 0 20px;
  max-width: 1100px;
  margin: 0 auto;
}
.header-inner { 
  display: flex; 
  justify-content: space-between; 
  align-items: baseline; 
  border-bottom: 1px solid #000000; /* 唯一的一条重色线条，确定视觉基准 */
  padding: 0 20px 10px; 
}
.brand-name { font-size: 18px; font-weight: 800; cursor: pointer; letter-spacing: -0.01em; }
.current-space { font-size: 13px; color: #888; margin-left: 8px; font-weight: 400; }

.search-box input { 
  border: none; 
  background: transparent;
  font-size: 13px;
  text-align: right;
  width: 150px;
  transition: all 0.2s;
}
.search-box input:focus { outline: none; width: 220px; }
.k-hint { font-size: 10px; color: #ccc; margin-left: 8px; border: 1px solid #eee; padding: 1px 3px; border-radius: 3px; }

/* --- 3. 布局：大留白容器 --- */
.codex-container { 
  max-width: 1100px; 
  margin: 0 auto; 
  display: flex; 
  padding: 40px 20px; 
}

/* --- 4. 侧边栏：极简目录 --- */
.codex-sidebar {
  width: 200px; 
  padding-right: 40px;
  position: sticky; 
  top: 40px; 
  height: fit-content;
}
.sidebar-header { font-size: 11px; color: #bbb; letter-spacing: 0.1em; margin-bottom: 30px; }
.dir-group { margin-bottom: 32px; }
.dir-label { font-size: 12px; font-weight: 600; color: #000; margin-bottom: 12px; display: block; }
.dir-item { 
  font-size: 13px; color: #666; padding: 5px 0; 
  cursor: pointer; transition: color 0.2s;
}
.dir-item:hover { color: #000; }

/* --- 5. 主内容区：专注阅读 --- */
.codex-content { 
  flex: 1; 
  max-width: 720px; /* 🌟 黄金阅读宽度：防止视线水平移动过长导致疲劳 */
  padding-bottom: 100px;
}

/* 导航：文字链接化 */
.axis-items, .nav-links {
  display: flex; gap: 24px; margin-bottom: 40px; border-bottom: 1px solid #f2f2f2;
}
.axis-btn, .nav-link { 
  font-size: 14px; color: #888; cursor: pointer; padding-bottom: 12px;
  position: relative;
}
.axis-btn.active, .nav-link.active { 
  color: #000; font-weight: 600;
}
.axis-btn.active::after, .nav-link.active::after {
  content: ''; position: absolute; bottom: -1px; left: 0; width: 100%; height: 1px; background: #000;
}

/* 标签：克制的颗粒感 */
.tag-bar { margin-bottom: 40px; }
.tag-scroll { display: flex; gap: 10px; overflow-x: auto; }
.tag-pill { 
  font-size: 12px; color: #666; cursor: pointer;
  border: 1px solid #eee; padding: 3px 10px; transition: all 0.2s;
}
.tag-pill.active { border-color: #000; color: #000; }

/* --- 6. 词条流：文档列表感 --- */
.entry-display { display: flex; flex-direction: column; }
.entry-item { 
  padding: 32px 0; 
  border-bottom: 1px solid #f2f2f2; 
  cursor: pointer;
  transition: opacity 0.2s;
}
.entry-item:hover { opacity: 0.7; } /* 极简交互：仅改变透明度 */

.entry-meta { display: flex; gap: 12px; margin-bottom: 12px; font-size: 11px; color: #888; }
.space-tag { font-weight: 600; color: #000; }
.type-tag { font-style: italic; }

.entry-title { font-size: 22px; font-weight: 700; margin-bottom: 12px; letter-spacing: -0.02em; }

/* 摘要：纯粹的文字流 */
.entry-excerpt-container {
  font-size: 14px; color: #444; line-height: 1.8;
  max-height: 75px; overflow: hidden;
  mask-image: linear-gradient(to bottom, black 50%, transparent 100%);
  -webkit-mask-image: linear-gradient(to bottom, black 50%, transparent 100%);
  margin-bottom: 16px;
}
.mini-renderer { zoom: 1; } /* 保持原比例，确保文字感 */

.entry-footer { font-size: 12px; color: #bbb; font-family: monospace; }

/* --- 7. 📱 手机端：极致克制 --- */
@media (max-width: 768px) {
  .codex-header { padding: 40px 20px 10px; }
  .codex-container { padding: 0 20px; flex-direction: column; }
  .codex-sidebar { 
    width: 100%; border-bottom: 1px solid #f2f2f2; 
    padding: 0 0 20px 0; margin-bottom: 20px;
    display: none; /* 🌟 手机端隐藏侧边栏目录以保证首屏留白 */
  }
  .codex-content { padding-left: 0; }
  .entry-title { font-size: 20px; }
  .k-hint { display: none; }
}
</style>