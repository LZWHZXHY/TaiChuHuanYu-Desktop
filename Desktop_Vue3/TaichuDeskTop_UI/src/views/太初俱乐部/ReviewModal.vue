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
        await request.post(`/club/orders/${this.order.orderNo}/review`, {
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
  background: rgba(0, 0, 0, 0.78);
  backdrop-filter: blur(8px);
  display: grid; place-items: center; padding: 24px;
}
.review-modal {
  width: min(480px, 100%);
  max-height: 90vh; overflow-y: auto;
  border: 1px solid rgba(255, 255, 255, 0.15);
  background: #0e1720; color: #e8eef4; border-radius: 4px;
}
.rm-head {
  display: flex; justify-content: space-between; align-items: center;
  padding: 18px 24px; border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}
.rm-head h3 { margin: 0; font-size: 16px; letter-spacing: 1px; }
.close-icon {
  background: none; border: none; font-size: 22px;
  color: #6b7a8a; cursor: pointer; line-height: 1;
}
.close-icon:hover { color: #e8eef4; }

.rm-body { padding: 24px; }

.rm-order {
  display: flex; flex-direction: column; gap: 4px;
  padding: 12px 14px; margin-bottom: 20px;
  background: rgba(255, 255, 255, 0.03);
  border-left: 2px solid #ff6b1a;
  border-radius: 2px;
}
.rm-order-no { font-size: 13px; color: #ff6b1a; }
.rm-order-game { font-size: 11px; color: #8b9aab; }
.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }

.rm-stars { display: flex; flex-direction: column; gap: 14px; margin-bottom: 22px; }
.star-row {
  display: grid;
  grid-template-columns: 48px 1fr 32px;
  align-items: center; gap: 12px;
}
.star-label {
  font-family: monospace; font-size: 11px;
  letter-spacing: 1px; color: #8b9aab;
}
.star-group { display: flex; gap: 4px; }
.star {
  background: none; border: none;
  font-size: 22px; line-height: 1;
  color: rgba(255, 255, 255, 0.15);
  cursor: pointer; transition: color 0.15s, transform 0.15s;
  padding: 0;
}
.star:hover { transform: scale(1.15); }
.star.on { color: #ffb800; }
.star-value {
  font-family: monospace; font-size: 13px;
  color: #ffb800; text-align: right;
}

.rm-comment label {
  display: block; margin-bottom: 8px;
  font-family: monospace; font-size: 10px;
  letter-spacing: 1px; color: #8b9aab;
}
.rm-comment textarea {
  width: 100%; padding: 10px 12px;
  font-family: inherit; font-size: 13px;
  color: #e8eef4; background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 3px; outline: none; resize: vertical;
}
.rm-comment textarea:focus { border-color: rgba(255, 107, 26, 0.5); }
.rm-len {
  margin-top: 4px; text-align: right;
  font-family: monospace; font-size: 10px;
  color: #6b7a8a;
}

.rm-foot {
  display: flex; gap: 12px;
  padding: 18px 24px;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
}
.btn-ghost, .btn-solid {
  flex: 1; height: 42px; font-size: 13px; letter-spacing: 1px;
  cursor: pointer; border-radius: 3px;
}
.btn-ghost { color: #8b9aab; background: transparent; border: 1px solid rgba(255, 255, 255, 0.15); }
.btn-ghost:hover { color: #e8eef4; }
.btn-solid { color: #fff; background: #ff6b1a; border: 1px solid #ff6b1a; }
.btn-solid:hover:not(:disabled) { background: #ff7e32; }
.btn-solid:disabled { opacity: 0.5; cursor: not-allowed; }
</style>