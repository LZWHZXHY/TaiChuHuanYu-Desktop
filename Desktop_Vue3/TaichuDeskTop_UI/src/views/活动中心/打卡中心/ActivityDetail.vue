<template>
  <div class="detail-page-container" v-if="!loading">
    <!-- 头部 -->
    <header class="activity-hero">
      <div class="hero-content">
        <h1>{{ activityInfo.title }}</h1>
        <div class="meta">
          <span><i class="fas fa-scroll"></i> {{ activityInfo.rule }}</span>
          <span><i class="fas fa-user"></i> {{ members.length }} 位挑战者</span>
          <span class="status-badge" :class="statusClass(activityInfo.status)">
            {{ activityInfo.status }}
          </span>
        </div>
      </div>
      <div class="hero-actions">
        <template v-if="isOwner">
          <button class="btn-admin" @click="openEditModal">
            <i class="fas fa-edit"></i> 编辑
          </button>
          <button class="btn-admin btn-danger" @click="confirmDelete">
            <i class="fas fa-trash"></i> 删除
          </button>
        </template>
        <button class="btn-join" :class="{ joined: isJoined }" @click="toggleJoin" :disabled="joining">
          <i :class="isJoined ? 'fas fa-check' : 'fas fa-plus'"></i>
          {{ isJoined ? '已参与' : '立即参与' }}
        </button>
        <button class="btn-outline"><i class="fas fa-share-alt"></i></button>
      </div>
    </header>

    <!-- 统计卡片 -->
    <div class="stats-row">
      <div class="stat-card"><div class="number purple">{{ stats.totalDays || 0 }}</div><div class="label">总天数</div></div>
      <div class="stat-card"><div class="number green">{{ stats.completionRate || 0 }}%</div><div class="label">打卡率</div></div>
      <div class="stat-card"><div class="number amber">{{ stats.consecutiveDays || 0 }}</div><div class="label">连续打卡</div></div>
      <div class="stat-card"><div class="number">#{{ stats.rank || 0 }}</div><div class="label">排名</div></div>
    </div>

    <!-- 主体 -->
    <div class="main-body">
      <section class="grid-section">
        <div class="member-list-horizontal">
          <button v-for="m in members" :key="m.id"
                  :class="['member-tab', { active: activeMember.id === m.id }]"
                  @click="switchMember(m)">
            <span class="dot" :class="{ 'active-dot': m.active }"></span>
            {{ m.name }}
          </button>
        </div>

        <div v-if="members.length === 0" class="empty-members">
          <p>暂无成员数据</p>
        </div>
        <div v-else-if="activeMember && activeMember.records && activeMember.records.length > 0" class="grid">
          <div v-for="cell in activeMember.records" :key="cell.day"
               :class="[
                 'cell',
                 { 
                   completed: cell.isCompleted, 
                   late: cell.isLate,
                   disabled: !cell.isCompleted && cell.day !== (stats.elapsedDays || 1)
                 }
               ]"
               @click="viewRecord(activeMember, cell)">
            {{ cell.day }}
          </div>
        </div>
        <div v-else class="empty-members">
          <p>该成员暂无打卡记录</p>
        </div>

        <div class="legend">
          <span><span class="dot-legend completed"></span> 已完成</span>
          <span><span class="dot-legend late"></span> 补卡</span>
          <span><span class="dot-legend normal"></span> 未打卡</span>
          <span><span class="dot-legend disabled"></span> 不可打卡</span>
        </div>
      </section>

      <aside class="stream-section">
        <div class="section-title"><i class="fas fa-stream"></i> {{ activeMember.name }} 的打卡轨迹</div>
        <div class="feed">
          <div v-for="log in filteredLogs" :key="log.id" class="log-card">
            <div class="log-header">
              <span class="user"><i class="fas fa-user-circle"></i> {{ activeMember.name }}</span>
              <span class="time">第 {{ log.day }} 天 · {{ log.time }}</span>
            </div>
            <p class="text">{{ log.text }}</p>
            <div v-if="log.image" class="log-image" :style="{ backgroundImage: `url(${log.image})` }"></div>
          </div>
          <div v-if="filteredLogs.length === 0" class="empty-state">
            <i class="fas fa-inbox"></i><p>暂无打卡内容</p>
          </div>
        </div>
      </aside>
    </div>

    <!-- 讨论区 -->
    <DiscussionBoard :activity-id="activityInfo.id" />

    <!-- 打卡弹窗 -->
    <CheckInModal
      v-model:visible="showModal"
      :member="modalMember"
      :day="modalDay"
      @checkin="handleCheckin"
    />

    <!-- 编辑弹窗 -->
    <div v-if="showEditModal" class="modal-overlay" @click.self="closeEditModal">
      <div class="modal-card">
        <div class="modal-header">
          <h3><i class="fas fa-edit"></i> 编辑活动</h3>
          <button class="close-btn" @click="closeEditModal">&times;</button>
        </div>
        <div class="modal-body">
          <div class="form-group">
            <label>活动名称 <span class="required">*</span></label>
            <input type="text" v-model="editForm.title" placeholder="输入活动名称..." />
          </div>
          <div class="form-group">
            <label>规则说明 <span class="required">*</span></label>
            <textarea v-model="editForm.rule" rows="3" placeholder="详细描述打卡要求..."></textarea>
          </div>
          <div class="form-row">
            <div class="form-group">
              <label>活动类型 <span class="required">*</span></label>
              <div class="type-tags">
                <span 
                  v-for="type in typeOptions" 
                  :key="type.id"
                  class="type-tag-option" 
                  :class="{ active: editForm.typeId === type.id }"
                  @click="editForm.typeId = type.id">
                  {{ type.name }}
                </span>
              </div>
            </div>
            <div class="form-group">
              <label>周期 (天) <span class="required">*</span></label>
              <input type="number" v-model.number="editForm.days" min="7" max="100" />
              <div class="hint">建议 7~100 天</div>
            </div>
          </div>
          <div class="form-group">
            <label>状态</label>
            <select v-model="editForm.status">
              <option value="招募中">招募中</option>
              <option value="进行中">进行中</option>
              <option value="已结束">已结束</option>
            </select>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn-cancel" @click="closeEditModal">取消</button>
          <button class="btn-submit" @click="saveEdit" :disabled="submittingEdit">
            <i class="fas fa-save"></i> 保存修改
          </button>
        </div>
      </div>
    </div>
  </div>

  <!-- 加载状态 -->
  <div v-else class="loading-container">
    <i class="fas fa-spinner fa-spin"></i> 加载中...
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import CheckInModal from './CheckInModal.vue';
import DiscussionBoard from './DiscussionBoard.vue';
import request from '@/utils/request';
import { useUserStore } from '@/stores/user';

// ---- 类型定义 ----
interface RecordItem {
  day: number;
  isCompleted: boolean;
  isLate: boolean;
  text: string;
  image: string;
}

interface Member {
  id: number;
  name: string;
  active: boolean;
  records: RecordItem[];
}

// ---- 接收父组件传递的活动数据 ----
const props = defineProps<{
  activity?: {
    id: number;
    title: string;
    description?: string;
    status: string;
    owner?: string;
    type?: string;
  };
}>();

// ---- 路由 & Store ----
const route = useRoute();
const router = useRouter();
const userStore = useUserStore();
const currentUser = computed(() => userStore.userInfo);

// ---- 活动 ID 计算 ----
const activityId = computed(() => {
  if (props.activity?.id) {
    return props.activity.id;
  }
  const id = route.params.id;
  if (!id) return 0;
  const num = Number(id);
  return isNaN(num) ? 0 : num;
});

// ---- 响应式状态 ----
const loading = ref(true);

const activityInfo = ref<{
  id: number;
  title: string;
  rule: string;
  status: string;
  owner: string;
  type: string;
}>({
  id: 0,
  title: '',
  rule: '',
  status: '',
  owner: '',
  type: ''
});

const members = ref<Member[]>([]);
const activeMember = ref<Member>({} as Member);
const stats = ref({
  totalDays: 0,
  elapsedDays: 0,
  completionRate: 0,
  consecutiveDays: 0,
  rank: 0
});
const isJoined = ref(false);
const joining = ref(false);
const isOwner = ref(false);

// 打卡弹窗
const showModal = ref(false);
const modalMember = ref<Member | null>(null);
const modalDay = ref<number>(0);

// 编辑弹窗
const showEditModal = ref(false);
const submittingEdit = ref(false);
const typeOptions = ref<{ id: number; name: string }[]>([]);
const editForm = ref({
  title: '',
  rule: '',
  typeId: 0,
  days: 30,
  status: '招募中'
});

// ---- 获取活动类型列表 ----
const fetchTypes = async () => {
  try {
    const data = await request.get('/activities/types');
    typeOptions.value = data || [];
  } catch (error) {
    console.error('获取活动类型失败:', error);
  }
};

// ---- 获取数据 ----
const fetchActivityDetail = async (id: number) => {
  try {
    const data = await request.get(`/activities/${id}`);
    activityInfo.value = {
      id: data.id,
      title: data.title,
      rule: data.description || '规则描述',
      status: data.status,
      owner: data.owner || '',
      type: data.type || ''
    };
    isOwner.value = data.owner === currentUser.value?.username;
  } catch (error) {
    console.error('获取活动详情失败:', error);
  }
};

const fetchMembers = async (id: number) => {
  try {
    console.log('正在获取成员列表，活动ID:', id);
    const data = await request.get(`/activities/${id}/members`);
    console.log('成员数据:', JSON.stringify(data, null, 2));
    if (data && data.length > 0) {
      members.value = data.map((m: any, index: number) => {
        let records = m.records || [];
        if (records.length === 0) {
          records = Array.from({ length: 30 }, (_, i) => ({
            day: i + 1,
            isCompleted: false,
            isLate: false,
            text: '',
            image: ''
          }));
        } else if (records.length < 30) {
          const existingDays = records.map((r: any) => r.day);
          for (let day = 1; day <= 30; day++) {
            if (!existingDays.includes(day)) {
              records.push({
                day: day,
                isCompleted: false,
                isLate: false,
                text: '',
                image: ''
              });
            }
          }
          records.sort((a: any, b: any) => a.day - b.day);
        }
        return {
          id: m.id,
          name: m.name,
          active: index === 0,
          records: records
        };
      });
      activeMember.value = members.value[0];
      console.log('已加载成员数量:', members.value.length);
    } else {
      const defaultRecords = Array.from({ length: 30 }, (_, i) => ({
        day: i + 1,
        isCompleted: false,
        isLate: false,
        text: '',
        image: ''
      }));
      members.value = [
        {
          id: 0,
          name: '无成员',
          active: true,
          records: defaultRecords
        }
      ];
      activeMember.value = members.value[0];
      console.warn('后端返回成员为空，已创建默认占位成员');
    }
  } catch (error) {
    console.error('获取成员列表失败:', error);
    const defaultRecords = Array.from({ length: 30 }, (_, i) => ({
      day: i + 1,
      isCompleted: false,
      isLate: false,
      text: '',
      image: ''
    }));
    members.value = [
      {
        id: 0,
        name: '加载失败',
        active: true,
        records: defaultRecords
      }
    ];
    activeMember.value = members.value[0];
  }
};

const fetchMyStatus = async (id: number) => {
  try {
    const data = await request.get(`/activities/${id}/my-status`);
    isJoined.value = data.isJoined || false;
    if (isJoined.value) {
      stats.value = {
        totalDays: data.totalDays || 0,
        elapsedDays: data.elapsedDays || 0,
        completionRate: data.completionRate || 0,
        consecutiveDays: data.consecutiveDays || 0,
        rank: 0
      };
    }
  } catch (error) {
    console.error('获取我的状态失败:', error);
  }
};

const fetchStats = async (id: number) => {
  try {
    const data = await request.get(`/activities/${id}/stats`);
    stats.value = {
      totalDays: data.totalDays || 0,
      elapsedDays: data.elapsedDays || 0,
      completionRate: data.completionRate || 0,
      consecutiveDays: data.consecutiveDays || 0,
      rank: data.rank || 0
    };
  } catch (error) {
    console.error('获取统计数据失败:', error);
  }
};

// ---- 加载所有数据 ----
const loadData = async (id: number) => {
  if (!id || id === 0) {
    loading.value = false;
    return;
  }
  loading.value = true;
  try {
    await Promise.all([
      fetchActivityDetail(id),
      fetchMembers(id),
      fetchMyStatus(id),
      fetchStats(id)
    ]);
    await fetchTypes();
  } catch (error) {
    console.error('加载数据失败:', error);
  } finally {
    loading.value = false;
  }
};

// ---- 切换成员 ----
const switchMember = (m: Member) => {
  activeMember.value = m;
  members.value.forEach(mem => (mem.active = mem.id === m.id));
};

// ---- 打卡交互 ----
const viewRecord = (m: Member, cell: RecordItem) => {
  const day = cell.day;
  if (cell.isCompleted) {
    alert(`${m.name} 第 ${day} 天: ${cell.text}`);
    return;
  }

  const isCurrentUser = m.name === currentUser.value?.username;
  if (!isCurrentUser) {
    alert('你只能打卡自己的记录');
    return;
  }

  const today = stats.value.elapsedDays || 1;
  if (day !== today) {
    if (day > today) {
      alert(`今天是活动第 ${today} 天，不能提前打卡`);
    } else {
      alert(`今天是活动第 ${today} 天，不能补签`);
    }
    return;
  }

  modalMember.value = m;
  modalDay.value = day;
  showModal.value = true;
};

// ---- 提交打卡（增强日志和错误提示） ----
const handleCheckin = async (payload: { member: Member; day: number; text: string; image: string }) => {
  const { day, text, image } = payload;
  const id = activityId.value;
  console.log('打卡请求参数:', { id, day, text, image });
  if (!id) {
    console.error('活动ID无效');
    alert('活动ID无效，无法打卡');
    return;
  }
  try {
    const response = await request.post(`/activities/${id}/checkin`, { day, text, image });
    console.log('打卡响应:', response);
    alert('打卡成功！');
    // 刷新数据
    await Promise.all([
      fetchMembers(id),
      fetchMyStatus(id),
      fetchStats(id)
    ]);
    // 更新本地记录（可选）
    const targetMember = members.value.find(m => m.id === payload.member.id);
    if (targetMember) {
      const record = targetMember.records.find(r => r.day === day);
      if (record) {
        record.isCompleted = true;
        record.text = text;
        record.image = image;
      }
    }
  } catch (error: any) {
    console.error('打卡失败:', error);
    const msg = error.response?.data?.message || error.message || '打卡失败，请重试';
    alert(msg);
  }
};

// ---- 加入/退出 ----
const toggleJoin = async () => {
  const id = activityId.value;
  console.log('toggleJoin activityId:', id);
  if (!id || id === 0) {
    alert('活动ID无效，请刷新页面重试');
    return;
  }
  joining.value = true;
  try {
    const url = isJoined.value 
      ? `/activities/${id}/leave`
      : `/activities/${id}/join`;
    const data = await request.post(url);
    isJoined.value = data.isJoined;
    await Promise.all([
      fetchMembers(id),
      fetchMyStatus(id),
      fetchStats(id)
    ]);
  } catch (error: any) {
    console.error('操作失败:', error);
    const msg = error.response?.data?.message || error.message || '操作失败，请重试';
    alert(msg);
  } finally {
    joining.value = false;
  }
};

// ---- 编辑功能 ----
const openEditModal = () => {
  const currentType = activityInfo.value.type || '';
  const foundType = typeOptions.value.find(t => t.name === currentType);
  editForm.value = {
    title: activityInfo.value.title,
    rule: activityInfo.value.rule,
    typeId: foundType?.id || 0,
    days: stats.value.totalDays || 30,
    status: activityInfo.value.status || '招募中'
  };
  showEditModal.value = true;
};

const closeEditModal = () => {
  showEditModal.value = false;
  editForm.value = { title: '', rule: '', typeId: 0, days: 30, status: '招募中' };
};

const saveEdit = async () => {
  const id = activityId.value;
  if (!id) return;
  
  if (!editForm.value.title || !editForm.value.rule) {
    alert('请填写完整信息');
    return;
  }
  
  submittingEdit.value = true;
  try {
    await request.put(`/activities/${id}`, {
      title: editForm.value.title,
      description: editForm.value.rule,
      typeId: editForm.value.typeId,
      days: editForm.value.days,
      status: editForm.value.status
    });
    alert('活动已更新！');
    closeEditModal();
    await loadData(id);
  } catch (error: any) {
    console.error('更新失败:', error);
    const msg = error.response?.data?.message || error.message || '更新失败，请重试';
    alert(msg);
  } finally {
    submittingEdit.value = false;
  }
};

// ---- 删除功能 ----
const confirmDelete = () => {
  if (confirm(`确定要删除活动 "${activityInfo.value.title}" 吗？此操作不可撤销！`)) {
    deleteActivity();
  }
};

const deleteActivity = async () => {
  const id = activityId.value;
  if (!id) return;
  
  try {
    await request.delete(`/activities/${id}`);
    alert('活动已删除');
    router.push('/activity');
  } catch (error: any) {
    console.error('删除失败:', error);
    const msg = error.response?.data?.message || error.message || '删除失败，请重试';
    alert(msg);
  }
};

// ---- 计算属性：动态流 ----
const filteredLogs = computed(() => {
  if (!activeMember.value || !activeMember.value.records) return [];
  return activeMember.value.records
    .filter(r => r.isCompleted)
    .map(r => ({
      id: r.day,
      day: r.day,
      text: r.text,
      image: r.image,
      time: `${Math.floor(Math.random() * 3) + 1}天前`
    }));
});

// ---- 状态样式 ----
const statusClass = (status: string) => ({
  ongoing: status === '进行中',
  recruiting: status === '招募中',
  ended: status === '已结束'
});

// ---- 监听路由变化 ----
watch(
  () => route.params.id,
  (newId) => {
    if (props.activity?.id) return;
    const id = Number(newId);
    if (!isNaN(id) && id > 0) {
      loadData(id);
    } else {
      loading.value = false;
    }
  },
  { immediate: true }
);

// 监听 props.activity 变化
watch(
  () => props.activity,
  (newActivity) => {
    if (newActivity?.id) {
      loadData(newActivity.id);
    }
  },
  { immediate: true }
);

onMounted(() => {
  const id = activityId.value;
  if (id > 0) {
    loadData(id);
  } else {
    loading.value = false;
  }
});
</script>



<style scoped>
/* 原有样式保持不变，新增编辑弹窗样式 */
.btn-admin {
  background: transparent;
  border: 1px solid #e5e7eb;
  color: #374151;
  padding: 8px 16px;
  border-radius: 30px;
  font-weight: 500;
  font-size: 0.85rem;
  cursor: pointer;
  transition: background 0.2s, color 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}
.btn-admin:hover { background: #f3f4f6; }
.btn-admin.btn-danger:hover {
  background: #fee2e2;
  color: #dc2626;
  border-color: #fecaca;
}

.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.3);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 1000;
  backdrop-filter: blur(2px);
}
.modal-card {
  background: #fff;
  border-radius: 12px;
  padding: 28px 32px;
  width: 480px;
  max-width: 92%;
  max-height: 80vh;
  overflow-y: auto;
  box-shadow: 0 8px 30px rgba(0, 0, 0, 0.1);
  animation: fadeIn 0.2s ease;
}
@keyframes fadeIn {
  from { opacity: 0; transform: scale(0.96); }
  to { opacity: 1; transform: scale(1); }
}
.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}
.modal-header h3 {
  margin: 0;
  font-weight: 600;
  font-size: 1.1rem;
  color: #1f2937;
}
.modal-header h3 i { color: #6366f1; margin-right: 6px; }
.close-btn {
  background: none;
  border: none;
  font-size: 1.6rem;
  cursor: pointer;
  color: #9ca3af;
  transition: 0.2s;
}
.close-btn:hover { color: #1f2937; }

.form-group { margin-bottom: 18px; }
.form-group label {
  display: block;
  font-weight: 500;
  font-size: 0.85rem;
  margin-bottom: 4px;
  color: #374151;
}
.form-group label .required { color: #ef4444; margin-left: 2px; }
.form-group input,
.form-group textarea,
.form-group select {
  width: 100%;
  padding: 10px 14px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  font-size: 0.9rem;
  font-family: inherit;
  transition: border 0.2s;
  background: #fafafa;
  color: #1f2937;
}
.form-group input:focus,
.form-group textarea:focus,
.form-group select:focus {
  outline: none;
  border-color: #6366f1;
  background: #fff;
}
.form-group textarea { resize: vertical; min-height: 60px; }
.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 14px;
}
.type-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 2px;
}
.type-tag-option {
  padding: 4px 14px;
  border-radius: 20px;
  border: 1px solid #e5e7eb;
  background: #fafafa;
  font-size: 0.75rem;
  font-weight: 500;
  color: #374151;
  cursor: pointer;
  transition: all 0.15s;
}
.type-tag-option:hover { border-color: #9ca3af; }
.type-tag-option.active {
  background: #1f2937;
  color: #fff;
  border-color: #1f2937;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 20px;
}
.btn-cancel {
  padding: 8px 20px;
  border: 1px solid #e5e7eb;
  border-radius: 30px;
  background: #fff;
  color: #374151;
  cursor: pointer;
  transition: background 0.2s;
}
.btn-cancel:hover { background: #f3f4f6; }
.btn-submit {
  padding: 8px 24px;
  border: none;
  border-radius: 30px;
  background: #6366f1;
  color: #fff;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}
.btn-submit:hover:not(:disabled) { background: #4f46e5; }
.btn-submit:disabled { opacity: 0.5; cursor: not-allowed; }

@media (max-width: 768px) {
  .form-row { grid-template-columns: 1fr; }
}

/* 原有详情样式保持不变 */
.detail-page-container { max-width: 1100px; margin: 0 auto; display: flex; flex-direction: column; gap: 24px; }
.activity-hero {
  background: #fff;
  border-radius: 12px;
  padding: 28px 32px;
  border: 1px solid #eee;
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: 20px;
}
.hero-content h1 {
  font-size: 1.6rem;
  font-weight: 600;
  letter-spacing: -0.3px;
  color: #1a1a1a;
  margin-bottom: 6px;
}
.hero-content .meta {
  font-size: 0.85rem;
  color: #6b7280;
  display: flex;
  align-items: center;
  gap: 14px;
  flex-wrap: wrap;
}
.hero-content .meta i { margin-right: 4px; color: #9ca3af; }
.status-badge {
  padding: 2px 12px;
  border-radius: 20px;
  font-size: 0.7rem;
  font-weight: 500;
  color: #fff;
}
.status-badge.ongoing { background: #6366f1; }
.status-badge.recruiting { background: #10b981; }
.status-badge.ended { background: #9ca3af; }

.hero-actions { display: flex; gap: 10px; align-items: center; flex-wrap: wrap; }
.btn-join {
  background: #6366f1;
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
  gap: 6px;
}
.btn-join:hover { background: #4f46e5; }
.btn-join.joined { background: #10b981; }
.btn-join.joined:hover { background: #059669; }
.btn-outline {
  background: transparent;
  border: 1px solid #e5e7eb;
  color: #374151;
  padding: 8px 16px;
  border-radius: 30px;
  font-weight: 500;
  font-size: 0.85rem;
  cursor: pointer;
  transition: background 0.2s;
}
.btn-outline:hover { background: #f3f4f6; }

.stats-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));
  gap: 12px;
}
.stat-card {
  background: #fff;
  border-radius: 10px;
  padding: 16px 20px;
  border: 1px solid #eee;
  text-align: center;
}
.stat-card .number {
  font-size: 1.4rem;
  font-weight: 600;
  line-height: 1.3;
}
.stat-card .number.green { color: #10b981; }
.stat-card .number.purple { color: #6366f1; }
.stat-card .number.amber { color: #f59e0b; }
.stat-card .label {
  font-size: 0.7rem;
  color: #9ca3af;
  margin-top: 2px;
}

.main-body {
  display: grid;
  grid-template-columns: 1fr 340px;
  gap: 32px;
  align-items: start;
}

.member-list-horizontal {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
  margin-bottom: 16px;
}
.member-tab {
  padding: 6px 16px;
  border-radius: 20px;
  border: 1px solid #e5e7eb;
  background: #fff;
  cursor: pointer;
  font-size: 0.75rem;
  font-weight: 500;
  color: #374151;
  transition: all 0.15s;
  display: flex;
  align-items: center;
  gap: 6px;
}
.member-tab:hover { background: #f3f4f6; border-color: #d1d5db; }
.member-tab.active {
  background: #1f2937;
  color: #fff;
  border-color: #1f2937;
}
.member-tab .dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  display: inline-block;
  background: #d1d5db;
}
.member-tab .dot.active-dot { background: #10b981; }

.grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 6px;
  margin-top: 4px;
}
.cell {
  aspect-ratio: 1;
  border-radius: 6px;
  border: 1px solid #e5e7eb;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.65rem;
  font-weight: 500;
  color: #9ca3af;
  cursor: pointer;
  transition: background 0.15s;
  background: #fff;
}
.cell:hover { border-color: #9ca3af; }
.cell.completed {
  background: #1f2937;
  color: #fff;
  border-color: #1f2937;
}
.cell.completed:hover { background: #374151; }
.cell.late {
  background: #fef3c7;
  color: #92400e;
  border-color: #fcd34d;
}
.cell.late:hover { background: #fde68a; }

.legend {
  display: flex;
  gap: 16px;
  margin-top: 10px;
  font-size: 0.7rem;
  color: #6b7280;
}
.legend .dot-legend {
  display: inline-block;
  width: 12px;
  height: 12px;
  border-radius: 3px;
  margin-right: 4px;
  vertical-align: middle;
}
.legend .dot-legend.completed { background: #1f2937; }
.legend .dot-legend.late { background: #fef3c7; border: 1px solid #fcd34d; }
.legend .dot-legend.normal { background: #fff; border: 1px solid #e5e7eb; }

.stream-section {
  background: #fff;
  border-radius: 12px;
  border: 1px solid #eee;
  padding: 20px 22px;
  max-height: 600px;
  overflow-y: auto;
}
.stream-section .section-title {
  font-weight: 600;
  font-size: 0.95rem;
  margin-bottom: 16px;
  color: #1f2937;
  display: flex;
  align-items: center;
  gap: 8px;
}
.stream-section .section-title i { color: #6366f1; }
.log-card {
  padding: 14px 16px;
  border-radius: 10px;
  background: #f9fafb;
  border: 1px solid #f3f4f6;
  margin-bottom: 12px;
}
.log-card:hover { background: #fff; border-color: #e5e7eb; }
.log-header {
  display: flex;
  justify-content: space-between;
  font-size: 0.7rem;
  color: #9ca3af;
  margin-bottom: 4px;
}
.log-header .user {
  font-weight: 500;
  color: #1f2937;
  display: flex;
  align-items: center;
  gap: 4px;
}
.log-header .time { font-size: 0.65rem; }
.log-card .text {
  font-size: 0.85rem;
  line-height: 1.5;
  color: #374151;
  margin: 2px 0 6px;
}
.log-image {
  height: 120px;
  background-size: cover;
  background-position: center;
  border-radius: 8px;
  margin-top: 6px;
  border: 1px solid #f3f4f6;
}
.empty-state {
  text-align: center;
  padding: 30px 10px;
  color: #9ca3af;
}
.empty-state i {
  font-size: 2rem;
  display: block;
  margin-bottom: 8px;
  color: #d1d5db;
}
.stream-section::-webkit-scrollbar { width: 4px; }
.stream-section::-webkit-scrollbar-track { background: #f3f4f6; border-radius: 4px; }
.stream-section::-webkit-scrollbar-thumb { background: #d1d5db; border-radius: 4px; }

@media (max-width: 1024px) {
  .main-body { grid-template-columns: 1fr; }
  .stream-section { max-height: 400px; }
}
@media (max-width: 768px) {
  .activity-hero { padding: 20px; flex-direction: column; }
  .hero-content h1 { font-size: 1.3rem; }
  .stats-row { grid-template-columns: repeat(2, 1fr); }
}
</style>