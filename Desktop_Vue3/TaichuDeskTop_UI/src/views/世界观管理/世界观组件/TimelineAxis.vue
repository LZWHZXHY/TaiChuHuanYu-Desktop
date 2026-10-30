<!-- TimelineAxis.vue：条目里的时间线区块。横向时间轴 + 刻度，事件是轴上的点，点开是一张事件卡片
     （配图、标题、日期、描述、标签、相关条目）。editable 时卡片可以直接改，也能添加 / 删除 / 对访客隐藏事件。 -->
<template>
  <div class="tl-axis" :class="{ editable }">
    <div ref="scroller" class="tl-scroll">
      <div class="tl-inner" :style="{ width: innerWidth + 'px' }">
        <!-- 事件卡片 -->
        <div v-if="active" class="tl-card" :class="{ hidden: active.ev.hidden }" :style="{ marginLeft: cardLeft + 'px', ...evColor(active.ev) }" role="dialog" :aria-label="`事件：${active.ev.title || '未命名事件'}`">
          <div class="tl-card-top">
            <div class="tl-img" :class="{ empty: !active.ev.image }">
              <img v-if="active.ev.image" :src="active.ev.image" :alt="active.ev.title" @click="editable ? pickImage() : undefined" />
              <button v-else-if="editable" type="button" class="tl-img-add" aria-label="添加配图" :disabled="uploading" @click="pickImage">{{ uploading ? '…' : '＋' }}</button>
              <button v-if="editable && active.ev.image" type="button" class="tl-img-x" aria-label="移除配图" @click="patch({ image: null })">×</button>
            </div>
            <div class="tl-head">
              <template v-if="editable">
                <input class="tl-title-in" :value="active.ev.title" placeholder="事件名称" aria-label="事件名称" @input="patch({ title: val($event) })" />
                <div class="tl-date-in">
                  <span aria-hidden="true">◷</span>
                  <input :value="active.ev.date" placeholder="时间，如 1725年5月29日" aria-label="时间" @input="patch({ date: val($event) })" />
                  <input :value="active.ev.era ?? ''" class="tl-era-in" placeholder="纪元（可选）" aria-label="纪元" @input="patch({ era: val($event) || null })" />
                </div>
                <p v-if="active.ev.date && active.value == null" class="tl-warn">这个时间里没有数字，放不到时间轴上（会列在下面）</p>
              </template>
              <template v-else>
                <h4 class="tl-title">{{ active.ev.title || '未命名事件' }}</h4>
                <p class="tl-date"><span aria-hidden="true">◷</span> {{ [active.ev.era, active.ev.date].filter(Boolean).join(' · ') || '时间未定' }}</p>
              </template>
            </div>
            <div class="tl-tools">
              <button v-if="editable" type="button" :aria-pressed="!!active.ev.hidden" :title="active.ev.hidden ? '对访客隐藏中，点击公开' : '对访客隐藏'" @click="patch({ hidden: !active.ev.hidden })">{{ active.ev.hidden ? '◌' : '◉' }}</button>
              <button v-if="editable" type="button" class="danger" title="删除事件" @click="removeActive">🗑</button>
              <button type="button" title="关闭" aria-label="关闭事件卡片" @click="activeKey = null">×</button>
            </div>
          </div>
          <span v-if="active.ev.hidden" class="tl-private">仅创建者和协作者可见</span>
          <!-- 颜色：区分战争、灾难、建国这类事件 -->
          <div v-if="editable" class="tl-colors" role="radiogroup" aria-label="事件颜色">
            <span>颜色</span>
            <button type="button" role="radio" class="none" :aria-checked="!active.ev.color" title="默认" aria-label="默认颜色" @click="patch({ color: null })"></button>
            <button v-for="c in COLORS" :key="c" type="button" role="radio" :aria-checked="active.ev.color === c" :style="{ background: c }" :aria-label="`颜色 ${c}`" @click="patch({ color: c })"></button>
          </div>
          <textarea v-if="editable" class="tl-desc-in" :value="active.ev.description ?? ''" rows="3" placeholder="描述" aria-label="描述" @input="patch({ description: val($event) || null })"></textarea>
          <p v-else-if="active.ev.description" class="tl-desc">{{ active.ev.description }}</p>
          <!-- 标签 -->
          <div v-if="editable || active.ev.tags?.length" class="tl-tags">
            <span v-for="tag in active.ev.tags ?? []" :key="tag" class="tl-tag">#{{ tag }}<button v-if="editable" type="button" :aria-label="`删除标签 ${tag}`" @click="patch({ tags: (active.ev.tags ?? []).filter(t => t !== tag) })">×</button></span>
            <input v-if="editable" v-model="tagDraft" class="tl-tag-in" placeholder="＋ 标签，回车添加" aria-label="添加标签" @keydown.enter.prevent="addTag" />
          </div>
          <!-- 相关条目 -->
          <div v-if="editable || linked.length" class="tl-cards">
            <button v-for="c in linked" :key="c.id" type="button" class="tl-card-chip" :title="editable ? c.title : `打开《${c.title}》`" @click="!editable && $emit('select', c.id)">
              <img v-if="c.cover" :src="c.cover" alt="" /><span>{{ c.title }}</span>
              <i v-if="editable" role="button" :aria-label="`移除 ${c.title}`" @click.stop="patch({ cardIds: (active.ev.cardIds ?? []).filter(id => id !== c.id) })">×</i>
            </button>
          </div>
          <div v-if="editable" class="tl-add-cards">
            <button type="button" class="tl-dashed" @click="pickerOpen = !pickerOpen">＋ 添加相关条目</button>
            <CardPicker v-if="pickerOpen" class="tl-picker" :project-id="projectId" :exclude="active.ev.cardIds ?? []"
              @close="pickerOpen = false" @confirm="ids => { patch({ cardIds: [...(active!.ev.cardIds ?? []), ...ids] }); pickerOpen = false }" />
          </div>
        </div>

        <!-- 事件标签（错开高度，避免重叠） -->
        <div class="tl-lanes" :style="{ height: laneHeight + 'px' }">
          <button v-for="m in markers" :key="m.key" type="button" class="tl-pin" :class="{ on: m.key === activeKey, hidden: m.ev.hidden }"
            :style="{ left: m.x + 'px', bottom: m.level * 34 + 'px', ...evColor(m.ev) }" :title="[m.ev.era, m.ev.date].filter(Boolean).join(' · ')" @click="toggle(m.key)">
            <span class="tl-pin-label">{{ m.ev.title || '未命名事件' }}</span>
            <span class="tl-pin-stem" :style="{ height: m.level * 34 + 10 + 'px' }"></span>
          </button>
        </div>
        <!-- 轴 -->
        <div class="tl-line">
          <span v-for="t in ticks.minor" :key="'m' + t" class="tl-tick minor" :style="{ left: pos(t) + 'px' }"></span>
          <span v-for="t in ticks.major" :key="'M' + t" class="tl-tick major" :style="{ left: pos(t) + 'px' }"><em>{{ tickLabel(t) }}</em></span>
          <span v-for="m in markers" :key="'d' + m.key" class="tl-dot" :class="{ on: m.key === activeKey, hidden: m.ev.hidden }" :style="{ left: m.x + 'px', ...evColor(m.ev) }"></span>
        </div>
      </div>
    </div>

    <!-- 放不到轴上的事件 -->
    <div v-if="unplaced.length" class="tl-unplaced">
      <span>时间未定：</span>
      <button v-for="u in unplaced" :key="u.key" type="button" :class="{ on: u.key === activeKey, hidden: u.ev.hidden }" :style="evColor(u.ev)" @click="toggle(u.key)">{{ u.ev.title || '未命名事件' }}<small v-if="u.ev.date"> · {{ u.ev.date }}</small></button>
    </div>
    <p v-if="!events.length && !editable" class="tl-empty">还没有事件</p>
    <div v-if="editable" class="tl-actions">
      <button type="button" class="tl-dashed" @click="addEvent">＋ 添加事件</button>
      <span class="tl-hint">时间写成「1725年5月29日」「公元前300年」「100」都行，有数字就能排到轴上</span>
    </div>
    <input ref="fileInput" type="file" accept="image/*" hidden @change="onImage" />
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import type { TimelineEvent } from '@/api/types/world'
import { useWorldStore } from '@/stores/world'
import { parseCoverImage } from '@/utils/worldPresentation'
import { withThumbnail } from '@/utils/imageThumb'
import { MARK_COLORS, newId, niceTicks, timelineValue } from '@/utils/worldLayout'
import { useCardUpload } from '@/composables/世界观管理/useCardUpload'
import CardPicker from './CardPicker.vue'

const props = defineProps<{ events: TimelineEvent[]; projectId: string; editable?: boolean }>()
const emit = defineEmits<{ 'update:events': [events: TimelineEvent[]]; select: [cardId: string] }>()
const store = useWorldStore()
const { uploadCover } = useCardUpload()

// 事件没有 ID 的（老数据）用下标当 key
const keyOf = (ev: TimelineEvent, i: number) => ev.id || `i${i}`
const COLORS = MARK_COLORS
const evColor = (ev: TimelineEvent) => ({ '--ev': ev.color || 'var(--accent-color)' })
const items = computed(() => props.events.map((ev, i) => ({ ev, i, key: keyOf(ev, i), value: timelineValue(ev.date) })))
const placed = computed(() => items.value.filter(x => x.value != null).sort((a, b) => a.value! - b.value!))
const unplaced = computed(() => items.value.filter(x => x.value == null))

const activeKey = ref<string | null>(null)
const active = computed(() => items.value.find(x => x.key === activeKey.value) ?? null)
const pickerOpen = ref(false)
const tagDraft = ref('')
watch(activeKey, () => { pickerOpen.value = false; tagDraft.value = '' })
function toggle(key: string) { activeKey.value = activeKey.value === key ? null : key }

// ---------- 尺寸和刻度 ----------
const scroller = ref<HTMLElement | null>(null)
const viewWidth = ref(640)
let observer: ResizeObserver | null = null
onMounted(() => {
  if (!scroller.value) return
  viewWidth.value = scroller.value.clientWidth || 640
  if (typeof ResizeObserver !== 'undefined') {
    observer = new ResizeObserver(([entry]) => { viewWidth.value = Math.round(entry.contentRect.width) || viewWidth.value })
    observer.observe(scroller.value)
  }
})
onBeforeUnmount(() => observer?.disconnect())
const PAD = 48
// 事件多就加宽，可以左右滚动
const innerWidth = computed(() => Math.max(viewWidth.value, placed.value.length * 96 + PAD * 2))
const domain = computed(() => {
  const vals = placed.value.map(x => x.value!)
  if (!vals.length) return niceTicks(0, 100, 5)
  let min = Math.min(...vals), max = Math.max(...vals)
  const span = max - min || Math.max(1, Math.abs(min) * 0.1)
  min -= span * 0.08; max += span * 0.08
  return niceTicks(min, max, Math.max(3, Math.floor(innerWidth.value / 130)))
})
const pos = (v: number) => {
  const { start, end } = domain.value
  return PAD + ((v - start) / (end - start || 1)) * (innerWidth.value - PAD * 2)
}
const ticks = computed(() => {
  const { ticks: major, step } = domain.value
  const minor: number[] = []
  for (let i = 0; i < major.length - 1; i++) for (let k = 1; k < 5; k++) minor.push(Number((major[i] + (step * k) / 5).toFixed(6)))
  return { major, minor }
})
function tickLabel(v: number) {
  const step = domain.value.step
  const text = step >= 1 ? String(Math.round(v)) : v.toFixed(step >= 0.1 ? 1 : 2)
  return v < 0 ? `前${text.replace('-', '')}` : text
}

// 标签错开：和左边已放的标签重叠就往上一层
const markers = computed(() => {
  const levels: number[] = []
  return placed.value.map(x => {
    const xPos = pos(x.value!)
    const width = Math.min(150, (x.ev.title || '未命名事件').length * 13 + 26)
    let level = 0
    while (levels[level] != null && levels[level] > xPos - width / 2 - 6) level++
    levels[level] = xPos + width / 2
    return { ...x, x: xPos, level: Math.min(level, 3) }
  })
})
const laneHeight = computed(() => (Math.max(0, ...markers.value.map(m => m.level)) + 1) * 34 + 14)
const CARD_W = 440
const cardLeft = computed(() => {
  const m = markers.value.find(x => x.key === activeKey.value)
  const x = m ? m.x - CARD_W / 2 : PAD
  return Math.max(0, Math.min(x, innerWidth.value - Math.min(CARD_W, innerWidth.value)))
})

const linked = computed(() => (active.value?.ev.cardIds ?? []).map(id => {
  const card = store.getCardById(id, props.projectId)
  return { id, title: card?.title ?? '已删除的条目', cover: card?.coverImage ? withThumbnail(parseCoverImage(card.coverImage), 64) : '' }
}))

// ---------- 编辑 ----------
const val = (e: Event) => (e.target as HTMLInputElement).value
function patch(changes: Partial<TimelineEvent>) {
  const a = active.value
  if (!a || !props.editable) return
  const next = props.events.slice()
  next[a.i] = { ...a.ev, ...changes }
  // 老数据第一次被改时补上 ID，之后卡片不会因为排序变化而跳走
  if (!next[a.i].id) { next[a.i].id = newId('e'); activeKey.value = next[a.i].id! }
  emit('update:events', next)
}
function addTag() {
  const tag = tagDraft.value.trim().replace(/^#/, '')
  tagDraft.value = ''
  if (!tag || (active.value?.ev.tags ?? []).includes(tag)) return
  patch({ tags: [...(active.value?.ev.tags ?? []), tag].slice(0, 20) })
}
async function addEvent() {
  const ev: TimelineEvent = { id: newId('e'), date: '', era: null, title: '新事件', description: null, image: null, tags: [], cardIds: [] }
  emit('update:events', [...props.events, ev])
  activeKey.value = ev.id!
  await nextTick()
  scroller.value?.parentElement?.querySelector<HTMLInputElement>('.tl-title-in')?.select()
}
function removeActive() {
  const a = active.value
  if (!a) return
  activeKey.value = null
  emit('update:events', props.events.filter((_, i) => i !== a.i))
}
const fileInput = ref<HTMLInputElement | null>(null)
const uploading = ref(false)
function pickImage() { fileInput.value?.click() }
async function onImage(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  uploading.value = true
  try { const url = await uploadCover(file); if (url) patch({ image: url }) } finally { uploading.value = false }
}
defineExpose({ addEvent })
</script>

<style scoped>
.tl-axis { position: relative; }
.tl-scroll { overflow-x: auto; overflow-y: visible; padding-bottom: 4px; }
.tl-inner { position: relative; min-width: 100%; }
.tl-lanes { position: relative; }
.tl-pin { position: absolute; transform: translateX(-50%); display: flex; flex-direction: column; align-items: center; border: 0; padding: 0; background: none; font: inherit; cursor: pointer; z-index: 1; }
.tl-pin-label { max-width: 150px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; padding: 4px 12px 4px 10px; border-radius: 999px; background: var(--bg-sub); color: var(--text-primary); font-size: 12px; border: 1px solid var(--border-line); border-left: 4px solid var(--ev); }
.tl-pin.on .tl-pin-label { background: var(--ev); color: #fff; border-color: var(--ev); }
.tl-pin.hidden .tl-pin-label { border-style: dashed; opacity: .75; }
.tl-pin:hover .tl-pin-label { border-color: var(--accent-color); }
.tl-pin-stem { position: absolute; top: 100%; width: 1px; background: var(--border-line); pointer-events: none; }
.tl-line { position: relative; height: 44px; border-top: 1px solid var(--text-secondary); margin-top: 10px; }
.tl-tick { position: absolute; top: 0; width: 1px; background: var(--text-secondary); }
.tl-tick.minor { height: 5px; opacity: .5; }
.tl-tick.major { height: 9px; }
.tl-tick em { position: absolute; top: 14px; left: 0; transform: translateX(-50%); font-style: normal; font-size: 11px; color: var(--text-secondary); white-space: nowrap; }
.tl-dot { position: absolute; top: -6px; width: 11px; height: 11px; margin-left: -5.5px; border-radius: 50%; background: var(--paper-card); border: 2px solid var(--text-primary); pointer-events: none; }
.tl-dot { border-color: var(--ev) !important; }
.tl-dot.on { background: var(--ev); }
.tl-dot.hidden { border-style: dashed; }
.tl-card { position: relative; width: min(440px, 100%); margin-bottom: 14px; padding: 14px; background: var(--paper-card); border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); box-shadow: 0 10px 28px rgba(0, 0, 0, .12); display: grid; gap: 10px; }
.tl-card { border-top-color: var(--ev); }
.tl-card.hidden { border-top-style: dashed; }
.tl-colors { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; font-size: 12px; color: var(--text-secondary); }
.tl-colors button { width: 18px; height: 18px; padding: 0; border: 2px solid transparent; border-radius: 50%; cursor: pointer; }
.tl-colors button.none { background: var(--paper-card); border-color: var(--border-line); }
.tl-colors button[aria-checked=true] { outline: 2px solid var(--text-primary); outline-offset: 1px; }
.tl-card-top { display: grid; grid-template-columns: 96px minmax(0, 1fr) auto; gap: 12px; align-items: start; }
.tl-img { position: relative; width: 96px; height: 96px; overflow: hidden; background: var(--bg-sub); display: grid; place-items: center; }
.tl-img.empty:not(:has(button)) { display: none; }
.tl-card-top:has(.tl-img.empty:not(:has(button))) { grid-template-columns: minmax(0, 1fr) auto; }
.tl-img img { width: 100%; height: 100%; object-fit: cover; }
.editable .tl-img img { cursor: pointer; }
.tl-img-add { width: 100%; height: 100%; border: 0; background: none; color: var(--text-secondary); font-size: 30px; cursor: pointer; }
.tl-img-add:hover { color: var(--accent-color); }
.tl-img-x { position: absolute; top: 4px; right: 4px; width: 22px; height: 22px; border: 0; background: var(--overlay, #0008); color: var(--on-overlay, #fff); cursor: pointer; }
.tl-head { min-width: 0; display: grid; gap: 6px; }
.tl-title { margin: 0; font-size: 17px; font-weight: 600; overflow-wrap: anywhere; }
.tl-date { margin: 0; font-size: 13px; color: var(--text-secondary); }
.tl-title-in { width: 100%; box-sizing: border-box; border: 0; border-bottom: 1px solid var(--border-line); background: none; color: var(--text-primary); font: inherit; font-size: 17px; font-weight: 600; padding: 2px 0; outline: none; }
.tl-title-in:focus { border-bottom-color: var(--accent-color); }
.tl-date-in { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; color: var(--text-secondary); font-size: 13px; }
.tl-date-in input { flex: 1 1 140px; min-width: 0; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; padding: 4px 8px; }
.tl-date-in .tl-era-in { flex: 0 1 110px; }
.tl-warn { margin: 0; font-size: 11px; color: var(--warning, #b7791f); }
.tl-tools { display: flex; gap: 2px; }
.tl-tools button { width: 28px; height: 28px; border: 0; background: none; color: var(--text-secondary); cursor: pointer; font-size: 15px; }
.tl-tools button:hover { color: var(--accent-color); background: var(--bg-sub); }
.tl-tools button.danger:hover { color: var(--danger); }
.tl-private { justify-self: start; font-size: 11px; padding: 1px 8px; border: 1px dashed var(--border-line); color: var(--text-secondary); }
.tl-desc { margin: 0; font-size: 14px; line-height: 1.7; white-space: pre-wrap; overflow-wrap: anywhere; }
.tl-desc-in { width: 100%; box-sizing: border-box; border: 1px solid var(--border-line); background: var(--bg-sub); color: var(--text-primary); font: inherit; font-size: 13px; line-height: 1.6; padding: 8px 10px; resize: vertical; }
.tl-tags { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; }
.tl-tag { display: inline-flex; align-items: center; gap: 2px; font-size: 12px; color: var(--accent-color); background: var(--accent-soft); padding: 1px 8px; }
.tl-tag button { border: 0; background: none; color: inherit; cursor: pointer; padding: 0 2px; }
.tl-tag-in { flex: 1 1 120px; min-width: 0; border: 0; border-bottom: 1px dashed var(--border-line); background: none; color: var(--text-primary); font: inherit; font-size: 12px; padding: 2px 0; outline: none; }
.tl-cards { display: flex; flex-wrap: wrap; gap: 6px; }
.tl-card-chip { display: inline-flex; align-items: center; gap: 6px; max-width: 100%; padding: 3px 10px 3px 3px; border: 1px solid var(--border-line); background: var(--bg-sub); color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.tl-card-chip img { width: 22px; height: 22px; object-fit: cover; }
.tl-card-chip span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.tl-card-chip:hover { border-color: var(--accent-color); }
.tl-card-chip i { font-style: normal; color: var(--text-secondary); padding-left: 2px; }
.tl-card-chip i:hover { color: var(--danger); }
.tl-add-cards { position: relative; }
.tl-picker { position: absolute; left: 0; top: calc(100% + 6px); z-index: 30; }
.tl-dashed { width: 100%; padding: 8px; border: 1px dashed var(--border-line); background: none; color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.tl-dashed:hover { color: var(--accent-color); border-color: var(--accent-color); }
.tl-unplaced { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; margin-top: 8px; font-size: 12px; color: var(--text-secondary); }
.tl-unplaced button { padding: 3px 10px; border: 1px solid var(--border-line); border-radius: 999px; background: var(--bg-sub); color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.tl-unplaced button { border-left: 4px solid var(--ev); }
.tl-unplaced button.on { background: var(--ev); color: #fff; }
.tl-unplaced button.hidden { border-style: dashed; }
.tl-empty { margin: 0; font-size: 13px; color: var(--text-secondary); }
.tl-actions { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; margin-top: 10px; }
.tl-actions .tl-dashed { width: auto; padding: 6px 14px; }
.tl-hint { font-size: 11px; color: var(--text-secondary); }
@media (max-width: 640px) {
  .tl-card-top { grid-template-columns: 72px minmax(0, 1fr); }
  .tl-img { width: 72px; height: 72px; }
  .tl-tools { grid-column: 1 / -1; justify-content: flex-end; order: -1; }
}
</style>
