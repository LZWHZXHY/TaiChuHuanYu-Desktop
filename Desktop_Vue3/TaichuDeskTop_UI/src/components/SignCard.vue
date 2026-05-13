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
            <div v-for="act in selectedActivities" :key="act.id" class="event-item">
              <div class="event-name">
                <span v-if="act.startTime" class="event-time">{{ act.startTime }}</span>
                {{ act.name }}
              </div>
              <div v-if="act.detail" class="event-detail">{{ act.detail }}</div>
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
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { signApi, type SignData } from '../api/sign'
import { useExpNotify } from '../composables/useExpNotify'
import { useUserStore } from '../stores/user'

interface Activity {
  id: string
  name: string
  detail?: string
  startTime?: string
}

type ActivitiesMap = Record<string, Activity[]>

const userStore = useUserStore()
const { notify } = useExpNotify()

const signData = ref<SignData>({})
const activitiesData = ref<ActivitiesMap>({})
const loading = ref(false)

const now = new Date()
const viewYear = ref(now.getFullYear())
const viewMonth = ref(now.getMonth())
const selectedDate = ref<number | null>(null)

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

const fetchSignData = async () => {
  try {
    signData.value = await signApi.getMonthData(viewYear.value, viewMonth.value + 1)
  } catch (e) { console.error(e) }
}

// 示例活动数据 (实际替换为真实API)
const fetchActivitiesData = async () => {
  await new Promise(r => setTimeout(r, 100))
  const mock: ActivitiesMap = {}
  const year = viewYear.value, month = viewMonth.value + 1, days = daysInMonth.value
  for (let d = 1; d <= days; d++) {
    const key = `${year}-${String(month).padStart(2,'0')}-${String(d).padStart(2,'0')}`
    const wd = new Date(year, month-1, d).getDay()
    const acts: Activity[] = []
    if (wd === 2 || wd === 4) acts.push({ id: `d-${d}`, name: '共修讨论', startTime: '20:00' })
    if (wd === 0) acts.push({ id: `s-${d}`, name: '修为分享', startTime: '15:00' })
    if (d === 15) acts.push({ id: `e-${d}`, name: '灵药兑换', startTime: '12:00' })
    if (acts.length) mock[key] = acts
  }
  activitiesData.value = mock
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
</script>

<style scoped>
/* ---------- 极简 · 留白 · 克制 ---------- */
.sign-container {
  max-width: 720px;
  margin: 0 auto;
  padding: 2rem 1rem;
  font-family: system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', sans-serif;
  color: #1a1f2c;
  background: #fff;
}

/* 头部 */
.calendar-header {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  flex-wrap: wrap;
  margin-bottom: 2rem;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid #e9ecef;
}
.current-month {
  display: flex;
  align-items: baseline;
  gap: 0.5rem;
}
.nav-btn {
  background: none;
  border: none;
  font-size: 1.2rem;
  cursor: pointer;
  color: #8b98a9;
  padding: 0 0.25rem;
  transition: color 0.1s;
}
.nav-btn:hover:not(:disabled) { color: #1a1f2c; }
.nav-btn:disabled { opacity: 0.3; cursor: default; }
.month-display {
  font-size: 1.2rem;
  font-weight: 450;
  letter-spacing: -0.2px;
}
.sign-stats {
  display: flex;
  align-items: baseline;
  gap: 1rem;
}
.sign-action-btn {
  background: none;
  border: 1px solid #d4dae2;
  padding: 0.2rem 1rem;
  font-size: 0.85rem;
  border-radius: 20px;
  cursor: pointer;
  color: #2c3e4e;
  transition: all 0.1s;
}
.sign-action-btn:hover:not(:disabled) {
  background: #f4f6f9;
  border-color: #b9c3ce;
}
.sign-action-btn:disabled {
  color: #b9c3ce;
  cursor: default;
}
.stats-label {
  font-size: 0.85rem;
  color: #6c7e97;
}

/* 星期 */
.weekday-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  text-align: center;
  font-size: 0.75rem;
  color: #8b98a9;
  margin-bottom: 0.5rem;
  letter-spacing: 0.3px;
}

/* 日历网格 */
.calendar-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 0.25rem;
}
.day-cell {
  aspect-ratio: 1 / 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  position: relative;
  cursor: pointer;
  font-size: 0.9rem;
  border-radius: 0;
  background: transparent;
  transition: background 0.1s;
}
.day-cell.empty {
  cursor: default;
}
.day-num {
  font-weight: 400;
  color: #1e293b;
}
/* 签到状态：只用底部细线或极淡背景 */
.status-signed {
  background: #f0f9f0;
}
.status-repay {
  background: #f0f4fe;
}
.status-none {
  background: transparent;
}
/* 今日：细下划线 */
.is-today .day-num {
  font-weight: 500;
  border-bottom: 1px solid #1a1f2c;
}
/* 选中：极淡背景 + 文字微调 */
.is-selected {
  background: #f4f7fb;
  outline: none;
}
.is-selected .day-num {
  font-weight: 500;
}
/* 活动标记：小圆点 */
.activity-marker {
  font-size: 6px;
  color: #e68a2e;
  margin-top: 2px;
  line-height: 1;
}
/* 有活动单元格，不加额外背景 */

/* 活动面板 – 极简 */
.event-panel {
  margin-top: 2rem;
  border-top: 1px solid #e9ecef;
  padding-top: 1.5rem;
}
.event-panel-header {
  font-size: 0.8rem;
  text-transform: uppercase;
  letter-spacing: 1px;
  color: #8b98a9;
  margin-bottom: 1rem;
}
.event-panel-content {
  font-size: 0.9rem;
  line-height: 1.5;
}
.event-date {
  font-size: 0.8rem;
  color: #6c7e97;
  margin-bottom: 1rem;
  font-family: monospace;
}
.event-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}
.event-item {
  padding-bottom: 0.75rem;
  border-bottom: 1px solid #f0f2f5;
}
.event-name {
  font-weight: 500;
}
.event-time {
  display: inline-block;
  font-size: 0.7rem;
  font-family: monospace;
  color: #e68a2e;
  margin-right: 0.75rem;
}
.event-detail {
  font-size: 0.8rem;
  color: #6c7e97;
  margin-top: 0.25rem;
}
.event-empty {
  color: #b9c3ce;
  font-size: 0.85rem;
  padding: 1rem 0;
  text-align: center;
}

/* 图例 */
.calendar-footer {
  margin-top: 2rem;
  padding-top: 1rem;
  border-top: 1px solid #e9ecef;
  display: flex;
  gap: 1.5rem;
  font-size: 0.7rem;
  color: #8b98a9;
}
.dot {
  display: inline-block;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  margin-right: 4px;
}
.dot.green { background: #4c9f70; }
.dot.blue { background: #6c8ebf; }
.dot.gray { background: #cbd5e1; }
.dot.orange { background: #e68a2e; }

/* 加载状态透明 */
.calendar-grid.is-loading {
  opacity: 0.5;
  pointer-events: none;
}

/* 移动端: 加大留白 */
@media (max-width: 560px) {
  .sign-container {
    padding: 1rem;
  }
  .day-cell {
    font-size: 0.8rem;
  }
  .calendar-footer {
    gap: 1rem;
  }
}
</style>