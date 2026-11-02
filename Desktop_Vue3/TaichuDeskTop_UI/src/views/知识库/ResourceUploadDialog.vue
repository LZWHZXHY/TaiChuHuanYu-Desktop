<!-- 上传资源弹窗：选文件（可拖进来）→ 填标题 / 介绍 / 分类 / 标签 / 价格 → 分片直传到 NAS（显示总进度、速度、剩余时间，可取消）→ 等管理员审核。
     传 resume 时是「继续上传」：重新选同一个文件，只传还没传上去的片。 -->
<template>
  <div class="rud-mask" role="dialog" aria-modal="true" :aria-label="resume ? '继续上传资源' : '上传资源'" @click.self="requestClose">
    <section class="rud-panel">
      <header class="rud-head">
        <div>
          <p class="rud-eyebrow">RESOURCE / UPLOAD</p>
          <h2>{{ resume ? '继续上传' : '上传资源' }}</h2>
        </div>
        <button type="button" class="rud-x" aria-label="关闭" @click="requestClose">×</button>
      </header>

      <!-- 1. 选文件 + 填资料 -->
      <form v-if="step === 'form'" class="rud-body" @submit.prevent="submit">
        <p v-if="resume" class="rud-note">
          《{{ resume.title }}》上次没传完。请重新选择当初那个文件 <b>{{ resume.fileName }}</b>（{{ formatBytes(resume.size) }}），已经传上去的部分不用再传。
        </p>
        <div
          class="rud-drop"
          :class="{ over: dragOver, has: !!file }"
          @dragover.prevent="dragOver = true"
          @dragleave.prevent="dragOver = false"
          @drop.prevent="onDrop"
        >
          <input :id="inputId" ref="fileInput" type="file" class="rud-file" @change="onPick" />
          <template v-if="file">
            <span class="rud-plate">{{ extLabel(file.name) }}</span>
            <strong class="rud-fname">{{ file.name }}</strong>
            <span class="rud-fsize">{{ formatBytes(file.size) }}</span>
            <label :for="inputId" class="rud-link">换一个文件</label>
          </template>
          <template v-else>
            <strong>把文件拖到这里</strong>
            <span>或者 <label :for="inputId" class="rud-link">点这里选择文件</label></span>
            <small>单个文件最大 {{ formatBytes(config?.maxFileBytes ?? 4 * 1024 ** 3) }}；可以运行的程序和脚本（.exe .bat .apk 等）不能上传，压缩包可以</small>
          </template>
        </div>

        <!-- 存到哪：NAS / 腾讯云（腾讯云没配置时只显示 NAS，不让选） -->
        <fieldset v-if="!resume && storageOptions.length > 1" class="rud-store">
          <legend>存到哪里</legend>
          <label v-for="o in storageOptions" :key="o.key" class="rud-store-opt" :class="{ on: storageKey === o.key, off: !o.available }">
            <input v-model="storageKey" type="radio" name="rud-storage" :value="o.key" :disabled="!o.available" />
            <span class="rud-store-name">{{ o.label }}<small v-if="o.key === (inChina ? 'cos' : 'nas')" class="rud-store-tip">{{ inChina ? '国内推荐' : '海外推荐' }}</small></span>
            <span class="rud-store-hint">{{ o.available ? o.hint : '网站还没配置，暂时不能选' }}</span>
          </label>
        </fieldset>

        <ResourceMetaFields v-if="!resume" v-model="meta" :config="config" />

        <p v-if="error" class="rud-error" role="alert">{{ error }}</p>
        <footer class="rud-actions">
          <button type="button" class="btn-line" @click="emit('close')">取消</button>
          <button type="submit" class="btn-line btn-primary" :disabled="busy || !file">{{ busy ? '准备中…' : resume ? '继续上传' : '开始上传' }}</button>
        </footer>
      </form>

      <!-- 2. 上传中 / 失败 / 合并中 -->
      <div v-else-if="step === 'uploading' || step === 'failed' || step === 'completing'" class="rud-body">
        <p class="rud-file-line"><span class="rud-plate">{{ extLabel(fileName) }}</span><strong>{{ fileName }}</strong><span>{{ formatBytes(progress.total) }}</span></p>
        <div class="rud-bar" role="progressbar" :aria-valuenow="percent" aria-valuemin="0" aria-valuemax="100" :aria-label="`已上传 ${percent}%`">
          <div class="rud-bar-fill" :class="{ failed: step === 'failed' }" :style="{ width: percent + '%' }"></div>
        </div>
        <dl class="rud-stats">
          <div><dt>进度</dt><dd>{{ percent }}%</dd></div>
          <div><dt>已传</dt><dd>{{ formatBytes(progress.loaded) }} / {{ formatBytes(progress.total) }}</dd></div>
          <div><dt>速度</dt><dd>{{ step === 'uploading' && progress.speed > 0 ? formatBytes(progress.speed) + '/s' : '—' }}</dd></div>
          <div><dt>剩余</dt><dd>{{ step === 'uploading' ? formatDuration(progress.eta) : '—' }}</dd></div>
          <div><dt>分片</dt><dd>{{ progress.doneParts }} / {{ progress.partCount }}</dd></div>
        </dl>
        <p v-if="step === 'uploading' && progress.retrying.length" class="rud-note warn" role="status">网络不太稳，第 {{ progress.retrying.join('、') }} 片正在自动重试…</p>
        <p v-if="step === 'uploading'" class="rud-note"><template v-if="!resume">正在传到 <b>{{ storageLabel(uploadingTo) }}</b>。</template>上传时请不要关闭或刷新页面。万一中断了，24 小时内可以在「我的资源」里继续上传。<template v-if="uploadingTo === 'nas' && slow && cosAvailable && !resume"><br />速度有点慢？可以取消，重新上传时选「腾讯云」试试。</template></p>
        <p v-if="step === 'completing'" class="rud-note" role="status">都传上去了，正在合并文件、核对大小……</p>
        <p v-if="step === 'failed'" class="rud-error" role="alert">{{ error }}</p>
        <footer class="rud-actions">
          <button v-if="step !== 'completing'" type="button" class="btn-line danger" @click="cancelUpload">取消上传</button>
          <button v-if="step === 'failed'" type="button" class="btn-line btn-primary" @click="retry">重试没传上去的部分</button>
        </footer>
      </div>

      <!-- 3. 完成 -->
      <div v-else-if="step === 'done'" class="rud-body rud-done">
        <span class="rud-check" aria-hidden="true">✓</span>
        <h3>已提交，等管理员审核</h3>
        <p>审核通过后会出现在知识库「下载资源」里，你会收到通知{{ config ? `，还能获得 ${config.approveExp} 经验` : '' }}。审核进度可以在「我的资源」里看。</p>
        <footer class="rud-actions">
          <button type="button" class="btn-line" @click="emit('openMine')">查看我的资源</button>
          <button type="button" class="btn-line btn-primary" @click="emit('close')">好的</button>
        </footer>
      </div>

      <!-- 4. 上传已失效 -->
      <div v-else class="rud-body">
        <p class="rud-error" role="alert">{{ error || '这次上传已经失效了（超过 24 小时会被自动清理），请重新上传。' }}</p>
        <footer class="rud-actions"><button type="button" class="btn-line btn-primary" @click="emit('close')">知道了</button></footer>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, useId } from 'vue'
import { onBeforeRouteLeave } from 'vue-router'
import { getErrorMessage } from '@/utils/apiError'
import { formatBytes, resourceApi, resourceErrorData, storageLabel, type ResourceConfig, type ResourceMeta, type ResourceStorageKey } from '@/api/resources'
import ResourceMetaFields from './ResourceMetaFields.vue'
import {
  ChunkUploader, PartFailedError, UploadCancelled, formatDuration, forgetUpload, matchesResume, rememberUpload,
  type UploadPlan, type UploadProgress,
} from './resourceUpload'

const props = defineProps<{
  /** 继续上传：资源编号、标题、文件名、大小（来自「我的资源」） */
  resume?: { id: string; title: string; fileName: string; size: number } | null
}>()
const emit = defineEmits<{ close: []; submitted: []; openMine: [] }>()

type Step = 'form' | 'uploading' | 'failed' | 'completing' | 'done' | 'expired'
const step = ref<Step>('form')
const config = ref<ResourceConfig | null>(null)
const file = ref<File | null>(null)
const fileInput = ref<HTMLInputElement | null>(null)
const inputId = useId()
const dragOver = ref(false)
const busy = ref(false)
const error = ref('')
const meta = ref<ResourceMeta>({ title: '', description: '', category: '其他', tags: [], price: 0, folder: '' })
const progress = reactive<UploadProgress>({ loaded: 0, total: 0, speed: 0, eta: null, doneParts: 0, partCount: 0, retrying: [] })
const percent = computed(() => (progress.total > 0 ? Math.min(100, Math.floor((progress.loaded / progress.total) * 100)) : 0))

let resourceId = ''
const fileName = ref('')
let plan: UploadPlan | null = null
let uploader: ChunkUploader | null = null
let disposed = false

const extLabel = (name: string) => {
  const dot = name.lastIndexOf('.')
  return dot > 0 && dot < name.length - 1 ? name.slice(dot + 1, dot + 6).toUpperCase() : 'FILE'
}

onMounted(async () => {
  try { config.value = await resourceApi.config() } catch { /* 用默认值，提交时后端会再检查 */ }
  // 存放位置：上次选的优先（还能用的话），否则用后端的默认值
  const saved = (() => { try { return localStorage.getItem(STORE_KEY) } catch { return null } })()
  const ok = (k: string | null) => storageOptions.value.some(o => o.key === k && o.available)
  // 没选过时：在国内默认腾讯云（NAS 在海外，国内传过去慢），在海外默认 NAS
  const preferred = inChina && ok('cos') ? 'cos' : config.value?.defaultStorage ?? 'nas'
  storageKey.value = (ok(saved) ? saved : ok(preferred) ? preferred : 'nas') as ResourceStorageKey
})

// ===== 存到哪（NAS / 腾讯云） =====
const STORE_KEY = 'tc-resource-storage'
const storageKey = ref<ResourceStorageKey>('nas')
const storageOptions = computed(() => config.value?.storages ?? [])
const cosAvailable = computed(() => storageOptions.value.some(o => o.key === 'cos' && o.available))
/** 这次实际传到哪（继续上传时以资源自己记的为准） */
const uploadingTo = ref<ResourceStorageKey>('nas')
/** 浏览器时区是中国：大概率在国内。NAS 在海外，国内传过去慢，给腾讯云标「国内推荐」、没选过时默认腾讯云 */
const inChina = (() => { try { return ['Asia/Shanghai', 'Asia/Chongqing', 'Asia/Harbin', 'Asia/Urumqi', 'PRC'].includes(Intl.DateTimeFormat().resolvedOptions().timeZone) } catch { return false } })()
/** 传了 20 秒以上、平均不到 1MB/s：提示可以换腾讯云 */
const slow = computed(() => progress.speed > 0 && progress.speed < 1024 * 1024 && progress.loaded > 10 * 1024 * 1024)

function useFile(f: File | undefined | null) {
  error.value = ''
  if (!f) return
  // 前端先挡一下明显不行的（后端还会再检查一遍）
  const dot = f.name.lastIndexOf('.')
  const ext = dot >= 0 ? f.name.slice(dot).toLowerCase() : ''
  const blocked = config.value?.blockedExtensions ?? ['.exe', '.msi', '.bat', '.cmd', '.com', '.scr', '.ps1', '.vbs', '.js', '.jar', '.apk']
  if (ext && blocked.includes(ext)) { error.value = `不能上传 ${ext} 文件：能直接运行的程序、脚本和安装包不收。确实要分享工具的话请打包成压缩包，并在介绍里写清楚用法。`; return }
  if (f.size <= 0) { error.value = '这个文件是空的'; return }
  const max = config.value?.maxFileBytes ?? 4 * 1024 ** 3
  if (f.size > max) { error.value = `文件太大了，单个文件最大 ${formatBytes(max)}`; return }
  if (props.resume) {
    const check = matchesResume(props.resume.id, props.resume.size, f)
    if (!check.ok) { error.value = check.reason ?? '不是当初那个文件'; return }
  }
  file.value = f
  // 没填标题时，用文件名（去掉扩展名）当默认标题
  if (!props.resume && !meta.value.title) meta.value.title = (dot > 0 ? f.name.slice(0, dot) : f.name).slice(0, config.value?.titleMax ?? 60)
}
function onPick(e: Event) { const input = e.target as HTMLInputElement; useFile(input.files?.[0]); input.value = '' }
function onDrop(e: DragEvent) { dragOver.value = false; useFile(e.dataTransfer?.files?.[0]) }

async function submit() {
  if (!file.value || busy.value) return
  error.value = ''
  busy.value = true
  try {
    if (props.resume) await startResume(file.value, props.resume.id)
    else await startNew(file.value)
  } catch (e) {
    error.value = getErrorMessage(e, '开始上传失败，请稍后再试')
  } finally { busy.value = false }
}

async function startNew(f: File) {
  if (!meta.value.title.trim()) { error.value = '请填写标题'; return }
  const target = cosAvailable.value ? storageKey.value : 'nas'
  try { localStorage.setItem(STORE_KEY, target) } catch { /* 记不住就算了 */ }
  const res = await resourceApi.initUpload({ ...meta.value, fileName: f.name, size: f.size, contentType: f.type || 'application/octet-stream', ...(target === 'cos' ? { storage: target } : {}) })
  uploadingTo.value = target
  resourceId = res.id
  fileName.value = res.fileName
  rememberUpload(res.id, f)
  plan = { file: f, partSize: res.partSize, partCount: res.partCount, concurrency: res.concurrency, urls: new Map(res.parts.map(p => [p.partNumber, p.url])), done: new Set() }
  void run()
}

async function startResume(f: File, id: string) {
  const status = await resourceApi.uploadStatus(id)
  if (status.expired) { step.value = 'expired'; forgetUpload(id); return }
  resourceId = id
  fileName.value = status.fileName
  const done = new Set(status.doneParts)
  const missing: number[] = []
  for (let n = 1; n <= status.partCount; n++) if (!done.has(n)) missing.push(n)
  const urls = new Map<number, string>()
  if (missing.length) for (const p of (await resourceApi.refreshUrls(id, missing)).parts) urls.set(p.partNumber, p.url)
  rememberUpload(id, f)
  plan = { file: f, partSize: status.partSize, partCount: status.partCount, concurrency: status.concurrency, urls, done }
  void run()
}

async function run() {
  if (!plan) return
  step.value = 'uploading'
  error.value = ''
  uploader = new ChunkUploader(plan, {
    refreshUrls: async parts => (await resourceApi.refreshUrls(resourceId, parts)).parts,
    reportPart: (n, size) => resourceApi.partDone(resourceId, n, size),
  }, p => Object.assign(progress, p))
  try {
    await uploader.start()
  } catch (e) {
    if (disposed) return
    if (e instanceof UploadCancelled) return // 用户取消 / 关闭：各自处理
    step.value = 'failed'
    error.value = e instanceof PartFailedError
      ? `${e.message}。可能是网络断了一下，点「重试」只会重传没传上去的部分。`
      : getErrorMessage(e, '上传中断了，点「重试」接着传')
    return
  }
  await finish()
}

async function finish() {
  step.value = 'completing'
  try {
    await resourceApi.complete(resourceId)
    forgetUpload(resourceId)
    if (disposed) return
    step.value = 'done'
    emit('submitted')
  } catch (e) {
    if (disposed) return
    const data = resourceErrorData(e)
    if (data?.code === 'missing_parts' && Array.isArray(data.missing) && plan) {
      // 服务器说有片没收到：把它们从「已完成」里拿掉，重试时重新传
      for (const n of data.missing as number[]) plan.done.delete(n)
      step.value = 'failed'
      error.value = data.message ?? '还有部分没传上去，请重试'
    } else if (data?.code === 'size_mismatch' || data?.code === 'expired') {
      forgetUpload(resourceId)
      step.value = 'expired'
      error.value = data.message ?? '这次上传作废了，请重新上传'
    } else {
      step.value = 'failed'
      error.value = getErrorMessage(e, '合并文件失败，请点「重试」')
    }
  }
}

function retry() {
  if (!plan) return
  if (plan.done.size >= plan.partCount) void finish()
  else void run()
}

/** 取消上传：停下来，并让服务器删掉已经传上去的部分 */
async function cancelUpload() {
  if (!window.confirm('确定取消这次上传吗？已经传上去的部分会被删除。')) return
  uploader?.cancel()
  if (resourceId) {
    try { await resourceApi.abort(resourceId) } catch { /* 服务器 24 小时后也会自动清理 */ }
    forgetUpload(resourceId)
  }
  emit('close')
}

const uploading = computed(() => step.value === 'uploading' || step.value === 'completing')
const LEAVE_TIP = '上传还没完成。离开会暂停上传，24 小时内可以在「我的资源」里继续。确定离开吗？'

/** 点 × 或者遮罩：上传中要确认（暂停，不删除，之后可以继续） */
function requestClose() {
  if (uploading.value && !window.confirm(LEAVE_TIP)) return
  uploader?.cancel()
  emit('close')
}

// 离开页面前提醒：切换路由、关标签页 / 刷新
onBeforeRouteLeave(() => {
  if (!uploading.value) return true
  if (!window.confirm(LEAVE_TIP)) return false
  uploader?.cancel()
  return true
})
function onBeforeUnload(e: BeforeUnloadEvent) {
  if (!uploading.value) return
  e.preventDefault()
  e.returnValue = ''
}
window.addEventListener('beforeunload', onBeforeUnload)
onBeforeUnmount(() => {
  disposed = true
  window.removeEventListener('beforeunload', onBeforeUnload)
  uploader?.cancel()
})
</script>

<style scoped>
.rud-mask { position: fixed; inset: 0; z-index: 10000; display: grid; place-items: start center; padding: 5vh 16px; overflow-y: auto; background: var(--overlay, #0008); }
.rud-panel { width: min(640px, 100%); background: var(--paper-card); color: var(--text-primary); border-top: 3px solid var(--accent-color); box-shadow: var(--shadow-md); }
.rud-head { display: flex; justify-content: space-between; align-items: start; gap: 12px; padding: 20px 22px 14px; border-bottom: 1px solid var(--border-line); }
.rud-eyebrow { margin: 0 0 6px; font: 10px var(--font-mono); letter-spacing: .14em; color: var(--accent-color); }
.rud-head h2 { margin: 0; font-size: 20px; }
.rud-x { width: 34px; height: 34px; border: 1px solid var(--border-line); background: transparent; color: var(--text-secondary); font-size: 20px; line-height: 1; cursor: pointer; border-radius: 0; }
.rud-x:hover { border-color: var(--accent-color); color: var(--accent-color); }
.rud-body { display: grid; gap: 16px; padding: 18px 22px 22px; }
.rud-drop { position: relative; display: grid; justify-items: center; gap: 6px; padding: 26px 16px; border: 1px dashed var(--border-line); background: var(--bg-sub); text-align: center; color: var(--text-secondary); font-size: 13px; transition: border-color .15s, background .15s; }
.rud-drop.over { border-color: var(--accent-color); background: var(--accent-soft); }
.rud-drop.has { border-style: solid; border-left: 3px solid var(--signal-mint); justify-items: start; text-align: left; grid-template-columns: auto minmax(0, 1fr) auto; align-items: center; column-gap: 12px; }
.rud-drop strong { color: var(--text-primary); font-size: 15px; }
.rud-drop small { max-width: 440px; line-height: 1.6; }
.rud-file { position: absolute; width: 1px; height: 1px; opacity: 0; pointer-events: none; }
.rud-link { color: var(--accent-color); text-decoration: underline; cursor: pointer; }
.rud-drop.has .rud-link { grid-column: 1 / -1; font-size: 12px; }
.rud-fname { overflow-wrap: anywhere; }
.rud-fsize { font: 12px var(--font-mono); }
.rud-plate { display: inline-block; min-width: 38px; padding: 3px 6px; border: 1px solid var(--text-primary); font: 600 10px var(--font-mono); letter-spacing: .08em; text-align: center; color: var(--text-primary); background: var(--paper-card); }
.rud-store { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 8px; margin: 0; padding: 0; border: 0; min-width: 0; }
.rud-store legend { padding: 0; margin-bottom: 6px; font-size: 13px; font-weight: 600; color: var(--text-primary); }
.rud-store-opt { position: relative; display: grid; gap: 3px; padding: 10px 12px 10px 34px; border: 1px solid var(--border-line); background: var(--paper-card); cursor: pointer; transition: border-color .15s, box-shadow .15s; }
.rud-store-opt input { position: absolute; left: 12px; top: 12px; accent-color: var(--accent-color); }
.rud-store-opt.on { border-color: var(--accent-color); box-shadow: inset 3px 0 0 var(--accent-color); }
.rud-store-opt.off { opacity: .55; cursor: not-allowed; }
.rud-store-name { display: flex; align-items: center; gap: 6px; font-weight: 600; font-size: 14px; }
.rud-store-tip { padding: 0 6px; font: 10px/1.7 var(--font-mono); letter-spacing: .06em; background: var(--inverse-bg); color: var(--inverse-text); border-left: 2px solid var(--signal-mint); }
.rud-store-hint { font-size: 12px; line-height: 1.6; color: var(--text-secondary); }
.rud-note { margin: 0; padding: 10px 12px; border-left: 2px solid var(--accent-color); background: var(--bg-sub); font-size: 13px; line-height: 1.7; color: var(--text-secondary); }
.rud-note b { color: var(--text-primary); overflow-wrap: anywhere; }
.rud-note.warn { border-left-color: var(--warning); background: var(--warning-soft); color: var(--warning); }
.rud-error { margin: 0; padding: 10px 12px; border-left: 2px solid var(--danger); background: var(--danger-soft); color: var(--danger); font-size: 13px; line-height: 1.7; }
.rud-actions { display: flex; justify-content: flex-end; flex-wrap: wrap; gap: 10px; }
.rud-actions button { min-height: 40px; padding: 9px 18px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 14px; cursor: pointer; }
.rud-actions .btn-primary, .rud-actions .btn-primary:not(:disabled):hover { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent); clip-path: polygon(0 0, calc(100% - var(--chamfer, 10px)) 0, 100% var(--chamfer, 10px), 100% 100%, var(--chamfer, 10px) 100%, 0 calc(100% - var(--chamfer, 10px))); }
.rud-actions .danger { color: var(--danger); }
.rud-actions button:disabled { opacity: .5; cursor: not-allowed; }
.rud-file-line { display: flex; align-items: center; gap: 10px; margin: 0; font-size: 14px; min-width: 0; }
.rud-file-line strong { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.rud-file-line > span:last-child { font: 12px var(--font-mono); color: var(--text-secondary); }
.rud-bar { height: 12px; border: 1px solid var(--border-line); background: var(--bg-sub); overflow: hidden; }
.rud-bar-fill { height: 100%; background: repeating-linear-gradient(-45deg, var(--accent-color) 0 8px, color-mix(in srgb, var(--accent-color) 82%, var(--signal-mint)) 8px 16px); transition: width .25s; }
.rud-bar-fill.failed { background: var(--danger); }
.rud-stats { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 1px; margin: 0; background: var(--border-line); border: 1px solid var(--border-line); }
.rud-stats div { padding: 8px 10px; background: var(--paper-card); }
.rud-stats dt { font: 10px var(--font-mono); letter-spacing: .1em; color: var(--text-secondary); }
.rud-stats dd { margin: 4px 0 0; font-size: 13px; font-variant-numeric: tabular-nums; overflow-wrap: anywhere; }
.rud-done { justify-items: center; text-align: center; }
.rud-done h3 { margin: 0; font-size: 18px; }
.rud-done p { margin: 0; color: var(--text-secondary); font-size: 14px; line-height: 1.7; }
.rud-check { display: grid; place-items: center; width: 48px; height: 48px; border: 2px solid var(--signal-mint); color: var(--signal-mint); font-size: 24px; font-weight: 700; }
@media (max-width: 560px) {
  .rud-mask { padding: 0; place-items: stretch; }
  .rud-panel { min-height: 100%; }
  .rud-head, .rud-body { padding-left: 16px; padding-right: 16px; }
  .rud-stats { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .rud-actions { justify-content: stretch; }
  .rud-actions button { flex: 1; }
}
</style>
