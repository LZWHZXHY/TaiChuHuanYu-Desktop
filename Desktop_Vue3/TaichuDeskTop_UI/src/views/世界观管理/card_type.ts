// src/views/世界观管理/card_type.ts

// ============================================================
//  1. 卡片类型枚举（共 12 种）
// ============================================================
export type CardType = 
  | 'character' 
  | 'location' 
  | 'item' 
  | 'event' 
  | 'faction' 
  | 'species' 
  | 'occupation'
  | 'organization'
  | 'creature'
  | 'skill'
  | 'climate'
  | 'concept'   // ✅ 新增：抽象概念/设定

// ============================================================
//  2. 共用基础字段
// ============================================================
export interface ContentBlock {
  id: string
  cardId: string
  cardType: string
  order: number
  cardTitle?: string
  cardCover?: string
  cardSummary?: string
  cardAttributes?: { key: string; value: string }[]
  contextLabel?: string  // 关系描述，如"出生地""武器""所属势力"
}

export interface AttributeItem {
  key: string
  value: any
  type: 'short' | 'long' | 'number' | 'date' | 'boolean'  // 必选
}


export interface BaseCardData {
  id: string
  projectId: string
  title: string
  type: CardType
  coverImage?: string
  galleryImages?: string[]
  attributes: AttributeItem[] 
  description: string
  tags: string[]
  relations: { targetCardId: string; relationType: string }[]
  contentBlocks: ContentBlock[]
  createdAt: string
  updatedAt: string
}

// ============================================================
//  3. 各类型专属字段
// ============================================================

// 3.1 角色卡片（合并：神祇）
export interface CharacterData extends BaseCardData {
  type: 'character'
  // 所有字段通过自定义属性 + 内容块扩展
}

// 3.2 地点卡片（合并：国家、大陆、建筑）
export interface LocationData extends BaseCardData {
  type: 'location'
  coordinate?: { x: number; y: number }  // 地图坐标
  dangerLevel?: '低' | '中' | '高' | '极度危险'  // 按钮组
}

// 3.3 物品卡片（合并：武器）
export interface ItemData extends BaseCardData {
  type: 'item'
  rarity?: '普通' | '稀有' | '史诗' | '传说' | '神器'
}

// 3.4 事件卡片
export interface EventData extends BaseCardData {
  type: 'event'
  startDate?: string
  endDate?: string
  locationId?: string
  outcome?: string
  significance?: string
}

// 3.5 派系卡片
export interface FactionData extends BaseCardData {
  type: 'faction'
  ideology?: string
  headquarters?: string
  foundedDate?: string
  size?: '小型' | '中型' | '大型' | '巨型'
}

// 3.6 物种卡片
export interface SpeciesData extends BaseCardData {
  type: 'species'
  // 所有字段通过自定义属性 + 内容块扩展
}

// 3.7 职业卡片
export interface OccupationData extends BaseCardData {
  type: 'occupation'
  rank?: '初级' | '中级' | '高级' | '大师' | '传说'
  requirements?: string
  abilities?: string[]
  equipment?: string[]
  affiliation?: string
  advancement?: string[]
}

// 3.8 组织卡片
export interface OrganizationData extends BaseCardData {
  type: 'organization'
  purpose?: string
  headquarters?: string
  foundedDate?: string
  size?: '小型' | '中型' | '大型' | '巨型'
}

// 3.9 生物卡片
export interface CreatureData extends BaseCardData {
  type: 'creature'
  habitat?: string
  diet?: string
  temperament?: string
  abilities?: string[]
  threatLevel?: '低' | '中' | '高' | '极度危险'
}

// 3.10 技能卡片
export interface SkillData extends BaseCardData {
  type: 'skill'
  skillType?: '主动' | '被动' | '终极' | '天赋'
  cost?: string
  cooldown?: string
  effect?: string
  prerequisite?: string
  level?: number
}

// 3.11 气候卡片
export interface ClimateData extends BaseCardData {
  type: 'climate'
  climateName: string
  alternativeNames?: string[]
  manifestations: string[]
  frequency: string
  predictability: string
  cause: string
  effects: string[]
  aftermath: string[]
  countermeasures: string[]
  safeZones?: string
}

// ✅ 3.12 概念卡片（抽象设定）
export interface ConceptData extends BaseCardData {
  type: 'concept'
  // 所有字段通过自定义属性 + 内容块扩展
}

// ============================================================
//  4. 类型联合
// ============================================================
export type AnyCardData = 
  | CharacterData
  | LocationData
  | ItemData
  | EventData
  | FactionData
  | SpeciesData
  | OccupationData
  | OrganizationData
  | CreatureData
  | SkillData
  | ClimateData
  | ConceptData   // ✅ 新增

// ============================================================
//  5. 卡片类型元信息
// ============================================================
export const CardTypeMeta: Record<CardType, {
  label: string
  icon: string
  color: string
  description: string
}> = {
  character: {
    label: '角色',
    icon: '👤',
    color: '#4f46e5',
    description: '人物、NPC、英雄、反派、神明、神灵'
  },
  location: {
    label: '地点',
    icon: '📍',
    color: '#059669',
    description: '城市、国家、大陆、建筑、遗迹、自然景观'
  },
  item: {
    label: '物品',
    icon: '📦',
    color: '#d97706',
    description: '武器、防具、道具、药水、消耗品、材料'
  },
  event: {
    label: '事件',
    icon: '📖',
    color: '#dc2626',
    description: '历史事件、战役、重大转折'
  },
  faction: {
    label: '派系',
    icon: '🏛️',
    color: '#8b5cf6',
    description: '势力、阵营、家族'
  },
  species: {
    label: '物种',
    icon: '🐉',
    color: '#f59e0b',
    description: '种族、生物种类'
  },
  occupation: {
    label: '职业',
    icon: '⚔️',
    color: '#f97316',
    description: '职业、身份、角色定位'
  },
  organization: {
    label: '组织',
    icon: '🤝',
    color: '#8b5cf6',
    description: '公会、公司、机构、宗教'
  },
  creature: {
    label: '生物',
    icon: '🐾',
    color: '#f59e0b',
    description: '具体生物个体、野兽、怪物'
  },
  skill: {
    label: '技能',
    icon: '💥',
    color: '#06b6d4',
    description: '技能、魔法、能力'
  },
  climate: {
    label: '气候',
    icon: '🌀',
    color: '#0ea5e9',
    description: '气候现象、天灾、环境特征'
  },
  // ✅ 新增概念
  concept: {
    label: '设定',
    icon: '✦',
    color: '#8b5cf6',
    description: '世界规则、宇宙真理、魔法体系、哲学概念、教义法则'
  },
}