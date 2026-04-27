<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'

interface Plugin {
  Name: string;
  Url: string;
  Icon: string;
}

const router = useRouter()
const plugins = ref<Plugin[]>([])
const activeMenu = ref('推送首页')



onMounted(() => {
  (window as any).receivePlugins = (data: Plugin[]) => {
    console.log("收到插件列表:", data)
    plugins.value = data

    data.forEach(item => {
      if (item.Url !== '/') {
        router.addRoute({
          path: item.Url,
          name: item.Name,
          component: () => import(`./views/${item.Name}/index.vue`)
        })
      }
    })
  }
})



const navigateTo = (item: Plugin) => {
  activeMenu.value = item.Name
  
  if (item.Url.startsWith('http')) {
    // 强制转为 any 来逃避类型检查
    (window as any).chrome?.webview?.postMessage({ 
      cmd: 'load_external_url', 
      url: item.Url 
    });
  } else {
    router.push(item.Url)
  }
}

</script>

<template>
  <div class="app-shell">
    <nav class="sidebar">
      <div class="brand">太初寰宇</div>
      <div 
        v-for="item in plugins" 
        :key="item.Name"
        :class="['nav-item', { active: activeMenu === item.Name }]"
        @click="navigateTo(item)"
      >
        <span class="icon">#</span> {{ item.Name }}
      </div>
    </nav>

    <main class="container">
      <header class="header">
        <h2>{{ activeMenu }}</h2>
      </header>
      <section class="viewport">
        <RouterView /> 
      </section>
    </main>
  </div>
</template>

<style scoped>
.app-shell { display: flex; height: 100vh; background: #0a0a0a; color: #eee; }
.sidebar { width: 240px; background: #111; border-right: 1px solid #222; }
.nav-item { padding: 12px 20px; cursor: pointer; color: #888; }
.nav-item.active { background: #0078d422; color: #0078d4; }
.container { flex: 1; display: flex; flex-direction: column; }
.header { height: 80px; padding: 0 30px; display: flex; align-items: center; border-bottom: 1px solid #222; }
.viewport { flex: 1; padding: 20px; overflow-y: auto; }
</style>