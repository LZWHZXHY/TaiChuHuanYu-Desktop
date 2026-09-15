<template>
  <div class="dividend">
    <table class="dv-table">
      <thead>
        <tr>
          <th>等级</th>
          <th>称号</th>
          <th>认证标准</th> 
          <th>基础分成</th>
          <th>好评奖励</th>
          <th class="ta-r">结算周期</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="t in tiers" :key="t.tier">
          <td><span class="dv-tier">{{ t.tier }}</span></td>
          <td class="dv-name">{{ t.name }}</td>
          <td class="dv-muted">{{ t.threshold }}</td>
          <td><b class="dv-rate">{{ (t.rate * 100).toFixed(0) }}%</b></td>
          <td class="dv-praise">{{ t.praise }}</td>
          <td class="ta-r dv-muted">{{ t.cycle }}</td>
        </tr>
      </tbody>
    </table>

    <!-- 试算器 -->
    <div class="calc">
      <div class="calc-head">
        <span class="calc-title">收益试算器</span>
        <span class="calc-code">EARNINGS CALCULATOR</span>
      </div>

      <div class="calc-body">
        <div class="calc-field">
          <label>订单金额（元）</label>
          <input v-model.number="amount" type="number" min="0" placeholder="输入订单金额" />
        </div>

        <div class="calc-field">
          <label>打手等级</label>
          <select v-model="tier">
            <option v-for="t in tiers" :key="t.tier" :value="t.tier">
              {{ t.tier }} · {{ t.name }}
            </option>
          </select>
        </div>

        <div class="calc-result">
          <div class="cr-row">
            <span>打手实得</span>
            <b class="cr-main">¥{{ payout.toFixed(2) }}</b>
          </div>
          <div class="cr-row">
            <span>俱乐部留存</span>
            <b>¥{{ (amount - payout).toFixed(2) }}</b>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
export default {
  name: 'DividendRules',
  props: {
    tiers: { type: Array, default: () => [] }
  },
  data() {
    return { amount: 200, tier: '' }
  },
  computed: {
    currentTier() {
      return this.tiers.find(t => t.tier === this.tier) || this.tiers[0] || { rate: 0 }
    },
    payout() {
      return (Number(this.amount) || 0) * this.currentTier.rate
    }
  },
  watch: {
    tiers: {
      immediate: true,
      handler(val) {
        if (val.length && !this.tier) this.tier = val[0].tier
      }
    }
  }
}
</script>

<style scoped>
.dv-table { width: 100%; border-collapse: collapse; margin-bottom: 44px; }
.dv-table th {
  padding: 12px 14px;
  font-family: monospace;
  font-size: 9px;
  font-weight: 400;
  letter-spacing: 1px;
  text-align: left;
  color: var(--text-muted);
  border-bottom: 1px solid var(--line);
}
.dv-table td {
  padding: 17px 14px;
  font-size: 13px;
  border-bottom: 1px solid var(--line);
  transition: background 0.2s ease;
}
.dv-table tbody tr:hover td { background: rgba(255, 107, 26, 0.035); }
.ta-r { text-align: right; }

.dv-tier {
  display: inline-block;
  padding: 3px 9px;
  font-family: monospace;
  font-size: 10px;
  font-weight: 700;
  color: var(--orange);
  border: 1px solid rgba(255, 107, 26, 0.35);
  background: rgba(255, 107, 26, 0.06);
}
.dv-name { font-weight: 600; color: var(--text); }
.dv-muted { color: var(--text-muted); font-size: 12px; }
.dv-rate { font-family: monospace; font-size: 15px; color: var(--cyan); }
.dv-praise { font-family: monospace; font-size: 12px; color: var(--text-soft); }

/* 试算器 */
.calc {
  max-width: 620px;
  border: 1px solid var(--line);
  background: rgba(16, 25, 35, 0.5);
}
.calc-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 14px;
  padding: 16px 22px;
  border-bottom: 1px solid var(--line);
}
.calc-title { font-size: 13px; font-weight: 600; letter-spacing: 1px; }
.calc-code {
  font-family: monospace;
  font-size: 8px;
  letter-spacing: 2px;
  color: var(--text-muted);
}
.calc-body {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 18px;
  padding: 22px;
}
.calc-field { display: flex; flex-direction: column; gap: 8px; }
.calc-field label {
  font-family: monospace;
  font-size: 9px;
  letter-spacing: 1px;
  color: var(--text-muted);
}
.calc-field input,
.calc-field select {
  height: 40px;
  padding: 0 12px;
  font-size: 13px;
  color: var(--text);
  background: rgba(0, 0, 0, 0.35);
  border: 1px solid var(--line-bright);
  outline: none;
  transition: border-color 0.2s ease;
}
.calc-field input:focus,
.calc-field select:focus { border-color: rgba(255, 107, 26, 0.5); }

.calc-result {
  grid-column: 1 / -1;
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding-top: 18px;
  border-top: 1px solid var(--line);
}
.cr-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 12px;
}
.cr-row span { color: var(--text-muted); }
.cr-row b { font-family: monospace; font-size: 13px; color: var(--text-soft); }
.cr-main { font-size: 22px !important; color: var(--orange) !important; }

@media (max-width: 640px) {
  .dv-table thead { display: none; }
  .dv-table tr {
    display: flex;
    flex-wrap: wrap;
    gap: 8px 16px;
    padding: 16px 0;
    border-bottom: 1px solid var(--line);
  }
  .dv-table td { padding: 0; border: none; }
  .dv-table td:nth-child(2) { flex: 1 0 100%; }
  .calc-body { grid-template-columns: 1fr; }
}
</style>