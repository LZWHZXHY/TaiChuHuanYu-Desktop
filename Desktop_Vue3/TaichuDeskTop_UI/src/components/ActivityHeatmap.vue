<!-- 活跃日历（像 GitHub 的贡献图）：默认显示最近一年，有更早的数据时可以切换到某一年。日期按北京时间。
     世界观创作统计、我的创作档案、个人主页的社区活跃都用它。 -->
<template>
  <div class="heat">
    <div v-if="years.length > 1" class="heat-years" role="group" aria-label="选择年份">
      <button type="button" :aria-pressed="year === null" @click="year = null">最近一年</button>
      <button v-for="y in years" :key="y" type="button" :aria-pressed="year === y" @click="year = y">{{ y }}</button>
    </div>
    <div ref="scroller" class="heat-scroll">
      <div class="heat-body">
        <div class="heat-months" aria-hidden="true">
          <span v-for="m in months" :key="m.col" :style="{ gridColumnStart: m.col + 1 }">{{ m.label }}</span>
        </div>
        <div class="heat-weekdays" aria-hidden="true"><span>一</span><span></span><span>三</span><span></span><span>五</span><span></span><span></span></div>
        <div class="heat-grid" role="img" :aria-label="summary" :style="{ gridTemplateColumns: `repeat(${weeks}, var(--cell))` }" @mouseleave="hover = null">
          <span
            v-for="cell in cells" :key="cell.date"
            class="heat-cell" :class="[`lv${cell.level}`, { off: cell.off }]"
            :style="{ gridColumnStart: cell.col + 1, gridRowStart: cell.row + 1 }"
            @mouseenter="onHover($event, cell)"
          ></span>
        </div>
      </div>
    </div>
    <div class="heat-foot">
      <span class="heat-note">{{ summary }}</span>
      <span class="heat-legend" aria-hidden="true">少<i class="heat-cell lv0"></i><i class="heat-cell lv1"></i><i class="heat-cell lv2"></i><i class="heat-cell lv3"></i><i class="heat-cell lv4"></i>多</span>
    </div>
    <div v-if="hover" class="heat-tip" :style="{ left: hover.x + 'px', top: hover.y + 'px' }" role="tooltip">
      <b>{{ hover.n ? `${hover.n} ${unit}` : `没有${verb}` }}</b>
      <span v-if="hover.detail">{{ hover.detail }}</span>
      <span>{{ hover.label }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'

interface ActivityDay { d: string; n: number; /** 各类分别几次（可选），悬停时显示 */ k?: Record<string, number> }

const props = withDefaults(defineProps<{
  days: ActivityDay[]
  /** 悬停时的单位：「次创作」「件事」 */
  unit?: string
  /** 「没有创作」「没有活动」 */
  verb?: string
  /** k 里各类的中文名 */
  kindLabels?: Record<string, string>
}>(), { unit: '次创作', verb: '创作', kindLabels: () => ({}) })

const DAY = 86400000
/** 北京时间的「今天」（用 UTC 字段表示，避免浏览器时区影响） */
function beijingToday() {
  const t = new Date(Date.now() + 8 * 3600000)
  return Date.UTC(t.getUTCFullYear(), t.getUTCMonth(), t.getUTCDate())
}
const iso = (ms: number) => new Date(ms).toISOString().slice(0, 10)
const mondayOf = (ms: number) => ms - ((new Date(ms).getUTCDay() + 6) % 7) * DAY

/** null = 最近一年；数字 = 那一年 */
const year = ref<number | null>(null)
const years = computed(() => {
  const set = new Set(props.days.filter(d => d.n > 0).map(d => Number(d.d.slice(0, 4))))
  const thisYear = new Date(beijingToday()).getUTCFullYear()
  // 只有今年的数据时不显示年份切换
  if (![...set].some(y => y < thisYear)) return []
  set.add(thisYear)
  return [...set].sort((a, b) => b - a)
})

interface Cell { date: string; n: number; level: number; col: number; row: number; off: boolean; k?: Record<string, number> }

const range = computed(() => {
  const today = beijingToday()
  if (year.value === null) return { from: mondayOf(today) - 52 * 7 * DAY, last: today, inFrom: 0, inTo: today }
  const first = Date.UTC(year.value, 0, 1), end = Date.UTC(year.value, 11, 31)
  return { from: mondayOf(first), last: Math.min(end, today), inFrom: first, inTo: Math.min(end, today) }
})
const weeks = computed(() => Math.floor((mondayOf(range.value.last) - range.value.from) / (7 * DAY)) + 1)
const shown = computed(() => {
  const { inFrom, inTo } = range.value
  return props.days.filter(d => { const ms = Date.parse(d.d + 'T00:00:00Z'); return ms >= Math.max(inFrom, range.value.from) && ms <= inTo })
})
const cells = computed(() => {
  const byDate = new Map(props.days.map(d => [d.d, d]))
  // 按分位分 4 档（用这段时间里的数据），避免一天特别多把其他天都压成最浅
  const values = shown.value.map(d => d.n).filter(n => n > 0).sort((a, b) => a - b)
  const q = (p: number) => values.length ? values[Math.min(values.length - 1, Math.floor(p * values.length))]! : 0
  const t1 = q(0.25), t2 = q(0.5), t3 = q(0.75)
  const level = (n: number) => !n ? 0 : n <= t1 ? 1 : n <= t2 ? 2 : n <= t3 ? 3 : 4
  const { from, inFrom, inTo } = range.value
  const out: Cell[] = []
  for (let i = 0; i < weeks.value * 7; i++) {
    const ms = from + i * DAY
    const date = iso(ms)
    const day = byDate.get(date)
    const n = day?.n ?? 0
    out.push({ date, n, level: level(n), col: Math.floor(i / 7), row: i % 7, off: ms > inTo || ms < inFrom, k: day?.k })
  }
  return out
})
const months = computed(() => {
  const out: { col: number; label: string }[] = []
  let last = -1
  for (const c of cells.value) {
    if (c.row !== 0) continue
    const m = Number(c.date.slice(5, 7))
    if (m !== last) { out.push({ col: c.col, label: `${m}月` }); last = m }
  }
  // 第一列的月份如果和第二个标签挨得太近就去掉
  if (out.length > 1 && out[1]!.col - out[0]!.col < 3) out.shift()
  return out
})
const summary = computed(() => {
  const active = shown.value.filter(d => d.n > 0)
  const total = active.reduce((s, d) => s + d.n, 0)
  const span = year.value === null ? '最近一年' : `${year.value} 年`
  return active.length ? `${span}有 ${active.length} 天在${props.verb}，共 ${total.toLocaleString('zh-CN')} ${props.unit}` : `${span}还没有${props.verb}记录`
})

const hover = ref<{ x: number; y: number; n: number; label: string; detail: string } | null>(null)
function onHover(e: MouseEvent, cell: Cell) {
  if (cell.off) { hover.value = null; return }
  const el = e.currentTarget as HTMLElement
  const root = el.closest('.heat') as HTMLElement
  const a = el.getBoundingClientRect(), b = root.getBoundingClientRect()
  const [y, m, d] = cell.date.split('-')
  const detail = cell.k ? Object.entries(cell.k).sort((p, q) => q[1] - p[1]).map(([key, n]) => `${props.kindLabels[key] ?? key} ${n}`).join(' · ') : ''
  hover.value = { x: a.left - b.left + a.width / 2, y: a.top - b.top, n: cell.n, label: `${y}年${Number(m)}月${Number(d)}日 周${'一二三四五六日'[cell.row]}`, detail }
}

// 手机上默认滚到最右边（最近的日子）；看往年时滚回最左边
const scroller = ref<HTMLElement | null>(null)
const align = () => nextTick(() => { if (scroller.value) scroller.value.scrollLeft = year.value === null ? scroller.value.scrollWidth : 0 })
let observer: ResizeObserver | null = null
onMounted(() => {
  align()
  // 宽度变了（转屏、侧栏收起）也保持对齐
  if (typeof ResizeObserver !== 'undefined' && scroller.value) { observer = new ResizeObserver(() => align()); observer.observe(scroller.value) }
})
onBeforeUnmount(() => observer?.disconnect())
watch([() => props.days, year], align)
</script>

<style scoped>
.heat { position: relative; --cell: 11px; --gap: 3px; }
.heat-years { display: flex; flex-wrap: wrap; gap: 4px; margin-bottom: 10px; }
.heat-years button { padding: 2px 10px; border: 1px solid var(--border-line); background: none; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.heat-years button[aria-pressed=true] { border-color: var(--accent-color); color: var(--accent-color); font-weight: 600; }
.heat-scroll { overflow-x: auto; padding-bottom: 4px; }
.heat-body { display: grid; grid-template-columns: 18px max-content; grid-template-rows: auto auto; gap: 4px 6px; width: max-content; }
.heat-months { grid-column: 2; position: relative; display: grid; grid-auto-columns: var(--cell); grid-auto-flow: column; column-gap: var(--gap); font-size: 11px; color: var(--text-secondary); height: 14px; }
.heat-months span { white-space: nowrap; grid-row: 1; }
.heat-weekdays { grid-column: 1; grid-row: 2; display: grid; grid-template-rows: repeat(7, var(--cell)); row-gap: var(--gap); font-size: 10px; line-height: var(--cell); color: var(--text-secondary); }
.heat-grid { grid-column: 2; grid-row: 2; display: grid; grid-template-rows: repeat(7, var(--cell)); gap: var(--gap); }
.heat-cell { display: inline-block; width: var(--cell); height: var(--cell); border-radius: 2px; background: var(--bg-sub); box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--border-line) 70%, transparent); }
/* 单一色相由浅到深：强调色和卡片底色混合 */
.heat-cell.lv1 { background: color-mix(in srgb, var(--accent-color) 28%, var(--paper-card)); box-shadow: none; }
.heat-cell.lv2 { background: color-mix(in srgb, var(--accent-color) 52%, var(--paper-card)); box-shadow: none; }
.heat-cell.lv3 { background: color-mix(in srgb, var(--accent-color) 76%, var(--paper-card)); box-shadow: none; }
.heat-cell.lv4 { background: var(--accent-color); box-shadow: none; }
.heat-cell.off { visibility: hidden; }
.heat-grid .heat-cell:not(.off):hover { outline: 2px solid var(--text-primary); outline-offset: 1px; }
.heat-foot { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px; margin-top: 8px; font-size: 12px; color: var(--text-secondary); }
.heat-legend { display: inline-flex; align-items: center; gap: 3px; }
.heat-legend .heat-cell { margin: 0 1px; }
.heat-tip { position: absolute; transform: translate(-50%, calc(-100% - 8px)); display: grid; gap: 2px; padding: 6px 10px; font-size: 12px; white-space: nowrap; pointer-events: none; background: var(--text-primary); color: var(--paper-card); z-index: 5; }
.heat-tip b { font-weight: 600; }
.heat-tip span { opacity: .85; font-size: 11px; }
</style>
