<template>
  <div class="joint-card" @click="emit('click', activity.id)">
    <!-- 封面图 -->
    <div class="card-cover">
      <img
        v-if="activity.coverUrl"
        :src="activity.coverUrl"
        :alt="activity.title"
        loading="lazy"
      />
      <div v-else class="cover-placeholder">
        <span>📋</span>
      </div>

      <!-- 左上角：来源标签 -->
      <span class="source-badge" :class="activity.organizerType">
        {{ activity.organizerType === 'official' ? '官方' : '用户' }}
      </span>

      <!-- 右上角：状态标签（使用 displayStatus） -->
      <span class="status-badge" :class="displayStatus">
        {{ statusLabel(displayStatus) }}
      </span>

      <!-- 用户自建：审批状态标签 -->
      <span
        v-if="activity.organizerType === 'user'"
        class="approval-badge"
        :class="activity.approvalStatus"
      >
        {{ approvalStatusLabel(activity.approvalStatus) }}
      </span>

      <!-- ⭐ 倒计时标签（仅当活动未结束且有结束时间） -->
      <span v-if="showCountdown" class="countdown-badge" :class="countdownClass">
        {{ countdownText }}
      </span>
    </div>

    <!-- 信息区 -->
    <div class="card-body">
      <h3 class="card-title">{{ activity.title }}</h3>
      <p class="card-desc">{{ truncateText(activity.description, 60) }}</p>

      <!-- ⭐ 活动时间 -->
      <div class="card-time">
        <span class="time-icon">📅</span>
        <span class="time-text">{{ formatDate(activity.startDate) }}</span>
        <span v-if="activity.endDate" class="time-arrow">→</span>
        <span v-if="activity.endDate" class="time-text">{{ formatDate(activity.endDate) }}</span>
      </div>

      <div class="card-meta">
        <span class="meta-type">{{ typeLabel(activity.type) }}</span>
        <span class="meta-count">👥 {{ activity.participantCount }} 人</span>
        <span v-if="activity.organizerType === 'official'" class="meta-official">官方</span>
      </div>

      <div class="card-footer">
        <span class="organizer">举办者：{{ activity.organizerName }}</span>
        <span v-if="activity.auditRequired" class="audit-tag">需审核</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { JointActivity, JointStatus } from '../joint'

const props = defineProps<{
  activity: JointActivity
}>()

const emit = defineEmits<{
  click: [id: string]
}>()

// ===== 判断活动是否已过期 =====
const isExpired = computed(() => {
  if (!props.activity.endDate) return false
  return new Date(props.activity.endDate) < new Date()
})

// ===== 实际显示的状态（如果过期则强制显示为 'ended'） =====
const displayStatus = computed<JointStatus>(() => {
  if (isExpired.value && props.activity.status !== 'ended') {
    return 'ended'
  }
  return props.activity.status
})

// ===== 是否显示倒计时（仅当未过期且有结束时间） =====
const showCountdown = computed(() => {
  return !isExpired.value && props.activity.endDate
})

// ===== 计算剩余天数 =====
const daysRemaining = computed(() => {
  if (!props.activity.endDate) return 0
  const now = new Date()
  const end = new Date(props.activity.endDate)
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

function approvalStatusLabel(status?: string): string {
  const map: Record<string, string> = {
    pending: '审核中',
    approved: '已通过',
    rejected: '已拒绝',
  }
  return status ? map[status] || status : ''
}

function truncateText(text: string, maxLength: number): string {
  if (text.length <= maxLength) return text
  return text.slice(0, maxLength) + '...'
}

// ===== ⭐ 格式化日期（短格式） =====
function formatDate(dateStr: string): string {
  const d = new Date(dateStr)
  return d.toLocaleDateString('zh-CN', {
    month: 'short',
    day: 'numeric',
  })
}
</script>

<style scoped>
.joint-card {
  border: 1px solid var(--line-raw);
  background: var(--paper-card);
  cursor: pointer;
  transition: all 0.3s ease;
  overflow: hidden;
}

.joint-card:hover {
  border-color: var(--ink-black);
  transform: translateY(-2px);
}

/* ===== 封面 ===== */
.card-cover {
  position: relative;
  width: 100%;
  aspect-ratio: 16/9;
  background: var(--paper-sub);
  overflow: hidden;
}

.card-cover img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.cover-placeholder {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 32px;
  color: var(--ink-light);
}

/* ===== 标签通用 ===== */
.status-badge,
.source-badge,
.approval-badge,
.countdown-badge {
  position: absolute;
  padding: 2px 10px;
  font-size: 11px;
  letter-spacing: 0.1em;
  color: #fff;
  border-radius: 2px;
}

/* 左上角：来源标签 */
.source-badge {
  top: 10px;
  left: 10px;
}

.source-badge.user {
  background: #6c7a89;
}

.source-badge.official {
  background: #9E2A2B;
}

/* 右上角：状态标签 */
.status-badge {
  top: 10px;
  right: 10px;
  background: rgba(44, 42, 41, 0.75);
}

.status-badge.open {
  background: #4CAF50;
}
.status-badge.closed {
  background: #FF9800;
}
.status-badge.ended {
  background: #9E9E9E;
}
.status-badge.banned {
  background: #F44336;
}
.status-badge.abandoned {
  background: #795548;
}

/* 右上角偏下：审批状态标签（仅用户自建） */
.approval-badge {
  top: 40px;
  right: 10px;
}

.approval-badge.pending {
  background: #FF9800;
}
.approval-badge.approved {
  background: #4CAF50;
}
.approval-badge.rejected {
  background: #F44336;
}

/* ===== ⭐ 倒计时标签 ===== */
.countdown-badge {
  bottom: 10px;
  right: 10px;
  background: rgba(0, 0, 0, 0.7);
  font-size: 12px;
  padding: 4px 12px;
  backdrop-filter: blur(4px);
}

.countdown-badge.countdown-normal {
  background: rgba(44, 42, 41, 0.7);
}

.countdown-badge.countdown-warning {
  background: rgba(255, 152, 0, 0.85);
}

.countdown-badge.countdown-urgent {
  background: rgba(244, 67, 54, 0.85);
  animation: pulse 1.5s ease-in-out infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.6; }
}

/* ===== 信息区 ===== */
.card-body {
  padding: 14px 16px 16px;
}

.card-title {
  font-size: 16px;
  font-weight: 400;
  letter-spacing: 0.15em;
  margin: 0 0 6px 0;
  color: var(--ink-black);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.card-desc {
  font-size: 13px;
  color: var(--ink-gray);
  letter-spacing: 0.08em;
  line-height: 1.6;
  margin: 0 0 10px 0;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

/* ===== ⭐ 活动时间 ===== */
.card-time {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--ink-light);
  letter-spacing: 0.08em;
  padding: 4px 0 8px 0;
  border-bottom: 1px solid var(--line-raw);
  margin-bottom: 8px;
}

.time-icon {
  font-size: 13px;
}
.time-arrow {
  color: var(--ink-light);
}
.time-text {
  color: var(--ink-gray);
}

.card-meta {
  display: flex;
  gap: 12px;
  font-size: 12px;
  color: var(--ink-gray);
  letter-spacing: 0.1em;
  margin-bottom: 10px;
}

.meta-type {
  padding: 1px 10px;
  border: 1px solid var(--line-raw);
}

.meta-official {
  padding: 1px 10px;
  border: 1px solid var(--cinnabar);
  color: var(--cinnabar);
}

.meta-count {
  display: flex;
  align-items: center;
  gap: 4px;
}

.card-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-top: 10px;
  border-top: 1px solid var(--line-raw);
  font-size: 12px;
  color: var(--ink-light);
  letter-spacing: 0.1em;
}

.organizer {
  color: var(--ink-gray);
}

.audit-tag {
  padding: 1px 8px;
  border: 1px solid var(--cinnabar);
  color: var(--cinnabar);
  font-size: 10px;
}

/* ===== 响应式 ===== */
@media (max-width: 600px) {
  .card-body {
    padding: 12px 14px 14px;
  }

  .card-title {
    font-size: 15px;
  }

  .card-desc {
    font-size: 12px;
  }

  .status-badge,
  .source-badge,
  .approval-badge,
  .countdown-badge {
    font-size: 10px;
    padding: 1px 8px;
  }

  .approval-badge {
    top: 36px;
  }

  .countdown-badge {
    font-size: 10px;
    padding: 2px 8px;
  }
}
</style>