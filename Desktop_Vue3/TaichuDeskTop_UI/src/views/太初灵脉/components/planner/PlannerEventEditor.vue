<template>
  <div class="pe-mask" @click.self="emit('close')">
    <form class="pe-panel" role="dialog" aria-modal="true" :aria-label="title" @submit.prevent="submit" @keydown.esc="emit('close')">
      <header class="pe-head">
        <h3>{{ title }}</h3>
        <button type="button" class="pe-x" aria-label="关闭" @click="emit('close')">✕</button>
      </header>

      <fieldset class="pe-body" :disabled="readOnly || busy">
        <label class="pe-field">
          <span>标题</span>
          <input ref="titleInput" v-model="form.title" maxlength="100" placeholder="要做什么？比如：交房租、和朋友吃饭" required />
        </label>

        <div class="pe-row">
          <label class="pe-field">
            <span>日期</span>
            <input v-model="form.date" type="date" required />
          </label>
          <label class="pe-check pe-allday">
            <input v-model="form.allDay" type="checkbox" />全天
          </label>
        </div>

        <div v-if="!form.allDay" class="pe-row">
          <label class="pe-field">
            <span>开始</span>
            <input v-model="form.startTime" type="time" required />
          </label>
          <label class="pe-field">
            <span>结束（可不填）</span>
            <input v-model="form.endTime" type="time" />
          </label>
        </div>

        <div class="pe-row">
          <label class="pe-field">
            <span>分类</span>
            <input v-model="form.category" list="pe-category-list" maxlength="20" placeholder="生活 / 工作 / 学习…可以自己写" @change="onCategoryChange" />
            <datalist id="pe-category-list">
              <option v-for="c in allCategories" :key="c" :value="c" />
            </datalist>
          </label>
          <label class="pe-field">
            <span>地点（可不填）</span>
            <input v-model="form.location" maxlength="100" placeholder="在哪儿" />
          </label>
        </div>

        <div class="pe-field">
          <span>颜色</span>
          <div class="pe-swatches">
            <button
              v-for="c in COLOR_SWATCHES"
              :key="c"
              type="button"
              class="pe-swatch"
              :class="{ on: form.color === c }"
              :style="{ background: c }"
              :aria-label="`颜色 ${c}`"
              :aria-pressed="form.color === c"
              @click="pickColor(c)"
            ></button>
            <input v-model="form.color" type="color" class="pe-color" aria-label="自定义颜色" @input="colorTouched = true" />
          </div>
        </div>

        <label class="pe-field">
          <span>备忘</span>
          <textarea v-model="form.memo" rows="4" maxlength="5000" placeholder="要带什么、注意什么，都可以写在这里"></textarea>
        </label>

        <div class="pe-row">
          <label class="pe-field">
            <span>重复</span>
            <select v-model="form.repeat">
              <option v-for="o in REPEAT_OPTIONS" :key="o.value" :value="o.value">{{ o.label }}</option>
            </select>
          </label>
          <label v-if="form.repeat !== 'none'" class="pe-field">
            <span>重复到哪天（可不填）</span>
            <input v-model="form.repeatUntil" type="date" :min="form.date" />
          </label>
        </div>

        <div class="pe-row">
          <label class="pe-field">
            <span>提醒</span>
            <select v-model="form.remind" @change="onRemindChange">
              <option v-for="o in REMIND_OPTIONS" :key="o.value" :value="o.value">{{ o.label }}</option>
            </select>
          </label>
          <label v-if="form.remind === 'custom'" class="pe-field">
            <span>在什么时候提醒</span>
            <input v-model="form.remindCustom" type="datetime-local" required />
          </label>
        </div>
        <p v-if="form.remind !== 'none' && form.allDay" class="pe-hint">全天的安排按当天早上 9 点算（比如「提前 1 天」= 前一天早上 9 点提醒）。</p>

        <div v-if="form.remind !== 'none'" class="pe-field">
          <span>提醒方式（可多选）</span>
          <div class="pe-channels">
            <label v-for="c in CHANNEL_OPTIONS" :key="c.value" class="pe-check" :class="{ locked: c.value === 'email' && !canEmail }">
              <input
                type="checkbox"
                :value="c.value"
                :checked="form.channels.includes(c.value)"
                :disabled="c.value === 'email' && !canEmail"
                @change="toggleChannel(c.value, ($event.target as HTMLInputElement).checked)"
              />
              <span>{{ c.label }}<small>{{ c.hint }}</small></span>
            </label>
          </div>
          <p v-if="!canEmail" class="pe-hint">
            邮件提醒是<router-link to="/sponsors" class="pe-link">{{ emailTier }}赞助专属</router-link>，站内通知和浏览器弹窗大家都能用。
          </p>
          <p v-else-if="form.channels.includes('email') && !hasEmailAddress" class="pe-hint warn">你的账号还没绑定邮箱，邮件发不出去。</p>
          <p v-if="form.channels.includes('browser') && browserPermission !== 'granted'" class="pe-hint">
            浏览器弹窗需要先点页面上方的「开启浏览器提醒」允许通知；没开的话只会在网页里弹一条提示。
          </p>
        </div>

        <label v-if="event" class="pe-check">
          <input v-model="form.isDone" type="checkbox" />
          {{ event.repeat !== 'none' ? '整个重复安排都已完成（以后不再提醒）' : '已完成' }}
        </label>
      </fieldset>

      <p v-if="localError || error" class="pe-error" role="alert">{{ localError || error }}</p>

      <footer class="pe-foot">
        <button v-if="event && !readOnly" type="button" class="pe-btn danger" :disabled="busy" @click="emit('remove')">删除</button>
        <span class="pe-spacer"></span>
        <button type="button" class="pe-btn" @click="emit('close')">{{ readOnly ? '关闭' : '取消' }}</button>
        <button v-if="!readOnly" type="submit" class="pe-btn primary" :disabled="busy">{{ busy ? '保存中…' : '保存' }}</button>
      </footer>
    </form>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, reactive, ref } from 'vue'
import type { PlannerChannel, PlannerEvent, PlannerEventInput, PlannerRepeat } from '@/api/planner'
import {
  CATEGORY_PRESETS, CHANNEL_OPTIONS, COLOR_SWATCHES, DEFAULT_COLOR, REMIND_OPTIONS, REPEAT_OPTIONS,
  browserTzOffset, pad, timeToMin, type RemindChoice,
} from './plannerModel'

const props = defineProps<{
  /** 为空 = 新建 */
  event: PlannerEvent | null
  initialDate: string
  initialStart: string | null
  readOnly: boolean
  canEmail: boolean
  emailTier: string
  hasEmailAddress: boolean
  browserPermission: string
  /** 用户以前用过的分类 */
  categories: string[]
  busy: boolean
  error: string
}>()

const emit = defineEmits<{
  (e: 'save', input: PlannerEventInput): void
  (e: 'remove'): void
  (e: 'close'): void
}>()

const title = computed(() => props.readOnly ? '查看安排' : props.event ? '编辑安排' : '新建安排')
const allCategories = computed(() => Array.from(new Set([...CATEGORY_PRESETS.map(c => c.name), ...props.categories.filter(Boolean)])))

const addMinutes = (t: string, m: number) => {
  const total = Math.min((timeToMin(t) ?? 0) + m, 23 * 60 + 59)
  return `${pad(Math.floor(total / 60))}:${pad(total % 60)}`
}

/** 新建时默认的开始时间：点了某个钟点就用它；今天就用下一个整点；其他日子早上 9 点 */
const defaultStart = () => {
  if (props.initialStart) return props.initialStart
  const now = new Date()
  const today = `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
  if (props.initialDate === today && now.getHours() < 23) return `${pad(now.getHours() + 1)}:00`
  return '09:00'
}

interface FormState {
  title: string; date: string; allDay: boolean; startTime: string; endTime: string
  category: string; location: string; color: string; memo: string
  repeat: PlannerRepeat; repeatUntil: string
  remind: RemindChoice; remindCustom: string
  channels: PlannerChannel[]; isDone: boolean
}

function initialForm(): FormState {
  const ev = props.event
  if (!ev) {
    const start = defaultStart()
    return {
      title: '', date: props.initialDate, allDay: false, startTime: start, endTime: addMinutes(start, 60),
      category: '', location: '', color: DEFAULT_COLOR, memo: '', repeat: 'none', repeatUntil: '',
      remind: 'none', remindCustom: '', channels: [], isDone: false,
    }
  }
  let remind: RemindChoice = 'none'
  let remindCustom = ''
  if (ev.remindAt) { remind = 'custom'; remindCustom = ev.remindAt }
  else if (ev.remindOffset !== null) {
    const hit = REMIND_OPTIONS.find(o => o.value === String(ev.remindOffset))
    if (hit) remind = hit.value
    else {
      // 不是常用选项的提前量：换算成自定义时间显示
      const base = new Date(`${ev.date}T${ev.allDay || !ev.startTime ? '09:00' : ev.startTime}`)
      base.setMinutes(base.getMinutes() - ev.remindOffset)
      remind = 'custom'
      remindCustom = `${base.getFullYear()}-${pad(base.getMonth() + 1)}-${pad(base.getDate())}T${pad(base.getHours())}:${pad(base.getMinutes())}`
    }
  }
  return {
    title: ev.title, date: ev.date, allDay: ev.allDay, startTime: ev.startTime || '09:00',
    endTime: ev.endTime || '', category: ev.category, location: ev.location,
    color: ev.color || CATEGORY_PRESETS.find(c => c.name === ev.category)?.color || DEFAULT_COLOR,
    memo: ev.memo, repeat: ev.repeat, repeatUntil: ev.repeatUntil || '',
    remind, remindCustom, channels: ev.channels.filter(c => c !== 'email' || props.canEmail), isDone: ev.isDone,
  }
}

const form = reactive<FormState>(initialForm())
/** 用户自己挑过颜色后，换分类就不再自动改颜色 */
const colorTouched = ref(!!props.event?.color)
const localError = ref('')
const titleInput = ref<HTMLInputElement | null>(null)

const pickColor = (c: string) => { form.color = c; colorTouched.value = true }
const onCategoryChange = () => {
  const preset = CATEGORY_PRESETS.find(c => c.name === form.category.trim())
  if (preset && !colorTouched.value) form.color = preset.color
}
/** 第一次选提醒时，默认勾上站内通知和浏览器弹窗 */
const onRemindChange = () => {
  if (form.remind !== 'none' && form.channels.length === 0) form.channels = ['site', 'browser']
  if (form.remind === 'custom' && !form.remindCustom) form.remindCustom = `${form.date}T${form.allDay ? '09:00' : form.startTime || '09:00'}`
}
const toggleChannel = (c: PlannerChannel, on: boolean) => {
  form.channels = on ? Array.from(new Set([...form.channels, c])) : form.channels.filter(x => x !== c)
}

function submit() {
  if (props.readOnly) return
  localError.value = ''
  const t = form.title.trim()
  if (!t) { localError.value = '先写个标题吧'; return }
  if (!form.date) { localError.value = '请选日期'; return }
  if (!form.allDay) {
    if (!form.startTime) { localError.value = '请填开始时间，或者勾选「全天」'; return }
    if (form.endTime && (timeToMin(form.endTime) ?? 0) < (timeToMin(form.startTime) ?? 0)) { localError.value = '结束时间不能早于开始时间'; return }
  }
  if (form.remind === 'custom' && !form.remindCustom) { localError.value = '请选提醒时间'; return }
  if (form.remind !== 'none' && form.channels.length === 0) { localError.value = '至少选一种提醒方式，或者把提醒改成「不提醒」'; return }
  const input: PlannerEventInput = {
    title: t,
    date: form.date,
    allDay: form.allDay,
    startTime: form.allDay ? null : form.startTime,
    endTime: form.allDay || !form.endTime ? null : form.endTime,
    color: form.color,
    category: form.category.trim(),
    memo: form.memo,
    location: form.location.trim(),
    repeat: form.repeat,
    repeatUntil: form.repeat !== 'none' && form.repeatUntil ? form.repeatUntil : null,
    remindOffset: form.remind === 'none' || form.remind === 'custom' ? null : Number(form.remind),
    // datetime-local 可能带秒，只留到分钟
    remindAt: form.remind === 'custom' ? form.remindCustom.slice(0, 16) : null,
    channels: form.remind === 'none' ? [] : form.channels,
    tzOffset: browserTzOffset(),
  }
  if (props.event) input.isDone = form.isDone
  emit('save', input)
}

onMounted(() => { if (!props.readOnly) nextTick(() => titleInput.value?.focus()) })
</script>

<style scoped>
.pe-mask { position: fixed; inset: 0; z-index: 2000; background: var(--overlay, rgba(0, 0, 0, .4)); display: flex; align-items: center; justify-content: center; padding: 16px; }
.pe-panel { width: min(600px, 100%); max-height: calc(100vh - 32px); display: flex; flex-direction: column; background: var(--paper-card); color: var(--text-primary); border: 1px solid var(--border-line); box-shadow: 0 16px 48px rgba(0, 0, 0, .2); border-radius: 0; }
.pe-head { display: flex; align-items: center; justify-content: space-between; padding: 14px 18px; border-bottom: 1px solid var(--border-line); }
.pe-head h3 { margin: 0; font-size: 16px; font-weight: 800; }
.pe-x { background: none; border: none; color: var(--text-secondary); font-size: 14px; cursor: pointer; width: 32px; height: 32px; }
.pe-x:hover { color: var(--accent-color); }
.pe-body { border: none; margin: 0; padding: 14px 18px; overflow-y: auto; display: flex; flex-direction: column; gap: 12px; min-width: 0; }
.pe-row { display: flex; gap: 12px; align-items: flex-end; flex-wrap: wrap; }
.pe-row > .pe-field { flex: 1 1 180px; }
.pe-field { display: flex; flex-direction: column; gap: 4px; min-width: 0; }
.pe-field > span { font-size: 12px; font-weight: 700; color: var(--text-secondary); }
.pe-field input:not([type="checkbox"]):not([type="color"]), .pe-field select, .pe-field textarea {
  width: 100%; box-sizing: border-box; min-height: 34px; padding: 6px 10px; font: inherit; font-size: 14px;
  color: var(--text-primary); background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 0; outline: none;
}
.pe-field textarea { resize: vertical; line-height: 1.6; }
.pe-field input:focus, .pe-field select:focus, .pe-field textarea:focus { border-color: var(--accent-color); box-shadow: 0 0 0 2px var(--accent-soft); }
.pe-check { display: inline-flex; align-items: flex-start; gap: 6px; font-size: 13px; cursor: pointer; }
.pe-check input { margin: 2px 0 0; accent-color: var(--accent-color); }
.pe-check small { display: block; font-size: 11px; color: var(--text-secondary); }
.pe-check.locked { opacity: .55; cursor: not-allowed; }
.pe-allday { min-height: 34px; align-items: center; flex: 0 0 auto; }
.pe-allday input { margin: 0; }
.pe-swatches { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; }
.pe-swatch { width: 24px; height: 24px; border: 2px solid var(--paper-card); outline: 1px solid var(--border-line); cursor: pointer; padding: 0; border-radius: 0; }
.pe-swatch.on { outline: 2px solid var(--text-primary); }
.pe-color { width: 30px; height: 26px; border: 1px solid var(--border-line); background: none; padding: 0; cursor: pointer; }
.pe-channels { display: flex; flex-wrap: wrap; gap: 10px 18px; }
.pe-hint { margin: 0; font-size: 12px; color: var(--text-secondary); line-height: 1.6; }
.pe-hint.warn { color: var(--warning, #c77700); }
.pe-link { color: var(--accent-color); font-weight: 700; margin: 0 2px; }
.pe-error { margin: 0; padding: 8px 18px; font-size: 13px; color: var(--danger, #d33); background: color-mix(in srgb, var(--danger, #d33) 10%, transparent); }
.pe-foot { display: flex; align-items: center; gap: 8px; padding: 12px 18px; border-top: 1px solid var(--border-line); }
.pe-spacer { flex: 1; }
.pe-btn { min-height: 34px; padding: 0 16px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); border-radius: 0; cursor: pointer; font-size: 13px; font-weight: 600; }
.pe-btn:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); }
.pe-btn.primary { background: var(--accent-color); border-color: var(--accent-color); color: var(--paper-card); }
.pe-btn.primary:hover:not(:disabled) { opacity: .88; color: var(--paper-card); }
.pe-btn.danger { color: var(--danger, #d33); }
.pe-btn:disabled { opacity: .5; cursor: default; }
@media (max-width: 640px) {
  .pe-mask { padding: 0; align-items: stretch; }
  .pe-panel { width: 100%; max-height: none; height: 100%; border: none; }
}
</style>
