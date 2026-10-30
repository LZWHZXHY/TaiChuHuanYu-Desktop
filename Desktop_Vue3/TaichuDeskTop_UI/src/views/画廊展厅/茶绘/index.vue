<!-- 茶绘：多人同屏绘画（每间最多 4 人，有图层）。这里是大厅和连接管理，进了画室交给 PaintStudio。 -->
<template>
  <article class="chahui">
    <PaintStudio v-if="room && connection" :key="studioKey" :connection="connection" :room="room" :me="String(user.userId ?? '')" @left="onLeft" @reload="reload" />

    <template v-else>
      <header class="ch-head">
        <div>
          <p class="eyebrow">CHAHUI · LIVE CANVAS / 茶绘</p>
          <h1>茶绘 · 一起画</h1>
          <p class="intro">开一间画室，最多 4 个人在同一张画布上同时画。每人有自己的图层，支持数位板压感，画完可以直接发到画廊。</p>
        </div>
        <div class="head-right">
          <span class="link-state" :class="{ on: connState === 'on' }"><i aria-hidden="true"></i>{{ connState === 'on' ? '已连接画室服务器' : connState === 'wait' ? '正在连接…' : '未连接' }}</span>
          <RouterLink to="/gallery" class="back">← 回到画廊</RouterLink>
        </div>
      </header>

      <p v-if="notice" class="ch-notice" :class="noticeKind" role="status">{{ notice }}<button type="button" aria-label="关闭提示" @click="notice = ''">×</button></p>
      <p v-if="!user.isLoggedIn" class="ch-notice">登录后才能进画室。<RouterLink :to="{ path: LOGIN_PATH, query: { redirect: route.fullPath } }">去登录</RouterLink></p>

      <div v-else class="lobby">
        <section class="panel">
          <p class="panel-tag">NEW ROOM / 开一间画室</p>
          <form class="create" @submit.prevent="create">
            <label>画室名称<input v-model.trim="form.name" maxlength="30" :placeholder="defaultName" /></label>
            <label>画布大小
              <select v-model="form.size">
                <option v-for="s in SIZES" :key="s.value" :value="s.value">{{ s.label }}</option>
              </select>
            </label>
            <label>密码（可选，只给朋友进）<input v-model.trim="form.password" maxlength="16" placeholder="不填就是公开画室" autocomplete="off" /></label>
            <button type="submit" class="btn primary" :disabled="!ready || busy">开画室</button>
          </form>
          <form class="join" @submit.prevent="join(joinId, joinPwd)">
            <h3>输入房间号</h3>
            <div class="join-row">
              <input v-model.trim="joinId" maxlength="16" placeholder="房间号" autocomplete="off" />
              <input v-model.trim="joinPwd" maxlength="16" placeholder="密码（没有就不填）" autocomplete="off" />
              <button type="submit" class="btn" :disabled="!ready || busy || !joinId">进入</button>
            </div>
          </form>
        </section>

        <section class="panel rooms-panel">
          <div class="rooms-head">
            <p class="panel-tag">ROOMS / 画室</p>
            <button type="button" class="btn small" :disabled="!ready" @click="loadLobby">刷新</button>
          </div>
          <ul v-if="rooms.length" class="rooms">
            <li v-for="r in rooms" :key="r.id" :class="{ full: r.full }">
              <span class="ratio" :style="{ aspectRatio: `${r.width} / ${r.height}` }" :title="`${r.width} × ${r.height}`"></span>
              <div class="room-main">
                <b>{{ r.name }}<em v-if="r.hasPassword" title="需要密码">🔒</em></b>
                <small>{{ r.members.length ? r.members.join('、') : '暂时没人' }} · {{ r.members.length }}/{{ MAX }} 人 · {{ r.layers }} 个图层</small>
              </div>
              <button v-if="!r.full" type="button" class="btn small primary" :disabled="busy" @click="joinListed(r)">进入</button>
              <span v-else class="room-state">已满</span>
            </li>
          </ul>
          <div v-else class="rooms-empty"><p>现在还没有画室。</p><small>开一间，把邀请链接发给朋友就能一起画。</small></div>
          <ul class="rules">
            <li>每间画室最多 {{ MAX }} 人；每人进来会自动有一个自己的图层，也可以新建图层。</li>
            <li>撤销只撤自己的笔画；图层的设置只有创建者或房主能改，锁定的图层只有创建者能画。</li>
            <li>画室只保存在服务器内存里：所有人离开 30 分钟后解散，服务器重启也会清空。画完记得导出或发到画廊。</li>
          </ul>
        </section>
      </div>
    </template>
  </article>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, shallowRef } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as signalR from '@microsoft/signalr'
import { useUserStore } from '@/stores/user'
import { LOGIN_PATH } from '@/router/auth'
import PaintStudio from './PaintStudio.vue'
import type { PaintRoomView } from './paintTypes'

interface LobbyRoom { id: string; name: string; width: number; height: number; hasPassword: boolean; members: string[]; full: boolean; layers: number }
const MAX = 4
const SIZES = [
  { value: '1600x1200', label: '1600 × 1200 · 横版 4:3' },
  { value: '1920x1080', label: '1920 × 1080 · 横版 16:9' },
  { value: '1080x1920', label: '1080 × 1920 · 竖版 9:16' },
  { value: '1600x1600', label: '1600 × 1600 · 方形' },
  { value: '1200x900', label: '1200 × 900 · 小画布（速涂）' },
]

const user = useUserStore()
const route = useRoute()
const router = useRouter()
const connection = shallowRef<signalR.HubConnection | null>(null)
const connState = ref<'wait' | 'on' | 'off'>('wait')
const room = ref<PaintRoomView | null>(null)
const studioKey = ref(0)
const rooms = ref<LobbyRoom[]>([])
const busy = ref(false)
const form = reactive({ name: '', size: SIZES[0]!.value, password: '' })
const joinId = ref(''), joinPwd = ref('')
const notice = ref(''), noticeKind = ref<'info' | 'error'>('info')
const defaultName = computed(() => `${user.userInfo?.username || '我'} 的画室`)
const ready = computed(() => user.isLoggedIn && connState.value === 'on')
let lobbyTimer = 0, noticeTimer = 0

function say(text: string, kind: 'info' | 'error' = 'info') {
  notice.value = text; noticeKind.value = kind
  window.clearTimeout(noticeTimer)
  noticeTimer = window.setTimeout(() => { notice.value = '' }, 5000)
}

function enter(r: PaintRoomView) {
  room.value = r
  studioKey.value++
  if (route.query.room !== r.roomId) void router.replace({ query: { ...route.query, room: r.roomId } })
}

async function loadLobby() {
  const c = connection.value
  if (!c || connState.value !== 'on') return
  try { rooms.value = await c.invoke<LobbyRoom[]>('Lobby') } catch { /* 忽略 */ }
}

async function create() {
  const c = connection.value
  if (!c || busy.value) return
  const [w, h] = form.size.split('x').map(Number)
  busy.value = true
  try {
    const res = await c.invoke<{ error: string | null; room: PaintRoomView | null }>('Create', form.name || defaultName.value, w, h, form.password || null)
    if (res.error || !res.room) say(res.error || '没能开画室', 'error')
    else enter(res.room)
  } catch { say('网络不稳定，没能开画室', 'error') } finally { busy.value = false }
}

async function join(id: string, password?: string) {
  const c = connection.value
  if (!c || busy.value || !id) return
  busy.value = true
  try {
    const res = await c.invoke<{ error: string | null; room: PaintRoomView | null }>('Join', id.trim(), password || null)
    if (res.error || !res.room) { say(res.error || '没能进入画室', 'error'); return false }
    enter(res.room)
    return true
  } catch { say('网络不稳定，没能进入画室', 'error'); return false } finally { busy.value = false }
}
async function joinListed(r: LobbyRoom) {
  let pwd: string | undefined
  if (r.hasPassword) {
    const v = window.prompt(`「${r.name}」需要密码`)
    if (v == null) return
    pwd = v
  }
  await join(r.id, pwd)
}

function onLeft(reason: string) {
  room.value = null
  if (reason === 'kicked') say('房主请你离开了画室', 'error')
  else if (reason === 'offline') say('掉线太久，已经离开画室', 'error')
  const q = { ...route.query }; delete q.room
  void router.replace({ query: q })
  void loadLobby()
}

/** 画室状态对不上时（例如刚好有人合并了图层）重新取一次 */
async function reload() {
  const c = connection.value
  if (!c) return
  try {
    const r = await c.invoke<PaintRoomView | null>('Resume')
    if (r) enter(r); else onLeft('left')
  } catch { say('重新载入画室失败，请刷新页面', 'error') }
}

async function connect() {
  if (!user.isLoggedIn) { connState.value = 'off'; return }
  const hubUrl = new URL('/signalr/paint', new URL(import.meta.env.VITE_API_BASE_URL || '/api', window.location.origin)).href
  const c = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, { accessTokenFactory: () => user.token || '' })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 20000, 30000])
    .build()
  connection.value = c
  c.onreconnecting(() => { connState.value = 'wait' })
  c.onreconnected(async () => {
    connState.value = 'on'
    // 断线期间可能漏了笔画：整个画室重新载入
    if (room.value) await reload()
    else void loadLobby()
  })
  c.onclose(() => { connState.value = 'off' })
  try {
    await c.start()
    connState.value = 'on'
    const r = await c.invoke<PaintRoomView | null>('Resume')
    if (r) enter(r)
    else if (typeof route.query.room === 'string' && route.query.room) {
      // 打开的是邀请链接：直接进（有密码的画室会提示输入）
      const id = route.query.room
      const ok = await join(id)
      if (!ok && notice.value === '密码不对') {
        const pwd = window.prompt('这间画室需要密码')
        if (pwd != null) await join(id, pwd)
      }
    }
    await loadLobby()
  } catch { connState.value = 'off' }
}

onMounted(async () => {
  await connect()
  lobbyTimer = window.setInterval(() => { if (!room.value && !document.hidden) void loadLobby() }, 6000)
})
onBeforeUnmount(() => {
  window.clearInterval(lobbyTimer); window.clearTimeout(noticeTimer)
  void connection.value?.stop()
})
</script>

<style scoped>
.chahui { display: grid; gap: 18px; color: var(--text-primary); }
.ch-head { position: relative; display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-end; gap: 12px 20px; padding-bottom: 14px; border-bottom: 1px solid var(--border-line); }
.ch-head::after { content: ''; position: absolute; left: 0; bottom: -1px; width: 64px; height: 3px; background: var(--accent-color); }
.eyebrow, .panel-tag { margin: 0 0 6px; font: 10px/1.2 var(--font-mono, monospace); letter-spacing: .16em; color: var(--accent-color); }
.ch-head h1 { margin: 0; font-size: 28px; letter-spacing: .04em; }
.intro { margin: 6px 0 0; max-width: 60ch; color: var(--text-secondary); font-size: 13px; line-height: 1.7; }
.head-right { display: flex; flex-wrap: wrap; align-items: center; gap: 14px; }
.link-state { display: inline-flex; align-items: center; gap: 8px; font: 11px var(--font-mono, monospace); color: var(--text-secondary); }
.link-state i { width: 7px; height: 7px; background: #d97706; }
.link-state.on i { background: var(--signal-mint, #2dd4bf); box-shadow: 0 0 0 3px color-mix(in srgb, var(--signal-mint, #2dd4bf) 25%, transparent); }
.back { color: var(--accent-color); font-size: 13px; text-decoration: none; }
.ch-notice { display: flex; justify-content: space-between; gap: 12px; margin: 0; padding: 10px 14px; border-left: 3px solid var(--accent-color); background: color-mix(in srgb, var(--accent-color) 8%, var(--paper-card)); font-size: 13px; }
.ch-notice.error { border-left-color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--paper-card)); }
.ch-notice button { border: 0; background: none; color: var(--text-secondary); cursor: pointer; font-size: 16px; line-height: 1; }

.lobby { display: grid; grid-template-columns: minmax(280px, 380px) minmax(0, 1fr); gap: 18px; align-items: start; }
.panel { position: relative; display: grid; gap: 16px; padding: 20px; border: 1px solid var(--border-line); background: var(--paper-card); }
.panel::before { content: ''; position: absolute; top: -1px; right: -1px; width: 28px; height: 6px; background: var(--hazard-stripe, repeating-linear-gradient(135deg, #f59e0b 0 4px, #111 4px 8px)); }
.create, .join { display: grid; gap: 10px; }
.join { padding-top: 16px; border-top: 1px dashed var(--border-line); }
h3 { margin: 0; font-size: 14px; }
label { display: grid; gap: 5px; font-size: 12px; color: var(--text-secondary); }
input, select { padding: 8px 10px; border: 1px solid var(--border-line); background: var(--bg-main); color: var(--text-primary); font: inherit; font-size: 14px; }
.join-row { display: grid; grid-template-columns: 1fr 1fr auto; gap: 8px; }
.join-row input { min-width: 0; }
.btn { padding: 8px 16px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 14px; cursor: pointer; }
.btn.small { padding: 5px 12px; font-size: 13px; }
.btn.primary { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent, #fff); font-weight: 600; }
.btn:disabled { opacity: .5; cursor: not-allowed; }
.rooms-head { display: flex; justify-content: space-between; align-items: center; }
.rooms-head .panel-tag { margin: 0; }
.rooms { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.rooms li { display: flex; align-items: center; gap: 14px; padding: 12px 14px; border: 1px solid var(--border-line); border-left: 3px solid var(--accent-color); background: var(--bg-main); }
.rooms li.full { border-left-color: var(--border-line); opacity: .75; }
.ratio { flex-shrink: 0; width: 40px; max-height: 40px; background: #fff; border: 1px solid var(--border-line); box-shadow: 2px 2px 0 color-mix(in srgb, var(--accent-color) 35%, transparent); }
.room-main { display: grid; gap: 3px; min-width: 0; flex: 1; }
.room-main b { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.room-main em { margin-left: 6px; font-style: normal; font-size: 12px; }
.room-main small, .room-state { color: var(--text-secondary); font-size: 12px; }
.rooms-empty { padding: 28px 16px; border: 1px dashed var(--border-line); text-align: center; color: var(--text-secondary); }
.rooms-empty p { margin: 0 0 4px; color: var(--text-primary); }
.rules { margin: 0; padding: 12px 0 0 18px; border-top: 1px solid var(--border-line); display: grid; gap: 4px; color: var(--text-secondary); font-size: 12px; line-height: 1.6; }

@media (max-width: 860px) { .lobby { grid-template-columns: 1fr; } .join-row { grid-template-columns: 1fr; } }
</style>
