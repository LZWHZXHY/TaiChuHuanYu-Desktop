// 番茄钟网页版的「同步」部分：和服务器（以及桌面程序）保持同一份计时状态，专注记录上传到账号。
// · 本地操作先改界面（乐观更新），再带着版本号 PUT；服务器说「另一端刚改过」（409）就照服务器的来；
// · 页面开着时由页面定时调用 pull() 拉最新状态，版本号比本地新就采用服务器的；
// · 专注记录先存一份到本地「待上传」队列，传成功再删，断网也不会丢；按 clientId 去重，两端重复传没关系；
// · 网页和服务器的时钟可能差几秒，用接口返回的 serverNowMs 校正，倒计时和桌面对得上。
import { ref } from 'vue'
import { pomodoroApi, type PomoSession, type PomoSettings, type PomoState, type PomoStateResponse } from '@/api/pomodoro'
import { useUserStore } from '@/stores/user'
import { dayStartMs, idleState, normalizeSettings, normalizeState, shiftDayStart } from './pomodoroModel'

const SETTINGS_KEY = 'taichu.pomo.settings'
const PENDING_KEY = 'taichu.pomo.pending'

export interface PomodoroSyncHooks {
  /** 采用了另一端改过的状态（prev = 采用前本地的状态），页面据此判断「是不是桌面到点结束了」 */
  onRemoteChange?: (prev: PomoState, next: PomoState) => void
  /** PUT 冲突，已经改成服务器的状态 */
  onConflict?: () => void
}

export function usePomodoroSync(hooks: PomodoroSyncHooks = {}) {
  const userStore = useUserStore()
  // 待上传队列按账号分开存，同一台电脑换了账号也不会传错人
  const pendingKey = () => `${PENDING_KEY}.${String(userStore.userId || 'anon')}`

  const readJson = <T>(key: string, fallback: T): T => {
    try {
      const raw = localStorage.getItem(key)
      return raw ? (JSON.parse(raw) as T) : fallback
    } catch { return fallback }
  }
  const writeJson = (key: string, value: unknown) => {
    try { localStorage.setItem(key, JSON.stringify(value)) } catch { /* 存不了就只在这次打开里有效 */ }
  }

  const state = ref<PomoState>(idleState())
  /** 服务器上还没有设置时，先用这台浏览器上次的设置（再没有就用默认 25/5） */
  const settings = ref<PomoSettings>(normalizeSettings(readJson<Partial<PomoSettings> | null>(SETTINGS_KEY, null)))
  const version = ref(0)
  const desktopOnline = ref(false)
  /** 第一次拉状态完成（成功或失败）之前不显示计时器，免得先闪一下「待命」 */
  const loaded = ref(false)
  /** 显示给用户看的同步问题（空 = 正常） */
  const syncError = ref('')
  /** 服务器时间 - 本机时间（毫秒） */
  const skew = ref(0)
  const now = () => Date.now() + skew.value

  /** 正在路上的 PUT 个数：有的话先不采用 GET 的结果（PUT 回来会带最新的） */
  let inflight = 0
  /** 上一次 PUT 因为网络没成功，下次拉状态时重试 */
  let dirty = false
  let dirtySettings: PomoSettings | null = null
  /** 每次冲突 +1：冲突前排队的修改是基于旧状态的，不能再发出去覆盖另一端 */
  let epoch = 0
  let chain: Promise<void> = Promise.resolve()

  function setSettings(s: PomoSettings) {
    settings.value = normalizeSettings(s)
    writeJson(SETTINGS_KEY, settings.value)
  }

  function adopt(res: PomoStateResponse, remote: boolean) {
    const prev = state.value
    state.value = normalizeState(res.state)
    if (res.settings) setSettings(res.settings)
    version.value = Number(res.version) || 0
    desktopOnline.value = !!res.desktopOnline
    if (remote) hooks.onRemoteChange?.(prev, state.value)
  }

  /** 拉一次服务器的状态 */
  let pulling = false
  async function pull() {
    if (inflight > 0 || pulling) return
    pulling = true
    const t0 = Date.now()
    try {
      const res = await pomodoroApi.getState('web')
      const t1 = Date.now()
      // 来回很快时才用来校正时钟（慢的话误差大）
      if (t1 - t0 < 2000 && Number.isFinite(res.serverNowMs)) skew.value = res.serverNowMs - (t0 + t1) / 2
      desktopOnline.value = !!res.desktopOnline
      syncError.value = ''
      if (inflight > 0) return
      if (!loaded.value) {
        adopt(res, false)
      } else if (dirty) {
        if (res.version === version.value) {
          // 服务器上没人动过：把刚才没传上去的再传一次
          dirty = false
          push(state.value, dirtySettings)
          dirtySettings = null
        } else {
          dirty = false
          dirtySettings = null
          adopt(res, true)
          hooks.onConflict?.()
        }
      } else if (res.version > version.value) {
        adopt(res, true)
      }
    } catch {
      syncError.value = '暂时连不上服务器，计时照常进行，恢复后会自动同步'
    } finally {
      pulling = false
      loaded.value = true
    }
  }

  function push(s: PomoState, newSettings: PomoSettings | null) {
    inflight++
    const myEpoch = epoch
    chain = chain.then(async () => {
      try {
        if (myEpoch !== epoch) return // 冲突之前排的队，作废
        const res = await pomodoroApi.putState({ baseVersion: version.value, state: s, settings: newSettings, source: 'web' })
        syncError.value = ''
        if (res.conflict) {
          epoch++
          dirty = false
          dirtySettings = null
          adopt(res, true)
          hooks.onConflict?.()
          return
        }
        version.value = Number(res.version) || version.value
        desktopOnline.value = !!res.desktopOnline
        // 后面没有排队的修改时，用服务器回来的那份（时间戳是服务器的）
        if (inflight === 1) state.value = normalizeState(res.state)
        if (res.settings) setSettings(res.settings)
      } catch {
        dirty = true
        dirtySettings = newSettings ?? dirtySettings
        syncError.value = '刚才的操作没同步上，网络恢复后会自动重试'
      } finally {
        inflight--
      }
    })
  }

  /** 本地操作：先改界面，再告诉服务器 */
  function commit(next: PomoState, newSettings: PomoSettings | null = null) {
    state.value = { ...next, source: 'web', updatedMs: now() }
    if (newSettings) setSettings(newSettings)
    push(state.value, newSettings ? settings.value : null)
  }

  // ---------------- 专注记录 ----------------

  const sessions = ref<PomoSession[]>([])
  const sessionsError = ref('')

  const readPending = () => {
    const list = readJson<PomoSession[]>(pendingKey(), [])
    return Array.isArray(list) ? list.filter(s => s && typeof s.clientId === 'string') : []
  }
  const writePending = (list: PomoSession[]) => writeJson(pendingKey(), list.slice(-500))

  const sortSessions = (list: PomoSession[]) => [...list].sort((a, b) => a.startMs - b.startMs)
  const upsertLocal = (s: PomoSession) => {
    sessions.value = sortSessions([...sessions.value.filter(x => x.clientId !== s.clientId), s])
  }

  /** 最近 8 天（今天 + 前 7 天）的记录，做统计用；还没传上去的也算进来 */
  async function loadSessions() {
    const from = shiftDayStart(dayStartMs(now()), -7)
    try {
      const list = await pomodoroApi.listSessions(from, now() + 86_400_000)
      const have = new Set(list.map(s => s.clientId))
      const extra = readPending().filter(s => !have.has(s.clientId) && s.startMs >= from)
      sessions.value = sortSessions([...list, ...extra])
      sessionsError.value = ''
    } catch {
      sessionsError.value = '记录没加载出来，稍后会再试'
    }
  }

  let flushing = false
  /** 把待上传的记录传上去（最多一次 200 条） */
  async function flushPending() {
    if (flushing) return
    const pending = readPending()
    if (!pending.length) return
    flushing = true
    try {
      const res = await pomodoroApi.postSessions(pending.slice(0, 200))
      const ok = new Set(res.clientIds || [])
      writePending(readPending().filter(s => !ok.has(s.clientId)))
    } catch { /* 下次再传 */ } finally {
      flushing = false
    }
  }

  /** 记一条（完成 / 放弃） */
  async function recordSession(s: PomoSession) {
    upsertLocal(s)
    writePending([...readPending().filter(x => x.clientId !== s.clientId), s])
    await flushPending()
  }

  /** 删一条：先从界面去掉，失败再放回来 */
  async function removeSession(clientId: string) {
    const old = sessions.value.find(s => s.clientId === clientId)
    const oldPending = readPending()
    sessions.value = sessions.value.filter(s => s.clientId !== clientId)
    // 还没传上去的也从待上传里拿掉，免得删了又被传上去
    writePending(oldPending.filter(s => s.clientId !== clientId))
    try {
      await pomodoroApi.removeSession(clientId)
    } catch (e) {
      if (old) upsertLocal(old)
      writePending(oldPending)
      throw e
    }
  }

  return {
    state, settings, version, desktopOnline, loaded, syncError, skew, now,
    pull, commit, setSettings,
    sessions, sessionsError, loadSessions, recordSession, removeSession, flushPending,
  }
}
