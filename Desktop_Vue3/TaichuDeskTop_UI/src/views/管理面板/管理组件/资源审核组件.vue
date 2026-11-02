<!-- 管理面板 ·「资源审核」：用户上传到知识库的资源。
     待审核：下载 / 预览核查 → 通过（公开、进社区动态、上传者 +20 经验）或打回（要写理由，上传者改好后自动重新提交）；
     已公开：可以下架（要写理由；下架后谁都不能下载，包括买过的人；数据和文件都保留）；已下架：可以恢复。
     权限：超级管理员或「用户审核」，和约战作品审核一样。 -->
<template>
  <div class="rv">
    <div class="rv-head">
      <div>
        <h2>资源审核</h2>
        <p>先点「下载核查」看看文件和介绍是否一致、有没有夹带程序、有没有版权问题，再决定通过还是打回。压缩包请解开检查。</p>
      </div>
      <button type="button" class="btn-line" :disabled="loading" @click="refresh">刷新</button>
    </div>

    <div class="rv-tabs" role="tablist" aria-label="资源状态">
      <button v-for="t in TABS" :key="t.id" type="button" role="tab" :aria-selected="status === t.id" :class="{ on: status === t.id }" @click="switchTab(t.id)">
        {{ t.label }}<b v-if="counts[t.id]">{{ counts[t.id] }}</b>
      </button>
    </div>

    <p v-if="notice" class="rv-notice" :class="{ error: noticeError }" role="status">{{ notice }}</p>
    <p v-if="error" class="rv-empty" role="alert">{{ error }}</p>
    <p v-else-if="loading && !items.length" class="rv-empty">读取中…</p>
    <p v-else-if="!items.length" class="rv-empty">{{ status === 'pending' ? '没有待审核的资源 🎉' : '这里还没有记录' }}</p>

    <ul v-if="items.length" class="rv-list">
      <li v-for="r in items" :key="r.id" class="rv-item">
        <div class="rv-top">
          <span class="rv-plate">{{ r.ext ? r.ext.slice(1, 6).toUpperCase() : 'FILE' }}</span>
          <strong class="rv-title">{{ r.title }}</strong>
          <span class="rv-cat">{{ r.category }}</span>
          <span class="rv-price">{{ priceText(r.price) }}</span>
        </div>
        <p class="rv-meta">
          <UserLink v-if="r.uploader" :id="r.uploader.id" :name="r.uploader.name">{{ r.uploader.name }}</UserLink>
          提交于 <time :datetime="r.updatedAt">{{ time(r.updatedAt) }}</time>
          <template v-if="r.status !== 'pending' && r.reviewedAt"> · {{ r.reviewerName || '管理员' }} 处理于 {{ time(r.reviewedAt) }}</template>
          <template v-if="r.status === 'approved'"> · {{ r.uniqueDownloadCount }} 人下载 · 经验收入 ✦ {{ r.expEarned }}</template>
        </p>
        <dl class="rv-file">
          <div><dt>文件名</dt><dd>{{ r.fileName }}</dd></div>
          <div><dt>大小</dt><dd>{{ formatBytes(r.size) }}</dd></div>
          <div><dt>类型</dt><dd>{{ r.contentType }}</dd></div>
          <div class="rv-loc">
            <dt>位置</dt>
            <dd v-if="folderEdit && folderEdit.id === r.id" class="rv-loc-edit">
              <input v-model="folderEdit.text" list="rv-folders" maxlength="200" :placeholder="`空着就放在「${r.category}」`" aria-label="位置" />
              <button type="button" class="rv-link" :disabled="busy === r.id" @click="saveFolder(r)">保存</button>
              <button type="button" class="rv-link" @click="folderEdit = null">取消</button>
            </dd>
            <dd v-else>{{ folderText(r.folder) }}<template v-if="!r.folderRaw">（用分类名）</template> <button type="button" class="rv-link" @click="editFolder(r)">改位置</button></dd>
          </div>
          <div><dt>存放</dt><dd>{{ storageLabel(r.storage) }}</dd></div>
        </dl>
        <ul v-if="r.warnings.length" class="rv-warn"><li v-for="w in r.warnings" :key="w">⚠ {{ w }}</li></ul>
        <p v-if="r.tags.length" class="rv-tags"><span v-for="t in r.tags" :key="t">#{{ t }}</span></p>
        <details class="rv-desc" :open="r.description.length < 160">
          <summary>介绍（{{ r.description.length }} 字）</summary>
          <p>{{ r.description || '（没写介绍）' }}</p>
        </details>
        <p v-if="r.reviewNote && (r.status === 'rejected' || r.status === 'removed')" class="rv-reason">{{ r.status === 'rejected' ? '打回理由' : '下架理由' }}：{{ r.reviewNote }}</p>

        <!-- 预览：图片 / 音频 / 视频直接放在这里，其他（PDF、文本）在新标签页打开 -->
        <div v-if="preview && preview.id === r.id" class="rv-preview">
          <img v-if="previewKind(r) === 'image'" :src="preview.url" :alt="r.title" />
          <audio v-else-if="previewKind(r) === 'audio'" :src="preview.url" controls preload="metadata"></audio>
          <video v-else-if="previewKind(r) === 'video'" :src="preview.url" controls preload="metadata"></video>
        </div>

        <!-- 打回 / 下架：写理由 -->
        <form v-if="reasonFor && reasonFor.id === r.id" class="rv-reason-form" @submit.prevent="submitReason(r)">
          <label :for="'rv-reason-' + r.id">{{ reasonFor.kind === 'reject' ? '打回理由（上传者会看到，写清楚怎么改）' : '下架理由（上传者会看到）' }}</label>
          <textarea :id="'rv-reason-' + r.id" v-model="reasonText" rows="3" maxlength="300" required placeholder="比如：介绍里没写授权来源 / 压缩包里有可执行程序 / 和标题不符"></textarea>
          <div class="rv-ops">
            <button type="button" class="btn-line" @click="reasonFor = null">取消</button>
            <button type="submit" class="btn-line danger" :disabled="busy === r.id || !reasonText.trim()">{{ reasonFor.kind === 'reject' ? '确认打回' : '确认下架' }}</button>
          </div>
        </form>
        <div v-else class="rv-ops">
          <button type="button" class="btn-line" :disabled="busy === r.id" @click="downloadFile(r)">下载核查</button>
          <button v-if="r.previewable" type="button" class="btn-line" :disabled="busy === r.id" @click="previewFile(r)">预览</button>
          <template v-if="r.status === 'pending'">
            <button type="button" class="btn-line danger" :disabled="busy === r.id" @click="askReason(r, 'reject')">打回</button>
            <button type="button" class="btn-line btn-primary" :disabled="busy === r.id" @click="approve(r)">通过</button>
          </template>
          <template v-else-if="r.status === 'approved'">
            <button type="button" class="btn-line danger" :disabled="busy === r.id" @click="askReason(r, 'remove')">下架</button>
          </template>
          <template v-else-if="r.status === 'rejected'">
            <button type="button" class="btn-line btn-primary" :disabled="busy === r.id" @click="approve(r)">改判通过</button>
          </template>
          <template v-else-if="r.status === 'removed'">
            <button type="button" class="btn-line btn-primary" :disabled="busy === r.id" @click="restore(r)">恢复公开</button>
          </template>
        </div>
      </li>
    </ul>
    <datalist id="rv-folders"><option v-for="f in folders" :key="f" :value="f" /></datalist>
    <button v-if="hasMore" type="button" class="btn-line rv-more" :disabled="loading" @click="load(page + 1)">{{ loading ? '读取中…' : '加载更多' }}</button>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import UserLink from '@/components/UserLink.vue'
import { getErrorMessage } from '@/utils/apiError'
import { folderText, formatBytes, priceText, resourceApi, startBrowserDownload, storageLabel, type AdminResourceItem } from '@/api/resources'

type Tab = 'pending' | 'approved' | 'rejected' | 'removed'
const TABS: { id: Tab; label: string }[] = [
  { id: 'pending', label: '待审核' }, { id: 'approved', label: '已通过' }, { id: 'rejected', label: '已打回' }, { id: 'removed', label: '已下架' },
]
const emit = defineEmits<{ counts: [pending: number] }>()
const status = ref<Tab>('pending')
const counts = reactive<Record<Tab, number>>({ pending: 0, approved: 0, rejected: 0, removed: 0 })
const items = ref<AdminResourceItem[]>([])
const page = ref(1)
const total = ref(0)
const hasMore = ref(false)
const loading = ref(false)
const error = ref('')
const busy = ref('')
const notice = ref('')
const noticeError = ref(false)
const reasonFor = ref<{ id: string; kind: 'reject' | 'remove' } | null>(null)
const reasonText = ref('')
const preview = ref<{ id: string; url: string } | null>(null)
// 位置：审核时可以顺手整理（只是展示用的，不改状态、不需要重审）
const folders = ref<string[]>([])
const folderEdit = ref<{ id: string; text: string } | null>(null)
function editFolder(r: AdminResourceItem) { folderEdit.value = { id: r.id, text: r.folderRaw ?? '' } }
async function saveFolder(r: AdminResourceItem) {
  if (!folderEdit.value) return
  busy.value = r.id
  try {
    const res = await resourceApi.adminSetFolder(r.id, folderEdit.value.text)
    r.folder = res.folder
    r.folderRaw = res.folderRaw
    folderEdit.value = null
    say(`《${r.title}》的位置改成「${res.folder}」`)
  } catch (e) { say(getErrorMessage(e, '保存位置失败'), true) } finally { busy.value = '' }
}

const time = (v: string) => new Date(v).toLocaleString('zh-CN', { month: 'numeric', day: 'numeric', hour: '2-digit', minute: '2-digit' })
function say(text: string, isError = false) { notice.value = text; noticeError.value = isError }

async function loadCounts() {
  try { Object.assign(counts, await resourceApi.adminCounts()); emit('counts', counts.pending) } catch { /* 读不到就不显示数字 */ }
}

async function load(p = 1) {
  loading.value = true
  error.value = ''
  try {
    const res = await resourceApi.adminList(status.value, p)
    items.value = p === 1 ? res.items : items.value.concat(res.items.filter(x => !items.value.some(y => y.id === x.id)))
    page.value = res.page
    total.value = res.total
    hasMore.value = res.page * res.pageSize < res.total
  } catch (e) { error.value = getErrorMessage(e, '读取失败') } finally { loading.value = false }
}

function refresh() { void loadCounts(); void load(1) }
function switchTab(t: Tab) { status.value = t; reasonFor.value = null; preview.value = null; notice.value = ''; void load(1) }

/** 处理完一条：从当前列表拿掉，刷新数量 */
function done(r: AdminResourceItem, text: string) {
  items.value = items.value.filter(x => x.id !== r.id)
  total.value = Math.max(0, total.value - 1)
  reasonFor.value = null
  say(text)
  void loadCounts()
}

async function approve(r: AdminResourceItem) {
  busy.value = r.id
  try {
    const res = await resourceApi.approve(r.id)
    done(r, `《${r.title}》已通过，已经公开到知识库${res.exp > 0 ? `，上传者获得 ${res.exp} 经验` : ''}`)
  } catch (e) { say(getErrorMessage(e, '操作失败'), true) } finally { busy.value = '' }
}

async function restore(r: AdminResourceItem) {
  busy.value = r.id
  try { await resourceApi.restore(r.id); done(r, `《${r.title}》已恢复公开`) }
  catch (e) { say(getErrorMessage(e, '操作失败'), true) } finally { busy.value = '' }
}

function askReason(r: AdminResourceItem, kind: 'reject' | 'remove') { reasonFor.value = { id: r.id, kind }; reasonText.value = '' }

async function submitReason(r: AdminResourceItem) {
  const target = reasonFor.value
  const reason = reasonText.value.trim()
  if (!target || !reason) return
  busy.value = r.id
  try {
    if (target.kind === 'reject') { await resourceApi.reject(r.id, reason); done(r, `《${r.title}》已打回，已通知上传者`) }
    else { await resourceApi.takeDown(r.id, reason); done(r, `《${r.title}》已下架，已通知上传者`) }
  } catch (e) { say(getErrorMessage(e, '操作失败'), true) } finally { busy.value = '' }
}

async function downloadFile(r: AdminResourceItem) {
  busy.value = r.id
  try { const res = await resourceApi.adminFile(r.id); startBrowserDownload(res.url, res.fileName) }
  catch (e) { say(getErrorMessage(e, '获取下载链接失败'), true) } finally { busy.value = '' }
}

const previewKind = (r: AdminResourceItem) =>
  ['.png', '.jpg', '.jpeg', '.gif', '.webp'].includes(r.ext) ? 'image'
    : ['.mp3', '.wav', '.ogg', '.flac', '.m4a'].includes(r.ext) ? 'audio'
      : ['.mp4', '.webm'].includes(r.ext) ? 'video' : 'tab'

async function previewFile(r: AdminResourceItem) {
  busy.value = r.id
  try {
    const res = await resourceApi.adminFile(r.id, true)
    if (previewKind(r) === 'tab') window.open(res.url, '_blank', 'noopener,noreferrer')
    else preview.value = { id: r.id, url: res.url }
  } catch (e) { say(getErrorMessage(e, '获取预览失败'), true) } finally { busy.value = '' }
}

onMounted(() => { refresh(); void resourceApi.folders().then(list => { folders.value = list }).catch(() => { /* 读不到就只能手填 */ }) })
</script>

<style scoped>
.rv { display: grid; gap: 16px; }
.rv-head { display: flex; justify-content: space-between; align-items: start; gap: 16px; }
.rv-head h2 { margin: 0 0 6px; font-size: 18px; }
.rv-head p { margin: 0; color: var(--text-secondary); font-size: 13px; line-height: 1.7; max-width: 760px; }
.rv-tabs { display: flex; flex-wrap: wrap; border-bottom: 1px solid var(--border-line); }
.rv-tabs button { display: inline-flex; align-items: center; gap: 6px; padding: 10px 16px; border: 0; border-bottom: 2px solid transparent; border-radius: 0; background: none; color: var(--text-secondary); font: inherit; font-size: 14px; cursor: pointer; }
.rv-tabs button.on { border-bottom-color: var(--accent-color); color: var(--accent-color); font-weight: 600; }
.rv-tabs b { min-width: 18px; padding: 0 5px; background: var(--bg-sub); color: var(--text-secondary); font-size: 11px; line-height: 18px; text-align: center; }
.rv-tabs button:first-child b { background: #dc2626; color: #fff; }
.rv-notice { margin: 0; padding: 8px 12px; border-left: 2px solid var(--signal-mint); background: var(--bg-sub); font-size: 13px; }
.rv-notice.error { border-left-color: var(--danger); background: var(--danger-soft); color: var(--danger); }
.rv-empty { padding: 28px 0; text-align: center; color: var(--text-secondary); }
.rv-list { display: grid; gap: 12px; margin: 0; padding: 0; list-style: none; }
.rv-item { display: grid; gap: 10px; padding: 16px 18px; border: 1px solid var(--border-line); border-left: 3px solid var(--accent-color); background: var(--paper-card); }
.rv-top { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.rv-plate { padding: 2px 6px; border: 1px solid var(--text-primary); font: 600 10px var(--font-mono); letter-spacing: .08em; }
.rv-title { font-size: 15px; overflow-wrap: anywhere; }
.rv-cat { font: 11px var(--font-mono); color: var(--accent-color); }
.rv-price { margin-left: auto; font-weight: 700; color: var(--accent-color); }
.rv-meta { margin: 0; color: var(--text-secondary); font-size: 12px; }
.rv-file { display: grid; grid-template-columns: 2fr 1fr 1fr 2fr; gap: 1px; margin: 0; background: var(--border-line); border: 1px solid var(--border-line); }
.rv-file div { padding: 6px 10px; background: var(--bg-sub); min-width: 0; }
.rv-file dt { font: 10px var(--font-mono); letter-spacing: .08em; color: var(--text-secondary); }
.rv-file dd { margin: 2px 0 0; font-size: 12px; overflow-wrap: anywhere; }
.rv-link { padding: 0; border: 0; background: none; color: var(--accent-color); font: inherit; font-size: 12px; text-decoration: underline; cursor: pointer; }
.rv-loc-edit { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; }
.rv-loc-edit input { flex: 1; min-width: 120px; padding: 4px 6px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 12px; }
.rv-warn { margin: 0; padding: 8px 12px; list-style: none; border-left: 2px solid var(--warning); background: var(--warning-soft); color: var(--warning); font-size: 12px; line-height: 1.7; }
.rv-tags { display: flex; flex-wrap: wrap; gap: 8px; margin: 0; font-size: 12px; color: var(--accent-color); }
.rv-desc summary { cursor: pointer; color: var(--text-secondary); font-size: 12px; }
.rv-desc p { margin: 6px 0 0; white-space: pre-wrap; overflow-wrap: anywhere; font-size: 13px; line-height: 1.7; }
.rv-reason { margin: 0; padding: 8px 12px; border-left: 2px solid var(--danger); background: var(--danger-soft); color: var(--danger); font-size: 12px; }
.rv-preview img, .rv-preview video { display: block; max-width: 100%; max-height: 360px; background: var(--bg-sub); }
.rv-preview audio { width: 100%; }
.rv-reason-form { display: grid; gap: 8px; padding: 12px; border: 1px solid var(--border-line); background: var(--bg-sub); }
.rv-reason-form label { font-size: 13px; color: var(--text-secondary); }
.rv-reason-form textarea { padding: 8px 10px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; resize: vertical; }
.rv-ops { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 8px; }
.rv .btn-line { min-height: 34px; padding: 6px 14px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.rv .btn-line:not(:disabled):hover { border-color: var(--accent-color); }
.rv .btn-line:disabled { opacity: .5; cursor: not-allowed; }
.rv .btn-primary, .rv .btn-primary:not(:disabled):hover { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent); clip-path: polygon(0 0, calc(100% - var(--chamfer, 10px)) 0, 100% var(--chamfer, 10px), 100% 100%, var(--chamfer, 10px) 100%, 0 calc(100% - var(--chamfer, 10px))); }
.rv .danger { color: var(--danger); }
.rv-more { justify-self: center; }
@media (max-width: 700px) {
  .rv-head { flex-direction: column; }
  .rv-file { grid-template-columns: minmax(0, 1fr); }
  .rv-tabs { flex-wrap: nowrap; overflow-x: auto; min-width: 0; max-width: 100%; }
  .rv-tabs button { white-space: nowrap; flex-shrink: 0; }
  .rv-ops button { flex: 1; }
}
</style>
