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

  let detailRequest = 0

  // ===== 详情 =====
  async function fetchDetail(id: string) {
    const request = ++detailRequest
    if (currentActivity.value?.id !== id) currentActivity.value = null
    loading.value = true
    try {
      const res = await jointApi.getDetail(id)
      if (request === detailRequest) currentActivity.value = res
      return res
    } catch (error) {
      if (request === detailRequest) currentActivity.value = null
      console.error('获取联合详情失败:', error)
      throw error
    } finally {
      if (request === detailRequest) loading.value = false
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

  // 各操作只更新原先负责的缓存：参与操作更新详情，编辑和管理操作同时更新列表。
  async function runActivityAction(id: string, action: () => Promise<JointActivity>, errorMessage: string, updateList = false) {
    try {
      const result = await action()
      if (currentActivity.value?.id === id) currentActivity.value = result
      if (updateList) {
        const index = activities.value.findIndex(activity => activity.id === id)
        if (index !== -1) activities.value[index] = result
      }
      return result
    } catch (error) {
      console.error(errorMessage, error)
      throw error
    }
  }

  async function update(id: string, data: UpdateJointRequest) {
    loading.value = true
    try {
      return await runActivityAction(id, () => jointApi.update(id, data), '更新联合失败:', true)
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

  function join(id: string, remark?: string) {
    return runActivityAction(id, () => jointApi.join(id, remark), '报名失败:')
  }

  function cancelJoin(id: string) {
    return runActivityAction(id, () => jointApi.cancelJoin(id), '取消报名失败:')
  }

  function auditParticipant(activityId: string, userId: string, status: 'approved' | 'rejected') {
    return runActivityAction(activityId, () => jointApi.auditParticipant(activityId, userId, status), '审核参与者失败:')
  }

  function kickParticipant(activityId: string, userId: string) {
    return runActivityAction(activityId, () => jointApi.kickParticipant(activityId, userId), '踢出参与者失败:')
  }

  function toggleBan(activityId: string) {
    return runActivityAction(activityId, () => jointApi.toggleBan(activityId), '操作失败:', true)
  }

  function approve(activityId: string, status: 'approved' | 'rejected') {
    return runActivityAction(activityId, () => jointApi.approve(activityId, status), '审批失败:', true)
  }

  // ===== 清空当前活动 =====
  function clearCurrent() {
    detailRequest++
    loading.value = false
    currentActivity.value = null
  }

  return {
    activities,
    currentActivity,
    myOrganized,
    myParticipated,
    loading,
    approve, 
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