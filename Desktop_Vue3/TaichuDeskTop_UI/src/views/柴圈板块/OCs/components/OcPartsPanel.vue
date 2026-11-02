<!-- OcPartsPanel.vue：OC 详情页的「部件图」一栏（复用知识库的社区资源：上传、审核、下载、经验定价都是同一套）。
     · 谁都能看：审核通过、挂在这个 OC 下的资源，点一下打开资源详情（介绍、下载）；
     · OC 作者：右上角「上传部件图」，传完等管理员审核；自己传的审核中 / 被打回的也显示（带状态）。
     没有部件图、也不是作者时整栏不显示。 -->
<template>
  <section v-if="items.length || canUpload" class="ocp desc-section" aria-label="部件图">
    <header class="ocp-head">
      <h3 class="desc-title">🧩 部件图</h3>
      <span v-if="items.length" class="ocp-count">{{ items.length }} 个</span>
      <span class="ocp-grow"></span>
      <button v-if="canUpload" type="button" class="ocp-upload" @click="uploadOpen = true">＋ 上传部件图</button>
    </header>
    <p v-if="canUpload && !items.length" class="ocp-tip">还没有部件图。把拆好的部件（分层 PSD、PNG 压缩包、骨骼文件……）传上来，别人点开就能下载；也可以定价收经验。</p>
    <p v-if="error" class="ocp-tip error" role="alert">{{ error }} <button type="button" class="ocp-link" @click="load">重试</button></p>

    <ul v-if="items.length" class="ocp-list">
      <li v-for="r in items" :key="r.id">
        <button type="button" class="ocp-item" :class="r.status && r.status !== 'approved' ? 'st-' + r.status : ''" @click="detailItem = r">
          <span class="ocp-plate" aria-hidden="true">{{ extLabel(r) }}</span>
          <span class="ocp-main">
            <strong class="ocp-title">{{ r.title }}</strong>
            <span class="ocp-meta">{{ formatBytes(r.size) }} · {{ r.price > 0 ? (r.purchased ? '已购买' : priceText(r.price)) : '免费' }}<template v-if="r.uniqueDownloadCount"> · {{ r.uniqueDownloadCount }} 人下载过</template></span>
          </span>
          <span v-if="r.status && r.status !== 'approved'" class="ocp-status">{{ STATUS_TEXT[r.status] ?? r.status }}</span>
          <span v-else class="ocp-dl" aria-hidden="true">↓</span>
        </button>
        <p v-if="r.status === 'rejected' && r.reviewNote" class="ocp-note">打回理由：{{ r.reviewNote }}</p>
      </li>
    </ul>

    <ResourceDetail v-if="detailItem" :key="detailItem.id" :item="detailItem" @close="detailItem = null" @changed="onChanged" />
    <ResourceUploadDialog v-if="uploadOpen" :oc="{ id: characterId, title: ocTitle }" @close="uploadOpen = false" @submitted="load" @open-mine="openMine" />
  </section>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { formatBytes, priceText, resourceApi, type MyResourceItem, type ResourceItem } from '@/api/resources'
import { getErrorMessage } from '@/utils/apiError'
import ResourceDetail from '@/views/知识库/ResourceDetail.vue'
import ResourceUploadDialog from '@/views/知识库/ResourceUploadDialog.vue'

const props = defineProps<{ characterId: string; ocTitle: string }>()
const router = useRouter()

type PartItem = ResourceItem & Partial<MyResourceItem>
const items = ref<PartItem[]>([])
const canUpload = ref(false)
const error = ref('')
const detailItem = ref<PartItem | null>(null)
const uploadOpen = ref(false)
const STATUS_TEXT: Record<string, string> = { pending: '审核中', rejected: '被打回', removed: '已下架' }

const extLabel = (r: PartItem) => (r.ext ? r.ext.replace('.', '').slice(0, 5).toUpperCase() : 'FILE')

let version = 0
async function load() {
  const v = ++version
  error.value = ''
  try {
    const res = await resourceApi.byOc(props.characterId)
    if (v !== version) return
    items.value = res.items
    canUpload.value = res.canUpload
  } catch (e) {
    if (v !== version) return
    // 后端还没更新（没有这个接口）时整栏不显示；其他错误给作者看一句
    items.value = []
    error.value = canUpload.value ? getErrorMessage(e, '部件图读取失败') : ''
  }
}
watch(() => props.characterId, id => { items.value = []; canUpload.value = false; if (id) void load() }, { immediate: true })

function onChanged(updated: ResourceItem) {
  items.value = items.value.map(r => (r.id === updated.id ? { ...r, ...updated } : r))
}
function openMine() {
  uploadOpen.value = false
  void router.push({ path: '/knowledge', query: { type: 'resource', mine: '1' } })
}
</script>

<style scoped>
.ocp { display: grid; gap: 10px; }
.ocp-head { display: flex; align-items: center; gap: 10px; }
/* 标题和详情页其他栏一个样子：父组件的样式管不到这里面，照抄一份 */
.ocp .desc-title { font-size: 15px; font-weight: 400; letter-spacing: 0.2em; margin: 0; color: var(--ink-black, var(--text-primary)); }
.ocp-count { font: 11px var(--font-mono, monospace); color: var(--text-secondary); }
.ocp-grow { flex: 1; }
.ocp-upload { padding: 5px 12px; border: 1px solid var(--accent-color); border-radius: 0; background: var(--paper-card); color: var(--accent-color); font: inherit; font-size: 12px; cursor: pointer; }
.ocp-upload:hover { background: var(--accent-soft); }
.ocp-tip { margin: 0; font-size: 13px; line-height: 1.7; color: var(--text-secondary); }
.ocp-tip.error { color: var(--danger); }
.ocp-link { border: 0; background: none; padding: 0; color: var(--accent-color); font: inherit; cursor: pointer; text-decoration: underline; }
.ocp-list { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 8px; }
.ocp-item { width: 100%; display: flex; align-items: center; gap: 10px; padding: 10px 12px; border: 1px solid var(--border-line); border-left: 3px solid var(--accent-color); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; text-align: left; cursor: pointer; transition: border-color .15s, translate .15s, box-shadow .15s; }
.ocp-item:hover, .ocp-item:focus-visible { border-color: var(--accent-color); translate: 0 -1px; box-shadow: 2px 2px 0 color-mix(in srgb, var(--accent-color) 18%, transparent); }
.ocp-item.st-pending { border-left-color: var(--warning); }
.ocp-item.st-rejected, .ocp-item.st-removed { border-left-color: var(--danger); opacity: .85; }
.ocp-plate { flex-shrink: 0; min-width: 40px; padding: 4px 6px; border: 1px solid var(--text-primary); font: 600 10px var(--font-mono, monospace); letter-spacing: .06em; text-align: center; }
.ocp-main { flex: 1; min-width: 0; display: grid; gap: 2px; }
.ocp-title { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 14px; }
.ocp-meta { font-size: 12px; color: var(--text-secondary); }
.ocp-dl { flex-shrink: 0; width: 28px; height: 28px; display: grid; place-items: center; border: 1px solid var(--border-line); color: var(--accent-color); font-size: 14px; }
.ocp-item:hover .ocp-dl { background: var(--accent-color); border-color: var(--accent-color); color: var(--on-accent); }
.ocp-status { flex-shrink: 0; padding: 1px 8px; font-size: 11px; border: 1px solid currentColor; color: var(--warning); }
.st-rejected .ocp-status, .st-removed .ocp-status { color: var(--danger); }
.ocp-note { margin: 4px 0 0; font-size: 12px; color: var(--danger); }
</style>
