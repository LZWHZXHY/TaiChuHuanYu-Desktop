/**
 * 全站界面音效（莱茵生命实验室风格）：全部用 Web Audio 实时合成，没有音频文件。
 * 音色思路：干净的正弦 / 三角波 + 短促的滤波噪声，像实验仪器的提示音，音量压得很低。
 *
 * 用法：
 * - 在 App.vue 里调用一次 setupUiSound(router)，会自动给按钮点击、页面切换、弹窗、报错加上声音；
 * - 其他地方需要时直接 uiSound.confirm() / uiSound.notify() ……；
 * - 某块区域有自己的音效（比如斗地主牌桌），给它的根元素加 data-no-ui-sound 属性即可跳过；
 *   只想去掉悬停音（比如棋盘的每个交叉点）就加 data-no-hover-sound。
 */
import { computed, ref, watch } from 'vue'
import type { Router } from 'vue-router'
import { bus } from './bus'

const KEY = 'taichu.uiSoundLevel'
const OLD_KEY = 'taichu.uiSound'
/** 音量档位：0 关 / 1 轻 / 2 标准 / 3 强 */
export type SoundLevel = 0 | 1 | 2 | 3
export const SOUND_LEVEL_NAMES = ['关', '轻', '标准', '强'] as const
const LEVEL_GAIN = [0, 0.5, 0.95, 1.6]
function readLevel(): SoundLevel {
  try {
    const v = localStorage.getItem(KEY)
    if (v !== null) return Math.min(3, Math.max(0, Number(v) || 0)) as SoundLevel
    if (localStorage.getItem(OLD_KEY) === '0') return 0
  } catch { /* 忽略 */ }
  return 2
}
/** 界面音效音量档位（默认「标准」） */
export const uiSoundLevel = ref<SoundLevel>(readLevel())
export const uiSoundOn = computed(() => uiSoundLevel.value > 0)
watch(() => uiSoundLevel.value, v => {
  try { localStorage.setItem(KEY, String(v)) } catch { /* 忽略 */ }
  if (master) master.gain.value = LEVEL_GAIN[v] ?? 0
})
/** 循环切换音量档位：关 → 轻 → 标准 → 强 → 关 */
export function cycleUiSoundLevel() {
  uiSoundLevel.value = ((uiSoundLevel.value + 1) % 4) as SoundLevel
  if (uiSoundLevel.value > 0) uiSound.confirm()
}

let ctx: AudioContext | null = null
let master: GainNode | null = null
/** 浏览器要求用户先和页面互动过才能出声；在那之前静默 */
const activated = () => (navigator as Navigator & { userActivation?: { hasBeenActive: boolean } }).userActivation?.hasBeenActive ?? true

function audio() {
  if (!uiSoundOn.value || !activated() || document.hidden) return null
  if (!ctx) {
    const AC = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
    if (!AC) return null
    ctx = new AC()
    master = ctx.createGain()
    master.gain.value = LEVEL_GAIN[uiSoundLevel.value] ?? 0
    // 轻微的高频滚降，让声音更「干净」不刺耳
    const soft = ctx.createBiquadFilter()
    soft.type = 'lowpass'
    soft.frequency.value = 7000
    master.connect(soft).connect(ctx.destination)
  }
  if (ctx.state === 'suspended') void ctx.resume()
  return ctx
}

let noiseBuf: AudioBuffer | null = null
function noiseSource(c: AudioContext) {
  if (!noiseBuf) {
    noiseBuf = c.createBuffer(1, c.sampleRate * 0.5, c.sampleRate)
    const d = noiseBuf.getChannelData(0)
    for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1
  }
  const s = c.createBufferSource()
  s.buffer = noiseBuf
  return s
}

function tone(at: number, dur: number, from: number, to: number, gain: number, type: OscillatorType = 'sine') {
  const c = audio(); if (!c || !master) return
  const t = c.currentTime + at
  const o = c.createOscillator(); o.type = type
  o.frequency.setValueAtTime(from, t)
  if (to !== from) o.frequency.exponentialRampToValueAtTime(Math.max(1, to), t + dur)
  const g = c.createGain()
  g.gain.setValueAtTime(0.0001, t)
  g.gain.exponentialRampToValueAtTime(gain, t + 0.006)
  g.gain.exponentialRampToValueAtTime(0.0001, t + dur)
  o.connect(g).connect(master)
  o.start(t); o.stop(t + dur + 0.02)
}

function hiss(at: number, dur: number, from: number, to: number, gain: number, type: BiquadFilterType = 'bandpass', q = 1.2) {
  const c = audio(); if (!c || !master) return
  const t = c.currentTime + at
  const s = noiseSource(c)
  const f = c.createBiquadFilter(); f.type = type; f.Q.value = q
  f.frequency.setValueAtTime(from, t)
  if (to !== from) f.frequency.exponentialRampToValueAtTime(Math.max(20, to), t + dur)
  const g = c.createGain()
  g.gain.setValueAtTime(0.0001, t)
  g.gain.exponentialRampToValueAtTime(gain, t + Math.min(0.03, dur / 3))
  g.gain.exponentialRampToValueAtTime(0.0001, t + dur)
  s.connect(f).connect(g).connect(master)
  s.start(t, Math.random() * 0.2); s.stop(t + dur + 0.02)
}

// 同一种声音短时间内只响一次，避免连点时糊成一片
const last: Record<string, number> = {}
function gate(name: string, ms: number) {
  const now = performance.now()
  if (now - (last[name] ?? 0) < ms) return false
  last[name] = now
  return true
}

export const uiSound = {
  /** 鼠标移到可点的东西上：极短的「咔」 */
  hover() { if (!gate('hover', 28)) return; hiss(0, 0.016, 5400, 5400, 0.3, 'bandpass', 4); tone(0, 0.018, 3200, 2700, 0.035, 'triangle') },
  /** 按钮：很轻的机械「嗒」 */
  tap() { if (!gate('tap', 45)) return; hiss(0, 0.025, 4200, 4200, 0.35, 'bandpass', 3); tone(0, 0.03, 2400, 1900, 0.05, 'triangle') },
  /** 开关 / 复选框 */
  toggle(on = true) { if (!gate('toggle', 60)) return; tone(0, 0.05, on ? 1320 : 990, on ? 1760 : 760, 0.08, 'triangle'); hiss(0, 0.02, 3800, 3800, 0.2, 'bandpass', 3) },
  /** 切换页面：「嗒」+ 一道上扫的扫描声 */
  nav() { if (!gate('nav', 150)) return; hiss(0, 0.022, 3600, 3600, 0.3, 'bandpass', 3); hiss(0.02, 0.22, 900, 5200, 0.12, 'bandpass', 4); tone(0.03, 0.14, 1600, 2400, 0.025) },
  /** 成功 / 确认：两声上行 */
  confirm() { if (!gate('confirm', 200)) return; tone(0, 0.11, 988, 988, 0.12, 'triangle'); tone(0.09, 0.2, 1480, 1480, 0.11, 'triangle'); tone(0.09, 0.2, 2960, 2960, 0.02) },
  /** 出错：低沉的两下 */
  error() { if (!gate('error', 400)) return; tone(0, 0.14, 220, 196, 0.16, 'square'); tone(0.16, 0.2, 196, 165, 0.14, 'square') },
  /** 新通知：柔和的铃声 */
  notify() { if (!gate('notify', 1500)) return; [1318, 1760, 2637].forEach((f, i) => tone(i * 0.09, 0.5 - i * 0.08, f, f, 0.07 - i * 0.015)) },
  /** 弹窗打开：一阵轻微的气流 */
  open() { if (!gate('open', 150)) return; hiss(0, 0.18, 600, 3200, 0.12, 'bandpass', 0.8) },
  /** 弹窗关闭 */
  close() { if (!gate('close', 150)) return; hiss(0, 0.14, 3000, 700, 0.09, 'bandpass', 0.8) },
  /** 将军：短促的两声高音提示 */
  check() { if (!gate('check', 300)) return; tone(0, 0.09, 1568, 1568, 0.12, 'square'); tone(0.11, 0.14, 2093, 2093, 0.1, 'square') },
  /** 落子（棋类） */
  stone() { if (!gate('stone', 40)) return; hiss(0, 0.05, 2600, 1800, 0.45, 'bandpass', 1.6); tone(0, 0.06, 720, 520, 0.08, 'triangle') },
}

// ===================== 全局接线 =====================

const CLICKABLE = 'button, a[href], [role="tab"], [role="radio"], [role="menuitem"], summary, select'
const HOVERABLE = `${CLICKABLE}, input[type="checkbox"], input[type="radio"], label[for], [role="button"], [role="gridcell"]`

/** 找到鼠标所在的「可点击的一块」：从里往外，取最外层仍然可交互 / 是小手指针的元素 */
function hoverTarget(start: Element): Element | null {
  let found: Element | null = null
  let el: Element | null = start
  for (let depth = 0; el && el !== document.body && depth < 10; depth++, el = el.parentElement) {
    if (el.matches(HOVERABLE) || getComputedStyle(el).cursor === 'pointer') found = el
    else if (found) break
  }
  return found
}
const isDialog = (el: Element) => el.matches('[role="dialog"], [aria-modal="true"], .el-overlay, .el-dialog__wrapper') || !!el.querySelector(':scope > [role="dialog"], :scope > [aria-modal="true"]')

let started = false
/** 在 App.vue 里调用一次 */
export function setupUiSound(router: Router) {
  if (started || typeof window === 'undefined') return
  started = true

  // 点击：按钮、链接、标签页……（开关和复选框单独处理）
  document.addEventListener('click', e => {
    const target = e.target as Element | null
    if (!target || target.closest('[data-no-ui-sound]')) return
    const input = target.closest('input[type="checkbox"], input[type="radio"]') as HTMLInputElement | null
    if (input) { if (!input.disabled) uiSound.toggle(input.checked); return }
    const el = target.closest(CLICKABLE) as HTMLButtonElement | null
    if (!el || el.matches(':disabled, [aria-disabled="true"]')) return
    // 站内链接会触发页面切换音，这里不再重复响
    if (el.matches('a[href]') && !el.matches('[target="_blank"]') && (el as unknown as HTMLAnchorElement).origin === location.origin) return
    // 带粒子爆发效果的主要操作（签到、领取、发布……）用确认音
    if (el.hasAttribute('data-burst')) uiSound.confirm()
    else uiSound.tap()
  }, true)

  // 接口报错 / 成功提示（GlobalNotify 用的同一个事件）
  bus.on('api-error', e => { if (e.type && e.type !== 'error') uiSound.confirm(); else uiSound.error() })

  // 悬停：鼠标移到可点的东西上（按钮、链接、画廊图片、卡片……）时「咔」一声。
  // 判断依据是元素本身可交互，或者鼠标指针是小手；同一块区域里移动不会重复响。
  let hoverItem: Element | null = null
  document.addEventListener('pointerover', e => {
    if (e.pointerType && e.pointerType !== 'mouse') return
    const t = e.target
    if (!(t instanceof Element) || t.closest('[data-no-ui-sound], [data-no-hover-sound]')) { hoverItem = null; return }
    const item = hoverTarget(t)
    if (item === hoverItem) return
    hoverItem = item
    if (item && !item.matches(':disabled, [aria-disabled="true"]')) uiSound.hover()
  }, { passive: true })

  // 页面切换（首次进入不响）
  let first = true
  router.afterEach((to, from) => {
    if (first) { first = false; return }
    if (to.path !== from.path) uiSound.nav()
  })

  // 弹窗：直接挂在 body 下的对话框（Teleport、Element Plus 的弹层）
  const seen = new WeakSet<Element>()
  new MutationObserver(records => {
    for (const r of records) {
      r.addedNodes.forEach(n => { if (n instanceof Element && isDialog(n)) { seen.add(n); uiSound.open() } })
      r.removedNodes.forEach(n => { if (n instanceof Element && seen.has(n)) uiSound.close() })
    }
  }).observe(document.body, { childList: true })
}
