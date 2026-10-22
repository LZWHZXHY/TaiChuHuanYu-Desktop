<template>
  <div class="page-shell">
    <Navbar />

    <main>
      <section class="page-hero">
        <div class="section-container">
          <SectionHeading index="01" title="PROFILE" subtitle="个人中心 / MY CENTER" />
        </div>
      </section>

      <section class="section-block alt">
        <div class="section-container">

          <div v-if="!isLoggedIn" class="op-state">
            请先登录后查看个人中心
            <div style="margin-top: 16px;">
              <router-link to="/LoginRegister" class="btn-link">前往登录 →</router-link>
            </div>
          </div>

          <div v-else-if="loading" class="op-state">
            <span class="dot"></span> 正在读取个人信息…
          </div>

          <div v-else-if="loadError" class="op-state">
            加载失败：{{ loadError }}
            <div style="margin-top: 16px;">
              <button class="btn-link" @click="reload">重试</button>
            </div>
          </div>

          <template v-else>

            <!-- ============ 身份卡 ============ -->
            <div class="identity-card">
              <div class="ic-avatar">{{ avatarChar }}</div>
              <div class="ic-main">
                <h2 class="ic-name">{{ displayName }}</h2>
                <div class="ic-tags">
                  <span class="ic-tag boss">老板</span>
                  <span v-if="isApprovedOperator" class="ic-tag operator">
                    打手 · {{ topLevel }}
                  </span>
                  <span v-else-if="hasApplied" class="ic-tag pending">
                    打手申请中
                  </span>
                  <!-- ⭐ 客服标签 -->
                  <span v-if="isApprovedCs" class="ic-tag cs">
                    客服
                  </span>
                  <span v-else-if="hasCsApplied && !hasApplied" class="ic-tag pending">
                    客服申请中
                  </span>
                </div>
                <p class="ic-uid mono">UID: {{ shortId(userId) }}</p>
              </div>
            </div>

            <!-- ============ 快捷入口 ============ -->
            <div class="entry-grid">
              <router-link to="/taichu/orders" class="entry-card">
                <div class="ec-icon">📋</div>
                <div class="ec-title">我的订单</div>
                <div class="ec-count">
                  {{ boss.totalOrders }}
                  <small v-if="boss.processingOrders > 0" class="ec-dot">
                    · {{ boss.processingOrders }} 进行中
                  </small>
                </div>
              </router-link>

              <router-link
                v-if="hasApplied"
                to="/taichu/operator/orders"
                class="entry-card"
              >
                <div class="ec-icon">🎯</div>
                <div class="ec-title">打手订单</div>
                <div class="ec-count">
                  {{ operatorInfo.totalOrders }}
                </div>
              </router-link>

              <router-link
                v-if="isApprovedOperator"
                to="/taichu/operator/wallet"
                class="entry-card"
              >
                <div class="ec-icon">💰</div>
                <div class="ec-title">我的钱包</div>
                <div class="ec-count">¥{{ walletBalance.toFixed(2) }}</div>
              </router-link>
              <div v-else class="entry-card disabled">
                <div class="ec-icon">💰</div>
                <div class="ec-title">我的钱包</div>
                <div class="ec-count muted">仅打手可见</div>
              </div>

                           <!-- ⭐ 我的待评价（只有打手/老板看得到，客服隐藏） -->
                            <!-- ⭐ 我作为老板的待评价（所有身份都能看到） -->
              <router-link to="/taichu/operator/reviews" class="entry-card">
                <div class="ec-icon">⭐</div>
                <div class="ec-title">我下的单·待评价</div>
                <div class="ec-count">
                  {{ pendingReviewCount }} 条
                </div>
              </router-link>
            </div>

            <!-- ============ 老板数据 ============ -->
            <section class="stat-section">
              <h3 class="ss-title">作为老板</h3>
              <div class="stat-grid">
                <div class="stat-cell">
                  <span class="sc-label">累计下单</span>
                  <b class="sc-value">{{ boss.totalOrders }}</b>
                </div>
                <div class="stat-cell">
                  <span class="sc-label">已完成</span>
                  <b class="sc-value">{{ boss.completedOrders }}</b>
                </div>
                <div class="stat-cell">
                  <span class="sc-label">累计消费</span>
                  <b class="sc-value accent">¥{{ boss.totalSpent }}</b>
                </div>
                <div class="stat-cell">
                  <span class="sc-label">进行中</span>
                  <b class="sc-value">{{ boss.processingOrders }}</b>
                </div>
              </div>
            </section>

            <!-- ============ ⭐ 客服身份 ============ -->
            <!-- 打手身份下不显示（互斥） -->
            <section v-if="!hasApplied && !loadingCs" class="stat-section">
              <h3 class="ss-title">客服身份</h3>

              <!-- 未申请 -->
              <router-link
                v-if="!hasCsApplied"
                to="/taichu/cs/register"
                class="cs-entry-card"
              >
                <div class="cec-left">
                  <div class="cec-icon">🎧</div>
                  <div class="cec-info">
                    <div class="cec-title">申请成为客服</div>
                    <div class="cec-desc">
                      成为太初客服，接待老板、指派打手、协助处理订单
                    </div>
                  </div>
                </div>
                <div class="cec-arrow">→</div>
              </router-link>

              <!-- 已申请：显示状态 -->
              <router-link
                v-else
                to="/taichu/cs/register"
                class="cs-entry-card"
                :class="csAuditStatus"
              >
                <div class="cec-left">
                  <div class="cec-icon">
                    {{ csAuditStatus === 'approved' ? '🎧' : '⏳' }}
                  </div>
                  <div class="cec-info">
                    <div class="cec-title">{{ csTitle }}</div>
                    <div class="cec-desc">{{ csSub }}</div>
                  </div>
                </div>
                <div class="cec-arrow">→</div>
              </router-link>
            </section>

            <!-- ============ 打手数据 ============ -->
            <section v-if="hasApplied" class="stat-section">
              <h3 class="ss-title">
                作为打手
                <span v-if="!isApprovedOperator" class="ss-note">
                  （{{ operatorAuditLabel }}）
                </span>
              </h3>

              <template v-if="isApprovedOperator">

                <!-- ⭐ 在线状态卡片 -->
                <div class="status-card" :class="profileStatus">
                  <div class="sc-left">
                    <OnlineStatusBadge :status="profileStatus" />
                    <div class="sc-text">
                      <div class="sc-title">{{ statusTitle }}</div>
                      <div class="sc-sub">
                        {{ statusSub }}
                        <template v-if="lastSeenText">
                          · 最后活跃 {{ lastSeenText }}
                        </template>
                      </div>
                    </div>
                  </div>

                  <div class="sc-actions">
                    <button
                      class="sc-btn"
                      :class="{ on: profileStatus === 'online' }"
                      :disabled="statusChanging"
                      @click="switchStatus('online')"
                    >
                      在线
                    </button>
                    <button
                      class="sc-btn"
                      :class="{ on: profileStatus === 'resting' }"
                      :disabled="statusChanging"
                      @click="switchStatus('resting')"
                    >
                      休息
                    </button>
                    <button
                      class="sc-btn"
                      :class="{ on: profileStatus === 'offline' }"
                      :disabled="statusChanging || busyLocked"
                      :title="busyLocked ? '有进行中的订单，无法离线' : ''"
                      @click="switchStatus('offline')"
                    >
                      离线
                    </button>
                  </div>
                </div>

                <div class="stat-grid">
                  <div class="stat-cell">
                    <span class="sc-label">信誉分</span>
                    <b class="sc-value accent">{{ operatorInfo.reputation }}</b>
                  </div>
                  <div class="stat-cell">
                    <span class="sc-label">累计接单</span>
                    <b class="sc-value">{{ operatorInfo.totalOrders }}</b>
                  </div>
                  <div class="stat-cell">
                    <span class="sc-label">已完成</span>
                    <b class="sc-value">{{ operatorInfo.completedOrders }}</b>
                  </div>
                  <div class="stat-cell">
                    <span class="sc-label">综合评分</span>
                    <b class="sc-value">
                      {{ operatorInfo.reviewCount > 0 ? operatorInfo.avgOverall : '—' }}
                    </b>
                  </div>
                </div>

                <!-- 游戏列表 -->
                <div class="game-list">
                  <div
                    v-for="g in approvedGames"
                    :key="g.gameCode"
                    class="game-row"
                  >
                    <div class="gr-left">
                      <span class="gr-name">{{ g.gameName }}</span>
                      <span v-if="g.code" class="gr-code mono">{{ g.code }}</span>
                    </div>
                    <div class="gr-right">
                      <span class="gr-level">{{ g.level || 'L1' }}</span>
                      <span class="gr-stat">
                        接 {{ g.ordersInGame }} · 成 {{ g.completedOrdersInGame }}
                      </span>
                    </div>
                  </div>
                </div>
              </template>

              <div v-else class="pending-note">
                你的打手申请已提交，等待审核通过后即可在这里查看打手数据。
              </div>
            </section>

          </template>

        </div>
      </section>
    </main>

    <Footer />
  </div>
</template>

<script>
import { useUserStore } from '@/stores/user'
import Navbar            from './Navbar.vue'
import Footer            from './Footer.vue'
import SectionHeading    from './SectionHeading.vue'
import OnlineStatusBadge from './OnlineStatusBadge.vue'
import request           from '@/utils/request'

export default {
  name: 'MyProfile',
  components: { Navbar, Footer, SectionHeading, OnlineStatusBadge },

  data() {
    return {
      loading: true,
      loadError: '',

      userId: '',
      boss: {
        totalOrders: 0,
        completedOrders: 0,
        cancelledOrders: 0,
        processingOrders: 0,
        totalSpent: 0
      },
      operatorInfo: null,
      walletBalance: 0,
            pendingReviewCount: 0,
      // ⭐ 客服信息
      loadingCs: true,
      csInfo: null,

      // ⭐ 打手在线状态
      profileStatus: 'offline',
      lastSeenAt: null,
      activeOrderCount: 0,
      statusChanging: false,
      heartbeatTimer: null
    }
  },

  computed: {
    isLoggedIn() {
      return useUserStore().isLoggedIn
    },

    displayName() {
      return this.operatorInfo?.nickname
        || this.csInfo?.name
        || useUserStore().userInfo?.username
        || '太初用户'
    },

    avatarChar() {
      const n = this.displayName
      return n ? n.charAt(0).toUpperCase() : '?'
    },

    hasApplied() {
      return !!this.operatorInfo
    },

    isApprovedOperator() {
      return !!this.operatorInfo &&
        (this.operatorInfo.games || []).some(g => g.auditStatus === 'approved')
    },

    approvedGames() {
      return (this.operatorInfo?.games || [])
        .filter(g => g.auditStatus === 'approved')
    },

    topLevel() {
      const levels = this.approvedGames
        .map(g => g.level)
        .filter(Boolean)
        .sort()
        .reverse()
      return levels[0] || 'L1'
    },

    operatorAuditLabel() {
      if (!this.operatorInfo) return ''
      const s = this.operatorInfo.auditStatus
      return {
        pending:   '待审核',
        reviewing: '考核中',
        rejected:  '未通过',
        banned:    '已封禁'
      }[s] || s
    },

    // ==================================================
    //  ⭐ 客服相关
    // ==================================================
    hasCsApplied() {
      return !!this.csInfo
    },

    isApprovedCs() {
      return this.csInfo?.auditStatus === 'approved'
    },

    csAuditStatus() {
      return this.csInfo?.auditStatus || 'pending'
    },

    csTitle() {
      return {
        pending:  '客服申请审核中',
        approved: '我的客服工作台',
        rejected: '客服申请未通过',
        banned:   '客服账号已封禁'
      }[this.csAuditStatus] || '客服申请'
    },

    csSub() {
      if (this.csAuditStatus === 'approved') {
        const parts = []
        if (this.csInfo?.currentLoad !== undefined) {
          parts.push(`载单 ${this.csInfo.currentLoad}/${this.csInfo.capacity}`)
        }
        if (this.csInfo?.reviewCount > 0) {
          parts.push(`评分 ${(this.csInfo.avgOverall || 0).toFixed(1)}（${this.csInfo.reviewCount}）`)
        }
        return parts.join(' · ') || '进入工作台处理订单'
      }
      if (this.csAuditStatus === 'pending') {
        return '我们会尽快审核，请留意通知'
      }
      if (this.csAuditStatus === 'rejected') {
        return this.csInfo?.auditNote || '如有疑问请联系管理员，或修改信息后重新提交'
      }
      if (this.csAuditStatus === 'banned') {
        return '如需申诉请联系管理员'
      }
      return ''
    },

    // ==================================================
    //  打手状态
    // ==================================================
    busyLocked() {
      return this.activeOrderCount > 0
    },

    statusTitle() {
      return {
        online:  '在线中',
        busy:    '接单中',
        resting: '休息中',
        offline: '离线'
      }[this.profileStatus] || '离线'
    },

    statusSub() {
      if (this.profileStatus === 'busy') {
        return `你还有 ${this.activeOrderCount} 个进行中的订单，完成后自动回到在线`
      }
      return {
        online:  '可以接受新的订单',
        resting: '暂不接受新订单',
        offline: '不会收到新订单通知'
      }[this.profileStatus] || ''
    },

    lastSeenText() {
      if (!this.lastSeenAt) return ''
      const d = new Date(this.lastSeenAt)
      const diff = Date.now() - d.getTime()
      const min = Math.floor(diff / 60000)
      if (min < 1) return '刚刚'
      if (min < 60) return `${min} 分钟前`
      const h = Math.floor(min / 60)
      if (h < 24) return `${h} 小时前`
      return `${Math.floor(h / 24)} 天前`
    }
  },

  async mounted() {
    if (!this.isLoggedIn) {
      this.loading = false
      this.loadingCs = false
      return
    }
    this.userId = this.readUidFromToken()

    // 并行：主数据 + 客服状态
    await Promise.all([
  this.loadSummary(),
  this.loadCsInfo(),
  this.loadPendingReviewCount()
])

    this.loading = false
    this.loadingCs = false

    // ⭐ 启动心跳
    this.startHeartbeat()
  },

  beforeUnmount() {
    this.stopHeartbeat()
  },

  methods: {
    async loadSummary() {
      this.loadError = ''
      try {
        const res = await request.get('/club/profile/summary')
        const data = res?.data ?? res

        this.boss = data?.boss ?? this.boss
        this.operatorInfo = data?.operator ?? null

        // ⭐ 读取在线状态
        if (this.operatorInfo) {
          this.profileStatus = this.operatorInfo.status || 'offline'
          this.lastSeenAt = this.operatorInfo.lastSeenAt || null
          this.activeOrderCount = this.operatorInfo.activeOrderCount || 0
        }

        // 已通过打手 → 拉一次钱包余额
        if (this.isApprovedOperator) {
          try {
            const wRes = await request.get('/club/wallet', { params: { pageSize: 1 } })
            const w = wRes?.data ?? wRes
            this.walletBalance = Number(w?.balance ?? 0)
          } catch { /* 忽略 */ }
        }
      } catch (e) {
        console.error('加载个人中心失败', e)
        this.loadError = e?.response?.data?.message || e.message || '未知错误'
      }
    },
        async loadPendingReviewCount() {
      try {
        const res = await request.get('/club/orders/reviews/pending')
        const data = res?.data ?? res
        const asBoss = data?.asBoss ?? []
        this.pendingReviewCount = asBoss.length
      } catch (e) {
        console.error('读取待评价数失败', e)
        this.pendingReviewCount = 0
      }
    },
    /** ⭐ 读取客服身份 */
    async loadCsInfo() {
      try {
        const res = await request.get('/club/cs/me')
        const data = res?.data ?? res

        if (data?.hasApplied) {
          this.csInfo = {
            id: data.id,
            name: data.name || '',
            wechat: data.wechat || '',
            auditStatus: data.auditStatus || 'pending',
            auditNote: data.auditNote || '',
            currentLoad: data.currentLoad || 0,
            capacity: data.capacity || 10,
            reviewCount: data.reviewCount || 0,
            avgOverall: data.avgOverall || 0,
            status: data.status || 'offline'
          }
        } else {
          this.csInfo = null
        }
      } catch (e) {
        console.error('读取客服状态失败', e)
        this.csInfo = null
      }
    },

    reload() {
      this.loading = true
      Promise.all([
        this.loadSummary(),
        this.loadCsInfo()
      ]).finally(() => {
        this.loading = false
      })
    },

    // ==================================================
    //  ⭐ 打手在线状态切换
    // ==================================================
    async switchStatus(status) {
      if (this.statusChanging) return
      if (status === this.profileStatus) return

      if (status === 'offline' && this.busyLocked) {
        alert('你还有进行中的订单，无法离线')
        return
      }

      this.statusChanging = true
      try {
        const res = await request.post('/club/operators/me/status', { status })
        const data = res?.data ?? res
        this.profileStatus = data?.status || status
        this.lastSeenAt = data?.lastSeenAt || new Date().toISOString()
      } catch (e) {
        alert(e?.response?.data?.message || e.message || '切换失败')
      } finally {
        this.statusChanging = false
      }
    },

    // ==================================================
    //  ⭐ 心跳
    // ==================================================
    startHeartbeat() {
      this.stopHeartbeat()
      if (!this.isApprovedOperator) return

      this.sendHeartbeat()

      this.heartbeatTimer = setInterval(() => {
        this.sendHeartbeat()
      }, 30000)
    },

    stopHeartbeat() {
      if (this.heartbeatTimer) {
        clearInterval(this.heartbeatTimer)
        this.heartbeatTimer = null
      }
    },

    async sendHeartbeat() {
      if (!this.isLoggedIn) return
      try {
        const res = await request.post('/club/operators/me/heartbeat')
        const data = res?.data ?? res
        if (data?.ok) {
          if (data.status && data.status !== this.profileStatus) {
            this.profileStatus = data.status
          }
          if (typeof data.activeOrderCount === 'number') {
            this.activeOrderCount = data.activeOrderCount
          }
        }
      } catch (e) {
        console.warn('心跳失败', e)
      }
    },

    readUidFromToken() {
      return useUserStore().userId
    },

    shortId(uid) {
      return uid ? String(uid).substring(0, 8).toUpperCase() : '—'
    }
  }
}
</script>

<style scoped>


.page-hero { padding: 120px 0 60px; border-bottom: 1px solid var(--line); }

.op-state {
  padding: 60px 32px; text-align: center;
  font-family: monospace; font-size: 12px;
  letter-spacing: 1px; color: var(--text-muted);
  border: 1px dashed var(--line);
  background: var(--panel);
}
.dot {
  display: inline-block; width: 6px; height: 6px;
  margin-right: 6px; border-radius: 50%;
  background: var(--orange);
  animation: pulse 1.4s infinite;
}
@keyframes pulse { 50% { opacity: 0.3; } }
.btn-link {
  display: inline-block;
  padding: 8px 18px;
  font-size: 12px; letter-spacing: 1px;
  color: var(--orange);
  background: transparent;
  border: 1px solid rgba(255, 107, 26, 0.4);
  text-decoration: none;
  cursor: pointer;
}
.btn-link:hover { color: var(--on-accent); background: var(--orange); border-color: var(--orange); }

.identity-card {
  display: flex; align-items: center; gap: 20px;
  padding: 26px 28px;
  margin-bottom: 30px;
  border: 1px solid var(--line);
  background: linear-gradient(135deg, var(--panel), var(--panel));
}
.ic-avatar {
  flex-shrink: 0;
  width: 62px; height: 62px;
  display: grid; place-items: center;
  border: 1px solid var(--line-bright);
  background: var(--panel-light);
  color: var(--orange);
  font-size: 26px; font-weight: 700;
}
.ic-main { min-width: 0; }
.ic-name {
  margin: 0 0 8px;
  font-size: 22px; font-weight: 700;
  letter-spacing: 1px; color: var(--text);
}
.ic-tags { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 8px; }
.ic-tag {
  padding: 3px 10px;
  font-family: monospace; font-size: 12px;
  letter-spacing: 1px;
  border: 1px solid;
  border-radius: 2px;
}
.ic-tag.boss {
  color: var(--orange);
  border-color: rgba(255, 107, 26, 0.4);
  background: rgba(255, 107, 26, 0.06);
}
.ic-tag.operator {
  color: var(--cyan);
  border-color: rgba(120, 215, 206, 0.4);
  background: rgba(120, 215, 206, 0.06);
}
/* ⭐ 客服标签 */
.ic-tag.cs {
  color: var(--accent-color);
  border-color: rgba(165, 165, 255, 0.4);
  background: rgba(165, 165, 255, 0.06);
}
.ic-tag.pending {
  color: var(--text-soft);
  border-color: var(--line-bright);
}
.ic-uid {
  margin: 0;
  font-size: 12px; letter-spacing: 1px;
  color: var(--text-muted);
}
.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }

.entry-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 14px;
  margin-bottom: 40px;
}
.entry-card {
  position: relative;
  display: flex; flex-direction: column;
  gap: 8px;
  padding: 18px 20px;
  border: 1px solid var(--line);
  background: var(--panel);
  text-decoration: none;
  color: inherit;
  transition: all 0.22s ease;
  cursor: pointer;
}
.entry-card:hover:not(.disabled) {
  border-color: rgba(255, 107, 26, 0.4);
  transform: translateY(-2px);
  background: var(--overlay);
}
.entry-card.disabled {
  opacity: 0.55;
  cursor: not-allowed;
}
.ec-icon {
  font-size: 20px;
  line-height: 1;
}
.ec-title {
  font-family: monospace;
  font-size: 12px; letter-spacing: 1.5px;
  color: var(--text-soft);
}
.ec-count {
  font-family: monospace;
  font-size: 22px; font-weight: 700;
  color: var(--text);
}
.ec-count.muted {
  font-size: 12px; font-weight: 400;
  color: var(--text-muted);
}
.ec-dot {
  font-size: 12px;
  color: var(--orange);
  margin-left: 4px;
}

.stat-section {
  margin-bottom: 40px;
}
.ss-title {
  margin: 0 0 16px;
  font-family: monospace;
  font-size: 12px; letter-spacing: 2px;
  color: var(--orange);
  font-weight: 600;
}
.ss-note {
  color: var(--text-muted);
  font-weight: 400;
  margin-left: 8px;
}

.stat-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 14px;
}
.stat-cell {
  display: flex; flex-direction: column;
  gap: 8px;
  padding: 18px 20px;
  border: 1px solid var(--line);
  background: var(--panel);
}
.sc-label {
  font-family: monospace;
  font-size: 12px; letter-spacing: 1px;
  color: var(--text-muted);
  text-transform: uppercase;
}
.sc-value {
  font-family: monospace;
  font-size: 22px; font-weight: 700;
  color: var(--text);
}
.sc-value.accent { color: var(--orange); }

/* ==================================================
   ⭐ 客服入口卡片
================================================== */
.cs-entry-card {
  display: flex; align-items: center; justify-content: space-between;
  gap: 20px;
  padding: 20px 24px;
  border: 1px solid var(--line);
  background: var(--panel);
  text-decoration: none;
  color: inherit;
  transition: all 0.22s ease;
  border-radius: 3px;
}
.cs-entry-card:hover {
  border-color: rgba(165, 165, 255, 0.5);
  transform: translateY(-2px);
  background: var(--overlay);
}
.cs-entry-card.approved { border-color: rgba(120, 215, 206, 0.35); }
.cs-entry-card.approved:hover { border-color: rgba(120, 215, 206, 0.6); }
.cs-entry-card.pending  { border-color: rgba(255, 107, 26, 0.3); }
.cs-entry-card.pending:hover { border-color: rgba(255, 107, 26, 0.55); }
.cs-entry-card.rejected { border-color: rgba(255, 80, 80, 0.35); }
.cs-entry-card.banned   { border-color: rgba(255, 80, 80, 0.45); opacity: 0.85; }

.cec-left {
  display: flex; align-items: center; gap: 16px;
  min-width: 0;
}
.cec-icon {
  flex-shrink: 0;
  width: 48px; height: 48px;
  display: grid; place-items: center;
  border: 1px solid var(--line-bright);
  background: var(--panel-light);
  font-size: 22px;
  border-radius: 3px;
}
.cec-info { min-width: 0; }
.cec-title {
  font-size: 15px; font-weight: 700;
  color: var(--text);
  letter-spacing: 0.5px;
  margin-bottom: 5px;
}
.cec-desc {
  font-family: monospace; font-size: 12px;
  color: var(--text-muted);
  line-height: 1.6;
}
.cec-arrow {
  flex-shrink: 0;
  font-size: 20px;
  color: var(--orange);
  transition: transform 0.2s;
}
.cs-entry-card:hover .cec-arrow {
  transform: translateX(4px);
}

/* ⭐ 打手在线状态卡片 */
.status-card {
  display: flex; align-items: center; justify-content: space-between;
  gap: 20px;
  padding: 16px 20px;
  margin-bottom: 16px;
  border: 1px solid var(--line);
  background: var(--panel);
  transition: border-color 0.2s, background 0.2s;
}
.status-card.online  { border-color: rgba(120, 215, 206, 0.35); background: rgba(120, 215, 206, 0.04); }
.status-card.busy    { border-color: rgba(255, 107, 26, 0.35);  background: rgba(255, 107, 26, 0.04); }
.status-card.resting { border-color: rgba(165, 165, 255, 0.3);  background: rgba(165, 165, 255, 0.03); }
.status-card.offline { border-color: rgba(89, 102, 115, 0.35);  background: rgba(89, 102, 115, 0.05); }

.sc-left {
  display: flex; align-items: center; gap: 14px;
  min-width: 0;
}
.sc-text { min-width: 0; }
.sc-title {
  font-size: 14px; font-weight: 700;
  color: var(--text);
  margin-bottom: 3px;
}
.sc-sub {
  font-size: 12px;
  color: var(--text-muted);
  line-height: 1.5;
}

.sc-actions {
  display: flex; gap: 6px;
  flex-shrink: 0;
}
.sc-btn {
  padding: 7px 14px;
  font-family: monospace;
  font-size: 12px; letter-spacing: 1px;
  color: var(--text-soft);
  background: transparent;
  border: 1px solid var(--line-bright);
  cursor: pointer;
  transition: all 0.2s;
  border-radius: 2px;
}
.sc-btn:hover:not(:disabled):not(.on) {
  color: var(--text);
  border-color: var(--text-soft);
}
.sc-btn.on {
  color: var(--orange);
  border-color: rgba(255, 107, 26, 0.5);
  background: rgba(255, 107, 26, 0.1);
}
.sc-btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.game-list {
  margin-top: 16px;
  border: 1px solid var(--line);
  background: var(--panel);
}
.game-row {
  display: flex; align-items: center; justify-content: space-between;
  gap: 16px;
  padding: 14px 20px;
  border-bottom: 1px solid var(--line);
}
.game-row:last-child { border-bottom: none; }
.gr-left { display: flex; align-items: center; gap: 12px; }
.gr-name { font-size: 14px; font-weight: 600; color: var(--text); }
.gr-code {
  font-size: 12px; letter-spacing: 1px;
  color: var(--text-muted);
}
.gr-right { display: flex; align-items: center; gap: 14px; }
.gr-level {
  padding: 3px 9px;
  font-family: monospace; font-size: 12px; font-weight: 700;
  color: var(--orange);
  border: 1px solid rgba(255, 107, 26, 0.4);
  background: rgba(255, 107, 26, 0.08);
  border-radius: 2px;
}
.gr-stat {
  font-family: monospace;
  font-size: 12px;
  color: var(--text-muted);
}

.pending-note {
  padding: 20px 24px;
  font-size: 13px;
  line-height: 1.7;
  color: var(--text-muted);
  border: 1px dashed var(--line);
  background: var(--panel);
}

@media (max-width: 900px) {
  .entry-grid { grid-template-columns: repeat(2, 1fr); }
  .stat-grid  { grid-template-columns: repeat(2, 1fr); }
  .status-card { flex-direction: column; align-items: stretch; }
  .sc-actions { width: 100%; }
  .sc-btn { flex: 1; text-align: center; }
}

@media (max-width: 520px) {
  .page-hero { padding: 80px 0 40px; }
  .identity-card { flex-direction: column; align-items: flex-start; }
  .entry-grid { grid-template-columns: 1fr 1fr; }
  .stat-grid  { grid-template-columns: 1fr 1fr; }
  .game-row { flex-direction: column; align-items: flex-start; gap: 8px; }
  .cs-entry-card { flex-direction: column; align-items: stretch; gap: 14px; }
  .cec-arrow { display: none; }
}
</style>
