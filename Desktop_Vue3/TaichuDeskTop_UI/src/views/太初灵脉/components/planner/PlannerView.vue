<template>
  <div class="pv-root">
    <!-- 读权限中 -->
    <div v-if="!access && !accessError" class="pv-state">
      <div class="pv-spinner"></div>
      <p>正在打开生活管理…</p>
    </div>

    <div v-else-if="accessError" class="pv-state" role="alert">
      <p>{{ accessError }}</p>
      <button type="button" class="pv-btn" @click="loadAccess">重试</button>
    </div>

    <!-- 没有特权、也没用过：介绍页 -->
    <section v-else-if="access && !access.canUse" class="pv-locked">
      <p class="pv-eyebrow">太初灵脉 · 生活管理</p>
      <h2>把这个月要做的事，一眼看清楚</h2>
      <p class="pv-locked-lead">生活管理是<strong>{{ access.needTier }}及以上赞助</strong>专享的功能。赞助之后就能用：</p>
      <ul class="pv-feature-list">
        <li><b>月历总览</b>：每天要做什么、几点开始，颜色分好类，今天高亮。</li>
        <li><b>当天时间轴</b>：点某一天，按钟点排开看详细安排，全天的事放在最上面。</li>
        <li><b>备忘 / 地点 / 分类</b>：每件事都能写备忘，生活、工作、学习分开管。</li>
        <li><b>到点提醒</b>：站内通知、浏览器弹窗；{{ access.emailTier }}赞助还能收邮件提醒。</li>
        <li><b>重复安排</b>：每天、每周、每月自动出现，还能导出到手机日历。</li>
      </ul>
      <router-link to="/sponsors" class="pv-btn primary">去赞助墙看看</router-link>
      <p v-if="access.until" class="pv-hint">你的赞助已于 {{ formatUntil(access.until) }} 到期。</p>
    </section>

    <template v-else-if="access">
      <header class="pv-head">
        <div class="pv-title">
          <h2>生活管理</h2>
          <p>这个月要做的事一眼看清；点某一天看当天的详细安排。</p>
        </div>
        <div class="pv-tools">
          <button
            v-if="access.canEdit && notifyPerm !== 'unsupported'"
            type="button"
            class="pv-btn"
            :class="{ on: notifyPerm === 'granted' }"
            :disabled="notifyPerm === 'granted'"
            :title="notifyPerm === 'denied' ? '浏览器禁止了通知：在地址栏左边的网站设置里把「通知」改成允许' : ''"
            @click="enableBrowser"
          >
            {{ notifyPerm === 'granted' ? '✓ 浏览器提醒已开启' : notifyPerm === 'denied' ? '浏览器提醒被禁止了' : '🔔 开启浏览器提醒' }}
          </button>
          <button type="button" class="pv-btn" :disabled="exporting" @click="exportIcs">{{ exporting ? '导出中…' : '导出到手机日历（.ics）' }}</button>
        </div>
      </header>

      <div v-if="access.readOnly" class="pv-banner" role="status">
        赞助已到期，续费后恢复编辑和提醒，你的安排都还在。现在可以查看和导出。
        <router-link to="/sponsors" class="pv-link">去续费</router-link>
      </div>
      <div v-if="notice" class="pv-banner info" role="status">
        {{ notice }}
        <button type="button" class="pv-x" aria-label="关闭提示" @click="notice = ''">✕</button>
      </div>

      <!-- 本月概览 -->
      <section class="pv-overview" aria-label="本月概览">
        <div class="pv-stats">
          <div class="pv-stat">
            <span class="pv-stat-num">{{ monthStats.total }}</span>
            <span class="pv-stat-label">{{ cursor.month0 + 1 }} 月共几件</span>
          </div>
          <div class="pv-stat">
            <span class="pv-stat-num">{{ monthStats.done }}</span>
            <span class="pv-stat-label">已完成</span>
            <span class="pv-progress" aria-hidden="true"><i :style="{ width: monthStats.total ? (monthStats.done / monthStats.total) * 100 + '%' : '0' }"></i></span>
          </div>
          <div class="pv-stat">
            <span class="pv-stat-num">{{ (occMap[today] || []).filter(o => !o.done).length }}</span>
            <span class="pv-stat-label">今天还剩</span>
          </div>
        </div>
        <div class="pv-next">
          <h3>接下来 7 天</h3>
          <p v-if="!next7.length" class="pv-hint">接下来 7 天没有安排，可以放松一下。</p>
          <ul v-else class="pv-next-list">
            <li v-for="o in next7.slice(0, 8)" :key="o.key">
              <button type="button" class="pv-next-item" :class="{ done: o.done }" @click="openDay(o.date)">
                <span class="pv-next-day">{{ dayShort(o.date) }}</span>
                <i class="pv-next-dot" :style="{ background: colorOf(o.ev) }"></i>
                <span class="pv-next-time">{{ o.ev.allDay || !o.ev.startTime ? '全天' : o.ev.startTime }}</span>
                <span class="pv-next-title">{{ o.ev.title }}</span>
              </button>
            </li>
          </ul>
          <p v-if="next7.length > 8" class="pv-hint">还有 {{ next7.length - 8 }} 件，点日历里的日子看全部。</p>
        </div>
      </section>

      <!-- 工具栏：视图切换、翻月、分类筛选、新建 -->
      <div class="pv-toolbar">
        <div class="pv-tabs" role="tablist" aria-label="视图">
          <button type="button" role="tab" :aria-selected="view === 'month'" :class="{ active: view === 'month' }" @click="view = 'month'">月</button>
          <button type="button" role="tab" :aria-selected="view === 'day'" :class="{ active: view === 'day' }" @click="view = 'day'">日</button>
        </div>
        <div v-if="view === 'month'" class="pv-month-nav">
          <button type="button" class="pv-btn icon" aria-label="上个月" @click="shiftMonth(-1)">◀</button>
          <strong class="pv-month-label">{{ cursor.year }} 年 {{ cursor.month0 + 1 }} 月</strong>
          <button type="button" class="pv-btn icon" aria-label="下个月" @click="shiftMonth(1)">▶</button>
          <button type="button" class="pv-btn" @click="goToday">今天</button>
        </div>
        <span class="pv-spacer"></span>
        <label class="pv-filter">
          <span>分类</span>
          <select v-model="categoryFilter">
            <option value="">全部</option>
            <option v-for="c in categories" :key="c" :value="c">{{ c }}</option>
            <option v-if="hasUncategorized" value="__none__">未分类</option>
          </select>
        </label>
        <!-- 日视图里新建按钮在当天标题旁边，这里只在月视图显示 -->
        <button v-if="access.canEdit && view === 'month'" type="button" class="pv-btn primary" @click="openNew(today, null)">＋ 新建安排</button>
      </div>

      <p v-if="loadError" class="pv-banner error" role="alert">{{ loadError }} <button type="button" class="pv-link" @click="loadEvents">重试</button></p>
      <div class="pv-stage" :class="{ loading }">
        <PlannerMonthView
          v-if="view === 'month'"
          :cells="grid"
          :occ="occMap"
          :selected="selectedDate"
          :read-only="!access.canEdit"
          @pick="openDay"
          @add="d => openNew(d, null)"
          @open="openEdit"
          @toggle="toggleDone"
          @move="moveEvent"
        />
        <PlannerDayView
          v-else
          :date="selectedDate"
          :list="occMap[selectedDate] || []"
          :read-only="!access.canEdit"
          @shift="shiftDay"
          @today="openDay(today)"
          @add="openNew"
          @open="openEdit"
          @toggle="toggleDone"
          @remove="removeOcc"
        />
      </div>

      <PlannerEventEditor
        v-if="editor.open"
        :key="editor.key"
        :event="editor.event"
        :initial-date="editor.date"
        :initial-start="editor.start"
        :read-only="!access.canEdit"
        :can-email="access.canEmail"
        :email-tier="access.emailTier"
        :has-email-address="access.hasEmailAddress"
        :browser-permission="notifyPerm"
        :categories="categories"
        :busy="saving"
        :error="editorError"
        @save="saveEvent"
        @remove="removeFromEditor"
        @close="editor.open = false"
      />

      <!-- 底部小提示（删除后可以撤销） -->
      <transition name="pv-fade">
        <div v-if="toast.text" class="pv-toast" role="status">
          <span>{{ toast.text }}</span>
          <button v-if="toast.undo" type="button" class="pv-toast-undo" @click="undoDelete">撤销</button>
        </div>
      </transition>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { plannerApi, type PlannerAccess, type PlannerEvent, type PlannerEventInput } from '@/api/planner'
import {
  enablePlannerBrowserNotify, plannerNotifyPermission, refreshPlannerNotifyPermission, setPlannerReminderActive,
} from '@/composables/usePlannerReminders'
import PlannerMonthView from './PlannerMonthView.vue'
import PlannerDayView from './PlannerDayView.vue'
import PlannerEventEditor from './PlannerEventEditor.vue'
import {
  addDays, buildMonthGrid, colorOf, downloadText, expandRange, isValidDate, parseDateStr, toIcs, toInput, todayStr,
  WEEK_HEADERS, weekdayMon0, type Occurrence,
} from './plannerModel'

const route = useRoute()
/** 浏览器通知权限（模板里用本地 computed，避免直接用外部导入的 ref） */
const notifyPerm = computed(() => plannerNotifyPermission.value)

const access = ref<PlannerAccess | null>(null)
const accessError = ref('')
const events = ref<PlannerEvent[]>([])
const loading = ref(false)
const loadError = ref('')
const notice = ref('')
const exporting = ref(false)

const today = ref(todayStr())
const view = ref<'month' | 'day'>('month')
const selectedDate = ref(today.value)
const initial = parseDateStr(today.value)
const cursor = reactive({ year: initial.y, month0: initial.m - 1 })
const categoryFilter = ref('')

const errText = (e: any, fallback = '操作失败，请重试') =>
  e?.response?.data?.message || e?.friendlyMessage || fallback

const formatUntil = (s: string) => new Date(s).toLocaleDateString('zh-CN')
const dayShort = (d: string) => {
  if (d === today.value) return '今天'
  if (d === addDays(today.value, 1)) return '明天'
  const { m, d: day } = parseDateStr(d)
  return `${m}/${day} 周${WEEK_HEADERS[weekdayMon0(d)]}`
}

// ---------------- 日历范围、展开 ----------------

const grid = computed(() => buildMonthGrid(cursor.year, cursor.month0))
/** 要拉的数据范围：当前月历的 6 周 + 接下来 7 天 */
const range = computed(() => {
  const first = grid.value[0]?.date || today.value
  const last = grid.value[grid.value.length - 1]?.date || today.value
  const weekEnd = addDays(today.value, 6)
  return { from: first < today.value ? first : today.value, to: last > weekEnd ? last : weekEnd }
})

const categories = computed(() => Array.from(new Set(events.value.map(e => e.category).filter(Boolean))).sort((a, b) => a.localeCompare(b, 'zh-CN')))
const hasUncategorized = computed(() => events.value.some(e => !e.category))
const filtered = computed(() => {
  const f = categoryFilter.value
  if (!f) return events.value
  return events.value.filter(e => (f === '__none__' ? !e.category : e.category === f))
})
const occMap = computed(() => expandRange(filtered.value, range.value.from, range.value.to))

const monthStats = computed(() => {
  let total = 0
  let done = 0
  for (const cell of grid.value) {
    if (!cell.inMonth) continue
    for (const o of occMap.value[cell.date] || []) { total++; if (o.done) done++ }
  }
  return { total, done }
})

const next7 = computed(() => {
  const list: Occurrence[] = []
  for (let i = 0; i < 7; i++) list.push(...(occMap.value[addDays(today.value, i)] || []))
  return list
})

// ---------------- 读数据 ----------------

async function loadAccess() {
  accessError.value = ''
  try {
    access.value = await plannerApi.access()
    // 能用的人才让全局去轮询浏览器提醒
    setPlannerReminderActive(access.value.canEdit)
    if (access.value.canUse) await loadEvents()
  } catch (e: any) {
    accessError.value = errText(e, '生活管理打不开，请稍后重试')
  }
}

let loadVersion = 0
async function loadEvents() {
  if (!access.value?.canUse) return
  const version = ++loadVersion
  loading.value = true
  loadError.value = ''
  try {
    const list = await plannerApi.list(range.value.from, range.value.to)
    if (version === loadVersion) events.value = list
  } catch (e: any) {
    if (version === loadVersion) loadError.value = errText(e, '安排加载失败')
  } finally {
    if (version === loadVersion) loading.value = false
  }
}

watch(() => `${range.value.from}|${range.value.to}`, () => { void loadEvents() })

// ---------------- 视图切换 ----------------

function setCursorTo(date: string) {
  const { y, m } = parseDateStr(date)
  if (cursor.year !== y || cursor.month0 !== m - 1) { cursor.year = y; cursor.month0 = m - 1 }
}
function openDay(date: string) {
  selectedDate.value = date
  setCursorTo(date)
  view.value = 'day'
}
function shiftDay(n: number) { openDay(addDays(selectedDate.value, n)) }
function shiftMonth(n: number) {
  const d = new Date(cursor.year, cursor.month0 + n, 1)
  cursor.year = d.getFullYear()
  cursor.month0 = d.getMonth()
}
function goToday() {
  today.value = todayStr()
  selectedDate.value = today.value
  setCursorTo(today.value)
}

// 从通知点进来：/lingmai/planner?date=2026-11-01 直接打开那一天
watch(() => route.query.date, d => {
  const date = Array.isArray(d) ? d[0] : d
  if (typeof date === 'string' && isValidDate(date)) openDay(date)
}, { immediate: true })

// ---------------- 编辑 ----------------

const editor = reactive({ open: false, key: 0, event: null as PlannerEvent | null, date: today.value, start: null as string | null })
const saving = ref(false)
const editorError = ref('')

function openNew(date: string, start: string | null) {
  if (!access.value?.canEdit) return
  Object.assign(editor, { open: true, key: editor.key + 1, event: null, date, start })
  editorError.value = ''
}
function openEdit(o: Occurrence) {
  Object.assign(editor, { open: true, key: editor.key + 1, event: o.ev, date: o.date, start: null })
  editorError.value = ''
}

const upsert = (item: PlannerEvent) => {
  const i = events.value.findIndex(e => e.id === item.id)
  if (i === -1) events.value = [...events.value, item]
  else events.value = events.value.map(e => (e.id === item.id ? item : e))
}

async function saveEvent(input: PlannerEventInput) {
  if (saving.value) return
  saving.value = true
  editorError.value = ''
  try {
    const res = editor.event ? await plannerApi.update(editor.event.id, input) : await plannerApi.create(input)
    upsert(res.item)
    editor.open = false
    if (res.notice) notice.value = res.notice
    showToast(editor.event ? '已保存' : '已添加')
  } catch (e: any) {
    editorError.value = errText(e, '保存失败，请重试')
    if (e?.response?.status === 403) void refreshAccess()
  } finally {
    saving.value = false
  }
}

async function moveEvent(id: string, date: string) {
  const ev = events.value.find(e => e.id === id)
  if (!ev || ev.date === date || !access.value?.canEdit) return
  const old = ev
  upsert({ ...ev, date })
  try {
    const res = await plannerApi.update(id, { ...toInput(ev), date })
    upsert(res.item)
    showToast(`已挪到 ${dayShort(date)}`)
  } catch (e: any) {
    upsert(old)
    showToast(errText(e, '挪动失败，请重试'))
  }
}

/** 勾选完成：重复事项只勾那一天 */
async function toggleDone(o: Occurrence) {
  if (!access.value?.canEdit) return
  const ev = o.ev
  const done = !o.done
  const repeatDay = ev.repeat !== 'none' && !ev.isDone
  const optimistic: PlannerEvent = repeatDay
    ? { ...ev, doneDates: done ? [...ev.doneDates, o.date] : ev.doneDates.filter(d => d !== o.date) }
    : { ...ev, isDone: done }
  upsert(optimistic)
  try {
    const res = await plannerApi.setDone(ev.id, done, repeatDay ? o.date : undefined)
    upsert(res.item)
  } catch (e: any) {
    upsert(ev)
    showToast(errText(e, '没保存上，请重试'))
  }
}

// ---------------- 删除 + 撤销 ----------------

const toast = reactive({ text: '', undo: null as PlannerEvent | null })
let toastTimer: ReturnType<typeof setTimeout> | null = null
function showToast(text: string, undo: PlannerEvent | null = null, ms = 2500) {
  if (toastTimer) clearTimeout(toastTimer)
  toast.text = text
  toast.undo = undo
  toastTimer = setTimeout(() => { toast.text = ''; toast.undo = null }, ms)
}

async function removeEvent(ev: PlannerEvent) {
  if (!access.value?.canEdit) return
  events.value = events.value.filter(e => e.id !== ev.id)
  try {
    await plannerApi.remove(ev.id)
    showToast(ev.repeat !== 'none' ? `已删除整个重复安排「${ev.title}」` : `已删除「${ev.title}」`, ev, 8000)
  } catch (e: any) {
    upsert(ev)
    showToast(errText(e, '删除失败，请重试'))
  }
}
const removeOcc = (o: Occurrence) => { void removeEvent(o.ev) }
function removeFromEditor() {
  const ev = editor.event
  editor.open = false
  if (ev) void removeEvent(ev)
}
async function undoDelete() {
  const ev = toast.undo
  if (!ev) return
  toast.text = ''
  toast.undo = null
  try {
    const res = await plannerApi.restore(ev.id)
    upsert(res.item)
    showToast('已恢复')
  } catch (e: any) {
    showToast(errText(e, '恢复失败，请重试'))
  }
}

// ---------------- 浏览器提醒、导出 ----------------

async function enableBrowser() {
  const ok = await enablePlannerBrowserNotify()
  showToast(ok ? '浏览器提醒已开启：网页开着时到点会弹系统通知' : '浏览器没有允许通知。可以在地址栏左边的网站设置里把「通知」改成允许。', null, 5000)
}

async function exportIcs() {
  exporting.value = true
  try {
    const all = await plannerApi.list()
    downloadText(`生活管理-${todayStr()}.ics`, toIcs(all))
    showToast(`已导出 ${all.length} 条安排，可以导入手机日历、Outlook 或 Google 日历`, null, 4000)
  } catch (e: any) {
    showToast(errText(e, '导出失败，请重试'))
  } finally {
    exporting.value = false
  }
}

/** 写操作被后端拒绝（比如赞助刚好到期）时，重新问一下权限，界面切成只读 */
async function refreshAccess() {
  try {
    access.value = await plannerApi.access()
    setPlannerReminderActive(access.value.canEdit)
  } catch { /* 下次进来再说 */ }
}

// 过了零点「今天」要跟着变
let dayTimer: ReturnType<typeof setInterval> | null = null
onMounted(() => {
  refreshPlannerNotifyPermission()
  void loadAccess()
  dayTimer = setInterval(() => { const t = todayStr(); if (t !== today.value) today.value = t }, 60_000)
})
onUnmounted(() => {
  if (dayTimer) clearInterval(dayTimer)
  if (toastTimer) clearTimeout(toastTimer)
})
</script>

<style scoped>
.pv-root { flex: 1; min-height: 0; overflow-y: auto; padding: 20px clamp(12px, 3vw, 32px) 32px; display: flex; flex-direction: column; gap: 16px; color: var(--text-primary); background: var(--paper-card); }
.pv-state { flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 12px; min-height: 260px; color: var(--text-secondary); font-size: 14px; }
.pv-spinner { width: 24px; height: 24px; border: 2px solid var(--border-line); border-top-color: var(--accent-color); border-radius: 50%; animation: pv-spin .8s linear infinite; }
@keyframes pv-spin { to { transform: rotate(360deg); } }

.pv-head { display: flex; flex-wrap: wrap; align-items: flex-end; justify-content: space-between; gap: 12px; }
.pv-title h2 { margin: 0; font-size: clamp(20px, 3vw, 28px); font-weight: 800; letter-spacing: -.02em; }
.pv-title p { margin: 4px 0 0; font-size: 13px; color: var(--text-secondary); }
.pv-tools { display: flex; flex-wrap: wrap; gap: 8px; }

.pv-btn { display: inline-flex; align-items: center; justify-content: center; gap: 6px; min-height: 32px; padding: 0 12px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); border-radius: 0; cursor: pointer; font-size: 13px; font-weight: 600; text-decoration: none; white-space: nowrap; }
.pv-btn:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); }
.pv-btn:disabled { opacity: .6; cursor: default; }
.pv-btn.on { color: var(--accent-color); border-color: var(--accent-color); background: var(--accent-soft); opacity: 1; }
.pv-btn.primary { background: var(--accent-color); border-color: var(--accent-color); color: var(--paper-card); }
.pv-btn.primary:hover:not(:disabled) { opacity: .88; color: var(--paper-card); }
.pv-btn.icon { width: 32px; padding: 0; }

.pv-banner { margin: 0; display: flex; align-items: center; gap: 10px; flex-wrap: wrap; padding: 10px 14px; font-size: 13px; border-left: 3px solid var(--warning, #c77700); background: color-mix(in srgb, var(--warning, #c77700) 10%, var(--paper-card)); }
.pv-banner.info { border-left-color: var(--accent-color); background: var(--accent-soft); }
.pv-banner.error { border-left-color: var(--danger, #d33); background: color-mix(in srgb, var(--danger, #d33) 10%, var(--paper-card)); }
.pv-link { background: none; border: none; padding: 0; color: var(--accent-color); font-weight: 700; cursor: pointer; font-size: 13px; text-decoration: underline; }
.pv-x { margin-left: auto; background: none; border: none; color: var(--text-secondary); cursor: pointer; }

.pv-overview { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1.4fr); gap: 16px; padding: 14px; border: 1px solid var(--border-line); background: var(--bg-sub); }
.pv-stats { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 8px; align-content: start; }
.pv-stat { display: flex; flex-direction: column; gap: 2px; padding: 10px 12px; background: var(--paper-card); border: 1px solid var(--border-line); min-width: 0; }
.pv-stat-num { font: 800 26px/1.1 monospace; color: var(--text-primary); }
.pv-stat-label { font-size: 12px; color: var(--text-secondary); }
.pv-progress { display: block; height: 4px; margin-top: 4px; background: var(--border-line); }
.pv-progress i { display: block; height: 100%; background: var(--accent-color); }
.pv-next h3 { margin: 0 0 6px; font-size: 13px; font-weight: 800; color: var(--text-secondary); }
.pv-next-list { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 2px 12px; }
.pv-next-item { width: 100%; display: flex; align-items: center; gap: 8px; padding: 4px 6px; background: none; border: none; color: var(--text-primary); cursor: pointer; text-align: left; font-size: 13px; min-width: 0; }
.pv-next-item:hover { background: var(--paper-card); }
.pv-next-item.done { opacity: .45; }
.pv-next-item.done .pv-next-title { text-decoration: line-through; }
.pv-next-day { flex: 0 0 74px; font-size: 12px; color: var(--text-secondary); white-space: nowrap; }
.pv-next-dot { flex: 0 0 8px; width: 8px; height: 8px; }
.pv-next-time { flex: 0 0 auto; font: 600 12px monospace; color: var(--text-secondary); }
.pv-next-title { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 600; }
.pv-hint { margin: 4px 0 0; font-size: 12px; color: var(--text-secondary); }

.pv-toolbar { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 12px; }
.pv-tabs { display: inline-flex; border: 1px solid var(--border-line); }
.pv-tabs button { min-width: 48px; min-height: 32px; padding: 0 14px; border: none; background: var(--paper-card); color: var(--text-secondary); cursor: pointer; font-size: 13px; font-weight: 700; border-radius: 0; }
.pv-tabs button + button { border-left: 1px solid var(--border-line); }
.pv-tabs button.active { background: var(--accent-color); color: var(--paper-card); }
.pv-month-nav { display: flex; align-items: center; gap: 6px; }
.pv-month-label { min-width: 110px; text-align: center; font-size: 15px; }
.pv-spacer { flex: 1; }
.pv-filter { display: inline-flex; align-items: center; gap: 6px; font-size: 12px; color: var(--text-secondary); }
.pv-filter select { min-height: 32px; padding: 0 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); border-radius: 0; font-size: 13px; }

.pv-stage { min-width: 0; transition: opacity .15s; }
.pv-stage.loading { opacity: .6; }

.pv-locked { max-width: 640px; margin: 24px auto; padding: 28px; border: 1px solid var(--border-line); background: var(--bg-sub); display: flex; flex-direction: column; gap: 12px; align-items: flex-start; }
.pv-eyebrow { margin: 0; font-size: 12px; letter-spacing: .1em; color: var(--accent-color); font-weight: 700; }
.pv-locked h2 { margin: 0; font-size: 24px; font-weight: 800; }
.pv-locked-lead { margin: 0; font-size: 14px; color: var(--text-secondary); }
.pv-feature-list { margin: 0; padding-left: 20px; display: flex; flex-direction: column; gap: 6px; font-size: 14px; line-height: 1.6; }

.pv-toast { position: fixed; left: 50%; bottom: 28px; transform: translateX(-50%); z-index: 1500; display: flex; align-items: center; gap: 14px; max-width: calc(100vw - 32px); padding: 10px 16px; background: var(--text-primary); color: var(--paper-card); font-size: 13px; box-shadow: 0 8px 24px rgba(0, 0, 0, .2); }
.pv-toast-undo { background: none; border: 1px solid currentColor; color: inherit; padding: 2px 10px; cursor: pointer; font-weight: 700; border-radius: 0; }
.pv-fade-enter-active, .pv-fade-leave-active { transition: opacity .2s; }
.pv-fade-enter-from, .pv-fade-leave-to { opacity: 0; }

@media (max-width: 900px) {
  .pv-overview { grid-template-columns: minmax(0, 1fr); }
}
@media (max-width: 640px) {
  .pv-root { padding: 14px 10px 24px; gap: 12px; }
  .pv-stats { gap: 6px; }
  .pv-stat { padding: 8px; }
  .pv-stat-num { font-size: 20px; }
  .pv-next-list { grid-template-columns: minmax(0, 1fr); }
  .pv-month-label { min-width: 0; }
  .pv-spacer { display: none; }
  .pv-locked { padding: 18px; margin: 8px 0; }
}
@media (prefers-reduced-motion: reduce) { .pv-stage, .pv-fade-enter-active, .pv-fade-leave-active { transition: none; } .pv-spinner { animation: none; } }
</style>
