// src/composables/世界观管理/useWorldMaps.ts
// 多层地图：找出这个世界里哪些条目有地图区块（编辑图钉「进入下一层地图」时选目标用）。
// 条目列表里没有区块数据，只能把条目详情分批读回来看（每批最多 100 个，后端的限制）。
// 读一次记住一会儿，同一个世界里反复打开选择框不会每次都重新读。
import { ref } from 'vue'
import { worldApi } from '@/api/worldApi'
import { useWorldStore } from '@/stores/world'
import { blockTitle, mapBlocksOf, type MapTarget } from '@/utils/worldLayout'

/** 一张可以「进入」的地图：哪个条目的哪个地图区块 */
export interface WorldMapOption extends MapTarget {
  cardTitle: string
  cardType: string
  /** 区块标题（没起标题就是「地图」） */
  blockTitle: string
  image: string
  pinCount: number
}

const CACHE_MS = 60_000
const BATCH = 100
const cache = new Map<string, { at: number; promise: Promise<WorldMapOption[]> }>()

/** 读这个世界里所有的地图区块；force = 不用缓存（比如刚保存了新地图） */
export function loadWorldMaps(projectId: string, cardIds: string[], force = false): Promise<WorldMapOption[]> {
  const hit = cache.get(projectId)
  if (hit && !force && Date.now() - hit.at < CACHE_MS) return hit.promise
  const promise = (async () => {
    const out: WorldMapOption[] = []
    for (let i = 0; i < cardIds.length; i += BATCH) {
      const cards = await worldApi.getCardsBatch(projectId, cardIds.slice(i, i + BATCH))
      for (const card of cards) {
        for (const block of mapBlocksOf(card.layout)) {
          out.push({ cardId: card.id, blockId: block.id, cardTitle: card.title, cardType: card.type, blockTitle: blockTitle(block), image: block.image ?? '', pinCount: block.pins?.length ?? 0 })
        }
      }
    }
    return out.sort((a, b) => a.cardTitle.localeCompare(b.cardTitle, 'zh-CN'))
  })()
  cache.set(projectId, { at: Date.now(), promise })
  // 读失败了不要记住，下次再试
  promise.catch(() => { if (cache.get(projectId)?.promise === promise) cache.delete(projectId) })
  return promise
}

/** 在组件里用：maps 是读到的地图，loading / failed 是读取状态 */
export function useWorldMaps(projectId: () => string) {
  const store = useWorldStore()
  const maps = ref<WorldMapOption[]>([])
  const loading = ref(false)
  const failed = ref(false)
  let version = 0
  async function load(force = false) {
    const v = ++version
    const pid = projectId()
    loading.value = true
    failed.value = false
    try {
      const ids = store.cards.filter(c => c.projectId === pid).map(c => c.id)
      const result = await loadWorldMaps(pid, ids, force)
      if (v === version) maps.value = result
    } catch {
      if (v === version) failed.value = true
    } finally {
      if (v === version) loading.value = false
    }
  }
  return { maps, loading, failed, load }
}
