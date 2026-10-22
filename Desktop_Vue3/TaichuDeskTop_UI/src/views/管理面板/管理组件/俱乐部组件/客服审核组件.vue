<template>
  <div class="panel">
    <div class="panel-toolbar">
      <div class="filter-row">
        <input
          v-model="search"
          @keyup.enter="loadList"
          class="ink-input"
          placeholder="搜索客服名称 / 微信 / GUID (回车确认)..."
        />
        <select v-model="statusFilter" class="ink-select" @change="loadList">
          <option value="pending">待审核</option>
          <option value="approved">已通过</option>
          <option value="rejected">已拒绝</option>
          <option value="banned">已封禁</option>
          <option value="all">全部</option>
        </select>
      </div>
      <button class="btn-refresh" @click="loadList" :disabled="loading">
        {{ loading ? '同步中...' : '刷新列表' }}
      </button>
    </div>

    <!-- 统计条 -->
    <div class="stats-bar" v-if="items.length > 0">
      <span class="stat"><i class="sdot pending"></i> 当前筛选 <b>{{ items.length }}</b> 条</span>
      <span class="stat"><i class="sdot confirmed"></i> 显示总数 <b>{{ total }}</b></span>
    </div>

    <!-- 卡片列表 -->
    <div class="operator-grid">
      <div v-for="cs in items" :key="cs.id" class="operator-card">
        <div class="op-head">
          <div class="op-avatar">{{ (cs.name || '?').substring(0, 1) }}</div>
          <div class="op-meta">
            <div class="op-name">{{ cs.name || '未命名客服' }}</div>
            <div class="op-contact mono">微信：{{ cs.wechat }}</div>
          </div>
          <span :class="['status-badge', cs.auditStatus]">
            {{ STATUS_LABEL[cs.auditStatus] || cs.auditStatus }}
          </span>
        </div>

        <div class="op-reputation">
          <span>载单</span>
          <b>{{ cs.currentLoad }} / {{ cs.capacity }}</b>
          <span class="divider">|</span>
          <span>评分</span>
          <b :class="cs.reviewCount > 0 ? 'good' : ''">
            {{ cs.reviewCount > 0 ? cs.avgOverall.toFixed(1) : '暂无' }}
          </b>
          <span v-if="cs.reviewCount > 0" class="review-count">
            ({{ cs.reviewCount }} 条)
          </span>
        </div>

        <div class="op-info-rows">
          <div class="info-row">
            <span>工作时间</span>
            <b>{{ cs.onlineTime || '—' }}</b>
          </div>
          <div class="info-row">
            <span>申请时间</span>
            <b class="mono">{{ fmtTime(cs.appliedAt) }}</b>
          </div>
        </div>

        <div class="op-actions">
          <button class="btn-text" @click="openDetail(cs)">详情 / 审核</button>
        </div>
      </div>

      <div v-if="items.length === 0 && !loading" class="empty-hint">
        没有符合条件的客服
      </div>
    </div>

    <!-- 分页 -->
    <div v-if="total > 0" class="pagination">
      <span class="page-info">共 {{ total }} 条</span>
      <button class="btn-page" :disabled="page === 1" @click="changePage(page - 1)">上一页</button>
      <span class="page-cur">{{ page }} / {{ totalPages }}</span>
      <button class="btn-page" :disabled="page >= totalPages" @click="changePage(page + 1)">下一页</button>
    </div>

    <!-- 详情 / 审核弹窗 -->
    <Teleport to="body">
      <div v-if="showModal" class="modal-mask">
        <div class="modal-container scroll-y">
          <header class="modal-header">
            <div>
              <h3>{{ target?.name || '客服详情' }}</h3>
              <p class="mono font-sm">GUID: {{ target?.userId }}</p>
            </div>
            <button class="close-icon" @click="closeModal">×</button>
          </header>

          <fieldset class="gov-fieldset">
            <legend>基础档案</legend>
            <div class="profile-grid">
              <div class="p-cell"><span>客服名称:</span> <b>{{ target?.name }}</b></div>
              <div class="p-cell"><span>客服微信:</span> <b class="mono">{{ target?.wechat }}</b></div>
              <div class="p-cell"><span>工作时间:</span> <b>{{ target?.onlineTime || '—' }}</b></div>
              <div class="p-cell"><span>接待容量:</span> <b>{{ target?.capacity || 10 }} 单</b></div>
              <div class="p-cell"><span>当前载单:</span> <b>{{ target?.currentLoad || 0 }}</b></div>
              <div class="p-cell"><span>评分:</span>
                <b class="accent">
                  {{ (target?.reviewCount || 0) > 0 ? target?.avgOverall.toFixed(1) : '暂无' }}
                  <span v-if="(target?.reviewCount || 0) > 0" class="review-count">
                    ({{ target?.reviewCount }} 条)
                  </span>
                </b>
              </div>
              <div class="p-cell full" v-if="target?.intro">
                <span>自我介绍:</span>
                <p class="bio-text">{{ target.intro }}</p>
              </div>
              <div class="p-cell full" v-if="target?.auditNote">
                <span>历史审核备注:</span>
                <p class="bio-text warn">{{ target.auditNote }}</p>
              </div>
            </div>
          </fieldset>

          <fieldset class="gov-fieldset">
            <legend>审核操作</legend>
            <div class="form-grid">
              <div class="field">
                <label>审核结论</label>
                <select v-model="auditForm.action" class="ink-input" :disabled="!canAudit">
                  <option value="approve">✓ 通过</option>
                  <option value="reject">✗ 拒绝</option>
                  <option value="ban">⊘ 封禁</option>
                </select>
              </div>
              <div class="field">
                <label>接待容量（仅通过时生效）</label>
                <input
                  type="number"
                  v-model.number="auditForm.capacity"
                  class="ink-input"
                  min="1"
                  max="100"
                  :disabled="auditForm.action !== 'approve' || !canAudit"
                />
              </div>
              <div class="field full">
                <label>{{ noteLabel }}</label>
                <textarea
                  v-model="auditForm.note"
                  class="ink-input"
                  rows="3"
                  :placeholder="notePlaceholder"
                  :disabled="!canAudit"
                ></textarea>
              </div>
            </div>
          </fieldset>

          <footer class="modal-footer">
            <button class="btn-cancel" @click="closeModal">关闭</button>
            <button
              v-if="canAudit"
              class="btn-submit"
              @click="submitAudit"
              :disabled="saving"
            >
              {{ saving ? '提交中...' : submitLabel }}
            </button>
          </footer>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue';
import request from '@/utils/request';

interface CsItem {
  id: string;
  userId: string;
  username: string;
  email: string;
  name: string;
  wechat: string;
  onlineTime: string;
  intro?: string;
  capacity: number;
  status: string;
  currentLoad: number;
  isAssignable: boolean;
  isActive: boolean;
  auditStatus: string;
  auditNote?: string;
  appliedAt?: string | null;
  reviewedAt?: string | null;
  reviewCount: number;
  avgOverall: number;
}

const STATUS_LABEL: Record<string, string> = {
  pending: '待审核',
  approved: '已通过',
  rejected: '已拒绝',
  banned: '已封禁',
};

const items = ref<CsItem[]>([]);
const loading = ref(false);
const search = ref('');
const statusFilter = ref('pending');
const page = ref(1);
const pageSize = 20;
const total = ref(0);

const showModal = ref(false);
const saving = ref(false);
const target = ref<CsItem | null>(null);

const auditForm = reactive({
  action: 'approve' as 'approve' | 'reject' | 'ban',
  capacity: 10,
  note: '',
});

const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)));

const canAudit = computed(() => {
  if (!target.value) return false;
  // pending 可以审核；已拒绝可以重新审核；已通过不能再审
  return ['pending', 'rejected'].includes(target.value.auditStatus);
});

const noteLabel = computed(() => ({
  approve: '审核备注（可选）',
  reject: '拒绝原因（必填）',
  ban: '封禁原因（必填）',
}[auditForm.action]));

const notePlaceholder = computed(() => ({
  approve: '例如：经验丰富，通过',
  reject: '例如：微信信息不完整，请补充后重新提交',
  ban: '例如：违规操作，永久封禁',
}[auditForm.action]));

const submitLabel = computed(() => ({
  approve: '确认通过',
  reject: '确认拒绝',
  ban: '确认封禁',
}[auditForm.action]));

const fmtTime = (iso?: string | null) => {
  if (!iso) return '—';
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

const loadList = async () => {
  loading.value = true;
  try {
    const res: any = await request.get('/admin/club/cs-applications', {
      params: {
        status: statusFilter.value || 'pending',
        search: search.value.trim() || undefined,
        page: page.value,
        pageSize,
      }
    });
    const payload = res?.data ?? res;
    items.value = payload?.items ?? [];
    total.value = payload?.total ?? 0;
  } catch (e) {
    console.error('拉取客服列表失败', e);
    items.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
};

const openDetail = (cs: CsItem) => {
  target.value = JSON.parse(JSON.stringify(cs));
  auditForm.action = 'approve';
  auditForm.capacity = cs.capacity || 10;
  auditForm.note = '';
  showModal.value = true;
};

const closeModal = () => {
  showModal.value = false;
  target.value = null;
};

const submitAudit = async () => {
  if (!target.value) return;

  // 校验
  if (auditForm.action === 'reject' && !auditForm.note.trim()) {
    alert('请填写拒绝原因');
    return;
  }
  if (auditForm.action === 'ban' && !auditForm.note.trim()) {
    alert('请填写封禁原因');
    return;
  }

  const confirmMsg = {
    approve: `确认通过客服【${target.value.name}】的申请？`,
    reject:  `确认拒绝客服【${target.value.name}】的申请？`,
    ban:     `确认封禁客服【${target.value.name}】？此操作会下线该客服。`,
  }[auditForm.action];

  if (!confirm(confirmMsg)) return;

  saving.value = true;
  try {
    if (auditForm.action === 'approve') {
      await request.post(`/admin/club/cs-application/${target.value.id}/approve`, {
        note: auditForm.note.trim() || null,
        capacity: auditForm.capacity > 0 ? auditForm.capacity : 10,
      });
    } else if (auditForm.action === 'reject') {
      await request.post(`/admin/club/cs-application/${target.value.id}/reject`, {
        reason: auditForm.note.trim(),
      });
    } else if (auditForm.action === 'ban') {
      await request.post(`/admin/club/cs-application/${target.value.id}/ban`, {
        reason: auditForm.note.trim(),
      });
    }

    await loadList();
    closeModal();
  } catch (e: any) {
    alert(e?.response?.data?.message || e.message || '操作失败');
  } finally {
    saving.value = false;
  }
};

const changePage = (p: number) => {
  if (p < 1 || p > totalPages.value) return;
  page.value = p;
  loadList();
};

onMounted(loadList);
</script>

<style scoped>
.panel { animation: fadeIn 0.3s ease; }

.panel-toolbar {
  display: flex; justify-content: space-between; align-items: center;
  margin-bottom: 16px; gap: 16px;
}
.filter-row { display: flex; gap: 12px; flex: 1; max-width: 720px; }

.ink-input, .ink-select {
  border: 1px solid var(--border-line); padding: 10px 14px; border-radius: 4px;
  font-size: 0.85rem; outline: none; background: var(--paper-card);
  font-family: inherit;
}
.ink-input:focus, .ink-select:focus { border-color: var(--border-line); }
.ink-select { cursor: pointer; min-width: 140px; }

.btn-refresh {
  background: var(--paper-card); border: 1px solid var(--border-line); padding: 10px 20px;
  border-radius: 4px; color: var(--text-secondary); cursor: pointer; font-size: 0.85rem;
}
.btn-refresh:hover { border-color: var(--border-line); color: var(--text-primary); }
.btn-refresh:disabled { opacity: 0.5; cursor: not-allowed; }

.stats-bar {
  display: flex; flex-wrap: wrap; gap: 24px;
  padding: 12px 20px; background: var(--bg-main); border-radius: 6px;
  font-size: 0.85rem; margin-bottom: 16px;
}
.stat { display: flex; align-items: center; gap: 8px; color: var(--text-secondary); }
.stat b { color: var(--text-primary); font-weight: 700; }
.sdot { width: 8px; height: 8px; border-radius: 50%; }
.sdot.pending { background: var(--warning); }
.sdot.confirmed { background: var(--success); }

.operator-grid {
  display: grid; grid-template-columns: repeat(auto-fill, minmax(360px, 1fr));
  gap: 16px;
}
.operator-card {
  background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 10px;
  padding: 20px; display: flex; flex-direction: column; gap: 14px;
  transition: 0.25s;
}
.operator-card:hover { border-color: var(--border-line); box-shadow: 0 6px 24px rgba(0,0,0,0.04); }

.op-head { display: flex; align-items: center; gap: 12px; }
.op-avatar {
  width: 42px; height: 42px; border-radius: 50%;
  background: var(--text-primary); color: var(--paper-card); display: flex;
  align-items: center; justify-content: center;
  font-weight: 700; font-size: 1.1rem;
  flex-shrink: 0;
}
.op-meta { flex: 1; min-width: 0; }
.op-name { font-weight: 700; color: var(--text-primary); font-size: 0.95rem; }
.op-contact { font-size: 0.72rem; color: var(--text-secondary); margin-top: 2px; }

.op-reputation {
  display: flex; align-items: center; gap: 8px; font-size: 0.8rem; color: var(--text-secondary);
  flex-wrap: wrap;
}
.op-reputation b { color: var(--text-primary); }
.op-reputation .good { color: var(--success); }
.op-reputation .divider { color: var(--text-secondary); }
.review-count { font-size: 0.72rem; color: var(--text-secondary); }

.op-info-rows {
  display: flex; flex-direction: column; gap: 6px;
  padding: 10px 12px; background: var(--bg-main); border-radius: 4px;
}
.info-row {
  display: flex; justify-content: space-between;
  font-size: 0.8rem;
}
.info-row span { color: var(--text-secondary); }
.info-row b { color: var(--text-primary); font-weight: 600; }

.op-actions { text-align: right; border-top: 1px solid var(--border-line); padding-top: 12px; }

.status-badge {
  font-size: 0.7rem; padding: 3px 8px; border-radius: 4px; font-weight: 600;
}
.status-badge.pending  { background: var(--warning-soft); color: var(--warning); }
.status-badge.approved { background: var(--success-soft); color: var(--success); }
.status-badge.rejected { background: var(--danger-soft); color: var(--danger); }
.status-badge.banned   { background: var(--text-primary); color: var(--paper-card); }

.empty-hint { font-size: 0.85rem; color: var(--text-secondary); padding: 30px; text-align: center; grid-column: 1 / -1; }

.btn-text {
  background: none; border: none; color: var(--accent-color); cursor: pointer;
  font-size: 0.8rem; font-weight: 700; padding: 0;
}
.btn-text:hover { text-decoration: underline; }

.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }
.font-sm { font-size: 0.8rem; }

/* Pagination */
.pagination {
  display: flex; justify-content: flex-end; align-items: center;
  gap: 12px; font-size: 0.85rem; color: var(--text-secondary);
  margin-top: 20px;
}
.page-info { margin-right: auto; }
.page-cur { font-family: monospace; }
.btn-page {
  background: var(--paper-card); border: 1px solid var(--border-line); padding: 6px 14px;
  border-radius: 4px; cursor: pointer; font-size: 0.8rem;
}
.btn-page:not(:disabled):hover { border-color: var(--border-line); }
.btn-page:disabled { opacity: 0.4; cursor: not-allowed; }

/* Modal */
.modal-mask {
  position: fixed; inset: 0; background: var(--overlay);
  backdrop-filter: blur(12px); z-index: 9999;
  display: flex; justify-content: center; align-items: center;
}
.modal-container {
  background: var(--paper-card); border: 1px solid var(--border-line); width: 100%; max-width: 640px;
  padding: 32px; box-shadow: 25px 25px 0 rgba(0,0,0,0.05);
  border-radius: 4px;

  max-height: 90dvh; overflow-y: auto; border-radius: var(--radius-md);
}
.modal-container.scroll-y { max-height: 85vh; overflow-y: auto; }
.modal-header {
  display: flex; justify-content: space-between; align-items: flex-start;
  margin-bottom: 24px; border-bottom: 1px solid var(--border-line); padding-bottom: 16px;
}
.modal-header h3 { font-size: 1.25rem; font-weight: 600; margin: 0; }
.modal-header p { margin: 4px 0 0; color: var(--text-secondary); }
.close-icon { background: none; border: none; font-size: 1.8rem; cursor: pointer; color: var(--text-secondary); line-height: 1; }
.close-icon:hover { color: var(--text-primary); }

.gov-fieldset {
  border: 1px solid var(--border-line); margin-bottom: 20px; padding: 18px; border-radius: 4px;
}
.gov-fieldset legend {
  font-size: 0.72rem; text-transform: uppercase; font-weight: 800;
  color: var(--text-secondary); padding: 0 8px; letter-spacing: 0.5px;
}
.profile-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; font-size: 0.85rem; }
.p-cell { display: flex; gap: 8px; color: var(--text-secondary); }
.p-cell b { color: var(--text-primary); }
.p-cell .accent { color: var(--warning); font-family: monospace; }
.p-cell.full { grid-column: span 2; flex-direction: column; gap: 4px; }
.bio-text {
  background: var(--bg-main); padding: 10px; border: 1px dashed var(--border-line);
  margin: 0; font-size: 0.8rem; line-height: 1.5; border-radius: 3px;
}
.bio-text.warn { color: var(--warning); }

.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
.field { display: flex; flex-direction: column; gap: 6px; }
.field.full { grid-column: span 2; }
.field label { font-size: 0.7rem; text-transform: uppercase; color: var(--text-secondary); font-weight: 700; }

.modal-footer {
  display: flex; justify-content: flex-end; gap: 14px;
  border-top: 1px solid var(--border-line); padding-top: 20px; margin-top: 20px;
}
.btn-cancel {
  background: none; border: 1px solid var(--border-line); padding: 12px 24px;
  cursor: pointer; color: var(--text-secondary); font-size: 0.85rem; border-radius: 4px;
}
.btn-cancel:hover { background: var(--paper-card); }
.btn-submit {
  background: var(--accent-color); color: var(--on-accent); border: none; padding: 12px 30px;
  font-weight: 700; cursor: pointer; font-size: 0.85rem; border-radius: 4px;
}
.btn-submit:disabled { opacity: 0.5; cursor: not-allowed; }

@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }

@media (max-width: 640px) {
  .filter-row { flex-direction: column; }
  .profile-grid { grid-template-columns: 1fr; }
  .form-grid { grid-template-columns: 1fr; }
}
</style>