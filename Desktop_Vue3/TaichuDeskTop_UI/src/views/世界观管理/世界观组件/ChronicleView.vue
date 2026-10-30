<!-- 世界编年史：整个世界的历史。多条时间线（正史、各国史、人物生平、假如……），可以分叉、汇合，
     有时期、时间跳跃、失落的历史，用世界自己的历法。两种看法：「时间轴」和按年代读的「编年史」。
     事件里 @ 的条目、相关条目、地点，点了都直接打开那个条目的完整页面（内容长，小卡片放不下）。 -->
<template>
  <div class="chron" :class="{ 'has-detail': selected }">
    <header class="ch-head">
      <div class="ch-title">
        <h2>📜 世界编年史</h2>
        <p>{{ lines.length ? `${lines.length} 条时间线 · ${realEvents.length} 件事` : '把这个世界发生过的事按时间串起来' }}<template v-if="!calendarIsDefault"> · 历法：{{ calendar.eras.map(e => e.name).join(' / ') }}</template></p>
      </div>
      <div class="ch-tools">
        <div class="ch-seg" role="tablist" aria-label="看法">
          <button type="button" role="tab" :aria-selected="mode === 'axis'" @click="setMode('axis')">时间轴</button>
          <button type="button" role="tab" :aria-selected="mode === 'list'" @click="setMode('list')">编年史</button>
        </div>
        <input v-model="query" type="search" class="ch-search" placeholder="搜索事件、标签…" aria-label="搜索事件" />
        <details ref="linesMenu" class="ch-menu">
          <summary>时间线 {{ hiddenLines.size ? `（隐藏了 ${hiddenLines.size}）` : '' }}</summary>
          <div class="ch-menu-pop">
            <label v-for="l in allLanes" :key="l.line.id" class="ch-menu-item" :style="{ paddingLeft: 10 + l.depth * 14 + 'px' }">
              <input type="checkbox" :checked="!hiddenLines.has(l.line.id)" @change="toggleLine(l.line.id)" />
              <span class="ch-sw" :style="{ background: colorOf(l.line) }"></span>{{ l.line.name }}
              <small v-if="l.line.status !== 'canon' && !l.line.virtual">{{ STATUS_META[l.line.status].label }}</small>
              <small v-if="l.line.hidden">访客不可见</small>
            </label>
            <button v-if="hiddenLines.size" type="button" class="ch-link" @click="hiddenLines = new Set(); saveHidden()">全部显示</button>
          </div>
        </details>
        <template v-if="editable">
          <button type="button" class="ch-btn primary" :disabled="!lines.length" @click="openNewEvent()">＋ 事件</button>
          <button type="button" class="ch-btn" @click="openLine(null)">＋ 时间线</button>
          <button type="button" class="ch-btn" title="纪元、月份、年的叫法" @click="calendarOpen = true">⚙ 历法</button>
        </template>
      </div>
      <div v-if="tagFilter || cardFilter" class="ch-filters">
        <span>只看：</span>
        <button v-if="tagFilter" type="button" class="ch-filter" @click="tagFilter = ''">#{{ tagFilter }} ×</button>
        <button v-if="cardFilter" type="button" class="ch-filter" @click="cardFilter = ''">和「{{ cardTitle(cardFilter) }}」有关 ×</button>
        <small>{{ matched?.size ?? 0 }} 件事</small>
      </div>
    </header>

    <p v-if="loading" class="ch-empty">正在读取编年史…</p>
    <p v-else-if="loadError" class="ch-empty" role="alert">编年史暂时读不出来。<button type="button" class="ch-link" @click="load">重新加载</button></p>
    <div v-else-if="!lines.length && !legacy.length" class="ch-empty ch-start">
      <template v-if="editable">
        <p>还没有编年史。先建一条「正史」，然后往上加事件；之后可以再加各国史、人物生平，或者从某件事分出「假如」的支线。</p>
        <button type="button" class="ch-btn primary" :disabled="busy" @click="quickStart">开始写编年史</button>
        <button type="button" class="ch-btn" @click="calendarOpen = true">先设置历法</button>
      </template>
      <p v-else>作者还没有写编年史，可以从目录继续阅读世界设定。</p>
    </div>

    <div v-else class="ch-body">
      <div class="ch-main">
        <template v-if="mode === 'axis'">
          <ChronicleAxis ref="axisRef" :calendar="calendar" :lanes="axisLanes" :all-events="allEvents" :find-card="findCard" :selected-id="selectedId"
            :highlight="matched" :can-edit="editable" @select="select" @open-card="openCard" @line="onLineClick" @add="addAt" />
          <div v-if="undated.length" class="ch-undated">
            <span class="ch-undated-title">时间未定（{{ undated.length }}）</span>
            <button v-for="ev in undated" :key="ev.id" type="button" class="ch-undated-item" :class="{ sel: ev.id === selectedId, dim: matched && !matched.has(ev.id) }"
              :style="{ '--lc': colorOfId(ev.timelineId) }" @click="select(ev.id)">{{ ev.dateText ? ev.dateText + ' · ' : '' }}{{ plain(ev.title) }}</button>
          </div>
        </template>

        <!-- 编年史：按纪元、年份往下读 -->
        <div v-else class="ch-list">
          <p v-if="!listGroups.length" class="ch-empty">没有符合条件的事件。</p>
          <section v-for="g in listGroups" :key="g.key" class="ch-era">
            <h3 class="ch-era-title" :style="{ borderColor: g.color || undefined }">{{ g.title }}</h3>
            <ol class="ch-entries">
              <li v-for="row in g.rows" :key="row.ev.id" class="ch-entry" :class="['k-' + row.ev.kind, { sel: row.ev.id === selectedId }]" :style="{ '--lc': row.ev.color || colorOfId(row.ev.timelineId) }">
                <div class="ch-when">{{ row.when }}</div>
                <article class="ch-what" @click="onEntryClick($event, row.ev.id)">
                  <div class="ch-line-tag">
                    <span class="ch-sw"></span>{{ lineName(row.ev.timelineId) }}
                    <span v-if="row.ev.kind !== 'event'" class="ch-kind">{{ KIND_META[row.ev.kind].icon }} {{ KIND_META[row.ev.kind].label }}</span>
                    <span v-if="row.ev.hidden" class="ch-kind">访客不可见</span>
                  </div>
                  <h4 class="ch-ev-title">
                    <template v-for="(p, pi) in parts(row.ev.title)" :key="pi">
                      <a v-if="p.cardId" class="ch-ref" :href="cardHref(p.cardId)" @click.prevent.stop="openCard(p.cardId)">{{ p.text }}</a>
                      <span v-else :class="{ broken: p.broken }">{{ p.text }}</span>
                    </template>
                  </h4>
                  <!-- eslint-disable-next-line vue/no-v-html -->
                  <div v-if="row.ev.description" class="ch-desc" v-html="renderDesc(row.ev.description)"></div>
                  <div v-if="row.ev.images.length" class="ch-thumbs">
                    <a v-for="url in row.ev.images.slice(0, 4)" :key="url" :href="url" target="_blank" rel="noopener" @click.stop><img :src="withThumbnail(url, 200)" alt="" loading="lazy" /></a>
                    <span v-if="row.ev.images.length > 4" class="ch-more">+{{ row.ev.images.length - 4 }}</span>
                  </div>
                  <div v-if="row.links.length || row.ev.tags.length" class="ch-chips">
                    <a v-for="c in row.links" :key="c.id" class="ch-card" :href="cardHref(c.id)" @click.prevent.stop="openCard(c.id)">{{ c.icon }} {{ c.title }}</a>
                    <button v-for="t in row.ev.tags" :key="t" type="button" class="ch-tag" @click.stop="tagFilter = t">#{{ t }}</button>
                  </div>
                </article>
              </li>
            </ol>
          </section>
        </div>
      </div>

      <!-- 选中事件的详情 -->
      <aside v-if="selected" class="ch-detail" :style="{ '--lc': selected.color || colorOfId(selected.timelineId) }" aria-label="事件详情">
        <header class="cd-head">
          <span class="cd-kind">{{ KIND_META[selected.kind].icon }} {{ KIND_META[selected.kind].label }}</span>
          <button type="button" class="cd-line" @click="onLineClick(selected.timelineId)"><span class="ch-sw"></span>{{ lineName(selected.timelineId) }}</button>
          <span class="ch-sp"></span>
          <button type="button" class="cd-nav" :disabled="!neighbors.prev" title="这条线上的上一件事" aria-label="上一件" @click="neighbors.prev && select(neighbors.prev)">‹</button>
          <button type="button" class="cd-nav" :disabled="!neighbors.next" title="这条线上的下一件事" aria-label="下一件" @click="neighbors.next && select(neighbors.next)">›</button>
          <button type="button" class="cd-nav" aria-label="关闭详情" @click="select(null)">×</button>
        </header>
        <div class="cd-body">
          <p class="cd-date">{{ formatEventDate(calendar, selected) }}<small v-if="spanOf(selected)"> · {{ spanOf(selected) }}</small></p>
          <h3 class="cd-title">
            <template v-for="(p, pi) in parts(selected.title)" :key="pi">
              <a v-if="p.cardId" class="ch-ref" :href="cardHref(p.cardId)" :title="`打开「${p.text}」`" @click.prevent="openCard(p.cardId)">{{ p.text }}</a>
              <span v-else :class="{ broken: p.broken }">{{ p.text }}</span>
            </template>
          </h3>
          <p v-if="selected.locationCardId && findCard(selected.locationCardId)" class="cd-place">📍 <a class="ch-ref" :href="cardHref(selected.locationCardId)" @click.prevent="openCard(selected.locationCardId!)">{{ cardTitle(selected.locationCardId) }}</a></p>
          <!-- eslint-disable-next-line vue/no-v-html -->
          <div v-if="selected.description" class="ch-desc cd-desc" @click="onDescClick" v-html="renderDesc(selected.description)"></div>
          <div v-if="selected.images.length" class="cd-imgs">
            <a v-for="url in selected.images" :key="url" :href="url" target="_blank" rel="noopener"><img :src="withThumbnail(url, 400)" alt="" loading="lazy" /></a>
          </div>

          <div v-if="detailLinks.length" class="cd-section">
            <h4>相关条目 <small>点了打开完整页面</small></h4>
            <ul class="cd-cards">
              <li v-for="c in detailLinks" :key="c.id">
                <a class="cd-card" :href="cardHref(c.id)" @click.prevent="openCard(c.id)">
                  <img v-if="c.cover" :src="c.cover" alt="" loading="lazy" />
                  <span v-else class="cd-card-icon" aria-hidden="true">{{ c.icon }}</span>
                  <span class="cd-card-text"><b>{{ c.title }}</b><small>{{ c.type }}</small></span>
                </a>
                <button type="button" class="cd-mini" :title="`只看和「${c.title}」有关的事`" :aria-label="`只看和 ${c.title} 有关的事`" @click="cardFilter = c.id">⌕</button>
                <button v-if="canPane" type="button" class="cd-mini" title="在旁边分屏打开" :aria-label="`分屏打开 ${c.title}`" @click="emit('pane', c.id)">⧉</button>
              </li>
            </ul>
          </div>
          <div v-if="selected.tags.length" class="cd-section">
            <button v-for="t in selected.tags" :key="t" type="button" class="ch-tag" title="只看这个标签" @click="tagFilter = t">#{{ t }}</button>
          </div>
          <div v-if="branchesHere.length || mergesHere.length || forkOfLine" class="cd-section">
            <h4>分叉与汇合</h4>
            <p v-if="forkOfLine" class="cd-branch">这条线从 <button type="button" class="ch-link" @click="select(forkOfLine.id)">{{ plain(forkOfLine.title) }}</button> 分出来</p>
            <p v-for="l in branchesHere" :key="l.id" class="cd-branch">⑂ 从这里分出：<button type="button" class="ch-link" @click="onLineClick(l.id)">{{ l.name }}</button> <small>{{ STATUS_META[l.status].label }}</small></p>
            <p v-for="l in mergesHere" :key="l.id" class="cd-branch">⤵ 在这里汇合：<button type="button" class="ch-link" @click="onLineClick(l.id)">{{ l.name }}</button></p>
          </div>
          <p v-if="selected.sourceCardId" class="cd-note">这是条目「<a class="ch-ref" :href="cardHref(selected.sourceCardId)" @click.prevent="openCard(selected.sourceCardId!)">{{ cardTitle(selected.sourceCardId) }}</a>」里的时间线事件，要改请到那个条目里改。</p>
          <p v-if="selected.hidden" class="cd-note">🔒 这件事对访客隐藏。</p>
        </div>
        <footer v-if="editable && !selected.sourceCardId" class="cd-foot">
          <button type="button" class="ch-btn" @click="openEvent(selected)">编辑</button>
          <button v-if="selected.kind !== 'skip'" type="button" class="ch-btn" title="从这件事开始分出一条支线" @click="branchFrom(selected.id)">⑂ 分出支线</button>
          <button type="button" class="ch-btn" title="在同一条线上、同一时间再加一件" @click="openNewEvent({ timelineId: selected.timelineId, eraId: selected.endYear != null ? selected.endEraId : selected.eraId, year: selected.endYear ?? selected.year })">＋ 接着写</button>
        </footer>
      </aside>
    </div>

    <ChronicleEventEditor v-if="eventEditor" :key="eventEditor.key" :project-id="projectId" :calendar="calendar" :lines="editableLanes" :event="eventEditor.event" :preset="eventEditor.preset"
      @close="eventEditor = null" @saved="onEventSaved" @deleted="onEventDeleted" @branch="id => { eventEditor = null; branchFrom(id) }" />
    <ChronicleLineEditor v-if="lineEditor" :key="lineEditor.key" :project-id="projectId" :calendar="calendar" :lines="editableLanes" :events="realEvents" :line="lineEditor.line" :preset="lineEditor.preset"
      @close="lineEditor = null" @saved="onLineSaved" @deleted="onLineDeleted" @move="moveLine" />
    <ChronicleCalendarEditor v-if="calendarOpen" :project-id="projectId" :calendar="calendar" :events="realEvents" @close="calendarOpen = false" @saved="c => { calendar = c; calendarOpen = false; axisRef?.fit() }" />
  </div>
</template>

<script setup lang="ts">
import { computed, onScopeDispose, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { chronicleApi } from '@/api/chronicle'
import { worldApi, type TimelineItem } from '@/api/worldApi'
import { useWorldStore } from '@/stores/world'
import { withThumbnail } from '@/utils/imageThumb'
import { timelineValue } from '@/utils/worldLayout'
import { renderCardDescription, getCardTypeLabel, parseCoverImage } from '@/utils/worldPresentation'
import {
  STATUS_META, KIND_META, LINE_COLORS, normalizeCalendar, defaultCalendar, defaultEra, orderLanes, eventStart, eventEnd, eraYearOf, eraOf,
  formatEventDate, formatSpan, titleParts, plainTitle, refIds,
  type Calendar, type ChronicleEvent, type ChronicleTimeline,
} from '@/utils/chronicle'
import ChronicleAxis from './ChronicleAxis.vue'
import ChronicleEventEditor from './ChronicleEventEditor.vue'
import ChronicleLineEditor from './ChronicleLineEditor.vue'
import ChronicleCalendarEditor from './ChronicleCalendarEditor.vue'

const props = defineProps<{ projectId: string; canEdit?: boolean; canPane?: boolean }>()
const emit = defineEmits<{ select: [cardId: string]; pane: [cardId: string] }>()
const store = useWorldStore()
const route = useRoute()
const router = useRouter()

// ===== 数据 =====
const calendar = ref<Calendar>(defaultCalendar())
const timelines = ref<ChronicleTimeline[]>([])
const events = ref<ChronicleEvent[]>([])
const legacy = ref<TimelineItem[]>([])
const serverCanEdit = ref(false)
const loading = ref(true)
const loadError = ref(false)
const busy = ref(false)
const editable = computed(() => serverCanEdit.value || Boolean(props.canEdit))
let version = 0

async function load() {
  const v = ++version
  loading.value = timelines.value.length === 0
  loadError.value = false
  const [data, old] = await Promise.allSettled([chronicleApi.get(props.projectId), worldApi.getTimeline(props.projectId)])
  if (v !== version) return
  if (data.status === 'fulfilled') {
    calendar.value = normalizeCalendar(data.value.calendar)
    timelines.value = data.value.timelines
    events.value = data.value.events
    serverCanEdit.value = data.value.canEdit
  } else loadError.value = true
  legacy.value = old.status === 'fulfilled' ? old.value : []
  loading.value = false
}
watch(() => props.projectId, () => { timelines.value = []; events.value = []; void load() }, { immediate: true })
onScopeDispose(() => { version++ })

const calendarIsDefault = computed(() => JSON.stringify(calendar.value.eras.map(e => e.name)) === JSON.stringify(['公元', '公元前']))
const findCard = (id: string) => store.getCardById(id, props.projectId)
const cardTitle = (id: string) => findCard(id)?.title ?? '已删除的条目'
const plain = (title: string) => plainTitle(title, findCard)
const parts = (title: string) => titleParts(title, findCard)
const cardHref = (id: string) => router.resolve({ name: 'CardDetail', params: { projectId: props.projectId, cardId: id } }).href
const renderDesc = (text: string) => renderCardDescription(text.replace(/!\{CARD:/g, '{CARD:'), findCard, 'document')

// 条目里原来写的时间线事件：单独一条只读的线
const LEGACY_ID = '__cards'
const legacyLine = computed<ChronicleTimeline | null>(() => legacy.value.length ? { id: LEGACY_ID, name: '条目里的时间线事件', description: '各个条目自己写的时间线事件（按文字里的年份估算位置），在条目里修改', status: 'canon', sortOrder: 1e9, virtual: true } : null)
const legacyEvents = computed<ChronicleEvent[]>(() => {
  const era = defaultEra(calendar.value)
  return legacy.value.map((it, i) => {
    const v = timelineValue(it.date)
    return {
      id: `card:${it.cardId}:${i}`, timelineId: LEGACY_ID, kind: 'event', title: it.title || '（无标题）', description: it.description,
      eraId: era.id, year: v === null ? null : Math.floor(v) - era.start + 1, dateText: [it.era, it.date].filter(Boolean).join(' ') || null,
      images: it.image ? [it.image] : [], tags: [], cardIds: [...new Set([it.cardId, ...(it.cardIds ?? [])])], sortOrder: i, sourceCardId: it.cardId,
    }
  })
})
const realEvents = computed(() => events.value)
const allEvents = computed(() => [...events.value, ...legacyEvents.value])
const lines = computed(() => timelines.value)

// ===== 显示哪些线（记在本机） =====
const hiddenKey = () => `taichu.chronicle.hidden:${props.projectId}`
const hiddenLines = ref<Set<string>>(new Set())
watch(() => props.projectId, () => {
  try { hiddenLines.value = new Set(JSON.parse(localStorage.getItem(hiddenKey()) || '[]')) } catch { hiddenLines.value = new Set() }
}, { immediate: true })
function saveHidden() { try { localStorage.setItem(hiddenKey(), JSON.stringify([...hiddenLines.value])) } catch { /* 存不了就算了 */ } }
function toggleLine(id: string) {
  const s = new Set(hiddenLines.value)
  if (s.has(id)) s.delete(id); else s.add(id)
  hiddenLines.value = s; saveHidden()
}
const allLanes = computed(() => [...orderLanes(timelines.value), ...(legacyLine.value ? [{ line: legacyLine.value, depth: 0 }] : [])])
const editableLanes = computed(() => orderLanes(timelines.value))
const laneIndex = computed(() => new Map(allLanes.value.map((l, i) => [l.line.id, i])))
const colorOf = (l: ChronicleTimeline) => l.color || LINE_COLORS[(laneIndex.value.get(l.id) ?? 0) % LINE_COLORS.length]!
const lineById = computed(() => new Map(allLanes.value.map(l => [l.line.id, l.line])))
const colorOfId = (id: string) => { const l = lineById.value.get(id); return l ? colorOf(l) : 'var(--text-secondary)' }
const lineName = (id: string) => lineById.value.get(id)?.name ?? '已删除的时间线'

const eventsByLine = computed(() => {
  const m = new Map<string, ChronicleEvent[]>()
  for (const e of allEvents.value) { if (!m.has(e.timelineId)) m.set(e.timelineId, []); m.get(e.timelineId)!.push(e) }
  return m
})
const visibleLanes = computed(() => allLanes.value.filter(l => !hiddenLines.value.has(l.line.id)))
const axisLanes = computed(() => visibleLanes.value.map(l => ({ ...l, events: eventsByLine.value.get(l.line.id) ?? [] })))
const startOf = (e: ChronicleEvent) => eventStart(calendar.value, e)
const undated = computed(() => visibleLanes.value.flatMap(l => (eventsByLine.value.get(l.line.id) ?? []).filter(e => startOf(e) === null)))

// ===== 筛选 =====
const query = ref('')
const tagFilter = ref('')
const cardFilter = ref(String(route.query.card || ''))
function mentions(e: ChronicleEvent, id: string) {
  return e.cardIds.includes(id) || e.locationCardId === id || e.sourceCardId === id || refIds(e.title).includes(id) || refIds(e.description).includes(id)
}
const matched = computed<Set<string> | null>(() => {
  const q = query.value.trim().toLowerCase()
  if (!q && !tagFilter.value && !cardFilter.value) return null
  const out = new Set<string>()
  for (const e of allEvents.value) {
    if (tagFilter.value && !e.tags.includes(tagFilter.value)) continue
    if (cardFilter.value && !mentions(e, cardFilter.value)) continue
    if (q) {
      const hay = [plain(e.title), plain(e.description ?? '').replace(/\{CARD:[^}]+\}/g, ''), e.tags.join(' '), e.dateText ?? '', lineName(e.timelineId),
        ...e.cardIds.map(cardTitle), ...refIds(e.description).map(cardTitle)].join('\n').toLowerCase()
      if (!hay.includes(q)) continue
    }
    out.add(e.id)
  }
  return out
})

// ===== 看法 =====
const mode = ref<'axis' | 'list'>('axis')
try { if (localStorage.getItem('taichu.chronicle.mode') === 'list') mode.value = 'list' } catch { /* ignore */ }
if (typeof window !== 'undefined' && window.innerWidth < 640) mode.value = 'list'
function setMode(m: 'axis' | 'list') { mode.value = m; try { localStorage.setItem('taichu.chronicle.mode', m) } catch { /* ignore */ } }
const axisRef = ref<InstanceType<typeof ChronicleAxis> | null>(null)
const linesMenu = ref<HTMLDetailsElement | null>(null)

// 编年史：按纪元分组，组里按时间
const listGroups = computed(() => {
  const cal = calendar.value
  const rows = visibleLanes.value.flatMap(l => eventsByLine.value.get(l.line.id) ?? [])
    .filter(e => !matched.value || matched.value.has(e.id))
    .map(e => ({ ev: e, t: startOf(e) }))
    .sort((a, b) => (a.t ?? Infinity) - (b.t ?? Infinity) || a.ev.sortOrder - b.ev.sortOrder)
  const groups: { key: string; title: string; color?: string; rows: { ev: ChronicleEvent; when: string; links: { id: string; title: string; icon: string }[] }[] }[] = []
  for (const { ev, t } of rows) {
    const era = t === null ? null : ev.sourceCardId ? null : eraYearOf(cal, Math.floor(t)).era
    const key = t === null ? 'undated' : era ? era.id : 'cards'
    let g = groups[groups.length - 1]
    if (!g || g.key !== key) {
      g = { key, title: t === null ? '时间未定' : era ? era.name : '条目里的事件', color: era?.color, rows: [] }
      groups.push(g)
    }
    const links = [...new Set([...ev.cardIds, ...(ev.locationCardId ? [ev.locationCardId] : [])])]
      .filter(id => findCard(id)).map(id => ({ id, title: cardTitle(id), icon: id === ev.locationCardId ? '📍' : '▤' }))
    g.rows.push({ ev, when: whenShort(ev), links })
  }
  return groups
})
function whenShort(e: ChronicleEvent) {
  if (e.sourceCardId || e.year === null || e.year === undefined) return e.dateText || '—'
  const s = formatEventDate(calendar.value, e)
  return s.replace(eraOf(calendar.value, e.eraId).name + ' ', '')
}
const spanOf = (e: ChronicleEvent) => {
  if (e.kind === 'event') return ''
  const a = startOf(e), b = eventEnd(calendar.value, e)
  return a !== null && b !== null && b > a ? `历时约 ${formatSpan(calendar.value, b - a)}` : ''
}

// ===== 选中（记在地址里，打开条目再返回还在） =====
const selectedId = ref<string | null>(String(route.query.event || '') || null)
const selected = computed(() => allEvents.value.find(e => e.id === selectedId.value) ?? null)
function select(id: string | null) {
  selectedId.value = id
  if (route.query.view === 'timeline' && (route.query.event || null) !== id) void router.replace({ query: { ...route.query, event: id || undefined } }).catch(() => {})
}
watch(() => route.query.event, v => { if (v && v !== selectedId.value) selectedId.value = String(v) })
function onEntryClick(e: MouseEvent, id: string) {
  const refEl = (e.target as HTMLElement).closest('.inline-ref') as HTMLElement | null
  if (refEl) { if (refEl.dataset.cardId) openCard(refEl.dataset.cardId); return }
  if ((e.target as HTMLElement).closest('a, button')) return
  select(id)
}
function onDescClick(e: MouseEvent) {
  const refEl = (e.target as HTMLElement).closest('.inline-ref') as HTMLElement | null
  if (refEl?.dataset.cardId) openCard(refEl.dataset.cardId)
}
/** 打开条目的完整页面（主窗口）；返回键能回到编年史的这件事 */
function openCard(id: string) {
  if (!findCard(id)) { ElMessage.info('这个条目已经删除了'); return }
  emit('select', id)
}

const neighbors = computed(() => {
  const ev = selected.value
  if (!ev) return { prev: null, next: null }
  const list = (eventsByLine.value.get(ev.timelineId) ?? []).filter(e => startOf(e) !== null)
    .sort((a, b) => startOf(a)! - startOf(b)! || a.sortOrder - b.sortOrder)
  const i = list.findIndex(e => e.id === ev.id)
  return { prev: i > 0 ? list[i - 1]!.id : null, next: i >= 0 && i < list.length - 1 ? list[i + 1]!.id : null }
})
const detailLinks = computed(() => {
  const ev = selected.value
  if (!ev) return []
  const ids = [...new Set([...ev.cardIds, ...refIds(ev.title), ...refIds(ev.description)])]
  return ids.map(id => findCard(id)).filter((c): c is NonNullable<typeof c> => Boolean(c)).map(c => ({
    id: c.id, title: c.title, type: getCardTypeLabel(c.type), icon: '▤', cover: c.coverImage ? withThumbnail(parseCoverImage(c.coverImage), 96) : '',
  }))
})
const branchesHere = computed(() => selected.value ? timelines.value.filter(l => l.forkEventId === selected.value!.id) : [])
const mergesHere = computed(() => selected.value ? timelines.value.filter(l => l.mergeEventId === selected.value!.id) : [])
const forkOfLine = computed(() => {
  const ev = selected.value
  const line = ev ? lineById.value.get(ev.timelineId) : undefined
  if (!line?.forkEventId || neighbors.value.prev) return null
  return events.value.find(e => e.id === line.forkEventId) ?? null
})

// ===== 编辑 =====
let editorKey = 0
const eventEditor = ref<{ key: number; event: ChronicleEvent | null; preset?: Partial<ChronicleEvent> } | null>(null)
const lineEditor = ref<{ key: number; line: ChronicleTimeline | null; preset?: Partial<ChronicleTimeline> } | null>(null)
const calendarOpen = ref(false)
function openEvent(ev: ChronicleEvent) { eventEditor.value = { key: ++editorKey, event: ev } }
function openNewEvent(preset: Partial<ChronicleEvent> = {}) {
  if (!timelines.value.length) { ElMessage.info('先建一条时间线'); return }
  const visible = editableLanes.value.find(l => !hiddenLines.value.has(l.line.id))
  eventEditor.value = { key: ++editorKey, event: null, preset: { timelineId: visible?.line.id ?? timelines.value[0]!.id, ...preset } }
}
function openLine(line: ChronicleTimeline | null, preset?: Partial<ChronicleTimeline>) {
  linesMenu.value?.removeAttribute('open')
  lineEditor.value = { key: ++editorKey, line, preset }
}
function onLineClick(id: string) {
  const line = timelines.value.find(l => l.id === id)
  if (line && editable.value) openLine(line)
  else if (line?.cardId) openCard(line.cardId)
  else if (id === LEGACY_ID) ElMessage.info('这些是各个条目里自己写的时间线事件，点事件可以打开对应的条目')
}
/** 双击时间轴空白处：在那个时间加事件 */
function addAt(lineId: string, t: number) {
  const cal = calendar.value
  const { era, year } = eraYearOf(cal, Math.floor(t))
  let month: number | null = null
  if (cal.useMonths) {
    let day = (t - Math.floor(t)) * cal.months.reduce((s, m) => s + m.days, 0)
    for (let i = 0; i < cal.months.length; i++) { if (day < cal.months[i]!.days) { month = i + 1; break } day -= cal.months[i]!.days }
  }
  openNewEvent({ timelineId: lineId, eraId: era.id, year, month: Math.abs(t - Math.round(t)) < 0.02 ? null : month })
}
function branchFrom(eventId: string) {
  const ev = events.value.find(e => e.id === eventId)
  if (!ev) return
  openLine(null, { parentId: ev.timelineId, forkEventId: ev.id, status: 'whatif', name: '' })
}
async function quickStart() {
  busy.value = true
  try {
    const line = await chronicleApi.createTimeline(props.projectId, { name: '正史', status: 'canon', color: LINE_COLORS[0] })
    timelines.value = [...timelines.value, line]
    openNewEvent({ timelineId: line.id })
  } catch { /* 请求工具已经提示过 */ } finally { busy.value = false }
}

function onEventSaved(ev: ChronicleEvent) {
  const i = events.value.findIndex(e => e.id === ev.id)
  events.value = i >= 0 ? events.value.map(e => (e.id === ev.id ? ev : e)) : [...events.value, ev]
  eventEditor.value = null
  if (hiddenLines.value.has(ev.timelineId)) toggleLine(ev.timelineId)
  select(ev.id)
  ElMessage.success(i >= 0 ? '已保存' : '已添加')
}
function onEventDeleted(id: string) {
  events.value = events.value.filter(e => e.id !== id)
  timelines.value = timelines.value.map(l => ({ ...l, forkEventId: l.forkEventId === id ? null : l.forkEventId, mergeEventId: l.mergeEventId === id ? null : l.mergeEventId }))
  eventEditor.value = null
  if (selectedId.value === id) select(null)
}
function onLineSaved(line: ChronicleTimeline) {
  const isNew = !timelines.value.some(l => l.id === line.id)
  timelines.value = isNew ? [...timelines.value, line] : timelines.value.map(l => (l.id === line.id ? line : l))
  lineEditor.value = null
  // 新分出的支线：接着在分叉的时间加第一件事
  if (isNew && line.forkEventId) {
    const fork = events.value.find(e => e.id === line.forkEventId)
    openNewEvent({ timelineId: line.id, eraId: fork?.endYear != null ? fork.endEraId : fork?.eraId, year: fork?.endYear ?? fork?.year ?? null })
  }
}
function onLineDeleted(id: string) {
  const gone = new Set(events.value.filter(e => e.timelineId === id).map(e => e.id))
  events.value = events.value.filter(e => e.timelineId !== id)
  timelines.value = timelines.value.filter(l => l.id !== id).map(l => ({
    ...l,
    parentId: l.parentId === id ? null : l.parentId,
    forkEventId: l.parentId === id || (l.forkEventId && gone.has(l.forkEventId)) ? null : l.forkEventId,
    mergeEventId: l.mergeEventId && gone.has(l.mergeEventId) ? null : l.mergeEventId,
  }))
  lineEditor.value = null
  if (selectedId.value && gone.has(selectedId.value)) select(null)
}
/** 上下移动：只在同一个父线下面的兄弟之间换位置 */
async function moveLine(id: string, dir: -1 | 1) {
  const all = editableLanes.value.map(l => l.line)
  const me = all.find(l => l.id === id)
  if (!me) return
  const siblings = all.filter(l => (l.parentId || null) === (me.parentId || null))
  const i = siblings.indexOf(me), j = i + dir
  if (j < 0 || j >= siblings.length) return
  const order = timelines.value.slice().sort((a, b) => a.sortOrder - b.sortOrder).map(l => l.id)
  const a = order.indexOf(me.id), b = order.indexOf(siblings[j]!.id)
  ;[order[a], order[b]] = [order[b]!, order[a]!]
  const rank = new Map(order.map((x, k) => [x, k + 1]))
  timelines.value = timelines.value.map(l => ({ ...l, sortOrder: rank.get(l.id) ?? l.sortOrder }))
  if (lineEditor.value?.line) lineEditor.value.line = timelines.value.find(l => l.id === id) ?? lineEditor.value.line
  try { await chronicleApi.reorderTimelines(props.projectId, order) } catch { void load() }
}

</script>

<style scoped>
.chron { display: flex; flex-direction: column; min-height: 100%; height: 100%; box-sizing: border-box; padding: 20px clamp(12px, 2.5vw, 28px) 16px; }
.ch-head { display: flex; flex-wrap: wrap; align-items: flex-end; gap: 10px 16px; margin-bottom: 12px; }
.ch-title { flex: 1 1 260px; min-width: 0; }
.ch-title h2 { margin: 0 0 4px; font-size: 20px; font-weight: 600; }
.ch-title p { margin: 0; font-size: 13px; color: var(--text-secondary); }
.ch-tools { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; }
.ch-seg { display: flex; }
.ch-seg button { padding: 6px 12px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.ch-seg button + button { border-left: 0; }
.ch-seg button[aria-selected=true] { background: var(--accent-color); border-color: var(--accent-color); color: #fff; }
.ch-search { width: 160px; padding: 6px 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; }
.ch-btn { padding: 6px 12px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; white-space: nowrap; }
.ch-btn:hover { border-color: var(--accent-color); color: var(--accent-color); }
.ch-btn.primary { background: var(--accent-color); border-color: var(--accent-color); color: #fff; }
.ch-btn:disabled { opacity: .5; cursor: default; }
.ch-link { padding: 0; border: 0; background: none; color: var(--accent-color); font: inherit; cursor: pointer; text-decoration: underline; text-underline-offset: 2px; }
.ch-menu { position: relative; }
.ch-menu summary { list-style: none; padding: 6px 12px; border: 1px solid var(--border-line); font-size: 13px; cursor: pointer; user-select: none; }
.ch-menu summary::-webkit-details-marker { display: none; }
.ch-menu summary::after { content: ' ▾'; font-size: 10px; }
.ch-menu-pop { position: absolute; right: 0; top: calc(100% + 4px); z-index: 20; min-width: 240px; max-height: 360px; overflow-y: auto; padding: 6px 0; background: var(--paper-card); border: 1px solid var(--border-line); box-shadow: 0 10px 28px rgba(0, 0, 0, .16); }
.ch-menu-item { display: flex; align-items: center; gap: 6px; padding: 5px 10px; font-size: 13px; cursor: pointer; }
.ch-menu-item:hover { background: var(--bg-sub); }
.ch-menu-item small { font-size: 11px; color: var(--text-secondary); }
.ch-menu-pop .ch-link { margin: 6px 10px 2px; font-size: 12px; }
.ch-sw { display: inline-block; flex-shrink: 0; width: 9px; height: 9px; background: var(--lc, currentColor); }
.ch-filters { flex-basis: 100%; display: flex; flex-wrap: wrap; align-items: center; gap: 6px; font-size: 13px; color: var(--text-secondary); }
.ch-filter { padding: 3px 10px; border: 1px solid var(--accent-color); background: color-mix(in srgb, var(--accent-color) 10%, transparent); color: var(--accent-color); font: inherit; font-size: 12px; cursor: pointer; }
.ch-empty { padding: 40px 0; text-align: center; font-size: 14px; color: var(--text-secondary); }
.ch-start p { max-width: 520px; margin: 0 auto 14px; line-height: 1.7; }
.ch-start .ch-btn { margin: 0 4px; }

.ch-body { flex: 1; min-height: 0; display: flex; gap: 14px; }
.ch-main { flex: 1 1 0; min-width: 0; min-height: 0; display: flex; flex-direction: column; }
.ch-undated { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; margin-top: 10px; }
.ch-undated-title { font-size: 12px; font-weight: 600; color: var(--text-secondary); }
.ch-undated-item { max-width: 280px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; padding: 3px 8px; border: 1px solid var(--border-line); border-left: 3px solid var(--lc); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.ch-undated-item.sel { background: var(--accent-color); color: #fff; }
.ch-undated-item.dim { opacity: .4; }

/* 编年史（列表） */
.ch-list { flex: 1; overflow-y: auto; overscroll-behavior: contain; padding-right: 4px; }
.ch-era + .ch-era { margin-top: 18px; }
.ch-era-title { position: sticky; top: 0; z-index: 2; margin: 0 0 8px; padding: 8px 12px; font-size: 15px; background: var(--bg-sub); border-left: 4px solid var(--accent-color); }
.ch-entries { list-style: none; margin: 0; padding: 0; }
.ch-entry { display: grid; grid-template-columns: 128px minmax(0, 1fr); gap: 14px; }
.ch-when { padding-top: 12px; font-size: 13px; font-weight: 600; color: var(--accent-color); text-align: right; font-variant-numeric: tabular-nums; overflow-wrap: anywhere; }
.ch-what { position: relative; padding: 10px 12px 14px 16px; border-left: 3px solid var(--lc); cursor: pointer; min-width: 0; }
.ch-what:hover { background: color-mix(in srgb, var(--bg-sub) 60%, transparent); }
.ch-entry.sel .ch-what { background: color-mix(in srgb, var(--accent-color) 8%, transparent); }
.ch-entry.k-period .ch-what { border-left-width: 6px; }
.ch-entry.k-lost .ch-what { border-left-style: dashed; background: repeating-linear-gradient(135deg, transparent 0 8px, color-mix(in srgb, var(--text-secondary) 7%, transparent) 8px 9px); }
.ch-entry.k-lost .ch-ev-title { font-style: italic; color: var(--text-secondary); }
.ch-entry.k-skip .ch-what { border-left-style: dotted; }
.ch-entry.k-skip .ch-ev-title::before { content: '⋯ '; }
.ch-line-tag { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; font-size: 12px; color: var(--text-secondary); }
.ch-kind { padding: 0 5px; border: 1px solid var(--border-line); font-size: 11px; }
.ch-ev-title { margin: 4px 0 2px; font-size: 15px; font-weight: 600; line-height: 1.5; }
.ch-ref { color: var(--accent-color); text-decoration: underline; text-decoration-thickness: 1px; text-underline-offset: 3px; cursor: pointer; }
.ch-ref:hover { text-decoration-thickness: 2px; }
.broken { color: var(--text-secondary); text-decoration: line-through; }
.ch-desc { margin-top: 4px; font-size: 14px; line-height: 1.75; white-space: pre-wrap; overflow-wrap: anywhere; color: var(--text-primary); }
.ch-desc :deep(.inline-ref) { color: var(--accent-color); text-decoration: underline; text-underline-offset: 3px; cursor: pointer; }
.ch-desc :deep(.inline-ref.broken) { color: var(--text-secondary); cursor: default; }
.ch-list .ch-desc { display: -webkit-box; -webkit-line-clamp: 4; line-clamp: 4; -webkit-box-orient: vertical; overflow: hidden; }
.ch-thumbs { display: flex; gap: 6px; margin-top: 8px; }
.ch-thumbs img { width: 72px; height: 72px; object-fit: cover; display: block; }
.ch-more { align-self: center; font-size: 12px; color: var(--text-secondary); }
.ch-chips { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 8px; }
.ch-card { padding: 2px 8px; background: var(--bg-sub); color: var(--text-primary); font-size: 12px; text-decoration: none; }
.ch-card:hover { color: var(--accent-color); }
.ch-tag { padding: 2px 6px; border: 0; background: none; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.ch-tag:hover { color: var(--accent-color); }

/* 详情 */
.ch-detail { flex: 0 0 340px; min-height: 0; display: flex; flex-direction: column; background: var(--paper-card); border: 1px solid var(--border-line); border-top: 3px solid var(--lc); }
.cd-head { display: flex; align-items: center; gap: 6px; padding: 8px 10px; border-bottom: 1px solid var(--border-line); font-size: 12px; }
.cd-kind { color: var(--text-secondary); }
.cd-line { display: inline-flex; align-items: center; gap: 5px; max-width: 150px; overflow: hidden; padding: 0; border: 0; background: none; color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; white-space: nowrap; }
.ch-sp { flex: 1; }
.cd-nav { min-width: 26px; height: 26px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); font: inherit; font-size: 14px; cursor: pointer; }
.cd-nav:disabled { opacity: .35; cursor: default; }
.cd-body { flex: 1; overflow-y: auto; overscroll-behavior: contain; padding: 14px 16px; }
.cd-date { margin: 0; font-size: 13px; font-weight: 600; color: var(--accent-color); }
.cd-date small { font-weight: 400; color: var(--text-secondary); }
.cd-title { margin: 6px 0 8px; font-size: 18px; line-height: 1.5; }
.cd-place { margin: 0 0 8px; font-size: 13px; }
.cd-imgs { display: grid; grid-template-columns: repeat(auto-fill, minmax(96px, 1fr)); gap: 6px; margin-top: 12px; }
.cd-imgs img { width: 100%; aspect-ratio: 1; object-fit: cover; display: block; }
.cd-section { margin-top: 16px; }
.cd-section h4 { margin: 0 0 8px; font-size: 13px; }
.cd-section h4 small { font-weight: 400; color: var(--text-secondary); }
.cd-cards { list-style: none; margin: 0; padding: 0; display: grid; gap: 6px; }
.cd-cards li { display: flex; align-items: stretch; gap: 4px; }
.cd-card { flex: 1; min-width: 0; display: flex; align-items: center; gap: 8px; padding: 6px; background: var(--bg-sub); color: var(--text-primary); text-decoration: none; }
.cd-card:hover { outline: 1px solid var(--accent-color); }
.cd-card img, .cd-card-icon { flex-shrink: 0; width: 36px; height: 36px; object-fit: cover; display: flex; align-items: center; justify-content: center; background: var(--paper-card); color: var(--text-secondary); }
.cd-card-text { min-width: 0; display: flex; flex-direction: column; }
.cd-card-text b { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 13px; }
.cd-card-text small { font-size: 11px; color: var(--text-secondary); }
.cd-card::after { content: '→'; margin-left: auto; padding-right: 4px; color: var(--text-secondary); }
.cd-mini { width: 30px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); cursor: pointer; }
.cd-mini:hover { color: var(--accent-color); border-color: var(--accent-color); }
.cd-branch { margin: 0 0 4px; font-size: 13px; }
.cd-branch small { color: var(--text-secondary); }
.cd-note { margin: 14px 0 0; padding: 8px 10px; background: var(--bg-sub); font-size: 12px; color: var(--text-secondary); }
.cd-foot { display: flex; flex-wrap: wrap; gap: 6px; padding: 10px; border-top: 1px solid var(--border-line); }

@media (max-width: 900px) {
  .ch-body { flex-direction: column; }
  .ch-detail { flex: none; max-height: 55vh; }
  .chron.has-detail .ch-main { max-height: 50vh; }
}
@media (max-width: 640px) {
  .chron { padding: 14px 10px; height: auto; }
  .ch-entry { grid-template-columns: 1fr; gap: 0; }
  .ch-when { padding: 8px 0 0 19px; text-align: left; }
  .ch-search { width: 100%; }
}
</style>
