// 在线心跳：页面可见时每分钟告诉后端“我还在”，用于管理后台的在线人数与游客统计。
// 游客用一个随机生成的匿名编号区分（只存在本浏览器里，不含任何个人信息）。
import request from './request'

const KEY = 'taichu-visitor-id'
const INTERVAL = 60_000
let timer = 0
let lastPing = 0
let fallbackId = ''

function visitorId(): string {
  try {
    let id = localStorage.getItem(KEY)
    if (!id || !/^[A-Za-z0-9_-]{8,64}$/.test(id)) {
      id = (crypto.randomUUID?.() ?? `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}`).replace(/[^A-Za-z0-9_-]/g, '')
      localStorage.setItem(KEY, id)
    }
    return id
  } catch {
    // 无痕模式等无法存储时，本次打开内保持同一个编号
    return fallbackId ||= `t${Date.now().toString(36)}${Math.random().toString(36).slice(2, 10)}`
  }
}

/** 立即发送一次心跳（距上次不足 15 秒时跳过）。 */
export function pingPresence(force = false) {
  if (typeof document !== 'undefined' && document.hidden) return
  const now = Date.now()
  if (!force && now - lastPing < 15_000) return
  lastPing = now
  const id = visitorId()
  void request.post('/Presence/ping', { visitorId: id }, { silentError: true, headers: { 'X-Visitor-Id': id } }).catch(() => undefined)
}

/** 在应用启动时调用一次。 */
export function startPresence() {
  if (timer) return
  pingPresence(true)
  timer = window.setInterval(() => pingPresence(true), INTERVAL)
  document.addEventListener('visibilitychange', () => { if (!document.hidden) pingPresence() })
}
