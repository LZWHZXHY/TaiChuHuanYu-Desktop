<template>
  <div class="md-layout">
    <header class="md-header">
      <div class="header-inner">
        <div class="brand-path" @click="resetToGlobal">
          <span class="md-hash">##</span>
          <span class="brand-name">灵脉百科</span>
        </div>
        <div class="search-box">
          <input v-model="searchQuery" placeholder="搜索知识骨架..." />
        </div>
      </div>
    </header>

    <div class="md-container">
      <aside class="md-sidebar">
        <div class="toc-header">目录索引</div>
        <div class="toc-tree">
          <template v-if="currentCategoryId === 'all'">
            <div v-for="rootCat in rootCategories" :key="rootCat.id" class="toc-group">
              <div class="md-h3" @click="handleCategoryChange(rootCat.id)">### {{ rootCat.name }}</div>
              <div class="toc-list">
                <div v-for="sub in getSubCategories(rootCat.id)" :key="sub.id" class="toc-item" @click="handleCategoryChange(sub.id)">
                  <span class="md-dash">-</span> {{ sub.name }}
                </div>
              </div>
            </div>
          </template>
          <template v-else>
            <div class="toc-group">
              <div class="md-h3">### {{ currentCategoryName }}</div>
              <div class="toc-list">
                <div v-for="entry in filteredEntries" :key="entry.id" class="toc-item entry-link" @click="goDetail(entry.id)">
                  <span class="md-dash">*</span> {{ entry.title }}
                </div>
              </div>
            </div>
          </template>
        </div>
        <div class="apply-section">
          <span class="apply-link" @click="showApplyModal = true">[+] 申请开辟新分类</span>
        </div>
      </aside>

      <main class="md-content">
        <div class="md-nav-area">
          <nav class="md-nav secondary">
            <span class="nav-item" :class="{ active: currentCategoryId === 'all' }" @click="handleCategoryChange('all')">全部分类</span>
            <span v-for="c in rootCategories" :key="c.id" class="nav-item" :class="{ active: currentCategoryId === c.id }" @click="handleCategoryChange(c.id)">
              {{ c.name }}
            </span>
          </nav>
        </div>

        <div class="article-list">
          <article v-for="entry in filteredEntries" :key="entry.id" class="article-item" @click="goDetail(entry.id)">
            <h2 class="article-title"><span class="md-hash">##</span> {{ entry.title }}</h2>
            <div class="article-excerpt">
              <SpiritPreview :modelValue="parseJson(entry.excerpt)" />
            </div>
          </article>
          <div v-if="filteredEntries.length === 0" class="empty-state">> 在此分类下未寻得对应知识...</div>
        </div>
      </main>
    </div>

    <Teleport to="body">
      <div v-if="showApplyModal" class="md-modal-mask" @mousedown="showApplyModal = false">
        <div class="md-modal" @mousedown.stop>
          <h3>## 申请开辟新分类</h3>
          <div class="form-group">
            <input v-model="applyForm.name" placeholder="分类名称" />
            <input v-model="applyForm.reason" placeholder="申请缘由..." />
          </div>
          <button class="md-btn" :disabled="isSubmitting" @click="submitApply">
            {{ isSubmitting ? '提交中...' : '[ 提交申请 ]' }}
          </button>
        </div>
      </div>
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

// JSON 解析工具
const parseJson = (content: any) => {
  if (!content) return '';
  if (typeof content === 'string') {
    try { return JSON.parse(content); } catch { return content; }
  }
  return content;
};

// 数据加载
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

// 提交申请
const submitApply = async () => {
  if (!applyForm.name.trim() || !applyForm.reason.trim()) return alert('请完整填写！');
  isSubmitting.value = true;
  try {
    await wikiApi.applyCategory({ name: applyForm.name, reason: applyForm.reason, parentId: null, sortOrder: 0 });
    alert('申请已提交');
    showApplyModal.value = false;
    applyForm.name = ''; applyForm.reason = '';
  } finally { isSubmitting.value = false; }
};

const handleCategoryChange = (id: number | 'all') => currentCategoryId.value = id;
const goDetail = (id: string) => router.push({ name: 'WikiDetail', params: { id } });
const resetToGlobal = () => { currentCategoryId.value = 'all'; searchQuery.value = ''; };

const rootCategories = computed(() => allCategories.value.filter(c => !c.parentId));
const getSubCategories = (parentId: number) => allCategories.value.filter(c => c.parentId === parentId);
const currentCategoryName = computed(() => allCategories.value.find(c => c.id === currentCategoryId.value)?.name || '');

const filteredEntries = computed(() => {
  let list = allEntries.value;
  if (currentCategoryId.value !== 'all') {
    list = list.filter(e => e.categoryId === currentCategoryId.value);
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
.md-layout { min-height: 100vh; background: #fff; color: #111; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; }
.md-header { max-width: 1000px; margin: 0 auto; padding: 40px 24px; }
.header-inner { display: flex; justify-content: space-between; align-items: center; }
.md-container { max-width: 1000px; margin: 0 auto; display: flex; padding: 40px 24px; gap: 80px; }
.md-sidebar { width: 200px; flex-shrink: 0; }
.toc-header { font-size: 0.7rem; color: #888; text-transform: uppercase; margin-bottom: 24px; letter-spacing: 0.1em; }
.md-h3 { font-weight: 600; cursor: pointer; margin-bottom: 12px; transition: 0.2s; }
.md-h3:hover { opacity: 0.5; }
.toc-list { display: flex; flex-direction: column; gap: 8px; margin-bottom: 20px; }
.toc-item { font-size: 0.85rem; color: #888; cursor: pointer; }
.md-content { flex: 1; min-width: 0; }
.md-nav { display: flex; gap: 24px; margin-bottom: 60px; overflow-x: auto; padding-bottom: 10px; }
.nav-item { cursor: pointer; color: #888; white-space: nowrap; }
.nav-item.active { color: #000; font-weight: 600; border-bottom: 2px solid #000; }
.article-item { cursor: pointer; margin-bottom: 56px; transition: transform 0.2s; }
.article-item:hover { transform: translateX(10px); }
.article-title { font-size: 1.5rem; font-weight: 600; margin-bottom: 16px; }
.md-hash { color: #ccc; margin-right: 4px; }
.article-excerpt { max-height: 120px; overflow: hidden; mask-image: linear-gradient(to bottom, black 50%, transparent 100%); }
.md-modal-mask { position: fixed; inset: 0; background: rgba(255,255,255,0.9); display: flex; justify-content: center; align-items: center; z-index: 999; }
.md-modal { background: #fff; width: 400px; padding: 40px; border: 1px solid #eee; }
.form-group { display: flex; flex-direction: column; gap: 20px; margin-bottom: 30px; }
.form-group input { border: none; border-bottom: 1px solid #eee; padding: 8px 0; outline: none; }
.md-btn { background: #000; color: #fff; border: none; padding: 10px; cursor: pointer; width: 100%; }
@media (max-width: 768px) {
  .md-container { flex-direction: column; gap: 40px; }
  .md-sidebar { width: 100%; }
}
</style>