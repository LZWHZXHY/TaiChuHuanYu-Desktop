<script setup lang="ts">
import { ref, onMounted, computed, watch, nextTick } from 'vue'
import { useRouter} from 'vue-router' // 🌟 引入 useRoute
import request from './utils/request' 
import OperationTerminal from './components/OperationTerminal.vue'
import { useUserStore } from './stores/user'
import { useVisibleMotion } from '@/composables/useVisibleMotion'
import { useSurfaceDepth } from '@/composables/useSurfaceDepth'
import { useScrollParallax } from '@/composables/useScrollParallax'
import { pingPresence, startPresence } from '@/utils/presence'
import { setupSiteBackground } from '@/composables/useSiteBackground'
import SiteBackdrop from './components/SiteBackdrop.vue'
import { setupUiSound } from '@/utils/uiSound'
const shellElement = ref<HTMLElement>()
useSurfaceDepth(shellElement)
const atmosphereElement = ref<SVGElement>()
const atmosphereMotion = useVisibleMotion(atmosphereElement)
import GlobalNotify from './components/GlobalNotify.vue'
import ChatNotifier from './components/ChatNotifier.vue'
import ParticleField from './components/ParticleField.vue'
import ParticleBurst from './components/ParticleBurst.vue'
import NotificationBell from './components/NotificationBell.vue'
import OnlineIndicator from './components/OnlineIndicator.vue'
import { startPlannerReminders } from '@/composables/usePlannerReminders'

// 插件接口定义
interface Plugin {
  name: string;
  url: string;
  icon: string;
  requiresAuth: boolean;
}
const userStore = useUserStore()
const router = useRouter()
// 生活管理的浏览器弹窗提醒：登录后每分钟看一次有没有快到点的安排（只对用过生活管理的人生效）
startPlannerReminders(router)
// 用户自定义背景图：替换纯色底色，网格、粒子、线条特效保留在上面
setupSiteBackground()
// 全站界面音效（实验室仪器风格，侧栏可关闭）
setupUiSound(router)
const isCommunityRoute = computed(() => !/^\/(club|taichu)(\/|$)/i.test(router.currentRoute.value.path))
const viewportElement = ref<HTMLElement>()
// 滚动视差：页头装饰与带 data-parallax 的元素随滚动以不同速度移动；换页后重新计算。
const parallax = useScrollParallax(viewportElement)
watch(() => router.currentRoute.value.fullPath, () => { void nextTick(() => window.setTimeout(parallax.refresh, 60)) })
watch(() => router.currentRoute.value.path, async () => {
  pingPresence() // 换页也算一次“在线”（15 秒内不重复发送）
  await nextTick()
  viewportElement.value?.scrollTo({ top: 0 })
})
// const route = useRoute() // 🌟 获取当前路由状态
const navigationPending = ref(''), navigationError = ref('')
let lastNavigation: Pick<Plugin, 'name' | 'url'> | null = null
let navigationVersion = 0
const allPlugins = ref<Plugin[]>([]) 
const activeMenu = computed(() => String(router.currentRoute.value.name || ''))
// 状态栏里的定位码，只做装饰展示，不参与导航。
const locatorPath = computed(() => {
  const path = router.currentRoute.value.path
  return path === '/' ? '/INDEX' : path.toUpperCase().slice(0, 32)
})
const corePlugins: Plugin[] = [
  { name: '太初灵脉', url: '/lingmai/note', icon: '·', requiresAuth: true },
  { name: '推送首页', url: '/', icon: '⌂', requiresAuth: false },
  { name: '画廊展厅', url: '/gallery', icon: '▧', requiresAuth: false },
  { name: '年度最佳', url: '/awards', icon: '✦', requiresAuth: false },
  { name: '太初随笔', url: '/stream', icon: '≡', requiresAuth: false },
  { name: '聊天室', url: '/chat', icon: '◍', requiresAuth: false },
  { name: '社区成员', url: '/members', icon: '◉', requiresAuth: false },
  { name: '岁月史书', url: '/history', icon: '📖', requiresAuth: false },
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
  { name: '交易行', url: '/trade', icon: '⇄', requiresAuth: true },
]

const visiblePlugins = computed(() => {
  
  return [...new Map([...corePlugins, ...allPlugins.value].map(item => [item.name, item])).values()].filter(item => {
    const hiddenItems = ['身份认证', '个人中心', '灵脉百科'];
    if (hiddenItems.includes(item.name)) return false;
    return !item.requiresAuth || userStore.isLoggedIn;
  }).map(item => item.name === '知识库' ? { ...item, url: '/knowledge' } : item.name === '太初灵脉' ? { ...item, url: '/lingmai/note' } : item.name === '管理面板' ? { ...item, url: '/admin' } : item.name === '交易行' ? { ...item, url: '/trade', requiresAuth: true } : item);
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
      // 插件表里交易行的旧地址跳到固定的 /trade
      if (item.name === '交易行' && item.url.startsWith('/') && item.url.toLowerCase() !== '/trade' && !router.getRoutes().some(route => route.path.toLowerCase() === item.url.toLowerCase())) {
        router.addRoute({ path: item.url, redirect: '/trade' })
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
  // 在线心跳：用于管理后台的在线人数统计
  startPresence()
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
  <div ref="shellElement" class="app-shell" :class="{ 'community-design': !/^\/(club|taichu)(\/|$)/i.test(router.currentRoute.value.path) }">
    <!-- 用户自定义背景（图片 / 视频）：铺满整个窗口（包括左侧导航栏后面），没设置时不渲染，保持默认坐标纸 -->
    <SiteBackdrop v-if="isCommunityRoute" />
    <OperationTerminal 
      :menuItems="visiblePlugins" 
      :activeName="activeMenu"
      @navigate="handleNavigation"
    />
    <ParticleBurst />
    <main class="container">
      <svg v-if="!/^\/(club|taichu)(\/|$)/i.test(router.currentRoute.value.path)" ref="atmosphereElement" class="app-atmosphere" :class="{ 'motion-paused': !atmosphereMotion }" viewBox="0 0 1200 900" preserveAspectRatio="xMidYMid slice" aria-hidden="true" focusable="false">
        <!-- 洁净实验室底图：定位十字、标尺刻度与少量折线电路，不使用有机曲线。 -->
        <g class="ambient-registration" fill="none" stroke="var(--text-primary)" opacity=".22">
          <path d="M60 60H96M78 42V78M1104 60H1140M1122 42V78M60 840H96M78 822V858M1104 840H1140M1122 822V858" />
          <circle cx="78" cy="60" r="9" /><circle cx="1122" cy="60" r="9" /><circle cx="78" cy="840" r="9" /><circle cx="1122" cy="840" r="9" />
        </g>
        <g class="ambient-ruler" stroke="var(--text-primary)" opacity=".16">
          <path v-for="n in 59" :key="n" :d="`M${n * 20} 0V${n % 5 === 0 ? 14 : 6}`" />
        </g>
        <path class="ambient-plane" d="M880 -160H1220L780 280H440Z" fill="var(--accent-color)" opacity=".055" />
        <path class="ambient-angle" d="M-170 580 70 340 260 530 20 770Z" fill="var(--text-primary)" opacity=".035" />
        <path d="M1040 380H1260V900H860L1080 680H1040Z" fill="var(--signal-mint)" opacity=".065" />
        <g class="ambient-orbit" fill="none" stroke="var(--signal-mint)"><circle cx="1100" cy="660" r="280" opacity=".2" stroke-dasharray="340 100 80 50" /><circle cx="1100" cy="660" r="245" opacity=".09" /><circle cx="1100" cy="380" r="4" fill="var(--signal-mint)" opacity=".5" /></g>
        <path d="M0 190H340L520 10H1200M0 840H440L590 690H1200" fill="none" stroke="var(--accent-color)" opacity=".14" />
        <path class="ambient-trace" d="M0 190H340L520 10H1200M0 840H440L590 690H1200" fill="none" stroke="var(--signal-mint)" opacity=".45" pathLength="100" stroke-dasharray="6 44" />
        <path class="ambient-frame" d="M-70 470 90 310 250 470 90 630ZM-34 470 90 346 214 470 90 594Z" fill="none" stroke="var(--signal-mint)" stroke-width="1.5" opacity=".2" />
        <path d="M0 270H90L180 360V550L90 640H0M1200 150H1050L960 240V360" fill="none" stroke="var(--accent-color)" opacity=".13" />
        <path class="ambient-circuit" d="M0 270H90L180 360V550L90 640H0M1200 150H1050L960 240V360" fill="none" stroke="var(--signal-mint)" stroke-width="2" opacity=".55" pathLength="100" stroke-dasharray="8 42" />
        <g class="ambient-shards" fill="var(--signal-mint)" opacity=".17"><path d="M1030 70H1120L1070 120H980Z" /><path d="M50 720H110L80 750H20Z" /></g>
      </svg>
      <!-- 全站背景粒子：漂浮、鼠标附近连线、定时在空白处聚成图形 -->
      <ParticleField v-if="!/^\/(club|taichu)(\/|$)/i.test(router.currentRoute.value.path)" />
      <div class="site-toolbar">
        <span class="site-wordmark">TAICHU HUANYU <span aria-hidden="true">/</span> COMMUNITY</span>
        <span class="site-locator" aria-hidden="true"><i class="status-dot"></i>SYS.ONLINE<span class="locator-path">LOC {{ locatorPath }}</span></span>
        <div class="site-tools">
          <span class="site-caution" aria-hidden="true"></span>
          <RouterLink to="/knowledge" class="site-search"><svg aria-hidden="true" viewBox="0 0 20 20" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.4"><circle cx="8.5" cy="8.5" r="5.5" /><path d="m13 13 4 4" /></svg>检索社区内容</RouterLink>
          <!-- 现在几人在线，点了看社区成员（在线的排最前） -->
          <OnlineIndicator />
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
          <!-- 聊天室提醒：不在聊天室页面时，有人 @我 / 回复我会在右下角提醒（可在聊天室顶部或通知面板里关掉） -->
          <ChatNotifier />
        </div>
      </section>
    </main>
  </div>
</template>

<style scoped>
.app-atmosphere { position: absolute; inset: 0; width: 100%; height: 100%; z-index: 0; pointer-events: none; overflow: hidden; }
.app-atmosphere.motion-paused * { animation-play-state: paused !important; }
.ambient-orbit { transform-origin: 1100px 660px; }
.ambient-frame { transform-origin: 90px 470px; }
@media(prefers-reduced-motion: no-preference) {
  .ambient-frame { animation: atmosphere-frame 12s ease-in-out infinite alternate; }
  .ambient-circuit { animation: atmosphere-trace 5s linear infinite; }
  .ambient-shards { animation: atmosphere-shards 6s ease-in-out infinite alternate; }
  .ambient-orbit { animation: atmosphere-orbit 60s linear infinite; }
  .ambient-trace { animation: atmosphere-trace 18s linear infinite; }
  .ambient-plane { animation: atmosphere-plane 18s ease-in-out infinite alternate; }
  .ambient-angle { animation: atmosphere-plane 26s ease-in-out -9s infinite alternate-reverse; }
  .status-dot { animation: status-blink 2.4s steps(1) infinite; }
}
@keyframes atmosphere-orbit { to { transform: rotate(360deg); } }
@keyframes status-blink { 50% { opacity: .25; } }
@keyframes atmosphere-frame { to { transform: translate(20px,20px) rotate(25deg); opacity: .34; } }
@keyframes atmosphere-shards { to { transform: translate(-36px,18px); } }
@keyframes atmosphere-plane { to { transform: translate(-22px, 14px); } }
@keyframes atmosphere-trace { to { stroke-dashoffset: -100; } }
@media(max-width: 640px) { .ambient-registration, .ambient-ruler { display: none; } }
/* 坐标纸：细格 8px、粗格 40px，铺满整个窗口（导航栏后面也有），像实验台面。
   自定义背景（SiteBackdrop）盖在坐标纸上面，导航栏和内容区再盖在背景上面。 */
.app-shell.community-design {
  background-color: var(--bg-main);
  background-image:
    linear-gradient(var(--lab-grid-major) 1px, transparent 1px),
    linear-gradient(90deg, var(--lab-grid-major) 1px, transparent 1px),
    linear-gradient(var(--lab-grid-minor) 1px, transparent 1px),
    linear-gradient(90deg, var(--lab-grid-minor) 1px, transparent 1px);
  background-size: 40px 40px, 40px 40px, 8px 8px, 8px 8px;
}
.site-locator { display: inline-flex; align-items: center; gap: 8px; margin-right: auto; padding-left: 16px; border-left: 1px solid var(--border-line); font: 10px/1 var(--font-mono); letter-spacing: .12em; color: var(--accent-mint); }
.status-dot { width: 6px; height: 6px; background: var(--signal-mint); box-shadow: 0 0 0 3px color-mix(in srgb, var(--signal-mint) 22%, transparent); }
.locator-path { color: var(--text-secondary); margin-left: 8px; max-width: 26ch; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.site-caution { width: 36px; height: 8px; background: var(--hazard-stripe); }

.navigation-state { position: relative; margin: 0; padding: 12px 20px; color: var(--text-primary); background: var(--accent-soft); font-size: 14px; }
/* 高度用 100%（html / body / #app 已经是 100%）。不要用 100dvh：荣耀等手机自带浏览器内核较旧、不认识 dvh，
   整条声明被丢掉后外壳没有高度，被 #app 的 overflow: hidden 裁掉，整个页面就滑不动了。 */
.app-shell { position: relative; isolation: isolate; display: flex; width: 100%; height: 100%; background: var(--bg-main); color: var(--text-primary); overflow: hidden; }
.container { position: relative; z-index: 1; flex: 1; display: flex; flex-direction: column; min-width: 0; }
/* z-index：工具栏带 backdrop-filter 会单独成一层，不给层级的话下面的页面内容会盖住通知下拉框，点不到 */
.site-toolbar { position: relative; z-index: 50; background: color-mix(in srgb, var(--bg-main) 92%, transparent); backdrop-filter: blur(6px); display: flex; align-items: center; justify-content: space-between; gap: 16px; flex-shrink: 0; min-height: 52px; padding: 8px clamp(20px, 3vw, 48px); border-bottom: 1px solid var(--border-line); }
.site-wordmark { font-family: var(--font-mono); font-size: 10px; letter-spacing: .12em; color: var(--text-secondary); }
.site-wordmark span { margin-inline: 12px; color: var(--accent-color); }
.site-tools { display: flex; align-items: center; gap: 16px; }
.site-search { display: inline-flex; align-items: center; gap: 8px; color: var(--text-secondary); font-size: 12px; text-decoration: none; }
.site-search:hover { color: var(--accent-color); }
.viewport { position: relative; flex: 1; min-height: 0; padding: 28px clamp(20px, 3vw, 48px); overflow: auto; scrollbar-gutter: stable; }
.viewport-content { width: 100%; max-width: 1440px; margin-inline: auto; }
@media(max-width: 900px) {
  .app-shell { flex-direction: column; }
  .container { min-height: 0; }
  .viewport { padding: 20px 16px; }
  .site-toolbar { min-height: 40px; padding: 6px 16px; }
  .site-wordmark { font-size: 9px; letter-spacing: .02em; }
  .site-wordmark span { margin-inline: 4px; }
  .site-locator { display: none; }
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
