import type { CardDetail, CardSummary } from '@/api/types/world'

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

