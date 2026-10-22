<template>
  <div class="project-detail">
    <!-- ========== 顶部 ========== -->
    <ProjectHeader :project="project" :card-count="cards.length" :is-owner="isOwner" @back="goBack" @create="openCreateDialog" />

    <!-- ========== 主体 ========== -->
    <div class="layout">
      <!-- 左侧栏 -->
      <ProjectCardNavigation v-model:search="searchQuery" :cards="cards" :loading="store.loadingCards"
        :selected-card-id="selectedCardId" :view="view" @select="selectCard" @graph="switchToGraph" />

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

        <!-- 编辑器视图 -->
        <div
          v-else-if="isOwner && (isCreating || selectedCard)"
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

        <!-- 只读详情 -->
        <ProjectCardDocument v-else-if="selectedCardDetail" :key="selectedCardDetail.id"
          :card="selectedCardDetail" :project-id="projectId" @select="selectCard" />

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

const router = useRouter()
const route = useRoute()
const store = useWorldStore()
const userStore = useUserStore()
const projectId = computed(() => route.params.id as string)
const project = computed(() => store.currentProject)
const cards = computed(() => store.cards)
const isOwner = computed(() => Boolean(userStore.userId && userStore.userId === project.value?.ownerId))
const searchQuery = ref('')
const view = ref<'card' | 'graph'>('card')
const graphRef = ref<InstanceType<typeof RelationGraph> | null>(null)
const selectedCardId = ref<string | null>(null)
const selectedCardDetail = ref<CardDetail | null>(null)
const isCreating = computed(() => selectedCardId.value === 'new')
const selectedCard = computed(() => cards.value.find(card => card.id === selectedCardId.value) ?? null)
let selectionVersion = 0

async function selectCard(id: string) {
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
function closePanel() {
  selectionVersion++
  selectedCardId.value = null
  selectedCardDetail.value = null
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
.graph-view {
  height: 100%;
  padding: 20px;
  background: var(--paper-card);
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
