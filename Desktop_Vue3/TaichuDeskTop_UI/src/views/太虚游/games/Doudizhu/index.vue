<template>
  <div class="ddz" data-no-ui-sound>
    <!-- ================= 大厅 ================= -->
    <section v-if="!state" class="ddz-lobby">
      <header class="lobby-head">
        <div>
          <p class="eyebrow">DOUDIZHU · CARD TABLE / 斗地主</p>
          <h2>斗地主</h2>
          <p>三人一桌，叫分抢地主。输赢用 🥔 土豆结算，土豆每天免费领，只能在游戏区用，不能充值、转让或兑换。</p>
        </div>
        <PotatoWallet />
      </header>
      <p v-if="!user.isLoggedIn" class="ddz-notice">登录后才能入座打牌。</p>
      <p v-else-if="connState !== 'on'" class="ddz-notice">{{ connState === 'wait' ? '正在连接牌桌服务器…' : '没能连上牌桌服务器，请刷新重试（如果刚更新过网站，需要先部署后端）。' }}</p>

      <div class="tier-grid">
        <article v-for="t in lobby.tiers" :key="t.key" class="tier-card" :class="'tier-' + t.key">
          <div class="tier-top"><b>{{ t.name }}</b><span>底分 {{ t.base }}</span><span v-if="t.fee" class="fee">台费 {{ t.fee }}</span></div>
          <p>{{ t.minBalance ? `需要至少 ${t.minBalance.toLocaleString()} 土豆` : '不限土豆，随便玩' }}</p>
          <div class="tier-actions">
            <button type="button" class="btn-primary" data-burst :disabled="!ready" @click="act('Quick', t.key)">快速开始</button>
            <button type="button" class="btn-line" :disabled="!ready" @click="act('Create', t.key)">新开一桌</button>
          </div>
        </article>
      </div>

      <div class="room-list">
        <div class="room-list-head"><h3>正在进行的牌桌</h3><button type="button" class="link" @click="loadLobby">刷新</button></div>
        <p v-if="!lobby.rooms.length" class="muted">还没有牌桌，点上面的「快速开始」开一桌吧。人不够可以加电脑。</p>
        <ul v-else>
          <li v-for="r in lobby.rooms" :key="r.id">
            <span class="room-tier">{{ r.tierName }}</span>
            <span class="room-players">{{ r.players.map(p => p.away ? `${p.name}（暂离）` : p.name).join('、') }}</span>
            <span class="room-state">{{ r.phase === 'waiting' ? `空 ${r.open} 位` : '对局中' }}</span>
            <button type="button" class="btn-line sm" :disabled="!ready || r.phase !== 'waiting' || !r.open" @click="act('Join', r.id)">加入</button>
          </li>
        </ul>
      </div>
      <p class="rules-note">和电脑一起打的局按一半结算土豆，每天最多从电脑身上赢 3000；全是真人的局正常结算，输家余额不够时只扣到 0。</p>
    </section>

    <!-- ================= 牌桌 ================= -->
    <section v-else class="table" :class="'phase-' + state.phase">
      <header class="table-bar">
        <span class="tb-code">TABLE / {{ state.roomId.slice(0, 6).toUpperCase() }}</span>
        <span class="tb-tier">{{ state.tierName }} · 底分 {{ state.base }}<template v-if="state.fee"> · 台费 {{ state.fee }}</template></span>
        <span v-if="state.phase === 'playing' || state.phase === 'bidding'" class="tb-mult">倍数 ×{{ state.multiplier }}<em v-if="state.bombs">（{{ state.bombs }} 炸）</em></span>
        <span v-if="state.hasBots" class="tb-bots">有电脑 · 半额结算</span>
        <span class="tb-space"></span>
        <button type="button" class="tb-toggle" :class="{ on: sfxOn }" :aria-pressed="sfxOn" title="出牌、选牌、炸弹等音效" @click="toggleSfx">{{ sfxOn ? '♪ 音效开' : '♪ 音效关' }}</button>
        <button type="button" class="tb-toggle" :class="{ on: voiceOn }" :aria-pressed="voiceOn" title="用系统语音念出牌型" @click="toggleVoice">{{ voiceOn ? '◉ 语音报牌' : '○ 语音报牌' }}</button>
        <PotatoWallet compact />
        <button v-if="state.phase === 'waiting' || state.phase === 'ended'" type="button" class="btn-line sm" @click="act('Leave')">离开牌桌</button>
      </header>

      <div class="felt">
        <i class="reg tl" aria-hidden="true"></i><i class="reg tr" aria-hidden="true"></i><i class="reg bl" aria-hidden="true"></i><i class="reg br" aria-hidden="true"></i>
        <!-- 对手：左（上家）、右（下家） -->
        <div v-for="pos in OPPONENTS" :key="pos" class="opponent" :class="pos">
          <template v-if="seatAt(pos)">
            <div class="op-card" :class="{ turn: state.turn === seatAt(pos)!.seat, landlord: seatAt(pos)!.role === '地主' }">
              <AvatarFrame :user-id="seatAt(pos)!.isBot || seatAt(pos)!.empty ? null : seatAt(pos)!.userId" size="sm"><div class="avatar" :class="{ bot: seatAt(pos)!.isBot }">{{ seatAt(pos)!.empty ? '+' : seatAt(pos)!.isBot ? '🤖' : seatAt(pos)!.name.slice(0, 1) }}</div></AvatarFrame>
              <div class="op-info">
                <b>{{ seatAt(pos)!.empty ? '空位' : seatAt(pos)!.name }}</b>
                <UserTitle v-if="!seatAt(pos)!.isBot && !seatAt(pos)!.empty" :user-id="seatAt(pos)!.userId" class="ddz-title" />
                <small>
                  <em v-if="seatAt(pos)!.role" :class="seatAt(pos)!.role === '地主' ? 'role-l' : 'role-f'">{{ seatAt(pos)!.role }}</em>
                  <template v-if="state.phase === 'waiting' && !seatAt(pos)!.empty">{{ seatAt(pos)!.ready ? '已准备' : '未准备' }}</template>
                  <template v-if="!seatAt(pos)!.online">· 掉线托管中</template>
                  <template v-else-if="seatAt(pos)!.trustee">· 托管中</template>
                </small>
              </div>
              <div v-if="seatAt(pos)!.cardCount" class="card-count" :class="{ warn: seatAt(pos)!.cardCount <= 2 && state.phase === 'playing' }">{{ seatAt(pos)!.cardCount }}</div>
              <span v-if="state.turn === seatAt(pos)!.seat" class="clock">{{ secondsLeft }}</span>
              <button v-if="state.phase === 'waiting' && seatAt(pos)!.isBot && isOwner" type="button" class="kick" title="移除电脑" @click="act('KickBot', seatAt(pos)!.seat)">×</button>
            </div>
            <div class="played" :class="pos">
              <template v-if="seatAt(pos)!.hand"><Card v-for="c in seatAt(pos)!.hand!" :key="c" :id="c" small /></template>
              <template v-else-if="seatAt(pos)!.recent">
                <span v-if="seatAt(pos)!.recent!.bid !== null && seatAt(pos)!.recent!.bid !== undefined" class="say">{{ seatAt(pos)!.recent!.bid ? `${seatAt(pos)!.recent!.bid} 分` : '不叫' }}</span>
                <span v-else-if="!seatAt(pos)!.recent!.cards.length" class="say">不出</span>
                <template v-else><Card v-for="c in seatAt(pos)!.recent!.cards" :key="c" :id="c" small /><span class="type-label">{{ seatAt(pos)!.recent!.label }}</span></template>
              </template>
            </div>
          </template>
        </div>

        <!-- 中间：底牌、阶段提示 -->
        <div class="center">
          <div class="bottom-cards">
            <template v-if="state.bottom.length"><Card v-for="c in state.bottom" :key="c" :id="c" small /></template>
            <template v-else-if="state.phase !== 'waiting'"><span v-for="i in 3" :key="i" class="card-back sm"></span></template>
          </div>
          <p v-if="state.phase === 'waiting'" class="center-tip">
            {{ emptySeats ? `还差 ${emptySeats} 人` : '等所有人准备' }}
            <button v-if="emptySeats && isOwner" type="button" class="btn-line sm" @click="act('AddBot')">加一个电脑</button>
          </p>
          <p v-else-if="state.phase === 'bidding'" class="center-tip">叫分中 · 当前 {{ state.currentBid || '无人叫' }}{{ state.currentBid ? ' 分' : '' }}</p>
          <div v-if="state.phase === 'ended' && state.result" class="result" :class="myWin ? 'win' : 'lose'" role="status">
            <b>{{ myWin ? '胜利！' : '输了' }}</b>
            <span>{{ state.result.summary }}</span>
            <ul>
              <li v-for="s in state.seats" :key="s.seat"><span>{{ s.name }}<em>{{ s.role }}</em></span><b v-if="s.isBot" class="muted-b">—</b><b v-else :class="changeOf(s.seat) >= 0 ? 'up' : 'down'">{{ changeOf(s.seat) >= 0 ? '+' : '' }}{{ changeOf(s.seat) }} 🥔</b></li>
            </ul>
            <small v-if="state.fee" class="fee-note">以上已扣除每人台费 {{ state.fee }} 🥔</small>
          </div>
        </div>

        <!-- 自己 -->
        <div class="me">
          <div class="played mine">
            <template v-if="mySeat?.recent">
              <span v-if="mySeat.recent.bid !== null && mySeat.recent.bid !== undefined" class="say">{{ mySeat.recent.bid ? `${mySeat.recent.bid} 分` : '不叫' }}</span>
              <span v-else-if="!mySeat.recent.cards.length" class="say">不出</span>
              <template v-else><Card v-for="c in mySeat.recent.cards" :key="c" :id="c" small /><span class="type-label">{{ mySeat.recent.label }}</span></template>
            </template>
          </div>

          <div class="actions">
            <template v-if="state.phase === 'waiting'">
              <button type="button" class="btn-primary big" data-burst @click="act('Ready', !mySeat?.ready)">{{ mySeat?.ready ? '取消准备' : '准备' }}</button>
            </template>
            <template v-else-if="state.phase === 'ended'">
              <span class="muted">{{ secondsLeft }} 秒后可以开始下一局</span>
            </template>
            <template v-else-if="myTurn && state.phase === 'bidding'">
              <button type="button" class="btn-line" @click="act('Bid', 0)">不叫</button>
              <button v-for="n in [1, 2, 3]" :key="n" type="button" class="btn-primary" :disabled="n <= state.currentBid" @click="act('Bid', n)">{{ n }} 分</button>
              <span class="clock inline">{{ secondsLeft }}</span>
            </template>
            <template v-else-if="myTurn && state.phase === 'playing'">
              <button type="button" class="btn-line" :disabled="mustLead" @click="act('Pass')">不出</button>
              <button type="button" class="btn-line" @click="hint">{{ mustLead || hintOptions.length ? '提示' : '要不起' }}</button>
              <button type="button" class="btn-primary" data-burst :disabled="!canPlay" @click="playSelected">出牌<small v-if="selectedPattern">（{{ PATTERN_LABEL[selectedPattern.type] }}）</small></button>
              <span class="clock inline">{{ secondsLeft }}</span>
            </template>
            <span v-else-if="state.phase === 'bidding' || state.phase === 'playing'" class="muted">等待 {{ state.seats[state.turn]?.name }} {{ state.phase === 'bidding' ? '叫分' : '出牌' }}…</span>
            <button v-if="state.phase === 'bidding' || state.phase === 'playing'" type="button" class="btn-line sm trustee" :class="{ on: mySeat?.trustee }" @click="act('Trustee', !mySeat?.trustee)">{{ mySeat?.trustee ? '取消托管' : '托管' }}</button>
          </div>
          <p v-if="error" class="ddz-error" role="alert">{{ error }}</p>

          <div class="hand" :class="{ landlord: mySeat?.role === '地主' }" role="group" aria-label="我的手牌">
            <button v-for="c in state.myHand" :key="c" type="button" class="hand-card" :class="{ up: selected.has(c) }" :aria-pressed="selected.has(c)" @click="toggle(c)"><Card :id="c" /></button>
          </div>
          <div class="me-info">
            <b>{{ mySeat?.name }}</b><UserTitle :user-id="user.userId" />
            <em v-if="mySeat?.role" :class="mySeat.role === '地主' ? 'role-l' : 'role-f'">{{ mySeat.role }}</em>
            <span v-if="state.phase === 'playing'">剩 {{ state.myHand.length }} 张</span>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, defineComponent, h, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import * as signalR from '@microsoft/signalr'
import { useUserStore } from '@/stores/user'
import PotatoWallet from '../../PotatoWallet.vue'
import AvatarFrame from '@/components/AvatarFrame.vue'
import UserTitle from '@/components/UserTitle.vue'
import { notifyPotatoChanged } from '@/api/potato'
import request from '@/utils/request'
import { PATTERN_LABEL, analyze, beats, chooseLead, findBeats, rankOf, suitOf, type Pattern } from './ddzRules'
import { announce, sfx, sfxOn, speak, voiceOn } from './ddzSound'

// ---------- 类型 ----------
interface Recent { cards: number[]; type?: string | null; label?: string | null; bid?: number | null }
interface SeatView { seat: number; userId?: string | null; name: string; isBot: boolean; ready: boolean; online: boolean; trustee: boolean; empty: boolean; cardCount: number; role?: string | null; recent?: Recent | null; hand?: number[] | null }
interface TableState {
  roomId: string; tier: string; tierName: string; base: number; fee?: number; ownerId: string; phase: 'waiting' | 'bidding' | 'playing' | 'ended'; round: number
  mySeat: number; myHand: number[]; seats: SeatView[]; bottom: number[]; landlord: number; currentBid: number; turn: number; deadline?: string | null
  lastPlay?: { seat: number; cards: number[]; type: string } | null; multiplier: number; bombs: number; hasBots: boolean
  result?: { landlordWin: boolean; spring: boolean; multiplier: number; summary: string; changes: { seat: number; change: number }[] } | null
}
interface LobbyView { tiers: { key: string; name: string; base: number; minBalance: number; fee?: number }[]; rooms: { id: string; tier: string; tierName: string; base: number; phase: string; players: { name: string; isBot: boolean; away?: boolean }[]; open: number }[] }

// ---------- 单张牌 ----------
const SUITS = ['♠', '♥', '♣', '♦']
const RANKS: Record<number, string> = { 11: 'J', 12: 'Q', 13: 'K', 14: 'A', 15: '2' }
const Card = defineComponent({
  props: { id: { type: Number, required: true }, small: Boolean },
  setup(props) {
    return () => {
      const r = rankOf(props.id), s = suitOf(props.id)
      if (r >= 16) return h('span', { class: ['card', 'joker', r === 17 ? 'big' : 'small-joker', { sm: props.small }], 'aria-label': r === 17 ? '大王' : '小王' }, [h('i', 'JOKER'), h('b', r === 17 ? '大王' : '小王')])
      const red = s === 1 || s === 3
      const label = RANKS[r] ?? String(r)
      return h('span', { class: ['card', { red, sm: props.small }], 'aria-label': `${SUITS[s]}${label}` }, [h('b', label), h('i', SUITS[s])])
    }
  },
})

// ---------- 连接 ----------
const user = useUserStore()
const state = ref<TableState | null>(null)
const lobby = ref<LobbyView>({ tiers: [], rooms: [] })
const connState = ref<'wait' | 'on' | 'off'>('wait')
const error = ref('')
let connection: signalR.HubConnection | null = null
let lobbyTimer = 0, clockTimer = 0, errorTimer = 0
const now = ref(Date.now())
const ready = computed(() => user.isLoggedIn && connState.value === 'on')

function showError(text: string) {
  error.value = text
  window.clearTimeout(errorTimer)
  errorTimer = window.setTimeout(() => { error.value = '' }, 3500)
}

async function act(method: string, ...args: unknown[]) {
  if (!connection) return
  try {
    const err = await connection.invoke<string | null>(method, ...args)
    if (err) showError(err)
    else if (method === 'Leave') { state.value = null; void loadLobby() }
  } catch { showError('网络不稳定，操作没有送达') }
}

async function loadLobby() {
  try {
    // 连上了走实时连接；没登录或还没连上，就用只读接口看看开着哪些桌
    lobby.value = connection && connState.value === 'on'
      ? await connection.invoke<LobbyView>('Lobby')
      : await request.get<LobbyView>('/game-lobby/ddz', { silentError: true })
  } catch { /* 忽略 */ }
}

async function connect() {
  if (!user.isLoggedIn) { connState.value = 'off'; return }
  const hubUrl = new URL('/signalr/ddz', new URL(import.meta.env.VITE_API_BASE_URL || '/api', window.location.origin)).href
  connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, { accessTokenFactory: () => user.token || '' })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
    .build()
  connection.on('State', (s: TableState | null) => { state.value = s; if (!s) void loadLobby() })
  connection.onreconnecting(() => { connState.value = 'wait' })
  connection.onreconnected(async () => { connState.value = 'on'; state.value = await connection!.invoke<TableState | null>('Resume'); void loadLobby() })
  connection.onclose(() => { connState.value = 'off' })
  try {
    await connection.start()
    connState.value = 'on'
    state.value = await connection.invoke<TableState | null>('Resume')
    await loadLobby()
  } catch { connState.value = 'off' }
}

// ---------- 牌桌视角 ----------
const mySeat = computed(() => state.value ? state.value.seats[state.value.mySeat] : undefined)
const isOwner = computed(() => !!state.value && state.value.ownerId === user.userId)
const seatAt = (pos: 'left' | 'right') => {
  const s = state.value
  if (!s || s.mySeat < 0) return null
  return s.seats[(s.mySeat + (pos === 'right' ? 1 : 2)) % 3]
}
const emptySeats = computed(() => state.value?.seats.filter(s => s.empty).length ?? 0)
const myTurn = computed(() => !!state.value && state.value.turn === state.value.mySeat)
const secondsLeft = computed(() => {
  const d = state.value?.deadline
  return d ? Math.max(0, Math.ceil((new Date(d).getTime() - now.value) / 1000)) : 0
})
const mustLead = computed(() => !state.value?.lastPlay || state.value.lastPlay.seat === state.value.mySeat)
const target = computed<Pattern | null>(() => (mustLead.value || !state.value?.lastPlay) ? null : analyze(state.value.lastPlay.cards))
const changeOf = (seat: number) => state.value?.result?.changes.find(c => c.seat === seat)?.change ?? 0
const myWin = computed(() => !!state.value?.result && ((state.value.landlord === state.value.mySeat) === state.value.result.landlordWin))
const OPPONENTS = ['left', 'right'] as const

// ---------- 选牌 ----------
const selected = ref(new Set<number>())
const selectedPattern = computed(() => analyze([...selected.value]))
const canPlay = computed(() => {
  const p = selectedPattern.value
  if (!p) return false
  return !target.value || beats(p, target.value)
})
function toggle(c: number) {
  const next = new Set(selected.value)
  if (next.has(c)) { next.delete(c); sfx.unselect() } else { next.add(c); sfx.select() }
  selected.value = next
}
const toggleSfx = () => { sfxOn.value = !sfxOn.value; if (sfxOn.value) sfx.select() }
const toggleVoice = () => { voiceOn.value = !voiceOn.value; if (voiceOn.value) speak('语音报牌已打开') }
const hintOptions = computed(() => state.value && target.value ? findBeats(state.value.myHand, target.value) : [])
let hintIndex = -1
function hint() {
  if (!state.value) return
  if (mustLead.value) { selected.value = new Set(chooseLead(state.value.myHand)); sfx.select(); return }
  if (!hintOptions.value.length) { void act('Pass'); return }
  hintIndex = (hintIndex + 1) % hintOptions.value.length
  selected.value = new Set(hintOptions.value[hintIndex])
  sfx.select()
}
async function playSelected() {
  if (!canPlay.value) return
  await act('Play', [...selected.value])
}
// 轮次变化、手牌变化时清空选择
watch(() => [state.value?.turn, state.value?.myHand.length, state.value?.phase], () => { selected.value = new Set(); hintIndex = -1 })
// 一局结束：刷新土豆余额
watch(() => state.value?.result?.summary, s => { if (s) window.setTimeout(notifyPotatoChanged, 800) })

// ---------- 音效与语音报牌 ----------
const recentSig = (s: SeatView) => (s.recent ? JSON.stringify([s.recent.cards, s.recent.bid ?? null]) : '')
let lastSigs: string[] | null = null
let lastRoom = ''
watch(() => state.value ? `${state.value.roomId}#${state.value.round}#${state.value.seats.map(recentSig).join('|')}` : '', () => {
  const s = state.value
  if (!s) { lastSigs = null; return }
  const sigs = s.seats.map(recentSig)
  // 刚进桌 / 断线重连拿到的是已有局面，不补放音效
  if (!lastSigs || lastRoom !== s.roomId) { lastSigs = sigs; lastRoom = s.roomId; return }
  sigs.forEach((sig, i) => {
    if (!sig || sig === lastSigs![i]) return
    const r = s.seats[i]!.recent!
    if (r.bid !== null && r.bid !== undefined) {
      sfx.bid(r.bid)
      speak(r.bid ? `${r.bid}分` : '不叫')
    } else if (!r.cards.length) {
      sfx.pass()
      speak(Math.random() < 0.5 ? '不要' : '要不起')
    } else {
      const type = r.type ?? analyze(r.cards)?.type ?? null
      if (type === 'rocket') sfx.rocket()
      else if (type === 'bomb') sfx.bomb()
      else if (type?.startsWith('plane')) { sfx.play(r.cards.length); sfx.plane() }
      else sfx.play(r.cards.length)
      announce(type, r.cards.map(rankOf), r.label)
    }
  })
  lastSigs = sigs
})
// 发牌、轮到自己、倒计时、报警、结算
watch(() => state.value?.phase, (p, old) => {
  if (p === 'bidding' && old === 'waiting') sfx.deal()
  if (p === 'ended' && old === 'playing' && state.value?.result) {
    if (myWin.value) sfx.win(); else sfx.lose()
    if (state.value.result.spring) window.setTimeout(() => { sfx.spring(); speak('春天') }, 700)
  }
})
watch(() => myTurn.value && (state.value?.phase === 'bidding' || state.value?.phase === 'playing'), (mine, was) => { if (mine && !was) sfx.turn() })
watch(secondsLeft, (sec, prev) => { if (myTurn.value && sec !== prev && sec > 0 && sec <= 5 && state.value?.phase !== 'ended' && state.value?.phase !== 'waiting') sfx.tick() })
watch(() => state.value?.phase === 'playing' ? state.value.seats.map(x => x.cardCount).join(',') : '', (now, before) => {
  if (!now || !before) return
  const a = now.split(',').map(Number), b = before.split(',').map(Number)
  a.forEach((n, i) => {
    if (n > 0 && n <= 2 && (b[i] ?? 99) > 2 && i !== state.value?.mySeat) {
      sfx.warn()
      speak(`${state.value?.seats[i]?.name ?? ''}只剩${n === 1 ? '一' : '两'}张牌了`)
    }
  })
})

onMounted(async () => {
  await connect()
  if (connState.value !== 'on') void loadLobby()
  lobbyTimer = window.setInterval(() => { if (!state.value && !document.hidden) void loadLobby() }, 5000)
  clockTimer = window.setInterval(() => { now.value = Date.now() }, 500)
})
onBeforeUnmount(() => {
  window.clearInterval(lobbyTimer); window.clearInterval(clockTimer); window.clearTimeout(errorTimer)
  void connection?.stop()
})
</script>

<style scoped>
/* ===== 斗地主 · 莱茵生命实验台风格：浅色台面 + 坐标网格 + 定位标记 + 等宽标签 ===== */
.ddz { color: var(--text-primary); --mint: var(--signal-mint, #2dd4bf); --amber: #f59e0b; }
.muted { color: var(--text-secondary); font-size: 13px; }
.eyebrow { margin: 0 0 6px; font: 10px/1.2 var(--font-mono, monospace); letter-spacing: .16em; color: var(--accent-color); }
.btn-primary { padding: 9px 18px; border: 1px solid var(--accent-color); background: var(--accent-color); color: var(--on-accent, #fff); font: inherit; font-size: 14px; font-weight: 600; cursor: pointer; }
.btn-primary:hover:not(:disabled) { filter: brightness(1.08); }
.btn-primary:disabled { opacity: .45; cursor: default; }
.btn-primary.big { padding: 11px 36px; font-size: 16px; letter-spacing: .08em; }
.btn-primary small { margin-left: 2px; font-size: 11px; font-weight: 400; opacity: .85; }
.btn-line { padding: 8px 14px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 14px; cursor: pointer; }
.btn-line:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); }
.btn-line:disabled { opacity: .45; cursor: default; }
.btn-line.sm { padding: 5px 10px; font-size: 12px; }
.link { border: 0; background: none; color: var(--accent-color); font: inherit; font-size: 13px; cursor: pointer; }

/* ---------- 大厅 ---------- */
.ddz-lobby { display: grid; gap: 20px; }
.lobby-head { position: relative; display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-end; gap: 14px; padding-bottom: 14px; border-bottom: 1px solid var(--border-line); }
.lobby-head::after { content: ''; position: absolute; left: 0; bottom: -1px; width: 64px; height: 3px; background: var(--accent-color); }
.lobby-head h2 { margin: 0 0 6px; font-size: 26px; letter-spacing: .04em; }
.lobby-head p:not(.eyebrow) { margin: 0; max-width: 620px; color: var(--text-secondary); font-size: 13px; line-height: 1.7; }
.ddz-notice { margin: 0; padding: 10px 14px; border-left: 3px solid var(--accent-color); background: color-mix(in srgb, var(--accent-color) 7%, var(--paper-card)); font-size: 13px; }
.tier-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 14px; }
.tier-card { position: relative; display: grid; gap: 10px; padding: 18px; border: 1px solid var(--border-line); background: var(--paper-card); }
.tier-card::before { content: ''; position: absolute; left: -1px; top: -1px; bottom: -1px; width: 4px; background: var(--mint); }
.tier-card.tier-normal::before { background: var(--amber); }
.tier-card.tier-expert::before { background: var(--danger, #dc2626); }
.tier-card::after { content: 'TIER'; position: absolute; top: 10px; right: 12px; font: 9px var(--font-mono, monospace); letter-spacing: .2em; color: var(--text-secondary); opacity: .7; }
.tier-top { display: flex; align-items: baseline; gap: 12px; }
.tier-top b { font-size: 20px; letter-spacing: .04em; }
.tier-top span { font: 600 12px var(--font-mono, monospace); color: var(--accent-color); }
.tier-top span.fee { color: var(--text-secondary); }
.fee-note { font-size: 11px; color: var(--text-secondary); }
.tier-card p { margin: 0; color: var(--text-secondary); font-size: 13px; }
.tier-actions { display: flex; gap: 8px; }
.room-list { position: relative; padding: 16px 18px; border: 1px solid var(--border-line); background: var(--paper-card); }
.room-list::before { content: ''; position: absolute; top: -1px; right: -1px; width: 28px; height: 6px; background: var(--hazard-stripe, repeating-linear-gradient(135deg, #f59e0b 0 4px, #111 4px 8px)); }
.room-list-head { display: flex; justify-content: space-between; align-items: center; }
.room-list h3 { margin: 0; font-size: 15px; }
.room-list ul { list-style: none; margin: 12px 0 0; padding: 0; display: grid; gap: 6px; }
.room-list li { display: grid; grid-template-columns: 80px minmax(0, 1fr) 80px auto; align-items: center; gap: 10px; padding: 9px 12px; border: 1px solid var(--border-line); border-left: 3px solid var(--accent-color); background: var(--bg-main); font-size: 13px; }
.room-tier { font-weight: 700; }
.room-players { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: var(--text-secondary); }
.room-state { font: 11px var(--font-mono, monospace); color: var(--text-secondary); }
.rules-note { margin: 0; padding-top: 12px; border-top: 1px dashed var(--border-line); color: var(--text-secondary); font-size: 12px; line-height: 1.6; }

/* ---------- 牌桌顶栏 ---------- */
.table-bar { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 14px; margin-bottom: 12px; padding-bottom: 10px; border-bottom: 1px solid var(--border-line); font-size: 13px; }
.tb-code { font: 10px var(--font-mono, monospace); letter-spacing: .16em; color: var(--accent-color); }
.tb-tier { font-weight: 700; }
.tb-mult { padding: 2px 8px; border: 1px solid var(--amber); color: #b45309; font: 600 12px var(--font-mono, monospace); }
.tb-mult em { font-style: normal; font-weight: 400; }
.tb-bots { padding: 2px 8px; border: 1px dashed var(--border-line); color: var(--text-secondary); font-size: 12px; }
.tb-space { flex: 1; }
.tb-toggle { padding: 4px 10px; border: 1px solid var(--border-line); background: transparent; color: var(--text-secondary); font: 12px var(--font-mono, monospace); cursor: pointer; }
.tb-toggle.on { border-color: var(--accent-color); color: var(--accent-color); background: color-mix(in srgb, var(--accent-color) 8%, transparent); }

/* ---------- 实验台 ---------- */
.felt {
  position: relative; display: grid; grid-template-columns: 1fr minmax(0, 1.4fr) 1fr; grid-template-rows: auto 1fr auto; gap: 14px;
  min-height: 620px; padding: 26px 24px 20px; border: 1px solid var(--border-line); color: var(--text-primary);
  background-color: var(--paper-card);
  background-image:
    radial-gradient(ellipse at 50% 42%, color-mix(in srgb, var(--accent-color) 9%, transparent), transparent 62%),
    linear-gradient(var(--lab-grid-major, rgb(20 60 85 / 6%)) 1px, transparent 1px),
    linear-gradient(90deg, var(--lab-grid-major, rgb(20 60 85 / 6%)) 1px, transparent 1px),
    linear-gradient(var(--lab-grid-minor, rgb(20 60 85 / 3%)) 1px, transparent 1px),
    linear-gradient(90deg, var(--lab-grid-minor, rgb(20 60 85 / 3%)) 1px, transparent 1px);
  background-size: auto, 40px 40px, 40px 40px, 8px 8px, 8px 8px;
}
.reg { position: absolute; width: 22px; height: 22px; border: 0 solid var(--accent-color); pointer-events: none; }
.reg.tl { top: 8px; left: 8px; border-top-width: 2px; border-left-width: 2px; }
.reg.tr { top: 8px; right: 8px; border-top-width: 2px; border-right-width: 2px; }
.reg.bl { bottom: 8px; left: 8px; border-bottom-width: 2px; border-left-width: 2px; }
.reg.br { bottom: 8px; right: 8px; border-bottom-width: 2px; border-right-width: 2px; }

/* 对手座位 */
.opponent { display: grid; gap: 12px; align-content: start; }
.opponent.left { grid-column: 1; grid-row: 1 / 3; justify-items: start; }
.opponent.right { grid-column: 3; grid-row: 1 / 3; justify-items: end; }
.op-card { position: relative; display: flex; align-items: center; gap: 10px; min-width: 210px; padding: 10px 12px; border: 1px solid var(--border-line); background: var(--bg-main); }
.op-card.turn { border-color: var(--accent-color); box-shadow: inset 3px 0 0 var(--mint), 0 0 0 3px color-mix(in srgb, var(--accent-color) 18%, transparent); }
.op-card.landlord::before { content: ''; position: absolute; left: -1px; right: -1px; top: -1px; height: 4px; background: repeating-linear-gradient(135deg, var(--amber) 0 5px, #1c1206 5px 10px); }
.avatar { display: grid; place-items: center; width: 40px; height: 40px; border: 1px solid var(--accent-color); background: color-mix(in srgb, var(--accent-color) 10%, var(--paper-card)); color: var(--accent-color); font-weight: 700; }
.avatar.bot { border-style: dashed; }
.op-info { display: grid; gap: 3px; min-width: 0; }
.op-info b { font-size: 14px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 120px; }
.op-info .ddz-title { justify-self: start; margin-left: 0; }
.op-info small { color: var(--text-secondary); font-size: 11px; }
.role-l, .role-f { margin-right: 4px; padding: 0 6px; font-style: normal; font-size: 11px; font-weight: 600; }
.role-l { background: var(--amber); color: #1c1206; }
.role-f { border: 1px solid var(--accent-color); color: var(--accent-color); }
.card-count { margin-left: auto; display: grid; place-items: center; width: 34px; height: 46px; border: 2px solid #fff; border-radius: 3px; outline: 1px solid var(--border-line); color: #fff; font: 700 15px var(--font-mono, monospace);
  background: repeating-linear-gradient(135deg, color-mix(in srgb, var(--accent-color) 85%, #000) 0 4px, var(--accent-color) 4px 8px); }
.card-count.warn { background: repeating-linear-gradient(135deg, #b91c1c 0 4px, #dc2626 4px 8px); animation: warn 0.9s infinite alternate; }
@keyframes warn { to { box-shadow: 0 0 0 4px rgb(220 38 38 / 35%); } }
.clock { position: absolute; top: -11px; right: -11px; display: grid; place-items: center; min-width: 30px; height: 24px; padding: 0 6px; background: var(--accent-color); color: var(--on-accent, #fff); font: 700 13px var(--font-mono, monospace); }
.clock.inline { position: static; }
.kick { position: absolute; top: 2px; right: 2px; width: 20px; height: 20px; padding: 0; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); cursor: pointer; }
.played { display: flex; flex-wrap: wrap; align-items: center; gap: 2px; min-height: 64px; }
.played.right { justify-content: flex-end; }
.played.mine { justify-content: center; min-height: 76px; }
.say { padding: 5px 14px; border: 1px solid var(--border-line); background: var(--paper-card); font-size: 14px; letter-spacing: .06em; }
.type-label { margin-left: 8px; padding: 1px 6px; border: 1px solid var(--accent-color); color: var(--accent-color); font: 600 11px var(--font-mono, monospace); letter-spacing: .06em; }

/* 中间：底牌与提示 */
.center { grid-column: 2; grid-row: 1 / 3; display: grid; justify-items: center; align-content: start; gap: 14px; padding-top: 4px; }
.bottom-cards { position: relative; display: flex; gap: 4px; min-height: 64px; padding: 16px 14px 8px; border: 1px dashed var(--border-line); background: color-mix(in srgb, var(--paper-card) 80%, transparent); }
.bottom-cards::before { content: 'RESERVE / 底牌'; position: absolute; top: -8px; left: 10px; padding: 0 6px; background: var(--paper-card); font: 9px var(--font-mono, monospace); letter-spacing: .16em; color: var(--text-secondary); }
.center-tip { margin: 0; display: flex; align-items: center; gap: 10px; color: var(--text-secondary); font-size: 14px; }
.result { position: relative; display: grid; gap: 8px; min-width: 280px; padding: 18px 22px 16px; border: 1px solid var(--border-line); background: var(--paper-card); text-align: center; box-shadow: 0 12px 30px rgb(0 0 0 / 12%); }
.result::before { content: ''; position: absolute; left: -1px; right: -1px; top: -1px; height: 4px; background: var(--mint); }
.result.lose::before { background: var(--danger, #dc2626); }
.result > b { font-size: 28px; letter-spacing: .1em; }
.result.win > b { color: var(--accent-color); }
.result.lose > b { color: var(--text-secondary); }
.result > span { font-size: 13px; color: var(--text-secondary); }
.result ul { list-style: none; margin: 6px 0 0; padding: 8px 0 0; border-top: 1px dashed var(--border-line); display: grid; gap: 5px; text-align: left; }
.result li { display: flex; justify-content: space-between; gap: 16px; font-size: 13px; }
.result li em { margin-left: 6px; font-style: normal; color: var(--text-secondary); font-size: 11px; }
.result li b { font-family: var(--font-mono, monospace); }
.result .up { color: var(--success, #16a34a); } .result .down { color: var(--danger, #dc2626); }
.muted-b { color: var(--text-secondary); }

/* 自己 */
.me { grid-column: 1 / 4; grid-row: 3; display: grid; gap: 10px; justify-items: center; }
.actions { display: flex; flex-wrap: wrap; justify-content: center; align-items: center; gap: 8px; min-height: 42px; }
.trustee { margin-left: 12px; }
.trustee.on { border-color: var(--amber) !important; background: var(--amber) !important; color: #1c1206 !important; }
.ddz-error { margin: 0; padding: 4px 12px; border-left: 3px solid var(--danger, #dc2626); background: color-mix(in srgb, var(--danger, #dc2626) 10%, var(--paper-card)); color: var(--danger, #dc2626); font-size: 13px; }
.hand { display: flex; justify-content: center; padding: 20px 10px 2px; max-width: 100%; overflow-x: auto; }
.hand.landlord { padding-top: 22px; }
.hand-card { margin-left: -32px; padding: 0; border: 0; background: none; cursor: pointer; transition: transform .1s ease-out; }
.hand-card:first-child { margin-left: 0; }
.hand-card:hover { transform: translateY(-6px); }
.hand-card.up { transform: translateY(-20px); }
.hand-card.up :deep(.card) { box-shadow: 0 0 0 2px var(--accent-color), 0 6px 14px rgb(0 0 0 / 18%); }
.hand-card:focus-visible { outline: none; }
.hand-card:focus-visible :deep(.card) { box-shadow: 0 0 0 3px var(--mint); }
.me-info { display: flex; gap: 10px; align-items: center; font-size: 13px; color: var(--text-secondary); }
.me-info b { color: var(--text-primary); }

/* ---------- 牌面：干净的实验卡片 ---------- */
:deep(.card) {
  position: relative; display: inline-flex; flex-direction: column; align-items: flex-start; gap: 2px; width: 60px; height: 86px; padding: 7px 6px 5px;
  border-radius: 4px; background: #fdfefe; color: #1b2128; border: 1px solid #c9d3d9; box-shadow: 0 1px 3px rgb(0 0 0 / 16%); user-select: none; overflow: hidden;
}
:deep(.card)::before { content: ''; position: absolute; left: 0; right: 0; top: 0; height: 3px; background: #1b2128; }
:deep(.card.red)::before { background: #d23b3b; }
:deep(.card)::after { content: ''; position: absolute; right: 5px; bottom: 5px; width: 14px; height: 14px; border-right: 1.5px solid currentColor; border-bottom: 1.5px solid currentColor; opacity: .25; }
:deep(.card b) { font: 800 21px/1 'Bahnschrift', 'DIN Alternate', 'Roboto Condensed', 'Arial Narrow', sans-serif; letter-spacing: -.02em; }
:deep(.card i) { font-style: normal; font-size: 16px; line-height: 1; }
:deep(.card.red) { color: #d23b3b; }
:deep(.card.sm) { width: 40px; height: 56px; padding: 5px 4px 3px; }
:deep(.card.sm b) { font-size: 16px; }
:deep(.card.sm i) { font-size: 12px; }
:deep(.card.sm)::after { width: 9px; height: 9px; right: 3px; bottom: 3px; }
:deep(.card.joker) { justify-content: space-between; background: linear-gradient(160deg, #fdfefe 60%, color-mix(in srgb, var(--accent-color) 12%, #fdfefe)); }
:deep(.card.joker)::before { background: repeating-linear-gradient(90deg, currentColor 0 4px, transparent 4px 6px); }
:deep(.card.joker i) { font: 700 8px/1 var(--font-mono, monospace); letter-spacing: .12em; writing-mode: vertical-rl; }
:deep(.card.joker b) { align-self: flex-end; font-size: 13px; writing-mode: vertical-rl; letter-spacing: .1em; }
:deep(.card.joker.big) { color: #d23b3b; }
:deep(.card.joker.small-joker) { color: #1b2128; }
:deep(.card.sm.joker b) { font-size: 10px; writing-mode: horizontal-tb; letter-spacing: 0; white-space: nowrap; }
.card-back { display: inline-block; width: 60px; height: 86px; border-radius: 4px; border: 2px solid #fff; outline: 1px solid var(--border-line);
  background: repeating-linear-gradient(135deg, color-mix(in srgb, var(--accent-color) 85%, #000) 0 5px, var(--accent-color) 5px 10px); }
.card-back.sm { width: 40px; height: 56px; }

@media (max-width: 900px) {
  .tier-grid { grid-template-columns: minmax(0, 1fr); }
  .felt { grid-template-columns: 1fr 1fr; grid-template-rows: auto auto 1fr auto; min-height: 0; padding: 18px 12px 12px; }
  .opponent.left { grid-column: 1; grid-row: 1; } .opponent.right { grid-column: 2; grid-row: 1; }
  .op-card { min-width: 0; }
  .center { grid-column: 1 / 3; grid-row: 2; }
  .me { grid-column: 1 / 3; grid-row: 4; }
  .hand { justify-content: flex-start; }
  .hand-card { margin-left: -38px; }
  .room-list li { grid-template-columns: 1fr auto; }
  .room-players, .room-state { grid-column: 1; }
}
</style>
