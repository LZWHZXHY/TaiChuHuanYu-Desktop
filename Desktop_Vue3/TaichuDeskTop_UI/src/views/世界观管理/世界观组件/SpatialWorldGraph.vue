<!-- SpatialWorldGraph.vue：世界观 3D「粒子星图」。每个条目是一团按类型塑形的发光粒子，关系是流动的粒子弧线；
     阵型（世界体系、星系、星座、层级树、编年长河……）切换时粒子飞散重组；还能找两个条目之间的关系路径、把聚焦条目的封面变成粒子画像。
     渲染在 particleWorld.ts，阵型在 particleLayouts.ts。 -->
<template>
  <div class="spatial" :class="{ failed }">
    <div ref="container" class="scene" />
    <div class="pw-vignette" aria-hidden="true"></div>
    <i class="pw-corner tl" aria-hidden="true"></i><i class="pw-corner tr" aria-hidden="true"></i><i class="pw-corner bl" aria-hidden="true"></i><i class="pw-corner br" aria-hidden="true"></i>
    <template v-if="!failed">
      <div class="pw-top">
        <div class="pw-seg" role="radiogroup" aria-label="星图结构">
          <button v-for="f in shownFormations" :key="f.id" type="button" role="radio" :aria-checked="formation === f.id" :class="{ on: formation === f.id && !focused && !path }" :disabled="focused || !!path" :title="focused ? '正在聚焦一个条目，返回全部后可以切换结构' : f.desc" @click="setFormation(f.id)">
            <span aria-hidden="true">{{ f.icon }}</span>{{ f.label }}
          </button>
        </div>
        <div class="pw-tools">
          <button type="button" class="pw-btn" :class="{ on: !!pathMode || !!path }" :aria-pressed="!!pathMode || !!path" title="找出两个条目之间是怎么一步步联系起来的" @click="togglePath">⇢ 关系路径</button>
          <button v-if="focused && portraitAvailable" type="button" class="pw-btn" :class="{ on: portrait }" :aria-pressed="portrait" title="把聚焦条目的封面图变成粒子画像" @click="togglePortrait">▣ 粒子画像</button>
          <button type="button" class="pw-btn" :class="{ on: rotating }" :aria-pressed="rotating" title="缓慢自动旋转" @click="toggleRotate">⟳ {{ rotating ? '旋转中' : '自动旋转' }}</button>
        </div>
      </div>
      <p v-if="!focused && !path && !pathMode" class="pw-desc">{{ currentDesc }}</p>

      <!-- 关系路径 -->
      <div v-if="pathMode || path || pathMsg" class="pw-path" role="status">
        <template v-if="pathMode === 'a'">选择<b>起点</b>：点一个星团或名字</template>
        <template v-else-if="pathMode === 'b'">起点「{{ titleOf(pathA) }}」· 现在选择<b>终点</b></template>
        <template v-else-if="path">
          <ol class="pw-chain">
            <template v-for="(id, i) in path" :key="id">
              <li><button type="button" :style="{ borderColor: colorOf(id) }" @click="openFromPath(id)">{{ titleOf(id) }}</button></li>
              <li v-if="i < path.length - 1" class="pw-rel" aria-hidden="true">{{ relationBetween(id, path[i + 1]!) }}</li>
            </template>
          </ol>
          <small>{{ path.length - 1 }} 步</small>
        </template>
        <span v-if="pathMsg" class="pw-path-msg">{{ pathMsg }}</span>
        <span class="pw-path-btns">
          <button v-if="path" type="button" @click="restartPath">重新选择</button>
          <button type="button" @click="exitPath">退出</button>
        </span>
      </div>

      <!-- 时间：配合时间线，看世界一步步展开 -->
      <div v-if="showTimeBar" class="pw-time" :class="{ on: timeIdx < timeSteps.length }">
        <button type="button" class="pw-play" :aria-label="playing ? '暂停' : '按时间播放'" :title="playing ? '暂停' : '从最早的时刻开始，一步步看世界展开'" @click="togglePlay">{{ playing ? '❚❚' : '▶' }}</button>
        <div class="pw-time-track">
          <input v-model.number="timeIdx" type="range" min="0" :max="timeSteps.length" step="1" aria-label="时间" :style="{ '--p': `${(timeIdx / Math.max(1, timeSteps.length)) * 100}%` }" @input="stopPlay" />
          <div class="pw-ticks" aria-hidden="true"><i v-for="(s, i) in timeSteps" :key="i" :class="{ era: s.eraStart, past: i <= timeIdx }" :style="{ left: `${(i / Math.max(1, timeSteps.length)) * 100}%` }"></i></div>
        </div>
        <div class="pw-time-label">
          <template v-if="timeIdx < timeSteps.length">
            <small v-if="timeSteps[timeIdx]!.era">{{ timeSteps[timeIdx]!.era }}</small>
            <b>{{ timeSteps[timeIdx]!.date }}</b>
            <em>{{ timeSteps[timeIdx]!.titles }}</em>
          </template>
          <b v-else>全部时间</b>
        </div>
      </div>

      <ul v-if="legend.length && !pathMode && !path" class="pw-legend" :class="{ lifted: showTimeBar }" aria-label="条目类型">
        <li v-for="l in legend" :key="l.type"><i :style="{ background: l.color, boxShadow: `0 0 8px ${l.color}` }"></i>{{ l.label }}<small>{{ l.count }}</small></li>
      </ul>
      <p v-if="!showTimeBar" class="pw-hint">拖动旋转 · 滚轮缩放 · 右键平移 · 点击星团聚焦</p>
    </template>
    <p v-else class="pw-failed" role="alert">你的浏览器或显卡不支持 3D 星图（WebGL）。可以切回「平面」视图查看关系。</p>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import type { WorldGraphNode, WorldGraphEdge } from '@/utils/worldGraph'
import { getCardTypeLabel } from '@/utils/worldPresentation'
import { worldApi } from '@/api/worldApi'
import { ParticleWorld, type Formation } from './particleWorld'
import { NN_LAYERS, type NeuralLayer } from './particleLayouts'

const props = defineProps<{
  data: { nodes: WorldGraphNode[]; links: WorldGraphEdge[] }
  focusId?: string
  /** 世界名：第一次打开时粒子先聚成这几个字 */
  worldTitle?: string
  /** 编年长河要读时间线 */
  projectId?: string
  /** 条目封面（聚焦时变成粒子画像） */
  covers?: Record<string, string>
  // ===== 给别的页面（知识库）复用时的配置 =====
  /** 只提供这些结构（默认全部） */
  formations?: Formation[]
  /** 记住所选结构用的键 */
  storageKey?: string
  typeLabel?: (type: string) => string
  /** 世界体系里当「恒星」的类型 */
  containers?: string[]
  neuralLayers?: NeuralLayer[]
  shapes?: Record<string, string>
  /** 名字标签开关（默认开） */
  labels?: boolean
  /** 开场聚成的文字（默认用世界名） */
  introText?: string
  /** 右侧被面板挡住的宽度（像素） */
  insetRight?: number
  layoutData?: unknown
  positions?: unknown
}>()
const emit = defineEmits<{ select: [id: string]; 'update:positions': [positions: Record<string, { x: number; y: number }>] }>()

const FORMATIONS: { id: Formation; label: string; icon: string; desc: string }[] = [
  { id: 'system', label: '世界体系', icon: '☉', desc: '世界体系：派系、组织、地点是恒星，和它们有关系的角色、物品……在轨道上环绕；小势力绕着大势力转，没有归属的条目在最外圈的游离星带。' },
  { id: 'neural', label: '脉络', icon: '⋮⋮', desc: '' },
  { id: 'galaxy', label: '星系', icon: '✺', desc: '星系：几条旋臂，关系越多的条目越靠近核心，同类型的条目在旋臂上挨在一起。' },
  { id: 'constellation', label: '星座', icon: '✧', desc: '星座：每种类型聚成一个星座，看得出这个世界里哪类设定多、哪类少。' },
  { id: 'tree', label: '层级树', icon: '⋔', desc: '层级树：从关系最多的条目开始一层层往下展开，像族谱或组织架构；没连起来的条目各自成树。' },
  { id: 'timeline', label: '编年长河', icon: '≈', desc: '编年长河：有时间线事件的条目按时间顺序排在长河上，按纪元分段；还没写时间的条目悬在上方。' },
  { id: 'network', label: '关系网', icon: '⋰', desc: '关系网：有关系的条目互相吸引，抱成一团一团，看得出谁和谁是一伙。' },
  { id: 'sphere', label: '星球', icon: '◍', desc: '星球：条目按类型分区铺在一颗球面上，一眼看全。' },
  { id: 'helix', label: '螺旋', icon: '⌇', desc: '螺旋：按类型排成一条双螺旋长卷。' },
]
const KEY = props.storageKey || 'taichu.world.formation'
const shownFormations = computed(() => props.formations ? FORMATIONS.filter(f => props.formations!.includes(f.id)) : FORMATIONS)
const saved = (() => { try { return localStorage.getItem(KEY) as Formation | null } catch { return null } })()
const formation = ref<Formation>(shownFormations.value.some(f => f.id === saved) ? saved! : shownFormations.value[0]?.id ?? 'system')
const typeName = (t: string) => (props.typeLabel ?? getCardTypeLabel)(t)
const currentDesc = computed(() => {
  if (formation.value === 'neural') {
    const layers = (props.neuralLayers ?? NN_LAYERS).filter(l => l.types.length && props.data.nodes.some(n => l.types.includes(n.type)))
    return `脉络：${layers.map(l => l.label).join(' → ')}，一层层排开，光脉冲顺着层层往下流动，看得出从源头一路延伸到最后一层。`
  }
  if (formation.value === 'system' && props.containers) {
    return `世界体系：${props.containers.map(typeName).join('、')}是恒星，和它们有关系的条目在轨道上环绕；没有归属的在最外圈的游离星带。`
  }
  return FORMATIONS.find(f => f.id === formation.value)?.desc ?? ''
})
const reduced = typeof window !== 'undefined' && !!window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
const rotating = ref(!reduced)
const portrait = ref(true)
const portraitAvailable = ref(false)
const failed = ref(false)
const container = ref<HTMLElement | null>(null)
let world: ParticleWorld | null = null
// 跟着网站的黑白主题
const isLight = () => typeof document !== 'undefined' && document.documentElement.dataset.theme !== 'dark'
let themeObs: MutationObserver | null = null

const focused = computed(() => !!props.focusId && props.data.nodes.some(n => n.id === props.focusId))
const legend = computed(() => {
  const m = new Map<string, { type: string; color: string; count: number }>()
  for (const n of props.data.nodes) {
    const e = m.get(n.type)
    if (e) e.count++; else m.set(n.type, { type: n.type, color: n.color, count: 1 })
  }
  return [...m.values()].sort((a, b) => b.count - a.count).map(x => ({ ...x, label: typeName(x.type) }))
})
const nodeById = computed(() => new Map(props.data.nodes.map(n => [n.id, n])))
const titleOf = (id: string) => nodeById.value.get(id)?.title ?? '…'
const colorOf = (id: string) => nodeById.value.get(id)?.color ?? '#94a3b8'

const toPW = () => ({
  nodes: props.data.nodes.map(n => ({ id: n.id, title: n.title, type: n.type, color: n.color, cover: props.covers?.[n.id] || undefined })),
  links: props.data.links.map(l => ({ source: l.source, target: l.target, label: l.label, color: l.color, isEmbed: l.isEmbed })),
})

// ===== 结构 =====
let timelineLoaded = ''
const timelineItems = ref<{ cardId: string; date: string; era?: string | null; title: string }[]>([])
async function loadTimeline() {
  const id = props.projectId
  if (!id || timelineLoaded === id) return
  timelineLoaded = id
  try {
    const items = await worldApi.getTimeline(id)
    timelineItems.value = items
    world?.setTimeline(items)
  } catch { timelineLoaded = '' }
}

function setFormation(f: Formation) {
  formation.value = f
  try { localStorage.setItem(KEY, f) } catch { /* 隐私模式 */ }
  world?.setFormation(f)
  if (f === 'timeline') void loadTimeline()
}
function toggleRotate() {
  rotating.value = !rotating.value
  world?.setAutoRotate(rotating.value)
}
function togglePortrait() {
  portrait.value = !portrait.value
  world?.setPortrait(portrait.value)
}
function resetCamera(scope: 'all' | 'selection' = 'all') { world?.resetCamera(path.value ? 'path' : scope) }

// ===== 关系路径 =====
const pathMode = ref<'' | 'a' | 'b'>('')
const pathA = ref('')
const path = ref<string[] | null>(null)
const pathMsg = ref('')
function togglePath() {
  if (pathMode.value || path.value) return exitPath()
  if (focused.value) emit('select', '')
  pathMode.value = 'a'
  pathMsg.value = ''
}
function restartPath() {
  path.value = null
  world?.showPath(null)
  pathMode.value = 'a'
  pathMsg.value = ''
}
function exitPath() {
  pathMode.value = ''
  pathA.value = ''
  pathMsg.value = ''
  if (path.value) { path.value = null; world?.showPath(null) }
  world?.highlight('')
}
/** 无向最短路径（广度优先） */
function shortestPath(a: string, b: string): string[] | null {
  const adj = new Map<string, string[]>()
  for (const l of props.data.links) {
    ;(adj.get(l.source) ?? adj.set(l.source, []).get(l.source)!).push(l.target)
    ;(adj.get(l.target) ?? adj.set(l.target, []).get(l.target)!).push(l.source)
  }
  const prev = new Map<string, string>([[a, '']])
  const queue = [a]
  while (queue.length) {
    const id = queue.shift()!
    if (id === b) break
    for (const k of adj.get(id) ?? []) if (!prev.has(k)) { prev.set(k, id); queue.push(k) }
  }
  if (!prev.has(b)) return null
  const out: string[] = []
  for (let cur = b; cur; cur = prev.get(cur)!) out.unshift(cur)
  return out
}
function relationBetween(a: string, b: string) {
  const l = props.data.links.find(x => x.source === a && x.target === b) ?? props.data.links.find(x => x.source === b && x.target === a)
  if (!l) return '—'
  return l.source === a ? `${l.label} →` : `← ${l.label}`
}
function onPick(id: string) {
  if (!pathMode.value) { emit('select', id); return }
  if (!id) return
  if (pathMode.value === 'a') { pathA.value = id; pathMode.value = 'b'; world?.highlight(id); return }
  if (id === pathA.value) return
  const p = shortestPath(pathA.value, id)
  pathMode.value = ''
  world?.highlight('')
  if (!p) { pathMsg.value = `「${titleOf(pathA.value)}」和「${titleOf(id)}」之间没有关系连线`; pathMode.value = 'a'; return }
  path.value = p
  world?.showPath(p)
}
function openFromPath(id: string) { exitPath(); emit('select', id) }

// ===== 时间轴：按时间线的时刻排序，拖动 / 播放时只点亮到这一刻为止出现过的条目 =====
const timeSteps = computed(() => {
  const ids = new Set(props.data.nodes.map(n => n.id))
  const items = timelineItems.value.filter(it => ids.has(it.cardId) && it.date)
    .sort((a, b) => a.date.localeCompare(b.date, 'zh-CN', { numeric: true }))
  const steps: { date: string; era: string; eraStart: boolean; titles: string; ids: string[] }[] = []
  for (const it of items) {
    const era = (it.era ?? '').trim()
    const last = steps[steps.length - 1]
    if (last && last.date === it.date && last.era === era) { if (!last.ids.includes(it.cardId)) last.ids.push(it.cardId); continue }
    steps.push({ date: it.date, era, eraStart: !last || last.era !== era, titles: '', ids: [it.cardId] })
  }
  for (const s of steps) {
    const names = s.ids.map(titleOf)
    s.titles = names.slice(0, 3).join('、') + (names.length > 3 ? ` 等 ${names.length} 个` : '')
  }
  return steps
})
/** 条目 → 它第一次出现在第几个时刻 */
const timeOrder = computed(() => {
  const m = new Map<string, number>()
  timeSteps.value.forEach((s, i) => { for (const id of s.ids) if (!m.has(id)) m.set(id, i) })
  return m
})
const timeIdx = ref(0)
const showTimeBar = computed(() => timeSteps.value.length >= 2 && !focused.value && !pathMode.value && !path.value)
watch(timeSteps, (s, old) => { if (!old || timeIdx.value >= (old?.length ?? 0)) timeIdx.value = s.length })
watch([timeIdx, timeOrder, showTimeBar], () => {
  const on = showTimeBar.value && timeIdx.value < timeSteps.value.length
  world?.setTimeCursor(on ? timeOrder.value : null, on ? timeIdx.value : null)
})
const playing = ref(false)
let playTimer = 0
function stopPlay() { playing.value = false; window.clearInterval(playTimer) }
function togglePlay() {
  if (playing.value) return stopPlay()
  playing.value = true
  if (timeIdx.value >= timeSteps.value.length - 1) timeIdx.value = 0
  playTimer = window.setInterval(() => {
    if (timeIdx.value < timeSteps.value.length - 1) timeIdx.value++
    else { stopPlay(); window.setTimeout(() => { if (!playing.value) timeIdx.value = timeSteps.value.length }, 2200) }
  }, reduced ? 2600 : 1700)
}

let refreshTimer = 0
watch(() => [props.data, props.focusId, props.covers] as const, () => {
  window.clearTimeout(refreshTimer)
  refreshTimer = window.setTimeout(() => {
    if (!world) return
    // 数据变了（筛选、搜索）：路径作废
    if (path.value && path.value.some(id => !nodeById.value.has(id))) exitPath()
    const d = toPW()
    world.setData(d.nodes, d.links, props.focusId ?? '')
  }, 80)
})
watch(() => props.labels, v => world?.setLabels(v !== false))
watch(() => props.insetRight, v => { world?.setInsetRight(v ?? 0); window.setTimeout(() => world?.resetCamera(), 30) })
watch(() => props.projectId, () => { timelineLoaded = ''; timelineItems.value = []; void loadTimeline() })

onMounted(() => {
  if (!container.value) return
  try {
    world = new ParticleWorld(container.value, {
      onSelect: onPick,
      onPortrait: ok => { portraitAvailable.value = ok },
      reducedMotion: reduced,
      introText: props.introText ?? props.worldTitle,
      typeLabel: typeName,
      containers: props.containers,
      neuralLayers: props.neuralLayers,
      shapes: props.shapes,
    })
    world.setLabels(props.labels !== false)
    if (props.insetRight) world.setInsetRight(props.insetRight)
    world.setTheme(isLight())
    themeObs = new MutationObserver(() => world?.setTheme(isLight()))
    themeObs.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] })
    world.setFormation(formation.value)
    world.setAutoRotate(rotating.value)
    const d = toPW()
    world.start(d.nodes, d.links, props.focusId ?? '')
    // 时间轴和编年长河都要用时间线
    void loadTimeline()
  } catch (e) {
    console.warn('3D 星图初始化失败', e)
    failed.value = true
    world = null
  }
})
onBeforeUnmount(() => {
  window.clearTimeout(refreshTimer)
  stopPlay()
  themeObs?.disconnect()
  world?.dispose()
  world = null
})
defineExpose({ resetCamera })
</script>

<style scoped>
/* 莱茵生命风格：纸色网格（亮色）/ 深色网格（暗色），跟着网站主题走；粒子画在透明画布上 */
.spatial {
  --pw-grid: color-mix(in srgb, var(--text-primary) 6%, transparent);
  --pw-panel: color-mix(in srgb, var(--paper-card) 84%, transparent);
  --pw-line: color-mix(in srgb, var(--text-primary) 16%, transparent);
  --pw-accent: var(--accent-color, #0e9f8e);
  position: absolute; inset: 0; overflow: hidden; color: var(--text-primary);
  background-color: var(--bg-main);
  background-image:
    radial-gradient(ellipse 60% 55% at 50% 46%, color-mix(in srgb, var(--pw-accent) 9%, transparent), transparent 72%),
    linear-gradient(var(--pw-grid) 1px, transparent 1px), linear-gradient(90deg, var(--pw-grid) 1px, transparent 1px),
    linear-gradient(color-mix(in srgb, var(--text-primary) 3%, transparent) 1px, transparent 1px), linear-gradient(90deg, color-mix(in srgb, var(--text-primary) 3%, transparent) 1px, transparent 1px);
  background-size: 100% 100%, 96px 96px, 96px 96px, 24px 24px, 24px 24px;
  background-position: center;
}
.scene { position: absolute; inset: 0; }
.pw-vignette { position: absolute; inset: 0; pointer-events: none; background: radial-gradient(ellipse 75% 70% at 50% 50%, transparent 55%, color-mix(in srgb, var(--bg-main) 70%, transparent) 100%); }
.pw-corner { position: absolute; width: 18px; height: 18px; border: 0 solid var(--pw-accent); opacity: .65; pointer-events: none; }
.pw-corner.tl { top: 6px; left: 6px; border-top-width: 2px; border-left-width: 2px; }
.pw-corner.tr { top: 6px; right: 6px; border-top-width: 2px; border-right-width: 2px; }
.pw-corner.bl { bottom: 6px; left: 6px; border-bottom-width: 2px; border-left-width: 2px; }
.pw-corner.br { bottom: 6px; right: 6px; border-bottom-width: 2px; border-right-width: 2px; }
.scene :deep(.pw-canvas) { display: block; width: 100%; height: 100%; touch-action: none; }
.scene :deep(.pw-labels) { position: absolute; inset: 0; pointer-events: none; overflow: hidden; }
.scene :deep(.pw-label) { position: absolute; left: 0; top: 0; display: flex; align-items: baseline; gap: 6px; height: 26px; padding: 0 9px; overflow: hidden; border: 1px solid var(--pw-line); border-left: 3px solid; background: var(--pw-panel); color: var(--text-primary); font: 600 12px/24px var(--font-sans, sans-serif); white-space: nowrap; text-overflow: ellipsis; pointer-events: auto; cursor: pointer; backdrop-filter: blur(4px); transition: background .15s; }
.scene :deep(.pw-label small) { color: var(--text-secondary); font-size: 10.5px; font-weight: 500; }
.scene :deep(.pw-label:hover), .scene :deep(.pw-label.on) { background: var(--paper-card); box-shadow: 0 2px 10px rgb(0 0 0 / 14%); }
.scene :deep(.pw-group) { position: absolute; left: 0; top: 0; display: grid; justify-items: center; gap: 1px; padding-bottom: 6px; color: color-mix(in srgb, var(--g) 68%, var(--text-primary)); text-align: center; white-space: nowrap; pointer-events: none; text-shadow: 0 0 10px var(--bg-main), 0 0 3px var(--bg-main); }
.scene :deep(.pw-group b) { font: 700 15px/1.2 var(--font-sans, sans-serif); letter-spacing: .14em; filter: saturate(1.2); }
.scene :deep(.pw-group small) { font-size: 11px; color: var(--text-secondary); letter-spacing: .02em; }
.scene :deep(.pw-group)::after { content: ''; width: 1px; height: 14px; margin-top: 3px; background: linear-gradient(var(--g), transparent); opacity: .7; }
.scene :deep(.pw-group.below) { padding: 6px 0 0; }
.scene :deep(.pw-group.below)::after { order: -1; margin: 0 0 3px; background: linear-gradient(transparent, var(--g)); }
.scene :deep(.pw-edge) { position: absolute; left: 0; top: 0; padding: 1px 7px; border: 1px solid var(--e); background: var(--pw-panel); color: var(--text-primary); font-size: 11px; white-space: nowrap; pointer-events: none; }
.pw-top { position: absolute; top: 12px; left: 14px; right: 14px; display: flex; flex-wrap: wrap; gap: 8px; align-items: center; justify-content: space-between; pointer-events: none; }
.pw-seg, .pw-tools { display: flex; flex-wrap: wrap; gap: 0; pointer-events: auto; }
.pw-tools { gap: 6px; }
.pw-seg { border: 1px solid var(--pw-line); background: var(--pw-panel); backdrop-filter: blur(6px); }
.pw-seg button, .pw-btn { display: inline-flex; align-items: center; gap: 5px; padding: 6px 10px; border: 0; background: transparent; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; white-space: nowrap; }
.pw-seg button + button { border-left: 1px solid var(--pw-line); }
.pw-seg button span { font-size: 13px; }
.pw-seg button.on { background: color-mix(in srgb, var(--pw-accent) 14%, transparent); color: var(--pw-accent); box-shadow: inset 0 -2px 0 var(--pw-accent); font-weight: 600; }
.pw-seg button:disabled { opacity: .4; cursor: default; }
.pw-btn { border: 1px solid var(--pw-line); background: var(--pw-panel); backdrop-filter: blur(6px); }
.pw-btn.on { color: var(--pw-accent); border-color: color-mix(in srgb, var(--pw-accent) 55%, transparent); }
.pw-seg button:hover:not(:disabled), .pw-btn:hover { color: var(--text-primary); }
.pw-seg button:focus-visible, .pw-btn:focus-visible, .pw-path button:focus-visible { outline: 2px solid var(--pw-accent); outline-offset: 1px; }
.pw-desc { position: absolute; top: 54px; left: 14px; max-width: min(580px, calc(100% - 28px)); margin: 0; padding: 4px 10px; border-left: 2px solid var(--pw-accent); background: color-mix(in srgb, var(--bg-main) 55%, transparent); color: var(--text-secondary); font-size: 12px; line-height: 1.6; pointer-events: none; }
.pw-path { position: absolute; left: 50%; bottom: 46px; transform: translateX(-50%); display: flex; flex-wrap: wrap; align-items: center; justify-content: center; gap: 8px 12px; max-width: calc(100% - 24px); padding: 8px 12px; border: 1px solid color-mix(in srgb, var(--pw-accent) 45%, transparent); background: var(--pw-panel); backdrop-filter: blur(8px); color: var(--text-primary); font-size: 13px; box-shadow: 0 6px 20px rgb(0 0 0 / 12%); }
.pw-path b { color: var(--pw-accent); margin: 0 2px; }
.pw-chain { display: flex; flex-wrap: wrap; align-items: center; gap: 4px; margin: 0; padding: 0; list-style: none; }
.pw-chain button { padding: 3px 9px; border: 1px solid; border-left-width: 3px; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.pw-rel { color: var(--text-secondary); font-size: 11px; }
.pw-path small { color: var(--text-secondary); }
.pw-path-msg { color: var(--warning, #d97706); font-size: 12px; }
.pw-path-btns { display: inline-flex; gap: 6px; }
.pw-path-btns button { padding: 4px 10px; border: 1px solid var(--pw-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.pw-legend { position: absolute; left: 14px; bottom: 12px; display: flex; flex-wrap: wrap; gap: 4px 12px; max-width: min(520px, calc(100% - 28px)); margin: 0; padding: 6px 10px; list-style: none; border: 1px solid var(--pw-line); background: var(--pw-panel); font-size: 11px; color: var(--text-secondary); pointer-events: none; }
.pw-legend.lifted { bottom: 66px; }
.pw-time { position: absolute; left: 50%; bottom: 12px; transform: translateX(-50%); display: grid; grid-template-columns: auto minmax(0, 1fr) minmax(150px, 220px); align-items: center; gap: 12px; width: min(760px, calc(100% - 28px)); padding: 8px 12px; border: 1px solid var(--pw-line); background: var(--pw-panel); backdrop-filter: blur(8px); }
.pw-time.on { border-color: color-mix(in srgb, var(--pw-accent) 50%, transparent); box-shadow: 0 4px 18px color-mix(in srgb, var(--pw-accent) 12%, transparent); }
.pw-play { width: 30px; height: 30px; border: 1px solid var(--pw-accent); background: transparent; color: var(--pw-accent); font-size: 11px; cursor: pointer; }
.pw-play:hover { background: color-mix(in srgb, var(--pw-accent) 14%, transparent); }
.pw-time-track { position: relative; height: 26px; }
.pw-time-track input { position: absolute; inset: 0; width: 100%; height: 100%; margin: 0; appearance: none; background: transparent; cursor: pointer; z-index: 1; }
.pw-time-track input::-webkit-slider-runnable-track { height: 2px; background: linear-gradient(90deg, var(--pw-accent) var(--p), var(--pw-line) var(--p)); }
.pw-time-track input::-moz-range-track { height: 2px; background: linear-gradient(90deg, var(--pw-accent) var(--p), var(--pw-line) var(--p)); }
.pw-time-track input::-webkit-slider-thumb { appearance: none; width: 12px; height: 12px; margin-top: -5px; border: 2px solid var(--pw-accent); background: var(--paper-card); transform: rotate(45deg); }
.pw-time-track input::-moz-range-thumb { width: 10px; height: 10px; border: 2px solid var(--pw-accent); border-radius: 0; background: var(--paper-card); transform: rotate(45deg); }
.pw-ticks { position: absolute; left: 0; right: 0; top: 50%; pointer-events: none; }
.pw-ticks i { position: absolute; top: -3px; width: 1px; height: 6px; background: var(--text-secondary); opacity: .45; }
.pw-ticks i.era { top: -7px; height: 14px; width: 2px; opacity: .8; }
.pw-ticks i.past { background: var(--pw-accent); opacity: 1; }
.pw-time-label { display: grid; min-width: 0; line-height: 1.3; }
.pw-time-label small { color: var(--pw-accent); font-size: 10px; letter-spacing: .12em; }
.pw-time-label b { font: 600 13px var(--font-mono, monospace); color: var(--text-primary); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.pw-time-label em { font-style: normal; font-size: 11px; color: var(--text-secondary); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.pw-legend li { display: inline-flex; align-items: center; gap: 5px; }
.pw-legend i { width: 7px; height: 7px; border-radius: 50%; }
.pw-legend small { color: var(--text-secondary); opacity: .7; font-family: var(--font-mono, monospace); }
.pw-hint { position: absolute; right: 14px; bottom: 12px; margin: 0; font: 11px var(--font-mono, monospace); letter-spacing: .04em; color: var(--text-secondary); pointer-events: none; }
.pw-failed { position: absolute; inset: 0; display: grid; place-items: center; margin: 0; padding: 24px; text-align: center; color: var(--text-secondary); font-size: 13px; }
@media (max-width: 760px) {
  .pw-hint, .pw-desc { display: none; }
  .pw-legend { bottom: 8px; left: 8px; }
  .pw-legend.lifted { display: none; }
  .pw-time { grid-template-columns: auto minmax(0, 1fr); bottom: 8px; }
  .pw-time-label { grid-column: 1 / -1; }
  .pw-seg button { padding: 6px 7px; }
  .pw-seg button span { display: none; }
  .pw-path { bottom: 8px; }
}
</style>
