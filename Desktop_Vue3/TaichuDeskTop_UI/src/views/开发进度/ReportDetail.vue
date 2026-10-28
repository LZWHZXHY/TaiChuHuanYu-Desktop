<template>
  <div class="report-detail content-page">
    <RouterLink to="/dev-progress" class="back">← 返回研发报告</RouterLink>

    <p v-if="loading" class="state">加载中…</p>
    <p v-else-if="!project" class="state">报告不存在或已被删除</p>

    <article v-else class="article">
      <header class="article-header">
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

      <section v-if="project.logs?.length" class="article-section">
        <h2 class="section-title">🧪 研究日志</h2>
        <ol class="log-list">
          <li v-for="l in project.logs" :key="l.id" class="log-item">
            <div class="log-head">
              <span class="log-dot"></span>
              <span class="log-title">{{ l.title }}</span>
              <time v-if="l.date" class="log-date">{{ l.date }}</time>
            </div>
            <p v-if="l.content" class="log-content">{{ l.content }}</p>
            <div v-if="l.images?.length" class="log-images">
              <img v-for="(img, i) in l.images" :key="i" :src="img" alt="" loading="lazy" />
            </div>
          </li>
        </ol>
      </section>

      <div v-if="project.content" class="article-body" v-html="renderMarkdown(project.content)"></div>
    </article>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { devProjectApi, type DevProject } from '@/api/devProject'
import { renderMarkdown } from '@/utils/markdown'

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
    project.value = list.find(p => p.id === id) || null
  } catch {
    project.value = null
  } finally {
    loading.value = false
  }
}

onMounted(load)
watch(() => route.params.id, load)
</script>

<style scoped>
.report-detail { max-width: 820px; margin: 0 auto; padding: 0 0 64px; }
.back { display: inline-block; margin: 20px 0 28px; color: var(--text-secondary); font-size: .85rem; text-decoration: none; }
.back:hover { color: var(--accent-color); }
.state { color: var(--text-secondary); text-align: center; padding: 60px 0; }

.article { color: var(--text-primary); }
.article-header { padding-bottom: 22px; margin-bottom: 8px; border-bottom: 2px solid var(--text-primary); }
.article-title { margin: 0; font-size: clamp(1.8rem, 4vw, 2.6rem); font-weight: 700; letter-spacing: -.02em; line-height: 1.25; }
.article-icon { margin-right: 8px; }
.article-meta { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; margin-top: 14px; font-size: .84rem; color: var(--text-secondary); }
.article-meta .dot { opacity: .5; }
.tag { font-size: .72rem; padding: 2px 10px; border-radius: 10px; }
.tag.planning, .tag.paused { background: var(--bg-sub); color: var(--text-secondary); }
.tag.ongoing { background: var(--accent-soft); color: var(--accent-color); }
.tag.done { background: #e6f6ec; color: var(--success); }
.tag.dead { background: #fdecea; color: #c0392b; }

.article-abstract { margin: 22px 0 0; font-size: 1.05rem; line-height: 1.8; color: var(--text-secondary); }

.article-progress { margin: 28px 0; }
.progress-head { display: flex; justify-content: space-between; align-items: baseline; margin-bottom: 8px; }
.progress-label { font-size: .82rem; color: var(--text-secondary); letter-spacing: .04em; }
.progress-pct { font-size: 1.3rem; font-weight: 700; font-variant-numeric: tabular-nums; }
.progress-bar { height: 10px; background: var(--bg-sub); border-radius: 5px; overflow: hidden; }
.progress-fill { display: block; height: 100%; background: linear-gradient(90deg, var(--accent-color), var(--success)); border-radius: 5px; }

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
.data-card { padding: 16px; border: 1px solid var(--border-line); border-radius: 10px; background: var(--paper-card); display: grid; gap: 8px; }
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
.log-images img { max-width: 220px; border-radius: 8px; border: 1px solid var(--border-line); }
.article-body { margin-top: 28px; font-size: 1rem; line-height: 1.95; color: var(--text-primary); overflow-wrap: anywhere; }
.article-body :deep(p) { margin: 0 0 18px; }
.article-body :deep(h1) { font-size: 1.6rem; margin: 34px 0 14px; }
.article-body :deep(h2) { font-size: 1.35rem; margin: 30px 0 12px; padding-bottom: 8px; border-bottom: 1px solid var(--border-line); }
.article-body :deep(h3) { font-size: 1.15rem; margin: 24px 0 10px; }
.article-body :deep(h4) { font-size: 1.02rem; margin: 20px 0 8px; }
.article-body :deep(img) { max-width: 100%; border-radius: 10px; display: block; margin: 18px auto; }
.article-body :deep(ul), .article-body :deep(ol) { margin: 0 0 18px; padding-left: 26px; }
.article-body :deep(li) { margin: 6px 0; }
.article-body :deep(a) { color: var(--accent-color); }
.article-body :deep(blockquote) { margin: 0 0 18px; padding: 12px 18px; border-left: 3px solid var(--accent-color); background: var(--bg-sub); border-radius: 0 8px 8px 0; color: var(--text-secondary); }
.article-body :deep(code) { background: var(--bg-sub); padding: 2px 6px; border-radius: 4px; font-size: .88em; }
.article-body :deep(pre) { background: var(--bg-sub); padding: 14px; border-radius: 10px; overflow-x: auto; line-height: 1.6; }
.article-body :deep(pre code) { background: none; padding: 0; }
.article-body :deep(hr) { border: none; border-top: 1px solid var(--border-line); margin: 28px 0; }
.article-body :deep(table) { border-collapse: collapse; width: 100%; margin: 0 0 18px; }
.article-body :deep(th), .article-body :deep(td) { border: 1px solid var(--border-line); padding: 8px 12px; text-align: left; }
.article-body :deep(th) { background: var(--bg-sub); }
</style>
