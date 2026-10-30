<!-- BackgroundSettings.vue：网站背景（只有自己能看到）。官方背景所有人可选（交易行在卖的要先兑换，可以先预览），自己上传图片 / 视频是赞助特权。调整时整站实时预览，保存后才生效。 -->
<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useCos } from '../../composables/useCos'
import { DEFAULT_BACKGROUND, useSiteBackground, type BgFocus, type BgMedia, type SiteBackground } from '@/composables/useSiteBackground'
import { appearanceApi, apiMessage } from '@/api/appearance'
import { IMAGE_ACCEPT, MediaError, VIDEO_ACCEPT, uploadImage, uploadVideo } from '@/utils/backgroundMedia'

const bg = useSiteBackground()
const { uploadFile, isUploading, progress } = useCos()

const clone = (v: SiteBackground): SiteBackground => ({ ...v, custom: v.custom ? { ...v.custom } : null })
const draft = ref<SiteBackground>(clone(bg.state.saved))
const imageInput = ref<HTMLInputElement | null>(null)
const videoInput = ref<HTMLInputElement | null>(null)
const busy = ref(false)
const stage = ref<'' | 'convert' | 'upload'>('')
const message = ref(''), error = ref('')

const FOCUSES: { id: BgFocus; label: string }[] = [
  { id: 'center', label: '居中' }, { id: 'top', label: '靠上' }, { id: 'bottom', label: '靠下' }, { id: 'left', label: '靠左' }, { id: 'right', label: '靠右' },
]

const presets = computed(() => bg.state.presets)
// 交易行在卖、还没兑换的背景（自己原本就在用的不算）
const needsBuy = (p: { id: number; locked?: boolean; owned?: boolean }) =>
  !!p.locked && !p.owned && !(bg.state.saved.source === 'preset' && bg.state.saved.presetId === p.id)
const draftNeedsBuy = computed(() => {
  const d = draft.value
  if (d.source !== 'preset') return false
  const p = presets.value.find(x => x.id === d.presetId)
  return !!p && needsBuy(p)
})
const sponsor = computed(() => bg.state.sponsor)
const canCustom = bg.canCustom
const sameAsSaved = (v: SiteBackground) => JSON.stringify(v) === JSON.stringify(bg.state.saved)
const dirty = computed(() => !sameAsSaved(draft.value))

// 服务器上的设置晚于本组件加载时，用服务器的值作为起点
watch(() => bg.state.saved, (saved, old) => { if (JSON.stringify(draft.value) === JSON.stringify(old)) draft.value = clone(saved) })
// 调整时整站实时预览；离开页面未保存则自动还原
watch(draft, value => bg.setPreview(sameAsSaved(value) ? null : value), { deep: true })
onMounted(() => { void bg.loadPresets(true) })
onBeforeUnmount(() => bg.setPreview(null))

const fmtDate = (iso: string | null | undefined) => iso ? new Date(iso).toLocaleString('zh-CN', { year: 'numeric', month: 'numeric', day: 'numeric', hour: '2-digit', minute: '2-digit' }) : ''
const daysLeft = computed(() => {
  const until = sponsor.value?.until
  return until ? Math.max(0, Math.ceil((new Date(until).getTime() - Date.now()) / 86400000)) : 0
})

const thumbOf = (m: BgMedia | null | undefined) => {
  const url = m ? (m.kind === 'video' ? m.poster : m.url) : null
  return url ? { backgroundImage: `url(${JSON.stringify(url)})` } : undefined
}

function say(text: string, isError = false) {
  message.value = isError ? '' : text
  error.value = isError ? text : ''
}

function choose(source: SiteBackground['source'], presetId: number | null = null) {
  if (source === 'custom') {
    if (!canCustom.value) return say('自定义背景是赞助用户的特权，下面有开通方法。', true)
    if (!draft.value.custom) return say('先在下面上传一张图片或一段视频。', true)
  }
  draft.value = { ...draft.value, source, presetId: source === 'preset' ? presetId : null }
  say('')
}

async function onPick(event: Event, type: 'image' | 'video') {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  say('')
  try {
    const upload = (f: File) => uploadFile(f, 'backgrounds')
    const onStage = (s: 'convert' | 'upload') => { stage.value = s }
    const media = type === 'image' ? await uploadImage(file, upload, onStage) : await uploadVideo(file, upload, onStage)
    draft.value = { ...draft.value, source: 'custom', presetId: null, custom: { url: media.url, kind: media.kind, poster: media.poster } }
    say(`${media.note}${type === 'image' ? '图片已上传，' : ''}现在看到的就是效果。满意的话点「保存」。`)
  } catch (e) {
    say(e instanceof MediaError ? e.message : '上传失败，请稍后再试', true)
  } finally { stage.value = '' }
}

async function save() {
  busy.value = true
  try {
    draft.value = clone(await bg.save(draft.value))
    say(draft.value.source === 'default' ? '已切回默认背景。' : '背景已保存，换设备登录也会保留。')
  } catch (e) {
    say(apiMessage(e, '保存失败，请稍后再试'), true)
  } finally { busy.value = false }
}
function revert() { draft.value = clone(bg.state.saved); say('') }
async function resetAll() {
  busy.value = true
  try { await bg.reset(); draft.value = clone(DEFAULT_BACKGROUND); say('已恢复默认背景。') }
  catch { say('操作失败，请稍后再试', true) } finally { busy.value = false }
}

// ===== 绑定爱发电，自动开通赞助特权 =====
const orderNo = ref('')
const binding = ref(false)
const bindMsg = ref(''), bindErr = ref('')
async function bind() {
  if (!orderNo.value.trim()) return
  binding.value = true; bindMsg.value = ''; bindErr.value = ''
  try {
    const r = await appearanceApi.bind(orderNo.value.trim())
    bg.setSponsor(r.sponsor)
    bindMsg.value = r.message
    orderNo.value = ''
  } catch (e) { bindErr.value = apiMessage(e, '绑定失败，请稍后再试') } finally { binding.value = false }
}
async function unbind() {
  binding.value = true; bindMsg.value = ''; bindErr.value = ''
  try { const r = await appearanceApi.unbind(); bg.setSponsor(r.sponsor); bindMsg.value = r.message }
  catch (e) { bindErr.value = apiMessage(e, '操作失败') } finally { binding.value = false }
}
</script>

<template>
  <div class="bg-panel">
    <header class="bg-head">
      <div>
        <h3>网站背景</h3>
        <p>只有你自己能看到。选好的背景会替换掉纯色底色，网格、粒子和线条特效都会保留。</p>
      </div>
      <span v-if="dirty" class="bg-badge">预览中 · 未保存</span>
    </header>

    <!-- 选择背景 -->
    <section class="bg-section">
      <h4>选择背景</h4>
      <div class="tiles" role="radiogroup" aria-label="网站背景">
        <button type="button" role="radio" class="tile" :aria-checked="draft.source === 'default'" :class="{ on: draft.source === 'default' }" @click="choose('default')">
          <span class="tile-thumb default-thumb" aria-hidden="true"></span>
          <span class="tile-name">默认坐标纸</span>
        </button>
        <button v-for="p in presets" :key="p.id" type="button" role="radio" class="tile" :aria-checked="draft.source === 'preset' && draft.presetId === p.id" :class="{ on: draft.source === 'preset' && draft.presetId === p.id }" @click="choose('preset', p.id)">
          <span class="tile-thumb" :style="thumbOf(p)" aria-hidden="true"><em v-if="p.kind === 'video'" class="video-tag">▶ 动态</em><b v-if="needsBuy(p)" class="shop-tag">🔒 交易行</b><b v-else-if="p.locked && p.owned" class="shop-tag owned">已兑换</b></span>
          <span class="tile-name">{{ p.title }}<small v-if="p.author" class="tile-author"> · {{ p.author }}</small></span>
        </button>
        <button type="button" role="radio" class="tile custom-tile" :aria-checked="draft.source === 'custom'" :class="{ on: draft.source === 'custom', locked: !canCustom }" @click="choose('custom')">
          <span class="tile-thumb" :style="thumbOf(draft.custom)" aria-hidden="true">
            <i v-if="!draft.custom">{{ canCustom ? '上传后可选' : '🔒 赞助特权' }}</i>
            <i v-else-if="!canCustom" class="lock-cover">🔒</i>
            <em v-if="draft.custom?.kind === 'video'" class="video-tag">▶ 视频</em>
          </span>
          <span class="tile-name">我的自定义</span>
        </button>
      </div>
      <p v-if="!presets.length" class="muted">官方背景还在准备中，敬请期待。</p>
      <p v-if="draftNeedsBuy" class="buy-hint">
        这是交易行的背景，现在看到的是预览效果；用经验或土豆兑换后才能保存。
        <RouterLink :to="{ name: '交易行' }">去交易行兑换 →</RouterLink>
      </p>
    </section>

    <!-- 自定义背景：赞助特权 -->
    <section class="perk-box" :class="{ unlocked: canCustom }">
      <div class="perk-head">
        <h4>自定义背景 <span class="perk-tag">赞助特权</span></h4>
        <span v-if="canCustom && sponsor" class="perk-state">
          <b>{{ sponsor.tier }}</b> · 有效期到 {{ fmtDate(sponsor.until) }}<template v-if="daysLeft <= 7">（还剩 {{ daysLeft }} 天）</template>
        </span>
      </div>

      <template v-if="canCustom">
        <p class="perk-desc">感谢支持！你可以上传自己的图片或视频当背景。</p>
        <input ref="imageInput" type="file" :accept="IMAGE_ACCEPT" hidden @change="onPick($event, 'image')" />
        <input ref="videoInput" type="file" :accept="VIDEO_ACCEPT" hidden @change="onPick($event, 'video')" />
        <div class="upload-btns">
          <button type="button" class="ghost-btn" :disabled="isUploading || !!stage" @click="imageInput?.click()">上传图片</button>
          <button type="button" class="ghost-btn" :disabled="isUploading || !!stage" @click="videoInput?.click()">上传视频</button>
          <span v-if="stage" class="upload-state" role="status">{{ stage === 'convert' ? '正在处理…' : `上传中 ${progress}%` }}</span>
        </div>
        <ul class="upload-tips">
          <li><b>图片</b>：JPG / PNG / WebP / GIF / AVIF / BMP，原图 80MB 以内。上传前自动转成 WebP 并缩到 2560 像素以内；10MB 以内的 GIF 原样上传，保留动画。</li>
          <li><b>视频</b>：MP4（H.264）或 WebM，30MB 以内，静音循环播放。建议 1080p、10–30 秒、首尾能接上的片段。</li>
          <li>建议用 16:9 横向素材。</li>
        </ul>
      </template>

      <template v-else>
        <p class="perk-desc">
          赞助任意档位即可解锁，上传自己的图片或视频当网站背景。每次赞助有效 {{ sponsor?.days ?? 30 }} 天，连续赞助自动顺延。
          <template v-if="sponsor?.until">你的特权已于 {{ fmtDate(sponsor.until) }} 到期。</template>
        </p>
        <p v-if="bg.state.customLocked" class="perk-note">你之前设置的自定义背景还保存着，续费后会自动恢复。</p>
        <div class="perk-actions">
          <RouterLink to="/sponsors" class="primary-link">去赞助 →</RouterLink>
        </div>
        <p class="perk-note">用<b>爱赞助</b>赞助的：赞助后在爱赞助私信里告诉我们你的网站用户名，管理员核对后会帮你开通。</p>
        <div v-if="sponsor?.afdianBound" class="bind-state">
          已绑定爱发电账号：在爱发电赞助后几分钟内会自动开通。
          <button type="button" class="link-btn" :disabled="binding" @click="unbind">解除绑定</button>
        </div>
        <form v-else class="bind-form" @submit.prevent="bind">
          <label for="afdian-order">在爱发电赞助过？填一笔订单号自动开通</label>
          <div class="bind-row">
            <input id="afdian-order" v-model="orderNo" maxlength="64" placeholder="爱发电「我的订单」里的订单号" autocomplete="off" />
            <button type="submit" class="ghost-btn" :disabled="binding || !orderNo.trim()">{{ binding ? '核对中…' : '绑定并开通' }}</button>
          </div>
          <small>只需要绑定一次，以后每次赞助都会自动顺延。也可以联系管理员手动开通。</small>
        </form>
      </template>
      <p v-if="bindMsg" class="bg-msg" role="status">{{ bindMsg }}</p>
      <p v-if="bindErr" class="bg-err" role="alert">{{ bindErr }}</p>
    </section>

    <!-- 显示参数 -->
    <!-- 导航栏：不管用不用背景图都能调 -->
    <section class="bg-section">
      <h4>左侧导航栏</h4>
      <label class="ctl">
        <span class="ctl-name">不透明度 <b>{{ draft.nav }}%</b></span>
        <input v-model.number="draft.nav" type="range" min="30" max="100" step="5" />
        <small>越低越透明，能透过导航栏看到后面的背景；太低时文字可能不好认。100% 是完全不透明。</small>
      </label>
    </section>

    <fieldset class="bg-controls" :disabled="draft.source === 'default'">
      <legend class="sr-only">显示参数</legend>
      <label class="ctl">
        <span class="ctl-name">遮罩强度 <b>{{ draft.veil }}%</b></span>
        <input v-model.number="draft.veil" type="range" min="0" max="90" step="5" />
        <small>越高背景越淡、文字越清楚。夜间模式下遮罩会自动变成深色。</small>
      </label>
      <label class="ctl">
        <span class="ctl-name">模糊 <b>{{ draft.blur ? draft.blur + 'px' : '不模糊' }}</b></span>
        <input v-model.number="draft.blur" type="range" min="0" max="20" step="1" />
        <small>细节多的背景稍微模糊一下会更不抢眼。动态背景开模糊更耗电，配置低的电脑建议保持 0。</small>
      </label>
      <div class="ctl">
        <span class="ctl-name">显示位置</span>
        <div class="focus-row" role="radiogroup" aria-label="背景对齐">
          <button v-for="f in FOCUSES" :key="f.id" type="button" role="radio" :aria-checked="draft.focus === f.id" :class="{ on: draft.focus === f.id }" @click="draft.focus = f.id">{{ f.label }}</button>
        </div>
        <small>屏幕比例和背景不一致时，决定保留哪一部分。</small>
      </div>
      <label class="ctl ctl-inline">
        <span class="ctl-name">保留坐标纸网格</span>
        <span class="ink-toggle"><input v-model="draft.grid" type="checkbox" /><span class="toggle-slider"></span></span>
      </label>
    </fieldset>

    <p v-if="message" class="bg-msg" role="status">{{ message }}</p>
    <p v-if="error" class="bg-err" role="alert">{{ error }}</p>

    <div class="bg-actions">
      <button type="button" class="link-btn" :disabled="busy || (bg.state.saved.source === 'default' && !bg.state.saved.custom)" @click="resetAll">恢复默认并清除我的上传</button>
      <span class="spacer"></span>
      <button type="button" class="ghost-btn" :disabled="!dirty || busy" @click="revert">撤销修改</button>
      <button type="button" class="save-btn" :disabled="!dirty || busy || isUploading || !!stage || draftNeedsBuy" :title="draftNeedsBuy ? '先在交易行兑换这个背景' : undefined" @click="save">{{ busy ? '保存中…' : '保存' }}</button>
    </div>
  </div>
</template>

<style scoped>
.bg-panel { display: grid; gap: 22px; padding: 24px; background: var(--paper-card); border: 1px solid var(--border-line); color: var(--text-primary); }
.bg-head { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; }
.bg-head h3 { margin: 0 0 6px; font-size: 1.05rem; }
.bg-head p, .muted { margin: 0; color: var(--text-secondary); font-size: .85rem; line-height: 1.6; }
.bg-badge { flex-shrink: 0; padding: 3px 8px; border: 1px solid var(--accent-color); color: var(--accent-color); font-size: 12px; }
.bg-section { display: grid; gap: 12px; }
h4 { margin: 0; font-size: .92rem; }

.tiles { display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 12px; }
.tile { display: grid; gap: 8px; padding: 8px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); text-align: left; font: inherit; cursor: pointer; }
.tile:hover { border-color: color-mix(in srgb, var(--accent-color) 50%, var(--border-line)); }
.tile.on { border-color: var(--accent-color); box-shadow: inset 0 0 0 1px var(--accent-color); }
.tile-thumb { position: relative; display: block; aspect-ratio: 16 / 9; overflow: hidden; border: 1px solid var(--border-line); background-color: var(--bg-sub); background-size: cover; background-position: center; }
.tile-thumb > i { position: absolute; inset: 0; display: grid; place-items: center; color: var(--text-secondary); font-size: 12px; font-style: normal; }
.tile-thumb > .lock-cover { background: rgb(0 0 0 / 45%); color: #fff; font-size: 18px; }
.shop-tag { position: absolute; left: 6px; top: 6px; padding: 1px 6px; background: rgb(0 0 0 / 62%); color: #fff; font-size: 11px; font-weight: 600; }
.shop-tag.owned { background: var(--accent-color); color: var(--on-accent, #fff); }
.buy-hint { margin: 10px 0 0; padding: 8px 12px; border-left: 3px solid var(--accent-color); background: var(--bg-sub); font-size: .85rem; }
.buy-hint a { margin-left: 6px; color: var(--accent-color); font-weight: 600; }
.tile-author { color: var(--text-secondary); font-size: 11px; }
.tile-name { font-size: .85rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.custom-tile.locked .tile-name { color: var(--text-secondary); }
.default-thumb { background-color: var(--bg-main); background-image: linear-gradient(var(--lab-grid-major) 1px, transparent 1px), linear-gradient(90deg, var(--lab-grid-major) 1px, transparent 1px), linear-gradient(var(--lab-grid-minor) 1px, transparent 1px), linear-gradient(90deg, var(--lab-grid-minor) 1px, transparent 1px); background-size: 16px 16px, 16px 16px, 4px 4px, 4px 4px; }
.video-tag { position: absolute; right: 4px; bottom: 4px; padding: 0 5px; background: rgb(0 0 0 / 60%); color: #fff; font-size: 10px; font-style: normal; }

.perk-box { display: grid; gap: 12px; padding: 16px; border: 1px dashed color-mix(in srgb, #d97706 60%, var(--border-line)); background: color-mix(in srgb, #f59e0b 6%, transparent); }
.perk-box.unlocked { border-style: solid; }
.perk-head { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: baseline; gap: 6px 12px; }
.perk-tag { margin-left: 6px; padding: 1px 6px; background: #f59e0b; color: #1c1206; font-size: 11px; font-weight: 600; vertical-align: 1px; }
.perk-state { font-size: .82rem; color: var(--text-secondary); }
.perk-state b { color: #b45309; }
.perk-desc, .perk-note { margin: 0; font-size: .85rem; line-height: 1.7; color: var(--text-secondary); }
.perk-note { color: var(--text-primary); }
.perk-actions { display: flex; gap: 10px; }
.primary-link { display: inline-block; padding: 7px 16px; background: #f59e0b; color: #1c1206; font-size: .88rem; font-weight: 600; text-decoration: none; }
.bind-form { display: grid; gap: 6px; }
.bind-form label { font-size: .85rem; font-weight: 600; }
.bind-row { display: flex; flex-wrap: wrap; gap: 8px; }
.bind-row input { flex: 1 1 240px; min-width: 0; padding: 7px 10px; border: 1px solid var(--border-line); background: var(--bg-main); color: var(--text-primary); font: inherit; font-size: .88rem; }
.bind-form small, .bind-state { color: var(--text-secondary); font-size: .8rem; }

.upload-btns { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; }
.upload-state { color: var(--accent-color); font-size: .82rem; font-variant-numeric: tabular-nums; }
.upload-tips { margin: 0; padding-left: 18px; display: grid; gap: 4px; color: var(--text-secondary); font-size: .78rem; line-height: 1.6; }
.upload-tips b { color: var(--text-primary); font-weight: 600; }

.bg-controls { display: grid; gap: 18px; margin: 0; padding: 16px 0 0; border: 0; border-top: 1px dashed var(--border-line); min-width: 0; }
.bg-controls:disabled { opacity: .5; }
.ctl { display: grid; gap: 8px; }
.ctl-name { font-size: .9rem; font-weight: 600; }
.ctl-name b { margin-left: 6px; color: var(--accent-color); font-variant-numeric: tabular-nums; }
.ctl small { color: var(--text-secondary); font-size: .78rem; }
.ctl input[type=range] { width: 100%; max-width: 420px; accent-color: var(--accent-color); }
.ctl-inline { display: flex; justify-content: space-between; align-items: center; max-width: 420px; }
.focus-row { display: flex; flex-wrap: wrap; gap: 6px; }
.focus-row button { padding: 5px 12px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: .82rem; cursor: pointer; }
.focus-row button.on { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent); }

.bg-msg { margin: 0; color: var(--success); font-size: .85rem; }
.bg-err { margin: 0; color: var(--danger); font-size: .85rem; }
.bg-actions { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; padding-top: 16px; border-top: 1px solid var(--border-line); }
.spacer { flex: 1; }
.save-btn { padding: 8px 24px; border: none; background: var(--accent-color); color: var(--on-accent); font: inherit; font-weight: 600; cursor: pointer; }
.ghost-btn { padding: 7px 16px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: .88rem; cursor: pointer; }
.link-btn { padding: 0; border: 0; background: none; color: var(--text-secondary); font: inherit; font-size: .82rem; text-decoration: underline; cursor: pointer; }
.save-btn:disabled, .ghost-btn:disabled, .link-btn:disabled { opacity: .5; cursor: not-allowed; }
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }

.ink-toggle { position: relative; display: inline-block; width: 44px; height: 24px; flex-shrink: 0; }
.ink-toggle input { opacity: 0; width: 0; height: 0; }
.toggle-slider { position: absolute; inset: 0; cursor: pointer; background-color: var(--bg-sub); transition: .3s; }
.toggle-slider::before { content: ""; position: absolute; left: 3px; bottom: 3px; width: 18px; height: 18px; border-radius: 50%; background-color: var(--paper-card); box-shadow: 0 2px 4px rgba(0,0,0,.1); transition: .3s; }
.ink-toggle input:checked + .toggle-slider { background-color: var(--text-primary); }
.ink-toggle input:checked + .toggle-slider::before { transform: translateX(20px); }

@media (max-width: 640px) {
  .bg-panel { padding: 16px; }
  .tiles { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .bg-actions .spacer { display: none; }
}
</style>
