<template>
  <div class="release-page content-page community-surface tone-library">
    <header class="rel-header surface-heading"><SectionTrace />
      <div>
        <p class="eyebrow">CHANGELOG · 版本更新</p>
        <h1>太初寰宇 · 更新日志</h1>
        <p class="sub">社区网站的每一次版本迭代，都会记录在这里。</p>
      </div>
      <div class="rel-actions">
        <span v-if="current" class="rel-current">{{ IS_LOCAL_DEV ? '本地运行的版本' : '当前线上版本' }} <strong>v{{ current.version }}</strong></span>
        <span v-if="beta" class="rel-current beta">内测中 <strong>v{{ beta.version }}</strong></span>
        <button v-if="groups.length > 1" class="btn" @click="toggleAll">{{ allExpanded ? '全部收起' : '全部展开' }}</button>
        <button v-if="isAdmin" class="btn primary" @click="openEditor()">+ 发布新版本</button>
      </div>
    </header>

    <p v-if="loading" class="rel-empty">加载中…</p>
    <p v-else-if="!releases.length" class="rel-empty">还没有版本记录。</p>

    <div v-else class="rel-groups">
      <section v-for="g in groups" :key="g.label" class="rel-group">
        <button type="button" class="rel-group-head" :aria-expanded="isExpanded(g.major)" @click="toggleGroup(g.major)">
          <span class="rel-caret" :class="{ open: isExpanded(g.major) }">▸</span>
          <h2 class="rel-group-title">{{ g.label }} 系列</h2>
          <span class="rel-group-count">{{ g.items.length }} 个版本</span>
        </button>
        <ol v-show="isExpanded(g.major)" class="rel-timeline">
          <li v-for="r in g.items" :id="'release-' + r.id" :key="r.id" class="rel-item" :class="['stage-' + stageOf(r), { focused: focusedId === r.id }]">
            <div class="rel-dot"></div>
            <div class="rel-card">
              <div class="rel-card-head">
                <div class="rel-version-row">
                  <span class="rel-version">v{{ r.version }}</span>
                  <span v-if="stageOf(r) === 'current'" class="rel-badge">{{ IS_LOCAL_DEV ? '本地运行中' : '当前版本 · 线上' }}</span>
                  <span v-else-if="stageOf(r) === 'beta'" class="rel-badge beta" title="更新说明已经写好，这个版本还没部署到正式网站">内测版 · 未上架</span>
                  <span v-else-if="stageOf(r) === 'draft'" class="rel-badge draft" title="只有管理员看得到">草稿 · 未公开</span>
                </div>
                <span class="rel-date">{{ formatDate(r.releasedAt) }}</span>
              </div>
              <h3 v-if="r.title" class="rel-title">{{ r.title }}</h3>
              <div v-if="r.content" class="rel-content">{{ r.content }}</div>
              <div v-if="isAdmin" class="rel-admin">
                <button class="mini-btn" @click="openEditor(r)">编辑</button>
                <button class="mini-btn danger" @click="removeItem(r)">删除</button>
              </div>
            </div>
          </li>
        </ol>
      </section>
    </div>

    <section class="release-development" aria-label="开发提交记录"><UpdateLog /></section>

    <!-- 编辑器 -->
    <div v-if="showEditor" class="rel-mask" @click.self="showEditor = false">
      <div class="rel-editor">
        <h2 class="rel-editor-title">{{ editorId ? '编辑版本' : '发布新版本' }}</h2>
        <div class="field-row">
          <label class="field"><span>版本号</span><input v-model="form.version" placeholder="7.0.0" /></label>
          <label class="field"><span>发布日期</span><input v-model="form.releasedAt" type="datetime-local" /></label>
        </div>
        <p class="rel-hint">当前看到的网站打包于 {{ buildTimeText }}{{ IS_LOCAL_DEV ? '（本地开发）' : '' }}。发布日期晚于打包时间的版本显示为「内测版 · 未上架」；重新打包部署后，它会自动变成当前版本，上一个版本变成历史版本。所以请在打包之前写好更新说明。</p>
        <label class="field"><span>版本标题</span><input v-model="form.title" placeholder="例如：接入 DSH，开启 7.0.0" /></label>
        <label class="field"><span>更新内容（支持换行）</span>
          <textarea v-model="form.content" rows="10" placeholder="新增：&#10;- 站内通知系统&#10;- 必吃榜&#10;修复：&#10;- 财政图表小数精度"></textarea>
        </label>
        <label class="check"><input v-model="form.isPublished" type="checkbox" /> 立即公开</label>
        <div class="rel-editor-actions">
          <button class="btn" @click="showEditor = false">取消</button>
          <button class="btn primary" :disabled="saving" @click="saveItem">{{ saving ? '保存中…' : '保存' }}</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import SectionTrace from '@/components/SectionTrace.vue'
import { ref, computed, onMounted, nextTick } from 'vue'
import { useRoute } from 'vue-router'
import { releaseApi, type ReleaseItem, type ReleasePayload } from '@/api/release'
import { useUserStore } from '@/stores/user'
import { serverDate } from '@/utils/serverTime'
import { versionSummary, formatBuildTime, IS_LOCAL_DEV } from '@/utils/appVersion'
import UpdateLog from '@/views/推送首页/components/updateLog.vue'

const userStore = useUserStore()
const releases = ref<ReleaseItem[]>([])
const loading = ref(false)
const isAdmin = computed(() => userStore.permissions?.includes('SuperAdmin') ?? false)
// 线上版本 / 内测版：按「前端打包时间」自动判断，见 utils/appVersion.ts
const summary = computed(() => versionSummary(releases.value))
const current = computed(() => summary.value.current)
const beta = computed(() => summary.value.beta)
const stageOf = (r: ReleaseItem) => summary.value.stages.get(r.id) ?? 'past'
const buildTimeText = formatBuildTime()

// 按大版本自动分组（5.x 一组、7.x 一组）
const groups = computed(() => {
  const map = new Map<number, { label: string; major: number; items: ReleaseItem[] }>()
  for (const r of releases.value) {
    const m = r.version.match(/(\d+)/)
    const major = m ? Number(m[1]) : 0
    if (!map.has(major)) map.set(major, { label: `v${major}.0`, major, items: [] })
    map.get(major)!.items.push(r)
  }
  return [...map.values()].sort((a, b) => b.major - a.major)
})

// 折叠状态：默认只展开最新一组
const expandedGroups = ref<Set<number>>(new Set())
const isExpanded = (major: number) => expandedGroups.value.has(major)
const toggleGroup = (major: number) => {
  const next = new Set(expandedGroups.value)
  next.has(major) ? next.delete(major) : next.add(major)
  expandedGroups.value = next
}
const allExpanded = computed(() => groups.value.length > 0 && expandedGroups.value.size >= groups.value.length)
const toggleAll = () => {
  expandedGroups.value = allExpanded.value ? new Set() : new Set(groups.value.map(g => g.major))
}

const showEditor = ref(false)
const editorId = ref<number | null>(null)
const saving = ref(false)

const toLocalInput = (iso: string | null) => {
  const d = iso ? serverDate(iso) : new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

// 自动建议下一个版本号：在最新版本的主版本号 +1（6.0.0 → 7.0.0）
function nextVersion(): string {
  const latest = releases.value[0]?.version // 包括内测版和草稿里最大的版本号
  if (!latest) return '1.0.0'
  const m = latest.match(/(\d+)\.(\d+)\.(\d+)/)
  if (!m) return latest
  return `${Number(m[1]) + 1}.0.0`
}

const form = ref<ReleasePayload>({ version: '', title: '', content: '', releasedAt: null, isPublished: true })

function openEditor(r?: ReleaseItem) {
  editorId.value = r?.id ?? null
  form.value = r
    ? { version: r.version, title: r.title, content: r.content || '', releasedAt: toLocalInput(r.releasedAt), isPublished: r.isPublished }
    : { version: nextVersion(), title: '', content: '', releasedAt: toLocalInput(null), isPublished: true }
  showEditor.value = true
}

async function saveItem() {
  if (!form.value.version.trim()) { alert('请填写版本号'); return }
  saving.value = true
  try {
    const payload: ReleasePayload = {
      ...form.value,
      releasedAt: form.value.releasedAt ? new Date(form.value.releasedAt).toISOString() : null
    }
    if (editorId.value) await releaseApi.update(editorId.value, payload)
    else await releaseApi.create(payload)
    showEditor.value = false
    await load()
  } catch (e: any) { alert(e.friendlyMessage || e.message || '保存失败') } finally { saving.value = false }
}

async function removeItem(r: ReleaseItem) {
  if (!confirm(`删除版本 v${r.version}？`)) return
  await releaseApi.remove(r.id)
  await load()
}

const formatDate = (v: string) => v ? serverDate(v).toLocaleDateString('zh-CN', { year: 'numeric', month: 'long', day: 'numeric' }) : ''

// 从知识库 / 别处点进来：/changelog?release=版本编号，展开那一组并滚动到那个版本
const route = useRoute()
const focusedId = ref<number | null>(null)
async function focusRequested() {
  const id = Number(route.query.release)
  const target = Number.isInteger(id) ? releases.value.find(r => r.id === id) : undefined
  if (!target) return
  const g = groups.value.find(x => x.items.includes(target))
  if (g && !expandedGroups.value.has(g.major)) expandedGroups.value = new Set([...expandedGroups.value, g.major])
  focusedId.value = target.id
  await nextTick()
  document.getElementById('release-' + target.id)?.scrollIntoView({ behavior: 'smooth', block: 'center' })
}

async function load() {
  loading.value = true
  try {
    releases.value = await releaseApi.list()
    if (groups.value.length && expandedGroups.value.size === 0) {
      expandedGroups.value = new Set([groups.value[0].major])
    }
  } catch { releases.value = [] } finally { loading.value = false }
  void focusRequested()
}

onMounted(load)
</script>

<style scoped>
.release-page { max-width: 860px; margin: 0 auto; padding-bottom: 56px; }
.rel-header { display: flex; justify-content: space-between; align-items: flex-end; gap: 16px; flex-wrap: wrap; padding: 28px 0 24px; }
.rel-header h1 { margin: 6px 0 8px; font-size: clamp(1.7rem, 3.2vw, 2.3rem); }
.rel-header .sub { margin: 0; color: var(--text-secondary); font-size: .9rem; }
.rel-actions { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
.rel-current { font-size: .82rem; color: var(--text-secondary); }
.rel-current strong { color: var(--accent-color); font-size: .95rem; }
.rel-empty { text-align: center; color: var(--text-secondary); padding: 48px 0; }
.rel-groups { display: grid; gap: 36px; }
.rel-group-head {
  display: flex; align-items: center; gap: 10px; width: 100%;
  margin-bottom: 16px; padding: 0 0 8px;
  border: none; border-bottom: 1px solid var(--border-line);
  background: transparent; color: inherit; cursor: pointer; text-align: left;
}
.rel-group-head:hover .rel-group-title { color: var(--accent-color); }
.rel-caret { font-size: .9rem; color: var(--text-secondary); transition: transform .2s; display: inline-block; }
.rel-caret.open { transform: rotate(90deg); }
.rel-group-title { margin: 0; font-size: 1.1rem; }
.rel-group-count { font-size: .78rem; color: var(--text-secondary); }
.rel-timeline { list-style: none; margin: 0; padding: 0; position: relative; }
.rel-timeline::before { content: ''; position: absolute; left: 7px; top: 8px; bottom: 8px; width: 2px; background: var(--border-line); }
.rel-item { position: relative; padding-left: 34px; margin-bottom: 22px; }
.rel-dot { position: absolute; left: 0; top: 18px; width: 16px; height: 16px; border-radius: 50%; background: var(--paper-card); border: 2px solid var(--border-line); }
.rel-item.stage-current .rel-dot { border-color: var(--accent-color); background: var(--accent-color); box-shadow: 0 0 0 4px var(--accent-soft); }
.rel-item.stage-beta .rel-dot { border-style: dashed; border-color: #d97706; }
.rel-item.stage-beta .rel-card { border-top: 2px dashed #d97706; }
.rel-item.stage-draft .rel-card { opacity: .75; }
.rel-item.focused .rel-card { outline: 2px solid var(--accent-color); outline-offset: 2px; }
.rel-card { border: 0; border-top: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); padding: 18px 20px; }
.rel-item.stage-current .rel-card { border-top: 2px solid var(--accent-color); background: linear-gradient(115deg,var(--accent-soft),var(--paper-card) 65%); }
.rel-card-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-wrap: wrap; }
.rel-version-row { display: flex; align-items: center; gap: 8px; }
.rel-version { font-size: 1.3rem; font-weight: 700; letter-spacing: -.5px; color: var(--text-primary); }
.rel-badge { font-size: .68rem; padding: 1px 8px; border-radius: 0; background: var(--accent-soft); color: var(--accent-color); font-weight: 600; }
.rel-badge.draft { background: var(--bg-sub); color: var(--text-secondary); }
.rel-badge.beta { background: color-mix(in srgb, #d97706 16%, transparent); color: #b45309; }
.rel-current.beta strong { color: #b45309; }
.rel-hint { margin: -4px 0 0; font-size: .76rem; line-height: 1.6; color: var(--text-secondary); }
.rel-date { font-size: .78rem; color: var(--text-secondary); }
.rel-title { margin: 10px 0 6px; font-size: 1.02rem; font-weight: 500; }
.rel-content { margin: 0; font-size: .88rem; line-height: 1.8; color: var(--text-primary); white-space: pre-wrap; word-break: break-word; }
.rel-admin { display: flex; gap: 8px; margin-top: 12px; }
.mini-btn { padding: 3px 10px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-secondary); cursor: pointer; font-size: .74rem; }
.mini-btn.danger { color: var(--danger); }
.rel-mask { position: fixed; inset: 0; z-index: 100; background: var(--overlay); display: flex; align-items: center; justify-content: center; padding: 20px; }
.rel-editor { border-top: 2px solid var(--signal-mint); box-shadow: var(--shadow-md); width: min(640px, 100%); max-height: 90vh; overflow-y: auto; background: var(--paper-card); border-radius: 0; padding: 24px; display: grid; gap: 14px; }
.rel-editor-title { margin: 0; font-size: 1.15rem; }
.field { display: grid; gap: 6px; font-size: .8rem; color: var(--text-secondary); }
.field input, .field textarea { padding: 9px 12px; border: 1px solid var(--border-line); border-radius: 0; background: var(--bg-main); color: var(--text-primary); font-size: .88rem; font-family: inherit; }
.field-row { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.check { display: flex; align-items: center; gap: 6px; font-size: .85rem; cursor: pointer; }
.rel-editor-actions { display: flex; justify-content: flex-end; gap: 10px; }
.btn { padding: 8px 18px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); cursor: pointer; font-size: .84rem; }
.btn.primary { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); }
.btn:disabled { opacity: .5; cursor: default; }
</style>
