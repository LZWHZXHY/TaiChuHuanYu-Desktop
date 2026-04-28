<template>
  <div class="sign-container">
    <div class="calendar-header">
      <div class="current-month">
        <button @click="changeMonth(-1)" class="nav-btn" :disabled="loading"> &lt; </button>
        <span class="month-display">{{ viewYear }}年 {{ viewMonth + 1 }}月</span>
        <button @click="changeMonth(1)" class="nav-btn" :disabled="loading"> &gt; </button>
      </div>

      <div class="sign-stats">
        <button 
          v-if="isCurrentMonth"
          @click="handleSignIn" 
          class="sign-action-btn"
          :disabled="loading || isTodaySigned"
        >
          {{ isTodaySigned ? '今日已筑基' : (loading ? '请求中...' : '立即打卡') }}
        </button>

        <span class="stats-label">
          本月累计签到: <span class="count-num">{{ monthlyCount }}</span> 天
        </span>
      </div>
    </div>

    <div class="weekday-grid">
      <span v-for="day in ['日', '一', '二', '三', '四', '五', '六']" :key="day">{{ day }}</span>
    </div>

    <div class="calendar-grid" :class="{ 'is-loading': loading }">
      <div v-for="empty in firstDayOffset" :key="'empty-' + empty" class="day-cell empty"></div>
      
      <div 
        v-for="date in daysInMonth" 
        :key="date" 
        class="day-cell"
        :class="[getSignStatus(date), { 'is-today': checkIsToday(date) }]"
      >
        <span class="day-num">{{ date }}</span>
        <div class="status-dot"></div>
      </div>
    </div>

    <div class="calendar-footer">
      <div class="legend"><span class="dot green"></span> 正常签到</div>
      <div class="legend"><span class="dot blue"></span> 补签记录</div>
      <div class="legend"><span class="dot gray"></span> 未签到</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { signApi, type SignData } from '../api/sign'
import { useExpNotify } from '../composables/useExpNotify'
import { useUserStore } from '../stores/user' // 确保路径指向你实际的 store 文件


const userStore = useUserStore()
const { notify } = useExpNotify()

// 1. 响应式数据
const signData = ref<SignData>({})
const loading = ref(false)

// 2. 当前查看的年月逻辑
const now = new Date()
const viewYear = ref(now.getFullYear())
const viewMonth = ref(now.getMonth())

// 3. 计算属性：日历逻辑
const daysInMonth = computed(() => new Date(viewYear.value, viewMonth.value + 1, 0).getDate())
const firstDayOffset = computed(() => new Date(viewYear.value, viewMonth.value, 1).getDay())

// 判断是否为当前月（用于控制签到按钮显示）
const isCurrentMonth = computed(() => {
  return viewYear.value === now.getFullYear() && viewMonth.value === now.getMonth()
})

// 判断今天是否已经签到
const isTodaySigned = computed(() => {
  const todayStr = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
  return signData.value[todayStr] !== undefined
})

// 4. API 请求：获取数据
const fetchSignData = async () => {
  loading.value = true
  try {
    const data = await signApi.getMonthData(viewYear.value, viewMonth.value + 1)
    signData.value = data
  } catch (error) {
    console.error("获取签到数据失败:", error)
  } finally {
    loading.value = false
  }
}

// 5. API 请求：执行签到
// 5. API 请求：执行签到
const handleSignIn = async () => {
  if (loading.value) return
  loading.value = true
  
  try {
    const res = await signApi.doSign()
    
    // 1. 修改点：字段名从 pointsAdded 改为 experienceAdded
    // 触发那个漂亮的修为漂浮提示
    notify(res.experienceAdded) 
    
    // 2. 优化点：同步更新本地 Store 的数据 (如果有 userStore)
    // 这样用户不用刷新页面，经验条和等级就能立刻跳动
    if (userStore.userInfo) {
      userStore.userInfo.experience += res.experienceAdded
      // 注意：等级(level)是后端计算的，如果怕前端算不准，
      // 也可以让后端在 doSign 结果里把最新的 level 也返回回来。
    }

    // 3. 刷新日历签到状态
    await fetchSignData()
    
  } catch (error: any) {
    // 这里的提示也可以优化，不再用原生的 alert
    const errorMsg = error.response?.data?.message || "由于灵力波动，打卡失败"
    console.error("签到异常:", error)
    // 如果你有通用的消息组件，可以用 ElMessage.error(errorMsg)
    alert(errorMsg) 
  } finally {
    loading.value = false
  }
}

// 6. 生命周期与监听
onMounted(fetchSignData)
watch([viewYear, viewMonth], fetchSignData)

// 7. 辅助方法
const changeMonth = (delta: number) => {
  const newDate = new Date(viewYear.value, viewMonth.value + delta, 1)
  viewYear.value = newDate.getFullYear()
  viewMonth.value = newDate.getMonth()
}

const getSignStatus = (date: number) => {
  const dateStr = `${viewYear.value}-${String(viewMonth.value + 1).padStart(2, '0')}-${String(date).padStart(2, '0')}`
  const status = signData.value[dateStr]
  if (status === 1) return 'status-normal'
  if (status === 2) return 'status-re-sign'
  return 'status-none'
}

const checkIsToday = (date: number) => {
  return isCurrentMonth.value && date === now.getDate()
}

const monthlyCount = computed(() => {
  const monthPrefix = `${viewYear.value}-${String(viewMonth.value + 1).padStart(2, '0')}`
  return Object.keys(signData.value).filter(key => key.startsWith(monthPrefix)).length
})
</script>

<style scoped>
.sign-container {
  background: #ffffff;
  border: 1px solid #f0f0f0;
  border-radius: 12px;
  padding: 24px;
  width: 100%;
  user-select: none;
}

/* 头部样式 */
.calendar-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
  flex-wrap: wrap;
  gap: 16px;
}

.month-display {
  font-size: 1.1rem;
  font-weight: 700;
  margin: 0 15px;
  color: #1f2328;
}

.nav-btn {
  border: 1px solid #d0d7de;
  background: #fff;
  border-radius: 6px;
  padding: 4px 10px;
  cursor: pointer;
  transition: all 0.2s;
}

.nav-btn:hover:not(:disabled) { background: #f6f8fa; border-color: #0969da; }
.nav-btn:disabled { opacity: 0.5; cursor: not-allowed; }

.sign-stats {
  display: flex;
  align-items: center;
}

/* 立即打卡按钮 */
.sign-action-btn {
  background: #24292f;
  color: #ffffff;
  border: none;
  padding: 8px 16px;
  border-radius: 6px;
  font-weight: 600;
  margin-right: 16px;
  cursor: pointer;
  transition: all 0.2s;
}

.sign-action-btn:hover:not(:disabled) {
  background: #0969da;
  transform: translateY(-1px);
}

.sign-action-btn:disabled {
  background: #f6f8fa;
  color: #8c959f;
  border: 1px solid #d0d7de;
  cursor: not-allowed;
}

.stats-label {
  font-size: 0.9rem;
  color: #57606a;
}

.count-num { color: #0969da; font-weight: 800; font-family: monospace; font-size: 1.1rem; }

/* 网格布局 */
.weekday-grid, .calendar-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  text-align: center;
}

.weekday-grid {
  font-size: 0.85rem;
  color: #57606a;
  margin-bottom: 12px;
  font-weight: 600;
}

.calendar-grid {
  transition: opacity 0.3s;
}

.calendar-grid.is-loading {
  opacity: 0.5;
  pointer-events: none;
}

.day-cell {
  aspect-ratio: 1 / 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  position: relative;
  border-radius: 8px;
  margin: 2px;
}

.day-num { font-size: 0.95rem; font-weight: 500; z-index: 1; }

/* 今日高亮标记 */
.is-today {
  outline: 2px solid #0969da;
  outline-offset: -2px;
}

/* 状态小圆点 */
.status-dot {
  width: 4px;
  height: 4px;
  border-radius: 50%;
  margin-top: 4px;
  background: transparent;
}

/* 状态颜色适配 */
.status-normal { background: #dafbe1; color: #1a7f37; }
.status-normal .status-dot { background: #2da44e; }

.status-re-sign { background: #ddf4ff; color: #0969da; }
.status-re-sign .status-dot { background: #0969da; }

.status-none { color: #57606a; }
.status-none .status-dot { background: #d0d7de; }

/* 底部图例 */
.calendar-footer {
  display: flex;
  gap: 20px;
  margin-top: 24px;
  padding-top: 16px;
  border-top: 1px solid #f0f0f0;
  font-size: 0.85rem;
  color: #57606a;
}

.legend { display: flex; align-items: center; gap: 6px; }
.dot { width: 8px; height: 8px; border-radius: 50%; }
.dot.green { background: #2da44e; }
.dot.blue { background: #0969da; }
.dot.gray { background: #d0d7de; }

/* 手机适配 */
@media (max-width: 768px) {
  .sign-container { padding: 16px; }
  .calendar-header { flex-direction: column; align-items: stretch; }
  .current-month { display: flex; justify-content: center; }
  .sign-stats { flex-direction: column; gap: 12px; }
  .sign-action-btn { width: 100%; margin-right: 0; }
  .day-num { font-size: 0.8rem; }
}
</style>