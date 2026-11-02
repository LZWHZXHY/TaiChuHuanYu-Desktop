// 斗地主牌桌互动：评论区、弹幕、点赞 / 砸番茄的类型和动画。
// 动画全部用 Web Animations（元素.animate）+ 少量 CSS，不引入新库；用户开了「减少动态效果」时只闪一下不飞。

/** 评论区里的一行（和后端 DdzManager.ChatView 字段一一对应） */
export interface ChatLine {
  id: number
  /** chat 说话；like 点赞提示；tomato 砸番茄提示 */
  kind: 'chat' | 'like' | 'tomato'
  userId?: string | null
  name: string
  seat: number
  /** 观战的人说的 / 砸的（界面上标「观战」） */
  spectator?: boolean
  text: string
  at: string
}

/** 服务器推送的 "Reaction"：谁给谁点赞 / 砸番茄 */
export interface ReactionEvent {
  kind: 'like' | 'tomato'
  /** 观战的人砸的为 -1（动画从「👁 观战」那里飞出来） */
  fromSeat: number
  fromSpectator?: boolean
  toSeat: number
  fromUserId: string
  fromName: string
  toName: string
  /** 发起人这一局已经用了几次 */
  used: number
  line: ChatLine
}

/** Hub 的 Social()：进桌时取回的最近评论和规则 */
export interface SocialInfo {
  roomId: string
  chat: ChatLine[]
  likeCost: number
  tomatoCost: number
  maxPerRound: number
  used: number
}

// ---------- 弹幕开关（记在本机，try/catch：无痕模式等存不了也不报错） ----------
const DANMAKU_KEY = 'ddz-danmaku'
export function readDanmakuOn(): boolean {
  try { return localStorage.getItem(DANMAKU_KEY) !== '0' } catch { return true }
}
export function saveDanmakuOn(on: boolean) {
  try { localStorage.setItem(DANMAKU_KEY, on ? '1' : '0') } catch { /* 存不了就算了，只是下次要重新关 */ }
}

const reducedMotion = () => {
  try { return window.matchMedia('(prefers-reduced-motion: reduce)').matches } catch { return false }
}
const canAnimate = (el: Element) => typeof (el as HTMLElement).animate === 'function'

/** 某个座位头像的中心点（相对牌桌 felt 左上角） */
function centerOf(felt: HTMLElement, seat: number): { x: number; y: number } | null {
  const el = felt.querySelector<HTMLElement>(`[data-seat-anchor="${seat}"]`)
  if (!el) return null
  const a = el.getBoundingClientRect(), base = felt.getBoundingClientRect()
  if (!a.width && !a.height) return null
  return { x: a.left + a.width / 2 - base.left, y: a.top + a.height / 2 - base.top }
}

function make(tag: string, className: string, parent: HTMLElement) {
  const el = document.createElement(tag)
  el.className = className
  parent.appendChild(el)
  return el
}

/**
 * 从 from 座位飞一个番茄 / 大拇指到 to 座位，落地后：番茄溅开、大拇指冒「+1」。
 * onLand 在落地那一刻调用（父组件用它给被砸 / 被赞的人加抖动 / 点亮效果）。
 * 飞行用两层元素：外层只管左右（匀速），里层只管上下（先上后下），合起来是一条抛物线。
 */
export function playReactionFx(felt: HTMLElement, layer: HTMLElement, from: number, to: number, kind: 'like' | 'tomato', onLand: () => void) {
  const end = centerOf(felt, to)
  if (!end) { onLand(); return }
  const start = centerOf(felt, from) ?? { x: end.x, y: Math.max(0, end.y - 160) }
  const glyph = kind === 'tomato' ? '🍅' : '👍'

  if (reducedMotion() || !canAnimate(layer)) {
    onLand()
    impact(layer, end.x, end.y, kind, false)
    return
  }

  const dist = Math.hypot(end.x - start.x, end.y - start.y)
  const duration = Math.round(Math.min(800, Math.max(450, dist * 0.9)))
  // 抛物线最高点：比两头都高一些，但不能飞出牌桌顶上（对手头像就在牌桌最上面）
  const peak = Math.max(6, Math.min(start.y, end.y) - Math.min(140, 50 + dist * 0.25))

  const outer = make('span', 'fx-fly-x', layer)
  const inner = make('span', 'fx-fly-y', outer)
  const icon = make('span', 'fx-fly-icon', inner)
  icon.textContent = glyph

  outer.animate([{ transform: `translateX(${start.x}px)` }, { transform: `translateX(${end.x}px)` }], { duration, easing: 'linear', fill: 'forwards' })
  inner.animate([
    { transform: `translateY(${start.y}px)`, easing: 'cubic-bezier(.2,.7,.4,1)' },
    { transform: `translateY(${peak}px)`, offset: 0.45, easing: 'cubic-bezier(.6,0,.8,.4)' },
    { transform: `translateY(${end.y}px)` },
  ], { duration, fill: 'forwards' })
  const spin = icon.animate(
    kind === 'tomato'
      ? [{ transform: 'rotate(0deg) scale(.7)' }, { transform: 'rotate(300deg) scale(1.15)', offset: 0.5 }, { transform: 'rotate(560deg) scale(1)' }]
      : [{ transform: 'rotate(-30deg) scale(.6)' }, { transform: 'rotate(10deg) scale(1.25)', offset: 0.6 }, { transform: 'rotate(0deg) scale(1)' }],
    { duration, fill: 'forwards' },
  )
  spin.onfinish = () => {
    outer.remove()
    onLand()
    impact(layer, end.x, end.y, kind, true)
  }
}

/** 落地效果：番茄压扁 + 汁水往四周溅；大拇指一圈光 + 「+1」往上飘 */
function impact(layer: HTMLElement, x: number, y: number, kind: 'like' | 'tomato', motion: boolean) {
  const box = make('span', `fx-hit fx-${kind}`, layer)
  box.style.left = `${x}px`
  box.style.top = `${y}px`
  if (kind === 'tomato') {
    // 番茄啪地压扁成一摊（红色不规则的一块，CSS 画的）
    const splat = make('span', 'fx-splat', box)
    if (motion) {
      splat.animate([
        { transform: 'translate(-50%, -50%) scale(.25, .35)', opacity: 1 },
        { transform: 'translate(-50%, -50%) scale(1.25, .8)', opacity: 1, offset: 0.16 },
        { transform: 'translate(-50%, -50%) scale(1, 1)', opacity: 0.92, offset: 0.3 },
        { transform: 'translate(-50%, -40%) scale(1, 1.05)', opacity: 0.85, offset: 0.75 },
        { transform: 'translate(-50%, -30%) scale(.95, 1.1)', opacity: 0 },
      ], { duration: 1150, easing: 'ease-out', fill: 'forwards' })
      // 8 滴番茄汁，方向和距离随机一点，看着更自然
      for (let i = 0; i < 8; i++) {
        const drop = make('i', 'fx-drop', box)
        const angle = (Math.PI * 2 * i) / 8 + (Math.random() - 0.5) * 0.6
        const r = 26 + Math.random() * 22
        const size = 5 + Math.random() * 5
        drop.style.width = drop.style.height = `${size}px`
        drop.animate([
          { transform: 'translate(-50%, -50%) scale(1)', opacity: 1 },
          { transform: `translate(calc(-50% + ${Math.cos(angle) * r}px), calc(-50% + ${Math.sin(angle) * r + 10}px)) scale(.5)`, opacity: 0 },
        ], { duration: 600 + Math.random() * 300, easing: 'cubic-bezier(.2,.8,.4,1)', fill: 'forwards' })
      }
    }
  } else {
    const ring = make('span', 'fx-ring', box)
    const plus = make('span', 'fx-plus', box)
    plus.textContent = '+1 👍'
    if (motion) {
      ring.animate([{ transform: 'translate(-50%, -50%) scale(.3)', opacity: 0.9 }, { transform: 'translate(-50%, -50%) scale(1.8)', opacity: 0 }], { duration: 700, easing: 'ease-out', fill: 'forwards' })
      plus.animate([{ transform: 'translate(-50%, -10px)', opacity: 0 }, { transform: 'translate(-50%, -34px)', opacity: 1, offset: 0.3 }, { transform: 'translate(-50%, -58px)', opacity: 0 }], { duration: 1100, easing: 'ease-out', fill: 'forwards' })
    }
  }
  window.setTimeout(() => box.remove(), motion ? 1200 : 900)
}
