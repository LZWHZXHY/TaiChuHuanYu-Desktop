<template>
  <div class="page-shell">
    <Navbar />

    <main>
      <section class="page-hero">
        <div class="section-container">
          <SectionHeading index="01" title="OPERATOR ORDERS" subtitle="打手订单 / MISSION QUEUE" />
        </div>
      </section>

      <section class="section-block alt">
        <div class="section-container">

          <div v-if="!isLoggedIn" class="op-state">请先登录后查看打手订单</div>

          <div v-else-if="!isOperator" class="op-state">
            你还没有通过打手认证，请先前往首页提交申请
          </div>

          <div v-else-if="loading" class="op-state">
            <span class="dot"></span> 正在读取任务队列…
          </div>

          <template v-else>

            <div v-if="myProfileStatus" class="status-bar" :class="myProfileStatus">
              <div class="sb-left">
                <OnlineStatusBadge :status="myProfileStatus" />
                <span class="sb-text">
                  {{ statusHintForSelf(myProfileStatus) }}
                </span>
              </div>
              <router-link to="/taichu/profile" class="sb-link">
                前往个人中心切换 →
              </router-link>
            </div>

            <div class="order-tabs">
              <button
                v-for="t in TABS"
                :key="t.value"
                class="order-tab"
                :class="{ active: filter === t.value }"
                @click="filter = t.value"
              >
                {{ t.label }}
                <span v-if="tabCount(t.value) > 0" class="tab-count">{{ tabCount(t.value) }}</span>
              </button>
            </div>

            <div v-if="filteredOrders.length === 0" class="op-state">
              当前没有可执行的任务
            </div>

            <div v-else class="order-list">
              <article
                v-for="o in filteredOrders"
                :key="o.orderNo"
                class="order-card"
                :class="{ 'is-pool': isPoolOrder(o) }"
              >
                <header class="oc-head">
                  <div class="oc-no">
                    <span class="oc-label">订单号</span>
                    <span class="oc-code mono">{{ o.orderNo }}</span>
                  </div>

                  <div class="oc-head-right">
                    <span v-if="isPoolOrder(o)" class="oc-pool-tag">
                      公海 · 客服指派
                    </span>
                    <span :class="['oc-status', deriveStatus(o)]">
                      {{ statusText(o) }}
                    </span>
                  </div>
                </header>

                <div class="oc-body">
                  <div class="oc-info">
                    <div class="oc-row">
                      <span>游戏</span>
                      <b>{{ o.gameName }}</b>
                    </div>
                    <div class="oc-row">
                      <span>类型</span>
                      <b>{{ o.orderTypeName }}</b>
                    </div>
                    <div v-for="(v, k) in o.params" :key="k" class="oc-row">
                      <span>{{ k }}</span>
                      <b>{{ v }}</b>
                    </div>
                    <div class="oc-row">
                      <span>老板</span>
                      <b>{{ o.customerName || shortId(o.customerId) }}</b>
                    </div>

                    <div v-if="o.customerServiceName" class="oc-row">
                      <span>客服</span>
                      <b>
                        {{ o.customerServiceName }}
                        <span v-if="o.customerServiceWechat" class="cs-wechat mono">
                          · {{ o.customerServiceWechat }}
                        </span>
                      </b>
                    </div>

                    <div v-if="o.remark" class="oc-row">
                      <span>备注</span>
                      <b class="oc-remark">{{ o.remark }}</b>
                    </div>
                  </div>

                  <div class="oc-side">
                    <div class="oc-price">¥{{ o.price }}</div>
                    <div class="oc-payout">
                      预计到手 <b>¥{{ estimatePayout(o) }}</b>
                      <span class="oc-rate">（{{ myLevelRateFor(o.gameCode) }}%）</span>
                    </div>
                    <div class="oc-time mono">{{ fmtTime(o.createdAt) }}</div>
                  </div>
                </div>

                <!-- ⭐ 接单进度 + 打手列表 -->
                <div v-if="showProgress(o)" class="oc-progress">
                  <div class="op-progress-head">
                    <span class="oph-label">接单进度</span>
                    <b class="oph-count" :class="{ done: isAllAccepted(o) }">
                      {{ o.acceptedCount || 0 }} / {{ o.totalOperatorCount || 0 }}
                    </b>
                    <span v-if="isAllAccepted(o)" class="oph-tag done">
                      ✓ 全员已接
                    </span>
                    <span v-else-if="didIAccept(o)" class="oph-tag mine">
                      ✓ 你已接单，等待其他打手
                    </span>
                  </div>
                  <div class="op-progress-list">
                    <div
                      v-for="op in (o.operators || [])"
                      :key="op.userId"
                      class="op-chip"
                      :class="{ accepted: op.accepted, me: op.userId === myUserId }"
                    >
                      <span class="chip-status">{{ op.accepted ? '✓' : '·' }}</span>
                      <span class="chip-name">{{ op.name }}</span>
                      <span v-if="op.userId === myUserId" class="chip-me">你</span>
                    </div>
                  </div>
                </div>

                <div v-if="statusHint(o)" class="oc-hint">{{ statusHint(o) }}</div>

                <footer class="oc-foot">
                  <!-- 待接单 & 我还没接 & 可以接 -->
                  <button
                    v-if="o.status === 'pending' && !didIAccept(o) && canAccept(o)"
                    class="oc-btn primary"
                    :disabled="acting === o.orderNo"
                    @click="doAction(o, 'accept')"
                  >
                    {{
                      acting === o.orderNo
                        ? '处理中…'
                        : (isPoolOrder(o) ? '抢单' : '接单')
                    }}
                  </button>

                  <!-- 待接单 & 我已接，等其他人 -->
                  <button
                    v-if="o.status === 'pending' && didIAccept(o)"
                    class="oc-btn waiting"
                    disabled
                  >
                    ✓ 已接单，等待其他打手
                  </button>

                  <!-- 待接单 & 我离线 -->
                  <button
                    v-if="o.status === 'pending' && !didIAccept(o) && !canAccept(o)"
                    class="oc-btn waiting"
                    disabled
                  >
                    离线中 · 无法接单
                  </button>

                  <button
                    v-if="o.status === 'accepted'"
                    class="oc-btn primary"
                    :disabled="acting === o.orderNo"
                    @click="doAction(o, 'start')"
                  >
                    {{ acting === o.orderNo ? '处理中…' : '开始服务' }}
                  </button>

                  <!-- ⭐ 服务中：等老板结单 -->
                  <button
                    v-if="o.status === 'processing'"
                    class="oc-btn waiting"
                    disabled
                  >
                    服务中 · 等待老板结单
                  </button>

                  <!-- ⭐ 放弃接单（替换原「取消」） -->
                  <button
                    v-if="(o.status === 'pending' || o.status === 'accepted') && !isPoolOrder(o)"
                    class="oc-btn ghost"
                    :disabled="acting === o.orderNo"
                    @click="abandonOrder(o)"
                  >
                    {{ acting === o.orderNo ? '处理中…' : '放弃接单' }}
                  </button>
                </footer>
              </article>
            </div>
          </template>

        </div>
      </section>
    </main>

    <Footer />

    <!-- 评价弹窗 -->
    <ReviewModal
      :visible="showReview"
      :order="reviewOrder"
      :from-customer="true"
      @close="showReview = false"
      @success="loadOrders"
    />
  </div>
</template>

<script>
import { fmtTime, isAllAccepted } from './orderHelpers'
import { useUserStore } from '@/stores/user'
import Navbar              from './Navbar.vue'
import Footer              from './Footer.vue'
import SectionHeading      from './SectionHeading.vue'
import ReviewModal         from './ReviewModal.vue'
import OnlineStatusBadge   from './OnlineStatusBadge.vue'
import request             from '@/utils/request'

const LEVEL_RATE = { L1: 60, L2: 68, L3: 76, L4: 84, L5: 92 }

export default {
  name: 'OperatorOrders',
  components: { Navbar, Footer, SectionHeading, ReviewModal, OnlineStatusBadge },

  data() {
    return {
      loading: true,
      isOperator: false,
      orders: [],
      filter: 'all',
      acting: '',
      myLevels: {},
      myUserId: '',
      myProfileStatus: '',

      showReview: false,
      reviewOrder: null,

      TABS: [
        { label: '全部',   value: 'all' },
        { label: '待接单', value: 'pending' },
        { label: '服务中', value: 'serving' },
        { label: '已完成', value: 'completed' },
        { label: '已取消', value: 'cancelled' }
      ]
    }
  },

  computed: {
    isLoggedIn() {
      return useUserStore().isLoggedIn
    },

    filteredOrders() {
      if (this.filter === 'all') return this.orders
      return this.orders.filter(o => this.deriveStatus(o) === this.filter)
    }
  },

  async mounted() {
    if (!this.isLoggedIn) {
      this.loading = false
      return
    }
    this.myUserId = useUserStore().userId
    await this.loadMyStatus({ checkEligibility: true })
    if (this.isOperator) {
      await this.loadOrders()
    }
    this.loading = false
  },

  methods: {
    async loadMyStatus({ checkEligibility = false } = {}) {
      try {
        const res = await request.get('/club/operators/me')
        const data = res?.data ?? res
        if (!data?.hasApplied) {
          if (checkEligibility) this.isOperator = false
          return
        }
        if (checkEligibility) {
          const approved = (data.games || []).filter(game => game.auditStatus === 'approved')
          this.isOperator = approved.length > 0
          approved.forEach(game => { this.myLevels[game.gameCode] = game.level || 'L1' })
        }
        this.myProfileStatus = data.status || 'offline'
      } catch (e) {
        console.error(checkEligibility ? '读取打手状态失败' : '刷新我的状态失败', e)
        if (checkEligibility) this.isOperator = false
      }
    },

    async loadOrders() {
      try {
        const res = await request.get('/club/orders/operator', { params: { pageSize: 50 } })
        const payload = res?.data ?? res
        this.orders = payload?.items ?? payload ?? []
      } catch (e) {
        console.error('拉取打手订单失败', e)
        this.orders = []
      }
    },

    isPoolOrder(o) {
      return !o.operatorUserId
    },

    canAccept() {
      return this.myProfileStatus !== 'offline'
    },

    // GUID 规范化比较，判断当前打手是否已接单
    didIAccept(o) {
      if (!this.myUserId) return false
      const norm = (s) => String(s || '').toLowerCase().replace(/-/g, '')
      const my = norm(this.myUserId)
      const ids = (o.acceptedOperatorIds || []).map(norm)
      return ids.includes(my)
    },

    // ⭐ 是否全员已接
    isAllAccepted,

    // ⭐ 是否显示进度区块
    showProgress(o) {
      const total = o.totalOperatorCount || 0
      const accepted = o.acceptedCount || 0
      return total > 1 || accepted > 0
    },

    deriveStatus(o) {
      if (o.status === 'cancelled') return 'cancelled'
      if (o.status === 'completed') return 'completed'
      if (o.status === 'processing' || o.status === 'accepted') return 'serving'
      if (o.status === 'pending') return 'pending'
      return 'other'
    },

    statusText(o) {
      if (o.status === 'pending')    return '待接单'
      if (o.status === 'accepted')   return '已接单'
      if (o.status === 'processing') return '服务中'
      if (o.status === 'completed')  return '已完成'
      if (o.status === 'cancelled')  return '已取消'
      return o.status
    },

    statusHint(o) {
      const isPool = this.isPoolOrder(o)
      if (o.status === 'pending') {
        if (this.myProfileStatus === 'offline') {
          return isPool
            ? '这是一单客服指派的公海单，但你当前处于离线状态，无法抢单'
            : '你当前处于离线状态，无法接单，请先切换为在线'
        }
        if (this.didIAccept(o)) {
          const rest = (o.totalOperatorCount || 0) - (o.acceptedCount || 0)
          return rest > 0
            ? `你已接单，还有 ${rest} 位打手未接，全员接单后即可开始服务`
            : '全员已接单，准备开始服务'
        }
        return isPool
          ? '客服已确认收款，这是公海单，先到先得'
          : '客服已确认收款，请尽快接单'
      }
      if (o.status === 'accepted')   return '全员已接单，准备开始服务'
      if (o.status === 'processing') return '服务进行中，完成后请等待老板结单'
      if (o.status === 'completed')  return '订单已完成'
      if (o.status === 'cancelled')  return '订单已取消'
      return ''
    },

    statusHintForSelf(status) {
      if (status === 'online')  return '当前状态：在线 · 可以接单'
      if (status === 'busy')    return '当前状态：接单中 · 已有进行中的订单'
      if (status === 'resting') return '当前状态：休息中 · 暂不接单'
      if (status === 'offline') return '当前状态：离线 · 无法接单，请前往个人中心切换'
      return ''
    },

    tabCount(val) {
      if (val === 'all') return this.orders.length
      return this.orders.filter(o => this.deriveStatus(o) === val).length
    },

    myLevelRateFor(gameCode) {
      const lv = this.myLevels[gameCode] || 'L1'
      return LEVEL_RATE[lv] ?? 60
    },

    estimatePayout(o) {
      const rate = this.myLevelRateFor(o.gameCode) / 100
      return (Number(o.price) * rate).toFixed(2)
    },

    async doAction(o, action) {
      if (this.acting) return

      // ⭐ 完成订单由老板操作
      if (action === 'complete') {
        alert('完成订单由老板操作，请等待老板结单')
        return
      }

      if (action === 'accept' && this.myProfileStatus === 'offline') {
        alert('你当前处于离线状态，请先前往个人中心切换为在线')
        return
      }

      this.acting = o.orderNo
      try {
        await request.post(`/club/orders/${o.orderNo}/${action}`)
        await Promise.all([
          this.loadOrders(),
          (action === 'accept' || action === 'abandon') && this.loadMyStatus()
        ])
      } catch (e) {
        alert(e?.response?.data?.message || e.message || '操作失败')
      } finally {
        this.acting = ''
      }
    },

    // ⭐ 放弃接单（替换原 cancelOrder）
    async abandonOrder(o) {
      if (this.acting) return

      if (!confirm(`确认放弃订单【${o.orderNo}】？\n\n放弃后将从订单移除，订单回到公海供其他打手抢单。`)) return

      await this.doAction(o, 'abandon')
    },

    shortId(uid) {
      return uid ? String(uid).substring(0, 8).toUpperCase() : '—'
    },

    fmtTime
  }
}
</script>

<style scoped>
.page-hero { padding: 120px 0 60px; border-bottom: 1px solid var(--line); }

.status-bar {
  display: flex; align-items: center; justify-content: space-between;
  gap: 16px;
  padding: 12px 18px;
  margin-bottom: 22px;
  border: 1px solid var(--line);
  background: var(--panel);
  border-radius: 3px;
}
.status-bar.online  { border-color: rgba(120, 215, 206, 0.35); background: rgba(120, 215, 206, 0.04); }
.status-bar.busy    { border-color: rgba(255, 107, 26, 0.35);  background: rgba(255, 107, 26, 0.04); }
.status-bar.resting { border-color: rgba(165, 165, 255, 0.3);  background: rgba(165, 165, 255, 0.03); }
.status-bar.offline { border-color: rgba(89, 102, 115, 0.35);  background: rgba(89, 102, 115, 0.05); }

.sb-left {
  display: flex; align-items: center; gap: 12px;
  min-width: 0;
}
.sb-text {
  font-size: 12px;
  color: var(--text-soft);
  line-height: 1.5;
}
.sb-link {
  flex-shrink: 0;
  font-family: monospace;
  font-size: 12px;
  color: var(--orange);
  text-decoration: none;
  letter-spacing: 1px;
  transition: opacity 0.2s;
}
.sb-link:hover { opacity: 0.75; }

.order-tabs {
  display: flex; flex-wrap: wrap; gap: 8px;
  margin-bottom: 28px;
  border-bottom: 1px solid var(--line);
  padding-bottom: 14px;
}
.order-tab {
  padding: 8px 16px;
  font-size: 12px; letter-spacing: 1px;
  color: var(--text-soft);
  background: transparent;
  border: 1px solid var(--line);
  cursor: pointer; transition: 0.2s;
  display: inline-flex; align-items: center; gap: 6px;
}
.order-tab:hover { color: var(--text); border-color: var(--line-bright); }
.order-tab.active {
  color: var(--orange);
  border-color: rgba(255, 107, 26, 0.5);
  background: rgba(255, 107, 26, 0.06);
}
.tab-count {
  font-family: monospace; font-size: 12px;
  padding: 1px 6px;
  background: var(--bg-soft);
  border-radius: 3px;
}

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

.order-list { display: flex; flex-direction: column; gap: 16px; }

.order-card {
  border: 1px solid var(--line);
  background: var(--panel);
  transition: border-color 0.2s, background 0.2s;
}
.order-card:hover { border-color: var(--line-bright); }

.order-card.is-pool {
  border-color: rgba(255, 107, 26, 0.35);
  background: linear-gradient(
    90deg,
    rgba(255, 107, 26, 0.05),
    var(--panel) 25%
  );
}
.order-card.is-pool:hover {
  border-color: rgba(255, 107, 26, 0.6);
}

.oc-head {
  display: flex; justify-content: space-between; align-items: center;
  padding: 16px 20px;
  border-bottom: 1px solid var(--line);
}
.oc-no { display: flex; flex-direction: column; gap: 4px; }
.oc-label {
  font-family: monospace; font-size: 12px;
  letter-spacing: 1px; color: var(--text-muted);
}
.oc-code { font-size: 13px; color: var(--text); }
.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }

.oc-head-right {
  display: flex; align-items: center; gap: 10px;
}

.oc-pool-tag {
  padding: 3px 10px;
  font-family: monospace; font-size: 12px;
  letter-spacing: 1px;
  color: var(--orange);
  border: 1px solid rgba(255, 107, 26, 0.45);
  background: rgba(255, 107, 26, 0.08);
  border-radius: 3px;
  white-space: nowrap;
}

.oc-status {
  padding: 4px 12px;
  font-size: 12px; font-weight: 600;
  letter-spacing: 1px;
  border-radius: 3px;
}
.oc-status.pending    { background: rgba(255, 140, 74, 0.12); color: var(--accent-color); }
.oc-status.serving    { background: rgba(120, 215, 206, 0.12); color: var(--cyan); }
.oc-status.completed  { background: rgba(80, 200, 120, 0.12); color: var(--success); }
.oc-status.cancelled  { background: var(--bg-soft); color: var(--text-muted); }

.oc-body {
  display: flex; justify-content: space-between; gap: 20px;
  padding: 16px 20px;
}
.oc-info { flex: 1; display: flex; flex-direction: column; gap: 8px; }
.oc-row { display: flex; gap: 12px; font-size: 13px; }
.oc-row span { color: var(--text-muted); min-width: 60px; }
.oc-row b { color: var(--text); }
.oc-remark { color: var(--text-soft) !important; font-weight: 400; }

.cs-wechat {
  font-size: 12px;
  color: var(--cyan);
  font-weight: 400;
  margin-left: 4px;
}

.oc-side {
  flex-shrink: 0;
  display: flex; flex-direction: column;
  align-items: flex-end; justify-content: center;
  gap: 6px;
}
.oc-price {
  font-family: monospace; font-size: 24px; font-weight: 700;
  color: var(--orange);
}
.oc-payout {
  font-family: monospace; font-size: 12px;
  color: var(--text-muted);
}
.oc-payout b { color: var(--cyan); }
.oc-rate { color: var(--text-muted); margin-left: 4px; }
.oc-time { font-size: 12px; color: var(--text-muted); }

/* ⭐ 接单进度 */
.oc-progress {
  padding: 12px 20px;
  background: var(--panel);
  border-top: 1px solid var(--line);
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.op-progress-head {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}
.oph-label {
  font-family: monospace;
  font-size: 12px;
  letter-spacing: 1px;
  color: var(--text-muted);
}
.oph-count {
  font-family: monospace;
  font-size: 14px;
  font-weight: 700;
  color: var(--orange);
}
.oph-count.done { color: var(--success); }
.oph-tag {
  margin-left: auto;
  padding: 2px 8px;
  font-family: monospace;
  font-size: 12px;
  border-radius: 2px;
  white-space: nowrap;
}
.oph-tag.mine {
  color: var(--success);
  border: 1px solid rgba(120, 215, 206, 0.4);
  background: rgba(120, 215, 206, 0.08);
}
.oph-tag.done {
  color: var(--success);
  border: 1px solid rgba(80, 200, 120, 0.5);
  background: rgba(80, 200, 120, 0.1);
}

.op-progress-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.op-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  font-size: 12px;
  color: var(--text-muted);
  border: 1px solid var(--line);
  background: var(--bg-soft);
  border-radius: 3px;
  transition: all 0.2s;
}
.op-chip.accepted {
  color: var(--cyan);
  border-color: rgba(120, 215, 206, 0.4);
  background: rgba(120, 215, 206, 0.06);
}
.op-chip.me {
  box-shadow: 0 0 0 1px rgba(255, 107, 26, 0.3) inset;
}
.chip-status {
  font-family: monospace;
  font-weight: 700;
  color: inherit;
}
.chip-name {
  color: inherit;
}
.chip-me {
  padding: 1px 5px;
  font-family: monospace;
  font-size: 12px;
  letter-spacing: 1px;
  color: var(--orange);
  border: 1px solid rgba(255, 107, 26, 0.4);
  background: rgba(255, 107, 26, 0.08);
  border-radius: 2px;
}

.oc-hint {
  padding: 10px 20px;
  font-size: 12px; line-height: 1.6;
  color: var(--text-muted);
  background: var(--panel);
  border-top: 1px solid var(--line);
}

.oc-foot {
  display: flex; gap: 10px; justify-content: flex-end;
  padding: 14px 20px;
  border-top: 1px solid var(--line);
  flex-wrap: wrap;
}
.oc-btn {
  padding: 8px 20px;
  font-size: 12px; letter-spacing: 1px;
  cursor: pointer; transition: all 0.2s;
  border-radius: 3px;
}
.oc-btn:disabled { opacity: 0.5; cursor: not-allowed; }
.oc-btn.primary {
  color: var(--on-accent); background: var(--orange);
  border: 1px solid var(--orange);
}
.oc-btn.primary:hover:not(:disabled) { background: var(--orange-bright); }
.oc-btn.ghost {
  color: var(--text-soft);
  background: transparent;
  border: 1px solid var(--line-bright);
}
.oc-btn.ghost:hover:not(:disabled) { color: var(--text); border-color: var(--text-soft); }
/* ⭐ 等待中按钮（已接单/离线/服务中） */
.oc-btn.waiting {
  color: var(--success);
  background: rgba(120, 215, 206, 0.06);
  border: 1px solid rgba(120, 215, 206, 0.4);
  cursor: not-allowed;
}

@media (max-width: 520px) {
  .oc-body { flex-direction: column; }
  .oc-side { align-items: flex-start; }
  .status-bar { flex-direction: column; align-items: flex-start; }
  .oc-head { flex-wrap: wrap; gap: 10px; }
  .page-hero { padding: 80px 0 40px; }
}
</style>
