<template>
  <div class="project-detail workbench-skin community-surface tone-world">
    <!-- ========== 顶部 ========== -->
    <ProjectHeader :project="projectError ? null : project" :card-count="cards.length" :is-owner="isOwner" :can-edit="canEdit" :can-organize="userStore.isLoggedIn" @back="goBack" @create="openCreateDialog" @organize="openAiOrganize" @import="openAiImport" @transfer="openTransfer" @settings="settingsTab = 'info'" @members="settingsTab = 'members'" @history="openRevisions()" />
    <WorldHistory v-if="revisionsOpen && project && !projectError" :project="project" :is-owner="isOwner" :can-edit="canEdit" :card-id="revisionsCard" :user-id="revisionsUser" :card-titles="cardTitles" @close="closeRevisions" @changed="onRevisionChanged" @open-card="selectCard" />

    <WorldTransfer v-if="transferMode && project && !projectError" :project="project" :mode="transferMode" :can-edit="isOwner" @close="transferMode = null" />
    <WorldSettings v-if="settingsTab && project && !projectError" :project="project" :tab="settingsTab" :is-owner="isOwner" @close="settingsTab = null" @left="onLeftWorld" @deleted="onLeftWorld" />
    <!-- ========== 主体 ========== -->
    <div class="layout">
      <!-- 左侧栏 -->
      <ProjectCardNavigation v-if="!projectError" v-model:search="searchQuery" :cards="cards" :loading="store.loadingCards"
        :selected-card-id="selectedCardId" :view="view" :can-stats="canStats" :can-pane="canPane" :pane-ids="paneIds" @pane="openPane" @select="selectCard" @overview="showOverview" @graph="switchToGraph" @timeline="switchToTimeline" @stats="switchToStats" />

      <!-- 主区域 -->
      <main class="main" :style="mainPaneStyle" :aria-busy="store.loadingCards || detailLoading">
        <div v-if="projectError" class="load-state" role="alert">
          <p>{{ projectError }}</p>
          <RouterLink v-if="!userStore.isLoggedIn" :to="{ path: '/LoginRegister', query: { redirect: route.fullPath } }">登录自己的账号</RouterLink>
          <button class="btn" @click="loadRoute">重新加载</button>
          <RouterLink to="/world?tab=public">浏览公开世界</RouterLink>
        </div>
        <div v-else-if="!project" class="load-state" role="status">正在加载世界观…</div>
        <!-- 图谱视图 -->
        <div v-else-if="view === 'graph'" class="graph-view" :inert="graphReloading" :aria-busy="graphReloading">
          <RelationGraph
            :key="`${projectId}:${graphRenderVersion}`"
            ref="graphRef"
            :project-id="projectId"
            :positions="graphPositions"
            :save-status="graphSaveStatus"
            :can-save="canEdit"
            @retry-save="saveGraphPositions(graphPositions)"
            @reload-positions="reloadGraphPositions"
            @select="selectCard"
            @update:positions="saveGraphPositions"
          />
        </div>

        <!-- 世界编年史（多条时间线、分叉汇合、自定义历法） -->
        <div v-else-if="view === 'timeline'" class="timeline-view-wrap">
          <ChronicleView :project-id="projectId" :can-edit="canEdit" :can-pane="canPane" @select="selectCard" @pane="openPane" />
        </div>

        <!-- 创作统计（创建者和协作者） -->
        <div v-else-if="view === 'stats'" class="stats-view-wrap">
          <WorldStats :project-id="projectId" @select="selectCard" />
        </div>

        <!-- 卡片视图（含导航历史） -->
        <div v-else class="card-view" :class="{ 'is-editing': canEdit && (isCreating || editing) }">
          <div v-if="selectedCardId && !isCreating && !editing && (canEdit || canGoBack || canGoForward)" class="history-bar">
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
            <button v-if="lastEditor && isMember" type="button" class="last-edit" title="查看这个条目的修改记录" @click="openRevisions(selectedCardId)">最后修改：{{ lastEditor }}</button>
            <span v-else-if="lastEditor" class="last-edit">最后修改：{{ lastEditor }}</span>
            <button v-if="canEdit && selectedCardDetail && !editing" class="edit-entry" @click="openEditor">✎ 编辑条目</button>
          </div>

          <!-- 编辑器视图 -->
          <div
            v-if="canEdit && (isCreating || editing)"
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
              @saved="id => onCardChanged(id, true)"
              @deleted="onCardChanged(undefined, true)"
            />
          </div>
        </div>

        <!-- 只读详情（含右侧关联面板） -->
        <div v-else-if="detailLoading" class="load-state" role="status">正在加载条目…</div>
        <div v-else-if="detailError" class="load-state" role="alert">条目不存在、已取消公开，或暂时无法加载。<button class="btn" @click="loadRoute">重新加载</button><button class="btn" @click="showOverview">返回世界概览</button></div>
        <div v-else-if="selectedCardDetail" class="doc-layout">
          <ProjectCardDocument :key="selectedCardDetail.id"
            :card="selectedCardDetail" :project-id="projectId" :can-view-history="isMember" @select="selectCard" @history="openRevisions(selectedCardDetail.id)" @pane="openPane" />
          <aside v-if="outRelations.length || inRelations.length" class="doc-side-panel">
            <RelationStar :title="selectedCardDetail.title" :card-type="selectedCardDetail.type" :outs="outRelations" :ins="inRelations" @select="selectCard" />
          </aside>
        </div>

        <section v-else-if="project && !projectError" class="world-overview">
          <div class="overview-hero">
            <img v-if="coverUpload" :src="coverUpload.preview" alt="" class="hero-cover is-uploading" />
            <img v-else-if="project.coverImage" :src="coverSrc(project.coverImage)" :style="coverStyle(project.coverImage)" :alt="project.name" class="hero-cover" />
            <div v-else class="hero-fallback">
              <ParticleMorph class="hero-particles" :shapes="heroShapes" :interval="4600" :density="18" :fill="0.9" :max-particles="2600" :colors="['--accent-color', '--signal-mint', '--signal-color']" interactive="parent" />
              <span class="hero-fallback-text">{{ isOwner ? '这个宇宙还没有封面 · 右下角可以添加' : '这个宇宙还没有封面' }}</span>
            </div>
            <div v-if="coverUpload" class="hero-upload" role="status" aria-live="polite">
              <div class="hero-upload-row">
                <span class="hero-upload-tag">UPLOAD</span>
                <span class="hero-upload-text">{{ coverUpload.saving ? '上传完成，正在保存…' : `封面上传中 ${coverUpload.percent}%` }}</span>
                <span class="hero-upload-size">{{ coverUpload.sizeText }}</span>
              </div>
              <div class="hero-upload-bar" role="progressbar" aria-label="封面上传进度" aria-valuemin="0" aria-valuemax="100" :aria-valuenow="coverUpload.percent">
                <span :style="{ width: coverUpload.percent + '%' }"></span>
              </div>
            </div>
            <div v-if="isOwner && !coverUpload" class="hero-cover-actions">
              <button v-if="project.coverImage" class="hero-cover-btn" :disabled="coverRemoving" @click="removeCover">{{ coverRemoving ? '删除中…' : '删除封面' }}</button>
              <button v-if="project.coverImage" class="hero-cover-btn" @click="focusOpen = true">调整显示位置</button>
              <button class="hero-cover-btn" @click="pickCover">{{ project.coverImage ? '更换封面' : '添加封面' }}</button>
            </div>
            <input ref="coverInput" type="file" accept="image/*" hidden @change="onCoverPicked" />
          </div>
          <CoverFocusEditor :open="focusOpen" :url="project.coverImage || ''" :saving="focusSaving" @close="focusOpen = false" @save="saveCoverFocus" />
          <p class="overview-intro">{{ project.description || '这个世界还没有简介，先从下面的设定开始了解吧。' }}</p>
          <div v-if="!cards.length" class="overview-start">
            <h3>{{ canEdit ? '先写下一个设定' : '作者还在构思这个世界' }}</h3>
            <p>{{ canEdit ? '一个人物、一处地点，或者故事背景都可以。不必一次填完所有信息。' : '有了公开的条目，就会显示在这里。' }}</p>
            <button v-if="canEdit" class="btn-primary" @click="openCreateDialog">创建第一个条目</button>
          </div>
          <div v-else class="overview-stats">
            <button v-for="(group, gi) in overviewGroups" :key="group.type" class="stat-chip" :style="{ '--tc': typeColor(group.type), '--d': gi * 0.05 + 's' }"
              :title="`看看${group.label}：${group.cards.slice(0, 5).map(c => c.title).join('、')}${group.cards.length > 5 ? '…' : ''}`" @click="selectCard(group.cards[0].id)">
              <span class="stat-icon" aria-hidden="true">{{ typeIcon(group.type) }}</span>
              <span class="stat-label">{{ group.label }}</span>
              <span class="stat-count">{{ group.cards.length }}</span>
              <span class="stat-bar" aria-hidden="true"><i :style="{ width: Math.max(6, group.cards.length / statMax * 100) + '%' }"></i></span>
            </button>
            <button v-if="canStats" class="stat-chip stat-chip-stats" @click="switchToStats">📊 创作统计 →</button>
          </div>

          <!-- 本世界的小说 -->
          <section class="novel-block">
            <div class="novel-block-head">
              <h3>📖 小说</h3>
              <button v-if="canEdit" class="novel-add" @click="createNovel">＋ 写本世界的小说</button>
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

          <!-- 柴圈人设：关联到这个世界的柴圈 OC（没有就不显示） -->
          <WorldOcList :project-id="projectId" />
        </section>
        </div>
      </main>
      <!-- 分屏：主窗口旁边最多再开两个条目一起看（屏幕太窄时先收起，加宽后还在）。
           每个窗格左边有一条分隔线：拖动（鼠标、手指都行）或者聚焦后按左右键调宽度，双击恢复默认。 -->
      <template v-if="canPane && project && !projectError">
        <template v-for="(id, i) in paneIds" :key="'pane' + i">
          <div class="pane-splitter" :class="{ dragging: splitDrag?.i === i }" role="separator" aria-orientation="vertical" tabindex="0"
            :aria-label="`调整${i === 0 ? '主窗口' : '左边窗格'}和右边窗格的宽度`" aria-valuemin="0" aria-valuemax="100" :aria-valuenow="splitterValue(i)"
            title="拖动调整宽度（聚焦后左右键也可以），双击恢复默认"
            @pointerdown="onSplitDown($event, i)" @pointermove="onSplitMove" @pointerup="onSplitUp" @pointercancel="onSplitUp" @lostpointercapture="onSplitUp"
            @dblclick="resetPaneWidths" @keydown="onSplitKey($event, i)"></div>
          <CardPane :ref="el => setPaneRef(el, i)" class="side-pane"
            :style="{ flex: `${paneGrows[i + 1]} 1 0` }" :card-id="id" :project-id="projectId" :wide="paneWide(i)"
            @close="closePane(i)" @main="selectCard" @navigate="nid => onPaneNavigate(i, nid)" @toggle-wide="toggleWide(i)" />
        </template>
      </template>
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
import { ElMessage, ElMessageBox } from 'element-plus'
import { useWorldStore } from '@/stores/world'
import { useUserStore } from '@/stores/user'
import type { CardDetail } from '@/api/types/world'
import { aiConfigApi, type ImportCardItem } from '@/api/aiConfig'
import { parseGraphPositions } from '@/utils/worldGraphLayout'
import { groupWorldCards } from '@/utils/worldPresentation'
import { worldNovelApi, type NovelBrief } from '@/api/worldNovel'
import { worldApi } from '@/api/worldApi'
import { renderMarkdown } from '@/utils/markdown'
import { useCos } from '@/composables/useCos'
import { coverSrc, coverStyle } from '@/utils/coverFocus'
import CoverFocusEditor from '@/components/CoverFocusEditor.vue'
import ProjectHeader from './世界观组件/ProjectHeader.vue'
import ProjectCardNavigation from './世界观组件/ProjectCardNavigation.vue'
import WorldStats from './世界观组件/WorldStats.vue'
import CardPane from './世界观组件/CardPane.vue'
import ProjectCardDocument from './世界观组件/ProjectCardDocument.vue'
import WorldOcList from './世界观组件/WorldOcList.vue'
import RelationStar from './世界观组件/RelationStar.vue'
import ParticleMorph from '@/components/ParticleMorph.vue'
import { firstGlyph, globeShape, hexShape, type ParticleShape } from '@/utils/particleShapes'
import { CardTypeMeta, type CardType } from './card_type'

const WorldTransfer = defineAsyncComponent(() => import('./世界观组件/WorldTransfer.vue'))
const WorldSettings = defineAsyncComponent(() => import('./世界观组件/WorldSettings.vue'))
const WorldHistory = defineAsyncComponent(() => import('./世界观组件/WorldHistory.vue'))
const transferMode = ref<'import' | 'export' | null>(null)
async function openTransfer(mode: 'import' | 'export') {
  if (await mayLeaveEditor()) transferMode.value = mode
}
const RelationGraph = defineAsyncComponent(() => import('./世界观组件/RelationGraph.vue'))
const ChronicleView = defineAsyncComponent(() => import('./世界观组件/ChronicleView.vue'))
const CardEditor = defineAsyncComponent(() => import('./世界观组件/CardEditor.vue'))

const router = useRouter()
const route = useRoute()
const store = useWorldStore()
const userStore = useUserStore()
const projectId = computed(() => String(route.params.id || route.params.projectId || ''))
const project = computed(() => store.currentProject)
const cards = computed(() => store.cards)
const isOwner = computed(() => Boolean(userStore.userId && userStore.userId === project.value?.ownerId))
// 协作者：编写者能写条目/关系/布局；只读成员只能看。改名、封面、公开、删除、管理成员只有创建者。
const myRole = computed(() => isOwner.value ? 'owner' : project.value?.myRole ?? null)
const isMember = computed(() => Boolean(myRole.value))
const canEdit = computed(() => isOwner.value || myRole.value === 'editor')
// 创作统计入口（fix121）：成员看完整的，公开世界的访客看公开的那部分
const canStats = computed(() => isMember.value || Boolean(project.value?.isPublic))
const settingsTab = ref<'info' | 'members' | null>(null)
// ---------- 修改记录 ----------
const revisionsOpen = ref(false)
const revisionsCard = ref<string | null>(null)
const revisionsUser = ref<string | null>(null)
const lastEditors = ref<Record<string, { name: string; at: string }>>({})
const cardTitles = computed(() => Object.fromEntries(cards.value.map(c => [c.id, c.title])))
function openRevisions(cardId?: string | null, userId?: string | null) {
  revisionsCard.value = cardId && cardId !== 'new' ? cardId : null
  revisionsUser.value = userId || null
  revisionsOpen.value = true
}
function closeRevisions() {
  revisionsOpen.value = false
  // 从通知点进来带着 ?history=1，关掉后去掉，刷新页面不会再弹
  if (route.query.history) router.replace({ query: { ...route.query, history: undefined, user: undefined } }).catch(() => {})
}
async function refreshLastEditors() {
  const id = projectId.value
  if (!id) return
  try {
    const rows = await worldApi.getLastEditors(id)
    if (id !== projectId.value) return
    lastEditors.value = Object.fromEntries(rows.filter(r => r.username).map(r => [r.cardId, { name: r.username!, at: r.createdAt }]))
  } catch { lastEditors.value = {} }
}
const lastEditor = computed(() => {
  const info = selectedCardId.value ? lastEditors.value[selectedCardId.value] : undefined
  if (!info) return ''
  const d = new Date(info.at.endsWith('Z') || info.at.includes('+') ? info.at : info.at + 'Z')
  const diff = Date.now() - d.getTime()
  const when = diff < 60000 ? '刚刚' : diff < 3600000 ? `${Math.floor(diff / 60000)} 分钟前` : diff < 86400000 ? `${Math.floor(diff / 3600000)} 小时前` : d.toLocaleDateString('zh-CN')
  return `${info.name} · ${when}`
})
async function onRevisionChanged(cardId: string) {
  await onCardChanged(cardId || selectedCardId.value || undefined)
}
function onLeftWorld() {
  settingsTab.value = null
  router.push('/world')
}
const searchQuery = ref('')
const view = ref<'card' | 'graph' | 'timeline' | 'stats'>('card')
const graphRef = ref<InstanceType<typeof RelationGraph> | null>(null)
const selectedCardId = ref<string | null>(null)
const selectedCardDetail = ref<CardDetail | null>(null)
const isCreating = computed(() => selectedCardId.value === 'new')
const selectedCard = computed(() => cards.value.find(card => card.id === selectedCardId.value) ?? null)
let selectionVersion = 0
const editing = ref(false)
const cardEditorRef = ref<InstanceType<typeof CardEditor> | null>(null)
const mayLeaveEditor = () => cardEditorRef.value?.requestLeave() ?? Promise.resolve(true)
async function mayLeavePage() {
  if (!await mayLeaveEditor()) return false
  const id = projectId.value
  await store.waitForGraphSave(id)
  if (!['error', 'conflict'].includes(store.graphSaves.get(id)?.status || '')) return true
  try {
    await ElMessageBox.confirm('节点位置尚未保存。离开会放弃本次位置调整，是否离开？', '位置保存失败', { confirmButtonText: '放弃调整并离开', cancelButtonText: '留下重试', type: 'warning' })
    store.discardGraphSave(id)
    return true
  } catch { return false }
}
onBeforeRouteLeave(mayLeavePage)
onBeforeRouteUpdate(mayLeavePage)
const detailLoading = ref(false)
const detailError = ref(false)
const projectError = ref('')
const overviewGroups = computed(() => groupWorldCards(cards.value))
const statMax = computed(() => Math.max(1, ...overviewGroups.value.map(g => g.cards.length)))
const typeColor = (type: string) => CardTypeMeta[type as CardType]?.color ?? 'var(--accent-color)'
const typeIcon = (type: string) => CardTypeMeta[type as CardType]?.icon ?? '◇'
/** 没有封面时横幅上的粒子：世界名字的第一个字 → 地球 → 六边形 */
const heroShapes = computed<ParticleShape[]>(() => [
  { kind: 'text', text: firstGlyph(project.value?.name), scale: 0.62 },
  { kind: 'draw', draw: globeShape },
  { kind: 'draw', draw: hexShape },
])

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
    // 刚保存完、store 里已经是最新内容时直接用
    const detail = await store.fetchCardDetail(projectId.value, id, reuseDetailOnce !== id)
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
/** 正在上传的封面：本地预览 + 百分比，传完保存前一直显示在横幅上 */
const coverUpload = ref<{ preview: string; percent: number; sizeText: string; saving: boolean } | null>(null)
const focusOpen = ref(false)
const focusSaving = ref(false)
function pickCover() { if (!coverUpload.value) coverInput.value?.click() }
function sizeText(bytes: number) {
  return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`
}
async function onCoverPicked(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file || coverUpload.value) return
  if (!file.type.startsWith('image/')) { ElMessage.warning('请选择图片文件'); return }
  const id = projectId.value
  const state = { preview: URL.createObjectURL(file), percent: 0, sizeText: sizeText(file.size), saving: false }
  coverUpload.value = state
  let saved = false
  try {
    const { url } = await uploadFile(file, 'world-covers', percent => {
      if (coverUpload.value) coverUpload.value.percent = Math.min(100, Math.max(coverUpload.value.percent, percent))
    })
    if (coverUpload.value) { coverUpload.value.percent = 100; coverUpload.value.saving = true }
    await store.updateProject(id, { coverImage: url })
    saved = true
    await loadData()
  } catch { ElMessage.error('封面上传失败，请重试') }
  finally {
    URL.revokeObjectURL(state.preview)
    coverUpload.value = null
  }
  // 新封面传好后直接让作者挑显示位置（不调也行，默认居中）
  if (saved && id === projectId.value) {
    ElMessage.success('封面已更新，可以拖动选择显示的位置')
    focusOpen.value = true
  }
}
/** 去掉封面（只是不再显示，图片文件本身不删，换回来重新上传即可） */
const coverRemoving = ref(false)
async function removeCover() {
  try {
    await ElMessageBox.confirm('删除后世界观列表和这里都不再显示封面，之后可以随时重新添加。', '删除封面', {
      confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning',
    })
  } catch { return }
  coverRemoving.value = true
  try {
    // 后端约定：传空字符串表示清空封面（不传 / 传 null 表示不改）
    await store.updateProject(projectId.value, { coverImage: '' })
    ElMessage.success('封面已删除')
    await loadData()
  } catch { ElMessage.error('删除失败，请重试') }
  finally { coverRemoving.value = false }
}
async function saveCoverFocus(url: string) {
  focusSaving.value = true
  try {
    await store.updateProject(projectId.value, { coverImage: url })
    focusOpen.value = false
    ElMessage.success('显示位置已保存，列表和这里会显示同一块')
    await loadData()
  } catch { ElMessage.error('保存失败，请重试') }
  finally { focusSaving.value = false }
}

// ===== 关系图谱节点位置（持久化） =====
const graphPositions = computed(() => parseGraphPositions(project.value?.graphPositions))
const graphSaveStatus = computed(() => store.graphSaves.get(projectId.value)?.status || '')
const graphRenderVersion = ref(0)
const graphReloading = ref(false)
async function reloadGraphPositions() {
  const id = projectId.value
  graphReloading.value = true
  try {
    if (await store.reloadGraphPositions(id) && id === projectId.value) graphRenderVersion.value++
  } catch { ElMessage.error('最新布局读取失败，本页调整仍保留，请重试。') }
  finally { graphReloading.value = false }
}
function saveGraphPositions(p: Record<string, { x: number; y: number }>) {
  if (canEdit.value) void store.saveGraphPositions(projectId.value, JSON.stringify(p))
}
function guardGraphRefresh(event: BeforeUnloadEvent) {
  if (['saving', 'error', 'conflict'].includes(graphSaveStatus.value)) {
    event.preventDefault()
    event.returnValue = ''
  }
}
if (typeof window !== 'undefined') window.addEventListener('beforeunload', guardGraphRefresh)
onScopeDispose(() => { if (typeof window !== 'undefined') window.removeEventListener('beforeunload', guardGraphRefresh) })
async function switchToGraph() {
  await router.push({ name: 'ProjectDetail', params: { id: projectId.value }, query: { view: 'graph' } })
}
async function switchToTimeline() {
  await router.push({ name: 'ProjectDetail', params: { id: projectId.value }, query: { view: 'timeline' } })
}
async function switchToStats() {
  await router.push({ name: 'ProjectDetail', params: { id: projectId.value }, query: { view: 'stats' } })
}
async function loadData(force = true) {
  const id = projectId.value
  const token = userStore.token
  projectError.value = ''
  try { await store.fetchCards(id, force) }
  catch (error: any) {
    if (id === projectId.value && token === userStore.token) projectError.value = [403, 404].includes(error?.response?.status)
      ? '这个世界未公开、已删除，或当前账号无权查看。'
      : '世界观暂时无法加载，请稍后重试。'
  }
  void refreshLastEditors()
}
// 从「协作者删除了条目」的通知点进来时带 ?history=1：直接打开修改记录
// 「xxx 修改了条目」的通知还带 &user=：只看这个人的修改
watch([() => route.query.history, isMember], ([flag, member]) => {
  if (flag && member) openRevisions(null, typeof route.query.user === 'string' ? route.query.user : null)
}, { immediate: true })
// storeFresh：编辑器保存 / 删除走的是 store，保存完 store 已经把世界和条目重新读过了，这里不用再强制读一遍
// （数据库远的时候每次重复读取都要多等一两秒）
let reuseDetailOnce = ''
async function onCardChanged(savedId?: string, storeFresh = false) {
  closePanel()
  await loadData(!storeFresh)
  if (storeFresh && savedId) reuseDetailOnce = savedId
  if (savedId && cards.value.some(card => card.id === savedId)) await selectCard(savedId)
  reuseDetailOnce = ''
  graphRef.value?.refresh()
  // 主窗口改了条目，旁边窗格里如果开着同一个，跟着刷新
  paneRefs.forEach(p => p?.refresh?.(savedId))
}
const goBack = () => router.push((isOwner.value || isMember.value) ? '/world' : '/world?tab=public')
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
    if (canEdit.value) selectedCardId.value = 'new'
    return
  }
  const cardId = String(route.params.cardId || route.query.edit || '')
  if (cardId) {
    await loadCard(cardId)
    if (version === routeVersion && canEdit.value && route.query.edit === cardId && selectedCardDetail.value) editing.value = true
  } else {
    closePanel()
    view.value = route.query.view === 'graph' || route.query.view === 'timeline' || route.query.view === 'stats' ? route.query.view : 'card'
  }
}
watch([projectId, () => route.params.cardId, () => route.query.edit, () => route.query.view, () => userStore.token], loadRoute, { immediate: true })
onScopeDispose(() => { selectionVersion++; routeVersion++ })

// ===== 分屏（多窗口）=====
const MAX_PANES = 2
const paneIds = ref<string[]>([])
const widePane = ref<number | null>(null)
const paneRefs: any[] = []
function setPaneRef(el: unknown, i: number) { paneRefs[i] = el }
const paneKey = () => `taichu.world.panes:${projectId.value}`
// 屏幕够宽（≥ 1000px）才分屏；窄的时候窗格先收起，状态还留着
const viewportWide = ref(typeof window === 'undefined' ? true : window.innerWidth >= 1000)
const onResize = () => { viewportWide.value = window.innerWidth >= 1000 }
if (typeof window !== 'undefined') window.addEventListener('resize', onResize)
onScopeDispose(() => { if (typeof window !== 'undefined') window.removeEventListener('resize', onResize) })
const canPane = computed(() => viewportWide.value)
function savePanes() { try { sessionStorage.setItem(paneKey(), JSON.stringify(paneIds.value)) } catch { /* 忽略 */ } }
function openPane(id: string) {
  if (!id) return
  if (!canPane.value) { void selectCard(id); return } // 窄屏没法分屏，就在主窗口打开
  if (paneIds.value.includes(id)) return
  // 满了就替换最后一个
  paneIds.value = paneIds.value.length >= MAX_PANES ? [...paneIds.value.slice(0, MAX_PANES - 1), id] : [...paneIds.value, id]
  savePanes()
}
function closePane(i: number) {
  paneIds.value = paneIds.value.filter((_, idx) => idx !== i)
  paneRefs.splice(i, 1)
  if (widePane.value === i) widePane.value = null
  else if (widePane.value !== null && widePane.value > i) widePane.value--
  savePanes()
}
function onPaneNavigate(i: number, id: string) {
  if (paneIds.value[i] === id) return
  const next = [...paneIds.value]; next[i] = id; paneIds.value = next
  savePanes()
}

// ----- 拖动分隔线调宽度 -----
// 主窗口和窗格都是「按比例分剩下的宽度」（flex-grow），拖动只改分隔线两边那两块的比例，别的不动。
// 拖好的比例按「开着几个窗格」分别记在这台设备上（localStorage），换世界、刷新页面都还在。
const PANE_WIDTHS_KEY = 'taichu.world.paneWidths'
const MIN_MAIN_PX = 320 // 主窗口最窄
const MIN_PANE_PX = 240 // 窗格最窄
/** 自己拖过的比例：[主窗口, 窗格1, 窗格2]；null = 没拖过，用默认（等宽，或者「加宽」的那个 1.6 倍） */
const customGrows = ref<number[] | null>(null)
function readPaneWidths(): Record<string, number[]> {
  try {
    const raw = JSON.parse(localStorage.getItem(PANE_WIDTHS_KEY) || '{}')
    return raw && typeof raw === 'object' && !Array.isArray(raw) ? raw : {}
  } catch { return {} }
}
function writePaneWidths(count: number, grows: number[] | null) {
  try {
    const all = readPaneWidths()
    if (grows) all[count] = grows.map(g => Number(g.toFixed(4)))
    else delete all[count]
    localStorage.setItem(PANE_WIDTHS_KEY, JSON.stringify(all))
  } catch { /* 隐私模式、存满了：只是记不住，不影响使用 */ }
}
/** 读出来的数要像样：个数对得上、都是正常的正数，不然当没拖过 */
function loadPaneWidths(count: number) {
  const saved = readPaneWidths()[count]
  customGrows.value = Array.isArray(saved) && saved.length === count + 1 && saved.every(g => typeof g === 'number' && Number.isFinite(g) && g > 0.05 && g < 20) ? saved : null
}
watch(() => paneIds.value.length, n => { if (n) loadPaneWidths(n) }, { immediate: true })
const paneGrows = computed(() => customGrows.value && customGrows.value.length === paneIds.value.length + 1
  ? customGrows.value
  : [1, ...paneIds.value.map((_, i) => widePane.value === i ? 1.6 : 1)])
// 没开窗格时主窗口保持原来的样式；开了窗格才按比例分
const mainPaneStyle = computed(() => canPane.value && paneIds.value.length ? { flex: `${paneGrows.value[0]} 1 0` } : undefined)
// 拖过宽度后，窗格上的「加宽」按钮变成「恢复默认宽度」
const paneWide = (i: number) => Boolean(customGrows.value) || widePane.value === i
function toggleWide(i: number) {
  if (customGrows.value) { resetPaneWidths(); return }
  widePane.value = widePane.value === i ? null : i
}
function resetPaneWidths() {
  customGrows.value = null
  widePane.value = null
  writePaneWidths(paneIds.value.length, null)
}
/** 分隔线两边各占多少（给读屏软件报的百分比） */
const splitterValue = (i: number) => {
  const g = paneGrows.value
  return Math.round((100 * (g[i] ?? 1)) / ((g[i] ?? 1) + (g[i + 1] ?? 1)))
}
/** 分隔线 i 左边那块挪 dx 像素：两块总宽不变，按最小宽度夹住，再换算成比例 */
function resizePair(i: number, leftPx: number, rightPx: number, base: number[], dx: number) {
  const total = leftPx + rightPx
  if (total <= 0) return
  const minLeft = Math.min(i === 0 ? MIN_MAIN_PX : MIN_PANE_PX, total / 2)
  const minRight = Math.min(MIN_PANE_PX, total / 2)
  const nextLeft = Math.max(minLeft, Math.min(total - minRight, leftPx + dx))
  const pair = (base[i] ?? 1) + (base[i + 1] ?? 1)
  const next = base.slice()
  next[i] = (pair * nextLeft) / total
  next[i + 1] = pair - next[i]!
  customGrows.value = next
  widePane.value = null
}
/** 分隔线左右两块的实际宽度（分隔线就夹在它们中间） */
function pairWidths(splitter: HTMLElement) {
  const left = splitter.previousElementSibling as HTMLElement | null
  const right = splitter.nextElementSibling as HTMLElement | null
  return left && right ? { left: left.getBoundingClientRect().width, right: right.getBoundingClientRect().width } : null
}
const splitDrag = ref<{ i: number; x: number; left: number; right: number; base: number[]; pointerId: number; moved: boolean } | null>(null)
function onSplitDown(e: PointerEvent, i: number) {
  if (e.pointerType === 'mouse' && e.button !== 0) return
  const el = e.currentTarget as HTMLElement
  const sizes = pairWidths(el)
  if (!sizes) return
  e.preventDefault() // 别选中文字、别让页面跟着滚
  try { el.setPointerCapture(e.pointerId) } catch { /* 个别浏览器不支持，也能拖，只是移出去会断 */ }
  splitDrag.value = { i, x: e.clientX, ...sizes, base: paneGrows.value.slice(), pointerId: e.pointerId, moved: false }
  setResizingCursor(true)
}
function onSplitMove(e: PointerEvent) {
  const d = splitDrag.value
  if (!d || e.pointerId !== d.pointerId) return
  const dx = e.clientX - d.x
  if (!d.moved && Math.abs(dx) < 2) return // 手抖一下不算拖，免得双击时宽度先变了
  d.moved = true
  resizePair(d.i, d.left, d.right, d.base, dx)
}
function onSplitUp(e: PointerEvent) {
  const d = splitDrag.value
  if (!d || e.pointerId !== d.pointerId) return
  splitDrag.value = null
  setResizingCursor(false)
  if (d.moved && customGrows.value) writePaneWidths(paneIds.value.length, customGrows.value)
}
function onSplitKey(e: KeyboardEvent, i: number) {
  if (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight' && e.key !== 'Enter') return
  e.preventDefault()
  if (e.key === 'Enter') { resetPaneWidths(); return } // 键盘没法双击，回车 = 恢复默认
  const sizes = pairWidths(e.currentTarget as HTMLElement)
  if (!sizes) return
  const step = e.shiftKey ? 96 : 24
  resizePair(i, sizes.left, sizes.right, paneGrows.value.slice(), e.key === 'ArrowLeft' ? -step : step)
  if (customGrows.value) writePaneWidths(paneIds.value.length, customGrows.value)
}
/** 拖动时整页都显示「左右拖」的鼠标样式、不选中文字（鼠标划到窗格上面也一样） */
function setResizingCursor(on: boolean) {
  if (typeof document === 'undefined') return
  document.body.style.cursor = on ? 'col-resize' : ''
  document.body.style.userSelect = on ? 'none' : ''
}
onScopeDispose(() => { if (splitDrag.value) setResizingCursor(false) })

// 换了世界：读那个世界上次开着的窗格；只留还存在的条目
watch([projectId, () => cards.value.length], ([pid], [oldPid]) => {
  if (pid !== oldPid) {
    widePane.value = null
    try { paneIds.value = (JSON.parse(sessionStorage.getItem(paneKey()) || '[]') as string[]).slice(0, MAX_PANES) } catch { paneIds.value = [] }
  }
  if (cards.value.length) {
    const alive = paneIds.value.filter(id => cards.value.some(c => c.id === id))
    if (alive.length !== paneIds.value.length) { paneIds.value = alive; savePanes() }
  }
}, { immediate: true })
</script>

<style scoped>
.world-overview { width: 100%; max-width: 1100px; padding: clamp(20px, 4vw, 40px); margin: 0 auto; }
.overview-hero { position: relative; border-radius: 0; overflow: hidden; aspect-ratio: 21 / 7; background: var(--bg-sub); margin-bottom: 24px; }
.hero-cover { width: 100%; height: 100%; object-fit: cover; display: block; }
.hero-cover.is-uploading { filter: saturate(.6) brightness(.85); }
.hero-cover-actions { position: absolute; right: 14px; bottom: 14px; display: flex; gap: 8px; }
.hero-cover-actions .hero-cover-btn { position: static; }
.hero-cover-btn:disabled { opacity: .6; cursor: not-allowed; }
@media (max-width: 820px) { .hero-cover-actions { left: 10px; right: 10px; bottom: 10px; flex-wrap: wrap; justify-content: flex-end; gap: 6px; } .hero-cover-actions .hero-cover-btn { padding: 5px 10px; font-size: 12px; } .hero-upload { left: 10px; right: 10px; bottom: 10px; } }
.hero-upload { position: absolute; left: 14px; right: 14px; bottom: 14px; padding: 10px 12px; background: var(--paper-card); border: 1px solid var(--border-line); border-left: 3px solid var(--accent-color); }
.hero-upload-row { display: flex; align-items: center; gap: 10px; font-size: 13px; color: var(--text-primary); }
.hero-upload-tag { padding: 1px 7px; font: 10px/1.6 var(--font-mono, monospace); letter-spacing: .12em; background: var(--inverse-bg); color: var(--inverse-text); }
.hero-upload-text { flex: 1; font-variant-numeric: tabular-nums; }
.hero-upload-size { font: 12px var(--font-mono, monospace); color: var(--text-secondary); }
.hero-upload-bar { margin-top: 8px; height: 6px; background: var(--bg-sub); overflow: hidden; }
.hero-upload-bar span { display: block; height: 100%; background: var(--accent-color); transition: width .2s; }
.hero-fallback { position: relative; width: 100%; height: 100%; display: flex; flex-direction: column; align-items: flex-start; justify-content: flex-end; color: var(--accent-color);
  background: radial-gradient(ellipse at 70% 40%, color-mix(in srgb, var(--signal-mint) 16%, transparent), transparent 60%),
    linear-gradient(var(--grid-line) 1px, transparent 1px) 0 0 / 24px 24px, linear-gradient(90deg, var(--grid-line) 1px, transparent 1px) 0 0 / 24px 24px,
    linear-gradient(135deg, var(--accent-soft), var(--paper-card) 70%); }
.hero-particles { position: absolute; inset: 0; width: 100%; height: 100%; }
.hero-fallback-text { position: relative; margin: 0 0 14px 16px; padding: 2px 8px; font: 11px/1.6 var(--font-mono, monospace); letter-spacing: .06em; color: var(--text-secondary); background: var(--paper-card); border-left: 2px solid var(--signal-mint); }
.hero-cover-btn { position: absolute; right: 14px; bottom: 14px; padding: 7px 14px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); cursor: pointer; font-size: 13px; }
.overview-intro { max-width: 70ch; white-space: pre-wrap; overflow-wrap: anywhere; line-height: 1.9; font-size: 15px; color: var(--text-secondary); margin: 0 0 28px; }
.overview-start { padding: 28px; border: 1px dashed var(--border-line); border-radius: 0; }.overview-start p { color: var(--text-secondary); line-height: 1.8; }.overview-start button { margin-top: 12px; }
.overview-stats { display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 10px; margin-top: 4px; }
.novel-block { margin-top: 34px; padding-top: 20px; border-top: 1px dashed var(--border-line); }
.novel-block-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 12px; }
.novel-block-head h3 { margin: 0; font-size: 1.02rem; }
.novel-add { padding: 6px 14px; border: 1px solid var(--accent-color); border-radius: 0; background: transparent; color: var(--accent-color); cursor: pointer; font-size: .8rem; }
.novel-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.novel-row { display: flex; align-items: center; gap: 12px; padding: 10px 12px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); text-decoration: none; color: inherit; }
.novel-row:hover { border-color: var(--accent-color); }
.nv-cover { width: 38px; height: 50px; flex-shrink: 0; border-radius: 0; overflow: hidden; background: var(--accent-soft); display: grid; place-items: center; color: var(--accent-color); font-weight: 700; }
.nv-cover img { width: 100%; height: 100%; object-fit: cover; }
.nv-main { min-width: 0; display: grid; gap: 4px; }
.nv-title { font-size: .9rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.nv-meta { font-size: .72rem; color: var(--text-secondary); }
.novel-empty { margin: 0; padding: 18px 0; color: var(--text-secondary); font-size: .84rem; }
.stat-chip { --tc: var(--accent-color); position: relative; display: grid; grid-template-columns: auto 1fr auto; align-items: center; gap: 4px 8px; padding: 12px 14px 10px; border: 1px solid var(--border-line); border-top: 2px solid var(--tc); border-radius: 0; background: var(--paper-card); color: var(--text-primary); cursor: pointer; font: inherit; text-align: left; transition: border-color .15s, translate .15s, box-shadow .15s; animation: stat-in .45s cubic-bezier(.2, .8, .2, 1) both; animation-delay: var(--d, 0s); }
.stat-chip::after { content: ''; position: absolute; right: -1px; top: -2px; width: 10px; height: 10px; background: linear-gradient(225deg, var(--tc) 50%, transparent 50%); }
.stat-chip:hover, .stat-chip:focus-visible { border-color: var(--tc); translate: 0 -2px; box-shadow: 3px 3px 0 color-mix(in srgb, var(--tc) 25%, transparent); }
.stat-chip-stats { --tc: var(--signal-mint); display: flex; align-items: center; justify-content: center; color: var(--accent-color); border-style: dashed; border-top-style: solid; }
.stat-icon { font-size: 15px; line-height: 1; }
.stat-label { font-size: 13px; color: var(--text-secondary); }
.stat-count { font: 600 22px/1 var(--font-display, inherit); color: var(--tc); font-variant-numeric: tabular-nums; }
.stat-bar { grid-column: 1 / -1; height: 3px; background: var(--bg-sub); overflow: hidden; }
.stat-bar i { display: block; height: 100%; background: var(--tc); opacity: .75; transform-origin: left; animation: stat-bar .8s cubic-bezier(.2, .8, .2, 1) both; animation-delay: calc(var(--d, 0s) + .15s); }
@keyframes stat-in { from { opacity: 0; translate: 0 6px; } }
@keyframes stat-bar { from { transform: scaleX(0); } }
@media (prefers-reduced-motion: reduce) { .stat-chip, .stat-bar i { animation: none; } }
.edit-entry { flex-shrink: 0; white-space: nowrap; padding: 7px 14px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--accent-color); cursor: pointer; font-size: 13px; }
.edit-entry:hover { border-color: var(--accent-color); background: var(--accent-soft); }
.last-edit { flex-shrink: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; margin-right: 10px; padding: 0; border: 0; background: none; color: var(--text-secondary); font: inherit; font-size: 12px; }
button.last-edit { cursor: pointer; }
button.last-edit:hover { color: var(--accent-color); text-decoration: underline; text-underline-offset: 3px; }.load-state { padding: 36px; color: var(--text-secondary); display: flex; gap: 16px; align-items: center; flex-wrap: wrap; }.load-state button { border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); padding: 8px 14px; cursor: pointer; }
button:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 2px; }
.load-state a { color: var(--accent-color); }.load-state p { flex-basis: 100%; margin: 0; line-height: 1.8; }

* { box-sizing: border-box; }
.project-detail {
  height: 100%;
  min-height: 0;
  border: 1px solid var(--border-line);
  border-radius: 0;
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
.side-pane { min-width: 0; overflow: hidden; }
/* 分隔线：本身不占宽度（左右用负边距叠在窗格的左边框上），鼠标放上去、拖动、键盘聚焦时显示主题色 */
.pane-splitter { position: relative; z-index: 6; flex: 0 0 7px; margin: 0 -6px 0 -1px; cursor: col-resize; touch-action: none; outline: none; }
.pane-splitter::after { content: ''; position: absolute; top: 0; bottom: 0; left: 1px; width: 2px; background: transparent; transition: background-color .15s; }
.pane-splitter:hover::after, .pane-splitter.dragging::after, .pane-splitter:focus-visible::after { background: var(--accent-color); }
.pane-splitter:focus-visible::after { width: 3px; }
/* 触屏手指粗，按的地方宽一点 */
@media (pointer: coarse) { .pane-splitter { flex-basis: 14px; margin: 0 -10px 0 -4px; } .pane-splitter::after { left: 4px; } }
.main {
  flex: 1 1 0;
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
  border-radius: 0;
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
  border-radius: 0;
}
.crumb:hover { background: var(--bg-sub); color: var(--text-primary); }
.crumb.current { color: var(--text-primary); font-weight: 600; }
.crumb-sep { color: var(--text-secondary); font-size: 12px; }
.graph-view {
  height: 100%;
  padding: 0 8px;
  box-sizing: border-box;
  background: var(--paper-card);
}
.timeline-view-wrap {
  height: 100%;
  min-height: 100%;
  background: var(--paper-card);
}
.stats-view-wrap {
  min-height: 100%;
  padding: 20px clamp(16px, 3vw, 32px);
  box-sizing: border-box;
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
  padding: 20px clamp(20px, 4vw, 40px) 32px;
  border-top: 1px dashed var(--border-line);
}
@media (min-width: 1480px) {
  .doc-layout:has(> .doc-side-panel) { grid-template-columns: minmax(0, 1fr) 300px; }
  .doc-side-panel {
    max-width: none;
    margin: 0;
    padding: 24px 18px;
    border-top: none;
    border-left: 1px solid var(--border-line);
    align-self: start;
    position: sticky;
    top: 0;
    max-height: calc(100dvh - 140px);
    overflow-y: auto;
  }
}
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
  border-radius: 0;
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
  border-radius: 0;
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
.ai-modal { width: min(760px, 100%); max-height: 86vh; display: flex; flex-direction: column; background: var(--paper-card); border-radius: 0; overflow: hidden; }
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
.ai-result :deep(code) { background: var(--bg-sub); padding: 1px 5px; border-radius: 0; font-size: .88em; }
.ai-result :deep(pre) { background: var(--bg-sub); padding: 12px; border-radius: 0; overflow-x: auto; }
.ai-modal-foot { display: flex; justify-content: flex-end; padding: 14px 22px; border-top: 1px solid var(--border-line); }
.ai-modal-foot .btn { padding: 8px 18px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); cursor: pointer; font-size: .84rem; }
.ai-modal-foot .btn.primary { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); }
.ai-modal-foot .btn:disabled { opacity: .5; cursor: default; }
.ai-import-modal { width: min(640px, 100%); }
.import-field { display: grid; gap: 8px; font-size: .85rem; }
.import-field textarea { padding: 12px; border: 1px solid var(--border-line); border-radius: 0; background: var(--bg-main); color: var(--text-primary); font-size: .88rem; line-height: 1.7; resize: vertical; font-family: inherit; }
.import-field textarea:focus { outline: none; border-color: var(--accent-color); }
.import-tip { margin: 0 0 12px; font-size: .85rem; color: var(--text-secondary); }
.import-preview { list-style: none; margin: 0; padding: 0; display: grid; gap: 10px; max-height: 50vh; overflow-y: auto; }
.import-preview li { padding: 12px 14px; border: 1px solid var(--border-line); border-radius: 0; background: var(--bg-main); }
.import-preview strong { font-size: .9rem; }
.import-type { margin-left: 8px; font-size: .7rem; color: var(--accent-color); background: var(--accent-soft); padding: 1px 8px; border-radius: 0; }
.import-preview p { margin: 6px 0 0; font-size: .82rem; color: var(--text-secondary); line-height: 1.6; }
.import-done { color: var(--success); font-size: .9rem; font-weight: 600; }
.import-errors { list-style: none; margin: 8px 0 0; padding: 0; }
.import-errors li { color: var(--danger); font-size: .8rem; }
</style>
