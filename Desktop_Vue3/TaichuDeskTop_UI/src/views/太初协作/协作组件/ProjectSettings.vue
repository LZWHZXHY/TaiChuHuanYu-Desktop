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

        <!-- 🌟 完善 1：新增准入策略控制（JoinPolicy） -->
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
          :disabled="!hasChanges || isUpdating" 
          @click="handleUpdate"
        >
          {{ isUpdating ? '正在同步...' : '保存更改' }}
        </button>
      </footer>
    </section>

    <!-- 🌟 完善 2：彻底打通彻底解散并抹除项目的动作 -->
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

const props = defineProps<{
  projectId: string;
  initialData: any;
}>();

// 🌟 新增对外冒泡事件，用于在更新完基础配置后让父组件能监听到并刷新外层大厅数据
const emit = defineEmits(['updated']);
const router = useRouter();

const isUpdating = ref(false);
const isDeleting = ref(false);
const hasChanges = ref(false);

// 响应式表单
const form = reactive({
  name: '',
  description: '',
  startTime: '',
  endTime: '',
  status: 1,
  isPublic: false,
  joinPolicy: 0 // 🌟 补全准入策略响应式初始值
});

// 深度克隆初始值用于对比
let originalData = '';

onMounted(() => {
  if (props.initialData) {
    Object.assign(form, {
      ...props.initialData,
      joinPolicy: props.initialData.joinPolicy ?? 0, // 接收外层输送来的准入策略数据
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
  if (isUpdating.value) return;
  isUpdating.value = true;
  
  // 处理日期：将 "" 转换为 null
  const submitData = {
    ...form,
    startTime: form.startTime || null,
    endTime: form.endTime || null
  };

  try {
    await projectService.updateProject(props.projectId, submitData);
    originalData = JSON.stringify(form);
    hasChanges.value = false;
    
    // 🌟 核心：通知最外层的 ProjectDetail.vue 刷新头部标题与状态面包屑
    emit('updated');
    alert("灵脉印记已成功重构更新。");
  } catch (err) {
    console.error("更新项目元数据失败", err);
    alert("设置同步失败，请检查网络或权限。");
  } finally {
    isUpdating.value = false;
  }
};

// 🌟 真正打通：调用解散项目端点
const handleDelete = async () => {
  const firstConfirm = confirm('确定要抹除这段灵脉吗？此操作将彻底消灭项目旗下一切意图、任务、自定义分栏，且无法撤销！');
  if (!firstConfirm) return;

  const secondConfirm = confirm('【终极警告】再次确认：是否真的彻底将该项目从太初世界中抹除？');
  if (!secondConfirm) return;

  isDeleting.value = true;
  try {
    await projectService.deleteProject(props.projectId);
    alert("项目已成功从灵脉大厅彻底抹除。");
    // 成功解散项目后，直接把用户护送回灵脉大厅
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

/* 针对选择框微调，防止样式在部分浏览器崩塌 */
.input-group select {
  cursor: pointer;
  border-radius: 0;
}

.input-group input:focus, 
.input-group textarea:focus,
.input-group select:focus {
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