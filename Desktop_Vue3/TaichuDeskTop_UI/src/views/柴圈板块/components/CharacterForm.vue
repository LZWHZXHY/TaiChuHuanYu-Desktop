<template>
  <form class="character-form" @submit.prevent="handleSubmit">
    <!-- ===== 基本信息 ===== -->
    <div class="form-section">
      <h3 class="section-label">基本信息</h3>

      <div class="form-group">
        <label>角色标题 <span class="required">*</span></label>
        <input v-model="form.title" placeholder="给角色取个名字" required />
      </div>

      <div class="form-group">
        <label>一句话简介</label>
        <input v-model="form.description" placeholder="简短介绍这个角色" />
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
            <span class="hint">建议尺寸：400×400 以上</span>
          </div>
          <div v-if="uploadingCover" class="uploading-status">上传中... {{ coverProgress }}%</div>
        </div>
      </div>
    </div>

    <!-- ===== 自定义属性 ===== -->
    <div class="form-section">
      <div class="section-header-inline">
        <h3 class="section-label">角色属性</h3>
        <button type="button" class="btn-line btn-sm" @click="addAttribute">＋ 添加属性</button>
      </div>
      <p class="hint">自由定义角色的各项属性，支持短文本（如：性别、年龄）和长文本（如：家族历史、背景故事）</p>

      <div
        v-for="(attr, index) in form.attributes"
        :key="index"
        class="attr-row"
      >
        <div class="attr-key">
          <input v-model="attr.key" placeholder="属性名（如：性别、家族历史）" />
        </div>

        <div class="attr-type">
          <select v-model="attr.type">
            <option value="short">短文本</option>
            <option value="long">长文本</option>
          </select>
        </div>

        <div class="attr-value">
          <input
            v-if="attr.type === 'short'"
            v-model="attr.value"
            placeholder="属性值（短文本）"
          />
          <textarea
            v-else
            v-model="attr.value"
            rows="4"
            placeholder="详细内容（长文本）"
          />
        </div>

        <button type="button" class="btn-remove" @click="removeAttribute(index)">×</button>
      </div>

      <div v-if="!form.attributes.length" class="empty-hint">
        还没有属性，点击「添加属性」开始定义
      </div>
    </div>

    <!-- ===== 图库 ===== -->
    <div class="form-section">
      <div class="section-header-inline">
        <h3 class="section-label">图库</h3>
        <button type="button" class="btn-line btn-sm" @click="uploadGalleryImage">＋ 上传图片</button>
      </div>
      <p class="hint">角色的其他展示图片</p>

      <div class="gallery-grid-form">
        <div
          v-for="(img, index) in form.images"
          :key="index"
          class="gallery-item-form"
        >
          <img :src="img.url" :alt="img.alt || '图库图片'" />
          <div class="gallery-item-overlay">
            <input v-model="img.alt" placeholder="描述（可选）" class="gallery-alt-input" />
            <button type="button" class="btn-remove-small" @click="removeImage(index)">×</button>
          </div>
        </div>

        <div v-if="uploadingGallery" class="gallery-uploading">
          <div class="spinner-small"></div>
          <span>上传中... {{ galleryProgress }}%</span>
        </div>
      </div>

      <div v-if="!form.images.length && !uploadingGallery" class="empty-hint">
        还没有图库图片，点击「上传图片」添加
      </div>
    </div>

    <!-- ===== 发布设置 ===== -->
    <div class="form-section">
      <h3 class="section-label">发布设置</h3>

      <div class="form-group">
        <label>状态</label>
        <select v-model="form.status">
          <option value="draft">存为草稿</option>
          <option value="published">直接发布</option>
        </select>
      </div>
    </div>

    <!-- ===== 按钮 ===== -->
    <div class="form-actions">
      <button type="button" class="btn-line" @click="router.back()">取消</button>
      <button type="submit" class="btn-line btn-submit" :disabled="loading || uploadingCover || uploadingGallery">
        {{ loading ? '保存中...' : '保存角色' }}
      </button>
    </div>
  </form>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useCos } from '@/composables/useCos'

// ===== 类型定义 =====
interface AttributeInput {
  key: string
  value: string
  type: 'short' | 'long'
}

interface ImageInput {
  url: string
  alt: string
}

interface FormData {
  title: string
  description: string
  coverUrl: string
  status: 'draft' | 'published'
  attributes: AttributeInput[]
  images: ImageInput[]
}

interface InitialData {
  id?: string
  title: string
  description?: string
  coverUrl?: string
  status: 'draft' | 'published'
  attributes?: {
    id?: string
    key: string
    value: string
    sortOrder: number
    type: 'short' | 'long'
  }[]
  images?: { id?: string; url: string; alt?: string; sortOrder: number }[]
}

const props = defineProps<{
  initialData?: InitialData
}>()

const emit = defineEmits<{
  submit: [data: any]
}>()

const router = useRouter()
const loading = ref(false)
const { uploadFile } = useCos()

// ===== 上传状态 =====
const uploadingCover = ref(false)
const coverProgress = ref(0)
const uploadingGallery = ref(false)
const galleryProgress = ref(0)

const form = reactive<FormData>({
  title: '',
  description: '',
  coverUrl: '',
  status: 'draft',
  attributes: [],
  images: [],
})

onMounted(() => {
  if (props.initialData) {
    form.title = props.initialData.title || ''
    form.description = props.initialData.description || ''
    form.coverUrl = props.initialData.coverUrl || ''
    form.status = props.initialData.status || 'draft'

    if (props.initialData.attributes?.length) {
      form.attributes = props.initialData.attributes.map(a => ({
        key: a.key,
        value: a.value || '',
        type: a.type || 'short'
      }))
    }

    if (props.initialData.images?.length) {
      form.images = props.initialData.images.map(i => ({
        url: i.url,
        alt: i.alt || '',
      }))
    }
  }
})

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
      const result = await uploadFile(file, 'stickman/cover')
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

// ===== 上传图库图片 =====
async function uploadGalleryImage() {
  const input = document.createElement('input')
  input.type = 'file'
  input.accept = 'image/*'
  input.multiple = true

  input.onchange = async (e: Event) => {
    const target = e.target as HTMLInputElement
    const files = target.files
    if (!files || files.length === 0) return

    uploadingGallery.value = true
    galleryProgress.value = 0

    try {
      for (let i = 0; i < files.length; i++) {
        const file = files[i]
        const result = await uploadFile(file, 'stickman/gallery')
        form.images.push({
          url: result.url,
          alt: ''
        })
        galleryProgress.value = Math.round(((i + 1) / files.length) * 100)
      }
    } catch (error) {
      console.error('图库上传失败:', error)
      alert('部分图片上传失败，请重试')
    } finally {
      uploadingGallery.value = false
      galleryProgress.value = 0
      target.value = ''
    }
  }

  input.click()
}

// ===== 操作函数 =====
function addAttribute() {
  form.attributes.push({ key: '', value: '', type: 'short' })
}

function removeAttribute(index: number) {
  form.attributes.splice(index, 1)
}

function removeImage(index: number) {
  form.images.splice(index, 1)
}

function handleSubmit() {
  if (!form.title.trim()) {
    alert('请输入角色标题')
    return
  }

  // 检查重复 Key
  const keys = form.attributes.map(a => a.key.trim()).filter(k => k)
  const duplicateKeys = keys.filter((k, i) => keys.indexOf(k) !== i)
  if (duplicateKeys.length) {
    alert(`属性名重复：${duplicateKeys.join('、')}，请修改后重试`)
    return
  }

  const validAttributes = form.attributes
    .filter(a => a.key.trim() && a.value !== undefined)
    .map((a, index) => ({
      key: a.key.trim(),
      value: a.value || '',
      sortOrder: index,
      type: a.type || 'short'
    }))

  const validImages = form.images
    .filter(i => i.url.trim())
    .map((i, index) => ({
      url: i.url.trim(),
      alt: i.alt || '',
      sortOrder: index,
    }))

  const submitData = {
    title: form.title.trim(),
    description: form.description.trim() || undefined,
    coverUrl: form.coverUrl.trim() || undefined,
    status: form.status,
    attributes: validAttributes.length ? validAttributes : undefined,
    images: validImages.length ? validImages : undefined,
  }

  emit('submit', submitData)
}
</script>

<style scoped>
.character-form {
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

.section-header-inline {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 4px;
}

.section-label {
  font-size: 16px;
  font-weight: 400;
  letter-spacing: 0.2em;
  margin: 0;
  color: var(--ink-black);
}

.hint {
  font-size: 12px;
  color: var(--ink-light);
  letter-spacing: 0.1em;
  margin: 4px 0 16px 0;
}

.empty-hint {
  font-size: 13px;
  color: var(--ink-light);
  letter-spacing: 0.1em;
  padding: 16px 0 8px 0;
  text-align: center;
}

.btn-sm {
  padding: 4px 14px;
  font-size: 12px;
}

.btn-line {
  background: none;
  border: 1px solid var(--line-raw);
  color: var(--ink-black);
  padding: 6px 16px;
  font-family: var(--font-family);
  font-size: 13px;
  letter-spacing: 0.15em;
  cursor: pointer;
  transition: all 0.3s ease;
}

.btn-line:hover {
  border-color: var(--cinnabar);
  color: var(--cinnabar);
}

.form-group {
  margin-bottom: 14px;
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

/* ===== 上传区域 ===== */
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
  max-width: 200px;
}

.upload-preview img {
  width: 100%;
  aspect-ratio: 1/1;
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

/* ===== 属性行 ===== */
.attr-row {
  display: grid;
  grid-template-columns: 1.2fr 0.8fr 2fr auto;
  gap: 10px;
  align-items: start;
  margin-bottom: 8px;
  padding: 4px 0;
}

.attr-key input,
.attr-type select,
.attr-value input,
.attr-value textarea {
  width: 100%;
  padding: 6px 12px;
  border: 1px solid var(--line-raw);
  background: var(--paper-card);
  color: var(--ink-black);
  font-family: var(--font-family);
  font-size: 13px;
  outline: none;
  transition: border-color 0.3s;
}

.attr-key input:focus,
.attr-type select:focus,
.attr-value input:focus,
.attr-value textarea:focus {
  border-color: var(--ink-black);
}

.attr-value textarea {
  resize: vertical;
  min-height: 60px;
}

.attr-type select {
  appearance: none;
  cursor: pointer;
  padding-right: 24px;
  background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 12 12'%3E%3Cpath fill='%237A7571' d='M6 8L1 3h10z'/%3E%3C/svg%3E");
  background-repeat: no-repeat;
  background-position: right 8px center;
}

.btn-remove {
  width: 32px;
  height: 32px;
  border: 1px solid var(--line-raw);
  background: transparent;
  color: var(--ink-light);
  font-size: 16px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.3s;
  flex-shrink: 0;
  margin-top: 2px;
}

.btn-remove:hover {
  border-color: var(--cinnabar);
  color: var(--cinnabar);
}

/* ===== 图库网格 ===== */
.gallery-grid-form {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
  gap: 12px;
  margin-top: 8px;
}

.gallery-item-form {
  position: relative;
  border: 1px solid var(--line-raw);
  overflow: hidden;
  aspect-ratio: 1/1;
  background: var(--paper-sub);
}

.gallery-item-form img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.gallery-item-overlay {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  padding: 6px 8px;
  background: rgba(44, 42, 41, 0.7);
  display: flex;
  align-items: center;
  gap: 4px;
}

.gallery-alt-input {
  flex: 1;
  padding: 2px 6px;
  border: none;
  background: rgba(255, 255, 255, 0.15);
  color: #fff;
  font-size: 11px;
  outline: none;
  font-family: var(--font-family);
}

.gallery-alt-input::placeholder {
  color: rgba(255, 255, 255, 0.5);
}

.btn-remove-small {
  width: 20px;
  height: 20px;
  border: none;
  border-radius: 50%;
  background: rgba(231, 76, 60, 0.8);
  color: #fff;
  cursor: pointer;
  font-size: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.btn-remove-small:hover {
  background: var(--cinnabar);
}

.gallery-uploading {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 24px;
  border: 1px dashed var(--line-raw);
  background: var(--paper-sub);
  border-radius: 4px;
  color: var(--ink-gray);
  font-size: 13px;
}

.spinner-small {
  width: 24px;
  height: 24px;
  border: 2px solid var(--line-raw);
  border-top-color: var(--ink-black);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

/* ===== 按钮区 ===== */
.form-actions {
  display: flex;
  gap: 12px;
  padding-top: 20px;
  border-top: 1px solid var(--line-raw);
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

/* ===== 响应式 ===== */
@media (max-width: 768px) {
  .attr-row {
    grid-template-columns: 1fr;
    gap: 6px;
    padding-bottom: 12px;
    border-bottom: 1px dashed var(--line-raw);
  }

  .attr-row:last-child {
    border-bottom: none;
  }

  .gallery-grid-form {
    grid-template-columns: repeat(auto-fill, minmax(80px, 1fr));
    gap: 8px;
  }

  .btn-remove {
    width: 100%;
    height: 32px;
    margin-top: 0;
  }

  .form-actions {
    flex-direction: column-reverse;
  }

  .section-header-inline {
    flex-direction: column;
    align-items: flex-start;
    gap: 8px;
  }
}
</style>