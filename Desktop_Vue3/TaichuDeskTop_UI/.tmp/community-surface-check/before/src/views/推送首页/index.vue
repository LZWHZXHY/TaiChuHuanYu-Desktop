<template>
  <article class="home-page content-page">
    <div class="page-index"><span>TAICHU / COMMUNITY</span><span>创作 · 相遇 · 共建</span></div>
    <header class="home-hero">
      <div class="hero-copy">
        <p class="hero-eyebrow"><span aria-hidden="true"></span>每一种想象，都值得被看见</p>
        <h1>让想象<br />成为<span class="title-outline">世界</span><span class="title-stop">.</span></h1>
        <p class="hero-intro">分享一幅画，写下一个故事。<br />从自己的灵感出发，在这里遇见同路的人。</p>
        <div class="hero-actions"><RouterLink to="/gallery" class="hero-primary">探索社区作品 <span aria-hidden="true">↗</span></RouterLink><RouterLink to="/write/blog" class="hero-secondary">开始创作 <span aria-hidden="true">→</span></RouterLink></div>
      </div>
      <div class="hero-art" aria-hidden="true">
        <span class="art-caption">IMAGINATION / WITHOUT LIMITS</span>
        <svg class="world-diagram" viewBox="0 0 400 340" fill="none">
          <defs><pattern id="world-grid" width="28" height="28" patternUnits="userSpaceOnUse"><path d="M28 0H0V28" stroke="currentColor" stroke-opacity=".12" stroke-width=".7" /></pattern></defs>
          <rect x="0" y="0" width="400" height="340" fill="url(#world-grid)" />
          <g class="diagram-base"><path d="M34 88H124V178H34Z" fill="var(--signal-secondary)" /><path d="M278 206H368V296H278Z" fill="var(--text-primary)" /><path d="M306 218V284M318 218V284M330 218V284M342 218V284" stroke="var(--bg-main)" stroke-opacity=".45" /></g>
          <g stroke="currentColor">
            <path d="M24 170H376M200 15V325" stroke-opacity=".2" stroke-dasharray="3 6" />
            <g class="diagram-orbit"><circle cx="200" cy="170" r="130" stroke-opacity=".3" /><ellipse cx="200" cy="170" rx="154" ry="48" transform="rotate(-30 200 170)" stroke-opacity=".6" /><circle cx="333" cy="94" r="4" fill="var(--signal-color)" stroke="none" /></g>
            <path class="diagram-outline" pathLength="1" d="M200 58 296 114 296 226 200 282 104 226 104 114Z" stroke-width="1.2" />
            <path class="diagram-outline" pathLength="1" d="M104 114 200 170 296 114M200 170V282M200 58V170M104 226 200 170 296 226" stroke-opacity=".45" />
            <g class="diagram-planes">
              <path class="plane-left" d="M104 114 200 170V282L104 226Z" fill="var(--text-primary)" fill-opacity=".9" stroke="none" />
              <path class="plane-right" d="M200 170 296 114V226L200 282Z" fill="var(--signal-secondary)" stroke="none" />
              <path class="plane-top" d="M104 114 200 58 296 114 200 170Z" fill="var(--signal-color)" stroke="none" />
              <path class="plane-top" d="M128 114 200 72 272 114 200 156Z" stroke="var(--bg-main)" stroke-opacity=".5" />
            </g>
            <path class="diagram-leaders" pathLength="1" d="M84 226H40V270M296 114H354V70M200 282V313H260" stroke-opacity=".55" />
            <path d="M20 36V20H36M364 20H380V36M20 304V320H36M364 320H380V304" stroke-width="1.5" />
          </g>
          <g fill="currentColor"><circle cx="104" cy="226" r="3" /><circle cx="296" cy="114" r="3" /><circle cx="200" cy="282" r="3" /></g>
          <g fill="currentColor" font-size="9" font-family="monospace" letter-spacing="1"><text x="24" y="287">IDEA</text><text x="322" y="59">CREATE</text><text x="265" y="316">CONNECT</text></g>
        </svg>
        <div class="art-footer"><span>从一个灵感，到无限可能</span><span>+ + +</span></div>
      </div>
    </header>

    <nav class="discover-strip" aria-label="探索社区内容">
      <RouterLink to="/gallery"><span class="discover-no">01</span><span><strong>画廊</strong><small>发现画作与灵感</small></span><span class="discover-arrow" aria-hidden="true">↗</span></RouterLink>
      <RouterLink to="/stream"><span class="discover-no">02</span><span><strong>博客与帖子</strong><small>阅读故事，参与讨论</small></span><span class="discover-arrow" aria-hidden="true">↗</span></RouterLink>
      <RouterLink to="/world"><span class="discover-no">03</span><span><strong>世界观</strong><small>走进彼此创造的世界</small></span><span class="discover-arrow" aria-hidden="true">↗</span></RouterLink>
    </nav>

    <section class="home-stats" aria-label="社区数据"><DataPanel /></section>

    <div class="content-layout">
      <section class="main-content" aria-label="社区动态与更新">
        <div class="home-block feed-block"><div class="block-index" aria-hidden="true">01 / COMMUNITY</div><CommunityFeed /></div>
        <div class="home-block news-block"><div class="block-index" aria-hidden="true">02 / NOTICES</div><LatestNews /></div>
        <div class="home-block"><div class="block-index" aria-hidden="true">03 / DEVELOPMENT</div><DevProgress /></div>
        <RouterLink to="/changelog" class="release-link"><span><strong>版本更新</strong><small>查看更新说明与开发记录</small></span><span aria-hidden="true">↗</span></RouterLink>
      </section>
      <aside class="side-widgets" aria-label="社区服务与数据">
        <div class="home-block"><SignCard /></div>
        <div class="home-block"><CommunityLinks /></div>
        <div class="home-block"><FinancePanel /></div>
        <div class="home-block"><feedback /></div>
      </aside>
    </div>
    <footer class="home-footer"><span>TAICHU HUANYU</span><span>想象不设限 · 创作有回响</span></footer>
  </article>
</template>
<script setup lang="ts">
import SignCard from '../../components/SignCard.vue'
import feedback from './components/feedback.vue'
import CommunityFeed from './components/CommunityFeed.vue'
import LatestNews from './components/LatestNews.vue'
import DataPanel from './components/DataPanel.vue'
import FinancePanel from './components/FinancePanel.vue'
import CommunityLinks from './components/CommunityLinks.vue'
import DevProgress from './components/DevProgress.vue'
</script>
<style scoped>
.home-page { max-width: 1280px; padding-bottom: 0; }
.page-index, .home-footer { display: flex; justify-content: space-between; gap: 16px; font-size: 11px; letter-spacing: .1em; color: var(--text-secondary); }
.page-index { border-bottom: 1px solid var(--text-primary); padding: 0 0 16px; }
.page-index > :first-child, .block-index, .discover-no, .art-caption, .home-footer > :first-child { font-family: var(--font-mono); }
.home-hero { position: relative; display: grid; grid-template-columns: minmax(0, 1.2fr) minmax(0, 1fr); gap: 24px; padding: 40px 0; align-items: center; }
.hero-eyebrow { display: flex; gap: 9px; align-items: center; font-size: 12px; margin: 0 0 20px; }
.hero-eyebrow > span { width: 7px; height: 7px; background: var(--signal-color); }
.home-page.content-page h1 { font-size: clamp(42px, 4.8vw, 76px); line-height: 1.16; letter-spacing: -.055em; margin: 0; font-weight: 800; }
.title-outline { position: relative; color: transparent; -webkit-text-stroke: 1px var(--text-primary); }
.title-outline::after { content: ''; position: absolute; z-index: -1; left: 0; right: -4px; bottom: -.1em; height: .13em; background: var(--signal-secondary); transform: skewX(-12deg); }
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
.hero-secondary:hover { background: var(--bg-sub); }
.hero-art { position: relative; min-width: 0; color: var(--text-primary); padding: 18px; background: var(--diagram-paper); border: 1px solid var(--border-line); overflow: hidden; clip-path: polygon(0 0, calc(100% - 18px) 0, 100% 18px, 100% 100%, 18px 100%, 0 calc(100% - 18px)); }
.hero-art::after { content: ''; position: absolute; inset: 0 auto 0 0; width: 2px; background: var(--signal-color); pointer-events: none; transform: translateX(-4px); }
.art-caption { display: block; font-size: 10px; letter-spacing: .12em; color: var(--text-secondary); }
.world-diagram { display: block; width: 100%; max-height: 340px; margin: 12px 0; }
.art-footer { display: flex; justify-content: space-between; gap: 12px; color: var(--text-secondary); font-size: 11px; }
.discover-strip { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); border-block: 1px solid var(--text-primary); }
.discover-strip a { position: relative; display: flex; align-items: center; gap: 16px; min-width: 0; padding: 23px 20px; color: var(--text-primary); text-decoration: none; border-right: 1px solid var(--border-line); transition: background .2s; }
.discover-strip a::after { content: ''; position: absolute; inset: auto 0 0; height: 3px; background: var(--signal-color); transform: scaleX(0); transform-origin: left; transition: transform .3s; }
.discover-strip a:hover::after, .discover-strip a:focus-visible::after { transform: scaleX(1); }
.discover-strip a:nth-child(2)::after { background: var(--signal-secondary); }
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
.news-block :deep(.news-featured) { border-radius: 3px; padding: 20px; margin-bottom: 16px; background: var(--paper-card); }
.news-block :deep(.featured-title) { font-size: 19px; }
.news-block :deep(.featured-badge) { border-radius: 2px; }
.news-block :deep(.pulse-dot) { animation: none; background: currentColor; }
.home-footer { border-top: 1px solid var(--text-primary); padding: 20px 0; margin-top: 24px; }
/* 几何依次入场、交叠并归位；不持续占用阅读注意力。悬停/键盘操作时再展开。 */
.diagram-orbit, .diagram-base, .diagram-planes { transform-origin: 200px 170px; }
.diagram-orbit { transition: transform .8s cubic-bezier(.2,.7,.2,1); }
.plane-top, .plane-left, .plane-right { transition: transform .65s cubic-bezier(.2,.7,.2,1); }
.home-hero:is(:hover, :focus-within) .plane-top { transform: translateY(-16px); }
.home-hero:is(:hover, :focus-within) .plane-left { transform: translate(-12px, 8px); }
.home-hero:is(:hover, :focus-within) .plane-right { transform: translate(12px, 8px); }
.home-hero:is(:hover, :focus-within) .diagram-orbit { transform: rotate(18deg); }
@media (prefers-reduced-motion: no-preference) {
  .hero-copy { animation: hero-enter .7s cubic-bezier(.2,.7,.2,1) both; }
  .diagram-outline, .diagram-leaders { stroke-dasharray: 1; animation: diagram-draw 1.6s cubic-bezier(.2,.7,.2,1) both; }
  .diagram-leaders { animation-delay: .6s; }
  .diagram-base { animation: base-arrive 1.2s cubic-bezier(.2,.7,.2,1) both; }
  .diagram-planes { animation: planes-arrive 3.4s cubic-bezier(.2,.7,.2,1) both; }
  .hero-art::after { animation: diagram-sweep 2.8s cubic-bezier(.2,.7,.2,1) .2s both; }
  .title-outline::after { transform-origin: left; animation: title-mark .9s .3s cubic-bezier(.2,.7,.2,1) both; }
  .page-index { transform-origin: left; animation: title-mark .6s ease-out both; }
}
@keyframes diagram-draw { from { stroke-dashoffset: 1; } to { stroke-dashoffset: 0; } }
@keyframes hero-enter { from { opacity: 0; transform: translateY(14px); } to { opacity: 1; transform: none; } }
@keyframes base-arrive { from { opacity: 0; transform: translate(30px, -18px) rotate(-12deg); } to { opacity: 1; transform: none; } }
@keyframes planes-arrive { 0%, 15% { opacity: 0; transform: translate(-35px, 24px) scale(.78); } 55% { opacity: 1; transform: translate(12px, -8px) scale(1.06); } 100% { opacity: 1; transform: none; } }
@keyframes diagram-sweep { 0% { left: 0; opacity: 0; } 20% { opacity: 1; } 90%, 100% { left: 100%; opacity: 0; } }
@keyframes title-mark { from { opacity: 0; scale: 0 1; } to { opacity: 1; scale: 1 1; } }
@media (prefers-reduced-motion: reduce) {
  .home-hero .diagram-orbit, .home-hero .plane-top, .home-hero .plane-left, .home-hero .plane-right { transform: none; transition: none; }
}
@media (max-width: 1100px) { .content-layout { grid-template-columns: minmax(0, 1fr) 260px; gap: 24px; }.side-widgets { padding-left: 20px; }.hero-actions a { gap: 18px; padding-inline: 13px; }.discover-strip a { padding-inline: 12px; gap: 12px; } }
@media (max-width: 700px) {
  .home-hero { padding: 32px 0; grid-template-columns: 1fr; gap: 28px; }
  .home-page.content-page h1 { font-size: clamp(44px, 10vw, 64px); }
  .hero-art { padding: 16px; }
  .world-diagram { max-height: 220px; margin-block: 4px; }
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
</style>
