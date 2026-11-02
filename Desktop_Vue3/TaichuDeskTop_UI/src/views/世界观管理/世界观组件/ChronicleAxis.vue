<!-- 编年史的「时间轴」：每条时间线一行（支线在父线下面），分叉 / 汇合画成曲线，
     时期是横条、失落的历史是斜线条、时间跳跃把轴折叠起来；按住 Ctrl 滚轮缩放，拖动空白处平移。
     事件标题里 @ 的条目可以直接点，打开那个条目的完整页面。 -->
<template>
  <div class="ax">
    <!-- 颜色图例（世界设置里定义的）：点一种颜色，别的事件变淡，只看这一类 -->
    <ColorLegend v-if="legendItems.length" v-model:active="legendHue" class="ax-legend" :entries="legendItems"
      hint="按事件自己选的颜色分类；没选颜色的事件和所在时间线同色，不算在内" />
    <div class="ax-zoom" role="group" aria-label="缩放">
      <button type="button" title="缩小（Ctrl + 滚轮也可以）" aria-label="缩小" @click="zoomBy(1 / 1.6)">－</button>
      <button type="button" title="放大" aria-label="放大" @click="zoomBy(1.6)">＋</button>
      <button type="button" title="整条轴放进窗口" @click="fit">适合宽度</button>
    </div>
    <div ref="scrollEl" class="ax-scroll" :class="{ panning }" @wheel="onWheel" @pointerdown="onPanStart">
      <div class="ax-inner" :style="{ width: LABEL_W + axis.width + 'px' }">
        <!-- 刻度尺 -->
        <div class="ax-ruler" :style="{ height: RULER_H + 'px' }">
          <div class="ax-corner" :style="{ width: LABEL_W + 'px' }">{{ calendar.yearUnit === '年' ? '时间' : calendar.yearUnit }}</div>
          <div class="ax-ruler-track" :style="{ width: axis.width + 'px' }">
            <span v-for="tk in ticks" :key="tk.t" class="ax-tick" :style="{ left: tk.x + 'px' }">{{ tickLabel(tk.t) }}</span>
            <span v-for="b in eras" :key="b.era.id" class="ax-era" :style="{ left: axis.x(b.t) + 'px', color: b.era.color || undefined }">{{ b.era.name }}</span>
            <button v-for="f in folds" :key="'f' + f.x0" type="button" class="ax-fold-label" :class="{ explicit: f.eventId }"
              :style="{ left: f.x0 + 'px', width: (f.x1 - f.x0) + 'px' }" :title="f.title" :disabled="!f.eventId"
              @click="f.eventId && emit('select', f.eventId)">{{ f.short }}</button>
          </div>
        </div>

        <div class="ax-lanes" :style="{ height: totalHeight + 'px' }">
          <!-- 时间跳跃 / 自动折叠：竖着的锯齿带 -->
          <div v-for="f in folds" :key="'band' + f.x0" class="ax-fold-band" :style="{ left: LABEL_W + f.x0 + 'px', width: (f.x1 - f.x0) + 'px' }" aria-hidden="true"></div>
          <div v-for="b in eras" :key="'eb' + b.era.id" class="ax-era-line" :style="{ left: LABEL_W + axis.x(b.t) + 'px' }" aria-hidden="true"></div>

          <!-- 分叉 / 汇合的曲线 -->
          <svg class="ax-links" :style="{ left: LABEL_W + 'px' }" :width="axis.width" :height="totalHeight" aria-hidden="true">
            <path v-for="l in links" :key="l.key" :d="l.d" :stroke="l.color" :class="l.kind" fill="none" />
          </svg>

          <div v-for="(lane, li) in laid" :key="lane.line.id" class="ax-lane" :class="['st-' + lane.line.status, { virtual: lane.line.virtual, dim: dimmed(lane.line.id) }]"
            :style="{ top: lane.top + 'px', height: lane.height + 'px', '--lc': lineColor(lane.line, li) }">
            <div class="ax-label" :style="{ width: LABEL_W + 'px', paddingLeft: 10 + lane.depth * 14 + 'px' }">
              <span v-if="lane.depth" class="ax-branch-mark" aria-hidden="true">↳</span>
              <span class="ax-swatch" aria-hidden="true"></span>
              <div class="ax-label-text">
                <button type="button" class="ax-line-name" :title="lane.line.description || lane.line.name" @click="emit('line', lane.line.id)">{{ lane.line.name }}</button>
                <span v-if="lane.line.status !== 'canon' && !lane.line.virtual" class="ax-status">{{ STATUS_META[lane.line.status].label }}</span>
                <span v-if="lane.line.hidden" class="ax-lock" title="这条线对访客隐藏">🔒</span>
                <a v-if="lane.line.cardId && findCard(lane.line.cardId)" class="ax-line-card" role="link" tabindex="0" :title="`打开「${findCard(lane.line.cardId)!.title}」`"
                  @click="emit('open-card', lane.line.cardId!)" @keydown.enter="emit('open-card', lane.line.cardId!)">{{ findCard(lane.line.cardId)!.title }} ↗</a>
              </div>
            </div>
            <div class="ax-track" :style="{ left: LABEL_W + 'px', width: axis.width + 'px' }" @dblclick="onTrackDbl($event, lane.line)">
              <!-- 这条线的「脊」：从第一件事到最后一件事 -->
              <div v-if="lane.spine" class="ax-spine" :style="{ left: lane.spine[0] + 'px', width: Math.max(2, lane.spine[1] - lane.spine[0]) + 'px' }"></div>
              <span v-for="m in lane.marks" :key="m.key" class="ax-mark" :class="m.kind" :style="{ left: m.x + 'px', width: m.w ? m.w + 'px' : undefined }"></span>

              <div v-for="it in lane.items" :key="it.ev.id" class="ax-item" :class="['k-' + it.ev.kind, { bar: it.barW > 0, sel: it.ev.id === selectedId, hid: it.ev.hidden, match: highlight?.has(it.ev.id), 'leg-off': legendOff(it.ev) }]"
                :style="{ left: it.x0 + 'px', top: SPINE_H + it.track * ROW_H + 'px', width: it.barW ? it.barW + 'px' : undefined, '--ec': it.ev.color || undefined }"
                role="button" tabindex="0" :aria-label="`${it.plain}，${formatEventDate(calendar, it.ev)}${meaningOf(it.ev.color) ? '，' + meaningOf(it.ev.color) : ''}`" :title="`${formatEventDate(calendar, it.ev)}${meaningOf(it.ev.color) ? ' · ' + meaningOf(it.ev.color) : ''}\n${it.plain}`"
                @click="emit('select', it.ev.id)" @keydown.enter.self="emit('select', it.ev.id)">
                <span v-if="it.ev.kind === 'event' || !it.barW" class="ax-dot" aria-hidden="true"></span>
                <span class="ax-text" :class="{ outside: it.outside }" :style="it.outside ? { left: it.barW + 6 + 'px' } : undefined">
                  <template v-for="(p, pi) in it.parts" :key="pi">
                    <a v-if="p.cardId" class="ax-ref" role="link" tabindex="0" :title="`打开「${p.text}」`" @click.stop="emit('open-card', p.cardId)" @keydown.enter.stop="emit('open-card', p.cardId)">{{ p.text }}</a>
                    <span v-else :class="{ broken: p.broken }">{{ p.text }}</span>
                  </template>
                  <span v-if="it.ev.images.length" class="ax-has" aria-hidden="true">🖼</span>
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
    <p class="ax-help">点事件看详情，点<u>带下划线的名字</u>直接打开那个条目；Ctrl + 滚轮缩放，拖动空白处平移<template v-if="canEdit">，双击空白处在那个时间加事件</template>。</p>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, onScopeDispose, ref, watch } from 'vue'
import {
  STATUS_META, LINE_COLORS, FOLD_WIDTH, buildAxis, axisTicks, eraBoundaries, eventStart, eventEnd, formatAbsYear, formatEventDate, formatSpan, titleParts,
  type Calendar, type ChronicleEvent, type ChronicleTimeline, type TitlePart,
} from '@/utils/chronicle'
import { useWorldStore } from '@/stores/world'
import { hueOf, legendEntries, useWorldLegend } from '@/utils/worldLegend'
import ColorLegend from './ColorLegend.vue'

const props = defineProps<{
  calendar: Calendar
  lanes: { line: ChronicleTimeline; depth: number; events: ChronicleEvent[] }[]
  /** 所有事件（找分叉 / 汇合点用，可能在别的线上） */
  allEvents: ChronicleEvent[]
  findCard: (id: string) => { title: string } | undefined
  selectedId?: string | null
  /** 筛选命中的事件；有筛选时其它的变淡 */
  highlight?: Set<string> | null
  canEdit?: boolean
  /** 世界 ID（读颜色图例用）；不传就用当前打开的世界 */
  projectId?: string
}>()
const emit = defineEmits<{ select: [id: string]; 'open-card': [id: string]; line: [id: string]; add: [lineId: string, t: number] }>()

const LABEL_W = 176, RULER_H = 36, SPINE_H = 16, ROW_H = 24
const lineColor = (l: ChronicleTimeline, i: number) => l.color || LINE_COLORS[i % LINE_COLORS.length]!

// ===== 颜色图例 =====
const worldStore = useWorldStore()
const legend = useWorldLegend(() => props.projectId || worldStore.currentProject?.id || '')
/** 事件自己选的颜色在图例里代表什么（没选颜色、没定义含义都是空） */
const meaningOf = (color?: string | null) => { const hue = hueOf(color); return hue ? legend.value[hue] ?? '' : '' }
// 看的时候只列轴上用到的颜色；能编辑的人看到全部定义过的
const legendItems = computed(() => legendEntries(legend.value, props.canEdit ? null
  : new Set(props.lanes.flatMap(l => l.events).map(e => hueOf(e.color)).filter((h): h is string => Boolean(h)))))
/** 只看某一种颜色（色系名），null = 全部。别的事件只是变淡，不挪位置，免得轴跳来跳去 */
const legendHue = ref<string | null>(null)
watch(legendItems, list => { if (legendHue.value && !list.some(e => e.key === legendHue.value)) legendHue.value = null })
const legendOff = (e: ChronicleEvent) => Boolean(legendHue.value) && hueOf(e.color) !== legendHue.value

// ===== 缩放 =====
const scrollEl = ref<HTMLElement | null>(null)
const pxPerYear = ref(10)
const fitted = ref(false)
const startOf = (e: ChronicleEvent) => eventStart(props.calendar, e)
const dated = computed(() => props.lanes.flatMap(l => l.events).filter(e => startOf(e) !== null))
const points = computed(() => dated.value.flatMap(e => [startOf(e)!, eventEnd(props.calendar, e)!]))
const skips = computed(() => dated.value.filter(e => e.kind === 'skip' && eventEnd(props.calendar, e)! > startOf(e)!)
  .map(e => ({ t0: startOf(e)!, t1: eventEnd(props.calendar, e)!, label: e.id })))

function build(px: number) {
  return buildAxis(points.value, skips.value, px, props.calendar.autoCompress !== false, props.calendar)
}
const axis = computed(() => build(pxPerYear.value))
const ticks = computed(() => axisTicks(axis.value, pxPerYear.value))
const eras = computed(() => eraBoundaries(props.calendar).filter(b => b.t > axis.value.min && b.t < axis.value.max))

/** 整条轴放进窗口：先估一次，按折叠后的实际长度再算一次 */
function fit() {
  const el = scrollEl.value
  const avail = Math.max(300, (el?.clientWidth ?? 900) - LABEL_W - 60 - 240)
  const span = Math.max(1e-3, Math.max(...points.value, 1) - Math.min(...points.value, 0))
  let px = avail / span
  for (let i = 0; i < 2; i++) {
    const a = build(px)
    const folded = a.segments.filter(s => s.folded)
    const open = a.segments.filter(s => !s.folded).reduce((s, g) => s + (g.t1 - g.t0), 0)
    px = Math.max(1e-6, (avail - folded.length * FOLD_WIDTH) / Math.max(open, 1e-3))
  }
  pxPerYear.value = clampPx(px)
  fitted.value = true
  void nextTick(() => { if (scrollEl.value) scrollEl.value.scrollLeft = 0 })
}
const clampPx = (px: number) => Math.min(1e7, Math.max(1e-6, px))
function zoomBy(f: number, anchorClientX?: number) {
  const el = scrollEl.value
  if (!el) { pxPerYear.value = clampPx(pxPerYear.value * f); return }
  const rect = el.getBoundingClientRect()
  const viewX = anchorClientX === undefined ? (el.clientWidth + LABEL_W) / 2 : anchorClientX - rect.left
  const t = axis.value.t(el.scrollLeft + viewX - LABEL_W)
  pxPerYear.value = clampPx(pxPerYear.value * f)
  void nextTick(() => { el.scrollLeft = Math.max(0, axis.value.x(t) + LABEL_W - viewX) })
}
function onWheel(e: WheelEvent) {
  if (!e.ctrlKey && !e.metaKey) return
  e.preventDefault()
  zoomBy(e.deltaY < 0 ? 1.25 : 1 / 1.25, e.clientX)
}
// 数据第一次到的时候自动适合宽度；后来增删事件不乱跳
watch(() => points.value.length > 0, has => { if (has && !fitted.value) void nextTick(fit) })
onMounted(() => { if (points.value.length) fit() })

// ===== 拖动平移 =====
const panning = ref(false)
let pan: { x: number; y: number; l: number; t: number; id: number } | null = null
function onPanStart(e: PointerEvent) {
  const target = e.target as HTMLElement
  if (e.button !== 0 || target.closest('.ax-item, button, a, .ax-label')) return
  const el = scrollEl.value!
  pan = { x: e.clientX, y: e.clientY, l: el.scrollLeft, t: el.scrollTop, id: e.pointerId }
  window.addEventListener('pointermove', onPanMove)
  window.addEventListener('pointerup', onPanEnd, { once: true })
}
function onPanMove(e: PointerEvent) {
  if (!pan || e.pointerId !== pan.id) return
  const dx = e.clientX - pan.x, dy = e.clientY - pan.y
  if (!panning.value && Math.hypot(dx, dy) < 4) return
  panning.value = true
  scrollEl.value!.scrollLeft = pan.l - dx
  scrollEl.value!.scrollTop = pan.t - dy
}
function onPanEnd() { pan = null; window.removeEventListener('pointermove', onPanMove); setTimeout(() => { panning.value = false }) }
onScopeDispose(() => window.removeEventListener('pointermove', onPanMove))

function onTrackDbl(e: MouseEvent, line: ChronicleTimeline) {
  if (!props.canEdit || line.virtual || (e.target as HTMLElement).closest('.ax-item')) return
  const rect = (e.currentTarget as HTMLElement).getBoundingClientRect()
  emit('add', line.id, axis.value.t(e.clientX - rect.left))
}

// ===== 刻度文字 =====
function tickLabel(t: number) {
  const step = ticks.value.length > 1 ? ticks.value[1]!.t - ticks.value[0]!.t : 1
  if (step >= 1 - 1e-9) return formatAbsYear(props.calendar, Math.round(t))
  const year = Math.floor(t + 1e-9)
  const frac = t - year
  const cal = props.calendar
  const dpy = cal.months.reduce((s, m) => s + m.days, 0) || 365
  let day = Math.round(frac * dpy)
  if (cal.useMonths) {
    for (let i = 0; i < cal.months.length; i++) {
      if (day < cal.months[i]!.days) return day === 0 ? `${formatAbsYear(cal, year)} ${cal.months[i]!.name}` : `${cal.months[i]!.name}${day + 1}日`
      day -= cal.months[i]!.days
    }
  }
  return `${formatAbsYear(cal, year)} 第${day + 1}天`
}

// ===== 折叠带 =====
const folds = computed(() => axis.value.segments.filter(s => s.folded).map(s => {
  // 明确的「时间跳跃」事件：显示它的名字，点了看详情
  const ev = dated.value.find(e => e.kind === 'skip' && Math.abs(startOf(e)! - s.t0) < 1e-9)
  const plain = ev ? titleParts(ev.title, props.findCard).map(p => p.text).join('') : ''
  return { x0: s.x0, x1: s.x1, eventId: ev?.id, short: ev ? `⋯ ${plain}` : s.label ?? '⋯', title: ev ? `时间跳跃：${plain}（${formatSpan(props.calendar, s.t1 - s.t0)}）` : `这里省略了 ${formatSpan(props.calendar, s.t1 - s.t0)} 的空白` }
}))

// ===== 排版：每条线里的事件分到几行，免得文字叠在一起 =====
let measureCtx: CanvasRenderingContext2D | null = null
function textWidth(s: string) {
  if (!measureCtx && typeof document !== 'undefined') {
    measureCtx = document.createElement('canvas').getContext('2d')
    if (measureCtx) measureCtx.font = '12px system-ui, -apple-system, "PingFang SC", "Microsoft YaHei", sans-serif'
  }
  return measureCtx ? measureCtx.measureText(s).width : s.length * 12
}

interface LaidItem { ev: ChronicleEvent; x0: number; barW: number; track: number; parts: TitlePart[]; plain: string; outside: boolean }
const laid = computed(() => {
  const ax = axis.value
  let top = 0
  return props.lanes.map(lane => {
    const ends: number[] = []
    const items: LaidItem[] = []
    const marks: { key: string; x: number; w?: number; kind: string }[] = []
    const evs = lane.events.filter(e => startOf(e) !== null && !(e.kind === 'skip' && eventEnd(props.calendar, e)! > startOf(e)!))
      .sort((a, b) => startOf(a)! - startOf(b)! || a.sortOrder - b.sortOrder)
    for (const ev of evs) {
      const parts = titleParts(ev.title, props.findCard)
      const plain = parts.map(p => p.text).join('')
      const x0 = ax.x(startOf(ev)!)
      const ranged = ev.kind !== 'event'
      const barW = ranged ? Math.max(0, ax.x(eventEnd(props.calendar, ev)!) - x0) : 0
      const labelW = Math.min(260, textWidth(plain) + (ev.images.length ? 18 : 0)) + 14
      const outside = ranged && barW < labelW
      const start = ranged ? x0 : x0 - 5
      const end = ranged ? (outside ? x0 + barW + 6 + labelW : x0 + barW) : x0 + labelW
      let track = ends.findIndex(e => e + 8 <= start)
      if (track < 0) { track = ends.length; ends.push(end) } else ends[track] = end
      items.push({ ev, x0: ranged && barW ? x0 : x0 - 5, barW: ranged ? barW : 0, track, parts, plain, outside })
      marks.push(ranged && barW > 2 ? { key: ev.id, x: x0, w: barW, kind: 'range k-' + ev.kind } : { key: ev.id, x: x0, kind: 'pt' })
    }
    const xs = items.flatMap(i => [i.x0 + 5 * (i.barW ? 0 : 1), i.x0 + i.barW])
    const fork = lane.line.forkEventId ? props.allEvents.find(e => e.id === lane.line.forkEventId) : undefined
    const merge = lane.line.mergeEventId ? props.allEvents.find(e => e.id === lane.line.mergeEventId) : undefined
    if (fork && startOf(fork) !== null) xs.push(ax.x(forkPoint(fork)))
    if (merge && startOf(merge) !== null) xs.push(ax.x(startOf(merge)!))
    const height = SPINE_H + Math.max(1, ends.length) * ROW_H + 10
    const out = { ...lane, top, height, items, marks, spine: xs.length ? [Math.min(...xs), Math.max(...xs)] as [number, number] : null }
    top += height
    return out
  })
})
const totalHeight = computed(() => laid.value.reduce((s, l) => s + l.height, 0) || 80)
/** 从时期分叉：在时期结束的地方分出去；从事件分叉：在那个时间点 */
const forkPoint = (e: ChronicleEvent) => e.kind === 'event' ? startOf(e)! : eventEnd(props.calendar, e)!

// ===== 分叉 / 汇合曲线 =====
const links = computed(() => {
  const ax = axis.value
  const laneOfLine = new Map(laid.value.map((l, i) => [l.line.id, i]))
  const spineY = (i: number) => laid.value[i]!.top + SPINE_H / 2 + 1
  const laneOfEvent = (e: ChronicleEvent) => laneOfLine.get(e.timelineId)
  const out: { key: string; d: string; color: string; kind: string }[] = []
  laid.value.forEach((lane, i) => {
    const color = lineColor(lane.line, i)
    const y = spineY(i)
    const fork = lane.line.forkEventId ? props.allEvents.find(e => e.id === lane.line.forkEventId) : undefined
    const fromLane = fork ? laneOfEvent(fork) : lane.line.parentId ? laneOfLine.get(lane.line.parentId) : undefined
    if (fromLane !== undefined && fromLane !== i && lane.spine) {
      const fx = fork && startOf(fork) !== null ? ax.x(forkPoint(fork)) : lane.spine[0] - 24
      const py = spineY(fromLane)
      const tx = Math.max(fx + 18, lane.spine[0])
      out.push({ key: 'fork' + lane.line.id, color, kind: 'fork', d: `M${fx},${py} C${fx + 14},${py} ${tx - 14},${y} ${tx},${y}` })
    }
    const merge = lane.line.mergeEventId ? props.allEvents.find(e => e.id === lane.line.mergeEventId) : undefined
    const toLane = merge ? laneOfEvent(merge) : undefined
    if (merge && toLane !== undefined && toLane !== i && startOf(merge) !== null && lane.spine) {
      const mx = ax.x(startOf(merge)!)
      const ty = spineY(toLane)
      const sx = Math.min(lane.spine[1], mx - 18)
      out.push({ key: 'merge' + lane.line.id, color, kind: 'merge', d: `M${sx},${y} C${sx + 14},${y} ${mx - 14},${ty} ${mx},${ty}` })
    }
  })
  return out
})

const dimmed = (lineId: string) => Boolean(props.highlight) && !laid.value.find(l => l.line.id === lineId)?.items.some(i => props.highlight!.has(i.ev.id))

// 选中的事件滚到看得见的地方
watch(() => props.selectedId, id => {
  if (!id) return
  void nextTick(() => {
    const el = scrollEl.value
    const lane = laid.value.find(l => l.items.some(i => i.ev.id === id))
    const it = lane?.items.find(i => i.ev.id === id)
    if (!el || !lane || !it) return
    const x = it.x0 + LABEL_W
    if (x < el.scrollLeft + LABEL_W || x > el.scrollLeft + el.clientWidth - 120) el.scrollTo({ left: Math.max(0, x - LABEL_W - el.clientWidth / 3), behavior: 'smooth' })
    const y = lane.top + RULER_H
    if (y < el.scrollTop || y > el.scrollTop + el.clientHeight - 60) el.scrollTo({ top: Math.max(0, y - 40), behavior: 'smooth' })
  })
}, { immediate: true })
defineExpose({ fit })
</script>

<style scoped>
.ax { position: relative; display: flex; flex-direction: column; min-height: 0; flex: 1; }
.ax-legend { flex-shrink: 0; margin-bottom: 6px; }
.ax-zoom { position: absolute; z-index: 8; right: 12px; bottom: 34px; display: flex; gap: 2px; background: var(--paper-card); box-shadow: 0 1px 4px rgba(0, 0, 0, .12); }
.ax-zoom button { min-width: 30px; height: 28px; padding: 0 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.ax-zoom button:hover { color: var(--accent-color); border-color: var(--accent-color); }
.ax-scroll { flex: 1; min-height: 240px; overflow: auto; overscroll-behavior: contain; cursor: grab; border: 1px solid var(--border-line); background: var(--paper-card); }
.ax-scroll.panning { cursor: grabbing; user-select: none; }
.ax-inner { position: relative; min-width: 100%; }
.ax-ruler { position: sticky; top: 0; z-index: 6; display: flex; background: var(--bg-sub); border-bottom: 1px solid var(--border-line); }
.ax-corner { position: sticky; left: 0; z-index: 7; flex-shrink: 0; box-sizing: border-box; padding: 10px; font-size: 12px; color: var(--text-secondary); background: var(--bg-sub); border-right: 1px solid var(--border-line); }
.ax-ruler-track { position: relative; flex-shrink: 0; overflow: hidden; }
.ax-tick { position: absolute; bottom: 4px; transform: translateX(-50%); font-size: 11px; color: var(--text-secondary); white-space: nowrap; font-variant-numeric: tabular-nums; }
.ax-tick::after { content: ''; position: absolute; left: 50%; bottom: -4px; height: 4px; border-left: 1px solid var(--border-line); }
.ax-era { position: absolute; top: 2px; padding-left: 4px; border-left: 2px solid currentColor; font-size: 11px; font-weight: 600; color: var(--accent-color); white-space: nowrap; }
.ax-fold-label { position: absolute; top: 0; bottom: 0; box-sizing: border-box; padding: 0 2px; border: 0; background: transparent; color: var(--text-secondary); font: inherit; font-size: 10px; line-height: 1.2; white-space: normal; overflow: hidden; cursor: default; }
.ax-fold-label.explicit { cursor: pointer; color: var(--text-primary); }
.ax-fold-label.explicit:hover { color: var(--accent-color); }
.ax-lanes { position: relative; }
.ax-fold-band { position: absolute; top: 0; bottom: 0; z-index: 1; pointer-events: none;
  background: repeating-linear-gradient(135deg, transparent 0 6px, color-mix(in srgb, var(--text-secondary) 14%, transparent) 6px 7px), color-mix(in srgb, var(--bg-sub) 70%, transparent);
  border-left: 1px dashed var(--border-line); border-right: 1px dashed var(--border-line); }
.ax-era-line { position: absolute; top: 0; bottom: 0; z-index: 1; border-left: 1px dashed color-mix(in srgb, var(--accent-color) 45%, transparent); pointer-events: none; }
.ax-links { position: absolute; top: 0; z-index: 2; pointer-events: none; overflow: visible; }
.ax-links path { stroke-width: 2; opacity: .75; }
.ax-links path.merge { stroke-dasharray: 5 3; }

.ax-lane { position: absolute; left: 0; right: 0; border-bottom: 1px solid color-mix(in srgb, var(--border-line) 60%, transparent); }
.ax-lane.dim { opacity: .45; }
.ax-label { position: sticky; left: 0; z-index: 5; box-sizing: border-box; height: 100%; display: flex; align-items: flex-start; gap: 6px; padding: 6px 8px 6px 10px; background: var(--paper-card); border-right: 1px solid var(--border-line); }
.ax-branch-mark { color: var(--text-secondary); font-size: 12px; line-height: 20px; }
.ax-swatch { flex-shrink: 0; width: 10px; height: 10px; margin-top: 5px; background: var(--lc); }
.st-whatif .ax-swatch { background: repeating-linear-gradient(45deg, var(--lc) 0 2px, transparent 2px 4px); outline: 1px solid var(--lc); }
.st-draft .ax-swatch { background: transparent; outline: 1px dashed var(--lc); }
.ax-label-text { min-width: 0; display: flex; flex-wrap: wrap; align-items: baseline; gap: 2px 6px; }
.ax-line-name { padding: 0; border: 0; background: none; color: var(--text-primary); font: inherit; font-size: 13px; font-weight: 600; text-align: left; cursor: pointer; overflow-wrap: anywhere; }
.ax-line-name:hover { color: var(--accent-color); }
.virtual .ax-line-name { font-weight: 500; color: var(--text-secondary); }
.ax-status { font-size: 11px; padding: 0 4px; border: 1px solid var(--lc); color: var(--lc); }
.ax-lock { font-size: 11px; }
.ax-line-card { font-size: 11px; color: var(--accent-color); cursor: pointer; text-decoration: none; }
.ax-line-card:hover { text-decoration: underline; }

.ax-track { position: absolute; top: 0; bottom: 0; }
.ax-spine { position: absolute; top: 7px; height: 3px; background: var(--lc); opacity: .8; }
.st-whatif .ax-spine { background: repeating-linear-gradient(90deg, var(--lc) 0 6px, transparent 6px 10px); }
.st-draft .ax-spine { opacity: .35; }
.virtual .ax-spine { opacity: .3; }
.ax-mark { position: absolute; top: 3px; height: 11px; }
.ax-mark.pt { width: 2px; margin-left: -1px; background: var(--lc); }
.ax-mark.range { top: 6px; height: 5px; background: var(--lc); }
.ax-mark.range.k-lost { background: repeating-linear-gradient(135deg, var(--lc) 0 2px, transparent 2px 5px); }

.ax-item { position: absolute; z-index: 3; height: 20px; display: flex; align-items: center; gap: 5px; box-sizing: border-box; cursor: pointer; white-space: nowrap; font-size: 12px; color: var(--text-primary); outline: none; }
.ax-dot { flex-shrink: 0; width: 10px; height: 10px; border-radius: 50%; background: var(--ec, var(--lc)); box-shadow: 0 0 0 2px var(--paper-card); }
.ax-text { max-width: 260px; overflow: hidden; text-overflow: ellipsis; padding: 1px 3px; }
.ax-item.k-period, .ax-item.k-lost, .ax-item.k-skip { min-width: 10px; }
.ax-item.k-period.bar { background: color-mix(in srgb, var(--ec, var(--lc)) 22%, var(--paper-card)); border-left: 3px solid var(--ec, var(--lc)); }
.ax-item.k-lost.bar { background: repeating-linear-gradient(135deg, color-mix(in srgb, var(--ec, var(--lc)) 30%, transparent) 0 4px, transparent 4px 9px); border: 1px dashed var(--ec, var(--lc)); color: var(--text-secondary); font-style: italic; }
.ax-item.k-skip .ax-dot { border-radius: 0; transform: rotate(45deg); width: 8px; height: 8px; }
.ax-text.outside { position: absolute; }
.ax-item:hover .ax-text, .ax-item:focus-visible .ax-text { background: var(--bg-sub); }
.ax-item.sel .ax-text { background: var(--accent-color); color: #fff; }
.ax-item.sel .ax-ref { color: #fff; }
.ax-item.sel .ax-dot { box-shadow: 0 0 0 2px var(--paper-card), 0 0 0 4px var(--accent-color); }
.ax-item.hid { opacity: .55; }
.ax-item.leg-off { opacity: .15; }
.ax-item.leg-off:hover, .ax-item.leg-off:focus-visible { opacity: .6; }
.ax-item.match .ax-text { box-shadow: inset 0 -2px 0 #e0a800; }
.ax-ref { color: var(--accent-color); text-decoration: underline; text-underline-offset: 2px; cursor: pointer; }
.ax-ref:hover { text-decoration-thickness: 2px; }
.broken { color: var(--text-secondary); text-decoration: line-through; }
.ax-has { margin-left: 3px; font-size: 10px; }
.ax-help { margin: 6px 0 0; font-size: 12px; color: var(--text-secondary); }
@media (max-width: 640px) { .ax-help { display: none; } }
</style>
