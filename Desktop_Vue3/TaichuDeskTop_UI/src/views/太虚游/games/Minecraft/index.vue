<template>
  <div class="mc-container">
    <div class="mc-header">
      <h2>太虚方块灵境</h2>
      <p>Fabric {{ serverVersion }} 纯净生存服 · 自由建筑与基建</p>
    </div>

    <!-- 服务器核心状态面板 -->
    <div class="mc-status-card">
      <div class="status-row">
        <span class="label">服务器状态：</span>
        <span :class="['value-badge', serverInfo.isOnline ? 'online' : 'offline']">
          {{ serverInfo.isOnline ? '● 灵气充沛 (在线)' : '○ 闭关维护中 (离线)' }}
        </span>
      </div>
      <div class="status-row">
        <span class="label">当前游戏版本：</span>
        <span class="value version-tag">Fabric {{ serverVersion }}</span>
      </div>
      <div class="status-row">
        <span class="label">连接地址：</span>
        <div class="ip-box">
          <code>{{ serverAddress }}</code>
          <button class="btn-copy" @click="copyIp">复制地址</button>
        </div>
      </div>
      <div class="status-row">
        <span class="label">当前在线道友：</span>
        <span class="value">{{ serverInfo.onlinePlayers }} / {{ serverInfo.maxPlayers }}</span>
      </div>
    </div>

    <!-- Mod 下载与客户端指引专区 ✅ 新增 -->
    <div class="mc-section-card">
      <h3>📥 客户端与 Mod 传送门</h3>
      <p class="section-desc">为了保证进入服务器后方块与物品正常同步，请下载指定的客户端整合包或基础 Mod 列表。</p>
      <div class="download-actions">
        <a :href="downloadLinks.modpack" target="_blank" class="btn-action">
          <span>📦 下载完整客户端整合包</span>
        </a>
        <a :href="downloadLinks.fabricApi" target="_blank" class="btn-action-line">
          <span>🔗 Fabric API & 基础依赖</span>
        </a>
      </div>
    </div>

    <!-- 在线玩家列表 -->
    <div class="mc-section-card" v-if="serverInfo.isOnline">
      <h3>👤 在线修行者</h3>
      <div v-if="serverInfo.playerList.length > 0" class="player-grid">
        <span v-for="player in serverInfo.playerList" :key="player" class="player-tag">
          {{ player }}
        </span>
      </div>
      <div v-else class="empty-players">当前暂无道友在线，快去抢占先机！</div>
    </div>

    <!-- 服务器更新日志 ✅ 新增 -->
    <div class="mc-section-card">
      <h3>📜 灵境更新日志</h3>
      <div class="changelog-list">
        <div v-for="(log, index) in changelogs" :key="index" class="changelog-item">
          <div class="log-header">
            <span class="log-version">{{ log.version }}</span>
            <span class="log-date">{{ log.date }}</span>
          </div>
          <p class="log-content">{{ log.content }}</p>
        </div>
      </div>
    </div>

    <!-- 底部说明 -->
    <div class="mc-footer-hint">
      <p>💡 提示：进入游戏前请核对版本为 <b>Fabric {{ serverVersion }}</b>，若遇阻碍可前往群内寻找阵法师协助。</p>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'

const serverAddress = ref('play.bianyuzhou.com:25565')
const serverVersion = ref('1.21.1')

// 下载链接配置
const downloadLinks = ref({
  modpack: 'https://pan.baidu.com/s/your-modpack-link', // 替换为你的网盘或直链
  fabricApi: 'https://modrinth.com/mod/fabric-api'
})

// 服务器更新日志数据（后期可以改成由后端接口动态提供）
const changelogs = ref([
  {
    version: 'v1.1.0',
    date: '2026-03-05',
    content: '服务器核心平稳升级至 Fabric 1.21.1，修复了部分区块加载延迟的问题，新增基础传送与领地保护插件。'
  },
  {
    version: 'v1.0.0',
    date: '2026-02-20',
    content: '太虚方块灵境正式开荒！纯净生存服搭建完毕，欢迎各位道友入驻。'
  }
])

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
  alert('服务器地址已复制到剪贴板！')
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
.mc-container {
  max-width: 750px;
  width: 100%;
  margin: 0 auto;
  padding: 20px;
}
.mc-header {
  text-align: center;
  margin-bottom: 30px;
}
.mc-header h2 {
  font-size: 24px;
  letter-spacing: 0.2em;
  margin-bottom: 8px;
  color: var(--ink-black);
}
.mc-header p {
  font-size: 14px;
  color: var(--ink-gray);
  letter-spacing: 0.1em;
}

.mc-status-card, .mc-section-card {
  background: var(--paper-card);
  border: 1px solid var(--line-raw);
  padding: 24px;
  border-radius: 8px;
  margin-bottom: 24px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.02);
}
.status-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 0;
  border-bottom: 1px dashed var(--line-raw);
  font-size: 15px;
}
.status-row:last-child {
  border-bottom: none;
}
.label {
  color: var(--ink-gray);
  letter-spacing: 0.1em;
}
.value-badge.online {
  color: #2e7d32;
  font-weight: 500;
}
.value-badge.offline {
  color: var(--cinnabar);
  font-weight: 500;
}
.version-tag {
  background: var(--paper-sub);
  padding: 2px 8px;
  border-radius: 4px;
  font-family: monospace;
}
.ip-box {
  display: flex;
  align-items: center;
  gap: 10px;
}
.ip-box code {
  background: var(--paper-sub);
  padding: 4px 10px;
  border-radius: 4px;
  font-family: monospace;
  font-size: 14px;
}
.btn-copy {
  background: none;
  border: 1px solid var(--line-raw);
  padding: 4px 12px;
  font-size: 12px;
  cursor: pointer;
  letter-spacing: 0.1em;
  transition: all 0.2s;
}
.btn-copy:hover {
  border-color: var(--cinnabar);
  color: var(--cinnabar);
}

/* 下载区块样式 */
.mc-section-card h3 {
  font-size: 16px;
  letter-spacing: 0.15em;
  margin-bottom: 10px;
  color: var(--ink-black);
}
.section-desc {
  font-size: 13px;
  color: var(--ink-gray);
  margin-bottom: 16px;
  letter-spacing: 0.05em;
}
.download-actions {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
}
.btn-action {
  background: var(--cinnabar);
  color: #fff;
  padding: 8px 16px;
  border-radius: 4px;
  text-decoration: none;
  font-size: 13px;
  letter-spacing: 0.1em;
  transition: opacity 0.2s;
}
.btn-action:hover {
  opacity: 0.9;
}
.btn-action-line {
  background: none;
  border: 1px solid var(--line-raw);
  color: var(--ink-black);
  padding: 8px 16px;
  border-radius: 4px;
  text-decoration: none;
  font-size: 13px;
  letter-spacing: 0.1em;
  transition: all 0.2s;
}
.btn-action-line:hover {
  border-color: var(--cinnabar);
  color: var(--cinnabar);
}

/* 玩家列表 */
.player-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 10px;
}
.player-tag {
  background: var(--paper-sub);
  padding: 4px 10px;
  border-radius: 4px;
  font-size: 13px;
  letter-spacing: 0.05em;
}
.empty-players {
  color: var(--ink-gray);
  font-size: 13px;
  letter-spacing: 0.1em;
  margin-top: 10px;
}

/* 更新日志样式 */
.changelog-list {
  display: flex;
  flex-direction: column;
  gap: 14px;
  margin-top: 12px;
}
.changelog-item {
  border-left: 2px solid var(--cinnabar);
  padding-left: 12px;
}
.log-header {
  display: flex;
  justify-content: space-between;
  font-size: 13px;
  margin-bottom: 4px;
}
.log-version {
  font-weight: 500;
  color: var(--ink-black);
  letter-spacing: 0.05em;
}
.log-date {
  color: var(--ink-gray);
}
.log-content {
  font-size: 13px;
  color: var(--ink-gray);
  line-height: 1.5;
  letter-spacing: 0.05em;
  margin: 0;
}

.mc-footer-hint {
  text-align: center;
  font-size: 12px;
  color: var(--ink-gray);
  letter-spacing: 0.1em;
  opacity: 0.8;
  margin-top: 10px;
}
</style>