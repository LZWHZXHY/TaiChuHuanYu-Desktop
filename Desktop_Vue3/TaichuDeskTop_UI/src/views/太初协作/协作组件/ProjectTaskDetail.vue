<template>
  <Transition name="fade">
    <div v-if="isOpen" class="modal-overlay drawer-overlay" @click.self="closeDrawer">
      <div class="task-detail-drawer">
        <header class="drawer-header">
          <div class="header-left-meta">
            <span class="task-id-large">#{{ localTask.id?.substring(0, 8) }}</span>
            <span class="points-badge" title="灵脉意图贡献点">⚡ {{ localTask.points ?? 1 }} 点</span>
          </div>
          <button class="close-btn" @click="closeDrawer">×</button>
        </header>

        <div class="drawer-content">
          <input 
            class="huge-title-input" 
            v-model="localTask.title" 
            placeholder="任务核心意图..." 
          />

          <!-- 基础属性网格 -->
          <div class="properties-grid">
            <div class="prop-item">
              <label>所属维度 (分类)</label>
              <select v-model="localTask.categoryId">
                <option :value="null">游离意图 (未分类)</option>
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
            <label>深层描绘 (Description)</label>
            <textarea 
              v-model="localTask.description" 
              placeholder="在此展开意图的细节..."
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
            抹除此意图
          </button>
          
          <span class="save-status" v-if="isSaving">正在同步灵脉...</span>
          <button class="save-btn" @click="handleSave">确立修改</button>
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
  if (!props.projectId) return;
  try {
    const res: any = await request.get(`/project/${props.projectId}/roles/my-permissions`);
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

const closeDrawer = () => emit('close');

const addTag = () => {
  const val = newTagInput.value.trim();
  if (val && !tagArray.value.includes(val)) {
    tagArray.value.push(val);
  }
  newTagInput.value = '';
};

const removeTag = (index: number) => tagArray.value.splice(index, 1);

const handleSave = async () => {
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
    closeDrawer();
  } catch (err: any) {
    console.error("意图细节同步失败", err);
    alert(err.response?.data?.message || err.response?.data || "更新失败，请确认权限");
  } finally {
    isSaving.value = false;
  }
};

const handleDelete = async () => {
  const isConfirmed = await openInternalConfirm("抹除意图", "确定要将这一意图卡片彻底从画布中抹除吗？此操作将无法撤销。");
  if (!isConfirmed) return;
  emit('confirmDelete', localTask.value.id);
};
</script>

<style scoped>
.modal-overlay {
  position: fixed; top: 0; left: 0; right: 0; bottom: 0;
  background: rgba(255, 255, 255, 0.85); backdrop-filter: blur(8px);
  display: flex; align-items: center; justify-content: center; z-index: 1000;
}
.drawer-overlay { background: rgba(0, 0, 0, 0.2); align-items: stretch; justify-content: flex-end; z-index: 1000; }

.drawer-modal-overlay {
  position: fixed; 
  top: 0; 
  left: 0; 
  right: 0; 
  bottom: 0;
  background: rgba(0, 0, 0, 0.4); 
  backdrop-filter: blur(6px);
  display: flex; 
  align-items: center; 
  justify-content: center; 
  z-index: 99999;
}

.minimal-modal { 
  background: #fff; 
  width: 100%; 
  max-width: 440px; 
  padding: 48px; 
  border: 1px solid #eee; 
  box-shadow: 0 40px 100px rgba(0,0,0,0.1); 
}
.modal-inner-header h2 { font-size: 1.2rem; font-weight: 500; margin: 0 0 12px 0; color: #1a1a1a; }
.modal-inner-header p { font-size: 0.85rem; color: #777; line-height: 1.6; margin: 0; }
.modal-footer { margin-top: 40px; display: flex; justify-content: flex-end; gap: 16px; }
.cancel-btn { background: none; border: none; color: #999; font-size: 0.85rem; cursor: pointer; padding: 10px 20px; transition: color 0.3s; }
.cancel-btn:hover { color: #1a1a1a; }
.confirm-btn { background: #1a1a1a; color: #fff; border: none; font-size: 0.85rem; cursor: pointer; padding: 10px 28px; border-radius: 2px; transition: background 0.3s; }
.confirm-btn:hover { background: #333; }

.task-detail-drawer {
  background: #fff; width: 100%; max-width: 740px; height: 100%;
  box-shadow: -20px 0 50px rgba(0,0,0,0.05); display: flex; flex-direction: column; overflow: hidden;
  animation: slideInRight 0.4s cubic-bezier(0.16, 1, 0.3, 1);
}

.drawer-header {
  padding: 32px 40px; border-bottom: 1px solid #f5f5f5;
  display: flex; justify-content: space-between; align-items: center;
}
.header-left-meta { display: flex; align-items: center; gap: 14px; }
.task-id-large { font-family: monospace; color: #ccc; font-size: 1.1rem; }
.points-badge {
  background: #1a1a1a; color: #fff; font-size: 0.72rem; padding: 3px 8px; border-radius: 2px;
  letter-spacing: 0.5px;
}
.close-btn { background: none; border: none; font-size: 2rem; line-height: 1; color: #aaa; cursor: pointer; transition: color 0.3s;}
.close-btn:hover { color: #1a1a1a; }

.drawer-content { flex: 1; padding: 40px; overflow-y: auto; }
.huge-title-input {
  width: 100%; font-size: 1.8rem; font-weight: 500; border: none; border-bottom: 1px solid transparent;
  color: #1a1a1a; padding-bottom: 10px; margin-bottom: 40px; outline: none; transition: border-color 0.3s;
}
.huge-title-input:focus { border-bottom-color: #eee; }

.properties-grid { 
  display: grid; 
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); 
  gap: 28px; 
  margin-bottom: 40px; 
}
.prop-item label { display: block; font-size: 0.7rem; color: #aaa; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 8px; }
.prop-item select, .prop-item input {
  width: 100%; padding: 10px 0; border: none; border-bottom: 1px solid #eee;
  background: transparent; outline: none; font-size: 0.95rem; color: #333; cursor: pointer;
}
.prop-item select:disabled {
  background: #fafafa;
  color: #aaa;
  cursor: not-allowed;
}

.quant-section {
  background: #fafafa;
  border: 1px solid #f0f0f0;
  padding: 24px;
  margin-bottom: 40px;
}
.section-label-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}
.section-label {
  display: block;
  font-size: 0.7rem;
  color: #888;
  text-transform: uppercase;
  letter-spacing: 1.5px;
  font-weight: 600;
  margin: 0;
}
.lock-notice {
  font-size: 0.7rem;
  color: #bbb;
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
  color: #555;
  font-weight: 500;
}
.quant-input {
  border: 1px solid #e0e0e0;
  background: #fff;
  padding: 8px 10px;
  font-size: 0.95rem;
  outline: none;
  transition: all 0.2s;
}
.quant-input:focus {
  border-color: #1a1a1a;
}
.quant-input.is-locked {
  background: #f5f5f5;
  border-color: #eee;
  color: #888;
  cursor: not-allowed;
}
.quant-hint {
  font-size: 0.65rem;
  color: #aaa;
  line-height: 1.3;
}

.tags-section { margin-bottom: 40px; }
.tags-section label { display: block; font-size: 0.7rem; color: #aaa; margin-bottom: 12px; }
.tags-container { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
.editable-tag { background: #f0f0f0; padding: 6px 12px; font-size: 0.8rem; color: #444; border-radius: 4px; display: flex; align-items: center; gap: 6px; }
.tag-remove { cursor: pointer; color: #aaa; transition: color 0.2s; }
.tag-remove:hover { color: #ff4757; }
.tag-input { border: none; background: transparent; font-size: 0.85rem; outline: none; border-bottom: 1px dashed #ccc; padding: 4px; width: 120px; }

.desc-section label { display: block; font-size: 0.7rem; color: #aaa; margin-bottom: 12px; text-transform: uppercase; }
.desc-section textarea {
  width: 100%; border: 1px solid #eee; background: #fafafa; padding: 16px;
  font-size: 0.95rem; color: #333; outline: none; resize: vertical; line-height: 1.6; transition: border-color 0.3s;
}
.desc-section textarea:focus { border-color: #ddd; background: #fff; }

.drawer-footer {
  padding: 24px 40px; border-top: 1px solid #f5f5f5; display: flex; justify-content: flex-end; align-items: center; gap: 20px;
}
.save-status { font-size: 0.8rem; color: #999; }
.save-btn { background: #1a1a1a; color: #fff; border: none; padding: 12px 32px; font-size: 0.9rem; cursor: pointer; transition: background 0.3s; }
.save-btn:hover { background: #333; }

.delete-task-btn {
  background: none; border: none; color: #ccc; font-size: 0.85rem; cursor: pointer; padding: 12px 0; margin-right: auto;
  transition: color 0.3s;
}
.delete-task-btn:hover { color: #ff4757; }

.fade-enter-active, .fade-leave-active { transition: opacity 0.3s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
@keyframes slideInRight { from { transform: translateX(100%); } to { transform: translateX(0); } }
</style>