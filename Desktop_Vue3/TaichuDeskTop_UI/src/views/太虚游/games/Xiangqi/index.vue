<!-- 中国象棋：联机对战（服务器判定、记战绩）+ 本地双人。莱茵生命实验室风格。 -->
<template>
  <div class="xiangqi">
    <header class="xq-head">
      <div>
        <p class="eyebrow">XIANGQI · {{ mode === 'online' ? 'ONLINE MATCH' : 'LOCAL MATCH' }} / 中国象棋</p>
        <h2>象棋</h2>
      </div>
      <div class="head-right">
        <span v-if="mode === 'online'" class="link-state" :class="{ on: connState === 'on' }"><i aria-hidden="true"></i>{{ connState === 'on' ? '已连接对战服务器' : connState === 'wait' ? '正在连接…' : '未连接' }}</span>
        <div class="mode-switch" role="tablist" aria-label="对局方式">
          <button type="button" role="tab" :aria-selected="mode === 'online'" :class="{ on: mode === 'online' }" @click="switchMode('online')">联机对战</button>
          <button type="button" role="tab" :aria-selected="mode === 'local'" :class="{ on: mode === 'local' }" @click="switchMode('local')">本地双人</button>
        </div>
      </div>
    </header>

    <p v-if="notice" class="xq-notice" :class="noticeKind" role="status">{{ notice }}<button type="button" aria-label="关闭提示" @click="notice = ''">×</button></p>

    <!-- ================= 联机 ================= -->
    <template v-if="mode === 'online'">
      <p v-if="!user.isLoggedIn" class="xq-notice">登录后才能联机对战。也可以先试试「本地双人」。</p>

      <!-- 大厅 -->
      <div v-if="!state" class="lobby">
        <section class="panel start-panel">
          <p class="panel-tag">ENTRY / 开始对局</p>
          <button type="button" class="quick" :disabled="!ready || busy" @click="call('Quick')">
            <span class="quick-title">快速匹配</span>
            <span class="quick-sub">有人在等就直接对战，没有就开房等人 · 每步 60 秒</span>
          </button>
          <form class="create" @submit.prevent="call('Create', form.name || defaultName, form.timeLimit)">
            <h3>自定义房间</h3>
            <label>房间名称<input v-model.trim="form.name" maxlength="30" :placeholder="defaultName" /></label>
            <label>每步时限
              <select v-model.number="form.timeLimit"><option :value="30">30 秒 · 快棋</option><option :value="60">60 秒</option><option :value="120">120 秒</option><option :value="180">180 秒 · 慢棋</option></select>
            </label>
            <button type="submit" class="btn primary" :disabled="!ready || busy">开房间</button>
          </form>
          <form class="join" @submit.prevent="call('Join', joinId)">
            <h3>输入房间号</h3>
            <div class="join-row">
              <input v-model.trim="joinId" maxlength="16" placeholder="例如 a1b2c3d4" autocomplete="off" />
              <button type="submit" class="btn" :disabled="!ready || busy || !joinId">加入</button>
            </div>
          </form>
        </section>

        <section class="panel rooms-panel">
          <div class="rooms-head">
            <p class="panel-tag">ROOMS / 房间</p>
            <button type="button" class="btn small" @click="loadLobby">刷新</button>
          </div>
          <ul v-if="rooms.length" class="rooms">
            <li v-for="r in rooms" :key="r.id" :class="{ busy: !(r.phase === 'waiting' && r.open) }">
              <div class="room-main">
                <b>{{ r.name }}</b>
                <small>{{ r.players.join(' vs ') || '—' }} · 每步 {{ r.timeLimit }} 秒</small>
              </div>
              <button v-if="r.phase === 'waiting' && r.open" type="button" class="btn small primary" :disabled="!ready || busy" :title="user.isLoggedIn ? '' : '登录后才能加入'" @click="call('Join', r.id)">加入</button>
              <span v-else class="room-state">{{ r.phase === 'playing' ? '对局中' : '已满' }}</span>
            </li>
          </ul>
          <div v-else class="rooms-empty"><p>现在没有人在等。</p><small>点「快速匹配」开一个房间，其他人进大厅就能看到。</small></div>
          <ul class="rules">
            <li>房主先执红先走；「再来一局」时交换先后手。</li>
            <li>将死或困毙对方获胜；超时、认输、中途离开、掉线超过 60 秒判负；双方同意、60 回合未吃子、同一局面重复三次判和。</li>
            <li>联机结果记入「我的战绩」和常胜 / 常败将军榜。</li>
          </ul>
        </section>
      </div>

      <!-- 等待开局 -->
      <section v-else-if="state.phase === 'waiting'" class="panel waiting">
        <div class="waiting-head">
          <div><p class="panel-tag">ROOM / 等待开局</p><h3>{{ state.roomName }}</h3></div>
          <button type="button" class="room-id" title="复制房间号发给朋友" @click="copyId"><small>房间号</small><b>{{ state.roomId }}</b><span>{{ copied ? '已复制' : '复制' }}</span></button>
        </div>
        <div class="seats">
          <div v-for="seat in onlineSeats" :key="seat.color" class="seat" :class="{ filled: !!seat.player, me: seat.isMe }">
            <span class="disc" :class="seat.color" aria-hidden="true">{{ seat.color === 'r' ? '帅' : '将' }}</span>
            <div>
              <small>{{ seat.color === 'r' ? '红方 · 先走' : '黑方 · 后走' }}<template v-if="seat.player && seat.player.userId === state.ownerId"> · 房主</template></small>
              <b>{{ seat.player ? seat.player.name : '等待对手加入…' }}<UserTitle v-if="seat.player" :user-id="seat.player.userId" /><em v-if="seat.isMe">你</em><i v-if="seat.player && !seat.player.online" class="off">掉线</i></b>
            </div>
          </div>
        </div>
        <p class="waiting-meta">每步 {{ state.timeLimit }} 秒<template v-if="state.round"> · 已下 {{ state.round }} 局，下一局交换先后手</template></p>
        <div class="waiting-actions">
          <button type="button" class="btn" :disabled="busy" @click="call('Leave')">离开房间</button>
          <button v-if="amOwner" type="button" class="btn primary" :disabled="busy || !state.red || !state.black" @click="call('Start')">{{ state.red && state.black ? '开始对局' : '等待对手…' }}</button>
          <span v-else class="muted">等待房主开始</span>
        </div>
      </section>

      <!-- 对局 -->
      <div v-else class="match">
        <div class="board-col">
          <XiangqiBoard :board="state.board" :turn="state.turn" :movable="state.phase === 'playing' ? state.myColor : null" :flipped="state.myColor === 'b'" :last-move="onlineLast" :check-side="state.inCheck ? state.turn : null" :disabled="state.phase !== 'playing' || pending" @move="onlineMove" />
        </div>
        <aside class="side">
          <div class="players">
            <div v-for="seat in sideSeats" :key="seat.color" class="player" :class="{ active: state.phase === 'playing' && state.turn === seat.color, me: seat.isMe }">
              <span class="disc" :class="seat.color" aria-hidden="true">{{ seat.color === 'r' ? '帅' : '将' }}</span>
              <div class="p-main">
                <small>{{ seat.color === 'r' ? 'RED · 红方' : 'BLACK · 黑方' }}<template v-if="seat.player && !seat.player.online"> · 掉线中</template></small>
                <b>{{ seat.player?.name ?? '（已离开）' }}<UserTitle v-if="seat.player" :user-id="seat.player.userId" /><em v-if="seat.isMe">你</em></b>
              </div>
              <span v-if="state.phase === 'playing' && state.turn === seat.color" class="clock" :class="{ low: secondsLeft <= 10 }">{{ secondsLeft }}s</span>
            </div>
            <div v-if="state.phase === 'playing'" class="timebar" aria-hidden="true"><span :style="{ width: `${Math.min(100, (secondsLeft / state.timeLimit) * 100)}%` }" :class="{ low: secondsLeft <= 10 }"></span></div>
          </div>

          <div class="status" :class="onlineStatusKind" role="status">
            <template v-if="state.phase === 'ended' && state.result">
              <b>{{ onlineResultTitle }}</b><span>{{ state.result.reason }}</span>
            </template>
            <template v-else>
              <b>{{ state.turn === state.myColor ? (state.inCheck ? '你被将军了！' : '轮到你走') : (state.inCheck ? '将军！等待对手应将…' : '等待对手走棋…') }}</b>
              <span>第 {{ Math.floor(state.moves.length / 2) + 1 }} 回合 · 每步 {{ state.timeLimit }} 秒</span>
            </template>
          </div>

          <div v-if="state.phase === 'playing' && state.drawOfferBy && state.drawOfferBy !== user.userId" class="draw-offer">
            <span>对手提议和棋</span>
            <button type="button" class="btn small primary" @click="call('AnswerDraw', true)">同意</button>
            <button type="button" class="btn small" @click="call('AnswerDraw', false)">拒绝</button>
          </div>

          <div class="actions">
            <template v-if="state.phase === 'playing'">
              <button type="button" class="btn" :disabled="!!state.drawOfferBy || state.moves.length < 10" :title="state.moves.length < 10 ? '至少走满 10 步才能提和' : ''" @click="call('OfferDraw')">{{ state.drawOfferBy === user.userId ? '已提和…' : '提和' }}</button>
              <button type="button" class="btn danger" @click="resignOnline">认输</button>
              <button type="button" class="btn" @click="leaveOnline">离开</button>
            </template>
            <template v-else>
              <button v-if="amOwner && state.red && state.black" type="button" class="btn primary" @click="call('Start')">再来一局</button>
              <span v-else-if="state.red && state.black" class="muted">等待房主开始下一局</span>
              <span v-else class="muted">对手已离开</span>
              <button type="button" class="btn" @click="call('Leave')">返回大厅</button>
            </template>
          </div>

          <MoveLog :moves="onlineNotes" />
        </aside>
      </div>
    </template>

    <!-- ================= 本地双人 ================= -->
    <div v-else class="match">
      <div class="board-col">
        <XiangqiBoard :board="local.board" :turn="local.turn" :movable="localOver ? null : local.turn" :flipped="localFlipped" :last-move="localLast" :check-side="!localOver && localInCheck ? local.turn : null" :disabled="localOver" @move="localMove" />
      </div>
      <aside class="side">
        <div class="players">
          <div v-for="c in SIDES" :key="c" class="player" :class="{ active: !localOver && local.turn === c }">
            <span class="disc" :class="c" aria-hidden="true">{{ c === 'r' ? '帅' : '将' }}</span>
            <div class="p-main"><small>{{ c === 'r' ? 'RED · 红方 · 先走' : 'BLACK · 黑方' }}</small><b>{{ c === 'r' ? (user.userInfo?.username || '红方') : '黑方' }}</b></div>
            <span class="cap">吃 {{ localCaptures[c] }}</span>
          </div>
        </div>
        <div class="status" :class="localOver ? 'over' : localInCheck ? 'checked' : ''" role="status">
          <template v-if="localOver"><b>{{ localResult.title }}</b><span>{{ localResult.reason }}</span></template>
          <template v-else><b>{{ local.turn === 'r' ? '红方走棋' : '黑方走棋' }}<template v-if="localInCheck"> · 将军！</template></b><span>第 {{ Math.floor(local.moves.length / 2) + 1 }} 回合</span></template>
        </div>
        <div class="actions">
          <template v-if="!localOver">
            <button type="button" class="btn" :disabled="!local.history.length" @click="localUndo">悔棋</button>
            <button type="button" class="btn danger" :disabled="!local.moves.length" @click="localResign">{{ local.turn === 'r' ? '红方' : '黑方' }}认输</button>
          </template>
          <template v-else>
            <button type="button" class="btn primary" @click="localReset(false)">再来一局</button>
            <button type="button" class="btn" :disabled="saved || saving || !user.isLoggedIn" :title="user.isLoggedIn ? '' : '登录后对局会自动记入战绩'" @click="saveLocal">{{ saved ? '已记入战绩' : saving ? '记录中…' : '记入战绩' }}</button>
          </template>
          <button type="button" class="btn ghost" @click="localFlipped = !localFlipped">翻转棋盘</button>
          <button v-if="!localOver && local.moves.length" type="button" class="btn ghost" @click="localReset(true)">重新开始</button>
        </div>
        <MoveLog :moves="localNotes" />
        <p class="note">两人在同一台电脑上轮流走。本地对局会记入战绩，但不计入胜率和排行榜。</p>
      </aside>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, defineComponent, h, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import * as signalR from '@microsoft/signalr'
import { useUserStore } from '@/stores/user'
import request from '@/utils/request'
import UserTitle from '@/components/UserTitle.vue'
import { gameRecordApi } from '@/api/gameRecord'
import XiangqiBoard from './XiangqiBoard.vue'
import { INITIAL, apply, hasAnyLegal, inCheck, notation, other, type Side } from './xqRules'

const SIDES: Side[] = ['r', 'b']
const lastOf = <T,>(arr: T[] | undefined): T | undefined => (arr && arr.length ? arr[arr.length - 1] : undefined)

// ---------- 棋谱 ----------
const MoveLog = defineComponent({
  props: { moves: { type: Array as () => string[], required: true } },
  setup(props) {
    const list = ref<HTMLOListElement | null>(null)
    watch(() => props.moves.length, () => void nextTick(() => { if (list.value) list.value.scrollTop = list.value.scrollHeight }))
    return () => h('div', { class: 'log' }, [
      h('p', { class: 'log-tag' }, `MOVES / 棋谱 · ${Math.ceil(props.moves.length / 2)} 回合`),
      h('ol', { ref: list }, props.moves.map((m, i) => h('li', { class: i % 2 ? 'b' : 'r', key: i }, [h('span', i % 2 ? '' : `${i / 2 + 1}.`), m]))),
    ])
  },
})
/** 从开局重放出每一步的中文记谱 */
function notesOf(moves: { from: number; to: number }[]) {
  let b = INITIAL
  return moves.map(m => { const n = notation(b, m.from, m.to); b = apply(b, m.from, m.to); return n })
}

const user = useUserStore()
const mode = ref<'online' | 'local'>('online')
const notice = ref(''), noticeKind = ref<'info' | 'error'>('info')
let noticeTimer = 0
function say(text: string, kind: 'info' | 'error' = 'info') {
  notice.value = text; noticeKind.value = kind
  window.clearTimeout(noticeTimer)
  noticeTimer = window.setTimeout(() => { notice.value = '' }, 5000)
}

// ================= 联机 =================
interface PlayerView { userId: string; name: string; online: boolean }
interface OnlineState {
  roomId: string; roomName: string; timeLimit: number; ownerId: string; phase: 'waiting' | 'playing' | 'ended'; round: number
  board: string; turn: Side; moves: { from: number; to: number; piece: string; captured: string | null }[]; deadline: string | null; inCheck: boolean
  red: PlayerView | null; black: PlayerView | null; myColor: Side | null; drawOfferBy: string | null; result: { winner: Side | null; reason: string } | null
}
interface LobbyRoom { id: string; name: string; timeLimit: number; phase: string; players: string[]; open: boolean }

const state = ref<OnlineState | null>(null)
const rooms = ref<LobbyRoom[]>([])
const connState = ref<'wait' | 'on' | 'off'>('wait')
const busy = ref(false), pending = ref(false)
const joinId = ref(''), copied = ref(false)
const form = reactive({ name: '', timeLimit: 60 })
const defaultName = computed(() => `${user.userInfo?.username || '我'} 的棋局`)
const ready = computed(() => user.isLoggedIn && connState.value === 'on')
const same = (a?: string | null, b?: string | null) => !!a && !!b && a.toLowerCase() === b.toLowerCase()
const amOwner = computed(() => same(state.value?.ownerId, String(user.userId ?? '')))
let connection: signalR.HubConnection | null = null
let lobbyTimer = 0, clockTimer = 0
const now = ref(Date.now())

async function call(method: string, ...args: unknown[]) {
  if (!connection || connState.value !== 'on') { say('还没连上对战服务器，请稍等', 'error'); return }
  busy.value = true
  try {
    const err = await connection.invoke<string | null>(method, ...args)
    if (err) say(err, 'error')
    if (method === 'Leave') void loadLobby()
  } catch { say('网络不稳定，操作没有送达', 'error') } finally { busy.value = false }
}
async function loadLobby() {
  try {
    // 连上了走实时连接；没登录或还没连上，就用只读接口看看开着哪些房间
    rooms.value = connection && connState.value === 'on'
      ? await connection.invoke<LobbyRoom[]>('Lobby')
      : await request.get<LobbyRoom[]>('/game-lobby/xiangqi', { silentError: true })
  } catch { /* 忽略 */ }
}
async function connect() {
  if (!user.isLoggedIn || connection) { if (!user.isLoggedIn) connState.value = 'off'; return }
  const hubUrl = new URL('/signalr/xiangqi', new URL(import.meta.env.VITE_API_BASE_URL || '/api', window.location.origin)).href
  connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, { accessTokenFactory: () => user.token || '' })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
    .build()
  connection.on('State', (s: OnlineState | null) => { state.value = s; pending.value = false; if (!s) void loadLobby() })
  connection.onreconnecting(() => { connState.value = 'wait' })
  connection.onreconnected(async () => { connState.value = 'on'; state.value = await connection!.invoke<OnlineState | null>('Resume'); void loadLobby() })
  connection.onclose(() => { connState.value = 'off' })
  try {
    await connection.start()
    connState.value = 'on'
    state.value = await connection.invoke<OnlineState | null>('Resume')
    await loadLobby()
  } catch { connState.value = 'off' }
}

const onlineSeats = computed(() => {
  const s = state.value
  if (!s) return []
  return (['r', 'b'] as const).map(color => {
    const player = color === 'r' ? s.red : s.black
    return { color, player, isMe: !!player && same(player.userId, String(user.userId ?? '')) }
  })
})
// 对局时自己那一方显示在下面
const sideSeats = computed(() => state.value?.myColor === 'r' ? [...onlineSeats.value].reverse() : onlineSeats.value)
const onlineLast = computed(() => { const m = lastOf(state.value?.moves); return m ? { from: m.from, to: m.to } : null })
const onlineNotes = computed(() => notesOf(state.value?.moves ?? []))
const secondsLeft = computed(() => { const d = state.value?.deadline; return d ? Math.max(0, Math.ceil((new Date(d).getTime() - now.value) / 1000)) : 0 })
const onlineResultTitle = computed(() => {
  const r = state.value?.result
  if (!r) return ''
  if (!r.winner) return '和棋'
  if (!state.value?.myColor) return r.winner === 'r' ? '红方胜' : '黑方胜'
  return r.winner === state.value.myColor ? '你赢了！' : '你输了'
})
const onlineStatusKind = computed(() => {
  const s = state.value
  if (!s) return ''
  if (s.phase === 'ended') return !s.result?.winner ? 'draw' : s.result.winner === s.myColor ? 'win' : 'lose'
  return s.inCheck && s.turn === s.myColor ? 'checked' : s.turn === s.myColor ? 'mine' : ''
})

async function onlineMove(from: number, to: number) {
  if (pending.value) return
  pending.value = true
  await call('Move', from, to)
  window.setTimeout(() => { pending.value = false }, 3000)
}
async function resignOnline() { if (window.confirm('确定认输吗？这局会记为负。')) await call('Resign') }
async function leaveOnline() { if (window.confirm('对局还没结束，离开会判负。确定离开吗？')) await call('Leave') }
async function copyId() {
  if (!state.value) return
  try { await navigator.clipboard.writeText(state.value.roomId); copied.value = true; window.setTimeout(() => { copied.value = false }, 1500) } catch { say(`房间号：${state.value.roomId}`) }
}
watch(() => state.value?.phase, (p, old) => {
  if (p === 'playing' && old !== 'playing') say(state.value?.myColor === 'r' ? '对局开始，你执红先走' : '对局开始，你执黑后走')
})
watch(() => state.value && state.value.red && state.value.black ? `${state.value.red.userId}|${state.value.black.userId}` : '', (v, old) => {
  if (v && old === '' && state.value?.phase === 'waiting' && amOwner.value) say('对手已加入，可以开始了')
})

// ================= 本地双人 =================
interface LocalSnap { board: string; turn: Side; moves: { from: number; to: number }[]; captures: Record<Side, number>; seen: Record<string, number>; sinceCapture: number }
const local = reactive({
  board: INITIAL, turn: 'r' as Side, moves: [] as { from: number; to: number }[], captures: { r: 0, b: 0 } as Record<Side, number>,
  history: [] as LocalSnap[], seen: { [`${INITIAL}r`]: 1 } as Record<string, number>, sinceCapture: 0,
  over: null as null | { winner: Side | null; reason: string },
})
const localFlipped = ref(false)
const saved = ref(false), saving = ref(false)
const localOver = computed(() => !!local.over)
const localInCheck = computed(() => inCheck(local.board, local.turn))
const localLast = computed(() => lastOf(local.moves) ?? null)
const localNotes = computed(() => notesOf(local.moves))
const localCaptures = computed(() => local.captures)
const localResult = computed(() => {
  const o = local.over
  if (!o) return { title: '', reason: '' }
  return { title: o.winner ? `${o.winner === 'r' ? '红方' : '黑方'}胜` : '和棋', reason: o.reason }
})

function localMove(from: number, to: number) {
  if (local.over) return
  const captured = local.board[to]
  local.history.push({ board: local.board, turn: local.turn, moves: [...local.moves], captures: { ...local.captures }, seen: { ...local.seen }, sinceCapture: local.sinceCapture })
  local.board = apply(local.board, from, to)
  local.moves.push({ from, to })
  if (captured && captured !== '.') { local.captures[local.turn]++; local.sinceCapture = 0 } else local.sinceCapture++
  const mover = local.turn
  local.turn = other(mover)
  const key = local.board + local.turn
  local.seen[key] = (local.seen[key] ?? 0) + 1
  if (!hasAnyLegal(local.board, local.turn)) local.over = { winner: mover, reason: inCheck(local.board, local.turn) ? '将死' : '困毙' }
  else if (local.sinceCapture >= 120) local.over = { winner: null, reason: '60 回合没有吃子，和棋' }
  else if (local.seen[key]! >= 3) local.over = { winner: null, reason: '同一局面重复三次，和棋' }
}
function localUndo() {
  const s = local.history.pop()
  if (!s) return
  Object.assign(local, { board: s.board, turn: s.turn, moves: s.moves, captures: s.captures, seen: s.seen, sinceCapture: s.sinceCapture })
}
function localResign() {
  if (!window.confirm(`${local.turn === 'r' ? '红方' : '黑方'}确定认输吗？`)) return
  local.over = { winner: other(local.turn), reason: `${local.turn === 'r' ? '红方' : '黑方'}认输` }
}
function localReset(ask: boolean) {
  if (ask && local.moves.length && !window.confirm('重新开始会清空这盘棋，确定吗？')) return
  Object.assign(local, { board: INITIAL, turn: 'r', moves: [], captures: { r: 0, b: 0 }, history: [], seen: { [`${INITIAL}r`]: 1 }, sinceCapture: 0, over: null })
  saved.value = false
}
// 本地对局结束自动记入战绩（登录时）
watch(localOver, over => { if (over && user.isLoggedIn) void saveLocal().catch(() => undefined) })
async function saveLocal() {
  if (!local.over || saved.value || saving.value) return
  saving.value = true
  try {
    await gameRecordApi.save({
      gameType: 'xiangqi',
      player1Name: user.userInfo?.username || '红方',
      player2Name: '黑方',
      winner: local.over.winner === 'r' ? 'player1' : local.over.winner === 'b' ? 'player2' : 'draw',
      movesCount: local.moves.length,
      movesJson: JSON.stringify({ moves: local.moves, notes: localNotes.value, reason: local.over.reason }),
    })
    saved.value = true
  } finally { saving.value = false }
}

// ================= 生命周期 =================
function switchMode(m: 'online' | 'local') {
  if (m === mode.value) return
  if (m === 'local' && state.value?.phase === 'playing') { say('联机对局还在进行，先下完或认输再切换', 'error'); return }
  mode.value = m
}
onMounted(async () => {
  await connect()
  if (connState.value !== 'on') void loadLobby()
  lobbyTimer = window.setInterval(() => { if (mode.value === 'online' && !state.value && !document.hidden) void loadLobby() }, 5000)
  clockTimer = window.setInterval(() => { now.value = Date.now() }, 500)
})
onBeforeUnmount(() => {
  window.clearInterval(lobbyTimer); window.clearInterval(clockTimer); window.clearTimeout(noticeTimer)
  void connection?.stop()
})

/** 切换游戏 / 离开页面前确认（太虚游页面会调用） */
async function handleBeforeLeave() {
  if (state.value?.phase === 'playing') {
    if (!window.confirm('联机对局还没结束，离开会判负。确定离开吗？')) return false
    await call('Leave')
  } else if (state.value) await call('Leave')
  return true
}
defineExpose({ handleBeforeLeave })
</script>

<style scoped>
.xiangqi { display: grid; gap: 18px; color: var(--text-primary); }
.xq-head { position: relative; display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-end; gap: 10px 16px; padding-bottom: 14px; border-bottom: 1px solid var(--border-line); }
.xq-head::after { content: ''; position: absolute; left: 0; bottom: -1px; width: 64px; height: 3px; background: var(--accent-color); }
.eyebrow, .panel-tag { margin: 0 0 6px; font: 10px/1.2 var(--font-mono, monospace); letter-spacing: .16em; color: var(--accent-color); }
.xq-head h2 { margin: 0; font-size: 26px; letter-spacing: .04em; }
.head-right { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; }
.link-state { display: inline-flex; align-items: center; gap: 8px; font: 11px var(--font-mono, monospace); color: var(--text-secondary); }
.link-state i { width: 7px; height: 7px; background: #d97706; }
.link-state.on i { background: var(--signal-mint, #2dd4bf); box-shadow: 0 0 0 3px color-mix(in srgb, var(--signal-mint, #2dd4bf) 25%, transparent); }
.mode-switch { display: inline-flex; border: 1px solid var(--border-line); }
.mode-switch button { padding: 7px 14px; border: 0; border-right: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.mode-switch button:last-child { border-right: 0; }
.mode-switch button.on { background: var(--accent-color); color: var(--on-accent, #fff); font-weight: 600; }
.xq-notice { display: flex; justify-content: space-between; gap: 12px; margin: 0; padding: 10px 14px; border-left: 3px solid var(--accent-color); background: color-mix(in srgb, var(--accent-color) 8%, var(--paper-card)); font-size: 13px; }
.xq-notice.error { border-left-color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--paper-card)); }
.xq-notice button { border: 0; background: none; color: var(--text-secondary); cursor: pointer; font-size: 16px; line-height: 1; }
.muted { color: var(--text-secondary); font-size: 13px; }

.panel { position: relative; padding: 20px; border: 1px solid var(--border-line); background: var(--paper-card); }
.panel::before { content: ''; position: absolute; top: -1px; right: -1px; width: 28px; height: 6px; background: var(--hazard-stripe, repeating-linear-gradient(135deg, #f59e0b 0 4px, #111 4px 8px)); }
.lobby { display: grid; grid-template-columns: minmax(280px, 380px) minmax(0, 1fr); gap: 18px; align-items: start; }
.start-panel { display: grid; gap: 18px; }
.quick { display: grid; gap: 6px; padding: 18px; border: 1px solid var(--accent-color); background: linear-gradient(135deg, var(--accent-color), color-mix(in srgb, var(--accent-color) 70%, #000)); color: var(--on-accent, #fff); text-align: left; font: inherit; cursor: pointer; }
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
.btn.danger { color: var(--danger); }
.btn.ghost { color: var(--text-secondary); }
.btn:disabled { opacity: .5; cursor: not-allowed; }
.rooms-panel { display: grid; gap: 14px; }
.rooms-head { display: flex; justify-content: space-between; align-items: center; }
.rooms-head .panel-tag { margin: 0; }
.rooms { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.rooms li { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 12px 14px; border: 1px solid var(--border-line); border-left: 3px solid var(--accent-color); background: var(--bg-main); }
.rooms li.busy { border-left-color: var(--border-line); opacity: .75; }
.room-main { display: grid; gap: 3px; min-width: 0; }
.room-main b { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.room-main small, .room-state { color: var(--text-secondary); font-size: 12px; }
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
.seat em, .p-main em { margin-left: 8px; padding: 0 6px; background: var(--accent-color); color: var(--on-accent, #fff); font-size: 11px; font-style: normal; font-weight: 600; vertical-align: 2px; }
.seat .off { margin-left: 6px; color: var(--danger); font-size: 11px; font-style: normal; }
.seat:not(.filled) .disc { opacity: .3; }
.disc { flex-shrink: 0; display: grid; place-items: center; width: 36px; height: 36px; border-radius: 50%; background: radial-gradient(circle at 40% 35%, #fff, #e7ebee); border: 2px solid currentColor; font: 700 18px 'KaiTi', 'STKaiti', serif; box-shadow: inset 0 0 0 3px #fff; }
.disc.r { color: #c2272d; }
.disc.b { color: #1b2128; }
.waiting-meta { margin: 0; font: 12px var(--font-mono, monospace); color: var(--text-secondary); }
.waiting-actions { display: flex; justify-content: flex-end; align-items: center; gap: 10px; padding-top: 14px; border-top: 1px solid var(--border-line); }

.match { display: grid; grid-template-columns: minmax(0, 1fr) 300px; gap: 20px; align-items: start; }
.board-col { width: 100%; max-width: min(100%, calc((100dvh - 190px) * 0.89)); min-width: 280px; justify-self: center; }
.side { display: grid; gap: 14px; position: sticky; top: 0; }
.players { display: grid; gap: 8px; padding: 14px; border: 1px solid var(--border-line); background: var(--paper-card); }
.player { display: flex; align-items: center; gap: 12px; padding: 10px; border: 1px solid transparent; }
.player.active { border-color: var(--accent-color); background: color-mix(in srgb, var(--accent-color) 7%, transparent); }
.p-main { display: grid; gap: 2px; min-width: 0; flex: 1; }
.p-main small { font: 10px var(--font-mono, monospace); letter-spacing: .12em; color: var(--text-secondary); }
.p-main b { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.player .disc { width: 30px; height: 30px; font-size: 15px; }
.clock { font: 700 18px var(--font-mono, monospace); color: var(--accent-color); font-variant-numeric: tabular-nums; }
.clock.low { color: var(--danger); }
.cap { font: 12px var(--font-mono, monospace); color: var(--text-secondary); }
.timebar { height: 3px; background: var(--bg-sub); }
.timebar span { display: block; height: 100%; background: var(--signal-mint, #2dd4bf); transition: width .5s linear; }
.timebar span.low { background: var(--danger); }
.status { display: grid; gap: 4px; padding: 14px; border: 1px solid var(--border-line); border-left: 3px solid var(--border-line); background: var(--paper-card); }
.status b { font-size: 16px; }
.status span { font-size: 12px; color: var(--text-secondary); }
.status.mine { border-left-color: var(--accent-color); }
.status.checked { border-left-color: var(--danger); background: color-mix(in srgb, var(--danger) 7%, var(--paper-card)); }
.status.win, .status.over { border-left-color: var(--signal-mint, #2dd4bf); background: color-mix(in srgb, var(--signal-mint, #2dd4bf) 10%, var(--paper-card)); }
.status.lose { border-left-color: var(--danger); }
.status.draw { border-left-color: #d97706; }
.draw-offer { display: flex; align-items: center; gap: 8px; padding: 10px 12px; border: 1px dashed #d97706; background: color-mix(in srgb, #f59e0b 8%, var(--paper-card)); font-size: 13px; }
.draw-offer span { flex: 1; }
.actions { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
:deep(.log) { padding: 12px 14px; border: 1px solid var(--border-line); background: var(--paper-card); }
:deep(.log-tag) { margin: 0 0 8px; font: 10px var(--font-mono, monospace); letter-spacing: .14em; color: var(--accent-color); }
:deep(.log ol) { list-style: none; margin: 0; padding: 0; max-height: 240px; overflow: auto; display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 3px 10px; font-size: 13px; }
:deep(.log li) { display: flex; gap: 6px; }
:deep(.log li.r) { color: #c2272d; }
:deep(.log li span) { min-width: 2.2em; color: var(--text-secondary); text-align: right; font: 12px var(--font-mono, monospace); }
.note { margin: 0; font-size: 12px; color: var(--text-secondary); line-height: 1.6; }

@media (max-width: 980px) {
  .match { grid-template-columns: 1fr; }
  .board-col { max-width: 100%; }
  .side { position: static; }
}
@media (max-width: 860px) { .lobby { grid-template-columns: 1fr; } }
@media (max-width: 520px) { .seats { grid-template-columns: 1fr; } .panel { padding: 16px; } }
</style>
