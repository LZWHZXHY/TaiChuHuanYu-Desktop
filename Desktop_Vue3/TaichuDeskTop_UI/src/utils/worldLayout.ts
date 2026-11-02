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
 * - canvas      画布（自由白板：便签、图片、条目卡片、连线） - tree 关系树（家谱、组织架构）
 * - statpanel   能力面板（多维雷达图，像 JOJO 替身面板；数据在块的 panel 里，格式见 utils/statPanel.ts）
 * 只出现一次的区块用固定 ID（= 种类名），老条目按固定规则排好，打开再保存不会产生多余修改。
 *
 * 不认识的区块（以后的新版网站加的种类，而这个页面还是旧的）：不能丢！
 * 读的时候整块原样包成 kind = 'unknown'（原始 JSON 放在 raw 里），编辑器里显示「需要刷新页面」的占位、可以移动和删除，
 * 保存时（serializeLayout）再原样放回去，所以旧页面打开再保存也不会把新区块弄丢。
 */
import { cleanStatPanel, createStatPanel, type StatPanel } from './statPanel'

export type BlockKind = 'description' | 'text' | 'media' | 'gallery' | 'timeline' | 'embeds' | 'relations' | 'map' | 'sources' | 'canvas' | 'tree' | 'statpanel'
  /** 这个页面不认识的区块（新版网站加的），原样保留，见文件开头的说明 */
  | 'unknown'
/** 参考来源的一条：名称、链接（可选）、备注（可选，比如第几页、第几分钟） */
export interface SourceItem { id: string; title: string; url?: string; note?: string }
/** 地图上的图钉：位置是相对底图的比例（0～1），可以连到一个条目 */
export interface MapPin { id: string; x: number; y: number; cardId?: string | null; label?: string; color?: string; note?: string
  /** 和连到的条目是什么关系：首都、战场遗址…… */
  relation?: string
  /** 多层地图：点这个图钉可以「进入」另一张地图（某个条目的某个地图区块），比如大陆地图上的王都 → 王都地图 */
  subMap?: MapTarget }
/** 指向某个条目里的某个地图区块（一个条目可以有好几张地图，所以要记区块 ID） */
export interface MapTarget { cardId: string; blockId: string }

/** 画布上的一样东西：便签（文字 + 颜色）、图片、条目卡片。位置和大小是画布上的像素（不随缩放变） */
export type CanvasItemType = 'note' | 'image' | 'card'
export interface CanvasItem {
  id: string
  type: CanvasItemType
  x: number
  y: number
  w: number
  h: number
  /** 便签的文字 */
  text?: string
  /** 便签的颜色 */
  color?: string
  /** 图片地址 */
  image?: string
  /** 条目卡片连到的条目 */
  cardId?: string | null
}
/** 画布上两样东西之间的连线，可以写一句说明，可以带箭头 */
export interface CanvasLink { id: string; from: string; to: string; label?: string; arrow?: boolean }
/** 关系树的一个节点：可以连到条目，也可以只写名字；parentId 为空就是最上面一层。
 *  同一个父节点下面孩子的先后顺序 = 在数组里的先后顺序 */
export interface TreeNode {
  id: string
  parentId?: string | null
  cardId?: string | null
  /** 没连条目时显示的名字 */
  label?: string
  /** 和父节点的关系：长子、副官…… */
  relation?: string
  /** 小字备注：生卒年、职位…… */
  note?: string
}
/** 画布、关系树的数量上限（整个条目的排列后端限制 512K 字，留足余量） */
export const CANVAS_MAX_ITEMS = 300
export const CANVAS_MAX_LINKS = 600
export const TREE_MAX_NODES = 300
/** 便签颜色（浅色底，深色模式下也看得清字，字固定用深色） */
export const NOTE_COLORS = ['#fff4b8', '#ffd9d2', '#d6f0d8', '#d4e8f7', '#e6dcf7', '#f1f1ee']

/** 图钉、时间线事件共用的颜色（每种颜色可以代表一类：战争、灾难、建国……） */
export const MARK_COLORS = ['#2a7f8f', '#d9534f', '#e0a526', '#5b8c3a', '#7a5cc4', '#3d4b5c', '#c46a9a', '#8a6a3a']
export interface LayoutBlock {
  id: string
  kind: BlockKind
  title?: string
  /** 隐藏标题栏 */
  hideTitle?: boolean
  /** 对访客隐藏（HIDEABLE_KINDS 里的才能隐藏，服务器不会发给访客） */
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
  /** canvas 块：画布上的东西、连线、画布高度（像素） */
  items?: CanvasItem[]
  links?: CanvasLink[]
  height?: number
  /** tree 块：关系树的节点 */
  nodes?: TreeNode[]
  /** statpanel 块：能力面板 */
  panel?: StatPanel
  /** unknown 块：原始 JSON（保存时原样放回去） */
  raw?: Record<string, unknown>
}
export interface LayoutRow { id: string; blocks: LayoutBlock[] }
export interface CardLayout { v: 1; rows: LayoutRow[] }

/** 「添加区块」菜单里的种类（不含 unknown：那个只用来保留不认识的区块） */
export const BLOCK_KINDS: BlockKind[] = ['description', 'text', 'media', 'map', 'canvas', 'tree', 'statpanel', 'gallery', 'timeline', 'embeds', 'relations', 'sources']
export const SINGLETON_KINDS: BlockKind[] = ['description', 'gallery', 'timeline', 'embeds', 'relations']
export const HIDEABLE_KINDS: BlockKind[] = ['text', 'media', 'map', 'sources', 'canvas', 'tree', 'statpanel']
export const BLOCK_META: Record<BlockKind, { label: string; icon: string; hint: string; keywords: string }> = {
  description: { label: '正文', icon: '¶', hint: '主要的设定描述', keywords: 'zhengwen 描述 简介' },
  text: { label: '文本', icon: '≡', hint: '自己起标题：背景、性格、外貌……', keywords: 'wenben text 背景 性格 外貌 笔记' },
  media: { label: '媒体', icon: '▣', hint: '放一张或几张图片', keywords: 'meiti image 图片 照片' },
  map: { label: '地图', icon: '⌖', hint: '上传地图，插图钉标出地点', keywords: 'ditu map 地图 图钉 坐标' },
  canvas: { label: '画布', icon: '⬚', hint: '自由白板：便签、图片、条目卡片，还能连线', keywords: 'huabu canvas 白板 便签 思维导图 连线 脑图' },
  tree: { label: '关系树', icon: '⋔', hint: '家谱、组织架构：一层层往下排', keywords: 'guanxishu tree 家谱 族谱 组织架构 树 上下级' },
  statpanel: { label: '能力面板', icon: '✦', hint: '多维雷达图：JOJO 替身面板、六维属性，也能自定义', keywords: 'nengli mianban radar 雷达图 能力 属性 六维 替身 jojo 等级 数值' },
  gallery: { label: '图库', icon: '▦', hint: '条目的图集', keywords: 'tuku gallery 图片' },
  timeline: { label: '时间线', icon: '⟶', hint: '按时间排列的事件', keywords: 'shijianxian timeline 事件 经历 年表' },
  embeds: { label: '引用设定', icon: '⧉', hint: '在这里展示其他条目', keywords: 'yinyong embed 嵌入' },
  relations: { label: '设定关系', icon: '⇄', hint: '居住地、所属组织……', keywords: 'guanxi relation 关联' },
  sources: { label: '参考来源', icon: '❡', hint: '记下参考的资料和链接，方便以后查（默认只有成员看得到）', keywords: 'cankao laiyuan source 参考 资料 出处 链接 引用' },
  unknown: { label: '新版区块', icon: '?', hint: '新版网站加的区块，刷新页面后才能显示和编辑', keywords: '' },
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
  if (!raw || typeof raw !== 'object' || typeof raw.kind !== 'string' || !raw.kind) return null
  // 不认识的种类：整块原样留着（以前这里直接返回 null，旧页面保存时会把新区块弄丢）
  if (!BLOCK_KINDS.includes(raw.kind)) return keepUnknown(raw)
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
      const sub = cleanTarget(p.subMap)
      if (sub) pin.subMap = sub
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
  if (kind === 'canvas') {
    // 先放进局部变量再赋值：block.items 是可选字段，直接用会被类型检查认为可能是 undefined
    const items: CanvasItem[] = (Array.isArray(raw.items) ? raw.items : []).map(cleanCanvasItem).filter((x: CanvasItem | null): x is CanvasItem => !!x).slice(0, CANVAS_MAX_ITEMS)
    block.items = items
    const ids = new Set(items.map(x => x.id))
    block.links = (Array.isArray(raw.links) ? raw.links : [])
      // 两头都要在画布上（东西删了，连线跟着去掉）
      .filter((l: any) => l && typeof l.from === 'string' && typeof l.to === 'string' && l.from !== l.to && ids.has(l.from) && ids.has(l.to))
      .map((l: any): CanvasLink => {
        const link: CanvasLink = { id: String(l.id || newId('l')), from: l.from, to: l.to }
        if (typeof l.label === 'string' && l.label.trim()) link.label = l.label.slice(0, 60)
        if (l.arrow) link.arrow = true
        return link
      }).slice(0, CANVAS_MAX_LINKS)
    if (Number.isFinite(raw.height)) block.height = clampInt(raw.height, 240, 1200)
  }
  if (kind === 'tree') block.nodes = cleanTreeNodes(raw.nodes)
  if (kind === 'statpanel') block.panel = cleanStatPanel(raw.panel) ?? createStatPanel('jojo')
  return block
}

/**
 * 把不认识的区块包起来。两种来源：
 * - 服务器上的新种类区块（raw 就是那一块原始 JSON）；
 * - 本地草稿里已经包过一次的（kind = 'unknown'、原始 JSON 在 raw.raw 里），直接还原，不要包两层。
 * 标题、隐藏标题、对访客隐藏这几项拿出来，编辑器标题栏能正常显示。
 */
function keepUnknown(raw: any): LayoutBlock | null {
  const original: Record<string, unknown> | null = raw.kind === 'unknown'
    ? (raw.raw && typeof raw.raw === 'object' && !Array.isArray(raw.raw) && typeof raw.raw.kind === 'string' ? raw.raw : null)
    : raw
  if (!original) return null
  const copy: Record<string, unknown> = JSON.parse(JSON.stringify(original))
  const block: LayoutBlock = { id: String(raw.id || copy.id || newId()), kind: 'unknown', raw: copy }
  // 草稿里包过的：标题以外层（用户可能改过）为准；新读到的：取原始块里的
  const source = raw.kind === 'unknown' ? raw : copy
  if (typeof source.title === 'string' && source.title.trim()) block.title = source.title.slice(0, 60)
  if (source.hideTitle) block.hideTitle = true
  if (copy.hidden) block.hidden = true
  return block
}

const clampInt = (v: unknown, min: number, max: number) => Math.round(Math.min(max, Math.max(min, Number(v))))
const isColor = (c: unknown): c is string => typeof c === 'string' && /^#[0-9a-f]{3,8}$/i.test(c)

function cleanTarget(raw: any): MapTarget | null {
  if (!raw || typeof raw !== 'object' || typeof raw.cardId !== 'string' || typeof raw.blockId !== 'string' || !raw.cardId || !raw.blockId) return null
  return { cardId: raw.cardId.slice(0, 64), blockId: raw.blockId.slice(0, 64) }
}

function cleanCanvasItem(raw: any): CanvasItem | null {
  if (!raw || typeof raw !== 'object' || !['note', 'image', 'card'].includes(raw.type) || !Number.isFinite(raw.x) || !Number.isFinite(raw.y)) return null
  const type = raw.type as CanvasItemType
  // 位置限制在 ±2 万像素以内，大小按种类给个最小值，取整（保存的内容稳定，打开再保存不会出现多余修改）
  const min = CANVAS_MIN_SIZE[type]
  const item: CanvasItem = {
    id: String(raw.id || newId('c')), type,
    x: clampInt(raw.x, -20000, 20000), y: clampInt(raw.y, -20000, 20000),
    w: clampInt(Number.isFinite(raw.w) ? raw.w : CANVAS_DEFAULT_SIZE[type].w, min.w, 1600),
    h: clampInt(Number.isFinite(raw.h) ? raw.h : CANVAS_DEFAULT_SIZE[type].h, min.h, 1600),
  }
  if (type === 'note') {
    item.text = typeof raw.text === 'string' ? raw.text.slice(0, 1000) : ''
    if (isColor(raw.color)) item.color = raw.color
  }
  if (type === 'image') {
    if (typeof raw.image !== 'string' || !raw.image) return null
    item.image = raw.image.slice(0, 1000)
  }
  if (type === 'card') item.cardId = typeof raw.cardId === 'string' && raw.cardId ? raw.cardId : null
  return item
}
/** 画布上每种东西的默认大小、最小大小 */
export const CANVAS_DEFAULT_SIZE: Record<CanvasItemType, { w: number; h: number }> = { note: { w: 200, h: 140 }, image: { w: 240, h: 180 }, card: { w: 220, h: 72 } }
export const CANVAS_MIN_SIZE: Record<CanvasItemType, { w: number; h: number }> = { note: { w: 80, h: 48 }, image: { w: 60, h: 40 }, card: { w: 140, h: 56 } }

/** 关系树节点：父节点不存在、指向自己、绕成圈的，都改成最上面一层 */
export function cleanTreeNodes(raw: unknown): TreeNode[] {
  const list: TreeNode[] = (Array.isArray(raw) ? raw : []).filter((n: any) => n && typeof n === 'object').map((n: any): TreeNode => {
    const node: TreeNode = { id: String(n.id || newId('n')) }
    if (typeof n.parentId === 'string' && n.parentId) node.parentId = n.parentId
    if (typeof n.cardId === 'string' && n.cardId) node.cardId = n.cardId
    if (typeof n.label === 'string' && n.label.trim()) node.label = n.label.slice(0, 60)
    if (typeof n.relation === 'string' && n.relation.trim()) node.relation = n.relation.slice(0, 30)
    if (typeof n.note === 'string' && n.note.trim()) node.note = n.note.slice(0, 120)
    return node
  })
  const seen = new Set<string>()
  const nodes = list.filter(n => !seen.has(n.id) && (seen.add(n.id), true)).slice(0, TREE_MAX_NODES)
  const byId = new Map(nodes.map(n => [n.id, n]))
  for (const node of nodes) {
    if (node.parentId && (node.parentId === node.id || !byId.has(node.parentId))) delete node.parentId
  }
  // 顺着父节点往上走，回到自己就说明绕圈了：把自己挪到最上层断开
  for (const node of nodes) {
    const path = new Set<string>([node.id])
    let up = node.parentId ? byId.get(node.parentId) : undefined
    while (up) {
      if (path.has(up.id)) { delete node.parentId; break }
      path.add(up.id)
      up = up.parentId ? byId.get(up.parentId) : undefined
    }
  }
  return nodes
}

/** 排列里所有连到的条目 ID（地图图钉、下一层地图、画布卡片、关系树节点），换世界导入时用来换 ID */
export function remapLayoutCards(layout: CardLayout, target: (id: string) => string) {
  for (const block of layout.rows.flatMap(r => r.blocks)) {
    for (const pin of block.pins ?? []) {
      if (pin.cardId) pin.cardId = target(pin.cardId)
      if (pin.subMap) pin.subMap = { ...pin.subMap, cardId: target(pin.subMap.cardId) }
    }
    for (const item of block.items ?? []) if (item.cardId) item.cardId = target(item.cardId)
    for (const node of block.nodes ?? []) if (node.cardId) node.cardId = target(node.cardId)
  }
  return layout
}

/** 一个条目里的所有地图区块（多层地图选「下一层」用） */
export function mapBlocksOf(layoutJson: string | null | undefined) {
  return (parseLayout(layoutJson)?.rows.flatMap(r => r.blocks) ?? []).filter(b => b.kind === 'map')
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
  // 不认识的区块放回原始 JSON（只把编号、标题、隐藏标题换成编辑器里现在的），一个字段都不丢
  const rows = layout.rows.map(row => ({
    ...row,
    blocks: row.blocks.map(block => {
      if (block.kind !== 'unknown' || !block.raw) return block
      const out: Record<string, unknown> = { ...block.raw, id: block.id }
      if (block.title) out.title = block.title; else delete out.title
      if (block.hideTitle) out.hideTitle = true; else delete out.hideTitle
      return out
    }),
  }))
  return JSON.stringify({ ...layout, rows })
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
  if (kind === 'canvas') { block.items = []; block.links = [] }
  if (kind === 'tree') block.nodes = []
  // 能力面板：默认 JOJO 替身面板，编辑器里可以换模板
  if (kind === 'statpanel') block.panel = createStatPanel('jojo')
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

/** 复制文本块 / 图片块 / 地图 / 画布 / 关系树（放在下面） */
export function duplicateBlock(layout: CardLayout, blockId: string) {
  const at = findBlock(layout, blockId)
  // 不认识的区块不复制（不知道里面有没有不能重复的东西）
  if (!at || isSingleton(at.block.kind) || at.block.kind === 'unknown') return null
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
