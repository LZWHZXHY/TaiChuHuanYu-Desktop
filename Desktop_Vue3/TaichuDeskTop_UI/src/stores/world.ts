import { defineStore } from 'pinia'
import { computed, reactive, ref, watch } from 'vue'
import { worldApi } from '@/api/worldApi'
import type {
  WorldProject, CardSummary, CardDetail, Relation, CardTypeOption,
  CreateProjectPayload, UpdateProjectPayload, CreateCardPayload, UpdateCardPayload,
} from '@/api/types/world'
import { useUserStore } from './user'
import { useAsyncRequests } from '@/composables/useAsyncRequests'
import { toCardSummary } from '@/utils/worldCards'

export type { WorldProject } from '@/api/types/world'
export type WorldCardSummary = CardSummary
export type WorldCardDetail = CardDetail
export type WorldRelation = Relation

interface ProjectSnapshot {
  project: WorldProject
  cards: CardSummary[]
  relations: Relation[]
}

const fallbackTypes: CardTypeOption[] = [
  { label: '角色', value: 'character', icon: '🧙' },
  { label: '地点', value: 'location', icon: '📍' },
  { label: '物品', value: 'item', icon: '⚔️' },
  { label: '事件', value: 'event', icon: '📖' },
  { label: '生态', value: 'ecology', icon: '🌿' },
  { label: '派系', value: 'faction', icon: '🏛️' },
  { label: '物种', value: 'species', icon: '🐉' },
  { label: '背景设定', value: 'lore', icon: '📜' },
]

export const useWorldStore = defineStore('world', () => {
  const userStore = useUserStore()
  const requests = useAsyncRequests()
  const projects = ref<WorldProject[]>([])
  const publicProjects = ref<WorldProject[]>([])
  const cardTypes = ref<CardTypeOption[]>([])
  const activeProjectId = ref('')
  const selectedDetailKey = ref('')
  const snapshots = reactive(new Map<string, ProjectSnapshot>())
  const dirtyProjects = new Set<string>()
  const references = reactive(new Map<string, Record<string, string[]>>())
  const details = reactive(new Map<string, CardDetail>())
  const graphSaves = reactive(new Map<string, { positions: string; saved: string; status: 'saving' | 'saved' | 'error' | 'conflict' }>())
  const graphJobs = new Map<string, Promise<void>>()
  let sessionVersion = 0

  const projectKey = (id: string) => `project:${id}:`
  const detailKey = (projectId: string, cardId: string) => `${projectKey(projectId)}card:${cardId}`
  const currentProject = computed(() => snapshots.get(activeProjectId.value)?.project ?? null)
  const cards = computed(() => snapshots.get(activeProjectId.value)?.cards ?? [])
  const allRelations = computed(() => snapshots.get(activeProjectId.value)?.relations ?? [])
  const currentCard = computed(() => details.get(selectedDetailKey.value) ?? null)
  const loadingProjects = computed(() => requests.isLoading('projects'))
  const loadingPublicProjects = computed(() => requests.isLoading('publicProjects'))
  const loadingCards = computed(() => requests.isLoading(projectKey(activeProjectId.value)))
  const loadingDetails = computed(() => requests.isLoading(selectedDetailKey.value))
  const saving = computed(() => requests.isLoading('saving'))

  function reset() {
    sessionVersion++
    requests.reset()
    projects.value = []
    publicProjects.value = []
    cardTypes.value = []
    snapshots.clear()
    graphSaves.clear()
    graphJobs.clear()
    references.clear()
    dirtyProjects.clear()
    details.clear()
    activeProjectId.value = ''
    selectedDetailKey.value = ''
  }
  watch(() => userStore.token, reset, { flush: 'sync' })

  const fetchProjects = () => requests.run('projects', 'projects', worldApi.getProjects, value => {
    projects.value = value
  })
  const fetchPublicProjects = () => requests.run('publicProjects', 'publicProjects', worldApi.getPublicProjects, value => {
    publicProjects.value = value
  })

  async function fetchCardTypes() {
    if (cardTypes.value.length) return cardTypes.value
    return requests.run('cardTypes', 'cardTypes', async () => {
      try {
        return (await worldApi.getCardTypes()).map(type => ({
          value: type.id, label: type.label, icon: type.icon ?? undefined,
        }))
      } catch {
        return fallbackTypes.map(type => ({ ...type }))
      }
    }, value => { cardTypes.value = value })
  }

  function syncProjectLists(project: WorldProject) {
    const index = projects.value.findIndex(item => item.id === project.id)
    if (index !== -1) projects.value[index] = project
    const publicIndex = publicProjects.value.findIndex(item => item.id === project.id)
    if (!project.isPublic) publicProjects.value = publicProjects.value.filter(item => item.id !== project.id)
    else if (publicIndex !== -1) publicProjects.value[publicIndex] = project
    else publicProjects.value.push(project)
  }

  function loadProject(projectId: string, force = false): Promise<ProjectSnapshot> {
    const cached = snapshots.get(projectId)
    if (cached && !force && !dirtyProjects.has(projectId)) return Promise.resolve(cached)
    return requests.run(`${projectKey(projectId)}snapshot`, projectKey(projectId), async () => {
      const [project, cards, relations] = await Promise.all([
        worldApi.getProject(projectId), worldApi.getCards(projectId), worldApi.getProjectRelations(projectId),
      ])
      return { project, cards, relations }
    }, snapshot => {
      const graph = graphSaves.get(projectId)
      if (graph && graph.status !== 'saved') snapshot.project.graphPositions = graph.positions
      else graphSaves.delete(projectId)
      snapshots.set(projectId, snapshot)
      dirtyProjects.delete(projectId)
      syncProjectLists(snapshot.project)
    })
  }

  async function fetchCards(projectId: string, force = false) {
    const version = sessionVersion
    if (activeProjectId.value !== projectId) selectedDetailKey.value = ''
    activeProjectId.value = projectId
    if (force) invalidateProject(projectId)
    try { return (await loadProject(projectId)).cards }
    catch (error) {
      if (version === sessionVersion) {
        snapshots.delete(projectId)
        invalidateProject(projectId)
      }
      throw error
    }
  }

  function loadReferences(projectId: string) {
    const cached = references.get(projectId)
    if (cached) return Promise.resolve(cached)
    return requests.run(`${projectKey(projectId)}references`, projectKey(projectId),
      () => worldApi.getReferences(projectId), result => references.set(projectId, result))
  }

  function loadCardDetail(projectId: string, cardId: string, force = false): Promise<CardDetail> {
    const key = detailKey(projectId, cardId)
    const cached = details.get(key)
    if (cached && !force) return Promise.resolve(cached)
    return requests.run(key, key, () => worldApi.getCard(projectId, cardId), card => {
      details.set(key, card)
      const snapshot = snapshots.get(projectId)
      const index = snapshot?.cards.findIndex(item => item.id === cardId) ?? -1
      if (snapshot && index !== -1) snapshot.cards[index] = toCardSummary(card)
    })
  }

  // 主详情选择与后台预加载分开，嵌入卡片和图谱不会改变 currentCard。
  function fetchCardDetail(projectId: string, cardId: string, force = false) {
    selectedDetailKey.value = detailKey(projectId, cardId)
    return loadCardDetail(projectId, cardId, force)
  }

  function getCardDetailById(cardId: string, projectId = activeProjectId.value) {
    return details.get(detailKey(projectId, cardId))
  }

  function getCardById(cardId: string, projectId = activeProjectId.value): CardSummary | undefined {
    const detail = getCardDetailById(cardId, projectId)
    return detail ? toCardSummary(detail) : snapshots.get(projectId)?.cards.find(card => card.id === cardId)
  }

  async function fetchCardsByIds(projectId: string, cardIds: string[]): Promise<CardSummary[]> {
    const uniqueIds = [...new Set(cardIds)]
    const result = await Promise.all(uniqueIds.map(async id =>
      getCardById(id, projectId) ?? toCardSummary(await loadCardDetail(projectId, id))))
    const byId = new Map(result.map(card => [card.id, card]))
    return cardIds.map(id => byId.get(id)!)
  }

  function invalidateProject(projectId: string) {
    requests.invalidate(projectKey(projectId))
    dirtyProjects.add(projectId)
    references.delete(projectId)
    for (const key of details.keys()) if (key.startsWith(projectKey(projectId))) details.delete(key)
  }

  // 多步写操作即使部分失败，也重新读取后端真实状态；刷新失败不伪装成写入失败。
  async function mutateProject<T>(projectId: string, action: () => Promise<T>): Promise<T> {
    const version = sessionVersion
    requests.invalidate('projects')
    requests.invalidate('publicProjects')
    invalidateProject(projectId)
    return requests.track('saving', async () => {
      try {
        return await action()
      } finally {
        if (version === sessionVersion) {
          requests.invalidate('projects')
          requests.invalidate('publicProjects')
          invalidateProject(projectId)
          if (activeProjectId.value === projectId) {
            try {
              const snapshot = await loadProject(projectId)
              const selectedId = snapshot.cards.find(card => detailKey(projectId, card.id) === selectedDetailKey.value)?.id
              if (selectedId) await loadCardDetail(projectId, selectedId)
            } catch (error) {
              console.error('世界观已失效的缓存刷新失败，可重新加载重试', error)
            }
          }
        }
      }
    })
  }

  async function createProject(payload: CreateProjectPayload) {
    const version = sessionVersion
    return requests.track('saving', async () => {
      const project = await worldApi.createProject(payload)
      if (version === sessionVersion) {
        requests.invalidate('projects')
        requests.invalidate('publicProjects')
        projects.value.push(project)
        syncProjectLists(project)
      }
      return project
    })
  }

  // 删除整个世界（只有创建者能删）：成功后从各列表和缓存里移除
  async function deleteProject(projectId: string) {
    return requests.track('saving', async () => {
      await worldApi.deleteProject(projectId)
      requests.invalidate('projects')
      requests.invalidate('publicProjects')
      invalidateProject(projectId)
      projects.value = projects.value.filter(p => p.id !== projectId)
      publicProjects.value = publicProjects.value.filter(p => p.id !== projectId)
      snapshots.delete(projectId)
      graphSaves.delete(projectId)
      graphJobs.delete(projectId)
      dirtyProjects.delete(projectId)
      if (activeProjectId.value === projectId) { activeProjectId.value = ''; selectedDetailKey.value = '' }
    })
  }

  // 只改本地显示用的字段（例如协作者人数），不发请求
  function patchProject(projectId: string, patch: Partial<WorldProject>) {
    const target = snapshots.get(projectId)?.project
    if (target) Object.assign(target, patch)
    for (const list of [projects.value, publicProjects.value]) {
      const item = list.find(p => p.id === projectId)
      if (item && item !== target) Object.assign(item, patch)
    }
  }

  async function updateProject(projectId: string, payload: UpdateProjectPayload) {
    const version = sessionVersion
    return mutateProject(projectId, async () => {
      const project = await worldApi.updateProject(projectId, payload)
      if (version === sessionVersion) syncProjectLists(project)
      return project
    })
  }

  // 每次松手立即排队：目标固定，后一次写入等待前一次结束，避免迟到回包覆盖新坐标。
  function saveGraphPositions(projectId: string, positions: string) {
    const version = sessionVersion
    const target = snapshots.get(projectId)?.project
    if (!target) return Promise.resolve()
    const previous = graphSaves.get(projectId)
    // 冲突后保留本页调整供查看，但不再向服务器提交旧布局。
    if (previous?.status === 'conflict') {
      previous.positions = positions
      target.graphPositions = positions
      return Promise.resolve()
    }
    const state = { positions, saved: previous?.saved ?? target.graphPositions ?? '{}', status: 'saving' as const }
    graphSaves.set(projectId, state)
    target.graphPositions = positions
    const job = (graphJobs.get(projectId) || Promise.resolve()).then(async () => {
      if (version !== sessionVersion || graphSaves.get(projectId)?.status === 'conflict') return
      try {
        await worldApi.saveGraphPositions(projectId, positions, graphSaves.get(projectId)!.saved)
        if (version !== sessionVersion) return
        const latest = graphSaves.get(projectId)
        if (latest) {
          latest.saved = positions
          if (graphJobs.get(projectId) === job) latest.status = 'saved'
        }
      } catch (error) {
        if (version !== sessionVersion) return
        const latest = graphSaves.get(projectId)
        const response = (error as { response?: { status: number; data?: { positions?: string } } }).response
        if (latest && response?.status === 409) {
          latest.status = 'conflict'
          latest.saved = response.data?.positions ?? latest.saved
        } else if (latest && graphJobs.get(projectId) === job) latest.status = 'error'
      }
    }).finally(() => { if (graphJobs.get(projectId) === job) graphJobs.delete(projectId) })
    graphJobs.set(projectId, job)
    return job
  }
  const waitForGraphSave = (id: string) => graphJobs.get(id) || Promise.resolve()
  async function reloadGraphPositions(id: string) {
    const version = sessionVersion
    await waitForGraphSave(id)
    const fresh = await worldApi.getProject(id)
    if (version !== sessionVersion) return false
    const target = snapshots.get(id)?.project
    if (target) target.graphPositions = fresh.graphPositions
    graphSaves.delete(id)
    return true
  }
  function discardGraphSave(id: string) {
    const state = graphSaves.get(id), target = snapshots.get(id)?.project
    if (target && state) target.graphPositions = state.saved
    graphSaves.delete(id)
  }

  async function createCard(projectId: string, payload: CreateCardPayload) {
    const card = await mutateProject(projectId, () => worldApi.createCard(projectId, payload))
    return getCardById(card.id, projectId) ?? toCardSummary(card)
  }

  function findCard(cardId: string): CardSummary {
    const card = getCardById(cardId) ?? (currentCard.value?.id === cardId ? toCardSummary(currentCard.value) : undefined)
    if (!card) throw new Error('卡片不存在，请重新加载后重试')
    return card
  }

  async function updateCard(cardId: string, payload: UpdateCardPayload) {
    const projectId = findCard(cardId).projectId
    const card = await mutateProject(projectId, () => worldApi.updateCard(projectId, cardId, payload))
    return getCardById(card.id, projectId) ?? toCardSummary(card)
  }

  async function deleteCard(cardId: string) {
    const projectId = findCard(cardId).projectId
    // 后端已在同一 SaveChanges 中删除双向关系，无需逐条请求。
    await mutateProject(projectId, () => worldApi.deleteCard(projectId, cardId))
  }

  async function addRelation(sourceCardId: string, targetCardId: string, relationType: string) {
    const projectId = findCard(sourceCardId).projectId
    return mutateProject(projectId, () => worldApi.addRelation(sourceCardId, targetCardId, relationType))
  }

  async function removeRelation(cardId: string, relationId: string) {
    const projectId = findCard(cardId).projectId
    await mutateProject(projectId, () => worldApi.removeRelation(cardId, relationId))
  }

  return {
    projects, publicProjects, currentProject, cards, currentCard, cardTypes, allRelations,
    loading: requests.loading, loadingProjects, loadingPublicProjects, loadingCards, loadingDetails, saving,
    fetchProjects, fetchPublicProjects, fetchCardTypes, createProject, updateProject, patchProject, deleteProject,
    graphSaves, saveGraphPositions, waitForGraphSave, discardGraphSave, reloadGraphPositions,
    loadReferences, fetchCards, fetchCardDetail, loadCardDetail, fetchCardsByIds,
    createCard, updateCard, deleteCard, addRelation, removeRelation,
    getCardById, getCardDetailById, reset,
    getCardTitle: (id: string) => getCardById(id)?.title || '已删除的卡片',
  }
})
