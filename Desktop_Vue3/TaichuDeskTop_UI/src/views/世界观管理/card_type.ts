// src/views/世界观管理/card_type.ts

// ============================================================
//  1. 卡片类型枚举
// ============================================================
export type CardType = 
  | 'character' 
  | 'location' 
  | 'item' 
  | 'event' 
  | 'faction' 
  | 'species' 
  | 'ecology'
  | 'lore'
  | 'occupation'
  | 'nation'
  | 'continent'
  | 'organization'
  | 'creature'
  | 'building'
  | 'weapon'
  | 'deity'
  | 'skill'
  | 'climate'   // ✅ 新增：气候/天灾

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
}

export interface BaseCardData {
  id: string
  projectId: string
  title: string
  type: CardType
  coverImage?: string
  attributes: { key: string; value: string }[]
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

// 3.1 角色卡片
export interface CharacterData extends BaseCardData {
  type: 'character'
  age?: number
  gender?: '男' | '女' | '其他' | '未知'
  powerLevel?: number
  stats?: {
    strength?: number
    agility?: number
    intelligence?: number
    charisma?: number
    endurance?: number
    luck?: number
  }
}

// 3.2 地点卡片
export interface LocationData extends BaseCardData {
  type: 'location'
  climate?: string
  area?: number
  population?: number
  dangerLevel?: '低' | '中' | '高' | '极度危险'
  coordinate?: { x: number; y: number }
}

// 3.3 物品卡片
export interface ItemData extends BaseCardData {
  type: 'item'
  material?: string
  rarity?: '普通' | '稀有' | '史诗' | '传说' | '神器'
  weight?: number
  value?: number
  origin?: string
  usage?: string
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
  habitat?: string
  diet?: string
  lifespan?: number
  abilities?: string[]
  origin?: string
}

// 3.7 生态卡片
export interface EcologyData extends BaseCardData {
  type: 'ecology'
  environment?: string
  species?: string[]
  foodChain?: string
  climatePattern?: string
}

// 3.8 背景设定卡片
export interface LoreData extends BaseCardData {
  type: 'lore'
  category?: string
  source?: string
  relatedEvents?: string[]
}

// ============================================================
//  4. 新增类型
// ============================================================

// 4.1 职业
export interface OccupationData extends BaseCardData {
  type: 'occupation'
  rank?: '初级' | '中级' | '高级' | '大师' | '传说'
  requirements?: string
  abilities?: string[]
  equipment?: string[]
  affiliation?: string
  advancement?: string[]
}

// 4.2 国家
export interface NationData extends BaseCardData {
  type: 'nation'
  government?: string
  capital?: string
  population?: number
  foundedDate?: string
  motto?: string
}

// 4.3 大陆
export interface ContinentData extends BaseCardData {
  type: 'continent'
  area?: number
  population?: number
  climate?: string
  notableFeatures?: string[]
}

// 4.4 组织
export interface OrganizationData extends BaseCardData {
  type: 'organization'
  purpose?: string
  headquarters?: string
  foundedDate?: string
  size?: '小型' | '中型' | '大型' | '巨型'
}

// 4.5 生物
export interface CreatureData extends BaseCardData {
  type: 'creature'
  habitat?: string
  diet?: string
  temperament?: string
  abilities?: string[]
  threatLevel?: '低' | '中' | '高' | '极度危险'
}

// 4.6 建筑
export interface BuildingData extends BaseCardData {
  type: 'building'
  location?: string
  builtDate?: string
  purpose?: string
  style?: string
  floors?: number
}

// 4.7 武器
export interface WeaponData extends BaseCardData {
  type: 'weapon'
  weaponType?: string
  material?: string
  rarity?: '普通' | '稀有' | '史诗' | '传说' | '神器'
  damage?: string
  weight?: number
  origin?: string
}

// 4.8 神祇
export interface DeityData extends BaseCardData {
  type: 'deity'
  domain?: string
  alignment?: string
  symbol?: string
  followers?: string[]
  holyBook?: string
  status?: '活跃' | '沉睡' | '陨落' | '被遗忘'
}

// 4.9 技能
export interface SkillData extends BaseCardData {
  type: 'skill'
  skillType?: '主动' | '被动' | '终极' | '天赋'
  cost?: string
  cooldown?: string
  effect?: string
  prerequisite?: string
  level?: number
}

// ✅ 4.10 气候/天灾
export interface ClimateData extends BaseCardData {
  type: 'climate'
  climateName: string           // 气候名称
  alternativeNames?: string[]   // 别名
  manifestations: string[]      // 表现形式（暴风、雪灾、陨石等）
  frequency: string             // 频率（高频、周期性、罕见、随机）
  predictability: string        // 规律性（规律可循、规律难循、完全随机）
  cause: string                 // 成因（用户自由填写）
  effects: string[]             // 直接影响
  aftermath: string[]           // 遗留影响
  countermeasures: string[]     // 应对方式
  safeZones?: string            // 安全区域
}

// ============================================================
//  5. 类型联合
// ============================================================
export type AnyCardData = 
  | CharacterData
  | LocationData
  | ItemData
  | EventData
  | FactionData
  | SpeciesData
  | EcologyData
  | LoreData
  | OccupationData
  | NationData
  | ContinentData
  | OrganizationData
  | CreatureData
  | BuildingData
  | WeaponData
  | DeityData
  | SkillData
  | ClimateData   // ✅ 新增

// ============================================================
//  6. 卡片类型元信息
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
    description: '人物、NPC、英雄、反派'
  },
  location: {
    label: '地点',
    icon: '📍',
    color: '#059669',
    description: '城市、王国、遗迹、自然景观'
  },
  item: {
    label: '物品',
    icon: '⚔️',
    color: '#d97706',
    description: '装备、道具、普通物品'
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
    description: '组织、势力、公会、家族'
  },
  species: {
    label: '物种',
    icon: '🐉',
    color: '#f59e0b',
    description: '种族、生物、怪物'
  },
  ecology: {
    label: '生态',
    icon: '🌿',
    color: '#10b981',
    description: '生态系统、环境、自然景观'
  },
  lore: {
    label: '背景设定',
    icon: '📜',
    color: '#6366f1',
    description: '传说、历史、文化、宗教'
  },
  occupation: {
    label: '职业',
    icon: '⚔️',
    color: '#f97316',
    description: '职业、身份、角色定位'
  },
  nation: {
    label: '国家',
    icon: '🏳️',
    color: '#ef4444',
    description: '王国、帝国、城邦'
  },
  continent: {
    label: '大陆',
    icon: '🌍',
    color: '#22c55e',
    description: '大陆、世界板块'
  },
  organization: {
    label: '组织',
    icon: '🤝',
    color: '#8b5cf6',
    description: '公会、公司、机构'
  },
  creature: {
    label: '生物',
    icon: '🐾',
    color: '#f59e0b',
    description: '具体生物个体'
  },
  building: {
    label: '建筑',
    icon: '🏰',
    color: '#ec4899',
    description: '城堡、神殿、塔'
  },
  weapon: {
    label: '武器',
    icon: '🗡️',
    color: '#d97706',
    description: '武器、装备'
  },
  deity: {
    label: '神祇',
    icon: '✨',
    color: '#fcd34d',
    description: '神明、神灵、信仰对象'
  },
  skill: {
    label: '技能',
    icon: '💥',
    color: '#06b6d4',
    description: '技能、魔法、能力'
  },
  // ✅ 新增气候
  climate: {
    label: '气候',
    icon: '🌀',
    color: '#0ea5e9',
    description: '气候现象、天灾、环境特征'
  },
}