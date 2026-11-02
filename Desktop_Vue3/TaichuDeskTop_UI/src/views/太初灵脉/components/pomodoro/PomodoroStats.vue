<template>
  <section class="ps-root" aria-label="专注统计">
    <div class="ps-tiles">
      <div class="ps-tile">
        <span class="ps-plate">TODAY · 轮</span>
        <span class="ps-num">{{ todayDone }}</span>
        <span class="ps-label">今天完成几轮</span>
      </div>
      <div class="ps-tile">
        <span class="ps-plate">TODAY · 时长</span>
        <span class="ps-num">{{ todayMinutes }}<small>分钟</small></span>
        <span class="ps-label">今天专注时长</span>
      </div>
    </div>

    <!-- 近 7 天：一根柱子一天，高度 = 专注分钟数 -->
    <figure class="ps-chart" :aria-label="chartLabel">
      <figcaption class="ps-cap">
        <span>近 7 天专注（分钟）</span>
        <span class="ps-cap-sum">合计 {{ weekMinutes }} 分钟</span>
      </figcaption>
      <div class="ps-bars" aria-hidden="true">
        <div
          v-for="d in days"
          :key="d.start"
          class="ps-bar-col"
          :class="{ today: d.isToday }"
          :title="`${d.label}：专注 ${d.minutes} 分钟，完成 ${d.done} 轮`"
        >
          <span class="ps-bar-val">{{ d.minutes || '' }}</span>
          <span class="ps-bar-track"><i class="ps-bar" :style="{ height: barHeight(d.minutes) }"></i></span>
          <span class="ps-bar-day">{{ d.label }}</span>
        </div>
      </div>
    </figure>

    <div class="ps-list-head">
      <h3>今天的记录</h3>
      <span v-if="error" class="ps-error">{{ error }}</span>
    </div>
    <p v-if="!today.length" class="ps-empty">今天还没有专注记录。完成一段专注，这里就会多一条。</p>
    <ul v-else class="ps-list">
      <li v-for="s in today" :key="s.clientId" class="ps-item" :class="{ abandoned: !s.done }">
        <span class="ps-item-time">{{ fmtTime(s.startMs) }}–{{ fmtTime(s.endMs) }}</span>
        <span class="ps-item-task">
          {{ s.task || '（没写在做什么）' }}
          <small v-if="s.plannerEventId" class="ps-item-link" title="关联了生活管理里的事项">📅 生活管理</small>
        </span>
        <span class="ps-item-min">{{ Math.round(s.focusSec / 60) }} 分</span>
        <span class="ps-item-tag">{{ s.done ? '完成' : '放弃' }}</span>
        <button type="button" class="ps-del" :aria-label="`删除 ${fmtTime(s.startMs)} 这条记录`" @click="emit('remove', s.clientId)">✕</button>
      </li>
    </ul>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { PomoSession } from '@/api/pomodoro'
import { dayStartMs, fmtTime, last7Days, shiftDayStart } from './pomodoroModel'

const props = defineProps<{
  sessions: PomoSession[]
  /** 现在（毫秒，已经按服务器时间校正过） */
  now: number
  error: string
}>()

const emit = defineEmits<{ (e: 'remove', clientId: string): void }>()

/** 今天（本地日期）的记录，最新的在上面；只按「开始时间」归到哪一天 */
const today = computed(() => {
  const start = dayStartMs(props.now)
  const end = shiftDayStart(start, 1)
  return props.sessions.filter(s => s.startMs >= start && s.startMs < end).sort((a, b) => b.startMs - a.startMs)
})
const todayDone = computed(() => today.value.filter(s => s.done).length)
// 放弃的那几段也是实打实专注过的时间，一起算进时长
const todayMinutes = computed(() => Math.round(today.value.reduce((sum, s) => sum + s.focusSec, 0) / 60))

const days = computed(() => last7Days(props.sessions, props.now))
const weekMinutes = computed(() => days.value.reduce((sum, d) => sum + d.minutes, 0))
/** 纵轴最少按 1 小时画，免得只专注了 5 分钟柱子也顶满 */
const maxMinutes = computed(() => Math.max(60, ...days.value.map(d => d.minutes)))
const barHeight = (m: number) => (m <= 0 ? '0' : `${Math.max(3, (m / maxMinutes.value) * 100)}%`)
/** 读屏软件读这一句，代替看图 */
const chartLabel = computed(() => '近 7 天专注分钟数：' + days.value.map(d => `${d.label} ${d.minutes} 分钟`).join('，'))
</script>

<style scoped>
.ps-root { display: flex; flex-direction: column; gap: 14px; min-width: 0; }
.ps-tiles { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px; }
.ps-tile { position: relative; display: flex; flex-direction: column; gap: 2px; padding: 10px 12px; background: var(--paper-card); border: 1px solid var(--border-line); min-width: 0; }
.ps-plate { font: 600 10px/1.4 var(--font-mono, monospace); letter-spacing: .08em; color: var(--text-secondary); }
.ps-num { font: 700 30px/1.1 var(--font-display, sans-serif); color: var(--text-primary); font-variant-numeric: tabular-nums; }
.ps-num small { margin-left: 4px; font: 600 12px var(--font-sans, sans-serif); color: var(--text-secondary); }
.ps-label { font-size: 12px; color: var(--text-secondary); }

.ps-chart { margin: 0; padding: 10px 12px 8px; border: 1px solid var(--border-line); background: var(--paper-card); }
.ps-cap { display: flex; justify-content: space-between; gap: 8px; font-size: 12px; font-weight: 700; color: var(--text-secondary); margin-bottom: 6px; }
.ps-cap-sum { font: 600 11px var(--font-mono, monospace); }
.ps-bars { display: grid; grid-template-columns: repeat(7, minmax(0, 1fr)); gap: 6px; height: 132px; }
.ps-bar-col { display: flex; flex-direction: column; align-items: stretch; min-width: 0; }
.ps-bar-val { height: 16px; text-align: center; font: 600 11px/16px var(--font-mono, monospace); color: var(--text-secondary); }
.ps-bar-track { position: relative; flex: 1; border-bottom: 1px solid var(--border-line); background-image: linear-gradient(var(--grid-line, rgb(0 0 0 / 6%)) 1px, transparent 1px); background-size: 100% 25%; }
.ps-bar { position: absolute; left: 22%; right: 22%; bottom: 0; background: color-mix(in srgb, var(--accent-color) 55%, var(--paper-card)); transition: height .25s; }
.ps-bar-col.today .ps-bar { background: var(--accent-color); }
.ps-bar-col:hover .ps-bar { background: var(--accent-color); }
.ps-bar-day { margin-top: 4px; text-align: center; font-size: 11px; color: var(--text-secondary); white-space: nowrap; }
.ps-bar-col.today .ps-bar-day { color: var(--text-primary); font-weight: 700; }

.ps-list-head { display: flex; align-items: baseline; justify-content: space-between; gap: 8px; }
.ps-list-head h3 { margin: 0; font-size: 13px; font-weight: 800; color: var(--text-secondary); }
.ps-error { font-size: 12px; color: var(--danger, #d33); }
.ps-empty { margin: 0; font-size: 13px; color: var(--text-secondary); }
.ps-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; border-top: 1px solid var(--border-line); max-height: 320px; overflow-y: auto; }
.ps-item { display: flex; align-items: center; gap: 10px; padding: 7px 4px; border-bottom: 1px solid var(--border-line); font-size: 13px; min-width: 0; }
.ps-item-time { flex: 0 0 auto; font: 600 12px var(--font-mono, monospace); color: var(--text-secondary); }
.ps-item-task { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: var(--text-primary); }
.ps-item-link { margin-left: 6px; font-size: 11px; color: var(--text-secondary); }
.ps-item-min { flex: 0 0 auto; font: 600 12px var(--font-mono, monospace); color: var(--text-primary); }
.ps-item-tag { flex: 0 0 auto; font-size: 11px; font-weight: 700; padding: 1px 6px; color: var(--success, #267453); background: var(--success-soft, #e5f2e9); }
.ps-item.abandoned .ps-item-tag { color: var(--text-secondary); background: var(--bg-sub); }
.ps-item.abandoned .ps-item-task { color: var(--text-secondary); }
.ps-del { flex: 0 0 auto; background: none; border: none; color: var(--text-secondary); cursor: pointer; font-size: 12px; padding: 2px 4px; opacity: .6; }
.ps-del:hover, .ps-del:focus-visible { color: var(--danger, #d33); opacity: 1; }
@media (prefers-reduced-motion: reduce) { .ps-bar { transition: none; } }
</style>
