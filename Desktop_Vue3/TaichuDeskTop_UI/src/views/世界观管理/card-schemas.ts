// src/views/世界观管理/card-schemas.ts
import type { CardType } from './card_type';

// ============================================================
//  字段类型
// ============================================================
export type FieldType =
  | 'text'         // 单行文本
  | 'textarea'     // 多行文本
  | 'number'       // 数字
  | 'date'         // 日期
  | 'select'       // 下拉选择
  | 'button-group' // 按钮组（少量选项）
  | 'tags'         // 标签数组
  | 'card-ref'     // 卡片引用
  | 'custom';      // 完全自定义（用 slot 渲染）

export interface FieldSchema {
  /** 对应 Content JSON 里的 key，必须严格一致 */
  key: string;
  /** 显示名称 */
  label: string;
  /** 控件类型 */
  type: FieldType;
  /** select / button-group 的选项 */
  options?: readonly string[];
  /** placeholder */
  placeholder?: string;
  /** 字段下方的提示 */
  hint?: string;
  /** 是否必填 */
  required?: boolean;
  /** tags 最大数量 */
  max?: number;
  /** number 最小值 */
  min?: number;
  /** custom 类型的标识，DynamicForm 会用它找对应的 slot */
  custom?: string;
}

// ============================================================
//  每种类型的字段定义
//  ⚠️ key 必须严格对齐 Content JSON 里的字段名
// ============================================================
export const cardSchemas: Record<CardType, FieldSchema[]> = {
  // ========== 角色：无专属字段 ==========
  character: [],

  // ========== 地点 ==========
  location: [
    {
      key: 'dangerLevel',
      label: '危险等级',
      type: 'button-group',
      options: ['低', '中', '高', '极度危险'] as const,
    },
    {
      key: 'coordinate',
      label: '地图坐标',
      type: 'custom',
      custom: 'location-coordinate',
      hint: '用于在地图上标记位置',
    },
  ],

  // ========== 物品 ==========
  item: [
    {
      key: 'rarity',
      label: '稀有度',
      type: 'button-group',
      options: ['普通', '稀有', '史诗', '传说', '神器'] as const,
    },
  ],

  // ========== 事件 ==========
  event: [
    { key: 'startDate', label: '开始时间', type: 'date' },
    { key: 'endDate', label: '结束时间', type: 'date' },
    {
      key: 'locationId',
      label: '发生地点',
      type: 'card-ref',
      hint: '从「地点」类型的卡片中选择',
    },
    {
      key: 'outcome',
      label: '结果',
      type: 'text',
      placeholder: '如：联盟胜利',
    },
    {
      key: 'significance',
      label: '历史意义',
      type: 'textarea',
      placeholder: '描述事件的历史意义...',
    },
  ],

  // ========== 派系 ==========
  faction: [
    {
      key: 'ideology',
      label: '核心理念',
      type: 'text',
      placeholder: '如：维护和平、征服世界',
    },
    { key: 'headquarters', label: '总部', type: 'text', placeholder: '派系总部所在地' },
    { key: 'foundedDate', label: '成立时间', type: 'date' },
    {
      key: 'size',
      label: '规模',
      type: 'button-group',
      options: ['小型', '中型', '大型', '巨型'] as const,
    },
  ],

  // ========== 物种：无专属字段 ==========
  species: [],

  // ========== 职业 ==========
  occupation: [
    {
      key: 'rank',
      label: '职业等级',
      type: 'button-group',
      options: ['初级', '中级', '高级', '大师', '传说'] as const,
    },
    {
      key: 'requirements',
      label: '前置要求',
      type: 'text',
      placeholder: '如：战士 Lv.10 以上',
    },
    {
      key: 'abilities',
      label: '核心能力',
      type: 'tags',
      max: 10,
    },
    {
      key: 'equipment',
      label: '装备限制',
      type: 'tags',
      max: 10,
    },
  ],

  // ========== 组织 ==========
  organization: [
    {
      key: 'purpose',
      label: '宗旨/目标',
      type: 'text',
      placeholder: '如：维护世界和平',
    },
    { key: 'headquarters', label: '总部', type: 'text', placeholder: '总部所在地' },
    { key: 'foundedDate', label: '成立时间', type: 'date' },
    {
      key: 'size',
      label: '规模',
      type: 'button-group',
      options: ['小型', '中型', '大型', '巨型'] as const,
    },
  ],

  // ========== 生物 ==========
  creature: [
    { key: 'habitat', label: '栖息地', type: 'text', placeholder: '如：森林、山地、洞穴' },
    {
      key: 'diet',
      label: '饮食',
      type: 'button-group',
      options: ['肉食', '草食', '杂食', '腐食', '魔法能量'] as const,
    },
    { key: 'temperament', label: '性情', type: 'text', placeholder: '如：温顺、凶暴、狡猾' },
    { key: 'abilities', label: '特殊能力', type: 'tags', max: 10 },
    {
      key: 'threatLevel',
      label: '威胁等级',
      type: 'button-group',
      options: ['低', '中', '高', '极度危险'] as const,
    },
  ],

  // ========== 技能 ==========
  skill: [
    {
      key: 'skillType',
      label: '技能类型',
      type: 'button-group',
      options: ['主动', '被动', '终极', '天赋'] as const,
    },
    { key: 'cost', label: '消耗', type: 'text', placeholder: '如：100 法力、10 怒气' },
    { key: 'cooldown', label: '冷却时间', type: 'text', placeholder: '如：10秒、1小时' },
    { key: 'effect', label: '效果描述', type: 'textarea', placeholder: '详细描述技能效果...' },
    { key: 'prerequisite', label: '前置条件', type: 'text', placeholder: '如：等级 20' },
    { key: 'level', label: '等级', type: 'number', min: 0, placeholder: '如：1' },
  ],

  // ========== 气候 ==========
  climate: [
    {
      key: 'climateName',
      label: '气候名称',
      type: 'text',
      required: true,
      placeholder: '如：天灾、永夜、晶雨',
    },
    { key: 'alternativeNames', label: '别名', type: 'tags', max: 10 },
    {
      key: 'manifestations',
      label: '表现形式',
      type: 'tags',
      max: 10,
      hint: '如：暴风、雪灾、陨石坠落',
    },
    {
      key: 'frequency',
      label: '频率',
      type: 'button-group',
      options: ['高频', '周期性', '罕见', '随机'] as const,
    },
    {
      key: 'predictability',
      label: '规律性',
      type: 'button-group',
      options: ['规律可循', '规律难循', '完全随机'] as const,
    },
    {
      key: 'cause',
      label: '成因',
      type: 'textarea',
      placeholder: '描述这个气候现象的成因...',
    },
    {
      key: 'effects',
      label: '影响',
      type: 'tags',
      max: 10,
      hint: '如：城市毁灭、地形重塑、生态崩溃',
    },
    {
      key: 'aftermath',
      label: '遗留影响',
      type: 'tags',
      max: 10,
      hint: '灾害结束后留下的长期影响',
    },
    {
      key: 'countermeasures',
      label: '应对方式',
      type: 'tags',
      max: 10,
      hint: '如：移动城市迁徙、地下避难所',
    },
    {
      key: 'safeZones',
      label: '安全区域',
      type: 'text',
      placeholder: '如：天灾安全带、庇护所',
    },
  ],

  // ========== 设定：无专属字段 ==========
  concept: [],
};