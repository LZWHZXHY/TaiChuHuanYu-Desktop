<!-- ProfileDecorSettings.vue：个人主页装扮（别人访问你的主页时看到）。顶部横幅 + 整页背景分开设置。
     官方素材所有人可选（交易行在卖的要先兑换，可以先在这里预览）；自己上传图片 / 视频是赞助特权。 -->
<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useUserStore } from '@/stores/user'
import { useCos } from '../../composables/useCos'
import { apiMessage, profileDecorApi, type BgFocus, type BgMedia, type ProfileDecorView, type ProfilePart, type ProfilePreset, type ProfileSlot } from '@/api/appearance'
import { IMAGE_ACCEPT, MediaError, VIDEO_ACCEPT, uploadImage, uploadVideo } from '@/utils/backgroundMedia'

const user = useUserStore()
const { uploadFile, progress } = useCos()

const SLOTS: { id: ProfileSlot; name: string; desc: string }[] = [
  { id: 'banner', name: '主页横幅', desc: '主页最上面的一张宽图，头像和名字压在上面。建议 3:1 左右的横图。' },
  { id: 'page', name: '整页背景', desc: '访客打开你的主页时，整个页面换成这张背景（只在你的主页生效）。' },
]
const FOCUSES: { id: BgFocus; label: string }[] = [
  { id: 'center', label: '居中' }, { id: 'top', label: '靠上' }, { id: 'bottom', label: '靠下' }, { id: 'left', label: '靠左' }, { id: 'right', label: '靠右' },
]
const FOCUS_POS: Record<BgFocus, string> = { center: 'center', top: 'center top', bottom: 'center bottom', left: 'left center', right: 'right center' }

const emptyPart = (): ProfilePart => ({ source: 'default', presetId: null, custom: null, focus: 'center', veil: 45, blur: 0 })
const view = ref<ProfileDecorView | null>(null)
const presets = ref<ProfilePreset[]>([])
const draft = ref<Record<ProfileSlot, ProfilePart>>({ banner: emptyPart(), page: emptyPart() })
const loading = ref(true), loadError = ref('')
const busy = ref(false)
const stage = ref<'' | 'convert' | 'upload'>('')
const uploadingSlot = ref<ProfileSlot | null>(null)
const message = ref(''), error = ref('')
const inputs: Record<ProfileSlot, HTMLInputElement | null> = { banner: null, page: null }
const videoInputs: Record<ProfileSlot, HTMLInputElement | null> = { banner: null, page: null }

const canCustom = computed(() => !!view.value?.canCustom)
const clone = (p: ProfilePart): ProfilePart => ({ ...p, custom: p.custom ? { ...p.custom } : null })
const dirty = computed(() => !!view.value && JSON.stringify(draft.value) !== JSON.stringify({ banner: view.value.banner, page: view.value.page }))
const presetsOf = (slot: ProfileSlot) => presets.value.filter(p => p.slot === slot)

function say(text: string, isError = false) { message.value = isError ? '' : text; error.value = isError ? text : '' }

/** 交易行在卖、还没兑换的（自己原本就在用的不算） */
function needsBuy(slot: ProfileSlot, p: ProfilePreset) {
  const saved = view.value?.[slot]
  return !!p.locked && !p.owned && !(saved?.source === 'preset' && saved.presetId === p.id)
}
const blocked = computed(() => SLOTS.filter(s => {
  const d = draft.value[s.id]
  const p = d.source === 'preset' ? presets.value.find(x => x.id === d.presetId) : null
  return !!p && needsBuy(s.id, p)
}).map(s => s.name))

/** 这一项实际显示的素材 */
function mediaOf(slot: ProfileSlot): BgMedia | null {
  const d = draft.value[slot]
  if (d.source === 'preset') { const p = presets.value.find(x => x.id === d.presetId); return p ? { url: p.url, kind: p.kind, poster: p.poster ?? null } : null }
  if (d.source === 'custom') return canCustom.value ? d.custom : null
  return null
}
const thumbOf = (m: BgMedia | null | undefined) => {
  const url = m ? (m.kind === 'video' ? m.poster : m.url) : null
  return url ? { backgroundImage: `url(${JSON.stringify(url)})` } : undefined
}
const previewStyle = (slot: ProfileSlot) => {
  const m = mediaOf(slot)
  const s = thumbOf(m)
  return s ? { ...s, backgroundPosition: FOCUS_POS[draft.value[slot].focus], filter: slot === 'page' && draft.value.page.blur ? `blur(${Math.round(draft.value.page.blur / 3)}px)` : undefined } : undefined
}

function choose(slot: ProfileSlot, source: ProfilePart['source'], presetId: number | null = null) {
  if (source === 'custom') {
    if (!canCustom.value) return say('自定义主页素材是赞助用户的特权，可以先选官方素材。', true)
    if (!draft.value[slot].custom) return say('先在下面上传一张图片或一段视频。', true)
  }
  draft.value[slot] = { ...draft.value[slot], source, presetId: source === 'preset' ? presetId : null }
  say('')
}

async function onPick(event: Event, slot: ProfileSlot, type: 'image' | 'video') {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  say('')
  uploadingSlot.value = slot
  try {
    const upload = (f: File) => uploadFile(f, `profile/${slot}`)
    const onStage = (s: 'convert' | 'upload') => { stage.value = s }
    const media = type === 'image' ? await uploadImage(file, upload, onStage) : await uploadVideo(file, upload, onStage)
    draft.value[slot] = { ...draft.value[slot], source: 'custom', presetId: null, custom: { url: media.url, kind: media.kind, poster: media.poster } }
    say(`${media.note}上传好了，满意的话点「保存」。`)
  } catch (e) {
    say(e instanceof MediaError ? e.message : '上传失败，请稍后再试', true)
  } finally { stage.value = ''; uploadingSlot.value = null }
}

function apply(v: ProfileDecorView) {
  view.value = v
  draft.value = { banner: clone(v.banner), page: clone(v.page) }
}

async function load() {
  loading.value = true; loadError.value = ''
  try {
    const [v, list] = await Promise.all([profileDecorApi.get(), profileDecorApi.presets()])
    presets.value = Array.isArray(list) ? list : []
    apply(v)
  } catch (e) { loadError.value = apiMessage(e, '读取主页装扮失败') } finally { loading.value = false }
}

const toSave = (p: ProfilePart) => ({ source: p.source, presetId: p.presetId, url: p.custom?.url ?? null, kind: p.custom?.kind ?? 'image' as const, poster: p.custom?.poster ?? null, focus: p.focus, veil: p.veil, blur: p.blur })
async function save() {
  busy.value = true
  try {
    apply(await profileDecorApi.save(toSave(draft.value.banner), toSave(draft.value.page)))
    say('主页装扮已保存，别人打开你的主页就能看到。')
  } catch (e) { say(apiMessage(e, '保存失败，请稍后再试'), true) } finally { busy.value = false }
}
function revert() { if (view.value) apply(view.value); say('') }

onMounted(load)
</script>

<template>
  <div class="pd-panel">
    <header class="pd-head">
      <div>
        <h3>主页装扮</h3>
        <p>别人访问你的个人主页时看到的横幅和背景。官方素材可以直接选，交易行在卖的要先兑换；自己上传是赞助特权。</p>
      </div>
      <span v-if="dirty" class="pd-badge">未保存</span>
    </header>

    <p v-if="loading" class="muted">读取中…</p>
    <p v-else-if="loadError" class="err" role="alert">{{ loadError }} <button type="button" class="ghost-btn" @click="load">重试</button></p>

    <template v-else-if="view">
      <!-- 效果预览：模拟别人看到的主页 -->
      <section class="pd-preview" aria-label="效果预览">
        <div class="pv-page" :style="previewStyle('page')"></div>
        <div class="pv-veil" :style="{ '--veil': mediaOf('page') ? `${draft.page.veil}%` : '100%' }"></div>
        <div class="pv-card">
          <div class="pv-banner" :style="previewStyle('banner')"><em v-if="!mediaOf('banner')">没有横幅</em></div>
          <div class="pv-who">
            <span class="pv-avatar"><img v-if="user.userInfo?.avatar" :src="user.userInfo.avatar" alt="" /><b v-else>{{ user.userInfo?.username?.[0] ?? '?' }}</b></span>
            <strong>{{ user.userInfo?.username }}</strong>
          </div>
          <i class="pv-line"></i><i class="pv-line short"></i>
        </div>
        <RouterLink v-if="user.userId" class="pv-open" :to="{ name: 'UserCenter', params: { id: user.userId }, query: { view: 'public' } }">打开我的公开主页 →</RouterLink>
      </section>

      <section v-for="slot in SLOTS" :key="slot.id" class="pd-section">
        <h4>{{ slot.name }}</h4>
        <p class="muted">{{ slot.desc }}</p>
        <div class="tiles" :class="{ wide: slot.id === 'banner' }" role="radiogroup" :aria-label="slot.name">
          <button type="button" role="radio" class="tile" :aria-checked="draft[slot.id].source === 'default'" :class="{ on: draft[slot.id].source === 'default' }" @click="choose(slot.id, 'default')">
            <span class="tile-thumb none"><i>不设置</i></span>
            <span class="tile-name">不设置</span>
          </button>
          <button v-for="p in presetsOf(slot.id)" :key="p.id" type="button" role="radio" class="tile" :aria-checked="draft[slot.id].source === 'preset' && draft[slot.id].presetId === p.id" :class="{ on: draft[slot.id].source === 'preset' && draft[slot.id].presetId === p.id }" @click="choose(slot.id, 'preset', p.id)">
            <span class="tile-thumb" :style="thumbOf(p)"><em v-if="p.kind === 'video'" class="video-tag">▶ 动态</em><b v-if="needsBuy(slot.id, p)" class="shop-tag">🔒 交易行</b><b v-else-if="p.locked && p.owned" class="shop-tag owned">已拥有</b></span>
            <span class="tile-name">{{ p.title }}<small v-if="p.author"> · {{ p.author }}</small></span>
          </button>
          <button type="button" role="radio" class="tile" :aria-checked="draft[slot.id].source === 'custom'" :class="{ on: draft[slot.id].source === 'custom', locked: !canCustom }" @click="choose(slot.id, 'custom')">
            <span class="tile-thumb" :style="thumbOf(draft[slot.id].custom)">
              <i v-if="!draft[slot.id].custom">{{ canCustom ? '上传后可选' : '🔒 赞助特权' }}</i>
              <i v-else-if="!canCustom" class="lock-cover">🔒</i>
            </span>
            <span class="tile-name">我的自定义</span>
          </button>
        </div>
        <p v-if="!presetsOf(slot.id).length" class="muted">官方{{ slot.name }}还在准备中。</p>

        <div v-if="canCustom" class="upload-row">
          <input :ref="el => { inputs[slot.id] = el as HTMLInputElement | null }" type="file" :accept="IMAGE_ACCEPT" hidden @change="onPick($event, slot.id, 'image')" />
          <input :ref="el => { videoInputs[slot.id] = el as HTMLInputElement | null }" type="file" :accept="VIDEO_ACCEPT" hidden @change="onPick($event, slot.id, 'video')" />
          <button type="button" class="ghost-btn" :disabled="!!stage" @click="inputs[slot.id]?.click()">上传图片</button>
          <button type="button" class="ghost-btn" :disabled="!!stage" @click="videoInputs[slot.id]?.click()">上传视频</button>
          <span v-if="stage && uploadingSlot === slot.id" class="stage">{{ stage === 'convert' ? '正在处理…' : `上传中 ${progress}%` }}</span>
        </div>

        <div v-if="draft[slot.id].source !== 'default'" class="controls">
          <label>显示位置
            <select v-model="draft[slot.id].focus"><option v-for="f in FOCUSES" :key="f.id" :value="f.id">{{ f.label }}</option></select>
          </label>
          <template v-if="slot.id === 'page'">
            <label>遮罩 {{ draft.page.veil }}%<small>越高背景越淡、文字越清楚</small><input v-model.number="draft.page.veil" type="range" min="0" max="90" step="5" /></label>
            <label>模糊 {{ draft.page.blur }}px<input v-model.number="draft.page.blur" type="range" min="0" max="20" /></label>
          </template>
        </div>
      </section>

      <p v-if="blocked.length" class="buy-hint">
        {{ blocked.join('、') }}选的是交易行的素材，预览里能先看效果；兑换后才能保存。
        <RouterLink :to="{ name: '交易行' }">去交易行兑换 →</RouterLink>
      </p>
      <p v-if="!canCustom" class="muted perk-note">想用自己的图片或视频？赞助后开通特权（在爱赞助私信告诉我们你的网站用户名），就能上传自定义横幅和背景。</p>
      <p v-if="message" class="ok" role="status">{{ message }}</p>
      <p v-if="error" class="err" role="alert">{{ error }}</p>

      <footer class="pd-actions">
        <button type="button" class="ghost-btn" :disabled="!dirty || busy" @click="revert">撤销修改</button>
        <button type="button" class="save-btn" :disabled="!dirty || busy || !!stage || blocked.length > 0" @click="save">{{ busy ? '保存中…' : '保存' }}</button>
      </footer>
    </template>
  </div>
</template>

<style scoped>
.pd-panel { display: grid; gap: 18px; }
.pd-head { display: flex; justify-content: space-between; gap: 12px; align-items: flex-start; }
.pd-head h3 { margin: 0 0 4px; }
.pd-head p, .muted { margin: 0; color: var(--text-secondary); font-size: .85rem; line-height: 1.6; }
.pd-badge { flex-shrink: 0; padding: 2px 8px; border: 1px solid var(--accent-color); color: var(--accent-color); font-size: 12px; }
.err { margin: 0; color: var(--danger); font-size: .85rem; }
.ok { margin: 0; color: var(--success); font-size: .85rem; }

.pd-preview { position: relative; display: grid; place-items: center; min-height: 260px; padding: 28px 16px 40px; overflow: hidden; border: 1px solid var(--border-line); background: var(--bg-main); }
.pv-page { position: absolute; inset: -12px; background: var(--bg-sub) center / cover no-repeat; }
.pv-veil { position: absolute; inset: 0; background: color-mix(in srgb, var(--bg-main) var(--veil, 100%), transparent); }
.pv-card { position: relative; width: min(420px, 100%); overflow: hidden; border: 1px solid var(--border-line); background: var(--paper-card); }
.pv-banner { position: relative; aspect-ratio: 3 / 1; background: linear-gradient(135deg, color-mix(in srgb, var(--accent-color) 20%, transparent), transparent) center / cover no-repeat; }
.pv-banner em { position: absolute; right: 8px; bottom: 6px; color: var(--text-secondary); font-size: 11px; font-style: normal; }
.pv-who { display: flex; align-items: flex-end; gap: 10px; margin-top: -26px; padding: 0 14px; }
.pv-avatar { position: relative; display: grid; place-items: center; width: 56px; height: 56px; overflow: hidden; border: 3px solid var(--paper-card); background: linear-gradient(135deg, var(--accent-color), var(--signal-mint, #2dd4bf)); color: #fff; }
.pv-avatar img { width: 100%; height: 100%; object-fit: cover; }
.pv-who strong { padding-bottom: 4px; font-size: 15px; }
.pv-line { display: block; height: 8px; margin: 12px 14px 0; background: var(--bg-sub); }
.pv-line.short { width: 40%; margin-bottom: 16px; }
.pv-open { position: absolute; right: 10px; bottom: 8px; color: var(--accent-color); font-size: 12px; }

.pd-section { display: grid; gap: 10px; padding-top: 14px; border-top: 1px solid var(--border-line); }
.pd-section h4 { margin: 0; }
.tiles { display: grid; grid-template-columns: repeat(auto-fill, minmax(140px, 1fr)); gap: 10px; }
.tiles.wide { grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); }
.tile { display: grid; gap: 6px; padding: 8px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); text-align: left; font: inherit; cursor: pointer; }
.tile.on { border-color: var(--accent-color); box-shadow: inset 0 0 0 1px var(--accent-color); }
.tile-thumb { position: relative; display: block; aspect-ratio: 16 / 9; overflow: hidden; border: 1px solid var(--border-line); background: var(--bg-sub) center / cover no-repeat; }
.tiles.wide .tile-thumb { aspect-ratio: 3 / 1; }
.tile-thumb.none { background: repeating-linear-gradient(135deg, var(--bg-sub) 0 8px, transparent 8px 16px); }
.tile-thumb > i { position: absolute; inset: 0; display: grid; place-items: center; color: var(--text-secondary); font-size: 12px; font-style: normal; }
.tile-thumb > .lock-cover { background: rgb(0 0 0 / 45%); color: #fff; font-size: 18px; }
.tile-name { overflow: hidden; font-size: .85rem; text-overflow: ellipsis; white-space: nowrap; }
.tile-name small { color: var(--text-secondary); font-size: 11px; }
.tile.locked .tile-name { color: var(--text-secondary); }
.video-tag { position: absolute; right: 4px; bottom: 4px; padding: 0 5px; background: rgb(0 0 0 / 60%); color: #fff; font-size: 10px; font-style: normal; }
.shop-tag { position: absolute; left: 6px; top: 6px; padding: 1px 6px; background: rgb(0 0 0 / 62%); color: #fff; font-size: 11px; font-weight: 600; }
.shop-tag.owned { background: var(--accent-color); color: var(--on-accent, #fff); }

.upload-row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.stage { color: var(--accent-color); font-size: 12px; }
.controls { display: grid; grid-template-columns: repeat(auto-fill, minmax(180px, 1fr)); gap: 12px; }
.controls label { display: grid; gap: 4px; font-size: 13px; }
.controls label small { color: var(--text-secondary); font-size: 11px; }
.controls select { padding: 6px 8px; border: 1px solid var(--border-line); background: var(--bg-main); color: var(--text-primary); font: inherit; }
.buy-hint { margin: 0; padding: 8px 12px; border-left: 3px solid var(--accent-color); background: var(--bg-sub); font-size: .85rem; }
.buy-hint a { margin-left: 6px; color: var(--accent-color); font-weight: 600; }
.perk-note { padding: 8px 12px; border: 1px dashed var(--border-line); }
.pd-actions { display: flex; justify-content: flex-end; gap: 10px; }
.ghost-btn { padding: 7px 14px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.save-btn { padding: 8px 22px; border: 0; background: var(--accent-color); color: var(--on-accent); font: inherit; font-weight: 600; cursor: pointer; }
.ghost-btn:disabled, .save-btn:disabled { opacity: .5; cursor: not-allowed; }
</style>
