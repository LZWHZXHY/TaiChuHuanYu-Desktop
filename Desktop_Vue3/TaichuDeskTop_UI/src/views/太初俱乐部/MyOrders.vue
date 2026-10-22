<template>
  <div class="page-shell">
    <Navbar />

    <main>
      <section class="page-hero">
        <div class="section-container">
          <SectionHeading index="01" title="MY ORDERS" subtitle="我的订单 / ORDER HISTORY" />
        </div>
      </section>

      <section class="section-block alt">
        <div class="section-container">

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

          <div v-if="loading" class="op-state">
            <span class="dot"></span> 正在读取订单…
          </div>

          <div v-else-if="!isLoggedIn" class="op-state">
            请先登录后查看订单
          </div>

          <div v-else-if="filteredOrders.length === 0" class="op-state">
            暂无订单
          </div>

          <div v-else class="order-list">
            <article v-for="o in filteredOrders" :key="o.orderNo" class="order-card">
              <header class="oc-head">
                <div class="oc-no">
                  <span class="oc-label">订单号</span>
                  <span class="oc-code mono">{{ o.orderNo }}</span>
                </div>
                <span :class="['oc-status', deriveStatus(o)]">
                  {{ statusText(o) }}
                </span>
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
                    <span>打手</span>
                    <b>{{ operatorText(o) }}</b>
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

                  <!-- ⭐ 炸单次数 -->
                  <div v-if="o.failedCount > 0" class="oc-row">
                    <span>炸单</span>
                    <b class="oc-failed">{{ o.failedCount }} 次</b>
                  </div>
                </div>

                <div class="oc-side">
                  <div class="oc-price">¥{{ o.price }}</div>
                  <div class="oc-time mono">{{ fmtTime(o.createdAt) }}</div>
                </div>
              </div>

              <!-- ⭐ 接单进度（多打手时显示） -->
              <div v-if="showProgress(o)" class="oc-progress">
                <div class="op-progress-head">
                  <span class="oph-label">接单进度</span>
                  <b class="oph-count" :class="{ done: isAllAccepted(o) }">
                    {{ o.acceptedCount || 0 }} / {{ o.totalOperatorCount || 0 }}
                  </b>
                  <span v-if="isAllAccepted(o)" class="oph-tag done">✓ 全员已接</span>
                </div>
                <div class="op-progress-list">
                  <div
                    v-for="op in (o.operators || [])"
                    :key="op.userId"
                    class="op-chip"
                    :class="{ accepted: op.accepted }"
                  >
                    <span class="chip-status">{{ op.accepted ? '✓' : '·' }}</span>
                    <span class="chip-name">{{ op.name }}</span>
                  </div>
                </div>
              </div>

              <!-- ⭐ 换人历史 -->
              <div v-if="o.reassignHistory && o.reassignHistory.length > 0" class="oc-reassign">
                <div class="ocr-title">🔄 换人记录</div>
                <div
                  v-for="(h, i) in o.reassignHistory"
                  :key="'rh-' + i"
                  class="ocr-item"
                >
                  <div class="ocr-note">{{ h.note }}</div>
                  <div class="ocr-time">{{ fmtTime(h.at) }}</div>
                </div>
              </div>

              <div v-if="statusHint(o)" class="oc-hint">
                {{ statusHint(o) }}
              </div>

              <!-- ⭐ 评价已过期提示 -->
              <div
                v-if="o.status === 'completed' && !o.canReview"
                class="oc-review-expired"
              >
                ⏰ 评价已过期（订单完成后 24 小时内可评价）
              </div>

              <!-- ⭐ 我的评价（已评的打手） -->
              <div v-if="o.myReviews && o.myReviews.length > 0" class="oc-my-reviews">
                <div class="omr-title">📝 我的评价</div>
                <div
                  v-for="rv in o.myReviews"
                  :key="'mr-' + rv.id"
                  class="omr-item"
                >
                  <div class="omr-head">
                    <span class="omr-op">{{ rv.toOperatorName }}</span>
                    <span class="omr-overall" :class="scoreClass(rv.overallScore)">
                      {{ rv.overallScore }}.0 星
                    </span>
                  </div>
                  <div class="omr-dims">
                    <span>技术 {{ rv.skillScore }}</span>
                    <span>态度 {{ rv.attitudeScore }}</span>
                    <span>准时 {{ rv.punctualScore }}</span>
                    <span>综合 {{ rv.overallScore }}</span>
                  </div>
                  <div v-if="rv.comment" class="omr-comment">{{ rv.comment }}</div>
                  <div class="omr-time">{{ fmtTime(rv.createdAt) }}</div>
                </div>
              </div>

              <footer class="oc-foot">
                <!-- ⭐ 完成订单（服务中时老板可主动结单） -->
                <button
                  v-if="o.status === 'processing'"
                  class="oc-btn primary"
                  @click="openCompleteModal(o)"
                >
                  ✓ 完成订单
                </button>

                <!-- ⭐ 上传/更换结算截图（服务中 或 已完成 都能传） -->
                <button
                  v-if="(o.status === 'processing' || o.status === 'completed')"
                  class="oc-btn"
                  :class="o.settlementProofUploaded ? 'ghost' : 'upload'"
                  @click="openUploadModal(o)"
                >
                  {{ o.settlementProofUploaded ? '📎 更换结算截图' : '📎 上传结算截图' }}
                </button>

                <button
                  v-if="showPayAction(o)"
                  class="oc-btn primary"
                  @click="openPayment(o)"
                >
                  查看收款码
                </button>

                <!-- ⭐ 评价打手（仅 24 小时有效期内显示） -->
                <template v-if="o.status === 'completed' && o.canReview">
                  <button
                    v-for="op in reviewableOperators(o)"
                    :key="'rv-' + op.userId"
                    class="oc-btn primary"
                    @click="openReviewFor(o, op)"
                  >
                    评价 {{ op.name }}
                  </button>
                  <button
                    v-if="(!o.operators || o.operators.length === 0)"
                    class="oc-btn primary"
                    @click="openReview(o)"
                  >
                    评价打手
                  </button>
                </template>

                <!-- ⭐ 评价客服 -->
                <button
                  v-if="canReviewCs(o)"
                  class="oc-btn cs-btn"
                  @click="openCsReview(o)"
                >
                  评价客服
                </button>

                <!-- ⭐ 投诉（completed 或 abandoned，24h 内，未投过） -->
                <button
                  v-if="canComplain(o)"
                  class="oc-btn complain-btn"
                  @click="openComplaintModal(o)"
                >
                  ⚠ 投诉
                </button>

                <button
                  v-if="o.status === 'pending' || o.status === 'accepted'"
                  class="oc-btn ghost"
                  @click="cancelOrder(o)"
                >
                  取消订单
                </button>
              </footer>
            </article>
          </div>
        </div>
      </section>
    </main>

    <Footer />

    <!-- 收款码弹窗 -->
    <Teleport to="body">
      <div v-if="payOrder" class="pay-modal-mask" @click.self="payOrder = null">
        <div class="pay-modal">
          <header class="pm-head">
            <h3>待付款</h3>
            <button class="close-icon" @click="payOrder = null">×</button>
          </header>

          <div class="pm-body">
            <p class="pm-no">订单号：<b class="mono">{{ payOrder.orderNo }}</b></p>
            <p class="pm-amount">应付金额 <b>¥{{ payOrder.price }}</b></p>

            <p v-if="payOrder.customerServiceName" class="pm-cs-assigned">
              本单由 <b>{{ payOrder.customerServiceName }}</b> 跟进
            </p>

            <div class="pm-qr-grid">
              <div class="pm-qr-item">
                <div class="pm-qr-label wechat">💚 微信支付</div>
                <img src="/payment-wechat.png" alt="微信收款码" />
              </div>

            </div>

            <div class="pm-tip">⚠ 付款后请点击下方按钮通知客服核对</div>

            <div v-if="payOrder.userPaidAt" class="pm-marked">
              ✓ 已通知客服核对，请耐心等待
            </div>
            <button
              v-else
              class="pm-confirm-btn"
              :disabled="markingPaid"
              @click="markPaid(payOrder)"
            >
              {{ markingPaid ? '提交中…' : '✓ 我已扫码并完成付款' }}
            </button>

            <p class="pm-cs">
              客服微信：<b>{{ payOrder.customerServiceWechat || customerServiceWechat }}</b>
            </p>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- ⭐ 打手评价弹窗 -->
    <ReviewModal
      :visible="showReview"
      :order="reviewOrder"
      :from-customer="true"
      @close="showReview = false"
      @success="handleReviewSuccess"
    />

    <!-- ⭐ 客服评价弹窗 -->
    <CsReviewModal
      :visible="showCsReview"
      :order="csReviewOrder"
      :cs-name="csReviewOrder?.customerServiceName || ''"
      @close="showCsReview = false"
      @success="handleCsReviewSuccess"
    />

    <!-- ⭐ 结单 / 上传截图弹窗 -->
    <CompleteOrderModal
      :visible="showCompleteModal"
      :order="completeOrder"
      :mode="completeModalMode"
      @close="showCompleteModal = false"
      @success="loadOrders"
    />

    <!-- ⭐ 客诉弹窗 -->
    <ComplaintModal
      :visible="showComplaintModal"
      :order="complaintOrder"
      @close="showComplaintModal = false"
      @success="handleComplaintSuccess"
    />
  </div>
</template>

<script>
import { fmtTime, operatorText, isAllAccepted, showProgress } from './orderHelpers'
import { useUserStore } from '@/stores/user'
import Navbar             from './Navbar.vue'
import Footer             from './Footer.vue'
import SectionHeading     from './SectionHeading.vue'
import ReviewModal        from './ReviewModal.vue'
import CsReviewModal      from './CsReviewModal.vue'
import CompleteOrderModal from './CompleteOrderModal.vue'
import ComplaintModal     from './ComplaintModal.vue'
import request            from '@/utils/request'

export default {
  name: 'MyOrders',
  components: {
    Navbar, Footer, SectionHeading,
    ReviewModal, CsReviewModal, CompleteOrderModal, ComplaintModal
  },

  data() {
    return {
      loading: true,
      orders: [],
      filter: 'all',
      payOrder: null,
      markingPaid: false,
      customerServiceWechat: 'taichu_service',

      // 打手评价
      showReview: false,
      reviewOrder: null,
      reviewedMap: {},

      // 客服评价
      showCsReview: false,
      csReviewOrder: null,
      pendingCsOrderNos: new Set(),

      // 结单 / 上传弹窗
      showCompleteModal: false,
      completeOrder: null,
      completeModalMode: 'complete',

      // ⭐ 客诉
      showComplaintModal: false,
      complaintOrder: null,
      complaintOrderNos: new Set(),

      TABS: [
        { label: '全部',        value: 'all' },
        { label: '待付款',      value: 'unpaid' },
        { label: '待客服确认',   value: 'payConfirming' },
        { label: '待接单',      value: 'waiting' },
        { label: '服务中',      value: 'processing' },
        { label: '已完成',      value: 'completed' },
        { label: '已弃单',      value: 'abandoned' },
        { label: '已取消',      value: 'cancelled' }
      ]
    }
  },

  computed: {
    isLoggedIn() {
      return useUserStore().isLoggedIn
    },

    filteredOrders() {
      const list = this.orders
      if (this.filter === 'all') return list
      return list.filter(o => this.deriveStatus(o) === this.filter)
    }
  },

  async mounted() {
    if (!this.isLoggedIn) {
      this.loading = false
      return
    }
    await Promise.all([
      this.loadOrders(),
      this.loadPendingCsReviews(),
      this.loadMyReviews(),
      this.loadComplaintStatus()
    ])
    this.loading = false
  },

  methods: {
    async loadOrders() {
      try {
        const res = await request.get('/club/orders/my', { params: { pageSize: 50 } })
        const payload = res?.data ?? res
        this.orders = payload?.items ?? payload ?? []
      } catch (e) {
        console.error('拉取订单失败', e)
        this.orders = []
      }
    },

    async loadPendingCsReviews() {
      try {
        const res = await request.get('/club/cs/reviews/pending')
        const list = res?.data ?? res
        const arr = Array.isArray(list) ? list : []
        this.pendingCsOrderNos = new Set(arr.map(o => o.orderNo))
      } catch (e) {
        console.error('拉取待评价客服失败', e)
        this.pendingCsOrderNos = new Set()
      }
    },

    async loadMyReviews() {
      try {
        const res = await request.get('/club/orders/reviews/mine')
        const payload = res?.data ?? res
        const list = Array.isArray(payload) ? payload : (payload?.items ?? [])
        const map = {}
        for (const r of list) {
          if (!r.orderNo || !r.toUserId) continue
          if (!map[r.orderNo]) map[r.orderNo] = new Set()
          map[r.orderNo].add(r.toUserId)
        }
        this.reviewedMap = map
      } catch (e) {
        this.reviewedMap = {}
      }
    },

    /** ⭐ 加载有客诉记录的订单号 */
    async loadComplaintStatus() {
      try {
        const res = await request.get('/club/complaints/my', { params: { pageSize: 50 } })
        const payload = res?.data ?? res
        const items = payload?.items ?? []
        const set = new Set()
        for (const c of items) {
          if (c.orderNo) set.add(c.orderNo)
        }
        this.complaintOrderNos = set
      } catch (e) {
        this.complaintOrderNos = new Set()
      }
    },

    isReviewed(o, opUserId) {
      const set = this.reviewedMap[o.orderNo]
      return set ? set.has(opUserId) : false
    },

    reviewableOperators(o) {
      const list = o.operators || []
      return list.filter(op => !this.isReviewed(o, op.userId))
    },

    operatorText,

    deriveStatus(o) {
      if (o.status === 'cancelled') return 'cancelled'
      if (o.status === 'abandoned') return 'abandoned'
      if (o.status === 'completed') return 'completed'
      if (o.status === 'processing' || o.status === 'accepted') return 'processing'
      if (o.status === 'pending') {
        if (!o.userPaidAt) return 'unpaid'
        if (!o.paidConfirmedAt) return 'payConfirming'
        return 'waiting'
      }
      return 'other'
    },

    statusText(o) {
      return {
        unpaid: '待付款',
        payConfirming: '待客服确认',
        waiting: '待打手接单',
        processing: '服务中',
        completed: '已完成',
        abandoned: '已弃单',
        cancelled: '已取消'
      }[this.deriveStatus(o)] || o.status
    },

    statusHint(o) {
      const csName = o.customerServiceName || '客服'
      const st = this.deriveStatus(o)

      if (st === 'processing') {
        return '打手服务中。如服务结束，可点击下方【✓ 完成订单】结单'
      }
      if (st === 'completed') {
        return '订单已完成，感谢使用'
      }
      if (st === 'abandoned') {
        return '订单已弃单，退款将由客服线下处理'
      }

      return {
        unpaid: `请扫码付款，付款后点击【查看收款码】→【我已扫码并完成付款】`,
        payConfirming: `已通知 ${csName} 核对收款，请耐心等待（一般 5-30 分钟）`,
        waiting: `${csName} 已确认收款，正在等待打手接单`,
        cancelled: '订单已取消'
      }[st] || ''
    },

    tabCount(val) {
      if (val === 'all') return this.orders.length
      return this.orders.filter(o => this.deriveStatus(o) === val).length
    },

    showPayAction(o) {
      return o.status === 'pending' && !o.userPaidAt
    },

    showProgress,

    isAllAccepted,

    canReviewCs(o) {
      if (o.status !== 'completed') return false
      if (!o.customerServiceId) return false
      return this.pendingCsOrderNos.has(o.orderNo)
    },

    /** ⭐ 能否投诉 */
    canComplain(o) {
      if (o.status !== 'completed' && o.status !== 'abandoned') return false
      if (this.complaintOrderNos.has(o.orderNo)) return false

      const endTime = o.status === 'completed' ? o.completedAt : o.cancelledAt
      if (!endTime) return false

      const deadline = new Date(endTime).getTime() + 24 * 60 * 60 * 1000
      return Date.now() <= deadline
    },

    scoreClass(score) {
      if (score >= 5) return 's5'
      if (score === 4) return 's4'
      if (score === 3) return 's3'
      if (score === 2) return 's2'
      return 's1'
    },

    openPayment(o) {
      this.payOrder = o
    },

    async markPaid(o) {
      if (!o || this.markingPaid) return
      this.markingPaid = true
      try {
        await request.post(`/club/orders/${o.orderNo}/user-paid`)
        o.userPaidAt = new Date().toISOString()
      } catch (e) {
        alert(e?.response?.data?.message || e.message || '操作失败，请重试')
      } finally {
        this.markingPaid = false
      }
    },

    async cancelOrder(o) {
      const reason = prompt(`取消订单【${o.orderNo}】？\n请输入取消原因（可留空）:`)
      if (reason === null) return
      try {
        await request.post(`/club/orders/${o.orderNo}/cancel`, { reason })
        await this.loadOrders()
      } catch (e) {
        alert(e?.response?.data?.message || e.message || '操作失败')
      }
    },

    // ==================================================
    //  完成订单
    // ==================================================
    openCompleteModal(o) {
      this.completeOrder = o
      this.completeModalMode = 'complete'
      this.showCompleteModal = true
    },

    openUploadModal(o) {
      this.completeOrder = o
      this.completeModalMode = 'upload'
      this.showCompleteModal = true
    },

    // ==================================================
    //  评价打手
    // ==================================================
    openReview(o) {
      this.reviewOrder = o
      this.showReview = true
    },

    openReviewFor(o, operator) {
      this.reviewOrder = {
        ...o,
        operatorUserId: operator.userId,
        operatorName: operator.name
      }
      this.showReview = true
    },

    async handleReviewSuccess() {
      await Promise.all([
        this.loadOrders(),
        this.loadMyReviews()
      ])
    },

    // ==================================================
    //  评价客服
    // ==================================================
    openCsReview(o) {
      this.csReviewOrder = o
      this.showCsReview = true
    },

    async handleCsReviewSuccess() {
      await Promise.all([
        this.loadOrders(),
        this.loadPendingCsReviews()
      ])
    },

    // ==================================================
    //  ⭐ 客诉
    // ==================================================
    openComplaintModal(o) {
      this.complaintOrder = o
      this.showComplaintModal = true
    },

    async handleComplaintSuccess() {
      await this.loadComplaintStatus()
      alert('客诉已提交，客服会尽快处理')
    },

    fmtTime
  }
}
</script>

<style scoped>
.page-hero { padding: 120px 0 60px; border-bottom: 1px solid var(--line); }

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
  transition: border-color 0.2s;
}
.order-card:hover { border-color: var(--line-bright); }

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

.oc-status {
  padding: 4px 12px;
  font-size: 12px; font-weight: 600;
  letter-spacing: 1px;
  border-radius: 3px;
}
.oc-status.unpaid         { background: rgba(255, 140, 74, 0.12); color: var(--accent-color); }
.oc-status.payConfirming  { background: rgba(74, 158, 255, 0.12); color: var(--accent-color); }
.oc-status.waiting        { background: rgba(165, 165, 255, 0.12); color: var(--accent-color); }
.oc-status.processing     { background: rgba(120, 215, 206, 0.12); color: var(--cyan); }
.oc-status.completed      { background: rgba(80, 200, 120, 0.12); color: var(--success); }
.oc-status.abandoned      { background: rgba(255, 140, 74, 0.15); color: var(--accent-color); }
.oc-status.cancelled      { background: var(--bg-soft); color: var(--text-muted); }

.oc-body {
  display: flex; justify-content: space-between; gap: 20px;
  padding: 16px 20px;
}
.oc-info { flex: 1; display: flex; flex-direction: column; gap: 8px; }
.oc-row { display: flex; gap: 12px; font-size: 13px; }
.oc-row span { color: var(--text-muted); min-width: 60px; }
.oc-row b { color: var(--text); }
.oc-failed { color: var(--accent-color) !important; }

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
.oph-tag.done {
  margin-left: auto;
  padding: 2px 8px;
  font-family: monospace;
  font-size: 12px;
  border-radius: 2px;
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
}
.op-chip.accepted {
  color: var(--cyan);
  border-color: rgba(120, 215, 206, 0.4);
  background: rgba(120, 215, 206, 0.06);
}
.chip-status {
  font-family: monospace;
  font-weight: 700;
}

/* ⭐ 换人历史 */
.oc-reassign {
  padding: 12px 20px;
  background: rgba(255, 140, 74, 0.04);
  border-top: 1px solid rgba(255, 140, 74, 0.2);
}
.ocr-title {
  font-family: monospace; font-size: 12px;
  letter-spacing: 2px; color: var(--accent-color);
  margin-bottom: 10px;
}
.ocr-item {
  padding: 8px 12px;
  border-left: 2px solid rgba(255, 140, 74, 0.4);
  background: var(--panel);
  border-radius: 0 3px 3px 0;
  margin-bottom: 6px;
}
.ocr-item:last-child { margin-bottom: 0; }
.ocr-note {
  font-size: 12px;
  color: var(--text-soft);
  line-height: 1.6;
  margin-bottom: 4px;
}
.ocr-time {
  font-family: monospace; font-size: 12px;
  color: var(--text-muted);
  text-align: right;
}

.oc-hint {
  padding: 10px 20px;
  font-size: 12px; line-height: 1.6;
  color: var(--text-muted);
  background: var(--panel);
  border-top: 1px solid var(--line);
}

.oc-review-expired {
  padding: 8px 20px;
  font-size: 12px;
  color: var(--text-muted);
  background: var(--panel);
  border-top: 1px solid var(--line);
  font-family: monospace;
}

/* ⭐ 我的评价区块 */
.oc-my-reviews {
  padding: 14px 20px;
  background: var(--panel);
  border-top: 1px solid var(--line);
}
.omr-title {
  font-family: monospace; font-size: 12px;
  letter-spacing: 2px; color: var(--cyan);
  margin-bottom: 10px;
}
.omr-item {
  padding: 12px 14px;
  border-left: 2px solid rgba(120, 215, 206, 0.4);
  background: rgba(120, 215, 206, 0.03);
  border-radius: 0 3px 3px 0;
  margin-bottom: 10px;
}
.omr-item:last-child { margin-bottom: 0; }
.omr-head {
  display: flex; align-items: center; justify-content: space-between;
  gap: 10px; margin-bottom: 8px;
}
.omr-op {
  font-size: 13px; font-weight: 600;
  color: var(--text);
}
.omr-overall {
  font-family: monospace; font-size: 13px;
  font-weight: 700;
  padding: 2px 8px;
  border-radius: 3px;
}
.omr-overall.s5 { color: var(--warning); background: rgba(255, 184, 0, 0.1); }
.omr-overall.s4 { color: var(--success); background: rgba(120, 215, 206, 0.1); }
.omr-overall.s3 { color: var(--accent-color); background: rgba(255, 140, 74, 0.1); }
.omr-overall.s2 { color: var(--accent-color); background: rgba(255, 140, 74, 0.15); }
.omr-overall.s1 { color: var(--danger); background: rgba(255, 80, 80, 0.15); }

.omr-dims {
  display: flex; gap: 14px;
  font-family: monospace; font-size: 12px;
  color: var(--text-muted);
  margin-bottom: 8px;
}
.omr-comment {
  font-size: 12px; line-height: 1.7;
  color: var(--text-soft);
  padding: 8px 10px;
  background: var(--panel);
  border-radius: 3px;
  margin-bottom: 6px;
}
.omr-time {
  font-family: monospace; font-size: 12px;
  color: var(--text-muted);
  text-align: right;
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
.oc-btn.primary {
  color: var(--on-accent); background: var(--orange);
  border: 1px solid var(--orange);
}
.oc-btn.primary:hover { background: var(--orange-bright); }
.oc-btn.ghost {
  color: var(--text-soft);
  background: transparent;
  border: 1px solid var(--line-bright);
}
.oc-btn.ghost:hover { color: var(--text); border-color: var(--text-soft); }

/* ⭐ 上传/更换结算截图按钮 */
.oc-btn.upload {
  color: var(--cyan);
  background: rgba(120, 215, 206, 0.06);
  border: 1px solid rgba(120, 215, 206, 0.35);
}
.oc-btn.upload:hover {
  background: rgba(120, 215, 206, 0.12);
  border-color: rgba(120, 215, 206, 0.6);
}

/* ⭐ 客服评价按钮（紫色） */
.oc-btn.cs-btn {
  color: var(--text);
  background: var(--accent-color);
  border: 1px solid var(--accent-color);
}
.oc-btn.cs-btn:hover {
  background: var(--accent-color);
  box-shadow: 0 4px 16px rgba(165, 165, 255, 0.25);
}

/* ⭐ 投诉按钮（橙色） */
.oc-btn.complain-btn {
  color: var(--on-accent);
  background: var(--accent-color);
  border: 1px solid var(--accent-color);
}
.oc-btn.complain-btn:hover {
  background: var(--accent-color);
  box-shadow: 0 4px 16px rgba(255, 140, 74, 0.25);
}

/* ================= 收款码弹窗 ================= */
.pay-modal-mask {
  position: fixed; inset: 0; z-index: 9999;
  background: var(--overlay);
  backdrop-filter: blur(8px);
  display: grid; place-items: center;
  padding: 24px;
}
.pay-modal {
  width: min(560px, 100%);
  max-height: 88vh; overflow-y: auto;
  border: 1px solid var(--line-bright);
  background: var(--panel);
  color: var(--text);
  border-radius: 4px;
}
.pm-head {
  display: flex; justify-content: space-between; align-items: center;
  padding: 18px 24px;
  border-bottom: 1px solid var(--line);
}
.pm-head h3 { margin: 0; font-size: 16px; letter-spacing: 1px; }
.close-icon {
  background: none; border: none; font-size: 22px;
  color: var(--text-muted); cursor: pointer; line-height: 1;
}
.close-icon:hover { color: var(--text); }

.pm-body { padding: 24px; text-align: center; }
.pm-no { margin: 0 0 6px; font-size: 13px; color: var(--text-soft); }
.pm-no b { color: var(--orange); }
.pm-amount { margin: 0 0 12px; font-size: 13px; color: var(--text-soft); }
.pm-amount b { color: var(--orange); font-size: 20px; margin-left: 6px; font-family: monospace; }

.pm-cs-assigned {
  margin: 0 0 18px;
  font-size: 12px;
  color: var(--text-soft);
}
.pm-cs-assigned b {
  color: var(--cyan);
  font-family: monospace;
}

.pm-qr-grid {
  display: grid; grid-template-columns: 1fr 1fr;
  gap: 16px; margin-bottom: 16px;
}
.pm-qr-item {
  display: flex; flex-direction: column;
  align-items: center; gap: 8px;
}
.pm-qr-label {
  font-family: monospace; font-size: 12px;
  letter-spacing: 1px; font-weight: 600;
}
.pm-qr-label.wechat { color: var(--success); }
.pm-qr-label.alipay { color: var(--accent-color); }
.pm-qr-item img {
  width: 100%; max-width: 180px; height: 180px;
  object-fit: contain;
  background: var(--paper-card); padding: 8px;
  border-radius: 4px;
}

.pm-tip {
  padding: 10px 14px; margin-bottom: 16px;
  font-size: 12px; color: var(--accent-color);
  background: rgba(255, 140, 74, 0.08);
  border-radius: 3px;
}

.pm-confirm-btn {
  width: 100%; height: 48px;
  font-size: 14px; letter-spacing: 1.5px; font-weight: 700;
  color: var(--text);
  background: linear-gradient(135deg, var(--accent-color), var(--accent-color));
  border: none; border-radius: 4px;
  cursor: pointer; transition: all 0.25s;
  margin-bottom: 14px;
}
.pm-confirm-btn:hover:not(:disabled) {
  transform: translateY(-1px);
  box-shadow: 0 8px 24px rgba(255, 107, 26, 0.3);
}
.pm-confirm-btn:disabled { opacity: 0.6; cursor: not-allowed; }

.pm-marked {
  padding: 14px;
  font-size: 13px; color: var(--cyan);
  background: rgba(120, 215, 206, 0.08);
  border: 1px solid rgba(120, 215, 206, 0.3);
  border-radius: 3px;
  margin-bottom: 14px;
}

.pm-cs {
  margin: 0; font-family: monospace; font-size: 12px;
  color: var(--text-muted);
}
.pm-cs b { color: var(--cyan); }

@media (max-width: 520px) {
  .oc-body { flex-direction: column; }
  .oc-side { align-items: flex-start; }
  .pm-qr-grid { grid-template-columns: 1fr; }
  .page-hero { padding: 80px 0 40px; }
  .oc-foot { flex-direction: column; }
  .oc-btn { width: 100%; text-align: center; }
}
</style>
