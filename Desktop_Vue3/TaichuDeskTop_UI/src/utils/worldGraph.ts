import type { CardDetail, CardSummary, Relation } from '@/api/types/world'
import { CardTypeMeta, type CardType } from '@/views/世界观管理/card_type'

// 保留图谱与编辑器原有的配色差异，其余类型使用共用元信息。
const graphColors: Record<string, string> = { organization: '#7c3aed', creature: '#84cc16', concept: '#6366f1' }
export const getGraphTypeColor = (type: string) => graphColors[type] || CardTypeMeta[type as CardType]?.color || '#64748b'
export interface WorldGraphNode { id: string; title: string; type: string; color: string; x?: number; y?: number; z?: number }
export interface WorldGraphEdge { source: string; target: string; label: string; color: string; width: number; isEmbed: boolean }

export function buildWorldGraph(cards: CardSummary[], relations: Relation[], findDetail: (id: string) => CardDetail | null | undefined) {
  const nodes: WorldGraphNode[] = cards.map(card => ({ id: card.id, title: card.title, type: card.type, color: getGraphTypeColor(card.type) }))
  const ids = new Set(cards.map(card => card.id))
  const relationKeys = new Set<string>()
  const links: WorldGraphEdge[] = []
  for (const relation of relations) {
    if (!ids.has(relation.sourceCardId) || !ids.has(relation.targetCardId)) continue
    relationKeys.add(`${relation.sourceCardId}__${relation.targetCardId}`)
    links.push({ source: relation.sourceCardId, target: relation.targetCardId, label: relation.relationType,
      color: '#94a3b8', width: 1.5, isEmbed: false })
  }
  for (const card of cards) {
    for (const block of findDetail(card.id)?.contentBlocks ?? []) {
      if (!block.cardId || block.cardId === card.id || !ids.has(block.cardId)) continue
      if (relationKeys.has(`${card.id}__${block.cardId}`)) continue
      links.push({ source: card.id, target: block.cardId, label: '', color: '#cbd5e1', width: 0.8, isEmbed: true })
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
