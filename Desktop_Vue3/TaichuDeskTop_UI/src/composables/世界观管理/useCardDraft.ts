import { computed, onScopeDispose, ref, watch, type Ref } from 'vue'
import { useUserStore } from '@/stores/user'
import type { CardForm } from './useCardForm'
import { defaultLayout, normalizeLayout } from '@/utils/worldLayout'

type Draft = { version: 1; key: string; updatedAt: number; baseline: string; form: CardForm }
type Candidate = Draft & { storageKey: string; raw: string }

// 每个编辑器独占一个存储槽；恢复其他页面的副本也不会接管其写入位置。
export function useCardDraft(form: Ref<CardForm>) {
  const user = useUserStore()
  const writer = crypto.randomUUID()
  const pending = ref<Candidate | null>(null)
  const candidates = ref<Candidate[]>([])
  const savedAt = ref(0), error = ref(''), baseSnapshot = ref('')
  let binding: { key: string; baseKey: string; token: string; userId: string; baseline: string } | undefined
  let restoredFrom: Candidate | undefined
  let paused = false
  let releaseLease: (() => void) | undefined
  const locks = typeof navigator !== 'undefined' ? navigator.locks : undefined
  const active = () => binding && binding.token === user.token && binding.userId === user.userId
  const serverChanged = computed(() => Boolean(pending.value && pending.value.baseline !== binding?.baseline))

  function suspend() {
    releaseLease?.(); releaseLease = undefined
    binding = undefined; restoredFrom = undefined
    pending.value = null; candidates.value = []; savedAt.value = 0; error.value = ''; baseSnapshot.value = ''
  }
  watch(() => [user.token, user.userId], suspend, { flush: 'sync' })
  onScopeDispose(suspend)

  function readCandidate(storageKey: string, baseKey: string): Candidate | null {
    const raw = localStorage.getItem(storageKey)
    if (!raw) return null
    const draft = JSON.parse(raw) as Draft
      // 严格检查表单字段和集合，损坏或不同版本的数据不能进入编辑器。
      const candidate = draft.form
      // 旧版草稿（没有别名和区块排列）：补上默认值
      if (candidate && !Array.isArray(candidate.aliases)) candidate.aliases = []
      if (candidate) candidate.layout = normalizeLayout(candidate.layout) ?? defaultLayout(candidate)
      const strings = ['title', 'type', 'coverImage', 'description'] as const
      const lists = ['galleryImages', 'attributes', 'tags', 'relations', 'contentBlocks', 'timelineEvents'] as const
      if (draft.version !== 1 || draft.key !== baseKey || !Number.isFinite(draft.updatedAt) || typeof draft.baseline !== 'string'
        || !candidate || strings.some(field => typeof candidate[field] !== 'string')
        || lists.some(field => !Array.isArray(candidate[field])) || !candidate.content || typeof candidate.content !== 'object' || Array.isArray(candidate.content)
        || [...candidate.galleryImages, ...candidate.tags].some(value => typeof value !== 'string')
        || candidate.attributes.some(value => !value || typeof value.key !== 'string' || typeof value.type !== 'string')
        || candidate.relations.some(value => !value || typeof value.targetCardId !== 'string' || typeof value.relationType !== 'string')
        || candidate.contentBlocks.some(value => !value || typeof value.cardId !== 'string')
        || candidate.timelineEvents.some(value => !value || typeof value.date !== 'string' || typeof value.title !== 'string')) throw new Error('invalid draft')
    return { ...draft, storageKey, raw }
  }
  function collect() {
    if (!active()) return
    const { key, baseKey } = binding!
    const keys = new Set([baseKey, key]) // 兼容上一版单槽草稿
    for (let i = 0; i < localStorage.length; i++) {
      const item = localStorage.key(i)
      if (item?.startsWith(baseKey + '~')) keys.add(item)
    }
    const found: Candidate[] = []
    for (const item of keys) {
      try {
        const draft = readCandidate(item, baseKey)
        if (draft) {
          if (JSON.stringify(draft.form) !== binding!.baseline) found.push(draft)
          else void removeCandidate(draft).catch(() => {}) // 已提交的旧副本只在原页面不再编辑时清理
        }
      } catch { error.value = '部分本地草稿无法读取，未恢复损坏的内容。' }
    }
    candidates.value = found.sort((a, b) => b.updatedAt - a.updatedAt)
    pending.value = candidates.value[0] || null
  }
  function bind(projectId: string, cardId: string) {
    suspend()
    if (!user.token || !user.userId || !projectId) return
    const baseKey = `world-card-draft:v1:${[user.userId, projectId, cardId || 'new'].map(encodeURIComponent).join(':')}`
    const key = `${baseKey}~${writer}`
    binding = { key, baseKey, token: user.token, userId: user.userId, baseline: JSON.stringify(form.value) }
    baseSnapshot.value = binding.baseline
    // 活跃页面持有自己的槽锁，其他页面不能清理仍在编辑的副本；关闭页面自动释放。
    if (locks) {
      const lifetime = new Promise<void>(resolve => { releaseLease = resolve })
      void locks.request(key, () => lifetime).catch(() => { error.value = '无法保护多标签页草稿，请保留当前页面并保存条目。' })
    }
    try { collect() } catch { error.value = '本地草稿无法读取，请检查浏览器存储设置。' }
  }
  function flush(): boolean {
    if (!active()) return false
    if (pending.value) return true
    try {
      const snapshot = JSON.stringify(form.value)
      if (snapshot === binding!.baseline) {
        localStorage.removeItem(binding!.key); savedAt.value = 0; error.value = ''; return true
      }
      const draft: Draft = { version: 1, key: binding!.baseKey, updatedAt: Date.now(), baseline: baseSnapshot.value, form: form.value }
      localStorage.setItem(binding!.key, JSON.stringify(draft))
      savedAt.value = draft.updatedAt; error.value = ''; return true
    } catch {
      error.value = '本地草稿备份失败，请先保存条目，暂勿刷新或关闭页面。'
      return false
    }
  }
  async function removeCandidate(candidate: Candidate) {
    const context = binding
    const remove = () => {
      if (!active() || binding !== context || localStorage.getItem(candidate.storageKey) !== candidate.raw) return false
      localStorage.removeItem(candidate.storageKey); return true
    }
    if (candidate.storageKey === context?.key) return remove()
    // 没有 Web Locks 的浏览器不自动删除其他页面的副本。
    return locks ? locks.request(candidate.storageKey, { ifAvailable: true }, lock => lock ? remove() : false) : false
  }
  async function clear() {
    if (!active()) return
    const context = binding
    try {
      if (pending.value) {
        if (!await removeCandidate(pending.value)) { error.value = '这个草稿仍在其他标签页编辑或已经更新，请先关闭那个编辑页面，再重新打开此页。'; return }
        if (context === binding) collect()
      } else {
        localStorage.removeItem(binding!.key)
        if (restoredFrom) await removeCandidate(restoredFrom)
        if (context === binding) { restoredFrom = undefined; savedAt.value = 0 }
      }
    } catch { error.value = '无法清理本地草稿，请检查浏览器存储设置。' }
  }
  function restore() {
    if (!active() || !pending.value) return
    try {
      if (localStorage.getItem(pending.value.storageKey) !== pending.value.raw) {
        collect(); error.value = '其他标签页刚更新了草稿，请重新选择要恢复的版本。'; return
      }
      restoredFrom = pending.value
      baseSnapshot.value = pending.value.baseline // 保留旧基线，不能把旧内容伪装成基于最新版的修改。
      paused = true
      form.value = JSON.parse(JSON.stringify(pending.value.form))
      pending.value = null; paused = false; flush()
    } catch { paused = false; error.value = '草稿暂时无法恢复，填写的内容未被替换。' }
  }
  function rebase(baseline: string) { baseSnapshot.value = baseline; if (binding) binding.baseline = baseline; flush() }
  async function acceptSaved() {
    const context = binding
    await clear()
    if (binding && binding === context) { binding.baseline = JSON.stringify(form.value); baseSnapshot.value = binding.baseline }
  }
  watch(form, () => { if (!paused) flush() }, { deep: true, flush: 'sync' })
  return { pending, candidates, savedAt, error, baseSnapshot, serverChanged, bind, suspend, restore, clear, flush, rebase, acceptSaved }
}
