<template>
  <div class="mc-portal-container">
    <!-- 顶部主标题横幅 -->
    <header class="portal-header">
      <div class="title-glow"></div>
      <h1 class="main-title">太初寰宇</h1>
      <p class="sub-title">TAICHU HUANYU · FABRIC {{ serverVersion }} DEV & SURVIVAL REALM</p>
    </header>

    <!-- 主体双栏网格布局 -->
    <div class="portal-grid">
      
      <!-- 左侧：状态与快速操作区 -->
      <div class="column-left">
        <div class="glass-card status-card">
          <div class="card-header">
            <h3>灵境核心状态</h3>
            <span :class="['status-dot', serverInfo.isOnline ? 'online' : 'offline']"></span>
          </div>
          
          <div class="status-list">
            <div class="status-item">
              <span class="label">运行端</span>
              <span class="value">Fabric {{ serverVersion }}</span>
            </div>
            <div class="status-item">
              <span class="label">底层架构</span>
              <span class="value highlight-java">Java 25 (LTS)</span>
            </div>
            <div class="status-item">
              <span class="label">当前在线</span>
              <span class="value">{{ serverInfo.onlinePlayers }} / {{ serverInfo.maxPlayers }} 道友</span>
            </div>
          </div>

          <!-- IP 直连复制框 -->
          <div class="ip-action-box">
            <div class="ip-text">
              <span class="ip-label">服务器入口</span>
              <code>{{ serverAddress }}</code>
            </div>
            <button class="btn-neon" @click="copyIp">复制入口地址</button>
          </div>
        </div>

        <!-- 客户端依赖下载 -->
        <div class="glass-card download-card">
          <h3>组件与传送门</h3>
          <p class="card-desc">适配当前 26.2 架构的前置与客户端支持。</p>
          <div class="action-buttons">
            <a :href="downloadLinks.fabricApi" target="_blank" class="btn-sub">
              <span>Fabric API 26.2</span>
            </a>
            <a href="#" class="btn-sub disabled" @click.prevent="alert('内测专用整合包正在加急打包中！')">
              <span>专用整合包 [内测筹备]</span>
            </a>
          </div>
        </div>
      </div>

      <!-- 右侧：世界观介绍与内测指引 -->
      <div class="column-right">
        <div class="glass-card lore-card">
          <div class="lore-badge">CLOSED BETA</div>
          <h2>核心内测阶段开启</h2>
          <div class="lore-text">
            <p>
              欢迎来到<strong>《太初寰宇》</strong>。本服目前正处于核心内测期，底层已全面跃迁至 <b>Fabric {{ serverVersion }} & Java 25</b> 现代高性能运行环境。
            </p>
            <p>
              当前阶段我们正在进行纯净开荒与底层性能压测。各位道友不仅能抢先体验原版生存的乐趣，未来还将作为核心见证者，逐步接入我们正在独立开发的**《太初寰宇》专属世界观系统**（包含复杂的属性矩阵与特色玩法）。
            </p>
          </div>

          <div class="feature-tags">
            <span class="tag">🌌 长期稳定开荒</span>
            <span class="tag">⚡ Java 25 满血性能</span>
            <span class="tag">🛠️ 独立 Mod 深度定制</span>
          </div>
        </div>

        <!-- 在线玩家实时面板 -->
        <div class="glass-card players-card" v-if="serverInfo.isOnline">
          <h3>当前在线修行者</h3>
          <div v-if="serverInfo.playerList.length > 0" class="player-chips">
            <span v-for="player in serverInfo.playerList" :key="player" class="player-chip">
              {{ player }}
            </span>
          </div>
          <div v-else class="empty-notice">暂无道友在线，寰宇静待开拓。</div>
        </div>
      </div>

    </div>

    <!-- 底部微型提示 -->
    <footer class="portal-footer">
      <p>太初寰宇项目组 · 保持对未知与代码的热忱</p>
    </footer>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'

const serverAddress = ref('120.53.224.225')
const serverVersion = ref('26.2')

const downloadLinks = ref({
  fabricApi: 'https://modrinth.com/mod/fabric-api'
})

const serverInfo = ref({
  isOnline: false,
  onlinePlayers: 0,
  maxPlayers: 20,
  playerList: []
})

let timer = null

const fetchServerStatus = async () => {
  try {
    const res = await fetch('https://bianyuzhou.com/api/minecraft/status')
    if (res.ok) {
      const data = await res.json()
      serverInfo.value = {
        isOnline: data.isOnline,
        onlinePlayers: data.onlinePlayers,
        maxPlayers: data.maxPlayers,
        playerList: data.playerList || []
      }
    }
  } catch (err) {
    console.error('获取服务器状态失败', err)
    serverInfo.value.isOnline = false
  }
}

const copyIp = () => {
  navigator.clipboard.writeText(serverAddress.value)
  alert('服务器入口地址已复制！')
}

onMounted(() => {
  fetchServerStatus()
  timer = setInterval(fetchServerStatus, 15000)
})

onUnmounted(() => {
  if (timer) clearInterval(timer)
})
</script>

<style scoped>
/* 全局容器与重置 */
.mc-portal-container {
  max-width: 1100px;
  width: 100%;
  margin: 0 auto;
  padding: 40px 20px;
  box-sizing: border-box;
}

/* 顶部横幅 */
.portal-header {
  text-align: center;
  margin-bottom: 40px;
  position: relative;
}
.main-title {
  font-size: 32px;
  font-weight: 700;
  letter-spacing: 0.3em;
  color: var(--ink-black, #1a1a1a);
  margin-bottom: 6px;
}
.sub-title {
  font-size: 11px;
  letter-spacing: 0.25em;
  color: var(--ink-gray, #666);
  opacity: 0.8;
}

/* 双栏网格 */
.portal-grid {
  display: grid;
  grid-template-columns: 380px 1fr;
  gap: 24px;
}
@media (max-width: 850px) {
  .portal-grid {
    grid-template-columns: 1fr;
  }
}

.column-left, .column-right {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

/* 毛玻璃卡片风格 */
.glass-card {
  background: rgba(255, 255, 255, 0.7);
  border: 1px solid rgba(0, 0, 0, 0.06);
  backdrop-filter: blur(10px);
  border-radius: 12px;
  padding: 24px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.03);
  transition: transform 0.2s ease, box-shadow 0.2s ease;
}
.glass-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 6px 24px rgba(0, 0, 0, 0.05);
}

/* 状态卡片 */
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  border-bottom: 1px solid rgba(0,0,0,0.05);
  padding-bottom: 10px;
}
.card-header h3 {
  font-size: 15px;
  letter-spacing: 0.1em;
  margin: 0;
  color: var(--ink-black);
}
.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}
.status-dot.online {
  background-color: #2e7d32;
  box-shadow: 0 0 8px rgba(46, 125, 50, 0.4);
}
.status-dot.offline {
  background-color: #d32f2f;
  box-shadow: 0 0 8px rgba(211, 47, 47, 0.4);
}

.status-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin-bottom: 20px;
}
.status-item {
  display: flex;
  justify-content: space-between;
  font-size: 14px;
}
.label {
  color: var(--ink-gray);
  letter-spacing: 0.05em;
}
.value {
  font-weight: 500;
  color: var(--ink-black);
}
.highlight-java {
  font-family: monospace;
  background: rgba(0,0,0,0.04);
  padding: 1px 6px;
  border-radius: 4px;
}

/* IP 复制框样式 */
.ip-action-box {
  background: rgba(0,0,0,0.02);
  border: 1px dashed rgba(0,0,0,0.1);
  padding: 12px;
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.ip-text {
  display: flex;
  justify-content: space-between;
  font-size: 13px;
}
.ip-label {
  color: var(--ink-gray);
}
.ip-action-box code {
  font-family: monospace;
  font-weight: bold;
  color: var(--ink-black);
}
.btn-neon {
  background: var(--ink-black, #1a1a1a);
  color: #fff;
  border: none;
  padding: 8px;
  border-radius: 6px;
  font-size: 12px;
  letter-spacing: 0.1em;
  cursor: pointer;
  transition: opacity 0.2s;
}
.btn-neon:hover {
  opacity: 0.85;
}

/* 下载区块 */
.download-card h3 {
  font-size: 15px;
  letter-spacing: 0.1em;
  margin: 0 0 6px 0;
}
.card-desc {
  font-size: 12px;
  color: var(--ink-gray);
  margin-bottom: 14px;
}
.action-buttons {
  display: flex;
  gap: 10px;
}
.btn-sub {
  flex: 1;
  text-align: center;
  background: none;
  border: 1px solid rgba(0,0,0,0.15);
  color: var(--ink-black);
  padding: 8px;
  border-radius: 6px;
  font-size: 12px;
  text-decoration: none;
  letter-spacing: 0.05em;
  transition: all 0.2s;
}
.btn-sub:hover:not(.disabled) {
  border-color: var(--ink-black);
  background: rgba(0,0,0,0.02);
}
.btn-sub.disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* 右侧世界观卡片 */
.lore-card {
  position: relative;
  overflow: hidden;
}
.lore-badge {
  display: inline-block;
  font-size: 10px;
  font-weight: bold;
  letter-spacing: 0.15em;
  background: rgba(211, 47, 47, 0.1);
  color: #d32f2f;
  padding: 3px 8px;
  border-radius: 4px;
  margin-bottom: 12px;
}
.lore-card h2 {
  font-size: 18px;
  letter-spacing: 0.1em;
  margin: 0 0 14px 0;
  color: var(--ink-black);
}
.lore-text {
  font-size: 14px;
  color: var(--ink-gray);
  line-height: 1.7;
  letter-spacing: 0.03em;
  margin-bottom: 20px;
}
.lore-text p {
  margin-bottom: 10px;
}
.feature-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.tag {
  background: rgba(0,0,0,0.03);
  border: 1px solid rgba(0,0,0,0.05);
  padding: 4px 10px;
  border-radius: 6px;
  font-size: 12px;
  color: var(--ink-black);
  letter-spacing: 0.05em;
}

/* 在线玩家列表 */
.players-card h3 {
  font-size: 15px;
  letter-spacing: 0.1em;
  margin: 0 0 12px 0;
}
.player-chips {
  display: flex;
  flex-wrap: gap;
  gap: 8px;
}
.player-chip {
  background: rgba(0,0,0,0.04);
  padding: 4px 10px;
  border-radius: 6px;
  font-size: 13px;
  letter-spacing: 0.05em;
}
.empty-notice {
  font-size: 13px;
  color: var(--ink-gray);
  letter-spacing: 0.05em;
}

/* 底部 */
.portal-footer {
  text-align: center;
  margin-top: 40px;
  font-size: 11px;
  color: var(--ink-gray);
  letter-spacing: 0.15em;
  opacity: 0.6;
}
</style>