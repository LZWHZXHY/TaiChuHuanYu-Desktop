<template>
  <div class="project-detail-wrapper" v-if="project">
    <p v-if="loadError" role="alert">{{ loadError }} <button @click="refreshProject">重试</button></p>
    <header class="detail-header">
      <div class="header-left">
        <router-link class="breadcrumb" to="/Project">
          <span class="back-icon">←</span>
          <span class="back-text">返回协作大厅</span>
        </router-link>
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
<p v-if="loadError" role="alert">{{ loadError }} <button @click="refreshProject">重试</button></p><div v-else class="loading-pulse"></div>
  </div>
</template>

<script setup lang="ts">
import { ref, onUnmounted, watch, computed } from 'vue'; // 🌟 去掉了未使用的 shallowRef 导入
import { useRoute } from 'vue-router';
import projectService from '../../../api/projectService'; 

// 引入你的真实业务组件
import ProjectKanban from './ProjectKanban.vue';
import ProjectSettings from './ProjectSettings.vue';
import ProjectMember from './ProjectMember.vue';
import ProjectTimeline from './ProjectTimeline.vue';
import ProjectDocument from './ProjectDocument.vue';
import ProjectReports from './ProjectReports.vue'; // 🌟 引入汇报组件
import ProjectDeliveries from './ProjectDeliveries.vue';
const route = useRoute();
const project = ref<any>(null), loadError = ref('');
let loadVersion = 0;
const currentTab = ref('kanban');

const tabs = [
  { id: 'kanban', name: '任务看板' },
  { id: 'timeline', name: '演进线' },
  { id: 'members', name: '成员' },
  { id: 'deliveries', name: '履约验收' }, // 🌟 新增的专门记账本
  { id: 'reports', name: '工作汇报' }, // 🌟 新增项
  { id: 'document', name: '文档'},
  { id: 'settings', name: '项目配置' }
];

const componentMap: Record<string, any> = {
  kanban: ProjectKanban,
  settings: ProjectSettings,
  timeline: ProjectTimeline,
  reports: ProjectReports,
  deliveries: ProjectDeliveries, // 🌟 挂载
  members: ProjectMember,
  document: ProjectDocument,
};

const activeComponent = computed(() => componentMap[currentTab.value] ?? ProjectKanban);

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
  const current = ++loadVersion; loadError.value = '';
  try {
    const data = await projectService.getProjectSettings(projectId);
    if (current === loadVersion) project.value = data;
  } catch (err: any) {
    if (current === loadVersion) loadError.value = err.friendlyMessage || '项目读取失败或当前账户无访问权限';
  }
};

// 仅切换项目时清空；子面板更新统计不能卸载面板并再次触发加载。
watch(() => route.params.id, () => { project.value = null; currentTab.value = 'kanban'; void refreshProject(); }, { immediate: true });
onUnmounted(() => loadVersion++);
</script>

<style scoped>
.project-detail-wrapper { color: var(--text-primary); display: grid; gap: 24px; min-width: 0; }
.detail-header { display: flex; justify-content: space-between; gap: 24px; padding: 28px; background: var(--paper-card); border: 1px solid var(--border-line); border-radius: var(--radius-lg); }
.breadcrumb { display: inline-flex; gap: 8px; color: var(--accent-color); cursor: pointer; margin-bottom: 16px; }
.project-name { font-size: clamp(24px, 3vw, 34px); margin: 0 0 12px; overflow-wrap: anywhere; }
.project-description { max-width: 700px; color: var(--text-secondary); line-height: 1.7; white-space: pre-wrap; }
.meta-card { display: grid; gap: 16px; min-width: 170px; }
.meta-row { display: grid; gap: 6px; font-size: 14px; }
.label { color: var(--text-secondary); }
.status-badge { color: var(--success); }
.module-nav { min-width: 0; border-bottom: 1px solid var(--border-line); }
.nav-container { display: flex; gap: 8px; overflow-x: auto; padding-bottom: 10px; }
.nav-link { flex-shrink: 0; padding: 10px 16px; border: 0; border-radius: var(--radius-sm); background: transparent; color: var(--text-secondary); cursor: pointer; }
.nav-link:hover { background: var(--bg-sub); }
.nav-link.is-active { background: var(--accent-soft); color: var(--accent-color); font-weight: 600; }
.content-viewport, .module-container { min-width: 0; }
.loading-state { padding: 60px 20px; text-align: center; color: var(--text-secondary); }
.loading-pulse { width: 40px; height: 4px; margin: auto; background: var(--accent-color); }
.view-fade-enter-active, .view-fade-leave-active { transition: opacity .15s; }
.view-fade-enter-from, .view-fade-leave-to { opacity: 0; }
@media (max-width: 700px) { .detail-header { flex-direction: column; padding: 20px; } .meta-card { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
</style>
