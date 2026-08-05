import { defineStore } from 'pinia'
import { ref } from 'vue'
import { jointApi, type JointActivity, type CreateJointRequest, type UpdateJointRequest, type JointStatus, type JointType } from './joint'

export const useJointStore = defineStore('joint', () => {
  // ===== 状态 =====
  const activities = ref<JointActivity[]>([])
  const currentActivity = ref<JointActivity | null>(null)
  const myOrganized = ref<JointActivity[]>([])
  const myParticipated = ref<JointActivity[]>([])
  const loading = ref(false)
  const total = ref(0)

  // ===== 列表 =====
  async function fetchList(params?: {
    page?: number
    pageSize?: number
    keyword?: string
    status?: JointStatus
    type?: JointType
  }) {
    loading.value = true
    try {
      const res = await jointApi.getList(params)
      activities.value = res.items ?? []
      total.value = res.total ?? 0
    } catch (error) {
      console.error('获取联合列表失败:', error)
      activities.value = []
      total.value = 0
    } finally {
      loading.value = false
    }
  }

  // ===== 详情 =====
  async function fetchDetail(id: string) {
    loading.value = true
    try {
      const res = await jointApi.getDetail(id)
      currentActivity.value = res
      return res
    } catch (error) {
      console.error('获取联合详情失败:', error)
      throw error
    } finally {
      loading.value = false
    }
  }

  // ===== 创建 =====
  async function create(data: CreateJointRequest) {
    loading.value = true
    try {
      const res = await jointApi.create(data)
      return res
    } catch (error) {
      console.error('创建联合失败:', error)
      throw error
    } finally {
      loading.value = false
    }
  }

  // ===== 更新 =====
  async function update(id: string, data: UpdateJointRequest) {
    loading.value = true
    try {
      const res = await jointApi.update(id, data)
      if (currentActivity.value?.id === id) {
        currentActivity.value = res
      }
      // 更新列表中的对应项
      const index = activities.value.findIndex(a => a.id === id)
      if (index !== -1) {
        activities.value[index] = res
      }
      return res
    } catch (error) {
      console.error('更新联合失败:', error)
      throw error
    } finally {
      loading.value = false
    }
  }

  // ===== 删除 =====
  async function remove(id: string) {
    loading.value = true
    try {
      await jointApi.delete(id)
      activities.value = activities.value.filter(a => a.id !== id)
      myOrganized.value = myOrganized.value.filter(a => a.id !== id)
      if (currentActivity.value?.id === id) {
        currentActivity.value = null
      }
    } catch (error) {
      console.error('删除联合失败:', error)
      throw error
    } finally {
      loading.value = false
    }
  }

  // ===== 我举办的 =====
  async function fetchMyOrganized(status?: JointStatus) {
    loading.value = true
    try {
      const res = await jointApi.getMyOrganized({ status })
      myOrganized.value = res ?? []
    } catch (error) {
      console.error('获取我举办的联合失败:', error)
      myOrganized.value = []
    } finally {
      loading.value = false
    }
  }

  // ===== 我参与的 =====
  async function fetchMyParticipated(status?: JointStatus) {
    loading.value = true
    try {
      const res = await jointApi.getMyParticipated({ status })
      myParticipated.value = res ?? []
    } catch (error) {
      console.error('获取我参与的联合失败:', error)
      myParticipated.value = []
    } finally {
      loading.value = false
    }
  }

  // ===== 报名 =====
  async function join(id: string, remark?: string) {
    try {
      const res = await jointApi.join(id, remark)
      if (currentActivity.value?.id === id) {
        currentActivity.value = res
      }
      return res
    } catch (error) {
      console.error('报名失败:', error)
      throw error
    }
  }

  // ===== 取消报名 =====
  async function cancelJoin(id: string) {
    try {
      const res = await jointApi.cancelJoin(id)
      if (currentActivity.value?.id === id) {
        currentActivity.value = res
      }
      return res
    } catch (error) {
      console.error('取消报名失败:', error)
      throw error
    }
  }

  // ===== 审核参与者（举办者） =====
  async function auditParticipant(activityId: string, userId: string, status: 'approved' | 'rejected') {
    try {
      const res = await jointApi.auditParticipant(activityId, userId, status)
      if (currentActivity.value?.id === activityId) {
        currentActivity.value = res
      }
      return res
    } catch (error) {
      console.error('审核参与者失败:', error)
      throw error
    }
  }

  // ===== 踢出参与者（举办者） =====
  async function kickParticipant(activityId: string, userId: string) {
    try {
      const res = await jointApi.kickParticipant(activityId, userId)
      if (currentActivity.value?.id === activityId) {
        currentActivity.value = res
      }
      return res
    } catch (error) {
      console.error('踢出参与者失败:', error)
      throw error
    }
  }

  // ===== 封禁/解封活动（管理员） =====
  async function toggleBan(activityId: string) {
    try {
      const res = await jointApi.toggleBan(activityId)
      if (currentActivity.value?.id === activityId) {
        currentActivity.value = res
      }
      const index = activities.value.findIndex(a => a.id === activityId)
      if (index !== -1) {
        activities.value[index] = res
      }
      return res
    } catch (error) {
      console.error('操作失败:', error)
      throw error
    }
  }

  // ===== 清空当前活动 =====
  function clearCurrent() {
    currentActivity.value = null
  }

  return {
    activities,
    currentActivity,
    myOrganized,
    myParticipated,
    loading,
    total,
    fetchList,
    fetchDetail,
    create,
    update,
    remove,
    fetchMyOrganized,
    fetchMyParticipated,
    join,
    cancelJoin,
    auditParticipant,
    kickParticipant,
    toggleBan,
    clearCurrent,
  }
})