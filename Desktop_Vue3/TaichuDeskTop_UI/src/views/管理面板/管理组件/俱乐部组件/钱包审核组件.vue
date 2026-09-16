<template>
  <div class="panel">
    <header class="module-header">
      <div>
        <h2 class="page-title">提现治理</h2>
        <p class="md-subtitle">打手提现申请审核 · 通过后线下打款 · 拒绝自动退款</p>
      </div>
    </header>

    <!-- 统计条 -->
    <div class="stats-bar">
      <span class="stat"><i class="sdot pending"></i> 待处理 <b>{{ stat.pending }}</b></span>
      <span class="stat"><i class="sdot completed"></i> 已完成 <b>{{ stat.completed }}</b></span>
      <span class="stat"><i class="sdot rejected"></i> 已拒绝 <b>{{ stat.rejected }}</b></span>
      <span class="stat total">
        待处理总额 <b>¥{{ stat.pendingAmount.toFixed(2) }}</b>
      </span>
    </div>

    <!-- 工具栏 -->
    <div class="panel-toolbar">
      <div class="filter-row">
        <select v-model="statusFilter" class="ink-select" @change="loadList">
          <option value="pending">待处理</option>
          <option value="completed">已完成</option>
          <option value="rejected">已拒绝</option>
          <option value="">全部</option>
        </select>
      </div>
      <button class="btn-refresh" @click="loadList" :disabled="loading">
        {{ loading ? '同步中...' : '刷新' }}
      </button>
    </div>

    <!-- 表格 -->
    <div class="table-card">
      <table class="ink-table">
        <thead>
          <tr>
            <th width="70">ID</th>
            <th width="160">打手</th>
            <th width="100">金额</th>
            <th>收款信息</th>
            <th width="100">状态</th>
            <th width="160">申请时间</th>
            <th width="180" class="text-right">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="w in items" :key="w.id">
            <td class="mono">#{{ w.id }}</td>
            <td>
              <div class="op-name">{{ w.nickname || '未命名打手' }}</div>
              <div class="op-id mono">{{ shortId(w.userId) }}</div>
            </td>
            <td class="mono price">¥{{ Number(w.amount).toFixed(2) }}</td>
            <td class="remark-cell">{{ parseContact(w.remark) }}</td>
            <td>
              <span :class="['status-pill', w.status]">
                {{ statusLabel(w.status) }}
              </span>
            </td>
            <td class="mono font-sm">{{ fmtTime(w.createdAt) }}</td>
            <td class="text-right actions">
              <template v-if="w.status === 'pending'">
                <button class="btn-text" @click="handleApprove(w)">通过</button>
                <button class="btn-text danger" @click="handleReject(w)">拒绝</button>
              </template>
              <span v-else class="text-muted">—</span>
            </td>
          </tr>
          <tr v-if="items.length === 0 && !loading">
            <td colspan="7" class="empty-cell">暂无提现记录</td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- 分页 -->
    <div v-if="total > 0" class="pagination">
      <span class="page-info">共 {{ total }} 条</span>
      <button class="btn-page" :disabled="page === 1" @click="changePage(page - 1)">上一页</button>
      <span class="page-cur">{{ page }} / {{ totalPages }}</span>
      <button class="btn-page" :disabled="page >= totalPages" @click="changePage(page + 1)">下一页</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue';
import request from '@/utils/request';

interface WithdrawItem {
  id: number;
  userId: string;
  nickname: string;
  amount: number;
  status: string;
  remark?: string;
  createdAt: string;
  completedAt?: string | null;
}

const items = ref<WithdrawItem[]>([]);
const loading = ref(false);
const statusFilter = ref('pending');
const page = ref(1);
const pageSize = 20;
const total = ref(0);

const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)));

const stat = reactive({
  pending: 0,
  completed: 0,
  rejected: 0,
  pendingAmount: 0,
});

const statusLabel = (s: string) => ({
  pending: '待处理',
  completed: '已完成',
  rejected: '已拒绝',
}[s] || s);

const shortId = (uid: string) => uid ? uid.substring(0, 8).toUpperCase() : '—';

const fmtTime = (iso: string) => {
  if (!iso) return '—';
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

const parseContact = (remark?: string): string => {
  if (!remark) return '—';
  // 格式：提现 · 微信:xxxx · 备注 | 拒绝原因：...
  return remark;
};

const loadList = async () => {
  loading.value = true;
  try {
    const res: any = await request.get('/admin/club/wallet/withdraws', {
      params: {
        status: statusFilter.value || undefined,
        page: page.value,
        pageSize,
      }
    });
    const payload = res?.data ?? res;
    items.value = payload?.items ?? [];
    total.value = payload?.total ?? 0;
    recalcStat();
  } catch (e) {
    console.error('拉取提现列表失败', e);
    items.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
};

const recalcStat = () => {
  const list = items.value;
  stat.pending = list.filter(w => w.status === 'pending').length;
  stat.completed = list.filter(w => w.status === 'completed').length;
  stat.rejected = list.filter(w => w.status === 'rejected').length;
  stat.pendingAmount = list
    .filter(w => w.status === 'pending')
    .reduce((sum, w) => sum + Number(w.amount || 0), 0);
};

const handleApprove = async (w: WithdrawItem) => {
  if (!confirm(`确认通过打手【${w.nickname}】的提现申请 ¥${Number(w.amount).toFixed(2)}？\n\n请确保已经线下打款。`)) return;
  try {
    await request.post(`/admin/club/wallet/withdraw/${w.id}/approve`);
    await loadList();
  } catch (e: any) {
    alert(e?.response?.data?.message || e.message || '操作失败');
  }
};

const handleReject = async (w: WithdrawItem) => {
  const reason = prompt(`拒绝打手【${w.nickname}】的提现申请 ¥${Number(w.amount).toFixed(2)}？\n\n请输入拒绝原因（金额将退回打手余额）:`);
  if (reason === null) return;
  if (!reason.trim()) {
    alert('请填写拒绝原因');
    return;
  }
  try {
    await request.post(`/admin/club/wallet/withdraw/${w.id}/reject`, { reason: reason.trim() });
    await loadList();
  } catch (e: any) {
    alert(e?.response?.data?.message || e.message || '操作失败');
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
.panel { display: flex; flex-direction: column; gap: 20px; animation: fadeIn 0.3s ease; }

.module-header { margin-bottom: 8px; }
.page-title { font-size: 1.6rem; font-weight: 700; color: #111; margin: 0; }
.md-subtitle { font-size: 0.85rem; color: #888; margin: 6px 0 0; }

.stats-bar {
  display: flex; flex-wrap: wrap; gap: 24px;
  padding: 12px 20px; background: #fafafa; border-radius: 6px;
  font-size: 0.85rem;
}
.stat { display: flex; align-items: center; gap: 8px; color: #666; }
.stat b { color: #111; font-weight: 700; }
.stat.total {
  margin-left: auto; padding-left: 24px;
  border-left: 1px solid #eee;
}
.stat.total b { color: #c2410c; }
.sdot { width: 8px; height: 8px; border-radius: 50%; }
.sdot.pending   { background: #f97316; }
.sdot.completed { background: #16a34a; }
.sdot.rejected  { background: #dc2626; }

.panel-toolbar {
  display: flex; justify-content: space-between; align-items: center;
  gap: 16px;
}
.filter-row { display: flex; gap: 12px; }

.ink-select {
  border: 1px solid #e0e0e0; padding: 10px 14px; border-radius: 4px;
  font-size: 0.85rem; outline: none; background: #fff;
  font-family: inherit; cursor: pointer; min-width: 140px;
}
.ink-select:focus { border-color: #1a1a1a; }

.btn-refresh {
  background: #fff; border: 1px solid #e0e0e0; padding: 10px 20px;
  border-radius: 4px; color: #555; cursor: pointer; font-size: 0.85rem;
}
.btn-refresh:hover { border-color: #1a1a1a; color: #000; }
.btn-refresh:disabled { opacity: 0.5; cursor: not-allowed; }

.table-card {
  background: #fff; border: 1px solid #f0f0f0; border-radius: 8px;
  overflow: hidden;
}
.ink-table { width: 100%; border-collapse: collapse; font-size: 0.88rem; }
.ink-table th {
  padding: 14px 16px; background: #fcfcfc; color: #888; text-align: left;
  font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.5px;
  border-bottom: 2px solid #111;
}
.ink-table td { padding: 14px 16px; border-bottom: 1px solid #f7f7f7; vertical-align: middle; }
.ink-table tr:hover td { background: #fafafa; }
.text-right { text-align: right; }
.text-muted { color: #999; }
.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }
.font-sm { font-size: 0.78rem; }
.empty-cell { text-align: center; padding: 60px !important; color: #bbb; font-style: italic; }

.op-name { font-weight: 600; color: #111; }
.op-id { font-size: 0.72rem; color: #999; margin-top: 2px; }
.price { color: #c2410c; font-weight: 700; font-size: 0.95rem; }

.remark-cell {
  font-size: 0.8rem; color: #555;
  max-width: 380px; word-break: break-all;
}

.status-pill {
  display: inline-block; padding: 3px 10px;
  font-size: 0.72rem; font-weight: 600; border-radius: 3px;
}
.status-pill.pending   { background: #fff7ed; color: #c2410c; }
.status-pill.completed { background: #e3fcef; color: #00875a; }
.status-pill.rejected  { background: #fef2f2; color: #dc2626; }

.btn-text {
  background: none; border: none; color: #2563eb; cursor: pointer;
  font-size: 0.8rem; font-weight: 700; margin-left: 12px; padding: 0;
}
.btn-text:hover { text-decoration: underline; }
.btn-text.danger { color: #dc2626; }

.pagination {
  display: flex; justify-content: flex-end; align-items: center;
  gap: 12px; font-size: 0.85rem; color: #666;
}
.page-info { margin-right: auto; }
.page-cur { font-family: monospace; }
.btn-page {
  background: #fff; border: 1px solid #ddd; padding: 6px 14px;
  border-radius: 4px; cursor: pointer; font-size: 0.8rem;
}
.btn-page:not(:disabled):hover { border-color: #111; }
.btn-page:disabled { opacity: 0.4; cursor: not-allowed; }

@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
</style>