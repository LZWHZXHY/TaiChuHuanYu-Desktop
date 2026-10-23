<template>
  <div class="project-detail">
    <!-- ========== 顶部 ========== -->
    <ProjectHeader :project="project" :card-count="cards.length" :is-owner="isOwner" @back="goBack" @create="openCreateDialog" />

    <!-- ========== 主体 ========== -->
    <div class="layout">
      <!-- 左侧栏 -->
      <ProjectCardNavigation v-model:search="searchQuery" :cards="cards" :loading="store.loadingCards"
        :selected-card-id="selectedCardId" :view="view" @select="selectCard" @graph="switchToGraph" @timeline="switchToTimeline" />

      <!-- 主区域 -->
      <main class="main">
        <!-- 图谱视图 -->
        <div v-if="view === 'graph'" class="graph-view">
          <RelationGraph
            ref="graphRef"
            :project-id="projectId"
            @card-inserted="loadData"
          />
        </div>

        <!-- 时间线视图 -->
        <div v-else-if="view === 'timeline'" class="timeline-view-wrap">
          <TimelineView :project-id="projectId" @select="selectCard" />
        </div>

        <!-- 卡片视图（含导航历史） -->
        <div v-else class="card-view">
          <div v-if="selectedCardId || selectedCardDetail" class="history-bar">
            <button class="nav-btn" :disabled="!canGoBack" title="后退" @click="backCard">←</button>
            <button class="nav-btn" :disabled="!canGoForward" title="前进" @click="forwardCard">→</button>
            <nav v-if="historyCrumbs.length > 1" class="crumbs">
              <template v-for="(c, i) in historyCrumbs" :key="c.id + i">
                <button class="crumb" :class="{ current: i === historyIndex }" @click="jumpTo(i)">{{ c.title }}</button>
                <span v-if="i < historyCrumbs.length - 1" class="crumb-sep">/</span>
              </template>
            </nav>
          </div>

          <!-- 编辑器视图 -->
          <div
            v-if="isOwner && (isCreating || selectedCard)"
            class="editor-view"
          >
          <div class="editor-topbar">
            <button class="editor-back" @click="closePanel">← 返回</button>
            <span class="editor-title">
              {{ isCreating ? '新建卡片' : selectedCard?.title }}
            </span>
          </div>
          <div class="editor-body">
            <CardEditor
              :project-id="projectId"
              :card-data="isCreating ? null : selectedCard"
              @saved="onCardChanged"
              @deleted="onCardChanged"
            />
          </div>
        </div>

        <!-- 只读详情（含右侧关联面板） -->
        <div v-else-if="selectedCardDetail" class="doc-layout">
          <ProjectCardDocument :key="selectedCardDetail.id"
            :card="selectedCardDetail" :project-id="projectId" @select="selectCard" />
          <aside class="doc-side-panel">
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
            <p v-if="!outRelations.length && !inRelations.length" class="panel-empty">暂无关联</p>
          </aside>
        </div>

        <!-- 空状态 -->
        <div v-else class="empty-state">
          <template v-if="cards.length">
            <p>从左侧选择一张卡片</p>
          </template>
          <template v-else>
            <p>这个项目还没有内容</p>
            <button v-if="isOwner" class="btn-primary" @click="openCreateDialog">创建第一张卡片</button>
          </template>
        </div>
        </div>
      </main>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onScopeDispose, defineAsyncComponent } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useWorldStore } from '@/stores/world'
import { useUserStore } from '@/stores/user'
import type { CardDetail } from '@/api/types/world'
import CardEditor from './世界观组件/CardEditor.vue'
import ProjectHeader from './世界观组件/ProjectHeader.vue'
import ProjectCardNavigation from './世界观组件/ProjectCardNavigation.vue'
import ProjectCardDocument from './世界观组件/ProjectCardDocument.vue'

const RelationGraph = defineAsyncComponent(() => import('./世界观组件/RelationGraph.vue'))
const TimelineView = defineAsyncComponent(() => import('./世界观组件/TimelineView.vue'))

const router = useRouter()
const route = useRoute()
const store = useWorldStore()
const userStore = useUserStore()
const projectId = computed(() => route.params.id as string)
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
  // 非历史导航时，记录到历史栈（并截断 forward）
  if (!navigatingFromHistory) {
    const next = history.value.slice(0, historyIndex.value + 1)
    next.push(id)
    history.value = next
    historyIndex.value = next.length - 1
  }
  navigatingFromHistory = false

  const version = ++selectionVersion
  view.value = 'card'
  selectedCardId.value = id
  selectedCardDetail.value = null
  try {
    const detail = await store.fetchCardDetail(projectId.value, id, true)
    if (version === selectionVersion) selectedCardDetail.value = detail
  } catch {
    if (version === selectionVersion) ElMessage.error('加载卡片失败')
  }
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
  selectedCardId.value = null
  selectedCardDetail.value = null
  history.value = []
  historyIndex.value = -1
}
function openCreateDialog() {
  closePanel()
  selectedCardId.value = 'new'
  view.value = 'card'
}
function switchToGraph() {
  view.value = 'graph'
  closePanel()
}
function switchToTimeline() {
  view.value = 'timeline'
  closePanel()
}
async function loadData() {
  try { await store.fetchCards(projectId.value) } catch (error) { console.error('加载世界观失败:', error) }
}
function onCardChanged() {
  closePanel()
  void loadData()
  graphRef.value?.refresh()
}
const goBack = () => router.push('/world')
watch(projectId, () => {
  closePanel()
  view.value = 'card'
  searchQuery.value = ''
  void loadData()
}, { immediate: true })
onScopeDispose(() => { selectionVersion++ })
</script>

<style scoped>
* { box-sizing: border-box; }
.project-detail {
  height: calc(100dvh - 72px);
  min-height: 560px;
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
  gap: 8px;
  padding: 8px 16px;
  background: var(--bg-main);
  border-bottom: 1px solid var(--border-line);
}
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
  display: flex;
  min-height: 100%;
}
.doc-layout > :first-child {
  flex: 1;
  min-width: 0;
}
.doc-side-panel {
  width: 240px;
  flex-shrink: 0;
  border-left: 1px solid var(--border-line);
  padding: 24px 16px;
  background: var(--bg-main);
  position: sticky;
  top: 0;
  align-self: flex-start;
  max-height: 100%;
  overflow-y: auto;
}
.panel-title { margin: 0 0 16px; font-size: 13px; font-weight: 600; color: var(--text-secondary); text-transform: uppercase; letter-spacing: 1px; }
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
.panel-rel-title { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.panel-empty { font-size: 13px; color: var(--text-secondary); }
@media (max-width: 820px) {
  .doc-side-panel { display: none; }
}
.editor-view {
  height: 100%;
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
  padding: 24px clamp(16px, 3vw, 40px) 32px;
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
  .project-detail { height: auto; min-height: 0; overflow: visible; }
  .layout { overflow: visible; }
  .main { overflow: visible; }
  .graph-view { height: 65dvh; }
  .editor-body { overflow: visible; padding: 20px 16px; }
}
</style>
