<template>
  <div class="club-game-manager">
    <header class="module-header">
      <div class="header-content">
        <h2 class="page-title">太初俱乐部中枢</h2>
        <p class="md-subtitle">陪玩游戏注册、动态字段编排与打手资质审核</p>
      </div>
    </header>

    <nav class="md-tabs">
      <span
        v-for="tab in TABS"
        :key="tab.id"
        class="tab-item"
        :class="{ active: activeTab === tab.id }"
        @click="activeTab = tab.id"
      >
        {{ tab.label }}
      </span>
    </nav>

    <section class="tab-panel">
      <component :is="currentComponent" />
    </section>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';
import 游戏库组件 from './俱乐部组件/游戏库组件.vue';
import 字段编排组件 from './俱乐部组件/字段编排组件.vue';
import 订单类型组件 from './俱乐部组件/订单类型组件.vue';
import 打手审核组件 from './俱乐部组件/打手审核组件.vue';
import 订单管理 from './俱乐部组件/订单管理.vue';
import 钱包审核组件 from './俱乐部组件/钱包审核组件.vue';
const TABS = [
  { id: 'games',      label: '游戏库',   component: 游戏库组件 },
  { id: 'fields',     label: '字段编排', component: 字段编排组件 },
  { id: 'orderTypes', label: '订单类型', component: 订单类型组件 },
  { id: 'operators',  label: '打手审核', component: 打手审核组件 },
  { id: 'orders',     label: '订单管理', component: 订单管理 },
  { id: 'wallet',     label: '钱包审核', component: 钱包审核组件 },  // ⭐ 新增
] as const;

type TabId = (typeof TABS)[number]['id'];
const activeTab = ref<TabId>('games');

const currentComponent = computed(() =>
  TABS.find(t => t.id === activeTab.value)?.component
);
</script>

<style scoped>
.club-game-manager { display: flex; flex-direction: column; animation: slideIn 0.35s cubic-bezier(0.16, 1, 0.3, 1); }
.module-header { margin-bottom: 30px; }
.page-title { font-size: 1.6rem; font-weight: 700; color: #111; margin: 0; }
.md-subtitle { font-size: 0.85rem; color: #888; margin: 6px 0 0; }

.md-tabs { display: flex; gap: 32px; border-bottom: 1px solid #f2f2f7; margin-bottom: 24px; }
.tab-item {
  cursor: pointer; color: #86868b; padding-bottom: 12px;
  font-size: 0.95rem; font-weight: 600;
  transition: all 0.2s ease; position: relative;
}
.tab-item.active { color: #111; }
.tab-item.active::after {
  content: ''; position: absolute; left: 0; right: 0; bottom: -1px;
  height: 2px; background: #111;
}

.tab-panel { animation: fadeIn 0.3s ease; }

@keyframes slideIn { from { opacity: 0; transform: translateY(8px); } to { opacity: 1; transform: translateY(0); } }
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
</style>