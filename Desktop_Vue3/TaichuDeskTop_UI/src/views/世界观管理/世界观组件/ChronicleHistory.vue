<!-- ChronicleHistory.vue：编年史的修改记录。谁在什么时候新建 / 修改 / 删除了哪条时间线、哪件事、历法，改了哪些地方；
     改错了可以撤销某一次（修改 → 恢复旧内容；新建 → 删掉；删除 → 原样恢复，时间线连同上面的事件一起回来）。
     创建者和协作者能看，创建者和编写者能撤销（和条目的修改记录一样）。 -->
<template>
  <Teleport to="body">
    <div class="hist-mask" @click.self="close">
      <section role="dialog" aria-modal="true" aria-label="编年史修改记录" class="hist-dialog workbench-skin editor-surface community-surface tone-world" @keydown.esc="close">
        <header class="hist-head">
          <div>
            <h2>编年史 · 修改记录</h2>
            <p v-if="targetFilter" class="hist-sub">只看{{ TARGET_LABEL[targetFilter.type] }}《{{ targetFilter.title }}》 <button type="button" class="link" @click="targetFilter = null">看全部</button></p>
            <p v-else class="hist-sub">每次新建、修改、删除时间线、事件和历法都会记下是谁、什么时候、改了什么，改错了可以撤销。</p>
          </div>
          <button class="close-btn" aria-label="关闭" @click="close">×</button>
        </header>

        <div class="hist-filters">
          <label v-if="!targetFilter">内容
            <select v-model="typeFilter">
              <option value="">全部</option>
              <option value="timeline">时间线</option>
              <option value="event">事件</option>
              <option value="calendar">历法</option>
            </select>
          </label>
          <label>成员
            <select v-model="userFilter">
              <option value="">全部</option>
              <option v-for="m in members" :key="m.userId" :value="m.userId">{{ m.username }}{{ m.role === 'owner' ? '（创建者）' : '' }}</option>
            </select>
          </label>
          <label>操作
            <select v-model="actionFilter">
              <option value="">全部</option>
              <option value="create">新建</option>
              <option value="update">修改</option>
              <option value="delete">删除</option>
              <option value="restore">撤销 / 恢复</option>
            </select>
          </label>
        </div>

        <p v-if="loadError" class="error" role="alert">{{ loadError }} <button type="button" class="link" @click="reload">重试</button></p>
        <p v-else-if="!items.length && loading" class="muted">正在加载…</p>
        <p v-else-if="!items.length" class="muted empty">还没有修改记录。从现在开始，每次修改编年史都会记在这里。</p>

        <ol class="hist-list">
          <li v-for="r in items" :key="r.id" class="hist-item" :class="`act-${r.action}`">
            <span class="avatar"><img v-if="r.avatar" :src="r.avatar" alt="" /><span v-else>{{ (r.username || '?').slice(0, 1) }}</span></span>
            <div class="hist-main">
              <p class="hist-line">
                <b>{{ r.username || '已注销用户' }}</b>
                <span class="verb">{{ VERBS[r.action] ?? '修改了' }}</span>
                <span class="kind">{{ TARGET_LABEL[r.targetType] ?? '内容' }}</span>
                <template v-if="r.targetType !== 'calendar'">
                  <button v-if="r.exists" type="button" class="link title" :title="r.targetType === 'event' ? '在编年史里看这件事' : '打开这条时间线'" @click="openTarget(r)">《{{ shownTitle(r.title) }}》</button>
                  <span v-else class="title gone" title="已经删掉了">《{{ shownTitle(r.title) }}》</span>
                </template>
                <span v-if="r.fields.length" class="fields">· 改了{{ fieldLabels(r) }}</span>
                <span v-if="r.note" class="note">· {{ r.note }}</span>
              </p>
              <p class="hist-meta">
                <time :title="fullTime(r.createdAt)">{{ relTime(r.createdAt) }}</time>
                <button type="button" class="link" @click="toggleDiff(r)">{{ openDiff[r.id] ? '收起改动' : '查看改动' }}</button>
                <button v-if="!targetFilter && r.targetType !== 'calendar'" type="button" class="link" @click="targetFilter = { type: r.targetType, id: r.targetId, title: shownTitle(r.title) }">这{{ r.targetType === 'event' ? '件事' : '条线' }}的全部记录</button>
                <button v-if="!targetFilter && r.targetType === 'calendar'" type="button" class="link" @click="typeFilter = 'calendar'">历法的全部记录</button>
                <button v-if="canEdit" type="button" class="link warn" :disabled="busyId === r.id" @click="undo(r)">{{ busyId === r.id ? '撤销中…' : undoLabel(r) }}</button>
              </p>
              <div v-if="openDiff[r.id]" class="hist-diff">
                <p v-if="diffLoading[r.id]" class="muted">正在对比…</p>
                <template v-else-if="diffs[r.id]">
                  <p v-if="!diffs[r.id]!.length" class="muted">内容没有变化。</p>
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
import { onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { chronicleApi, type ChronicleRevision, type ChronicleTarget, type ChronicleAction } from '@/api/chronicle'
import { worldApi, type WorldMember } from '@/api/worldApi'
import { getErrorMessage } from '@/utils/apiError'
import {
  STATUS_META, KIND_META, formatDate, normalizeCalendar, plainTitle,
  type Calendar, type ChronicleEvent, type ChronicleTimeline, type EventKind, type TimelineStatus,
} from '@/utils/chronicle'

const props = defineProps<{
  projectId: string
  canEdit: boolean
  calendar: Calendar
  /** 现在的时间线和事件：用来把记录里的 Id 显示成名字 */
  timelines: ChronicleTimeline[]
  events: ChronicleEvent[]
  findCard: (id: string) => { title: string } | undefined
}>()
const emit = defineEmits<{ close: []; changed: []; 'select-event': [id: string]; 'open-line': [id: string] }>()

const typeFilter = ref<ChronicleTarget | ''>('')
const userFilter = ref('')
const actionFilter = ref<ChronicleAction | ''>('')
// 「这件事 / 这条线的全部记录」
const targetFilter = ref<{ type: ChronicleTarget; id: string; title: string } | null>(null)
const items = ref<ChronicleRevision[]>([])
const hasMore = ref(false)
const loading = ref(false)
const loadError = ref('')
const busyId = ref(0)
const members = ref<WorldMember[]>([])
const openDiff = reactive<Record<number, boolean>>({})
const diffLoading = reactive<Record<number, boolean>>({})
const diffs = reactive<Record<number, DiffRow[] | undefined>>({})
let version = 0

const VERBS: Record<ChronicleAction, string> = { create: '新建了', update: '修改了', delete: '删除了', restore: '撤销 / 恢复了' }
const TARGET_LABEL: Record<ChronicleTarget, string> = { timeline: '时间线', event: '事件', calendar: '历法' }

function close() { emit('close') }

const shownTitle = (title: string) => plainTitle(title || '', props.findCard) || '无标题'
const cardName = (id: unknown) => typeof id === 'string' && id ? props.findCard(id)?.title ?? '已删除的条目' : ''
const lineName = (id: unknown) => typeof id === 'string' && id ? props.timelines.find(l => l.id === id)?.name ?? '已删除的时间线' : ''
const eventName = (id: unknown) => {
  if (typeof id !== 'string' || !id) return ''
  const ev = props.events.find(e => e.id === id)
  return ev ? shownTitle(ev.title) : '已删除的事件'
}

function openTarget(r: ChronicleRevision) {
  if (r.targetType === 'event') emit('select-event', r.targetId)
  else if (r.targetType === 'timeline') emit('open-line', r.targetId)
  close()
}

function relTime(v: string) {
  const d = new Date(v)
  const diff = Date.now() - d.getTime()
  if (diff < 60000) return '刚刚'
  if (diff < 3600000) return `${Math.floor(diff / 60000)} 分钟前`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)} 小时前`
  if (diff < 7 * 86400000) return `${Math.floor(diff / 86400000)} 天前`
  return d.toLocaleDateString('zh-CN')
}
const fullTime = (v: string) => new Date(v).toLocaleString('zh-CN')

async function load(append = false) {
  const my = ++version
  loading.value = true
  loadError.value = ''
  try {
    const before = append ? items.value[items.value.length - 1]?.id : undefined
    const target = targetFilter.value
    const res = await chronicleApi.history(props.projectId, {
      targetType: target?.type ?? (typeFilter.value || undefined), targetId: target?.id,
      userId: userFilter.value || undefined, action: actionFilter.value || undefined, before, take: 30,
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
watch([typeFilter, userFilter, actionFilter, targetFilter], reload)

// ---------- 字段名 → 说人话 ----------
// 时间拆成好几个字段（纪元、年、月、日），列表里合成「时间」一项
const FIELD_LABEL: Record<string, string> = {
  name: '名称', description: '说明', color: '颜色', status: '类型', parentId: '从哪条线分出', forkEventId: '分叉点', mergeEventId: '汇合点',
  cardId: '关于的条目', sortOrder: '排列顺序', hidden: '对访客隐藏',
  timelineId: '所在时间线', kind: '种类', title: '名称', eraId: '时间', year: '时间', month: '时间', day: '时间',
  endEraId: '结束时间', endYear: '结束时间', endMonth: '结束时间', endDay: '结束时间', dateText: '时间说明',
  images: '图片', tags: '标签', cardIds: '相关条目', locationCardId: '地点',
  eras: '纪元', months: '月份', yearUnit: '年的叫法', useMonths: '用不用月日', autoCompress: '自动折叠空白',
}
function fieldLabels(r: ChronicleRevision) {
  const labels = r.fields.map(f => (r.targetType === 'event' && f === 'description' ? '描述' : FIELD_LABEL[f] ?? f))
  return [...new Set(labels)].join('、')
}

// ---------- 对比改动 ----------
interface DiffRow { label: string; before: string | null; after: string | null }
type Snap = Record<string, any>
const clip = (t: string, n = 300) => (t.length > n ? t.slice(0, n) + '…' : t)
const yesNo = (v: unknown) => (v ? '是' : '否')
const list = (v: unknown): unknown[] => (Array.isArray(v) ? v : [])
const text = (v: unknown) => (typeof v === 'string' ? v : v == null ? '' : String(v))
// 描述里的 {CARD:id} 换成条目名
const cleanText = (v: unknown) => text(v).replace(/!?\{CARD:([^}]+)\}/g, (_, id: string) => `《${cardName(id)}》`).trim()
const num = (v: unknown) => (typeof v === 'number' ? v : null)

/** 把一份内容拆成「项目 → 给人看的文字」，改前改后逐项对比 */
function describe(type: ChronicleTarget, s: Snap): [string, string][] {
  if (type === 'timeline') {
    const rows: [string, string][] = [
      ['名称', text(s.name)],
      ['类型', STATUS_META[s.status as TimelineStatus]?.label ?? text(s.status)],
      ['说明', clip(text(s.description))],
      ['颜色', text(s.color)],
      ['从哪条线分出', lineName(s.parentId)],
      ['分叉点', eventName(s.forkEventId)],
      ['汇合点', eventName(s.mergeEventId)],
      ['关于的条目', cardName(s.cardId)],
      ['对访客隐藏', yesNo(s.hidden)],
    ]
    // 删除记录里带着当时线上的事件（撤销删除时一起恢复）
    if (Array.isArray(s.events) && s.events.length) rows.push(['线上的事件', `${s.events.length} 件：${clip(s.events.map((e: Snap) => shownTitle(text(e.title))).join('、'), 200)}`])
    return rows
  }
  if (type === 'event') {
    return [
      ['名称', shownTitle(text(s.title))],
      ['种类', KIND_META[s.kind as EventKind]?.label ?? text(s.kind)],
      ['所在时间线', lineName(s.timelineId)],
      ['时间', formatDate(props.calendar, s.eraId, num(s.year), num(s.month), num(s.day)) || '未定'],
      ['结束时间', s.endYear == null ? '' : formatDate(props.calendar, s.endEraId ?? s.eraId, num(s.endYear), num(s.endMonth), num(s.endDay))],
      ['时间说明', text(s.dateText)],
      ['描述', clip(cleanText(s.description))],
      ['地点', cardName(s.locationCardId)],
      ['相关条目', list(s.cardIds).map(cardName).join('、')],
      ['标签', list(s.tags).map(text).join('、')],
      ['图片', list(s.images).length ? `${list(s.images).length} 张` : ''],
      ['颜色', text(s.color)],
      ['对访客隐藏', yesNo(s.hidden)],
    ]
  }
  // 历法：整理一下再显示（缺的项按默认值算）
  const cal = normalizeCalendar(s)
  return [
    ['纪元', cal.eras.map(e => `${e.name}（第 1 年 = ${e.start}${e.backward ? '，倒着数' : ''}）`).join(' / ')],
    ['月份', `${cal.months.length} 个月：${clip(cal.months.map(m => `${m.name} ${m.days} 天`).join('、'), 200)}`],
    ['年的叫法', cal.yearUnit || '年'],
    ['用不用月日', yesNo(cal.useMonths)],
    ['自动折叠空白', yesNo(cal.autoCompress)],
  ]
}

function buildDiff(type: ChronicleTarget, before: Snap | null, after: Snap | null): DiffRow[] {
  // 历法：之前没设置过 = 默认历法，也拿来对比
  if (type === 'calendar') { before = before ?? {}; after = after ?? {} }
  const b = new Map(before ? describe(type, before) : [])
  const a = new Map(after ? describe(type, after) : [])
  const rows: DiffRow[] = []
  for (const label of new Set([...b.keys(), ...a.keys()])) {
    const bv = b.get(label) ?? '', av = a.get(label) ?? ''
    if (before && after) { if (bv !== av) rows.push({ label, before: bv, after: av }) }
    // 新建 / 删除：只列有内容的项（「否」也不列）
    else if ((bv || av) && (bv || av) !== '否') rows.push({ label, before: before ? bv : null, after: after ? av : null })
  }
  return rows
}

async function toggleDiff(r: ChronicleRevision) {
  openDiff[r.id] = !openDiff[r.id]
  if (!openDiff[r.id] || diffs[r.id]) return
  diffLoading[r.id] = true
  try {
    const detail = await chronicleApi.revision(props.projectId, r.id)
    diffs[r.id] = buildDiff(r.targetType, detail.before, detail.after)
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '读取改动失败'))
    openDiff[r.id] = false
  } finally {
    diffLoading[r.id] = false
  }
}

// ---------- 撤销 ----------
function undoLabel(r: ChronicleRevision) {
  if (r.action === 'create') return r.targetType === 'calendar' ? '撤销（恢复默认历法）' : '撤销（删掉）'
  if (r.action === 'delete') return '恢复回来'
  return '撤销这次修改'
}
async function undo(r: ChronicleRevision) {
  const what = r.targetType === 'calendar' ? '历法' : `${TARGET_LABEL[r.targetType]}《${shownTitle(r.title)}》`
  const tip = r.action === 'create'
    ? r.targetType === 'calendar' ? '会把历法恢复成默认设置（公元纪年）。' : `会删除${what}${r.targetType === 'timeline' ? '和它上面的事件' : ''}（删除后还能在这里恢复）。`
    : r.action === 'delete' ? `会把${what}按删除前的样子恢复回来${r.targetType === 'timeline' ? '，当时线上的事件也一起回来' : ''}。`
    : `会把${what}恢复成这次操作之前的样子。这之后别人对它的修改也会被一起覆盖（会再记一笔，可以再撤销回来）。`
  try { await ElMessageBox.confirm(tip, '撤销修改', { confirmButtonText: '撤销', cancelButtonText: '取消', type: 'warning' }) } catch { return }
  busyId.value = r.id
  try {
    const res = await chronicleApi.undo(props.projectId, r.id)
    ElMessage.success(res.message || '已撤销')
    emit('changed')
    await reload()
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '撤销失败，请重试'))
  } finally {
    busyId.value = 0
  }
}

onMounted(async () => {
  void reload()
  try {
    const m = await worldApi.getMembers(props.projectId)
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
select { padding: 6px 10px; border: 1px solid var(--border-line); border-radius: 0; background: var(--bg-sub); color: var(--text-primary); }
.hist-filters { display: flex; flex-wrap: wrap; gap: 16px; margin: 14px 0; font-size: 13px; color: var(--text-secondary); }
.hist-filters label { display: inline-flex; align-items: center; gap: 8px; }
.link { border: 0; padding: 0; background: none; color: var(--accent-color); cursor: pointer; font-size: inherit; }
.link:hover { text-decoration: underline; text-underline-offset: 3px; }
.link.warn { color: var(--danger, #c0504d); }
.link:disabled { opacity: .6; cursor: default; }
.muted { color: var(--text-secondary); font-size: 13px; }
.empty { padding: 24px 0; text-align: center; }
.error { color: var(--danger, #c0504d); font-size: 13px; }
.hist-list { list-style: none; margin: 0; padding: 0; }
.hist-item { display: flex; gap: 12px; padding: 12px 4px; border-bottom: 1px solid var(--border-line); }
.avatar { flex-shrink: 0; width: 32px; height: 32px; display: grid; place-items: center; overflow: hidden; border: 1px solid var(--border-line); background: var(--bg-sub); color: var(--text-secondary); font-size: 14px; }
.avatar img { width: 100%; height: 100%; object-fit: cover; }
.hist-main { flex: 1; min-width: 0; }
.hist-line { margin: 0; font-size: 14px; line-height: 1.6; overflow-wrap: anywhere; }
.verb { margin: 0 4px; color: var(--text-secondary); }
.kind { color: var(--text-secondary); }
.act-delete .verb { color: var(--danger, #c0504d); }
.act-create .verb { color: var(--accent-color); }
.title.gone { color: var(--text-secondary); text-decoration: line-through; }
.fields, .note { margin-left: 4px; color: var(--text-secondary); font-size: 12px; }
.hist-meta { display: flex; flex-wrap: wrap; gap: 14px; margin: 4px 0 0; font-size: 12px; color: var(--text-secondary); }
.hist-diff { margin-top: 10px; padding: 10px 12px; background: var(--bg-sub); border: 1px solid var(--border-line); font-size: 13px; }
.hist-diff dl { margin: 0; display: grid; grid-template-columns: max-content 1fr; gap: 6px 14px; }
.hist-diff dt { color: var(--text-secondary); }
.hist-diff dd { margin: 0; overflow-wrap: anywhere; white-space: pre-wrap; }
.before { color: var(--danger, #c0504d); text-decoration: line-through; text-decoration-thickness: 1px; opacity: .85; }
.after { color: var(--text-primary); }
.arrow { margin: 0 6px; color: var(--text-secondary); }
.more { display: block; margin: 14px auto 0; padding: 8px 18px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); cursor: pointer; }
@media (max-width: 560px) { .hist-diff dl { grid-template-columns: 1fr; } }
</style>
