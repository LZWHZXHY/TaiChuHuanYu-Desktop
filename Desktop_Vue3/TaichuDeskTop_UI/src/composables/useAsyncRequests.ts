import { computed, reactive } from 'vue'

/** 只复用进行中的请求。清除某个 key 后，旧响应不会提交状态。 */
export function useAsyncRequests() {
  const pending = new Map<string, Promise<unknown>>()
  const operations = reactive(new Map<symbol, string>())
  const loading = computed(() => operations.size > 0)
  const isLoading = (group: string) => [...operations.values()].includes(group)

  async function track<T>(group: string, action: () => Promise<T>): Promise<T> {
    const id = Symbol(group)
    operations.set(id, group)
    try { return await action() } finally { operations.delete(id) }
  }

  function run<T>(key: string, group: string, action: () => Promise<T>, commit: (value: T) => void): Promise<T> {
    const existing = pending.get(key)
    if (existing) return existing as Promise<T>
    const task = track(group, action).then(value => {
      if (pending.get(key) === task) commit(value)
      return value
    }).finally(() => {
      if (pending.get(key) === task) pending.delete(key)
    })
    pending.set(key, task)
    return task
  }

  function invalidate(prefix: string) {
    for (const key of pending.keys()) if (key.startsWith(prefix)) pending.delete(key)
    for (const [id, group] of operations) if (group.startsWith(prefix)) operations.delete(id)
  }

  function reset() {
    pending.clear()
    operations.clear()
  }

  return { loading, isLoading, track, run, invalidate, reset }
}
