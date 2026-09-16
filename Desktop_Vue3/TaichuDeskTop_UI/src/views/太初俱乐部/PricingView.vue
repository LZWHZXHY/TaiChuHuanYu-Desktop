<template>
  <div class="page-shell">
    <Navbar />

    <main>
      <section class="page-hero">
        <div class="section-container">
          <SectionHeading index="01" title="PRICE BOARD" subtitle="服务价目 / SERVICE RATE" />

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

    <!-- ⭐ 下单弹窗 -->
    <OrderModal
      :visible="showOrderModal"
      :order-data="pendingOrder"
      @close="closeOrderModal"
      @success="handleOrderSuccess"
    />
  </div>
</template>

<script>
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

      // ⭐ 下单弹窗状态
      showOrderModal: false,
      pendingOrder: null
    }
  },

  computed: {
    // 当前选中的游戏对象（含 commonRulesText）
    activeGame() {
      return this.games.find(g => g.code === this.activeGameCode) || null
    }
  },

  async mounted() {
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

    // ⭐ 点"下单" → 打开弹窗
    handleOrder(payload) {
      if (!localStorage.getItem('token')) {
        alert('请先登录')
        return
      }

      this.pendingOrder = {
        gameCode: this.activeGameCode,
        gameName: this.activeGame?.name || '',
        orderTypeCode: payload.orderTypeCode,
        orderTypeName: payload.orderTypeName,
        params: payload.params,
        price: payload.price
      }
      this.showOrderModal = true
    },

    // ⭐ 关闭弹窗
    closeOrderModal() {
      this.showOrderModal = false
      this.pendingOrder = null
    },

    // ⭐ 下单成功回调
    handleOrderSuccess(order) {
      console.log('下单成功:', order)
      // 订单号已在弹窗内显示，这里只做记录
    }
  }
}
</script>

<style scoped>
.page-hero { padding: 120px 0 60px; border-bottom: 1px solid var(--line); }

/* 游戏切换器 */
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

/* 加载 / 空态 */
.loading-state,
.empty-state {
  padding: 60px 32px;
  text-align: center;
  font-family: monospace;
  font-size: 12px;
  letter-spacing: 1px;
  color: var(--text-muted);
  border: 1px dashed var(--line);
  background: rgba(16, 25, 35, 0.3);
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