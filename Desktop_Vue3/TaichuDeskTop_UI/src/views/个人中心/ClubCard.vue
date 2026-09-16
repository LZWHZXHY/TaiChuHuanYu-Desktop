<!-- ClubCard.vue -->
<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import request from '../../utils/request'

interface BossStats {
  totalOrders: number
  completedOrders: number
  cancelledOrders: number
  processingOrders: number
  totalSpent: number
}

interface GameSkill {
  gameCode: string
  gameName: string
  level: string | null
  code: string | null
  auditStatus: string
}

interface OperatorInfo {
  nickname: string
  auditStatus: string
  reputation: number
  totalOrders: number
  completedOrders: number
  reviewCount: number
  avgOverall: number
  games: GameSkill[]
}

const loading = ref(true)
const error = ref('')

const boss = ref<BossStats>({
  totalOrders: 0,
  completedOrders: 0,
  cancelledOrders: 0,
  processingOrders: 0,
  totalSpent: 0
})
const operatorInfo = ref<OperatorInfo | null>(null)

const hasApplied = computed(() => !!operatorInfo.value)

const approvedGames = computed(() =>
  (operatorInfo.value?.games || []).filter(g => g.auditStatus === 'approved')
)

const isApprovedOperator = computed(() => approvedGames.value.length > 0)

const topLevel = computed(() => {
  const levels = approvedGames.value
    .map(g => g.level)
    .filter(Boolean)
    .sort()
    .reverse()
  return levels[0] || 'L1'
})

const loadSummary = async () => {
  loading.value = true
  error.value = ''
  try {
    const res: any = await request.get('/club/profile/summary')
    const data = res?.data ?? res
    boss.value = data?.boss ?? boss.value
    operatorInfo.value = data?.operator ?? null
  } catch (e: any) {
    error.value = e?.response?.data?.message || e?.message || '加载失败'
  } finally {
    loading.value = false
  }
}

onMounted(loadSummary)
</script>

<template>
  <div class="club-card">
    <header class="cc-header">
      <div class="cc-title-wrap">
        <h3>太初俱乐部</h3>
        <span class="cc-sub">TACTICAL ESPORTS CLUB</span>
      </div>
      <span class="cc-badge">TC</span>
    </header>

    <div v-if="loading" class="cc-state">加载中...</div>
    <div v-else-if="error" class="cc-state error">{{ error }}</div>

    <template v-else>
      <!-- 身份 -->
      <div class="cc-roles">
        <span class="role boss">老板</span>
        <span v-if="isApprovedOperator" class="role operator">
          打手 · {{ topLevel }}
        </span>
        <span v-else-if="hasApplied" class="role pending">打手申请中</span>
      </div>

      <!-- 快捷入口 -->
      <div class="cc-grid">
        <router-link to="/taichu/orders" class="cc-item">
          <span class="cc-item-label">我的订单</span>
          <span class="cc-item-value">
            {{ boss.totalOrders }}
            <small v-if="boss.processingOrders > 0" class="cc-dot">
              · {{ boss.processingOrders }} 进行中
            </small>
          </span>
        </router-link>

        <router-link
          v-if="hasApplied"
          to="/taichu/operator/orders"
          class="cc-item"
        >
          <span class="cc-item-label">打手订单</span>
          <span class="cc-item-value">{{ operatorInfo?.totalOrders || 0 }}</span>
        </router-link>

        <div class="cc-item disabled">
          <span class="cc-item-label">钱包</span>
          <span class="cc-item-value muted">即将上线</span>
        </div>

        <div class="cc-item disabled">
          <span class="cc-item-label">评价</span>
          <span class="cc-item-value muted">
            {{ operatorInfo?.reviewCount || 0 }} 条
          </span>
        </div>
      </div>

      <router-link to="/taichu/profile" class="cc-enter">
        进入俱乐部
        <span>→</span>
      </router-link>
    </template>
  </div>
</template>

<style scoped>
.club-card {
  background: #fff;
  border: 1px solid #f0f0f0;
  border-radius: 12px;
  padding: 20px 22px;
  animation: fadeIn 0.4s ease;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(5px); }
  to { opacity: 1; transform: translateY(0); }
}

/* 头部 */
.cc-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  padding-bottom: 14px;
  margin-bottom: 16px;
  border-bottom: 1px dashed #eaeef2;
}
.cc-title-wrap h3 {
  margin: 0 0 4px;
  font-size: 1rem;
  font-weight: 700;
  color: #24292f;
}
.cc-sub {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  font-size: 0.65rem;
  letter-spacing: 1.5px;
  color: #8c959f;
}
.cc-badge {
  flex-shrink: 0;
  width: 32px;
  height: 32px;
  display: grid;
  place-items: center;
  border: 1px solid #24292f;
  border-radius: 6px;
  color: #24292f;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  font-size: 0.75rem;
  font-weight: 700;
}

/* 状态 */
.cc-state {
  padding: 20px;
  text-align: center;
  font-size: 0.85rem;
  color: #8c959f;
}
.cc-state.error { color: #cf222e; }

/* 身份标签 */
.cc-roles {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-bottom: 16px;
}
.role {
  padding: 3px 10px;
  font-size: 0.72rem;
  font-weight: 600;
  letter-spacing: 0.5px;
  border-radius: 4px;
  border: 1px solid;
}
.role.boss {
  color: #cf5e00;
  border-color: rgba(207, 94, 0, 0.3);
  background: rgba(207, 94, 0, 0.06);
}
.role.operator {
  color: #0969da;
  border-color: rgba(9, 105, 218, 0.3);
  background: rgba(9, 105, 218, 0.06);
}
.role.pending {
  color: #8c959f;
  border-color: #eaeef2;
  background: #f6f8fa;
}

/* 快捷入口 */
.cc-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
  margin-bottom: 16px;
}
.cc-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 10px 12px;
  border: 1px solid #eaeef2;
  border-radius: 8px;
  background: #fafbfc;
  text-decoration: none;
  color: inherit;
  transition: all 0.18s ease;
  cursor: pointer;
}
.cc-item:hover:not(.disabled) {
  border-color: #24292f;
  background: #fff;
}
.cc-item.disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
.cc-item-label {
  font-size: 0.7rem;
  color: #8c959f;
  letter-spacing: 0.5px;
}
.cc-item-value {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  font-size: 1.05rem;
  font-weight: 700;
  color: #24292f;
}
.cc-item-value.muted {
  font-size: 0.75rem;
  font-weight: 400;
  color: #8c959f;
}
.cc-dot {
  font-size: 0.7rem;
  color: #cf5e00;
  font-weight: 400;
}

/* 进入按钮 */
.cc-enter {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  height: 38px;
  font-size: 0.85rem;
  font-weight: 600;
  letter-spacing: 0.5px;
  color: #fff;
  background: #24292f;
  border-radius: 8px;
  text-decoration: none;
  transition: opacity 0.2s;
}
.cc-enter:hover { opacity: 0.85; }
.cc-enter span {
  font-size: 1.1rem;
  font-weight: 400;
}
</style>