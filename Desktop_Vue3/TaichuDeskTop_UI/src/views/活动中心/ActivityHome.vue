<template>
  <div class="zone-home">
    <div class="zone-header">
      <h1><i class="fas fa-cubes"></i> 互动中心</h1>
      <p class="subtitle">选择你想参与的互动专区</p>
    </div>

    <div class="zone-grid">
      <!-- ===== 打卡专区 ===== -->
      <div class="zone-card" @click="$router.push('/activity/checkin')">
        <div class="zone-icon" style="background: linear-gradient(135deg, #6366f1, #8b5cf6);">
          <i class="fas fa-calendar-check"></i>
        </div>
        <div class="zone-info">
          <h3>🔥 打卡专区</h3>
          <p>参与打卡挑战，养成好习惯</p>
          <span class="badge">进行中 {{ checkinCount }} 个</span>
        </div>
        <div class="zone-arrow">
          <i class="fas fa-arrow-right"></i>
        </div>
      </div>

      <!-- ===== 问卷专区（原投票专区） ===== -->
      <div class="zone-card" @click="$router.push('/activity/survey')">
        <div class="zone-icon" style="background: linear-gradient(135deg, #f59e0b, #f97316);">
          <i class="fas fa-clipboard-list"></i>
        </div>
        <div class="zone-info">
          <h3>📋 问卷专区</h3>
          <p>参与问卷调查，表达你的观点</p>
          <span class="badge">进行中 {{ surveyCount }} 个</span>
        </div>
        <div class="zone-arrow">
          <i class="fas fa-arrow-right"></i>
        </div>
      </div>

      <!-- ===== 预留扩展 ===== -->
      <div class="zone-card disabled">
        <div class="zone-icon" style="background: linear-gradient(135deg, #9ca3af, #6b7280);">
          <i class="fas fa-ellipsis-h"></i>
        </div>
        <div class="zone-info">
          <h3>🚀 更多专区</h3>
          <p>即将上线，敬请期待...</p>
        </div>
        <div class="zone-arrow">
          <i class="fas fa-clock"></i>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import request from '@/utils/request'

const checkinCount = ref(0)
const surveyCount = ref(0)

onMounted(async () => {
  // 获取进行中的打卡活动数量
  try {
    const res = await request.get('/activities?status=进行中')
    checkinCount.value = res?.length || 0
  } catch (e) {
    console.error('获取打卡活动数量失败:', e)
  }
  
  // 获取发布中的问卷数量（状态为 1）
  try {
    const res = await request.get('/survey/list?status=1')
    // 根据返回的数据结构判断是数组还是分页对象
    if (Array.isArray(res)) {
      surveyCount.value = res.length || 0
    } else if (res?.data && Array.isArray(res.data)) {
      surveyCount.value = res.data.length || 0
    } else {
      surveyCount.value = 0
    }
  } catch (e) {
    console.error('获取问卷数量失败:', e)
    // 静默失败，不影响页面展示
    surveyCount.value = 0
  }
})
</script>

<style scoped>
.zone-home {
  max-width: 900px;
  margin: 0 auto;
  padding: 20px 0;
}

.zone-header {
  text-align: center;
  margin-bottom: 48px;
}

.zone-header h1 {
  font-size: 2rem;
  font-weight: 700;
  color: #1f2937;
}

.zone-header h1 i {
  color: #6366f1;
  margin-right: 10px;
}

.zone-header .subtitle {
  color: #9ca3af;
  font-size: 1rem;
  margin-top: 4px;
}

.zone-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 24px;
}

.zone-card {
  background: #fff;
  border-radius: 16px;
  padding: 28px 24px;
  border: 1px solid #f0f0f0;
  display: flex;
  align-items: center;
  gap: 18px;
  cursor: pointer;
  transition: all 0.3s ease;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
}

.zone-card:hover:not(.disabled) {
  transform: translateY(-4px);
  box-shadow: 0 12px 30px rgba(0, 0, 0, 0.08);
  border-color: #e5e7eb;
}

.zone-card.disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.zone-icon {
  width: 56px;
  height: 56px;
  border-radius: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.zone-icon i {
  font-size: 1.6rem;
  color: #fff;
}

.zone-info {
  flex: 1;
}

.zone-info h3 {
  font-size: 1rem;
  font-weight: 600;
  margin: 0 0 2px;
  color: #1f2937;
}

.zone-info p {
  font-size: 0.85rem;
  color: #9ca3af;
  margin: 0 0 6px;
}

.zone-info .badge {
  font-size: 0.7rem;
  background: #f3f4f6;
  padding: 2px 12px;
  border-radius: 20px;
  color: #6b7280;
}

.zone-arrow {
  color: #d1d5db;
  font-size: 1.2rem;
  transition: 0.2s;
}

.zone-card:hover:not(.disabled) .zone-arrow {
  color: #6366f1;
  transform: translateX(4px);
}

@media (max-width: 768px) {
  .zone-home {
    padding: 16px;
  }
  
  .zone-header h1 {
    font-size: 1.6rem;
  }
  
  .zone-grid {
    grid-template-columns: 1fr;
  }
}
</style>