import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router' // 引入刚才创建的路由
import { setupAuthSession } from './router/auth'
import './assets/styles/base.css';
import './assets/styles/content.css';
import './views/柴圈板块/detail.css';
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
import './assets/styles/variables.css'
import './assets/styles/theme-rhine.css'
import { setupTheme } from './composables/useTheme'
const disposeTheme = setupTheme()
if (import.meta.hot) import.meta.hot.dispose(disposeTheme)

// 太初俱乐部主题（样式已限定在 .page-shell 内，不影响其他模块）
import '@/views/太初俱乐部/theme.css'

const app = createApp(App)
const pinia = createPinia() // 创建实例

app.use(pinia) // 插件注册
const disposeAuthSession = setupAuthSession(pinia, router)
if (import.meta.hot) import.meta.hot.dispose(disposeAuthSession)
app.use(router)
app.use(ElementPlus)
app.mount('#app')
