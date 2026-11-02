<!-- 小说目录：部 → 卷 / 番外 → 章（fix175）。
     没分组的章排在最前面（以前写的章都在这里，和以前一样）；分组可以折叠。
     作者在这里新建部 / 卷 / 番外、改名、上下移、删除，把章挪进某个分组、在分组里上下移。 -->
<template>
  <div class="ntoc">
    <div class="ntoc-head">
      <h2>目录 <small v-if="chapters.length">{{ chapters.length }} 章</small></h2>
      <div v-if="isOwner" class="ntoc-tools">
        <button type="button" class="btn primary" @click="emit('new-chapter', null)">＋ 新增章节</button>
        <button type="button" class="btn" @click="openSection('part', null)">＋ 部</button>
        <button type="button" class="btn" @click="openSection('volume', null)">＋ 卷</button>
        <button type="button" class="btn" @click="openSection('extra', null)">＋ 番外</button>
      </div>
    </div>
    <p v-if="isOwner && !sections.length && chapters.length > 3" class="ntoc-tip">章节多了可以分「部」「卷」，番外、外传、后记可以单独放一组。新建分组后，在每章右边的「移到」里选。</p>
    <p v-if="error" class="ntoc-err" role="alert">{{ error }}</p>

    <!-- 没分组的章 -->
    <section v-if="loose.length" class="ntoc-group loose">
      <h3 v-if="sections.length" class="ntoc-loose-title">正文</h3>
      <ol class="ntoc-list">
        <li v-for="c in loose" :key="c.id" class="ntoc-row">
          <ChapterRow :c="c" />
        </li>
      </ol>
    </section>

    <!-- 部 / 最外层的卷、番外 -->
    <section v-for="top in tops" :key="top.id" class="ntoc-group" :class="'k-' + top.kind">
      <SectionHeader :s="top" :index="siblingsOf(top).indexOf(top)" :count="siblingsOf(top).length" />
      <template v-if="!collapsed.has(top.id)">
        <ol v-if="chaptersIn(top.id).length" class="ntoc-list">
          <li v-for="c in chaptersIn(top.id)" :key="c.id" class="ntoc-row"><ChapterRow :c="c" /></li>
        </ol>
        <div v-for="child in childrenOf(top.id)" :key="child.id" class="ntoc-sub" :class="'k-' + child.kind">
          <SectionHeader :s="child" :index="childrenOf(top.id).indexOf(child)" :count="childrenOf(top.id).length" />
          <ol v-if="!collapsed.has(child.id) && chaptersIn(child.id).length" class="ntoc-list">
            <li v-for="c in chaptersIn(child.id)" :key="c.id" class="ntoc-row"><ChapterRow :c="c" /></li>
          </ol>
          <p v-else-if="!collapsed.has(child.id)" class="ntoc-empty">还没有章{{ isOwner ? '，点上面的「＋ 本组新章」' : '' }}</p>
        </div>
        <p v-if="!chaptersIn(top.id).length && !childrenOf(top.id).length" class="ntoc-empty">还没有内容{{ isOwner ? (top.kind === 'part' ? '，可以在这一部里加卷或番外，也可以直接加章' : '，点上面的「＋ 本组新章」') : '' }}</p>
      </template>
    </section>

    <p v-if="!chapters.length && !sections.length" class="ntoc-empty big">还没有章节{{ isOwner ? '，点右上角「新增章节」开始写' : '' }}。</p>

    <!-- 新建 / 改名分组 -->
    <div v-if="sectionForm" class="mask" @click.self="sectionForm = null">
      <div class="modal workbench-skin" role="dialog" aria-modal="true" :aria-label="sectionForm.id ? '修改分组' : '新建分组'">
        <h2 class="modal-title studio-heading">{{ sectionForm.id ? '修改' : '新建' }}{{ KIND_LABEL[sectionForm.kind] }}</h2>
        <label class="field"><span>名字 *</span><input v-model="sectionForm.title" maxlength="60" :placeholder="KIND_PLACEHOLDER[sectionForm.kind]" /></label>
        <label v-if="!sectionForm.id && sectionForm.kind !== 'part' && parts.length" class="field"><span>放在哪</span>
          <select v-model="sectionForm.parentId">
            <option :value="null">最外层（不属于哪一部）</option>
            <option v-for="p in parts" :key="p.id" :value="p.id">{{ p.title }}</option>
          </select>
        </label>
        <label class="field"><span>简介（可不填）</span><textarea v-model="sectionForm.note" rows="2" maxlength="500" placeholder="这一部分讲了什么"></textarea></label>
        <p v-if="sectionForm.kind === 'extra'" class="hint">番外里的章不算「第几章」。名字可以改成外传、后记、设定集……</p>
        <div class="modal-actions editor-footer">
          <button type="button" class="btn" @click="sectionForm = null">取消</button>
          <button type="button" class="btn primary" :disabled="busy || !sectionForm.title.trim()" @click="saveSection">{{ busy ? '保存中…' : '保存' }}</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, defineComponent, h, ref } from 'vue'
import { worldNovelApi, type NovelChapterBrief, type NovelSection, type SectionKind } from '@/api/worldNovel'

const props = defineProps<{ novelId: string; chapters: NovelChapterBrief[]; sections: NovelSection[]; isOwner: boolean }>()
const emit = defineEmits<{ open: [id: string]; 'new-chapter': [sectionId: string | null]; 'remove-chapter': [id: string]; changed: [] }>()

const KIND_LABEL: Record<SectionKind, string> = { part: '部', volume: '卷', extra: '番外' }
const KIND_PLACEHOLDER: Record<SectionKind, string> = { part: '第一部 · 风起', volume: '第一卷 · 初入江湖', extra: '番外 / 外传 / 后记' }
const busy = ref(false)
const error = ref('')
const collapsed = ref(new Set<string>())

const ids = computed(() => new Set(props.sections.map(s => s.id)))
const byOrder = (a: NovelSection, b: NovelSection) => a.sortOrder - b.sortOrder
const tops = computed(() => props.sections.filter(s => !s.parentId || !ids.value.has(s.parentId)).sort(byOrder))
const parts = computed(() => tops.value.filter(s => s.kind === 'part'))
const childrenOf = (id: string) => props.sections.filter(s => s.parentId === id).sort(byOrder)
const siblingsOf = (s: NovelSection) => (s.parentId && ids.value.has(s.parentId) ? childrenOf(s.parentId) : tops.value)
const sectionOf = (c: NovelChapterBrief) => (c.sectionId && ids.value.has(c.sectionId) ? c.sectionId : null)
const loose = computed(() => props.chapters.filter(c => !sectionOf(c)))
const chaptersIn = (id: string) => props.chapters.filter(c => sectionOf(c) === id)
const chapterLabel = (c: NovelChapterBrief) => (c.displayNo != null ? `第 ${c.displayNo} 章` : '番外')

/** 「移到」下拉的选项：不分组 + 每个分组（部里的卷缩进显示） */
const moveTargets = computed(() => {
  const out: { id: string | null; label: string }[] = [{ id: null, label: '不分组（正文最前面）' }]
  for (const t of tops.value) {
    out.push({ id: t.id, label: `${KIND_LABEL[t.kind]}：${t.title}` })
    for (const c of childrenOf(t.id)) out.push({ id: c.id, label: `　└ ${KIND_LABEL[c.kind]}：${c.title}` })
  }
  return out
})

async function run(task: () => Promise<unknown>, fallback: string) {
  if (busy.value) return
  busy.value = true
  error.value = ''
  try { await task(); emit('changed') }
  catch (e: any) { error.value = e?.response?.data?.message || e?.friendlyMessage || fallback }
  finally { busy.value = false }
}

/** 1 → 一、12 → 十二（99 以内） */
function cn(n: number) {
  const d = '零一二三四五六七八九'
  if (n < 10) return d[n]
  if (n < 20) return `十${n % 10 ? d[n % 10] : ''}`
  if (n < 100) return `${d[Math.floor(n / 10)]}十${n % 10 ? d[n % 10] : ''}`
  return String(n)
}

// ===== 分组表单 =====
const sectionForm = ref<{ id?: string; kind: SectionKind; title: string; note: string; parentId: string | null } | null>(null)
function openSection(kind: SectionKind, parentId: string | null) {
  // 默认名字：第 N 部 / 第 N 卷（同一层里第几个），番外就叫番外
  const same = props.sections.filter(s => s.kind === kind && (kind === 'part' || (s.parentId ?? null) === parentId)).length + 1
  const title = kind === 'part' ? `第${cn(same)}部` : kind === 'volume' ? `第${cn(same)}卷` : '番外'
  sectionForm.value = { kind, title, note: '', parentId }
}
function editSection(s: NovelSection) { sectionForm.value = { id: s.id, kind: s.kind, title: s.title, note: s.note ?? '', parentId: s.parentId ?? null } }
async function saveSection() {
  const f = sectionForm.value
  if (!f || !f.title.trim()) return
  await run(async () => {
    if (f.id) await worldNovelApi.updateSection(props.novelId, f.id, { title: f.title.trim(), note: f.note.trim() || null })
    else await worldNovelApi.createSection(props.novelId, { kind: f.kind, title: f.title.trim(), note: f.note.trim() || null, parentId: f.parentId })
    sectionForm.value = null
  }, '保存失败')
}
function removeSection(s: NovelSection) {
  if (!confirm(`删除「${s.title}」？里面的章不会删，会回到「正文」最前面。`)) return
  void run(() => worldNovelApi.removeSection(props.novelId, s.id), '删除失败')
}
const toggle = (id: string) => { const n = new Set(collapsed.value); n.has(id) ? n.delete(id) : n.add(id); collapsed.value = n }

// ===== 小组件：分组标题、章节行（写在这里，方便直接用上面的函数） =====
const SectionHeader = defineComponent({
  props: { s: { type: Object as () => NovelSection, required: true }, index: { type: Number, required: true }, count: { type: Number, required: true } },
  setup(p) {
    return () => {
      const s = p.s
      const count = chaptersIn(s.id).length + childrenOf(s.id).reduce((n, c) => n + chaptersIn(c.id).length, 0)
      const open = !collapsed.value.has(s.id)
      return h('header', { class: 'ntoc-sec' }, [
        h('button', { type: 'button', class: 'ntoc-sec-toggle', 'aria-expanded': String(open), onClick: () => toggle(s.id) }, [
          h('span', { class: 'ntoc-caret', 'aria-hidden': 'true' }, open ? '▾' : '▸'),
          h('span', { class: 'ntoc-kind' }, KIND_LABEL[s.kind]),
          h('span', { class: 'ntoc-sec-title' }, s.title),
          h('small', { class: 'ntoc-sec-count' }, `${count} 章`),
        ]),
        s.note ? h('p', { class: 'ntoc-sec-note' }, s.note) : null,
        props.isOwner ? h('div', { class: 'ntoc-sec-tools' }, [
          h('button', { type: 'button', class: 'mini', onClick: () => emit('new-chapter', s.id) }, '＋ 本组新章'),
          s.kind === 'part' ? h('button', { type: 'button', class: 'mini', onClick: () => openSection('volume', s.id) }, '＋ 卷') : null,
          s.kind === 'part' ? h('button', { type: 'button', class: 'mini', onClick: () => openSection('extra', s.id) }, '＋ 番外') : null,
          h('button', { type: 'button', class: 'mini', onClick: () => editSection(s) }, '改名'),
          h('button', { type: 'button', class: 'mini', disabled: p.index <= 0 || busy.value, title: '上移', 'aria-label': '上移', onClick: () => run(() => worldNovelApi.moveSection(props.novelId, s.id, -1), '移动失败') }, '↑'),
          h('button', { type: 'button', class: 'mini', disabled: p.index >= p.count - 1 || busy.value, title: '下移', 'aria-label': '下移', onClick: () => run(() => worldNovelApi.moveSection(props.novelId, s.id, 1), '移动失败') }, '↓'),
          h('button', { type: 'button', class: 'mini danger', onClick: () => removeSection(s) }, '删除'),
        ]) : null,
      ])
    }
  },
})

const ChapterRow = defineComponent({
  props: { c: { type: Object as () => NovelChapterBrief, required: true } },
  setup(p) {
    return () => {
      const c = p.c
      const group = sectionOf(c)
      const list = group ? chaptersIn(group) : loose.value
      const i = list.findIndex(x => x.id === c.id)
      return h('div', { class: 'ntoc-line' }, [
        h('button', { type: 'button', class: 'toc-item', onClick: () => emit('open', c.id) }, [
          h('span', { class: ['toc-num', c.displayNo == null ? 'extra' : ''] }, chapterLabel(c)),
          h('span', { class: 'toc-title' }, c.title),
          h('span', { class: 'toc-words' }, `${c.wordCount} 字`),
        ]),
        props.isOwner ? h('div', { class: 'ntoc-ch-tools' }, [
          h('select', {
            class: 'mini ntoc-move', 'aria-label': `把「${c.title}」移到`, value: group ?? '', disabled: busy.value,
            onChange: (e: Event) => { const v = (e.target as HTMLSelectElement).value || null; if (v !== group) void run(() => worldNovelApi.setChapterSection(props.novelId, c.id, v), '移动失败') },
          }, moveTargets.value.map(t => h('option', { value: t.id ?? '' }, t.id === group ? `在：${t.label.replace(/^　└ /, '')}` : `移到 ${t.label.replace(/^　└ /, '')}`))),
          h('button', { type: 'button', class: 'mini', disabled: i <= 0 || busy.value, title: '上移', 'aria-label': '上移', onClick: () => run(() => worldNovelApi.moveChapter(props.novelId, c.id, -1), '移动失败') }, '↑'),
          h('button', { type: 'button', class: 'mini', disabled: i >= list.length - 1 || busy.value, title: '下移', 'aria-label': '下移', onClick: () => run(() => worldNovelApi.moveChapter(props.novelId, c.id, 1), '移动失败') }, '↓'),
          h('button', { type: 'button', class: 'mini danger', onClick: () => emit('remove-chapter', c.id) }, '删除'),
        ]) : null,
      ])
    }
  },
})
</script>

<style scoped>
:deep(.btn) { padding: 8px 16px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); cursor: pointer; font-size: .84rem; }
:deep(.btn.primary) { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); }
:deep(.btn:disabled) { opacity: .45; cursor: default; }
:deep(.mini) { padding: 4px 10px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-secondary); cursor: pointer; font: inherit; font-size: .74rem; }
:deep(.mini:hover:not(:disabled)) { border-color: var(--accent-color); color: var(--accent-color); }
:deep(.mini:disabled) { opacity: .4; cursor: default; }
:deep(.mini.danger) { color: var(--danger); }
:deep(.toc-item) { display: flex; align-items: center; gap: 12px; padding: 11px 14px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); cursor: pointer; text-align: left; font: inherit; font-size: .88rem; }
:deep(.toc-item:hover) { border-color: var(--accent-color); }
:deep(.toc-num) { flex-shrink: 0; color: var(--text-secondary); font-variant-numeric: tabular-nums; font-size: .8rem; }
:deep(.toc-title) { flex: 1; min-width: 0; overflow-wrap: anywhere; }
:deep(.toc-words) { flex-shrink: 0; font-size: .72rem; color: var(--text-secondary); }
.mask { position: fixed; inset: 0; z-index: 120; background: rgba(0,0,0,.45); display: flex; align-items: center; justify-content: center; padding: 20px; }
.modal { width: min(520px, 100%); max-height: calc(100dvh - 40px); box-sizing: border-box; overflow-y: auto; display: grid; gap: 14px; padding: 24px; border: 1px solid var(--surface-edge, var(--border-line)); background: var(--paper-card); }
.modal-title { margin: 0; font-size: 1.15rem; }
.field { display: grid; gap: 6px; font-size: .82rem; color: var(--text-secondary); }
.field input, .field select, .field textarea { padding: 9px 12px; border: 1px solid var(--border-line); border-radius: 0; background: var(--bg-main); color: var(--text-primary); font: inherit; font-size: .88rem; width: 100%; box-sizing: border-box; }
.hint { margin: 0; font-size: .76rem; color: var(--text-secondary); }
.modal-actions { display: flex; justify-content: flex-end; gap: 10px; }
.ntoc-head { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 10px; margin: 26px 0 14px; }
.ntoc-head h2 { margin: 0; font-size: 1.1rem; }
.ntoc-head h2 small { margin-left: 6px; font-size: .75rem; font-weight: 400; color: var(--text-secondary); }
.ntoc-tools { display: flex; flex-wrap: wrap; gap: 6px; }
.ntoc-tip { margin: 0 0 12px; padding: 8px 12px; border-left: 3px solid var(--accent-color); background: var(--accent-soft); font-size: .8rem; color: var(--text-secondary); }
.ntoc-err { margin: 0 0 10px; color: var(--danger); font-size: .82rem; }
.ntoc-group { margin: 0 0 14px; }
.ntoc-group.k-part { padding: 10px 12px 12px; border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); background: color-mix(in srgb, var(--paper-card) 70%, transparent); }
.ntoc-group.k-volume, .ntoc-sub.k-volume { border-left: 3px solid var(--signal-mint, var(--accent-color)); padding-left: 10px; }
.ntoc-group.k-extra, .ntoc-sub.k-extra { border-left: 3px dashed var(--warning, #d97706); padding-left: 10px; }
.ntoc-sub { margin: 12px 0 0 6px; }
.ntoc-loose-title { margin: 0 0 8px; font-size: .9rem; color: var(--text-secondary); }
.ntoc-list { list-style: none; margin: 8px 0 0; padding: 0; display: grid; gap: 6px; }
.ntoc-empty { margin: 8px 0 0; font-size: .8rem; color: var(--text-secondary); }
.ntoc-empty.big { padding: 36px 0; text-align: center; }
:deep(.ntoc-sec) { display: grid; gap: 4px; }
:deep(.ntoc-sec-toggle) { display: flex; align-items: center; gap: 8px; width: 100%; padding: 4px 0; border: 0; background: none; color: var(--text-primary); font: inherit; text-align: left; cursor: pointer; }
:deep(.ntoc-sec-toggle:hover .ntoc-sec-title) { color: var(--accent-color); }
:deep(.ntoc-caret) { width: 14px; color: var(--text-secondary); }
:deep(.ntoc-kind) { padding: 0 6px; font: 600 11px/1.7 var(--font-mono, monospace); color: var(--inverse-text, #fff); background: var(--inverse-bg, #222); }
:deep(.k-extra > .ntoc-sec .ntoc-kind), :deep(.ntoc-sub.k-extra .ntoc-kind) { background: var(--warning, #d97706); color: #fff; }
:deep(.ntoc-sec-title) { font-weight: 700; font-size: 1rem; overflow-wrap: anywhere; }
:deep(.ntoc-sub .ntoc-sec-title) { font-size: .92rem; }
:deep(.ntoc-sec-count) { margin-left: auto; font-size: .72rem; color: var(--text-secondary); white-space: nowrap; }
:deep(.ntoc-sec-note) { margin: 0 0 0 22px; font-size: .8rem; color: var(--text-secondary); white-space: pre-wrap; }
:deep(.ntoc-sec-tools), :deep(.ntoc-ch-tools) { display: flex; flex-wrap: wrap; gap: 4px; }
:deep(.ntoc-sec-tools) { margin-left: 22px; }
:deep(.ntoc-line) { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
:deep(.ntoc-line .toc-item) { flex: 1 1 280px; }
:deep(.toc-num.extra) { color: var(--warning, #d97706); }
:deep(.ntoc-ch-tools) { flex-wrap: nowrap; }
:deep(.ntoc-move) { flex: 1 1 auto; min-width: 0; max-width: 180px; }
@media (max-width: 640px) { :deep(.ntoc-ch-tools) { width: 100%; } :deep(.ntoc-move) { max-width: none; } }
:deep(.toc-num) { width: auto; min-width: 54px; }
</style>
