<template>
  <div class="sign-container">
    <div class="calendar-header">
      <div class="current-month">
        <button @click="changeMonth(-1)" class="nav-btn" :disabled="loading">←</button>
        <span class="month-display">{{ viewYear }}年 {{ viewMonth + 1 }}月</span>
        <button @click="changeMonth(1)" class="nav-btn" :disabled="loading">→</button>
      </div>

      <div class="sign-stats">
        <button 
          v-if="isCurrentMonth"
          @click="handleSignIn" 
          class="sign-action-btn"
          :disabled="loading || isTodaySigned"
        >
          {{ isTodaySigned ? '已签到' : (loading ? '...' : '签到') }}
        </button>
        <span class="stats-label">累计 {{ monthlyCount }} 天</span>
      </div>
    </div>

    <!-- 星期 -->
    <div class="weekday-grid">
      <span v-for="day in ['日', '一', '二', '三', '四', '五', '六']" :key="day">{{ day }}</span>
    </div>

    <!-- 日历网格 -->
    <div class="calendar-grid" :class="{ 'is-loading': loading }">
      <div v-for="empty in firstDayOffset" :key="'empty-' + empty" class="day-cell empty"></div>
      
      <div 
        v-for="date in daysInMonth" 
        :key="date" 
        class="day-cell"
        :class="[
          getSignStatus(date),
          { 
            'is-today': checkIsToday(date),
            'has-activity': hasActivityOnDate(date),
            'is-selected': selectedDate === date
          }
        ]"
        @click="handleDateClick(date)"
      >
        <span class="day-num">{{ date }}</span>
        <span v-if="hasActivityOnDate(date)" class="activity-marker">●</span>
      </div>
    </div>

    <!-- 活动详情面板 —— 极简卡片 -->
    <div class="event-panel">
      <div class="event-panel-header">活动安排</div>
      <div class="event-panel-content">
        <template v-if="selectedDateStr">
          <div class="event-date">{{ selectedDateStr }}</div>
          <div v-if="selectedActivities.length" class="event-list">
            <!-- 增加了点击事件和交互样式 -->
            <div 
              v-for="act in selectedActivities" 
              :key="act.id" 
              class="event-item clickable"
              @click="openEventDetail(act)"
            >
              <div class="event-name">
                <span class="event-time">
                  {{ act.startTime || '全天' }}
                  <template v-if="act.endTime">- {{ act.endTime }}</template>
                </span>
                {{ act.title }}
              </div>
              <!-- 列表只显示截断的简要描述 -->
              <div v-if="act.description" class="event-detail truncate">{{ act.description }}</div>
            </div>
          </div>
          <div v-else class="event-empty">— 无活动 —</div>
        </template>
        <div v-else class="event-empty">点击日期查看活动</div>
      </div>
    </div>

    <!-- 图例（极简） -->
    <div class="calendar-footer">
      <span><span class="dot green"></span> 签到</span>
      <span><span class="dot blue"></span> 补签</span>
      <span><span class="dot gray"></span> 未签</span>
      <span><span class="dot orange"></span> 有活动</span>
    </div>

    <!-- 活动详情弹窗 -->
    <Teleport to="body">
      <Transition name="fade">
        <div v-if="isModalOpen" class="modal-overlay" @click.self="closeEventDetail">
          <div class="modal-content">
            <button class="modal-close-btn" @click="closeEventDetail">×</button>
            
            <div class="modal-header">
              <span class="status-badge" :class="getStatusClass(selectedEvent?.status)">
                {{ getStatusText(selectedEvent?.status) }}
              </span>
              <h3 class="modal-title">{{ selectedEvent?.title }}</h3>
            </div>
            
            <div class="modal-body">
              <div class="info-row">
                <span class="info-label">时间</span>
                <span class="info-value">
                  {{ selectedEvent?.startDate }} {{ selectedEvent?.startTime || '' }} 
                  <template v-if="selectedEvent?.startDate !== selectedEvent?.endDate || selectedEvent?.endTime">
                    <span class="time-divider">至</span>
                    {{ selectedEvent?.endDate !== selectedEvent?.startDate ? selectedEvent?.endDate : '' }}
                    {{ selectedEvent?.endTime || '' }}
                  </template>
                </span>
              </div>
              
              <div class="info-row description-row">
                <span class="info-label">详情</span>
                <div class="info-value desc-text">{{ selectedEvent?.description || '暂无描述' }}</div>
              </div>
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { signApi, type SignData } from '../api/sign'
import { eventApi, type EventDto, EventStatus } from '../api/event' // 🌟 引入真实的活动API
import { useExpNotify } from '../composables/useExpNotify'
import { useUserStore } from '../stores/user'

const userStore = useUserStore()
const { notify } = useExpNotify()

const signData = ref<SignData>({})
const activitiesData = ref<Record<string, EventDto[]>>({}) // 🌟 使用真实类型
const loading = ref(false)

const now = new Date()
const viewYear = ref(now.getFullYear())
const viewMonth = ref(now.getMonth())
const selectedDate = ref<number | null>(null)

// 🌟 弹窗控制状态
const isModalOpen = ref(false)
const selectedEvent = ref<EventDto | null>(null)

const selectedDateStr = computed(() => {
  if (selectedDate.value === null) return ''
  return `${viewYear.value}-${String(viewMonth.value + 1).padStart(2, '0')}-${String(selectedDate.value).padStart(2, '0')}`
})

const selectedActivities = computed(() => {
  if (!selectedDateStr.value) return []
  return activitiesData.value[selectedDateStr.value] || []
})

const daysInMonth = computed(() => new Date(viewYear.value, viewMonth.value + 1, 0).getDate())
const firstDayOffset = computed(() => new Date(viewYear.value, viewMonth.value, 1).getDay())

const isCurrentMonth = computed(() => viewYear.value === now.getFullYear() && viewMonth.value === now.getMonth())
const isTodaySigned = computed(() => {
  const todayStr = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
  return signData.value[todayStr] !== undefined
})
const monthlyCount = computed(() => {
  const prefix = `${viewYear.value}-${String(viewMonth.value + 1).padStart(2, '0')}`
  return Object.keys(signData.value).filter(k => k.startsWith(prefix)).length
})

const getDateKey = (date: number) => `${viewYear.value}-${String(viewMonth.value + 1).padStart(2, '0')}-${String(date).padStart(2, '0')}`
const hasActivityOnDate = (date: number) => !!(activitiesData.value[getDateKey(date)]?.length)

const handleDateClick = (date: number) => { selectedDate.value = date }

// 获取签到数据
const fetchSignData = async () => {
  try {
    signData.value = await signApi.getMonthData(viewYear.value, viewMonth.value + 1)
  } catch (e) { console.error(e) }
}

// 🌟 获取真实活动数据
const fetchActivitiesData = async () => {
  try {
    const data = await eventApi.getMonthEvents(viewYear.value, viewMonth.value + 1)
    activitiesData.value = data || {}
  } catch (e) {
    console.error('获取活动失败', e)
    activitiesData.value = {}
  }
}

const loadAll = async () => {
  loading.value = true
  await Promise.all([fetchSignData(), fetchActivitiesData()])
  if (isCurrentMonth.value && now.getDate() <= daysInMonth.value) selectedDate.value = now.getDate()
  else selectedDate.value = 1
  loading.value = false
}

const handleSignIn = async () => {
  if (loading.value) return
  loading.value = true
  try {
    const res = await signApi.doSign()
    notify(res.experienceAdded)
    if (userStore.userInfo) userStore.userInfo.experience += res.experienceAdded
    await fetchSignData()
  } catch (err: any) {
    alert(err.response?.data?.message || '签到失败')
  } finally { loading.value = false }
}

onMounted(loadAll)
watch([viewYear, viewMonth], loadAll)

const changeMonth = (delta: number) => {
  const d = new Date(viewYear.value, viewMonth.value + delta, 1)
  viewYear.value = d.getFullYear()
  viewMonth.value = d.getMonth()
}

const getSignStatus = (date: number) => {
  const status = signData.value[getDateKey(date)]
  if (status === 1) return 'status-signed'
  if (status === 2) return 'status-repay'
  return 'status-none'
}

const checkIsToday = (date: number) => isCurrentMonth.value && date === now.getDate()

// 🌟 弹窗操作方法
const openEventDetail = (act: EventDto) => {
  selectedEvent.value = act
  isModalOpen.value = true
  document.body.style.overflow = 'hidden' // 防止背景滚动
}

const closeEventDetail = () => {
  isModalOpen.value = false
  setTimeout(() => { selectedEvent.value = null }, 300) // 等待动画结束清理数据
  document.body.style.overflow = ''
}

// 🌟 状态文字映射
const getStatusText = (status?: number) => {
  if (status === undefined) return ''
  switch(status) {
    case EventStatus.Draft: return '草稿'
    case EventStatus.Published: return '未开始'
    case EventStatus.Ongoing: return '进行中'
    case EventStatus.Completed: return '已结束'
    case EventStatus.Cancelled: return '已取消'
    default: return '未知'
  }
}

const getStatusClass = (status?: number) => {
  if (status === EventStatus.Ongoing) return 'badge-ongoing'
  if (status === EventStatus.Completed) return 'badge-completed'
  if (status === EventStatus.Cancelled) return 'badge-cancelled'
  return 'badge-normal'
}
</script>

<style scoped>
/* 保持原有基础样式不变 */
.sign-container {
  max-width: 720px;
  margin: 0 auto;
  padding: 2rem 1rem;
  font-family: system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', sans-serif;
  color: #1a1f2c;
  background: #fff;
}

.calendar-header { display: flex; justify-content: space-between; align-items: baseline; flex-wrap: wrap; margin-bottom: 2rem; padding-bottom: 0.5rem; border-bottom: 1px solid #e9ecef; }
.current-month { display: flex; align-items: baseline; gap: 0.5rem; }
.nav-btn { background: none; border: none; font-size: 1.2rem; cursor: pointer; color: #8b98a9; padding: 0 0.25rem; transition: color 0.1s; }
.nav-btn:hover:not(:disabled) { color: #1a1f2c; }
.nav-btn:disabled { opacity: 0.3; cursor: default; }
.month-display { font-size: 1.2rem; font-weight: 450; letter-spacing: -0.2px; }
.sign-stats { display: flex; align-items: baseline; gap: 1rem; }
.sign-action-btn { background: none; border: 1px solid #d4dae2; padding: 0.2rem 1rem; font-size: 0.85rem; border-radius: 20px; cursor: pointer; color: #2c3e4e; transition: all 0.1s; }
.sign-action-btn:hover:not(:disabled) { background: #f4f6f9; border-color: #b9c3ce; }
.sign-action-btn:disabled { color: #b9c3ce; cursor: default; }
.stats-label { font-size: 0.85rem; color: #6c7e97; }

.weekday-grid { display: grid; grid-template-columns: repeat(7, 1fr); text-align: center; font-size: 0.75rem; color: #8b98a9; margin-bottom: 0.5rem; letter-spacing: 0.3px; }
.calendar-grid { display: grid; grid-template-columns: repeat(7, 1fr); gap: 0.25rem; }
.day-cell { aspect-ratio: 1 / 1; display: flex; flex-direction: column; align-items: center; justify-content: center; position: relative; cursor: pointer; font-size: 0.9rem; border-radius: 0; background: transparent; transition: background 0.1s; }
.day-cell.empty { cursor: default; }
.day-num { font-weight: 400; color: #1e293b; }
.status-signed { background: #f0f9f0; }
.status-repay { background: #f0f4fe; }
.status-none { background: transparent; }
.is-today .day-num { font-weight: 500; border-bottom: 1px solid #1a1f2c; }
.is-selected { background: #f4f7fb; outline: none; }
.is-selected .day-num { font-weight: 500; }
.activity-marker { font-size: 6px; color: #e68a2e; margin-top: 2px; line-height: 1; }

.event-panel { margin-top: 2rem; border-top: 1px solid #e9ecef; padding-top: 1.5rem; }
.event-panel-header { font-size: 0.8rem; text-transform: uppercase; letter-spacing: 1px; color: #8b98a9; margin-bottom: 1rem; }
.event-panel-content { font-size: 0.9rem; line-height: 1.5; }
.event-date { font-size: 0.8rem; color: #6c7e97; margin-bottom: 1rem; font-family: monospace; }
.event-list { display: flex; flex-direction: column; gap: 0.5rem; } /* 缩小了 gap */

/* 🌟 活动列表项增强交互 */
.event-item { 
  padding: 0.75rem; 
  margin: 0 -0.75rem; /* 抵消内边距，保持文字对齐 */
  border-radius: 8px;
  border-bottom: 1px solid #f0f2f5; 
  transition: background-color 0.2s ease;
}
.event-item.clickable { cursor: pointer; }
.event-item.clickable:hover { background-color: #f8fafc; border-bottom-color: transparent; }

.event-name { font-weight: 500; }
.event-time { display: inline-block; font-size: 0.75rem; font-family: monospace; color: #e68a2e; margin-right: 0.75rem; }
.event-detail { font-size: 0.8rem; color: #6c7e97; margin-top: 0.25rem; }
.truncate { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 90%; }
.event-empty { color: #b9c3ce; font-size: 0.85rem; padding: 1rem 0; text-align: center; }

.calendar-footer { margin-top: 2rem; padding-top: 1rem; border-top: 1px solid #e9ecef; display: flex; gap: 1.5rem; font-size: 0.7rem; color: #8b98a9; }
.dot { display: inline-block; width: 8px; height: 8px; border-radius: 50%; margin-right: 4px; }
.dot.green { background: #4c9f70; }
.dot.blue { background: #6c8ebf; }
.dot.gray { background: #cbd5e1; }
.dot.orange { background: #e68a2e; }
.calendar-grid.is-loading { opacity: 0.5; pointer-events: none; }

@media (max-width: 560px) {
  .sign-container { padding: 1rem; }
  .day-cell { font-size: 0.8rem; }
  .calendar-footer { gap: 1rem; }
}
</style>

<!-- 🌟 弹窗的全局样式（不加 scoped，或直接在 scoped 中确保影响到 Teleport） -->
<style>
/* 弹窗基础：毛玻璃遮罩 */
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

/* 弹窗容器：大圆角，足留白 */
.modal-content {
  background: #fff;
  width: 90%;
  max-width: 440px;
  border-radius: 16px;
  padding: 2.5rem 2rem;
  position: relative;
  box-shadow: 0 20px 40px rgba(0,0,0,0.1);
  font-family: system-ui, -apple-system, sans-serif;
  color: #1a1f2c;
}

/* 关闭按钮 */
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

/* 头部排版 */
.modal-header { margin-bottom: 2rem; }
.status-badge {
  display: inline-block;
  font-size: 0.7rem;
  padding: 0.2rem 0.6rem;
  border-radius: 20px;
  margin-bottom: 0.75rem;
  letter-spacing: 0.5px;
}
.badge-normal { background: #f0f4fe; color: #4b6bfb; } /* 未开始 */
.badge-ongoing { background: #fff5e6; color: #e68a2e; } /* 进行中 */
.badge-completed { background: #f0f9f0; color: #4c9f70; } /* 已结束 */
.badge-cancelled { background: #f1f5f9; color: #64748b; } /* 已取消/草稿 */

.modal-title {
  margin: 0;
  font-size: 1.3rem;
  font-weight: 500;
  line-height: 1.4;
}

/* 内容排版：列表结构 */
.modal-body { display: flex; flex-direction: column; gap: 1.25rem; }
.info-row { display: flex; align-items: flex-start; gap: 1rem; }
.info-label {
  font-size: 0.85rem;
  color: #8b98a9;
  width: 2.5rem;
  flex-shrink: 0;
  padding-top: 0.1rem;
}
.info-value {
  font-size: 0.95rem;
  color: #2c3e4e;
  line-height: 1.5;
  word-break: break-all;
}
.time-divider { color: #b9c3ce; margin: 0 0.4rem; font-size: 0.8rem; }
.desc-text { color: #5a6b82; white-space: pre-wrap; }

/* Vue 过渡动画 */
.fade-enter-active, .fade-leave-active { transition: opacity 0.25s ease; }
.fade-enter-active .modal-content, .fade-leave-active .modal-content { transition: transform 0.25s cubic-bezier(0.16, 1, 0.3, 1); }

.fade-enter-from, .fade-leave-to { opacity: 0; }
.fade-enter-from .modal-content { transform: translateY(15px) scale(0.98); }
.fade-leave-to .modal-content { transform: translateY(10px) scale(0.98); }
</style>