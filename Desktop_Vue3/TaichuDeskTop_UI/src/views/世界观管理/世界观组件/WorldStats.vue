<!-- 世界观创作统计：词条树、字数、创作天数、连续创作、活跃日历、几点写、谁写得最多、改得最多的条目、创作里程碑。
     创建者和协作者看完整统计；公开世界的访客（包括没登录的）看公开的那部分：
     只算访客能看到的内容，不显示是谁写的、不显示修改记录（后端按身份裁剪，前端只管按 mode 显示）。 -->
<template>
  <section class="ws" aria-label="创作统计">
    <header class="ws-head">
      <div>
        <p class="ws-kicker">CREATION STATS · 创作统计{{ pub ? ' · 公开' : '' }}</p>
        <h2>{{ data ? `《${data.name}》的创作档案` : '创作统计' }}</h2>
        <p v-if="data" class="ws-sub">建立于 {{ day(data.createdAt) }} · 今天是第 <b>{{ data.days.toLocaleString('zh-CN') }}</b> 天{{ data.totals.lastActiveAt ? ` · ${pub ? '最近更新' : '最近一次创作'} ${ago(data.totals.lastActiveAt)}` : '' }}</p>
      </div>
      <button type="button" class="ws-refresh" :disabled="loading" @click="load">{{ loading ? '读取中…' : '刷新' }}</button>
    </header>

    <p v-if="loading && !data" class="ws-state" role="status">正在统计这个世界的创作数据…</p>
    <p v-else-if="error && !data" class="ws-state" role="alert">{{ error }} <button type="button" @click="load">重试</button></p>

    <template v-else-if="stats">
      <!-- 关键数字 -->
      <div class="ws-tiles">
        <div class="ws-tile">
          <span class="ws-label">词条</span>
          <b class="ws-num">{{ fmt(stats.totals.cards) }}</b>
          <small>{{ stats.tree.length }} 种类型 · {{ fmt(stats.totals.relations) }} 条关系</small>
        </div>
        <div class="ws-tile">
          <span class="ws-label">设定字数</span>
          <b class="ws-num">{{ fmtWords(stats.totals.words) }}</b>
          <small>{{ stats.totals.novels ? `另有小说 ${fmtWords(stats.totals.novelWords)} 字` : `平均每个词条 ${fmt(avgWords)} 字` }}</small>
        </div>
        <div class="ws-tile">
          <span class="ws-label">创作天数</span>
          <b class="ws-num">{{ fmt(stats.totals.activeDays) }}<em> 天</em></b>
          <small>建立以来的 {{ fmt(stats.days) }} 天里，{{ activeRate }}% 的日子在写</small>
        </div>
        <div class="ws-tile">
          <span class="ws-label">连续创作</span>
          <b class="ws-num">{{ fmt(stats.totals.currentStreak) }}<em> 天</em></b>
          <small>最长连续 {{ fmt(stats.totals.longestStreak) }} 天{{ stats.totals.currentStreak && stats.totals.currentStreak === stats.totals.longestStreak ? ' · 正在刷新纪录' : '' }}</small>
        </div>
        <div class="ws-tile">
          <span class="ws-label">修改次数</span>
          <b class="ws-num">{{ fmt(stats.totals.edits) }}</b>
          <small>新建 {{ fmt(stats.totals.creates) }} · 修改 {{ fmt(stats.totals.updates) }}{{ stats.totals.deletes ? ` · 删除 ${fmt(stats.totals.deletes)}` : '' }}</small>
        </div>
        <div class="ws-tile">
          <span class="ws-label">最近活跃</span>
          <b class="ws-num">{{ fmt(stats.totals.last7) }}<em> 次</em></b>
          <small>最近 7 天 · 最近 30 天 {{ fmt(stats.totals.last30) }} 次</small>
        </div>
      </div>

      <!-- 活跃日历 -->
      <section class="ws-card">
        <h3>活跃日历</h3>
        <CreationHeatmap :days="stats.heatmap" />
      </section>

      <!-- 创作里程碑：达成的点亮，没达成的灰着显示还差多少（老缓存里没有这一项就先不显示） -->
      <MilestoneWall v-if="stats.milestones?.length" :milestones="stats.milestones ?? []" />

      <div class="ws-cols">
        <!-- 词条树 -->
        <section class="ws-card">
          <h3>词条树 <small>{{ fmt(stats.totals.cards) }} 个词条 · {{ fmtWords(stats.totals.words) }} 字</small></h3>
          <p v-if="!stats.tree.length" class="ws-empty">还没有词条。</p>
          <ul class="ws-tree">
            <li v-for="node in stats.tree" :key="node.type">
              <button type="button" class="ws-tree-row" :aria-expanded="open[node.type] ? 'true' : 'false'" @click="open[node.type] = !open[node.type]">
                <span class="ws-chev" aria-hidden="true">{{ open[node.type] ? '▾' : '▸' }}</span>
                <span class="ws-tree-name">{{ typeLabel(node.type) }}</span>
                <span class="ws-tree-bar" aria-hidden="true"><i :style="{ width: (node.count / maxTypeCount) * 100 + '%' }"></i></span>
                <span class="ws-tree-count">{{ node.count }}</span>
                <span class="ws-tree-words">{{ fmtWords(node.words) }} 字</span>
              </button>
              <div v-if="open[node.type]" class="ws-tree-children">
                <p v-if="node.subTypes.length" class="ws-subtypes">
                  <span v-for="s in node.subTypes" :key="s.name">{{ s.name }} <b>{{ s.count }}</b></span>
                </p>
                <ol class="ws-tree-cards">
                  <li v-for="c in node.cards" :key="c.id">
                    <button type="button" @click="emit('select', c.id)">{{ c.title || '未命名' }}</button>
                    <small v-if="c.subType">{{ c.subType }}</small>
                    <span>{{ fmtWords(c.words) }} 字</span>
                  </li>
                </ol>
              </div>
            </li>
          </ul>
          <p class="ws-foot">另有 {{ fmt(stats.totals.timelineEvents) }} 个时间线事件、{{ fmt(stats.totals.images) }} 张图片 / 地图</p>
        </section>

        <!-- 什么时候写 -->
        <section class="ws-card">
          <h3>什么时候写</h3>
          <p v-if="habit" class="ws-insight">{{ habit }}</p>
          <h4>一天中的时段（北京时间）</h4>
          <StatBars title="一天中的时段" :values="stats.hours" :labels="HOUR_LABELS" :short="hourShort" :tick-every="6" />
          <h4>一周里的哪天</h4>
          <StatBars title="一周里的哪天" :values="stats.weekdays" :labels="WEEK_LABELS" :short="i => WEEK_SHORT[i] ?? ''" />
        </section>
      </div>

      <div class="ws-cols">
        <!-- 协作者 -->
        <section v-if="stats.contributors.length" class="ws-card">
          <h3>谁在写 <small>{{ stats.contributors.length }} 人</small></h3>
          <ul class="ws-people">
            <li v-for="p in stats.contributors" :key="p.userId">
              <img v-if="p.avatar" :src="p.avatar" alt="" class="ws-avatar" />
              <span v-else class="ws-avatar ws-avatar-text" aria-hidden="true">{{ (p.username || '?').slice(0, 1) }}</span>
              <div class="ws-person">
                <b>{{ p.username || '已注销用户' }}<em v-if="p.isOwner">创建者</em></b>
                <small>新建 {{ fmt(p.creates) }} · 共 {{ fmt(p.edits) }} 次 · 活跃 {{ fmt(p.activeDays) }} 天 · 最近 {{ ago(p.lastAt) }}</small>
              </div>
              <span class="ws-share" :title="`占全部修改的 ${share(p.edits)}%`">{{ share(p.edits) }}%</span>
            </li>
          </ul>
        </section>

        <!-- 改得最多 / 写得最长 -->
        <section class="ws-card">
          <h3>改得最多的词条</h3>
          <p v-if="!stats.topEdited.length" class="ws-empty">还没有修改记录。</p>
          <ol class="ws-rank">
            <li v-for="c in stats.topEdited" :key="c.id">
              <button v-if="c.exists" type="button" @click="emit('select', c.id)">{{ c.title || '未命名' }}</button>
              <span v-else class="ws-gone">{{ c.title || '未命名' }}（已删除）</span>
              <span>{{ c.count }} 次</span>
            </li>
          </ol>
          <h3 class="ws-second">写得最长的词条</h3>
          <ol class="ws-rank">
            <li v-for="c in stats.longest" :key="c.id">
              <button type="button" @click="emit('select', c.id)">{{ c.title || '未命名' }}</button>
              <span>{{ fmtWords(c.words) }} 字</span>
            </li>
          </ol>
        </section>
      </div>

      <p class="ws-note">
        字数只算设定文字（正文、属性、文本块、时间线事件），空格和引用标记不算。
        修改次数{{ stats.trackingSince ? `从 ${day(stats.trackingSince)} 开始记录` : '从开始记录修改后才有' }}，在这之前建的词条按建立时间算一次「新建」。日期按北京时间。
        里程碑每 30 分钟检测一次，新达成的会给创建者和协作者发通知。
      </p>
    </template>

    <!-- 公开世界的访客：只看公开的那部分（没有各人贡献、修改次数、改得最多这些） -->
    <template v-else-if="pub">
      <div class="ws-tiles">
        <div class="ws-tile">
          <span class="ws-label">词条</span>
          <b class="ws-num">{{ fmt(pub.totals.cards) }}</b>
          <small>{{ pub.milestones.length ? `已点亮 ${pub.milestones.length} 个里程碑` : '还没有点亮里程碑' }}</small>
        </div>
        <div class="ws-tile">
          <span class="ws-label">设定字数</span>
          <b class="ws-num">{{ fmtWords(pub.totals.words) }}</b>
          <small>{{ pub.totals.novels ? `另有公开小说 ${fmtWords(pub.totals.novelWords)} 字` : '只算公开的设定内容' }}</small>
        </div>
        <div class="ws-tile">
          <span class="ws-label">创作天数</span>
          <b class="ws-num">{{ fmt(pub.totals.activeDays) }}<em> 天</em></b>
          <small>建立以来的 {{ fmt(pub.days) }} 天里，{{ activeRate }}% 的日子在写</small>
        </div>
        <div class="ws-tile">
          <span class="ws-label">最近更新</span>
          <b class="ws-num ws-num-sm">{{ pub.totals.lastActiveAt ? ago(pub.totals.lastActiveAt) : '—' }}</b>
          <small>{{ pub.totals.lastActiveAt ? day(pub.totals.lastActiveAt) : '还没有更新过' }}</small>
        </div>
      </div>

      <section class="ws-card">
        <h3>活跃日历</h3>
        <CreationHeatmap :days="pub.heatmap" />
      </section>

      <MilestoneWall :milestones="pub.milestones" public-view />

      <p class="ws-note">
        这是公开的创作统计：只算访客能看到的内容（作者设成「对访客隐藏」的区块和时间线事件不算，小说只算公开的），
        活跃日历只显示每天创作了几次。字数不算空格和引用标记，日期按北京时间。统计每 10 分钟更新一次{{ pub.generatedAt ? `（这份是 ${ago(pub.generatedAt)}算的）` : '' }}。
      </p>
    </template>
  </section>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { worldStatsApi, type PublicWorldStats, type WorldStats } from '@/api/worldStats'
import { getErrorMessage } from '@/utils/apiError'
import { getCardTypeLabel } from '@/utils/worldPresentation'
import CreationHeatmap from '@/components/ActivityHeatmap.vue'
import StatBars from './StatBars.vue'
import MilestoneWall from './MilestoneWall.vue'
import { useUserStore } from '@/stores/user'

const props = defineProps<{ projectId: string }>()
const emit = defineEmits<{ select: [cardId: string] }>()

const userStore = useUserStore()
/** 后端给的统计：成员拿到完整的（mode = member），公开世界的访客拿到公开的那部分（mode = public） */
const data = ref<WorldStats | PublicWorldStats | null>(null)
/** 成员看的完整统计（老缓存里没有 mode，也算成员版） */
const stats = computed<WorldStats | null>(() => data.value && data.value.mode !== 'public' ? data.value : null)
/** 访客看的公开统计 */
const pub = computed<PublicWorldStats | null>(() => data.value?.mode === 'public' ? data.value : null)
const loading = ref(false)
const error = ref('')
const open = reactive<Record<string, boolean>>({})
let version = 0

// 上次的结果存在浏览器里，进来先显示，后台再换成最新的。
// 按账号分开存：不然同一台电脑上，退出登录后访客会先看到成员版（含各人贡献）的旧缓存
const cacheKey = () => `taichu.world.stats:${userStore.userId || 'guest'}:${props.projectId}`
async function load() {
  const v = ++version
  loading.value = true
  error.value = ''
  // 以前的缓存不分账号，顺手删掉
  try { localStorage.removeItem(`taichu.world.stats:${props.projectId}`) } catch { /* 忽略 */ }
  if (!data.value) { try { const raw = localStorage.getItem(cacheKey()); if (raw) data.value = JSON.parse(raw) as WorldStats | PublicWorldStats } catch { /* 忽略 */ } }
  try {
    const fresh = await worldStatsApi.project(props.projectId)
    if (v !== version) return
    data.value = fresh
    try { localStorage.setItem(cacheKey(), JSON.stringify(fresh)) } catch { /* 存不下就算了 */ }
  } catch (e) {
    if (v === version) error.value = getErrorMessage(e, '创作统计读取失败')
  } finally { if (v === version) loading.value = false }
}
// 换世界、登录 / 退出（身份变了，看到的版本也不一样）都重新读
watch([() => props.projectId, () => userStore.userId], () => { data.value = null; void load() }, { immediate: true })

const fmt = (n: number) => Number(n || 0).toLocaleString('zh-CN')
/** 一万以上用「万」 */
const fmtWords = (n: number) => n >= 10000 ? `${(n / 10000).toFixed(n >= 100000 ? 0 : 1)} 万` : fmt(n)
const day = (v: string) => new Date(v).toLocaleDateString('zh-CN', { year: 'numeric', month: 'long', day: 'numeric' })
function ago(v: string) {
  const diff = Date.now() - new Date(v).getTime()
  if (diff < 3600000) return `${Math.max(1, Math.floor(diff / 60000))} 分钟前`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)} 小时前`
  if (diff < 30 * 86400000) return `${Math.floor(diff / 86400000)} 天前`
  return new Date(v).toLocaleDateString('zh-CN')
}
const typeLabel = (t: string) => t === '未分类' ? t : getCardTypeLabel(t)
const maxTypeCount = computed(() => Math.max(1, ...(stats.value?.tree.map(t => t.count) ?? [1])))
const avgWords = computed(() => stats.value?.totals.cards ? Math.round(stats.value.totals.words / stats.value.totals.cards) : 0)
const activeRate = computed(() => data.value?.days ? Math.min(100, Math.round((data.value.totals.activeDays / data.value.days) * 100)) : 0)
const share = (n: number) => {
  const total = stats.value?.contributors.reduce((s, p) => s + p.edits, 0) || 1
  return Math.round((n / total) * 100)
}

const HOUR_LABELS = Array.from({ length: 24 }, (_, h) => `${h}:00 – ${h}:59`)
const hourShort = (i: number) => `${i}点`
const WEEK_LABELS = ['周一', '周二', '周三', '周四', '周五', '周六', '周日']
const WEEK_SHORT = ['一', '二', '三', '四', '五', '六', '日']

/** 一句话总结创作习惯 */
const habit = computed(() => {
  const s = stats.value
  if (!s) return ''
  const total = s.hours.reduce((a, b) => a + b, 0)
  if (total < 5) return ''
  const peakHour = s.hours.indexOf(Math.max(...s.hours))
  const peakDay = s.weekdays.indexOf(Math.max(...s.weekdays))
  const night = s.hours.slice(22).concat(s.hours.slice(0, 5)).reduce((a, b) => a + b, 0) / total
  const period = peakHour < 5 ? '深夜' : peakHour < 9 ? '清晨' : peakHour < 12 ? '上午' : peakHour < 14 ? '中午' : peakHour < 18 ? '下午' : peakHour < 22 ? '晚上' : '深夜'
  return `最常在${WEEK_LABELS[peakDay]}的${period} ${peakHour} 点创作${night >= 0.3 ? '，是个夜猫子世界' : ''}。`
})
</script>

<style scoped>
.ws { display: grid; gap: 18px; padding: 4px 0 40px; color: var(--text-primary); }
.ws > * { min-width: 0; }
.ws-head { display: flex; flex-wrap: wrap; align-items: flex-end; justify-content: space-between; gap: 12px; padding-bottom: 12px; border-bottom: 1px solid var(--border-line); }
.ws-kicker { margin: 0 0 4px; font-size: 11px; letter-spacing: .14em; color: var(--accent-color); }
.ws-head h2 { margin: 0 0 6px; font-size: clamp(20px, 3vw, 26px); }
.ws-sub { margin: 0; font-size: 13px; color: var(--text-secondary); }
.ws-sub b { color: var(--text-primary); }
.ws-refresh, .ws-state button { padding: 6px 14px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.ws-state { padding: 40px 0; text-align: center; color: var(--text-secondary); }

.ws-tiles { display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 10px; }
.ws-tile { display: grid; gap: 4px; padding: 14px 16px; background: var(--paper-card); border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); min-width: 0; }
.ws-label { font-size: 12px; color: var(--text-secondary); }
.ws-num { font-size: 28px; line-height: 1.1; font-weight: 700; font-variant-numeric: tabular-nums; }
.ws-num-sm { font-size: 20px; line-height: 1.4; }
.ws-num em { font-style: normal; font-size: 14px; font-weight: 500; color: var(--text-secondary); }
.ws-tile small { font-size: 12px; line-height: 1.5; color: var(--text-secondary); }

.ws-card { padding: 16px 18px; background: var(--paper-card); border: 1px solid var(--border-line); min-width: 0; }
.ws-card h3 { display: flex; align-items: baseline; gap: 8px; margin: 0 0 12px; font-size: 15px; }
.ws-card h3 small { font-size: 12px; font-weight: 400; color: var(--text-secondary); }
.ws-card h4 { margin: 16px 0 8px; font-size: 12px; font-weight: 500; color: var(--text-secondary); }
.ws-second { margin-top: 20px !important; }
.ws-cols { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 18px; align-items: start; }
.ws-empty, .ws-foot { margin: 0; font-size: 13px; color: var(--text-secondary); }
.ws-foot { margin-top: 12px; font-size: 12px; }
.ws-insight { margin: 0; padding: 8px 12px; font-size: 13px; background: var(--bg-sub); border-left: 3px solid var(--accent-color); }

.ws-tree { list-style: none; margin: 0; padding: 0; display: grid; gap: 2px; }
.ws-tree-row { width: 100%; display: grid; grid-template-columns: 14px minmax(64px, 120px) minmax(0, 1fr) 34px 64px; align-items: center; gap: 8px; padding: 6px 4px; border: 0; background: none; color: inherit; font: inherit; font-size: 13px; text-align: left; cursor: pointer; }
.ws-tree-row:hover { background: var(--bg-sub); }
.ws-chev { color: var(--text-secondary); font-size: 11px; }
.ws-tree-name { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 500; }
.ws-tree-bar { height: 8px; }
.ws-tree-bar i { display: block; height: 100%; min-width: 3px; border-radius: 0 4px 4px 0; background: var(--accent-color); }
.ws-tree-count { text-align: right; font-weight: 600; font-variant-numeric: tabular-nums; }
.ws-tree-words { text-align: right; font-size: 12px; color: var(--text-secondary); font-variant-numeric: tabular-nums; }
.ws-tree-children { padding: 4px 0 10px 22px; }
.ws-subtypes { display: flex; flex-wrap: wrap; gap: 6px; margin: 0 0 8px; }
.ws-subtypes span { padding: 1px 8px; font-size: 12px; background: var(--bg-sub); }
.ws-subtypes b { font-weight: 600; margin-left: 2px; }
.ws-tree-cards { margin: 0; padding: 0 0 0 18px; display: grid; gap: 3px; max-height: 260px; overflow: auto; font-size: 13px; }
.ws-tree-cards li { display: flex; align-items: baseline; gap: 8px; }
.ws-tree-cards small { font-size: 11px; color: var(--text-secondary); }
.ws-tree-cards span { margin-left: auto; padding-right: 6px; font-size: 12px; color: var(--text-secondary); font-variant-numeric: tabular-nums; }
.ws-tree-cards button, .ws-rank button { padding: 0; border: 0; background: none; color: var(--accent-color); font: inherit; text-align: left; cursor: pointer; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.ws-tree-cards button:hover, .ws-rank button:hover { text-decoration: underline; }

.ws-people { list-style: none; margin: 0; padding: 0; display: grid; gap: 10px; }
.ws-people li { display: flex; align-items: center; gap: 10px; }
.ws-avatar { width: 34px; height: 34px; flex-shrink: 0; border-radius: 50%; object-fit: cover; }
.ws-avatar-text { display: grid; place-items: center; font-size: 14px; font-weight: 600; color: var(--on-accent); background: var(--accent-color); }
.ws-person { display: grid; min-width: 0; flex: 1; }
.ws-person b { font-size: 14px; }
.ws-person em { margin-left: 6px; padding: 0 6px; font-style: normal; font-size: 11px; font-weight: 500; color: var(--accent-color); border: 1px solid var(--accent-color); }
.ws-person small { font-size: 12px; color: var(--text-secondary); }
.ws-share { font-size: 13px; font-weight: 600; font-variant-numeric: tabular-nums; }

.ws-rank { margin: 0; padding: 0 0 0 20px; display: grid; gap: 4px; font-size: 13px; }
.ws-rank li { display: flex; align-items: baseline; gap: 8px; }
.ws-rank li > span:last-child { margin-left: auto; font-size: 12px; color: var(--text-secondary); font-variant-numeric: tabular-nums; }
.ws-gone { color: var(--text-secondary); }
.ws-note { margin: 0; font-size: 12px; line-height: 1.7; color: var(--text-secondary); }

@media (max-width: 860px) {
  .ws-cols { grid-template-columns: 1fr; }
  .ws-tree-row { grid-template-columns: 14px minmax(56px, 90px) minmax(0, 1fr) 30px 56px; }
}
</style>
