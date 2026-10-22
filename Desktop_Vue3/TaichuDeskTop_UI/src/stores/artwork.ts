import { defineStore } from 'pinia'
import { ref, watch } from 'vue'
import { useUserStore } from './user'
import type { ArtworkStats } from '../api/artwork'

export const useArtworkStore = defineStore('artwork', () => {
  const user = useUserStore()
  const stats = ref<Record<number, ArtworkStats>>({})
  const pending = ref<Record<number, boolean>>({})
  let version = 0
  let session = 0
  let changed: Record<number, number> = {}
  const capture = () => ({ version, session })
  watch(() => user.token, () => {
    version++
    session++
    stats.value = {}
    pending.value = {}
    changed = {}
  }, { flush: 'sync' })

  function seed(rows: Array<ArtworkStats & { id: number }>, start = capture()) {
    if (start.session !== session) return
    for (const row of rows) {
      if (!pending.value[row.id] && (changed[row.id] ?? 0) <= start.version) {
        const { likesCount, favoritesCount, commentsCount, isLiked, isFavorited } = row
        stats.value[row.id] = { likesCount, favoritesCount, commentsCount, isLiked, isFavorited }
      }
    }
  }

  async function mutate<T extends { stats: ArtworkStats & { id: number } }>(id: number, action: () => Promise<T>) {
    if (pending.value[id]) return null
    const started = session
    pending.value[id] = true
    changed[id] = ++version
    try {
      const result = await action()
      if (started !== session) return null
      stats.value[id] = result.stats
      return result
    } finally {
      if (started === session) {
        changed[id] = ++version
        pending.value[id] = false
      }
    }
  }

  return { stats, pending, capture, seed, mutate }
})
