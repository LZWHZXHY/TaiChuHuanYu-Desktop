<template>
  <div class="member-manager">
    <div v-if="isLoading" class="loading-state">
      <div class="loading-bar"></div>
    </div>

    <div v-else class="manager-layout">
      <aside v-if="hasManagerPermission || joinRequests.length > 0" class="side-panel">
        <section v-if="hasManagerPermission" class="panel-block invite-section">
          <h2 class="section-title">邀请协作者</h2>
          <div class="invite-form">
            <input
              v-model="inviteTarget"
              type="text"
              placeholder="输入用户名 或 用户ID..."
              class="invite-input"
              @keyup.enter="sendInvitation"
            />
            <button class="invite-btn" @click="sendInvitation" :disabled="!inviteTarget.trim()">
              发送邀请
            </button>
          </div>
        </section>

        <section v-if="hasManagerPermission && joinRequests.length > 0" class="panel-block requests-section">
          <h2 class="section-title">
            待审核申请
            <span class="count-badge">{{ joinRequests.length }}</span>
          </h2>
          <ul class="request-list">
            <li v-for="request in joinRequests" :key="request.id" class="request-item">
              <div class="requester-info">
                <div class="member-avatar small">{{ request.applicantName ? request.applicantName.charAt(0) : '?' }}</div>
                <div class="requester-details">
                  <span class="requester-name">{{ request.applicantName }}</span>
                  <span class="requester-email">{{ request.applicantEmail }}</span>
                  <p class="request-message" v-if="request.message">“ {{ request.message }} ”</p>
                </div>
              </div>
              <div class="request-actions">
                <button class="accept-btn" @click="handleRequest(request.id, 'approve')">接受</button>
                <button class="reject-btn" @click="handleRequest(request.id, 'reject')">拒绝</button>
              </div>
            </li>
          </ul>
        </section>
      </aside>

      <main class="members-panel">
        <h2 class="section-title">
          团队成员
          <span class="count-badge">{{ members.length }}</span>
        </h2>
        <ul class="member-grid">
          <li v-for="member in members" :key="member.id" class="member-card">
            <div class="card-top">
              <div class="member-avatar large">{{ member.name ? member.name.charAt(0) : '?' }}</div>
              <div class="member-core">
                <span class="member-name">
                  {{ member.name }}
                  <span v-if="member.role === 'owner'" class="owner-tag">创建者</span>
                </span>
                <span class="member-email">{{ member.email }}</span>
              </div>
            </div>
            <div class="card-bottom">
              <div class="role-section">
                <select
                  class="role-select"
                  :value="member.role"
                  :disabled="member.role === 'owner' || !hasManagerPermission"
                  @change="updateRole(member.id, ($event.target as HTMLSelectElement).value)"
                >
                  <option v-for="role in roleOptions" :key="role.value" :value="role.value">
                    {{ role.label }}
                  </option>
                </select>
                <p class="role-description">
                  {{ getRoleDescription(member.role) }}
                </p>
              </div>
              <button
                v-if="member.role !== 'owner' && hasManagerPermission"
                class="remove-btn"
                @click="confirmRemove(member)"
              >
                移除
              </button>
            </div>
          </li>
        </ul>
      </main>
    </div>

    <Transition name="fade">
      <div v-if="removeModal.isOpen" class="modal-overlay" @click.self="closeRemoveModal">
        <div class="minimal-modal">
          <header class="modal-inner-header">
            <h2>移除成员</h2>
            <p>确定要将 <strong>{{ removeModal.memberName }}</strong> 移出项目吗？此操作无法撤销。</p>
          </header>
          <footer class="modal-footer">
            <button class="cancel-btn" @click="closeRemoveModal">取消</button>
            <button class="confirm-btn" @click="executeRemove">确认移除</button>
          </footer>
        </div>
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import projectService from '../../../api/projectService'

const props = defineProps<{
  projectId: string
}>()

const emit = defineEmits(['updated'])

interface Member {
  id: string
  name: string
  email: string
  role: string
}

interface JoinRequest {
  id: string
  applicantName: string
  applicantEmail: string
  message: string 
}

const roleOptions = [
  { value: 'owner', label: '超级管理员', description: '完全控制权限，可管理项目、成员与所有任务' },
  { value: 'admin', label: '管理员', description: '管理成员和项目设置，可操作所有任务' },
  { value: 'editor', label: '编辑者', description: '创建、编辑、移动所有任务，但不能管理成员' },
  { value: 'executor', label: '执行员', description: '仅可移动和处理自己被指派的任务' },
  { value: 'viewer', label: '观察者', description: '只读访问，无法进行任何修改' },
]

const roleMapToBackend: Record<string, number> = {
  'owner': 0,
  'admin': 1,
  'editor': 2,
  'executor': 3,
  'viewer': 4
}

const getRoleDescription = (roleValue: string) => {
  return roleOptions.find(r => r.value === roleValue)?.description || ''
}

const isLoading = ref(true)
const members = ref<Member[]>([])
const joinRequests = ref<JoinRequest[]>([])
const inviteTarget = ref('') 
const hasManagerPermission = ref(false) // 🌟 新增：标记当前登录用户是否有管理权限

const removeModal = ref({
  isOpen: false,
  memberId: '',
  memberName: '',
})

// 🌟 核心改进：拆解 Promise.all，将常规列表与权限审批解耦加载
const loadData = async () => {
  isLoading.value = true
  
  // 1. 获取团队成员列表（所有成员都能看，不应该被卡住）
  try {
    const membersData = await projectService.getProjectMembers(props.projectId)
    members.value = membersData as any[]
  } catch (error) {
    console.error('加载常规团队成员失败:', error)
  }

  // 2. 获取待审核申请列表（仅管理层可读，403 时不应该引发页面溃败）
  try {
    const requestsData = await projectService.getPendingApplications(props.projectId)
    joinRequests.value = requestsData as any[]
    hasManagerPermission.value = true // 请求成功，说明是项目所有者或管理员
  } catch (error: any) {
    // 判断是否是 403 或者是触发了后端的权限校验错误
    if (error.response?.status === 403 || error.response?.status === 500) {
      hasManagerPermission.value = false
      joinRequests.value = []
      console.log('当前登录用户非管理层，已隐式隐藏审批及邀请面板。')
    } else {
      console.error('加载待审核申请列表遇到了其他异常:', error)
    }
  } finally {
    isLoading.value = false
  }
}

onMounted(loadData)

// 发送邀请
const sendInvitation = async () => {
  const target = inviteTarget.value.trim()
  if (!target) return
  try {
    await projectService.inviteMember(props.projectId, { usernameOrId: target })
    inviteTarget.value = ''
    alert("已成功将该共建者纳入灵脉。")
    await loadData() 
    emit('updated')
  } catch (err) {
    console.error('邀请失败', err)
    alert("邀请失败，未在太初世界寻得此用户或其已身在此内。")
  }
}

const updateRole = async (memberId: string, newRoleString: string) => {
  const numericRole = roleMapToBackend[newRoleString] ?? 4
  try {
    await projectService.updateMemberRole(props.projectId, memberId, { roleValue: numericRole })
    const member = members.value.find(m => m.id === memberId)
    if (member) member.role = newRoleString
  } catch (err) {
    console.error('更新角色失败', err)
  }
}

const confirmRemove = (member: Member) => {
  removeModal.value = {
    isOpen: true,
    memberId: member.id,
    memberName: member.name,
  }
}

const closeRemoveModal = () => {
  removeModal.value.isOpen = false
}

const executeRemove = async () => {
  try {
    await projectService.removeMember(props.projectId, removeModal.value.memberId)
    members.value = members.value.filter(m => m.id !== removeModal.value.memberId)
    closeRemoveModal()
    emit('updated')
  } catch (err) {
    console.error('移除成员失败', err)
  }
}

const handleRequest = async (requestId: string, action: 'approve' | 'reject') => {
  try {
    await projectService.handleApplication(props.projectId, requestId, {
      approve: action === 'approve'
    })
    
    joinRequests.value = joinRequests.value.filter(r => r.id !== requestId)
    
    if (action === 'approve') {
      await loadData()
      emit('updated') 
    }
    
    alert(action === 'approve' ? "已接纳该共建者融入灵脉。" : "已婉拒该用户的申请。")
  } catch (err) {
    console.error('裁决申请失败', err)
    alert("操作失败，请确保您拥有项目管理层权限。")
  }
}
</script>

<style scoped>
.member-manager {
  width: 100%;
  max-width: 1200px;
  margin: 0 auto;
  padding: 40px 24px 60px;
  animation: fadeIn 0.8s cubic-bezier(0.16, 1, 0.3, 1);
}
.loading-state { display: flex; justify-content: center; padding: 100px 0; }
.loading-bar { width: 60px; height: 1px; background: #1a1a1a; animation: pulse 1.5s infinite; }
.manager-layout { display: flex; gap: 48px; align-items: flex-start; }
@media (max-width: 800px) { .manager-layout { flex-direction: column; } }
.side-panel { flex: 0 0 340px; display: flex; flex-direction: column; gap: 32px; }
.panel-block { background: #fff; border: 1px solid #f0f0f0; padding: 28px; }
.members-panel { flex: 1; min-width: 0; }
.section-title { font-size: 0.85rem; font-weight: 500; letter-spacing: 0.5px; text-transform: uppercase; color: #888; margin: 0 0 20px 0; display: flex; align-items: center; gap: 8px; }
.count-badge { font-size: 0.7rem; font-family: monospace; color: #bbb; background: #fafafa; padding: 2px 6px; border-radius: 2px; font-weight: 400; }
.invite-form { display: flex; gap: 10px; }
.invite-input { flex: 1; border: 1px solid #eaeaea; padding: 10px 14px; font-size: 0.9rem; color: #1a1a1a; outline: none; transition: border-color 0.2s; background: #fff; }
.invite-input:focus { border-color: #1a1a1a; }
.invite-btn { padding: 10px 20px; background: #1a1a1a; color: #fff; border: none; font-size: 0.85rem; cursor: pointer; transition: background 0.3s; white-space: nowrap; }
.invite-btn:disabled { background: #ccc; cursor: not-allowed; }
.invite-btn:not(:disabled):hover { background: #333; }

.request-list { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 16px; }
.request-item { display: flex; flex-direction: column; gap: 16px; padding: 20px 0; border-bottom: 1px solid #f5f5f5; }
.request-item:last-child { border-bottom: none; }
.requester-info { display: flex; align-items: flex-start; gap: 12px; }
.requester-details { display: flex; flex-direction: column; flex: 1; min-width: 0; }
.requester-name { font-size: 0.9rem; color: #1a1a1a; font-weight: 500; }
.requester-email { font-size: 0.75rem; color: #999; margin-bottom: 6px; }
.request-message { font-size: 0.8rem; color: #666; font-style: italic; background: #fafafa; padding: 8px 12px; border-left: 2px solid #1a1a1a; margin: 4px 0 0 0; line-height: 1.5; word-break: break-all; }

.request-actions { display: flex; gap: 8px; justify-content: flex-end; width: 100%; }
.accept-btn { background: #1a1a1a; color: #fff; border: none; font-size: 0.75rem; padding: 6px 16px; cursor: pointer; transition: background 0.2s; border-radius: 2px; }
.accept-btn:hover { background: #333; }
.reject-btn { background: none; border: 1px solid #eaeaea; color: #888; font-size: 0.75rem; padding: 6px 16px; cursor: pointer; transition: all 0.2s; border-radius: 2px; }
.reject-btn:hover { border-color: #ff4757; color: #ff4757; background: #fff5f5; }

.member-grid { list-style: none; padding: 0; margin: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 16px; }
.member-card { background: #fff; border: 1px solid #f0f0f0; padding: 24px; display: flex; flex-direction: column; gap: 16px; transition: border-color 0.2s, box-shadow 0.2s; }
.member-card:hover { border-color: #ddd; box-shadow: 0 10px 30px rgba(0,0,0,0.03); }
.card-top { display: flex; align-items: center; gap: 16px; }
.member-avatar { background: #f5f5f5; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-weight: 500; color: #666; text-transform: uppercase; flex-shrink: 0; }
.member-avatar.small { width: 32px; height: 32px; font-size: 0.75rem; }
.member-avatar.large { width: 44px; height: 44px; font-size: 0.9rem; }
.member-core { display: flex; flex-direction: column; gap: 2px; }
.member-name { font-size: 0.95rem; color: #1a1a1a; font-weight: 500; display: flex; align-items: center; gap: 6px; }
.owner-tag { font-size: 0.65rem; background: #1a1a1a; color: #fff; padding: 1px 6px; border-radius: 2px; font-weight: 400; }
.member-email { font-size: 0.8rem; color: #999; }
.card-bottom { display: flex; justify-content: space-between; align-items: flex-end; margin-top: auto; }
.role-section { display: flex; flex-direction: column; gap: 6px; flex: 1; }
.role-select { border: 1px solid #eaeaea; padding: 6px 10px; font-size: 0.8rem; color: #1a1a1a; background: #fff; outline: none; cursor: pointer; transition: border-color 0.2s; width: 140px; }
.role-select:disabled { background: #fafafa; color: #999; cursor: not-allowed; }
.role-select:focus { border-color: #1a1a1a; }
.role-description { font-size: 0.7rem; color: #aaa; line-height: 1.4; margin: 0; }
.remove-btn { background: none; border: none; color: #bbb; font-size: 0.8rem; cursor: pointer; padding: 6px 0; transition: color 0.2s; align-self: center; }
.remove-btn:hover { color: #ff4757; }
.modal-overlay { position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(255, 255, 255, 0.85); backdrop-filter: blur(8px); display: flex; align-items: center; justify-content: center; z-index: 1000; }
.minimal-modal { background: #fff; width: 100%; max-width: 420px; padding: 48px; border: 1px solid #eee; box-shadow: 0 40px 100px rgba(0, 0, 0, 0.04); }
.modal-inner-header h2 { font-size: 1.2rem; font-weight: 500; margin: 0 0 12px 0; color: #1a1a1a; }
.modal-inner-header p { font-size: 0.85rem; color: #777; line-height: 1.6; margin: 0; }
.modal-footer { margin-top: 40px; display: flex; justify-content: flex-end; gap: 16px; }
.cancel-btn { background: none; border: none; color: #999; font-size: 0.85rem; cursor: pointer; padding: 10px 20px; transition: color 0.3s; }
.cancel-btn:hover { color: #1a1a1a; }
.confirm-btn { background: #1a1a1a; color: #fff; border: none; font-size: 0.85rem; cursor: pointer; padding: 10px 28px; border-radius: 2px; transition: background 0.3s; }
.confirm-btn:hover { background: #333; }
.fade-enter-active, .fade-leave-active { transition: opacity 0.4s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
@keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
@keyframes pulse { 0% { transform: scaleX(0.5); opacity: 0.2; } 50% { transform: scaleX(1.5); opacity: 1; } 100% { transform: scaleX(0.5); opacity: 0.2; } }
</style>