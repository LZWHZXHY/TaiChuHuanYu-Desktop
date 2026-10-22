import { defineStore } from 'pinia'
import { ref } from 'vue'
import { battleApi, type Battle } from './battle_api'

export const useBattleStore = defineStore('battle', () => {
  // 状态
  const currentBattle = ref<Battle | null>(null)
  const loading = ref(false)
  const myBattles = ref<Battle[]>([])
  const myBattlesLoading = ref(false)
  let detailRequest = 0

  // 获取详情
  const fetchDetail = async (id: string) => {
    const request = ++detailRequest
    if (currentBattle.value?.id !== id) currentBattle.value = null
    loading.value = true
    try {
      const res = await battleApi.detail(id)
      // ✅ request 已经返回 data，直接赋值
      if (request === detailRequest) currentBattle.value = res
      return res
    } catch (error) {
      if (request === detailRequest) currentBattle.value = null
      throw error
    } finally {
      if (request === detailRequest) loading.value = false
    }
  }

  // 获取我的约战
  const fetchMyBattles = async (status?: string) => {
    myBattlesLoading.value = true
    try {
      const res = await battleApi.my({ status })
      // ✅ request 已经返回 data，直接赋值
      myBattles.value = res
      return res
    } finally {
      myBattlesLoading.value = false
    }
  }

  // 创建约战
  const create = async (data: any) => {
    const res = await battleApi.create(data)
    // ✅ request 已经返回 data，直接返回
    return res
  }

  // 更新约战
  const update = async (id: string, data: any) => {
    const res = await battleApi.update(id, data)
    // ✅ request 已经返回 data，直接使用 res
    if (currentBattle.value?.id === id) {
      currentBattle.value = { ...currentBattle.value, ...res }
    }
    return res
  }

  // 刷新当前约战
  const refreshCurrent = async () => {
    if (currentBattle.value?.id) {
      await fetchDetail(currentBattle.value.id)
    }
  }

  // 清空
  const clear = () => {
    detailRequest++
    loading.value = false
    currentBattle.value = null
  }

  return {
    currentBattle,
    loading,
    myBattles,
    myBattlesLoading,
    fetchDetail,
    fetchMyBattles,
    create,
    update,
    refreshCurrent,
    clear,
  }
})
