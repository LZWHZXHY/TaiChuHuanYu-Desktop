import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { test } from 'node:test'
import { parse, compileScript } from '@vue/compiler-sfc'
import ts from 'typescript'
import * as vue from 'vue'

// 执行真实 SFC setup；只替换网络、图表引擎和 DOM，不复制业务实现。
const require = createRequire(import.meta.url)
function loadScript(path, mocks = {}) {
  const source = readFileSync(new URL(`../src/${path}`, import.meta.url), 'utf8')
  const content = path.endsWith('.vue') ? compileScript(parse(source).descriptor, { id: path }).content : source
  const js = ts.transpileModule(content, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText
  const exports = {}
  new Function('require', 'exports', js)(id => id in mocks ? mocks[id] : id === '@/components/SectionTrace.vue' ? {} : require(id), exports)
  return exports
}
function mountScript(path, mocks = {}, props = {}, emit = () => {}) {
  const component = loadScript(path, mocks).default
  let state
  const exposed = {}
  const renderer = vue.createRenderer({ createComment: () => ({}), insert() {}, remove() {}, parentNode() {}, nextSibling() {} })
  const app = renderer.createApp({ setup() {
    state = component.setup(props, { expose(value) { Object.assign(exposed, value) }, emit })
    return () => null
  } })
  app.mount({})
  let mounted = true
  return { state, exposed, unmount: () => { if (mounted) { app.unmount(); mounted = false } } }
}
const settle = async () => { await vue.nextTick(); await new Promise(resolve => setImmediate(resolve)) }
const row = (payReceive, amount, extra = {}) => ({ payReceive, amount, date: '2025-01-01', ...extra })

function finance(t, request = async () => []) {
  const charts = [], frames = [], listeners = new Map(), restore = []
  t.mock.method(console, 'error', () => {})
  for (const [key, value] of Object.entries({ document: { body: { style: {} }, documentElement: {} },
    getComputedStyle: () => ({ getPropertyValue: key => ({ '--text-primary': '#e8ebed', '--paper-card': '#1c2229', '--border-line': '#37414b' }[key] || '') }),
    window: { addEventListener: (key, fn) => listeners.set(key, fn), removeEventListener: key => listeners.delete(key) },
    requestAnimationFrame: fn => frames.push(fn) })) {
    const old = Object.getOwnPropertyDescriptor(globalThis, key)
    Object.defineProperty(globalThis, key, { configurable: true, value })
    restore.push(() => old ? Object.defineProperty(globalThis, key, old) : delete globalThis[key])
  }
  const mounted = mountScript('views/推送首页/components/FinancePanel.vue', {
    '@/utils/request': { __esModule: true, default: { get: url => { assert.equal(url, '/financial/report'); return request() } } },
    '@/utils/financeCharts': { init: element => {
      const chart = { element, setOption: t.mock.fn(), resize: t.mock.fn(), dispose: t.mock.fn() }
      charts.push(chart)
      return chart
    } },
  })
  t.after(() => { mounted.unmount(); restore.forEach(fn => fn()) })
  for (const name of ['income', 'shouKuan', 'zhiChu', 'trend']) mounted.state[`${name}ChartEl`].value = { name }
  return { ...mounted, charts, listeners, flush: () => frames.splice(0).forEach(fn => fn()) }
}

test('finance preserves grouping, truncation, decimal precision before top-three merging, totals and annual order', t => {
  const { state: s } = finance(t)
  s.financeLogs.value = [row(0, 10.2, { zhiChuXiangMu: ' 123456789\n' }), row(0, 2.2, { zhiChuXiangMu: '123456789' }),
    row(0, 7, { date: new Date().toISOString() }), ...[10.4, 9.4, 8.4, 1.4, 1.4].map((amount, i) =>
      row(1, amount, { shouKuan: ` 商户${i}[说明] `, zhiChu: ' 行者 ', date: i < 2 ? '2024-06-01' : '2023-06-01' }))]
  assert.deepEqual(s.incomeChartData.value, [{ name: '12345678...', value: 12.4 }, { name: '社区共建赞助', value: 7 }])
  assert.deepEqual(s.shouKuanChartData.value, ['商户0', '商户1', '商户2', '其他开销'].map((name, i) => ({ name, value: [10.4, 9.4, 8.4, 2.8][i] })))
  assert.deepEqual(s.zhiChuChartData.value, [{ name: '行者', value: 31 }])
  assert.deepEqual(s.trendChartData.value, { years: ['2023年', '2024年', new Date().getFullYear() + '年'], income: [0, 12.4, 7], expense: [11.2, 19.8, 0] })
  assert.ok(Math.abs(s.totalBalance.value + 11.6) < 1e-10)
  assert.equal(s.monthlyIncome.value, 7)
})

test('finance mounts three pies and a trend, resizes and disposes each instance once', async t => {
  const f = finance(t, async () => [row(1, 20)])
  f.state.openModal(); await settle(); f.flush()
  assert.equal(f.charts.length, 4)
  assert.equal(f.charts[0].setOption.mock.calls[0].arguments[0].tooltip.backgroundColor, '#1c2229')
  assert.equal(f.charts[0].setOption.mock.calls[0].arguments[0].legend.textStyle.color, '#e8ebed')
  assert.deepEqual(f.charts.map(c => c.setOption.mock.calls[0].arguments[0].series[0].type), ['pie', 'pie', 'pie', 'line'])
  f.listeners.get('resize')()
  assert.ok(f.charts.every(c => c.resize.mock.callCount() === 2))
  f.state.closeModal(); f.state.closeModal()
  assert.ok(f.charts.every(c => c.dispose.mock.callCount() === 1))
  assert.equal(f.listeners.size, 0)
  assert.equal(document.body.style.overflow, '')
})

test('empty finance data retains placeholder pies without creating an empty trend', async t => {
  const f = finance(t)
  f.state.openModal(); await settle(); f.flush()
  assert.equal(f.charts.length, 3)
  assert.ok(f.charts.every(c => c.setOption.mock.calls[0].arguments[0].series[0].data[0].name === '暂无数据'))
})

test('closing and reopening ignores stale responses and queued frames; unmount disposes current charts', async t => {
  const pending = []
  const f = finance(t, () => new Promise(resolve => pending.push(resolve)))
  f.state.openModal(); await settle(); f.state.closeModal()
  f.state.openModal(); await settle()
  pending[0]([row(0, 999)]); await settle()
  assert.deepEqual(f.state.financeLogs.value, [])
  assert.equal(f.state.loading.value, true)
  pending[1]([row(0, 30)]); await settle(); f.state.closeModal(); f.flush()
  assert.equal(f.charts.length, 0)
  f.state.openModal(); await settle(); pending[2]([row(0, 40)]); await settle(); f.flush()
  f.unmount()
  assert.equal(f.state.totalBalance.value, 40)
  assert.ok(f.charts.every(c => c.dispose.mock.callCount() === 1))
  assert.equal(f.listeners.size, 0)
})

test('finance request failure releases loading and closing remains safe', async t => {
  const f = finance(t, async () => { throw new Error('offline') })
  f.state.openModal(); await settle(); f.flush()
  assert.equal(f.state.loading.value, false)
  assert.equal(f.charts.length, 0)
  f.state.closeModal()
})

test('OC detail preserves attribute order, hidden fields, ownership and gallery pagination', async t => {
  const attributes = [
    { key: '长文', type: 'long', sortOrder: 2 }, { key: '昵称', type: 'short', sortOrder: 0, value: '昵称' },
    { key: '后项', type: 'short', sortOrder: 3 }, { key: '前项', type: 'short', sortOrder: 1 },
  ]
  const store = vue.reactive({ currentCharacter: { attributes, authorId: 1,
    images: Array.from({ length: 13 }, (_, i) => ({ url: `${i}.jpg` })) }, fetchDetail: async () => {} })
  const { state: s, unmount } = mountScript('views/柴圈板块/OCs/components/detail.vue', {
    'vue-router': { useRouter: () => ({}), useRoute: () => ({ params: { id: 'oc' } }) },
    '../stickman_store': { useStickmanStore: () => store },
    '../stickman': {stickmanApi: {}}, '@/api/follow': {followApi: {}},
    '../../components/ChaiComments.vue': {default: {}}, '@/utils/imageThumb': loadScript('utils/imageThumb.ts'),
    '@/stores/user': { useUserStore: () => ({ userInfo: { id: '1' } }) }, 'element-plus': { ElImage: {} },
  })
  t.after(unmount)
  assert.deepEqual(s.shortAttributes.value.map(a => a.key), ['前项', '后项'])
  assert.deepEqual(s.longAttributes.value.map(a => a.key), ['长文'])
  assert.deepEqual(store.currentCharacter.attributes.map(a => a.key), ['长文', '昵称', '后项', '前项'])
  assert.equal(s.getAttr('昵称'), '昵称'); assert.equal(s.isOwner.value, true)
  assert.equal(s.displayImages.value.length, 12); assert.equal(s.previewList.value.length, 13)
  s.showAllImages.value = true
  assert.equal(s.displayImages.value.length, 13)
})

for (const [method, args, updateList, errorMessage] of [
  ['join', ['a', '备注'], false, '报名失败:'], ['cancelJoin', ['a'], false, '取消报名失败:'],
  ['auditParticipant', ['a', 'user', 'approved'], false, '审核参与者失败:'],
  ['kickParticipant', ['a', 'user'], false, '踢出参与者失败:'],
  ['toggleBan', ['a'], true, '操作失败:'], ['approve', ['a', 'rejected'], true, '审批失败:'],
  ['update', ['a', { title: '新标题' }], true, '更新联合失败:'],
]) test(`joint ${method} preserves arguments, cache scope, loading and rejection`, async t => {
  const { createPinia, setActivePinia } = require('pinia')
  setActivePinia(createPinia())
  let resolve, reject
  const api = { [method]: t.mock.fn(() => new Promise((ok, fail) => { resolve = ok; reject = fail })) }
  const log = t.mock.method(console, 'error', () => {})
  const store = loadScript('views/柴圈板块/Joint/joint_store.ts', { './joint': { jointApi: api } }).useJointStore()
  store.currentActivity = { id: 'a', title: '旧标题' }; store.activities = [{ id: 'a', title: '旧标题' }]
  store.myOrganized = [{ id: 'a', title: '举办列表' }]; store.myParticipated = [{ id: 'a', title: '参与列表' }]
  const promise = store[method](...args)
  assert.deepEqual(api[method].mock.calls[0].arguments, args)
  assert.equal(store.loading, method === 'update')
  const result = { id: 'a', title: '新标题' }; resolve(result)
  assert.equal(await promise, result)
  assert.equal(store.currentActivity.title, '新标题')
  assert.equal(store.activities[0].title, updateList ? '新标题' : '旧标题')
  assert.equal(store.myOrganized[0].title, '举办列表'); assert.equal(store.myParticipated[0].title, '参与列表')
  assert.equal(store.loading, false)
  store.currentActivity = { id: 'b', title: '其他详情' }; store.activities = [{ id: 'b', title: '其他列表' }]
  const other = store[method](...args); resolve(result); await other
  assert.equal(store.currentActivity.id, 'b'); assert.equal(store.activities[0].id, 'b')
  const failed = store[method](...args), error = new Error('offline'); reject(error)
  await assert.rejects(failed, e => e === error)
  assert.equal(store.loading, false); assert.deepEqual(log.mock.calls[0].arguments, [errorMessage, error])
})

function activity(t, get, props = {}, params = {}) {
  const mounted = mountScript('views/活动中心/打卡中心/ActivityDetail.vue', {
    'vue-router': { useRouter: () => ({}), useRoute: () => ({ params }) },
    '@/stores/user': { useUserStore: () => ({ userInfo: {} }) },
    '@/utils/request': { __esModule: true, default: { get } },
    './CheckInModal.vue': {}, './DiscussionBoard.vue': {}, './CompetitionPanel.vue': {},
  }, props)
  t.after(mounted.unmount)
  return mounted.state
}

test('check-in preserves completed records and orders the actual activity period', async t => {
  let records = []
  const s = activity(t, async () => [{ id: 1, name: '成员', records }])
  await s.fetchMembers(1)
  assert.equal(s.members.value[0].records.length, 30); assert.equal(records.length, 0)
  assert.deepEqual(s.members.value[0].records.map(r => r.day), Array.from({ length: 30 }, (_, i) => i + 1))
  records = [{ day: 3, isCompleted: true, isLate: true, text: '保留', image: '图.jpg' }, { day: 1, text: '已有' }]
  await s.fetchMembers(1)
  assert.equal(s.members.value[0].records[2].text, '保留'); assert.equal(s.members.value[0].records[2].isLate, true)
  assert.equal(s.activeMember.value, s.members.value[0])
  records = Array.from({ length: 30 }, (_, i) => ({ day: 30 - i }))
  await s.fetchMembers(1)
  assert.equal(s.members.value[0].records[0].day, 1)
  s.activityDays.value = 7
  await s.fetchMembers(1)
  assert.equal(s.members.value[0].records.length, 7)
  assert.equal(s.members.value[0].records[6].day, 7)
})

test('check-in distinguishes no members from failed loading without inventing members', async t => {
  t.mock.method(console, 'warn', () => {}); t.mock.method(console, 'error', () => {})
  let fail = false
  const s = activity(t, async () => { if (fail) throw Error('offline'); return [] })
  await s.fetchMembers(1)
  assert.equal(s.members.value.length, 0)
  fail = true
  await assert.rejects(s.fetchMembers(1), /offline/)
  assert.equal(s.members.value.length, 0)
})

for (const embedded of [false, true]) test(`check-in loads once on ${embedded ? 'embedded' : 'route'} entry and reloads on activity change`, async t => {
  const params = vue.reactive({ id: '1' }), props = vue.reactive(embedded ? { activity: { id: 2 } } : {})
  const get = t.mock.fn(async url => url.endsWith('/members') ? [{ id: 1, records: [] }] : {})
  const s = activity(t, get, props, params)
  await settle()
  const urls = id => [`/activities/${id}`, `/activities/${id}/members`, `/activities/${id}/my-status`, `/activities/${id}/stats`, '/activities/types']
  assert.deepEqual(get.mock.calls.map(c => c.arguments[0]), urls(embedded ? 2 : 1))
  if (embedded) props.activity = { id: 3 }; else params.id = '3'
  await settle()
  assert.deepEqual(get.mock.calls.slice(5).map(c => c.arguments[0]), urls(3))
  assert.equal(s.loading.value, false)
})

function clubPage(t, name, api, props = {}, user = { isLoggedIn: true, userId: 'user' }) {
  const mocks = new Proxy({
    '@/utils/request': { __esModule: true, default: api }, '@/stores/user': { useUserStore: () => user },
    './orderHelpers': loadScript('views/太初俱乐部/orderHelpers.js'),
    '@/api/customerService.ts': { fetchCustomerServices: async () => [] },
  }, { has: (target, key) => key in target || key.endsWith('.vue'), get: (target, key) => target[key] ?? {} })
  const component = loadScript(`views/太初俱乐部/${name}.vue`, mocks).default
  const app = vue.createRenderer({ createComment: () => ({}), insert() {}, remove() {}, parentNode() {}, nextSibling() {} })
    .createApp({ ...component, render: () => null }, props)
  const vm = app.mount({})
  t.after(() => app.unmount())
  return vm
}
function clubApi(t) {
  t.mock.method(globalThis, 'setInterval', () => 1); t.mock.method(globalThis, 'clearInterval', () => {})
  t.mock.method(console, 'error', () => {})
  for (const key of ['alert', 'confirm']) {
    const old = Object.getOwnPropertyDescriptor(globalThis, key)
    Object.defineProperty(globalThis, key, { configurable: true, value: t.mock.fn(() => true) })
    t.after(() => old ? Object.defineProperty(globalThis, key, old) : delete globalThis[key])
  }
  return { post: t.mock.fn(async () => ({ orderNo: 'created' })), get: t.mock.fn(async (url, { params } = {}) => {
    if (url === '/club/cs/me') return { hasApplied: true, auditStatus: 'approved' }
    if (url === '/club/operators/me') return { data: { hasApplied: true, status: 'online', games: [{ auditStatus: 'approved', gameCode: 'g', level: 'L3' }] } }
    if (url === '/club/cs/orders' || url === '/club/orders/operator') return { items: [{ orderNo: 'order' }], total: 11 }
    if (url === '/club/cs/complaints') return { data: { items: [], total: params.status === 'pending' ? 2 : 9 } }
    throw Error(`Unexpected endpoint: ${url}`)
  }) }
}

test('operator entry fetches profile once; accept and abandon still refresh profile and orders', async t => {
  const api = clubApi(t), vm = clubPage(t, 'OperatorOrders', api)
  await settle()
  assert.equal(api.get.mock.callCount(), 2); assert.equal(vm.myProfileStatus, 'online'); assert.equal(vm.myLevels.g, 'L3')
  for (const action of ['accept', 'abandon']) {
    const count = api.get.mock.callCount()
    if (action === 'abandon') await vm.abandonOrder({ orderNo: 'order' }); else await vm.doAction({ orderNo: 'order' }, action)
    assert.equal(api.post.mock.calls.at(-1).arguments[0], `/club/orders/order/${action}`)
    assert.equal(api.get.mock.callCount() - count, 2); assert.equal(vm.acting, '')
  }
  vm.myProfileStatus = 'offline'; await vm.doAction({ orderNo: 'order' }, 'accept')
  assert.equal(api.post.mock.callCount(), 2)
  api.post.mock.mockImplementation(async () => { throw Error('offline') })
  await vm.abandonOrder({ orderNo: 'order' })
  assert.equal(vm.acting, ''); assert.equal(alert.mock.calls.at(-1).arguments[0], 'offline')
})

test('CS refresh reuses active list total and keeps pending complaint counts separate', async t => {
  const api = clubApi(t), vm = clubPage(t, 'CsDashboard', api)
  await settle()
  assert.equal(api.get.mock.callCount(), 8)
  assert.equal(api.get.mock.calls.filter(c => c.arguments[1]?.params?.tab === 'waiting-confirm').length, 1)
  assert.equal(vm.tabCounts['waiting-confirm'], 11); assert.equal(vm.tabCounts.complaints, 2)
  await vm.switchTab('complaints'); assert.equal(vm.complaintsTotal, 9); assert.equal(vm.tabCounts.complaints, 2)
  const count = api.get.mock.callCount(); await vm.refreshOrders()
  assert.equal(api.get.mock.callCount() - count, 8); assert.equal(vm.tabCounts.complaints, 2)
  await vm.switchTab('processing'); vm.page = 3
  await vm.refreshOrders()
  const calls = api.get.mock.calls.slice(-7)
  assert.equal(calls.filter(c => c.arguments[1]?.params?.tab === 'processing').length, 1)
  assert.equal(calls.find(c => c.arguments[1]?.params?.tab === 'processing').arguments[1].params.page, 3)
})

test('CS assignment preserves payload and refresh scope; failed lists release loading', async t => {
  const api = clubApi(t), vm = clubPage(t, 'CsDashboard', api)
  await settle()
  vm.assignOrder = { orderNo: 'order', minOperatorCount: 1, maxOperatorCount: 2 }; vm.selectedOpIds = ['op']
  vm.availableOps = [{ userId: 'op', status: 'online' }]
  const count = api.get.mock.callCount(); await vm.doAssign()
  assert.deepEqual(api.post.mock.calls[0].arguments, ['/club/cs/orders/order/assign-operator', { operatorUserIds: ['op'] }])
  assert.equal(api.get.mock.callCount() - count, 7); assert.equal(vm.assignOrder, null); assert.equal(vm.assigning, false)
  api.get.mock.mockImplementation((url, options) => {
    if (options?.params?.tab === 'waiting-confirm') throw Error('offline')
    return { items: [], total: 11 }
  })
  await vm.refreshOrders()
  assert.deepEqual(vm.orders, []); assert.equal(vm.total, 0); assert.equal(vm.tabCounts['waiting-confirm'], 0)
  assert.equal(vm.loadingOrders, false); assert.equal(vm.tabCounts.processing, 11)
  api.get.mock.mockImplementation((url, options) => {
    if (options?.params?.pageSize === 20) throw Error('list failed')
    return { total: 11 }
  })
  await vm.refreshOrders()
  assert.equal(vm.total, 0); assert.equal(vm.tabCounts['waiting-confirm'], 11)

})

test('order modal keeps manual-count validation and create payload', async t => {
  const api = clubApi(t), vm = clubPage(t, 'OrderModal', api, {
    orderData: { gameCode: 'g', orderTypeCode: 'type', price: 100, minOperatorCount: 1, maxOperatorCount: 2, params: { hours: 2 } },
  })
  vm.operatorMode = 'manual'; await vm.submit(); assert.equal(api.post.mock.callCount(), 0)
  vm.selectedOpIds = ['a', 'b', 'c']; await vm.submit(); assert.equal(api.post.mock.callCount(), 0)
  vm.operators = [{ userId: 'a', status: 'online', games: [{ gameCode: 'g' }] }]
  vm.selectedOpIds = ['a']; vm.remark = ' 备注 '; await vm.submit()
  assert.deepEqual(api.post.mock.calls[0].arguments, ['/club/orders', {
    gameCode: 'g', orderTypeCode: 'type', params: { hours: 2 }, remark: '备注', source: 'self', operatorUserIds: ['a'],
  }])
  assert.equal(vm.successOrderNo, 'created'); assert.equal(vm.submitting, false)
})

test('CS assignment ignores stale operators, blocks duplicate requests and preserves failed selections', async t => {
  const api = clubApi(t), vm = clubPage(t, 'CsDashboard', api)
  await settle()
  const pending = []
  api.get.mock.mockImplementation(() => new Promise(resolve => pending.push(resolve)))
  vm.openAssign({ orderNo: 'old', gameCode: 'old' })
  vm.closeAssign()
  vm.openAssign({ orderNo: 'new', gameCode: 'new' })
  pending[1]({ items: [{ userId: 'new-op', status: 'online' }, { userId: 'off', status: 'offline' }] }); await settle()
  pending[0]({ items: [{ userId: 'old-op', status: 'online' }] }); await settle()
  assert.equal(vm.availableOps[0].userId, 'new-op')
  vm.toggleOp('off'); assert.equal(vm.selectedOpIds.length, 0)
  vm.toggleOp('new-op'); assert.equal(vm.canAssign, true)
  let reject
  api.post.mock.mockImplementation(() => new Promise((_, fail) => { reject = fail }))
  const first = vm.doAssign(); await vm.doAssign(); vm.closeAssign(); vm.toggleOp('new-op')
  assert.equal(api.post.mock.callCount(), 1)
  assert.equal(vm.assignOrder.orderNo, 'new'); assert.deepEqual(vm.selectedOpIds, ['new-op'])
  reject(Error('失败')); await first
  assert.equal(vm.assigning, false); assert.equal(vm.assignOrder.orderNo, 'new')
  vm.closeAssign(); assert.equal(vm.assignOrder, null)
})

test('battle registration failure keeps modal selection and avoids duplicate posts', async t => {
  t.mock.method(console, 'error', () => {})
  let reject
  const register = t.mock.fn(() => new Promise((_, fail) => { reject = fail }))
  const mounted = mountScript('views/柴圈板块/Battle/components/BattleDetail.vue', {
    'vue-router': { useRoute: () => ({ params: { id: 'b' } }), useRouter: () => ({ push() {} }) },
    '@/stores/user': { useUserStore: () => ({ userId: 'u', userInfo: { id: 'u' } }) },
    '../battle_Store': { useBattleStore: () => ({ currentBattle: { id: 'b' }, fetchDetail: async () => {}, clear() {} }) },
    '../battle_api': { battleApi: { register } },
    '../../OCs/stickman': { stickmanApi: { getMyCharacters: async () => [] } },
    'element-plus': { ElMessage: { error() {}, success() {} }, ElMessageBox: {} },
  })
  t.after(mounted.unmount)
  const s = mounted.state
  s.showRegisterModal.value = true; s.selectedRegisterOcIds.value = ['oc']
  const first = s.handleRegisterSubmit(); await s.handleRegisterSubmit()
  assert.equal(register.mock.callCount(), 1)
  reject(Error('offline')); await first
  assert.equal(s.showRegisterModal.value, true); assert.deepEqual(s.selectedRegisterOcIds.value, ['oc'])
  assert.equal(s.joining.value, false)
  register.mock.mockImplementation(async () => ({})); await s.handleRegisterSubmit()
  assert.equal(s.showRegisterModal.value, false); assert.equal(s.selectedRegisterOcIds.value.length, 0)
})

test('battle creation carries the source OC and invites teammates separately from the owner', async t => {
  const oc = { id: 'own-oc', authorId: 'owner', title: '当前人设', status: 'published', isBattleEnabled: true }
  const create = t.mock.fn(async () => ({ id: 'created' }))
  const mounted = mountScript('views/柴圈板块/Battle/components/CreateBattle.vue', {
    'vue-router': { useRoute: () => ({ query: { ocId: oc.id } }), useRouter: () => ({ push() {} }) },
    '@/stores/user': { useUserStore: () => ({ userId: 'owner' }) },
    '../battle_Store': { useBattleStore: () => ({ create }) },
    '@/composables/useCos': { useCos: () => ({}) },
    '../../OCs/stickman': { stickmanApi: { getList: async () => ({ items: [oc] }) } },
    'element-plus': { ElMessage: { success() {}, error() {}, warning() {} } },
  })
  t.after(mounted.unmount); await settle()
  const s = mounted.state
  assert.deepEqual(s.teamA.value[0].ocIds, [oc.id])
  s.removeMemberOc('A', 0, oc.id); assert.equal(s.teamA.value[0].userId, 'owner')
  s.addOcToMember('A', 0, oc)
  s.addMember('A'); s.addOcToMember('A', 1, { id: 'mate-oc', authorId: 'mate', title: '队友人设' })
  s.addMember('B'); s.addOcToMember('B', 0, { id: 'guest-oc', authorId: 'guest', title: '对方人设' })
  Object.assign(s.form, { title: '测试', battleType: '2v1', rules: '规则' })
  await s.handleSubmit()
  const payload = create.mock.calls[0].arguments[0]
  assert.deepEqual(payload.challengerOcIds, ['own-oc'])
  assert.deepEqual(payload.opponentOcIds, { mate: ['mate-oc'], guest: ['guest-oc'] })
  assert.deepEqual(JSON.parse(payload.battleConfigJson), { teams: { owner: 1, mate: 1, guest: 2 } })
  assert.equal(payload.judgmentType, 'internal')
  s.addMember('B'); assert.equal(s.isValid.value, false)
})

test('CS reassignment retains existing offline members but cannot add new offline members', async t => {
  const api = clubApi(t), vm = clubPage(t, 'CsDashboard', api); await settle()
  vm.reassignOrder = { orderNo: 'o', operators: [{ userId: 'existing' }], maxOperatorCount: 2 }
  vm.reassignReason = '替换另一位成员'
  vm.availableOps = [{ userId: 'existing', status: 'offline' }, { userId: 'new', status: 'offline' }]
  vm.selectedOpIds = ['existing']; assert.equal(vm.canReassign, true)
  vm.toggleOp('new'); assert.deepEqual(vm.selectedOpIds, ['existing'])
  vm.toggleOp('existing'); assert.deepEqual(vm.selectedOpIds, [])
})

test('order pages retain login and approval guards without issuing order requests', async t => {
  const api = clubApi(t)
  for (const name of ['OperatorOrders', 'CsDashboard']) {
    api.get.mock.resetCalls()
    clubPage(t, name, api, {}, { isLoggedIn: false })
    await settle(); assert.equal(api.get.mock.callCount(), 0)
    api.get.mock.mockImplementation(async () => ({ hasApplied: true, auditStatus: 'pending', games: [] }))
    clubPage(t, name, api)
    await settle(); assert.equal(api.get.mock.callCount(), 1)
  }
})

test('spirit sync payload preserves raw content, metadata defaults and block identity/order', () => {
  const { createEditorBlocks, createNoteSyncPayload } = loadScript('utils/editorHelpers.ts', { nanoid: { nanoid: size => 'x'.repeat(size) } })
  const doc = { type: 'doc', content: [{ type: 'paragraph', attrs: { id: 'keep' }, content: [{ type: 'text', text: '正文' }] }, { type: 'image', attrs: { assetId: 'asset' } }] }
  const blocks = createEditorBlocks(doc, 'n', 'blog')
  assert.deepEqual(blocks, doc.content.map((block, sortOrder) => ({ id: sortOrder ? 'x'.repeat(21) : 'keep', ownerId: 'n', ownerType: 'blog', type: block.type, sortOrder, data: JSON.stringify(block) })))
  assert.equal(doc.content[1].attrs.id, undefined)
  assert.deepEqual(createEditorBlocks(null, 'n'), [])
  assert.deepEqual(createNoteSyncPayload('n', null, { rawContent: doc }), { noteId: 'n', title: '', extraData: '[]', tags: [], rawContent: doc })
  const meta = { title: '标题', extraData: '[1]', tags: ['标签'] }
  const payload = createNoteSyncPayload('n', meta, { blocks })
  assert.deepEqual(payload, { noteId: 'n', ...meta, blocks })
  assert.equal(payload.blocks, blocks)
})

test('spirit autosave coalesces the same note across callers without dropping other notes', async t => {
  t.mock.timers.enable({ apis: ['setTimeout'] })
  const save = t.mock.fn(async () => {}), toast = t.mock.fn()
  const { useAutoSave } = loadScript('composables/useAutoSave.ts', { '@/api/lingmai': { lingmaiApi: { updateNoteContent: save } } })
  const a = useAutoSave(), b = useAutoSave()
  a.syncToCloud('a', { title: 'old' }, toast)
  t.mock.timers.tick(1000)
  b.syncToCloud('a', { title: 'latest' }, toast)
  b.syncToCloud('b', { title: 'other' })
  t.mock.timers.tick(1999); await settle()
  assert.equal(save.mock.callCount(), 0)
  t.mock.timers.tick(1); await settle()
  assert.deepEqual(save.mock.calls.map(c => c.arguments), [['a', { title: 'latest' }], ['b', { title: 'other' }]])
  assert.deepEqual(toast.mock.calls.map(c => c.arguments), [['☁️ 已自动同步', 1500]])
})

test('spirit autosave reports failure and allows the next edit to save', async t => {
  t.mock.timers.enable({ apis: ['setTimeout'] })
  t.mock.method(console, 'error', () => {})
  const save = t.mock.fn(async () => { if (save.mock.callCount() === 0) throw new Error('offline') }), toast = t.mock.fn()
  const { syncToCloud } = loadScript('composables/useAutoSave.ts', { '@/api/lingmai': { lingmaiApi: { updateNoteContent: save } } }).useAutoSave()
  syncToCloud('a', {}, toast); t.mock.timers.tick(2000); await settle()
  syncToCloud('a', { title: 'retry' }, toast); t.mock.timers.tick(2000); await settle()
  assert.equal(save.mock.callCount(), 2)
  assert.deepEqual(toast.mock.calls.map(c => c.arguments), [['❌ 同步失败', 2000], ['☁️ 已自动同步', 1500]])
})

// 执行真实编辑器与上传 hook；事务桩模拟 Tiptap 派发后的 onUpdate。
function spiritEditor(t, upload = async () => ({ assetId: 'asset', url: '/uploaded' }), props = {}) {
  let options, nodes = []
  const changed = t.mock.fn(), updated = t.mock.fn(), dispatched = t.mock.fn(), isUploading = vue.ref(false)
  const uploadAndRegister = t.mock.fn(upload)
  const uploads = loadScript('composables/useEditorImageUpload.ts', { './useCos': { useCos: () => ({ uploadAndRegister, progress: vue.ref(0), isUploading }) } })
  const view = { dom: { dispatchEvent: dispatched }, state: {
    schema: { nodes: Object.fromEntries(['image', 'pdfEmbed'].map(name => [name, { create: attrs => ({ type: { name }, attrs, nodeSize: 1 }) }])) },
    doc: { descendants: visit => nodes.slice().forEach((node, pos) => visit(node, pos)) },
    tr: { insert: (pos, node) => () => nodes.splice(pos, 0, node), replaceSelectionWith: node => () => nodes.push(node),
      replaceWith: (from, to, node) => () => nodes.splice(from, to - from, node), delete: (from, to) => () => nodes.splice(from, to - from) },
  }, dispatch: transaction => { transaction(); options.onUpdate({ editor }) } }
  const editor = { view, state: { selection: { empty: false } }, getJSON: () => ({ type: 'doc', content: nodes.map(n => ({ type: n.type.name, attrs: n.attrs })) }) }
  const mounted = mountScript('components/SpiritText.vue', {
    vue: { ...vue, onMounted() {}, onUnmounted() {} }, 'vue-router': { useRouter: () => ({}) },
    '@tiptap/vue-3': { useEditor: value => { options = value; return vue.shallowRef(editor) } }, nanoid: { nanoid: () => 'id' },
    './ContentReferencePicker.vue': {}, './SpiritTextComponents/EditorBubbleMenu.vue': {}, '../utils/editorConfig': { spiritExtensions: [], spiritColors: [] },
    '../composables/useSpiritData': { useSpiritData: () => ({ notes: vue.ref([]), currentNoteId: vue.ref('n'), updateNoteContent: updated }) },
    '@/composables/useEditorImageUpload.ts': uploads, '@/composables/slashExtension.ts': { SlashMenuExtension: {} }, '@/api/lingmai': { lingmaiApi: {} },
  }, props, changed)
  t.after(mounted.unmount)
  return { ...mounted.state, getSnapshot: mounted.exposed.getSnapshot, view, options, changed, updated, dispatched, uploadAndRegister, isUploading }
}

test('standalone writing does not overwrite the active LingMai note', async t => {
  const s = spiritEditor(t, undefined, { standalone: true, initialContent: { type: 'doc', content: [] } })
  await s.handleImageProcess(s.editor, s.view, { type: 'image/png', name: 'image.png' }, 0)
  assert.equal(s.updated.mock.callCount(), 0)
  assert.equal(s.getSnapshot().content[0].attrs.assetId, 'asset')
  assert.ok(s.changed.mock.callCount() > 0)
})

test('image and PDF uploads retain asset fields and notify each final document only once', async t => {
  const s = spiritEditor(t)
  await s.handleImageProcess(s.editor, s.view, { type: 'image/png', name: 'image.png' })
  const image = s.editor.value.getJSON().content[0]
  assert.deepEqual(image, { type: 'image', attrs: { src: '/uploaded', align: 'center', width: '60%', caption: '', assetId: 'asset' } })
  // 占位图是一次独立编辑；最终图片事务与上传回调不应重复上报相同正文。
  assert.equal(s.changed.mock.callCount(), 2)
  await s.handlePdfProcess(s.editor, s.view, { type: 'application/pdf', name: 'file.pdf' }, 0)
  assert.deepEqual(s.editor.value.getJSON().content[0], { type: 'pdfEmbed', attrs: { assetId: 'asset', url: '/uploaded', fileName: 'file.pdf', height: '1000px' } })
  assert.equal(s.changed.mock.callCount(), 3)
  assert.equal(s.updated.mock.callCount(), 3)
  assert.equal(s.dispatched.mock.callCount(), 3)
  assert.ok(s.uploadAndRegister.mock.calls.every(c => c.arguments[1] === 'lingmai'))
  assert.ok(s.updated.mock.calls.every(c => c.arguments[0] === 'n'))
  assert.ok(s.dispatched.mock.calls.every(c => c.arguments[0].type === 'change-content' && c.arguments[0].bubbles))
  s.options.onUpdate({ editor: s.editor.value })
  assert.equal(s.changed.mock.callCount(), 3)
})

test('upload completion still saves when ordinary editor updates are paused', async t => {
  const s = spiritEditor(t)
  s.isUploading.value = true
  await s.handleImageProcess(s.editor, s.view, { type: 'image/png', name: 'image.png' })
  assert.equal(s.changed.mock.callCount(), 1)
  s.isUploading.value = false
  s.options.onUpdate({ editor: s.editor.value })
  assert.equal(s.changed.mock.callCount(), 1)
})

test('failed uploads remove image placeholders, insert no PDF and reject unsupported types', async t => {
  t.mock.method(console, 'error', () => {})
  const s = spiritEditor(t, async () => { throw new Error('upload/register failed') })
  await s.handleImageProcess(s.editor, s.view, { type: 'image/png' })
  await s.handlePdfProcess(s.editor, s.view, { type: 'application/pdf' })
  assert.deepEqual(s.editor.value.getJSON().content, [])
  assert.equal(s.uploadAndRegister.mock.callCount(), 2)
  await s.handleImageProcess(s.editor, s.view, { type: 'text/plain' })
  await s.handlePdfProcess(s.editor, s.view, { type: 'text/plain' })
  assert.equal(s.uploadAndRegister.mock.callCount(), 2)
})

const deferred = () => { let resolve, reject; const promise = new Promise((a, b) => { resolve = a; reject = b }); return { promise, resolve, reject } }

test('history refresh follows note changes, ignores stale replies and supports retry after failure', async t => {
  const requests = []
  const props = vue.reactive({ modelValue: true, noteId: 'a' })
  const panel = mountScript('views/太初灵脉/components/HistoryPanel.vue', { '../../../api/lingmai': { lingmaiApi: {
    getHistoryList: id => { const d = deferred(); requests.push({ id, ...d }); return d.promise },
  } } }, props)
  t.after(panel.unmount)
  props.noteId = 'b'; await settle()
  requests[0].resolve([{ id: 'old' }]); await settle()
  assert.equal(panel.state.loading.value, true)
  assert.deepEqual(panel.state.historyList.value, [])
  requests[1].reject(new Error('offline')); await settle()
  assert.match(panel.state.error.value, /加载失败/)
  const refresh = panel.exposed.refresh(); requests[2].resolve([{ id: 'new' }]); await refresh
  assert.deepEqual(panel.state.historyList.value, [{ id: 'new' }])
  assert.equal(panel.state.error.value, '')
  props.modelValue = false; await settle()
  assert.deepEqual(panel.state.historyList.value, [])
  assert.equal(requests.length, 3)
})

test('snapshot reads live editor content, retains system blocks and clones pending schedule data', t => {
  const note = vue.ref({ id: 'n', type: 'blog', blocks: [{ id: 'cover', type: 'blog_fixed_cover', data: '{"url":"/cover"}' }] })
  const display = vue.ref({ ...note.value }), loading = vue.ref(false)
  const helpers = loadScript('utils/editorHelpers.ts', { nanoid: { nanoid: () => 'new' } })
  const m = mountScript('views/太初灵脉/components/NoteEditorView.vue', {
    vue: { ...vue, inject: key => ({ currentDisplayNote: display, currentBlocks: vue.ref([]), isContentLoading: loading })[key] },
    'vue-router': { useRoute: () => ({ params: { id: 'n' } }) }, './MainWorkspace.vue': {},
    '@/composables/useSpiritData': { useSpiritData: () => ({ activeNote: note, updateNoteTitle() {} }) },
    '@/composables/useAutoSave': { useAutoSave: () => ({ syncToCloud() {} }) }, '@/utils/editorHelpers': helpers,
  })
  t.after(m.unmount)
  assert.throws(m.exposed.getSnapshot, /尚未就绪/)
  const doc = { type: 'doc', content: [{ type: 'paragraph', attrs: { id: 'p' }, content: [{ type: 'text', text: '最新内容' }] }] }
  m.state.workspaceRef.value = { getDocument: () => doc }
  const snapshot = m.exposed.getSnapshot()
  assert.deepEqual(snapshot.blocks.map(b => b.id), ['cover', 'p'])
  doc.content[0].content[0].text = '保存后继续编辑'
  assert.equal(snapshot.content[0].content[0].text, '最新内容')
  display.value.type = 'schedule'; m.state.workspaceRef.value = null
  note.value.blocks = [{ id: 'item', type: 'schedule-item', data: '{"title":"尚未触发400ms保存"}' }]
  assert.deepEqual(m.exposed.getSnapshot().blocks, note.value.blocks)
  loading.value = true; assert.equal(m.exposed.getSnapshot(), undefined)
})

test('snapshot refuses unfinished uploads and ignores callbacks from a destroyed editor', async t => {
  const upload = deferred(), s = spiritEditor(t, () => upload.promise)
  const processing = s.handlePdfProcess(s.editor, s.view, { type: 'application/pdf' })
  assert.throws(s.getSnapshot, /上传完成/)
  upload.resolve({ assetId: 'asset', url: '/pdf' }); await processing
  assert.equal(s.getSnapshot().content[0].type, 'pdfEmbed')
  s.editor.value.isDestroyed = true
  s.syncEditorContent(s.editor.value, { type: 'doc', content: [] })
  assert.equal(s.changed.mock.callCount(), 1)
})

test('restore waits for running writes, preserves failed-restore drafts and discards successful-restore queues', async t => {
  t.mock.timers.enable({ apis: ['setTimeout'] })
  const gate = deferred(), saves = []
  const api = { updateNoteContent: (id, payload) => { saves.push([id, payload]); return saves.length === 1 ? gate.promise : Promise.resolve() } }
  const queue = loadScript('composables/useAutoSave.ts', { '@/api/lingmai': { lingmaiApi: api } }).useAutoSave()
  queue.syncToCloud('a', { title: 'running' }); t.mock.timers.tick(2000); await settle()
  queue.syncToCloud('a', { title: 'pending' })
  queue.syncToCloud('a', { rawContent: { type: 'doc' } }, undefined, 1000)
  let paused = false
  const pause = queue.pauseNote('a').then(() => { paused = true })
  t.mock.timers.tick(3000); await settle(); assert.equal(paused, false); assert.equal(saves.length, 1)
  gate.resolve(); await pause
  queue.resumeNote('a'); t.mock.timers.tick(2000); await settle()
  assert.equal(saves.length, 3)
  queue.syncToCloud('a', { title: 'discard' }); queue.syncToCloud('b', { title: 'keep' })
  await queue.pauseNote('a'); queue.resumeNote('a', true); t.mock.timers.tick(2000); await settle()
  assert.deepEqual(saves.at(-1), ['b', { title: 'keep' }])
  assert.equal(saves.length, 4)
})

async function historyLayout(t, overrides = {}) {
  const note = { id: 'a', title: '标题', type: 'note', content: { type: 'doc', content: [] }, blocks: [] }
  const activeNote = vue.ref(note), route = vue.reactive({ params: { id: 'a' } })
  const notes = vue.ref(overrides.notes ?? [note, { ...note, id: 'b' }])
  const snapshot = { type: 'doc', content: [], blocks: [{ id: 'p', type: 'paragraph', data: '{}' }] }
  const api = { createSnapshot: t.mock.fn(async () => {}), rollbackTo: t.mock.fn(async () => {}), ...overrides.api }
  const queue = { syncToCloud: t.mock.fn(), saveNow: t.mock.fn(async () => {}), pauseNote: t.mock.fn(async () => {}), resumeNote: t.mock.fn(), ...overrides.queue }
  const selectNote = t.mock.fn(async id => { activeNote.value = { ...note, id }; return activeNote.value })
  const components = Object.fromEntries(['SidebarIndex', 'RightSidePanel', 'HistoryPanel', 'GraphView', 'PublishModal', 'NoteSettingsPanel', 'TopBar', 'QuickEditorDrawer'].map(n => ['./components/' + n + '.vue', {}]))
  const m = mountScript('views/太初灵脉/SpiritLayout.vue', {
    ...components, '@/components/SpiritToast.vue': {}, vue: { ...vue, onMounted() {}, onUnmounted() {} },
    'vue-router': { useRoute: () => route, useRouter: () => ({}) }, '../../api/lingmai': { lingmaiApi: api },
    '../../composables/useSpiritData': { useSpiritData: () => ({ activeNote, notes, currentSpaceId: vue.ref('space'), isLoading: vue.ref(false), selectNote }) },
    '@/utils/editorHelpers': loadScript('utils/editorHelpers.ts', { nanoid: { nanoid: () => 'id' } }),
    '@/composables/useAutoSave': { useAutoSave: () => queue },
  })
  t.after(m.unmount); await settle()
  const toast = t.mock.fn(), refresh = t.mock.fn(async () => {})
  m.state.noteEditorRef.value = { getSnapshot: () => structuredClone(snapshot) }
  m.state.historyPanelRef.value = { refresh }; m.state.toastRef.value = { show: toast }
  return { ...m.state, route, api, queue, selectNote, toast, refresh, snapshot }
}

test('manual save waits for sync, prevents double submission and refreshes history after snapshot', async t => {
  const gate = deferred(), s = await historyLayout(t, { queue: { saveNow: t.mock.fn(() => gate.promise) } })
  const action = s.handleHistoryAction(); await s.handleHistoryAction()
  assert.equal(s.queue.saveNow.mock.callCount(), 1); assert.equal(s.api.createSnapshot.mock.callCount(), 0)
  gate.resolve(); await action
  assert.deepEqual(s.api.createSnapshot.mock.calls[0].arguments, ['a', s.snapshot])
  assert.equal(s.refresh.mock.callCount(), 1); assert.equal(s.historyBusy.value, false)
  assert.match(s.toast.mock.calls.at(-1).arguments[0], /当前版本已保存/)
})

test('manual sync and snapshot failures are visible and leave history retryable', async t => {
  const s = await historyLayout(t)
  s.queue.saveNow.mock.mockImplementation(async () => { throw new Error('同步失败') })
  await s.handleHistoryAction(); assert.equal(s.api.createSnapshot.mock.callCount(), 0)
  s.queue.saveNow.mock.mockImplementation(async () => {})
  s.api.createSnapshot.mock.mockImplementation(async () => { throw new Error('快照失败') })
  await s.handleHistoryAction(); assert.equal(s.refresh.mock.callCount(), 0)
  assert.match(s.toast.mock.calls.at(-1).arguments[0], /快照失败/)
  assert.equal(s.historyBusy.value, false)
})

test('switching notes during manual save keeps the original target and does not refresh another history', async t => {
  const gate = deferred(), s = await historyLayout(t, { queue: { saveNow: t.mock.fn(() => gate.promise) } })
  const action = s.handleHistoryAction(); s.route.params.id = 'b'; await settle()
  gate.resolve(); await action
  assert.equal(s.api.createSnapshot.mock.calls[0].arguments[0], 'a')
  assert.equal(s.refresh.mock.callCount(), 0)
  assert.equal(s.currentDisplayNote.value.id, 'b')
})

test('restore pauses saves, reloads the displayed note and resumes drafts on failure', async t => {
  const s = await historyLayout(t)
  s.isHistoryOpen.value = true
  await s.handleHistoryAction({ id: 'rev' }, 'a')
  assert.deepEqual(s.api.rollbackTo.mock.calls[0].arguments, ['a', 'rev'])
  assert.equal(s.selectNote.mock.callCount(), 2)
  assert.equal(s.isHistoryOpen.value, false)
  assert.deepEqual(s.queue.resumeNote.mock.calls[0].arguments, ['a', true])
  s.api.rollbackTo.mock.mockImplementation(async () => { throw new Error('恢复失败') })
  s.isHistoryOpen.value = true
  await s.handleHistoryAction({ id: 'rev' }, 'a')
  assert.equal(s.isHistoryOpen.value, true)
  assert.equal(s.isContentLoading.value, false)
  assert.deepEqual(s.queue.resumeNote.mock.calls.at(-1).arguments, ['a', false])
  assert.match(s.toast.mock.calls.at(-1).arguments[0], /恢复失败/)
})


test('snapshot API keeps existing endpoints and contentJson envelope', async () => {
  const calls = [], request = { get: async (...args) => calls.push(['get', ...args]), post: async (...args) => calls.push(['post', ...args]) }
  const api = loadScript('api/lingmai.ts', { '../utils/request': { __esModule: true, default: request }, nanoid: { nanoid: () => 'id' } }).lingmaiApi
  const doc = { type: 'doc', content: [], blocks: [] }
  await api.createSnapshot('n', doc); await api.getHistoryList('n'); await api.rollbackTo('n', 'revision')
  assert.deepEqual(calls, [['post', '/LingMai/notes/n/snapshot', { contentJson: JSON.stringify(doc), remark: '手动保存' }], ['get', '/LingMai/notes/n/history'], ['post', '/LingMai/history/revision/rollback']])
})

test('content saves retain their one-second delay and failed detail loads never return stale notes', async () => {
  const calls = [], gate = deferred()
  const store = loadScript('composables/useSpiritData.ts', {
    '../api/lingmai': { lingmaiApi: { getNote: () => gate.promise } }, '../api/Wiki': { wikiApi: {} }, 'lodash-es': { debounce: fn => fn },
    './useAutoSave': { useAutoSave: () => ({ syncToCloud: (...args) => calls.push(args) }) },
    '../utils/editorHelpers': loadScript('utils/editorHelpers.ts', { nanoid: { nanoid: () => 'id' } }),
  }).useSpiritData()
  const doc = { type: 'doc', content: [] }
  store.notes.value = [{ id: 'n', title: '标题' }]
  await store.updateNoteContent('n', doc)
  assert.equal(calls[0][3], 1000); assert.deepEqual(calls[0][1].rawContent, doc)
  const selection = store.selectNote('n', true)
  gate.reject(new Error('detail failed'))
  assert.equal(await selection, undefined)
  assert.equal(store.isLoading.value, false)
})

test('a completed restore never reloads or closes another note after navigation', async t => {
  const gate = deferred(), s = await historyLayout(t, { api: { rollbackTo: t.mock.fn(() => gate.promise) } })
  s.isHistoryOpen.value = true
  const action = s.handleHistoryAction({ id: 'rev' }, 'a'); await settle()
  s.route.params.id = 'b'; await settle()
  gate.resolve(); await action
  assert.equal(s.currentDisplayNote.value.id, 'b')
  assert.equal(s.isHistoryOpen.value, true)
  assert.deepEqual(s.queue.resumeNote.mock.calls.at(-1).arguments, ['a', true])
  assert.equal(s.selectNote.mock.callCount(), 2)
})


test('publication success updates the original note without erasing its type', async t => {
  const s = await historyLayout(t)
  s.notes.value[0].type = 'art'
  s.onPublishSuccess({ noteId: 'a' })
  assert.equal(s.notes.value[0].type, 'art'); assert.equal(s.notes.value[0].isPublic, true)
  await s.preparePublish('a')
  assert.deepEqual(s.queue.saveNow.mock.calls[0].arguments[1].blocks, s.snapshot.blocks)
  s.isContentLoading.value = true
  await assert.rejects(s.preparePublish('a'), /尚未准备好/)
})

test('withdrawal updates its source after switching notes and cannot be submitted twice', async t => {
  const pending = deferred(), call = t.mock.fn(() => pending.promise)
  const s = await historyLayout(t, { api: { unpublishNote: call } })
  s.activeNote.value.isPublic = true; s.notes.value[0].isPublic = true
  const run = s.handlePublishClick(); await s.handlePublishClick()
  s.route.params.id = 'b'; await settle(); s.activeNote.value.isPublic = true
  pending.resolve(); await run
  assert.equal(call.mock.callCount(), 1); assert.equal(s.notes.value[0].isPublic, false)
  assert.equal(s.activeNote.value.isPublic, true); assert.equal(s.publicationBusy.value, false)
})

test('collaboration metadata refresh preserves the mounted panel and drops stale project responses', async t => {
  const route = vue.reactive({params:{id:'one'}}), pending=[]
  const child = {__esModule:true,default:{}}
  const s=mountScript('views/太初协作/协作组件/ProjectDetail.vue', {
    'vue-router': {useRoute:()=>route},
    '../../../api/projectService': {__esModule:true,default:{getProjectSettings: id=>new Promise(resolve=>pending.push({id,resolve}))}},
    ...Object.fromEntries(['ProjectKanban','ProjectSettings','ProjectMember','ProjectTimeline','ProjectDocument','ProjectReports','ProjectDeliveries'].map(name=>[`./${name}.vue`,child])),
  })
  t.after(s.unmount)
  pending[0].resolve({id:'one',name:'first'}); await settle()
  const refresh=s.state.refreshProject()
  assert.equal(s.state.project.value.id,'one','child must stay mounted during metadata refresh')
  route.params.id='two'; await settle()
  assert.equal(s.state.project.value,null,'clear previous project on route change')
  pending[1].resolve({id:'one',name:'stale'}); await refresh; await settle()
  assert.equal(s.state.project.value,null)
  pending[2].resolve({id:'two',name:'second'}); await settle()
  assert.equal(s.state.project.value.id,'two')
})

test('private profile library discards old type responses and resets pagination', async t => {
  const user=vue.reactive({token:'test',isLoggedIn:true,userInfo:{experience:0},fetchUserInfo:async()=>null})
  const pending=[]
  const s=mountScript('views/个人中心/index.vue',{
    '../../stores/user':{useUserStore:()=>user},
    '../../composables/useCos':{useCos:()=>({uploadFile:async()=>({url:''}),isUploading:vue.ref(false)})},
    '../../utils/request':{__esModule:true,default:{get:(url,config)=>new Promise(resolve=>pending.push({url,config,resolve}))}},
    'vue-router':{useRouter:()=>({push(){}})},
    '../画廊展厅/ArtworkInfiniteGrid.vue': {default: {}},
    ...Object.fromEntries(['ProfileCard','PreferenceSettings','UserManualViewer','ClubCard','AiSettings'].map(name=>[`./${name}.vue`,{__esModule:true,default:{}}])),
  })
  t.after(s.unmount)
  s.state.page.value=2; await settle()
  s.state.selectContent('blog'); await settle()
  assert.equal(s.state.page.value,1)
  assert.deepEqual(pending.at(-1).config.params,{type:'blog',page:1})
  pending[0].resolve({items:[{id:'wrong'}],total:1}); await settle()
  assert.deepEqual(s.state.items.value,[])
  pending.at(-1).resolve({items:[{id:'draft'}],total:1}); await settle()
  assert.equal(s.state.items.value[0].id,'draft')
  assert.deepEqual(s.state.contentLink({id:'draft'}),{path:'/write/blog',query:{draft:'draft'}})
  user.token='';user.isLoggedIn=false;await settle()
  assert.deepEqual(s.state.items.value,[])
})

test('task workspace reads actual project tasks and filters assignees without mutating projects', async t => {
  const get = t.mock.fn(async url => url === '/Project/my' ? [{ id: 'p', name: '项目' }] : [
    { id: '1', title: '我的任务', assigneeId: 'ME', categoryName: '待办' },
    { id: '2', title: '别人的任务', assigneeId: 'other', categoryName: '进行中' },
  ])
  const mounted = mountScript('views/任务栏/index.vue', {
    '@/utils/request': { __esModule: true, default: { get } },
    '@/stores/user': { useUserStore: () => ({ userId: 'me', userInfo: { id: 'me' } }) },
  })
  t.after(mounted.unmount); await settle()
  const s = mounted.state
  assert.deepEqual(get.mock.calls.map(c => c.arguments[0]), ['/Project/my', '/project/p/tasks'])
  assert.equal(s.filtered.value.length, 2)
  s.mine.value = true; assert.equal(s.filtered.value[0].id, '1')
  s.search.value = '不存在'; assert.equal(s.filtered.value.length, 0)
  assert.equal(s.tasks.value.length, 2)
})

test('check-in composer retains text until successful parent closure and blocks repeated submission', async t => {
  const events = []
  const props = vue.reactive({ visible: true, day: 1, member: { id: 1 }, submitting: false })
  const mounted = mountScript('views/活动中心/打卡中心/CheckInModal.vue', {
    '@/composables/useCos': { useCos: () => ({ uploadFile: async () => ({}), isUploading: vue.ref(false), progress: vue.ref(0) }) },
  }, props, (...args) => events.push(args))
  t.after(mounted.unmount)
  const s = mounted.state; s.text.value = '今日记录'; s.submit()
  assert.equal(events.length, 1); assert.equal(events[0][0], 'checkin'); assert.equal(s.text.value, '今日记录')
  props.submitting = true; s.submit(); s.close(); assert.equal(events.length, 1)
  props.submitting = false; assert.equal(s.text.value, '今日记录')
  props.visible = false; await settle(); assert.equal(s.text.value, '')
})

test('trade confirmation blocks duplicate purchases and keeps a failed request retryable', async t => {
  let resolve
  const purchase = t.mock.fn(() => new Promise(ok => resolve = ok))
  const mounted = mountScript('views/交易行/index.vue', {
    '../../api/trade': { TradeApi: { getAccountStatus: async () => ({ experience: 200 }), getStoreItems: async () => [], purchase } },
  })
  t.after(mounted.unmount); await settle()
  const s = mounted.state; s.openConfirm({ id: 1, name: '配额', cost: 100 })
  const saving = s.executeExchange(); await s.executeExchange(); s.closeDialog()
  assert.equal(purchase.mock.callCount(), 1); assert.equal(s.dialog.show, true)
  resolve({ isSuccess: false, message: '余额不足' }); await saving
  assert.equal(s.purchasing.value, false); assert.equal(s.dialog.show, true)
  assert.equal(s.notice.message, '余额不足')
})

test('survey UTC dates survive local editing and default duration is seven days', async t => {
  const raw = { title: '草稿', startTime: '2026-10-22T01:40:00', endTime: '2026-10-29T01:40:00Z', questions: [] }
  const api = loadScript('views/活动中心/问卷中心/api.ts', {
    '@/utils/request': { __esModule: true, default: { get: async () => raw } },
  })
  const detail = await api.getSurveyDetail(6)
  assert.equal(detail.startTime, '2026-10-22T01:40:00Z')
  assert.equal(detail.endTime, raw.endTime)
  const mocks = {
    'vue-router': { useRoute: () => ({ params: { id: '6' } }), useRouter: () => ({ push() {} }) },
    vuedraggable: { __esModule: true, default: {} }, './api': { getSurveyDetail: async () => detail },
  }
  const edited = mountScript('views/活动中心/问卷中心/SurveyEditor.vue', mocks)
  t.after(edited.unmount); await settle()
  assert.equal(edited.state.buildPayload().startTime, '2026-10-22T01:40:00.000Z')
  assert.equal(edited.state.buildPayload().endTime, '2026-10-29T01:40:00.000Z')
  mocks['vue-router'].useRoute = () => ({ params: {} })
  const created = mountScript('views/活动中心/问卷中心/SurveyEditor.vue', mocks)
  t.after(created.unmount)
  const payload = created.state.buildPayload()
  assert.equal(new Date(payload.endTime) - new Date(payload.startTime), 7 * 86400000)
})

test('character form blocks repeat saves while preserving the draft for retry', t => {
  const props = vue.reactive({ loading: false }), events = []
  const mounted = mountScript('views/柴圈板块/components/CharacterForm.vue', {
    'vue-router': { useRouter: () => ({ back() {} }) },
    '@/composables/useCos': { useCos: () => ({ uploadFile: async () => ({}) }) },
  }, props, (...args) => events.push(args))
  t.after(mounted.unmount)
  const s = mounted.state; s.form.title = '草稿'; s.form.description = '保留简介'
  s.handleSubmit(); assert.equal(events.length, 1)
  props.loading = true; s.handleSubmit(); assert.equal(events.length, 1)
  props.loading = false; s.handleSubmit(); assert.equal(events.length, 2)
  assert.equal(events[1][1].description, '保留简介')
})


test('direct note links wait for the directory and never keep another note editable during loading', async t => {
  const s = await historyLayout(t, { notes: [] })
  assert.equal(s.selectNote.mock.callCount(), 0)
  assert.equal(s.currentDisplayNote.value, null)
  s.notes.value = [{ id: 'a', title: '迟到的目录', type: 'note' }]
  await settle()
  assert.equal(s.currentDisplayNote.value.id, 'a')
  assert.equal(s.selectNote.mock.callCount(), 1)
  s.route.params.id = 'b'; await settle()
  assert.equal(s.currentDisplayNote.value, null)
  s.notes.value.push({ id: 'b', type: 'note' }); await settle()
  assert.equal(s.currentDisplayNote.value.id, 'b')
  assert.equal(s.selectNote.mock.callCount(), 2)
})


test('commit pagination retries the failed page and retains previous records', async t => {
  const requests = []
  let fail = false
  t.mock.method(globalThis, 'fetch', async url => {
    requests.push(String(url));
    return { ok: !fail, json: async () => Array.from({ length: 5 }, (_, id) => ({ sha: requests.length + '-' + id })) }
  })
  const m = mountScript('views/推送首页/components/updateLog.vue', { vue: { ...vue, onMounted() {} } })
  t.after(m.unmount)
  const s = m.state
  await s.fetchLogs(1)
  fail = true; s.loadMore(); await settle()
  assert.equal(s.logs.value.length, 5)
  assert.equal(s.currentPage.value, 1)
  assert.ok(s.error.value)
  fail = false; s.loadMore(); await settle()
  assert.match(requests.at(-1), /page=2$/)
  assert.equal(s.logs.value.length, 10)
  assert.equal(s.error.value, '')
})
