<!-- GoOnline.vue：联机围棋。大厅（快速匹配 / 开房 / 房间号）→ 等待开局 → 对局（服务器判定提子、禁着点、打劫）→ 双方确认点目 → 中国规则数子。 -->
<template>
  <div class="go-online">
    <p v-if="notice" class="go-notice" :class="noticeKind" role="status">{{ notice }}<button type="button" aria-label="关闭提示" @click="notice = ''">×</button></p>

    <p v-if="!user.isLoggedIn" class="go-notice">登录后才能联机对战。也可以先试试「本地双人」。</p>

    <!-- 大厅 -->
    <div v-if="!state" class="lobby">
      <section class="panel start-panel">
        <p class="panel-tag">ENTRY / 开始对局</p>
        <button type="button" class="quick" :disabled="!ready || busy" @click="call('Quick')">
          <span class="quick-title">快速匹配</span>
          <span class="quick-sub">有人在等就直接对战，没有就开 19 路房间等人 · 每步 60 秒</span>
        </button>
        <form class="create" @submit.prevent="call('Create', form.name || defaultName, form.size, form.timeLimit)">
          <h3>自定义房间</h3>
          <label>房间名称<input v-model.trim="form.name" maxlength="30" :placeholder="defaultName" /></label>
          <div class="form-row">
            <label>棋盘
              <select v-model.number="form.size"><option :value="9">9 路 · 快速</option><option :value="13">13 路</option><option :value="19">19 路 · 标准</option></select>
            </label>
            <label>每步时限
              <select v-model.number="form.timeLimit"><option :value="30">30 秒</option><option :value="60">60 秒</option><option :value="120">120 秒</option><option :value="180">180 秒</option></select>
            </label>
          </div>
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
            <span class="size-chip">{{ r.size }}<small>路</small></span>
            <div class="room-main">
              <b>{{ r.name }}</b>
              <small>{{ r.players.join(' vs ') || '—' }} · 每步 {{ r.timeLimit }} 秒</small>
            </div>
            <button v-if="r.phase === 'waiting' && r.open" type="button" class="btn small primary" :disabled="!ready || busy" :title="user.isLoggedIn ? '' : '登录后才能加入'" @click="call('Join', r.id)">加入</button>
            <span v-else class="room-state">{{ r.phase === 'playing' ? '对局中' : r.phase === 'scoring' ? '点目中' : '已满' }}</span>
          </li>
        </ul>
        <div v-else class="rooms-empty"><p>现在没有人在等。</p><small>点「快速匹配」开一个房间，其他人进大厅就能看到。</small></div>
        <ul class="rules">
          <li>房主执黑先下；「再来一局」时交换黑白。中国规则数子，白贴 {{ KOMI }} 目。</li>
          <li>双方连续停一手进入点目：任意一方点棋子可以整块标记死子，双方都确认后结算；觉得还没下完可以点「继续下」。</li>
          <li>超时、认输、中途离开、掉线超过 60 秒判负；点目 3 分钟内没确认，按当前标记自动结算。</li>
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
        <div v-for="seat in seats" :key="seat.color" class="seat" :class="{ filled: !!seat.player, me: seat.isMe }">
          <span class="stone" :class="seat.color === 'b' ? 'black' : 'white'" aria-hidden="true"></span>
          <div>
            <small>{{ seat.color === 'b' ? '黑方 · 先下' : `白方 · 贴 ${KOMI} 目` }}<template v-if="seat.player && same(seat.player.userId, state.ownerId)"> · 房主</template></small>
            <b>{{ seat.player ? seat.player.name : '等待对手加入…' }}<em v-if="seat.isMe">你</em><i v-if="seat.player && !seat.player.online" class="off">掉线</i></b>
          </div>
        </div>
      </div>
      <p class="waiting-meta">{{ state.size }} 路 · 每步 {{ state.timeLimit }} 秒<template v-if="state.round"> · 已下 {{ state.round }} 局，下一局交换黑白</template></p>
      <div class="waiting-actions">
        <button type="button" class="btn" :disabled="busy" @click="call('Leave')">离开房间</button>
        <button v-if="amOwner" type="button" class="btn primary" :disabled="busy || !state.black || !state.white" @click="call('Start')">{{ state.black && state.white ? '开始对局' : '等待对手…' }}</button>
        <span v-else class="muted">等待房主开始</span>
      </div>
    </section>

    <!-- 对局 / 点目 / 结束 -->
    <div v-else class="match">
      <div class="board-col">
        <LabBoard
          :size="state.size"
          :stones="grid"
          :last-move="state.phase === 'playing' ? lastStone : null"
          :ghost="myTurn && !pending ? myNum : 0"
          :disabled="!(myTurn && !pending) && state.phase !== 'scoring'"
          :pick-stones="state.phase === 'scoring' && !!state.myColor"
          :dead="state.score ? deadPoints : null"
          :territory="state.score ? territory : null"
          @play="onBoard"
        />
      </div>

      <aside class="side">
        <div class="players">
          <div v-for="seat in sideSeats" :key="seat.color" class="player" :class="{ active: state.phase === 'playing' && state.turn === seat.color, me: seat.isMe }">
            <span class="stone" :class="seat.color === 'b' ? 'black' : 'white'" aria-hidden="true"></span>
            <div class="p-main">
              <small>{{ seat.color === 'b' ? 'BLACK · 黑方' : `WHITE · 贴 ${KOMI}` }}<template v-if="seat.player && !seat.player.online"> · 掉线中</template><template v-if="state.phase === 'scoring' && state.confirmed[seat.color]"> · 已确认</template></small>
              <b>{{ seat.player?.name ?? '（已离开）' }}<em v-if="seat.isMe">你</em></b>
            </div>
            <span v-if="state.phase === 'playing' && state.turn === seat.color" class="clock" :class="{ low: secondsLeft <= 10 }">{{ secondsLeft }}s</span>
            <span v-else class="cap" title="提子数">提 {{ state.captures[seat.color] }}</span>
          </div>
          <div v-if="state.phase === 'playing'" class="timebar" aria-hidden="true"><span :style="{ width: `${Math.min(100, (secondsLeft / state.timeLimit) * 100)}%` }" :class="{ low: secondsLeft <= 10 }"></span></div>
        </div>

        <div class="status" :class="statusKind" role="status">
          <template v-if="state.phase === 'ended' && state.result">
            <b>{{ resultTitle }}</b><span>{{ state.result.reason }}</span>
          </template>
          <template v-else-if="state.phase === 'scoring'">
            <b>点目：点棋子标记死子（整块切换）</b>
            <span v-if="state.score">黑 {{ state.score.black }} ：白 {{ state.score.white }}（含贴目） · {{ leaderText }}</span>
            <span>{{ confirmText }} · 剩 {{ secondsLeft }} 秒自动结算</span>
          </template>
          <template v-else>
            <b>{{ myTurn ? '轮到你落子' : state.myColor ? '等待对手落子…' : '对局中' }}</b>
            <span>第 {{ state.moves.length + 1 }} 手 · {{ state.size }} 路 · 每步 {{ state.timeLimit }} 秒</span>
            <span v-if="state.passes === 1" class="warn">{{ lastPassByMe ? '你停了一手，对手再停一手就进入点目' : '对手停了一手：你也停一手就进入点目' }}</span>
          </template>
        </div>

        <div class="actions">
          <template v-if="state.phase === 'playing'">
            <button type="button" class="btn" :disabled="!myTurn || busy" @click="passOnline">停一手</button>
            <button type="button" class="btn danger" @click="resignOnline">认输</button>
            <button type="button" class="btn" @click="leaveOnline">离开</button>
          </template>
          <template v-else-if="state.phase === 'scoring'">
            <button type="button" class="btn primary" :disabled="busy || myConfirmed" @click="call('ConfirmScore')">{{ myConfirmed ? '已确认，等待对手' : '确认结果' }}</button>
            <button type="button" class="btn" :disabled="busy" @click="call('ResumePlay')">继续下</button>
            <button type="button" class="btn danger" @click="resignOnline">认输</button>
          </template>
          <template v-else>
            <button v-if="amOwner && state.black && state.white" type="button" class="btn primary" @click="call('Start')">再来一局</button>
            <span v-else-if="state.black && state.white" class="muted">等待房主开始下一局</span>
            <span v-else class="muted">对手已离开</span>
            <button type="button" class="btn" @click="call('Leave')">返回大厅</button>
          </template>
        </div>

        <div class="log">
          <p class="log-tag">MOVES / 棋谱 · {{ state.moves.length }} 手</p>
          <ol ref="logEl">
            <li v-for="(m, i) in state.moves" :key="i" :class="m.color"><span>{{ i + 1 }}</span>{{ m.color === 'b' ? '黑' : '白' }} {{ m.index < 0 ? '停一手' : coord(m.index) }}<small v-if="m.captured">提 {{ m.captured }}</small></li>
          </ol>
        </div>
      </aside>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import * as signalR from '@microsoft/signalr'
import { useUserStore } from '@/stores/user'
import request from '@/utils/request'
import LabBoard from '../LabBoard.vue'

type Color = 'b' | 'w'
interface PlayerView { userId: string; name: string; online: boolean }
interface GoState {
  roomId: string; roomName: string; size: number; timeLimit: number; ownerId: string
  phase: 'waiting' | 'playing' | 'scoring' | 'ended'; round: number
  board: string; turn: Color; passes: number
  moves: { index: number; color: Color; captured: number }[]
  captures: Record<Color, number>; deadline: string | null
  black: PlayerView | null; white: PlayerView | null; myColor: Color | null
  dead: number[]; confirmed: Record<Color, boolean>
  score: { black: number; white: number; owner: string; komi: number } | null
  result: { winner: Color | null; reason: string } | null
}
interface LobbyRoom { id: string; name: string; size: number; timeLimit: number; phase: string; players: string[]; open: boolean }

const KOMI = 7.5
const LETTERS = 'ABCDEFGHJKLMNOPQRSTUVWXYZ'
const COLORS: Color[] = ['b', 'w']
const emit = defineEmits<{ conn: [state: 'wait' | 'on' | 'off'] }>()

const user = useUserStore()
const state = ref<GoState | null>(null)
const rooms = ref<LobbyRoom[]>([])
const connState = ref<'wait' | 'on' | 'off'>('wait')
const busy = ref(false), pending = ref(false)
const joinId = ref(''), copied = ref(false)
const form = reactive({ name: '', size: 19, timeLimit: 60 })
const defaultName = computed(() => `${user.userInfo?.username || '我'} 的棋局`)
const ready = computed(() => user.isLoggedIn && connState.value === 'on')
const same = (a?: string | null, b?: string | null) => !!a && !!b && a.toLowerCase() === b.toLowerCase()
const amOwner = computed(() => same(state.value?.ownerId, String(user.userId ?? '')))
const logEl = ref<HTMLOListElement | null>(null)
const now = ref(Date.now())
let connection: signalR.HubConnection | null = null
let lobbyTimer = 0, clockTimer = 0, noticeTimer = 0

const notice = ref(''), noticeKind = ref<'info' | 'error'>('info')
function say(text: string, kind: 'info' | 'error' = 'info') {
  notice.value = text; noticeKind.value = kind
  window.clearTimeout(noticeTimer)
  noticeTimer = window.setTimeout(() => { notice.value = '' }, 5000)
}
watch(connState, s => emit('conn', s), { immediate: true })

// ================= 连接 =================
async function call(method: string, ...args: unknown[]) {
  if (!connection || connState.value !== 'on') { say('还没连上对战服务器，请稍等', 'error'); return }
  busy.value = true
  try {
    const err = await connection.invoke<string | null>(method, ...args)
    if (err) { say(err, 'error'); pending.value = false }
    if (method === 'Leave') void loadLobby()
  } catch { say('网络不稳定，操作没有送达', 'error'); pending.value = false } finally { busy.value = false }
}
async function loadLobby() {
  try {
    // 连上了走实时连接；没登录或还没连上，就用只读接口看看开着哪些房间
    rooms.value = connection && connState.value === 'on'
      ? await connection.invoke<LobbyRoom[]>('Lobby')
      : await request.get<LobbyRoom[]>('/game-lobby/go', { silentError: true })
  } catch { /* 忽略 */ }
}
async function connect() {
  if (!user.isLoggedIn || connection) { if (!user.isLoggedIn) connState.value = 'off'; return }
  const hubUrl = new URL('/signalr/go', new URL(import.meta.env.VITE_API_BASE_URL || '/api', window.location.origin)).href
  connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, { accessTokenFactory: () => user.token || '' })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
    .build()
  connection.on('State', (s: GoState | null) => { state.value = s; pending.value = false; if (!s) void loadLobby() })
  connection.onreconnecting(() => { connState.value = 'wait' })
  connection.onreconnected(async () => { connState.value = 'on'; state.value = await connection!.invoke<GoState | null>('Resume'); void loadLobby() })
  connection.onclose(() => { connState.value = 'off' })
  try {
    await connection.start()
    connState.value = 'on'
    state.value = await connection.invoke<GoState | null>('Resume')
    await loadLobby()
  } catch { connState.value = 'off' }
}

// ================= 棋盘数据 =================
const N = computed(() => state.value?.size ?? 19)
const toGrid = (s: string, map: (ch: string) => number) => {
  const n = N.value
  return Array.from({ length: n }, (_, r) => Array.from({ length: n }, (_, c) => map(s[r * n + c] ?? '.')))
}
const grid = computed(() => toGrid(state.value?.board ?? '', ch => (ch === 'b' ? 1 : ch === 'w' ? 2 : 0)))
const territory = computed(() => toGrid(state.value?.score?.owner ?? '', ch => (ch === 'b' ? 1 : ch === 'w' ? 2 : 0)))
const toPoint = (i: number): [number, number] => [Math.floor(i / N.value), i % N.value]
const deadPoints = computed(() => (state.value?.dead ?? []).map(toPoint))
const lastStone = computed<[number, number] | null>(() => {
  const moves = state.value?.moves ?? []
  const m = moves.length ? moves[moves.length - 1] : undefined
  return m && m.index >= 0 ? toPoint(m.index) : null
})
const coord = (i: number) => { const [r, c] = toPoint(i); return `${LETTERS[c] ?? c + 1}${N.value - r}` }

const myNum = computed(() => (state.value?.myColor === 'b' ? 1 : state.value?.myColor === 'w' ? 2 : 0))
const myTurn = computed(() => state.value?.phase === 'playing' && !!state.value.myColor && state.value.turn === state.value.myColor)
const myConfirmed = computed(() => !!state.value?.myColor && !!state.value.confirmed[state.value.myColor])
const lastPassByMe = computed(() => {
  const moves = state.value?.moves ?? []
  const m = moves.length ? moves[moves.length - 1] : undefined
  return !!m && m.index < 0 && m.color === state.value?.myColor
})
const secondsLeft = computed(() => { const d = state.value?.deadline; return d ? Math.max(0, Math.ceil((new Date(d).getTime() - now.value) / 1000)) : 0 })

const seats = computed(() => {
  const s = state.value
  if (!s) return []
  return COLORS.map(color => {
    const player = color === 'b' ? s.black : s.white
    return { color, player, isMe: !!player && same(player.userId, String(user.userId ?? '')) }
  })
})
// 对局时自己那一方显示在下面
const sideSeats = computed(() => (state.value?.myColor === 'b' ? [...seats.value].reverse() : seats.value))

const leaderText = computed(() => {
  const sc = state.value?.score
  if (!sc) return ''
  const d = sc.black - sc.white
  return d > 0 ? `黑方领先 ${d} 子` : `白方领先 ${-d} 子`
})
const confirmText = computed(() => {
  const s = state.value
  if (!s?.myColor) return ''
  const mine = s.confirmed[s.myColor], theirs = s.confirmed[s.myColor === 'b' ? 'w' : 'b']
  return mine && theirs ? '双方已确认' : mine ? '你已确认，等待对手' : theirs ? '对手已确认，等你确认' : '双方都还没确认'
})
const resultTitle = computed(() => {
  const r = state.value?.result
  if (!r) return ''
  if (!r.winner) return '和棋'
  if (!state.value?.myColor) return r.winner === 'b' ? '黑方胜' : '白方胜'
  return r.winner === state.value.myColor ? '你赢了！' : '你输了'
})
const statusKind = computed(() => {
  const s = state.value
  if (!s) return ''
  if (s.phase === 'ended') return !s.result?.winner ? 'draw' : s.result.winner === s.myColor ? 'win' : 'lose'
  if (s.phase === 'scoring') return 'scoring'
  return myTurn.value ? 'mine' : ''
})

// ================= 操作 =================
async function onBoard(r: number, c: number) {
  const s = state.value
  if (!s) return
  const idx = r * s.size + c
  if (s.phase === 'scoring') { if (s.board[idx] !== '.') await call('ToggleDead', idx); return }
  if (!myTurn.value || pending.value || s.board[idx] !== '.') return
  pending.value = true
  await call('Play', idx)
  window.setTimeout(() => { pending.value = false }, 3000)
}
async function passOnline() {
  const s = state.value
  if (!s) return
  if (s.passes === 1 && !window.confirm('对手刚停了一手，你再停一手就进入点目。确定停一手吗？')) return
  await call('Pass')
}
async function resignOnline() { if (window.confirm('确定认输吗？这局会记为负。')) await call('Resign') }
async function leaveOnline() { if (window.confirm('对局还没结束，离开会判负。确定离开吗？')) await call('Leave') }
async function copyId() {
  if (!state.value) return
  try { await navigator.clipboard.writeText(state.value.roomId); copied.value = true; window.setTimeout(() => { copied.value = false }, 1500) } catch { say(`房间号：${state.value.roomId}`) }
}

watch(() => state.value?.phase, (p, old) => {
  if (p === 'playing' && old === 'waiting') say(state.value?.myColor === 'b' ? '对局开始，你执黑先下' : '对局开始，你执白后下')
  else if (p === 'scoring' && old === 'playing') say('双方都停了一手，进入点目：把死子点出来，然后确认结果')
  else if (p === 'playing' && old === 'scoring') say('回到对局，继续下')
})
// 同一个房间里从一个人变成两个人时提示房主（刚打开页面恢复状态时不提示）
watch(() => (!state.value ? null : state.value.black && state.value.white ? `${state.value.black.userId}|${state.value.white.userId}` : ''), (v, old) => {
  if (v && old === '' && state.value?.phase === 'waiting' && amOwner.value) say('对手已加入，可以开始了')
})
watch(() => state.value?.moves.length ?? 0, () => void nextTick(() => { if (logEl.value) logEl.value.scrollTop = logEl.value.scrollHeight }))

// ================= 生命周期 =================
onMounted(async () => {
  await connect()
  if (connState.value !== 'on') void loadLobby()
  lobbyTimer = window.setInterval(() => { if (!state.value && !document.hidden) void loadLobby() }, 5000)
  clockTimer = window.setInterval(() => { now.value = Date.now() }, 500)
})
onBeforeUnmount(() => {
  window.clearInterval(lobbyTimer); window.clearInterval(clockTimer); window.clearTimeout(noticeTimer)
  void connection?.stop()
})

function isPlaying() { return state.value?.phase === 'playing' || state.value?.phase === 'scoring' }
async function handleBeforeLeave() {
  if (isPlaying()) {
    if (!window.confirm('联机对局还没结束，离开会判负。确定离开吗？')) return false
    await call('Leave')
  } else if (state.value) await call('Leave')
  return true
}
defineExpose({ handleBeforeLeave, isPlaying, say })
</script>

<style scoped>
.go-online { display: grid; gap: 18px; }
.panel-tag { margin: 0 0 6px; font: 10px/1.2 var(--font-mono, monospace); letter-spacing: .16em; color: var(--accent-color); }
.go-notice { display: flex; justify-content: space-between; gap: 12px; margin: 0; padding: 10px 14px; border-left: 3px solid var(--accent-color); background: color-mix(in srgb, var(--accent-color) 8%, var(--paper-card)); font-size: 13px; }
.go-notice.error { border-left-color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--paper-card)); }
.go-notice button { border: 0; background: none; color: var(--text-secondary); cursor: pointer; font-size: 16px; line-height: 1; }
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
.form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
h3 { margin: 0; font-size: 14px; }
label { display: grid; gap: 5px; font-size: 12px; color: var(--text-secondary); }
input, select { padding: 8px 10px; border: 1px solid var(--border-line); background: var(--bg-main); color: var(--text-primary); font: inherit; font-size: 14px; }
.join-row { display: flex; gap: 8px; }
.join-row input { flex: 1; min-width: 0; font-family: var(--font-mono, monospace); }
.btn { padding: 8px 16px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 14px; cursor: pointer; }
.btn.small { padding: 5px 12px; font-size: 13px; }
.btn.primary { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent, #fff); font-weight: 600; }
.btn.danger { color: var(--danger); }
.btn:disabled { opacity: .5; cursor: not-allowed; }
.rooms-panel { display: grid; gap: 14px; }
.rooms-head { display: flex; justify-content: space-between; align-items: center; }
.rooms-head .panel-tag { margin: 0; }
.rooms { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.rooms li { display: flex; align-items: center; gap: 12px; padding: 12px 14px; border: 1px solid var(--border-line); border-left: 3px solid var(--accent-color); background: var(--bg-main); }
.rooms li.busy { border-left-color: var(--border-line); opacity: .75; }
.size-chip { flex-shrink: 0; display: grid; place-items: center; width: 40px; height: 40px; border: 1px solid var(--border-line); font: 700 16px/1 var(--font-mono, monospace); color: var(--accent-color); }
.size-chip small { font: 9px var(--font-mono, monospace); color: var(--text-secondary); }
.room-main { display: grid; gap: 3px; min-width: 0; flex: 1; }
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
.seat:not(.filled) .stone { opacity: .3; }
.stone { flex-shrink: 0; width: 32px; height: 32px; border-radius: 50%; }
.stone.black { background: radial-gradient(circle at 35% 32%, #4a5560, #07090c 75%); }
.stone.white { background: radial-gradient(circle at 35% 30%, #fff, #c9d2d8 85%); border: 1px solid #8f9ba4; }
.waiting-meta { margin: 0; font: 12px var(--font-mono, monospace); color: var(--text-secondary); }
.waiting-actions { display: flex; justify-content: flex-end; align-items: center; gap: 10px; padding-top: 14px; border-top: 1px solid var(--border-line); }

.match { display: grid; grid-template-columns: minmax(0, 1fr) 300px; gap: 20px; align-items: start; }
.board-col { width: 100%; max-width: min(100%, calc(100dvh - 190px)); min-width: 280px; justify-self: center; }
.side { display: grid; gap: 14px; position: sticky; top: 0; }
.players { display: grid; gap: 8px; padding: 14px; border: 1px solid var(--border-line); background: var(--paper-card); }
.player { display: flex; align-items: center; gap: 12px; padding: 10px; border: 1px solid transparent; }
.player.active { border-color: var(--accent-color); background: color-mix(in srgb, var(--accent-color) 7%, transparent); }
.player .stone { width: 28px; height: 28px; }
.p-main { display: grid; gap: 2px; min-width: 0; flex: 1; }
.p-main small { font: 10px var(--font-mono, monospace); letter-spacing: .12em; color: var(--text-secondary); }
.p-main b { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.clock { font: 700 18px var(--font-mono, monospace); color: var(--accent-color); font-variant-numeric: tabular-nums; }
.clock.low { color: var(--danger); }
.cap { font: 12px var(--font-mono, monospace); color: var(--text-secondary); font-variant-numeric: tabular-nums; }
.timebar { height: 3px; background: var(--bg-sub); }
.timebar span { display: block; height: 100%; background: var(--signal-mint, #2dd4bf); transition: width .5s linear; }
.timebar span.low { background: var(--danger); }
.status { display: grid; gap: 4px; padding: 14px; border: 1px solid var(--border-line); border-left: 3px solid var(--border-line); background: var(--paper-card); }
.status b { font-size: 16px; }
.status span { font-size: 12px; color: var(--text-secondary); line-height: 1.6; }
.status .warn { color: #d97706; }
.status.mine { border-left-color: var(--accent-color); }
.status.scoring { border-left-color: #d97706; }
.status.win { border-left-color: var(--signal-mint, #2dd4bf); background: color-mix(in srgb, var(--signal-mint, #2dd4bf) 10%, var(--paper-card)); }
.status.lose { border-left-color: var(--danger); }
.status.draw { border-left-color: #d97706; }
.actions { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.log { padding: 12px 14px; border: 1px solid var(--border-line); background: var(--paper-card); }
.log-tag { margin: 0 0 8px; font: 10px var(--font-mono, monospace); letter-spacing: .14em; color: var(--accent-color); }
.log ol { list-style: none; margin: 0; padding: 0; max-height: 220px; overflow: auto; display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 2px 10px; font: 12px var(--font-mono, monospace); }
.log li { display: flex; gap: 6px; align-items: baseline; }
.log li span { width: 2.6em; color: var(--text-secondary); text-align: right; }
.log li small { margin-left: auto; color: var(--danger); font-size: 10px; }

@media (max-width: 980px) {
  .match { grid-template-columns: 1fr; }
  .board-col { max-width: 100%; }
  .side { position: static; }
}
@media (max-width: 860px) { .lobby { grid-template-columns: 1fr; } }
@media (max-width: 520px) { .seats { grid-template-columns: 1fr; } .panel { padding: 16px; } .form-row { grid-template-columns: 1fr; } }
</style>
