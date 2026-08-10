<!-- src/views/柴圈板块/World/index.vue -->
<template>
  <div class="world-index">
    <div class="container">
      <!-- ===== 侧边栏（纯文字，无线条装饰） ===== -->
      <aside class="sidebar">
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
          <span class="meta-value">v0.1</span>
        </div>
      </aside>

      <!-- ===== 主内容 ===== -->
      <main class="main">
        <!-- 顶部栏 -->
        <header class="header">
          <div>
            <h1>{{ pageTitle }}</h1>
            <p>{{ pageSubtitle }}</p>
          </div>
          <button v-if="activeMenu === 'projects'" class="btn-primary" @click="goToCreate">
            <span>+</span> 新建项目
          </button>
        </header>

        <!-- 统计 -->
        <div v-if="activeMenu === 'projects'" class="stats-line">
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
                <span class="dot" :class="p.isPublic ? 'public' : 'private'"></span>
                {{ p.isPublic ? '公开' : '私有' }}
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

const router = useRouter();
const route = useRoute();
const store = useWorldStore();

const activeMenu = ref('projects');
const loading = ref(false);

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

const loadData = async () => {
  loading.value = true;
  try {
    if (activeMenu.value === 'projects') await store.fetchProjects();
    else await store.fetchPublicProjects();
  } finally {
    loading.value = false;
  }
};

const goToProject = (id: string) => router.push(`/world/project/${id}`);
const goToCreate = () => router.push('/world/create');

watch(() => route.path, (newPath) => {
  if (newPath === '/world' || newPath === '/world/') loadData();
});

onMounted(loadData);
</script>

<style scoped>
* { box-sizing: border-box; }

.world-index {
  min-height: 100vh;
  background: #fafbfc;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', sans-serif;
  color: #1e293b;
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
  color: #0f172a;
  letter-spacing: -0.3px;
  padding-bottom: 20px;
  border-bottom: 1px solid #eef2f6;
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
  color: #64748b;
  font-size: 14px;
  font-weight: 450;
  cursor: pointer;
  transition: background 0.12s;
  text-decoration: none;
}
.nav-item:hover {
  background: #f1f5f9;
  color: #0f172a;
}
.nav-item.active {
  background: #eef2ff;
  color: #4f46e5;
}
.nav-item .count {
  font-size: 12px;
  color: #94a3b8;
}
.nav-item.active .count {
  color: #4f46e5;
}

.divider {
  height: 1px;
  background: #eef2f6;
  margin: 16px 0;
}

.nav-meta {
  display: flex;
  gap: 8px;
  font-size: 12px;
  color: #94a3b8;
  padding: 0 8px;
}
.meta-label {
  color: #c0c4cc;
}
.meta-value {
  color: #64748b;
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
  color: #0f172a;
  letter-spacing: -0.3px;
}
.header p {
  margin: 2px 0 0;
  color: #94a3b8;
  font-size: 14px;
}

.btn-primary {
  display: flex;
  align-items: center;
  gap: 6px;
  background: #0f172a;
  color: #fff;
  border: none;
  padding: 6px 16px;
  border-radius: 8px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s;
}
.btn-primary:hover {
  background: #1e293b;
}
.btn-primary span {
  font-size: 18px;
  line-height: 1;
}

.btn-outline {
  background: transparent;
  border: 1px solid #e2e8f0;
  color: #475569;
  padding: 6px 16px;
  border-radius: 8px;
  font-size: 13px;
  cursor: pointer;
  transition: 0.2s;
}
.btn-outline:hover {
  background: #f1f5f9;
  border-color: #cbd5e1;
}

/* ===== 统计行 ===== */
.stats-line {
  font-size: 13px;
  color: #94a3b8;
  margin-bottom: 16px;
  padding-bottom: 12px;
  border-bottom: 1px solid #eef2f6;
}
.stats-line strong {
  font-weight: 600;
  color: #1e293b;
}
.stats-line .sep {
  color: #e2e8f0;
  margin: 0 8px;
}

/* ===== 表格 ===== */
.table-wrap {
  background: #fff;
  border-radius: 12px;
  border: 1px solid #eef2f6;
  overflow: hidden;
}

.table {
  width: 100%;
}
.table-head {
  display: grid;
  grid-template-columns: 3fr 1fr 80px 120px;
  padding: 8px 16px;
  background: #f8fafc;
  font-size: 12px;
  font-weight: 500;
  color: #94a3b8;
  border-bottom: 1px solid #eef2f6;
  letter-spacing: 0.3px;
  text-transform: uppercase;
}

.table-row {
  display: grid;
  grid-template-columns: 3fr 1fr 80px 120px;
  padding: 10px 16px;
  font-size: 14px;
  color: #1e293b;
  cursor: pointer;
  border-bottom: 1px solid #f4f6f8;
  transition: background 0.1s;
}
.table-row:hover {
  background: #fafbfc;
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
  color: #64748b;
}
.col-status .dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  flex-shrink: 0;
}
.dot.public {
  background: #22c55e;
}
.dot.private {
  background: #94a3b8;
}
.col-entries {
  font-size: 13px;
  color: #64748b;
}
.col-time {
  font-size: 13px;
  color: #94a3b8;
}

/* ===== 空状态 ===== */
.empty-state {
  padding: 48px 20px;
  text-align: center;
  color: #94a3b8;
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
    border-bottom: 1px solid #eef2f6;
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
</style>