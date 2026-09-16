<template>
  <div class="page-shell">
    <Navbar />
    <main>
      <section class="page-hero">
        <div class="section-container">
          <SectionHeading index="01" title="WALLET" subtitle="我的钱包 / OPERATOR WALLET" />
        </div>
      </section>

      <section class="section-block alt">
        <div class="section-container">

          <div v-if="!isLoggedIn" class="op-state">请先登录后查看钱包</div>
          <div v-else-if="loading" class="op-state">
            <span class="dot"></span> 正在读取钱包…
          </div>
          <div v-else-if="loadError" class="op-state">{{ loadError }}</div>

          <template v-else>
            <div class="wallet-card">
              <div class="wc-balance">
                <span class="wc-label">可用余额</span>
                <div class="wc-amount">
                  <span class="wc-symbol">¥</span>
                  <b>{{ balance.toFixed(2) }}</b>
                </div>
              </div>

              <div class="wc-side">
                <div class="wc-side-row">
                  <span>冻结中</span>
                  <b>¥{{ frozenBalance.toFixed(2) }}</b>
                </div>
                <div class="wc-side-row">
                  <span>累计收益</span>
                  <b>¥{{ totalIncome.toFixed(2) }}</b>
                </div>
                <div class="wc-side-row">
                  <span>累计提现</span>
                  <b>¥{{ totalWithdrawn.toFixed(2) }}</b>
                </div>
              </div>

              <button
                class="wc-withdraw"
                :disabled="!canSubmitWithdraw"
                @click="showWithdraw = true"
              >
                {{ withdrawButtonText }}
              </button>
            </div>

            <!-- 提现规则提示 -->
            <div class="withdraw-rule">
              <span class="wr-icon">ⓘ</span>
              <span class="wr-text">
                每周可申请提现一次，最低 ¥1 起提。
                <template v-if="!canWithdraw && nextWeekStart">
                  下次可提现时间：{{ fmtDate(nextWeekStart) }} 起
                </template>
              </span>
            </div>

            <h3 class="ss-title">流水明细</h3>

            <div v-if="items.length === 0" class="op-state">暂无流水</div>

            <div v-else class="tx-list">
              <div v-for="t in items" :key="t.id" class="tx-row">
                <div class="tx-left">
                  <div class="tx-icon" :class="t.type">{{ typeIcon(t.type) }}</div>
                  <div class="tx-info">
                    <div class="tx-title">{{ typeLabel(t) }}</div>
                    <div class="tx-sub">
                      <span v-if="t.orderNo" class="mono">{{ t.orderNo }}</span>
                      <span v-else-if="t.remark">{{ t.remark }}</span>
                    </div>
                    <div class="tx-time">{{ fmtTime(t.createdAt) }}</div>
                  </div>
                </div>

                <div class="tx-right">
                  <div class="tx-amount" :class="amountClass(t)">
                    {{ t.amount > 0 ? '+' : '' }}{{ t.amount.toFixed(2) }}
                  </div>
                  <div v-if="t.status !== 'completed'" class="tx-status" :class="t.status">
                    {{ statusLabel(t.status) }}
                  </div>
                  <div v-else-if="t.type === 'income' && !t.isUnfrozen" class="tx-status frozen">
                    冻结中
                  </div>
                </div>
              </div>
            </div>

            <div v-if="total > pageSize" class="tx-pager">
              <button :disabled="page <= 1" @click="changePage(page - 1)">上一页</button>
              <span class="mono">{{ page }} / {{ Math.ceil(total / pageSize) }}</span>
              <button :disabled="page >= Math.ceil(total / pageSize)" @click="changePage(page + 1)">下一页</button>
            </div>
          </template>

        </div>
      </section>
    </main>
    <Footer />

    <Teleport to="body">
      <div v-if="showWithdraw" class="modal-mask" @click.self="showWithdraw = false">
        <div class="modal">
          <header class="modal-head">
            <h3>申请提现</h3>
            <button class="close-icon" @click="showWithdraw = false">×</button>
          </header>

          <div class="modal-body">
            <div class="modal-tip">
              每周可申请一次 · 最低 ¥1 · 可用 ¥{{ balance.toFixed(2) }}
            </div>

            <div class="form-row">
              <label>提现金额（元）</label>
              <input
                v-model.number="withdrawForm.amount"
                type="number" min="1" step="0.01" :max="balance"
                placeholder="最低 ¥1"
              />
            </div>
            <div class="form-row">
              <label>收款方式</label>
              <select v-model="withdrawForm.contactType">
                <option value="" disabled>请选择</option>
                <option value="微信">微信</option>
                <option value="支付宝">支付宝</option>
                <option value="银行卡">银行卡</option>
              </select>
            </div>
            <div class="form-row">
              <label>收款账号</label>
              <input v-model="withdrawForm.contactValue" placeholder="填写对应的收款账号" />
            </div>
            <div class="form-row">
              <label>备注（可选）</label>
              <input v-model="withdrawForm.remark" placeholder="选填" />
            </div>
          </div>

          <footer class="modal-foot">
            <button class="btn-ghost" @click="showWithdraw = false">取消</button>
            <button class="btn-solid" :disabled="submitting" @click="submitWithdraw">
              {{ submitting ? '提交中…' : '提交申请' }}
            </button>
          </footer>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script>
import Navbar         from './Navbar.vue'
import Footer         from './Footer.vue'
import SectionHeading from './SectionHeading.vue'
import request        from '@/utils/request'

export default {
  name: 'OperatorWallet',
  components: { Navbar, Footer, SectionHeading },

  data() {
    return {
      loading: true,
      loadError: '',
      balance: 0,
      frozenBalance: 0,
      totalIncome: 0,
      totalWithdrawn: 0,
      items: [],
      total: 0,
      page: 1,
      pageSize: 30,

      // ⭐ 提现规则状态
      canWithdraw: true,
      minWithdrawAmount: 1,
      weekStart: null,
      nextWeekStart: null,
      thisWeekWithdrawAt: null,

      showWithdraw: false,
      submitting: false,
      withdrawForm: { amount: null, contactType: '', contactValue: '', remark: '' }
    }
  },

  computed: {
    isLoggedIn() { return !!localStorage.getItem('token') },

    canSubmitWithdraw() {
      return this.canWithdraw && this.balance >= this.minWithdrawAmount
    },

    withdrawButtonText() {
      if (!this.canWithdraw) return '本周已提现，下周再来'
      if (this.balance < this.minWithdrawAmount) return `余额满 ¥${this.minWithdrawAmount} 可提现`
      return '申请提现'
    }
  },

  async mounted() {
    if (!this.isLoggedIn) { this.loading = false; return }
    await this.loadWallet()
    this.loading = false
  },

  methods: {
    async loadWallet() {
      this.loadError = ''
      try {
        const res = await request.get('/club/wallet', { params: { page: this.page, pageSize: this.pageSize } })
        const data = res?.data ?? res
        this.balance              = Number(data?.balance ?? 0)
        this.frozenBalance        = Number(data?.frozenBalance ?? 0)
        this.totalIncome          = Number(data?.totalIncome ?? 0)
        this.totalWithdrawn       = Number(data?.totalWithdrawn ?? 0)
        this.items                = data?.items ?? []
        this.total                = data?.total ?? 0

        this.canWithdraw          = data?.canWithdraw ?? true
        this.minWithdrawAmount    = Number(data?.minWithdrawAmount ?? 1)
        this.weekStart            = data?.weekStart ?? null
        this.nextWeekStart        = data?.nextWeekStart ?? null
        this.thisWeekWithdrawAt   = data?.thisWeekWithdrawAt ?? null
      } catch (e) {
        console.error('加载钱包失败', e)
        this.loadError = e?.response?.data?.message || e.message || '加载失败'
      }
    },

    changePage(p) { this.page = p; this.loadWallet() },

    async submitWithdraw() {
      if (this.submitting) return
      const amount = Number(this.withdrawForm.amount)
      if (!amount || amount < this.minWithdrawAmount)
        return alert(`最低提现 ¥${this.minWithdrawAmount}`)
      if (amount > this.balance)
        return alert('金额不能超过可用余额')
      if (!this.withdrawForm.contactType)
        return alert('请选择收款方式')
      if (!this.withdrawForm.contactValue.trim())
        return alert('请填写收款账号')

      this.submitting = true
      try {
        await request.post('/club/wallet/withdraw', {
          amount,
          contactType: this.withdrawForm.contactType,
          contactValue: this.withdrawForm.contactValue.trim(),
          remark: this.withdrawForm.remark.trim() || null
        })
        this.showWithdraw = false
        this.withdrawForm = { amount: null, contactType: '', contactValue: '', remark: '' }
        await this.loadWallet()
        alert('提现申请已提交，客服将在 1-2 个工作日内处理')
      } catch (e) {
        alert(e?.response?.data?.message || e.message || '提交失败')
      } finally {
        this.submitting = false
      }
    },

    typeIcon(type) { return { income: '↑', withdraw: '↓', adjust: '±' }[type] || '·' },
    typeLabel(t) {
      if (t.type === 'income')   return '订单收益'
      if (t.type === 'withdraw') return '提现'
      if (t.type === 'adjust')   return '平台调整'
      return t.type
    },
    amountClass(t) {
      if (t.amount > 0) return 'in'
      if (t.amount < 0) return 'out'
      return ''
    },
    statusLabel(s) { return { pending: '处理中', completed: '已完成', rejected: '已拒绝' }[s] || s },
    fmtTime(iso) {
      if (!iso) return '—'
      const d = new Date(iso)
      const pad = n => String(n).padStart(2, '0')
      return `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
    },
    fmtDate(iso) {
      if (!iso) return '—'
      const d = new Date(iso)
      const pad = n => String(n).padStart(2, '0')
      return `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}`
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

.wallet-card {
  display: grid;
  grid-template-columns: 1.2fr 1fr auto;
  align-items: center;
  gap: 30px;
  padding: 30px 34px;
  margin-bottom: 20px;
  border: 1px solid var(--line);
  background: linear-gradient(135deg, rgba(16, 25, 35, 0.8), rgba(10, 15, 21, 0.6));
}
.wc-label {
  display: block; font-family: monospace; font-size: 10px;
  letter-spacing: 2px; color: var(--text-muted); margin-bottom: 10px;
}
.wc-amount { display: flex; align-items: baseline; gap: 6px; }
.wc-symbol { font-family: monospace; font-size: 22px; color: var(--orange); }
.wc-amount b {
  font-family: monospace; font-size: 46px; font-weight: 700;
  color: var(--orange); line-height: 1;
}
.wc-side {
  display: flex; flex-direction: column; gap: 8px;
  padding-left: 30px; border-left: 1px solid var(--line);
}
.wc-side-row { display: flex; justify-content: space-between; font-size: 12px; }
.wc-side-row span { color: var(--text-muted); }
.wc-side-row b { font-family: monospace; color: var(--text-soft); }
.wc-withdraw {
  padding: 12px 24px; font-size: 13px; letter-spacing: 1px;
  color: #fff; background: var(--orange);
  border: 1px solid var(--orange); cursor: pointer; transition: 0.2s;
  white-space: nowrap;
}
.wc-withdraw:hover:not(:disabled) { background: var(--orange-bright); }
.wc-withdraw:disabled { opacity: 0.4; cursor: not-allowed; }

/* ⭐ 提现规则提示 */
.withdraw-rule {
  display: flex; align-items: flex-start; gap: 10px;
  padding: 12px 16px;
  margin-bottom: 40px;
  border: 1px solid var(--line);
  background: rgba(16, 25, 35, 0.35);
  font-size: 12px;
  color: var(--text-soft);
}
.wr-icon { color: var(--orange); flex-shrink: 0; }
.wr-text { line-height: 1.7; }

.ss-title {
  margin: 0 0 16px;
  font-family: monospace; font-size: 11px; letter-spacing: 2px;
  color: var(--orange); font-weight: 600;
}

.tx-list { border: 1px solid var(--line); background: rgba(16, 25, 35, 0.4); }
.tx-row {
  display: flex; align-items: center; justify-content: space-between;
  gap: 16px; padding: 16px 22px;
  border-bottom: 1px solid var(--line);
}
.tx-row:last-child { border-bottom: none; }
.tx-left { display: flex; align-items: center; gap: 14px; min-width: 0; }
.tx-icon {
  flex-shrink: 0; width: 32px; height: 32px;
  display: grid; place-items: center;
  border: 1px solid var(--line-bright); border-radius: 50%;
  font-size: 14px; color: var(--text-soft);
}
.tx-icon.income { color: var(--cyan); border-color: rgba(120, 215, 206, 0.4); }
.tx-icon.withdraw { color: var(--orange); border-color: rgba(255, 107, 26, 0.4); }
.tx-info { min-width: 0; }
.tx-title { font-size: 13px; font-weight: 600; color: var(--text); margin-bottom: 4px; }
.tx-sub {
  font-size: 11px; color: var(--text-muted); margin-bottom: 4px;
  white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
}
.tx-time { font-family: monospace; font-size: 10px; color: var(--text-muted); }
.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }
.tx-right { flex-shrink: 0; text-align: right; display: flex; flex-direction: column; gap: 4px; }
.tx-amount { font-family: monospace; font-size: 16px; font-weight: 700; }
.tx-amount.in  { color: var(--cyan); }
.tx-amount.out { color: var(--orange); }
.tx-status {
  font-family: monospace; font-size: 10px; letter-spacing: 1px;
  color: var(--text-muted);
}
.tx-status.pending { color: #ff8c4a; }
.tx-status.rejected { color: #ff5050; }
.tx-status.frozen { color: #a5a5ff; }

.tx-pager {
  display: flex; align-items: center; justify-content: center;
  gap: 16px; margin-top: 20px;
  font-family: monospace; font-size: 12px; color: var(--text-muted);
}
.tx-pager button {
  padding: 6px 14px; font-size: 11px;
  color: var(--text-soft); background: transparent;
  border: 1px solid var(--line-bright); cursor: pointer;
}
.tx-pager button:hover:not(:disabled) { color: var(--text); border-color: var(--text-soft); }
.tx-pager button:disabled { opacity: 0.4; cursor: not-allowed; }

.modal-mask {
  position: fixed; inset: 0; z-index: 9999;
  background: rgba(0, 0, 0, 0.78);
  backdrop-filter: blur(8px);
  display: grid; place-items: center; padding: 24px;
}
.modal {
  width: min(460px, 100%);
  border: 1px solid var(--line-bright);
  background: #0e1720; color: #e8eef4; border-radius: 4px;
}
.modal-head {
  display: flex; justify-content: space-between; align-items: center;
  padding: 18px 24px; border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}
.modal-head h3 { margin: 0; font-size: 16px; letter-spacing: 1px; }
.close-icon {
  background: none; border: none; font-size: 22px;
  color: var(--text-muted); cursor: pointer;
}
.modal-body {
  padding: 24px; display: flex; flex-direction: column; gap: 16px;
}
/* ⭐ 弹窗顶部提示 */
.modal-tip {
  padding: 10px 12px;
  font-family: monospace; font-size: 11px;
  color: #ff8c4a;
  background: rgba(255, 140, 74, 0.08);
  border-radius: 3px;
  letter-spacing: 0.5px;
}
.form-row { display: flex; flex-direction: column; gap: 8px; }
.form-row label {
  font-family: monospace; font-size: 10px;
  letter-spacing: 1px; color: var(--text-muted);
}
.form-row input, .form-row select {
  height: 40px; padding: 0 12px;
  font-size: 13px; font-family: inherit;
  color: var(--text); background: rgba(0, 0, 0, 0.35);
  border: 1px solid var(--line-bright); outline: none;
}
.form-row input:focus, .form-row select:focus { border-color: rgba(255, 107, 26, 0.5); }
.modal-foot {
  display: flex; gap: 12px; padding: 18px 24px;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
}
.btn-ghost, .btn-solid {
  flex: 1; height: 42px; font-size: 12px; letter-spacing: 1px;
  cursor: pointer; border-radius: 3px;
}
.btn-ghost { color: var(--text-soft); background: transparent; border: 1px solid var(--line-bright); }
.btn-solid { color: #fff; background: var(--orange); border: 1px solid var(--orange); }
.btn-solid:hover:not(:disabled) { background: var(--orange-bright); }
.btn-solid:disabled { opacity: 0.5; cursor: not-allowed; }

@media (max-width: 700px) {
  .wallet-card { grid-template-columns: 1fr; gap: 20px; }
  .wc-side { padding-left: 0; padding-top: 16px; border-left: none; border-top: 1px solid var(--line); }
  .wc-amount b { font-size: 36px; }
}
@media (max-width: 520px) {
  .page-hero { padding: 80px 0 40px; }
  .tx-row { padding: 14px 16px; }
}
</style>