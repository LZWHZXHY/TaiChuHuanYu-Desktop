<!-- ExpExchange.vue：经验兑换土豆（单向）。每天有额度上限，土豆不能换回经验。 -->
<template>
  <div class="xx-mask" role="presentation" @click.self="close">
    <section class="xx-dialog" role="dialog" aria-modal="true" aria-labelledby="xx-title">
      <header class="xx-head">
        <h3 id="xx-title">经验兑换土豆</h3>
        <button type="button" class="xx-x" aria-label="关闭" @click="close">×</button>
      </header>

      <p v-if="loading" class="xx-muted">读取中…</p>
      <p v-else-if="loadError" class="xx-err" role="alert">{{ loadError }}</p>

      <template v-else-if="info">
        <div class="xx-rate">
          <span><b>1</b> 经验</span><i aria-hidden="true">→</i><span><b>{{ info.rate }}</b> 🥔</span>
          <small>今日额度 {{ info.usedToday }} / {{ info.dailyCap }} 经验</small>
        </div>

        <p v-if="info.leftToday <= 0" class="xx-err">今天的兑换额度已经用完了，明天再来。</p>
        <p v-else-if="maxExp <= 0" class="xx-err">你现在没有可以兑换的经验。</p>

        <form v-else class="xx-form" @submit.prevent="submit">
          <label for="xx-amount">兑换多少经验 <small>（最多 {{ maxExp }}）</small></label>
          <div class="xx-row">
            <input id="xx-amount" v-model.number="amount" type="number" :min="1" :max="maxExp" step="1" inputmode="numeric" />
            <span class="xx-get">= <b>{{ (safeAmount * info.rate).toLocaleString() }}</b> 🥔</span>
          </div>
          <div class="xx-quick">
            <button v-for="q in quick" :key="q" type="button" :class="{ on: amount === q }" @click="amount = q">{{ q === maxExp ? `全部 ${q}` : q }}</button>
          </div>

          <dl class="xx-facts">
            <div><dt>经验</dt><dd>{{ info.experience.toLocaleString() }} → {{ (info.experience - safeAmount).toLocaleString() }}</dd></div>
            <div :class="{ warn: levelAfter < info.level }"><dt>等级</dt><dd>Lv.{{ info.level }}<template v-if="levelAfter < info.level"> → Lv.{{ levelAfter }}（会降级）</template></dd></div>
          </dl>
          <p class="xx-note">土豆只能在游戏区使用，<b>不能换回经验</b>，也不能转让。兑换的土豆不计入土豆榜。</p>
          <p v-if="error" class="xx-err" role="alert">{{ error }}</p>

          <div class="xx-actions">
            <button type="button" class="xx-ghost" :disabled="submitting" @click="close">取消</button>
            <button type="submit" class="xx-primary" :disabled="submitting || safeAmount <= 0">{{ submitting ? '兑换中…' : `兑换 ${(safeAmount * info.rate).toLocaleString()} 土豆` }}</button>
          </div>
        </form>
      </template>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { notifyPotatoChanged, potatoApi, type ExpExchangeInfo } from '@/api/potato'
import { useUserStore } from '@/stores/user'

const emit = defineEmits<{ close: []; done: [message: string] }>()
const user = useUserStore()
const info = ref<ExpExchangeInfo | null>(null)
const loading = ref(true), loadError = ref('')
const amount = ref(0)
const submitting = ref(false), error = ref('')

const maxExp = computed(() => (info.value ? Math.max(0, Math.min(info.value.leftToday, info.value.experience)) : 0))
const safeAmount = computed(() => Math.max(0, Math.min(maxExp.value, Math.floor(Number(amount.value) || 0))))
const levelAfter = computed(() => {
  const exp = Math.max(0, (info.value?.experience ?? 0) - safeAmount.value)
  return exp <= 0 ? 0 : Math.floor(Math.sqrt(exp / 100))
})
const quick = computed(() => [...new Set([50, 100, maxExp.value].filter(q => q > 0 && q <= maxExp.value))])

function close() { if (!submitting.value) emit('close') }
const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') close() }

async function submit() {
  if (safeAmount.value <= 0) return
  submitting.value = true; error.value = ''
  try {
    const r = await potatoApi.exchange(safeAmount.value)
    notifyPotatoChanged()
    void user.fetchUserInfo(true)
    emit('done', r.message)
  } catch (e) {
    error.value = (e as { response?: { data?: { message?: string } } })?.response?.data?.message || '兑换失败，请稍后再试'
  } finally { submitting.value = false }
}

onMounted(async () => {
  window.addEventListener('keydown', onKey)
  try {
    info.value = await potatoApi.exchangeInfo()
    amount.value = Math.min(100, maxExp.value)
  } catch { loadError.value = '读取兑换信息失败，请稍后再试' } finally { loading.value = false }
})
onBeforeUnmount(() => window.removeEventListener('keydown', onKey))
</script>

<style scoped>
.xx-mask { position: fixed; inset: 0; z-index: 3000; display: grid; place-items: center; padding: 16px; background: rgb(0 0 0 / 45%); }
.xx-dialog { width: min(420px, 100%); max-height: calc(100dvh - 32px); overflow: auto; display: grid; gap: 16px; padding: 20px 22px; border: 1px solid var(--border-line); border-top: 3px solid #f59e0b; background: var(--paper-card); color: var(--text-primary); box-shadow: 0 18px 50px rgb(0 0 0 / 25%); }
.xx-head { display: flex; justify-content: space-between; align-items: center; }
.xx-head h3 { margin: 0; font-size: 17px; }
.xx-x { padding: 0 6px; border: 0; background: none; color: var(--text-secondary); font-size: 22px; line-height: 1; cursor: pointer; }
.xx-rate { display: flex; flex-wrap: wrap; align-items: baseline; gap: 6px 12px; padding: 12px 14px; border: 1px dashed #d97706; background: color-mix(in srgb, #f59e0b 8%, transparent); }
.xx-rate b { font-size: 22px; font-variant-numeric: tabular-nums; }
.xx-rate i { color: var(--text-secondary); font-style: normal; }
.xx-rate small { flex-basis: 100%; color: var(--text-secondary); font-size: 12px; }
.xx-form { display: grid; gap: 10px; }
.xx-form label { font-size: 13px; font-weight: 600; }
.xx-form label small { font-weight: 400; color: var(--text-secondary); }
.xx-row { display: flex; align-items: center; gap: 12px; }
.xx-row input { width: 140px; padding: 8px 10px; border: 1px solid var(--border-line); background: var(--bg-main); color: var(--text-primary); font: inherit; font-size: 16px; font-variant-numeric: tabular-nums; }
.xx-get { font-size: 14px; color: var(--text-secondary); }
.xx-get b { color: #b45309; font-size: 18px; }
.xx-quick { display: flex; flex-wrap: wrap; gap: 6px; }
.xx-quick button { padding: 4px 12px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.xx-quick button.on { border-color: #d97706; background: #f59e0b; color: #1c1206; }
.xx-facts { margin: 4px 0 0; display: grid; gap: 4px; font-size: 13px; }
.xx-facts div { display: flex; justify-content: space-between; gap: 12px; }
.xx-facts dt { color: var(--text-secondary); }
.xx-facts dd { margin: 0; font-variant-numeric: tabular-nums; }
.xx-facts .warn dd { color: #b45309; }
.xx-note { margin: 0; padding: 8px 10px; background: var(--bg-sub); font-size: 12px; line-height: 1.6; color: var(--text-secondary); }
.xx-note b { color: var(--text-primary); }
.xx-actions { display: flex; justify-content: flex-end; gap: 8px; }
.xx-primary { padding: 8px 18px; border: 0; background: #f59e0b; color: #1c1206; font: inherit; font-weight: 600; cursor: pointer; }
.xx-ghost { padding: 7px 14px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; cursor: pointer; }
.xx-primary:disabled, .xx-ghost:disabled { opacity: .5; cursor: not-allowed; }
.xx-muted { margin: 0; color: var(--text-secondary); font-size: 13px; }
.xx-err { margin: 0; color: var(--danger); font-size: 13px; }
</style>
