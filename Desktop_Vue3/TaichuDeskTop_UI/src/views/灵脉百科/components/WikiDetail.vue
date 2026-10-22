<template>
  <main class="wiki-page content-page">
    <nav class="toolbar">
      <RouterLink :to="{ name: 'KnowledgeHome', query: { type: 'wiki', q: route.query.q, page: route.query.page, category: route.query.category } }">← 公共知识</RouterLink>
      <button v-if="entry && !editing" @click="startEdit">编辑词条</button>
      <button v-if="entry" @click="copyLink">复制引用链接</button>
    </nav>
    <p class="eyebrow">公共知识 · 社区共同维护</p>
    <p v-if="loading" role="status">正在加载词条…</p>
    <div v-else-if="loadError" role="alert">
      <p>{{ loadError }}</p>
      <button @click="load">重新加载</button>
    </div>
    <template v-else>
      <h1>{{ isNew ? '新建公共知识' : entry?.title }}</h1>
      <p v-if="entry" class="meta">{{ entry.revisionCount || 1 }} 个公开版本 · {{ Math.max(0, (entry.revisionCount || 1) - 1) }} 次修订 · 最近更新 {{ date(entry.publishedAt) }}</p>
      <div v-if="entry" class="tags">
        <span v-for="tag in wikiTags(entry.tags)" :key="tag">#{{ tag }}</span>
      </div>
      <section v-if="editing" class="wiki-editor content-panel">
        <p class="notice">提交后等待审核。保存修改说明，帮助其他贡献者理解这次编辑。</p>
        <template v-if="isNew">
          <label>
            词条标题
            <input v-model="title" maxlength="200" :disabled="saving" />
          </label>
          <label>
            分类
            <select v-model="categoryId" :disabled="saving">
              <option :value="0">请选择分类</option>
              <option v-for="c in categories" :key="c.id" :value="c.id">{{ c.name }}</option>
            </select>
          </label>
          <label>
            标签（可选，用逗号分隔）
            <input v-model="tags" maxlength="200" :disabled="saving" />
          </label>
          <label>
            摘要（可选）
            <textarea v-model="excerpt" maxlength="150" :disabled="saving" />
          </label>
        </template>
        <fieldset :disabled="saving">
          <legend>词条正文</legend>
          <SpiritText
            :key="editorKey"
            ref="editor"
            standalone
            :disabled="saving"
            :initial-content="draft"
            @change="dirty = true"
          />
        </fieldset>
        <label>
          修改说明
          <input v-model="summary" maxlength="255" placeholder="例如：修正第二段的年代，补充参考资料" :disabled="saving" />
        </label>
        <div class="toolbar">
          <button class="primary" :disabled="saving" @click="submit">{{ saving ? '提交中…' : '提交审核' }}</button>
          <button v-if="!isNew" :disabled="saving" @click="cancelEdit">取消编辑</button>
        </div>
        <div v-if="conflict" class="notice" role="alert">
          <p>已有新版本，你的编辑内容仍保留。请先打开最新内容对照修改，再重新提交。</p>
          <button @click="showLatest">查看最新内容</button>
          <template v-if="latest">
            <SpiritPreview :model-value="latest.content" />
            <button @click="baseRevision = latest.currentRevisionId; conflict = false">已对照，基于此版本继续提交</button>
          </template>
        </div>
      </section>
      <template v-else-if="entry">
        <nav class="section-toolbar" aria-label="词条视图">
          <div class="content-filters"><button :aria-pressed="pane === 'article'" @click="pane = 'article'">词条正文</button><button :aria-pressed="pane === 'history'" @click="pane = 'history'">修订记录 · {{ entry.revisionCount || 1 }}</button></div>
          <span class="section-hint">每个公开版本都可追溯</span>
        </nav>
        <article v-show="pane === 'article'" class="wiki-reading content-panel"><SpiritPreview :model-value="entry.content" /><footer class="wiki-reading-footer"><span>发现错误或想补充资料？</span><button @click="startEdit">参与编辑</button><button @click="pane = 'history'">查看修订记录</button></footer></article>
        <section v-show="pane === 'history'" class="history content-panel">
          <h2>修订历史</h2>
          <p>公开记录仅包含已生效版本；待审核建议不会改变正文。</p>
          <p v-if="historyLoading" role="status">正在加载历史…</p>
          <div v-else-if="historyError" role="alert">
            {{ historyError }}
            <button @click="loadHistory">重试</button>
          </div>
          <template v-else-if="history">
            <p>
              参与维护：
              <span v-for="person in history.contributors" :key="person.id" class="contributor">{{ person.name }}</span>
            </p>
            <ol>
              <li v-for="revision in history.items" :key="revision.id">
                <div>
                  <strong>{{ revision.contributorName }}</strong>
                  ·
                  <time :title="date(revision.createdAt)">{{ relative(revision.createdAt) }}</time>
                  · 版本 #
                  {{ revision.id }}
                  <span v-if="revision.id === entry.currentRevisionId">（当前）</span>
                </div>
                <p>{{ revision.editSummary || '未填写修改说明' }}</p>
                <button :disabled="comparing" @click="compare(revision.id)">{{ comparing ? '加载版本中…' : '查看版本与段落差异' }}</button>
              </li>
            </ol>
            <div class="toolbar" v-if="history.total > 20">
              <button :disabled="history.page <= 1" @click="historyPage--; loadHistory()">上一页</button>
              <span>{{ history.page }} / {{ Math.ceil(history.total / 20) }}</span>
              <button :disabled="history.page * 20 >= history.total" @click="historyPage++; loadHistory()">下一页</button>
            </div>
          </template>
        </section>
      </template>
    </template>
    <p v-if="message" role="status">{{ message }}</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <section v-if="comparison && pane === 'history' && !editing" class="comparison content-panel" ref="comparisonPanel" tabindex="-1">
      <h2>版本 #{{ comparison.id }}</h2>
      <p>{{ comparison.editSummary }} · {{ date(comparison.createdAt) }}</p>
      <button @click="comparison = null">收起对比</button>
      <template v-if="comparison.previousContent != null">
        <p v-if="diffError" role="alert">{{ diffError }}</p>
        <p v-else-if="!changes.length">正文没有变化。</p>
        <article v-for="(change, index) in changes" :key="index" class="change">
          <h3>{{ change.oldParagraph ? '原第 ' + change.oldParagraph + ' 段' : '新增' }} → {{ change.newParagraph ? '第 ' + change.newParagraph + ' 段' : '删除' }}</h3>
          <p v-if="change.structural">格式、链接或媒体信息发生变化，请对照下面的完整版本。</p>
          <template v-else>
            <p class="before"><strong class="diff-label">修改前</strong>
              <span>{{ change.parts.prefix }}</span>
              <del>{{ change.parts.removed }}</del>
              <span>{{ change.parts.suffix }}</span>
            </p>
            <p class="after"><strong class="diff-label">修改后</strong>
              <span>{{ change.parts.prefix }}</span>
              <ins>{{ change.parts.added }}</ins>
              <span>{{ change.parts.suffix }}</span>
            </p>
          </template>
        </article>
        <details>
          <summary>修改前的完整版本</summary>
          <SpiritPreview :model-value="comparison.previousContent" />
        </details>
      </template>
      <p v-else>首版或历史记录缺少基准版本，无法准确还原本次差异；以下为该版本内容。</p>
      <details open>
        <summary>此版本完整内容</summary>
        <SpiritPreview :model-value="comparison.content" />
      </details>
    </section>
  </main>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick, onUnmounted, defineAsyncComponent } from 'vue';
import { useRoute, useRouter, onBeforeRouteLeave, onBeforeRouteUpdate } from 'vue-router';
import { wikiApi, type IWikiCategory } from '@/api/Wiki';
import { useUserStore } from '@/stores/user';
import SpiritPreview from '@/components/SpiritTextComponents/SpiritPreview.vue';
import { parseWikiDocument, wikiTags } from '@/utils/wikiContent';
import { wikiDiff, changedText } from '@/utils/wikiDiff';
const SpiritText = defineAsyncComponent(() => import('@/components/SpiritText.vue'));
const route = useRoute(), router = useRouter(), user = useUserStore();
const isNew = computed(() => route.params.id === 'new'), entry = ref<any>(null), loading = ref(false), loadError = ref(''), error = ref(''), message = ref('');
const pane = ref<'article' | 'history'>('article');
const comparisonPanel = ref<HTMLElement>();
const comparing = ref(false);
const editing = ref(false), saving = ref(false), dirty = ref(false), editor = ref<any>(), draft = ref<any>(), editorKey = ref(0), baseRevision = ref(0);
const title = ref(''), categoryId = ref(0), categories = ref<IWikiCategory[]>([]), tags = ref(''), excerpt = ref(''), summary = ref('');
const history = ref<any>(null), historyPage = ref(1), historyLoading = ref(false), historyError = ref(''), comparison = ref<any>(null), changes = ref<any[]>([]), diffError = ref(''), conflict = ref(false), latest = ref<any>(null);
let version = 0, historyVersion = 0, comparisonVersion = 0;
const date = (value: string) => value ? new Date(value).toLocaleString('zh-CN') : '时间未知';
const relative = (value: string) => { const seconds = Math.max(0, (Date.now() - new Date(value).getTime()) / 1000); return seconds < 60 ? '刚刚' : seconds < 3600 ? Math.floor(seconds / 60) + ' 分钟前' : seconds < 86400 ? Math.floor(seconds / 3600) + ' 小时前' : seconds < 2592000 ? Math.floor(seconds / 86400) + ' 天前' : date(value); };
function login() { return router.push({ name: '身份认证', query: { redirect: route.fullPath } }); }
async function load() {
    pane.value = 'article';
    const current = ++version;
    historyVersion++;
    comparisonVersion++;
    comparing.value = false;
    loading.value = true;
    loadError.value = '';
    entry.value = null;
    history.value = null;
    comparison.value = null;
    editing.value = false;
    dirty.value = false;
    conflict.value = false;
    latest.value = null;
    summary.value = '';
    message.value = '';
    error.value = '';
    historyPage.value = 1;
    try {
        if (isNew.value) {
            if (!user.isLoggedIn) {
                await login();
                return;
            }
            categories.value = await wikiApi.getCategories();
            if (current !== version)
                return;
            draft.value = { type: 'doc', content: [{ type: 'paragraph' }] };
            editing.value = true;
            editorKey.value++;
        }
        else {
            const result = await wikiApi.getArticleDetail(String(route.params.id));
            if (current !== version)
                return;
            if (result.status === 'Pending') {
                loadError.value = result.message;
                return;
            }
            entry.value = { ...result, content: parseWikiDocument(result.content) };
            void loadHistory();
        }
    }
    catch (cause: any) {
        if (current === version)
            loadError.value = cause.friendlyMessage || cause.message || '词条加载失败';
    }
    finally {
        if (current === version)
            loading.value = false;
    }
}
function startEdit() { if (!user.isLoggedIn)
    return login(); draft.value = entry.value.content; baseRevision.value = entry.value.currentRevisionId; editing.value = true; editorKey.value++; summary.value = ''; comparisonVersion++; comparing.value = false; comparison.value = null; }
function cancelEdit() { if (!dirty.value || window.confirm('放弃未提交的编辑？')) {
    editing.value = false;
    dirty.value = false;
} }
async function submit() {
    if (saving.value)
        return;
    error.value = '';
    message.value = '';
    let content: string;
    try {
        content = JSON.stringify(editor.value.getSnapshot());
    }
    catch (cause: any) {
        error.value = cause.message;
        return;
    }
    if (isNew.value && (!title.value.trim() || !categoryId.value)) {
        error.value = '请填写标题并选择分类';
        return;
    }
    const current = version;
    saving.value = true;
    try {
        const result: any = isNew.value ? await wikiApi.createArticle({ title: title.value.trim(), categoryId: categoryId.value, content, tags: wikiTags(tags.value), excerpt: excerpt.value.trim() }) : await wikiApi.updateFromNote({ articleId: String(route.params.id), content, summary: summary.value, baseRevisionId: baseRevision.value });
        if (current !== version)
            return;
        dirty.value = false;
        message.value = result.message || '修订已提交，等待审核';
        if (isNew.value) {
            saving.value = false;
            await router.replace({ name: 'WikiDetail', params: { id: result.articleId } });
        }
        else
            editing.value = false;
    }
    catch (cause: any) {
        if (current === version) {
            conflict.value = cause.response?.status === 409;
            error.value = cause.friendlyMessage || '提交失败，编辑内容已保留，请重试';
        }
    }
    finally {
        if (current === version)
            saving.value = false;
    }
}
async function showLatest() { const current = version; try {
    const result = await wikiApi.getArticleDetail(String(route.params.id));
    if (current === version)
        latest.value = { ...result, content: parseWikiDocument(result.content) };
}
catch {
    error.value = '最新版本加载失败，请重试';
} }
async function loadHistory() {
    const current = ++historyVersion;
    historyLoading.value = true;
    historyError.value = '';
    try {
        const result = await wikiApi.history(String(route.params.id), historyPage.value);
        if (current === historyVersion)
            history.value = result;
    }
    catch {
        if (current === historyVersion)
            historyError.value = '修订历史加载失败';
    }
    finally {
        if (current === historyVersion)
            historyLoading.value = false;
    }
}
async function compare(id: number) {
    const current = ++comparisonVersion;
    comparing.value = true;
    comparison.value = null;
    error.value = '';
    diffError.value = '';
    changes.value = [];
    try {
        const result = await wikiApi.revision(String(route.params.id), id);
        if (current !== comparisonVersion)
            return;
        comparison.value = result;
        if (result.previousContent != null) {
            try {
                changes.value = wikiDiff(result.previousContent, result.content).map(change => ({ ...change, parts: changedText(change.before, change.after) }));
            }
            catch (cause: any) {
                diffError.value = cause.message;
            }
        }
        await nextTick();
        if (current === comparisonVersion && pane.value === 'history' && !editing.value) { comparisonPanel.value?.focus({ preventScroll: true }); comparisonPanel.value?.scrollIntoView({ block: 'start' }); }
    }
    catch {
        if (current === comparisonVersion)
            error.value = '历史版本加载失败，请重试';
    } finally { if (current === comparisonVersion) comparing.value = false; }
}
async function copyLink() { try {
    await navigator.clipboard.writeText(window.location.href);
    message.value = '引用链接已复制';
}
catch {
    message.value = '复制失败，请复制浏览器地址栏中的链接';
} }
const canLeave = () => !saving.value && (!dirty.value || window.confirm('有未提交的编辑，确定离开？'));
onBeforeRouteLeave(canLeave);
onBeforeRouteUpdate((to, from) => to.params.id === from.params.id || canLeave());
function beforeUnload(e: BeforeUnloadEvent) { if (dirty.value || saving.value) {
    e.preventDefault();
    e.returnValue = '';
} }
window.addEventListener('beforeunload', beforeUnload);
watch([title, tags, excerpt, summary], () => { if (editing.value)
    dirty.value = true; });
watch(() => route.params.id, load, { immediate: true });
watch(() => user.token, () => { version++; saving.value = false; editing.value = false; dirty.value = false; message.value = '登录状态已变化，请重新打开编辑。'; });
onUnmounted(() => { version++; historyVersion++; comparisonVersion++; window.removeEventListener('beforeunload', beforeUnload); });
</script>

<style scoped>
.wiki-page {
  max-width: 1200px;
  margin: 0 auto 40px;
  color: var(--text-primary);
  line-height: 1.8;
}
h1 {
  font-size: 2.4rem;
  line-height: 1.3;
}
.toolbar, .tags {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px;
}
.toolbar {
  margin: 16px 0;
}
.type-label, a {
  color: var(--accent-color);
}
.meta, .history > p {
  color: var(--text-secondary);
}
label {
  display: block;
  margin: 16px 0;
}
input, select, textarea {
  display: block;
  box-sizing: border-box;
  width: 100%;
  font: inherit;
  padding: 10px;
}
button, input, select, textarea {
  background: var(--paper-card);
  color: inherit;
  border: 1px solid var(--border-line);
  border-radius: 6px;
}
button {
  padding: 8px 14px;
  cursor: pointer;
}
button:disabled {
  opacity: .5;
  cursor: wait;
}
fieldset {
  min-width: 0;
  border: 1px solid var(--border-line);
  border-radius: 10px;
  padding: 20px;
}
fieldset:disabled {
  pointer-events: none;
  opacity: .7;
}
.notice {
  padding: 16px;
  background: var(--paper-sub);
  border-radius: 8px;
}
[role=alert] {
  color: var(--accent-color);
}
.history, .comparison { margin-top: 24px; }
.wiki-reading { padding: 36px 48px; }
.wiki-reading :deep(.spirit-render-engine) { max-width: 760px; margin: auto; font-size: 1.05rem; line-height: 1.9; }
.wiki-reading-footer { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; border-top: 1px solid var(--border-line); margin-top: 32px; padding-top: 20px; color: var(--text-secondary); font-size: .9rem; }
.diff-label { display: block; color: var(--text-secondary); font-size: .78rem; margin-bottom: 6px; }
.tags span, .contributor { padding: 3px 10px; background: var(--paper-sub); border-radius: 6px; font-size: .85rem; }
.history li { border-left: 2px solid var(--border-line); padding-left: 20px; }
@media(max-width: 720px) { .wiki-reading { padding: 20px; } fieldset { padding: 12px; } }
ol {
  list-style: none;
  padding: 0;
}
li {
  padding: 16px 0;
  border-bottom: 1px solid var(--border-line);
}
li p {
  margin: 4px 0 12px;
}
.contributor {
  display: inline-block;
  margin-right: 12px;
}
.change {
  padding: 16px;
  margin: 12px 0;
  border: 1px solid var(--border-line);
  border-radius: 8px;
}
.change p {
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}
del {
  background: #ffe3e3;
  color: #8b2222;
}
ins {
  background: #daf3dd;
  color: #195f2c;
}
details {
  margin: 20px 0;
}
summary {
  cursor: pointer;
}
</style>
