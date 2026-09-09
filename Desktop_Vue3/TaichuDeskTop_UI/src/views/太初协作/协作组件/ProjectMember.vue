<template>
  <div class="member-manager" @click="closeDropdown">
    <div v-if="isLoading" class="loading-state">
      <div class="loading-bar"></div>
    </div>

    <div v-else class="manager-layout">
      <aside v-if="hasManagerPermission || joinRequests.length > 0" class="side-panel">
        <section v-if="hasManagerPermission" class="panel-block invite-section">
          <h2 class="section-title">邀请协作者</h2>
          
          <!-- 🌟 带有实时检索联想浮层的邀请输入框 -->
          <div class="invite-form-wrapper" @click.stop>
            <div class="invite-form">
              <input
                v-model="inviteTarget"
                type="text"
                placeholder="输入用户名检索 (例如: 照烧)..."
                class="invite-input"
                @input="handleInputSearch"
                @focus="handleInputFocus"
                @keyup.enter="sendInvitation"
              />
              <button class="invite-btn" @click="sendInvitation" :disabled="!inviteTarget.trim()">
                发送邀请
              </button>
            </div>

            <!-- 实时联想下拉悬浮层 -->
            <div v-if="showDropdown && (candidateList.length > 0 || isSearching)" class="search-dropdown-menu">
              <div v-if="isSearching" class="searching-hint">
                <span>正在探寻共建者...</span>
              </div>
              <ul v-else class="candidate-list">
                <li
                  v-for="user in candidateList"
                  :key="user.id"
                  class="candidate-item"
                  @click="selectCandidate(user)"
                >
                  <div class="candidate-avatar">{{ user.username.charAt(0) }}</div>
                  <div class="candidate-info">
                    <span class="candidate-name">{{ user.username }}</span>
                    <span class="candidate-email">{{ user.email }}</span>
                  </div>
                  <span class="choose-action">+ 选择</span>
                </li>
              </ul>
            </div>
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
                  <span v-if="member.isOwner" class="owner-tag">创建者</span>
                </span>
                <span class="member-email">{{ member.email }}</span>
              </div>
            </div>
            <div class="card-bottom">
              <div class="role-section">
                <!-- 创建者：固定展示项目掌控者 -->
                <div v-if="member.isOwner" class="owner-identity">
                  <span class="owner-title">项目掌控者</span>
                  <p class="role-description">至高权限，统领项目一切意图、长卷、汇报与成员</p>
                </div>

                <!-- 普通成员：展示动态身份组下拉框 -->
                <template v-else>
                  <select
                    class="role-select"
                    :value="member.roleIds?.[0] || 'role_system_viewer'"
                    :disabled="!hasManagerPermission"
                    @change="updateRole(member.id, ($event.target as HTMLSelectElement).value)"
                  >
                    <option v-for="role in availableRoles" :key="role.id" :value="role.id">
                      {{ role.name }}
                    </option>
                  </select>
                  <p class="role-description">
                    {{ getRoleDescription(member.roleIds?.[0]) }}
                  </p>
                </template>
              </div>

              <button
                v-if="!member.isOwner && hasManagerPermission"
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
import request from '@/utils/request'

const props = defineProps<{
  projectId: string
}>()

const emit = defineEmits(['updated'])

interface Member {
  id: string
  name: string
  email: string
  isOwner: boolean
  roleIds: string[]
}

interface JoinRequest {
  id: string
  applicantName: string
  applicantEmail: string
  message: string 
}

interface CandidateUser {
  id: string
  username: string
  email: string
}

const availableRoles = ref<any[]>([
  { id: 'role_system_viewer', name: '只读观察者', description: '只读访问，无法修改任务与汇报' },
  { id: 'role_system_member', name: '执行协作者', description: '可流转自己被指派的任务与提交汇报' },
  { id: 'role_system_admin', name: '统筹管理员', description: '可发布意图任务并查看全员汇报' }
])

const getRoleDescription = (roleId?: string) => {
  const match = availableRoles.value.find(r => r.id === roleId)
  return match?.description || '项目协作者身份'
}

const isLoading = ref(true)
const members = ref<Member[]>([])
const joinRequests = ref<JoinRequest[]>([])
const hasManagerPermission = ref(false)

// 🌟 实时搜索相关响应式状态
const inviteTarget = ref('')
const candidateList = ref<CandidateUser[]>([])
const showDropdown = ref(false)
const isSearching = ref(false)
let searchTimer: any = null

const removeModal = ref({
  isOpen: false,
  memberId: '',
  memberName: '',
})

const loadData = async () => {
  isLoading.value = true
  
  // 1. 获取动态身份组定义
  try {
    const rolesRes: any = await request.get(`/project/${props.projectId}/roles`)
    const list = rolesRes.data || rolesRes || []
    if (list.length > 0) {
      availableRoles.value = list
    }
  } catch (err) {
    console.warn('获取项目自定义身份组失败，使用默认角色模板')
  }

  // 2. 获取团队成员列表
  try {
    const membersData: any = await projectService.getProjectMembers(props.projectId)
    members.value = (membersData.data || membersData || []).map((m: any) => ({
      id: m.id,
      name: m.name,
      email: m.email,
      isOwner: Boolean(m.isOwner),
      roleIds: m.roleIds || []
    }))
  } catch (error) {
    console.error('加载团队成员失败:', error)
  }

  // 3. 获取待审核申请列表
  try {
    const requestsData: any = await projectService.getPendingApplications(props.projectId)
    joinRequests.value = requestsData.data || requestsData || []
    hasManagerPermission.value = true
  } catch (error: any) {
    if (error.response?.status === 403 || error.response?.status === 500) {
      hasManagerPermission.value = false
      joinRequests.value = []
    }
  } finally {
    isLoading.value = false
  }
}

onMounted(loadData)

// 🌟 输入防抖实时检索
const handleInputSearch = () => {
  if (searchTimer) clearTimeout(searchTimer)
  
  const query = inviteTarget.value.trim()
  if (!query) {
    candidateList.value = []
    showDropdown.value = false
    return
  }

  showDropdown.value = true
  isSearching.value = true

  searchTimer = setTimeout(async () => {
    try {
      const res: any = await request.get(`/project/${props.projectId}/members/search-candidates`, {
        params: { keyword: query }
      })
      candidateList.value = res.data || res || []
    } catch (err) {
      console.error('检索用户失败:', err)
      candidateList.value = []
    } finally {
      isSearching.value = false
    }
  }, 280) // 280ms 优雅防抖
}

const handleInputFocus = () => {
  if (candidateList.value.length > 0 && inviteTarget.value.trim()) {
    showDropdown.value = true
  }
}

// 选中候选人
const selectCandidate = (user: CandidateUser) => {
  inviteTarget.value = user.username
  showDropdown.value = false
  sendInvitation()
}

const closeDropdown = () => {
  showDropdown.value = false
}

// 发送邀请
const sendInvitation = async () => {
  const target = inviteTarget.value.trim()
  if (!target) return
  try {
    await projectService.inviteMember(props.projectId, { usernameOrId: target })
    inviteTarget.value = ''
    candidateList.value = []
    showDropdown.value = false
    alert("已成功将该共建者纳入灵脉。")
    await loadData() 
    emit('updated')
  } catch (err) {
    console.error('邀请失败', err)
    alert("邀请失败，未在太初世界寻得此用户或其已身在此内。")
  }
}

// 更新角色
const updateRole = async (memberId: string, newRoleId: string) => {
  try {
    await request.put(`/project/${props.projectId}/members/${memberId}/role`, {
      roleIds: [newRoleId]
    })
    const member = members.value.find(m => m.id === memberId)
    if (member) member.roleIds = [newRoleId]
    emit('updated')
  } catch (err) {
    console.error('更新角色身份组失败', err)
    alert('身份调整失败，可能权限不足')
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
.side-panel { flex: 0 0 360px; display: flex; flex-direction: column; gap: 32px; }
.panel-block { background: #fff; border: 1px solid #f0f0f0; padding: 28px; }
.members-panel { flex: 1; min-width: 0; }
.section-title { font-size: 0.85rem; font-weight: 500; letter-spacing: 0.5px; text-transform: uppercase; color: #888; margin: 0 0 20px 0; display: flex; align-items: center; gap: 8px; }
.count-badge { font-size: 0.7rem; font-family: monospace; color: #bbb; background: #fafafa; padding: 2px 6px; border-radius: 2px; font-weight: 400; }

/* 🌟 输入与联想浮层容器样式 */
.invite-form-wrapper {
  position: relative;
  width: 100%;
}
.invite-form { display: flex; gap: 10px; }
.invite-input { flex: 1; border: 1px solid #eaeaea; padding: 10px 14px; font-size: 0.9rem; color: #1a1a1a; outline: none; transition: border-color 0.2s; background: #fff; }
.invite-input:focus { border-color: #1a1a1a; }
.invite-btn { padding: 10px 18px; background: #1a1a1a; color: #fff; border: none; font-size: 0.85rem; cursor: pointer; transition: background 0.3s; white-space: nowrap; }
.invite-btn:disabled { background: #ccc; cursor: not-allowed; }
.invite-btn:not(:disabled):hover { background: #333; }

/* 联想下拉菜单 */
.search-dropdown-menu {
  position: absolute;
  top: calc(100% + 6px);
  left: 0;
  right: 0;
  background: #ffffff;
  border: 1px solid #eaeaea;
  box-shadow: 0 12px 30px rgba(0, 0, 0, 0.08);
  border-radius: 2px;
  z-index: 50;
  max-height: 240px;
  overflow-y: auto;
}
.searching-hint {
  padding: 16px;
  font-size: 0.8rem;
  color: #999;
  text-align: center;
}
.candidate-list {
  list-style: none;
  padding: 0;
  margin: 0;
}
.candidate-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 14px;
  cursor: pointer;
  border-bottom: 1px solid #f9f9f9;
  transition: background 0.2s;
}
.candidate-item:last-child {
  border-bottom: none;
}
.candidate-item:hover {
  background: #f7f7f7;
}
.candidate-avatar {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  background: #1a1a1a;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.75rem;
  font-weight: 500;
  flex-shrink: 0;
}
.candidate-info {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
}
.candidate-name {
  font-size: 0.85rem;
  font-weight: 500;
  color: #1a1a1a;
}
.candidate-email {
  font-size: 0.72rem;
  color: #999;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.choose-action {
  font-size: 0.75rem;
  color: #666;
}

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

.owner-identity { display: flex; flex-direction: column; gap: 4px; }
.owner-title { font-size: 0.85rem; font-weight: 600; color: #1a1a1a; letter-spacing: 0.5px; }

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