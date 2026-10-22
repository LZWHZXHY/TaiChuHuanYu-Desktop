<template>
  <main class="compose-page content-page">
    <header class="content-header">
      <div>
      <RouterLink :to="{ path: '/stream', query: topic ? { tag: topic } : {} }">← {{ topic || '博客与帖子' }}</RouterLink>
      <h1>{{ isPost ? '发帖子' : '写博客' }}</h1>
      <p>独立发布即可，标签和正文引用均可选。草稿会保存到你的创作空间。</p>
      </div>
      <div class="content-actions"><button :disabled="busy" @click="showDrafts = !showDrafts; showDrafts && loadDrafts()">我的{{ isPost ? '帖子' : '博客' }}</button></div>
    </header>
    <section v-if="showDrafts" class="draft-list content-panel" aria-label="我的创作">
      <h2>继续创作</h2>
      <p v-if="draftLoading" role="status">正在读取你的内容…</p>
      <p v-if="draftError" role="alert">{{ draftError }} <button @click="loadDrafts">重试</button></p>
      <RouterLink v-for="item in drafts" :key="item.id" :to="{ name: 'Compose', params: { type: kind }, query: { draft: item.id } }">{{ item.title || '无标题' }} <small>{{ item.isPublic ? '已发布' : '草稿' }}</small></RouterLink>
      <p v-if="!draftLoading && !draftError && !drafts.length">还没有保存的内容。</p>
      <RouterLink :to="{ name: 'Compose', params: { type: kind } }">＋ 新建{{ isPost ? '帖子' : '博客' }}</RouterLink>
    </section>
    <p v-if="loading" role="status">正在加载草稿…</p>
    <template v-else-if="!loadError">
      <label for="compose-title">{{ isPost ? '标题（可选）' : '标题' }}</label>
      <input id="compose-title" v-model="title" :disabled="busy" maxlength="100" placeholder="为内容起个标题" />
      <label for="compose-tags">标签（可选）</label>
      <input id="compose-tags" v-model="tags" :disabled="busy" maxlength="200" placeholder="用逗号分隔，例如：风景，创作记录，练习" />
      <fieldset :disabled="busy">
        <legend>正文</legend>
        <SpiritText
          :key="editorKey"
          ref="editor"
          standalone
          :disabled="busy"
          :initial-content="initialContent"
          @change="changeContent"
        />
      </fieldset>
      <label v-if="!isPost" for="compose-summary">{{ isPost ? '内容简介（可选）' : '文章摘要（可选）' }}</label>
      <textarea
        v-if="!isPost"
        id="compose-summary"
        v-model="summary"
        :disabled="busy"
        maxlength="150"
        placeholder="不填时由正文生成"
      />
      <p v-if="message" role="status">{{ message }}</p>
      <p v-if="error" role="alert">{{ error }}</p>
      <footer class="compose-footer">
        <button :disabled="busy" @click="save(false)">{{ busy ? '处理中…' : '保存草稿' }}</button>
        <button class="primary" :disabled="busy" @click="save(true)">{{ isPublished ? '更新发布' : '发布' }}</button>
        <RouterLink v-if="publishedId" :to="{ path: '/stream', query: { post: publishedId, tag: topic || undefined } }">查看发布内容 →</RouterLink>
        <RouterLink v-if="noteId" :to="{ name: 'SpiritNote', params: { id: noteId } }">在灵脉中整理</RouterLink>
      </footer>
    </template>
    <div v-else role="alert">
      <p>{{ loadError }}</p>
      <button @click="load">重试</button>
    </div>
  </main>
</template>

<script setup lang="ts">
import { ref, computed, watch, onUnmounted } from 'vue';
import { useRoute, useRouter, onBeforeRouteLeave, onBeforeRouteUpdate } from 'vue-router';
import SpiritText from '@/components/SpiritText.vue';
import { lingmaiApi } from '@/api/lingmai';
import { wikiTags, wikiText } from '@/utils/wikiContent';
import { useUserStore } from '@/stores/user';
const route = useRoute(), router = useRouter(), user = useUserStore();
const topic = computed(() => typeof route.query.tag === 'string' ? route.query.tag.trim().slice(0, 50) : '');
const kind = computed(() => route.params.type === 'post' ? 'post' : 'blog'), isPost = computed(() => kind.value === 'post');
const emptyDocument = () => ({ type: 'doc', content: [{ type: 'paragraph' }] });
const title = ref(''), tags = ref(''), summary = ref(''), content = ref<any>(emptyDocument()), initialContent = ref<any>(emptyDocument()), editor = ref<any>(), editorKey = ref(0);
const noteId = ref(''), publishedId = ref(''), loading = ref(false), loadError = ref(''), error = ref(''), message = ref(''), busy = ref(false);
let extraData = '[]', fixedBlocks: any[] = [], session = 0;
const saved = ref('');
const isPublished = ref(false), showDrafts = ref(false), draftLoading = ref(false), draftError = ref('');
const drafts = ref<{ id: string; title: string; isPublic: boolean }[]>([]);
let draftVersion = 0;
async function loadDrafts() {
    const version = ++draftVersion, currentSession = session, currentKind = kind.value;
    draftLoading.value = true; draftError.value = '';
    try {
        const spaces = await lingmaiApi.getSpaces() as { id: string }[];
        const results = await Promise.allSettled(spaces.map(space => lingmaiApi.getNoteList(space.id)));
        if (version !== draftVersion || currentSession !== session || currentKind !== kind.value) return;
        drafts.value = results.flatMap(result => result.status === 'fulfilled' ? result.value as any[] : []).filter(note => note.type === currentKind);
        if (results.some(result => result.status === 'rejected')) draftError.value = '部分内容未能读取，已显示成功读取的内容。';
    } catch { if (version === draftVersion) draftError.value = '内容列表加载失败，请重试。'; }
    finally { if (version === draftVersion) draftLoading.value = false; }
}
const fingerprint = () => JSON.stringify([title.value, tags.value, summary.value, content.value]);
const dirty = computed(() => fingerprint() !== saved.value);
function changeContent(value: any) { content.value = value; }
async function load() {
    const current = ++session;
    loading.value = true;
    loadError.value = '';
    error.value = '';
    message.value = '';
    saved.value = '';
    title.value = '';
    tags.value = topic.value;
    summary.value = '';
    content.value = emptyDocument();
    extraData = '[]';
    fixedBlocks = [];
    publishedId.value = '';
    isPublished.value = false;
    showDrafts.value = false;
    drafts.value = [];
    draftVersion++;
    noteId.value = typeof route.query.draft === 'string' ? route.query.draft : '';
    try {
        if (noteId.value) {
            const note: any = await lingmaiApi.getNote(noteId.value);
            if (current !== session)
                return;
            if (note.type !== kind.value)
                throw new Error('草稿类型与当前入口不符，请从灵脉打开原稿。');
            title.value = note.title || '';
            isPublished.value = Boolean(note.isPublic);
            tags.value = wikiTags(note.tags).join(', ');
            extraData = note.extraData || '[]';
            fixedBlocks = (note.blocks || []).filter((b: any) => b.type.startsWith('blog_fixed_'));
            const excerpt = fixedBlocks.find(b => b.type === 'blog_fixed_excerpt');
            if (excerpt) {
                const data = typeof excerpt.data === 'string' ? JSON.parse(excerpt.data) : excerpt.data;
                summary.value = data.text || '';
            }
            content.value = { ...note.tiptapContent, content: note.tiptapContent.content.filter((b: any) => !b.type.startsWith('blog_fixed_')) };
        }
        initialContent.value = content.value;
        editorKey.value++;
        saved.value = fingerprint();
    }
    catch (cause: any) {
        if (current === session)
            loadError.value = cause.friendlyMessage || cause.message || '草稿加载失败';
    }
    finally {
        if (current === session)
            loading.value = false;
    }
}
async function save(publish: boolean) {
    if (busy.value || !editor.value)
        return;
    error.value = '';
    message.value = '';
    try {
        content.value = editor.value.getSnapshot();
    }
    catch (cause: any) {
        error.value = cause.message;
        return;
    }
    if (!isPost.value && !title.value.trim()) {
        error.value = '请填写博客标题';
        return;
    }
    if (!wikiText(content.value).trim() && !JSON.stringify(content.value).includes('"image"') && !JSON.stringify(content.value).includes('"pdfEmbed"')) {
        error.value = '请先写一些内容或添加图片';
        return;
    }
    const tagList = wikiTags(tags.value);
    if (tagList.length > 10 || tagList.some(t => t.length > 50)) {
        error.value = '最多 10 个标签，每个不超过 50 字';
        return;
    }
    busy.value = true;
    const current = session;
    try {
        const finalTitle = title.value.trim() || wikiText(content.value).trim().slice(0, 50) || '图片分享';
        if (!noteId.value) {
            const spaces: any = await lingmaiApi.getSpaces();
            if (current !== session)
                return;
            const space = Array.isArray(spaces) && spaces.length ? spaces[0] : await lingmaiApi.createSpace('创作草稿') as any;
            if (current !== session)
                return;
            const note: any = await lingmaiApi.createNote({ title: finalTitle, type: kind.value, spaceId: space.id, folderId: null });
            if (current !== session)
                return;
            noteId.value = note.id;
            await router.replace({ query: { ...route.query, draft: note.id } });
        }
        // 后端按字符串排序；补零避免第 10 段被排到第 2 段前面。
        const blocks = (content.value.content || []).map((node: any, index: number) => ({ id: node.attrs?.id || crypto.randomUUID(), type: node.type, data: JSON.stringify(node), sortOrder: String(index).padStart(8, '0') }));
        const metadata = fixedBlocks.filter(b => b.type !== 'blog_fixed_excerpt');
        if (!isPost.value && summary.value.trim())
            metadata.push({ id: fixedBlocks.find(b => b.type === 'blog_fixed_excerpt')?.id || crypto.randomUUID(), type: 'blog_fixed_excerpt', data: JSON.stringify({ text: summary.value.trim() }), sortOrder: '-1' });
        await lingmaiApi.updateNoteContent(noteId.value, { noteId: noteId.value, title: finalTitle, tags: tagList, extraData, blocks: [...metadata, ...blocks] }, true);
        if (current !== session)
            return;
        saved.value = fingerprint();
        message.value = '草稿已保存';
        if (publish) {
            const result = await lingmaiApi.publishNote(noteId.value, { type: kind.value, tags: tagList });
            if (current !== session)
                return;
            publishedId.value = result.publishedId || '';
            isPublished.value = true;
            message.value = '发布成功，已进入社区和知识库。继续修改后，可再次点击“更新发布”。';
        }
    }
    catch (cause: any) {
        if (current === session)
            error.value = cause.friendlyMessage || '操作失败，正文仍保留在编辑器中，请重试';
    }
    finally {
        if (current === session)
            busy.value = false;
    }
}
function beforeUnload(event: BeforeUnloadEvent) { if (dirty.value || busy.value) {
    event.preventDefault();
    event.returnValue = '';
} }
window.addEventListener('beforeunload', beforeUnload);
onBeforeRouteLeave(() => !busy.value && (!dirty.value || window.confirm('有未保存的修改，确定离开？')));
watch(() => user.token, () => { session++; draftVersion++; drafts.value = []; showDrafts.value = false; busy.value = false; loadError.value = '登录状态已变化，请重新进入创作页面。'; }, { flush: 'sync' });
onBeforeRouteUpdate((to, from) => (to.params.type === from.params.type && to.query.tag === from.query.tag && String(to.query.draft || '') === noteId.value) || (!busy.value && (!dirty.value || window.confirm('有未保存的修改，确定切换草稿？'))));
watch([kind, () => route.query.draft, topic], (values, previous) => { if (!previous?.length || values[0] !== previous[0] || values[2] !== previous[2] || String(values[1] || '') !== noteId.value)
    void load(); }, { immediate: true });
onUnmounted(() => { session++; draftVersion++; window.removeEventListener('beforeunload', beforeUnload); });
</script>

<style scoped>

.compose-page { max-width: 920px; }
label { display: block; margin: 20px 0 8px; font-weight: 600; font-size: .9rem; }
input, textarea { width: 100%; padding: 12px; font: inherit; color: inherit; background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 8px; }
textarea { min-height: 90px; resize: vertical; }
fieldset { margin: 24px 0; min-height: 300px; min-width: 0; border: 1px solid var(--border-line); border-radius: 12px; padding: 20px; background: var(--paper-card); }
legend { padding: 0 8px; font-weight: 600; }
fieldset:disabled { pointer-events: none; opacity: .7; }
.compose-footer { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; padding: 16px 0; margin-top: 24px; border-top: 1px solid var(--border-line); background: var(--paper-bg); position: sticky; bottom: -1px; z-index: 2; }
button { padding: 10px 16px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: inherit; }
a { color: var(--accent-color); text-underline-offset: 3px; }
.draft-list a { display: flex; justify-content: space-between; gap: 16px; padding: 12px 0; border-bottom: 1px solid var(--border-line); }
.draft-list h2 { font-size: 1.1rem; }
:deep(.tiptap) { min-height: 260px; padding-top: 16px; line-height: 1.9; }
@media (max-width: 720px) {
  fieldset { padding: 12px; }
  /* 给全站左下角导航按钮留出位置，避免挡住保存草稿。 */
  .compose-footer { bottom: -20px; padding-left: 64px; }
}

</style>
