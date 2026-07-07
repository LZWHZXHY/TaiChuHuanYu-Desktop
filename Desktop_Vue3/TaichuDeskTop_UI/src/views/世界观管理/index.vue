<template>
  <div class="world-index">
    <div class="container">
      <!-- 侧边栏 -->
      <aside class="sidebar">
        <div class="logo">
          <span class="logo-icon">✦</span>
          <span class="logo-text">世界观</span>
        </div>

        <nav class="nav">
          <a
            class="nav-item"
            :class="{ active: activeMenu === 'projects' }"
            @click="switchMenu('projects')"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/>
            </svg>
            <span>我的项目</span>
            <span v-if="store.projects.length" class="badge">{{ store.projects.length }}</span>
          </a>
          <a
            class="nav-item"
            :class="{ active: activeMenu === 'public' }"
            @click="switchMenu('public')"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="10"/>
              <line x1="2" y1="12" x2="22" y2="12"/>
              <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"/>
            </svg>
            <span>公开项目</span>
            <span v-if="store.publicProjects.length" class="badge">{{ store.publicProjects.length }}</span>
          </a>
        </nav>

        <div class="footer-info">
          <span>✨ 社区共创</span>
        </div>
      </aside>

      <!-- 主区域 -->
      <main class="main">
        <!-- 顶部栏（仅在列表页显示） -->
        <header v-if="isListPage" class="header">
          <div>
            <h1>{{ pageTitle }}</h1>
            <p>{{ pageSubtitle }}</p>
          </div>
          <button
            v-if="activeMenu === 'projects'"
            class="btn-primary"
            @click="goToCreate"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
              <line x1="12" y1="5" x2="12" y2="19"/>
              <line x1="5" y1="12" x2="19" y2="12"/>
            </svg>
            新建项目
          </button>
        </header>

        <!-- 统计条（仅在列表页显示） -->
        <div v-if="isListPage && activeMenu === 'projects'" class="stats">
          <span class="stat-item">
            <strong>{{ store.projects.length }}</strong> 总项目
          </span>
          <span class="stat-divider"></span>
          <span class="stat-item">
            <strong>{{ store.projects.filter(p => p.isPublic).length }}</strong> 公开
          </span>
          <span class="stat-divider"></span>
          <span class="stat-item">
            <strong>{{ store.projects.filter(p => !p.isPublic).length }}</strong> 私有
          </span>
        </div>

        <!-- ===== 核心：路由视图 ===== -->
        <!-- 在列表页显示项目网格，在创建页显示 <router-view> -->
        <div v-if="isListPage" class="grid" v-loading="loading">
          <div
            v-for="(p, index) in displayProjects"
            :key="p.id"
            class="card"
            :style="{ animationDelay: `${index * 50}ms` }"
            @click="goToProject(p.id)"
          >
            <div class="card-header">
              <h3>{{ p.name }}</h3>
              <span class="tag" :class="p.isPublic ? 'public' : 'private'">
                {{ p.isPublic ? '公开' : '私有' }}
              </span>
            </div>
            <p class="desc">{{ p.description || '还没有描述' }}</p>
            <div class="card-footer">
              <span class="meta">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="16" height="16">
                  <rect x="2" y="3" width="20" height="18" rx="2" ry="2"/>
                  <line x1="8" y1="21" x2="16" y2="21"/>
                  <line x1="12" y1="17" x2="12" y2="21"/>
                </svg>
                {{ p.cardCount || 0 }} 个条目
              </span>
              <span v-if="activeMenu === 'public'" class="author">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="16" height="16">
                  <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/>
                  <circle cx="12" cy="7" r="4"/>
                </svg>
                {{ p.ownerName || '匿名' }}
              </span>
              <span class="time">{{ formatTime(p.updatedAt) }}</span>
            </div>
          </div>

          <!-- 空状态 -->
          <div v-if="!loading && displayProjects.length === 0" class="empty">
            <div class="empty-icon">📭</div>
            <p>{{ emptyText }}</p>
            <button v-if="activeMenu === 'projects'" class="btn-outline" @click="goToCreate">
              创建第一个项目
            </button>
          </div>
        </div>

        <!-- 子路由渲染（创建页、详情页等） -->
        <router-view v-else />
      </main>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { ElMessage } from 'element-plus';
import { useWorldStore } from '../../stores/world';

const router = useRouter();
const route = useRoute();
const store = useWorldStore();

// 当前激活菜单
const activeMenu = ref('projects');

// 加载状态
const loading = ref(false);

// 判断当前是否在列表页（/world 或 /world/projects）
const isListPage = computed(() => {
  return route.path === '/world' || route.path === '/world/';
});

// 页面标题
const pageTitle = computed(() => {
  return activeMenu.value === 'projects' ? '我的项目' : '公开项目';
});

const pageSubtitle = computed(() => {
  return activeMenu.value === 'projects'
    ? '管理你创造的世界'
    : '发现其他创作者的世界';
});

const emptyText = computed(() => {
  return activeMenu.value === 'projects'
    ? '还没有项目，开始创建吧'
    : '暂无公开项目';
});

// 显示的项目列表
const displayProjects = computed(() => {
  if (activeMenu.value === 'projects') {
    return store.projects;
  } else {
    return store.publicProjects;
  }
});

// 格式化时间
const formatTime = (dateStr: string) => {
  const d = new Date(dateStr);
  const now = new Date();
  const diff = Math.floor((now.getTime() - d.getTime()) / 1000);
  if (diff < 60) return '刚刚';
  if (diff < 3600) return Math.floor(diff / 60) + ' 分钟前';
  if (diff < 86400) return Math.floor(diff / 3600) + ' 小时前';
  if (diff < 604800) return Math.floor(diff / 86400) + ' 天前';
  return d.toLocaleDateString('zh-CN');
};

// 切换菜单
const switchMenu = (menu: string) => {
  if (menu === activeMenu.value) return;
  activeMenu.value = menu;
  loadData();
};

// 加载数据
const loadData = async () => {
  loading.value = true;
  try {
    if (activeMenu.value === 'projects') {
      await store.fetchProjects();
    } else {
      await store.fetchPublicProjects();
    }
  } catch (e) {
    console.error(e);
  } finally {
    loading.value = false;
  }
};

// 跳转到项目详情
const goToProject = (id: string) => {
  router.push(`/world/project/${id}`);
};

// 跳转到创建页
const goToCreate = () => {
  router.push('/world/create');
};

// 监听路由变化，回到列表页时刷新数据
watch(
  () => route.path,
  (newPath) => {
    if (newPath === '/world' || newPath === '/world/') {
      loadData();
    }
  }
);

// 初始化加载
onMounted(() => {
  loadData();
});
</script>

<style scoped>
/* ===== 全局重置 ===== */
* {
  box-sizing: border-box;
}
.world-index {
  min-height: 100vh;
  background: #f8f9fc;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', sans-serif;
  color: #1e293b;
  padding: 24px;
}

.container {
  max-width: 1280px;
  margin: 0 auto;
  display: flex;
  gap: 32px;
  align-items: flex-start;
}

/* ===== 侧边栏 ===== */
.sidebar {
  flex: 0 0 200px;
  position: sticky;
  top: 24px;
  background: white;
  border-radius: 20px;
  padding: 28px 16px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.02);
  border: 1px solid rgba(0, 0, 0, 0.03);
  transition: box-shadow 0.2s;
}

.logo {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 0 12px 24px 12px;
  border-bottom: 1px solid #f1f3f5;
  margin-bottom: 24px;
}
.logo-icon {
  font-size: 22px;
  line-height: 1;
}
.logo-text {
  font-weight: 600;
  font-size: 18px;
  letter-spacing: -0.3px;
}

.nav {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.nav-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 14px;
  border-radius: 12px;
  color: #64748b;
  text-decoration: none;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 15px;
  font-weight: 500;
}
.nav-item svg {
  width: 20px;
  height: 20px;
  flex-shrink: 0;
}
.nav-item:hover {
  background: #f1f5f9;
  color: #1e293b;
}
.nav-item.active {
  background: #eef2ff;
  color: #4f46e5;
}
.nav-item .badge {
  margin-left: auto;
  background: #e2e8f0;
  color: #475569;
  font-size: 12px;
  font-weight: 600;
  padding: 0 8px;
  border-radius: 10px;
  height: 20px;
  line-height: 20px;
}
.nav-item.active .badge {
  background: #c7d2fe;
  color: #4f46e5;
}

.footer-info {
  margin-top: 32px;
  padding-top: 20px;
  border-top: 1px solid #f1f3f5;
  font-size: 13px;
  color: #94a3b8;
  text-align: center;
}

/* ===== 主内容 ===== */
.main {
  flex: 1;
  min-width: 0;
}

.header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 28px;
}
.header h1 {
  font-size: 28px;
  font-weight: 600;
  margin: 0 0 4px 0;
  letter-spacing: -0.5px;
}
.header p {
  margin: 0;
  color: #64748b;
  font-size: 15px;
}

.btn-primary {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  background: #4f46e5;
  color: white;
  border: none;
  padding: 10px 20px;
  border-radius: 12px;
  font-size: 15px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
  box-shadow: 0 2px 8px rgba(79, 70, 229, 0.2);
}
.btn-primary:hover {
  background: #4338ca;
  transform: translateY(-1px);
  box-shadow: 0 6px 16px rgba(79, 70, 229, 0.25);
}
.btn-primary:active {
  transform: scale(0.97);
}
.btn-primary svg {
  width: 20px;
  height: 20px;
  stroke: currentColor;
}

.btn-outline {
  display: inline-block;
  background: transparent;
  border: 1px solid #d1d5db;
  color: #374151;
  padding: 8px 18px;
  border-radius: 10px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}
.btn-outline:hover {
  background: #f3f4f6;
  border-color: #9ca3af;
}

/* ===== 统计 ===== */
.stats {
  display: flex;
  gap: 24px;
  background: white;
  padding: 14px 24px;
  border-radius: 16px;
  margin-bottom: 28px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.02);
  border: 1px solid #f1f3f5;
}
.stat-item {
  font-size: 14px;
  color: #64748b;
}
.stat-item strong {
  font-weight: 600;
  color: #1e293b;
}
.stat-divider {
  width: 1px;
  background: #e9edf2;
}

/* ===== 网格 ===== */
.grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 20px;
}

/* ===== 卡片 ===== */
.card {
  background: white;
  border-radius: 18px;
  padding: 22px 24px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.02);
  border: 1px solid #f1f3f5;
  cursor: pointer;
  transition: all 0.25s cubic-bezier(0.25, 0.46, 0.45, 0.94);
  display: flex;
  flex-direction: column;
  opacity: 0;
  animation: fadeUp 0.4s ease forwards;
}

@keyframes fadeUp {
  0% { opacity: 0; transform: translateY(12px); }
  100% { opacity: 1; transform: translateY(0); }
}

.card:hover {
  transform: translateY(-4px);
  box-shadow: 0 12px 30px rgba(0, 0, 0, 0.04);
  border-color: #dbe0e8;
}
.card:active {
  transform: scale(0.98);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 12px;
  margin-bottom: 8px;
}
.card-header h3 {
  margin: 0;
  font-size: 17px;
  font-weight: 600;
  color: #0f172a;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.tag {
  font-size: 11px;
  font-weight: 500;
  padding: 2px 10px;
  border-radius: 20px;
  flex-shrink: 0;
  height: 22px;
  line-height: 22px;
}
.tag.public {
  background: #dcfce7;
  color: #16a34a;
}
.tag.private {
  background: #f1f3f5;
  color: #64748b;
}

.desc {
  margin: 6px 0 14px 0;
  font-size: 14px;
  line-height: 1.5;
  color: #475569;
  flex: 1;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.card-footer {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px 16px;
  margin-top: 14px;
  padding-top: 14px;
  border-top: 1px solid #f1f3f5;
  font-size: 13px;
  color: #94a3b8;
}
.card-footer .meta,
.card-footer .author {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}
.card-footer .author {
  color: #4f46e5;
}
.card-footer .time {
  margin-left: auto;
  font-size: 12px;
}

/* ===== 空状态 ===== */
.empty {
  grid-column: 1 / -1;
  text-align: center;
  padding: 60px 20px;
}
.empty-icon {
  font-size: 48px;
  margin-bottom: 16px;
}
.empty p {
  color: #94a3b8;
  font-size: 16px;
  margin: 0 0 20px 0;
}

/* ===== router-view 容器 ===== */
.main > :deep(.router-view) {
  /* 让子路由内容占满 main 区域 */
  width: 100%;
}

/* ===== 响应式 ===== */
@media (max-width: 768px) {
  .world-index { padding: 12px; }
  .container { flex-direction: column; gap: 16px; }
  .sidebar {
    flex: none;
    width: 100%;
    position: static;
    padding: 16px;
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 12px;
  }
  .logo { padding: 0; border: none; margin: 0; }
  .nav { flex-direction: row; flex: 1; justify-content: flex-end; }
  .nav-item { padding: 6px 12px; font-size: 14px; }
  .nav-item .badge { display: none; }
  .footer-info { display: none; }
  .header { flex-direction: column; align-items: stretch; gap: 12px; }
  .btn-primary { justify-content: center; }
  .grid { grid-template-columns: 1fr; }
  .stats { flex-wrap: wrap; gap: 12px; }
  .stat-divider { display: none; }
}
</style>