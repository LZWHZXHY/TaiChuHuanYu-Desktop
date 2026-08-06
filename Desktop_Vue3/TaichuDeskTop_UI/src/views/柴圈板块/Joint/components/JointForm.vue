<template>
  <form class="joint-form" @submit.prevent="handleSubmit">
    <!-- ===== 基本信息 ===== -->
    <div class="form-section">
      <h3 class="section-label">基本信息</h3>

      <div class="form-group">
        <label>活动名称 <span class="required">*</span></label>
        <input v-model="form.title" type="text" placeholder="给联合活动取个名字" required />
      </div>

      <div class="form-group">
        <label>活动描述 <span class="required">*</span></label>
        <textarea v-model="form.description" rows="5" placeholder="详细描述这次联合活动的内容、主题、目标..." required />
      </div>

      <div class="form-group">
        <label>封面图</label>
        <div class="upload-area">
          <div v-if="form.coverUrl" class="upload-preview">
            <img :src="form.coverUrl" alt="封面图" />
            <button type="button" class="remove-image" @click="form.coverUrl = ''">×</button>
          </div>
          <div v-else class="upload-placeholder" @click="uploadCover">
            <span>📷 点击上传封面图</span>
            <span class="hint">建议尺寸：1200×630 以上</span>
          </div>
          <div v-if="uploadingCover" class="uploading-status">上传中... {{ coverProgress }}%</div>
        </div>
      </div>
    </div>

    <!-- ===== 活动设置 ===== -->
    <div class="form-section">
      <h3 class="section-label">活动设置</h3>

      <div class="form-row">
        <div class="form-group">
          <label>活动类型 <span class="required">*</span></label>
          <select v-model="form.type">
            <option value="joint">联合</option>
            <option value="relay">接力</option>
            <option value="project">企划</option>
            <option value="free">自由</option>
            <option value="other">其他</option>
          </select>
        </div>
        <div class="form-group">
          <label>活动状态 <span class="required">*</span></label>
          <select v-model="form.status">
            <option value="open">报名中</option>
            <option value="closed">已截止</option>
            <option value="ended">已结束</option>
            <option value="banned">已封禁</option>
            <option value="abandoned">暴毙</option>
          </select>
        </div>
      </div>

      <!-- ===== 来源类型（仅管理员可见） ===== -->
      <div v-if="canCreateOfficial" class="form-group">
        <label>活动来源 <span class="required">*</span></label>
        <select v-model="form.organizerType">
          <option value="user">用户自建（需管理员审核）</option>
          <option value="official">太虚绘院官方（直接发布）</option>
        </select>
        <p class="hint">普通用户只能创建用户自建活动，管理员可创建官方活动</p>
      </div>

      <div class="form-group">
        <label class="checkbox-label">
          <input type="checkbox" v-model="form.auditRequired" />
          <span>报名需要审核</span>
        </label>
        <p class="hint">开启后，参与者报名需要举办者审核通过才能加入</p>
      </div>
    </div>

    <!-- ===== 其他信息 ===== -->
    <div class="form-section">
      <h3 class="section-label">其他信息</h3>

      <div class="form-group">
        <label>群聊号 / 联系方式</label>
        <input v-model="form.contact" type="text" placeholder="QQ群号、微信群、Discord 等" />
        <p class="hint">参与者可以通过此方式联系举办者</p>
      </div>

      <div class="form-group">
        <label>参与要求</label>
        <textarea v-model="form.requirements" rows="3" placeholder="参与门槛、需要具备的技能、注意事项等" />
      </div>
    </div>

    <!-- ===== 按钮 ===== -->
    <div class="form-actions">
      <button type="button" class="btn-line" @click="emit('cancel')">取消</button>
      <button type="submit" class="btn-line btn-submit" :disabled="submitting || uploadingCover">
        {{ submitting ? '保存中...' : submitLabel }}
      </button>
    </div>
  </form>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { useCos } from '@/composables/useCos'
import { useUserStore } from '@/stores/user'
import type { JointStatus, JointType, JointActivity } from '../joint'

const props = defineProps<{
  initialData?: JointActivity
  submitLabel?: string
}>()

const emit = defineEmits<{
  submit: [data: any]
  cancel: []
}>()

const { uploadFile } = useCos()
const userStore = useUserStore()

const submitting = ref(false)
const uploadingCover = ref(false)
const coverProgress = ref(0)

// ===== 权限判断 =====
const canCreateOfficial = computed(() => {
  return userStore.userInfo?.permissions?.some(p => ['SuperAdmin', 'JointManager'].includes(p)) ?? false
})

// ===== 表单数据 =====
const form = reactive({
  title: '',
  description: '',
  coverUrl: '',
  type: 'joint' as JointType,
  status: 'open' as JointStatus,
  auditRequired: true,
  contact: '',
  requirements: '',
  organizerType: 'user' as 'user' | 'official',
})

// ===== 监听初始数据变化 =====
watch(
  () => props.initialData,
  (data) => {
    if (data) {
      form.title = data.title || ''
      form.description = data.description || ''
      form.coverUrl = data.coverUrl || ''
      form.type = data.type || 'joint'
      form.status = data.status || 'open'
      form.auditRequired = data.auditRequired ?? true
      form.contact = data.contact || ''
      form.requirements = data.requirements || ''
      form.organizerType = data.organizerType || 'user'
    }
  },
  { immediate: true }
)

// ===== 上传封面图 =====
async function uploadCover() {
  const input = document.createElement('input')
  input.type = 'file'
  input.accept = 'image/*'

  input.onchange = async (e: Event) => {
    const target = e.target as HTMLInputElement
    const file = target.files?.[0]
    if (!file) return

    uploadingCover.value = true
    coverProgress.value = 0

    try {
      const result = await uploadFile(file, 'joint/cover')
      form.coverUrl = result.url
    } catch (error) {
      console.error('封面上传失败:', error)
      alert('封面上传失败，请重试')
    } finally {
      uploadingCover.value = false
      coverProgress.value = 0
      target.value = ''
    }
  }

  input.click()
}

// ===== 提交 =====
function handleSubmit() {
  if (!form.title.trim()) {
    alert('请输入活动名称')
    return
  }
  if (!form.description.trim()) {
    alert('请输入活动描述')
    return
  }

  // 如果用户没有权限，强制设为 user（防止伪造请求）
  const organizerType = canCreateOfficial.value ? form.organizerType : 'user'

  const data = {
    title: form.title.trim(),
    description: form.description.trim(),
    coverUrl: form.coverUrl.trim() || undefined,
    type: form.type,
    status: form.status,
    auditRequired: form.auditRequired,
    contact: form.contact.trim() || undefined,
    requirements: form.requirements.trim() || undefined,
    organizerType: organizerType,
  }

  emit('submit', data)
}
</script>

<style scoped>
/* 样式保持不变，与之前一致 */
.joint-form {
  display: flex;
  flex-direction: column;
  gap: 32px;
}

.form-section {
  border-bottom: 1px solid var(--line-raw);
  padding-bottom: 24px;
}

.form-section:last-of-type {
  border-bottom: none;
  padding-bottom: 0;
}

.section-label {
  font-size: 16px;
  font-weight: 400;
  letter-spacing: 0.2em;
  margin: 0 0 18px 0;
  color: var(--ink-black);
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.form-group {
  margin-bottom: 16px;
}

.form-group:last-child {
  margin-bottom: 0;
}

.form-group label {
  display: block;
  font-size: 13px;
  letter-spacing: 0.15em;
  margin-bottom: 4px;
  color: var(--ink-black);
}

.required {
  color: var(--cinnabar);
}

.form-group input,
.form-group select,
.form-group textarea {
  width: 100%;
  padding: 8px 14px;
  border: 1px solid var(--line-raw);
  background: var(--paper-card);
  color: var(--ink-black);
  font-family: var(--font-family);
  font-size: 14px;
  letter-spacing: 0.08em;
  transition: border-color 0.3s;
  outline: none;
}

.form-group input:focus,
.form-group select:focus,
.form-group textarea:focus {
  border-color: var(--ink-black);
}

.form-group textarea {
  resize: vertical;
  min-height: 60px;
}

.hint {
  font-size: 12px;
  color: var(--ink-light);
  letter-spacing: 0.1em;
  margin: 4px 0 0 0;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  font-size: 13px;
  letter-spacing: 0.15em;
  color: var(--ink-black);
}

.checkbox-label input[type="checkbox"] {
  width: 16px;
  height: 16px;
  accent-color: var(--ink-black);
  cursor: pointer;
}

/* 上传区域 */
.upload-area {
  border: 1px dashed var(--line-raw);
  padding: 16px;
  border-radius: 4px;
  background: var(--paper-sub);
  min-height: 120px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
}

.upload-preview {
  position: relative;
  width: 100%;
  max-width: 300px;
}

.upload-preview img {
  width: 100%;
  aspect-ratio: 16/9;
  object-fit: cover;
  border-radius: 4px;
  border: 1px solid var(--line-raw);
}

.upload-preview .remove-image {
  position: absolute;
  top: -8px;
  right: -8px;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  border: none;
  background: rgba(44, 42, 41, 0.8);
  color: #fff;
  cursor: pointer;
  font-size: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.upload-preview .remove-image:hover {
  background: var(--cinnabar);
}

.upload-placeholder {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  cursor: pointer;
  color: var(--ink-gray);
  padding: 16px;
}

.upload-placeholder:hover {
  color: var(--ink-black);
}

.upload-placeholder .hint {
  font-size: 12px;
  color: var(--ink-light);
  margin: 0;
}

.uploading-status {
  font-size: 13px;
  color: var(--ink-gray);
  letter-spacing: 0.1em;
  margin-top: 8px;
}

/* 按钮 */
.form-actions {
  display: flex;
  gap: 12px;
  padding-top: 20px;
  border-top: 1px solid var(--line-raw);
}

.btn-line {
  background: none;
  border: 1px solid var(--line-raw);
  color: var(--ink-black);
  padding: 10px 24px;
  font-family: var(--font-family);
  font-size: 14px;
  letter-spacing: 0.15em;
  cursor: pointer;
  transition: all 0.3s ease;
}

.btn-line:hover {
  border-color: var(--cinnabar);
  color: var(--cinnabar);
}

.btn-submit {
  flex: 1;
  border-color: var(--ink-black);
  background: var(--ink-black);
  color: var(--paper-card);
}

.btn-submit:hover {
  background: var(--paper-card);
  color: var(--ink-black);
}

.btn-submit:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-submit:disabled:hover {
  background: var(--ink-black);
  color: var(--paper-card);
}

@media (max-width: 600px) {
  .form-row {
    grid-template-columns: 1fr;
  }

  .form-actions {
    flex-direction: column-reverse;
  }

  .upload-preview {
    max-width: 100%;
  }
}
</style>