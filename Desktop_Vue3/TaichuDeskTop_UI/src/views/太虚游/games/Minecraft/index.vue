<template>
  <section class="mc-page">
    <header class="mc-header"><div><h2>我的世界</h2><p>太初寰宇社区服务器 · Fabric {{ serverVersion }}</p></div><RouterLink to="/stream?tag=我的世界">进入论坛版块 →</RouterLink></header>
    <div class="mc-grid">
      <section class="mc-card"><h3>加入服务器</h3>
        <p class="server-state">{{ statusLoaded ? (serverInfo.isOnline ? '服务器在线' : '服务器离线') : statusError || '正在读取服务器状态…' }}</p>
        <p v-if="statusLoaded && serverInfo.isOnline">在线 {{ serverInfo.onlinePlayers }} / {{ serverInfo.maxPlayers }} 人</p>
        <div class="server-address"><code>{{ serverAddress }}</code><button @click="copyIp">复制地址</button></div>
        <p v-if="copyMessage" role="status">{{ copyMessage }}</p>
        <details><summary>客户端与运行环境</summary><p>Fabric {{ serverVersion }} · Java 25 (LTS)。当前为核心内测阶段，正在进行生存开荒和性能测试；专属世界观模组持续开发中。</p></details>
        <div v-if="statusLoaded && serverInfo.isOnline" class="players"><h4>在线玩家</h4><span v-for="player in serverInfo.playerList" :key="player">{{ player }}</span><p v-if="!serverInfo.playerList.length">暂无玩家列表。</p></div>
      </section>
      <section class="mc-card"><h3>下载与准备</h3><p>按需要安装客户端依赖和原创模组。</p>
        <a :href="downloadLinks.fabricApi" target="_blank" rel="noopener">Fabric API 下载 ↗</a>
        <a :href="downloadLinks.customMod" target="_blank" rel="noopener">太初寰宇原创模组下载 ↗</a>
        <p class="muted">专用整合包筹备中。</p>
      </section>
    </div>
    <section class="mc-card discussion"><div><h3>分享你的游戏日常</h3><p>晒建筑、发截图、交流玩法。内容会显示在论坛的「我的世界」版块。</p></div><RouterLink class="primary" :to="{ path: '/write/post', query: { tag: '我的世界', sub: '作品分享' } }">发帖子</RouterLink></section>
  </section>
</template>
<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import request from '@/utils/request'

const serverAddress = ref('120.53.224.225')
const serverVersion = ref('26.2')

const downloadLinks = ref({
  fabricApi: 'https://modrinth.com/mod/fabric-api',
  // 自制 Mod 下载地址（项目组文件管理分享页）
  customMod: 'https://ug.link/TaiChuHuanYu/filemgr/share-download/?id=5a2e739ffee24885afc967fb19869b9a'
})

const serverInfo = ref({
  isOnline: false,
  onlinePlayers: 0,
  maxPlayers: 20,
  playerList: []
})

const statusError = ref(''), statusLoaded = ref(false)
let timer = null
let disposed = false

const fetchServerStatus = async () => {
  try {
    const data = await request.get('/minecraft/status', { silentError: true })
    if (disposed) return
    serverInfo.value = { ...data, playerList: data.playerList || [] }
    statusLoaded.value = true; statusError.value = ''
  } catch (err) {
    if (disposed) return
    statusLoaded.value = false
    statusError.value = err.response?.status === 404 ? '状态服务暂未接入' : '暂时无法读取服务器状态'
    if (err.response?.status === 404 && timer) { clearInterval(timer); timer = null }
  }
}

const copyMessage = ref('')
const copyIp = async () => {
  try { await navigator.clipboard.writeText(serverAddress.value); copyMessage.value = '已复制服务器地址' }
  catch { copyMessage.value = '复制失败，请选中地址手动复制' }
}

onMounted(() => {
  fetchServerStatus()
  timer = setInterval(fetchServerStatus, 15000)
})

onUnmounted(() => {
  disposed = true
  if (timer) clearInterval(timer)
})
</script>
<style scoped>
.mc-page { width: 100%; min-width: 0; color: var(--text-primary); }
.mc-header, .discussion { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: 16px; }
.mc-header { margin-bottom: 24px; padding-bottom: 16px; border-bottom: 1px solid var(--surface-edge); }
h2 { font-size: 26px; margin: 0 0 8px; } h3 { margin: 0 0 12px; font-size: 18px; } h4 { margin: 16px 0 8px; }
p { color: var(--text-secondary); font-size: 14px; line-height: 1.7; margin: 8px 0; }
.mc-grid { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 20px; margin-bottom: 20px; }
.mc-card { padding: clamp(16px, 2vw, 24px); border: 1px solid var(--border-line); border-radius: 0; border-top: 2px solid var(--surface-edge); background: linear-gradient(135deg, var(--paper-card) 65%, var(--surface-wash)); min-width: 0; }
a { color: var(--accent-color); text-decoration: none; } .mc-card > a:not(.primary) { display: block; padding: 12px 0; }
.server-address { display: flex; flex-wrap: wrap; gap: 12px; align-items: center; justify-content: space-between; background: var(--bg-sub); padding: 12px; border-radius: 0; margin: 16px 0; }
code { overflow-wrap: anywhere; } button, .primary { border: 0; background: var(--accent-color); color: var(--on-accent); padding: 10px 16px; border-radius: 0; cursor: pointer; }
summary { cursor: pointer; color: var(--text-secondary); font-size: 14px; padding: 8px 0; }
.players span { display: inline-block; padding: 4px 8px; margin: 4px; border-radius: 0; background: var(--bg-sub); font-size: 13px; }
.muted { font-size: 12px; } .discussion div { flex: 1 1 220px; }
@media (max-width: 1100px) { .mc-grid { grid-template-columns: minmax(0,1fr); } }
</style>
