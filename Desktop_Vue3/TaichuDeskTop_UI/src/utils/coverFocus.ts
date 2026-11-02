// 封面「显示哪一块」（焦点）。
// 焦点记在图片地址末尾的 #tc-focus=x,y（百分比，0~100）里：
// · 浏览器加载图片时会忽略 # 后面的部分，所以图片照常显示，旧数据、别的页面都不受影响；
// · 不用改数据库，也不用改后端接口。
// 显示时用 object-position 让裁切以焦点为中心：列表卡片（16:9）和详情页横幅（21:7）比例不同，但都对准同一块。

const KEY = 'tc-focus'

export interface CoverInfo {
  /** 去掉焦点标记后的真实图片地址 */
  src: string
  /** 焦点横向位置（%） */
  x: number
  /** 焦点纵向位置（%） */
  y: number
  /** 有没有设置过焦点 */
  hasFocus: boolean
}

const clamp = (v: number) => Math.min(100, Math.max(0, Math.round(v)))

export function parseCover(url: string | null | undefined): CoverInfo {
  const raw = url ?? ''
  const i = raw.indexOf('#')
  if (i < 0) return { src: raw, x: 50, y: 50, hasFocus: false }
  const base = raw.slice(0, i)
  const frag = raw.slice(i + 1)
  const m = new RegExp(`(?:^|&)${KEY}=(\\d{1,3}(?:\\.\\d+)?),(\\d{1,3}(?:\\.\\d+)?)`).exec(frag)
  if (!m) return { src: raw, x: 50, y: 50, hasFocus: false }
  return { src: base, x: clamp(Number(m[1])), y: clamp(Number(m[2])), hasFocus: true }
}

/** 把焦点写进地址（替换掉原来的焦点标记） */
export function withFocus(url: string, x: number, y: number): string {
  const i = url.indexOf('#')
  const src = i < 0 ? url : url.slice(0, i)
  return `${src}#${KEY}=${clamp(x)},${clamp(y)}`
}

/** 给 <img> 用的样式：object-position 对准焦点 */
export function coverStyle(url: string | null | undefined): Record<string, string> {
  const c = parseCover(url)
  return c.hasFocus ? { objectPosition: `${c.x}% ${c.y}%` } : {}
}

/** 给 <img :src> 用的地址（去掉焦点标记） */
export function coverSrc(url: string | null | undefined): string {
  return parseCover(url).src
}
