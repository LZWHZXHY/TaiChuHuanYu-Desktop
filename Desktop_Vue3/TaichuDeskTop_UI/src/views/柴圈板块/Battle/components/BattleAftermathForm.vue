<!-- BattleAftermathForm.vue：写 / 修改一个 OC 的战后记录（弹窗）。
     战后状态（预设 + 自定义，最多 10 个字）、说明、视频（默认用自己在这场约战交的作品，可以改成站内视频或别的链接），
     还可以勾选「写进世界观编年史」：从这个 OC 已关联、自己能编辑的世界观里选一个，建一件事。 -->
<template>
  <el-dialog :model-value="open" :title="`${item.record ? '修改' : '写'}战后记录 · ${item.ocName}`" width="min(600px, calc(100vw - 24px))"
    class="battle-dialog" :close-on-click-modal="!saving" :close-on-press-escape="!saving" :show-close="!saving" @update:model-value="close">
    <form class="amf" @submit.prevent="save">
      <!-- 战后状态 -->
      <fieldset class="amf-field">
        <legend>战后状态 <small>必选，也可以自己写（最多 {{ MAX_STATUS_TAG }} 个字）</small></legend>
        <div class="amf-tags" role="radiogroup" aria-label="战后状态">
          <button v-for="t in STATUS_PRESETS" :key="t" type="button" role="radio" :aria-checked="tag === t && !custom"
            class="amf-tag" :class="['tone-' + statusTone(t), { on: tag === t && !custom }]" @click="pick(t)">{{ t }}</button>
          <button type="button" role="radio" :aria-checked="custom" class="amf-tag" :class="{ on: custom }" @click="startCustom">其他…</button>
        </div>
        <input v-if="custom" ref="customInput" v-model="tag" class="amf-input" :maxlength="MAX_STATUS_TAG" placeholder="比如：失忆、被封印、黑化" aria-label="自定义战后状态" />
      </fieldset>

      <label class="amf-field">
        <span class="amf-label">说明 <small>可选，比如「左臂断了，需要休养三个月」</small></span>
        <textarea v-model="note" class="amf-input" rows="3" :maxlength="MAX_NOTE" placeholder="这一战之后，TA 怎么样了？"></textarea>
        <small class="amf-count">{{ note.length }} / {{ MAX_NOTE }}</small>
      </label>

      <!-- 视频 -->
      <div class="amf-field">
        <span class="amf-label">视频 <small>可选：B 站链接会显示封面，站内视频直接播放</small></span>
        <div class="amf-row">
          <input v-model="videoUrl" class="amf-input grow" type="url" inputmode="url" placeholder="https://…（B 站 / 站内作品 / 其他链接）" aria-label="视频链接" @blur="onUrlBlur" />
        </div>
        <div class="amf-quick">
          <button v-if="defaultVideo" type="button" class="amf-link" @click="useDefault">用我在这场约战交的作品</button>
          <button type="button" class="amf-link" @click="toggleMyVideos">{{ myVideosOpen ? '收起站内视频' : '从我的站内视频选' }}</button>
          <button v-if="videoUrl" type="button" class="amf-link" @click="clearVideo">不放视频</button>
        </div>
        <div v-if="myVideosOpen" class="amf-videos">
          <p v-if="myVideosLoading" class="amf-tip" role="status">正在读取你的视频作品…</p>
          <p v-else-if="!myVideos.length" class="amf-tip">你还没有发布过视频作品。<RouterLink to="/works/create">去发布一个</RouterLink></p>
          <button v-for="w in myVideos" v-else :key="w.id" type="button" class="amf-video" :class="{ on: siteWork?.id === w.id }" @click="useSiteWork(w)">
            <img v-if="w.coverUrl" :src="w.coverUrl" alt="" loading="lazy" />
            <span v-else class="amf-video-ph" aria-hidden="true">▶</span>
            <span>{{ w.title }}</span>
          </button>
        </div>
        <p v-if="videoUrl && !safeUrl" class="amf-error">链接要以 http:// 或 https:// 开头</p>
        <p v-else-if="defaultPending && videoUrl.trim() === defaultVideo?.url" class="amf-tip">这件作品还在审核，通过之前别人看不到这个视频。</p>
        <input v-if="safeUrl" v-model="videoTitle" class="amf-input" maxlength="200" placeholder="视频标题（可选，B 站链接会自动抓取）" aria-label="视频标题" />
        <p v-if="fetching" class="amf-tip" role="status">正在抓取 B 站封面…</p>
        <VideoLinkCard v-if="safeUrl" class="amf-preview" :url="safeUrl" :title="videoTitle || null" :cover="videoCover || null" :site-work="siteWork" compact />
      </div>

      <!-- 写进世界观编年史 -->
      <div class="amf-field amf-chron">
        <p v-if="alreadyWritten" class="amf-tip">✓ 已经写进了世界观{{ item.record?.projectName ? `《${item.record.projectName}》` : '' }}的编年史。要改那件事的内容，请到编年史里改。</p>
        <template v-else>
          <p v-if="item.record?.eventDeleted" class="amf-tip">之前写进编年史的那件事已经被删了，可以重新写一次。</p>
          <label class="amf-check"><input v-model="toChronicle" type="checkbox" /> 同时写进世界观的编年史（记下这一战，世界观里也能看到）</label>
          <div v-if="toChronicle" class="amf-chron-box">
            <p v-if="worldsLoading" class="amf-tip" role="status">正在读取这个 OC 关联的世界观…</p>
            <p v-else-if="worldsError" class="amf-error">{{ worldsError }}</p>
            <p v-else-if="!editableWorlds.length" class="amf-tip">
              {{ linkedCount ? '这个 OC 关联的世界观你都没有编辑权限（要是创建者或编写者）。' : '这个 OC 还没有关联到任何世界观。' }}
              先到 <RouterLink :to="`/ocs/${item.ocId}`">OC 页面</RouterLink> 的「世界观」一栏关联一个你能编辑的世界观。
            </p>
            <template v-else>
              <label class="amf-sub">世界观
                <select v-model="projectId" class="amf-input">
                  <option v-for="w in editableWorlds" :key="w.projectId" :value="w.projectId">{{ w.name }}</option>
                </select>
              </label>
              <label class="amf-sub">时间线
                <select v-model="timelineId" class="amf-input" :disabled="linesLoading">
                  <option value="">{{ lines.length ? `第一条（${lines[0]!.name}）` : '自动新建一条「约战记录」' }}</option>
                  <option v-for="l in lines" :key="l.id" :value="l.id">{{ l.name }}</option>
                </select>
              </label>
              <label class="amf-sub">事件标题
                <input v-model="eventTitle" class="amf-input" maxlength="200" :placeholder="defaultTitle" />
              </label>
              <div class="amf-row">
                <label class="amf-sub grow"><span>哪一年 <small>可选</small></span>
                  <input v-model="eventYear" class="amf-input" type="number" step="1" placeholder="世界历法里的年份" />
                </label>
                <label class="amf-sub grow"><span>时间说明 <small>可选</small></span>
                  <input v-model="eventDateText" class="amf-input" maxlength="100" placeholder="比如：第三纪元末" />
                </label>
              </div>
              <p class="amf-tip">事件内容会自动写上：结果、战后状态、说明、视频链接和约战页链接。写进去以后可以在编年史里继续改，也能在「修改记录」里撤销。</p>
            </template>
          </div>
        </template>
      </div>

      <p v-if="error" class="amf-error" role="alert">{{ error }}</p>
    </form>
    <template #footer>
      <button type="button" class="btn-line" :disabled="saving" @click="close(false)">取消</button>
      <button type="button" class="btn-line primary" :disabled="saving || !canSave" @click="save">{{ saving ? '保存中…' : '保存' }}</button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import VideoLinkCard from '@/components/VideoLinkCard.vue'
import {
  battleAftermathApi, statusTone, STATUS_PRESETS, MAX_STATUS_TAG,
  type AftermathRecord, type AftermathSiteWork, type BattleAftermathData, type BattleAftermathItem, type MyVideoWork,
} from '@/api/battleAftermath'
import { chronicleApi } from '@/api/chronicle'
import type { ChronicleTimeline } from '@/utils/chronicle'
import { ocWorldApi, type OcWorldLink } from '../../OCs/ocWorld'
import { getErrorMessage } from '@/utils/apiError'
import { safeWebLink, siteWorkIdOf, isBilibiliLink, fetchBilibiliMeta } from '@/utils/videoLink'

const props = defineProps<{
  open: boolean
  battleId: string
  item: BattleAftermathItem
  defaultVideo: BattleAftermathData['myDefaultVideo']
  /** 编年史事件的默认标题（「约战《xxx》：A vs B」） */
  defaultTitle: string
}>()
const emit = defineEmits<{ (e: 'update:open', v: boolean): void; (e: 'saved', record: AftermathRecord): void }>()
const router = useRouter()

const MAX_NOTE = 500
const presets: readonly string[] = STATUS_PRESETS

// ---------- 表单 ----------
const tag = ref('')
const custom = ref(false)
const note = ref('')
const videoUrl = ref('')
const videoTitle = ref('')
const videoCover = ref('')
const siteWork = ref<AftermathSiteWork | null>(null)
const toChronicle = ref(false)
const projectId = ref('')
const timelineId = ref('')
const eventTitle = ref('')
const eventYear = ref<number | string>('')
const eventDateText = ref('')
const saving = ref(false)
const error = ref('')
const customInput = ref<HTMLInputElement | null>(null)

const defaultPending = computed(() => Boolean(props.defaultVideo?.pending))
const safeUrl = computed(() => safeWebLink(videoUrl.value))
const alreadyWritten = computed(() => Boolean(props.item.record?.chronicleEventId))
const canSave = computed(() => tag.value.trim().length > 0 && tag.value.trim().length <= MAX_STATUS_TAG && (!videoUrl.value.trim() || Boolean(safeUrl.value)))

/** 每次打开都按现在的记录重新填（没有记录：视频默认用自己交的作品） */
function reset() {
  const r = props.item.record
  tag.value = r?.statusTag ?? ''
  custom.value = Boolean(r && !presets.includes(r.statusTag))
  note.value = r?.statusNote ?? ''
  if (r) {
    videoUrl.value = r.videoUrl ?? ''
    videoTitle.value = r.videoTitle ?? ''
    videoCover.value = r.videoCover ?? ''
    siteWork.value = r.siteWork
  } else {
    videoUrl.value = props.defaultVideo?.url ?? ''
    videoTitle.value = props.defaultVideo?.title ?? ''
    videoCover.value = ''
    siteWork.value = null
    void autoFetch()
  }
  toChronicle.value = false
  eventTitle.value = ''
  eventYear.value = ''
  eventDateText.value = ''
  error.value = ''
}

function pick(t: string) { tag.value = t; custom.value = false }
function startCustom() {
  if (!custom.value && presets.includes(tag.value)) tag.value = ''
  custom.value = true
  void nextTick(() => customInput.value?.focus())
}

// ---------- 视频 ----------
const fetching = ref(false)
let lastFetched = ''
/** B 站链接：用必吃榜的接口抓封面和标题（标题自己填过就不覆盖） */
async function autoFetch() {
  const url = safeUrl.value
  if (!url || !isBilibiliLink(url) || url === lastFetched) return
  lastFetched = url
  fetching.value = true
  try {
    const meta = await fetchBilibiliMeta(url)
    if (safeUrl.value !== url || !meta) return
    videoCover.value = meta.coverImage ?? ''
    if (!videoTitle.value.trim() && meta.title) videoTitle.value = meta.title
  } finally { fetching.value = false }
}
function onUrlBlur() {
  // 换了链接：之前的封面、站内作品信息作废（站内作品链接没变就保留）
  const url = safeUrl.value
  if (siteWork.value && (!url || siteWorkIdOf(url) !== siteWork.value.id)) siteWork.value = null
  if (url !== lastFetched) videoCover.value = ''
  void autoFetch()
}
function useDefault() {
  if (!props.defaultVideo) return
  videoUrl.value = props.defaultVideo.url
  videoTitle.value = props.defaultVideo.title
  videoCover.value = ''
  siteWork.value = null
  lastFetched = ''
  void autoFetch()
}
function clearVideo() { videoUrl.value = ''; videoTitle.value = ''; videoCover.value = ''; siteWork.value = null; lastFetched = '' }

const myVideosOpen = ref(false)
const myVideosLoading = ref(false)
const myVideos = ref<MyVideoWork[]>([])
let myVideosLoaded = false
async function toggleMyVideos() {
  myVideosOpen.value = !myVideosOpen.value
  if (!myVideosOpen.value || myVideosLoaded) return
  myVideosLoading.value = true
  try { myVideos.value = (await battleAftermathApi.myVideos()).items; myVideosLoaded = true }
  catch (e) { error.value = getErrorMessage(e, '读取你的视频作品失败') }
  finally { myVideosLoading.value = false }
}
/** 选站内视频：存作品页地址（别人点开能看到作品页），预览里直接播放 */
function useSiteWork(w: MyVideoWork) {
  videoUrl.value = siteLink(`/works/${w.id}`)
  videoTitle.value = w.title
  videoCover.value = safeWebLink(w.coverUrl) ?? ''
  siteWork.value = { id: w.id, title: w.title, mediaUrl: w.mediaUrl, coverUrl: w.coverUrl, mediaType: 'video' }
  lastFetched = videoUrl.value
  myVideosOpen.value = false
}

// ---------- 写进编年史 ----------
const worlds = ref<OcWorldLink[]>([])
const worldsLoading = ref(false)
const worldsError = ref('')
let worldsLoaded = false
const editableWorlds = computed(() => worlds.value.filter(w => w.exists && w.canEdit && w.name))
const linkedCount = computed(() => worlds.value.length)
watch(toChronicle, async on => {
  if (!on || worldsLoaded) return
  worldsLoading.value = true
  worldsError.value = ''
  try {
    worlds.value = (await ocWorldApi.ocInfo(props.item.ocId)).worlds
    worldsLoaded = true
    if (!projectId.value && editableWorlds.value.length) projectId.value = editableWorlds.value[0]!.projectId
  } catch (e) { worldsError.value = getErrorMessage(e, '读取 OC 关联的世界观失败') }
  finally { worldsLoading.value = false }
})

const lines = ref<ChronicleTimeline[]>([])
const linesLoading = ref(false)
watch(projectId, async pid => {
  lines.value = []
  timelineId.value = ''
  if (!pid) return
  linesLoading.value = true
  try {
    const data = await chronicleApi.get(pid)
    if (projectId.value === pid) lines.value = data.timelines
  } catch { /* 读不到时间线就用默认（第一条 / 自动新建） */ }
  finally { linesLoading.value = false }
})

/** 站内页面的完整地址（按路由的写法生成，哈希路由就是 https://xx.com/#/works/xxx）；去掉当前页面地址里多余的 ?参数 */
function siteLink(path: string) {
  const u = new URL(router.resolve(path).href, window.location.href)
  u.search = ''
  return u.href
}

// ---------- 保存 ----------
function close(v: boolean) { if (!v && !saving.value) emit('update:open', false) }

async function save() {
  if (saving.value || !canSave.value) return
  error.value = ''
  const writeChronicle = toChronicle.value && !alreadyWritten.value
  if (writeChronicle && !projectId.value) { error.value = '请选一个世界观，或者取消勾选「写进编年史」'; return }
  const yearText = String(eventYear.value ?? '').trim()
  const year = yearText === '' ? null : Number(yearText)
  if (writeChronicle && year !== null && !Number.isInteger(year)) { error.value = '年份要填整数'; return }
  saving.value = true
  try {
    const res = await battleAftermathApi.save(props.battleId, props.item.ocId, {
      statusTag: tag.value.trim(),
      statusNote: note.value.trim() || null,
      videoUrl: safeUrl.value,
      videoTitle: safeUrl.value ? videoTitle.value.trim() || null : null,
      videoCover: safeUrl.value ? videoCover.value || null : null,
      chronicle: writeChronicle ? {
        projectId: projectId.value,
        timelineId: timelineId.value || null,
        title: eventTitle.value.trim() || null,
        year,
        dateText: eventDateText.value.trim() || null,
        // 网站首页地址（哈希路由），事件内容里的「约战页面」「OC 页面」链接用
        linkBase: siteLink('/'),
      } : null,
    })
    if (res.warning) ElMessage.warning(res.warning)
    else ElMessage.success(res.message)
    emit('saved', res.record)
    emit('update:open', false)
  } catch (e) {
    error.value = getErrorMessage(e, '保存失败，请稍后再试')
  } finally {
    saving.value = false
  }
}
// 放在最后：reset 用到上面声明的变量（lastFetched 等），要等它们都声明好了再立刻执行
watch(() => props.open, v => { if (v) reset() }, { immediate: true })
</script>

<style scoped>
.amf { display: grid; gap: 16px; }
.amf-field { display: grid; gap: 8px; margin: 0; padding: 0; border: 0; min-width: 0; }
.amf-field legend, .amf-label { padding: 0; font-size: 13px; font-weight: 600; color: var(--text-primary); }
.amf-field legend { margin-bottom: 8px; }
.amf-field small, .amf-sub small { font-weight: 400; color: var(--text-secondary); }
.amf-tags { display: flex; flex-wrap: wrap; gap: 6px; }
.amf-tag { padding: 4px 12px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.amf-tag:hover { border-color: var(--accent-color); }
.amf-tag.on { border-color: var(--accent-color); background: var(--accent-soft); color: var(--accent-color); font-weight: 600; }
.amf-tag.on.tone-ok { border-color: var(--success, #3a8f5c); background: color-mix(in srgb, var(--success, #3a8f5c) 14%, transparent); color: var(--success, #3a8f5c); }
.amf-tag.on.tone-warn { border-color: var(--warning, #c08a1e); background: color-mix(in srgb, var(--warning, #c08a1e) 14%, transparent); color: var(--warning, #c08a1e); }
.amf-tag.on.tone-bad { border-color: var(--danger, #c0392b); background: color-mix(in srgb, var(--danger, #c0392b) 14%, transparent); color: var(--danger, #c0392b); }
.amf-tag.on.tone-dead { border-color: var(--text-primary); background: var(--text-primary); color: var(--paper-card); }
.amf-input { width: 100%; box-sizing: border-box; padding: 6px 10px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; }
.amf-input:focus { outline: none; border-color: var(--accent-color); }
textarea.amf-input { resize: vertical; line-height: 1.6; }
.amf-count { justify-self: end; margin-top: -4px; }
.amf-row { display: flex; flex-wrap: wrap; gap: 8px; align-items: flex-end; }
.grow { flex: 1 1 200px; min-width: 0; }
.amf-quick { display: flex; flex-wrap: wrap; gap: 4px 16px; }
.amf-link { border: 0; padding: 0; background: none; color: var(--accent-color); font: inherit; font-size: 12px; cursor: pointer; }
.amf-tip { margin: 0; font-size: 12px; line-height: 1.7; color: var(--text-secondary); }
.amf-tip a { color: var(--accent-color); }
.amf-error { margin: 0; font-size: 12px; color: var(--danger, #c0392b); }
.amf-videos { display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 6px; max-height: 220px; overflow-y: auto; padding: 8px; border: 1px dashed var(--border-line); background: var(--bg-sub); }
.amf-videos .amf-tip { grid-column: 1 / -1; }
.amf-video { display: grid; gap: 4px; padding: 4px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 12px; text-align: left; cursor: pointer; }
.amf-video.on, .amf-video:hover { border-color: var(--accent-color); }
.amf-video img, .amf-video-ph { width: 100%; aspect-ratio: 16 / 9; object-fit: cover; display: grid; place-items: center; background: var(--accent-soft); color: var(--accent-color); }
.amf-video span:last-child { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.amf-chron { padding-top: 12px; border-top: 1px solid var(--border-line); }
.amf-check { display: flex; align-items: center; gap: 6px; font-size: 13px; color: var(--text-primary); cursor: pointer; }
.amf-chron-box { display: grid; gap: 10px; padding: 10px 12px; border: 1px dashed var(--border-line); background: var(--bg-sub); }
.amf-sub { display: grid; gap: 4px; font-size: 12px; color: var(--text-secondary); }
.btn-line.primary { border-color: var(--accent-color); color: var(--accent-color); }
</style>
