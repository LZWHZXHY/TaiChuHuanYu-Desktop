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

      <!-- 右上角：状态标签 -->
      <span class="status-badge" :class="activity.status">
        {{ statusLabel(activity.status) }}
      </span>

      <!-- 用户自建：审批状态标签 -->
      <span
        v-if="activity.organizerType === 'user'"
        class="approval-badge"
        :class="activity.approvalStatus"
      >
        {{ approvalStatusLabel(activity.approvalStatus) }}
      </span>
    </div>

    <!-- 信息区 -->
    <div class="card-body">
      <h3 class="card-title">{{ activity.title }}</h3>
      <p class="card-desc">{{ truncateText(activity.description, 60) }}</p>

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
import type { JointActivity } from '../joint'

const props = defineProps<{
  activity: JointActivity
}>()

const emit = defineEmits<{
  click: [id: string]
}>()

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
.approval-badge {
  position: absolute;
  padding: 2px 10px;
  font-size: 11px;
  letter-spacing: 0.1em;
  color: #fff;
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
  .approval-badge {
    font-size: 10px;
    padding: 1px 8px;
  }

  .approval-badge {
    top: 36px;
  }
}
</style>