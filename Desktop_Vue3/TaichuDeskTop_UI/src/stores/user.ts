import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { authApi, type UserInfo } from '../api/auth'

export const useUserStore = defineStore('user', () => {
  const userInfo = ref<UserInfo | null>(null)

  async function fetchUserInfo() {
    try {
      const data = await authApi.getUserInfo()
      userInfo.value = data
    } catch (error) {
      console.error('用户信息同步失败', error)
    }
  }

  return { userInfo, fetchUserInfo }
})