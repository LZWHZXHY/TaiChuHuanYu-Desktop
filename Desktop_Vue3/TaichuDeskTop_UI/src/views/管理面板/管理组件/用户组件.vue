<template>
  <div class="user-governance-hub" v-loading="loading">
    <header class="module-header">
      <div class="filter-panel">
        <input 
          v-model="searchQuery" 
          @keyup.enter="handleSearch"
          type="text" 
          placeholder="搜索 GUID、用户名、邮箱或手机号 (回车确认)..." 
          class="ink-input search-bar"
        />
        <select v-model="filterPermission" class="ink-select">
          <option value="">所有系统权限</option>
          <option v-for="(label, key) in PERMISSION_MAP" :key="key" :value="key">
            {{ label }}
          </option>
        </select>
        <select v-model="filterReputation" class="ink-select">
          <option value="">所有信誉等级</option>
          <option value="low">信誉受损 (&lt; 90)</option>
          <option value="normal">信誉良好 (90 - 100)</option>
        </select>
      </div>
      <button class="btn-refresh" @click="handleSearch" :disabled="loading">
        {{ loading ? '数据同步中...' : '刷新中枢数据' }}
      </button>
    </header>

    <div class="table-card">
      <div class="table-responsive">
        <table class="ink-table">
          <thead>
            <tr>
              <th width="100">用户 GUID</th>
              <th width="160" class="sortable" @click="toggleSort('createdAt')">
                基本凭证 / 注册时间
                <span class="sort-icon" v-show="sortField === 'createdAt'">{{ sortOrder === 'asc' ? '↑' : '↓' }}</span>
              </th>
              <th width="140" class="sortable" @click="toggleSort('experience')">
                等级 / 经验值
                <span class="sort-icon" v-show="sortField === 'experience'">{{ sortOrder === 'asc' ? '↑' : '↓' }}</span>
              </th>
              <th width="110" class="sortable" @click="toggleSort('reputation')">
                信誉资产
                <span class="sort-icon" v-show="sortField === 'reputation'">{{ sortOrder === 'asc' ? '↑' : '↓' }}</span>
              </th>
              <th width="200">配额负载 (已用/最大)</th>
              <th width="160">特权绑定</th>
              <th width="140" class="text-right">介入治理</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="loading && userList.length === 0">
              <td colspan="7" class="empty-cell">太初数据库同步中...</td>
            </tr>
            <tr v-else-if="userList.length === 0">
              <td colspan="7" class="empty-cell">未检索到匹配的用户实体</td>
            </tr>
            
            <tr v-for="user in userList" :key="user.id" class="data-row">
              <td class="mono font-sm" :title="user.id">#{{ user.id.substring(0, 8) }}</td>
              
              <td>
                <div class="user-base-info">
                  <span class="username">{{ user.username }}<span v-if="user.stats?.title" class="user-title-chip" title="正在戴的头衔">{{ user.stats.title }}</span></span>
                  <span class="email" v-if="user.email">{{ user.email }}</span>
                  <span class="date-hint">{{ formatDate(user.createdAt) }} 注册</span>
                </div>
              </td>
              
              <td>
                <div class="level-badge-group">
                  <span class="level-tag">Lv.{{ user.stats?.level ?? 0 }}</span>
                  <span class="mono exp-value">{{ user.stats?.experience ?? 0 }} EXP</span>
                </div>
              </td>
              
              <td>
                <span :class="['reputation-text', getReputationClass(user.stats?.reputation ?? 100)]">
                  🛡️ {{ user.stats?.reputation ?? 100 }}
                </span>
              </td>
              
              <td>
                <div class="quota-matrix">
                  <div class="quota-item">
                    <span>空间:</span>
                    <span class="mono">{{ user.stats?.usedSpaces ?? 0 }}/<b>{{ user.stats?.maxSpaces ?? 1 }}</b></span>
                  </div>
                  <div class="quota-item">
                    <span>笔记:</span>
                    <span class="mono">{{ user.stats?.usedNotes ?? 0 }}/<b>{{ user.stats?.maxNotes ?? 100 }}</b></span>
                  </div>
                  <div class="quota-item">
                    <span>项目上限:</span>
                    <span class="mono"><b>{{ user.stats?.maxProjectCount ?? 10 }}</b> 个</span>
                  </div>
                </div>
              </td>
              
              <td>
                <div class="permission-tags">
                  <span v-if="user.permissions.length === 0" class="p-badge guest">普通用户</span>
                  <span 
                    v-for="p in user.permissions" 
                    :key="p" 
                    :class="['p-badge', p.toLowerCase()]"
                  >
                    {{ PERMISSION_MAP[p] || p }}
                  </span>
                </div>
              </td>
              
              <td class="text-right actions">
                <button class="btn-action edit" @click="handleOpenGovernance(user)">微调参数</button>
                <button class="btn-action danger" @click="handlePunishReputation(user)">违规扣分</button>
                <button class="btn-action edit" title="授予 / 收回头衔" @click="titleUser = user">头衔</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      
      <div class="pagination-footer" v-if="totalCount > 0">
        <span class="page-info">共 {{ totalCount }} 条数据，每页展示 {{ pageSize }} 条</span>
        <div class="page-controls">
          <button class="btn-page" :disabled="currentPage === 1" @click="changePage(currentPage - 1)">上一页</button>
          <span class="current-page">{{ currentPage }} / {{ totalPages }}</span>
          <button class="btn-page" :disabled="currentPage >= totalPages" @click="changePage(currentPage + 1)">下一页</button>
        </div>
      </div>
    </div>

    <Teleport to="body">
      <div v-if="showGovModal" class="modal-mask" @click.self="closeModal">
        <div class="modal-container scroll-y workbench-skin editor-surface community-surface tone-admin">
          <header class="modal-header studio-heading">
            <div>
              <h3>太初域用户深度治理中枢</h3>
              <p class="mono font-sm">GUID: {{ targetUser?.id }}</p>
            </div>
            <button class="close-icon" @click="closeModal">×</button>
          </header>

          <div class="modal-body">
            <fieldset class="gov-fieldset">
              <legend>只读用户画像 (Profile 映射)</legend>
              <div class="profile-grid-readonly">
                <div class="p-cell"><span>年龄:</span> <b>{{ targetUser?.profile?.age ?? '0' }} 岁</b></div>
                <div class="p-cell"><span>星轨星座:</span> <b>{{ targetUser?.profile?.zodiac ?? '未知' }}</b></div>
                <div class="p-cell"><span>华夏生肖:</span> <b>{{ targetUser?.profile?.chineseZodiac ?? '未知' }}</b></div>
                <div class="p-cell"><span>绑定手机:</span> <b>{{ targetUser?.profile?.phoneNumber || '未绑定' }}</b></div>
                <div class="p-cell full"><span>自我介绍:</span> <p class="bio-text">{{ targetUser?.profile?.bio || '暂无介绍' }}</p></div>
              </div>
            </fieldset>

            <fieldset class="gov-fieldset">
              <legend>核心统计指标与资源配额修改 (Stats 映射)</legend>
              <div class="form-grid">
                <div class="field">
                  <label>信誉评分 (Reputation)</label>
                  <input type="number" v-model.number="statsForm.reputation" max="100" min="0" />
                </div>
                <div class="field">
                  <label>核心经验值 (Experience)</label>
                  <input type="number" v-model.number="statsForm.experience" />
                </div>
                <div class="field">
                  <label>最大存储空间边界 (MaxSpaces)</label>
                  <input type="number" v-model.number="statsForm.maxSpaces" />
                </div>
                <div class="field">
                  <label>最大笔记容量阈值 (MaxNotes)</label>
                  <input type="number" v-model.number="statsForm.maxNotes" />
                </div>
                <div class="field full">
                  <label>最大项目承载数 (MaxProjectCount)</label>
                  <input type="number" v-model.number="statsForm.maxProjectCount" />
                </div>
              </div>
            </fieldset>

            <fieldset class="gov-fieldset">
              <legend>系统管理凭证指派 (UserPermission 映射)</legend>
              <div class="permission-checkbox-group">
                <label v-for="(label, key) in PERMISSION_MAP" :key="key" class="checkbox-label">
                  <input 
                    type="checkbox" 
                    :value="key" 
                    v-model="permissionForm"
                  />
                  <div class="custom-checkbox-box">
                    <span class="p-title">{{ label }}</span>
                    <span class="mono font-xs p-key">{{ key }}</span>
                  </div>
                </label>
              </div>
            </fieldset>
          </div>

          <footer class="modal-footer">
            <button class="btn-cancel" @click="closeModal">放弃变动</button>
            <button class="btn-submit" @click="submitGovernance" :disabled="submitting">
              {{ submitting ? '指令写入中...' : '提交指令并更新太初域' }}
            </button>
          </footer>
        </div>
      </div>
    </Teleport>
    <TitleGrantDialog v-if="titleUser" :user="titleUser" @close="titleUser = null" @changed="onTitleChanged" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { adminUserApi, type UserDto } from '@/api/Admin/AdminUser';
import TitleGrantDialog from './TitleGrantDialog.vue';

const PERMISSION_MAP: Record<string, string> = {
  SuperAdmin: '核心中枢 (所有权限)',
  Trade_Manage: '交易行管理员',
  User_Audit: '用户审计师',
  Wiki_Editor: '维基知识库审核员',
  System_Monitor: '系统负载监控员',
  Survey_Manage: '问卷管理员', // ✅ 新增
  JointManager:'联合管理员'
};

const loading = ref(false);
const submitting = ref(false);
const showGovModal = ref(false);
// 头衔：给谁授予 / 收回
const titleUser = ref<UserDto | null>(null);
function onTitleChanged(equipped: string | null) {
  const u = titleUser.value;
  if (u?.stats) u.stats.title = equipped;
}

const userList = ref<UserDto[]>([]);
const targetUser = ref<UserDto | null>(null);

// 服务端分页、搜索与排序状态
const currentPage = ref(1);
const pageSize = ref(30);
const totalCount = ref(0);
const totalPages = computed(() => Math.ceil(totalCount.value / pageSize.value) || 1);

const searchQuery = ref('');
const filterPermission = ref('');
const filterReputation = ref('');

// 🌟 排序状态
const sortField = ref('createdAt');
const sortOrder = ref<'asc' | 'desc'>('desc');

// 表单控制状态
const statsForm = ref({
  reputation: 100,
  experience: 0,
  maxSpaces: 1,
  maxNotes: 100,
  maxProjectCount: 10
});
const permissionForm = ref<string[]>([]);

// 发起服务端请求
const fetchUsers = async () => {
  loading.value = true;
  try {
    // ⚠️ 注意：你的 api 接口参数定义里需要补上 sortBy 和 isDesc
    const res = await adminUserApi.getUsers({
      page: currentPage.value,
      pageSize: pageSize.value,
      search: searchQuery.value.trim() || undefined,
      permission: filterPermission.value || undefined,
      reputation: filterReputation.value || undefined,
      sortBy: sortField.value,                     // 传给后端的排序字段
      isDesc: sortOrder.value === 'desc'           // 传给后端的升降序布尔值
    } as any); // 如果 AdminUser.ts 类型报错，可用 as any 临时规避或去补充接口声明

    userList.value = res.items;
    totalCount.value = res.totalCount;
  } catch (error) {
    console.error('拉取用户分页数据失败', error);
  } finally {
    loading.value = false;
  }
};

onMounted(fetchUsers);

// 🌟 表头点击排序逻辑
const toggleSort = (field: string) => {
  if (sortField.value === field) {
    // 如果已经是该字段，则切换升降序
    sortOrder.value = sortOrder.value === 'asc' ? 'desc' : 'asc';
  } else {
    // 切换了新字段，默认先降序排列（经验、信誉、时间通常降序更合理）
    sortField.value = field;
    sortOrder.value = 'desc';
  }
  handleSearch(); // 重置第一页并拉取
};

// 触发检索行为 (重置回第一页)
const handleSearch = () => {
  currentPage.value = 1;
  fetchUsers();
};

// 当下拉框发生变化时，自动触发检索
watch([filterPermission, filterReputation], () => {
  handleSearch();
});

// 分页器翻页控制
const changePage = (page: number) => {
  if (page < 1 || page > totalPages.value) return;
  currentPage.value = page;
  fetchUsers();
};

// ...下面的 handleOpenGovernance / handlePunishReputation 等方法完全不变 ...
const handleOpenGovernance = (user: UserDto) => {
  targetUser.value = user;
  permissionForm.value = [...user.permissions];
  
  if (user.stats) {
    statsForm.value = {
      reputation: user.stats.reputation,
      experience: user.stats.experience,
      maxSpaces: user.stats.maxSpaces,
      maxNotes: user.stats.maxNotes,
      maxProjectCount: user.stats.maxProjectCount
    };
  }
  showGovModal.value = true;
};

const closeModal = () => {
  showGovModal.value = false;
  targetUser.value = null;
};

const handlePunishReputation = async (user: UserDto) => {
  if (!user.stats) return;
  if (!confirm(`确定认定用户【${user.username}】存在违规行为并扣除 15 点信誉分吗？`)) return;
  
  try {
    await adminUserApi.punish(user.id, 15);
    user.stats.reputation = Math.max(0, user.stats.reputation - 15);
  } catch (error) {
    console.error('违规扣分执行失败', error);
  }
};

const submitGovernance = async () => {
  if (!targetUser.value) return;
  submitting.value = true;
  
  try {
    await Promise.all([
      adminUserApi.updateStats(targetUser.value.id, statsForm.value),
      adminUserApi.updatePermissions(targetUser.value.id, permissionForm.value)
    ]);
    
    if (targetUser.value.stats) {
      Object.assign(targetUser.value.stats, statsForm.value);
      targetUser.value.stats.level = Math.floor(Math.sqrt(statsForm.value.experience / 100.0));
    }
    targetUser.value.permissions = [...permissionForm.value];
    
    closeModal();
  } catch (error) {
    console.error('深层指令写入失败', error);
  } finally {
    submitting.value = false;
  }
};

const getReputationClass = (rep: number) => rep >= 90 ? 'reputation-good' : 'reputation-bad';
const formatDate = (dateStr: string) => {
  if (!dateStr) return '未知时间';
  return new Date(dateStr).toLocaleDateString('zh-CN', { year: 'numeric', month: '2-digit', day: '2-digit' });
};
</script>

<style scoped>
.user-governance-hub { display: flex; flex-direction: column; gap: 24px; animation: slideIn 0.35s cubic-bezier(0.16, 1, 0.3, 1); }
.module-header { display: flex; justify-content: space-between; align-items: center; gap: 20px; }
.filter-panel { display: flex; gap: 12px; flex: 1; max-width: 900px; }
.search-bar { flex: 2; }

/* 统一太初扁平化输入框 */
.ink-input, .ink-select { border: 1px solid var(--border-line); padding: 10px 14px; border-radius: 0; font-size: 0.85rem; outline: none; background: var(--paper-card); transition: 0.25s; }
.ink-input:focus, .ink-select:focus { border-color: var(--border-line); }
.ink-select { cursor: pointer; flex: 1; }

.btn-refresh { background: var(--paper-card); border: 1px solid var(--border-line); padding: 10px 20px; border-radius: 0; color: var(--text-secondary); cursor: pointer; font-weight: 500; font-size: 0.85rem; }
.btn-refresh:hover { border-color: var(--border-line); color: var(--text-primary); }

/* 线条极简高留白表格结构 */
.table-card { background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 0; box-shadow: 0 4px 20px rgba(0,0,0,0.01); }
.table-responsive { width: 100%; overflow-x: auto; }
.ink-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 0.88rem; }
.ink-table th { padding: 16px; background: var(--paper-card); color: var(--text-secondary); font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.5px; border-bottom: 2px solid var(--border-line); }
.ink-table td { padding: 16px; border-bottom: 1px solid var(--border-line); vertical-align: middle; }
.data-row:hover td { background-color: var(--bg-main); }

/* 🌟 表头排序专用样式 */
.sortable { cursor: pointer; user-select: none; transition: 0.2s; }
.sortable:hover { color: var(--text-primary); background: var(--bg-sub); }
.sort-icon { display: inline-block; margin-left: 4px; font-weight: bold; color: var(--text-primary); font-size: 0.8rem; }

/* 单元格微型组件排版 */
.user-base-info { display: flex; flex-direction: column; gap: 2px; }
.user-base-info .username { font-weight: 700; color: var(--text-primary); }
.user-base-info .email { font-size: 0.75rem; color: var(--text-secondary); }
.user-base-info .date-hint { font-size: 0.7rem; color: var(--text-secondary); }

.level-badge-group { display: flex; align-items: center; gap: 8px; }
.level-tag { background: var(--text-primary); color: var(--paper-card); font-size: 0.7rem; font-weight: 800; padding: 2px 6px; border-radius: 0; font-family: monospace; }
.exp-value { color: var(--text-secondary); font-size: 0.8rem; }

.reputation-text { font-weight: 600; font-size: 0.85rem; padding: 4px 8px; border-radius: 0; }
.reputation-good { background: var(--success-soft); color: var(--success); }
.reputation-bad { background: var(--danger-soft); color: var(--danger); font-weight: 800; }

.quota-matrix { display: flex; flex-direction: column; gap: 4px; font-size: 0.8rem; }
.quota-item { display: flex; gap: 6px; color: var(--text-secondary); }
.quota-item b { color: var(--text-primary); }

.permission-tags { display: flex; flex-wrap: wrap; gap: 4px; }
.p-badge { font-size: 0.7rem; padding: 2px 6px; border-radius: 0; font-weight: 600; background: var(--bg-sub); color: var(--text-secondary); }
.p-badge.superadmin { background: var(--bg-sub); color: var(--danger); border: 1px solid var(--danger-soft); }
.p-badge.wiki_editor { background: var(--success-soft); color: var(--success); }
.p-badge.trade_manage { background: var(--bg-main); color: var(--accent-color); }
.p-badge.guest { background: var(--bg-sub); color: var(--text-secondary); font-weight: 400; }

.btn-action { background: none; border: none; font-size: 0.8rem; font-weight: 700; cursor: pointer; margin-left: 12px; padding: 0; }
.btn-action.edit { color: var(--accent-color); }
.btn-action.danger { color: var(--danger); }
.btn-action:hover { text-decoration: underline; }
.empty-cell { text-align: center; padding: 60px !important; color: var(--text-secondary); font-style: italic; }

/* 分页器样式 */
.pagination-footer { display: flex; justify-content: space-between; align-items: center; padding: 16px 20px; border-top: 1px solid var(--border-line); background: var(--bg-main); border-radius: 0; }
.page-info { font-size: 0.8rem; color: var(--text-secondary); }
.page-controls { display: flex; align-items: center; gap: 12px; }
.btn-page { background: var(--paper-card); border: 1px solid var(--border-line); padding: 6px 14px; font-size: 0.8rem; border-radius: 0; cursor: pointer; transition: 0.2s; }
.btn-page:not(:disabled):hover { border-color: var(--border-line); color: var(--text-primary); }
.btn-page:disabled { opacity: 0.4; cursor: not-allowed; background: var(--bg-sub); }
.current-page { font-size: 0.85rem; font-family: monospace; font-weight: 600; color: var(--text-primary); }

/* 沉浸式治理弹窗容器 */
.modal-mask { position: fixed; inset: 0; background: var(--overlay); backdrop-filter: blur(12px); z-index: 9999; display: flex; justify-content: center; align-items: center; }
.modal-container { background: var(--paper-card); border: 1px solid var(--border-line); width: 100%; max-width: 650px; padding: 40px; box-shadow: 25px 25px 0 rgba(0,0,0,0.05); 
  max-height: 90dvh; overflow-y: auto; border-radius: 0;
}
.modal-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; border-bottom: 1px solid var(--border-line); padding-bottom: 16px; }
.modal-header h3 { font-size: 1.4rem; font-weight: 200; margin: 0; letter-spacing: 0.5px; }
.modal-header p { margin: 4px 0 0 0; color: var(--text-secondary); }
.close-icon { background: none; border: none; font-size: 1.8rem; cursor: pointer; color: var(--text-secondary); line-height: 1; }
.close-icon:hover { color: var(--text-primary); }
.scroll-y { max-height: 85vh; overflow-y: auto; }

/* 治理专用表单组 */
.gov-fieldset { border: 1px solid var(--border-line); margin-bottom: 24px; padding: 20px; border-radius: 0; }
.gov-fieldset legend { font-size: 0.75rem; text-transform: uppercase; font-weight: 800; color: var(--text-secondary); padding: 0 8px; letter-spacing: 0.5px; }

.profile-grid-readonly { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; font-size: 0.85rem; }
.profile-grid-readonly .p-cell { display: flex; gap: 8px; color: var(--text-secondary); }
.profile-grid-readonly .p-cell b { color: var(--text-primary); }
.profile-grid-readonly .p-cell.full { grid-column: span 2; flex-direction: column; gap: 4px; }
.bio-text { background: var(--paper-card); padding: 10px; border: 1px dashed var(--border-line); margin: 0; font-size: 0.8rem; color: var(--text-secondary); line-height: 1.5; }

.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
.field { display: flex; flex-direction: column; gap: 6px; }
.field.full { grid-column: span 2; }
.field label { font-size: 0.7rem; text-transform: uppercase; color: var(--text-secondary); font-weight: 700; }
.field input { border: 1px solid var(--border-line); padding: 10px; font-size: 0.9rem; outline: none; }
.field input:focus { border-color: var(--border-line); }

/* 核心细粒度多选权限矩阵 */
.permission-checkbox-group { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.checkbox-label { cursor: pointer; position: relative; }
.checkbox-label input { position: absolute; opacity: 0; width: 0; height: 0; }
.custom-checkbox-box { border: 1px solid var(--border-line); padding: 12px; border-radius: 0; display: flex; flex-direction: column; gap: 2px; transition: 0.2s; }
.checkbox-label input:checked + .custom-checkbox-box { border-color: var(--border-line); background: var(--bg-main); box-shadow: inset 0 0 0 1px var(--accent-color); }
.custom-checkbox-box .p-title { font-size: 0.85rem; font-weight: 700; color: var(--text-primary); }
.custom-checkbox-box .p-key { color: var(--text-secondary); }

.modal-footer { display: flex; justify-content: flex-end; gap: 14px; border-top: 1px solid var(--border-line); padding-top: 20px; margin-top: 10px; }
.btn-cancel { background: none; border: 1px solid var(--border-line); padding: 12px 24px; cursor: pointer; color: var(--text-secondary); font-size: 0.85rem; }
.btn-cancel:hover { background: var(--paper-card); }
.btn-submit { background: var(--accent-color); color: var(--on-accent); border: none; padding: 12px 30px; font-weight: 700; cursor: pointer; font-size: 0.85rem; }
.btn-submit:disabled { opacity: 0.5; cursor: not-allowed; }

.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }
.font-sm { font-size: 0.8rem; }
.font-xs { font-size: 0.7rem; }

@keyframes slideIn { from { opacity: 0; transform: translateY(8px); } to { opacity: 1; transform: translateY(0); } }
.user-title-chip { display: inline-block; margin-left: 6px; padding: 0 5px; border: 1px solid var(--accent-color); color: var(--accent-color); font-size: 11px; font-weight: 600; line-height: 16px; vertical-align: middle; }
</style>