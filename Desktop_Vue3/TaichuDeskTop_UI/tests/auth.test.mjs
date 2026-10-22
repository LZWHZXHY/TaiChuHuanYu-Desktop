import assert from 'node:assert/strict'
import { after, before, beforeEach, test } from 'node:test'
import { fileURLToPath } from 'node:url'
import { createServer } from 'vite'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { AxiosError, CanceledError } from 'axios'

const root = fileURLToPath(new URL('../', import.meta.url))
let server, createHttpClient, getErrorMessage, useUserStore, authApi, storage, setupAuthSession, bus
let originalLogin, originalProfile

before(async () => {
  // Use the project's TypeScript transform; no backend, browser, or extra test dependency is needed.
  server = await createServer({
    root, configFile: false, envDir: false,
    resolve: { alias: { '@': `${root}src` } },
    server: { middlewareMode: true, hmr: false, ws: false, watch: null, fs: { allow: [root] } },
    optimizeDeps: { noDiscovery: true, include: [] },
  })
  ;({ createHttpClient } = await server.ssrLoadModule('/src/utils/httpClient.ts'))
  ;({ getErrorMessage } = await server.ssrLoadModule('/src/utils/apiError.ts'))
  ;({ useUserStore } = await server.ssrLoadModule('/src/stores/user.ts'))
  ;({ authApi } = await server.ssrLoadModule('/src/api/auth.ts'))
  storage = await server.ssrLoadModule('/src/utils/authStorage.ts')
  ;({ setupAuthSession } = await server.ssrLoadModule('/src/router/auth.ts'))
  ;({ bus } = await server.ssrLoadModule('/src/utils/bus.ts'))
  originalLogin = authApi.login
  originalProfile = authApi.getUserInfo
})

beforeEach(() => {
  const values = new Map()
  globalThis.localStorage = {
    getItem: key => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, String(value)),
    removeItem: key => values.delete(key),
    clear: () => values.clear(),
  }
  globalThis.window = Object.assign(new EventTarget(), { location: { reload() {} } })
  setActivePinia(createPinia())
  authApi.login = originalLogin
  authApi.getUserInfo = originalProfile
})

after(async () => { await server?.close() })

const user = { id: 'user-a', username: 'Alice', email: 'a@example.test', level: 1, permissions: ['SuperAdmin'] }
const deferred = () => {
  let resolve, reject
  const promise = new Promise((ok, fail) => { resolve = ok; reject = fail })
  return { promise, resolve, reject }
}
const response = (config, data) => ({ config, data, status: 200, statusText: 'OK', headers: {} })
const failure = (config, status, data) => new AxiosError('Request failed', 'ERR_BAD_RESPONSE', config, null, {
  config, status, data, statusText: 'Error', headers: {},
})
const clientHarness = () => {
  const state = { token: 'session-a', expired: [], notices: [] }
  const client = createHttpClient({
    baseURL: '/api', getToken: () => state.token,
    onUnauthorized: token => { state.expired.push(token); state.token = '' },
    onError: notice => state.notices.push(notice),
  })
  return { client, state }
}

test('HTTP methods retain data-only responses, headers, params, and request bodies', async () => {
  const { client } = clientHarness()
  for (const method of ['get', 'post', 'put', 'patch', 'delete']) {
    const payload = { data: ['business envelope'], method }
    const config = {
      params: { page: 2 }, headers: { 'X-Custom': 'kept' },
      adapter: async config => {
        assert.equal(config.headers.get('Authorization'), 'Bearer session-a')
        assert.equal(config.headers.get('X-Custom'), 'kept')
        assert.deepEqual(config.params, { page: 2 })
        if (['post', 'put', 'patch'].includes(method)) assert.deepEqual(JSON.parse(config.data), { name: 'saved' })
        return response(config, payload)
      },
    }
    const result = ['get', 'delete'].includes(method)
      ? await client[method]('/items', config)
      : await client[method]('/items', { name: 'saved' }, config)
    assert.deepEqual(result, payload)
  }
})

test('binary download bodies are not wrapped or transformed', async () => {
  const { client } = clientHarness()
  const blob = new Blob(['download'])
  assert.equal(await client.get('/download', {
    responseType: 'blob', adapter: async config => response(config, blob),
  }), blob)
})

test('bad login credentials settle normally without clearing an existing session or showing a global error', async () => {
  const { client, state } = clientHarness()
  let finalized = false
  await assert.rejects(client.post('/Auth/login', {}, {
    skipAuth: true, silentError: true,
    adapter: async config => {
      assert.equal(config.headers.get('Authorization'), undefined)
      throw failure(config, 401, { message: '密码错误' })
    },
  }).finally(() => { finalized = true }), error => getErrorMessage(error) === '密码错误')
  assert.equal(finalized, true)
  assert.equal(state.token, 'session-a')
  assert.equal(state.expired.length, 0)
  assert.equal(state.notices.length, 0)
})

test('concurrent 401s reject all callers but expire and notify only once', async () => {
  const { client, state } = clientHarness()
  const adapter = async config => { throw failure(config, 401, {}) }
  const results = await Promise.allSettled([client.get('/a', { adapter }), client.get('/b', { adapter })])
  assert.deepEqual(results.map(result => result.status), ['rejected', 'rejected'])
  assert.deepEqual(state.expired, ['session-a'])
  assert.equal(state.notices.length, 1)
})

test('a delayed 401 from the previous account cannot sign out the new account', async () => {
  const { client, state } = clientHarness()
  await assert.rejects(client.get('/old', { adapter: async config => {
    state.token = 'session-b'
    throw failure(config, 401, {})
  } }))
  assert.equal(state.token, 'session-b')
  assert.equal(state.expired.length, 0)
})

test('403 and validation errors preserve server messages and keep the session', async () => {
  const { client, state } = clientHarness()
  await assert.rejects(client.get('/denied', { adapter: async config => {
    throw failure(config, 403, { message: '配额已用完' })
  } }), error => error.friendlyMessage === '配额已用完')
  assert.equal(state.token, 'session-a')
  assert.equal(state.notices[0].msg, '配额已用完')
  assert.equal(getErrorMessage(failure({}, 400, { title: 'Validation', errors: { name: ['请输入名称'] } })), '请输入名称')
  assert.equal(getErrorMessage(failure({}, 500, '暂时不可用')), '暂时不可用')
})

test('request cancellation does not show an error notification', async () => {
  const { client, state } = clientHarness()
  await assert.rejects(client.get('/cancel', { adapter: async () => { throw new CanceledError() } }))
  assert.equal(state.notices.length, 0)
})

test('invalid persisted user data cannot crash startup or grant permissions', () => {
  localStorage.setItem('token', 'session-a')
  for (const data of ['undefined', '{', 'null', '[]', '42']) {
    localStorage.setItem('userInfo', data)
    assert.equal(storage.readStoredUser(), null)
  }
  localStorage.setItem('userInfo', JSON.stringify({ ...user, permissions: 'SuperAdmin' }))
  assert.deepEqual(storage.readStoredUser().permissions, [])
  localStorage.removeItem('token')
  assert.equal(useUserStore().userInfo, null)
  assert.equal(useUserStore().hasPermission('SuperAdmin'), false)
})

test('JWT identity fallback supports base64url and malformed tokens', () => {
  const encoded = Buffer.from(JSON.stringify({ nameid: 'user-id' })).toString('base64url')
  assert.equal(storage.getTokenUserId(`header.${encoded}.signature`), 'user-id')
  assert.equal(storage.getTokenUserId('broken'), '')
})

test('login, refresh restoration, and logout share one persisted session', async () => {
  authApi.login = async () => ({ token: 'session-a', user })
  const store = useUserStore()
  await store.login({ identifier: 'Alice', password: 'test' })
  assert.equal(store.isLoggedIn, true)
  assert.equal(localStorage.getItem('userId'), user.id)
  const restored = useUserStore(createPinia())
  assert.equal(restored.userInfo.username, 'Alice')
  assert.equal(restored.canManageSurvey, true)
  localStorage.setItem('unrelated-setting', 'keep')
  store.logout()
  for (const key of storage.authStorageKeys) assert.equal(localStorage.getItem(key), null)
  assert.equal(store.userInfo, null)
  assert.equal(store.hasPermission('SuperAdmin'), false)
  assert.equal(localStorage.getItem('unrelated-setting'), 'keep')
})

test('profile refresh is deduplicated and preserves ID/permissions omitted by /User/me', async () => {
  const store = useUserStore()
  store.setSession('session-a', user)
  const pending = deferred()
  let calls = 0
  authApi.getUserInfo = () => { calls++; return pending.promise }
  const first = store.fetchUserInfo(true)
  const second = store.fetchUserInfo(true)
  assert.equal(calls, 1)
  pending.resolve({ username: 'Updated', email: user.email, level: 2, experience: 100 })
  await Promise.all([first, second])
  assert.equal(store.userInfo.id, user.id)
  assert.equal(store.userInfo.experience, 100)
  assert.equal(store.hasPermission('SuperAdmin'), true)
  assert.equal(localStorage.getItem('username'), 'Updated')
  await store.fetchUserInfo()
  assert.equal(calls, 1)
})

test('late profile responses do not restore a signed-out user', async () => {
  const store = useUserStore()
  store.setSession('session-a', user)
  const pending = deferred()
  authApi.getUserInfo = () => pending.promise
  const request = store.fetchUserInfo(true)
  store.logout()
  pending.resolve(user)
  await request
  assert.equal(store.userInfo, null)
  assert.equal(localStorage.getItem('userInfo'), null)
})

test('late profile responses do not overwrite the new account or release its pending request', async () => {
  const store = useUserStore()
  store.setSession('session-a', user)
  const old = deferred(), current = deferred()
  let calls = 0
  authApi.getUserInfo = () => (++calls === 1 ? old.promise : current.promise)
  const first = store.fetchUserInfo(true)
  store.setSession('session-b', { ...user, id: 'user-b', username: 'Bob', permissions: [] })
  const second = store.fetchUserInfo(true)
  old.resolve(user)
  await first
  const third = store.fetchUserInfo(true)
  assert.equal(calls, 2)
  current.resolve({ username: 'Bob', email: 'b@example.test', level: 3 })
  await Promise.all([second, third])
  assert.equal(store.userInfo.id, 'user-b')
  assert.equal(store.hasPermission('SuperAdmin'), false)
})

test('failed profile loads release the request so a retry can succeed', async () => {
  const store = useUserStore()
  store.setSession('session-a', user)
  authApi.getUserInfo = async () => { throw new Error('offline') }
  const originalError = console.error
  console.error = () => {}
  try { assert.equal(await store.fetchUserInfo(true), null) } finally { console.error = originalError }
  authApi.getUserInfo = async () => ({ ...user, username: 'Retried' })
  await store.fetchUserInfo(true)
  assert.equal(store.userInfo.username, 'Retried')
})

test('logout during a pending login prevents that response from restoring a session', async () => {
  const store = useUserStore()
  const pending = deferred()
  authApi.login = () => pending.promise
  const request = store.login({ identifier: 'Alice', password: 'test' })
  store.logout()
  pending.resolve({ token: 'session-a', user })
  await assert.rejects(request)
  assert.equal(store.isLoggedIn, false)
})

test('failed login preserves the current user and cached identity', async () => {
  const store = useUserStore()
  store.setSession('session-a', user)
  authApi.login = async () => { throw failure({}, 401, { message: '密码错误' }) }
  await assert.rejects(store.login({ identifier: 'wrong', password: 'wrong' }))
  assert.equal(store.token, 'session-a')
  assert.equal(store.userInfo.id, user.id)
  assert.equal(localStorage.getItem('userId'), user.id)
})

test('cross-tab account changes refresh identity and discard the old page state', () => {
  const pinia = createPinia()
  const store = useUserStore(pinia)
  store.setSession('session-a', user)
  let reloads = 0
  window.location.reload = () => { reloads++ }
  const dispose = setupAuthSession(pinia, {})
  try {
    localStorage.setItem('token', 'session-b')
    localStorage.setItem('userInfo', JSON.stringify({ ...user, id: 'user-b', permissions: [] }))
    const event = Object.assign(new Event('storage'), { key: 'token', storageArea: localStorage })
    window.dispatchEvent(event)
    assert.equal(store.userId, 'user-b')
    assert.equal(store.hasPermission('SuperAdmin'), false)
    assert.equal(reloads, 1)
    window.dispatchEvent(event)
    assert.equal(reloads, 1)
  } finally { dispose() }
})

test('session expiration clears state and navigates to the real login route', async () => {
  const pinia = createPinia()
  const store = useUserStore(pinia)
  store.setSession('session-a', user)
  const router = createRouter({ history: createMemoryHistory(), routes: [
    { path: '/', component: {} }, { path: '/LoginRegister', component: {} },
  ] })
  await router.push('/')
  const dispose = setupAuthSession(pinia, router)
  try {
    bus.emit('auth-expired', 'old-session')
    assert.equal(store.isLoggedIn, true)
    bus.emit('auth-expired', 'session-a')
    await new Promise(resolve => setImmediate(resolve))
    assert.equal(router.currentRoute.value.path, '/LoginRegister')
    assert.equal(store.isLoggedIn, false)
    assert.equal(store.userInfo, null)
  } finally { dispose() }
})
