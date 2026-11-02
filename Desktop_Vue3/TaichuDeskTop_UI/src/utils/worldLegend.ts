/**
 * 时间线颜色图例：每个世界自己规定每种颜色代表什么（比如 红 = 战争、灰 = 灾难）。
 * 条目里的时间线区块（调色板 MARK_COLORS）和世界编年史（调色板 LINE_COLORS）的色值略有不同，
 * 所以图例按「色系」记：同一个色系的几个色值算同一种颜色，一份图例两边都能用。
 * 后端只认这里的色系名字（WorldLegendController.Hues），两边要一起改。
 */
import { computed, reactive, watch, type ComputedRef } from 'vue'
import { worldLegendApi } from '@/api/worldLegend'

export interface LegendHue {
  key: string
  /** 给人看的颜色名 */
  name: string
  /** 图例上画的色块 */
  swatch: string
  /** 属于这个色系的色值（两套调色板里的都算） */
  colors: string[]
  /** 设置里输入框的示例 */
  example: string
}

export const LEGEND_HUES: LegendHue[] = [
  { key: 'red', name: '红', swatch: '#d9534f', colors: ['#d9534f', '#c0504d'], example: '战争' },
  { key: 'amber', name: '黄', swatch: '#e0a526', colors: ['#e0a526', '#d99a2b'], example: '建国、盛世' },
  { key: 'green', name: '绿', swatch: '#5b8c3a', colors: ['#5b8c3a'], example: '和平、发现' },
  { key: 'teal', name: '青', swatch: '#2a7f8f', colors: ['#2a7f8f'], example: '科技' },
  { key: 'blue', name: '蓝', swatch: '#3d6fb0', colors: ['#3d6fb0'], example: '航海、迁徙' },
  { key: 'purple', name: '紫', swatch: '#7a5cc4', colors: ['#7a5cc4'], example: '神迹、魔法' },
  { key: 'pink', name: '粉', swatch: '#c46a9a', colors: ['#c46a9a'], example: '婚姻、结盟' },
  { key: 'brown', name: '棕', swatch: '#8a6a3a', colors: ['#8a6a3a'], example: '文化、传说' },
  { key: 'gray', name: '灰', swatch: '#4f5b66', colors: ['#3d4b5c', '#4f5b66'], example: '灾难' },
]

const hueByColor = new Map(LEGEND_HUES.flatMap(h => h.colors.map(c => [c.toLowerCase(), h.key] as const)))
/** 一个色值属于哪个色系；没选颜色或者不是调色板里的颜色返回 null */
export const hueOf = (color?: string | null) => (color ? hueByColor.get(color.trim().toLowerCase()) ?? null : null)

export interface LegendEntry { key: string; name: string; swatch: string; label: string }
/** 把「色系 → 含义」排成图例（只留定义过含义的颜色）；used 给了就只留这些色系 */
export function legendEntries(items: Record<string, string>, used?: Set<string> | null): LegendEntry[] {
  return LEGEND_HUES
    .filter(h => items[h.key] && (!used || used.has(h.key)))
    .map(h => ({ key: h.key, name: h.name, swatch: h.swatch, label: items[h.key]! }))
}

// ---------- 缓存：同一个世界的图例在整个页面里共用一份（多个时间线区块、分屏窗格不用重复请求） ----------
const cache = reactive(new Map<string, Record<string, string>>())
const loadedAt = new Map<string, number>()
const inflight = new Map<string, Promise<void>>()
const STALE_MS = 5 * 60 * 1000

function ensureLegend(projectId: string) {
  const at = loadedAt.get(projectId)
  if (inflight.has(projectId) || (at && Date.now() - at < STALE_MS)) return
  const task = worldLegendApi.get(projectId)
    .then(data => { cache.set(projectId, data?.items && typeof data.items === 'object' ? data.items : {}) })
    // 读不到（网络、权限）就当没有图例；记下时间，免得每个区块都再请求一遍
    .catch(() => { if (!cache.has(projectId)) cache.set(projectId, {}) })
    .finally(() => { loadedAt.set(projectId, Date.now()); inflight.delete(projectId) })
  inflight.set(projectId, task)
}

/** 世界设置里保存后调用：页面上所有图例立刻跟着变 */
export function setWorldLegend(projectId: string, items: Record<string, string>) {
  cache.set(projectId, { ...items })
  loadedAt.set(projectId, Date.now())
}

/** 组件里用：返回这个世界的「色系 → 含义」（还没读到时是空的） */
export function useWorldLegend(projectId: () => string | null | undefined): ComputedRef<Record<string, string>> {
  watch(projectId, id => { if (id) ensureLegend(id) }, { immediate: true })
  return computed(() => {
    const id = projectId()
    return (id && cache.get(id)) || {}
  })
}
