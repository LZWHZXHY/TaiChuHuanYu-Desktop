<!-- 编年史：这个世界的历法。纪元（正着数 / 倒着数）、一年几个月每月几天、「年」叫什么。 -->
<template>
  <div class="ce-mask" @mousedown.self="tryClose">
    <form class="ce" role="dialog" aria-modal="true" aria-label="历法设置" @submit.prevent="save" @keydown.esc="tryClose">
      <header class="ce-head">
        <h3>⚙ 历法设置</h3>
        <button type="button" class="ce-x" aria-label="关闭" @click="tryClose">×</button>
      </header>
      <div class="ce-body">
        <div class="ce-field">
          <span>从模板开始 <small>选了之后还可以改</small></span>
          <div class="ce-kinds">
            <button type="button" @click="preset('gregorian')">公历<small>公元 / 公元前，12 个月</small></button>
            <button type="button" @click="preset('ages')">纪元制<small>第一纪元、第二纪元……</small></button>
            <button type="button" @click="preset('yearsOnly')">只有年份<small>不用月和日</small></button>
          </div>
        </div>

        <div class="ce-field">
          <span>纪元 <small>所有纪元排在同一根数轴上，「起点」就是这个纪元第 1 年在数轴上的位置</small></span>
          <ol class="cal-list">
            <li v-for="(era, i) in cal.eras" :key="era.id" class="cal-era">
              <input v-model="era.name" maxlength="30" required placeholder="纪元名称" :aria-label="`第 ${i + 1} 个纪元的名称`" />
              <label class="cal-inline">起点 <input v-model.number="era.start" type="number" step="1" required :aria-label="`${era.name} 的起点`" /></label>
              <label class="cal-inline" title="像「公元前」：第 1 年在最后，越早数字越大"><input v-model="era.backward" type="checkbox" /> 倒着数</label>
              <input :value="era.color || '#2a7f8f'" type="color" class="cal-color" :aria-label="`${era.name} 的颜色`" @input="era.color = ($event.target as HTMLInputElement).value" />
              <button type="button" class="ce-ghost cal-del" :disabled="cal.eras.length <= 1" :aria-label="`删除 ${era.name}`" @click="removeEra(i)">×</button>
              <small class="cal-note">{{ eraNote(era) }}</small>
            </li>
          </ol>
          <div class="ce-chips">
            <button type="button" class="ce-dashed" @click="addEra">＋ 加一个纪元</button>
            <span v-if="lastForward" class="cal-inline">接在「{{ lastForward.name }}」后面，它一共有 <input v-model.number="prevLength" type="number" min="1" step="1" class="cal-len" aria-label="上一个纪元有多少年" /> {{ cal.yearUnit }}</span>
          </div>
        </div>

        <div class="ce-row2">
          <label class="ce-field">
            <span>「年」叫什么</span>
            <input v-model="cal.yearUnit" maxlength="6" placeholder="年 / 载 / 周期" />
          </label>
          <div class="ce-field">
            <span>轴上的空白</span>
            <label class="ce-check"><input v-model="cal.autoCompress" type="checkbox" /> 自动折叠大段空白（几千年什么都没发生的时候）</label>
          </div>
        </div>

        <div class="ce-field">
          <label class="ce-check"><input v-model="cal.useMonths" type="checkbox" /> <b>使用月份和日期</b></label>
          <template v-if="cal.useMonths">
            <ol class="cal-months">
              <li v-for="(m, i) in cal.months" :key="i">
                <input v-model="m.name" maxlength="20" required :aria-label="`第 ${i + 1} 个月的名称`" />
                <input v-model.number="m.days" type="number" min="1" max="10000" required :aria-label="`${m.name} 有几天`" />
                <span>天</span>
                <button type="button" class="ce-ghost cal-del" :disabled="cal.months.length <= 1" :aria-label="`删除 ${m.name}`" @click="cal.months.splice(i, 1)">×</button>
              </li>
            </ol>
            <div class="ce-chips">
              <button type="button" class="ce-dashed" @click="cal.months.push({ name: `${cal.months.length + 1}月`, days: 30 })">＋ 加一个月</button>
              <small class="cal-note">一年 {{ cal.months.length }} 个月、{{ totalDays }} 天</small>
            </div>
          </template>
        </div>
        <p class="ce-hint">事件按「纪元 + 年」保存。改了某个纪元的起点，这个纪元里的事件会整体跟着移动；删掉的纪元里的事件会归到「{{ fallbackName }}」。</p>
        <p v-if="error" class="ce-error" role="alert">{{ error }}</p>
      </div>
      <footer class="ce-foot">
        <span class="ce-sp"></span>
        <button type="button" class="ce-ghost" @click="tryClose">取消</button>
        <button type="submit" class="ce-primary" :disabled="busy">{{ busy ? '保存中…' : '保存' }}</button>
      </footer>
    </form>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { chronicleApi } from '@/api/chronicle'
import { defaultCalendar, normalizeCalendar, DEFAULT_MONTHS, newLocalId, type Calendar, type ChronicleEvent, type Era } from '@/utils/chronicle'

const props = defineProps<{ projectId: string; calendar: Calendar; events: ChronicleEvent[] }>()
const emit = defineEmits<{ close: []; saved: [cal: Calendar] }>()
const cal = ref<Calendar>(JSON.parse(JSON.stringify(props.calendar)))
const initial = JSON.stringify(cal.value)
const busy = ref(false)
const error = ref('')
const prevLength = ref(1000)

const totalDays = computed(() => cal.value.months.reduce((s, m) => s + (Number(m.days) || 0), 0))
const lastForward = computed(() => cal.value.eras.filter(e => !e.backward).sort((a, b) => b.start - a.start)[0])
const fallbackName = computed(() => (cal.value.eras.find(e => !e.backward) ?? cal.value.eras[0])?.name ?? '')
const used = (id: string) => props.events.filter(e => e.eraId === id || e.endEraId === id).length

function eraNote(era: Era) {
  const s = Number(era.start) || 0
  const n = used(era.id)
  const tail = n ? ` · ${n} 件事用了这个纪元` : ''
  if (era.backward) return `倒着数：${era.name} 1 ${cal.value.yearUnit} 在数轴 ${s}，${era.name} 100 ${cal.value.yearUnit} 在数轴 ${s - 99}${tail}`
  const next = cal.value.eras.filter(e => !e.backward && Number(e.start) > s).sort((a, b) => a.start - b.start)[0]
  return `${era.name} 1 ${cal.value.yearUnit} 在数轴 ${s}${next ? `，到 ${next.name} 开始前共 ${Number(next.start) - s} ${cal.value.yearUnit}` : ''}${tail}`
}
function addEra() {
  const last = lastForward.value
  const start = last ? Number(last.start) + Math.max(1, Math.trunc(Number(prevLength.value) || 1)) : 1
  cal.value.eras.push({ id: newLocalId('era-'), name: `第${cal.value.eras.length + 1}纪元`, start })
}
function removeEra(i: number) {
  const era = cal.value.eras[i]!
  const n = used(era.id)
  if (n && !window.confirm(`有 ${n} 件事用了「${era.name}」，删掉以后它们会归到别的纪元，位置可能会变。确定删除？`)) return
  cal.value.eras.splice(i, 1)
}
function preset(kind: 'gregorian' | 'ages' | 'yearsOnly') {
  if (JSON.stringify(cal.value) !== initial && !window.confirm('用模板替换现在的设置？')) return
  // 保留已有纪元的 id，事件不会丢纪元
  const keep = (i: number) => cal.value.eras[i]?.id
  if (kind === 'gregorian') {
    const d = defaultCalendar()
    d.eras = d.eras.map((e, i) => ({ ...e, id: keep(i) ?? e.id }))
    cal.value = d
  } else if (kind === 'ages') {
    cal.value = { ...cal.value, eras: ['第一纪元', '第二纪元', '第三纪元'].map((name, i) => ({ id: keep(i) ?? newLocalId('era-'), name, start: 1 + i * 1000 })), useMonths: true, months: DEFAULT_MONTHS.map(m => ({ ...m })) }
  } else {
    cal.value = { ...cal.value, useMonths: false }
  }
}
async function save() {
  error.value = ''
  const names = cal.value.eras.map(e => e.name.trim())
  if (names.some(n => !n)) { error.value = '纪元名称要填'; return }
  if (new Set(names).size !== names.length) { error.value = '纪元名称不能重复'; return }
  const clean = normalizeCalendar({ ...cal.value, eras: cal.value.eras.map(e => ({ ...e, name: e.name.trim(), color: e.color || undefined })) })
  busy.value = true
  try {
    await chronicleApi.saveCalendar(props.projectId, clean)
    emit('saved', clean)
  } catch (e: any) { error.value = e?.response?.data?.message || '保存失败，稍后再试' }
  finally { busy.value = false }
}
function tryClose() {
  if (JSON.stringify(cal.value) !== initial && !window.confirm('改动还没保存，确定关闭？')) return
  emit('close')
}
</script>

<style scoped src="./ChronicleEditor.css"></style>
<style scoped>
.cal-list, .cal-months { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.cal-era { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; padding: 8px; background: var(--bg-sub); }
.cal-era > input:first-child { flex: 1 1 140px; }
.cal-inline { display: inline-flex; align-items: center; gap: 4px; font-size: 13px; }
.cal-inline input[type=number] { width: 100px; }
.cal-len { width: 80px !important; }
.cal-color { width: 32px; height: 28px; padding: 0 !important; }
.cal-del { padding: 2px 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); cursor: pointer; }
.cal-note { flex-basis: 100%; font-size: 12px; color: var(--text-secondary); }
.cal-months { grid-template-columns: repeat(auto-fill, minmax(190px, 1fr)); }
.cal-months li { display: flex; align-items: center; gap: 4px; font-size: 13px; }
.cal-months input:first-child { flex: 1; min-width: 0; }
.cal-months input[type=number] { width: 64px; }
.ce-chips small.cal-note { flex-basis: auto; }
</style>
