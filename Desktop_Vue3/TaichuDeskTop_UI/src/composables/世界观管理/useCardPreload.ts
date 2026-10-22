import { onScopeDispose, ref, toValue, watch, type MaybeRefOrGetter } from 'vue'
import { useWorldStore } from '@/stores/world'

/** 后台详情加载统一复用 Store 缓存，切换项目或卸载后停止后续批次。 */
export function useCardPreload(projectId: MaybeRefOrGetter<string | undefined>,
  cardIds?: MaybeRefOrGetter<string[]>) {
  const store = useWorldStore()
  const loading = ref(false)
  let version = 0
  let disposed = false

  async function load(ids: string[]) {
    const current = ++version
    const pid = toValue(projectId)
    loading.value = false
    if (disposed || !pid) return
    const missing = [...new Set(ids)].filter(id => id && !store.getCardDetailById(id, pid))
    loading.value = missing.length > 0
    try {
      for (let offset = 0; offset < missing.length; offset += 8) {
        if (disposed || current !== version || pid !== toValue(projectId)) return
        await Promise.all(missing.slice(offset, offset + 8).map(id => store.loadCardDetail(pid, id).catch(error => {
          console.warn(`加载卡片详情失败: ${id}`, error)
        })))
      }
    } finally {
      if (current === version) loading.value = false
    }
  }

  if (cardIds) watch([() => toValue(projectId), () => JSON.stringify(toValue(cardIds))],
    () => { void load(toValue(cardIds)) }, { immediate: true })
  onScopeDispose(() => { disposed = true; version++; loading.value = false })
  return { loading, load, getDetail: (id: string) => store.getCardDetailById(id, toValue(projectId)) ?? null }
}
