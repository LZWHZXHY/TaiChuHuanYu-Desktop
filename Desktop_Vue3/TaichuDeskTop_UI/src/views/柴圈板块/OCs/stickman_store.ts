// ==================== Pinia Store ====================
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { stickmanApi, type StickmanCharacter, type CreateStickmanDto, type UpdateStickmanDto } from './stickman'

export const useStickmanStore = defineStore('stickman', () => {
  // ---------- 状态 ----------
  const characters = ref<StickmanCharacter[]>([])
  const currentCharacter = ref<StickmanCharacter | null>(null)
  const myCharacters = ref<StickmanCharacter[]>([])
  const loading = ref(false)
  const total = ref(0)

  // ---------- 方法 ----------
  async function fetchList(params?: { page?: number; pageSize?: number; keyword?: string; tag?: string }) {
    loading.value = true
    try {
      const res = await stickmanApi.getList(params)
      characters.value = res?.items ?? []
      total.value = res?.total ?? 0
    } catch (error) {
      console.error('获取角色列表失败:', error)
      characters.value = []
      total.value = 0
    } finally {
      loading.value = false
    }
  }

  async function fetchDetail(id: string) {
    loading.value = true
    try {
      const res = await stickmanApi.getDetail(id)
      currentCharacter.value = res
      return res
    } catch (error) {
      console.error('获取角色详情失败:', error)
      throw error
    } finally {
      loading.value = false
    }
  }

  async function createCharacter(data: CreateStickmanDto) {
    loading.value = true
    try {
      const res = await stickmanApi.create(data)
      return res
    } catch (error) {
      console.error('创建角色失败:', error)
      throw error
    } finally {
      loading.value = false
    }
  }

  async function updateCharacter(id: string, data: UpdateStickmanDto) {
    loading.value = true
    try {
      const res = await stickmanApi.update(id, data)
      if (currentCharacter.value?.id === id) {
        currentCharacter.value = res
      }
      return res
    } catch (error) {
      console.error('更新角色失败:', error)
      throw error
    } finally {
      loading.value = false
    }
  }

  async function deleteCharacter(id: string) {
    loading.value = true
    try {
      await stickmanApi.delete(id)
      characters.value = characters.value.filter(c => c.id !== id)
      myCharacters.value = myCharacters.value.filter(c => c.id !== id)
      if (currentCharacter.value?.id === id) {
        currentCharacter.value = null
      }
    } catch (error) {
      console.error('删除角色失败:', error)
      throw error
    } finally {
      loading.value = false
    }
  }

  async function fetchMyCharacters(status?: string) {
    loading.value = true
    try {
      const res = await stickmanApi.getMyCharacters({ status })
      myCharacters.value = res ?? []
    } catch (error) {
      console.error('获取我的角色失败:', error)
      myCharacters.value = []
    } finally {
      loading.value = false
    }
  }

  function clearCurrent() {
    currentCharacter.value = null
  }

  // ---------- 导出 ----------
  return {
    characters,
    currentCharacter,
    myCharacters,
    loading,
    total,
    fetchList,
    fetchDetail,
    createCharacter,
    updateCharacter,
    deleteCharacter,
    fetchMyCharacters,
    clearCurrent,
  }
})