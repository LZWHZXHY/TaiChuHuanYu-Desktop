<template>
  <div class="editorial-container content-page community-surface tone-library" v-loading="loading">
    <div class="content-wrapper">
      <header class="editorial-header surface-heading"><SectionTrace />
        <div class="header-left">
          <span class="eyebrow">经验 · 土豆 · 账户权益</span><h1 class="main-title">交易行</h1>
          <p class="sub-caption">头像、头衔、头像框、网站背景、主页装扮、改名卡和站内配额可以用经验或土豆兑换；游戏 CDKey 和资源链接只能用经验兑换。兑换前会再次确认消耗。</p>
        </div>

        <div class="header-right">
          <div class="sort-controls">
            <button @click="sortKey = 'cost'" :aria-pressed="sortKey === 'cost'" :class="{ active: sortKey === 'cost' }">按消耗排序</button>
            <button @click="sortKey = 'name'" :aria-pressed="sortKey === 'name'" :class="{ active: sortKey === 'name' }">按名称排序</button>
            <button class="wardrobe-btn" @click="showWardrobe = true">我的装扮</button>
          </div>
          <div class="meters">
            <div class="experience-meter">
              <span class="label">可用经验 / EXP</span>
              <span class="value">{{ loaded ? state.experience.toLocaleString() : '—' }}</span>
            </div>
            <div class="experience-meter potato">
              <span class="label">土豆</span>
              <span class="value">{{ loaded ? state.potatoes.toLocaleString() : '—' }}</span>
            </div>
          </div>
        </div>
      </header>

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
          <div class="audit-cell">
            <span class="c-label">世界观</span>
            <span class="c-val">{{ loaded && state.maxWorldCount != null ? `${state.usedWorldCount ?? 0} / ${state.maxWorldCount} 个` : '—' }}</span>
          </div>
          <div class="audit-cell" title="对你所有的世界观通用，扩容一次每个世界都会提高">
            <span class="c-label">每个世界的节点上限</span>
            <span class="c-val">{{ loaded && state.maxCardsPerWorld != null ? state.maxCardsPerWorld : '—' }}</span>
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
          :class="{ locked: blockReason(item) !== '' }"
        >
          <div class="item-inner">
            <div class="item-header">
              <span class="item-cat">#{{ String(item.id).padStart(3, '0') }}</span>
              <span class="tags">
                <span v-if="item.effect" class="tag fx">{{ EFFECT_NAMES[item.effect] }}</span>
                <span v-if="item.expOnly" class="tag">仅限经验</span>
                <span v-if="item.owned" class="tag ok">已拥有</span>
                <span v-else-if="item.stock === 0" class="tag bad">已售罄</span>
                <span v-else-if="item.stock != null" class="tag">剩 {{ item.stock }}</span>
                <span v-if="item.purchaseCount > 0" class="purchase-info">已兑换 {{ item.purchaseCount }} 次</span>
              </span>
            </div>

            <div class="item-body">
              <div v-if="item.effect === 'frame' && item.effectValue" class="fx-preview">
                <AvatarFrame :frame="item.effectValue"><span class="fx-avatar"><img v-if="avatar" :src="avatar" alt="" /><b v-else>{{ initial }}</b></span></AvatarFrame>
              </div>
              <div v-else-if="item.effect === 'title' && item.effectValue" class="fx-preview">
                <span class="title-badge">{{ item.effectValue }}</span>
              </div>
              <div v-else-if="item.effect === 'avatar' && item.preview" class="fx-preview">
                <span class="fx-avatar big"><img :src="item.preview.url" :alt="item.preview.title" loading="lazy" /></span>
                <em v-if="item.preview.kind === 'animated'" class="video-tag">动图</em>
              </div>
              <div v-else-if="(item.effect === 'bg' || item.effect === 'pbg') && item.preview" class="fx-preview bg-preview" :class="{ banner: item.preview.slot === 'banner' }" :style="bgThumb(item.preview)">
                <em v-if="item.preview.kind === 'video'" class="video-tag">▶ 动态</em>
              </div>
              <h2 class="item-title">{{ item.name }}</h2>
              <div class="desc-wrapper">
                <p class="item-desc">{{ item.description }}</p>
              </div>
              <div v-if="item.benefit" class="item-benefit">
                <span class="b-dot"></span>
                {{ item.benefit }}
              </div>
            </div>

            <div class="item-footer">
              <div class="price-box">
                <div><span class="p-val">{{ item.expCost.toLocaleString() }}</span><span class="p-unit">EXP</span></div>
                <div v-if="item.potatoCost != null" class="p-alt">或 <b>{{ item.potatoCost.toLocaleString() }}</b> 土豆</div>
              </div>
              <button class="exchange-btn" @click="openConfirm(item)" :disabled="purchasing || blockReason(item) !== ''">
                {{ blockReason(item) || '兑换' }}
              </button>
            </div>
          </div>
        </div>
      </main>

      <!-- 确认 / 结果 -->
      <transition name="fade">
        <div v-if="dialog.show && dialog.item" class="modal-overlay" @click.self="closeDialog">
          <div class="modal-content" role="dialog" aria-modal="true" aria-labelledby="trade-confirm-title" @keydown.esc="closeDialog">
            <template v-if="!dialog.result">
              <div class="modal-header" id="trade-confirm-title">确认兑换</div>
              <div class="modal-body">
                <p class="confirm-name">「{{ dialog.item.name }}」</p>
                <div v-if="dialog.item.potatoCost != null" class="pay-choice" role="radiogroup" aria-label="支付方式">
                  <label :class="{ on: dialog.currency === 'exp', off: state.experience < dialog.item.expCost }">
                    <input v-model="dialog.currency" type="radio" value="exp" :disabled="state.experience < dialog.item.expCost" />
                    <strong>{{ dialog.item.expCost.toLocaleString() }} EXP</strong>
                    <small>{{ state.experience < dialog.item.expCost ? '经验不够' : '用经验' }}</small>
                  </label>
                  <label :class="{ on: dialog.currency === 'potato', off: state.potatoes < dialog.item.potatoCost }">
                    <input v-model="dialog.currency" type="radio" value="potato" :disabled="state.potatoes < dialog.item.potatoCost" />
                    <strong>{{ dialog.item.potatoCost.toLocaleString() }} 土豆</strong>
                    <small>{{ state.potatoes < dialog.item.potatoCost ? '土豆不够' : '用土豆' }}</small>
                  </label>
                </div>
                <p v-else-if="dialog.item.expOnly" class="hint">游戏 CDKey、资源链接只能用经验兑换。</p>
                <dl class="ledger">
                  <template v-if="dialog.currency === 'exp'">
                    <div><dt>经验</dt><dd>{{ state.experience.toLocaleString() }} → <span class="highlight">{{ (state.experience - dialog.item.expCost).toLocaleString() }}</span></dd></div>
                    <div :class="{ warn: levelAfter < state.level }"><dt>等级</dt><dd>Lv.{{ state.level }}<template v-if="levelAfter < state.level"> → Lv.{{ levelAfter }}（等级按剩余经验计算，会降级）</template></dd></div>
                  </template>
                  <div v-else><dt>土豆</dt><dd>{{ state.potatoes.toLocaleString() }} → <span class="highlight">{{ (state.potatoes - (dialog.item.potatoCost ?? 0)).toLocaleString() }}</span></dd></div>
                </dl>
                <p v-if="dialog.error" class="dialog-error" role="alert">{{ dialog.error }}</p>
              </div>
              <div class="modal-actions">
                <button class="btn-cancel" @click="closeDialog" :disabled="purchasing">取消</button>
                <button class="btn-confirm" @click="executeExchange" :disabled="purchasing || !canPay">{{ purchasing ? '兑换中…' : '确认兑换' }}</button>
              </div>
            </template>
            <template v-else>
              <div class="modal-header" id="trade-confirm-title">兑换成功</div>
              <div class="modal-body">
                <p>{{ dialog.result.message }}</p>
                <div v-if="dialog.result.payload" class="payload">
                  <span class="payload-label">{{ dialog.item.delivery === 'Link' ? '资源链接' : '你的卡密' }}（请妥善保存）</span>
                  <code>{{ dialog.result.payload }}</code>
                  <button class="btn-cancel" @click="copyPayload">{{ copied ? '已复制' : '复制' }}</button>
                </div>
              </div>
              <div class="modal-actions">
                <button v-if="dialog.item.effect === 'title' || dialog.item.effect === 'frame'" class="btn-cancel" @click="dialog.show = false; showWardrobe = true">去佩戴</button>
                <RouterLink v-if="dialog.item.effect === 'bg' && user.userId" class="btn-cancel" :to="{ path: `/user/${user.userId}`, query: { tab: 'appearance' } }">去换背景</RouterLink>
                <RouterLink v-if="dialog.item.effect === 'avatar' && user.userId" class="btn-cancel" :to="{ path: `/user/${user.userId}`, query: { pick: 'avatar' } }">去换头像</RouterLink>
                <RouterLink v-if="dialog.item.effect === 'pbg' && user.userId" class="btn-cancel" :to="{ path: `/user/${user.userId}`, query: { tab: 'profile' } }">去装扮主页</RouterLink>
                <button class="btn-confirm" @click="dialog.show = false">好的</button>
              </div>
            </template>
          </div>
        </div>
      </transition>

      <CosmeticsPanel v-if="showWardrobe" @close="showWardrobe = false" @changed="onWardrobeChanged" />

      <transition name="slide-up">
        <div v-if="notice.show" class="minimal-toast" role="status" :class="notice.type">
          {{ notice.message }}
        </div>
      </transition>
    </div>
  </div>
</template>

<script setup lang="ts">
import SectionTrace from '@/components/SectionTrace.vue'
import AvatarFrame from '@/components/AvatarFrame.vue'
import CosmeticsPanel from './CosmeticsPanel.vue'
import { reactive, computed, onMounted, onUnmounted, ref } from 'vue';
import { useUserStore } from '@/stores/user'
import { useRoute } from 'vue-router'
import { TradeApi, tradeError, type ITradeItem, type ITradeResult, type TradeCurrency } from '../../api/trade';

const EFFECT_NAMES: Record<string, string> = { title: '头衔', frame: '头像框', bg: '网站背景', pbg: '主页装扮', avatar: '头像', rename: '改名卡' }
const bgThumb = (p: NonNullable<ITradeItem['preview']>) => {
  const url = p.kind === 'video' ? p.poster : p.url
  return url ? { backgroundImage: `url(${JSON.stringify(url)})` } : undefined
}

const user = useUserStore()
const avatar = computed(() => user.userInfo?.avatar || '')
const initial = computed(() => user.userInfo?.username?.[0] ?? '?')

const loading = ref(false), loaded = ref(false), purchasing = ref(false);
const loadError = ref('');
// 地址带 ?wardrobe=1（比如「授予了你头衔」的通知点进来）直接打开「我的装扮」
const showWardrobe = ref(useRoute().query.wardrobe === '1')
const copied = ref(false)
let noticeTimer: ReturnType<typeof setTimeout> | undefined;
onUnmounted(() => clearTimeout(noticeTimer));
const sortKey = ref<'cost' | 'name'>('cost');

const state = reactive({
  experience: 0,
  level: 0,
  potatoes: 0,
  usedSpaces: 0,
  maxSpaces: 0,
  usedNotes: 0,
  maxNotes: 0,
  usedProjectCount: 0,
  maxProjectCount: 0,
  usedWorldCount: undefined as number | undefined,
  maxWorldCount: undefined as number | undefined,
  maxCardsPerWorld: undefined as number | undefined,
  items: [] as ITradeItem[]
});

const dialog = reactive({
  show: false,
  item: null as ITradeItem | null,
  currency: 'exp' as TradeCurrency,
  error: '',
  result: null as ITradeResult | null,
});
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
    Object.assign(state, status);
    state.potatoes = status.potatoes ?? 0;
    state.items = storeItems; loaded.value = true;
  } catch (err) {
    loadError.value = tradeError(err, '账户或资源读取失败，请重试。');
  } finally {
    loading.value = false;
  }
};

onMounted(initHome);

const processedItems = computed(() => [...state.items].sort((a, b) => {
  if (sortKey.value === 'cost') return a.expCost - b.expCost;
  return a.name.localeCompare(b.name);
}));

const canExp = (item: ITradeItem) => state.experience >= item.expCost
const canPotato = (item: ITradeItem) => item.potatoCost != null && state.potatoes >= item.potatoCost

/** 不能兑换的原因（空字符串 = 可以兑换） */
const blockReason = (item: ITradeItem) => {
  if (item.owned) return '已拥有'
  if (item.stock === 0) return '已售罄'
  if (!canExp(item) && !canPotato(item)) return item.potatoCost != null ? '经验和土豆都不够' : '经验不足'
  return ''
}

const levelAfter = computed(() => {
  const exp = Math.max(0, state.experience - (dialog.item?.expCost ?? 0))
  return exp <= 0 ? 0 : Math.floor(Math.sqrt(exp / 100))
})

const canPay = computed(() => {
  const item = dialog.item
  if (!item) return false
  return dialog.currency === 'potato' ? canPotato(item) : canExp(item)
})

const openConfirm = (item: ITradeItem) => {
  if (purchasing.value || loading.value || loadError.value || blockReason(item)) return;
  dialog.item = item;
  // 经验不够时默认用土豆
  dialog.currency = canExp(item) ? 'exp' : 'potato';
  dialog.error = '';
  dialog.result = null;
  copied.value = false;
  dialog.show = true;
};

const closeDialog = () => { if (!purchasing.value) dialog.show = false; };

const executeExchange = async () => {
  if (purchasing.value || !dialog.item || !canPay.value) return;
  purchasing.value = true;
  dialog.error = '';
  const item = dialog.item;
  try {
    const res = await TradeApi.purchase(item.id, dialog.currency);
    if (res.isSuccess) {
      dialog.result = res;
      await initHome();
      // 列表刷新后换成最新的同一件商品（价格、库存可能变了）
      dialog.item = state.items.find(i => i.id === item.id) ?? item;
      if (item.effect === 'title') user.fetchUserInfo(true).catch(() => null);
    } else {
      dialog.error = res.message || '兑换失败';
    }
  } catch (err) {
    dialog.error = tradeError(err, '兑换失败，请稍后重试。');
  } finally { purchasing.value = false; }
};

const copyPayload = async () => {
  const text = dialog.result?.payload
  if (!text) return
  try { await navigator.clipboard.writeText(text); copied.value = true }
  catch { showNotice('复制失败，请手动选中复制', 'error') }
}

const onWardrobeChanged = () => {
  // 佩戴后不影响价格，只需提示
  showNotice('装扮已更新')
}
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
  border-radius: 0;
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
  border-radius: 0;
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
  grid-template-columns: repeat(3,minmax(0,1fr));
  gap: 16px;
  margin: 24px 0;
}
.audit-cell {
  display: grid;
  gap: 10px;
  padding: 20px;
  background: var(--paper-card);
  border: 1px solid var(--border-line);
  border-radius: 0;
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
  border-radius: 0;
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
  border-radius: 0;
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
  border-radius: 0;
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
  border-radius: 0;
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
  border-radius: 0;
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
  border-radius: 0;
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
.meters { display: grid; gap: 6px; justify-items: end; }
.experience-meter.potato .value { font-size: 22px; color: var(--warning, #d9a400); }
.wardrobe-btn { border-color: var(--accent-color) !important; color: var(--accent-color) !important; }
.tags { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 6px; align-items: center; }
.tag { padding: 1px 7px; border: 1px solid var(--border-line); font-size: 11px; color: var(--text-secondary); }
.tag.fx { border-color: var(--accent-color); color: var(--accent-color); }
.tag.ok { border-color: var(--success); color: var(--success); }
.tag.bad { border-color: var(--danger); color: var(--danger); }
.fx-preview { display: grid; place-items: center; min-height: 92px; margin-top: 14px; border: 1px dashed var(--border-line); background: var(--bg-main); }
.fx-avatar { display: grid; place-items: center; width: 52px; height: 52px; overflow: hidden; background: linear-gradient(135deg, var(--accent-color), var(--signal-mint, #2dd4bf)); color: #fff; font-size: 20px; }
.fx-avatar img { width: 100%; height: 100%; object-fit: cover; }
.fx-avatar.big { width: 80px; height: 80px; }
.fx-preview { position: relative; }
.bg-preview.banner { min-height: 0; aspect-ratio: 3 / 1; align-self: center; }
.bg-preview { min-height: 132px; position: relative; background: var(--bg-sub) center / cover no-repeat; border-style: solid; }
.video-tag { position: absolute; right: 6px; bottom: 6px; padding: 1px 6px; background: rgb(0 0 0 / 55%); color: #fff; font-size: 11px; font-style: normal; }
a.btn-cancel { display: inline-flex; align-items: center; text-decoration: none; }
.title-badge { display: inline-block; padding: 2px 10px; border: 1px solid var(--accent-color); background: var(--accent-soft); color: var(--accent-color); font-size: 13px; font-weight: 600; letter-spacing: .06em; }
.p-alt { font-size: 13px; color: var(--text-secondary); }
.p-alt b { color: var(--text-primary); }
.feed-item.locked { opacity: .78; }
.confirm-name { margin: 0 0 12px; font-size: 18px; font-weight: 600; overflow-wrap: anywhere; }
.pay-choice { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; margin-bottom: 14px; }
.pay-choice label { display: grid; gap: 2px; padding: 10px 12px; border: 1px solid var(--border-line); background: var(--bg-main); cursor: pointer; line-height: 1.4; }
.pay-choice label.on { border-color: var(--accent-color); box-shadow: inset 0 -3px 0 var(--accent-color); }
.pay-choice label.off { opacity: .5; cursor: not-allowed; }
.pay-choice input { position: absolute; opacity: 0; pointer-events: none; }
.pay-choice small { font-size: 12px; color: var(--text-secondary); }
.hint { margin: 0 0 12px; font-size: 13px; color: var(--text-secondary); }
.ledger { display: grid; gap: 6px; margin: 0; font-size: 14px; line-height: 1.6; }
.ledger div { display: flex; justify-content: space-between; gap: 12px; }
.ledger dt { color: var(--text-secondary); }
.ledger dd { margin: 0; text-align: right; }
.ledger .warn dd { color: var(--danger); }
.dialog-error { margin: 12px 0 0; color: var(--danger); font-size: 14px; }
.payload { display: grid; gap: 8px; margin-top: 12px; }
.payload-label { font-size: 12px; color: var(--text-secondary); }
.payload code { padding: 10px 12px; border: 1px solid var(--border-line); background: var(--bg-main); font-size: 14px; overflow-wrap: anywhere; user-select: all; }
.payload .btn-cancel { justify-self: start; padding: 6px 14px; }
@media(max-width:700px) {
  .meters { justify-items: start; }
  .sort-controls { flex-wrap: wrap; }
}
</style>

