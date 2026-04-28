import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router' // 引入刚才创建的路由
import './style.css'


const app = createApp(App)
const pinia = createPinia() // 创建实例

app.use(pinia) // 插件注册
app.use(router) 
app.mount('#app')