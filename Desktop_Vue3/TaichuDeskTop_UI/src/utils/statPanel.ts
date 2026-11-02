/**
 * 能力面板（像 JOJO 替身面板那样的多维雷达图）：柴圈 OC 和世界观条目共用这一套数据结构和计算。
 *
 * 存成 JSON（后端见 Services/Panels/StatPanelRules.cs，两边的上限要保持一致）：
 * { "v": 1, "title": "能力面板", "scale": "grade" | "number", "max": 100,
 *   "dims": [ { "name": "破坏力", "value": "A" } 或 { "name": "力量", "value": 80 } ],
 *   "note": "备注（可选）", "overall": true（显示综合评级）, "template": "jojo" | "six" | "custom" }
 *
 * - scale = grade：字母等级 S、A～E（S 比 A 还高一档），另外有 ∞（超出上限）、?（未知）、—（无）
 * - scale = number：0～max 的数值，max 可以自己设（默认 100）
 * - 维度最少 3 个，不设上限（界面上也不写「最多几个」）；只有一个很宽的防滥用上限 100 个，后端另外限制整个 JSON 不超过 64KB
 */

export type Grade = 'S' | 'A' | 'B' | 'C' | 'D' | 'E' | '∞' | '?' | '—'
export type StatScale = 'grade' | 'number'
export type StatTemplateId = 'jojo' | 'six' | 'custom'
export interface StatDim { name: string; value: Grade | number }
export interface StatPanel {
  v: 1
  title: string
  scale: StatScale
  /** 数值刻度的满分（字母等级时也留着，切回数值时用） */
  max: number
  dims: StatDim[]
  note?: string
  /** 显示自动算出的「综合评级」 */
  overall?: boolean
  template?: StatTemplateId
}

/** 雷达图叠加对比的一份（约战双方对比用）：name 写在图例里，dashed = 虚线（同一方的第二个 OC 起） */
export interface ChartSeries { name: string; color: string; panel: StatPanel; dashed?: boolean }

/** 上限：和后端 StatPanelRules.cs 保持一致，改一边一定要改另一边。
 *  maxDims 只是防有人直接调接口塞几万个维度，正常用不到，界面上不提示 */
export const STAT_LIMITS = { minDims: 3, maxDims: 100, title: 20, name: 12, note: 300, maxMax: 10000 } as const
/** 维度超过这么多时，雷达图下面再列一张「维度：等级」的表（图上的字太小了） */
export const STAT_TABLE_FROM = 13
export const DEFAULT_PANEL_TITLE = '能力面板'
/** 字母等级（按从强到弱排）+ 三个特殊值 */
export const GRADES: Grade[] = ['S', 'A', 'B', 'C', 'D', 'E']
export const ALL_GRADES: Grade[] = ['S', 'A', 'B', 'C', 'D', 'E', '∞', '?', '—']
/** 每个等级的意思（鼠标悬停时提示）。本站的标准：E 是普通人、大约平均水准，往上一级比一级强 */
export const GRADE_HINT: Record<Grade, string> = {
  S: '顶级（比 A 更高）', A: '超级', B: '很强', C: '强', D: '比常人强', E: '普通人（平均水准）', '∞': '超出上限', '?': '未知', '—': '无',
}
/** 字母等级算分：S=6、A=5 … E=1；∞ 至少是 A，按 5 分算；? 和 — 不参与平均 */
const GRADE_SCORE: Partial<Record<Grade, number>> = { S: 6, A: 5, B: 4, C: 3, D: 2, E: 1, '∞': 5 }

const isGrade = (v: unknown): v is Grade => typeof v === 'string' && (ALL_GRADES as string[]).includes(v)
/** 别人手打的 a、-、无穷 之类也认 */
function toGrade(v: unknown): Grade | null {
  if (typeof v !== 'string') return null
  const s = v.trim().toUpperCase()
  if (isGrade(s)) return s
  if (s === '-' || s === '－' || s === '无') return '—'
  if (s === '？') return '?'
  if (s === 'INF' || s === '无穷' || s === '∞') return '∞'
  return null
}

// ============== 模板 ==============

export const STAT_TEMPLATES: { id: StatTemplateId; label: string; hint: string }[] = [
  { id: 'jojo', label: 'JOJO 替身面板', hint: '破坏力、速度、射程距离、持久力、精密动作性、成长性，字母等级' },
  { id: 'six', label: '六维', hint: '力量、敏捷、体质、智力、感知、魅力，数值 0～100' },
  { id: 'custom', label: '自定义', hint: '五个空维度，名字和刻度都自己定' },
]
const JOJO_DIMS = ['破坏力', '速度', '射程距离', '持久力', '精密动作性', '成长性']
const SIX_DIMS = ['力量', '敏捷', '体质', '智力', '感知', '魅力']

/** 按模板生成一个新面板；custom 会沿用传进来的刻度（不打乱用户已经选好的） */
export function createStatPanel(template: StatTemplateId = 'jojo', scale: StatScale = 'grade', max = 100): StatPanel {
  if (template === 'jojo') return { v: 1, title: DEFAULT_PANEL_TITLE, scale: 'grade', max: 100, dims: JOJO_DIMS.map(name => ({ name, value: 'C' })), overall: true, template }
  if (template === 'six') return { v: 1, title: DEFAULT_PANEL_TITLE, scale: 'number', max: 100, dims: SIX_DIMS.map(name => ({ name, value: 50 })), overall: true, template }
  return { v: 1, title: DEFAULT_PANEL_TITLE, scale, max, dims: Array.from({ length: 5 }, () => ({ name: '', value: defaultValue(scale, max) })), overall: true, template }
}
export const defaultValue = (scale: StatScale, max: number): Grade | number => scale === 'grade' ? 'C' : Math.round(max / 2)

// ============== 整理（读到的数据、保存前的数据都过一遍） ==============

const clampMax = (v: unknown) => {
  const n = Math.round(Number(v))
  return Number.isFinite(n) ? Math.min(STAT_LIMITS.maxMax, Math.max(1, n)) : 100
}
/** 数值保留一位小数，限制在 0～max */
export const clampNumber = (v: unknown, max: number) => {
  const n = Number(v)
  if (!Number.isFinite(n)) return 0
  return Math.round(Math.min(max, Math.max(0, n)) * 10) / 10
}

/**
 * 把面板整理干净；不是对象返回 null。
 * editing = true：编辑中，名字不去空格、空名字不补（不然用户删掉名字准备重打时会被立刻填回去）；
 * 其他情况空名字补成「维度 N」。
 */
export function cleanStatPanel(raw: unknown, editing = false): StatPanel | null {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return null
  const r = raw as Record<string, unknown>
  const scale: StatScale = r.scale === 'number' ? 'number' : 'grade'
  const max = clampMax(r.max ?? 100)
  const title = typeof r.title === 'string' ? (editing ? r.title : r.title.trim()).slice(0, STAT_LIMITS.title) : ''
  const list = Array.isArray(r.dims) ? r.dims : []
  const dims: StatDim[] = list.filter(d => d && typeof d === 'object').slice(0, STAT_LIMITS.maxDims).map((d: any, i): StatDim => {
    let name = typeof d.name === 'string' ? d.name : ''
    if (!editing) name = name.trim() || `维度${i + 1}`
    return { name: name.slice(0, STAT_LIMITS.name), value: cleanValue(d.value, scale, max) }
  })
  // 少于 3 个维度画不成多边形：补够
  while (dims.length < STAT_LIMITS.minDims) dims.push({ name: editing ? '' : `维度${dims.length + 1}`, value: defaultValue(scale, max) })
  const panel: StatPanel = { v: 1, title: editing ? title : title || DEFAULT_PANEL_TITLE, scale, max, dims }
  if (typeof r.note === 'string' && (editing ? r.note : r.note.trim())) panel.note = (editing ? r.note : r.note.trim()).slice(0, STAT_LIMITS.note)
  if (r.overall) panel.overall = true
  if (r.template === 'jojo' || r.template === 'six' || r.template === 'custom') panel.template = r.template
  return panel
}

function cleanValue(v: unknown, scale: StatScale, max: number): Grade | number {
  if (scale === 'grade') {
    const g = toGrade(v)
    // 存的是数字（比如从数值刻度改过来没转换）：按百分比换成等级
    if (!g && typeof v === 'number') return pctToGrade((v / max) * 100)
    return g ?? 'C'
  }
  if (typeof v === 'string') {
    const g = toGrade(v)
    if (g) return gradeToNumber(g, max)
  }
  return clampNumber(v, max)
}

export const serializeStatPanel = (panel: StatPanel) => JSON.stringify(cleanStatPanel(panel))
export const sameStatPanel = (a: StatPanel | null, b: StatPanel | null) =>
  JSON.stringify(a ? cleanStatPanel(a) : null) === JSON.stringify(b ? cleanStatPanel(b) : null)

// ============== 换刻度 / 计算 ==============

/** 字母 → 百分比：A=100、B=80、C=60、D=40、E=20；S 和 ∞ 当满分（换成数值时），? 和 — 当 0 */
const GRADE_PCT: Record<Grade, number> = { S: 100, A: 100, B: 80, C: 60, D: 40, E: 20, '∞': 100, '?': 0, '—': 0 }
const gradeToNumber = (g: Grade, max: number) => clampNumber((GRADE_PCT[g] / 100) * max, max)
/** 百分比 → 字母：≥90 A、≥70 B、≥50 C、≥30 D，其余 E */
export function pctToGrade(pct: number): Grade {
  if (pct >= 90) return 'A'
  if (pct >= 70) return 'B'
  if (pct >= 50) return 'C'
  if (pct >= 30) return 'D'
  return 'E'
}

/** 换刻度：字母 ⇄ 数值按上面的对照表换算，不会把已经填的值清空 */
export function convertScale(panel: StatPanel, scale: StatScale): StatPanel {
  if (panel.scale === scale) return panel
  const dims = panel.dims.map(d => ({
    name: d.name,
    value: scale === 'number'
      ? (typeof d.value === 'number' ? clampNumber(d.value, panel.max) : gradeToNumber(d.value, panel.max))
      : (typeof d.value === 'number' ? pctToGrade((d.value / panel.max) * 100) : d.value),
  }))
  return { ...panel, scale, dims }
}

/** 字母等级在雷达图上的档数：平时 5 档（E～A，A 在最外圈）；面板里用到 S 时 6 档（S 在最外圈，A 往里一圈） */
const LEVEL: Partial<Record<Grade, number>> = { S: 6, A: 5, B: 4, C: 3, D: 2, E: 1 }
export const usesS = (...panels: StatPanel[]) => panels.some(p => p.scale === 'grade' && p.dims.some(d => d.value === 'S'))
export const gradeLevels = (...panels: StatPanel[]) => usesS(...panels) ? 6 : 5

/** 这个维度在雷达图上占半径的多少（0～1）。levels：字母等级分几档（几个面板叠在一起比时要用同一个档数） */
export function dimRatio(panel: StatPanel, dim: StatDim, levels = gradeLevels(panel)): number {
  if (typeof dim.value === 'number') return panel.max > 0 ? Math.min(1, Math.max(0, dim.value / panel.max)) : 0
  if (dim.value === '∞') return 1
  const level = LEVEL[dim.value]
  return level ? Math.min(1, level / levels) : 0
}

/** 显示用的值：字母原样；数值去掉多余的 .0 */
export const formatValue = (dim: StatDim) => typeof dim.value === 'number' ? String(Number(dim.value.toFixed(1))) : dim.value
export const dimName = (dim: StatDim, index: number) => dim.name.trim() || `维度${index + 1}`

/**
 * 综合评级：字母等级按 A=5…E=1 平均（∞ 算 5，? 和 — 不算），数值按百分比平均，最后都显示成字母。
 * 一个能算的维度都没有时返回 null（不显示）。
 */
export function overallOf(panel: StatPanel): { grade: Grade; detail: string } | null {
  if (panel.scale === 'number') {
    const pcts = panel.dims.filter(d => typeof d.value === 'number').map(d => dimRatio(panel, d) * 100)
    if (!pcts.length) return null
    const avg = pcts.reduce((a, b) => a + b, 0) / pcts.length
    return { grade: pctToGrade(avg), detail: `各项平均 ${Math.round(avg)}%（≥90% 为 A，≥70% 为 B，≥50% 为 C，≥30% 为 D）` }
  }
  const scores = panel.dims.map(d => typeof d.value === 'string' ? GRADE_SCORE[d.value] : undefined).filter((s): s is number => s != null)
  if (!scores.length) return null
  const avg = scores.reduce((a, b) => a + b, 0) / scores.length
  const grade: Grade = avg >= 5.5 ? 'S' : avg >= 4.5 ? 'A' : avg >= 3.5 ? 'B' : avg >= 2.5 ? 'C' : avg >= 1.5 ? 'D' : 'E'
  return { grade, detail: `各项平均 ${avg.toFixed(1)} 分（${usesS(panel) ? 'S=6、' : ''}A=5、B=4、C=3、D=2、E=1，∞ 按 5 分算，? 和 — 不算）` }
}

/** 读屏软件念的一句话：「能力面板：破坏力 A，速度 B……」 */
export function describeStatPanel(panel: StatPanel) {
  const overall = panel.overall ? overallOf(panel) : null
  return `${panel.title || DEFAULT_PANEL_TITLE}：${panel.dims.map((d, i) => `${dimName(d, i)} ${formatValue(d)}${panel.scale === 'number' ? ` / ${panel.max}` : ''}`).join('，')}${overall ? `；综合 ${overall.grade}` : ''}`
}
