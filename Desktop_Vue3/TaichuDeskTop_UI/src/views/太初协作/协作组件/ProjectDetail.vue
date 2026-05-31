<template>
  <div class="project-detail-wrapper" v-if="project">
    <header class="detail-header">
      <div class="header-left">
        <nav class="breadcrumb" @click="router.push('/Project')">
          <span class="back-icon">←</span>
          <span class="back-text">返回灵脉大厅</span>
        </nav>
        <div class="title-group">
          <h1 class="project-name">{{ project.name }}</h1>
          <p class="project-description" v-if="project.description">
            {{ project.description }}
          </p>
        </div>
      </div>
      
      <div class="header-right">
        <div class="meta-card">
          <div class="meta-row">
            <span class="label">当前状态</span>
            <span :class="['status-badge', `status-${project.status ?? 1}`]">
              {{ getStatusLabel(project.status ?? 1) }}
            </span>
          </div>
          <div class="meta-row" v-if="project.startTime">
            <span class="label">时空周期</span>
            <span class="value">
              {{ formatTime(project.startTime) }} — {{ project.endTime ? formatTime(project.endTime) : '持续编织' }}
            </span>
          </div>
        </div>
      </div>
    </header>

    <nav class="module-nav">
      <div class="nav-container">
        <button 
          v-for="tab in tabs" 
          :key="tab.id"
          :class="['nav-link', { 'is-active': currentTab === tab.id }]"
          @click="currentTab = tab.id"
        >
          {{ tab.name }}
          <span class="active-indicator"></span>
        </button>
      </div>
    </nav>

    <main class="content-viewport">
      <Transition name="view-fade" mode="out-in">
        <div :key="currentTab" class="module-container">
          
          <component 
            :is="activeComponent"
            :projectId="project.id" 
            :initialData="project"
            @updated="refreshProject"
          />
          
        </div>
      </Transition>
    </main>
  </div>

  <div v-else class="loading-state">
    <div class="loading-pulse"></div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch, computed, h } from 'vue'; // 🌟 去掉了未使用的 shallowRef 导入
import { useRoute, useRouter } from 'vue-router';
import projectService from '../../../api/projectService'; 

// 引入你的真实业务组件
import ProjectKanban from './ProjectKanban.vue';
import ProjectSettings from './ProjectSettings.vue';
import ProjectMember from './ProjectMember.vue';
import ProjectTimeline from './ProjectTimeline.vue';


const route = useRoute();
const router = useRouter();
const project = ref<any>(null);
const currentTab = ref('kanban');

const tabs = [
  { id: 'kanban', name: '任务看板' },
  { id: 'timeline', name: '演进线' },
  { id: 'members', name: '共建者' },
  { id: 'settings', name: '项目配置' }
];

/**
 * 局部内联占位渲染函数
 */
const renderPlaceholder = () => {
  const currentTabName = tabs.find(t => t.id === currentTab.value)?.name || '';
  return h('div', { class: 'layout-placeholder' }, [
    h('div', { class: 'placeholder-content' }, [
      h('div', { class: 'placeholder-icon' }, '⠿'),
      h('h3', `${currentTabName}模块已就绪`),
      h('p', '等待接入具体的业务逻辑组件')
    ])
  ]);
};

/**
 * 🌟 路由静态映射表
 * 移除了内部零散的 shallowRef，交由下方的计算属性统一安全解析
 */
const componentMap: Record<string, any> = {
  kanban: ProjectKanban,
  settings: ProjectSettings,
  timeline: ProjectTimeline,
  members: ProjectMember
};

// 🌟 动态计算视口挂载项，自动防御 Ref 包装问题
const activeComponent = computed(() => {
  const target = componentMap[currentTab.value] || renderPlaceholder;
  // 如果当前对象仍然是个 Ref 节点，则自动解包，确保丢给 <component :is> 的永远是原生组件
  return target && target.__v_isRef ? target.value : target;
});

const getStatusLabel = (status: number) => {
  const labels: Record<number, string> = { 0: '筹备', 1: '活跃', 2: '圆满', 3: '归档' };
  return labels[status] || '未知';
};

const formatTime = (date: string) => {
  if (!date) return '';
  return new Date(date).toLocaleDateString('zh-CN', { year: 'numeric', month: '2-digit' });
};

const refreshProject = async () => {
  const projectId = route.params.id as string;
  if (!projectId) return;
  try {
    const data = await projectService.getProjectSettings(projectId);
    project.value = data;
  } catch (err) {
    console.error("刷新灵脉细节失败", err);
  }
};

watch(() => route.params.id, () => {
  refreshProject();
});

onMounted(refreshProject);
</script>

<style scoped>
/* 占位符样式完美保留在原处，通过内联组件的类名激活 */
.layout-placeholder {
  background: #fff;
  border: 1px solid #f5f5f5;
  height: 500px;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 40px 80px rgba(0,0,0,0.02);
}

.placeholder-content {
  text-align: center;
}

.placeholder-icon {
  font-size: 3rem;
  color: #eee;
  margin-bottom: 20px;
}

.placeholder-content h3 {
  font-size: 1.1rem;
  font-weight: 400;
  color: #ccc;
  margin-bottom: 8px;
}

.placeholder-content p {
  font-size: 0.8rem;
  color: #ddd;
}

/* 详情大厅的基础样式保持一致 */
.project-detail-wrapper {
  min-height: 100vh;
  background-color: #ffffff;
  color: #1a1a1a;
  padding: 0 8%;
  display: flex;
  flex-direction: column;
}

.detail-header {
  padding: 80px 0 60px;
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
}

.breadcrumb {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: #ccc;
  font-size: 0.8rem;
  cursor: pointer;
  margin-bottom: 32px;
  transition: all 0.3s;
}
.breadcrumb:hover { color: #1a1a1a; transform: translateX(-4px); }

.project-name {
  font-size: 2.8rem;
  font-weight: 300;
  letter-spacing: -1.5px;
  margin: 0 0 16px 0;
}

.project-description {
  color: #666;
  font-size: 1rem;
  line-height: 1.8;
  max-width: 680px;
  font-weight: 300;
}

.meta-card {
  margin-top: 20px;
  padding: 24px;
  background: #fff;
  border: 1px solid #f9f9f9;
  box-shadow: 0 10px 30px rgba(0,0,0,0.02); 
}

.meta-row {
  margin-bottom: 12px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.meta-row:last-child { margin-bottom: 0; }

.meta-row .label {
  font-size: 0.6rem;
  color: #bbb;
  text-transform: uppercase;
  letter-spacing: 2px;
}

.meta-row .value { font-size: 0.85rem; color: #444; }

.status-badge {
  font-size: 0.75rem;
  font-weight: 500;
  letter-spacing: 1px;
}
.status-1 { color: #1a1a1a; }

.module-nav {
  margin-bottom: 50px;
  border-bottom: 1px solid #f2f2f2;
}

.nav-container {
  display: flex;
  gap: 50px;
}

.nav-link {
  background: none;
  border: none;
  padding: 20px 0;
  font-size: 0.95rem;
  color: #aaa;
  cursor: pointer;
  position: relative;
  transition: color 0.4s cubic-bezier(0.16, 1, 0.3, 1);
}

.nav-link:hover { color: #1a1a1a; }
.nav-link.is-active { color: #1a1a1a; font-weight: 500; }

.active-indicator {
  position: absolute;
  bottom: -1px;
  left: 0;
  width: 0;
  height: 1.5px;
  background: #1a1a1a;
  transition: width 0.4s cubic-bezier(0.16, 1, 0.3, 1);
}
.nav-link.is-active .active-indicator { width: 100%; }

.content-viewport {
  flex: 1;
  padding-bottom: 120px;
}

.view-fade-enter-active,
.view-fade-leave-active {
  transition: all 0.5s cubic-bezier(0.16, 1, 0.3, 1);
}

.view-fade-enter-from { opacity: 0; transform: translateY(15px); }
.view-fade-leave-to { opacity: 0; transform: translateY(-15px); }

.loading-state {
  height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
}

.loading-pulse {
  width: 40px;
  height: 1px;
  background: #eee;
  animation: pulse 1.5s infinite;
}

@keyframes pulse {
  0% { transform: scaleX(1); opacity: 1; }
  50% { transform: scaleX(2); opacity: 0.3; }
  100% { transform: scaleX(1); opacity: 1; }
}
</style>