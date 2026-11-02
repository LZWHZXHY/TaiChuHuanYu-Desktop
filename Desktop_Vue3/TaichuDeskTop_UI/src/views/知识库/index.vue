<template>
  <section class="knowledge-home content-page community-surface tone-library">
    <header class="knowledge-header content-header">
      <SectionTrace particles="knowledge" />
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
          <p v-if="type === 'wiki'" class="notice">公共知识由社区共同维护，可提交编辑建议、查看修订历史和版本差异。论坛里的博客和帖子由作者自己发布和编辑。</p>
          <p v-if="type === 'wiki' && categoryError" role="alert">{{ categoryError }} <button @click="loadCategories">重试分类</button></p>
          <!-- 茶绘笔刷：数据在笔刷库，这里只是浏览入口（和下载资源一样单独列） -->
          <section v-if="type === 'brush'" class="brush-section">
            <p class="notice">大家在茶绘画室里做的、选择公开的笔刷。收藏后，在茶绘画室「我的笔刷」里就能直接用。</p>
            <BrushSquare :search="search" show-studio-link />
          </section>
          <template v-if="type !== 'resource' && type !== 'brush'">
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
                <template v-if="type === 'all'"> · 茶绘笔刷和下载资源另列在下方</template>
              </p>
              <div v-if="!items.length && devLocked" class="state">
                <h3>🔒 开发进度是赞助会员专享</h3>
                <p>研发报告、开发日志只对赞助会员和管理员开放；首页可以看到各项目的简单进度。</p>
                <RouterLink to="/sponsors" class="state-link">去赞助墙了解</RouterLink>
              </div>
              <div v-else-if="!items.length" class="state">
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
                      <button class="title-button" @click="openItem(item)"><template v-for="(seg, si) in marks(item.title || '无标题')" :key="si"><mark v-if="seg.hit">{{ seg.text }}</mark><template v-else>{{ seg.text }}</template></template></button>
                    </h3>
                    <p><template v-for="(seg, si) in marks(wikiText(item.summary).slice(0, 180) || '打开原内容查看详情。')" :key="si"><mark v-if="seg.hit">{{ seg.text }}</mark><template v-else>{{ seg.text }}</template></template></p>
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
          <section v-if="type === 'all'" class="resource-section brush-section">
            <BrushSquare :search="search" compact @more="selectType('brush')" />
          </section>
          <!-- 下载资源：官方资源 + 社区上传的资源（筛选、详情、付费下载、上传、我的资源都在这个组件里） -->
          <ResourceSection
            v-if="type === 'all' || type === 'resource'"
            ref="resourceSection"
            :search="search"
            :compact="type === 'all'"
            :page="type === 'resource' ? page : 1"
            @more="selectType('resource')"
            @page="changePage"
          />
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
    <!-- 公告 / 版本更新 / 使用手册 / 必吃榜：在知识库里直接读全文 -->
    <div
      v-if="isTextType && selectedId"
      class="document-mask"
      role="dialog"
      aria-modal="true"
      :aria-label="textTypeLabel"
      @click.self="closeDetail"
    >
      <article class="document-panel text-panel">
        <button @click="closeDetail">关闭</button>
        <p v-if="textLoading" role="status">正在加载…</p>
        <p v-else-if="textError" role="alert" class="text-error">{{ textError }} <button v-if="!textMissing" @click="loadText">重试</button> <button @click="closeDetail">回到列表</button></p>
        <template v-else-if="textDetail">
          <p class="text-kind"><span class="type-label content-badge">{{ textTypeLabel }}</span><span v-if="textDetail.meta">{{ textDetail.meta }}</span><time :datetime="textDetail.updatedAt">{{ textDate(textDetail.updatedAt) }}</time></p>
          <h2>{{ textDetail.title }}</h2>
          <img v-if="textDetail.coverUrl" :src="textDetail.coverUrl" class="text-cover" alt="" />
          <div v-if="textDetail.format === 'markdown'" class="text-body md-rich-text" v-html="renderMarkdown(textDetail.content)"></div>
          <div v-else-if="textDetail.content" class="text-body plain">{{ textDetail.content }}</div>
          <p v-else class="text-body empty">（没有正文）</p>
          <p v-if="textDetail.externalUrl || textDetail.link" class="text-links">
            <a v-if="textDetail.externalUrl" :href="textDetail.externalUrl" target="_blank" rel="noopener noreferrer">打开链接 ↗</a>
            <RouterLink v-if="textDetail.link" :to="textDetail.link">{{ textDetail.linkText || '查看原页面' }} →</RouterLink>
          </p>
        </template>
      </article>
    </div>
  </section>
</template>

<script setup lang="ts">
import SectionTrace from '@/components/SectionTrace.vue'
import { ref, computed, watch, onUnmounted, defineAsyncComponent } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { knowledgeApi, knowledgeTypes, knowledgeRoute, TEXT_TYPES, type KnowledgeType, type KnowledgeItem, type KnowledgeText } from '@/api/knowledge';
import { renderMarkdown } from '@/utils/markdown';
import { serverDate } from '@/utils/serverTime';
import { wikiApi, type IWikiCategory } from '@/api/Wiki';
import { wikiTags, wikiText } from '@/utils/wikiContent';
import ArtworkDetail from '../画廊展厅/ArtworkDetail.vue';
import PublicNoteDetail from '../太初随笔/PublicNoteDetail.vue';
import SpiritPreview from '@/components/SpiritTextComponents/SpiritPreview.vue';
import BrushSquare from '../画廊展厅/茶绘/BrushSquare.vue';
import ResourceSection from './ResourceSection.vue';
const KnowledgeGraph = defineAsyncComponent(() => import('./KnowledgeGraph.vue'));
const route = useRoute(), router = useRouter();
const searchInput = ref(''), items = ref<KnowledgeItem[]>([]), total = ref(0), loading = ref(false), error = ref('');
// 开发进度只给管理员和赞助会员看，其他人选这一类时提示
const devLocked = ref(false);
// 下载资源区块（刷新按钮要通知它重新加载）
const resourceSection = ref<InstanceType<typeof ResourceSection> | null>(null);
const documentDetail = ref<{
    title: string;
    description: string;
}>(), documentLoading = ref(false), documentError = ref('');
const search = computed(() => typeof route.query.q === 'string' ? route.query.q.trim().slice(0, 100) : '');
// 搜索结果里把关键词标出来（多个词用空格分开）
const searchTerms = computed(() => search.value.toLowerCase().split(/\s+/).filter(Boolean).slice(0, 10));
function marks(text: string): { text: string; hit: boolean }[] {
    const terms = searchTerms.value;
    if (!terms.length || !text) return [{ text, hit: false }];
    const escaped = terms.map(t => t.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));
    return text.split(new RegExp(`(${escaped.join('|')})`, 'gi')).filter(Boolean)
        .map(part => ({ text: part, hit: terms.includes(part.toLowerCase()) }));
}
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
let catalogVersion = 0, documentVersion = 0, disposed = false;
const setQuery = (query: Record<string, any>, replace = false) => router[replace ? 'replace' : 'push']({ query: { ...route.query, detail: undefined, detailType: undefined, ...query } });
const applySearch = () => setQuery({ q: searchInput.value.trim() || undefined, page: undefined }, true);
const clearSearch = () => { searchInput.value = ''; return applySearch(); };
const selectType = (value: KnowledgeType) => setQuery({ type: value === 'all' ? undefined : value, page: undefined, category: undefined });
const resetFilters = () => setQuery({ q: undefined, type: undefined, page: undefined, category: undefined });
const changePage = (value: number) => setQuery({ page: value > 1 ? value : undefined });
const switchView = () => setQuery({ view: graphMode.value ? undefined : 'graph' });
async function loadCatalog() {
    const version = ++catalogVersion;
    if (type.value === 'resource' || type.value === 'brush' || graphMode.value) {
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
            devLocked.value = !!res.locked;
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
function openItem(item: KnowledgeItem) {
    if (item.type === 'wiki')
        return router.push({ name: 'WikiDetail', params: { id: item.id }, query: { q: search.value || undefined, type: 'wiki', page: route.query.page, category: route.query.category } });
    if (['world', 'character', 'resource', 'novel', 'work', 'setting', 'joint', 'devlog'].includes(item.type))
        return router.push(knowledgeRoute(item));
    return setQuery({ detail: item.id, detailType: item.type });
}
const closeDetail = () => setQuery({});
// ---------- 公告 / 版本更新 / 使用手册 / 必吃榜的全文 ----------
const isTextType = computed(() => typeof selectedType.value === 'string' && (TEXT_TYPES as readonly string[]).includes(selectedType.value));
const textTypeLabel = computed(() => knowledgeTypes[selectedType.value as KnowledgeType] ?? '资料');
const textDetail = ref<KnowledgeText>(), textLoading = ref(false), textError = ref('');
/** true = 后端说找不到（不存在 / 已下线），不用给「重试」 */
const textMissing = ref(false);
let textVersion = 0;
async function loadText() {
    const version = ++textVersion;
    textDetail.value = undefined;
    textError.value = '';
    textMissing.value = false;
    if (!isTextType.value || !selectedId.value)
        return;
    textLoading.value = true;
    try {
        const res = await knowledgeApi.text(String(selectedType.value), selectedId.value);
        if (version === textVersion)
            textDetail.value = res;
    }
    catch (e) {
        if (version === textVersion) {
            // 404 = 真的找不到（不存在、没公开或已下线）；其他（网络、服务器出错）才是读取失败
            const status = (e as { response?: { status?: number } } | null)?.response?.status;
            textMissing.value = status === 404;
            textError.value = status === 404 ? '这条资料不存在或已下线。' : '读取失败，请重试。';
        }
    }
    finally {
        if (version === textVersion)
            textLoading.value = false;
    }
}
// 开发进度在知识库里没有全文：/knowledge?detailType=devlog&detail=项目ID 这类链接直接转到开发进度页
// （那边的 DevGate 会按赞助权限判断，没权限显示「开发进度是赞助会员专享」，不会显示读取失败）
watch([selectedType, selectedId], ([t, id]) => { if (t === 'devlog' && id) void router.replace({ name: 'DevReportDetail', params: { id } }); }, { immediate: true });
const textDate = (v: string) => serverDate(v).toLocaleDateString('zh-CN', { year: 'numeric', month: 'long', day: 'numeric' });
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
    void resourceSection.value?.reload(); }
watch([search, type, page, graphMode, categoryId], () => { searchInput.value = search.value; void loadCatalog(); }, { immediate: true });
watch(type, value => { if (value === 'wiki' && !categories.value.length)
    void loadCategories(); }, { immediate: true });
watch([selectedType, selectedId], loadDocument, { immediate: true });
watch([selectedType, selectedId], loadText, { immediate: true });
onUnmounted(() => { disposed = true; catalogVersion++; documentVersion++; });
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
  border-radius: 0;
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
  padding: 20px 0;
  border: 0;
  border-bottom: 1px solid var(--border-line);
  border-radius: 0;
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
  border-radius: 0;
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
.type-nav { padding: 12px; border: 1px solid var(--surface-edge); border-radius: 0; background: linear-gradient(165deg, var(--surface-wash), var(--paper-card) 65%); }
.wiki-category { display: flex; align-items: center; flex-wrap: wrap; gap: 12px; margin: 16px 0; color: var(--text-secondary); font-size: 14px; }
.wiki-category select { min-height: 40px; padding: 8px 28px 8px 12px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); }
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
  border-radius: 0;
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
  border: 0;
  border-bottom: 1px solid var(--border-line);
  border-radius: 0;
  padding: 20px;
}
.content-card > img {
  width: 144px;
  height: 112px;
  object-fit: cover;
  border-radius: 0;
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
.state-link { color: var(--accent-color); }
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
  border-radius: 0;
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
.text-panel .text-kind { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; margin: 16px 0 6px; color: var(--text-secondary); font-size: .82rem; }
.text-panel h2 { margin: 0 0 14px; }
.text-panel .text-cover { display: block; max-width: 100%; max-height: 360px; margin: 0 0 16px; object-fit: contain; }
.text-panel .text-body { font-size: .95rem; line-height: 1.85; overflow-wrap: anywhere; }
.text-panel .text-body.plain { white-space: pre-wrap; }
.text-panel .text-error button { margin-left: 8px; }
.text-panel .text-body.empty { color: var(--text-secondary); }
.text-panel .text-body :deep(img) { max-width: 100%; }
.text-panel .text-links { display: flex; flex-wrap: wrap; gap: 16px; margin: 20px 0 0; padding-top: 14px; border-top: 1px solid var(--border-line); }
.text-panel .text-links a { color: var(--accent-color); }
.document-panel {
  max-width: 800px;
  margin: auto;
  padding: 28px;
  background: var(--paper-bg);
  border-radius: 0;
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
.mobile-type-filter select { min-width: 0; flex: 1; padding: 9px; background: var(--paper-card); color: var(--text-primary); border: 1px solid var(--border-line); border-radius: 0; }
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
mark { padding: 0 1px; background: color-mix(in srgb, var(--accent-color) 22%, transparent); color: inherit; }
</style>
