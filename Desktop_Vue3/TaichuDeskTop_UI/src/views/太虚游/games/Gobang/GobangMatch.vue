<!-- GobangMatch.vue：联机五子棋对局界面。棋盘由服务器推送驱动（MoveMade / GameOver / TurnChanged），自己只负责发落子请求。
     spectating（观战）时只读：不能落子、没有认输按钮，结果显示「黑方胜 / 白方胜」。 -->
<template>
  <div class="match">
    <div class="board-col" :class="{ timed: !result }">
      <TurnClock v-if="!result" :seconds="remaining" :total="room.timeLimit" :mine="myTurn" :label="turnLabel" :show-sound="!spectating" />
      <LabBoard
        :size="room.boardSize"
        :stones="board"
        :last-move="lastMove"
        :highlight="winLine"
        :ghost="myTurn ? myColor : 0"
        :disabled="!myTurn || pending"
        @play="play"
      />
    </div>

    <aside class="side">
      <!-- 双方 -->
      <div class="players">
        <div v-for="p in seats" :key="p.color" class="player" :class="{ active: turnColor === p.color && !result, me: p.isMe }">
          <span class="stone" :class="p.color === 1 ? 'black' : 'white'" aria-hidden="true"></span>
          <div class="p-main">
            <small>{{ p.color === 1 ? 'BLACK · 先手' : 'WHITE · 后手' }}</small>
            <b>{{ p.name }}<UserTitle v-if="p.id" :user-id="p.id" /><em v-if="p.isMe">你</em></b>
          </div>
          <span v-if="turnColor === p.color && !result" class="clock" :class="{ low: remaining <= 5 }">{{ remaining }}s</span>
        </div>
      </div>

      <!-- 状态 -->
      <div class="status" :class="statusKind" role="status">
        <template v-if="result">
          <b>{{ resultTitle }}</b>
          <!-- 写清楚哪方赢、谁赢的：「黑方胜（五子连珠）· 某某」 -->
          <span>{{ resultLine }}</span>
        </template>
        <template v-else>
          <b>{{ spectating ? `观战中 · 轮到${turnColor === 1 ? '黑方' : '白方'}` : myTurn ? '轮到你落子' : '等待对手落子…' }}</b>
          <span>第 {{ moveBase + moves.length + 1 }} 手 · 每步 {{ room.timeLimit }} 秒</span>
        </template>
      </div>

      <!-- 操作 -->
      <div class="actions">
        <!-- 观战：只有「不看了」 -->
        <template v-if="spectating">
          <span v-if="result" class="wait-tip">{{ opponentHere ? '等房主开始下一局，开局后会接着看' : '有人离开了' }}</span>
          <button type="button" class="btn" @click="emit('leave')">不看了</button>
        </template>
        <template v-else-if="!result">
          <button type="button" class="btn danger" @click="resign">认输</button>
          <button type="button" class="btn" @click="leave">离开</button>
        </template>
        <template v-else>
          <button v-if="amOwner && opponentHere" type="button" class="btn primary" @click="emit('rematch')">再来一局</button>
          <span v-else-if="opponentHere" class="wait-tip">等待房主开始下一局</span>
          <span v-else class="wait-tip">对手已离开</span>
          <button type="button" class="btn" @click="emit('leave')">返回大厅</button>
        </template>
      </div>

      <!-- 棋谱 -->
      <div class="log">
        <p class="log-tag">MOVES / 棋谱 · {{ moveBase + moves.length }} 手</p>
        <ol ref="logEl">
          <li v-if="moveBase" class="log-skip">（进来之前已经下了 {{ moveBase }} 手）</li>
          <li v-for="(m, i) in moves" :key="i" :class="m.color === 1 ? 'b' : 'w'"><span>{{ moveBase + i + 1 }}</span>{{ m.color === 1 ? '黑' : '白' }} {{ coord(m.row, m.col) }}</li>
        </ol>
      </div>
    </aside>
  </div>
</template>

<script setup lang="ts">
import { computed, inject, nextTick, onMounted, onUnmounted, ref, type Ref } from 'vue'
import LabBoard from '../LabBoard.vue'
import UserTitle from '@/components/UserTitle.vue'
import TurnClock from '../TurnClock.vue'
import { useTurnAlert } from '../turnAlert'
import type { GobangRoom, GobangStart } from './gobangTypes'

const props = withDefaults(defineProps<{ room: GobangRoom; start: GobangStart; myId: string; spectating?: boolean }>(), { spectating: false })
const emit = defineEmits<{ rematch: []; leave: []; error: [message: string] }>()

interface SignalRApi { connection: Ref<{ invoke: (method: string, ...args: unknown[]) => Promise<unknown> } | null> }
const signalR = inject<SignalRApi | null>('signalR', null)

const same = (a?: string | null, b?: string | null) => !!a && !!b && a.toLowerCase() === b.toLowerCase()
const N = props.room.boardSize
// 观战中途进来：用服务器给的当前棋盘；正常开局是空棋盘
const board = ref<number[][]>(Array.from({ length: N }, (_, r) => Array.from({ length: N }, (_, c) => props.start.board?.[r]?.[c] ?? 0)))
/** 观战进来之前已经下了几手（棋谱从这里接着编号） */
const moveBase = props.start.moveCount ?? 0
const moves = ref<{ row: number; col: number; color: number }[]>([])
const lastMove = ref<[number, number] | null>(null)
const winLine = ref<[number, number][] | null>(null)
const turn = ref<string | null>(props.start.currentTurn)
const deadline = ref(Date.now() + (props.start.remainingTime ?? props.room.timeLimit) * 1000)
const now = ref(Date.now())
const pending = ref(false)
const result = ref<{ winner: string | null; reason: string } | null>(null)
const logEl = ref<HTMLOListElement | null>(null)

// 这一局谁执黑：开局时的房主。中途房主离开会把房主交给对手，如果跟着变，黑白和结果就会显示反
const blackId = props.room.ownerId
const colorOf = (id: string | null | undefined) => (id ? (same(id, blackId) ? 1 : 2) : 0)
const myColor = computed(() => (props.spectating ? 0 : colorOf(props.myId)))
const amOwner = computed(() => same(props.myId, props.room.ownerId))
const myTurn = computed(() => !props.spectating && !result.value && same(turn.value, props.myId))
const turnColor = computed(() => colorOf(turn.value))
const remaining = computed(() => Math.max(0, Math.ceil((deadline.value - now.value) / 1000)))
const opponentHere = computed(() => props.room.players.length >= 2)
const inProgress = computed(() => !props.spectating && !result.value)

const seats = computed(() => {
  const owner = props.room.players.find(p => same(p.id, blackId))
  const guest = props.room.players.find(p => !same(p.id, blackId))
  return [
    { color: 1, name: owner?.name ?? '黑方', id: owner?.id ?? null, isMe: !!owner && same(owner.id, props.myId) },
    { color: 2, name: guest?.name ?? '（已离开）', id: guest?.id ?? null, isMe: !!guest && same(guest.id, props.myId) },
  ]
})

// 回合倒计时：轮到自己响一声、最后 10 秒提醒、后台时标签页标题闪烁
const turnLabel = computed(() => {
  const side = turnColor.value === 1 ? '黑方' : '白方'
  const name = seats.value.find(s => s.color === turnColor.value)?.name ?? side
  if (props.spectating) return `观战中 · ${side}（${name}）落子`
  if (myTurn.value) return '轮到你落子'
  return `等待 ${name} 落子…`
})
useTurnAlert({ mine: () => myTurn.value, seconds: () => remaining.value })

const resultTitle = computed(() => {
  const r = result.value
  if (!r) return ''
  if (!r.winner) return '和棋'
  if (props.spectating) return colorOf(r.winner) === 1 ? '黑方胜' : '白方胜'
  return same(r.winner, props.myId) ? '你赢了！' : '你输了'
})
/** 「黑方胜（五子连珠）· 某某」/ 和棋时就是原因 */
const resultLine = computed(() => {
  const r = result.value
  if (!r) return ''
  if (!r.winner) return r.reason
  const seat = seats.value.find(s => s.color === colorOf(r.winner))
  // 赢家已经离开房间时名字从棋局开始时的名单里找不到，就只写黑方 / 白方
  const name = props.room.players.find(p => same(p.id, r.winner))?.name ?? (seat?.id && same(seat.id, r.winner) ? seat.name : '')
  return `${colorOf(r.winner) === 1 ? '黑方' : '白方'}胜（${r.reason}）${name ? ` · ${name}` : ''}`
})
const statusKind = computed(() => {
  const r = result.value
  if (!r) return myTurn.value ? 'mine' : ''
  if (!r.winner) return 'draw'
  if (props.spectating) return 'draw'
  return same(r.winner, props.myId) ? 'win' : 'lose'
})

const LETTERS = 'ABCDEFGHJKLMNOPQRSTUVWXYZ'
const coord = (r: number, c: number) => `${LETTERS[c] ?? c + 1}${N - r}`

async function invoke(method: string, ...args: unknown[]) {
  const conn = signalR?.connection.value
  if (!conn) { emit('error', '和对战服务器的连接断开了'); return false }
  try { await conn.invoke(method, ...args); return true } catch (e) { emit('error', (e as Error)?.message || '操作失败'); return false }
}

async function play(r: number, c: number) {
  if (!myTurn.value || pending.value || board.value[r]?.[c]) return
  pending.value = true
  // 等服务器确认后才真正落子（MoveMade 推送回来）；3 秒没回音就允许重试
  const ok = await invoke('MakeMove', props.room.roomId, r, c)
  if (!ok) pending.value = false
  else window.setTimeout(() => { pending.value = false }, 3000)
}
async function resign() {
  if (!window.confirm('确定认输吗？这局会记为负。')) return
  await invoke('Resign', props.room.roomId)
}
function leave() {
  if (!window.confirm('对局还没结束，离开会判负。确定离开吗？')) return
  emit('leave')
}

// ===== 服务器事件 =====
type Detail = Record<string, unknown>
function onMove(d: Detail) {
  const row = Number(d.row), col = Number(d.col)
  if (!Number.isInteger(row) || !Number.isInteger(col) || !board.value[row]) return
  const color = Number(d.color) || colorOf(String(d.playerId ?? ''))
  board.value[row]![col] = color
  moves.value.push({ row, col, color })
  lastMove.value = [row, col]
  turn.value = d.currentTurn ? String(d.currentTurn) : null
  deadline.value = Date.now() + Number(d.remainingTime ?? props.room.timeLimit) * 1000
  pending.value = false
  void nextTick(() => { if (logEl.value) logEl.value.scrollTop = logEl.value.scrollHeight })
}
function onGameOver(d: Detail) {
  result.value = { winner: d.winner ? String(d.winner) : null, reason: String(d.reason ?? '对局结束') }
  turn.value = null
  if (Array.isArray(d.line)) winLine.value = (d.line as unknown[]).filter((p): p is [number, number] => Array.isArray(p) && p.length === 2)
}
function onTurn(d: Detail) {
  if (result.value) return
  turn.value = d.currentTurn ? String(d.currentTurn) : null
  deadline.value = Date.now() + props.room.timeLimit * 1000
}
const listeners: [string, (d: Detail) => void][] = [['move-made', onMove], ['game-over', onGameOver], ['turn-changed', onTurn]]
const bound = listeners.map(([name, fn]) => [name, (e: Event) => fn(((e as CustomEvent).detail ?? {}) as Detail)] as const)

let tick = 0
onMounted(() => {
  bound.forEach(([name, fn]) => window.addEventListener(name, fn))
  tick = window.setInterval(() => { now.value = Date.now() }, 250)
})
onUnmounted(() => {
  bound.forEach(([name, fn]) => window.removeEventListener(name, fn))
  window.clearInterval(tick)
})

defineExpose({ inProgress })
</script>

<style scoped>
.match { display: grid; grid-template-columns: minmax(0, 1fr) 300px; gap: 20px; align-items: start; }
.board-col { width: 100%; max-width: min(100%, calc(100dvh - 200px)); min-width: 280px; justify-self: center; }
/* 棋盘上方多了一条回合倒计时，棋盘相应让出一点高度 */
.board-col.timed { max-width: min(100%, calc(100dvh - 264px)); }
.side { display: grid; gap: 14px; position: sticky; top: 0; }
.players { display: grid; gap: 8px; padding: 14px; border: 1px solid var(--border-line); background: var(--paper-card); }
.player { display: flex; align-items: center; gap: 12px; padding: 10px; border: 1px solid transparent; }
.player.active { border-color: var(--accent-color); background: color-mix(in srgb, var(--accent-color) 7%, transparent); }
.player.me .p-main b em { margin-left: 6px; padding: 0 5px; background: var(--accent-color); color: var(--on-accent, #fff); font-size: 10px; font-style: normal; vertical-align: 2px; }
.log-skip { grid-column: 1 / -1; color: var(--text-secondary); font-size: 11px; }
.p-main { display: grid; gap: 2px; min-width: 0; flex: 1; }
.p-main small { font: 10px var(--font-mono, monospace); letter-spacing: .12em; color: var(--text-secondary); }
.p-main b { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.stone { flex-shrink: 0; width: 28px; height: 28px; border-radius: 50%; }
.stone.black { background: radial-gradient(circle at 35% 32%, #4a5560, #07090c 75%); }
.stone.white { background: radial-gradient(circle at 35% 30%, #fff, #c9d2d8 85%); border: 1px solid #8f9ba4; }
.clock { font: 700 18px var(--font-mono, monospace); color: var(--accent-color); font-variant-numeric: tabular-nums; }
.clock.low { color: var(--danger); }
.timebar { height: 3px; background: var(--bg-sub); }
.timebar span { display: block; height: 100%; background: var(--signal-mint, #2dd4bf); transition: width .25s linear; }
.timebar span.low { background: var(--danger); }

.status { display: grid; gap: 4px; padding: 14px; border: 1px solid var(--border-line); border-left: 3px solid var(--border-line); background: var(--paper-card); }
.status b { font-size: 16px; }
.status span { font-size: 12px; color: var(--text-secondary); }
.status.mine { border-left-color: var(--accent-color); }
.status.win { border-left-color: var(--signal-mint, #2dd4bf); background: color-mix(in srgb, var(--signal-mint, #2dd4bf) 10%, var(--paper-card)); }
.status.lose { border-left-color: var(--danger); }
.status.draw { border-left-color: #d97706; }
.actions { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.btn { padding: 8px 16px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 14px; cursor: pointer; }
.btn.primary { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent, #fff); font-weight: 600; }
.btn.danger { color: var(--danger); }
.wait-tip { flex: 1; font-size: 13px; color: var(--text-secondary); }

.log { padding: 12px 14px; border: 1px solid var(--border-line); background: var(--paper-card); }
.log-tag { margin: 0 0 8px; font: 10px var(--font-mono, monospace); letter-spacing: .14em; color: var(--accent-color); }
.log ol { list-style: none; margin: 0; padding: 0; max-height: 220px; overflow: auto; display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 2px 10px; font: 12px var(--font-mono, monospace); }
.log li { display: flex; gap: 6px; color: var(--text-primary); }
.log li span { width: 2.4em; color: var(--text-secondary); text-align: right; }

@media (max-width: 980px) {
  .match { grid-template-columns: 1fr; }
  .board-col, .board-col.timed { max-width: 100%; }
  .side { position: static; }
}
</style>
