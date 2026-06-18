import { defineStore } from 'pinia'
import { ref } from 'vue'
import { authApi, type UserInfo } from '../api/auth'

export const useUserStore = defineStore('user', () => {
  const userInfo = ref<UserInfo | null>(null)
  
  // 🌟 核心防线：用一个变量缓存当前正在进行的网络请求 Promise
  let currentRequestPromise: Promise<UserInfo> | null = null

  async function fetchUserInfo() {
    // 1. 如果 userInfo 已经有值了，说明之前彻底请求成功过，直接返回，不再惊动后端
    if (userInfo.value) return;

    // 2. 如果当前正有一个请求在飞（并发冲突发生），直接等待那个正在飞的请求，绝不发第二个！
    if (currentRequestPromise) {
      try {
        await currentRequestPromise;
        return;
      } catch (error) {
        // 如果前一个失败了，允许清除锁后面重试
        currentRequestPromise = null;
        return;
      }
    }

    // 3. 正常发起请求，并挂上全局锁
    try {
      currentRequestPromise = authApi.getUserInfo();
      
      // 等待请求结果
      const data = await currentRequestPromise;
      userInfo.value = data
    } catch (error) {
      console.error('用户信息同步失败', error)
    } finally {
      // 4. 无论成功还是失败，请求结束后把锁解开
      currentRequestPromise = null;
    }
  }

  return { userInfo, fetchUserInfo }
})