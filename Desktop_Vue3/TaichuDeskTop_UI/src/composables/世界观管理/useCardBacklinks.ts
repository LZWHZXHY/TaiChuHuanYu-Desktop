import { ref, watch, onScopeDispose } from 'vue'
import { worldApi } from '@/api/worldApi'
import type { CardSummary } from '@/api/types/world'

// 反向引用：加载「引用了当前卡片的其它卡片」。
export function useCardBacklinks(projectId: () => string, cardId: () => string) {
  const backlinks = ref<CardSummary[]>([])
  const loading = ref(false)
  let version = 0

  async function load() {
    const pid = projectId()
    const cid = cardId()
    if (!pid || !cid) { backlinks.value = []; return }
    const current = ++version
    loading.value = true
    try {
      const result = await worldApi.getCardBacklinks(pid, cid)
      if (current === version) backlinks.value = result
    } catch {
      if (current === version) backlinks.value = []
    } finally {
      if (current === version) loading.value = false
    }
  }

  watch([projectId, cardId], load, { immediate: true })
  onScopeDispose(() => { version++ })

  return { backlinks, loading }
}
