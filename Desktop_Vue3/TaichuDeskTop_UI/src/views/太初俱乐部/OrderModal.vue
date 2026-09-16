<template>
  <div v-if="visible" class="order-modal-mask">
    <div class="order-modal">

      <!-- ============ 成功态 ============ -->
      <div v-if="successOrderNo" class="om-success">
        <div class="om-success-icon">✓</div>
        <h3>下单成功</h3>
        <p class="om-success-no">订单号：{{ successOrderNo }}</p>
        <p class="om-success-amount">应付金额 <b>¥{{ price }}</b></p>

        <!-- ⭐ 双收款码 -->
        <div class="om-pay">
          <div class="om-pay-title">请扫码付款</div>

          <div class="om-pay-grid">
            <div class="om-pay-item">
              <div class="om-pay-label wechat">
                <span class="pay-icon">💚</span>
                微信支付
              </div>
              <div class="om-pay-qr">
                <img src="/payment-wechat.png" alt="微信收款码" />
              </div>
            </div>

            <div class="om-pay-item">
              <div class="om-pay-label alipay">
                <span class="pay-icon">💙</span>
                支付宝
              </div>
              <div class="om-pay-qr">
                <img src="/payment-alipay.png" alt="支付宝收款码" />
              </div>
            </div>
          </div>

          <div class="om-pay-tip">
            ⚠ 付款后请点击下方【我已完成付款】，客服核对后打手方可接单
          </div>
          <div class="om-pay-cs">
            客服微信：<b>{{ customerServiceWechat }}</b>
          </div>
        </div>

        <!-- ⭐ 二次确认区 -->
        <div v-if="!userPaidMarked" class="om-confirm-pay">
          <button
            class="btn-confirm-pay"
            :disabled="markingPaid"
            @click="markPaid"
          >
            {{ markingPaid ? '提交中…' : '✓ 我已扫码并完成付款' }}
          </button>
          <p class="om-confirm-hint">
            点击后客服将立即核对收款，请确认已完成付款
          </p>
        </div>

        <div v-else class="om-paid-done">
          <div class="om-paid-done-icon">✓</div>
          <p class="om-paid-done-title">已通知客服核对</p>
          <p class="om-paid-done-hint">
            客服确认到账后，打手即可接单。请耐心等待，如有疑问联系客服微信
          </p>
        </div>

        <div class="om-success-actions">
          <button class="btn-ghost" @click="close">继续下单</button>
          <router-link to="/taichu/orders" class="btn-solid" @click="close">
            查看订单
          </router-link>
        </div>
      </div>

      <!-- ============ 下单表单 ============ -->
      <template v-else>
        <header class="om-head">
          <h3>确认下单</h3>
          <button class="close-icon" @click="close">×</button>
        </header>

        <!-- 订单信息 -->
        <section class="om-block">
          <h4 class="om-title">订单信息</h4>
          <div class="om-info">
            <div class="om-info-row">
              <span>游戏</span>
              <b>{{ gameName }}</b>
            </div>
            <div class="om-info-row">
              <span>类型</span>
              <b>{{ orderTypeName }}</b>
            </div>
            <div v-for="(v, k) in orderParams" :key="k" class="om-info-row">
              <span>{{ k }}</span>
              <b>{{ v }}</b>
            </div>
            <div class="om-info-row total">
              <span>金额</span>
              <b class="om-price">¥{{ price }}</b>
            </div>
          </div>
        </section>

        <!-- 选打手 -->
        <section class="om-block">
          <h4 class="om-title">选择打手</h4>

          <div v-if="loadingOps" class="om-empty">
            <span class="dot"></span> 加载打手…
          </div>

          <div v-else-if="availableOps.length === 0" class="om-empty">
            该游戏暂无可用打手
          </div>

          <div v-else class="op-picker">
            <label
              v-for="op in availableOps"
              :key="op.userId"
              class="op-item"
              :class="{ on: selectedOpId === op.userId }"
            >
              <input type="radio" :value="op.userId" v-model="selectedOpId" />
              <div class="op-avatar">{{ (op.name || '?').charAt(0) }}</div>
              <div class="op-info">
                <div class="op-name">
                  {{ op.name }}
                  <span class="op-level">{{ getLevelForGame(op) }}</span>
                </div>
                <div class="op-meta">
                  信誉 {{ op.reputation }} · 接单 {{ op.totalOrders }}
                </div>
              </div>
            </label>
          </div>
        </section>

        <!-- 备注 -->
        <section class="om-block">
          <h4 class="om-title">备注（可选）</h4>
          <textarea
            v-model="remark"
            class="om-textarea"
            rows="2"
            placeholder="给打手的特殊要求…"
          ></textarea>
        </section>

        <footer class="om-foot">
          <button class="btn-ghost" @click="close">取消</button>
          <button
            class="btn-solid"
            :disabled="!selectedOpId || submitting"
            @click="submit"
          >
            {{ submitting ? '提交中…' : `确认下单 ¥${price}` }}
          </button>
        </footer>
      </template>
    </div>
  </div>
</template>

<script>
import request from '@/utils/request'

export default {
  name: 'OrderModal',

  props: {
    visible: { type: Boolean, default: false },
    orderData: { type: Object, default: null }
  },

  emits: ['close', 'success'],

  data() {
    return {
      operators: [],
      loadingOps: false,
      selectedOpId: '',
      remark: '',
      submitting: false,
      successOrderNo: '',
      userPaidMarked: false,
      markingPaid: false,
      customerServiceWechat: 'taichu_service'
    }
  },

  computed: {
    gameName() { return this.orderData?.gameName || '' },
    orderTypeName() { return this.orderData?.orderTypeName || '' },
    orderParams() { return this.orderData?.params || {} },
    price() { return this.orderData?.price || 0 },
    gameCode() { return this.orderData?.gameCode || '' },

    availableOps() {
      if (!this.gameCode) return []
      return this.operators.filter(op =>
        op.games?.some(g => g.gameCode === this.gameCode)
      )
    }
  },

  watch: {
    visible(v) {
      if (v) this.onOpen()
    }
  },

  methods: {
    async onOpen() {
      this.successOrderNo = ''
      this.userPaidMarked = false
      this.selectedOpId = ''
      this.remark = ''
      await this.loadOperators()
    },

    async loadOperators() {
      this.loadingOps = true
      try {
        const res = await request.get('/club/operators/list')
        const payload = res?.data ?? res
        this.operators = payload?.items ?? payload ?? []
      } catch (e) {
        console.error('加载打手失败', e)
        this.operators = []
      } finally {
        this.loadingOps = false
      }
    },

    getLevelForGame(op) {
      const g = op.games?.find(x => x.gameCode === this.gameCode)
      return g?.level || 'L1'
    },

    async submit() {
      if (!this.selectedOpId) return
      this.submitting = true
      try {
        const res = await request.post('/club/orders', {
          gameCode: this.gameCode,
          orderTypeCode: this.orderData.orderTypeCode,
          operatorUserId: this.selectedOpId,
          params: this.orderParams,
          remark: this.remark.trim() || null
        })
        const payload = res?.data ?? res
        this.successOrderNo = payload?.orderNo || ''
        this.$emit('success', payload)
      } catch (e) {
        alert(e?.friendlyMessage || e?.response?.data?.message || e.message || '下单失败')
      } finally {
        this.submitting = false
      }
    },

    // ⭐ 用户点击"我已完成付款"
    async markPaid() {
      if (!this.successOrderNo || this.markingPaid) return

      this.markingPaid = true
      try {
        await request.post(`/club/orders/${this.successOrderNo}/user-paid`)
        this.userPaidMarked = true
      } catch (e) {
        alert(e?.response?.data?.message || e.message || '操作失败，请重试')
      } finally {
        this.markingPaid = false
      }
    },

    close() {
      this.$emit('close')
    }
  }
}
</script>

<style scoped>
/* ==================================================
   弹窗容器
================================================== */
.order-modal-mask {
  position: fixed; inset: 0; z-index: 9999;
  background: rgba(0, 0, 0, 0.78);
  backdrop-filter: blur(8px);
  display: grid; place-items: center;
  padding: 24px;
}
.order-modal {
  width: min(600px, 100%);
  max-height: 88vh;
  overflow-y: auto;
  border: 1px solid rgba(255, 255, 255, 0.15);
  background: #0e1720;
  color: #e8eef4;
  border-radius: 4px;
  box-shadow: 0 24px 80px rgba(0, 0, 0, 0.6);
}

/* ==================================================
   成功态
================================================== */
.om-success {
  padding: 48px 32px;
  text-align: center;
}
.om-success-icon {
  width: 60px; height: 60px;
  margin: 0 auto 20px;
  display: grid; place-items: center;
  border-radius: 50%;
  border: 2px solid #78d7ce;
  color: #78d7ce;
  font-size: 32px;
  font-weight: 700;
}
.om-success h3 {
  margin: 0 0 10px;
  font-size: 20px;
  color: #f2f6fa;
}
.om-success-no {
  margin: 0 0 6px;
  font-family: monospace;
  font-size: 14px;
  color: #ff6b1a;
}
.om-success-amount {
  margin: 4px 0 22px;
  font-family: monospace;
  font-size: 13px;
  color: #8b9aab;
}
.om-success-amount b {
  color: #ff6b1a;
  font-size: 18px;
  margin-left: 4px;
}

/* ============ 收款区 ============ */
.om-pay {
  margin: 0 auto 20px;
  padding: 20px;
  max-width: 520px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  background: rgba(255, 255, 255, 0.02);
  border-radius: 4px;
}
.om-pay-title {
  margin-bottom: 16px;
  font-family: monospace;
  font-size: 11px;
  letter-spacing: 1.5px;
  color: #8b9aab;
  text-align: center;
}

.om-pay-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
  margin-bottom: 16px;
}
.om-pay-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
}
.om-pay-label {
  display: flex;
  align-items: center;
  gap: 6px;
  font-family: monospace;
  font-size: 11px;
  letter-spacing: 1px;
  font-weight: 600;
}
.om-pay-label.wechat { color: #4ecb71; }
.om-pay-label.alipay { color: #4a9eff; }
.pay-icon { font-size: 13px; }

.om-pay-qr {
  display: grid;
  place-items: center;
  padding: 8px;
  background: #fff;
  border-radius: 4px;
  width: 100%;
}
.om-pay-qr img {
  width: 100%;
  max-width: 180px;
  height: 180px;
  object-fit: contain;
  display: block;
}

.om-pay-tip {
  font-size: 11px;
  line-height: 1.7;
  color: #ff8c4a;
  margin-bottom: 10px;
  text-align: center;
}
.om-pay-cs {
  font-family: monospace;
  font-size: 12px;
  color: #8b9aab;
  text-align: center;
}
.om-pay-cs b {
  color: #78d7ce;
  font-size: 14px;
  margin-left: 4px;
}

/* ⭐ 二次确认区 */
.om-confirm-pay {
  margin: 0 auto 26px;
  max-width: 520px;
  text-align: center;
}
.btn-confirm-pay {
  width: 100%;
  height: 52px;
  font-size: 15px;
  letter-spacing: 2px;
  font-weight: 700;
  color: #fff;
  background: linear-gradient(135deg, #ff6b1a, #ff8c4a);
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.25s;
  box-shadow: 0 6px 20px rgba(255, 107, 26, 0.25);
}
.btn-confirm-pay:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 10px 28px rgba(255, 107, 26, 0.35);
}
.btn-confirm-pay:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
.om-confirm-hint {
  margin: 10px 0 0;
  font-family: monospace;
  font-size: 11px;
  color: #8b9aab;
}

/* ⭐ 已标记完成状态 */
.om-paid-done {
  margin: 0 auto 26px;
  padding: 20px;
  max-width: 520px;
  border: 1px solid rgba(120, 215, 206, 0.3);
  background: rgba(120, 215, 206, 0.05);
  border-radius: 4px;
  text-align: center;
}
.om-paid-done-icon {
  width: 36px;
  height: 36px;
  margin: 0 auto 12px;
  display: grid;
  place-items: center;
  border-radius: 50%;
  border: 2px solid #78d7ce;
  color: #78d7ce;
  font-size: 18px;
  font-weight: 700;
}
.om-paid-done-title {
  margin: 0 0 8px;
  font-size: 15px;
  font-weight: 700;
  color: #78d7ce;
}
.om-paid-done-hint {
  margin: 0;
  font-size: 12px;
  line-height: 1.7;
  color: #8b9aab;
}

.om-success-actions {
  display: flex;
  gap: 12px;
  justify-content: center;
}

/* ==================================================
   下单表单
================================================== */
.om-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 20px 26px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}
.om-head h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 700;
  letter-spacing: 1px;
  color: #f2f6fa;
}
.close-icon {
  background: none;
  border: none;
  font-size: 22px;
  color: #6b7a8a;
  cursor: pointer;
  line-height: 1;
}
.close-icon:hover { color: #e8eef4; }

.om-block {
  padding: 20px 26px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
}
.om-block:last-of-type { border-bottom: none; }
.om-title {
  margin: 0 0 14px;
  font-family: monospace;
  font-size: 10px;
  letter-spacing: 2px;
  color: #ff6b1a;
}

/* 订单信息 */
.om-info { display: flex; flex-direction: column; gap: 8px; }
.om-info-row {
  display: flex;
  justify-content: space-between;
  font-size: 13px;
  padding: 6px 0;
}
.om-info-row span { color: #8b9aab; }
.om-info-row b { color: #f2f6fa; }
.om-info-row.total {
  margin-top: 6px;
  padding-top: 12px;
  border-top: 1px dashed rgba(255, 255, 255, 0.1);
}
.om-price {
  font-family: monospace;
  font-size: 20px !important;
  color: #ff6b1a !important;
}

/* 打手选择 */
.op-picker {
  display: flex;
  flex-direction: column;
  gap: 8px;
  max-height: 260px;
  overflow-y: auto;
}
.op-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 14px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  background: rgba(255, 255, 255, 0.02);
  border-radius: 3px;
  cursor: pointer;
  transition: 0.2s;
}
.op-item:hover { border-color: rgba(255, 255, 255, 0.2); }
.op-item.on {
  border-color: rgba(255, 107, 26, 0.5);
  background: rgba(255, 107, 26, 0.06);
}
.op-item input { display: none; }
.op-avatar {
  width: 38px;
  height: 38px;
  display: grid;
  place-items: center;
  border: 1px solid rgba(255, 255, 255, 0.15);
  background: rgba(255, 255, 255, 0.03);
  color: #ff6b1a;
  font-weight: 700;
  border-radius: 2px;
}
.op-info { flex: 1; min-width: 0; }
.op-name {
  font-size: 14px;
  font-weight: 700;
  color: #f2f6fa;
  margin-bottom: 3px;
  display: flex;
  align-items: center;
  gap: 8px;
}
.op-level {
  padding: 1px 6px;
  font-family: monospace;
  font-size: 10px;
  color: #ff6b1a;
  border: 1px solid rgba(255, 107, 26, 0.4);
  border-radius: 2px;
}
.op-meta { font-size: 11px; color: #8b9aab; }

/* 空态 */
.om-empty {
  padding: 24px;
  text-align: center;
  font-family: monospace;
  font-size: 12px;
  color: #6b7a8a;
  border: 1px dashed rgba(255, 255, 255, 0.1);
  border-radius: 3px;
}
.dot {
  display: inline-block;
  width: 6px;
  height: 6px;
  margin-right: 6px;
  border-radius: 50%;
  background: #ff6b1a;
  animation: pulse 1.4s infinite;
}
@keyframes pulse { 50% { opacity: 0.3; } }

/* 备注 */
.om-textarea {
  width: 100%;
  padding: 10px 12px;
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.1);
  color: #e8eef4;
  border-radius: 3px;
  font-family: inherit;
  font-size: 13px;
  outline: none;
  resize: vertical;
}
.om-textarea:focus { border-color: rgba(255, 107, 26, 0.5); }

/* 底部按钮 */
.om-foot {
  display: flex;
  gap: 12px;
  padding: 20px 26px;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
}
.btn-ghost, .btn-solid {
  flex: 1;
  height: 42px;
  font-size: 13px;
  letter-spacing: 1px;
  cursor: pointer;
  border-radius: 3px;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
  text-decoration: none;
}
.btn-ghost {
  color: #8b9aab;
  background: transparent;
  border: 1px solid rgba(255, 255, 255, 0.15);
}
.btn-ghost:hover { color: #e8eef4; border-color: rgba(255, 255, 255, 0.3); }
.btn-solid {
  color: #fff;
  background: #ff6b1a;
  border: 1px solid #ff6b1a;
}
.btn-solid:hover:not(:disabled) { background: #ff7e32; }
.btn-solid:disabled { opacity: 0.5; cursor: not-allowed; }

/* 移动端：双码竖排 */
@media (max-width: 520px) {
  .om-pay-grid { grid-template-columns: 1fr; }
  .om-pay-qr img { max-width: 200px; height: 200px; }
}
</style>