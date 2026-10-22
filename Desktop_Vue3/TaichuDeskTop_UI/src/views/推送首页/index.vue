<template>
  <article class="home-page content-page">
    <header class="home-welcome">
      <div><p class="eyebrow">TAICHU COMMUNITY</p><h1>在这里，留下你的创造。</h1><p class="intro">发现作品、交流想法，和社区一起把灵感变成现实。</p></div>

    </header>
    <div class="content-layout">
      <section class="main-content" aria-label="社区动态">
        <LatestNews />
        <feedback />
        <updateLog />
      </section>
      <aside class="side-widgets" aria-label="签到与社区数据"><SignCard /><DataPanel /><FinancePanel /></aside>
    </div>
  </article>
</template>
<script setup lang="ts">
import SignCard from '../../components/SignCard.vue'
import feedback from './components/feedback.vue'
import LatestNews from './components/LatestNews.vue'
import updateLog from './components/updateLog.vue'
import DataPanel from './components/DataPanel.vue'
import FinancePanel from './components/FinancePanel.vue'
</script>
<style scoped>
.home-welcome { display: flex; justify-content: space-between; align-items: center; gap: 24px; padding: 28px 0 32px; }
.home-welcome h1 { font-size: clamp(1.8rem, 3vw, 2.5rem); margin: 12px 0; }
.content-layout { display: grid; grid-template-columns: minmax(0,1fr) 320px; gap: 24px; align-items: start; }
.main-content, .side-widgets { min-width: 0; display: grid; gap: 24px; }
.main-content > *, .side-widgets > * { min-width: 0; background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 12px; padding: 24px; margin: 0; }
@media(max-width: 1150px) { .content-layout { grid-template-columns: minmax(0,1fr) 280px; } }
@media(max-width: 680px) { .home-welcome { padding-top: 8px; } .content-layout { grid-template-columns: minmax(0,1fr); } .main-content > *, .side-widgets > * { padding: 18px; } }
</style>
