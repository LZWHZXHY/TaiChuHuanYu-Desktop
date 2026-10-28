import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { test } from 'node:test'
import ts from 'typescript'
import { ref } from 'vue'
function theme(t, stored = null, blocked = false) {
  const system = { matches: false, addEventListener: (_, fn) => listeners.set('system', fn), removeEventListener: () => listeners.delete('system') }
  const listeners = new Map(), writes = []
  const window = { matchMedia: () => system, addEventListener: (_, fn) => listeners.set('storage', fn), removeEventListener: () => listeners.delete('storage') }
  const document = { documentElement: { dataset: {} } }
  const localStorage = { getItem: () => { if (blocked) throw Error('blocked'); return stored }, setItem: (...args) => { if(blocked) throw Error('blocked'); writes.push(args) } }
  const source = readFileSync(new URL('../src/composables/useTheme.ts', import.meta.url), 'utf8')
  const exports = {}
  new Function('require', 'exports', 'window', 'document', 'localStorage', ts.transpileModule(source, {compilerOptions:{module:ts.ModuleKind.CommonJS}}).outputText)(() => ({ref}), exports, window, document, localStorage)
  const dispose = exports.setupTheme(); t.after(dispose)
  return { ...exports.useTheme(), system, listeners, writes, document, dispose }
}
test('theme follows system until user chooses a persistent preference', t => {
  const s=theme(t)
  assert.equal(s.document.documentElement.dataset.theme,'light')
  s.system.matches=true; s.listeners.get('system')()
  assert.equal(s.document.documentElement.dataset.theme,'dark')
  s.setTheme('light'); s.listeners.get('system')()
  assert.equal(s.document.documentElement.dataset.theme,'light')
  assert.deepEqual(s.writes.at(-1),['taichu-theme','light'])
  s.setTheme('system'); assert.equal(s.document.documentElement.dataset.theme,'dark')
})
test('theme restores, syncs across tabs, handles storage clearing and disposes listeners', t => {
  const s=theme(t,'dark')
  assert.equal(s.document.documentElement.dataset.theme,'dark')
  s.listeners.get('storage')({key:'unrelated',newValue:'light'})
  assert.equal(s.mode.value,'dark')
  s.listeners.get('storage')({key:'taichu-theme',newValue:'light'})
  assert.equal(s.document.documentElement.dataset.theme,'light')
  s.listeners.get('storage')({key:null,newValue:null}); assert.equal(s.mode.value,'system')
  s.dispose(); assert.equal(s.listeners.size,0)
})
test('theme remains usable when browser storage is blocked',t=>{
  const s=theme(t,null,true)
  s.setTheme('dark'); assert.equal(s.document.documentElement.dataset.theme,'dark')
})

test('legacy Rhine preference keeps the redesigned light mode, including cross-tab updates', t => {
  const s = theme(t, 'rhine')
  assert.equal(s.mode.value, 'light')
  s.system.matches = true
  s.listeners.get('system')()
  assert.equal(s.document.documentElement.dataset.theme, 'light')
  s.setTheme('dark')
  s.listeners.get('storage')({ key: 'taichu-theme', newValue: 'rhine' })
  assert.equal(s.document.documentElement.dataset.theme, 'light')
})

test('first-paint theme matches legacy and current saved preferences', () => {
  const html = readFileSync(new URL('../index.html', import.meta.url), 'utf8')
  const bootstrap = html.match(/<script>([\s\S]*?)<\/script>/)[1]
  for (const [saved, systemDark, expected] of [['rhine', true, 'light'], ['light', true, 'light'], ['dark', false, 'dark'], ['system', true, 'dark'], [null, false, 'light']]) {
    const document = { documentElement: { dataset: {} } }
    new Function('document', 'localStorage', 'matchMedia', bootstrap)(document, { getItem: () => saved }, () => ({ matches: systemDark }))
    assert.equal(document.documentElement.dataset.theme, expected)
  }
})


test('decorative motion pauses offscreen and in hidden tabs, rebinds targets and cleans up', () => {
  let mount, unmount, change, intersect, visibility, disconnected = false
  const observed = new Set(), document = { hidden: false, addEventListener: (_, fn) => { visibility = fn }, removeEventListener: () => { visibility = undefined } }
  class Observer { constructor(fn) { intersect = fn } observe(el) { observed.add(el) } unobserve(el) { observed.delete(el) } disconnect() { disconnected = true; observed.clear() } }
  const exports = {}, target = ref({ id: 'header' })
  const source = readFileSync(new URL('../src/composables/useVisibleMotion.ts', import.meta.url), 'utf8')
  new Function('require', 'exports', 'document', 'IntersectionObserver', ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText)(() => ({ ref, onMounted: fn => { mount = fn }, onUnmounted: fn => { unmount = fn }, watch: (_, fn) => { change = fn } }), exports, document, Observer)
  const active = exports.useVisibleMotion(target); mount(); assert.equal(active.value, false)
  intersect([{ isIntersecting: true }]); assert.equal(active.value, true)
  document.hidden = true; visibility(); assert.equal(active.value, false)
  document.hidden = false; visibility(); assert.equal(active.value, true)
  intersect([{ isIntersecting: false }]); assert.equal(active.value, false)
  const previous = target.value; target.value = { id: 'replacement' }; change(target.value, previous)
  assert.equal(observed.has(previous), false); assert.equal(observed.has(target.value), true); assert.equal(active.value, false)
  intersect([{ isIntersecting: true }]); assert.equal(active.value, true)
  change(undefined, target.value); assert.equal(active.value, false)
  unmount(); assert.equal(disconnected, true); assert.equal(visibility, undefined)
})
