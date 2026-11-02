<!-- StatPanelEditor.vue：编辑「能力面板」。柴圈 OC 编辑页、世界观条目的「能力面板」区块共用。
     左边改：选模板（JOJO 替身面板 / 六维 / 自定义）、标题、刻度（字母等级或数值）、每个维度的名字和值、增删和调顺序、备注、综合评级；
     右边（窄屏时在下面）实时预览雷达图。
     用 v-model 传面板对象；每次修改都发出一个新对象（不直接改传进来的），外面存不存、什么时候存由外面决定。 -->
<template>
  <div class="spe">
    <div class="spe-form">
      <!-- 模板 -->
      <div class="spe-line">
        <span class="spe-label">模板</span>
        <div class="spe-seg" role="group" aria-label="选择模板">
          <button v-for="t in STAT_TEMPLATES" :key="t.id" type="button" :aria-pressed="panel.template === t.id" :title="t.hint" @click="pickTemplate(t.id)">{{ t.label }}</button>
        </div>
      </div>
      <!-- 换模板会替换现有维度：先问一句，免得辛苦填的值一下没了 -->
      <div v-if="pendingTemplate" class="spe-confirm" role="alertdialog" aria-label="确认套用模板">
        <span>套用「{{ templateLabel(pendingTemplate) }}」会替换现在的 {{ panel.dims.length }} 个维度（名字和值），确定吗？</span>
        <button type="button" class="spe-btn primary" @click="applyTemplate(pendingTemplate)">套用</button>
        <button type="button" class="spe-btn" @click="pendingTemplate = null">取消</button>
      </div>

      <label v-if="!hideTitle" class="spe-line">
        <span class="spe-label">标题</span>
        <input class="spe-input" :value="panel.title" :maxlength="STAT_LIMITS.title" :placeholder="DEFAULT_PANEL_TITLE" @input="patch({ title: inputValue($event) })" />
      </label>

      <!-- 刻度 -->
      <div class="spe-line">
        <span class="spe-label">刻度</span>
        <div class="spe-seg" role="group" aria-label="刻度类型">
          <button type="button" :aria-pressed="panel.scale === 'grade'" title="S、A～E（S 比 A 高一档），另有 ∞（超出上限）、?（未知）、—（无）" @click="setScale('grade')">字母等级</button>
          <button type="button" :aria-pressed="panel.scale === 'number'" title="0～满分的数值" @click="setScale('number')">数值</button>
        </div>
        <label v-if="panel.scale === 'number'" class="spe-max">满分
          <input class="spe-input num" type="number" min="1" :max="STAT_LIMITS.maxMax" step="1" :value="panel.max" aria-label="满分" @change="setMax(inputValue($event))" />
        </label>
      </div>

      <!-- 维度 -->
      <ol class="spe-dims">
        <li v-for="(d, i) in panel.dims" :key="i" class="spe-dim">
          <span class="spe-no" aria-hidden="true">{{ i + 1 }}</span>
          <input class="spe-input spe-name" :value="d.name" :maxlength="STAT_LIMITS.name" :placeholder="`维度${i + 1}`" :aria-label="`第 ${i + 1} 个维度的名字`"
            @input="setDim(i, { name: inputValue($event) })" />
          <div v-if="panel.scale === 'grade'" class="spe-grades" role="radiogroup" :aria-label="`${dimName(d, i)}的等级`">
            <button v-for="g in ALL_GRADES" :key="g" type="button" role="radio" :aria-checked="d.value === g" :class="{ special: !GRADES.includes(g) }"
              :title="`${g}：${GRADE_HINT[g]}`" @click="setDim(i, { value: g })">{{ g }}</button>
          </div>
          <div v-else class="spe-number">
            <input type="range" min="0" :max="panel.max" :step="panel.max <= 20 ? 0.5 : 1" :value="numberOf(d)" :aria-label="`${dimName(d, i)}的数值（拖动）`"
              @input="setDim(i, { value: clampNumber(inputValue($event), panel.max) })" />
            <input class="spe-input num" type="number" min="0" :max="panel.max" step="any" :value="numberOf(d)" :aria-label="`${dimName(d, i)}的数值`"
              @change="setDim(i, { value: clampNumber(inputValue($event), panel.max) })" />
          </div>
          <div class="spe-ops">
            <button type="button" :disabled="i === 0" :aria-label="`上移 ${dimName(d, i)}`" title="上移" @click="move(i, -1)">↑</button>
            <button type="button" :disabled="i === panel.dims.length - 1" :aria-label="`下移 ${dimName(d, i)}`" title="下移" @click="move(i, 1)">↓</button>
            <button type="button" class="del" :disabled="panel.dims.length <= STAT_LIMITS.minDims" :aria-label="`删除 ${dimName(d, i)}`"
              :title="panel.dims.length <= STAT_LIMITS.minDims ? `最少要 ${STAT_LIMITS.minDims} 个维度` : '删除这个维度'" @click="remove(i)">×</button>
          </div>
        </li>
      </ol>
      <div class="spe-add">
        <!-- 维度个数不设上限（只在后端有个防滥用的很宽的限制），这里不写「最多几个」 -->
        <button type="button" class="spe-btn" :disabled="panel.dims.length >= STAT_LIMITS.maxDims" @click="add">＋ 添加维度</button>
        <span class="spe-count">共 {{ panel.dims.length }} 个维度（最少 {{ STAT_LIMITS.minDims }} 个）</span>
      </div>

      <label class="spe-line top">
        <span class="spe-label">备注</span>
        <textarea class="spe-input" rows="2" :value="panel.note ?? ''" :maxlength="STAT_LIMITS.note" placeholder="可选：能力说明、数据出处……" @input="patch({ note: inputValue($event) })"></textarea>
      </label>
      <label class="spe-check">
        <input type="checkbox" :checked="!!panel.overall" @change="patch({ overall: ($event.target as HTMLInputElement).checked || undefined })" />
        显示综合评级<small>（自动算：{{ panel.scale === 'grade' ? '字母按 S=6、A=5…E=1 平均' : '数值按百分比平均' }}，显示成字母）</small>
      </label>
    </div>

    <div class="spe-preview">
      <p class="spe-preview-title">预览</p>
      <StatPanelChart :panel="panel" :color="color" :hide-title="hideTitle" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import StatPanelChart from './StatPanelChart.vue'
import {
  ALL_GRADES, DEFAULT_PANEL_TITLE, GRADES, GRADE_HINT, STAT_LIMITS, STAT_TEMPLATES,
  clampNumber, cleanStatPanel, convertScale, createStatPanel, defaultValue, dimName,
  type StatDim, type StatPanel, type StatScale, type StatTemplateId,
} from '@/utils/statPanel'

const props = withDefaults(defineProps<{
  modelValue: StatPanel
  /** 不编辑标题（世界观区块用区块自己的标题） */
  hideTitle?: boolean
  color?: string
}>(), { hideTitle: false, color: '' })
const emit = defineEmits<{ 'update:modelValue': [panel: StatPanel] }>()

const panel = computed(() => props.modelValue)
const inputValue = (e: Event) => (e.target as HTMLInputElement | HTMLTextAreaElement).value

/** 发出新对象（editing 模式整理：数值夹到范围内，但名字不去空格、不自动补，打字不会被打断） */
function commit(next: StatPanel) { emit('update:modelValue', cleanStatPanel(next, true) ?? next) }
function patch(change: Partial<StatPanel>) { commit({ ...panel.value, ...change }) }
function setDim(i: number, change: Partial<StatDim>) {
  commit({ ...panel.value, dims: panel.value.dims.map((d, k) => k === i ? { ...d, ...change } : d) })
}
const numberOf = (d: StatDim) => typeof d.value === 'number' ? d.value : 0

/**
 * 换刻度会换算数值（∞、?、— 换成数值后就分不出来了）。记住换之前的样子：
 * 如果换过去之后什么都没改又换回来，就直接恢复原样，不会因为来回点两下把 ∞ 变成 A。
 */
let beforeSwitch: { from: StatPanel; to: string } | null = null
function setScale(scale: StatScale) {
  if (scale === panel.value.scale) return
  if (beforeSwitch && beforeSwitch.from.scale === scale && beforeSwitch.to === JSON.stringify(panel.value)) {
    const from = beforeSwitch.from
    beforeSwitch = null
    return commit(from)
  }
  const from = panel.value
  const next = cleanStatPanel(convertScale(from, scale), true) ?? convertScale(from, scale)
  beforeSwitch = { from, to: JSON.stringify(next) }
  commit(next)
}
function setMax(raw: string) {
  const max = Math.round(Number(raw))
  if (!Number.isFinite(max) || max < 1) return commit({ ...panel.value })
  // 满分改了：已经填的值按比例换算（100 分制的 50 → 10 分制的 5），雷达图形状不变
  const next = Math.min(STAT_LIMITS.maxMax, max)
  const old = panel.value.max || 100
  commit({ ...panel.value, max: next, dims: panel.value.dims.map(d => typeof d.value === 'number' ? { ...d, value: clampNumber((d.value / old) * next, next) } : d) })
}

function move(i: number, dir: -1 | 1) {
  const j = i + dir
  if (j < 0 || j >= panel.value.dims.length) return
  const dims = [...panel.value.dims]
  ;[dims[i], dims[j]] = [dims[j], dims[i]]
  commit({ ...panel.value, dims })
}
function remove(i: number) {
  if (panel.value.dims.length <= STAT_LIMITS.minDims) return
  commit({ ...panel.value, dims: panel.value.dims.filter((_, k) => k !== i) })
}
function add() {
  if (panel.value.dims.length >= STAT_LIMITS.maxDims) return
  commit({ ...panel.value, dims: [...panel.value.dims, { name: '', value: defaultValue(panel.value.scale, panel.value.max) }] })
}

// ----- 模板 -----
const pendingTemplate = ref<StatTemplateId | null>(null)
const templateLabel = (id: StatTemplateId) => STAT_TEMPLATES.find(t => t.id === id)?.label ?? id
/** 现在的维度是不是还是某个模板的原样（没改过就不用问，直接换） */
function untouched() {
  const fresh = createStatPanel(panel.value.template ?? 'custom', panel.value.scale, panel.value.max)
  return JSON.stringify(panel.value.dims) === JSON.stringify(fresh.dims)
}
function pickTemplate(id: StatTemplateId) {
  if (id === panel.value.template && untouched()) return
  if (untouched()) applyTemplate(id)
  else pendingTemplate.value = id
}
function applyTemplate(id: StatTemplateId) {
  pendingTemplate.value = null
  const fresh = createStatPanel(id, panel.value.scale, panel.value.max)
  // 标题、备注、综合评级是用户自己设的，换模板不动它们
  commit({ ...fresh, title: panel.value.title, note: panel.value.note, overall: panel.value.overall })
}
</script>

<style scoped>
.spe { display: flex; flex-wrap: wrap; gap: 16px 20px; align-items: flex-start; min-width: 0; }
/* 容器查询：编辑器放在窄的地方（OC 编辑页、并排的半宽区块）时，按自己的宽度换成上下排，而不是按整个屏幕宽度 */
.spe-form { flex: 1 1 420px; min-width: 0; display: grid; gap: 10px; container-type: inline-size; }
.spe-preview { flex: 1 1 240px; max-width: 100%; min-width: 0; display: grid; gap: 6px; padding: 10px; border: 1px dashed var(--border-line); background: var(--paper-card); }
.spe-preview-title { margin: 0; font-size: 12px; color: var(--text-secondary); letter-spacing: .1em; }
.spe-line { display: flex; flex-wrap: wrap; align-items: center; gap: 6px 10px; min-width: 0; }
.spe-line.top { align-items: flex-start; }
.spe-label { flex: 0 0 auto; width: 3em; font-size: 13px; color: var(--text-secondary); }
.spe-line > .spe-input { flex: 1 1 180px; }
.spe-input { box-sizing: border-box; min-width: 0; padding: 5px 8px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; }
.spe-input:focus { outline: none; border-color: var(--accent-color); }
textarea.spe-input { resize: vertical; line-height: 1.6; }
.spe-input.num { width: 76px; }
.spe-max { display: flex; align-items: center; gap: 6px; font-size: 13px; color: var(--text-secondary); }
.spe-seg { display: flex; flex-wrap: wrap; }
.spe-seg button { padding: 4px 12px; border: 1px solid var(--border-line); margin-right: -1px; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.spe-seg button[aria-pressed='true'] { position: relative; border-color: var(--accent-color); background: var(--accent-soft); color: var(--accent-color); font-weight: 600; }
.spe-seg button:hover { color: var(--accent-color); }
.spe-confirm { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; padding: 8px 10px; border-left: 3px solid var(--warning, #e0a526); background: var(--bg-sub); font-size: 13px; color: var(--text-primary); }
.spe-btn { padding: 4px 12px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.spe-btn:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); }
.spe-btn:disabled { opacity: .45; cursor: default; }
.spe-btn.primary { border-color: var(--accent-color); background: var(--accent-color); color: #fff; }
.spe-dims { list-style: none; margin: 0; padding: 0; display: grid; gap: 4px; }
.spe-dim { display: grid; grid-template-columns: 20px minmax(70px, 7em) minmax(0, 1fr) auto; gap: 6px 8px; align-items: center; padding: 6px 8px; background: var(--bg-sub); }
.spe-no { font-size: 12px; color: var(--text-secondary); text-align: center; }
.spe-name { width: 100%; font-weight: 600; }
.spe-grades { display: flex; flex-wrap: wrap; gap: 2px; }
.spe-grades button { min-width: 26px; height: 28px; padding: 0 4px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; font-weight: 600; cursor: pointer; }
.spe-grades button.special { color: var(--text-secondary); font-weight: 400; }
.spe-grades button[aria-checked='true'] { border-color: var(--accent-color); background: var(--accent-color); color: #fff; }
.spe-grades button:hover:not([aria-checked='true']) { border-color: var(--accent-color); color: var(--accent-color); }
.spe-number { display: flex; align-items: center; gap: 8px; min-width: 0; }
.spe-number input[type='range'] { flex: 1; min-width: 60px; accent-color: var(--accent-color); }
.spe-ops { display: flex; gap: 2px; }
.spe-ops button { padding: 3px 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.spe-ops button:disabled { opacity: .4; cursor: default; }
.spe-ops button:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); }
.spe-ops .del:hover:not(:disabled) { border-color: var(--danger); color: var(--danger); }
.spe-add { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; }
.spe-count { font-size: 12px; color: var(--text-secondary); }
.spe-check { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; font-size: 13px; color: var(--text-primary); cursor: pointer; }
.spe-check small { color: var(--text-secondary); }
.spe-check input { accent-color: var(--accent-color); }
@container (max-width: 480px) {
  /* 窄的时候：名字和上下删按钮一行，等级按钮 / 滑块单独一行 */
  .spe-dim { grid-template-columns: 20px minmax(0, 1fr) auto; }
  .spe-grades, .spe-number { grid-column: 2 / -1; }
  .spe-ops { grid-row: 1; grid-column: 3; }
}
</style>
