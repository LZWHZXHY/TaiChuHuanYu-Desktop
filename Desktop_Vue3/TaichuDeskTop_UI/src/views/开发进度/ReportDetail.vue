<template>
  <div class="report-detail content-page community-surface tone-collab">
    <RouterLink to="/dev-progress" class="back">← 返回研发报告</RouterLink>

    <p v-if="loading" class="state">加载中…</p>
    <p v-else-if="!project" class="state">报告不存在或已被删除</p>

    <article v-else class="article">
      <header class="article-header surface-heading"><SectionTrace />
        <h1 class="article-title"><span class="article-icon">{{ project.icon }}</span>{{ project.name }}</h1>
        <div class="article-meta">
          <span class="tag" :class="project.status">{{ statusLabel(project.status) }}</span>
          <span>{{ categoryLabel(project.category) }}</span>
          <span class="dot">·</span>
          <span>更新于 {{ formatDate(project.updatedAt) }}</span>
        </div>
      </header>

      <p v-if="project.description" class="article-abstract">{{ project.description }}</p>

      <section class="article-progress">
        <div class="progress-head">
          <span class="progress-label">研发进度</span>
          <span class="progress-pct">{{ project.progress }}%</span>
        </div>
        <div class="progress-bar"><span class="progress-fill" :style="{ width: project.progress + '%' }"></span></div>
      </section>

      <!-- 项目介绍：长期不变的说明，过长时折叠 -->
      <section v-if="project.content" class="article-section intro-section">
        <h2 class="section-title">📄 项目介绍</h2>
        <div class="intro-wrap" :class="{ collapsed: introLong && !introOpen }">
          <div class="article-body" v-html="renderMarkdown(project.content)"></div>
        </div>
        <button v-if="introLong" type="button" class="fold-btn" @click="introOpen = !introOpen">{{ introOpen ? '收起介绍 ↑' : '展开全部介绍 ↓' }}</button>
      </section>

      <!-- 开发日志时间线：最新在上，可按月份筛选，长条目折叠 -->
      <section v-if="logs.length" class="article-section timeline-section">
        <div class="timeline-head">
          <h2 class="section-title">🗓 {{ project.type === 'research' ? '研究日志' : '开发日志' }} <small>共 {{ logs.length }} 条</small></h2>
          <div v-if="months.length > 1" class="month-chips" role="group" aria-label="按月份查看">
            <button type="button" :class="{ active: !month }" :aria-pressed="!month" @click="month = ''">全部</button>
            <button v-for="m in months" :key="m.key" type="button" :class="{ active: month === m.key }" :aria-pressed="month === m.key" @click="month = m.key">{{ m.label }}<small>{{ m.count }}</small></button>
          </div>
        </div>
        <ol class="timeline">
          <li v-for="l in shownLogs" :key="l.id" class="tl-item">
            <div class="tl-date" :title="formatLogDate(l.date)">
              <b>{{ l.date ? l.date.slice(8, 10) : '--' }}</b>
              <span>{{ l.date ? l.date.slice(0, 7) : '未注明' }}</span>
            </div>
            <article class="tl-card">
              <header class="tl-card-head">
                <h3>{{ l.title || '（无标题）' }}</h3>
                <span v-if="l.progress != null" class="tl-progress">进度 {{ l.progress }}%<em v-if="deltas.get(l.id)" :class="{ down: deltas.get(l.id)! < 0 }">{{ deltas.get(l.id)! > 0 ? '+' : '' }}{{ deltas.get(l.id) }}%</em></span>
              </header>
              <div class="tl-body-wrap" :class="{ collapsed: isLong(l) && !expanded.has(l.id) }">
                <div v-if="l.content" class="article-body tl-body" v-html="renderMarkdown(l.content)"></div>
                <div v-if="l.images?.length" class="log-images">
                  <img v-for="(img, i) in l.images" :key="i" :src="img" alt="" loading="lazy" />
                </div>
              </div>
              <button v-if="isLong(l)" type="button" class="fold-btn" @click="toggle(l.id)">{{ expanded.has(l.id) ? '收起 ↑' : '展开全文 ↓' }}</button>
            </article>
          </li>
        </ol>
        <button v-if="filteredLogs.length > visibleCount" type="button" class="more-btn" @click="visibleCount += 10">查看更早的 {{ filteredLogs.length - visibleCount }} 条日志 ↓</button>
      </section>

      <section v-if="project.milestones?.length" class="article-milestones">
        <h2 class="section-title">里程碑</h2>
        <ul class="ms-list">
          <li v-for="(m, i) in project.milestones" :key="i" class="ms-item" :class="{ done: m.done }">
            <span class="check">{{ m.done ? '✓' : '○' }}</span>
            <span class="ms-title">{{ m.title }}</span>
          </li>
        </ul>
      </section>

      <section v-if="project.references?.length" class="article-section">
        <h2 class="section-title">📚 参考文献</h2>
        <ol class="ref-list">
          <li v-for="r in project.references" :key="r.id" class="ref-item">
            <a v-if="r.url" :href="r.url" target="_blank" rel="noopener noreferrer" class="ref-title">{{ r.title }}</a>
            <span v-else class="ref-title">{{ r.title }}</span>
            <p class="ref-meta">
              <span v-if="r.authors">{{ r.authors }}</span>
              <span v-if="r.venue"> · {{ r.venue }}</span>
              <span v-if="r.year"> · {{ r.year }}</span>
            </p>
            <p v-if="r.note" class="ref-note">{{ r.note }}</p>
          </li>
        </ol>
      </section>

      <section v-if="project.dataPoints?.length" class="article-section">
        <h2 class="section-title">📊 数据</h2>
        <div class="data-grid">
          <div v-for="d in project.dataPoints" :key="d.id" class="data-card">
            <span class="data-name">{{ d.name }}</span>
            <span class="data-value">{{ d.value }}<small v-if="d.unit">{{ d.unit }}</small></span>
            <p v-if="d.note" class="data-note">{{ d.note }}</p>
          </div>
        </div>
      </section>

    </article>
  </div>
</template>

<script setup lang="ts">
import SectionTrace from '@/components/SectionTrace.vue'
import { computed, ref, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { devProjectApi, type DevProject } from '@/api/devProject'
import { renderMarkdown } from '@/utils/markdown'
import { formatLogDate, monthOf, progressDeltas, sortLogs } from '@/utils/devLogs'
import type { ResearchLog } from '@/api/devProject'

const route = useRoute()
const project = ref<DevProject | null>(null)
const loading = ref(true)

const categoryMap: Record<string, string> = { ai: 'AI', mod: '我的世界 Mod', game: '虚幻游戏', world: '世界观', ip: 'IP 动画', web: '网站', other: '其他' }
const categoryLabel = (k: string) => categoryMap[k] || '其他'
const statusLabel = (s: string) => ({ planning: '规划中', ongoing: '进行中', done: '已完成', paused: '暂停', dead: '暴死' } as Record<string, string>)[s] || s
const formatDate = (v: string) => v ? new Date(v).toLocaleDateString('zh-CN', { year: 'numeric', month: 'long', day: 'numeric' }) : ''

async function load() {
  loading.value = true
  const id = String(route.params.id)
  try {
    const list = await devProjectApi.list()
    // 知识库 / 图谱里带过来的项目 ID 大小写可能和接口返回的不一样，不区分大小写比较
    project.value = list.find(p => p.id.toLowerCase() === id.toLowerCase()) || null
  } catch {
    project.value = null
  } finally {
    loading.value = false
  }
}

// ---- 日志时间线 ----
const PAGE = 5
const logs = computed(() => sortLogs(project.value?.logs ?? []))
const deltas = computed(() => progressDeltas(logs.value))
const month = ref('')
const visibleCount = ref(PAGE)
const expanded = ref(new Set<string>())
const months = computed(() => {
  const counts = new Map<string, number>()
  for (const l of logs.value) counts.set(monthOf(l.date), (counts.get(monthOf(l.date)) ?? 0) + 1)
  return [...counts].map(([key, count]) => ({ key, count, label: /^\d{4}-\d{2}$/.test(key) ? `${key.slice(0, 4)}.${key.slice(5)}` : key }))
})
const filteredLogs = computed(() => month.value ? logs.value.filter(l => monthOf(l.date) === month.value) : logs.value)
const shownLogs = computed(() => filteredLogs.value.slice(0, visibleCount.value))
watch(month, () => { visibleCount.value = PAGE })
/** 超过约 12 行或带图片的日志默认折叠 */
const isLong = (l: ResearchLog) => (l.content?.length ?? 0) > 500 || (l.content?.split('\n').length ?? 0) > 12 || /!\[[^\]]*]\(/.test(l.content ?? '') || (l.images?.length ?? 0) > 0
function toggle(id: string) {
  const next = new Set(expanded.value)
  next.has(id) ? next.delete(id) : next.add(id)
  expanded.value = next
}
// ---- 项目介绍折叠 ----
const introOpen = ref(false)
const introLong = computed(() => (project.value?.content?.length ?? 0) > 900 || (project.value?.content?.split('\n').length ?? 0) > 24)

onMounted(load)
watch(() => route.params.id, () => { month.value = ''; visibleCount.value = PAGE; expanded.value = new Set(); introOpen.value = false; void load() })
</script>

<style scoped>
.report-detail { max-width: 820px; margin: 0 auto; padding: 0 0 64px; }

/* 项目介绍折叠 */
.intro-wrap.collapsed { max-height: 380px; overflow: hidden; -webkit-mask-image: linear-gradient(#000 70%, transparent); mask-image: linear-gradient(#000 70%, transparent); }
.fold-btn { margin-top: 10px; padding: 6px 14px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--accent-color); font: inherit; font-size: .8rem; cursor: pointer; }
.fold-btn:hover { border-color: var(--accent-color); }

/* 日志时间线 */
.timeline-head { display: flex; flex-wrap: wrap; align-items: baseline; justify-content: space-between; gap: 10px 16px; }
.timeline-head .section-title small { margin-left: 6px; font-size: .75rem; font-weight: 400; color: var(--text-secondary); }
.month-chips { display: flex; flex-wrap: wrap; gap: 6px; }
.month-chips button { display: inline-flex; align-items: baseline; gap: 4px; padding: 4px 10px; border: 1px solid var(--border-line); background: transparent; color: var(--text-secondary); font: 12px var(--font-mono, monospace); cursor: pointer; }
.month-chips button small { opacity: .6; font-size: 10px; }
.month-chips button.active { border-color: var(--accent-color); color: var(--accent-color); background: var(--accent-soft); }
.timeline { list-style: none; margin: 18px 0 0; padding: 0; position: relative; }
.timeline::before { content: ''; position: absolute; left: 58px; top: 6px; bottom: 6px; width: 2px; background: linear-gradient(var(--signal-mint), var(--border-line)); }
.tl-item { position: relative; display: grid; grid-template-columns: 48px minmax(0, 1fr); gap: 26px; padding-bottom: 22px; }
.tl-item::before { content: ''; position: absolute; left: 53px; top: 10px; width: 12px; height: 12px; background: var(--paper-card); border: 2px solid var(--accent-color); transform: rotate(45deg); }
.tl-item:first-child::before { background: var(--signal-mint); border-color: var(--signal-mint); }
.tl-date { display: grid; justify-items: end; align-content: start; padding-top: 2px; font-family: var(--font-mono, monospace); }
.tl-date b { font-size: 1.35rem; line-height: 1; color: var(--text-primary); }
.tl-date span { margin-top: 4px; font-size: .68rem; color: var(--text-secondary); white-space: nowrap; }
.tl-card { min-width: 0; padding: 14px 18px; border: 1px solid var(--border-line); border-left: 3px solid var(--accent-color); background: var(--paper-card); }
.tl-item:first-child .tl-card { border-left-color: var(--signal-mint); }
.tl-card-head { display: flex; flex-wrap: wrap; align-items: baseline; justify-content: space-between; gap: 6px 12px; }
.tl-card-head h3 { margin: 0; font-size: 1.02rem; line-height: 1.45; }
.tl-progress { font: 12px var(--font-mono, monospace); color: var(--text-secondary); white-space: nowrap; }
.tl-progress em { margin-left: 6px; padding: 1px 5px; font-style: normal; background: var(--success-soft, #e6f6ec); color: var(--success); }
.tl-progress em.down { background: var(--danger-soft, #fdecea); color: var(--danger); }
.tl-card .tl-body { margin-top: 8px; font-size: .94rem; }
.tl-card .tl-body :deep(p:last-child) { margin-bottom: 0; }
.intro-wrap .article-body { margin-top: 6px; }
.intro-wrap .article-body :deep(> :first-child) { margin-top: 0; }
.tl-body-wrap.collapsed { max-height: 260px; overflow: hidden; -webkit-mask-image: linear-gradient(#000 65%, transparent); mask-image: linear-gradient(#000 65%, transparent); }
.more-btn { display: block; width: 100%; margin-top: 4px; padding: 10px; border: 1px dashed var(--border-line); background: transparent; color: var(--text-secondary); font: inherit; font-size: .85rem; cursor: pointer; }
.more-btn:hover { color: var(--accent-color); border-color: var(--accent-color); }
@media (max-width: 600px) {
  .timeline::before { left: 8px; }
  .tl-item { grid-template-columns: 1fr; gap: 6px; padding-left: 26px; }
  .tl-item::before { left: 3px; }
  .tl-date { justify-items: start; justify-content: start; grid-auto-flow: column; gap: 8px; align-items: baseline; }
  .tl-date b { font-size: 1rem; }
}
.back { display: inline-block; margin: 20px 0 28px; color: var(--text-secondary); font-size: .85rem; text-decoration: none; }
.back:hover { color: var(--accent-color); }
.state { color: var(--text-secondary); text-align: center; padding: 60px 0; }

.article { color: var(--text-primary); }
.article-header { padding-bottom: 22px; margin-bottom: 8px; border-bottom: 2px solid var(--text-primary); }
.article-title { margin: 0; font-size: clamp(1.8rem, 4vw, 2.6rem); font-weight: 700; letter-spacing: -.02em; line-height: 1.25; }
.article-icon { margin-right: 8px; }
.article-meta { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; margin-top: 14px; font-size: .84rem; color: var(--text-secondary); }
.article-meta .dot { opacity: .5; }
.tag { font-size: .72rem; padding: 2px 10px; border-radius: 0; }
.tag.planning, .tag.paused { background: var(--bg-sub); color: var(--text-secondary); }
.tag.ongoing { background: var(--accent-soft); color: var(--accent-color); }
.tag.done { background: #e6f6ec; color: var(--success); }
.tag.dead { background: #fdecea; color: #c0392b; }

.article-abstract { margin: 22px 0 0; font-size: 1.05rem; line-height: 1.8; color: var(--text-secondary); }

.article-progress { margin: 28px 0; }
.progress-head { display: flex; justify-content: space-between; align-items: baseline; margin-bottom: 8px; }
.progress-label { font-size: .82rem; color: var(--text-secondary); letter-spacing: .04em; }
.progress-pct { font-size: 1.3rem; font-weight: 700; font-variant-numeric: tabular-nums; }
.progress-bar { height: 10px; background: var(--bg-sub); border-radius: 0; overflow: hidden; }
.progress-fill { display: block; height: 100%; background: linear-gradient(90deg, var(--accent-color), var(--success)); border-radius: 0; }

.article-milestones { margin: 28px 0; }
.section-title { font-size: 1.05rem; font-weight: 650; margin: 0 0 12px; letter-spacing: -.01em; }
.ms-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.ms-item { display: flex; align-items: center; gap: 10px; font-size: .92rem; color: var(--text-secondary); }
.ms-item.done { color: var(--success); }
.ms-item.done .ms-title { text-decoration: line-through; opacity: .75; }
.check { width: 18px; text-align: center; flex-shrink: 0; }

/* 正文 —— 报告/文章排版 */
.article-section { margin: 32px 0; }
.article-section .section-title { font-size: 1.05rem; margin: 0 0 16px; }
.ref-list { margin: 0; padding-left: 20px; display: grid; gap: 14px; }
.ref-item { line-height: 1.6; }
.ref-title { font-size: .92rem; font-weight: 600; color: var(--accent-color); text-decoration: none; }
.ref-title:hover { text-decoration: underline; }
.ref-meta { margin: 4px 0 0; font-size: .78rem; color: var(--text-secondary); }
.ref-note { margin: 6px 0 0; font-size: .82rem; color: var(--text-secondary); line-height: 1.7; white-space: pre-wrap; }
.data-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 180px), 1fr)); gap: 14px; }
.data-card { padding: 16px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); display: grid; gap: 8px; }
.data-name { font-size: .82rem; color: var(--text-secondary); }
.data-value { font-size: 1.6rem; font-weight: 700; color: var(--text-primary); font-variant-numeric: tabular-nums; }
.data-value small { font-size: .8rem; font-weight: 400; color: var(--text-secondary); margin-left: 4px; }
.data-note { margin: 0; font-size: .76rem; color: var(--text-secondary); line-height: 1.6; }
.log-list { list-style: none; margin: 0; padding: 0 0 0 18px; border-left: 2px solid var(--border-line); display: grid; gap: 20px; }
.log-item { position: relative; }
.log-head { display: flex; align-items: baseline; gap: 8px; flex-wrap: wrap; }
.log-dot { position: absolute; left: -25px; top: 6px; width: 10px; height: 10px; border-radius: 50%; background: var(--accent-color); }
.log-title { font-size: .95rem; font-weight: 600; }
.log-date { font-size: .75rem; color: var(--text-secondary); margin-left: auto; }
.log-content { margin: 8px 0 0; font-size: .88rem; line-height: 1.8; white-space: pre-wrap; color: var(--text-secondary); }
.log-images { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 10px; }
.log-images img { max-width: 220px; border-radius: 0; border: 1px solid var(--border-line); }
.article-body { margin-top: 28px; font-size: 1rem; line-height: 1.95; color: var(--text-primary); overflow-wrap: anywhere; }
.article-body :deep(p) { margin: 0 0 18px; }
.article-body :deep(h1) { font-size: 1.6rem; margin: 34px 0 14px; }
.article-body :deep(h2) { font-size: 1.35rem; margin: 30px 0 12px; padding-bottom: 8px; border-bottom: 1px solid var(--border-line); }
.article-body :deep(h3) { font-size: 1.15rem; margin: 24px 0 10px; }
.article-body :deep(h4) { font-size: 1.02rem; margin: 20px 0 8px; }
.article-body :deep(img) { max-width: 100%; border-radius: 0; display: block; margin: 18px auto; }
.article-body :deep(ul), .article-body :deep(ol) { margin: 0 0 18px; padding-left: 26px; }
.article-body :deep(li) { margin: 6px 0; }
.article-body :deep(a) { color: var(--accent-color); }
.article-body :deep(blockquote) { margin: 0 0 18px; padding: 12px 18px; border-left: 3px solid var(--accent-color); background: var(--bg-sub); border-radius: 0; color: var(--text-secondary); }
.article-body :deep(code) { background: var(--bg-sub); padding: 2px 6px; border-radius: 0; font-size: .88em; }
.article-body :deep(pre) { background: var(--bg-sub); padding: 14px; border-radius: 0; overflow-x: auto; line-height: 1.6; }
.article-body :deep(pre code) { background: none; padding: 0; }
.article-body :deep(hr) { border: none; border-top: 1px solid var(--border-line); margin: 28px 0; }
.article-body :deep(table) { border-collapse: collapse; width: 100%; margin: 0 0 18px; }
.article-body :deep(th), .article-body :deep(td) { border: 1px solid var(--border-line); padding: 8px 12px; text-align: left; }
.article-body :deep(th) { background: var(--bg-sub); }
</style>
