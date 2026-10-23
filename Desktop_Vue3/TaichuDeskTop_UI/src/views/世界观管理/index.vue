<!-- src/views/柴圈板块/World/index.vue -->
<template>
  <div class="world-index" :class="{ 'detail-mode': !isListPage }">
    <div class="container">
      <!-- ===== 侧边栏（纯文字，无线条装饰） ===== -->
      <aside v-if="isListPage" class="sidebar">
        <div class="logo">世界观</div>
        <nav class="nav">
          <a
            class="nav-item"
            :class="{ active: activeMenu === 'projects' }"
            @click="switchMenu('projects')"
          >
            我的项目
            <span class="count">{{ store.projects.length }}</span>
          </a>
          <a
            class="nav-item"
            :class="{ active: activeMenu === 'public' }"
            @click="switchMenu('public')"
          >
            公开项目
            <span class="count">{{ store.publicProjects.length }}</span>
          </a>
        </nav>
        <div class="divider"></div>
        <div class="nav-meta">
          <span class="meta-label">版本</span>
          <span class="meta-value">v2.1</span>
        </div>
      </aside>

      <!-- ===== 主内容 ===== -->
      <main class="main">
        <!-- 顶部栏 -->
        <header v-if="isListPage" class="header">
          <div>
            <h1>{{ pageTitle }}</h1>
            <p>{{ pageSubtitle }}</p>
          </div>
          <div class="header-right">
            <div class="global-search">
              <input
                v-model="searchQuery"
                type="search"
                placeholder="全局搜索设定…"
                @input="onSearchInput"
              />
              <div v-if="searchResults.length" class="search-dropdown">
                <button
                  v-for="r in searchResults"
                  :key="r.cardId"
                  class="search-result"
                  @click="goToCard(r)"
                >
                  <span class="sr-type">{{ r.cardType }}</span>
                  <span class="sr-title">{{ r.cardTitle }}</span>
                  <span class="sr-project">{{ r.projectName }}</span>
                </button>
              </div>
            </div>
            <button v-if="activeMenu === 'projects'" class="btn-primary" @click="goToCreate">
              <span>+</span> 新建项目
            </button>
          </div>
        </header>

        <!-- 统计 -->
        <div v-if="isListPage && activeMenu === 'projects'" class="stats-line">
          <span>共 <strong>{{ store.projects.length }}</strong> 个项目</span>
          <span class="sep">·</span>
          <span>{{ store.projects.filter(p => p.isPublic).length }} 公开</span>
          <span class="sep">·</span>
          <span>{{ store.projects.filter(p => !p.isPublic).length }} 私有</span>
        </div>

        <!-- ===== 项目列表（表格风格） ===== -->
        <div v-if="isListPage" v-loading="loading" class="table-wrap">
          <div v-if="displayProjects.length" class="table">
            <div class="table-head">
              <span class="col-name">名称</span>
              <span class="col-status">状态</span>
              <span class="col-entries">条目</span>
              <span class="col-time">更新</span>
            </div>
            <div
              v-for="p in displayProjects"
              :key="p.id"
              class="table-row"
              @click="goToProject(p.id)"
            >
              <span class="col-name">{{ p.name }}</span>
              <span class="col-status">
                <button
                  v-if="activeMenu === 'projects'"
                  class="status-toggle"
                  :class="p.isPublic ? 'is-public' : 'is-private'"
                  :title="p.isPublic ? '点击切换为私有' : '点击切换为公开'"
                  @click.stop="togglePublic(p)"
                >
                  <span class="dot" :class="p.isPublic ? 'public' : 'private'"></span>
                  {{ p.isPublic ? '公开' : '私有' }}
                </button>
                <template v-else>
                  <span class="dot" :class="p.isPublic ? 'public' : 'private'"></span>
                  {{ p.isPublic ? '公开' : '私有' }}
                </template>
              </span>
              <span class="col-entries">{{ p.cardCount || 0 }}</span>
              <span class="col-time">{{ formatTime(p.updatedAt) }}</span>
            </div>
          </div>

          <!-- 空状态 -->
          <div v-else class="empty-state">
            <p>{{ emptyText }}</p>
            <button v-if="activeMenu === 'projects'" class="btn-outline" @click="goToCreate">
              + 创建第一个项目
            </button>
          </div>
        </div>

        <!-- 子路由 -->
        <router-view v-else />
      </main>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { useWorldStore } from '../../stores/world';
import { worldApi, type WorldSearchResult } from '@/api/worldApi';

const router = useRouter();
const route = useRoute();
const store = useWorldStore();

const activeMenu = ref('projects');
const loading = computed(() => activeMenu.value === 'projects' ? store.loadingProjects : store.loadingPublicProjects);

const isListPage = computed(() => route.path === '/world' || route.path === '/world/');

const pageTitle = computed(() => activeMenu.value === 'projects' ? '我的项目' : '公开项目');
const pageSubtitle = computed(() =>
  activeMenu.value === 'projects' ? '管理你创造的世界' : '发现其他创作者的世界'
);
const emptyText = computed(() =>
  activeMenu.value === 'projects' ? '还没有项目，开始创建吧' : '暂无公开项目'
);

const displayProjects = computed(() =>
  activeMenu.value === 'projects' ? store.projects : store.publicProjects
);

const formatTime = (dateStr: string) => {
  const d = new Date(dateStr);
  const now = new Date();
  const diff = Math.floor((now.getTime() - d.getTime()) / 1000);
  if (diff < 60) return '刚刚';
  if (diff < 3600) return Math.floor(diff / 60) + '分钟前';
  if (diff < 86400) return Math.floor(diff / 3600) + '小时前';
  if (diff < 604800) return Math.floor(diff / 86400) + '天前';
  return d.toLocaleDateString('zh-CN');
};

const switchMenu = (menu: string) => {
  if (menu === activeMenu.value) return;
  activeMenu.value = menu;
  loadData();
};

// ===== 全局搜索 =====
const searchQuery = ref('')
const searchResults = ref<WorldSearchResult[]>([])
let searchTimer: ReturnType<typeof setTimeout> | null = null

function onSearchInput() {
  if (searchTimer) clearTimeout(searchTimer)
  const q = searchQuery.value.trim()
  if (!q) { searchResults.value = []; return }
  searchTimer = setTimeout(async () => {
    try {
      searchResults.value = await worldApi.searchCards(q, 10)
    } catch {
      searchResults.value = []
    }
  }, 300)
}

function goToCard(r: WorldSearchResult) {
  searchResults.value = []
  searchQuery.value = ''
  router.push(`/world/project/${r.projectId}/card/${r.cardId}`)
}

const loadData = async () => {
  try {
    if (activeMenu.value === 'projects') await store.fetchProjects();
    else await store.fetchPublicProjects();
  } catch (error) {
    console.error('加载世界观项目失败:', error);
  }
};

const goToProject = (id: string) => router.push(`/world/project/${id}`);
const goToCreate = () => router.push('/world/create');

const togglingId = ref<string | null>(null);
const togglePublic = async (p: any) => {
  if (togglingId.value) return;
  togglingId.value = p.id;
  try {
    await store.updateProject(p.id, { isPublic: !p.isPublic });
  } finally {
    togglingId.value = null;
  }
};

watch(() => route.path, (newPath) => {
  if (newPath === '/world' || newPath === '/world/') loadData();
});

onMounted(loadData);
</script>

<style scoped>
* { box-sizing: border-box; }

.world-index {
  min-height: 0;
  background: var(--paper-card);
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', sans-serif;
  color: var(--text-primary);
  padding: 32px 40px;
}

.container {
  max-width: 100%;
  margin: 0 auto;
  display: flex;
  gap: 48px;
  align-items: flex-start;
}

/* ===== 侧边栏 ===== */
.sidebar {
  flex: 0 0 160px;
  position: sticky;
  top: 32px;
  padding: 0;
}

.logo {
  font-size: 18px;
  font-weight: 600;
  color: var(--text-primary);
  letter-spacing: -0.3px;
  padding-bottom: 20px;
  border-bottom: 1px solid var(--border-line);
  margin-bottom: 16px;
}

.nav {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.nav-item {
  display: flex;
  justify-content: space-between;
  padding: 6px 8px;
  border-radius: 6px;
  color: var(--text-secondary);
  font-size: 14px;
  font-weight: 450;
  cursor: pointer;
  transition: background 0.12s;
  text-decoration: none;
}
.nav-item:hover {
  background: var(--bg-sub);
  color: var(--text-primary);
}
.nav-item.active {
  background: var(--accent-soft);
  color: var(--accent-color);
}
.nav-item .count {
  font-size: 12px;
  color: var(--text-secondary);
}
.nav-item.active .count {
  color: var(--accent-color);
}

.divider {
  height: 1px;
  background: var(--bg-sub);
  margin: 16px 0;
}

.nav-meta {
  display: flex;
  gap: 8px;
  font-size: 12px;
  color: var(--text-secondary);
  padding: 0 8px;
}
.meta-label {
  color: var(--text-secondary);
}
.meta-value {
  color: var(--text-secondary);
}

/* ===== 主内容 ===== */
.main {
  flex: 1;
  min-width: 0;
}

.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}
.header h1 {
  font-size: 24px;
  font-weight: 600;
  margin: 0;
  color: var(--text-primary);
  letter-spacing: -0.3px;
}
.header p {
  margin: 2px 0 0;
  color: var(--text-secondary);
  font-size: 14px;
}
.header-right { display: flex; align-items: center; gap: 12px; }
.global-search { position: relative; }
.global-search input {
  width: 240px;
  padding: 7px 14px;
  border: 1px solid var(--border-line);
  border-radius: 8px;
  background: var(--paper-card);
  color: var(--text-primary);
  font-size: 13px;
}
.global-search input:focus { outline: none; border-color: var(--accent-color); }
.search-dropdown {
  position: absolute;
  top: calc(100% + 6px);
  left: 0;
  right: 0;
  z-index: 20;
  background: var(--paper-card);
  border: 1px solid var(--border-line);
  border-radius: 10px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.12);
  overflow: hidden;
  max-height: 360px;
  overflow-y: auto;
}
.search-result {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  padding: 10px 14px;
  border: none;
  background: none;
  cursor: pointer;
  text-align: left;
  border-bottom: 1px solid var(--border-line);
}
.search-result:last-child { border-bottom: none; }
.search-result:hover { background: var(--bg-sub); }
.sr-type {
  font-size: 11px;
  color: var(--accent-color);
  background: var(--accent-soft);
  padding: 1px 8px;
  border-radius: 4px;
  flex-shrink: 0;
}
.sr-title { flex: 1; min-width: 0; font-size: 13px; color: var(--text-primary); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.sr-project { font-size: 11px; color: var(--text-secondary); flex-shrink: 0; max-width: 120px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

.btn-primary {
  display: flex;
  align-items: center;
  gap: 6px;
  background: var(--accent-color);
  color: var(--on-accent);
  border: none;
  padding: 6px 16px;
  border-radius: 8px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s;
}
.btn-primary:hover {
  background: var(--text-primary);
}
.btn-primary span {
  font-size: 18px;
  line-height: 1;
}

.btn-outline {
  background: transparent;
  border: 1px solid var(--border-line);
  color: var(--text-secondary);
  padding: 6px 16px;
  border-radius: 8px;
  font-size: 13px;
  cursor: pointer;
  transition: 0.2s;
}
.btn-outline:hover {
  background: var(--bg-sub);
  border-color: var(--border-line);
}

/* ===== 统计行 ===== */
.stats-line {
  font-size: 13px;
  color: var(--text-secondary);
  margin-bottom: 16px;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--border-line);
}
.stats-line strong {
  font-weight: 600;
  color: var(--text-primary);
}
.stats-line .sep {
  color: var(--text-secondary);
  margin: 0 8px;
}

/* ===== 表格 ===== */
.table-wrap {
  background: var(--paper-card);
  border-radius: 12px;
  border: 1px solid var(--border-line);
  overflow: hidden;
}

.table {
  width: 100%;
}
.table-head {
  display: grid;
  grid-template-columns: 3fr 1fr 80px 120px;
  padding: 8px 16px;
  background: var(--bg-main);
  font-size: 12px;
  font-weight: 500;
  color: var(--text-secondary);
  border-bottom: 1px solid var(--border-line);
  letter-spacing: 0.3px;
  text-transform: uppercase;
}

.table-row {
  display: grid;
  grid-template-columns: 3fr 1fr 80px 120px;
  padding: 10px 16px;
  font-size: 14px;
  color: var(--text-primary);
  cursor: pointer;
  border-bottom: 1px solid var(--border-line);
  transition: background 0.1s;
}
.table-row:hover {
  background: var(--paper-card);
}
.table-row:last-child {
  border-bottom: none;
}

.col-name {
  font-weight: 500;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.col-status {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: var(--text-secondary);
}
.col-status .dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  flex-shrink: 0;
}
.dot.public {
  background: var(--success);
}
.dot.private {
  background: var(--text-secondary);
}
.status-toggle {
  display: flex;
  align-items: center;
  gap: 6px;
  background: none;
  border: 1px solid transparent;
  border-radius: 6px;
  padding: 2px 8px;
  font-size: 13px;
  color: var(--text-secondary);
  cursor: pointer;
  transition: background 0.15s, border-color 0.15s;
}
.status-toggle:hover {
  background: var(--bg-sub);
  border-color: var(--border-line);
}
.status-toggle.is-public {
  color: var(--success);
}
.col-entries {
  font-size: 13px;
  color: var(--text-secondary);
}
.col-time {
  font-size: 13px;
  color: var(--text-secondary);
}

/* ===== 空状态 ===== */
.empty-state {
  padding: 48px 20px;
  text-align: center;
  color: var(--text-secondary);
}
.empty-state p {
  font-size: 15px;
  margin: 0 0 16px;
}

/* ===== 响应式 ===== */
@media (max-width: 768px) {
  .world-index { padding: 16px; }
  .container { flex-direction: column; gap: 24px; }
  .sidebar {
    flex: none;
    width: 100%;
    position: static;
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 12px;
    border-bottom: 1px solid var(--border-line);
    padding-bottom: 12px;
  }
  .logo { border: none; padding: 0; margin: 0; }
  .nav { flex-direction: row; }
  .nav-item { font-size: 13px; padding: 4px 10px; }
  .nav-item .count { display: none; }
  .divider, .nav-meta { display: none; }
  .table-head, .table-row {
    grid-template-columns: 2fr 1fr 60px;
  }
  .col-time { display: none; }
}
.world-index.detail-mode { padding: 0; background: transparent; }
.detail-mode .container { display: block; }
</style>
