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

            <div v-if="filteredOrders.length === 0" class="op-state">
              当前没有可执行的任务
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
                      <span>老板</span>
                      <b>{{ o.customerName || shortId(o.customerId) }}</b>
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

                <div v-if="statusHint(o)" class="oc-hint">{{ statusHint(o) }}</div>

                <footer class="oc-foot">
                  <button
                    v-if="o.status === 'pending'"
                    class="oc-btn primary"
                    :disabled="acting === o.orderNo"
                    @click="doAction(o, 'accept')"
                  >
                    {{ acting === o.orderNo ? '处理中…' : '接单' }}
                  </button>

                  <button
                    v-if="o.status === 'accepted'"
                    class="oc-btn primary"
                    :disabled="acting === o.orderNo"
                    @click="doAction(o, 'start')"
                  >
                    {{ acting === o.orderNo ? '处理中…' : '开始服务' }}
                  </button>

                  <button
                    v-if="o.status === 'processing'"
                    class="oc-btn primary"
                    :disabled="acting === o.orderNo"
                    @click="doAction(o, 'complete')"
                  >
                    {{ acting === o.orderNo ? '处理中…' : '完成订单' }}
                  </button>

                  <button
                    v-if="o.status === 'completed'"
                    class="oc-btn primary"
                    @click="openReview(o)"
                  >
                    去评价
                  </button>

                  <button
                    v-if="o.status === 'pending' || o.status === 'accepted'"
                    class="oc-btn ghost"
                    :disabled="acting === o.orderNo"
                    @click="cancelOrder(o)"
                  >
                    取消
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

const LEVEL_RATE = { L1: 60, L2: 68, L3: 76, L4: 84, L5: 92 }

export default {
  name: 'OperatorOrders',
  components: { Navbar, Footer, SectionHeading, ReviewModal },

  data() {
    return {
      loading: true,
      isOperator: false,
      orders: [],
      filter: 'all',
      acting: '',
      myLevels: {},

      showReview: false,
      reviewOrder: null,
      reviewFromCustomer: false,

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
      return !!localStorage.getItem('token')
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
    await this.checkOperator()
    if (this.isOperator) {
      await this.loadOrders()
    }
    this.loading = false
  },

  methods: {
    async checkOperator() {
      try {
        const res = await request.get('/club/operators/me')
        const data = res?.data ?? res
        if (!data?.hasApplied) {
          this.isOperator = false
          return
        }
        const approved = (data.games || []).filter(g => g.auditStatus === 'approved')
        this.isOperator = approved.length > 0
        approved.forEach(g => {
          this.myLevels[g.gameCode] = g.level || 'L1'
        })
      } catch (e) {
        console.error('读取打手状态失败', e)
        this.isOperator = false
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
      if (o.status === 'pending')    return '客服已确认收款，请尽快接单'
      if (o.status === 'accepted')   return '已接单，准备开始服务'
      if (o.status === 'processing') return '服务进行中，完成后请点【完成订单】'
      if (o.status === 'completed')  return '订单已完成'
      if (o.status === 'cancelled')  return '订单已取消'
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
      this.acting = o.orderNo
      try {
        await request.post(`/club/orders/${o.orderNo}/${action}`)
        await this.loadOrders()
      } catch (e) {
        alert(e?.response?.data?.message || e.message || '操作失败')
      } finally {
        this.acting = ''
      }
    },

    async cancelOrder(o) {
      const reason = prompt(`取消订单【${o.orderNo}】？\n请输入取消原因（必填）:`)
      if (reason === null) return
      if (!reason.trim()) return alert('请填写取消原因')

      this.acting = o.orderNo
      try {
        await request.post(`/club/orders/${o.orderNo}/cancel`, { reason: reason.trim() })
        await this.loadOrders()
      } catch (e) {
        alert(e?.response?.data?.message || e.message || '操作失败')
      } finally {
        this.acting = ''
      }
    },

    openReview(o) {
      this.reviewOrder = o
      this.reviewFromCustomer = false
      this.showReview = true
    },

    shortId(uid) {
      return uid ? String(uid).substring(0, 8).toUpperCase() : '—'
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
.oc-status.pending    { background: rgba(255, 140, 74, 0.12); color: #ff8c4a; }
.oc-status.serving    { background: rgba(120, 215, 206, 0.12); color: var(--cyan); }
.oc-status.completed  { background: rgba(80, 200, 120, 0.12); color: #4ecb71; }
.oc-status.cancelled  { background: rgba(255, 255, 255, 0.05); color: var(--text-muted); }

.oc-body {
  display: flex; justify-content: space-between; gap: 20px;
  padding: 16px 20px;
}
.oc-info { flex: 1; display: flex; flex-direction: column; gap: 8px; }
.oc-row { display: flex; gap: 12px; font-size: 13px; }
.oc-row span { color: var(--text-muted); min-width: 60px; }
.oc-row b { color: var(--text); }
.oc-remark { color: var(--text-soft) !important; font-weight: 400; }

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
  font-family: monospace; font-size: 11px;
  color: var(--text-muted);
}
.oc-payout b { color: var(--cyan); }
.oc-rate { color: var(--text-muted); margin-left: 4px; }
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
.oc-btn:disabled { opacity: 0.5; cursor: not-allowed; }
.oc-btn.primary {
  color: #fff; background: var(--orange);
  border: 1px solid var(--orange);
}
.oc-btn.primary:hover:not(:disabled) { background: var(--orange-bright); }
.oc-btn.ghost {
  color: var(--text-soft);
  background: transparent;
  border: 1px solid var(--line-bright);
}
.oc-btn.ghost:hover:not(:disabled) { color: var(--text); border-color: var(--text-soft); }

@media (max-width: 520px) {
  .oc-body { flex-direction: column; }
  .oc-side { align-items: flex-start; }
  .page-hero { padding: 80px 0 40px; }
}
</style>