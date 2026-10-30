/**
 * 斗地主音效与语音报牌。
 * 音效全部用 Web Audio 实时合成（没有音频文件，也就没有版权和加载问题）；
 * 语音报牌用浏览器自带的中文语音（speechSynthesis），音色取决于用户的系统。
 * 两个开关分别记在本地，刷新后保留。
 */
import { ref, watch } from 'vue'

const SFX_KEY = 'taichu.ddz.sfx'
const VOICE_KEY = 'taichu.ddz.voice'
const read = (k: string, d: boolean) => { try { const v = localStorage.getItem(k); return v === null ? d : v === '1' } catch { return d } }
const write = (k: string, v: boolean) => { try { localStorage.setItem(k, v ? '1' : '0') } catch { /* 忽略 */ } }

export const sfxOn = ref(read(SFX_KEY, true))
export const voiceOn = ref(read(VOICE_KEY, false))
watch(() => sfxOn.value, v => write(SFX_KEY, v))
watch(() => voiceOn.value, v => { write(VOICE_KEY, v); if (!v) window.speechSynthesis?.cancel() })

let ctx: AudioContext | null = null
let master: GainNode | null = null
/** 浏览器要求用户先和页面互动过才能出声；在那之前静默，不报警告 */
const activated = () => (navigator as Navigator & { userActivation?: { hasBeenActive: boolean } }).userActivation?.hasBeenActive ?? true
function audio() {
  if (!sfxOn.value || !activated()) return null
  if (!ctx) {
    const AC = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
    if (!AC) return null
    ctx = new AC()
    master = ctx.createGain()
    master.gain.value = 0.85
    master.connect(ctx.destination)
  }
  if (ctx.state === 'suspended') void ctx.resume()
  return ctx
}

let noiseBuf: AudioBuffer | null = null
function noise(c: AudioContext) {
  if (!noiseBuf) {
    noiseBuf = c.createBuffer(1, c.sampleRate * 0.6, c.sampleRate)
    const d = noiseBuf.getChannelData(0)
    for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1
  }
  const src = c.createBufferSource()
  src.buffer = noiseBuf
  return src
}

/** 一段短噪声（纸牌摩擦、拍桌的质感） */
function burst(at: number, dur: number, freq: number, q: number, gain: number, type: BiquadFilterType = 'bandpass') {
  const c = audio(); if (!c || !master) return
  const t = c.currentTime + at
  const src = noise(c)
  const f = c.createBiquadFilter(); f.type = type; f.frequency.value = freq; f.Q.value = q
  const g = c.createGain()
  g.gain.setValueAtTime(0.0001, t)
  g.gain.exponentialRampToValueAtTime(gain, t + 0.004)
  g.gain.exponentialRampToValueAtTime(0.0001, t + dur)
  src.connect(f).connect(g).connect(master)
  src.start(t, Math.random() * 0.3)
  src.stop(t + dur + 0.02)
}

/** 一个音（可以滑音） */
function tone(at: number, dur: number, from: number, to: number, gain: number, type: OscillatorType = 'sine') {
  const c = audio(); if (!c || !master) return
  const t = c.currentTime + at
  const o = c.createOscillator(); o.type = type
  o.frequency.setValueAtTime(from, t)
  if (to !== from) o.frequency.exponentialRampToValueAtTime(Math.max(1, to), t + dur)
  const g = c.createGain()
  g.gain.setValueAtTime(0.0001, t)
  g.gain.exponentialRampToValueAtTime(gain, t + 0.01)
  g.gain.exponentialRampToValueAtTime(0.0001, t + dur)
  o.connect(g).connect(master)
  o.start(t)
  o.stop(t + dur + 0.02)
}

export const sfx = {
  /** 选牌：清脆的「咔」 */
  select() { burst(0, 0.035, 3200, 2.2, 0.5); tone(0, 0.03, 1800, 1200, 0.08, 'triangle') },
  /** 取消选牌：低一点的「咔」 */
  unselect() { burst(0, 0.03, 2200, 2, 0.35) },
  /** 出牌：几张牌拍在桌上，张数越多越「哗」 */
  play(count = 1) {
    const n = Math.min(6, Math.max(1, Math.ceil(count / 2)))
    for (let i = 0; i < n; i++) burst(i * 0.028, 0.09, 1400 - i * 80, 0.9, 0.55)
    burst(0, 0.06, 300, 0.7, 0.4, 'lowpass')
  },
  /** 不出 / 要不起 */
  pass() { tone(0, 0.12, 520, 440, 0.12, 'triangle'); tone(0.1, 0.16, 440, 330, 0.1, 'triangle') },
  /** 叫分：分越高音越高 */
  bid(n: number) {
    if (!n) { tone(0, 0.14, 330, 300, 0.1, 'triangle'); return }
    const base = [0, 523, 659, 784][n] ?? 523
    tone(0, 0.18, base, base, 0.14, 'triangle'); tone(0.08, 0.22, base * 1.5, base * 1.5, 0.08)
  },
  /** 轮到自己 */
  turn() { tone(0, 0.12, 880, 880, 0.12); tone(0.12, 0.2, 1175, 1175, 0.12) },
  /** 最后 5 秒倒计时 */
  tick() { tone(0, 0.05, 1500, 1500, 0.06, 'square') },
  /** 只剩一两张牌的警报 */
  warn() { tone(0, 0.1, 1046, 1046, 0.09, 'square'); tone(0.14, 0.1, 1046, 1046, 0.09, 'square') },
  /** 炸弹：低沉的爆炸 */
  bomb() {
    tone(0, 0.6, 140, 38, 0.6); burst(0, 0.5, 600, 0.6, 0.7, 'lowpass'); burst(0.02, 0.25, 2500, 0.8, 0.25)
  },
  /** 王炸：上冲的呼啸 + 爆炸 */
  rocket() {
    tone(0, 0.35, 220, 1600, 0.12, 'sawtooth'); burst(0, 0.35, 1800, 0.6, 0.25, 'highpass')
    window.setTimeout(() => sfx.bomb(), 330)
  },
  /** 飞机：一阵呼啸 */
  plane() { tone(0, 0.5, 300, 900, 0.07, 'sawtooth'); burst(0, 0.5, 1200, 0.5, 0.2) },
  /** 胜利：上行琶音 */
  win() { [523, 659, 784, 1046].forEach((f, i) => tone(i * 0.11, 0.3, f, f, 0.14, 'triangle')) },
  /** 失败：下行小调 */
  lose() { [440, 392, 349, 262].forEach((f, i) => tone(i * 0.14, 0.32, f, f, 0.12, 'triangle')) },
  /** 春天：明亮的连串高音 */
  spring() { [1046, 1318, 1568, 2093, 1568, 2093].forEach((f, i) => tone(i * 0.07, 0.16, f, f, 0.08)) },
  /** 发牌 */
  deal() { for (let i = 0; i < 8; i++) burst(i * 0.05, 0.05, 2600, 1.4, 0.25) },
}

// ===== 语音报牌 =====
const RANK_NAME: Record<number, string> = { 3: '三', 4: '四', 5: '五', 6: '六', 7: '七', 8: '八', 9: '九', 10: '十', 11: '勾', 12: '圈', 13: 'K', 14: '尖', 15: '二', 16: '小王', 17: '大王' }
export const rankName = (r: number) => RANK_NAME[r] ?? String(r)

let zhVoice: SpeechSynthesisVoice | null | undefined
function pickVoice() {
  if (zhVoice !== undefined) return zhVoice
  const voices = window.speechSynthesis?.getVoices() ?? []
  zhVoice = voices.find(v => /zh[-_]CN/i.test(v.lang)) ?? voices.find(v => /^zh/i.test(v.lang)) ?? null
  return zhVoice
}
if (typeof window !== 'undefined' && window.speechSynthesis) window.speechSynthesis.onvoiceschanged = () => { zhVoice = undefined }

export function speak(text: string) {
  if (!voiceOn.value || !text || !window.speechSynthesis) return
  const u = new SpeechSynthesisUtterance(text)
  u.lang = 'zh-CN'
  const v = pickVoice()
  if (v) u.voice = v
  u.rate = 1.15
  u.pitch = 1.05
  window.speechSynthesis.cancel()
  window.speechSynthesis.speak(u)
}

/** 根据牌型播报（单张、对子报点数，其他报牌型） */
export function announce(type: string | null | undefined, ranks: number[], label?: string | null) {
  if (!type) return
  if (type === 'single') return speak(rankName(ranks[0] ?? 0))
  if (type === 'pair') return speak(`对${rankName(ranks[0] ?? 0)}`)
  if (type === 'rocket') return speak('王炸')
  if (type === 'bomb') return speak('炸弹')
  speak(label || '')
}
