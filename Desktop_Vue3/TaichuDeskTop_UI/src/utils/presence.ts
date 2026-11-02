// 在线心跳：页面可见时每分钟告诉后端“我还在”，用于管理后台的在线人数与游客统计。
// 游客用一个随机生成的匿名编号区分（只存在本浏览器里，不含任何个人信息）。
import request from './request'

const KEY = 'taichu-visitor-id'
const INTERVAL = 60_000
let timer = 0
let lastPing = 0
let fallbackId = ''
let lastSection = ''

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

// 正在看哪个板块：按地址的第一段算（只告诉后端板块名，不发具体页面）；社区成员页显示「在聊天室」之类
const SECTION_OF: Record<string, string> = {
  '': 'home', chat: 'chat', games: 'games', gallery: 'gallery', stream: 'stream', posts: 'stream', knowledge: 'knowledge', codex: 'knowledge',
  world: 'world', novels: 'world', history: 'history', members: 'members', trade: 'trade', lingmai: 'lingmai',
  community: 'chai', ocs: 'chai', joint: 'chai', battles: 'chai', works: 'chai', activity: 'activity', awards: 'honor', honor: 'honor',
  user: 'profile', changelog: 'changelog', 'dev-progress': 'dev', 'must-eat': 'musteat', sponsors: 'sponsors', write: 'write',
  club: 'club', taichu: 'club', toolbox: 'tools', rules: 'rules',
}
export const SECTION_LABELS: Record<string, string> = {
  home: '在首页', chat: '在聊天室', games: '在太虚游', gallery: '在逛画廊', stream: '在逛论坛', knowledge: '在查知识库', world: '在世界观',
  history: '在翻岁月史书', members: '在看社区成员', trade: '在交易行', lingmai: '在灵脉写东西', chai: '在柴圈', activity: '在活动中心',
  honor: '在荣誉墙', profile: '在看个人主页', changelog: '在看版本更新', dev: '在看开发进度', musteat: '在看必吃榜', sponsors: '在看赞助墙',
  write: '在写东西', club: '在太初俱乐部', tools: '在工具箱', rules: '在看社区规则',
}
function currentSection(): string {
  const raw = location.hash.startsWith('#/') ? location.hash.slice(1) : location.pathname
  const first = (raw.split(/[?#]/)[0].split('/')[1] ?? '').toLowerCase()
  return SECTION_OF[first] ?? '' // 管理面板等不在表里的不告诉别人
}

/** 立即发送一次心跳（距上次不足 15 秒时跳过）。换页时调用，「正在看的板块」会跟着变。 */
export function pingPresence(force = false) {
  if (typeof document !== 'undefined' && document.hidden) return
  const now = Date.now()
  const section = currentSection()
  // 换了板块就马上告诉后端，同一板块里 15 秒内不重复发送
  if (!force && now - lastPing < 15_000 && section === lastSection) return
  lastPing = now
  lastSection = section
  const id = visitorId()
  void request.post('/Presence/ping', { visitorId: id, section }, { silentError: true, headers: { 'X-Visitor-Id': id } }).catch(() => undefined)
}

/** 在应用启动时调用一次。 */
export function startPresence() {
  if (timer) return
  pingPresence(true)
  timer = window.setInterval(() => pingPresence(true), INTERVAL)
  document.addEventListener('visibilitychange', () => { if (!document.hidden) pingPresence() })
}
