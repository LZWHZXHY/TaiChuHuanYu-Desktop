<template>
  <div class="settings-view">
    <section class="settings-section">
      <div class="section-header">
        <h3>基础信息</h3>
        <p>定义灵脉的核心身份与愿景</p>
      </div>

      <div class="form-grid">
        <div class="input-group">
          <label>项目标题</label>
          <input v-model="form.name" placeholder="输入标题..." @input="checkChanges" />
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
          <label>当前状态</label>
          <select v-model="form.status" @change="checkChanges">
            <option :value="0">筹备中</option>
            <option :value="1">活跃运行</option>
            <option :value="2">圆满结束</option>
            <option :value="3">已归档</option>
          </select>
        </div>

        <div class="input-group">
          <label>可见性</label>
          <div class="switch-wrapper">
            <label class="checkbox-container">
              <input type="checkbox" v-model="form.isPublic" @change="checkChanges" />
              <span class="checkmark"></span>
              公开此项目
            </label>
          </div>
        </div>
      </div>

      <footer class="settings-footer">
        <button 
          class="save-btn" 
          :disabled="!hasChanges || isUpdating" 
          @click="handleUpdate"
        >
          {{ isUpdating ? '正在同步...' : '保存更改' }}
        </button>
      </footer>
    </section>

    <section class="danger-zone">
      <div class="section-header">
        <h3>危险区域</h3>
      </div>
      <button class="delete-link" @click="handleDelete">解散并抹除此项目</button>
    </section>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, watch } from 'vue';
import projectService from '../../../api/projectService';

const props = defineProps<{
  projectId: string;
  initialData: any;
}>();

const isUpdating = ref(false);
const hasChanges = ref(false);

// 响应式表单
const form = reactive({
  name: '',
  description: '',
  startTime: '',
  endTime: '',
  status: 1,
  isPublic: false
});

// 深度克隆初始值用于对比
let originalData = '';

onMounted(() => {
  if (props.initialData) {
    Object.assign(form, {
      ...props.initialData,
      // 格式化日期为 input[type="date"] 识别的格式
      startTime: props.initialData.startTime?.split('T')[0] || '',
      endTime: props.initialData.endTime?.split('T')[0] || ''
    });
    originalData = JSON.stringify(form);
  }
});

const checkChanges = () => {
  hasChanges.value = JSON.stringify(form) !== originalData;
};

const handleUpdate = async () => {
  isUpdating.value = true;
  
  // 🌟 处理日期：将 "" 转换为 null
  const submitData = {
    ...form,
    startTime: form.startTime || null,
    endTime: form.endTime || null
  };

  try {
    // 传 submitData 而不是直接传 form
    await projectService.updateProject(props.projectId, submitData);
    originalData = JSON.stringify(form);
    hasChanges.value = false;
  } finally {
    isUpdating.value = false;
  }
};

const handleDelete = () => {
  if (confirm('确定要抹除这段灵脉吗？此操作无法撤销。')) {
    // 调用删除接口逻辑
    console.log('删除项目:', props.projectId);
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

.input-group input:focus, 
.input-group textarea:focus {
  border-bottom-color: #1a1a1a;
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
  color: #eee;
  font-size: 0.85rem;
  cursor: pointer;
  transition: color 0.3s;
}

.delete-link:hover {
  color: #ff4757;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: translateY(0); }
}
</style>