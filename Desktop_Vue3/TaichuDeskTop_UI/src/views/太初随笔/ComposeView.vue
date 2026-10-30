<template>
  <main class="compose-page content-page workbench-skin editor-surface community-surface tone-library">
    <header class="content-header studio-heading"><SectionTrace />
      <div>
      <RouterLink :to="{ path: '/stream', query: topic ? { tag: topic } : {} }">← {{ topic || FORUM_NAME }}</RouterLink>
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
      <div class="compose-fields">
      <section class="compose-main" aria-label="正文编辑">
      <label for="compose-title">{{ isPost ? '标题（可选）' : '标题' }}</label>
      <input id="compose-title" v-model="title" :disabled="busy" maxlength="100" placeholder="为内容起个标题" />
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
      </section>
      <aside class="compose-settings" aria-label="发布设置">
        <h2>发布设置</h2>
      <!-- 从世界任务页点「发帖报告」进来：发布后自动把这篇帖子提交给管理员审核 -->
      <section v-if="questId" class="quest-box" aria-label="世界任务报告">
        <p v-if="questLoading" class="field-hint">正在读取世界任务…</p>
        <template v-else-if="quest">
          <strong>⛏ 世界任务报告</strong>
          <p class="quest-title">{{ quest.line === 'side' ? '支线' : '主线' }} ·《{{ quest.title }}》</p>
          <template v-if="!questReported">
            <label v-if="quest.kind !== 'goal'" for="quest-amount">这次完成的数量（{{ quest.unit || '个' }}）</label>
            <input v-if="quest.kind !== 'goal'" id="quest-amount" v-model.number="questAmount" type="number" min="1" :disabled="busy" />
            <label for="quest-player">游戏里的名字</label>
            <input id="quest-player" v-model.trim="questPlayer" maxlength="50" :disabled="busy" placeholder="方便管理员去服务器里核对" />
            <p class="field-hint">发布后这篇帖子会自动提交给管理员，管理员看帖确认{{ quest.kind === 'goal' ? '后就会标记任务完成' : '后计入进度' }}。截图、坐标写在正文里就行。</p>
          </template>
          <p v-else class="field-hint">✓ 已提交给管理员审核，可以在世界任务页「我的贡献」里看进度。</p>
        </template>
        <p v-else class="field-hint">{{ questError || '这个世界任务已经结束了，帖子照常发布，不会提交审核。' }}</p>
      </section>
      <label for="compose-space">保存到空间</label>
      <select id="compose-space" v-model="spaceId" :disabled="busy || !spaces.length">
        <option v-for="space in spaces" :key="space.id" :value="space.id">{{ space.name }}</option>
      </select>
      <p class="field-hint">{{ spaces.length ? '这篇内容的草稿会保存在灵脉的这个空间里。' : '还没有空间，保存时会自动创建「创作草稿」空间。' }}</p>
      <label for="compose-board">发布到版块</label>
      <select id="compose-board" v-model="boardTag" :disabled="busy">
        <option value="">不选版块（只在「全部」里显示）</option>
        <option v-for="item in FORUM_BOARDS" :key="item.tag" :value="item.tag">{{ item.icon }} {{ item.tag }}</option>
      </select>
      <fieldset v-if="selectedBoard" class="sub-picker" :disabled="busy">
        <legend>小类</legend>
        <label v-for="item in availableSubs" :key="item.tag" class="sub-option">
          <input v-model="subTag" type="radio" name="compose-sub" :value="item.tag" />
          <span><strong>{{ item.tag }}</strong><small>{{ item.desc }}</small></span>
        </label>
      </fieldset>
      <label for="compose-tags">其他标签（可选）</label>
      <input id="compose-tags" v-model="tags" :disabled="busy" maxlength="200" placeholder="用逗号分隔，例如：风景，创作记录，练习" />
      <label v-if="!isPost" for="compose-summary">{{ isPost ? '内容简介（可选）' : '文章摘要（可选）' }}</label>
      <textarea
        v-if="!isPost"
        id="compose-summary"
        v-model="summary"
        :disabled="busy"
        maxlength="150"
        placeholder="不填时由正文生成"
      />
      </aside>
      </div>
      <p v-if="message" role="status">{{ message }}</p>
      <p v-if="error" role="alert">{{ error }}</p>
      <footer class="compose-footer">
        <button :disabled="busy" @click="save(false)">{{ busy ? '处理中…' : '保存草稿' }}</button>
        <button class="primary" :disabled="busy" @click="save(true)">{{ isPublished ? '更新发布' : '发布' }}</button>
        <RouterLink v-if="publishedId" :to="{ path: '/stream', query: { post: publishedId, tag: boardTag || undefined } }">查看发布内容 →</RouterLink>
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
import SectionTrace from '@/components/SectionTrace.vue'
import { ref, computed, watch, onUnmounted } from 'vue';
import { useRoute, useRouter, onBeforeRouteLeave, onBeforeRouteUpdate } from 'vue-router';
import SpiritText from '@/components/SpiritText.vue';
import { lingmaiApi } from '@/api/lingmai';
import { wikiTags, wikiText } from '@/utils/wikiContent';
import { FORUM_BOARDS, FORUM_NAME, findBoard, boardOf, applyBoardTags } from '@/utils/forumBoards';
import { canOpenAdmin } from '@/utils/adminAccess';
import { useUserStore } from '@/stores/user';
import { questApi, type ServerQuest } from '@/api/quests';
const route = useRoute(), router = useRouter(), user = useUserStore();
const topic = computed(() => typeof route.query.tag === 'string' ? route.query.tag.trim().slice(0, 50) : '');
const querySub = computed(() => typeof route.query.sub === 'string' ? route.query.sub.trim().slice(0, 50) : '');
// 版块和小类单独选择，保存时写入标签；「其他标签」只放用户自己的标签。
const boardTag = ref(''), subTag = ref('');
const selectedBoard = computed(() => findBoard(boardTag.value));
const isAdmin = computed(() => canOpenAdmin(user.permissions));
const availableSubs = computed(() => (selectedBoard.value?.subs || []).filter(sub => !sub.adminOnly || isAdmin.value));
const normalizeSub = () => { if (!availableSubs.value.some(sub => sub.tag === subTag.value)) subTag.value = availableSubs.value[0]?.tag || ''; };
watch(boardTag, normalizeSub);
const kind = computed(() => route.params.type === 'post' ? 'post' : 'blog'), isPost = computed(() => kind.value === 'post');
const emptyDocument = () => ({ type: 'doc', content: [{ type: 'paragraph' }] });
const title = ref(''), tags = ref(''), summary = ref(''), content = ref<any>(emptyDocument()), initialContent = ref<any>(emptyDocument()), editor = ref<any>(), editorKey = ref(0);
const noteId = ref(''), publishedId = ref(''), loading = ref(false), loadError = ref(''), error = ref(''), message = ref(''), busy = ref(false);
let extraData = '[]', fixedBlocks: any[] = [], session = 0;
const saved = ref('');
// ---------- 世界任务报告（?quest=任务id）----------
const QUEST_NAME_KEY = 'taichu.mcPlayerName';
const questId = computed(() => { const n = Number(route.query.quest); return Number.isInteger(n) && n > 0 ? n : 0; });
const quest = ref<ServerQuest | null>(null), questLoading = ref(false), questError = ref(''), questReported = ref(false);
const questAmount = ref<number | null>(null), questPlayer = ref(readKeySafe(QUEST_NAME_KEY));
function readKeySafe(key: string) { try { return localStorage.getItem(key) || ''; } catch { return ''; } }
async function loadQuest() {
    quest.value = null; questError.value = ''; questReported.value = false;
    if (!questId.value) return;
    questLoading.value = true;
    try {
        const overview = await questApi.overview();
        const list = overview.actives ?? (overview.active ? [overview.active] : []);
        quest.value = list.find(q => q.id === questId.value) ?? null;
        if (quest.value && !title.value.trim()) title.value = `【任务报告】${quest.value.title}`;
    } catch { questError.value = '世界任务读取失败，帖子照常发布，但不会自动提交审核。'; }
    finally { questLoading.value = false; }
}
watch(questId, () => void loadQuest(), { immediate: true });
/** 帖子发布后提交世界任务报告（只提交一次） */
async function reportQuest(postId: string) {
    const q = quest.value;
    if (!q || questReported.value || !postId) return '';
    try {
        await questApi.contribute(q.id, { amount: q.kind === 'goal' ? 1 : Number(questAmount.value) || 0, playerName: questPlayer.value || undefined, postId });
        questReported.value = true;
        try { localStorage.setItem(QUEST_NAME_KEY, questPlayer.value); } catch { /* 忽略 */ }
        return '，任务报告已提交给管理员审核';
    } catch (cause: any) {
        return `，但任务报告没提交成功（${cause?.friendlyMessage || cause?.message || '请稍后到世界任务页重试'}）`;
    }
}
// 草稿所在的灵脉空间：默认沿用上次的选择，避免总是落进第一个空间。
const COMPOSE_SPACE_KEY = 'taichu-compose-space', LINGMAI_SPACE_KEY = 'taichu-lingmai-space';
const spaces = ref<{ id: string; name: string }[]>([]), spaceId = ref(''), draftSpaceId = ref('');
const readKey = (key: string) => { try { return localStorage.getItem(key) || ''; } catch { return ''; } };
async function loadSpaces(current: number) {
    try {
        const list = await lingmaiApi.getSpaces() as { id: string; name: string }[];
        if (current !== session) return;
        spaces.value = Array.isArray(list) ? list : [];
    } catch { if (current === session) spaces.value = []; }
    if (current !== session) return;
    const has = (id: string) => spaces.value.some(space => space.id === id);
    spaceId.value = [draftSpaceId.value, readKey(COMPOSE_SPACE_KEY), readKey(LINGMAI_SPACE_KEY)].find(id => id && has(id)) || spaces.value[0]?.id || '';
    if (!dirty.value) saved.value = fingerprint();
}
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
const fingerprint = () => JSON.stringify([title.value, tags.value, summary.value, content.value, spaceId.value, boardTag.value, subTag.value]);
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
    tags.value = '';
    boardTag.value = findBoard(topic.value) ? topic.value : '';
    subTag.value = querySub.value;
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
    draftSpaceId.value = '';
    try {
        if (noteId.value) {
            const note: any = await lingmaiApi.getNote(noteId.value);
            if (current !== session)
                return;
            if (note.type !== kind.value)
                throw new Error('草稿类型与当前入口不符，请从灵脉打开原稿。');
            title.value = note.title || '';
            draftSpaceId.value = note.spaceId || '';
            isPublished.value = Boolean(note.isPublic);
            // 从分区入口打开旧草稿时，补上该分区标签，否则发布后在分区里看不到。
            // 草稿已有版块时沿用；没有版块而是从版块入口打开的，补上该版块。
            const existing = boardOf(note.tags);
            tags.value = applyBoardTags(wikiTags(note.tags), '', '').join(', ');
            if (existing.board) { boardTag.value = existing.board.tag; subTag.value = existing.sub?.tag || ''; }
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
        normalizeSub();
        await loadSpaces(current);
        if (current !== session)
            return;
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
    if (selectedBoard.value && !subTag.value) {
        error.value = '请选择一个小类';
        return;
    }
    if (publish && quest.value && !questReported.value && quest.value.kind !== 'goal' && !((Number(questAmount.value) || 0) > 0)) {
        error.value = '请填写这次完成的数量';
        return;
    }
    const tagList = applyBoardTags(wikiTags(tags.value), boardTag.value, subTag.value);
    if (tagList.length > 10 || tagList.some(t => t.length > 50)) {
        error.value = '最多 10 个标签（含版块），每个不超过 50 字';
        return;
    }
    busy.value = true;
    const current = session;
    try {
        const finalTitle = title.value.trim() || wikiText(content.value).trim().slice(0, 50) || '图片分享';
        if (!spaceId.value) {
            const space = await lingmaiApi.createSpace('创作草稿') as any;
            if (current !== session)
                return;
            spaces.value = [...spaces.value, space];
            spaceId.value = space.id;
        }
        try { localStorage.setItem(COMPOSE_SPACE_KEY, spaceId.value); } catch { /* 仅本次有效 */ }
        if (noteId.value && draftSpaceId.value && draftSpaceId.value !== spaceId.value) {
            await lingmaiApi.updateNoteMeta(noteId.value, { spaceId: spaceId.value });
            if (current !== session)
                return;
            draftSpaceId.value = spaceId.value;
        }
        if (!noteId.value) {
            const note: any = await lingmaiApi.createNote({ title: finalTitle, type: kind.value, spaceId: spaceId.value, folderId: null });
            if (current !== session)
                return;
            noteId.value = note.id;
            draftSpaceId.value = spaceId.value;
            await router.replace({ query: { ...route.query, draft: note.id } });
        }
        // 后端 Block.SortOrder 是 int，这里发数字，不要用补零字符串（会导致 JSON 反序列化失败、正文块丢失）。
        const blocks = (content.value.content || []).map((node: any, index: number) => ({ id: node.attrs?.id || crypto.randomUUID(), type: node.type, data: JSON.stringify(node), sortOrder: index }));
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
            const questNote = await reportQuest(publishedId.value);
            if (current !== session)
                return;
            message.value = `发布成功${questNote}。继续修改后，可再次点击“更新发布”。`;
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
watch([kind, () => route.query.draft, topic, querySub], (values, previous) => { if (!previous?.length || values[0] !== previous[0] || values[2] !== previous[2] || values[3] !== previous[3] || String(values[1] || '') !== noteId.value)
    void load(); }, { immediate: true });
onUnmounted(() => { session++; draftVersion++; window.removeEventListener('beforeunload', beforeUnload); });
</script>

<style scoped>

.compose-page { max-width: 1200px; }
.compose-fields { display: grid; grid-template-columns: minmax(0, 1fr) 240px; gap: 24px; align-items: start; }
.compose-main { min-width: 0; }
.compose-settings { padding: 20px; background: var(--bg-sub); border-top: 2px solid var(--text-primary); }
.compose-settings h2 { margin: 0; font-size: 14px; }
label { display: block; margin: 20px 0 8px; font-weight: 600; font-size: .9rem; }
.sub-picker { margin: 12px 0 0; min-height: 0; padding: 8px 12px; background: var(--paper-card); }
.sub-picker legend { font-size: .85rem; }
.sub-option { display: flex; align-items: flex-start; gap: 8px; margin: 8px 0; font-weight: 400; cursor: pointer; }
.sub-option input { width: auto; margin-top: 4px; }
.sub-option strong { display: block; font-size: .88rem; }
.sub-option small { display: block; color: var(--text-secondary); font-size: .78rem; }
.quest-box { margin: 14px 0 4px; padding: 12px; background: var(--paper-card); border-left: 3px solid var(--accent-color); }
.quest-box strong { font-size: .88rem; }
.quest-box .quest-title { margin: 4px 0 0; font-size: .9rem; }
.quest-box label { margin: 12px 0 6px; font-size: .82rem; }
.quest-box input { padding: 8px 10px; }
.field-hint { margin: 6px 0 0; font-size: 12px; line-height: 1.6; color: var(--text-secondary); }
input, textarea, select { width: 100%; padding: 12px; font: inherit; color: inherit; background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 0; }
textarea { min-height: 90px; resize: vertical; }
fieldset { margin: 24px 0; min-height: 300px; min-width: 0; border: 1px solid var(--border-line); border-radius: 0; padding: 20px; background: var(--paper-card); }
legend { padding: 0 8px; font-weight: 600; }
fieldset:disabled { pointer-events: none; opacity: .7; }
.compose-footer { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; padding: 16px 0; margin-top: 24px; border-top: 1px solid var(--border-line); background: var(--paper-bg); position: sticky; bottom: -1px; z-index: 2; }
button { padding: 10px 16px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: inherit; }
a { color: var(--accent-color); text-underline-offset: 3px; }
.draft-list a { display: flex; justify-content: space-between; gap: 16px; padding: 12px 0; border-bottom: 1px solid var(--border-line); }
.draft-list h2 { font-size: 1.1rem; }
:deep(.tiptap) { min-height: 260px; padding-top: 16px; line-height: 1.9; }
@media (max-width: 1000px) { .compose-fields { grid-template-columns: 1fr; }.compose-settings { padding: 16px; } }
@media (max-width: 720px) {
  fieldset { padding: 12px; }
  .compose-footer { bottom: 0; }
}

</style>
