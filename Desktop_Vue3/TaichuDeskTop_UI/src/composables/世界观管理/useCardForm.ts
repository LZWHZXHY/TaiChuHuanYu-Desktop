// src/composables/世界观管理/useCardForm.ts
import { ref } from 'vue'
import type { CardDetail, TimelineEvent } from '@/api/types/world'
import { defaultLayout, resolveLayout, type CardLayout } from '@/utils/worldLayout'
import type { CardType, AttributeItem, ContentBlock } from '@/views/世界观管理/card_type'

function createEmptyCardForm() {
  return {
    title: '',
    type: 'character' as CardType,
    coverImage: '',
    galleryImages: [] as string[],
    attributes: [] as AttributeItem[],
    description: '',
    content: {} as Record<string, any>,   // ✅ 对象
    tags: [] as string[],
    relations: [] as { targetCardId: string; relationType: string }[],
    contentBlocks: [] as ContentBlock[],
    timelineEvents: [] as TimelineEvent[],
    aliases: [] as string[],
    /** 区块排列（背景、性格这类文本块的内容也在这里） */
    layout: defaultLayout({}) as CardLayout,
  }
}

export type CardForm = ReturnType<typeof createEmptyCardForm>

export function useCardForm() {
  const form = ref(createEmptyCardForm())
  const resetForm = () => { form.value = createEmptyCardForm() }

  // 批量设置表单数据（用于加载已有卡片）
  const setFormData = (data: Partial<typeof form.value>) => {
    Object.assign(form.value, data)
  }

  return {
    form,
    resetForm,
    setFormData,
  }
}

/** 所有比较都使用编辑器真正保存的字段，避免把缓存或反向引用算作本人的修改。 */
export function cardToForm(card: CardDetail): CardForm {
  let content = {}
  try { const value = JSON.parse(card.content || '{}'); if (value && typeof value === 'object' && !Array.isArray(value)) content = value } catch { /* 兼容旧数据 */ }
  return JSON.parse(JSON.stringify({
    title: card.title || '', type: card.type, coverImage: card.coverImage || '',
    galleryImages: card.galleryImages || [],
    attributes: (card.attributes || []).map(attr => ({ key: attr.key, value: attr.value,
      type: ['short', 'long', 'number', 'date', 'boolean'].includes(attr.type) ? attr.type : 'short' })),
    description: card.description || '', content, tags: Array.isArray(card.tags) ? card.tags : [],
    relations: (card.outRelations || []).map(r => ({ targetCardId: r.targetCardId, relationType: r.relationType })),
    contentBlocks: card.contentBlocks || [], timelineEvents: card.timelineEvents || [],
    aliases: Array.isArray(card.aliases) ? card.aliases : [],
    layout: resolveLayout(card.layout, card),
  }))
}

export const cardFieldLabels: Record<keyof CardForm, string> = {
  title: '名称', type: '条目类型', coverImage: '封面', galleryImages: '图库', attributes: '自定义属性',
  description: '正文', content: '类型属性', tags: '标签', relations: '设定关系', contentBlocks: '引用设定', timelineEvents: '时间线',
  aliases: '别名', layout: '区块（文本块、图片块和排列）',
}
const stable = (value: unknown): string => JSON.stringify(value, (_key, item) => item && typeof item === 'object' && !Array.isArray(item)
  ? Object.fromEntries(Object.keys(item).sort().map(key => [key, item[key]])) : item)
export function mergeCardForms(base: CardForm | null, local: CardForm, remote: CardForm) {
  const merged = JSON.parse(JSON.stringify(remote)) as CardForm
  const conflicts: (keyof CardForm)[] = []
  for (const key of Object.keys(cardFieldLabels) as (keyof CardForm)[]) {
    if (stable(local[key]) === stable(remote[key])) continue
    if (base && stable(local[key]) === stable(base[key])) continue
    if (!base || stable(remote[key]) !== stable(base[key])) conflicts.push(key)
    Object.assign(merged, { [key]: local[key] })
  }
  return { merged, conflicts }
}
