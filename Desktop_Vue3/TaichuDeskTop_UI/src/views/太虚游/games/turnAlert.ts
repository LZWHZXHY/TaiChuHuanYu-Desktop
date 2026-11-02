/**
 * 联机棋牌通用的「回合提醒」：
 * · 轮到自己时响一声「叮咚」，手机上轻震一下；
 * · 最后 10 秒提醒一次，最后 5 秒每秒滴答；
 * · 页面在后台（切到别的标签页）时，标签页标题闪烁「【轮到你了】」「【还剩 5 秒】」，切回来就停；
 * · 声音开关记在本地，象棋 / 围棋 / 五子棋共用（斗地主有自己的一套音效，只用标题提醒）。
 * 另外 useServerSkew 用服务器推送的 serverNow 校正本机时钟偏差，避免本机时间不准时倒计时乱跳。
 */
import { onBeforeUnmount, ref, watch } from 'vue'

const KEY = 'taichu.games.turnSound'
const read = () => { try { return localStorage.getItem(KEY) !== '0' } catch { return true } }
export const turnSoundOn = ref(read())
watch(turnSoundOn, v => { try { localStorage.setItem(KEY, v ? '1' : '0') } catch { /* 忽略 */ } })

let ctx: AudioContext | null = null
/** 浏览器要求先和页面互动过才能出声；在那之前静默 */
const activated = () => (navigator as Navigator & { userActivation?: { hasBeenActive: boolean } }).userActivation?.hasBeenActive ?? true
function beep(notes: [at: number, dur: number, freq: number][], type: OscillatorType = 'sine', gain = 0.12) {
  if (!turnSoundOn.value || !activated()) return
  try {
    if (!ctx) {
      const AC = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
      if (!AC) return
      ctx = new AC()
    }
    if (ctx.state === 'suspended') void ctx.resume()
    for (const [at, dur, freq] of notes) {
      const t = ctx.currentTime + at
      const o = ctx.createOscillator(); o.type = type; o.frequency.value = freq
      const g = ctx.createGain()
      g.gain.setValueAtTime(0.0001, t)
      g.gain.exponentialRampToValueAtTime(gain, t + 0.01)
      g.gain.exponentialRampToValueAtTime(0.0001, t + dur)
      o.connect(g).connect(ctx.destination)
      o.start(t); o.stop(t + dur + 0.02)
    }
  } catch { /* 出不了声就算了 */ }
}
export const turnSfx = {
  turn: () => beep([[0, 0.12, 880], [0.12, 0.22, 1175]]),
  warn: () => beep([[0, 0.1, 1046], [0.14, 0.1, 1046]], 'square', 0.08),
  tick: () => beep([[0, 0.05, 1500]], 'square', 0.05),
}

// ---------- 标签页标题闪烁（整个页面只有一份） ----------
let baseTitle = ''
let flashText = ''
let flashTimer = 0
function startFlash(text: string) {
  flashText = text
  if (flashTimer) return
  baseTitle = document.title
  let on = true
  document.title = flashText
  flashTimer = window.setInterval(() => { on = !on; document.title = on ? flashText : baseTitle }, 1000)
}
function stopFlash() {
  if (!flashTimer) return
  window.clearInterval(flashTimer)
  flashTimer = 0
  document.title = baseTitle
}

export interface TurnAlertOptions {
  /** 现在是不是轮到我（观战时恒为 false） */
  mine: () => boolean
  /** 这一步还剩几秒 */
  seconds: () => number
  /** 要不要出声（斗地主自己有音效，传 false） */
  sound?: boolean
}

export function useTurnAlert(opts: TurnAlertOptions) {
  const sound = opts.sound !== false
  watch(opts.mine, (mine, was) => {
    if (mine && !was) {
      if (sound) turnSfx.turn()
      try { navigator.vibrate?.(120) } catch { /* 不支持就算了 */ }
      if (document.hidden) startFlash('【轮到你了】')
    } else if (!mine) stopFlash()
  })
  watch(opts.seconds, (s, prev) => {
    if (!opts.mine() || s === prev || s <= 0 || prev === undefined || s > prev) return
    if (sound) {
      if (s <= 5) turnSfx.tick()
      else if (s <= 10 && prev > 10) turnSfx.warn()
    }
    if (document.hidden && s <= 10) startFlash(`【还剩 ${s} 秒】`)
  })
  const onVisible = () => { if (!document.hidden) stopFlash() }
  document.addEventListener('visibilitychange', onVisible)
  onBeforeUnmount(() => { document.removeEventListener('visibilitychange', onVisible); stopFlash() })
}

/**
 * 本机时钟和服务器的差（毫秒）。服务器每次推送状态都带 serverNow，
 * 差值变化超过 1 秒才更新，避免网络抖动让倒计时来回跳。
 * 倒计时用 deadline - (Date.now() + skew)。
 */
export function useServerSkew(serverNow: () => string | null | undefined) {
  const skew = ref(0)
  watch(serverNow, v => {
    if (!v) return
    const t = Date.parse(v)
    if (Number.isNaN(t)) return
    const s = t - Date.now()
    if (Math.abs(s - skew.value) > 1000) skew.value = s
  }, { immediate: true })
  return skew
}
