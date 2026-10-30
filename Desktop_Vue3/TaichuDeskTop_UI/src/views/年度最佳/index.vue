<script setup lang="ts">
import SectionTrace from '@/components/SectionTrace.vue'
import ParticleMorph from '@/components/ParticleMorph.vue'
import UserLink from '@/components/UserLink.vue'
import ParticleState from '@/components/ParticleState.vue'
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { awardsApi, type AwardCategory, type AwardEntry, type AwardsHall } from '@/api/awards'
import { cubeShape, starShape, trophyShape, type ParticleShape } from '@/utils/particleShapes'

const route = useRoute(), router = useRouter()
const hall = ref<AwardsHall | null>(null)
const loading = ref(true), error = ref('')
const year = ref<number | null>(null)
const category = ref<string>('all')

async function load() {
  loading.value = true; error.value = ''
  try {
    hall.value = await awardsApi.get()
    const wanted = Number(route.query.year)
    year.value = hall.value.years.some(y => y.year === wanted) ? wanted : hall.value.years[0]?.year ?? null
  } catch { error.value = '展览暂时没能加载，请稍后再试。' }
  finally { loading.value = false }
}
onMounted(load)

watch(year, value => {
  if (value && String(route.query.year) !== String(value)) router.replace({ query: { ...route.query, year: String(value) } })
})

const current = computed(() => hall.value?.years.find(y => y.year === year.value) ?? null)
const categories = computed<AwardCategory[]>(() => hall.value?.categories ?? [])
const countOf = (key: string) => current.value?.entries.filter(e => e.category === key).length ?? 0
const sections = computed(() => categories.value
  .filter(c => category.value === 'all' || c.key === category.value)
  .map((c, index) => {
    const entries = (current.value?.entries ?? []).filter(e => e.category === c.key).slice().sort((a, b) => a.rank - b.rank)
    return { ...c, index, featured: entries[0], others: entries.slice(1) }
  })
  .filter(s => s.featured))
const prizes = computed(() => (current.value?.entries ?? []).filter(e => e.prize).slice().sort((a, b) => a.category.localeCompare(b.category) || a.rank - b.rank))
const categoryName = (key: string) => categories.value.find(c => c.key === key)?.name ?? key

// 顶部粒子：奖杯 → 年份 → 方块（我的世界）→ 星。
const heroShapes = computed<ParticleShape[]>(() => [
  { kind: 'draw', draw: trophyShape },
  { kind: 'text', text: String(year.value ?? new Date().getFullYear()), mono: true, scale: 0.4 },
  { kind: 'draw', draw: cubeShape },
  { kind: 'draw', draw: starShape },
])

const medal = (rank: number) => rank === 1 ? 'gold' : rank === 2 ? 'silver' : rank === 3 ? 'bronze' : 'plain'
const isInternal = (link?: string | null) => Boolean(link && link.startsWith('/'))
// 颁奖日期按 yyyy-MM-dd 直接拆开显示，不经过时区换算（否则可能差一天）。
const formatDate = (value?: string | null) => {
  if (!value) return ''
  const m = /^(\d{4})-(\d{1,2})-(\d{1,2})/.exec(value)
  return m ? `${m[1]} 年 ${Number(m[2])} 月 ${Number(m[3])} 日` : value
}
const hide = (event: Event) => { (event.target as HTMLImageElement).hidden = true }
const entryKey = (e: AwardEntry) => e.id
</script>

<template>
  <article class="awards-page content-page community-surface tone-gallery">
    <header class="awards-hero content-header">
      <SectionTrace />
      <div class="hero-text">
        <span class="eyebrow">ANNUAL AWARDS · 年度最佳</span>
        <h1>年度最佳作品展</h1>
        <p>每一年，我们把最打动社区的作品放进这间展厅，并为创作者送上一份奖品。这里记录着每一届的获奖作品，以及它们被选中的理由。</p>
        <div v-if="hall?.years.length" class="year-switch" role="tablist" aria-label="选择年份">
          <button v-for="y in hall.years" :key="y.year" type="button" role="tab" :aria-selected="year === y.year" :class="{ active: year === y.year }" @click="year = y.year">
            {{ y.year }}<small v-if="!y.published">未公开</small>
          </button>
        </div>
      </div>
      <div class="hero-plate" data-parallax="0.08" aria-hidden="true">
        <ParticleMorph class="plate-particles" :shapes="heroShapes" :interval="4600" />
        <span class="plate-code">AWD-{{ year ?? '----' }}</span>
        <span class="plate-corner">BEST OF THE YEAR</span>
      </div>
    </header>

    <ParticleState v-if="loading" class="content-state" text="正在布置展厅" />
    <div v-else-if="error" class="content-state" role="alert"><p>{{ error }}</p><button class="btn-line" @click="load">重新加载</button></div>
    <div v-else-if="!current" class="content-state">
      <ParticleState kind="empty" preset="awards" compact />
      <h2>首届年度最佳正在筹备中</h2>
      <p>评选结果公布后，获奖作品和奖品会陈列在这里。</p>
      <RouterLink v-if="hall?.canEdit" class="btn-line" to="/admin?tab=awards">去后台添加年度</RouterLink>
    </div>

    <template v-else>
      <section class="year-banner">
        <div>
          <p class="banner-index">EXHIBITION / {{ current.year }}</p>
          <h2>{{ current.title || `${current.year} 年度最佳` }}</h2>
          <p v-if="current.intro" class="banner-intro">{{ current.intro }}</p>
        </div>
        <dl class="banner-spec">
          <div><dt>CEREMONY</dt><dd>{{ formatDate(current.ceremonyDate) || '待定' }}</dd></div>
          <div><dt>AWARDS</dt><dd>{{ current.entries.length }} 项</dd></div>
          <div><dt>PRIZES</dt><dd>{{ prizes.length }} 份</dd></div>
        </dl>
        <p v-if="!current.published" class="banner-draft" role="status">这一年还没有公开，只有管理员能看到。</p>
        <RouterLink v-if="hall?.canEdit" class="banner-edit" to="/admin?tab=awards">编辑展览 →</RouterLink>
      </section>

      <nav class="category-chips" aria-label="板块">
        <button type="button" :class="{ active: category === 'all' }" :aria-pressed="category === 'all'" @click="category = 'all'">全部</button>
        <button v-for="c in categories" :key="c.key" type="button" :class="{ active: category === c.key }" :aria-pressed="category === c.key" :disabled="!countOf(c.key)" @click="category = c.key">
          <span v-if="c.icon" aria-hidden="true">{{ c.icon }}</span>{{ c.name }}<small>{{ countOf(c.key) }}</small>
        </button>
      </nav>

      <p v-if="!sections.length" class="content-state">这个板块今年还没有获奖作品。</p>

      <section v-for="s in sections" :key="s.key" class="award-section" :aria-label="s.name">
        <header class="section-head">
          <span class="section-no">CATEGORY {{ String(s.index + 1).padStart(2, '0') }}</span>
          <h2><span v-if="s.icon" aria-hidden="true">{{ s.icon }}</span>{{ s.name }}</h2>
          <p v-if="s.description">{{ s.description }}</p>
        </header>

        <!-- 本板块最高奖项 -->
        <article class="featured" :class="'medal-' + medal(s.featured.rank)">
          <component :is="isInternal(s.featured.workLink) ? 'RouterLink' : s.featured.workLink ? 'a' : 'div'" class="featured-cover" data-depth
            v-bind="isInternal(s.featured.workLink) ? { to: s.featured.workLink } : s.featured.workLink ? { href: s.featured.workLink, target: '_blank', rel: 'noopener' } : {}">
            <img v-if="s.featured.coverUrl" :src="s.featured.coverUrl" :alt="s.featured.workTitle || ''" loading="lazy" @error="hide" />
            <span v-else class="cover-empty" aria-hidden="true">{{ s.icon || '★' }}</span>
            <span class="award-plate"><b>No.{{ s.featured.rank }}</b>{{ s.featured.awardName }}</span>
          </component>
          <div class="featured-info">
            <p class="info-label">WINNER / {{ s.name }}</p>
            <h3>{{ s.featured.workTitle || '未命名作品' }}</h3>
            <p class="byline">作者 <UserLink :id="s.featured.authorId" :name="s.featured.authorName || '未署名'" /></p>
            <blockquote v-if="s.featured.citation">{{ s.featured.citation }}</blockquote>
            <div v-if="s.featured.prize" class="prize-box">
              <img v-if="s.featured.prizeImage" :src="s.featured.prizeImage" alt="" loading="lazy" @error="hide" />
              <div><span>奖品</span><strong>{{ s.featured.prize }}</strong><small v-if="s.featured.prizeNote">{{ s.featured.prizeNote }}</small></div>
            </div>
          </div>
        </article>

        <!-- 其他奖项 -->
        <div v-if="s.others.length" class="award-grid">
          <article v-for="e in s.others" :key="entryKey(e)" class="award-card" :class="'medal-' + medal(e.rank)">
            <component :is="isInternal(e.workLink) ? 'RouterLink' : e.workLink ? 'a' : 'div'" class="card-cover" data-depth
              v-bind="isInternal(e.workLink) ? { to: e.workLink } : e.workLink ? { href: e.workLink, target: '_blank', rel: 'noopener' } : {}">
              <img v-if="e.coverUrl" :src="e.coverUrl" :alt="e.workTitle || ''" loading="lazy" @error="hide" />
              <span v-else class="cover-empty" aria-hidden="true">{{ s.icon || '★' }}</span>
              <span class="award-plate"><b>No.{{ e.rank }}</b>{{ e.awardName }}</span>
            </component>
            <div class="card-info">
              <h3>{{ e.workTitle || '未命名作品' }}</h3>
              <p class="byline"><UserLink :id="e.authorId" :name="e.authorName || '未署名'" /></p>
              <p v-if="e.citation" class="card-citation">{{ e.citation }}</p>
              <p v-if="e.prize" class="card-prize"><span>奖品</span>{{ e.prize }}</p>
            </div>
          </article>
        </div>
      </section>

      <!-- 奖品发放记录 -->
      <section v-if="prizes.length" class="prize-ledger" aria-label="奖品发放记录">
        <header class="section-head">
          <span class="section-no">LEDGER / {{ current.year }}</span>
          <h2>奖品发放记录</h2>
        </header>
        <div class="ledger-table" role="table">
          <div class="ledger-row ledger-head" role="row"><span role="columnheader">奖项</span><span role="columnheader">获奖者</span><span role="columnheader">奖品</span><span role="columnheader">说明</span></div>
          <div v-for="e in prizes" :key="'p' + e.id" class="ledger-row" role="row">
            <span role="cell"><small>{{ categoryName(e.category) }}</small>{{ e.awardName }}</span>
            <span role="cell"><UserLink :id="e.authorId" :name="e.authorName || '未署名'" /></span>
            <span role="cell" class="ledger-prize"><img v-if="e.prizeImage" :src="e.prizeImage" alt="" loading="lazy" @error="hide" />{{ e.prize }}</span>
            <span role="cell" class="ledger-note">{{ e.prizeNote || '—' }}</span>
          </div>
        </div>
      </section>
    </template>
  </article>
</template>

<style scoped>
.awards-page { max-width: 1200px; margin: 0 auto; color: var(--text-primary); }

/* ---- 顶部 ---- */
.awards-hero { position: relative; display: grid; grid-template-columns: minmax(0, 1.3fr) minmax(220px, 1fr); gap: 28px; align-items: stretch; }
.hero-text { display: grid; align-content: center; gap: 14px; min-width: 0; }
.hero-text h1 { margin: 0; font-size: clamp(28px, 4vw, 46px); letter-spacing: -.03em; line-height: 1.15; }
.hero-text > p { margin: 0; max-width: 560px; color: var(--text-secondary); line-height: 1.85; }
.year-switch { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 8px; }
.year-switch button { position: relative; display: inline-flex; align-items: baseline; gap: 6px; padding: 8px 16px; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-secondary); font: 600 15px var(--font-mono); cursor: pointer; transition: color .2s, border-color .2s; }
.year-switch button small { font: 10px var(--font-sans, inherit); color: var(--warning, #b7791f); }
.year-switch button:hover { color: var(--text-primary); border-color: var(--accent-color); }
.year-switch button.active { background: var(--inverse-bg); color: var(--inverse-text); border-color: var(--inverse-bg); box-shadow: inset 0 -3px var(--signal-mint); }
.hero-plate { position: relative; min-height: 260px; background: var(--inverse-bg); clip-path: polygon(0 0, calc(100% - 22px) 0, 100% 22px, 100% 100%, 22px 100%, 0 calc(100% - 22px)); overflow: hidden; }
.hero-plate::before { content: ''; position: absolute; inset: 0; background-image: linear-gradient(color-mix(in srgb, var(--inverse-text) 6%, transparent) 1px, transparent 1px), linear-gradient(90deg, color-mix(in srgb, var(--inverse-text) 6%, transparent) 1px, transparent 1px); background-size: 24px 24px; pointer-events: none; }
.plate-particles { position: absolute; left: 6%; top: 8%; width: 88%; height: 84%; }
.plate-code, .plate-corner { position: absolute; font: 10px var(--font-mono); letter-spacing: .12em; color: var(--inverse-text); opacity: .7; }
.plate-code { left: 14px; top: 12px; padding: 2px 6px; border-left: 2px solid var(--signal-mint); }
.plate-corner { right: 30px; bottom: 12px; }

/* ---- 年度横幅 ---- */
.year-banner { position: relative; display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 20px 32px; align-items: end; margin: 28px 0 20px; padding: 22px 24px; border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); background: linear-gradient(115deg, var(--paper-card) 55%, var(--accent-soft)); }
.banner-index { margin: 0 0 6px; font: 10px var(--font-mono); letter-spacing: .14em; color: var(--accent-color); }
.year-banner h2 { margin: 0; font-size: clamp(22px, 2.6vw, 30px); }
.banner-intro { margin: 10px 0 0; max-width: 720px; line-height: 1.85; color: var(--text-secondary); white-space: pre-wrap; }
.banner-spec { display: grid; grid-template-columns: repeat(3, auto); margin: 0; border: 1px solid var(--border-line); background: var(--paper-card); }
.banner-spec div { padding: 8px 16px; border-left: 1px solid var(--border-line); }
.banner-spec div:first-child { border-left: 0; }
.banner-spec dt { font: 9px var(--font-mono); letter-spacing: .14em; color: var(--accent-color); }
.banner-spec dd { margin: 3px 0 0; font-weight: 600; font-size: 14px; white-space: nowrap; }
.banner-draft { grid-column: 1 / -1; margin: 0; padding: 6px 10px; border-left: 3px solid var(--warning, #b7791f); background: var(--warning-soft, rgba(183,121,31,.1)); font-size: 13px; }
.banner-edit { position: absolute; top: 14px; right: 18px; font-size: 13px; color: var(--accent-color); text-decoration: none; }

/* ---- 板块筛选 ---- */
.category-chips { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 24px; }
.category-chips button { display: inline-flex; align-items: center; gap: 6px; padding: 8px 14px; border: 1px solid var(--border-line); background: transparent; color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.category-chips button small { font: 11px var(--font-mono); opacity: .7; }
.category-chips button.active { color: var(--accent-color); border-color: var(--accent-color); background: var(--accent-soft); }
.category-chips button:disabled { opacity: .4; cursor: default; }

/* ---- 板块 ---- */
.award-section { margin-bottom: 44px; }
.section-head { display: grid; gap: 4px; margin-bottom: 16px; padding-bottom: 12px; border-bottom: 1px solid var(--border-line); position: relative; }
.section-head::after { content: ''; position: absolute; left: 0; bottom: -1px; width: 64px; height: 2px; background: var(--signal-mint); }
.section-no { font: 10px var(--font-mono); letter-spacing: .14em; color: var(--accent-color); }
.section-head h2 { display: flex; align-items: center; gap: 10px; margin: 0; font-size: 22px; }
.section-head p { margin: 0; color: var(--text-secondary); font-size: 13px; }

/* 奖牌配色 */
.medal-gold { --medal: #d4af37; --medal-soft: rgba(212, 175, 55, .16); }
.medal-silver { --medal: #a7b1bd; --medal-soft: rgba(167, 177, 189, .18); }
.medal-bronze { --medal: #b87333; --medal-soft: rgba(184, 115, 51, .16); }
.medal-plain { --medal: var(--accent-color); --medal-soft: var(--accent-soft); }

.featured { display: grid; grid-template-columns: minmax(0, 1.25fr) minmax(0, 1fr); border: 1px solid var(--border-line); background: var(--paper-card); }
.featured-cover, .card-cover { position: relative; display: block; overflow: hidden; background: linear-gradient(135deg, var(--inverse-bg), var(--accent-color)); color: inherit; text-decoration: none; }
.featured-cover { min-height: 300px; aspect-ratio: 16 / 10; }
.featured-cover img, .card-cover img { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; transition: transform .6s cubic-bezier(.2,.7,.2,1); }
a.featured-cover:hover img, a.card-cover:hover img { transform: scale(1.04); }
.cover-empty { position: absolute; inset: 0; display: grid; place-items: center; font-size: 48px; color: var(--inverse-text); opacity: .6; }
.award-plate { position: absolute; left: 0; bottom: 18px; display: inline-flex; align-items: center; gap: 10px; padding: 8px 16px 8px 14px; background: var(--inverse-bg); color: var(--inverse-text); font-weight: 600; font-size: 14px; border-left: 4px solid var(--medal); clip-path: polygon(0 0, 100% 0, calc(100% - 12px) 100%, 0 100%); padding-right: 26px; }
.award-plate b { font: 700 12px var(--font-mono); color: var(--medal); }
.featured-info { display: grid; align-content: start; gap: 12px; padding: 26px 28px; border-top: 3px solid var(--medal); }
.info-label { margin: 0; font: 10px var(--font-mono); letter-spacing: .14em; color: var(--text-secondary); }
.featured-info h3 { margin: 0; font-size: clamp(22px, 2.4vw, 30px); line-height: 1.25; overflow-wrap: anywhere; }
.byline { margin: 0; color: var(--text-secondary); font-size: 14px; }
.byline :deep(.user-link) { color: var(--text-primary); font-weight: 600; }
.featured-info blockquote { margin: 4px 0; padding: 12px 16px; border-left: 3px solid var(--signal-mint); background: linear-gradient(90deg, var(--accent-soft), transparent); line-height: 1.85; white-space: pre-wrap; }
.prize-box { display: flex; align-items: center; gap: 14px; padding: 12px 14px; border: 1px dashed var(--medal); background: var(--medal-soft); }
.prize-box img { width: 56px; height: 56px; object-fit: cover; flex-shrink: 0; }
.prize-box div { display: grid; gap: 2px; min-width: 0; }
.prize-box span { font: 10px var(--font-mono); letter-spacing: .12em; color: var(--text-secondary); }
.prize-box strong { font-size: 15px; overflow-wrap: anywhere; }
.prize-box small { color: var(--text-secondary); font-size: 12px; }

.award-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 16px; margin-top: 16px; }
.award-card { display: grid; grid-template-rows: auto 1fr; border: 1px solid var(--border-line); border-top: 3px solid var(--medal); background: var(--paper-card); }
.card-cover { aspect-ratio: 16 / 10; }
.card-cover .award-plate { bottom: 12px; font-size: 12px; padding-block: 6px; }
.card-info { display: grid; align-content: start; gap: 6px; padding: 14px 16px 16px; }
.card-info h3 { margin: 0; font-size: 16px; overflow-wrap: anywhere; }
.card-citation { margin: 0; color: var(--text-secondary); font-size: 13px; line-height: 1.7; display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; overflow: hidden; }
.card-prize { margin: 4px 0 0; padding-top: 8px; border-top: 1px dashed var(--border-line); font-size: 13px; }
.card-prize span { margin-right: 8px; font: 10px var(--font-mono); letter-spacing: .1em; color: var(--medal); }

/* ---- 奖品记录 ---- */
.prize-ledger { margin-bottom: 40px; }
.ledger-table { border: 1px solid var(--border-line); background: var(--paper-card); }
.ledger-row { display: grid; grid-template-columns: 1.2fr 1fr 1.3fr 1.2fr; gap: 12px; align-items: center; padding: 12px 16px; border-top: 1px solid var(--border-line); font-size: 14px; }
.ledger-row:first-child { border-top: 0; }
.ledger-head { background: var(--bg-sub); font: 10px var(--font-mono); letter-spacing: .12em; color: var(--text-secondary); }
.ledger-row small { display: block; font-size: 11px; color: var(--text-secondary); }
.ledger-prize { display: flex; align-items: center; gap: 10px; font-weight: 600; }
.ledger-prize img { width: 32px; height: 32px; object-fit: cover; }
.ledger-note { color: var(--text-secondary); font-size: 13px; }

@media (max-width: 860px) {
  .awards-hero { grid-template-columns: 1fr; }
  .hero-plate { min-height: 220px; }
  .year-banner { grid-template-columns: 1fr; }
  .featured { grid-template-columns: 1fr; }
  .featured-cover { min-height: 0; }
  .ledger-head { display: none; }
  .ledger-row { grid-template-columns: 1fr 1fr; }
}
@media (max-width: 520px) {
  .banner-spec { grid-template-columns: 1fr; width: 100%; }
  .banner-spec div { border-left: 0; border-top: 1px solid var(--border-line); display: flex; justify-content: space-between; }
  .banner-spec div:first-child { border-top: 0; }
  .featured-info { padding: 18px; }
  .banner-edit { position: static; }
}
@media (prefers-reduced-motion: reduce) { .featured-cover img, .card-cover img { transition: none; } }
</style>
