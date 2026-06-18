<template>
  <div class="settings-view">
    <section class="settings-section">
      <div class="section-header">
        <h3>基础信息</h3>
        <p>定义灵脉的核心身份与愿景</p>
      </div>

      <div class="form-grid">
        <div class="input-group full-width cover-upload-group">
          <label>灵脉封面</label>
          <div class="cover-uploader">
            <div v-if="form.coverUrl" class="cover-preview-wrapper">
              <img :src="form.coverUrl" class="cover-image-preview" alt="项目封面" />
              <button class="remove-cover-btn" @click="removeCover" :disabled="isCosUploading">移除封面</button>
            </div>
            <div v-else class="upload-placeholder-box" @click="triggerFileInput">
              <span class="upload-icon">{{ isCosUploading ? '⏳' : '✦' }}</span>
              <span class="upload-text">
                {{ isCosUploading ? `正在上载到太初云端 (${cosProgress}%)` : '点击描绘灵脉封面' }}
              </span>
            </div>
            <input 
              ref="fileInputRef" 
              type="file" 
              accept="image/*" 
              class="hidden-file-input" 
              @change="handleFileChange" 
            />
          </div>
        </div>

        <div class="input-group">
          <label>项目标题</label>
          <input v-model="form.name" placeholder="输入标题..." @input="checkChanges" />
        </div>

        <div class="input-group">
          <label>当前状态</label>
          <select v-model="form.status" @change="checkChanges">
            <option :value="0">筹备中</option>
            <option :value="1">活跃运行</option>
            <option :value="2">圆满结束</option>
            <option :value="3">已归档</option>
          </select>
        </div>

        <div class="input-group full-width">
          <label>愿景简介</label>
          <textarea 
            v-model="form.description" 
            placeholder="描述这个项目的终点..." 
            rows="3"
            @input="checkChanges"
          ></textarea>
        </div>

        <div class="input-group">
          <label>开启时间</label>
          <input type="date" v-model="form.startTime" @change="checkChanges" />
        </div>
        <div class="input-group">
          <label>结束时间 (不填则持续编织)</label>
          <input type="date" v-model="form.endTime" @change="checkChanges" />
        </div>

        <div class="input-group">
          <label>准入策略</label>
          <select v-model="form.joinPolicy" @change="checkChanges">
            <option :value="0">仅限邀请 (主理人主动引入)</option>
            <option :value="1">允许申请 (需掌控者审批通过)</option>
            <option :value="2">自由加入 (任何人可直接融入)</option>
          </select>
        </div>

        <div class="input-group">
          <label>可见性</label>
          <div class="switch-wrapper">
            <label class="checkbox-container">
              <input type="checkbox" v-model="form.isPublic" @change="checkChanges" />
              <span class="checkmark"></span>
              公开此项目（广场可见）
            </label>
          </div>
        </div>
      </div>

      <footer class="settings-footer">
        <button 
          class="save-btn" 
          :disabled="!hasChanges || isUpdating || isCosUploading" 
          @click="handleUpdate"
        >
          {{ isUpdating ? '正在同步...' : '保存更改' }}
        </button>
      </footer>
    </section>

    <section class="danger-zone">
      <div class="section-header">
        <h3>危险区域</h3>
        <p>警告：抹除操作将把此灵脉及旗下所有意图、分栏从太初世界彻底降维消灭</p>
      </div>
      <button class="delete-link" :disabled="isDeleting" @click="handleDelete">
        {{ isDeleting ? '正在抹除...' : '解散并抹除此项目' }}
      </button>
    </section>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import projectService from '../../../api/projectService';
import { useCos } from '@/composables/useCos'; // 🌟 引入你上传的腾讯云 COS 上传封装组件

const props = defineProps<{
  projectId: string;
  initialData: any;
}>();

const emit = defineEmits(['updated']);
const router = useRouter();

const isUpdating = ref(false);
const isDeleting = ref(false);
const hasChanges = ref(false);

// 🌟 引入 COS 相关响应式状态和方法
const fileInputRef = ref<HTMLInputElement | null>(null);
const { uploadFile, isUploading: isCosUploading, progress: cosProgress } = useCos();

// 响应式表单
const form = reactive({
  name: '',
  description: '',
  coverUrl: '', // 🌟 补全项目封面图的响应式字段
  startTime: '',
  endTime: '',
  status: 1,
  isPublic: false,
  joinPolicy: 0 
});

let originalData = '';

onMounted(() => {
  if (props.initialData) {
    Object.assign(form, {
      ...props.initialData,
      coverUrl: props.initialData.coverUrl || '', // 接收后端同步而来的封面 URL 属性
      joinPolicy: props.initialData.joinPolicy ?? 0, 
      startTime: props.initialData.startTime?.split('T')[0] || '',
      endTime: props.initialData.endTime?.split('T')[0] || ''
    });
    originalData = JSON.stringify(form);
  }
});

const checkChanges = () => {
  hasChanges.value = JSON.stringify(form) !== originalData;
};

// 🌟 触发隐藏的文件选择框
const triggerFileInput = () => {
  if (isCosUploading.value) return;
  fileInputRef.value?.click();
};

// 🌟 捕获文件更改事件，执行基于腾讯云 COS 的对象存储直传
const handleFileChange = async (event: Event) => {
  const target = event.target as HTMLInputElement;
  const file = target.files?.[0];
  if (!file) return;

  // 简单大小验证保护（示例限制 5MB）
  if (file.size > 5 * 1024 * 1024) {
    alert("封面图容量过大，请保持在 5MB 以内。");
    return;
  }

  try {
    // 注入至项目专属的封面路径文件夹中
    const res = await uploadFile(file, `projects/${props.projectId}/cover`);
    form.coverUrl = res.url; // 完美捕获自定义 CDN 域名拼接出的图片直链
    checkChanges();
  } catch (err) {
    console.error("COS 封面图上传出错:", err);
    alert("封面上传至云端网络崩溃，请检查密钥配置。");
  } finally {
    // 清空 input 确保同一张图连续选择依然可被正常捕获
    if (fileInputRef.value) fileInputRef.value.value = '';
  }
};

// 🌟 移除现有封面图
const removeCover = () => {
  form.coverUrl = '';
  checkChanges();
};

const handleUpdate = async () => {
  if (isUpdating.value || isCosUploading.value) return;
  isUpdating.value = true;
  
  const submitData = {
    ...form,
    startTime: form.startTime || null,
    endTime: form.endTime || null
  };

  try {
    await projectService.updateProject(props.projectId, submitData);
    originalData = JSON.stringify(form);
    hasChanges.value = false;
    
    emit('updated');
    alert("灵脉印记已成功重构更新。");
  } catch (err) {
    console.error("更新项目元数据失败", err);
    alert("设置同步失败，请检查网络或权限。");
  } finally {
    isUpdating.value = false;
  }
};

const handleDelete = async () => {
  const firstConfirm = confirm('确定要抹除这段灵脉吗？此操作将彻底消灭项目旗下一切意图、任务、自定义分栏，且无法撤销！');
  if (!firstConfirm) return;

  const secondConfirm = confirm('【终极警告】再次确认：是否真的彻底将该项目从太初世界中抹除？');
  if (!secondConfirm) return;

  isDeleting.value = true;
  try {
    await projectService.deleteProject(props.projectId);
    alert("项目已成功从灵脉大厅彻底抹除。");
    router.push('/Project');
  } catch (err) {
    console.error('解散项目失败:', err);
    alert('抹除失败，可能由于您并非该项目的超级管理员（Owner）。');
  } finally {
    isDeleting.value = false;
  }
};
</script>

<style scoped>
.settings-view {
  max-width: 800px;
  animation: fadeIn 0.6s ease;
}

.settings-section {
  margin-bottom: 80px;
}

.section-header {
  margin-bottom: 40px;
}

.section-header h3 {
  font-size: 1.1rem;
  font-weight: 500;
  margin-bottom: 8px;
  letter-spacing: 1px;
}

.section-header p {
  font-size: 0.85rem;
  color: #bbb;
  line-height: 1.5;
}

/* 网格布局 */
.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 40px 60px;
}

.full-width {
  grid-column: span 2;
}

/* 极致简约的输入组 */
.input-group label {
  display: block;
  font-size: 0.65rem;
  color: #ccc;
  text-transform: uppercase;
  letter-spacing: 1.5px;
  margin-bottom: 12px;
}

.input-group input, 
.input-group textarea, 
.input-group select {
  width: 100%;
  border: none;
  border-bottom: 1px solid #f0f0f0;
  padding: 12px 0;
  font-size: 1rem;
  color: #1a1a1a;
  outline: none;
  background: transparent;
  transition: border-color 0.3s;
}

.input-group select {
  cursor: pointer;
  border-radius: 0;
}

.input-group input:focus, 
.input-group textarea:focus,
.input-group select:focus {
  border-bottom-color: #1a1a1a;
}

/* 🌟 新增：极简轻量化的封面上传器外观样式 */
.cover-upload-group {
  margin-bottom: 10px;
}
.cover-uploader {
  width: 100%;
  position: relative;
}
.upload-placeholder-box {
  width: 100%;
  height: 160px;
  border: 1px dashed #e0e0e0;
  background: #fafafa;
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  gap: 12px;
  cursor: pointer;
  transition: all 0.3s cubic-bezier(0.16, 1, 0.3, 1);
}
.upload-placeholder-box:hover {
  border-color: #1a1a1a;
  background: #f5f5f5;
}
.upload-icon {
  font-size: 1.6rem;
  color: #aaa;
}
.upload-text {
  font-size: 0.8rem;
  color: #888;
  font-weight: 300;
}
.cover-preview-wrapper {
  position: relative;
  width: 100%;
  height: 200px;
  overflow: hidden;
  border: 1px solid #f0f0f0;
}
.cover-image-preview {
  width: 100%;
  height: 100%;
  object-fit: cover;
}
.remove-cover-btn {
  position: absolute;
  bottom: 12px;
  right: 12px;
  background: rgba(255, 255, 255, 0.9);
  backdrop-filter: blur(4px);
  border: 1px solid #ddd;
  padding: 6px 14px;
  font-size: 0.75rem;
  color: #ff4757;
  cursor: pointer;
  transition: all 0.2s;
}
.remove-cover-btn:hover {
  background: #ff4757;
  color: #fff;
  border-color: #ff4757;
}
.hidden-file-input {
  display: none;
}

.switch-wrapper {
  padding: 12px 0;
}

/* 底部按钮 */
.settings-footer {
  margin-top: 60px;
  display: flex;
  justify-content: flex-start;
}

.save-btn {
  background: #1a1a1a;
  color: #fff;
  border: none;
  padding: 14px 40px;
  font-size: 0.85rem;
  cursor: pointer;
  border-radius: 2px;
  transition: all 0.3s;
}

.save-btn:disabled {
  background: #f5f5f5;
  color: #ccc;
  cursor: not-allowed;
}

/* 危险区样式 */
.danger-zone {
  border-top: 1px solid #f9f9f9;
  padding-top: 60px;
}

.delete-link {
  background: none;
  border: none;
  color: #bbb;
  font-size: 0.85rem;
  cursor: pointer;
  transition: color 0.3s;
  padding: 8px 0;
}

.delete-link:hover {
  color: #ff4757;
}

.delete-link:disabled {
  color: #eee;
  cursor: not-allowed;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: translateY(0); }
}
</style>