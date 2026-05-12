<template>
  <div class="collaboration-portal">
    <header class="portal-header">
      <div class="brand-section">
        <h1 class="title">太初协作</h1>
        <nav class="portal-tabs">
          <button 
            :class="['tab-link', { active: viewMode === 'mine' }]" 
            @click="viewMode = 'mine'"
          >
            我的灵脉
          </button>
          <button 
            :class="['tab-link', { active: viewMode === 'public' }]" 
            @click="viewMode = 'public'"
          >
            公开广场
          </button>
        </nav>
      </div>
      <button class="create-trigger" @click="showCreateModal = true">
        <span class="plus">+</span> 开启新项目
      </button>
    </header>

    <main class="project-grid">
      <div 
        v-for="project in displayProjects" 
        :key="project.id" 
        class="project-entry"
        @click="enterProject(project)"
      >
        <div class="entry-content">
          <div class="entry-header">
            <span class="role-indicator" v-if="viewMode === 'mine'">
              {{ getRoleLabel(project.roleId) }}
            </span>
            <span class="role-indicator joined-tag" v-else-if="project.isJoined">
              已加入
            </span>
            <h2 class="entry-name">{{ project.name }}</h2>
          </div>
          <p class="entry-desc">{{ project.description || '暂无愿景描述' }}</p>
          
          <div class="entry-meta">
            <div class="status-group">
              <span :class="['status-tag', `status-${project.status ?? 0}`]">
                {{ getStatusLabel(project.status ?? 0) }}
              </span>
              <span class="count">{{ project.memberCount || 1 }} 位成员</span>
            </div>
            
            <div class="time-flow" v-if="project.startTime || project.endTime">
              <span v-if="project.startTime">{{ formatShortDate(project.startTime) }}</span>
              <span class="flow-sep" v-if="project.startTime">/</span>
              <span :class="{ 'ongoing': !project.endTime }">
                {{ project.endTime ? formatShortDate(project.endTime) : '持续中' }}
              </span>
            </div>
          </div>
        </div>
      </div>

      <div 
        v-if="viewMode === 'mine'"
        class="project-entry empty-placeholder" 
        @click="showCreateModal = true"
      >
        <div class="placeholder-content">
          <div class="placeholder-icon">+</div>
          <p>新增项目</p>
        </div>
      </div>
    </main>

    <Transition name="fade">
      <div v-if="showCreateModal" class="modal-overlay" @click.self="closeModal">
        <div class="minimal-modal">
          <header class="modal-inner-header">
            <h2>开启新灵脉</h2>
            <p>定义一段新的时空协作</p>
          </header>
          
          <div class="modal-body">
            <div class="input-group">
              <label>项目名称</label>
              <input v-model="form.name" placeholder="为你的构思命名..." autofocus @keyup.enter="handleCreate" />
            </div>
            
            <div class="input-group">
              <label>愿景描述</label>
              <textarea v-model="form.description" placeholder="简述这个项目的终点..." rows="2"></textarea>
            </div>

            <div class="date-range-row">
              <div class="input-group">
                <label>开启时间</label>
                <input type="date" v-model="form.startTime" />
              </div>
              <div class="input-group">
                <label>预期结束 (可选)</label>
                <input type="date" v-model="form.endTime" />
              </div>
            </div>

            <div class="switch-group">
              <label class="checkbox-container">
                <input type="checkbox" v-model="form.isPublic" />
                <span class="checkmark"></span>
                公开此项目（广场可见）
              </label>
            </div>
          </div>

          <footer class="modal-footer">
            <button class="cancel-btn" @click="closeModal">取消</button>
            <button 
              class="confirm-btn" 
              :disabled="!form.name || isSubmitting" 
              @click="handleCreate"
            >
              {{ isSubmitting ? '正在编织...' : '确认创建' }}
            </button>
          </footer>
        </div>
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, reactive, watch, computed } from 'vue';
import { useRouter } from 'vue-router';
import projectService from '../../api/projectService';

const router = useRouter();
const myProjects = ref<any[]>([]);
const publicProjects = ref<any[]>([]);
const viewMode = ref<'mine' | 'public'>('mine');
const showCreateModal = ref(false);
const isSubmitting = ref(false);

const form = reactive({
  name: '',
  description: '',
  isPublic: false,
  startTime: '',
  endTime: ''
});

// 计算属性：当前显示的列表
const displayProjects = computed(() => {
  return viewMode.value === 'mine' ? myProjects.value : publicProjects.value;
});

const getRoleLabel = (role: number) => {
  const labels: Record<number, string> = { 0: 'OWNER', 1: 'DEV', 2: 'GUEST' };
  return labels[role] || 'MEMBER';
};

const getStatusLabel = (status: number) => {
  const labels: Record<number, string> = { 0: '筹备', 1: '活跃', 2: '圆满', 3: '归档' };
  return labels[status] || '未知';
};

const fetchProjects = async () => {
  try {
    if (viewMode.value === 'mine') {
      myProjects.value = await projectService.getMyProjects();
    } else {
      // 假设你在 projectService 中添加了 getPublicProjects 方法
      publicProjects.value = await projectService.getPublicProjects();
    }
  } catch (err) {
    console.error("加载项目失败");
  }
};

// 监听视图模式切换
watch(viewMode, fetchProjects);

onMounted(fetchProjects);

const handleCreate = async () => {
  if (!form.name || isSubmitting.value) return;
  
  isSubmitting.value = true;
  try {
    // 提交前处理日期，防止后端解析空字符串报错
    const payload = {
      ...form,
      startTime: form.startTime || null,
      endTime: form.endTime || null
    };
    const res = await projectService.createProject(payload);
    showCreateModal.value = false;
    router.push(`/Project/project/${res.id}`);
  } finally {
    isSubmitting.value = false;
  }
};

const closeModal = () => {
  showCreateModal.value = false;
  Object.assign(form, { name: '', description: '', isPublic: false, startTime: '', endTime: '' });
};

const enterProject = (project: any) => {
  // 如果已加入，直接进入；如果未加入，跳转到预览或详情页处理申请
  router.push(`/Project/project/${project.id}`);
};

const formatShortDate = (dateStr: string) => {
  if (!dateStr) return '';
  const date = new Date(dateStr);
  return `${date.getFullYear()}.${String(date.getMonth() + 1).padStart(2, '0')}`;
};
</script>

<style scoped>
.collaboration-portal {
  min-height: 100vh;
  background-color: #ffffff;
  padding: 80px 8% 120px;
  color: #1a1a1a;
  font-family: "PingFang SC", "Inter", system-ui, sans-serif;
}

.portal-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  margin-bottom: 80px;
}

.title {
  font-size: 2.2rem;
  font-weight: 300;
  letter-spacing: -0.01em;
  margin: 0;
}

/* 视图切换 Tabs */
.portal-tabs {
  display: flex;
  gap: 32px;
  margin-top: 24px;
}

.tab-link {
  background: none;
  border: none;
  font-size: 0.9rem;
  color: #bbb;
  cursor: pointer;
  padding: 8px 0;
  position: relative;
  transition: color 0.3s;
}

.tab-link.active {
  color: #1a1a1a;
  font-weight: 500;
}

.tab-link.active::after {
  content: '';
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  height: 1px;
  background: #1a1a1a;
}

.create-trigger {
  background: #1a1a1a;
  color: #fff;
  border: none;
  padding: 12px 28px;
  font-size: 0.8rem;
  cursor: pointer;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  border-radius: 2px;
}

.create-trigger:hover {
  background: #444;
  transform: translateY(-2px);
}

.project-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
  gap: 40px;
}

.project-entry {
  background: #fff;
  border: 1px solid #f2f2f2;
  padding: 40px;
  transition: all 0.5s cubic-bezier(0.16, 1, 0.3, 1);
  cursor: pointer;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  min-height: 280px;
}

.project-entry:hover {
  border-color: #e5e5e5;
  transform: translateY(-6px);
  box-shadow: 0 30px 60px rgba(0, 0, 0, 0.05);
}

.role-indicator {
  font-size: 0.6rem;
  font-weight: 700;
  letter-spacing: 0.15em;
  color: #c0c0c0;
  margin-bottom: 16px;
  display: block;
  text-transform: uppercase;
}

.joined-tag {
  color: #1a1a1a;
}

.entry-name {
  font-size: 1.35rem;
  font-weight: 500;
  margin: 0 0 16px 0;
  color: #1a1a1a;
}

.entry-desc {
  font-size: 0.88rem;
  color: #777;
  line-height: 1.8;
  margin: 0;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.entry-meta {
  margin-top: 32px;
  padding-top: 24px;
  border-top: 1px solid #f8f8f8;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.status-group {
  display: flex;
  align-items: center;
  gap: 12px;
}

.status-tag {
  font-size: 0.65rem;
  padding: 2px 8px;
  border-radius: 2px;
}

.status-0 { color: #aaa; background: #f9f9f9; }
.status-1 { color: #fff; background: #1a1a1a; }
.status-2 { color: #1a1a1a; border: 1px solid #1a1a1a; }

.count {
  font-size: 0.75rem;
  color: #999;
}

.time-flow {
  font-size: 0.7rem;
  color: #ccc;
  display: flex;
  gap: 4px;
}

.flow-sep { opacity: 0.5; }
.ongoing { color: #999; font-style: italic; }

.empty-placeholder {
  border: 1px dashed #e5e5e5;
  background: transparent;
  justify-content: center;
  align-items: center;
}

.placeholder-icon {
  font-size: 2rem;
  color: #eee;
  margin-bottom: 10px;
}

/* Modal 样式保持原有并微调 */
.modal-overlay {
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  background: rgba(255, 255, 255, 0.95);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  backdrop-filter: blur(12px);
}

.minimal-modal {
  background: #fff;
  width: 100%;
  max-width: 540px;
  padding: 60px;
  border: 1px solid #eee;
  box-shadow: 0 40px 100px rgba(0,0,0,0.03);
}

.date-range-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 24px;
  margin-bottom: 8px;
}

.input-group label {
  display: block;
  font-size: 0.65rem;
  color: #aaa;
  text-transform: uppercase;
  letter-spacing: 1.5px;
  margin-bottom: 12px;
}

.input-group input, .input-group textarea {
  width: 100%;
  border: none;
  border-bottom: 1px solid #f0f0f0;
  padding: 12px 0;
  font-size: 1.1rem;
  outline: none;
  background: transparent;
}

.modal-footer {
  margin-top: 60px;
  display: flex;
  justify-content: flex-end;
  gap: 24px;
}

.confirm-btn {
  background: #1a1a1a;
  color: #fff;
  border: none;
  padding: 14px 40px;
  cursor: pointer;
  border-radius: 2px;
}

.fade-enter-active, .fade-leave-active { transition: opacity 0.5s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>