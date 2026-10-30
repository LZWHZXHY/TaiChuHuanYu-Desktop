<!-- 后台 · 网站背景与赞助特权（仅超级管理员）：维护官方网站背景、个人主页素材（横幅 / 整页背景）；给账号手动开通 / 延长 / 收回赞助特权。
     素材可以填原作者（社区投稿被采用的图），作者账号自动免费拥有。 -->
<template>
  <div class="bp-admin">
    <nav class="bp-tabs" role="tablist" aria-label="分区">
      <button type="button" role="tab" :aria-selected="tab === 'presets'" :class="{ on: tab === 'presets' }" @click="tab = 'presets'">官方网站背景</button>
      <button type="button" role="tab" :aria-selected="tab === 'profile'" :class="{ on: tab === 'profile' }" @click="tab = 'profile'">主页素材</button>
      <button type="button" role="tab" :aria-selected="tab === 'perks'" :class="{ on: tab === 'perks' }" @click="tab = 'perks'">赞助特权</button>
    </nav>

    <p v-if="error" class="bp-error" role="alert">{{ error }}</p>
    <p v-if="notice" class="bp-notice" role="status">{{ notice }}</p>

    <!-- ===== 官方背景 ===== -->
    <section v-if="tab === 'presets' || tab === 'profile'" class="bp-section">
      <p v-if="tab === 'presets'" class="bp-muted">所有登录用户都能在「个人中心 → 网站背景」里选择这些背景（只有自己能看到）。在「交易行」把背景设成商品后，就要先用经验或土豆兑换。图片会自动转成 WebP；也可以上传 MP4 / WebM 短视频做动态背景。</p>
      <p v-else class="bp-muted">个人主页的素材库，和网站背景分开：「横幅」是主页顶部的宽图（建议 3:1 左右），「整页背景」是访客打开主页时整个页面的背景。用户在「个人中心 → 主页装扮」里选择；在「交易行」设成商品后要先兑换。</p>
      <ul v-if="currentList.length" class="bp-grid">
        <li v-for="p in currentList" :key="p.id" class="bp-card" :class="{ off: !p.enabled }">
          <span class="bp-thumb" :class="{ banner: (p as ProfilePreset).slot === 'banner' }" :style="thumb(p)"><em v-if="p.kind === 'video'">▶ 视频</em></span>
          <div class="bp-card-body">
            <b>{{ p.title }}</b>
            <small><template v-if="tab === 'profile'">{{ (p as ProfilePreset).slot === 'banner' ? '横幅' : '整页背景' }} · </template>排序 {{ p.sortOrder }}<template v-if="p.authorName"> · 作者 {{ p.authorName }}</template><template v-if="!p.enabled"> · 已隐藏</template></small>
          </div>
          <div class="bp-ops">
            <button type="button" @click="editPreset(p)">编辑</button>
            <button type="button" @click="togglePreset(p)">{{ p.enabled ? '隐藏' : '显示' }}</button>
            <button type="button" class="danger" @click="removePreset(p)">删除</button>
          </div>
        </li>
      </ul>
      <p v-else-if="!loading" class="bp-muted">{{ tab === 'presets' ? '还没有官方背景。' : '还没有主页素材。' }}</p>

      <form class="bp-form" @submit.prevent="savePreset">
        <h3>{{ form.id ? `编辑「${form.title}」` : tab === 'presets' ? '添加官方网站背景' : '添加主页素材' }}</h3>
        <div class="bp-media">
          <span class="bp-thumb large" :style="thumb(form)"><i v-if="!form.url">还没有素材</i><em v-if="form.url && form.kind === 'video'">▶ 视频</em></span>
          <div class="bp-media-ops">
            <input ref="imageInput" type="file" :accept="IMAGE_ACCEPT" hidden @change="pick($event, 'image')" />
            <input ref="videoInput" type="file" :accept="VIDEO_ACCEPT" hidden @change="pick($event, 'video')" />
            <button type="button" :disabled="!!stage" @click="imageInput?.click()">上传图片</button>
            <button type="button" :disabled="!!stage" @click="videoInput?.click()">上传视频</button>
            <span v-if="stage" class="bp-stage">{{ stage === 'convert' ? '正在处理…' : `上传中 ${progress}%` }}</span>
            <small v-if="mediaNote">{{ mediaNote }}</small>
          </div>
        </div>
        <div class="bp-fields">
          <label>名称<input v-model.trim="form.title" maxlength="60" required placeholder="例如：雨夜实验室" /></label>
          <label>排序<small>越小越靠前</small><input v-model.number="form.sortOrder" type="number" /></label>
          <label v-if="tab === 'profile'">用途
            <select v-model="form.slot"><option value="banner">横幅（主页顶部）</option><option value="page">整页背景</option></select>
          </label>
          <label class="check"><input v-model="form.enabled" type="checkbox" /> 对用户显示</label>
          <div class="wide author-field"><AuthorPicker v-model="author" :search="searchUsers" /><small>社区投稿被采用的图填上原作者，作者自动免费拥有，并在素材上署名。</small></div>
        </div>
        <div class="bp-actions">
          <button type="submit" class="btn-primary" :disabled="saving || !form.url || !form.title">{{ saving ? '保存中…' : '保存' }}</button>
          <button v-if="form.id" type="button" @click="resetForm">取消编辑</button>
        </div>
      </form>
    </section>

    <!-- ===== 赞助特权 ===== -->
    <section v-else class="bp-section">
      <p class="bp-muted">
        每笔赞助开通 {{ perkMeta.days }} 天，连续赞助顺延。用户在个人中心填一次爱发电订单号即可绑定，之后的赞助会在爱发电同步时自动开通。
        没走爱发电、或者想送特权的，在这里手动开通。
      </p>
      <div class="bp-features">
        <span>当前特权：</span>
        <em v-for="(rank, key) in perkMeta.features" :key="key">{{ FEATURE_NAMES[key] ?? key }} · {{ perkMeta.tiers[rank - 1] ?? '任意' }}及以上</em>
      </div>

      <form class="bp-form" @submit.prevent="grant">
        <h3>手动开通 / 延长</h3>
        <div class="bp-find">
          <input v-model.trim="query" placeholder="输入用户名查找" @keydown.enter.prevent="findUsers" />
          <button type="button" :disabled="!query || finding" @click="findUsers">{{ finding ? '查找中…' : '查找' }}</button>
        </div>
        <ul v-if="hits.length" class="bp-hits">
          <li v-for="u in hits" :key="u.userId"><button type="button" :class="{ on: target?.userId === u.userId }" @click="target = u">{{ u.name }}</button></li>
        </ul>
        <div class="bp-fields">
          <label>账号<input :value="target?.name ?? ''" readonly placeholder="先查找并点选一个账号" /></label>
          <label>天数
            <select v-model.number="days"><option :value="30">30 天</option><option :value="90">90 天</option><option :value="180">180 天</option><option :value="365">365 天</option></select>
          </label>
          <label>等级<select v-model="tier"><option v-for="t in perkMeta.tiers" :key="t" :value="t">{{ t }}</option></select></label>
          <label class="wide">备注<input v-model.trim="note" maxlength="100" placeholder="例如：微信转账 30 元 / 活动奖励" /></label>
        </div>
        <div class="bp-actions"><button type="submit" class="btn-primary" :disabled="!target || granting">{{ granting ? '处理中…' : `开通 ${days} 天` }}</button></div>
      </form>

      <h3 class="bp-subtitle">特权账号</h3>
      <table v-if="perkList.length" class="bp-table">
        <thead><tr><th>账号</th><th>等级</th><th>到期时间</th><th>爱发电</th><th></th></tr></thead>
        <tbody>
          <tr v-for="p in perkList" :key="p.userId" :class="{ expired: !p.active }">
            <td>{{ p.name }}</td>
            <td>{{ p.tier ?? '—' }}</td>
            <td>{{ p.until ? fmt(p.until) : '—' }}<em v-if="!p.active && p.until"> 已到期</em></td>
            <td>{{ p.afdianBound ? '已绑定' : '—' }}</td>
            <td class="bp-row-ops">
              <button type="button" @click="quickExtend(p)">+30 天</button>
              <button type="button" @click="showLog(p)">记录</button>
              <button v-if="p.active" type="button" class="danger" @click="revoke(p)">收回</button>
            </td>
          </tr>
        </tbody>
      </table>
      <p v-else-if="!loading" class="bp-muted">还没有开通过特权的账号。</p>

      <div v-if="logRows" class="bp-log">
        <h3 class="bp-subtitle">{{ logTitle }} <button type="button" class="link" @click="logRows = null">收起</button></h3>
        <ul>
          <li v-for="l in logRows" :key="l.id">
            <time>{{ fmt(l.createdAt) }}</time>
            <b>{{ SOURCE_LABEL[l.source] }}</b>
            <span>{{ l.source === 'revoke' ? '立即到期' : `+${l.days} 天` }}<template v-if="l.tier"> · {{ l.tier }}</template><template v-if="l.untilAfter"> · 到 {{ fmt(l.untilAfter) }}</template></span>
            <small>{{ l.note }}<template v-if="l.refId"> · 订单 {{ l.refId }}</template></small>
          </li>
          <li v-if="!logRows.length" class="bp-muted">没有记录。</li>
        </ul>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useCos } from '@/composables/useCos'
import { adminAppearanceApi, apiMessage, SOURCE_LABEL, type BgKind, type BgPreset, type PerkItem, type PerkList, type PerkLogRow, type ProfilePreset, type ProfileSlot, type UserHit } from '@/api/appearance'
import AuthorPicker, { type AuthorRef } from './AuthorPicker.vue'
import { IMAGE_ACCEPT, MediaError, VIDEO_ACCEPT, uploadImage, uploadVideo } from '@/utils/backgroundMedia'

const FEATURE_NAMES: Record<string, string> = { 'custom-background': '自定义网站背景', 'custom-profile': '自定义主页横幅 / 背景' }

const tab = ref<'presets' | 'profile' | 'perks'>('presets')
const loading = ref(true)
const error = ref(''), notice = ref('')
function say(text: string, isError = false) { notice.value = isError ? '' : text; error.value = isError ? text : '' }
const fmt = (iso: string) => new Date(iso).toLocaleString('zh-CN', { year: 'numeric', month: 'numeric', day: 'numeric', hour: '2-digit', minute: '2-digit' })

// ===== 官方背景 =====
const { uploadFile, progress } = useCos()
const presets = ref<BgPreset[]>([])
const imageInput = ref<HTMLInputElement | null>(null)
const videoInput = ref<HTMLInputElement | null>(null)
const stage = ref<'' | 'convert' | 'upload'>('')
const mediaNote = ref('')
const saving = ref(false)
const profilePresets = ref<ProfilePreset[]>([])
const currentList = computed<(BgPreset | ProfilePreset)[]>(() => tab.value === 'profile' ? profilePresets.value : presets.value)
const author = ref<AuthorRef | null>(null)
const searchUsers = (q: string) => adminAppearanceApi.findUsers(q)
const emptyForm = () => ({ id: 0, title: '', kind: 'image' as BgKind, url: '', poster: null as string | null, sortOrder: 0, enabled: true, slot: 'banner' as ProfileSlot })
const form = reactive(emptyForm())

const thumb = (m: { kind: BgKind; url: string; poster: string | null }) => {
  const url = m.kind === 'video' ? m.poster : m.url
  return url ? { backgroundImage: `url(${JSON.stringify(url)})` } : undefined
}

async function loadPresets() {
  try { presets.value = await adminAppearanceApi.presets() } catch (e) { say(apiMessage(e, '读取官方背景失败'), true) }
}
async function loadProfilePresets() {
  try { profilePresets.value = await adminAppearanceApi.profilePresets() } catch (e) { say(apiMessage(e, '读取主页素材失败'), true) }
}
function resetForm() { Object.assign(form, emptyForm()); author.value = null; mediaNote.value = '' }
function editPreset(p: BgPreset | ProfilePreset) {
  Object.assign(form, { id: p.id, title: p.title, kind: p.kind, url: p.url, poster: p.poster ?? null, sortOrder: p.sortOrder, enabled: p.enabled, slot: (p as ProfilePreset).slot ?? 'banner' })
  author.value = p.authorId ? { id: p.authorId, name: p.authorName ?? '' } : null
  mediaNote.value = ''
}
const isProfile = () => tab.value === 'profile'
const reloadCurrent = () => (isProfile() ? loadProfilePresets() : loadPresets())
async function pick(event: Event, type: 'image' | 'video') {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  say('')
  try {
    const upload = (f: File) => uploadFile(f, isProfile() ? 'backgrounds/profile' : 'backgrounds/official')
    const onStage = (s: 'convert' | 'upload') => { stage.value = s }
    const media = type === 'image' ? await uploadImage(file, upload, onStage) : await uploadVideo(file, upload, onStage)
    Object.assign(form, { kind: media.kind, url: media.url, poster: media.poster })
    if (!form.title) form.title = file.name.replace(/\.[^.]+$/, '').slice(0, 60)
    mediaNote.value = media.note
  } catch (e) {
    say(e instanceof MediaError ? e.message : '上传失败，请稍后再试', true)
  } finally { stage.value = '' }
}
async function savePreset() {
  saving.value = true
  try {
    const body = { title: form.title, kind: form.kind, url: form.url, poster: form.poster, sortOrder: form.sortOrder, enabled: form.enabled, authorId: author.value?.id ?? null, slot: form.slot }
    if (isProfile()) await adminAppearanceApi.saveProfilePreset(body, form.id || undefined)
    else await adminAppearanceApi.savePreset(body, form.id || undefined)
    say(form.id ? '已保存修改' : isProfile() ? '已添加主页素材' : '已添加官方背景')
    resetForm()
    await reloadCurrent()
  } catch (e) { say(apiMessage(e, '保存失败'), true) } finally { saving.value = false }
}
async function togglePreset(p: BgPreset | ProfilePreset) {
  try {
    const body = { title: p.title, kind: p.kind, url: p.url, poster: p.poster ?? null, sortOrder: p.sortOrder, enabled: !p.enabled, authorId: p.authorId ?? null, slot: (p as ProfilePreset).slot }
    if (isProfile()) await adminAppearanceApi.saveProfilePreset(body, p.id)
    else await adminAppearanceApi.savePreset(body, p.id)
    await reloadCurrent()
  } catch (e) { say(apiMessage(e, '操作失败'), true) }
}
async function removePreset(p: BgPreset | ProfilePreset) {
  if (!window.confirm(`删除「${p.title}」？正在使用它的用户会回到默认。`)) return
  try {
    if (isProfile()) await adminAppearanceApi.deleteProfilePreset(p.id)
    else await adminAppearanceApi.deletePreset(p.id)
    say('已删除'); await reloadCurrent()
  } catch (e) { say(apiMessage(e, '删除失败'), true) }
}

// ===== 赞助特权 =====
const perkMeta = reactive<Omit<PerkList, 'items'>>({ tiers: ['觉醒者', '超凡者', '大超凡者', '君者'], features: {}, days: 30, minAmount: 0 })
const perkList = ref<PerkItem[]>([])
const query = ref('')
const finding = ref(false)
const hits = ref<UserHit[]>([])
const target = ref<UserHit | null>(null)
const days = ref(30)
const tier = ref('觉醒者')
const note = ref('')
const granting = ref(false)
const logRows = ref<PerkLogRow[] | null>(null)
const logTitle = ref('')

async function loadPerks() {
  try {
    const r = await adminAppearanceApi.perks()
    perkList.value = r.items
    Object.assign(perkMeta, { tiers: r.tiers, features: r.features, days: r.days, minAmount: r.minAmount })
  } catch (e) { say(apiMessage(e, '读取赞助特权失败'), true) }
}
async function findUsers() {
  if (!query.value) return
  finding.value = true
  try {
    hits.value = await adminAppearanceApi.findUsers(query.value)
    if (!hits.value.length) say('没有找到这个用户名', true)
    else if (hits.value.length === 1) target.value = hits.value[0] ?? null
  } catch (e) { say(apiMessage(e, '查找失败'), true) } finally { finding.value = false }
}
async function grant() {
  if (!target.value) return
  granting.value = true
  try {
    const r = await adminAppearanceApi.grant(target.value.userId, days.value, tier.value, note.value || undefined)
    say(`已为「${target.value.name}」开通，有效期到 ${fmt(r.until)}`)
    note.value = ''
    await loadPerks()
  } catch (e) { say(apiMessage(e, '开通失败'), true) } finally { granting.value = false }
}
async function quickExtend(p: PerkItem) {
  try {
    const r = await adminAppearanceApi.grant(p.userId, 30, p.tier ?? perkMeta.tiers[0] ?? '觉醒者', '后台快捷延长')
    say(`「${p.name}」已延长到 ${fmt(r.until)}`)
    await loadPerks()
  } catch (e) { say(apiMessage(e, '操作失败'), true) }
}
async function revoke(p: PerkItem) {
  if (!window.confirm(`收回「${p.name}」的赞助特权？会立即到期。`)) return
  try { await adminAppearanceApi.revoke(p.userId, '后台收回'); say('已收回'); await loadPerks() } catch (e) { say(apiMessage(e, '操作失败'), true) }
}
async function showLog(p: PerkItem) {
  try { logRows.value = await adminAppearanceApi.log(p.userId); logTitle.value = `「${p.name}」的特权记录` } catch (e) { say(apiMessage(e, '读取记录失败'), true) }
}

watch(tab, () => { say(''); resetForm() })
onMounted(async () => {
  await Promise.all([loadPresets(), loadProfilePresets(), loadPerks()])
  loading.value = false
})
</script>

<style scoped>
.bp-admin { display: grid; gap: 18px; color: var(--text-primary); }
.bp-tabs { display: flex; border-bottom: 1px solid var(--border-line); }
.bp-tabs button { padding: 8px 16px; border: 0; border-bottom: 2px solid transparent; background: none; color: var(--text-secondary); font: inherit; cursor: pointer; }
.bp-tabs button.on { border-bottom-color: var(--accent-color); color: var(--accent-color); }
.bp-section { display: grid; gap: 16px; }
.bp-muted { margin: 0; color: var(--text-secondary); font-size: 13px; line-height: 1.7; }
.bp-error { margin: 0; color: var(--danger); font-size: 13px; }
.bp-notice { margin: 0; color: var(--success); font-size: 13px; }

.bp-grid { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 12px; }
.bp-card { display: grid; gap: 8px; padding: 10px; border: 1px solid var(--border-line); background: var(--paper-card); }
.bp-card.off { opacity: .6; }
.bp-thumb { position: relative; display: block; aspect-ratio: 16 / 9; border: 1px solid var(--border-line); background: var(--bg-sub) center / cover no-repeat; }
.bp-thumb.large { width: 260px; max-width: 100%; }
.bp-thumb.banner { aspect-ratio: 3 / 1; }
.author-field { display: grid; gap: 4px; grid-column: 1 / -1; }
.author-field small { color: var(--text-secondary); font-size: 11px; }
.bp-thumb em { position: absolute; right: 4px; bottom: 4px; padding: 0 5px; background: rgb(0 0 0 / 60%); color: #fff; font-size: 10px; font-style: normal; }
.bp-thumb i { position: absolute; inset: 0; display: grid; place-items: center; color: var(--text-secondary); font-size: 12px; font-style: normal; }
.bp-card-body { display: grid; gap: 2px; }
.bp-card-body small { color: var(--text-secondary); font-size: 12px; }
.bp-ops, .bp-row-ops { display: flex; flex-wrap: wrap; gap: 6px; }

.bp-form { display: grid; gap: 14px; padding: 16px; border: 1px solid var(--border-line); background: var(--paper-card); }
.bp-form h3, .bp-subtitle { margin: 0; font-size: 15px; }
.bp-subtitle { display: flex; align-items: center; gap: 10px; }
.bp-media { display: flex; flex-wrap: wrap; gap: 16px; align-items: flex-start; }
.bp-media-ops { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; max-width: 420px; }
.bp-media-ops small { flex-basis: 100%; color: var(--text-secondary); font-size: 12px; }
.bp-stage { color: var(--accent-color); font-size: 12px; }
.bp-fields { display: grid; grid-template-columns: repeat(auto-fill, minmax(180px, 1fr)); gap: 12px; }
.bp-fields label { display: grid; gap: 4px; font-size: 13px; }
.bp-fields label small { color: var(--text-secondary); font-size: 11px; }
.bp-fields label.wide { grid-column: span 2; }
.bp-fields label.check { display: flex; align-items: center; gap: 6px; align-self: end; }
.bp-fields input:not([type=checkbox]), .bp-fields select, .bp-find input { padding: 7px 9px; border: 1px solid var(--border-line); background: var(--bg-main); color: var(--text-primary); font: inherit; font-size: 13px; }
.bp-actions { display: flex; gap: 10px; }
.bp-find { display: flex; gap: 8px; max-width: 420px; }
.bp-find input { flex: 1; min-width: 0; }
.bp-hits { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: 6px; }
.bp-hits button.on { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent); }
.bp-features { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; font-size: 13px; color: var(--text-secondary); }
.bp-features em { padding: 2px 8px; border: 1px solid #d97706; color: #b45309; font-style: normal; font-size: 12px; }

button { padding: 5px 12px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
button:disabled { opacity: .5; cursor: not-allowed; }
button.danger { color: var(--danger); }
button.link { padding: 0; border: 0; color: var(--text-secondary); text-decoration: underline; font-size: 12px; }
.btn-primary { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent); font-weight: 600; }

.bp-table { width: 100%; border-collapse: collapse; font-size: 13px; }
.bp-table th, .bp-table td { padding: 8px 10px; border-bottom: 1px solid var(--border-line); text-align: left; }
.bp-table th { color: var(--text-secondary); font-weight: 400; font-size: 12px; }
.bp-table tr.expired td { color: var(--text-secondary); }
.bp-table td em { color: var(--danger); font-style: normal; font-size: 12px; }
.bp-log ul { list-style: none; margin: 8px 0 0; padding: 0; display: grid; gap: 6px; }
.bp-log li { display: flex; flex-wrap: wrap; gap: 4px 12px; padding: 8px 10px; border: 1px solid var(--border-line); font-size: 13px; }
.bp-log time, .bp-log small { color: var(--text-secondary); font-size: 12px; }
@media (max-width: 700px) {
  .bp-fields label.wide { grid-column: auto; }
  .bp-table th:nth-child(4), .bp-table td:nth-child(4) { display: none; }
}
</style>
