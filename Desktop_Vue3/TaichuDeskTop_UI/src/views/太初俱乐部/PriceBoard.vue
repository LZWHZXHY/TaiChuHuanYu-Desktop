<template>
  <div class="price-board">

    <!-- ⭐ 游戏通用规则（置顶，一眼可见） -->
    <section v-if="commonRules" class="game-common-rules">
      <div class="gcr-head">
        <span class="gcr-icon">📋</span>
        <span class="gcr-title">通用规则 · 所有订单适用</span>
        <button type="button" class="gcr-toggle" @click="rulesOpen = !rulesOpen">
          {{ rulesOpen ? '收起' : '展开' }}
        </button>
      </div>
      <div v-if="rulesOpen" class="gcr-body">
        <div
          v-for="(line, i) in splitRules(commonRules)"
          :key="i"
          class="gcr-line"
        >{{ line }}</div>
      </div>
    </section>

    <!-- ⭐ 分组切换标签（吸顶） -->
    <nav v-if="groups.length" ref="tabs" class="pg-tabs">
      <button
        v-for="g in groupsWithRows"
        :key="g.code"
        type="button"
        class="pg-tab"
        :class="{ active: g.code === activeCode }"
        @click="selectGroup(g.code)"
      >
        <span class="tab-name">{{ g.name }}</span>
        <span class="tab-count">{{ g.rows.length }}</span>
      </button>
    </nav>

    <!-- ⭐ 只渲染当前分组 -->
    <div v-if="activeGroup" class="price-group">

      <!-- 头部 -->
      <div class="pg-head">
        <div>
          <h3>{{ activeGroup.name }}</h3>
          <span class="pg-code">{{ activeGroup.code.toUpperCase() }}</span>
        </div>
        <span class="pg-count">{{ activeGroup.rows.length }} 档</span>
      </div>

      <!-- 说明 -->
      <p v-if="activeGroup.description" class="pg-desc">{{ activeGroup.description }}</p>

      <!-- 订单特有规则 -->
      <div v-if="activeGroup.specificRulesText" class="pg-rules">
        <div class="rules-title">⚡ 本单特有规则</div>
        <div class="rules-body">
          <div
            v-for="(line, i) in splitRules(activeGroup.specificRulesText)"
            :key="i"
            class="rules-line"
          >{{ line }}</div>
        </div>
      </div>

      <!-- 档位表 -->
      <table class="price-table">
        <thead>
          <tr>
            <th v-for="label in fieldLabels(activeGroup)" :key="label">{{ label }}</th>
            <th class="ta-r col-price">价格</th>
            <th class="ta-r col-action">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in activeGroup.rows" :key="row.key">
            <td v-for="(val, k) in row.values" :key="k" class="pi-muted">
              {{ val }}
            </td>
            <td class="ta-r col-price">
              <b class="pi-price">¥{{ row.price }}</b>
            </td>
            <td class="ta-r col-action">
              <button class="pi-order" @click="handleOrder(activeGroup, row)">
                下单
              </button>
            </td>
          </tr>
        </tbody>
      </table>
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

  data() {
    return {
      activeCode: '',
      rulesOpen: true
    }
  },

  computed: {
    /** 预计算每个分组的展开行，避免模板里反复调用 */
    groupsWithRows() {
      return this.groups.map(g => ({ ...g, rows: this.expandRows(g) }))
    },
    activeGroup() {
      return (
        this.groupsWithRows.find(g => g.code === this.activeCode) ||
        this.groupsWithRows[0] ||
        null
      )
    }
  },

  watch: {
    groups: {
      immediate: true,
      handler(val) {
        if (!val.length) {
          this.activeCode = ''
          return
        }
        if (!val.some(g => g.code === this.activeCode)) {
          this.activeCode = val[0].code
        }
      }
    }
  },

  methods: {
    selectGroup(code) {
      if (code === this.activeCode) return
      this.activeCode = code
      // 切换后如果页面已滚到下方，拉回到标签栏位置
      this.$nextTick(() => {
        const el = this.$refs.tabs
        if (!el) return
        const top = el.getBoundingClientRect().top
        if (top < 0) window.scrollBy({ top, behavior: 'smooth' })
      })
    },

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

    splitRules(text) {
      if (!text) return []
      return text.split('\n').map(s => s.trim()).filter(Boolean)
    },

    handleOrder(group, row) {
      this.$emit('order', {
        orderTypeCode: group.code,
        orderTypeName: group.name,
        params: row.params,
        price: row.price,
        pricingMode: group.pricingMode || 'fixed',
        minOperatorCount: group.minOperatorCount || 1,
        maxOperatorCount: group.maxOperatorCount || 1
      })
    }
  }
}
</script>

<style scoped>
/* ══ 顶部通用规则 ══ */
.game-common-rules {
  margin-bottom: 24px;
  padding: 16px 20px;
  border: 1px solid var(--line);
  border-left: 3px solid var(--cyan);
  background: var(--panel);
  border-radius: 4px;
}
.gcr-head {
  display: flex;
  align-items: center;
  gap: 10px;
}
.gcr-icon { font-size: 16px; }
.gcr-title {
  flex: 1;
  font-family: monospace;
  font-size: 12px;
  letter-spacing: 1.5px;
  color: var(--cyan);
  font-weight: 600;
}
.gcr-toggle {
  padding: 4px 12px;
  font-size: 12px;
  letter-spacing: 1px;
  color: var(--text-muted);
  background: transparent;
  border: 1px solid var(--line);
  cursor: pointer;
  transition: all 0.2s ease;
}
.gcr-toggle:hover { color: var(--orange); border-color: var(--orange); }
.gcr-body {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-top: 14px;
  padding-top: 12px;
  border-top: 1px dashed var(--line);
}
.gcr-line {
  font-size: 12px;
  line-height: 1.7;
  color: var(--text-soft);
  padding-left: 4px;
}

/* ══ 分组切换标签（吸顶） ══ */
.pg-tabs {
  position: sticky;
  top: 0;
  z-index: 20;
  display: flex;
  gap: 8px;
  overflow-x: auto;
  padding: 10px 0;
  margin-bottom: 22px;
  background: var(--bg, var(--paper-card));
  border-bottom: 1px solid var(--line);
  scrollbar-width: none;
  -webkit-overflow-scrolling: touch;
}
.pg-tabs::-webkit-scrollbar { display: none; }

.pg-tab {
  flex: 0 0 auto;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  font-size: 12px;
  letter-spacing: 1px;
  white-space: nowrap;
  color: var(--text-muted);
  background: transparent;
  border: 1px solid var(--line);
  cursor: pointer;
  transition: all 0.2s ease;
}
.pg-tab:hover {
  color: var(--text-soft);
  border-color: var(--line-bright);
}
.pg-tab.active {
  color: var(--on-accent);
  background: var(--orange);
  border-color: var(--orange);
}
.tab-count {
  font-family: monospace;
  font-size: 12px;
  opacity: 0.75;
}

/* ══ 分组头部 ══ */
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
  font-size: 12px;
  letter-spacing: 2px;
  color: var(--text-muted);
}
.pg-count {
  font-family: monospace;
  font-size: 12px;
  letter-spacing: 1px;
  color: var(--text-muted);
}

.pg-desc {
  margin: 0 0 6px;
  padding: 8px 0;
  font-size: 12px;
  line-height: 1.7;
  color: var(--text-muted);
  white-space: pre-line;
}

/* ══ 订单特有规则 ══ */
.pg-rules {
  margin: 12px 0;
  padding: 12px 16px;
  border-left: 3px solid var(--orange);
  background: rgba(255, 107, 26, 0.05);
}
.rules-title {
  margin-bottom: 8px;
  font-family: monospace;
  font-size: 12px;
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

/* ══ 表格 ══ */
.price-table {
  width: 100%;
  border-collapse: collapse;
  table-layout: fixed;
}
.price-table th {
  padding: 12px 14px;
  font-family: monospace;
  font-size: 12px;
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
  font-size: 12px;
  letter-spacing: 1px;
  color: var(--text-soft);
  background: transparent;
  border: 1px solid var(--line-bright);
  cursor: pointer;
  transition: all 0.2s ease;
}
.pi-order:hover {
  color: var(--on-accent);
  background: var(--orange);
  border-color: var(--orange);
}

/* ══ 移动端 ══ */
@media (max-width: 640px) {
  .game-common-rules { padding: 14px 16px; }
  .pg-tab { padding: 7px 13px; font-size: 12px; }

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
