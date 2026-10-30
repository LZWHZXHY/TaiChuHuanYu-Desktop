<!-- MapBlock.vue：地图区块（像 vvd）。一张底图 + 图钉，可以缩放、拖动；图钉可以连到条目，点开看详情。
     editable 时：上传 / 更换底图，「插图钉」后点地图放一个，图钉可以拖动，改名字、颜色、备注、连到哪个条目。 -->
<template>
  <div class="map-block" :class="{ editable }">
    <div v-if="!image" class="mb-empty">
      <template v-if="editable">
        <button type="button" class="mb-upload" :disabled="uploading" @click="pickImage">{{ uploading ? '上传中…' : '＋ 上传地图图片' }}</button>
        <p>建议用清晰的大图（世界地图、城市平面图、区域图都行）。上传后可以在上面插图钉。</p>
      </template>
      <p v-else>还没有地图</p>
    </div>

    <template v-else>
      <div class="mb-tools">
        <button type="button" title="放大" aria-label="放大" @click="zoomBy(1.4)">＋</button>
        <button type="button" title="缩小" aria-label="缩小" @click="zoomBy(1 / 1.4)">－</button>
        <button type="button" title="还原" @click="resetView">还原</button>
        <span class="mb-zoom">{{ Math.round(scale * 100) }}%</span>
        <template v-if="editable">
          <button type="button" class="mb-place" :aria-pressed="placing" @click="placing = !placing">{{ placing ? '点地图放图钉…（再点一次取消）' : '📍 插图钉' }}</button>
          <button type="button" :disabled="uploading" @click="pickImage">{{ uploading ? '上传中…' : '更换底图' }}</button>
        </template>
        <span class="mb-hint">{{ editable ? '拖动图钉可以挪位置' : '滚轮缩放（先点一下地图）· 拖动平移' }}</span>
      </div>

      <div ref="viewport" class="mb-viewport" :class="{ placing, grabbing: dragging === 'pan' }" :style="{ height: baseH + 'px' }" tabindex="0"
        aria-label="地图，可以缩放和拖动" @wheel="onWheel" @pointerdown="onDown" @pointermove="onMove" @pointerup="onUp" @pointercancel="onUp"
        @keydown.esc="activePin = null; placing = false">
        <div ref="layer" class="mb-layer" :style="{ width: baseW + 'px', height: baseH + 'px', transform: `translate(${tx}px, ${ty}px) scale(${scale})` }">
          <img :src="image" alt="地图" draggable="false" @load="onImageLoad" />
          <button v-for="pin in pins" :key="pin.id" type="button" class="mb-pin" :class="{ on: pin.id === activePin, linked: !!pin.cardId }"
            :style="{ left: pin.x * 100 + '%', top: pin.y * 100 + '%', '--pin': pin.color || 'var(--accent-color)', transform: `translate(-50%, -100%) scale(${1 / scale})` }"
            :title="pin.relation ? `${pin.relation} · ${pinTitle(pin)}` : pinTitle(pin)" @pointerdown.stop="onPinDown($event, pin)" @click.stop>
            <svg viewBox="0 0 24 32" aria-hidden="true"><path d="M12 0C5.4 0 0 5.3 0 11.9 0 20.8 12 32 12 32s12-11.2 12-20.1C24 5.3 18.6 0 12 0z" /><circle cx="12" cy="12" r="4.5" /></svg>
            <span class="mb-pin-label">{{ pinTitle(pin) }}</span>
          </button>
        </div>

        <!-- 阅读时：图钉旁边弹出小卡片 -->
        <div v-if="current && !editable" class="mb-pop" :style="popStyle" @pointerdown.stop @wheel.stop>
          <div class="mb-pop-head">
            <img v-if="linkedCover" :src="linkedCover" alt="" class="mb-cover" />
            <div class="mb-pop-title">
              <b>{{ pinTitle(current) }}</b>
              <small v-if="current.cardId">{{ cardType(current.cardId) }}<template v-if="current.label?.trim() && current.label.trim() !== cardTitle(current.cardId)"> · {{ cardTitle(current.cardId) }}</template></small>
            </div>
            <span v-if="current.relation" class="mb-rel-tag" :style="{ '--pin': current.color || COLORS[0] }">{{ current.relation }}</span>
            <button type="button" class="x" aria-label="关闭" @click="activePin = null">×</button>
          </div>
          <p v-if="current.note" class="mb-note">{{ current.note }}</p>
          <button v-if="current.cardId" type="button" class="mb-open" @click="$emit('peek', current.cardId)">查看「{{ cardTitle(current.cardId) }}」详情 →</button>
        </div>
      </div>

      <!-- 编辑时：图钉的设置放在地图下面，不会挡住地图，也不会被裁掉 -->
      <div v-if="current && editable" class="mb-pop mb-panel">
        <span class="mb-panel-tip">正在编辑选中的图钉（拖动图钉可以挪位置）</span>
            <div class="mb-pop-head">
              <input v-model="current.label" placeholder="图钉名字（连了条目可以不填）" maxlength="60" aria-label="图钉名字" @input="emitPins" />
              <button type="button" class="x" aria-label="关闭" @click="activePin = null">×</button>
            </div>
            <div class="mb-colors" role="radiogroup" aria-label="图钉颜色">
              <button v-for="c in COLORS" :key="c" type="button" role="radio" :aria-checked="(current.color || COLORS[0]) === c" :style="{ background: c }" :aria-label="`颜色 ${c}`" @click="current.color = c; emitPins()"></button>
            </div>
            <div class="mb-link">
              <template v-if="current.cardId">
                <span>连到：<b>{{ cardTitle(current.cardId) }}</b></span>
                <button type="button" @click="current.cardId = null; current.relation = undefined; emitPins()">取消连接</button>
                <input v-model="current.relation" class="mb-rel" placeholder="关系说明，如 首都、战场遗址" maxlength="40" aria-label="和这个条目的关系" @input="emitPins" />
                <span class="mb-rel-quick">
                  <button v-for="r in RELATION_HINTS" :key="r" type="button" @click="current.relation = r; emitPins()">{{ r }}</button>
                </span>
              </template>
              <button v-else type="button" @click="pickerOpen = !pickerOpen">＋ 连到一个条目</button>
              <CardPicker v-if="pickerOpen" class="mb-picker" :project-id="projectId" @close="pickerOpen = false"
                @confirm="ids => { current!.cardId = ids[0] ?? null; pickerOpen = false; emitPins() }" />
            </div>
            <textarea v-model="current.note" rows="2" placeholder="备注（可选）" aria-label="备注" @input="emitPins"></textarea>
            <button type="button" class="mb-del" @click="removePin(current.id)">删除图钉</button>
      </div>

      <!-- 图钉列表 -->
      <div v-if="pins.length" class="mb-legend">
        <button v-for="pin in pins" :key="pin.id" type="button" :class="{ on: pin.id === activePin }" @click="focusPin(pin)">
          <i :style="{ background: pin.color || 'var(--accent-color)' }"></i>{{ pinTitle(pin) }}<small v-if="pin.relation">{{ pin.relation }}</small>
        </button>
      </div>
    </template>
    <input ref="fileInput" type="file" accept="image/*" hidden @change="onFile" />
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useWorldStore } from '@/stores/world'
import { getCardTypeLabel, parseCoverImage } from '@/utils/worldPresentation'
import { withThumbnail } from '@/utils/imageThumb'
import { MARK_COLORS, newId, type MapPin } from '@/utils/worldLayout'
import { useCardUpload } from '@/composables/世界观管理/useCardUpload'
import CardPicker from './CardPicker.vue'

const props = defineProps<{ image?: string; pins?: MapPin[]; projectId: string; editable?: boolean }>()
const emit = defineEmits<{ 'update:image': [url: string]; 'update:pins': [pins: MapPin[]]; peek: [cardId: string] }>()
const store = useWorldStore()
const { uploadGallery, uploadingGallery: uploading } = useCardUpload()
const COLORS = MARK_COLORS

// 编辑时在本地副本上改，改完整个发出去
const pins = ref<MapPin[]>([])
watch(() => props.pins, value => { pins.value = (value ?? []).map(p => ({ ...p })) }, { immediate: true, deep: true })
function emitPins() { emit('update:pins', pins.value.map(p => ({ ...p }))) }

const cardOf = (id?: string | null) => (id ? store.getCardById(id, props.projectId) : undefined)
const cardTitle = (id?: string | null) => cardOf(id)?.title ?? '已删除的条目'
const cardType = (id?: string | null) => getCardTypeLabel(cardOf(id)?.type ?? '')
// 常用的关系说明，一点就填上
const RELATION_HINTS = ['首都', '城市', '据点', '遗址', '战场', '出生地', '总部', '禁地']
const pinTitle = (pin: MapPin) => pin.label?.trim() || (pin.cardId ? cardTitle(pin.cardId) : '未命名地点')

// ---------- 尺寸 ----------
const viewport = ref<HTMLElement | null>(null)
const layer = ref<HTMLElement | null>(null)
const natural = ref({ w: 4, h: 3 })
const viewW = ref(640)
const MAX_H = 560
const baseW = computed(() => {
  const h = viewW.value * natural.value.h / natural.value.w
  return h > MAX_H ? MAX_H * natural.value.w / natural.value.h : viewW.value
})
const baseH = computed(() => baseW.value * natural.value.h / natural.value.w)
function onImageLoad(e: Event) {
  const img = e.target as HTMLImageElement
  if (img.naturalWidth && img.naturalHeight) natural.value = { w: img.naturalWidth, h: img.naturalHeight }
  void nextTick(resetView)
}
let observer: ResizeObserver | null = null
function measure() { if (viewport.value) viewW.value = viewport.value.clientWidth || viewW.value }
watch(viewport, el => {
  observer?.disconnect()
  if (!el) return
  measure()
  if (typeof ResizeObserver !== 'undefined') { observer = new ResizeObserver(() => { const before = viewW.value; measure(); if (before !== viewW.value) resetView() }); observer.observe(el) }
})
onMounted(measure)
onBeforeUnmount(() => observer?.disconnect())

// ---------- 缩放和平移 ----------
const scale = ref(1)
const tx = ref(0)
const ty = ref(0)
function clampPan() {
  const w = baseW.value * scale.value, h = baseH.value * scale.value
  const vw = viewW.value, vh = baseH.value
  tx.value = w <= vw ? (vw - w) / 2 : Math.min(0, Math.max(vw - w, tx.value))
  ty.value = h <= vh ? (vh - h) / 2 : Math.min(0, Math.max(vh - h, ty.value))
}
function resetView() { scale.value = 1; tx.value = 0; ty.value = 0; clampPan() }
function zoomAt(factor: number, cx: number, cy: number) {
  const next = Math.min(8, Math.max(1, scale.value * factor))
  const k = next / scale.value
  tx.value = cx - (cx - tx.value) * k
  ty.value = cy - (cy - ty.value) * k
  scale.value = next
  clampPan()
}
const zoomBy = (factor: number) => zoomAt(factor, viewW.value / 2, baseH.value / 2)
function onWheel(e: WheelEvent) {
  // 只有点过地图（地图有焦点）或者按着 Ctrl 才用滚轮缩放，不然会抢了页面滚动
  if (document.activeElement !== viewport.value && !e.ctrlKey) return
  e.preventDefault()
  const rect = viewport.value!.getBoundingClientRect()
  zoomAt(e.deltaY < 0 ? 1.15 : 1 / 1.15, e.clientX - rect.left, e.clientY - rect.top)
}

// ---------- 拖动：平移 / 挪图钉 / 放图钉 ----------
const dragging = ref<'pan' | 'pin' | null>(null)
let start = { x: 0, y: 0, tx: 0, ty: 0 }
let moved = false
let dragPin: MapPin | null = null
const placing = ref(false)
const activePin = ref<string | null>(null)
const pickerOpen = ref(false)
watch(activePin, () => { pickerOpen.value = false })
const current = computed(() => pins.value.find(p => p.id === activePin.value) ?? null)

function toMap(e: PointerEvent) {
  const rect = layer.value!.getBoundingClientRect()
  return { x: Math.min(1, Math.max(0, (e.clientX - rect.left) / rect.width)), y: Math.min(1, Math.max(0, (e.clientY - rect.top) / rect.height)) }
}
function onDown(e: PointerEvent) {
  if (e.button !== 0) return
  viewport.value?.focus({ preventScroll: true })
  dragging.value = 'pan'
  moved = false
  start = { x: e.clientX, y: e.clientY, tx: tx.value, ty: ty.value }
  viewport.value?.setPointerCapture(e.pointerId)
}
function onPinDown(e: PointerEvent, pin: MapPin) {
  if (e.button !== 0) return
  moved = false
  start = { x: e.clientX, y: e.clientY, tx: tx.value, ty: ty.value }
  dragPin = pin
  dragging.value = 'pin'
  viewport.value?.setPointerCapture(e.pointerId)
}
function onMove(e: PointerEvent) {
  if (!dragging.value) return
  const dx = e.clientX - start.x, dy = e.clientY - start.y
  if (Math.abs(dx) + Math.abs(dy) > 4) moved = true
  if (!moved) return
  if (dragging.value === 'pan') { tx.value = start.tx + dx; ty.value = start.ty + dy; clampPan() }
  else if (dragPin && props.editable) Object.assign(dragPin, toMap(e))
}
function onUp(e: PointerEvent) {
  const kind = dragging.value
  dragging.value = null
  if (viewport.value?.hasPointerCapture(e.pointerId)) viewport.value.releasePointerCapture(e.pointerId)
  if (kind === 'pin' && dragPin) {
    const pin = dragPin
    dragPin = null
    if (moved && props.editable) emitPins()
    else activePin.value = activePin.value === pin.id ? null : pin.id
    return
  }
  if (kind === 'pan' && !moved) {
    if (placing.value && props.editable) {
      const pin: MapPin = { id: newId('p'), ...toMap(e) }
      pins.value.push(pin)
      emitPins()
      placing.value = false
      activePin.value = pin.id
    } else activePin.value = null
  }
}
function removePin(id: string) {
  pins.value = pins.value.filter(p => p.id !== id)
  activePin.value = null
  emitPins()
}
function focusPin(pin: MapPin) {
  activePin.value = pin.id
  if (scale.value < 2) scale.value = 2
  tx.value = viewW.value / 2 - pin.x * baseW.value * scale.value
  ty.value = baseH.value / 2 - pin.y * baseH.value * scale.value
  clampPan()
}

// 图钉卡片放在图钉旁边（不超出地图）
const popStyle = computed(() => {
  const pin = current.value
  if (!pin) return {}
  const px = tx.value + pin.x * baseW.value * scale.value
  const py = ty.value + pin.y * baseH.value * scale.value
  const w = Math.min(280, viewW.value - 16)
  const left = Math.min(Math.max(8, px + 14), viewW.value - w - 8)
  // 放在图钉上下空间大的一边；放不下就限制高度，卡片里面滚动
  const spaceBelow = baseH.value - py - 14, spaceAbove = py - 48
  const below = spaceBelow >= spaceAbove
  const maxHeight = Math.max(120, (below ? spaceBelow : spaceAbove) - 8) + 'px'
  return { left: left + 'px', width: w + 'px', maxHeight, ...(below ? { top: Math.max(8, py + 6) + 'px' } : { bottom: Math.max(8, baseH.value - py + 40) + 'px' }) }
})
const linkedCover = computed(() => {
  const card = cardOf(current.value?.cardId)
  return card?.coverImage ? withThumbnail(parseCoverImage(card.coverImage), 96) : ''
})

// ---------- 底图 ----------
const fileInput = ref<HTMLInputElement | null>(null)
function pickImage() { fileInput.value?.click() }
async function onFile(e: Event) {
  const input = e.target as HTMLInputElement
  const files = input.files
  if (!files?.length) return
  const [url] = await uploadGallery(files)
  input.value = ''
  if (url) emit('update:image', url)
}
</script>

<style scoped>
.map-block { display: grid; gap: 8px; }
.mb-empty { display: grid; justify-items: start; gap: 6px; padding: 18px; border: 1px dashed var(--border-line); background: var(--bg-sub); }
.mb-empty p { margin: 0; font-size: 12px; color: var(--text-secondary); }
.mb-upload { padding: 8px 16px; border: 1px dashed var(--accent-color); background: var(--accent-soft); color: var(--accent-color); font: inherit; font-size: 13px; cursor: pointer; }
.mb-tools { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; font-size: 12px; }
.mb-tools button { min-width: 28px; height: 28px; padding: 0 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.mb-tools button:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); }
.mb-tools .mb-place[aria-pressed=true] { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); }
.mb-zoom { min-width: 40px; color: var(--text-secondary); }
.mb-hint { margin-left: auto; color: var(--text-secondary); }
.mb-viewport { position: relative; overflow: hidden; background: var(--bg-sub); border: 1px solid var(--border-line); cursor: grab; touch-action: none; user-select: none; outline: none; }
.mb-viewport:focus-visible { border-color: var(--accent-color); }
.mb-viewport.grabbing { cursor: grabbing; }
.mb-viewport.placing { cursor: crosshair; }
.mb-layer { position: absolute; left: 0; top: 0; transform-origin: 0 0; }
.mb-layer img { width: 100%; height: 100%; display: block; pointer-events: none; }
.mb-pin { position: absolute; transform-origin: 50% 100%; width: 24px; height: 32px; padding: 0; border: 0; background: none; cursor: pointer; }
.editable .mb-pin { cursor: move; }
.mb-pin svg { width: 24px; height: 32px; display: block; filter: drop-shadow(0 2px 3px rgba(0, 0, 0, .35)); }
.mb-pin path { fill: var(--pin); stroke: #fff; stroke-width: 1.5; }
.mb-pin circle { fill: #fff; }
.mb-pin:not(.linked) circle { fill: #ffffffaa; }
.mb-pin.on svg { transform: scale(1.2); transform-origin: 50% 100%; }
.mb-pin-label { position: absolute; left: 50%; top: 100%; transform: translateX(-50%); margin-top: 2px; max-width: 140px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; padding: 1px 8px; border-radius: 999px; background: rgba(20, 24, 30, .78); color: #fff; font-size: 11px; pointer-events: none; }
.mb-pop { position: absolute; z-index: 5; overflow-y: auto; overscroll-behavior: contain; display: grid; gap: 8px; padding: 10px 12px; background: var(--paper-card); border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); box-shadow: 0 10px 28px rgba(0, 0, 0, .2); cursor: default; font-size: 13px; }
.mb-panel { position: static; max-width: 420px; box-shadow: none; overflow: visible; }
.mb-panel-tip { font-size: 11px; color: var(--text-secondary); }
.mb-pop-head { display: flex; align-items: center; gap: 8px; }
.mb-pop-head input { flex: 1; min-width: 0; border: 0; border-bottom: 1px solid var(--border-line); background: none; color: var(--text-primary); font: inherit; font-weight: 600; padding: 2px 0; outline: none; }
.mb-pop-head .x { margin-left: auto; width: 24px; height: 24px; border: 0; background: none; color: var(--text-secondary); font-size: 16px; cursor: pointer; }
.mb-cover { width: 40px; height: 40px; object-fit: cover; }
.mb-pop-title { display: grid; min-width: 0; }
.mb-pop-title b { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.mb-pop-title small { font-size: 11px; color: var(--accent-color); }
.mb-colors { display: flex; gap: 6px; }
.mb-colors button { width: 20px; height: 20px; border: 2px solid transparent; border-radius: 50%; cursor: pointer; padding: 0; }
.mb-colors button[aria-checked=true] { border-color: var(--text-primary); }
.mb-link { position: relative; display: flex; flex-wrap: wrap; align-items: center; gap: 6px; font-size: 12px; color: var(--text-secondary); }
.mb-link button { padding: 3px 8px; border: 1px dashed var(--border-line); background: none; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.mb-link button:hover { color: var(--accent-color); border-color: var(--accent-color); }
.mb-picker { position: absolute; left: 0; top: calc(100% + 4px); z-index: 10; }
.mb-pop textarea { width: 100%; box-sizing: border-box; border: 1px solid var(--border-line); background: var(--bg-sub); color: var(--text-primary); font: inherit; font-size: 12px; padding: 6px 8px; resize: vertical; }
.mb-del { justify-self: start; border: 0; background: none; color: var(--danger); font: inherit; font-size: 12px; cursor: pointer; padding: 0; }
.mb-note { margin: 0; white-space: pre-wrap; line-height: 1.6; overflow-wrap: anywhere; }
.mb-open { justify-self: start; border: 0; background: none; color: var(--accent-color); font: inherit; font-size: 12px; cursor: pointer; padding: 0; }
.mb-legend { display: flex; flex-wrap: wrap; gap: 6px; }
.mb-legend button { display: inline-flex; align-items: center; gap: 6px; padding: 3px 10px; border: 1px solid var(--border-line); border-radius: 999px; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.mb-legend button.on { border-color: var(--accent-color); color: var(--accent-color); }
.mb-legend small { font-size: 10px; color: var(--text-secondary); }
.mb-rel { flex: 1 1 100%; min-width: 0; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 12px; padding: 4px 8px; }
.mb-rel-quick { display: flex; flex-wrap: wrap; gap: 4px; }
.mb-rel-quick button { padding: 1px 6px !important; border-style: solid !important; font-size: 11px !important; }
.mb-rel-tag { flex-shrink: 0; padding: 1px 8px; font-size: 11px; color: #fff; background: var(--pin); }
.mb-legend i { width: 8px; height: 8px; border-radius: 50%; }
@media (max-width: 640px) { .mb-hint { display: none; } }
</style>
