<template>
  <div class="data-center-panel fade-in">
    <div class="panel-header">
      <div class="header-title">
        <h2>社区数据</h2>
        <span class="subtitle">在线、活跃、签到与新用户，可和历史对比。日期一律按北京时间。<template v-if="live">更新于 {{ live.now.slice(11) }}</template></span>
      </div>
      <div class="header-actions">
        <label class="auto-refresh"><input v-model="autoRefresh" type="checkbox" /> 每 30 秒自动刷新在线数据</label>
        <button class="btn-primary" :disabled="loading" @click="refreshAll">{{ loading ? '刷新中…' : '全部刷新' }}</button>
      </div>
    </div>

    <p v-if="error" class="panel-error" role="alert">{{ error }}</p>
    <p v-if="!live && loading" role="status">正在读取社区数据…</p>

    <!-- ① 实时 -->
    <section v-if="live" class="block">
      <h3 class="block-title">实时 <small>最近 {{ live.onlineWindowMinutes }} 分钟内有访问的算在线</small></h3>
      <div class="live-grid">
        <div class="metric-card hero">
          <span class="metric-name">当前在线</span>
          <div class="metric-value"><span class="live-dot" aria-hidden="true"></span>{{ live.online.total }}</div>
          <div class="metric-sub">成员 {{ live.online.members }} · 游客 {{ live.online.guests }}</div>
          <div class="metric-sub">今日峰值 <b>{{ live.peak.total }}</b>（{{ live.peak.at }}）</div>
        </div>
        <div v-for="card in liveCards" :key="card.label" class="metric-card">
          <span class="metric-name">{{ card.label }}</span>
          <div class="metric-value">{{ card.value.toLocaleString() }}</div>
          <div v-if="card.delta" class="metric-trend" :class="card.delta.cls" :title="card.deltaTitle">{{ card.delta.text }} <span>{{ card.deltaLabel }}</span></div>
          <div v-if="card.note" class="metric-sub">{{ card.note }}</div>
        </div>
      </div>
      <div class="online-list">
        <span class="online-list-title">在线成员（{{ live.online.members }}）</span>
        <p v-if="!live.onlineUsers.length" class="muted">现在没有登录用户在线。</p>
        <ul v-else>
          <li v-for="u in live.onlineUsers" :key="u.id">
            <UserLink :id="u.id" :name="u.name" class="online-user">
              <img v-if="u.avatar" :src="u.avatar" alt="" loading="lazy" /><span v-else class="avatar-fallback" aria-hidden="true">{{ u.name.slice(0, 1) }}</span>
              {{ u.name }}
            </UserLink>
            <small>{{ ago(u.secondsAgo) }}</small>
          </li>
        </ul>
      </div>
    </section>

    <!-- ② 趋势对比 -->
    <section v-if="trend" class="block">
      <div class="block-head">
        <h3 class="block-title">趋势对比 <small>最近 {{ trend.days }} 天（实线）与之前 {{ trend.days }} 天（虚线）</small></h3>
        <div class="seg" role="group" aria-label="时间范围">
          <button v-for="d in RANGES" :key="d" type="button" :class="{ active: days === d }" :aria-pressed="days === d" @click="setDays(d)">{{ d }} 天</button>
        </div>
      </div>
      <p v-if="trackingNote" class="note">{{ trackingNote }}</p>
      <div class="seg metric-tabs" role="group" aria-label="指标">
        <button v-for="m in METRICS" :key="m.key" type="button" :class="{ active: metric === m.key }" :aria-pressed="metric === m.key" @click="metric = m.key">{{ m.label }}</button>
      </div>
      <div ref="trendEl" class="chart chart-lg" role="img" :aria-label="`${metricLabel}：最近 ${trend.days} 天与之前 ${trend.days} 天对比`"></div>

      <div class="summary-grid">
        <div v-for="s in summaryCards" :key="s.label" class="summary-card">
          <span class="metric-name">{{ s.label }}</span>
          <div class="summary-value">{{ s.value }}</div>
          <div class="metric-trend" :class="s.delta.cls">{{ s.delta.text }} <span>前期 {{ s.previous }}</span></div>
        </div>
      </div>

      <div class="table-tools">
        <button type="button" class="link-btn" @click="showTable = !showTable">{{ showTable ? '收起数据表' : '查看数据表' }}</button>
        <button type="button" class="link-btn" @click="exportCsv">导出 CSV</button>
      </div>
      <div v-if="showTable" class="table-wrap">
        <table>
          <thead><tr><th>日期</th><th>活跃用户</th><th>游客</th><th>在线峰值</th><th>签到</th><th>签到率</th><th>新注册</th></tr></thead>
          <tbody>
            <tr v-for="r in [...trend.current].reverse()" :key="r.date">
              <td>{{ r.date }}</td><td>{{ r.active ?? '—' }}</td><td>{{ r.visitors ?? '—' }}</td><td>{{ r.peakOnline ?? '—' }}</td>
              <td>{{ r.signed }}</td><td>{{ signRate(r) }}</td><td>{{ r.newUsers }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- ③ 在线曲线 -->
    <section v-if="curves" class="block">
      <div class="block-head">
        <h3 class="block-title">一天中的在线人数 <small>每 5 分钟记录一次</small></h3>
        <label class="date-pick">日期 <input v-model="curveDate" type="date" :max="todayStr" @change="loadCurves" /></label>
      </div>
      <p v-if="!curves.day.points.length && !curves.previousDay.points.length" class="muted">这一天还没有在线记录（上线新版本后开始记录）。</p>
      <div v-else ref="curveEl" class="chart" role="img" aria-label="当天、前一天和上周同一天的在线人数曲线"></div>
    </section>

    <!-- ④ 签到分析 + 留存 -->
    <section v-if="sign" class="block">
      <h3 class="block-title">签到分析 <small>正在连签的用户 {{ sign.activeStreakUsers }} 人</small></h3>
      <div class="sign-grid">
        <figure class="mini">
          <figcaption>当前连签天数分布</figcaption>
          <div ref="streakEl" class="chart chart-sm" role="img" aria-label="连签天数分布"></div>
        </figure>
        <figure class="mini">
          <figcaption>签到时间段（近 30 天）</figcaption>
          <div ref="hourEl" class="chart chart-sm" role="img" aria-label="一天中各小时的签到次数"></div>
        </figure>
        <figure class="mini">
          <figcaption>星期几签到最多（近 8 周日均）</figcaption>
          <div ref="weekEl" class="chart chart-sm" role="img" aria-label="周一到周日的平均签到人数"></div>
        </figure>
        <figure class="mini rank">
          <figcaption>连签排行</figcaption>
          <p v-if="!sign.topStreaks.length" class="muted">暂无正在连签的用户。</p>
          <ol v-else>
            <li v-for="(u, i) in sign.topStreaks" :key="u.id">
              <span class="rank-no">{{ i + 1 }}</span>
              <UserLink :id="u.id" :name="u.name" class="rank-name" />
              <b>{{ u.streak }} 天</b><small>最长 {{ u.best }}</small>
            </li>
          </ol>
        </figure>
      </div>
      <div v-if="trend" class="retention">
        <div class="summary-card">
          <span class="metric-name">新用户次日留存</span>
          <div class="summary-value">{{ pct(trend.retention.d1.returned, trend.retention.d1.users) }}</div>
          <div class="metric-sub">近 {{ trend.days }} 天注册的 {{ trend.retention.d1.users }} 人中，第二天回来 {{ trend.retention.d1.returned }} 人</div>
        </div>
        <div class="summary-card">
          <span class="metric-name">新用户 7 日留存</span>
          <div class="summary-value">{{ pct(trend.retention.d7.returned, trend.retention.d7.users) }}</div>
          <div class="metric-sub">注册满 7 天的 {{ trend.retention.d7.users }} 人中，7 天内回来过 {{ trend.retention.d7.returned }} 人</div>
        </div>
      </div>
    </section>

    <!-- ⑤ 各板块内容：总数、公开的、最近 N 天新增（和之前对比）、每天新增 -->
    <section v-if="content" class="block">
      <div class="block-head">
        <h3 class="block-title">各板块内容 <small>总数含下架 / 私密 / 审核中；「公开」是大家看得到的；新增按北京时间、和趋势用同一个时间范围（{{ content.days }} 天）</small></h3>
      </div>
      <div class="table-wrap content-wrap">
        <table class="content-table">
          <thead><tr><th>板块</th><th class="num">总数</th><th class="num">公开</th><th class="num">今天</th><th class="num">最近 {{ content.days }} 天</th><th class="num">较之前 {{ content.days }} 天</th><th>每天新增</th></tr></thead>
          <tbody>
            <template v-for="g in contentGroups" :key="g.name">
              <tr class="group-row"><td colspan="7">{{ g.name }}</td></tr>
              <tr v-for="m in g.modules" :key="m.key" :class="{ 'row-error': m.error }">
                <td :title="m.note || ''">{{ m.label }}<small v-if="m.note" class="row-note">{{ m.note }}</small></td>
                <template v-if="m.error"><td colspan="6" class="muted">{{ m.error }}</td></template>
                <template v-else>
                  <td class="num">{{ m.total.toLocaleString() }}</td>
                  <td class="num">{{ m.visible == null ? '—' : m.visible.toLocaleString() }}<small v-if="m.visible != null && m.total > m.visible" class="row-note">{{ (m.total - m.visible).toLocaleString() }} 不公开</small></td>
                  <td class="num">{{ m.today }}</td>
                  <td class="num"><b>{{ m.current.toLocaleString() }}</b></td>
                  <td class="num"><span class="metric-trend" :class="delta(m.current, m.previous).cls">{{ delta(m.current, m.previous).text }}</span> <small class="muted">{{ m.previous }}</small></td>
                  <td><span class="spark" :title="sparkTitle(m)"><i v-for="(v, i) in m.series" :key="i" :style="{ height: sparkH(m, v) }"></i></span></td>
                </template>
              </tr>
            </template>
          </tbody>
        </table>
      </div>
    </section>
    <!-- 旧接口兜底：新接口读不到（后端还没更新）时显示原来的内容总量 -->
    <section v-else-if="distItems.length" class="block">
      <h3 class="block-title">内容总量</h3>
      <div v-for="item in distItems" :key="item.label" class="dist-row">
        <span class="dist-label">{{ item.label }}</span>
        <span class="dist-bar"><span class="dist-fill" :style="{ width: item.pct + '%' }"></span></span>
        <span class="dist-value">{{ item.value.toLocaleString() }}</span>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, nextTick, watch } from 'vue'
import type { ECharts } from 'echarts/core'
import request from '@/utils/request'
import UserLink from '@/components/UserLink.vue'
import { analyticsApi, type ContentModule, type ContentStats, type LiveStats, type OnlineCurves, type SignStats, type TrendRow, type TrendStats } from '@/api/analytics'

type MetricKey = 'active' | 'signed' | 'newUsers' | 'peakOnline' | 'visitors'
const RANGES = [7, 30, 90] as const
const METRICS: { key: MetricKey; label: string }[] = [
  { key: 'active', label: '活跃用户' },
  { key: 'signed', label: '签到人数' },
  { key: 'newUsers', label: '新注册' },
  { key: 'peakOnline', label: '在线峰值' },
  { key: 'visitors', label: '游客' },
]

const loading = ref(false), error = ref('')
const live = ref<LiveStats | null>(null)
const trend = ref<TrendStats | null>(null)
const curves = ref<OnlineCurves | null>(null)
const sign = ref<SignStats | null>(null)
const overview = ref<Record<string, number> | null>(null)
const content = ref<ContentStats | null>(null)
const days = ref<number>(30)
const metric = ref<MetricKey>('active')
const showTable = ref(false)
const autoRefresh = ref(true)

const localDate = (d = new Date()) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
const todayStr = computed(() => live.value?.now.slice(0, 10) ?? localDate())
const curveDate = ref('')

// ---------- 小工具 ----------
function delta(cur: number | null | undefined, prev: number | null | undefined) {
  if (cur == null || prev == null) return { text: '—', cls: 'flat' }
  if (prev === 0) return cur === 0 ? { text: '持平', cls: 'flat' } : { text: `+${cur}`, cls: 'up' }
  const p = Math.round((cur - prev) / prev * 1000) / 10
  return { text: p === 0 ? '持平' : `${p > 0 ? '↑' : '↓'} ${Math.abs(p)}%`, cls: p > 0 ? 'up' : p < 0 ? 'down' : 'flat' }
}
const pct = (a: number, b: number) => b ? `${Math.round(a / b * 1000) / 10}%` : '—'
const signRate = (r: TrendRow) => r.active ? pct(r.signed, r.active) : '—'
const ago = (s: number) => s < 60 ? '刚刚' : `${Math.floor(s / 60)} 分钟前`
const sum = (rows: TrendRow[], k: MetricKey) => rows.reduce((t, r) => t + (r[k] ?? 0), 0)
const tracked = (rows: TrendRow[], k: MetricKey) => rows.filter(r => r[k] != null)
const avg = (rows: TrendRow[], k: MetricKey) => { const t = tracked(rows, k); return t.length ? Math.round(sum(t, k) / t.length * 10) / 10 : null }

const liveCards = computed(() => {
  const l = live.value
  if (!l) return []
  const y = l.yesterdaySoFar
  const title = '和昨天同一时间点相比，避免拿“半天”比“一整天”'
  return [
    { label: '今日活跃用户', value: l.today.active, delta: delta(l.today.active, y.active), deltaLabel: '较昨日同时段', deltaTitle: title, note: `昨日全天 ${l.yesterday.active}` },
    { label: '今日签到', value: l.today.signed, delta: delta(l.today.signed, y.signed), deltaLabel: '较昨日同时段', deltaTitle: title, note: `签到率 ${pct(l.today.signed, l.today.active)}（签到 ÷ 活跃）` },
    { label: '今日新注册', value: l.today.newUsers, delta: delta(l.today.newUsers, y.newUsers), deltaLabel: '较昨日同时段', deltaTitle: title, note: `昨日全天 ${l.yesterday.newUsers}` },
    { label: '今日游客', value: l.today.visitors, delta: null, deltaLabel: '', deltaTitle: '', note: '未登录的访问者（按浏览器计）' },
    { label: '注册用户总数', value: l.totalUsers, delta: null, deltaLabel: '', deltaTitle: '', note: '' },
  ]
})

const trackingNote = computed(() => {
  const t = trend.value
  if (!t) return ''
  if (!t.trackingSince) return '活跃、游客和在线数据从这个版本上线后开始记录，现在还没有数据；签到和注册是完整的历史数据。'
  const first = t.previous[0]?.date ?? t.current[0]?.date
  return first && t.trackingSince > first ? `活跃、游客和在线数据从 ${t.trackingSince} 开始记录，之前的日期显示为空（不是 0）；签到和注册是完整的历史数据。` : ''
})

const summaryCards = computed(() => {
  const t = trend.value
  if (!t) return []
  const c = t.summary.current, p = t.summary.previous
  const label = t.days === 7 ? '周活跃（不重复）' : t.days === 30 ? '月活跃（不重复）' : `${t.days} 天活跃（不重复）`
  const rate = (rows: TrendRow[]) => { const r = rows.filter(x => x.active); return r.length ? Math.round(r.reduce((s, x) => s + x.signed / x.active!, 0) / r.length * 1000) / 10 : null }
  const cr = rate(t.current), pr = rate(t.previous)
  return [
    { label, value: c.uniqueActive ?? '—', previous: p.uniqueActive ?? '—', delta: delta(c.uniqueActive, p.uniqueActive) },
    { label: '日均活跃', value: avg(t.current, 'active') ?? '—', previous: avg(t.previous, 'active') ?? '—', delta: delta(avg(t.current, 'active'), avg(t.previous, 'active')) },
    { label: '签到过的用户（不重复）', value: c.uniqueSigners, previous: p.uniqueSigners, delta: delta(c.uniqueSigners, p.uniqueSigners) },
    { label: '平均签到率', value: cr == null ? '—' : `${cr}%`, previous: pr == null ? '—' : `${pr}%`, delta: delta(cr, pr) },
    { label: '新注册', value: c.newUsers, previous: p.newUsers, delta: delta(c.newUsers, p.newUsers) },
    { label: '日均在线峰值', value: avg(t.current, 'peakOnline') ?? '—', previous: avg(t.previous, 'peakOnline') ?? '—', delta: delta(avg(t.current, 'peakOnline'), avg(t.previous, 'peakOnline')) },
  ]
})
const metricLabel = computed(() => METRICS.find(m => m.key === metric.value)?.label ?? '')

// 各板块内容：按分组排（顺序跟着接口）
const contentGroups = computed(() => {
  const out: { name: string; modules: ContentModule[] }[] = []
  for (const m of content.value?.modules ?? []) {
    let g = out.find(x => x.name === m.group)
    if (!g) { g = { name: m.group, modules: [] }; out.push(g) }
    g.modules.push(m)
  }
  return out
})
const sparkH = (m: ContentModule, v: number) => { const max = Math.max(1, ...m.series); return v ? `${Math.max(8, Math.round(v / max * 100))}%` : '0' }
const sparkTitle = (m: ContentModule) => {
  const dates = content.value?.dates ?? []
  const max = Math.max(...m.series)
  const i = m.series.indexOf(max)
  return max > 0 ? `最多的一天：${dates[i]}（${max}）` : '这段时间没有新增'
}
async function loadContent() {
  try { content.value = await analyticsApi.content(days.value) } catch { content.value = null }
}

const distItems = computed(() => {
  const o = overview.value
  if (!o) return []
  const items = [
    { label: '用户', value: o.userCount ?? 0 }, { label: '作品', value: o.workCount ?? 0 },
    { label: '博客', value: o.blogCount ?? 0 }, { label: '帖子', value: o.postCount ?? 0 },
    { label: '百科', value: o.wikiCount ?? 0 }, { label: '项目', value: o.projectCount ?? 0 },
  ]
  const max = Math.max(1, ...items.map(i => i.value))
  return items.map(i => ({ ...i, pct: Math.round(i.value / max * 100) }))
})

// ---------- 图表 ----------
const trendEl = ref<HTMLElement>(), curveEl = ref<HTMLElement>(), streakEl = ref<HTMLElement>(), hourEl = ref<HTMLElement>(), weekEl = ref<HTMLElement>()
const charts = new Map<string, ECharts>()
let echarts: typeof import('@/utils/analyticsCharts') | null = null
let resizeObserver: ResizeObserver | undefined

function palette() {
  const s = getComputedStyle(document.documentElement)
  const v = (n: string, f: string) => s.getPropertyValue(n).trim() || f
  return { accent: v('--accent-color', '#0b6e8a'), muted: v('--text-secondary', '#6b7280'), text: v('--text-primary', '#111'), line: v('--border-line', '#e5e7eb'), surface: v('--paper-card', '#fff') }
}
function baseOption() {
  const c = palette()
  return {
    animationDuration: 400,
    textStyle: { color: c.muted, fontSize: 12 },
    grid: { left: 8, right: 16, top: 36, bottom: 8, containLabel: true },
    tooltip: { trigger: 'axis', backgroundColor: c.surface, borderColor: c.line, textStyle: { color: c.text, fontSize: 12 }, axisPointer: { type: 'line', lineStyle: { color: c.line } } },
    xAxis: { type: 'category', axisLine: { lineStyle: { color: c.line } }, axisTick: { show: false }, axisLabel: { color: c.muted, hideOverlap: true } },
    yAxis: { type: 'value', minInterval: 1, splitLine: { lineStyle: { color: c.line, type: 'dashed' } }, axisLabel: { color: c.muted } },
  }
}
function render(name: string, el: HTMLElement | undefined, option: Record<string, unknown>) {
  if (!el || !echarts) return
  let chart = charts.get(name)
  if (chart && chart.getDom() !== el) { chart.dispose(); chart = undefined }
  if (!chart) { chart = echarts.init(el); charts.set(name, chart); resizeObserver?.observe(el) }
  chart.setOption(option, true)
}

function drawTrend() {
  const t = trend.value
  if (!t) return
  const c = palette(), k = metric.value, label = metricLabel.value
  const base = baseOption()
  const short = (d: string) => d.slice(5).replace('-', '/')
  render('trend', trendEl.value, {
    ...base,
    legend: { top: 0, left: 0, textStyle: { color: c.muted }, itemWidth: 18, data: [`最近 ${t.days} 天`, `之前 ${t.days} 天`] },
    tooltip: {
      ...base.tooltip,
      formatter: (items: { dataIndex: number }[]) => {
        const i = items[0]?.dataIndex ?? 0
        const cur = t.current[i], prev = t.previous[i]
        const fmt = (r: TrendRow | undefined) => r ? (r[k] == null ? '无数据' : String(r[k])) : '—'
        return `<b>${label}</b><br>${cur?.date ?? ''}：<b>${fmt(cur)}</b><br><span style="opacity:.7">${prev?.date ?? ''}：${fmt(prev)}</span>`
      },
    },
    xAxis: { ...base.xAxis, data: t.current.map(r => short(r.date)), boundaryGap: false },
    series: [
      { name: `之前 ${t.days} 天`, type: 'line', data: t.previous.map(r => r[k]), showSymbol: false, connectNulls: false, lineStyle: { width: 2, type: 'dashed', color: c.muted }, itemStyle: { color: c.muted }, z: 1 },
      { name: `最近 ${t.days} 天`, type: 'line', data: t.current.map(r => r[k]), showSymbol: t.days <= 30, symbolSize: 8, connectNulls: false, lineStyle: { width: 2, color: c.accent }, itemStyle: { color: c.accent }, areaStyle: { color: c.accent, opacity: 0.08 }, z: 2 },
    ],
  })
}

function drawCurves() {
  const cv = curves.value
  if (!cv) return
  const c = palette(), base = baseOption()
  const slots = Array.from({ length: 288 }, (_, i) => i * 5)
  const label = (m: number) => `${String(Math.floor(m / 60)).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}`
  const series = (points: { minute: number; total: number }[]) => { const map = new Map(points.map(p => [p.minute - p.minute % 5, p.total])); return slots.map(s => map.get(s) ?? null) }
  // 今天只画到现在，之后留空
  render('curve', curveEl.value, {
    ...base,
    legend: { top: 0, left: 0, textStyle: { color: c.muted }, itemWidth: 18 },
    xAxis: { ...base.xAxis, data: slots.map(label), boundaryGap: false, axisLabel: { color: c.muted, interval: 35 } },
    series: [
      { name: `上周同日 ${cv.lastWeek.date.slice(5)}`, type: 'line', data: series(cv.lastWeek.points), showSymbol: false, lineStyle: { width: 1.5, type: 'dotted', color: c.muted }, itemStyle: { color: c.muted } },
      { name: `前一天 ${cv.previousDay.date.slice(5)}`, type: 'line', data: series(cv.previousDay.points), showSymbol: false, lineStyle: { width: 2, type: 'dashed', color: c.muted }, itemStyle: { color: c.muted } },
      { name: `${cv.day.date === todayStr.value ? '今天' : '当天'} ${cv.day.date.slice(5)}`, type: 'line', data: series(cv.day.points), showSymbol: false, lineStyle: { width: 2, color: c.accent }, itemStyle: { color: c.accent }, areaStyle: { color: c.accent, opacity: 0.08 } },
    ],
  })
}

function bars(name: string, el: HTMLElement | undefined, labels: string[], values: number[], unit: string) {
  const c = palette(), base = baseOption()
  render(name, el, {
    ...base,
    grid: { ...base.grid, top: 12 },
    tooltip: { ...base.tooltip, axisPointer: { type: 'shadow' }, valueFormatter: (v: number) => `${v} ${unit}` },
    xAxis: { ...base.xAxis, data: labels },
    series: [{ type: 'bar', data: values, barMaxWidth: 28, itemStyle: { color: c.accent, borderRadius: [4, 4, 0, 0] } }],
  })
}
function drawSign() {
  const s = sign.value
  if (!s) return
  bars('streak', streakEl.value, s.distribution.map(d => d.label), s.distribution.map(d => d.count), '人')
  bars('hour', hourEl.value, s.hours.map((_, h) => `${h}时`), s.hours, '次')
  bars('week', weekEl.value, s.weekdays.map(w => `周${w.label}`), s.weekdays.map(w => w.average), '人/天')
}

// ---------- 数据 ----------
async function ensureCharts() { echarts ??= await import('@/utils/analyticsCharts') }

async function loadLive() {
  try { live.value = await analyticsApi.live(); if (!curveDate.value) curveDate.value = live.value.now.slice(0, 10) }
  catch { error.value = '在线数据读取失败。如果刚更新过后端，请确认已重新部署。' }
}
async function loadTrend() {
  try { trend.value = await analyticsApi.trend(days.value); await ensureCharts(); await nextTick(); drawTrend() }
  catch { error.value = '趋势数据读取失败，请稍后重试。' }
}
async function loadCurves() {
  try { curves.value = await analyticsApi.onlineCurve(curveDate.value || undefined); await ensureCharts(); await nextTick(); drawCurves() }
  catch { /* 曲线失败不影响其他部分 */ }
}
async function loadSign() {
  try { sign.value = await analyticsApi.sign(); await ensureCharts(); await nextTick(); drawSign() }
  catch { /* 同上 */ }
}

async function refreshAll() {
  if (loading.value) return
  loading.value = true; error.value = ''
  try {
    await loadLive()
    await Promise.all([
      loadTrend(), loadCurves(), loadSign(), loadContent(),
      request.get<Record<string, number>>('/DataPanel/overview', { silentError: true }).then(o => { overview.value = o }).catch(() => undefined),
    ])
  } finally { loading.value = false }
}

function setDays(d: number) { if (days.value !== d) { days.value = d; void loadTrend(); void loadContent() } }
watch(metric, drawTrend)

function exportCsv() {
  const t = trend.value
  if (!t) return
  const head = '日期,活跃用户,游客,在线峰值,签到,新注册'
  const rows = [...t.previous, ...t.current].map(r => [r.date, r.active ?? '', r.visitors ?? '', r.peakOnline ?? '', r.signed, r.newUsers].join(','))
  const blob = new Blob(['﻿' + [head, ...rows].join('\n')], { type: 'text/csv;charset=utf-8' })
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = `社区数据_${t.previous[0]?.date ?? ''}_${t.current[t.current.length - 1]?.date ?? ''}.csv`
  a.click()
  setTimeout(() => URL.revokeObjectURL(a.href), 1000)
}

// 自动刷新在线数据（页面隐藏时不刷新）
let liveTimer = 0
function tick() { if (autoRefresh.value && !document.hidden) void loadLive() }

onMounted(() => {
  resizeObserver = new ResizeObserver(() => charts.forEach(c => c.resize()))
  void refreshAll()
  liveTimer = window.setInterval(tick, 30_000)
})
onBeforeUnmount(() => {
  window.clearInterval(liveTimer)
  resizeObserver?.disconnect()
  charts.forEach(c => c.dispose())
  charts.clear()
})
</script>

<style scoped>
.data-center-panel { display: grid; gap: 24px; color: var(--text-primary); }
.panel-header { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 16px 20px; padding: 24px; background: var(--paper-card); border: 1px solid var(--border-line); }
h2 { margin: 0 0 8px; font-size: 24px; }
.subtitle { color: var(--text-secondary); font-size: 14px; }
.header-actions { display: flex; flex-wrap: wrap; align-items: center; gap: 12px 16px; }
.auto-refresh { display: inline-flex; align-items: center; gap: 6px; font-size: 13px; color: var(--text-secondary); cursor: pointer; }
.btn-primary { background: var(--accent-color); color: var(--on-accent); border: 0; padding: 10px 16px; cursor: pointer; }
.btn-primary:disabled { opacity: .6; cursor: default; }
.panel-error { margin: 0; padding: 10px 14px; border-left: 3px solid var(--danger); background: var(--paper-card); color: var(--danger); }
.muted { color: var(--text-secondary); font-size: 13px; margin: 6px 0; }
.note { margin: 0 0 12px; padding: 8px 12px; font-size: 12.5px; color: var(--text-secondary); background: var(--bg-sub); border-left: 3px solid var(--accent-color); }

.block { background: var(--paper-card); border: 1px solid var(--border-line); padding: 22px 24px; min-width: 0; }
.block-head { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 10px 16px; margin-bottom: 12px; }
.block-title { margin: 0 0 14px; font-size: 18px; }
.block-head .block-title { margin: 0; }
.block-title small { margin-left: 8px; font-size: 12px; font-weight: 400; color: var(--text-secondary); }

.live-grid { display: grid; grid-template-columns: 1.3fr repeat(5, minmax(0, 1fr)); gap: 14px; }
.metric-card { min-width: 0; border: 1px solid var(--border-line); padding: 16px; background: var(--bg-main, transparent); }
.metric-card.hero { border-color: var(--accent-color); border-left-width: 3px; }
.metric-name { font-size: 13px; color: var(--text-secondary); }
.metric-value { display: flex; align-items: center; gap: 10px; margin: 10px 0 6px; font-size: 30px; font-weight: 650; font-variant-numeric: tabular-nums; }
.hero .metric-value { font-size: 40px; }
.live-dot { width: 10px; height: 10px; border-radius: 50%; background: var(--success); box-shadow: 0 0 0 0 var(--success); animation: live-pulse 2s infinite; }
@keyframes live-pulse { 0% { box-shadow: 0 0 0 0 color-mix(in srgb, var(--success) 50%, transparent); } 70% { box-shadow: 0 0 0 10px transparent; } 100% { box-shadow: 0 0 0 0 transparent; } }
@media (prefers-reduced-motion: reduce) { .live-dot { animation: none; } }
.metric-sub { margin-top: 4px; font-size: 12px; color: var(--text-secondary); }
.metric-trend { font-size: 13px; font-variant-numeric: tabular-nums; color: var(--text-secondary); }
.metric-trend span { color: var(--text-secondary); font-size: 12px; }
.metric-trend.up { color: var(--success); }
.metric-trend.down { color: var(--danger); }

.online-list { margin-top: 16px; padding-top: 14px; border-top: 1px dashed var(--border-line); }
.online-list-title { font-size: 13px; color: var(--text-secondary); }
.online-list ul { list-style: none; display: flex; flex-wrap: wrap; gap: 8px; margin: 10px 0 0; padding: 0; max-height: 180px; overflow: auto; }
.online-list li { display: inline-flex; align-items: center; gap: 6px; padding: 4px 10px 4px 4px; border: 1px solid var(--border-line); font-size: 13px; }
.online-list li small { color: var(--text-secondary); font-size: 11px; }
.online-user img, .avatar-fallback { width: 22px; height: 22px; object-fit: cover; border-radius: 50%; }
.avatar-fallback { display: inline-grid; place-items: center; background: var(--accent-soft, var(--bg-sub)); color: var(--accent-color); font-size: 12px; }

.seg { display: inline-flex; flex-wrap: wrap; gap: 0; border: 1px solid var(--border-line); }
.seg button { padding: 6px 12px; border: 0; border-right: 1px solid var(--border-line); background: transparent; color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.seg button:last-child { border-right: 0; }
.seg button.active { background: var(--accent-color); color: var(--on-accent); }
.metric-tabs { margin-bottom: 8px; }
.chart { width: 100%; height: 300px; }
.chart-lg { height: 340px; }
.chart-sm { height: 200px; }

.summary-grid { display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)); gap: 12px; margin-top: 16px; }
.summary-card { min-width: 0; padding: 12px 14px; border: 1px solid var(--border-line); }
.summary-value { margin: 6px 0 4px; font-size: 22px; font-weight: 650; font-variant-numeric: tabular-nums; }
.table-tools { display: flex; gap: 16px; margin-top: 14px; }
.link-btn { padding: 0; border: 0; background: none; color: var(--accent-color); font: inherit; font-size: 13px; cursor: pointer; }
.link-btn:hover { text-decoration: underline; }
.table-wrap { margin-top: 10px; max-height: 360px; overflow: auto; border: 1px solid var(--border-line); }
table { width: 100%; border-collapse: collapse; font-size: 13px; font-variant-numeric: tabular-nums; }
th, td { padding: 7px 12px; text-align: right; border-bottom: 1px solid var(--border-line); white-space: nowrap; }
th:first-child, td:first-child { text-align: left; }
th { position: sticky; top: 0; background: var(--paper-card); color: var(--text-secondary); font-weight: 500; }

.date-pick { display: inline-flex; align-items: center; gap: 8px; font-size: 13px; color: var(--text-secondary); }
.date-pick input { padding: 5px 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; }

.sign-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 14px; }
.mini { margin: 0; min-width: 0; padding: 12px; border: 1px solid var(--border-line); }
.mini figcaption { font-size: 13px; color: var(--text-secondary); margin-bottom: 4px; }
.rank ol { list-style: none; margin: 6px 0 0; padding: 0; display: grid; gap: 6px; }
.rank li { display: grid; grid-template-columns: 20px minmax(0, 1fr) auto auto; align-items: baseline; gap: 8px; font-size: 13px; }
.rank-no { color: var(--text-secondary); font-variant-numeric: tabular-nums; }
.rank-name { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.rank li small { color: var(--text-secondary); font-size: 11px; }
.retention { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 14px; margin-top: 14px; }

.table-wrap.content-wrap { max-height: none; }
.content-table { width: 100%; border-collapse: collapse; font-size: 13px; }
.content-table th, .content-table td { padding: 7px 10px; border-bottom: 1px solid var(--border-line); text-align: left; vertical-align: middle; }
.content-table th { font-size: 12px; font-weight: 500; color: var(--text-secondary); white-space: nowrap; }
.content-table .num { text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
.content-table .group-row td { padding-top: 14px; font-size: 12px; font-weight: 600; color: var(--accent-color); border-bottom: 0; }
.content-table .row-note { display: block; font-size: 11px; font-weight: 400; color: var(--text-secondary); }
.content-table .row-error td { color: var(--text-secondary); }
.spark { display: flex; align-items: flex-end; gap: 1px; width: 160px; height: 26px; }
.spark i { flex: 1; min-width: 1px; background: var(--accent-color); opacity: .75; }
.dist-row { display: flex; align-items: center; gap: 14px; font-size: 14px; margin-top: 10px; }
.dist-label { width: 48px; flex-shrink: 0; color: var(--text-secondary); }
.dist-bar { flex: 1; height: 10px; background: var(--bg-sub); overflow: hidden; }
.dist-fill { display: block; height: 100%; background: var(--accent-color); }
.dist-value { width: 70px; text-align: right; font-variant-numeric: tabular-nums; }

@media (max-width: 1280px) { .live-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); } .summary-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); } .sign-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 640px) { .live-grid, .summary-grid, .sign-grid, .retention { grid-template-columns: 1fr; } .block { padding: 16px; } }
</style>
