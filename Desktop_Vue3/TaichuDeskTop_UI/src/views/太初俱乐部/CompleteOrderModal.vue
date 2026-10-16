<template>
  <Teleport to="body">
    <div v-if="visible" class="cm-mask" @click.self="close">
      <div class="cm-modal">
        <header class="cm-head">
          <div>
            <h3>{{ isUploadMode ? '上传结算截图' : '完成订单' }}</h3>
            <p class="cm-sub">
              {{ order?.orderNo }} · {{ order?.gameName }} · {{ order?.orderTypeName }}
            </p>
          </div>
          <button class="close-icon" @click="close">×</button>
        </header>

        <div class="cm-body">
          <div class="cm-section">
            <div class="cm-label">
              {{ isUploadMode ? '结算截图' : '结算截图（可选）' }}
            </div>

            <div v-if="!previewUrl" class="cm-upload-area">
              <input
                ref="fileInput"
                type="file"
                accept="image/jpeg,image/png,image/webp"
                class="cm-file-input"
                @change="onFileChange"
              />
              <div class="cm-upload-hint" @click="pickFile">
                <div class="cm-upload-icon">📎</div>
                <div class="cm-upload-text">
                  {{ isUploadMode ? '点击选择结算截图' : '点击上传结算截图' }}
                </div>
                <div class="cm-upload-desc">支持 jpg / png / webp，单张不超过 5MB</div>
              </div>
            </div>

            <div v-else class="cm-preview">
              <img :src="previewUrl" alt="结算截图" />
              <div class="cm-preview-actions">
                <span class="cm-preview-name">{{ fileName }}</span>
                <button class="cm-remove-btn" @click="removeFile">移除</button>
              </div>
            </div>

            <div v-if="uploading" class="cm-progress">
              <div class="cm-progress-bar">
                <em :style="{ width: uploadPercent + '%' }"></em>
              </div>
              <span class="cm-progress-text">{{ uploadPercent }}%</span>
            </div>

            <div class="cm-warn">
              <div class="cm-warn-title">⚠ 重要提示</div>
              <div class="cm-warn-body">
                · <b>上传结算截图</b>：享平台<b>纠纷处理保障</b>，如遇问题可凭截图申诉<br />
                · <b>不上传直接结单</b>：后续如出现问题，平台<b>不承担纠纷处理责任</b>
              </div>
            </div>
          </div>

          <div v-if="!isUploadMode" class="cm-section">
            <div class="cm-label">结单备注（可选）</div>
            <textarea
              v-model="remark"
              class="cm-textarea"
              rows="2"
              placeholder="例如：本次服务已结束，感谢打手"
              maxlength="200"
            ></textarea>
          </div>
        </div>

        <footer class="cm-foot">
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
import { useCos } from '@/composables/useCos'   // ⚠️ 按你实际路径改

export default {
  name: 'CompleteOrderModal',
  props: {
    visible: { type: Boolean, default: false },
    order: { type: Object, default: null },
    // ⭐ 关键：声明 mode，否则 Vue 报 "Extraneous non-props attributes" 警告
    mode: { type: String, default: 'complete' }
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
      remark: '',
      submitting: false,
      uploadPercent: 0
    }
  },

  computed: {
    uploading() {
      return this.isUploading ? this.isUploading.value : false
    },

    isUploadMode() {
      return this.mode === 'upload'
    },

    submitDisabled() {
      if (this.submitting || this.uploading) return true
      if (this.isUploadMode && !this.file) return true
      return false
    },

    submitText() {
      if (this.submitting) return '提交中…'
      if (this.uploading) return '上传中…'
      return this.isUploadMode ? '确认上传' : '确认完成订单'
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
      this.remark = ''
      this.submitting = false
      this.uploadPercent = 0
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

      this.submitting = true
      try {
        // ① 上传图片到 COS（如果选了文件）
        let settlementProofUrl = null
        if (this.file) {
          const result = await this.uploadFile(
            this.file,
            'club/settlement-proofs'
          )
          settlementProofUrl = result.url
        }

        // ② 根据 mode 调不同接口
        if (this.isUploadMode) {
          await request.post(
            `/club/orders/${this.order.orderNo}/settlement-proof`,
            { settlementProofUrl }
          )
        } else {
          await request.post(
            `/club/orders/${this.order.orderNo}/complete`,
            {
              settlementProofUrl,
              remark: this.remark.trim() || null
            }
          )
        }

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
.cm-mask {
  position: fixed; inset: 0; z-index: 9999;
  background: rgba(0, 0, 0, 0.78);
  backdrop-filter: blur(8px);
  display: grid; place-items: center;
  padding: 24px;
}
.cm-modal {
  width: min(520px, 100%);
  max-height: 90vh; overflow-y: auto;
  border: 1px solid rgba(255, 255, 255, 0.15);
  background: #0e1720; color: #e8eef4;
  border-radius: 4px;
  box-shadow: 0 24px 80px rgba(0, 0, 0, 0.6);
}
.cm-head {
  display: flex; justify-content: space-between; align-items: flex-start;
  gap: 16px;
  padding: 20px 24px 16px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}
.cm-head h3 {
  margin: 0 0 6px;
  font-size: 17px; font-weight: 700;
  letter-spacing: 1px; color: #f2f6fa;
}
.cm-sub {
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
.cm-body {
  padding: 20px 24px;
  display: flex; flex-direction: column; gap: 20px;
}
.cm-section { display: flex; flex-direction: column; gap: 10px; }
.cm-label {
  font-family: monospace; font-size: 10px;
  letter-spacing: 2px; color: #ff6b1a;
}
.cm-upload-area { position: relative; }
.cm-file-input { display: none; }
.cm-upload-hint {
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  gap: 8px;
  padding: 28px 20px;
  border: 1px dashed rgba(255, 255, 255, 0.2);
  background: rgba(255, 255, 255, 0.02);
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
}
.cm-upload-hint:hover {
  border-color: rgba(255, 107, 26, 0.5);
  background: rgba(255, 107, 26, 0.04);
}
.cm-upload-icon { font-size: 26px; line-height: 1; }
.cm-upload-text {
  font-size: 13px; font-weight: 600; color: #f2f6fa;
}
.cm-upload-desc {
  font-family: monospace; font-size: 10px;
  color: #6b7a8a;
}
.cm-preview {
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 4px;
  overflow: hidden;
  background: rgba(0, 0, 0, 0.2);
}
.cm-preview img {
  display: block;
  width: 100%;
  max-height: 240px;
  object-fit: contain;
  background: #000;
}
.cm-preview-actions {
  display: flex; align-items: center; justify-content: space-between;
  gap: 12px;
  padding: 8px 12px;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
}
.cm-preview-name {
  font-family: monospace; font-size: 11px;
  color: #8b9aab;
  overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
  min-width: 0;
}
.cm-remove-btn {
  flex-shrink: 0;
  padding: 4px 10px;
  font-size: 11px;
  color: #ff5050;
  background: transparent;
  border: 1px solid rgba(255, 80, 80, 0.4);
  border-radius: 3px;
  cursor: pointer;
}
.cm-remove-btn:hover {
  background: rgba(255, 80, 80, 0.1);
}
.cm-progress {
  display: flex; align-items: center; gap: 10px;
}
.cm-progress-bar {
  flex: 1;
  height: 4px;
  background: rgba(255, 255, 255, 0.08);
  border-radius: 2px;
  overflow: hidden;
}
.cm-progress-bar em {
  display: block;
  height: 100%;
  background: linear-gradient(90deg, rgba(120, 215, 206, 0.4), #78d7ce);
  transition: width 0.2s;
}
.cm-progress-text {
  font-family: monospace; font-size: 11px;
  color: #78d7ce;
}
.cm-warn {
  padding: 12px 14px;
  border-left: 3px solid #ff8c4a;
  background: rgba(255, 140, 74, 0.06);
  border-radius: 0 3px 3px 0;
}
.cm-warn-title {
  font-size: 12px;
  font-weight: 700;
  color: #ff8c4a;
  margin-bottom: 6px;
}
.cm-warn-body {
  font-size: 12px;
  line-height: 1.8;
  color: #c0cbd6;
}
.cm-warn-body b { color: #ff8c4a; }
.cm-textarea {
  width: 100%;
  padding: 10px 12px;
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.1);
  color: #e8eef4;
  border-radius: 3px;
  font-family: inherit;
  font-size: 13px;
  outline: none;
  resize: vertical;
}
.cm-textarea:focus { border-color: rgba(255, 107, 26, 0.5); }
.cm-foot {
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
</style>