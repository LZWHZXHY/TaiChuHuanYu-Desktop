<template>
  <div class="pomo-root" :class="`phase-${state.phase || 'idle'}`">
    <header class="pomo-head">
      <div class="pomo-title">
        <span class="pomo-eyebrow">LINGMAI · POMODORO</span>
        <h2>番茄钟</h2>
        <p>专注一段、休息一下。登录同一个账号，网页和桌面程序显示的是同一个倒计时。</p>
      </div>
      <div class="pomo-tools">
        <button
          v-if="notifyPerm !== 'unsupported'"
          type="button"
          class="pomo-btn"
          :class="{ on: notifyPerm === 'granted' }"
          :disabled="notifyPerm === 'granted'"
          :title="notifyPerm === 'denied' ? '浏览器禁止了通知：在地址栏左边的网站设置里把「通知」改成允许' : ''"
          @click="enableNotify"
        >
          {{ notifyPerm === 'granted' ? '✓ 到点通知已开启' : notifyPerm === 'denied' ? '浏览器通知被禁止了' : '🔔 开启到点通知' }}
        </button>
        <button type="button" class="pomo-btn" :class="{ on: showSettings }" :aria-expanded="showSettings" @click="showSettings = !showSettings">⚙ 设置</button>
      </div>
    </header>

    <!-- 同步状态：一句话说清楚到点由谁提醒 -->
    <p class="pomo-sync" :class="syncTone" role="status">
      <i aria-hidden="true"></i>{{ syncText }}
    </p>

    <!-- 到点横幅 -->
    <div v-if="banner" class="pomo-banner" role="alert">
      <div class="pomo-banner-text">
        <strong>{{ bannerTitle }}</strong>
        <span>{{ bannerText }}</span>
      </div>
      <div class="pomo-banner-actions">
        <template v-if="state.phase === ''">
          <button type="button" class="pomo-btn primary" @click="startNext()">开始{{ PHASE_LABEL[nextPhase] }} {{ phaseMinutes(settings, nextPhase) }} 分钟</button>
          <button type="button" class="pomo-btn" @click="banner = null">先停一下</button>
        </template>
        <template v-else>
          <button type="button" class="pomo-btn primary" @click="banner = null">知道了</button>
          <button type="button" class="pomo-btn" @click="stopAutoStarted">先停一下</button>
        </template>
      </div>
    </div>

    <div v-if="plannerHintTitle" class="pomo-hint" role="status">
      <span>📅 已填好生活管理里的「{{ plannerHintTitle }}」，{{ state.phase === '' ? '点开始就开始专注。' : '这一段结束后，点「开始专注」就会专注它。' }}</span>
      <button type="button" class="pomo-x" aria-label="关闭提示" @click="plannerHintTitle = ''">✕</button>
    </div>

    <section v-if="showSettings" class="pomo-panel">
      <PomodoroSettingsPanel :settings="settings" @update="updateSettings" />
    </section>

    <div class="pomo-main">
      <!-- 计时器 -->
      <section class="pomo-card" :class="{ paused: isPausedNow, loading: !loaded }" aria-label="计时器">
        <span class="pomo-corner tl" aria-hidden="true"></span>
        <span class="pomo-corner br" aria-hidden="true"></span>

        <div class="pomo-card-top">
          <span class="pomo-phase-tag">{{ phaseText }}</span>
          <span class="pomo-plate">{{ plateText }}</span>
        </div>

        <div class="pomo-clock" :aria-label="`剩余 ${clockText}`">{{ loaded ? clockText : '--:--' }}</div>

        <div class="pomo-progress" aria-hidden="true">
          <i :style="{ width: progress * 100 + '%' }"></i>
        </div>

        <div class="pomo-rounds">
          <span v-if="settings.longEvery > 0" class="pomo-squares" aria-hidden="true">
            <i v-for="i in settings.longEvery" :key="i" :class="{ on: i <= filledRounds, now: state.phase === 'focus' && i === filledRounds + 1 }"></i>
          </span>
          <span class="pomo-rounds-text">{{ roundsText }}</span>
        </div>

        <!-- 在做什么 + 关联生活管理 -->
        <div class="pomo-task">
          <label class="pomo-task-field">
            <span>在做什么</span>
            <input
              ref="taskInputEl"
              v-model="taskInput"
              type="text"
              :maxlength="MAX_TASK"
              placeholder="比如：写周报、背 50 个单词（可以不填）"
              @change="onTaskChange"
              @keydown.enter="($event.target as HTMLInputElement).blur()"
            />
          </label>
          <label v-if="plannerOk" class="pomo-task-field link">
            <span>关联生活管理</span>
            <select :value="linkKey" @change="onPickPlanner(($event.target as HTMLSelectElement).value)">
              <option value="">不关联</option>
              <option v-if="link && !linkInOptions" :value="linkKey">已关联：{{ link.title || '（事项）' }}</option>
              <option v-for="o in plannerOptions" :key="o.key" :value="o.key">{{ o.time }} · {{ o.title }}{{ o.done ? '（已完成）' : '' }}</option>
            </select>
          </label>
          <p v-else-if="link" class="pomo-link-note">
            📅 关联：{{ link.title || '生活管理里的事项' }}<template v-if="link.date">（{{ link.date }}）</template>
            <button type="button" class="pomo-link-x" @click="onPickPlanner('')">取消关联</button>
          </p>
        </div>

        <!-- 主按钮 -->
        <div class="pomo-controls">
          <template v-if="state.phase === ''">
            <button type="button" class="pomo-main-btn" :disabled="!loaded" @click="startNext()">
              开始{{ PHASE_LABEL[nextPhase] }} {{ phaseMinutes(settings, nextPhase) }} 分钟
            </button>
            <button v-if="nextPhase !== 'focus'" type="button" class="pomo-btn" :disabled="!loaded" @click="startNext('focus')">跳过休息，直接专注</button>
          </template>
          <template v-else>
            <button v-if="isPausedNow" type="button" class="pomo-main-btn" @click="resumeIt">继续</button>
            <button v-else type="button" class="pomo-main-btn" @click="pauseIt">暂停</button>
            <button type="button" class="pomo-btn" @click="finishEarly">{{ state.phase === 'focus' ? '提前完成' : '结束休息' }}</button>
            <button v-if="state.phase === 'focus'" type="button" class="pomo-btn danger" @click="giveUp">放弃</button>
          </template>
        </div>

        <div class="pomo-presets" role="group" aria-label="预设时长">
          <span class="pomo-presets-label">预设</span>
          <button
            v-for="p in PRESETS"
            :key="p.focus"
            type="button"
            class="pomo-preset"
            :class="{ active: settings.focusMin === p.focus && settings.shortMin === p.short }"
            :aria-pressed="settings.focusMin === p.focus && settings.shortMin === p.short"
            @click="applyPreset(p.focus, p.short)"
          >{{ p.focus }} / {{ p.short }}</button>
        </div>
      </section>

      <aside class="pomo-side">
        <PomodoroStats :sessions="sessions" :now="nowMs" :error="sessionsError" @remove="removeRecord" />
      </aside>
    </div>

    <transition name="pomo-fade">
      <div v-if="toast" class="pomo-toast" role="status">{{ toast }}</div>
    </transition>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import type { PomoPhase, PomoSettings, PomoState } from '@/api/pomodoro'
import { plannerApi } from '@/api/planner'
import { enablePlannerBrowserNotify, plannerNotifyPermission, refreshPlannerNotifyPermission } from '@/composables/usePlannerReminders'
import { expandRange, isValidDate, timeLabel, todayStr } from '../planner/plannerModel'
import PomodoroStats from './PomodoroStats.vue'
import PomodoroSettingsPanel from './PomodoroSettingsPanel.vue'
import { usePomodoroSync } from './usePomodoroSync'
import {
  MAX_TASK, PHASE_LABEL, PRESETS, abandon, finishPhase, fmtClock, fmtMinutes, isPaused, isRunning, pause, phaseMinutes,
  playChime, remainingMs, resume, startPhase, unlockAudio, type PlannerLink,
} from './pomodoroModel'

/** 桌面程序在线时，到点后先等它这么久（它会结束这一段并上传记录），还没动静网页再自己结束 */
const DESKTOP_GRACE_MS = 15_000
/** 到点后过了这么久才发现（比如电脑睡眠了），就不再响铃 / 弹通知，只显示横幅 */
const LATE_SILENT_MS = 10 * 60_000

const route = useRoute()
const notifyPerm = computed(() => plannerNotifyPermission.value)

const sync = usePomodoroSync({
  onRemoteChange: (prev, next) => onRemote(prev, next),
  onConflict: () => showToast('另一端刚改过，已同步'),
})
const { state, settings, desktopOnline, loaded, syncError, sessions, sessionsError } = sync

const nowMs = ref(sync.now())
const showSettings = ref(false)
/** 横幅：刚结束的是哪一段、结束后是第几轮、下一段是不是已经自动开始了、是不是桌面结束的 */
const banner = ref<{ ended: PomoPhase; round: number; autoStarted: boolean; remote: boolean } | null>(null)

// ---------------- 显示 ----------------

const isPausedNow = computed(() => isPaused(state.value))
const nextPhase = computed<Exclude<PomoPhase, ''>>(() => state.value.next || 'focus')
const leftMs = computed(() =>
  state.value.phase === '' ? phaseMinutes(settings.value, nextPhase.value) * 60_000 : remainingMs(state.value, nowMs.value))
const clockText = computed(() => fmtClock(leftMs.value))
const progress = computed(() => {
  const s = state.value
  if (s.phase === '' || s.totalMs <= 0) return 0
  return Math.min(1, Math.max(0, 1 - leftMs.value / s.totalMs))
})
const phaseText = computed(() => {
  const s = state.value
  if (s.phase === '') return `待命 · 下一段：${PHASE_LABEL[nextPhase.value]}`
  return isPausedNow.value ? `已暂停 · ${PHASE_LABEL[s.phase]}` : PHASE_LABEL[s.phase]
})
/** 右上角的小铭牌 */
const plateText = computed(() => {
  const s = state.value
  const code = s.phase === 'focus' ? 'FOCUS' : s.phase === 'short' ? 'BREAK·S' : s.phase === 'long' ? 'BREAK·L' : 'STANDBY'
  return `${code} / ${String(s.round).padStart(2, '0')}`
})
/** 这一组亮几个小方块 */
const filledRounds = computed(() => {
  const n = settings.value.longEvery
  const r = state.value.round
  if (n <= 0 || r <= 0) return 0
  return ((r - 1) % n) + 1
})
const roundsText = computed(() => {
  const r = state.value.round
  const n = settings.value.longEvery
  const doing = state.value.phase === 'focus' ? `，正在第 ${r + 1} 轮` : ''
  return n > 0 ? `这一组已完成 ${r} 轮${doing}（每 ${n} 轮长休息一次）` : `已完成 ${r} 轮${doing}`
})

const syncTone = computed(() => (syncError.value ? 'warn' : desktopOnline.value ? 'desk' : 'web'))
const waitingDesktop = ref(false)
const syncText = computed(() => {
  if (syncError.value) return syncError.value
  if (waitingDesktop.value) return '已经到点，正在等桌面程序结束这一段…'
  return desktopOnline.value
    ? '桌面程序在线，到点由桌面提醒；这里会同步显示'
    : '桌面程序不在线，只在这个网页里计时：到点由这个网页提醒，别关掉它'
})

// ---------------- 小提示 ----------------

const toast = ref('')
let toastTimer: ReturnType<typeof setTimeout> | null = null
function showToast(text: string, ms = 3000) {
  if (toastTimer) clearTimeout(toastTimer)
  toast.value = text
  toastTimer = setTimeout(() => { toast.value = '' }, ms)
}

// ---------------- 在做什么 / 关联生活管理 ----------------

const taskInput = ref('')
const taskInputEl = ref<HTMLInputElement | null>(null)
const link = ref<PlannerLink | null>(null)
/** 从生活管理点「专注」带过来的，在开始专注之前不被同步来的状态覆盖 */
let draftFromQuery = false
const plannerHintTitle = ref('')

interface PlannerOption { key: string; eventId: string; date: string; title: string; time: string; done: boolean }
const plannerOk = ref(false)
const plannerOptions = ref<PlannerOption[]>([])
const linkKey = computed(() => (link.value ? `${link.value.eventId}@${link.value.date || ''}` : ''))
const linkInOptions = computed(() => plannerOptions.value.some(o => o.key === linkKey.value))

const cutTask = (s: string) => s.trim().slice(0, MAX_TASK)

/** 任务框、关联事项跟着计时状态走（用户正在输入时不打扰） */
function syncTaskFromState() {
  if (draftFromQuery) return
  if (typeof document !== 'undefined' && document.activeElement === taskInputEl.value) return
  const s = state.value
  taskInput.value = s.task
  if (s.plannerEventId) {
    const key = `${s.plannerEventId}@${s.plannerDate || ''}`
    const title = plannerOptions.value.find(o => o.key === key)?.title || s.task
    link.value = { eventId: s.plannerEventId, date: s.plannerDate, title }
  } else {
    link.value = null
  }
}

/** 计时中改了任务：同步给另一端（待命时只是先填着，点开始才算） */
function onTaskChange() {
  taskInput.value = cutTask(taskInput.value)
  const s = state.value
  if (s.phase !== '' && s.task !== taskInput.value) sync.commit({ ...s, task: taskInput.value })
}

function onPickPlanner(key: string) {
  const o = plannerOptions.value.find(x => x.key === key)
  if (!key || (!o && key !== linkKey.value)) {
    link.value = null
  } else if (o) {
    link.value = { eventId: o.eventId, date: o.date, title: o.title }
    taskInput.value = cutTask(o.title)
  }
  const s = state.value
  if (s.phase !== '') {
    sync.commit({ ...s, task: taskInput.value, plannerEventId: link.value?.eventId ?? null, plannerDate: link.value?.date ?? null })
  }
}

/** 今天的生活管理事项（有生活管理权限才显示下拉） */
async function loadPlanner() {
  try {
    const access = await plannerApi.access()
    if (!access.canUse) return
    const day = todayStr()
    const events = await plannerApi.list(day, day)
    const list = expandRange(events, day, day)[day] || []
    plannerOptions.value = list.map(o => ({
      key: `${o.ev.id}@${o.date}`, eventId: o.ev.id, date: o.date, title: o.ev.title, time: timeLabel(o.ev), done: o.done,
    }))
    plannerOk.value = true
    // 已关联的事项拿到标题了，补上
    const l = link.value
    if (l) {
      const hit = plannerOptions.value.find(o => o.key === linkKey.value)
      if (hit) link.value = { ...l, title: hit.title }
    }
  } catch { /* 没权限或者加载失败：不显示下拉，番茄钟照常用 */ }
}

const firstStr = (v: unknown): string =>
  typeof v === 'string' ? v : Array.isArray(v) && typeof v[0] === 'string' ? v[0] : ''

// 从生活管理当天详情点「专注」进来：?event=<id>&date=<YYYY-MM-DD>&title=<标题>，只填好，不自动开始
watch(() => [route.query.event, route.query.date, route.query.title], () => {
  const eventId = firstStr(route.query.event)
  if (!eventId) return
  const date = firstStr(route.query.date)
  const title = cutTask(firstStr(route.query.title))
  link.value = { eventId, date: isValidDate(date) ? date : null, title }
  taskInput.value = title
  draftFromQuery = true
  plannerHintTitle.value = title || '这件事'
}, { immediate: true })

// ---------------- 操作 ----------------

function startNext(phase?: Exclude<PomoPhase, ''>) {
  unlockAudio()
  const p = phase ?? nextPhase.value
  const base: PomoState = { ...state.value }
  if (p === 'focus') {
    base.task = cutTask(taskInput.value)
    base.plannerEventId = link.value?.eventId ?? null
    base.plannerDate = link.value?.date ?? null
    draftFromQuery = false
    plannerHintTitle.value = ''
  }
  banner.value = null
  sync.commit(startPhase(base, p, settings.value, sync.now()))
}

function pauseIt() { sync.commit(pause(state.value, sync.now())) }
function resumeIt() { unlockAudio(); sync.commit(resume(state.value, sync.now())) }

/** 提前完成（专注算一轮）/ 结束休息 */
function finishEarly() {
  const wasFocus = state.value.phase === 'focus'
  const r = finishPhase(state.value, settings.value, sync.now(), true, 'web')
  sync.commit(r.state)
  if (r.session) void sync.recordSession(r.session)
  banner.value = null
  if (wasFocus && r.session) showToast(`提前完成，记了一轮（专注 ${fmtMinutes(r.session.focusSec)}）`)
}

/** 放弃：不算一轮，满 1 分钟的记一条「放弃」 */
function giveUp() {
  const r = abandon(state.value, sync.now(), 'web')
  sync.commit(r.state)
  if (r.session) {
    void sync.recordSession(r.session)
    showToast(`已放弃，这 ${fmtMinutes(r.session.focusSec)}记成了一条「放弃」`)
  } else {
    showToast('已放弃（不到 1 分钟，不记录）')
  }
}

/** 横幅上的「先停一下」：自动开始的下一段先停掉，回到待命 */
function stopAutoStarted() {
  banner.value = null
  if (state.value.phase === '') return
  const r = abandon(state.value, sync.now(), 'web')
  sync.commit(r.state)
  if (r.session) void sync.recordSession(r.session)
}

function updateSettings(s: PomoSettings) {
  sync.commit(state.value, s)
  showToast('设置已保存，桌面程序也会用这个设置')
}
function applyPreset(focus: number, short: number) {
  updateSettings({ ...settings.value, focusMin: focus, shortMin: short })
}

async function removeRecord(clientId: string) {
  try {
    await sync.removeSession(clientId)
    showToast('已删除这条记录')
  } catch {
    showToast('删除失败，请重试')
  }
}

async function enableNotify() {
  const ok = await enablePlannerBrowserNotify()
  showToast(ok ? '到点通知已开启：网页开着时到点会弹系统通知' : '浏览器没有允许通知。可以在地址栏左边的网站设置里把「通知」改成允许。', 5000)
}

// ---------------- 到点 ----------------

const bannerTitle = computed(() => {
  const b = banner.value
  if (!b) return ''
  if (b.ended === 'focus') return `🍅 专注完成！这一组已完成 ${b.round} 轮`
  if (b.ended === 'long') return '长休息结束，新的一组开始了'
  return '短休息结束'
})
const bannerText = computed(() => {
  const b = banner.value
  if (!b) return ''
  const s = state.value
  const who = b.remote ? '桌面程序已经结束了这一段。' : ''
  if (s.phase !== '') return `${who}已自动开始${PHASE_LABEL[s.phase]} ${Math.round(s.totalMs / 60_000)} 分钟。`
  return `${who}接下来：${PHASE_LABEL[nextPhase.value]} ${phaseMinutes(settings.value, nextPhase.value)} 分钟。`
})

function notify(title: string, body: string) {
  try {
    if (typeof Notification !== 'undefined' && Notification.permission === 'granted') {
      const n = new Notification(title, { body, tag: 'taichu-pomodoro' })
      n.onclick = () => { try { window.focus() } catch { /* 忽略 */ } n.close() }
    }
  } catch { /* 有的浏览器不能直接 new Notification，横幅照样显示 */ }
}

/** 一段结束了：显示横幅；网页自己结束的才响铃、弹通知（桌面结束的话桌面已经响过了） */
function announce(ended: PomoPhase, after: PomoState, autoStarted: boolean, remote: boolean, silent: boolean) {
  banner.value = { ended, round: after.round, autoStarted, remote }
  if (remote || silent) return
  playChime()
  notify(bannerTitle.value, bannerText.value)
}

let dueKey = ''
let dueSince = 0
/** 每一小下检查：到点了没有、该谁来结束 */
function checkDue() {
  const s = state.value
  if (!isRunning(s) || nowMs.value < s.endsMs) {
    waitingDesktop.value = false
    return
  }
  const key = `${s.phase}@${s.startedMs}`
  if (key !== dueKey) { dueKey = key; dueSince = Date.now() }
  // 桌面在线：先不动，让桌面结束（它会响铃并上传记录）；等太久还没动静就自己来
  if (desktopOnline.value && Date.now() - dueSince < DESKTOP_GRACE_MS) {
    waitingDesktop.value = true
    return
  }
  waitingDesktop.value = false
  const late = sync.now() - s.endsMs > LATE_SILENT_MS
  const r = finishPhase(s, settings.value, sync.now(), false, 'web')
  sync.commit(r.state)
  if (r.session) void sync.recordSession(r.session)
  announce(r.ended, r.state, r.autoStarted, false, late)
}

let reloadTimer: ReturnType<typeof setTimeout> | null = null
/** 另一端改了状态 */
function onRemote(prev: PomoState, next: PomoState) {
  const sameSegment = prev.phase === next.phase && prev.startedMs === next.startedMs
  // 上一段已经到点、被另一端结束了：横幅照样显示（不响）
  if (!sameSegment && isRunning(prev) && sync.now() >= prev.endsMs - 2000) {
    announce(prev.phase, next, next.phase !== '', true, true)
  }
  // 另一端结束 / 放弃了一段专注：过几秒重新拉记录（等它传上来）
  if (!sameSegment && prev.phase === 'focus') {
    if (reloadTimer) clearTimeout(reloadTimer)
    reloadTimer = setTimeout(() => { void sync.loadSessions() }, 4000)
  }
  syncTaskFromState()
}

// ---------------- 定时器、可见性 ----------------

let ticker: ReturnType<typeof setInterval> | null = null
let lastPull = 0
let lastSessionsLoad = 0
let originalTitle = ''

function updateTitle() {
  const s = state.value
  if (!originalTitle) return
  document.title = s.phase === ''
    ? originalTitle
    : `${isPausedNow.value ? '⏸ ' : ''}${clockText.value} ${PHASE_LABEL[s.phase]} · 番茄钟`
}

function tick() {
  nowMs.value = sync.now()
  if (loaded.value) checkDue()
  // 页面看得见时每 5 秒拉一次；到点等桌面时每 3 秒（看不见也拉，好尽快接手）
  const visible = document.visibilityState === 'visible'
  const interval = waitingDesktop.value ? 3000 : 5000
  if ((visible || waitingDesktop.value) && Date.now() - lastPull >= interval) {
    lastPull = Date.now()
    void sync.pull()
  }
  updateTitle()
}

function refreshNow() {
  lastPull = Date.now()
  void sync.pull()
  void sync.flushPending()
  refreshPlannerNotifyPermission()
  // 记录一分钟内拉过就不重复拉
  if (Date.now() - lastSessionsLoad > 60_000) {
    lastSessionsLoad = Date.now()
    void sync.loadSessions()
  }
}
function onVisible() { if (document.visibilityState === 'visible') refreshNow() }
function onFocus() { refreshNow() }

onMounted(async () => {
  refreshPlannerNotifyPermission()
  originalTitle = document.title
  lastPull = Date.now()
  ticker = setInterval(tick, 250)
  document.addEventListener('visibilitychange', onVisible)
  window.addEventListener('focus', onFocus)
  await sync.pull()
  syncTaskFromState()
  lastSessionsLoad = Date.now()
  void sync.flushPending().then(() => sync.loadSessions())
  void loadPlanner()
})

onUnmounted(() => {
  if (ticker) clearInterval(ticker)
  if (toastTimer) clearTimeout(toastTimer)
  if (reloadTimer) clearTimeout(reloadTimer)
  document.removeEventListener('visibilitychange', onVisible)
  window.removeEventListener('focus', onFocus)
  if (originalTitle) document.title = originalTitle
})
</script>

<style scoped>
.pomo-root {
  --phase: var(--accent-color);
  flex: 1; min-height: 0; overflow-y: auto; padding: 20px clamp(12px, 3vw, 32px) 32px;
  display: flex; flex-direction: column; gap: 14px; color: var(--text-primary); background: var(--paper-card);
}
.pomo-root.phase-short { --phase: var(--accent-mint, #087f70); }
.pomo-root.phase-long { --phase: var(--info, #286390); }

.pomo-head { display: flex; flex-wrap: wrap; align-items: flex-end; justify-content: space-between; gap: 12px; }
.pomo-eyebrow { display: inline-block; padding: 3px 8px; background: var(--inverse-bg, #142733); color: var(--inverse-text, #fff); border-left: 2px solid var(--signal-mint, #28b99c); font: 10px/1.5 var(--font-mono, monospace); letter-spacing: .1em; }
.pomo-title h2 { margin: 6px 0 0; font: 800 clamp(22px, 3vw, 30px)/1.15 var(--font-display, sans-serif); letter-spacing: -.01em; }
.pomo-title p { margin: 4px 0 0; font-size: 13px; color: var(--text-secondary); }
.pomo-tools { display: flex; flex-wrap: wrap; gap: 8px; }

.pomo-btn { display: inline-flex; align-items: center; justify-content: center; gap: 6px; min-height: 34px; padding: 0 14px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); border-radius: 0; cursor: pointer; font-size: 13px; font-weight: 600; white-space: nowrap; font-family: inherit; }
.pomo-btn:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); }
.pomo-btn:disabled { opacity: .6; cursor: default; }
.pomo-btn.on { color: var(--accent-color); border-color: var(--accent-color); background: var(--accent-soft); opacity: 1; }
.pomo-btn.primary { background: var(--accent-color); border-color: var(--accent-color); color: var(--on-accent, var(--paper-card)); }
.pomo-btn.primary:hover:not(:disabled) { opacity: .88; color: var(--on-accent, var(--paper-card)); }
.pomo-btn.danger:hover:not(:disabled) { border-color: var(--danger, #d33); color: var(--danger, #d33); }

.pomo-sync { margin: 0; display: flex; align-items: center; gap: 8px; font-size: 13px; color: var(--text-secondary); }
.pomo-sync i { flex: 0 0 8px; width: 8px; height: 8px; background: var(--text-secondary); }
.pomo-sync.desk i { background: var(--success, #267453); box-shadow: 0 0 0 3px var(--success-soft, #e5f2e9); }
.pomo-sync.web i { background: var(--accent-color); }
.pomo-sync.warn { color: var(--warning, #966015); }
.pomo-sync.warn i { background: var(--warning, #966015); }

.pomo-banner { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 10px 16px; padding: 12px 16px; border: 1px solid var(--phase); border-left-width: 4px; background: color-mix(in srgb, var(--phase) 10%, var(--paper-card)); }
.pomo-banner-text { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
.pomo-banner-text strong { font-size: 15px; }
.pomo-banner-text span { font-size: 13px; color: var(--text-secondary); }
.pomo-banner-actions { display: flex; flex-wrap: wrap; gap: 8px; }

.pomo-hint { display: flex; align-items: center; gap: 10px; padding: 8px 12px; font-size: 13px; background: var(--accent-soft); border-left: 3px solid var(--accent-color); }
.pomo-x { margin-left: auto; background: none; border: none; color: var(--text-secondary); cursor: pointer; }

.pomo-panel { padding: 14px; border: 1px solid var(--border-line); background: var(--bg-sub); }

.pomo-main { display: grid; grid-template-columns: minmax(0, 1.25fr) minmax(0, 1fr); gap: 16px; align-items: start; }

/* 计时器卡片：角标框 + 切角主按钮（莱茵风格的小细节） */
.pomo-card { position: relative; display: flex; flex-direction: column; gap: 14px; padding: 22px clamp(14px, 3vw, 28px); border: 1px solid var(--border-line); background: var(--bg-sub); min-width: 0; }
.pomo-card.loading { opacity: .6; }
.pomo-corner { position: absolute; width: 14px; height: 14px; pointer-events: none; border-color: var(--phase); border-style: solid; border-width: 0; }
.pomo-corner.tl { top: -1px; left: -1px; border-top-width: 2px; border-left-width: 2px; }
.pomo-corner.br { right: -1px; bottom: -1px; border-right-width: 2px; border-bottom-width: 2px; }
.pomo-card-top { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.pomo-phase-tag { display: inline-flex; align-items: center; min-height: 26px; padding: 0 10px; background: var(--phase); color: var(--on-accent, #fff); font-size: 13px; font-weight: 800; letter-spacing: .02em; }
.pomo-card.paused .pomo-phase-tag { background: var(--paper-card); color: var(--phase); border: 1px dashed var(--phase); }
.pomo-root.phase-idle .pomo-phase-tag { background: var(--paper-card); color: var(--text-secondary); border: 1px solid var(--border-line); }
.pomo-plate { font: 600 11px/1 var(--font-mono, monospace); letter-spacing: .1em; color: var(--text-secondary); padding: 5px 8px; border: 1px solid var(--border-line); background: var(--paper-card); }

.pomo-clock { font: 700 clamp(64px, 12vw, 132px)/1 var(--font-display, sans-serif); font-variant-numeric: tabular-nums; letter-spacing: -.02em; text-align: center; color: var(--text-primary); padding: 6px 0 2px; }
.pomo-card.paused .pomo-clock { color: var(--text-secondary); }

/* 进度条：底下画刻度，像仪表 */
.pomo-progress { position: relative; height: 8px; background: var(--paper-card); border: 1px solid var(--border-line); background-image: repeating-linear-gradient(90deg, transparent 0 calc(10% - 1px), var(--border-line) calc(10% - 1px) 10%); }
.pomo-progress i { position: absolute; left: 0; top: 0; bottom: 0; background: var(--phase); transition: width .25s linear; }

.pomo-rounds { display: flex; align-items: center; flex-wrap: wrap; gap: 8px 12px; font-size: 13px; color: var(--text-secondary); }
.pomo-squares { display: inline-flex; gap: 5px; }
.pomo-squares i { width: 14px; height: 14px; border: 1.5px solid var(--phase); background: var(--paper-card); box-sizing: border-box; }
.pomo-squares i.on { background: var(--phase); }
.pomo-squares i.now { background: repeating-linear-gradient(-45deg, var(--phase) 0 2px, transparent 2px 5px); }

.pomo-task { display: grid; grid-template-columns: minmax(0, 1.4fr) minmax(0, 1fr); gap: 10px; }
.pomo-task-field { display: flex; flex-direction: column; gap: 4px; font-size: 12px; font-weight: 700; color: var(--text-secondary); min-width: 0; }
.pomo-task-field input, .pomo-task-field select { min-height: 36px; padding: 0 10px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); border-radius: 0; font-size: 14px; font-family: inherit; width: 100%; box-sizing: border-box; min-width: 0; }
.pomo-task-field input:focus, .pomo-task-field select:focus { outline: none; border-color: var(--accent-color); box-shadow: 0 0 0 2px var(--accent-soft); }
.pomo-link-note { margin: 0; align-self: end; font-size: 13px; color: var(--text-secondary); display: flex; flex-wrap: wrap; align-items: center; gap: 6px; }
.pomo-link-x { background: none; border: none; padding: 0; color: var(--accent-color); cursor: pointer; font-size: 12px; text-decoration: underline; }

.pomo-controls { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; }
/* 主按钮：右上、左下切角 */
.pomo-main-btn {
  min-height: 46px; min-width: 150px; padding: 0 26px; border: none; cursor: pointer; border-radius: 0;
  background: var(--phase); color: var(--on-accent, #fff); font: 800 16px var(--font-display, sans-serif); letter-spacing: .03em;
  clip-path: polygon(0 0, calc(100% - var(--chamfer, 10px)) 0, 100% var(--chamfer, 10px), 100% 100%, var(--chamfer, 10px) 100%, 0 calc(100% - var(--chamfer, 10px)));
}
.pomo-main-btn:hover:not(:disabled) { filter: brightness(1.08); }
.pomo-main-btn:disabled { opacity: .5; cursor: default; }
.pomo-main-btn:focus-visible { outline: 2px solid var(--text-primary); outline-offset: 3px; }

.pomo-presets { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; padding-top: 10px; border-top: 1px dashed var(--border-line); }
.pomo-presets-label { font: 600 11px var(--font-mono, monospace); letter-spacing: .08em; color: var(--text-secondary); margin-right: 4px; }
.pomo-preset { min-height: 30px; padding: 0 12px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); border-radius: 0; cursor: pointer; font: 600 13px var(--font-mono, monospace); }
.pomo-preset:hover { border-color: var(--accent-color); color: var(--accent-color); }
.pomo-preset.active { background: var(--accent-soft); border-color: var(--accent-color); color: var(--accent-color); }

.pomo-side { min-width: 0; }

.pomo-toast { position: fixed; left: 50%; bottom: 28px; transform: translateX(-50%); z-index: 1500; max-width: calc(100vw - 32px); padding: 10px 16px; background: var(--text-primary); color: var(--paper-card); font-size: 13px; box-shadow: 0 8px 24px rgba(0, 0, 0, .2); }
.pomo-fade-enter-active, .pomo-fade-leave-active { transition: opacity .2s; }
.pomo-fade-enter-from, .pomo-fade-leave-to { opacity: 0; }

@media (max-width: 960px) {
  .pomo-main { grid-template-columns: minmax(0, 1fr); }
}
@media (max-width: 640px) {
  .pomo-root { padding: 14px 10px 24px; gap: 12px; }
  .pomo-task { grid-template-columns: minmax(0, 1fr); }
  .pomo-controls .pomo-main-btn { flex: 1 1 100%; }
  .pomo-controls .pomo-btn { flex: 1 1 0; }
  .pomo-banner-actions { width: 100%; }
  .pomo-banner-actions .pomo-btn { flex: 1 1 0; }
}
@media (prefers-reduced-motion: reduce) { .pomo-progress i, .pomo-fade-enter-active, .pomo-fade-leave-active { transition: none; } }
</style>
