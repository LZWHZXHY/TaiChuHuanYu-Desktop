// src/composables/usePlannerReminders.ts
// 生活管理的「浏览器弹窗」提醒：登录状态下每分钟问一次后端「接下来十几分钟要提醒什么」，到点用系统通知弹出来。
// · 只有打开过生活管理、而且能用的人才会去问（别的用户不多发请求）；
// · 浏览器通知权限只在用户点「开启浏览器提醒」时才申请，不会一进网站就弹授权框；
// · 没给权限时退一步，在网页右上角弹一条页面内提示；
// · 同一条只弹一次（记在 localStorage，多开几个标签页也不重复弹）。
import { ref, watch } from 'vue'
import type { Router } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useUserStore } from '@/stores/user'
import { plannerApi, type PlannerUpcoming } from '@/api/planner'
import { pomodoroApi } from '@/api/pomodoro'

const SHOWN_KEY = 'taichu.planner.shown'
/** 打开过生活管理且能用：1；没权限：0（不再轮询） */
export const PLANNER_ACTIVE_KEY = 'taichu.planner.active'
const POLL_MS = 60_000
/** 到点后超过这么久才拿到的就不弹了 */
const LATE_MS = 3 * 60_000

/** 浏览器通知权限：granted 允许 / default 还没问过 / denied 被拒绝 / unsupported 不支持 */
export const plannerNotifyPermission = ref<NotificationPermission | 'unsupported'>(
  typeof Notification === 'undefined' ? 'unsupported' : Notification.permission,
)

export function refreshPlannerNotifyPermission() {
  plannerNotifyPermission.value = typeof Notification === 'undefined' ? 'unsupported' : Notification.permission
}

/** 用户点「开启浏览器提醒」时调用：这时才向浏览器要权限 */
export async function enablePlannerBrowserNotify(): Promise<boolean> {
  if (typeof Notification === 'undefined') return false
  try {
    let permission = Notification.permission
    if (permission === 'default') permission = await Notification.requestPermission()
    plannerNotifyPermission.value = permission
    return permission === 'granted'
  } catch {
    return false
  }
}

/** 生活管理页面告诉这里：这个人能不能用（能用才轮询） */
export function setPlannerReminderActive(active: boolean) {
  try { localStorage.setItem(PLANNER_ACTIVE_KEY, active ? '1' : '0') } catch { /* 存储不可用时只是不轮询 */ }
  if (active) void poll()
}

const isActive = () => {
  try { return localStorage.getItem(PLANNER_ACTIVE_KEY) === '1' } catch { return false }
}

const readShown = (): string[] => {
  try {
    const raw = JSON.parse(localStorage.getItem(SHOWN_KEY) || '[]')
    return Array.isArray(raw) ? raw.filter((x): x is string => typeof x === 'string') : []
  } catch { return [] }
}
const markShown = (key: string) => {
  try {
    const list = readShown().filter(k => k !== key)
    list.push(key)
    localStorage.setItem(SHOWN_KEY, JSON.stringify(list.slice(-200)))
  } catch { /* 存不了就算了，最多同一条在别的标签页再弹一次 */ }
}

let router: Router | null = null
let pollTimer: ReturnType<typeof setInterval> | null = null
const pending = new Map<string, ReturnType<typeof setTimeout>>()
let started = false

// 桌面程序在线时，生活管理的提醒由桌面弹（它也会拉这些提醒），网页就不再弹，免得同一件事响两次。
// 在不在线问番茄钟的接口（顺带返回 desktopOnline），一分钟最多问一次；问不到就当不在线，照常弹。
const DESKTOP_CACHE_MS = 60_000
let desktopCache = { at: 0, online: false }
async function desktopOnline(): Promise<boolean> {
  if (Date.now() - desktopCache.at < DESKTOP_CACHE_MS) return desktopCache.online
  let online = false
  try { online = !!(await pomodoroApi.getState('web')).desktopOnline } catch { online = false }
  desktopCache = { at: Date.now(), online }
  return online
}

function fire(item: PlannerUpcoming, key: string) {
  pending.delete(key)
  if (readShown().includes(key)) return
  // 排上队之后桌面才上线的：到点时再看一眼缓存，桌面在就让桌面弹（不记「已弹过」，桌面下线后还能补弹）
  if (desktopCache.online && Date.now() - desktopCache.at < DESKTOP_CACHE_MS * 2) return
  markShown(key)
  const when = item.allDay || !item.eventAt
    ? '今天'
    : new Date(item.eventAt).toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })
  const body = `${when}${item.location ? ` · ${item.location}` : ''}`
  const open = () => { router?.push({ path: '/lingmai/planner', query: { date: item.date } }).catch(() => {}) }
  try {
    if (typeof Notification !== 'undefined' && Notification.permission === 'granted') {
      const n = new Notification(`日程提醒：${item.title}`, { body, tag: key })
      n.onclick = () => { try { window.focus() } catch { /* 忽略 */ } open(); n.close() }
      return
    }
  } catch { /* 有的浏览器（比如部分手机浏览器）不能直接 new Notification，退回页面内提示 */ }
  ElMessage({ message: `日程提醒：${item.title}（${body}）`, type: 'info', duration: 15000, showClose: true })
}

async function poll() {
  const userStore = useUserStore()
  if (!userStore.isLoggedIn || !isActive()) return
  let list: PlannerUpcoming[]
  try { list = await plannerApi.upcoming() } catch { return }
  // 有要提醒的才去问桌面在不在线（没有提醒时不多发请求）
  if (list.length && await desktopOnline()) return
  const shown = readShown()
  for (const item of list) {
    const at = new Date(item.remindAt).getTime()
    if (!Number.isFinite(at)) continue
    const key = `${item.id}@${item.remindAt}`
    if (shown.includes(key) || pending.has(key)) continue
    const delay = at - Date.now()
    if (delay < -LATE_MS) continue
    pending.set(key, setTimeout(() => fire(item, key), Math.max(0, delay)))
  }
}

function stop() {
  if (pollTimer) clearInterval(pollTimer)
  pollTimer = null
  pending.forEach(t => clearTimeout(t))
  pending.clear()
}

function onVisible() {
  if (document.visibilityState === 'visible') { refreshPlannerNotifyPermission(); void poll() }
}

/** 在 App.vue 里调用一次：登录后开始轮询，退出登录就停 */
export function startPlannerReminders(appRouter: Router) {
  if (started) return
  started = true
  router = appRouter
  const userStore = useUserStore()
  watch(() => userStore.isLoggedIn, loggedIn => {
    stop()
    if (!loggedIn) return
    void poll()
    pollTimer = setInterval(() => { void poll() }, POLL_MS)
  }, { immediate: true })
  document.addEventListener('visibilitychange', onVisible)
}
