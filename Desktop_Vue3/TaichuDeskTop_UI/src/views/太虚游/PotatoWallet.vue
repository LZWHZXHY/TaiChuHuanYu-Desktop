<template>
  <!-- 土豆钱包：余额、每日领取、破产补助。compact 用在页头，完整版用在斗地主大厅 -->
  <div class="potato-wallet" :class="{ compact }">
    <template v-if="!user.isLoggedIn">
      <span class="pw-label">🥔 土豆</span><small>登录后每天免费领</small>
    </template>
    <template v-else-if="wallet">
      <span class="pw-balance" :title="`土豆：游戏区专用的娱乐积分，不能充值、转让或兑换`"><span aria-hidden="true">🥔</span><b>{{ wallet.balance.toLocaleString() }}</b><small>土豆</small></span>
      <button v-if="canDaily" type="button" class="pw-btn hot" data-burst :disabled="busy" @click="claimDaily">{{ dailyText }}</button>
      <button v-if="wallet.canRelief" type="button" class="pw-btn" :disabled="busy" @click="claimRelief">领破产补助 +{{ wallet.rules.relief }}</button>
      <button type="button" class="pw-btn exp" title="用经验换土豆（单向，每天有额度）" @click="showExchange = true">{{ compact ? '经验兑换' : '用经验换土豆' }}</button>
      <span v-if="!compact && !wallet.signedToday && !wallet.signBonusClaimed" class="pw-tip">今天签到后还能多领 {{ wallet.rules.signBonus }}</span>
      <span v-if="message" class="pw-msg" role="status">{{ message }}</span>
    </template>
    <Teleport to="body"><ExpExchange v-if="showExchange" @close="showExchange = false" @done="onExchanged" /></Teleport>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useUserStore } from '@/stores/user'
import { POTATO_CHANGED, notifyPotatoChanged, potatoApi, type PotatoWallet } from '@/api/potato'
import ExpExchange from './ExpExchange.vue'

defineProps<{ compact?: boolean }>()
const user = useUserStore()
const wallet = ref<PotatoWallet | null>(null)
const busy = ref(false)
const message = ref('')
const showExchange = ref(false)
function onExchanged(text: string) { showExchange.value = false; say(text); void load() }
let msgTimer = 0

const canDaily = computed(() => !!wallet.value && (!wallet.value.dailyClaimed || (wallet.value.signedToday && !wallet.value.signBonusClaimed)))
const dailyText = computed(() => {
  const w = wallet.value
  if (!w) return ''
  const amount = (w.dailyClaimed ? 0 : w.rules.daily) + (w.signedToday && !w.signBonusClaimed ? w.rules.signBonus : 0)
  return `领今日土豆 +${amount}`
})

function say(text: string) {
  message.value = text
  window.clearTimeout(msgTimer)
  msgTimer = window.setTimeout(() => { message.value = '' }, 4000)
}
async function load() {
  if (!user.isLoggedIn) return
  try { wallet.value = await potatoApi.me() } catch { /* 后端未部署时不显示 */ }
}
async function claimDaily() {
  busy.value = true
  try { const r = await potatoApi.daily(); wallet.value = r.wallet; say(r.message); notifyPotatoChanged() }
  catch { say('领取失败，稍后再试') } finally { busy.value = false }
}
async function claimRelief() {
  busy.value = true
  try { const r = await potatoApi.relief(); wallet.value = r.wallet; say(r.message); notifyPotatoChanged() }
  catch (e) { say((e as { response?: { data?: { message?: string } } })?.response?.data?.message || '领取失败') } finally { busy.value = false }
}
const onChanged = () => { void load() }
onMounted(() => { void load(); window.addEventListener(POTATO_CHANGED, onChanged) })
onBeforeUnmount(() => { window.removeEventListener(POTATO_CHANGED, onChanged); window.clearTimeout(msgTimer) })
defineExpose({ reload: load })
</script>

<style scoped>
.potato-wallet { display: inline-flex; flex-wrap: wrap; align-items: center; gap: 8px 10px; padding: 8px 12px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font-size: 13px; }
.potato-wallet.compact { padding: 6px 10px; }
.pw-balance { display: inline-flex; align-items: baseline; gap: 5px; }
.pw-balance b { font-size: 18px; font-variant-numeric: tabular-nums; }
.compact .pw-balance b { font-size: 15px; }
.pw-balance small, .pw-label + small { color: var(--text-secondary); font-size: 12px; }
.pw-btn { padding: 5px 10px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.pw-btn.hot { border-color: #d97706; background: #f59e0b; color: #1c1206; font-weight: 600; }
.pw-btn:disabled { opacity: .6; cursor: default; }
.pw-btn.exp { border-style: dashed; color: var(--text-secondary); }
.pw-btn.exp:hover { border-color: #d97706; color: #b45309; }
.pw-tip { color: var(--text-secondary); font-size: 12px; }
.pw-msg { color: var(--success); font-size: 12px; }
</style>
