<template>
  <div v-if="modelValue" class="publish-mask" @click.self="close">
    <div class="publish-modal">
      <header class="modal-header">
        <div>
          <h2 class="modal-title">灵脉聚形 / 展布画卷</h2>
          <p class="modal-subtitle">无需撰写长卷笔记，直接铺陈图幅入馆</p>
        </div>
        <button class="close-btn" @click="close">✕</button>
      </header>

      <div class="modal-body">
        <!-- 左侧：图像管理与上传 -->
        <div class="media-section">
          <label class="section-label">画卷素材 ({{ imageList.length }}P)</label>
          <div 
            class="dropzone"
            @dragover.prevent="isDragging = true"
            @dragleave.prevent="isDragging = false"
            @drop.prevent="handleDrop"
            @click="triggerFileInput"
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
              <span class="sub-hint">支持多图上传，支持 JPG / PNG / WEBP</span>
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
                  <button type="button" class="del-btn" @click="removeImage(idx)">✕</button>
                </div>
              </div>
              <input 
                v-model="item.caption" 
                class="caption-input" 
                placeholder="为本画幅题注 (可选)" 
                maxlength="100"
              />
            </div>
          </div>
        </div>

        <!-- 右侧：作品元数据与水印 -->
        <div class="meta-section">
          <div class="form-group">
            <label>画作标题 <span class="required">*</span></label>
            <input 
              v-model="form.title" 
              type="text" 
              placeholder="题写画作之名..." 
              maxlength="80" 
              class="text-input"
            />
          </div>

          <div class="form-group">
            <label>作意陈述</label>
            <textarea 
              v-model="form.description" 
              rows="4" 
              placeholder="概述灵感、画法或心境..." 
              maxlength="800"
              class="text-textarea"
            ></textarea>
          </div>

          <div class="form-group">
            <label class="toggle-label">
              <input type="checkbox" v-model="form.watermarkEnabled" />
              <span>开启灵印防护 (水印)</span>
            </label>
            <div v-if="form.watermarkEnabled" class="watermark-subform">
              <input 
                v-model="form.watermarkText" 
                type="text" 
                placeholder="水印文字内容（默认为太初寰宇）" 
                class="text-input"
              />
              <div class="watermark-options">
                <select v-model="form.watermarkPosition" class="select-input">
                  <option value="bottom-right">右下角</option>
                  <option value="center">画心正中</option>
                  <option value="bottom-left">左下角</option>
                </select>
                <input 
                  type="color" 
                  v-model="form.watermarkColor" 
                  title="水印颜色" 
                  class="color-picker"
                />
              </div>
            </div>
          </div>
        </div>
      </div>

      <footer class="modal-footer">
        <button class="btn-cancel" @click="close" :disabled="submitting">取消</button>
        <button class="btn-submit" @click="handleSubmit" :disabled="submitting">
          {{ submitting ? '画卷凝炼中...' : '即刻布展' }}
        </button>
      </footer>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue';
// 导入项目通用的请求封装（包含自动挂载 Token / 拦截器）
import request from '../../utils/request'; 

interface ImageItem {
  localId: string;
  file: File;
  previewUrl: string;
  caption: string;
  isCover: boolean;
}

const props = defineProps<{ modelValue: boolean }>();
const emit = defineEmits(['update:modelValue', 'published']);

const fileInputRef = ref<HTMLInputElement | null>(null);
const isDragging = ref(false);
const submitting = ref(false);
const imageList = ref<ImageItem[]>([]);

const form = reactive({
  title: '',
  description: '',
  watermarkEnabled: true,
  watermarkText: '',
  watermarkPosition: 'bottom-right',
  watermarkColor: '#ffffff'
});

const triggerFileInput = () => fileInputRef.value?.click();

const processFiles = (files: FileList | null) => {
  if (!files) return;
  Array.from(files).forEach((file) => {
    if (!file.type.startsWith('image/')) return;
    const isFirst = imageList.value.length === 0;
    imageList.value.push({
      localId: `${Date.now()}-${Math.random()}`,
      file,
      previewUrl: URL.createObjectURL(file),
      caption: '',
      isCover: isFirst
    });
  });
};

const handleFileSelect = (e: Event) => {
  const target = e.target as HTMLInputElement;
  processFiles(target.files);
  target.value = '';
};

const handleDrop = (e: DragEvent) => {
  isDragging.value = false;
  processFiles(e.dataTransfer?.files || null);
};

const setCover = (index: number) => {
  imageList.value.forEach((item, idx) => {
    item.isCover = idx === index;
  });
};

const removeImage = (index: number) => {
  const [removed] = imageList.value.splice(index, 1);
  URL.revokeObjectURL(removed.previewUrl);
  if (removed.isCover && imageList.value.length > 0) {
    imageList.value[0].isCover = true;
  }
};

const resetForm = () => {
  imageList.value.forEach(img => URL.revokeObjectURL(img.previewUrl));
  imageList.value = [];
  form.title = '';
  form.description = '';
  form.watermarkText = '';
};

const close = () => {
  emit('update:modelValue', false);
};

// 🌟 API 请求直接内聚在组件内部
const handleSubmit = async () => {
  if (!form.title.trim()) {
    alert('请为画作题写标题');
    return;
  }
  if (imageList.value.length === 0) {
    alert('请至少上传一张画作');
    return;
  }

  submitting.value = true;

  try {
    const formData = new FormData();
    formData.append('title', form.title.trim());
    if (form.description.trim()) {
      formData.append('description', form.description.trim());
    }

    formData.append('watermarkEnabled', String(form.watermarkEnabled));
    if (form.watermarkText.trim()) {
      formData.append('watermarkText', form.watermarkText.trim());
    }
    formData.append('watermarkPosition', form.watermarkPosition);
    formData.append('watermarkColor', form.watermarkColor);

    // 确定封面索引 CoverIndex
    const coverIdx = imageList.value.findIndex(item => item.isCover);
    formData.append('coverIndex', String(coverIdx >= 0 ? coverIdx : 0));

    // 追加图片文件与对应单图注记
    imageList.value.forEach((item, index) => {
      formData.append('images', item.file);
      if (item.caption.trim()) {
        formData.append(`captions[${index}]`, item.caption.trim());
      }
    });

    // ✅ 正确写法：调用对象的 post 方法
const response: any = await request.post(
  '/NewArtwork/direct-upload',
  formData,
  {
    headers: {
      'Content-Type': 'multipart/form-data'
    }
  }
);

    emit('published', response);
    resetForm();
    close();
  } catch (err: any) {
    alert(err?.response?.data?.message || err?.message || '画布布展异常');
  } finally {
    submitting.value = false;
  }
};
</script>

<style scoped>
.publish-mask {
  position: fixed;
  inset: 0;
  z-index: 1000;
  background: rgba(0, 0, 0, 0.45);
  backdrop-filter: blur(8px);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px;
}

.publish-modal {
  background: #ffffff;
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
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 24px 32px;
  border-bottom: 1px solid #f0f0f0;
}
.modal-title { margin: 0; font-size: 1.25rem; font-weight: 700; color: #1a1a1a; }
.modal-subtitle { margin: 4px 0 0; font-size: 0.8rem; color: #86868b; }
.close-btn { background: none; border: none; font-size: 1.2rem; cursor: pointer; color: #999; }

.modal-body {
  padding: 32px;
  display: grid;
  grid-template-columns: 1.3fr 1fr;
  gap: 32px;
  overflow-y: auto;
}

.media-section { display: flex; flex-direction: column; gap: 16px; }
.section-label { font-size: 0.85rem; font-weight: 600; color: #333; }

.dropzone {
  border: 1px dashed #d1d1d6;
  border-radius: 8px;
  padding: 28px 16px;
  text-align: center;
  cursor: pointer;
  background: #fafafa;
  transition: all 0.2s ease;
}
.dropzone:hover { border-color: #000; background: #f5f5f7; }
.hidden-input { display: none; }
.upload-icon { font-size: 1.5rem; color: #86868b; }
.drop-hint p { margin: 8px 0 4px; font-weight: 500; font-size: 0.9rem; }
.sub-hint { font-size: 0.75rem; color: #86868b; }

.image-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
  max-height: 320px;
  overflow-y: auto;
  padding-right: 4px;
}

.grid-item {
  border: 1px solid #eee;
  border-radius: 6px;
  padding: 6px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  background: #fff;
}
.grid-item.is_cover { border-color: #000; }

.thumb-box {
  position: relative;
  width: 100%;
  aspect-ratio: 4/3;
  overflow: hidden;
  border-radius: 4px;
  background: #f0f0f0;
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
.cover-btn { background: #fff; color: #000; }
.cover-btn.active { background: #000; color: #fff; }
.del-btn { background: rgba(255, 60, 60, 0.85); color: #fff; }
.caption-input {
  border: 1px solid #f0f0f0;
  border-radius: 4px;
  font-size: 0.75rem;
  padding: 4px 6px;
}

.meta-section { display: flex; flex-direction: column; gap: 20px; }
.form-group { display: flex; flex-direction: column; gap: 6px; }
.form-group label { font-size: 0.85rem; font-weight: 600; color: #333; }
.required { color: #eb4d4b; }
.text-input, .text-textarea, .select-input {
  border: 1px solid #e5e5ea;
  border-radius: 6px;
  padding: 10px 12px;
  font-size: 0.85rem;
  outline: none;
}
.text-input:focus, .text-textarea:focus, .select-input:focus { border-color: #000; }
.text-textarea { resize: vertical; }

.toggle-label { display: flex; align-items: center; gap: 8px; cursor: pointer; font-size: 0.85rem; }
.watermark-subform { display: flex; flex-direction: column; gap: 8px; margin-top: 8px; }
.watermark-options { display: flex; gap: 8px; align-items: center; }
.color-picker { width: 36px; height: 36px; border: none; cursor: pointer; border-radius: 4px; background: none; }

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  padding: 20px 32px;
  border-top: 1px solid #f0f0f0;
}
.btn-cancel { background: #f5f5f7; border: none; padding: 8px 18px; border-radius: 6px; cursor: pointer; font-size: 0.85rem; }
.btn-submit { background: #000; color: #fff; border: none; padding: 8px 22px; border-radius: 6px; cursor: pointer; font-size: 0.85rem; }
.btn-submit:disabled { opacity: 0.5; cursor: not-allowed; }

@media (max-width: 768px) {
  .modal-body { grid-template-columns: 1fr; }
}
</style>