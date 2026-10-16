<template>
  <Teleport to="body">
    <div v-if="visible" class="cp-mask" @click.self="close">
      <div class="cp-modal">
        <header class="cp-head">
          <div>
            <h3>提交客诉</h3>
            <p class="cp-sub">
              {{ order?.orderNo }} · {{ order?.gameName }} · {{ order?.orderTypeName }}
            </p>
          </div>
          <button class="close-icon" @click="close">×</button>
        </header>

        <div class="cp-body">
          <!-- 客诉对象 -->
          <div class="cp-section">
            <div class="cp-label">投诉对象 <em>*</em></div>
            <div class="target-list">
              <label class="target-item" :class="{ on: form.targetOperatorId === null }">
                <input type="radio" :value="null" v-model="form.targetOperatorId" />
                <div class="ti-icon">📦</div>
                <div class="ti-info">
                  <div class="ti-name">整个订单</div>
                  <div class="ti-desc">不针对具体打手</div>
                </div>
              </label>

              <label
                v-for="op in (order?.operators || [])"
                :key="op.userId"
                class="target-item"
                :class="{ on: form.targetOperatorId === op.userId }"
              >
                <input type="radio" :value="op.userId" v-model="form.targetOperatorId" />
                <div class="ti-icon">🎮</div>
                <div class="ti-info">
                  <div class="ti-name">{{ op.name }}</div>
                  <div class="ti-desc">指定这位打手</div>
                </div>
              </label>
            </div>
          </div>

          <!-- 投诉类型 -->
          <div class="cp-section">
            <div class="cp-label">投诉类型 <em>*</em></div>
            <div class="type-grid">
              <label
                v-for="t in TYPES"
                :key="t.value"
                class="type-item"
                :class="{ on: form.complaintType === t.value }"
              >
                <input type="radio" :value="t.value" v-model="form.complaintType" />
                <span class="type-icon">{{ t.icon }}</span>
                <span class="type-label">{{ t.label }}</span>
              </label>
            </div>
          </div>

          <!-- 描述 -->
          <div class="cp-section">
            <div class="cp-label">详细描述 <em>*</em></div>
            <textarea
              v-model="form.description"
              class="cp-textarea"
              rows="4"
              maxlength="2000"
              placeholder="请详细描述问题，客服会尽快核实处理"
            ></textarea>
            <div class="cp-len">{{ form.description.length }} / 2000</div>
          </div>

          <!-- 证据 -->
          <div class="cp-section">
            <div class="cp-label">证据截图（可选）</div>

            <div v-if="!previewUrl" class="cp-upload-area">
              <input
                ref="fileInput"
                type="file"
                accept="image/jpeg,image/png,image/webp"
                class="cp-file-input"
                @change="onFileChange"
              />
              <div class="cp-upload-hint" @click="pickFile">
                <div class="cp-upload-icon">📎</div>
                <div class="cp-upload-text">点击上传截图</div>
                <div class="cp-upload-desc">支持 jpg / png / webp，最大 5MB</div>
              </div>
            </div>

            <div v-else class="cp-preview">
              <img :src="previewUrl" alt="证据截图" />
              <div class="cp-preview-actions">
                <span class="cp-preview-name">{{ fileName }}</span>
                <button class="cp-remove-btn" @click="removeFile">移除</button>
              </div>
            </div>

            <div v-if="uploading" class="cp-progress">
              <div class="cp-progress-bar">
                <em :style="{ width: uploadPercent + '%' }"></em>
              </div>
              <span class="cp-progress-text">{{ uploadPercent }}%</span>
            </div>

            <div class="cp-warn">
              ⚠ <b>上传证据</b>有助于客服快速核实处理。没有证据的客诉可能无法有效处理。
            </div>
          </div>
        </div>

        <footer class="cp-foot">
          <button class="btn-ghost" @click="close">取消</button>
          <button
            class="btn-solid"
            :disabled="submitDisabled"
            @click="submit"
          >
            {{ submitText }}
          </button>
        </footer>
      </div>
    </div>
  </Teleport>
</template>

<script>
import request from '@/utils/request'
import { useCos } from '@/composables/useCos'   

export default {
  name: 'ComplaintModal',
  props: {
    visible: { type: Boolean, default: false },
    order: { type: Object, default: null }
  },
  emits: ['close', 'success'],

  setup() {
    const { uploadFile, isUploading, progress } = useCos()
    return { uploadFile, isUploading, progress }
  },

  data() {
    return {
      file: null,
      previewUrl: '',
      fileName: '',
      submitting: false,
      uploadPercent: 0,

      form: {
        targetOperatorId: null,
        complaintType: 'other',
        description: ''
      },

      TYPES: [
        { value: 'attitude',   label: '服务态度差', icon: '😤' },
        { value: 'late',       label: '迟到/缺席',   icon: '⏰' },
        { value: 'runner',     label: '打手跑路',   icon: '🏃' },
        { value: 'quality',    label: '服务质量差', icon: '📉' },
        { value: 'harassment', label: '辱骂/骚扰',   icon: '🚫' },
        { value: 'other',      label: '其他',        icon: '❓' }
      ]
    }
  },

  computed: {
    uploading() {
      return this.isUploading ? this.isUploading.value : false
    },

    submitDisabled() {
      if (this.submitting || this.uploading) return true
      if (!this.form.complaintType) return true
      if (!this.form.description.trim()) return true
      return false
    },

    submitText() {
      if (this.submitting) return '提交中…'
      if (this.uploading) return '上传中…'
      return '提交客诉'
    }
  },

  watch: {
    visible(v) {
      if (v) this.reset()
    }
  },

  methods: {
    reset() {
      this.file = null
      this.previewUrl = ''
      this.fileName = ''
      this.submitting = false
      this.uploadPercent = 0
      this.form = {
        targetOperatorId: null,
        complaintType: 'other',
        description: ''
      }
    },

    pickFile() {
      this.$refs.fileInput?.click()
    },

    onFileChange(e) {
      const f = e.target.files?.[0]
      if (!f) return

      if (f.size > 5 * 1024 * 1024) {
        alert('图片不能超过 5MB')
        e.target.value = ''
        return
      }

      const allowed = ['image/jpeg', 'image/png', 'image/webp']
      if (!allowed.includes(f.type)) {
        alert('只支持 jpg / png / webp 格式')
        e.target.value = ''
        return
      }

      this.file = f
      this.fileName = f.name

      const reader = new FileReader()
      reader.onload = (ev) => {
        this.previewUrl = ev.target.result
      }
      reader.readAsDataURL(f)
    },

    removeFile() {
      this.file = null
      this.previewUrl = ''
      this.fileName = ''
      if (this.$refs.fileInput) this.$refs.fileInput.value = ''
    },

    async submit() {
      if (this.submitDisabled) return
      if (!this.order?.orderNo) return

      if (!this.form.description.trim()) {
        alert('请填写详细描述')
        return
      }

      this.submitting = true
      try {
        // 1. 上传证据（如果选了）
        let evidenceUrl = null
        if (this.file) {
          const result = await this.uploadFile(this.file, 'club/complaints')
          evidenceUrl = result.url
        }

        // 2. 提交客诉
        await request.post('/club/complaints', {
          orderNo: this.order.orderNo,
          targetOperatorId: this.form.targetOperatorId,
          complaintType: this.form.complaintType,
          description: this.form.description.trim(),
          evidenceUrl
        })

        this.$emit('success')
        this.close()
      } catch (e) {
        alert(e?.response?.data?.message || e.message || '提交失败')
      } finally {
        this.submitting = false
      }
    },

    close() {
      if (this.submitting || this.uploading) return
      this.$emit('close')
    }
  }
}
</script>

<style scoped>
.cp-mask {
  position: fixed; inset: 0; z-index: 9999;
  background: rgba(0, 0, 0, 0.78);
  backdrop-filter: blur(8px);
  display: grid; place-items: center;
  padding: 24px;
}
.cp-modal {
  width: min(560px, 100%);
  max-height: 90vh; overflow-y: auto;
  border: 1px solid rgba(255, 255, 255, 0.15);
  background: #0e1720; color: #e8eef4;
  border-radius: 4px;
  box-shadow: 0 24px 80px rgba(0, 0, 0, 0.6);
}
.cp-head {
  display: flex; justify-content: space-between; align-items: flex-start;
  gap: 16px;
  padding: 20px 24px 16px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}
.cp-head h3 {
  margin: 0 0 6px;
  font-size: 17px; font-weight: 700;
  letter-spacing: 1px; color: #f2f6fa;
}
.cp-sub {
  margin: 0;
  font-family: monospace; font-size: 11px;
  color: #8b9aab;
}
.close-icon {
  background: none; border: none;
  font-size: 24px; color: #6b7a8a;
  cursor: pointer; line-height: 1;
}
.close-icon:hover { color: #e8eef4; }

.cp-body {
  padding: 20px 24px;
  display: flex; flex-direction: column; gap: 20px;
}
.cp-section { display: flex; flex-direction: column; gap: 10px; }
.cp-label {
  font-family: monospace; font-size: 10px;
  letter-spacing: 2px; color: #ff6b1a;
}
.cp-label em { font-style: normal; color: #ff5050; margin-left: 3px; }

/* 投诉对象 */
.target-list {
  display: flex; flex-direction: column; gap: 8px;
}
.target-item {
  display: flex; align-items: center; gap: 12px;
  padding: 12px 14px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  background: rgba(255, 255, 255, 0.02);
  border-radius: 3px;
  cursor: pointer; transition: all 0.2s;
}
.target-item:hover { border-color: rgba(255, 255, 255, 0.2); }
.target-item.on {
  border-color: rgba(255, 107, 26, 0.5);
  background: rgba(255, 107, 26, 0.06);
}
.target-item input { display: none; }
.ti-icon { font-size: 20px; line-height: 1; }
.ti-info { min-width: 0; }
.ti-name {
  font-size: 13px; font-weight: 700; color: #f2f6fa;
  margin-bottom: 3px;
}
.ti-desc { font-size: 11px; color: #8b9aab; }

/* 投诉类型 */
.type-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
}
.type-item {
  display: flex; flex-direction: column; align-items: center;
  gap: 6px;
  padding: 12px 8px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  background: rgba(255, 255, 255, 0.02);
  border-radius: 3px;
  cursor: pointer; transition: all 0.2s;
}
.type-item:hover { border-color: rgba(255, 255, 255, 0.2); }
.type-item.on {
  border-color: rgba(255, 107, 26, 0.5);
  background: rgba(255, 107, 26, 0.06);
}
.type-item input { display: none; }
.type-icon { font-size: 20px; line-height: 1; }
.type-label {
  font-size: 11px; color: #c0cbd6;
  text-align: center; line-height: 1.4;
}
.type-item.on .type-label { color: #ff6b1a; }

/* 描述 */
.cp-textarea {
  width: 100%; padding: 10px 12px;
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.1);
  color: #e8eef4;
  border-radius: 3px;
  font-family: inherit; font-size: 13px;
  outline: none; resize: vertical;
}
.cp-textarea:focus { border-color: rgba(255, 107, 26, 0.5); }
.cp-len {
  font-family: monospace; font-size: 10px;
  color: #6b7a8a; text-align: right;
}

/* 上传 */
.cp-upload-area { position: relative; }
.cp-file-input { display: none; }
.cp-upload-hint {
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  gap: 6px;
  padding: 24px 20px;
  border: 1px dashed rgba(255, 255, 255, 0.2);
  background: rgba(255, 255, 255, 0.02);
  border-radius: 4px;
  cursor: pointer; transition: all 0.2s;
}
.cp-upload-hint:hover {
  border-color: rgba(255, 107, 26, 0.5);
  background: rgba(255, 107, 26, 0.04);
}
.cp-upload-icon { font-size: 24px; line-height: 1; }
.cp-upload-text {
  font-size: 13px; font-weight: 600; color: #f2f6fa;
}
.cp-upload-desc {
  font-family: monospace; font-size: 10px; color: #6b7a8a;
}
.cp-preview {
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 4px;
  overflow: hidden;
  background: rgba(0, 0, 0, 0.2);
}
.cp-preview img {
  display: block;
  width: 100%; max-height: 240px;
  object-fit: contain;
  background: #000;
}
.cp-preview-actions {
  display: flex; align-items: center; justify-content: space-between;
  gap: 12px;
  padding: 8px 12px;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
}
.cp-preview-name {
  font-family: monospace; font-size: 11px;
  color: #8b9aab;
  overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
  min-width: 0;
}
.cp-remove-btn {
  flex-shrink: 0;
  padding: 4px 10px;
  font-size: 11px;
  color: #ff5050;
  background: transparent;
  border: 1px solid rgba(255, 80, 80, 0.4);
  border-radius: 3px;
  cursor: pointer;
}
.cp-remove-btn:hover { background: rgba(255, 80, 80, 0.1); }

.cp-progress {
  display: flex; align-items: center; gap: 10px;
}
.cp-progress-bar {
  flex: 1; height: 4px;
  background: rgba(255, 255, 255, 0.08);
  border-radius: 2px; overflow: hidden;
}
.cp-progress-bar em {
  display: block; height: 100%;
  background: linear-gradient(90deg, rgba(120, 215, 206, 0.4), #78d7ce);
  transition: width 0.2s;
}
.cp-progress-text {
  font-family: monospace; font-size: 11px;
  color: #78d7ce;
}

.cp-warn {
  padding: 10px 12px;
  border-left: 3px solid #ff8c4a;
  background: rgba(255, 140, 74, 0.06);
  border-radius: 0 3px 3px 0;
  font-size: 11px;
  line-height: 1.7;
  color: #c0cbd6;
}
.cp-warn b { color: #ff8c4a; }

.cp-foot {
  display: flex; gap: 12px;
  padding: 16px 24px;
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
.btn-ghost:hover { color: #e8eef4; border-color: rgba(255, 255, 255, 0.3); }
.btn-solid {
  color: #fff; background: #ff6b1a;
  border: 1px solid #ff6b1a;
}
.btn-solid:hover:not(:disabled) { background: #ff7e32; }
.btn-solid:disabled { opacity: 0.5; cursor: not-allowed; }

@media (max-width: 520px) {
  .type-grid { grid-template-columns: repeat(2, 1fr); }
}
</style>