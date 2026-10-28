<template>
  <div class="admin-layout">
    <aside class="admin-sidebar">
      <div class="sidebar-brand">
        <span class="logo-text">管理中心</span>
        
      </div>
      <nav class="nav-group">
        <button type="button"
          v-for="menu in menus" :key="menu.id"
          class="nav-item" :class="{ 'is-active': activeTab === menu.id }"
          @click="activeTab = menu.id"
        >
          <span class="nav-icon" v-if="menu.icon">{{ menu.icon }}</span>
          <span class="nav-label">{{ menu.label }}</span>
          </button>
      </nav>
      <div class="sidebar-footer">
        <div class="admin-info">
          <span class="name">管理员</span>
          <span class="role">内容与社区治理</span>
        </div>
      </div>
    </aside>

    <main class="admin-main">
      <header class="main-header">
        <div class="breadcrumb">
          <span class="root">管理后台</span>
          <span class="separator">/</span>
          <span class="current">{{ activeMenuLabel }}</span>
        </div>
      </header>

      <section class="content-body">
        <transition name="fade-transform" mode="out-in">
          <component v-if="currentView" :is="currentView" />
        </transition>
      </section>
    </main>
  </div>
</template>

<script setup lang="ts">
import { useUserStore } from '@/stores/user'
import { ref, computed, watch} from 'vue';
import 交易行组件 from './管理组件/交易行组件.vue';
import 活动组件 from './管理组件/活动组件.vue';
import 反馈组件 from './管理组件/反馈组件.vue';
import 公告组件 from './管理组件/公告组件.vue';
import 维基组件 from './管理组件/维基组件.vue';
// 🌟 1. 新增：引入用户治理组件
import 用户组件 from './管理组件/用户组件.vue'; 
import 作品管理 from './管理组件/作品管理.vue';
// 🌟 新增：俱乐部游戏管理组件
import 俱乐部游戏组件 from './管理组件/俱乐部游戏组件.vue';
import 邮件管理 from './管理组件/邮件管理.vue';
import 数据中心 from './管理组件/数据中心.vue';
import 用户手册组件 from './管理组件/用户手册组件.vue';





const activeTab = ref('data');

// 🌟 2. 新增：将用户组件注册到左侧导航菜单
const user = useUserStore()
const allMenus = [
  { id: 'data', label: '数据', icon: '◱', component: 数据中心 },
  { id: 'trade', label: '交易行', icon: '◈', component: 交易行组件 },
  { id: 'users', label: '用户', icon: '⚇', component: 用户组件 },
  { id: 'manual', label: '手册', icon: '📖', component: 用户手册组件 },
  { id: 'event', label: '活动', icon: '◒', component: 活动组件 },
  { id: 'feedback', label: '反馈', icon: '✉', component: 反馈组件 },
  { id: 'news', label: '公告', icon: '☖', component: 公告组件 },
  { id: 'email', label: '邮件', icon: '＠', component: 邮件管理 },
  { id: 'wiki', label: '维基', icon: '▤', component: 维基组件 },
  { id: 'club', label: '俱乐部', icon: '🎮', component: 俱乐部游戏组件 }, // 🌟 新增
  { id: 'product', label: '作品', icon: '▤', component: 作品管理 },
];
const modulePermissions: Record<string,string> = { data: 'System_Monitor', trade: 'Trade_Manage', users: 'User_Audit', product: 'Trade_Manage', wiki: 'Wiki_Editor' }
const menus = computed(() => allMenus.filter(menu => user.permissions.includes('SuperAdmin') || (modulePermissions[menu.id] && user.permissions.includes(modulePermissions[menu.id]))))
watch(menus, items => { if (!items.some(item => item.id === activeTab.value)) activeTab.value = items[0]?.id || '' }, { immediate: true })
const currentView = computed(() => {
  const menu = menus.value.find(m => m.id === activeTab.value);
  return menu?.component; // 建议兜底与 activeTab 默认值一致
});

const activeMenuLabel = computed(() => {
  return menus.value.find(m => m.id === activeTab.value)?.label || '';
});
</script>

<style scoped>
.admin-layout { display: grid; grid-template-columns: 168px minmax(0, 1fr); gap: 24px; color: var(--text-primary); align-items: start; }
.admin-sidebar { position: sticky; top: 0; padding: 16px; background: var(--paper-card); border: 1px solid var(--border-line); border-radius: var(--radius-md); }
.sidebar-brand { font-size: 18px; font-weight: 700; padding: 8px 10px 20px; }
.nav-group { display: grid; gap: 4px; }
.nav-item { display: flex; align-items: center; gap: 10px; padding: 11px 12px; width: 100%; background: transparent; color: var(--text-secondary); border: none; border-radius: var(--radius-sm); text-align: left; cursor: pointer; }
.nav-item:hover { background: var(--bg-sub); }
.nav-item.is-active { background: var(--accent-soft); color: var(--accent-color); font-weight: 600; }
.sidebar-footer { margin-top: 20px; padding: 14px 10px 0; border-top: 1px solid var(--border-line); }
.admin-info { display: grid; gap: 6px; font-size: 12px; color: var(--text-secondary); }
.admin-main, .content-body { min-width: 0; }
.main-header { padding: 12px 0 20px; border-bottom: 1px solid var(--border-line); margin-bottom: 24px; }
.breadcrumb { display: flex; gap: 12px; font-size: 14px; }
.breadcrumb .root, .separator { color: var(--text-secondary); }
.content-body :deep(table) { width: 100%; }
.content-body :deep(.table-container), .content-body :deep(.table-wrapper) { overflow-x: auto; }
.fade-transform-enter-active, .fade-transform-leave-active { transition: opacity .15s; }
.fade-transform-enter-from, .fade-transform-leave-to { opacity: 0; }
@media (max-width: 1100px) { .admin-layout { grid-template-columns: minmax(0, 1fr); gap: 16px; } .admin-sidebar { position: static; padding: 10px; } .sidebar-brand, .sidebar-footer { display: none; } .nav-group { display: flex; overflow-x: auto; } .nav-item { flex-shrink: 0; width: auto; white-space: nowrap; } }
</style>
