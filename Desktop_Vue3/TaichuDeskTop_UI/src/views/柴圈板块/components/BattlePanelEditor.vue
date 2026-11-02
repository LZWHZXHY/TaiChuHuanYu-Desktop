<!-- BattlePanelEditor.vue：填写「约战面板」（官方约战模板）。维度和能选的等级都由后端模板定，这里只能给每个维度选等级。
     用 v-model 传 { 维度key: 等级 }；模板里已经去掉的旧 key 原样带着（不显示，也不会被清掉）。 -->
<template>
  <div class="bpe">
    <div class="bpe-form">
      <p class="bpe-tip">官方约战模板：所有 OC 用同一套维度，方便约战时对比。不确定的选「?」。</p>
      <ol class="bpe-dims">
        <li v-for="d in template.dims" :key="d.key" class="bpe-dim">
          <span class="bpe-name" :title="d.hint ?? undefined">{{ d.name }}<small v-if="d.hint">{{ d.hint }}</small></span>
          <div class="bpe-grades" role="radiogroup" :aria-label="`${d.name}的等级`">
            <button v-for="g in template.grades" :key="g" type="button" role="radio" :aria-checked="current(d.key) === g" :class="{ special: g === '?' }"
              :title="`${g}：${hintOf(g)}`" @click="set(d.key, g)">{{ g }}</button>
          </div>
        </li>
      </ol>
    </div>
    <div class="bpe-preview">
      <p class="bpe-preview-title">预览</p>
      <StatPanelChart :panel="preview" :color="color" hide-title />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import StatPanelChart from '@/components/StatPanelChart.vue'
import { GRADE_HINT, type Grade } from '@/utils/statPanel'
import { battleGrade, battleToStatPanel, type BattlePanelTemplate } from '../OCs/ocPanel'

const props = withDefaults(defineProps<{
  template: BattlePanelTemplate
  modelValue: Record<string, string>
  color?: string
}>(), { color: '' })
const emit = defineEmits<{ 'update:modelValue': [values: Record<string, string>] }>()

const data = computed(() => ({ values: props.modelValue, filled: true }))
const current = (key: string) => battleGrade(props.template, data.value, key)
const hintOf = (g: string) => GRADE_HINT[g as Grade] ?? ''
// 发出新对象：旧 key（模板里去掉的）原样保留
function set(key: string, grade: string) { emit('update:modelValue', { ...props.modelValue, [key]: grade }) }
const preview = computed(() => battleToStatPanel(props.template, data.value))
</script>

<style scoped>
.bpe { display: flex; flex-wrap: wrap; gap: 16px 20px; align-items: flex-start; min-width: 0; }
.bpe-form { flex: 1 1 320px; min-width: 0; display: grid; gap: 8px; }
.bpe-tip { margin: 0; font-size: 12px; color: var(--text-secondary); line-height: 1.6; }
.bpe-dims { list-style: none; margin: 0; padding: 0; display: grid; gap: 4px; }
.bpe-dim { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 6px 10px; padding: 6px 10px; background: var(--bg-sub); }
.bpe-name { display: grid; font-size: 14px; font-weight: 600; color: var(--text-primary); }
.bpe-name small { font-size: 11px; font-weight: 400; color: var(--text-secondary); }
.bpe-grades { display: flex; flex-wrap: wrap; gap: 2px; }
.bpe-grades button { min-width: 30px; height: 30px; padding: 0 4px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 14px; font-weight: 600; cursor: pointer; }
.bpe-grades button.special { color: var(--text-secondary); font-weight: 400; }
.bpe-grades button[aria-checked='true'] { border-color: var(--accent-color); background: var(--accent-color); color: #fff; }
.bpe-grades button:hover:not([aria-checked='true']) { border-color: var(--accent-color); color: var(--accent-color); }
.bpe-preview { flex: 1 1 240px; max-width: 100%; min-width: 0; display: grid; gap: 6px; padding: 10px; border: 1px dashed var(--border-line); background: var(--paper-card); }
.bpe-preview-title { margin: 0; font-size: 12px; color: var(--text-secondary); letter-spacing: .1em; }
</style>
