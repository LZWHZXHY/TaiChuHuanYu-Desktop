import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { CanceledError } from 'axios'
import { authApi, type LoginCredentials, type UserInfo } from '../api/auth'
import { getStoredToken, getTokenUserId, persistToken, persistUser, readStoredUser } from '../utils/authStorage'

export const useUserStore = defineStore('user', () => {
  const token = ref(getStoredToken())
  const userInfo = ref<UserInfo | null>(readStoredUser())
  const isLoggedIn = computed(() => Boolean(token.value))
  const userId = computed(() => userInfo.value?.id || getTokenUserId(token.value))
  const permissions = computed(() => isLoggedIn.value ? userInfo.value?.permissions || [] : [])

  let sessionVersion = 0
  let loginAttempt = 0
  let profileRequest: Promise<UserInfo | null> | null = null

  function setUser(user: UserInfo | null) {
    userInfo.value = token.value ? user : null
    persistUser(userInfo.value)
  }

  function setSession(nextToken: string, user: UserInfo | null = null) {
    sessionVersion++
    profileRequest = null
    token.value = nextToken
    persistToken(nextToken)
    setUser(user)
  }

  function logout() {
    setSession('')
  }

  async function login(credentials: LoginCredentials) {
    const attempt = ++loginAttempt
    const version = sessionVersion
    const result = await authApi.login(credentials)
    if (attempt !== loginAttempt || version !== sessionVersion) throw new CanceledError()
    setSession(result.token, result.user ?? null)
    return result
  }

  function fetchUserInfo(force = false): Promise<UserInfo | null> {
    if (!token.value) return Promise.resolve(null)
    if (profileRequest) return profileRequest
    if (!force && userInfo.value) return Promise.resolve(userInfo.value)

    const version = sessionVersion
    const requestToken = token.value
    const pending = authApi.getUserInfo()
      .then(profile => {
        // 退出或切换账号后，旧响应不能恢复旧用户或覆盖新账号。
        if (version !== sessionVersion || requestToken !== getStoredToken()) return null
        setUser({
          ...userInfo.value,
          ...profile,
          id: userInfo.value?.id || getTokenUserId(requestToken),
        })
        return userInfo.value
      })
      .catch(error => {
        console.error('用户信息同步失败', error)
        return null
      })
      .finally(() => {
        if (profileRequest === pending) profileRequest = null
      })

    profileRequest = pending
    return pending
  }

  function syncFromStorage() {
    const storedToken = getStoredToken()
    if (storedToken !== token.value) {
      sessionVersion++
      profileRequest = null
      token.value = storedToken
    }
    userInfo.value = readStoredUser()
  }

  const hasPermission = (permission: string) =>
    permissions.value.includes('SuperAdmin') || permissions.value.includes(permission)

  const canManageSurvey = computed(() => hasPermission('Survey_Manage'))

  return {
    token, userInfo, userId, isLoggedIn, permissions, canManageSurvey,
    login, logout, setSession, setUser, fetchUserInfo, syncFromStorage, hasPermission,
    clearUser: () => setUser(null),
  }
})
