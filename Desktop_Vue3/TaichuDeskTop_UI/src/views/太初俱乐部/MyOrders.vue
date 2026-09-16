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

          <!-- 状态筛选 -->
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
                    <b>{{ o.operatorName || '—' }}</b>
                  </div>
                </div>

                <div class="oc-side">
                  <div class="oc-price">¥{{ o.price }}</div>
                  <div class="oc-time mono">{{ fmtTime(o.createdAt) }}</div>
                </div>
              </div>

              <div v-if="statusHint(o)" class="oc-hint">
                {{ statusHint(o) }}
              </div>

              <footer class="oc-foot">
                <button
                  v-if="showPayAction(o)"
                  class="oc-btn primary"
                  @click="openPayment(o)"
                >
                  查看收款码
                </button>

                <button
                  v-if="o.status === 'completed'"
                  class="oc-btn primary"
                  @click="openReview(o)"
                >
                  去评价
                </button>

                <button
                  v-if="o.status === 'pending'"
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

            <div class="pm-qr-grid">
              <div class="pm-qr-item">
                <div class="pm-qr-label wechat">💚 微信支付</div>
                <img src="/payment-wechat.png" alt="微信收款码" />
              </div>
              <div class="pm-qr-item">
                <div class="pm-qr-label alipay">💙 支付宝</div>
                <img src="/payment-alipay.png" alt="支付宝收款码" />
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
              客服微信：<b>{{ customerServiceWechat }}</b>
            </p>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- 评价弹窗 -->
    <ReviewModal
      :visible="showReview"
      :order="reviewOrder"
      :from-customer="reviewFromCustomer"
      @close="showReview = false"
      @success="loadOrders"
    />
  </div>
</template>

<script>
import Navbar         from './Navbar.vue'
import Footer         from './Footer.vue'
import SectionHeading from './SectionHeading.vue'
import ReviewModal    from './ReviewModal.vue'
import request        from '@/utils/request'

export default {
  name: 'MyOrders',
  components: { Navbar, Footer, SectionHeading, ReviewModal },

  data() {
    return {
      loading: true,
      orders: [],
      filter: 'all',
      payOrder: null,
      markingPaid: false,
      customerServiceWechat: 'taichu_service',

      showReview: false,
      reviewOrder: null,
      reviewFromCustomer: true,

      TABS: [
        { label: '全部',        value: 'all' },
        { label: '待付款',      value: 'unpaid' },
        { label: '待客服确认',   value: 'payConfirming' },
        { label: '待接单',      value: 'waiting' },
        { label: '服务中',      value: 'processing' },
        { label: '已完成',      value: 'completed' },
        { label: '已取消',      value: 'cancelled' }
      ]
    }
  },

  computed: {
    isLoggedIn() {
      return !!localStorage.getItem('token')
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
    await this.loadOrders()
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

    deriveStatus(o) {
      if (o.status === 'cancelled') return 'cancelled'
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
        cancelled: '已取消'
      }[this.deriveStatus(o)] || o.status
    },

    statusHint(o) {
      return {
        unpaid: '请扫码付款，付款后点击【查看收款码】→【我已扫码并完成付款】',
        payConfirming: '已通知客服核对收款，请耐心等待（一般 5-30 分钟）',
        waiting: '客服已确认收款，正在等待打手接单',
        processing: '打手已接单，服务进行中',
        completed: '订单已完成，感谢使用',
        cancelled: '订单已取消'
      }[this.deriveStatus(o)] || ''
    },

    tabCount(val) {
      if (val === 'all') return this.orders.length
      return this.orders.filter(o => this.deriveStatus(o) === val).length
    },

    showPayAction(o) {
      return o.status === 'pending' && !o.userPaidAt
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

    openReview(o) {
      this.reviewOrder = o
      this.reviewFromCustomer = true
      this.showReview = true
    },

    fmtTime(iso) {
      if (!iso) return '—'
      const d = new Date(iso)
      const pad = (n) => String(n).padStart(2, '0')
      return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
    }
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
  cursor: pointer;
  transition: 0.2s;
  display: inline-flex; align-items: center; gap: 6px;
}
.order-tab:hover { color: var(--text); border-color: var(--line-bright); }
.order-tab.active {
  color: var(--orange);
  border-color: rgba(255, 107, 26, 0.5);
  background: rgba(255, 107, 26, 0.06);
}
.tab-count {
  font-family: monospace; font-size: 10px;
  padding: 1px 6px;
  background: rgba(255, 255, 255, 0.08);
  border-radius: 3px;
}

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

.order-list { display: flex; flex-direction: column; gap: 16px; }

.order-card {
  border: 1px solid var(--line);
  background: rgba(16, 25, 35, 0.5);
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
  font-family: monospace; font-size: 9px;
  letter-spacing: 1px; color: var(--text-muted);
}
.oc-code { font-size: 13px; color: var(--text); }
.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }

.oc-status {
  padding: 4px 12px;
  font-size: 11px; font-weight: 600;
  letter-spacing: 1px;
  border-radius: 3px;
}
.oc-status.unpaid         { background: #fff7ed; color: #c2410c; }
.oc-status.payConfirming  { background: #eff6ff; color: #2563eb; }
.oc-status.waiting        { background: #f3e8ff; color: #a855f7; }
.oc-status.processing     { background: #e0f2fe; color: #0369a1; }
.oc-status.completed      { background: #e3fcef; color: #00875a; }
.oc-status.cancelled      { background: #f5f5f5; color: #888; }

.oc-body {
  display: flex; justify-content: space-between; gap: 20px;
  padding: 16px 20px;
}
.oc-info { flex: 1; display: flex; flex-direction: column; gap: 8px; }
.oc-row { display: flex; gap: 12px; font-size: 13px; }
.oc-row span { color: var(--text-muted); min-width: 60px; }
.oc-row b { color: var(--text); }

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
.oc-time { font-size: 11px; color: var(--text-muted); }

.oc-hint {
  padding: 10px 20px;
  font-size: 12px; line-height: 1.6;
  color: var(--text-muted);
  background: rgba(0, 0, 0, 0.2);
  border-top: 1px solid var(--line);
}

.oc-foot {
  display: flex; gap: 10px; justify-content: flex-end;
  padding: 14px 20px;
  border-top: 1px solid var(--line);
}
.oc-btn {
  padding: 8px 20px;
  font-size: 12px; letter-spacing: 1px;
  cursor: pointer; transition: all 0.2s;
  border-radius: 3px;
}
.oc-btn.primary {
  color: #fff; background: var(--orange);
  border: 1px solid var(--orange);
}
.oc-btn.primary:hover { background: var(--orange-bright); }
.oc-btn.ghost {
  color: var(--text-soft);
  background: transparent;
  border: 1px solid var(--line-bright);
}
.oc-btn.ghost:hover { color: var(--text); border-color: var(--text-soft); }

.pay-modal-mask {
  position: fixed; inset: 0; z-index: 9999;
  background: rgba(0, 0, 0, 0.78);
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
.pm-amount { margin: 0 0 22px; font-size: 13px; color: var(--text-soft); }
.pm-amount b { color: var(--orange); font-size: 20px; margin-left: 6px; font-family: monospace; }

.pm-qr-grid {
  display: grid; grid-template-columns: 1fr 1fr;
  gap: 16px; margin-bottom: 16px;
}
.pm-qr-item {
  display: flex; flex-direction: column;
  align-items: center; gap: 8px;
}
.pm-qr-label {
  font-family: monospace; font-size: 11px;
  letter-spacing: 1px; font-weight: 600;
}
.pm-qr-label.wechat { color: #4ecb71; }
.pm-qr-label.alipay { color: #4a9eff; }
.pm-qr-item img {
  width: 100%; max-width: 180px; height: 180px;
  object-fit: contain;
  background: #fff; padding: 8px;
  border-radius: 4px;
}

.pm-tip {
  padding: 10px 14px; margin-bottom: 16px;
  font-size: 12px; color: #ff8c4a;
  background: rgba(255, 140, 74, 0.08);
  border-radius: 3px;
}

.pm-confirm-btn {
  width: 100%; height: 48px;
  font-size: 14px; letter-spacing: 1.5px; font-weight: 700;
  color: #fff;
  background: linear-gradient(135deg, #ff6b1a, #ff8c4a);
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
}
</style>