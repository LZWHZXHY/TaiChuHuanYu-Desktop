<!-- 社区成员：所有注册成员，在线的排在最前面。可以搜索名字、只看在线、按加入时间 / 等级 / 名字排；点人打开主页。 -->
<template>
  <div class="members-page content-page community-surface">
    <header class="mb-header surface-heading">
      <div>
        <h1>社区成员</h1>
        <p class="sub">
          <template v-if="page">共 {{ page.total.toLocaleString('zh-CN') }} 位{{ keyword ? '符合条件的' : '' }}成员 · </template>
          <span class="mb-online-now"><i class="mb-dot" aria-hidden="true"></i>现在 <b>{{ online?.total ?? '—' }}</b> 人在线</span>
          <template v-if="online">（{{ online.members }} 位成员{{ online.hidden ? `、${online.hidden} 位隐身` : '' }}、{{ online.guests }} 位游客）</template>
        </p>
      </div>
      <div class="mb-head-actions">
        <label v-if="meInvisible !== null" class="mb-ghost" :class="{ on: meInvisible }" title="隐身后，别人在这里看不到你在线、看不到你最近来过和正在看的板块（在线人数里只算一个「隐身」）">
          <input type="checkbox" :checked="meInvisible" :disabled="savingGhost" @change="toggleInvisible(($event.target as HTMLInputElement).checked)" />
          👻 隐身
        </label>
        <button type="button" class="mb-btn" :disabled="loading" @click="reload()">{{ loading ? '刷新中…' : '刷新' }}</button>
      </div>
    </header>
    <p v-if="ghostMessage" class="mb-ghost-msg" role="status">{{ ghostMessage }}</p>

    <div class="mb-tools">
      <div class="mb-seg" role="tablist" aria-label="显示">
        <button type="button" role="tab" :aria-selected="!onlineOnly" @click="onlineOnly = false">全部成员</button>
        <button type="button" role="tab" :aria-selected="onlineOnly" @click="onlineOnly = true">只看在线<span v-if="online"> · {{ online.members }}</span></button>
      </div>
      <input v-model="keyword" type="search" class="mb-search" placeholder="搜索名字…" aria-label="搜索成员名字" maxlength="50" />
      <label v-if="!onlineOnly" class="mb-sort">
        <span>其他成员按</span>
        <select v-model="sort" aria-label="排序">
          <option value="joined">新加入的在前</option>
          <option value="old">老成员在前</option>
          <option value="level">等级高的在前</option>
          <option value="name">名字</option>
        </select>
      </label>
    </div>

    <p v-if="error" class="mb-empty" role="alert">成员列表暂时读不出来。<button type="button" class="mb-link" @click="reload()">重试</button></p>
    <p v-else-if="!items.length && loading" class="mb-empty">正在读取…</p>
    <p v-else-if="!items.length" class="mb-empty">{{ onlineOnly ? (keyword ? '没有符合条件的在线成员' : '现在没有成员在线（游客不显示在列表里）') : '没有找到这个名字的成员' }}</p>

    <template v-else>
      <section v-if="onlineItems.length" class="mb-section" aria-label="在线成员">
        <h2 class="mb-section-title"><i class="mb-dot" aria-hidden="true"></i>在线 <small>{{ online?.matched ?? onlineItems.length }}</small></h2>
        <ul class="mb-grid">
          <li v-for="m in onlineItems" :key="m.id"><MemberCard :member="m" /></li>
        </ul>
      </section>
      <section v-if="offlineItems.length" class="mb-section" aria-label="其他成员">
        <h2 class="mb-section-title">{{ onlineItems.length ? '其他成员' : '成员' }}</h2>
        <ul class="mb-grid">
          <li v-for="m in offlineItems" :key="m.id"><MemberCard :member="m" /></li>
        </ul>
      </section>
      <div class="mb-more">
        <button v-if="page?.hasMore" type="button" class="mb-btn" :disabled="loading" @click="loadMore">{{ loading ? '读取中…' : `加载更多（还有 ${Math.max(0, page.total - items.length)} 位）` }}</button>
        <small v-else>已经是全部了</small>
      </div>
    </template>
    <p class="mb-note">最近 5 分钟内在网站上有动静的算在线，后面写着正在逛哪个板块。游客只计人数，不显示在列表里；隐身的成员也只计人数。</p>
  </div>
</template>

<script setup lang="ts">
import { computed, defineComponent, h, onBeforeUnmount, onMounted, ref, watch, type PropType } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { membersApi, type MemberItem, type MemberPage, type MemberSort, type OnlineCount } from '@/api/members'
import AvatarFrame from '@/components/AvatarFrame.vue'
import UserTitle from '@/components/UserTitle.vue'
import { SECTION_LABELS } from '@/utils/presence'

const route = useRoute()
const router = useRouter()
const PAGE_SIZE = 48

// 筛选条件记在地址里，返回时还在
const keyword = ref(String(route.query.q || ''))
const onlineOnly = ref(route.query.online === '1')
const sort = ref<MemberSort>((['joined', 'old', 'level', 'name'] as const).find(s => s === route.query.sort) ?? 'joined')

const items = ref<MemberItem[]>([])
const page = ref<MemberPage | null>(null)
const online = ref<(OnlineCount & { matched?: number }) | null>(null)
const pageNo = ref(1)
const loading = ref(false)
const error = ref(false)
let version = 0

async function fetchPage(n: number) {
  const v = ++version
  loading.value = true
  error.value = false
  try {
    const res = await membersApi.list({ q: keyword.value.trim() || undefined, sort: sort.value, onlineOnly: onlineOnly.value || undefined, page: n, pageSize: PAGE_SIZE })
    if (v !== version) return
    page.value = res
    online.value = res.online
    meInvisible.value = res.meInvisible ?? null
    pageNo.value = n
    // 翻页时在线的人可能刚好下线 / 上线，按 id 去重
    const seen = new Set(n === 1 ? [] : items.value.map(i => i.id))
    items.value = [...(n === 1 ? [] : items.value), ...res.items.filter(i => !seen.has(i.id))]
  } catch {
    if (v === version) error.value = true
  } finally {
    if (v === version) loading.value = false
  }
}
const reload = () => fetchPage(1)

// ===== 隐身 =====
const meInvisible = ref<boolean | null>(null)
const savingGhost = ref(false)
const ghostMessage = ref('')
async function toggleInvisible(on: boolean) {
  savingGhost.value = true; ghostMessage.value = ''
  try {
    const res = await membersApi.setInvisible(on)
    meInvisible.value = res.invisible
    ghostMessage.value = res.message
    void reload()
  } catch (e: any) {
    ghostMessage.value = e?.response?.data?.message || '没设置成功，稍后再试'
  } finally { savingGhost.value = false }
}
const loadMore = () => fetchPage(pageNo.value + 1)

let searchTimer = 0
watch(keyword, () => { window.clearTimeout(searchTimer); searchTimer = window.setTimeout(reload, 300) })
watch([onlineOnly, sort], () => void reload())
watch([keyword, onlineOnly, sort], () => {
  const query = { ...route.query, q: keyword.value.trim() || undefined, online: onlineOnly.value ? '1' : undefined, sort: sort.value === 'joined' ? undefined : sort.value }
  void router.replace({ query }).catch(() => {})
})

// 在线状态一分钟变一次：页面开着时每分钟只刷新在线人数；切回来超过 1 分钟就整页刷新
let timer = 0
let hiddenAt = 0
async function refreshCount() {
  if (document.hidden) return
  try { const c = await membersApi.onlineCount(); online.value = { ...(online.value ?? {}), ...c } } catch { /* 忽略 */ }
}
function onVisible() {
  if (document.hidden) { hiddenAt = Date.now(); return }
  if (hiddenAt && Date.now() - hiddenAt > 60_000 && pageNo.value === 1) void reload()
}
onMounted(() => {
  void reload()
  timer = window.setInterval(refreshCount, 60_000)
  document.addEventListener('visibilitychange', onVisible)
})
onBeforeUnmount(() => { window.clearInterval(timer); window.clearTimeout(searchTimer); document.removeEventListener('visibilitychange', onVisible) })

const onlineItems = computed(() => items.value.filter(i => i.online))
const offlineItems = computed(() => items.value.filter(i => !i.online))

// ===== 时间：北京时间分天 =====
const DAY = 86_400_000
const bjDay = (t: number) => Math.floor((t + 8 * 3600_000) / DAY)
function seenText(m: MemberItem) {
  if (m.invisible) return m.online ? '隐身中（只有你看得到）' : '隐身中'
  if (m.online) return (m.section && SECTION_LABELS[m.section]) || '在线'
  if (!m.lastSeenAt) return ''
  const t = new Date(m.lastSeenAt).getTime()
  const now = Date.now()
  const mins = Math.floor((now - t) / 60_000)
  if (mins < 60) return `${Math.max(1, mins)} 分钟前来过`
  const days = bjDay(now) - bjDay(t)
  if (days <= 0) return `${Math.floor(mins / 60)} 小时前来过`
  if (days === 1) return '昨天来过'
  if (days < 30) return `${days} 天前来过`
  if (days < 365) return `${Math.floor(days / 30)} 个月前来过`
  return '一年多没来了'
}
const joinedText = (m: MemberItem) => {
  const d = new Date(m.joinedAt)
  return Number.isNaN(d.getTime()) ? '' : `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')} 加入`
}

// 一张成员卡片
const MemberCard = defineComponent({
  props: { member: { type: Object as PropType<MemberItem>, required: true } },
  setup(props) {
    return () => {
      const m = props.member
      const seen = seenText(m)
      return h(RouterLink, { to: { name: 'UserCenter', params: { id: m.id } }, class: ['mb-card', { 'is-online': m.online }], title: `查看 ${m.username} 的主页` }, () => [
        h('span', { class: 'mb-avatar-wrap' }, [
          h(AvatarFrame, { userId: m.id, size: 'sm' }, () => m.avatar
            ? h('img', { class: 'mb-avatar', src: m.avatar, alt: '', loading: 'lazy' })
            : h('span', { class: 'mb-avatar mb-avatar-empty', 'aria-hidden': 'true' }, (m.username || '?').slice(0, 1))),
          m.online ? h('i', { class: 'mb-avatar-dot', 'aria-label': '在线' }) : null,
        ]),
        h('span', { class: 'mb-info' }, [
          h('span', { class: 'mb-name-row' }, [h('b', { class: 'mb-name' }, m.username), h(UserTitle, { userId: m.id })]),
          h('span', { class: 'mb-meta' }, [
            h('span', { class: 'mb-level' }, `Lv.${m.level}`),
            h('span', { class: ['mb-seen', { on: m.online }] }, seen || joinedText(m)),
          ]),
        ]),
      ])
    }
  },
})
</script>

<style scoped>
.members-page { max-width: 1100px; margin: 0 auto; padding: 0 0 48px; }
.mb-header { display: flex; justify-content: space-between; align-items: flex-end; gap: 16px; flex-wrap: wrap; padding: 24px 0 18px; }
.mb-header h1 { margin: 0; font-size: clamp(1.7rem, 3.4vw, 2.3rem); }
.mb-header .sub { margin: 8px 0 0; color: var(--text-secondary); font-size: .9rem; }
.mb-online-now { display: inline-flex; align-items: center; gap: 6px; }
.mb-online-now b { color: var(--text-primary); font-variant-numeric: tabular-nums; }
.mb-dot { display: inline-block; width: 8px; height: 8px; border-radius: 50%; background: #22c55e; box-shadow: 0 0 0 3px color-mix(in srgb, #22c55e 22%, transparent); }

.mb-tools { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; margin-bottom: 18px; }
.mb-seg { display: flex; }
.mb-seg button { padding: 7px 14px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.mb-seg button + button { border-left: 0; }
.mb-seg button[aria-selected=true] { background: var(--accent-color); border-color: var(--accent-color); color: #fff; }
.mb-search { flex: 1 1 200px; max-width: 320px; padding: 7px 10px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; }
.mb-sort { display: inline-flex; align-items: center; gap: 6px; font-size: 13px; color: var(--text-secondary); }
.mb-sort select { padding: 6px 8px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; }
.mb-btn { padding: 7px 14px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.mb-btn:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); }
.mb-btn:disabled { opacity: .6; cursor: default; }
.mb-link { padding: 0; border: 0; background: none; color: var(--accent-color); font: inherit; cursor: pointer; text-decoration: underline; }

.mb-section + .mb-section { margin-top: 26px; }
.mb-section-title { display: flex; align-items: center; gap: 8px; margin: 0 0 12px; padding: 0 0 8px 12px; font-size: 13px; font-weight: 600; letter-spacing: 1px; color: var(--text-secondary); border-bottom: 1px solid var(--border-line); border-left: 3px solid var(--accent-color); }
.mb-section-title small { font-weight: 400; letter-spacing: 0; }
.mb-grid { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(230px, 1fr)); gap: 10px; }

.mb-grid :deep(.mb-card) { display: flex; align-items: center; gap: 12px; min-width: 0; padding: 12px; background: var(--paper-card); border: 1px solid var(--border-line); color: var(--text-primary); text-decoration: none; transition: border-color .15s, transform .15s; }
.mb-grid :deep(.mb-card:hover) { border-color: var(--accent-color); transform: translateY(-1px); }
.mb-grid :deep(.mb-card.is-online) { border-left: 3px solid #22c55e; }
.mb-grid :deep(.mb-avatar-wrap) { position: relative; flex-shrink: 0; display: inline-flex; }
.mb-grid :deep(.mb-avatar) { width: 44px; height: 44px; object-fit: cover; display: block; background: var(--bg-sub); }
.mb-grid :deep(.mb-avatar-empty) { display: flex; align-items: center; justify-content: center; font-weight: 700; color: var(--text-secondary); }
.mb-grid :deep(.mb-avatar-dot) { position: absolute; right: -3px; bottom: -3px; z-index: 3; width: 12px; height: 12px; border-radius: 50%; background: #22c55e; border: 2px solid var(--paper-card); }
.mb-grid :deep(.mb-info) { min-width: 0; display: flex; flex-direction: column; gap: 4px; }
.mb-grid :deep(.mb-name-row) { display: flex; align-items: center; min-width: 0; }
.mb-grid :deep(.mb-name) { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 14px; }
.mb-grid :deep(.mb-meta) { display: flex; align-items: center; gap: 8px; font-size: 12px; color: var(--text-secondary); white-space: nowrap; }
.mb-grid :deep(.mb-level) { padding: 0 5px; border: 1px solid var(--border-line); font-family: var(--font-mono, monospace); font-size: 11px; }
.mb-grid :deep(.mb-seen.on) { color: #16a34a; font-weight: 600; }

.mb-head-actions { display: flex; align-items: center; gap: 10px; }
.mb-ghost { display: inline-flex; align-items: center; gap: 6px; padding: 6px 12px; border: 1px solid var(--border-line); background: var(--paper-card); font-size: 13px; color: var(--text-secondary); cursor: pointer; user-select: none; }
.mb-ghost.on { border-color: var(--accent-color); color: var(--accent-color); background: var(--accent-soft); }
.mb-ghost input { margin: 0; accent-color: var(--accent-color); }
.mb-ghost-msg { margin: -8px 0 14px; font-size: 13px; color: var(--text-secondary); }
.mb-empty { padding: 48px 0; text-align: center; color: var(--text-secondary); }
.mb-more { display: flex; justify-content: center; margin-top: 20px; color: var(--text-secondary); font-size: 12px; }
.mb-note { margin: 24px 0 0; font-size: 12px; color: var(--text-secondary); text-align: center; }
@media (max-width: 640px) {
  .mb-search { max-width: none; }
  .mb-grid { grid-template-columns: 1fr; }
}
</style>
