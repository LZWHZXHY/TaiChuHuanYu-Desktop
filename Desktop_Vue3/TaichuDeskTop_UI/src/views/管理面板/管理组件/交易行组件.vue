<template>
  <div class="trade-manager" v-loading="loading">
    <header class="module-header">
      <div class="search-bar">
        <input v-model="searchQuery" type="text" placeholder="搜索资源编码、名称或收益..." />
      </div>
      <div class="action-btns">
        <button class="btn-add" @click="handleAddNew">＋ 新增资源</button>
      </div>
    </header>

    <div class="table-card">
      <div class="table-responsive">
        <table class="ink-table">
          <thead>
            <tr>
              <th width="60">ID</th>
              <th width="180">资源定义</th>
              <th width="100">交付模式</th>
              <th width="120">EXP 价格 (基数/系数)</th>
              <th width="80">库存</th>
              <th width="80">状态</th>
              <th width="120" class="text-right">介入操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in filteredItems" :key="item.id" :class="{ 'dimmed': !item.isActive }">
              <td class="mono">#{{ String(item.id).padStart(3, '0') }}</td>
              <td>
                <div class="name-info">
                  <span class="name">{{ item.name }}</span>
                  <span class="benefit-tag">{{ item.benefit }}</span>
                </div>
              </td>
              <td><span class="badge" :class="item.delivery.toLowerCase()">{{ getDeliveryLabel(item.delivery) }}</span></td>
              <td class="mono">
                <span class="price">{{ item.baseCost }}</span>
                <span class="multiplier">/ x{{ item.priceMultiplier }}</span>
              </td>
              <td class="mono">
                <span :class="{ 'warning-text': item.globalStock !== null && item.globalStock <= 5 }">
                  {{ item.globalStock ?? '∞' }}
                </span>
              </td>
              <td>
                <span class="status-dot" :class="item.isActive ? 'on' : 'off'"></span>
                <span class="status-text">{{ item.isActive ? '流转中' : '静默' }}</span>
              </td>
              <td class="text-right actions">
                <button class="btn-s" @click="handleEdit(item)">编辑</button>
                <button class="btn-s danger" @click="handleToggle(item)">{{ item.isActive ? '下架' : '激活' }}</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <Teleport to="body">
      <div 
        v-if="showEditModal" 
        class="modal-mask" 
        @mousedown="handleMaskMouseDown"
        @mouseup="handleMaskMouseUp"
      >
        <div class="modal-container mobile-full" @mousedown.stop @mouseup.stop>
          <header class="modal-header">
            <div class="header-content">
              <h3>{{ isEdit ? '修订资源参数' : '上架太初资源' }}</h3>
              <p>配置将直接影响全站资源流向与 EXP 消耗曲线</p>
            </div>
            <button class="close-icon" @click="closeModal">×</button>
          </header>

          <div class="modal-body scroll-y">
            <div class="form-grid">
              <div class="field full">
                <label>资源名称</label>
                <input v-model="form.name" placeholder="例如：空间上限扩展" />
              </div>
              <div class="field">
                <label>类别</label>
                <select v-model="form.category">
                  <option value="Quota">个人配额</option>
                  <option value="Asset">数字资产</option>
                  <option value="Utility">功能项</option>
                  <option value="Social">社交</option>
                </select>
              </div>
              <div class="field">
                <label>交付模式</label>
                <select v-model="form.delivery">
                  <option value="None">直接交付</option>
                  <option value="Link">静态链接</option>
                  <option value="SecretKey">独立密钥池</option>
                </select>
              </div>
              <div class="field">
                <label>基础价格 (Base EXP)</label>
                <input type="number" v-model="form.baseCost" />
              </div>
              <div class="field">
                <label>增长系数 (Multiplier)</label>
                <input type="number" step="0.01" v-model="form.priceMultiplier" />
              </div>
              <div class="field">
                <label>视觉位阶 (Weight)</label>
                <input type="number" v-model="form.baseWeight" />
              </div>
              <div class="field">
                <label>全局库存 (空为无限)</label>
                <input type="number" v-model="form.globalStock" placeholder="∞" />
              </div>
              <div class="field full">
                <label>核心收益简述</label>
                <input v-model="form.benefit" placeholder="例如：空间 +1.0 TB" />
              </div>
              <div class="field full" v-if="form.delivery === 'Link'">
                <label>资源下载地址 (Static Payload)</label>
                <textarea v-model="form.staticPayload" rows="2" placeholder="输入 itch.io 资源链接或网盘地址..."></textarea>
              </div>
              <div class="field full">
                <label>资源详细描述 (Markdown Support)</label>
                <textarea v-model="form.description" rows="3" placeholder="详细说明资源功能..."></textarea>
              </div>
            </div>
          </div>

          <footer class="modal-footer">
            <button class="btn-confirm" @click="submitForm">确认并发布到太初系统</button>
          </footer>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { TradeApi, type IStoreItem } from '../../../api/Admin';

const loading = ref(false);
const items = ref<IStoreItem[]>([]);
const showEditModal = ref(false);
const isEdit = ref(false);
const searchQuery = ref('');

// 🌟 修复 2: 抽离重置函数，不再在打开弹窗时强制调用
const getEmptyForm = (): IStoreItem => ({
  id: 0, name: '', category: 'Quota', delivery: 'None',
  baseCost: 1000, priceMultiplier: 1.12, baseWeight: 0,
  globalStock: null, benefit: '', description: '', staticPayload: '', isActive: true
});

const form = ref<IStoreItem>(getEmptyForm());

// 记录鼠标是否在遮罩层按下
const isMouseDownOnMask = ref(false);

const handleMaskMouseDown = (e: MouseEvent) => {
  isMouseDownOnMask.value = e.target === e.currentTarget;
};

const handleMaskMouseUp = (e: MouseEvent) => {
  if (isMouseDownOnMask.value && e.target === e.currentTarget) {
    closeModal();
  }
  isMouseDownOnMask.value = false;
};

const loadItems = async () => {
  loading.value = true;
  try {
    items.value = await TradeApi.getAllItems();
  } finally {
    loading.value = false;
  }
};

onMounted(loadItems);

const filteredItems = computed(() => {
  const query = searchQuery.value.toLowerCase();
  return items.value.filter(i => 
    i.name.toLowerCase().includes(query) || 
    i.benefit.toLowerCase().includes(query)
  );
});

const getDeliveryLabel = (t: string) => ({ 'None':'直接', 'Link':'链接', 'SecretKey':'密钥' }[t] || t);

// 🌟 修改：明确点击“新增”才清空，意外关闭后再打开会保留草稿
const handleAddNew = () => {
  isEdit.value = false;
  form.value = getEmptyForm(); // 仅在这里重置
  showEditModal.value = true;
};

const openAddModal = () => {
  // 如果是由于意外关闭想找回内容，直接打开即可
  showEditModal.value = true;
};

const handleEdit = (item: IStoreItem) => {
  isEdit.value = true;
  form.value = { ...item };
  showEditModal.value = true;
};

const closeModal = () => {
  showEditModal.value = false;
  // 关闭时不重置 form.value，保留当前输入状态作为草稿
};

const handleToggle = async (item: IStoreItem) => {
  try {
    const res = await TradeApi.toggleStatus(item.id);
    item.isActive = res.isActive;
  } catch (err) {
    console.error("状态切换失败");
  }
};

const submitForm = async () => {
  try {
    if (isEdit.value) {
      await TradeApi.updateItem(form.value.id, form.value);
    } else {
      await TradeApi.createItem(form.value);
    }
    showEditModal.value = false;
    form.value = getEmptyForm(); // 🌟 提交成功后才清空数据
    await loadItems();
  } catch (err) {
    console.error("提交失败");
  }
};
</script>

<style scoped>
/* 保持原样式不变 */
.trade-manager { display: flex; flex-direction: column; gap: 24px; animation: fadeIn 0.4s ease; }
.module-header { display: flex; justify-content: space-between; align-items: center; gap: 20px; }
.search-bar { flex: 1; max-width: 400px; }
.search-bar input { width: 100%; border: 1px solid #eee; padding: 10px 16px; border-radius: 4px; outline: none; transition: 0.3s; }
.search-bar input:focus { border-color: #111; }
.btn-add { background: #111; color: #fff; border: none; padding: 10px 24px; cursor: pointer; font-weight: 600; }
.table-card { background: #fff; border: 1px solid #f0f0f0; border-radius: 8px; }
.table-responsive { width: 100%; overflow-x: auto; }
.ink-table { width: 100%; border-collapse: collapse; min-width: 800px; }
.ink-table th { padding: 16px; text-align: left; font-size: 0.7rem; color: #bbb; text-transform: uppercase; border-bottom: 2px solid #111; }
.ink-table td { padding: 16px; border-bottom: 1px solid #f9f9f9; font-size: 0.85rem; vertical-align: middle; }
.name-info .name { display: block; font-weight: 700; margin-bottom: 4px; }
.benefit-tag { font-size: 10px; background: #f5f5f5; color: #888; padding: 2px 6px; border-radius: 2px; }
.mono { font-family: "JetBrains Mono", monospace; }
.multiplier { color: #ccc; font-size: 0.75rem; margin-left: 4px; }
.warning-text { color: #ff3b30; font-weight: 800; }
.status-dot { width: 6px; height: 6px; border-radius: 50%; display: inline-block; margin-right: 8px; }
.status-dot.on { background: #34c759; box-shadow: 0 0 8px rgba(52,199,89,0.4); }
.status-dot.off { background: #ff3b30; }
.status-text { font-size: 0.75rem; color: #999; }
.btn-s { background: none; border: none; font-size: 0.75rem; font-weight: 700; cursor: pointer; margin-left: 12px; transition: 0.2s; }
.btn-s.danger { color: #ff3b30; }
.modal-mask { position: fixed; top: 0; left: 0; width: 100vw; height: 100vh; background: rgba(255,255,255,0.8); backdrop-filter: blur(10px); z-index: 999; display: flex; justify-content: center; align-items: center; }
.modal-container { background: #fff; width: 600px; padding: 40px; border: 1px solid #111; box-shadow: 30px 30px 0 rgba(0,0,0,0.05); }
.modal-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 30px; }
.modal-header h3 { font-size: 1.5rem; font-weight: 200; letter-spacing: 1px; }
.modal-header p { font-size: 0.75rem; color: #999; margin-top: 4px; }
.close-icon { background: none; border: none; font-size: 1.5rem; cursor: pointer; color: #ccc; }
.scroll-y { max-height: 60vh; overflow-y: auto; padding-right: 10px; }
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; }
.field.full { grid-column: span 2; }
.field label { display: block; font-size: 0.65rem; color: #aaa; text-transform: uppercase; margin-bottom: 8px; font-weight: 700; }
.field input, .field select, .field textarea { width: 100%; border: 1px solid #eee; padding: 10px; font-size: 0.9rem; outline: none; transition: 0.3s; }
.field input:focus { border-color: #111; }
.btn-confirm { width: 100%; background: #111; color: #fff; border: none; padding: 14px; font-weight: 700; margin-top: 30px; cursor: pointer; }
@media (max-width: 768px) {
  .module-header { flex-direction: column; align-items: stretch; }
  .search-bar { max-width: none; }
  .modal-container.mobile-full { width: 100%; height: 100%; padding: 30px 20px; border: none; box-shadow: none; }
  .form-grid { grid-template-columns: 1fr; gap: 16px; }
  .field.full { grid-column: span 1; }
  .scroll-y { max-height: 75vh; }
  .ink-table { min-width: 600px; }
}
@keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
</style>