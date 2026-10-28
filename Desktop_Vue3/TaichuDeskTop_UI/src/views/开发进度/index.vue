<template>
  <div class="report-page content-page">
    <header class="report-header">
      <div>
        <h1>研发报告</h1>
        <p class="sub">天枢 AI · 我的世界 Mod · 虚幻游戏 · 世界观 · IP 动画——记录我们的研发进展</p>
      </div>
      <button v-if="isAdmin" class="btn primary" @click="openEditor()">+ 新增报告</button>
    </header>

    <div class="report-filters">
      <div class="cat-tabs">
        <button v-for="c in categories" :key="c.key" class="cat-tab" :class="{ active: filterCategory === c.key }" @click="filterCategory = c.key">{{ c.label }}</button>
      </div>
      <div class="status-tabs">
        <button v-for="s in statuses" :key="s.key" class="status-tab" :class="{ active: filterStatus === s.key }" @click="filterStatus = s.key">{{ s.label }}</button>
      </div>
    </div>

    <p v-if="loading" class="report-empty">加载中…</p>
    <p v-else-if="!filtered.length" class="report-empty">该分类下暂无报告</p>

    <div v-else class="report-list">
      <template v-for="(node, i) in filtered" :key="node.project.id">
        <!-- 系列（含子项目） -->
        <section v-if="node.children.length" class="series">
          <RouterLink :to="`/dev-progress/${node.project.id}`" class="series-head">
            <span class="report-num">{{ String(i + 1).padStart(2, '0') }}</span>
            <span class="series-icon">{{ node.project.icon }}</span>
            <h2 class="series-name">{{ node.project.name }}</h2>
            <span class="tag" :class="node.project.status">{{ statusLabel(node.project.status) }}</span>
            <span class="series-summary">
              <span class="series-summary-bar"><span class="series-summary-fill" :style="{ width: seriesSummary(node.children).avg + '%' }"></span></span>
              <span class="series-summary-text">{{ node.children.length }} 项 · 平均 {{ seriesSummary(node.children).avg }}%</span>
            </span>
            <span v-if="isAdmin" class="mini-btn" @click.prevent="openEditor(node.project)">编辑</span>
            <span class="report-arrow">→</span>
          </RouterLink>
          <div class="series-children">
            <RouterLink v-for="c in node.children" :key="c.id" :to="`/dev-progress/${c.id}`" class="report-item child-item">
              <div class="report-main">
                <div class="report-title-row">
                  <h3 class="report-title"><span class="report-icon">{{ c.icon }}</span>{{ c.name }}</h3>
                  <span class="tag" :class="c.status">{{ statusLabel(c.status) }}</span>
                  <span v-if="isAdmin" class="mini-btn" @click.prevent="openEditor(c)">编辑</span>
                </div>
                <p v-if="c.description" class="report-abstract">{{ c.description }}</p>
                <div class="report-meta">
                  <span>{{ categoryLabel(c.category) }}</span>
                  <span class="dot">·</span>
                  <span>进度 {{ c.progress }}%</span>
                  <span class="dot">·</span>
                  <span>{{ formatDate(c.updatedAt) }}</span>
                </div>
              </div>
              <span class="report-arrow">→</span>
            </RouterLink>
          </div>
        </section>

        <!-- 独立项目 -->
        <RouterLink v-else :to="`/dev-progress/${node.project.id}`" class="report-item">
          <span class="report-num">{{ String(i + 1).padStart(2, '0') }}</span>
          <div class="report-main">
            <div class="report-title-row">
              <h2 class="report-title"><span class="report-icon">{{ node.project.icon }}</span>{{ node.project.name }}</h2>
              <span class="tag" :class="node.project.status">{{ statusLabel(node.project.status) }}</span>
              <span v-if="isAdmin" class="mini-btn" @click.prevent="openEditor(node.project)">编辑</span>
            </div>
            <p v-if="node.project.description" class="report-abstract">{{ node.project.description }}</p>
            <div class="report-meta">
              <span>{{ categoryLabel(node.project.category) }}</span>
              <span class="dot">·</span>
              <span>进度 {{ node.project.progress }}%</span>
              <span class="dot">·</span>
              <span>{{ formatDate(node.project.updatedAt) }}</span>
            </div>
          </div>
          <span class="report-arrow">→</span>
        </RouterLink>
      </template>
    </div>

    <!-- 编辑器 -->
    <div v-if="showEditor" class="editor-overlay" @click.self="closeEditor">
      <div class="editor">
        <header class="editor-head">
          <h2 class="editor-title">{{ editorId ? '编辑报告' : '新增报告' }}</h2>
          <button class="editor-close" @click="closeEditor">×</button>
        </header>

        <div class="editor-body">
          <!-- 基本信息 -->
          <section class="editor-section">
            <h3 class="editor-section-title">基本信息</h3>
            <label class="field"><span>报告标题</span><input v-model="form.name" placeholder="如：天枢 AI" /></label>
            <label class="field"><span>摘要</span><input v-model="form.description" placeholder="一句话摘要，显示在报告目录里" /></label>
            <label class="field"><span>所属系列（可选）</span>
              <select v-model="form.parentId">
                <option :value="null">无（独立项目）</option>
                <option v-for="opt in parentOptions" :key="opt.id" :value="opt.id">{{ opt.icon }} {{ opt.name }}</option>
              </select>
            </label>
            <div class="grid-4">
              <label class="field"><span>分类</span>
                <select v-model="form.category">
                  <option value="ai">AI</option>
                  <option value="mod">我的世界 Mod</option>
                  <option value="game">虚幻游戏</option>
                  <option value="world">世界观</option>
                  <option value="ip">IP 动画</option>
                  <option value="web">网站</option>
                  <option value="other">其他</option>
                </select>
              </label>
              <label class="field"><span>状态</span>
                <select v-model="form.status">
                  <option value="planning">规划中</option>
                  <option value="ongoing">进行中</option>
                  <option value="done">已完成</option>
                  <option value="paused">暂停</option>
                  <option value="dead">暴死</option>
                </select>
              </label>
              <label class="field"><span>进度 %</span><input v-model.number="form.progress" type="number" min="0" max="100" /></label>
              <label class="field"><span>图标</span><input v-model="form.icon" placeholder="🚀" maxlength="4" /></label>
              <label class="field"><span>类型</span>
                <select v-model="form.type">
                  <option value="dev">开发</option>
                  <option value="research">研究</option>
                </select>
              </label>
            </div>
          </section>

          <!-- 正文 -->
          <section class="editor-section">
            <h3 class="editor-section-title">正文内容 <small>Markdown</small></h3>
            <div class="md-toolbar">
              <button type="button" class="tool-btn" title="二级标题" @click="insertAtCursor('## ', 3)">H2</button>
              <button type="button" class="tool-btn" title="加粗" @click="insertAtCursor('****', 2)"><b>B</b></button>
              <button type="button" class="tool-btn" title="斜体" @click="insertAtCursor('**', 1)"><i>I</i></button>
              <button type="button" class="tool-btn" title="列表" @click="insertAtCursor('- ', 2)">• 列表</button>
              <button type="button" class="tool-btn" title="引用" @click="insertAtCursor('> ', 2)">引用</button>
              <button type="button" class="tool-btn" title="分割线" @click="insertAtCursor('\n---\n', 0)">—</button>
              <button type="button" class="tool-btn" :disabled="uploading" @click="pickImage">{{ uploading ? '上传中…' : '🖼 图片' }}</button>
              <button type="button" class="tool-btn preview-btn" @click="fullPreview = true">👁 全屏预览</button>
              <input ref="fileInput" type="file" accept="image/*" hidden @change="onImagePicked" />
            </div>
            <div class="md-split">
              <textarea ref="contentInput" v-model="form.content" class="md-input" placeholder="撰写报告正文，支持 Markdown 语法…"></textarea>
              <div class="md-preview-wrap">
                <div class="md-preview-label">预览</div>
                <div class="md-preview" v-html="previewHtml"></div>
              </div>
            </div>
          </section>

          <!-- 里程碑 -->
          <section class="editor-section">
            <h3 class="editor-section-title">里程碑 <small>按完成顺序排列</small></h3>
            <div class="ms-list-edit">
              <div v-for="(m, i) in form.milestones" :key="i" class="ms-edit-row">
                <input v-model="m.done" type="checkbox" title="已完成" />
                <input v-model="m.title" placeholder="里程碑描述（如：原型验证）" />
                <button type="button" class="mini-btn danger" @click="form.milestones.splice(i, 1)">删除</button>
              </div>
              <p v-if="!form.milestones.length" class="ms-empty">暂无里程碑</p>
            </div>
            <button type="button" class="btn" @click="form.milestones.push({ title: '', done: false })">+ 添加里程碑</button>
          </section>

          <!-- 参考文献 -->
          <section class="editor-section">
            <h3 class="editor-section-title">📚 参考文献 <small>论文 / 书籍 / 资料</small></h3>
            <div class="row-list">
              <div v-for="(r, i) in form.references" :key="r.id" class="row-card">
                <div class="row-grid">
                  <input v-model="r.title" class="span-2" placeholder="标题（必填）" />
                  <input v-model="r.authors" placeholder="作者" />
                  <input v-model="r.venue" placeholder="期刊 / 会议 / 出版社" />
                  <input v-model="r.year" placeholder="年份" />
                  <input v-model="r.url" class="span-2" placeholder="链接 https://…" />
                </div>
                <textarea v-model="r.note" rows="2" placeholder="我的批注：这篇讲什么、为什么重要…"></textarea>
                <button type="button" class="mini-btn danger" @click="form.references.splice(i, 1)">删除</button>
              </div>
              <p v-if="!form.references.length" class="ms-empty">还没有参考文献</p>
            </div>
            <button type="button" class="btn" @click="addReference">+ 添加文献</button>
          </section>

          <!-- 数据 -->
          <section class="editor-section">
            <h3 class="editor-section-title">📊 数据 <small>实验 / 调研数据点</small></h3>
            <div class="row-list">
              <div v-for="(d, i) in form.dataPoints" :key="d.id" class="row-card">
                <div class="row-grid">
                  <input v-model="d.name" class="span-2" placeholder="名称（如：人格一致性得分）" />
                  <input v-model="d.value" placeholder="数值" />
                  <input v-model="d.unit" placeholder="单位（%）" />
                </div>
                <textarea v-model="d.note" rows="2" placeholder="说明 / 数据来源"></textarea>
                <button type="button" class="mini-btn danger" @click="form.dataPoints.splice(i, 1)">删除</button>
              </div>
              <p v-if="!form.dataPoints.length" class="ms-empty">还没有数据</p>
            </div>
            <button type="button" class="btn" @click="addDataPoint">+ 添加数据</button>
          </section>

          <!-- 研究日志 -->
          <section class="editor-section">
            <h3 class="editor-section-title">🧪 研究日志 <small>过程记录</small></h3>
            <div class="row-list">
              <div v-for="(l, i) in form.logs" :key="l.id" class="row-card">
                <div class="row-grid">
                  <input v-model="l.date" type="date" />
                  <input v-model="l.title" class="span-2" placeholder="今天做了什么 / 发现了什么" />
                </div>
                <textarea v-model="l.content" rows="3" placeholder="详细记录…"></textarea>
                <div class="log-img-row">
                  <img v-for="(img, j) in (l.images || [])" :key="j" :src="img" alt="" />
                  <button type="button" class="mini-btn" @click="pickLogImage(i)">+ 配图</button>
                </div>
                <button type="button" class="mini-btn danger" @click="form.logs.splice(i, 1)">删除</button>
              </div>
              <p v-if="!form.logs.length" class="ms-empty">还没有日志</p>
            </div>
            <button type="button" class="btn" @click="addLog">+ 添加日志</button>
            <input ref="logFileInput" type="file" accept="image/*" hidden @change="onLogImagePicked" />
          </section>
        </div>

        <footer class="editor-foot">
          <span class="editor-hint">
            <template v-if="uploading">图片上传中…</template>
            <template v-else-if="draftSavedAt">已自动保存草稿 {{ draftSavedAt }} <button class="clear-draft" @click="clearDraft">清除草稿</button></template>
          </span>
          <button class="btn" @click="closeEditor">取消</button>
          <button class="btn primary" :disabled="saving" @click="saveProject">{{ saving ? '保存中…' : '保存报告' }}</button>
        </footer>
      </div>
    </div>

    <!-- 全屏预览 -->
    <div v-if="fullPreview" class="preview-overlay" @click.self="fullPreview = false">
      <div class="preview-full">
        <header class="preview-head">
          <span class="preview-title">{{ form.name || '正文预览' }}</span>
          <button class="btn" @click="fullPreview = false">✕ 退出全屏预览</button>
        </header>
        <div class="preview-body" v-html="previewHtml"></div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, nextTick, watch } from 'vue'
import { devProjectApi, type DevProject, type DevProjectPayload } from '@/api/devProject'
import { useUserStore } from '@/stores/user'
import { useCos } from '@/composables/useCos'
import { renderMarkdown } from '@/utils/markdown'

const userStore = useUserStore()
const projects = ref<DevProject[]>([])
const loading = ref(false)
const filterCategory = ref('')
const filterStatus = ref('')
const showEditor = ref(false)
const editorId = ref<string | null>(null)
const saving = ref(false)
const uploading = ref(false)

const { uploadFile } = useCos()
const contentInput = ref<HTMLTextAreaElement | null>(null)
const fileInput = ref<HTMLInputElement | null>(null)

const fullPreview = ref(false)

// 草稿自动保存
const DRAFT_KEY = 'devproject-draft'
let draftTimer: ReturnType<typeof setTimeout> | null = null
const draftSavedAt = ref('')

const isAdmin = computed(() => userStore.permissions?.includes('SuperAdmin') ?? false)

const categories = [
  { key: '', label: '全部' },
  { key: 'ai', label: 'AI' },
  { key: 'mod', label: 'Mod' },
  { key: 'game', label: '虚幻游戏' },
  { key: 'world', label: '世界观' },
  { key: 'ip', label: 'IP 动画' },
  { key: 'other', label: '其他' },
]
const statuses = [
  { key: '', label: '全部状态' },
  { key: 'planning', label: '规划中' },
  { key: 'ongoing', label: '进行中' },
  { key: 'done', label: '已完成' },
  { key: 'paused', label: '暂停' },
  { key: 'dead', label: '暴死' },
]

const categoryMap: Record<string, string> = { ai: 'AI', mod: '我的世界 Mod', game: '虚幻游戏', world: '世界观', ip: 'IP 动画', web: '网站', other: '其他' }
const categoryLabel = (k: string) => categoryMap[k] || '其他'
const statusLabel = (s: string) => ({ planning: '规划中', ongoing: '进行中', done: '已完成', paused: '暂停', dead: '暴死' } as Record<string, string>)[s] || s
const formatDate = (v: string) => v ? new Date(v).toLocaleDateString('zh-CN', { year: 'numeric', month: 'long', day: 'numeric' }) : ''

const tree = computed(() => {
  const childrenMap: Record<string, DevProject[]> = {}
  for (const p of projects.value) {
    if (p.parentId) (childrenMap[p.parentId] ||= []).push(p)
  }
  for (const k in childrenMap) childrenMap[k].sort((a, b) => a.sortOrder - b.sortOrder)
  const top = projects.value.filter(p => !p.parentId).sort((a, b) => a.sortOrder - b.sortOrder)
  return top.map(p => ({ project: p, children: childrenMap[p.id] || [] }))
})

const filtered = computed(() => {
  const match = (p: DevProject) => (!filterCategory.value || p.category === filterCategory.value) && (!filterStatus.value || p.status === filterStatus.value)
  return tree.value
    .map(node => {
      const children = node.children.filter(match)
      const showParent = match(node.project) || children.length > 0
      return showParent ? { project: node.project, children } : null
    })
    .filter((x): x is { project: DevProject; children: DevProject[] } => x !== null)
})

function seriesSummary(children: DevProject[]) {
  if (!children.length) return { count: 0, avg: 0, done: 0 }
  const total = children.reduce((s, c) => s + c.progress, 0)
  const done = children.filter(c => c.status === 'done').length
  return { count: children.length, avg: Math.round(total / children.length), done }
}

const emptyForm = (): DevProjectPayload => ({
  name: '', description: '', content: '', category: 'other', status: 'ongoing', progress: 0,
  milestones: [], type: 'dev', references: [], dataPoints: [], logs: [], icon: '🚀', sortOrder: 0, parentId: null
})
const form = ref<DevProjectPayload>(emptyForm())

const previewHtml = computed(() => renderMarkdown(form.value.content || ''))

function saveDraft() {
  try {
    localStorage.setItem(DRAFT_KEY, JSON.stringify({ form: form.value, editorId: editorId.value, at: Date.now() }))
    draftSavedAt.value = new Date().toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })
  } catch { /* ignore */ }
}
function loadDraft(): { form: DevProjectPayload; editorId: string | null } | null {
  try {
    const raw = localStorage.getItem(DRAFT_KEY)
    if (!raw) return null
    const d = JSON.parse(raw)
    return d?.form ? d : null
  } catch { return null }
}
function clearDraft() {
  try { localStorage.removeItem(DRAFT_KEY) } catch { /* ignore */ }
  draftSavedAt.value = ''
}

// 自动保存草稿（防抖）
watch(form, () => {
  if (!showEditor.value) return
  if (draftTimer) clearTimeout(draftTimer)
  draftTimer = setTimeout(saveDraft, 800)
}, { deep: true })

async function load() {
  loading.value = true
  try { projects.value = await devProjectApi.list() } catch { projects.value = [] } finally { loading.value = false }
}

function openEditor(p?: DevProject) {
  editorId.value = p?.id ?? null
  if (p) {
    form.value = { name: p.name, description: p.description || '', content: p.content || '', category: p.category, status: p.status, progress: p.progress, milestones: p.milestones.map(m => ({ ...m })), type: p.type || 'dev', references: (p.references || []).map(r => ({ ...r })), dataPoints: (p.dataPoints || []).map(d => ({ ...d })), logs: (p.logs || []).map(l => ({ ...l })), icon: p.icon, sortOrder: p.sortOrder, parentId: p.parentId || null }
  } else {
    const draft = loadDraft()
    form.value = (draft && !draft.editorId) ? { ...emptyForm(), ...draft.form } : emptyForm()
  }
  draftSavedAt.value = ''
  showEditor.value = true
}

const parentOptions = computed(() => projects.value.filter(x => !x.parentId && x.id !== editorId.value))

function closeEditor() { showEditor.value = false }

async function saveProject() {
  if (!form.value.name.trim()) { alert('请填写报告标题'); return }
  saving.value = true
  try {
    if (editorId.value) await devProjectApi.update(editorId.value, form.value)
    else await devProjectApi.create(form.value)
    clearDraft()
    showEditor.value = false
    await load()
  } catch (e: any) {
    alert(e.friendlyMessage || '保存失败')
  } finally { saving.value = false }
}

function insertAtCursor(text: string, cursorOffset = text.length) {
  const ta = contentInput.value
  const content = form.value.content || ''
  const start = ta?.selectionStart ?? content.length
  const end = ta?.selectionEnd ?? start
  form.value.content = content.slice(0, start) + text + content.slice(end)
  nextTick(() => { if (ta) { ta.focus(); const pos = start + cursorOffset; ta.setSelectionRange(pos, pos) } })
}

function pickImage() { fileInput.value?.click() }

async function onImagePicked(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  if (!file.type.startsWith('image/')) { alert('请选择图片文件'); return }
  uploading.value = true
  try {
    const { url } = await uploadFile(file, 'devproject')
    insertAtCursor(`![${file.name}](${url})`, 0)
  } catch {
    alert('图片上传失败')
  } finally { uploading.value = false }
}

// ===== 研究内容编辑器（参考文献 / 数据 / 日志） =====
const logFileInput = ref<HTMLInputElement | null>(null)
let logImageIndex = -1
const uid = () => Math.random().toString(36).slice(2, 10)
function addReference() { form.value.references.push({ id: uid(), title: '', authors: '', venue: '', year: '', url: '', note: '' }) }
function addDataPoint() { form.value.dataPoints.push({ id: uid(), name: '', value: '', unit: '', note: '' }) }
function addLog() { form.value.logs.push({ id: uid(), date: new Date().toISOString().slice(0, 10), title: '', content: '', images: [] }) }
function pickLogImage(i: number) { logImageIndex = i; logFileInput.value?.click() }
async function onLogImagePicked(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  const target = form.value.logs[logImageIndex]
  if (!target) return
  try {
    const { url } = await uploadFile(file, 'devproject')
    if (!target.images) target.images = []
    target.images.push(url)
  } catch { alert('图片上传失败') }
}

onMounted(load)
</script>

<style scoped>
.report-page { max-width: 860px; margin: 0 auto; padding: 0 0 48px; }
.report-header { display: flex; justify-content: space-between; align-items: flex-end; gap: 16px; flex-wrap: wrap; padding: 24px 0 20px; border-bottom: 2px solid var(--text-primary); }
.report-header h1 { margin: 0; font-size: clamp(1.7rem, 3.4vw, 2.3rem); letter-spacing: -.02em; font-weight: 700; }
.report-header .sub { margin: 8px 0 0; color: var(--text-secondary); font-size: .9rem; line-height: 1.7; }
.report-filters { display: flex; flex-direction: column; gap: 10px; margin: 18px 0 8px; }
.cat-tabs, .status-tabs { display: flex; flex-wrap: wrap; gap: 8px; }
.cat-tab { padding: 6px 14px; border: 1px solid var(--border-line); border-radius: 18px; background: transparent; color: var(--text-secondary); cursor: pointer; font-size: .8rem; }
.cat-tab.active { background: var(--text-primary); color: var(--paper-card); border-color: var(--text-primary); }
.status-tab { padding: 3px 10px; border: 1px solid var(--border-line); border-radius: 8px; background: transparent; color: var(--text-secondary); cursor: pointer; font-size: .74rem; }
.status-tab.active { border-color: var(--accent-color); color: var(--accent-color); }
.report-empty { color: var(--text-secondary); text-align: center; padding: 48px 0; }

.report-list { margin-top: 8px; }
.report-item { display: flex; align-items: flex-start; gap: 18px; padding: 22px 4px; border-bottom: 1px solid var(--border-line); text-decoration: none; color: inherit; transition: padding-left .2s; }
.report-item:hover { padding-left: 10px; }
.report-num { font-size: 1.4rem; font-weight: 200; color: var(--text-secondary); font-variant-numeric: tabular-nums; padding-top: 2px; min-width: 44px; }
.report-main { flex: 1; min-width: 0; }
.report-title-row { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
.report-title { margin: 0; font-size: 1.25rem; font-weight: 650; letter-spacing: -.01em; }
.report-icon { margin-right: 6px; }
.tag { font-size: .7rem; padding: 2px 10px; border-radius: 10px; }
.tag.planning, .tag.paused { background: var(--bg-sub); color: var(--text-secondary); }
.tag.ongoing { background: var(--accent-soft); color: var(--accent-color); }
.tag.done { background: #e6f6ec; color: var(--success); }
.tag.dead { background: #fdecea; color: #c0392b; }
.report-abstract { margin: 8px 0 6px; font-size: .88rem; color: var(--text-secondary); line-height: 1.7; }
.report-meta { display: flex; align-items: center; gap: 8px; font-size: .76rem; color: var(--text-secondary); }
.report-meta .dot { opacity: .5; }
.report-arrow { font-size: 1.2rem; color: var(--text-secondary); padding-top: 4px; transition: transform .2s, color .2s; }
.report-item:hover .report-arrow { transform: translateX(4px); color: var(--accent-color); }

/* 系列分组 */
.series { margin-bottom: 8px; border: 1px solid var(--border-line); border-radius: 10px; overflow: hidden; }
.series-head { display: flex; align-items: center; gap: 12px; padding: 14px 16px; text-decoration: none; color: inherit; background: var(--bg-sub); }
.series-icon { font-size: 1.4rem; }
.series-name { margin: 0; font-size: 1.12rem; font-weight: 650; flex: 1; min-width: 0; overflow-wrap: anywhere; }
.series-summary { display: flex; align-items: center; gap: 8px; flex-shrink: 0; }
.series-summary-bar { width: 60px; height: 6px; background: var(--bg-main); border-radius: 3px; overflow: hidden; }
.series-summary-fill { display: block; height: 100%; background: linear-gradient(90deg, var(--accent-color), var(--success)); border-radius: 3px; }
.series-summary-text { font-size: .72rem; color: var(--text-secondary); white-space: nowrap; }
.series-children { padding: 4px 8px 4px 16px; }
.child-item { padding: 14px 4px; }
.child-item .report-title { font-size: 1.02rem; font-weight: 600; }

/* ===== 编辑器 ===== */
.editor-overlay { position: fixed; inset: 0; z-index: 100; background: rgba(0,0,0,.5); display: flex; align-items: flex-start; justify-content: center; padding: 4vh 20px; }
.editor { width: min(960px, 100%); max-height: 92vh; background: var(--paper-card); border-radius: 14px; display: flex; flex-direction: column; overflow: hidden; box-shadow: 0 24px 60px rgba(0,0,0,.25); }
.editor-head { display: flex; align-items: center; justify-content: space-between; padding: 18px 24px; border-bottom: 1px solid var(--border-line); }
.editor-title { margin: 0; font-size: 1.15rem; font-weight: 650; }
.editor-close { border: none; background: none; font-size: 1.5rem; line-height: 1; color: var(--text-secondary); cursor: pointer; padding: 4px 8px; border-radius: 6px; }
.editor-close:hover { background: var(--bg-sub); color: var(--text-primary); }
.editor-body { flex: 1; overflow-y: auto; padding: 20px 24px; display: grid; gap: 26px; }
.editor-section { display: grid; gap: 14px; }
.editor-section-title { margin: 0; font-size: .95rem; font-weight: 650; color: var(--text-primary); letter-spacing: .01em; }
.editor-section-title small { font-weight: 400; color: var(--text-secondary); margin-left: 6px; }
.field { display: grid; gap: 6px; font-size: .8rem; color: var(--text-secondary); }
.field span { font-weight: 500; }
.field input, .field select { padding: 9px 12px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--bg-main); color: var(--text-primary); font-size: .88rem; font-family: inherit; }
.field input:focus, .field select:focus { outline: none; border-color: var(--accent-color); }
.grid-4 { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 12px; }
/* Markdown 工具栏 + 分栏 */
.md-toolbar { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
.tool-btn { padding: 5px 11px; border: 1px solid var(--border-line); border-radius: 6px; background: var(--bg-main); color: var(--text-primary); cursor: pointer; font-size: .78rem; }
.tool-btn:hover { border-color: var(--accent-color); color: var(--accent-color); }
.tool-btn:disabled { opacity: .5; cursor: default; }
.md-split { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; min-height: 300px; }
.md-input { padding: 14px; border: 1px solid var(--border-line); border-radius: 10px; background: var(--bg-main); color: var(--text-primary); font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: .85rem; line-height: 1.7; resize: vertical; }
.md-input:focus { outline: none; border-color: var(--accent-color); }
.md-preview-wrap { border: 1px solid var(--border-line); border-radius: 10px; overflow: hidden; display: flex; flex-direction: column; }
.md-preview-label { font-size: .7rem; color: var(--text-secondary); padding: 7px 12px; background: var(--bg-sub); border-bottom: 1px solid var(--border-line); letter-spacing: .05em; }
.md-preview { flex: 1; overflow-y: auto; padding: 14px; font-size: .9rem; line-height: 1.8; color: var(--text-primary); overflow-wrap: anywhere; }
.md-preview :deep(p) { margin: 0 0 12px; }
.md-preview :deep(img) { max-width: 100%; border-radius: 8px; display: block; margin: 8px 0; }
.md-preview :deep(h1), .md-preview :deep(h2), .md-preview :deep(h3) { margin: 14px 0 6px; line-height: 1.4; }
.md-preview :deep(ul), .md-preview :deep(ol) { margin: 0 0 12px; padding-left: 22px; }
.md-preview :deep(a) { color: var(--accent-color); }
.md-preview :deep(blockquote) { margin: 0 0 12px; padding: 6px 12px; border-left: 3px solid var(--accent-color); background: var(--bg-sub); color: var(--text-secondary); }
.md-preview :deep(code) { background: var(--bg-sub); padding: 1px 5px; border-radius: 4px; font-size: .85em; }
.md-preview :deep(pre) { background: var(--bg-sub); padding: 10px; border-radius: 8px; overflow-x: auto; }
/* 里程碑编辑 */
.ms-list-edit { display: grid; gap: 8px; }
.ms-edit-row { display: flex; align-items: center; gap: 10px; }
.ms-edit-row input[type="checkbox"] { width: 18px; height: 18px; cursor: pointer; flex-shrink: 0; }
.ms-edit-row input[type="text"] { flex: 1; padding: 8px 12px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--bg-main); color: var(--text-primary); font-size: .85rem; }
.ms-edit-row input[type="text"]:focus { outline: none; border-color: var(--accent-color); }
.ms-empty { margin: 0; font-size: .82rem; color: var(--text-secondary); }
.editor-foot { display: flex; align-items: center; justify-content: flex-end; gap: 12px; padding: 14px 24px; border-top: 1px solid var(--border-line); }
.editor-hint { margin-right: auto; font-size: .78rem; color: var(--text-secondary); }
.btn { padding: 8px 18px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); cursor: pointer; font-size: .84rem; }
.btn.primary { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); }
.btn:disabled { opacity: .5; cursor: default; }
.mini-btn { padding: 3px 9px; border: 1px solid var(--border-line); border-radius: 6px; background: var(--paper-card); color: var(--text-secondary); cursor: pointer; font-size: .74rem; }
.mini-btn.danger { color: var(--danger); }
.preview-btn { margin-left: auto; }
.clear-draft { border: none; background: none; color: var(--accent-color); cursor: pointer; font-size: .78rem; text-decoration: underline; padding: 0; margin-left: 8px; }

/* 全屏预览 */
.preview-overlay { position: fixed; inset: 0; z-index: 200; background: var(--bg-main); display: flex; flex-direction: column; }
.preview-full { display: flex; flex-direction: column; height: 100%; }
.preview-head { display: flex; align-items: center; justify-content: space-between; padding: 14px 24px; border-bottom: 1px solid var(--border-line); background: var(--paper-card); }
.preview-title { font-weight: 650; font-size: 1rem; }
.preview-body { flex: 1; overflow-y: auto; max-width: 820px; margin: 0 auto; width: 100%; padding: 32px 24px 80px; font-size: 1rem; line-height: 1.95; color: var(--text-primary); overflow-wrap: anywhere; }
.preview-body :deep(p) { margin: 0 0 18px; }
.preview-body :deep(h1) { font-size: 1.6rem; margin: 34px 0 14px; }
.preview-body :deep(h2) { font-size: 1.35rem; margin: 30px 0 12px; padding-bottom: 8px; border-bottom: 1px solid var(--border-line); }
.preview-body :deep(h3) { font-size: 1.15rem; margin: 24px 0 10px; }
.preview-body :deep(img) { max-width: 100%; border-radius: 10px; display: block; margin: 18px auto; }
.preview-body :deep(ul), .preview-body :deep(ol) { margin: 0 0 18px; padding-left: 26px; }
.preview-body :deep(a) { color: var(--accent-color); }
.preview-body :deep(blockquote) { margin: 0 0 18px; padding: 12px 18px; border-left: 3px solid var(--accent-color); background: var(--bg-sub); color: var(--text-secondary); }
.preview-body :deep(code) { background: var(--bg-sub); padding: 2px 6px; border-radius: 4px; font-size: .88em; }
.preview-body :deep(pre) { background: var(--bg-sub); padding: 14px; border-radius: 10px; overflow-x: auto; }
.preview-body :deep(table) { border-collapse: collapse; width: 100%; margin: 0 0 18px; }
.preview-body :deep(th), .preview-body :deep(td) { border: 1px solid var(--border-line); padding: 8px 12px; text-align: left; }
.preview-body :deep(th) { background: var(--bg-sub); }

@media (max-width: 720px) {
  .grid-4 { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .md-split { grid-template-columns: 1fr; }
  .editor { max-height: 96vh; }
}
@media (max-width: 560px) { .report-item { gap: 12px; } .report-num { min-width: 32px; font-size: 1.1rem; } }
/* 研究内容编辑器 */
.row-list { display: grid; gap: 12px; margin-bottom: 12px; }
.row-card { display: grid; gap: 8px; padding: 12px; border: 1px solid var(--border-line); border-radius: 10px; background: var(--bg-main); }
.row-card input, .row-card textarea { padding: 7px 10px; border: 1px solid var(--border-line); border-radius: 6px; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: .84rem; }
.row-card textarea { resize: vertical; }
.row-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(120px, 1fr)); gap: 8px; }
.row-grid .span-2 { grid-column: span 2; }
.row-card > .mini-btn { justify-self: start; }
.log-img-row { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
.log-img-row img { width: 72px; height: 48px; object-fit: cover; border-radius: 6px; border: 1px solid var(--border-line); }
</style>
