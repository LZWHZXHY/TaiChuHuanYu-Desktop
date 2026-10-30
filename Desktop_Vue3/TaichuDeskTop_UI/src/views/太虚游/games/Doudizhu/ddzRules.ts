// 斗地主规则：牌的编号、牌型识别、比大小、找能压过上家的牌（提示 / 电脑出牌）。
// 后端 Services/Games/Doudizhu/DdzRules.cs 是同一套逻辑的 C# 版本，两边改动要保持一致。
//
// 牌的编号 0–53：0–51 为普通牌，点数 = floor(id / 4) + 3，花色 = id % 4；52 小王，53 大王。
// 点数大小：3 … 10, J=11, Q=12, K=13, A=14, 2=15, 小王=16, 大王=17。

export type PatternType =
  | 'single' | 'pair' | 'triple' | 'triple1' | 'triple2'
  | 'straight' | 'pairs' | 'plane' | 'plane1' | 'plane2'
  | 'four2' | 'four22' | 'bomb' | 'rocket'

export interface Pattern {
  type: PatternType
  /** 用来比大小的点数（顺子、连对、飞机取最大的那一组） */
  rank: number
  /** 单位数量：顺子张数、连对对数、飞机的三张组数；其他牌型为 1 */
  len: number
}

export const PATTERN_LABEL: Record<PatternType, string> = {
  single: '单张', pair: '对子', triple: '三张', triple1: '三带一', triple2: '三带一对',
  straight: '顺子', pairs: '连对', plane: '飞机', plane1: '飞机带单', plane2: '飞机带对',
  four2: '四带二', four22: '四带两对', bomb: '炸弹', rocket: '王炸',
}

export const rankOf = (card: number) => (card < 52 ? Math.floor(card / 4) + 3 : card === 52 ? 16 : 17)
export const suitOf = (card: number) => (card < 52 ? card % 4 : -1)
const RANK_TEXT: Record<number, string> = { 11: 'J', 12: 'Q', 13: 'K', 14: 'A', 15: '2', 16: '小王', 17: '大王' }
export const rankText = (rank: number) => RANK_TEXT[rank] ?? String(rank)

/** 按点数从小到大、同点数按花色排序 */
export const sortCards = (cards: number[]) => [...cards].sort((a, b) => rankOf(a) - rankOf(b) || a - b)

function countRanks(cards: number[]) {
  const m = new Map<number, number>()
  for (const c of cards) m.set(rankOf(c), (m.get(rankOf(c)) ?? 0) + 1)
  return m
}

/** 从 start 开始连续 len 个点数（都 ≤ A）是否都满足条件 */
function consecutive(ranks: number[]) {
  for (let i = 1; i < ranks.length; i++) if (ranks[i] !== ranks[i - 1] + 1) return false
  return ranks[ranks.length - 1] <= 14
}

/** 在点数表里找长度为 k、每个点数至少 min 张的最高连续段，返回段的最大点数 */
function highestRun(cnt: Map<number, number>, k: number, min: number, exact = false): number | null {
  for (let top = 14; top - k + 1 >= 3; top--) {
    let ok = true
    for (let r = top - k + 1; r <= top; r++) {
      const n = cnt.get(r) ?? 0
      if (exact ? n !== min : n < min) { ok = false; break }
    }
    if (ok) return top
  }
  return null
}

/** 识别一手牌的牌型；不合法返回 null */
export function analyze(cards: number[]): Pattern | null {
  const n = cards.length
  if (n === 0) return null
  const cnt = countRanks(cards)
  const ranks = [...cnt.keys()].sort((a, b) => a - b)
  const counts = ranks.map(r => cnt.get(r)!)
  const all = (k: number) => counts.every(c => c === k)
  const rankWith = (k: number) => ranks.filter(r => cnt.get(r) === k)

  if (n === 1) return { type: 'single', rank: ranks[0], len: 1 }
  if (n === 2) {
    if (ranks.length === 2 && ranks[0] === 16 && ranks[1] === 17) return { type: 'rocket', rank: 17, len: 1 }
    return all(2) ? { type: 'pair', rank: ranks[0], len: 1 } : null
  }
  if (n === 3) return all(3) ? { type: 'triple', rank: ranks[0], len: 1 } : null
  if (n === 4) {
    if (all(4)) return { type: 'bomb', rank: ranks[0], len: 1 }
    const t = rankWith(3)
    return t.length === 1 ? { type: 'triple1', rank: t[0], len: 1 } : null
  }
  if (n === 5 && ranks.length === 2 && rankWith(3).length === 1 && rankWith(2).length === 1)
    return { type: 'triple2', rank: rankWith(3)[0], len: 1 }

  if (n >= 5 && all(1) && consecutive(ranks)) return { type: 'straight', rank: ranks[ranks.length - 1], len: n }
  if (n >= 6 && n % 2 === 0 && all(2) && consecutive(ranks)) return { type: 'pairs', rank: ranks[ranks.length - 1], len: n / 2 }
  if (n >= 6 && n % 3 === 0 && all(3) && consecutive(ranks)) return { type: 'plane', rank: ranks[ranks.length - 1], len: n / 3 }

  const fours = rankWith(4)
  if (n === 6 && fours.length === 1) return { type: 'four2', rank: fours[0], len: 1 }
  if (n === 8 && fours.length >= 1) {
    // 四带两对：其余必须是两对（另一个四张也可以当两对）
    const main = fours[fours.length - 1]
    const rest = ranks.filter(r => r !== main)
    const pairsLeft = rest.reduce((s, r) => s + (cnt.get(r)! % 2 === 0 ? cnt.get(r)! / 2 : -99), 0)
    if (pairsLeft === 2) return { type: 'four22', rank: main, len: 1 }
  }

  // 飞机带单：k 组连续三张 + k 张单牌
  if (n >= 8 && n % 4 === 0) {
    const k = n / 4
    const top = highestRun(cnt, k, 3)
    if (top !== null) return { type: 'plane1', rank: top, len: k }
  }
  // 飞机带对：k 组连续三张 + k 对
  if (n >= 10 && n % 5 === 0) {
    const k = n / 5
    const top = highestRun(cnt, k, 3, true)
    if (top !== null) {
      const rest = ranks.filter(r => r < top - k + 1 || r > top)
      if (rest.every(r => cnt.get(r)! % 2 === 0) && rest.reduce((s, r) => s + cnt.get(r)! / 2, 0) === k)
        return { type: 'plane2', rank: top, len: k }
    }
  }
  return null
}

/** a 能不能压过 b */
export function beats(a: Pattern, b: Pattern): boolean {
  if (a.type === 'rocket') return true
  if (b.type === 'rocket') return false
  if (a.type === 'bomb' && b.type !== 'bomb') return true
  if (b.type === 'bomb' && a.type !== 'bomb') return false
  return a.type === b.type && a.len === b.len && a.rank > b.rank
}

// ---------------- 找牌（提示 / 电脑） ----------------

function groupByRank(hand: number[]) {
  const m = new Map<number, number[]>()
  for (const c of sortCards(hand)) {
    const r = rankOf(c)
    if (!m.has(r)) m.set(r, [])
    m.get(r)!.push(c)
  }
  return m
}

/** 取带牌：从 exclude 以外的点数里，优先拿单张多的小牌，凑 count 份、每份 size 张 */
function pickWings(groups: Map<number, number[]>, exclude: Set<number>, count: number, size: 1 | 2): number[] | null {
  const options = [...groups.entries()]
    .filter(([r, cs]) => !exclude.has(r) && cs.length >= size && !(size === 1 && r >= 16 && groups.has(16) && groups.has(17)))
    .sort((a, b) => (a[1].length === size ? 0 : 1) - (b[1].length === size ? 0 : 1) || a[0] - b[0])
  const out: number[] = []
  for (const [, cs] of options) {
    if (out.length / size >= count) break
    out.push(...cs.slice(0, size))
  }
  return out.length / size >= count ? out.slice(0, count * size) : null
}

/** 找出所有能压过 target 的出法（同一种点数组合只给一种），代价小的在前；炸弹、王炸排最后 */
export function findBeats(hand: number[], target: Pattern): number[][] {
  const g = groupByRank(hand)
  const has = (r: number, k: number) => (g.get(r)?.length ?? 0) >= k
  const take = (r: number, k: number) => g.get(r)!.slice(0, k)
  const out: number[][] = []
  const t = target

  if (t.type === 'single' || t.type === 'pair' || t.type === 'triple') {
    const k = t.type === 'single' ? 1 : t.type === 'pair' ? 2 : 3
    // 先用正好 k 张的点数，避免拆牌；再拆
    const ranks = [...g.keys()].filter(r => r > t.rank && has(r, k))
      .sort((a, b) => (g.get(a)!.length === k ? 0 : 1) - (g.get(b)!.length === k ? 0 : 1) || a - b)
    for (const r of ranks) {
      if (k === 1 && r >= 16 && has(16, 1) && has(17, 1) && g.get(r)!.length === 1 && ranks.some(x => x < 16)) continue
      out.push(take(r, k))
    }
  } else if (t.type === 'triple1' || t.type === 'triple2') {
    for (const r of [...g.keys()].sort((a, b) => a - b)) {
      if (r <= t.rank || !has(r, 3)) continue
      const wings = pickWings(g, new Set([r]), 1, t.type === 'triple1' ? 1 : 2)
      if (wings) out.push([...take(r, 3), ...wings])
    }
  } else if (t.type === 'straight' || t.type === 'pairs' || t.type === 'plane' || t.type === 'plane1' || t.type === 'plane2') {
    const k = t.type === 'straight' ? 1 : t.type === 'pairs' ? 2 : 3
    const L = t.len
    for (let top = t.rank + 1; top <= 14; top++) {
      const start = top - L + 1
      if (start < 3) continue
      let ok = true
      for (let r = start; r <= top; r++) if (!has(r, k)) { ok = false; break }
      if (!ok) continue
      const body: number[] = []
      const used = new Set<number>()
      for (let r = start; r <= top; r++) { body.push(...take(r, k)); used.add(r) }
      if (t.type === 'plane1' || t.type === 'plane2') {
        const wings = pickWings(g, used, L, t.type === 'plane1' ? 1 : 2)
        if (!wings) continue
        body.push(...wings)
      }
      out.push(body)
    }
  } else if (t.type === 'four2' || t.type === 'four22') {
    for (const r of [...g.keys()].sort((a, b) => a - b)) {
      if (r <= t.rank || !has(r, 4)) continue
      const wings = pickWings(g, new Set([r]), 2, t.type === 'four2' ? 1 : 2)
      if (wings) out.push([...take(r, 4), ...wings])
    }
  }

  // 炸弹
  for (const r of [...g.keys()].sort((a, b) => a - b)) {
    if (!has(r, 4)) continue
    if (t.type === 'bomb' && r <= t.rank) continue
    if (t.type === 'rocket') continue
    out.push(take(r, 4))
  }
  if (t.type !== 'rocket' && has(16, 1) && has(17, 1)) out.push([...take(16, 1), ...take(17, 1)])
  return out
}

/** 自己先出（或上家都不要）时，挑一手比较合理的牌：先出小牌，尽量成组出 */
export function chooseLead(hand: number[]): number[] {
  const whole = analyze(hand)
  if (whole && whole.type !== 'four2' && whole.type !== 'four22') return sortCards(hand)
  const g = groupByRank(hand)
  const ranks = [...g.keys()].sort((a, b) => a - b)
  // 最小的非炸弹点数（全是炸弹/王炸时只能出炸弹）
  const low = ranks.find(r => g.get(r)!.length < 4 && !(r >= 16 && g.has(16) && g.has(17)))
  if (low === undefined) {
    const bomb = ranks.find(r => g.get(r)!.length === 4)
    return bomb !== undefined ? g.get(bomb)! : [...g.get(16)!, ...g.get(17)!]
  }
  const c = g.get(low)!.length
  const has = (r: number, k: number) => (g.get(r)?.length ?? 0) >= k && (g.get(r)?.length ?? 0) < 4

  // 顺子：从最小点数往上连，至少 5 张
  if (low <= 10) {
    let end = low
    while (end + 1 <= 14 && has(end + 1, 1)) end++
    if (end - low + 1 >= 5) {
      const cards: number[] = []
      for (let r = low; r <= end; r++) cards.push(g.get(r)![0])
      return cards
    }
  }
  if (c === 2) {
    let end = low
    while (end + 1 <= 14 && (g.get(end + 1)?.length ?? 0) === 2) end++
    if (end - low + 1 >= 3) {
      const cards: number[] = []
      for (let r = low; r <= end; r++) cards.push(...g.get(r)!)
      return cards
    }
    return g.get(low)!
  }
  if (c === 3) {
    let end = low
    while (end + 1 <= 14 && (g.get(end + 1)?.length ?? 0) === 3) end++
    const k = end - low + 1
    const used = new Set<number>(); const body: number[] = []
    for (let r = low; r <= end; r++) { used.add(r); body.push(...g.get(r)!) }
    const singles = pickWings(g, used, k, 1)
    if (singles && singles.every(card => rankOf(card) < 15)) return [...body, ...singles]
    const pairs = pickWings(g, used, k, 2)
    if (pairs && pairs.every(card => rankOf(card) < 15)) return [...body, ...pairs]
    return body
  }
  return [g.get(low)![0]]
}

/**
 * 电脑跟牌：没有要求时自己出；有要求时找最小的能压过的牌。
 * 队友（同为农民）出的牌不压；炸弹只在对手快出完、或自己快出完时用。
 */
export function chooseFollow(hand: number[], target: Pattern, opts: { teammateLed: boolean; opponentLeft: number }): number[] | null {
  if (opts.teammateLed) return null
  const options = findBeats(hand, target)
  if (!options.length) return null
  const normal = options.find(o => { const p = analyze(o); return p && p.type !== 'bomb' && p.type !== 'rocket' })
  if (normal) {
    // 别用 2 和王去压很小的单张、对子（除非对手快出完了）
    const top = Math.max(...normal.map(rankOf))
    if (top >= 15 && target.rank < 11 && opts.opponentLeft > 5 && hand.length > 6) return null
    return normal
  }
  if (opts.opponentLeft <= 5 || hand.length <= 6) return options[0]
  return null
}

/** 电脑叫分：按大牌数量估算手牌强度，返回 0（不叫）或 1–3 */
export function chooseBid(hand: number[], currentBid: number): number {
  const g = groupByRank(hand)
  let score = 0
  if (g.has(17)) score += 4
  if (g.has(16)) score += 3
  score += (g.get(15)?.length ?? 0) * 2
  score += g.get(14)?.length ?? 0
  for (const cs of g.values()) if (cs.length === 4) score += 4
  const want = score >= 11 ? 3 : score >= 8 ? 2 : score >= 6 ? 1 : 0
  return want > currentBid ? want : 0
}
