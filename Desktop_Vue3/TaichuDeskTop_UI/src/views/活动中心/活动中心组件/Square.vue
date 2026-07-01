<template>
  <div class="square-container">
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

    <div class="activity-grid">
      <div class="card" v-for="act in filteredActivities" :key="act.id" @click="goToDetail(act.id)">
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
          <button class="action-btn" @click.stop="goToDetail(act.id)">查看详情 <i class="fas fa-arrow-right"></i></button>
        </div>
      </div>
      <div v-if="filteredActivities.length === 0" class="empty-state">
        <i class="fas fa-search"></i>
        <p>没有找到匹配的活动</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';
import { useRouter } from 'vue-router';

const router = useRouter();

const activities = ref([
  { id: 1, title: '每日算法通关', owner: 'Junjie', cycle: '30天', participants: 128, type: '技术精进', status: '进行中', desc: 'LeetCode 每日一题，截图打卡，30天养成刷题习惯。', cover: 'https://images.unsplash.com/photo-1516116216624-53e697fedbea?w=600&h=400&fit=crop', completedRate: 87 },
  { id: 2, title: 'DrinkWater 习惯养成', owner: 'System', cycle: '21天', participants: 356, type: '健康生活', status: '招募中', desc: '每天喝够8杯水，打卡记录，21天塑造健康习惯。', cover: 'https://images.unsplash.com/photo-1544275066-8367916960d9?w=600&h=400&fit=crop', completedRate: 62 },
  { id: 3, title: '每日阅读 30 分钟', owner: 'Alice', cycle: '30天', participants: 89, type: '阅读写作', status: '进行中', desc: '每天坚持阅读，分享读书笔记，与书友共同成长。', cover: 'https://images.unsplash.com/photo-1526374965328-7f61d4dc18c5?w=600&h=400&fit=crop', completedRate: 58 },
  { id: 4, title: '晨跑 2 公里挑战', owner: 'Bob', cycle: '14天', participants: 67, type: '运动健身', status: '已结束', desc: '清晨慢跑2公里，用汗水开启活力满满的一天。', cover: 'https://images.unsplash.com/photo-1517838277536-f5f99be501cd?w=600&h=400&fit=crop', completedRate: 100 },
]);

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

const setFilter = (value: string) => { currentFilter.value = value; };
const filterActivities = () => {};
const goToDetail = (id: number) => router.push(`/activity/detail/${id}`);
const statusClass = (status: string) => ({
  'recruiting': status === '招募中',
  'ongoing': status === '进行中',
  'ended': status === '已结束',
});
</script>

<style scoped>
/* Square 样式 */
.square-container { max-width: 1200px; margin: 0 auto; }
.square-header { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 16px; margin-bottom: 28px; }
.square-header h2 { font-size: 1.6rem; font-weight: 700; letter-spacing: -0.3px; }
.square-header h2 i { color: #6366f1; margin-right: 10px; }
.square-actions { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; }
.search-box { display: flex; align-items: center; background: #fff; border: 1px solid #e2e8f0; border-radius: 40px; padding: 0 16px; transition: border 0.2s; }
.search-box:focus-within { border-color: #6366f1; box-shadow: 0 0 0 3px rgba(99,102,241,0.08); }
.search-box i { color: #8e99ab; font-size: 0.9rem; }
.search-box input { border: none; outline: none; padding: 10px 12px; font-size: 0.9rem; background: transparent; min-width: 180px; color: #18181b; }
.search-box input::placeholder { color: #b0bac9; }
.btn-primary { background: #18181b; color: #fff; border: none; padding: 10px 22px; border-radius: 40px; font-weight: 600; font-size: 0.9rem; cursor: pointer; display: inline-flex; align-items: center; gap: 8px; transition: all 0.25s ease; box-shadow: 0 2px 8px rgba(24,24,27,0.10); }
.btn-primary:hover { background: #2d2d32; transform: translateY(-2px); box-shadow: 0 8px 20px rgba(24,24,27,0.15); }
.filter-tabs { display: flex; gap: 6px; flex-wrap: wrap; margin-bottom: 28px; padding-bottom: 4px; border-bottom: 1px solid #eef2f6; }
.filter-tab { padding: 8px 18px; border-radius: 30px; border: none; background: transparent; font-size: 0.85rem; font-weight: 500; color: #6b7a8e; cursor: pointer; transition: all 0.2s; }
.filter-tab:hover { background: #f1f4f9; color: #18181b; }
.filter-tab.active { background: #18181b; color: #fff; box-shadow: 0 2px 8px rgba(24,24,27,0.10); }
.activity-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 28px; }
.card { background: #fff; border-radius: 20px; overflow: hidden; border: 1px solid #eef2f6; transition: all 0.3s cubic-bezier(0.2, 0, 0, 1); cursor: pointer; box-shadow: 0 1px 3px rgba(0,0,0,0.02); }
.card:hover { transform: translateY(-6px); box-shadow: 0 20px 40px rgba(0,0,0,0.06), 0 6px 14px rgba(0,0,0,0.02); border-color: #d0d9e6; }
.card-cover { height: 160px; background-size: cover; background-position: center; position: relative; display: flex; align-items: flex-start; justify-content: space-between; padding: 14px 16px; }
.type-tag { background: rgba(0,0,0,0.55); backdrop-filter: blur(4px); color: #fff; padding: 4px 14px; border-radius: 30px; font-size: 0.7rem; font-weight: 500; border: 1px solid rgba(255,255,255,0.10); }
.status-badge { padding: 4px 14px; border-radius: 30px; font-size: 0.7rem; font-weight: 500; color: #fff; border: 1px solid rgba(255,255,255,0.10); }
.status-badge.recruiting { background: #10b981; }
.status-badge.ongoing { background: #6366f1; }
.status-badge.ended { background: #8e99ab; }
.card-body { padding: 20px 22px 16px; }
.card-meta { display: flex; gap: 18px; font-size: 0.75rem; color: #8e99ab; margin-bottom: 10px; }
.card-meta span { display: flex; align-items: center; gap: 5px; }
.card-body h3 { font-size: 1.15rem; font-weight: 700; margin: 0 0 6px 0; letter-spacing: -0.2px; color: #18181b; }
.card-body .desc { font-size: 0.85rem; color: #6b7a8e; line-height: 1.5; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; margin-bottom: 8px; }
.card-body .author { font-size: 0.8rem; color: #8e99ab; display: flex; align-items: center; gap: 6px; }
.card-footer { padding: 12px 22px 20px; border-top: 1px solid #f1f4f9; display: flex; justify-content: space-between; align-items: center; }
.card-footer .action-btn { padding: 8px 20px; border-radius: 30px; border: none; background: #f1f4f9; font-weight: 500; font-size: 0.8rem; color: #3d4a5c; cursor: pointer; transition: all 0.2s; display: inline-flex; align-items: center; gap: 6px; }
.card-footer .action-btn:hover { background: #18181b; color: #fff; }
.card-footer .participant-count { font-size: 0.75rem; color: #8e99ab; }
.empty-state { grid-column: 1 / -1; text-align: center; padding: 80px 20px; color: #8e99ab; }
.empty-state i { font-size: 3rem; color: #d5dde8; margin-bottom: 16px; display: block; }
@media (max-width: 768px) {
  .square-header { flex-direction: column; align-items: stretch; }
  .square-actions { flex-direction: column; align-items: stretch; }
  .search-box input { min-width: auto; width: 100%; }
  .activity-grid { grid-template-columns: 1fr; }
}
</style>