<template>
  <div class="horizontal-timeline-container" v-if="!isLoading">
    
    <header class="gantt-timeline-header">
      <div class="header-block asset-title">意图长卷</div>
      <div class="header-block month-scale" v-for="m in timelineMonths" :key="m">
        {{ m }}
      </div>
    </header>

    <main class="gantt-timeline-body">
      <div v-if="ganttTasks.length === 0" class="gantt-empty-state">
        <div class="empty-box">
          <p>此空间内尚无具有明确横向时空跨度的意图卡片。</p>
          <p class="sub">请前往看板，点击卡片展开详情，为其确立「开启时间」与「截止节点」。</p>
        </div>
      </div>

      <div 
        v-else
        v-for="task in ganttTasks" 
        :key="task.id" 
        class="gantt-lane-row"
        @click="openTaskDrawer(task)"
      >
        <div class="lane-task-info">
          <h4 class="info-title">{{ task.title }}</h4>
          <span class="info-assignee" v-if="task.assigneeId">@{{ getAssigneeName(task.assigneeId) }}</span>
        </div>

        <div class="lane-track-view">
          <div 
            class="gantt-capsule-bar"
            :style="calculateBarPosition(task)"
          >
           <div 
            class="capsule-inner" 
            :style="{ backgroundColor: priorityColors[task.priority] || task.categoryColor || '#1a1a1a' }"
          >
              <span class="capsule-date-label">
                {{ formatDateNode(task.startDate) }} — {{ formatDateNode(task.dueDate) }}
              </span>
            </div>
          </div>
        </div>
      </div>
    </main>

    <TaskDetailDrawer
      :is-open="drawerState.isOpen"
      :project-id="projectId"
      :task="drawerState.activeTask"
      :original-category-id="drawerState.originalCategoryId"
      :board-categories="boardCategories"
      :project-members="projectMembers"
      @close="drawerState.isOpen = false"
      @refresh="loadTimelineData"
    />

  </div>

  <div v-else class="gantt-loading-wrapper">
    <div class="loading-pulse-line"></div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import projectService from '../../../api/projectService';
import TaskDetailDrawer from './ProjectTaskDetail.vue'; // 🌟 引入解耦抽屉
// 预设优先级色彩（采用低饱和度、高高级感的协作色体系）
const priorityColors: Record<number, string> = {
  0: '#eaeaea', // 低缓：清静淡灰
  1: '#20c997', // 常规：太初竹翠
  2: '#fd7e14', // 高优：灵脉琥珀
  3: '#ff4757'  // 极度紧急：朱砂警告红
};
const props = defineProps<{ projectId: string; }>();

const isLoading = ref(true);
const rawTasks = ref<any[]>([]);
const projectMembers = ref<any[]>([]);
const boardCategories = ref<any[]>([]);

const drawerState = ref({ isOpen: false, activeTask: null as any, originalCategoryId: null as string | null });

// 💡 核心算法 1：筛选出必须包含完整起止两个边界的任务用于横向甘特图绘制
const ganttTasks = computed(() => {
  return rawTasks.value
    .filter(t => t.startDate && t.dueDate)
    .sort((a, b) => new Date(a.startDate).getTime() - new Date(b.startDate).getTime());
});

// 💡 核心算法 2：自动计算排期长卷顶层应该跨越的最小和最大月份范围，生成动态刻度
const timelineMonths = computed(() => {
  if (ganttTasks.value.length === 0) {
    const today = new Date();
    return [`${today.getFullYear()}.${String(today.getMonth() + 1).padStart(2, '0')}`];
  }

  const startTimestamps = ganttTasks.value.map(t => new Date(t.startDate).getTime());
  const endTimestamps = ganttTasks.value.map(t => new Date(t.dueDate).getTime());

  const minDate = new Date(Math.min(...startTimestamps));
  const maxDate = new Date(Math.max(...endTimestamps));

  const months: string[] = [];
  let runner = new Date(minDate.getFullYear(), minDate.getMonth(), 1);

  while (runner <= maxDate) {
    months.push(`${runner.getFullYear()}.${String(runner.getMonth() + 1).padStart(2, '0')}`);
    runner.setMonth(runner.getMonth() + 1);
  }
  return months;
});

// 💡 核心算法 3：计算胶囊条基于横向 Flex / 百分比格线的精准左偏移量(Margin-Left)和宽度比例(Width)
const calculateBarPosition = (task: any) => {
  if (timelineMonths.value.length === 0) return {};

  const timelineStart = new Date(timelineMonths.value[0] + '.01').getTime();
  
  // 终点算在最后一个月份下一个月的第一天
  const [endYear, endMonth] = timelineMonths.value[timelineMonths.value.length - 1].split('.').map(Number);
  const timelineEnd = new Date(endYear, endMonth, 1).getTime();

  const totalTimeRange = timelineEnd - timelineStart;
  const taskStart = new Date(task.startDate).getTime();
  const taskEnd = new Date(task.dueDate).getTime();

  const leftOffsetPercent = ((taskStart - timelineStart) / totalTimeRange) * 100;
  const widthPercent = ((taskEnd - taskStart) / totalTimeRange) * 100;

  return {
    marginLeft: `${Math.max(0, leftOffsetPercent)}%`,
    width: `${Math.max(3, widthPercent)}%` // 给予 3% 最小保底可见宽度
  };
};

const loadTimelineData = async () => {
  isLoading.value = true;
  try {
    const [tasksRes, membersRes, boardRes] = await Promise.all([
      projectService.getProjectTasks(props.projectId),
      projectService.getProjectMembers(props.projectId),
      projectService.getKanbanBoard(props.projectId)
    ]);
    rawTasks.value = tasksRes || [];
    projectMembers.value = membersRes || [];
    boardCategories.value = boardRes.board || [];
  } catch (err) {
    console.error("加载横向时间轴数据失败:", err);
  } finally {
    isLoading.value = false;
  }
};

onMounted(loadTimelineData);

const getAssigneeName = (userId: string) => {
  return projectMembers.value.find(m => m.id === userId)?.name || userId.substring(0, 5);
};

const openTaskDrawer = (task: any) => {
  drawerState.value.activeTask = task;
  drawerState.value.originalCategoryId = task.categoryId;
  drawerState.value.isOpen = true;
};

const formatDateNode = (dateStr: string) => {
  const d = new Date(dateStr);
  return `${d.getMonth() + 1}.${d.getDate()}`;
};
</script>

<style scoped>
.horizontal-timeline-container {
  width: 100%;
  background: #ffffff;
  border: 1px solid #f6f6f6;
  box-shadow: 0 30px 80px rgba(0,0,0,0.01);
  animation: fadeIn 0.8s cubic-bezier(0.16, 1, 0.3, 1);
}

/* 顶部横向月份刻度标尺 */
.gantt-timeline-header {
  display: flex;
  background: #fafafa;
  border-bottom: 1px solid #eee;
}

.header-block {
  flex: 1;
  padding: 18px;
  font-size: 0.75rem;
  color: #888;
  text-align: center;
  border-right: 1px solid #f2f2f2;
  font-family: monospace;
  letter-spacing: 0.5px;
}

.header-block.asset-title {
  flex: 0 0 240px;
  text-align: left;
  font-weight: 500;
  color: #1a1a1a;
  border-right: 1px solid #eee;
}

/* 轨道 body */
.gantt-timeline-body {
  display: flex;
  flex-direction: column;
}

.gantt-lane-row {
  display: flex;
  border-bottom: 1px solid #f9f9f9;
  cursor: pointer;
  transition: background 0.25s ease;
}
.gantt-lane-row:hover {
  background: #fafafa;
}

/* 左侧栏 */
.lane-task-info {
  flex: 0 0 240px;
  padding: 24px 20px;
  border-right: 1px solid #eee;
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: 4px;
}
.info-title {
  font-size: 0.88rem;
  font-weight: 400;
  color: #1a1a1a;
  margin: 0;
  line-height: 1.4;
}
.info-assignee {
  font-size: 0.7rem;
  color: #bbb;
}

/* 右侧排期轴胶囊轨道 */
.lane-track-view {
  flex: 1;
  padding: 24px 0;
  display: flex;
  align-items: center;
  /* 优雅的格线背景：动态根据月份数组的长度自动画线分割列 */
  background-image: linear-gradient(to right, #f6f6f6 1px, transparent 1px);
  background-size: calc(100% / v-bind('timelineMonths.length')) 100%;
}

/* 🌟 核心：横向胶囊进度块 */
.gantt-capsule-bar {
  height: 28px;
  transition: all 0.4s cubic-bezier(0.16, 1, 0.3, 1);
}

.capsule-inner {
  height: 100%;
  width: 100%;
  border-radius: 2px;
  display: flex;
  align-items: center;
  padding: 0 12px;
  box-shadow: 0 4px 15px rgba(0,0,0,0.03);
}

.capsule-date-label {
  font-size: 0.8rem;
  font-family: monospace;
  color: #000000;
  /* mix-blend-mode: difference; /* 🌟 核心美学：不论分类颜色多暗或多亮，文字都能智能反色清晰可见 */
  white-space: nowrap;
}

/* 空白占位 */
.gantt-empty-state {
  padding: 100px 40px;
  text-align: center;
}
.empty-box p { font-size: 0.9rem; color: #999; margin: 4px 0; }
.empty-box .sub { font-size: 0.75rem; color: #ccc; }

/* 加载动画 */
.gantt-loading-wrapper {
  height: 4px;
  width: 100%;
  background: #f5f5f5;
  position: relative;
}
.loading-pulse-line {
  height: 100%;
  background: #1a1a1a;
  width: 25%;
  position: absolute;
  animation: barRunner 1.6s infinite ease-in-out;
}

@keyframes barRunner {
  0% { left: -25%; }
  100% { left: 100%; }
}
@keyframes fadeIn {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: translateY(0); }
}
</style>