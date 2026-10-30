import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { test } from 'node:test'
import { parse, compileScript } from '@vue/compiler-sfc'
import ts from 'typescript'
import * as vue from 'vue'
import { createPinia, setActivePinia } from 'pinia'

const require = createRequire(import.meta.url)
function load(path, mocks = {}) {
  const source = readFileSync(new URL(`../src/${path}`, import.meta.url), 'utf8')
  const code = path.endsWith('.vue') ? compileScript(parse(source).descriptor, { id: path }).content : source
  const js = ts.transpileModule(code, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText
  const exports = {}
  new Function('require', 'exports', js)(id => id in mocks ? mocks[id] : id.endsWith('/imageThumb') ? load('utils/imageThumb.ts') : require(id), exports)
  return exports
}
const settle = async () => { await vue.nextTick(); await new Promise(resolve => setImmediate(resolve)) }
const deferred = () => { let resolve, reject; const promise = new Promise((a, b) => { resolve = a; reject = b }); return { promise, resolve, reject } }
const stats = (id = 1, extra = {}) => ({ id, likesCount: 3, favoritesCount: 1, commentsCount: 0, isLiked: false, isFavorited: true, ...extra })
function setup(t) {
  const pinia = createPinia(); setActivePinia(pinia)
  const user = vue.reactive({ token: 'session', isLoggedIn: true })
  const store = load('stores/artwork.ts', { './user': { useUserStore: () => user } }).useArtworkStore(pinia)
  t.after(() => store.$dispose())
  return { user, store }
}
function mount(t, path, props, mocks) {
  const component = load(path, mocks).default
  let state
  const renderer = vue.createRenderer({ createComment: () => ({}), insert() {}, remove() {}, parentNode() {}, nextSibling() {} })
  const app = renderer.createApp({ setup() { state = component.setup(props, { expose() {}, emit() {} }); return () => null } })
  let mounted = true
  const unmount = () => { if (mounted) { app.unmount(); mounted = false } }
  app.mount({}); t.after(unmount)
  return { state, unmount }
}
const shared = ({ store, user }) => ({ '../../stores/artwork': { useArtworkStore: () => store }, '../../stores/user': { useUserStore: () => user } })

test('ranking ignores stale period responses and can retry a failed period', async t => {
  const first = deferred(), second = deferred(); const periods = []
  const props = vue.reactive({ period: '总' })
  const { state } = mount(t, 'views/画廊展厅/ArtworkRanking.vue', props, {
    '@/api/artwork': { artworkApi: { getGallery(_offset, _limit, _search, period) {
      periods.push(period)
      return periods.length === 1 ? first.promise : periods.length === 2 ? second.promise : Promise.resolve({ data: [{ id: 3 }] })
    } } }
  })
  props.period = '日'; await settle()
  first.resolve({ data: [{ id: 1 }] }); await settle()
  assert.deepEqual(state.items.value, [])
  second.reject(new Error('network')); await settle()
  assert.match(state.error.value, /重试/)
  await state.load()
  assert.deepEqual(periods, ['all', 'day', 'day'])
  assert.equal(state.items.value[0].id, 3)
  assert.equal(state.error.value, '')
})

test('admin entry allows scoped administrators but excludes ordinary and unrelated roles', () => {
  const { canOpenAdmin } = load('utils/adminAccess.ts')
  for (const permissions of [[], ['Club_Admin'], ['unknown']]) assert.equal(canOpenAdmin(permissions), false)
  for (const permission of ['SuperAdmin', 'Trade_Manage', 'User_Audit', 'Wiki_Editor', 'System_Monitor']) assert.equal(canOpenAdmin([permission]), true)
})

test('shared artwork state rejects late reads and prevents duplicate mutations', async t => {
  const { store } = setup(t)
  store.seed([stats()]); const old = store.capture(), pending = deferred()
  const first = store.mutate(1, () => pending.promise)
  const duplicate = await store.mutate(1, () => { throw new Error('must not execute') })
  assert.equal(duplicate, null)
  store.seed([stats(1, { likesCount: 0 })], old)
  assert.equal(store.stats[1].likesCount, 3)
  pending.resolve({ stats: stats(1, { likesCount: 4, isLiked: true }) }); await first
  store.seed([stats()], old)
  assert.equal(store.stats[1].likesCount, 4)
  assert.equal(store.pending[1], false)
})

test('account change clears state and ignores old mutation responses', async t => {
  const { store, user } = setup(t); store.seed([stats()])
  const old = store.capture(), pending = deferred()
  const call = store.mutate(1, () => pending.promise)
  user.token = ''
  user.token = 'session' // Reusing a token must not revive responses from the previous session.
  assert.deepEqual(store.stats, {})
  pending.resolve({ stats: stats() }); assert.equal(await call, null)
  store.seed([stats()], old); assert.deepEqual(store.stats, {})
})

test('failed interaction keeps server state and permits retry', async t => {
  const { store } = setup(t); store.seed([stats()])
  await assert.rejects(store.mutate(1, async () => { throw new Error('offline') }))
  assert.equal(store.stats[1].likesCount, 3); assert.equal(store.pending[1], false)
  await store.mutate(1, async () => ({ stats: stats(1, { likesCount: 4 }) }))
  assert.equal(store.stats[1].likesCount, 4)
})

test('interaction sends desired state, blocks guests, and shares updates across views', async t => {
  const env = setup(t), calls = []
  env.store.seed([stats()])
  const props = vue.reactive({ targetId: 1, targetType: 'Artwork', initialStats: stats() })
  const mocks = { '../stores/artwork': { useArtworkStore: () => env.store }, '../stores/user': { useUserStore: () => env.user },
    '../api/interact': { interactApi: { toggleAction: async (...args) => { calls.push(args); return { stats: stats(1, { likesCount: 4, isLiked: true }) } } } } }
  const a = mount(t, 'components/InteractActions.vue', props, mocks).state
  const b = mount(t, 'components/InteractActions.vue', props, mocks).state
  await a.handleAction('Like')
  assert.deepEqual(calls[0], [1, 'Artwork', 'Like', true]); assert.equal(b.status.value.likesCount, 4)
  env.user.isLoggedIn = false
  await a.handleAction('Favorite'); assert.equal(calls.length, 1); assert.match(a.error.value, /登录/)
})

function comments(t, api = {}) {
  const env = setup(t), props = vue.reactive({ artworkId: 1, initialCount: 0 })
  const calls = []
  const defaults = { getComments: async () => ({ items: [], total: 0, hasMore: false }), addComment: async (...args) => { calls.push(args); return { id: 'new', stats: stats(1, { commentsCount: 1 }) } } }
  const mounted = mount(t, 'views/画廊展厅/ArtworkComments.vue', props, { ...shared(env), '../../api/artwork': { artworkApi: { ...defaults, ...api } } })
  return { ...env, ...mounted, props, calls }
}

test('comment submit trims text, sends parent and refreshes state; empty input sends nothing', async t => {
  const { state: s, calls, store } = comments(t); await settle()
  s.draft.value = '   '; await s.submit(); assert.equal(calls.length, 0)
  s.draft.value = '  正文  '; s.replyTo.value = { id: 'parent', authorName: 'author' }
  await s.submit()
  assert.deepEqual(calls, [[1, '正文', 'parent']]); assert.equal(store.stats[1].commentsCount, 1)
  assert.equal(s.draft.value, ''); assert.equal(s.replyTo.value, null)
})

test('failed submit retains draft and reply; successful submit with failed refresh cannot repost', async t => {
  let failing = true, reads = 0
  const c = comments(t, { addComment: async () => { if (failing) throw new Error('offline'); return { id: 'x', stats: stats() } },
    getComments: async () => { if (++reads > 1) throw new Error('load failed'); return { items: [], hasMore: false } } })
  await settle(); c.state.draft.value = '正文'; c.state.replyTo.value = { id: 'p' }
  await c.state.submit(); assert.equal(c.state.draft.value, '正文'); assert.equal(c.state.replyTo.value.id, 'p')
  failing = false; await c.state.submit(); assert.equal(c.state.draft.value, ''); assert.equal(c.state.loadError.value, true)
})

test('comment switching ignores old list and pending submit responses', async t => {
  const read = deferred(), write = deferred()
  const c = comments(t, { getComments: async id => id === 1 ? read.promise : { items: [{ id: 'two' }], hasMore: false }, addComment: () => write.promise })
  c.state.draft.value = 'one'; const submission = c.state.submit()
  c.props.artworkId = 2; await settle(); c.state.draft.value = 'new draft'
  read.resolve({ items: [{ id: 'old' }], hasMore: true }); write.resolve({ id: 'saved', stats: stats() })
  await submission; await settle()
  assert.deepEqual(c.state.items.value.map(c => c.id), ['two']); assert.equal(c.state.draft.value, 'new draft')
})

test('comment delete updates totals and retries failed pagination at the same offset', async t => {
  const offsets = [], deleted = []; let fail = true
  const c = comments(t, { getComments: async (id, offset) => { offsets.push(offset); if (offset === 1 && fail) { fail = false; throw new Error('offline') } return { items: [{ id: String(offset) }], hasMore: offset === 0 } },
    deleteComment: async (...args) => { deleted.push(args); return { stats: stats(1, { commentsCount: 0 }) } } })
  await settle(); await c.state.load(); await c.state.load()
  assert.deepEqual(offsets, [0, 1, 1]); assert.equal(c.state.items.value.length, 2)
  c.state.replyTo.value = { id: '0' }; await c.state.remove('0')
  assert.deepEqual(deleted, [[1, '0']]); assert.equal(c.state.replyTo.value, null); assert.equal(c.store.stats[1].commentsCount, 0)
})

test('detail switch ignores old fetch, exposes retry, and restores scrolling', async t => {
  const env = setup(t), old = deferred(), props = vue.reactive({ id: 1 }), body = { style: { overflow: 'auto' } }
  const previous = globalThis.document; globalThis.document = { body }; t.after(() => { globalThis.document = previous })
  let fail = false
  const c = mount(t, 'views/画廊展厅/ArtworkDetail.vue', props, { ...shared(env), '@/components/SectionTrace.vue': {}, '../../api/artwork': { artworkApi: { getDetail: async id => { if (fail) throw new Error('offline'); return id === 1 ? old.promise : { ...stats(id), title: 'second', images: [] } } } },
    '../../components/InteractActions.vue': { default: {} }, './ArtworkComments.vue': { default: {} }, './ArtworkPublishModal.vue': { default: {} } })
  props.id = 2; await settle(); old.resolve({ ...stats(1), title: 'old', images: [] }); await settle()
  assert.equal(c.state.artwork.value.id, 2); assert.equal(c.state.loading.value, false)
  fail = true; await c.state.loadArtwork(); assert.match(c.state.error.value, /offline/); assert.equal(c.state.loading.value, false)
  fail = false; await c.state.loadArtwork(); assert.equal(c.state.artwork.value.id, 2)
  c.state.onImgError(); assert.equal(c.state.imageError.value, true)
  c.unmount(); assert.equal(body.style.overflow, 'auto')
})

test('comment content renders as text and all modified SFC templates compile', () => {
  const path = 'views/画廊展厅/ArtworkComments.vue'
  const source = readFileSync(new URL(`../src/${path}`, import.meta.url), 'utf8')
  assert.doesNotMatch(source, /v-html/)
  assert.match(source, /maxlength="1000"/)
  for (const file of [path, 'components/InteractActions.vue', 'views/画廊展厅/ArtworkDetail.vue', 'views/画廊展厅/ArtworkInfiniteGrid.vue']) {
    const parsed = parse(readFileSync(new URL(`../src/${file}`, import.meta.url), 'utf8'))
    assert.deepEqual(parsed.errors, [])
    assert.ok(compileScript(parsed.descriptor, { id: file }))
  }
})
