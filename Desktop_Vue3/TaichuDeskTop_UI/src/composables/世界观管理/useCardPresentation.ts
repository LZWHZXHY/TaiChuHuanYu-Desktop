import { computed, toValue, type MaybeRefOrGetter } from 'vue'
import type { CardDetail } from '@/api/types/world'
import { useWorldStore } from '@/stores/world'
import { createCardPreview } from '@/utils/worldCards'
import { getEmbeddedCardIds, parseCoverImage, parseStringArray, renderCardDescription } from '@/utils/worldPresentation'
import { withThumbnail } from '@/utils/imageThumb'

export function useCardPresentation(card: MaybeRefOrGetter<CardDetail | null | undefined>,
  projectId: MaybeRefOrGetter<string>, variant: 'document' | 'detail') {
  const store = useWorldStore()
  const embeddedIds = computed(() => getEmbeddedCardIds(toValue(card), variant === 'detail'))
  const embeddedCards = computed(() => embeddedIds.value.flatMap(id => {
    const detail = store.getCardDetailById(id, toValue(projectId))
    if (detail) return [detail]
    const summary = store.getCardById(id, toValue(projectId))
    return summary ? [createCardPreview(summary)] : []
  }))
  return {
    embeddedIds, embeddedCards,
    cardTags: computed(() => parseStringArray(toValue(card)?.tags)),
    galleryImages: computed(() => parseStringArray(toValue(card)?.galleryImages).map(url => withThumbnail(url, 800))),
    coverUrl: computed(() => withThumbnail(parseCoverImage(toValue(card)?.coverImage), 800)),
    renderedDescription: computed(() => renderCardDescription(toValue(card)?.description ?? '',
      id => store.getCardById(id, toValue(projectId)), variant)),
  }
}
