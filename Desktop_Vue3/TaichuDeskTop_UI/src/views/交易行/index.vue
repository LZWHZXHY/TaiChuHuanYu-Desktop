<template>
  <div class="editorial-container content-page" v-loading="loading">
    <div class="content-wrapper">
      <header class="editorial-header">
        <div class="header-left">
          <span class="eyebrow">经验兑换 · 账户权益</span><h1 class="main-title">交易行</h1>
          <p class="sub-caption">查看可用经验和配额，选择需要的资源。兑换前会再次确认消耗。</p>
        </div>

        <div class="header-right">
          <!-- 🌟 排序切换控件 -->
          <div class="sort-controls">
            <button @click="sortKey = 'cost'" :aria-pressed="sortKey === 'cost'" :class="{ active: sortKey === 'cost' }">按消耗排序</button>
            <button @click="sortKey = 'name'" :aria-pressed="sortKey === 'name'" :class="{ active: sortKey === 'name' }">按名称排序</button>
          </div>
          <div class="experience-meter">
            <span class="label">可用经验 / EXP</span>
            <span class="value">{{ loaded ? state.experience.toLocaleString() : '—' }}</span>
          </div>
        </div>
      </header>

      <!-- 🌟 审计区域：展示实时配额 (均已对齐后端实时查询结果) -->
      <section class="audit-section">
        <div class="audit-line">
          <div class="audit-cell">
            <span class="c-label">当前等级</span>
            <span class="c-val">Lv. {{ loaded ? state.level : '—' }}</span>
          </div>
          <div class="audit-cell">
            <span class="c-label">空间配额</span>
            <span class="c-val">{{ loaded ? state.usedSpaces : '—' }} / {{ loaded ? state.maxSpaces : '—' }}</span>
          </div>
          <div class="audit-cell">
            <span class="c-label">灵脉笔记</span>
            <span class="c-val">{{ loaded ? state.usedNotes : '—' }} / {{ loaded ? state.maxNotes : '—' }}</span>
          </div>
          <div class="audit-cell">
            <span class="c-label">协作项目</span>
            <span class="c-val">{{ loaded ? state.usedProjectCount : '—' }} / {{ loaded ? state.maxProjectCount : '—' }} 个</span>
          </div>
        </div>
      </section>

      <div v-if="loadError" class="trade-state" role="alert">{{ loadError }} <button class="btn-line" @click="initHome">重新加载</button></div>
      <p v-else-if="loaded && !state.items.length" class="trade-state">暂时没有可兑换的资源。</p>
      <main class="dynamic-feed" v-if="loaded && !loadError">
        <div
          v-for="item in processedItems"
          :key="item.id"
          class="feed-item"
          :class="{ locked: state.experience < item.cost }"
        >
          <div class="item-inner">
            <div class="item-header">
              <span class="item-cat">#{{ String(item.id).padStart(3, '0') }}</span>
              <span class="purchase-info" v-if="item.purchaseCount > 0">已兑换 {{ item.purchaseCount }} 次</span>
            </div>

            <div class="item-body">
              <h2 class="item-title">{{ item.name }}</h2>
              <div class="desc-wrapper">
                <p class="item-desc">{{ item.description }}</p>
              </div>
              <div class="item-benefit">
                <span class="b-dot"></span>
                {{ item.benefit }}
              </div>
            </div>

            <div class="item-footer">
              <div class="price-box">
                <span class="p-val">{{ item.cost }}</span>
                <span class="p-unit">EXP</span>
              </div>
              <button class="exchange-btn" @click="openConfirm(item)" :disabled="purchasing || state.experience < item.cost">
                {{ state.experience < item.cost ? '经验不足' : '兑换资源' }}
              </button>
            </div>
          </div>
        </div>
      </main>

      <!-- 对话框 -->
      <transition name="fade">
        <div v-if="dialog.show" class="modal-overlay" @click.self="closeDialog">
          <div class="modal-content" role="dialog" aria-modal="true" aria-labelledby="trade-confirm-title" @keydown.esc="closeDialog">
            <div class="modal-header" id="trade-confirm-title">确认兑换</div>
            <div class="modal-body">
              是否消耗 <span class="highlight">{{ dialog.item?.cost }} EXP</span> 兑换
              <br class="mobile-only"/>
              <span class="item-name">「 {{ dialog.item?.name }} 」</span>？
            </div>
            <div class="modal-actions">
              <button class="btn-cancel" @click="closeDialog" :disabled="purchasing">取消</button>
              <button class="btn-confirm" @click="executeExchange" :disabled="purchasing">{{ purchasing ? '兑换中…' : '确认扣除' }}</button>
            </div>
          </div>
        </div>
      </transition>

      <transition name="slide-up">
        <div v-if="notice.show" class="minimal-toast" role="status" :class="notice.type">
          {{ notice.message }}
        </div>
      </transition>
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, computed, onMounted, onUnmounted, ref } from 'vue';
import { TradeApi } from '../../api/trade';

const loading = ref(false), loaded = ref(false), purchasing = ref(false);
const loadError = ref('');
let noticeTimer: ReturnType<typeof setTimeout> | undefined;
onUnmounted(() => clearTimeout(noticeTimer));
const sortKey = ref<'cost' | 'name'>('cost'); // 排序状态

const state = reactive({
  experience: 0,
  level: 0,
  usedSpaces: 0,
  maxSpaces: 0,
  usedNotes: 0,
  maxNotes: 0,
  usedProjectCount: 0, // 🌟 接收后端实时统计值
  maxProjectCount: 0,
  items: [] as any[]
});

const dialog = reactive({ show: false, item: null as any });
const notice = reactive({ show: false, message: '', type: 'success' });

const showNotice = (msg: string, type = 'success') => {
  notice.message = msg;
  notice.type = type;
  notice.show = true;
  clearTimeout(noticeTimer);
  noticeTimer = setTimeout(() => notice.show = false, 4000);
};

const initHome = async () => {
  loading.value = true; loadError.value = '';
  try {
    const [status, storeItems] = await Promise.all([
      TradeApi.getAccountStatus(),
      TradeApi.getStoreItems()
    ]);
    // 🌟 后端 status 接口现已返回所有实时数据，直接合并
    Object.assign(state, status);
    state.items = storeItems; loaded.value = true;
  } catch (err) {
    loadError.value = "账户或资源读取失败，请重试。";
  } finally {
    loading.value = false;
  }
};

onMounted(initHome);

// 🌟 核心：包含计算成本与排序的响应式列表
const processedItems = computed(() => {
  let list = state.items.map(item => {
    const pCount = item.purchaseCount || 0;
    const cost = Math.floor(item.baseCost * Math.pow(item.priceMultiplier || 1, pCount));
    return { ...item, cost, purchaseCount: pCount };
  });

  return list.sort((a, b) => {
    if (sortKey.value === 'cost') return a.cost - b.cost;
    return a.name.localeCompare(b.name);
  });
});

const openConfirm = (item: any) => {
  if (purchasing.value || loading.value || loadError.value || state.experience < item.cost) return;
  dialog.item = item;
  dialog.show = true;
};

const closeDialog = () => { if (!purchasing.value) dialog.show = false; };
const executeExchange = async () => {
  if (purchasing.value || !dialog.item) return;
  purchasing.value = true;
  const item = dialog.item;
  try {
    const res = await TradeApi.purchase(item.id);
    if (res.isSuccess) {
      dialog.show = false;
      showNotice(res.payload ? `兑换成功: ${res.payload}` : `[${item.name}] 已兑换`);
      await initHome(); // 购买后重新拉取最新审计数据
    } else {
      showNotice(res.message, "error");
    }
  } catch (err) {
    showNotice("兑换失败，请稍后重试。", "error");
  } finally { purchasing.value = false; }
};
</script>

<style scoped>
.editorial-container {
  color: var(--text-primary);
  line-height: 1.6;
}
.editorial-header {
  display: grid;
  grid-template-columns: minmax(0,1fr) auto;
  gap: 24px;
  padding: 28px;
  background: var(--paper-card);
  border: 1px solid var(--border-line);
  border-radius: var(--radius-lg);
}
.main-title {
  margin: 8px 0;
}
.sub-caption {
  color: var(--text-secondary);
  font-size: 14px;
  margin: 0;
  max-width: 600px;
}
.header-right {
  display: flex;
  flex-direction: column-reverse;
  justify-content: space-between;
  align-items: end;
  gap: 16px;
}
.experience-meter {
  display: flex;
  align-items: baseline;
  gap: 16px;
}
.experience-meter .label {
  color: var(--text-secondary);
  font-size: 13px;
}
.experience-meter .value {
  font-size: 30px;
  font-weight: 700;
  color: var(--accent-color);
}
.sort-controls {
  display: flex;
  gap: 8px;
}
.sort-controls button {
  padding: 7px 12px;
  border: 1px solid var(--border-line);
  border-radius: 8px;
  background: var(--bg-main);
  color: var(--text-secondary);
}
.sort-controls button.active {
  background: var(--accent-soft);
  color: var(--accent-color);
  border-color: var(--accent-color);
}
.audit-line {
  display: grid;
  grid-template-columns: repeat(4,minmax(0,1fr));
  gap: 16px;
  margin: 24px 0;
}
.audit-cell {
  display: grid;
  gap: 10px;
  padding: 20px;
  background: var(--paper-card);
  border: 1px solid var(--border-line);
  border-radius: var(--radius-md);
}
.c-label {
  font-size: 13px;
  color: var(--text-secondary);
}
.c-val {
  font-size: 20px;
  font-weight: 600;
}
.dynamic-feed {
  display: grid;
  grid-template-columns: repeat(auto-fill,minmax(min(100%,280px),1fr));
  gap: 20px;
}
.feed-item {
  display: flex;
  padding: 24px;
  background: var(--paper-card);
  border: 1px solid var(--border-line);
  border-radius: var(--radius-md);
  min-width: 0;
}
.item-inner {
  width: 100%;
  display: flex;
  flex-direction: column;
}
.item-header {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  font-size: 12px;
  color: var(--text-secondary);
}
.item-body {
  flex: 1;
}
.item-title {
  font-size: 21px;
  margin: 18px 0 10px;
  overflow-wrap: anywhere;
}
.item-desc {
  color: var(--text-secondary);
  font-size: 14px;
  overflow-wrap: anywhere;
}
.item-benefit {
  background: var(--accent-soft);
  color: var(--accent-color);
  font-size: 13px;
  padding: 8px 12px;
  border-radius: 8px;
  margin: 16px 0;
}
.item-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
  border-top: 1px solid var(--border-line);
  padding-top: 16px;
}
.p-val {
  font-size: 24px;
  font-weight: 700;
}
.p-unit {
  color: var(--text-secondary);
  font-size: 12px;
  margin-left: 5px;
}
.exchange-btn,.btn-confirm {
  background: var(--accent-color);
  color: var(--on-accent);
  border: 1px solid var(--accent-color);
  border-radius: 8px;
  padding: 10px 18px;
}
.exchange-btn:disabled {
  background: var(--bg-sub);
  color: var(--text-secondary);
  border-color: var(--border-line);
}
.modal-overlay {
  position: fixed;
  inset: 0;
  background: var(--overlay);
  display: grid;
  place-items: center;
  padding: 20px;
  z-index: 3000;
}
.modal-content {
  width: min(100%,480px);
  max-height: calc(100dvh - 40px);
  overflow: auto;
  background: var(--paper-card);
  padding: 28px;
  border: 1px solid var(--border-line);
  border-radius: 16px;
  box-shadow: var(--shadow-lg);
}
.modal-header {
  font-size: 21px;
  font-weight: 600;
}
.modal-body {
  padding: 24px 0;
  line-height: 1.9;
}
.highlight {
  color: var(--accent-color);
  font-weight: 700;
}
.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}
.btn-cancel {
  border: 1px solid var(--border-line);
  background: var(--bg-main);
  color: var(--text-primary);
  border-radius: 8px;
  padding: 10px 18px;
}
.mobile-only {
  display: none;
}
.trade-state {
  padding: 40px;
  text-align: center;
  color: var(--text-secondary);
}
.minimal-toast {
  position: fixed;
  bottom: 24px;
  right: 24px;
  max-width: min(440px,calc(100vw - 48px));
  padding: 16px 24px;
  background: var(--paper-card);
  border: 1px solid var(--border-line);
  border-radius: 12px;
  box-shadow: var(--shadow-lg);
  color: var(--success);
  z-index: 3100;
  overflow-wrap: anywhere;
}
.minimal-toast.error {
  color: var(--danger);
}
@media(max-width:700px) {
  .editorial-header {
    grid-template-columns: 1fr;
    padding: 20px;
  }
  .header-right {
    align-items: start;
  }
  .audit-line {
    grid-template-columns: repeat(2,minmax(0,1fr));
    gap: 10px;
  }
  .audit-cell {
    padding: 16px;
  }
}
</style>
