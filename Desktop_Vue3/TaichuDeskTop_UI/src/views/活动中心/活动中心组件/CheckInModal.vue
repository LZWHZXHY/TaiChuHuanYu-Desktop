<template>
  <div v-if="visible" class="modal-overlay" @click.self="close">
    <div class="modal-card">
      <div class="modal-header">
        <h3><i class="fas fa-pen-fancy"></i> 打卡 - 第 {{ day }} 天</h3>
        <button class="close-btn" @click="close">&times;</button>
      </div>
      <div class="modal-body">
        <div class="form-group">
          <label>今日心得 <span class="required">*</span></label>
          <textarea v-model="text" placeholder="写下今天的收获..." rows="4"></textarea>
        </div>
        <div class="form-group">
          <label>配图（可选）</label>
          <input type="file" accept="image/*" @change="onFileChange" :disabled="isUploading" />
          <div v-if="isUploading" class="upload-progress">
            <i class="fas fa-spinner fa-spin"></i> 上传中 {{ uploadProgress }}%
          </div>
          <div v-if="imagePreview" class="image-preview">
            <img :src="imagePreview" alt="预览" />
            <button @click="removeImage" class="remove-image">&times;</button>
          </div>
        </div>
      </div>
      <div class="modal-footer">
        <button class="btn-cancel" @click="close">取消</button>
        <button class="btn-submit" @click="submit" :disabled="!text.trim() || isUploading">
          <i class="fas fa-check"></i> 提交打卡
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue';
import { useCos } from '@/composables/useCos';

const props = defineProps<{
  visible: boolean;
  member: any;
  day: number;
}>();

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void;
  (e: 'checkin', payload: { member: any; day: number; text: string; image: string }): void;
}>();

const { uploadFile, isUploading, progress } = useCos();

const text = ref('');
const imagePreview = ref('');
const uploadedImageUrl = ref('');
const uploadProgress = ref(0);

// 监听进度
watch(progress, (val: number) => {
  uploadProgress.value = val;
});

// 文件选择
const onFileChange = async (e: Event) => {
  const input = e.target as HTMLInputElement;
  if (input.files && input.files[0]) {
    const file = input.files[0];
    imagePreview.value = URL.createObjectURL(file);
    try {
      const result = await uploadFile(file, 'activity/checkin');
      uploadedImageUrl.value = result.url;
    } catch (error) {
      console.error('图片上传失败:', error);
      imagePreview.value = '';
      uploadedImageUrl.value = '';
      const inputEl = document.querySelector('input[type="file"]') as HTMLInputElement;
      if (inputEl) inputEl.value = '';
    }
  }
};

const removeImage = () => {
  imagePreview.value = '';
  uploadedImageUrl.value = '';
  const input = document.querySelector('input[type="file"]') as HTMLInputElement;
  if (input) input.value = '';
};

const close = () => {
  emit('update:visible', false);
  text.value = '';
  imagePreview.value = '';
  uploadedImageUrl.value = '';
  const input = document.querySelector('input[type="file"]') as HTMLInputElement;
  if (input) input.value = '';
};

const submit = () => {
  if (!text.value.trim()) {
    alert('请写下今日心得');
    return;
  }
  if (isUploading.value) {
    alert('图片正在上传，请稍候');
    return;
  }
  const imageUrl = uploadedImageUrl.value || '';
  emit('checkin', {
    member: props.member,
    day: props.day,
    text: text.value,
    image: imageUrl,
  });
  close();
};
</script>

<style scoped>
/* 原有样式保持不变，新增上传进度样式 */
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.3);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 1000;
  backdrop-filter: blur(2px);
}
.modal-card {
  background: #fff;
  border-radius: 12px;
  padding: 28px 32px;
  width: 480px;
  max-width: 92%;
  max-height: 80vh;
  overflow-y: auto;
  box-shadow: 0 8px 30px rgba(0, 0, 0, 0.1);
  animation: fadeIn 0.2s ease;
}
@keyframes fadeIn {
  from { opacity: 0; transform: scale(0.96); }
  to { opacity: 1; transform: scale(1); }
}
.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}
.modal-header h3 {
  margin: 0;
  font-weight: 600;
  font-size: 1.1rem;
  color: #1f2937;
}
.modal-header h3 i { color: #6366f1; margin-right: 6px; }
.close-btn {
  background: none;
  border: none;
  font-size: 1.6rem;
  cursor: pointer;
  color: #9ca3af;
  transition: 0.2s;
}
.close-btn:hover { color: #1f2937; }

.form-group { margin-bottom: 18px; }
.form-group label {
  display: block;
  font-weight: 500;
  font-size: 0.85rem;
  margin-bottom: 4px;
  color: #374151;
}
.form-group label .required { color: #ef4444; margin-left: 2px; }
.form-group textarea {
  width: 100%;
  padding: 10px 14px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  font-size: 0.9rem;
  font-family: inherit;
  resize: vertical;
  background: #fafafa;
  transition: border 0.2s;
}
.form-group textarea:focus {
  outline: none;
  border-color: #6366f1;
  background: #fff;
}
.form-group input[type="file"] {
  border: 1px dashed #e5e7eb;
  padding: 10px;
  border-radius: 8px;
  width: 100%;
  background: #fafafa;
}
.upload-progress {
  margin-top: 8px;
  color: #6366f1;
  font-size: 0.85rem;
}
.image-preview {
  margin-top: 8px;
  position: relative;
  display: inline-block;
}
.image-preview img {
  max-width: 100%;
  max-height: 160px;
  border-radius: 8px;
  border: 1px solid #e5e7eb;
}
.remove-image {
  position: absolute;
  top: 4px;
  right: 4px;
  background: rgba(0,0,0,0.5);
  color: #fff;
  border: none;
  border-radius: 50%;
  width: 24px;
  height: 24px;
  cursor: pointer;
  font-size: 0.8rem;
  display: flex;
  align-items: center;
  justify-content: center;
}
.remove-image:hover { background: #000; }

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 20px;
}
.btn-cancel {
  padding: 8px 20px;
  border: 1px solid #e5e7eb;
  border-radius: 30px;
  background: #fff;
  color: #374151;
  cursor: pointer;
  transition: background 0.2s;
}
.btn-cancel:hover { background: #f3f4f6; }
.btn-submit {
  padding: 8px 24px;
  border: none;
  border-radius: 30px;
  background: #6366f1;
  color: #fff;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}
.btn-submit:hover:not(:disabled) { background: #4f46e5; }
.btn-submit:disabled { opacity: 0.5; cursor: not-allowed; }
</style>