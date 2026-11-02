<!-- 转盘编辑器：创建 / 编辑用户转盘，管理员创建 / 编辑官方转盘（官方转盘能给扇区设奖品）。
     右边（手机上是上面）实时预览，可以试转。扇区可以拖动排序，手机上用 ↑ ↓ 按钮。 -->
<template>
  <form class="wheel-editor" novalidate @submit.prevent="save">
    <header class="ed-head">
      <div>
        <p class="eyebrow">{{ official ? 'OFFICIAL WHEEL' : 'MY WHEEL' }}</p>
        <h3>{{ heading }}</h3>
      </div>
      <button type="button" class="btn ghost" @click="emit('cancel')">← 返回</button>
    </header>

    <div class="ed-grid">
      <section class="ed-form">
        <label class="field">
          <span>转盘名字 <small>{{ title.length }}/{{ limits.titleMax }}</small></span>
          <input v-model="title" type="text" :maxlength="limits.titleMax" placeholder="例如：今天中午吃什么" />
        </label>
        <label class="field">
          <span>简介 <small>可以不写，{{ intro.length }}/{{ limits.introMax }}</small></span>
          <textarea v-model="intro" rows="2" :maxlength="limits.introMax" placeholder="说说这个转盘是干嘛用的"></textarea>
        </label>

        <fieldset v-if="!official" class="field vis">
          <legend>谁能看到</legend>
          <label :class="{ on: visibility === 'public' }"><input v-model="visibility" type="radio" value="public" /><b>公开</b><small>出现在转盘大厅，谁都能转</small></label>
          <label :class="{ on: visibility === 'private' }"><input v-model="visibility" type="radio" value="private" /><b>仅自己</b><small>不进大厅，只有你和拿到链接的人能转</small></label>
        </fieldset>
        <label v-if="!official" class="check">
          <input v-model="removeOnHit" type="checkbox" />
          <span>默认开「抽签模式」：转到的扇区从盘上拿掉，适合抽人、抽题不重复（只在当前页面生效，刷新就恢复）</span>
        </label>
        <label v-if="official" class="field narrow">
          <span>排序 <small>数字小的排前面</small></span>
          <input v-model.number="sortOrder" type="number" step="1" />
        </label>

        <div class="seg-head">
          <h4>扇区 <small>{{ segs.length }}/{{ limits.maxSegments }}</small></h4>
          <div class="seg-tools">
            <button v-if="!official && !initial" type="button" class="btn small ghost" @click="templatesOpen = !templatesOpen">用模板</button>
            <button type="button" class="btn small ghost" @click="bulkOpen = !bulkOpen">批量添加</button>
            <button type="button" class="btn small ghost" title="所有扇区概率一样" @click="evenWeights">权重全设为 1</button>
          </div>
        </div>
        <div v-if="templatesOpen" class="templates" role="group" aria-label="模板">
          <button v-for="t in TEMPLATES" :key="t.name" type="button" class="chip" @click="useTemplate(t)">{{ t.name }}</button>
          <small>会替换掉现在的扇区</small>
        </div>
        <div v-if="bulkOpen" class="bulk">
          <textarea v-model="bulkText" rows="4" placeholder="一行一个，例如：&#10;张三&#10;李四&#10;王五"></textarea>
          <div class="bulk-foot">
            <small>还能加 {{ limits.maxSegments - segs.length }} 个，每个最多 {{ limits.segmentTextMax }} 字</small>
            <button type="button" class="btn small primary" @click="addBulk">加进去</button>
          </div>
        </div>

        <ol class="seg-list">
          <li
            v-for="(s, i) in segs" :key="s.key" class="seg"
            :class="{ dragging: dragIndex === i, over: overIndex === i && dragIndex !== i, prize: official }"
            draggable="true"
            @dragstart="onDragStart(i, $event)" @dragover.prevent="overIndex = i" @dragleave="overIndex === i && (overIndex = -1)"
            @drop.prevent="onDrop(i)" @dragend="dragIndex = -1; overIndex = -1"
          >
            <span class="drag" title="按住拖动调整顺序" aria-hidden="true">⋮⋮</span>
            <span class="no">{{ i + 1 }}</span>
            <input v-model="s.color" class="color" type="color" :aria-label="`第 ${i + 1} 个扇区的颜色`" />
            <input v-model="s.text" class="text" type="text" :maxlength="limits.segmentTextMax" placeholder="写点什么" :aria-label="`第 ${i + 1} 个扇区的文字`" />
            <label class="weight" :title="`权重越大越容易转到（1–${limits.weightMax}）`">
              <small>权重</small>
              <input v-model.number="s.weight" type="number" min="1" :max="limits.weightMax" step="1" :aria-label="`第 ${i + 1} 个扇区的权重`" />
            </label>
            <span class="pct" title="转到的概率">{{ percent(probs[i] ?? 0) }}</span>
            <span v-if="official" class="prize-pick">
              <select v-model="s.prizeType" :aria-label="`第 ${i + 1} 个扇区的奖品`">
                <option value="none">无奖品</option>
                <option value="potato">土豆</option>
                <option value="exp">经验</option>
              </select>
              <input v-if="s.prizeType !== 'none'" v-model.number="s.prizeAmount" type="number" min="1" :max="s.prizeType === 'potato' ? limits.potatoPrizeMax : limits.expPrizeMax" step="1" :aria-label="`第 ${i + 1} 个扇区的奖品数量`" />
            </span>
            <span class="ops">
              <button type="button" :disabled="i === 0" aria-label="上移" title="上移" @click="move(i, -1)">↑</button>
              <button type="button" :disabled="i === segs.length - 1" aria-label="下移" title="下移" @click="move(i, 1)">↓</button>
              <button type="button" class="del" :disabled="segs.length <= limits.minSegments" aria-label="删掉这个扇区" title="删掉" @click="removeSeg(i)">×</button>
            </span>
          </li>
        </ol>
        <button type="button" class="add-seg" :disabled="segs.length >= limits.maxSegments" @click="addSeg()">+ 加一个扇区</button>

        <!-- 付费（只有新建用户转盘时） -->
        <fieldset v-if="isCreate" class="pay">
          <legend>做这个转盘要花</legend>
          <p v-if="!pricing" class="muted">正在读取价格…</p>
          <template v-else>
            <label :class="{ on: payWith === 'exp', off: pricing.costExp < 0 }">
              <input v-model="payWith" type="radio" value="exp" :disabled="pricing.costExp < 0" />
              <b>{{ pricing.costExp < 0 ? '经验（暂不可用）' : pricing.costExp === 0 ? '经验：免费' : `${pricing.costExp.toLocaleString()} 经验` }}</b>
              <small>你有 {{ pricing.experience.toLocaleString() }} 经验</small>
            </label>
            <label :class="{ on: payWith === 'potato', off: pricing.costPotato < 0 }">
              <input v-model="payWith" type="radio" value="potato" :disabled="pricing.costPotato < 0" />
              <b>{{ pricing.costPotato < 0 ? '土豆（暂不可用）' : pricing.costPotato === 0 ? '土豆：免费' : `🥔 ${pricing.costPotato.toLocaleString()} 土豆` }}</b>
              <small>你有 {{ pricing.potato.toLocaleString() }} 土豆</small>
            </label>
            <p class="pay-note">只在创建时收一次，以后编辑不再收费；删除转盘不退还。你已经做了 {{ pricing.count }} / {{ pricing.max }} 个。</p>
            <p v-if="shortage" class="warn">{{ shortage }}</p>
          </template>
        </fieldset>

        <p v-if="error" class="err" role="alert">{{ error }}</p>
        <footer class="ed-foot">
          <button type="submit" class="btn primary" :disabled="busy || (isCreate && (!pricing || !!shortage || pricing.count >= pricing.max))">{{ busy ? '保存中…' : submitText }}</button>
          <button type="button" class="btn ghost" @click="emit('cancel')">取消</button>
          <button v-if="initial && !official" type="button" class="btn danger" :disabled="busy" @click="del">删除转盘</button>
        </footer>
      </section>

      <aside class="ed-preview">
        <h4>实时预览</h4>
        <WheelCanvas ref="preview" :segments="previewSegs" :size="340" :highlight="previewHit" :sound="false">
          <template #center>
            <button type="button" class="try" :disabled="trying || previewSegs.length < 2" title="试转一下（不计次数）" @click="trySpin">试</button>
          </template>
        </WheelCanvas>
        <p class="muted">点中间的「试」转一下看看效果{{ previewHit != null ? `：转到「${previewSegs[previewHit]?.text}」` : '' }}</p>
        <p v-if="official" class="muted small">扇区大小大致按权重分（太小的会放大一点方便看），真正的中奖概率看左边的百分比。</p>
      </aside>
    </div>
  </form>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { wheelApi, wheelError, type WheelDetail, type WheelInput, type WheelPayWith, type WheelPricing, type WheelPrizeType, type WheelSegment, type WheelVisibility } from '@/api/wheel'
import { notifyPotatoChanged } from '@/api/potato'
import WheelCanvas from './WheelCanvas.vue'
import { DEFAULT_LIMITS, PALETTE, nextColor, percent, pickWeighted, probabilities } from './wheelShared'

const props = defineProps<{
  /** 是不是官方转盘（管理员用） */
  official?: boolean
  /** 要编辑的转盘；null 表示新建 */
  initial?: WheelDetail | null
}>()
const emit = defineEmits<{ saved: [id: number, shareCode: string | null]; cancel: []; deleted: [] }>()

interface EditSeg { key: number; text: string; color: string; weight: number; prizeType: WheelPrizeType; prizeAmount: number }
let keySeed = 1
const mk = (text: string, color: string, weight = 1, prizeType: WheelPrizeType = 'none', prizeAmount = 0): EditSeg =>
  ({ key: keySeed++, text, color, weight, prizeType, prizeAmount })

const isCreate = computed(() => !props.initial && !props.official)
const heading = computed(() => props.official ? (props.initial ? '编辑官方转盘' : '新建官方转盘') : (props.initial ? '编辑我的转盘' : '做一个转盘'))

const title = ref(props.initial?.title ?? '')
const intro = ref(props.initial?.intro ?? '')
const visibility = ref<WheelVisibility>(props.initial?.visibility ?? 'public')
const removeOnHit = ref(props.initial?.removeOnHit ?? false)
const sortOrder = ref(props.initial?.sortOrder ?? 0)
const segs = ref<EditSeg[]>(props.initial
  ? props.initial.segments.map(s => mk(s.text, s.color, s.weight, s.prizeType, s.prizeAmount))
  : props.official
    ? [mk('谢谢参与', '#669BBC', 60), mk('50 土豆', '#F3A712', 25, 'potato', 50), mk('200 土豆', '#E4572E', 10, 'potato', 200), mk('20 经验', '#7D5BA6', 5, 'exp', 20)]
    : [mk('选项一', PALETTE[0]), mk('选项二', PALETTE[1]), mk('选项三', PALETTE[2]), mk('选项四', PALETTE[3])])

const pricing = ref<WheelPricing | null>(null)
const limits = computed(() => pricing.value?.limits ?? DEFAULT_LIMITS)
const payWith = ref<WheelPayWith>('exp')
const busy = ref(false)
const error = ref('')

const probs = computed(() => probabilities(segs.value))

// ===== 模板 / 批量 =====
const TEMPLATES: { name: string; title: string; items: string[] }[] = [
  { name: '今天吃什么', title: '今天吃什么', items: ['火锅', '烧烤', '麻辣烫', '面条', '盖浇饭', '汉堡', '饺子', '不吃了'] },
  { name: '是 / 否', title: '要不要做', items: ['要！', '不要'] },
  { name: '掷骰子', title: '掷骰子', items: ['1', '2', '3', '4', '5', '6'] },
  { name: '抽一个人', title: '抽一个幸运儿', items: ['甲', '乙', '丙', '丁', '戊'] },
  { name: '真心话大冒险', title: '真心话还是大冒险', items: ['真心话', '大冒险', '跳过', '指定别人'] },
]
const templatesOpen = ref(false)
function useTemplate(t: { title: string; items: string[] }) {
  segs.value = t.items.map((x, i) => mk(x, PALETTE[i % PALETTE.length]))
  if (!title.value.trim()) title.value = t.title
  if (t.title === '抽一个幸运儿') removeOnHit.value = true
  templatesOpen.value = false
}
const bulkOpen = ref(false)
const bulkText = ref('')
function addBulk() {
  const lines = bulkText.value.split(/\r?\n/).map(l => l.trim().slice(0, limits.value.segmentTextMax)).filter(Boolean)
  const room = limits.value.maxSegments - segs.value.length
  if (!lines.length) { ElMessage.info('一行写一个'); return }
  lines.slice(0, room).forEach(text => addSeg(text))
  if (lines.length > room) ElMessage.warning(`最多 ${limits.value.maxSegments} 个扇区，多出来的 ${lines.length - room} 个没加`)
  bulkText.value = ''
  bulkOpen.value = false
}

// ===== 扇区增删改 =====
function addSeg(text = '') {
  if (segs.value.length >= limits.value.maxSegments) return
  segs.value.push(mk(text || `选项${segs.value.length + 1}`, nextColor(segs.value.map(s => s.color))))
}
function removeSeg(i: number) {
  if (segs.value.length <= limits.value.minSegments) return
  segs.value.splice(i, 1)
}
function move(i: number, d: number) {
  const j = i + d
  if (j < 0 || j >= segs.value.length) return
  const list = segs.value.slice()
  const a = list[i], b = list[j]
  if (!a || !b) return
  list[i] = b
  list[j] = a
  segs.value = list
}
function evenWeights() { segs.value.forEach(s => { s.weight = 1 }) }

// 拖动排序（电脑上；手机上用 ↑ ↓）
const dragIndex = ref(-1)
const overIndex = ref(-1)
function onDragStart(i: number, e: DragEvent) {
  dragIndex.value = i
  if (e.dataTransfer) { e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/plain', String(i)) }
}
function onDrop(to: number) {
  const from = dragIndex.value
  dragIndex.value = -1
  overIndex.value = -1
  if (from < 0 || from === to) return
  const list = segs.value.slice()
  const item = list.splice(from, 1)[0]
  if (!item) return
  list.splice(to, 0, item)
  segs.value = list
}

// ===== 预览 =====
const preview = ref<InstanceType<typeof WheelCanvas> | null>(null)
const previewSegs = computed(() => segs.value.map((s, i) => ({
  text: s.text.trim() || `扇区${i + 1}`,
  color: s.color,
  weight: Math.min(limits.value.weightMax, Math.max(1, Math.round(Number(s.weight) || 1))),
})))
const previewHit = ref<number | null>(null)
const trying = ref(false)
async function trySpin() {
  if (trying.value || previewSegs.value.length < 2) return
  trying.value = true
  previewHit.value = null
  const idx = pickWeighted(previewSegs.value)
  await preview.value?.spinTo(idx, { duration: 2600 })
  previewHit.value = idx
  trying.value = false
}

// ===== 检查 / 保存 =====
const costOf = (w: WheelPayWith) => pricing.value ? (w === 'exp' ? pricing.value.costExp : pricing.value.costPotato) : 0
const shortage = computed(() => {
  const p = pricing.value
  if (!p) return ''
  if (p.count >= p.max) return `每人最多做 ${p.max} 个转盘，你已经做满了，删掉几个不用的再来吧`
  if (p.costExp < 0 && p.costPotato < 0) return '现在暂时不能创建转盘'
  const cost = costOf(payWith.value)
  if (payWith.value === 'exp' && cost > p.experience) return `经验不够：需要 ${cost}，你现在有 ${p.experience}`
  if (payWith.value === 'potato' && cost > p.potato) return `土豆不够：需要 ${cost.toLocaleString()}，你现在有 ${p.potato.toLocaleString()}`
  return ''
})
const submitText = computed(() => {
  if (!isCreate.value) return props.initial ? (props.official ? '保存' : '保存修改（不收费）') : '创建并上架'
  const cost = costOf(payWith.value)
  return cost <= 0 ? '创建转盘（免费）' : `创建转盘（花 ${cost.toLocaleString()} ${payWith.value === 'exp' ? '经验' : '土豆'}）`
})

function validate(): string {
  if (!title.value.trim()) return '给转盘起个名字吧'
  const L = limits.value
  if (segs.value.length < L.minSegments) return `至少要有 ${L.minSegments} 个扇区`
  if (segs.value.length > L.maxSegments) return `最多 ${L.maxSegments} 个扇区`
  for (let i = 0; i < segs.value.length; i++) {
    const s = segs.value[i]
    if (!s) continue
    if (!s.text.trim()) return `第 ${i + 1} 个扇区还没写文字`
    const w = Number(s.weight)
    if (!Number.isInteger(w) || w < 1 || w > L.weightMax) return `第 ${i + 1} 个扇区的权重要是 1 到 ${L.weightMax} 的整数`
    if (props.official && s.prizeType !== 'none') {
      const max = s.prizeType === 'potato' ? L.potatoPrizeMax : L.expPrizeMax
      const a = Number(s.prizeAmount)
      if (!Number.isInteger(a) || a < 1 || a > max) return `第 ${i + 1} 个扇区的奖品数量要是 1 到 ${max} 的整数`
    }
  }
  return ''
}

function body(): WheelInput {
  return {
    title: title.value.trim(),
    intro: intro.value.trim(),
    visibility: props.official ? 'public' : visibility.value,
    removeOnHit: props.official ? false : removeOnHit.value,
    sortOrder: Number(sortOrder.value) || 0,
    segments: segs.value.map((s): WheelSegment => ({
      text: s.text.trim(),
      color: s.color,
      weight: Number(s.weight),
      prizeType: props.official ? s.prizeType : 'none',
      prizeAmount: props.official && s.prizeType !== 'none' ? Number(s.prizeAmount) : 0,
    })),
  }
}

async function save() {
  error.value = validate()
  if (error.value) return
  busy.value = true
  try {
    if (props.official) {
      if (props.initial) {
        const r = await wheelApi.adminOfficialUpdate(props.initial.id, body())
        ElMessage.success(r.message)
        emit('saved', props.initial.id, null)
      } else {
        const r = await wheelApi.adminOfficialCreate(body())
        ElMessage.success(r.message)
        emit('saved', r.id, null)
      }
    } else if (props.initial) {
      const r = await wheelApi.update(props.initial.id, body())
      ElMessage.success(r.message)
      emit('saved', props.initial.id, props.initial.shareCode)
    } else {
      const cost = costOf(payWith.value)
      if (cost > 0) {
        try {
          await ElMessageBox.confirm(`创建这个转盘要花 ${cost.toLocaleString()} ${payWith.value === 'exp' ? '经验' : '土豆'}，以后编辑不再收费，删除也不退还。确定吗？`, '确认创建', { confirmButtonText: '确定创建', cancelButtonText: '再想想' })
        } catch { return }
      }
      const r = await wheelApi.create({ ...body(), payWith: payWith.value })
      if (payWith.value === 'potato') notifyPotatoChanged()
      ElMessage.success(r.message)
      emit('saved', r.id, r.shareCode)
    }
  } catch (e) {
    error.value = wheelError(e, '没保存成功，稍后再试')
  } finally {
    busy.value = false
  }
}

async function del() {
  if (!props.initial) return
  try {
    await ElMessageBox.confirm('删除后不能恢复，创建时花的经验 / 土豆也不会退还。确定删除吗？', '删除转盘', { confirmButtonText: '删除', cancelButtonText: '不删了', type: 'warning' })
  } catch { return }
  busy.value = true
  try {
    const r = await wheelApi.remove(props.initial.id)
    ElMessage.success(r.message)
    emit('deleted')
  } catch (e) {
    error.value = wheelError(e, '没删掉，稍后再试')
  } finally {
    busy.value = false
  }
}

onMounted(async () => {
  if (props.official) return
  try {
    pricing.value = await wheelApi.pricing()
    // 默认选能付得起的那种
    const p = pricing.value
    const canExp = p.costExp >= 0 && p.costExp <= p.experience
    const canPotato = p.costPotato >= 0 && p.costPotato <= p.potato
    payWith.value = canExp || (!canPotato && p.costExp >= 0) ? 'exp' : 'potato'
  } catch (e) {
    if (isCreate.value) error.value = wheelError(e, '读取价格失败，刷新试试')
  }
})
</script>

<style scoped>
.wheel-editor { display: grid; gap: 16px; color: var(--text-primary); }
.ed-head { position: relative; display: flex; justify-content: space-between; align-items: flex-end; gap: 12px; padding-bottom: 12px; border-bottom: 1px solid var(--border-line); }
.ed-head::after { content: ''; position: absolute; left: 0; bottom: -1px; width: 64px; height: 3px; background: var(--accent-color); }
.ed-head h3 { margin: 0; font-size: 22px; }
.eyebrow { margin: 0 0 6px; font: 10px/1.2 var(--font-mono, monospace); letter-spacing: .16em; color: var(--accent-color); }
.ed-grid { display: grid; grid-template-columns: minmax(0, 1fr) minmax(260px, 360px); gap: 22px; align-items: start; }
.ed-form { display: grid; gap: 14px; min-width: 0; }
.field { display: grid; gap: 6px; margin: 0; padding: 0; border: 0; font-size: 13px; }
.field > span, .field legend { font-weight: 600; font-size: 13px; }
.field small, .seg-head small { font-weight: 400; color: var(--text-secondary); }
.field.narrow { max-width: 200px; }
input[type='text'], input[type='number'], textarea, select { box-sizing: border-box; padding: 8px 10px; border: 1px solid var(--border-line); border-radius: 0; background: var(--bg-main, var(--paper-card)); color: var(--text-primary); font: inherit; font-size: 14px; }
textarea { resize: vertical; width: 100%; }
input:focus, textarea:focus, select:focus { outline: 2px solid color-mix(in srgb, var(--accent-color) 50%, transparent); outline-offset: -1px; }
.vis { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
.vis legend { margin-bottom: 6px; }
.vis label, .pay label { display: grid; grid-template-columns: auto 1fr; column-gap: 8px; align-items: center; padding: 10px 12px; border: 1px solid var(--border-line); background: var(--paper-card); cursor: pointer; }
.vis label small, .pay label small { grid-column: 2; color: var(--text-secondary); font-size: 12px; }
.vis label.on, .pay label.on { border-color: var(--accent-color); background: var(--accent-soft); }
.pay label.off { opacity: .55; cursor: not-allowed; }
.check { display: flex; gap: 8px; align-items: flex-start; font-size: 13px; color: var(--text-secondary); line-height: 1.6; cursor: pointer; }
.check input { margin-top: 4px; }

.seg-head { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: 8px; padding-top: 6px; border-top: 1px dashed var(--border-line); }
.seg-head h4 { margin: 0; font-size: 15px; }
.seg-tools { display: flex; flex-wrap: wrap; gap: 6px; }
.templates { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; padding: 10px; background: var(--bg-sub); }
.templates small { color: var(--text-secondary); font-size: 12px; }
.chip { padding: 4px 10px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.chip:hover { border-color: var(--accent-color); color: var(--accent-color); }
.bulk { display: grid; gap: 6px; padding: 10px; background: var(--bg-sub); }
.bulk-foot { display: flex; justify-content: space-between; align-items: center; gap: 8px; }
.bulk-foot small { color: var(--text-secondary); font-size: 12px; }

.seg-list { display: grid; gap: 6px; margin: 0; padding: 0; list-style: none; }
.seg { display: grid; grid-template-columns: 16px 20px 34px minmax(80px, 1fr) 92px 46px auto; gap: 6px; align-items: center; padding: 6px 8px; border: 1px solid var(--border-line); background: var(--paper-card); }
.seg.prize { grid-template-columns: 16px 20px 34px minmax(80px, 1fr) 92px 46px minmax(150px, auto) auto; }
.seg.dragging { opacity: .45; }
.seg.over { border-color: var(--accent-color); box-shadow: inset 0 3px 0 var(--accent-color); }
.drag { color: var(--text-secondary); cursor: grab; font-size: 13px; letter-spacing: -2px; user-select: none; }
.no { font: 11px var(--font-mono, monospace); color: var(--text-secondary); text-align: right; }
.color { width: 34px; height: 30px; padding: 2px; border: 1px solid var(--border-line); background: var(--paper-card); cursor: pointer; }
.text { min-width: 0; width: 100%; }
.weight { display: flex; align-items: center; gap: 4px; }
.weight small { color: var(--text-secondary); font-size: 11px; white-space: nowrap; }
.weight input { width: 60px; padding: 6px; }
.pct { font: 12px var(--font-mono, monospace); color: var(--accent-color); text-align: right; }
.prize-pick { display: flex; gap: 4px; }
.prize-pick select { padding: 6px; }
.prize-pick input { width: 80px; padding: 6px; }
.ops { display: flex; gap: 2px; }
.ops button { width: 28px; height: 28px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; cursor: pointer; }
.ops button:disabled { opacity: .3; cursor: default; }
.ops .del:not(:disabled):hover { border-color: var(--danger, #dc2626); color: var(--danger, #dc2626); }
.add-seg { padding: 9px; border: 1px dashed var(--border-line); background: transparent; color: var(--accent-color); font: inherit; font-size: 13px; cursor: pointer; }
.add-seg:disabled { opacity: .4; cursor: default; }

.pay { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; margin: 0; padding: 14px 0 0; border: 0; border-top: 1px dashed var(--border-line); }
.pay legend { font-weight: 600; font-size: 13px; margin-bottom: 8px; }
.pay > p { grid-column: 1 / -1; margin: 0; }
.pay-note { font-size: 12px; color: var(--text-secondary); line-height: 1.6; }
.warn { color: #b45309; font-size: 13px; }
.err { margin: 0; padding: 8px 12px; border-left: 3px solid var(--danger, #dc2626); background: color-mix(in srgb, var(--danger, #dc2626) 8%, var(--paper-card)); color: var(--danger, #dc2626); font-size: 13px; }
.ed-foot { display: flex; flex-wrap: wrap; gap: 8px; }
.ed-foot .danger { margin-left: auto; }
.muted { margin: 0; color: var(--text-secondary); font-size: 13px; }
.muted.small { font-size: 12px; line-height: 1.6; }

.ed-preview { position: sticky; top: 12px; display: grid; gap: 10px; padding: 16px; border: 1px solid var(--border-line); background: var(--paper-card); }
.ed-preview h4 { margin: 0; font-size: 14px; }
.try { width: 52px; height: 52px; border: 3px solid #fff; border-radius: 50%; background: #20252b; color: #fff; font: inherit; font-size: 16px; font-weight: 700; cursor: pointer; box-shadow: 0 2px 8px rgba(0, 0, 0, .3); }
.try:disabled { opacity: .6; cursor: default; }

.btn { padding: 8px 16px; border: 1px solid var(--border-line); border-radius: 0; background: transparent; color: var(--text-primary); font: inherit; font-size: 14px; cursor: pointer; }
.btn.small { padding: 4px 10px; font-size: 12px; }
.btn.primary { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent, #fff); font-weight: 600; }
.btn.ghost { color: var(--text-secondary); }
.btn.danger { color: var(--danger, #dc2626); border-color: color-mix(in srgb, var(--danger, #dc2626) 40%, var(--border-line)); }
.btn:disabled { opacity: .5; cursor: not-allowed; }

@media (max-width: 900px) {
  .ed-grid { grid-template-columns: minmax(0, 1fr); }
  .ed-preview { position: static; order: -1; }
}
@media (max-width: 600px) {
  .vis, .pay { grid-template-columns: minmax(0, 1fr); }
  .seg, .seg.prize { grid-template-columns: 16px 34px minmax(0, 1fr) auto; grid-template-areas: 'drag color text ops' 'drag weight weight pct' 'drag prize prize prize'; }
  .seg .no { display: none; }
  .drag { grid-area: drag; }
  .color { grid-area: color; }
  .text { grid-area: text; }
  .ops { grid-area: ops; }
  .weight { grid-area: weight; }
  .pct { grid-area: pct; }
  .prize-pick { grid-area: prize; }
}
</style>
