// src/views/世界观管理/card_templates.ts
import type { CardType, AttributeItem } from './card_type'

// 每种卡片类型的预设属性模板（键 + 属性类型），新建卡片时自动填入空值，提升录入效率。
export const CARD_TEMPLATES: Record<CardType, { key: string; type: AttributeItem['type'] }[]> = {
  character: [
    { key: '性别', type: 'short' },
    { key: '年龄', type: 'short' },
    { key: '种族', type: 'short' },
    { key: '阵营', type: 'short' },
    { key: '身份', type: 'short' },
    { key: '能力', type: 'long' },
  ],
  location: [
    { key: '位置', type: 'short' },
    { key: '气候', type: 'short' },
    { key: '地形', type: 'short' },
    { key: '所属势力', type: 'short' },
    { key: '人口', type: 'number' },
  ],
  item: [
    { key: '类别', type: 'short' },
    { key: '稀有度', type: 'short' },
    { key: '出处', type: 'short' },
    { key: '持有者', type: 'short' },
    { key: '用途', type: 'long' },
  ],
  event: [
    { key: '发生时间', type: 'date' },
    { key: '地点', type: 'short' },
    { key: '参与者', type: 'short' },
    { key: '起因', type: 'long' },
    { key: '结果', type: 'long' },
    { key: '影响', type: 'long' },
  ],
  faction: [
    { key: '宗旨', type: 'long' },
    { key: '领袖', type: 'short' },
    { key: '规模', type: 'short' },
    { key: '势力范围', type: 'short' },
    { key: '盟友', type: 'short' },
    { key: '敌人', type: 'short' },
  ],
  species: [
    { key: '寿命', type: 'short' },
    { key: '形态', type: 'short' },
    { key: '栖息地', type: 'short' },
    { key: '能力', type: 'long' },
    { key: '天敌', type: 'short' },
  ],
  occupation: [
    { key: '等级', type: 'short' },
    { key: '要求', type: 'long' },
    { key: '能力', type: 'long' },
    { key: '装备', type: 'long' },
  ],
  organization: [
    { key: '宗旨', type: 'long' },
    { key: '总部', type: 'short' },
    { key: '规模', type: 'short' },
    { key: '创始人', type: 'short' },
  ],
  creature: [
    { key: '栖息地', type: 'short' },
    { key: '食性', type: 'short' },
    { key: '性情', type: 'short' },
    { key: '能力', type: 'long' },
    { key: '威胁等级', type: 'short' },
  ],
  skill: [
    { key: '技能类型', type: 'short' },
    { key: '消耗', type: 'short' },
    { key: '冷却', type: 'short' },
    { key: '效果', type: 'long' },
  ],
  climate: [
    { key: '频率', type: 'short' },
    { key: '可预测性', type: 'short' },
    { key: '成因', type: 'long' },
    { key: '影响', type: 'long' },
  ],
  concept: [
    { key: '类别', type: 'short' },
    { key: '规则', type: 'long' },
    { key: '例外', type: 'long' },
  ],
}

export function templateAttributes(type: CardType): AttributeItem[] {
  return (CARD_TEMPLATES[type] || []).map(t => ({ key: t.key, value: '', type: t.type }))
}
