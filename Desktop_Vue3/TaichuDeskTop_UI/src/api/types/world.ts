/** 与后端 Dtos/World 的 JSON 契约对应，日期和 Guid 均通过字符串传输。 */
export interface WorldProject {
  id: string
  name: string
  description: string | null
  coverImage: string | null
  graphPositions: string | null
  isPublic: boolean
  createdAt: string
  updatedAt: string
  cardCount: number
  ownerName: string | null
  ownerId: string
  /** 协作者人数（不含创建者） */
  collaboratorCount?: number
  /** 当前用户的身份：owner 创建者 / editor 编写者 / viewer 只读成员；路人为 null */
  myRole?: WorldRole | null
}

export type WorldRole = 'owner' | 'editor' | 'viewer'
export type CollaboratorRole = Exclude<WorldRole, 'owner'>
export interface WorldMember {
  userId: string
  username: string
  avatar: string | null
  role: WorldRole
  createdAt: string | null
}
export interface WorldMembers {
  owner: WorldMember
  items: WorldMember[]
  myRole: WorldRole | null
  max: number
}
export interface WorldMemberCandidate { userId: string; username: string; avatar: string | null }

export interface CreateProjectPayload {
  name: string
  description?: string | null
  coverImage?: string | null
  graphPositions?: string | null
  isPublic?: boolean
}
export type UpdateProjectPayload = Partial<CreateProjectPayload>

export interface StartWorldRestore {
  sourceProjectId: string
  fingerprint: string
  fileName: string
  newProject: boolean
  name: string
  defaultType: string
  plainText: boolean
  cardCount: number
  images: { name: string; size: number }[]
}
export interface WorldRestoreStatus {
  id: string
  state: 'pending' | 'committed' | 'cleaning' | 'cancelled'
  targetProjectId: string
  plan: { source: StartWorldRestore; cardIds: string[] }
  images: Record<string, string>
  updatedAt: string
}

export interface CardAttribute { key: string; value: string; type: string }
export interface ContentBlock { id: string; cardId: string; cardType: string; order: number; contextLabel?: string | null }
export interface TimelineEvent {
  /** 前端生成的事件 ID（老数据没有） */
  id?: string
  date: string
  era?: string | null
  title: string
  description: string | null
  /** 事件配图 */
  image?: string | null
  tags?: string[] | null
  /** 和这件事有关的条目 */
  cardIds?: string[] | null
  /** 对访客隐藏 */
  hidden?: boolean
  /** 事件颜色（区分战争、灾难、建国……） */
  color?: string | null
}
export interface TimelineItem {
  cardId: string
  cardTitle: string
  cardType: string
  date: string
  era?: string | null
  title: string
  description: string | null
  image?: string | null
  cardIds?: string[] | null
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
  /** 区块排列 JSON（见 utils/worldLayout.ts）；null = 老条目，按默认顺序 */
  layout?: string | null
  outRelations: Relation[]
  inRelations: Relation[]
}

export interface CreateCardPayload {
  relations?: RelationInput[]
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
  layout?: string | null
}
export type UpdateCardPayload = Partial<CreateCardPayload> & { expectedUpdatedAt?: string }

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

/** 条目修改记录 */
export type RevisionAction = 'create' | 'update' | 'delete' | 'restore' | 'baseline'
export interface WorldRevision {
  id: number
  cardId: string
  userId: string | null
  username: string | null
  avatar: string | null
  action: RevisionAction
  title: string
  note: string | null
  createdAt: string
  cardExists: boolean
}
export interface WorldCardSnapshot {
  title: string
  type: string
  subType?: string | null
  coverImage?: string | null
  galleryImages: string
  aliases: string
  attributes: string
  description?: string | null
  contentBlocks: string
  timelineEvents: string
  tags: string
  embeddedCards: string
  content: string
  out: { cardId: string; relationType: string }[]
  in: { cardId: string; relationType: string }[]
}
export interface WorldRevisionDetail { id: number; action: RevisionAction; before: WorldCardSnapshot | null; after: WorldCardSnapshot | null }
export interface WorldFieldBlame { username: string | null; at: string; count: number; editors: number }
export interface WorldCardBlame {
  createdBy: string | null
  createdAt: string | null
  totalEdits: number
  trackedFromStart: boolean
  editors: { userId: string; username: string | null; count: number; lastAt: string }[]
  /** title / type / description / aliases / tags / cover / gallery / timeline / relations / blocks / attr:属性名 / field:表单字段名 */
  fields: Record<string, WorldFieldBlame>
}
export interface WorldLastEditor { cardId: string; userId: string | null; username: string | null; action: RevisionAction; createdAt: string }

