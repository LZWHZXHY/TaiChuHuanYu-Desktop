<!-- ReplayPlayer.vue：茶绘绘画过程回放。按落笔顺序把最终留在画面上的笔画一笔笔画出来，可以调速度、拖进度，
     还能导出下载：MP4（手机、微信都能看）/ WebM / GIF 动图（体积小，方便发群），浏览器不支持的格式会灰掉。 -->
<template>
  <div class="replay-mask" role="dialog" aria-modal="true" aria-label="绘画过程回放" @click.self="close">
    <div class="replay">
      <header class="replay-head">
        <div>
          <p class="eyebrow">REPLAY / 绘画过程</p>
          <h2>{{ title }}</h2>
        </div>
        <button type="button" class="close" aria-label="关闭回放" :disabled="recording" @click="close">✕</button>
      </header>

      <div ref="frameEl" class="frame">
        <canvas ref="displayEl" class="display" :width="data?.width ?? 16" :height="data?.height ?? 9"></canvas>
        <p v-if="loading" class="frame-tip">正在取回放数据…</p>
        <p v-else-if="error" class="frame-tip err">{{ error }}</p>
        <p v-else-if="data && !total" class="frame-tip">这间画室还没有留下笔画。</p>
        <span v-if="painter && !loading" class="painter" :style="{ '--c': painter.color }"><i></i>{{ painter.name }}</span>
      </div>

      <div class="controls">
        <button type="button" class="play" :disabled="!total || recording" :aria-label="playing ? '暂停' : '播放'" @click="toggle">{{ playing ? '❚❚' : '▶' }}</button>
        <input class="seek" type="range" min="0" :max="total" :value="index" :disabled="!total || recording" aria-label="进度" @input="onSeek" />
        <span class="count">{{ index }} / {{ total }} 笔</span>
        <div class="speeds" role="radiogroup" aria-label="速度">
          <button v-for="s in SPEEDS" :key="s" type="button" role="radio" :aria-checked="speed === s" :class="{ on: speed === s }" :disabled="recording" @click="speed = s">{{ s }}×</button>
        </div>
        <div v-if="formats.some(f => f.ok)" class="export">
          <select v-model="format" :disabled="!total || recording" aria-label="导出格式">
            <option v-for="f in formats" :key="f.id" :value="f.id" :disabled="!f.ok">{{ f.label }}{{ f.ok ? '' : '（当前浏览器不支持）' }}</option>
          </select>
          <button type="button" class="rec" :disabled="!total || recording || !currentFormat?.ok" :title="exportTitle" @click="exportReplay">{{ recording ? `${format === 'gif' ? '生成中' : '录制中'}… ${progress}%` : '导出' }}</button>
        </div>
      </div>
      <p v-if="exportMsg" class="note" :class="{ err: exportErr }">{{ exportMsg }}</p>
      <p v-if="data?.truncated" class="note">这间画室画得太多了，回放只记录到了前面一部分。</p>
      <p v-else class="note">只回放最后还留在画面上的笔画（撤销掉的、被清空的不算），图层按现在的顺序叠放。</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import request from '@/utils/request'
import { normalizeStyle, renderStroke, type StrokeData } from './paintEngine'
import { onTipLoaded } from './brushApi'
import { ensurePatches, onPatchLoaded, usePatchRoom } from './patchCache'
import { buildPalette, GifWriter } from './gifEncoder'

interface ReplayStroke extends StrokeData { layerId: number; userId: string }
interface ReplayData {
  name: string; width: number; height: number; truncated: boolean
  layers: { id: number; name: string; opacity: number }[]
  painters: { userId: string; name: string }[]
  strokes: ReplayStroke[]
}

const props = defineProps<{ roomId: string; title: string; colors?: Record<string, string> }>()
const emit = defineEmits<{ close: [] }>()

const SPEEDS = [1, 2, 4, 8]
/** 1 倍速下整段回放大约多少秒（笔画很少时会更短） */
const BASE_SECONDS = 24

const frameEl = ref<HTMLElement | null>(null)
const displayEl = ref<HTMLCanvasElement | null>(null)
const data = ref<ReplayData | null>(null)
const loading = ref(true)
const error = ref('')
const index = ref(0)
const playing = ref(false)
const speed = ref(2)
const recording = ref(false)
const total = computed(() => data.value?.strokes.length ?? 0)
const canRecord = typeof window !== 'undefined' && typeof (window as any).MediaRecorder !== 'undefined' && typeof HTMLCanvasElement.prototype.captureStream === 'function'

// ===== 导出格式 =====
type ExportFormat = 'mp4' | 'webm' | 'gif'
const MIME: Record<'mp4' | 'webm', string[]> = {
  mp4: ['video/mp4;codecs=avc1.42E01E', 'video/mp4;codecs=avc1', 'video/mp4'],
  webm: ['video/webm;codecs=vp9', 'video/webm;codecs=vp8', 'video/webm'],
}
const pickMime = (f: 'mp4' | 'webm') => canRecord ? MIME[f].find(t => (window as any).MediaRecorder.isTypeSupported?.(t)) ?? '' : ''
const formats = [
  { id: 'mp4' as const, label: 'MP4 视频（手机、微信都能看）', ok: !!pickMime('mp4') },
  { id: 'webm' as const, label: 'WebM 视频（电脑浏览器、画质好）', ok: !!pickMime('webm') },
  { id: 'gif' as const, label: 'GIF 动图（体积小，适合发群）', ok: typeof document !== 'undefined' },
]
const FORMAT_KEY = 'taichu.paint.exportFormat'
const savedFormat = (() => { try { return localStorage.getItem(FORMAT_KEY) as ExportFormat | null } catch { return null } })()
const format = ref<ExportFormat>(formats.find(f => f.id === savedFormat && f.ok)?.id ?? formats.find(f => f.ok)?.id ?? 'gif')
watch(format, f => { try { localStorage.setItem(FORMAT_KEY, f) } catch { /* 隐私模式 */ } })
const currentFormat = computed(() => formats.find(f => f.id === format.value))
const progress = ref(0)
const exportMsg = ref(''), exportErr = ref(false)
const exportTitle = computed(() => format.value === 'gif'
  ? '在本机生成 GIF 动图（最长边 480 像素），不用等实时播放'
  : `从头播放一遍并录成视频（${recordSeconds.value} 秒左右）`)
/** 每秒画多少笔：至少 6 笔，笔画多时按 BASE_SECONDS 压缩 */
const perSecond = computed(() => Math.max(6, total.value / BASE_SECONDS) * speed.value)
const recordSeconds = computed(() => Math.max(3, Math.round(total.value / perSecond.value) + 2))
const painter = computed(() => {
  const s = data.value?.strokes[Math.max(0, index.value - 1)]
  if (!s || !index.value || index.value >= total.value) return null
  const name = data.value!.painters.find(p => p.userId.toLowerCase() === s.userId.toLowerCase())?.name ?? '画师'
  return { name, color: props.colors?.[s.userId.toLowerCase()] ?? '#2dd4bf' }
})

let layers = new Map<number, { canvas: HTMLCanvasElement; ctx: CanvasRenderingContext2D; opacity: number }>()
let scratch: CanvasRenderingContext2D | null = null
let raf = 0, last = 0, budget = 0

function setup(d: ReplayData) {
  layers = new Map()
  for (const l of d.layers) {
    const canvas = document.createElement('canvas')
    canvas.width = d.width; canvas.height = d.height
    layers.set(l.id, { canvas, ctx: canvas.getContext('2d')!, opacity: l.opacity })
  }
  const sc = document.createElement('canvas')
  sc.width = d.width; sc.height = d.height
  scratch = sc.getContext('2d')
}

function drawUpTo(n: number) {
  const d = data.value
  if (!d || !scratch) return
  if (n < index.value) {
    for (const l of layers.values()) l.ctx.clearRect(0, 0, d.width, d.height)
    index.value = 0
  }
  for (let i = index.value; i < n; i++) {
    const s = d.strokes[i]!
    const l = layers.get(s.layerId)
    if (l) renderStroke(l.ctx, s, scratch, d.width, d.height)
  }
  index.value = n
  compose()
}

function compose() {
  const d = data.value, c = displayEl.value?.getContext('2d')
  if (!d || !c) return
  c.globalAlpha = 1
  c.fillStyle = '#ffffff'
  c.fillRect(0, 0, d.width, d.height)
  for (const l of d.layers) {
    const layer = layers.get(l.id)
    if (!layer) continue
    c.globalAlpha = layer.opacity
    c.drawImage(layer.canvas, 0, 0)
  }
  c.globalAlpha = 1
}

function tick(t: number) {
  raf = 0
  if (!playing.value) return
  const dt = last ? Math.min(0.1, (t - last) / 1000) : 0
  last = t
  budget += dt * perSecond.value
  const steps = Math.floor(budget)
  if (steps > 0) {
    budget -= steps
    drawUpTo(Math.min(total.value, index.value + steps))
  }
  if (index.value >= total.value) { playing.value = false; onEnded?.(); return }
  raf = requestAnimationFrame(tick)
}

function play() {
  if (!total.value) return
  if (index.value >= total.value) drawUpTo(0)
  playing.value = true
  last = 0; budget = 0
  if (!raf) raf = requestAnimationFrame(tick)
}
function pause() { playing.value = false; cancelAnimationFrame(raf); raf = 0 }
function toggle() { playing.value ? pause() : play() }
function onSeek(e: Event) { pause(); drawUpTo(Number((e.target as HTMLInputElement).value)) }

// ===== 导出 =====
let onEnded: (() => void) | null = null

function download(blob: Blob, ext: string) {
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = `${props.title}-绘画过程.${ext}`
  a.click()
  window.setTimeout(() => URL.revokeObjectURL(a.href), 4000)
  exportErr.value = false
  exportMsg.value = `已导出 ${ext.toUpperCase()}（${blob.size >= 1048576 ? (blob.size / 1048576).toFixed(1) + 'MB' : Math.max(1, Math.round(blob.size / 1024)) + 'KB'}）`
}

function exportReplay() {
  exportMsg.value = ''
  if (format.value === 'gif') void exportGif()
  else void recordVideo(format.value)
}

/** 视频：实时播放一遍，用 MediaRecorder 录下画布 */
async function recordVideo(f: 'mp4' | 'webm') {
  const canvas = displayEl.value
  if (!canvas || !total.value || recording.value) return
  const MR = (window as any).MediaRecorder
  const mimeType = pickMime(f)
  if (!mimeType) { exportErr.value = true; exportMsg.value = '当前浏览器不支持导出这个格式，换一个试试'; return }
  pause()
  drawUpTo(0)
  const stream = canvas.captureStream(30)
  const rec = new MR(stream, { mimeType, videoBitsPerSecond: 6_000_000 })
  const chunks: Blob[] = []
  rec.ondataavailable = (e: BlobEvent) => { if (e.data.size) chunks.push(e.data) }
  rec.onstop = () => {
    recording.value = false
    onEnded = null
    const type = rec.mimeType || mimeType
    download(new Blob(chunks, { type }), type.includes('mp4') ? 'mp4' : 'webm')
  }
  recording.value = true
  progress.value = 0
  const stopWatch = watch(index, i => { progress.value = Math.round(i / Math.max(1, total.value) * 100) })
  rec.start(500)
  // 开头停半秒、结尾停一秒半，看得清成品
  await new Promise(r => window.setTimeout(r, 500))
  onEnded = () => { stopWatch(); window.setTimeout(() => rec.state !== 'inactive' && rec.stop(), 1500) }
  play()
}

/** GIF：不用实时播放，按回放节奏一帧帧算出来再编码（每秒 10 帧，最长边 480 像素） */
async function exportGif() {
  const d = data.value, display = displayEl.value
  if (!d || !display || !total.value || recording.value) return
  pause()
  recording.value = true
  progress.value = 0
  try {
    const FPS = 10
    const scale = Math.min(1, 480 / Math.max(d.width, d.height))
    const w = Math.max(1, Math.round(d.width * scale)), h = Math.max(1, Math.round(d.height * scale))
    const small = document.createElement('canvas')
    small.width = w; small.height = h
    const sctx = small.getContext('2d', { willReadFrequently: true })!
    const grab = () => { sctx.drawImage(display, 0, 0, w, h); return sctx.getImageData(0, 0, w, h).data }
    // 调色板取自成品（加上半程的画面，颜色更全）
    drawUpTo(0); drawUpTo(Math.floor(total.value / 2))
    const mid = grab()
    drawUpTo(total.value)
    const palette = buildPalette([grab(), mid])
    const gif = new GifWriter(w, h, palette)
    drawUpTo(0)
    gif.addFrame(grab(), 50) // 开头停半秒
    const perFrame = perSecond.value / FPS
    let shown = 0, acc = 0, frames = 0
    while (shown < total.value) {
      acc += perFrame
      const n = Math.min(total.value, Math.floor(acc))
      if (n > shown) { drawUpTo(n); shown = n }
      gif.addFrame(grab(), shown >= total.value ? 150 : 10) // 最后一帧停一秒半
      progress.value = Math.round(shown / total.value * 100)
      // 每隔几帧让出主线程，界面不卡
      if (++frames % 4 === 0) await new Promise(r => window.setTimeout(r, 0))
      if (!recording.value) return
    }
    download(gif.finish(), 'gif')
  } catch (e) {
    console.error('GIF 导出失败', e)
    exportErr.value = true
    exportMsg.value = 'GIF 生成失败，画面太大时可以试试导出视频'
  } finally {
    recording.value = false
  }
}

function close() { if (!recording.value) emit('close') }
function onKey(e: KeyboardEvent) {
  if (e.key === 'Escape') close()
  else if (e.code === 'Space' && !recording.value) { e.preventDefault(); toggle() }
}

watch(speed, () => { budget = 0 })
// 自制笔刷的笔尖图载入后，已经画过的部分重画一遍
const redraw = () => { const i = index.value; drawUpTo(0); drawUpTo(i) }
const offTip = onTipLoaded(redraw)
const offPatch = onPatchLoaded(redraw)

onMounted(async () => {
  window.addEventListener('keydown', onKey)
  try {
    const d = await request.get<ReplayData>(`/Paint/${props.roomId}/history`, { silentError: true })
    d.strokes = d.strokes.map(s => normalizeStyle(s))
    // 油漆桶等工具的结果图先取回来，回放时才不会缺
    usePatchRoom(props.roomId)
    await ensurePatches(props.roomId, d.strokes.filter(s => s.tool === 'patch' && s.patch).map(s => s.patch!))
    data.value = d
    setup(d)
    // 等画布按新尺寸建好再画
    await new Promise(r => requestAnimationFrame(r))
    compose()
    if (d.strokes.length) play()
  } catch (e: any) {
    error.value = e?.friendlyMessage || '没取到回放数据'
  } finally { loading.value = false }
})
onBeforeUnmount(() => { pause(); offTip(); offPatch(); window.removeEventListener('keydown', onKey) })
</script>

<style scoped>
.replay-mask { position: fixed; inset: 0; z-index: 4000; display: grid; place-items: center; padding: 20px; background: rgb(8 12 16 / 72%); }
.replay { display: grid; gap: 12px; width: min(1200px, 100%); max-height: 100%; padding: 16px 18px; border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); background: var(--paper-card); color: var(--text-primary); overflow: auto; }
.replay-head { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; }
.eyebrow { margin: 0 0 4px; font: 10px/1.2 var(--font-mono, monospace); letter-spacing: .16em; color: var(--accent-color); }
.replay-head h2 { margin: 0; font-size: 18px; }
.close { border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); width: 32px; height: 32px; cursor: pointer; }
.frame { position: relative; display: grid; place-items: center; min-height: 200px; padding: 12px; background: #d9dde0; }
.display { display: block; max-width: 100%; max-height: calc(100vh - 260px); width: auto; height: auto; background: #fff; box-shadow: 0 6px 24px rgb(0 0 0 / 22%); }
.frame-tip { position: absolute; margin: 0; padding: 6px 12px; background: var(--paper-card); font-size: 13px; color: var(--text-secondary); }
.frame-tip.err { color: var(--danger); }
.painter { position: absolute; left: 20px; top: 20px; display: inline-flex; align-items: center; gap: 6px; padding: 3px 10px; background: rgb(0 0 0 / 55%); color: #fff; font-size: 12px; }
.painter i { width: 8px; height: 8px; border-radius: 50%; background: var(--c); }
.controls { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; }
.play { width: 40px; height: 34px; border: 1px solid var(--accent-color); background: var(--accent-color); color: var(--on-accent, #fff); font-size: 14px; cursor: pointer; }
.seek { flex: 1; min-width: 160px; accent-color: var(--accent-color); }
.count { font: 12px var(--font-mono, monospace); color: var(--text-secondary); min-width: 8em; }
.speeds { display: inline-flex; border: 1px solid var(--border-line); }
.speeds button { padding: 6px 10px; border: 0; border-right: 1px solid var(--border-line); background: transparent; color: var(--text-secondary); font: 12px var(--font-mono, monospace); cursor: pointer; }
.speeds button:last-child { border-right: 0; }
.speeds button.on { background: var(--accent-color); color: var(--on-accent, #fff); }
.rec { padding: 7px 14px; border: 1px solid var(--danger, #dc2626); background: transparent; color: var(--danger, #dc2626); font: inherit; font-size: 13px; cursor: pointer; }
button:disabled { opacity: .5; cursor: not-allowed; }
.note { margin: 0; font-size: 12px; color: var(--text-secondary); }
.note.err { color: var(--danger, #dc2626); }
.export { display: flex; gap: 6px; align-items: center; }
.export select { max-width: 220px; padding: 6px 8px; border: 1px solid var(--border-line); background: var(--bg-main); color: var(--text-primary); font: inherit; font-size: 12px; }
.export select:disabled, .rec:disabled { opacity: .55; }
</style>
