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
            {{ novel.authorName }} · {{ novel.chapters.length }} 章 · {{ formatWords(novel.wordCount) }} 字 · {{ statusLabel(novel.status) }}
            <span v-if="!novel.isPublic" class="draft">草稿</span>
            <RouterLink v-if="novel.projectName" :to="`/world/project/${novel.projectId}`" class="world-chip">🌌 {{ novel.projectName }}</RouterLink>
          </p>
          <p class="head-desc">{{ novel.description || '暂无简介' }}</p>
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
          <span class="read-pos">{{ chapter.index }} / {{ chapter.total }}</span>
          <button v-if="novel.isOwner" class="btn" @click="openChapterEdit(chapter)">编辑本章</button>
        </div>
        <article class="reader">
          <h2 class="chapter-title">第 {{ chapter.number }} 章 · {{ chapter.title }}</h2>
          <div class="chapter-body" v-html="renderMarkdown(chapter.content)"></div>
          <nav class="chapter-nav">
            <button class="btn" :disabled="!chapter.prevId" @click="chapter.prevId && openChapter(chapter.prevId)">上一章</button>
            <button class="btn" :disabled="!chapter.nextId" @click="chapter.nextId && openChapter(chapter.nextId)">下一章</button>
          </nav>
        </article>
      </template>

      <!-- 目录 -->
      <template v-else>
        <div class="toc-head">
          <h2>目录</h2>
          <button v-if="novel.isOwner" class="btn primary" @click="openChapterCreate">＋ 新增章节</button>
        </div>
        <ol v-if="novel.chapters.length" class="toc-list">
          <li v-for="c in novel.chapters" :key="c.id" class="toc-row">
            <button class="toc-item" @click="openChapter(c.id)">
              <span class="toc-num">{{ c.number }}</span>
              <span class="toc-title">{{ c.title }}</span>
              <span class="toc-words">{{ c.wordCount }} 字</span>
            </button>
            <button v-if="novel.isOwner" class="mini danger" @click="removeChapter(c.id)">删除</button>
          </li>
        </ol>
        <p v-else class="state">还没有章节，作者可以点右上角「新增章节」开始写。</p>
      </template>
    </template>

    <!-- 小说信息编辑 -->
    <div v-if="showNovelEdit" class="mask" @click.self="showNovelEdit = false">
      <div class="modal workbench-skin" role="dialog" aria-modal="true" aria-label="小说信息">
        <h2 class="modal-title studio-heading">编辑小说信息</h2>
        <p v-if="error" class="err" role="alert">{{ error }}</p>
        <label class="field"><span>标题 *</span><input v-model="novelForm.title" /></label>
        <label class="field"><span>简介</span><textarea v-model="novelForm.description" rows="3"></textarea></label>
        <label class="field"><span>封面图链接</span><input v-model="novelForm.coverUrl" /></label>
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
        <label class="field"><span>章节标题 *</span><input v-model="chapterForm.title" placeholder="第几章 · 标题" /></label>
        <label class="field"><span>正文（支持 Markdown）</span>
          <textarea v-model="chapterForm.content" class="chapter-input" placeholder="在这里写正文…"></textarea>
        </label>
        <p class="hint">当前 {{ chapterForm.content.replace(/\s/g, '').length }} 字</p>
        <p v-if="chapterError" class="err">{{ chapterError }}</p>
        <div class="modal-actions editor-footer">
          <button class="btn" @click="showChapterEdit = false">取消</button>
          <button class="btn primary" :disabled="saving" @click="saveChapter">{{ saving ? '保存中…' : '保存章节' }}</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { worldNovelApi, type NovelDetail, type ChapterContent, type NovelPayload } from '@/api/worldNovel'
import { renderMarkdown } from '@/utils/markdown'

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
const chapterForm = ref({ title: '', content: '' })
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

function openChapterCreate() {
  editingChapterId.value = null
  chapterForm.value = { title: '', content: '' }
  chapterError.value = ''
  showChapterEdit.value = true
}

function openChapterEdit(c: ChapterContent) {
  editingChapterId.value = c.id
  chapterForm.value = { title: c.title, content: c.content }
  chapterError.value = ''
  showChapterEdit.value = true
}

async function saveChapter() {
  if (!chapterForm.value.title.trim()) { chapterError.value = '请填写章节标题'; return }
  saving.value = true
  chapterError.value = ''
  try {
    if (editingChapterId.value) {
      await worldNovelApi.updateChapter(novelId.value, editingChapterId.value, chapterForm.value)
    } else {
      const res = await worldNovelApi.addChapter(novelId.value, chapterForm.value)
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
.novel-detail { max-width: 900px; margin: 0 auto; padding-bottom: 56px; }
.back { color: var(--accent-color); text-decoration: none; font-size: .85rem; }
.state { padding: 48px 0; text-align: center; color: var(--text-secondary); }
.err { margin: 0; color: var(--danger); font-size: .82rem; }

.novel-head { display: flex; gap: 24px; padding: 24px 0; border-bottom: 1px solid var(--border-line); margin-top: 12px; }
.head-cover { width: 160px; aspect-ratio: 3 / 4; flex-shrink: 0; border-radius: 12px; overflow: hidden; background: linear-gradient(160deg, var(--accent-soft), var(--bg-sub)); display: grid; place-items: center; }
.head-cover img { width: 100%; height: 100%; object-fit: cover; }
.head-cover span { font-size: 3rem; font-weight: 800; color: var(--accent-color); }
.head-main { min-width: 0; flex: 1; }
.head-title { margin: 0 0 8px; font-size: clamp(1.4rem, 3vw, 1.9rem); overflow-wrap: anywhere; }
.head-meta { margin: 0 0 10px; font-size: .8rem; color: var(--text-secondary); display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
.draft { padding: 1px 8px; border-radius: 6px; background: var(--bg-sub); font-size: .72rem; }
.world-chip { padding: 2px 10px; border-radius: 999px; border: 1px solid var(--border-line); color: var(--accent-color); text-decoration: none; font-size: .72rem; }
.world-chip:hover { border-color: var(--accent-color); }
.head-desc { margin: 0 0 16px; font-size: .9rem; line-height: 1.8; color: var(--text-secondary); white-space: pre-wrap; }
.head-actions { display: flex; flex-wrap: wrap; gap: 8px; }

.btn { padding: 8px 16px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); cursor: pointer; font-size: .84rem; }
.btn.primary { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); }
.btn.danger { color: var(--danger); border-color: var(--danger); }
.btn:disabled { opacity: .45; cursor: default; }
.mini { padding: 4px 10px; border: 1px solid var(--border-line); border-radius: 6px; background: var(--paper-card); color: var(--text-secondary); cursor: pointer; font-size: .74rem; }
.mini.danger { color: var(--danger); }

.toc-head { display: flex; justify-content: space-between; align-items: center; margin: 26px 0 14px; }
.toc-head h2 { margin: 0; font-size: 1.1rem; }
.toc-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 6px; }
.toc-row { display: flex; gap: 8px; align-items: center; }
.toc-item { flex: 1; display: flex; align-items: center; gap: 12px; padding: 11px 14px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); cursor: pointer; text-align: left; font-size: .88rem; }
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
.chapter-body :deep(img) { max-width: 100%; border-radius: 10px; display: block; margin: 18px auto; }
.chapter-nav { display: flex; justify-content: space-between; gap: 12px; margin-top: 40px; padding-top: 20px; border-top: 1px dashed var(--border-line); }

.mask { position: fixed; inset: 0; z-index: 120; background: rgba(0,0,0,.45); display: flex; align-items: center; justify-content: center; padding: 20px; }
.modal { width: min(560px, 100%); max-height: calc(100dvh - 40px); box-sizing: border-box; overflow-y: auto; display: grid; gap: 14px; padding: 24px; border-radius: var(--radius-sm); border: 1px solid var(--surface-edge); background: var(--paper-card); }
.modal.wide { width: min(860px, 100%); }
.modal-title { margin: 0; font-size: 1.15rem; }
.field { display: grid; gap: 6px; font-size: .82rem; color: var(--text-secondary); }
.field input, .field select, .field textarea { padding: 9px 12px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--bg-main); color: var(--text-primary); font: inherit; font-size: .88rem; }
.field-row { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.chapter-input { min-height: min(380px,45dvh); line-height: 1.8; resize: vertical; font-family: ui-monospace, monospace; }
.hint { margin: 0; font-size: .76rem; color: var(--text-secondary); }
.modal-actions { display: flex; justify-content: flex-end; gap: 10px; }
@media (max-width: 640px) { .novel-head { flex-direction: column; } .head-cover { width: 120px; } }
.modal-actions { position: sticky; bottom: -24px; padding: 16px 0; background: var(--paper-card); border-top: 1px solid var(--border-line); }
.modal .field input,.modal .field textarea,.modal .field select { min-width: 0; width: 100%; box-sizing: border-box; }
@media(max-width:640px) { .mask { padding: 12px; }.modal { padding: 16px; max-height: calc(100dvh - 24px); }.modal-actions { bottom: -16px; }.field-row { grid-template-columns: 1fr; } }
</style>
