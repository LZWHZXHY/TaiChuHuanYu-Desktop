<template>
  <div class="world-index" :class="{ 'detail-mode': !isListPage, 'content-page': isListPage }">
    <template v-if="isListPage">
      <header class="world-heading content-header">
        <div><p class="eyebrow">共同想象 · 世界观</p><h1>世界观</h1><p>{{ userStore.isLoggedIn ? '从一个人物、一处地点开始，慢慢写出你的世界。' : '走进创作者的世界，阅读人物、地点与故事设定。' }}</p></div>
        <div class="world-heading-actions content-actions">
          <RouterLink to="/novels" class="ghost-link">📖 小说书架</RouterLink>
          <button class="primary" @click="router.push('/world/create')">＋ 创建世界</button>
        </div>
      </header>
      <div class="world-toolbar">
        <nav aria-label="世界观范围">
          <button v-if="userStore.isLoggedIn" :aria-pressed="activeMenu === 'projects'" @click="switchMenu('projects')">我的世界</button>
          <button :aria-pressed="activeMenu === 'public'" @click="switchMenu('public')">公开世界</button>
        </nav>
        <div class="global-search">
          <input v-model="searchQuery" type="search" aria-label="搜索世界观设定" placeholder="搜索人物、地点或其他设定" @input="onSearchInput" @keydown.esc="clearSearch" />
          <div v-if="searchQuery.trim()" class="search-dropdown" aria-live="polite">
            <p v-if="searching">正在搜索…</p>
            <p v-else-if="searchError">搜索失败，请修改关键词或重试。</p>
            <template v-else-if="searchResults.length">
              <button v-for="r in searchResults" :key="r.cardId" @click="goToCard(r)">
                <strong>{{ r.cardTitle }}</strong><span>{{ r.projectName }} · {{ getCardTypeLabel(r.cardType) }}</span>
              </button>
            </template>
            <p v-else>没有找到匹配的设定</p>
          </div>
        </div>
      </div>
      <section :aria-busy="loading" aria-label="世界观列表">
        <div v-if="loadError" class="empty-state" role="alert"><p>世界观加载失败，已有内容仍然保留。</p><button @click="loadData">重新加载</button></div>
        <p v-else-if="loading && !displayProjects.length" class="empty-state">正在加载世界观…</p>
        <div v-if="displayProjects.length" class="world-grid">
          <article v-for="p in displayProjects" :key="p.id" class="world-card">
            <RouterLink class="world-link" :to="`/world/project/${p.id}`">
              <div class="world-cover">
                <img v-if="p.coverImage" :src="p.coverImage" :alt="p.name" loading="lazy" />
                <span v-else class="world-mark" aria-hidden="true">{{ p.name.slice(0, 1) }}</span>
              </div>
              <h2>{{ p.name }}</h2><p>{{ p.description || '这个世界的故事，正等待被写下。' }}</p>
            </RouterLink>
            <footer><span>{{ p.cardCount || 0 }} 个条目 · {{ formatTime(p.updatedAt) }}</span>
              <button v-if="activeMenu === 'projects'" :disabled="Boolean(togglingId)" :aria-label="`${p.name}：${p.isPublic ? '改为私有' : '改为公开'}`" @click="togglePublic(p)">{{ togglingId === p.id ? '保存中…' : p.isPublic ? '公开' : '私有' }}</button>
              <span v-else>{{ p.ownerName || '创作者' }}</span>
            </footer>
          </article>
        </div>
        <div v-else-if="!loading && !loadError" class="empty-state">
          <h2>{{ activeMenu === 'projects' ? '你的世界，从一个想法开始' : '还没有公开的世界' }}</h2>
          <p>{{ activeMenu === 'projects' ? '先起个名字，设定可以随时补充。默认只有你自己可见。' : '创作者公开世界后，就可以在这里阅读。' }}</p>
          <button v-if="activeMenu === 'projects'" @click="router.push('/world/create')">创建第一个世界</button>
        </div>
      </section>
    </template>
    <router-view v-else />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onScopeDispose } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useWorldStore } from '@/stores/world'
import { useUserStore } from '@/stores/user'
import { worldApi, type WorldSearchResult } from '@/api/worldApi'
import type { WorldProject } from '@/api/types/world'
import { formatWorldUpdatedAt as formatTime, getCardTypeLabel } from '@/utils/worldPresentation'
const router = useRouter()
const route = useRoute()
const store = useWorldStore()
const userStore = useUserStore()
const activeMenu = ref<'projects' | 'public'>(userStore.isLoggedIn && route.query.tab !== 'public' ? 'projects' : 'public')
const isListPage = computed(() => /^\/world\/?$/.test(route.path))
const loading = computed(() => activeMenu.value === 'projects' ? store.loadingProjects : store.loadingPublicProjects)
const displayProjects = computed(() => activeMenu.value === 'projects' ? store.projects : store.publicProjects)
const loadError = ref(false)
let loadVersion = 0
async function loadData() {
  const version = ++loadVersion
  loadError.value = false
  try {
    if (activeMenu.value === 'projects') await store.fetchProjects()
    else await store.fetchPublicProjects()
  } catch { if (version === loadVersion) loadError.value = true }
}
function switchMenu(menu: 'projects' | 'public') {
  if (menu === 'projects' && !userStore.isLoggedIn) return
  if (menu === activeMenu.value) return
  activeMenu.value = menu
  void loadData()
}
const searchQuery = ref('')
const searchResults = ref<WorldSearchResult[]>([])
const searching = ref(false)
const searchError = ref(false)
let searchTimer: ReturnType<typeof setTimeout> | undefined
let searchVersion = 0
function onSearchInput() {
  clearTimeout(searchTimer)
  const version = ++searchVersion
  const query = searchQuery.value.trim()
  searchResults.value = []
  searchError.value = false
  searching.value = Boolean(query)
  if (!query) return
  searchTimer = setTimeout(async () => {
    try {
      const results = await worldApi.searchCards(query, 10)
      if (version === searchVersion) searchResults.value = results
    } catch { if (version === searchVersion) searchError.value = true }
    finally { if (version === searchVersion) searching.value = false }
  }, 300)
}
function clearSearch() { searchQuery.value = ''; onSearchInput() }
function goToCard(result: WorldSearchResult) {
  clearSearch()
  router.push(`/world/project/${result.projectId}/card/${result.cardId}`)
}
const togglingId = ref<string | null>(null)
async function togglePublic(project: WorldProject) {
  if (togglingId.value) return
  togglingId.value = project.id
  try { await store.updateProject(project.id, { isPublic: !project.isPublic }) }
  catch { ElMessage.error('可见性修改失败，请重试') }
  finally { togglingId.value = null }
}
watch([isListPage, () => route.query.tab], ([isList]) => {
  if (!isList) return
  if (route.query.tab === 'public') activeMenu.value = 'public'
  void loadData()
}, { immediate: true })
watch(() => userStore.token, () => {
  if (!userStore.isLoggedIn) activeMenu.value = 'public'
  clearSearch()
  if (isListPage.value) void loadData()
})
onScopeDispose(() => { clearTimeout(searchTimer); searchVersion++; loadVersion++ })
</script>

<style scoped>
.world-index { padding: 0 0 40px; max-width: 1200px; margin: auto; color: var(--text-primary); }
.world-heading, .world-toolbar, .world-card footer { display: flex; align-items: center; justify-content: space-between; gap: 16px; }
.world-heading { margin-bottom: 32px; }
h1 { font-size: clamp(26px, 3vw, 36px); margin: 0 0 10px; }
.world-heading p, .empty-state p { color: var(--text-secondary); margin: 0; line-height: 1.7; }
button, input { font: inherit; }
button { cursor: pointer; border: 1px solid var(--border-line); border-radius: var(--radius-sm); padding: 9px 16px; background: var(--paper-card); color: var(--text-primary); }
button:disabled { cursor: wait; opacity: .6; }
button:focus-visible, a:focus-visible, input:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 3px; }
.primary { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); white-space: nowrap; }
.world-toolbar { margin-bottom: 24px; flex-wrap: wrap; }
.world-toolbar nav { display: flex; gap: 6px; }
.world-toolbar nav button { border-color: transparent; background: transparent; }
.world-toolbar button[aria-pressed=true] { background: var(--inverse-bg); color: var(--inverse-text); }
.global-search { position: relative; width: min(360px, 100%); }
.global-search input { box-sizing: border-box; width: 100%; padding: 11px 14px; border: 1px solid var(--border-line); border-radius: var(--radius-sm); background: var(--paper-card); color: var(--text-primary); }
.search-dropdown { position: absolute; inset: calc(100% + 6px) 0 auto; z-index: 20; max-height: 360px; overflow: auto; background: var(--paper-card); border: 1px solid var(--border-line); border-radius: var(--radius-sm); box-shadow: 0 12px 30px #0002; }
.search-dropdown button { display: grid; gap: 4px; width: 100%; text-align: left; border: 0; border-radius: 0; }
.search-dropdown button:hover { background: var(--bg-sub); }
.search-dropdown span, .search-dropdown p { color: var(--text-secondary); font-size: 13px; }
.search-dropdown p { padding: 12px; }
.world-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 300px), 1fr)); gap: 20px; }
.world-card { min-width: 0; background: var(--paper-card); border: 1px solid var(--border-line); border-radius: var(--radius-sm); overflow: hidden; }
.world-card:hover { border-color: var(--accent-color); }
.world-link { display: block; padding: 16px 16px 20px; text-decoration: none; color: inherit; }
.world-cover { aspect-ratio: 16 / 9; border-radius: var(--radius-sm); overflow: hidden; background: var(--bg-sub); display: grid; place-items: center; margin-bottom: 16px; }
.world-cover img { width: 100%; height: 100%; object-fit: cover; display: block; }
.world-mark { display: grid; place-items: center; width: 44px; height: 44px; border-radius: var(--radius-sm); background: var(--accent-soft); color: var(--accent-color); font-size: 22px; }
.world-link h2 { font-size: 19px; margin: 0 0 8px; overflow-wrap: anywhere; }
.world-link p { color: var(--text-secondary); line-height: 1.8; height: 3.6em; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; margin: 0; }
.world-card footer { padding: 12px 24px; border-top: 1px solid var(--border-line); color: var(--text-secondary); font-size: 12px; flex-wrap: wrap; }
.world-card footer button { padding: 3px 10px; font-size: 12px; }
.empty-state { padding: 56px 20px; text-align: center; border: 1px dashed var(--border-line); border-radius: var(--radius-sm); margin-bottom: 20px; }
.empty-state h2 { font-size: 21px; }.empty-state button { margin-top: 20px; }
.detail-mode { padding: 0; max-width: none; }
@media(max-width: 600px) { .world-heading { align-items: flex-start; flex-direction: column; gap: 20px; }.global-search { width: 100%; } }
.world-heading-actions { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
.ghost-link { padding: 9px 18px; border: 1px solid var(--border-line); border-radius: var(--radius-sm); background: var(--paper-card); color: var(--text-primary); text-decoration: none; font-size: .85rem; white-space: nowrap; }
.ghost-link:hover { border-color: var(--accent-color); color: var(--accent-color); }
</style>
