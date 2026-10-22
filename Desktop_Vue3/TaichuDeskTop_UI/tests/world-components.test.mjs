import assert from 'node:assert/strict'
import { before, beforeEach, after, test } from 'node:test'
import { fileURLToPath } from 'node:url'
import { createServer } from 'vite'
import vue from '@vitejs/plugin-vue'
import { createPinia, setActivePinia } from 'pinia'
import { createSSRApp, h, effectScope, ref, nextTick } from 'vue'
import { renderToString } from '@vue/server-renderer'
import { createRouter, createMemoryHistory } from 'vue-router'

const root = fileURLToPath(new URL('../', import.meta.url))
const componentPath = '/src/views/世界观管理/世界观组件/'
const date = '2026-10-21T00:00:00Z'
const card = (id, projectId = 'a', extra = {}) => ({ id, projectId, title: id, type: 'character',
  subType: null, coverImage: null, createdAt: date, updatedAt: date, description: null, content: '{}',
  aliases: [], attributes: [], contentBlocks: [], timelineEvents: [], tags: [], embeddedCards: [],
  galleryImages: [], outRelations: [], inRelations: [], ...extra })
const block = (id, order = 0) => ({ id: `block-${id}`, cardId: id, cardType: 'character', order })
const project = id => ({ id, name: '测试世界', description: '世界简介', isPublic: true,
  createdAt: date, updatedAt: date, cardCount: 2, ownerName: '作者', ownerId: 'owner' })
const deferred = () => {
  let resolve, reject
  const promise = new Promise((ok, fail) => { resolve = ok; reject = fail })
  return { promise, resolve, reject }
}
const settle = async () => { await nextTick(); await new Promise(resolve => setImmediate(resolve)) }
let server, pinia, worldApi, useWorldStore, useUserStore, presentation, graph, useCardPreload,
  useCardPresentation, useImagePreview, useExpandedItems, originalApi
const scopes = []
function inScope(setup) {
  const scope = effectScope()
  scopes.push(scope)
  return { value: scope.run(setup), stop: () => scope.stop() }
}

before(async () => {
  server = await createServer({ root, configFile: false, envDir: false, plugins: [vue()],
    resolve: { alias: { '@': `${root}src` } },
    server: { middlewareMode: true, hmr: false, ws: false, watch: null, fs: { allow: [root] } },
    optimizeDeps: { noDiscovery: true, include: [] },
  })
  ;({ worldApi } = await server.ssrLoadModule('/src/api/worldApi.ts'))
  ;({ useWorldStore } = await server.ssrLoadModule('/src/stores/world.ts'))
  ;({ useUserStore } = await server.ssrLoadModule('/src/stores/user.ts'))
  presentation = await server.ssrLoadModule('/src/utils/worldPresentation.ts')
  graph = await server.ssrLoadModule('/src/utils/worldGraph.ts')
  ;({ useCardPreload } = await server.ssrLoadModule('/src/composables/世界观管理/useCardPreload.ts'))
  ;({ useCardPresentation } = await server.ssrLoadModule('/src/composables/世界观管理/useCardPresentation.ts'))
  ;({ useImagePreview } = await server.ssrLoadModule('/src/composables/世界观管理/useImagePreview.ts'))
  ;({ useExpandedItems } = await server.ssrLoadModule('/src/composables/世界观管理/useExpandedItems.ts'))
  originalApi = { ...worldApi }
})
beforeEach(() => {
  scopes.splice(0).forEach(scope => scope.stop())
  const values = new Map()
  globalThis.localStorage = { getItem: key => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, String(value)), removeItem: key => values.delete(key) }
  pinia = createPinia()
  setActivePinia(pinia)
  useUserStore().setSession('fixture-token')
  Object.assign(worldApi, originalApi, { getProject: async id => project(id),
    getCards: async pid => [card('one', pid), card('two', pid)], getProjectRelations: async () => [],
    getCard: async (pid, id) => card(id, pid) })
})
after(async () => {
  scopes.forEach(scope => scope.stop())
  await server?.close()
})

test('legacy image/tag fields retain URL, array and JSON compatibility', () => {
  const { parseCoverImage, parseStringArray } = presentation
  assert.equal(parseCoverImage('https://example.test/a.jpg'), 'https://example.test/a.jpg')
  assert.equal(parseCoverImage('["a.jpg","b.jpg"]'), 'a.jpg')
  for (const value of [null, '', '[]', '{}']) assert.equal(parseCoverImage(value), '')
  assert.deepEqual(parseStringArray('["神话","奇幻"]'), ['神话', '奇幻'])
  assert.deepEqual(parseStringArray(['标签']), ['标签'])
  assert.deepEqual(parseStringArray('invalid json'), [])
  assert.deepEqual(parseStringArray('[null,1,"有效"]'), ['有效'])
})

test('navigation preserves type order, title order, case-insensitive search and empty groups', () => {
  const cards = [card('Zeta', 'a', { type: 'item' }), card('Alpha'), card('beta'), card('unknown', 'a', { type: 'custom' })]
  const groups = presentation.groupWorldCards(cards)
  assert.deepEqual(groups.map(group => [group.label, group.cards.map(card => card.id)]),
    [['角色', ['Alpha', 'beta']], ['物品', ['Zeta']]])
  assert.deepEqual(presentation.groupWorldCards(cards, ' ALP ')[0].cards.map(card => card.id), ['Alpha'])
  assert.deepEqual(presentation.groupWorldCards(cards, 'missing'), [])
  assert.equal(presentation.getCardTypeLabel('custom'), 'custom')
})

test('document and standalone detail retain their distinct embedded-reference rules', () => {
  const source = card('root', 'a', { contentBlocks: [block('one'), block('one'), block('two')],
    description: '{CARD:two} {CARD:three}' })
  assert.deepEqual(presentation.getEmbeddedCardIds(source), ['one', 'two'])
  assert.deepEqual(presentation.getEmbeddedCardIds(source, true), ['one', 'two', 'three'])
})

test('nested cards stop at the depth limit and exclude self and ancestor cycles', () => {
  const blocks = [block('root'), block('parent'), block('child')]
  assert.deepEqual(presentation.getNestedBlocks(blocks, 'root', ['parent'], 0, 2).map(b => b.cardId), ['child'])
  assert.deepEqual(presentation.getNestedBlocks(blocks, 'root', ['parent'], 1, 2), [])
  assert.equal(presentation.getNestedBlocks([{ ...block('child'), id: '' }], 'root', [], 0, 2)[0].blockId, 'child-0')
})

test('inline reference markup retains each view format and escapes substituted titles', () => {
  const find = id => id === 'one' ? card(id, 'a', { title: '<img src=x>' }) : undefined
  assert.equal(presentation.renderCardDescription('<p>{CARD:one}</p>', find, 'document'),
    '<p><a class="inline-ref" data-card-id="one">@角色 &lt;img src=x&gt;</a></p>')
  assert.equal(presentation.renderCardDescription('{CARD:one}', find, 'detail'),
    '<span class="inline-ref">@角色: &lt;img src=x&gt;</span>')
  assert.match(presentation.renderCardDescription('{CARD:deleted}', find, 'detail'), /已删除的卡片/)
})

test('graph retains explicit-edge priority and excludes missing/self embedded targets', () => {
  const cards = [card('one'), card('two'), card('three')]
  const relations = [{ sourceCardId: 'one', targetCardId: 'two', relationType: '朋友' },
    { sourceCardId: 'one', targetCardId: 'deleted', relationType: '丢失' }]
  const result = graph.buildWorldGraph(cards, relations, id => card(id, 'a', {
    contentBlocks: id === 'one' ? ['one', 'two', 'three', 'deleted'].map(block) : [] }))
  assert.equal(result.nodes.length, 3)
  assert.deepEqual(result.links.map(edge => [edge.source, edge.target, edge.isEmbed]),
    [['one', 'two', false], ['one', 'three', true]])
  assert.equal(result.links[0].label, '朋友')
  assert.equal(graph.getGraphTypeColor('organization'), '#7c3aed')
  assert.equal(graph.getGraphTypeColor('creature'), '#84cc16')
  assert.equal(graph.getGraphTypeColor('concept'), '#6366f1')
})

test('graph camera maintains the viewing distance including nodes at the origin', () => {
  for (const node of [{}, { x: 0, y: 0, z: 0 }, { x: 3, y: 4, z: 0 }]) {
    const { position, target } = graph.getNodeCameraPosition(node)
    assert.ok(Object.values(position).every(Number.isFinite))
    assert.ok(Math.abs(Math.hypot(position.x - target.x, position.y - target.y, position.z - target.z) - 100) < 0.001)
  }
})

test('preloads share Store requests/cache without changing the selected card', async () => {
  const store = useWorldStore()
  await store.fetchCards('a')
  await store.fetchCardDetail('a', 'one')
  let calls = 0
  worldApi.getCard = async (pid, id) => { calls++; return card(id, pid) }
  const first = inScope(() => useCardPreload('a')).value
  const second = inScope(() => useCardPreload('a')).value
  await Promise.all([first.load(['', 'one', 'two', 'two']), second.load(['two'])])
  assert.equal(calls, 1)
  assert.equal(first.loading.value, false)
  assert.equal(first.getDetail('two').id, 'two')
  assert.equal(store.currentCard.id, 'one')
})

test('project switches stop subsequent preload batches and cannot mix caches', async () => {
  const pid = ref('a'), ids = ref(Array.from({ length: 10 }, (_, i) => String(i)))
  const pending = deferred(), requested = []
  worldApi.getCard = async (projectId, id) => {
    requested.push(`${projectId}:${id}`)
    if (projectId === 'a') await pending.promise
    return card(id, projectId)
  }
  const loader = inScope(() => useCardPreload(pid, ids)).value
  assert.equal(loader.loading.value, true)
  assert.equal(requested.length, 8)
  pid.value = 'b'; ids.value = ['new']
  await settle()
  assert.equal(loader.getDetail('new').projectId, 'b')
  pending.resolve(); await settle()
  assert.equal(requested.includes('a:8'), false)
  assert.equal(loader.getDetail('0'), null)
  assert.equal(loader.loading.value, false)
})

test('disposing a preload stops queued work and resets loading', async () => {
  const pending = deferred(), requested = []
  worldApi.getCard = async (pid, id) => { requested.push(id); await pending.promise; return card(id, pid) }
  const scope = inScope(() => useCardPreload('a'))
  const task = scope.value.load(Array.from({ length: 10 }, (_, i) => String(i)))
  scope.stop(); pending.resolve(); await task
  assert.equal(requested.length, 8)
  assert.equal(scope.value.loading.value, false)
})

test('a failed preload does not hide successful cards and can be retried', async () => {
  const originalWarn = console.warn
  console.warn = () => {}
  let fail = true
  worldApi.getCard = async (pid, id) => {
    if (id === 'two' && fail) throw new Error('fixture failure')
    return card(id, pid)
  }
  try {
    const loader = inScope(() => useCardPreload('a')).value
    await loader.load(['one', 'two'])
    assert.equal(loader.getDetail('one').id, 'one')
    assert.equal(loader.getDetail('two'), null)
    assert.equal(loader.loading.value, false)
    fail = false; await loader.load(['two'])
    assert.equal(loader.getDetail('two').id, 'two')
  } finally { console.warn = originalWarn }
})

test('card presentation replaces summary fallbacks reactively with cached details', async () => {
  const store = useWorldStore()
  await store.fetchCards('a')
  const source = ref(card('root', 'a', { contentBlocks: [block('one')], description: '{CARD:two}' }))
  const doc = useCardPresentation(source, 'a', 'document')
  const detail = useCardPresentation(source, 'a', 'detail')
  assert.deepEqual(doc.embeddedCards.value.map(c => c.id), ['one'])
  assert.deepEqual(detail.embeddedCards.value.map(c => c.id), ['one', 'two'])
  worldApi.getCard = async (pid, id) => card(id, pid, { description: '完整描述' })
  await store.loadCardDetail('a', 'one')
  assert.equal(doc.embeddedCards.value[0].description, '完整描述')
  source.value = card('next')
  assert.deepEqual(doc.embeddedCards.value, [])
})

test('image preview handles close/reopen, empty images and bounded indexes without mutating inputs', () => {
  const preview = useImagePreview(), urls = ['one.jpg', 'two.jpg']
  preview.open([]); assert.equal(preview.visible.value, false)
  preview.open(urls, 1); urls.pop()
  assert.equal(preview.index.value, 1)
  assert.deepEqual(preview.images.value, ['one.jpg', 'two.jpg'])
  preview.close(); assert.equal(preview.visible.value, false)
  preview.open(['new.jpg'], 9); assert.equal(preview.index.value, 0)
  assert.equal(preview.visible.value, true)
})

test('expanded item states are independent and support both toggle and explicit control', () => {
  const items = useExpandedItems()
  items.toggle('one'); items.toggle('two', true); items.toggle('one', false)
  assert.equal(items.isExpanded('one'), false)
  assert.equal(items.isExpanded('two'), true)
  items.toggle('two'); assert.equal(items.isExpanded('two'), false)
})

async function render(name, props) {
  const component = (await server.ssrLoadModule(`${componentPath}${name}.vue`)).default
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/:pathMatch(.*)*', component: { render: () => null } }] })
  await router.push('/world/project/a'); await router.isReady()
  const app = createSSRApp({ render: () => h(component, props) })
  // 使用当前测试的 Store，保证嵌套组件也读取同一份缓存。
  app.use(pinia); app.use(router)
  app.directive('loading', { getSSRProps: () => ({}) })
  return renderToString(app)
}

test('extracted header and navigation render ownership, search and selected-state correctly', async () => {
  const readOnly = await render('ProjectHeader', { project: project('a'), cardCount: 2, isOwner: false })
  assert.match(readOnly, /作者/); assert.doesNotMatch(readOnly, /header-actions/)
  assert.match(await render('ProjectHeader', { project: project('a'), cardCount: 2, isOwner: true }), /新建/)
  const nav = await render('ProjectCardNavigation', { cards: [card('one'), card('two')],
    loading: false, selectedCardId: 'one', view: 'card', search: 'one' })
  assert.match(nav, /class="[^"]*active[^"]*"[^>]*>one</)
  assert.doesNotMatch(nav, />two</)
  assert.match(await render('ProjectCardNavigation', { cards: [], loading: false,
    selectedCardId: null, view: 'graph', search: 'none' }), /没有匹配的卡片/)
})

test('read-only document keeps images, attributes, nested cards and tags after extraction', async () => {
  const store = useWorldStore()
  await store.fetchCards('a'); await store.loadCardDetail('a', 'two')
  const html = await render('ProjectCardDocument', { projectId: 'a', card: card('主卡片', 'a', {
    coverImage: '["cover.jpg"]', galleryImages: ['gallery.jpg'], attributes: [{ key: '年龄', value: '20' }],
    contentBlocks: [block('two')], tags: ['神话'], description: '<p>故事 {CARD:two}</p>' }) })
  for (const content of ['主卡片', 'cover.jpg', 'gallery.jpg', '年龄', '故事', '@角色 two', '神话', '关联内容']) {
    assert.ok(html.includes(content), `missing ${content}`)
  }
  assert.doesNotMatch(html, /暂无关联内容/)
})

test('collapsed and expanded embedded cards keep fallback titles and depth boundaries', async () => {
  const collapsed = await render('EmbeddedCard', { fallbackTitle: '尚未加载', fallbackType: 'location', collapsible: true, expanded: false })
  assert.match(collapsed, /尚未加载/); assert.match(collapsed, /地点/); assert.doesNotMatch(collapsed, /class="ec-body"/)
  const expanded = await render('EmbeddedCard', { card: card('根卡片', 'a', { description: '完整描述',
    contentBlocks: [block('two')] }), collapsible: true, expanded: true, depth: 1, maxDepth: 2 })
  assert.match(expanded, /完整描述/); assert.doesNotMatch(expanded, /包含内容/)
})

test('create preview and quota banner retain public/private metadata and quota-full actions', async () => {
  const html = await render('ProjectCreatePreview', { form: { name: '新世界', description: '简介', isPublic: false, type: '奇幻', tags: ['甲', '乙'] } })
  for (const text of ['新世界', '私有', '奇幻', '#甲 #乙']) assert.ok(html.includes(text))
  const quotaInfo = { usedWorldCount: 2, maxWorldCount: 2, remainingWorldCount: 0, experience: 100, expCostPerWorldSlot: 10 }
  const full = await render('WorldQuotaBanner', { quotaInfo })
  assert.match(full, /已满/); assert.match(full, /扩充名额/); assert.match(full, /10 经验/)
  const available = await render('WorldQuotaBanner', { quotaInfo: { ...quotaInfo, remainingWorldCount: 1 } })
  assert.match(available, /quota-upgrade-btn/); assert.doesNotMatch(available, /world\/quota/)
  const pending = await render('WorldQuotaBanner', { quotaInfo, upgrading: true })
  assert.match(pending, /disabled/); assert.match(pending, /处理中/)
})
