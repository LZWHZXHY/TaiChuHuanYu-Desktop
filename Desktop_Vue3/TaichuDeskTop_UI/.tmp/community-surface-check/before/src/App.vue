<script setup lang="ts">
import { ref, onMounted, computed, watch, nextTick } from 'vue'
import { useRouter} from 'vue-router' // 🌟 引入 useRoute
import request from './utils/request' 
import OperationTerminal from './components/OperationTerminal.vue'
import { useUserStore } from './stores/user'
import GlobalNotify from './components/GlobalNotify.vue'
import NotificationBell from './components/NotificationBell.vue'

// 插件接口定义
interface Plugin {
  name: string;
  url: string;
  icon: string;
  requiresAuth: boolean;
}
const userStore = useUserStore()
const router = useRouter()
const viewportElement = ref<HTMLElement>()
watch(() => router.currentRoute.value.path, async () => {
  await nextTick()
  viewportElement.value?.scrollTo({ top: 0 })
})
// const route = useRoute() // 🌟 获取当前路由状态
const navigationPending = ref(''), navigationError = ref('')
let lastNavigation: Pick<Plugin, 'name' | 'url'> | null = null
let navigationVersion = 0
const allPlugins = ref<Plugin[]>([]) 
const activeMenu = computed(() => String(router.currentRoute.value.name || ''))
const corePlugins: Plugin[] = [
  { name: '太初灵脉', url: '/lingmai/note', icon: '·', requiresAuth: true },
  { name: '推送首页', url: '/', icon: '⌂', requiresAuth: false },
  { name: '画廊展厅', url: '/gallery', icon: '▧', requiresAuth: false },
  { name: '太初随笔', url: '/stream', icon: '≡', requiresAuth: false },
  { name: '知识库', url: '/knowledge', icon: '▤', requiresAuth: false },
  { name: '开发进度', url: '/dev-progress', icon: '▣', requiresAuth: false },
  { name: '赞助墙', url: '/sponsors', icon: '♡', requiresAuth: false },
  { name: '必吃榜', url: '/must-eat', icon: '🍽', requiresAuth: false },
  { name: '版本更新', url: '/changelog', icon: '⏱', requiresAuth: false },
  { name: '社区规则', url: '/rules', icon: '📜', requiresAuth: false },
  { name: '太初俱乐部', url: '/Club', icon: '◇', requiresAuth: false },
  { name: '柴圈板块', url: '/community', icon: '◎', requiresAuth: false },
  { name: '荣誉墙', url: '/honor', icon: '☆', requiresAuth: false },
  { name: '太虚游', url: '/games', icon: '⊞', requiresAuth: false },
]

const visiblePlugins = computed(() => {
  
  return [...new Map([...corePlugins, ...allPlugins.value].map(item => [item.name, item])).values()].filter(item => {
    const hiddenItems = ['身份认证', '个人中心', '灵脉百科'];
    if (hiddenItems.includes(item.name)) return false;
    return !item.requiresAuth || userStore.isLoggedIn;
  }).map(item => item.name === '知识库' ? { ...item, url: '/knowledge' } : item.name === '太初灵脉' ? { ...item, url: '/lingmai/note' } : item.name === '管理面板' ? { ...item, url: '/admin' } : item);
})



const fetchPlugins = async () => {
  try {
    // 🌟 修复点：只保留一个泛型参数，代表返回的是 Plugin 数组
    const res = await request.get<Plugin[]>('/Plugins');
    
    // 因为你在 request.ts 拦截器里已经剥离了 .data，所以 res 直接就是数组
    const pluginList = Array.isArray(res) ? res : [];
    allPlugins.value = pluginList;
    
    // 注册路由逻辑保持不变
    pluginList.forEach((item: Plugin) => {
      if (item.name === '管理面板' && item.url.startsWith('/') && !router.getRoutes().some(route => route.path.toLowerCase() === item.url.toLowerCase())) {
        router.addRoute({ path: item.url, redirect: '/admin', meta: { requiresAuth: true, requiresAdmin: true } })
      }
      if (item.url !== '/' && !item.url.startsWith('http')) {
        if (!router.hasRoute(item.name) && !router.getRoutes().some(route => route.path === item.url)) {
          router.addRoute({
            path: item.url,
            name: item.name,
            component: () => import(`./views/${item.name}/index.vue`),
            meta: { requiresAuth: item.requiresAuth, requiresAdmin: ['管理面板', '用户治理中枢'].includes(item.name) } 
          });
        }
      }
    });

    console.log('灵脉插件加载成功:', pluginList.length);

    if (router.currentRoute.value.matched.length === 0) {
      console.log("检测到冷启动路径，正在重新解析灵脉路由...");
      await router.replace(router.currentRoute.value.fullPath);
    }

  } catch (error) {
    console.error("云端灵脉读取失败:", (error as any).friendlyMessage || error);
  }
}

// 处理从“操作终端”组件传回的导航指令
const handleNavigation = async (item: Pick<Plugin, 'name' | 'url'>) => {
  lastNavigation = item
  const version = ++navigationVersion
  navigationPending.value = item.name
  navigationError.value = ''
  try {
    if (item.url.startsWith('http')) {
      const host = (window as any).chrome?.webview
      if (host) host.postMessage({ cmd: 'load_external_url', url: item.url })
      else window.open(item.url, '_blank', 'noopener,noreferrer')
    } else await router.push(item.url)
  } catch {
    if (version === navigationVersion) navigationError.value = '页面加载失败，请重试。若刚更新过网站，请刷新后再打开。'
  } finally { if (version === navigationVersion) navigationPending.value = '' }
}

onMounted(async ()=> {
  // 初次懒加载完成前 currentRoute 还是起始占位，不能据此重新导航到首页。
  await router.isReady();
  // 🌟 【关键修复点 2】使用 await 确保插件加载（addRoute）完成后再往后走
  await fetchPlugins();

  if (userStore.isLoggedIn) {
    await userStore.fetchUserInfo(true);
    console.log('灵脉数据已同步');
  }
  
  (window as any).receivePlugins = (data: Plugin[]) => {
    console.log("收到 WPF 指令:", data);
    if(data && data.length > 0) allPlugins.value = data;
  }
})
</script>

<template>
  <div class="app-shell">
    <OperationTerminal 
      :menuItems="visiblePlugins" 
      :activeName="activeMenu"
      @navigate="handleNavigation"
    />
    <main class="container">
      <div class="site-toolbar">
        <span class="site-wordmark">TAICHU HUANYU <span aria-hidden="true">/</span> COMMUNITY</span>
        <div class="site-tools">
          <RouterLink to="/knowledge" class="site-search"><svg aria-hidden="true" viewBox="0 0 20 20" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.4"><circle cx="8.5" cy="8.5" r="5.5" /><path d="m13 13 4 4" /></svg>检索社区内容</RouterLink>
          <NotificationBell />
        </div>
      </div>
      <div v-if="navigationPending" class="navigation-state" role="status">正在打开 {{ navigationPending }}…</div>
      <div v-if="navigationError" class="navigation-state" role="alert">{{ navigationError }} <button @click="lastNavigation && handleNavigation(lastNavigation)">重试</button></div>
      <p v-if="router.currentRoute.value.query.access === 'denied'" class="navigation-state" role="alert">当前账号没有管理权限。</p>
      
      
      <section ref="viewportElement" class="viewport">
        <div class="viewport-content">
          <RouterView /> 
          <GlobalNotify/>
        </div>
      </section>
    </main>
  </div>
</template>

<style scoped>
.navigation-state { margin: 0; padding: 12px 20px; color: var(--text-primary); background: var(--accent-soft); font-size: 14px; }
.app-shell { display: flex; width: 100%; height: 100dvh; background: var(--bg-main); color: var(--text-primary); overflow: hidden; }
.container { flex: 1; display: flex; flex-direction: column; min-width: 0; }
.site-toolbar { display: flex; align-items: center; justify-content: space-between; gap: 16px; flex-shrink: 0; min-height: 52px; padding: 8px clamp(20px, 3vw, 48px); border-bottom: 1px solid var(--border-line); }
.site-wordmark { font-family: var(--font-mono); font-size: 10px; letter-spacing: .12em; color: var(--text-secondary); }
.site-wordmark span { margin-inline: 12px; color: var(--accent-color); }
.site-tools { display: flex; align-items: center; gap: 16px; }
.site-search { display: inline-flex; align-items: center; gap: 8px; color: var(--text-secondary); font-size: 12px; text-decoration: none; }
.site-search:hover { color: var(--accent-color); }
.viewport { flex: 1; min-height: 0; padding: 28px clamp(20px, 3vw, 48px); overflow: auto; scrollbar-gutter: stable; }
.viewport-content { width: 100%; max-width: 1440px; margin-inline: auto; }
@media(max-width: 900px) {
  .app-shell { flex-direction: column; }
  .container { min-height: 0; }
  .viewport { padding: 20px 16px; }
  .site-toolbar { min-height: 40px; padding: 6px 16px; }
  .site-wordmark { font-size: 9px; letter-spacing: .02em; }
  .site-wordmark span { margin-inline: 4px; }
}
@media(max-width: 420px) { .site-wordmark { display: none; }.site-tools { width: 100%; justify-content: space-between; } }
/* 编辑工作区占满剩余空间，滚动由内部文档承接。 */
.viewport:has(:deep(.spirit-link-app)) { padding: 16px; overflow: hidden; }
.viewport-content:has(:deep(.spirit-link-app)) { height: 100%; }
@media (min-width: 821px) {
  .viewport:has(:deep(.world-index.detail-mode)) { padding: 16px; overflow: hidden; }
  .viewport-content:has(:deep(.world-index.detail-mode)), :deep(.world-index.detail-mode) { height: 100%; }
}
</style>
