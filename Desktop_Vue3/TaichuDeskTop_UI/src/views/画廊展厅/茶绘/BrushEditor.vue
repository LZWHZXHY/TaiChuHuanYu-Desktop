<!-- BrushEditor.vue：制作 / 编辑自己的笔刷。左边调参数（笔尖、间距散布、抖动、压感、渐细），右边试画板实时看效果；笔尖可以上传图片或现场画一个。 -->
<template>
  <div class="be-mask" role="dialog" aria-modal="true" :aria-label="brush ? '编辑笔刷' : '制作笔刷'" @click.self="close">
    <div class="be">
      <header class="be-head">
        <div>
          <p class="eyebrow">BRUSH LAB / 笔刷工坊</p>
          <h2>{{ brush ? `编辑「${brush.name}」` : '制作新笔刷' }}</h2>
        </div>
        <button type="button" class="x" aria-label="关闭" @click="close">✕</button>
      </header>

      <div class="be-body">
        <!-- 参数 -->
        <div class="params">
          <label class="field">笔刷名称<input v-model.trim="name" maxlength="20" placeholder="例如：水彩晕染" /></label>

          <fieldset>
            <legend>笔尖</legend>
            <div class="seg">
              <button type="button" :class="{ on: p.tip === 'round' }" @click="p.tip = 'round'">圆形</button>
              <button type="button" :class="{ on: p.tip === 'image' }" @click="p.tip = 'image'">图案</button>
            </div>
            <div v-if="p.tip === 'image'" class="tip-box">
              <canvas ref="tipPreviewEl" class="tip-preview" :width="TIP_SIZE" :height="TIP_SIZE" aria-label="笔尖预览"></canvas>
              <div class="tip-actions">
                <label class="btn-s">上传图片<input type="file" accept="image/*" hidden @change="onTipFile" /></label>
                <button type="button" class="btn-s" @click="startDrawTip">{{ drawingTip ? '画完了' : '画一个' }}</button>
                <button type="button" class="btn-s" :disabled="!sourceImage" title="黑底白图时用" @click="invertTip">反相</button>
                <small>白底黑图或透明底的图都行：越黑的地方墨越浓。图会缩成 128×128。</small>
              </div>
              <div v-if="drawingTip" class="tip-pad-wrap">
                <canvas ref="tipPadEl" class="tip-pad" :width="TIP_SIZE" :height="TIP_SIZE" @pointerdown="padDown" @pointermove="padMove" @pointerup="padUp" @pointercancel="padUp"></canvas>
                <div class="pad-tools">
                  <label class="mini-slider">笔粗 <input v-model.number="padSize" type="range" min="2" max="40" /></label>
                  <button type="button" class="btn-s" @click="padErase = !padErase">{{ padErase ? '橡皮中' : '画笔中' }}</button>
                  <button type="button" class="btn-s" @click="clearPad">清空</button>
                </div>
              </div>
            </div>
            <label v-if="p.tip === 'round'" class="slider">硬度<input v-model.number="p.hardness" type="range" min="0" max="1" step="0.05" /><b>{{ pct(p.hardness) }}</b></label>
            <label class="slider">扁圆<input v-model.number="p.roundness" type="range" min="0.05" max="1" step="0.05" /><b>{{ pct(p.roundness) }}</b></label>
            <label class="slider">角度<input v-model.number="p.angle" type="range" min="-180" max="180" step="5" /><b>{{ p.angle }}°</b></label>
            <label class="check"><input v-model="p.followDirection" type="checkbox" />笔尖跟着笔画方向转</label>
          </fieldset>

          <fieldset>
            <legend>排布</legend>
            <label class="slider" title="相邻两个印章之间的距离（笔尖大小的百分比）">间距<input v-model.number="p.spacing" type="range" min="0.02" max="3" step="0.01" /><b>{{ pct(p.spacing) }}</b></label>
            <label class="slider" title="印章偏离笔画的距离">散布<input v-model.number="p.scatter" type="range" min="0" max="3" step="0.05" /><b>{{ pct(p.scatter) }}</b></label>
            <label class="slider" title="每一步盖几个印章">数量<input v-model.number="p.count" type="range" min="1" max="6" step="1" /><b>{{ p.count }}</b></label>
            <label class="slider" title="每个印章的浓度，越低越需要叠涂">流量<input v-model.number="p.flow" type="range" min="0.02" max="1" step="0.02" /><b>{{ pct(p.flow) }}</b></label>
          </fieldset>

          <fieldset>
            <legend>随机抖动</legend>
            <label class="slider">大小<input v-model.number="p.sizeJitter" type="range" min="0" max="1" step="0.05" /><b>{{ pct(p.sizeJitter) }}</b></label>
            <label class="slider">浓度<input v-model.number="p.opacityJitter" type="range" min="0" max="1" step="0.05" /><b>{{ pct(p.opacityJitter) }}</b></label>
            <label class="slider">角度<input v-model.number="p.angleJitter" type="range" min="0" max="1" step="0.05" /><b>{{ pct(p.angleJitter) }}</b></label>
          </fieldset>

          <fieldset>
            <legend>压感与渐细</legend>
            <label class="check"><input v-model="p.pressureSize" type="checkbox" />压感控制粗细</label>
            <label v-if="p.pressureSize" class="slider" title="压感最轻时剩下多粗">最细<input v-model.number="p.minSize" type="range" min="0" max="1" step="0.05" /><b>{{ pct(p.minSize) }}</b></label>
            <label class="check"><input v-model="p.pressureOpacity" type="checkbox" />压感控制浓淡</label>
            <label class="slider" title="起笔多长一段从细变粗（像素）">起笔<input v-model.number="p.taperStart" type="range" min="0" max="400" step="10" /><b>{{ p.taperStart }}</b></label>
            <label class="slider" title="收笔多长一段从粗变细（像素）">收笔<input v-model.number="p.taperEnd" type="range" min="0" max="400" step="10" /><b>{{ p.taperEnd }}</b></label>
          </fieldset>

          <fieldset>
            <legend>默认值</legend>
            <label class="slider">大小<input v-model.number="p.size" type="range" min="1" :max="p.maxSize" step="1" /><b>{{ p.size }}</b></label>
            <label class="slider">不透明度<input v-model.number="p.opacity" type="range" min="0.05" max="1" step="0.05" /><b>{{ pct(p.opacity) }}</b></label>
            <label class="slider" title="画室里大小滑块的上限">最大尺寸<input v-model.number="p.maxSize" type="range" min="4" max="300" step="2" /><b>{{ p.maxSize }}</b></label>
          </fieldset>
        </div>

        <!-- 试画 -->
        <div class="test">
          <div class="test-head">
            <span>试画板 · 用鼠标或数位笔在下面画</span>
            <label class="test-color" title="试画颜色"><input v-model="testColor" type="color" /><i :style="{ background: testColor }"></i></label>
            <button type="button" class="btn-s" @click="clearTest">清空</button>
          </div>
          <canvas ref="testEl" class="test-pad" :width="TEST_W" :height="TEST_H" @pointerdown="testDown" @pointermove="testMove" @pointerup="testUp" @pointercancel="testUp"></canvas>
          <canvas ref="stripEl" class="strip" width="600" height="90" aria-label="笔刷效果示意"></canvas>
          <label class="check public"><input v-model="isPublic" type="checkbox" />公开到知识库「茶绘笔刷」，别人可以收藏使用</label>
          <p v-if="error" class="err" role="alert">{{ error }}</p>
          <div class="be-actions">
            <button v-if="brush" type="button" class="btn danger" :disabled="saving" @click="remove">删除</button>
            <span class="grow"></span>
            <button type="button" class="btn" :disabled="saving" @click="close">取消</button>
            <button v-if="brush" type="button" class="btn" :disabled="saving" @click="save(true)">另存为新笔刷</button>
            <button type="button" class="btn primary" :disabled="saving" @click="save(false)">{{ saving ? '保存中…' : '保存' }}</button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { DEFAULT_PARAMS, renderStroke, type BrushParams } from './paintEngine'
import { TIP_SIZE, brushApi, canvasToPng, drawBrushPreview, errorText, imageToMask, onTipLoaded, registerTip, tipMask, type BrushView } from './brushApi'

const props = defineProps<{ brush?: BrushView | null; color?: string }>()
const emit = defineEmits<{ close: []; saved: [brush: BrushView]; deleted: [id: string] }>()

const TEST_W = 640, TEST_H = 360
const pct = (v: number) => `${Math.round(v * 100)}%`

const name = ref(props.brush?.name ?? '')
const isPublic = ref(props.brush?.isPublic ?? false)
const p = reactive<BrushParams>({ ...DEFAULT_PARAMS, ...(props.brush?.params ?? {}) })
const testColor = ref(props.color || '#1b2128')
const saving = ref(false)
const error = ref('')

// ================= 笔尖图 =================
const tipPreviewEl = ref<HTMLCanvasElement | null>(null)
const tipPadEl = ref<HTMLCanvasElement | null>(null)
/** 本地新做的笔尖（还没上传）；保存时上传换成哈希 */
let localMask: HTMLCanvasElement | null = null
let localKey = ''
const sourceImage = ref<HTMLCanvasElement | ImageBitmap | null>(null)
let inverted = false
const drawingTip = ref(false)
const padSize = ref(10), padErase = ref(false)
let padLast: [number, number] | null = null

function useLocalMask(mask: HTMLCanvasElement) {
  localMask = mask
  localKey = `local-${Math.random().toString(36).slice(2)}`
  registerTip(localKey, mask)
  p.tip = 'image'
  p.tipHash = localKey
}
async function onTipFile(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  if (file.size > 8 * 1024 * 1024) { error.value = '图片太大了（最多 8MB）'; return }
  try {
    const img = await createImageBitmap(file)
    sourceImage.value = img
    inverted = false
    drawingTip.value = false
    useLocalMask(imageToMask(img))
  } catch { error.value = '这张图片读不出来，换一张试试' }
}
function invertTip() {
  if (!sourceImage.value) return
  inverted = !inverted
  useLocalMask(imageToMask(sourceImage.value, inverted))
}
async function startDrawTip() {
  if (drawingTip.value) { finishPad(); drawingTip.value = false; return }
  drawingTip.value = true
  await nextTick()
  const pad = tipPadEl.value
  if (!pad) return
  const x = pad.getContext('2d')!
  x.fillStyle = '#fff'
  x.fillRect(0, 0, TIP_SIZE, TIP_SIZE)
  // 有现成的笔尖就在它上面改
  const current = p.tip === 'image' && p.tipHash ? tipMask(p.tipHash) : null
  if (current) x.drawImage(current, 0, 0)
}
function capture(e: PointerEvent) { try { (e.target as HTMLElement).setPointerCapture(e.pointerId) } catch { /* 指针已经抬起时会报错，忽略 */ } }
function padPos(e: PointerEvent): [number, number] {
  const r = (e.target as HTMLCanvasElement).getBoundingClientRect()
  return [(e.clientX - r.left) / r.width * TIP_SIZE, (e.clientY - r.top) / r.height * TIP_SIZE]
}
function padDraw(a: [number, number], b: [number, number], pressure: number) {
  const x = tipPadEl.value?.getContext('2d')
  if (!x) return
  x.strokeStyle = padErase.value ? '#fff' : '#000'
  x.lineCap = 'round'
  x.lineWidth = padSize.value * (0.3 + 0.7 * pressure)
  x.beginPath(); x.moveTo(a[0], a[1]); x.lineTo(b[0], b[1]); x.stroke()
}
function padDown(e: PointerEvent) {
  capture(e)
  padLast = padPos(e)
  padDraw(padLast, padLast, e.pointerType === 'pen' ? e.pressure : 1)
}
function padMove(e: PointerEvent) {
  if (!padLast) return
  const pos = padPos(e)
  padDraw(padLast, pos, e.pointerType === 'pen' ? e.pressure : 1)
  padLast = pos
  finishPad()
}
function padUp() { if (padLast) finishPad(); padLast = null }
function clearPad() {
  const x = tipPadEl.value?.getContext('2d')
  if (!x) return
  x.fillStyle = '#fff'
  x.fillRect(0, 0, TIP_SIZE, TIP_SIZE)
  finishPad()
}
let padTimer = 0
/** 画板内容 → 笔尖蒙版（节流，画的时候也能看到效果） */
function finishPad() {
  window.clearTimeout(padTimer)
  padTimer = window.setTimeout(() => {
    const pad = tipPadEl.value
    if (!pad) return
    sourceImage.value = null
    useLocalMask(imageToMask(pad))
  }, 60)
}

function drawTipPreview() {
  const c = tipPreviewEl.value
  const x = c?.getContext('2d')
  if (!c || !x) return
  x.clearRect(0, 0, TIP_SIZE, TIP_SIZE)
  x.fillStyle = '#fff'
  x.fillRect(0, 0, TIP_SIZE, TIP_SIZE)
  const mask = p.tipHash ? tipMask(p.tipHash) : null
  if (mask) x.drawImage(mask, 0, 0)
}

// ================= 试画板 =================
const testEl = ref<HTMLCanvasElement | null>(null)
const stripEl = ref<HTMLCanvasElement | null>(null)
const strokes: { points: number[]; color: string }[] = []
let active: number[] | null = null
let scratch: CanvasRenderingContext2D | null = null

function testPos(e: PointerEvent) {
  const r = testEl.value!.getBoundingClientRect()
  return [Math.round((e.clientX - r.left) / r.width * TEST_W * 10), Math.round((e.clientY - r.top) / r.height * TEST_H * 10), Math.round((e.pointerType === 'pen' ? (e.pressure || 0.5) : 1) * 100)]
}
function testDown(e: PointerEvent) {
  capture(e)
  active = [...testPos(e)]
  strokes.push({ points: active, color: testColor.value })
  schedule()
}
function testMove(e: PointerEvent) {
  if (!active) return
  const evs = typeof e.getCoalescedEvents === 'function' ? e.getCoalescedEvents() : []
  for (const ev of evs.length ? evs : [e]) active.push(...testPos(ev))
  schedule()
}
function testUp() { active = null }
function clearTest() { strokes.length = 0; schedule() }

let frame = 0
function schedule() { if (!frame) frame = requestAnimationFrame(render) }
function render() {
  frame = 0
  const c = testEl.value?.getContext('2d')
  if (c) {
    if (!scratch) { const s = document.createElement('canvas'); s.width = TEST_W; s.height = TEST_H; scratch = s.getContext('2d') }
    c.clearRect(0, 0, TEST_W, TEST_H)
    for (const s of strokes) renderStroke(c, { tool: 'brush', color: s.color, size: p.size, opacity: p.opacity, brush: 'custom', hardness: 1, flags: 0, custom: { ...p }, points: s.points }, scratch!, TEST_W, TEST_H)
  }
  if (stripEl.value) drawBrushPreview(stripEl.value, { ...p }, testColor.value)
  drawTipPreview()
}
watch([p, testColor], schedule, { deep: true })
watch(() => p.tip, () => void nextTick(schedule))
const offTip = onTipLoaded(schedule)

// ================= 保存 =================
function check(): string | null {
  if (!name.value) return '给笔刷起个名字吧'
  if (p.tip === 'image' && !p.tipHash) return '图案笔尖还没有图：上传一张或者画一个'
  return null
}
async function save(asNew: boolean) {
  error.value = check() ?? ''
  if (error.value) return
  saving.value = true
  try {
    const params: BrushParams = { ...p }
    if (params.tip === 'image' && localMask && params.tipHash === localKey) {
      const { hash } = await brushApi.uploadTip(await canvasToPng(localMask))
      registerTip(hash, localMask)
      params.tipHash = hash
      p.tipHash = hash
      localMask = null
    }
    if (params.tip !== 'image') params.tipHash = null
    const data = { name: name.value, params, isPublic: isPublic.value }
    const saved = props.brush && !asNew ? await brushApi.update(props.brush.id, data) : await brushApi.create(data)
    emit('saved', saved)
  } catch (e) {
    error.value = errorText(e, '保存失败，请重试')
  } finally { saving.value = false }
}
async function remove() {
  if (!props.brush || !window.confirm(`删除笔刷「${props.brush.name}」？收藏了它的人也会用不了。已经画下去的笔画不受影响。`)) return
  saving.value = true
  try { await brushApi.remove(props.brush.id); emit('deleted', props.brush.id) } catch (e) { error.value = errorText(e, '删除失败') } finally { saving.value = false }
}
function close() { if (!saving.value) emit('close') }
function onKey(e: KeyboardEvent) { if (e.key === 'Escape') close() }

onMounted(() => {
  window.addEventListener('keydown', onKey)
  // 先在试画板上放一笔示意
  const pts: number[] = []
  for (let i = 0; i <= 60; i++) { const t = i / 60; pts.push(Math.round((60 + t * 520) * 10), Math.round((180 + Math.sin(t * Math.PI * 2) * 90) * 10), Math.round(Math.sin(t * Math.PI) * 100)) }
  strokes.push({ points: pts, color: testColor.value })
  schedule()
})
onBeforeUnmount(() => { offTip(); cancelAnimationFrame(frame); window.clearTimeout(padTimer); window.removeEventListener('keydown', onKey) })
</script>

<style scoped>
.be-mask { position: fixed; inset: 0; z-index: 4100; display: grid; place-items: center; padding: 16px; background: rgb(8 12 16 / 70%); }
.be { display: grid; grid-template-rows: auto minmax(0, 1fr); gap: 12px; width: min(1180px, 100%); max-height: 100%; padding: 16px 18px; border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); background: var(--paper-card); color: var(--text-primary); }
.be-head { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; }
.eyebrow { margin: 0 0 4px; font: 10px/1.2 var(--font-mono, monospace); letter-spacing: .16em; color: var(--accent-color); }
.be-head h2 { margin: 0; font-size: 18px; }
.x { width: 32px; height: 32px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); cursor: pointer; }
.be-body { display: grid; grid-template-columns: minmax(280px, 360px) minmax(0, 1fr); gap: 16px; min-height: 0; }
.params { display: grid; align-content: start; gap: 10px; overflow-y: auto; padding-right: 6px; min-height: 0; }
.field { display: grid; gap: 4px; font-size: 12px; color: var(--text-secondary); }
.field input { padding: 7px 9px; border: 1px solid var(--border-line); background: var(--bg-main); color: var(--text-primary); font: inherit; font-size: 14px; }
fieldset { display: grid; gap: 6px; margin: 0; padding: 8px 10px 10px; border: 1px solid var(--border-line); }
legend { padding: 0 4px; font: 10px var(--font-mono, monospace); letter-spacing: .12em; color: var(--accent-color); }
.slider { display: grid; grid-template-columns: 4.5em 1fr 3.4em; align-items: center; gap: 8px; font-size: 12px; color: var(--text-secondary); }
.slider input { width: 100%; accent-color: var(--accent-color); }
.slider b { font: 600 12px var(--font-mono, monospace); color: var(--text-primary); text-align: right; }
.check { display: inline-flex; align-items: center; gap: 6px; font-size: 12px; color: var(--text-secondary); cursor: pointer; }
.check input { accent-color: var(--accent-color); }
.seg { display: inline-flex; border: 1px solid var(--border-line); width: max-content; }
.seg button { padding: 5px 14px; border: 0; background: transparent; color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.seg button.on { background: var(--accent-color); color: var(--on-accent, #fff); }
.tip-box { display: grid; grid-template-columns: 88px 1fr; gap: 10px; align-items: start; }
.tip-preview { width: 88px; height: 88px; border: 1px solid var(--border-line); background: #fff; }
.tip-actions { display: flex; flex-wrap: wrap; gap: 6px; align-content: start; }
.tip-actions small { flex-basis: 100%; font-size: 11px; color: var(--text-secondary); line-height: 1.5; }
.tip-pad-wrap { grid-column: 1 / -1; display: grid; grid-template-columns: 192px 1fr; gap: 10px; align-items: start; }
.tip-pad { width: 192px; height: 192px; border: 1px solid var(--accent-color); background: #fff; touch-action: none; cursor: crosshair; }
.pad-tools { display: grid; gap: 6px; align-content: start; }
.mini-slider { display: grid; gap: 2px; font-size: 11px; color: var(--text-secondary); }
.mini-slider input { accent-color: var(--accent-color); }
.btn-s { display: inline-flex; align-items: center; padding: 4px 10px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.btn-s:disabled { opacity: .45; cursor: not-allowed; }
.test { display: grid; align-content: start; gap: 10px; min-width: 0; }
.test-head { display: flex; align-items: center; gap: 10px; font-size: 12px; color: var(--text-secondary); }
.test-head span { flex: 1; }
.test-color { position: relative; width: 24px; height: 24px; cursor: pointer; }
.test-color input { position: absolute; inset: 0; width: 100%; height: 100%; opacity: 0; padding: 0; border: 0; cursor: pointer; }
.test-color i { display: block; width: 100%; height: 100%; box-shadow: 0 0 0 1px var(--text-primary); }
.test-pad { width: 100%; height: auto; aspect-ratio: 640 / 360; background: #fff; border: 1px solid var(--border-line); touch-action: none; cursor: crosshair; }
.strip { width: 100%; height: auto; aspect-ratio: 600 / 90; background: #fff; border: 1px dashed var(--border-line); }
.public { padding-top: 4px; }
.err { margin: 0; color: var(--danger); font-size: 13px; }
.be-actions { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; padding-top: 10px; border-top: 1px solid var(--border-line); }
.grow { flex: 1; }
.btn { padding: 7px 14px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.btn.primary { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent, #fff); font-weight: 600; }
.btn.danger { color: var(--danger); }
.btn:disabled { opacity: .5; cursor: not-allowed; }
@media (max-width: 860px) {
  .be { overflow-y: auto; }
  .be-body { grid-template-columns: 1fr; }
  .params { overflow: visible; }
}
</style>
