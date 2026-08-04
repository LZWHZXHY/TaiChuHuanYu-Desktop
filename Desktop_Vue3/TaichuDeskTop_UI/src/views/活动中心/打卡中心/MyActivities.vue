<template>
  <div class="my-activities">
    <div class="page-header">
      <h2><i class="fas fa-user-circle"></i> 我的活动</h2>
      <span class="subtitle">共 {{ myActivities.length }} 个活动</span>
    </div>

    <!-- 加载状态 -->
    <div v-if="loading" class="loading-container">
      <i class="fas fa-spinner fa-spin"></i> 加载中...
    </div>

    <!-- 活动列表 -->
    <div v-else-if="myActivities.length > 0" class="activity-grid">
      <div
        v-for="act in myActivities"
        :key="act.id"
        class="card"
        @click="goToDetail(act.id)"
      >
        <div class="card-cover" :style="{ backgroundImage: `url(${act.cover})` }">
          <span class="type-tag"><i class="fas fa-tag"></i> {{ act.type }}</span>
          <span class="status-badge" :class="statusClass(act.status)">
            {{ act.status }}
          </span>
        </div>
        <div class="card-body">
          <div class="card-meta">
            <span><i class="far fa-calendar-alt"></i> {{ act.cycle }}</span>
            <span><i class="fas fa-user"></i> {{ act.participants }} 人</span>
          </div>
          <h3>{{ act.title }}</h3>
          <p class="desc">{{ act.desc }}</p>
          <div class="progress-section">
            <div class="progress-bar">
              <div
                class="progress-fill"
                :style="{ width: act.progress + '%' }"
              ></div>
            </div>
            <span class="progress-text">
              {{ act.completedDays }} / {{ act.days }} 天
            </span>
          </div>
          <p class="author"><i class="fas fa-user-circle"></i> 发起人: {{ act.owner }}</p>
        </div>
        <div class="card-footer">
          <span class="participant-count">
            <i class="fas fa-check-circle" :style="{ color: act.progress > 0 ? '#10b981' : '#9ca3af' }"></i>
            {{ act.progress }}% 已完成
          </span>
          <button class="action-btn" @click.stop="goToDetail(act.id)">
            查看详情 <i class="fas fa-arrow-right"></i>
          </button>
        </div>
      </div>
    </div>

    <!-- 空状态 -->
    <div v-else class="empty-state">
      <i class="fas fa-calendar-plus"></i>
      <p>你还没有参与任何活动</p>
      <button class="btn-primary" @click="$router.push('/activity')">
        去发现广场 <i class="fas fa-arrow-right"></i>
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import request from '@/utils/request';
import { useUserStore } from '@/stores/user';

const router = useRouter();
const userStore = useUserStore();

const loading = ref(true);
const myActivities = ref<any[]>([]);

// 状态样式
const statusClass = (status: string) => ({
  'recruiting': status === '招募中',
  'ongoing': status === '进行中',
  'ended': status === '已结束',
});

// 跳转到详情
const goToDetail = (id: number) => {
  router.push(`/activity/detail/${id}`);
};

// 加载我的活动
const loadMyActivities = async () => {
  loading.value = true;
  try {
    // 1. 获取所有活动
    const allActivities = await request.get('/activities');
    if (!allActivities || allActivities.length === 0) {
      myActivities.value = [];
      return;
    }

    // 2. 并行获取每个活动的 my-status（判断是否加入并获取个人进度）
    const statusPromises = allActivities.map((act: any) =>
      request.get(`/activities/${act.id}/my-status`).catch(() => null)
    );
    const statusResults = await Promise.all(statusPromises);

    // 3. 筛选已加入的活动，并合并个人进度数据
    const merged = allActivities
      .map((act: any, index: number) => {
        const status = statusResults[index];
        if (status && status.isJoined) {
          return {
            id: act.id,
            title: act.title,
            desc: act.description || '',
            type: act.type,
            status: act.status,
            cover: act.cover || '',
            days: act.days,
            cycle: act.cycle || `${act.days}天`,
            participants: act.participants || 0,
            owner: act.owner || '',
            completedDays: status.completedDays || 0,
            progress: act.days > 0 ? Math.round((status.completedDays || 0) / act.days * 100) : 0,
          };
        }
        return null;
      })
      .filter(Boolean);

    myActivities.value = merged;
  } catch (error) {
    console.error('加载我的活动失败:', error);
    myActivities.value = [];
  } finally {
    loading.value = false;
  }
};

onMounted(() => {
  loadMyActivities();
});
</script>

<style scoped>
/* 样式与之前一致（保持不变，直接复用原样式） */
.my-activities {
  max-width: 1200px;
  margin: 0 auto;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 28px;
}
.page-header h2 {
  font-size: 1.4rem;
  font-weight: 600;
  color: #1f2937;
  display: flex;
  align-items: center;
  gap: 10px;
}
.page-header h2 i {
  color: #6366f1;
}
.page-header .subtitle {
  font-size: 0.9rem;
  color: #9ca3af;
}

.activity-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 24px;
}
.card {
  background: #fff;
  border-radius: 12px;
  overflow: hidden;
  border: 1px solid #eee;
  transition: transform 0.2s, border-color 0.2s;
  cursor: pointer;
}
.card:hover {
  transform: translateY(-3px);
  border-color: #d1d5db;
}
.card-cover {
  height: 140px;
  background-size: cover;
  background-position: center;
  position: relative;
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  padding: 10px 14px;
}
.type-tag {
  background: rgba(0,0,0,0.5);
  backdrop-filter: blur(2px);
  color: #fff;
  padding: 2px 10px;
  border-radius: 20px;
  font-size: 0.6rem;
  font-weight: 500;
}
.status-badge {
  padding: 2px 10px;
  border-radius: 20px;
  font-size: 0.6rem;
  font-weight: 500;
  color: #fff;
}
.status-badge.recruiting { background: #10b981; }
.status-badge.ongoing { background: #6366f1; }
.status-badge.ended { background: #9ca3af; }

.card-body { padding: 16px 18px 12px; }
.card-meta {
  display: flex;
  gap: 12px;
  font-size: 0.65rem;
  color: #9ca3af;
  margin-bottom: 6px;
}
.card-meta span { display: flex; align-items: center; gap: 4px; }
.card-body h3 {
  font-size: 1rem;
  font-weight: 600;
  margin: 0 0 4px;
  color: #1f2937;
}
.card-body .desc {
  font-size: 0.8rem;
  color: #6b7280;
  line-height: 1.4;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin-bottom: 8px;
}
.card-body .author {
  font-size: 0.7rem;
  color: #9ca3af;
  display: flex;
  align-items: center;
  gap: 4px;
  margin-top: 4px;
}

.progress-section {
  display: flex;
  align-items: center;
  gap: 12px;
  margin: 4px 0 6px;
}
.progress-bar {
  flex: 1;
  height: 6px;
  background: #e5e7eb;
  border-radius: 3px;
  overflow: hidden;
}
.progress-fill {
  height: 100%;
  background: #6366f1;
  border-radius: 3px;
  transition: width 0.3s;
}
.progress-text {
  font-size: 0.7rem;
  color: #6b7280;
  white-space: nowrap;
}

.card-footer {
  padding: 10px 18px 16px;
  border-top: 1px solid #f3f4f6;
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.card-footer .action-btn {
  padding: 6px 14px;
  border-radius: 20px;
  border: none;
  background: #f3f4f6;
  font-weight: 500;
  font-size: 0.7rem;
  color: #374151;
  cursor: pointer;
  transition: background 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}
.card-footer .action-btn:hover { background: #e5e7eb; }
.card-footer .participant-count {
  font-size: 0.7rem;
  color: #9ca3af;
}

.empty-state {
  text-align: center;
  padding: 80px 20px;
  color: #9ca3af;
}
.empty-state i {
  font-size: 3.5rem;
  color: #d1d5db;
  display: block;
  margin-bottom: 16px;
}
.empty-state p {
  font-size: 1rem;
  margin-bottom: 20px;
}
.btn-primary {
  background: #1f2937;
  color: #fff;
  border: none;
  padding: 10px 24px;
  border-radius: 30px;
  font-weight: 500;
  font-size: 0.9rem;
  cursor: pointer;
  transition: background 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 8px;
}
.btn-primary:hover { background: #374151; }

@media (max-width: 768px) {
  .activity-grid { grid-template-columns: 1fr; }
}
</style>