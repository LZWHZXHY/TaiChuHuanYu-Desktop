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

// ===== 网站更新后旧页面打不开的自动恢复 =====
// 每次发布新版本，页面拆出来的小文件名字都会变。还开着旧页面（或者浏览器缓存了旧首页）的人，
// 点到没打开过的页面时会去要旧名字的文件，服务器回的是网页而不是脚本，就报
// 「Failed to fetch dynamically imported module」，页面打不开。
// 遇到这种情况自动刷新一次拿新版本；30 秒内只刷一次，防止一直刷。
const CHUNK_RELOAD_KEY = 'tc-chunk-reload-at'
const isChunkError = (err: unknown) =>
  /dynamically imported module|Importing a module script failed|error loading dynamically imported module|Failed to load module script|ChunkLoadError/i
    .test(String((err as Error)?.message ?? err))
function reloadForNewVersion(targetPath?: string) {
  try {
    const last = Number(sessionStorage.getItem(CHUNK_RELOAD_KEY) || 0)
    if (Date.now() - last < 30000) return false
    sessionStorage.setItem(CHUNK_RELOAD_KEY, String(Date.now()))
  } catch { /* 存不了也照样刷新一次 */ }
  // 刷新后直接打开本来要去的页面
  if (targetPath) history.replaceState(history.state, '', `#${targetPath}`)
  location.reload()
  return true
}
window.addEventListener('vite:preloadError', event => { if (reloadForNewVersion()) event.preventDefault() })
router.onError((err, to) => { if (isChunkError(err)) reloadForNewVersion(to?.fullPath) })
app.use(ElementPlus)
app.mount('#app')
