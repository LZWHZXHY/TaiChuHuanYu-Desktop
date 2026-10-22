<template>
  <Teleport to="body">
    <div v-if="visible" class="review-modal-mask" @click.self="close">
      <div class="review-modal">
        <header class="rm-head">
          <h3>{{ fromCustomer ? '评价打手' : '评价老板' }}</h3>
          <button class="close-icon" @click="close">×</button>
        </header>

        <div class="rm-body">
          <div v-if="order" class="rm-order">
            <span class="rm-order-no mono">{{ order.orderNo }}</span>
            <span class="rm-order-game">{{ order.gameName }} · {{ order.orderTypeName }}</span>
            <span v-if="order.operatorName" class="rm-order-target">
              评价对象：<b>{{ order.operatorName }}</b>
            </span>
          </div>

          <div class="rm-stars">
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

          <div class="rm-comment">
            <label>评价留言（可选）</label>
            <textarea
              v-model="form.comment"
              rows="3"
              placeholder="说说这次体验如何…"
              maxlength="500"
            ></textarea>
            <div class="rm-len">{{ form.comment.length }} / 500</div>
          </div>
        </div>

        <footer class="rm-foot">
          <button class="btn-ghost" @click="close">取消</button>
          <button class="btn-solid" :disabled="submitting || !isValid" @click="submit">
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
  name: 'ReviewModal',
  props: {
    visible: { type: Boolean, default: false },
    order: { type: Object, default: null },
    fromCustomer: { type: Boolean, default: true }
  },
  emits: ['close', 'success'],

  data() {
    return {
      submitting: false,
      form: {
        skillScore: 5,
        attitudeScore: 5,
        punctualScore: 5,
        overallScore: 5,
        comment: ''
      }
    }
  },

  computed: {
    dims() {
      return [
        { key: 'skillScore',    label: '技术' },
        { key: 'attitudeScore', label: '态度' },
        { key: 'punctualScore', label: '准时' },
        { key: 'overallScore',  label: '综合' }
      ]
    },
    isValid() {
      const f = this.form
      return f.skillScore > 0 && f.attitudeScore > 0
          && f.punctualScore > 0 && f.overallScore > 0
    }
  },

  watch: {
    visible(v) {
      if (v) {
        this.form = {
          skillScore: 5,
          attitudeScore: 5,
          punctualScore: 5,
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
        // ⭐ 带上被评打手的 ID
        await request.post(`/club/orders/${this.order.orderNo}/review`, {
          toOperatorUserId: this.order.operatorUserId || null,
          skillScore: this.form.skillScore,
          attitudeScore: this.form.attitudeScore,
          punctualScore: this.form.punctualScore,
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
.review-modal-mask {
  position: fixed; inset: 0; z-index: 9999;
  background: var(--overlay);
  backdrop-filter: blur(8px);
  display: grid; place-items: center; padding: 24px;
}
.review-modal {
  width: min(480px, 100%);
  max-height: 90vh; overflow-y: auto;
  border: 1px solid var(--line);
  background: var(--paper-card); color: var(--text-primary); border-radius: 4px;
}
.rm-head {
  display: flex; justify-content: space-between; align-items: center;
  padding: 18px 24px; border-bottom: 1px solid var(--bg-soft);
}
.rm-head h3 { margin: 0; font-size: 16px; letter-spacing: 1px; }
.close-icon {
  background: none; border: none; font-size: 22px;
  color: var(--text-secondary); cursor: pointer; line-height: 1;
}
.close-icon:hover { color: var(--text-primary); }

.rm-body { padding: 24px; }

.rm-order {
  display: flex; flex-direction: column; gap: 4px;
  padding: 12px 14px; margin-bottom: 20px;
  background: var(--bg-soft);
  border-left: 2px solid var(--accent-color);
  border-radius: 2px;
}
.rm-order-no { font-size: 13px; color: var(--accent-color); }
.rm-order-game { font-size: 12px; color: var(--text-secondary); }
.rm-order-target {
  font-size: 12px;
  color: var(--text-secondary);
  margin-top: 4px;
  padding-top: 6px;
  border-top: 1px dashed var(--line);
}
.rm-order-target b { color: var(--success); }
.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }

.rm-stars { display: flex; flex-direction: column; gap: 14px; margin-bottom: 22px; }
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

.rm-comment label {
  display: block; margin-bottom: 8px;
  font-family: monospace; font-size: 12px;
  letter-spacing: 1px; color: var(--text-secondary);
}
.rm-comment textarea {
  width: 100%; padding: 10px 12px;
  font-family: inherit; font-size: 13px;
  color: var(--text-primary); background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 3px; outline: none; resize: vertical;
}
.rm-comment textarea:focus { border-color: rgba(255, 107, 26, 0.5); }
.rm-len {
  margin-top: 4px; text-align: right;
  font-family: monospace; font-size: 12px;
  color: var(--text-secondary);
}

.rm-foot {
  display: flex; gap: 12px;
  padding: 18px 24px;
  border-top: 1px solid var(--bg-soft);
}
.btn-ghost, .btn-solid {
  flex: 1; height: 42px; font-size: 13px; letter-spacing: 1px;
  cursor: pointer; border-radius: 3px;
}
.btn-ghost { color: var(--text-secondary); background: transparent; border: 1px solid var(--line); }
.btn-ghost:hover { color: var(--text-primary); }
.btn-solid { color: var(--on-accent); background: var(--accent-color); border: 1px solid var(--accent-color); }
.btn-solid:hover:not(:disabled) { background: var(--accent-color); }
.btn-solid:disabled { opacity: 0.5; cursor: not-allowed; }
</style>
