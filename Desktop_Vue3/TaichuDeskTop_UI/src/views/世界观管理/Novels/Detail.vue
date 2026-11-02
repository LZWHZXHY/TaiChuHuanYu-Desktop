<template>
  <div class="novel-detail content-page community-surface tone-world editor-surface">
    <RouterLink to="/novels" class="back">← 返回书架</RouterLink>

    <p v-if="loading" class="state">加载中…</p>
    <p v-else-if="!novel" class="state">小说不存在或未公开</p>

    <template v-else>
      <!-- 头部 -->
      <header class="novel-head studio-heading">
        <div class="head-cover">
          <img v-if="novel.coverUrl" :src="novel.coverUrl" :alt="novel.title" />
          <span v-else>{{ novel.title.slice(0, 1) }}</span>
        </div>
        <div class="head-main">
          <h1 class="head-title">{{ novel.title }}</h1>
          <p class="head-meta">
            <UserLink :id="novel.authorId" :name="novel.authorName" /> · {{ novel.chapters.length }} 章 · {{ formatWords(novel.wordCount) }} 字 · {{ statusLabel(novel.status) }}
            <span v-if="!novel.isPublic" class="draft">草稿</span>
            <RouterLink v-if="novel.projectName" :to="`/world/project/${novel.projectId}`" class="world-chip">🌌 {{ novel.projectName }}</RouterLink>
          </p>
          <p class="head-desc">{{ novel.description || '暂无简介' }}</p>
          <!-- 点赞、收藏、评论（草稿只有作者自己看得到，也只有作者能点） -->
          <WorldSocialBar class="head-social" kind="novel" :target-id="novel.id" comment-anchor="novel-comments" />
          <div class="head-actions">
            <button v-if="novel.chapters.length" class="btn primary" @click="openChapter(novel.chapters[0].id)">开始阅读</button>
            <template v-if="novel.isOwner">
              <button class="btn" @click="openNovelEdit">编辑信息</button>
              <button class="btn" @click="togglePublic">{{ novel.isPublic ? '转为草稿' : '公开' }}</button>
              <button class="btn danger" @click="removeNovel">删除</button>
            </template>
          </div>
          <p v-if="error" class="err">{{ error }}</p>
        </div>
      </header>

      <!-- 阅读模式 -->
      <template v-if="mode === 'read' && chapter">
        <div class="read-bar">
          <button class="btn" @click="mode = 'toc'">← 目录</button>
          <span class="read-pos"><template v-if="chapter.sectionPath">{{ chapter.sectionPath }} · </template>{{ chapter.index }} / {{ chapter.total }}</span>
          <button v-if="novel.isOwner" class="btn" @click="openChapterEdit(chapter)">编辑本章</button>
        </div>
        <article class="reader">
          <p v-if="chapter.sectionPath" class="chapter-path">{{ chapter.sectionPath }}</p>
          <h2 class="chapter-title">{{ chapterHeading(chapter) }}</h2>
          <!-- 正文里的 [[设定名]] 会变成链接，点了在右边打开那个设定 -->
          <div class="chapter-body" v-html="renderChapter(chapter.content)" @click="onBodyClick"></div>
          <nav class="chapter-nav">
            <button class="btn" :disabled="!chapter.prevId" @click="chapter.prevId && openChapter(chapter.prevId)">上一章</button>
            <button class="btn" :disabled="!chapter.nextId" @click="chapter.nextId && openChapter(chapter.nextId)">下一章</button>
          </nav>
        </article>
      </template>

      <!-- 目录：部 → 卷 / 番外 → 章 -->
      <NovelToc v-else :novel-id="novel.id" :chapters="novel.chapters" :sections="novel.sections ?? []" :is-owner="novel.isOwner"
        @open="openChapter" @new-chapter="openChapterCreate" @remove-chapter="removeChapter" @changed="reloadKeep" />
      <!-- 评论区：对整本小说的讨论 -->
      <section id="novel-comments" class="novel-comments">
        <ChaiComments target-type="novel" :target-id="novel.id" />
      </section>
    </template>

    <!-- 小说信息编辑 -->
    <div v-if="showNovelEdit" class="mask" @click.self="showNovelEdit = false">
      <div class="modal workbench-skin" role="dialog" aria-modal="true" aria-label="小说信息">
        <h2 class="modal-title studio-heading">编辑小说信息</h2>
        <p v-if="error" class="err" role="alert">{{ error }}</p>
        <label class="field"><span>标题 *</span><input v-model="novelForm.title" /></label>
        <label class="field"><span>简介</span><textarea v-model="novelForm.description" rows="3"></textarea></label>
        <div class="field"><span>封面</span><NovelCoverPicker v-model="novelCover" :fallback-text="novelForm.title" /></div>
        <div class="field-row">
          <label class="field"><span>状态</span>
            <select v-model="novelForm.status">
              <option value="ongoing">连载中</option>
              <option value="completed">已完结</option>
              <option value="paused">暂停</option>
            </select>
          </label>
        </div>
        <div class="modal-actions editor-footer">
          <button class="btn" @click="showNovelEdit = false">取消</button>
          <button class="btn primary" :disabled="saving" @click="saveNovel">保存</button>
        </div>
      </div>
    </div>

    <!-- 章节编辑 -->
    <div v-if="showChapterEdit" class="mask" @click.self="showChapterEdit = false">
      <div class="modal wide workbench-skin" role="dialog" aria-modal="true" aria-label="章节编辑">
        <h2 class="modal-title studio-heading">{{ editingChapterId ? '编辑章节' : '新增章节' }}</h2>
        <div class="field-row">
          <label class="field"><span>章节标题 *</span><input v-model="chapterForm.title" placeholder="标题（「第几章」会自动编号，不用写）" /></label>
          <label v-if="novel?.sections?.length" class="field"><span>放在</span>
            <select v-model="chapterForm.sectionId">
              <option :value="null">不分组（正文最前面）</option>
              <option v-for="o in sectionOptions" :key="o.id" :value="o.id">{{ o.label }}</option>
            </select>
          </label>
        </div>
        <label class="field"><span>正文（支持 Markdown）</span>
          <textarea ref="chapterInput" v-model="chapterForm.content" class="chapter-input" placeholder="在这里写正文…"></textarea>
        </label>
        <!-- 插入设定：绑定了世界观才有。写成 [[设定名]]，读者点了能看到那个设定 -->
        <div v-if="novel?.projectId" class="ref-tool">
          <button type="button" class="btn" @click="refOpen = !refOpen">🔗 插入世界观设定</button>
          <span class="hint">写成 <code>[[设定名]]</code>，或者 <code>[[设定名|显示的字]]</code>，读者点了会在旁边打开那个设定</span>
          <div v-if="refOpen" class="ref-pop">
            <input v-model="refQuery" class="ref-search" placeholder="搜设定名…" aria-label="搜设定名" />
            <p v-if="!worldCards.length" class="hint">{{ cardsError || '这个世界还没有设定，或者读取失败' }}</p>
            <ul v-else class="ref-list">
              <li v-for="cd in refMatches" :key="cd.id"><button type="button" @click="insertRef(cd.title)">{{ cd.title }}</button></li>
            </ul>
          </div>
        </div>
        <p class="hint">当前 {{ chapterForm.content.replace(/\s/g, '').length }} 字</p>
        <p v-if="chapterError" class="err">{{ chapterError }}</p>
        <div class="modal-actions editor-footer">
          <button class="btn" @click="showChapterEdit = false">取消</button>
          <button class="btn primary" :disabled="saving" @click="saveChapter">{{ saving ? '保存中…' : '保存章节' }}</button>
        </div>
      </div>
    </div>
    <!-- 正文里点设定链接：右边打开那个设定 -->
    <CardPeek v-if="peekId && novel?.projectId" :card-id="peekId" :project-id="novel.projectId" @close="peekId = null" @select="openCardPage" @pane="openCardPage" />
  </div>
</template>

<script setup lang="ts">
import UserLink from '@/components/UserLink.vue'
import WorldSocialBar from '@/components/WorldSocialBar.vue'
import ChaiComments from '@/views/柴圈板块/components/ChaiComments.vue'
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { worldNovelApi, type NovelDetail, type ChapterContent, type NovelPayload } from '@/api/worldNovel'
import { renderMarkdown } from '@/utils/markdown'
import { useRouter } from 'vue-router'
import { useWorldStore } from '@/stores/world'
import NovelToc from './NovelToc.vue'
import NovelCoverPicker from './NovelCoverPicker.vue'
import CardPeek from '../世界观组件/CardPeek.vue'

const route = useRoute()
const novelId = computed(() => String(route.params.id || ''))

const novel = ref<NovelDetail | null>(null)
const chapter = ref<ChapterContent | null>(null)
const loading = ref(false)
const error = ref('')
const mode = ref<'toc' | 'read'>('toc')

const showNovelEdit = ref(false)
const novelForm = ref<NovelPayload>({ title: '', description: '', coverUrl: '', status: 'ongoing' })

const showChapterEdit = ref(false)
const editingChapterId = ref<string | null>(null)
const chapterForm = ref<{ title: string; content: string; sectionId: string | null }>({ title: '', content: '', sectionId: null })
const router = useRouter()
const worldStore = useWorldStore()

// ===== 部 / 卷 / 番外 =====
const KIND_LABEL: Record<string, string> = { part: '部', volume: '卷', extra: '番外' }
/** 章节编辑里「放在」的选项（部里的卷缩进） */
const sectionOptions = computed(() => {
  const ss = novel.value?.sections ?? []
  const ids = new Set(ss.map(s => s.id))
  const tops = ss.filter(s => !s.parentId || !ids.has(s.parentId)).sort((a, b) => a.sortOrder - b.sortOrder)
  return tops.flatMap(t => [
    { id: t.id, label: `${KIND_LABEL[t.kind]}：${t.title}` },
    ...ss.filter(s => s.parentId === t.id).sort((a, b) => a.sortOrder - b.sortOrder).map(c => ({ id: c.id, label: `　└ ${KIND_LABEL[c.kind]}：${c.title}` })),
  ])
})
/** 标题：正文的章「第 N 章 · 标题」，番外的章只有标题 */
const chapterHeading = (c: ChapterContent) => (c.displayNo === null ? c.title : `第 ${c.displayNo ?? c.number} 章 · ${c.title}`)

// ===== 世界观设定链接：[[设定名]] / [[设定名|显示的字]] =====
const worldCards = ref<{ id: string; title: string }[]>([])
const cardsError = ref('')
const cardByTitle = computed(() => new Map(worldCards.value.map(c => [c.title.trim(), c.id])))
const peekId = ref<string | null>(null)
async function loadWorldCards() {
  worldCards.value = []
  cardsError.value = ''
  const pid = novel.value?.projectId
  if (!pid) return
  try { worldCards.value = (await worldStore.fetchCards(pid)).map(c => ({ id: c.id, title: c.title })) }
  catch { cardsError.value = '读不到这个世界的设定（可能是私有世界）' }
}
const decode = (s: string) => s.replace(/&amp;/g, '&').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, "'")
/** 正文渲染后把 [[...]] 换成链接；找不到这个设定（或者看不到这个世界）的就只显示文字 */
function renderChapter(md: string) {
  return renderMarkdown(md).replace(/\[\[([^\[\]|\n<>]{1,60})(?:\|([^\[\]\n<>]{1,60}))?\]\]/g, (_m, name: string, label?: string) => {
    const text = (label ?? name).trim()
    const id = cardByTitle.value.get(decode(name).trim())
    return id ? `<a href="#" class="wref" data-card="${id}" title="查看设定：${name.trim()}">${text}</a>` : `<span class="wref-missing">${text}</span>`
  })
}
function onBodyClick(e: MouseEvent) {
  const a = (e.target as HTMLElement)?.closest?.('a.wref') as HTMLElement | null
  if (!a) return
  e.preventDefault()
  peekId.value = a.dataset.card ?? null
}
const openCardPage = (id: string) => { const pid = novel.value?.projectId; peekId.value = null; if (pid) void router.push(`/world/project/${pid}/card/${id}`) }

// 章节编辑里插入设定
const refOpen = ref(false)
const refQuery = ref('')
const chapterInput = ref<HTMLTextAreaElement | null>(null)
const refMatches = computed(() => {
  const q = refQuery.value.trim().toLowerCase()
  return (q ? worldCards.value.filter(c => c.title.toLowerCase().includes(q)) : worldCards.value).slice(0, 40)
})
function insertRef(title: string) {
  const el = chapterInput.value
  const text = `[[${title}]]`
  const v = chapterForm.value.content
  const start = el?.selectionStart ?? v.length, end = el?.selectionEnd ?? v.length
  chapterForm.value.content = v.slice(0, start) + text + v.slice(end)
  refOpen.value = false
  refQuery.value = ''
  requestAnimationFrame(() => { el?.focus(); el?.setSelectionRange(start + text.length, start + text.length) })
}

// 封面：v-model 用字符串（表单里 coverUrl 可能是 null）
const novelCover = computed({ get: () => novelForm.value.coverUrl || '', set: v => { novelForm.value.coverUrl = v } })

const chapterError = ref('')
const saving = ref(false)

const statusLabel = (s: string) => ({ ongoing: '连载中', completed: '已完结', paused: '暂停' } as Record<string, string>)[s] || s
const formatWords = (n: number) => (n >= 10000 ? `${(n / 10000).toFixed(1)} 万` : `${n}`)

async function load() {
  if (!novelId.value) return
  loading.value = true
  error.value = ''
  try {
    novel.value = await worldNovelApi.detail(novelId.value)
    mode.value = 'toc'
    chapter.value = null
    void loadWorldCards()
  } catch (e: any) {
    novel.value = null
    error.value = e.friendlyMessage || ''
  } finally { loading.value = false }
}

async function openChapter(chapterId: string) {
  try {
    chapter.value = await worldNovelApi.chapter(novelId.value, chapterId)
    mode.value = 'read'
    window.scrollTo({ top: 0, behavior: 'smooth' })
  } catch (e: any) { error.value = e.friendlyMessage || '章节加载失败' }
}

function openNovelEdit() {
  if (!novel.value) return
  novelForm.value = {
    title: novel.value.title,
    description: novel.value.description || '',
    coverUrl: novel.value.coverUrl || '',
    status: novel.value.status
  }
  showNovelEdit.value = true
}

async function saveNovel() {
  if (!novelForm.value.title.trim()) { error.value = '标题不能为空'; return }
  saving.value = true
  try {
    await worldNovelApi.update(novelId.value, novelForm.value)
    showNovelEdit.value = false
    await load()
  } catch (e: any) { error.value = e.friendlyMessage || '保存失败' } finally { saving.value = false }
}

async function togglePublic() {
  if (!novel.value) return
  try {
    await worldNovelApi.update(novelId.value, { title: novel.value.title, isPublic: !novel.value.isPublic })
    await load()
  } catch (e: any) { error.value = e.friendlyMessage || '操作失败' }
}

async function removeNovel() {
  if (!confirm('删除这部小说？所有章节会一起删掉，不可恢复。')) return
  await worldNovelApi.remove(novelId.value)
  await load()
}

/** 改目录后重新读，但不跳回顶部、不清掉正在看的东西 */
async function reloadKeep() {
  try { novel.value = await worldNovelApi.detail(novelId.value) } catch (e: any) { error.value = e.friendlyMessage || '刷新失败' }
}

function openChapterCreate(sectionId: string | null = null) {
  editingChapterId.value = null
  chapterForm.value = { title: '', content: '', sectionId }
  chapterError.value = ''
  showChapterEdit.value = true
}

function openChapterEdit(c: ChapterContent) {
  editingChapterId.value = c.id
  chapterForm.value = { title: c.title, content: c.content, sectionId: c.sectionId ?? null }
  chapterError.value = ''
  showChapterEdit.value = true
}

async function saveChapter() {
  if (!chapterForm.value.title.trim()) { chapterError.value = '请填写章节标题'; return }
  saving.value = true
  chapterError.value = ''
  try {
    const { title, content, sectionId } = chapterForm.value
    if (editingChapterId.value) {
      await worldNovelApi.updateChapter(novelId.value, editingChapterId.value, { title, content })
      // 换了分组就挪过去
      const before = novel.value?.chapters.find(c => c.id === editingChapterId.value)?.sectionId ?? null
      if ((sectionId ?? null) !== before) await worldNovelApi.setChapterSection(novelId.value, editingChapterId.value, sectionId)
    } else {
      const res = await worldNovelApi.addChapter(novelId.value, { title, content, sectionId })
      editingChapterId.value = res.id
    }
    showChapterEdit.value = false
    await load()
    if (editingChapterId.value) await openChapter(editingChapterId.value)
  } catch (e: any) {
    chapterError.value = e.friendlyMessage || '保存失败'
  } finally { saving.value = false }
}

async function removeChapter(id: string) {
  if (!confirm('删除这一章？')) return
  await worldNovelApi.removeChapter(novelId.value, id)
  await load()
}

onMounted(load)
watch(novelId, load)
</script>

<style scoped>
.chapter-path { margin: 0 0 6px; font-size: .8rem; letter-spacing: .08em; color: var(--text-secondary); }
.chapter-body :deep(a.wref) { color: var(--accent-color); text-decoration: none; border-bottom: 1px dashed currentColor; cursor: pointer; }
.chapter-body :deep(a.wref:hover) { background: var(--accent-soft); border-bottom-style: solid; }
.chapter-body :deep(.wref-missing) { border-bottom: 1px dotted var(--border-line); }
.ref-tool { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; position: relative; }
.ref-tool code { padding: 0 4px; background: var(--bg-sub); }
.ref-pop { flex-basis: 100%; display: grid; gap: 8px; padding: 10px; border: 1px solid var(--border-line); background: var(--bg-main); }
.ref-search { padding: 7px 10px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: .85rem; }
.ref-list { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: 6px; max-height: 160px; overflow-y: auto; }
.ref-list button { padding: 3px 10px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: .8rem; cursor: pointer; }
.ref-list button:hover { border-color: var(--accent-color); color: var(--accent-color); }
.head-social { margin: 0 0 12px; }
.novel-comments { scroll-margin-top: 16px; }
.novel-detail { max-width: 900px; margin: 0 auto; padding-bottom: 56px; }
.back { color: var(--accent-color); text-decoration: none; font-size: .85rem; }
.state { padding: 48px 0; text-align: center; color: var(--text-secondary); }
.err { margin: 0; color: var(--danger); font-size: .82rem; }

.novel-head { display: flex; gap: 24px; padding: 24px 0; border-bottom: 1px solid var(--border-line); margin-top: 12px; }
.head-cover { width: 160px; aspect-ratio: 3 / 4; flex-shrink: 0; border-radius: 0; overflow: hidden; background: linear-gradient(160deg, var(--accent-soft), var(--bg-sub)); display: grid; place-items: center; }
.head-cover img { width: 100%; height: 100%; object-fit: cover; }
.head-cover span { font-size: 3rem; font-weight: 800; color: var(--accent-color); }
.head-main { min-width: 0; flex: 1; }
.head-title { margin: 0 0 8px; font-size: clamp(1.4rem, 3vw, 1.9rem); overflow-wrap: anywhere; }
.head-meta { margin: 0 0 10px; font-size: .8rem; color: var(--text-secondary); display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
.draft { padding: 1px 8px; border-radius: 0; background: var(--bg-sub); font-size: .72rem; }
.world-chip { padding: 2px 10px; border-radius: 0; border: 1px solid var(--border-line); color: var(--accent-color); text-decoration: none; font-size: .72rem; }
.world-chip:hover { border-color: var(--accent-color); }
.head-desc { margin: 0 0 16px; font-size: .9rem; line-height: 1.8; color: var(--text-secondary); white-space: pre-wrap; }
.head-actions { display: flex; flex-wrap: wrap; gap: 8px; }

.btn { padding: 8px 16px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); cursor: pointer; font-size: .84rem; }
.btn.primary { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); }
.btn.danger { color: var(--danger); border-color: var(--danger); }
.btn:disabled { opacity: .45; cursor: default; }
.mini { padding: 4px 10px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-secondary); cursor: pointer; font-size: .74rem; }
.mini.danger { color: var(--danger); }

.toc-head { display: flex; justify-content: space-between; align-items: center; margin: 26px 0 14px; }
.toc-head h2 { margin: 0; font-size: 1.1rem; }
.toc-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 6px; }
.toc-row { display: flex; gap: 8px; align-items: center; }
.toc-item { flex: 1; display: flex; align-items: center; gap: 12px; padding: 11px 14px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); cursor: pointer; text-align: left; font-size: .88rem; }
.toc-item:hover { border-color: var(--accent-color); }
.toc-num { flex-shrink: 0; width: 26px; color: var(--text-secondary); font-variant-numeric: tabular-nums; }
.toc-title { flex: 1; min-width: 0; overflow-wrap: anywhere; }
.toc-words { flex-shrink: 0; font-size: .72rem; color: var(--text-secondary); }

.read-bar { display: flex; align-items: center; gap: 12px; margin: 20px 0; }
.read-pos { font-size: .8rem; color: var(--text-secondary); }
.reader { max-width: 720px; margin: 0 auto; }
.chapter-title { font-size: 1.35rem; margin: 0 0 24px; }
.chapter-body { font-size: 1rem; line-height: 2; overflow-wrap: anywhere; }
.chapter-body :deep(p) { margin: 0 0 18px; }
.chapter-body :deep(h1), .chapter-body :deep(h2), .chapter-body :deep(h3) { margin: 28px 0 12px; }
.chapter-body :deep(blockquote) { margin: 0 0 18px; padding: 10px 16px; border-left: 3px solid var(--accent-color); background: var(--bg-sub); color: var(--text-secondary); }
.chapter-body :deep(img) { max-width: 100%; border-radius: 0; display: block; margin: 18px auto; }
.chapter-nav { display: flex; justify-content: space-between; gap: 12px; margin-top: 40px; padding-top: 20px; border-top: 1px dashed var(--border-line); }

.mask { position: fixed; inset: 0; z-index: 120; background: rgba(0,0,0,.45); display: flex; align-items: center; justify-content: center; padding: 20px; }
.modal { width: min(560px, 100%); max-height: calc(100dvh - 40px); box-sizing: border-box; overflow-y: auto; display: grid; gap: 14px; padding: 24px; border-radius: 0; border: 1px solid var(--surface-edge); background: var(--paper-card); }
.modal.wide { width: min(860px, 100%); }
.modal-title { margin: 0; font-size: 1.15rem; }
.field { display: grid; gap: 6px; font-size: .82rem; color: var(--text-secondary); }
.field input, .field select, .field textarea { padding: 9px 12px; border: 1px solid var(--border-line); border-radius: 0; background: var(--bg-main); color: var(--text-primary); font: inherit; font-size: .88rem; }
.field-row { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.chapter-input { min-height: min(380px,45dvh); line-height: 1.8; resize: vertical; font-family: ui-monospace, monospace; }
.hint { margin: 0; font-size: .76rem; color: var(--text-secondary); }
.modal-actions { display: flex; justify-content: flex-end; gap: 10px; }
@media (max-width: 640px) { .novel-head { flex-direction: column; } .head-cover { width: 120px; } }
.modal-actions { position: sticky; bottom: -24px; padding: 16px 0; background: var(--paper-card); border-top: 1px solid var(--border-line); }
.modal .field input,.modal .field textarea,.modal .field select { min-width: 0; width: 100%; box-sizing: border-box; }
@media(max-width:640px) { .mask { padding: 12px; }.modal { padding: 16px; max-height: calc(100dvh - 24px); }.modal-actions { bottom: -16px; }.field-row { grid-template-columns: 1fr; } }
</style>
