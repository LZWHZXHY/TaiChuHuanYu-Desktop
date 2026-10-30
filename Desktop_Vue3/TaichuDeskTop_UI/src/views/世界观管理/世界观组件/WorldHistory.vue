<!-- WorldHistory.vue：世界观修改记录。谁在什么时候新建 / 修改 / 删除了哪个条目、改了什么；可以撤销某次修改；
     创建者还能一键撤销某位协作者一段时间内的全部修改（防止恶意破坏）。 -->
<template>
  <Teleport to="body">
    <div class="hist-mask" @click.self="close">
      <section role="dialog" aria-modal="true" aria-label="修改记录" class="hist-dialog workbench-skin editor-surface community-surface tone-world" @keydown.esc="close">
        <header class="hist-head">
          <div>
            <h2>修改记录</h2>
            <p v-if="cardFilter" class="hist-sub">只看条目《{{ cardTitleOf(cardFilter) }}》 <button type="button" class="link" @click="cardFilter = ''">看全部</button></p>
            <p v-else class="hist-sub">每次新建、修改、删除条目都会记下是谁、什么时候、改了什么，改错了可以撤销。</p>
          </div>
          <button class="close-btn" aria-label="关闭" @click="close">×</button>
        </header>

        <div class="hist-filters">
          <label>成员
            <select v-model="userFilter">
              <option value="">全部</option>
              <option v-for="m in memberOptions" :key="m.userId" :value="m.userId">{{ m.username }}{{ m.role === 'owner' ? '（创建者）' : '' }}</option>
            </select>
          </label>
          <label>操作
            <select v-model="actionFilter">
              <option value="">全部</option>
              <option value="create">新建</option>
              <option value="update">修改</option>
              <option value="delete">删除</option>
              <option value="restore">恢复 / 撤销</option>
            </select>
          </label>
        </div>

        <!-- 创建者：一键撤销某位协作者的修改 -->
        <div v-if="isOwner && userFilter && userFilter !== project.ownerId" class="revert-box">
          <span>撤销 <b>{{ memberName(userFilter) }}</b> 在</span>
          <select v-model.number="revertHours" aria-label="撤销的时间范围">
            <option :value="1">最近 1 小时</option>
            <option :value="24">最近 24 小时</option>
            <option :value="168">最近 7 天</option>
            <option :value="0">全部时间</option>
          </select>
          <span>的全部修改</span>
          <button type="button" class="danger" :disabled="reverting" @click="revertUser">{{ reverting ? '撤销中…' : '一键撤销' }}</button>
        </div>

        <p v-if="loadError" class="error" role="alert">{{ loadError }} <button type="button" @click="reload">重试</button></p>
        <p v-else-if="!items.length && loading" class="muted">正在加载…</p>
        <p v-else-if="!items.length" class="muted empty">还没有修改记录。从现在开始，每次修改条目都会记在这里。</p>

        <ol class="hist-list">
          <li v-for="r in items" :key="r.id" class="hist-item" :class="`act-${r.action}`">
            <span class="avatar"><img v-if="r.avatar" :src="r.avatar" alt="" /><span v-else>{{ (r.username || '?').slice(0, 1) }}</span></span>
            <div class="hist-main">
              <p class="hist-line">
                <b>{{ r.username || (r.action === 'baseline' ? '记录起点' : '已注销用户') }}</b>
                <span class="verb">{{ verb(r.action) }}</span>
                <button v-if="r.cardExists" type="button" class="link title" @click="openCard(r.cardId)">《{{ r.title || '无标题' }}》</button>
                <span v-else class="title gone">《{{ r.title || '无标题' }}》</span>
                <span v-if="r.note" class="note">· {{ r.note }}</span>
              </p>
              <p class="hist-meta">
                <time :title="fullTime(r.createdAt)">{{ relTime(r.createdAt) }}</time>
                <button type="button" class="link" @click="toggleDiff(r)">{{ openDiff[r.id] ? '收起改动' : '查看改动' }}</button>
                <button v-if="!cardFilter" type="button" class="link" @click="cardFilter = r.cardId">这个条目的全部记录</button>
                <button v-if="canEdit && r.action !== 'baseline'" type="button" class="link warn" :disabled="busyId === r.id" @click="undo(r)">{{ busyId === r.id ? '撤销中…' : undoLabel(r.action) }}</button>
              </p>
              <div v-if="openDiff[r.id]" class="hist-diff">
                <p v-if="diffLoading[r.id]" class="muted">正在对比…</p>
                <template v-else-if="diffs[r.id]">
                  <p v-if="!diffs[r.id]!.length" class="muted">内容没有变化（可能只是重新保存）。</p>
                  <dl v-else>
                    <template v-for="(d, i) in diffs[r.id]" :key="i">
                      <dt>{{ d.label }}</dt>
                      <dd>
                        <span v-if="d.before !== null" class="before">{{ d.before || '（空）' }}</span>
                        <span v-if="d.before !== null && d.after !== null" class="arrow">→</span>
                        <span v-if="d.after !== null" class="after">{{ d.after || '（空）' }}</span>
                      </dd>
                    </template>
                  </dl>
                </template>
              </div>
            </div>
          </li>
        </ol>
        <button v-if="hasMore" type="button" class="more" :disabled="loading" @click="loadMore">{{ loading ? '加载中…' : '加载更早的记录' }}</button>
      </section>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { worldApi } from '@/api/worldApi'
import type { WorldProject, WorldRevision, WorldCardSnapshot, WorldMember, RevisionAction } from '@/api/types/world'
import { getCardTypeLabel } from '@/utils/worldPresentation'
import { getErrorMessage } from '@/utils/apiError'

const props = defineProps<{ project: WorldProject; isOwner: boolean; canEdit: boolean; cardId?: string | null; userId?: string | null; cardTitles: Record<string, string> }>()
const emit = defineEmits<{ close: []; changed: [cardId: string]; 'open-card': [cardId: string] }>()

const cardFilter = ref(props.cardId || '')
// 从「某人修改了条目」的通知点进来时，直接只看这个人的修改
const userFilter = ref(props.userId || '')
const actionFilter = ref('')
const items = ref<WorldRevision[]>([])
const hasMore = ref(false)
const loading = ref(false)
const loadError = ref('')
const busyId = ref(0)
const reverting = ref(false)
const revertHours = ref(24)
const members = ref<WorldMember[]>([])
const openDiff = reactive<Record<number, boolean>>({})
const diffLoading = reactive<Record<number, boolean>>({})
const diffs = reactive<Record<number, DiffRow[] | undefined>>({})
let version = 0

const memberOptions = computed(() => members.value)
const memberName = (id: string) => members.value.find(m => m.userId === id)?.username ?? '这位成员'
const cardTitleOf = (id: string) => props.cardTitles[id] ?? items.value.find(r => r.cardId === id)?.title ?? '条目'

function close() { emit('close') }
function openCard(id: string) { emit('open-card', id); close() }

const VERBS: Record<RevisionAction, string> = { create: '新建了', update: '修改了', delete: '删除了', restore: '恢复了', baseline: '（开启记录前的内容）' }
const verb = (a: RevisionAction) => VERBS[a] ?? '修改了'
const undoLabel = (a: RevisionAction) => a === 'create' ? '撤销（删除这个条目）' : a === 'delete' ? '恢复这个条目' : '撤销这次修改'

function relTime(v: string) {
  const d = new Date(v.endsWith('Z') || v.includes('+') ? v : v + 'Z')
  const diff = Date.now() - d.getTime()
  if (diff < 60000) return '刚刚'
  if (diff < 3600000) return `${Math.floor(diff / 60000)} 分钟前`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)} 小时前`
  if (diff < 7 * 86400000) return `${Math.floor(diff / 86400000)} 天前`
  return d.toLocaleDateString('zh-CN')
}
const fullTime = (v: string) => new Date(v.endsWith('Z') || v.includes('+') ? v : v + 'Z').toLocaleString('zh-CN')

async function load(append = false) {
  const my = ++version
  loading.value = true
  loadError.value = ''
  try {
    const before = append ? items.value[items.value.length - 1]?.id : undefined
    const res = await worldApi.getHistory(props.project.id, {
      cardId: cardFilter.value || undefined, userId: userFilter.value || undefined, action: actionFilter.value || undefined, before, take: 30,
    })
    if (my !== version) return
    items.value = append ? [...items.value, ...res.items] : res.items
    hasMore.value = res.hasMore
  } catch (error) {
    if (my === version) loadError.value = getErrorMessage(error, '修改记录加载失败')
  } finally {
    if (my === version) loading.value = false
  }
}
const reload = () => load(false)
const loadMore = () => load(true)
watch([cardFilter, userFilter, actionFilter], reload)

// ---------- 对比改动 ----------
interface DiffRow { label: string; before: string | null; after: string | null }
const parseList = (raw: string | null | undefined): unknown[] => { try { const v = JSON.parse(raw || '[]'); return Array.isArray(v) ? v : [] } catch { return [] } }
const strList = (raw: string | null | undefined) => parseList(raw).map(x => typeof x === 'string' ? x : JSON.stringify(x)).join('、')
const cleanText = (t: string | null | undefined) => (t ?? '').replace(/\{CARD:([^}]+)\}/g, (_, id) => `《${props.cardTitles[id] ?? '条目'}》`).trim()
const clip = (t: string, n = 300) => t.length > n ? t.slice(0, n) + '…' : t
function attrMap(raw: string | null | undefined) {
  const map = new Map<string, string>()
  for (const a of parseList(raw)) if (a && typeof a === 'object') {
    const o = a as Record<string, unknown>
    const key = String(o.key ?? o.Key ?? '').trim()
    if (key) map.set(key, String(o.value ?? o.Value ?? ''))
  }
  return map
}
function timeline(raw: string | null | undefined) {
  return parseList(raw).map(e => { const o = (e ?? {}) as Record<string, unknown>; return [o.era, o.date, o.title].filter(Boolean).join(' ') }).filter(Boolean)
}
const relNames = (rels: { cardId: string; relationType: string }[] | undefined) =>
  (rels ?? []).map(r => `${props.cardTitles[r.cardId] ?? '已删除的条目'}（${r.relationType || '关联'}）`)

function buildDiff(before: WorldCardSnapshot | null, after: WorldCardSnapshot | null): DiffRow[] {
  const rows: DiffRow[] = []
  const add = (label: string, b: string, a: string) => { if (b !== a) rows.push({ label, before: before ? clip(b) : null, after: after ? clip(a) : null }) }
  const b = before, a = after
  add('标题', b?.title ?? '', a?.title ?? '')
  add('类型', b ? `${getCardTypeLabel(b.type)}${b.subType ? ' · ' + b.subType : ''}` : '', a ? `${getCardTypeLabel(a.type)}${a.subType ? ' · ' + a.subType : ''}` : '')
  add('简介', cleanText(b?.description), cleanText(a?.description))
  add('别名', strList(b?.aliases), strList(a?.aliases))
  add('标签', strList(b?.tags), strList(a?.tags))
  // 属性逐项对比
  const ba = attrMap(b?.attributes), aa = attrMap(a?.attributes)
  for (const key of new Set([...ba.keys(), ...aa.keys()])) add(`属性 · ${key}`, ba.get(key) ?? '', aa.get(key) ?? '')
  add('时间线', timeline(b?.timelineEvents).join('；'), timeline(a?.timelineEvents).join('；'))
  // 关系：列出增加 / 减少的
  const bo = relNames(b?.out), ao = relNames(a?.out)
  const removed = bo.filter(x => !ao.includes(x)), added = ao.filter(x => !bo.includes(x))
  if (before && after && (removed.length || added.length))
    rows.push({ label: '关联', before: removed.length ? '去掉：' + removed.join('、') : '', after: added.length ? '新增：' + added.join('、') : '' })
  else if (!before || !after) add('关联', bo.join('、'), ao.join('、'))
  if ((b?.coverImage ?? '') !== (a?.coverImage ?? '')) rows.push({ label: '封面', before: before ? (b?.coverImage ? '有封面' : '无') : null, after: after ? (a?.coverImage ? '换了新封面' : '去掉了封面') : null })
  const bg = parseList(b?.galleryImages).length, ag = parseList(a?.galleryImages).length
  if ((b?.galleryImages ?? '[]') !== (a?.galleryImages ?? '[]')) rows.push({ label: '图集', before: before ? `${bg} 张` : null, after: after ? `${ag} 张` : null })
  if ((b?.contentBlocks ?? '[]') !== (a?.contentBlocks ?? '[]')) rows.push({ label: '内嵌条目块', before: before ? `${parseList(b?.contentBlocks).length} 个` : null, after: after ? `${parseList(a?.contentBlocks).length} 个` : null })
  if ((b?.content ?? '{}') !== (a?.content ?? '{}') && before && after) rows.push({ label: '其他字段', before: '', after: '有改动' })
  // 新建 / 删除时把空字段去掉，只列有内容的
  return rows.filter(r => before && after ? true : (r.before || r.after))
}

async function toggleDiff(r: WorldRevision) {
  openDiff[r.id] = !openDiff[r.id]
  if (!openDiff[r.id] || diffs[r.id]) return
  diffLoading[r.id] = true
  try {
    const detail = await worldApi.getRevision(props.project.id, r.id)
    diffs[r.id] = buildDiff(detail.before, detail.after)
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '读取改动失败'))
    openDiff[r.id] = false
  } finally {
    diffLoading[r.id] = false
  }
}

// ---------- 撤销 ----------
async function undo(r: WorldRevision) {
  const tip = r.action === 'create' ? `会删除条目《${r.title}》（删除后还能在这里恢复）。`
    : r.action === 'delete' ? `会把条目《${r.title}》按删除前的样子恢复回来。`
    : `会把条目《${r.title}》恢复成这次修改之前的样子。这之后别人对它的修改也会被一起覆盖（会再记一笔，可以再撤销回来）。`
  try { await ElMessageBox.confirm(tip, '撤销修改', { confirmButtonText: '撤销', cancelButtonText: '取消', type: 'warning' }) } catch { return }
  busyId.value = r.id
  try {
    const res = await worldApi.undoRevision(props.project.id, r.id)
    ElMessage.success(res.message || '已撤销')
    emit('changed', r.cardId)
    await reload()
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '撤销失败，请重试'))
  } finally {
    busyId.value = 0
  }
}

async function revertUser() {
  const name = memberName(userFilter.value)
  const range = revertHours.value ? `最近 ${revertHours.value >= 24 ? revertHours.value / 24 + ' 天' : revertHours.value + ' 小时'}` : '全部时间'
  try {
    await ElMessageBox.confirm(`会把 ${name} 在${range}里碰过的每个条目，恢复成 Ta 第一次修改之前的样子；Ta 新建的条目会被删除。之后其他人对这些条目的修改也会被覆盖。`,
      `一键撤销 ${name} 的修改`, { confirmButtonText: '一键撤销', cancelButtonText: '取消', type: 'warning' })
  } catch { return }
  reverting.value = true
  try {
    const res = await worldApi.revertUser(props.project.id, userFilter.value, revertHours.value)
    ElMessage.success(res.message)
    emit('changed', '')
    await reload()
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '撤销失败，请重试'))
  } finally {
    reverting.value = false
  }
}

onMounted(async () => {
  void reload()
  try {
    const m = await worldApi.getMembers(props.project.id)
    members.value = [m.owner, ...m.items]
  } catch { /* 成员列表读不到也不影响看记录 */ }
})
</script>

<style scoped>
.hist-mask { position: fixed; inset: 0; z-index: 2000; background: #0006; display: grid; place-items: center; padding: 16px; }
.hist-dialog { box-sizing: border-box; width: min(720px, 100%); max-height: 88vh; overflow: auto; padding: 20px 24px 24px; background: var(--paper-card); color: var(--text-primary); border: 1px solid var(--border-line); border-radius: 0; }
.hist-head { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; padding-bottom: 12px; border-bottom: 1px solid var(--border-line); }
.hist-head h2 { margin: 0 0 4px; font-size: 18px; }
.hist-sub { margin: 0; font-size: 13px; color: var(--text-secondary); line-height: 1.6; }
.close-btn { border: 0; background: none; color: var(--text-secondary); font-size: 22px; cursor: pointer; padding: 2px 8px; }
button, select { font: inherit; }
select { padding: 6px 10px; border: 1px solid var(--border-line); border-radius: 0; background: var(--bg-main); color: var(--text-primary); }
.hist-filters { display: flex; flex-wrap: wrap; gap: 16px; margin: 14px 0; font-size: 13px; color: var(--text-secondary); }
.hist-filters label { display: inline-flex; align-items: center; gap: 8px; }
.revert-box { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; margin-bottom: 14px; padding: 12px; border: 1px solid var(--danger); font-size: 13px; }
.danger { padding: 6px 14px; border: 1px solid var(--danger); border-radius: 0; background: var(--danger); color: #fff; cursor: pointer; }
.danger:disabled { opacity: .6; cursor: default; }
.link { border: 0; padding: 0; background: none; color: var(--accent-color); cursor: pointer; font-size: inherit; }
.link:hover { text-decoration: underline; text-underline-offset: 3px; }
.link.warn { color: var(--danger); }
.link:disabled { opacity: .6; cursor: default; }
.muted { color: var(--text-secondary); font-size: 13px; }
.empty { padding: 24px 0; text-align: center; }
.error { color: var(--danger); font-size: 13px; }
.hist-list { list-style: none; margin: 0; padding: 0; }
.hist-item { display: flex; gap: 12px; padding: 12px 4px; border-bottom: 1px solid var(--border-line); }
.avatar { flex-shrink: 0; width: 32px; height: 32px; display: grid; place-items: center; overflow: hidden; border: 1px solid var(--border-line); background: var(--bg-sub); color: var(--text-secondary); font-size: 14px; }
.avatar img { width: 100%; height: 100%; object-fit: cover; }
.hist-main { flex: 1; min-width: 0; }
.hist-line { margin: 0; font-size: 14px; line-height: 1.6; overflow-wrap: anywhere; }
.verb { margin: 0 4px; color: var(--text-secondary); }
.act-delete .verb { color: var(--danger); }
.act-create .verb { color: var(--accent-color); }
.title.gone { color: var(--text-secondary); text-decoration: line-through; }
.note { color: var(--text-secondary); font-size: 12px; }
.hist-meta { display: flex; flex-wrap: wrap; gap: 14px; margin: 4px 0 0; font-size: 12px; color: var(--text-secondary); }
.hist-diff { margin-top: 10px; padding: 10px 12px; background: var(--bg-sub); border: 1px solid var(--border-line); font-size: 13px; }
.hist-diff dl { margin: 0; display: grid; grid-template-columns: max-content 1fr; gap: 6px 14px; }
.hist-diff dt { color: var(--text-secondary); }
.hist-diff dd { margin: 0; overflow-wrap: anywhere; white-space: pre-wrap; }
.before { color: var(--danger); text-decoration: line-through; text-decoration-thickness: 1px; opacity: .85; }
.after { color: var(--text-primary); }
.arrow { margin: 0 6px; color: var(--text-secondary); }
.more { display: block; margin: 14px auto 0; padding: 8px 18px; border: 1px solid var(--border-line); border-radius: 0; background: var(--bg-main); color: var(--text-primary); cursor: pointer; }
@media (max-width: 560px) { .hist-diff dl { grid-template-columns: 1fr; } }
</style>
