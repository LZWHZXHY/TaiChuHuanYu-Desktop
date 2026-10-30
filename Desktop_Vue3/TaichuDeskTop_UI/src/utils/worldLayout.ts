/**
 * 条目的区块排列（像 vvd 那样：正文是一串可增删、排序、左右并排的区块）。
 * 存在条目的 layout 字段里（JSON 字符串），后端见 WorldLayout.cs。
 *
 * 每行 1～2 块。区块种类：
 * - description 正文（内容在 card.description）      - text  自定义文本块（背景、性格、外貌……可以有很多个）
 * - media       图片块（图片存在块里，可以有很多个）   - gallery 图库（card.galleryImages）
 * - timeline    时间线（card.timelineEvents）          - embeds  引用设定（card.contentBlocks）
 * - relations   设定关系（出向关系）
 * - map         地图（底图 + 图钉）                    - sources 参考来源（资料、链接、备注，默认只有成员看得到）
 * 只出现一次的区块用固定 ID（= 种类名），老条目按固定规则排好，打开再保存不会产生多余修改。
 */

export type BlockKind = 'description' | 'text' | 'media' | 'gallery' | 'timeline' | 'embeds' | 'relations' | 'map' | 'sources'
/** 参考来源的一条：名称、链接（可选）、备注（可选，比如第几页、第几分钟） */
export interface SourceItem { id: string; title: string; url?: string; note?: string }
/** 地图上的图钉：位置是相对底图的比例（0～1），可以连到一个条目 */
export interface MapPin { id: string; x: number; y: number; cardId?: string | null; label?: string; color?: string; note?: string
  /** 和连到的条目是什么关系：首都、战场遗址…… */
  relation?: string }

/** 图钉、时间线事件共用的颜色（每种颜色可以代表一类：战争、灾难、建国……） */
export const MARK_COLORS = ['#2a7f8f', '#d9534f', '#e0a526', '#5b8c3a', '#7a5cc4', '#3d4b5c', '#c46a9a', '#8a6a3a']
export interface LayoutBlock {
  id: string
  kind: BlockKind
  title?: string
  /** 隐藏标题栏 */
  hideTitle?: boolean
  /** 对访客隐藏（只有 text / media 能隐藏，服务器不会发给访客） */
  hidden?: boolean
  /** text 块的内容（和正文一样，可以 @ 引用其他条目） */
  text?: string
  /** media 块的图片 */
  images?: string[]
  /** map 块：底图和图钉 */
  image?: string
  pins?: MapPin[]
  /** sources 块：参考来源 */
  sources?: SourceItem[]
}
export interface LayoutRow { id: string; blocks: LayoutBlock[] }
export interface CardLayout { v: 1; rows: LayoutRow[] }

export const BLOCK_KINDS: BlockKind[] = ['description', 'text', 'media', 'map', 'gallery', 'timeline', 'embeds', 'relations', 'sources']
export const SINGLETON_KINDS: BlockKind[] = ['description', 'gallery', 'timeline', 'embeds', 'relations']
export const HIDEABLE_KINDS: BlockKind[] = ['text', 'media', 'map', 'sources']
export const BLOCK_META: Record<BlockKind, { label: string; icon: string; hint: string; keywords: string }> = {
  description: { label: '正文', icon: '¶', hint: '主要的设定描述', keywords: 'zhengwen 描述 简介' },
  text: { label: '文本', icon: '≡', hint: '自己起标题：背景、性格、外貌……', keywords: 'wenben text 背景 性格 外貌 笔记' },
  media: { label: '媒体', icon: '▣', hint: '放一张或几张图片', keywords: 'meiti image 图片 照片' },
  map: { label: '地图', icon: '⌖', hint: '上传地图，插图钉标出地点', keywords: 'ditu map 地图 图钉 坐标' },
  gallery: { label: '图库', icon: '▦', hint: '条目的图集', keywords: 'tuku gallery 图片' },
  timeline: { label: '时间线', icon: '⟶', hint: '按时间排列的事件', keywords: 'shijianxian timeline 事件 经历 年表' },
  embeds: { label: '引用设定', icon: '⧉', hint: '在这里展示其他条目', keywords: 'yinyong embed 嵌入' },
  relations: { label: '设定关系', icon: '⇄', hint: '居住地、所属组织……', keywords: 'guanxi relation 关联 关系树' },
  sources: { label: '参考来源', icon: '❡', hint: '记下参考的资料和链接，方便以后查（默认只有成员看得到）', keywords: 'cankao laiyuan source 参考 资料 出处 链接 引用' },
}

export const isSingleton = (kind: BlockKind) => SINGLETON_KINDS.includes(kind)
export const blockTitle = (block: LayoutBlock) => (block.title ?? '').trim() || BLOCK_META[block.kind]?.label || '区块'

let seq = 0
export function newId(prefix = 'b') {
  return `${prefix}${Date.now().toString(36)}${(seq++).toString(36)}${Math.random().toString(36).slice(2, 6)}`
}

/** 只要有内容就要显示的那些数据 */
interface LayoutSource {
  description?: string | null
  galleryImages?: unknown[] | null
  timelineEvents?: unknown[] | null
  contentBlocks?: unknown[] | null
}

/** 把一块整理干净（去掉没用的字段，保证对比稳定） */
function cleanBlock(raw: any): LayoutBlock | null {
  if (!raw || typeof raw !== 'object' || !BLOCK_KINDS.includes(raw.kind)) return null
  const kind = raw.kind as BlockKind
  const block: LayoutBlock = { id: isSingleton(kind) ? kind : String(raw.id || newId()), kind }
  if (typeof raw.title === 'string' && raw.title.trim()) block.title = raw.title.slice(0, 60)
  if (raw.hideTitle) block.hideTitle = true
  if (raw.hidden && HIDEABLE_KINDS.includes(kind)) block.hidden = true
  if (kind === 'text') block.text = typeof raw.text === 'string' ? raw.text : ''
  if (kind === 'media') block.images = Array.isArray(raw.images) ? raw.images.filter((x: unknown) => typeof x === 'string') : []
  if (kind === 'map') {
    block.image = typeof raw.image === 'string' ? raw.image : ''
    block.pins = (Array.isArray(raw.pins) ? raw.pins : []).filter((p: any) => p && Number.isFinite(p.x) && Number.isFinite(p.y)).map((p: any): MapPin => {
      const pin: MapPin = { id: String(p.id || newId('p')), x: Math.min(1, Math.max(0, Number(p.x))), y: Math.min(1, Math.max(0, Number(p.y))) }
      if (typeof p.cardId === 'string' && p.cardId) pin.cardId = p.cardId
      if (typeof p.label === 'string' && p.label.trim()) pin.label = p.label.slice(0, 60)
      if (typeof p.color === 'string' && /^#[0-9a-f]{3,8}$/i.test(p.color)) pin.color = p.color
      if (typeof p.note === 'string' && p.note.trim()) pin.note = p.note.slice(0, 1000)
      if (typeof p.relation === 'string' && p.relation.trim()) pin.relation = p.relation.slice(0, 40)
      return pin
    }).slice(0, 500)
  }
  if (kind === 'sources') {
    block.sources = (Array.isArray(raw.sources) ? raw.sources : []).filter((s: any) => s && typeof s === 'object').map((s: any): SourceItem => {
      const item: SourceItem = { id: String(s.id || newId('s')), title: typeof s.title === 'string' ? s.title.slice(0, 120) : '' }
      if (typeof s.url === 'string' && s.url.trim()) item.url = s.url.trim().slice(0, 1000)
      if (typeof s.note === 'string' && s.note.trim()) item.note = s.note.slice(0, 300)
      return item
    }).slice(0, 100)
  }
  return block
}

/** 解析保存的排列；没有或坏了返回 null */
export function parseLayout(json: string | null | undefined): CardLayout | null {
  if (!json) return null
  let raw: any
  try { raw = JSON.parse(json) } catch { return null }
  return normalizeLayout(raw)
}

export function normalizeLayout(raw: any): CardLayout | null {
  if (!raw || !Array.isArray(raw.rows)) return null
  const seen = new Set<string>()
  const rows: LayoutRow[] = []
  for (const r of raw.rows) {
    const blocks = (Array.isArray(r?.blocks) ? r.blocks : []).map(cleanBlock)
      .filter((b: LayoutBlock | null): b is LayoutBlock => !!b && !seen.has(b.id) && (seen.add(b.id), true)).slice(0, 2)
    if (blocks.length) rows.push({ id: String(r.id || newId('r')), blocks })
  }
  return { v: 1, rows }
}

/** 老条目的默认排列：正文 → 图库 → 时间线 → 引用设定（只放有内容的） */
export function defaultLayout(src: LayoutSource): CardLayout {
  const rows: LayoutRow[] = [{ id: 'r-description', blocks: [{ id: 'description', kind: 'description' }] }]
  appendMissing({ v: 1, rows }, src)
  return { v: 1, rows }
}

/** 有内容但排列里没有的固定区块补到最后（比如 AI 整理加了时间线），保证内容不会「消失」 */
function appendMissing(layout: CardLayout, src: LayoutSource) {
  const has = new Set(layout.rows.flatMap(r => r.blocks.map(b => b.kind)))
  const need: [BlockKind, boolean][] = [
    ['description', Boolean(String(src.description ?? '').trim())],
    ['gallery', Boolean(src.galleryImages?.length)],
    ['timeline', Boolean(src.timelineEvents?.length)],
    ['embeds', Boolean(src.contentBlocks?.length)],
  ]
  for (const [kind, filled] of need) if (filled && !has.has(kind)) layout.rows.push({ id: 'r-' + kind, blocks: [{ id: kind, kind }] })
}

/** 显示用的排列：保存过的就用保存的，没有就用默认的 */
export function resolveLayout(json: string | null | undefined, src: LayoutSource): CardLayout {
  const saved = parseLayout(json)
  if (!saved) return defaultLayout(src)
  appendMissing(saved, src)
  return saved
}

export function serializeLayout(layout: CardLayout): string {
  return JSON.stringify(layout)
}

// ============== 编辑操作（直接改传进来的 layout） ==============

export function findBlock(layout: CardLayout, blockId: string) {
  for (let r = 0; r < layout.rows.length; r++) {
    const c = layout.rows[r].blocks.findIndex(b => b.id === blockId)
    if (c >= 0) return { r, c, row: layout.rows[r], block: layout.rows[r].blocks[c] }
  }
  return null
}
export const hasKind = (layout: CardLayout, kind: BlockKind) => layout.rows.some(r => r.blocks.some(b => b.kind === kind))

export function createBlock(kind: BlockKind): LayoutBlock {
  const block: LayoutBlock = { id: isSingleton(kind) ? kind : newId(), kind }
  if (kind === 'text') { block.text = ''; block.title = '' }
  if (kind === 'media') block.images = []
  if (kind === 'map') { block.image = ''; block.pins = [] }
  // 参考来源多半是写给自己看的：默认对访客隐藏，需要的话在 ⋮ 里打开
  if (kind === 'sources') { block.sources = []; block.hidden = true }
  return block
}

export type Where = 'above' | 'below' | 'left' | 'right' | 'end'

/** 能不能放在某块的左右（那一行已经有两块就不行） */
export const canPlaceBeside = (layout: CardLayout, blockId: string) => (findBlock(layout, blockId)?.row.blocks.length ?? 2) < 2

export function insertBlock(layout: CardLayout, block: LayoutBlock, anchorId: string | null, where: Where) {
  const at = anchorId ? findBlock(layout, anchorId) : null
  if (!at || where === 'end') { layout.rows.push({ id: newId('r'), blocks: [block] }); return }
  if (where === 'above') layout.rows.splice(at.r, 0, { id: newId('r'), blocks: [block] })
  else if (where === 'below') layout.rows.splice(at.r + 1, 0, { id: newId('r'), blocks: [block] })
  else if (at.row.blocks.length < 2) at.row.blocks.splice(where === 'left' ? at.c : at.c + 1, 0, block)
  else layout.rows.splice(at.r + 1, 0, { id: newId('r'), blocks: [block] })
}

export function removeBlock(layout: CardLayout, blockId: string) {
  const at = findBlock(layout, blockId)
  if (!at) return
  at.row.blocks.splice(at.c, 1)
  if (!at.row.blocks.length) layout.rows.splice(at.r, 1)
}

/** 上移 / 下移：并排的块先拆成单独一行 */
export function moveBlock(layout: CardLayout, blockId: string, dir: -1 | 1) {
  const at = findBlock(layout, blockId)
  if (!at) return
  if (at.row.blocks.length > 1) {
    at.row.blocks.splice(at.c, 1)
    layout.rows.splice(dir < 0 ? at.r : at.r + 1, 0, { id: newId('r'), blocks: [at.block] })
    return
  }
  const target = at.r + dir
  if (target < 0 || target >= layout.rows.length) return
  const [row] = layout.rows.splice(at.r, 1)
  layout.rows.splice(target, 0, row)
}

export function swapRow(layout: CardLayout, blockId: string) {
  const at = findBlock(layout, blockId)
  if (at && at.row.blocks.length === 2) at.row.blocks.reverse()
}

/** 复制文本块 / 图片块（放在下面） */
export function duplicateBlock(layout: CardLayout, blockId: string) {
  const at = findBlock(layout, blockId)
  if (!at || isSingleton(at.block.kind)) return null
  const copy: LayoutBlock = JSON.parse(JSON.stringify(at.block))
  copy.id = newId()
  if (copy.title) copy.title = `${copy.title}（副本）`.slice(0, 60)
  insertBlock(layout, copy, blockId, 'below')
  return copy
}

// ============== 时间线 ==============

/** 从「1725年5月29日」「公元前300年」「100」这类写法里取出能排在时间轴上的数字；取不到返回 null */
export function timelineValue(date: string | null | undefined): number | null {
  const text = String(date ?? '').trim()
  if (!text) return null
  const nums = text.match(/-?\d+(?:\.\d+)?/g)
  if (!nums) return null
  let year = Number(nums[0])
  if (/前|BC|B\.C\./i.test(text) && year > 0) year = -year
  const month = nums.length > 1 ? Math.min(12, Math.max(1, Number(nums[1]))) : 1
  const day = nums.length > 2 ? Math.min(31, Math.max(1, Number(nums[2]))) : 1
  return year + (month - 1) / 12 + (day - 1) / 365
}

/** 时间轴刻度：大约 count 个整齐的刻度 */
export function niceTicks(min: number, max: number, count = 6) {
  if (!(max > min)) { min -= 1; max += 1 }
  const raw = (max - min) / count
  const mag = Math.pow(10, Math.floor(Math.log10(raw)))
  const step = [1, 2, 5, 10].map(m => m * mag).find(s => s >= raw) ?? 10 * mag
  const start = Math.floor(min / step) * step
  const end = Math.ceil(max / step) * step
  const ticks: number[] = []
  for (let v = start; v <= end + step * 0.001; v += step) ticks.push(Number(v.toFixed(6)))
  return { ticks, step, start, end }
}
