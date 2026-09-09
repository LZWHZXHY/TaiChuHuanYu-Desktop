<template>
  <div class="project-kanban-root">
    
    <div class="kanban-wrapper" v-if="!isLoading">
      <div class="kanban-scroll-viewport">
        
        <!-- 🌟 分类泳道：支持整个列表左右拖动排期，并根据 categoryType 触发特效 -->
        <div 
          v-for="(lane, lIndex) in boardData" 
          :key="lane.id" 
          class="kanban-lane"
          :class="{
            'status-lane-done': lane.categoryType === 1,
            'status-lane-archived': lane.categoryType === 2
          }"
          draggable="true"
          @dragstart="handleLaneDragStart($event, lane, lIndex)"
          @dragover.prevent
          @drop="handleLaneDropOnLane($event, lane, lIndex)"
        >
          <header class="lane-header" :style="{ borderTopColor: lane.colorCode }">
            <div class="lane-title-area">
              <!-- 🌟 拖拽手柄指示 -->
              <span class="lane-drag-handle" title="按住可左右拖拽调换列表顺序">⠿</span>

              <div class="color-picker-wrapper" :style="{ backgroundColor: lane.colorCode }">
                <input 
                  type="color" 
                  v-model="lane.colorCode" 
                  @change="updateCategoryColor(lane.id, lane.colorCode)" 
                />
              </div>
              
              <input 
                v-model="lane.name" 
                class="lane-name-input"
                @change="renameCategory(lane.id, lane.name)"
                @keyup.enter="($event.target as HTMLInputElement).blur()"
                placeholder="命名此维度..."
              />
              <span class="lane-count">{{ lane.tasks?.length ?? 0 }}</span>
            </div>

            <!-- 🌟 分栏状态类型快速切换（常规 / 完成 / 归档） -->
            <div class="lane-type-switcher">
              <select :value="lane.categoryType ?? 0" @change="updateCategoryType(lane.id, Number(($event.target as HTMLSelectElement).value))" title="切换分栏状态特效">
                <option :value="0">常规</option>
                <option :value="1">完成</option>
                <option :value="2">归档</option>
              </select>
            </div>

            <button class="lane-delete-btn" @click="removeCategory(lane.id)">×</button>
          </header>

          <!-- 卡片承载主体 -->
          <div 
            class="lane-body"
            @dragover.prevent
            @drop.stop="handleCardDropOnLaneEmpty($event, lane.id)"
          >
            <TransitionGroup name="task-list">
              <div 
                v-for="(task, index) in lane.tasks" 
                :key="task.id" 
                class="task-card"
                draggable="true"
                @dragstart.stop="handleCardDragStart($event, task, lane.id)"
                @dragover.prevent.stop
                @drop.stop="handleCardDropOnCard($event, task, lane.id, Number(index))"
                @click="openTaskModal(task, lane.id)"
              >
                <div class="task-card-inner">
                  <div class="card-top-meta">
                    <div class="tag-pills">
                      <span class="tiny-quant-pill" title="意图量化点数">⚡ {{ task.points ?? 1 }}</span>
                      <span v-if="task.estimatedHours" class="tiny-quant-pill hours" title="预估工时">⏱ {{ task.estimatedHours }}h</span>
                      <span v-for="tag in parseTags(task.tags)" :key="tag" class="tiny-tag">{{ tag }}</span>
                    </div>
                    <span v-if="task.priority === 2" class="priority-icon high">↑</span>
                    <span v-if="task.priority === 3" class="priority-icon urgent">!!</span>
                  </div>

                  <h4 class="task-title">{{ task.title }}</h4>
                  <p class="task-desc" v-if="task.description">{{ task.description }}</p>
                  
                  <div class="task-meta">
                    <span class="task-id">#{{ task.id.substring(0, 4) }}</span>
                    <div class="meta-right">
                      <span v-if="task.dueDate" class="due-date" :class="{ 'overdue': isOverdue(task.dueDate) }">
                        {{ formatShortDate(task.dueDate) }}
                      </span>
                      <span v-if="task.assigneeId" class="task-assignee">
                        @{{ getAssigneeName(task.assigneeId) }}
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            </TransitionGroup>

            <div class="lane-actions">
              <button class="add-task-trigger" @click="triggerQuickTask(lane.id)">+ 注入意图</button>
            </div>
          </div>
        </div>

        <!-- 游离未分类泳道 (固定在末尾，不可拖拽) -->
        <div 
          class="kanban-lane unclassified-lane"
          v-if="unclassifiedTasks.length > 0 || boardData.length === 0"
        >
          <header class="lane-header">
            <div class="lane-title-area">
              <h3 class="static-lane-title">游离意图 (未分类)</h3>
              <span class="lane-count">{{ unclassifiedTasks.length }}</span>
            </div>
          </header>
          <div 
            class="lane-body"
            @dragover.prevent
            @drop.stop="handleCardDropOnLaneEmpty($event, null)"
          >
            <TransitionGroup name="task-list">
              <div 
                v-for="(task, index) in unclassifiedTasks" 
                :key="task.id" 
                class="task-card"
                draggable="true"
                @dragstart.stop="handleCardDragStart($event, task, null)"
                @dragover.prevent.stop
                @drop.stop="handleCardDropOnCard($event, task, null, Number(index))"
                @click="openTaskModal(task, null)"
              >
                <div class="task-card-inner">
                  <div class="card-top-meta">
                    <div class="tag-pills">
                      <span class="tiny-quant-pill">⚡ {{ task.points ?? 1 }}</span>
                      <span v-if="task.estimatedHours" class="tiny-quant-pill hours">⏱ {{ task.estimatedHours }}h</span>
                      <span v-for="tag in parseTags(task.tags)" :key="tag" class="tiny-tag">{{ tag }}</span>
                    </div>
                    <span v-if="task.priority === 2" class="priority-icon high">↑</span>
                    <span v-if="task.priority === 3" class="priority-icon urgent">!!</span>
                  </div>

                  <h4 class="task-title">{{ task.title }}</h4>
                  <p class="task-desc" v-if="task.description">{{ task.description }}</p>
                  <div class="task-meta">
                    <span class="task-id">#{{ task.id.substring(0, 4) }}</span>
                    <div class="meta-right">
                      <span v-if="task.dueDate" class="due-date" :class="{ 'overdue': isOverdue(task.dueDate) }">
                        {{ formatShortDate(task.dueDate) }}
                      </span>
                      <span v-if="task.assigneeId" class="task-assignee">
                        @{{ getAssigneeName(task.assigneeId) }}
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            </TransitionGroup>
            <div class="lane-actions">
              <button class="add-task-trigger" @click="triggerQuickTask(null)">+ 注入游离意图</button>
            </div>
          </div>
        </div>

        <!-- 新建分栏卡片 -->
        <div class="kanban-lane lane-creator-card">
          <button class="create-lane-btn" @click="addNewCategory">
            <span class="plus">+</span> 铸造新分栏维度
          </button>
        </div>

      </div>
    </div>

    <div v-else class="kanban-loading">
      <div class="loading-bar"></div>
    </div>

    <Transition name="fade">
      <div v-if="modalConfig.isOpen" class="modal-overlay" @click.self="handleModalCancel">
        <div class="minimal-modal">
          <header class="modal-inner-header">
            <h2>{{ modalConfig.title }}</h2>
            <p v-if="modalConfig.message">{{ modalConfig.message }}</p>
          </header>

          <div class="modal-body" v-if="modalConfig.type === 'prompt'">
            <div class="input-group">
              <input 
                v-model="modalConfig.inputValue" 
                :placeholder="modalConfig.placeholder" 
                autofocus
                @keyup.enter="handleModalConfirm" 
              />
            </div>
          </div>

          <footer class="modal-footer">
            <button class="cancel-btn" @click="handleModalCancel">取消</button>
            <button class="confirm-btn" @click="handleModalConfirm">确认</button>
          </footer>
        </div>
      </div>
    </Transition>

    <TaskDetailDrawer
      :is-open="drawerState.isOpen"
      :project-id="projectId"
      :task="drawerState.activeTask"
      :original-category-id="drawerState.originalCategoryId"
      :board-categories="boardData"
      :project-members="projectMembers"
      @close="drawerState.isOpen = false"
      @refresh="loadBoard"
      @confirm-delete="handleDeleteTask"
    />

  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import projectService from '../../../api/projectService';
import request from '@/utils/request';
import TaskDetailDrawer from './ProjectTaskDetail.vue';

const props = defineProps<{ projectId: string; initialData?: any; }>();
const emit = defineEmits(['updated']);

const isLoading = ref(true);
const boardData = ref<any[]>([]);          
const unclassifiedTasks = ref<any[]>([]);  
const projectMembers = ref<any[]>([]);     

let draggingTask: any = null;
let draggingLane: any = null;

const drawerState = ref({
  isOpen: false,
  activeTask: null as any,
  originalCategoryId: null as string | null
});

const modalConfig = ref({
  isOpen: false, type: 'prompt', title: '', message: '', inputValue: '', placeholder: '', resolve: null as ((value: any) => void) | null
});

const customPrompt = (title: string, placeholder: string = ''): Promise<string | null> => {
  return new Promise((resolve) => { modalConfig.value = { isOpen: true, type: 'prompt', title, message: '', inputValue: '', placeholder, resolve }; });
};

const customConfirm = (title: string, message: string): Promise<boolean> => {
  return new Promise((resolve) => { 
    modalConfig.value = { 
      isOpen: true, 
      type: 'confirm', 
      title, 
      message, 
      inputValue: '', 
      placeholder: '', // 🌟 这里给个空字符串，或者直接删掉 placeholder 属性
      resolve 
    }; 
  });
};

const handleModalConfirm = () => {
  if (modalConfig.value.resolve) modalConfig.value.resolve(modalConfig.value.type === 'prompt' ? modalConfig.value.inputValue : true);
  modalConfig.value.isOpen = false;
};

const handleModalCancel = () => {
  if (modalConfig.value.resolve) modalConfig.value.resolve(modalConfig.value.type === 'prompt' ? null : false);
  modalConfig.value.isOpen = false;
};

const loadBoard = async () => {
  isLoading.value = true;
  try {
    const [boardRes, membersRes] = await Promise.all([
      projectService.getKanbanBoard(props.projectId),
      projectService.getProjectMembers(props.projectId)
    ]);
    
    boardData.value = boardRes.board || [];
    unclassifiedTasks.value = boardRes.unclassified || [];
    projectMembers.value = membersRes || [];
    
    emit('updated');
  } catch (err) {
    console.error("协作看板载入失败:", err);
  } finally {
    isLoading.value = false;
  }
};
onMounted(loadBoard);

const getAssigneeName = (userId: string) => {
  const target = projectMembers.value.find(m => m.id === userId);
  return target ? target.name : userId.substring(0, 5);
};

const openTaskModal = (task: any, currentCategoryId: string | null) => {
  drawerState.value.activeTask = task;
  drawerState.value.originalCategoryId = currentCategoryId;
  drawerState.value.isOpen = true;
};

const handleDeleteTask = async (taskId: string) => {
  const isConfirmed = await customConfirm("抹除意图", "确定要将这一意图卡片彻底从画布中抹除吗？此操作将无法撤销。");
  if (!isConfirmed) return;

  try {
    await projectService.deleteTask(props.projectId, taskId);
    drawerState.value.isOpen = false; 
    await loadBoard(); 
  } catch (err) {
    console.error("抹除意图失败:", err);
  }
};

const addNewCategory = async () => {
  const name = await customPrompt("铸造新维度", "请输入新分栏维度的称谓...");
  if (!name || !name.trim()) return;
  const palette = ['#1a1a1a', '#6f42c1', '#007bff', '#20c997', '#fd7e14', '#e83e8c'];
  const randomColor = palette[Math.floor(Math.random() * palette.length)];
  try {
    const newCategory = await projectService.createKanbanCategory(props.projectId, { 
      name: name.trim(), 
      colorCode: randomColor, 
      categoryType: 0 
    });
    boardData.value.push({ ...newCategory, tasks: [] });
  } catch (err) { console.error(err); }
};

const renameCategory = async (categoryId: string, newName: string) => {
  if (!newName || !newName.trim()) return;
  try { await projectService.updateKanbanCategory(props.projectId, categoryId, { name: newName.trim() }); } catch (err) { console.error(err); }
};

// 🌟 同步更新分栏状态类型
const updateCategoryType = async (categoryId: string, newType: number) => {
  try {
    await projectService.updateKanbanCategory(props.projectId, categoryId, { categoryType: newType });
    await loadBoard();
  } catch (err) {
    console.error("同步分栏状态失败:", err);
  }
};

const removeCategory = async (categoryId: string) => {
  const isConfirmed = await customConfirm("解构分栏维度", "确定要解构这一分栏吗？其中的意图卡片将会退回到『游离意图』池中。");
  if (!isConfirmed) return;
  try {
    await projectService.deleteKanbanCategory(props.projectId, categoryId);
    await loadBoard();
  } catch (err) { console.error(err); }
};

const updateCategoryColor = async (categoryId: string, newColor: string) => {
  try { await projectService.updateKanbanCategory(props.projectId, categoryId, { colorCode: newColor }); } catch (err) { console.error("颜色同步失败", err); }
};

// ==================== 拖拽相关方法 ====================

const handleLaneDragStart = (e: DragEvent, lane: any, _index: number) => {
  if (draggingTask) return;
  draggingLane = lane;
  if (e.dataTransfer) {
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('type', 'lane');
  }
};

const handleLaneDropOnLane = async (e: DragEvent, targetLane: any, targetIndex: number) => {
  if (!draggingLane || draggingLane.id === targetLane.id) return;
  const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
  const isDropRight = (e.clientX - rect.left) > (rect.width / 2);

  let prevOrder: number | null = null;
  let nextOrder: number | null = null;

  if (isDropRight) {
    prevOrder = targetLane.sortOrder || 1000.0;
    const nextLane = boardData.value[targetIndex + 1];
    nextOrder = nextLane && nextLane.id !== draggingLane.id ? nextLane.sortOrder : null;
  } else {
    const prevLane = boardData.value[targetIndex - 1];
    prevOrder = prevLane && prevLane.id !== draggingLane.id ? prevLane.sortOrder : null;
    nextOrder = targetLane.sortOrder || 1000.0;
  }

  try {
    await request.put(`/project/${props.projectId}/kanban/categories/${draggingLane.id}/move`, {
      prevSortOrder: prevOrder,
      nextSortOrder: nextOrder
    });
    await loadBoard();
  } catch (err: any) {
    console.error("调整分栏顺序失败:", err);
    alert(err.response?.data?.message || "无权调整分栏顺序");
  } finally {
    draggingLane = null;
  }
};

const handleCardDragStart = (e: DragEvent, task: any, _laneId: string | null) => {
  draggingTask = task;
  if (e.dataTransfer) {
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('type', 'card');
  }
};

const handleCardDropOnCard = async (e: DragEvent, targetTask: any, targetLaneId: string | null, targetIndex: number) => {
  if (!draggingTask || draggingTask.id === targetTask.id) return;
  const targetLaneTasks = targetLaneId === null ? unclassifiedTasks.value : (boardData.value.find((l: any) => l.id === targetLaneId)?.tasks || []);
  const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
  const isDropAfter = (e.clientY - rect.top) > (rect.height / 2);

  let prevOrder: number | null = null;
  let nextOrder: number | null = null;

  if (isDropAfter) {
    prevOrder = targetTask.sortOrder;
    const nextTask = targetLaneTasks[targetIndex + 1];
    nextOrder = nextTask && nextTask.id !== draggingTask.id ? nextTask.sortOrder : null;
  } else {
    const prevTask = targetLaneTasks[targetIndex - 1];
    prevOrder = prevTask && prevTask.id !== draggingTask.id ? prevTask.sortOrder : null;
    nextOrder = targetTask.sortOrder;
  }

  await executeMoveTask(draggingTask.id, targetLaneId, prevOrder, nextOrder);
};

const handleCardDropOnLaneEmpty = async (_e: DragEvent, targetLaneId: string | null) => {
  if (!draggingTask) return;
  const targetLaneTasks = targetLaneId === null ? unclassifiedTasks.value : (boardData.value.find((l: any) => l.id === targetLaneId)?.tasks || []);
  const validTasks = targetLaneTasks.filter((t: any) => t.id !== draggingTask.id);

  let prevOrder: number | null = null;
  if (validTasks.length > 0) {
    prevOrder = validTasks[validTasks.length - 1].sortOrder;
  }

  await executeMoveTask(draggingTask.id, targetLaneId, prevOrder, null);
};

const executeMoveTask = async (taskId: string, targetLaneId: string | null, prevOrder: number | null, nextOrder: number | null) => {
  try {
    await projectService.moveKanbanTask(props.projectId, taskId, {
      targetCategoryId: targetLaneId,
      prevSortOrder: prevOrder,
      nextSortOrder: nextOrder
    });
    await loadBoard();
  } catch (err: any) {
    console.error("卡片流转失败:", err);
    alert(err.response?.data?.message || "移动卡片失败，请确认您的权限");
  } finally {
    draggingTask = null;
  }
};

const triggerQuickTask = async (laneId: string | null) => {
  const title = await customPrompt("注入新意图", "请输入要注入的任务意图...");
  if (!title || !title.trim()) return;
  try {
    await projectService.createTask(props.projectId, { title: title.trim(), status: 0, categoryId: laneId });
    await loadBoard();
  } catch (err) { console.error(err); }
};

const parseTags = (tagsStr: string) => tagsStr ? tagsStr.split(',').filter(Boolean) : [];
const isOverdue = (dateStr: string) => new Date(dateStr) < new Date();
const formatShortDate = (dateStr: string) => {
  const d = new Date(dateStr);
  return `${String(d.getMonth() + 1).padStart(2, '0')}.${String(d.getDate()).padStart(2, '0')}`;
};
</script>

<style scoped>
.project-kanban-root { width: 100%; }
.kanban-wrapper { width: 100%; animation: fadeIn 0.8s cubic-bezier(0.16, 1, 0.3, 1); }
.kanban-scroll-viewport { display: flex; gap: 32px; overflow-x: auto; padding: 10px 0 30px; align-items: flex-start; min-height: calc(100vh - 350px); }
.kanban-scroll-viewport::-webkit-scrollbar { height: 4px; }
.kanban-scroll-viewport::-webkit-scrollbar-thumb { background: #eaeaea; border-radius: 2px; }

.kanban-lane { 
  flex: 0 0 320px; 
  background: #ffffff; 
  border: 1px solid #f6f6f6; 
  padding: 24px 20px; 
  box-shadow: 0 20px 50px rgba(0, 0, 0, 0.01); 
  min-height: 450px; 
  display: flex; 
  flex-direction: column; 
  transition: transform 0.2s, box-shadow 0.2s, background-color 0.3s;
}

/* ==================== 🌟 特效 1：已完成 (CategoryType === 1) 样式 ==================== */
.kanban-lane.status-lane-done {
  background: #fcfcfc;
  border-color: #e5e5e5;
}
.kanban-lane.status-lane-done .task-title {
  text-decoration: line-through;
  color: #888;
}
.kanban-lane.status-lane-done .task-card {
  background: #fafafa;
  border-style: dashed;
  opacity: 0.85;
}

/* ==================== 🌟 特效 2：已归档/封档 (CategoryType === 2) 样式 ==================== */
.kanban-lane.status-lane-archived {
  background: #f4f4f4;
  border-color: #dedede;
  filter: grayscale(85%);
  opacity: 0.65;
}
.kanban-lane.status-lane-archived:hover {
  opacity: 0.95;
  filter: grayscale(20%);
}
.kanban-lane.status-lane-archived .lane-name-input {
  text-decoration: line-through;
  color: #666;
}
.kanban-lane.status-lane-archived .task-card {
  background: #efefef;
  box-shadow: none;
  border-color: #ddd;
}
.kanban-lane.status-lane-archived .task-title,
.kanban-lane.status-lane-archived .task-desc {
  color: #999;
}

/* 栏类型切换选择器微调 */
.lane-type-switcher select {
  font-size: 0.7rem;
  padding: 2px 4px;
  background: #fafafa;
  border: 1px solid #eee;
  color: #666;
  cursor: pointer;
  outline: none;
  border-radius: 2px;
}
.lane-type-switcher select:hover {
  border-color: #ccc;
}

.lane-drag-handle {
  cursor: grab;
  color: #ccc;
  font-size: 1rem;
  user-select: none;
  transition: color 0.2s;
  padding: 0 4px;
}
.lane-drag-handle:hover { color: #1a1a1a; }
.lane-drag-handle:active { cursor: grabbing; }

.lane-header { border-top: 3px solid #eee; padding-top: 16px; display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
.lane-title-area { display: flex; align-items: center; gap: 8px; flex-grow: 1; }
.lane-name-input { border: none; background: transparent; font-size: 0.9rem; font-weight: 500; letter-spacing: 0.5px; color: #1a1a1a; outline: none; width: 55%; transition: border-color 0.3s; }
.lane-name-input:focus { border-bottom: 1px solid #1a1a1a; }
.lane-count { font-size: 0.7rem; font-family: monospace; color: #bbb; background: #fafafa; padding: 2px 6px; }
.lane-delete-btn { background: none; border: none; color: #ccc; cursor: pointer; font-size: 1.2rem; line-height: 1; transition: color 0.3s; }
.lane-delete-btn:hover { color: #ff4757; }
.lane-body { display: flex; flex-direction: column; gap: 16px; flex-grow: 1; min-height: 220px; }

.task-card { background: #ffffff; border: 1px solid #eee; cursor: grab; position: relative; transition: transform 0.3s cubic-bezier(0.16, 1, 0.3, 1), border-color 0.3s, box-shadow 0.3s; }
.task-card:active { cursor: grabbing; }
.task-card:hover { border-color: #1a1a1a; transform: translateY(-2px); box-shadow: 0 15px 35px rgba(0,0,0,0.03); }
.task-card-inner { padding: 20px; pointer-events: none; }

.card-top-meta { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 8px; }
.tag-pills { display: flex; flex-wrap: wrap; gap: 4px; align-items: center; }
.tiny-quant-pill {
  background: #1a1a1a; color: #fff; font-size: 0.6rem; padding: 1px 5px; border-radius: 2px;
  font-family: monospace;
}
.tiny-quant-pill.hours { background: #f0f0f0; color: #555; }
.tiny-tag { background: #f9f9f9; color: #888; font-size: 0.6rem; padding: 1px 5px; border-radius: 2px; }

.priority-icon { font-weight: bold; font-size: 0.8rem; }
.priority-icon.high { color: #fd7e14; }
.priority-icon.urgent { color: #dc3545; }

.task-title { font-size: 0.9rem; font-weight: 400; color: #1a1a1a; margin: 0 0 8px 0; line-height: 1.4; }
.task-desc { font-size: 0.78rem; color: #777; line-height: 1.6; margin: 0 0 16px 0; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
.task-meta { display: flex; justify-content: space-between; align-items: center; font-size: 0.68rem; color: #bbb; }
.meta-right { display: flex; align-items: center; gap: 8px; }
.task-id { font-family: monospace; color: #ddd; }
.task-assignee { color: #666; font-weight: 500; }

.lane-creator-card { background: transparent; border: 1px dashed #e5e5e5; justify-content: center; align-items: center; min-height: 180px; box-shadow: none; }
.create-lane-btn { background: none; border: none; color: #bbb; font-size: 0.8rem; letter-spacing: 1px; cursor: pointer; transition: color 0.3s; }
.create-lane-btn:hover { color: #1a1a1a; }
.add-task-trigger { width: 100%; background: none; border: 1px dashed #eee; padding: 12px; color: #ccc; font-size: 0.75rem; cursor: pointer; transition: all 0.3s; }
.add-task-trigger:hover { color: #1a1a1a; border-color: #1a1a1a; background: #fafafa; }
.unclassified-lane { background: #fafafa; border-style: dashed; }
.static-lane-title { font-size: 0.85rem; font-weight: 500; letter-spacing: 0.5px; color: #999; margin: 0; text-transform: uppercase; }

.color-picker-wrapper { width: 14px; height: 14px; border-radius: 50%; overflow: hidden; position: relative; cursor: pointer; flex-shrink: 0; border: 1px solid rgba(0,0,0,0.1); }
.color-picker-wrapper input[type="color"] { position: absolute; top: -10px; left: -10px; width: 40px; height: 40px; opacity: 0; cursor: pointer; }

.due-date { font-size: 0.65rem; padding: 2px 4px; background: #eee; border-radius: 2px; color: #666; }
.due-date.overdue { background: #fee; color: #c00; }

.modal-overlay { position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(255, 255, 255, 0.85); backdrop-filter: blur(8px); display: flex; align-items: center; justify-content: center; z-index: 1000; }
.minimal-modal { background: #fff; width: 100%; max-width: 440px; padding: 48px; border: 1px solid #eee; box-shadow: 0 40px 100px rgba(0,0,0,0.04); }
.modal-inner-header h2 { font-size: 1.2rem; font-weight: 500; margin: 0 0 12px 0; color: #1a1a1a; }
.modal-inner-header p { font-size: 0.85rem; color: #777; line-height: 1.6; margin: 0; }
.modal-footer { margin-top: 40px; display: flex; justify-content: flex-end; gap: 16px; }
.cancel-btn { background: none; border: none; color: #999; font-size: 0.85rem; cursor: pointer; padding: 10px 20px; transition: color 0.3s; }
.cancel-btn:hover { color: #1a1a1a; }
.confirm-btn { background: #1a1a1a; color: #fff; border: none; font-size: 0.85rem; cursor: pointer; padding: 10px 28px; border-radius: 2px; transition: background 0.3s; }
.confirm-btn:hover { background: #333; }

.task-list-enter-from, .task-list-leave-to { opacity: 0; transform: scale(0.95); }
.kanban-loading { height: 200px; display: flex; align-items: center; justify-content: center; }
.loading-bar { width: 60px; height: 1px; background: #1a1a1a; animation: pulse 1.5s infinite; }
.fade-enter-active, .fade-leave-active { transition: opacity 0.3s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
@keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
@keyframes pulse { 0% { transform: scaleX(0.5); opacity: 0.2; } 50% { transform: scaleX(1.5); opacity: 1; } 100% { transform: scaleX(0.5); opacity: 0.2; } }
</style>