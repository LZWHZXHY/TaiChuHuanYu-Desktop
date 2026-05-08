<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router' // 🌟 引入 useRoute
import request from './utils/request' 
import OperationTerminal from './components/OperationTerminal.vue'
import { useUserStore } from './stores/user'
import GlobalNotify from './components/GlobalNotify.vue'

// 插件接口定义
interface Plugin {
  name: string;
  url: string;
  icon: string;
  requiresAuth: boolean;
}
const userStore = useUserStore()
const router = useRouter()
const route = useRoute() // 🌟 获取当前路由状态
const allPlugins = ref<Plugin[]>([]) 
const activeMenu = ref('推送首页')

const visiblePlugins = computed(() => {
  const hasToken = !!localStorage.getItem('token');
  
  return allPlugins.value.filter(item => {
    const hiddenItems = ['身份认证', '个人中心'];
    if (hiddenItems.includes(item.name)) return false;
    return !item.requiresAuth || hasToken;
  });
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
      if (item.url !== '/' && !item.url.startsWith('http')) {
        if (!router.hasRoute(item.name)) {
          router.addRoute({
            path: item.url,
            name: item.name,
            component: () => import(`./views/${item.name}/index.vue`),
            meta: { requiresAuth: item.requiresAuth } 
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
const handleNavigation = (item: Plugin) => {
  activeMenu.value = item.name
  if (item.url.startsWith('http')) {
    (window as any).chrome?.webview?.postMessage({ 
      cmd: 'load_external_url', 
      url: item.url 
    });
  } else {
    router.push(item.url)
  }
}

onMounted(async ()=> {
  // 🌟 【关键修复点 2】使用 await 确保插件加载（addRoute）完成后再往后走
  await fetchPlugins();

  if (localStorage.getItem('token')) {
    await userStore.fetchUserInfo();
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
      
      
      <section class="viewport">
        <div class="viewport-content">
          <RouterView /> 
          <GlobalNotify/>
        </div>
      </section>
    </main>
  </div>
</template>

<style scoped>
.app-shell { 
  display: flex; 
  width: 100vw;
  height: 100vh; 
  background: #ffffff;
  color: #24292f;
  overflow: hidden;
}

.container { 
  flex: 1; 
  display: flex; 
  flex-direction: column;
  min-width: 0; /* 关键：允许容器在 flex 布局中缩小，防止挤出屏幕 */
}



/* App.vue 中的样式修改 */

.header-content, .viewport-content {

  width: 100%;
  /* 极致阅读体验：限制最大宽度防止行太长，但不强制居中 */

  /* 删掉 margin: 0 auto; */
  margin: 0; 
  transition: all 0.3s ease;
}

.header { 
  height: 80px; 
  padding: 0 5%; 
  display: flex; 
  align-items: flex-end; 
  padding-bottom: 24px;
  border-bottom: 1px solid #f0f0f0;
}

.viewport { 
  flex: 1; 
  /* 同步左侧留白 */
  padding: 40px 5%; 
  overflow-y: auto;
}

/* 移动端适配：回归紧凑 */
@media (max-width: 768px) {
  .header {
    height: 70px;
    padding: 0 20px;
  }
  .viewport {
    padding: 20px;
  }
  .header-content, .viewport-content {
    max-width: 100%; /* 手机端必须占满 */
  }
}
</style>