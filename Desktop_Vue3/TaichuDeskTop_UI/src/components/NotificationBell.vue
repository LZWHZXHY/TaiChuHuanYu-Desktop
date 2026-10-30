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
          <UserLink :id="n.actorId" :name="n.actorName" @click="open = false">
            <img v-if="n.actorAvatar" :src="n.actorAvatar" class="notify-avatar" alt="" />
            <span v-else class="notify-avatar placeholder">👤</span>
          </UserLink>
          <div class="notify-body">
            <p class="notify-text"><UserLink :id="n.actorId" :name="n.actorName" @click="open = false"><strong>{{ n.actorName }}</strong></UserLink> {{ n.content }}</p>
            <time class="notify-time">{{ formatTime(n.createdAt) }}</time>
          </div>
          <span v-if="!n.isRead" class="notify-unread-dot"></span>
        </li>
      </ul>
      <!-- 聊天室提醒设置：@我 一定提醒；普通消息可选 -->
      <div class="notify-chat-set">
        <span>聊天室：有人 @你 或回复你时一定会提醒</span>
        <label class="notify-check">
          <input type="checkbox" :checked="chatNotifyMode === 'all'" @change="chatNotifyMode = ($event.target as HTMLInputElement).checked ? 'all' : 'mention'" />普通消息也提醒
        </label>
        <!-- 系统通知：浏览器允许了才能用 -->
        <label v-if="notifyPermission === 'granted'" class="notify-check">
          <input type="checkbox" :checked="chatNotifyDesktop" @change="toggleDesktop(($event.target as HTMLInputElement).checked)" />网页在后台时弹系统通知
        </label>
        <button v-else-if="notifyPermission === 'default'" type="button" class="notify-grant" @click="toggleDesktop(true)">开启系统通知（网页在后台也能收到 @你）</button>
        <span v-else-if="notifyPermission === 'denied'" class="notify-denied">浏览器禁止了通知，网页在后台时收不到系统通知。可在地址栏左边的网站设置里把「通知」改成允许。</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import UserLink from '@/components/UserLink.vue'
import { ref, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { notificationApi, type NotificationItem } from '@/api/notification'
import { useUserStore } from '@/stores/user'
import { uiSound } from '@/utils/uiSound'
import { ElMessage } from 'element-plus'
import { chatNotifyMode, chatNotifyDesktop, enableDesktopNotify, notifyPermission, refreshNotifyPermission } from '@/utils/chatNotify'

async function toggleDesktop(on: boolean) {
  if (!on) { chatNotifyDesktop.value = false; return }
  if (!(await enableDesktopNotify())) alertBlocked()
}
function alertBlocked() {
  // 浏览器拒绝了通知权限：只能在浏览器地址栏左边的网站设置里重新打开
  ElMessage.warning({ message: '浏览器没有允许通知。可以在地址栏左边的网站设置里把「通知」改成允许。', duration: 6000, showClose: true })
}

const userStore = useUserStore()
const router = useRouter()
const open = ref(false)
const unread = ref(0)
const items = ref<NotificationItem[]>([])
const loading = ref(false)
let timer: ReturnType<typeof setInterval> | null = null
let unreadLoaded = false

async function refreshUnread() {
  if (!userStore.isLoggedIn) return
  try {
    const r = await notificationApi.unreadCount()
    // 有新通知时响一声（首次读取不响）
    if (unreadLoaded && r.count > unread.value) uiSound.notify()
    unread.value = r.count
    unreadLoaded = true
  } catch { /* ignore */ }
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
  // 用户可能在浏览器设置里改过通知权限，每次打开面板重新读一下
  if (open.value) refreshNotifyPermission()
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
  position: relative; width: 32px; height: 32px; border-radius: 0;
  border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary);
  font-size: 1rem; cursor: pointer; display: flex; align-items: center; justify-content: center;
  transition: border-color .2s, transform .2s;
}
.notify-btn:hover, .notify-btn.active { border-color: var(--accent-color); transform: translateY(-1px); }
.notify-dot {
  position: absolute; top: -4px; right: -4px; min-width: 16px; height: 16px; padding: 0 4px;
  border-radius: 0; background: #ef4444; color: #fff; font-size: 10px; line-height: 16px;
  text-align: center; font-weight: 700;
}
.notify-panel {
  position: absolute; top: 46px; right: 0; width: 320px; max-height: 420px; overflow-y: auto;
  background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 0;
  box-shadow: 0 16px 40px rgba(0, 0, 0, .16); padding: 10px;
}
.notify-head { display: flex; align-items: center; justify-content: space-between; padding: 4px 6px 10px; }
.notify-head strong { font-size: .9rem; }
.notify-all { background: none; border: none; color: var(--accent-color); font-size: .74rem; cursor: pointer; }
.notify-empty { text-align: center; color: var(--text-secondary); font-size: .82rem; padding: 24px 0; margin: 0; }
.notify-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 2px; }
.notify-item {
  position: relative; display: flex; gap: 10px; padding: 9px 8px; border-radius: 0; cursor: pointer;
  transition: background .15s;
}
.notify-item:hover { background: var(--bg-sub); }
.notify-item.unread { background: var(--accent-soft); }
.notify-avatar { width: 30px; height: 30px; border-radius: 0; object-fit: cover; flex-shrink: 0; }
.notify-avatar.placeholder { display: flex; align-items: center; justify-content: center; background: var(--bg-sub); font-size: .9rem; }
.notify-body { min-width: 0; flex: 1; }
.notify-text { margin: 0; font-size: .8rem; line-height: 1.45; color: var(--text-primary); overflow-wrap: anywhere; }
.notify-text strong { color: var(--accent-color); }
.notify-time { font-size: .68rem; color: var(--text-secondary); }
.notify-chat-set { display: grid; gap: 6px; margin-top: 8px; padding: 10px 6px 2px; border-top: 1px solid var(--border-line); font-size: .74rem; color: var(--text-secondary); }
.notify-check { display: flex; align-items: center; gap: 6px; color: var(--text-primary); cursor: pointer; }
.notify-check input { accent-color: var(--accent-color); }
.notify-grant { justify-self: start; padding: 5px 10px; border: 1px solid var(--accent-color); border-radius: 0; background: none; color: var(--accent-color); font: inherit; font-size: .74rem; cursor: pointer; }
.notify-grant:hover { background: var(--accent-soft); }
.notify-denied { color: var(--text-secondary); line-height: 1.5; }
.notify-unread-dot { width: 7px; height: 7px; border-radius: 50%; background: #ef4444; flex-shrink: 0; align-self: center; }
@media(max-width: 820px) {
  .notify-panel { position: fixed; top: 110px; right: 12px; width: min(320px, calc(100vw - 24px)); }
}
</style>
