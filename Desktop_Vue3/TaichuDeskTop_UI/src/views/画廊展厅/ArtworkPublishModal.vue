<template>
  <div v-if="modelValue" class="publish-mask" role="dialog" aria-modal="true" aria-label="作品编辑与发布" @click.self="close">
    <div class="publish-modal content-form">
      <header class="modal-header">
        <div>
          <h2 class="modal-title">{{ artwork ? '编辑作品' : '发布作品' }}</h2>
          <p class="modal-subtitle">{{ artwork ? '修改标题、简介、题注和封面；已发布图片及水印保持原样' : '选择图片、填写作品信息后发布到画廊' }}</p>
        </div>
        <button class="close-btn" aria-label="关闭作品编辑与发布" @click="close">✕</button>
      </header>

      <fieldset class="modal-body" :disabled="submitting">
        <!-- 左侧：图像管理与上传 -->
        <div class="media-section">
          <label class="section-label">作品图片 ({{ imageList.length }}P)</label>
          <div 
            v-if="!artwork" class="dropzone" :class="{ dragging: isDragging }"
            @dragover.prevent="isDragging = true"
            @dragleave.prevent="isDragging = false"
            @drop.prevent="handleDrop"
            @click="triggerFileInput"
            role="button" tabindex="0" aria-label="选择作品图片" @keydown.enter.prevent="triggerFileInput" @keydown.space.prevent="triggerFileInput"
          >
            <input 
              type="file" 
              ref="fileInputRef" 
              multiple 
              accept="image/*" 
              class="hidden-input" 
              @change="handleFileSelect"
            />
            <div class="drop-hint">
              <span class="upload-icon">✦</span>
              <p>点击或拖拽图像至此处</p>
              <span class="sub-hint">最多 20 张，单张 10 MB，总计 25 MB；水印建议使用 JPG / PNG</span>
            </div>
          </div>

          <!-- 图片拖拽/预览列表 -->
          <div class="image-grid" v-if="imageList.length > 0">
            <div 
              v-for="(item, idx) in imageList" 
              :key="item.localId" 
              :class="['grid-item', { is_cover: item.isCover }]"
            >
              <div class="thumb-box">
                <img :src="item.previewUrl" class="thumb-img" />
                <div class="item-mask">
                  <button 
                    type="button" 
                    :class="['cover-btn', { active: item.isCover }]" 
                    @click="setCover(idx)"
                  >
                    {{ item.isCover ? '★ 封面' : '设为封面' }}
                  </button>
                  <button type="button" v-if="!artwork" class="del-btn" @click="removeImage(idx)">✕</button>
                </div>
              </div>
              <input 
                v-model="item.caption" 
                class="caption-input" :aria-label="`第 ${idx + 1} 张图片题注`" 
                placeholder="为本画幅题注 (可选)" 
                maxlength="500"
              />
            </div>
          </div>
        </div>

        <!-- 右侧：作品元数据与水印 -->
        <div class="meta-section">
          <div class="form-group">
            <label for="artwork-title">画作标题 <span class="required">*</span></label>
            <input 
              id="artwork-title" v-model="form.title" 
              type="text" 
              placeholder="填写作品标题" 
              maxlength="100" 
              class="text-input"
            />
          </div>

          <div class="form-group">
            <label for="artwork-description">作品说明</label>
            <textarea 
              id="artwork-description" v-model="form.description" 
              rows="4" 
              placeholder="介绍作品、练习目的或参考来源（可选）" 
              maxlength="1000"
              class="text-textarea"
            ></textarea>
          </div>

          <div class="form-group"><label for="artwork-tags">标签（可选）</label><input id="artwork-tags" v-model="form.tags" class="text-input" placeholder="例如：风景，原创，练习；用逗号分隔" maxlength="510" /><p class="sub-hint">标签用于分类与搜索，不要求关联人设、世界观或笔记。</p></div>
        </div>
      </fieldset>
      <p v-if="submitting && !artwork" class="form-error" role="status">{{ progress < 100 ? `上传中 ${progress}%` : '图片已上传，正在处理并保存，请稍候…' }}</p>
      <p v-if="error" class="form-error" role="alert">{{ error }}</p>

      <footer class="modal-footer">
        <button class="btn-cancel" @click="close" :disabled="submitting">取消</button>
        <button class="btn-submit" @click="handleSubmit" :disabled="submitting">
          {{ submitting ? '正在保存…' : artwork ? '保存修改' : '发布作品' }}
        </button>
      </footer>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, watch, onUnmounted } from 'vue'
import { artworkApi, type ArtworkDetail } from '../../api/artwork'
import { wikiTags } from '@/utils/wikiContent'
import { useUserStore } from '../../stores/user'

interface ImageItem { localId: string; file?: File; previewUrl: string; caption: string; isCover: boolean }
const props = defineProps<{ modelValue: boolean; artwork?: ArtworkDetail }>()
const emit = defineEmits(['update:modelValue', 'published'])
const user = useUserStore()
const fileInputRef = ref<HTMLInputElement | null>(null)
const isDragging = ref(false), submitting = ref(false), error = ref(''), progress = ref(0)
const imageList = ref<ImageItem[]>([])
const defaults = () => ({ title: '', description: '', tags: '', watermarkEnabled: false, watermarkText: '', watermarkPosition: 'bottom-right', watermarkColor: '#ffffff' })
const form = reactive(defaults())
let generation = 0
const releaseImages = () => imageList.value.forEach(item => { if (item.file) URL.revokeObjectURL(item.previewUrl) })
function reset() {
  releaseImages(); imageList.value = []; Object.assign(form, defaults()); error.value = ''; isDragging.value = false
}
watch(() => [props.modelValue, props.artwork], () => {
  generation++; reset()
  if (props.modelValue && props.artwork) {
    Object.assign(form, { title: props.artwork.title, description: props.artwork.description || '', tags: (props.artwork.tags || []).join(', ') })
    imageList.value = props.artwork.images.map((image, index) => ({ localId: String(index), previewUrl: image.url, caption: image.caption || '', isCover: !!image.isCover }))
    if (!imageList.value.some(item => item.isCover) && imageList.value[0]) imageList.value[0].isCover = true
  }
}, { immediate: true })
watch(() => user.token, () => { generation++; reset(); emit('update:modelValue', false) }, { flush: 'sync' })
onUnmounted(() => { generation++; releaseImages() })
const close = () => { if (!submitting.value) emit('update:modelValue', false) }
const triggerFileInput = () => { if (!submitting.value) fileInputRef.value?.click() }
function processFiles(files: FileList | null) {
  if (!files || submitting.value || props.artwork) return
  const selected = Array.from(files)
  if (imageList.value.length + selected.length > 20 || selected.some(f => !f.type.startsWith('image/') || !f.size || f.size > 10 * 1024 * 1024) ||
      [...imageList.value.map(i => i.file!), ...selected].reduce((sum, f) => sum + f.size, 0) > 25 * 1024 * 1024) {
    error.value = '请选择有效图片：最多 20 张，单张 10 MB，总计 25 MB'; return
  }
  error.value = ''
  selected.forEach(file => imageList.value.push({ localId: crypto.randomUUID(), file, previewUrl: URL.createObjectURL(file), caption: '', isCover: imageList.value.length === 0 }))
}
const handleFileSelect = (event: Event) => { const input = event.target as HTMLInputElement; processFiles(input.files); input.value = '' }
const handleDrop = (event: DragEvent) => { isDragging.value = false; processFiles(event.dataTransfer?.files || null) }
const setCover = (index: number) => { if (!submitting.value) imageList.value.forEach((item, i) => { item.isCover = i === index }) }
function removeImage(index: number) {
  if (submitting.value || props.artwork) return
  const [item] = imageList.value.splice(index, 1)
  if (!item) return
  URL.revokeObjectURL(item.previewUrl)
  if (item.isCover && imageList.value[0]) imageList.value[0].isCover = true
}
async function handleSubmit() {
  if (submitting.value) return
  if (!user.isLoggedIn) { error.value = '请先登录后再发布或编辑作品'; return }
  if (!form.title.trim() || !imageList.value.length) { error.value = '请填写标题并选择至少一张图片'; return }
  const tags = wikiTags(form.tags);
  if (tags.length > 10 || tags.some(t => t.length > 50)) { error.value = '最多 10 个标签，每个不超过 50 字'; return }
  const scope = generation
  submitting.value = true; error.value = ''; progress.value = 0
  try {
    const data = { tags, title: form.title.trim(), description: form.description.trim(), captions: imageList.value.map(i => i.caption.trim()), coverIndex: Math.max(0, imageList.value.findIndex(i => i.isCover)) }
    let response
    if (props.artwork) response = await artworkApi.edit(props.artwork.id, data)
    else {
      const body = new FormData()
      Object.entries({ ...form, tags: undefined, title: data.title, description: data.description, coverIndex: data.coverIndex }).forEach(([key, value]) => value !== undefined && body.append(key, String(value)))
      imageList.value.forEach((item, i) => { body.append('images', item.file!); body.append(`captions[${i}]`, data.captions[i]) })
      tags.forEach(tag => body.append('tags', tag));
      response = await artworkApi.publish(body, value => progress.value = value)
    }
    if (scope !== generation) return
    emit('published', response); emit('update:modelValue', false)
  } catch (cause: any) {
    if (scope === generation) error.value = cause.friendlyMessage || '保存失败，输入内容已保留，请重试'
  } finally { submitting.value = false }
}
</script>

<style scoped>
.modal-body { margin: 0; border: 0; min-width: 0; min-height: 0; flex: 1; }
.form-error { margin: 0; padding: 12px 32px; color: var(--accent-color); }
input, textarea, select { color: var(--text-primary); background: var(--paper-card); }
button:disabled { opacity: .5; cursor: default; }
button:focus-visible, .dropzone:focus-visible { outline: 2px solid var(--accent-color); }
.thumb-box:focus-within .item-mask, .dropzone.dragging { opacity: 1; border-color: var(--accent-color); }
@media (hover: none) { .item-mask { opacity: 1; } }
.publish-mask {
  position: fixed;
  inset: 0;
  z-index: 10001;
  background: rgba(0, 0, 0, 0.45);
  backdrop-filter: blur(8px);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px;
}

.publish-modal {
  background: var(--paper-bg);
  width: 920px;
  max-width: 100%;
  max-height: 90vh;
  border-radius: 12px;
  box-shadow: 0 30px 60px rgba(0, 0, 0, 0.15);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.modal-header {
  flex-shrink: 0;
  gap: 16px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 24px 32px;
  border-bottom: 1px solid var(--border-line);
}
.modal-title { margin: 0; font-size: 1.25rem; font-weight: 700; color: var(--text-primary); }
.modal-subtitle { margin: 4px 0 0; font-size: 0.8rem; color: var(--text-secondary); }
.close-btn { flex-shrink: 0; align-self: flex-start; background: none; border: none; font-size: 1.2rem; cursor: pointer; color: var(--text-secondary); }

.modal-body {
  padding: 32px;
  display: grid;
  grid-template-columns: 1.3fr 1fr;
  gap: 32px;
  overflow-y: auto;
  -webkit-overflow-scrolling: touch;
  overscroll-behavior: contain;
  touch-action: pan-y;
}

.media-section { display: flex; flex-direction: column; gap: 16px; }
.section-label { font-size: 0.85rem; font-weight: 600; color: var(--text-primary); }

.dropzone {
  border: 1px dashed var(--border-line);
  border-radius: 8px;
  padding: 28px 16px;
  text-align: center;
  cursor: pointer;
  background: var(--paper-card);
  transition: all 0.2s ease;
}
.dropzone:hover { border-color: var(--accent-color); background: var(--paper-card); }
.hidden-input { display: none; }
.upload-icon { font-size: 1.5rem; color: var(--text-secondary); }
.drop-hint p { margin: 8px 0 4px; font-weight: 500; font-size: 0.9rem; }
.sub-hint { font-size: 0.75rem; color: var(--text-secondary); }

.image-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
  padding-right: 4px;
}

.grid-item {
  border: 1px solid var(--border-line);
  border-radius: 6px;
  padding: 6px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  background: var(--paper-bg);
}
.grid-item.is_cover { border-color: var(--accent-color); }

.thumb-box {
  position: relative;
  width: 100%;
  aspect-ratio: 4/3;
  overflow: hidden;
  border-radius: 4px;
  background: var(--border-line);
}
.thumb-img { width: 100%; height: 100%; object-fit: cover; }
.item-mask {
  position: absolute;
  inset: 0;
  background: rgba(0, 0, 0, 0.4);
  opacity: 0;
  display: flex;
  align-items: center;
  justify-content: space-around;
  transition: opacity 0.2s;
}
.thumb-box:hover .item-mask { opacity: 1; }

.cover-btn, .del-btn {
  border: none;
  font-size: 0.75rem;
  padding: 4px 8px;
  border-radius: 4px;
  cursor: pointer;
}
.cover-btn { background: var(--paper-bg); color: var(--accent-color); }
.cover-btn.active { background: var(--accent-color); color: #fff; }
.del-btn { background: rgba(255, 60, 60, 0.85); color: var(--paper-bg); }
.caption-input {
  border: 1px solid var(--border-line);
  border-radius: 4px;
  font-size: 0.75rem;
  padding: 4px 6px;
}

.meta-section { display: flex; flex-direction: column; gap: 20px; }
.form-group { display: flex; flex-direction: column; gap: 6px; }
.form-group label { font-size: 0.85rem; font-weight: 600; color: var(--text-primary); }
.required { color: #eb4d4b; }
.text-input, .text-textarea, .select-input {
  border: 1px solid var(--border-line);
  border-radius: 6px;
  padding: 10px 12px;
  font-size: 0.85rem;
  outline: none;
}
.text-input:focus, .text-textarea:focus, .select-input:focus { border-color: var(--accent-color); }
.text-textarea { resize: vertical; }

.toggle-label { display: flex; align-items: center; gap: 8px; cursor: pointer; font-size: 0.85rem; }
.watermark-subform { display: flex; flex-direction: column; gap: 8px; margin-top: 8px; }
.watermark-options { display: flex; gap: 8px; align-items: center; }
.color-picker { width: 36px; height: 36px; border: none; cursor: pointer; border-radius: 4px; background: none; }

.modal-footer {
  flex-shrink: 0;
  background: var(--paper-bg);
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  padding: 20px 32px;
  border-top: 1px solid var(--border-line);
}
.btn-cancel { color: var(--text-primary); background: var(--paper-card); border: 1px solid var(--border-line); padding: 8px 18px; border-radius: 6px; cursor: pointer; font-size: 0.85rem; }
.btn-submit { background: var(--accent-color); color: #fff; border: none; padding: 8px 22px; border-radius: 6px; cursor: pointer; font-size: 0.85rem; }
.btn-submit:disabled { opacity: 0.5; cursor: not-allowed; }

@media (max-width: 768px) {
  .modal-body { grid-template-columns: 1fr; padding: 20px; gap: 24px; }
  .modal-header, .modal-footer { padding: 20px; }
  .item-mask { opacity: 1; }
}
/* 触屏设备（平板/手机）：去掉毛玻璃背景，避免遮挡触摸滚动 */
@media (hover: none) {
  .publish-mask { backdrop-filter: none; }
}
</style>
