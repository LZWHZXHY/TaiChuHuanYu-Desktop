<template>
  <div class="page-shell">
    <Navbar />
    <main>
      <section class="page-hero">
        <div class="section-container">
          <SectionHeading index="01" title="REVIEWS" subtitle="我的评价 / REVIEWS" />
        </div>
      </section>

      <section class="section-block alt">
        <div class="section-container">

          <div v-if="!isLoggedIn" class="op-state">请先登录后查看评价</div>
          <div v-else-if="loading" class="op-state">
            <span class="dot"></span> 正在读取评价…
          </div>

          <template v-else>
            <!-- 待评价 - 作为老板 -->
            <section v-if="asBoss.length > 0" class="rv-section">
              <h3 class="rv-title">作为老板 · 待评价（{{ asBoss.length }}）</h3>
              <div class="rv-list">
                <div v-for="o in asBoss" :key="o.orderNo" class="rv-row">
                  <div class="rv-info">
                    <div class="rv-no mono">{{ o.orderNo }}</div>
                    <div class="rv-meta">
                      {{ o.gameName }} · {{ o.orderTypeName }} ·
                      打手 {{ o.operatorName || '—' }}
                    </div>
                  </div>
                  <button class="rv-btn primary" @click="openReview(o, true)">去评价</button>
                </div>
              </div>
            </section>

            <!-- 待评价 - 作为打手 -->
            <section v-if="asOperator.length > 0" class="rv-section">
              <h3 class="rv-title">作为打手 · 待评价（{{ asOperator.length }}）</h3>
              <div class="rv-list">
                <div v-for="o in asOperator" :key="o.orderNo" class="rv-row">
                  <div class="rv-info">
                    <div class="rv-no mono">{{ o.orderNo }}</div>
                    <div class="rv-meta">
                      {{ o.gameName }} · {{ o.orderTypeName }} ·
                      老板 {{ o.customerName || '—' }}
                    </div>
                  </div>
                  <button class="rv-btn primary" @click="openReview(o, false)">去评价</button>
                </div>
              </div>
            </section>

            <div v-if="asBoss.length === 0 && asOperator.length === 0" class="op-state">
              暂无待评价订单
            </div>
          </template>

        </div>
      </section>
    </main>
    <Footer />

    <ReviewModal
      :visible="showReview"
      :order="reviewOrder"
      :from-customer="reviewFromCustomer"
      @close="showReview = false"
      @success="handleReviewSuccess"
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
  name: 'OperatorReviews',
  components: { Navbar, Footer, SectionHeading, ReviewModal },

  data() {
    return {
      loading: true,
      asBoss: [],
      asOperator: [],
      showReview: false,
      reviewOrder: null,
      reviewFromCustomer: true
    }
  },

  computed: {
    isLoggedIn() { return !!localStorage.getItem('token') }
  },

  async mounted() {
    if (!this.isLoggedIn) { this.loading = false; return }
    await this.loadPending()
    this.loading = false
  },

  methods: {
    async loadPending() {
      try {
        const res = await request.get('/club/orders/reviews/pending')
        const data = res?.data ?? res
        this.asBoss = data?.asBoss ?? []
        this.asOperator = data?.asOperator ?? []
      } catch (e) {
        console.error('加载待评价失败', e)
        this.asBoss = []
        this.asOperator = []
      }
    },

    openReview(order, fromCustomer) {
      this.reviewOrder = order
      this.reviewFromCustomer = fromCustomer
      this.showReview = true
    },

    async handleReviewSuccess() {
      await this.loadPending()
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
  background: var(--orange); animation: pulse 1.4s infinite;
}
@keyframes pulse { 50% { opacity: 0.3; } }

.rv-section { margin-bottom: 40px; }
.rv-title {
  margin: 0 0 16px;
  font-family: monospace; font-size: 11px;
  letter-spacing: 2px; color: var(--orange); font-weight: 600;
}

.rv-list { border: 1px solid var(--line); background: rgba(16, 25, 35, 0.4); }
.rv-row {
  display: flex; align-items: center; justify-content: space-between;
  gap: 16px; padding: 16px 22px;
  border-bottom: 1px solid var(--line);
}
.rv-row:last-child { border-bottom: none; }
.rv-info { min-width: 0; }
.rv-no { font-size: 13px; color: var(--text); margin-bottom: 6px; }
.rv-meta { font-size: 11px; color: var(--text-muted); }
.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }

.rv-btn {
  padding: 8px 20px; font-size: 12px; letter-spacing: 1px;
  cursor: pointer; transition: 0.2s; border-radius: 3px;
  flex-shrink: 0;
}
.rv-btn.primary {
  color: #fff; background: var(--orange); border: 1px solid var(--orange);
}
.rv-btn.primary:hover { background: var(--orange-bright); }

@media (max-width: 520px) {
  .page-hero { padding: 80px 0 40px; }
  .rv-row { flex-direction: column; align-items: stretch; gap: 12px; }
}
</style>