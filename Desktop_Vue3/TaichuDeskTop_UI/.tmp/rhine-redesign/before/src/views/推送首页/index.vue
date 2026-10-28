<template>
  <article class="home-page content-page rhine-home">
    <!-- 印刷裁切标记 -->
    <i class="crop tl" aria-hidden="true"></i>
    <i class="crop tr" aria-hidden="true"></i>
    <i class="crop bl" aria-hidden="true"></i>
    <i class="crop br" aria-hidden="true"></i>

    <!-- 档案抬头：一页一锚点，其余留白 -->
    <header class="masthead">
      <div class="mast-main">
        <p class="kicker">TAICHU · COMMUNITY ARCHIVE</p>
        <h1 class="display">在这里，<br />留下你的创造。</h1>
      </div>
      <dl class="mast-meta">
        <div><dt>FILE</dt><dd>No. 001</dd></div>
        <div><dt>STATUS</dt><dd>ACTIVE</dd></div>
        <div><dt>ACCESS</dt><dd>PUBLIC</dd></div>
      </dl>
    </header>

    <div class="rule" role="presentation"></div>
    <div class="hazard" aria-hidden="true"></div>

    <div class="content-layout">
      <section class="main-content" aria-label="开发进度与社区动态">
        <div class="block"><span class="idx">01</span><DevProgress /></div>
        <div class="block"><span class="idx">02</span><LatestNews /></div>
        <div class="block"><span class="idx">03</span><CommunityFeed /></div>
        <div class="block"><span class="idx">04</span><updateLog /></div>
      </section>
      <aside class="side-widgets" aria-label="社区入口与数据">
        <div class="block"><span class="idx">05</span><CommunityLinks /></div>
        <div class="block"><span class="idx">06</span><FinancePanel /></div>
        <div class="block"><span class="idx">07</span><feedback /></div>
        <div class="block"><span class="idx">08</span><SignCard /></div>
        <div class="block"><span class="idx">09</span><DataPanel /></div>
      </aside>
    </div>
  </article>
</template>
<script setup lang="ts">
import SignCard from '../../components/SignCard.vue'
import feedback from './components/feedback.vue'
import CommunityFeed from './components/CommunityFeed.vue'
import LatestNews from './components/LatestNews.vue'
import updateLog from './components/updateLog.vue'
import DataPanel from './components/DataPanel.vue'
import FinancePanel from './components/FinancePanel.vue'
import CommunityLinks from './components/CommunityLinks.vue'
import DevProgress from './components/DevProgress.vue'
</script>
<style scoped>
/* ===== 版面骨架：一页一锚点 · 7:5 非对称 · 大留白 ===== */
.rhine-home { position: relative; padding-top: 26px; }

/* 裁切标记（纯 CSS 印刷件） */
.crop { position: absolute; width: 14px; height: 14px; pointer-events: none; border-color: var(--text-secondary); opacity: .55; }
.crop.tl { top: 0; left: 0; border-left: 1px solid; border-top: 1px solid; }
.crop.tr { top: 0; right: 0; border-right: 1px solid; border-top: 1px solid; }
.crop.bl { bottom: 0; left: 0; border-left: 1px solid; border-bottom: 1px solid; }
.crop.br { bottom: 0; right: 0; border-right: 1px solid; border-bottom: 1px solid; }

/* ===== 抬头：巨型标题 + 右侧档案元信息 ===== */
.masthead {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 40px;
  align-items: end;
  padding: 34px 0 30px;
}
.kicker {
  margin: 0 0 22px;
  font-family: var(--font-mono);
  font-size: .66rem;
  letter-spacing: .28em;
  text-transform: uppercase;
  color: var(--text-secondary);
}
.display {
  margin: 0;
  font-size: clamp(2.4rem, 6.2vw, 4.6rem);
  line-height: .98;
  letter-spacing: -.045em;
  font-weight: 800;
  color: var(--text-primary);
}
.mast-meta { margin: 0; display: grid; gap: 12px; min-width: 168px; border-top: 1px solid var(--text-primary); padding-top: 10px; }
.mast-meta div { display: flex; justify-content: space-between; gap: 20px; }
.mast-meta dt { font-family: var(--font-mono); font-size: .62rem; letter-spacing: .22em; text-transform: uppercase; color: var(--text-secondary); }
.mast-meta dd { margin: 0; font-family: var(--font-mono); font-size: .72rem; letter-spacing: .06em; color: var(--text-primary); }

/* 主分隔线：2px 墨线 */
.rule { height: 2px; background: var(--text-primary); }

/* ===== 常驻动效：斜纹警示带（持续滚动） ===== */
.hazard {
  height: 5px;
  margin-top: 3px;
  background-image: repeating-linear-gradient(
    -45deg,
    var(--text-primary) 0 6px,
    transparent 6px 12px
  );
  background-size: 17px 5px;      /* 与 45° 条纹周期对齐，循环无缝 */
  opacity: .5;
  animation: hazard-flow 2.6s linear infinite;
}
@keyframes hazard-flow { from { background-position: 0 0; } to { background-position: 17px 0; } }

/* ===== 常驻动效：主栏每块细线上的扫描光点 ===== */
.block::after {
  content: '';
  position: absolute;
  left: 0; top: 0; height: 1px; width: 22%;
  background: linear-gradient(90deg, transparent, var(--accent-color), transparent);
  animation: line-run 7s linear infinite;
  animation-delay: calc(var(--i, 0) * -0.9s);
  pointer-events: none;
}
@keyframes line-run {
  0% { transform: translateX(-120%); opacity: 0; }
  8% { opacity: 1; }
  55% { opacity: 1; }
  65%, 100% { transform: translateX(460%); opacity: 0; }
}

/* ===== 常驻动效：竖分割线上的游走光点 ===== */
.side-widgets { position: relative; }
.side-widgets::before {
  content: '';
  position: absolute;
  left: -1px; top: 0; width: 1px; height: 18%;
  background: linear-gradient(180deg, transparent, var(--accent-color), transparent);
  animation: vline-run 8s linear infinite;
  pointer-events: none;
}
@keyframes vline-run {
  0% { transform: translateY(-120%); opacity: 0; }
  10% { opacity: 1; }
  60% { opacity: 1; }
  72%, 100% { transform: translateY(560%); opacity: 0; }
}

/* ===== 常驻动效：编号前的短横线脉冲 ===== */
.idx::before {
  content: '';
  display: inline-block;
  width: 14px; height: 1px;
  margin-right: 8px;
  vertical-align: middle;
  background: var(--accent-color);
  animation: tick-blink 2.4s steps(1, end) infinite;
}
@keyframes tick-blink { 0%, 60% { opacity: 1; } 61%, 100% { opacity: .15; } }

/* ===== 常驻动效：抬头扫描线 + 主线的微光 ===== */
.rule { position: relative; overflow: hidden; }
.rule::after {
  content: '';
  position: absolute; inset: 0; width: 30%;
  background: linear-gradient(90deg, transparent, var(--accent-color), transparent);
  animation: rule-sheen 9s linear infinite;
}
@keyframes rule-sheen { 0% { transform: translateX(-120%); } 100% { transform: translateX(420%); } }

/* ===== 双栏：主栏 7 / 侧栏 5，侧栏用竖发丝线切分 ===== */
.content-layout {
  display: grid;
  grid-template-columns: minmax(0, 7fr) minmax(0, 5fr);
  gap: 0;
  align-items: start;
}
.main-content { min-width: 0; display: grid; gap: 46px; padding: 38px 44px 60px 0; }
.side-widgets { min-width: 0; display: grid; gap: 38px; padding: 38px 0 60px 44px; border-left: 1px solid var(--border-line); }

/* 每个区块：编号 + 上方发丝线，去掉卡片感 */
.block { min-width: 0; padding-top: 16px; position: relative; border-top: 0; }
.idx {
  display: block;
  margin-bottom: 12px;
  font-family: var(--font-mono);
  font-size: .62rem;
  letter-spacing: .26em;
  color: var(--accent-color);
}

/* ===== 动态线条：机械缓动，压印/显影 ===== */
@keyframes draw-line { from { transform: scaleX(0); } to { transform: scaleX(1); } }
@keyframes rise-in { from { opacity: 0; transform: translateY(6px); } to { opacity: 1; transform: none; } }
@keyframes scan-sweep { 0% { transform: translateX(-110%); } 55%, 100% { transform: translateX(320%); } }
@keyframes tick-pulse { 0%, 100% { opacity: .35; } 50% { opacity: .9; } }

/* 抬头下方的 2px 墨线：从左向右画出 */
.rule { transform-origin: left center; animation: draw-line .7s cubic-bezier(.22,.9,.24,1) both; }

/* 区块细线：按编号顺序依次绘制；编号随后显影 */
.block::before {
  content: '';
  position: absolute;
  left: 0; right: 0; top: 0; height: 1px;
  background: var(--border-line);
  transform-origin: left center;
  animation: draw-line .55s cubic-bezier(.22,.9,.24,1) both;
  animation-delay: calc(var(--i, 0) * 70ms + 140ms);
}
.idx { animation: rise-in .45s cubic-bezier(.22,.9,.24,1) both; animation-delay: calc(var(--i, 0) * 70ms + 240ms); }

/* 抬头扫描线：一条橙色光带缓慢横扫 */
.masthead { position: relative; }
.masthead::after {
  content: '';
  position: absolute;
  left: 0; bottom: 0;
  width: 34%; height: 1px;
  background: linear-gradient(90deg, transparent, var(--accent-color), transparent);
  animation: scan-sweep 6s linear infinite;
  pointer-events: none;
}

/* 四角裁切标记呼吸（轻微的"设备运行"感） */
.crop { animation: tick-pulse 3.2s ease-in-out infinite; }

/* 编号错峰：主栏 4 个 + 侧栏 5 个 */
.main-content .block:nth-child(1), .side-widgets .block:nth-child(1) { --i: 0; }
.main-content .block:nth-child(2), .side-widgets .block:nth-child(2) { --i: 1; }
.main-content .block:nth-child(3), .side-widgets .block:nth-child(3) { --i: 2; }
.main-content .block:nth-child(4), .side-widgets .block:nth-child(4) { --i: 3; }
.side-widgets .block:nth-child(5) { --i: 4; }

@media (prefers-reduced-motion: reduce) {
  .rule, .block::before, .idx, .masthead::after, .crop { animation: none !important; }
}
/* 子组件根节点：剥掉自带卡片样式，交给版式统一 */
.block > :deep(*) {
  background: transparent !important;
  border: 0 !important;
  border-radius: 0 !important;
  box-shadow: none !important;
  padding: 0 !important;
  margin: 0 !important;
}

@media (max-width: 1150px) {
  .content-layout { grid-template-columns: minmax(0, 1fr); }
  .side-widgets { border-left: 0; padding-left: 0; border-top: 1px solid var(--border-line); padding-top: 34px; }
}
@media (max-width: 680px) {
  .masthead { grid-template-columns: 1fr; gap: 24px; align-items: start; }
  .main-content, .side-widgets { padding-right: 0; gap: 34px; }
  .crop { display: none; }
}
</style>
