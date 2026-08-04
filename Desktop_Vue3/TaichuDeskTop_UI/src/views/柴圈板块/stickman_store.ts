// ==================== 追加：Pinia Store ====================
// ==================== 导入依赖 ====================
import { defineStore } from 'pinia'
import { ref } from 'vue'
// ✅ 关键：从同目录的 stickman.ts 导入所有需要的类型和 API
import { stickmanApi, type StickmanCharacter, type CreateStickmanDto, type UpdateStickmanDto } from './stickman'

export const useStickmanStore = defineStore('stickman', () => {
  // ---------- 状态 ----------
  const characters = ref<StickmanCharacter[]>([])      // 列表数据
  const currentCharacter = ref<StickmanCharacter | null>(null)  // 当前查看的角色
  const myCharacters = ref<StickmanCharacter[]>([])    // 我的角色列表
  const loading = ref(false)                           // 加载状态
  const total = ref(0)                                 // 总数（用于分页）

  // ---------- 方法 ----------
  // 获取列表
  async function fetchList(params?: { page?: number; pageSize?: number; keyword?: string; tag?: string }) {
    loading.value = true
    try {
      const res = await stickmanApi.getList(params)
      characters.value = res.items
      total.value = res.total
    } catch (error) {
      console.error('获取角色列表失败:', error)
    } finally {
      loading.value = false
    }
  }

  // 获取详情
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

  // 创建角色
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

  // 更新角色
  async function updateCharacter(id: string, data: UpdateStickmanDto) {
    loading.value = true
    try {
      const res = await stickmanApi.update(id, data)
      // 如果当前查看的角色正好是更新的这个，也更新 currentCharacter
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

  // 删除角色
  async function deleteCharacter(id: string) {
    loading.value = true
    try {
      await stickmanApi.delete(id)
      // 从列表中移除
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

  // 获取我的角色
  async function fetchMyCharacters(status?: string) {
    loading.value = true
    try {
      const res = await stickmanApi.getMyCharacters({ status })
      myCharacters.value = res
    } catch (error) {
      console.error('获取我的角色失败:', error)
    } finally {
      loading.value = false
    }
  }

  // 点赞切换
  async function toggleLike(id: string) {
    try {
      await stickmanApi.toggleLike(id)
      // 乐观更新：本地先改，后端成功后保持
      const target = characters.value.find(c => c.id === id)
      if (target) {
        // 这里暂时只做本地更新，后续后端返回真实数据后再调整
      }
    } catch (error) {
      console.error('点赞失败:', error)
    }
  }

  // 收藏切换
  async function toggleFavorite(id: string) {
    try {
      await stickmanApi.toggleFavorite(id)
    } catch (error) {
      console.error('收藏失败:', error)
    }
  }

  // 清空当前角色
  function clearCurrent() {
    currentCharacter.value = null
  }

  // ---------- 导出 ----------
  return {
    // 状态
    characters,
    currentCharacter,
    myCharacters,
    loading,
    total,
    // 方法
    fetchList,
    fetchDetail,
    createCharacter,
    updateCharacter,
    deleteCharacter,
    fetchMyCharacters,
    toggleLike,
    toggleFavorite,
    clearCurrent,
  }
})