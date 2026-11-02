<!-- 我的资源：自己上传的全部资源（上传中 / 待审核 / 已公开 / 被打回 / 已下架），打回理由、下载数、经验收入；修改、删除、继续上传。 -->
<template>
  <div class="myr-mask" role="dialog" aria-modal="true" aria-label="我的资源" @click.self="emit('close')">
    <section class="myr-panel">
      <header class="myr-head">
        <div>
          <p class="myr-eyebrow">MY RESOURCES</p>
          <h2>我的资源</h2>
        </div>
        <button type="button" class="myr-x" aria-label="关闭" @click="emit('close')">×</button>
      </header>
      <dl v-if="loaded" class="myr-sum">
        <div><dt>资源</dt><dd>{{ items.length }}</dd></div>
        <div><dt>已公开</dt><dd>{{ items.filter(i => i.status === 'approved').length }}</dd></div>
        <div><dt>下载人数</dt><dd>{{ totalDownloads }}</dd></div>
        <div><dt>经验收入</dt><dd>✦ {{ totalEarned.toLocaleString() }}</dd></div>
      </dl>
      <p v-if="message" class="myr-msg" role="status">{{ message }}</p>
      <p v-if="loading && !loaded" class="myr-state" role="status">正在读取…</p>
      <p v-else-if="error" class="myr-state" role="alert">{{ error }} <button type="button" class="btn-line" @click="load">重试</button></p>
      <p v-else-if="loaded && !items.length" class="myr-state">你还没有上传过资源。</p>
      <ul v-else class="myr-list">
        <li v-for="item in items" :key="item.id" class="myr-item" :class="'is-' + item.status">
          <div class="myr-top">
            <span class="myr-status">{{ RESOURCE_STATUS_LABELS[item.status] }}</span>
            <strong class="myr-title">{{ item.title }}</strong>
            <span class="myr-price">{{ priceText(item.price) }}</span>
          </div>
          <p class="myr-meta">
            <span>{{ item.fileName }} · {{ formatBytes(item.size) }}</span>
            <span>{{ item.category }}</span>
            <span>位置：{{ folderText(item.folder) }}</span>
            <span v-if="item.status !== 'uploading'">{{ item.uniqueDownloadCount }} 人下载（共 {{ item.downloadCount }} 次）</span>
            <span v-if="item.status !== 'uploading'">经验收入 ✦ {{ item.expEarned }}</span>
            <time :datetime="item.createdAt">{{ dateText(item.createdAt) }}</time>
          </p>
          <p v-if="item.status === 'rejected' && item.reviewNote" class="myr-note">打回理由：{{ item.reviewNote }}。改好后点「修改」保存，就会重新提交审核。</p>
          <p v-else-if="item.status === 'removed'" class="myr-note">已被管理员下架{{ item.reviewNote ? '：' + item.reviewNote : '' }}。下架后谁都不能下载了（包括买过的人）。</p>
          <p v-else-if="item.status === 'pending'" class="myr-tip">等管理员审核，通过后会通知你。</p>
          <p v-else-if="item.status === 'uploading'" class="myr-tip">上次没传完。24 小时内可以继续，过时会自动清理。</p>

          <!-- 修改 -->
          <form v-if="editingId === item.id" class="myr-edit" @submit.prevent="save(item)">
            <ResourceMetaFields v-model="draft" :config="config" :disabled="busy === item.id" />
            <p v-if="item.status === 'approved'" class="myr-tip">只改价格、分类、标签、位置马上生效；改了标题或介绍需要重新审核，审核期间暂时不显示。改价格不影响已经买过的人。</p>
            <div class="myr-ops">
              <button type="button" class="btn-line" @click="editingId = ''">取消</button>
              <button type="submit" class="btn-line btn-primary" :disabled="busy === item.id">{{ busy === item.id ? '保存中…' : item.status === 'rejected' ? '保存并重新提交' : '保存' }}</button>
            </div>
          </form>
          <div v-else class="myr-ops">
            <template v-if="item.status === 'uploading'">
              <button type="button" class="btn-line btn-primary" @click="emit('resume', item)">继续上传</button>
              <button type="button" class="btn-line danger" :disabled="busy === item.id" @click="remove(item)">取消上传</button>
            </template>
            <template v-else>
              <button v-if="item.status !== 'removed'" type="button" class="btn-line" @click="edit(item)">修改</button>
              <button type="button" class="btn-line danger" :disabled="busy === item.id" @click="remove(item)">删除</button>
            </template>
          </div>
        </li>
      </ul>
    </section>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { getErrorMessage } from '@/utils/apiError'
import { RESOURCE_STATUS_LABELS, folderText, formatBytes, priceText, resourceApi, type MyResourceItem, type ResourceConfig, type ResourceMeta } from '@/api/resources'
import ResourceMetaFields from './ResourceMetaFields.vue'
import { forgetUpload } from './resourceUpload'

const emit = defineEmits<{ close: []; resume: [item: MyResourceItem]; changed: [] }>()
const items = ref<MyResourceItem[]>([])
const totalEarned = ref(0)
const totalDownloads = ref(0)
const loading = ref(false)
const loaded = ref(false)
const error = ref('')
const message = ref('')
const busy = ref('')
const editingId = ref('')
const draft = ref<ResourceMeta>({ title: '', description: '', category: '其他', tags: [], price: 0, folder: '' })
const config = ref<ResourceConfig | null>(null)

const dateText = (v: string) => new Date(v).toLocaleDateString('zh-CN', { month: 'short', day: 'numeric', year: 'numeric' })

async function load() {
  loading.value = true
  error.value = ''
  try {
    const res = await resourceApi.mine()
    items.value = res.items
    totalEarned.value = res.totalEarned
    totalDownloads.value = res.totalDownloads
    loaded.value = true
  } catch (e) { error.value = getErrorMessage(e, '读取失败') } finally { loading.value = false }
}

function edit(item: MyResourceItem) {
  draft.value = { title: item.title, description: item.description, category: item.category, tags: [...item.tags], price: item.price, folder: item.folderRaw ?? '' }
  editingId.value = item.id
  message.value = ''
}

async function save(item: MyResourceItem) {
  busy.value = item.id
  message.value = ''
  try {
    const res = await resourceApi.update(item.id, draft.value)
    message.value = res.message
    editingId.value = ''
    await load()
    emit('changed')
  } catch (e) { message.value = getErrorMessage(e, '保存失败') } finally { busy.value = '' }
}

async function remove(item: MyResourceItem) {
  const tip = item.status === 'uploading'
    ? `取消《${item.title}》的上传？已经传上去的部分会被删除。`
    : item.price > 0 && item.expEarned > 0
      ? `删除《${item.title}》？删除后谁都下载不了，包括已经买过的人。`
      : `删除《${item.title}》？删除后知识库里就看不到了。`
  if (!window.confirm(tip)) return
  busy.value = item.id
  try {
    if (item.status === 'uploading') await resourceApi.abort(item.id)
    else await resourceApi.remove(item.id)
    forgetUpload(item.id)
    message.value = item.status === 'uploading' ? '已取消上传' : '已删除'
    await load()
    emit('changed')
  } catch (e) { message.value = getErrorMessage(e, '操作失败') } finally { busy.value = '' }
}

onMounted(async () => {
  void load()
  try { config.value = await resourceApi.config() } catch { /* 修改时用默认分类 */ }
})
</script>

<style scoped>
.myr-mask { position: fixed; inset: 0; z-index: 10000; display: grid; place-items: start center; padding: 5vh 16px; overflow-y: auto; background: var(--overlay, #0008); }
.myr-panel { width: min(760px, 100%); background: var(--paper-card); color: var(--text-primary); border-top: 3px solid var(--accent-color); box-shadow: var(--shadow-md); padding-bottom: 12px; }
.myr-head { display: flex; justify-content: space-between; align-items: start; gap: 12px; padding: 20px 22px 14px; border-bottom: 1px solid var(--border-line); }
.myr-eyebrow { margin: 0 0 6px; font: 10px var(--font-mono); letter-spacing: .14em; color: var(--accent-color); }
.myr-head h2 { margin: 0; font-size: 20px; }
.myr-x { width: 34px; height: 34px; border: 1px solid var(--border-line); border-radius: 0; background: transparent; color: var(--text-secondary); font-size: 20px; line-height: 1; cursor: pointer; }
.myr-sum { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 1px; margin: 16px 22px 0; background: var(--border-line); border: 1px solid var(--border-line); }
.myr-sum div { padding: 10px 12px; background: var(--paper-card); }
.myr-sum dt { font: 10px var(--font-mono); letter-spacing: .1em; color: var(--text-secondary); }
.myr-sum dd { margin: 4px 0 0; font-size: 18px; font-weight: 650; font-variant-numeric: tabular-nums; }
.myr-msg { margin: 14px 22px 0; padding: 8px 12px; border-left: 2px solid var(--signal-mint); background: var(--bg-sub); font-size: 13px; }
.myr-state { padding: 30px 22px; text-align: center; color: var(--text-secondary); }
.myr-list { display: grid; gap: 0; margin: 12px 0 0; padding: 0 22px; list-style: none; }
.myr-item { display: grid; gap: 8px; padding: 16px 0; border-bottom: 1px solid var(--border-line); }
.myr-top { display: flex; align-items: center; gap: 10px; min-width: 0; }
.myr-status { flex-shrink: 0; padding: 2px 8px; font: 11px var(--font-mono); border: 1px solid currentColor; color: var(--text-secondary); }
.is-approved .myr-status { color: var(--success); }
.is-pending .myr-status { color: var(--info); }
.is-rejected .myr-status, .is-removed .myr-status { color: var(--danger); }
.is-uploading .myr-status { color: var(--warning); }
.myr-title { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.myr-price { flex-shrink: 0; font-weight: 700; color: var(--accent-color); }
.myr-meta { display: flex; flex-wrap: wrap; gap: 4px 14px; margin: 0; color: var(--text-secondary); font-size: 12px; }
.myr-meta span:first-child { overflow-wrap: anywhere; }
.myr-note { margin: 0; padding: 8px 12px; border-left: 2px solid var(--danger); background: var(--danger-soft); color: var(--danger); font-size: 13px; line-height: 1.6; }
.myr-tip { margin: 0; color: var(--text-secondary); font-size: 12px; line-height: 1.6; }
.myr-edit { display: grid; gap: 12px; padding: 14px; border: 1px solid var(--border-line); background: var(--bg-sub); }
.myr-ops { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 8px; }
.myr-panel .btn-line { min-height: 34px; padding: 6px 14px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.myr-panel .btn-line:not(:disabled):hover { border-color: var(--accent-color); }
.myr-panel .btn-primary, .myr-panel .btn-primary:not(:disabled):hover { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent); clip-path: polygon(0 0, calc(100% - var(--chamfer, 10px)) 0, 100% var(--chamfer, 10px), 100% 100%, var(--chamfer, 10px) 100%, 0 calc(100% - var(--chamfer, 10px))); }
.myr-panel .danger { color: var(--danger); }
.myr-panel .btn-line:disabled { opacity: .5; cursor: wait; }
@media (max-width: 560px) {
  .myr-mask { padding: 0; place-items: stretch; }
  .myr-panel { min-height: 100%; }
  .myr-head, .myr-list { padding-left: 16px; padding-right: 16px; }
  .myr-sum { grid-template-columns: repeat(2, minmax(0, 1fr)); margin: 14px 16px 0; }
  .myr-top { flex-wrap: wrap; }
  .myr-title { white-space: normal; }
}
</style>
