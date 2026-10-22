import fs from 'node:fs';
const p='src/views/太虚游/games/Minecraft/index.vue';let code=fs.readFileSync(p,'utf8');
const script=code.slice(code.indexOf('<script setup>'),code.indexOf('</script>')+9)
 .replace("const copyIp = () => {\r\n  navigator.clipboard.writeText(serverAddress.value)\r\n  alert('服务器入口地址已复制！')\r\n}",`const copyMessage = ref('')
const copyIp = async () => {
  try { await navigator.clipboard.writeText(serverAddress.value); copyMessage.value = '已复制服务器地址' }
  catch { copyMessage.value = '复制失败，请选中地址手动复制' }
}`);
fs.writeFileSync(p,`<template>
  <section class="mc-page">
    <header class="mc-header"><div><h2>我的世界</h2><p>太初寰宇社区服务器 · Fabric {{ serverVersion }}</p></div><RouterLink to="/stream?tag=我的世界">浏览讨论 →</RouterLink></header>
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
    <section class="mc-card discussion"><div><h3>分享你的游戏日常</h3><p>晒建筑、发截图、交流玩法。内容会显示在博客与帖子的“我的世界”分类中。</p></div><RouterLink class="primary" to="/write/post?tag=我的世界">发帖子</RouterLink></section>
  </section>
</template>
${script}
<style scoped>
.mc-page { width: 100%; min-width: 0; color: var(--text-primary); }
.mc-header, .discussion { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: 16px; }
.mc-header { margin-bottom: 24px; }
h2 { font-size: 26px; margin: 0 0 8px; } h3 { margin: 0 0 12px; font-size: 18px; } h4 { margin: 16px 0 8px; }
p { color: var(--text-secondary); font-size: 14px; line-height: 1.7; margin: 8px 0; }
.mc-grid { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 20px; margin-bottom: 20px; }
.mc-card { padding: clamp(16px, 2vw, 24px); border: 1px solid var(--border-line); border-radius: 12px; background: var(--paper-card); min-width: 0; }
a { color: var(--accent-color); text-decoration: none; } .mc-card > a { display: block; padding: 12px 0; }
.server-address { display: flex; flex-wrap: wrap; gap: 12px; align-items: center; justify-content: space-between; background: var(--bg-sub); padding: 12px; border-radius: 8px; margin: 16px 0; }
code { overflow-wrap: anywhere; } button, .primary { border: 0; background: var(--accent-color); color: var(--on-accent); padding: 10px 16px; border-radius: 8px; cursor: pointer; }
summary { cursor: pointer; color: var(--text-secondary); font-size: 14px; padding: 8px 0; }
.players span { display: inline-block; padding: 4px 8px; margin: 4px; border-radius: 6px; background: var(--bg-sub); font-size: 13px; }
.muted { font-size: 12px; } .discussion div { flex: 1 1 220px; }
@media (max-width: 1100px) { .mc-grid { grid-template-columns: minmax(0,1fr); } }
</style>
`);
