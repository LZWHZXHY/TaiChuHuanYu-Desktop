<!-- 编年史：新建 / 编辑一件事。标题和描述都可以 @ 条目（「@某人 写下了 @涅塔尔之书」），读的时候点名字直接打开那个条目。 -->
<template>
  <div class="ce-mask" @mousedown.self="tryClose">
    <form class="ce" role="dialog" aria-modal="true" :aria-label="isNew ? '添加事件' : '编辑事件'" @submit.prevent="save" @keydown.esc="tryClose">
      <header class="ce-head">
        <h3>{{ isNew ? '添加事件' : '编辑事件' }}</h3>
        <button type="button" class="ce-x" aria-label="关闭" @click="tryClose">×</button>
      </header>

      <div class="ce-body">
        <div class="ce-kinds" role="radiogroup" aria-label="类型">
          <button v-for="(m, k) in KIND_META" :key="k" type="button" role="radio" :aria-checked="form.kind === k" :class="{ on: form.kind === k }" :title="m.hint" @click="form.kind = k">
            <span aria-hidden="true">{{ m.icon }}</span> {{ m.label }}
          </button>
        </div>
        <p class="ce-hint">{{ KIND_META[form.kind].hint }}<template v-if="form.kind === 'skip'">。轴上会把这一段折叠成一小格，适合「三百年后……」</template><template v-if="form.kind === 'lost'">。比如「文献全部焚毁，这一百年发生了什么没人知道」</template></p>

        <label class="ce-field">
          <span>在哪条时间线上</span>
          <select v-model="form.timelineId" required>
            <option v-for="l in lines" :key="l.line.id" :value="l.line.id">{{ '　'.repeat(l.depth) }}{{ l.depth ? '↳ ' : '' }}{{ l.line.name }}{{ l.line.status !== 'canon' ? `（${STATUS_META[l.line.status].label}）` : '' }}</option>
          </select>
        </label>

        <div class="ce-field">
          <span>标题 <small>可以输入 @ 关联条目，读的时候点名字就打开它</small></span>
          <CardRefTextarea v-model="form.title" :project-id="projectId" :rows="1" placeholder="比如：@某人 写下了 @涅塔尔之书" />
        </div>

        <fieldset class="ce-time">
          <legend>{{ form.kind === 'event' ? '时间' : '开始' }}</legend>
          <div class="ce-date">
            <select v-model="form.eraId" aria-label="纪元"><option v-for="e in calendar.eras" :key="e.id" :value="e.id">{{ e.name }}</option></select>
            <input v-model.number="form.year" type="number" step="1" :placeholder="'年份'" :aria-label="`年（${calendar.yearUnit}）`" />
            <span class="ce-unit">{{ calendar.yearUnit }}</span>
            <template v-if="calendar.useMonths">
              <select v-model.number="form.month" aria-label="月" :disabled="form.year === null || form.year === ''">
                <option :value="null">月（可不填）</option>
                <option v-for="(m, i) in calendar.months" :key="i" :value="i + 1">{{ m.name }}</option>
              </select>
              <input v-model.number="form.day" type="number" min="1" :max="maxDay(form.month)" placeholder="日" aria-label="日" :disabled="!form.month" />
            </template>
          </div>
        </fieldset>
        <fieldset v-if="form.kind !== 'event'" class="ce-time">
          <legend>结束 <small>（不填就当成只有开始那一年）</small></legend>
          <div class="ce-date">
            <select v-model="form.endEraId" aria-label="结束纪元"><option v-for="e in calendar.eras" :key="e.id" :value="e.id">{{ e.name }}</option></select>
            <input v-model.number="form.endYear" type="number" step="1" placeholder="年份" aria-label="结束年" />
            <span class="ce-unit">{{ calendar.yearUnit }}</span>
            <template v-if="calendar.useMonths">
              <select v-model.number="form.endMonth" aria-label="结束月" :disabled="form.endYear === null || form.endYear === ''">
                <option :value="null">月（可不填）</option>
                <option v-for="(m, i) in calendar.months" :key="i" :value="i + 1">{{ m.name }}</option>
              </select>
              <input v-model.number="form.endDay" type="number" min="1" :max="maxDay(form.endMonth)" placeholder="日" aria-label="结束日" :disabled="!form.endMonth" />
            </template>
          </div>
          <p v-if="spanText" class="ce-hint">{{ spanText }}</p>
        </fieldset>
        <label class="ce-field">
          <span>时间说明 <small>可选。不知道具体年份就只写这个（「第三纪元末」「大约在战后」），事件会放在「时间未定」</small></span>
          <input v-model="form.dateText" maxlength="100" placeholder="约 / 传说中 / 第三纪元末……" />
        </label>

        <div class="ce-field">
          <span>描述 <small>可以 @ 条目</small></span>
          <CardRefTextarea v-model="form.description" :project-id="projectId" :rows="5" placeholder="发生了什么、谁参与了、留下了什么……" />
        </div>

        <div class="ce-field">
          <span>相关条目 <small>人物、组织、物品……读者点了直接打开条目</small></span>
          <div class="ce-chips">
            <span v-for="id in form.cardIds" :key="id" class="ce-chip">{{ cardTitle(id) }}<button type="button" :aria-label="`移除 ${cardTitle(id)}`" @click="form.cardIds = form.cardIds.filter(x => x !== id)">×</button></span>
            <span class="ce-pick-wrap">
              <button type="button" class="ce-dashed" @click="picker = picker === 'cards' ? null : 'cards'">＋ 选择条目</button>
              <CardPicker v-if="picker === 'cards'" class="ce-picker" :project-id="projectId" :exclude="form.cardIds" @close="picker = null"
                @confirm="ids => { form.cardIds = [...form.cardIds, ...ids].slice(0, 50); picker = null }" />
            </span>
          </div>
        </div>

        <div class="ce-row2">
          <div class="ce-field">
            <span>发生地点 <small>选一个地点条目</small></span>
            <div class="ce-chips">
              <span v-if="form.locationCardId" class="ce-chip">📍 {{ cardTitle(form.locationCardId) }}<button type="button" aria-label="清除地点" @click="form.locationCardId = null">×</button></span>
              <span class="ce-pick-wrap">
                <button type="button" class="ce-dashed" @click="picker = picker === 'place' ? null : 'place'">{{ form.locationCardId ? '换一个' : '＋ 选择地点' }}</button>
                <CardPicker v-if="picker === 'place'" class="ce-picker" :project-id="projectId" @close="picker = null"
                  @confirm="ids => { form.locationCardId = ids[ids.length - 1] ?? null; picker = null }" />
              </span>
            </div>
          </div>
          <div class="ce-field">
            <span>颜色 <small>不选就跟时间线一样</small></span>
            <div class="ce-colors" role="radiogroup" aria-label="颜色">
              <button type="button" role="radio" class="ce-color none" :aria-checked="!form.color" title="跟时间线一样" @click="form.color = null">∅</button>
              <button v-for="c in LINE_COLORS" :key="c" type="button" role="radio" class="ce-color" :aria-checked="form.color === c" :style="{ background: c }" :aria-label="c" @click="form.color = c"></button>
            </div>
          </div>
        </div>

        <div class="ce-field">
          <span>标签</span>
          <TagInput v-model="form.tags" />
        </div>

        <div class="ce-field">
          <span>图片 <small>插图、地图截图、文物……最多 20 张</small></span>
          <div class="ce-imgs">
            <figure v-for="(url, i) in form.images" :key="url" class="ce-img">
              <img :src="withThumbnail(url, 160)" alt="" loading="lazy" />
              <button type="button" aria-label="移除这张图片" @click="form.images.splice(i, 1)">×</button>
            </figure>
            <label v-if="form.images.length < 20" class="ce-img-add" :class="{ busy: uploading }">
              <input type="file" accept="image/*" multiple :disabled="uploading" @change="onPick" />
              {{ uploading ? '上传中…' : '＋ 上传' }}
            </label>
          </div>
        </div>

        <label class="ce-check"><input v-model="form.hidden" type="checkbox" /> 对访客隐藏（只有创建者和编写者看得到，适合还没揭晓的秘密）</label>
        <p v-if="error" class="ce-error" role="alert">{{ error }}</p>
      </div>

      <footer class="ce-foot">
        <button v-if="!isNew" type="button" class="ce-del" :disabled="busy" @click="remove">删除</button>
        <button v-if="!isNew && form.kind !== 'skip'" type="button" class="ce-ghost" :disabled="busy" title="从这件事开始分出一条支线：「如果当年……」" @click="emit('branch', event!.id)">⑂ 从这里分出支线</button>
        <span class="ce-sp"></span>
        <button type="button" class="ce-ghost" @click="tryClose">取消</button>
        <button type="submit" class="ce-primary" :disabled="busy || uploading">{{ busy ? '保存中…' : '保存' }}</button>
      </footer>
    </form>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { chronicleApi, type EventInput } from '@/api/chronicle'
import { useCos } from '@/composables/useCos'
import { useWorldStore } from '@/stores/world'
import { withThumbnail } from '@/utils/imageThumb'
import {
  KIND_META, STATUS_META, LINE_COLORS, defaultEra, timeValue, formatSpan, plainTitle,
  type Calendar, type ChronicleEvent, type ChronicleTimeline, type EventKind,
} from '@/utils/chronicle'
import CardRefTextarea from './CardRefTextarea.vue'
import CardPicker from './CardPicker.vue'
import TagInput from './TagInput.vue'

const props = defineProps<{
  projectId: string
  calendar: Calendar
  lines: { line: ChronicleTimeline; depth: number }[]
  event: ChronicleEvent | null
  /** 新建时的默认值（在哪条线、什么时间） */
  preset?: Partial<ChronicleEvent>
}>()
const emit = defineEmits<{ close: []; saved: [ev: ChronicleEvent]; deleted: [id: string]; branch: [eventId: string] }>()
const store = useWorldStore()
const { uploadFile } = useCos()

const isNew = computed(() => !props.event)
type Num = number | null | ''
interface Form {
  timelineId: string; kind: EventKind; title: string; description: string
  eraId: string; year: Num; month: number | null; day: Num
  endEraId: string; endYear: Num; endMonth: number | null; endDay: Num
  dateText: string; color: string | null; images: string[]; tags: string[]; cardIds: string[]; locationCardId: string | null; hidden: boolean
}
const src: Partial<ChronicleEvent> = props.event ?? props.preset ?? {}
const era0 = defaultEra(props.calendar).id
const validEra = (id?: string | null) => (id && props.calendar.eras.some(e => e.id === id) ? id : era0)
const form = ref<Form>({
  timelineId: src.timelineId ?? props.lines[0]?.line.id ?? '',
  kind: src.kind ?? 'event',
  title: src.title ?? '',
  description: src.description ?? '',
  eraId: validEra(src.eraId), year: src.year ?? null, month: src.month ?? null, day: src.day ?? null,
  endEraId: validEra(src.endEraId ?? src.eraId), endYear: src.endYear ?? null, endMonth: src.endMonth ?? null, endDay: src.endDay ?? null,
  dateText: src.dateText ?? '', color: src.color ?? null,
  images: [...(src.images ?? [])], tags: [...(src.tags ?? [])], cardIds: [...(src.cardIds ?? [])],
  locationCardId: src.locationCardId ?? null, hidden: Boolean(src.hidden),
})
const initial = JSON.stringify(form.value)
const picker = ref<'cards' | 'place' | null>(null)
const busy = ref(false)
const uploading = ref(false)
const error = ref('')

const cardTitle = (id: string) => store.getCardById(id, props.projectId)?.title ?? '已删除的条目'
const maxDay = (m: number | null) => (m ? props.calendar.months[m - 1]?.days ?? 31 : 31)
const num = (v: Num) => (v === '' || v === null || v === undefined || !Number.isFinite(Number(v)) ? null : Math.trunc(Number(v)))

const spanText = computed(() => {
  const f = form.value
  const a = timeValue(props.calendar, f.eraId, num(f.year), f.month, num(f.day))
  const b = timeValue(props.calendar, f.endEraId, num(f.endYear), f.endMonth, num(f.endDay))
  if (a === null || b === null) return ''
  if (b < a) return '⚠ 结束比开始还早，检查一下纪元和年份'
  return `持续约 ${formatSpan(props.calendar, b - a + (f.endMonth ? 0 : 1))}`
})

function payload(): EventInput {
  const f = form.value
  const year = num(f.year), endYear = num(f.endYear)
  const ranged = f.kind !== 'event'
  return {
    timelineId: f.timelineId, kind: f.kind,
    title: f.title.replace(/\s*\n+\s*/g, ' ').trim(),
    description: f.description.trim() ? f.description : null,
    eraId: year === null ? null : f.eraId, year, month: year === null ? null : f.month, day: year === null || !f.month ? null : num(f.day),
    endEraId: ranged && endYear !== null ? f.endEraId : null, endYear: ranged ? endYear : null,
    endMonth: ranged && endYear !== null ? f.endMonth : null, endDay: ranged && endYear !== null && f.endMonth ? num(f.endDay) : null,
    dateText: f.dateText.trim() || null, color: f.color, images: f.images, tags: f.tags, cardIds: f.cardIds,
    locationCardId: f.locationCardId, hidden: f.hidden,
    sortOrder: props.event?.sortOrder ?? null,
  }
}

async function save() {
  const body = payload()
  error.value = ''
  if (!body.timelineId) { error.value = '先选一条时间线'; return }
  if (!plainTitle(body.title, id => store.getCardById(id, props.projectId)).trim()) { error.value = '标题要填'; return }
  if (body.kind === 'skip' && (body.year === null || body.endYear === null)) { error.value = '时间跳跃要写开始和结束的年份，轴上才知道折叠哪一段'; return }
  const a = timeValue(props.calendar, body.eraId, body.year, body.month, body.day)
  const b = timeValue(props.calendar, body.endEraId, body.endYear, body.endMonth, body.endDay)
  if (a !== null && b !== null && b < a) { error.value = '结束比开始还早'; return }
  busy.value = true
  try {
    const saved = props.event ? await chronicleApi.updateEvent(props.projectId, props.event.id, body) : await chronicleApi.createEvent(props.projectId, body)
    emit('saved', saved)
  } catch (e: any) {
    error.value = e?.response?.data?.message || e?.message || '保存失败，稍后再试'
  } finally { busy.value = false }
}
async function remove() {
  if (!props.event || !window.confirm('删除这件事？以它为分叉点 / 汇合点的支线会保留，只是不再连在这里。')) return
  busy.value = true
  try { await chronicleApi.deleteEvent(props.projectId, props.event.id); emit('deleted', props.event.id) }
  catch (e: any) { error.value = e?.response?.data?.message || '删除失败' }
  finally { busy.value = false }
}
function tryClose() {
  if (picker.value) { picker.value = null; return }
  if (JSON.stringify(form.value) !== initial && !window.confirm('改动还没保存，确定关闭？')) return
  emit('close')
}
async function onPick(e: Event) {
  const input = e.target as HTMLInputElement
  const files = [...(input.files ?? [])].slice(0, 20 - form.value.images.length)
  input.value = ''
  if (!files.length) return
  uploading.value = true
  try {
    for (const file of files) {
      if (!file.type.startsWith('image/')) continue
      if (file.size > 10 * 1024 * 1024) { ElMessage.warning(`${file.name} 超过 10MB，跳过了`); continue }
      form.value.images.push((await uploadFile(file, 'world-chronicle')).url)
    }
  } catch { ElMessage.error('图片上传失败') }
  finally { uploading.value = false }
}
</script>

<style scoped src="./ChronicleEditor.css"></style>
