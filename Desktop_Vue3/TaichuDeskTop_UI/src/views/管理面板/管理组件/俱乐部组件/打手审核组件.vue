<template>
  <div class="panel">
    <div class="panel-toolbar">
      <div class="filter-row">
        <input
          v-model="search"
          @keyup.enter="loadOperators"
          class="ink-input"
          placeholder="搜索昵称 / 联系方式 / GUID (回车确认)..."
        />
        <select v-model="statusFilter" class="ink-select" @change="loadOperators">
          <option value="">全部状态</option>
          <option value="pending">待审核</option>
          <option value="reviewing">复核中</option>
          <option value="approved">已通过</option>
          <option value="rejected">已拒绝</option>
          <option value="banned">已封禁</option>
        </select>
      </div>
      <button class="btn-refresh" @click="loadOperators" :disabled="loading">
        {{ loading ? '同步中...' : '刷新列表' }}
      </button>
    </div>

    <div class="operator-grid">
      <div v-for="op in operators" :key="op.userId" class="operator-card">
        <div class="op-head">
          <div class="op-avatar">{{ (op.nickname || '?').substring(0, 1) }}</div>
          <div class="op-meta">
            <div class="op-name">{{ op.nickname || '未命名打手' }}</div>
            <div class="op-contact mono">{{ op.contactType }} / {{ op.contactValue }}</div>
          </div>
          <span :class="['status-badge', op.auditStatus]">
            {{ STATUS_LABEL[op.auditStatus] || op.auditStatus }}
          </span>
        </div>

        <div class="op-reputation">
          <span>信誉分</span>
          <b :class="op.reputation >= 90 ? 'good' : 'bad'">{{ op.reputation }}</b>
          <span class="divider">|</span>
          <span>累计单量</span>
          <b>{{ op.totalOrders }}</b>
        </div>

        <div class="op-games">
          <div
            v-for="sk in op.gameSkills"
            :key="sk.id"
            class="skill-chip"
            :class="sk.auditStatus"
          >
            <span class="game-name">{{ sk.gameName || sk.gameCode }}</span>
            <span class="skill-status">{{ STATUS_LABEL[sk.auditStatus] || sk.auditStatus }}</span>
          </div>
          <span v-if="!op.gameSkills?.length" class="no-skill">暂未申报游戏</span>
        </div>

        <div class="op-actions">
          <button class="btn-text" @click="openOperatorModal(op)">详情 / 审核</button>
        </div>
      </div>

      <div v-if="operators.length === 0 && !loading" class="empty-hint">
        没有符合条件的打手申请
      </div>
    </div>

    <!-- 打手审核弹窗 -->
    <Teleport to="body">
      <div v-if="showModal" class="modal-mask">
        <div class="modal-container scroll-y">
          <header class="modal-header">
            <div>
              <h3>{{ targetOperator?.nickname || '打手详情' }}</h3>
              <p class="mono font-sm">GUID: {{ targetOperator?.userId }}</p>
            </div>
            <button class="close-icon" @click="closeOperatorModal">×</button>
          </header>

          <fieldset class="gov-fieldset">
            <legend>基础档案</legend>
            <div class="profile-grid">
              <div class="p-cell"><span>联系方式:</span> <b>{{ targetOperator?.contactType }} / {{ targetOperator?.contactValue }}</b></div>
              <div class="p-cell"><span>在线时段:</span> <b>{{ targetOperator?.onlineTime || '—' }}</b></div>
              <div class="p-cell"><span>信誉分:</span> <b>{{ targetOperator?.reputation }}</b></div>
              <div class="p-cell"><span>累计单量:</span> <b>{{ targetOperator?.totalOrders }}</b></div>
              <div class="p-cell full"><span>自我介绍:</span> <p class="bio-text">{{ targetOperator?.intro || '无' }}</p></div>
            </div>
          </fieldset>

          <fieldset class="gov-fieldset">
            <legend>游戏技能审核</legend>
            <div v-for="sk in targetOperator?.gameSkills || []" :key="sk.id" class="skill-row">
              <div class="skill-info">
                <b>{{ sk.gameName || sk.gameCode }}</b>
                <span class="mono metrics">{{ sk.metricsJson }}</span>
              </div>
              <div class="skill-form">
                <select v-model="sk.auditStatus" class="ink-select small">
                  <option value="pending">待审核</option>
                  <option value="reviewing">复核中</option>
                  <option value="approved">通过</option>
                  <option value="rejected">拒绝</option>
                  <option value="banned">封禁</option>
                </select>
                <input v-model="sk.code" class="ink-input small mono" placeholder="编号 OP-XXX" />
                <select v-model="sk.operatorLevel" class="ink-select small">
                  <option :value="null">未定级</option>
                  <option value="L1">L1</option>
                  <option value="L2">L2</option>
                  <option value="L3">L3</option>
                  <option value="L4">L4</option>
                  <option value="L5">L5</option>
                </select>
                <input v-model="sk.auditNote" class="ink-input small" placeholder="审核备注" />
              </div>
            </div>
            <div v-if="!targetOperator?.gameSkills?.length" class="empty-hint">该打手暂无游戏申报</div>
          </fieldset>

          <fieldset class="gov-fieldset">
            <legend>整体审核结论</legend>
            <div class="form-grid">
              <div class="field">
                <label>档案状态</label>
                <select v-model="auditForm.auditStatus" class="ink-input">
                  <option value="pending">待审核</option>
                  <option value="reviewing">复核中</option>
                  <option value="approved">已通过</option>
                  <option value="rejected">已拒绝</option>
                  <option value="banned">已封禁</option>
                </select>
              </div>
              <div class="field full">
                <label>审核备注</label>
                <textarea v-model="auditForm.auditNote" class="ink-input" rows="2"></textarea>
              </div>
            </div>
          </fieldset>

          <footer class="modal-footer">
            <button class="btn-cancel" @click="closeOperatorModal">放弃</button>
            <button class="btn-submit" @click="submitAudit" :disabled="saving">
              {{ saving ? '写入中...' : '提交审核结论' }}
            </button>
          </footer>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue';
import request from '@/utils/request';

interface OperatorSkill {
  id: number;
  userId: string;
  gameCode: string;
  gameName: string;
  metricsJson: string;
  auditStatus: string;
  auditNote?: string;
  code?: string;
  operatorLevel?: string | null;
}

interface OperatorProfile {
  userId: string;
  nickname: string;
  contactType: string;
  contactValue: string;
  onlineTime: string;
  intro?: string;
  auditStatus: string;
  auditNote?: string;
  reputation: number;
  totalOrders: number;
  gameSkills: OperatorSkill[];
}

const STATUS_LABEL: Record<string, string> = {
  pending: '待审核', reviewing: '复核中', approved: '已通过',
  rejected: '已拒绝', banned: '已封禁',
};

const operators = ref<OperatorProfile[]>([]);
const loading = ref(false);
const search = ref('');
const statusFilter = ref('');
const showModal = ref(false);
const saving = ref(false);
const targetOperator = ref<OperatorProfile | null>(null);

const auditForm = reactive({
  auditStatus: 'pending',
  auditNote: '',
});

const loadOperators = async () => {
  loading.value = true;
  try {
    const res: any = await request.get('/admin/club/operators', {
      params: {
        search: search.value.trim() || undefined,
        status: statusFilter.value || undefined,
      },
    });
    operators.value = res.data || res || [];
  } catch (e) {
    console.error('拉取打手列表失败', e);
  } finally {
    loading.value = false;
  }
};

const openOperatorModal = (op: OperatorProfile) => {
  targetOperator.value = JSON.parse(JSON.stringify(op)) as OperatorProfile;
  auditForm.auditStatus = op.auditStatus || 'pending';
  auditForm.auditNote = op.auditNote || '';
  showModal.value = true;
};

const closeOperatorModal = () => {
  showModal.value = false;
  targetOperator.value = null;
};

const submitAudit = async () => {
  if (!targetOperator.value) return;
  saving.value = true;
  try {
    await request.post('/admin/club/operator/audit', {
      userId: targetOperator.value.userId,
      auditStatus: auditForm.auditStatus,
      auditNote: auditForm.auditNote,
      gameSkills: (targetOperator.value.gameSkills || []).map((sk) => ({
        id: sk.id,
        auditStatus: sk.auditStatus,
        auditNote: sk.auditNote,
        code: sk.code,
        operatorLevel: sk.operatorLevel,
      })),
    });
    await loadOperators();
    closeOperatorModal();
  } catch (e: any) {
    alert(e.message || '审核提交失败');
  } finally {
    saving.value = false;
  }
};

onMounted(loadOperators);
</script>

<style scoped>
.panel { animation: fadeIn 0.3s ease; }

.panel-toolbar {
  display: flex; justify-content: space-between; align-items: center;
  margin-bottom: 16px; gap: 16px;
}
.filter-row { display: flex; gap: 12px; flex: 1; max-width: 720px; }

.ink-input, .ink-select {
  border: 1px solid #e0e0e0; padding: 10px 14px; border-radius: 4px;
  font-size: 0.85rem; outline: none; background: #fff;
  font-family: inherit;
}
.ink-input:focus, .ink-select:focus { border-color: #1a1a1a; }
.ink-input.small, .ink-select.small { padding: 6px 10px; font-size: 0.8rem; }
.ink-select { cursor: pointer; min-width: 140px; }

.btn-refresh {
  background: #fff; border: 1px solid #e0e0e0; padding: 10px 20px;
  border-radius: 4px; color: #555; cursor: pointer; font-size: 0.85rem;
}
.btn-refresh:hover { border-color: #1a1a1a; color: #000; }

.operator-grid {
  display: grid; grid-template-columns: repeat(auto-fill, minmax(360px, 1fr));
  gap: 16px;
}
.operator-card {
  background: #fff; border: 1px solid #f0f0f0; border-radius: 10px;
  padding: 20px; display: flex; flex-direction: column; gap: 14px;
  transition: 0.25s;
}
.operator-card:hover { border-color: #ddd; box-shadow: 0 6px 24px rgba(0,0,0,0.04); }

.op-head { display: flex; align-items: center; gap: 12px; }
.op-avatar {
  width: 42px; height: 42px; border-radius: 50%;
  background: #111; color: #fff; display: flex;
  align-items: center; justify-content: center;
  font-weight: 700; font-size: 1.1rem;
}
.op-meta { flex: 1; min-width: 0; }
.op-name { font-weight: 700; color: #111; font-size: 0.95rem; }
.op-contact { font-size: 0.72rem; color: #999; }

.op-reputation {
  display: flex; align-items: center; gap: 8px; font-size: 0.8rem; color: #666;
}
.op-reputation b { color: #111; }
.op-reputation .good { color: #16a34a; }
.op-reputation .bad { color: #dc2626; }
.op-reputation .divider { color: #eee; }

.op-games { display: flex; flex-wrap: wrap; gap: 6px; }
.skill-chip {
  display: inline-flex; align-items: center; gap: 6px;
  font-size: 0.72rem; padding: 4px 10px; border-radius: 4px;
  background: #f5f5f5; color: #333;
}
.skill-chip.approved { background: #e3fcef; color: #00875a; }
.skill-chip.rejected { background: #fef2f2; color: #dc2626; }
.skill-chip.pending { background: #fff7ed; color: #c2410c; }
.skill-chip .skill-status { opacity: 0.7; font-weight: 500; }
.no-skill { font-size: 0.75rem; color: #bbb; }

.op-actions { text-align: right; border-top: 1px solid #f5f5f5; padding-top: 12px; }

.status-badge {
  font-size: 0.7rem; padding: 3px 8px; border-radius: 4px; font-weight: 600;
}
.status-badge.pending { background: #fff7ed; color: #c2410c; }
.status-badge.reviewing { background: #e0f2fe; color: #0369a1; }
.status-badge.approved { background: #e3fcef; color: #00875a; }
.status-badge.rejected { background: #fef2f2; color: #dc2626; }
.status-badge.banned { background: #111; color: #fff; }

.empty-hint { font-size: 0.85rem; color: #bbb; padding: 30px; text-align: center; }

.btn-text {
  background: none; border: none; color: #2563eb; cursor: pointer;
  font-size: 0.8rem; font-weight: 700; padding: 0;
}
.btn-text:hover { text-decoration: underline; }

.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }

.modal-mask {
  position: fixed; inset: 0; background: rgba(255,255,255,0.85);
  backdrop-filter: blur(12px); z-index: 9999;
  display: flex; justify-content: center; align-items: center;
}
.modal-container {
  background: #fff; border: 1px solid #000; width: 100%; max-width: 640px;
  padding: 32px; box-shadow: 25px 25px 0 rgba(0,0,0,0.05);
  border-radius: 4px;
}
.modal-container.scroll-y { max-height: 85vh; overflow-y: auto; }
.modal-header {
  display: flex; justify-content: space-between; align-items: flex-start;
  margin-bottom: 24px; border-bottom: 1px solid #eee; padding-bottom: 16px;
}
.modal-header h3 { font-size: 1.25rem; font-weight: 600; margin: 0; }
.modal-header p { margin: 4px 0 0; color: #888; }
.close-icon { background: none; border: none; font-size: 1.8rem; cursor: pointer; color: #ccc; line-height: 1; }
.close-icon:hover { color: #000; }

.gov-fieldset {
  border: 1px solid #eee; margin-bottom: 20px; padding: 18px; border-radius: 4px;
}
.gov-fieldset legend {
  font-size: 0.72rem; text-transform: uppercase; font-weight: 800;
  color: #999; padding: 0 8px; letter-spacing: 0.5px;
}
.profile-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; font-size: 0.85rem; }
.p-cell { display: flex; gap: 8px; color: #666; }
.p-cell b { color: #111; }
.p-cell.full { grid-column: span 2; flex-direction: column; gap: 4px; }
.bio-text { background: #fafafa; padding: 10px; border: 1px dashed #e0e0e0; margin: 0; font-size: 0.8rem; line-height: 1.5; }

.skill-row {
  display: flex; flex-direction: column; gap: 10px;
  padding: 12px 0; border-bottom: 1px solid #f5f5f5;
}
.skill-row:last-child { border-bottom: none; }
.skill-info { display: flex; align-items: center; gap: 12px; font-size: 0.85rem; }
.skill-info b { color: #111; }
.metrics { font-size: 0.75rem; color: #888; }
.skill-form { display: grid; grid-template-columns: 110px 1fr 90px 1fr; gap: 8px; align-items: center; }

.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
.field { display: flex; flex-direction: column; gap: 6px; }
.field.full { grid-column: span 2; }
.field label { font-size: 0.7rem; text-transform: uppercase; color: #aaa; font-weight: 700; }

.modal-footer {
  display: flex; justify-content: flex-end; gap: 14px;
  border-top: 1px solid #eee; padding-top: 20px; margin-top: 20px;
}
.btn-cancel {
  background: none; border: 1px solid #e0e0e0; padding: 12px 24px;
  cursor: pointer; color: #666; font-size: 0.85rem; border-radius: 4px;
}
.btn-cancel:hover { background: #fbfbfb; }
.btn-submit {
  background: #111; color: #fff; border: none; padding: 12px 30px;
  font-weight: 700; cursor: pointer; font-size: 0.85rem; border-radius: 4px;
}
.btn-submit:disabled { opacity: 0.5; cursor: not-allowed; }

.font-sm { font-size: 0.8rem; }

@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
</style>