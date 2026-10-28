import type { Pinia } from 'pinia'
import type { Router } from 'vue-router'
import { useUserStore } from '@/stores/user'
import { authStorageKeys, getStoredToken } from '@/utils/authStorage'
import { bus } from '@/utils/bus'

export const LOGIN_PATH = '/LoginRegister'

export function setupAuthSession(pinia: Pinia, router: Router) {
  const userStore = useUserStore(pinia)

  const onExpired = (requestToken: string) => {
    if (requestToken !== getStoredToken()) return
    userStore.logout()
    if (router.currentRoute.value.meta.requiresAuth !== false && router.currentRoute.value.path !== LOGIN_PATH) {
      void router.replace(LOGIN_PATH)
    }
  }

  const onStorage = (event: StorageEvent) => {
    if (event.storageArea !== localStorage) return
    if (event.key !== null && !authStorageKeys.some(key => key === event.key)) return
    const previousToken = userStore.token
    userStore.syncFromStorage()
    // 其他标签页切换账号后，重新加载以清理各业务模块的账号缓存。
    if (previousToken !== userStore.token) window.location.reload()
  }

  bus.on('auth-expired', onExpired)
  window.addEventListener('storage', onStorage)
  return () => {
    bus.off('auth-expired', onExpired)
    window.removeEventListener('storage', onStorage)
  }
}
