import { createApp } from 'vue'
import App from './App.vue'
import router from './router' // 引入刚才创建的路由
import './style.css'


const app = createApp(App)

app.use(router) 
app.mount('#app')