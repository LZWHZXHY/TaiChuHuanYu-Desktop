import { createRouter, createWebHashHistory } from 'vue-router'

// ===== 静态导入世界观相关组件（避免动态导入问题） =====
import WorldIndex from '@/views/世界观管理/index.vue'
import ProjectCreate from '@/views/世界观管理/ProjectCreate.vue'
import ProjectDetail from '@/views/世界观管理/ProjectDetail.vue'

// ===== 其他页面导入 =====
// 注意：如果你有其他页面的导入，请保留，这里只展示世界观部分

const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    {
      path: '/',
      name: 'root',
      component: () => import('../views/推送首页/index.vue')
    },

    // ===== 世界观模块 =====
    // ===== 世界观模块 =====
{
  path: '/world',
  name: 'WorldHome',
  component: WorldIndex,
  meta: { requiresAuth: true },
  children: [
    {
      path: 'create',
      name: 'ProjectCreate',
      component: ProjectCreate,
      meta: { requiresAuth: true }
    },
    {
      path: 'project/:id',
      name: 'ProjectDetail',
      component: ProjectDetail,
      meta: { requiresAuth: true }
    },
    // 👇 新增：卡片详情路由
    {
      path: 'project/:projectId/card/:cardId',
      name: 'CardDetail',
      component: () => import('@/views/世界观管理/世界观组件/CardDetail.vue'),
      meta: { requiresAuth: true }
    }
  ]
},

    // ===== 其他路由（保持不变） =====
    {
      path: '/lingmai',
      component: () => import('@/views/太初灵脉/SpiritLayout.vue'),
      meta: { requiresAuth: true },
      children: [
        { path: '', redirect: '/lingmai/note' },
        { path: 'note/:id?', name: 'SpiritNote', component: () => import('@/views/太初灵脉/components/NoteEditorView.vue'), props: true },
        { path: 'graph', name: 'SpiritGraph', component: () => import('@/views/太初灵脉/components/GraphView.vue'), props: true },
        { path: 'settings/:id', name: 'SpiritSettings', component: () => import('@/views/太初灵脉/components/NoteSettingsPanel.vue'), props: true },
        { path: 'wiki/:id', name: 'WikiEdit', component: () => import('@/views/太初灵脉/components/WorkspaceWiki.vue'), props: true }
      ]
    },
    {
      path: '/LoginRegister',
      name: '身份认证',
      component: () => import('../views/身份认证/index.vue')
    },
    {
      path: '/user/:id',
      name: 'UserCenter',
      component: () => import('../views/个人中心/index.vue'),
      props: true
    },
    {
      path: '/Project/project/:id',
      name: 'ProjectDetailCollab',   // 改名避免与世界观的路由名冲突
      component: () => import('../views/太初协作/协作组件/ProjectDetail.vue'),
      meta: { requiresAuth: true }
    },
    {
      path: '/codex/:id',
      name: 'WikiDetail',
      component: () => import('../views/灵脉百科/components/WikiDetail.vue'),
      props: true
    },
    {
      path: '/activity',
      component: () => import('@/views/活动中心/index.vue'),
      children: [
        { path: '', component: () => import('@/views/活动中心/活动中心组件/ActivityHome.vue') },
        { path: 'detail/:id', component: () => import('@/views/活动中心/活动中心组件/ActivityDetail.vue') },
        { path: 'create', component: () => import('@/views/活动中心/活动中心组件/CreateActivity.vue') },
        { path: 'my', component: () => import('@/views/活动中心/活动中心组件/MyActivities.vue') }
      ]
    }
    // 如果你有其他路由，继续添加
  ]
})

// ===== 打印所有注册的路由（用于调试） =====
console.log('✅ 已注册的路由路径：', router.getRoutes().map(r => r.path))

router.beforeEach((to, from, next) => {
  const hasToken = !!localStorage.getItem('token')
  if (to.meta.requiresAuth && !hasToken) {
    next('/LoginRegister')
  } else {
    next()
  }
})

export default router