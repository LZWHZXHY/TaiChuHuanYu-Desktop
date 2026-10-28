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
  const error = ref(false)
  let loadVersion = 0

  const loadQuota = async () => {
    const version = ++loadVersion
    const id = projectIdRef.value
    if (!id) {
      loading.value = false
      return
    }
    loading.value = true
    error.value = false
    try {
      const data = await worldApi.canAddCard(id)
      if (version !== loadVersion) return
      quotaInfo.value = {
        currentCount: data.currentCount,
        maxCount: data.maxCount,
        remaining: data.maxCount - data.currentCount,
        canAdd: data.canAdd,
      }
    } catch {
      if (version === loadVersion) error.value = true
    } finally {
      if (version === loadVersion) loading.value = false
    }
  }

  const quotaStatus = computed(() => {
    if (quotaInfo.value.remaining <= 0) return 'quota-full'
    if (quotaInfo.value.remaining <= 10) return 'quota-warning'
    return 'quota-normal'
  })

  // 保存前重新检查配额，不复用页面展示的旧值。
  const checkCanCreate = async (): Promise<boolean> => {
    const id = projectIdRef.value
    if (!id) return false
    try {
      const data = await worldApi.canAddCard(id)
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
    error,
    quotaInfo,
    quotaStatus,
    loadQuota,
    loading,
    checkCanCreate,
  }
}
