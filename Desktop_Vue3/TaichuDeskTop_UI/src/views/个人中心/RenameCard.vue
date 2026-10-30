<!-- RenameCard.vue：改名卡。用一张改名卡（交易行买的），或者直接付经验 / 土豆，改一次用户名。在个人中心用户名旁边打开。 -->
<template>
  <div class="rename-mask" role="presentation" @click.self="close">
    <section class="rename-dialog" role="dialog" aria-modal="true" aria-labelledby="rename-title">
      <header class="rn-head">
        <h3 id="rename-title">改名卡</h3>
        <button type="button" class="rn-x" aria-label="关闭" @click="close">×</button>
      </header>

      <p v-if="loading" class="rn-muted">读取中…</p>
      <p v-else-if="loadError" class="rn-err" role="alert">{{ loadError }}</p>

      <template v-else-if="info">
        <div class="rn-ticket" aria-hidden="true">
          <span class="rn-ticket-name">RENAME CARD</span>
          <b>{{ info.cards }}</b><small>张改名卡</small>
        </div>

        <div class="rn-pay" role="radiogroup" aria-label="付费方式">
          <label v-for="o in payOptions" :key="o.id" :class="{ on: pay === o.id, off: !o.ok }">
            <input v-model="pay" type="radio" :value="o.id" :disabled="!o.ok || step === 'confirm'" />
            <strong>{{ o.label }}</strong>
            <small>{{ o.note }}</small>
          </label>
        </div>

        <dl class="rn-facts">
          <div><dt>当前用户名</dt><dd>{{ info.username }}</dd></div>
          <div v-if="pay === 'card'"><dt>改名卡</dt><dd>{{ info.cards }} → {{ info.cards - 1 }} 张</dd></div>
          <div v-else-if="pay === 'potato'"><dt>土豆</dt><dd>{{ info.potatoes.toLocaleString() }} → {{ (info.potatoes - info.potatoCost).toLocaleString() }}</dd></div>
          <template v-else>
            <div><dt>经验</dt><dd>{{ info.experience.toLocaleString() }} → {{ (info.experience - info.cost).toLocaleString() }}</dd></div>
            <div v-if="info.levelAfter < info.level" class="warn"><dt>等级变化</dt><dd>Lv.{{ info.level }} → Lv.{{ info.levelAfter }}（扣经验后会降级）</dd></div>
          </template>
        </dl>

        <p v-if="!canPayAny" class="rn-err">没有改名卡，经验和土豆也不够（经验要 {{ info.cost.toLocaleString() }}，土豆要 {{ info.potatoCost.toLocaleString() }}）。可以去交易行看看改名卡。</p>

        <form v-else class="rn-form" @submit.prevent="step === 'input' ? toConfirm() : submit()">
          <template v-if="step === 'input'">
            <label for="rn-name">新用户名</label>
            <input id="rn-name" ref="nameInput" v-model.trim="newName" :maxlength="info.rules.maxLength" autocomplete="off" :placeholder="`${info.rules.minLength}–${info.rules.maxLength} 个字，中文、字母、数字或 _ - ·`" @input="scheduleCheck" />
            <p class="rn-check" :class="checkState" role="status">{{ checkText }}</p>
            <div class="rn-actions">
              <button type="button" class="rn-ghost" @click="close">取消</button>
              <button type="submit" class="rn-primary" :disabled="checkState !== 'ok'">下一步</button>
            </div>
          </template>

          <template v-else>
            <div class="rn-confirm">
              <p><b>{{ info.username }}</b> → <b>{{ newName }}</b></p>
              <ul>
                <li>{{ payText }}，不退还。</li>
                <li>用户名也是登录名：改完后请用 <b>「{{ newName }}」</b> 或邮箱登录，旧用户名不能再登录。</li>
                <li>旧用户名会保留 {{ info.rules.reserveDays }} 天，期间别人不能使用。</li>
                <li>以前发的内容里，部分地方可能还显示旧名字。</li>
              </ul>
            </div>
            <p v-if="submitError" class="rn-err" role="alert">{{ submitError }}</p>
            <div class="rn-actions">
              <button type="button" class="rn-ghost" :disabled="submitting" @click="step = 'input'">返回修改</button>
              <button type="submit" class="rn-primary" :disabled="submitting">{{ submitting ? '处理中…' : `确认改名（${payShort}）` }}</button>
            </div>
          </template>
        </form>

        <details v-if="info.history.length" class="rn-history">
          <summary>改名记录（{{ info.history.length }}）</summary>
          <ol><li v-for="h in info.history" :key="h.at"><span>{{ h.oldName }} → {{ h.newName }}</span><small>{{ historyPay(h) }}</small><time>{{ new Date(h.at).toLocaleDateString('zh-CN') }}</time></li></ol>
        </details>
      </template>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { renameApi, type RenameInfo, type RenamePay, type RenameResult } from '@/api/rename'

const emit = defineEmits<{ close: []; renamed: [result: RenameResult] }>()

const info = ref<RenameInfo | null>(null)
const loading = ref(true), loadError = ref('')
const newName = ref('')
const nameInput = ref<HTMLInputElement | null>(null)
const step = ref<'input' | 'confirm'>('input')
const checkState = ref<'' | 'checking' | 'ok' | 'bad'>('')
const checkMsg = ref('')
const submitting = ref(false), submitError = ref('')
const pay = ref<RenamePay>('exp')

const payOptions = computed(() => {
  const i = info.value
  if (!i) return []
  return [
    { id: 'card' as RenamePay, label: '用改名卡', note: i.cards > 0 ? `还有 ${i.cards} 张` : '没有改名卡', ok: i.cards > 0 },
    { id: 'exp' as RenamePay, label: `${i.cost.toLocaleString()} 经验`, note: i.experience >= i.cost ? '直接付经验' : '经验不够', ok: i.experience >= i.cost },
    { id: 'potato' as RenamePay, label: `${i.potatoCost.toLocaleString()} 土豆`, note: i.potatoes >= i.potatoCost ? '直接付土豆' : '土豆不够', ok: i.potatoes >= i.potatoCost },
  ]
})
const canPayAny = computed(() => payOptions.value.some(o => o.ok))
const payText = computed(() => {
  const i = info.value
  if (!i) return ''
  if (pay.value === 'card') return '用掉 1 张改名卡'
  if (pay.value === 'potato') return `扣除 ${i.potatoCost.toLocaleString()} 土豆`
  return `扣除 ${i.cost.toLocaleString()} 经验`
})
const payShort = computed(() => pay.value === 'card' ? '用 1 张改名卡' : payText.value.replace('扣除 ', '扣 '))
const historyPay = (h: RenameInfo['history'][number]) =>
  h.pay === 'card' ? '改名卡' : h.pay === 'potato' ? `${h.cost.toLocaleString()} 土豆` : `${h.cost.toLocaleString()} 经验`
let timer = 0
let checkVersion = 0

const checkText = computed(() => checkState.value === 'checking' ? '检查中…' : checkMsg.value || '输入后会自动检查能不能用')

function close() { if (!submitting.value) emit('close') }
const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') close() }

function scheduleCheck() {
  window.clearTimeout(timer)
  const name = newName.value
  if (!name) { checkState.value = ''; checkMsg.value = ''; return }
  checkState.value = 'checking'
  timer = window.setTimeout(() => void check(name), 400)
}
async function check(name: string) {
  const version = ++checkVersion
  try {
    const r = await renameApi.check(name)
    if (version !== checkVersion || name !== newName.value) return
    checkState.value = r.available ? 'ok' : 'bad'
    checkMsg.value = r.message
  } catch {
    if (version === checkVersion) { checkState.value = 'bad'; checkMsg.value = '检查失败，请稍后再试' }
  }
}
function toConfirm() { if (checkState.value === 'ok') { submitError.value = ''; step.value = 'confirm' } }
async function submit() {
  submitting.value = true; submitError.value = ''
  try {
    const r = await renameApi.rename(newName.value, pay.value)
    emit('renamed', r)
  } catch (e) {
    submitError.value = (e as { response?: { data?: { message?: string } } })?.response?.data?.message || '改名失败，请稍后再试'
  } finally { submitting.value = false }
}

onMounted(async () => {
  window.addEventListener('keydown', onKey)
  try {
    info.value = await renameApi.info()
    // 默认：有改名卡先用卡，否则经验，经验不够再用土豆
    pay.value = payOptions.value.find(o => o.ok)?.id ?? 'exp'
  } catch { loadError.value = '读取改名卡信息失败，请稍后再试' } finally { loading.value = false }
  await nextTick()
  nameInput.value?.focus()
})
onBeforeUnmount(() => { window.removeEventListener('keydown', onKey); window.clearTimeout(timer) })
</script>

<style scoped>
.rename-mask { position: fixed; inset: 0; z-index: 3000; display: grid; place-items: center; padding: 16px; background: rgb(0 0 0 / 45%); }
.rename-dialog { width: min(460px, 100%); max-height: calc(100dvh - 32px); overflow: auto; display: grid; gap: 16px; padding: 20px 22px; border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); background: var(--paper-card); color: var(--text-primary); box-shadow: 0 18px 50px rgb(0 0 0 / 25%); }
.rn-head { display: flex; justify-content: space-between; align-items: center; }
.rn-head h3 { margin: 0; font-size: 17px; }
.rn-x { padding: 0 6px; border: 0; background: none; color: var(--text-secondary); font-size: 22px; line-height: 1; cursor: pointer; }
.rn-ticket { position: relative; display: flex; align-items: baseline; gap: 8px; padding: 14px 16px; border: 1px dashed var(--accent-color); background: repeating-linear-gradient(135deg, color-mix(in srgb, var(--accent-color) 7%, transparent) 0 8px, transparent 8px 16px); }
.rn-ticket-name { position: absolute; top: 6px; right: 10px; font: 9px/1 var(--font-mono); letter-spacing: .16em; color: var(--accent-color); }
.rn-ticket b { font-size: 26px; font-variant-numeric: tabular-nums; color: var(--accent-color); }
.rn-ticket small { color: var(--text-secondary); font-size: 12px; }
.rn-facts { margin: 0; display: grid; gap: 6px; font-size: 13px; }
.rn-facts div { display: flex; justify-content: space-between; gap: 12px; }
.rn-facts dt { color: var(--text-secondary); }
.rn-facts dd { margin: 0; font-variant-numeric: tabular-nums; overflow-wrap: anywhere; text-align: right; }
.rn-facts .warn dd { color: #b45309; }
.rn-form { display: grid; gap: 8px; }
.rn-pay { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 8px; }
.rn-pay label { display: grid; gap: 2px; padding: 8px 10px; border: 1px solid var(--border-line); background: var(--bg-main); font-size: 13px; line-height: 1.4; cursor: pointer; }
.rn-pay label.on { border-color: var(--accent-color); box-shadow: inset 0 -3px 0 var(--accent-color); }
.rn-pay label.off { opacity: .5; cursor: not-allowed; }
.rn-pay input { position: absolute; opacity: 0; pointer-events: none; }
.rn-pay small { font-size: 11px; color: var(--text-secondary); }
.rn-history li small { margin-left: auto; }
.rn-form label { font-size: 13px; font-weight: 600; }
.rn-form input { padding: 9px 10px; border: 1px solid var(--border-line); background: var(--bg-main); color: var(--text-primary); font: inherit; font-size: 15px; }
.rn-check { margin: 0; min-height: 18px; font-size: 12px; color: var(--text-secondary); }
.rn-check.ok { color: var(--success); }
.rn-check.bad { color: var(--danger); }
.rn-confirm { padding: 12px 14px; border: 1px solid var(--border-line); background: var(--bg-sub); font-size: 13px; }
.rn-confirm p { margin: 0 0 8px; font-size: 15px; overflow-wrap: anywhere; }
.rn-confirm ul { margin: 0; padding-left: 18px; display: grid; gap: 4px; line-height: 1.6; }
.rn-actions { display: flex; justify-content: flex-end; flex-wrap: wrap; gap: 8px; margin-top: 6px; }
.rn-primary { padding: 8px 18px; border: 0; background: var(--accent-color); color: var(--on-accent); font: inherit; font-weight: 600; cursor: pointer; }
.rn-ghost { padding: 7px 14px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; cursor: pointer; }
.rn-primary:disabled, .rn-ghost:disabled { opacity: .5; cursor: not-allowed; }
.rn-muted { margin: 0; color: var(--text-secondary); font-size: 13px; }
.rn-err { margin: 0; color: var(--danger); font-size: 13px; }
.rn-history { font-size: 13px; color: var(--text-secondary); }
.rn-history ol { margin: 8px 0 0; padding: 0; list-style: none; display: grid; gap: 4px; }
.rn-history li { display: flex; justify-content: space-between; gap: 12px; }
.rn-history li span { color: var(--text-primary); overflow-wrap: anywhere; }
</style>
