<template>
  <section class="pd-day" :aria-label="`${formatDayLabel(date)} 的安排`">
    <header class="pd-head">
      <div class="pd-nav">
        <button type="button" class="pd-btn" aria-label="前一天" @click="emit('shift', -1)">◀</button>
        <h3 class="pd-date">{{ formatDayLabel(date) }}<span v-if="isToday" class="pd-today">今天</span></h3>
        <button type="button" class="pd-btn" aria-label="后一天" @click="emit('shift', 1)">▶</button>
        <button v-if="!isToday" type="button" class="pd-btn" @click="emit('today')">回到今天</button>
      </div>
      <div class="pd-summary">
        共 {{ list.length }} 件<template v-if="doneCount">，完成 {{ doneCount }} 件</template>
        <button v-if="!readOnly" type="button" class="pd-primary" @click="emit('add', date, null)">＋ 新建安排</button>
      </div>
    </header>

    <!-- 全天事项放在最上面 -->
    <div v-if="allDay.length" class="pd-allday">
      <span class="pd-allday-label">全天</span>
      <div class="pd-allday-list">
        <div
          v-for="o in allDay"
          :key="o.key"
          class="pd-chip"
          :class="{ done: o.done }"
          :style="{ '--ev-color': colorOf(o.ev), '--ev-tint': colorOf(o.ev) + '1f' }"
          role="button"
          tabindex="0"
          @click="emit('open', o)"
          @keydown.enter.self="emit('open', o)"
        >
          <button type="button" class="pd-check" :class="{ on: o.done }" :disabled="readOnly" :aria-label="o.done ? '标记为没完成' : '标记为完成'" @click.stop="emit('toggle', o)"></button>
          <span class="pd-chip-title">{{ o.ev.title }}</span>
          <span v-if="o.ev.location" class="pd-chip-meta">📍{{ o.ev.location }}</span>
          <span v-if="o.ev.category" class="pd-chip-meta">{{ o.ev.category }}</span>
          <span v-if="focusMin(o)" class="pd-focus-min" :title="`这件事这一天专注了 ${focusMin(o)} 分钟`">🍅{{ focusMin(o) }} 分</span>
          <button type="button" class="pd-focus" title="用番茄钟专注做这件事" @click.stop="startFocus(o)">专注</button>
          <button v-if="!readOnly" type="button" class="pd-del" aria-label="删除" @click.stop="emit('remove', o)">✕</button>
        </div>
      </div>
    </div>

    <p v-if="!list.length" class="pd-empty">
      这一天还没有安排。<template v-if="!readOnly">点下面的时间格，或者「＋ 新建安排」就能加一条。</template>
    </p>

    <!-- 这一天的番茄钟专注记录（没有就不显示） -->
    <section v-if="focusSessions.length" class="pd-focus-log" aria-label="这一天的专注记录">
      <h4 class="pd-fl-head">
        🍅 专注记录
        <small>{{ focusSessions.length }} 段 · 共 {{ focusTotalMin }} 分钟</small>
        <router-link to="/lingmai/pomodoro" class="pd-fl-link">打开番茄钟</router-link>
      </h4>
      <ul class="pd-fl-list">
        <li v-for="s in focusSessions" :key="s.clientId" class="pd-fl-item" :class="{ abandoned: !s.done }">
          <span class="pd-fl-time">{{ fmtTime(s.startMs) }}–{{ fmtTime(s.endMs) }}</span>
          <span class="pd-fl-task">{{ s.task || '（没写在做什么）' }}</span>
          <span class="pd-fl-min">{{ Math.round(s.focusSec / 60) }} 分</span>
          <span class="pd-fl-tag">{{ s.done ? '完成' : '放弃' }}</span>
        </li>
      </ul>
    </section>

    <!-- 时间轴：一小时一行，点空白处在那个钟点新建 -->
    <div class="pd-timeline" :style="{ height: (24 - startHour) * HOUR_PX + 'px' }">
      <div
        v-for="h in hours"
        :key="h"
        class="pd-hour"
        :style="{ top: (h - startHour) * HOUR_PX + 'px', height: HOUR_PX + 'px' }"
        @click="!readOnly && emit('add', date, `${pad(h)}:00`)"
      >
        <span class="pd-hour-label">{{ pad(h) }}:00</span>
      </div>

      <div v-if="nowTop !== null" class="pd-now" :style="{ top: nowTop + 'px' }" aria-hidden="true"><i></i></div>

      <div class="pd-events">
        <div
          v-for="x in timed"
          :key="x.o.key"
          class="pd-event"
          :class="{ done: x.o.done, short: x.e - x.s < 45, medium: x.e - x.s < 120, narrow: x.lanes > 1 }"
          :style="{
            top: ((x.s - startHour * 60) / 60) * HOUR_PX + 'px',
            height: Math.max(((x.e - x.s) / 60) * HOUR_PX, 26) + 'px',
            left: `calc(${(x.lane / x.lanes) * 100}% + 2px)`,
            width: `calc(${100 / x.lanes}% - 4px)`,
            '--ev-color': colorOf(x.o.ev),
            '--ev-tint': colorOf(x.o.ev) + '26',
          }"
          role="button"
          tabindex="0"
          @click="emit('open', x.o)"
          @keydown.enter.self="emit('open', x.o)"
        >
          <div class="pd-event-top">
            <button type="button" class="pd-check" :class="{ on: x.o.done }" :disabled="readOnly" :aria-label="x.o.done ? '标记为没完成' : '标记为完成'" @click.stop="emit('toggle', x.o)"></button>
            <span class="pd-event-time">{{ timeLabel(x.o.ev) }}</span>
            <strong class="pd-event-title">{{ x.o.ev.title }}</strong>
            <span v-if="focusMin(x.o)" class="pd-focus-min" :title="`这件事这一天专注了 ${focusMin(x.o)} 分钟`">🍅{{ focusMin(x.o) }}</span>
            <button type="button" class="pd-focus" title="用番茄钟专注做这件事" @click.stop="startFocus(x.o)">专注</button>
            <button v-if="!readOnly" type="button" class="pd-del" aria-label="删除" @click.stop="emit('remove', x.o)">✕</button>
          </div>
          <p v-if="x.o.ev.location || x.o.ev.category" class="pd-event-meta">
            <span v-if="x.o.ev.location">📍{{ x.o.ev.location }}</span>
            <span v-if="x.o.ev.category">{{ x.o.ev.category }}</span>
            <span v-if="x.o.ev.repeat !== 'none'">↻ {{ repeatLabel(x.o.ev.repeat) }}</span>
          </p>
          <p v-if="x.o.ev.memo" class="pd-event-memo">{{ x.o.ev.memo }}</p>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { pomodoroApi, type PomoSession } from '@/api/pomodoro'
import { colorOf, formatDayLabel, pad, repeatLabel, timeLabel, timeToMin, todayStr, type Occurrence } from './plannerModel'
import { dateStrStartMs, fmtTime, shiftDayStart } from '../pomodoro/pomodoroModel'

const props = defineProps<{
  date: string
  list: Occurrence[]
  readOnly: boolean
}>()

const emit = defineEmits<{
  (e: 'shift', days: number): void
  (e: 'today'): void
  (e: 'add', date: string, startTime: string | null): void
  (e: 'open', o: Occurrence): void
  (e: 'toggle', o: Occurrence): void
  (e: 'remove', o: Occurrence): void
}>()

/** 一小时多高（像素） */
const HOUR_PX = 52

const isToday = computed(() => props.date === todayStr())
const doneCount = computed(() => props.list.filter(o => o.done).length)
const allDay = computed(() => props.list.filter(o => o.ev.allDay || timeToMin(o.ev.startTime) === null))

interface Placed { o: Occurrence; s: number; e: number; lane: number; lanes: number }

/** 有时间的安排：算出位置；时间重叠的并排显示（分成几列） */
const timed = computed<Placed[]>(() => {
  const list: Placed[] = []
  for (const o of props.list) {
    if (o.ev.allDay) continue
    const s = timeToMin(o.ev.startTime)
    if (s === null) continue
    const end = timeToMin(o.ev.endTime)
    // 没填结束时间的按半小时显示
    const e = Math.min(end !== null && end > s ? end : s + 30, 24 * 60)
    list.push({ o, s, e, lane: 0, lanes: 1 })
  }
  list.sort((a, b) => a.s - b.s || b.e - a.e)
  let cluster: Placed[] = []
  let clusterEnd = -1
  let laneEnds: number[] = []
  const flush = () => {
    const n = Math.max(1, laneEnds.length)
    cluster.forEach(x => { x.lanes = n })
    cluster = []
    laneEnds = []
    clusterEnd = -1
  }
  for (const x of list) {
    if (cluster.length && x.s >= clusterEnd) flush()
    let lane = laneEnds.findIndex(end => end <= x.s)
    if (lane === -1) { lane = laneEnds.length; laneEnds.push(x.e) } else laneEnds[lane] = x.e
    x.lane = lane
    cluster.push(x)
    clusterEnd = Math.max(clusterEnd, x.e)
  }
  flush()
  return list
})

/** 时间轴默认从早上 6 点开始；有更早的安排就往前延 */
const startHour = computed(() => {
  const earliest = Math.min(...timed.value.map(x => Math.floor(x.s / 60)), 6)
  return Math.max(0, earliest)
})
const hours = computed(() => Array.from({ length: 24 - startHour.value }, (_, i) => startHour.value + i))

// 今天的话画一条「现在」红线，每分钟挪一下
const nowMin = ref(0)
const updateNow = () => { const d = new Date(); nowMin.value = d.getHours() * 60 + d.getMinutes() }
let timer: ReturnType<typeof setInterval> | null = null
onMounted(() => { updateNow(); timer = setInterval(updateNow, 60_000) })
onUnmounted(() => { if (timer) clearInterval(timer) })
const nowTop = computed(() => {
  if (!isToday.value || nowMin.value < startHour.value * 60) return null
  return ((nowMin.value - startHour.value * 60) / 60) * HOUR_PX
})

// ---------------- 番茄钟联动 ----------------

const router = useRouter()

/** 点「专注」：带上这件事跳到番茄钟，那边只填好，不自动开始 */
function startFocus(o: Occurrence) {
  router.push({ path: '/lingmai/pomodoro', query: { event: o.ev.id, date: o.date, title: o.ev.title } }).catch(() => {})
}

/** 这一天（本地日期 0 点到 24 点，按开始时间）的番茄记录 */
const focusSessions = ref<PomoSession[]>([])
const focusTotalMin = computed(() => Math.round(focusSessions.value.reduce((sum, s) => sum + s.focusSec, 0) / 60))

let focusLoadId = 0
async function loadFocusSessions() {
  const id = ++focusLoadId
  const from = dateStrStartMs(props.date)
  try {
    const list = await pomodoroApi.listSessions(from, shiftDayStart(from, 1))
    if (id === focusLoadId) focusSessions.value = list
  } catch {
    // 记录拉不到不影响看安排，这一块先不显示
    if (id === focusLoadId) focusSessions.value = []
  }
}
watch(() => props.date, () => { void loadFocusSessions() }, { immediate: true })
// 从番茄钟回来（或者桌面刚传了记录）时刷新一下
const onWindowFocus = () => { void loadFocusSessions() }
onMounted(() => window.addEventListener('focus', onWindowFocus))
onUnmounted(() => window.removeEventListener('focus', onWindowFocus))

/**
 * 每件事这一天专注了多少分钟：按「事项 id + 是哪一天」对上。
 * 不重复的事项，记录里没带日期的也算它的。
 */
const focusSecByKey = computed(() => {
  const map = new Map<string, number>()
  for (const s of focusSessions.value) {
    if (!s.plannerEventId) continue
    const key = `${s.plannerEventId}@${s.plannerDate || ''}`
    map.set(key, (map.get(key) || 0) + s.focusSec)
  }
  return map
})
function focusMin(o: Occurrence) {
  const map = focusSecByKey.value
  let sec = map.get(`${o.ev.id}@${o.date}`) || 0
  if (o.ev.repeat === 'none') sec += map.get(`${o.ev.id}@`) || 0
  return Math.round(sec / 60)
}
</script>

<style scoped>
.pd-day { display: flex; flex-direction: column; gap: 12px; min-width: 0; }
.pd-head { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px 16px; }
.pd-nav { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
.pd-date { margin: 0 4px; font-size: 18px; font-weight: 800; color: var(--text-primary); display: flex; align-items: center; gap: 8px; }
.pd-today { font-size: 11px; font-weight: 700; color: var(--paper-card); background: var(--accent-color); padding: 2px 6px; }
.pd-btn { background: var(--paper-card); border: 1px solid var(--border-line); color: var(--text-secondary); min-height: 30px; padding: 0 10px; border-radius: 0; cursor: pointer; font-size: 12px; }
.pd-btn:hover { color: var(--accent-color); border-color: var(--accent-color); }
.pd-summary { display: flex; align-items: center; gap: 12px; font-size: 13px; color: var(--text-secondary); }
.pd-primary { background: var(--accent-color); color: var(--paper-card); border: 1px solid var(--accent-color); min-height: 32px; padding: 0 14px; border-radius: 0; cursor: pointer; font-size: 13px; font-weight: 700; }
.pd-primary:hover { opacity: .88; }
.pd-allday { display: flex; gap: 10px; align-items: flex-start; padding: 8px 0; border-top: 1px solid var(--border-line); border-bottom: 1px solid var(--border-line); }
.pd-allday-label { flex: 0 0 52px; font-size: 12px; font-weight: 700; color: var(--text-secondary); padding-top: 6px; }
.pd-allday-list { flex: 1; display: flex; flex-wrap: wrap; gap: 6px; min-width: 0; }
.pd-chip { display: inline-flex; align-items: center; gap: 6px; max-width: 100%; padding: 4px 8px; background: var(--ev-tint); border-left: 3px solid var(--ev-color); font-size: 13px; cursor: pointer; color: var(--text-primary); }
.pd-chip.done { opacity: .45; }
.pd-chip.done .pd-chip-title { text-decoration: line-through; }
.pd-chip-title { font-weight: 700; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.pd-chip-meta { font-size: 11px; color: var(--text-secondary); white-space: nowrap; }
.pd-empty { margin: 0; font-size: 13px; color: var(--text-secondary); }
.pd-check { flex: 0 0 auto; width: 14px; height: 14px; padding: 0; border: 1.5px solid var(--ev-color); background: var(--paper-card); border-radius: 0; cursor: pointer; }
.pd-check.on { background: var(--ev-color); }
.pd-check:disabled { cursor: default; }
.pd-del { margin-left: auto; flex: 0 0 auto; background: none; border: none; color: var(--text-secondary); cursor: pointer; font-size: 12px; padding: 0 2px; opacity: .6; }
.pd-del:hover { color: var(--danger, #d33); opacity: 1; }

.pd-timeline { position: relative; border-top: 1px solid var(--border-line); }
.pd-hour { position: absolute; left: 0; right: 0; border-bottom: 1px solid var(--border-line); cursor: pointer; }
.pd-hour:hover { background: var(--bg-sub); }
.pd-hour-label { position: absolute; left: 0; top: 2px; width: 52px; font: 600 11px monospace; color: var(--text-secondary); }
.pd-events { position: absolute; top: 0; bottom: 0; left: 58px; right: 0; pointer-events: none; }
.pd-event { position: absolute; pointer-events: auto; overflow: hidden; padding: 4px 6px; /* 底下垫一层纸色，时间线的横线不会透出来 */ background: linear-gradient(var(--ev-tint), var(--ev-tint)), var(--paper-card); border: 1px solid var(--ev-color); border-left-width: 4px; color: var(--text-primary); cursor: pointer; display: flex; flex-direction: column; gap: 2px; box-sizing: border-box; }
.pd-event:hover, .pd-event:focus-visible { box-shadow: 0 2px 10px rgba(0, 0, 0, .12); z-index: 2; }
.pd-event.done { opacity: .45; }
.pd-event.done .pd-event-title { text-decoration: line-through; }
.pd-event-top { display: flex; align-items: center; gap: 6px; min-width: 0; }
.pd-event-time { flex: 0 0 auto; font: 600 11px monospace; color: var(--text-secondary); }
.pd-event-title { flex: 1 1 auto; min-width: 0; font-size: 13px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.pd-event-meta { margin: 0; display: flex; flex-wrap: wrap; gap: 8px; font-size: 11px; color: var(--text-secondary); }
.pd-event-memo { margin: 0; font-size: 12px; color: var(--text-secondary); white-space: pre-wrap; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; }
.pd-event.short .pd-event-meta, .pd-event.medium .pd-event-memo { display: none; }
.pd-now { position: absolute; left: 52px; right: 0; height: 0; border-top: 2px solid var(--danger, #e03131); z-index: 3; pointer-events: none; }
.pd-now i { position: absolute; left: -4px; top: -5px; width: 8px; height: 8px; background: var(--danger, #e03131); }

/* 番茄钟联动：「专注」小按钮、专注了几分钟、这一天的专注记录 */
.pd-focus { flex: 0 0 auto; margin-left: auto; padding: 0 6px; min-height: 20px; border: 1px solid var(--ev-color, var(--accent-color)); background: var(--paper-card); color: var(--text-primary); font-size: 11px; font-weight: 700; border-radius: 0; cursor: pointer; line-height: 18px; }
.pd-focus:hover, .pd-focus:focus-visible { background: var(--ev-color, var(--accent-color)); color: var(--paper-card); }
.pd-focus + .pd-del { margin-left: 0; }
.pd-focus-min { flex: 0 0 auto; font: 600 11px var(--font-mono, monospace); color: var(--text-secondary); white-space: nowrap; }
.pd-chip .pd-focus-min { margin-left: 2px; }
.pd-focus-log { border: 1px solid var(--border-line); background: var(--bg-sub); padding: 8px 12px; }
.pd-fl-head { margin: 0 0 6px; display: flex; align-items: baseline; flex-wrap: wrap; gap: 4px 10px; font-size: 13px; font-weight: 800; color: var(--text-primary); }
.pd-fl-head small { font-size: 12px; font-weight: 600; color: var(--text-secondary); }
.pd-fl-link { margin-left: auto; font-size: 12px; font-weight: 700; color: var(--accent-color); }
.pd-fl-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; max-height: 180px; overflow-y: auto; }
.pd-fl-item { display: flex; align-items: center; gap: 10px; padding: 4px 0; font-size: 13px; border-top: 1px dashed var(--border-line); min-width: 0; }
.pd-fl-item:first-child { border-top: none; }
.pd-fl-time { flex: 0 0 auto; font: 600 12px var(--font-mono, monospace); color: var(--text-secondary); }
.pd-fl-task { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.pd-fl-min { flex: 0 0 auto; font: 600 12px var(--font-mono, monospace); }
.pd-fl-tag { flex: 0 0 auto; font-size: 11px; font-weight: 700; padding: 0 6px; color: var(--success, #267453); background: var(--success-soft, #e5f2e9); }
.pd-fl-item.abandoned .pd-fl-task { color: var(--text-secondary); }
.pd-fl-item.abandoned .pd-fl-tag { color: var(--text-secondary); background: var(--paper-card); }
@media (max-width: 640px) {
  .pd-date { font-size: 16px; }
  .pd-allday { flex-direction: column; gap: 4px; }
  .pd-allday-label { flex: none; padding-top: 0; }
  /* 并排的窄卡片放不下时间，只留标题（点开能看全部） */
  .pd-event.narrow .pd-event-time { display: none; }
  .pd-event.narrow .pd-focus-min { display: none; }
  .pd-hour-label { width: 40px; }
  .pd-events { left: 44px; }
  .pd-now { left: 40px; }
}
</style>
