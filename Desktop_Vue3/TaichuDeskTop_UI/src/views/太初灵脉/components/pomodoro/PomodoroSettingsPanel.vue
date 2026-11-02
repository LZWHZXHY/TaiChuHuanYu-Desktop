<template>
  <section class="pset-root" aria-label="番茄钟设置">
    <div class="pset-grid">
      <label class="pset-field">
        <span>专注（分钟）</span>
        <input v-model.number="form.focusMin" type="number" min="1" max="240" inputmode="numeric" @change="save" />
      </label>
      <label class="pset-field">
        <span>短休息（分钟）</span>
        <input v-model.number="form.shortMin" type="number" min="1" max="120" inputmode="numeric" @change="save" />
      </label>
      <label class="pset-field">
        <span>长休息（分钟）</span>
        <input v-model.number="form.longMin" type="number" min="1" max="240" inputmode="numeric" @change="save" />
      </label>
      <label class="pset-field">
        <span>几轮一次长休息</span>
        <input v-model.number="form.longEvery" type="number" min="0" max="12" inputmode="numeric" @change="save" />
        <small>填 0 = 不要长休息</small>
      </label>
    </div>
    <div class="pset-checks">
      <label class="pset-check"><input v-model="form.autoBreak" type="checkbox" @change="save" /> 专注结束后自动开始休息</label>
      <label class="pset-check"><input v-model="form.autoFocus" type="checkbox" @change="save" /> 休息结束后自动开始专注</label>
    </div>
    <p class="pset-hint">设置跟着账号走，桌面程序也会用同样的时长。改了时长，正在计时的这一段不受影响，下一段开始生效。</p>
  </section>
</template>

<script setup lang="ts">
import { reactive, watch } from 'vue'
import type { PomoSettings } from '@/api/pomodoro'
import { normalizeSettings } from './pomodoroModel'

const props = defineProps<{ settings: PomoSettings }>()
const emit = defineEmits<{ (e: 'update', settings: PomoSettings): void }>()

// 表单是一份副本：输入到一半（比如清空了准备重填）不会马上影响计时
const form = reactive<PomoSettings>({ ...props.settings })
// 另一端改了设置时，表单跟着变
watch(() => props.settings, s => { Object.assign(form, s) }, { deep: true })

function save() {
  // 超出范围的数字自动拉回范围内（和后端一致），再把整理后的值放回表单
  const s = normalizeSettings(form)
  Object.assign(form, s)
  const old = props.settings
  const same = (Object.keys(s) as (keyof PomoSettings)[]).every(k => s[k] === old[k])
  if (!same) emit('update', s)
}
</script>

<style scoped>
.pset-root { display: flex; flex-direction: column; gap: 12px; }
.pset-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 10px; }
.pset-field { display: flex; flex-direction: column; gap: 4px; font-size: 12px; color: var(--text-secondary); font-weight: 700; min-width: 0; }
.pset-field input { min-height: 34px; padding: 0 10px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); border-radius: 0; font: 600 15px var(--font-mono, monospace); width: 100%; box-sizing: border-box; }
.pset-field input:focus { outline: none; border-color: var(--accent-color); box-shadow: 0 0 0 2px var(--accent-soft); }
.pset-field small { font-weight: 400; font-size: 11px; }
.pset-checks { display: flex; flex-wrap: wrap; gap: 8px 20px; }
.pset-check { display: inline-flex; align-items: center; gap: 6px; font-size: 13px; color: var(--text-primary); cursor: pointer; }
.pset-check input { accent-color: var(--accent-color); width: 15px; height: 15px; margin: 0; }
.pset-hint { margin: 0; font-size: 12px; color: var(--text-secondary); }
@media (max-width: 640px) {
  .pset-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
}
</style>
