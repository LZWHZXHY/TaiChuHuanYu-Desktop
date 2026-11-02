<!-- 知识库「下载资源」区块：资源卡片（官方资源 + 审核通过的社区资源）、分类 / 排序筛选、详情弹窗、上传入口、我的资源。
     compact = 在「全部资料」里只显示 6 个，点「查看全部」切到下载资源类型。
     地址栏 ?res=资源id 直接打开详情（通知、社区动态里的链接），?mine=1 打开「我的资源」。 -->
<template>
  <section class="rs resource-section" aria-labelledby="rs-heading">
    <header class="rs-head">
      <h3 id="rs-heading">下载资源 <span v-if="!loading && !error" class="rs-count">({{ total }})</span></h3>
      <div class="rs-head-actions">
        <button v-if="user.isLoggedIn" type="button" class="btn-line" @click="openMine">我的资源</button>
        <button type="button" class="btn-line btn-primary" @click="openUpload">上传资源</button>
      </div>
    </header>
    <p v-if="!compact" class="rs-intro">社区成员分享的音效、素材、模板、字体、模型……以及站长放的官方资源。上传的资源经管理员审核后公开；付费资源花的经验全部加给上传者，买一次永久可以重下。</p>

    <div v-if="!compact" class="rs-filters">
      <div class="rs-cats" role="group" aria-label="资源分类">
        <button v-for="c in categoryTabs" :key="c.value" type="button" :aria-pressed="category === c.value" :class="{ on: category === c.value }" @click="setCategory(c.value)">{{ c.label }}</button>
      </div>
      <label class="rs-sort">位置
        <select :value="folder" aria-label="按位置筛选" @change="setFolder(($event.target as HTMLSelectElement).value)">
          <option value="">全部位置</option>
          <option value="/">根目录（官方）</option>
          <option v-for="f in folders" :key="f" :value="f">{{ f }}</option>
        </select>
      </label>
      <label class="rs-sort">排序
        <select :value="sort" aria-label="排序方式" @change="setSort(($event.target as HTMLSelectElement).value as ResourceSort)">
          <option value="new">最新</option>
          <option value="downloads">下载最多</option>
          <option value="free">只看免费</option>
          <option value="paid">只看付费</option>
        </select>
      </label>
    </div>

    <p v-if="loading && !items.length" class="rs-state" role="status">正在加载资源…</p>
    <div v-else-if="error" class="rs-state" role="alert"><p>{{ error }}</p><button type="button" class="btn-line" @click="load">重新加载</button></div>
    <template v-else>
      <p v-if="partialNote" class="rs-note">{{ partialNote }}</p>
      <p v-if="!items.length" class="rs-state">{{ search ? '没有找到匹配的资源。' : '这里还没有资源，来上传第一个吧。' }}</p>
      <ul v-else class="rs-grid">
        <li v-for="item in items" :key="item.id" class="rs-card" :class="{ official: item.kind === 'official' }">
          <div class="rs-card-top">
            <span class="rs-plate">{{ extOf(item) }}</span>
            <span class="rs-cat">{{ item.kind === 'official' ? '官方资源' : item.category }}</span>
            <span v-if="item.purchased" class="rs-flag">已购买</span>
            <span v-if="item.mine" class="rs-flag mine">我的</span>
          </div>
          <h4><button type="button" class="rs-title" @click="openDetail(item)">{{ item.title }}</button></h4>
          <p class="rs-loc" :title="'位置：' + folderText(item.folder)">位置：{{ folderText(item.folder) }}</p>
          <p v-if="item.description" class="rs-desc">{{ item.description }}</p>
          <p class="rs-meta">
            <span>{{ formatBytes(item.size) }}</span>
            <span v-if="item.uploader"><UserLink :id="item.uploader.id" :name="item.uploader.name">{{ item.uploader.name }}</UserLink></span>
            <span v-else>太初官方</span>
            <span v-if="item.kind === 'community'" :title="`${item.uniqueDownloadCount} 人下载过，共 ${item.downloadCount} 次`">↓ {{ item.downloadCount }}</span>
          </p>
          <footer class="rs-card-foot">
            <span class="rs-price" :class="{ free: !item.price }">{{ priceText(item.price) }}</span>
            <button type="button" class="btn-line" :disabled="quickBusy === item.id" @click="quickDownload(item)">{{ quickBusy === item.id ? '获取中…' : quickLabel(item) }}</button>
          </footer>
        </li>
      </ul>
      <p v-if="quickError" class="rs-error" role="alert">{{ quickError }}</p>
      <button v-if="compact && total > items.length" type="button" class="btn-line rs-more" @click="emit('more')">查看全部 {{ total }} 个资源</button>
      <nav v-if="!compact && total > PAGE_SIZE" class="rs-pages" aria-label="资源分页">
        <button type="button" class="btn-line" :disabled="page <= 1" @click="emit('page', page - 1)">上一页</button>
        <span>{{ page }} / {{ lastPage }}</span>
        <button type="button" class="btn-line" :disabled="page >= lastPage" @click="emit('page', page + 1)">下一页</button>
      </nav>
    </template>

    <ResourceDetail v-if="detailItem || detailId" :key="detailItem?.id || detailId" :item="detailItem" :id="detailId" @close="closeDetail" @changed="onChanged" />
    <ResourceUploadDialog v-if="uploadOpen" :resume="resumeTarget" @close="closeUpload" @submitted="onSubmitted" @open-mine="openMineFromUpload" />
    <MyResources v-if="mineOpen" @close="closeMine" @resume="onResume" @changed="load" />
  </section>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import UserLink from '@/components/UserLink.vue'
import { useUserStore } from '@/stores/user'
import { LOGIN_PATH } from '@/router/auth'
import { getErrorMessage } from '@/utils/apiError'
import { knowledgeApi } from '@/api/knowledge'
import { RESOURCE_CATEGORIES, folderText, formatBytes, priceText, resourceApi, startBrowserDownload, type MyResourceItem, type ResourceItem, type ResourceSort } from '@/api/resources'
import ResourceDetail from './ResourceDetail.vue'
import ResourceUploadDialog from './ResourceUploadDialog.vue'
import MyResources from './MyResources.vue'

const props = defineProps<{ search: string; compact?: boolean; page: number }>()
const emit = defineEmits<{ more: []; page: [page: number] }>()
const user = useUserStore()
const route = useRoute()
const router = useRouter()

const PAGE_SIZE = 24
const categoryTabs = [{ value: '', label: '全部' }, { value: 'official', label: '官方' }, ...RESOURCE_CATEGORIES.map(c => ({ value: c, label: c }))]
const category = ref('')
const sort = ref<ResourceSort>('new')
// 按位置筛选（含子文件夹；「/」= 官方资源的根目录）
const folder = ref('')
const folders = ref<string[]>([])
async function loadFolders() { try { folders.value = await resourceApi.folders() } catch { folders.value = [] } }
const items = ref<ResourceItem[]>([])
const total = ref(0)
const loading = ref(false)
const error = ref('')
const partialNote = ref('')
const lastPage = computed(() => Math.max(1, Math.ceil(total.value / PAGE_SIZE)))
let version = 0

async function load() {
  const v = ++version
  loading.value = true
  error.value = ''
  try {
    const res = await resourceApi.list({
      q: props.search || undefined,
      category: props.compact ? undefined : category.value || undefined,
      folder: props.compact ? undefined : folder.value || undefined,
      sort: props.compact ? 'new' : sort.value,
      page: props.compact ? 1 : props.page,
      pageSize: props.compact ? 6 : PAGE_SIZE,
    })
    if (v !== version) return
    items.value = res.items
    total.value = res.total
    partialNote.value = res.officialError ? '官方资源目录暂时读不到（NAS 可能在维护），先显示社区资源。'
      : res.communityError ? '社区资源暂时读不到，先显示官方资源。' : ''
    // 页码超出（比如资源被删了）：回到最后一页
    if (!props.compact && props.page > 1 && props.page > Math.max(1, Math.ceil(res.total / PAGE_SIZE))) emit('page', Math.max(1, Math.ceil(res.total / PAGE_SIZE)))
  } catch (e) {
    if (v === version) error.value = getErrorMessage(e, '下载资源暂时读不到，其他社区资料仍可浏览。')
  } finally {
    if (v === version) loading.value = false
  }
}
watch(() => [props.search, props.page, props.compact, category.value, sort.value, folder.value], () => { void load() }, { immediate: true })
watch(() => props.compact, compact => { if (!compact && !folders.value.length) void loadFolders() }, { immediate: true })

function setCategory(value: string) { category.value = value; if (props.page > 1) emit('page', 1) }
function setSort(value: ResourceSort) { sort.value = value; if (props.page > 1) emit('page', 1) }
function setFolder(value: string) { folder.value = value; if (props.page > 1) emit('page', 1) }

const extOf = (item: ResourceItem) => (item.ext ? item.ext.slice(1, 6).toUpperCase() : 'FILE')
const quickLabel = (item: ResourceItem) => (item.kind === 'community' && item.price > 0 && !item.purchased && !item.mine ? '购买下载' : '下载')

// ---------- 下载：免费 / 已买 / 官方直接下；付费没买过或者没登录，打开详情走确认 ----------
const quickBusy = ref('')
const quickError = ref('')
async function quickDownload(item: ResourceItem) {
  quickError.value = ''
  const needsDetail = item.kind === 'community' && (!user.isLoggedIn || (item.price > 0 && !item.purchased && !item.mine))
  if (needsDetail) { openDetail(item); return }
  quickBusy.value = item.id
  try {
    if (item.kind === 'official') {
      const res = await knowledgeApi.download(item.key ?? '')
      if (!res.success) throw new Error()
      startBrowserDownload(res.downloadUrl, item.fileName)
    } else {
      const res = await resourceApi.download(item.id)
      startBrowserDownload(res.url, res.fileName)
      if (!item.mine) onChanged({ ...item, downloadCount: item.downloadCount + 1 })
    }
  } catch (e) {
    quickError.value = getErrorMessage(e, '获取下载链接失败，请再点一次')
  } finally { quickBusy.value = '' }
}

// ---------- 详情（?res=资源id 也能直接打开） ----------
const detailItem = ref<ResourceItem | null>(null)
const detailId = computed(() => (!detailItem.value && typeof route.query.res === 'string' ? route.query.res : ''))
function openDetail(item: ResourceItem) { detailItem.value = item }
function closeDetail() {
  detailItem.value = null
  if (route.query.res) void router.replace({ query: { ...route.query, res: undefined } })
}
function onChanged(updated: ResourceItem) {
  const i = items.value.findIndex(x => x.id === updated.id)
  if (i >= 0) items.value.splice(i, 1, { ...items.value[i], ...updated, description: items.value[i]?.description ?? updated.description })
}

// ---------- 上传 / 我的资源 ----------
const uploadOpen = ref(false)
const resumeTarget = ref<{ id: string; title: string; fileName: string; size: number } | null>(null)
const mineOpen = computed(() => route.query.mine === '1')
function needLogin() {
  if (user.isLoggedIn) return false
  void router.push({ path: LOGIN_PATH, query: { redirect: route.fullPath } })
  return true
}
function openUpload() { if (needLogin()) return; resumeTarget.value = null; uploadOpen.value = true }
function closeUpload() { uploadOpen.value = false; resumeTarget.value = null }
function onSubmitted() { void load() }
function openMine() { if (!needLogin()) void router.push({ query: { ...route.query, mine: '1' } }) }
function closeMine() { void router.replace({ query: { ...route.query, mine: undefined } }) }
function openMineFromUpload() { closeUpload(); openMine() }
function onResume(item: MyResourceItem) {
  closeMine()
  resumeTarget.value = { id: item.id, title: item.title, fileName: item.fileName, size: item.size }
  uploadOpen.value = true
}

defineExpose({ reload: load })
</script>

<style scoped>
.rs { margin-top: 28px; padding-top: 16px; border-top: 1px solid var(--border-line); }
.rs-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-wrap: wrap; }
.rs-head h3 { margin: 0; font-size: 1.1rem; }
.rs-count { color: var(--text-secondary); font-weight: 400; font-size: .9rem; }
.rs-head-actions { display: flex; gap: 8px; flex-wrap: wrap; }
.rs .btn-line { min-height: 36px; padding: 7px 14px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: .86rem; cursor: pointer; }
.rs .btn-line:not(:disabled):hover { border-color: var(--accent-color); }
.rs .btn-line:disabled { opacity: .5; cursor: wait; }
.rs .btn-primary, .rs .btn-primary:not(:disabled):hover { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent); clip-path: polygon(0 0, calc(100% - var(--chamfer, 10px)) 0, 100% var(--chamfer, 10px), 100% 100%, var(--chamfer, 10px) 100%, 0 calc(100% - var(--chamfer, 10px))); }
.rs .btn-primary:not(:disabled):hover { box-shadow: inset 0 -3px 0 var(--signal-mint); }
.rs-intro { margin: 10px 0 0; color: var(--text-secondary); font-size: .88rem; line-height: 1.7; }
.rs-filters { display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-wrap: wrap; margin: 16px 0 4px; padding: 8px 0; border-block: 1px solid var(--border-line); }
.rs-cats { display: flex; flex-wrap: wrap; gap: 2px; }
.rs-cats button { padding: 6px 12px; border: 0; border-radius: 0; background: transparent; color: var(--text-secondary); font: inherit; font-size: .85rem; cursor: pointer; }
.rs-cats button:hover { color: var(--text-primary); background: var(--bg-sub); }
.rs-cats button.on { color: var(--text-primary); box-shadow: inset 0 -3px 0 var(--signal-color); font-weight: 600; }
.rs-sort { display: flex; align-items: center; gap: 8px; color: var(--text-secondary); font-size: .85rem; }
.rs-sort select { max-width: 220px; padding: 7px 10px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; }
.rs-state { padding: 32px 16px; text-align: center; color: var(--text-secondary); }
.rs-note { margin: 12px 0 0; padding: 8px 12px; border-left: 2px solid var(--warning); background: var(--warning-soft); color: var(--warning); font-size: .82rem; }
.rs-error { margin: 12px 0 0; color: var(--danger); font-size: .85rem; }
.rs-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(250px, 1fr)); gap: 12px; margin: 16px 0 0; padding: 0; list-style: none; }
.rs-card { position: relative; display: flex; flex-direction: column; gap: 8px; min-width: 0; padding: 14px 16px; border: 1px solid var(--border-line); border-top: 2px solid var(--surface-edge, var(--border-line)); background: linear-gradient(160deg, var(--paper-card) 60%, var(--surface-wash, var(--paper-card))); transition: border-color .2s; }
.rs-card:hover, .rs-card:focus-within { border-color: var(--accent-color); }
.rs-card.official { border-top-color: var(--signal-mint); }
.rs-card-top { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
.rs-plate { padding: 2px 6px; border: 1px solid var(--text-primary); font: 600 10px var(--font-mono); letter-spacing: .08em; color: var(--text-primary); }
.rs-cat { font: 11px var(--font-mono); color: var(--accent-color); letter-spacing: .04em; }
.rs-flag { margin-left: auto; padding: 1px 6px; font-size: 11px; color: var(--success); background: var(--success-soft); }
.rs-flag.mine { color: var(--info); background: var(--info-soft); }
.rs-flag + .rs-flag { margin-left: 0; }
.rs-card h4 { margin: 0; }
.rs-title { padding: 0; border: 0; background: none; color: var(--text-primary); font: inherit; font-size: 1rem; font-weight: 600; text-align: left; cursor: pointer; overflow-wrap: anywhere; }
.rs-title:hover { color: var(--accent-color); }
.rs-loc { margin: 0; color: var(--text-secondary); font: 11px var(--font-mono); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.rs-desc { margin: 0; color: var(--text-secondary); font-size: .82rem; line-height: 1.6; display: -webkit-box; -webkit-line-clamp: 2; line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; overflow-wrap: anywhere; }
.rs-meta { display: flex; flex-wrap: wrap; gap: 4px 12px; margin: 0; color: var(--text-secondary); font-size: .78rem; }
.rs-meta :deep(a) { color: inherit; }
.rs-card-foot { display: flex; align-items: center; justify-content: space-between; gap: 8px; margin-top: auto; padding-top: 8px; border-top: 1px dashed var(--border-line); }
.rs-price { font-weight: 700; font-variant-numeric: tabular-nums; color: var(--accent-color); }
.rs-price.free { color: var(--success); font-weight: 600; }
.rs-more { margin-top: 14px; }
.rs-pages { display: flex; align-items: center; justify-content: center; gap: 12px; margin: 24px 0; }
:is(button, select, a):focus-visible { outline: 2px solid var(--accent-color); outline-offset: 2px; }
@media (max-width: 720px) {
  .rs-grid { grid-template-columns: minmax(0, 1fr); }
  .rs-filters { flex-direction: column; align-items: stretch; }
  .rs-cats { flex-wrap: nowrap; overflow-x: auto; min-width: 0; max-width: 100%; }
  .rs-cats button { white-space: nowrap; flex-shrink: 0; }
}
</style>
