<template>
  <div class="editorial-container" v-loading="loading">
    <div class="content-wrapper">
      <header class="editorial-header">
        <div class="header-left">
          <h1 class="main-title">资源中心</h1>
          <p class="sub-caption">账户权益审计与维度扩展 [2026.05]</p>
        </div>
        
        <div class="header-right">
          <div class="experience-meter">
            <span class="label">可用经验 / EXP</span>
            <span class="value">{{ state.experience.toLocaleString() }}</span>
          </div>
        </div>
      </header>

      <section class="audit-section">
        <div class="audit-line">
          <div class="audit-cell">
            <span class="c-label">当前位阶</span>
            <span class="c-val">Lv. {{ state.level }}</span>
          </div>
          <div class="audit-cell">
            <span class="c-label">空间配额</span>
            <span class="c-val">{{ state.usedSpaces }} / {{ state.maxSpaces }}</span>
          </div>
          <div class="audit-cell">
            <span class="c-label">逻辑节点</span>
            <span class="c-val">{{ state.usedNotes }} / {{ state.maxNotes }}</span>
          </div>
        </div>
      </section>

      <main class="dynamic-feed">
        <div 
          v-for="item in processedItems" 
          :key="item.id" 
          class="feed-item"
          :class="[item.rankClass, { 'locked': state.experience < item.cost }]"
          @click="openConfirm(item)"
        >
          <div class="item-inner">
            <div class="item-header">
              <span class="item-cat">#{{ String(item.id).padStart(3, '0') }}</span>
              <span class="purchase-info" v-if="item.purchaseCount > 0">已加持 {{ item.purchaseCount }} 次</span>
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
              <button class="exchange-btn" :disabled="state.experience < item.cost">
                {{ state.experience < item.cost ? '封印中' : '点击兑换' }}
              </button>
            </div>
          </div>
        </div>
      </main>

      <transition name="fade">
        <div v-if="dialog.show" class="modal-overlay" @click.self="dialog.show = false">
          <div class="modal-content">
            <div class="modal-header">系统共鸣请求</div>
            <div class="modal-body">
              是否消耗 <span class="highlight">{{ dialog.item?.cost }} EXP</span> 兑换 
              <br class="mobile-only"/>
              <span class="item-name">「 {{ dialog.item?.name }} 」</span>？
            </div>
            <div class="modal-actions">
              <button class="btn-cancel" @click="dialog.show = false">取消</button>
              <button class="btn-confirm" @click="executeExchange">确认扣除</button>
            </div>
          </div>
        </div>
      </transition>

      <transition name="slide-up">
        <div v-if="notice.show" class="minimal-toast" :class="notice.type">
          {{ notice.message }}
        </div>
      </transition>
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, computed, onMounted, ref } from 'vue';
import { TradeApi } from '../../api/trade';

const loading = ref(false);

const state = reactive({
  experience: 0,
  level: 0,
  usedSpaces: 0,
  maxSpaces: 0,
  usedNotes: 0,
  maxNotes: 0,
  items: [] as any[]
});

const dialog = reactive({ show: false, item: null as any });
const notice = reactive({ show: false, message: '', type: 'success' });

const showNotice = (msg: string, type = 'success') => {
  notice.message = msg;
  notice.type = type;
  notice.show = true;
  setTimeout(() => notice.show = false, 4000);
};

const initHome = async () => {
  loading.value = true;
  try {
    const [status, storeItems] = await Promise.all([
      TradeApi.getAccountStatus(),
      TradeApi.getStoreItems()
    ]);
    Object.assign(state, status);
    state.items = storeItems;
  } catch (err) {
    showNotice("灵脉审计感应中断", "error");
  } finally {
    loading.value = false;
  }
};

onMounted(initHome);

const processedItems = computed(() => {
  return state.items.map(item => {
    const pCount = item.purchaseCount || 0;
    const cost = Math.floor(item.baseCost * Math.pow(item.priceMultiplier || 1, pCount));
    const score = (item.baseWeight || 0) + (cost * 0.1);
    
    let rankClass = 'r-standard';
    if (score >= 6000) rankClass = 'r-hero';
    else if (score >= 2500) rankClass = 'r-feature';
    
    return { ...item, cost, rankClass, purchaseCount: pCount };
  });
});

const openConfirm = (item: any) => {
  if (state.experience < item.cost) return;
  dialog.item = item;
  dialog.show = true;
};

const executeExchange = async () => {
  const item = dialog.item;
  dialog.show = false;
  try {
    const res = await TradeApi.purchase(item.id);
    if (res.isSuccess) {
      showNotice(res.payload ? `兑换成功: ${res.payload}` : `[${item.name}] 已加持`);
      await initHome();
    } else {
      showNotice(res.message, "error");
    }
  } catch (err) {
    showNotice("灵脉传输震荡", "error");
  }
};
</script>

<style scoped>
/* --- 1. 基础布局 --- */
.editorial-container {
  min-height: 100vh;
  background-color: #ffffff;
  color: #111;
  font-family: "Inter", "PingFang SC", sans-serif;
  padding: 80px 6vw;
  line-height: 1.5;
}
.content-wrapper { max-width: 1400px; margin: 0 auto; }

/* --- 2. 响应式页眉 --- */
.editorial-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  margin-bottom: 120px;
}
.main-title { font-size: 3.8rem; font-weight: 200; letter-spacing: -3px; margin: 0; }
.sub-caption { font-size: 0.85rem; color: #999; margin-top: 10px; text-transform: uppercase; }
.experience-meter .value { font-size: 3rem; font-weight: 700; border-bottom: 4px solid #111; }

/* --- 3. 审计线布局 --- */
.audit-section { border-top: 1px solid #eee; padding: 40px 0; margin-bottom: 60px; }
.audit-line { display: flex; gap: 80px; }
.audit-cell .c-label { font-size: 0.65rem; color: #bbb; text-transform: uppercase; margin-bottom: 6px; display: block; }
.audit-cell .c-val { font-size: 1.25rem; font-weight: 500; font-family: monospace; }

/* --- 4. 资源网格 --- */
.dynamic-feed { display: grid; grid-template-columns: repeat(12, 1fr); gap: 80px 48px; }
.feed-item { display: flex; flex-direction: column; cursor: pointer; }
.r-standard { grid-column: span 3; }
.r-feature { grid-column: span 6; }
.r-hero { grid-column: span 12; padding: 60px 0; border-top: 1px solid #111; border-bottom: 1px solid #111; }
.item-title { font-size: 1.6rem; font-weight: 500; margin-bottom: 16px; }
.r-hero .item-title { font-size: 3.5rem; letter-spacing: -2px; }
.item-desc { font-size: 0.9rem; color: #777; line-height: 1.8; }
.item-footer { margin-top: 40px; display: flex; justify-content: space-between; align-items: flex-end; }
.price-box .p-val { font-size: 1.8rem; font-weight: 700; }
.exchange-btn { background: none; border: 1px solid #111; padding: 8px 20px; font-weight: 600; cursor: pointer; }

/* --- 5. 对话框与通知 --- */
.modal-overlay {
  position: fixed; top: 0; left: 0; width: 100%; height: 100%;
  background: rgba(255,255,255,0.95);
  display: flex; align-items: center; justify-content: center; z-index: 2000;
}
.modal-content {
  background: #fff; border: 1px solid #111; padding: 60px;
  max-width: 500px; width: 90%; text-align: center;
}
.modal-body { font-size: 1.2rem; line-height: 2; margin-bottom: 40px; }
.modal-body .item-name { font-size: 1.8rem; font-weight: 200; display: block; margin-top: 10px; }
.modal-actions { display: flex; gap: 20px; justify-content: center; }
.btn-confirm { background: #111; border: 1px solid #111; color: #fff; padding: 12px 40px; cursor: pointer; }
.btn-cancel { background: none; border: 1px solid #eee; padding: 12px 30px; cursor: pointer; color: #999; }

.minimal-toast {
  position: fixed; bottom: 40px; right: 40px;
  background: #111; color: #fff; padding: 16px 32px;
  font-size: 0.85rem; z-index: 3000;
}

/* --- 6. 🌟 核心适配修复 (Mobile Fixes) --- */
@media (max-width: 1024px) {
  .editorial-container { padding: 40px 24px; }
  .editorial-header { flex-direction: column; align-items: flex-start; gap: 40px; margin-bottom: 80px; }
  .main-title { font-size: 2.8rem; letter-spacing: -1.5px; }
  .experience-meter .value { font-size: 2.4rem; }
  
  .audit-line { gap: 32px; flex-wrap: wrap; }
  .audit-cell { flex: 1 1 120px; } /* 自动换行 */
  
  .dynamic-feed { grid-template-columns: repeat(2, 1fr); gap: 48px 24px; }
  .r-standard, .r-feature { grid-column: span 1; }
  .r-hero { grid-column: span 2; padding: 40px 0; }
  .r-hero .item-title { font-size: 2.2rem; }
}

@media (max-width: 640px) {
  .editorial-container { padding: 30px 20px; }
  .main-title { font-size: 2.2rem; letter-spacing: -1px; }
  
  .dynamic-feed { grid-template-columns: 1fr; }
  .r-standard, .r-feature, .r-hero { grid-column: span 1; }
  
  .modal-content { padding: 40px 24px; }
  .modal-body { font-size: 1.1rem; }
  .modal-body .item-name { font-size: 1.4rem; }
  .modal-actions { flex-direction: column; width: 100%; }
  .modal-actions button { width: 100%; }
  
  .minimal-toast {
    bottom: 20px; left: 20px; right: 20px;
    text-align: center; transform: none;
  }
}

.mobile-only { display: none; }
@media (max-width: 640px) { .mobile-only { display: block; } }

/* 动画 */
.fade-enter-active, .fade-leave-active { transition: opacity 0.3s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
.slide-up-enter-active, .slide-up-leave-active { transition: all 0.4s cubic-bezier(0.165, 0.84, 0.44, 1); }
.slide-up-enter-from, .slide-up-leave-to { transform: translateY(20px); opacity: 0; }
</style>