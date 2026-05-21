<template>
  <div class="admin-layout">
    <aside class="admin-sidebar">
      <div class="sidebar-brand">
        <span class="logo-text">太初</span>
        <span class="version">V2.0</span>
      </div>
      <nav class="nav-group">
        <div 
          v-for="menu in menus" :key="menu.id"
          class="nav-item" :class="{ 'is-active': activeTab === menu.id }"
          @click="activeTab = menu.id"
        >
          <span class="nav-icon">{{ menu.icon }}</span>
          <span class="nav-label">{{ menu.label }}</span>
          <div class="active-bar"></div>
        </div>
      </nav>
      <div class="sidebar-footer">
        <div class="admin-info">
          <span class="name">管理员</span>
          <span class="role">系统审计师</span>
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
        <div class="global-stats">
          <div class="stat-bubble hide-mobile">
            <span class="label">负载</span>
            <span class="value">2.4%</span>
          </div>
          <div class="stat-bubble">
            <span class="label">流量</span>
            <span class="value">1.2k</span>
          </div>
        </div>
      </header>

      <section class="content-body">
        <transition name="fade-transform" mode="out-in">
          <component :is="currentView" />
        </transition>
      </section>
    </main>
  </div>
</template>

<script setup lang="ts">
import { ref, computed} from 'vue';
import 交易行组件 from './管理组件/交易行组件.vue';
import 活动组件 from './管理组件/活动组件.vue';
import 反馈组件 from './管理组件/反馈组件.vue';
import 公告组件 from './管理组件/公告组件.vue';


const activeTab = ref('trade');
const menus = [
  { id: 'trade', label: '交易行', icon: '◈', component: 交易行组件 },
  { id: 'event', label: '活动', component:活动组件},
  { id: 'feedback', label: '反馈', component:反馈组件},
  { id: 'news', label: '公告', component: 公告组件}

];

const currentView = computed(() => {
  const menu = menus.find(m => m.id === activeTab.value);
  return menu ? menu.component : 交易行组件;
});

const activeMenuLabel = computed(() => {
  return menus.find(m => m.id === activeTab.value)?.label || '';
});
</script>

<style scoped>
.admin-layout { display: flex; width: 100%; height: 100%; overflow: hidden; background: #fcfcfc; }

/* 侧边栏：桌面端 */
.admin-sidebar {
  width: 240px; background: #fff; border-right: 1px solid #f0f0f0;
  display: flex; flex-direction: column; padding: 40px 0; z-index: 100;
}
.sidebar-brand { padding: 0 40px; margin-bottom: 60px; }
.logo-text { font-size: 1.5rem; font-weight: 200; letter-spacing: 4px; }
.nav-group { flex: 1; }
.nav-item { 
  height: 56px; display: flex; align-items: center; padding: 0 40px; 
  cursor: pointer; position: relative; color: #888; transition: 0.3s;
}
.nav-item.is-active { color: #111; font-weight: 600; }
.active-bar { position: absolute; right: 0; width: 3px; height: 20px; background: #111; opacity: 0; }
.nav-item.is-active .active-bar { opacity: 1; }
.sidebar-footer { padding: 0 40px; border-top: 1px solid #f9f9f9; padding-top: 24px; }

/* 主区域 */
.admin-main { flex: 1; display: flex; flex-direction: column; min-width: 0; overflow: hidden; }
.main-header {
  height: 80px; padding: 0 40px; display: flex; justify-content: space-between; align-items: center;
  background: rgba(255, 255, 255, 0.8); border-bottom: 1px solid #f0f0f0; backdrop-filter: blur(10px);
}
.content-body { flex: 1; padding: 40px; overflow-y: auto; }

/* 移动端适配 */
@media (max-width: 768px) {
  .admin-sidebar { position: fixed; bottom: 0; width: 100%; height: 60px; flex-direction: row; padding: 0; }
  .sidebar-brand, .sidebar-footer, .nav-label { display: none; }
  .nav-group { display: flex; width: 100%; }
  .nav-item { flex: 1; justify-content: center; height: 100%; padding: 0; }
  .active-bar { top: 0; bottom: auto; width: 30%; height: 3px; left: 35%; }
  .main-header { padding: 0 20px; }
  .content-body { padding: 20px; padding-bottom: 80px; }
  .hide-mobile { display: none; }
}
</style>