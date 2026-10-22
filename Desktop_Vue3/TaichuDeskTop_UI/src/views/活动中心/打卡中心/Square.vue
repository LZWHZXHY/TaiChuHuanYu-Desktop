<template>
  <div class="square-container">
    <!-- 加载状态 -->
    <div v-if="loading" class="loading-state">
      <i class="fas fa-spinner fa-spin"></i> 加载中...
    </div>

    <div v-else-if="loadError" class="empty-state" role="alert">{{ loadError }} <button class="btn-primary" @click="loadActivities">重新加载</button></div>
    <!-- 正常内容 -->
    <template v-else>
      <div class="square-header">
        <h2><i class="fas fa-fire"></i> 热门挑战</h2>
        <div class="square-actions">
          <div class="search-box">
            <i class="fas fa-search"></i>
            <input type="text" placeholder="搜索活动..." v-model="searchKeyword" />
          </div>
          <button class="btn-primary" @click="$router.push('/activity/checkin/create')">
            <i class="fas fa-plus"></i> 发起活动
          </button>
        </div>
      </div>

      <div class="filter-tabs">
        <button v-for="tab in filterOptions" :key="tab.value"
                class="filter-tab" :class="{ active: currentFilter === tab.value }"
                @click="setFilter(tab.value)">
          {{ tab.label }}
        </button>
      </div>

      <div class="activity-grid">
        <div class="card" v-for="act in filteredActivities" :key="act.id" @click="goToDetail(act.id)">
          <div class="card-cover" :style="{ backgroundImage: `url(${act.cover || defaultCover})` }">
            <span class="type-tag"><i class="fas fa-tag"></i> {{ act.type }}</span>
            <span class="status-badge" :class="statusClass(act.status)">{{ act.status }}</span>
          </div>
          <div class="card-body">
            <div class="card-meta">
              <span><i class="far fa-calendar-alt"></i> {{ act.cycle }}</span>
              <span><i class="fas fa-user"></i> {{ act.participants }} 人</span>
            </div>
            <h3>{{ act.title }}</h3>
            <p class="desc">{{ act.desc || '暂无描述' }}</p>
            <p class="author"><i class="fas fa-user-circle"></i> 发起人: {{ act.owner }}</p>
          </div>
          <div class="card-footer">
            <span class="participant-count">
              <i class="fas fa-check-circle" :style="{ color: act.completedRate > 0 ? '#10b981' : '#8e99ab' }"></i>
              {{ act.completedRate }}% 已打卡
            </span>
            <button class="action-btn" @click.stop="goToDetail(act.id)">查看详情 <i class="fas fa-arrow-right"></i></button>
          </div>
        </div>
        <div v-if="filteredActivities.length === 0" class="empty-state">
          <i class="fas fa-search"></i>
          <p>没有找到匹配的活动</p>
        </div>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { getActivities, type Activity } from './storage';

const router = useRouter();

// ===== 状态 =====
const activities = ref<Activity[]>([]);
const loading = ref(true);
const loadError = ref('');
const searchKeyword = ref('');
const currentFilter = ref('全部');

// ===== 默认封面图（当活动没有封面时使用） =====
const defaultCover = 'https://images.unsplash.com/photo-1516116216624-53e697fedbea?w=600&h=400&fit=crop';

// ===== 筛选选项 =====
const filterOptions = [
  { label: '全部', value: '全部' },
  { label: '进行中', value: '进行中' },
  { label: '招募中', value: '招募中' },
  { label: '已结束', value: '已结束' },
];

// ===== 从后端加载数据 =====
const loadActivities = async () => {
  loading.value = true; loadError.value = '';
  try {
    const data = await getActivities();
    activities.value = data;
  } catch (error) {
    console.error('加载活动失败:', error);
    loadError.value = '活动读取失败，请重试。';
  } finally {
    loading.value = false;
  }
};

// ===== 计算属性：筛选后的活动列表 =====
const filteredActivities = computed(() => {
  let list = activities.value;

  // 按状态筛选
  if (currentFilter.value !== '全部') {
    list = list.filter(item => item.status === currentFilter.value);
  }

  // 按关键词搜索
  if (searchKeyword.value.trim()) {
    const keyword = searchKeyword.value.trim().toLowerCase();
    list = list.filter(item =>
      item.title.toLowerCase().includes(keyword) ||
      (item.desc && item.desc.toLowerCase().includes(keyword))
    );
  }

  return list;
});

// ===== 方法 =====
const setFilter = (value: string) => {
  currentFilter.value = value;
};


const goToDetail = (id: number) => {
  router.push(`/activity/checkin/detail/${id}`);
};

const statusClass = (status: string) => ({
  'recruiting': status === '招募中',
  'ongoing': status === '进行中',
  'ended': status === '已结束',
});

// ===== 生命周期 =====
onMounted(() => {
  loadActivities();
});
</script>

<style scoped>
/* 原有样式保持不变，新增 loading 样式 */
.square-container { max-width: 1200px; margin: 0 auto; }

.loading-state {
  text-align: center;
  padding: 80px 20px;
  color: var(--text-secondary);
}

.loading-state i {
  font-size: 2rem;
  display: block;
  margin-bottom: 12px;
}

.square-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 16px;
  margin-bottom: 28px;
}

.square-header h2 {
  font-size: 1.6rem;
  font-weight: 700;
  letter-spacing: -0.3px;
}

.square-header h2 i {
  color: var(--info);
  margin-right: 10px;
}

.square-actions {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
}

.search-box {
  display: flex;
  align-items: center;
  background: var(--paper-card);
  border: 1px solid var(--border-line);
  border-radius: 40px;
  padding: 0 16px;
  transition: border 0.2s;
}

.search-box:focus-within {
  border-color: var(--info);
  box-shadow: 0 0 0 3px rgba(99, 102, 241, 0.08);
}

.search-box i {
  color: var(--text-secondary);
  font-size: 0.9rem;
}

.search-box input {
  border: none;
  outline: none;
  padding: 10px 12px;
  font-size: 0.9rem;
  background: transparent;
  min-width: 180px;
  color: var(--text-primary);
}

.search-box input::placeholder {
  color: var(--text-secondary);
}

.btn-primary {
  background: var(--bg-sub);
  color: var(--text-primary);
  border: none;
  padding: 10px 22px;
  border-radius: 40px;
  font-weight: 600;
  font-size: 0.9rem;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  transition: all 0.25s ease;
  box-shadow: 0 2px 8px rgba(24, 24, 27, 0.10);
}

.btn-primary:hover {
  background: var(--bg-sub);
  transform: translateY(-2px);
  box-shadow: 0 8px 20px rgba(24, 24, 27, 0.15);
}

.filter-tabs {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
  margin-bottom: 28px;
  padding-bottom: 4px;
  border-bottom: 1px solid var(--border-line);
}

.filter-tab {
  padding: 8px 18px;
  border-radius: 30px;
  border: none;
  background: transparent;
  font-size: 0.85rem;
  font-weight: 500;
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 0.2s;
}

.filter-tab:hover {
  background: var(--bg-main);
  color: var(--text-primary);
}

.filter-tab.active {
  background: var(--bg-sub);
  color: var(--text-primary);
  box-shadow: 0 2px 8px rgba(24, 24, 27, 0.10);
}

.activity-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 28px;
}

.card {
  background: var(--paper-card);
  border-radius: 20px;
  overflow: hidden;
  border: 1px solid var(--border-line);
  transition: all 0.3s cubic-bezier(0.2, 0, 0, 1);
  cursor: pointer;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.02);
}

.card:hover {
  transform: translateY(-6px);
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.06), 0 6px 14px rgba(0, 0, 0, 0.02);
  border-color: var(--border-line);
}

.card-cover {
  height: 160px;
  background-size: cover;
  background-position: center;
  position: relative;
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  padding: 14px 16px;
}

.type-tag {
  background: var(--overlay);
  backdrop-filter: blur(4px);
  color: var(--on-overlay);
  padding: 4px 14px;
  border-radius: 30px;
  font-size: 0.7rem;
  font-weight: 500;
  border: 1px solid rgba(255, 255, 255, 0.10);
}

.status-badge {
  padding: 4px 14px;
  border-radius: 30px;
  font-size: 0.7rem;
  font-weight: 500;
  color: var(--text-primary);
  border: 1px solid rgba(255, 255, 255, 0.10);
}

.status-badge.recruiting {
  background: var(--success);
}

.status-badge.ongoing {
  background: var(--info);
}

.status-badge.ended {
  background: var(--bg-sub);
}

.card-body {
  padding: 20px 22px 16px;
}

.card-meta {
  display: flex;
  gap: 18px;
  font-size: 0.75rem;
  color: var(--text-secondary);
  margin-bottom: 10px;
}

.card-meta span {
  display: flex;
  align-items: center;
  gap: 5px;
}

.card-body h3 {
  font-size: 1.15rem;
  font-weight: 700;
  margin: 0 0 6px 0;
  letter-spacing: -0.2px;
  color: var(--text-primary);
}

.card-body .desc {
  font-size: 0.85rem;
  color: var(--text-secondary);
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin-bottom: 8px;
}

.card-body .author {
  font-size: 0.8rem;
  color: var(--text-secondary);
  display: flex;
  align-items: center;
  gap: 6px;
}

.card-footer {
  padding: 12px 22px 20px;
  border-top: 1px solid var(--border-line);
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.card-footer .action-btn {
  padding: 8px 20px;
  border-radius: 30px;
  border: none;
  background: var(--bg-main);
  font-weight: 500;
  font-size: 0.8rem;
  color: var(--text-primary);
  cursor: pointer;
  transition: all 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.card-footer .action-btn:hover {
  background: var(--bg-sub);
  color: var(--text-primary);
}

.card-footer .participant-count {
  font-size: 0.75rem;
  color: var(--text-secondary);
}

.empty-state {
  grid-column: 1 / -1;
  text-align: center;
  padding: 80px 20px;
  color: var(--text-secondary);
}

.empty-state i {
  font-size: 3rem;
  color: var(--text-secondary);
  margin-bottom: 16px;
  display: block;
}

@media (max-width: 768px) {
  .square-header {
    flex-direction: column;
    align-items: stretch;
  }

  .square-actions {
    flex-direction: column;
    align-items: stretch;
  }

  .search-box input {
    min-width: auto;
    width: 100%;
  }

  .activity-grid {
    grid-template-columns: 1fr;
  }
}
</style>
