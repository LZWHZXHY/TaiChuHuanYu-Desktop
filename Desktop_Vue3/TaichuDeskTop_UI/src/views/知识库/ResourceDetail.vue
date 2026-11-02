<!-- 资源详情弹窗：介绍、标签、文件信息；下载按钮。
     价格单位是经验：付费资源第一次下载先确认「会花掉你 x 点经验（等级可能下降），买一次永久可重下」并显示现有经验和花完后的等级；
     经验不够时告诉还差多少。资源区不碰土豆。 -->
<template>
  <div class="rdt-mask" role="dialog" aria-modal="true" :aria-label="view ? view.title : '资源详情'" @click.self="emit('close')">
    <article class="rdt-panel">
      <button type="button" class="rdt-x" aria-label="关闭" @click="emit('close')">×</button>
      <p v-if="loading && !view" class="rdt-state" role="status">正在读取资源…</p>
      <p v-else-if="loadError && !view" class="rdt-state" role="alert">{{ loadError }}</p>
      <template v-else-if="view">
        <div class="rdt-plates">
          <span class="rdt-plate strong">{{ extLabel }}</span>
          <span class="rdt-plate">{{ view.kind === 'official' ? '官方资源' : view.category }}</span>
          <span class="rdt-plate">{{ formatBytes(view.size) }}</span>
          <span v-if="view.purchased" class="rdt-plate ok">已购买</span>
          <span v-if="view.mine" class="rdt-plate ok">我上传的</span>
        </div>
        <h2>{{ view.title }}</h2>
        <p class="rdt-by">
          <template v-if="view.uploader"><UserLink :id="view.uploader.id" :name="view.uploader.name">{{ view.uploader.name }}</UserLink> 上传</template>
          <template v-else>太初官方</template>
          <time v-if="view.publishedAt" :datetime="view.publishedAt"> · {{ dateText(view.publishedAt) }}</time>
          <template v-if="view.kind === 'community'"> · {{ view.uniqueDownloadCount }} 人下载过</template>
        </p>
        <ul v-if="view.tags.length" class="rdt-tags"><li v-for="t in view.tags" :key="t">#{{ t }}</li></ul>
        <div v-if="view.description" class="rdt-desc">{{ view.description }}</div>
        <p class="rdt-file"><span>位置</span><b>{{ folderText(view.folder) }}</b></p>
        <p class="rdt-file"><span>文件</span><b>{{ view.fileName }}</b></p>

        <!-- 下载区 -->
        <div class="rdt-buy" :class="phase">
          <template v-if="phase === 'confirm'">
            <p class="rdt-buy-title">会花掉你 <b>{{ view.price }}</b> 点经验</p>
            <p class="rdt-buy-sub">
              花掉的经验全部加给上传者，买一次永久可以重新下载。
              你现在有 <b>{{ myExp ? myExp.experience.toLocaleString() : '…' }}</b> 经验（Lv.{{ myExp?.level ?? '…' }}），
              花完剩 <b>{{ myExp ? myExp.experienceAfter.toLocaleString() : '…' }}</b> 经验（Lv.{{ myExp?.levelAfter ?? '…' }}）。
            </p>
            <p v-if="myExp && myExp.levelAfter < myExp.level" class="rdt-buy-warn">⚠ 花完后等级会从 Lv.{{ myExp.level }} 降到 Lv.{{ myExp.levelAfter }}。</p>
            <div class="rdt-buy-actions">
              <button type="button" class="btn-line" :disabled="working" @click="phase = 'idle'">再想想</button>
              <button type="button" class="btn-line btn-primary" :disabled="working || !myExp || myExp.experience < view.price" @click="doDownload(view.price)">{{ working ? '处理中…' : `确认，花 ${view.price} 经验` }}</button>
            </div>
          </template>
          <template v-else-if="phase === 'insufficient'">
            <p class="rdt-buy-title">经验不够，还差 <b>{{ shortfall }}</b> 经验</p>
            <p class="rdt-buy-sub">这个资源要 {{ view.price }} 经验，你现在有 {{ (myExp?.experience ?? 0).toLocaleString() }} 经验。签到、发布内容、参加活动都能攒经验，攒够了再来。</p>
            <div class="rdt-buy-actions"><button type="button" class="btn-line" @click="phase = 'idle'">返回</button></div>
          </template>
          <template v-else>
            <p v-if="view.kind === 'official'" class="rdt-buy-sub">官方资源，免费下载，不用登录。</p>
            <p v-else-if="view.mine" class="rdt-buy-sub">这是你上传的资源，自己下载不扣经验。</p>
            <p v-else-if="view.price > 0 && view.purchased" class="rdt-buy-sub">你已经买过了，可以随时重新下载。</p>
            <p v-else-if="view.price > 0" class="rdt-buy-sub">付费资源：<b>{{ priceText(view.price) }}</b>，买一次永久可以重新下载。</p>
            <p v-else class="rdt-buy-sub">免费资源{{ user.isLoggedIn ? '' : '，登录后就能下载' }}。</p>
            <div class="rdt-buy-actions">
              <button v-if="view.kind === 'community' && !user.isLoggedIn" type="button" class="btn-line btn-primary" @click="goLogin">登录后下载</button>
              <button v-else type="button" class="btn-line btn-primary" :disabled="working" @click="startDownload">{{ working ? '获取链接中…' : buttonText }}</button>
            </div>
          </template>
          <p v-if="message" class="rdt-ok" role="status">{{ message }}</p>
          <p v-if="error" class="rdt-error" role="alert">{{ error }}</p>
        </div>
      </template>
    </article>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import UserLink from '@/components/UserLink.vue'
import { useUserStore } from '@/stores/user'
import { LOGIN_PATH } from '@/router/auth'
import { getErrorMessage } from '@/utils/apiError'
import { knowledgeApi } from '@/api/knowledge'
import { folderText, formatBytes, priceText, resourceApi, resourceErrorData, startBrowserDownload, type MyExp, type ResourceItem } from '@/api/resources'

const props = defineProps<{ item?: ResourceItem | null; id?: string }>()
const emit = defineEmits<{ close: []; changed: [item: ResourceItem] }>()
const user = useUserStore()
const route = useRoute()
const router = useRouter()

const view = ref<ResourceItem | null>(props.item ? { ...props.item } : null)
const loading = ref(false)
const loadError = ref('')
type Phase = 'idle' | 'confirm' | 'insufficient'
const phase = ref<Phase>('idle')
/** 我现在的经验、等级，以及花掉这个资源的价格后剩多少、是几级 */
const myExp = ref<MyExp | null>(null)
const working = ref(false)
const message = ref('')
const error = ref('')

const extLabel = computed(() => (view.value?.ext ? view.value.ext.slice(1, 6).toUpperCase() : 'FILE'))
const shortfall = computed(() => Math.max(0, (view.value?.price ?? 0) - (myExp.value?.experience ?? 0)))
const buttonText = computed(() => {
  const v = view.value
  if (!v) return '下载'
  if (v.kind === 'community' && v.price > 0 && !v.purchased && !v.mine) return `购买并下载（${priceText(v.price)}）`
  return '下载'
})
const dateText = (v: string) => new Date(v).toLocaleDateString('zh-CN', { year: 'numeric', month: 'long', day: 'numeric' })

/** 社区资源：读完整介绍（列表里只有前 300 字） */
async function loadDetail() {
  const id = props.item?.kind === 'official' ? '' : props.item?.id ?? props.id ?? ''
  if (!id) return
  loading.value = true
  loadError.value = ''
  try {
    const d = await resourceApi.detail(id)
    view.value = { ...d, purchased: d.purchased ?? view.value?.purchased ?? false }
  } catch (e) {
    loadError.value = getErrorMessage(e, '资源不存在、还没公开或者已经下架了')
  } finally { loading.value = false }
}

async function loadExp() {
  try { myExp.value = await resourceApi.myExp(view.value?.price ?? 0) } catch { myExp.value = null }
}

function goLogin() { void router.push({ path: LOGIN_PATH, query: { redirect: route.fullPath } }) }

async function startDownload() {
  const v = view.value
  if (!v) return
  error.value = ''
  message.value = ''
  if (v.kind === 'official') {
    // 官方资源：和以前一样，匿名拿临时链接
    working.value = true
    try {
      const res = await knowledgeApi.download(v.key ?? '')
      if (!res.success) throw new Error()
      startBrowserDownload(res.downloadUrl, v.fileName)
      message.value = '开始下载了'
    } catch { error.value = '获取下载链接失败，请再点一次' } finally { working.value = false }
    return
  }
  if (!user.isLoggedIn) { goLogin(); return }
  if (v.price > 0 && !v.purchased && !v.mine) {
    // 付费资源第一次下载：先确认、看经验够不够
    phase.value = 'confirm'
    await loadExp()
    if (myExp.value && myExp.value.experience < v.price) phase.value = 'insufficient'
    return
  }
  await doDownload()
}

async function doDownload(confirmPrice?: number) {
  const v = view.value
  if (!v || working.value) return
  working.value = true
  error.value = ''
  message.value = ''
  try {
    const res = await resourceApi.download(v.id, confirmPrice)
    startBrowserDownload(res.url, res.fileName)
    if (res.charged > 0) {
      message.value = `已花 ${res.charged} 经验${res.balance != null ? `，还剩 ${res.balance.toLocaleString()} 经验` : ''}。以后可以随时重新下载。`
    } else message.value = '开始下载了（下载链接 15 分钟内有效）'
    phase.value = 'idle'
    const updated: ResourceItem = { ...v, purchased: v.purchased || res.charged > 0, downloadCount: v.mine ? v.downloadCount : v.downloadCount + 1 }
    view.value = updated
    emit('changed', updated)
  } catch (e) {
    const data = resourceErrorData(e)
    if (data?.code === 'insufficient') {
      await loadExp()
      phase.value = 'insufficient'
    } else if (data?.code === 'confirm') {
      // 上传者刚改了价格：用新价格重新确认
      if (typeof data.price === 'number') view.value = { ...v, price: data.price }
      phase.value = 'confirm'
      error.value = '价格刚刚变了，请再确认一次'
      await loadExp()
    } else error.value = getErrorMessage(e, '获取下载链接失败，请再点一次')
  } finally { working.value = false }
}

onMounted(() => { void loadDetail() })
</script>

<style scoped>
.rdt-mask { position: fixed; inset: 0; z-index: 10000; display: grid; place-items: start center; padding: 5vh 16px; overflow-y: auto; background: var(--overlay, #0008); }
.rdt-panel { position: relative; width: min(720px, 100%); padding: 26px 26px 24px; background: var(--paper-card); color: var(--text-primary); border-top: 3px solid var(--accent-color); box-shadow: var(--shadow-md); }
.rdt-x { position: absolute; top: 12px; right: 12px; width: 34px; height: 34px; border: 1px solid var(--border-line); border-radius: 0; background: transparent; color: var(--text-secondary); font-size: 20px; line-height: 1; cursor: pointer; }
.rdt-x:hover { border-color: var(--accent-color); color: var(--accent-color); }
.rdt-state { padding: 30px 0; text-align: center; color: var(--text-secondary); }
.rdt-plates { display: flex; flex-wrap: wrap; gap: 6px; padding-right: 40px; }
.rdt-plate { padding: 3px 8px; border: 1px solid var(--border-line); font: 11px var(--font-mono); letter-spacing: .06em; color: var(--text-secondary); }
.rdt-plate.strong { border-color: var(--text-primary); color: var(--text-primary); font-weight: 600; }
.rdt-plate.ok { border-color: var(--success); color: var(--success); background: var(--success-soft); }
.rdt-panel h2 { margin: 14px 0 6px; font-size: clamp(20px, 3vw, 26px); line-height: 1.35; overflow-wrap: anywhere; }
.rdt-by { margin: 0; color: var(--text-secondary); font-size: 13px; }
.rdt-tags { display: flex; flex-wrap: wrap; gap: 8px; margin: 14px 0 0; padding: 0; list-style: none; }
.rdt-tags li { font-size: 12px; color: var(--accent-color); }
.rdt-desc { margin: 16px 0 0; padding: 14px 16px; border-left: 2px solid var(--surface-edge, var(--border-line)); background: var(--bg-sub); white-space: pre-wrap; overflow-wrap: anywhere; font-size: 14px; line-height: 1.8; }
.rdt-file { display: flex; gap: 10px; margin: 14px 0 0; font-size: 13px; color: var(--text-secondary); }
.rdt-file b { color: var(--text-primary); font-weight: 500; overflow-wrap: anywhere; }
.rdt-buy { margin-top: 20px; padding: 16px; border: 1px solid var(--border-line); border-left: 3px solid var(--accent-color); display: grid; gap: 10px; }
.rdt-buy.insufficient { border-left-color: var(--warning); background: var(--warning-soft); }
.rdt-buy-title { margin: 0; font-size: 16px; font-weight: 600; }
.rdt-buy-title b { color: var(--accent-color); font-size: 20px; }
.rdt-buy.insufficient .rdt-buy-title b { color: var(--warning); }
.rdt-buy-sub { margin: 0; color: var(--text-secondary); font-size: 13px; line-height: 1.7; }
.rdt-buy-sub b { color: var(--text-primary); }
.rdt-buy-sub a { color: var(--accent-color); }
.rdt-buy-warn { margin: 0; color: var(--warning); font-size: 13px; }
.rdt-buy-actions { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 10px; }
.rdt-buy-actions button { min-height: 40px; padding: 9px 18px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 14px; cursor: pointer; }
.rdt-buy-actions .btn-primary, .rdt-buy-actions .btn-primary:not(:disabled):hover { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent); clip-path: polygon(0 0, calc(100% - var(--chamfer, 10px)) 0, 100% var(--chamfer, 10px), 100% 100%, var(--chamfer, 10px) 100%, 0 calc(100% - var(--chamfer, 10px))); }
.rdt-buy-actions button:disabled { opacity: .5; cursor: not-allowed; }
.rdt-ok { margin: 0; color: var(--success); font-size: 13px; }
.rdt-error { margin: 0; color: var(--danger); font-size: 13px; }
@media (max-width: 560px) {
  .rdt-mask { padding: 0; place-items: stretch; }
  .rdt-panel { min-height: 100%; padding: 22px 16px; }
  .rdt-buy-actions button { flex: 1; }
}
</style>
