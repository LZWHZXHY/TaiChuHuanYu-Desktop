<!-- ChatNotifier.vue：不在聊天室页面时，聊天室有人 @我 / 回复我就一定在右下角弹一条提醒；
     普通新消息要不要提醒由用户自己选（聊天室顶部或通知面板里的「普通消息也提醒」），可选系统通知。点提醒进到那个频道。 -->
<template>
  <div class="chat-toasts" aria-live="polite">
    <TransitionGroup name="toast">
      <button v-for="t in toasts" :key="t.key" type="button" class="chat-toast" :class="{ mention: t.mention }" @click="openToast(t)">
        <span class="t-head">
          <b>{{ t.mention ? '有人找你' : '聊天室新消息' }}</b>
          <span class="t-ch"># {{ t.channelName }}</span>
          <span class="t-close" role="button" aria-label="关闭提醒" @click.stop="dismiss(t.key)">×</span>
        </span>
        <span class="t-body"><b>{{ t.author }}</b>{{ t.mention ? (t.reply ? ' 回复了你：' : ' @了你：') : '：' }}{{ t.preview }}</span>
        <span v-if="t.more > 0" class="t-more">还有 {{ t.more }} 条新消息</span>
      </button>
    </TransitionGroup>
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as signalR from '@microsoft/signalr'
import { useUserStore } from '@/stores/user'
import { chatApi } from '@/api/chat'
import { uiSound } from '@/utils/uiSound'
import { chatNotifyMode, chatNotifyDesktop } from '@/utils/chatNotify'

interface Activity { channelId: string; messageId: number; authorId?: string | null; authorName?: string; isAi?: boolean; preview?: string; replyToUserId?: string | null }
interface Toast { key: string; channelId: string; channelName: string; author: string; preview: string; mention: boolean; reply: boolean; more: number; timer: number }

const user = useUserStore()
const route = useRoute()
const router = useRouter()
const toasts = ref<Toast[]>([])
let connection: signalR.HubConnection | null = null
let channelNames: Record<string, string> | null = null

async function channelName(id: string) {
  if (!channelNames) {
    try { channelNames = Object.fromEntries((await chatApi.channels()).map(c => [c.id, c.name])) } catch { channelNames = {} }
  }
  return channelNames[id] ?? '聊天室'
}

const escapeRe = (s: string) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
function mentionsMe(text: string) {
  const name = user.userInfo?.username
  if (!name || !text) return false
  return new RegExp(`@${escapeRe(name)}(?![\\w\\u4e00-\\u9fa5])`, 'i').test(text)
}

function dismiss(key: string) {
  const t = toasts.value.find(x => x.key === key)
  if (t) clearTimeout(t.timer)
  toasts.value = toasts.value.filter(x => x.key !== key)
}
function openToast(t: Toast) {
  dismiss(t.key)
  router.push({ path: '/chat', query: { channel: t.channelId } }).catch(() => {})
}

async function onActivity(e: Activity) {
  if (!user.isLoggedIn) return
  // 正在聊天室页面：页面自己会显示，不重复提醒
  if (route.path.toLowerCase().startsWith('/chat')) return
  const myId = String(user.userId || '').toLowerCase()
  if (e.authorId && String(e.authorId).toLowerCase() === myId) return
  const reply = Boolean(e.replyToUserId && String(e.replyToUserId).toLowerCase() === myId)
  const mention = reply || mentionsMe(e.preview ?? '')
  // @我 / 回复我一定提醒；普通消息看用户有没有打开「普通消息也提醒」
  if (!mention && chatNotifyMode.value !== 'all') return

  const name = await channelName(e.channelId)
  const author = e.authorName || '有人'
  const preview = (e.preview ?? '').replace(/\s+/g, ' ')
  // 普通新消息：同一频道已有提醒就合并，避免刷屏
  const existing = !mention ? toasts.value.find(t => t.channelId === e.channelId && !t.mention) : undefined
  if (existing) {
    existing.more++
    existing.author = author
    existing.preview = preview
    return
  }
  const key = `${e.channelId}:${e.messageId}`
  const toast: Toast = { key, channelId: e.channelId, channelName: name, author, preview, mention, reply, more: 0, timer: 0 }
  toast.timer = window.setTimeout(() => dismiss(key), mention ? 15000 : 8000)
  toasts.value = [...toasts.value, toast].slice(-3)
  uiSound.notify()

  // 网页在后台时：有人 @我 就在标签页标题上闪提示；允许了系统通知的再弹系统通知
  if (mention && document.hidden) flashTitle(reply ? '有人回复你' : '有人@你')
  if (chatNotifyDesktop.value && document.hidden && typeof Notification !== 'undefined' && Notification.permission === 'granted') {
    try {
      const n = new Notification(mention ? `${author} ${reply ? '回复了你' : '@了你'} · #${name}` : `#${name} 有新消息`, {
        body: mention ? preview : `${author}：${preview}`, tag: `taichu-chat-${e.channelId}`,
      })
      n.onclick = () => { window.focus(); openToast(toast); n.close() }
    } catch { /* 有的浏览器（比如手机）不支持直接 new Notification */ }
  }
}

// 标签页标题闪烁：切回网页后恢复原标题
let titleTimer = 0
let savedTitle = ''
function flashTitle(text: string) {
  if (titleTimer) return
  savedTitle = document.title
  let on = false
  titleTimer = window.setInterval(() => { on = !on; document.title = on ? `【${text}】${savedTitle}` : savedTitle }, 1000)
}
function stopFlash() {
  if (!titleTimer) return
  clearInterval(titleTimer)
  titleTimer = 0
  document.title = savedTitle
}
const onVisible = () => { if (!document.hidden) stopFlash() }
document.addEventListener('visibilitychange', onVisible)

async function connect() {
  if (connection) return
  const hubUrl = new URL('/signalr/chat', new URL(import.meta.env.VITE_API_BASE_URL || '/api', window.location.origin)).href
  const c = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, { accessTokenFactory: () => user.token || '' })
    .withAutomaticReconnect([0, 5000, 15000, 30000, 60000])
    .configureLogging(signalR.LogLevel.None)
    .build()
  c.on('ChannelActivity', (e: Activity) => { void onActivity(e) })
  connection = c
  try { await c.start() } catch { if (connection === c) connection = null }
}
async function disconnect() {
  const c = connection
  connection = null
  if (c) await c.stop().catch(() => undefined)
}

// 登录了就连（@我 一定要能收到）；换账号时重连
watch(() => user.token, async token => {
  await disconnect()
  channelNames = null
  if (token) await connect()
}, { immediate: true })

onBeforeUnmount(() => {
  document.removeEventListener('visibilitychange', onVisible)
  stopFlash()
  toasts.value.forEach(t => clearTimeout(t.timer))
  void disconnect()
})
</script>

<style scoped>
.chat-toasts { position: fixed; right: 18px; bottom: 18px; z-index: 1900; display: flex; flex-direction: column; gap: 10px; width: min(340px, calc(100vw - 36px)); pointer-events: none; }
.chat-toast { pointer-events: auto; display: grid; gap: 6px; padding: 12px 14px; border: 1px solid var(--border-line); border-left: 3px solid var(--accent-color); background: var(--paper-card); color: var(--text-primary); text-align: left; font: inherit; cursor: pointer; box-shadow: 0 12px 30px #0003; }
.chat-toast.mention { border-left-color: var(--signal-mint, var(--accent-color)); }
.chat-toast:hover { border-color: var(--accent-color); }
.t-head { display: flex; align-items: center; gap: 8px; font-size: 12px; }
.t-head b { color: var(--accent-color); }
.t-ch { color: var(--text-secondary); }
.t-close { margin-left: auto; padding: 0 4px; color: var(--text-secondary); font-size: 16px; line-height: 1; }
.t-close:hover { color: var(--text-primary); }
.t-body { font-size: 13px; line-height: 1.5; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; overflow-wrap: anywhere; }
.t-more { font-size: 12px; color: var(--text-secondary); }
.toast-enter-active, .toast-leave-active { transition: opacity .25s, transform .25s; }
.toast-enter-from, .toast-leave-to { opacity: 0; transform: translateY(10px); }
@media (prefers-reduced-motion: reduce) { .toast-enter-active, .toast-leave-active { transition: none; } }
</style>
