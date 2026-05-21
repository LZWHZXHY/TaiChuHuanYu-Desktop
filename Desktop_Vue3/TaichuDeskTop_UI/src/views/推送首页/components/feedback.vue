<template>
  <div class="feedback-container">
    <div class="feedback-header">
      <h3 class="section-title">意见反馈与公示</h3>
      <button class="submit-btn" @click="openModal">我要反馈</button>
    </div>

    <div v-if="loading" class="empty-state">
      <p>加载中...</p>
    </div>
    
    <div v-else-if="feedbackList.length === 0" class="empty-state">
      <p>暂无反馈记录</p>
    </div>

    <ul v-else class="feedback-list">
      <li v-for="item in feedbackList" :key="item.id" class="feedback-item">
        <div class="feedback-meta">
          <!-- 显示反馈的短 ID -->
          <span class="id-text">#{{ item.id.substring(0, 8) }}</span>
          <span class="separator">/</span>

          <span :class="['status-text', getStatusClass(item.status)]">
            {{ getStatusText(item.status) }}
          </span>
          <span class="separator">/</span>

          <!-- 智能显示用户身份 -->
          <span class="user">
            {{ item.isAnonymous ? '匿名用户' : (item.contactInfo || '热心用户') }}
          </span>
          <span class="separator">/</span>

          <span class="date">{{ formatDate(item.createdAt) }}</span>
        </div>
        
        <h4 class="feedback-title">{{ item.content }}</h4>
        
        <div v-if="item.imageUrls" class="feedback-images">
          <img 
            v-for="(img, idx) in item.imageUrls.split(',')" 
            :key="idx" 
            :src="img" 
            alt="反馈配图" 
            class="list-img"
          />
        </div>
      </li>
    </ul>

    <!-- 我要反馈 弹窗 -->
    <Teleport to="body">
      <Transition name="fade">
        <div v-if="isModalOpen" class="modal-overlay" @click.self="closeModal">
          <div class="modal-content">
            <button class="modal-close-btn" @click="closeModal">×</button>
            <h3 class="modal-title">写下你的建议</h3>
            
            <div class="form-body">
              <textarea 
                v-model="formData.content" 
                placeholder="你遇到了什么问题？或是有什么好点子？" 
                rows="5"
                class="minimal-input"
              ></textarea>

              <!-- 图片上传区 -->
              <div class="image-upload-area">
                <div v-for="(url, index) in formData.images" :key="index" class="image-preview">
                  <img :src="url" alt="预览图" />
                  <button class="remove-btn" @click="removeImage(index)">×</button>
                </div>
                <!-- 最多3张图，按钮状态直接绑定 hook 里的 isUploading -->
                <label v-if="formData.images.length < 3" class="upload-btn" :class="{ 'is-uploading': isUploading }">
                  <input type="file" accept="image/png, image/jpeg, image/gif" @change="handleFileUpload" hidden :disabled="isUploading" />
                  <span v-if="isUploading" class="loading-spinner"></span>
                  <span v-else>+</span>
                </label>
              </div>

              <input 
                v-model="formData.contactInfo" 
                type="text" 
                placeholder="怎么称呼你？（选填，邮箱或微信）" 
                class="minimal-input"
              />
              
              <!-- 🌟 底部选项与提交按钮区域 -->
              <div class="form-footer">
                <label class="anonymous-checkbox">
                  <input type="checkbox" v-model="formData.isAnonymous" />
                  <span class="checkmark"></span>
                  匿名提交 (不对外公开身份)
                </label>

                <button class="primary-btn" @click="handleSubmit" :disabled="submitting || isUploading">
                  {{ submitting ? '提交中...' : '提交反馈' }}
                </button>
              </div>
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { feedbackApi, type CreateFeedbackDto } from '@/api/Feedback';
import { useCos } from '@/composables/useCos'; // 🌟 引入你的 COS Hook

// 🌟 初始化你的 COS Hook
const { uploadFile, isUploading } = useCos();

const feedbackList = ref<any[]>([]);
const loading = ref(false);

const isModalOpen = ref(false);
const submitting = ref(false);

// 🌟 补全 isAnonymous 字段
const formData = ref<CreateFeedbackDto>({
  content: '',
  contactInfo: '',
  images: [],
  isAnonymous: false
});

const fetchFeedbacks = async () => {
  loading.value = true;
  try {
    feedbackList.value = await feedbackApi.getPublicFeedbacks(); // 🌟 使用安全脱敏的公开接口
  } catch (error) {
    console.error('获取反馈列表失败', error);
  } finally {
    loading.value = false;
  }
};

onMounted(fetchFeedbacks);

const openModal = () => {
  isModalOpen.value = true;
  document.body.style.overflow = 'hidden';
};

const closeModal = () => {
  isModalOpen.value = false;
  document.body.style.overflow = '';
};

const getStatusText = (status: number) => status === 1 ? '已解决' : '待处理';
const getStatusClass = (status: number) => status === 1 ? 'resolved' : 'pending';

const formatDate = (dateString: string) => {
  if (!dateString) return '';
  const date = new Date(dateString);
  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
};

// 🌟 改造后的文件上传逻辑
const handleFileUpload = async (event: Event) => {
  const target = event.target as HTMLInputElement;
  const file = target.files?.[0];
  if (!file) return;

  if (file.size > 5 * 1024 * 1024) {
    alert('图片大小不能超过 5MB');
    target.value = '';
    return;
  }

  try {
    const result = await uploadFile(file, 'feedbacks');
    formData.value.images.push(result.url);
  } catch (error) {
    alert('图片上传失败');
    console.error(error);
  } finally {
    target.value = ''; 
  }
};

const removeImage = (index: number) => {
  formData.value.images.splice(index, 1);
};

const handleSubmit = async () => {
  if (!formData.value.content.trim()) {
    alert('请填写反馈内容');
    return;
  }

  submitting.value = true;
  try {
    await feedbackApi.submit(formData.value);
    
    // 🌟 提交成功后彻底重置表单（包括 isAnonymous）
    formData.value = { content: '', contactInfo: '', images: [], isAnonymous: false };
    
    closeModal();
    await fetchFeedbacks(); 
  } catch (error) {
    alert('提交失败，请重试');
  } finally {
    submitting.value = false;
  }
};
</script>

<style scoped>
/* ==========================================
   原始容器与列表样式
   ========================================== */
.feedback-container {
  margin-top: 56px;
  padding-top: 40px;
  border-top: 1px solid #f0f2f5;
}

.feedback-header {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  margin-bottom: 32px;
}

.section-title {
  font-size: 0.85rem;
  font-weight: 500;
  color: #8c959f;
  letter-spacing: 0.1em;
  margin: 0;
}

.submit-btn {
  background: transparent;
  border: none;
  color: #1f2328;
  font-size: 0.85rem;
  padding: 0;
  cursor: pointer;
  text-decoration: underline;
  text-underline-offset: 4px;
  transition: color 0.3s ease;
}
.submit-btn:hover { color: #8c959f; }

.empty-state {
  color: #a1aebb;
  font-size: 0.9rem;
  font-weight: 300;
}

.feedback-list {
  list-style: none;
  padding: 0;
  margin: 0;
}

.feedback-item {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-bottom: 48px;
}
.feedback-item:last-child { margin-bottom: 0; }

.feedback-meta {
  font-size: 0.8rem;
  display: flex;
  align-items: center;
  gap: 10px;
  font-family: ui-monospace, SFMono-Regular, "SF Mono", Menlo, Consolas, "Liberation Mono", monospace;
}

.id-text { color: #a1aebb; }
.status-text { font-weight: 500; }
.pending { color: #8c959f; }      /* 待处理：浅灰 */
.resolved { color: #d0d7de; }     /* 已解决：极淡 */

.separator { color: #d0d7de; font-weight: 300; }
.user, .date { color: #a1aebb; }

.feedback-title {
  margin: 0;
  font-size: 1.05rem;
  color: #1f2328;
  line-height: 1.6;
  font-weight: 400;
  white-space: pre-wrap;
}

/* 列表中的图片展示 */
.feedback-images {
  display: flex;
  gap: 8px;
  margin-top: 8px;
}
.list-img {
  width: 60px;
  height: 60px;
  border-radius: 6px;
  object-fit: cover;
  border: 1px solid #f0f2f5;
  opacity: 0.9;
  transition: opacity 0.2s;
}
.list-img:hover { opacity: 1; }

@media (max-width: 600px) {
  .feedback-item { margin-bottom: 36px; }
  .feedback-title { font-size: 1rem; }
}

/* ==========================================
   弹窗与表单样式
   ========================================== */
.modal-overlay {
  position: fixed;
  top: 0; left: 0; width: 100vw; height: 100vh;
  background-color: rgba(31, 35, 40, 0.3);
  backdrop-filter: blur(4px);
  -webkit-backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 9999;
}

.modal-content {
  background: #fff;
  width: 90%;
  max-width: 480px;
  border-radius: 12px;
  padding: 2.5rem 2rem;
  position: relative;
  box-shadow: 0 20px 40px rgba(0,0,0,0.08);
}

.modal-close-btn {
  position: absolute;
  top: 1rem;
  right: 1.2rem;
  background: none;
  border: none;
  font-size: 1.5rem;
  color: #8c959f;
  cursor: pointer;
  line-height: 1;
  transition: color 0.2s;
}
.modal-close-btn:hover { color: #1f2328; }

.modal-title {
  margin: 0 0 1.5rem 0;
  font-size: 1.2rem;
  font-weight: 500;
  color: #1f2328;
}

.form-body {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.minimal-input {
  width: 100%;
  padding: 0.8rem;
  border: 1px solid #d0d7de;
  border-radius: 6px;
  font-size: 0.95rem;
  color: #1f2328;
  outline: none;
  transition: border-color 0.2s;
  background: #f6f8fa;
  font-family: inherit;
  resize: vertical;
  box-sizing: border-box;
}
.minimal-input:focus {
  border-color: #1f2328;
  background: #fff;
}
.minimal-input::placeholder { color: #8c959f; }

/* 图片上传区 */
.image-upload-area {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
}

.image-preview {
  position: relative;
  width: 70px;
  height: 70px;
  border-radius: 6px;
  overflow: hidden;
  border: 1px solid #d0d7de;
}
.image-preview img { width: 100%; height: 100%; object-fit: cover; }
.remove-btn {
  position: absolute;
  top: 2px;
  right: 2px;
  width: 18px;
  height: 18px;
  border-radius: 50%;
  background: rgba(0,0,0,0.5);
  color: #fff;
  border: none;
  font-size: 10px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}
.remove-btn:hover { background: rgba(0,0,0,0.8); }

.upload-btn {
  width: 70px;
  height: 70px;
  border: 1px dashed #d0d7de;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.5rem;
  color: #8c959f;
  cursor: pointer;
  background: #f6f8fa;
  transition: border-color 0.2s, color 0.2s;
}
.upload-btn:hover:not(.is-uploading) {
  border-color: #1f2328;
  color: #1f2328;
}
.upload-btn.is-uploading { opacity: 0.5; cursor: not-allowed; }

.loading-spinner {
  width: 18px;
  height: 18px;
  border: 2px solid #d0d7de;
  border-top-color: #1f2328;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}
@keyframes spin { to { transform: rotate(360deg); } }

/* ==========================================
   🌟 新增：底部布局与极简 Checkbox 样式
   ========================================== */
.form-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 0.5rem;
}

.anonymous-checkbox {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 0.85rem;
  color: #6e7781;
  cursor: pointer;
  user-select: none;
}

.anonymous-checkbox input {
  position: absolute;
  opacity: 0;
  cursor: pointer;
  height: 0;
  width: 0;
}

.checkmark {
  height: 16px;
  width: 16px;
  background-color: #f6f8fa;
  border: 1px solid #d0d7de;
  border-radius: 4px;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
}

.anonymous-checkbox:hover .checkmark {
  border-color: #1f2328;
}

.anonymous-checkbox input:checked ~ .checkmark {
  background-color: #1f2328;
  border-color: #1f2328;
}

.checkmark:after {
  content: "";
  display: none;
  width: 4px;
  height: 8px;
  border: solid white;
  border-width: 0 2px 2px 0;
  transform: rotate(45deg);
  margin-bottom: 2px;
}

.anonymous-checkbox input:checked ~ .checkmark:after {
  display: block;
}

.primary-btn {
  background: #1f2328;
  color: #fff;
  border: none;
  padding: 0.6rem 1.5rem;
  border-radius: 6px;
  font-size: 0.95rem;
  cursor: pointer;
  transition: opacity 0.2s;
}
.primary-btn:hover { opacity: 0.85; }
.primary-btn:disabled { opacity: 0.5; cursor: not-allowed; }

/* 动画 */
.fade-enter-active, .fade-leave-active { transition: opacity 0.25s ease; }
.fade-enter-active .modal-content, .fade-leave-active .modal-content { transition: transform 0.25s cubic-bezier(0.16, 1, 0.3, 1); }
.fade-enter-from, .fade-leave-to { opacity: 0; }
.fade-enter-from .modal-content { transform: translateY(15px) scale(0.98); }
.fade-leave-to .modal-content { transform: translateY(10px) scale(0.98); }
</style>