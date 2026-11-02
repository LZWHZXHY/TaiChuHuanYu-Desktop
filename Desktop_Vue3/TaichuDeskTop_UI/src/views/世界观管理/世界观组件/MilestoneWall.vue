<!-- 创作里程碑墙：第 N 个词条、设定 N 万字、连续创作 N 天、建立满 N 年……
     达成的点亮、写上达成日期；没达成的灰着，下面一条小进度条，写「还差多少」。
     公开世界的访客只看到已经达成的（后端只给已达成的）。 -->
<template>
  <section class="mw" aria-labelledby="mw-title">
    <h3 id="mw-title">
      创作里程碑
      <small v-if="publicView">已点亮 {{ litCount }} 个</small>
      <small v-else>已点亮 {{ litCount }} / {{ milestones.length }}</small>
    </h3>
    <p v-if="!groups.length" class="mw-empty">{{ publicView ? '这个世界还没有点亮的里程碑。' : '还没有里程碑。' }}</p>
    <div v-for="g in groups" :key="g.kind" class="mw-group">
      <h4>{{ g.label }}</h4>
      <ul class="mw-list" :aria-label="g.label">
        <li
          v-for="m in g.items" :key="m.key"
          class="mw-badge" :class="{ on: m.achieved, next: !m.achieved && m.key === g.nextKey }"
          :title="hint(m)"
        >
          <b class="mw-label">{{ m.label }}</b>
          <span class="mw-name">{{ m.title }}</span>
          <small class="mw-sub">
            <span class="sr-only">{{ m.achieved ? '已达成：' : '未达成：' }}</span>{{ sub(m) }}
          </small>
          <span v-if="!m.achieved" class="mw-bar" aria-hidden="true"><i :style="{ width: Math.round(m.progress * 100) + '%' }"></i></span>
        </li>
      </ul>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { MilestoneKind, WorldMilestone } from '@/api/worldStats'

const props = defineProps<{
  milestones: WorldMilestone[]
  /** 访客看的公开统计：只有已达成的，不显示进度 */
  publicView?: boolean
}>()

const KINDS: { kind: MilestoneKind; label: string }[] = [
  { kind: 'cards', label: '词条' },
  { kind: 'words', label: '设定字数' },
  { kind: 'novel', label: '小说字数' },
  { kind: 'streak', label: '连续创作' },
  { kind: 'active', label: '累计创作' },
  { kind: 'age', label: '世界年龄' },
]

const litCount = computed(() => props.milestones.filter(m => m.achieved).length)

/** 按种类分组；每组里第一个没达成的是「下一个目标」，稍微亮一点 */
const groups = computed(() => KINDS.map(k => {
  const items = props.milestones.filter(m => m.kind === k.kind)
  return { ...k, items, nextKey: items.find(m => !m.achieved)?.key ?? '' }
}).filter(g => g.items.length > 0
  // 这个世界还没写小说：小说那一行全是灰的没意义，先不显示
  && !(g.kind === 'novel' && !g.items.some(m => m.achieved || m.progress > 0))))

const fmt = (n: number) => Number(n || 0).toLocaleString('zh-CN')
/** 一万以上用「万」 */
const fmtWords = (n: number) => n >= 10000 ? `${(n / 10000).toFixed(n >= 100000 ? 0 : 1)} 万` : fmt(n)
const day = (v: string) => new Date(v).toLocaleDateString('zh-CN', { year: 'numeric', month: 'numeric', day: 'numeric' })

/** 徽章下面的小字：达成日期，或者还差多少 */
function sub(m: WorldMilestone) {
  if (m.achieved) {
    if (m.pending) return '刚刚达成'
    if (m.achievedAt) return `${day(m.achievedAt)} 达成`
    return '上线前已达成'
  }
  const r = m.remaining
  if (m.kind === 'cards') return `还差 ${fmt(r)} 个词条`
  if (m.kind === 'words' || m.kind === 'novel') return `还差 ${fmtWords(r)} 字`
  if (m.kind === 'streak') return `再连续写 ${fmt(r)} 天`
  if (m.kind === 'age') return `还有 ${fmt(r)} 天`
  return `还差 ${fmt(r)} 天`
}

/** 悬停说明：补记的日期是推算的，说清楚 */
function hint(m: WorldMilestone) {
  if (!m.achieved) return m.kind === 'streak' ? `${m.title}：到今天已经连续写了 ${fmt(m.threshold - m.remaining)} 天（今天还没写不算断）` : m.title
  if (m.backfilled) return `${m.title}：里程碑功能上线前就达成了${m.achievedAt ? '，日期是按记录推算的' : ''}`
  return m.title
}
</script>

<style scoped>
.mw { padding: 16px 18px; background: var(--paper-card); border: 1px solid var(--border-line); min-width: 0; }
.mw h3 { display: flex; align-items: baseline; gap: 8px; margin: 0 0 4px; font-size: 15px; }
.mw h3 small { font-size: 12px; font-weight: 400; color: var(--text-secondary); }
.mw-empty { margin: 8px 0 0; font-size: 13px; color: var(--text-secondary); }
.mw-group { display: grid; grid-template-columns: 72px minmax(0, 1fr); gap: 10px; align-items: start; padding: 12px 0; border-top: 1px dashed var(--border-line); }
.mw-group:first-of-type { border-top: 0; }
.mw-group h4 { margin: 10px 0 0; font-size: 12px; font-weight: 500; color: var(--text-secondary); }
.mw-list { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(118px, 1fr)); gap: 8px; }

/* 一块徽章：没达成的灰着（虚线框、文字变淡），达成的点亮（主题色上边框 + 浅底） */
.mw-badge { position: relative; display: grid; gap: 2px; padding: 10px 12px 12px; border: 1px dashed var(--border-line); background: var(--bg-sub); color: var(--text-secondary); min-width: 0; }
.mw-badge .mw-label { font-size: 20px; line-height: 1.15; font-weight: 700; font-variant-numeric: tabular-nums; opacity: .55; }
.mw-badge .mw-name { font-size: 12px; opacity: .8; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.mw-badge .mw-sub { font-size: 11px; line-height: 1.4; }
.mw-badge.next { border-style: solid; }
.mw-badge.next .mw-label { opacity: .8; }
.mw-badge.on { border: 1px solid var(--accent-color); border-top-width: 3px; padding-top: 8px; background: var(--accent-soft); color: var(--text-primary); }
.mw-badge.on .mw-label { color: var(--accent-color); opacity: 1; }
.mw-badge.on .mw-name { opacity: 1; font-weight: 500; }
.mw-badge.on .mw-sub { color: var(--text-secondary); }

.mw-bar { position: absolute; left: 0; right: 0; bottom: 0; height: 3px; background: color-mix(in srgb, var(--border-line) 70%, transparent); }
.mw-bar i { display: block; height: 100%; background: color-mix(in srgb, var(--accent-color) 60%, var(--text-secondary)); }

.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }

@media (max-width: 560px) {
  .mw-group { grid-template-columns: 1fr; gap: 6px; }
  .mw-group h4 { margin: 0; }
}
</style>
