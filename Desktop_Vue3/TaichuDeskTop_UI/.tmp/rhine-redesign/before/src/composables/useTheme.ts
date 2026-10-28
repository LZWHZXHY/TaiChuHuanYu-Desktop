import { ref } from 'vue'

export type ThemeMode = 'light' | 'dark' | 'rhine' | 'system'
const key = 'taichu-theme'
const mode = ref<ThemeMode>('system')
const valid = (value: unknown): value is ThemeMode => ['light', 'dark', 'rhine', 'system'].includes(String(value))
let preference: MediaQueryList | undefined

function apply() {
  // 莱茵主题是独立的第三套皮肤，不参与明暗推断
  if (mode.value === 'rhine') { document.documentElement.dataset.theme = 'rhine'; return }
  const dark = mode.value === 'dark' || (mode.value === 'system' && preference?.matches)
  document.documentElement.dataset.theme = dark ? 'dark' : 'light'
}

export function setupTheme() {
  preference = window.matchMedia('(prefers-color-scheme: dark)')
  try { const saved = localStorage.getItem(key); if (valid(saved)) mode.value = saved } catch { /* 浏览器禁止存储时仍可切换。 */ }
  apply()
  const sync = (event: StorageEvent) => {
    if (event.key === key || event.key === null) { mode.value = valid(event.newValue) ? event.newValue : 'system'; apply() }
  }
  preference.addEventListener('change', apply)
  window.addEventListener('storage', sync)
  return () => { preference?.removeEventListener('change', apply); window.removeEventListener('storage', sync) }
}

export function useTheme() {
  function setTheme(value: ThemeMode) {
    mode.value = value
    try { localStorage.setItem(key, value) } catch { /* 本次会话继续使用所选主题。 */ }
    apply()
  }
  return { mode, setTheme }
}
