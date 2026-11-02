<!-- 约战面板模板组件.vue：管理面板 →「约战面板模板」（只有超级管理员能看到、能改）。
     官方约战模板 = 所有 OC 参加柴圈约战时填的「约战面板」用的同一套维度和等级。
     在这里可以增删维度、改名字和说明、调顺序、选能用哪些等级，右边有预览雷达图。
     数据安全：OC 的值按维度的「标识」（key）存，所以——
       · 改名字：不影响已经填的值；
       · 删掉维度：OC 填的值还留在数据库里，只是不显示，下面「已删除的维度」里点「加回来」就又出现了；
       · 新加维度：老 OC 显示成「?」，等作者补填。
     改模板只改模板本身，绝不改写、删除任何 OC 的数据。 -->
<template>
  <div class="bpt-admin">
    <header class="bpt-header">
      <div>
        <h1>约战面板模板</h1>
        <p>柴圈约战用的官方模板：所有 OC 的「约战面板」都用这一套维度和等级，方便双方对比、论战。</p>
      </div>
      <div class="bpt-actions">
        <button type="button" class="btn-line" :disabled="saving || !dirty" @click="reset">撤销修改</button>
        <button type="button" class="btn-line btn-primary" :disabled="saving || !dirty || !canEdit" @click="save">{{ saving ? '保存中…' : dirty ? '保存模板' : '已保存' }}</button>
      </div>
    </header>
    <p class="bpt-safe">改名不影响已填的数值；删掉的维度数值会保留但不显示，加回来还在；新加的维度老 OC 会显示「?」。改模板不会改写或删除任何 OC 的数据。</p>
    <p v-if="message" class="bpt-message" :class="{ error: messageIsError }" role="status">{{ message }}</p>
    <p v-if="loading" class="bpt-state" role="status">正在读取…</p>
    <p v-else-if="loadError" class="bpt-state" role="alert">{{ loadError }} <button type="button" class="btn-line" @click="load">重试</button></p>

    <div v-else class="bpt-body">
      <fieldset class="bpt-form" :disabled="!canEdit || saving">
        <p v-if="!canEdit" class="bpt-message error">只有超级管理员能修改约战模板，你现在只能查看。</p>
        <label class="bpt-field"><span>面板标题</span><input v-model="draft.title" maxlength="20" placeholder="约战面板" /></label>

        <div class="bpt-field">
          <span>可选等级（至少两个）</span>
          <div class="bpt-grades">
            <label v-for="g in ALL_GRADES" :key="g" class="bpt-grade" :title="GRADE_HINT[g]">
              <input type="checkbox" :checked="draft.grades.includes(g)" @change="toggleGrade(g)" />
              <b>{{ g }}</b><small>{{ GRADE_HINT[g] }}</small>
            </label>
          </div>
          <small class="bpt-note">去掉某个等级后，已经选了这个等级的 OC 会暂时显示成「?」（值还在，加回来就恢复）。</small>
        </div>

        <div class="bpt-field">
          <span>维度（顺序就是雷达图上从正上方开始顺时针的顺序，最少 {{ MIN_DIMS }} 个）</span>
          <ol class="bpt-dims">
            <li v-for="(d, i) in draft.dims" :key="d.key || 'new' + i" class="bpt-dim">
              <span class="bpt-no">{{ i + 1 }}</span>
              <input v-model="d.name" class="bpt-name" maxlength="12" placeholder="维度名字" :aria-label="`第 ${i + 1} 个维度的名字`" />
              <input :value="d.hint ?? ''" class="bpt-hint" maxlength="40" placeholder="一句话说明（可选）" :aria-label="`${d.name || '维度'}的说明`" @input="d.hint = ($event.target as HTMLInputElement).value" />
              <small class="bpt-key" :title="d.key ? '标识不会变：OC 的值按它存，改名字不影响' : '保存后自动生成标识'">{{ d.key || '新维度' }}</small>
              <span class="bpt-ops">
                <button type="button" :disabled="i === 0" :aria-label="`上移 ${d.name}`" @click="move(i, -1)">↑</button>
                <button type="button" :disabled="i === draft.dims.length - 1" :aria-label="`下移 ${d.name}`" @click="move(i, 1)">↓</button>
                <button type="button" class="del" :disabled="draft.dims.length <= MIN_DIMS" :title="draft.dims.length <= MIN_DIMS ? `最少 ${MIN_DIMS} 个维度` : '删除（OC 填的值会保留）'" :aria-label="`删除 ${d.name}`" @click="remove(i)">×</button>
              </span>
            </li>
          </ol>
          <button type="button" class="btn-line bpt-add" @click="addDim">＋ 添加维度</button>
        </div>

        <div v-if="retiredList.length" class="bpt-field">
          <span>已删除的维度（OC 以前填的值都还在，加回来就又显示）</span>
          <ul class="bpt-retired">
            <li v-for="d in retiredList" :key="d.key">
              <b>{{ d.name }}</b><small>{{ d.key }}</small>
              <button type="button" class="btn-line" @click="restore(d)">加回来</button>
            </li>
          </ul>
        </div>
      </fieldset>

      <aside class="bpt-preview">
        <p class="bpt-preview-title">预览（示例等级）</p>
        <StatPanelChart :panel="preview" />
        <p class="bpt-note">当前版本 v{{ saved?.version ?? 1 }}{{ dirty ? ' · 有未保存的修改' : '' }}</p>
      </aside>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import StatPanelChart from '@/components/StatPanelChart.vue'
import { ALL_GRADES, GRADE_HINT, type Grade, type StatPanel } from '@/utils/statPanel'
import { ocPanelApi, panelError, type BattlePanelTemplate, type BattleTemplateDim } from '@/views/柴圈板块/OCs/ocPanel'

/** 最少几个维度（和后端 BattlePanelTemplate.MinDims 一致）；不设上限，界面上也不写 */
const MIN_DIMS = 3

interface Draft { title: string; grades: string[]; dims: BattleTemplateDim[] }
const loading = ref(true)
const loadError = ref('')
const saving = ref(false)
const message = ref('')
const messageIsError = ref(false)
const saved = ref<BattlePanelTemplate | null>(null)
const draft = ref<Draft>({ title: '', grades: [], dims: [] })
const canEdit = computed(() => Boolean(saved.value?.canEdit))

const toDraft = (t: BattlePanelTemplate): Draft => ({
  title: t.title,
  grades: [...t.grades],
  dims: t.dims.map(d => ({ key: d.key, name: d.name, hint: d.hint ?? '' })),
})
const snapshot = (d: Draft) => JSON.stringify({ title: d.title.trim(), grades: d.grades, dims: d.dims.map(x => ({ key: x.key, name: x.name.trim(), hint: (x.hint ?? '').trim() })) })
const dirty = computed(() => Boolean(saved.value) && snapshot(draft.value) !== snapshot(toDraft(saved.value!)))

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    saved.value = await ocPanelApi.templateFresh()
    draft.value = toDraft(saved.value)
  } catch (err) {
    loadError.value = panelError(err, '约战模板读取失败')
  } finally {
    loading.value = false
  }
}
onMounted(load)
function reset() { if (saved.value) draft.value = toDraft(saved.value); message.value = '' }

function toggleGrade(g: Grade) {
  const set = new Set(draft.value.grades)
  if (set.has(g)) set.delete(g); else set.add(g)
  // 按固定顺序排（A B C D E ∞ ? —）
  draft.value.grades = ALL_GRADES.filter(x => set.has(x))
}
function move(i: number, dir: -1 | 1) {
  const j = i + dir
  const dims = draft.value.dims
  if (j < 0 || j >= dims.length) return
  ;[dims[i], dims[j]] = [dims[j], dims[i]]
}
function remove(i: number) { if (draft.value.dims.length > MIN_DIMS) draft.value.dims.splice(i, 1) }
/** 新维度不带标识（key 留空），保存时后端生成一个从没用过的，不会错连到以前删掉的维度的旧值上 */
function addDim() { draft.value.dims.push({ key: '', name: '', hint: '' }) }

/** 已删除的维度：服务器记的 + 这次编辑里刚删掉的（还没保存），去掉已经在当前列表里的 */
const retiredList = computed(() => {
  const inUse = new Set(draft.value.dims.map(d => d.key).filter(Boolean))
  const list = [...(saved.value?.dims ?? []), ...(saved.value?.retired ?? [])]
  const seen = new Set<string>()
  return list.filter(d => !inUse.has(d.key) && !seen.has(d.key) && (seen.add(d.key), true))
})
/** 加回来：用回原来的标识，OC 以前填的值就又显示了 */
function restore(d: BattleTemplateDim) { draft.value.dims.push({ key: d.key, name: d.name, hint: d.hint ?? '' }) }

// 预览：按可选等级轮流给示例值，看看雷达图的样子
const preview = computed<StatPanel>(() => {
  const usable = draft.value.grades.filter(g => g !== '?' && g !== '—') as Grade[]
  return {
    v: 1, title: draft.value.title.trim() || '约战面板', scale: 'grade', max: 100, overall: true,
    dims: draft.value.dims.map((d, i) => ({ name: d.name.trim() || `维度${i + 1}`, value: usable.length ? usable[i % usable.length] : '?' })),
  }
})

async function save() {
  if (!saved.value || !canEdit.value) return
  message.value = ''
  const d = draft.value
  if (d.grades.length < 2) return fail('至少要选两个可选等级')
  if (d.dims.length < MIN_DIMS) return fail(`最少要 ${MIN_DIMS} 个维度`)
  if (d.dims.some(x => !x.name.trim())) return fail('每个维度都要有名字')
  // 保存前把影响说清楚
  const removed = saved.value.dims.filter(x => !d.dims.some(y => y.key === x.key))
  const added = d.dims.filter(x => !x.key).length
  const lines = ['改名不影响已填的数值；删掉的维度数值会保留但不显示，加回来还在。']
  if (removed.length) lines.push(`这次删掉：${removed.map(x => `「${x.name}」`).join('')}（OC 填的值保留，只是不显示）`)
  if (added) lines.push(`这次新加 ${added} 个维度：已经填过约战面板的 OC 这几项会显示「?」，需要作者补填。`)
  if (!confirm(lines.join('\n\n') + '\n\n确定保存吗？')) return
  saving.value = true
  try {
    saved.value = { ...(await ocPanelApi.saveTemplate({ title: d.title.trim(), grades: d.grades, dims: d.dims.map(x => ({ key: x.key, name: x.name.trim(), hint: (x.hint ?? '').trim() || null })) })), canEdit: true }
    draft.value = toDraft(saved.value)
    messageIsError.value = false
    message.value = `已保存（v${saved.value.version}）。约战页、OC 编辑页刷新后就用新模板。`
  } catch (err) {
    fail(panelError(err, '保存失败，请重试（修改还在）'))
  } finally {
    saving.value = false
  }
}
function fail(text: string) { messageIsError.value = true; message.value = text }
</script>

<style scoped>
.bpt-admin { display: grid; gap: 18px; color: var(--text-primary); }
.bpt-header { display: flex; justify-content: space-between; align-items: flex-end; gap: 20px; flex-wrap: wrap; }
.bpt-header h1 { margin: 0; font-size: 26px; letter-spacing: -.03em; }
.bpt-header p { margin: 8px 0 0; color: var(--text-secondary); font-size: 13px; max-width: 640px; }
.bpt-actions { display: flex; gap: 10px; }
.bpt-safe { margin: 0; padding: 10px 14px; border-left: 3px solid var(--accent-color); background: var(--accent-soft); font-size: 13px; line-height: 1.7; }
.bpt-message { margin: 0; padding: 10px 14px; border-left: 3px solid var(--signal-mint, var(--accent-color)); background: var(--bg-sub); font-size: 13px; }
.bpt-message.error { border-left-color: var(--danger, #c0392b); background: var(--danger-soft, rgba(192,57,43,.08)); }
.bpt-state { color: var(--text-secondary); }
.bpt-body { display: flex; flex-wrap: wrap; gap: 20px; align-items: flex-start; }
.bpt-form { flex: 2 1 420px; min-width: 0; margin: 0; display: grid; gap: 18px; padding: 18px 20px; border: 1px solid var(--border-line); border-top: 2px solid var(--accent-color); background: var(--paper-card); }
.bpt-preview { flex: 1 1 280px; min-width: 0; display: grid; gap: 8px; padding: 14px; border: 1px dashed var(--border-line); background: var(--paper-card); position: sticky; top: 16px; }
.bpt-preview-title { margin: 0; font-size: 12px; color: var(--text-secondary); letter-spacing: .1em; }
.bpt-field { display: grid; gap: 6px; min-width: 0; font-size: 13px; }
.bpt-field > span { color: var(--text-secondary); font-size: 12px; letter-spacing: .04em; }
.bpt-note { color: var(--text-secondary); font-size: 12px; margin: 0; }
input:not([type='checkbox']) { box-sizing: border-box; width: 100%; min-width: 0; padding: 7px 9px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-bg); color: var(--text-primary); font: inherit; font-size: 13px; }
input:focus { outline: none; border-color: var(--accent-color); }
.bpt-grades { display: flex; flex-wrap: wrap; gap: 6px; }
.bpt-grade { display: flex; align-items: center; gap: 4px; padding: 4px 10px; border: 1px solid var(--border-line); background: var(--paper-bg); cursor: pointer; }
.bpt-grade b { font-size: 14px; }
.bpt-grade small { color: var(--text-secondary); font-size: 11px; }
.bpt-grade input { accent-color: var(--accent-color); }
.bpt-dims { list-style: none; margin: 0; padding: 0; display: grid; gap: 4px; }
.bpt-dim { display: grid; grid-template-columns: 20px minmax(90px, 1fr) minmax(120px, 2fr) auto auto; gap: 8px; align-items: center; padding: 6px 8px; background: var(--bg-sub); }
.bpt-no { color: var(--text-secondary); font-size: 12px; text-align: center; }
.bpt-name { font-weight: 600; }
.bpt-key { color: var(--text-secondary); font: 11px var(--font-mono, monospace); white-space: nowrap; cursor: help; }
.bpt-ops { display: flex; gap: 2px; }
.bpt-ops button { padding: 3px 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.bpt-ops button:disabled { opacity: .4; cursor: default; }
.bpt-ops button:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); }
.bpt-ops .del:hover:not(:disabled) { border-color: var(--danger, #c0392b); color: var(--danger, #c0392b); }
.bpt-add { justify-self: start; }
.bpt-retired { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: 8px; }
.bpt-retired li { display: flex; align-items: center; gap: 8px; padding: 4px 4px 4px 10px; border: 1px dashed var(--border-line); }
.bpt-retired small { color: var(--text-secondary); font: 11px var(--font-mono, monospace); }
@media (max-width: 720px) {
  .bpt-dim { grid-template-columns: 20px minmax(0, 1fr) auto; }
  .bpt-hint { grid-column: 2 / -1; }
  .bpt-key { grid-column: 2; }
  .bpt-preview { position: static; }
}
</style>
