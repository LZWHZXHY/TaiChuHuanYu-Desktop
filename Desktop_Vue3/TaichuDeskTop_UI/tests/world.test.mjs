import assert from 'node:assert/strict'
import { before, beforeEach, after, test } from 'node:test'
import { fileURLToPath } from 'node:url'
import { createServer } from 'vite'
import { createPinia, setActivePinia } from 'pinia'

let server, worldApi, useWorldStore, useUserStore, request, originalApi, diffRelations
const root = fileURLToPath(new URL('../', import.meta.url))
const date = '2026-10-21T00:00:00Z'
const project = id => ({ id, name: id, description: null, isPublic: false, createdAt: date,
  updatedAt: date, cardCount: 0, ownerName: 'Owner', ownerId: 'owner' })
const detail = (id, projectId = 'a') => ({ id, projectId, title: id, type: 'character', subType: null,
  coverImage: null, createdAt: date, updatedAt: date, description: null, content: '{}', aliases: [],
  attributes: [], contentBlocks: [], timelineEvents: [], tags: [], embeddedCards: [], galleryImages: [],
  outRelations: [], inRelations: [] })
const summary = card => ({ id: card.id, projectId: card.projectId, title: card.title, type: card.type,
  coverImage: card.coverImage, updatedAt: card.updatedAt,
  outRelationCount: card.outRelations.length, inRelationCount: card.inRelations.length })
const relation = (id, sourceCardId, targetCardId, relationType = '朋友') => ({ id, sourceCardId,
  targetCardId, relationType, createdAt: date, sourceCardTitle: sourceCardId, targetCardTitle: targetCardId,
  sourceCardType: 'character', targetCardType: 'character' })
const deferred = () => {
  let resolve, reject
  const promise = new Promise((ok, fail) => { resolve = ok; reject = fail })
  return { promise, resolve, reject }
}

before(async () => {
  server = await createServer({ root, configFile: false, envDir: false,
    resolve: { alias: { '@': `${root}src` } },
    server: { middlewareMode: true, hmr: false, ws: false, watch: null, fs: { allow: [root] } },
    optimizeDeps: { noDiscovery: true, include: [] },
  })
  ;({ worldApi } = await server.ssrLoadModule('/src/api/worldApi.ts'))
  ;({ useWorldStore } = await server.ssrLoadModule('/src/stores/world.ts'))
  ;({ useUserStore } = await server.ssrLoadModule('/src/stores/user.ts'))
  ;({ default: request } = await server.ssrLoadModule('/src/utils/request.ts'))
  ;({ diffRelations } = await server.ssrLoadModule('/src/utils/worldCards.ts'))
  originalApi = { ...worldApi }
})
beforeEach(() => {
  const values = new Map()
  globalThis.localStorage = {
    getItem: key => values.get(key) ?? null, setItem: (key, value) => values.set(key, String(value)),
    removeItem: key => values.delete(key),
  }
  setActivePinia(createPinia())
  useUserStore().setSession('token-a')
  Object.assign(worldApi, originalApi, {
    getProject: async id => project(id), getCards: async () => [], getProjectRelations: async () => [],
    getCard: async (pid, id) => detail(id, pid),
  })
})
after(async () => { await server?.close() })

// 可变的模拟后端：回归检查观察真实服务返回值如何进入 Store，而非检查实现细节。
function backend() {
  const data = new Map([['one', detail('one')], ['two', detail('two')]])
  let relations = []
  let nextId = 0
  const getDetail = id => ({ ...data.get(id), outRelations: relations.filter(r => r.sourceCardId === id),
    inRelations: relations.filter(r => r.targetCardId === id) })
  Object.assign(worldApi, {
    getProject: async id => ({ ...project(id), cardCount: data.size }),
    getCards: async () => [...data.keys()].map(id => summary(getDetail(id))),
    getProjectRelations: async () => structuredClone(relations),
    getCard: async (_, id) => structuredClone(getDetail(id)),
    createCard: async (pid, payload) => {
      const created = { ...detail(`new-${++nextId}`, pid), ...payload }
      data.set(created.id, created)
      return getDetail(created.id)
    },
    updateCard: async (_, id, payload) => { data.set(id, { ...data.get(id), ...payload }); return getDetail(id) },
    deleteCard: async (_, id) => { data.delete(id); relations = relations.filter(r => r.sourceCardId !== id && r.targetCardId !== id) },
    addRelation: async (source, target, type) => {
      const added = relation(`r-${++nextId}`, source, target, type)
      relations.push(added)
      return added
    },
    removeRelation: async (_, id) => { relations = relations.filter(r => r.id !== id) },
  })
  return data
}

test('API returns DTO bodies directly and sends card filtering through params', async () => {
  const originalGet = request.get
  const originalDelete = request.delete
  const body = [summary(detail('one'))]
  try {
    request.get = async (url, config) => {
      assert.equal(url, '/world/projects/a/cards')
      assert.deepEqual(config.params, { type: 'type & more' })
      return body
    }
    assert.equal(await originalApi.getCards('a', 'type & more'), body)
    request.delete = async () => ''
    assert.equal(await originalApi.deleteCard('a', 'one'), undefined)
  } finally { request.get = originalGet; request.delete = originalDelete }
})

test('an empty project is cached and explicit refresh bypasses it', async () => {
  const store = useWorldStore()
  let calls = 0
  worldApi.getCards = async () => { calls++; return [] }
  await store.fetchCards('a')
  await store.fetchCards('a')
  assert.equal(calls, 1)
  await store.fetchCards('a', true)
  assert.equal(calls, 2)
  assert.equal(store.loading, false)
})

test('concurrent project loads deduplicate and an old project cannot replace the active project', async () => {
  const store = useWorldStore()
  const a = deferred(), b = deferred()
  let calls = 0
  worldApi.getCards = id => { calls++; return id === 'a' ? a.promise : b.promise }
  const first = store.fetchCards('a'), same = store.fetchCards('a')
  const second = store.fetchCards('b')
  b.resolve([summary(detail('b-card', 'b'))])
  await second
  assert.equal(store.currentProject.id, 'b')
  assert.equal(store.loadingCards, false)
  assert.equal(store.loading, true)
  a.resolve([summary(detail('a-card', 'a'))])
  await Promise.all([first, same])
  assert.equal(calls, 2)
  assert.equal(store.currentProject.id, 'b')
  assert.deepEqual(store.cards.map(c => c.id), ['b-card'])
  assert.equal(store.loading, false)
})

test('independent list loading flags remain accurate when one request finishes first', async () => {
  const store = useWorldStore(), own = deferred(), publicList = deferred()
  worldApi.getProjects = () => own.promise
  worldApi.getPublicProjects = () => publicList.promise
  const a = store.fetchProjects(), b = store.fetchPublicProjects()
  own.resolve([project('a')]); await a
  assert.equal(store.loadingProjects, false)
  assert.equal(store.loadingPublicProjects, true)
  assert.equal(store.loading, true)
  publicList.resolve([]); await b
  assert.equal(store.loading, false)
})

test('detail requests deduplicate; background preloads do not select cards or pollute the active project', async () => {
  backend()
  const store = useWorldStore()
  await store.fetchCards('a')
  await store.fetchCardDetail('a', 'one')
  let calls = 0
  worldApi.getCard = async (pid, id) => { calls++; return detail(id, pid) }
  await Promise.all([store.loadCardDetail('b', 'other'), store.loadCardDetail('b', 'other')])
  assert.equal(calls, 1)
  assert.equal(store.currentCard.id, 'one')
  assert.deepEqual(store.cards.map(c => c.id), ['one', 'two'])
  assert.equal(store.getCardDetailById('other'), undefined)
  assert.equal(store.getCardDetailById('other', 'b').id, 'other')
})

test('selecting a later card wins even if the earlier request completes last', async () => {
  const store = useWorldStore(), one = deferred(), two = deferred()
  worldApi.getCard = (_, id) => id === 'one' ? one.promise : two.promise
  const a = store.fetchCardDetail('a', 'one'), b = store.fetchCardDetail('a', 'two')
  two.resolve(detail('two')); await b
  assert.equal(store.loadingDetails, false)
  one.resolve(detail('one')); await a
  assert.equal(store.currentCard.id, 'two')
})

test('batch fetch returns cached and fetched cards in input order, including repeated IDs', async () => {
  const store = useWorldStore()
  await store.loadCardDetail('a', 'one')
  let calls = 0
  worldApi.getCard = async (pid, id) => { calls++; return detail(id, pid) }
  const result = await store.fetchCardsByIds('a', ['one', 'two', 'one'])
  assert.deepEqual(result.map(c => c.id), ['one', 'two', 'one'])
  assert.equal(calls, 1)
  assert.equal(store.getCardDetailById('two', 'a').id, 'two')
})

test('create with relations refreshes cards, graph, both counters and project metadata', async () => {
  backend()
  const store = useWorldStore()
  await store.fetchCards('a')
  const created = await store.createCard('a', { title: 'Created', type: 'character',
    relations: [{ targetCardId: 'one', relationType: '朋友' }] })
  assert.equal(store.cards.length, 3)
  assert.equal(store.currentProject.cardCount, 3)
  assert.equal(store.allRelations.length, 1)
  assert.equal(store.getCardById(created.id).outRelationCount, 1)
  assert.equal(store.getCardById('one').inRelationCount, 1)
})

test('editing only a relation type replaces that relation and refreshes both details', async () => {
  backend()
  const store = useWorldStore()
  await store.fetchCards('a')
  await store.addRelation('one', 'two', '朋友')
  await store.fetchCardDetail('a', 'one')
  await store.loadCardDetail('a', 'two')
  await store.updateCard('one', { title: 'Renamed', relations: [{ targetCardId: 'two', relationType: '敌人' }] })
  assert.equal(store.allRelations.length, 1)
  assert.equal(store.allRelations[0].relationType, '敌人')
  assert.equal(store.currentCard.outRelations[0].relationType, '敌人')
  assert.equal(store.getCardById('one').title, 'Renamed')
  assert.equal(store.getCardDetailById('two'), undefined)
  assert.equal((await store.loadCardDetail('a', 'two')).inRelations[0].relationType, '敌人')
})

test('deleting a relation updates both endpoints and the selected detail', async () => {
  backend()
  const store = useWorldStore()
  await store.fetchCards('a')
  const rel = await store.addRelation('one', 'two', '朋友')
  await store.fetchCardDetail('a', 'two')
  await store.removeRelation('one', rel.id)
  assert.equal(store.getCardById('one').outRelationCount, 0)
  assert.equal(store.getCardById('two').inRelationCount, 0)
  assert.equal(store.currentCard.inRelations.length, 0)
  assert.equal(store.allRelations.length, 0)
})

test('card deletion relies on server relationship cleanup and removes stale details', async () => {
  backend()
  const store = useWorldStore()
  await store.fetchCards('a')
  await store.addRelation('one', 'two', '朋友')
  await store.fetchCardDetail('a', 'one')
  worldApi.removeRelation = async () => { throw new Error('Should not delete relationships individually') }
  await store.deleteCard('one')
  assert.equal(store.currentCard, null)
  assert.equal(store.getCardById('one'), undefined)
  assert.equal(store.getCardById('two').inRelationCount, 0)
  assert.equal(store.currentProject.cardCount, 1)
})

test('partial relation save failures reconcile successful server changes and release saving state', async () => {
  backend()
  const store = useWorldStore()
  await store.fetchCards('a')
  worldApi.addRelation = async () => { throw new Error('relation failed') }
  await assert.rejects(store.updateCard('one', { title: 'Saved title',
    relations: [{ targetCardId: 'two', relationType: '朋友' }] }), /relation failed/)
  assert.equal(store.getCardById('one').title, 'Saved title')
  assert.equal(store.saving, false)
  assert.equal(store.loading, false)
})

test('old in-flight reads cannot overwrite a mutation refresh', async () => {
  backend()
  const store = useWorldStore()
  await store.fetchCards('a')
  const old = deferred(), realGet = worldApi.getCard
  worldApi.getCard = () => old.promise
  const stale = store.loadCardDetail('a', 'one')
  worldApi.getCard = realGet
  await store.updateCard('one', { title: 'Latest' })
  old.resolve(detail('one')); await stale
  assert.equal(store.getCardById('one').title, 'Latest')
})

test('failed reads release pending requests and permit retry', async () => {
  const store = useWorldStore()
  worldApi.getCards = async () => { throw new Error('offline') }
  await assert.rejects(store.fetchCards('a'), /offline/)
  assert.equal(store.loading, false)
  worldApi.getCards = async () => []
  await store.fetchCards('a')
  assert.equal(store.currentProject.id, 'a')
})

test('explicit refresh ignores an earlier response and its loading state', async () => {
  const store = useWorldStore(), old = deferred()
  worldApi.getCards = () => old.promise
  const stale = store.fetchCards('a')
  worldApi.getCards = async () => [summary(detail('latest'))]
  await store.fetchCards('a', true)
  assert.equal(store.loadingCards, false)
  assert.equal(store.loading, false)
  old.resolve([summary(detail('old'))]); await stale
  assert.deepEqual(store.cards.map(card => card.id), ['latest'])
})

test('a successful save stays successful when refresh fails and can be reloaded later', async () => {
  backend()
  const store = useWorldStore()
  await store.fetchCards('a')
  const getCards = worldApi.getCards, log = console.error
  worldApi.getCards = async () => { throw new Error('refresh offline') }
  console.error = () => {}
  try {
    await store.updateCard('one', { title: 'Saved remotely' })
    assert.equal(store.saving, false)
  } finally { worldApi.getCards = getCards; console.error = log }
  await store.fetchCards('a')
  assert.equal(store.getCardById('one').title, 'Saved remotely')
})

test('session changes clear all project caches and reject old state commits', async () => {
  backend()
  const store = useWorldStore()
  await store.fetchCards('a')
  const old = deferred()
  worldApi.getCard = () => old.promise
  const pending = store.fetchCardDetail('a', 'one')
  useUserStore().logout()
  old.resolve(detail('one')); await pending
  assert.equal(store.currentProject, null)
  assert.equal(store.currentCard, null)
  assert.equal(store.cards.length, 0)
  assert.equal(store.getCardDetailById('one', 'a'), undefined)
  assert.equal(store.loading, false)
})

test('stores in separate Pinia instances never share pending requests', async () => {
  let calls = 0
  worldApi.getCards = async () => { calls++; return [] }
  await Promise.all([useWorldStore(createPinia()).fetchCards('a'), useWorldStore(createPinia()).fetchCards('a')])
  assert.equal(calls, 2)
})

test('card type DTOs are mapped once into selector options', async () => {
  worldApi.getCardTypes = async () => [{ id: 'character', label: '角色', icon: null }]
  const store = useWorldStore()
  await store.fetchCardTypes()
  assert.deepEqual(store.cardTypes, [{ value: 'character', label: '角色', icon: undefined }])
})

test('relation diff treats a changed type as removal plus addition', () => {
  const old = relation('r', 'one', 'two', '朋友')
  assert.deepEqual(diffRelations([old], [{ targetCardId: 'two', relationType: '敌人' }]), {
    toRemove: [old], toAdd: [{ targetCardId: 'two', relationType: '敌人' }],
  })
})
