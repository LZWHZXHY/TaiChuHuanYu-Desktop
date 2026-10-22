// src/composables/世界观管理/useCardForm.ts
import { ref } from 'vue'
import { type CardType } from '@/views/世界观管理/card_type'
import type { AttributeItem } from '@/views/世界观管理/card_type'

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