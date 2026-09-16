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

              <router-link to="/taichu/operator/reviews" class="entry-card">
                <div class="ec-icon">⭐</div>
                <div class="ec-title">我的评价</div>
                <div class="ec-count">
                  {{ operatorInfo ? operatorInfo.reviewCount : 0 }} 条
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

            <!-- ============ 打手数据 ============ -->
            <section v-if="hasApplied" class="stat-section">
              <h3 class="ss-title">
                作为打手
                <span v-if="!isApprovedOperator" class="ss-note">
                  （{{ operatorAuditLabel }}）
                </span>
              </h3>

              <template v-if="isApprovedOperator">
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
import Navbar         from './Navbar.vue'
import Footer         from './Footer.vue'
import SectionHeading from './SectionHeading.vue'
import request        from '@/utils/request'

export default {
  name: 'MyProfile',
  components: { Navbar, Footer, SectionHeading },

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
      walletBalance: 0
    }
  },

  computed: {
    isLoggedIn() {
      return !!localStorage.getItem('token')
    },

    displayName() {
      return this.operatorInfo?.nickname || '太初用户'
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
    }
  },

  async mounted() {
    if (!this.isLoggedIn) {
      this.loading = false
      return
    }
    this.userId = this.readUidFromToken()
    await this.loadSummary()
    this.loading = false
  },

  methods: {
    async loadSummary() {
      this.loadError = ''
      try {
        const res = await request.get('/club/profile/summary')
        const data = res?.data ?? res

        this.boss = data?.boss ?? this.boss
        this.operatorInfo = data?.operator ?? null

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

    reload() {
      this.loading = true
      this.loadSummary().finally(() => {
        this.loading = false
      })
    },

    readUidFromToken() {
      const token = localStorage.getItem('token')
      if (!token) return ''
      try {
        const payload = JSON.parse(atob(token.split('.')[1]))
        return payload.sub || payload.nameid || payload.userId || ''
      } catch {
        return ''
      }
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
  background: rgba(16, 25, 35, 0.3);
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
.btn-link:hover { color: #fff; background: var(--orange); border-color: var(--orange); }

.identity-card {
  display: flex; align-items: center; gap: 20px;
  padding: 26px 28px;
  margin-bottom: 30px;
  border: 1px solid var(--line);
  background: linear-gradient(135deg, rgba(16, 25, 35, 0.7), rgba(10, 15, 21, 0.55));
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
  font-family: monospace; font-size: 10px;
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
.ic-tag.pending {
  color: var(--text-soft);
  border-color: var(--line-bright);
}
.ic-uid {
  margin: 0;
  font-size: 10px; letter-spacing: 1px;
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
  background: rgba(16, 25, 35, 0.55);
  text-decoration: none;
  color: inherit;
  transition: all 0.22s ease;
  cursor: pointer;
}
.entry-card:hover:not(.disabled) {
  border-color: rgba(255, 107, 26, 0.4);
  transform: translateY(-2px);
  background: rgba(16, 25, 35, 0.8);
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
  font-size: 10px; letter-spacing: 1.5px;
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
  font-size: 10px;
  color: var(--orange);
  margin-left: 4px;
}

.stat-section {
  margin-bottom: 40px;
}
.ss-title {
  margin: 0 0 16px;
  font-family: monospace;
  font-size: 11px; letter-spacing: 2px;
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
  background: rgba(16, 25, 35, 0.5);
}
.sc-label {
  font-family: monospace;
  font-size: 9px; letter-spacing: 1px;
  color: var(--text-muted);
  text-transform: uppercase;
}
.sc-value {
  font-family: monospace;
  font-size: 22px; font-weight: 700;
  color: var(--text);
}
.sc-value.accent { color: var(--orange); }

.game-list {
  margin-top: 16px;
  border: 1px solid var(--line);
  background: rgba(16, 25, 35, 0.4);
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
  font-size: 10px; letter-spacing: 1px;
  color: var(--text-muted);
}
.gr-right { display: flex; align-items: center; gap: 14px; }
.gr-level {
  padding: 3px 9px;
  font-family: monospace; font-size: 10px; font-weight: 700;
  color: var(--orange);
  border: 1px solid rgba(255, 107, 26, 0.4);
  background: rgba(255, 107, 26, 0.08);
  border-radius: 2px;
}
.gr-stat {
  font-family: monospace;
  font-size: 11px;
  color: var(--text-muted);
}

.pending-note {
  padding: 20px 24px;
  font-size: 13px;
  line-height: 1.7;
  color: var(--text-muted);
  border: 1px dashed var(--line);
  background: rgba(0, 0, 0, 0.15);
}

@media (max-width: 900px) {
  .entry-grid { grid-template-columns: repeat(2, 1fr); }
  .stat-grid  { grid-template-columns: repeat(2, 1fr); }
}

@media (max-width: 520px) {
  .page-hero { padding: 80px 0 40px; }
  .identity-card { flex-direction: column; align-items: flex-start; }
  .entry-grid { grid-template-columns: 1fr 1fr; }
  .stat-grid  { grid-template-columns: 1fr 1fr; }
  .game-row { flex-direction: column; align-items: flex-start; gap: 8px; }
}
</style>