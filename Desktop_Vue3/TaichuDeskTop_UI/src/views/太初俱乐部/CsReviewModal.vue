<template>
  <Teleport to="body">
    <div v-if="visible" class="crm-mask" @click.self="close">
      <div class="crm-modal">
        <header class="crm-head">
          <h3>评价客服</h3>
          <button class="close-icon" @click="close">×</button>
        </header>

        <div class="crm-body">
          <!-- 订单 + 客服信息 -->
          <div v-if="order" class="crm-order">
            <span class="crm-order-no mono">{{ order.orderNo }}</span>
            <span class="crm-order-game">
              {{ order.gameName }} · {{ order.orderTypeName }}
            </span>
            <span v-if="csName" class="crm-cs">
              对接客服：<b>{{ csName }}</b>
            </span>
          </div>

          <!-- 4 个维度 -->
          <div class="crm-stars">
            <div class="star-row" v-for="dim in dims" :key="dim.key">
              <span class="star-label">{{ dim.label }}</span>
              <div class="star-group">
                <button
                  v-for="n in 5" :key="n"
                  class="star"
                  :class="{ on: form[dim.key] >= n }"
                  @click="form[dim.key] = n"
                >★</button>
              </div>
              <span class="star-value">{{ form[dim.key] || '—' }}</span>
            </div>
          </div>

          <!-- 留言 -->
          <div class="crm-comment">
            <label>评价留言（可选）</label>
            <textarea
              v-model="form.comment"
              rows="3"
              placeholder="说说客服的服务如何…"
              maxlength="500"
            ></textarea>
            <div class="crm-len">{{ form.comment.length }} / 500</div>
          </div>
        </div>

        <footer class="crm-foot">
          <button class="btn-ghost" @click="close">取消</button>
          <button
            class="btn-solid"
            :disabled="submitting || !isValid"
            @click="submit"
          >
            {{ submitting ? '提交中…' : '提交评价' }}
          </button>
        </footer>
      </div>
    </div>
  </Teleport>
</template>

<script>
import request from '@/utils/request'

export default {
  name: 'CsReviewModal',
  props: {
    visible: { type: Boolean, default: false },
    order: { type: Object, default: null },
    csName: { type: String, default: '' }
  },
  emits: ['close', 'success'],

  data() {
    return {
      submitting: false,
      form: {
        responseScore: 5,
        attitudeScore: 5,
        professionalScore: 5,
        overallScore: 5,
        comment: ''
      }
    }
  },

  computed: {
    dims() {
      return [
        { key: 'responseScore',     label: '响应' },
        { key: 'attitudeScore',     label: '态度' },
        { key: 'professionalScore', label: '专业' },
        { key: 'overallScore',      label: '综合' }
      ]
    },
    isValid() {
      const f = this.form
      return f.responseScore > 0 && f.attitudeScore > 0
          && f.professionalScore > 0 && f.overallScore > 0
    }
  },

  watch: {
    visible(v) {
      if (v) {
        this.form = {
          responseScore: 5,
          attitudeScore: 5,
          professionalScore: 5,
          overallScore: 5,
          comment: ''
        }
      }
    }
  },

  methods: {
    close() { this.$emit('close') },

    async submit() {
      if (!this.order?.orderNo || this.submitting) return
      this.submitting = true
      try {
        await request.post(`/club/cs/orders/${this.order.orderNo}/review`, {
          responseScore: this.form.responseScore,
          attitudeScore: this.form.attitudeScore,
          professionalScore: this.form.professionalScore,
          overallScore: this.form.overallScore,
          comment: this.form.comment.trim() || null
        })
        this.$emit('success')
        this.close()
      } catch (e) {
        alert(e?.response?.data?.message || e.message || '提交失败')
      } finally {
        this.submitting = false
      }
    }
  }
}
</script>

<style scoped>
.crm-mask {
  position: fixed; inset: 0; z-index: 9999;
  background: var(--overlay);
  backdrop-filter: blur(8px);
  display: grid; place-items: center;
  padding: 24px;
}
.crm-modal {
  width: min(480px, 100%);
  max-height: 90vh; overflow-y: auto;
  border: 1px solid var(--line);
  background: var(--paper-card); color: var(--text-primary);
  border-radius: 4px;
}
.crm-head {
  display: flex; justify-content: space-between; align-items: center;
  padding: 18px 24px;
  border-bottom: 1px solid var(--bg-soft);
}
.crm-head h3 { margin: 0; font-size: 16px; letter-spacing: 1px; }
.close-icon {
  background: none; border: none; font-size: 22px;
  color: var(--text-secondary); cursor: pointer; line-height: 1;
}
.close-icon:hover { color: var(--text-primary); }

.crm-body { padding: 24px; }

.crm-order {
  display: flex; flex-direction: column; gap: 4px;
  padding: 12px 14px; margin-bottom: 20px;
  background: var(--bg-soft);
  border-left: 2px solid var(--accent-color);
  border-radius: 2px;
}
.crm-order-no { font-size: 13px; color: var(--accent-color); }
.crm-order-game { font-size: 12px; color: var(--text-secondary); }
.crm-cs { font-size: 12px; color: var(--text-secondary); margin-top: 4px; }
.crm-cs b { color: var(--success); }
.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }

.crm-stars { display: flex; flex-direction: column; gap: 14px; margin-bottom: 22px; }
.star-row {
  display: grid;
  grid-template-columns: 48px 1fr 32px;
  align-items: center; gap: 12px;
}
.star-label {
  font-family: monospace; font-size: 12px;
  letter-spacing: 1px; color: var(--text-secondary);
}
.star-group { display: flex; gap: 4px; }
.star {
  background: none; border: none;
  font-size: 22px; line-height: 1;
  color: var(--text-secondary);
  cursor: pointer; transition: color 0.15s, transform 0.15s;
  padding: 0;
}
.star:hover { transform: scale(1.15); }
.star.on { color: var(--warning); }
.star-value {
  font-family: monospace; font-size: 13px;
  color: var(--warning); text-align: right;
}

.crm-comment label {
  display: block; margin-bottom: 8px;
  font-family: monospace; font-size: 12px;
  letter-spacing: 1px; color: var(--text-secondary);
}
.crm-comment textarea {
  width: 100%; padding: 10px 12px;
  font-family: inherit; font-size: 13px;
  color: var(--text-primary); background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 3px; outline: none; resize: vertical;
}
.crm-comment textarea:focus { border-color: rgba(165, 165, 255, 0.5); }
.crm-len {
  margin-top: 4px; text-align: right;
  font-family: monospace; font-size: 12px;
  color: var(--text-secondary);
}

.crm-foot {
  display: flex; gap: 12px;
  padding: 18px 24px;
  border-top: 1px solid var(--bg-soft);
}
.btn-ghost, .btn-solid {
  flex: 1; height: 42px;
  font-size: 13px; letter-spacing: 1px;
  cursor: pointer; border-radius: 3px;
  transition: all 0.2s;
}
.btn-ghost {
  color: var(--text-secondary); background: transparent;
  border: 1px solid var(--line);
}
.btn-ghost:hover { color: var(--text-primary); }
.btn-solid {
  color: var(--text); background: var(--accent-color);
  border: 1px solid var(--accent-color);
}
.btn-solid:hover:not(:disabled) { background: var(--accent-color); }
.btn-solid:disabled { opacity: 0.5; cursor: not-allowed; }
</style>
