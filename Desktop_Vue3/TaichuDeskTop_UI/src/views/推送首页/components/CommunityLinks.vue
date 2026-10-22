<template>
  <div class="community-links">
    <header class="links-header">
      <h2>加入社区</h2>
      <span>一起交流 · 一起创造</span>
    </header>

    <div class="link-list">
      <a
        class="link-item"
        :href="OOPZ_URL"
        target="_blank"
        rel="noopener noreferrer"
        @click.prevent="openExternal(OOPZ_URL)"
      >
        <span class="link-emoji" aria-hidden="true">🎙</span>
        <div class="link-body">
          <strong>OOPZ</strong>
          <p>官方邀请链接</p>
        </div>
        <span class="link-arrow" aria-hidden="true">↗</span>
      </a>

      <a
        class="link-item"
        :href="QQ_GROUP_URL"
        target="_blank"
        rel="noopener noreferrer"
        @click.prevent="openExternal(QQ_GROUP_URL)"
      >
        <span class="link-emoji" aria-hidden="true">💬</span>
        <div class="link-body">
          <strong>QQ 外群</strong>
          <p>太初寰宇 - 太虚坛 - 外院</p>
        </div>
        <span class="link-arrow" aria-hidden="true">↗</span>
      </a>
    </div>
  </div>
</template>

<script setup lang="ts">
// 🌟 社区对外入口：OOPZ 语音房 + QQ 外群
const OOPZ_URL = 'https://oopz.cn/i/iZDd74'
const QQ_GROUP_URL = 'https://qm.qq.com/q/D8EaBjOygo'

// 桌面 WebView 环境下交给宿主打开系统浏览器，Web 环境直接新窗口打开。
function openExternal(url: string) {
  const host = (window as any).chrome?.webview
  if (host) host.postMessage({ cmd: 'load_external_url', url })
  else window.open(url, '_blank', 'noopener,noreferrer')
}
</script>

<style scoped>
.community-links { color: var(--text-primary); }
.links-header { display: flex; align-items: baseline; justify-content: space-between; gap: 16px; margin-bottom: 14px; }
.links-header h2 { margin: 0; font-size: 1.15rem; }
.links-header span { color: var(--text-secondary); font-size: .78rem; }
.link-list { display: grid; gap: 10px; }
.link-item {
  display: flex; align-items: center; gap: 12px;
  padding: 12px; border: 1px solid var(--border-line); border-radius: 8px;
  background: var(--bg-sub); text-decoration: none; color: inherit;
  transition: border-color .2s, background .2s, transform .2s;
}
.link-item:hover { background: var(--paper-card); border-color: var(--accent-color); transform: translateY(-1px); }
.link-emoji { font-size: 1.2rem; line-height: 1; }
.link-body { flex: 1; min-width: 0; }
.link-body strong { display: block; font-size: .9rem; font-weight: 600; }
.link-body p { margin: 3px 0 0; font-size: .75rem; color: var(--text-secondary); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.link-arrow { color: var(--text-secondary); font-size: .85rem; transition: color .2s; }
.link-item:hover .link-arrow { color: var(--accent-color); }
</style>
