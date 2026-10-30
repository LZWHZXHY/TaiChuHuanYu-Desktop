<!-- GoLocal.vue：围棋本地双人：9 / 13 / 19 路，提子、禁着点、打劫、悔棋、停一手、点目（标记死子）、中国规则数子（贴 7.5 目）。 -->
<template>
  <div class="go-local">

    <div class="layout">
      <div class="board-col">
        <LabBoard
          :size="N"
          :stones="board"
          :last-move="phase === 'play' ? lastMove : null"
          :ghost="phase === 'play' ? current : 0"
          :disabled="phase === 'over'"
          :pick-stones="phase === 'scoring'"
          :dead="phase !== 'play' ? deadList : null"
          :territory="phase !== 'play' ? scoreInfo.owner : null"
          @play="onBoardClick"
        />
      </div>

      <aside class="side">
        <div class="size-switch" role="radiogroup" aria-label="棋盘大小">
          <span>棋盘</span>
          <button v-for="s in SIZES" :key="s" type="button" role="radio" :aria-checked="N === s" :class="{ on: N === s }" @click="changeSize(s)">{{ s }} 路</button>
        </div>
        <div class="players">
          <div v-for="c in [1, 2]" :key="c" class="player" :class="{ active: phase === 'play' && current === c }">
            <span class="stone" :class="c === 1 ? 'black' : 'white'" aria-hidden="true"></span>
            <div class="p-main">
              <small>{{ c === 1 ? 'BLACK · 先手' : `WHITE · 贴 ${KOMI} 目` }}</small>
              <b>{{ c === 1 ? (userStore.userInfo?.username || '黑方') : '白方' }}</b>
            </div>
            <span class="cap" title="提子数">提 {{ captures[c] }}</span>
          </div>
        </div>

        <div class="status" :class="statusKind" role="status">
          <template v-if="phase === 'play'">
            <b>{{ current === 1 ? '黑方落子' : '白方落子' }}</b>
            <span>第 {{ moves.length + 1 }} 手<template v-if="passes === 1"> · 对方刚停了一手，再停一手就进入点目</template></span>
            <span v-if="hint" class="hint">{{ hint }}</span>
          </template>
          <template v-else-if="phase === 'scoring'">
            <b>点目：点击死子把整块标记出来</b>
            <span>黑 {{ scoreInfo.black }} ：白 {{ scoreInfo.white }}（含贴目） · {{ leaderText }}</span>
          </template>
          <template v-else>
            <b>{{ resultText }}</b>
            <span v-if="finalScore">黑 {{ finalScore.black }} ：白 {{ finalScore.white }}（中国规则数子，白贴 {{ KOMI }} 目）</span>
          </template>
        </div>

        <div class="actions">
          <template v-if="phase === 'play'">
            <button type="button" class="btn" @click="pass">停一手</button>
            <button type="button" class="btn" :disabled="!history.length" @click="undo">悔棋</button>
            <button type="button" class="btn danger" :disabled="!moves.length" @click="resign">{{ current === 1 ? '黑方' : '白方' }}认输</button>
          </template>
          <template v-else-if="phase === 'scoring'">
            <button type="button" class="btn primary" @click="confirmScore">确认结果</button>
            <button type="button" class="btn" @click="resumePlay">继续下</button>
          </template>
          <template v-else>
            <button type="button" class="btn primary" @click="reset()">再来一局</button>
            <button type="button" class="btn" :disabled="saved || saving || !userStore.isLoggedIn" :title="userStore.isLoggedIn ? '' : '登录后对局会自动记入战绩'" @click="saveRecord">{{ saved ? '已记入战绩' : saving ? '记录中…' : '记入战绩' }}</button>
          </template>
          <button v-if="phase === 'play' && moves.length" type="button" class="btn ghost" @click="reset(true)">重新开始</button>
        </div>

        <div class="log">
          <p class="log-tag">MOVES / 棋谱 · {{ moves.length }} 手</p>
          <ol ref="logEl">
            <li v-for="(m, i) in moves" :key="i"><span>{{ i + 1 }}</span>{{ m.color === 1 ? '黑' : '白' }} {{ m.r < 0 ? '停一手' : coord(m.r, m.c) }}</li>
          </ol>
        </div>
        <p class="note">两人在同一台电脑上轮流下。本地对局会记入战绩，但不计入胜率和排行榜。</p>
      </aside>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { useUserStore } from '@/stores/user'
import { gameRecordApi } from '@/api/gameRecord'
import LabBoard from '../LabBoard.vue'

const SIZES = [9, 13, 19] as const
const KOMI = 7.5
const LETTERS = 'ABCDEFGHJKLMNOPQRSTUVWXYZ'

type Grid = number[][]
interface Move { r: number; c: number; color: number }
interface Snapshot { board: Grid; current: number; captures: Record<number, number>; ko: string | null; passes: number; lastMove: [number, number] | null }

const userStore = useUserStore()
const N = ref<number>(19)
const empty = (n: number): Grid => Array.from({ length: n }, () => Array<number>(n).fill(0))
const board = ref<Grid>(empty(N.value))
const current = ref(1)
const captures = ref<Record<number, number>>({ 1: 0, 2: 0 })
const passes = ref(0)
const moves = ref<Move[]>([])
const lastMove = ref<[number, number] | null>(null)
/** 打劫：上一手单提一子后，对方不能马上在这个点提回 */
const ko = ref<string | null>(null)
const history = ref<Snapshot[]>([])
const phase = ref<'play' | 'scoring' | 'over'>('play')
const dead = ref<Set<string>>(new Set())
const finalScore = ref<{ black: number; white: number } | null>(null)
const resignedBy = ref(0)
const saved = ref(false), saving = ref(false)
const hint = ref('')
const logEl = ref<HTMLOListElement | null>(null)

const key = (r: number, c: number) => `${r},${c}`
const coord = (r: number, c: number) => `${LETTERS[c] ?? c + 1}${N.value - r}`
const opponent = (c: number) => (c === 1 ? 2 : 1)
const clone = (g: Grid) => g.map(row => row.slice())

function neighbors(r: number, c: number): [number, number][] {
  const n = N.value
  return ([[r - 1, c], [r + 1, c], [r, c - 1], [r, c + 1]] as [number, number][]).filter(([x, y]) => x >= 0 && x < n && y >= 0 && y < n)
}
function groupOf(g: Grid, r: number, c: number) {
  const color = g[r]![c]!
  const stones: [number, number][] = []
  let liberties = 0
  const seen = new Set<string>(), libs = new Set<string>()
  const stack: [number, number][] = [[r, c]]
  while (stack.length) {
    const [x, y] = stack.pop()!
    const k = key(x, y)
    if (seen.has(k)) continue
    seen.add(k)
    stones.push([x, y])
    for (const [nx, ny] of neighbors(x, y)) {
      const v = g[nx]![ny]!
      if (v === 0) { const lk = key(nx, ny); if (!libs.has(lk)) { libs.add(lk); liberties++ } }
      else if (v === color && !seen.has(key(nx, ny))) stack.push([nx, ny])
    }
  }
  return { stones, liberties }
}

let hintTimer = 0
function flash(text: string) {
  hint.value = text
  window.clearTimeout(hintTimer)
  hintTimer = window.setTimeout(() => { hint.value = '' }, 2500)
}

function snapshot(): Snapshot {
  return { board: clone(board.value), current: current.value, captures: { ...captures.value }, ko: ko.value, passes: passes.value, lastMove: lastMove.value }
}

function placeStone(r: number, c: number) {
  if (board.value[r]![c]) return
  if (ko.value === key(r, c)) return flash('打劫：不能马上提回，先在别处下一手')
  const color = current.value
  const g = clone(board.value)
  g[r]![c] = color
  let captured: [number, number][] = []
  for (const [nx, ny] of neighbors(r, c)) {
    if (g[nx]![ny] === opponent(color)) {
      const grp = groupOf(g, nx, ny)
      if (grp.liberties === 0) { for (const [x, y] of grp.stones) g[x]![y] = 0; captured = captured.concat(grp.stones) }
    }
  }
  if (groupOf(g, r, c).liberties === 0) return flash('禁着点：这里落子没有气')

  history.value.push(snapshot())
  // 单提一子、且落下的子自己也只有一口气、只有一个子 → 形成劫
  const own = groupOf(g, r, c)
  ko.value = captured.length === 1 && own.stones.length === 1 && own.liberties === 1 ? key(captured[0]![0], captured[0]![1]) : null
  board.value = g
  captures.value = { ...captures.value, [color]: captures.value[color]! + captured.length }
  passes.value = 0
  moves.value.push({ r, c, color })
  lastMove.value = [r, c]
  current.value = opponent(color)
  scrollLog()
}

function pass() {
  history.value.push(snapshot())
  moves.value.push({ r: -1, c: -1, color: current.value })
  passes.value++
  ko.value = null
  lastMove.value = null
  current.value = opponent(current.value)
  scrollLog()
  if (passes.value >= 2) { phase.value = 'scoring'; dead.value = new Set() }
}

function undo() {
  const s = history.value.pop()
  if (!s) return
  board.value = s.board
  current.value = s.current
  captures.value = s.captures
  ko.value = s.ko
  passes.value = s.passes
  lastMove.value = s.lastMove
  moves.value.pop()
}

function resign() {
  if (!window.confirm(`${current.value === 1 ? '黑方' : '白方'}确定认输吗？`)) return
  resignedBy.value = current.value
  finalScore.value = null
  phase.value = 'over'
}

function onBoardClick(r: number, c: number) {
  if (phase.value === 'play') placeStone(r, c)
  else if (phase.value === 'scoring' && board.value[r]![c]) {
    // 点目：整块切换死活
    const grp = groupOf(board.value, r, c).stones.map(([x, y]) => key(x, y))
    const next = new Set(dead.value)
    const isDead = next.has(grp[0]!)
    grp.forEach(k => (isDead ? next.delete(k) : next.add(k)))
    dead.value = next
  }
}

const deadList = computed(() => [...dead.value].map(k => k.split(',').map(Number) as [number, number]))

/** 中国规则数子：活子 + 只被一方包围的空点（死子当作对方的地）；白方贴目 */
const scoreInfo = computed(() => {
  const n = N.value
  const g = board.value.map((row, r) => row.map((v, c) => (dead.value.has(key(r, c)) ? 0 : v)))
  const owner = empty(n)
  let black = 0, white = 0
  const seen = new Set<string>()
  for (let r = 0; r < n; r++) {
    for (let c = 0; c < n; c++) {
      const v = g[r]![c]!
      if (v === 1) { black++; continue }
      if (v === 2) { white++; continue }
      if (seen.has(key(r, c))) continue
      const region: [number, number][] = []
      let touchB = false, touchW = false
      const stack: [number, number][] = [[r, c]]
      while (stack.length) {
        const [x, y] = stack.pop()!
        const k = key(x, y)
        if (seen.has(k)) continue
        seen.add(k)
        region.push([x, y])
        for (const [nx, ny] of neighbors(x, y)) {
          const w = g[nx]![ny]!
          if (w === 0) stack.push([nx, ny])
          else if (w === 1) touchB = true
          else touchW = true
        }
      }
      const who = touchB && !touchW ? 1 : touchW && !touchB ? 2 : 0
      if (who) region.forEach(([x, y]) => { owner[x]![y] = who })
      if (who === 1) black += region.length
      else if (who === 2) white += region.length
    }
  }
  return { black, white: white + KOMI, owner }
})
const leaderText = computed(() => {
  const d = scoreInfo.value.black - scoreInfo.value.white
  return d > 0 ? `黑方领先 ${d} 子` : `白方领先 ${-d} 子`
})

function confirmScore() {
  finalScore.value = { black: scoreInfo.value.black, white: scoreInfo.value.white }
  phase.value = 'over'
}
function resumePlay() {
  phase.value = 'play'
  dead.value = new Set()
  passes.value = 0
}

const winner = computed(() => {
  if (phase.value !== 'over') return 0
  if (resignedBy.value) return opponent(resignedBy.value)
  const s = finalScore.value
  return s ? (s.black > s.white ? 1 : 2) : 0
})
const resultText = computed(() => {
  if (resignedBy.value) return `${resignedBy.value === 1 ? '黑方' : '白方'}认输，${winner.value === 1 ? '黑方' : '白方'}中盘胜`
  const s = finalScore.value
  if (!s) return '对局结束'
  const diff = Math.abs(s.black - s.white)
  return `${winner.value === 1 ? '黑方' : '白方'}胜 ${diff} 子`
})
const statusKind = computed(() => (phase.value === 'over' ? 'over' : phase.value === 'scoring' ? 'scoring' : ''))

function scrollLog() { void nextTick(() => { if (logEl.value) logEl.value.scrollTop = logEl.value.scrollHeight }) }

function reset(ask = false) {
  if (ask && moves.value.length && !window.confirm('重新开始会清空这盘棋，确定吗？')) return
  board.value = empty(N.value)
  current.value = 1
  captures.value = { 1: 0, 2: 0 }
  passes.value = 0
  moves.value = []
  lastMove.value = null
  ko.value = null
  history.value = []
  phase.value = 'play'
  dead.value = new Set()
  finalScore.value = null
  resignedBy.value = 0
  saved.value = false
}
function changeSize(s: number) {
  if (s === N.value) return
  if (moves.value.length && phase.value !== 'over' && !window.confirm(`换成 ${s} 路会清空当前棋局，确定吗？`)) return
  N.value = s
  reset()
}

// 对局结束自动记入战绩（登录时）；失败时可以手动点「记入战绩」重试
watch(phase, p => { if (p === 'over' && userStore.isLoggedIn) void saveRecord().catch(() => undefined) })

async function saveRecord() {
  if (saved.value || saving.value || phase.value !== 'over') return
  saving.value = true
  try {
    await gameRecordApi.save({
      gameType: 'go',
      player1Name: userStore.userInfo?.username || '黑方',
      player2Name: '白方',
      winner: winner.value === 1 ? 'player1' : winner.value === 2 ? 'player2' : 'draw',
      movesCount: moves.value.filter(m => m.r >= 0).length,
      movesJson: JSON.stringify({ size: N.value, komi: KOMI, moves: moves.value, score: finalScore.value, resignedBy: resignedBy.value || null }),
    })
    saved.value = true
  } finally { saving.value = false }
}
</script>

<style scoped>
.go-local { display: grid; color: var(--text-primary); }
.size-switch { display: flex; align-items: stretch; border: 1px solid var(--border-line); background: var(--paper-card); }
.size-switch span { display: grid; place-items: center; padding: 0 12px; border-right: 1px solid var(--border-line); font: 10px var(--font-mono, monospace); letter-spacing: .12em; color: var(--text-secondary); }
.size-switch button { flex: 1; padding: 7px 10px; border: 0; border-right: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.size-switch button:last-child { border-right: 0; }
.size-switch button.on { background: var(--accent-color); color: var(--on-accent, #fff); font-weight: 600; }

.layout { display: grid; grid-template-columns: minmax(0, 1fr) 300px; gap: 20px; align-items: start; }
.board-col { width: 100%; max-width: min(100%, calc(100dvh - 190px)); min-width: 280px; justify-self: center; }
.side { display: grid; gap: 14px; position: sticky; top: 0; }
.players { display: grid; gap: 8px; padding: 14px; border: 1px solid var(--border-line); background: var(--paper-card); }
.player { display: flex; align-items: center; gap: 12px; padding: 10px; border: 1px solid transparent; }
.player.active { border-color: var(--accent-color); background: color-mix(in srgb, var(--accent-color) 7%, transparent); }
.p-main { display: grid; gap: 2px; min-width: 0; flex: 1; }
.p-main small { font: 10px var(--font-mono, monospace); letter-spacing: .12em; color: var(--text-secondary); }
.p-main b { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.cap { font: 12px var(--font-mono, monospace); color: var(--text-secondary); font-variant-numeric: tabular-nums; }
.stone { flex-shrink: 0; width: 28px; height: 28px; border-radius: 50%; }
.stone.black { background: radial-gradient(circle at 35% 32%, #4a5560, #07090c 75%); }
.stone.white { background: radial-gradient(circle at 35% 30%, #fff, #c9d2d8 85%); border: 1px solid #8f9ba4; }

.status { display: grid; gap: 4px; padding: 14px; border: 1px solid var(--border-line); border-left: 3px solid var(--accent-color); background: var(--paper-card); }
.status b { font-size: 16px; }
.status span { font-size: 12px; color: var(--text-secondary); line-height: 1.6; }
.status .hint { color: var(--danger); }
.status.scoring { border-left-color: #d97706; }
.status.over { border-left-color: var(--signal-mint, #2dd4bf); background: color-mix(in srgb, var(--signal-mint, #2dd4bf) 8%, var(--paper-card)); }
.actions { display: flex; flex-wrap: wrap; gap: 8px; }
.btn { padding: 8px 16px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 14px; cursor: pointer; }
.btn.primary { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent, #fff); font-weight: 600; }
.btn.danger { color: var(--danger); }
.btn.ghost { color: var(--text-secondary); }
.btn:disabled { opacity: .5; cursor: not-allowed; }

.log { padding: 12px 14px; border: 1px solid var(--border-line); background: var(--paper-card); }
.log-tag { margin: 0 0 8px; font: 10px var(--font-mono, monospace); letter-spacing: .14em; color: var(--accent-color); }
.log ol { list-style: none; margin: 0; padding: 0; max-height: 200px; overflow: auto; display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 2px 10px; font: 12px var(--font-mono, monospace); }
.log li { display: flex; gap: 6px; }
.log li span { width: 2.6em; color: var(--text-secondary); text-align: right; }
.note { margin: 0; font-size: 12px; color: var(--text-secondary); line-height: 1.6; }

@media (max-width: 980px) {
  .layout { grid-template-columns: 1fr; }
  .board-col { max-width: 100%; }
  .side { position: static; }
}
</style>
