<template>
  <Transition name="fade">
    <div v-if="isOpen" class="modal-overlay drawer-overlay" @click.self="closeDrawer">
      <div class="task-detail-drawer">
        <header class="drawer-header">
          <span class="task-id-large">#{{ localTask.id?.substring(0, 8) }}</span>
          <button class="close-btn" @click="closeDrawer">×</button>
        </header>

        <div class="drawer-content">
          <input 
            class="huge-title-input" 
            v-model="localTask.title" 
            placeholder="任务核心意图..." 
          />

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
              <select v-model="localTask.assigneeId">
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
          <button class="delete-task-btn" @click="handleDelete">抹除此意图</button>
          
          <span class="save-status" v-if="isSaving">正在同步灵脉...</span>
          <button class="save-btn" @click="handleSave">确立修改</button>
        </footer>
      </div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue';
import projectService from '../../../api/projectService';

const props = defineProps<{
  isOpen: boolean;
  projectId: string;
  task: any;                  // 激活的任务基准数据
  originalCategoryId: string | null;
  boardCategories: any[];     // 可选的分栏维度
  projectMembers: any[];      // 注入的共建者池
}>();

const emit = defineEmits(['close', 'refresh', 'confirmDelete']);

const isSaving = ref(false);
const newTagInput = ref('');
const localTask = ref<any>({});
const tagArray = ref<string[]>([]);

// 深度监听传入任务的激活状态，完成隔离的深拷贝复制与日期截取
watch(() => props.isOpen, (newVal) => {
  if (newVal && props.task) {
    localTask.value = JSON.parse(JSON.stringify(props.task));
    tagArray.value = localTask.value.tags ? localTask.value.tags.split(',').filter(Boolean) : [];
    
    // 🌟 核心同步：防御并截取开始时间的 T00:00:00 后缀，使其能原生地在 <input type="date"> 中初始化赋默认值
    if (localTask.value.startDate) {
      localTask.value.startDate = localTask.value.startDate.split('T')[0];
    }
    if (localTask.value.dueDate) {
      localTask.value.dueDate = localTask.value.dueDate.split('T')[0];
    }
  }
}, { immediate: true });

const closeDrawer = () => {
  emit('close');
};

const addTag = () => {
  const val = newTagInput.value.trim();
  if (val && !tagArray.value.includes(val)) {
    tagArray.value.push(val);
  }
  newTagInput.value = '';
};

const removeTag = (index: number) => {
  tagArray.value.splice(index, 1);
};

// 保存细节并自动处理跨维度分栏排序
const handleSave = async () => {
  isSaving.value = true;
  localTask.value.tags = tagArray.value.join(',');
  
  // 🌟 时空防御：若用户清除日期，将其重置为 null 传给后端 UpdateTaskDto，防止空字符串导致 .NET 反序列化异常
  const submitPayload = {
    ...localTask.value,
    startDate: localTask.value.startDate || null,
    dueDate: localTask.value.dueDate || null
  };

  try {
    // 1. 同步详情全量数据
    await projectService.updateTaskDetails(props.projectId, submitPayload.id, submitPayload);
    
    // 2. 如果分栏节点发生了变化，自动执行越栏跨区排序
    if (submitPayload.categoryId !== props.originalCategoryId) {
      await projectService.moveKanbanTask(props.projectId, submitPayload.id, {
        targetCategoryId: submitPayload.categoryId,
        prevSortOrder: null,
        nextSortOrder: null
      });
    }
    emit('refresh'); // 通知母画布（或横向时间轴长卷）拉取最新投影
    closeDrawer();
  } catch (err) {
    console.error("意图细节同步失败", err);
  } finally {
    isSaving.value = false;
  }
};

const handleDelete = () => {
  emit('confirmDelete', localTask.value.id);
};
</script>

<style scoped>
.modal-overlay {
  position: fixed; top: 0; left: 0; right: 0; bottom: 0;
  background: rgba(255, 255, 255, 0.85); backdrop-filter: blur(8px);
  display: flex; align-items: center; justify-content: center; z-index: 1000;
}
.drawer-overlay { background: rgba(0, 0, 0, 0.2); align-items: stretch; justify-content: flex-end; }

.task-detail-drawer {
  background: #fff; width: 100%; max-width: 720px; height: 100%;
  box-shadow: -20px 0 50px rgba(0,0,0,0.05); display: flex; flex-direction: column; overflow: hidden;
  animation: slideInRight 0.4s cubic-bezier(0.16, 1, 0.3, 1);
}

.drawer-header {
  padding: 32px 40px; border-bottom: 1px solid #f5f5f5;
  display: flex; justify-content: space-between; align-items: center;
}
.task-id-large { font-family: monospace; color: #ccc; font-size: 1.1rem; }
.close-btn { background: none; border: none; font-size: 2rem; line-height: 1; color: #aaa; cursor: pointer; transition: color 0.3s;}
.close-btn:hover { color: #1a1a1a; }

.drawer-content { flex: 1; padding: 40px; overflow-y: auto; }
.huge-title-input {
  width: 100%; font-size: 1.8rem; font-weight: 500; border: none; border-bottom: 1px solid transparent;
  color: #1a1a1a; padding-bottom: 10px; margin-bottom: 40px; outline: none; transition: border-color 0.3s;
}
.huge-title-input:focus { border-bottom-color: #eee; }

/* 🌟 微调为无固定比例的 Grid，使其能够优雅包裹新拓宽的 5 个属性项 */
.properties-grid { 
  display: grid; 
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); 
  gap: 30px; 
  margin-bottom: 40px; 
}
.prop-item label { display: block; font-size: 0.7rem; color: #aaa; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 8px; }
.prop-item select, .prop-item input {
  width: 100%; padding: 10px 0; border: none; border-bottom: 1px solid #eee;
  background: transparent; outline: none; font-size: 0.95rem; color: #333; cursor: pointer;
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
  transition: color 0.3s cubic-bezier(0.16, 1, 0.3, 1);
}
.delete-task-btn:hover { color: #ff4757; }

.fade-enter-active, .fade-leave-active { transition: opacity 0.3s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
@keyframes slideInRight { from { transform: translateX(100%); } to { transform: translateX(0); } }
</style>