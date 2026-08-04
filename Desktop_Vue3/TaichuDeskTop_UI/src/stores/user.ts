import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { authApi, type UserInfo } from '../api/auth'

export const useUserStore = defineStore('user', () => {
  // ✅ 从 localStorage 恢复用户信息（页面刷新后依然存在）
  const savedUserInfo = localStorage.getItem('userInfo')
  const userInfo = ref<UserInfo | null>(savedUserInfo ? JSON.parse(savedUserInfo) : null)
  
  // 🌟 核心防线：用一个变量缓存当前正在进行的网络请求 Promise
  let currentRequestPromise: Promise<UserInfo> | null = null

  async function fetchUserInfo() {
    if (userInfo.value) return;

    if (currentRequestPromise) {
      try {
        await currentRequestPromise;
        return;
      } catch (error) {
        currentRequestPromise = null;
        return;
      }
    }

    try {
      currentRequestPromise = authApi.getUserInfo();
      const data = await currentRequestPromise;
      userInfo.value = data
      localStorage.setItem('userInfo', JSON.stringify(data))
    } catch (error) {
      console.error('用户信息同步失败', error)
    } finally {
      currentRequestPromise = null;
    }
  }

  // ✅ 设置用户信息（登录成功后调用）
  function setUser(user: UserInfo | null) {
    userInfo.value = user
    if (user) {
      localStorage.setItem('userInfo', JSON.stringify(user))
    } else {
      localStorage.removeItem('userInfo')
    }
  }

  // ✅ 清除用户信息（登出时使用）
  function clearUser() {
    userInfo.value = null
    localStorage.removeItem('userInfo')
  }

  // ============================================================
  // 权限相关
  // ============================================================

  const permissions = computed(() => userInfo.value?.permissions || [])

  const hasPermission = (permission: string) => {
    const perms = userInfo.value?.permissions || []
    return perms.includes('SuperAdmin') || perms.includes(permission)
  }

  const canManageSurvey = computed(() => {
    const perms = userInfo.value?.permissions || []
    return perms.includes('SuperAdmin') || perms.includes('Survey_Manage')
  })

  return { 
    userInfo, 
    fetchUserInfo,
    setUser,
    permissions,
    hasPermission,
    canManageSurvey,
    clearUser,
  }
})