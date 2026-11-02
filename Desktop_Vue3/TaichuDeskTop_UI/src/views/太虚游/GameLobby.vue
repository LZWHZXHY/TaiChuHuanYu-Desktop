<template>
  <div class="lobby">
    <!-- ① 主推：太初寰宇出品的 Steam 游戏 -->
    <section v-if="hero" class="hero" :style="heroBg ? { '--hero-bg': `url(${heroBg})` } : undefined" aria-labelledby="hero-title">
      <div class="hero-shade" aria-hidden="true"></div>
      <div class="hero-copy">
        <p class="hero-eyebrow"><span>太初寰宇出品</span><span v-if="hero.appId">STEAM · {{ hero.appId }}</span><b :class="'st-' + hero.status">{{ statusLabel(hero.status) }}</b></p>
        <h2 id="hero-title">{{ hero.title }}</h2>
        <p v-if="hero.steamName && hero.steamName !== hero.title" class="hero-alt">{{ hero.steamName }}</p>
        <p v-if="hero.tagline" class="hero-tagline">{{ hero.tagline }}</p>
        <p v-if="hero.description" class="hero-desc">{{ hero.description }}</p>
        <ul class="hero-meta">
          <li v-if="hero.releaseDate"><small>发售</small>{{ hero.releaseDate }}</li>
          <li v-if="hero.price || hero.isFree"><small>价格</small>
            <template v-if="hero.isFree">免费</template>
            <template v-else><s v-if="hero.discountPercent">{{ hero.originalPrice }}</s>{{ hero.price }}<em v-if="hero.discountPercent">-{{ hero.discountPercent }}%</em></template>
          </li>
          <li v-if="hero.genres.length"><small>类型</small>{{ hero.genres.slice(0, 3).join(' / ') }}</li>
        </ul>
        <div class="hero-actions">
          <a v-if="hero.storeUrl" :href="hero.storeUrl" class="btn-steam" data-burst @click.prevent="openExternal(hero.storeUrl)">
            <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true"><circle cx="12" cy="12" r="10" fill="none" stroke="currentColor" stroke-width="1.6" /><circle cx="15.5" cy="9" r="2.6" fill="none" stroke="currentColor" stroke-width="1.6" /><circle cx="8.5" cy="15" r="1.8" fill="currentColor" /><path d="M9.8 14.2 13.3 10.5" stroke="currentColor" stroke-width="1.6" /></svg>
            {{ hero.status === 'released' ? '在 Steam 上购买' : '在 Steam 上加入愿望单' }}
          </a>
          <RouterLink v-if="hero.devProjectId" :to="`/dev-progress/${hero.devProjectId}`" class="btn-line">开发日志</RouterLink>
          <RouterLink v-if="hero.discussionTag" :to="{ path: '/stream', query: { tag: hero.discussionTag } }" class="btn-line">玩家讨论</RouterLink>
        </div>
        <div v-if="featured.length > 1" class="hero-dots" role="tablist" aria-label="切换主推游戏">
          <button v-for="(g, i) in featured" :key="g.id" type="button" role="tab" :aria-selected="i === heroIndex" :aria-label="g.title" :class="{ on: i === heroIndex }" @click="pickHero(i)"></button>
        </div>
      </div>
      <figure class="hero-media" data-depth="soft">
        <img v-if="hero.cover" :src="hero.cover" :alt="`${hero.title} 封面`" />
        <figcaption v-if="hero.screenshotThumbs.length" class="hero-shots">
          <button v-for="(s, i) in hero.screenshotThumbs.slice(0, 4)" :key="s" type="button" :class="{ on: shotIndex === i }" :aria-label="`查看截图 ${i + 1}`" @click="shotIndex = i"><img :src="s" alt="" loading="lazy" /></button>
        </figcaption>
      </figure>
    </section>
    <section v-else-if="loadingShowcase" class="hero hero-loading" aria-busy="true"><ParticleState kind="loading" text="正在读取我们的游戏…" compact /></section>

    <!-- ② 我们的游戏 -->
    <section v-if="showcase.length" class="block" aria-labelledby="studio-title">
      <header class="block-head">
        <h3 id="studio-title"><span class="idx">01</span>我们的游戏</h3>
        <p>太初寰宇自己开发的游戏。已经上架的可以直接去 Steam 看看，还在做的可以跟着开发日志一起等。</p>
      </header>
      <div class="studio-grid">
        <article v-for="g in showcase" :key="g.id" class="studio-card" data-depth>
          <button type="button" class="studio-cover" :aria-label="`${g.title}：设为主推查看`" @click="focusGame(g)">
            <img v-if="g.cover" :src="g.cover" alt="" loading="lazy" />
            <span v-else class="cover-empty">{{ g.title }}</span>
            <b class="ribbon" :class="'st-' + g.status">{{ statusLabel(g.status) }}</b>
          </button>
          <div class="studio-body">
            <h4>{{ g.title }}</h4>
            <p>{{ g.tagline || g.description }}</p>
            <div class="studio-foot">
              <span class="price">{{ g.isFree ? '免费' : g.price || (g.status === 'released' ? '' : g.releaseDate || '敬请期待') }}</span>
              <a v-if="g.storeUrl" :href="g.storeUrl" @click.prevent="openExternal(g.storeUrl)">Steam ↗</a>
              <RouterLink v-if="g.devProjectId" :to="`/dev-progress/${g.devProjectId}`">开发日志</RouterLink>
            </div>
          </div>
        </article>
        <RouterLink to="/dev-progress" class="studio-card next-card">
          <span class="next-plus" aria-hidden="true">+</span>
          <b>下一款正在开发</b>
          <small>去开发进度看看我们在做什么 →</small>
        </RouterLink>
      </div>
    </section>

    <!-- ③ 我的世界服务器 -->
    <section v-if="modules.MinecraftServer" class="block" aria-labelledby="mc-title">
      <header class="block-head">
        <h3 id="mc-title"><span class="idx">02</span>社区服务器</h3>
      </header>
      <div class="mc-card">
        <div class="mc-art" aria-hidden="true"><span v-for="i in 48" :key="i" :class="'b' + (mcPattern[i % mcPattern.length])"></span></div>
        <div class="mc-copy">
          <p class="hero-eyebrow"><span>MINECRAFT · 太初寰宇服务器</span><b class="st-released">{{ modules.MinecraftServer.status }}</b></p>
          <h4>我的世界</h4>
          <p>{{ modules.MinecraftServer.description }}。和社区成员一起建城、探险，好的建筑还能参加年度最佳评选。</p>
          <div class="hero-actions">
            <button type="button" class="btn-primary" data-burst @click="emit('select', 'MinecraftServer')">进入服务器专区</button>
            <RouterLink class="btn-line" to="/stream?tag=我的世界">我的世界讨论</RouterLink>
            <RouterLink class="btn-line" to="/stream?tag=我的世界&view=quests">⛏ 世界任务</RouterLink>
            <RouterLink class="btn-line" to="/awards">MC 建筑年度最佳</RouterLink>
          </div>
        </div>
      </div>
    </section>

    <!-- 幸运转盘入口（fix132）：官方抽奖每天免费转一次，也能自己做转盘 -->
    <button v-if="modules.Wheel" type="button" class="wheel-banner" data-burst @click="emit('select', 'Wheel')">
      <span class="wb-wheel" aria-hidden="true"></span>
      <span class="wb-copy">
        <b>🎡 幸运转盘</b>
        <span>官方抽奖转盘每天免费转一次，土豆、经验直接到账；也可以自己做个转盘，帮你决定吃什么、抽人、抽题。</span>
      </span>
      <span class="wb-go">去转一转 →</span>
    </button>

    <!-- ④ 社区小游戏 -->
    <section class="block" aria-labelledby="mini-title">
      <header class="block-head">
        <h3 id="mini-title"><span class="idx">03</span>社区小游戏</h3>
        <div class="cat-tabs" role="group" aria-label="游戏分类">
          <button type="button" :class="{ on: !cat }" :aria-pressed="!cat" @click="cat = ''">全部</button>
          <button v-for="c in categories" :key="c.key" type="button" :class="{ on: cat === c.key }" :aria-pressed="cat === c.key" @click="cat = c.key">{{ c.label }}</button>
        </div>
      </header>
      <div class="mini-grid">
        <button v-for="g in playable" :key="g.key" type="button" class="mini-card" data-depth @click="emit('select', g.key)">
          <GameCover :kind="g.key" :label="g.label" :icon="g.icon" :eyebrow="categoryLabel(g.category)" />
          <span class="mini-body">
            <span>{{ g.description }}</span>
            <span class="mini-tags"><em v-if="g.isNew" class="tag-new">新</em><em v-if="g.online">可联机</em>进入 →</span>
          </span>
        </button>
      </div>
      <template v-if="upcoming.length">
        <h4 class="upcoming-title">即将开放</h4>
        <div class="mini-grid upcoming">
          <button v-for="g in upcoming" :key="g.key" type="button" class="mini-card is-dev" :title="`${g.label}还在开发中`" @click="emit('select', g.key)">
            <GameCover :kind="g.key" :label="g.label" :icon="g.icon" :eyebrow="categoryLabel(g.category)" />
            <span class="mini-body"><span>{{ g.description }}</span><span class="mini-tags"><em>开发中</em></span></span>
          </button>
        </div>
      </template>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import ParticleState from '@/components/ParticleState.vue'
import GameCover from './GameCover.vue'
import { openExternal, showcaseApi, statusLabel, type ShowcaseGame } from '@/api/gameShowcase'

interface LobbyModule { label: string; category: string; icon: string; description: string; status: string; isNew?: boolean; hidden?: boolean }
const props = defineProps<{ modules: Record<string, LobbyModule>; categoryMap: Record<string, { label: string; order: number }> }>()
const emit = defineEmits<{ select: [key: string] }>()

// ---------- Steam 游戏 ----------
const showcase = ref<ShowcaseGame[]>([])
const loadingShowcase = ref(true)
const featured = computed(() => {
  const f = showcase.value.filter(g => g.featured)
  return f.length ? f : showcase.value.slice(0, 1)
})
const heroIndex = ref(0)
const shotIndex = ref(-1)
const hero = computed(() => featured.value[heroIndex.value] ?? null)
/** 背景：选中的截图 > Steam 背景图 > 第一张截图 */
const heroBg = computed(() => {
  const h = hero.value
  if (!h) return ''
  return (shotIndex.value >= 0 ? h.screenshots[shotIndex.value] : '') || h.background || h.screenshots[0] || h.cover || ''
})
let rotate = 0
function pickHero(i: number) { heroIndex.value = i; shotIndex.value = -1; restartRotate() }
function restartRotate() {
  window.clearInterval(rotate)
  if (featured.value.length > 1) rotate = window.setInterval(() => { heroIndex.value = (heroIndex.value + 1) % featured.value.length; shotIndex.value = -1 }, 9000)
}
function focusGame(g: ShowcaseGame) {
  const i = featured.value.findIndex(x => x.id === g.id)
  if (i >= 0) pickHero(i)
  else if (g.storeUrl) openExternal(g.storeUrl)
  window.scrollTo?.({ top: 0, behavior: 'smooth' })
  document.querySelector('.hero')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

// ---------- 社区小游戏 ----------
const cat = ref('')
const ONLINE = new Set(['Gobang', 'Doudizhu', 'Xiangqi', 'Go'])
const categories = computed(() => Object.entries(props.categoryMap)
  .filter(([key]) => key !== 'server' && Object.values(props.modules).some(m => m.category === key && !m.hidden))
  .sort((a, b) => a[1].order - b[1].order)
  .map(([key, v]) => ({ key, label: v.label })))
const categoryLabel = (key: string) => props.categoryMap[key]?.label ?? ''
const games = computed(() => Object.entries(props.modules)
  .filter(([key, m]) => key !== 'MinecraftServer' && !m.hidden && (!cat.value || m.category === cat.value))
  .map(([key, m]) => ({ key, ...m, online: ONLINE.has(key) })))
const playable = computed(() => games.value.filter(g => g.status !== '开发中'))
const upcoming = computed(() => games.value.filter(g => g.status === '开发中'))
const mcPattern = [1, 1, 2, 1, 3, 1, 2, 2, 1, 1, 3, 2, 1, 2]

onMounted(async () => {
  try { showcase.value = await showcaseApi.list() } catch { showcase.value = [] } finally { loadingShowcase.value = false }
  restartRotate()
})
onBeforeUnmount(() => window.clearInterval(rotate))
</script>

<style scoped>
.lobby { display: grid; gap: 36px; }

/* ---------- 主推 ---------- */
.hero { position: relative; isolation: isolate; overflow: hidden; display: grid; grid-template-columns: minmax(0, 1.05fr) minmax(0, .95fr); gap: 32px; align-items: center; min-height: 380px; padding: 40px; color: #f3f6f7; background: #0d1417 var(--hero-bg) center / cover no-repeat; border-top: 2px solid var(--signal-mint); transition: background-image .6s; }
.hero-shade { position: absolute; inset: 0; z-index: -1; background: linear-gradient(100deg, rgba(8, 14, 17, .94) 0%, rgba(8, 14, 17, .82) 42%, rgba(8, 14, 17, .35) 100%), repeating-linear-gradient(0deg, rgba(255,255,255,.025) 0 1px, transparent 1px 4px); }
.hero-loading { display: block; min-height: 220px; color: var(--text-primary); background: var(--paper-card); border: 1px solid var(--border-line); }
.hero-eyebrow { display: flex; flex-wrap: wrap; align-items: center; gap: 6px 12px; margin: 0 0 14px; font: 600 11px var(--font-mono, monospace); letter-spacing: .14em; color: var(--signal-mint); }
.hero-eyebrow b { padding: 2px 8px; font-weight: 600; letter-spacing: .04em; border: 1px solid currentColor; }
.st-released { color: #4ade80; }
.st-coming { color: #fbbf24; }
.st-dev { color: #93c5fd; }
.hero h2 { margin: 0; font-size: clamp(26px, 3.2vw, 40px); line-height: 1.2; letter-spacing: .01em; }
.hero-alt { margin: 6px 0 0; color: rgba(243, 246, 247, .6); font-size: 13px; }
.hero-tagline { margin: 16px 0 0; font-size: 16px; line-height: 1.7; color: #fff; }
.hero-desc { margin: 10px 0 0; max-width: 560px; font-size: 13.5px; line-height: 1.8; color: rgba(243, 246, 247, .72); }
.hero-meta { display: flex; flex-wrap: wrap; gap: 8px 26px; margin: 20px 0 0; padding: 14px 0 0; list-style: none; border-top: 1px solid rgba(255, 255, 255, .14); font-size: 14px; }
.hero-meta small { display: block; margin-bottom: 3px; font: 10px var(--font-mono, monospace); letter-spacing: .14em; color: rgba(243, 246, 247, .5); }
.hero-meta s { margin-right: 6px; color: rgba(243, 246, 247, .45); }
.hero-meta em { margin-left: 6px; padding: 0 5px; background: #4c6b22; color: #beee11; font-style: normal; font-size: 12px; }
.hero-actions { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 24px; }
.btn-steam { display: inline-flex; align-items: center; gap: 10px; padding: 12px 20px; background: linear-gradient(90deg, #06bfff, #2d73ff); color: #fff; font-weight: 600; text-decoration: none; box-shadow: 0 8px 24px rgba(45, 115, 255, .35); transition: transform .2s, box-shadow .2s; }
.btn-steam:hover { transform: translateY(-2px); box-shadow: 0 12px 28px rgba(45, 115, 255, .45); }
.btn-line { display: inline-flex; align-items: center; padding: 11px 16px; border: 1px solid rgba(255, 255, 255, .35); color: inherit; text-decoration: none; font-size: 14px; background: transparent; }
.btn-line:hover { border-color: var(--signal-mint); color: var(--signal-mint); }
.hero-dots { display: flex; gap: 8px; margin-top: 26px; }
.hero-dots button { width: 26px; height: 4px; padding: 0; border: 0; background: rgba(255, 255, 255, .3); cursor: pointer; }
.hero-dots button.on { width: 44px; background: var(--signal-mint); }
.hero-media { margin: 0; display: grid; gap: 10px; }
.hero-media > img { display: block; width: 100%; aspect-ratio: 460 / 215; object-fit: cover; box-shadow: 0 24px 60px rgba(0, 0, 0, .55), 0 0 0 1px rgba(255, 255, 255, .12); }
.hero-shots { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 8px; }
.hero-shots button { padding: 0; border: 1px solid rgba(255, 255, 255, .15); background: none; cursor: pointer; opacity: .65; transition: opacity .2s, border-color .2s; }
.hero-shots button:hover, .hero-shots button.on { opacity: 1; border-color: var(--signal-mint); }
.hero-shots img { display: block; width: 100%; aspect-ratio: 16 / 9; object-fit: cover; }

/* ---------- 区块 ---------- */
.block { display: grid; gap: 18px; }
.block-head { display: flex; flex-wrap: wrap; align-items: flex-end; justify-content: space-between; gap: 10px 24px; padding-bottom: 12px; border-bottom: 1px solid var(--border-line); }
.block-head h3 { display: flex; align-items: baseline; gap: 12px; margin: 0; font-size: 20px; }
.block-head p { margin: 0; max-width: 560px; color: var(--text-secondary); font-size: 13px; line-height: 1.7; }
.idx { font: 600 12px var(--font-mono, monospace); color: var(--accent-color); letter-spacing: .1em; }

/* ---------- 我们的游戏 ---------- */
.studio-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 18px; }
.studio-card { position: relative; display: flex; flex-direction: column; min-width: 0; background: var(--paper-card); border: 1px solid var(--border-line); color: var(--text-primary); text-decoration: none; }
.studio-cover { position: relative; display: block; padding: 0; border: 0; background: #0d1417; cursor: pointer; }
.studio-cover img { display: block; width: 100%; aspect-ratio: 460 / 215; object-fit: cover; }
.cover-empty { display: grid; place-items: center; aspect-ratio: 460 / 215; color: #f3f6f7; font-size: 20px; font-weight: 700; }
.ribbon { position: absolute; top: 10px; left: 10px; padding: 3px 8px; background: rgba(8, 14, 17, .85); border: 1px solid currentColor; font-size: 12px; font-weight: 600; }
.studio-body { display: grid; gap: 8px; padding: 14px 16px 16px; flex: 1; }
.studio-body h4 { margin: 0; font-size: 16px; }
.studio-body p { margin: 0; color: var(--text-secondary); font-size: 13px; line-height: 1.7; display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; overflow: hidden; }
.studio-foot { display: flex; flex-wrap: wrap; align-items: center; gap: 14px; margin-top: auto; padding-top: 8px; font-size: 13px; }
.studio-foot .price { margin-right: auto; font-weight: 600; }
.studio-foot a { color: var(--accent-color); text-decoration: none; }
.studio-foot a:hover { text-decoration: underline; }
.next-card { align-items: center; justify-content: center; gap: 8px; min-height: 220px; border-style: dashed; text-align: center; padding: 24px; background: transparent; }
.next-card:hover { border-color: var(--accent-color); }
.next-plus { display: grid; place-items: center; width: 48px; height: 48px; border: 1px solid var(--border-line); font-size: 26px; color: var(--accent-color); }
.next-card small { color: var(--text-secondary); }

/* ---------- MC ---------- */
.mc-card { display: grid; grid-template-columns: minmax(0, .8fr) minmax(0, 1.2fr); min-height: 240px; border: 1px solid var(--border-line); background: var(--paper-card); overflow: hidden; }
.mc-art { display: grid; grid-template-columns: repeat(8, 1fr); grid-auto-rows: 1fr; background: #6b4a2b; }
.mc-art span { display: block; min-height: 30px; }
.mc-art .b1 { background: #5d8a32; } .mc-art .b2 { background: #4f7a2a; } .mc-art .b3 { background: #7a5634; }
.mc-art span:nth-child(n+17) { background: #7a5634; } .mc-art span:nth-child(n+17):nth-child(3n) { background: #6b4a2b; } .mc-art span:nth-child(n+33):nth-child(5n) { background: #8c8c8c; }
.mc-copy { padding: 28px 32px; }
.mc-copy .hero-eyebrow { color: var(--accent-color); }
.mc-copy .hero-eyebrow .st-released { color: var(--success); }
.mc-copy h4 { margin: 0; font-size: 24px; }
.mc-copy > p { margin: 10px 0 0; color: var(--text-secondary); line-height: 1.75; font-size: 14px; }
.mc-copy .btn-line { border-color: var(--border-line); color: var(--text-primary); }
.mc-copy .btn-line:hover { border-color: var(--accent-color); color: var(--accent-color); }
.btn-primary { padding: 11px 18px; border: 0; background: var(--accent-color); color: var(--on-accent); font: inherit; font-size: 14px; cursor: pointer; }

/* ---------- 社区小游戏 ---------- */
.cat-tabs { display: flex; flex-wrap: wrap; border: 1px solid var(--border-line); }
.cat-tabs button { padding: 6px 12px; border: 0; border-right: 1px solid var(--border-line); background: transparent; color: var(--text-secondary); font: inherit; font-size: 13px; cursor: pointer; }
.cat-tabs button:last-child { border-right: 0; }
.cat-tabs button.on { background: var(--accent-color); color: var(--on-accent); }
.mini-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 16px; }
.mini-card { display: flex; flex-direction: column; padding: 0; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font: inherit; text-align: left; cursor: pointer; transition: border-color .2s; }
.mini-card:hover { border-color: var(--accent-color); }
.mini-body { display: grid; gap: 10px; padding: 12px 14px 14px; flex: 1; font-size: 13px; color: var(--text-secondary); line-height: 1.6; }
.mini-tags { display: flex; align-items: center; gap: 6px; margin-top: auto; color: var(--accent-color); font-size: 12px; }
.mini-tags em { padding: 1px 6px; border: 1px solid var(--border-line); color: var(--text-secondary); font-style: normal; }
.mini-tags em.tag-new { border-color: var(--signal-mint); color: var(--signal-mint); }
.upcoming-title { margin: 8px 0 0; font-size: 13px; font-weight: 500; color: var(--text-secondary); }
.mini-card.is-dev { filter: grayscale(.9); opacity: .7; }
.mini-card.is-dev:hover { filter: none; opacity: 1; }

/* ---------- 幸运转盘入口 ---------- */
.wheel-banner { display: flex; align-items: center; gap: 20px; padding: 16px 22px; border: 1px solid #d97706; background: linear-gradient(110deg, color-mix(in srgb, #f59e0b 16%, var(--paper-card)), var(--paper-card) 65%); color: var(--text-primary); font: inherit; text-align: left; cursor: pointer; transition: box-shadow .2s, transform .2s; }
.wheel-banner:hover { transform: translateY(-2px); box-shadow: 0 10px 26px rgba(217, 119, 6, .2); }
.wb-wheel { flex: none; width: 64px; height: 64px; border-radius: 50%; border: 4px solid #20252b; box-shadow: 0 0 0 2px #fbbf24 inset; background: conic-gradient(#E4572E 0 60deg, #F3A712 0 120deg, #29335C 0 180deg, #669BBC 0 240deg, #A8C686 0 300deg, #7D5BA6 0); transition: transform 1.2s cubic-bezier(.2, .8, .3, 1); }
.wheel-banner:hover .wb-wheel { transform: rotate(300deg); }
.wb-copy { display: grid; gap: 4px; flex: 1; min-width: 0; }
.wb-copy b { font-size: 17px; }
.wb-copy span { color: var(--text-secondary); font-size: 13px; line-height: 1.7; }
.wb-go { flex: none; padding: 9px 16px; background: #f59e0b; color: #1c1206; font-weight: 600; font-size: 14px; }

@media (max-width: 900px) {
  .hero { grid-template-columns: minmax(0, 1fr); padding: 28px 22px; }
  .hero-media { order: -1; }
  .mc-card { grid-template-columns: minmax(0, 1fr); }
  .mc-art { grid-template-rows: repeat(3, 26px); overflow: hidden; }
  .mc-copy { padding: 22px; }
}
@media (max-width: 600px) { .wheel-banner { flex-wrap: wrap; gap: 12px 16px; padding: 14px; } .wb-go { width: 100%; text-align: center; } }
@media (prefers-reduced-motion: reduce) { .hero { transition: none; } .btn-steam:hover { transform: none; } .wheel-banner, .wb-wheel { transition: none; } }
</style>
