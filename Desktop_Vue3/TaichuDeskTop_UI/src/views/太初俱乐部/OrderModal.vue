<template>
  <div v-if="visible" class="order-modal-mask">
    <div class="order-modal">

      <!-- ============ 成功态 ============ -->
      <div v-if="successOrderNo" class="om-success">
        <div class="om-success-icon">✓</div>
        <h3>下单成功</h3>
        <p class="om-success-no">订单号：{{ successOrderNo }}</p>
        <p class="om-success-amount">应付金额 <b>¥{{ price }}</b></p>

        <div class="om-assign">
          <div class="oa-row">
            <span>接单客服</span>
            <b>{{ submittedCsLabel }}</b>
          </div>
          <div class="oa-row">
            <span>打手安排</span>
            <b>{{ submittedOpLabel }}</b>
          </div>
        </div>

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

            <!--

            <div class="om-pay-item">
              <div class="om-pay-label alipay">
                <span class="pay-icon">💙</span>
                支付宝
              </div>
              <div class="om-pay-qr">
                <img src="/payment-alipay.png" alt="支付宝收款码" />
              </div>
            </div>

            -->

          </div>

          <div class="om-pay-tip">
            ⚠ 付款后请点击下方【我已完成付款】，客服核对后打手方可接单
          </div>
          <div class="om-pay-cs">
            客服微信：<b>{{ customerServiceWechat }}</b>
          </div>
        </div>

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

        <!-- 订单信息 + 实时算价 -->
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

            <!-- ⭐ 价格明细 -->
            <template v-if="isLevelBased && selectedOps.length > 0">
              <div class="om-info-row sub">
                <span>基础价</span>
                <b>¥{{ basePrice.toFixed(2) }}</b>
              </div>
              <div
                v-for="op in selectedOps"
                :key="'pd-' + op.userId"
                class="om-info-row sub"
              >
                <span>· {{ op.name }} ({{ getLevelForGame(op) }})</span>
                <b>+ ¥{{ opSurcharge(op).toFixed(2) }}</b>
              </div>
            </template>

            <div class="om-info-row total">
              <span>金额</span>
              <b class="om-price">¥{{ price }}</b>
            </div>
          </div>
        </section>

        <!-- 客服来源 -->
        <section class="om-block">
          <h4 class="om-title">客服来源</h4>

          <div class="source-grid">
            <label class="source-item" :class="{ on: orderSource === 'cs' }">
              <input type="radio" value="cs" v-model="orderSource" />
              <div class="source-icon">👤</div>
              <div class="source-info">
                <div class="source-name">客服拉单</div>
                <div class="source-desc">指定对接你的客服</div>
              </div>
            </label>

            <label class="source-item" :class="{ on: orderSource === 'self' }">
              <input type="radio" value="self" v-model="orderSource" />
              <div class="source-icon">⚡</div>
              <div class="source-info">
                <div class="source-name">自己找的</div>
                <div class="source-desc">系统随机分配客服</div>
              </div>
            </label>
          </div>

          <div v-if="orderSource === 'cs'" class="cs-picker-wrap">
            <div v-if="loadingCs" class="om-empty">
              <span class="dot"></span> 正在加载客服…
            </div>

            <div v-else-if="customerServices.length === 0" class="om-empty">
              暂无在线客服，请改选「自己找的」由系统分配
            </div>

            <div v-else class="cs-picker">
              <label
                v-for="cs in customerServices"
                :key="cs.id"
                class="cs-item"
                :class="{ on: selectedCsId === cs.id, off: cs.status === 'offline' }"
              >
                <input
                  type="radio"
                  :value="cs.id"
                  v-model="selectedCsId"
                  :disabled="cs.status === 'offline'"
                />
                <div class="cs-avatar">{{ (cs.name || '?').charAt(0) }}</div>
                <div class="cs-info">
                  <div class="cs-name">
                    {{ cs.name }}
                    <OnlineStatusBadge :status="cs.status" />
                  </div>
                  <div class="cs-meta">
                    微信 {{ cs.wechat }} · 载单 {{ cs.currentLoad }}/{{ cs.capacity }}
                  </div>
                </div>
              </label>
            </div>
          </div>

          <div v-else class="auto-hint">
            ⓘ 订单提交后，系统将随机分配一位在线客服跟进
          </div>
        </section>

        <!-- ⭐ 打手选择（多选 + 数量约束 + 等级增幅） -->
        <section class="om-block">
          <h4 class="om-title">打手选择</h4>

          <div class="mode-grid">
            <button
              type="button"
              class="mode-item"
              :class="{ on: operatorMode === 'auto' }"
              @click="switchOpMode('auto')"
            >
              <div class="mode-name">客服指派</div>
              <div class="mode-desc">由客服匹配最合适的打手</div>
            </button>

            <button
              type="button"
              class="mode-item"
              :class="{ on: operatorMode === 'manual' }"
              @click="switchOpMode('manual')"
            >
              <div class="mode-name">指定打手</div>
              <div class="mode-desc">自己挑选心仪的打手</div>
            </button>
          </div>

          <div v-if="operatorMode === 'manual'" class="op-picker-wrap">
            <!-- ⭐ 数量提示 -->
            <div class="op-count-hint" :class="{ full: isOpCountMaxed, locked: isOpCountLocked }">
              <span class="och-label">打手数量</span>
              <b class="och-count">{{ selectedOpIds.length }} / {{ opCountRange }}</b>
              <span v-if="isOpCountLocked" class="och-tag locked">本单必须 {{ minOpCount }} 人</span>
              <span v-else-if="minOpCount === maxOpCount" class="och-tag locked">
                本单必须 {{ minOpCount }} 人
              </span>
              <span v-else class="och-tag">
                请选择 {{ minOpCount }}–{{ maxOpCount }} 位
              </span>
            </div>

            <div v-if="loadingOps" class="om-empty">
              <span class="dot"></span> 正在加载打手…
            </div>

            <div v-else-if="availableOps.length === 0" class="om-empty">
              该游戏暂无可用打手，建议改为「客服指派」
            </div>

            <div v-else class="op-picker">
              <label
                v-for="op in availableOps"
                :key="op.userId"
                class="op-item"
                :class="{
                  on: isSelected(op.userId),
                  off: op.status === 'offline' || (!isSelected(op.userId) && isOpCountMaxed)
                }"
              >
                <input
                  type="checkbox"
                  :value="op.userId"
                  :checked="isSelected(op.userId)"
                  :disabled="op.status === 'offline' || (!isSelected(op.userId) && isOpCountMaxed)"
                  @change="toggleOp(op.userId)"
                />
                <div class="op-avatar">{{ (op.name || '?').charAt(0) }}</div>
                <div class="op-info">
                  <div class="op-name">
                    {{ op.name }}
                    <span class="op-level">{{ getLevelForGame(op) }}</span>
                    <OnlineStatusBadge :status="op.status || 'offline'" />
                  </div>
                  <div class="op-meta">
                    信誉 {{ op.reputation }} · 接单 {{ op.totalOrders }}
                    <span v-if="isLevelBased" class="op-surcharge">
                      · 增幅 {{ levelRateText(op) }}
                    </span>
                  </div>
                </div>
              </label>
            </div>
          </div>

          <div v-else class="auto-hint">
            ⓘ 提交后由客服为你匹配空闲打手
            <template v-if="isLevelBased">
              <br />
              <span class="auto-hint-sub">
                客服指派按最高等级 L5 预估，实际指派后按打手等级重算
              </span>
            </template>
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
            :disabled="!canSubmit || submitting"
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
import OnlineStatusBadge from './OnlineStatusBadge.vue'
import { fetchCustomerServices } from '@/api/customerService.ts'

// ⭐ 等级 → 价格增幅率（与后端 GetLevelSurchargeRate 保持一致）
const LEVEL_SURCHARGE = {
  L1: 0.10,
  L2: 0.20,
  L3: 0.30,
  L4: 0.40,
  L5: 0.50
}

export default {
  name: 'OrderModal',
  components: { OnlineStatusBadge },

  props: {
    visible: { type: Boolean, default: false },
    orderData: { type: Object, default: null }
  },

  emits: ['close', 'success'],

  data() {
    return {
      operators: [],
      customerServices: [],

      loadingOps: false,
      loadingCs: false,

      // ⭐ 多选：改为数组
      selectedOpIds: [],
      selectedCsId: '',

      orderSource: 'self',
      operatorMode: 'auto',

      remark: '',
      submitting: false,

      successOrderNo: '',
      userPaidMarked: false,
      markingPaid: false,
      customerServiceWechat: 'taichu_service',

      submittedCsLabel: '',
      submittedOpLabel: ''
    }
  },

  computed: {
    gameName()       { return this.orderData?.gameName || '' },
    orderTypeName()  { return this.orderData?.orderTypeName || '' },
    orderParams()    { return this.orderData?.params || {} },
    gameCode()       { return this.orderData?.gameCode || '' },

    // ⭐ 订单类型配置
    pricingMode()    { return this.orderData?.pricingMode || 'fixed' },
    isLevelBased()   { return this.pricingMode === 'level-based' },
    basePrice()      { return Number(this.orderData?.price) || 0 },
    minOpCount()     { return Number(this.orderData?.minOperatorCount) || 1 },
    maxOpCount()     { return Number(this.orderData?.maxOperatorCount) || 1 },

    // ⭐ 数量状态
    opCountRange() {
      return this.minOpCount === this.maxOpCount
        ? `${this.minOpCount}`
        : `${this.minOpCount}-${this.maxOpCount}`
    },
    isOpCountLocked() {
      return this.minOpCount === this.maxOpCount
    },
    isOpCountMaxed() {
      return this.selectedOpIds.length >= this.maxOpCount
    },

    availableOps() {
      if (!this.gameCode) return []
      return this.operators.filter(op =>
        op.games?.some(g => g.gameCode === this.gameCode)
      )
    },

    // ⭐ 已选打手对象列表
    selectedOps() {
      return this.availableOps.filter(op => this.selectedOpIds.includes(op.userId))
    },

    // ⭐ 实时总价
    price() {
      if (!this.isLevelBased) return this.basePrice.toFixed(2)

      // 客服指派模式：按最高等级 L5 预估（与后端 Create 保持一致）
      if (this.operatorMode === 'auto') {
        const estSurcharge = this.basePrice * LEVEL_SURCHARGE.L5 * this.maxOpCount
        return (this.basePrice + estSurcharge).toFixed(2)
      }

      // 手动模式：按实际选中打手等级累加
      let total = this.basePrice
      for (const op of this.selectedOps) {
        total += this.basePrice * this.getLevelRate(op)
      }
      return total.toFixed(2)
    },

    // ⭐ 提交校验
    canSubmit() {
      if (this.orderSource === 'cs') {
        if (this.customerServices.length === 0) return false
        if (!this.selectedCsId) return false
      }
      if (this.operatorMode === 'manual') {
        if (this.selectedOpIds.length < this.minOpCount) return false
        if (this.selectedOpIds.length > this.maxOpCount) return false
      }
      return true
    }
  },

  watch: {
    visible(v) { if (v) this.onOpen() }
  },

  methods: {
    async onOpen() {
      this.successOrderNo = ''
      this.userPaidMarked = false
      this.selectedOpIds = []
      this.selectedCsId = ''
      this.orderSource = 'self'
      this.operatorMode = 'auto'
      this.remark = ''
      this.submittedCsLabel = ''
      this.submittedOpLabel = ''

      await Promise.all([
        this.loadOperators(),
        this.loadCustomerServices()
      ])

      // ⭐ 从打手列表跳转过来时，自动切到「指定打手」并预选
      const prefId = this.orderData?.preferredOperatorId
      if (prefId) {
        this.operatorMode = 'manual'
        // 只加入，不越界
        this.selectedOpIds = [prefId].slice(0, this.maxOpCount)
      }
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

    async loadCustomerServices() {
      this.loadingCs = true
      try {
        const list = await fetchCustomerServices()
        this.customerServices = Array.isArray(list)
          ? list
          : (list?.items ?? list?.data ?? [])
      } catch (e) {
        console.error('加载客服失败', e)
        this.customerServices = []
      } finally {
        this.loadingCs = false
        if (this.customerServices.length === 0) {
          this.orderSource = 'self'
        }
      }
    },

    // ⭐ 打手选中工具
    isSelected(opId) {
      return this.selectedOpIds.includes(opId)
    },

    getLevelForGame(op) {
      const g = op.games?.find(x => x.gameCode === this.gameCode)
      return g?.level || 'L1'
    },

    getLevelRate(op) {
      return LEVEL_SURCHARGE[this.getLevelForGame(op)] ?? LEVEL_SURCHARGE.L1
    },

    levelRateText(op) {
      const rate = this.getLevelRate(op)
      return `+${Math.round(rate * 100)}%`
    },

    opSurcharge(op) {
      return this.basePrice * this.getLevelRate(op)
    },

    // ⭐ 多选切换
    toggleOp(opId) {
      if (this.operatorMode !== 'manual') return
      const idx = this.selectedOpIds.indexOf(opId)
      if (idx >= 0) {
        this.selectedOpIds.splice(idx, 1)
      } else {
        if (this.selectedOpIds.length >= this.maxOpCount) {
          alert(`本订单最多选择 ${this.maxOpCount} 位打手`)
          return
        }
        this.selectedOpIds.push(opId)
      }
    },

    // ⭐ 切换模式时清空已选打手
    switchOpMode(mode) {
      if (this.operatorMode === mode) return
      this.operatorMode = mode
      if (mode === 'auto') this.selectedOpIds = []
    },

    async submit() {
      if (!this.canSubmit || this.submitting) return

      // 前端兜底再校验一次
      if (this.operatorMode === 'manual') {
        if (this.selectedOpIds.length < this.minOpCount) {
          return alert(`本订单至少需要 ${this.minOpCount} 位打手`)
        }
        if (this.selectedOpIds.length > this.maxOpCount) {
          return alert(`本订单最多 ${this.maxOpCount} 位打手`)
        }
      }

      this.submitting = true

      try {
        const payload = {
          gameCode: this.gameCode,
          orderTypeCode: this.orderData.orderTypeCode,
          params: this.orderParams,
          remark: this.remark.trim() || null,
          source: this.orderSource
        }

        if (this.orderSource === 'cs') {
          payload.customerServiceId = this.selectedCsId
        }

        // ⭐ 关键：多打手传数组
        if (this.operatorMode === 'manual' && this.selectedOpIds.length > 0) {
          payload.operatorUserIds = [...this.selectedOpIds]
        }

        const res = await request.post('/club/orders', payload)
        const data = res?.data ?? res
        this.successOrderNo = data?.orderNo || ''

        // 记录分配信息
        if (this.orderSource === 'cs') {
          const cs = this.customerServices.find(c => c.id === this.selectedCsId)
          this.submittedCsLabel = cs ? `${cs.name}（${cs.wechat}）` : '已指定客服'
        } else {
          const assigned = data?.customerService
          this.submittedCsLabel = assigned
            ? `${assigned.name}（系统分配）`
            : '系统自动分配'
        }

        if (this.operatorMode === 'manual') {
          const names = this.selectedOps.map(o => o.name).join('、')
          this.submittedOpLabel = names
            ? `${names}（共 ${this.selectedOps.length} 人，指定）`
            : '已指定打手'
        } else {
          const assigned = data?.operator
          this.submittedOpLabel = assigned
            ? `${assigned.name}（客服指派）`
            : '客服指派'
        }

        if (data?.customerService?.wechat) {
          this.customerServiceWechat = data.customerService.wechat
        }

        this.$emit('success', data)
      } catch (e) {
        alert(e?.friendlyMessage || e?.response?.data?.message || e.message || '下单失败')
      } finally {
        this.submitting = false
      }
    },

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

    close() { this.$emit('close') }
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
  width: min(620px, 100%);
  max-height: 88vh; overflow-y: auto;
  border: 1px solid rgba(255, 255, 255, 0.15);
  background: #0e1720; color: #e8eef4;
  border-radius: 4px;
  box-shadow: 0 24px 80px rgba(0, 0, 0, 0.6);
}

/* ============ 成功态 ============ */
.om-success { padding: 48px 32px; text-align: center; }
.om-success-icon {
  width: 60px; height: 60px; margin: 0 auto 20px;
  display: grid; place-items: center;
  border-radius: 50%; border: 2px solid #78d7ce;
  color: #78d7ce; font-size: 32px; font-weight: 700;
}
.om-success h3 { margin: 0 0 10px; font-size: 20px; color: #f2f6fa; }
.om-success-no { margin: 0 0 6px; font-family: monospace; font-size: 14px; color: #ff6b1a; }
.om-success-amount { margin: 4px 0 22px; font-family: monospace; font-size: 13px; color: #8b9aab; }
.om-success-amount b { color: #ff6b1a; font-size: 18px; margin-left: 4px; }

.om-assign {
  margin: 0 auto 22px;
  max-width: 460px;
  padding: 14px 18px;
  border: 1px solid rgba(120, 215, 206, 0.25);
  background: rgba(120, 215, 206, 0.04);
  border-radius: 4px;
  display: flex; flex-direction: column; gap: 8px;
  text-align: left;
}
.oa-row {
  display: flex; justify-content: space-between; align-items: center;
  font-size: 12px;
}
.oa-row span { color: #8b9aab; font-family: monospace; letter-spacing: 1px; }
.oa-row b { color: #78d7ce; font-weight: 600; }

/* ============ 收款区 ============ */
.om-pay {
  margin: 0 auto 20px; padding: 20px; max-width: 520px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  background: rgba(255, 255, 255, 0.02);
  border-radius: 4px;
}
.om-pay-title {
  margin-bottom: 16px; font-family: monospace; font-size: 11px;
  letter-spacing: 1.5px; color: #8b9aab; text-align: center;
}
.om-pay-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 16px; }
.om-pay-item { display: flex; flex-direction: column; align-items: center; gap: 10px; }
.om-pay-label {
  display: flex; align-items: center; gap: 6px;
  font-family: monospace; font-size: 11px; letter-spacing: 1px; font-weight: 600;
}
.om-pay-label.wechat { color: #4ecb71; }
.om-pay-label.alipay { color: #4a9eff; }
.pay-icon { font-size: 13px; }
.om-pay-qr {
  display: grid; place-items: center;
  padding: 8px; background: #fff; border-radius: 4px;
  width: 100%;
}
.om-pay-qr img {
  width: 100%; max-width: 180px; height: 180px;
  object-fit: contain; display: block;
}
.om-pay-tip {
  font-size: 11px; line-height: 1.7; color: #ff8c4a;
  margin-bottom: 10px; text-align: center;
}
.om-pay-cs {
  font-family: monospace; font-size: 12px;
  color: #8b9aab; text-align: center;
}
.om-pay-cs b { color: #78d7ce; font-size: 14px; margin-left: 4px; }

.om-confirm-pay { margin: 0 auto 26px; max-width: 520px; text-align: center; }
.btn-confirm-pay {
  width: 100%; height: 52px;
  font-size: 15px; letter-spacing: 2px; font-weight: 700;
  color: #fff;
  background: linear-gradient(135deg, #ff6b1a, #ff8c4a);
  border: none; border-radius: 4px; cursor: pointer;
  transition: all 0.25s;
  box-shadow: 0 6px 20px rgba(255, 107, 26, 0.25);
}
.btn-confirm-pay:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 10px 28px rgba(255, 107, 26, 0.35);
}
.btn-confirm-pay:disabled { opacity: 0.6; cursor: not-allowed; }
.om-confirm-hint {
  margin: 10px 0 0; font-family: monospace;
  font-size: 11px; color: #8b9aab;
}

.om-paid-done {
  margin: 0 auto 26px; padding: 20px; max-width: 520px;
  border: 1px solid rgba(120, 215, 206, 0.3);
  background: rgba(120, 215, 206, 0.05);
  border-radius: 4px; text-align: center;
}
.om-paid-done-icon {
  width: 36px; height: 36px; margin: 0 auto 12px;
  display: grid; place-items: center;
  border-radius: 50%; border: 2px solid #78d7ce;
  color: #78d7ce; font-size: 18px; font-weight: 700;
}
.om-paid-done-title { margin: 0 0 8px; font-size: 15px; font-weight: 700; color: #78d7ce; }
.om-paid-done-hint { margin: 0; font-size: 12px; line-height: 1.7; color: #8b9aab; }

.om-success-actions { display: flex; gap: 12px; justify-content: center; }

/* ============ 表单头部 ============ */
.om-head {
  display: flex; align-items: center; justify-content: space-between;
  padding: 20px 26px; border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}
.om-head h3 {
  margin: 0; font-size: 18px; font-weight: 700;
  letter-spacing: 1px; color: #f2f6fa;
}
.close-icon {
  background: none; border: none; font-size: 22px;
  color: #6b7a8a; cursor: pointer; line-height: 1;
}
.close-icon:hover { color: #e8eef4; }

.om-block {
  padding: 20px 26px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
}
.om-block:last-of-type { border-bottom: none; }
.om-title {
  margin: 0 0 14px; font-family: monospace;
  font-size: 10px; letter-spacing: 2px; color: #ff6b1a;
}

/* ============ 订单信息 ============ */
.om-info { display: flex; flex-direction: column; gap: 8px; }
.om-info-row {
  display: flex; justify-content: space-between;
  font-size: 13px; padding: 6px 0;
}
.om-info-row span { color: #8b9aab; }
.om-info-row b { color: #f2f6fa; }
/* ⭐ 价格明细行 */
.om-info-row.sub {
  font-size: 12px;
  padding: 2px 0 2px 8px;
  opacity: 0.85;
}
.om-info-row.sub span { color: #6b7a8a; font-family: monospace; }
.om-info-row.sub b { color: #78d7ce; font-family: monospace; }
.om-info-row.total {
  margin-top: 6px; padding-top: 12px;
  border-top: 1px dashed rgba(255, 255, 255, 0.1);
}
.om-price { font-family: monospace; font-size: 20px !important; color: #ff6b1a !important; }

/* ============ 客服来源 ============ */
.source-grid {
  display: grid; grid-template-columns: 1fr 1fr; gap: 10px;
  margin-bottom: 14px;
}
.source-item {
  display: flex; align-items: center; gap: 12px;
  padding: 14px 16px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  background: rgba(255, 255, 255, 0.02);
  border-radius: 3px;
  cursor: pointer; transition: all 0.2s;
}
.source-item:hover { border-color: rgba(255, 255, 255, 0.2); }
.source-item.on {
  border-color: rgba(255, 107, 26, 0.5);
  background: rgba(255, 107, 26, 0.06);
}
.source-item input { display: none; }
.source-icon { font-size: 22px; line-height: 1; }
.source-info { min-width: 0; }
.source-name {
  font-size: 13px; font-weight: 700; color: #f2f6fa;
  margin-bottom: 3px;
}
.source-desc { font-size: 11px; color: #8b9aab; line-height: 1.5; }

/* 客服选择器 */
.cs-picker-wrap { margin-top: 4px; }
.cs-picker {
  display: flex; flex-direction: column; gap: 8px;
  max-height: 240px; overflow-y: auto;
}
.cs-item {
  display: flex; align-items: center; gap: 12px;
  padding: 12px 14px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  background: rgba(255, 255, 255, 0.02);
  border-radius: 3px;
  cursor: pointer; transition: 0.2s;
}
.cs-item:hover:not(.off) { border-color: rgba(255, 255, 255, 0.2); }
.cs-item.on {
  border-color: rgba(255, 107, 26, 0.5);
  background: rgba(255, 107, 26, 0.06);
}
.cs-item.off { opacity: 0.4; cursor: not-allowed; }
.cs-item input { display: none; }

.cs-avatar {
  width: 38px; height: 38px;
  display: grid; place-items: center;
  border: 1px solid rgba(255, 255, 255, 0.15);
  background: rgba(255, 255, 255, 0.03);
  color: #ff6b1a; font-weight: 700;
  border-radius: 50%;
  flex-shrink: 0;
}
.cs-info { flex: 1; min-width: 0; }
.cs-name {
  display: flex; align-items: center; gap: 8px;
  font-size: 14px; font-weight: 700; color: #f2f6fa;
  margin-bottom: 3px;
}
.cs-meta { font-size: 11px; color: #8b9aab; font-family: monospace; }

/* ============ 打手模式切换 ============ */
.mode-grid {
  display: grid; grid-template-columns: 1fr 1fr;
  gap: 10px; margin-bottom: 14px;
}
.mode-item {
  padding: 14px 16px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  background: rgba(255, 255, 255, 0.02);
  border-radius: 3px;
  cursor: pointer; text-align: left;
  transition: 0.2s;
  font-family: inherit; color: inherit;
}
.mode-item:hover { border-color: rgba(255, 255, 255, 0.2); }
.mode-item.on {
  border-color: rgba(255, 107, 26, 0.5);
  background: rgba(255, 107, 26, 0.06);
}
.mode-name {
  font-size: 13px; font-weight: 700;
  color: #f2f6fa; margin-bottom: 3px;
}
.mode-desc { font-size: 11px; color: #8b9aab; line-height: 1.5; }

/* ⭐ 数量提示条 */
.op-count-hint {
  display: flex; align-items: center; gap: 10px;
  padding: 10px 14px;
  margin-bottom: 12px;
  border: 1px solid rgba(255, 107, 26, 0.3);
  background: rgba(255, 107, 26, 0.05);
  border-radius: 3px;
  font-size: 12px;
}
.op-count-hint.locked {
  border-color: rgba(120, 215, 206, 0.35);
  background: rgba(120, 215, 206, 0.05);
}
.op-count-hint.full {
  border-color: rgba(80, 200, 120, 0.5);
  background: rgba(80, 200, 120, 0.08);
}
.och-label {
  font-family: monospace; font-size: 10px;
  letter-spacing: 1px; color: #8b9aab;
}
.och-count {
  font-family: monospace; font-size: 14px;
  font-weight: 700; color: #ff6b1a;
}
.op-count-hint.locked .och-count { color: #78d7ce; }
.op-count-hint.full .och-count { color: #4ecb71; }
.och-tag {
  margin-left: auto;
  font-family: monospace; font-size: 10px;
  color: #8b9aab;
}
.och-tag.locked { color: #78d7ce; }

.op-picker-wrap { margin-top: 4px; }
.op-picker {
  display: flex; flex-direction: column; gap: 8px;
  max-height: 260px; overflow-y: auto;
}
.op-item {
  display: flex; align-items: center; gap: 12px;
  padding: 12px 14px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  background: rgba(255, 255, 255, 0.02);
  border-radius: 3px;
  cursor: pointer; transition: 0.2s;
}
.op-item:hover:not(.off) { border-color: rgba(255, 255, 255, 0.2); }
.op-item.on {
  border-color: rgba(255, 107, 26, 0.5);
  background: rgba(255, 107, 26, 0.06);
}
.op-item.off { opacity: 0.35; cursor: not-allowed; }
.op-item input { display: none; }
.op-avatar {
  width: 38px; height: 38px;
  display: grid; place-items: center;
  border: 1px solid rgba(255, 255, 255, 0.15);
  background: rgba(255, 255, 255, 0.03);
  color: #ff6b1a; font-weight: 700; border-radius: 2px;
  flex-shrink: 0;
}
.op-info { flex: 1; min-width: 0; }
.op-name {
  font-size: 14px; font-weight: 700; color: #f2f6fa;
  margin-bottom: 3px;
  display: flex; align-items: center; gap: 8px; flex-wrap: wrap;
}
.op-level {
  padding: 1px 6px;
  font-family: monospace; font-size: 10px;
  color: #ff6b1a;
  border: 1px solid rgba(255, 107, 26, 0.4);
  border-radius: 2px;
}
.op-meta { font-size: 11px; color: #8b9aab; }
.op-surcharge { color: #78d7ce; font-family: monospace; }

/* ============ 自动分配提示 ============ */
.auto-hint {
  padding: 12px 14px;
  border: 1px dashed rgba(120, 215, 206, 0.3);
  background: rgba(120, 215, 206, 0.04);
  border-radius: 3px;
  font-size: 12px;
  line-height: 1.6;
  color: #78d7ce;
}
.auto-hint-sub {
  font-family: monospace; font-size: 10px;
  color: #8b9aab;
}

/* ============ 空态 ============ */
.om-empty {
  padding: 24px; text-align: center;
  font-family: monospace; font-size: 12px; color: #6b7a8a;
  border: 1px dashed rgba(255, 255, 255, 0.1);
  border-radius: 3px;
}
.dot {
  display: inline-block; width: 6px; height: 6px;
  margin-right: 6px; border-radius: 50%; background: #ff6b1a;
  animation: pulse 1.4s infinite;
}
@keyframes pulse { 50% { opacity: 0.3; } }

/* ============ 备注 ============ */
.om-textarea {
  width: 100%; padding: 10px 12px;
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.1);
  color: #e8eef4; border-radius: 3px;
  font-family: inherit; font-size: 13px;
  outline: none; resize: vertical;
}
.om-textarea:focus { border-color: rgba(255, 107, 26, 0.5); }

/* ============ 底部按钮 ============ */
.om-foot {
  display: flex; gap: 12px;
  padding: 20px 26px;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
}
.btn-ghost, .btn-solid {
  flex: 1; height: 42px;
  font-size: 13px; letter-spacing: 1px;
  cursor: pointer; border-radius: 3px;
  transition: all 0.2s;
  display: flex; align-items: center; justify-content: center;
  text-decoration: none;
}
.btn-ghost {
  color: #8b9aab; background: transparent;
  border: 1px solid rgba(255, 255, 255, 0.15);
}
.btn-ghost:hover { color: #e8eef4; border-color: rgba(255, 255, 255, 0.3); }
.btn-solid {
  color: #fff; background: #ff6b1a;
  border: 1px solid #ff6b1a;
}
.btn-solid:hover:not(:disabled) { background: #ff7e32; }
.btn-solid:disabled { opacity: 0.5; cursor: not-allowed; }

/* ============ 移动端 ============ */
@media (max-width: 520px) {
  .om-pay-grid { grid-template-columns: 1fr; }
  .om-pay-qr img { max-width: 200px; height: 200px; }
  .source-grid, .mode-grid { grid-template-columns: 1fr; }
}
</style>