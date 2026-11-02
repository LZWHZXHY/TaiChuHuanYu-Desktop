<!-- 转盘管理（只有超级管理员看得到）：
     ① 做一个转盘的价格（经验 / 土豆），存在系统配置 SystemConfigs 里，改完马上生效；
     ② 官方抽奖转盘：新建、编辑、上架 / 下架、删除。 -->
<template>
  <div class="wheel-admin">
    <WheelEditor v-if="editing !== undefined" official :initial="editing" @saved="onSaved" @cancel="editing = undefined" />

    <template v-else>
      <section class="panel">
        <header class="panel-head">
          <h3>做转盘的价格</h3>
          <p>玩家做一个转盘时，可以选择用经验付或用土豆付。填 <b>-1</b> 表示不允许用这种方式付，填 <b>0</b> 表示免费。编辑转盘不收费，删除不退。</p>
        </header>
        <form v-if="settings" class="price-form" @submit.prevent="saveSettings">
          <label>
            <span>花多少经验</span>
            <input v-model.number="costExp" type="number" min="-1" max="1000000" step="1" />
            <small>系统配置键：{{ settings.keys.exp }}（默认 {{ settings.defaults.exp }}）</small>
          </label>
          <label>
            <span>花多少土豆</span>
            <input v-model.number="costPotato" type="number" min="-1" max="1000000" step="1" />
            <small>系统配置键：{{ settings.keys.potato }}（默认 {{ settings.defaults.potato }}）</small>
          </label>
          <button type="submit" class="btn primary" :disabled="savingSettings">{{ savingSettings ? '保存中…' : '保存价格' }}</button>
        </form>
        <p v-else-if="settingsError" class="err">{{ settingsError }}</p>
        <p v-else class="muted">读取中…</p>
      </section>

      <section class="panel">
        <header class="panel-head row">
          <div>
            <h3>官方抽奖转盘</h3>
            <p>登录用户每个官方转盘每天免费转 1 次，结果由服务器决定，奖品（土豆 / 经验）自动发放。改了扇区不影响今天已经转过的人。</p>
          </div>
          <button type="button" class="btn primary" @click="editing = null">+ 新建官方转盘</button>
        </header>
        <p v-if="listError" class="err">{{ listError }}</p>
        <p v-else-if="!list.length" class="muted">还没有官方转盘。</p>
        <ul v-else class="off-list">
          <li v-for="w in list" :key="w.id" :class="{ hidden: w.status === 'hidden' }">
            <span class="thumb" :style="{ background: conicOf(w.segments) }" aria-hidden="true"></span>
            <div class="off-main">
              <b>{{ w.title }}</b>
              <small>排序 {{ w.sortOrder }} · {{ w.segments.length }} 个扇区 · 转了 {{ w.spinCount.toLocaleString() }} 次 · {{ prizeSummary(w) }}</small>
            </div>
            <span class="st" :class="w.status">{{ w.status === 'active' ? '上架中' : '已下架' }}</span>
            <span class="off-ops">
              <button type="button" class="btn small" @click="emit('open', w.id)">打开</button>
              <button type="button" class="btn small" @click="editing = w">编辑</button>
              <button type="button" class="btn small" @click="toggle(w)">{{ w.status === 'active' ? '下架' : '上架' }}</button>
              <button type="button" class="btn small danger" @click="remove(w)">删除</button>
            </span>
          </li>
        </ul>
      </section>
    </template>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { wheelApi, wheelError, type WheelDetail } from '@/api/wheel'
import WheelEditor from './WheelEditor.vue'
import { conicOf, probabilities, percent } from './wheelShared'

const emit = defineEmits<{ open: [id: number] }>()

// ===== 价格 =====
const settings = ref<{ costExp: number; costPotato: number; keys: { exp: string; potato: string }; defaults: { exp: number; potato: number } } | null>(null)
const settingsError = ref('')
const costExp = ref(0)
const costPotato = ref(0)
const savingSettings = ref(false)

async function loadSettings() {
  try {
    settings.value = await wheelApi.adminSettings()
    costExp.value = settings.value.costExp
    costPotato.value = settings.value.costPotato
  } catch (e) { settingsError.value = wheelError(e, '读取价格失败') }
}
async function saveSettings() {
  const e = Number(costExp.value), p = Number(costPotato.value)
  if (!Number.isInteger(e) || !Number.isInteger(p) || e < -1 || p < -1) { ElMessage.warning('价格要填 -1 或者 0 以上的整数'); return }
  savingSettings.value = true
  try {
    const r = await wheelApi.adminSaveSettings(e, p)
    ElMessage.success(r.message)
    await loadSettings()
  } catch (err) { ElMessage.error(wheelError(err, '保存失败')) } finally { savingSettings.value = false }
}

// ===== 官方转盘 =====
const list = ref<WheelDetail[]>([])
const listError = ref('')
/** undefined = 不在编辑；null = 新建；对象 = 编辑这个 */
const editing = ref<WheelDetail | null | undefined>(undefined)

async function loadList() {
  try { list.value = await wheelApi.adminOfficialList(); listError.value = '' } catch (e) { listError.value = wheelError(e, '读取官方转盘失败') }
}
function prizeSummary(w: WheelDetail) {
  const probs = probabilities(w.segments)
  const win = w.segments.reduce((sum, s, i) => sum + (s.prizeType !== 'none' ? (probs[i] ?? 0) : 0), 0)
  return `中奖率 ${percent(win)}`
}
function onSaved() { editing.value = undefined; void loadList() }
async function toggle(w: WheelDetail) {
  try {
    const r = await wheelApi.adminSetStatus(w.id, w.status === 'active' ? 'hidden' : 'active')
    ElMessage.success(r.message)
    await loadList()
  } catch (e) { ElMessage.error(wheelError(e, '操作失败')) }
}
async function remove(w: WheelDetail) {
  try {
    await ElMessageBox.confirm(`删除官方转盘「${w.title}」？已经发出去的奖品和抽奖记录都会保留。只是暂时不想让大家转的话，用「下架」就行。`, '删除官方转盘', { confirmButtonText: '删除', cancelButtonText: '不删了', type: 'warning' })
  } catch { return }
  try {
    const r = await wheelApi.adminOfficialDelete(w.id)
    ElMessage.success(r.message)
    await loadList()
  } catch (e) { ElMessage.error(wheelError(e, '删除失败')) }
}

onMounted(() => { void loadSettings(); void loadList() })
</script>

<style scoped>
.wheel-admin { display: grid; gap: 18px; color: var(--text-primary); }
.panel { position: relative; display: grid; gap: 14px; padding: 18px; border: 1px solid var(--border-line); background: var(--paper-card); }
.panel::before { content: ''; position: absolute; top: -1px; right: -1px; width: 28px; height: 6px; background: var(--hazard-stripe, repeating-linear-gradient(135deg, #f59e0b 0 4px, #111 4px 8px)); }
.panel-head h3 { margin: 0 0 4px; font-size: 16px; }
.panel-head p { margin: 0; color: var(--text-secondary); font-size: 13px; line-height: 1.7; }
.panel-head.row { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-end; gap: 12px; }
.price-form { display: flex; flex-wrap: wrap; align-items: flex-end; gap: 14px; }
.price-form label { display: grid; gap: 4px; font-size: 13px; }
.price-form span { font-weight: 600; }
.price-form small { color: var(--text-secondary); font: 11px var(--font-mono, monospace); }
.price-form input { width: 180px; padding: 8px 10px; border: 1px solid var(--border-line); border-radius: 0; background: var(--bg-main, var(--paper-card)); color: var(--text-primary); font: inherit; }
.off-list { display: grid; gap: 8px; margin: 0; padding: 0; list-style: none; }
.off-list li { display: grid; grid-template-columns: 44px minmax(0, 1fr) auto auto; gap: 12px; align-items: center; padding: 10px 12px; border: 1px solid var(--border-line); }
.off-list li.hidden { opacity: .6; }
.thumb { width: 44px; height: 44px; border-radius: 50%; border: 3px solid #20252b; }
.off-main { display: grid; gap: 2px; min-width: 0; }
.off-main b { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.off-main small { color: var(--text-secondary); font-size: 12px; }
.st { padding: 2px 8px; font-size: 12px; border: 1px solid var(--border-line); }
.st.active { border-color: var(--success, #16a34a); color: var(--success, #16a34a); }
.off-ops { display: flex; flex-wrap: wrap; gap: 4px; }
.err { margin: 0; color: var(--danger, #dc2626); font-size: 13px; }
.muted { margin: 0; color: var(--text-secondary); font-size: 13px; }
.btn { padding: 8px 16px; border: 1px solid var(--border-line); border-radius: 0; background: transparent; color: var(--text-primary); font: inherit; font-size: 14px; cursor: pointer; }
.btn.small { padding: 4px 10px; font-size: 12px; }
.btn.primary { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent, #fff); font-weight: 600; }
.btn.danger { color: var(--danger, #dc2626); }
.btn:disabled { opacity: .5; cursor: not-allowed; }
@media (max-width: 640px) {
  .off-list li { grid-template-columns: 44px minmax(0, 1fr) auto; }
  .off-ops { grid-column: 1 / -1; }
}
</style>
