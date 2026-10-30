<!-- XiangqiBoard.vue：莱茵生命风格的象棋棋盘（SVG）。选子后显示可走位置，点目标格落子；可以翻转让黑方在下。 -->
<template>
  <div class="xq-board" :class="{ disabled }" data-no-hover-sound>
    <svg :viewBox="`0 0 ${W} ${H}`" role="grid" aria-label="象棋棋盘">
      <defs>
        <radialGradient :id="`${uid}-face`" cx="40%" cy="35%" r="75%">
          <stop offset="0%" stop-color="#ffffff" />
          <stop offset="100%" stop-color="#e7ebee" />
        </radialGradient>
      </defs>
      <rect class="face" x="0" y="0" :width="W" :height="H" />
      <rect class="field" :x="PAD - 36" :y="PAD - 36" :width="8 * CELL + 72" :height="9 * CELL + 72" />
      <g class="reg"><path v-for="(d, i) in corners" :key="i" :d="d" /></g>

      <!-- 路数（各自从右往左数：红方 一~九，黑方 1~9） -->
      <g class="coord">
        <text v-for="dc in 9" :key="`t${dc}`" :x="x(dc - 1)" :y="PAD - 48" text-anchor="middle">{{ fileLabel(topSide, dc - 1) }}</text>
        <text v-for="dc in 9" :key="`b${dc}`" :x="x(dc - 1)" :y="PAD + 9 * CELL + 60" text-anchor="middle">{{ fileLabel(bottomSide, dc - 1) }}</text>
      </g>

      <!-- 棋盘线 -->
      <g class="grid">
        <line v-for="r in 10" :key="`h${r}`" :x1="x(0)" :x2="x(8)" :y1="y(r - 1)" :y2="y(r - 1)" />
        <template v-for="c in 9" :key="`v${c}`">
          <line v-if="c === 1 || c === 9" :x1="x(c - 1)" :x2="x(c - 1)" :y1="y(0)" :y2="y(9)" />
          <template v-else>
            <line :x1="x(c - 1)" :x2="x(c - 1)" :y1="y(0)" :y2="y(4)" />
            <line :x1="x(c - 1)" :x2="x(c - 1)" :y1="y(5)" :y2="y(9)" />
          </template>
        </template>
        <path :d="`M${x(3)} ${y(0)}L${x(5)} ${y(2)}M${x(5)} ${y(0)}L${x(3)} ${y(2)}M${x(3)} ${y(7)}L${x(5)} ${y(9)}M${x(5)} ${y(7)}L${x(3)} ${y(9)}`" />
        <rect class="edge" :x="x(0)" :y="y(0)" :width="8 * CELL" :height="9 * CELL" />
        <path v-for="(d, i) in marks" :key="`m${i}`" class="mark" :d="d" />
      </g>
      <g class="river">
        <text :x="x(2)" :y="y(4) + CELL / 2 + 9" text-anchor="middle">{{ flipped ? '汉　界' : '楚　河' }}</text>
        <text :x="x(6)" :y="y(4) + CELL / 2 + 9" text-anchor="middle">{{ flipped ? '楚　河' : '汉　界' }}</text>
        <text class="river-code" :x="x(4)" :y="y(4) + CELL / 2 + 4" text-anchor="middle">RIVER / 楚河汉界</text>
      </g>

      <!-- 最后一步：起点小方块 -->
      <rect v-if="lastMove" class="last-from" :x="px(lastMove.from) - 7" :y="py(lastMove.from) - 7" width="14" height="14" />

      <!-- 棋子 -->
      <g v-for="(ch, i) in cells" :key="i" class="piece" :class="[side(ch), { sel: selected === i, check: checkKing === i, last: lastMove?.to === i }]" :transform="`translate(${px(i)} ${py(i)})`">
        <template v-if="ch !== '.'">
          <circle class="disc" :r="R" :fill="`url(#${uid}-face)`" />
          <circle class="ring" :r="R - 5" />
          <text class="label" y="9" text-anchor="middle">{{ NAMES[ch] }}</text>
          <path v-if="lastMove?.to === i" class="last-to" :d="bracket" />
        </template>
      </g>

      <!-- 可走位置 -->
      <template v-for="t in targets" :key="`t${t}`">
        <circle v-if="cells[t] === '.'" class="dot" :cx="px(t)" :cy="py(t)" r="7" />
        <circle v-else class="hit-ring" :cx="px(t)" :cy="py(t)" :r="R + 3" />
      </template>

      <!-- 点击区域 -->
      <g class="hits">
        <rect v-for="i in 90" :key="`h${i}`" :x="px(i - 1) - CELL / 2" :y="py(i - 1) - CELL / 2" :width="CELL" :height="CELL" @click="onClick(i - 1)" />
      </g>
    </svg>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { COLS, legalMoves, sideOf, type Side } from './xqRules'
import { uiSound } from '@/utils/uiSound'

const props = withDefaults(defineProps<{
  board: string
  /** 黑方在下（执黑的人看棋盘） */
  flipped?: boolean
  /** 现在谁走 */
  turn: Side
  /** 这台电脑能动哪一方：联机时是自己的颜色，本地对弈时等于 turn */
  movable?: Side | null
  lastMove?: { from: number; to: number } | null
  /** 被将军的一方（高亮它的将 / 帅） */
  checkSide?: Side | null
  disabled?: boolean
}>(), { flipped: false, movable: null, lastMove: null, checkSide: null, disabled: false })
const emit = defineEmits<{ move: [from: number, to: number] }>()

const uid = `xq${Math.random().toString(36).slice(2, 8)}`
const CELL = 60
const PAD = 80
const R = CELL * 0.43
const W = PAD * 2 + 8 * CELL
const H = PAD * 2 + 9 * CELL
const NAMES: Record<string, string> = { K: '帅', A: '仕', B: '相', N: '马', R: '车', C: '炮', P: '兵', k: '将', a: '士', b: '象', n: '马', r: '车', c: '炮', p: '卒' }
const RED_NUM = ['一', '二', '三', '四', '五', '六', '七', '八', '九']

const cells = computed(() => props.board.split(''))
const side = (ch: string) => (ch === '.' ? '' : sideOf(ch) === 'r' ? 'red' : 'black')

/** 显示位置（翻转时整盘旋转 180°） */
const disp = (i: number) => {
  const r = Math.floor(i / COLS), c = i % COLS
  return props.flipped ? [9 - r, 8 - c] : [r, c]
}
const x = (dc: number) => PAD + dc * CELL
const y = (dr: number) => PAD + dr * CELL
const px = (i: number) => x(disp(i)[1]!)
const py = (i: number) => y(disp(i)[0]!)

const topSide = computed<Side>(() => (props.flipped ? 'r' : 'b'))
const bottomSide = computed<Side>(() => (props.flipped ? 'b' : 'r'))
/** 显示列 dc 上对应某一方的路数 */
function fileLabel(s: Side, dc: number) {
  const c = props.flipped ? 8 - dc : dc
  return s === 'r' ? RED_NUM[8 - c] : String(c + 1)
}

const corners = (() => {
  const a = 10, L = 24
  return [`M${a} ${a + L}V${a}H${a + L}`, `M${W - a - L} ${a}H${W - a}V${a + L}`, `M${a} ${H - a - L}V${H - a}H${a + L}`, `M${W - a - L} ${H - a}H${W - a}V${H - a - L}`]
})()

/** 炮位、兵位的小折角标记 */
const marks = (() => {
  const pts: [number, number][] = [[2, 1], [2, 7], [7, 1], [7, 7], [3, 0], [3, 2], [3, 4], [3, 6], [3, 8], [6, 0], [6, 2], [6, 4], [6, 6], [6, 8]]
  const g = 5, l = 12
  const out: string[] = []
  for (const [r, c] of pts) {
    const cx = x(c), cy = y(r)
    for (const sx of [-1, 1]) {
      if ((c === 0 && sx < 0) || (c === 8 && sx > 0)) continue
      for (const sy of [-1, 1]) out.push(`M${cx + sx * g} ${cy + sy * (g + l)}V${cy + sy * g}H${cx + sx * (g + l)}`)
    }
  }
  return out
})()
const bracket = (() => {
  const a = R + 6, l = 9
  return `M${-a} ${-a + l}V${-a}H${-a + l}M${a - l} ${-a}H${a}V${-a + l}M${-a} ${a - l}V${a}H${-a + l}M${a - l} ${a}H${a}V${a - l}`
})()

const checkKing = computed(() => (props.checkSide ? props.board.indexOf(props.checkSide === 'r' ? 'K' : 'k') : -1))

// ===== 选子与走子 =====
const selected = ref<number | null>(null)
const targets = computed(() => (selected.value == null ? [] : legalMoves(props.board, selected.value)))
const canMove = computed(() => !props.disabled && !!props.movable && props.movable === props.turn)
watch(() => [props.board, props.turn, props.disabled], () => { selected.value = null })

function onClick(i: number) {
  if (!canMove.value) return
  const ch = props.board[i]
  if (selected.value != null && targets.value.includes(i)) {
    emit('move', selected.value, i)
    selected.value = null
    return
  }
  if (sideOf(ch) === props.movable) {
    selected.value = selected.value === i ? null : i
    if (selected.value != null) uiSound.hover()
  } else selected.value = null
}

// 落子声、将军提示音
watch(() => props.lastMove ? `${props.lastMove.from}-${props.lastMove.to}` : '', (v, old) => { if (v && v !== old) uiSound.stone() })
watch(() => props.checkSide, (s, old) => { if (s && s !== old) window.setTimeout(() => uiSound.check(), 120) })
</script>

<style scoped>
.xq-board { width: 100%; user-select: none; }
.xq-board svg { display: block; width: 100%; height: auto; }
.face { fill: color-mix(in srgb, var(--paper-card) 94%, var(--accent-color)); stroke: var(--border-line); stroke-width: 2; }
.field { fill: #efe6cf; }
:root[data-theme='dark'] .field { fill: #c6bb9e; }
.reg path { fill: none; stroke: var(--accent-color); stroke-width: 2.5; }
.coord text { font: 600 14px var(--font-mono, monospace); fill: var(--text-secondary); }
.grid line, .grid path { stroke: #4a4134; stroke-width: 1.2; fill: none; }
.grid .edge { fill: none; stroke: #2b251c; stroke-width: 2.6; }
.grid .mark { stroke-width: 1.2; }
.river text { font: 600 26px 'KaiTi', 'STKaiti', 'Kaiti SC', serif; fill: #6b5d45; letter-spacing: .1em; }
.river .river-code { font: 9px var(--font-mono, monospace); fill: var(--accent-color); letter-spacing: .2em; opacity: .7; }

.piece .disc { stroke: #7d8790; stroke-width: 1; filter: drop-shadow(0 2px 2px rgb(0 0 0 / 28%)); }
.piece .ring { fill: none; stroke-width: 2; }
.piece .label { font: 700 27px 'KaiTi', 'STKaiti', 'Kaiti SC', 'SimSun', serif; }
.piece.red .ring { stroke: #c2272d; }
.piece.red .label { fill: #c2272d; }
.piece.black .ring { stroke: #1b2128; }
.piece.black .label { fill: #1b2128; }
.piece.sel .disc { stroke: var(--accent-color); stroke-width: 3.5; }
.piece.sel { transform-box: fill-box; }
.piece.check .disc { stroke: var(--danger, #dc2626); stroke-width: 4; animation: check-pulse 0.8s ease-in-out infinite alternate; }
@keyframes check-pulse { to { stroke-width: 7; } }
.last-to { fill: none; stroke: var(--signal-mint, #2dd4bf); stroke-width: 2.5; }
.last-from { fill: none; stroke: var(--signal-mint, #2dd4bf); stroke-width: 2; stroke-dasharray: 3 2; }
.dot { fill: var(--signal-mint, #2dd4bf); opacity: .85; pointer-events: none; }
.hit-ring { fill: none; stroke: var(--danger, #dc2626); stroke-width: 3; stroke-dasharray: 6 4; pointer-events: none; }
.hits rect { fill: transparent; cursor: pointer; }
.disabled .hits rect { cursor: default; }
</style>
