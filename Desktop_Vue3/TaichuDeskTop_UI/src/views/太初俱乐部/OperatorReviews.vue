<template>
  <div class="page-shell">
    <Navbar />
    <main>
      <section class="page-hero">
        <div class="section-container">
          <SectionHeading index="01" title="REVIEWS" subtitle="我的待评价 / PENDING REVIEWS" />
        </div>
      </section>

      <section class="section-block alt">
        <div class="section-container">

          <div v-if="!isLoggedIn" class="op-state">请先登录后查看待评价</div>
          <div v-else-if="loading" class="op-state">
            <span class="dot"></span> 正在读取待评价…
          </div>

          <template v-else>
            <!-- 待评价 - 作为老板 -->
            <section v-if="asBoss.length > 0" class="rv-section">
              <h3 class="rv-title">
                待评价打手（{{ asBoss.length }}）
              </h3>
              <div class="rv-list">
                <div v-for="o in asBoss" :key="o.orderNo + '-' + o.operatorUserId" class="rv-row">
                  <div class="rv-info">
                    <div class="rv-no mono">{{ o.orderNo }}</div>
                    <div class="rv-meta">
                      {{ o.gameName }} · {{ o.orderTypeName }} ·
                      打手 <b class="rv-op">{{ o.operatorName || '—' }}</b>
                      <span v-if="o.operatorCount > 1" class="rv-count">
                        （本单共 {{ o.operatorCount }} 位打手，分别评价）
                      </span>
                    </div>
                  </div>
                  <button class="rv-btn primary" @click="openReview(o)">去评价</button>
                </div>
              </div>
            </section>

            <div v-if="asBoss.length === 0" class="op-state">
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
      :from-customer="true"
      @close="showReview = false"
      @success="handleReviewSuccess"
    />
  </div>
</template>

<script>
import { useUserStore } from '@/stores/user'
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
      showReview: false,
      reviewOrder: null
    }
  },

  computed: {
    isLoggedIn() { return useUserStore().isLoggedIn }
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
        // ⭐ 后端返回 { asBoss: [...], asOperator: [...] }，
        //    但 asOperator 已下线，只读 asBoss
        this.asBoss = data?.asBoss ?? []
      } catch (e) {
        console.error('加载待评价失败', e)
        this.asBoss = []
      }
    },

    /** ⭐ 直接打开评价弹窗，order 里已带 operatorUserId / operatorName */
    openReview(order) {
      this.reviewOrder = order
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
  background: var(--panel);
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
  font-family: monospace; font-size: 12px;
  letter-spacing: 2px; color: var(--orange); font-weight: 600;
}

.rv-list { border: 1px solid var(--line); background: var(--panel); }
.rv-row {
  display: flex; align-items: center; justify-content: space-between;
  gap: 16px; padding: 16px 22px;
  border-bottom: 1px solid var(--line);
}
.rv-row:last-child { border-bottom: none; }
.rv-info { min-width: 0; }
.rv-no { font-size: 13px; color: var(--text); margin-bottom: 6px; }
.rv-meta { font-size: 12px; color: var(--text-muted); }
.rv-op { color: var(--cyan); font-weight: 600; }
.rv-count { color: var(--text-muted); margin-left: 4px; }
.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }

.rv-btn {
  padding: 8px 20px; font-size: 12px; letter-spacing: 1px;
  cursor: pointer; transition: 0.2s; border-radius: 3px;
  flex-shrink: 0;
}
.rv-btn.primary {
  color: var(--on-accent); background: var(--orange); border: 1px solid var(--orange);
}
.rv-btn.primary:hover { background: var(--orange-bright); }

@media (max-width: 520px) {
  .page-hero { padding: 80px 0 40px; }
  .rv-row { flex-direction: column; align-items: stretch; gap: 12px; }
}
</style>
