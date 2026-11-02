<!-- 太虚游 · 转盘：大厅（官方抽奖转盘置顶 + 玩家公开转盘）、我的转盘、做转盘、管理（超级管理员）。
     打开某个转盘时地址会带上 ?wheel=编号（仅自己可见的还有 &k=分享码），可以直接把地址发给别人。 -->
<template>
  <div class="wheel-hub">
    <header class="wh-head">
      <div>
        <p class="eyebrow">LUCKY WHEEL · 转盘</p>
        <h2>转盘</h2>
        <p class="sub">帮你做决定、抽人、抽题，也有每天免费转一次的官方抽奖。</p>
      </div>
      <nav class="mode-switch" aria-label="转盘页面">
        <button type="button" :class="{ on: tab === 'lobby' }" :aria-pressed="tab === 'lobby'" @click="go('lobby')">转盘大厅</button>
        <button type="button" :class="{ on: tab === 'mine' }" :aria-pressed="tab === 'mine'" @click="go('mine')">我的转盘</button>
        <button type="button" :class="{ on: tab === 'create' }" :aria-pressed="tab === 'create'" @click="go('create')">+ 做一个</button>
        <button v-if="isSuper" type="button" :class="{ on: tab === 'admin' }" :aria-pressed="tab === 'admin'" @click="go('admin')">管理</button>
      </nav>
    </header>

    <!-- 转盘页 -->
    <WheelPlay v-if="openId" :id="openId" :share-key="shareKey" @back="closeWheel" @edit="startEdit" @changed="reloadLists" />

    <!-- 编辑器 -->
    <template v-else-if="tab === 'create' || tab === 'edit'">
      <div v-if="!user.isLoggedIn" class="need-login">
        <p>登录后就能做自己的转盘。</p>
        <button type="button" class="btn primary" @click="goLogin">去登录</button>
      </div>
      <WheelEditor v-else :key="editing?.id ?? 'new'" :initial="tab === 'edit' ? editing : null" @saved="onSaved" @cancel="go(editing ? 'mine' : 'lobby')" @deleted="onDeleted" />
    </template>

    <!-- 管理 -->
    <WheelAdmin v-else-if="tab === 'admin' && isSuper" @open="id => openWheel(id)" />

    <!-- 我的转盘 -->
    <section v-else-if="tab === 'mine'" class="mine">
      <div v-if="!user.isLoggedIn" class="need-login">
        <p>登录后可以看到自己做的转盘。</p>
        <button type="button" class="btn primary" @click="goLogin">去登录</button>
      </div>
      <template v-else>
        <div class="mine-head">
          <span>已经做了 <b>{{ mine.length }}</b> / {{ mineMax }} 个</span>
          <button type="button" class="btn primary small" :disabled="mine.length >= mineMax" @click="go('create')">+ 做一个</button>
        </div>
        <p v-if="mineError" class="err">{{ mineError }}</p>
        <div v-else-if="mineLoading" class="state">读取中…</div>
        <div v-else-if="!mine.length" class="empty">
          <span class="empty-wheel" aria-hidden="true"></span>
          <p>你还没有做过转盘。做一个「今天吃什么」试试？</p>
          <button type="button" class="btn primary" @click="go('create')">做一个转盘</button>
        </div>
        <ul v-else class="mine-list">
          <li v-for="m in mine" :key="m.card.id" :class="{ hidden: m.card.status === 'hidden' }">
            <button type="button" class="thumb-btn" :aria-label="`打开 ${m.card.title}`" @click="openWheel(m.card.id, m.shareCode)">
              <span class="thumb" :style="{ background: conicOf(m.card.colors) }"></span>
            </button>
            <div class="mine-main">
              <b>{{ m.card.title }}</b>
              <small>
                <em :class="m.card.visibility">{{ m.card.visibility === 'private' ? '仅自己' : '公开' }}</em>
                <em v-if="m.card.status === 'hidden'" class="hidden-tag">被下架{{ m.hiddenReason ? `：${m.hiddenReason}` : '' }}</em>
                {{ m.card.segmentCount }} 个扇区 · 转了 {{ m.card.spinCount.toLocaleString() }} 次
              </small>
            </div>
            <span class="mine-ops">
              <button type="button" class="btn small" @click="openWheel(m.card.id, m.shareCode)">打开</button>
              <button type="button" class="btn small" @click="startEditById(m.card.id)">编辑</button>
              <button v-if="m.card.status === 'active'" type="button" class="btn small" @click="copyLink(m.card.id, m.card.visibility === 'private' ? m.shareCode : null)">复制链接</button>
            </span>
          </li>
        </ul>
        <p class="fine">做转盘时付的经验 / 土豆，删除转盘后不退还。删除在「编辑」里面。</p>
      </template>
    </section>

    <!-- 大厅 -->
    <section v-else class="lobby">
      <div v-if="feed.length" class="ticker" aria-label="最近中奖">
        <span class="ticker-tag">🎉 最近中奖</span>
        <div class="ticker-window">
          <div class="ticker-track" :style="{ animationDuration: `${Math.max(16, feed.length * 4)}s` }">
            <span v-for="(f, i) in [...feed, ...feed]" :key="i" class="ticker-item"><b>{{ f.name }}</b> 抽中 {{ f.prize }}</span>
          </div>
        </div>
      </div>

      <div v-if="official.length" class="official">
        <h3 class="block-title"><span class="idx">01</span>官方抽奖 <small>每个转盘每天免费转 1 次</small></h3>
        <div class="official-grid">
          <button v-for="w in official" :key="w.id" type="button" class="off-card" @click="openWheel(w.id)">
            <span class="thumb big" :style="{ background: conicOf(w.colors) }" aria-hidden="true"></span>
            <span class="off-body">
              <b>{{ w.title }}</b>
              <span class="off-intro">{{ w.intro || '每天来转一转，土豆和经验等你拿' }}</span>
              <span class="off-state" :class="{ done: !!w.today }">
                <template v-if="w.today">今天已转：{{ w.today.prizeLabel || w.today.text }}</template>
                <template v-else-if="user.isLoggedIn">今天还能免费转 1 次 →</template>
                <template v-else>登录后每天免费转 1 次 →</template>
              </span>
            </span>
          </button>
        </div>
      </div>

      <div class="public">
        <div class="public-head">
          <h3 class="block-title"><span class="idx">{{ official.length ? '02' : '01' }}</span>玩家的转盘</h3>
          <div class="public-tools">
            <form class="search" role="search" @submit.prevent="search">
              <input v-model="q" type="search" maxlength="40" placeholder="搜转盘名字" aria-label="搜转盘名字" />
              <button type="submit" class="btn small">搜</button>
            </form>
            <div class="sort" role="group" aria-label="排序">
              <button type="button" :class="{ on: sort === 'hot' }" :aria-pressed="sort === 'hot'" @click="setSort('hot')">最热</button>
              <button type="button" :class="{ on: sort === 'new' }" :aria-pressed="sort === 'new'" @click="setSort('new')">最新</button>
            </div>
          </div>
        </div>
        <p v-if="listError" class="err">{{ listError }}</p>
        <div v-else-if="listLoading && !items.length" class="state">读取中…</div>
        <div v-else-if="!items.length" class="empty">
          <span class="empty-wheel" aria-hidden="true"></span>
          <p>{{ searched ? `没找到名字里有「${searched}」的转盘` : '还没有人做公开的转盘，来做第一个吧！' }}</p>
          <button type="button" class="btn primary" @click="go('create')">做一个转盘</button>
        </div>
        <div v-else class="card-grid">
          <button v-for="w in items" :key="w.id" type="button" class="w-card" @click="openWheel(w.id)">
            <span class="thumb" :style="{ background: conicOf(w.colors) }" aria-hidden="true"></span>
            <span class="w-body">
              <b>{{ w.title }}</b>
              <span v-if="w.intro" class="w-intro">{{ w.intro }}</span>
              <span class="w-meta">{{ w.owner?.name ?? '' }} · {{ w.segmentCount }} 项 · 转了 {{ w.spinCount.toLocaleString() }} 次</span>
            </span>
          </button>
        </div>
        <button v-if="hasMore" type="button" class="btn more" :disabled="listLoading" @click="loadMore">{{ listLoading ? '读取中…' : '加载更多' }}</button>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useUserStore } from '@/stores/user'
import { LOGIN_PATH } from '@/router/auth'
import { wheelApi, wheelError, type WheelCard, type WheelDetail, type WheelMineItem } from '@/api/wheel'
import WheelPlay from './WheelPlay.vue'
import WheelEditor from './WheelEditor.vue'
import WheelAdmin from './WheelAdmin.vue'
import { absoluteUrl, conicOf, wheelQuery } from './wheelShared'

type Tab = 'lobby' | 'mine' | 'create' | 'edit' | 'admin'

const user = useUserStore()
const route = useRoute()
const router = useRouter()
const isSuper = computed(() => user.permissions.includes('SuperAdmin'))

const tab = ref<Tab>('lobby')
/** 地址里带了 ?wheel= 就打开那个转盘 */
const openId = computed(() => {
  const n = Number(route.query.wheel)
  return Number.isInteger(n) && n > 0 ? n : 0
})
const shareKey = computed(() => (typeof route.query.k === 'string' ? route.query.k : ''))

function go(t: Tab) {
  tab.value = t
  if (openId.value) closeWheel()
  if (t === 'mine') void loadMine()
  if (t === 'lobby') void loadLobby(true)
}
function openWheel(id: number, code?: string | null) {
  void router.push({ query: { ...route.query, wheel: String(id), k: code || undefined } })
}
function closeWheel() {
  void router.push({ query: { ...route.query, wheel: undefined, k: undefined } })
}
function goLogin() {
  void router.push({ path: LOGIN_PATH, query: { redirect: route.fullPath } })
}

// ===== 大厅 =====
const official = ref<WheelCard[]>([])
const items = ref<WheelCard[]>([])
const hasMore = ref(false)
const page = ref(1)
const sort = ref<'hot' | 'new'>('hot')
const q = ref('')
const searched = ref('')
const listLoading = ref(false)
const listError = ref('')
const feed = ref<{ id: number; name: string; prize: string }[]>([])

async function loadLobby(reset: boolean) {
  if (reset) page.value = 1
  listLoading.value = true
  listError.value = ''
  try {
    const r = await wheelApi.list(sort.value, page.value, searched.value)
    if (page.value === 1) { official.value = r.official; items.value = r.items } else items.value = items.value.concat(r.items)
    hasMore.value = r.hasMore
  } catch (e) {
    listError.value = wheelError(e, '转盘列表没读出来，稍后再试')
  } finally {
    listLoading.value = false
  }
  if (reset) {
    try { feed.value = await wheelApi.feed() } catch { feed.value = [] }
  }
}
function loadMore() { page.value++; void loadLobby(false) }
function setSort(s: 'hot' | 'new') { if (sort.value !== s) { sort.value = s; void loadLobby(true) } }
function search() { searched.value = q.value.trim(); void loadLobby(true) }

// ===== 我的转盘 =====
const mine = ref<WheelMineItem[]>([])
const mineMax = ref(30)
const mineLoading = ref(false)
const mineError = ref('')
async function loadMine() {
  if (!user.isLoggedIn) return
  mineLoading.value = true
  mineError.value = ''
  try {
    const r = await wheelApi.mine()
    mine.value = r.items
    mineMax.value = r.max
  } catch (e) {
    mineError.value = wheelError(e, '没读出来，稍后再试')
  } finally {
    mineLoading.value = false
  }
}
function copyLink(id: number, code: string | null) {
  const url = absoluteUrl(router.resolve({ path: route.path, query: wheelQuery(id, code) }).href)
  navigator.clipboard.writeText(url)
    .then(() => ElMessage.success(code ? '链接已复制（带分享码，拿到的人都能转）' : '链接已复制'))
    .catch(() => window.prompt('复制这个链接', url))
}

// ===== 编辑 =====
const editing = ref<WheelDetail | null>(null)
function startEdit(w: WheelDetail) {
  editing.value = w
  tab.value = 'edit'
  if (openId.value) closeWheel()
}
async function startEditById(id: number) {
  try {
    const r = await wheelApi.get(id)
    startEdit(r.wheel)
  } catch (e) { ElMessage.error(wheelError(e, '打不开这个转盘')) }
}
function onSaved(id: number, shareCode: string | null) {
  editing.value = null
  tab.value = 'mine'
  void loadMine()
  // 保存完直接打开转盘看看效果
  openWheel(id, shareCode)
}
function onDeleted() {
  editing.value = null
  go('mine')
}
function reloadLists() {
  if (tab.value === 'mine') void loadMine()
  else if (tab.value === 'lobby') void loadLobby(true)
}

// 从转盘页回到列表时刷新一下（次数、今天转没转）
watch(openId, (now, before) => { if (!now && before) reloadLists() })
onMounted(() => { void loadLobby(true) })
</script>

<style scoped>
.wheel-hub { display: grid; gap: 18px; color: var(--text-primary); }
.wh-head { position: relative; display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-end; gap: 10px 16px; padding-bottom: 14px; border-bottom: 1px solid var(--border-line); }
.wh-head::after { content: ''; position: absolute; left: 0; bottom: -1px; width: 64px; height: 3px; background: var(--accent-color); }
.eyebrow { margin: 0 0 6px; font: 10px/1.2 var(--font-mono, monospace); letter-spacing: .16em; color: var(--accent-color); }
.wh-head h2 { margin: 0; font-size: 26px; letter-spacing: .04em; }
.sub { margin: 6px 0 0; color: var(--text-secondary); font-size: 13px; }
.mode-switch { display: inline-flex; flex-wrap: wrap; border: 1px solid var(--border-line); }
.mode-switch button { padding: 7px 14px; border: 0; border-right: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.mode-switch button:last-child { border-right: 0; }
.mode-switch button.on { background: var(--accent-color); color: var(--on-accent, #fff); font-weight: 600; }

.btn { padding: 8px 16px; border: 1px solid var(--border-line); border-radius: 0; background: transparent; color: var(--text-primary); font: inherit; font-size: 14px; cursor: pointer; }
.btn.small { padding: 4px 10px; font-size: 12px; }
.btn.primary { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent, #fff); font-weight: 600; }
.btn:disabled { opacity: .5; cursor: not-allowed; }
.err { margin: 0; color: var(--danger, #dc2626); font-size: 13px; }
.state { padding: 36px; text-align: center; color: var(--text-secondary); }
.fine { margin: 0; font-size: 12px; color: var(--text-secondary); }
.need-login, .empty { display: grid; justify-items: center; gap: 12px; padding: 40px 16px; border: 1px dashed var(--border-line); text-align: center; color: var(--text-secondary); }
.need-login p, .empty p { margin: 0; }
.empty-wheel { width: 72px; height: 72px; border-radius: 50%; border: 4px solid #20252b; background: conic-gradient(#E4572E 0 25%, #F3A712 0 50%, #669BBC 0 75%, #A8C686 0); opacity: .75; }

.thumb { flex: none; display: block; width: 64px; height: 64px; border-radius: 50%; border: 4px solid #20252b; box-shadow: 0 0 0 2px #fbbf24 inset, 0 4px 10px rgba(0, 0, 0, .18); }
.thumb.big { width: 96px; height: 96px; border-width: 5px; }

/* 中奖滚动条 */
.ticker { display: flex; align-items: center; gap: 10px; padding: 8px 12px; border: 1px solid var(--border-line); background: var(--paper-card); font-size: 13px; overflow: hidden; }
.ticker-tag { flex: none; font-weight: 600; color: #b45309; }
.ticker-window { flex: 1; overflow: hidden; mask-image: linear-gradient(90deg, transparent, #000 6%, #000 94%, transparent); }
.ticker-track { display: inline-flex; gap: 28px; white-space: nowrap; animation: ticker linear infinite; }
.ticker-item b { color: var(--accent-color); font-weight: 600; }
@keyframes ticker { from { transform: translateX(0); } to { transform: translateX(-50%); } }

.lobby { display: grid; gap: 22px; }
.block-title { display: flex; align-items: baseline; gap: 10px; margin: 0; font-size: 18px; }
.block-title small { color: var(--text-secondary); font-size: 12px; font-weight: 400; }
.idx { font: 600 12px var(--font-mono, monospace); color: var(--accent-color); letter-spacing: .1em; }
.official { display: grid; gap: 12px; }
.official-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 14px; }
.off-card { display: flex; align-items: center; gap: 16px; padding: 16px; border: 1px solid #d97706; background: linear-gradient(135deg, color-mix(in srgb, #f59e0b 14%, var(--paper-card)), var(--paper-card) 70%); color: var(--text-primary); font: inherit; text-align: left; cursor: pointer; transition: transform .15s, box-shadow .15s; }
.off-card:hover { transform: translateY(-2px); box-shadow: 0 10px 24px rgba(217, 119, 6, .18); }
.off-card:hover .thumb { transform: rotate(40deg); }
.off-card .thumb { transition: transform .6s cubic-bezier(.2, .8, .3, 1); }
.off-body { display: grid; gap: 6px; min-width: 0; }
.off-body b { font-size: 16px; }
.off-intro { color: var(--text-secondary); font-size: 13px; line-height: 1.6; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
.off-state { font-size: 13px; font-weight: 600; color: #b45309; }
.off-state.done { color: var(--text-secondary); font-weight: 400; }

.public { display: grid; gap: 12px; }
.public-head { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: 10px; }
.public-tools { display: flex; flex-wrap: wrap; gap: 8px; }
.search { display: flex; }
.search input { width: 160px; padding: 5px 8px; border: 1px solid var(--border-line); border-right: 0; border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; }
.sort { display: inline-flex; border: 1px solid var(--border-line); }
.sort button { padding: 5px 12px; border: 0; border-right: 1px solid var(--border-line); background: transparent; color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.sort button:last-child { border-right: 0; }
.sort button.on { background: var(--accent-color); color: var(--on-accent, #fff); }
.card-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(250px, 1fr)); gap: 12px; }
.w-card { display: flex; align-items: center; gap: 14px; padding: 14px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; text-align: left; cursor: pointer; transition: border-color .2s; }
.w-card:hover { border-color: var(--accent-color); }
.w-card:hover .thumb { transform: rotate(40deg); }
.w-card .thumb { transition: transform .6s cubic-bezier(.2, .8, .3, 1); }
.w-body { display: grid; gap: 4px; min-width: 0; }
.w-body b { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.w-intro { color: var(--text-secondary); font-size: 12px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.w-meta { color: var(--text-secondary); font-size: 12px; }
.more { justify-self: center; min-width: 160px; }

.mine { display: grid; gap: 12px; }
.mine-head { display: flex; justify-content: space-between; align-items: center; gap: 10px; font-size: 13px; color: var(--text-secondary); }
.mine-head b { color: var(--text-primary); }
.mine-list { display: grid; gap: 8px; margin: 0; padding: 0; list-style: none; }
.mine-list li { display: grid; grid-template-columns: auto minmax(0, 1fr) auto; gap: 14px; align-items: center; padding: 10px 14px; border: 1px solid var(--border-line); background: var(--paper-card); }
.mine-list li.hidden { border-color: color-mix(in srgb, var(--danger, #dc2626) 40%, var(--border-line)); }
.thumb-btn { padding: 0; border: 0; background: none; cursor: pointer; }
.thumb-btn .thumb { width: 52px; height: 52px; }
.mine-main { display: grid; gap: 4px; min-width: 0; }
.mine-main b { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.mine-main small { color: var(--text-secondary); font-size: 12px; }
.mine-main em { margin-right: 6px; padding: 0 6px; border: 1px solid var(--border-line); font-style: normal; }
.mine-main em.private { border-color: var(--accent-color); color: var(--accent-color); }
.mine-main em.hidden-tag { border-color: var(--danger, #dc2626); color: var(--danger, #dc2626); }
.mine-ops { display: flex; flex-wrap: wrap; gap: 4px; justify-content: flex-end; }

@media (max-width: 640px) {
  .mode-switch { width: 100%; }
  .mode-switch button { flex: 1; padding: 8px 6px; }
  .official-grid, .card-grid { grid-template-columns: minmax(0, 1fr); }
  .thumb.big { width: 76px; height: 76px; }
  .mine-list li { grid-template-columns: auto minmax(0, 1fr); }
  .mine-ops { grid-column: 1 / -1; justify-content: flex-start; }
  .search input { width: 120px; }
}
@media (prefers-reduced-motion: reduce) {
  .ticker-track { animation: none; flex-wrap: wrap; white-space: normal; }
  .off-card, .off-card .thumb, .w-card .thumb { transition: none; }
}
</style>
