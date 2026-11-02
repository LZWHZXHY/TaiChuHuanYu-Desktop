<!-- CanvasBlock.vue：「画布」区块——一块自由白板。可以放便签（文字 + 颜色）、图片、条目卡片（连到本世界的条目），
     东西之间可以连线（线上可以写一句说明、可以带箭头）。能拖动、缩放视图、改大小。
     editable = 编辑；不传就是阅读：只能看、拖动平移、缩放，点条目卡片在右边弹出那个条目，点图片看大图。
     数据都存在区块里（items / links / height），位置和大小是画布上的像素。 -->
<template>
  <div class="cv" :class="{ editable }">
    <div class="cv-tools">
      <template v-if="editable">
        <button type="button" :disabled="full" @click="addNote">＋ 便签</button>
        <button type="button" :disabled="uploading || full" @click="pickImage">{{ uploading ? '上传中…' : '＋ 图片' }}</button>
        <span class="cv-anchor">
          <button type="button" :disabled="full" :aria-expanded="pickerFor === 'add'" @click="pickerFor = pickerFor === 'add' ? null : 'add'">＋ 条目卡片</button>
          <CardPicker v-if="pickerFor === 'add'" class="cv-picker" :project-id="projectId" @close="pickerFor = null" @confirm="addCards" />
        </span>
        <span class="cv-sep" aria-hidden="true"></span>
      </template>
      <button type="button" title="缩小" aria-label="缩小" @click="zoomBy(1 / 1.25)">－</button>
      <span class="cv-zoom">{{ Math.round(scale * 100) }}%</span>
      <button type="button" title="放大" aria-label="放大" @click="zoomBy(1.25)">＋</button>
      <button type="button" title="把所有东西放进视野里" @click="fit">适应</button>
      <span class="cv-hint">{{ editable ? '拖空白处平移 · 选中后拖右边的圆点连线、拖右下角改大小 · Delete 删除' : '拖动平移 · 滚轮缩放（先点一下画布）· 点条目卡片看详情' }}</span>
    </div>

    <div ref="viewport" class="cv-viewport" :class="{ grabbing: drag?.kind === 'pan' && moved, linking: drag?.kind === 'link' }" :style="viewportStyle" tabindex="0"
      :aria-label="editable ? '画布：可以拖动、缩放，选中东西后按 Delete 删除' : '画布：可以拖动和缩放'"
      @pointerdown="onDown" @pointermove="onMove" @pointerup="onUp" @pointercancel="onUp" @wheel="onWheel" @keydown="onKey" @scroll="onScroll">
      <div class="cv-layer" :style="{ transform: `translate(${tx}px, ${ty}px) scale(${scale})` }">
        <!-- 连线画在最下面；SVG 的范围跟着内容走，坐标就是画布像素 -->
        <svg class="cv-links" :style="svgBox.style" :viewBox="svgBox.viewBox" aria-hidden="true">
          <defs>
            <marker :id="markerId" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="10" markerHeight="10" markerUnits="userSpaceOnUse" orient="auto-start-reverse">
              <path d="M0 0L10 5L0 10z" />
            </marker>
          </defs>
          <g v-for="l in drawnLinks" :key="l.id" class="cv-link" :class="{ on: selected === 'l:' + l.id }">
            <line :x1="l.x1" :y1="l.y1" :x2="l.x2" :y2="l.y2" :marker-end="l.arrow ? `url(#${markerId})` : undefined" />
            <line v-if="editable" class="hit" :x1="l.x1" :y1="l.y1" :x2="l.x2" :y2="l.y2" @pointerdown="onLinkDown($event, l.id)" />
          </g>
          <line v-if="linkDraft" class="cv-draft" :x1="linkDraft.x1" :y1="linkDraft.y1" :x2="linkDraft.x2" :y2="linkDraft.y2" />
        </svg>
        <span v-for="l in drawnLinks.filter(x => x.label)" :key="'t' + l.id" class="cv-link-label" :class="{ on: selected === 'l:' + l.id }"
          :style="{ left: l.mx + 'px', top: l.my + 'px' }" @pointerdown="onLinkDown($event, l.id)">{{ l.label }}</span>

        <div v-for="item in items" :key="item.id" class="cv-item" :class="['t-' + item.type, { on: selected === 'i:' + item.id, target: linkHover === item.id }]"
          :data-item-id="item.id" :style="{ left: item.x + 'px', top: item.y + 'px', width: item.w + 'px', height: item.h + 'px', '--note': item.color || NOTE_COLORS[0] }"
          :role="!editable && item.type !== 'note' ? 'button' : undefined" :tabindex="!editable && item.type !== 'note' ? 0 : undefined"
          :aria-label="!editable && item.type === 'card' ? `查看「${cardTitle(item.cardId)}」` : !editable && item.type === 'image' ? '看大图' : undefined"
          @pointerdown="onItemDown($event, item)" @keydown.enter="!editable && openItem(item)">
          <template v-if="item.type === 'note'">
            <textarea v-if="editable" class="cv-note-input" :value="item.text ?? ''" placeholder="写点什么…" maxlength="1000" aria-label="便签内容"
              @pointerdown.stop="selected = 'i:' + item.id" @input="item.text = ($event.target as HTMLTextAreaElement).value; emitItems()"></textarea>
            <div v-else class="cv-note-text">{{ item.text }}</div>
          </template>
          <img v-else-if="item.type === 'image'" :src="thumb(item.image)" alt="" draggable="false" />
          <template v-else>
            <span class="cv-card-cover"><img v-if="coverOf(item.cardId)" :src="coverOf(item.cardId)" alt="" draggable="false" /><span v-else aria-hidden="true">▢</span></span>
            <span class="cv-card-text"><b>{{ cardTitle(item.cardId) }}</b><small>{{ cardType(item.cardId) }}</small></span>
          </template>
          <template v-if="editable && selected === 'i:' + item.id">
            <span class="cv-handle-link" title="拖到另一样东西上，连一条线" @pointerdown.stop="onLinkStart($event, item)"></span>
            <span class="cv-handle-size" title="拖动改大小" @pointerdown.stop="onResizeStart($event, item)"></span>
          </template>
        </div>
      </div>
      <p v-if="editable && !items.length" class="cv-empty">用上面的按钮放便签、图片或者条目卡片。<br />选中一样东西后，拖它右边的圆点到另一样东西上就能连线。</p>
    </div>
    <div v-if="editable" class="cv-resize" title="上下拖动，调整画布高度" @pointerdown="onHeightDown" @pointermove="onHeightMove" @pointerup="onHeightUp" @pointercancel="onHeightUp">
      <span aria-hidden="true">⋯</span> 拖这里调整画布高度（{{ boardH }}px）
    </div>

    <!-- 编辑时：选中的东西的设置放在画布下面，不挡画布 -->
    <div v-if="editable && (selItem || selLink)" class="cv-panel">
      <template v-if="selItem">
        <span class="cv-panel-tip">{{ selItem.type === 'note' ? '便签' : selItem.type === 'image' ? '图片' : '条目卡片' }}</span>
        <div v-if="selItem.type === 'note'" class="cv-colors" role="radiogroup" aria-label="便签颜色">
          <button v-for="c in NOTE_COLORS" :key="c" type="button" role="radio" :aria-checked="(selItem.color || NOTE_COLORS[0]) === c" :style="{ background: c }" :aria-label="`颜色 ${c}`"
            @click="setNoteColor(c)"></button>
        </div>
        <span v-if="selItem.type === 'card'" class="cv-anchor cv-link-to">
          连到：<b>{{ cardTitle(selItem.cardId) }}</b>
          <button type="button" @click="pickerFor = pickerFor === 'change' ? null : 'change'">更换条目</button>
          <CardPicker v-if="pickerFor === 'change'" class="cv-picker" :project-id="projectId" @close="pickerFor = null" @confirm="ids => changeCard(ids[0])" />
        </span>
        <button type="button" @click="toFront">移到最上层</button>
        <button type="button" class="danger" @click="removeSelected">删除</button>
      </template>
      <template v-else-if="selLink">
        <span class="cv-panel-tip">连线</span>
        <input class="cv-label-input" :value="selLink.label ?? ''" maxlength="60" placeholder="线上的说明（可选），比如 师徒、敌对" aria-label="连线说明"
          @input="setLinkLabel(($event.target as HTMLInputElement).value)" />
        <label class="cv-check"><input type="checkbox" :checked="!!selLink.arrow" @change="setLinkArrow(($event.target as HTMLInputElement).checked)" />箭头</label>
        <button v-if="selLink.arrow" type="button" title="箭头换个方向" @click="flipLink">⇄ 反向</button>
        <button type="button" class="danger" @click="removeSelected">删除连线</button>
      </template>
    </div>
    <p v-if="editable && full" class="cv-full">画布上最多放 {{ CANVAS_MAX_ITEMS }} 样东西，可以再加一块画布。</p>
    <input ref="fileInput" type="file" accept="image/*" multiple hidden @change="onFiles" />
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useWorldStore } from '@/stores/world'
import { getCardTypeLabel, parseCoverImage } from '@/utils/worldPresentation'
import { withThumbnail } from '@/utils/imageThumb'
import {
  CANVAS_DEFAULT_SIZE, CANVAS_MAX_ITEMS, CANVAS_MAX_LINKS, CANVAS_MIN_SIZE, NOTE_COLORS, newId,
  type CanvasItem, type CanvasLink,
} from '@/utils/worldLayout'
import { useCardUpload } from '@/composables/世界观管理/useCardUpload'
import CardPicker from './CardPicker.vue'

const props = defineProps<{ items?: CanvasItem[]; links?: CanvasLink[]; height?: number; projectId: string; editable?: boolean }>()
const emit = defineEmits<{
  'update:items': [items: CanvasItem[]]; 'update:links': [links: CanvasLink[]]; 'update:height': [height: number]
  peek: [cardId: string]; preview: [urls: string[], index: number]
}>()
const store = useWorldStore()
const { uploadGallery, uploadingGallery: uploading } = useCardUpload()
// 同一页可能有好几块画布，箭头的 id 要各不相同
const markerId = newId('cvm')

// ---------- 数据：在本地副本上改，改完整个发出去（拖动时只在松手时发一次） ----------
const items = ref<CanvasItem[]>([])
const links = ref<CanvasLink[]>([])
// 正在拖的时候不要被外面传回来的数据打断（拖动的状态见下面「选中、拖动」）
const drag = ref<Drag | null>(null)
const moved = ref(false)
watch(() => props.items, v => { if (!drag.value) items.value = (v ?? []).map(x => ({ ...x })) }, { immediate: true, deep: true })
watch(() => props.links, v => { if (!drag.value) links.value = (v ?? []).map(x => ({ ...x })) }, { immediate: true, deep: true })
const emitItems = () => emit('update:items', items.value.map(x => ({ ...x })))
const emitLinks = () => emit('update:links', links.value.map(x => ({ ...x })))
const full = computed(() => items.value.length >= CANVAS_MAX_ITEMS)

const byId = computed(() => new Map(items.value.map(x => [x.id, x])))
const cardOf = (id?: string | null) => (id ? store.getCardById(id, props.projectId) : undefined)
const cardTitle = (id?: string | null) => cardOf(id)?.title ?? (id ? '已删除的条目' : '未选择条目')
const cardType = (id?: string | null) => getCardTypeLabel(cardOf(id)?.type ?? '')
const coverOf = (id?: string | null) => { const c = cardOf(id)?.coverImage; return c ? withThumbnail(parseCoverImage(c), 96) : '' }
const thumb = (url?: string) => (url ? withThumbnail(url, 600) : '')

// ---------- 视图：平移、缩放（不保存，每次打开自动「适应」） ----------
const viewport = ref<HTMLElement | null>(null)
const viewW = ref(640)
const boardH = ref(props.height ?? 420)
watch(() => props.height, h => { if (!heightDrag) boardH.value = h ?? 420 })
const scale = ref(1)
const tx = ref(0)
const ty = ref(0)
const GRID = 24
const viewportStyle = computed(() => ({
  height: boardH.value + 'px',
  backgroundSize: `${GRID * scale.value}px ${GRID * scale.value}px`,
  backgroundPosition: `${tx.value}px ${ty.value}px`,
}))
function zoomAt(factor: number, cx: number, cy: number) {
  const next = Math.min(2.5, Math.max(0.2, scale.value * factor))
  const k = next / scale.value
  tx.value = cx - (cx - tx.value) * k
  ty.value = cy - (cy - ty.value) * k
  scale.value = next
}
const zoomBy = (factor: number) => zoomAt(factor, viewW.value / 2, boardH.value / 2)
function onWheel(e: WheelEvent) {
  // 和地图一样：点过画布（焦点在画布里）或者按着 Ctrl 才用滚轮缩放，不抢页面滚动
  if (!viewport.value?.contains(document.activeElement) && !e.ctrlKey) return
  e.preventDefault()
  const rect = viewport.value!.getBoundingClientRect()
  zoomAt(e.deltaY < 0 ? 1.12 : 1 / 1.12, e.clientX - rect.left, e.clientY - rect.top)
}
/** 把所有东西放进视野（最多放大到 100%） */
function fit() {
  const list = items.value
  if (!list.length) { scale.value = 1; tx.value = 0; ty.value = 0; return }
  const minX = Math.min(...list.map(i => i.x)), minY = Math.min(...list.map(i => i.y))
  const maxX = Math.max(...list.map(i => i.x + i.w)), maxY = Math.max(...list.map(i => i.y + i.h))
  const bw = Math.max(1, maxX - minX), bh = Math.max(1, maxY - minY)
  const s = Math.min(1, Math.max(0.2, Math.min((viewW.value - 48) / bw, (boardH.value - 48) / bh)))
  scale.value = s
  tx.value = (viewW.value - bw * s) / 2 - minX * s
  ty.value = (boardH.value - bh * s) / 2 - minY * s
}
let fitted = false
let observer: ResizeObserver | null = null
function measure() { if (viewport.value?.clientWidth) viewW.value = viewport.value.clientWidth }
onMounted(() => {
  measure()
  void nextTick(() => { fit(); fitted = true })
  if (viewport.value && typeof ResizeObserver !== 'undefined') {
    observer = new ResizeObserver(() => { const before = viewW.value; measure(); if (!touched && before !== viewW.value) fit() })
    observer.observe(viewport.value)
  }
})
onBeforeUnmount(() => observer?.disconnect())
// 用户动过视图就不再自动适应；条目读进来之前是空的，读进来后自动适应一次
let touched = false
watch(() => items.value.length, (n, o) => { if (fitted && !o && n && !touched) fit() })

/** 屏幕坐标 → 画布坐标 */
function toWorld(clientX: number, clientY: number) {
  const rect = viewport.value!.getBoundingClientRect()
  return { x: (clientX - rect.left - tx.value) / scale.value, y: (clientY - rect.top - ty.value) / scale.value }
}

// ---------- 选中、拖动 ----------
const selected = ref<string | null>(null)
const selItem = computed(() => (selected.value?.startsWith('i:') ? byId.value.get(selected.value.slice(2)) ?? null : null))
const selLink = computed(() => (selected.value?.startsWith('l:') ? links.value.find(l => l.id === selected.value!.slice(2)) ?? null : null))
const pickerFor = ref<'add' | 'change' | null>(null)
watch(selected, () => { if (pickerFor.value === 'change') pickerFor.value = null })

type Drag =
  | { kind: 'pan'; sx: number; sy: number; tx0: number; ty0: number; clickItem?: string }
  | { kind: 'move'; id: string; sx: number; sy: number; x0: number; y0: number }
  | { kind: 'resize'; id: string; sx: number; sy: number; w0: number; h0: number }
  | { kind: 'link'; id: string }
const linkHover = ref<string | null>(null)
const linkDraft = ref<{ x1: number; y1: number; x2: number; y2: number } | null>(null)

function begin(e: PointerEvent, d: Drag) {
  viewport.value?.focus({ preventScroll: true })
  drag.value = d
  moved.value = false
  viewport.value?.setPointerCapture(e.pointerId)
}
function onDown(e: PointerEvent) {
  if (e.button !== 0) return
  begin(e, { kind: 'pan', sx: e.clientX, sy: e.clientY, tx0: tx.value, ty0: ty.value })
}
function onItemDown(e: PointerEvent, item: CanvasItem) {
  if (e.button !== 0) return
  e.stopPropagation()
  if (props.editable) {
    selected.value = 'i:' + item.id
    begin(e, { kind: 'move', id: item.id, sx: e.clientX, sy: e.clientY, x0: item.x, y0: item.y })
  } else {
    // 阅读时按在东西上也是平移；没挪动就当成点了一下
    begin(e, { kind: 'pan', sx: e.clientX, sy: e.clientY, tx0: tx.value, ty0: ty.value, clickItem: item.id })
  }
}
function onResizeStart(e: PointerEvent, item: CanvasItem) {
  if (e.button !== 0) return
  begin(e, { kind: 'resize', id: item.id, sx: e.clientX, sy: e.clientY, w0: item.w, h0: item.h })
}
function onLinkStart(e: PointerEvent, item: CanvasItem) {
  if (e.button !== 0) return
  begin(e, { kind: 'link', id: item.id })
  const p = toWorld(e.clientX, e.clientY)
  linkDraft.value = { x1: item.x + item.w / 2, y1: item.y + item.h / 2, x2: p.x, y2: p.y }
}
function onLinkDown(e: PointerEvent, id: string) {
  if (!props.editable || e.button !== 0) return
  e.stopPropagation()
  selected.value = 'l:' + id
  viewport.value?.focus({ preventScroll: true })
}
function onMove(e: PointerEvent) {
  const d = drag.value
  if (!d) return
  if (d.kind === 'link') {
    const p = toWorld(e.clientX, e.clientY)
    if (linkDraft.value) { linkDraft.value.x2 = p.x; linkDraft.value.y2 = p.y }
    // 按住的时候指针被画布「抓住」了，用坐标找下面是哪样东西
    const hit = (document.elementFromPoint(e.clientX, e.clientY) as HTMLElement | null)?.closest?.('[data-item-id]')?.getAttribute('data-item-id') ?? null
    linkHover.value = hit && hit !== d.id ? hit : null
    return
  }
  const dx = e.clientX - d.sx, dy = e.clientY - d.sy
  if (!moved.value && Math.abs(dx) + Math.abs(dy) > 3) moved.value = true
  if (!moved.value) return
  if (d.kind === 'pan') { tx.value = d.tx0 + dx; ty.value = d.ty0 + dy; touched = true; return }
  const item = byId.value.get(d.id)
  if (!item) return
  if (d.kind === 'move') {
    item.x = Math.round(Math.min(20000, Math.max(-20000, d.x0 + dx / scale.value)))
    item.y = Math.round(Math.min(20000, Math.max(-20000, d.y0 + dy / scale.value)))
  } else {
    const min = CANVAS_MIN_SIZE[item.type]
    item.w = Math.round(Math.min(1600, Math.max(min.w, d.w0 + dx / scale.value)))
    item.h = Math.round(Math.min(1600, Math.max(min.h, d.h0 + dy / scale.value)))
  }
}
function onUp(e: PointerEvent) {
  const d = drag.value
  if (!d) return
  if (viewport.value?.hasPointerCapture(e.pointerId)) viewport.value.releasePointerCapture(e.pointerId)
  drag.value = null
  if (d.kind === 'link') {
    if (linkHover.value) addLink(d.id, linkHover.value)
    linkHover.value = null
    linkDraft.value = null
    return
  }
  if (d.kind === 'pan' && !moved.value) {
    const item = d.clickItem ? byId.value.get(d.clickItem) : null
    if (item) openItem(item)
    else if (props.editable) selected.value = null
    return
  }
  if ((d.kind === 'move' || d.kind === 'resize') && moved.value) emitItems()
}

/** 阅读时点了一样东西：条目卡片在右边弹出，图片看大图 */
function openItem(item: CanvasItem) {
  if (item.type === 'card' && item.cardId) emit('peek', item.cardId)
  else if (item.type === 'image' && item.image) {
    const images = items.value.filter(i => i.type === 'image' && i.image)
    emit('preview', images.map(i => i.image!), Math.max(0, images.indexOf(item)))
  }
}

// 画布是 overflow: hidden，但点到半露在外面的便签时浏览器会偷偷把它滚进来，整个画面就错位了：滚了就滚回去
function onScroll(e: Event) {
  const el = e.target as HTMLElement
  if (el === viewport.value && (el.scrollTop || el.scrollLeft)) { el.scrollTop = 0; el.scrollLeft = 0 }
}
function onKey(e: KeyboardEvent) {
  // 正在便签、输入框里打字时不处理
  if ((e.target as HTMLElement)?.closest?.('textarea, input')) return
  if (props.editable && (e.key === 'Delete' || e.key === 'Backspace') && selected.value) { e.preventDefault(); removeSelected() }
  else if (e.key === 'Escape') selected.value = null
}

// ---------- 加、删、改 ----------
let addCount = 0
/** 新东西放在当前视野中间，连着加的话错开一点，免得叠在一起 */
function spot(w: number, h: number) {
  const offset = (addCount++ % 6) * 24
  return {
    x: Math.round((viewW.value / 2 - tx.value) / scale.value - w / 2 + offset),
    y: Math.round((boardH.value / 2 - ty.value) / scale.value - h / 2 + offset),
  }
}
function addItem(item: CanvasItem) {
  if (full.value) return
  items.value.push(item)
  selected.value = 'i:' + item.id
}
function addNote() {
  const size = CANVAS_DEFAULT_SIZE.note
  addItem({ id: newId('c'), type: 'note', ...spot(size.w, size.h), ...size, text: '' })
  emitItems()
  // 加好直接打字
  void nextTick(() => viewport.value?.querySelector<HTMLTextAreaElement>(`[data-item-id="${selItem.value?.id}"] textarea`)?.focus({ preventScroll: true }))
}
function addCards(ids: string[]) {
  pickerFor.value = null
  const size = CANVAS_DEFAULT_SIZE.card
  const cols = Math.min(3, ids.length)
  const base = spot(size.w * cols + 16 * (cols - 1), size.h)
  ids.slice(0, CANVAS_MAX_ITEMS - items.value.length).forEach((cardId, i) => {
    addItem({ id: newId('c'), type: 'card', x: base.x + (i % cols) * (size.w + 16), y: base.y + Math.floor(i / cols) * (size.h + 16), ...size, cardId })
  })
  emitItems()
}
function changeCard(cardId?: string) {
  pickerFor.value = null
  if (!cardId || !selItem.value) return
  selItem.value.cardId = cardId
  emitItems()
}
const fileInput = ref<HTMLInputElement | null>(null)
const pickImage = () => fileInput.value?.click()
/** 读出图片的宽高，按比例放（读不出来就用默认大小） */
function imageSize(url: string): Promise<{ w: number; h: number }> {
  return new Promise(resolve => {
    const img = new Image()
    const fallback = () => resolve(CANVAS_DEFAULT_SIZE.image)
    img.onload = () => {
      if (!img.naturalWidth || !img.naturalHeight) return fallback()
      const w = 240
      resolve({ w, h: Math.round(Math.min(600, Math.max(CANVAS_MIN_SIZE.image.h, w * img.naturalHeight / img.naturalWidth))) })
    }
    img.onerror = fallback
    img.src = url
  })
}
async function onFiles(e: Event) {
  const input = e.target as HTMLInputElement
  const files = input.files
  if (!files?.length) return
  const urls = await uploadGallery(files)
  input.value = ''
  for (const url of urls.slice(0, CANVAS_MAX_ITEMS - items.value.length)) {
    const size = await imageSize(url)
    addItem({ id: newId('c'), type: 'image', ...spot(size.w, size.h), ...size, image: url })
  }
  if (urls.length) emitItems()
}
function addLink(from: string, to: string) {
  // 同两样东西之间已经有线了就选中那条，不重复连
  const exist = links.value.find(l => (l.from === from && l.to === to) || (l.from === to && l.to === from))
  if (exist) { selected.value = 'l:' + exist.id; return }
  if (links.value.length >= CANVAS_MAX_LINKS) return
  const link: CanvasLink = { id: newId('l'), from, to }
  links.value.push(link)
  selected.value = 'l:' + link.id
  emitLinks()
  // 连好了直接写说明
  void nextTick(() => document.querySelector<HTMLInputElement>('.cv-panel .cv-label-input')?.focus({ preventScroll: true }))
}
// 下面这几个改的都是「当前选中」的那一个
function setNoteColor(c: string) {
  if (!selItem.value) return
  // 第一种颜色是默认色，不用存
  selItem.value.color = c === NOTE_COLORS[0] ? undefined : c
  emitItems()
}
function setLinkLabel(text: string) {
  if (!selLink.value) return
  selLink.value.label = text || undefined
  emitLinks()
}
function setLinkArrow(on: boolean) {
  if (!selLink.value) return
  selLink.value.arrow = on || undefined
  emitLinks()
}
function flipLink() {
  const link = selLink.value
  if (!link) return
  const from = link.from
  link.from = link.to
  link.to = from
  emitLinks()
}
function toFront() {
  const i = items.value.findIndex(x => x.id === selItem.value?.id)
  if (i < 0 || i === items.value.length - 1) return
  const [item] = items.value.splice(i, 1)
  items.value.push(item!)
  emitItems()
}
function removeSelected() {
  const sel = selected.value
  if (!sel) return
  selected.value = null
  if (sel.startsWith('l:')) {
    links.value = links.value.filter(l => l.id !== sel.slice(2))
    emitLinks()
    return
  }
  const id = sel.slice(2)
  const item = byId.value.get(id)
  if (item?.type === 'note' && item.text?.trim() && !window.confirm('删除这张便签？')) return
  items.value = items.value.filter(x => x.id !== id)
  // 连着它的线一起删
  const before = links.value.length
  links.value = links.value.filter(l => l.from !== id && l.to !== id)
  emitItems()
  if (links.value.length !== before) emitLinks()
}

// ---------- 连线的位置：从框的边上出发，不压在东西上面 ----------
function edgePoint(r: CanvasItem, toward: { x: number; y: number }) {
  const cx = r.x + r.w / 2, cy = r.y + r.h / 2
  const dx = toward.x - cx, dy = toward.y - cy
  if (!dx && !dy) return { x: cx, y: cy }
  const t = Math.min(Math.abs(dx) > 1e-9 ? (r.w / 2 + 4) / Math.abs(dx) : Infinity, Math.abs(dy) > 1e-9 ? (r.h / 2 + 4) / Math.abs(dy) : Infinity)
  return { x: cx + dx * t, y: cy + dy * t }
}
const drawnLinks = computed(() => links.value.flatMap(l => {
  const a = byId.value.get(l.from), b = byId.value.get(l.to)
  if (!a || !b) return []
  const p1 = edgePoint(a, { x: b.x + b.w / 2, y: b.y + b.h / 2 })
  const p2 = edgePoint(b, { x: a.x + a.w / 2, y: a.y + a.h / 2 })
  return [{ ...l, x1: p1.x, y1: p1.y, x2: p2.x, y2: p2.y, mx: (p1.x + p2.x) / 2, my: (p1.y + p2.y) / 2 }]
}))
// SVG 的范围 = 所有东西外面再留一圈（连线拖出去的时候也够用）
const svgBox = computed(() => {
  const list = items.value
  const pad = 2000
  const minX = (list.length ? Math.min(...list.map(i => i.x)) : 0) - pad, minY = (list.length ? Math.min(...list.map(i => i.y)) : 0) - pad
  const maxX = (list.length ? Math.max(...list.map(i => i.x + i.w)) : 0) + pad, maxY = (list.length ? Math.max(...list.map(i => i.y + i.h)) : 0) + pad
  return { viewBox: `${minX} ${minY} ${maxX - minX} ${maxY - minY}`, style: { left: minX + 'px', top: minY + 'px', width: maxX - minX + 'px', height: maxY - minY + 'px' } }
})

// ---------- 画布高度（编辑时拖下面的条调整，保存在区块里） ----------
let heightDrag: { sy: number; h0: number } | null = null
function onHeightDown(e: PointerEvent) {
  if (e.button !== 0) return
  heightDrag = { sy: e.clientY, h0: boardH.value }
  ;(e.currentTarget as HTMLElement).setPointerCapture(e.pointerId)
}
function onHeightMove(e: PointerEvent) {
  if (heightDrag) boardH.value = Math.round(Math.min(1200, Math.max(240, heightDrag.h0 + e.clientY - heightDrag.sy)))
}
function onHeightUp(e: PointerEvent) {
  if (!heightDrag) return
  const changed = boardH.value !== heightDrag.h0
  heightDrag = null
  const el = e.currentTarget as HTMLElement
  if (el.hasPointerCapture(e.pointerId)) el.releasePointerCapture(e.pointerId)
  if (changed) emit('update:height', boardH.value)
}
</script>

<style scoped>
.cv { display: grid; gap: 8px; min-width: 0; }
.cv-tools { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; font-size: 12px; }
.cv-tools button { min-width: 28px; height: 28px; padding: 0 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.cv-tools button:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); }
.cv-tools button:disabled { opacity: .45; cursor: default; }
.cv-sep { width: 1px; height: 18px; margin: 0 4px; background: var(--border-line); }
.cv-zoom { min-width: 40px; text-align: center; color: var(--text-secondary); }
.cv-hint { margin-left: auto; color: var(--text-secondary); }
.cv-anchor { position: relative; }
.cv-picker { position: absolute; left: 0; top: calc(100% + 4px); z-index: 20; }
.cv-viewport { position: relative; max-height: 80vh; overflow: hidden; border: 1px solid var(--border-line); background-color: var(--bg-sub); background-image: radial-gradient(circle, var(--border-line) 1px, transparent 1.2px); cursor: grab; touch-action: none; user-select: none; outline: none; }
.cv-viewport:focus-visible { border-color: var(--accent-color); }
.cv-viewport.grabbing { cursor: grabbing; }
.cv-viewport.linking { cursor: crosshair; }
.cv-layer { position: absolute; left: 0; top: 0; width: 0; height: 0; transform-origin: 0 0; }
.cv-links { position: absolute; overflow: visible; pointer-events: none; }
.cv-link line { stroke: var(--text-secondary); stroke-width: 1.6; vector-effect: non-scaling-stroke; }
.cv-link.on line:not(.hit) { stroke: var(--accent-color); stroke-width: 2.4; }
.cv-link line.hit { stroke: transparent; stroke-width: 14; pointer-events: stroke; cursor: pointer; }
.cv-links marker path { fill: var(--text-secondary); }
.cv-draft { stroke: var(--accent-color); stroke-width: 2; stroke-dasharray: 6 4; vector-effect: non-scaling-stroke; }
.cv-link-label { position: absolute; transform: translate(-50%, -50%); max-width: 160px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; padding: 1px 8px; background: var(--paper-card); border: 1px solid var(--border-line); color: var(--text-secondary); font-size: 12px; line-height: 1.5; }
.editable .cv-link-label { cursor: pointer; }
.cv-link-label.on { border-color: var(--accent-color); color: var(--accent-color); }
.cv-item { position: absolute; box-sizing: border-box; border: 1px solid var(--border-line); background: var(--paper-card); box-shadow: 0 2px 6px rgba(0, 0, 0, .08); }
.editable .cv-item { cursor: move; }
.cv-item.on { outline: 2px solid var(--accent-color); outline-offset: 2px; }
.cv-item.target { outline: 2px dashed var(--accent-color); outline-offset: 3px; }
.cv-item[role=button] { cursor: pointer; }
.cv-item[role=button]:hover, .cv-item[role=button]:focus-visible { border-color: var(--accent-color); outline: none; }
/* 便签：浅色底，字固定深色（深色模式下也看得清） */
.cv-item.t-note { background: var(--note); border-color: rgba(0, 0, 0, .12); color: #2b2b2b; padding: 14px 8px 8px; }
.cv-note-input { width: 100%; height: 100%; box-sizing: border-box; border: 0; padding: 0; background: none; color: inherit; font: inherit; font-size: 14px; line-height: 1.6; resize: none; outline: none; cursor: text; }
.cv-note-input::placeholder { color: rgba(0, 0, 0, .4); }
.cv-note-text { height: 100%; overflow: hidden; font-size: 14px; line-height: 1.6; white-space: pre-wrap; overflow-wrap: anywhere; }
.cv-item.t-image { padding: 0; background: var(--bg-sub); }
.cv-item.t-image img { width: 100%; height: 100%; display: block; object-fit: cover; pointer-events: none; }
.cv-item.t-card { display: flex; align-items: center; gap: 10px; padding: 8px 10px; border-left: 3px solid var(--accent-color); }
.cv-card-cover { flex-shrink: 0; width: 44px; height: 44px; display: grid; place-items: center; overflow: hidden; background: var(--bg-sub); color: var(--text-secondary); }
.cv-card-cover img { width: 100%; height: 100%; object-fit: cover; pointer-events: none; }
.cv-card-text { display: grid; min-width: 0; }
.cv-card-text b { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 14px; color: var(--text-primary); }
.cv-card-text small { font-size: 11px; color: var(--accent-color); }
.cv-handle-link { position: absolute; right: -9px; top: 50%; width: 14px; height: 14px; margin-top: -7px; border: 2px solid var(--paper-card); border-radius: 50%; background: var(--accent-color); cursor: crosshair; }
.cv-handle-size { position: absolute; right: -5px; bottom: -5px; width: 10px; height: 10px; background: var(--accent-color); border: 1px solid var(--paper-card); cursor: nwse-resize; }
.cv-empty { position: absolute; inset: 0; display: grid; place-content: center; margin: 0; padding: 20px; text-align: center; font-size: 13px; line-height: 1.8; color: var(--text-secondary); pointer-events: none; }
.cv-resize { display: flex; justify-content: center; align-items: center; gap: 6px; margin-top: -8px; padding: 2px 0; border: 1px solid var(--border-line); border-top: 0; background: var(--paper-card); color: var(--text-secondary); font-size: 11px; cursor: ns-resize; touch-action: none; user-select: none; }
.cv-resize:hover { color: var(--accent-color); }
.cv-panel { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; padding: 8px 10px; border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); background: var(--paper-card); font-size: 12px; }
.cv-panel-tip { color: var(--text-secondary); }
.cv-panel button { padding: 3px 10px; border: 1px solid var(--border-line); background: none; color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.cv-panel button:hover { border-color: var(--accent-color); color: var(--accent-color); }
.cv-panel button.danger { color: var(--danger); }
.cv-panel button.danger:hover { border-color: var(--danger); color: var(--danger); }
.cv-colors { display: flex; gap: 6px; }
.cv-colors button { width: 20px; height: 20px; padding: 0; border: 1px solid rgba(0, 0, 0, .2); border-radius: 50%; }
.cv-colors button[aria-checked=true] { outline: 2px solid var(--text-primary); outline-offset: 1px; }
.cv-link-to { display: inline-flex; align-items: center; gap: 6px; color: var(--text-secondary); }
.cv-link-to b { color: var(--text-primary); }
.cv-label-input { flex: 1 1 200px; min-width: 0; padding: 4px 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 12px; }
.cv-label-input:focus { outline: none; border-color: var(--accent-color); }
.cv-check { display: inline-flex; align-items: center; gap: 4px; cursor: pointer; }
.cv-check input { accent-color: var(--accent-color); }
.cv-full { margin: 0; font-size: 12px; color: var(--text-secondary); }
@media (max-width: 640px) { .cv-hint { display: none; } }
</style>
