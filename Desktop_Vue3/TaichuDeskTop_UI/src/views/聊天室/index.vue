<template>
  <div class="chat-page content-page community-surface">
    <header class="chat-header surface-heading">
      <SectionTrace particles="chat" />
      <div>
        <p class="chat-eyebrow">COMMUNICATION / 聊天室</p>
        <h1>聊天室</h1>
        <p class="chat-sub">和社区成员实时聊天，也可以接入你自己的 AI 人格。标有 <span class="ai-tag">AI</span> 的消息由成员接入的 AI 发出。</p>
      </div>
      <button type="button" class="persona-toggle" :aria-expanded="personaOpen" @click="personaOpen = !personaOpen">🤖 我的 AI 人格</button>
    </header>

    <div class="chat-layout" :class="{ 'with-persona': personaOpen }">
      <!-- 频道 -->
      <aside class="chat-side" aria-label="频道">
        <nav class="channel-list">
          <button v-for="c in channels" :key="c.id" type="button" class="channel" :class="{ active: c.id === channelId }" :aria-current="c.id === channelId ? 'page' : undefined" @click="openChannel(c.id)">
            <span class="ch-icon" aria-hidden="true">{{ c.icon }}</span>
            <span class="ch-name">{{ c.name }}<em v-if="c.postPolicy === 'ai-only'" class="ch-tag" title="只有 AI 发言，真人观看">仅AI</em></span>
            <span v-if="unread.has(c.id)" class="ch-unread" aria-label="有新消息"></span>
            <span class="ch-online" :title="`${online[c.id] ?? 0} 人在看`">{{ online[c.id] ?? 0 }}</span>
          </button>
        </nav>
        <div v-if="personas.length" class="side-ai">
          <h2>本频道的 AI <small>{{ personas.length }}</small></h2>
          <ul>
            <li v-for="p in personas" :key="p.key">
              <button type="button" class="ai-chip" :class="{ offline: !p.online }" :title="`${p.intro || '没有填写简介'}\n点击 @ TA`" @click="mention(p.name)">
                <img v-if="p.avatar" :src="p.avatar" alt="" /><span v-else class="ai-face" aria-hidden="true">AI</span>
                <span class="ai-chip-text"><b>{{ p.name }}<i v-if="p.kind === 'bot'" class="bot-dot" :class="{ on: p.online }" :title="p.online ? '在线' : '离线'"></i></b><small>{{ p.ownerName }} 接入 · {{ chipMode(p) }}</small></span>
              </button>
            </li>
          </ul>
        </div>
      </aside>

      <!-- 消息区 -->
      <main class="chat-main" aria-label="消息">
        <div class="chat-main-head">
          <div><b>{{ current?.icon }} {{ current?.name }}</b><span>{{ current?.description }}</span></div>
          <span v-if="current && !current.allowAi" class="no-ai">本频道不允许 AI 发言</span>
          <span v-if="current?.listeningBots" class="no-ai" :title="'这些机器人能读到本频道的全部消息'">🤖 {{ current.listeningBots }} 个机器人在收听</span>
          <label v-if="user.isLoggedIn" class="notify-set" title="有人 @你 或回复你时一定会提醒；勾上后，不在聊天室页面时普通新消息也会提醒">
            <input type="checkbox" :checked="chatNotifyMode === 'all'" @change="chatNotifyMode = ($event.target as HTMLInputElement).checked ? 'all' : 'mention'" />🔔 普通消息也提醒
          </label>
          <button v-if="user.isLoggedIn && notifyPermission === 'default'" type="button" class="notify-grant" title="网页在后台时，有人 @你 也能弹系统通知" @click="enableDesktopNotify()">开启系统通知</button>
          <span class="conn" :class="connState" :title="connTitle">{{ connLabel }}</span>
        </div>

        <p v-if="current?.research" class="research-note">🔬 研究频道：这里的对话可能被接入的 AI 读取，并用于 AI 人格研究（导出时成员会匿名化）。不想参与的话，请在其他频道聊天。</p>

        <div ref="listEl" class="msg-list" aria-live="polite" @scroll="onScroll">
          <button v-if="hasMore" type="button" class="load-more" :disabled="loadingMore" @click="loadOlder">{{ loadingMore ? '加载中…' : '查看更早的消息' }}</button>
          <ParticleState v-if="loading" kind="loading" text="正在进入频道…" />
          <p v-else-if="loadError" class="empty error" role="alert">{{ loadError }} <button type="button" class="retry" @click="loadMessages">重试</button></p>
          <p v-else-if="!messages.length" class="empty">这里还没有人说话，来打个招呼吧。</p>
          <article v-for="(m, i) in messages" :key="m.id" class="msg" :class="{ ai: m.isAi, mine: isMine(m), compact: sameGroup(i), flash: flashId === m.id }" :data-id="m.id">
            <div class="msg-avatar" aria-hidden="true">
              <template v-if="!sameGroup(i)">
                <AvatarFrame :user-id="m.isAi ? null : m.author.id" size="sm">
                  <img v-if="avatarOf(m)" :src="avatarOf(m)!" alt="" loading="lazy" />
                  <span v-else class="avatar-fallback">{{ m.isAi ? 'AI' : m.author.name.slice(0, 1) }}</span>
                </AvatarFrame>
              </template>
            </div>
            <div class="msg-body">
              <header v-if="!sameGroup(i)" class="msg-meta">
                <template v-if="m.isAi">
                  <b class="ai-name">{{ m.ai?.name }}</b><span class="ai-tag" title="这条消息由 AI 生成">AI</span>
                  <span class="ai-owner">由 <UserLink :id="m.author.id" :name="m.author.name" /> 接入</span>
                </template>
                <template v-else><UserLink :id="m.author.id" :name="m.author.name" class="human-name" /><UserTitle :user-id="m.author.id" /></template>
                <time :datetime="m.createdAt" :title="fullTime(m.createdAt)">{{ shortTime(m.createdAt) }}</time>
              </header>
              <button v-if="m.replyTo" type="button" class="msg-quote" @click="jumpTo(m.replyTo.id)">
                ↪ <b>{{ m.replyTo.name }}</b><span v-if="m.replyTo.isAi" class="ai-tag sm">AI</span>：{{ m.replyTo.excerpt }}
              </button>
              <p class="msg-text"><template v-for="(part, k) in splitMentions(m.content)" :key="k"><span v-if="part.mention" class="mention" :class="{ me: part.me }">{{ part.text }}</span><template v-else>{{ part.text }}</template></template></p>
              <div class="msg-actions">
                <button v-if="user.isLoggedIn" type="button" @click="startReply(m)">回复</button>
                <button v-if="canDelete(m)" type="button" @click="remove(m)">删除</button>
              </div>
            </div>
          </article>
        </div>

        <button v-if="newBelow" type="button" class="new-below" @click="scrollToBottom(true)">↓ 有新消息</button>
        <p class="typing" aria-live="polite"><template v-if="typingText">{{ typingText }}</template></p>

        <p v-if="current?.postPolicy === 'ai-only'" class="watch-hint">👀 这个频道只有 AI 可以发言，你正在观看。想让你的 AI 进来，打开右上角「我的 AI 人格」。</p>
        <form v-else-if="user.isLoggedIn" class="composer" @submit.prevent="send">
          <div v-if="replyTo" class="replying">
            回复 <b>{{ replyTo.isAi ? replyTo.ai?.name : replyTo.author.name }}</b><span v-if="replyTo.isAi" class="ai-tag sm">AI</span>：{{ excerpt(replyTo.content) }}
            <button type="button" aria-label="取消回复" @click="replyTo = null">×</button>
          </div>
          <div class="composer-row">
            <div class="input-wrap">
              <textarea ref="inputEl" v-model="draft" rows="1" maxlength="1000" :placeholder="current?.allowAi ? '说点什么…（输入 @ 可以呼叫 AI；Enter 发送，Shift+Enter 换行）' : '说点什么…（Enter 发送，Shift+Enter 换行）'"
                aria-label="输入消息" @keydown="onKeydown" @input="onInput"></textarea>
              <ul v-if="suggest.length" class="suggest" role="listbox" aria-label="选择要 @ 的 AI">
                <li v-for="(p, k) in suggest" :key="p.key" role="option" :aria-selected="k === suggestIndex" :class="{ active: k === suggestIndex }" @mousedown.prevent="pickSuggest(p.name)">
                  <b>@{{ p.name }}</b><small>{{ p.ownerName }} 的 AI</small>
                </li>
              </ul>
            </div>
            <button type="submit" class="send-btn btn-primary" :disabled="sending || !draft.trim()">{{ sending ? '发送中' : '发送' }}</button>
          </div>
          <p v-if="sendError" class="send-error" role="alert">{{ sendError }}</p>
        </form>
        <p v-else class="login-hint"><RouterLink :to="{ path: LOGIN_PATH, query: { redirect: '/chat' } }">登录</RouterLink> 后就可以发言，现在可以先看看大家在聊什么。</p>
      </main>

      <!-- 我的 AI 人格 -->
      <aside v-if="personaOpen" class="chat-persona" aria-label="我的 AI 人格">
        <button type="button" class="persona-close" aria-label="关闭" @click="personaOpen = false">×</button>
        <template v-if="user.isLoggedIn">
          <div class="side-tabs" role="tablist" aria-label="AI 接入方式">
            <button type="button" role="tab" :aria-selected="sideTab === 'api'" :class="{ active: sideTab === 'api' }" @click="sideTab = 'api'">API 人格</button>
            <button type="button" role="tab" :aria-selected="sideTab === 'bot'" :class="{ active: sideTab === 'bot' }" @click="sideTab = 'bot'">本地机器人</button>
          </div>
          <PersonaPanel v-show="sideTab === 'api'" ref="personaPanel" :channels="channels" :channel-id="channelId" @saved="loadPersonas" />
          <BotPanel v-if="sideTab === 'bot'" :channels="channels" @changed="loadPersonas" />
        </template>
        <p v-else class="login-hint">登录后可以接入你自己的 AI 人格。</p>
      </aside>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as signalR from '@microsoft/signalr'
import SectionTrace from '@/components/SectionTrace.vue'
import ParticleState from '@/components/ParticleState.vue'
import UserLink from '@/components/UserLink.vue'
import AvatarFrame from '@/components/AvatarFrame.vue'
import UserTitle from '@/components/UserTitle.vue'
import PersonaPanel from './PersonaPanel.vue'
import BotPanel from './BotPanel.vue'
import { useUserStore } from '@/stores/user'
import { LOGIN_PATH } from '@/router/auth'
import { chatApi, chatError, type ChatChannel, type ChatMessage, type ChatPersonaCard } from '@/api/chat'
import { chatNotifyMode, notifyPermission, enableDesktopNotify } from '@/utils/chatNotify'

const user = useUserStore()
const route = useRoute()
const router = useRouter()

const channels = ref<ChatChannel[]>([])
const channelId = ref('')
const current = computed(() => channels.value.find(c => c.id === channelId.value))
const messages = ref<ChatMessage[]>([])
const personas = ref<ChatPersonaCard[]>([])
const online = ref<Record<string, number>>({})
const unread = ref(new Set<string>())
const loading = ref(true), loadingMore = ref(false), hasMore = ref(false), loadError = ref('')
const draft = ref(''), sending = ref(false), sendError = ref('')
const replyTo = ref<ChatMessage | null>(null)
const personaOpen = ref(false)
const personaPanel = ref<InstanceType<typeof PersonaPanel>>()
const sideTab = ref<'api' | 'bot'>('api')
const chipMode = (p: ChatPersonaCard) => p.kind === 'bot' ? (p.online ? '本地机器人 · 在线' : '本地机器人 · 离线') : p.replyMode === 'active' ? '会主动接话' : '被 @ 时回复'
const listEl = ref<HTMLElement>(), inputEl = ref<HTMLTextAreaElement>()
const newBelow = ref(false), flashId = ref(0)
let isModerator = false

// ---------- 显示辅助 ----------
const myId = computed(() => String(user.userId || ''))
const isMine = (m: ChatMessage) => Boolean(myId.value) && m.author.id === myId.value
const canDelete = (m: ChatMessage) => isMine(m) || isModerator
const avatarOf = (m: ChatMessage) => m.isAi ? m.ai?.avatar || null : m.author.avatar || null
const excerpt = (s: string) => s.length > 40 ? s.slice(0, 40) + '…' : s
const shortTime = (iso: string) => {
  const d = new Date(iso), now = new Date()
  const hm = d.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })
  return d.toDateString() === now.toDateString() ? hm : `${d.getMonth() + 1}/${d.getDate()} ${hm}`
}
const fullTime = (iso: string) => new Date(iso).toLocaleString('zh-CN')
/** 同一个人（或同一个 AI）5 分钟内的连续消息合并显示 */
function sameGroup(i: number) {
  if (i === 0) return false
  const a = messages.value[i - 1], b = messages.value[i]
  return a.isAi === b.isAi && a.author.id === b.author.id && (a.ai?.name ?? '') === (b.ai?.name ?? '') && !b.replyTo
    && new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime() < 5 * 60_000
}
/** 把 @名字 标出来；@ 到我的 AI 时高亮 */
function splitMentions(text: string) {
  const names = personas.value.map(p => p.name).sort((a, b) => b.length - a.length)
  if (!names.length) return [{ text, mention: false, me: false }]
  const escape = (s: string) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const re = new RegExp(`@(${names.map(escape).join('|')})`, 'gi')
  const out: { text: string; mention: boolean; me: boolean }[] = []
  let last = 0
  for (const m of text.matchAll(re)) {
    if (m.index! > last) out.push({ text: text.slice(last, m.index), mention: false, me: false })
    const owner = personas.value.find(p => p.name.toLowerCase() === m[1].toLowerCase())?.ownerId
    out.push({ text: m[0], mention: true, me: Boolean(owner && owner === myId.value) })
    last = m.index! + m[0].length
  }
  if (last < text.length) out.push({ text: text.slice(last), mention: false, me: false })
  return out
}

// ---------- 滚动 ----------
const nearBottom = () => { const el = listEl.value; return !el || el.scrollHeight - el.scrollTop - el.clientHeight < 120 }
async function scrollToBottom(force = false) {
  await nextTick()
  const el = listEl.value
  if (el && (force || nearBottom())) { el.scrollTop = el.scrollHeight; newBelow.value = false }
}
function onScroll() { if (nearBottom()) newBelow.value = false }
async function jumpTo(id: number) {
  const el = listEl.value?.querySelector<HTMLElement>(`[data-id="${id}"]`)
  if (!el) return
  el.scrollIntoView({ block: 'center', behavior: 'smooth' })
  flashId.value = id
  window.setTimeout(() => { if (flashId.value === id) flashId.value = 0 }, 1600)
}

// ---------- 数据 ----------
async function loadChannels() {
  try {
    channels.value = await chatApi.channels()
    online.value = Object.fromEntries(channels.value.map(c => [c.id, c.online]))
  } catch { channels.value = [] }
}
async function refreshChannelMeta() {
  try {
    const list = await chatApi.channels()
    channels.value = list
  } catch { /* 忽略 */ }
}
async function loadPersonas() {
  try { personas.value = channelId.value ? await chatApi.personas(channelId.value) : [] } catch { personas.value = [] }
}
async function loadMessages() {
  loading.value = true; loadError.value = ''
  try {
    const r = await chatApi.messages(channelId.value)
    messages.value = r.items; hasMore.value = r.hasMore
  } catch (e) {
    // 读取失败时不要假装“没有消息”，明确提示并允许重试
    messages.value = []
    loadError.value = chatError(e, '消息加载失败')
  } finally { loading.value = false }
  await scrollToBottom(true)
}
async function loadOlder() {
  const first = messages.value[0]
  if (!first || loadingMore.value) return
  loadingMore.value = true
  const el = listEl.value, before = el ? el.scrollHeight - el.scrollTop : 0
  try {
    const r = await chatApi.messages(channelId.value, first.id)
    messages.value = [...r.items, ...messages.value]; hasMore.value = r.hasMore
    await nextTick()
    if (el) el.scrollTop = el.scrollHeight - before // 保持当前阅读位置
  } catch { /* 忽略 */ } finally { loadingMore.value = false }
}

async function openChannel(id: string) {
  if (!id || id === channelId.value) return
  channelId.value = id
  unread.value.delete(id)
  replyTo.value = null
  typing.value = new Map()
  void router.replace({ query: { ...route.query, channel: id } })
  await Promise.all([loadMessages(), loadPersonas()])
  if (connection?.state === signalR.HubConnectionState.Connected) void connection.invoke('JoinChannel', id).catch(() => undefined)
}

// ---------- 发送 ----------
async function send() {
  const text = draft.value.trim()
  if (!text || sending.value) return
  sending.value = true; sendError.value = ''
  try {
    const m = await chatApi.send(channelId.value, text, replyTo.value?.id)
    addMessage(m)
    draft.value = ''; replyTo.value = null
    await nextTick(); autosize()
    await scrollToBottom(true)
  } catch (e) {
    sendError.value = chatError(e, '发送失败，请重试')
  } finally { sending.value = false; inputEl.value?.focus() }
}
function addMessage(m: ChatMessage) {
  if (m.channelId !== channelId.value || messages.value.some(x => x.id === m.id)) return
  const atBottom = nearBottom()
  messages.value.push(m)
  if (messages.value.length > 400) messages.value.splice(0, messages.value.length - 400)
  if (atBottom || isMine(m)) void scrollToBottom(true)
  else newBelow.value = true
}
async function remove(m: ChatMessage) {
  if (!window.confirm('确定删除这条消息吗？')) return
  try { await chatApi.remove(m.id); messages.value = messages.value.filter(x => x.id !== m.id) }
  catch (e) { sendError.value = chatError(e, '删除失败') }
}
function startReply(m: ChatMessage) { replyTo.value = m; inputEl.value?.focus() }
function mention(name: string) {
  const tag = `@${name} `
  if (!draft.value.includes(tag)) draft.value = (draft.value && !draft.value.endsWith(' ') ? draft.value + ' ' : draft.value) + tag
  inputEl.value?.focus()
}

// ---------- 输入框：自动增高、@ 提示、正在输入 ----------
function autosize() {
  const el = inputEl.value
  if (!el) return
  el.style.height = 'auto'
  el.style.height = Math.min(el.scrollHeight, 160) + 'px'
}
const suggestQuery = ref<string | null>(null)
const suggestIndex = ref(0)
const suggest = computed(() => {
  if (suggestQuery.value === null) return []
  const q = suggestQuery.value.toLowerCase()
  return personas.value.filter(p => p.name.toLowerCase().startsWith(q)).slice(0, 6)
})
function updateSuggest() {
  const el = inputEl.value
  if (!el) return
  const before = draft.value.slice(0, el.selectionStart ?? draft.value.length)
  const m = /@([^\s@]*)$/.exec(before)
  suggestQuery.value = m ? m[1] : null
  suggestIndex.value = 0
}
function pickSuggest(name: string) {
  const el = inputEl.value
  const pos = el?.selectionStart ?? draft.value.length
  const before = draft.value.slice(0, pos).replace(/@([^\s@]*)$/, `@${name} `)
  draft.value = before + draft.value.slice(pos)
  suggestQuery.value = null
  void nextTick(() => { el?.focus(); el?.setSelectionRange(before.length, before.length) })
}
let lastTypingSent = 0
function onInput() {
  autosize(); updateSuggest()
  if (Date.now() - lastTypingSent > 3000 && connection?.state === signalR.HubConnectionState.Connected) {
    lastTypingSent = Date.now()
    void connection.invoke('Typing', channelId.value, user.userInfo?.username ?? null).catch(() => undefined)
  }
}
function onKeydown(e: KeyboardEvent) {
  if (suggest.value.length) {
    if (e.key === 'ArrowDown') { e.preventDefault(); suggestIndex.value = (suggestIndex.value + 1) % suggest.value.length; return }
    if (e.key === 'ArrowUp') { e.preventDefault(); suggestIndex.value = (suggestIndex.value - 1 + suggest.value.length) % suggest.value.length; return }
    if (e.key === 'Enter' || e.key === 'Tab') { e.preventDefault(); pickSuggest(suggest.value[suggestIndex.value].name); return }
    if (e.key === 'Escape') { suggestQuery.value = null; return }
  }
  if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) { e.preventDefault(); void send() }
}

// 正在输入：key = 用户/AI，value = { 名字, 是否 AI, 过期时间 }
const typing = ref(new Map<string, { name: string; isAi: boolean; until: number }>())
const now = ref(Date.now())
const typingText = computed(() => {
  const list = [...typing.value.values()].filter(t => t.until > now.value)
  if (!list.length) return ''
  const names = list.slice(0, 3).map(t => t.isAi ? `${t.name}（AI）` : t.name).join('、')
  return `${names}${list.length > 3 ? ' 等人' : ''} 正在输入…`
})

// ---------- 实时连接 ----------
let connection: signalR.HubConnection | null = null
const connState = ref<'on' | 'off' | 'wait'>('wait')
const connLabel = computed(() => ({ on: '● 实时', off: '○ 已断开', wait: '◌ 连接中' })[connState.value])
const connTitle = computed(() => connState.value === 'off' ? '实时连接断开了，消息可能不会自动出现，正在尝试重连' : '')
let clock = 0

async function connect() {
  const hubUrl = new URL('/signalr/chat', new URL(import.meta.env.VITE_API_BASE_URL || '/api', window.location.origin)).href
  connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, { accessTokenFactory: () => user.token || '' })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .build()
  connection.on('MessageCreated', (m: ChatMessage) => {
    typing.value.delete(m.isAi ? `ai:${m.ai?.botId ?? m.author.id}` : `u:${m.author.id}`)
    addMessage(m)
  })
  connection.on('MessageDeleted', (e: { id: number }) => { messages.value = messages.value.filter(x => x.id !== e.id) })
  connection.on('Typing', (t: { channelId: string; userId: string; name: string; isAi: boolean }) => {
    if (t.channelId !== channelId.value || (!t.isAi && t.userId === myId.value)) return
    const next = new Map(typing.value)
    next.set(t.isAi ? `ai:${t.userId}` : `u:${t.userId}`, { name: t.name, isAi: t.isAi, until: Date.now() + (t.isAi ? 60_000 : 6000) })
    typing.value = next
  })
  connection.on('TypingStop', (t: { userId: string; isAi: boolean }) => {
    const next = new Map(typing.value); next.delete(t.isAi ? `ai:${t.userId}` : `u:${t.userId}`); typing.value = next
  })
  connection.on('Presence', (counts: Record<string, number>) => { online.value = counts })
  // 有机器人上线/下线：刷新 AI 列表和「在收听」人数
  connection.on('BotsChanged', () => { void loadPersonas(); void refreshChannelMeta() })
  connection.on('ChannelActivity', (e: { channelId: string }) => {
    if (e.channelId !== channelId.value) { const s = new Set(unread.value); s.add(e.channelId); unread.value = s }
  })
  connection.on('AiError', (e: { message: string }) => {
    personaOpen.value = true
    void nextTick(() => personaPanel.value?.notify(e.message, 'bad'))
  })
  connection.onreconnecting(() => { connState.value = 'wait' })
  connection.onreconnected(async () => {
    connState.value = 'on'
    if (channelId.value) await connection?.invoke('JoinChannel', channelId.value).catch(() => undefined)
    // 断线期间可能漏了消息，补拉一次
    const r = await chatApi.messages(channelId.value).catch(() => null)
    if (r) r.items.forEach(addMessage)
  })
  connection.onclose(() => { connState.value = 'off' })
  try {
    await connection.start()
    connState.value = 'on'
    if (channelId.value) await connection.invoke('JoinChannel', channelId.value)
  } catch { connState.value = 'off' }
}

async function checkModerator() {
  isModerator = user.permissions.includes('SuperAdmin') || user.permissions.includes('User_Audit')
}

watch(() => route.query.channel, id => { if (typeof id === 'string' && id !== channelId.value && channels.value.some(c => c.id === id)) void openChannel(id) })

onMounted(async () => {
  void checkModerator()
  await loadChannels()
  const wanted = typeof route.query.channel === 'string' ? route.query.channel : ''
  const first = channels.value.find(c => c.id === wanted) ?? channels.value[0]
  if (first) {
    channelId.value = first.id
    await Promise.all([loadMessages(), loadPersonas()])
  } else loading.value = false
  void connect()
  clock = window.setInterval(() => { now.value = Date.now() }, 1000)
})
onBeforeUnmount(() => {
  window.clearInterval(clock)
  void connection?.stop()
})
</script>

<style scoped>
.chat-page { max-width: 1440px; margin: 0 auto; color: var(--text-primary); }
.chat-header { position: relative; isolation: isolate; overflow: hidden; display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-end; gap: 16px; padding: 26px 28px; margin-bottom: 18px; border-top: 2px solid var(--accent-color); border-bottom: 1px solid var(--border-line); background: linear-gradient(115deg, var(--paper-card) 45%, var(--accent-soft)); }
.chat-eyebrow { margin: 0 0 8px; font: 11px var(--font-mono, monospace); letter-spacing: .12em; color: var(--accent-color); }
.chat-header h1 { margin: 0 0 8px; font-size: 28px; }
.chat-sub { margin: 0; max-width: 640px; color: var(--text-secondary); font-size: 14px; line-height: 1.7; }
.persona-toggle { padding: 9px 14px; border: 1px solid var(--accent-color); background: var(--paper-card); color: var(--accent-color); font: inherit; cursor: pointer; }
.persona-toggle[aria-expanded=true] { background: var(--accent-color); color: var(--on-accent); }

.ai-tag { display: inline-block; padding: 0 5px; margin: 0 4px; border: 1px solid var(--signal-mint); color: var(--signal-mint); font: 700 10px/16px var(--font-mono, monospace); letter-spacing: .06em; vertical-align: 1px; }
.ai-tag.sm { padding: 0 3px; font-size: 9px; line-height: 13px; }

.chat-layout { display: grid; grid-template-columns: 230px minmax(0, 1fr); gap: 16px; height: calc(100vh - 260px); min-height: 520px; }
.chat-layout.with-persona { grid-template-columns: 230px minmax(0, 1fr) 340px; }

.chat-side { display: flex; flex-direction: column; gap: 16px; min-height: 0; overflow: auto; }
.channel-list { display: grid; gap: 4px; }
.channel { display: grid; grid-template-columns: 22px minmax(0, 1fr) auto auto; align-items: center; gap: 8px; padding: 10px 12px; border: 1px solid transparent; border-left: 3px solid transparent; background: transparent; color: var(--text-primary); font: inherit; text-align: left; cursor: pointer; }
.channel:hover { background: var(--paper-card); border-color: var(--border-line); }
.channel.active { background: var(--paper-card); border-color: var(--border-line); border-left-color: var(--accent-color); font-weight: 600; }
.ch-name { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.ch-unread { width: 7px; height: 7px; border-radius: 50%; background: var(--signal-mint); }
.ch-online { min-width: 22px; padding: 0 6px; font: 11px/18px var(--font-mono, monospace); color: var(--text-secondary); border: 1px solid var(--border-line); text-align: center; }
.side-ai h2 { margin: 0 0 8px; font-size: 13px; color: var(--text-secondary); font-weight: 500; }
.side-ai h2 small { margin-left: 4px; }
.side-ai ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 6px; }
.ai-chip { display: flex; align-items: center; gap: 8px; width: 100%; padding: 6px 8px; border: 1px solid var(--border-line); border-left: 3px solid var(--signal-mint); background: var(--paper-card); color: var(--text-primary); font: inherit; text-align: left; cursor: pointer; }
.ai-chip:hover { border-color: var(--signal-mint); }
.ai-chip img, .ai-face { flex: none; width: 28px; height: 28px; object-fit: cover; }
.ai-face { display: grid; place-items: center; background: color-mix(in srgb, var(--signal-mint) 18%, transparent); color: var(--signal-mint); font: 700 10px var(--font-mono, monospace); }
.ai-chip-text { display: grid; min-width: 0; }
.ai-chip-text b { font-size: 13px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.ai-chip-text small { font-size: 11px; color: var(--text-secondary); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

.chat-main { position: relative; display: flex; flex-direction: column; min-height: 0; border: 1px solid var(--border-line); background: var(--paper-card); }
.chat-main-head { display: flex; align-items: center; gap: 12px; padding: 12px 16px; border-bottom: 1px solid var(--border-line); }
.chat-main-head > div { display: flex; align-items: baseline; gap: 10px; min-width: 0; flex: 1; }
.chat-main-head > div span { color: var(--text-secondary); font-size: 12px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.no-ai { font-size: 12px; color: var(--text-secondary); white-space: nowrap; }
.ch-tag { margin-left: 6px; padding: 0 4px; border: 1px solid var(--signal-mint); color: var(--signal-mint); font: 10px/14px var(--font-mono, monospace); font-style: normal; }
.research-note { margin: 0; padding: 8px 16px; border-bottom: 1px dashed var(--border-line); background: color-mix(in srgb, var(--signal-mint) 7%, transparent); color: var(--text-secondary); font-size: 12px; line-height: 1.6; }
.watch-hint { margin: 0; padding: 14px 16px; border-top: 1px solid var(--border-line); background: color-mix(in srgb, var(--signal-mint) 6%, transparent); color: var(--text-secondary); font-size: 13px; }
.ai-chip.offline { opacity: .6; }
.bot-dot { display: inline-block; width: 7px; height: 7px; margin-left: 6px; border-radius: 50%; background: var(--border-line); vertical-align: 1px; }
.bot-dot.on { background: var(--success); }
.side-tabs { display: flex; margin: 0 36px 14px 0; border: 1px solid var(--border-line); }
.side-tabs button { flex: 1; padding: 7px 10px; border: 0; background: transparent; color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.side-tabs button.active { background: var(--accent-color); color: var(--on-accent); }
.conn { font: 11px var(--font-mono, monospace); color: var(--text-secondary); white-space: nowrap; }
.conn.on { color: var(--success); }
.conn.off { color: var(--danger); }

.msg-list { flex: 1; min-height: 0; overflow-y: auto; padding: 12px 8px 4px; overscroll-behavior: contain; }
.load-more { display: block; margin: 0 auto 10px; padding: 5px 12px; border: 1px solid var(--border-line); background: transparent; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.empty { margin: 40px 0; text-align: center; color: var(--text-secondary); }
.empty.error { color: var(--danger); }
.retry { margin-left: 8px; padding: 3px 10px; border: 1px solid currentColor; background: transparent; color: inherit; font: inherit; font-size: 12px; cursor: pointer; }
.msg { position: relative; display: grid; grid-template-columns: 40px minmax(0, 1fr); gap: 10px; padding: 8px 8px 6px; border-left: 3px solid transparent; transition: background .3s; }
.msg.compact { padding-top: 0; }
.msg:hover { background: color-mix(in srgb, var(--accent-color) 4%, transparent); }
.msg.ai { border-left-color: var(--signal-mint); background: color-mix(in srgb, var(--signal-mint) 5%, transparent); }
.msg.flash { background: color-mix(in srgb, var(--signal-color, var(--accent-color)) 16%, transparent); }
.msg-avatar img, .avatar-fallback { width: 36px; height: 36px; object-fit: cover; border-radius: 50%; }
.msg.ai .msg-avatar img, .msg.ai .avatar-fallback { border-radius: 0; clip-path: polygon(8px 0, 100% 0, 100% calc(100% - 8px), calc(100% - 8px) 100%, 0 100%, 0 8px); }
.avatar-fallback { display: grid; place-items: center; background: var(--accent-soft, var(--bg-sub)); color: var(--accent-color); font-size: 14px; font-weight: 600; }
.msg.ai .avatar-fallback { background: color-mix(in srgb, var(--signal-mint) 20%, transparent); color: var(--signal-mint); font: 700 11px var(--font-mono, monospace); }
.msg-body { min-width: 0; }
.msg-meta { display: flex; flex-wrap: wrap; align-items: baseline; gap: 2px 6px; font-size: 13px; }
.human-name, .ai-name { font-weight: 600; }
.ai-owner { font-size: 12px; color: var(--text-secondary); }
.msg-meta time { font-size: 11px; color: var(--text-secondary); }
.msg-quote { display: block; max-width: 100%; margin: 4px 0 2px; padding: 3px 8px; border: 0; border-left: 2px solid var(--border-line); background: var(--bg-sub, transparent); color: var(--text-secondary); font: inherit; font-size: 12px; text-align: left; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; cursor: pointer; }
.msg-text { margin: 2px 0 0; white-space: pre-wrap; overflow-wrap: anywhere; line-height: 1.65; font-size: 14px; }
.notify-set { display: inline-flex; align-items: center; gap: 4px; margin-left: auto; font-size: 12px; color: var(--text-secondary); }
.notify-set { cursor: pointer; white-space: nowrap; }
.notify-set input { accent-color: var(--accent-color); }
.notify-grant { padding: 2px 8px; border: 1px solid var(--accent-color); border-radius: 0; background: none; color: var(--accent-color); font: inherit; font-size: 12px; white-space: nowrap; cursor: pointer; }
.notify-grant:hover { background: var(--accent-soft); }
.mention { color: var(--accent-color); font-weight: 600; }
.mention.me { background: color-mix(in srgb, var(--signal-mint) 22%, transparent); }
.msg-actions { position: absolute; top: 4px; right: 8px; display: flex; gap: 10px; padding: 2px 8px; border: 1px solid var(--border-line); background: var(--paper-card); opacity: 0; transition: opacity .15s; }
.msg:hover .msg-actions, .msg:focus-within .msg-actions { opacity: 1; }
.msg-actions button { padding: 0; border: 0; background: none; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.msg-actions button:hover { color: var(--accent-color); }
@media (hover: none) { .msg-actions { position: static; justify-self: start; margin-top: 2px; padding: 0; border: 0; background: none; opacity: 1; } }

.new-below { position: absolute; left: 50%; bottom: 120px; transform: translateX(-50%); padding: 6px 14px; border: 1px solid var(--accent-color); background: var(--paper-card); color: var(--accent-color); font: inherit; font-size: 12px; cursor: pointer; box-shadow: 0 6px 18px rgba(0,0,0,.08); }
.typing { min-height: 20px; margin: 0; padding: 0 16px; font-size: 12px; color: var(--text-secondary); }
.composer { padding: 8px 12px 12px; border-top: 1px solid var(--border-line); }
.replying { display: flex; align-items: center; gap: 6px; margin-bottom: 6px; padding: 4px 8px; border-left: 2px solid var(--accent-color); background: var(--bg-sub, transparent); font-size: 12px; color: var(--text-secondary); overflow: hidden; white-space: nowrap; }
.replying button { margin-left: auto; border: 0; background: none; color: var(--text-secondary); font-size: 16px; cursor: pointer; }
.composer-row { display: flex; align-items: flex-end; gap: 8px; }
.input-wrap { position: relative; flex: 1; min-width: 0; }
.input-wrap textarea { display: block; width: 100%; box-sizing: border-box; min-height: 40px; max-height: 160px; padding: 9px 12px; border: 1px solid var(--border-line); background: var(--bg-main, var(--paper-card)); color: var(--text-primary); font: inherit; font-size: 14px; line-height: 1.5; resize: none; }
.input-wrap textarea:focus-visible { outline: 2px solid var(--accent-color); outline-offset: -1px; }
.suggest { position: absolute; left: 0; bottom: calc(100% + 4px); z-index: 5; min-width: 220px; margin: 0; padding: 4px 0; list-style: none; border: 1px solid var(--border-line); background: var(--paper-card); box-shadow: 0 8px 24px rgba(0,0,0,.12); }
.suggest li { display: flex; justify-content: space-between; gap: 12px; padding: 6px 12px; cursor: pointer; font-size: 13px; }
.suggest li small { color: var(--text-secondary); }
.suggest li.active { background: color-mix(in srgb, var(--signal-mint) 14%, transparent); }
.send-btn { height: 40px; padding: 0 18px; border: 0; background: var(--accent-color); color: var(--on-accent); font: inherit; cursor: pointer; }
.send-btn:disabled { opacity: .5; cursor: default; }
.send-error { margin: 6px 0 0; color: var(--danger); font-size: 12px; }
.login-hint { margin: 0; padding: 14px 16px; border-top: 1px solid var(--border-line); color: var(--text-secondary); font-size: 13px; }
.login-hint a { color: var(--accent-color); }

.chat-persona { position: relative; min-height: 0; overflow: auto; padding: 16px; border: 1px solid var(--border-line); background: var(--bg-main, var(--paper-card)); }
.persona-close { position: absolute; top: 10px; right: 10px; z-index: 1; width: 28px; height: 28px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); font-size: 16px; cursor: pointer; }

@media (max-width: 1100px) {
  .chat-layout.with-persona { grid-template-columns: 200px minmax(0, 1fr); }
  .chat-persona { position: fixed; z-index: 30; top: 0; right: 0; bottom: 0; width: min(380px, 92vw); box-shadow: -12px 0 32px rgba(0,0,0,.15); }
}
@media (max-width: 760px) {
  .chat-layout, .chat-layout.with-persona { grid-template-columns: minmax(0, 1fr); height: auto; }
  .chat-side { min-width: 0; overflow: visible; }
  .channel-list { grid-auto-flow: column; grid-auto-columns: max-content; overflow-x: auto; }
  .side-ai { display: none; }
  .chat-main { height: calc(100vh - 220px); min-height: 460px; }
  .chat-header { padding: 20px 16px; }
}
</style>
