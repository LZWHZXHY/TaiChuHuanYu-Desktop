<!-- 首页侧栏的「赞助」卡片（fix181）：比原来「加入社区」里的一行链接显眼，讲清楚赞助能得到什么。
     · 四个档位（觉醒者 / 超凡者 / 大超凡者 / 君者）：点一下看这一档的价格、介绍和福利，每一档都送同名专属头衔；
     · 已经在赞助的人看到自己的档位和到期时间，按钮变成「续费 / 升级」；
     · 下面是赞助人数和赞助墙入口。
     档位价格、介绍、福利都来自管理面板「赞助福利」（/api/SponsorPerks），读不到时用默认文字，卡片照样显示。 -->
<template>
  <section class="ssp" aria-label="赞助太初寰宇">
    <div class="ssp-card">
      <header class="ssp-head">
        <span class="ssp-eyebrow">SUPPORT · 赞助</span>
        <h2 class="ssp-title">支持太初寰宇继续创造</h2>
        <p class="ssp-intro">{{ intro }}<span class="ssp-soon">后续还会增加更多福利，敬请期待。</span></p>
      </header>

      <!-- 已经在赞助 -->
      <p v-if="mine?.active && mine.tier" class="ssp-mine">
        <UserTitle :title="mine.tier" class="ssp-mine-badge" />
        <span>你是 <b>{{ mine.tier }}</b>，谢谢你！<small v-if="mine.until">特权到 {{ fmtDate(mine.until) }}</small></span>
      </p>

      <!-- 四个档位 -->
      <div class="ssp-tiers" role="tablist" aria-label="赞助档位">
        <button v-for="t in tiers" :key="t.rank" type="button" role="tab" class="ssp-tier" :class="['t' + t.rank, { on: picked === t.rank }]"
          :aria-selected="picked === t.rank" @click="picked = t.rank">
          <span class="ssp-tier-name">{{ t.name }}</span>
          <small v-if="t.price">{{ t.price }}</small>
        </button>
      </div>
      <div v-if="current" class="ssp-detail" role="tabpanel">
        <p v-if="current.blurb" class="ssp-blurb">{{ current.blurb }}</p>
        <ul class="ssp-perks">
          <li class="ssp-perk-title">专属头衔 <UserTitle :title="current.name" /></li>
          <li v-for="p in perksOf(current.rank)" :key="p.id" :title="p.description">{{ p.title }}</li>
          <li v-if="morePerks(current.rank) > 0" class="ssp-more">还有 {{ morePerks(current.rank) }} 项，去赞助墙看全部</li>
        </ul>
      </div>

      <a class="ssp-cta" :href="AZZ_URL" target="_blank" rel="noopener noreferrer" @click.prevent="openExternal(AZZ_URL)">
        <span>{{ mine?.active ? '续费 / 升级档位' : '去爱赞助支持' }}</span><span aria-hidden="true">↗</span>
      </a>
      <footer class="ssp-foot">
        <span v-if="sponsorCount">已有 <b>{{ sponsorCount }}</b> 位赞助者</span>
        <span v-else>每一份支持都会记在赞助墙上</span>
        <RouterLink to="/sponsors" class="ssp-wall">赞助墙 · 全部福利 →</RouterLink>
      </footer>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import UserTitle from '@/components/UserTitle.vue'
import { sponsorPerksApi, type PerkCatalog, type PerkItem } from '@/api/sponsorPerks'
import { sponsorApi } from '@/api/sponsor'
import { AZZ_URL } from '@/utils/sponsorChannels'

const NAMES = ['觉醒者', '超凡者', '大超凡者', '君者']
const DEFAULT_INTRO = '社区没有广告，服务器、存储和开发都靠大家的赞助撑着。赞助后自动获得同名专属头衔，档位越高解锁的福利越多。'

const catalog = ref<PerkCatalog | null>(null)
const sponsorCount = ref(0)
const picked = ref(1)

const intro = computed(() => catalog.value?.intro?.trim() || DEFAULT_INTRO)
const mine = computed(() => catalog.value?.mine ?? null)
const tiers = computed(() => NAMES.map((name, i) => {
  const t = catalog.value?.tiers.find(x => x.rank === i + 1)
  return { rank: i + 1, name, price: t?.price ?? '', blurb: t?.blurb ?? '' }
}))
const current = computed(() => tiers.value.find(t => t.rank === picked.value) ?? null)
const visiblePerks = computed<PerkItem[]>(() => (catalog.value?.perks ?? []).filter(p => p.visible))
/** 这一档能用的福利（高档包含低档的），卡片里最多列 4 条 */
const allOf = (rank: number) => visiblePerks.value.filter(p => p.minTier <= rank)
const perksOf = (rank: number) => allOf(rank).sort((a, b) => b.minTier - a.minTier).slice(0, 4)
const morePerks = (rank: number) => Math.max(0, allOf(rank).length - 4)

const fmtDate = (iso: string) => new Date(iso).toLocaleDateString('zh-CN', { year: 'numeric', month: 'numeric', day: 'numeric' })

onMounted(async () => {
  const [c, wall] = await Promise.allSettled([sponsorPerksApi.get(), sponsorApi.list()])
  if (c.status === 'fulfilled' && c.value) {
    catalog.value = c.value
    // 已经在赞助的人默认看自己那一档的下一档（升级能多得到什么），君者就看君者
    if (c.value.mine?.active && c.value.mine.rank) picked.value = Math.min(4, c.value.mine.rank + 1)
  }
  if (wall.status === 'fulfilled' && wall.value) sponsorCount.value = wall.value.totalCount
})

// 桌面 WebView 里交给宿主用系统浏览器打开，网页里直接新窗口打开（和「加入社区」一样）
function openExternal(url: string) {
  const host = (window as any).chrome?.webview
  if (host) host.postMessage({ cmd: 'load_external_url', url })
  else window.open(url, '_blank', 'noopener,noreferrer')
}
</script>

<style scoped>
.ssp-card {
  --gold: #eab308;
  position: relative; display: grid; gap: 12px; padding: 16px 16px 14px;
  border: 1px solid color-mix(in srgb, var(--gold) 55%, var(--border-line));
  background:
    radial-gradient(120% 80% at 100% 0%, color-mix(in srgb, var(--gold) 16%, transparent), transparent 60%),
    var(--paper-card);
  color: var(--text-primary);
}
.ssp-card::before { content: ''; position: absolute; left: -1px; right: -1px; top: -1px; height: 3px; background: linear-gradient(90deg, #3bb8a6, #7a5cf0, #f97316, var(--gold)); }
.ssp-head { display: grid; gap: 4px; }
.ssp-eyebrow { font: 600 10px/1.4 var(--font-mono, monospace); letter-spacing: .16em; color: var(--gold); }
.ssp-title { margin: 0; font-size: 18px !important; font-weight: 700; letter-spacing: -.01em; }
.ssp-intro { margin: 0; font-size: 12.5px; line-height: 1.7; color: var(--text-secondary); }
.ssp-soon { margin-left: .25em; color: var(--gold); font-weight: 600; }
.ssp-mine { display: flex; align-items: center; gap: 8px; margin: 0; padding: 8px 10px; border: 1px dashed color-mix(in srgb, var(--gold) 60%, var(--border-line)); font-size: 13px; }
.ssp-mine :deep(.user-title-badge) { margin-left: 0; }
.ssp-mine small { display: block; color: var(--text-secondary); font-size: 11px; }
.ssp-tiers { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 4px; }
.ssp-tier { display: grid; gap: 2px; justify-items: center; padding: 7px 2px; border: 1px solid var(--border-line); background: var(--bg-sub); color: var(--text-secondary); font: inherit; cursor: pointer; transition: border-color .15s, color .15s, background .15s, translate .15s; }
.ssp-tier-name { font-size: 12.5px; font-weight: 700; white-space: nowrap; }
.ssp-tier small { font-size: 10px; opacity: .85; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 100%; }
.ssp-tier:hover { translate: 0 -1px; color: var(--text-primary); }
.ssp-tier:focus-visible { outline: 2px solid var(--gold); outline-offset: 1px; }
/* 选中的档位：颜色和这一档的头衔一样 */
.ssp-tier.on { color: #fff; border-color: transparent; }
.ssp-tier.t1.on { background: linear-gradient(135deg, #2f9e8f, #3bb8a6); }
.ssp-tier.t2.on { background: linear-gradient(135deg, #3b6fd8, #7a5cf0); }
.ssp-tier.t3.on { background: linear-gradient(135deg, #c2410c, #f97316); }
.ssp-tier.t4.on { background: linear-gradient(135deg, #a16207, #eab308 55%, #fde68a); color: #3b2a00; }
.ssp-detail { display: grid; gap: 6px; min-height: 92px; }
.ssp-blurb { margin: 0; font-size: 12.5px; line-height: 1.6; color: var(--text-primary); }
.ssp-perks { list-style: none; margin: 0; padding: 0; display: grid; gap: 4px; font-size: 12.5px; color: var(--text-secondary); }
.ssp-perks li { position: relative; padding-left: 14px; }
.ssp-perks li::before { content: '✦'; position: absolute; left: 0; color: var(--gold); font-size: 10px; top: 2px; }
.ssp-perk-title { display: flex; align-items: center; gap: 2px; color: var(--text-primary); }
.ssp-more { font-size: 11.5px; opacity: .85; }
.ssp-more::before { content: '' !important; }
.ssp-cta {
  display: flex; align-items: center; justify-content: space-between; gap: 10px; padding: 11px 14px;
  background: linear-gradient(135deg, #a16207, var(--gold)); color: #241a00; font-weight: 700; font-size: 14px; text-decoration: none;
  transition: filter .15s, translate .15s, box-shadow .15s;
}
.ssp-cta:hover, .ssp-cta:focus-visible { filter: brightness(1.08); translate: 0 -1px; box-shadow: 0 4px 14px color-mix(in srgb, var(--gold) 35%, transparent); }
.ssp-cta:focus-visible { outline: 2px solid var(--text-primary); outline-offset: 2px; }
.ssp-foot { display: flex; align-items: center; justify-content: space-between; gap: 8px; flex-wrap: wrap; font-size: 12px; color: var(--text-secondary); }
.ssp-foot b { color: var(--gold); font-family: var(--font-mono, monospace); }
.ssp-wall { color: var(--text-secondary); text-decoration: none; white-space: nowrap; }
.ssp-wall:hover { color: var(--gold); }
@media (prefers-reduced-motion: reduce) { .ssp-tier, .ssp-cta { transition: none; } }
</style>
