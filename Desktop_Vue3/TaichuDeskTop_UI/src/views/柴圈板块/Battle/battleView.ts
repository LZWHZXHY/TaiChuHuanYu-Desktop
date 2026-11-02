// 约战页面共用的「显示」小工具：状态名、结果种类、双方分边、战绩预览、时间格式。
// 以前大厅、详情、我的约战各写一套状态名（报名中 / 待应战 / 评审中 / 定夺中……），同一个状态三种叫法，这里统一成一套。
import type { Battle, BattleOutcome, BattleParticipant } from './battle_api'

// ===== 状态 =====
export const STATUS_LABEL: Record<Battle['status'], string> = {
  open: '报名中',
  ongoing: '创作中',
  judging: '判定中',
  finished: '已结束',
  cancelled: '已取消',
}
export const statusLabel = (s?: string | null) => (s && (STATUS_LABEL as Record<string, string>)[s]) || s || ''

/** 进度条上的第几步（报名 0 → 创作 1 → 判定 2 → 结束 3）；取消的约战返回 -1 */
export function stageIndex(status?: string | null): number {
  return ({ open: 0, ongoing: 1, judging: 2, finished: 3 } as Record<string, number>)[status ?? ''] ?? -1
}

// ===== 结果种类 =====
export interface OutcomeMeta {
  key: BattleOutcome
  icon: string
  label: string
  /** 一句话说明这个结果是什么意思 */
  desc: string
  /** 战绩怎么记 */
  record: string
  /** 要不要选胜者 */
  needsWinner: boolean
}
export const OUTCOMES: OutcomeMeta[] = [
  { key: 'win', icon: '🏆', label: '一方胜', desc: '分出了胜负。', record: '胜方每个出战 OC 记 1 胜，其他人记 1 负', needsWinner: true },
  { key: 'pyrrhic', icon: '🩸', label: '惨胜', desc: '赢了，但自己也只剩半条命。', record: '和一方胜一样（胜方记胜、其他人记负），结果显示「惨胜」', needsWinner: true },
  { key: 'draw', icon: '🤝', label: '平手', desc: '势均力敌，谁也没赢。', record: '所有人每个出战 OC 记 1 平', needsWinner: false },
  { key: 'mutual', icon: '☠', label: '同归于尽', desc: '两败俱伤，双方都倒下了。', record: '按平局算，所有人每个出战 OC 记 1 平', needsWinner: false },
  { key: 'nocontest', icon: '⏸', label: '无结果 / 中止', desc: '没打完、作废，或者不想分胜负。', record: '所有人都不计战绩', needsWinner: false },
]
export const outcomeMeta = (o?: string | null) => OUTCOMES.find(x => x.key === o)

/** 结果种类；后端没给 outcome 时（老接口）按 result 推断 */
export function outcomeOf(b?: Pick<Battle, 'outcome' | 'result'> | null): BattleOutcome | '' {
  if (!b) return ''
  if (b.outcome) return b.outcome
  if (b.result === 'draw') return 'draw'
  if (b.result === 'nocontest') return 'nocontest'
  if (b.result === 'completed') return 'win'
  return ''
}

/** 参与者个人结果的显示文字（惨胜的胜者显示「惨胜」，同归于尽显示「同归于尽」） */
export function resultLabel(r: BattleParticipant['result'], outcome?: string | null): string {
  if (r === 'win') return outcome === 'pyrrhic' ? '🩸 惨胜' : '🏆 胜'
  if (r === 'lose') return '负'
  if (r === 'draw') return outcome === 'mutual' ? '☠ 同归于尽' : '🤝 平'
  if (r === 'none') return '不计'
  return '—'
}

// ===== 双方分边 =====
export interface BattleSide {
  key: 'A' | 'B'
  /** 显示名：A 队 / B 队（团队战）或 发起方 / 应战方 */
  label: string
  members: BattleParticipant[]
  /** 已经被邀请、还没应战的人数 */
  pending: number
}

/** 读 battleConfigJson 里的 teams（用户编号 → 1 / 2 队），读不出来就是空 */
function teamsConfig(json?: string): Record<string, number> {
  if (!json) return {}
  try {
    const t = JSON.parse(json)?.teams
    if (!t || typeof t !== 'object') return {}
    return Object.fromEntries(Object.entries(t).map(([k, v]) => [k.toLowerCase(), Number(v)]))
  } catch { return {} }
}

/** 团队战：两队都有人，而且总人数超过 2（和后端投票、录结果的判断一致） */
export function isTeamBattle(ps: BattleParticipant[]): boolean {
  return ps.length > 2 && ps.some(p => p.teamNumber === 1) && ps.some(p => p.teamNumber === 2)
}

/**
 * 把参与者分成左右两边：有队伍号的按队伍分（A 队 = 1，B 队 = 2），没有的按「发起方 vs 其他人」分。
 * 还没应战的受邀者只算人数，放到配置里写的那一队（没写就放应战方）。
 */
export function splitSides(b: Pick<Battle, 'participants' | 'opponentOcIds' | 'battleConfigJson'>): [BattleSide, BattleSide] {
  const ps = b.participants ?? []
  const joined = new Set(ps.map(p => (p.userId || '').toLowerCase()))
  const teams = teamsConfig(b.battleConfigJson)
  const pendingIds = Object.keys(b.opponentOcIds ?? {}).filter(id => !joined.has(id.toLowerCase()))
  const byTeam = ps.length > 0 && ps.every(p => p.teamNumber === 1 || p.teamNumber === 2)
  const a = byTeam ? ps.filter(p => p.teamNumber === 1) : ps.slice(0, 1)
  const bb = byTeam ? ps.filter(p => p.teamNumber === 2) : ps.slice(1)
  const pendingA = pendingIds.filter(id => teams[id.toLowerCase()] === 1).length
  const pendingB = pendingIds.length - pendingA
  // 哪边超过一个人（算上没应战的）就叫「队」
  const team = a.length + pendingA > 1 || bb.length + pendingB > 1
  return [
    { key: 'A', label: team ? 'A 队' : '发起方', members: a, pending: pendingA },
    { key: 'B', label: team ? 'B 队' : '应战方', members: bb, pending: pendingB },
  ]
}

/** 一个参与者在哪一边（用来给名字、作品上色） */
export function sideOf(b: Pick<Battle, 'participants' | 'opponentOcIds' | 'battleConfigJson'>, participantId?: string | null): 'A' | 'B' | '' {
  if (!participantId) return ''
  const [a, bb] = splitSides(b)
  if (a.members.some(p => p.id === participantId)) return 'A'
  if (bb.members.some(p => p.id === participantId)) return 'B'
  return ''
}

const names = (ps: BattleParticipant[]) => ps.map(p => p.userName).join('、')

/** 结果横幅的大字：🏆 张三 胜 / 🩸 A 队（张三、李四）惨胜 / 🤝 平手 …… */
export function resultHeadline(b: Battle): { icon: string; text: string; outcome: BattleOutcome | '' } {
  const o = outcomeOf(b)
  const meta = outcomeMeta(o)
  if (o === 'win' || o === 'pyrrhic') {
    const winners = (b.participants ?? []).filter(p => p.result === 'win')
    const [a, bb] = splitSides(b)
    const side = [a, bb].find(s => s.members.length > 1 && winners.length && s.members.every(m => m.result === 'win'))
    const who = side ? `${side.label}（${names(side.members)}）` : names(winners) || '胜者'
    return { icon: meta!.icon, text: `${who} ${o === 'pyrrhic' ? '惨胜' : '胜'}`, outcome: o }
  }
  if (meta) return { icon: meta.icon, text: meta.label, outcome: o }
  return { icon: '📜', text: '结果已记录', outcome: '' }
}

// ===== 战绩预览（录入 / 修正结果时给发起人、管理员看「战绩会怎么记」） =====
export interface RecordLine { participant: BattleParticipant; result: 'win' | 'lose' | 'draw' | 'none' }

/** 照后端的算法预先算一遍：一方胜 / 惨胜时胜者所在的整队都算赢；所有人都赢就按平手；平手 / 同归于尽都记平；无结果不计 */
export function recordPreview(ps: BattleParticipant[], outcome: BattleOutcome | '', winnerId: string): RecordLine[] {
  if (!outcome) return []
  if (outcome === 'win' || outcome === 'pyrrhic') {
    const w = ps.find(p => p.id === winnerId)
    if (!w) return []
    const lines = ps.map((p): RecordLine => ({
      participant: p,
      result: (p.id === w.id || ((p.teamNumber === 1 || p.teamNumber === 2) && p.teamNumber === w.teamNumber) ? 'win' : 'lose'),
    }))
    if (lines.length > 1 && lines.every(l => l.result === 'win')) return lines.map((l): RecordLine => ({ ...l, result: 'draw' }))
    return lines
  }
  return ps.map((p): RecordLine => ({ participant: p, result: outcome === 'nocontest' ? 'none' : 'draw' }))
}
export const RECORD_TEXT: Record<RecordLine['result'], string> = { win: '记 1 胜', lose: '记 1 负', draw: '记 1 平', none: '不计战绩' }

// ===== 时间 =====
/** 10月31日 22:15 */
export function fmtTime(iso?: string | null): string {
  if (!iso) return ''
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return ''
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getMonth() + 1}月${d.getDate()}日 ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

/** 两个时间之间隔了多久：3 秒 / 5 分钟 / 2 小时 10 分 / 3 天 4 小时 */
export function fmtDuration(ms: number): string {
  if (!Number.isFinite(ms) || ms < 0) return ''
  const s = Math.floor(ms / 1000)
  if (s < 60) return `${s} 秒`
  const m = Math.floor(s / 60)
  if (m < 60) return `${m} 分钟`
  const h = Math.floor(m / 60)
  if (h < 24) return m % 60 ? `${h} 小时 ${m % 60} 分` : `${h} 小时`
  const d = Math.floor(h / 24)
  return h % 24 ? `${d} 天 ${h % 24} 小时` : `${d} 天`
}

/** 列表里的相对日期：今天 / 昨天 / 3 天前 / 10月3日 */
export function relDay(iso?: string | null): string {
  if (!iso) return ''
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return ''
  const diff = Math.floor((Date.now() - d.getTime()) / 86400000)
  if (diff <= 0) return '今天'
  if (diff === 1) return '昨天'
  if (diff < 7) return `${diff} 天前`
  return d.toLocaleDateString('zh-CN', { month: 'short', day: 'numeric' })
}
