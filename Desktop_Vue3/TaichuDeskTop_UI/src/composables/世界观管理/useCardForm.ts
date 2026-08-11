// src/composables/世界观管理/useCardForm.ts
import { ref } from 'vue'
import { type CardType } from '@/views/世界观管理/card_type'
import type { AttributeItem } from '@/views/世界观管理/card_type'

export function useCardForm() {
  // 表单数据
  const form = ref({
    title: '',
    type: 'character' as CardType,
    coverImage: '',
    galleryImages: [] as string[],
    attributes: [] as AttributeItem[],
    description: '',
    content: '{}',
    tags: [] as string[],
    relations: [] as { targetCardId: string; relationType: string }[],
    contentBlocks: [] as {
      id: string;
      cardId: string;
      cardType: string;
      order: number;
      cardTitle?: string;
      cardCover?: string;
      cardSummary?: string;
      cardAttributes?: { key: string; value: string }[];
      contextLabel?: string;
    }[],
  })

  // 重置表单
  const resetForm = () => {
    form.value = {
      title: '',
      type: 'character',
      coverImage: '',
      galleryImages: [],
      attributes: [],
      description: '',
      content: '{}',
      tags: [],
      relations: [],
      contentBlocks: [],
    }
  }

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