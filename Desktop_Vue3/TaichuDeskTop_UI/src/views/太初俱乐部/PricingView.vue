<template>
  <div class="page-shell">
    <Navbar />

    <main>
      <section class="page-hero">
        <div class="section-container">
          <SectionHeading index="01" title="PRICE BOARD" subtitle="服务价目 / SERVICE RATE" />

          <!-- ⭐ 指定打手提示 -->
          <div v-if="preferredOperatorId" class="preferred-hint">
            <span class="ph-dot"></span>
            <span class="ph-text">
              已为你指定打手，下单时将默认选中
            </span>
            <button class="ph-clear" @click="clearPreferredOperator">
              取消指定 ✕
            </button>
          </div>

          <!-- 游戏切换器 -->
          <div v-if="games.length > 0" class="game-switcher">
            <button
              v-for="g in games"
              :key="g.code"
              class="game-btn"
              :class="{ on: activeGameCode === g.code }"
              @click="switchGame(g.code)"
            >{{ g.name }}</button>
          </div>
        </div>
      </section>

      <section class="section-block alt">
        <div class="section-container">
          <div v-if="loading" class="loading-state">
            <span class="dot"></span> 正在读取价目…
          </div>

          <div v-else-if="orderTypes.length === 0" class="empty-state">
            该游戏暂未开放服务项目
          </div>

          <PriceBoard
            v-else
            :groups="orderTypes"
            :common-rules="activeGame ? (activeGame.commonRulesText || '') : ''"
            @order="handleOrder"
          />
        </div>
      </section>
    </main>

    <Footer />

    <OrderModal
      :visible="showOrderModal"
      :order-data="pendingOrder"
      @close="closeOrderModal"
      @success="handleOrderSuccess"
    />
  </div>
</template>

<script>
import { useUserStore } from '@/stores/user'
import Navbar         from './Navbar.vue'
import Footer         from './Footer.vue'
import SectionHeading from './SectionHeading.vue'
import PriceBoard     from './PriceBoard.vue'
import OrderModal     from './OrderModal.vue'
import request        from '@/utils/request'

export default {
  name: 'PricingView',
  components: { Navbar, Footer, SectionHeading, PriceBoard, OrderModal },

  data() {
    return {
      games: [],
      activeGameCode: '',
      orderTypes: [],
      loading: true,

      // ⭐ 指定打手 ID（来自 URL query）
      preferredOperatorId: '',

      showOrderModal: false,
      pendingOrder: null
    }
  },

  computed: {
    activeGame() {
      return this.games.find(g => g.code === this.activeGameCode) || null
    }
  },

  async mounted() {
    // ⭐ 读 URL 参数
    this.preferredOperatorId = this.$route.query.operatorUserId || ''

    await this.loadGames()
  },

  methods: {
    async loadGames() {
      try {
        const res = await request.get('/club/games')
        const payload = res?.data ?? res
        this.games = Array.isArray(payload) ? payload : (payload?.items ?? [])

        if (this.games.length > 0) {
          this.activeGameCode = this.games[0].code
          await this.loadOrderTypes()
        }
      } catch (e) {
        console.error('拉取游戏失败', e)
      } finally {
        this.loading = false
      }
    },

    async switchGame(code) {
      if (code === this.activeGameCode) return
      this.activeGameCode = code
      this.loading = true
      await this.loadOrderTypes()
      this.loading = false
    },

    async loadOrderTypes() {
      if (!this.activeGameCode) return
      try {
        const res = await request.get('/club/order-types', {
          params: { gameCode: this.activeGameCode }
        })
        const payload = res?.data ?? res
        this.orderTypes = Array.isArray(payload) ? payload : (payload?.items ?? [])
      } catch (e) {
        console.error('拉取订单类型失败', e)
        this.orderTypes = []
      }
    },

    handleOrder(payload) {
  if (!useUserStore().token) {
    alert('请先登录')
    return
  }

  this.pendingOrder = {
    gameCode: this.activeGameCode,
    gameName: this.activeGame?.name || '',
    orderTypeCode: payload.orderTypeCode,
    orderTypeName: payload.orderTypeName,
    params: payload.params,
    price: payload.price,
    // ⭐ 新增
    pricingMode: payload.pricingMode,
    minOperatorCount: payload.minOperatorCount,
    maxOperatorCount: payload.maxOperatorCount,
    // 原有
    preferredOperatorId: this.preferredOperatorId
  }
  this.showOrderModal = true
},

    closeOrderModal() {
      this.showOrderModal = false
      this.pendingOrder = null
    },

    handleOrderSuccess(order) {
      console.log('下单成功:', order)

      // ⭐ 下单成功后清掉"指定打手"
      this.clearPreferredOperator()
    },

    /** ⭐ 清除指定打手 */
    clearPreferredOperator() {
      this.preferredOperatorId = ''
      if (this.$route.query.operatorUserId) {
        this.$router.replace({ path: '/taichu/pricing' })
      }
    }
  }
}
</script>

<style scoped>
.page-hero { padding: 120px 0 60px; border-bottom: 1px solid var(--line); }

/* ⭐ 指定打手提示条 */
.preferred-hint {
  display: flex; align-items: center; gap: 10px;
  padding: 10px 14px;
  margin-top: 20px;
  border: 1px solid rgba(255, 107, 26, 0.35);
  background: rgba(255, 107, 26, 0.06);
  border-radius: 3px;
  font-size: 12px;
  color: var(--text-soft);
}
.ph-dot {
  width: 6px; height: 6px; border-radius: 50%;
  background: var(--orange);
  box-shadow: 0 0 8px var(--orange);
  flex-shrink: 0;
}
.ph-text { flex: 1; }
.ph-clear {
  background: transparent;
  border: 1px solid var(--line-bright);
  color: var(--text-muted);
  font-family: monospace;
  font-size: 12px;
  padding: 4px 10px;
  cursor: pointer;
  border-radius: 2px;
  transition: 0.2s;
}
.ph-clear:hover {
  color: var(--orange);
  border-color: rgba(255, 107, 26, 0.5);
}

.game-switcher {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  margin-top: 30px;
}
.game-btn {
  padding: 9px 20px;
  font-size: 12px;
  letter-spacing: 1px;
  color: var(--text-soft);
  background: transparent;
  border: 1px solid var(--line);
  cursor: pointer;
  transition: all 0.2s ease;
}
.game-btn:hover { color: var(--text); border-color: var(--line-bright); }
.game-btn.on {
  color: var(--orange);
  border-color: rgba(255, 107, 26, 0.5);
  background: rgba(255, 107, 26, 0.08);
}

.loading-state,
.empty-state {
  padding: 60px 32px;
  text-align: center;
  font-family: monospace;
  font-size: 12px;
  letter-spacing: 1px;
  color: var(--text-muted);
  border: 1px dashed var(--line);
  background: var(--panel);
}
.dot {
  display: inline-block;
  width: 6px; height: 6px;
  margin-right: 6px;
  border-radius: 50%;
  background: var(--orange);
  animation: pulse 1.4s infinite;
}
@keyframes pulse { 50% { opacity: 0.3; } }

@media (max-width: 768px) {
  .page-hero { padding: 80px 0 40px; }
}
</style>
