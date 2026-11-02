<!-- 编年史：新建 / 编辑一条时间线（主线、某国史、某人生平、「假如」支线……） -->
<template>
  <div class="ce-mask" @mousedown.self="tryClose">
    <form class="ce" role="dialog" aria-modal="true" :aria-label="isNew ? '新建时间线' : '编辑时间线'" @submit.prevent="save" @keydown.esc="tryClose">
      <header class="ce-head">
        <h3>{{ isNew ? (form.parentId ? '分出一条支线' : '新建时间线') : '编辑时间线' }}</h3>
        <button type="button" class="ce-x" aria-label="关闭" @click="tryClose">×</button>
      </header>
      <div class="ce-body">
        <label class="ce-field">
          <span>名称</span>
          <input v-model="form.name" maxlength="80" required placeholder="比如：正史 / 北境王国史 / 艾琳的一生 / 假如王子没有死" />
        </label>

        <div class="ce-field">
          <span>这是什么线</span>
          <div class="ce-kinds" role="radiogroup" aria-label="类型">
            <button v-for="(m, k) in STATUS_META" :key="k" type="button" role="radio" :aria-checked="form.status === k" :class="{ on: form.status === k }" @click="form.status = k">{{ m.label }}<small>{{ m.hint }}</small></button>
          </div>
        </div>

        <label class="ce-field">
          <span>从哪条线分出来 <small>不选就是独立的一条（比如各国各自的历史、不同人物的视角）</small></span>
          <select v-model="form.parentId">
            <option :value="null">— 独立的一条 —</option>
            <option v-for="l in parentChoices" :key="l.line.id" :value="l.line.id">{{ '　'.repeat(l.depth) }}{{ l.line.name }}</option>
          </select>
        </label>
        <label v-if="form.parentId" class="ce-field">
          <span>在哪件事分叉 <small>支线从这件事之后开始走另一条路</small></span>
          <select v-model="form.forkEventId">
            <option :value="null">— 不指定 —</option>
            <option v-for="e in forkChoices" :key="e.id" :value="e.id">{{ e.label }}</option>
          </select>
        </label>
        <label class="ce-field">
          <span>在哪件事汇合 <small>可选：支线最后又回到某条线（「殊途同归」）</small></span>
          <select v-model="form.mergeEventId">
            <option :value="null">— 不汇合 —</option>
            <option v-for="e in mergeChoices" :key="e.id" :value="e.id">{{ e.label }}</option>
          </select>
        </label>

        <div class="ce-row2">
          <div class="ce-field">
            <span>关于哪个条目 <small>人物生平、国家史……</small></span>
            <div class="ce-chips">
              <span v-if="form.cardId" class="ce-chip">{{ cardTitle(form.cardId) }}<button type="button" aria-label="清除" @click="form.cardId = null">×</button></span>
              <span class="ce-pick-wrap">
                <button type="button" class="ce-dashed" @click="picking = !picking">{{ form.cardId ? '换一个' : '＋ 选择条目' }}</button>
                <CardPicker v-if="picking" class="ce-picker" :project-id="projectId" @close="picking = false" @confirm="ids => { form.cardId = ids[ids.length - 1] ?? null; picking = false }" />
              </span>
            </div>
          </div>
          <div class="ce-field">
            <span>颜色</span>
            <div class="ce-colors" role="radiogroup" aria-label="颜色">
              <button type="button" role="radio" class="ce-color none" :aria-checked="!form.color" title="自动" @click="form.color = null">∅</button>
              <button v-for="c in LINE_COLORS" :key="c" type="button" role="radio" class="ce-color" :aria-checked="form.color === c" :style="{ background: c }" :aria-label="c" @click="form.color = c"></button>
            </div>
          </div>
        </div>

        <label class="ce-field">
          <span>说明 <small>可选</small></span>
          <textarea v-model="form.description" rows="2" maxlength="1000" placeholder="这条线讲的是什么"></textarea>
        </label>
        <label class="ce-check"><input v-model="form.hidden" type="checkbox" /> 对访客隐藏整条线（连同上面的事件）</label>
        <p v-if="error" class="ce-error" role="alert">{{ error }}</p>
      </div>
      <footer class="ce-foot">
        <button v-if="!isNew" type="button" class="ce-del" :disabled="busy" @click="remove">删除这条线</button>
        <template v-if="!isNew">
          <button type="button" class="ce-ghost" :disabled="busy" title="往上排" @click="emit('move', line!.id, -1)">↑</button>
          <button type="button" class="ce-ghost" :disabled="busy" title="往下排" @click="emit('move', line!.id, 1)">↓</button>
        </template>
        <span class="ce-sp"></span>
        <button type="button" class="ce-ghost" @click="tryClose">取消</button>
        <button type="submit" class="ce-primary" :disabled="busy">{{ busy ? '保存中…' : '保存' }}</button>
      </footer>
    </form>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { chronicleApi } from '@/api/chronicle'
import { useWorldStore } from '@/stores/world'
import { STATUS_META, LINE_COLORS, formatEventDate, plainTitle, eventStart, type Calendar, type ChronicleEvent, type ChronicleTimeline, type TimelineStatus } from '@/utils/chronicle'
import CardPicker from './CardPicker.vue'

const props = defineProps<{
  projectId: string
  calendar: Calendar
  lines: { line: ChronicleTimeline; depth: number }[]
  events: ChronicleEvent[]
  line: ChronicleTimeline | null
  preset?: Partial<ChronicleTimeline>
}>()
const emit = defineEmits<{ close: []; saved: [line: ChronicleTimeline]; deleted: [id: string]; move: [id: string, dir: -1 | 1] }>()
const store = useWorldStore()
const isNew = computed(() => !props.line)
const src: Partial<ChronicleTimeline> = props.line ?? props.preset ?? {}
const form = ref({
  name: src.name ?? '', description: src.description ?? '', color: src.color ?? null as string | null,
  status: (src.status ?? 'canon') as TimelineStatus, parentId: src.parentId ?? null as string | null,
  forkEventId: src.forkEventId ?? null as string | null, mergeEventId: src.mergeEventId ?? null as string | null,
  cardId: src.cardId ?? null as string | null, hidden: Boolean(src.hidden),
})
const initial = JSON.stringify(form.value)
const picking = ref(false)
const busy = ref(false)
const error = ref('')
const findCard = (id: string) => store.getCardById(id, props.projectId)
const cardTitle = (id: string) => findCard(id)?.title ?? '已删除的条目'

// 不能选自己和自己的后代当父线
const descendants = computed(() => {
  const out = new Set<string>()
  if (!props.line) return out
  out.add(props.line.id)
  let grew = true
  while (grew) {
    grew = false
    for (const { line } of props.lines) if (line.parentId && out.has(line.parentId) && !out.has(line.id)) { out.add(line.id); grew = true }
  }
  return out
})
const parentChoices = computed(() => props.lines.filter(l => !l.line.virtual && !descendants.value.has(l.line.id)))
const label = (e: ChronicleEvent) => {
  const line = props.lines.find(l => l.line.id === e.timelineId)?.line
  return `${formatEventDate(props.calendar, e)} · ${plainTitle(e.title, findCard)}${line ? `（${line.name}）` : ''}`
}
const sorted = (list: ChronicleEvent[]) => list.slice().sort((a, b) => (eventStart(props.calendar, a) ?? Infinity) - (eventStart(props.calendar, b) ?? Infinity))
const forkChoices = computed(() => sorted(props.events.filter(e => e.timelineId === form.value.parentId && e.kind !== 'skip')).map(e => ({ id: e.id, label: label(e) })))
const mergeChoices = computed(() => sorted(props.events.filter(e => e.timelineId !== props.line?.id && e.kind !== 'skip' && !e.sourceCardId)).map(e => ({ id: e.id, label: label(e) })))
watch(() => form.value.parentId, (p, old) => { if (old !== undefined && p !== old) form.value.forkEventId = null })

async function save() {
  const f = form.value
  error.value = ''
  if (!f.name.trim()) { error.value = '名称要填'; return }
  busy.value = true
  // 新线没选颜色：自动给一个，以后调整顺序颜色也不会变
  const color = f.color ?? (props.line ? null : LINE_COLORS[props.lines.length % LINE_COLORS.length]!)
  const body = { ...f, color, name: f.name.trim(), description: f.description.trim() || null, sortOrder: props.line?.sortOrder ?? null }
  try {
    const saved = props.line ? await chronicleApi.updateTimeline(props.projectId, props.line.id, body) : await chronicleApi.createTimeline(props.projectId, body)
    emit('saved', saved)
  } catch (e: any) { error.value = e?.response?.data?.message || '保存失败，稍后再试' }
  finally { busy.value = false }
}
async function remove() {
  const count = props.events.filter(e => e.timelineId === props.line?.id).length
  if (!props.line || !window.confirm(`删除「${props.line.name}」${count ? `和上面的 ${count} 件事` : ''}？从它分出去的支线会保留，变成独立的线。删错了可以在「修改记录」里恢复（连同上面的事件）。`)) return
  busy.value = true
  try { await chronicleApi.deleteTimeline(props.projectId, props.line.id); emit('deleted', props.line.id) }
  catch (e: any) { error.value = e?.response?.data?.message || '删除失败' }
  finally { busy.value = false }
}
function tryClose() {
  if (picking.value) { picking.value = false; return }
  if (JSON.stringify(form.value) !== initial && !window.confirm('改动还没保存，确定关闭？')) return
  emit('close')
}
</script>

<style scoped src="./ChronicleEditor.css"></style>
