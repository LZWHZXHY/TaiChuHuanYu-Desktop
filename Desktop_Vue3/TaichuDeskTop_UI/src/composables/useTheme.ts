import { ref } from 'vue'

export type ThemeMode = 'light' | 'dark' | 'system'
const key = 'taichu-theme'
const mode = ref<ThemeMode>('system')
// 原莱茵皮肤已成为全站设计，旧偏好继续对应浅色，避免升级后突然变暗。
const normalize = (value: unknown): ThemeMode => value === 'rhine' || value === 'light' ? 'light' : value === 'dark' ? 'dark' : 'system'
let preference: MediaQueryList | undefined

function apply() {
  const dark = mode.value === 'dark' || (mode.value === 'system' && preference?.matches)
  document.documentElement.dataset.theme = dark ? 'dark' : 'light'
}

export function setupTheme() {
  preference = window.matchMedia('(prefers-color-scheme: dark)')
  try { mode.value = normalize(localStorage.getItem(key)) } catch { /* 浏览器禁止存储时仍可切换。 */ }
  apply()
  const sync = (event: StorageEvent) => {
    if (event.key === key || event.key === null) { mode.value = normalize(event.newValue); apply() }
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
