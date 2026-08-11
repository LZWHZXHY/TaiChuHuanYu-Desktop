// src/composables/世界观管理/useCardTypeOptions.ts
import { computed } from 'vue'
import { useWorldStore } from '@/stores/world'
import { CardTypeMeta } from '@/views/世界观管理/card_type'

export function useCardTypeOptions() {
  const store = useWorldStore()

  const cardTypeOptions = computed(() => {
    if (store.cardTypes && store.cardTypes.length) {
      return store.cardTypes.map((t: any) => ({
        value: t.id || t.value,
        label: t.label,
      }))
    }
    return Object.entries(CardTypeMeta).map(([value, meta]) => ({
      value,
      label: meta.label,
    }))
  })

  return { cardTypeOptions }
}