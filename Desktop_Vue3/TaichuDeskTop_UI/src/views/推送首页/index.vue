<template>
  <article class="home-page content-page">
    <div class="page-index"><span><b class="index-file">FILE 00</b>TAICHU / COMMUNITY</span><span class="index-meta"><RouterLink v-if="siteVersion" to="/changelog" class="index-version" :title="versionTitle">v{{ siteVersion }}</RouterLink>创作 · 相遇 · 共建<i class="index-barcode" aria-hidden="true"></i></span></div>
    <header ref="heroElement" class="home-hero" :class="{ 'motion-paused': !heroMotion }">
      <div class="hero-copy">
        <p class="hero-eyebrow"><span aria-hidden="true"></span>每一种想象，都值得被看见</p>
        <RouterLink v-if="siteVersion" to="/changelog" class="hero-version" :title="versionTitle">
          <span class="hv-label">{{ isLocalDev ? 'LOCAL' : 'VERSION' }}</span>
          <strong class="hv-number">v{{ siteVersion }}</strong>
          <span class="hv-text">{{ isLocalDev ? '本地运行中' : '当前版本' }}</span>
          <span v-if="betaVersion" class="hv-beta">v{{ betaVersion }} 内测中</span>
          <span class="hv-arrow" aria-hidden="true">更新说明 →</span>
        </RouterLink>
        <h1>让想象<br />成为<span class="title-outline">世界</span><span class="title-stop">.</span></h1>
        <p class="hero-intro">分享一幅画，写下一个故事。<br />从自己的灵感出发，在这里遇见同路的人。</p>
        <dl class="hero-spec">
          <div><dt>CLASS</dt><dd>创作社区</dd></div>
          <div><dt>SECTORS</dt><dd>画廊 · 博客 · 世界观</dd></div>
          <div><dt>ACCESS</dt><dd>访客可浏览</dd></div>
        </dl>
        <div class="hero-actions"><RouterLink to="/gallery" class="hero-primary">探索社区作品 <span aria-hidden="true">↗</span></RouterLink><RouterLink to="/write/blog" class="hero-secondary">开始创作 <span aria-hidden="true">→</span></RouterLink></div>
      </div>
      <div class="hero-art" data-depth="soft" data-parallax="0.06" aria-hidden="true">
        <div class="art-head"><span class="art-caption">IMAGINATION / WITHOUT LIMITS</span><span class="art-code">OBJ-001</span></div>
        <div class="diagram-stage">
        <svg class="world-diagram" viewBox="0 0 400 340" fill="none" focusable="false">
          <!-- 观测刻度盘：每 5° 一格，每 30° 加长；扫描线在标识底板之下转动。 -->
          <g class="dial-ticks" stroke="var(--text-primary)" opacity=".4"><path v-for="n in 72" :key="n" :d="dialTick(n)" /></g>
          <circle cx="200" cy="170" r="128" stroke="var(--text-primary)" opacity=".12" />
          <g class="dial-sweep"><path d="M200 170 324 106A140 140 0 0 1 340 170Z" fill="var(--signal-color)" opacity=".12" /><path d="M200 170H340" stroke="var(--signal-color)" opacity=".7" /></g>
          <path d="M0 18H186L88 116H0ZM300 260H400V340H220Z" fill="var(--signal-color)" opacity=".14" />
          <path d="M18 110H62V44H180M240 310H378V220M24 170H376M200 18V322" stroke="currentColor" stroke-opacity=".2" />
          <g class="brand-orbit" stroke="var(--accent-color)"><circle cx="200" cy="170" r="144" stroke-dasharray="170 45 28 65" opacity=".5" /><circle cx="200" cy="26" r="4" fill="var(--signal-color)" stroke="none" /></g>
          <g class="brand-halo" stroke="var(--signal-mint)" opacity=".45"><ellipse cx="200" cy="170" rx="185" ry="112" transform="rotate(-24 200 170)" /><path d="M18 24H64M18 24V70M336 316H382V270" /></g>
          <path class="brand-frame" d="M200 28 323 99V241L200 312 77 241V99Z" stroke="var(--signal-mint)" stroke-width="1.5" opacity=".65" />
          <g class="brand-sliders" fill="var(--signal-mint)"><path d="M16 88H62L46 104H0Z" opacity=".6" /><path d="M338 236H384L400 220H354Z" opacity=".4" /></g>
          <path d="M8 144V68L60 16H170M230 324H340L392 272V196" stroke="var(--accent-color)" opacity=".3" />
          <path class="brand-circuit" d="M8 144V68L60 16H170M230 324H340L392 272V196" stroke="var(--accent-color)" stroke-width="3" stroke-linecap="square" pathLength="100" stroke-dasharray="14 36" />
          <path d="M72 42H314L328 56V298H72Z" fill="var(--inverse-bg)" />
          <path class="brand-signal" d="M12 292H58L90 260M310 80L344 46H388" stroke="var(--accent-color)" pathLength="100" />
          <g fill="var(--accent-color)"><circle cx="42" cy="210" r="2" /><circle cx="352" cy="140" r="2" /><path d="M336 284h12m-6-6v12" stroke="currentColor" /></g>
          <g class="dial-label" fill="var(--text-secondary)"><text x="10" y="334">X 200 · Y 170 · R 146</text><text x="390" y="12" text-anchor="end">SCAN 05°</text></g>
        </svg>
          <!-- 标识底板上的粒子：站标 → 文字 → 几何徽记循环变形，鼠标靠近会把粒子推开。 -->
          <ParticleMorph class="brand-particles" :shapes="brandShapes" />
        </div>
        <div class="art-footer"><span>从一个灵感，到无限可能</span><span>+ + +</span></div>
      </div>
    </header>

    <nav class="discover-strip" aria-label="探索社区内容">
      <RouterLink to="/gallery"><span class="discover-no">01</span><span><strong>画廊</strong><small>发现画作与灵感</small></span><span class="discover-arrow" aria-hidden="true">↗</span></RouterLink>
      <RouterLink to="/stream"><span class="discover-no">02</span><span><strong>论坛</strong><small>讨论、分享，按版块逛</small></span><span class="discover-arrow" aria-hidden="true">↗</span></RouterLink>
      <RouterLink to="/world"><span class="discover-no">03</span><span><strong>世界观</strong><small>走进彼此创造的世界</small></span><span class="discover-arrow" aria-hidden="true">↗</span></RouterLink>
    </nav>

    <ArtworkShowcase />

    <ForumHighlights />

    <section class="home-stats" aria-label="社区数据"><DataPanel /></section>

    <div class="content-layout">
      <section class="main-content" aria-label="社区动态与更新">
        <ServerQuestPanel class="home-block" />
        <OnThisDayPanel class="home-block" />
        <div class="home-block feed-block"><div class="block-index" aria-hidden="true">01 / COMMUNITY</div><CommunityFeed /></div>
        <div class="home-block news-block"><div class="block-index" aria-hidden="true">02 / NOTICES</div><LatestNews /></div>
        <div class="home-block"><div class="block-index" aria-hidden="true">03 / DEVELOPMENT</div><DevProgress /></div>
        <RouterLink to="/changelog" class="release-link"><span><strong>版本更新<em v-if="siteVersion" class="release-current">当前版本 v{{ siteVersion }}</em></strong><small>{{ betaVersion ? `v${betaVersion} 正在内测，` : '' }}查看更新说明与开发记录</small></span><span aria-hidden="true">↗</span></RouterLink>
      </section>
      <aside class="side-widgets" aria-label="社区服务与数据">
        <div class="home-block"><SignCard /></div>
        <div class="home-block"><CommunityLinks /></div>
        <div class="home-block"><FinancePanel /></div>
        <div class="home-block"><feedback /></div>
      </aside>
    </div>
    <!-- 页尾粒子带：社区名 → 站标 → 标语，循环变形；鼠标划过会把粒子拨开。 -->
    <div class="footer-particles" aria-hidden="true">
      <ParticleMorph :shapes="footerShapes" :interval="5000" :fill="0.96" :density="38" :colors="['--accent-color', '--signal-mint', '--text-secondary']" />
    </div>
    <footer class="home-footer"><span><i class="footer-caution" aria-hidden="true"></i>TAICHU HUANYU · END OF INDEX</span><span>想象不设限 · 创作有回响</span><span v-if="siteVersion" class="footer-version">VERSION {{ siteVersion }}</span><span class="footer-icp"><a class="icp-link" href="https://beian.miit.gov.cn/" target="_blank" rel="noopener noreferrer">京ICP备2022020268号-2</a></span></footer>
  </article>
</template>
<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useVisibleMotion } from '@/composables/useVisibleMotion'
import { releaseApi, type ReleaseItem } from '@/api/release'
import { versionSummary, formatBuildTime, IS_LOCAL_DEV } from '@/utils/appVersion'

// 当前版本号：自动取「打包时间之前发布的最新版本」，不用手动改（见 utils/appVersion.ts）
const releases = ref<ReleaseItem[]>([])
const versions = computed(() => versionSummary(releases.value))
const siteVersion = computed(() => versions.value.current?.version ?? '')
const betaVersion = computed(() => versions.value.beta?.version ?? '')
const isLocalDev = IS_LOCAL_DEV
const versionTitle = computed(() => `${IS_LOCAL_DEV ? '本地开发版' : '当前版本'} v${siteVersion.value}，打包于 ${formatBuildTime()}${betaVersion.value ? `；v${betaVersion.value} 内测中` : ''}`)
onMounted(async () => { try { releases.value = await releaseApi.list() } catch { /* 读不到就不显示版本号 */ } })
const heroElement = ref<HTMLElement>()
const heroMotion = useVisibleMotion(heroElement)
const footerShapes: ParticleShape[] = [
  { kind: 'text', text: 'TAICHU HUANYU', mono: true, scale: 0.34 },
  { kind: 'image', src: '/taichu-logo.png', scale: 0.9 },
  { kind: 'text', text: '想象不设限', scale: 0.42 },
]
const brandShapes: ParticleShape[] = [
  { kind: 'image', src: '/taichu-logo.png' },
  { kind: 'text', text: '太初', scale: 0.5 },
  { kind: 'draw', draw: targetShape },
  { kind: 'text', text: '寰宇', scale: 0.5 },
  { kind: 'draw', draw: diamondShape },
  { kind: 'draw', draw: hexShape },
]
// 刻度盘第 n 格的线段：每 6 格（30°）为主刻度。
function dialTick(n: number) {
  const angle = (n * 5 * Math.PI) / 180
  const inner = n % 6 === 0 ? 132 : 139
  const point = (r: number) => `${(200 + Math.cos(angle) * r).toFixed(1)} ${(170 + Math.sin(angle) * r).toFixed(1)}`
  return `M${point(inner)}L${point(146)}`
}
import ArtworkShowcase from './components/ArtworkShowcase.vue'
import ParticleMorph from '@/components/ParticleMorph.vue'
import { diamondShape, hexShape, targetShape, type ParticleShape } from '@/utils/particleShapes'
import ForumHighlights from './components/ForumHighlights.vue'
import SignCard from '../../components/SignCard.vue'
import feedback from './components/feedback.vue'
import CommunityFeed from './components/CommunityFeed.vue'
import ServerQuestPanel from './components/ServerQuestPanel.vue'
import OnThisDayPanel from './components/OnThisDayPanel.vue'
import LatestNews from './components/LatestNews.vue'
import DataPanel from './components/DataPanel.vue'
import FinancePanel from './components/FinancePanel.vue'
import CommunityLinks from './components/CommunityLinks.vue'
import DevProgress from './components/DevProgress.vue'
</script>
<style scoped>
.motion-paused *, .motion-paused::after, .motion-paused .hero-art::after { animation-play-state: paused !important; }
.home-page { max-width: 1280px; padding-bottom: 0; }
.page-index, .home-footer { display: flex; justify-content: space-between; gap: 16px; font-size: 11px; letter-spacing: .1em; color: var(--text-secondary); }
.page-index { border-bottom: 1px solid var(--text-primary); padding: 0 0 16px; }
.page-index > :first-child, .block-index, .discover-no, .art-caption, .home-footer > :first-child { font-family: var(--font-mono); }
.home-hero { position: relative; display: grid; grid-template-columns: minmax(0, 1.2fr) minmax(0, 1fr); gap: 24px; padding: 40px 0; align-items: center; }
.hero-eyebrow { display: flex; gap: 9px; align-items: center; font-size: 12px; margin: 0 0 20px; }
.hero-eyebrow > span { width: 7px; height: 7px; background: var(--signal-color); }
/* 首页版本号：标题上方一枚醒目的标签，点了看更新说明 */
.hero-version { display: inline-flex; align-items: stretch; flex-wrap: wrap; max-width: 100%; margin: -6px 0 18px; border: 1px solid var(--accent-color); background: var(--paper-card); color: var(--text-primary); text-decoration: none; font-size: 13px; line-height: 1; box-shadow: 3px 3px 0 var(--accent-soft); transition: box-shadow .2s, transform .2s; }
.hero-version > * { display: inline-flex; align-items: center; padding: 8px 10px; }
.hv-label { background: var(--accent-color); color: var(--on-accent); font: 700 10px/1 var(--font-mono); letter-spacing: .16em; }
.hv-number { padding-right: 4px; font-family: var(--font-mono); font-size: 17px; font-weight: 700; letter-spacing: -.02em; color: var(--accent-color); }
.hv-text { padding-left: 0; color: var(--text-secondary); font-size: 12px; }
.hv-beta { border-left: 1px dashed var(--border-line); color: #b45309; font-size: 12px; font-weight: 600; }
.hv-arrow { border-left: 1px solid var(--border-line); color: var(--text-secondary); font-size: 12px; }
.hero-version:is(:hover, :focus-visible) { transform: translate(-1px, -1px); box-shadow: 5px 5px 0 var(--accent-soft); }
.hero-version:is(:hover, :focus-visible) .hv-arrow { color: var(--accent-color); }
@media (max-width: 520px) { .hv-arrow { display: none; } .hero-version > * { padding: 7px 8px; } }
@media (prefers-reduced-motion: reduce) { .hero-version { transition: none; } .hero-version:is(:hover, :focus-visible) { transform: none; } }
.home-page.content-page h1 { font-family: var(--font-display); font-size: clamp(42px, 4.8vw, 76px); line-height: 1.16; letter-spacing: -.055em; margin: 0; font-weight: 800; }
.title-outline { position: relative; color: transparent; -webkit-text-stroke: 1px var(--text-primary); }
.title-outline::after { content: ''; position: absolute; z-index: -1; left: 0; right: -4px; bottom: -.1em; height: .13em; background: color-mix(in srgb,var(--signal-mint) 50%,transparent); transform: skewX(-12deg); }
.hero-copy { isolation: isolate; }
.title-stop { color: var(--signal-color); padding-left: 4px; }
.hero-intro { font-size: 14px; line-height: 1.9; margin: 24px 0; }
.hero-actions { display: flex; align-items: center; flex-wrap: wrap; gap: 12px; }
.hero-actions a { display: inline-flex; gap: 24px; align-items: center; justify-content: space-between; padding: 12px 18px; min-height: 46px; text-decoration: none; font-size: 13px; border: 1px solid var(--text-primary); transition: background .2s, color .2s; }
.hero-actions a span { transition: transform .25s; }
.hero-actions a:hover span, .hero-actions a:focus-visible span { transform: translate(3px, -3px); }
.hero-primary { background: var(--text-primary); color: var(--bg-main); }
.hero-primary:hover { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); }
.hero-secondary { color: var(--text-primary); }
.hero-secondary:is(:hover,:focus-visible) { background: color-mix(in srgb,var(--signal-mint) 9%,var(--paper-card)); border-color: var(--accent-mint); }
.hero-art { position: relative; min-width: 0; color: var(--text-primary); padding: 18px; background: color-mix(in srgb,var(--paper-card) 76%,transparent); border: 0; border-top: 1px solid var(--border-line); overflow: hidden; box-shadow: 14px 14px 0 color-mix(in srgb,var(--text-primary) 4%,transparent); }
.hero-art::after { content: ''; position: absolute; inset: 0 auto 0 0; width: 2px; background: var(--signal-color); pointer-events: none; transform: translateX(-4px); }
.art-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.art-code { padding: 4px 8px; font: 700 10px/1 var(--font-mono); letter-spacing: .14em; color: var(--caution-ink); background: var(--caution); }
.art-caption { display: inline-block; padding: 5px 9px; border-left: 2px solid var(--signal-mint); background: var(--inverse-bg); color: var(--inverse-text); font-size: 10px; letter-spacing: .12em; }
.brand-piece { color: var(--inverse-text); }
.dial-sweep { transform-origin: 200px 170px; }
.dial-label { font: 9px var(--font-mono); letter-spacing: .1em; }
/* 舞台保持与 SVG 相同的宽高比，粒子画布才能按百分比精确盖在标识底板上。 */
.diagram-stage { position: relative; width: min(100%, 400px); aspect-ratio: 400 / 340; margin: 12px auto; }
.world-diagram { display: block; width: 100%; height: 100%; }
.brand-particles { position: absolute; left: 18%; top: 12.35%; width: 64%; height: 75.3%; }
/* 主视觉的鼠标视差：粒子层朝鼠标方向、线框层反向，拉开前后距离。 */
.hero-art .brand-particles { translate: calc(var(--depth-x, 0) * 14px) calc(var(--depth-y, 0) * 10px); transition: translate .3s ease-out; }
.hero-art .world-diagram { translate: calc(var(--depth-x, 0) * -6px) calc(var(--depth-y, 0) * -5px); transition: translate .3s ease-out; }
.footer-particles { height: 120px; margin-top: 32px; border-top: 1px dashed var(--border-line); }
.footer-particles :deep(.particle-morph) { width: 100%; height: 100%; }
@media (max-width: 640px) { .footer-particles { height: 80px; } }
.art-footer { display: flex; justify-content: space-between; gap: 12px; color: var(--text-secondary); font-size: 11px; }
.discover-strip { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); border-block: 1px solid var(--text-primary); }
.discover-strip a { position: relative; display: flex; align-items: center; gap: 16px; min-width: 0; padding: 23px 20px; color: var(--text-primary); text-decoration: none; border-right: 1px solid var(--border-line); transition: background .2s; }
.discover-strip a::after { content: ''; position: absolute; inset: auto 0 0; height: 3px; background: var(--signal-color); transform: scaleX(0); transform-origin: left; transition: transform .3s; }
.discover-strip a:hover::after, .discover-strip a:focus-visible::after { transform: scaleX(1); }
.discover-strip a:nth-child(2)::after { background: linear-gradient(90deg,var(--signal-color),var(--signal-mint)); }
.discover-strip a:nth-child(3)::after { background: var(--signal-mint); }
.discover-strip a:first-child { padding-left: 0; }
.discover-strip a:last-child { border-right: 0; }
.discover-strip a:hover { background: var(--bg-sub); }
.discover-no { align-self: flex-start; font-size: 11px; color: var(--accent-color); }
.discover-strip strong { font-size: 16px; font-weight: 600; }
.discover-strip small { display: block; font-size: 12px; margin-top: 5px; }
.discover-arrow { margin-left: auto; font-size: 21px; transition: transform .25s; }
.discover-strip a:hover .discover-arrow { transform: translate(3px, -3px); }
.content-layout { display: grid; grid-template-columns: minmax(0, 1fr) minmax(260px, .48fr); gap: 32px; margin-top: 36px; align-items: start; }
.main-content, .side-widgets { display: grid; min-width: 0; gap: 32px; }
.side-widgets { border-left: 1px solid var(--border-line); padding-left: 28px; }
.home-block { min-width: 0; border-bottom: 1px solid var(--border-line); padding-bottom: 28px; }
.block-index { color: var(--text-secondary); font-size: 10px; letter-spacing: .12em; margin-bottom: 14px; }
/* 只整合首页组件根容器，不改弹窗、正文或内部控件。 */
.home-block > :deep(:not(.block-index)) { margin: 0; padding: 0; background: transparent; border: 0; border-radius: 0; box-shadow: none; }
.home-block :deep(h2) { font-size: 19px; font-weight: 650; letter-spacing: -.02em; }
.news-block :deep(.md-header) { margin-bottom: 20px; }
.news-block :deep(.news-featured) { border-radius: 0; padding: 20px; margin-bottom: 16px; background: var(--paper-card); }
.news-block :deep(.featured-title) { font-size: 19px; }
.news-block :deep(.featured-badge) { border-radius: 0; }
.news-block :deep(.pulse-dot) { animation: none; background: currentColor; }
.home-footer { border-top: 1px solid var(--text-primary); padding: 20px 0; margin-top: 24px; flex-wrap: wrap; }
/* ICP 备案号：按规定放在首页底部，链接到工信部备案系统 */
.footer-version { font-family: var(--font-mono); }
.index-version { margin-right: 12px; font-family: var(--font-mono); color: var(--accent-color); text-decoration: none; letter-spacing: .06em; }
.index-version:hover { text-decoration: underline; }
.release-current { margin-left: 10px; padding: 1px 7px; background: var(--accent-soft); color: var(--accent-color); font-family: var(--font-mono); font-size: 11px; font-style: normal; font-weight: 600; letter-spacing: .04em; vertical-align: 2px; }
.footer-icp { flex-basis: 100%; text-align: center; letter-spacing: .04em; }
.icp-link { color: var(--text-secondary); text-decoration: none; }
.icp-link:hover, .icp-link:focus-visible { color: var(--accent-color); text-decoration: underline; text-underline-offset: 3px; }
/* 使用社区原始 Logo 蒙版；片段归位后保持完整标识，环线持续缓慢运行。 */
.brand-orbit, .brand-halo { transform-origin: 200px 170px; }
.brand-frame { transform-origin: 200px 170px; }
@media(prefers-reduced-motion: no-preference) {
  .brand-frame { animation: brand-orbit 18s linear infinite reverse; }
  .brand-sliders { animation: brand-slide 3.6s ease-in-out infinite alternate; }
  .brand-circuit { animation: brand-signal 3.4s linear -1s infinite; }
  .hero-copy { animation: hero-enter .7s ease-out both; }
  .brand-orbit { animation: brand-orbit 28s linear infinite; }
  .brand-piece { animation: brand-assemble 1.2s cubic-bezier(.2,.7,.2,1) both; }
  .brand-signal { stroke-dasharray: 18 82; animation: brand-signal 4.8s linear infinite; }
  .brand-halo { animation: brand-halo 14s ease-in-out infinite alternate; }
  .dial-sweep { animation: brand-orbit 9s linear infinite; }
  .title-outline::after, .page-index { transform-origin: left; animation: title-mark .9s ease-out both; }
}
@keyframes brand-halo { to { transform: rotate(10deg) scale(.96); opacity: .72; } }
@keyframes brand-slide { to { transform: translate(22px,-18px); } }
@keyframes brand-orbit { to { transform: rotate(360deg); } }
@keyframes brand-assemble { 0%, 8% { opacity: .7; transform: translate(var(--piece-x), var(--piece-y)); } 24%, 88%, 100% { opacity: 1; transform: translate(0,0); } 96% { transform: translate(calc(var(--piece-x) * .5),calc(var(--piece-y) * .5)); } }
@keyframes brand-signal { to { stroke-dashoffset: -100; } }
@keyframes hero-enter { from { opacity: 0; transform: translateY(14px); } to { opacity: 1; transform: none; } }
@keyframes title-mark { from { opacity: 0; scale: 0 1; } to { opacity: 1; scale: 1 1; } }
@media (max-width: 1100px) { .content-layout { grid-template-columns: minmax(0, 1fr) 260px; gap: 24px; }.side-widgets { padding-left: 20px; }.hero-actions a { gap: 18px; padding-inline: 13px; }.discover-strip a { padding-inline: 12px; gap: 12px; } }
@media (max-width: 700px) {
  .home-hero { padding: 32px 0; grid-template-columns: 1fr; gap: 28px; }
  .home-page.content-page h1 { font-size: clamp(44px, 10vw, 64px); }
  .hero-art { padding: 16px; }
  .diagram-stage { width: min(100%, 259px); margin-block: 4px; }
  .discover-strip { grid-template-columns: 1fr; }
  .discover-strip a, .discover-strip a:first-child { border-right: 0; border-bottom: 1px solid var(--border-line); padding: 16px 4px; }
  .discover-strip a:last-child { border-bottom: 0; }
  .discover-strip small { margin-top: 2px; }
  .content-layout { grid-template-columns: 1fr; margin-top: 28px; }
  .side-widgets { border-left: 0; padding-left: 0; }
  .page-index, .home-footer { font-size: 10px; letter-spacing: .02em; }
}
.home-stats { margin-top: 24px; }
.home-stats :deep(.data-grid), .home-stats :deep(.panel-loading) { grid-template-columns: repeat(6, minmax(0,1fr)); }
@media(max-width: 1100px) { .home-stats :deep(.data-grid), .home-stats :deep(.panel-loading) { grid-template-columns: repeat(3, minmax(0,1fr)); } }
@media(max-width: 480px) { .home-stats :deep(.data-grid), .home-stats :deep(.panel-loading) { grid-template-columns: repeat(2, minmax(0,1fr)); } }
/* 入口有明确的背景流动和按压反馈，静态正文不参与位移。 */
.release-link { display: flex; align-items: center; justify-content: space-between; gap: 20px; padding: 18px 20px; border: 1px solid var(--border-line); color: var(--text-primary); text-decoration: none; background: linear-gradient(115deg, var(--paper-card), var(--accent-soft)); }
.release-link small { display: block; color: var(--text-secondary); margin-top: 6px; font-size: 12px; }.release-link > :last-child { font-size: 24px; transition: transform .25s; }
.release-link:is(:hover,:focus-visible) { border-color: var(--accent-color); box-shadow: inset 3px 0 var(--signal-color); }.release-link:is(:hover,:focus-visible) > :last-child { transform: translate(3px,-3px); }
.discover-strip a { background-image: linear-gradient(110deg, var(--paper-card), var(--accent-soft), var(--paper-card)); background-size: 220% 100%; transition: background-position .45s, box-shadow .2s; }
.discover-strip a:nth-child(2) { background-image: linear-gradient(110deg, var(--paper-card), color-mix(in srgb, #7b8df3 12%, var(--paper-card)), var(--paper-card)); }
.discover-strip a:nth-child(3) { background-image: linear-gradient(110deg, var(--paper-card), color-mix(in srgb, #3fc1aa 12%, var(--paper-card)), var(--paper-card)); }
.discover-strip a:is(:hover,:focus-visible) { background-position: 100% 50%; box-shadow: inset 0 -3px var(--signal-color); }
.home-page :deep(.feed-item) { padding-inline: 8px; border-left: 2px solid transparent; transition: background .2s, border-color .2s; }
.home-page :deep(.feed-item:is(:hover,:focus-visible)) { background: var(--accent-soft); border-left-color: var(--accent-color); }
.home-page :is(.hero-actions a, .discover-strip a, .release-link):active { box-shadow: inset 0 0 0 2px var(--accent-color); }
@media(prefers-reduced-motion: no-preference) {
  .hero-art::after { width: 32%; background: linear-gradient(90deg, transparent, color-mix(in srgb, var(--signal-color) 12%, transparent), transparent); left: 0; transform: translateX(-120%) skewX(-18deg); animation: ambient-scan 10s ease-in-out 3s infinite; }
}
@keyframes ambient-scan { 0%,65% { opacity: 0; transform: translateX(-120%) skewX(-18deg); } 72% { opacity: 1; } 95%,100% { opacity: 0; transform: translateX(420%) skewX(-18deg); } }
@media(prefers-reduced-motion: reduce) { .release-link > :last-child, .discover-strip a, .home-page :deep(.feed-item) { transition: none; }.release-link:is(:hover,:focus-visible) > :last-child { transform: none; } }
/* —— 实验室文档化：编号、规格表、角标框、警戒条 —— */
.page-index { align-items: center; }
.index-file { margin-right: 14px; padding: 3px 7px; font-weight: 400; color: var(--inverse-text); background: var(--inverse-bg); }
.index-meta { display: inline-flex; align-items: center; gap: 14px; }
.index-barcode { width: 64px; height: 14px; background: repeating-linear-gradient(90deg, var(--text-primary) 0 1px, transparent 1px 3px, var(--text-primary) 3px 6px, transparent 6px 7px, var(--text-primary) 7px 8px, transparent 8px 11px); opacity: .75; }
.hero-spec { display: grid; grid-template-columns: repeat(3, auto); width: fit-content; max-width: 100%; margin: 0 0 28px; border: 1px solid var(--border-line); background: color-mix(in srgb, var(--paper-card) 85%, transparent); }
.hero-spec > div { padding: 9px 16px 10px; border-left: 1px solid var(--border-line); }
.hero-spec > div:first-child { border-left: 0; }
.hero-spec dt { font: 9px/1.4 var(--font-mono); letter-spacing: .16em; color: var(--accent-color); }
.hero-spec dd { margin: 3px 0 0; font-size: 13px; font-weight: 600; white-space: nowrap; }
.hero-intro { margin-bottom: 20px; }
/* 角标框：四角 L 形细线，不额外占用 DOM。 */
.hero-art, .release-link {
  background-image:
    linear-gradient(var(--bracket-color), var(--bracket-color)), linear-gradient(var(--bracket-color), var(--bracket-color)),
    linear-gradient(var(--bracket-color), var(--bracket-color)), linear-gradient(var(--bracket-color), var(--bracket-color)),
    linear-gradient(var(--bracket-color), var(--bracket-color)), linear-gradient(var(--bracket-color), var(--bracket-color)),
    linear-gradient(var(--bracket-color), var(--bracket-color)), linear-gradient(var(--bracket-color), var(--bracket-color));
  background-repeat: no-repeat;
  background-size: var(--bracket-size) 2px, 2px var(--bracket-size), var(--bracket-size) 2px, 2px var(--bracket-size), var(--bracket-size) 2px, 2px var(--bracket-size), var(--bracket-size) 2px, 2px var(--bracket-size);
  background-position: 0 0, 0 0, 100% 0, 100% 0, 0 100%, 0 100%, 100% 100%, 100% 100%;
}
.hero-art { border-top: 0; background-color: color-mix(in srgb, var(--paper-card) 82%, transparent); outline: 1px solid var(--border-line); outline-offset: -6px; }
.release-link { background-color: var(--paper-card); }
.hero-primary { clip-path: polygon(0 0, calc(100% - var(--chamfer)) 0, 100% var(--chamfer), 100% 100%, var(--chamfer) 100%, 0 calc(100% - var(--chamfer))); }
.hero-primary:focus-visible { outline-offset: -5px; outline-color: var(--signal-color); }
.discover-no { font: 600 28px/1 var(--font-display); letter-spacing: -.02em; color: var(--accent-color); }
.discover-no::after { content: ''; display: block; width: 100%; height: 2px; margin-top: 6px; background: var(--signal-color); }
.block-index { display: flex; align-items: center; gap: 8px; }
.block-index::before { content: ''; width: 6px; height: 6px; background: var(--signal-color); }
.block-index::after { content: ''; flex: 1; height: 1px; background: repeating-linear-gradient(90deg, var(--border-line) 0 4px, transparent 4px 8px); }
.footer-caution { display: inline-block; width: 40px; height: 8px; margin-right: 12px; vertical-align: middle; background: var(--hazard-stripe); }
@media (max-width: 700px) {
  .hero-spec { grid-template-columns: 1fr; width: 100%; }
  .hero-spec > div { border-left: 0; border-top: 1px solid var(--border-line); display: flex; justify-content: space-between; align-items: baseline; gap: 12px; }
  .hero-spec > div:first-child { border-top: 0; }
  .hero-spec dd { margin: 0; }
  .index-barcode, .index-file { display: none; }
  .discover-no { font-size: 22px; }
}
</style>
