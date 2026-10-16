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
  background: rgba(0, 0, 0, 0.78);
  backdrop-filter: blur(8px);
  display: grid; place-items: center;
  padding: 24px;
}
.crm-modal {
  width: min(480px, 100%);
  max-height: 90vh; overflow-y: auto;
  border: 1px solid rgba(255, 255, 255, 0.15);
  background: #0e1720; color: #e8eef4;
  border-radius: 4px;
}
.crm-head {
  display: flex; justify-content: space-between; align-items: center;
  padding: 18px 24px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}
.crm-head h3 { margin: 0; font-size: 16px; letter-spacing: 1px; }
.close-icon {
  background: none; border: none; font-size: 22px;
  color: #6b7a8a; cursor: pointer; line-height: 1;
}
.close-icon:hover { color: #e8eef4; }

.crm-body { padding: 24px; }

.crm-order {
  display: flex; flex-direction: column; gap: 4px;
  padding: 12px 14px; margin-bottom: 20px;
  background: rgba(255, 255, 255, 0.03);
  border-left: 2px solid #a5a5ff;
  border-radius: 2px;
}
.crm-order-no { font-size: 13px; color: #a5a5ff; }
.crm-order-game { font-size: 11px; color: #8b9aab; }
.crm-cs { font-size: 12px; color: #8b9aab; margin-top: 4px; }
.crm-cs b { color: #78d7ce; }
.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }

.crm-stars { display: flex; flex-direction: column; gap: 14px; margin-bottom: 22px; }
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

.crm-comment label {
  display: block; margin-bottom: 8px;
  font-family: monospace; font-size: 10px;
  letter-spacing: 1px; color: #8b9aab;
}
.crm-comment textarea {
  width: 100%; padding: 10px 12px;
  font-family: inherit; font-size: 13px;
  color: #e8eef4; background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 3px; outline: none; resize: vertical;
}
.crm-comment textarea:focus { border-color: rgba(165, 165, 255, 0.5); }
.crm-len {
  margin-top: 4px; text-align: right;
  font-family: monospace; font-size: 10px;
  color: #6b7a8a;
}

.crm-foot {
  display: flex; gap: 12px;
  padding: 18px 24px;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
}
.btn-ghost, .btn-solid {
  flex: 1; height: 42px;
  font-size: 13px; letter-spacing: 1px;
  cursor: pointer; border-radius: 3px;
  transition: all 0.2s;
}
.btn-ghost {
  color: #8b9aab; background: transparent;
  border: 1px solid rgba(255, 255, 255, 0.15);
}
.btn-ghost:hover { color: #e8eef4; }
.btn-solid {
  color: #fff; background: #a5a5ff;
  border: 1px solid #a5a5ff;
}
.btn-solid:hover:not(:disabled) { background: #b8b8ff; }
.btn-solid:disabled { opacity: 0.5; cursor: not-allowed; }
</style>