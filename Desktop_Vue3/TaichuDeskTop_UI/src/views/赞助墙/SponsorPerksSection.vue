<!-- 赞助墙上的「赞助福利」：每个等级多少钱、能得到什么（高等级包含低等级的全部福利）。
     内容由超级管理员在「管理面板 → 赞助福利」里随时改（后端 /api/SponsorPerks），这里只负责展示。 -->
<template>
  <section class="spk" aria-labelledby="spk-title">
    <p class="spk-intro">{{ catalog?.intro || FALLBACK_INTRO }}</p>

    <div class="spk-head">
      <h2 id="spk-title">赞助福利</h2>
      <RouterLink v-if="catalog?.canEdit" class="spk-edit" :to="{ path: '/admin', query: { tab: 'sponsorPerks' } }">✎ 编辑福利</RouterLink>
    </div>

    <p v-if="mineText" class="spk-mine" :class="{ on: catalog?.mine?.active }">{{ mineText }}</p>

    <p v-if="loading" class="spk-tip">正在加载赞助福利…</p>
    <p v-else-if="!catalog" class="spk-tip">赞助福利暂时读不出来，请稍后刷新。</p>
    <div v-else class="spk-grid">
      <article v-for="t in catalog.tiers" :key="t.rank" class="spk-tier" :class="[tierClass(t.rank), { mine: catalog.mine?.active && catalog.mine.rank === t.rank }]">
        <header class="spk-tier-head">
          <h3>{{ t.name }}</h3>
          <span v-if="catalog.mine?.active && catalog.mine.rank === t.rank" class="spk-you">你在这里</span>
        </header>
        <p v-if="t.price" class="spk-price">{{ t.price }}</p>
        <p v-if="t.blurb" class="spk-blurb">{{ t.blurb }}</p>
        <p v-if="t.rank > 1" class="spk-include">包含「{{ catalog.tiers[t.rank - 2]?.name }}」的全部福利，另外还有：</p>
        <ul v-if="perksOf(t.rank).length" class="spk-list">
          <li v-for="p in perksOf(t.rank)" :key="p.id">
            <b>{{ p.title }}</b>
            <span v-if="p.description">{{ p.description }}</span>
          </li>
        </ul>
        <p v-else-if="t.rank === 1" class="spk-tip">（暂时还没写福利）</p>
      </article>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { sponsorPerksApi, type PerkCatalog } from '@/api/sponsorPerks'

const FALLBACK_INTRO = '通过爱赞助支持太初寰宇。赞助后在爱赞助私信里告诉我们你的网站用户名，我们会给你的账号开通对应等级的赞助福利。'

const catalog = ref<PerkCatalog | null>(null)
const loading = ref(true)

// 赞助墙只展示公开的福利；管理员拿到的数据里有隐藏的，这里也不显示（在后台编辑时才看得到）
const perksOf = (rank: number) => (catalog.value?.perks ?? []).filter(p => p.visible && p.minTier === rank)
const tierClass = (rank: number) => ['tier-awaken', 'tier-transcend', 'tier-great', 'tier-lord'][rank - 1] ?? 'tier-awaken'

const mineText = computed(() => {
  const m = catalog.value?.mine
  if (!m) return ''
  if (!m.active) return m.until ? `你的赞助福利已经在 ${fmt(m.until)} 到期，续费后马上恢复。` : '你还没有赞助福利。'
  return `你现在是「${m.tier}」，福利有效期到 ${m.until ? fmt(m.until) : '—'}。`
})
const fmt = (v: string) => new Date(v).toLocaleDateString('zh-CN', { year: 'numeric', month: 'long', day: 'numeric' })

onMounted(async () => {
  try { catalog.value = await sponsorPerksApi.get() } catch { catalog.value = null } finally { loading.value = false }
})
</script>

<style scoped>
.spk { margin-bottom: 24px; }
.spk-intro { margin: 0 0 18px; padding: 12px 16px; border-left: 3px solid var(--accent-color); background: var(--accent-soft); font-size: 13px; line-height: 1.7; color: var(--text-primary); white-space: pre-line; }
.spk-head { display: flex; align-items: baseline; justify-content: space-between; gap: 12px; margin-bottom: 8px; }
.spk-head h2 { margin: 0; font-size: 18px; font-weight: 600; color: var(--text-primary); }
.spk-edit { font-size: 13px; color: var(--accent-color); text-decoration: none; }
.spk-edit:hover { text-decoration: underline; }
.spk-mine { margin: 0 0 12px; font-size: 13px; color: var(--text-secondary); }
.spk-mine.on { color: var(--text-primary); }
.spk-tip { margin: 0; color: var(--text-secondary); font-size: 13px; }
.spk-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 12px; }
.spk-tier { display: grid; align-content: start; gap: 8px; padding: 14px; border: 1px solid var(--border-line); border-top: 3px solid var(--success); background: var(--paper-card); }
.spk-tier.tier-transcend { border-top-color: var(--info); }
.spk-tier.tier-great { border-top-color: var(--accent-color); }
.spk-tier.tier-lord { border-top-color: var(--warning); }
.spk-tier.mine { outline: 2px solid var(--accent-color); outline-offset: -2px; }
.spk-tier-head { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.spk-tier-head h3 { margin: 0; font-size: 16px; color: var(--text-primary); }
.spk-you { padding: 1px 6px; background: var(--accent-color); color: var(--on-accent, #fff); font-size: 11px; }
.spk-price { margin: 0; font-size: 15px; font-weight: 600; color: var(--text-primary); }
.spk-blurb { margin: 0; font-size: 13px; line-height: 1.6; color: var(--text-secondary); }
.spk-include { margin: 0; font-size: 12px; color: var(--text-secondary); }
.spk-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.spk-list li { display: grid; gap: 2px; padding-left: 12px; border-left: 2px solid var(--border-line); font-size: 13px; }
.spk-list li b { font-weight: 600; color: var(--text-primary); }
.spk-list li span { color: var(--text-secondary); line-height: 1.5; }
@media (max-width: 900px) { .spk-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 520px) { .spk-grid { grid-template-columns: 1fr; } }
</style>
