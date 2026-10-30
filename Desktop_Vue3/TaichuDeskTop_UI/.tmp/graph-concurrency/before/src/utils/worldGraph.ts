import type { CardDetail, CardSummary, Relation } from '@/api/types/world'
import { CardTypeMeta, type CardType } from '@/views/世界观管理/card_type'
import { getEmbeddedCardIds } from './worldPresentation'

// 保留图谱与编辑器原有的配色差异，其余类型使用共用元信息。
const graphColors: Record<string, string> = { organization: '#7c3aed', creature: '#84cc16', concept: '#6366f1' }
export const getGraphTypeColor = (type: string) => graphColors[type] || CardTypeMeta[type as CardType]?.color || '#64748b'

// 关系类型配色（按字符串哈希取稳定色，保证同一种关系颜色一致）
const relationPalette = ['#ef4444', '#f59e0b', '#10b981', '#3b82f6', '#8b5cf6', '#ec4899', '#14b8a6', '#f97316', '#6366f1', '#84cc16']
export function getRelationTypeColor(type: string) {
  if (!type) return '#94a3b8'
  let hash = 0
  for (const ch of type) hash = (hash * 31 + ch.charCodeAt(0)) >>> 0
  return relationPalette[hash % relationPalette.length]
}

export interface WorldGraphNode { id: string; title: string; type: string; color: string; x?: number; y?: number; z?: number }
export interface WorldGraphEdge { source: string; target: string; label: string; color: string; width: number; isEmbed: boolean }

export interface GraphLabelPoint { id: string; x: number; y: number; z: number; width: number; priority?: boolean }
/** 固定 32px 行高，按屏幕网格避让；隐藏名称不移除节点或关系。 */
export function placeGraphLabels(points: GraphLabelPoint[], width: number, height: number) {
  const boxes: (GraphLabelPoint & { left: number; top: number })[] = []
  const grid = new Map<string, typeof boxes>()
  const limit = Math.min(120, Math.max(6, Math.floor(width * height / 5500)))
  for (const point of [...points].sort((a, b) => Number(Boolean(b.priority)) - Number(Boolean(a.priority)) || a.z - b.z || a.id.localeCompare(b.id))) {
    if (point.x < 0 || point.x > width || point.y < 0 || point.y > height) continue
    const left = Math.max(4, Math.min(width - point.width - 4, point.x - point.width / 2))
    for (const top of [point.y - 40, point.y + 8]) {
      if (!Number.isFinite(left + top) || left < 4 || top < 4 || left + point.width > width - 4 || top + 32 > height - 4) continue
      const cells: string[] = []
      for (let x = Math.floor((left - 4) / 64); x <= Math.floor((left + point.width + 4) / 64); x++)
        for (let y = Math.floor((top - 4) / 64); y <= Math.floor((top + 36) / 64); y++) cells.push(`${x}:${y}`)
      if (cells.some(cell => grid.get(cell)?.some(box => left < box.left + box.width + 4 && left + point.width + 4 > box.left && top < box.top + 36 && top + 36 > box.top))) continue
      const box = { ...point, left, top }; boxes.push(box)
      for (const cell of cells) { if (!grid.has(cell)) grid.set(cell, []); grid.get(cell)!.push(box) }
      break
    }
    if (boxes.length >= limit) break
  }
  return boxes
}

export interface GraphBox { left: number; top: number; width: number; height: number }
export const graphBoxesOverlap = (a: GraphBox, b: GraphBox, gap = 4) =>
  a.left < b.left + b.width + gap && a.left + a.width + gap > b.left && a.top < b.top + b.height + gap && a.top + a.height + gap > b.top

/** 沿连线寻找空白位置；放不下就省略关系名，连线始终保留。 */
export function placeEdgeLabel(a: { x: number; y: number }, b: { x: number; y: number }, control: { x: number; y: number }, textWidth: number, width: number, height: number, occupied: GraphBox[]) {
  for (const t of [.5, .35, .65, .25, .75]) {
    const x = (1-t)**2*a.x + 2*(1-t)*t*control.x + t*t*b.x
    const y = (1-t)**2*a.y + 2*(1-t)*t*control.y + t*t*b.y
    const box = { left: x - textWidth / 2, top: y - 10, width: textWidth, height: 20 }
    if (box.left < 4 || box.top < 4 || box.left + box.width > width - 4 || box.top + box.height > height - 4) continue
    if (occupied.some(other => graphBoxesOverlap(box, other))) continue
    occupied.push(box)
    return { x, y, box }
  }
  return null
}

export function buildWorldGraph(cards: CardSummary[], relations: Relation[], findDetail: (id: string) => CardDetail | null | undefined) {
  const nodes: WorldGraphNode[] = cards.map(card => ({ id: card.id, title: card.title, type: card.type, color: getGraphTypeColor(card.type) }))
  const ids = new Set(cards.map(card => card.id))
  const relationKeys = new Set<string>()
  const links: WorldGraphEdge[] = []
  for (const relation of relations) {
    if (!ids.has(relation.sourceCardId) || !ids.has(relation.targetCardId)) continue
    relationKeys.add(`${relation.sourceCardId}__${relation.targetCardId}`)
    links.push({ source: relation.sourceCardId, target: relation.targetCardId, label: relation.relationType,
      color: getRelationTypeColor(relation.relationType), width: 1.5, isEmbed: false })
  }
  for (const card of cards) {
    for (const target of getEmbeddedCardIds(findDetail(card.id), true)) {
      if (target === card.id || !ids.has(target)) continue
      if (relationKeys.has(`${card.id}__${target}`)) continue
      links.push({ source: card.id, target, label: '引用', color: '#94a3b8', width: 0.8, isEmbed: true })
    }
  }
  return { nodes, links }
}

export function getNodeCameraPosition(node: Pick<WorldGraphNode, 'x' | 'y' | 'z'>) {
  const target = { x: node.x || 0, y: node.y || 0, z: node.z || 0 }
  const length = Math.hypot(target.x, target.y, target.z)
  if (!length) return { position: { x: 0, y: 0, z: 100 }, target }
  const ratio = 1 + 100 / length
  return { position: { x: target.x * ratio, y: target.y * ratio, z: target.z * ratio }, target }
}

export function getGraphCameraPosition(nodes: WorldGraphNode[], aspect: number, fov: number) {
  const axes = ['x', 'y', 'z'] as const
  const center = { x: 0, y: 0, z: 0 }
  for (const axis of axes) {
    const values = nodes.map(node => node[axis] || 0)
    center[axis] = values.length ? (Math.min(...values) + Math.max(...values)) / 2 : 0
  }
  // 用节点位置计算范围，并为标签留出边距。
  const radius = Math.max(30, ...nodes.map(node => Math.hypot(...axes.map(axis => (node[axis] || 0) - center[axis])) + 30))
  const halfFov = fov * Math.PI / 360
  const angle = Math.min(halfFov, Math.atan(Math.tan(halfFov) * Math.max(aspect, 0.1)))
  const distance = radius / Math.sin(angle)
  return { target: center, position: { x: center.x + distance * 0.6, y: center.y + distance * 0.36, z: center.z + distance * 0.715 } }
}
