/**
 * 中国象棋规则（前端：本地对弈、联机时的走法提示和棋谱记法；服务器端有一份同样的 C# 实现负责判定）。
 * 棋盘是 90 个字符的字符串，下标 = 行 * 9 + 列；第 0 行在黑方（上），第 9 行在红方（下）。
 * 大写是红方，小写是黑方：K 帅 / A 仕 / B 相 / N 马 / R 车 / C 炮 / P 兵，'.' 为空。
 */
export type Side = 'r' | 'b'
export const ROWS = 10
export const COLS = 9
export const INITIAL =
  'rnbakabnr' +
  '.........' +
  '.c.....c.' +
  'p.p.p.p.p' +
  '.........' +
  '.........' +
  'P.P.P.P.P' +
  '.C.....C.' +
  '.........' +
  'RNBAKABNR'

export const sideOf = (ch: string | undefined): Side | null => (!ch || ch === '.' ? null : ch === ch.toUpperCase() ? 'r' : 'b')
export const other = (s: Side): Side => (s === 'r' ? 'b' : 'r')
const inside = (r: number, c: number) => r >= 0 && r < ROWS && c >= 0 && c < COLS
const inPalace = (side: Side, r: number, c: number) => c >= 3 && c <= 5 && (side === 'r' ? r >= 7 : r <= 2)
const ownHalf = (side: Side, r: number) => (side === 'r' ? r >= 5 : r <= 4)

/** 不考虑「送将」的走法 */
export function pseudoMoves(b: string, from: number): number[] {
  const p = b[from]
  const side = sideOf(p)
  if (!p || !side) return []
  const r = Math.floor(from / COLS), c = from % COLS
  const out: number[] = []
  const add = (rr: number, cc: number) => {
    if (!inside(rr, cc)) return
    const t = b[rr * COLS + cc]
    if (sideOf(t) !== side) out.push(rr * COLS + cc)
  }
  const at = (rr: number, cc: number) => b[rr * COLS + cc]
  switch (p.toUpperCase()) {
    case 'K':
      for (const [dr, dc] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) if (inPalace(side, r + dr!, c + dc!)) add(r + dr!, c + dc!)
      break
    case 'A':
      for (const [dr, dc] of [[1, 1], [1, -1], [-1, 1], [-1, -1]]) if (inPalace(side, r + dr!, c + dc!)) add(r + dr!, c + dc!)
      break
    case 'B':
      for (const [dr, dc] of [[2, 2], [2, -2], [-2, 2], [-2, -2]]) {
        const rr = r + dr!, cc = c + dc!
        if (inside(rr, cc) && ownHalf(side, rr) && at(r + dr! / 2, c + dc! / 2) === '.') add(rr, cc)
      }
      break
    case 'N':
      for (const [dr, dc] of [[2, 1], [2, -1], [-2, 1], [-2, -1], [1, 2], [1, -2], [-1, 2], [-1, -2]]) {
        const rr = r + dr!, cc = c + dc!
        if (!inside(rr, cc)) continue
        const legR = Math.abs(dr!) === 2 ? r + dr! / 2 : r
        const legC = Math.abs(dc!) === 2 ? c + dc! / 2 : c
        if (at(legR, legC) === '.') add(rr, cc)
      }
      break
    case 'R':
    case 'C': {
      const cannon = p.toUpperCase() === 'C'
      for (const [dr, dc] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        let rr = r + dr!, cc = c + dc!
        let jumped = false
        while (inside(rr, cc)) {
          const t = at(rr, cc)
          if (!jumped) {
            if (t === '.') out.push(rr * COLS + cc)
            else if (cannon) jumped = true
            else { if (sideOf(t) !== side) out.push(rr * COLS + cc); break }
          } else if (t !== '.') {
            if (sideOf(t) !== side) out.push(rr * COLS + cc)
            break
          }
          rr += dr!; cc += dc!
        }
      }
      break
    }
    case 'P': {
      const fwd = side === 'r' ? -1 : 1
      add(r + fwd, c)
      if (!ownHalf(side, r)) { add(r, c - 1); add(r, c + 1) }
      break
    }
  }
  return out
}

export function apply(b: string, from: number, to: number): string {
  const arr = b.split('')
  arr[to] = arr[from]!
  arr[from] = '.'
  return arr.join('')
}

/** 将帅照面（同一列中间没有子） */
export function kingsFacing(b: string): boolean {
  const k = b.indexOf('k'), K = b.indexOf('K')
  if (k < 0 || K < 0 || k % COLS !== K % COLS) return false
  for (let i = k + COLS; i < K; i += COLS) if (b[i] !== '.') return false
  return true
}

/** side 方的将 / 帅是否正被将军 */
export function inCheck(b: string, side: Side): boolean {
  const king = b.indexOf(side === 'r' ? 'K' : 'k')
  if (king < 0) return true
  if (kingsFacing(b)) return true
  const enemy = other(side)
  for (let i = 0; i < b.length; i++) {
    if (sideOf(b[i]) === enemy && pseudoMoves(b, i).includes(king)) return true
  }
  return false
}

/** 合法走法（走完不能让自己被将军、不能将帅照面） */
export function legalMoves(b: string, from: number): number[] {
  const side = sideOf(b[from])
  if (!side) return []
  return pseudoMoves(b, from).filter(to => !inCheck(apply(b, from, to), side))
}

export function hasAnyLegal(b: string, side: Side): boolean {
  for (let i = 0; i < b.length; i++) if (sideOf(b[i]) === side && legalMoves(b, i).length) return true
  return false
}

export function countLegal(b: string, side: Side): number {
  let n = 0
  for (let i = 0; i < b.length; i++) if (sideOf(b[i]) === side) n += legalMoves(b, i).length
  return n
}

// ===== 中文记谱（炮二平五、马8进7……） =====
const RED_NUM = ['一', '二', '三', '四', '五', '六', '七', '八', '九']
const NAMES: Record<string, string> = { K: '帅', A: '仕', B: '相', N: '马', R: '车', C: '炮', P: '兵', k: '将', a: '士', b: '象', n: '马', r: '车', c: '炮', p: '卒' }
export const pieceName = (ch: string) => NAMES[ch] ?? ''

export function notation(b: string, from: number, to: number): string {
  const p = b[from]
  const side = sideOf(p)
  if (!p || !side) return ''
  const fr = Math.floor(from / COLS), fc = from % COLS, tr = Math.floor(to / COLS), tc = to % COLS
  const fileNo = (c: number) => (side === 'r' ? 9 - c : c + 1)
  const num = (n: number) => (side === 'r' ? RED_NUM[n - 1] ?? String(n) : String(n))
  const name = NAMES[p]!
  // 同一列有两个以上同样的子：用「前 / 中 / 后」区分
  const same: number[] = []
  for (let r = 0; r < ROWS; r++) if (b[r * COLS + fc] === p) same.push(r)
  if (side === 'b') same.reverse() // 黑方的「前」是行号大的
  let head: string
  if (same.length >= 2 && 'RNCP'.includes(p.toUpperCase())) {
    const idx = same.indexOf(fr)
    const tags = same.length === 2 ? ['前', '后'] : same.length === 3 ? ['前', '中', '后'] : ['一', '二', '三', '四', '五']
    head = `${tags[idx] ?? ''}${name}`
  } else head = `${name}${num(fileNo(fc))}`
  if (fr === tr) return `${head}平${num(fileNo(tc))}`
  const forward = side === 'r' ? tr < fr : tr > fr
  const diagonal = 'NBA'.includes(p.toUpperCase())
  return `${head}${forward ? '进' : '退'}${diagonal ? num(fileNo(tc)) : num(Math.abs(tr - fr))}`
}
