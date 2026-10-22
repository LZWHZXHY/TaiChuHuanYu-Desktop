import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { test } from 'node:test'
import { parse, compileScript, compileTemplate } from '@vue/compiler-sfc'
import ts from 'typescript'
import * as vue from 'vue'

const require = createRequire(import.meta.url)
const helper = {}; new Function('exports', ts.transpileModule(readFileSync(new URL('../src/utils/wikiContent.ts', import.meta.url), 'utf8'), { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText)(helper)
const settle = async () => { await vue.nextTick(); await new Promise(resolve => setImmediate(resolve)) }
const deferred = () => { let resolve, reject; const promise = new Promise((a, b) => { resolve = a; reject = b }); return { promise, resolve, reject } }
function mount(t, file, props, mocks) {
  const source = readFileSync(new URL('../src/' + file, import.meta.url), 'utf8')
  const { content } = compileScript(parse(source).descriptor, { id: file })
  const js = ts.transpileModule(content, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText
  const exports = {}; new Function('require', 'exports', js)(id => id === '@/utils/wikiContent' ? helper : id in mocks ? mocks[id] : id.endsWith('.vue') ? {} : require(id), exports)
  let state; const events = []
  const renderer = vue.createRenderer({ createComment: () => ({}), insert() {}, remove() {}, parentNode() {}, nextSibling() {} })
  const app = renderer.createApp({ setup() { state = exports.default.setup(props, { expose() {}, emit: (...args) => events.push(args) }); return () => null } })
  app.mount({}); let mounted = true
  const unmount = () => { if (mounted) { mounted = false; app.unmount() } }; t.after(unmount)
  return { state, events, unmount }
}
function publication(t, api = {}, artwork) {
  const user = vue.reactive({ token: 'one', isLoggedIn: true }), props = vue.reactive({ modelValue: true, artwork })
  const urls = [], revoked = []
  t.mock.method(URL, 'createObjectURL', () => { const value = 'blob:' + urls.length; urls.push(value); return value })
  t.mock.method(URL, 'revokeObjectURL', url => revoked.push(url))
  const calls = [], defaults = { publish: async data => { calls.push(data); return { artworkId: 3, success: true } } }
  const mounted = mount(t, 'views/画廊展厅/ArtworkPublishModal.vue', props, {
    '../../api/artwork': { artworkApi: { ...defaults, ...api } }, '../../stores/user': { useUserStore: () => user }
  })
  return { ...mounted, user, props, calls, urls, revoked }
}
const file = () => new File(['image'], 'test.png', { type: 'image/png' })

test('independent gallery publication keeps tags separate and requires no note or project', async t => {
  const c = publication(t), s = c.state
  s.processFiles([file()]); s.form.title = '独立风景'; s.form.tags = '风景，练习'
  await s.handleSubmit()
  assert.deepEqual(c.calls[0].getAll('tags'), ['风景', '练习'])
  assert.equal(c.calls[0].has('originalNoteId'), false)
  assert.equal(c.calls[0].has('projectId'), false)
})

test('publication sends contiguous captions including blanks and the selected cover', async t => {
  const c = publication(t), s = c.state
  s.processFiles([file(), file()]); s.form.title = '  标题  '; s.imageList.value[1].caption = '  第二张  '; s.setCover(1)
  await s.handleSubmit()
  const body = c.calls[0]
  assert.equal(body.get('title'), '标题'); assert.equal(body.get('coverIndex'), '1')
  assert.equal(body.get('captions[0]'), ''); assert.equal(body.get('captions[1]'), '第二张')
  assert.equal(body.getAll('images').length, 2); assert.equal(c.events[0][1].artworkId, 3)
})

test('publication blocks duplicate submissions and closing; failure retains input and allows retry', async t => {
  const pending = deferred(); let calls = 0
  const c = publication(t, { publish: () => { calls++; return pending.promise } }), s = c.state
  s.form.title = 'draft'; s.processFiles([file()])
  const first = s.handleSubmit(); await s.handleSubmit(); s.close(); s.removeImage(0)
  assert.equal(calls, 1); assert.equal(c.events.length, 0); assert.equal(s.imageList.value.length, 1)
  pending.reject(new Error('network')); await first
  assert.equal(s.form.title, 'draft'); assert.equal(s.submitting.value, false); assert.match(s.error.value, /输入内容已保留/)
})

test('editing sends metadata only and preserves existing images', async t => {
  const calls = [], c = publication(t, { edit: async (...args) => { calls.push(args); return { artworkId: 7 } } },
    { id: 7, title: '旧标题', description: '说明', images: [{ url: 'https://media.test/one', caption: '旧', isCover: true }, { url: 'https://media.test/two', isCover: false }] })
  const s = c.state; s.form.title = '新标题'; s.imageList.value[0].caption = ''; s.setCover(1)
  s.removeImage(0); s.processFiles([file()]); await s.handleSubmit()
  assert.deepEqual(calls, [[7, { tags: [], title: '新标题', description: '说明', captions: ['', ''], coverIndex: 1 }]])
  assert.equal(c.urls.length, 0); assert.equal(c.calls.length, 0)
})

test('account changes discard stale publication responses and revoke preview URLs', async t => {
  const pending = deferred(), c = publication(t, { publish: () => pending.promise }), s = c.state
  s.form.title = 'draft'; s.processFiles([file()]); const task = s.handleSubmit()
  c.user.token = 'two'; pending.resolve({ artworkId: 3 }); await task
  assert.equal(s.form.title, ''); assert.deepEqual(c.revoked, ['blob:0']); assert.ok(!c.events.some(e => e[0] === 'published'))
  c.unmount(); assert.equal(c.revoked.length, 1)
})

test('upload validation and deleting a cover maintain a single valid cover', async t => {
  const c = publication(t), s = c.state
  s.processFiles([new File([], 'empty.png', { type: 'image/png' })]); assert.equal(s.imageList.value.length, 0)
  s.processFiles([file(), file()]); s.removeImage(0); assert.equal(s.imageList.value[0].isCover, true)
  c.user.isLoggedIn = false; s.form.title = 'title'; await s.handleSubmit(); assert.equal(c.calls.length, 0); assert.match(s.error.value, /登录/)
  c.unmount(); assert.equal(c.revoked.length, 2)
})

function notePublisher(t, overrides = {}) {
  const props = vue.reactive({ modelValue: true, noteId: 'one', noteType: 'art', spaceName: '', beforePublish: async () => {}, ...overrides.props })
  const calls = [], api = { publishNote: async (...args) => { calls.push(args); return { success: true } }, ...overrides.api }
  return { props, calls, ...mount(t, 'views/太初灵脉/components/PublishModal.vue', props, {
    '../../../api/lingmai': { lingmaiApi: api }, '@/api/Wiki': { wikiApi: { getCategories: async () => [] } },
    '@/api/projectService': { default: { getMyProjects: async () => [] } }
  }) }
}

test('note publication awaits current-editor save and blocks duplicates', async t => {
  const pending = deferred(), c = notePublisher(t, { props: { beforePublish: () => pending.promise } })
  const run = c.state.handleConfirm(); await c.state.handleConfirm(); c.state.close()
  assert.equal(c.calls.length, 0); assert.equal(c.events.length, 0)
  pending.resolve(); await run
  assert.deepEqual(c.calls, [['one', { type: 'art' }]])
  assert.equal(c.events[0][1].noteId, 'one')
})

test('save failure blocks publication and old responses cannot update another note', async t => {
  const c = notePublisher(t, { props: { beforePublish: async () => { throw new Error('上传尚未完成') } } })
  await c.state.handleConfirm(); assert.equal(c.calls.length, 0); assert.match(c.state.error.value, /上传尚未完成/)
  const pending = deferred(); c.props.beforePublish = () => pending.promise
  const run = c.state.handleConfirm(); c.props.noteId = 'two'; await settle(); pending.resolve(); await run
  assert.equal(c.calls.length, 0); assert.equal(c.events.length, 0)
})

test('gallery reads artworkId from publication response and refreshes the grid', t => {
  const pushed = [], original = globalThis.window; globalThis.window = { scrollTo() {} }; t.after(() => { globalThis.window = original })
  const c = mount(t, 'views/画廊展厅/index.vue', {}, {
    'vue-router': { useRoute: () => ({ query: {} }), useRouter: () => ({ push: value => pushed.push(value) }) },
    '../../stores/user': { useUserStore: () => ({ isLoggedIn: true }) },
    ...Object.fromEntries(['ArtworkRanking', 'ArtworkInfiniteGrid', 'ArtworkDetail', 'ArtworkPublishModal'].map(name => ['./' + name + '.vue', { default: {} }]))
  })
  c.state.onArtworkPublished({ artworkId: 42, success: true })
  assert.equal(c.state.gridKey.value, 1); assert.equal(pushed[0].query.workId, '42')
})

test('changed publication templates compile with their script bindings', () => {
  for (const file of ['画廊展厅/ArtworkPublishModal.vue', '画廊展厅/ArtworkDetail.vue', '太初灵脉/components/PublishModal.vue', '太初灵脉/components/NoteSettingsPanel.vue', '管理面板/管理组件/作品管理组件/画廊治理.vue']) {
    const filename = '../src/views/' + file, source = readFileSync(new URL(filename, import.meta.url), 'utf8')
    const { descriptor, errors } = parse(source); assert.deepEqual(errors, [])
    const script = compileScript(descriptor, { id: file })
    const template = compileTemplate({ id: file, filename, source: descriptor.template.content, compilerOptions: { bindingMetadata: script.bindings } })
    assert.deepEqual(template.errors, [])
  }
})

const managedWork = () => ({ id: 'GAL-000001', title: '作品', authorId: 'one', authorName: '作者', views: 8, likes: 3, favorites: 2, isFeatured: false, status: 'published' })
function governance(t, overrides = {}) {
  const calls = [], work = managedWork()
  const api = {
    getGalleryWorks: async () => ({ items: [work], totalCount: 1 }),
    updateGalleryGovernance: async (...args) => { calls.push(args) },
    ...overrides
  }
  return { calls, work, ...mount(t, 'views/管理面板/管理组件/作品管理组件/画廊治理.vue', {}, { '@/api/Admin/AdminProduct': { adminProductApi: api } }) }
}

test('admin edits send no artificial likes and reload authoritative stats', async t => {
  let loads = 0
  const c = governance(t, { getGalleryWorks: async () => ({ items: [{ ...managedWork(), likes: ++loads }], totalCount: 1 }) })
  await settle(); c.state.handleOpenGovernance(c.work); c.state.editForm.value.views = 12
  await c.state.submitGovernance()
  assert.deepEqual(c.calls, [['GAL-000001', { views: 12, isFeatured: false, status: 'published' }]])
  assert.equal(c.state.galleryList.value[0].likes, 2); assert.equal(c.state.showGovModal.value, false)
  const template = readFileSync(new URL('../src/views/管理面板/管理组件/作品管理组件/画廊治理.vue', import.meta.url), 'utf8')
  assert.doesNotMatch(template, /喜爱值|editForm\.likes/); assert.match(template, /<output>.*targetWork\?\.likes/)
})

test('admin save blocks repeated actions and closing; failure preserves draft', async t => {
  const pending = deferred(); let requests = 0
  const c = governance(t, { updateGalleryGovernance: () => { requests++; return pending.promise } }), s = c.state
  await settle(); s.handleOpenGovernance(c.work); s.editForm.value.views = 21
  const first = s.submitGovernance(); await s.submitGovernance(); s.closeModal()
  assert.equal(requests, 1); assert.equal(s.showGovModal.value, true)
  pending.reject(new Error('network')); await first
  assert.equal(s.editForm.value.views, 21); assert.equal(s.submitting.value, false); assert.match(s.error.value, /修改内容已保留/)
  s.editForm.value.views = -1; await s.submitGovernance(); assert.equal(requests, 1)
})

test('admin ignores stale list responses after search changes', async t => {
  const old = deferred(); let requests = 0
  const c = governance(t, { getGalleryWorks: () => ++requests === 1 ? old.promise : Promise.resolve({ items: [], totalCount: 0 }) })
  c.state.searchQuery.value = '新搜索'; await c.state.handleSearch()
  old.resolve({ items: [c.work], totalCount: 1 }); await settle()
  assert.equal(c.state.galleryList.value.length, 0); assert.equal(c.state.totalCount.value, 0); assert.equal(c.state.loading.value, false)
})

test('admin status actions share payload and deletion returns from an empty last page', async t => {
  const original = globalThis.confirm; globalThis.confirm = () => true; t.after(() => { globalThis.confirm = original })
  let deleted
  const c = governance(t, { deleteGalleryWork: async id => { deleted = id } }), s = c.state
  await settle(); await s.handleStatus(c.work, 'rejected'); await s.handleStatus(c.work, 'published')
  assert.deepEqual(c.calls.map(c => c[1]), [
    { views: 8, isFeatured: false, status: 'rejected' }, { views: 8, isFeatured: false, status: 'published' }
  ])
  s.currentPage.value = 2; await s.handleDelete(c.work)
  assert.equal(deleted, c.work.id); assert.equal(s.currentPage.value, 1); assert.equal(s.submitting.value, false)
})
