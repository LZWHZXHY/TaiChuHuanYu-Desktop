<template>
  <div v-if="visible" class="modal-overlay" @click.self="close">
    <div class="modal-card" role="dialog" aria-modal="true" aria-label="填写打卡记录">
      <div class="modal-header">
        <h3><i class="fas fa-pen-fancy"></i> 打卡 - 第 {{ day }} 天</h3>
        <button aria-label="关闭打卡" class="close-btn" @click="close">&times;</button>
      </div>
      <div class="modal-body">
        <div class="form-group">
          <label>今日心得 <span class="required">*</span></label>
          <textarea aria-label="今日心得" v-model="text" placeholder="写下今天的收获..." rows="4"></textarea>
        </div>
        <div class="form-group">
          <label>配图（可选）</label>
          <input aria-label="配图（可选）" ref="fileInput" type="file" accept="image/*" @change="onFileChange" :disabled="isUploading || submitting" />
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
        <button class="btn-submit" @click="submit" :disabled="!text.trim() || isUploading || submitting">
          <i class="fas fa-check"></i> {{ submitting ? '保存中…' : '提交打卡' }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onUnmounted } from 'vue';
import { useCos } from '@/composables/useCos';

const props = defineProps<{
  visible: boolean;
  member: any;
  day: number;
  submitting?: boolean;
}>();

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void;
  (e: 'checkin', payload: { member: any; day: number; text: string; image: string }): void;
}>();

const { uploadFile, isUploading, progress } = useCos();

const text = ref('');
const fileInput = ref<HTMLInputElement | null>(null);
const clearPreview = () => { if (imagePreview.value.startsWith('blob:')) URL.revokeObjectURL(imagePreview.value); imagePreview.value = ''; };
onUnmounted(clearPreview);
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
    clearPreview();
    imagePreview.value = URL.createObjectURL(file);
    try {
      const result = await uploadFile(file, 'activity/checkin');
      uploadedImageUrl.value = result.url;
    } catch (error) {
      console.error('图片上传失败:', error);
      clearPreview();
      uploadedImageUrl.value = '';
      if (fileInput.value) fileInput.value.value = '';
    }
  }
};

const removeImage = () => {
  clearPreview();
  uploadedImageUrl.value = '';
  if (fileInput.value) fileInput.value.value = '';
};

const close = () => {
  if (props.submitting || isUploading.value) return;
  emit('update:visible', false);
};
watch(() => props.visible, (visible) => {
  if (visible) return;
  text.value = '';
  clearPreview();
  uploadedImageUrl.value = '';
  if (fileInput.value) fileInput.value.value = '';
});

const submit = () => {
  if (props.submitting) return;
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
};
</script>

<style scoped>
/* 原有样式保持不变，新增上传进度样式 */
.modal-overlay {
  position: fixed;
  inset: 0;
  background: var(--overlay);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 1000;
  backdrop-filter: blur(2px);
}
.modal-card {
  background: var(--paper-card);
  border-radius: 0;
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
  color: var(--text-primary);
}
.modal-header h3 i { color: var(--info); margin-right: 6px; }
.close-btn {
  background: none;
  border: none;
  font-size: 1.6rem;
  cursor: pointer;
  color: var(--text-secondary);
  transition: 0.2s;
}
.close-btn:hover { color: var(--text-primary); }

.form-group { margin-bottom: 18px; }
.form-group label {
  display: block;
  font-weight: 500;
  font-size: 0.85rem;
  margin-bottom: 4px;
  color: var(--text-primary);
}
.form-group label .required { color: var(--danger); margin-left: 2px; }
.form-group textarea {
  width: 100%;
  padding: 10px 14px;
  border: 1px solid var(--border-line);
  border-radius: 0;
  font-size: 0.9rem;
  font-family: inherit;
  resize: vertical;
  background: var(--paper-card);
  transition: border 0.2s;
}
.form-group textarea:focus {
  outline: none;
  border-color: var(--info);
  background: var(--paper-card);
}
.form-group input[type="file"] {
  border: 1px dashed var(--border-line);
  padding: 10px;
  border-radius: 0;
  width: 100%;
  background: var(--paper-card);
}
.upload-progress {
  margin-top: 8px;
  color: var(--info);
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
  border-radius: 0;
  border: 1px solid var(--border-line);
}
.remove-image {
  position: absolute;
  top: 4px;
  right: 4px;
  background: var(--overlay);
  color: var(--on-overlay);
  border: none;
  border-radius: 0;
  width: 24px;
  height: 24px;
  cursor: pointer;
  font-size: 0.8rem;
  display: flex;
  align-items: center;
  justify-content: center;
}
.remove-image:hover { background: var(--bg-sub); }

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 20px;
}
.btn-cancel {
  padding: 8px 20px;
  border: 1px solid var(--border-line);
  border-radius: 0;
  background: var(--paper-card);
  color: var(--text-primary);
  cursor: pointer;
  transition: background 0.2s;
}
.btn-cancel:hover { background: var(--bg-sub); }
.btn-submit {
  padding: 8px 24px;
  border: none;
  border-radius: 0;
  background: var(--info);
  color: var(--on-accent);
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}
.btn-submit:hover:not(:disabled) { background: var(--info); }
.btn-submit:disabled { opacity: 0.5; cursor: not-allowed; }
</style>
