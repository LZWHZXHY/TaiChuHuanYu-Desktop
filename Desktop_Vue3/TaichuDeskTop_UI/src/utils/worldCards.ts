import type { CardDetail, CardSummary, Relation, RelationInput } from '@/api/types/world'

export function toCardSummary(card: CardDetail): CardSummary {
  return {
    id: card.id, projectId: card.projectId, title: card.title, type: card.type,
    coverImage: card.coverImage, updatedAt: card.updatedAt,
    outRelationCount: card.outRelations.length, inRelationCount: card.inRelations.length,
  }
}

/** 嵌入预览的占位数据不写入详情缓存。 */
export function createCardPreview(card: CardSummary): CardDetail {
  return {
    ...card, createdAt: card.updatedAt, subType: null, description: '', content: '{}',
    aliases: [], attributes: [], contentBlocks: [], timelineEvents: [], tags: [],
    embeddedCards: [], galleryImages: [], outRelations: [], inRelations: [],
  }
}

export function diffRelations(existing: Relation[], desired: RelationInput[]) {
  const matches = (a: RelationInput, b: RelationInput) =>
    a.targetCardId === b.targetCardId && a.relationType === b.relationType
  return {
    toRemove: existing.filter(old => !desired.some(next => matches(old, next))),
    toAdd: desired.filter(next => !existing.some(old => matches(old, next))),
  }
}
