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

        <div class="doc-list">
          <div 
            v-for="entry in filteredEntries" 
            :key="entry.id" 
            class="doc-row"
            @click="goDetail(entry.id)"
          >
            <div class="doc-ref">
              <span class="ref-badge">{{ String(entry.id).substring(0, 6).toUpperCase() }}</span>
            </div>
            
            <div class="doc-body">
              <h3 class="doc-title">{{ entry.title }}</h3>
              <div class="doc-excerpt">
                <SpiritPreview :modelValue="parseJson(entry.excerpt)" />
              </div>
            </div>
            
            <div class="doc-action">
              <div class="action-arrow">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M5 12h14M12 5l7 7-7 7"/></svg>
              </div>
            </div>
          </div>

          <div v-if="filteredEntries.length === 0" class="empty-state">
            <div class="empty-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>
            </div>
            <h4>知识荒原</h4>
            <p>该节点或其下辖子域暂无任何公开文档</p>
          </div>
        </div>
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
import SpiritPreview from '@/components/SpiritTextComponents/SpiritPreview.vue';

const router = useRouter();
const searchQuery = ref('');
const showApplyModal = ref(false);
const isSubmitting = ref(false);
const applyForm = reactive({ name: '', reason: '' });

const allCategories = ref<any[]>([]);
const allEntries = ref<any[]>([]);
const currentCategoryId = ref<number | 'all'>('all');

const parseJson = (content: any) => {
  if (!content) return '';
  if (typeof content === 'string') {
    try { return JSON.parse(content); } catch { return content; }
  }
  return content;
};

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

// 🌟 联动核心算法：当选中大分类时，自动穿透并拉取其下辖的所有子分类文章
const filteredEntries = computed(() => {
  let list = allEntries.value;
  
  if (currentCategoryId.value !== 'all') {
    const currentCat = allCategories.value.find(c => c.id === currentCategoryId.value);
    if (currentCat) {
      // 找出当前分类下所有的子分类 ID 数组
      const subCatIds = getSubCategories(currentCat.id).map(c => c.id);
      
      // 过滤条件：文章属于当前大分类，或者文章属于其下属的任何一个子分类
      list = list.filter(e => e.categoryId === currentCategoryId.value || subCatIds.includes(e.categoryId));
    }
  }
  
  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase();
    list = list.filter(e => e.title.toLowerCase().includes(q));
  }
  return list;
});

onMounted(loadData);
</script>

<style scoped>
.premium-layout {
  --c-bg: #ffffff;
  --c-sidebar: #fbfbfc;
  --c-border: #f0f0f0;
  --c-border-hover: #e4e4e7;
  --c-text-main: #09090b;
  --c-text-muted: #71717a;
  --c-text-light: #a1a1aa;
  --c-brand: #000000;
  --font-sans: 'Inter', -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  --font-mono: 'JetBrains Mono', ui-monospace, SFMono-Regular, monospace;

  display: flex;
  height: 100%;
  background: var(--c-bg);
  color: var(--c-text-main);
  font-family: var(--font-sans);
  overflow: hidden;
}

/* 左侧结构化导航 */
.premium-sidebar {
  width: 260px;
  background: var(--c-sidebar);
  border-right: 1px solid var(--c-border);
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
}

.sidebar-header {
  height: 60px;
  display: flex;
  align-items: center;
  padding: 0 20px;
  border-bottom: 1px solid var(--c-border);
}
.brand-logo { display: flex; align-items: center; gap: 10px; cursor: pointer; }
.logo-box { width: 24px; height: 24px; color: var(--c-brand); }
.brand-text { font-weight: 600; font-size: 0.95rem; letter-spacing: -0.01em; }

.sidebar-scroll {
  flex: 1;
  overflow-y: auto;
  padding: 24px 12px;
}
.sidebar-scroll::-webkit-scrollbar { display: none; }

.nav-section { margin-bottom: 12px; }

/* 🌟 修改：大分类链接样式（更稳重、带明显的点击态） */
.root-link {
  font-weight: 600;
  font-size: 0.9rem;
  color: #18181b !important;
  margin-bottom: 4px;
}
.root-link .link-icon {
  width: 16px;
  height: 16px;
  color: var(--c-text-muted);
}
.root-link.active .link-icon {
  color: var(--c-brand);
}

.nav-link {
  display: flex; align-items: center; gap: 10px;
  padding: 8px 12px; border-radius: 6px; font-size: 0.9rem;
  color: var(--c-text-muted); cursor: pointer;
  transition: all 0.15s ease; user-select: none;
}
.nav-link:hover { background: #f4f4f5; color: var(--c-text-main); }
.nav-link.active { background: #efeef0; color: var(--c-text-main); font-weight: 600; }

/* 子分类树形排版缩进 */
.sub-links-group {
  padding-left: 12px;
  margin-top: 2px;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.sub-link { 
  padding-left: 16px; 
  font-size: 0.85rem; 
  position: relative;
}
.link-dot { width: 4px; height: 4px; border-radius: 50%; background: #e4e4e7; transition: all 0.2s; }
.sub-link:hover .link-dot { background: var(--c-text-light); }
.sub-link.active .link-dot { background: var(--c-brand); transform: scale(1.5); }

.sidebar-footer { padding: 16px; border-top: 1px solid var(--c-border); }
.btn-create {
  width: 100%; display: flex; align-items: center; justify-content: center; gap: 8px;
  background: #fff; border: 1px solid var(--c-border); padding: 8px 0;
  border-radius: 6px; font-size: 0.85rem; font-weight: 500; color: var(--c-text-main);
  cursor: pointer; box-shadow: 0 1px 2px rgba(0,0,0,0.02); transition: all 0.2s;
}
.btn-create:hover { border-color: var(--c-text-muted); box-shadow: 0 2px 4px rgba(0,0,0,0.04); }
.btn-create svg { width: 16px; height: 16px; }

/* 右侧主工作区 */
.premium-main {
  flex: 1; display: flex; flex-direction: column; min-width: 0;
}

.main-header {
  height: 60px; padding: 0 32px;
  display: flex; align-items: center; justify-content: space-between;
  border-bottom: 1px solid var(--c-border);
  background: rgba(255, 255, 255, 0.8);
  backdrop-filter: blur(12px); z-index: 10;
}

.header-breadcrumbs { display: flex; align-items: center; gap: 8px; font-size: 0.85rem; color: var(--c-text-muted); }
.crumb-sep { color: var(--c-border); }
.crumb.active { color: var(--c-text-main); font-weight: 500; }

.search-box {
  display: flex; align-items: center; background: var(--c-sidebar);
  border: 1px solid var(--c-border); border-radius: 6px; padding: 0 12px;
  width: 280px; height: 32px; transition: border-color 0.2s;
}
.search-box:focus-within { border-color: var(--c-text-muted); background: #fff; }
.search-icon { width: 14px; height: 14px; color: var(--c-text-light); }
.search-box input { flex: 1; border: none; background: transparent; padding: 0 8px; font-size: 0.85rem; outline: none; }
.shortcut { font-family: var(--font-mono); font-size: 0.7rem; color: var(--c-text-light); border: 1px solid var(--c-border); padding: 1px 4px; border-radius: 4px; background: #fff; }

.content-scroll-area { flex: 1; overflow-y: auto; padding: 48px 64px 100px; }
.page-title-box { margin-bottom: 40px; }
.page-title { font-size: 2.2rem; font-weight: 700; letter-spacing: -0.03em; margin: 0 0 8px; }
.page-subtitle { font-size: 0.95rem; color: var(--c-text-muted); margin: 0; }

/* 列表排版 */
.doc-list { display: flex; flex-direction: column; border-top: 1px solid var(--c-border); }
.doc-row { display: flex; padding: 24px 0; border-bottom: 1px solid var(--c-border); cursor: pointer; transition: background 0.2s; }
.doc-row:hover { background: #fafafa; }

.doc-ref { width: 100px; flex-shrink: 0; padding-top: 4px; }
.ref-badge { font-family: var(--font-mono); font-size: 0.75rem; color: var(--c-text-light); }

.doc-body { flex: 1; min-width: 0; padding-right: 40px; }
.doc-title { font-size: 1.25rem; font-weight: 600; margin: 0 0 12px; color: var(--c-text-main); letter-spacing: -0.01em; }
.doc-excerpt {
  font-size: 0.95rem; line-height: 1.6; color: var(--c-text-muted);
  max-height: 48px; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical;
}

.doc-action { display: flex; align-items: center; justify-content: flex-end; width: 60px; }
.action-arrow {
  width: 36px; height: 36px; border-radius: 50%; display: flex; align-items: center; justify-content: center;
  border: 1px solid transparent; color: var(--c-text-light); transition: all 0.3s;
}
.action-arrow svg { width: 18px; height: 18px; }
.doc-row:hover .action-arrow { border-color: var(--c-border-hover); color: var(--c-text-main); background: #fff; transform: translateX(4px); }

.empty-state { text-align: center; padding: 80px 0; color: var(--c-text-muted); }
.empty-icon { width: 48px; height: 48px; margin: 0 auto 16px; color: var(--c-border-hover); }
.empty-icon svg { width: 100%; height: 100%; }
.empty-state h4 { font-size: 1rem; color: var(--c-text-main); margin: 0 0 8px; font-weight: 500; }

/* 侧边抽屉 */
.drawer-overlay { position: fixed; inset: 0; background: rgba(0,0,0,0.1); backdrop-filter: blur(2px); z-index: 1000; display: flex; justify-content: flex-end; }
.drawer-content { width: 400px; height: 100vh; background: #fff; box-shadow: -10px 0 30px rgba(0,0,0,0.05); display: flex; flex-direction: column; }
.drawer-header { padding: 24px 32px; display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid var(--c-border); }
.drawer-header h3 { margin: 0; font-size: 1.1rem; font-weight: 600; }
.btn-icon { background: none; border: none; cursor: pointer; color: var(--c-text-muted); padding: 4px; }
.btn-icon svg { width: 20px; height: 20px; }

.drawer-body { flex: 1; padding: 32px; display: flex; flex-direction: column; gap: 24px; overflow-y: auto; }
.form-item { display: flex; flex-direction: column; gap: 8px; }
.form-item label { font-size: 0.85rem; font-weight: 500; color: var(--c-text-main); }
.form-item input, .form-item textarea { padding: 10px 12px; border: 1px solid var(--c-border); border-radius: 6px; font-family: var(--font-sans); font-size: 0.9rem; outline: none; transition: border-color 0.2s; }
.form-item input:focus, .form-item textarea:focus { border-color: var(--c-brand); }

.drawer-footer { padding: 24px 32px; border-top: 1px solid var(--c-border); display: flex; justify-content: flex-end; gap: 12px; }
.btn-cancel { padding: 10px 16px; background: #fff; border: 1px solid var(--c-border); border-radius: 6px; cursor: pointer; font-size: 0.9rem; }
.btn-primary { padding: 10px 24px; background: var(--c-brand); color: #fff; border: none; border-radius: 6px; cursor: pointer; font-size: 0.9rem; font-weight: 500; }
.btn-primary:disabled { opacity: 0.6; }

.drawer-enter-active, .drawer-leave-active { transition: all 0.3s cubic-bezier(0.16, 1, 0.3, 1); }
.drawer-enter-from, .drawer-leave-to { opacity: 0; }
.drawer-enter-from .drawer-content, .drawer-leave-to .drawer-content { transform: translateX(100%); }

@media (max-width: 1024px) {
  .premium-sidebar { display: none; }
  .content-scroll-area { padding: 32px 24px; }
  .doc-ref { display: none; }
  .drawer-content { width: 100%; }
}
</style>