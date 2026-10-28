<template>
  <div class="project-detail workbench-skin">
    <!-- ========== 顶部 ========== -->
    <ProjectHeader :project="projectError ? null : project" :card-count="cards.length" :is-owner="isOwner" :can-organize="userStore.isLoggedIn" @back="goBack" @create="openCreateDialog" @organize="openAiOrganize" @import="openAiImport" @transfer="openTransfer" />

    <WorldTransfer v-if="transferMode && project && !projectError" :project="project" :mode="transferMode" :can-edit="isOwner" @close="transferMode = null" />
    <!-- ========== 主体 ========== -->
    <div class="layout">
      <!-- 左侧栏 -->
      <ProjectCardNavigation v-if="!projectError" v-model:search="searchQuery" :cards="cards" :loading="store.loadingCards"
        :selected-card-id="selectedCardId" :view="view" @select="selectCard" @overview="showOverview" @graph="switchToGraph" @timeline="switchToTimeline" />

      <!-- 主区域 -->
      <main class="main" :aria-busy="store.loadingCards || detailLoading">
        <div v-if="projectError" class="load-state" role="alert">
          <p>{{ projectError }}</p>
          <RouterLink v-if="!userStore.isLoggedIn" :to="{ path: '/LoginRegister', query: { redirect: route.fullPath } }">登录自己的账号</RouterLink>
          <button class="btn" @click="loadRoute">重新加载</button>
          <RouterLink to="/world?tab=public">浏览公开世界</RouterLink>
        </div>
        <div v-else-if="!project" class="load-state" role="status">正在加载世界观…</div>
        <!-- 图谱视图 -->
        <div v-else-if="view === 'graph'" class="graph-view">
          <RelationGraph
            ref="graphRef"
            :project-id="projectId"
            :positions="graphPositions"
            @select="selectCard"
            @update:positions="saveGraphPositions"
          />
        </div>

        <!-- 时间线视图 -->
        <div v-else-if="view === 'timeline'" class="timeline-view-wrap">
          <TimelineView :project-id="projectId" :can-edit="isOwner" @select="selectCard" />
        </div>

        <!-- 卡片视图（含导航历史） -->
        <div v-else class="card-view" :class="{ 'is-editing': isOwner && (isCreating || editing) }">
          <div v-if="selectedCardId && !isCreating && !editing && (isOwner || canGoBack || canGoForward)" class="history-bar">
            <div class="history-nav">
              <button class="nav-btn" :disabled="!canGoBack" title="后退" @click="backCard">←</button>
              <button class="nav-btn" :disabled="!canGoForward" title="前进" @click="forwardCard">→</button>
            </div>
            <nav v-if="historyCrumbs.length > 1" class="crumbs">
              <template v-for="(c, i) in historyCrumbs" :key="c.id + i">
                <button class="crumb" :class="{ current: i === historyIndex }" @click="jumpTo(i)">{{ c.title }}</button>
                <span v-if="i < historyCrumbs.length - 1" class="crumb-sep">/</span>
              </template>
            </nav>
            <span v-else class="history-spacer"></span>
            <button v-if="isOwner && selectedCardDetail && !editing" class="edit-entry" @click="openEditor">✎ 编辑条目</button>
          </div>

          <!-- 编辑器视图 -->
          <div
            v-if="isOwner && (isCreating || editing)"
            class="editor-view"
          >
          <div class="editor-topbar">
            <button class="editor-back" @click="leaveEditor">← {{ isCreating ? '返回概览' : '返回阅读' }}</button>
            <span class="editor-title">
              {{ isCreating ? '新建条目' : selectedCard?.title }}
            </span>
          </div>
          <div class="editor-body">
            <CardEditor inline
              ref="cardEditorRef"
              :key="selectedCardId || 'new'"
              :project-id="projectId"
              :card-data="isCreating ? null : selectedCard"
              @saved="onCardChanged"
              @deleted="onCardChanged"
            />
          </div>
        </div>

        <!-- 只读详情（含右侧关联面板） -->
        <div v-else-if="detailLoading" class="load-state" role="status">正在加载条目…</div>
        <div v-else-if="detailError" class="load-state" role="alert">条目不存在、已取消公开，或暂时无法加载。<button class="btn" @click="loadRoute">重新加载</button><button class="btn" @click="showOverview">返回世界概览</button></div>
        <div v-else-if="selectedCardDetail" class="doc-layout">
          <ProjectCardDocument :key="selectedCardDetail.id"
            :card="selectedCardDetail" :project-id="projectId" @select="selectCard" />
          <aside v-if="outRelations.length || inRelations.length" class="doc-side-panel">
            <h3 class="panel-title">关联</h3>
            <template v-if="outRelations.length">
              <div class="panel-group">
                <span class="panel-label">关联到</span>
                <button v-for="r in outRelations" :key="r.id" class="panel-item" @click="selectCard(r.targetCardId)">
                  <span class="panel-rel-type">{{ r.relationType }}</span>
                  <span class="panel-rel-title">{{ r.targetCardTitle || '未命名' }}</span>
                </button>
              </div>
            </template>
            <template v-if="inRelations.length">
              <div class="panel-group">
                <span class="panel-label">被关联</span>
                <button v-for="r in inRelations" :key="r.id" class="panel-item" @click="selectCard(r.sourceCardId)">
                  <span class="panel-rel-type">{{ r.relationType }}</span>
                  <span class="panel-rel-title">{{ r.sourceCardTitle || '未命名' }}</span>
                </button>
              </div>
            </template>
          </aside>
        </div>

        <section v-else-if="project && !projectError" class="world-overview">
          <div class="overview-hero">
            <img v-if="project.coverImage" :src="project.coverImage" :alt="project.name" class="hero-cover" />
            <div v-else class="hero-fallback">
              <span class="hero-fallback-icon">🌌</span>
              <span class="hero-fallback-text">这个宇宙还没有封面</span>
            </div>
            <button v-if="isOwner" class="hero-cover-btn" @click="pickCover">{{ project.coverImage ? '更换封面' : '添加封面' }}</button>
            <input ref="coverInput" type="file" accept="image/*" hidden @change="onCoverPicked" />
          </div>
          <p class="overview-intro">{{ project.description || '这个世界还没有简介，先从下面的设定开始了解吧。' }}</p>
          <div v-if="!cards.length" class="overview-start">
            <h3>{{ isOwner ? '先写下一个设定' : '作者还在构思这个世界' }}</h3>
            <p>{{ isOwner ? '一个人物、一处地点，或者故事背景都可以。不必一次填完所有信息。' : '有了公开的条目，就会显示在这里。' }}</p>
            <button v-if="isOwner" class="btn-primary" @click="openCreateDialog">创建第一个条目</button>
          </div>
          <div v-else class="overview-stats">
            <button v-for="group in overviewGroups" :key="group.type" class="stat-chip" @click="selectCard(group.cards[0].id)">
              <span class="stat-label">{{ group.label }}</span>
              <span class="stat-count">{{ group.cards.length }}</span>
            </button>
          </div>

          <!-- 本世界的小说 -->
          <section class="novel-block">
            <div class="novel-block-head">
              <h3>📖 小说</h3>
              <button v-if="isOwner" class="novel-add" @click="createNovel">＋ 写本世界的小说</button>
            </div>
            <ul v-if="projectNovels.length" class="novel-list">
              <li v-for="nv in projectNovels" :key="nv.id">
                <RouterLink :to="`/novels/${nv.id}`" class="novel-row">
                  <span class="nv-cover">
                    <img v-if="nv.coverUrl" :src="nv.coverUrl" :alt="nv.title" loading="lazy" />
                    <span v-else>{{ nv.title.slice(0, 1) }}</span>
                  </span>
                  <span class="nv-main">
                    <span class="nv-title">{{ nv.title }}</span>
                    <span class="nv-meta">{{ nv.authorName }} · {{ nv.chapterCount }} 章 · {{ nv.wordCount }} 字</span>
                  </span>
                </RouterLink>
              </li>
            </ul>
            <p v-else class="novel-empty">还没有这个宇宙的故事。</p>
          </section>
        </section>
        </div>
      </main>
    </div>

    <!-- AI 整理弹窗 -->
    <div v-if="showAiModal" class="ai-modal-mask" @click.self="showAiModal = false">
      <div class="ai-modal">
        <header class="ai-modal-head">
          <h3>🤖 AI 世界观整理</h3>
          <button class="ai-close" @click="showAiModal = false">×</button>
        </header>
        <div class="ai-modal-body">
          <p v-if="aiOrganizing" class="ai-state">正在用你的 AI 整理这个世界观，可能需要十几秒…</p>
          <p v-else-if="aiError" class="ai-error">{{ aiError }}<br /><small>请先在「个人中心 → AI 助手」里填写你的接口地址和 Key。</small></p>
          <div v-else-if="aiResult" class="ai-result" v-html="renderMarkdown(aiResult)"></div>
        </div>
        <footer class="ai-modal-foot">
          <button class="btn" @click="showAiModal = false">关闭</button>
        </footer>
      </div>
    </div>

    <!-- AI 导入弹窗 -->
    <div v-if="showImportModal" class="ai-modal-mask" @click.self="showImportModal = false">
      <div class="ai-modal ai-import-modal">
        <header class="ai-modal-head">
          <h3>⬆ AI 导入设定</h3>
          <button class="ai-close" @click="showImportModal = false">×</button>
        </header>
        <div class="ai-modal-body">
          <template v-if="!importCards.length">
            <label class="import-field">
              <span>粘贴你写好的设定文字</span>
              <textarea v-model="importText" rows="9" placeholder="例如：青云宗，掌门叫林风，擅御剑术；宗门位于云海之上，有三座主峰……"></textarea>
            </label>
            <p v-if="importError" class="ai-error">{{ importError }}<br /><small>请先在「个人中心 → AI 助手」配置你的 AI。</small></p>
          </template>
          <template v-else>
            <p class="import-tip">解析出 {{ importCards.length }} 张卡片，确认后创建：</p>
            <ul class="import-preview">
              <li v-for="(c, i) in importCards" :key="i">
                <strong>{{ c.title }}</strong>
                <span class="import-type">{{ c.type }}</span>
                <p v-if="c.description">{{ c.description }}</p>
              </li>
            </ul>
            <p v-if="importCreated" class="import-done">✅ 已创建 {{ importCreated }} 张卡片</p>
            <ul v-if="importErrors.length" class="import-errors"><li v-for="(e, i) in importErrors" :key="i">{{ e }}</li></ul>
          </template>
        </div>
        <footer class="ai-modal-foot">
          <template v-if="!importCards.length">
            <button class="btn" @click="showImportModal = false">取消</button>
            <button class="btn primary" :disabled="importParsing" @click="parseImport">{{ importParsing ? '解析中…' : '解析' }}</button>
          </template>
          <template v-else>
            <button class="btn" @click="importCards = []; importError = ''">返回重贴</button>
            <button class="btn primary" :disabled="importCreating || importCreated > 0" @click="confirmImport">{{ importCreating ? '创建中…' : importCreated > 0 ? '已创建' : '确认创建' }}</button>
          </template>
        </footer>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onScopeDispose, defineAsyncComponent } from 'vue'
import { useRouter, useRoute, onBeforeRouteLeave, onBeforeRouteUpdate } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useWorldStore } from '@/stores/world'
import { useUserStore } from '@/stores/user'
import type { CardDetail } from '@/api/types/world'
import { aiConfigApi, type ImportCardItem } from '@/api/aiConfig'
import { groupWorldCards } from '@/utils/worldPresentation'
import { worldNovelApi, type NovelBrief } from '@/api/worldNovel'
import { renderMarkdown } from '@/utils/markdown'
import { useCos } from '@/composables/useCos'
import request from '@/utils/request'
import ProjectHeader from './世界观组件/ProjectHeader.vue'
import ProjectCardNavigation from './世界观组件/ProjectCardNavigation.vue'
import ProjectCardDocument from './世界观组件/ProjectCardDocument.vue'

const WorldTransfer = defineAsyncComponent(() => import('./世界观组件/WorldTransfer.vue'))
const transferMode = ref<'import' | 'export' | null>(null)
async function openTransfer(mode: 'import' | 'export') {
  if (await mayLeaveEditor()) transferMode.value = mode
}
const RelationGraph = defineAsyncComponent(() => import('./世界观组件/RelationGraph.vue'))
const TimelineView = defineAsyncComponent(() => import('./世界观组件/TimelineView.vue'))
const CardEditor = defineAsyncComponent(() => import('./世界观组件/CardEditor.vue'))

const router = useRouter()
const route = useRoute()
const store = useWorldStore()
const userStore = useUserStore()
const projectId = computed(() => String(route.params.id || route.params.projectId || ''))
const project = computed(() => store.currentProject)
const cards = computed(() => store.cards)
const isOwner = computed(() => Boolean(userStore.userId && userStore.userId === project.value?.ownerId))
const searchQuery = ref('')
const view = ref<'card' | 'graph' | 'timeline'>('card')
const graphRef = ref<InstanceType<typeof RelationGraph> | null>(null)
const selectedCardId = ref<string | null>(null)
const selectedCardDetail = ref<CardDetail | null>(null)
const isCreating = computed(() => selectedCardId.value === 'new')
const selectedCard = computed(() => cards.value.find(card => card.id === selectedCardId.value) ?? null)
let selectionVersion = 0
const editing = ref(false)
const cardEditorRef = ref<InstanceType<typeof CardEditor> | null>(null)
const mayLeaveEditor = () => cardEditorRef.value?.requestLeave() ?? Promise.resolve(true)
onBeforeRouteLeave(mayLeaveEditor)
onBeforeRouteUpdate(mayLeaveEditor)
const detailLoading = ref(false)
const detailError = ref(false)
const projectError = ref('')
const overviewGroups = computed(() => groupWorldCards(cards.value))

// ===== AI 一键整理 =====
const aiOrganizing = ref(false)
const aiResult = ref('')
const aiError = ref('')
const showAiModal = ref(false)

async function openAiOrganize() {
  showAiModal.value = true
  aiOrganizing.value = true
  aiError.value = ''
  aiResult.value = ''
  try {
    const res = await aiConfigApi.organizeWorld(projectId.value)
    aiResult.value = res.result
  } catch (e: any) {
    aiError.value = e.friendlyMessage || 'AI 整理失败，请先登录并在个人中心配置你的 AI'
  } finally {
    aiOrganizing.value = false
  }
}

// ===== AI 一键导入 =====
const showImportModal = ref(false)
const importText = ref('')
const importParsing = ref(false)
const importCards = ref<ImportCardItem[]>([])
const importError = ref('')
const importCreating = ref(false)
const importCreated = ref(0)
const importErrors = ref<string[]>([])

async function openAiImport() {
  showImportModal.value = true
  importText.value = ''
  importCards.value = []
  importError.value = ''
  importCreated.value = 0
  importErrors.value = []
}

async function parseImport() {
  if (!importText.value.trim()) { importError.value = '请先粘贴设定文字'; return }
  importParsing.value = true
  importError.value = ''
  importCards.value = []
  try {
    const res = await aiConfigApi.importWorld(projectId.value, importText.value)
    importCards.value = res.cards || []
  } catch (e: any) {
    importError.value = e.friendlyMessage || '解析失败'
  } finally { importParsing.value = false }
}

async function confirmImport() {
  if (!importCards.value.length) return
  importCreating.value = true
  importCreated.value = 0
  importErrors.value = []
  try {
    const res = await aiConfigApi.confirmImport(projectId.value, importCards.value)
    importCreated.value = res.created
    importErrors.value = res.errors || []
    if (res.created > 0) await loadData()
  } catch (e: any) {
    importErrors.value = [e.friendlyMessage || '创建失败']
  } finally { importCreating.value = false }
}

// ===== 卡片导航历史 =====
const history = ref<string[]>([])
const historyIndex = ref(-1)
let navigatingFromHistory = false
const canGoBack = computed(() => historyIndex.value > 0)
const canGoForward = computed(() => historyIndex.value < history.value.length - 1)
const historyCrumbs = computed(() =>
  history.value.slice(0, historyIndex.value + 1).map(id => ({
    id,
    title: cards.value.find(c => c.id === id)?.title ?? '…'
  }))
)

// 当前卡片的关系（出度/入度）
const outRelations = computed(() => selectedCardDetail.value?.outRelations || [])
const inRelations = computed(() => selectedCardDetail.value?.inRelations || [])

async function selectCard(id: string) {
  if (route.params.cardId === id) {
    if (await mayLeaveEditor()) {
      if (route.query.edit) await router.replace({ query: { ...route.query, edit: undefined } })
      else await loadCard(id)
    }
    return
  }
  await router.push({ name: 'CardDetail', params: { projectId: projectId.value, cardId: id } })
}
async function loadCard(id: string) {
  // 非历史导航时，记录到历史栈（并截断 forward）
  if (!navigatingFromHistory && selectedCardId.value !== id) {
    const next = history.value.slice(0, historyIndex.value + 1)
    next.push(id)
    history.value = next
    historyIndex.value = next.length - 1
  }
  navigatingFromHistory = false

  const version = ++selectionVersion
  editing.value = false
  detailLoading.value = true
  detailError.value = false
  view.value = 'card'
  selectedCardId.value = id
  selectedCardDetail.value = null
  try {
    const detail = await store.fetchCardDetail(projectId.value, id, true)
    if (version === selectionVersion) selectedCardDetail.value = detail
  } catch {
    if (version === selectionVersion) detailError.value = true
  } finally { if (version === selectionVersion) detailLoading.value = false }
}
function backCard() {
  if (!canGoBack.value) return
  navigatingFromHistory = true
  historyIndex.value--
  void selectCard(history.value[historyIndex.value])
}
function forwardCard() {
  if (!canGoForward.value) return
  navigatingFromHistory = true
  historyIndex.value++
  void selectCard(history.value[historyIndex.value])
}
function jumpTo(index: number) {
  if (index === historyIndex.value) return
  navigatingFromHistory = true
  historyIndex.value = index
  void selectCard(history.value[index])
}
function closePanel() {
  selectionVersion++
  editing.value = false
  detailError.value = false
  detailLoading.value = false
  selectedCardId.value = null
  selectedCardDetail.value = null
  history.value = []
  historyIndex.value = -1
}
async function showOverview() {
  if (!await mayLeaveEditor()) return
  closePanel(); view.value = 'card'
  if (route.name !== 'ProjectDetail' || route.query.edit || route.query.view) void router.push({ name: 'ProjectDetail', params: { id: projectId.value } })
}
function openEditor() {
  void router.replace({ query: { ...route.query, edit: selectedCardId.value } })
}
async function leaveEditor() {
  if (!await mayLeaveEditor()) return
  if (isCreating.value) await showOverview()
  else if (selectedCardId.value) {
    if (route.query.edit) await router.replace({ query: { ...route.query, edit: undefined } })
    else await loadCard(selectedCardId.value)
  }
}
async function openCreateDialog() {
  if (!await mayLeaveEditor()) return
  await router.push({ name: 'ProjectDetail', params: { id: projectId.value }, query: { edit: 'new' } })
}

// ===== 本世界的小说 =====
const projectNovels = ref<NovelBrief[]>([])
async function loadNovels() {
  if (!projectId.value) return
  try { projectNovels.value = await worldNovelApi.list({ projectId: projectId.value }) }
  catch { projectNovels.value = [] }
}
function createNovel() {
  void router.push({ path: '/novels', query: { create: '1', project: projectId.value } })
}

// ===== 世界观封面 =====
const { uploadFile } = useCos()
const coverInput = ref<HTMLInputElement | null>(null)
function pickCover() { coverInput.value?.click() }
async function onCoverPicked(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  try {
    const { url } = await uploadFile(file, 'world-covers')
    await store.updateProject(projectId.value, { coverImage: url })
    ElMessage.success('封面已更新')
    await loadData()
  } catch { ElMessage.error('封面上传失败') }
}

// ===== 关系图谱节点位置（持久化） =====
const graphPositions = computed<Record<string, { x: number; y: number }>>(() => {
  try { return JSON.parse(project.value?.graphPositions || '{}') } catch { return {} }
})
let graphSaveTimer: ReturnType<typeof setTimeout> | null = null
function saveGraphPositions(p: Record<string, { x: number; y: number }>) {
  if (!isOwner.value) return
  if (graphSaveTimer) clearTimeout(graphSaveTimer)
  graphSaveTimer = setTimeout(() => {
    // 轻量保存：只写坐标，不触发项目重取/重渲染（避免节点弹回、视野跳走）
    void request.put(`/world/projects/${projectId.value}/graph-positions`, { positions: JSON.stringify(p) }, { silentError: true })
  }, 600)
}
async function switchToGraph() {
  await router.push({ name: 'ProjectDetail', params: { id: projectId.value }, query: { view: 'graph' } })
}
async function switchToTimeline() {
  await router.push({ name: 'ProjectDetail', params: { id: projectId.value }, query: { view: 'timeline' } })
}
async function loadData() {
  const id = projectId.value
  const token = userStore.token
  projectError.value = ''
  try { await store.fetchCards(id, true) }
  catch (error: any) {
    if (id === projectId.value && token === userStore.token) projectError.value = [403, 404].includes(error?.response?.status)
      ? '这个世界未公开、已删除，或当前账号无权查看。'
      : '世界观暂时无法加载，请稍后重试。'
  }
}
async function onCardChanged(savedId?: string) {
  closePanel()
  await loadData()
  if (savedId && cards.value.some(card => card.id === savedId)) await selectCard(savedId)
  graphRef.value?.refresh()
}
const goBack = () => router.push(isOwner.value ? '/world' : '/world?tab=public')
let routeVersion = 0
let loadedProject = ''
let loadedToken = ''
async function loadRoute() {
  const version = ++routeVersion
  const id = projectId.value
  const token = userStore.token
  const reloadProject = loadedProject !== id || loadedToken !== token || !project.value || Boolean(projectError.value)
  selectionVersion++
  selectedCardDetail.value = null
  editing.value = false
  if (reloadProject) {
    closePanel(); searchQuery.value = ''; view.value = 'card'
    await loadData()
    if (version !== routeVersion || projectError.value) return
    void loadNovels()
    loadedProject = id; loadedToken = token
  }
  if (route.query.edit === 'new') {
    closePanel(); view.value = 'card'
    if (isOwner.value) selectedCardId.value = 'new'
    return
  }
  const cardId = String(route.params.cardId || route.query.edit || '')
  if (cardId) {
    await loadCard(cardId)
    if (version === routeVersion && isOwner.value && route.query.edit === cardId && selectedCardDetail.value) editing.value = true
  } else {
    closePanel()
    view.value = route.query.view === 'graph' || route.query.view === 'timeline' ? route.query.view : 'card'
  }
}
watch([projectId, () => route.params.cardId, () => route.query.edit, () => route.query.view, () => userStore.token], loadRoute, { immediate: true })
onScopeDispose(() => { selectionVersion++; routeVersion++ })
</script>

<style scoped>
.world-overview { width: 100%; max-width: 1100px; padding: clamp(20px, 4vw, 40px); margin: 0 auto; }
.overview-hero { position: relative; border-radius: 16px; overflow: hidden; aspect-ratio: 21 / 7; background: var(--bg-sub); margin-bottom: 24px; }
.hero-cover { width: 100%; height: 100%; object-fit: cover; display: block; }
.hero-fallback { width: 100%; height: 100%; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 10px; color: var(--accent-color); background: linear-gradient(135deg, var(--accent-soft), var(--paper-card) 70%); }
.hero-fallback-icon { font-size: clamp(32px, 5vw, 56px); line-height: 1; }
.hero-fallback-text { font-size: 13px; color: var(--text-secondary); }
.hero-cover-btn { position: absolute; right: 14px; bottom: 14px; padding: 7px 14px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); cursor: pointer; font-size: 13px; }
.overview-intro { max-width: 70ch; white-space: pre-wrap; overflow-wrap: anywhere; line-height: 1.9; font-size: 15px; color: var(--text-secondary); margin: 0 0 28px; }
.overview-start { padding: 28px; border: 1px dashed var(--border-line); border-radius: 12px; }.overview-start p { color: var(--text-secondary); line-height: 1.8; }.overview-start button { margin-top: 12px; }
.overview-stats { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 4px; }
.novel-block { margin-top: 34px; padding-top: 20px; border-top: 1px dashed var(--border-line); }
.novel-block-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 12px; }
.novel-block-head h3 { margin: 0; font-size: 1.02rem; }
.novel-add { padding: 6px 14px; border: 1px solid var(--accent-color); border-radius: 8px; background: transparent; color: var(--accent-color); cursor: pointer; font-size: .8rem; }
.novel-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.novel-row { display: flex; align-items: center; gap: 12px; padding: 10px 12px; border: 1px solid var(--border-line); border-radius: 10px; background: var(--paper-card); text-decoration: none; color: inherit; }
.novel-row:hover { border-color: var(--accent-color); }
.nv-cover { width: 38px; height: 50px; flex-shrink: 0; border-radius: 6px; overflow: hidden; background: var(--accent-soft); display: grid; place-items: center; color: var(--accent-color); font-weight: 700; }
.nv-cover img { width: 100%; height: 100%; object-fit: cover; }
.nv-main { min-width: 0; display: grid; gap: 4px; }
.nv-title { font-size: .9rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.nv-meta { font-size: .72rem; color: var(--text-secondary); }
.novel-empty { margin: 0; padding: 18px 0; color: var(--text-secondary); font-size: .84rem; }
.stat-chip { display: inline-flex; align-items: baseline; gap: 8px; padding: 9px 18px; border: 1px solid var(--border-line); border-radius: 999px; background: var(--paper-card); color: var(--text-primary); cursor: pointer; font: inherit; transition: border-color .15s, transform .15s; }
.stat-chip:hover { border-color: var(--accent-color); transform: translateY(-1px); }
.stat-label { font-size: 14px; }
.stat-count { font-size: 12px; font-weight: 600; color: var(--accent-color); }
.edit-entry { flex-shrink: 0; white-space: nowrap; padding: 7px 14px; border: 1px solid var(--border-line); border-radius: 6px; background: var(--paper-card); color: var(--accent-color); cursor: pointer; font-size: 13px; }
.edit-entry:hover { border-color: var(--accent-color); background: var(--accent-soft); }.load-state { padding: 36px; color: var(--text-secondary); display: flex; gap: 16px; align-items: center; flex-wrap: wrap; }.load-state button { border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); padding: 8px 14px; cursor: pointer; }
button:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 2px; }
.load-state a { color: var(--accent-color); }.load-state p { flex-basis: 100%; margin: 0; line-height: 1.8; }

* { box-sizing: border-box; }
.project-detail {
  height: 100%;
  min-height: 0;
  border: 1px solid var(--border-line);
  border-radius: var(--radius-md);
  display: flex;
  flex-direction: column;
  background: var(--bg-main);
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  color: var(--text-primary);
  overflow: hidden;
}
.layout {
  flex: 1;
  display: flex;
  min-height: 0;
  overflow: hidden;
}
.main {
  flex: 1;
  min-width: 0;
  overflow-y: auto;
  background: var(--bg-main);
}
.card-view {
  min-height: 100%;
  display: flex;
  flex-direction: column;
}
.history-bar {
  position: sticky;
  top: 0;
  z-index: 5;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 16px;
  background: var(--bg-main);
  border-bottom: 1px solid var(--border-line);
}
.history-nav { display: flex; gap: 6px; flex-shrink: 0; }
.history-spacer { flex: 1; }
.nav-btn {
  width: 28px;
  height: 28px;
  display: grid;
  place-items: center;
  border: 1px solid var(--border-line);
  background: var(--paper-card);
  color: var(--text-primary);
  border-radius: 6px;
  cursor: pointer;
  font-size: 14px;
  flex-shrink: 0;
}
.nav-btn:disabled { opacity: 0.35; cursor: default; }
.crumbs {
  display: flex;
  align-items: center;
  gap: 4px;
  flex: 1;
  min-width: 0;
  overflow-x: auto;
  white-space: nowrap;
  scrollbar-width: none;
}
.crumbs::-webkit-scrollbar { display: none; }
.crumb {
  border: none;
  background: none;
  color: var(--text-secondary);
  cursor: pointer;
  font-size: 13px;
  padding: 2px 4px;
  border-radius: 4px;
}
.crumb:hover { background: var(--bg-sub); color: var(--text-primary); }
.crumb.current { color: var(--text-primary); font-weight: 600; }
.crumb-sep { color: var(--text-secondary); font-size: 12px; }
.graph-view {
  height: 100%;
  padding: 20px;
  background: var(--paper-card);
}
.timeline-view-wrap {
  min-height: 100%;
  background: var(--paper-card);
}
.doc-layout {
  min-height: 100%;
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  align-items: start;
}
.doc-side-panel {
  max-width: 720px;
  margin: 0 auto;
  padding: 24px clamp(20px, 4vw, 40px) 32px;
  border-top: 1px dashed var(--border-line);
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(100%, 220px), 1fr));
  gap: 12px 24px;
}
@media (min-width: 1120px) {
  .doc-layout { grid-template-columns: minmax(0, 1fr) 280px; }
  .doc-side-panel {
    max-width: none;
    margin: 0;
    padding: 28px 22px;
    border-top: none;
    border-left: 1px solid var(--border-line);
    grid-template-columns: 1fr;
    align-content: start;
    align-self: start;
    position: sticky;
    top: 0;
    max-height: calc(100dvh - 140px);
    overflow-y: auto;
  }
}
.panel-title { grid-column: 1 / -1; margin: 0; font-size: 14px; font-weight: 600; color: var(--text-secondary); }
.panel-group { margin-bottom: 20px; }
.panel-label { display: block; font-size: 11px; color: var(--text-secondary); margin-bottom: 6px; }
.panel-item {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  padding: 6px 8px;
  border: none;
  background: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 13px;
  color: var(--text-primary);
  text-align: left;
}
.panel-item:hover { background: var(--bg-sub); }
.panel-rel-type {
  font-size: 10px;
  color: var(--accent-color);
  background: var(--accent-soft);
  padding: 1px 6px;
  border-radius: 4px;
  flex-shrink: 0;
  max-width: 72px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.panel-rel-title { flex: 1; min-width: 0; overflow-wrap: anywhere; line-height: 1.7; }
.card-view.is-editing { height: 100%; min-height: 0; }
.editor-view {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  background: var(--paper-card);
}
.editor-topbar {
  flex-shrink: 0;
  padding: 12px 32px;
  border-bottom: 1px solid var(--border-line);
  display: flex;
  align-items: center;
  gap: 16px;
}
.editor-back {
  background: none;
  border: none;
  color: var(--text-secondary);
  font-size: 13px;
  cursor: pointer;
  padding: 4px 0;
}
.editor-back:hover { color: var(--text-primary); }
.editor-title {
  font-size: 13px;
  color: var(--text-secondary);
}
.editor-body {
  flex: 1;
  overflow-y: auto;
  padding: 24px clamp(16px, 3vw, 40px) 0;
}
.editor-body :deep(.card-editor-inline) {
  max-width: 960px;
  margin: 0 auto;
}
.empty-state {
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 16px;
  color: var(--text-secondary);
  font-size: 14px;
}
.empty-state p {
  margin: 0;
}
.btn-primary {
  padding: 8px 20px;
  background: var(--accent-color);
  color: var(--on-accent);
  border: none;
  border-radius: 6px;
  font-size: 13px;
  cursor: pointer;
  transition: background 0.15s;
}
.btn-primary:hover {
  background: var(--text-primary);
}
.main::-webkit-scrollbar,
.editor-body::-webkit-scrollbar {
  width: 6px;
}
.main::-webkit-scrollbar-track,
.editor-body::-webkit-scrollbar-track {
  background: transparent;
}
.main::-webkit-scrollbar-thumb,
.editor-body::-webkit-scrollbar-thumb {
  background: var(--bg-sub);
  border-radius: 3px;
}
.main::-webkit-scrollbar-thumb:hover,
.editor-body::-webkit-scrollbar-thumb:hover {
  background: var(--bg-sub);
}
@media (max-width: 820px) {
  .layout { flex-direction: column; }
  .card-view.is-editing { height: auto; }
  .project-detail { height: auto; min-height: 0; overflow: visible; }
  .layout { overflow: visible; }
  .main { overflow: visible; }
  .graph-view { height: auto; min-height: 65dvh; }
  .editor-body { overflow: visible; padding: 20px 16px; }
}

/* ===== AI 整理弹窗 ===== */
.ai-modal-mask { position: fixed; inset: 0; z-index: 200; background: rgba(0,0,0,.5); display: flex; align-items: center; justify-content: center; padding: 20px; }
.ai-modal { width: min(760px, 100%); max-height: 86vh; display: flex; flex-direction: column; background: var(--paper-card); border-radius: 14px; overflow: hidden; }
.ai-modal-head { display: flex; align-items: center; justify-content: space-between; padding: 16px 22px; border-bottom: 1px solid var(--border-line); }
.ai-modal-head h3 { margin: 0; font-size: 1.05rem; }
.ai-close { border: none; background: none; font-size: 1.4rem; line-height: 1; color: var(--text-secondary); cursor: pointer; }
.ai-modal-body { flex: 1; overflow-y: auto; padding: 20px 22px; }
.ai-state { color: var(--text-secondary); text-align: center; padding: 30px 0; }
.ai-error { color: var(--danger); line-height: 1.7; }
.ai-error small { color: var(--text-secondary); font-size: .78rem; }
.ai-result { font-size: .92rem; line-height: 1.9; color: var(--text-primary); overflow-wrap: anywhere; }
.ai-result :deep(p) { margin: 0 0 12px; }
.ai-result :deep(h1) { font-size: 1.5rem; margin: 22px 0 10px; }
.ai-result :deep(h2) { font-size: 1.25rem; margin: 20px 0 8px; padding-bottom: 6px; border-bottom: 1px solid var(--border-line); }
.ai-result :deep(h3) { font-size: 1.08rem; margin: 16px 0 6px; }
.ai-result :deep(ul), .ai-result :deep(ol) { margin: 0 0 12px; padding-left: 24px; }
.ai-result :deep(li) { margin: 5px 0; }
.ai-result :deep(blockquote) { margin: 0 0 12px; padding: 8px 14px; border-left: 3px solid var(--accent-color); background: var(--bg-sub); color: var(--text-secondary); }
.ai-result :deep(code) { background: var(--bg-sub); padding: 1px 5px; border-radius: 4px; font-size: .88em; }
.ai-result :deep(pre) { background: var(--bg-sub); padding: 12px; border-radius: 8px; overflow-x: auto; }
.ai-modal-foot { display: flex; justify-content: flex-end; padding: 14px 22px; border-top: 1px solid var(--border-line); }
.ai-modal-foot .btn { padding: 8px 18px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); cursor: pointer; font-size: .84rem; }
.ai-modal-foot .btn.primary { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); }
.ai-modal-foot .btn:disabled { opacity: .5; cursor: default; }
.ai-import-modal { width: min(640px, 100%); }
.import-field { display: grid; gap: 8px; font-size: .85rem; }
.import-field textarea { padding: 12px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--bg-main); color: var(--text-primary); font-size: .88rem; line-height: 1.7; resize: vertical; font-family: inherit; }
.import-field textarea:focus { outline: none; border-color: var(--accent-color); }
.import-tip { margin: 0 0 12px; font-size: .85rem; color: var(--text-secondary); }
.import-preview { list-style: none; margin: 0; padding: 0; display: grid; gap: 10px; max-height: 50vh; overflow-y: auto; }
.import-preview li { padding: 12px 14px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--bg-main); }
.import-preview strong { font-size: .9rem; }
.import-type { margin-left: 8px; font-size: .7rem; color: var(--accent-color); background: var(--accent-soft); padding: 1px 8px; border-radius: 8px; }
.import-preview p { margin: 6px 0 0; font-size: .82rem; color: var(--text-secondary); line-height: 1.6; }
.import-done { color: var(--success); font-size: .9rem; font-weight: 600; }
.import-errors { list-style: none; margin: 8px 0 0; padding: 0; }
.import-errors li { color: var(--danger); font-size: .8rem; }
</style>
