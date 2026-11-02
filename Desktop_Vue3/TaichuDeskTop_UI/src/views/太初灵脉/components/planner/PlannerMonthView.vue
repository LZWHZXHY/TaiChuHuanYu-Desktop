<template>
  <div class="pm-month">
    <div class="pm-week-head" aria-hidden="true">
      <span v-for="(w, i) in WEEK_HEADERS" :key="w" :class="{ weekend: i >= 5 }">{{ w }}</span>
    </div>

    <div class="pm-grid" role="grid" aria-label="月历">
      <div
        v-for="cell in cells"
        :key="cell.date"
        class="pm-cell"
        :class="{ 'other-month': !cell.inMonth, today: cell.isToday, selected: cell.date === selected, weekend: cell.weekend, 'drop-hover': dropDate === cell.date }"
        role="gridcell"
        @click="emit('pick', cell.date)"
        @dragover.prevent="onDragOver(cell.date)"
        @dragleave="dropDate = ''"
        @drop.prevent="onDrop($event, cell.date)"
      >
        <div class="pm-cell-head">
          <button type="button" class="pm-day-num" :aria-label="`查看 ${formatDayLabel(cell.date)} 的安排`" @click.stop="emit('pick', cell.date)">
            {{ cell.dayNum }}
          </button>
          <span v-if="cell.isToday" class="pm-today-tag">今天</span>
          <button v-if="!readOnly" type="button" class="pm-add" :aria-label="`在 ${formatDayLabel(cell.date)} 新建安排`" @click.stop="emit('add', cell.date)">＋</button>
        </div>

        <!-- 电脑上：时间 + 标题 -->
        <ul class="pm-list">
          <li
            v-for="o in visible(cell.date)"
            :key="o.key"
            class="pm-strip"
            :class="{ done: o.done }"
            :style="{ '--ev-color': colorOf(o.ev), '--ev-tint': colorOf(o.ev) + '1f' }"
            :draggable="!readOnly && o.ev.repeat === 'none'"
            :title="`${timeLabel(o.ev)} ${o.ev.title}${o.ev.location ? ' · ' + o.ev.location : ''}`"
            @dragstart="onDragStart($event, o)"
            @click.stop="emit('open', o)"
          >
            <button
              type="button"
              class="pm-check"
              :class="{ on: o.done }"
              :disabled="readOnly"
              :aria-label="o.done ? `把「${o.ev.title}」标记为没完成` : `把「${o.ev.title}」标记为完成`"
              @click.stop="emit('toggle', o)"
            ></button>
            <span v-if="!o.ev.allDay && o.ev.startTime" class="pm-time">{{ o.ev.startTime }}</span>
            <span class="pm-title">{{ o.ev.title }}</span>
            <span v-if="o.ev.repeat !== 'none'" class="pm-repeat" aria-label="重复">↻</span>
          </li>
        </ul>
        <button v-if="hidden(cell.date) > 0" type="button" class="pm-more" @click.stop="emit('pick', cell.date)">+{{ hidden(cell.date) }}</button>

        <!-- 手机上：小色块 + 数量 -->
        <div v-if="(occ[cell.date] || []).length" class="pm-dots" aria-hidden="true">
          <i v-for="o in (occ[cell.date] || []).slice(0, 4)" :key="o.key" :class="{ done: o.done }" :style="{ background: colorOf(o.ev) }"></i>
          <small v-if="(occ[cell.date] || []).length > 4">+{{ (occ[cell.date] || []).length - 4 }}</small>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { WEEK_HEADERS, colorOf, formatDayLabel, timeLabel, type DayCell, type Occurrence } from './plannerModel'

const props = defineProps<{
  cells: DayCell[]
  occ: Record<string, Occurrence[]>
  selected: string
  readOnly: boolean
}>()

const emit = defineEmits<{
  (e: 'pick', date: string): void
  (e: 'add', date: string): void
  (e: 'open', o: Occurrence): void
  (e: 'toggle', o: Occurrence): void
  (e: 'move', id: string, date: string): void
}>()

/** 每格最多直接显示几条，多的显示「+N」点进当天详情 */
const MAX_IN_CELL = 3
const visible = (date: string) => (props.occ[date] || []).slice(0, MAX_IN_CELL)
const hidden = (date: string) => Math.max(0, (props.occ[date] || []).length - MAX_IN_CELL)

// 拖动：只有不重复的安排能拖到别的日子（重复事项请在编辑里改第一次的日期）
const dropDate = ref('')
let draggingId = ''
const onDragStart = (e: DragEvent, o: Occurrence) => {
  if (props.readOnly || o.ev.repeat !== 'none') { e.preventDefault(); return }
  draggingId = o.ev.id
  if (e.dataTransfer) { e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/plain', o.ev.id) }
}
const onDragOver = (date: string) => { if (draggingId) dropDate.value = date }
const onDrop = (e: DragEvent, date: string) => {
  dropDate.value = ''
  const id = draggingId || e.dataTransfer?.getData('text/plain') || ''
  draggingId = ''
  if (id && !props.readOnly) emit('move', id, date)
}
</script>

<style scoped>
.pm-month { display: flex; flex-direction: column; min-width: 0; }
.pm-week-head { display: grid; grid-template-columns: repeat(7, minmax(0, 1fr)); font-size: 12px; font-weight: 700; color: var(--text-secondary); text-align: center; padding: 6px 0; border-bottom: 1px solid var(--border-line); }
.pm-week-head .weekend { color: var(--accent-color); }
.pm-grid { display: grid; grid-template-columns: repeat(7, minmax(0, 1fr)); grid-auto-rows: minmax(108px, auto); border-left: 1px solid var(--border-line); }
.pm-cell { position: relative; min-width: 0; padding: 4px 4px 6px; border-right: 1px solid var(--border-line); border-bottom: 1px solid var(--border-line); background: var(--paper-card); cursor: pointer; display: flex; flex-direction: column; gap: 3px; transition: background .15s; }
.pm-cell:hover { background: var(--bg-sub); }
.pm-cell.weekend { background: color-mix(in srgb, var(--bg-sub) 45%, var(--paper-card)); }
.pm-cell.other-month { opacity: .5; }
.pm-cell.selected { box-shadow: inset 0 0 0 2px var(--accent-color); }
.pm-cell.today { background: var(--accent-soft); }
.pm-cell.drop-hover { box-shadow: inset 0 0 0 2px var(--accent-color); background: var(--accent-soft); }
.pm-cell-head { display: flex; align-items: center; gap: 4px; min-height: 22px; }
.pm-day-num { background: none; border: none; padding: 0 4px; min-width: 24px; height: 22px; font: 600 13px/22px monospace; color: var(--text-primary); cursor: pointer; text-align: left; border-radius: 0; }
.pm-cell.today .pm-day-num { background: var(--accent-color); color: var(--paper-card); text-align: center; }
.pm-today-tag { font-size: 11px; color: var(--accent-color); font-weight: 700; }
.pm-add { margin-left: auto; background: none; border: 1px solid transparent; color: var(--text-secondary); width: 22px; height: 22px; padding: 0; cursor: pointer; opacity: 0; border-radius: 0; font-size: 13px; line-height: 1; }
.pm-cell:hover .pm-add, .pm-add:focus-visible { opacity: 1; }
.pm-add:hover { color: var(--accent-color); border-color: var(--accent-color); }
.pm-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 2px; min-width: 0; }
.pm-strip { display: flex; align-items: center; gap: 4px; min-width: 0; padding: 2px 4px; font-size: 12px; line-height: 18px; color: var(--text-primary); background: var(--ev-tint); border-left: 3px solid var(--ev-color); cursor: pointer; }
.pm-strip:hover { filter: brightness(.97); outline: 1px solid var(--ev-color); }
.pm-strip[draggable="true"] { cursor: grab; }
.pm-strip.done { opacity: .45; }
.pm-strip.done .pm-title { text-decoration: line-through; }
.pm-check { flex: 0 0 auto; width: 12px; height: 12px; padding: 0; border: 1.5px solid var(--ev-color); background: transparent; border-radius: 0; cursor: pointer; }
.pm-check.on { background: var(--ev-color); }
.pm-check:disabled { cursor: default; }
.pm-time { flex: 0 0 auto; font: 600 11px/18px monospace; color: var(--text-secondary); }
.pm-title { flex: 1 1 auto; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 600; }
.pm-repeat { flex: 0 0 auto; font-size: 11px; color: var(--text-secondary); }
.pm-more { align-self: flex-start; background: none; border: none; padding: 0 4px; font-size: 12px; font-weight: 700; color: var(--accent-color); cursor: pointer; }
.pm-dots { display: none; }
@media (hover: none) { .pm-add { opacity: 1; } }
/* 手机：格子太窄，只显示色块和数量，点格子看当天详情 */
@media (max-width: 640px) {
  .pm-grid { grid-auto-rows: minmax(58px, auto); }
  .pm-list, .pm-more, .pm-add, .pm-today-tag { display: none; }
  .pm-dots { display: flex; flex-wrap: wrap; gap: 2px; align-items: center; padding: 0 2px; }
  .pm-dots i { width: 7px; height: 7px; display: block; }
  .pm-dots i.done { opacity: .35; }
  .pm-dots small { font-size: 10px; color: var(--text-secondary); }
  .pm-day-num { font-size: 12px; padding: 0 2px; min-width: 20px; }
}
@media (prefers-reduced-motion: reduce) { .pm-cell { transition: none; } }
</style>
