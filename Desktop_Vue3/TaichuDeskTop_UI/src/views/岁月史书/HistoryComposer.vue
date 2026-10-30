<!-- 岁月史书「记一笔」：类型、哪天发生的、标题、内容、截图（可以直接 Ctrl+V 粘贴截图、拖进来）。
     管理员编辑别人的内容也用这个。 -->
<template>
  <div class="hc-mask" @mousedown.self="tryClose">
    <form class="hc" role="dialog" aria-modal="true" :aria-label="editing ? '编辑这一段' : '记一笔'" @submit.prevent="submit" @keydown.esc="tryClose" @paste="onPaste"
      @dragover.prevent="dragging = true" @dragleave.self="dragging = false" @drop.prevent="onDrop">
      <header class="hc-head">
        <h3>{{ editing ? '编辑这一段（管理员）' : '✍ 记一笔' }}</h3>
        <button type="button" class="hc-x" aria-label="关闭" @click="tryClose">×</button>
      </header>
      <div class="hc-body">
        <div class="hc-kinds" role="radiogroup" aria-label="类型">
          <button v-for="(m, k) in KIND_META" :key="k" type="button" role="radio" :aria-checked="form.kind === k" :class="{ on: form.kind === k }" :title="m.hint" @click="form.kind = k">
            {{ m.icon }} {{ m.label }}
          </button>
        </div>
        <p v-if="KIND_META[form.kind].hint" class="hc-hint">{{ KIND_META[form.kind].hint }}</p>

        <div class="hc-row">
          <label class="hc-field">
            <span>哪天发生的</span>
            <input v-model="form.happenedOn" type="date" :max="today" min="1900-01-01" required />
          </label>
          <label class="hc-field">
            <span>记得多清楚</span>
            <select v-model="form.precision">
              <option value="day">记得是哪天</option>
              <option value="month">只记得哪个月</option>
              <option value="year">只记得哪一年</option>
            </select>
          </label>
        </div>

        <label class="hc-field">
          <span>标题 <small>可选，一句话说说是什么事</small></span>
          <input v-model="form.title" maxlength="100" placeholder="比如：老王半夜在群里发了一张泡面的照片" />
        </label>
        <label class="hc-field">
          <span>内容 <small>可选，{{ form.content.length }}/5000</small></span>
          <textarea v-model="form.content" rows="5" maxlength="5000" placeholder="当时发生了什么、大家说了什么、后来怎么样了……"></textarea>
        </label>

        <div class="hc-field">
          <span>截图 / 照片 <small>最多 {{ MAX }} 张；可以直接 Ctrl + V 粘贴截图，或者拖进来</small></span>
          <div class="hc-imgs" :class="{ dragging }">
            <figure v-for="(url, i) in form.images" :key="url" class="hc-img">
              <img :src="url" alt="" />
              <div class="hc-img-ops">
                <button type="button" :disabled="i === 0" aria-label="往前挪" @click="move(i, -1)">‹</button>
                <button type="button" aria-label="移除这张" @click="form.images.splice(i, 1)">×</button>
                <button type="button" :disabled="i === form.images.length - 1" aria-label="往后挪" @click="move(i, 1)">›</button>
              </div>
            </figure>
            <figure v-for="u in uploading" :key="u.id" class="hc-img hc-uploading" :aria-label="`上传中 ${u.percent}%`">
              <img :src="u.preview" alt="" />
              <span class="hc-pct">{{ u.percent }}%</span>
              <i class="hc-bar" :style="{ width: u.percent + '%' }" aria-hidden="true"></i>
            </figure>
            <label v-if="form.images.length + uploading.length < MAX" class="hc-img-add">
              <input type="file" accept="image/*" multiple @change="onPick" />
              <span>＋</span><small>选图片</small>
            </label>
          </div>
        </div>

        <p v-if="!editing" class="hc-notice">
          所有人（包括没登录的访客）都能看到。群聊截图里如果有不想露出来的名字、头像、联系方式，记得先打码。
          发出去以后<b>只能删除，不能修改</b>（发错了就删掉重发）。
        </p>
        <p v-else class="hc-notice">修改后会显示「管理员编辑过」，并记下是谁、什么时候改的。</p>
        <p v-if="error" class="hc-error" role="alert">{{ error }}</p>
      </div>
      <footer class="hc-foot">
        <span class="hc-sp"></span>
        <button type="button" class="hc-btn" @click="tryClose">取消</button>
        <button type="submit" class="hc-btn primary" :disabled="busy || uploading.length > 0">{{ busy ? '保存中…' : uploading.length ? `图片上传中 ${totalPercent}%…` : editing ? '保存修改' : '记下来' }}</button>
      </footer>
    </form>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { historyApi, errorText, KIND_META, type HistoryEntry, type HistoryInput } from '@/api/history'
import { useCos } from '@/composables/useCos'

const props = defineProps<{ entry?: HistoryEntry | null }>()
const emit = defineEmits<{ close: []; saved: [entry: HistoryEntry] }>()
const { uploadFile } = useCos()
const MAX = 12

const editing = computed(() => Boolean(props.entry))
const today = (() => { const t = new Date(Date.now() + 8 * 3600_000); return t.toISOString().slice(0, 10) })()
const form = ref<HistoryInput>({
  kind: props.entry?.kind ?? 'funny',
  title: props.entry?.title ?? '',
  content: props.entry?.content ?? '',
  images: [...(props.entry?.images ?? [])],
  happenedOn: props.entry?.happenedOn ?? today,
  precision: props.entry?.precision ?? 'day',
})
const initial = JSON.stringify(form.value)
const busy = ref(false)
const error = ref('')
const dragging = ref(false)
/** 正在上传的图片：本地预览 + 这一张的百分比 */
const uploading = ref<{ id: number; preview: string; percent: number; size: number }[]>([])
/** 所有正在上传的图片合起来的进度（按文件大小加权） */
const totalPercent = computed(() => {
  const total = uploading.value.reduce((s, u) => s + u.size, 0)
  return total ? Math.floor(uploading.value.reduce((s, u) => s + u.size * u.percent, 0) / total) : 0
})
let uploadSeq = 0

function move(i: number, d: -1 | 1) {
  const [it] = form.value.images.splice(i, 1)
  form.value.images.splice(i + d, 0, it!)
}
async function upload(files: File[]) {
  const room = MAX - form.value.images.length - uploading.value.length
  const list = files.filter(f => f.type.startsWith('image/')).slice(0, Math.max(0, room))
  if (files.length && !list.length) { error.value = room <= 0 ? `最多 ${MAX} 张` : '只能传图片'; return }
  error.value = ''
  await Promise.all(list.map(async file => {
    if (file.size > 15 * 1024 * 1024) { error.value = `${file.name || '图片'} 超过 15MB 了`; return }
    const id = ++uploadSeq
    uploading.value.push({ id, preview: URL.createObjectURL(file), percent: 0, size: file.size || 1 })
    const slot = () => uploading.value.find(u => u.id === id)
    try {
      const { url } = await uploadFile(file, 'history', p => { const s = slot(); if (s) s.percent = Math.min(99, p) })
      form.value.images.push(url)
    } catch { error.value = '有图片上传失败了（网络不好？），再传一次试试' }
    finally {
      const s = slot()
      if (s) URL.revokeObjectURL(s.preview)
      uploading.value = uploading.value.filter(u => u.id !== id)
    }
  }))
}
function onPick(e: Event) {
  const input = e.target as HTMLInputElement
  const files = [...(input.files ?? [])]
  input.value = ''
  void upload(files)
}
/** 直接粘贴截图（微信 / QQ 截图后 Ctrl+V） */
function onPaste(e: ClipboardEvent) {
  const files = [...(e.clipboardData?.files ?? [])].filter(f => f.type.startsWith('image/'))
  if (!files.length) return
  e.preventDefault()
  void upload(files)
}
function onDrop(e: DragEvent) {
  dragging.value = false
  void upload([...(e.dataTransfer?.files ?? [])])
}

async function submit() {
  const f = form.value
  error.value = ''
  if (!f.title.trim() && !f.content.trim() && !f.images.length) { error.value = '写点什么，或者传张截图吧'; return }
  if (!f.happenedOn) { error.value = '选一下是哪天发生的'; return }
  if (f.happenedOn > today) { error.value = '还没发生的事不能记哦'; return }
  if (!editing.value && !window.confirm('发出去以后所有人都能看到，自己只能删除不能修改。确定记下来？')) return
  busy.value = true
  try {
    const body = { ...f, title: f.title.trim(), content: f.content.trim() }
    const saved = props.entry ? await historyApi.edit(props.entry.id, body) : await historyApi.create(body)
    emit('saved', saved)
  } catch (e) {
    error.value = errorText(e, '没保存成功，稍后再试')
  } finally { busy.value = false }
}
function tryClose() {
  if (JSON.stringify(form.value) !== initial && !window.confirm('写的内容还没保存，确定关掉？')) return
  emit('close')
}
</script>

<style scoped>
.hc-mask { position: fixed; inset: 0; z-index: 2000; display: flex; align-items: flex-start; justify-content: center; padding: 5vh 16px; overflow-y: auto; background: rgba(0, 0, 0, .4); }
.hc { width: min(640px, 100%); display: flex; flex-direction: column; background: var(--paper-card); color: var(--text-primary); border-top: 3px solid var(--accent-color); box-shadow: 0 20px 50px rgba(0, 0, 0, .25); }
.hc-head { display: flex; align-items: center; padding: 14px 18px; border-bottom: 1px solid var(--border-line); }
.hc-head h3 { flex: 1; margin: 0; font-size: 16px; }
.hc-x { width: 30px; height: 30px; border: 0; background: none; color: var(--text-secondary); font-size: 20px; cursor: pointer; }
.hc-body { display: grid; gap: 14px; padding: 16px 18px; }
.hc-kinds { display: flex; flex-wrap: wrap; gap: 4px; }
.hc-kinds button { padding: 6px 12px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.hc-kinds button.on { border-color: var(--accent-color); background: var(--accent-color); color: #fff; }
.hc-hint { margin: -6px 0 0; font-size: 12px; color: var(--text-secondary); }
.hc-row { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
.hc-field { display: grid; gap: 6px; min-width: 0; }
.hc-field > span { font-size: 13px; font-weight: 600; }
.hc-field small { font-weight: 400; color: var(--text-secondary); }
.hc-field input, .hc-field select, .hc-field textarea { box-sizing: border-box; width: 100%; min-width: 0; padding: 7px 9px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 14px; }
.hc-field textarea { resize: vertical; line-height: 1.7; }
.hc-imgs { display: flex; flex-wrap: wrap; gap: 8px; padding: 8px; border: 1px dashed var(--border-line); }
.hc-imgs.dragging { border-color: var(--accent-color); background: color-mix(in srgb, var(--accent-color) 6%, transparent); }
.hc-img { position: relative; margin: 0; width: 92px; height: 92px; background: var(--bg-sub); }
.hc-img img { width: 100%; height: 100%; object-fit: cover; display: block; }
.hc-img-ops { position: absolute; left: 0; right: 0; bottom: 0; display: flex; justify-content: space-between; background: rgba(0, 0, 0, .55); }
.hc-img-ops button { flex: 1; height: 22px; border: 0; background: none; color: #fff; cursor: pointer; }
.hc-img-ops button:disabled { opacity: .35; cursor: default; }
.hc-uploading { display: flex; align-items: center; justify-content: center; overflow: hidden; }
.hc-uploading img { position: absolute; inset: 0; opacity: .45; filter: grayscale(.4); }
.hc-pct { position: relative; z-index: 1; padding: 2px 6px; background: rgba(0, 0, 0, .6); color: #fff; font-size: 14px; font-weight: 700; font-variant-numeric: tabular-nums; }
.hc-bar { position: absolute; left: 0; bottom: 0; z-index: 1; height: 4px; background: var(--accent-color); transition: width .2s; }
.hc-img-add { width: 92px; height: 92px; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 2px; border: 1px solid var(--border-line); color: var(--text-secondary); cursor: pointer; }
.hc-img-add span { font-size: 22px; line-height: 1; }
.hc-img-add small { font-size: 12px; }
.hc-img-add input { display: none; }
.hc-img-add:hover { color: var(--accent-color); border-color: var(--accent-color); }
.hc-notice { margin: 0; padding: 8px 10px; background: var(--bg-sub); font-size: 12px; line-height: 1.7; color: var(--text-secondary); }
.hc-error { margin: 0; color: #dc2626; font-size: 13px; }
.hc-foot { display: flex; align-items: center; gap: 8px; padding: 12px 18px; border-top: 1px solid var(--border-line); }
.hc-sp { flex: 1; }
.hc-btn { padding: 7px 16px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.hc-btn.primary { border-color: var(--accent-color); background: var(--accent-color); color: #fff; }
.hc-btn:disabled { opacity: .55; cursor: default; }
@media (max-width: 560px) { .hc-row { grid-template-columns: 1fr; } .hc-mask { padding: 0; } }
</style>
