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