<template>
  <Transition name="fade">
    <div v-if="isOpen" class="modal-overlay drawer-overlay" @click.self="closeDrawer">
      <div class="task-detail-drawer" role="dialog" aria-modal="true" aria-label="任务详情">
        <header class="drawer-header">
          <div class="header-left-meta">
            <span class="task-id-large">#{{ localTask.id?.substring(0, 8) }}</span>
            <span class="points-badge" title="灵脉意图贡献点">⚡ {{ localTask.points ?? 1 }} 点</span>
          </div>
          <button class="close-btn" :disabled="isSaving" aria-label="关闭任务详情" @click="closeDrawer">×</button>
        </header>

        <div class="drawer-content" :inert="isSaving">
          <textarea rows="2"
            class="huge-title-input" aria-label="任务标题" 
            v-model="localTask.title" 
            placeholder="任务标题..." 
          />

          <!-- 基础属性网格 -->
          <div class="properties-grid">
            <div class="prop-item">
              <label>任务分组</label>
              <select v-model="localTask.categoryId">
                <option :value="null">未分类任务</option>
                <option v-for="lane in boardCategories" :key="lane.id" :value="lane.id">
                  {{ lane.name }}
                </option>
              </select>
            </div>

            <div class="prop-item">
              <label>指派给</label>
              <select v-model="localTask.assigneeId" :disabled="!canManageTasks">
                <option :value="null">未指派</option>
                <option v-for="m in projectMembers" :key="m.id" :value="m.id">
                  {{ m.name || m.id }}
                </option>
              </select>
            </div>

            <div class="prop-item">
              <label>紧急程度</label>
              <select v-model="localTask.priority">
                <option :value="0">低缓</option>
                <option :value="1">常规</option>
                <option :value="2">高优</option>
                <option :value="3">极度紧急</option>
              </select>
            </div>

            <div class="prop-item">
              <label>开启时间</label>
              <input type="date" v-model="localTask.startDate" />
            </div>

            <div class="prop-item">
              <label>截止节点</label>
              <input type="date" v-model="localTask.dueDate" />
            </div>
          </div>

          <!-- 量化评估与贡献核算专区 -->
          <div class="quant-section">
            <div class="section-label-bar">
              <label class="section-label">量化评估与贡献核算</label>
              <span v-if="!canManageTasks" class="lock-notice">🔒 核心量化权重已由主理人锁定</span>
            </div>
            
            <div class="quant-grid">
              <div class="quant-item">
                <span class="quant-title">贡献点数 (Points)</span>
                <input 
                  type="number" 
                  min="0" 
                  v-model.number="localTask.points" 
                  placeholder="如: 1, 3, 5..." 
                  class="quant-input"
                  :disabled="!canManageTasks"
                  :class="{ 'is-locked': !canManageTasks }"
                />
                <span class="quant-hint">{{ canManageTasks ? '衡量任务难度权重与完成后的实际贡献奖励' : '仅主理人与管理员可核准分配' }}</span>
              </div>
            </div>
          </div>

          <div class="tags-section">
            <label>业务标签 (回车添加)</label>
            <div class="tags-container">
              <span v-for="(tag, index) in tagArray" :key="index" class="editable-tag">
                {{ tag }} <span class="tag-remove" @click="removeTag(index)">×</span>
              </span>
              <input 
                class="tag-input" 
                v-model="newTagInput" 
                @keyup.enter="addTag" 
                placeholder="添加标签..." 
              />
            </div>
          </div>

          <div class="desc-section">
            <label>任务描述</label>
            <textarea 
              aria-label="任务描述" v-model="localTask.description" 
              placeholder="填写任务要求与验收标准..."
              rows="6"
            ></textarea>
          </div>
        </div>

        <footer class="drawer-footer">
          <button 
            v-if="canManageTasks" 
            class="delete-task-btn" 
            @click="handleDelete"
          >
            删除任务
          </button>
          
          <span class="save-status" v-if="isSaving">正在保存...</span>
          <button class="save-btn" :disabled="isSaving" @click="handleSave">保存修改</button>
        </footer>
      </div>
    </div>
  </Transition>

  <!-- 🌟 抽屉内部专用的高层级确认弹窗 -->
  <Transition name="fade">
    <div v-if="showInternalConfirm" class="drawer-modal-overlay">
      <div class="minimal-modal">
        <header class="modal-inner-header">
          <h2>{{ internalConfirmConfig.title }}</h2>
          <p>{{ internalConfirmConfig.message }}</p>
        </header>
        <footer class="modal-footer">
          <button class="cancel-btn" @click="handleInternalCancel">取消</button>
          <button class="confirm-btn" @click="handleInternalConfirm">确认</button>
        </footer>
      </div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { ref, watch, computed } from 'vue';
import projectService from '../../../api/projectService';
import request from '@/utils/request';

const props = defineProps<{
  isOpen: boolean;
  projectId: string;
  task: any;
  originalCategoryId: string | null;
  boardCategories: any[];
  projectMembers: any[];
}>();

const emit = defineEmits(['close', 'refresh', 'confirmDelete']);

const isSaving = ref(false);
const newTagInput = ref('');
const localTask = ref<any>({});
const tagArray = ref<string[]>([]);

// 抽屉内部专用的确认框状态
const showInternalConfirm = ref(false);
const internalConfirmConfig = ref({
  title: '',
  message: '',
  resolve: null as ((value: boolean) => void) | null
});

const openInternalConfirm = (title: string, message: string): Promise<boolean> => {
  return new Promise((resolve) => {
    internalConfirmConfig.value = { title, message, resolve };
    showInternalConfirm.value = true;
  });
};

const handleInternalConfirm = () => {
  if (internalConfirmConfig.value.resolve) {
    internalConfirmConfig.value.resolve(true);
  }
  showInternalConfirm.value = false;
};

const handleInternalCancel = () => {
  if (internalConfirmConfig.value.resolve) internalConfirmConfig.value.resolve(false);
  showInternalConfirm.value = false;
};

// 权限控制状态
const myPermissions = ref<string[]>([]);
const isOwner = ref(false);

const canManageTasks = computed(() => {
  if (isOwner.value) return true;
  return myPermissions.value.includes('*') || myPermissions.value.includes('task:manage');
});

const fetchMyPermissions = async () => {
  isOwner.value = false;
  myPermissions.value = [];
  if (!props.projectId) return;
  const projectId = props.projectId;
  try {
    const res: any = await request.get(`/project/${projectId}/roles/my-permissions`);
    if (props.projectId !== projectId) return;
    const data = res.data || res;
    isOwner.value = Boolean(data.isOwner);
    myPermissions.value = data.permissions || [];
  } catch (err) {
    console.error("加载个人权限清单失败:", err);
  }
};

watch(() => props.isOpen, (newVal) => {
  if (newVal && props.task) {
    localTask.value = JSON.parse(JSON.stringify(props.task));
    
    if (localTask.value.points === undefined || localTask.value.points === null) {
      localTask.value.points = 1;
    }

    tagArray.value = localTask.value.tags ? localTask.value.tags.split(',').filter(Boolean) : [];
    
    if (localTask.value.startDate) {
      localTask.value.startDate = localTask.value.startDate.split('T')[0];
    }
    if (localTask.value.dueDate) {
      localTask.value.dueDate = localTask.value.dueDate.split('T')[0];
    }

    fetchMyPermissions();
  }
}, { immediate: true });

const closeDrawer = () => { if (!isSaving.value) emit('close'); };

const addTag = () => {
  const val = newTagInput.value.trim();
  if (val && !tagArray.value.includes(val)) {
    tagArray.value.push(val);
  }
  newTagInput.value = '';
};

const removeTag = (index: number) => tagArray.value.splice(index, 1);

const handleSave = async () => {
  if (isSaving.value) return;
  if (!localTask.value.title?.trim()) return alert('请填写任务标题');
  if (localTask.value.startDate && localTask.value.dueDate && localTask.value.startDate > localTask.value.dueDate) return alert('截止时间不能早于开始时间');
  isSaving.value = true;
  localTask.value.tags = tagArray.value.join(',');
  
  const submitPayload = {
    ...localTask.value,
    points: Number(localTask.value.points) || 0,
    startDate: localTask.value.startDate || null,
    dueDate: localTask.value.dueDate || null
  };

  try {
    await projectService.updateTaskDetails(props.projectId, submitPayload.id, submitPayload);
    
    if (submitPayload.categoryId !== props.originalCategoryId) {
      await projectService.moveKanbanTask(props.projectId, submitPayload.id, {
        targetCategoryId: submitPayload.categoryId,
        prevSortOrder: null,
        nextSortOrder: null
      });
    }
    emit('refresh');
    emit('close');
  } catch (err: any) {
    console.error("意图细节同步失败", err);
    alert(err.response?.data?.message || err.response?.data || "更新失败，请确认权限");
  } finally {
    isSaving.value = false;
  }
};

const handleDelete = async () => {
    const isConfirmed = await openInternalConfirm("删除任务", "确定删除这个任务吗？此操作无法撤销。");
  if (!isConfirmed) return;
  emit('confirmDelete', localTask.value.id);
};
</script>

<style scoped>
.modal-overlay {
  position: fixed; top: 0; left: 0; right: 0; bottom: 0;
  background: var(--overlay); backdrop-filter: blur(8px);
  display: flex; align-items: center; justify-content: center; z-index: 1000;
}
.drawer-overlay { background: var(--overlay); align-items: stretch; justify-content: flex-end; z-index: 1000; }

.drawer-modal-overlay {
  position: fixed; 
  top: 0; 
  left: 0; 
  right: 0; 
  bottom: 0;
  background: var(--overlay); 
  backdrop-filter: blur(6px);
  display: flex; 
  align-items: center; 
  justify-content: center; 
  z-index: 99999;
}

.minimal-modal { 
  background: var(--paper-card); 
  width: 100%; 
  max-width: 440px; 
  padding: clamp(20px, 4vw, 32px); 
  border: 1px solid var(--border-line); 
  box-shadow: 0 40px 100px rgba(0,0,0,0.1); 

  max-height: 90dvh; overflow-y: auto; border-radius: var(--radius-md);
}
.modal-inner-header h2 { font-size: 1.2rem; font-weight: 500; margin: 0 0 12px 0; color: var(--text-primary); }
.modal-inner-header p { font-size: 0.85rem; color: var(--text-secondary); line-height: 1.6; margin: 0; }
.modal-footer { margin-top: 40px; display: flex; justify-content: flex-end; gap: 16px; }
.cancel-btn { background: none; border: none; color: var(--text-secondary); font-size: 0.85rem; cursor: pointer; padding: 10px 20px; transition: color 0.3s; }
.cancel-btn:hover { color: var(--text-primary); }
.confirm-btn { background: var(--accent-color); color: var(--on-accent); border: none; font-size: 0.85rem; cursor: pointer; padding: 10px 28px; border-radius: 2px; transition: background 0.3s; }
.confirm-btn:hover { background: var(--text-primary); }

.task-detail-drawer {
  background: var(--paper-card); width: 100%; max-width: 740px; height: 100%;
  box-shadow: -20px 0 50px rgba(0,0,0,0.05); display: flex; flex-direction: column; overflow: hidden;
  animation: slideInRight 0.4s cubic-bezier(0.16, 1, 0.3, 1);
}

.drawer-header {
  padding: 20px clamp(16px, 3vw, 32px); border-bottom: 1px solid var(--border-line);
  display: flex; justify-content: space-between; align-items: center;
}
.header-left-meta { display: flex; align-items: center; gap: 14px; }
.task-id-large { font-family: monospace; color: var(--text-secondary); font-size: 1.1rem; }
.points-badge {
  background: var(--accent-soft); color: var(--accent-color); font-size: 0.8rem; padding: 5px 10px; border-radius: 20px;
  letter-spacing: 0.5px;
}
.close-btn { background: none; border: none; font-size: 2rem; line-height: 1; color: var(--text-secondary); cursor: pointer; transition: color 0.3s;}
.close-btn:hover { color: var(--text-primary); }

.drawer-content { flex: 1; padding: 24px clamp(16px, 3vw, 32px); overflow-y: auto; }
.huge-title-input {
  width: 100%; font-size: clamp(22px, 3vw, 28px); line-height: 1.4; resize: vertical; font-weight: 500; border: none; border-bottom: 1px solid transparent;
  color: var(--text-primary); padding-bottom: 10px; margin-bottom: 24px; outline: none; transition: border-color 0.3s;
}
.huge-title-input:focus { border-bottom-color: var(--border-line); }

.properties-grid { 
  display: grid; 
  grid-template-columns: repeat(auto-fit, minmax(min(220px, 100%), 1fr)); 
  gap: 20px; 
  margin-bottom: 24px; 
}
.prop-item label { display: block; font-size: 0.85rem; color: var(--text-secondary); margin-bottom: 8px; }
.prop-item select, .prop-item input {
  width: 100%; padding: 10px 12px; border: 1px solid var(--border-line); border-radius: 8px;
  background: var(--bg-main); font-size: 0.95rem; color: var(--text-primary); cursor: pointer;
}
.prop-item select:disabled {
  background: var(--bg-main);
  color: var(--text-secondary);
  cursor: not-allowed;
}

.quant-section {
  background: var(--bg-main);
  border: 1px solid var(--border-line);
  padding: 24px;
  border-radius: var(--radius-md);
  margin-bottom: 24px;
}
.section-label-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap; gap: 8px;
  margin-bottom: 16px;
}
.section-label {
  display: block;
  font-size: 0.85rem;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 1.5px;
  font-weight: 600;
  margin: 0;
}
.lock-notice {
  font-size: 0.7rem;
  color: var(--text-secondary);
  letter-spacing: 0.5px;
}
/* 移除了原本的 repeat(3, 1fr)，现在只有一项，单列占满 */
.quant-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 20px;
}
.quant-item {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.quant-title {
  font-size: 0.75rem;
  color: var(--text-secondary);
  font-weight: 500;
}
.quant-input {
  border: 1px solid var(--border-line);
  border-radius: 8px;
  background: var(--paper-card);
  padding: 8px 10px;
  font-size: 0.95rem;
  outline: none;
  transition: all 0.2s;
}
.quant-input:focus {
  border-color: var(--border-line);
}
.quant-input.is-locked {
  background: var(--bg-sub);
  border-color: var(--border-line);
  color: var(--text-secondary);
  cursor: not-allowed;
}
.quant-hint {
  font-size: 0.8rem;
  color: var(--text-secondary);
  line-height: 1.3;
}

.tags-section { margin-bottom: 24px; }
.tags-section label { display: block; font-size: 0.85rem; color: var(--text-secondary); margin-bottom: 12px; }
.tags-container { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
.editable-tag { background: var(--bg-sub); padding: 6px 12px; font-size: 0.8rem; color: var(--text-primary); border-radius: 4px; display: flex; align-items: center; gap: 6px; }
.tag-remove { cursor: pointer; color: var(--text-secondary); transition: color 0.2s; }
.tag-remove:hover { color: var(--danger); }
.tag-input { border: none; background: transparent; font-size: 0.85rem; outline: none; border-bottom: 1px dashed var(--border-line); padding: 4px; width: 120px; }

.desc-section label { display: block; font-size: 0.85rem; color: var(--text-secondary); margin-bottom: 12px; }
.desc-section textarea {
  width: 100%; border: 1px solid var(--border-line); border-radius: 8px; background: var(--bg-main); padding: 16px;
  font-size: 0.95rem; color: var(--text-primary); outline: none; resize: vertical; line-height: 1.6; transition: border-color 0.3s;
}
.desc-section textarea:focus { border-color: var(--border-line); background: var(--paper-card); }

.drawer-footer {
  padding: 16px clamp(16px, 3vw, 32px); border-top: 1px solid var(--border-line); display: flex; flex-wrap: wrap; justify-content: flex-end; align-items: center; gap: 12px;
}
.save-status { font-size: 0.8rem; color: var(--text-secondary); }
.save-btn { background: var(--accent-color); color: var(--on-accent); border: none; border-radius: 8px; padding: 12px 24px; font-size: 0.9rem; cursor: pointer; transition: background 0.3s; }
.save-btn:disabled { opacity: .65; cursor: wait; }
.save-btn:hover { background: var(--text-primary); }

.delete-task-btn {
  background: none; border: none; color: var(--text-secondary); font-size: 0.85rem; cursor: pointer; padding: 12px 0; margin-right: auto;
  transition: color 0.3s;
}
.delete-task-btn:hover { color: var(--danger); }

.fade-enter-active, .fade-leave-active { transition: opacity 0.3s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
@keyframes slideInRight { from { transform: translateX(100%); } to { transform: translateX(0); } }
</style>
