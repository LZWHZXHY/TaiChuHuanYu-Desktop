<template>
  <div class="panel">
    <div class="panel-toolbar">
      <div class="game-picker">
        <label>当前游戏：</label>
        <select v-model="gameCode" class="ink-select" @change="loadOrderTypes">
          <option v-for="g in games" :key="g.code" :value="g.code">
            {{ g.name }} ({{ g.code }})
          </option>
        </select>
      </div>
      <button class="btn-primary" :disabled="!gameCode" @click="openModal()">
        + 新增订单类型
      </button>
    </div>

    <div v-if="orderTypes.length > 0" class="stats-bar">
      <span class="stat"><i class="sdot active"></i> 在售 <b>{{ statActive }}</b></span>
      <span class="stat"><i class="sdot upcoming"></i> 未开始 <b>{{ statUpcoming }}</b></span>
      <span class="stat"><i class="sdot expired"></i> 已过期 <b>{{ statExpired }}</b></span>
      <span class="stat"><i class="sdot disabled"></i> 已停用 <b>{{ statDisabled }}</b></span>
    </div>

    <div class="table-card">
      <table class="ink-table">
        <thead>
          <tr>
            <th width="60">上架</th>
            <th width="120">代码</th>
            <th width="200">显示名</th>
            <th width="100">状态</th>
            <th width="140">价格档位</th>
            <th width="160">有效期</th>
            <th width="220" class="text-right">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="t in orderTypes"
            :key="t.id"
            :class="{ 'row-disabled': !t.isActive }"
          >
            <td>
              <label class="switch">
                <input type="checkbox" :checked="t.isActive" @change="toggleActive(t)" />
                <span class="slider"></span>
              </label>
            </td>
            <td class="mono">{{ t.code }}</td>
            <td>
              <div class="ot-name">{{ t.name }}</div>
              <div v-if="t.description" class="ot-desc">{{ t.description }}</div>
            </td>
            <td>
              <span :class="['status-pill', getStatus(t)]">{{ statusLabel(getStatus(t)) }}</span>
            </td>
            <td class="mono price-preview">{{ priceRange(t) }}</td>
            <td>
              <span class="editable-time" @click="openTimeModal(t)">
                {{ formatTimeRange(t.startAt, t.endAt) }}
              </span>
            </td>
            <td class="text-right actions">
              <button class="btn-text" @click="openModal(t)">编辑</button>
              <button class="btn-text" @click="copyOrderType(t)">复制</button>
              <button class="btn-text danger" @click="handleDelete(t)">删除</button>
            </td>
          </tr>
          <tr v-if="orderTypes.length === 0">
            <td colspan="7" class="empty-cell">
              {{ gameCode ? '该游戏暂无订单类型，点击右上角新增' : '请先选择一款游戏' }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- 订单类型编辑弹窗 -->
    <Teleport to="body">
      <div v-if="showModal" class="modal-mask">
        <div class="modal-container scroll-y">
          <header class="modal-header">
            <h3>{{ form.isEdit ? '编辑订单类型' : '新增订单类型' }}</h3>
            <button class="close-icon" @click="closeModal">×</button>
          </header>

          <div class="form-grid">
            <div class="field">
              <label>代码 (Code) *</label>
              <input
                v-model="form.code"
                :disabled="form.isEdit"
                class="ink-input"
                placeholder="如 escort / fun / gamble"
              />
            </div>
            <div class="field">
              <label>显示名 *</label>
              <input v-model="form.name" class="ink-input" placeholder="如 护航单" />
            </div>
            <div class="field full">
              <label>说明</label>
              <textarea v-model="form.description" class="ink-input" rows="2"></textarea>
            </div>
            <div class="field">
              <label>计价方式</label>
              <select v-model="form.pricingMode" class="ink-input">
                <option value="fixed">fixed 一口价</option>
                <option value="per-round">per-round 按局</option>
                <option value="per-hour">per-hour 按小时</option>
                <option value="tiered">tiered 阶梯价</option>
              </select>
            </div>
            <div class="field">
              <label>排序权重</label>
              <input type="number" v-model.number="form.sortOrder" class="ink-input" />
            </div>
            <div class="field full">
              <label>下单参数结构 (JSON Schema)</label>
              <textarea
                v-model="form.paramsSchemaJson"
                class="ink-input mono"
                rows="4"
                placeholder='{"保底":{"type":"select","options":["2588W","4588W"]}}'
              ></textarea>
              <p class="score-hint">描述此订单类型需要老板填写的参数。可以为空。</p>
            </div>
            <div class="field full">
              <label>价格映射 (JSON)</label>
              <textarea
                v-model="form.pricingJson"
                class="ink-input mono"
                rows="3"
                placeholder='{"2588W":388,"4588W":688}'
              ></textarea>
              <p class="score-hint">
                把"下拉选项 → 价格"对应起来。只有 <code>default</code> 一个 key 时表示无参数、固定价。
              </p>
            </div>
            <div class="field full">
              <label>订单特有规则（每行一条）</label>
              <textarea
                v-model="form.specificRulesText"
                class="ink-input"
                rows="5"
                placeholder="· 保底 2588W 起，撤离失败加保底 60W"
              ></textarea>
            </div>
            <div class="field">
              <label>上架时间（留空=立即）</label>
              <input type="datetime-local" v-model="form.startAt" class="ink-input" />
            </div>
            <div class="field">
              <label>下架时间（留空=永久）</label>
              <input type="datetime-local" v-model="form.endAt" class="ink-input" />
            </div>
            <div class="field full">
              <label class="switch-label">
                <input type="checkbox" v-model="form.isActive" />
                <span>启用（前台可见）</span>
              </label>
            </div>
          </div>

          <footer class="modal-footer">
            <button class="btn-cancel" @click="closeModal">取消</button>
            <button class="btn-submit" @click="save" :disabled="saving">
              {{ saving ? '保存中...' : '保存' }}
            </button>
          </footer>
        </div>
      </div>
    </Teleport>

    <!-- 快速改时效弹窗 -->
    <Teleport to="body">
      <div v-if="showTimeModal" class="modal-mask">
        <div class="modal-container" style="max-width: 480px;">
          <header class="modal-header">
            <h3>调整时效 · {{ timeTarget?.name }}</h3>
            <button class="close-icon" @click="closeTimeModal">×</button>
          </header>
          <div class="form-grid">
            <div class="field full">
              <label>上架时间（留空=立即）</label>
              <input type="datetime-local" v-model="timeForm.startAt" class="ink-input" />
            </div>
            <div class="field full">
              <label>下架时间（留空=永久）</label>
              <input type="datetime-local" v-model="timeForm.endAt" class="ink-input" />
            </div>
          </div>
          <footer class="modal-footer">
            <button class="btn-cancel" @click="closeTimeModal">取消</button>
            <button class="btn-submit" @click="saveQuickTime" :disabled="savingTime">
              {{ savingTime ? '保存中...' : '保存' }}
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

interface ClubGame {
  code: string;
  name: string;
  isActive: boolean;
  sortOrder: number;
}

interface ClubOrderType {
  id?: number;
  gameCode: string;
  code: string;
  name: string;
  description?: string;
  paramsSchemaJson?: string;
  pricingJson?: string;
  specificRulesText?: string;
  pricingMode: string;
  isActive: boolean;
  sortOrder: number;
  startAt?: string | null;
  endAt?: string | null;
}

interface OrderTypeFormState {
  isEdit: boolean;
  id?: number;
  code: string;
  name: string;
  description: string;
  paramsSchemaJson: string;
  pricingJson: string;
  specificRulesText: string;
  pricingMode: string;
  isActive: boolean;
  sortOrder: number;
  startAt: string;
  endAt: string;
}

const games = ref<ClubGame[]>([]);
const gameCode = ref('');
const orderTypes = ref<ClubOrderType[]>([]);
const showModal = ref(false);
const saving = ref(false);

const form = reactive<OrderTypeFormState>({
  isEdit: false,
  id: undefined,
  code: '',
  name: '',
  description: '',
  paramsSchemaJson: '',
  pricingJson: '',
  specificRulesText: '',
  pricingMode: 'fixed',
  isActive: true,
  sortOrder: 0,
  startAt: '',
  endAt: '',
});

const toLocalInput = (iso?: string | null): string => {
  if (!iso) return '';
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

const toIso = (local?: string): string | null => {
  if (!local) return null;
  return new Date(local).toISOString();
};

const getStatus = (t: ClubOrderType): 'active' | 'upcoming' | 'expired' | 'disabled' => {
  if (!t.isActive) return 'disabled';
  const now = Date.now();
  if (t.startAt && new Date(t.startAt).getTime() > now) return 'upcoming';
  if (t.endAt && new Date(t.endAt).getTime() < now) return 'expired';
  return 'active';
};

const statusLabel = (s: string) => ({
  active: '在售',
  upcoming: '未开始',
  expired: '已过期',
  disabled: '已停用',
}[s] || s);

const priceRange = (t: ClubOrderType): string => {
  if (!t.pricingJson) return '—';
  try {
    const obj = JSON.parse(t.pricingJson);
    const nums = Object.values(obj).filter((v): v is number => typeof v === 'number');
    if (nums.length === 0) return '—';
    const min = Math.min(...nums);
    const max = Math.max(...nums);
    return min === max ? `¥${min}` : `¥${min}~${max}`;
  } catch {
    return '—';
  }
};

const formatTimeRange = (start?: string | null, end?: string | null): string => {
  if (!start && !end) return '永久';
  const fmt = (iso?: string | null) => {
    if (!iso) return null;
    const d = new Date(iso);
    return `${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  };
  return `${fmt(start) ?? '立即'} ~ ${fmt(end) ?? '永久'}`;
};

const statActive = computed(() => orderTypes.value.filter(t => getStatus(t) === 'active').length);
const statUpcoming = computed(() => orderTypes.value.filter(t => getStatus(t) === 'upcoming').length);
const statExpired = computed(() => orderTypes.value.filter(t => getStatus(t) === 'expired').length);
const statDisabled = computed(() => orderTypes.value.filter(t => getStatus(t) === 'disabled').length);

const loadGames = async () => {
  try {
    const res: any = await request.get('/admin/club/games');
    games.value = res.data || res || [];
    if (games.value.length > 0 && !gameCode.value) {
      gameCode.value = games.value[0].code;
      await loadOrderTypes();
    }
  } catch (e) {
    console.error('拉取游戏列表失败', e);
  }
};

const loadOrderTypes = async () => {
  if (!gameCode.value) {
    orderTypes.value = [];
    return;
  }
  try {
    const res: any = await request.get('/admin/club/order-types', {
      params: { gameCode: gameCode.value },
    });
    orderTypes.value = res.data || res || [];
  } catch (e) {
    console.error('拉取订单类型失败', e);
  }
};

const openModal = (t?: ClubOrderType) => {
  if (!gameCode.value) {
    alert('请先选择游戏');
    return;
  }
  if (t) {
    form.isEdit = true;
    form.id = t.id;
    form.code = t.code;
    form.name = t.name;
    form.description = t.description ?? '';
    form.paramsSchemaJson = t.paramsSchemaJson ?? '';
    form.pricingJson = t.pricingJson ?? '';
    form.specificRulesText = t.specificRulesText ?? '';
    form.pricingMode = t.pricingMode || 'fixed';
    form.isActive = t.isActive;
    form.sortOrder = t.sortOrder;
    form.startAt = toLocalInput(t.startAt);
    form.endAt = toLocalInput(t.endAt);
  } else {
    form.isEdit = false;
    form.id = undefined;
    form.code = '';
    form.name = '';
    form.description = '';
    form.paramsSchemaJson = '';
    form.pricingJson = '';
    form.specificRulesText = '';
    form.pricingMode = 'fixed';
    form.isActive = true;
    form.sortOrder = orderTypes.value.length + 1;
    form.startAt = '';
    form.endAt = '';
  }
  showModal.value = true;
};

const closeModal = () => { showModal.value = false; };

const save = async () => {
  if (!form.code.trim() || !form.name.trim()) {
    alert('请填写代码与显示名');
    return;
  }
  if (form.paramsSchemaJson.trim()) {
    try { JSON.parse(form.paramsSchemaJson); }
    catch { alert('下单参数结构不是合法的 JSON'); return; }
  }
  if (form.pricingJson.trim()) {
    try { JSON.parse(form.pricingJson); }
    catch { alert('价格映射不是合法的 JSON'); return; }
  }
  saving.value = true;
  try {
    await request.post('/admin/club/order-type', {
      id: form.id,
      gameCode: gameCode.value,
      code: form.code.trim(),
      name: form.name.trim(),
      description: form.description,
      paramsSchemaJson: form.paramsSchemaJson.trim() || null,
      pricingJson: form.pricingJson.trim() || null,
      specificRulesText: form.specificRulesText.trim() || null,
      pricingMode: form.pricingMode,
      isActive: form.isActive,
      sortOrder: form.sortOrder,
      startAt: toIso(form.startAt),
      endAt: toIso(form.endAt),
    });
    await loadOrderTypes();
    closeModal();
  } catch (e: any) {
    alert(e.message || '保存失败');
  } finally {
    saving.value = false;
  }
};

const handleDelete = async (t: ClubOrderType) => {
  if (!t.id) return;
  if (!confirm(`确定删除订单类型【${t.name}】吗？`)) return;
  try {
    await request.delete(`/admin/club/order-type/${t.id}`);
    await loadOrderTypes();
  } catch (e: any) {
    alert(e.message || '删除失败');
  }
};

const toggleActive = async (t: ClubOrderType) => {
  if (!t.id) return;
  const before = t.isActive;
  t.isActive = !before;
  try {
    const res: any = await request.patch(`/admin/club/order-type/${t.id}/toggle`);
    const payload = res?.data ?? res;
    if (typeof payload?.isActive === 'boolean') t.isActive = payload.isActive;
  } catch (e: any) {
    t.isActive = before;
    alert(e.message || '操作失败');
  }
};

const copyOrderType = async (t: ClubOrderType) => {
  if (!t.id) return;
  if (!confirm(`复制订单类型【${t.name}】？`)) return;
  try {
    await request.post(`/admin/club/order-type/${t.id}/copy`);
    await loadOrderTypes();
  } catch (e: any) {
    alert(e.message || '复制失败');
  }
};

const showTimeModal = ref(false);
const savingTime = ref(false);
const timeTarget = ref<ClubOrderType | null>(null);
const timeForm = reactive({ startAt: '', endAt: '' });

const openTimeModal = (t: ClubOrderType) => {
  timeTarget.value = t;
  timeForm.startAt = toLocalInput(t.startAt);
  timeForm.endAt = toLocalInput(t.endAt);
  showTimeModal.value = true;
};

const closeTimeModal = () => {
  showTimeModal.value = false;
  timeTarget.value = null;
};

const saveQuickTime = async () => {
  if (!timeTarget.value?.id) return;
  savingTime.value = true;
  try {
    await request.patch(`/admin/club/order-type/${timeTarget.value.id}/time`, {
      startAt: toIso(timeForm.startAt),
      endAt: toIso(timeForm.endAt),
    });
    await loadOrderTypes();
    closeTimeModal();
  } catch (e: any) {
    alert(e.message || '保存失败');
  } finally {
    savingTime.value = false;
  }
};

onMounted(loadGames);
</script>

<style scoped>
.panel { animation: fadeIn 0.3s ease; }

.panel-toolbar {
  display: flex; justify-content: space-between; align-items: center;
  margin-bottom: 16px; gap: 16px;
}
.game-picker { display: flex; align-items: center; gap: 10px; font-size: 0.85rem; color: #555; }

.ink-input, .ink-select {
  border: 1px solid #e0e0e0; padding: 10px 14px; border-radius: 4px;
  font-size: 0.85rem; outline: none; background: #fff;
  font-family: inherit;
}
.ink-input:focus, .ink-select:focus { border-color: #1a1a1a; }
.ink-select { cursor: pointer; min-width: 140px; }

.btn-primary {
  background: #111; color: #fff; border: none; padding: 10px 20px;
  border-radius: 4px; cursor: pointer; font-size: 0.85rem; font-weight: 500;
}
.btn-primary:hover:not(:disabled) { background: #333; }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }

.stats-bar {
  display: flex; flex-wrap: wrap; gap: 24px;
  padding: 12px 20px; margin-bottom: 16px;
  background: #fafafa; border-radius: 6px; font-size: 0.85rem;
}
.stat { display: flex; align-items: center; gap: 8px; color: #666; }
.stat b { color: #111; font-weight: 700; }
.sdot { width: 8px; height: 8px; border-radius: 50%; }
.sdot.active   { background: #16a34a; }
.sdot.upcoming { background: #f97316; }
.sdot.expired  { background: #999; }
.sdot.disabled { background: #dc2626; }

.table-card {
  background: #fff; border: 1px solid #f0f0f0; border-radius: 8px;
  box-shadow: 0 4px 20px rgba(0,0,0,0.01); overflow: hidden;
}
.ink-table { width: 100%; border-collapse: collapse; font-size: 0.88rem; }
.ink-table th {
  padding: 16px; background: #fcfcfc; color: #888; text-align: left;
  font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.5px;
  border-bottom: 2px solid #111;
}
.ink-table td { padding: 16px; border-bottom: 1px solid #f7f7f7; vertical-align: middle; }
.ink-table tr:hover td { background: #fafafa; }
.text-right { text-align: right; }
.text-muted { color: #999; }
.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }
.empty-cell { text-align: center; padding: 60px !important; color: #bbb; font-style: italic; }

.row-disabled td { opacity: 0.55; }
.ot-name { font-weight: 600; color: #111; }
.ot-desc { margin-top: 4px; font-size: 0.75rem; color: #999; }

.switch { position: relative; display: inline-block; width: 34px; height: 18px; }
.switch input { opacity: 0; width: 0; height: 0; }
.slider {
  position: absolute; cursor: pointer; inset: 0;
  background: #ddd; transition: 0.3s; border-radius: 18px;
}
.slider::before {
  position: absolute; content: '';
  height: 14px; width: 14px; left: 2px; bottom: 2px;
  background: #fff; transition: 0.3s; border-radius: 50%;
}
.switch input:checked + .slider { background: #16a34a; }
.switch input:checked + .slider::before { transform: translateX(16px); }

.status-pill {
  display: inline-block; padding: 3px 10px;
  font-size: 0.72rem; font-weight: 600; border-radius: 3px;
}
.status-pill.active   { background: #e3fcef; color: #00875a; }
.status-pill.upcoming { background: #fff7ed; color: #c2410c; }
.status-pill.expired  { background: #f5f5f5; color: #888; }
.status-pill.disabled { background: #fef2f2; color: #dc2626; }

.price-preview { color: #c2410c; font-weight: 600; }

.editable-time {
  padding: 2px 6px; border-bottom: 1px dashed #ddd;
  cursor: pointer; color: #555; font-size: 0.82rem;
}
.editable-time:hover { color: #2563eb; border-bottom-color: #2563eb; }

.btn-text {
  background: none; border: none; color: #2563eb; cursor: pointer;
  font-size: 0.8rem; font-weight: 700; margin-left: 12px; padding: 0;
}
.btn-text:hover { text-decoration: underline; }
.btn-text.danger { color: #dc2626; }

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
.close-icon { background: none; border: none; font-size: 1.8rem; cursor: pointer; color: #ccc; line-height: 1; }
.close-icon:hover { color: #000; }

.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 24px; }
.field { display: flex; flex-direction: column; gap: 6px; }
.field.full { grid-column: span 2; }
.field label { font-size: 0.7rem; text-transform: uppercase; color: #aaa; font-weight: 700; }
.field textarea.ink-input { resize: vertical; font-family: inherit; }
.switch-label { display: flex; align-items: center; gap: 8px; font-size: 0.85rem; cursor: pointer; padding-top: 8px; }

.score-hint { margin: 4px 0 0; font-size: 0.72rem; color: #888; line-height: 1.6; }
.score-hint code {
  padding: 1px 6px; background: #f0f0f0; border-radius: 3px;
  font-family: monospace; font-size: 0.7rem; color: #c2410c;
}

.modal-footer {
  display: flex; justify-content: flex-end; gap: 14px;
  border-top: 1px solid #eee; padding-top: 20px;
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

@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
</style>