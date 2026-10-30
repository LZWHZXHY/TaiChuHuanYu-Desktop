// 全站共用的「别人戴了什么」缓存：头衔 + 头像框。
// 同一时刻要显示的用户 ID 先攒起来，下一拍合并成一次请求（/trade/appearances?ids=...），结果按用户缓存 5 分钟。
// 组件里直接用 appearanceOf(userId)，拿到的是响应式数据，请求回来后自动刷新。
import { reactive } from 'vue'
import request from '@/utils/request'

export interface Appearance { title: string | null; frame: string | null }

const TTL = 5 * 60 * 1000
const BATCH = 100
const store = reactive(new Map<string, Appearance>())
const loadedAt = new Map<string, number>()
const queue = new Set<string>()
const inflight = new Set<string>()
let timer: ReturnType<typeof setTimeout> | undefined

const norm = (id: unknown) => String(id ?? '').trim().toLowerCase()
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/

async function flush() {
  timer = undefined
  const ids = [...queue]
  queue.clear()
  for (let i = 0; i < ids.length; i += BATCH) {
    const part = ids.slice(i, i + BATCH)
    part.forEach(id => inflight.add(id))
    try {
      const res = await request.get<Record<string, Appearance>>('/trade/appearances', { params: { ids: part.join(',') }, silentError: true })
      const now = Date.now()
      for (const id of part) {
        const hit = res?.[id] ?? Object.entries(res ?? {}).find(([k]) => norm(k) === id)?.[1]
        store.set(id, { title: hit?.title || null, frame: hit?.frame || null })
        loadedAt.set(id, now)
      }
    } catch {
      // 读不到就当没戴，过一会儿再试
      const now = Date.now() - TTL + 30_000
      for (const id of part) { if (!store.has(id)) store.set(id, { title: null, frame: null }); loadedAt.set(id, now) }
    } finally {
      part.forEach(id => inflight.delete(id))
    }
  }
}

/** 某个用户当前戴的头衔和头像框（还没加载时返回 null，加载完自动更新） */
export function appearanceOf(userId: unknown): Appearance | null {
  const id = norm(userId)
  if (!GUID.test(id)) return null
  const fresh = (loadedAt.get(id) ?? 0) + TTL > Date.now()
  if (!fresh && !inflight.has(id) && !queue.has(id)) {
    queue.add(id)
    if (!timer) timer = setTimeout(() => void flush(), 30)
  }
  return store.get(id) ?? null
}

/** 自己换了装扮后马上更新缓存（不用等 5 分钟） */
export function setAppearance(userId: unknown, patch: Partial<Appearance>) {
  const id = norm(userId)
  if (!id) return
  const cur = store.get(id) ?? { title: null, frame: null }
  store.set(id, { ...cur, ...patch })
  loadedAt.set(id, Date.now())
}
