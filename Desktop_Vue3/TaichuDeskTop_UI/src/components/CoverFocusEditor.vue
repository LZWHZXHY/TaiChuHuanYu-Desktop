<!-- 调整封面显示位置：在原图上点 / 拖一个焦点，下面实时预览「世界观列表卡片」和「世界观页面顶部」两种裁切效果。
     焦点存在图片地址末尾（见 utils/coverFocus.ts），不改数据库。 -->
<template>
  <Teleport to="body">
    <div v-if="open" class="cfe-mask" @click.self="emit('close')">
      <section class="cfe" role="dialog" aria-modal="true" aria-labelledby="cfe-title">
        <header class="cfe-head">
          <span class="cfe-eyebrow">COVER / FOCUS</span>
          <h2 id="cfe-title">调整封面显示位置</h2>
          <p>在图片上点一下或拖动十字，选出最想让人看到的那一块。列表卡片和世界观页面顶部会以这里为中心裁切。</p>
          <button type="button" class="cfe-close" aria-label="关闭" @click="emit('close')">×</button>
        </header>

        <div class="cfe-body">
          <div
            ref="stage"
            class="cfe-stage"
            tabindex="0"
            aria-label="封面焦点位置，可用方向键微调"
            @pointerdown="onDown"
            @keydown="onKey"
          >
            <div class="cfe-frame">
              <img :src="src" alt="封面原图" draggable="false" @load="loaded = true" />
              <span v-if="loaded" class="cfe-dot" :style="{ left: x + '%', top: y + '%' }" aria-hidden="true"></span>
              <span v-if="loaded" class="cfe-line h" :style="{ top: y + '%' }" aria-hidden="true"></span>
              <span v-if="loaded" class="cfe-line v" :style="{ left: x + '%' }" aria-hidden="true"></span>
            </div>
          </div>

          <div class="cfe-previews">
            <figure>
              <div class="cfe-pv card"><img :src="src" alt="" :style="pos" /></div>
              <figcaption>世界观列表里的卡片</figcaption>
            </figure>
            <figure>
              <div class="cfe-pv hero"><img :src="src" alt="" :style="pos" /></div>
              <figcaption>进入世界观后顶部的横幅</figcaption>
            </figure>
            <p class="cfe-pos">焦点：横向 {{ x }}% · 纵向 {{ y }}%</p>
          </div>
        </div>

        <footer class="cfe-foot">
          <button type="button" class="cfe-btn" @click="reset">恢复居中</button>
          <span class="cfe-grow"></span>
          <button type="button" class="cfe-btn" @click="emit('close')">取消</button>
          <button type="button" class="cfe-btn primary" :disabled="saving" @click="save">{{ saving ? '保存中…' : '保存' }}</button>
        </footer>
      </section>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { parseCover, withFocus } from '@/utils/coverFocus'

const props = defineProps<{ open: boolean; url: string; saving?: boolean }>()
const emit = defineEmits<{ (e: 'close'): void; (e: 'save', url: string): void }>()

const stage = ref<HTMLElement | null>(null)
const x = ref(50)
const y = ref(50)
const loaded = ref(false)
const src = computed(() => parseCover(props.url).src)
const pos = computed(() => ({ objectPosition: `${x.value}% ${y.value}%` }))

watch(
  () => [props.open, props.url] as const,
  ([open]) => {
    if (!open) return
    const c = parseCover(props.url)
    x.value = c.x
    y.value = c.y
    loaded.value = false
  },
  { immediate: true },
)

function setFrom(ev: PointerEvent) {
  const img = stage.value?.querySelector('img')
  if (!img) return
  const r = img.getBoundingClientRect()
  if (!r.width || !r.height) return
  x.value = Math.round(Math.min(100, Math.max(0, ((ev.clientX - r.left) / r.width) * 100)))
  y.value = Math.round(Math.min(100, Math.max(0, ((ev.clientY - r.top) / r.height) * 100)))
}
function onMove(ev: PointerEvent) {
  setFrom(ev)
}
function onUp() {
  window.removeEventListener('pointermove', onMove)
  window.removeEventListener('pointerup', onUp)
}
function onDown(ev: PointerEvent) {
  ev.preventDefault()
  stage.value?.focus()
  setFrom(ev)
  window.addEventListener('pointermove', onMove)
  window.addEventListener('pointerup', onUp)
}
function onKey(ev: KeyboardEvent) {
  const step = ev.shiftKey ? 10 : 2
  const moves: Record<string, [number, number]> = { ArrowLeft: [-step, 0], ArrowRight: [step, 0], ArrowUp: [0, -step], ArrowDown: [0, step] }
  const m = moves[ev.key]
  if (!m) {
    if (ev.key === 'Escape') emit('close')
    return
  }
  ev.preventDefault()
  x.value = Math.min(100, Math.max(0, x.value + m[0]))
  y.value = Math.min(100, Math.max(0, y.value + m[1]))
}
function reset() {
  x.value = 50
  y.value = 50
}
function save() {
  emit('save', withFocus(props.url, x.value, y.value))
}
onBeforeUnmount(onUp)
</script>

<style scoped>
.cfe-mask { position: fixed; inset: 0; z-index: 3000; display: grid; place-items: center; padding: 16px; background: var(--overlay, rgb(12 18 24 / 45%)); }
.cfe {
  width: min(980px, 100%); max-height: calc(100vh - 32px); overflow: auto;
  background: var(--paper-card); color: var(--text-primary);
  border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); border-radius: 0;
  box-shadow: var(--shadow-md);
}
.cfe-head { position: relative; padding: 18px 56px 14px 22px; border-bottom: 1px solid var(--border-line); }
.cfe-eyebrow {
  display: inline-block; padding: 3px 9px; font: 10px/1.5 var(--font-mono, monospace); letter-spacing: .12em;
  background: var(--inverse-bg); color: var(--inverse-text); border-left: 2px solid var(--signal-mint);
}
.cfe-head h2 { margin: 8px 0 4px; font-family: var(--font-display, inherit); font-size: 20px; }
.cfe-head p { margin: 0; font-size: 13px; color: var(--text-secondary); line-height: 1.6; }
.cfe-close { position: absolute; right: 14px; top: 14px; width: 34px; height: 34px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font-size: 20px; line-height: 1; cursor: pointer; border-radius: 0; }
.cfe-body { display: grid; grid-template-columns: minmax(0, 1.25fr) minmax(0, 1fr); gap: 20px; padding: 18px 22px; }
.cfe-stage {
  position: relative; display: grid; place-items: center; min-height: 240px; max-height: 62vh; overflow: hidden;
  background: repeating-conic-gradient(var(--bg-sub) 0 25%, var(--paper-card) 0 50%) 0 0 / 16px 16px;
  border: 1px solid var(--border-line); cursor: crosshair; touch-action: none; user-select: none;
}
.cfe-stage:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 2px; }
.cfe-stage img { display: block; max-width: 100%; max-height: 62vh; object-fit: contain; pointer-events: none; }
/* 十字和焦点按图片本身的尺寸定位（图片外面包一层和图片一样大的框） */
.cfe-stage { width: 100%; }
.cfe-frame { position: relative; display: inline-block; line-height: 0; max-width: 100%; }
.cfe-dot, .cfe-line { position: absolute; pointer-events: none; }
.cfe-dot { width: 18px; height: 18px; margin: -9px 0 0 -9px; border: 2px solid #fff; background: var(--accent-color); box-shadow: 0 0 0 2px var(--accent-color), 0 2px 8px rgb(0 0 0 / 35%); }
.cfe-line { background: var(--signal-color); opacity: .7; }
.cfe-line.h { left: 0; right: 0; height: 1px; }
.cfe-line.v { top: 0; bottom: 0; width: 1px; }
.cfe-previews { display: flex; flex-direction: column; gap: 14px; }
.cfe-previews figure { margin: 0; }
.cfe-pv { overflow: hidden; background: var(--bg-sub); border: 1px solid var(--border-line); }
.cfe-pv.card { aspect-ratio: 16 / 9; }
.cfe-pv.hero { aspect-ratio: 21 / 7; }
.cfe-pv img { width: 100%; height: 100%; object-fit: cover; display: block; }
.cfe-previews figcaption { margin-top: 6px; font-size: 12px; color: var(--text-secondary); }
.cfe-pos { margin: 0; font: 12px var(--font-mono, monospace); color: var(--text-secondary); }
.cfe-foot { display: flex; align-items: center; gap: 10px; padding: 14px 22px; border-top: 1px solid var(--border-line); }
.cfe-grow { flex: 1; }
.cfe-btn { padding: 7px 16px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); border-radius: 0; cursor: pointer; font-size: 13px; }
.cfe-btn:not(:disabled):hover { border-color: var(--accent-color); color: var(--accent-color); }
.cfe-btn.primary {
  background: var(--accent-color); border-color: var(--accent-color); color: var(--on-accent);
  clip-path: polygon(0 0, calc(100% - 10px) 0, 100% 10px, 100% 100%, 10px 100%, 0 calc(100% - 10px));
}
.cfe-btn.primary:not(:disabled):hover { color: var(--on-accent); box-shadow: inset 0 -3px 0 var(--signal-mint); }
.cfe-btn:disabled { opacity: .5; cursor: not-allowed; }
@media (max-width: 760px) {
  .cfe-body { grid-template-columns: 1fr; }
  .cfe-stage { max-height: 50vh; }
  .cfe-stage img { max-height: 50vh; }
}
</style>
