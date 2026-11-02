<!-- StatPanelChart.vue：「能力面板」雷达图（像 JOJO 替身面板）。柴圈 OC 详情、约战人设卡片、世界观条目的「能力面板」区块都用它。
     纯 SVG 画：几圈多边形网格 + 轴线 + 数据多边形 + 每个轴末端的「维度名 + 等级/数值」。
     颜色全用 CSS 变量，深色模式自动跟着变；SVG 按内容算好 viewBox、宽度 100%，手机上会等比缩小，不会撑破页面。
     维度很多时：字自动变小、名字截短（悬停显示全名），超过 12 个维度时图下面再列一张「维度：等级」的表。
     传 series 时是「叠加对比」：几份维度相同的面板画在同一张图上（约战双方对比），每份一种颜色，下面有图例；
     这时轴标签只写维度名，每份的值在悬停提示里。 -->
<template>
  <figure class="spc" :class="['spc-' + size]" :style="{ '--sp-color': color || 'var(--accent-color)' }">
    <header v-if="(!hideTitle && panel.title) || overall" class="spc-head">
      <span v-if="!hideTitle && panel.title" class="spc-title">{{ panel.title }}</span>
      <span v-if="overall" class="spc-overall" :title="overall.detail">综合 <b>{{ overall.grade }}</b></span>
    </header>
    <svg class="spc-svg" :viewBox="box.viewBox" :style="{ maxWidth: box.width * zoom + 'px' }" role="img" :aria-label="describeStatPanel(panel)" preserveAspectRatio="xMidYMid meet">
      <!-- 网格：5 圈多边形（字母等级时从里到外是 E、D、C、B、A） -->
      <polygon v-for="(ring, i) in rings" :key="'r' + i" class="spc-ring" :class="{ outer: i === rings.length - 1 }" :points="ring" />
      <line v-for="(axis, i) in axes" :key="'a' + i" class="spc-axis" x1="0" y1="0" :x2="axis.x" :y2="axis.y" />
      <!-- 刻度（只在普通大小时显示，沿第一条轴往上标） -->
      <template v-if="size === 'normal'">
        <text v-for="(t, i) in ticks" :key="'t' + i" class="spc-tick" :x="4" :y="t.y" dominant-baseline="middle">{{ t.label }}</text>
      </template>
      <!-- 数据：单份面板一个多边形；对比时每份一个，颜色各不相同 -->
      <g v-for="(s, si) in shapes" :key="'s' + si" class="spc-shape" :style="{ '--sp-color': s.color }">
        <polygon class="spc-area" :class="{ dashed: s.dashed, multi: isMulti }" :points="s.area" />
        <template v-for="(p, i) in s.points" :key="'p' + i">
          <!-- ∞：从最外圈一直冲出去，出圈的那段画成流动的虚线，尖上一个会呼吸的圈和 ∞ 记号 -->
          <template v-if="p.inf">
            <line class="spc-inf-ray" :x1="p.rim.x" :y1="p.rim.y" :x2="p.x" :y2="p.y" />
            <circle class="spc-inf-halo" :cx="p.x" :cy="p.y" :r="dotR * 3.2" />
            <text v-if="!mini" class="spc-inf-mark" :x="p.x" :y="p.y" dy="-0.9em" text-anchor="middle" :font-size="font * 1.25">∞</text>
          </template>
          <circle v-if="p.show" class="spc-dot" :class="{ inf: p.inf }" :cx="p.x" :cy="p.y" :r="p.inf ? dotR * 1.6 : dotR"><title>{{ p.title }}</title></circle>
        </template>
      </g>
      <!-- 轴末端的标签：名字 + 值；悬停显示全名 -->
      <g v-for="(l, i) in labels" :key="'l' + i" class="spc-label">
        <title>{{ l.title }}</title>
        <text :x="l.x" :y="l.y" :text-anchor="l.anchor" :font-size="font">
          <tspan v-for="(line, li) in l.lines" :key="li" :x="l.x" :dy="li === 0 ? 0 : font * LINE" class="spc-name">{{ line }}</tspan>
          <template v-if="isMulti"></template>
          <tspan v-else-if="l.valueOwnLine" :x="l.x" :dy="l.lines.length ? font * LINE : 0" class="spc-value" :class="{ special: l.special }" :font-size="font * 1.15">{{ l.value }}</tspan>
          <tspan v-else class="spc-value" :class="{ special: l.special }" dx="3">{{ l.value }}</tspan>
        </text>
      </g>
    </svg>
    <!-- 对比图的图例 -->
    <ul v-if="isMulti" class="spc-legend">
      <li v-for="(s, si) in shapes" :key="si" :style="{ '--sp-color': s.color }"><i :class="{ dashed: s.dashed }" aria-hidden="true"></i>{{ s.name }}</li>
    </ul>
    <!-- 维度太多时图上的字很小：下面再列一张表 -->
    <dl v-if="showTable && !isMulti" class="spc-table">
      <div v-for="(d, i) in panel.dims" :key="i">
        <dt :title="dimName(d, i)">{{ dimName(d, i) }}</dt>
        <dd :class="{ special: isSpecial(d) }">{{ formatValue(d) }}<small v-if="panel.scale === 'number'"> / {{ panel.max }}</small></dd>
      </div>
    </dl>
    <figcaption v-if="showNote && panel.note?.trim()" class="spc-note">{{ panel.note }}</figcaption>
  </figure>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import {
  GRADE_HINT, STAT_TABLE_FROM, describeStatPanel, dimName, dimRatio, formatValue, gradeLevels, overallOf,
  type ChartSeries, type StatDim, type StatPanel,
} from '@/utils/statPanel'

const props = withDefaults(defineProps<{
  panel: StatPanel
  /** normal = 详情页 / 条目里的大图；mini = 约战卡片里的小图（字更少） */
  size?: 'normal' | 'mini'
  /** 多边形颜色，默认主题色（约战时另一方用橙色，方便对比） */
  color?: string
  /** 不显示面板标题（世界观区块自己有标题栏） */
  hideTitle?: boolean
  showNote?: boolean
  /** 叠加对比：几份维度相同的面板（按维度顺序对应 panel 的轴），name 写在图例里 */
  series?: ChartSeries[]
}>(), { size: 'normal', color: '', hideTitle: false, showNote: true, series: () => [] })
const isMulti = computed(() => props.series.length > 0)

const LINE = 1.2
const n = computed(() => props.panel.dims.length)
const mini = computed(() => props.size === 'mini')
/** 半径（viewBox 里的单位）：图按 viewBox 宽度原样大小显示，所以这也差不多是屏幕上的像素 */
const R = computed(() => mini.value ? 62 : 118)
/** 普通大小时 SVG 最多放大到原尺寸的这么多倍（大屏上别太小） */
const zoom = computed(() => mini.value ? 1 : 1.15)
const dotR = computed(() => mini.value ? 2.5 : 3.5)

/**
 * 标签排版随维度数变化：维度越多，字越小、名字越短。
 * 返回：字号、每行最多几个字、名字最多几行、值是否单独一行
 */
const layout = computed(() => {
  const count = n.value
  if (mini.value) {
    if (count <= 6) return { font: 11, perLine: 4, maxLines: 1, valueOwnLine: false }
    if (count <= 12) return { font: 10, perLine: 3, maxLines: 1, valueOwnLine: false }
    if (count <= 24) return { font: 9, perLine: 2, maxLines: 1, valueOwnLine: false }
    return { font: 8, perLine: 0, maxLines: 0, valueOwnLine: false }
  }
  if (count <= 8) return { font: 13, perLine: 6, maxLines: 2, valueOwnLine: true }
  if (count <= 12) return { font: 12, perLine: 5, maxLines: 1, valueOwnLine: true }
  if (count <= 24) return { font: 10, perLine: 4, maxLines: 1, valueOwnLine: false }
  if (count <= 48) return { font: 8, perLine: 2, maxLines: 1, valueOwnLine: false }
  // 再多就只标值，名字看下面的表
  return { font: 7, perLine: 0, maxLines: 0, valueOwnLine: false }
})
const font = computed(() => layout.value.font)
const showTable = computed(() => !mini.value && n.value >= STAT_TABLE_FROM)

/** 第 i 个轴的方向：从正上方开始，顺时针 */
const angle = (i: number) => -Math.PI / 2 + (i * 2 * Math.PI) / n.value
const at = (i: number, r: number) => ({ x: Math.cos(angle(i)) * r, y: Math.sin(angle(i)) * r })
const fmt = (v: number) => Number(v.toFixed(2))
const poly = (pts: { x: number; y: number }[]) => pts.map(p => `${fmt(p.x)},${fmt(p.y)}`).join(' ')

/** 字母等级分几档：平时 5 档（E～A），有人用了 S 就 6 档（对比图里所有面板用同一个档数） */
const levels = computed(() => props.panel.scale === 'grade' ? gradeLevels(props.panel, ...props.series.map(s => s.panel)) : 5)
const rings = computed(() => Array.from({ length: levels.value }, (_, j) => j + 1).map(k => poly(props.panel.dims.map((_, i) => at(i, (R.value * k) / levels.value)))))
const axes = computed(() => props.panel.dims.map((_, i) => at(i, R.value)))
const ticks = computed(() => Array.from({ length: levels.value }, (_, j) => j + 1).map(k => ({
  y: fmt((-R.value * k) / levels.value),
  label: props.panel.scale === 'grade' ? ['E', 'D', 'C', 'B', 'A', 'S'][k - 1] : String(Math.round((props.panel.max * k) / 5)),
})))

const isSpecial = (d: StatDim) => d.value === '∞' || d.value === '?' || d.value === '—'
const valueText = (panel: StatPanel, d: StatDim) =>
  formatValue(d) + (typeof d.value === 'string' ? `（${GRADE_HINT[d.value]}）` : ` / ${panel.max}`)
const fullTitle = (d: StatDim, i: number) => isMulti.value
  // 对比图：「破坏力：甲 A（超级）、乙 B（强）」
  ? `${dimName(d, i)}：${props.series.map(s => `${s.name} ${s.panel.dims[i] ? valueText(s.panel, s.panel.dims[i]) : '?'}`).join('、')}`
  : `${dimName(d, i)}：${valueText(props.panel, d)}`

/** ∞ 冲出最外圈很远（最外圈半径的 INF_RATIO 倍），一眼看出「超出上限」 */
const INF_RATIO = 1.75
const ratioOf = (panel: StatPanel, d: StatDim) => d.value === '∞' ? INF_RATIO : dimRatio(panel, d, panel.scale === 'grade' ? levels.value : 5)
/** 哪几条轴上有 ∞（任何一份面板）：这些轴的标签要挪到尖的外面，别压在一起 */
const infAxes = computed(() => {
  const list = isMulti.value ? props.series.map(s => s.panel) : [props.panel]
  return new Set(props.panel.dims.map((_, i) => i).filter(i => list.some(p => p.dims[i]?.value === '∞')))
})
const shapes = computed(() => {
  const list: ChartSeries[] = isMulti.value ? props.series : [{ name: '', color: props.color || 'var(--accent-color)', panel: props.panel }]
  return list.map(s => {
    // 按轴的个数取值，对比的面板维度少了的当作 ?（画在中心）
    const points = props.panel.dims.map((axis, i) => {
      const d = s.panel.dims[i] ?? { name: axis.name, value: '?' as const }
      return { ...at(i, R.value * ratioOf(s.panel, d)), rim: at(i, R.value), show: d.value !== '?' && d.value !== '—', inf: d.value === '∞', title: `${s.name ? s.name + ' · ' : ''}${dimName(axis, i)}：${valueText(s.panel, d)}` }
    })
    return { name: s.name, color: s.color, dashed: !!s.dashed, points, area: poly(points) }
  })
})

/** 估算文字宽度：中文按一个字号宽，英文数字按 0.6 个 */
const textWidth = (s: string, size: number) => [...s].reduce((w, ch) => w + (/[\x20-\x7e]/.test(ch) ? 0.6 : 1), 0) * size
function splitName(name: string) {
  const { perLine, maxLines } = layout.value
  if (!perLine || !maxLines) return []
  const chars = [...name]
  const lines: string[] = []
  for (let k = 0; k < maxLines && k * perLine < chars.length; k++) lines.push(chars.slice(k * perLine, (k + 1) * perLine).join(''))
  // 截掉的部分用「…」表示，全名在悬停提示里
  // （每行只有一两个字时就直接截，不加「…」，不然只剩一个字）
  if (chars.length > perLine * maxLines && perLine > 2) lines[lines.length - 1] = [...lines[lines.length - 1]].slice(0, perLine - 1).join('') + '…'
  return lines
}

const labels = computed(() => {
  const gap = mini.value ? 6 : 10
  return props.panel.dims.map((d, i) => {
    const cos = Math.cos(angle(i)), sin = Math.sin(angle(i))
    // 标签放在最外圈再往外一点；这条轴上有 ∞ 时放到 ∞ 的尖外面（还要让出 ∞ 记号的位置）
    const p = at(i, infAxes.value.has(i) ? R.value * INF_RATIO + gap + font.value * 1.6 : R.value * 1.1 + gap)
    const anchor: 'start' | 'middle' | 'end' = Math.abs(cos) < 0.3 ? 'middle' : cos > 0 ? 'start' : 'end'
    const lines = splitName(dimName(d, i))
    // 对比图不在轴上写值（几份的值挤在一起看不清），只写维度名
    const valueOwnLine = !isMulti.value && (layout.value.valueOwnLine || !lines.length)
    const rowCount = Math.max(1, lines.length + (valueOwnLine ? 1 : 0))
    const h = font.value * LINE * rowCount
    // 竖直方向：上面的标签整体往上摆，下面的往下摆，左右两边的居中
    const top = sin < -0.3 ? p.y - h : sin > 0.3 ? p.y : p.y - h / 2
    const y = top + font.value * 0.9
    const value = isMulti.value ? '' : formatValue(d)
    const width = Math.max(
      ...lines.map(l => textWidth(l, font.value) + (valueOwnLine || !value ? 0 : textWidth(value, font.value) + 3)),
      textWidth(value, font.value * 1.15),
    )
    const left = anchor === 'start' ? p.x : anchor === 'end' ? p.x - width : p.x - width / 2
    return { x: fmt(p.x), y: fmt(y), anchor, lines, value, valueOwnLine, special: isSpecial(d), title: fullTitle(d, i), box: { left, right: left + width, top, bottom: top + h } }
  })
})

/** 按网格和所有标签算出刚好装得下的 viewBox（不会被裁掉，也不会留一大片空白） */
const box = computed(() => {
  const r = R.value * 1.1
  let left = -r, right = r, top = -r, bottom = r
  for (const l of labels.value) {
    left = Math.min(left, l.box.left); right = Math.max(right, l.box.right)
    top = Math.min(top, l.box.top); bottom = Math.max(bottom, l.box.bottom)
  }
  const pad = 4
  left -= pad; top -= pad; right += pad; bottom += pad
  return { viewBox: `${fmt(left)} ${fmt(top)} ${fmt(right - left)} ${fmt(bottom - top)}`, width: right - left }
})

const overall = computed(() => !isMulti.value && props.panel.overall ? overallOf(props.panel) : null)
</script>

<style scoped>
.spc { margin: 0; display: grid; gap: 8px; min-width: 0; justify-items: center; }
.spc-head { justify-self: stretch; display: flex; align-items: baseline; gap: 10px; flex-wrap: wrap; }
.spc-title { font-size: 14px; letter-spacing: .15em; color: var(--text-primary); }
.spc-overall { margin-left: auto; padding: 1px 10px; border: 1px solid var(--sp-color); color: var(--text-secondary); font-size: 12px; letter-spacing: .1em; cursor: help; }
.spc-overall b { color: var(--sp-color); font-size: 15px; }
.spc-mini .spc-head { gap: 6px; }
.spc-mini .spc-overall { padding: 0 6px; font-size: 11px; }
.spc-mini .spc-overall b { font-size: 13px; }
/* 宽度 100%：容器变窄时整张图等比缩小；最大宽度在模板里按 viewBox 算 */
.spc-svg { display: block; width: 100%; height: auto; overflow: visible; }
.spc-ring { fill: none; stroke: var(--border-line); stroke-width: 1; }
.spc-ring.outer { stroke: var(--text-secondary); stroke-opacity: .6; }
.spc-axis { stroke: var(--border-line); stroke-width: 1; }
.spc-tick { fill: var(--text-secondary); font-size: 9px; opacity: .8; }
.spc-area { fill: var(--sp-color); fill-opacity: .22; stroke: var(--sp-color); stroke-width: 2; stroke-linejoin: round; }
.spc-area.multi { fill-opacity: .12; }
.spc-area.dashed { stroke-dasharray: 6 4; }
.spc-dot { fill: var(--sp-color); }
.spc-dot.inf { fill: var(--paper-card); stroke: var(--sp-color); stroke-width: 2.5; }
.spc-inf-ray { stroke: var(--sp-color); stroke-width: 2.5; stroke-dasharray: 6 5; stroke-linecap: square; animation: spc-inf-flow 0.9s linear infinite; }
.spc-inf-halo { fill: var(--sp-color); fill-opacity: .14; stroke: var(--sp-color); stroke-opacity: .5; stroke-width: 1; transform-box: fill-box; transform-origin: center; animation: spc-inf-pulse 1.8s ease-in-out infinite; }
.spc-inf-mark { fill: var(--sp-color); font-weight: 700; }
@keyframes spc-inf-flow { to { stroke-dashoffset: -11; } }
@keyframes spc-inf-pulse { 50% { transform: scale(1.35); fill-opacity: .04; } }
@media (prefers-reduced-motion: reduce) { .spc-inf-ray, .spc-inf-halo { animation: none; } }
.spc-label { cursor: default; }
.spc-name { fill: var(--text-secondary); }
.spc-value { fill: var(--sp-color); font-weight: 700; }
.spc-value.special { fill: var(--text-primary); }
.spc-legend { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; justify-content: center; gap: 4px 14px; font-size: 13px; color: var(--text-primary); }
.spc-legend li { display: flex; align-items: center; gap: 6px; }
.spc-legend i { width: 18px; height: 0; border-top: 3px solid var(--sp-color); }
.spc-legend i.dashed { border-top-style: dashed; }
.spc-table { justify-self: stretch; margin: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(140px, 1fr)); gap: 4px 12px; font-size: 13px; }
.spc-table div { display: flex; justify-content: space-between; gap: 8px; padding: 3px 0; border-bottom: 1px dashed var(--border-line); min-width: 0; }
.spc-table dt { color: var(--text-secondary); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.spc-table dd { margin: 0; color: var(--sp-color); font-weight: 700; white-space: nowrap; }
.spc-table dd.special { color: var(--text-primary); }
.spc-table small { color: var(--text-secondary); font-weight: 400; }
.spc-note { justify-self: stretch; margin: 0; font-size: 13px; line-height: 1.7; color: var(--text-secondary); white-space: pre-wrap; word-break: break-word; }
.spc-mini .spc-note { font-size: 12px; }
</style>
