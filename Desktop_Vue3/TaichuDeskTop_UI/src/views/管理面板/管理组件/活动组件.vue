<template>
  <div class="admin-container">
    <div class="admin-header">
      <h2 class="page-title">活动管理</h2>
      <button class="primary-btn" @click="openCreateModal">+ 新增活动</button>
    </div>

    <!-- 数据表格 -->
    <div class="table-wrapper">
      <table class="minimal-table">
        <thead>
          <tr>
            <th>活动标题</th>
            <th>日期范围</th>
            <th>时间</th>
            <th>状态</th>
            <th class="actions-col">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading" class="empty-row"><td colspan="5">加载中...</td></tr>
          <tr v-else-if="!events.length" class="empty-row"><td colspan="5">暂无活动数据</td></tr>
          
          <tr v-for="event in events" :key="event.id" class="data-row">
            <td class="font-medium">{{ event.title }}</td>
            <td class="text-secondary">
              {{ event.startDate }} <template v-if="event.startDate !== event.endDate">至 {{ event.endDate }}</template>
            </td>
            <td class="text-secondary">{{ event.startTime || '--' }} - {{ event.endTime || '--' }}</td>
            <td>
              <!-- 快捷状态切换下拉框 -->
              <select 
                class="status-select" 
                :class="getStatusClass(event.status)"
                v-model="event.status"
                @change="handleStatusChange(event)"
              >
                <option :value="0">草稿</option>
                <option :value="1">未开始</option>
                <option :value="2">进行中</option>
                <option :value="3">已结束</option>
                <option :value="4">已取消</option>
              </select>
            </td>
            <td class="actions-col">
              <button class="action-btn edit" @click="openEditModal(event)">编辑</button>
              <button class="action-btn delete" @click="handleDelete(event.id)">删除</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- 编辑/新增 弹窗 -->
    <Teleport to="body">
      <Transition name="fade">
        <div v-if="isModalOpen" class="modal-overlay" @click.self="closeModal">
          <div class="modal-content form-modal">
            <button class="modal-close-btn" @click="closeModal">×</button>
            <h3 class="modal-title">{{ isEditing ? '编辑活动' : '新增活动' }}</h3>
            
            <form @submit.prevent="submitForm" class="admin-form">
              <div class="form-group">
                <label>活动标题 <span class="required">*</span></label>
                <input type="text" v-model="formData.title" required placeholder="例如：共修讨论" />
              </div>
              
              <div class="form-row">
                <div class="form-group">
                  <label>开始日期 <span class="required">*</span></label>
                  <input type="date" v-model="formData.startDate" required />
                </div>
                <div class="form-group">
                  <label>结束日期 <span class="required">*</span></label>
                  <input type="date" v-model="formData.endDate" required />
                </div>
              </div>

              <div class="form-row">
                <div class="form-group">
                  <label>开始时间</label>
                  <input type="time" v-model="formData.startTime" />
                </div>
                <div class="form-group">
                  <label>结束时间</label>
                  <input type="time" v-model="formData.endTime" />
                </div>
              </div>

              <div class="form-group">
                <label>活动状态</label>
                <select v-model="formData.status">
                  <option :value="0">草稿</option>
                  <option :value="1">未开始 (已发布)</option>
                  <option :value="2">进行中</option>
                  <option :value="3">已结束</option>
                  <option :value="4">已取消</option>
                </select>
              </div>

              <div class="form-group">
                <label>活动描述</label>
                <textarea v-model="formData.description" rows="3" placeholder="填写活动详情..."></textarea>
              </div>

              <div class="form-actions">
                <button type="button" class="cancel-btn" @click="closeModal">取消</button>
                <button type="submit" class="primary-btn" :disabled="saving">
                  {{ saving ? '保存中...' : '保存' }}
                </button>
              </div>
            </form>
          </div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { eventApi, type EventDto, EventStatus } from '@/api/event' 

const events = ref<EventDto[]>([])
const loading = ref(false)
const saving = ref(false)

// 弹窗控制
const isModalOpen = ref(false)
const isEditing = ref(false)

// 表单数据绑定
const defaultForm = (): Partial<EventDto> => ({
  title: '',
  description: '',
  startDate: new Date().toISOString().split('T')[0],
  endDate: new Date().toISOString().split('T')[0],
  startTime: '',
  endTime: '',
  status: EventStatus.Draft
})

const formData = ref<Partial<EventDto>>(defaultForm())

// 获取列表数据
const fetchEvents = async () => {
  loading.value = true
  try {
    // 🌟 真实调用获取所有列表接口
    events.value = await eventApi.getAllEvents() 
  } catch (error) {
    console.error('获取列表失败', error)
  } finally {
    loading.value = false
  }
}

onMounted(fetchEvents)

// 打开新增弹窗
const openCreateModal = () => {
  isEditing.value = false
  formData.value = defaultForm()
  isModalOpen.value = true
  document.body.style.overflow = 'hidden'
}

// 打开编辑弹窗
const openEditModal = (event: EventDto) => {
  isEditing.value = true
  formData.value = { ...event } // 深拷贝
  isModalOpen.value = true
  document.body.style.overflow = 'hidden'
}

const closeModal = () => {
  isModalOpen.value = false
  document.body.style.overflow = ''
}

// 提交表单
const submitForm = async () => {
  saving.value = true
  try {
    if (isEditing.value && formData.value.id) {
      // 🌟 真实调用更新接口
      await eventApi.updateEvent(formData.value.id, formData.value)
    } else {
      // 🌟 真实调用创建接口
      await eventApi.createEvent(formData.value)
    }
    await fetchEvents() // 刷新表格数据
    closeModal()
  } catch (error) {
    alert('保存失败，请检查网络或后台报错')
    console.error(error)
  } finally {
    saving.value = false
  }
}

// 表格内快捷修改状态
const handleStatusChange = async (event: EventDto) => {
  try {
    // 🌟 真实调用局部更新状态接口
    await eventApi.updateEventStatus(event.id, event.status)
  } catch (error) {
    alert('状态更新失败')
    await fetchEvents() // 更新失败时重新拉取数据以恢复正确的UI状态
  }
}

// 删除操作
const handleDelete = async (id: string) => {
  if (confirm('确定要删除这个活动吗？操作不可恢复。')) {
    try {
      // 🌟 真实调用删除接口
      await eventApi.deleteEvent(id)
      await fetchEvents() // 删除成功后刷新表格数据
    } catch (error) {
      alert('删除失败')
    }
  }
}

// 状态样式映射
const getStatusClass = (status: number) => {
  if (status === EventStatus.Ongoing) return 'status-ongoing'
  if (status === EventStatus.Completed) return 'status-completed'
  if (status === EventStatus.Cancelled) return 'status-cancelled'
  if (status === EventStatus.Draft) return 'status-draft'
  return 'status-normal' // Published
}
</script>

<style scoped>
/* 容器样式 */
.admin-container {
  max-width: 1000px;
  margin: 0 auto;
  padding: 2rem 1rem;
  font-family: system-ui, -apple-system, sans-serif;
  color: #1a1f2c;
}

.admin-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
}

.page-title {
  font-size: 1.5rem;
  font-weight: 500;
  margin: 0;
}

/* 极简按钮 */
.primary-btn {
  background: #1a1f2c;
  color: #fff;
  border: none;
  padding: 0.5rem 1.25rem;
  border-radius: 6px;
  font-size: 0.9rem;
  cursor: pointer;
  transition: opacity 0.2s;
}
.primary-btn:hover { opacity: 0.9; }
.primary-btn:disabled { opacity: 0.5; cursor: not-allowed; }

.cancel-btn {
  background: transparent;
  color: #6c7e97;
  border: 1px solid #d4dae2;
  padding: 0.5rem 1.25rem;
  border-radius: 6px;
  font-size: 0.9rem;
  cursor: pointer;
  transition: all 0.2s;
}
.cancel-btn:hover { background: #f4f6f9; color: #1a1f2c; }

/* 表格设计: 极简线条，大量留白 */
.table-wrapper {
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.02);
  overflow-x: auto;
}

.minimal-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
  font-size: 0.9rem;
}

.minimal-table th {
  padding: 1rem;
  color: #8b98a9;
  font-weight: 500;
  border-bottom: 1px solid #e9ecef;
  background: #f8fafc;
}

.minimal-table td {
  padding: 1rem;
  border-bottom: 1px solid #f0f2f5;
  vertical-align: middle;
}

.data-row:hover {
  background-color: #fcfcfd;
}

.empty-row td {
  text-align: center;
  color: #8b98a9;
  padding: 3rem 0;
}

.font-medium { font-weight: 500; }
.text-secondary { color: #6c7e97; font-size: 0.85rem; }

/* 操作列与按钮 */
.actions-col { text-align: right; }
.action-btn {
  background: none;
  border: none;
  font-size: 0.85rem;
  cursor: pointer;
  margin-left: 0.75rem;
  padding: 0;
}
.action-btn.edit { color: #4b6bfb; }
.action-btn.delete { color: #ef4444; }
.action-btn:hover { text-decoration: underline; }

/* 状态下拉框设计 */
.status-select {
  appearance: none;
  border: none;
  background: transparent;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  font-size: 0.8rem;
  font-weight: 500;
  cursor: pointer;
  outline: none;
}
.status-select:hover { filter: brightness(0.95); }
.status-normal { background: #f0f4fe; color: #4b6bfb; }
.status-ongoing { background: #fff5e6; color: #e68a2e; }
.status-completed { background: #f0f9f0; color: #4c9f70; }
.status-cancelled { background: #f1f5f9; color: #64748b; }
.status-draft { background: #f3f4f6; color: #475569; border: 1px dashed #cbd5e1; }

/* 表单内部布局 */
.admin-form {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.form-row {
  display: flex;
  gap: 1rem;
}
.form-row .form-group {
  flex: 1;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.form-group label {
  font-size: 0.85rem;
  color: #6c7e97;
  font-weight: 500;
}
.required { color: #ef4444; }

.form-group input, 
.form-group select, 
.form-group textarea {
  padding: 0.6rem;
  border: 1px solid #d4dae2;
  border-radius: 6px;
  font-size: 0.9rem;
  color: #1a1f2c;
  outline: none;
  transition: border-color 0.2s;
  background: #fff;
  font-family: inherit;
}
.form-group input:focus, 
.form-group select:focus, 
.form-group textarea:focus {
  border-color: #1a1f2c;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 1rem;
  margin-top: 1rem;
  padding-top: 1.5rem;
  border-top: 1px solid #e9ecef;
}

/* ==========================================
   弹窗与遮罩层样式
   ========================================== */
.form-modal {
  max-width: 500px;
  padding: 2rem;
}

.modal-overlay {
  position: fixed;
  top: 0; left: 0; width: 100vw; height: 100vh;
  background-color: rgba(26, 31, 44, 0.4);
  backdrop-filter: blur(4px);
  -webkit-backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 9999;
}

.modal-content {
  background: #fff;
  width: 90%;
  border-radius: 16px;
  position: relative;
  box-shadow: 0 20px 40px rgba(0,0,0,0.1);
}

.modal-close-btn {
  position: absolute;
  top: 1rem;
  right: 1.2rem;
  background: none;
  border: none;
  font-size: 1.5rem;
  color: #8b98a9;
  cursor: pointer;
  line-height: 1;
  transition: color 0.2s;
}
.modal-close-btn:hover { color: #1a1f2c; }

.modal-title {
  margin-top: 0;
  margin-bottom: 1.5rem;
  font-size: 1.25rem;
  font-weight: 500;
}

/* Vue 过渡动画 */
.fade-enter-active, .fade-leave-active { transition: opacity 0.25s ease; }
.fade-enter-active .modal-content, .fade-leave-active .modal-content { transition: transform 0.25s cubic-bezier(0.16, 1, 0.3, 1); }

.fade-enter-from, .fade-leave-to { opacity: 0; }
.fade-enter-from .modal-content { transform: translateY(15px) scale(0.98); }
.fade-leave-to .modal-content { transform: translateY(10px) scale(0.98); }
</style>