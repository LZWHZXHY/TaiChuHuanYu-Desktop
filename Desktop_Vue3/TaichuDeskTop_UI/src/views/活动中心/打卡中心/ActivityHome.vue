<template>
  <div class="activity-home">
    <!-- 顶部：搜索与筛选 -->
    <div class="square-header">
      <h2><i class="fas fa-fire"></i> 热门挑战</h2>
      <div class="square-actions">
        <div class="search-box">
          <i class="fas fa-search"></i>
          <input type="text" placeholder="搜索活动..." v-model="searchKeyword" @input="filterActivities" />
        </div>
        <button class="btn-primary" @click="$router.push('/activity/create')">
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

    <!-- 活动卡片网格 -->
    <div class="activity-grid">
      <div class="card" v-for="act in filteredActivities" :key="act.id"
           @click="selectActivity(act.id)"
           :class="{ 'selected': selectedId === act.id }">
        <div class="card-cover" :style="{ backgroundImage: `url(${act.cover})` }">
          <span class="type-tag"><i class="fas fa-tag"></i> {{ act.type }}</span>
          <span class="status-badge" :class="statusClass(act.status)">{{ act.status }}</span>
        </div>
        <div class="card-body">
          <div class="card-meta">
            <span><i class="far fa-calendar-alt"></i> {{ act.cycle }}</span>
            <span><i class="fas fa-user"></i> {{ act.participants }} 人</span>
          </div>
          <h3>{{ act.title }}</h3>
          <p class="desc">{{ act.desc }}</p>
          <p class="author"><i class="fas fa-user-circle"></i> 发起人: {{ act.owner }}</p>
        </div>
        <div class="card-footer">
          <span class="participant-count">
            <i class="fas fa-check-circle" :style="{ color: act.completedRate > 0 ? '#10b981' : '#8e99ab' }"></i>
            {{ act.completedRate }}% 已打卡
          </span>
          <button class="action-btn" @click.stop="selectActivity(act.id)">查看详情 <i class="fas fa-arrow-right"></i></button>
        </div>
      </div>
      <div v-if="filteredActivities.length === 0" class="empty-state">
        <i class="fas fa-search"></i>
        <p>没有找到匹配的活动</p>
      </div>
    </div>

    <!-- 活动详情区域（点击卡片后展开） -->
    <div v-if="selectedActivity" class="detail-wrapper">
      <div class="detail-divider">
        <span><i class="fas fa-chevron-down"></i> 活动详情</span>
        <button class="close-detail" @click="selectedActivity = null"><i class="fas fa-times"></i></button>
      </div>
      <!-- 引用详情组件，通过 props 传入活动数据 -->
      <ActivityDetail :activity="selectedActivity" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import ActivityDetail from './ActivityDetail.vue';
import { getActivities, type Activity } from './storage';


const router = useRouter();
const activities = ref<Activity[]>([]);
const loading = ref(false);

onMounted(async () => {
  loading.value = true;
  try {
    activities.value = await getActivities();
  } catch (error) {
    console.error('加载活动失败:', error);
    // 可显示错误提示
  } finally {
    loading.value = false;
  }
});

const searchKeyword = ref('');
const currentFilter = ref('全部');
const filterOptions = [
  { label: '全部', value: '全部' },
  { label: '进行中', value: '进行中' },
  { label: '招募中', value: '招募中' },
  { label: '已结束', value: '已结束' },
];

const filteredActivities = computed(() => {
  let list = activities.value;
  if (currentFilter.value !== '全部') {
    list = list.filter(item => item.status === currentFilter.value);
  }
  if (searchKeyword.value.trim()) {
    const keyword = searchKeyword.value.trim().toLowerCase();
    list = list.filter(item =>
      item.title.toLowerCase().includes(keyword) ||
      item.desc.toLowerCase().includes(keyword)
    );
  }
  return list;
});

// 选中的活动 ID
const selectedId = ref<number | null>(null);
// 选中的活动对象（用于传给 Detail 组件）
const selectedActivity = ref<any>(null);

const selectActivity = (id: number) => {
  const act = activities.value.find(a => a.id === id);
  if (act) {
    selectedId.value = id;
    selectedActivity.value = act;
  }
};

const setFilter = (value: string) => { currentFilter.value = value; };
const filterActivities = () => {};
const statusClass = (status: string) => ({
  'recruiting': status === '招募中',
  'ongoing': status === '进行中',
  'ended': status === '已结束',
});
</script>

<style scoped>
.activity-home { max-width: 1200px; margin: 0 auto; }

/* 头部 */
.square-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 14px;
  margin-bottom: 24px;
}
.square-header h2 {
  font-size: 1.4rem;
  font-weight: 600;
  letter-spacing: -0.2px;
  color: var(--text-primary);
}
.square-header h2 i { color: var(--info); margin-right: 6px; }
.square-actions { display: flex; gap: 10px; align-items: center; flex-wrap: wrap; }
.search-box {
  display: flex;
  align-items: center;
  background: var(--paper-card);
  border: 1px solid var(--border-line);
  border-radius: 0;
  padding: 0 14px;
  transition: border 0.2s;
}
.search-box:focus-within { border-color: var(--info); }
.search-box i { color: var(--text-secondary); font-size: 0.8rem; }
.search-box input {
  border: none;
  outline: none;
  padding: 8px 10px;
  font-size: 0.85rem;
  background: transparent;
  min-width: 140px;
  color: var(--text-primary);
}
.search-box input::placeholder { color: var(--text-secondary); }

.btn-primary {
  background: var(--bg-sub);
  color: var(--text-primary);
  border: none;
  padding: 8px 18px;
  border-radius: 0;
  font-weight: 500;
  font-size: 0.85rem;
  cursor: pointer;
  transition: background 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.btn-primary:hover { background: var(--bg-sub); }

/* 筛选标签 */
.filter-tabs {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
  margin-bottom: 20px;
  border-bottom: 1px solid var(--border-line);
  padding-bottom: 8px;
}
.filter-tab {
  padding: 6px 14px;
  border-radius: 0;
  border: none;
  background: transparent;
  font-size: 0.8rem;
  font-weight: 500;
  color: var(--text-secondary);
  cursor: pointer;
  transition: background 0.2s, color 0.2s;
}
.filter-tab:hover { background: var(--bg-sub); color: var(--text-primary); }
.filter-tab.active {
  background: var(--bg-sub);
  color: var(--text-primary);
}

/* 卡片网格 */
.activity-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 24px;
}
.card {
  background: var(--paper-card);
  border-radius: 0;
  overflow: hidden;
  border: 1px solid var(--border-line);
  transition: transform 0.2s, border-color 0.2s;
  cursor: pointer;
}
.card:hover { transform: translateY(-3px); border-color: var(--border-line); }
.card.selected { border-color: var(--info); }

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
  background: var(--overlay);
  backdrop-filter: blur(2px);
  color: var(--on-overlay);
  padding: 2px 10px;
  border-radius: 0;
  font-size: 0.6rem;
  font-weight: 500;
}
.status-badge {
  padding: 2px 10px;
  border-radius: 0;
  font-size: 0.6rem;
  font-weight: 500;
  color: var(--text-primary);
}
.status-badge.recruiting { background: var(--success); }
.status-badge.ongoing { background: var(--info); }
.status-badge.ended { background: var(--bg-sub); }

.card-body { padding: 16px 18px 12px; }
.card-meta {
  display: flex;
  gap: 12px;
  font-size: 0.65rem;
  color: var(--text-secondary);
  margin-bottom: 6px;
}
.card-meta span { display: flex; align-items: center; gap: 4px; }
.card-body h3 {
  font-size: 1rem;
  font-weight: 600;
  margin: 0 0 4px;
  color: var(--text-primary);
}
.card-body .desc {
  font-size: 0.8rem;
  color: var(--text-secondary);
  line-height: 1.4;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin-bottom: 6px;
}
.card-body .author {
  font-size: 0.7rem;
  color: var(--text-secondary);
  display: flex;
  align-items: center;
  gap: 4px;
}

.card-footer {
  padding: 10px 18px 16px;
  border-top: 1px solid var(--border-line);
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.card-footer .action-btn {
  padding: 6px 14px;
  border-radius: 0;
  border: none;
  background: var(--bg-sub);
  font-weight: 500;
  font-size: 0.7rem;
  color: var(--text-primary);
  cursor: pointer;
  transition: background 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}
.card-footer .action-btn:hover { background: var(--bg-sub); }
.card-footer .participant-count {
  font-size: 0.7rem;
  color: var(--text-secondary);
}

.empty-state {
  grid-column: 1 / -1;
  text-align: center;
  padding: 60px 20px;
  color: var(--text-secondary);
}
.empty-state i { font-size: 2.5rem; color: var(--text-secondary); margin-bottom: 12px; display: block; }

/* 详情展开区域 */
.detail-wrapper {
  margin-top: 40px;
  padding-top: 20px;
  border-top: 1px solid var(--border-line);
}
.detail-divider {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
  font-weight: 500;
  color: var(--text-primary);
  font-size: 0.9rem;
}
.detail-divider span i { margin-right: 6px; color: var(--info); }
.close-detail {
  background: none;
  border: none;
  font-size: 1rem;
  color: var(--text-secondary);
  cursor: pointer;
  padding: 2px 6px;
  border-radius: 0;
  transition: 0.2s;
}
.close-detail:hover { background: var(--bg-sub); color: var(--text-primary); }

@media (max-width: 768px) {
  .square-header { flex-direction: column; align-items: stretch; }
  .square-actions { flex-direction: column; align-items: stretch; }
  .search-box input { min-width: auto; width: 100%; }
  .activity-grid { grid-template-columns: 1fr; }
}
</style>
