<template>
  <div class="form-card">
    <h2><i class="fas fa-rocket"></i> 开启新挑战</h2>
    <form @submit.prevent="handleSubmit">
      <div class="form-group">
        <label>活动名称 <span class="required">*</span></label>
        <input type="text" v-model="form.title" placeholder="输入活动名称..." />
      </div>
      <div class="form-row">
        <div class="form-group">
          <label>活动类型 <span class="required">*</span></label>
          <div class="type-tags" v-if="!loadingTypes">
            <span 
              v-for="type in typeOptions" 
              :key="type.id"
              class="type-tag-option" 
              :class="{ active: form.typeId === type.id }"
              @click="form.typeId = type.id">
              {{ type.name }}
            </span>
          </div>
          <div v-else class="loading-types">
            <i class="fas fa-spinner fa-spin"></i> 加载中...
          </div>
        </div>
        <div class="form-group">
          <label>周期 (天) <span class="required">*</span></label>
          <input type="number" v-model.number="form.days" min="7" max="100" />
          <div class="hint">建议 7~100 天</div>
        </div>
      </div>
      <div class="form-group">
        <label>规则说明 <span class="required">*</span></label>
        <textarea v-model="form.rule" rows="3" placeholder="详细描述打卡要求..."></textarea>
      </div>
      
      <!-- 封面图区域：支持上传图片或手动输入 URL -->
      <div class="form-group">
        <label>封面图</label>
        <div class="cover-upload-wrapper">
          <!-- 上传按钮 -->
          <div class="upload-area">
            <input 
              type="file" 
              accept="image/*" 
              @change="handleFileSelect" 
              :disabled="uploading"
              ref="fileInput"
            />
            <button type="button" class="upload-btn" @click="fileInput?.click()" :disabled="uploading">
              <i class="fas fa-cloud-upload-alt"></i> 选择图片
            </button>
            <span v-if="uploading" class="upload-progress">
              <i class="fas fa-spinner fa-spin"></i> 上传中 {{ uploadProgress }}%
            </span>
            <span v-else-if="form.cover" class="upload-success">
              <i class="fas fa-check-circle" style="color: #10b981;"></i> 已上传
            </span>
          </div>
          <!-- 预览 -->
          <div v-if="coverPreview" class="cover-preview">
            <img :src="coverPreview" alt="封面预览" />
            <button type="button" class="remove-cover" @click="removeCover">
              <i class="fas fa-times"></i>
            </button>
          </div>
          <!-- 手动输入 URL -->
          <div class="url-input-wrapper">
            <span class="url-prefix">或输入图片 URL：</span>
            <input 
              type="text" 
              v-model="form.cover" 
              placeholder="https://example.com/cover.jpg" 
              @input="onUrlInput"
            />
          </div>
        </div>
        <div class="hint">建议使用 16:9 比例的图片，支持 JPG/PNG</div>
      </div>

      <button type="submit" class="submit-btn" :disabled="submitting || uploading">
        <i class="fas fa-paper-plane"></i> 正式发布活动
      </button>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue';
import { useRouter } from 'vue-router';
import request from '@/utils/request';
import { useCos } from '@/composables/useCos';

const router = useRouter();

const { uploadFile, isUploading, progress } = useCos();

const form = ref({
  title: '',
  typeId: 0,
  days: 30,
  rule: '',
  cover: '',
});

const typeOptions = ref<{ id: number; name: string }[]>([]);
const loadingTypes = ref(false);
const submitting = ref(false);
const uploading = ref(false);
const uploadProgress = ref(0);
const coverPreview = ref(''); // 用于本地预览
const fileInput = ref<HTMLInputElement | null>(null);

// 监听上传进度
watch(progress, (val) => {
  uploadProgress.value = val;
});

// 从后端获取活动类型列表
const fetchTypes = async () => {
  loadingTypes.value = true;
  try {
    const data = await request.get('/activities/types');
    typeOptions.value = data || [];
    if (typeOptions.value.length > 0) {
      form.value.typeId = typeOptions.value[0].id;
    }
  } catch (error) {
    console.error('加载活动类型失败:', error);
    // 降级默认
    typeOptions.value = [
      { id: 1, name: '技术精进' },
      { id: 2, name: '健康生活' },
      { id: 3, name: '阅读写作' },
      { id: 4, name: '运动健身' },
      { id: 5, name: '其他' },
    ];
    form.value.typeId = typeOptions.value[0].id;
  } finally {
    loadingTypes.value = false;
  }
};

// 文件选择处理
const handleFileSelect = async (event: Event) => {
  const input = event.target as HTMLInputElement;
  if (!input.files || input.files.length === 0) return;
  const file = input.files[0];
  
  // 立即显示预览
  coverPreview.value = URL.createObjectURL(file);
  uploading.value = true;
  
  try {
    // 上传到 COS，文件夹使用 activity/covers
    const result = await uploadFile(file, 'activity/covers');
    form.value.cover = result.url; // 填入真实 URL
    // 如果预览用的本地 blob，可保持不变（但提交时使用的是 form.cover）
    // 注意：如果用户之后删除图片，要重置
  } catch (error) {
    console.error('上传失败:', error);
    // 上传失败时清空预览和 URL
    coverPreview.value = '';
    form.value.cover = '';
    // 提示用户
    alert('图片上传失败，请重试或手动输入 URL');
  } finally {
    uploading.value = false;
    // 重置 input，允许重复选择同一文件
    if (fileInput.value) {
      fileInput.value.value = '';
    }
  }
};

// 移除封面
const removeCover = () => {
  form.value.cover = '';
  coverPreview.value = '';
  if (fileInput.value) {
    fileInput.value.value = '';
  }
};

// 当用户手动输入 URL 时，更新预览（可选）
const onUrlInput = () => {
  // 如果用户手动输入了 URL，我们可以将其设置为预览，但无法真正预览外部图片（跨域问题）
  // 为了简单，仅当用户输入时清空文件预览
  if (form.value.cover) {
    // 如果用户输入了 URL，且之前有上传预览，可考虑清除预览
    // 但为了更好的体验，如果用户手动输入，我们保留预览区域展示该 URL（但这里不强制）
  }
};

// 提交表单
const handleSubmit = async () => {
  if (!form.value.title || !form.value.rule || !form.value.days) {
    alert('请填写完整信息');
    return;
  }
  if (!form.value.typeId) {
    alert('请选择活动类型');
    return;
  }
  // 如果正在上传，阻止提交
  if (uploading.value) {
    alert('图片正在上传，请稍候');
    return;
  }
  submitting.value = true;
  try {
    await request.post('/activities', {
      title: form.value.title,
      description: form.value.rule,
      typeId: form.value.typeId,
      cover: form.value.cover || '', // 可为空
      days: form.value.days,
    });
    alert('活动发布成功！');
    router.push('/activity');
  } catch (error: any) {
    console.error('发布失败:', error);
  } finally {
    submitting.value = false;
  }
};

onMounted(() => {
  fetchTypes();
});
</script>

<style scoped>
.form-card {
  background: #fff;
  padding: 32px 36px;
  border-radius: 12px;
  max-width: 600px;
  border: 1px solid #eee;
  margin: 0 auto;
}
.form-card h2 {
  font-size: 1.3rem;
  font-weight: 600;
  margin-bottom: 24px;
  letter-spacing: -0.2px;
  display: flex;
  align-items: center;
  gap: 8px;
  color: #1f2937;
}
.form-card h2 i { color: #6366f1; }

.form-group { margin-bottom: 20px; }
.form-group label {
  display: block;
  margin-bottom: 4px;
  font-weight: 500;
  font-size: 0.85rem;
  color: #374151;
}
.form-group label .required { color: #ef4444; margin-left: 2px; }
.form-group input,
.form-group textarea {
  width: 100%;
  padding: 10px 14px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  font-size: 0.9rem;
  font-family: inherit;
  transition: border 0.2s;
  background: #fafafa;
  color: #1f2937;
}
.form-group input:focus,
.form-group textarea:focus {
  outline: none;
  border-color: #6366f1;
  background: #fff;
}
.form-group textarea { resize: vertical; min-height: 70px; }
.form-group .hint {
  font-size: 0.7rem;
  color: #9ca3af;
  margin-top: 2px;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 14px;
}
.type-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 2px;
}
.type-tag-option {
  padding: 4px 14px;
  border-radius: 20px;
  border: 1px solid #e5e7eb;
  background: #fafafa;
  font-size: 0.75rem;
  font-weight: 500;
  color: #374151;
  cursor: pointer;
  transition: all 0.15s;
}
.type-tag-option:hover { border-color: #9ca3af; }
.type-tag-option.active {
  background: #1f2937;
  color: #fff;
  border-color: #1f2937;
}
.loading-types {
  padding: 8px 0;
  color: #9ca3af;
  font-size: 0.85rem;
}

/* 封面图上传样式 */
.cover-upload-wrapper {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.upload-area {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}
.upload-area input[type="file"] {
  display: none;
}
.upload-btn {
  padding: 6px 16px;
  border: 1px dashed #d1d5db;
  border-radius: 6px;
  background: #fafafa;
  color: #374151;
  font-size: 0.85rem;
  cursor: pointer;
  transition: all 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.upload-btn:hover:not(:disabled) {
  border-color: #6366f1;
  background: #f3f4f6;
}
.upload-btn:disabled { opacity: 0.5; cursor: not-allowed; }
.upload-progress {
  font-size: 0.85rem;
  color: #6366f1;
}
.upload-success {
  font-size: 0.85rem;
  color: #10b981;
}
.url-input-wrapper {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}
.url-prefix {
  font-size: 0.8rem;
  color: #9ca3af;
}
.url-input-wrapper input {
  flex: 1;
  min-width: 200px;
}
.cover-preview {
  position: relative;
  display: inline-block;
  margin-top: 8px;
  max-width: 300px;
}
.cover-preview img {
  width: 100%;
  max-height: 160px;
  object-fit: cover;
  border-radius: 8px;
  border: 1px solid #e5e7eb;
}
.remove-cover {
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
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.8rem;
  transition: 0.2s;
}
.remove-cover:hover { background: #000; }

.submit-btn {
  width: 100%;
  padding: 14px;
  background: #6366f1;
  color: #fff;
  border: none;
  border-radius: 10px;
  font-weight: 600;
  font-size: 0.95rem;
  cursor: pointer;
  transition: background 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  margin-top: 6px;
}
.submit-btn:hover:not(:disabled) { background: #4f46e5; }
.submit-btn:disabled { opacity: 0.6; cursor: not-allowed; }

@media (max-width: 768px) {
  .form-card { padding: 24px 20px; }
  .form-row { grid-template-columns: 1fr; }
  .upload-area { flex-direction: column; align-items: stretch; }
  .url-input-wrapper { flex-direction: column; align-items: stretch; }
}
</style>