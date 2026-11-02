<!-- 联机五子棋：大厅（快速匹配 / 开房 / 房间列表 / 房间号加入）→ 等待房间 → 对局。莱茵生命实验室风格。 -->
<template>
  <div class="gomoku">
    <header class="gm-head">
      <div>
        <p class="eyebrow">GOMOKU · ONLINE MATCH / 联机五子棋</p>
        <h2>五子棋</h2>
      </div>
      <span class="link-state" :class="{ on: connected }"><i aria-hidden="true"></i>{{ connected ? '已连接对战服务器' : '正在连接对战服务器…' }}</span>
    </header>

    <p v-if="notice" class="gm-notice" :class="noticeKind" role="status">{{ notice }}<button type="button" aria-label="关闭提示" @click="notice = ''">×</button></p>

    <!-- ===== 大厅 ===== -->
    <div v-if="view === 'lobby'" class="lobby">
      <section class="panel start-panel">
        <p class="panel-tag">ENTRY / 开始对局</p>
        <button type="button" class="quick" :disabled="!connected || busy" @click="quickMatch">
          <span class="quick-title">快速匹配</span>
          <span class="quick-sub">有人在等就直接对战，没有就开房等人 · 15 路 · 每步 30 秒</span>
        </button>

        <form class="create" @submit.prevent="createRoom">
          <h3>自定义房间</h3>
          <label>房间名称<input v-model.trim="form.name" maxlength="30" :placeholder="defaultRoomName" /></label>
          <label>每步时限
            <select v-model.number="form.timeLimit"><option :value="15">15 秒 · 快棋</option><option :value="30">30 秒</option><option :value="60">60 秒</option><option :value="120">120 秒 · 慢棋</option></select>
          </label>
          <button type="submit" class="btn primary" :disabled="!connected || busy">开房间</button>
        </form>

        <form class="join" @submit.prevent="joinById">
          <h3>输入房间号</h3>
          <div class="join-row">
            <input v-model.trim="joinId" maxlength="16" placeholder="例如 a1b2c3d4" autocomplete="off" />
            <button type="submit" class="btn" :disabled="!connected || busy || !joinId">加入</button>
          </div>
        </form>
      </section>

      <section class="panel rooms-panel">
        <div class="rooms-head">
          <p class="panel-tag">ROOMS / 房间（等人的在前面，正在下的可以观战）</p>
          <button type="button" class="btn small" :disabled="!connected || loadingRooms" @click="loadRooms">{{ loadingRooms ? '刷新中…' : '刷新' }}</button>
        </div>
        <ul v-if="rooms.length" class="rooms">
          <li v-for="r in rooms" :key="r.roomId">
            <div class="room-main">
              <b>{{ r.roomName }}</b>
              <small>{{ r.ownerName }} · {{ r.boardSize }} 路 · 每步 {{ r.timeLimit }} 秒 · {{ r.started ? '对局中' : (r.playerCount ?? 1) >= 2 ? '人齐了，等开局' : '等对手' }} · {{ ago(r.createdAt) }}<template v-if="r.spectators"> · 👁 {{ r.spectators }}</template></small>
            </div>
            <span class="room-btns">
              <button v-if="!r.started && (r.playerCount ?? 1) < 2" type="button" class="btn small primary" :disabled="busy" @click="join(r.roomId)">加入</button>
              <!-- 观战：房主关了或人满了就灰掉，鼠标放上去有原因 -->
              <button type="button" class="btn small" :disabled="busy || watchState(r).disabled" :title="watchState(r).title" @click="watchRoom(r.roomId)">{{ r.allowSpectate === false ? '不让观战' : '👁 观战' }}</button>
            </span>
          </li>
        </ul>
        <div v-else class="rooms-empty">
          <p>{{ loadingRooms ? '读取中…' : '现在没有人在等。' }}</p>
          <small v-if="!loadingRooms">点「快速匹配」开一个房间，其他人进大厅就能看到。</small>
        </div>
        <ul class="rules">
          <li>黑方（房主）先行，横、竖、斜任意方向连成五子获胜。</li>
          <li>超时、认输或中途离开都判负，结果会记入「我的战绩」和常胜 / 常败将军榜。</li>
        </ul>
      </section>
    </div>

    <!-- ===== 房间里的观战条：谁在看（点开名单）、观战的人可以坐下 / 不看了 ===== -->
    <div v-if="room && view !== 'lobby'" class="gm-watchbar">
      <span v-if="spectating" class="watching-tag">观战中 · 只能看不能下</span>
      <WatchBadge :watch="watchInfo" :my-user-id="myId" :can-toggle="!spectating && amOwner && view === 'waiting'" placement="down" @toggle="setSpectate" />
      <span class="bar-space"></span>
      <template v-if="spectating">
        <button v-if="view === 'waiting' && room.players.length < 2" type="button" class="btn small primary" :disabled="busy" @click="join(room.roomId)">坐下</button>
        <button v-if="view === 'waiting'" type="button" class="btn small" :disabled="busy" @click="leave">不看了</button>
      </template>
    </div>

    <!-- ===== 等待房间 ===== -->
    <section v-if="view === 'waiting' && room" class="panel waiting">
      <div class="waiting-head">
        <div>
          <p class="panel-tag">ROOM / 等待开局</p>
          <h3>{{ room.roomName }}</h3>
        </div>
        <button type="button" class="room-id" :title="'复制房间号发给朋友'" @click="copyId">
          <small>房间号</small><b>{{ room.roomId }}</b><span>{{ copied ? '已复制' : '复制' }}</span>
        </button>
      </div>

      <div class="seats">
        <div v-for="seat in seats" :key="seat.color" class="seat" :class="{ filled: !!seat.player, me: seat.player && isMe(seat.player.id) }">
          <span class="stone" :class="seat.color === 1 ? 'black' : 'white'" aria-hidden="true"></span>
          <div>
            <small>{{ seat.color === 1 ? '黑方 · 先手 · 房主' : '白方 · 后手' }}</small>
            <b>{{ seat.player ? seat.player.name : '等待对手加入…' }}<em v-if="seat.player && isMe(seat.player.id)">你</em></b>
          </div>
        </div>
      </div>

      <p class="waiting-meta">{{ room.boardSize }} 路棋盘 · 每步 {{ room.timeLimit }} 秒</p>

      <div v-if="spectating" class="waiting-actions"><span class="waiting-tip">你在观战，等房主开局后会自动开始看棋</span></div>
      <div v-else class="waiting-actions">
        <button type="button" class="btn" :disabled="busy" @click="leave">离开房间</button>
        <button v-if="amOwner" type="button" class="btn primary" :disabled="busy || room.players.length < 2" @click="start">{{ room.players.length < 2 ? '等待对手…' : '开始对局' }}</button>
        <span v-else class="waiting-tip">等待房主开始</span>
      </div>
    </section>

    <!-- ===== 对局 ===== -->
    <GobangMatch
      v-else-if="view === 'playing' && room && startInfo"
      :key="matchKey"
      ref="matchRef"
      :room="room"
      :start="startInfo"
      :my-id="myId"
      :spectating="spectating"
      @rematch="start"
      @leave="leave"
      @error="say($event, 'error')"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, inject, onMounted, onUnmounted, reactive, ref, watch, type Ref } from 'vue'
import { useUserStore } from '@/stores/user'
import GobangMatch from './GobangMatch.vue'
import type { GobangPlayer, GobangRoom, GobangStart } from './gobangTypes'
import WatchBadge from '../WatchBadge.vue'
import { EMPTY_WATCH, watchButton, type WatchInfo } from '../spectate'

interface SignalRApi { connection: Ref<{ invoke: (method: string, ...args: unknown[]) => Promise<unknown> } | null>; isConnected: Ref<boolean> }
interface RoomSummary { roomId: string; roomName: string; ownerName: string; boardSize: number; timeLimit: number; createdAt: string; playerCount?: number; started?: boolean; spectators?: number; allowSpectate?: boolean; maxSpectators?: number }
/** 观战刚进来时服务器给的一份完整局面（GameHub.Snapshot） */
interface WatchSnapshot { roomId: string; roomName: string; boardSize: number; timeLimit: number; ownerId: string; players: GobangPlayer[]; started: boolean; currentTurn?: string | null; board?: number[][] | null; remainingTime?: number; moveCount?: number; watch?: WatchInfo }

const signalR = inject<SignalRApi | null>('signalR', null)
const user = useUserStore()
const myId = computed(() => String(user.userId || ''))
const isMe = (id: string) => id.toLowerCase() === myId.value.toLowerCase()
const connected = computed(() => Boolean(signalR?.isConnected.value))

const view = ref<'lobby' | 'waiting' | 'playing'>('lobby')
const room = ref<GobangRoom | null>(null)
const startInfo = ref<GobangStart | null>(null)
const matchKey = ref(0)
const matchRef = ref<InstanceType<typeof GobangMatch> | null>(null)
const rooms = ref<RoomSummary[]>([])
const loadingRooms = ref(false)
const busy = ref(false)
const joinId = ref('')
const copied = ref(false)
const notice = ref(''), noticeKind = ref<'info' | 'error'>('info')
const form = reactive({ name: '', timeLimit: 30 })
const defaultRoomName = computed(() => `${user.userInfo?.username || '我'} 的房间`)
const amOwner = computed(() => !!room.value && isMe(room.value.ownerId))

// ===== 观战 =====
/** 我是在观战（不是下棋的人） */
const spectating = ref(false)
const watchInfo = ref<WatchInfo>(EMPTY_WATCH)
const watchState = (r: RoomSummary) => watchButton(r, r.maxSpectators ?? 20, user.isLoggedIn)

/** 用服务器给的局面进入观战：在下就直接看棋盘，没开局就看等待房间 */
function applySnapshot(snap: WatchSnapshot) {
  spectating.value = true
  watchInfo.value = snap.watch ?? EMPTY_WATCH
  room.value = { roomId: snap.roomId, roomName: snap.roomName, boardSize: snap.boardSize, timeLimit: snap.timeLimit, ownerId: snap.ownerId, players: snap.players ?? [] }
  if (snap.started) {
    startInfo.value = { currentTurn: String(snap.currentTurn ?? snap.ownerId), startedAt: Date.now(), board: snap.board ?? null, remainingTime: snap.remainingTime, moveCount: snap.moveCount }
    matchKey.value++
    view.value = 'playing'
  } else {
    startInfo.value = null
    view.value = 'waiting'
  }
}
async function watchRoom(roomId: string) {
  busy.value = true
  try {
    const res = await invoke<{ error?: string; snapshot?: WatchSnapshot }>('WatchRoom', roomId)
    if (res?.error) say(res.error, 'error')
    else if (res?.snapshot) applySnapshot(res.snapshot)
  } finally { busy.value = false }
}
async function setSpectate(allow: boolean) { await invoke('SetSpectate', allow) }
function backToLobby(message?: string) {
  room.value = null
  startInfo.value = null
  spectating.value = false
  watchInfo.value = EMPTY_WATCH
  view.value = 'lobby'
  if (message) say(message)
  void loadRooms()
}

const seats = computed(() => {
  const r = room.value
  if (!r) return []
  const owner = r.players.find(p => p.id === r.ownerId) ?? null
  const guest = r.players.find(p => p.id !== r.ownerId) ?? null
  return [{ color: 1, player: owner }, { color: 2, player: guest }] as { color: number; player: GobangPlayer | null }[]
})

let noticeTimer = 0
function say(text: string, kind: 'info' | 'error' = 'info') {
  notice.value = text
  noticeKind.value = kind
  window.clearTimeout(noticeTimer)
  noticeTimer = window.setTimeout(() => { notice.value = '' }, 6000)
}

async function invoke<T = unknown>(method: string, ...args: unknown[]): Promise<T | undefined> {
  const conn = signalR?.connection.value
  if (!conn || !connected.value) { say('还没连上对战服务器，请稍等几秒再试', 'error'); return undefined }
  try { return await conn.invoke(method, ...args) as T }
  catch (e) { say(`操作失败：${(e as Error)?.message || '请稍后再试'}`, 'error'); return undefined }
}

const ago = (iso: string) => {
  const s = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 1000))
  return s < 60 ? '刚刚' : s < 3600 ? `${Math.floor(s / 60)} 分钟前` : `${Math.floor(s / 3600)} 小时前`
}

async function loadRooms() {
  loadingRooms.value = true
  try { rooms.value = (await invoke<RoomSummary[]>('ListRooms')) ?? [] } finally { loadingRooms.value = false }
}

async function quickMatch() {
  busy.value = true
  try { await invoke('QuickMatch') } finally { busy.value = false }
}
async function createRoom() {
  busy.value = true
  try { await invoke('CreateRoom', form.name || defaultRoomName.value, 'classic', 15, form.timeLimit) } finally { busy.value = false }
}
async function join(roomId: string) {
  busy.value = true
  try { await invoke('JoinRoom', roomId) } finally { busy.value = false }
}
const joinById = () => join(joinId.value)
async function start() {
  if (!room.value) return
  busy.value = true
  try { await invoke('StartGame', room.value.roomId) } finally { busy.value = false }
}
async function leave() {
  // 观战的人点「不看了」也走这里：服务器只是把他移出观战名单
  await invoke('LeaveRoom')
  backToLobby()
}
async function copyId() {
  if (!room.value) return
  try { await navigator.clipboard.writeText(room.value.roomId); copied.value = true; window.setTimeout(() => { copied.value = false }, 1500) } catch { say(`房间号：${room.value.roomId}`) }
}

// ===== 服务器事件（由太虚游页面统一转发成 window 事件） =====
type Detail = Record<string, unknown>
const toRoom = (d: Detail): GobangRoom => ({
  roomId: String(d.roomId ?? room.value?.roomId ?? ''),
  roomName: String(d.roomName ?? room.value?.roomName ?? '五子棋房间'),
  boardSize: Number(d.boardSize ?? room.value?.boardSize ?? 15),
  timeLimit: Number(d.timeLimit ?? room.value?.timeLimit ?? 30),
  ownerId: String(d.ownerId ?? room.value?.ownerId ?? ''),
  players: Array.isArray(d.players) ? (d.players as GobangPlayer[]).filter(p => p && typeof p === 'object' && 'id' in p) : room.value?.players ?? [],
})

const handlers: Record<string, (d: Detail) => void> = {
  'room-created': d => { spectating.value = false; watchInfo.value = EMPTY_WATCH; room.value = toRoom(d); view.value = 'waiting' },
  'player-joined': d => {
    room.value = toRoom(d)
    // 观战的人自己坐下了：不再是观战
    if (spectating.value && room.value.players.some(p => isMe(p.id))) spectating.value = false
    if (view.value === 'lobby') view.value = 'waiting'
    else if (view.value === 'waiting' && amOwner.value && room.value.players.length === 2) say('对手已加入，可以开始了')
  },
  'player-left': d => {
    if (!room.value) return
    room.value = { ...room.value, ownerId: String(d.ownerId ?? room.value.ownerId), players: Array.isArray(d.players) ? d.players as GobangPlayer[] : room.value.players.filter(p => p.id !== d.playerId) }
    if (view.value === 'waiting') say(`${String(d.name ?? '对手')} 离开了房间`)
  },
  'game-started': d => {
    if (!room.value) return
    const list = Array.isArray(d.playerList) ? d.playerList as GobangPlayer[] : room.value.players
    room.value = { ...room.value, players: list, ownerId: String(d.ownerId ?? room.value.ownerId), boardSize: Number(d.boardSize ?? 15), timeLimit: Number(d.timeLimit ?? 30) }
    startInfo.value = { currentTurn: String(d.currentTurn ?? room.value.ownerId), startedAt: Date.now() }
    matchKey.value++
    view.value = 'playing'
  },
}
// 观战名单变了 / 被请出去 / 大厅列表变了 / 断线重连
handlers['watchers-changed'] = d => { if (room.value && d.roomId === room.value.roomId && d.watch) watchInfo.value = d.watch as WatchInfo }
handlers['watch-ended'] = d => { if (spectating.value) backToLobby(String(d.message ?? '观战结束了')) }
let lobbyDebounce = 0
handlers['gobang-lobby-changed'] = () => {
  if (view.value !== 'lobby') return
  window.clearTimeout(lobbyDebounce)
  lobbyDebounce = window.setTimeout(() => { if (connected.value) void loadRooms() }, 400)
}
// 观战的人断线时被移出了名单和分组：重连后自动重新进来看（局面以服务器为准）
handlers['gobang-reconnected'] = () => { if (spectating.value && room.value) void watchRoom(room.value.roomId) }
const listeners = Object.entries(handlers).map(([name, fn]) => [name, (e: Event) => fn(((e as CustomEvent).detail ?? {}) as Detail)] as const)
// 服务器报错：在这里显示，不弹窗
const onError = (e: Event) => {
  const msg = ((e as CustomEvent).detail as { message?: string } | undefined)?.message
  if (msg) { e.preventDefault(); say(msg, 'error') }
}

onMounted(() => {
  listeners.forEach(([name, fn]) => window.addEventListener(name, fn))
  window.addEventListener('game-error', onError)
  if (connected.value) void loadRooms()
})
onUnmounted(() => {
  listeners.forEach(([name, fn]) => window.removeEventListener(name, fn))
  window.removeEventListener('game-error', onError)
  window.clearTimeout(noticeTimer)
  window.clearTimeout(lobbyDebounce)
  if (room.value) void signalR?.connection.value?.invoke('LeaveRoom').catch(() => undefined)
})
watch(connected, ok => { if (ok && view.value === 'lobby') void loadRooms() })
// 在大厅时每 15 秒自动刷新房间列表
let poll = 0
watch(view, v => {
  window.clearInterval(poll)
  if (v === 'lobby') poll = window.setInterval(() => { if (connected.value) void loadRooms() }, 15000)
}, { immediate: true })
onUnmounted(() => window.clearInterval(poll))

/** 切换游戏 / 离开页面前确认（太虚游页面会调用） */
async function handleBeforeLeave() {
  if (view.value === 'playing' && matchRef.value?.inProgress) {
    if (!window.confirm('对局还没结束，离开会判负。确定离开吗？')) return false
  }
  if (room.value) await invoke('LeaveRoom')
  return true
}
defineExpose({ handleBeforeLeave })
</script>

<style scoped>
.gomoku { display: grid; gap: 18px; color: var(--text-primary); }
.gm-head { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-end; gap: 10px 16px; padding-bottom: 14px; border-bottom: 1px solid var(--border-line); position: relative; }
.gm-head::after { content: ''; position: absolute; left: 0; bottom: -1px; width: 64px; height: 3px; background: var(--accent-color); }
.eyebrow, .panel-tag { margin: 0 0 6px; font: 10px/1.2 var(--font-mono, monospace); letter-spacing: .16em; color: var(--accent-color); text-transform: uppercase; }
.gm-head h2 { margin: 0; font-size: 26px; letter-spacing: .04em; }
.link-state { display: inline-flex; align-items: center; gap: 8px; font: 11px var(--font-mono, monospace); letter-spacing: .06em; color: var(--text-secondary); }
.link-state i { width: 7px; height: 7px; background: #d97706; }
.link-state.on i { background: var(--signal-mint, #2dd4bf); box-shadow: 0 0 0 3px color-mix(in srgb, var(--signal-mint, #2dd4bf) 25%, transparent); }
.gm-notice { display: flex; justify-content: space-between; gap: 12px; margin: 0; padding: 10px 14px; border-left: 3px solid var(--accent-color); background: color-mix(in srgb, var(--accent-color) 8%, var(--paper-card)); font-size: 13px; }
.gm-notice.error { border-left-color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--paper-card)); }
.gm-notice button { border: 0; background: none; color: var(--text-secondary); cursor: pointer; font-size: 16px; line-height: 1; }

.panel { position: relative; padding: 20px; border: 1px solid var(--border-line); background: var(--paper-card); }
.panel::before { content: ''; position: absolute; top: -1px; right: -1px; width: 28px; height: 6px; background: var(--hazard-stripe, repeating-linear-gradient(135deg, #f59e0b 0 4px, #111 4px 8px)); }
.lobby { display: grid; grid-template-columns: minmax(280px, 380px) minmax(0, 1fr); gap: 18px; align-items: start; }
.start-panel { display: grid; gap: 18px; }
.quick { display: grid; gap: 6px; padding: 18px; border: 1px solid var(--accent-color); background: linear-gradient(135deg, var(--accent-color), color-mix(in srgb, var(--accent-color) 70%, #000)); color: var(--on-accent, #fff); text-align: left; font: inherit; cursor: pointer; }
.quick:hover:not(:disabled) { filter: brightness(1.08); }
.quick:disabled { opacity: .55; cursor: not-allowed; }
.quick-title { font-size: 20px; font-weight: 700; letter-spacing: .08em; }
.quick-sub { font-size: 12px; opacity: .85; }
.create, .join { display: grid; gap: 10px; padding-top: 16px; border-top: 1px dashed var(--border-line); }
h3 { margin: 0; font-size: 14px; }
label { display: grid; gap: 5px; font-size: 12px; color: var(--text-secondary); }
input, select { padding: 8px 10px; border: 1px solid var(--border-line); background: var(--bg-main); color: var(--text-primary); font: inherit; font-size: 14px; }
.join-row { display: flex; gap: 8px; }
.join-row input { flex: 1; min-width: 0; font-family: var(--font-mono, monospace); }
.btn { padding: 8px 16px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 14px; cursor: pointer; }
.btn.small { padding: 5px 12px; font-size: 13px; }
.btn.primary { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent, #fff); font-weight: 600; }
.btn:disabled { opacity: .5; cursor: not-allowed; }

.rooms-panel { display: grid; gap: 14px; }
.rooms-head { display: flex; justify-content: space-between; align-items: center; }
.rooms-head .panel-tag { margin: 0; }
.rooms { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.room-btns { display: flex; flex-shrink: 0; gap: 6px; }
.gm-watchbar { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 12px; padding: 8px 12px; border: 1px solid var(--border-line); background: var(--paper-card); }
.gm-watchbar .bar-space { flex: 1; }
.watching-tag { padding: 2px 8px; border: 1px solid var(--accent-color); background: color-mix(in srgb, var(--accent-color) 10%, transparent); color: var(--accent-color); font-size: 12px; font-weight: 600; }
.rooms li { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 12px 14px; border: 1px solid var(--border-line); border-left: 3px solid var(--accent-color); background: var(--bg-main); }
.room-main { display: grid; gap: 3px; min-width: 0; }
.room-main b { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.room-main small { color: var(--text-secondary); font-size: 12px; }
.rooms-empty { padding: 28px 16px; border: 1px dashed var(--border-line); text-align: center; color: var(--text-secondary); }
.rooms-empty p { margin: 0 0 4px; color: var(--text-primary); }
.rules { margin: 0; padding: 12px 0 0 18px; border-top: 1px solid var(--border-line); display: grid; gap: 4px; color: var(--text-secondary); font-size: 12px; line-height: 1.6; }

.waiting { display: grid; gap: 18px; max-width: 720px; width: 100%; margin: 0 auto; }
.waiting-head { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-end; gap: 12px; }
.waiting-head h3 { font-size: 20px; }
.room-id { display: inline-grid; grid-template-columns: auto auto; column-gap: 10px; align-items: baseline; padding: 8px 12px; border: 1px solid var(--border-line); background: var(--bg-main); color: var(--text-primary); font: inherit; cursor: pointer; }
.room-id small { grid-column: 1 / -1; font-size: 11px; color: var(--text-secondary); text-align: left; }
.room-id b { font: 600 18px var(--font-mono, monospace); letter-spacing: .12em; }
.room-id span { font-size: 12px; color: var(--accent-color); }
.seats { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
.seat { display: flex; align-items: center; gap: 14px; padding: 16px; border: 1px dashed var(--border-line); color: var(--text-secondary); }
.seat.filled { border-style: solid; color: var(--text-primary); }
.seat.me { border-color: var(--accent-color); box-shadow: inset 3px 0 0 var(--accent-color); }
.seat small { display: block; margin-bottom: 4px; font-size: 11px; color: var(--text-secondary); }
.seat b { font-size: 16px; }
.seat em { margin-left: 8px; padding: 0 6px; background: var(--accent-color); color: var(--on-accent, #fff); font-size: 11px; font-style: normal; font-weight: 600; vertical-align: 2px; }
.stone { flex-shrink: 0; width: 34px; height: 34px; border-radius: 50%; }
.stone.black { background: radial-gradient(circle at 35% 32%, #4a5560, #07090c 75%); }
.stone.white { background: radial-gradient(circle at 35% 30%, #fff, #c9d2d8 85%); border: 1px solid #8f9ba4; }
.seat:not(.filled) .stone { opacity: .3; }
.waiting-meta { margin: 0; font: 12px var(--font-mono, monospace); color: var(--text-secondary); letter-spacing: .04em; }
.waiting-actions { display: flex; justify-content: flex-end; align-items: center; gap: 10px; padding-top: 14px; border-top: 1px solid var(--border-line); }
.waiting-tip { color: var(--text-secondary); font-size: 13px; }

@media (max-width: 860px) {
  .lobby { grid-template-columns: 1fr; }
}
@media (max-width: 520px) {
  .seats { grid-template-columns: 1fr; }
  .panel { padding: 16px; }
}
</style>
