<!-- 岁月史书：太初寰宇的日常——群聊名场面和截图、生活里的事、大家的成果进步。
     谁都能看；登录后谁都能「记一笔」，马上就能看到；自己只能删除自己的；管理员可以编辑、下架、恢复。
     按发生的年份、月份往下排，顶上有「那年今日」。 -->
<template>
  <div class="history-page content-page community-surface">
    <header class="hp-header surface-heading">
      <div>
        <h1>📖 岁月史书</h1>
        <p class="sub">太初寰宇的日常：群聊名场面、截图、生活琐事、大家的成果进步。谁都能记一笔。</p>
      </div>
      <div class="hp-header-actions">
        <RouterLink v-if="years.length" class="hp-btn" :to="{ name: 'HistoryYearbook', query: { year: String(year || years[0].year) } }" title="把一整年排成一本年鉴，可以打印或存成 PDF">📕 年鉴</RouterLink>
        <button v-if="userStore.isLoggedIn" type="button" class="hp-btn primary" @click="composer = { entry: null }">✍ 记一笔</button>
        <RouterLink v-else class="hp-btn" :to="{ path: LOGIN_PATH, query: { redirect: route.fullPath } }">登录后记一笔</RouterLink>
      </div>
    </header>

    <!-- 分享链接打开的那一段 -->
    <section v-if="focused" class="hp-focus" aria-label="分享的这一段">
      <div class="hp-focus-head"><span>{{ focusLabel }}</span><button type="button" class="hp-link" @click="clearFocus">{{ route.query.entry ? '看全部' : '收起' }}</button></div>
      <HistoryEntryCard :entry="focused" :can-manage="canManage" :busy="reacting.has(focused.id)" v-on="cardEvents" />
    </section>

    <section v-if="onThisDay.length && !filtered" class="hp-otd" aria-label="那年今日">
      <h2>🕰 那年今日 <small>往年的今天，社区里发生了这些事</small></h2>
      <div class="hp-otd-list">
        <HistoryEntryCard v-for="e in onThisDay" :key="e.id" :entry="e" :can-manage="canManage" compact v-on="cardEvents" />
      </div>
    </section>

    <div class="hp-tools">
      <div class="hp-kinds" role="tablist" aria-label="类型">
        <button type="button" role="tab" :aria-selected="!kind" @click="kind = ''">全部</button>
        <button v-for="(m, k) in KIND_META" :key="k" type="button" role="tab" :aria-selected="kind === k" @click="kind = k">{{ m.icon }} {{ m.label }}</button>
      </div>
      <div class="hp-tools-row">
        <input v-model="keyword" type="search" class="hp-search" placeholder="搜标题和内容…" aria-label="搜索" maxlength="50" />
        <label v-if="userStore.isLoggedIn" class="hp-check"><input v-model="mine" type="checkbox" /> 只看我记的</label>
        <div class="hp-view" role="group" aria-label="排版">
          <button type="button" :aria-pressed="!brief" @click="setBrief(false)">完整</button>
          <button type="button" :aria-pressed="brief" title="每段只显示一行，点开再看全部" @click="setBrief(true)">缩略</button>
        </div>
      </div>
      <p v-if="author" class="hp-author-filter">只看 <b>{{ authorName }}</b> 记的 <button type="button" class="hp-link" @click="author = ''">看全部</button></p>
      <p v-if="person" class="hp-author-filter">和 <b>{{ personName }}</b> 有关的：TA 记的，和别人记的时候 @ 了 TA 的 <button type="button" class="hp-link" @click="person = ''">看全部</button></p>
      <div v-if="years.length > 1" class="hp-years" role="tablist" aria-label="年份">
        <button type="button" role="tab" :aria-selected="!year" @click="year = 0">全部年份</button>
        <button v-for="y in years" :key="y.year" type="button" role="tab" :aria-selected="year === y.year" @click="year = y.year">{{ y.year }} <small>{{ y.count }}</small></button>
      </div>
    </div>

    <p v-if="error" class="hp-empty" role="alert">岁月史书暂时打不开。<button type="button" class="hp-link" @click="reload">重试</button></p>
    <p v-else-if="loading && !entries.length" class="hp-empty">正在翻开史书…</p>
    <div v-else-if="!entries.length" class="hp-empty">
      <template v-if="filtered">没有符合条件的内容。<button type="button" class="hp-link" @click="resetFilters">清除筛选</button></template>
      <template v-else>
        <p>史书还是空白的。</p>
        <p>群里的名场面、一起出去玩的照片、谁做出了什么东西……都可以记下来。</p>
        <button v-if="userStore.isLoggedIn" type="button" class="hp-btn primary" @click="composer = { entry: null }">✍ 记下第一笔</button>
      </template>
    </div>

    <div v-else class="hp-timeline" :class="{ 'is-brief': brief }">
      <section v-for="g in groups" :key="g.year" class="hp-year">
        <h2 class="hp-year-title">{{ g.year }}<small> 年</small></h2>
        <div v-for="mg in g.months" :key="mg.key" class="hp-month">
          <h3 class="hp-month-title">{{ mg.label }}</h3>
          <ol class="hp-entries">
            <li v-for="e in mg.entries" :key="e.id">
              <!-- 缩略：一行一段，点开看完整的 -->
              <button v-if="brief && !opened.has(e.id)" type="button" class="hp-brief" :class="'k-' + e.kind" :aria-expanded="false" @click="toggleOpen(e.id)">
                <span class="hp-brief-day">{{ briefDay(e) }}</span>
                <span class="hp-brief-kind" :title="KIND_META[e.kind].label">{{ KIND_META[e.kind].icon }}</span>
                <span class="hp-brief-title">{{ briefTitle(e) }}</span>
                <span class="hp-brief-stats">
                  <span v-if="e.hidden" title="已下架">🚫</span>
                  <span v-if="e.images.length" :title="`${e.images.length} 张图`">🖼 {{ e.images.length }}</span>
                  <span v-if="e.commentCount" :title="`${e.commentCount} 条评论`">💬 {{ e.commentCount }}</span>
                  <span v-if="reactionTotal(e)" :title="`${reactionTotal(e)} 个回应`">{{ topReaction(e) }} {{ reactionTotal(e) }}</span>
                  <span class="hp-brief-author">{{ e.author.username }}</span>
                </span>
              </button>
              <div v-else class="hp-opened">
                <button v-if="brief" type="button" class="hp-fold" @click="toggleOpen(e.id)">▴ 收起</button>
                <HistoryEntryCard :entry="e" :can-manage="canManage" :busy="reacting.has(e.id)" v-on="cardEvents" />
              </div>
            </li>
          </ol>
        </div>
      </section>
      <div class="hp-more">
        <button v-if="hasMore" type="button" class="hp-btn" :disabled="loading" @click="loadMore">{{ loading ? '翻页中…' : '往前翻' }}</button>
        <small v-else>— 翻到最早的一页了 —</small>
      </div>
    </div>

    <HistoryComposer v-if="composer" :key="composer.entry?.id ?? 'new'" :entry="composer.entry" @close="composer = null" @saved="onSaved" />
    <el-image-viewer v-if="viewer" :url-list="viewer.urls" :initial-index="viewer.index" hide-on-click-modal @close="viewer = null" />
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { ElImageViewer, ElMessage } from 'element-plus'
import { historyApi, errorText, KIND_META, REACTION_META, type HistoryEntry, type HistoryKind, type HistoryPage, type ReactionKind } from '@/api/history'
import { useUserStore } from '@/stores/user'
import { LOGIN_PATH } from '@/router/auth'
import HistoryEntryCard from './HistoryEntryCard.vue'
import HistoryComposer from './HistoryComposer.vue'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()

// ===== 筛选（记在地址里） =====
const kind = ref<HistoryKind | ''>((Object.keys(KIND_META) as HistoryKind[]).find(k => k === route.query.kind) ?? '')
const year = ref(Number(route.query.year) || 0)
const keyword = ref(String(route.query.q || ''))
const mine = ref(route.query.mine === '1')
/** 只看某个人记的（从个人主页点进来） */
const author = ref(typeof route.query.author === 'string' ? route.query.author : '')
/** 和某个人有关的（他记的 + @ 了他的）：卡片上点「@某人」、个人主页点进来 */
const person = ref(typeof route.query.person === 'string' ? route.query.person : '')
const personName = ref('这位成员')
watch(() => route.query.person, v => { const next = typeof v === 'string' ? v : ''; if (next !== person.value) person.value = next })
const filtered = computed(() => Boolean(kind.value || year.value || keyword.value.trim() || mine.value || author.value || person.value))
function resetFilters() { kind.value = ''; year.value = 0; keyword.value = ''; mine.value = false; author.value = ''; person.value = '' }

// 名字从列表里拿（列表都是这个人记的）
const authorName = computed(() => entries.value[0]?.author.username ?? '这位成员')

// ===== 数据 =====
const entries = ref<HistoryEntry[]>([])
const onThisDay = ref<HistoryEntry[]>([])
const years = ref<HistoryPage['years']>([])
const canManage = ref(false)
const hasMore = ref(false)
const pageNo = ref(1)
const loading = ref(false)
const error = ref(false)
let version = 0

async function fetchPage(n: number) {
  const v = ++version
  loading.value = true
  error.value = false
  try {
    const res = await historyApi.list({ kind: kind.value || undefined, year: year.value || undefined, q: keyword.value.trim() || undefined, mine: mine.value || undefined, author: author.value || undefined, person: person.value || undefined, page: n })
    if (v !== version) return
    const seen = new Set(n === 1 ? [] : entries.value.map(e => e.id))
    entries.value = [...(n === 1 ? [] : entries.value), ...res.items.filter(e => !seen.has(e.id))]
    if (n === 1) { onThisDay.value = res.onThisDay; years.value = res.years }
    canManage.value = res.canManage
    if (res.person) personName.value = res.person.username
    hasMore.value = res.hasMore
    pageNo.value = n
  } catch {
    if (v === version) error.value = true
  } finally {
    if (v === version) loading.value = false
  }
}
const reload = () => fetchPage(1)
const loadMore = () => fetchPage(pageNo.value + 1)

let searchTimer = 0
watch(keyword, () => { window.clearTimeout(searchTimer); searchTimer = window.setTimeout(reload, 300) })
watch([kind, year, mine, author, person], () => void reload())
watch([kind, year, keyword, mine, author, person], () => {
  const query = { ...route.query, kind: kind.value || undefined, year: year.value ? String(year.value) : undefined, q: keyword.value.trim() || undefined, mine: mine.value ? '1' : undefined, author: author.value || undefined, person: person.value || undefined }
  void router.replace({ query }).catch(() => {})
})
watch(() => userStore.isLoggedIn, () => void reload())
void reload()

// ===== 分享链接：?entry=ID 打开时把那一段放在最上面 =====
const focused = ref<HistoryEntry | null>(null)
const focusLabel = ref('🔗 分享给你的这一段')
async function loadFocus() {
  const id = Number(route.query.entry)
  if (!id) { focused.value = null; return }
  focusLabel.value = '🔗 分享给你的这一段'
  try { focused.value = await historyApi.get(id) }
  catch { focused.value = null; ElMessage.warning('分享的那一段找不到了，可能已经被删除') }
}
watch(() => route.query.entry, loadFocus, { immediate: true })
function clearFocus() {
  if (route.query.entry) void router.replace({ query: { ...route.query, entry: undefined } }).catch(() => {})
  else focused.value = null
}

// ===== 缩略 / 完整（记在本机） =====
const brief = ref(false)
try { brief.value = localStorage.getItem('taichu.history.brief') === '1' } catch { /* 存不了就用完整 */ }
const opened = ref(new Set<number>())
function setBrief(v: boolean) {
  brief.value = v; opened.value = new Set()
  try { localStorage.setItem('taichu.history.brief', v ? '1' : '0') } catch { /* 忽略 */ }
}
function toggleOpen(id: number) {
  const s = new Set(opened.value)
  if (s.has(id)) s.delete(id); else s.add(id)
  opened.value = s
}
const briefDay = (e: HistoryEntry) => {
  const d = Number(e.happenedOn.slice(8, 10))
  return e.precision === 'day' ? `${d} 日` : e.precision === 'month' ? '某天' : ''
}
const briefTitle = (e: HistoryEntry) => e.title || (e.content ?? '').replace(/\s+/g, ' ').slice(0, 60) || (e.images.length ? `（${e.images.length} 张截图）` : '（无标题）')
const reactionTotal = (e: HistoryEntry) => Object.values(e.reactions).reduce((s, n) => s + (n ?? 0), 0)
const topReaction = (e: HistoryEntry) => {
  const top = (Object.entries(e.reactions) as [keyof typeof REACTION_META, number][]).sort((a, b) => b[1] - a[1])[0]
  return top ? REACTION_META[top[0]].icon : ''
}

// ===== 按年、月分组 =====
const groups = computed(() => {
  const out: { year: number; months: { key: string; label: string; entries: HistoryEntry[] }[] }[] = []
  for (const e of entries.value) {
    const [y, m] = e.happenedOn.split('-').map(Number) as [number, number]
    let g = out[out.length - 1]
    if (!g || g.year !== y) { g = { year: y, months: [] }; out.push(g) }
    const key = e.precision === 'year' ? `${y}-?` : `${y}-${m}`
    let mg = g.months[g.months.length - 1]
    if (!mg || mg.key !== key) { mg = { key, label: e.precision === 'year' ? '这一年里（记不清哪个月）' : `${m} 月`, entries: [] }; g.months.push(mg) }
    mg.entries.push(e)
  }
  return out
})

// ===== 卡片上的操作 =====
const viewer = ref<{ urls: string[]; index: number } | null>(null)
const composer = ref<{ entry: HistoryEntry | null } | null>(null)
const reacting = ref(new Set<number>())

function replaceEntry(next: HistoryEntry) {
  entries.value = entries.value.map(e => (e.id === next.id ? next : e))
  onThisDay.value = onThisDay.value.map(e => (e.id === next.id ? next : e))
  if (focused.value?.id === next.id) focused.value = next
}
function dropEntry(id: number) {
  entries.value = entries.value.filter(e => e.id !== id)
  onThisDay.value = onThisDay.value.filter(e => e.id !== id)
  if (focused.value?.id === id) clearFocus()
}
function onSaved(entry: HistoryEntry) {
  const isNew = !entries.value.some(e => e.id === entry.id) && focused.value?.id !== entry.id
  composer.value = null
  if (isNew) {
    ElMessage.success('记下来了')
    void reload()
    // 刚记的那一段先放在最上面（它按发生日期排，可能在很后面）
    focusLabel.value = '✅ 刚记下的这一段'
    focused.value = entry
  } else { replaceEntry(entry); ElMessage.success('已保存修改') }
}
const cardEvents = {
  preview: (urls: string[], index: number) => { viewer.value = { urls, index } },
  update: (entry: HistoryEntry) => replaceEntry(entry),
  react: async (entry: HistoryEntry, k: ReactionKind) => {
    if (!userStore.isLoggedIn) { ElMessage.info('登录后才能回应'); return }
    const s = new Set(reacting.value); s.add(entry.id); reacting.value = s
    try {
      const res = await historyApi.react(entry.id, k)
      replaceEntry({ ...entry, reactions: res.reactions, myReactions: res.myReactions })
    } catch (e) { ElMessage.error(errorText(e, '没点上，稍后再试')) }
    finally { const t = new Set(reacting.value); t.delete(entry.id); reacting.value = t }
  },
  edit: (entry: HistoryEntry) => { composer.value = { entry } },
  hide: async (entry: HistoryEntry) => {
    const reason = window.prompt('下架这一段？可以写一下原因（上传的人能看到）：', '')
    if (reason === null) return
    try { replaceEntry(await historyApi.hide(entry.id, reason.trim())); ElMessage.success('已下架，其他人看不到了') }
    catch (e) { ElMessage.error(errorText(e, '下架失败')) }
  },
  unhide: async (entry: HistoryEntry) => {
    try { replaceEntry(await historyApi.unhide(entry.id)); ElMessage.success('已恢复') }
    catch (e) { ElMessage.error(errorText(e, '恢复失败')) }
  },
  delete: async (entry: HistoryEntry) => {
    const own = entry.author.id === userStore.userId
    if (!window.confirm(own ? '删除这一段？删除后找不回来。' : `删除「${entry.author.username}」上传的这一段？删除后找不回来（只想让大家看不到可以用「下架」）。`)) return
    try { await historyApi.remove(entry.id); dropEntry(entry.id); ElMessage.success('已删除') }
    catch (e) { ElMessage.error(errorText(e, '删除失败')) }
  },
  'copy-link': async (entry: HistoryEntry) => {
    const url = location.origin + router.resolve({ name: 'CommunityHistory', query: { entry: String(entry.id) } }).href
    try { await navigator.clipboard.writeText(url); ElMessage.success('链接已复制，发给朋友就能直接看到这一段') }
    catch { window.prompt('复制这个链接：', url) }
  },
}
</script>

<style scoped>
.history-page { max-width: 900px; margin: 0 auto; padding: 0 0 48px; }
.hp-header { display: flex; justify-content: space-between; align-items: flex-end; gap: 16px; flex-wrap: wrap; padding: 24px 0 18px; }
.hp-header h1 { margin: 0; font-size: clamp(1.7rem, 3.4vw, 2.3rem); }
.hp-header .sub { margin: 8px 0 0; color: var(--text-secondary); font-size: .9rem; line-height: 1.6; }
.hp-btn { display: inline-flex; align-items: center; padding: 8px 16px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 14px; text-decoration: none; cursor: pointer; }
.hp-btn:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); }
.hp-btn.primary { background: var(--accent-color); border-color: var(--accent-color); color: #fff; }
.hp-btn.primary:hover:not(:disabled) { color: #fff; filter: brightness(1.08); }
.hp-btn:disabled { opacity: .6; cursor: default; }
.hp-link { padding: 0; border: 0; background: none; color: var(--accent-color); font: inherit; cursor: pointer; text-decoration: underline; text-underline-offset: 2px; }

.hp-focus { margin-bottom: 20px; padding: 10px; background: color-mix(in srgb, var(--accent-color) 8%, transparent); border: 1px solid var(--accent-color); }
.hp-focus-head { display: flex; justify-content: space-between; margin-bottom: 8px; font-size: 13px; font-weight: 600; }

.hp-otd { margin-bottom: 22px; }
.hp-otd h2 { margin: 0 0 10px; font-size: 16px; }
.hp-otd h2 small { margin-left: 6px; font-size: 12px; font-weight: 400; color: var(--text-secondary); }
.hp-otd-list { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 10px; }

.hp-tools { display: grid; gap: 10px; margin-bottom: 20px; }
.hp-kinds, .hp-years { display: flex; flex-wrap: wrap; gap: 4px; }
.hp-kinds button, .hp-years button { padding: 5px 12px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.hp-kinds button[aria-selected=true], .hp-years button[aria-selected=true] { background: var(--accent-color); border-color: var(--accent-color); color: #fff; }
.hp-years button small { margin-left: 2px; opacity: .7; font-size: 11px; }
.hp-tools-row { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; }
.hp-search { flex: 1 1 220px; max-width: 360px; padding: 7px 10px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; }
.hp-view { display: flex; margin-left: auto; }
.hp-view button { padding: 5px 12px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.hp-view button + button { border-left: 0; }
.hp-view button[aria-pressed=true] { background: var(--accent-color); border-color: var(--accent-color); color: #fff; }
.hp-brief { display: flex; align-items: center; gap: 10px; width: 100%; min-width: 0; padding: 9px 12px; border: 1px solid var(--border-line); border-left: 3px solid var(--kc, var(--accent-color)); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 14px; text-align: left; cursor: pointer; }
.hp-brief:hover { border-color: var(--accent-color); }
.hp-brief.k-funny { --kc: #f59e0b; } .hp-brief.k-chat { --kc: #3b82f6; } .hp-brief.k-life { --kc: #22c55e; } .hp-brief.k-progress { --kc: #a855f7; } .hp-brief.k-event { --kc: #ef4444; } .hp-brief.k-other { --kc: #64748b; }
.hp-brief-day { flex: 0 0 3.2em; font-size: 12px; font-weight: 600; color: var(--text-secondary); font-variant-numeric: tabular-nums; }
.hp-brief-title { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.hp-brief-stats { display: flex; flex-shrink: 0; gap: 8px; font-size: 12px; color: var(--text-secondary); white-space: nowrap; }
.hp-brief-author { max-width: 7em; overflow: hidden; text-overflow: ellipsis; }
.hp-opened { display: grid; gap: 4px; }
.hp-fold { justify-self: end; padding: 0; border: 0; background: none; color: var(--accent-color); font: inherit; font-size: 12px; cursor: pointer; }
.hp-author-filter { margin: 0; font-size: 13px; color: var(--text-secondary); }
.hp-author-filter b { color: var(--text-primary); }
.hp-check { display: inline-flex; align-items: center; gap: 6px; font-size: 13px; color: var(--text-secondary); cursor: pointer; }

.hp-empty { padding: 48px 0; text-align: center; color: var(--text-secondary); line-height: 1.8; }
.hp-empty p { margin: 0; }
.hp-empty .hp-btn { margin-top: 14px; }

.hp-timeline { position: relative; }
.hp-year + .hp-year { margin-top: 28px; }
.hp-year-title { position: sticky; top: -28px; z-index: 2; margin: 0 0 6px; padding: 8px 0; font-size: 26px; font-family: var(--font-mono, monospace); background: color-mix(in srgb, var(--bg-main) 92%, transparent); backdrop-filter: blur(4px); }
.hp-year-title small { font-size: 14px; font-family: inherit; color: var(--text-secondary); }
.hp-month { position: relative; padding-left: 22px; border-left: 2px solid var(--border-line); margin-left: 6px; }
.hp-month-title { position: relative; margin: 0; padding: 12px 0 8px; font-size: 14px; color: var(--text-secondary); }
.hp-month-title::before { content: ''; position: absolute; left: -29px; top: 15px; width: 10px; height: 10px; border-radius: 50%; background: var(--accent-color); box-shadow: 0 0 0 3px var(--bg-main); }
.hp-entries { list-style: none; margin: 0; padding: 0 0 8px; display: grid; gap: 12px; }
.hp-timeline.is-brief .hp-entries { gap: 4px; }
.hp-more { display: flex; justify-content: center; margin-top: 24px; color: var(--text-secondary); font-size: 12px; }
@media (max-width: 640px) {
  .hp-brief-author { display: none; }
  .hp-month { padding-left: 14px; margin-left: 4px; }
  .hp-month-title::before { left: -20px; }
  .hp-search { max-width: none; }
  .hp-year-title { top: -20px; }
}
.hp-header-actions { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
</style>
