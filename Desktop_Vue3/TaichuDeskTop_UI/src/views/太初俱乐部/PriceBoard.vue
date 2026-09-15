<template>
  <div class="price-board">
    <div v-for="group in groups" :key="group.code" class="price-group">

      <!-- 头部 -->
      <div class="pg-head">
        <div>
          <h3>{{ group.name }}</h3>
          <span class="pg-code">{{ group.code.toUpperCase() }}</span>
        </div>
        <span class="pg-count">{{ expandRows(group).length }} 档</span>
      </div>

      <!-- 说明 -->
      <p v-if="group.description" class="pg-desc">{{ group.description }}</p>

      <!-- ⭐ 订单特有规则 -->
      <div v-if="group.specificRulesText" class="pg-rules">
        <div class="rules-title">⚡ 本单特有规则</div>
        <div class="rules-body">
          <div
            v-for="(line, i) in splitRules(group.specificRulesText)"
            :key="i"
            class="rules-line"
          >{{ line }}</div>
        </div>
      </div>

      <!-- 档位表 -->
      <table class="price-table">
        <thead>
          <tr>
            <th v-for="label in fieldLabels(group)" :key="label">{{ label }}</th>
            <th class="ta-r col-price">价格</th>
            <th class="ta-r col-action">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in expandRows(group)" :key="row.key">
            <td v-for="(val, k) in row.values" :key="k" class="pi-muted">
              {{ val }}
            </td>
            <td class="ta-r col-price">
              <b class="pi-price">¥{{ row.price }}</b>
            </td>
            <td class="ta-r col-action">
              <button class="pi-order" @click="handleOrder(group, row)">
                下单
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- ⭐ 游戏通用规则（页面最底部，只显示一次） -->
    <div v-if="commonRules" class="game-common-rules">
      <div class="gcr-head">
        <span class="gcr-icon">📋</span>
        <span class="gcr-title">通用规则 · 所有订单适用</span>
      </div>
      <div class="gcr-body">
        <div
          v-for="(line, i) in splitRules(commonRules)"
          :key="i"
          class="gcr-line"
        >{{ line }}</div>
      </div>
    </div>
  </div>
</template>

<script>
export default {
  name: 'PriceBoard',
  props: {
    groups: { type: Array, default: () => [] },
    commonRules: { type: String, default: '' }
  },
  emits: ['order'],

  methods: {
    parseSchema(group) {
      if (!group.paramsSchemaJson) return []
      try {
        const obj = JSON.parse(group.paramsSchemaJson)
        return Object.entries(obj).map(([name, def]) => ({
          name,
          type: def.type || 'text',
          options: def.options || []
        }))
      } catch {
        return []
      }
    },

    parsePricing(group) {
      if (!group.pricingJson) return {}
      try {
        return JSON.parse(group.pricingJson)
      } catch {
        return {}
      }
    },

    fieldLabels(group) {
      const fields = this.parseSchema(group).filter(f => f.type === 'select')
      if (fields.length === 0) return ['服务']
      return fields.map(f => f.name)
    },

    expandRows(group) {
      const schema = this.parseSchema(group)
      const pricing = this.parsePricing(group)
      const selectFields = schema.filter(f => f.type === 'select')

      if (selectFields.length === 0) {
        const price = pricing.default ?? 0
        return [{
          key: 'default',
          values: { 服务: group.name },
          params: {},
          price
        }]
      }

      const priceField = selectFields[0]
      const otherFields = selectFields.slice(1)

      return priceField.options.map(opt => {
        const params = { [priceField.name]: opt }
        const values = { [priceField.name]: opt }

        otherFields.forEach(f => {
          const first = f.options[0] || ''
          params[f.name] = first
          values[f.name] = first
        })

        const price = pricing[opt] ?? 0
        return {
          key: `${priceField.name}=${opt}`,
          values,
          params,
          price
        }
      })
    },

    // ⭐ 按换行切规则
    splitRules(text) {
      if (!text) return []
      return text.split('\n').map(s => s.trim()).filter(Boolean)
    },

    handleOrder(group, row) {
      this.$emit('order', {
        orderTypeCode: group.code,
        orderTypeName: group.name,
        params: row.params,
        price: row.price
      })
    }
  }
}
</script>

<style scoped>
.price-group + .price-group { margin-top: 42px; }

.pg-head {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 16px;
  padding-bottom: 14px;
  margin-bottom: 2px;
  border-bottom: 1px solid var(--line);
}
.pg-head h3 { margin: 0 0 5px; font-size: 17px; letter-spacing: 1.5px; }
.pg-code {
  font-family: monospace;
  font-size: 8px;
  letter-spacing: 2px;
  color: var(--text-muted);
}
.pg-count {
  font-family: monospace;
  font-size: 9px;
  letter-spacing: 1px;
  color: var(--text-muted);
}

.pg-desc {
  margin: 0 0 6px;
  padding: 8px 0;
  font-size: 12px;
  line-height: 1.7;
  color: var(--text-muted);
}

/* ⭐ 订单特有规则 */
.pg-rules {
  margin: 12px 0 12px;
  padding: 12px 16px;
  border-left: 3px solid var(--orange);
  background: rgba(255, 107, 26, 0.05);
}
.rules-title {
  margin-bottom: 8px;
  font-family: monospace;
  font-size: 10px;
  letter-spacing: 1.5px;
  color: var(--orange);
  font-weight: 600;
}
.rules-body {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.rules-line {
  font-size: 12px;
  line-height: 1.7;
  color: var(--text-soft);
}

/* 表格 */
.price-table {
  width: 100%;
  border-collapse: collapse;
  table-layout: fixed;
}
.price-table th {
  padding: 12px 14px;
  font-family: monospace;
  font-size: 9px;
  font-weight: 400;
  letter-spacing: 1px;
  text-align: left;
  color: var(--text-muted);
  border-bottom: 1px solid var(--line);
}
.price-table th.ta-r { text-align: right; }

.price-table td {
  padding: 16px 14px;
  font-size: 13px;
  border-bottom: 1px solid var(--line);
  transition: background 0.2s ease;
}
.price-table tbody tr:hover td { background: rgba(255, 107, 26, 0.035); }

.col-price  { width: 120px; }
.col-action { width: 100px; }

.ta-r { text-align: right; }

.pi-muted { color: var(--text-soft); font-size: 12px; }
.pi-price { font-family: monospace; font-size: 16px; color: var(--orange); }

.pi-order {
  padding: 6px 16px;
  font-size: 11px;
  letter-spacing: 1px;
  color: var(--text-soft);
  background: transparent;
  border: 1px solid var(--line-bright);
  cursor: pointer;
  transition: all 0.2s ease;
}
.pi-order:hover {
  color: #fff;
  background: var(--orange);
  border-color: var(--orange);
}

/* ⭐ 游戏通用规则 */
.game-common-rules {
  margin-top: 50px;
  padding: 20px 24px;
  border: 1px solid var(--line);
  background: rgba(16, 25, 35, 0.4);
  border-radius: 4px;
}
.gcr-head {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 14px;
  padding-bottom: 12px;
  border-bottom: 1px dashed var(--line);
}
.gcr-icon { font-size: 16px; }
.gcr-title {
  font-family: monospace;
  font-size: 11px;
  letter-spacing: 1.5px;
  color: var(--cyan);
  font-weight: 600;
}
.gcr-body {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.gcr-line {
  font-size: 12px;
  line-height: 1.7;
  color: var(--text-soft);
  padding-left: 4px;
}

/* 移动端 */
@media (max-width: 640px) {
  .price-table thead { display: none; }
  .price-table tr {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 8px 14px;
    padding: 16px 0;
    border-bottom: 1px solid var(--line);
  }
  .price-table td { padding: 0; border: none; }
  .price-table td:first-child { flex: 1 0 100%; }
  .pi-price { font-size: 15px; }
}
</style>