<template>
  <div v-if="userStore.isLoggedIn" class="notify-wrapper">
    <button class="notify-btn" :class="{ active: open }" aria-label="通知" @click.stop="toggle">
      <svg aria-hidden="true" viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M6 10a6 6 0 0 1 12 0c0 7 3 7 3 8H3c0-1 3-1 3-8ZM9 21h6" /></svg>
      <span v-if="unread > 0" class="notify-dot">{{ unread > 99 ? '99+' : unread }}</span>
    </button>

    <div v-if="open" class="notify-panel" @click.stop>
      <div class="notify-head">
        <strong>通知</strong>
        <button v-if="unread > 0" class="notify-all" @click="readAll">全部已读</button>
      </div>
      <p v-if="loading" class="notify-empty">加载中…</p>
      <p v-else-if="!items.length" class="notify-empty">暂无通知</p>
      <ul v-else class="notify-list">
        <li v-for="n in items" :key="n.id" class="notify-item" :class="{ unread: !n.isRead }" @click="openItem(n)">
          <img v-if="n.actorAvatar" :src="n.actorAvatar" class="notify-avatar" alt="" />
          <span v-else class="notify-avatar placeholder">👤</span>
          <div class="notify-body">
            <p class="notify-text"><strong>{{ n.actorName }}</strong> {{ n.content }}</p>
            <time class="notify-time">{{ formatTime(n.createdAt) }}</time>
          </div>
          <span v-if="!n.isRead" class="notify-unread-dot"></span>
        </li>
      </ul>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { notificationApi, type NotificationItem } from '@/api/notification'
import { useUserStore } from '@/stores/user'

const userStore = useUserStore()
const router = useRouter()
const open = ref(false)
const unread = ref(0)
const items = ref<NotificationItem[]>([])
const loading = ref(false)
let timer: ReturnType<typeof setInterval> | null = null

async function refreshUnread() {
  if (!userStore.isLoggedIn) return
  try { const r = await notificationApi.unreadCount(); unread.value = r.count } catch { /* ignore */ }
}

async function loadList() {
  loading.value = true
  try {
    const r = await notificationApi.list(1, 20)
    items.value = r.items
    unread.value = r.unread
  } catch { items.value = [] } finally { loading.value = false }
}

function toggle() {
  open.value = !open.value
  if (open.value) void loadList()
}

async function readAll() {
  await notificationApi.markAllRead()
  items.value = items.value.map(n => ({ ...n, isRead: true }))
  unread.value = 0
}

async function openItem(n: NotificationItem) {
  if (!n.isRead) {
    try { await notificationApi.markRead(n.id) } catch { /* ignore */ }
    n.isRead = true
    unread.value = Math.max(0, unread.value - 1)
  }
  open.value = false
  if (n.link) router.push(n.link).catch(() => {})
}

function formatTime(v: string) {
  if (!v) return ''
  const d = new Date(v)
  const diff = Date.now() - d.getTime()
  if (diff < 60000) return '刚刚'
  if (diff < 3600000) return `${Math.floor(diff / 60000)} 分钟前`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)} 小时前`
  return d.toLocaleDateString('zh-CN', { month: 'numeric', day: 'numeric' })
}

function onDocClick() { open.value = false }

onMounted(() => {
  document.addEventListener('click', onDocClick)
  if (userStore.isLoggedIn) {
    void refreshUnread()
    timer = setInterval(refreshUnread, 30000)
  }
})
onUnmounted(() => {
  document.removeEventListener('click', onDocClick)
  if (timer) clearInterval(timer)
})
</script>

<style scoped>
.notify-wrapper { position: relative; z-index: 60; }
.notify-btn {
  position: relative; width: 32px; height: 32px; border-radius: 3px;
  border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary);
  font-size: 1rem; cursor: pointer; display: flex; align-items: center; justify-content: center;
  transition: border-color .2s, transform .2s;
}
.notify-btn:hover, .notify-btn.active { border-color: var(--accent-color); transform: translateY(-1px); }
.notify-dot {
  position: absolute; top: -4px; right: -4px; min-width: 16px; height: 16px; padding: 0 4px;
  border-radius: 8px; background: #ef4444; color: #fff; font-size: 10px; line-height: 16px;
  text-align: center; font-weight: 700;
}
.notify-panel {
  position: absolute; top: 46px; right: 0; width: 320px; max-height: 420px; overflow-y: auto;
  background: var(--paper-card); border: 1px solid var(--border-line); border-radius: var(--radius-md);
  box-shadow: 0 16px 40px rgba(0, 0, 0, .16); padding: 10px;
}
.notify-head { display: flex; align-items: center; justify-content: space-between; padding: 4px 6px 10px; }
.notify-head strong { font-size: .9rem; }
.notify-all { background: none; border: none; color: var(--accent-color); font-size: .74rem; cursor: pointer; }
.notify-empty { text-align: center; color: var(--text-secondary); font-size: .82rem; padding: 24px 0; margin: 0; }
.notify-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 2px; }
.notify-item {
  position: relative; display: flex; gap: 10px; padding: 9px 8px; border-radius: 8px; cursor: pointer;
  transition: background .15s;
}
.notify-item:hover { background: var(--bg-sub); }
.notify-item.unread { background: var(--accent-soft); }
.notify-avatar { width: 30px; height: 30px; border-radius: 50%; object-fit: cover; flex-shrink: 0; }
.notify-avatar.placeholder { display: flex; align-items: center; justify-content: center; background: var(--bg-sub); font-size: .9rem; }
.notify-body { min-width: 0; flex: 1; }
.notify-text { margin: 0; font-size: .8rem; line-height: 1.45; color: var(--text-primary); overflow-wrap: anywhere; }
.notify-text strong { color: var(--accent-color); }
.notify-time { font-size: .68rem; color: var(--text-secondary); }
.notify-unread-dot { width: 7px; height: 7px; border-radius: 50%; background: #ef4444; flex-shrink: 0; align-self: center; }
@media(max-width: 820px) {
  .notify-panel { position: fixed; top: 110px; right: 12px; width: min(320px, calc(100vw - 24px)); }
}
</style>
