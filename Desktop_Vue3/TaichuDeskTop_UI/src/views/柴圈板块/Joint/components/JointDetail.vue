<template>
  <div class="joint-detail">
    <!-- 加载状态 -->
    <div v-if="loading" class="loading-state">
      <div class="spinner"></div>
      <span>加载联合详情...</span>
    </div>

    <!-- 空状态 -->
    <div v-else-if="!activity" class="empty-state">
      <p>联合活动不存在</p>
      <router-link to="/joint" class="empty-link">返回联合列表</router-link>
    </div>

    <!-- 详情内容 -->
    <template v-else>
      <!-- 页面头部 -->
      <div class="page-header">
        <div class="header-left">
          <button class="back-btn" @click="goBack">← 返回</button>
          <div>
            <h1 class="page-title">{{ activity.title }}</h1>
            <div class="page-meta">
              <span class="meta-tag type" :class="activity.type">{{ typeLabel(activity.type) }}</span>
              <span class="meta-tag status" :class="displayStatus">{{ statusLabel(displayStatus) }}</span>
              <span class="meta-tag audit" :class="{ required: activity.auditRequired }">
                {{ activity.auditRequired ? '需要审核' : '直接加入' }}
              </span>
              <span class="meta-tag source" :class="activity.organizerType">
                {{ activity.organizerType === 'official' ? '太虚绘院官方' : '用户自建' }}
              </span>
              <span
                v-if="activity.organizerType === 'user'"
                class="meta-tag approval"
                :class="activity.approvalStatus"
              >
                {{ approvalStatusLabel(activity.approvalStatus) }}
              </span>
              <!-- ⭐ 如果过期，显示过期标签 -->
              <span v-if="isExpired" class="meta-tag expired">已过期</span>
            </div>
          </div>
        </div>
        <div class="header-right">
          <router-link
            v-if="canEdit"
            :to="`/joint/edit/${activity.id}`"
            class="btn-line"
          >
            ✎ 编辑
          </router-link>

          <template v-if="canApprove">
            <button class="btn-line" @click="handleApprove('approved')">✓ 通过</button>
            <button class="btn-line danger" @click="handleApprove('rejected')">✕ 拒绝</button>
          </template>

          <button
            v-if="canBan"
            class="btn-line danger"
            @click="handleToggleBan"
          >
            {{ activity.status === 'banned' ? '解封' : '封禁' }}
          </button>

          <button
            v-if="canDelete"
            class="btn-line danger"
            @click="handleDelete"
          >
            🗑 删除
          </button>
        </div>
      </div>

      <!-- 封面图 -->
      <div v-if="activity.coverUrl" class="cover-area">
        <img :src="activity.coverUrl" :alt="activity.title" />
      </div>

      <!-- 详情网格 -->
      <div class="detail-grid">
        <div class="detail-left">
          <div class="info-item">
            <span class="info-label">举办者</span>
            <span class="info-value">{{ activity.organizerName }}</span>
          </div>
          <div v-if="activity.contact" class="info-item">
            <span class="info-label">群聊号</span>
            <span class="info-value">{{ activity.contact }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">参与人数</span>
            <span class="info-value">{{ activity.participantCount }} 人</span>
          </div>

          <!-- ⭐ 开始时间 -->
          <div class="info-item">
            <span class="info-label">开始时间</span>
            <span class="info-value">{{ formatDateTime(activity.startDate) }}</span>
          </div>

          <!-- ⭐ 结束时间（如果有） -->
          <div v-if="activity.endDate" class="info-item">
            <span class="info-label">结束时间</span>
            <span class="info-value">{{ formatDateTime(activity.endDate) }}</span>
          </div>

          <!-- ⭐ 如果过期，显示过期提醒 -->
          <div v-if="isExpired" class="info-item expired-warning">
            <span class="info-label">⚠️ 状态</span>
            <span class="info-value expired-text">该活动已结束</span>
          </div>

          <!-- ⭐ 剩余天数（如果未过期且有结束时间） -->
          <div v-if="!isExpired && activity.endDate" class="info-item">
            <span class="info-label">剩余时间</span>
            <span class="info-value" :class="countdownClass">{{ countdownText }}</span>
          </div>

          <div class="info-item">
            <span class="info-label">创建时间</span>
            <span class="info-value">{{ formatDate(activity.createdAt) }}</span>
          </div>
        </div>

        <div class="detail-right">
          <div class="desc-section">
            <h3 class="desc-title">活动描述</h3>
            <p class="desc-text">{{ activity.description }}</p>
          </div>
          <div v-if="activity.requirements" class="desc-section">
            <h3 class="desc-title">参与要求</h3>
            <p class="desc-text">{{ activity.requirements }}</p>
          </div>
        </div>
      </div>

      <!-- 参与者列表 -->
      <div class="participants-section">
        <div class="section-header">
          <h3 class="section-title">参与者</h3>
          <span class="section-count">{{ activity.participantCount }} 人</span>
        </div>

        <div class="participant-actions">
          <button
            v-if="!isParticipant && !isOrganizer"
            class="btn-line btn-join"
            :disabled="joining || !canJoin || isExpired"
            @click="handleJoin"
          >
            <span v-if="isExpired">活动已结束</span>
            <span v-else-if="!canJoin && activity.organizerType === 'user' && activity.approvalStatus !== 'approved'">
              审核中，暂不可报名
            </span>
            <span v-else-if="joining">报名中...</span>
            <span v-else>报名参与</span>
          </button>

          <button
            v-if="isParticipant && !isExpired"
            class="btn-line btn-cancel"
            :disabled="cancelling"
            @click="handleCancelJoin"
          >
            {{ cancelling ? '取消中...' : '取消报名' }}
          </button>

          <span v-if="isOrganizer" class="organizer-tip">（你举办的活动）</span>
          <span v-if="isExpired && !isOrganizer" class="expired-tip">（活动已结束，不可操作）</span>
        </div>

        <div v-if="activity.participants?.length" class="participant-list">
          <div
            v-for="p in activity.participants"
            :key="p.id"
            class="participant-item"
          >
            <span class="participant-name">{{ p.userName }}</span>
            <span class="participant-status" :class="p.status">
              {{ participantStatusLabel(p.status) }}
            </span>
            <div v-if="isOrganizer && !isExpired" class="participant-actions-admin">
              <template v-if="p.status === 'pending'">
                <button
                  class="btn-sm approve"
                  @click="handleAuditParticipant(p.userId, 'approved')"
                >
                  通过
                </button>
                <button
                  class="btn-sm reject"
                  @click="handleAuditParticipant(p.userId, 'rejected')"
                >
                  拒绝
                </button>
              </template>
              <button
                v-if="p.status === 'approved'"
                class="btn-sm kick"
                @click="handleKick(p.userId)"
              >
                踢出
              </button>
            </div>
          </div>
        </div>

        <div v-else class="participant-empty">
          暂无参与者
        </div>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useJointStore } from '../joint_store'
import { useUserStore } from '@/stores/user'
import type { JointActivity, ParticipantStatus, JointStatus } from '../joint'

const router = useRouter()
const route = useRoute()
const store = useJointStore()
const userStore = useUserStore()

const joining = ref(false)
const cancelling = ref(false)

const activity = computed(() => store.currentActivity)
const loading = computed(() => store.loading)

// ===== 判断活动是否已过期 =====
const isExpired = computed(() => {
  if (!activity.value?.endDate) return false
  return new Date(activity.value.endDate) < new Date()
})

// ===== 实际显示的状态（如果过期则强制显示为 'ended'） =====
const displayStatus = computed<JointStatus>(() => {
  if (!activity.value) return 'open'
  if (isExpired.value && activity.value.status !== 'ended') {
    return 'ended'
  }
  return activity.value.status
})

// ===== 计算剩余天数 =====
const daysRemaining = computed(() => {
  if (!activity.value?.endDate) return 0
  const now = new Date()
  const end = new Date(activity.value.endDate)
  const diff = end.getTime() - now.getTime()
  return Math.ceil(diff / (1000 * 60 * 60 * 24))
})

// ===== 倒计时文本 =====
const countdownText = computed(() => {
  const days = daysRemaining.value
  if (days <= 0) return '已结束'
  if (days === 1) return '剩余 1 天'
  if (days <= 7) return `剩余 ${days} 天`
  if (days <= 30) return `剩余 ${Math.floor(days / 7)} 周`
  return `剩余 ${Math.floor(days / 30)} 月`
})

// ===== 倒计时样式类 =====
const countdownClass = computed(() => {
  const days = daysRemaining.value
  if (days <= 3) return 'countdown-urgent'
  if (days <= 7) return 'countdown-warning'
  return 'countdown-normal'
})

const isOrganizer = computed(() =>
  activity.value?.organizerId === userStore.userInfo?.id
)

const isSuperAdmin = computed(() =>
  userStore.userInfo?.permissions?.includes('SuperAdmin') ?? false
)

const isJointManager = computed(() =>
  userStore.userInfo?.permissions?.includes('JointManager') ?? false
)

const hasAdminPermission = computed(() => isSuperAdmin.value || isJointManager.value)

// ===== 删除权限 =====
const canDelete = computed(() => {
  if (!activity.value) return false

  // 官方联合：只有 SuperAdmin 可删除
  if (activity.value.organizerType === 'official') {
    return isSuperAdmin.value
  }

  // 用户自建：作者本人 或 SuperAdmin 可删除
  if (activity.value.organizerType === 'user') {
    return isOrganizer.value || isSuperAdmin.value
  }

  return false
})

// ===== 封禁权限 =====
const canBan = computed(() => {
  if (!activity.value) return false
  if (activity.value.organizerId === userStore.userInfo?.id) return false

  if (activity.value.organizerType === 'official') {
    return isSuperAdmin.value
  }

  return isSuperAdmin.value || isJointManager.value
})

// ===== 审批权限 =====
const canApprove = computed(() => {
  if (!activity.value) return false
  if (activity.value.organizerType !== 'user') return false
  if (activity.value.approvalStatus !== 'pending') return false
  return hasAdminPermission.value
})

// ===== 报名权限 =====
const canJoin = computed(() => {
  if (!activity.value) return false
  if (activity.value.organizerType === 'user' && activity.value.approvalStatus !== 'approved') {
    return false
  }
  return true
})

const isParticipant = computed(() => {
  if (!activity.value?.participants) return false
  return activity.value.participants.some(p => p.userId === userStore.userInfo?.id)
})

function statusLabel(status: string): string {
  const map: Record<string, string> = {
    open: '报名中',
    closed: '已截止',
    ended: '已结束',
    banned: '已封禁',
    abandoned: '暴毙',
  }
  return map[status] || status
}

function typeLabel(type: string): string {
  const map: Record<string, string> = {
    joint: '联合',
    relay: '接力',
    project: '企划',
    free: '自由',
    other: '其他',
  }
  return map[type] || type
}

function participantStatusLabel(status: ParticipantStatus): string {
  const map: Record<string, string> = {
    pending: '待审核',
    approved: '已通过',
    rejected: '已拒绝',
  }
  return map[status] || status
}

function approvalStatusLabel(status?: string): string {
  const map: Record<string, string> = {
    pending: '审核中',
    approved: '已通过',
    rejected: '已拒绝',
  }
  return status ? map[status] || status : ''
}

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString('zh-CN', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  })
}

// ===== ⭐ 格式化日期时间（详情页用） =====
function formatDateTime(dateStr: string): string {
  return new Date(dateStr).toLocaleString('zh-CN', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function goBack() {
  router.push('/joint')
}

async function handleJoin() {
  if (!activity.value) return
  if (!canJoin.value) {
    alert('该活动暂不可报名')
    return
  }
  if (isExpired.value) {
    alert('该活动已结束，无法报名')
    return
  }
  joining.value = true
  try {
    await store.join(activity.value.id)
  } catch (error) {
    console.error('报名失败:', error)
    alert('报名失败，请重试')
  } finally {
    joining.value = false
  }
}

async function handleCancelJoin() {
  if (!activity.value) return
  if (!confirm('确定要取消报名吗？')) return
  cancelling.value = true
  try {
    await store.cancelJoin(activity.value.id)
  } catch (error) {
    console.error('取消报名失败:', error)
    alert('取消报名失败，请重试')
  } finally {
    cancelling.value = false
  }
}

async function handleAuditParticipant(userId: string, status: 'approved' | 'rejected') {
  if (!activity.value) return
  try {
    await store.auditParticipant(activity.value.id, userId, status)
    await store.fetchDetail(activity.value.id)
  } catch (error) {
    console.error('审核失败:', error)
    alert('操作失败，请重试')
  }
}

async function handleKick(userId: string) {
  if (!activity.value) return
  if (!confirm('确定要踢出该参与者吗？')) return
  try {
    await store.kickParticipant(activity.value.id, userId)
    await store.fetchDetail(activity.value.id)
  } catch (error) {
    console.error('踢出失败:', error)
    alert('操作失败，请重试')
  }
}

async function handleApprove(status: 'approved' | 'rejected') {
  if (!activity.value) return
  const action = status === 'approved' ? '通过' : '拒绝'
  if (!confirm(`确定要${action}该联合活动吗？`)) return
  try {
    await store.approve(activity.value.id, status)
    await store.fetchDetail(activity.value.id)
  } catch (error) {
    console.error('审批失败:', error)
    alert('操作失败，请重试')
  }
}


// ===== 🆕 编辑权限 =====
const canEdit = computed(() => {
  if (!activity.value) return false

  // 管理员：可编辑任何状态的活动（包括已过期、已封禁等）
  if (hasAdminPermission.value) return true

  // 组织者：只能编辑自己举办的、且未过期的活动
  if (isOrganizer.value && !isExpired.value) return true

  return false
})

async function handleToggleBan() {
  if (!activity.value) return
  const action = activity.value.status === 'banned' ? '解封' : '封禁'
  if (!confirm(`确定要${action}该联合活动吗？`)) return
  try {
    await store.toggleBan(activity.value.id)
    await store.fetchDetail(activity.value.id)
  } catch (error) {
    console.error('操作失败:', error)
    alert('操作失败，请重试')
  }
}

async function handleDelete() {
  if (!activity.value) return
  if (!confirm(`确定要永久删除「${activity.value.title}」吗？此操作不可恢复！`)) return
  try {
    await store.remove(activity.value.id)
    router.push('/joint')
  } catch (error) {
    console.error('删除失败:', error)
    alert('删除失败，请重试')
  }
}

onMounted(async () => {
  const id = route.params.id as string
  await store.fetchDetail(id)
})
</script>

<style scoped>
/* ===== 柴圈板块统一风格 ===== */
.joint-detail {
  max-width: 1100px;
  margin: 0 auto;
  padding: 32px 24px 60px;
  background: var(--paper-bg);
  min-height: 100vh;
  color: var(--ink-black);
  font-family: var(--font-family);
}

.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 80px 0;
  gap: 16px;
  color: var(--ink-gray);
  letter-spacing: 0.15em;
}

.spinner {
  width: 32px;
  height: 32px;
  border: 2px solid var(--line-raw);
  border-top-color: var(--ink-black);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.empty-state {
  padding: 80px 0;
  text-align: center;
  color: var(--ink-gray);
  letter-spacing: 0.15em;
}

.empty-link {
  color: var(--cinnabar);
  text-decoration: none;
  border-bottom: 1px solid var(--line-raw);
  padding-bottom: 2px;
}

.empty-link:hover {
  border-color: var(--cinnabar);
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding-bottom: 16px;
  border-bottom: 1px solid var(--line-raw);
  margin-bottom: 24px;
}

.header-left {
  display: flex;
  align-items: flex-start;
  gap: 16px;
}

.header-right {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
}

.back-btn {
  background: none;
  border: none;
  color: var(--ink-gray);
  font-size: 14px;
  letter-spacing: 0.15em;
  cursor: pointer;
  padding: 4px 8px 4px 0;
  font-family: var(--font-family);
  transition: color 0.3s;
  white-space: nowrap;
  margin-top: 2px;
}

.back-btn:hover {
  color: var(--ink-black);
}

.page-title {
  font-size: 24px;
  font-weight: 400;
  letter-spacing: 0.2em;
  margin: 0 0 8px 0;
  color: var(--ink-black);
}

.page-meta {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.meta-tag {
  font-size: 12px;
  padding: 2px 12px;
  border: 1px solid var(--line-raw);
  letter-spacing: 0.1em;
  background: var(--paper-card);
}

.meta-tag.type.joint {
  border-color: #4A6CF7;
  color: #4A6CF7;
}
.meta-tag.type.relay {
  border-color: #FF9800;
  color: #FF9800;
}
.meta-tag.type.project {
  border-color: #9C27B0;
  color: #9C27B0;
}
.meta-tag.type.free {
  border-color: #4CAF50;
  color: #4CAF50;
}
.meta-tag.type.other {
  border-color: #9E9E9E;
  color: #9E9E9E;
}

.meta-tag.status.open {
  border-color: #4CAF50;
  color: #4CAF50;
}
.meta-tag.status.closed {
  border-color: #FF9800;
  color: #FF9800;
}
.meta-tag.status.ended {
  border-color: #9E9E9E;
  color: #9E9E9E;
}
.meta-tag.status.banned {
  border-color: #F44336;
  color: #F44336;
}
.meta-tag.status.abandoned {
  border-color: #795548;
  color: #795548;
}

/* ⭐ 过期标签 */
.meta-tag.expired {
  border-color: #F44336;
  color: #F44336;
  background: #fff5f5;
}

.meta-tag.audit {
  border-color: var(--line-raw);
  color: var(--ink-gray);
}
.meta-tag.audit.required {
  border-color: var(--cinnabar);
  color: var(--cinnabar);
}

.meta-tag.source.user {
  border-color: #6c7a89;
  color: #6c7a89;
}
.meta-tag.source.official {
  border-color: #9E2A2B;
  color: #9E2A2B;
}

.meta-tag.approval.pending {
  border-color: #FF9800;
  color: #FF9800;
}
.meta-tag.approval.approved {
  border-color: #4CAF50;
  color: #4CAF50;
}
.meta-tag.approval.rejected {
  border-color: #F44336;
  color: #F44336;
}

.cover-area {
  border: 1px solid var(--line-raw);
  overflow: hidden;
  margin-bottom: 28px;
  background: var(--paper-sub);
}

.cover-area img {
  width: 100%;
  max-height: 360px;
  object-fit: cover;
  display: block;
}

.detail-grid {
  display: grid;
  grid-template-columns: 280px 1fr;
  gap: 40px;
  margin-bottom: 40px;
}

.detail-left {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.info-item {
  display: flex;
  flex-direction: column;
  padding: 8px 0;
  border-bottom: 1px solid var(--line-raw);
}

.info-item:last-child {
  border-bottom: none;
}

.info-label {
  font-size: 11px;
  color: var(--ink-light);
  letter-spacing: 0.1em;
  text-transform: uppercase;
}

.info-value {
  font-size: 15px;
  color: var(--ink-black);
  letter-spacing: 0.05em;
  margin-top: 2px;
}

/* ⭐ 过期警告 */
.expired-warning {
  background: #fff5f5;
  border-left: 3px solid #F44336;
  padding-left: 12px;
}

.expired-text {
  color: #F44336 !important;
}

/* ⭐ 倒计时样式 */
.countdown-normal {
  color: #4CAF50;
}

.countdown-warning {
  color: #FF9800;
}

.countdown-urgent {
  color: #F44336;
  animation: pulse 1.5s ease-in-out infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

.detail-right {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.desc-section {
  border-bottom: 1px solid var(--line-raw);
  padding-bottom: 20px;
}

.desc-section:last-of-type {
  border-bottom: none;
}

.desc-title {
  font-size: 15px;
  font-weight: 400;
  letter-spacing: 0.2em;
  margin: 0 0 8px 0;
  color: var(--ink-black);
}

.desc-text {
  font-size: 14px;
  line-height: 1.8;
  color: var(--ink-gray);
  letter-spacing: 0.08em;
  margin: 0;
  white-space: pre-wrap;
  word-break: break-word;
}

.participants-section {
  border-top: 1px solid var(--line-raw);
  padding-top: 28px;
}

.section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
}

.section-title {
  font-size: 16px;
  font-weight: 400;
  letter-spacing: 0.2em;
  margin: 0;
  color: var(--ink-black);
}

.section-count {
  font-size: 13px;
  color: var(--ink-gray);
  letter-spacing: 0.1em;
}

.participant-actions {
  display: flex;
  gap: 12px;
  align-items: center;
  margin-bottom: 20px;
  padding-bottom: 16px;
  border-bottom: 1px solid var(--line-raw);
}

.btn-join {
  border-color: var(--cinnabar);
  color: var(--cinnabar);
  padding: 8px 28px;
  background: none;
  font-family: var(--font-family);
  font-size: 13px;
  letter-spacing: 0.15em;
  cursor: pointer;
  transition: all 0.3s ease;
}

.btn-join:hover {
  background: var(--cinnabar);
  color: #fff;
}

.btn-join:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-cancel {
  border-color: var(--ink-gray);
  color: var(--ink-gray);
  padding: 8px 28px;
  background: none;
  font-family: var(--font-family);
  font-size: 13px;
  letter-spacing: 0.15em;
  cursor: pointer;
  transition: all 0.3s ease;
}

.btn-cancel:hover {
  border-color: var(--cinnabar);
  color: var(--cinnabar);
}

.btn-cancel:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.organizer-tip {
  font-size: 13px;
  color: var(--ink-light);
  letter-spacing: 0.1em;
}

.expired-tip {
  font-size: 13px;
  color: #F44336;
  letter-spacing: 0.1em;
}

.participant-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.participant-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 12px;
  border: 1px solid var(--line-raw);
  background: var(--paper-card);
}

.participant-name {
  font-size: 14px;
  color: var(--ink-black);
  letter-spacing: 0.1em;
  flex: 1;
}

.participant-status {
  font-size: 12px;
  padding: 1px 10px;
  border: 1px solid var(--line-raw);
  letter-spacing: 0.1em;
}

.participant-status.pending {
  border-color: #FF9800;
  color: #FF9800;
}
.participant-status.approved {
  border-color: #4CAF50;
  color: #4CAF50;
}
.participant-status.rejected {
  border-color: #F44336;
  color: #F44336;
}

.participant-actions-admin {
  display: flex;
  gap: 4px;
}

.btn-sm {
  padding: 2px 10px;
  border: 1px solid var(--line-raw);
  background: none;
  font-family: var(--font-family);
  font-size: 11px;
  cursor: pointer;
  letter-spacing: 0.1em;
  transition: all 0.3s;
}

.btn-sm.approve {
  border-color: #4CAF50;
  color: #4CAF50;
}
.btn-sm.approve:hover {
  background: #4CAF50;
  color: #fff;
}

.btn-sm.reject {
  border-color: #F44336;
  color: #F44336;
}
.btn-sm.reject:hover {
  background: #F44336;
  color: #fff;
}

.btn-sm.kick {
  border-color: #FF9800;
  color: #FF9800;
}
.btn-sm.kick:hover {
  background: #FF9800;
  color: #fff;
}

.participant-empty {
  padding: 20px 0;
  text-align: center;
  color: var(--ink-light);
  font-size: 14px;
  letter-spacing: 0.1em;
}

.btn-line {
  background: none;
  border: 1px solid var(--line-raw);
  color: var(--ink-black);
  padding: 6px 16px;
  font-family: var(--font-family);
  font-size: 13px;
  letter-spacing: 0.15em;
  cursor: pointer;
  transition: all 0.3s ease;
  text-decoration: none;
  display: inline-block;
}

.btn-line:hover {
  border-color: var(--cinnabar);
  color: var(--cinnabar);
}

.btn-line.danger {
  border-color: #F44336;
  color: #F44336;
}

.btn-line.danger:hover {
  background: #F44336;
  color: #fff;
}

@media (max-width: 860px) {
  .detail-grid {
    grid-template-columns: 1fr;
    gap: 24px;
  }

  .page-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 12px;
  }

  .header-left {
    flex-wrap: wrap;
  }

  .header-right {
    width: 100%;
  }

  .cover-area img {
    max-height: 200px;
  }

  .participant-item {
    flex-wrap: wrap;
  }

  .participant-actions-admin {
    width: 100%;
    justify-content: flex-end;
  }
}

@media (max-width: 480px) {
  .joint-detail {
    padding: 16px 12px 40px;
  }

  .page-title {
    font-size: 20px;
  }

  .participant-item {
    flex-direction: column;
    align-items: flex-start;
    gap: 6px;
  }

  .participant-actions-admin {
    width: 100%;
    justify-content: flex-start;
  }
}
</style>