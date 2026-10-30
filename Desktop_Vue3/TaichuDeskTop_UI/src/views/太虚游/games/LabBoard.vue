<!-- LabBoard.vue：莱茵生命风格的棋盘（五子棋、围棋共用）。SVG 绘制，随容器缩放，坐标、星位、最后一手、连线高亮、落子预览。 -->
<template>
  <div class="lab-board" :class="{ disabled }" data-no-hover-sound>
    <svg :viewBox="`0 0 ${W} ${W}`" role="grid" :aria-label="`${size} 路棋盘`" @mouseleave="hover = null">
      <defs>
        <radialGradient :id="`${uid}-b`" cx="35%" cy="32%" r="70%">
          <stop offset="0%" stop-color="#4a5560" />
          <stop offset="55%" stop-color="#1b2128" />
          <stop offset="100%" stop-color="#07090c" />
        </radialGradient>
        <radialGradient :id="`${uid}-w`" cx="35%" cy="30%" r="75%">
          <stop offset="0%" stop-color="#ffffff" />
          <stop offset="70%" stop-color="#eef2f4" />
          <stop offset="100%" stop-color="#c9d2d8" />
        </radialGradient>
      </defs>

      <!-- 底板与外框 -->
      <rect class="face" x="0" y="0" :width="W" :height="W" />
      <rect class="field" :x="PAD - CELL / 2" :y="PAD - CELL / 2" :width="span + CELL" :height="span + CELL" />
      <!-- 四角定位标记 -->
      <g class="reg">
        <path v-for="(d, i) in corners" :key="i" :d="d" />
      </g>

      <!-- 坐标 -->
      <g v-if="coords" class="coord">
        <template v-for="i in size" :key="`c${i}`">
          <text :x="pos(i - 1)" :y="PAD - CELL * 0.72" text-anchor="middle">{{ colLabel(i - 1) }}</text>
          <text :x="pos(i - 1)" :y="PAD + span + CELL * 0.72 + 9" text-anchor="middle">{{ colLabel(i - 1) }}</text>
          <text :x="PAD - CELL * 0.72" :y="pos(i - 1) + 4.5" text-anchor="end">{{ size - i + 1 }}</text>
          <text :x="PAD + span + CELL * 0.72" :y="pos(i - 1) + 4.5" text-anchor="start">{{ size - i + 1 }}</text>
        </template>
      </g>

      <!-- 网格 -->
      <g class="grid">
        <line v-for="i in size" :key="`h${i}`" :x1="PAD" :x2="PAD + span" :y1="pos(i - 1)" :y2="pos(i - 1)" />
        <line v-for="i in size" :key="`v${i}`" :y1="PAD" :y2="PAD + span" :x1="pos(i - 1)" :x2="pos(i - 1)" />
        <rect class="edge" :x="PAD" :y="PAD" :width="span" :height="span" />
      </g>
      <g class="stars">
        <circle v-for="([r, c], i) in starPoints" :key="i" :cx="pos(c)" :cy="pos(r)" r="4.2" />
      </g>

      <!-- 连线高亮（五子连珠） -->
      <line v-if="lineEnds" class="win-line" :x1="pos(lineEnds[0][1])" :y1="pos(lineEnds[0][0])" :x2="pos(lineEnds[1][1])" :y2="pos(lineEnds[1][0])" />

      <!-- 棋子 -->
      <g class="stones">
        <template v-for="(row, r) in stones" :key="r">
          <template v-for="(v, c) in row" :key="c">
            <circle v-if="v" :cx="pos(c)" :cy="pos(r)" :r="CELL * 0.45" :fill="`url(#${uid}-${v === 1 ? 'b' : 'w'})`" :class="[v === 1 ? 'black' : 'white', { dead: deadSet.has(r * size + c) }]" />
            <rect v-else-if="territory?.[r]?.[c]" class="terr" :class="territory[r]![c] === 1 ? 'tb' : 'tw'" :x="pos(c) - 7" :y="pos(r) - 7" width="14" height="14" />
          </template>
        </template>
        <circle v-for="([r, c], i) in highlight ?? []" :key="`hl${i}`" class="hl" :cx="pos(c)" :cy="pos(r)" :r="CELL * 0.45 + 3" />
        <path v-for="([r, c], i) in dead ?? []" :key="`dx${i}`" class="dead-x" :d="`M${pos(c) - 9} ${pos(r) - 9}L${pos(c) + 9} ${pos(r) + 9}M${pos(c) + 9} ${pos(r) - 9}L${pos(c) - 9} ${pos(r) + 9}`" />
      </g>

      <!-- 最后一手 -->
      <rect v-if="lastMove" class="last" :class="{ onWhite: stoneAt(lastMove) === 2 }" :x="pos(lastMove[1]) - 6" :y="pos(lastMove[0]) - 6" width="12" height="12" />

      <!-- 落子预览 -->
      <circle v-if="hover && ghost && !disabled && !stoneAt(hover)" class="ghost" :class="ghost === 1 ? 'black' : 'white'" :cx="pos(hover[1])" :cy="pos(hover[0])" :r="CELL * 0.45" :fill="`url(#${uid}-${ghost === 1 ? 'b' : 'w'})`" />

      <!-- 点击区域 -->
      <g class="hits">
        <template v-for="r in size" :key="`hr${r}`">
          <rect
            v-for="c in size"
            :key="`hc${c}`"
            :x="pos(c - 1) - CELL / 2"
            :y="pos(r - 1) - CELL / 2"
            :width="CELL"
            :height="CELL"
            role="gridcell"
            :aria-label="`${colLabel(c - 1)}${size - r + 1}`"
            @mouseenter="hover = [r - 1, c - 1]"
            @click="onClick(r - 1, c - 1)"
          />
        </template>
      </g>
    </svg>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { uiSound } from '@/utils/uiSound'

type Point = [number, number]
const props = withDefaults(defineProps<{
  size: number
  /** 0 空 / 1 黑 / 2 白，stones[行][列] */
  stones: number[][]
  lastMove?: Point | null
  /** 需要加圈强调的点（例如五子连珠） */
  highlight?: Point[] | null
  /** 当前可以落子的颜色，用来显示预览；0 表示不显示 */
  ghost?: number
  disabled?: boolean
  coords?: boolean
  /** 点目时标记的死子（画叉、变淡） */
  dead?: Point[] | null
  /** 点目时的地盘归属：1 黑 / 2 白（画小方块） */
  territory?: number[][] | null
  /** 允许点击已有棋子（点目时用来标记死子） */
  pickStones?: boolean
}>(), { lastMove: null, highlight: null, ghost: 0, disabled: false, coords: true, dead: null, territory: null, pickStones: false })
const emit = defineEmits<{ play: [row: number, col: number] }>()

const uid = `lb${Math.random().toString(36).slice(2, 8)}`
const CELL = 40
const PAD = 52
const span = computed(() => (props.size - 1) * CELL)
const W = computed(() => PAD * 2 + span.value)
const pos = (i: number) => PAD + i * CELL
const hover = ref<Point | null>(null)

const LETTERS = 'ABCDEFGHJKLMNOPQRSTUVWXYZ' // 按棋谱惯例跳过 I
const colLabel = (c: number) => LETTERS[c] ?? String(c + 1)
const stoneAt = (p: Point) => props.stones[p[0]]?.[p[1]] ?? 0
const deadSet = computed(() => new Set((props.dead ?? []).map(([r, c]) => r * props.size + c)))

/** 星位：9 路 5 个，13 路 5 个，15 路 5 个，19 路 9 个 */
const starPoints = computed<Point[]>(() => {
  const n = props.size
  if (n < 7) return []
  const e = n >= 13 ? 3 : 2
  const m = (n - 1) / 2
  const pts: Point[] = [[e, e], [e, n - 1 - e], [n - 1 - e, e], [n - 1 - e, n - 1 - e]]
  if (n % 2 === 1) pts.push([m, m])
  if (n >= 19) pts.push([e, m], [m, e], [m, n - 1 - e], [n - 1 - e, m])
  return pts
})

const corners = computed(() => {
  const a = 10, L = 22, w = W.value
  return [
    `M${a} ${a + L}V${a}H${a + L}`, `M${w - a - L} ${a}H${w - a}V${a + L}`,
    `M${a} ${w - a - L}V${w - a}H${a + L}`, `M${w - a - L} ${w - a}H${w - a}V${w - a - L}`,
  ]
})

const lineEnds = computed<[Point, Point] | null>(() => {
  const h = props.highlight
  return h && h.length >= 2 ? [h[0]!, h[h.length - 1]!] : null
})

// 每落一子（自己或对手）响一声
watch(() => props.lastMove?.join(','), (v, old) => { if (v && v !== old) uiSound.stone() })

function onClick(r: number, c: number) {
  if (props.disabled || (stoneAt([r, c]) && !props.pickStones)) return
  emit('play', r, c)
}
</script>

<style scoped>
.lab-board { width: 100%; user-select: none; }
.lab-board svg { display: block; width: 100%; height: auto; }
.face { fill: color-mix(in srgb, var(--paper-card) 94%, var(--accent-color)); stroke: var(--border-line); stroke-width: 2; }
.field { fill: color-mix(in srgb, var(--paper-card) 82%, #e9dcc0); }
/* 夜间模式：棋盘保持浅色木纹底，棋子才看得清；线条改成深色 */
:root[data-theme='dark'] .field { fill: #bdb398; }
:root[data-theme='dark'] .grid line { stroke: rgb(20 24 28 / 72%); }
:root[data-theme='dark'] .grid .edge { stroke: #14181c; }
:root[data-theme='dark'] .stars circle { fill: #14181c; }
.reg path { fill: none; stroke: var(--accent-color); stroke-width: 2.5; }
.coord text { font: 600 13px var(--font-mono, monospace); fill: var(--text-secondary); letter-spacing: .04em; }
.grid line { stroke: color-mix(in srgb, var(--text-primary) 58%, transparent); stroke-width: 1; }
.grid .edge { fill: none; stroke: var(--text-primary); stroke-width: 2.2; }
.stars circle { fill: var(--text-primary); }
.stones .white { stroke: #8f9ba4; stroke-width: 1; }
.stones .black { stroke: #000; stroke-width: .6; }
.stones .hl { fill: none; stroke: var(--signal-mint, #2dd4bf); stroke-width: 3; }
.win-line { stroke: var(--signal-mint, #2dd4bf); stroke-width: 5; stroke-linecap: round; opacity: .55; }
.last { fill: var(--signal-mint, #2dd4bf); stroke: #0b2b28; stroke-width: 1; }
.last.onWhite { fill: var(--accent-color); stroke: none; }
.ghost { opacity: .38; pointer-events: none; }
.stones .dead { opacity: .35; }
.dead-x { stroke: var(--danger, #dc2626); stroke-width: 3; stroke-linecap: round; pointer-events: none; }
.terr { pointer-events: none; }
.terr.tb { fill: #11161b; }
.terr.tw { fill: #f4f7f9; stroke: #8f9ba4; stroke-width: 1; }
.hits rect { fill: transparent; cursor: pointer; }
.disabled .hits rect { cursor: default; }
@media (prefers-reduced-motion: no-preference) {
  .stones circle:not(.hl) { animation: drop .16s ease-out; transform-box: fill-box; transform-origin: center; }
  @keyframes drop { from { transform: scale(1.18); opacity: .4; } to { transform: scale(1); opacity: 1; } }
}
</style>
