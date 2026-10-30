<template>
  <div class="admin-layout" :class="{ 'workbench-skin': activeTab !== 'club' }">
    <header class="admin-heading">
      <div><p class="workbench-eyebrow">ADMINISTRATION / 社区治理</p><h1>管理中心</h1></div>
      <p class="admin-identity">{{ user.userInfo?.username || '管理员' }}<span>仅显示当前账号有权限的模块</span></p>
    </header>
    <nav class="admin-modules" aria-label="管理模块">
      <button v-for="menu in menus" :key="menu.id" type="button" class="nav-item" :class="{ 'is-active': activeTab === menu.id }" :aria-pressed="activeTab === menu.id" @click="activeTab = menu.id">
        <span aria-hidden="true">{{ menu.icon }}</span>{{ menu.label }}
      </button>
    </nav>
    <main class="admin-main">
      <div class="module-caption"><span>{{ activeMenuLabel }}</span><span aria-hidden="true">CONTROL / WORKSPACE</span></div>
      <section class="content-body">
        <transition name="fade-transform" mode="out-in"><component v-if="currentView" :is="currentView" /></transition>
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
.admin-layout { max-width: 1440px; margin: auto; color: var(--text-primary); }
.admin-heading { display: flex; justify-content: space-between; align-items: end; gap: 24px; padding: 24px 0; border-top: 2px solid var(--text-primary); }
.workbench-eyebrow { font: 11px var(--font-mono); letter-spacing: .12em; color: var(--accent-color); margin-bottom: 12px; }
h1 { font-size: clamp(28px, 3vw, 40px); letter-spacing: -.04em; line-height: 1.2; }
.admin-identity { font-size: 13px; text-align: right; }.admin-identity span { display: block; margin-top: 7px; color: var(--text-secondary); font-size: 12px; }
.admin-modules { display: flex; flex-wrap: wrap; gap: 4px; padding: 8px 0; border-block: 1px solid var(--border-line); margin-bottom: 24px; }
.nav-item { position: relative; display: inline-flex; gap: 8px; align-items: center; padding: 12px 16px; border: 0; border-radius: 2px; background: transparent; color: var(--text-secondary); font-size: 13px; cursor: pointer; }
.nav-item::after { content: ''; position: absolute; bottom: 0; left: 0; right: 0; height: 3px; background: var(--signal-color); transform: scaleX(0); transform-origin: left; transition: transform .2s; }
.nav-item:is(:hover,:focus-visible) { background: var(--bg-sub); color: var(--text-primary); }.nav-item:is(:hover,:focus-visible)::after, .nav-item.is-active::after { transform: scaleX(1); }
.nav-item.is-active { background: var(--inverse-bg); color: var(--inverse-text); }
.admin-main, .content-body { min-width: 0; }.module-caption { display: flex; justify-content: space-between; align-items: center; padding-bottom: 12px; font-size: 13px; }.module-caption > :last-child { font: 10px var(--font-mono); letter-spacing: .12em; color: var(--text-secondary); }
.content-body :deep(table) { width: 100%; }.content-body :deep(.table-container), .content-body :deep(.table-wrapper) { overflow-x: auto; }
.fade-transform-enter-active, .fade-transform-leave-active { transition: opacity .15s; }.fade-transform-enter-from, .fade-transform-leave-to { opacity: 0; }
@media(max-width: 700px) { .admin-heading { align-items: start; flex-direction: column; gap: 14px; }.admin-identity { text-align: left; }.admin-modules { flex-wrap: nowrap; overflow-x: auto; }.nav-item { white-space: nowrap; flex-shrink: 0; padding: 10px 14px; }.module-caption > :last-child { display: none; } }
</style>
