import { createRouter, createWebHashHistory } from 'vue-router'

const router = createRouter({
  history: createWebHashHistory(), 
  routes: [
    {
      path: '/',
      name: 'root',
      component: () => import('../views/推送首页/index.vue')
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
      props: true // 这样可以通过 props 直接获取 url 里的 id
    },
    
    {
      path: '/Project/project/:id',
      name: 'ProjectDetail',
      component: () => import('../views/太初协作/协作组件/ProjectDetail.vue'), // 确保路径指向你刚创建的文件
      meta: { requiresAuth: true }
    },
    {
    path: '/codex/:id', // 访问路径，例如 /codex/3170cadf...
    name: 'WikiDetail', // 🌟 必须和 index.vue 里的名字完全一致
    component: () => import('../views/灵脉百科/components/WikiDetail.vue'), // 详情页组件路径
    props: true // 允许将 id 直接作为 props 传给组件
    },
      // 添加到你的 router/index.ts 的 routes 数组中
    {
      path: '/activity',
      component: () => import('@/views/活动中心/index.vue'),
      children: [
        { 
          path: '', 
          component: () => import('@/views/活动中心/活动中心组件/ActivityHome.vue') 
        },
        { 
          path: 'detail/:id', 
          component: () => import('@/views/活动中心/活动中心组件/ActivityDetail.vue') 
        },
        { 
          path: 'create', 
          component: () => import('@/views/活动中心/活动中心组件/CreateActivity.vue') 
        },
        {
          path: 'my',
          component: () => import('@/views/活动中心/活动中心组件/MyActivities.vue')
        }
      ]
    }
  ]
})

router.beforeEach((to, from, next) => {
  const hasToken = !!localStorage.getItem('token');
  
  // 拦截逻辑
  if (to.meta.requiresAuth && !hasToken) {
    next('/LoginRegister'); 
  } else {
    next();
  }
});

export default router