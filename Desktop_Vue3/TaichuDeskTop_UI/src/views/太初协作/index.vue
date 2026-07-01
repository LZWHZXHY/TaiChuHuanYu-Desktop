<template>
  <div class="collaboration-portal">
    <header class="portal-header">
      <div class="brand-section">
        <h1 class="title">太初协作</h1>
        
        <div v-if="quotaInfo" class="quota-dashboard">
          <span class="quota-text">
            活跃灵脉负载：<strong>{{ quotaInfo.activeCount }}</strong> / {{ quotaInfo.maxCount }}
          </span>
          <div class="quota-progress-track">
            <div 
              class="quota-progress-bar" 
              :class="{ 'is-full': quotaInfo.isFull }"
              :style="{ width: `${(quotaInfo.activeCount / quotaInfo.maxCount) * 100}%` }"
            ></div>
          </div>
        </div>

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

      <button 
        class="create-trigger" 
        :class="{ 'quota-locked': quotaInfo?.isFull }"
        @click="triggerCreateModal"
      >
        <span class="plus">{{ quotaInfo?.isFull ? '⚠️' : '+' }}</span> 
        {{ quotaInfo?.isFull ? '活跃灵脉已满' : '开启新项目' }}
      </button>
    </header>

    <main class="project-grid">
      <div 
        v-for="project in displayProjects" 
        :key="project.id" 
        class="project-entry"
        :class="{ 'actionable-entry': viewMode === 'public' && !project.isJoined }"
        @click="enterProject(project)"
      >
        <div class="entry-content">
          <div class="entry-header">
            <template v-if="viewMode === 'mine'">
              <span class="role-indicator">{{ getRoleLabel(project.roleId) }}</span>
            </template>
            <template v-else>
              <span class="role-indicator joined-tag" v-if="project.isJoined">已加入</span>
              <span class="role-indicator pending-tag" v-else-if="project.hasApplied">申请中</span>
              <span class="role-indicator open-tag" v-else-if="project.joinPolicy === 2">直接加入</span>
              <span class="role-indicator apply-tag" v-else-if="project.joinPolicy === 1">申请加入</span>
              <span class="role-indicator lock-tag" v-else>仅限邀请</span>
            </template>
            <span class="role-indicator">
              {{ viewMode === 'mine' ? getRoleLabel(project.roleId) : 'OWNER' }} 
              | {{ project.ownerName }}
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
        :class="{ 'placeholder-locked': quotaInfo?.isFull }"
        @click="triggerCreateModal"
      >
        <div class="placeholder-content">
          <div class="placeholder-icon">{{ quotaInfo?.isFull ? '⚠️' : '+' }}</div>
          <p>{{ quotaInfo?.isFull ? '灵脉负载已满' : '新增项目' }}</p>
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

    <Transition name="fade">
      <div v-if="showApplyModal" class="modal-overlay" @click.self="closeApplyModal">
        <div class="minimal-modal">
          <header class="modal-inner-header">
            <h2>申请加入灵脉</h2>
            <p>向项目发起人提交你的协同意向</p>
          </header>
          
          <div class="modal-body">
            <div class="apply-target-preview">
              <label>目标项目灵脉</label>
              <h3>{{ selectedProject?.name }}</h3>
            </div>

            <div class="input-group">
              <label>申请寄语 / 留言</label>
              <textarea 
                v-model="applyMessage" 
                placeholder="阐述你想在此项目中扮演的角色或共建想法..." 
                rows="3"
                autofocus
              ></textarea>
            </div>
          </div>

          <footer class="modal-footer">
            <button class="cancel-btn" @click="closeApplyModal">暂不加入</button>
            <button 
              class="confirm-btn" 
              :disabled="isProcessingApply" 
              @click="handleApplySubmit"
            >
              {{ isProcessingApply ? '正在传书...' : '发送申请' }}
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
// 🌟 核心改动：接入你写好的全局 Pinia Store
import { useUserStore } from '../../stores/user'; 

const router = useRouter();
const userStore = useUserStore(); // 🌟 实例化 Store

const myProjects = ref<any[]>([]);
const publicProjects = ref<any[]>([]);
const viewMode = ref<'mine' | 'public'>('mine');

// 🌟 核心改动：通过计算属性直接代理全局 Store 的数据负载，严格采用小驼峰对齐
const quotaInfo = computed(() => {
  return (userStore.userInfo as any)?.projectQuota || null;
});

// 创建灵脉状态
const showCreateModal = ref(false);
const isSubmitting = ref(false);
const form = reactive({
  name: '',
  description: '',
  isPublic: false,
  startTime: '',
  endTime: ''
});

// 申请加入状态
const showApplyModal = ref(false);
const isProcessingApply = ref(false);
const applyMessage = ref('');
const selectedProject = ref<any>(null);

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

// 🌟 核心优化：不再发起点对点的孤立 me 接口调用，而是直接借由全局 Store 驱动
const fetchProjects = async () => {
  try {
    if (viewMode.value === 'mine') {
      myProjects.value = await projectService.getMyProjects();
    } else {
      publicProjects.value = await projectService.getPublicProjects();
    }

    // 🌟 保险逻辑：如若别的全局组件没有提前加载用户信息，这里顺手拉起，绝不产生二次并发
    if (!userStore.userInfo) {
      await userStore.fetchUserInfo();
    }
  } catch (err) {
    console.error("加载项目失败");
  }
};

// 🌟 完美承袭你原有版本的单次呼叫逻辑
watch(viewMode, fetchProjects);
onMounted(fetchProjects);

// 🌟 新增：拦截点击，确保额度已满时在前端即时拦截，严格校准小驼峰
const triggerCreateModal = () => {
  if (quotaInfo.value && quotaInfo.value.isFull) {
    alert(`您的活跃灵脉负载已达上限（${quotaInfo.value.activeCount}/${quotaInfo.value.maxCount}）。\n请前往项目配置封存闲置项目释放额度，或去交易行购置更多空间。`);
    return;
  }
  showCreateModal.value = true;
};

const handleCreate = async () => {
  if (!form.name || isSubmitting.value) return;
  
  isSubmitting.value = true;
  try {
    const payload = {
      ...form,
      startTime: form.startTime || null,
      endTime: form.endTime || null
    };
    const res = await projectService.createProject(payload);
    showCreateModal.value = false;

    // 🌟 体验大升级：创建项目成功后，强刷一次全局用户信息，让额度看板数字瞬间+1
    await userStore.fetchUserInfo();

    router.push(`/Project/project/${res.id}`);
  } finally {
    isSubmitting.value = false;
  }
};

const closeModal = () => {
  showCreateModal.value = false;
  Object.assign(form, { name: '', description: '', isPublic: false, startTime: '', endTime: '' });
};

// 智能处理卡片点击动作
const enterProject = async (project: any) => {
  if (viewMode.value === 'mine' || project.isJoined) {
    router.push(`/Project/project/${project.id}`);
    return;
  }

  if (project.hasApplied) {
    alert("该灵脉的加入申请正通过飞鸽传递中，请静候掌控者审阅。");
    return;
  }

  if (project.joinPolicy === 2) {
    try {
      await projectService.joinProject(project.id, { message: '' });
      project.isJoined = true;
      project.memberCount = (project.memberCount || 0) + 1;
      alert("已成功融入该项目灵脉！");
    } catch (err: any) {
      alert(err.response?.data || "自由加入失败");
    }
    return;
  }

  if (project.joinPolicy === 1) {
    selectedProject.value = project;
    showApplyModal.value = true;
    return;
  }

  alert("该灵脉隐匿于现世，无法主动申请，需通过主理人点对点引入。");
};

const handleApplySubmit = async () => {
  if (!selectedProject.value || isProcessingApply.value) return;

  isProcessingApply.value = true;
  try {
    await projectService.joinProject(selectedProject.value.id, {
      message: applyMessage.value
    });
    
    selectedProject.value.hasApplied = true;
    
    alert("申请传书成功，已递交至掌控者。");
    closeApplyModal();
  } catch (err: any) {
    alert(err.response?.data || "递交申请失败");
  } finally {
    isProcessingApply.value = false;
  }
};

const closeApplyModal = () => {
  showApplyModal.value = false;
  selectedProject.value = null;
  applyMessage.value = '';
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

/* 🌟 将品牌区改为纵向弹性，优雅容纳额度条 */
.brand-section {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.title {
  font-size: 2.2rem;
  font-weight: 300;
  letter-spacing: -0.01em;
  margin: 0;
}

/* 🌟 新增：额度条美化控制 */
.quota-dashboard {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-top: 8px;
}
.quota-text {
  font-size: 0.8rem;
  color: #888;
  font-weight: 300;
}
.quota-text strong {
  color: #1a1a1a;
  font-weight: 600;
}
.quota-progress-track {
  width: 120px;
  height: 3px;
  background: #f0f0f0;
  border-radius: 2px;
  overflow: hidden;
}
.quota-progress-bar {
  height: 100%;
  background: #1a1a1a;
  transition: width 0.4s cubic-bezier(0.16, 1, 0.3, 1);
}
.quota-progress-bar.is-full {
  background: #ff4d4f;
}

.portal-tabs {
  display: flex;
  gap: 32px;
  margin-top: 16px; /* 微调间距 */
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

/* 🌟 新增：额度被锁定时按钮的外观弱化 */
.create-trigger.quota-locked {
  background: #f5f5f5;
  color: #bbb;
  border: 1px solid #e5e5e5;
  cursor: pointer;
}
.create-trigger.quota-locked:hover {
  transform: none;
  background: #f5f5f5;
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

.actionable-entry:hover {
  border-color: #cbd5e1;
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

.joined-tag { color: #1a1a1a; }
.pending-tag { color: #d97706; } 
.open-tag { color: #059669; }    
.apply-tag { color: #2563eb; }   
.lock-tag { color: #94a3b8; }    

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
.status-3 { color: #888; background: #f0f0f0; text-decoration: line-through; } /* 🌟 封存项目线 */

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
/* 🌟 新增：额度满时占位卡的警示色 */
.empty-placeholder.placeholder-locked:hover {
  border-color: #ff4d4f;
}
.empty-placeholder.placeholder-locked .placeholder-icon {
  color: #ff4d4f;
}

.placeholder-icon {
  font-size: 2rem;
  color: #eee;
  margin-bottom: 10px;
}

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

.modal-inner-header h2 {
  font-size: 1.4rem;
  font-weight: 400;
  margin: 0 0 8px 0;
}

.modal-inner-header p {
  font-size: 0.8rem;
  color: #999;
  margin: 0 0 40px 0;
}

.apply-target-preview {
  margin-bottom: 32px;
}
.apply-target-preview label {
  display: block;
  font-size: 0.65rem;
  color: #aaa;
  text-transform: uppercase;
  letter-spacing: 1.5px;
  margin-bottom: 8px;
}
.apply-target-preview h3 {
  font-size: 1.2rem;
  font-weight: 400;
  margin: 0;
  color: #1a1a1a;
}

.date-range-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 24px;
  margin-bottom: 8px;
}

.input-group {
  margin-bottom: 32px;
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

.input-group textarea {
  border: 1px solid #f0f0f0;
  padding: 12px;
  font-size: 0.95rem;
  resize: none;
  border-radius: 2px;
}
.input-group textarea:focus {
  border-color: #1a1a1a;
}

.modal-footer {
  margin-top: 60px;
  display: flex;
  justify-content: flex-end;
  gap: 24px;
}

.cancel-btn {
  background: none;
  border: none;
  color: #999;
  padding: 14px 24px;
  font-size: 0.85rem;
  cursor: pointer;
}
.cancel-btn:hover { color: #1a1a1a; }

.confirm-btn {
  background: #1a1a1a;
  color: #fff;
  border: none;
  padding: 14px 40px;
  font-size: 0.85rem;
  cursor: pointer;
  border-radius: 2px;
}
.confirm-btn:disabled {
  background: #f5f5f5;
  color: #ccc;
  cursor: not-allowed;
}

.fade-enter-active, .fade-leave-active { transition: opacity 0.5s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>