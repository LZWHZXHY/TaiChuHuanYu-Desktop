/** 与后端 Dtos/World 的 JSON 契约对应，日期和 Guid 均通过字符串传输。 */
export interface WorldProject {
  id: string
  name: string
  description: string | null
  isPublic: boolean
  createdAt: string
  updatedAt: string
  cardCount: number
  ownerName: string | null
  ownerId: string
}

export interface CreateProjectPayload {
  name: string
  description?: string | null
  isPublic?: boolean
}
export type UpdateProjectPayload = Partial<CreateProjectPayload>

export interface CardAttribute { key: string; value: string; type: string }
export interface ContentBlock { id: string; cardId: string; cardType: string; order: number }
export interface TimelineEvent { date: string; era?: string | null; title: string; description: string | null }
export interface TimelineItem {
  cardId: string
  cardTitle: string
  cardType: string
  date: string
  era?: string | null
  title: string
  description: string | null
}
export interface WorldSearchResult {
  cardId: string
  cardTitle: string
  cardType: string
  projectId: string
  projectName: string
}
export interface RelationInput { targetCardId: string; relationType: string }
export interface Relation extends RelationInput {
  id: string
  sourceCardId: string
  createdAt: string
  sourceCardTitle: string | null
  targetCardTitle: string | null
  sourceCardType: string | null
  targetCardType: string | null
}

export interface CardSummary {
  id: string
  projectId: string
  title: string
  type: string
  coverImage: string | null
  updatedAt: string
  outRelationCount: number
  inRelationCount: number
}

/** 详情接口返回关系列表，数量仅存在于精简列表 DTO 中。 */
export interface CardDetail extends Omit<CardSummary, 'outRelationCount' | 'inRelationCount'> {
  subType: string | null
  createdAt: string
  description: string | null
  content: string
  contentBlocks: ContentBlock[]
  timelineEvents: TimelineEvent[]
  aliases: string[]
  attributes: CardAttribute[]
  tags: string[]
  embeddedCards: string[]
  galleryImages: string[]
  outRelations: Relation[]
  inRelations: Relation[]
}

export interface CreateCardPayload {
  title: string
  type: string
  subType?: string | null
  coverImage?: string | null
  description?: string | null
  content?: string | null
  aliases?: string[]
  attributes?: CardAttribute[]
  contentBlocks?: ContentBlock[]
  timelineEvents?: TimelineEvent[]
  tags?: string[]
  embeddedCards?: string[]
  galleryImages?: string[]
}
export type UpdateCardPayload = Partial<CreateCardPayload>
export type CardSavePayload<T = CreateCardPayload> = T & { relations?: RelationInput[] }

export interface CardTypeDto {
  id: string
  label: string
  icon: string | null
  description: string | null
  sortOrder: number
  isActive: boolean
  isSystem: boolean
  createdAt: string
  updatedAt: string | null
}
export interface CardTypeOption { value: string; label: string; icon?: string }
export interface WorldQuota {
  usedWorldCount: number
  maxWorldCount: number
  remainingWorldCount: number
  maxCardsPerWorld: number
  experience: number
  expCostPerWorldSlot: number
  expCostPer10Cards: number
}
export interface ProjectQuotaCheck { canCreate: boolean; message: string; used: number; max: number }
export interface CardQuotaCheck { canAdd: boolean; message: string; currentCount: number; maxCount: number }
export type QuotaUpgradeType = 'WorldCount' | 'WorldCardCapacity'
export interface QuotaUpgradeResult {
  success: boolean
  message: string
  newValue: number
  previousValue: number
  costExp: number
  remainingExp: number
  upgradeType: string
}
export interface QuotaHistory {
  upgradeType: QuotaUpgradeType
  amount: number
  costExp: number
  previousValue: number
  newValue: number
  createdAt: string
}
