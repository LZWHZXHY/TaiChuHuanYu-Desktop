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
              <span class="meta-tag status" :class="activity.status">{{ statusLabel(activity.status) }}</span>
              <span class="meta-tag audit" :class="{ required: activity.auditRequired }">
                {{ activity.auditRequired ? '需要审核' : '直接加入' }}
              </span>
            </div>
          </div>
        </div>
        <div class="header-right">
          <!-- 举办者编辑按钮 -->
          <router-link
            v-if="isOrganizer"
            :to="`/joint/edit/${activity.id}`"
            class="btn-line"
          >
            ✎ 编辑
          </router-link>
          <!-- 管理员封禁按钮（预留） -->
          <button
            v-if="isAdmin"
            class="btn-line danger"
            @click="handleToggleBan"
          >
            {{ activity.status === 'banned' ? '解封' : '封禁' }}
          </button>
        </div>
      </div>

      <!-- 封面图 -->
      <div v-if="activity.coverUrl" class="cover-area">
        <img :src="activity.coverUrl" :alt="activity.title" />
      </div>

      <!-- 详情网格 -->
      <div class="detail-grid">
        <!-- 左侧：基本信息 -->
        <div class="detail-left">
          <!-- 举办者 -->
          <div class="info-item">
            <span class="info-label">举办者</span>
            <span class="info-value">{{ activity.organizerName }}</span>
          </div>

          <!-- 群聊号 -->
          <div v-if="activity.contact" class="info-item">
            <span class="info-label">群聊号</span>
            <span class="info-value">{{ activity.contact }}</span>
          </div>

          <!-- 参与人数 -->
          <div class="info-item">
            <span class="info-label">参与人数</span>
            <span class="info-value">{{ activity.participantCount }} 人</span>
          </div>

          <!-- 创建时间 -->
          <div class="info-item">
            <span class="info-label">创建时间</span>
            <span class="info-value">{{ formatDate(activity.createdAt) }}</span>
          </div>
        </div>

        <!-- 右侧：描述 -->
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

      <!-- ===== 参与者列表 ===== -->
      <div class="participants-section">
        <div class="section-header">
          <h3 class="section-title">参与者</h3>
          <span class="section-count">{{ activity.participantCount }} 人</span>
        </div>

        <!-- 报名按钮 -->
        <div class="participant-actions">
          <!-- 未报名：显示报名按钮 -->
          <button
            v-if="!isParticipant && !isOrganizer"
            class="btn-line btn-join"
            :disabled="joining"
            @click="handleJoin"
          >
            {{ joining ? '报名中...' : '报名参与' }}
          </button>

          <!-- 已报名：显示取消报名 -->
          <button
            v-if="isParticipant"
            class="btn-line btn-cancel"
            :disabled="cancelling"
            @click="handleCancelJoin"
          >
            {{ cancelling ? '取消中...' : '取消报名' }}
          </button>

          <!-- 举办者提示 -->
          <span v-if="isOrganizer" class="organizer-tip">（你举办的活动）</span>
        </div>

        <!-- 参与者列表 -->
        <div v-if="activity.participants?.length" class="participant-list">
          <div
            v-for="p in activity.participants"
            :key="p.id"
            class="participant-item"
          >
            <span class="participant-name">{{ p.userName }}</span>

            <!-- 审核状态 -->
            <span class="participant-status" :class="p.status">
              {{ participantStatusLabel(p.status) }}
            </span>

            <!-- 举办者操作 -->
            <div v-if="isOrganizer" class="participant-actions-admin">
              <!-- 待审核：通过/拒绝 -->
              <template v-if="p.status === 'pending'">
                <button
                  class="btn-sm approve"
                  @click="handleApprove(p.userId)"
                >
                  通过
                </button>
                <button
                  class="btn-sm reject"
                  @click="handleReject(p.userId)"
                >
                  拒绝
                </button>
              </template>

              <!-- 已通过：可踢出 -->
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
import type { JointActivity, ParticipantStatus } from '../joint'

const router = useRouter()
const route = useRoute()
const store = useJointStore()
const userStore = useUserStore()

// ===== 状态 =====
const joining = ref(false)
const cancelling = ref(false)

// ===== 计算属性 =====
const activity = computed(() => store.currentActivity)
const loading = computed(() => store.loading)

const isOrganizer = computed(() =>
  activity.value?.organizerId === userStore.userInfo?.id
)

const isAdmin = computed(() =>
  userStore.userInfo?.permissions?.includes('SuperAdmin') ?? false
)

const isParticipant = computed(() => {
  if (!activity.value?.participants) return false
  return activity.value.participants.some(p => p.userId === userStore.userInfo?.id)
})

// ===== 标签映射 =====
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

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString('zh-CN', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  })
}

// ===== 返回 =====
function goBack() {
  router.push('/joint')
}

// ===== 报名 =====
async function handleJoin() {
  if (!activity.value) return
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

// ===== 取消报名 =====
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

// ===== 审核参与者（举办者） =====
async function handleApprove(userId: string) {
  if (!activity.value) return
  try {
    await store.auditParticipant(activity.value.id, userId, 'approved')
    // 刷新详情
    await store.fetchDetail(activity.value.id)
  } catch (error) {
    console.error('审核失败:', error)
    alert('操作失败，请重试')
  }
}

async function handleReject(userId: string) {
  if (!activity.value) return
  try {
    await store.auditParticipant(activity.value.id, userId, 'rejected')
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

// ===== 封禁/解封（管理员） =====
async function handleToggleBan() {
  if (!activity.value) return
  const action = activity.value.status === 'banned' ? '解封' : '封禁'
  if (!confirm(`确定要${action}该联合活动吗？`)) return
  try {
    // 调用封禁/解封 API（需要后端实现）
    // await store.toggleBan(activity.value.id)
    alert(`${action}功能开发中...`)
  } catch (error) {
    console.error('操作失败:', error)
  }
}

// ===== 生命周期 =====
onMounted(async () => {
  const id = route.params.id as string
  await store.fetchDetail(id)
})
</script>

<style scoped>
.joint-detail {
  max-width: 1100px;
  margin: 0 auto;
  padding: 32px 24px 60px;
  background: var(--paper-bg);
  min-height: 100vh;
}

/* ===== 加载状态 ===== */
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

/* ===== 空状态 ===== */
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

/* ===== 页面头部 ===== */
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

.meta-tag.audit {
  border-color: var(--line-raw);
  color: var(--ink-gray);
}
.meta-tag.audit.required {
  border-color: var(--cinnabar);
  color: var(--cinnabar);
}

/* ===== 封面图 ===== */
.cover-area {
  border: 1px solid var(--line-raw);
  overflow: hidden;
  margin-bottom: 28px;
}

.cover-area img {
  width: 100%;
  max-height: 360px;
  object-fit: cover;
  display: block;
}

/* ===== 详情网格 ===== */
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
  padding-bottom: 0;
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

/* ===== 参与者区域 ===== */
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
}

.btn-join:hover {
  background: var(--cinnabar);
  color: #fff;
}

.btn-cancel {
  border-color: var(--ink-gray);
  color: var(--ink-gray);
  padding: 8px 28px;
}

.btn-cancel:hover {
  border-color: var(--cinnabar);
  color: var(--cinnabar);
}

.btn-join:disabled,
.btn-cancel:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.organizer-tip {
  font-size: 13px;
  color: var(--ink-light);
  letter-spacing: 0.1em;
}

/* ===== 参与者列表 ===== */
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

/* ===== 按钮统一样式 ===== */
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

/* ===== 响应式 ===== */
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