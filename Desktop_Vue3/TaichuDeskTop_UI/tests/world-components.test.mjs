import assert from 'node:assert/strict'
import { before, beforeEach, after, test } from 'node:test'
import { fileURLToPath } from 'node:url'
import { createServer } from 'vite'
import vue from '@vitejs/plugin-vue'
import { createPinia, setActivePinia } from 'pinia'
import { createSSRApp, createRenderer, reactive, h, effectScope, ref, nextTick } from 'vue'
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
let transfer, graphLayout, server, pinia, worldApi, useWorldStore, useUserStore, presentation, graph, useCardPreload,
  useCardPresentation, useImagePreview, useExpandedItems, originalApi
const scopes = []
test('graph labels keep readable screen bounds and avoid overlapping on narrow screens', () => {
  const points = Array.from({ length: 1000 }, (_, i) => ({ id: String(i), x: 10 + i % 20 * 19,
    y: 30 + Math.floor(i / 20) * 12, z: i / 1000, width: i % 7 ? 92 : 176 }))
  const labels = graph.placeGraphLabels(points, 390, 480)
  assert.ok(labels.length > 5 && labels.length <= 34)
  for (const [i, a] of labels.entries()) {
    assert.ok(a.left >= 4 && a.top >= 4 && a.left + a.width <= 386 && a.top + 32 <= 476)
    for (const b of labels.slice(i + 1)) {
      assert.ok(a.left >= b.left + b.width + 4 || a.left + a.width + 4 <= b.left || a.top >= b.top + 36 || a.top + 36 <= b.top)
    }
  }
  assert.deepEqual(graph.placeGraphLabels([...points].reverse(), 390, 480), labels)
  assert.equal(points.length, 1000)
})
test('graph labels prioritize the active node and ignore offscreen or invalid coordinates', () => {
  const a = { id: 'near', x: 150, y: 150, z: 0, width: 100 }
  const b = { ...a, id: 'active', z: 0.9, priority: true }
  const labels = graph.placeGraphLabels([a, b, { ...a, id: 'invalid', x: NaN }, { ...a, id: 'outside', y: -100 }], 390, 480)
  assert.deepEqual(labels.map(point => point.id), ['active', 'near'])
  assert.ok(labels[0].top + 36 <= labels[1].top)
  assert.deepEqual(graph.placeGraphLabels([a], 0, 0), [])
})
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
  transfer = await server.ssrLoadModule('/src/utils/worldTransfer.ts')
  graphLayout = await server.ssrLoadModule('/src/utils/worldGraphLayout.ts')
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
    setItem: (key, value) => values.set(key, String(value)), removeItem: key => values.delete(key),
    get length() { return values.size }, key: index => [...values.keys()][index] ?? null }
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
    [['角色', ['Alpha', 'beta']], ['物品', ['Zeta']], ['custom', ['unknown']]])
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
    '&lt;p&gt;<a class="inline-ref" data-card-id="one">@角色 &lt;img src=x&gt;</a>&lt;/p&gt;')
  assert.equal(presentation.renderCardDescription('{CARD:one}', find, 'detail'),
    '<a class="inline-ref" data-card-id="one">@角色: &lt;img src=x&gt;</a>')
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

test('graph includes inline references once, prioritizes relations and excludes unavailable targets', () => {
  const cards = ['one', 'two', 'three', 'four'].map(id => card(id))
  const result = graph.buildWorldGraph(cards,
    [{ sourceCardId: 'one', targetCardId: 'two', relationType: '朋友' }],
    id => card(id, 'a', id === 'one' ? {
      contentBlocks: [block('two'), block('three'), block('three')],
      description: '{CARD:one} {CARD:two} {CARD:three} {CARD:four} {CARD:four} {CARD:private}'
    } : {}))
  assert.deepEqual(result.links.map(edge => [edge.source, edge.target, edge.isEmbed]),
    [['one', 'two', false], ['one', 'three', true], ['one', 'four', true]])
})

test('graph reset centers displaced nodes and fits both wide and narrow canvases', () => {
  const nodes = [{ x: 100, y: 50, z: 0 }, { x: 200, y: 150, z: 100 }]
  const wide = graph.getGraphCameraPosition(nodes, 2, 50)
  const narrow = graph.getGraphCameraPosition(nodes, 0.5, 50)
  assert.deepEqual(wide.target, { x: 150, y: 100, z: 50 })
  assert.ok(narrow.position.z > wide.position.z)
  for (const point of [wide.position, narrow.position, graph.getGraphCameraPosition([], 0, 50).position])
    assert.ok(Object.values(point).every(Number.isFinite))
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
  assert.match(readOnly, /作者/); assert.doesNotMatch(readOnly, /create-btn|visibility-btn|AI 导入设定/)
  assert.doesNotMatch(readOnly, /AI 整理|导入 TXT|AI 导入/)
  assert.match(await render('ProjectHeader', { project: project('a'), cardCount: 2, isOwner: false, canOrganize: true }), /AI 整理/)
  assert.match(await render('ProjectHeader', { project: project('a'), cardCount: 2, isOwner: true }), /新建/)
  const nav = await render('ProjectCardNavigation', { cards: [card('one'), card('two')],
    loading: false, selectedCardId: 'one', view: 'card', search: 'one' })
  assert.match(nav, /class="[^"]*active[^"]*"[^>]*>one</)
  assert.doesNotMatch(nav, />two</)
  assert.match(await render('ProjectCardNavigation', { cards: [], loading: false,
    selectedCardId: null, view: 'graph', search: 'none' }), /没有匹配的设定/)
})

test('read-only document keeps images, attributes, nested cards and tags after extraction', async () => {
  const store = useWorldStore()
  await store.fetchCards('a'); await store.loadCardDetail('a', 'two')
  const html = await render('ProjectCardDocument', { projectId: 'a', card: card('主卡片', 'a', {
    coverImage: '["cover.jpg"]', galleryImages: ['gallery.jpg'], attributes: [{ key: '年龄', value: '20' }],
    contentBlocks: [{...block('two'), contextLabel: '所属组织'}], tags: ['神话'], description: '<p>故事 {CARD:two}</p>' }) })
  for (const content of ['主卡片', 'cover.jpg', 'gallery.jpg', '年龄', '故事', '@角色 two', '神话', '引用设定', '所属组织']) {
    assert.ok(html.includes(content), `missing ${content}`)
  }
  assert.doesNotMatch(html, /暂无关联内容/)
})

test('collapsed and expanded embedded cards keep fallback titles and depth boundaries', async () => {
  const collapsed = await render('EmbeddedCard', { fallbackTitle: '尚未加载', fallbackType: 'location', collapsible: true, expanded: false })
  assert.match(collapsed, /尚未加载/); assert.match(collapsed, /地点/); assert.doesNotMatch(collapsed, /class="ec-body"/)
  const expanded = await render('EmbeddedCard', { card: card('根卡片', 'a', { description: '完整描述',
    attributes: [{key:'空属性',value:''}, {key:'计数',value:0}], contentBlocks: [block('two')] }), collapsible: true, expanded: true, depth: 1, maxDepth: 2 })
  assert.match(expanded, /完整描述/); assert.doesNotMatch(expanded, /包含内容/)
  assert.doesNotMatch(expanded, /空属性/); assert.match(expanded, /计数/)
})

test('quota banner retains quota-full and upgrade actions', async () => {
  const quotaInfo = { usedWorldCount: 2, maxWorldCount: 2, remainingWorldCount: 0, experience: 100, expCostPerWorldSlot: 10 }
  const full = await render('WorldQuotaBanner', { quotaInfo })
  assert.match(full, /已满/); assert.match(full, /扩充名额/); assert.match(full, /10 经验/)
  const available = await render('WorldQuotaBanner', { quotaInfo: { ...quotaInfo, remainingWorldCount: 1 } })
  assert.match(available, /quota-upgrade-btn/); assert.doesNotMatch(available, /world\/quota/)
  const pending = await render('WorldQuotaBanner', { quotaInfo, upgrading: true })
  assert.match(pending, /disabled/); assert.match(pending, /处理中/)
})

 test('directory exposes matching entries without requiring a selected card', async () => {
  const html = await render('ProjectCardNavigation', {cards: [card('one'), card('two')], loading: false, selectedCardId: null, view: 'card', search: 'two'})
  assert.match(html, />two</)
  assert.doesNotMatch(html, />one</)
  assert.match(html, /世界概览/)
})

test('reading retains type-specific values, zeroes and references while hiding empty attributes', async () => {
  const store = useWorldStore(); await store.fetchCards('a')
  const html = await render('ProjectCardDocument', {projectId:'a', card:card('事件', 'a', {type:'event', content:JSON.stringify({locationId:'one', outcome:'胜利', count:0}), attributes:[{key:'空项',value:''}]})})
  for(const text of ['发生地点', 'one', '胜利', 'count', '>0<']) assert.ok(html.includes(text), text)
  assert.doesNotMatch(html, /空项|暂无关联内容/)
})

async function editorFixture(checkQuota = async () => true) {
  const { useCardEditor } = await server.ssrLoadModule('/src/composables/世界观管理/useCardEditor.ts')
  const { useCardForm } = await server.ssrLoadModule('/src/composables/世界观管理/useCardForm.ts')
  const router = createRouter({ history: createMemoryHistory(), routes: [
    { path: '/world/project/:projectId/card/:cardId', component: { render: () => null } },
  ] })
  await router.push('/world/project/a/card/old'); await router.isReady()
  const app = createSSRApp({ render: () => null }); app.use(pinia); app.use(router)
  return app.runWithContext(() => inScope(() => {
    const fields = useCardForm()
    return { ...fields, ...useCardEditor(fields.form, fields.resetForm, fields.setFormData, checkQuota, 'a') }
  }).value)
}

test('new entry from a detail URL does not reload or overwrite the previous card', async t => {
  const { ElMessage } = await import('element-plus')
  t.mock.method(ElMessage, 'success', () => {})
  const editor = await editorFixture()
  await editor.loadCardData(null)
  assert.equal(editor.isCreating.value, true)
  assert.equal(editor.currentCardId.value, '')
  assert.equal(editor.form.value.title, '')
  let creates = 0, updates = 0
  const store = useWorldStore()
  store.createCard = async () => { creates++; return card('created') }
  store.updateCard = async id => { assert.equal(id, 'created'); updates++ }
  editor.form.value.title = '新条目'
  await editor.handleSave(() => {})
  await editor.handleSave(() => {})
  assert.equal(creates, 1); assert.equal(updates, 1)
})

test('editing arrays are isolated from cached reading data', async () => {
  const editor = await editorFixture()
  const original = card('old', 'a', { galleryImages: ['cover.png'],
    contentBlocks: [{ ...block('two'), contextLabel: '所属组织' }],
    timelineEvents: [{ date: '100', title: '启航', description: '' }] })
  useWorldStore().fetchCardDetail = async () => original
  await editor.loadCardData({ id: 'old' })
  editor.form.value.galleryImages.push('draft.png')
  editor.form.value.contentBlocks[0].contextLabel = '未保存'
  editor.form.value.timelineEvents[0].title = '未保存'
  assert.deepEqual(original.galleryImages, ['cover.png'])
  assert.equal(original.contentBlocks[0].contextLabel, '所属组织')
  assert.equal(original.timelineEvents[0].title, '启航')
})

test('failed detail loading cannot fall back to creating or saving an empty entry', async () => {
  const editor = await editorFixture(), store = useWorldStore()
  store.fetchCardDetail = async () => { throw new Error('read unavailable') }
  store.createCard = store.updateCard = async () => assert.fail('failed load must block writes')
  await editor.loadCardData({ id: 'old' })
  assert.equal(editor.loadError.value, true)
  assert.equal(editor.isCreating.value, false)
  assert.equal(editor.currentCardId.value, 'old')
  editor.form.value.title = '未读取完整的草稿'
  await editor.handleSave(() => assert.fail('must not report success'))
})

test('leaving a draft during the quota check cannot save the replacement form', async () => {
  const pending = deferred(), editor = await editorFixture(() => pending.promise)
  useWorldStore().createCard = async () => assert.fail('obsolete draft must not be written')
  editor.form.value.title = '旧草稿'
  const saving = editor.handleSave(() => assert.fail('obsolete draft must not report success'))
  await editor.loadCardData(null)
  editor.form.value.title = '新草稿'
  pending.resolve(true); await saving
  assert.equal(editor.form.value.title, '新草稿')
  assert.equal(editor.currentCardId.value, '')
})

test('readable reference labels round-trip IDs, duplicate names, missing targets and plain text safely', async () => {
  const { parseWorldEditorText, serializeWorldEditorText } = await server.ssrLoadModule('/src/utils/worldEditorText.ts')
  const entries = [card('one', 'a', {title:'同名',type:'character'}), card('two', 'a', {title:'同名',type:'location'})]
  const raw = '正文 <script>不是HTML</script>\n{CARD:one} 与 {CARD:two}\n\n{CARD:missing}\n'
  const doc = parseWorldEditorText(raw, entries)
  assert.equal(serializeWorldEditorText(doc), raw)
  assert.equal(doc.content[1].content[0].attrs.label, '@角色 同名')
  assert.equal(doc.content[1].content[2].attrs.label, '@地点 同名')
  assert.equal(doc.content[3].content[0].attrs.label, '@不可用的条目')
  entries[0].title = '新名称'
  const renamed = parseWorldEditorText(raw, entries)
  assert.equal(renamed.content[1].content[0].attrs.label, '@角色 新名称')
  assert.equal(serializeWorldEditorText(renamed), raw)
  assert.equal(serializeWorldEditorText(parseWorldEditorText('', entries)), '')
})

test('dirty state covers nested edits, undo, failed saves and successful saves', async t => {
  const { ElMessage } = await import('element-plus')
  t.mock.method(ElMessage, 'success', () => {}); t.mock.method(ElMessage, 'error', () => {})
  const editor = await editorFixture(), store = useWorldStore()
  store.fetchCardDetail = async () => card('old', 'a', {title:'原名', description:'正文',
    contentBlocks:[{...block('two'),contextLabel:'组织'}]})
  await editor.loadCardData({id:'old'})
  assert.equal(editor.isDirty.value, false)
  editor.form.value.contentBlocks[0].contextLabel = '新说明'
  assert.equal(editor.isDirty.value, true)
  editor.form.value.contentBlocks[0].contextLabel = '组织'
  assert.equal(editor.isDirty.value, false)
  editor.form.value.description = '修改后的正文'
  store.updateCard = async () => { throw new Error('save failed') }
  await editor.handleSave(() => assert.fail('failed save must not leave'))
  assert.equal(editor.isDirty.value, true)
  assert.equal(editor.form.value.description, '修改后的正文')
  store.updateCard = async () => {}
  await editor.handleSave(() => assert.equal(editor.isDirty.value, false))
  assert.equal(editor.isDirty.value, false)
})

test('draft recovery isolates accounts, worlds and entries and restores nested reference data', async () => {
  useUserStore().setSession('token-a', { id: 'account-a' })
  const editor = await editorFixture()
  await editor.loadCardData({ id: 'one' })
  editor.form.value.description = '正文 {CARD:two}'
  editor.form.value.relations.push({ targetCardId: 'two', relationType: '故乡' })
  editor.form.value.timelineEvents.push({ date: '一年', title: '启程', description: null })
  const expected = JSON.parse(JSON.stringify(editor.form.value))
  await editor.loadCardData({ id: 'two' })
  assert.equal(editor.draft.pending.value, null)
  await editor.loadCardData({ id: 'one' })
  assert.deepEqual(editor.draft.pending.value.form, expected)
  assert.equal(editor.form.value.description, '') // never applies without consent
  editor.draft.restore()
  assert.deepEqual(editor.form.value, expected)
  assert.equal(editor.isDirty.value, true)
  const { useCardDraft } = await server.ssrLoadModule('/src/composables/世界观管理/useCardDraft.ts')
  const otherWorld = inScope(() => useCardDraft(ref(expected))).value
  otherWorld.bind('b', 'one')
  assert.equal(otherWorld.pending.value, null)
  useUserStore().setSession('token-b', { id: 'account-b' })
  assert.equal(editor.draft.pending.value, null)
  assert.equal(editor.form.value.description, '')
  await editor.loadCardData({ id: 'one' })
  assert.equal(editor.draft.pending.value, null)
  editor.form.value.description = '账号 B'
  useUserStore().setSession('token-a-again', { id: 'account-a' })
  await editor.loadCardData({ id: 'one' })
  assert.equal(editor.draft.pending.value.form.description, expected.description)
})

test('new-entry backups survive failed save, clear on success, and never become another entry', async t => {
  const { ElMessage } = await import('element-plus')
  t.mock.method(ElMessage, 'success', () => {})
  t.mock.method(ElMessage, 'error', () => {})
  useUserStore().setSession('token-a', { id: 'account-a' })
  const editor = await editorFixture(), store = useWorldStore()
  await editor.loadCardData(null)
  editor.form.value.title = '新人物草稿'
  t.mock.method(store, 'createCard', async () => { throw new Error('offline') })
  await editor.handleSave(() => assert.fail())
  await editor.loadCardData(null)
  assert.equal(editor.draft.pending.value.form.title, '新人物草稿')
  editor.draft.restore()
  store.createCard = async () => card('created')
  await editor.handleSave(() => {})
  await editor.loadCardData(null)
  assert.equal(editor.draft.pending.value, null)
})

test('drafts flag changed server content and remain hidden on permission failure', async t => {
  useUserStore().setSession('token-a', { id: 'account-a' })
  const editor = await editorFixture(), store = useWorldStore()
  await editor.loadCardData({ id: 'one' })
  editor.form.value.description = 'local draft'
  t.mock.method(store, 'fetchCardDetail', async () => card('one', 'a', { description: 'changed remotely' }))
  await editor.loadCardData({ id: 'one' })
  assert.equal(editor.draft.serverChanged.value, true)
  assert.equal(editor.form.value.description, 'changed remotely')
  store.fetchCardDetail = async () => { throw new Error('403') }
  await editor.loadCardData({ id: 'one' })
  assert.equal(editor.draft.pending.value, null)
  assert.equal(editor.loadError.value, true)
})

test('unavailable storage and malformed drafts do not crash or silently claim a backup', async t => {
  useUserStore().setSession('token-a', { id: 'account-a' })
  const editor = await editorFixture()
  t.mock.method(localStorage, 'getItem', () => '{broken')
  await editor.loadCardData({ id: 'one' })
  assert.equal(editor.draft.pending.value, null)
  assert.match(editor.draft.error.value, /无法读取/)
  t.mock.method(localStorage, 'setItem', () => { throw new Error('quota exceeded') })
  editor.form.value.description = 'not lost in memory'
  assert.equal(editor.draft.flush(), false)
  assert.equal(editor.draft.savedAt.value, 0)
  assert.match(editor.draft.error.value, /备份失败/)
})

test('account change cancels pending load before it can bind or expose another account draft', async t => {
  useUserStore().setSession('token-a', { id: 'account-a' })
  const editor = await editorFixture(), store = useWorldStore(), pending = deferred()
  t.mock.method(store, 'fetchCardDetail', () => pending.promise)
  const loading = editor.loadCardData({ id: 'one' })
  useUserStore().setSession('token-b', { id: 'account-b' })
  pending.resolve(card('one', 'a', { description: 'private A' }))
  await loading
  assert.equal(editor.form.value.description, '')
  assert.equal(editor.draft.pending.value, null)
  assert.equal(editor.draft.flush(), false)
})

test('undo and explicit discard clear only the current entry backup', async () => {
  useUserStore().setSession('token-a', { id: 'account-a' })
  const editor = await editorFixture()
  await editor.loadCardData({ id: 'one' })
  editor.form.value.description = 'keep one'
  await editor.loadCardData({ id: 'two' })
  editor.form.value.description = 'temporary'
  editor.form.value.description = ''
  await editor.loadCardData({ id: 'two' })
  assert.equal(editor.draft.pending.value, null)
  editor.form.value.description = 'discard two'
  await editor.loadCardData({ id: 'two' })
  editor.draft.clear()
  await editor.loadCardData({ id: 'two' })
  assert.equal(editor.draft.pending.value, null)
  await editor.loadCardData({ id: 'one' })
  assert.equal(editor.draft.pending.value.form.description, 'keep one')
})

test('two editors keep separate backups; saving or undoing one never clears the other', async () => {
  useUserStore().setSession('token-a', { id: 'account-a' })
  const a = await editorFixture(), b = await editorFixture()
  await a.loadCardData({ id: 'one' }); await b.loadCardData({ id: 'one' })
  a.form.value.description = 'A'
  b.form.value.description = 'B'
  await a.draft.acceptSaved()
  const reopened = await editorFixture()
  await reopened.loadCardData({ id: 'one' })
  assert.deepEqual(reopened.draft.candidates.value.map(d => d.form.description), ['B'])
  b.form.value.description = ''
  await reopened.loadCardData({ id: 'one' })
  assert.equal(reopened.draft.pending.value, null)
})

test('selected draft changing elsewhere cannot be restored or discarded as an old snapshot', async () => {
  useUserStore().setSession('token-a', { id: 'account-a' })
  const a = await editorFixture(), b = await editorFixture()
  await a.loadCardData({ id: 'one' }); a.form.value.description = 'A1'
  await b.loadCardData({ id: 'one' })
  a.form.value.description = 'A2'
  b.draft.restore()
  assert.equal(b.form.value.description, '')
  assert.equal(b.draft.pending.value.form.description, 'A2')
  b.draft.restore()
  assert.equal(b.form.value.description, 'A2')
  await b.draft.acceptSaved()
  await a.loadCardData({ id: 'one' })
  assert.equal(a.draft.pending.value.form.description, 'A2')
})

test('old recovered baseline survives another reload and blocks conflicting server overwrite', async t => {
  const { ElMessage } = await import('element-plus')
  t.mock.method(ElMessage, 'success', () => {})
  useUserStore().setSession('token-a', { id: 'account-a' })
  const editor = await editorFixture(), store = useWorldStore()
  await editor.loadCardData({ id: 'one' })
  editor.form.value.description = 'old local draft'
  store.fetchCardDetail = async () => card('one', 'a', { description: 'new server body', title: 'new server title', updatedAt: '2026-10-25T12:00:00.123456' })
  await editor.loadCardData({ id: 'one' }); editor.draft.restore()
  await editor.loadCardData({ id: 'one' })
  assert.equal(editor.draft.serverChanged.value, true)
  editor.draft.restore()
  let writes = 0, payload
  store.updateCard = async (_id, value) => { writes++; payload = value }
  await editor.handleSave(() => assert.fail('conflict must not save'))
  assert.equal(writes, 0)
  assert.deepEqual(editor.conflicts.value, ['description'])
  assert.equal(editor.remoteForm.value.description, 'new server body')
  editor.conflictChoices.value.description = 'local'
  editor.resolveConflicts()
  assert.equal(editor.form.value.title, 'new server title')
  await editor.handleSave(() => {})
  assert.equal(writes, 1)
  assert.equal(payload.expectedUpdatedAt, '2026-10-25T12:00:00.123456')
  assert.equal(payload.description, 'old local draft')
  assert.ok(!('title' in payload), 'unchanged remote title stays on the server')
})

test('disjoint server/local changes merge and HTTP409 preserves the draft without reporting success', async t => {
  const { ElMessage } = await import('element-plus')
  t.mock.method(ElMessage, 'warning', () => {})
  useUserStore().setSession('token-a', { id: 'account-a' })
  const editor = await editorFixture(), store = useWorldStore()
  await editor.loadCardData({ id: 'one' })
  editor.form.value.description = 'local body'
  store.fetchCardDetail = async () => card('one', 'a', { title: 'remote title' })
  store.updateCard = async (_id, payload) => {
    assert.ok(!('title' in payload)); assert.equal(payload.description, 'local body')
    throw { response: { status: 409, data: { code: 'WORLD_CARD_CONFLICT' } } }
  }
  await editor.handleSave(() => assert.fail())
  assert.equal(editor.isDirty.value, true)
  await editor.loadCardData({ id: 'one' })
  assert.equal(editor.draft.pending.value.form.description, 'local body')
})


test('world card saves leave error presentation to the editor instead of the global debug panel', async t => {
  const { default: request } = await server.ssrLoadModule('/src/utils/request.ts')
  let captured
  t.mock.method(request, 'put', async (...args) => { captured = args; return card('one') })
  await worldApi.updateCard('a', 'one', { title: '正文', expectedUpdatedAt: date })
  assert.equal(captured[0], '/world/projects/a/cards/one')
  assert.equal(captured[1].expectedUpdatedAt, date)
  assert.equal(captured[2].silentError, true)
})


test('graph references use one shared request, invalidate after reset, and do not load card details', async t => {
  let calls=0
  t.mock.method(worldApi,'getReferences',async()=>{calls++;return {one:['two']}})
  t.mock.method(worldApi,'getCard',()=>assert.fail('no per-card request for graph'))
  const store=useWorldStore()
  const [a,b]=await Promise.all([store.loadReferences('a'),store.loadReferences('a')])
  assert.deepEqual(a,{one:['two']});assert.deepEqual(a,b);assert.equal(calls,1)
  await store.loadReferences('a');assert.equal(calls,1)
  store.reset();await store.loadReferences('a');assert.equal(calls,2)
})
test('flat layout keeps every node with finite distinct positions and is deterministic',()=>{
  const nodes=Array.from({length:1000},(_,i)=>({id:String(i),title:String(i),type:'lore',color:'#000'}))
  const links=nodes.slice(1).map(n=>({source:n.id,target:'0'}))
  const result=graphLayout.layoutWorldGraph(nodes,links)
  assert.equal(result.length,1000);assert.equal(new Set(result.map(n=>`${n.x},${n.y}`)).size,1000)
  assert.ok(result.every(n=>Number.isFinite(n.x+n.y)))
  assert.deepEqual(result,graphLayout.layoutWorldGraph(nodes,links));assert.equal(nodes[0].x,undefined)
})
test('text import preserves plain files, splits sections, ignores code fences and rejects empty/oversized titles',()=>{
  assert.deepEqual(transfer.parseWorldText('正文','世界.txt'),[{title:'世界',description:'正文'}])
  const result=transfer.parseWorldText('# 雾海\n简介\n\n## 港口\n正文\n```md\n## 代码标题\n```\n## 人物\n人设','world.md')
  assert.equal(result.length,3);assert.equal(result[1].title,'港口');assert.match(result[1].description,/## 代码标题/)
  assert.throws(()=>transfer.parseWorldText('  ','empty.txt'));assert.throws(()=>transfer.parseWorldText('## '+ '字'.repeat(201),'bad.md'))
})
test('Markdown export includes readable references, attributes, timeline, tags and images',()=>{
  const content=transfer.worldMarkdown(project('a'),[card('one','a',{title:'旅人',description:'来自 {CARD:two}',attributes:[{key:'身份',value:'向导'},{key:'年龄',value:20},{key:'公开',value:false}],tags:['雾海'],galleryImages:['https://example.com/image.png'],timelineEvents:[{date:'100年',title:'远航',description:'出发'}]}),card('two','a',{title:'港口'})])
  assert.match(content,/\[港口\]\(#card-two\)/);assert.match(content,/身份：向导/);assert.match(content,/年龄：20/);assert.match(content,/公开：false/);assert.match(content,/100年/);assert.match(content,/标签：雾海/);assert.match(content,/image.png/)
})

test('import remaps exported anchors, forward links and Chinese headings without touching examples', () => {
  const items = transfer.parseWorldText('## 旅人\n<a id="card-old-a"></a>\n来自 [港口](#card-old-b) 与 [港口](#%E6%B8%AF%E5%8F%A3)\n`[示例](#bad)`\n```md\n[示例](#bad)\n```\n## 港口\n<a id="card-old-b"></a>\n迎接 [旅人](#card-old-a)', '世界.md')
  const result = transfer.resolveWorldImport(items, ['new-a', 'new-b'])
  assert.equal(result.unresolved, 0)
  assert.match(result.descriptions[0], /来自 \{CARD:new-b\} 与 \{CARD:new-b\}/)
  assert.match(result.descriptions[1], /\{CARD:new-a\}/)
  assert.match(result.descriptions[0], /`\[示例\]\(#bad\)`/)
  assert.doesNotMatch(result.descriptions[0], /<a id/)
})

test('ambiguous or missing import references remain readable, never link to an arbitrary card', () => {
  const items = transfer.parseWorldText('## 港口\n甲\n## 港口\n乙\n## 人物\n[港口](#港口) [失效](#missing) [外部](https://example.com)', '重复.md')
  const result = transfer.resolveWorldImport(items, ['a', 'b', 'c'])
  assert.equal(result.unresolved, 2)
  assert.doesNotMatch(result.descriptions[2], /\{CARD:/)
  assert.match(result.descriptions[2], /港口（未匹配引用/)
  assert.match(result.descriptions[2], /https:\/\/example.com/)
})

test('export/import keeps images usable and nested headings do not create spurious entries', () => {
  const md = transfer.worldMarkdown({ ...project('a'), description: '' }, [card('one','a',{
    title:'旅人 [甲]', description:'## 人物经历\n中文正文',
    coverImage:'["https://example.com/cover.png"]', galleryImages:['https://example.com/tall.png', 'javascript:bad'],
  })])
  assert.match(md,/!\[旅人 \\\[甲\\\] · 封面\]/)
  assert.match(md,/### 人物经历/)
  assert.doesNotMatch(md.split('<!-- taichu-world:')[0],/javascript:bad/)
  const items=transfer.parseWorldText(md,'世界.md')
  assert.equal(items.length,1)
  assert.equal(items[0].title,'旅人 [甲]')
  assert.deepEqual(items[0].images,['https://example.com/tall.png','javascript:bad'])
  assert.equal(items[0].payload.coverImage,'["https://example.com/cover.png"]')
  assert.deepEqual(items[0].anchors,['card-one'])
})

test('structured MD roundtrip preserves every business field and remaps all reference forms, even duplicate titles', () => {
  const original = [card('one', 'a', { title:'重名', subType:'子类型', aliases:['别名'],
    description:'## 原始标题\n来自 {CARD:two}。\n```md\n## 示例\n```\n保留空格  ',
    content:'{"enabled":false,"age":0,"text":"<!-- 注释 -->"}',
    coverImage:'["/cover.png"]',galleryImages:['/image.png','https://example.test/tall.png'],
    attributes:[{key:'年龄',value:'0',type:'number'}],tags:['甲','乙'],embeddedCards:['two'],
    contentBlocks:[{...block('two'),contextLabel:'出身'}],
    timelineEvents:[{date:'100年',era:'旧纪元',title:'启程',description:'细节'}],
    outRelations:[{targetCardId:'two',relationType:'居住于'}],
  }), card('two','a',{title:'重名',description:'认识 {CARD:one}'})]
  const md=transfer.worldMarkdown(project('a'),original)
  const items=transfer.parseWorldText('\uFEFF'+md.replace(/\n/g,'\r\n'),'世界.md')
  assert.equal(items.length,2,'world intro must not become an extra card')
  const resolved=transfer.resolveWorldImport(items,['new-one','new-two'])
  assert.equal(resolved.unresolved,0)
  const expected={...items[0].payload,description:original[0].description.replace('CARD:two','CARD:new-two'),
    contentBlocks:[{...block('new-two'),id:'block-two',contextLabel:'出身'}],embeddedCards:['new-two'],
    relations:[{targetCardId:'new-two',relationType:'居住于'}]}
  assert.deepEqual(resolved.payloads[0],expected)
  assert.equal(resolved.payloads[1].description,'认识 {CARD:new-one}')
  for(const key of ['title','type','subType','aliases','content','coverImage','galleryImages','attributes','tags','timelineEvents'])
    assert.deepEqual(resolved.payloads[0][key],original[0][key],key)
  const second=resolved.payloads.map((p,i)=>card(['new-one','new-two'][i],'b',{...p,outRelations:p.relations}))
  const again=transfer.resolveWorldImport(transfer.parseWorldText(transfer.worldMarkdown(project('b'),second),'再次.md'),['third-one','third-two'])
  assert.equal(again.payloads[0].description,original[0].description.replace('CARD:two','CARD:third-two'))
  assert.equal(again.payloads[0].galleryImages.length,2)
  assert.doesNotMatch(transfer.worldMarkdown(project('a'),original,false),/taichu-world:v1/,'print must not contain restore metadata')
})

test('structured import creates neutral references first and never imports ownership or permissions', () => {
  const md=transfer.worldMarkdown(project('a'),[card('one','a',{description:'来自 {CARD:two}',embeddedCards:['two']}),card('two')])
  const edited=md.replace('"payload":{','"payload":{"ownerId":"attacker","projectId":"other","isPublic":true,"expectedUpdatedAt":"old",')
  const items=transfer.parseWorldText(edited,'世界.md')
  const payload=transfer.createWorldImportPayload(items[0],'lore')
  assert.equal(payload.type,'character')
  assert.equal(payload.description,'来自 （引用待恢复）')
  for(const key of ['embeddedCards','relations','contentBlocks']) assert.deepEqual(payload[key],[])
  for(const key of ['ownerId','projectId','isPublic','expectedUpdatedAt']) assert.ok(!(key in payload))
  assert.deepEqual(transfer.createWorldImportPayload({title:'普通',description:'正文',images:['/a.png']},'lore'),
    {title:'普通',description:'正文',galleryImages:['/a.png'],type:'lore'})
})

test('edited, truncated, duplicate-ID and incomplete restore files fail before import; plain fallback is explicit', () => {
  const md=transfer.worldMarkdown(project('a'),[card('one','a',{description:'正文 {CARD:two}'}),card('two')])
  assert.throws(()=>transfer.parseWorldText(md.replace('正文','已修改正文'),'修改.md'),/可读内容已修改/)
  assert.throws(()=>transfer.parseWorldText(md.slice(0,-5),'截断.md'))
  assert.throws(()=>transfer.parseWorldText(md.replace('"sourceId":"two"','"sourceId":"one"'),'重复.md'),/重复/)
  assert.throws(()=>transfer.parseWorldText(md.replace('CARD:two','CARD:missing'),'缺失.md'),/未包含/)
  const plain=transfer.parseWorldText(md.replace('正文','已修改正文'),'修改.md',true)
  assert.ok(plain.some(p=>p.description.includes('已修改正文')))
  assert.ok(plain.every(p=>!p.payload&&!p.description.includes('taichu-world:v1')))
  assert.throws(()=>transfer.resolveWorldImport(transfer.parseWorldText(md,'世界.md'),['same','same']))
})

test('editing imported entries preserves whitespace and leaves untouched legacy fields on the server', async t => {
  const { ElMessage } = await import('element-plus')
  t.mock.method(ElMessage,'success',()=>{})
  const editor=await editorFixture(),store=useWorldStore()
  store.fetchCardDetail=async()=>card('one','a',{content:'[1,2,3]',description:'  正文  \n',
    attributes:[{key:'旧属性',type:'legacy',value:'值'}],subType:'自定义',aliases:['别名'],embeddedCards:['two']})
  await editor.loadCardData({id:'one'})
  editor.form.value.description='  修改正文  \n\n'
  let payload
  store.updateCard=async(_id,p)=>{payload=p}
  await editor.handleSave(()=>{})
  assert.deepEqual(payload,{description:'  修改正文  \n\n',expectedUpdatedAt:date})
})

test('schema-defined references in type data remap while unrelated JSON fields stay intact',()=>{
  const md=transfer.worldMarkdown(project('a'),[card('one','a',{type:'event',content:'{"locationId":"two","memo":"two","count":0}'}),card('two','a',{type:'location'})])
  const items=transfer.parseWorldText(md,'世界.md')
  assert.equal(JSON.parse(transfer.createWorldImportPayload(items[0],'lore').content).locationId,'')
  assert.deepEqual(JSON.parse(transfer.resolveWorldImport(items,['new-one','new-two']).payloads[0].content),
    {locationId:'new-two',memo:'two',count:0})
  assert.throws(()=>transfer.parseWorldText(md.replace('\\"locationId\\":\\"two\\"','\\"locationId\\":\\"missing\\"'),'broken.md'),/未包含/)
})

test('embedded reference previews show entry names instead of internal IDs',async()=>{
  await useWorldStore().fetchCards('a')
  const html=await render('EmbeddedCard',{projectId:'a',card:card('one','a',{description:'认识 {CARD:two} 与 {CARD:missing}'})})
  assert.match(html,/认识 「two」 与 「不可用的条目」/)
  assert.doesNotMatch(html,/\{CARD:/)
})

test('camera fits actual moved coordinates around their bounds and focuses a single node', () => {
  const points=[{x:-12000,y:8000},{x:5000,y:3000},{x:400,y:15000}]
  for (const [width,height] of [[900,500],[280,300]]) {
    const fit=graphLayout.fitWorldGraph(points,width,height)
    const screen=points.map(p=>({x:p.x*fit.zoom+fit.offset.x,y:p.y*fit.zoom+fit.offset.y}))
    assert.ok(screen.every(p=>p.x>=63 && p.x<=width-63 && p.y>=49 && p.y<=height-49))
    assert.ok(Math.abs((Math.min(...screen.map(p=>p.x))+Math.max(...screen.map(p=>p.x)))/2-width/2)<.001)
    assert.ok(Math.abs((Math.min(...screen.map(p=>p.y))+Math.max(...screen.map(p=>p.y)))/2-height/2)<.001)
    const single=graphLayout.fitWorldGraph([points[0]],width,height)
    assert.equal(single.zoom,1)
    assert.equal(points[0].x*single.zoom+single.offset.x,width/2)
    assert.equal(points[0].y*single.zoom+single.offset.y,height/2)
  }
  assert.equal(graphLayout.fitWorldGraph([{x:NaN,y:1}],900,500),null)
  assert.equal(graphLayout.fitWorldGraph(points,0,500),null)
})


test('graph saves serialize writes, retain local layout across project switches and preserve the latest cache', async () => {
  const store = useWorldStore(), first = deferred(), calls = []
  worldApi.saveGraphPositions = async (id, positions, expected) => { calls.push([id, positions, expected]); if (calls.length === 1) await first.promise }
  await store.fetchCards('a')
  const p1 = store.saveGraphPositions('a', '{"one":{"x":10,"y":20}}')
  const p2 = store.saveGraphPositions('a', '{"one":{"x":30,"y":40}}')
  await settle()
  assert.equal(calls.length, 1)
  assert.equal(store.graphSaves.get('a').status, 'saving')
  assert.equal(JSON.parse(store.currentProject.graphPositions).one.x, 30)
  await store.fetchCards('b')
  first.resolve(); await Promise.all([p1, p2])
  assert.deepEqual(calls.map(c => c[0]), ['a', 'a'])
  assert.deepEqual(calls.map(c => c[2]), ['{}', calls[0][1]])
  assert.equal(store.currentProject.id, 'b')
  await store.fetchCards('a')
  assert.equal(JSON.parse(store.currentProject.graphPositions).one.x, 30)
  assert.equal(store.graphSaves.get('a').status, 'saved')
})

test('graph save failures remain retryable and cannot overwrite the next session', async () => {
  const store = useWorldStore()
  await store.fetchCards('a')
  worldApi.saveGraphPositions = async () => { throw new Error('offline') }
  await store.saveGraphPositions('a', '{}')
  assert.equal(store.graphSaves.get('a').status, 'error')
  worldApi.saveGraphPositions = async () => {}
  await store.saveGraphPositions('a', '{}')
  assert.equal(store.graphSaves.get('a').status, 'saved')
  const pending = deferred(), calls = []
  worldApi.saveGraphPositions = async id => { calls.push(id); await pending.promise }
  const one = store.saveGraphPositions('a', '{"x":1}')
  const two = store.saveGraphPositions('a', '{"x":2}')
  await settle(); store.reset(); pending.resolve(); await Promise.all([one, two])
  assert.deepEqual(calls, ['a'])
  assert.equal(store.graphSaves.size, 0)
  assert.equal(store.currentProject, null)
})

test('reloading project data while graph save is pending preserves local positions; discard restores last confirmed write', async () => {
  const store = useWorldStore(), pending = deferred()
  worldApi.saveGraphPositions = () => pending.promise
  await store.fetchCards('a')
  const task = store.saveGraphPositions('a', '{"one":{"x":5,"y":6}}')
  await store.fetchCards('a', true)
  assert.equal(JSON.parse(store.currentProject.graphPositions).one.x, 5)
  pending.reject(new Error('offline')); await task
  store.discardGraphSave('a')
  assert.equal(store.currentProject.graphPositions, '{}')
  assert.equal(store.graphSaves.has('a'), false)
})

test('relation text avoids node labels, dots and other relation text', () => {
  const occupied = [{left:140,top:90,width:120,height:30}]
  const a={x:20,y:100}, b={x:380,y:100}, c={x:200,y:100}
  const first = graph.placeEdgeLabel(a,b,c,50,400,220,occupied)
  assert.ok(first)
  assert.ok(!graph.graphBoxesOverlap(first.box, occupied[0]))
  const second=graph.placeEdgeLabel(a,b,c,50,400,220,occupied)
  assert.ok(second)
  assert.ok(!graph.graphBoxesOverlap(first.box, second.box))
  assert.equal(graph.placeEdgeLabel(a,b,c,400,400,220,occupied), null)
})


test('compact overview shows all 19 node labels without overlap at a desktop canvas size', () => {
  const nodes = Array.from({ length: 19 }, (_, i) => ({ id: String(i), title: '测试条目'+i }))
  const links = Array.from({ length: 14 }, (_, i) => ({ source: String(i), target: String(i+1) }))
  const placed = graphLayout.layoutWorldGraph(nodes, links)
  const camera = graphLayout.fitWorldGraph(placed, 740, 319)
  const labels = graph.placeGraphLabels(placed.map(p => ({...p, x:p.x*camera.zoom+camera.offset.x, y:p.y*camera.zoom+camera.offset.y, z:0, width:176*camera.zoom})),740,319)
  assert.equal(labels.length, 19)
  for (const [i, label] of labels.entries()) for (const other of labels.slice(i+1))
    assert.equal(graph.graphBoxesOverlap({...label,height:30},{...other,height:30}),false)
})

test('legacy malformed graph coordinates cannot crash rendering', () => {
  for (const json of ['null','[]','broken','1']) assert.deepEqual(graphLayout.parseGraphPositions(json),{})
  assert.deepEqual(graphLayout.parseGraphPositions('{"a":null,"b":{"x":"1","y":2},"c":{"x":2,"y":3}}'),{c:{x:2,y:3}})
})


test('dragged graph positions survive stale save echoes; overview fit ignores drag selection and focus fit targets the node', async t => {
  const originals = Object.fromEntries(['window','ResizeObserver','MutationObserver','requestAnimationFrame','cancelAnimationFrame'].map(k => [k,globalThis[k]]))
  globalThis.window = { addEventListener() {}, removeEventListener() {} }
  globalThis.ResizeObserver = class { constructor(cb) { this.cb=cb } observe() { this.cb() } disconnect() {} }
  globalThis.MutationObserver = class { observe() {} disconnect() {} }
  globalThis.requestAnimationFrame = () => 1
  globalThis.cancelAnimationFrame = () => {}
  const oldDocument = globalThis.document
  globalThis.document = { documentElement: {} }
  const component = (await server.ssrLoadModule(componentPath+'FlatWorldGraph.vue')).default
  const props = reactive({data:{nodes:[{id:'a',title:'A'},{id:'b',title:'B'},{id:'c',title:'C'}],links:[]},positions:{a:{x:0,y:0},b:{x:190,y:0}},focusId:''})
  const emitted=[]
  let state
  const renderer=createRenderer({createComment:()=>({}),insert(){},remove(){},parentNode(){},nextSibling(){}})
  const app=renderer.createApp({setup(){
    state=component.setup(props,{expose(){},emit:(...args)=>emitted.push(args)})
    state.container.value={clientWidth:900,clientHeight:500}
    return ()=>null
  }})
  app.provide(Symbol.for('v-scx'), { modules: new Set() })
  app.mount({})
  t.after(()=>{app.unmount();Object.assign(globalThis,originals);globalThis.document=oldDocument})
  state.resetCamera('all')
  state.labelDown({clientX:100,clientY:100,preventDefault(){},stopPropagation(){}},{id:'a'})
  state.onWindowMove({clientX:150,clientY:120})
  state.onWindowUp()
  const saved=emitted.find(e=>e[0]==='update:positions')[1]
  assert.equal(Object.keys(saved).length,3)
  assert.deepEqual(saved.a,{x:50,y:20})
  props.positions={a:{x:0,y:0},b:{x:190,y:0}}
  await settle()
  assert.deepEqual(state.pinned.value.get('a'),saved.a)
  state.resetCamera('all')
  assert.equal(state.selected.value.size,0)
  assert.deepEqual(state.offset.value,graphLayout.fitWorldGraph(state.worldNodes.value,900,500).offset)
  props.focusId='a';await settle()
  const focused=state.screen.value.find(n=>n.id==='a')
  assert.equal(focused.x,450);assert.equal(focused.y,250)
  state.labelDown({clientX:100,clientY:100,preventDefault(){},stopPropagation(){}},{id:'a'})
  state.onWindowMove({clientX:180,clientY:160})
  state.onWindowCancel()
  assert.deepEqual(state.pinned.value.get('a'),saved.a)
  assert.equal(emitted.filter(e=>e[0]==='update:positions').length,1)
  props.focusId=''; props.data.nodes[0].title='非常长的中文世界观人物名称'; props.data.links=[{source:'a',target:'b'}]
  state.hovered.value='a'; await settle()
  assert.ok(state.neighbors.value.has('b'))
  assert.ok(state.labels.value.find(label=>label.id==='a').width > 176)
  state.zoom.value=.1
  assert.ok(state.labels.value.some(label=>label.id==='a'), 'overview still retains the active title at tiny zoom')
})

test('two independent graph stores reject stale writes, stop queued retries, and resume only after loading current positions', async () => {
  const first = useWorldStore(), second = useWorldStore(createPinia())
  let saved = '{}', writes = 0
  worldApi.getProject = async id => ({ ...project(id), graphPositions: saved })
  worldApi.saveGraphPositions = async (id, positions, expected) => {
    writes++
    if (expected !== saved) throw { response: { status: 409, data: { positions: saved } } }
    saved = positions
  }
  await first.fetchCards('a'); await second.fetchCards('a')
  const one = '{"one":{"x":10,"y":20}}', two = '{"one":{"x":30,"y":40}}'
  await first.saveGraphPositions('a', one)
  await Promise.all([second.saveGraphPositions('a', two), second.saveGraphPositions('a', '{}')])
  assert.equal(saved, one)
  assert.equal(writes, 2)
  assert.equal(second.graphSaves.get('a').status, 'conflict')
  assert.equal(second.graphSaves.get('a').saved, one)
  await second.saveGraphPositions('a', two)
  assert.equal(writes, 2)
  assert.equal(second.currentProject.graphPositions, two)
  const secondPinia = createPinia(), reopened = useWorldStore(secondPinia)
  await reopened.fetchCards('a')
  assert.equal(reopened.currentProject.graphPositions, one)
  await second.reloadGraphPositions('a')
  assert.equal(second.currentProject.graphPositions, one)
  assert.equal(second.graphSaves.has('a'), false)
  await second.saveGraphPositions('a', two)
  assert.equal(saved, two)
  await first.saveGraphPositions('a', '{}')
  assert.equal(first.graphSaves.get('a').status, 'conflict')
  assert.equal(saved, two)
})

test('failed conflict reload retains local changes and account reset ignores late reloads', async () => {
  const store = useWorldStore()
  await store.fetchCards('a')
  worldApi.saveGraphPositions = async () => { throw { response: { status: 409, data: { positions: '{"remote":{}}' } } } }
  await store.saveGraphPositions('a', '{"local":{}}')
  worldApi.getProject = async () => { throw new Error('offline') }
  await assert.rejects(store.reloadGraphPositions('a'), /offline/)
  assert.equal(store.graphSaves.get('a').status, 'conflict')
  assert.equal(store.currentProject.graphPositions, '{"local":{}}')
  const pending = deferred()
  worldApi.getProject = () => pending.promise
  const reload = store.reloadGraphPositions('a')
  await settle(); store.reset(); pending.resolve(project('a'))
  assert.equal(await reload, false)
  assert.equal(store.currentProject, null)
})

test('refresh after a successful graph save updates the base for the next conditional write', async () => {
  const store = useWorldStore(), bases = []
  worldApi.saveGraphPositions = async (_id, _positions, expected) => { bases.push(expected) }
  await store.fetchCards('a'); await store.saveGraphPositions('a', '{"first":{}}')
  worldApi.getProject = async id => ({...project(id), graphPositions:'{"remote":{}}'})
  await store.fetchCards('a', true)
  await store.saveGraphPositions('a', '{"next":{}}')
  assert.deepEqual(bases, ['{}','{"remote":{}}'])
})
