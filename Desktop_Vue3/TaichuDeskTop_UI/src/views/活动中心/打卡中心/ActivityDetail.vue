<template>
  <div class="detail-page-container" v-if="!loading && !loadError">
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
        <RouterLink class="btn-outline" to="/activity/checkin">返回列表</RouterLink>
      </div>
    </header>

    <CompetitionPanel :activity-id="activityId" />

    <!-- 统计卡片 -->
    <div class="stats-row">
      <div class="stat-card"><div class="number purple">{{ stats.totalDays || 0 }}</div><div class="label">总天数</div></div>
      <div class="stat-card"><div class="number green">{{ stats.completionRate || 0 }}%</div><div class="label">打卡率</div></div>
      <div class="stat-card"><div class="number amber">{{ stats.consecutiveDays || 0 }}</div><div class="label">连续打卡</div></div>
      <div class="stat-card"><div class="number">{{ stats.rank > 0 ? `#${stats.rank}` : '—' }}</div><div class="label">排名</div></div>
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
          <button type="button" v-for="cell in activeMember.records" :key="cell.day"
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
          </button>
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
        <div class="section-title"><i class="fas fa-stream"></i> <UserLink :id="activeMember.userId" :name="activeMember.name" /> 的打卡轨迹</div>
        <div class="feed">
          <div v-for="log in filteredLogs" :key="log.id" class="log-card">
            <div class="log-header">
              <UserLink class="user" :id="activeMember.userId" :name="activeMember.name"><i class="fas fa-user-circle"></i> {{ activeMember.name }}</UserLink>
              <span class="time">第 {{ log.day }} 天</span>
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
      :submitting="checkingIn"
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
            <input aria-label="活动名称" type="text" v-model="editForm.title" placeholder="输入活动名称..." />
          </div>
          <div class="form-group">
            <label>规则说明 <span class="required">*</span></label>
            <textarea aria-label="规则说明" v-model="editForm.rule" rows="3" placeholder="详细描述打卡要求..."></textarea>
          </div>
          <div class="form-group">
            <label>活动形态</label>
            <div class="type-tags">
              <button type="button" :class="{ active: editForm.kind === 'general' }" @click="editForm.kind = 'general'" style="padding:7px 14px;border:1px solid var(--border-line);border-radius:0;background:var(--paper-card);cursor:pointer">普通活动</button>
              <button type="button" :class="{ active: editForm.kind === 'match' }" @click="editForm.kind = 'match'" style="padding:7px 14px;border:1px solid var(--border-line);border-radius:0;background:var(--paper-card);cursor:pointer">对战赛</button>
              <button type="button" :class="{ active: editForm.kind === 'contest' }" @click="editForm.kind = 'contest'" style="padding:7px 14px;border:1px solid var(--border-line);border-radius:0;background:var(--paper-card);cursor:pointer">作品赛</button>
            </div>
          </div>
          <div v-if="editForm.kind !== 'general'" class="form-group">
            <label>比赛时间</label>
            <div style="display:grid;gap:8px">
              <input type="date" v-model="editForm.signupDeadline" aria-label="报名截止" />
              <input type="date" v-model="editForm.startAt" aria-label="开始时间" />
              <input type="date" v-model="editForm.endAt" aria-label="结束时间" />
            </div>
          </div>
          <div class="form-row">
            <div class="form-group">
              <label>活动类型 <span class="required">*</span></label>
              <div class="type-tags">
                <button type="button" v-for="type in typeOptions"
                  :key="type.id"
                  class="type-tag-option"
                  :class="{ active: editForm.typeId === type.id }"
                  @click="editForm.typeId = type.id">
                  {{ type.name }}
                </button>
              </div>
            </div>
            <div class="form-group">
              <label>周期 (天) <span class="required">*</span></label>
              <input aria-label="周期 (天)" type="number" v-model.number="editForm.days" min="7" max="100" />
              <div class="hint">建议 7~100 天</div>
            </div>
          </div>
          <div class="form-group">
            <label>状态</label>
            <select aria-label="状态" v-model="editForm.status">
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

  <div v-else-if="loadError" class="empty-state" role="alert">{{ loadError }} <button class="btn-primary" @click="loadData(activityId)">重新加载</button><RouterLink to="/activity/checkin">返回活动列表</RouterLink></div>
  <!-- 加载状态 -->
  <div v-else class="loading-container">
    <i class="fas fa-spinner fa-spin"></i> 加载中...
  </div>
</template>

<script setup lang="ts">
import UserLink from '@/components/UserLink.vue'
import { ref, computed, watch } from 'vue';
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
  userId?: string; // 成员的用户 ID，点击名字进入个人主页
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
import CompetitionPanel from './CompetitionPanel.vue'
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
const loadError = ref('');
const checkingIn = ref(false);
const activityDays = ref(30);

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
const rawActivity = ref<any>(null);
const editForm = ref({
  title: '',
  rule: '',
  typeId: 0,
  days: 30,
  status: '招募中',
  kind: 'general',
  signupDeadline: '',
  startAt: '',
  endAt: ''
});
const toDateInput = (v: string | null | undefined) => v ? String(v).slice(0, 10) : '';

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
    rawActivity.value = data;
    activityInfo.value = {
      id: data.id,
      title: data.title,
      rule: data.description || '规则描述',
      status: data.status,
      owner: data.owner || '',
      type: data.type || ''
    };
    activityDays.value = data.days || 30;
    isOwner.value = data.owner === currentUser.value?.username;
  } catch (error) {
    throw error;
  }
};

// 按活动实际周期展示日历；不制造占位成员或虚构打卡记录。
const fillRecords = (records: RecordItem[] = []) => {
  const existing = new Map(records.map(record => [record.day, record]));
  return Array.from({ length: activityDays.value }, (_, index) => existing.get(index + 1) || {
    day: index + 1, isCompleted: false, isLate: false, text: '', image: ''
  });
};

const fetchMembers = async (id: number) => {
  try {
    const data = await request.get<Member[]>(`/activities/${id}/members`);
    if (data && data.length > 0) {
      members.value = data.map((member, index) => ({
        id: member.id,
        name: member.name,
        userId: member.userId,
        active: index === 0,
        records: fillRecords(member.records || [])
      }));
      activeMember.value = members.value[0];
    } else {
      members.value = []; activeMember.value = {} as Member;
    }
  } catch (error) {
    console.error('获取成员列表失败:', error);
    throw error;
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

const refreshParticipation = (id: number) => Promise.all([
  fetchMembers(id), fetchMyStatus(id), fetchStats(id)
]);

// ---- 加载所有数据 ----
const loadData = async (id: number) => {
  if (!id || id === 0) {
    loading.value = false;
    return;
  }
  loading.value = true; loadError.value = '';
  try {
    await fetchActivityDetail(id);
    await refreshParticipation(id);
    await fetchTypes();
  } catch (error) {
    loadError.value = '活动详情读取失败，请重试。';
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

// ---- 提交打卡 ----
const handleCheckin = async (payload: { member: Member; day: number; text: string; image: string }) => {
  if (checkingIn.value) return;
  const { day, text, image } = payload;
  const id = activityId.value;
  if (!id) {
    console.error('活动ID无效');
    alert('活动ID无效，无法打卡');
    return;
  }
  checkingIn.value = true;
  try {
    await request.post(`/activities/${id}/checkin`, { day, text, image });
    showModal.value = false;
    alert('打卡成功！');
    // 刷新数据
    await refreshParticipation(id);
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
  } finally { checkingIn.value = false; }
};

// ---- 加入/退出 ----
const toggleJoin = async () => {
  const id = activityId.value;
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
    await refreshParticipation(id);
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
    days: activityDays.value,
    status: activityInfo.value.status || '招募中',
    kind: rawActivity.value?.kind || 'general',
    signupDeadline: toDateInput(rawActivity.value?.signupDeadline),
    startAt: toDateInput(rawActivity.value?.startAt),
    endAt: toDateInput(rawActivity.value?.endAt)
  };
  showEditModal.value = true;
};

const closeEditModal = () => {
  showEditModal.value = false;
  editForm.value = { title: '', rule: '', typeId: 0, days: 30, status: '招募中', kind: 'general', signupDeadline: '', startAt: '', endAt: '' };
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
      status: editForm.value.status,
      kind: editForm.value.kind,
      rules: editForm.value.rule,
      signupDeadline: editForm.value.signupDeadline || null,
      startAt: editForm.value.startAt || null,
      endAt: editForm.value.endAt || null
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
      image: r.image
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


</script>



<style scoped>
/* 原有样式保持不变，新增编辑弹窗样式 */
.btn-admin {
  background: transparent;
  border: 1px solid var(--border-line);
  color: var(--text-primary);
  padding: 8px 16px;
  border-radius: 0;
  font-weight: 500;
  font-size: 0.85rem;
  cursor: pointer;
  transition: background 0.2s, color 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}
.btn-admin:hover { background: var(--bg-sub); }
.btn-admin.btn-danger:hover {
  background: var(--bg-sub);
  color: var(--danger);
  border-color: var(--border-line);
}

.modal-overlay {
  position: fixed;
  inset: 0;
  background: var(--overlay);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 1000;
  backdrop-filter: blur(2px);
}
.modal-card {
  background: var(--paper-card);
  border-radius: 0;
  padding: 28px 32px;
  width: 660px;
  max-width: 94%;
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
  color: var(--text-primary);
}
.modal-header h3 i { color: var(--info); margin-right: 6px; }
.close-btn {
  background: none;
  border: none;
  font-size: 1.6rem;
  cursor: pointer;
  color: var(--text-secondary);
  transition: 0.2s;
}
.close-btn:hover { color: var(--text-primary); }

.form-group { margin-bottom: 18px; }
.form-group label {
  display: block;
  font-weight: 500;
  font-size: 0.85rem;
  margin-bottom: 4px;
  color: var(--text-primary);
}
.form-group label .required { color: var(--danger); margin-left: 2px; }
.form-group input,
.form-group textarea,
.form-group select {
  width: 100%;
  padding: 10px 14px;
  border: 1px solid var(--border-line);
  border-radius: 0;
  font-size: 0.9rem;
  font-family: inherit;
  transition: border 0.2s;
  background: var(--paper-card);
  color: var(--text-primary);
}
.form-group input:focus,
.form-group textarea:focus,
.form-group select:focus {
  outline: none;
  border-color: var(--info);
  background: var(--paper-card);
}
.form-group textarea { resize: vertical; min-height: 60px; }
.form-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(100%, 240px), 1fr));
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
  border-radius: 0;
  border: 1px solid var(--border-line);
  background: var(--paper-card);
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--text-primary);
  cursor: pointer;
  transition: all 0.15s;
}
.type-tag-option:hover { border-color: var(--border-line); }
.type-tag-option.active {
  background: var(--bg-sub);
  color: var(--text-primary);
  border-color: var(--border-line);
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 20px;
}
.btn-cancel {
  padding: 8px 20px;
  border: 1px solid var(--border-line);
  border-radius: 0;
  background: var(--paper-card);
  color: var(--text-primary);
  cursor: pointer;
  transition: background 0.2s;
}
.btn-cancel:hover { background: var(--bg-sub); }
.btn-submit {
  padding: 8px 24px;
  border: none;
  border-radius: 0;
  background: var(--info);
  color: var(--on-accent);
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}
.btn-submit:hover:not(:disabled) { background: var(--info); }
.btn-submit:disabled { opacity: 0.5; cursor: not-allowed; }

@media (max-width: 768px) {
  .form-row { grid-template-columns: 1fr; }
}

/* 原有详情样式保持不变 */
.detail-page-container { max-width: 1100px; margin: 0 auto; display: flex; flex-direction: column; gap: 24px; }
.activity-hero {
  background: var(--paper-card);
  border-radius: 0;
  padding: 28px 32px;
  border: 1px solid var(--border-line);
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
  color: var(--text-primary);
  margin-bottom: 6px;
}
.hero-content .meta {
  font-size: 0.85rem;
  color: var(--text-secondary);
  display: flex;
  align-items: center;
  gap: 14px;
  flex-wrap: wrap;
}
.hero-content .meta i { margin-right: 4px; color: var(--text-secondary); }
.status-badge {
  padding: 2px 12px;
  border-radius: 0;
  font-size: 0.7rem;
  font-weight: 500;
  color: var(--text-primary);
}
.status-badge.ongoing { background: var(--info); }
.status-badge.recruiting { background: var(--success); }
.status-badge.ended { background: var(--bg-sub); }

.hero-actions { display: flex; gap: 10px; align-items: center; flex-wrap: wrap; }
.btn-join {
  background: var(--info);
  color: var(--on-accent);
  border: none;
  padding: 10px 24px;
  border-radius: 0;
  font-weight: 500;
  font-size: 0.9rem;
  cursor: pointer;
  transition: background 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.btn-join:hover { background: var(--info); }
.btn-join.joined { background: var(--success); }
.btn-join.joined:hover { background: var(--success); }
.btn-outline {
  background: transparent;
  border: 1px solid var(--border-line);
  color: var(--text-primary);
  padding: 8px 16px;
  border-radius: 0;
  font-weight: 500;
  font-size: 0.85rem;
  cursor: pointer;
  transition: background 0.2s;
}
.btn-outline:hover { background: var(--bg-sub); }

.stats-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));
  gap: 12px;
}
.stat-card {
  background: var(--paper-card);
  border-radius: 0;
  padding: 16px 20px;
  border: 1px solid var(--border-line);
  text-align: center;
}
.stat-card .number {
  font-size: 1.4rem;
  font-weight: 600;
  line-height: 1.3;
}
.stat-card .number.green { color: var(--success); }
.stat-card .number.purple { color: var(--info); }
.stat-card .number.amber { color: var(--warning); }
.stat-card .label {
  font-size: 0.7rem;
  color: var(--text-secondary);
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
  border-radius: 0;
  border: 1px solid var(--border-line);
  background: var(--paper-card);
  cursor: pointer;
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--text-primary);
  transition: all 0.15s;
  display: flex;
  align-items: center;
  gap: 6px;
}
.member-tab:hover { background: var(--bg-sub); border-color: var(--border-line); }
.member-tab.active {
  background: var(--bg-sub);
  color: var(--text-primary);
  border-color: var(--border-line);
}
.member-tab .dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  display: inline-block;
  background: var(--bg-sub);
}
.member-tab .dot.active-dot { background: var(--success); }

.grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 6px;
  margin-top: 4px;
}
.cell {
  aspect-ratio: 1;
  border-radius: 0;
  border: 1px solid var(--border-line);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.65rem;
  font-weight: 500;
  color: var(--text-secondary);
  cursor: pointer;
  transition: background 0.15s;
  background: var(--paper-card);
}
.cell:hover { border-color: var(--border-line); }
.cell.completed {
  background: var(--bg-sub);
  color: var(--text-primary);
  border-color: var(--border-line);
}
.cell.completed:hover { background: var(--bg-sub); }
.cell.late {
  background: var(--warning-soft);
  color: var(--warning);
  border-color: var(--warning);
}
.cell.late:hover { background: var(--warning-soft); }

.legend {
  display: flex;
  gap: 16px;
  margin-top: 10px;
  font-size: 0.7rem;
  color: var(--text-secondary);
}
.legend .dot-legend {
  display: inline-block;
  width: 12px;
  height: 12px;
  border-radius: 0;
  margin-right: 4px;
  vertical-align: middle;
}
.legend .dot-legend.completed { background: var(--bg-sub); }
.legend .dot-legend.late { background: var(--warning-soft); border: 1px solid var(--warning); }
.legend .dot-legend.normal { background: var(--paper-card); border: 1px solid var(--border-line); }

.stream-section {
  background: var(--paper-card);
  border-radius: 0;
  border: 1px solid var(--border-line);
  padding: 20px 22px;
  max-height: 600px;
  overflow-y: auto;
}
.stream-section .section-title {
  font-weight: 600;
  font-size: 0.95rem;
  margin-bottom: 16px;
  color: var(--text-primary);
  display: flex;
  align-items: center;
  gap: 8px;
}
.stream-section .section-title i { color: var(--info); }
.log-card {
  padding: 14px 16px;
  border-radius: 0;
  background: var(--paper-card);
  border: 1px solid var(--border-line);
  margin-bottom: 12px;
}
.log-card:hover { background: var(--paper-card); border-color: var(--border-line); }
.log-header {
  display: flex;
  justify-content: space-between;
  font-size: 0.7rem;
  color: var(--text-secondary);
  margin-bottom: 4px;
}
.log-header .user {
  font-weight: 500;
  color: var(--text-primary);
  display: flex;
  align-items: center;
  gap: 4px;
}
.log-header .time { font-size: 0.65rem; }
.log-card .text {
  font-size: 0.85rem;
  line-height: 1.5;
  color: var(--text-primary);
  margin: 2px 0 6px;
}
.log-image {
  height: 120px;
  background-size: cover;
  background-position: center;
  border-radius: 0;
  margin-top: 6px;
  border: 1px solid var(--border-line);
}
.empty-state {
  text-align: center;
  padding: 30px 10px;
  color: var(--text-secondary);
}
.empty-state i {
  font-size: 2rem;
  display: block;
  margin-bottom: 8px;
  color: var(--text-secondary);
}
.stream-section::-webkit-scrollbar { width: 4px; }
.stream-section::-webkit-scrollbar-track { background: var(--bg-sub); border-radius: 0; }
.stream-section::-webkit-scrollbar-thumb { background: var(--bg-sub); border-radius: 0; }

@media (max-width: 1024px) {
  .main-body { grid-template-columns: 1fr; }
  .stream-section { max-height: 400px; }
}
@media (max-width: 768px) {
  .activity-hero { padding: 20px; flex-direction: column; }
  .hero-content h1 { font-size: 1.3rem; }
  .stats-row { grid-template-columns: repeat(2, 1fr); }
}
/* 防止标题/正文被 flex 挤成竖排 */
.activity-hero { flex-wrap: wrap; }
.hero-content { min-width: 0; flex: 1 1 320px; }
.hero-content h1 { overflow-wrap: anywhere; }
.detail-page-container { width: 100%; min-width: 0; }
.main-body, .grid-section { min-width: 0; }
</style>
