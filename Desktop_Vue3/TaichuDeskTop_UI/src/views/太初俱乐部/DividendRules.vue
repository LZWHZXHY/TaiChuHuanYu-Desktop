<template>
  <div class="dividend">

    <!-- ==================================================
         区块 1：抽成档位
    ================================================== -->
    <section class="dvd-block">
      <div class="dvd-head">
        <div class="dvd-head-left">
          <span class="dvd-idx">01</span>
          <span class="dvd-title">抽成档位</span>
        </div>
        <span class="dvd-code">COMMISSION TIERS</span>
      </div>

      <p class="dvd-desc">
        所有订单（护航 / 陪玩）通用。按累计完成订单数自动升级档位，单量越多，平台抽成越低。
      </p>

      <table class="dv-table">
        <thead>
          <tr>
            <th>累计接单</th>
            <th>平台抽成</th>
            <th>打手池实得</th>
            <th class="ta-r">说明</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="t in commissionTiers" :key="t.id">
            <td><span class="dv-tier">{{ t.range }}</span></td>
            <td><b class="dv-rate out">{{ t.commission }}%</b></td>
            <td><b class="dv-rate">{{ t.payout }}%</b></td>
            <td class="ta-r dv-muted">{{ t.note }}</td>
          </tr>
        </tbody>
      </table>
    </section>


    <!-- ==================================================
         区块 2：陪玩等级增幅
    ================================================== -->
    <section class="dvd-block">
      <div class="dvd-head">
        <div class="dvd-head-left">
          <span class="dvd-idx">02</span>
          <span class="dvd-title">陪玩等级增幅</span>
        </div>
        <span class="dvd-code">LEVEL SURCHARGE</span>
      </div>

      <p class="dvd-desc">
        仅陪玩单生效，等级越高订单价格越高，打手收益也越高。
        <b class="hl">护航单为固定价，不受等级影响。</b>
      </p>

      <table class="dv-table">
        <thead>
          <tr>
            <th>等级</th>
            <th>称号</th>
            <th>认证标准</th>
            <th class="ta-r">陪玩单增幅</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="t in tiers" :key="t.tier">
            <td><span class="dv-tier">{{ t.tier }}</span></td>
            <td class="dv-name">{{ t.name }}</td>
            <td class="dv-muted">{{ t.threshold }}</td>
            <td class="ta-r">
              <b class="dv-rate up">+{{ (t.surcharge * 100).toFixed(0) }}%</b>
            </td>
          </tr>
        </tbody>
      </table>
    </section>


    <!-- ==================================================
         区块 3：押金政策
    ================================================== -->
    <section class="dvd-block">
      <div class="dvd-head">
        <div class="dvd-head-left">
          <span class="dvd-idx">03</span>
          <span class="dvd-title">押金政策</span>
        </div>
        <span class="dvd-code">DEPOSIT POLICY</span>
      </div>

      <p class="dvd-desc">
        押金非强制。两种入驻方式任选其一，都能正常接单。
      </p>

      <div class="deposit-grid">
        <div class="dp-card">
          <div class="dp-icon">💳</div>
          <div class="dp-title">缴纳 ¥200 押金</div>
          <div class="dp-desc">
            一次性缴纳，正常离队可退还。<br />
            不占用订单收益，抽成照常结算。
          </div>
        </div>

        <div class="dp-card highlight">
          <div class="dp-icon">🎯</div>
          <div class="dp-title">免押金 · 抽成抵扣</div>
          <div class="dp-desc">
            不交押金也能入驻。<br />
            平台从每单抽成中逐步抵扣 ¥200，扣完为止。
          </div>
        </div>
      </div>
    </section>


    <!-- ==================================================
         区块 4：接单模式
    ================================================== -->
    <section class="dvd-block">
      <div class="dvd-head">
        <div class="dvd-head-left">
          <span class="dvd-idx">04</span>
          <span class="dvd-title">接单模式</span>
        </div>
        <span class="dvd-code">FLEXIBLE MODE</span>
      </div>

      <ul class="free-list">
        <li>
          <div class="fl-idx">A</div>
          <div class="fl-body">
            <b>自由挂单</b>
            <span>上线即接受派单，下线不收通知，随时切换</span>
          </div>
        </li>
        <li>
          <div class="fl-idx">B</div>
          <div class="fl-body">
            <b>不限制外接</b>
            <span>不影响你在其他平台 / 俱乐部接单，多线并行</span>
          </div>
        </li>
        <li>
          <div class="fl-idx">C</div>
          <div class="fl-body">
            <b>随缘接单</b>
            <span>有空就接，不想接就休息，无强制出勤要求</span>
          </div>
        </li>
      </ul>
    </section>


    <!-- ==================================================
         试算器
    ================================================== -->
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
          <label>累计接单档位</label>
          <select v-model="commissionRange">
            <option v-for="t in commissionTiers" :key="t.id" :value="t.id">
              {{ t.range }} · 抽成 {{ t.commission }}%
            </option>
          </select>
        </div>

        <div class="calc-field">
          <label>打手数量</label>
          <select v-model.number="operatorCount">
            <option :value="1">1 人（陪玩单）</option>
            <option :value="2">2 人（护航单）</option>
          </select>
        </div>

        <div class="calc-field">
          <label>当前平台抽成</label>
          <div class="calc-readonly">{{ currentCommission.commission }}%</div>
        </div>

        <div class="calc-result">
          <div class="cr-row">
            <span>订单总额（打手池）</span>
            <b>¥{{ payout.toFixed(2) }}</b>
          </div>

          <div class="cr-row highlight">
            <span>
              每人实得
              <em class="cr-split">（{{ operatorCount }} 人平分）</em>
            </span>
            <b class="cr-main">¥{{ perOperator.toFixed(2) }}</b>
          </div>

          <div class="cr-row">
            <span>俱乐部留存</span>
            <b>¥{{ platformKeep.toFixed(2) }}</b>
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
    return {
      amount: 388,
      commissionRange: 'new',
      operatorCount: 2,

      // ⭐ 抽成档位（按累计接单数）
      commissionTiers: [
        { id: 'new',    range: '1 - 15 单',  commission: 25, payout: 75, note: '新晋干员' },
        { id: 'stable', range: '16 - 30 单', commission: 20, payout: 80, note: '稳定干员' },
        { id: 'senior', range: '31 单以上',  commission: 15, payout: 85, note: '资深干员' }
      ]
    }
  },

  computed: {
    currentCommission() {
      return this.commissionTiers.find(t => t.id === this.commissionRange)
        || this.commissionTiers[0]
    },

    /** 打手池总额（订单金额 × 打手实得比例） */
    payout() {
      return (Number(this.amount) || 0) * this.currentCommission.payout / 100
    },

    /** 每个打手实得（打手池 ÷ 人数） */
    perOperator() {
      const count = Math.max(1, this.operatorCount)
      return this.payout / count
    },

    /** 平台留存 */
    platformKeep() {
      return (Number(this.amount) || 0) - this.payout
    }
  }
}
</script>

<style scoped>
/* ==================================================
   区块容器
================================================== */
.dvd-block + .dvd-block {
  margin-top: 56px;
}

.dvd-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 16px;
  padding-bottom: 14px;
  margin-bottom: 18px;
  border-bottom: 1px solid var(--line);
}
.dvd-head-left {
  display: flex;
  align-items: baseline;
  gap: 12px;
}
.dvd-idx {
  font-family: monospace;
  font-size: 11px;
  letter-spacing: 1px;
  color: var(--orange);
}
.dvd-title {
  font-size: 17px;
  font-weight: 700;
  letter-spacing: 1.5px;
  color: var(--text);
}
.dvd-code {
  font-family: monospace;
  font-size: 8px;
  letter-spacing: 2px;
  color: var(--text-muted);
}

.dvd-desc {
  margin: 0 0 18px;
  font-size: 12px;
  line-height: 1.7;
  color: var(--text-muted);
}
.dvd-desc .hl {
  color: var(--orange);
  font-weight: 600;
}

/* ==================================================
   表格
================================================== */
.dv-table {
  width: 100%;
  border-collapse: collapse;
}
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
.dv-table tbody tr:hover td {
  background: rgba(255, 107, 26, 0.035);
}
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
.dv-name {
  font-weight: 600;
  color: var(--text);
}
.dv-muted {
  color: var(--text-muted);
  font-size: 12px;
}
.dv-rate {
  font-family: monospace;
  font-size: 15px;
  color: var(--cyan);
}
.dv-rate.up { color: var(--cyan); }
.dv-rate.out { color: #ff8c4a; }

/* ==================================================
   押金政策
================================================== */
.deposit-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}
.dp-card {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 22px 24px;
  border: 1px solid var(--line);
  background: rgba(16, 25, 35, 0.5);
  transition: border-color 0.2s;
}
.dp-card:hover { border-color: var(--line-bright); }
.dp-card.highlight {
  border-color: rgba(120, 215, 206, 0.35);
  background: rgba(120, 215, 206, 0.04);
}
.dp-card.highlight:hover { border-color: rgba(120, 215, 206, 0.6); }

.dp-icon { font-size: 26px; line-height: 1; }
.dp-title {
  font-size: 15px;
  font-weight: 700;
  letter-spacing: 0.5px;
  color: var(--text);
}
.dp-desc {
  font-size: 12px;
  line-height: 1.7;
  color: var(--text-muted);
}

/* ==================================================
   接单模式
================================================== */
.free-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.free-list li {
  display: flex;
  align-items: flex-start;
  gap: 16px;
  padding: 16px 20px;
  border-left: 2px solid var(--orange);
  background: rgba(255, 107, 26, 0.04);
  border-radius: 0 3px 3px 0;
}
.fl-idx {
  flex-shrink: 0;
  width: 22px;
  height: 22px;
  display: grid;
  place-items: center;
  font-family: monospace;
  font-size: 11px;
  font-weight: 700;
  color: var(--orange);
  border: 1px solid rgba(255, 107, 26, 0.5);
  border-radius: 2px;
}
.fl-body { display: flex; flex-direction: column; gap: 4px; }
.fl-body b {
  font-size: 13px;
  color: var(--text);
  letter-spacing: 0.5px;
}
.fl-body span {
  font-size: 12px;
  line-height: 1.6;
  color: var(--text-muted);
}

/* ==================================================
   试算器
================================================== */
.calc {
  margin-top: 56px;
  max-width: 680px;
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
.calc-title {
  font-size: 13px;
  font-weight: 600;
  letter-spacing: 1px;
}
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
.calc-field select:focus {
  border-color: rgba(255, 107, 26, 0.5);
}

/* ⭐ 只读展示框（显示当前抽成） */
.calc-readonly {
  height: 40px;
  display: flex;
  align-items: center;
  padding: 0 12px;
  font-family: monospace;
  font-size: 15px;
  font-weight: 700;
  color: #ff8c4a;
  background: rgba(255, 140, 74, 0.06);
  border: 1px dashed rgba(255, 140, 74, 0.35);
}

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
.cr-row b {
  font-family: monospace;
  font-size: 13px;
  color: var(--text-soft);
}

/* ⭐ 每人实得高亮 */
.cr-row.highlight {
  padding: 12px 0;
  margin: 6px 0;
  border-top: 1px dashed var(--line);
  border-bottom: 1px dashed var(--line);
}
.cr-row.highlight span {
  color: var(--text-soft);
  font-size: 13px;
}
.cr-split {
  font-style: normal;
  font-family: monospace;
  font-size: 10px;
  color: var(--text-muted);
  margin-left: 4px;
}
.cr-main {
  font-size: 22px !important;
  color: var(--orange) !important;
}

/* ==================================================
   响应式
================================================== */
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

  .deposit-grid { grid-template-columns: 1fr; }
  .calc-body { grid-template-columns: 1fr; }

  .dvd-head { flex-wrap: wrap; gap: 8px; }
  .dvd-code { display: none; }
}
</style>