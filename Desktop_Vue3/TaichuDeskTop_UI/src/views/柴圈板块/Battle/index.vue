<template>
  <div class="battle-page content-page community-surface tone-chai">
    <!-- ============ 顶部：和柴圈其它页面同一种页头 ============ -->
    <header class="content-header">
      <SectionTrace />
      <div>
        <p class="eyebrow">TCV · 太初论战</p>
        <h1>约战广场</h1>
        <p class="intro">画师以创作为刃，一决高下：发起一场约战，双方各交作品，用投票或协商分出胜负，结果记进 OC 战绩。</p>
      </div>
      <div class="content-actions">
        <button class="primary" @click="goCreate">发起约战</button>
        <button @click="goMyBattles">我的约战</button>
        <button @click="goLeaderboard">🏆 排行榜</button>
      </div>
    </header>
    <nav class="community-nav" aria-label="柴圈导航"><RouterLink to="/community">社区总览</RouterLink><RouterLink to="/ocs">OC 角色</RouterLink><RouterLink to="/works">动画作品</RouterLink><RouterLink to="/joint">联合创作</RouterLink><RouterLink to="/battles" class="selected">约战广场</RouterLink><RouterLink to="/battles/leaderboard">🏆 排行榜</RouterLink></nav>

    <!-- ============ 作品审核（只有审核员看得到：超级管理员 / 用户审核权限） ============ -->
    <section v-if="pending" class="review-box">
      <h2>📋 待审核作品 <small>{{ pending.length }} 件</small></h2>
      <p v-if="!pending.length" class="review-empty">没有待审核的作品</p>
      <ul v-else class="review-list">
        <li v-for="w in pending" :key="w.id" class="review-item">
          <div class="review-main">
            <b>{{ w.title }}</b>
            <span class="review-meta">约战「<a href="#" @click.prevent="goDetail(w.battleId)">{{ w.battleTitle }}</a>」 <UserLink :id="w.userId" :name="w.userName" /> {{ formatTime(w.submittedAt) }}</span>
            <span class="review-url">{{ w.contentUrl }}</span>
            <span v-if="w.description" class="review-desc">{{ w.description }}</span>
          </div>
          <div class="review-actions">
            <a :href="w.contentUrl" target="_blank" rel="noopener noreferrer" class="rv-btn">打开看看</a>
            <button class="rv-btn rv-ok" :disabled="reviewing === w.id" @click="review(w, true)">通过</button>
            <button class="rv-btn" :disabled="reviewing === w.id" @click="review(w, false)">打回</button>
          </div>
        </li>
      </ul>
    </section>

    <!-- ============ 约战怎么打：默认收起，第一次来的人点开看 ============ -->
    <details class="howto">
      <summary>第一次来？看看约战怎么打</summary>
      <div class="howto-body">
        <p class="howto-intro">
          约战是画师之间以<strong>创作作品</strong>为形式的对决。
          每位参与者提交作品链接，通过<strong>投票</strong>或<strong>内定</strong>判定胜负，结果会影响 OC 的战绩。
        </p>
        <ol class="howto-flow">
          <li><b>发起</b><span>设定类型、规则、判定方式</span></li>
          <li><b>报名</b><span>选择 OC 加入</span></li>
          <li><b>提交</b><span>上传作品链接</span></li>
          <li><b>判定</b><span>投票制 / 内定制</span></li>
          <li><b>更新</b><span>记录战绩，更新 OC 状态</span></li>
        </ol>
        <ul class="howto-notes">
          <li><strong>投票制</strong>：路人投票决定胜负</li>
          <li><strong>内定制</strong>：参与者协商决定胜负，为剧情服务</li>
          <li>结果可以是一方胜、惨胜、平手、同归于尽，也可以不计结果</li>
          <li>须提交<strong>作品链接</strong>，作为判定依据</li>
        </ul>
      </div>
    </details>

    <!-- ============ 筛选：按阶段一键切换 + 搜索 ============ -->
    <div class="filter-bar">
      <div class="content-filters" role="group" aria-label="按阶段筛选">
        <button v-for="f in filters" :key="f.value" type="button" :aria-pressed="filterStatus === f.value" @click="setStatus(f.value)">{{ f.label }}</button>
      </div>
      <label class="search">
        <span class="sr-only">搜索约战</span>
        <input v-model="keyword" type="search" placeholder="搜索约战标题…" @input="onKeyword" />
      </label>
      <span class="result-count">共 {{ total }} 场</span>
    </div>

    <!-- ============ 约战列表 ============ -->
    <div v-if="loading && !list.length" class="state">正在读取约战…</div>

    <div v-else-if="loadFailed" class="state">
      <p>约战列表没读出来，可能是网络不稳定。</p>
      <button class="link-btn" @click="fetchList">重新读取</button>
    </div>

    <div v-else-if="!list.length" class="state">
      <template v-if="filterStatus || keyword.trim()">
        <p>没有符合条件的约战。</p>
        <button class="link-btn" @click="clearFilters">看全部约战</button>
      </template>
      <template v-else>
        <p class="state-big">⚔ 这里还很安静</p>
        <p>还没有人发起约战。挑一个你的 OC，向别的画师下一封战书吧。</p>
        <button class="link-btn" @click="goCreate">发起第一场约战</button>
      </template>
    </div>

    <div v-else class="battle-list" :class="{ dim: loading }">
      <BattleCard v-for="item in list" :key="item.id" :battle="item" @open="goDetail" />
    </div>

    <!-- 分页 -->
    <div v-if="total > pageSize" class="pagination">
      <button @click="page--" :disabled="page <= 1 || loading">上一页</button>
      <span>{{ page }} / {{ Math.ceil(total / pageSize) }}</span>
      <button @click="page++" :disabled="page >= Math.ceil(total / pageSize) || loading">下一页</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import UserLink from '@/components/UserLink.vue'
import SectionTrace from '@/components/SectionTrace.vue'
import BattleCard from './components/BattleCard.vue'
import { ref, onMounted, onUnmounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { battleApi, type Battle, type PendingBattleWork } from './battle_api'
import { STATUS_LABEL } from './battleView'

const router = useRouter()

const list = ref<Battle[]>([])
const loading = ref(false)
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)
const filterStatus = ref('')
const keyword = ref('')

// 阶段筛选：状态名和详情页、我的约战统一（battleView.ts）。
// 「已取消」不放：后端大厅列表本来就不返回已取消的约战，以前选了只会看到空列表
const filters = [
  { value: '', label: '全部' },
  ...(['open', 'ongoing', 'judging', 'finished'] as const).map(v => ({ value: v, label: STATUS_LABEL[v] })),
]
const loadFailed = ref(false)

const formatTime = (iso: string) => {
  const d = new Date(iso)
  const now = new Date()
  const diff = Math.floor((now.getTime() - d.getTime()) / 86400000)
  if (diff === 0) return '今天'
  if (diff === 1) return '昨天'
  if (diff < 7) return `${diff} 天前`
  return d.toLocaleDateString('zh-CN', { month: 'short', day: 'numeric' })
}

// 连续打字时会连发几次请求，只认最后一次的结果，免得旧结果盖掉新结果
let listRequest = 0
const fetchList = async () => {
  const req = ++listRequest
  loading.value = true
  loadFailed.value = false
  try {
    const params: any = { page: page.value, pageSize: pageSize.value }
    if (filterStatus.value) params.status = filterStatus.value
    if (keyword.value.trim()) params.keyword = keyword.value.trim()
    const res = await battleApi.list(params)
    if (req !== listRequest) return
    list.value = res.items
    total.value = res.total
  } catch {
    if (req === listRequest) loadFailed.value = true
  } finally {
    if (req === listRequest) loading.value = false
  }
}

// 以前翻页按钮只改了页码、没有重新读列表，点了没反应
watch(page, () => { void fetchList() })
/** 换筛选条件时回到第一页（已经在第一页就直接读） */
function reload() {
  if (page.value !== 1) page.value = 1
  else void fetchList()
}
function setStatus(v: string) {
  if (filterStatus.value === v) return
  filterStatus.value = v
  reload()
}
// 搜索框：停手 300 毫秒再搜，不用每敲一个字就请求一次
let keywordTimer: ReturnType<typeof setTimeout> | undefined
function onKeyword() {
  clearTimeout(keywordTimer)
  keywordTimer = setTimeout(reload, 300)
}
function clearFilters() {
  filterStatus.value = ''
  keyword.value = ''
  reload()
}
onUnmounted(() => clearTimeout(keywordTimer))

const goCreate = () => router.push('/battles/create')
const goMyBattles = () => router.push('/battles/my')
const goLeaderboard = () => router.push('/battles/leaderboard')
const goDetail = (id: string) => router.push(`/battles/${id}`)

// ===== 作品审核：接口返回 403 说明不是审核员，就不显示这一块 =====
const pending = ref<PendingBattleWork[] | null>(null)
const reviewing = ref('')
async function loadPending() {
  try { pending.value = (await battleApi.pendingReviews()).items } catch { pending.value = null }
}
async function review(w: PendingBattleWork, approve: boolean) {
  let reason: string | undefined
  if (!approve) {
    const r = window.prompt(`打回「${w.title}」的原因（会通知作者）：`)
    if (!r?.trim()) return
    reason = r.trim()
  }
  reviewing.value = w.id
  try {
    await battleApi.reviewSubmission(w.id, approve, reason)
    pending.value = (pending.value ?? []).filter(x => x.id !== w.id)
  } catch {
    alert('操作失败，请稍后再试')
  } finally {
    reviewing.value = ''
  }
}

onMounted(() => { void fetchList(); void loadPending() })
</script>

<style scoped>
/* ============================================================
   约战广场：页头、导航沿用柴圈公共样式（content-page / community-surface），这里只管约战自己的部分
   ============================================================ */
.battle-page { padding: 0 20px 60px; }
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }

/* ===== 审核员：待审核作品 ===== */
.review-box { margin: 0 0 20px; padding: 14px 16px; border: 1px solid var(--border-line); border-left: 4px solid var(--caution); background: var(--paper-card); }
.review-box h2 { margin: 0 0 10px; font-size: 16px; }
.review-box h2 small { margin-left: 6px; color: var(--text-secondary); font-weight: 400; font-size: 13px; }
.review-empty { margin: 0; color: var(--text-secondary); font-size: 13px; }
.review-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 10px; }
.review-item { display: flex; gap: 12px; align-items: flex-start; justify-content: space-between; padding-top: 10px; border-top: 1px dashed var(--border-line); }
.review-main { min-width: 0; display: grid; gap: 3px; font-size: 13px; }
.review-meta, .review-desc { color: var(--text-secondary); display: flex; flex-wrap: wrap; gap: 0 8px; }
.review-meta a { color: var(--accent-color); }
.review-url { word-break: break-all; color: var(--accent-color); }
.review-actions { display: flex; gap: 6px; flex-shrink: 0; }
.rv-btn { display: inline-flex; align-items: center; padding: 5px 12px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; text-decoration: none; cursor: pointer; }
.rv-btn:hover:not(:disabled) { border-color: var(--accent-color); color: var(--accent-color); }
.rv-btn.rv-ok { background: var(--accent-color); border-color: var(--accent-color); color: var(--on-accent); }
@media (max-width: 640px) { .review-item { flex-direction: column; } }

/* ===== 约战怎么打（可收起） ===== */
.howto { margin-bottom: 20px; border: 1px solid var(--border-line); background: var(--paper-card); }
.howto summary { padding: 10px 14px; font-size: 14px; color: var(--text-primary); cursor: pointer; list-style-position: inside; }
.howto summary:hover { color: var(--accent-color); }
.howto[open] summary { border-bottom: 1px dashed var(--border-line); }
.howto-body { display: grid; gap: 14px; padding: 14px 16px 16px; }
.howto-intro { margin: 0; font-size: 14px; line-height: 1.9; color: var(--text-secondary); max-width: 46em; }
.howto-intro strong, .howto-notes strong { color: var(--text-primary); }
/* 五步流程：真的是先后顺序，所以用编号 */
.howto-flow { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); counter-reset: step; }
.howto-flow li { counter-increment: step; display: grid; gap: 2px; padding: 10px 10px 0 0; border-top: 3px solid var(--border-line); font-size: 12px; color: var(--text-secondary); }
.howto-flow li::before { content: counter(step); font: 700 18px/1 var(--font-display); color: var(--accent-color); }
.howto-flow li b { font-size: 14px; color: var(--text-primary); }
.howto-notes { margin: 0; padding-left: 18px; display: grid; gap: 2px; font-size: 13px; color: var(--text-secondary); }

/* ===== 筛选 ===== */
.filter-bar { display: flex; align-items: center; gap: 10px 14px; flex-wrap: wrap; margin-bottom: 16px; }
.filter-bar .content-filters { flex-wrap: wrap; }
.search { flex: 1 1 200px; min-width: 0; display: flex; }
.search input { width: 100%; min-height: 38px; padding: 6px 12px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); outline: none; }
.search input:focus { border-color: var(--text-primary); }
.result-count { font-size: 13px; color: var(--text-secondary); white-space: nowrap; font-variant-numeric: tabular-nums; }

/* ===== 列表 ===== */
.battle-list { display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 400px), 1fr)); gap: 14px; transition: opacity .2s; }
.battle-list.dim { opacity: .6; }

.state { padding: 48px 20px; border: 1px dashed var(--border-line); background: var(--paper-card); text-align: center; color: var(--text-secondary); font-size: 14px; }
.state p { margin: 0 0 10px; }
.state-big { font: 600 22px/1.4 var(--font-display); color: var(--text-primary); }
.link-btn { padding: 8px 18px; border: 1px solid var(--accent-color); background: var(--accent-color); color: var(--on-accent); font: inherit; font-size: 14px; cursor: pointer; }
.link-btn:hover { filter: brightness(1.08); }

/* ===== 分页 ===== */
.pagination { display: flex; justify-content: center; align-items: center; gap: 14px; margin-top: 28px; padding-top: 18px; border-top: 1px solid var(--border-line); }
.pagination button { padding: 6px 18px; border: 1px solid var(--border-line); background: var(--paper-card); cursor: pointer; font: inherit; font-size: 13px; color: var(--text-primary); }
.pagination button:hover:not(:disabled) { border-color: var(--text-primary); }
.pagination button:disabled { opacity: .35; cursor: not-allowed; }
.pagination span { font-size: 13px; color: var(--text-secondary); min-width: 60px; text-align: center; font-variant-numeric: tabular-nums; }

@media (max-width: 720px) {
  .battle-page { padding: 0 12px 40px; }
  .howto-flow { grid-template-columns: repeat(auto-fill, minmax(110px, 1fr)); row-gap: 10px; }
  .filter-bar .content-filters { width: 100%; }
  .filter-bar .content-filters button { flex: 1 1 auto; padding: 7px 10px; }
}
@media (prefers-reduced-motion: reduce) { .battle-list { transition: none; } }
</style>
