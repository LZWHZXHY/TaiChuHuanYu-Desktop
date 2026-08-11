// src/composables/世界观管理/useCardQuota.ts
import { ref, computed, watch, type Ref } from 'vue'
import { worldApi } from '@/api/worldApi'

export function useCardQuota(projectId: Ref<string> | string) {
  // 确保 projectId 是 ref
  const projectIdRef = typeof projectId === 'string' ? ref(projectId) : projectId

  const quotaInfo = ref({
    currentCount: 0,
    maxCount: 0,
    remaining: 0,
    canAdd: true,
  })
  const loading = ref(false)

  const loadQuota = async () => {
    const id = projectIdRef.value
    if (!id) return
    loading.value = true
    try {
      const { data } = await worldApi.canAddCard(id)
      quotaInfo.value = {
        currentCount: data.currentCount,
        maxCount: data.maxCount,
        remaining: data.maxCount - data.currentCount,
        canAdd: data.canAdd,
      }
    } catch (error) {
      console.error('获取卡片配额失败:', error)
    } finally {
      loading.value = false
    }
  }

  const quotaStatus = computed(() => {
    if (quotaInfo.value.remaining <= 0) return 'quota-full'
    if (quotaInfo.value.remaining <= 10) return 'quota-warning'
    return 'quota-normal'
  })

  // 🆕 新增：仅检查是否可以创建卡片（不修改状态）
  const checkCanCreate = async (): Promise<boolean> => {
    const id = projectIdRef.value
    if (!id) return false
    try {
      const { data } = await worldApi.canAddCard(id)
      return data.canAdd
    } catch {
      return false
    }
  }

  // 监听 projectId 变化自动重新加载
  watch(
    projectIdRef,
    () => {
      loadQuota()
    },
    { immediate: true }
  )

  return {
    quotaInfo,
    quotaStatus,
    loadQuota,
    loading,
    checkCanCreate, // 🆕 导出
  }
}