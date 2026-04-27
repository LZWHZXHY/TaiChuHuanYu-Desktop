import { createRouter, createWebHashHistory } from 'vue-router'


const router = createRouter({
 
  history: createWebHashHistory(), 
  routes: [
    {
      path: '/',
      name: 'root',
      component: () => import('../views/推送首页/index.vue') // 确保这个文件存在
    }
  ]
})



export default router