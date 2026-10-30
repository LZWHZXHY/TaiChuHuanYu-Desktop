<template>
  <div class="showcase-admin">
    <div class="sa-head">
      <div>
        <h2>Steam 游戏</h2>
        <p>太虚游大厅顶部和「我们的游戏」里展示的作品。填 Steam 商店链接或 AppID，封面、简介、截图、价格、发售日会自动从 Steam 读取（每 6 小时更新一次）。还没上架的游戏可以先不填 AppID，状态选「开发中」。</p>
      </div>
      <RouterLink to="/games" class="sa-link">打开太虚游 →</RouterLink>
    </div>

    <p v-if="error" class="sa-error" role="alert">{{ error }}</p>
    <p v-if="notice" class="sa-notice" role="status">{{ notice }}</p>

    <ul v-if="games.length" class="sa-list">
      <li v-for="g in games" :key="g.id" class="sa-item" :class="{ hidden: !g.visible }">
        <img v-if="g.cover" :src="g.cover" alt="" class="sa-cover" />
        <span v-else class="sa-cover empty">无封面</span>
        <div class="sa-info">
          <b>{{ g.title }}</b>
          <span class="sa-tags">
            <em :class="'st-' + g.status">{{ statusLabel(g.status) }}</em>
            <em v-if="g.featured">主推</em>
            <em v-if="!g.visible">已隐藏</em>
            <em v-if="g.appId">AppID {{ g.appId }}</em>
            <em v-if="g.price">{{ g.price }}</em>
          </span>
          <small v-if="g.appId">Steam 数据：{{ g.steamFetchedAt ? `更新于 ${new Date(g.steamFetchedAt).toLocaleString('zh-CN')}` : '还没读取成功' }}</small>
        </div>
        <div class="sa-ops">
          <button type="button" @click="edit(g)">编辑</button>
          <button v-if="g.appId" type="button" :disabled="refreshing === g.id" @click="refresh(g)">{{ refreshing === g.id ? '读取中…' : '从 Steam 更新' }}</button>
          <button type="button" class="danger" @click="remove(g)">删除</button>
        </div>
      </li>
    </ul>
    <p v-else-if="!loading" class="sa-muted">还没有游戏。</p>

    <form class="sa-form" @submit.prevent="save">
      <h3>{{ editingId ? `编辑「${form.title}」` : '添加游戏' }}</h3>
      <div class="sa-steam">
        <label>Steam 商店链接或 AppID<input v-model.trim="form.steam" placeholder="https://store.steampowered.com/app/3654500/..." /></label>
        <button type="button" :disabled="!form.steam || looking" @click="lookup">{{ looking ? '读取中…' : '从 Steam 读取' }}</button>
      </div>
      <div v-if="preview" class="sa-preview">
        <img v-if="preview.info.headerImage" :src="preview.info.headerImage" alt="" />
        <div><b>{{ preview.info.name }}</b><p>{{ preview.info.shortDescription }}</p><small>{{ [preview.info.releaseDate, preview.info.price, preview.info.genres.join(' / ')].filter(Boolean).join(' · ') }}</small></div>
      </div>
      <div class="sa-grid">
        <label class="wide">显示名称<small>大厅里显示的名字，可以用中文名</small><input v-model.trim="form.title" maxlength="100" required /></label>
        <label class="full">一句话推荐<small>显示在 Steam 简介前面，写给社区玩家看的</small><textarea v-model="form.tagline" rows="2" maxlength="300"></textarea></label>
        <label>状态<select v-model="form.status"><option value="released">已发售</option><option value="coming">即将推出</option><option value="dev">开发中</option></select></label>
        <label>排序<small>越小越靠前</small><input v-model.number="form.sortOrder" type="number" /></label>
        <label class="check"><input v-model="form.featured" type="checkbox" /> 放在顶部主推位</label>
        <label class="check"><input v-model="form.visible" type="checkbox" /> 在大厅显示</label>
        <label class="wide">自定义封面<small>可选；不填就用 Steam 的封面。还没上架的游戏建议填</small><input v-model.trim="form.coverUrl" placeholder="https://..." /></label>
        <label>关联开发进度<small>大厅里显示「开发日志」入口</small>
          <select v-model="form.devProjectId"><option value="">不关联</option><option v-for="p in projects" :key="p.id" :value="p.id">{{ p.title }}</option></select>
        </label>
        <label>讨论标签<small>论坛里的标签，例如 喝水房间</small><input v-model.trim="form.discussionTag" maxlength="40" /></label>
      </div>
      <div class="sa-actions">
        <button type="submit" class="btn-primary" :disabled="saving">{{ saving ? '保存中…' : '保存' }}</button>
        <button v-if="editingId" type="button" @click="reset">取消编辑</button>
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { adminShowcaseApi, statusLabel, type SaveShowcase, type ShowcaseGame, type SteamLookup } from '@/api/gameShowcase'
import { devProjectApi } from '@/api/devProject'
import { chatError } from '@/api/chat'

const games = ref<ShowcaseGame[]>([])
const projects = ref<{ id: string; title: string }[]>([])
const loading = ref(true), saving = ref(false), looking = ref(false), refreshing = ref(0)
const error = ref(''), notice = ref('')
const editingId = ref(0)
const preview = ref<SteamLookup | null>(null)
const blank = (): SaveShowcase => ({ steam: '', title: '', tagline: '', status: 'released', featured: false, sortOrder: (games.value.length + 1) * 10, coverUrl: '', devProjectId: '', discussionTag: '', visible: true })
const form = reactive<SaveShowcase>(blank())

async function load() {
  loading.value = true
  try { games.value = await adminShowcaseApi.list(); error.value = '' }
  catch (e) { error.value = chatError(e, '读取失败。如果刚更新过后端，请确认已重新部署。') }
  finally { loading.value = false }
}
function reset() { Object.assign(form, blank()); editingId.value = 0; preview.value = null }
function edit(g: ShowcaseGame) {
  editingId.value = g.id
  preview.value = null
  Object.assign(form, {
    steam: g.appId ? String(g.appId) : '', title: g.title, tagline: g.tagline, status: g.status, featured: g.featured, sortOrder: g.sortOrder,
    coverUrl: g.customCover ?? '', devProjectId: g.devProjectId ?? '', discussionTag: g.discussionTag ?? '', visible: g.visible,
  })
}
async function lookup() {
  looking.value = true; error.value = ''
  try {
    preview.value = await adminShowcaseApi.lookup(form.steam)
    if (!form.title) form.title = preview.value.info.name
    if (preview.value.info.comingSoon && form.status === 'released') form.status = 'coming'
  } catch (e) { error.value = chatError(e, '没能从 Steam 读取') } finally { looking.value = false }
}
async function save() {
  saving.value = true; error.value = ''
  try {
    await adminShowcaseApi.save({ ...form }, editingId.value || undefined)
    notice.value = editingId.value ? '已保存' : '已添加，太虚游大厅里马上就能看到'
    reset(); await load()
  } catch (e) { error.value = chatError(e, '保存失败') } finally { saving.value = false }
}
async function refresh(g: ShowcaseGame) {
  refreshing.value = g.id
  try { await adminShowcaseApi.refresh(g.id); notice.value = `已从 Steam 更新「${g.title}」`; await load() }
  catch (e) { error.value = chatError(e, '从 Steam 更新失败') } finally { refreshing.value = 0 }
}
async function remove(g: ShowcaseGame) {
  if (!window.confirm(`从大厅删除「${g.title}」？（不会影响 Steam 上的游戏）`)) return
  try { await adminShowcaseApi.remove(g.id); await load() } catch (e) { error.value = chatError(e, '删除失败') }
}

onMounted(async () => {
  await load(); reset()
  try { projects.value = (await devProjectApi.list()).map(p => ({ id: String(p.id), title: p.name })) } catch { projects.value = [] }
})
</script>

<style scoped>
.showcase-admin { display: grid; gap: 20px; color: var(--text-primary); }
.sa-head { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-end; gap: 12px; padding: 22px 24px; border: 1px solid var(--border-line); background: var(--paper-card); }
.sa-head h2 { margin: 0 0 6px; font-size: 22px; }
.sa-head p { margin: 0; max-width: 760px; color: var(--text-secondary); font-size: 13px; line-height: 1.7; }
.sa-link { color: var(--accent-color); font-size: 14px; }
.sa-error, .sa-notice { margin: 0; padding: 10px 14px; border-left: 3px solid var(--danger); color: var(--danger); background: var(--paper-card); }
.sa-notice { border-left-color: var(--success); color: var(--text-primary); }
.sa-muted { color: var(--text-secondary); }
.sa-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 10px; }
.sa-item { display: grid; grid-template-columns: 184px minmax(0, 1fr) auto; gap: 16px; align-items: center; padding: 12px; border: 1px solid var(--border-line); background: var(--paper-card); }
.sa-item.hidden { opacity: .6; }
.sa-cover { width: 184px; aspect-ratio: 460 / 215; object-fit: cover; display: grid; place-items: center; background: var(--bg-sub); color: var(--text-secondary); font-size: 12px; }
.sa-info { display: grid; gap: 6px; min-width: 0; }
.sa-tags { display: flex; flex-wrap: wrap; gap: 6px; }
.sa-tags em { padding: 1px 6px; border: 1px solid var(--border-line); font-style: normal; font-size: 12px; color: var(--text-secondary); }
.sa-tags em.st-released { color: var(--success); border-color: currentColor; }
.sa-tags em.st-coming { color: var(--warning, #b7791f); border-color: currentColor; }
.sa-info small { color: var(--text-secondary); font-size: 12px; }
.sa-ops { display: flex; flex-wrap: wrap; gap: 6px; justify-content: flex-end; }
.sa-ops button, .sa-steam button, .sa-actions button { padding: 6px 12px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.sa-ops button.danger { color: var(--danger); }
.sa-form { display: grid; gap: 14px; padding: 20px 24px; border: 1px solid var(--border-line); background: var(--paper-card); }
.sa-form h3 { margin: 0; font-size: 16px; }
.sa-steam { display: flex; align-items: flex-end; gap: 10px; }
.sa-steam label { flex: 1; display: grid; gap: 4px; font-size: 13px; }
.sa-preview { display: grid; grid-template-columns: 220px minmax(0, 1fr); gap: 14px; padding: 12px; border: 1px dashed var(--border-line); }
.sa-preview img { width: 100%; }
.sa-preview p { margin: 6px 0; color: var(--text-secondary); font-size: 13px; line-height: 1.6; }
.sa-preview small { color: var(--text-secondary); }
.sa-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 14px; }
.sa-grid label { display: grid; gap: 4px; font-size: 13px; align-content: start; }
.sa-grid small { color: var(--text-secondary); font-size: 11px; }
.sa-grid .wide { grid-column: span 2; }
.sa-grid .full { grid-column: 1 / -1; }
.sa-grid .check { display: flex; align-items: center; gap: 8px; align-self: center; }
.showcase-admin input:not([type=checkbox]), .showcase-admin select, .showcase-admin textarea { padding: 8px 10px; border: 1px solid var(--border-line); background: var(--bg-main, var(--paper-card)); color: var(--text-primary); font: inherit; }
.sa-actions { display: flex; gap: 10px; }
.sa-actions .btn-primary { background: var(--accent-color); border-color: var(--accent-color); color: var(--on-accent); }
@media (max-width: 900px) { .sa-item { grid-template-columns: 1fr; } .sa-cover { width: 100%; } .sa-grid { grid-template-columns: 1fr 1fr; } .sa-preview { grid-template-columns: 1fr; } }
</style>
