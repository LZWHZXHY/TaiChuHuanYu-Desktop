<template>
  <section class="knowledge-home content-page community-surface tone-library">
    <header class="knowledge-header content-header">
      <SectionTrace />
      <div>
        <p class="eyebrow">社区资料 · 统一检索</p>
        <h1>{{ type === 'wiki' ? '公共 Wiki' : '知识库' }}</h1>
        <p class="intro">{{ type === 'wiki' ? '一起完善公共知识，让每次修改都有据可查。' : '社区的公开资料，在这里查找、阅读与引用。' }}</p>
      </div>
      <nav class="knowledge-actions content-actions" aria-label="创作入口">
        <RouterLink class="primary" to="/codex/new">新建公共知识</RouterLink>
        <button @click="switchView">{{ graphMode ? '返回资料列表' : '关系图谱' }}</button>
      </nav>
    </header>
    <template v-if="graphMode">
      <p class="intro">关系图谱保留现有节点与资源关联；社区各类内容请使用资料列表检索。</p>
      <div class="graph-area">
        <KnowledgeGraph />
      </div>
    </template>
    <template v-else>
      <form class="search-form" role="search" @submit.prevent="applySearch">
        <label for="knowledge-search">查找公开资料</label>
        <div class="search-controls">
          <input
            id="knowledge-search"
            v-model="searchInput"
            type="search"
            maxlength="100"
            placeholder="输入标题、摘要、作者、标签或资源名称"
          />
          <button class="primary" type="submit">搜索</button>
          <button v-if="search" type="button" @click="clearSearch">清除</button>
        </div>
      </form>
      <div class="browse-layout">
        <aside class="type-nav" aria-label="资料类型">
          <label class="mobile-type-filter">资料类型<select aria-label="选择资料类型" :value="type" @change="selectType(($event.target as HTMLSelectElement).value as KnowledgeType)"><option v-for="(label, key) in knowledgeTypes" :key="key" :value="key">{{ label }}</option></select></label>
          <span class="nav-caption">浏览资料</span>
          <button v-for="(label, key) in knowledgeTypes" :key="key" :class="{ active: type === key }" :aria-pressed="type === key" @click="selectType(key)">{{ label }}</button>
        </aside>
        <main class="results">
          <div class="result-header">
            <h2>{{ knowledgeTypes[type] }}</h2>
            <span v-if="search">“{{ search }}”</span>
            <button :disabled="loading" @click="refresh">刷新</button>
          </div>
          <label v-if="type === 'wiki'" class="wiki-category">
            知识分类
            <select
              :value="categoryId || ''"
              @change="setQuery({ category: ($event.target as HTMLSelectElement).value || undefined, page: undefined })"
            >
              <option value="">全部分类</option>
              <option v-for="category in categories" :key="category.id" :value="category.id">{{ category.name }}</option>
            </select>
          </label>
          <p v-if="type === 'wiki'" class="notice">公共知识由社区共同维护，可提交编辑建议、查看修订历史和版本差异。博客与帖子保留作者自己的发布与编辑流程。</p>
          <p v-if="type === 'wiki' && categoryError" role="alert">{{ categoryError }} <button @click="loadCategories">重试分类</button></p>
          <template v-if="type !== 'resource'">
            <p v-if="loading" class="state" role="status">正在查找公开资料…</p>
            <div v-else-if="error" class="state" role="alert">
              <p>{{ error }}</p>
              <button @click="loadCatalog">重新加载资料</button>
            </div>
            <template v-else>
              <p class="result-count" role="status">
                找到
                {{ total }}
                条内容
                <template v-if="type === 'all'"> · 下载资源另列在下方</template>
              </p>
              <div v-if="!items.length" class="state">
                <h3>暂时没有匹配的内容</h3>
                <p>换个关键词，或查看其他类型。</p>
                <button @click="resetFilters">查看全部资料</button>
              </div>
              <div class="content-list">
                <article v-for="item in items" :key="item.type + ':' + item.id" class="content-card">
                  <img
                    v-if="item.coverUrl"
                    :src="item.coverUrl"
                    alt=""
                    loading="lazy"
                    @error="($event.target as HTMLImageElement).hidden = true"
                  />
                  <div class="card-body">
                    <span class="type-label content-badge">{{ knowledgeTypes[item.type] }}</span>
                    <h3>
                      <button class="title-button" @click="openItem(item)">{{ item.title || '无标题' }}</button>
                    </h3>
                    <p>{{ wikiText(item.summary).slice(0, 180) || '打开原内容查看详情。' }}</p>
                    <div class="card-meta">
                      <span v-if="item.author">{{ item.author }}</span>
                      <time>{{ item.updatedAt?.slice(0, 10) }}</time>
                      <span v-for="tag in wikiTags(item.tags).slice(0, 3)" :key="tag">#{{ tag }}</span>
                    </div>
                  </div>
                </article>
              </div>
              <nav v-if="total > 24" class="pagination" aria-label="资料分页">
                <button :disabled="page <= 1" @click="changePage(page - 1)">上一页</button>
                <span>{{ page }} / {{ Math.ceil(total / 24) }}</span>
                <button :disabled="page * 24 >= total" @click="changePage(page + 1)">下一页</button>
              </nav>
            </template>
          </template>
          <section v-if="type === 'all' || type === 'resource'" class="resource-section">
            <h3>
              下载资源
              <span v-if="!resourceLoading && !resourceError">({{ matchedResources.length }})</span>
            </h3>
            <p v-if="resourceLoading" role="status">正在加载资源目录…</p>
            <div v-else-if="resourceError" role="alert">
              <p>{{ resourceError }}</p>
              <button @click="loadResources">重新加载资源</button>
            </div>
            <template v-else>
              <p v-if="!matchedResources.length" class="intro">没有找到匹配的下载资源。</p>
              <div v-for="file in visibleResources" :key="file.name" class="resource-row">
                <div>
                  <strong>{{ file.name.split('/').pop() }}</strong>
                  <p>{{ file.name }} · {{ formatSize(file.size) }}</p>
                </div>
                <button :disabled="downloading === file.name" @click="download(file)">{{ downloading === file.name ? '获取中…' : '下载' }}</button>
              </div>
              <button v-if="type === 'all' && matchedResources.length > 6" @click="selectType('resource')">查看全部 {{ matchedResources.length }} 个资源</button>
              <nav v-if="type === 'resource' && matchedResources.length > 24" class="pagination">
                <button :disabled="page <= 1" @click="changePage(page - 1)">上一页</button>
                <span>{{ page }} / {{ Math.ceil(matchedResources.length / 24) }}</span>
                <button :disabled="page * 24 >= matchedResources.length" @click="changePage(page + 1)">下一页</button>
              </nav>
            </template>
            <p v-if="downloadError" role="alert" class="notice">{{ downloadError }}</p>
          </section>
        </main>
      </div>
    </template>
    <ArtworkDetail v-if="selectedType === 'artwork' && selectedId" :key="selectedId" :id="selectedId" @close="closeDetail" @updated="loadCatalog" />
    <PublicNoteDetail
      v-if="selectedId && (selectedType === 'blog' || selectedType === 'post')"
      :key="selectedId"
      :id="selectedId"
      @close="closeDetail"
    />
    <div
      v-if="selectedType === 'document'"
      class="document-mask"
      role="dialog"
      aria-modal="true"
      aria-label="图谱资料"
      @click.self="closeDetail"
    >
      <article class="document-panel">
        <button @click="closeDetail">关闭</button>
        <p v-if="documentLoading" role="status">正在加载资料…</p>
        <p v-else-if="documentError" role="alert">
          {{ documentError }}
          <button @click="loadDocument">重试</button>
        </p>
        <template v-else>
          <h2>{{ documentDetail?.title }}</h2>
          <SpiritPreview :model-value="documentDetail?.description" />
        </template>
      </article>
    </div>
  </section>
</template>

<script setup lang="ts">
import SectionTrace from '@/components/SectionTrace.vue'
import { ref, computed, watch, onUnmounted, defineAsyncComponent } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { knowledgeApi, knowledgeTypes, knowledgeRoute, type KnowledgeType, type KnowledgeItem, type PublicResource } from '@/api/knowledge';
import { wikiApi, type IWikiCategory } from '@/api/Wiki';
import { wikiTags, wikiText } from '@/utils/wikiContent';
import ArtworkDetail from '../画廊展厅/ArtworkDetail.vue';
import PublicNoteDetail from '../太初随笔/PublicNoteDetail.vue';
import SpiritPreview from '@/components/SpiritTextComponents/SpiritPreview.vue';
const KnowledgeGraph = defineAsyncComponent(() => import('./KnowledgeGraph.vue'));
const route = useRoute(), router = useRouter();
const searchInput = ref(''), items = ref<KnowledgeItem[]>([]), total = ref(0), loading = ref(false), error = ref('');
const resources = ref<PublicResource[]>([]), resourceLoading = ref(false), resourceError = ref(''), downloadError = ref(''), downloading = ref('');
const documentDetail = ref<{
    title: string;
    description: string;
}>(), documentLoading = ref(false), documentError = ref('');
const search = computed(() => typeof route.query.q === 'string' ? route.query.q.trim().slice(0, 100) : '');
const type = computed<KnowledgeType>(() => typeof route.query.type === 'string' && Object.hasOwn(knowledgeTypes, route.query.type) ? route.query.type as KnowledgeType : 'all');
const categories = ref<IWikiCategory[]>([]);
const categoryError = ref('');
async function loadCategories() {
    categoryError.value = '';
    try { const result = await wikiApi.getCategories(); if (!disposed) categories.value = result; }
    catch { if (!disposed) categoryError.value = '分类加载失败，仍可浏览全部公共知识。'; }
}
const categoryId = computed(() => type.value === 'wiki' && Number.isInteger(Number(route.query.category)) && Number(route.query.category) > 0 ? Number(route.query.category) : undefined);
const page = computed(() => Number.isFinite(Number(route.query.page)) ? Math.max(1, Math.min(1000000, Math.floor(Number(route.query.page) || 1))) : 1);
const graphMode = computed(() => route.query.view === 'graph');
const selectedType = computed(() => route.query.detailType), selectedId = computed(() => typeof route.query.detail === 'string' ? route.query.detail : '');
let catalogVersion = 0, resourceVersion = 0, documentVersion = 0, disposed = false;
const setQuery = (query: Record<string, any>, replace = false) => router[replace ? 'replace' : 'push']({ query: { ...route.query, detail: undefined, detailType: undefined, ...query } });
const applySearch = () => setQuery({ q: searchInput.value.trim() || undefined, page: undefined }, true);
const clearSearch = () => { searchInput.value = ''; return applySearch(); };
const selectType = (value: KnowledgeType) => setQuery({ type: value === 'all' ? undefined : value, page: undefined, category: undefined });
const resetFilters = () => setQuery({ q: undefined, type: undefined, page: undefined, category: undefined });
const changePage = (value: number) => setQuery({ page: value > 1 ? value : undefined });
const switchView = () => setQuery({ view: graphMode.value ? undefined : 'graph' });
async function loadCatalog() {
    const version = ++catalogVersion;
    if (type.value === 'resource' || graphMode.value) {
        loading.value = false;
        return;
    }
    loading.value = true;
    error.value = '';
    try {
        const res = await knowledgeApi.search({ type: type.value, search: search.value, page: page.value, categoryId: categoryId.value });
        if (version === catalogVersion) {
            const lastPage = Math.max(1, Math.ceil(res.total / 24));
            if (page.value > lastPage) { void setQuery({ page: lastPage > 1 ? lastPage : undefined }, true); return; }
            items.value = res.items;
            total.value = res.total;
        }
    }
    catch {
        if (version === catalogVersion)
            error.value = '资料加载失败，请重试。';
    }
    finally {
        if (version === catalogVersion)
            loading.value = false;
    }
}
async function loadResources() {
    const version = ++resourceVersion;
    resourceLoading.value = true;
    resourceError.value = '';
    try {
        const res = await knowledgeApi.resources();
        if (!res.success || !Array.isArray(res.data))
            throw new Error();
        if (version === resourceVersion)
            resources.value = res.data;
    }
    catch {
        if (version === resourceVersion)
            resourceError.value = '下载资源目录暂时不可用，其他社区资料仍可浏览。';
    }
    finally {
        if (version === resourceVersion)
            resourceLoading.value = false;
    }
}
const matchedResources = computed(() => { const terms = search.value.toLowerCase().split(/\s+/).filter(Boolean); return resources.value.filter(file => terms.every(term => file.name.toLowerCase().includes(term))).sort((a, b) => a.name.localeCompare(b.name)); });
const visibleResources = computed(() => type.value === 'resource' ? matchedResources.value.slice((page.value - 1) * 24, page.value * 24) : matchedResources.value.slice(0, 6));
const formatSize = (bytes: number) => bytes < 1048576 ? (bytes / 1024).toFixed(1) + ' KB' : (bytes / 1048576).toFixed(1) + ' MB';
async function download(file: PublicResource) {
    if (downloading.value)
        return;
    downloading.value = file.name;
    downloadError.value = '';
    try {
        const res = await knowledgeApi.download(file.name), url = new URL(res.downloadUrl);
        if (!res.success || !['https:', 'http:'].includes(url.protocol))
            throw new Error();
        if (disposed)
            return;
        const a = document.createElement('a');
        a.href = url.href;
        a.download = file.name.split('/').pop() || 'download';
        a.target = '_blank';
        a.rel = 'noopener noreferrer';
        a.click();
    }
    catch {
        if (!disposed)
            downloadError.value = '获取下载链接失败，请再次点击下载。';
    }
    finally {
        downloading.value = '';
    }
}
function openItem(item: KnowledgeItem) {
    if (item.type === 'wiki')
        return router.push({ name: 'WikiDetail', params: { id: item.id }, query: { q: search.value || undefined, type: 'wiki', page: route.query.page, category: route.query.category } });
    if (item.type === 'world' || item.type === 'character' || item.type === 'resource')
        return router.push(knowledgeRoute(item));
    return setQuery({ detail: item.id, detailType: item.type });
}
const closeDetail = () => setQuery({});
async function loadDocument() {
    const version = ++documentVersion;
    documentDetail.value = undefined;
    documentError.value = '';
    if (selectedType.value !== 'document' || !selectedId.value)
        return;
    documentLoading.value = true;
    try {
        const res = await knowledgeApi.document(selectedId.value);
        if (version === documentVersion)
            documentDetail.value = res;
    }
    catch {
        if (version === documentVersion)
            documentError.value = '资料不存在或读取失败。';
    }
    finally {
        if (version === documentVersion)
            documentLoading.value = false;
    }
}
function refresh() { void loadCatalog(); if (type.value === 'all' || type.value === 'resource')
    void loadResources(); }
watch([search, type, page, graphMode, categoryId], () => { searchInput.value = search.value; void loadCatalog(); }, { immediate: true });
watch(type, value => { if (value === 'wiki' && !categories.value.length)
    void loadCategories(); if ((value === 'all' || value === 'resource') && !resources.value.length && !resourceLoading.value)
    void loadResources(); }, { immediate: true });
watch([selectedType, selectedId], loadDocument, { immediate: true });
watch([type, page, () => matchedResources.value.length, resourceLoading], () => {
    if (type.value === 'resource' && !resourceLoading.value && !resourceError.value) {
        const last = Math.max(1, Math.ceil(matchedResources.value.length / 24));
        if (page.value > last) void setQuery({ page: last > 1 ? last : undefined }, true);
    }
});
onUnmounted(() => { disposed = true; catalogVersion++; resourceVersion++; documentVersion++; });
</script>

<style scoped>
h1 {
  font-size: clamp(2rem, 4vw, 3rem);
  margin: 8px 0;
}
.eyebrow, .type-label {
  color: var(--accent-color);
  font-size: .8rem;
}
.intro, .card-meta, .result-count {
  color: var(--text-secondary);
  line-height: 1.7;
}
.knowledge-actions, .search-controls, .result-header, .pagination {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}
button, .knowledge-actions a {
  border: 1px solid var(--border-line);
  background: var(--paper-card);
  color: var(--text-primary);
  border-radius: var(--radius-sm);
  padding: 9px 14px;
  font: inherit;
  font-size: .88rem;
  cursor: pointer;
  text-decoration: none;
}
button:disabled {
  opacity: .5;
  cursor: wait;
}
button:hover:not(:disabled), a:hover {
  border-color: var(--accent-color);
}
.search-form {
  padding: 20px;
  border: 1px solid var(--border-line);
  border-radius: var(--radius-sm);
  background: var(--paper-card);
}
.search-form label {
  display: block;
  font-size: .85rem;
  margin-bottom: 12px;
}
.search-controls input {
  flex: 1;
  min-width: 180px;
  border: 1px solid var(--border-line);
  background: var(--paper-bg);
  color: var(--text-primary);
  border-radius: var(--radius-sm);
  padding: 12px;
  font: inherit;
}
.type-nav .active {
  background: var(--text-primary);
  color: var(--paper-bg);
  border-color: var(--text-primary);
}
.browse-layout {
  display: grid;
  grid-template-columns: 188px minmax(0, 1fr);
  gap: 28px;
  margin-top: 28px;
}
.type-nav {
  display: flex;
  flex-direction: column;
  gap: 8px;
  align-self: start;
}
.mobile-type-filter { display: none; }
.type-nav button { text-align: left; border-color: transparent; }
.type-nav { padding: 12px; border: 1px solid var(--surface-edge); border-radius: var(--radius-sm); background: linear-gradient(165deg, var(--surface-wash), var(--paper-card) 65%); }
.wiki-category { display: flex; align-items: center; flex-wrap: wrap; gap: 12px; margin: 16px 0; color: var(--text-secondary); font-size: 14px; }
.wiki-category select { min-height: 40px; padding: 8px 28px 8px 12px; border: 1px solid var(--border-line); border-radius: var(--radius-sm); background: var(--paper-card); color: var(--text-primary); }
.nav-caption { font-size: .78rem; color: var(--text-secondary); padding: 6px 12px; }
.content-card:hover, .content-card:focus-within { border-color: var(--accent-color); }
.result-header {
  border-bottom: 1px solid var(--border-line);
  padding-bottom: 12px;
}
.result-header h2 {
  font-size: 1.2rem;
  margin: 0;
}
.result-header button {
  margin-left: auto;
}
.notice {
  padding: 14px;
  background: var(--paper-sub);
  border-radius: var(--radius-sm);
  line-height: 1.7;
  font-size: .9rem;
}
.content-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.content-card {
  transition: border-color .2s;
  display: flex;
  align-items: center;
  gap: 20px;
  background: var(--paper-card);
  border: 1px solid var(--border-line);
  border-radius: var(--radius-sm);
  padding: 20px;
}
.content-card > img {
  width: 144px;
  height: 112px;
  object-fit: cover;
  border-radius: var(--radius-sm);
}
.card-body {
  flex: 1;
  min-width: 0;
}
.card-body h3 {
  margin: 7px 0;
}
.card-body p {
  margin: 8px 0;
  font-size: .9rem;
  line-height: 1.7;
  color: var(--text-secondary);
  overflow-wrap: anywhere;
}
.title-button {
  border: 0;
  background: none;
  padding: 0;
  text-align: left;
  font-size: 1.12rem;
  font-weight: 600;
}
.card-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  font-size: .78rem;
}
.state {
  text-align: center;
  padding: 40px 16px;
  color: var(--text-secondary);
}
.pagination {
  justify-content: center;
  margin: 24px 0;
}
.resource-section {
  margin-top: 28px;
  border-top: 1px solid var(--border-line);
  padding-top: 16px;
}
.resource-row {
  display: flex;
  gap: 16px;
  align-items: center;
  justify-content: space-between;
  padding: 16px 0;
  border-bottom: 1px solid var(--border-line);
}
.resource-row > div {
  min-width: 0;
  overflow-wrap: anywhere;
}
.resource-row p {
  color: var(--text-secondary);
  font-size: .8rem;
  margin: 8px 0 0;
}
.graph-area {
  height: 72vh;
  min-height: 460px;
  border-radius: var(--radius-sm);
  overflow: hidden;
}
.document-mask {
  position: fixed;
  inset: 0;
  z-index: 10000;
  background: #0008;
  padding: 4vh 16px;
  overflow-y: auto;
}
.document-panel {
  max-width: 800px;
  margin: auto;
  padding: 28px;
  background: var(--paper-bg);
  border-radius: var(--radius-sm);
}
:is(button,a,input):focus-visible {
  outline: 2px solid var(--accent-color);
  outline-offset: 3px;
}
@media(max-width: 720px) {
  .knowledge-header {
  align-items: stretch;
  flex-direction: column;
}
.browse-layout {
  grid-template-columns: 1fr;
  gap: 16px;
}
.type-nav { flex-direction: row; flex-wrap: wrap; padding: 8px; gap: 4px; }
.type-nav > .nav-caption, .type-nav > button { display: none; }
.mobile-type-filter { display: flex; width: 100%; align-items: center; gap: 12px; padding: 4px; font-size: .9rem; }
.mobile-type-filter select { min-width: 0; flex: 1; padding: 9px; background: var(--paper-card); color: var(--text-primary); border: 1px solid var(--border-line); border-radius: var(--radius-sm); }
.type-nav button {
  white-space: nowrap;
}
.content-card {
  gap: 12px;
  padding: 16px;
  flex-wrap: wrap;
}
.content-card > img {
  width: 72px;
  height: 72px;
}

}
</style>
