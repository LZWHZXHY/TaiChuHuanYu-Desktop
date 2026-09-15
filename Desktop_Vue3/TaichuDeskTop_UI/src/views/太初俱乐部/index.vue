<template>
  <div class="page-shell">

    <Navbar />

    <main>

      <!-- ==================== Hero ==================== -->
      <section class="hero-section">

        <div class="hero-grid"></div>
        <div class="scan-line"></div>
        <div class="hero-number">01</div>

        <div class="hero-content">

          <div class="hero-status">
            <span class="status-dot"></span>
            SYSTEM ONLINE
            <span class="status-line"></span>
            TAICHU NETWORK
          </div>

          <div class="hero-badge">
            TAICHU / TACTICAL ESPORTS CLUB
          </div>

          <h1 class="hero-title">
            无畏前行
            <br />
            <span>精准打击</span>
          </h1>

          <p class="hero-description">
            太初俱乐部 · 多游戏精英陪玩团队
            <br />
            专注竞技、战术与团队协作。
          </p>

          <div class="hero-meta">
            <div>
              <span>UNIT</span>
              <strong>TC-CORE</strong>
            </div>

            <div>
              <span>STATUS</span>
              <strong class="online">ACTIVE</strong>
            </div>

            <div>
              <span>EST.</span>
              <strong>2025</strong>
            </div>
          </div>

          <div class="hero-btns">

            <button class="btn-primary" @click="handleJoin">
              <span>立即加入</span>
              <b>→</b>
            </button>

            <button class="btn-outline" @click="handleLearnMore">
              <span>了解我们</span>
              <b>+</b>
            </button>

          </div>

        </div>

        <!-- ==================== 右侧战术 HUD ==================== -->
        <div class="tactical-display">

          <div class="coordinate coordinate-top">
            31°14'25.8"N
            <br />
            121°28'33.4"E
          </div>

          <div class="coordinate coordinate-right">
            GRID
            <strong>TC-HQ</strong>
          </div>

          <div class="hud-ring ring-outer"></div>
          <div class="hud-ring ring-middle"></div>
          <div class="hud-ring ring-inner"></div>

          <div class="crosshair">
            <span class="cross-top"></span>
            <span class="cross-right"></span>
            <span class="cross-bottom"></span>
            <span class="cross-left"></span>
            <div class="cross-center"></div>
          </div>

          <div class="hud-center">

            <div class="hud-code">
              OPERATION
            </div>

            <div class="hud-id">
              01
            </div>

            <div class="hud-label">
              TACTICAL UNIT
            </div>

          </div>

          <div class="radar-sweep"></div>

          <div class="hud-tick tick-1"></div>
          <div class="hud-tick tick-2"></div>
          <div class="hud-tick tick-3"></div>
          <div class="hud-tick tick-4"></div>

          <div class="hud-info">

            <div class="hud-info-row">
              <span>LATENCY</span>
              <strong>18ms</strong>
            </div>

            <div class="hud-info-row">
              <span>NETWORK</span>
              <strong>SECURE</strong>
            </div>

            <div class="hud-info-row">
              <span>OPERATORS</span>
              <strong>{{ statsLoading ? '—' : stats.operatorCount }}</strong>
            </div>

          </div>

          <div class="corner corner-tl"></div>
          <div class="corner corner-tr"></div>
          <div class="corner corner-bl"></div>
          <div class="corner corner-br"></div>

        </div>

        <div class="hero-bottom">

          <span>TAICHU TACTICAL NETWORK</span>

          <div class="hero-bottom-line"></div>

          <span>SECURE CONNECTION</span>

          <span class="connection-dot"></span>

        </div>

      </section>


      <!-- ==================== 数据状态 ==================== -->
      <section class="operations-section">

        <div class="section-container">

          <div class="operations-heading">

            <div>
              <span class="section-index">01 /</span>
              <h2>OPERATIONS STATUS</h2>
            </div>

            <span class="live-status">
              <i></i>
              LIVE DATA
            </span>

          </div>


          <div class="operations-grid">

            <div class="operation-item">

              <span class="operation-number">{{ statsLoading ? '—' : stats.daysActive }}</span>

              <div class="operation-detail">
                <span class="operation-label">
                  DAYS ACTIVE
                </span>

                <span class="operation-line"></span>

                <span class="operation-desc">
                  组织持续运行
                </span>
              </div>

              <span class="operation-index">01</span>

            </div>


            <div class="operation-item">

              <span class="operation-number">{{ statsLoading ? '—' : stats.operatorCount }}</span>

              <div class="operation-detail">
                <span class="operation-label">
                  ACTIVE OPERATORS
                </span>

                <span class="operation-line"></span>

                <span class="operation-desc">
                  当前现役干员
                </span>
              </div>

              <span class="operation-index">02</span>

            </div>


            <div class="operation-item">

              <span class="operation-number">{{ statsLoading ? '—' : stats.completedOrders }}</span>

              <div class="operation-detail">
                <span class="operation-label">
                  MISSIONS
                </span>

                <span class="operation-line"></span>

                <span class="operation-desc">
                  已完成任务
                </span>
              </div>

              <span class="operation-index">03</span>

            </div>


            <div class="operation-item">

              <span class="operation-number">{{ statsLoading ? '—' : stats.gameCount }}</span>

              <div class="operation-detail">
                <span class="operation-label">
                  GAMES
                </span>

                <span class="operation-line"></span>

                <span class="operation-desc">
                  上架陪玩游戏
                </span>
              </div>

              <span class="operation-index">04</span>

            </div>

          </div>

        </div>

      </section>


      <!-- ==================== 打手注册 ==================== -->
      <section class="section-block">
        <div class="section-container">
          <SectionHeading
            index="02"
            title="ENLIST"
            subtitle="打手注册 / JOIN AS OPERATOR"
          />
          <OperatorRegister
            @submit="handleRegister"
            @invalid="handleInvalid"
          />
        </div>
      </section>


      <!-- ==================== 情报 ==================== -->
      <section class="intel-section">

        <div class="section-container">

          <div class="intel-heading">

            <div class="section-heading-left">
              <span class="section-index">03 /</span>

              <div>
                <h2>INTEL</h2>
                <span>情报速递 / INTERNAL INFORMATION</span>
              </div>
            </div>

            <button class="view-all">
              VIEW ALL
              <span>→</span>
            </button>

          </div>


          <div class="intel-list">

            <article class="intel-item" @click="readNews(1)">

              <div class="intel-number">01</div>

              <div class="intel-date">
                <span>09</span>
                <small>SEP / 2026</small>
              </div>

              <div class="intel-content">

                <div class="intel-topline">
                  <span class="intel-tag match">MATCH</span>
                  <span class="intel-code">OP-0926-01</span>
                </div>

                <h3>秋季联赛八强赛程公布</h3>

                <p>
                  本周五晚 20:00，对阵老牌劲旅。
                  请各位干员准时上线完成战前集结。
                </p>

              </div>

              <div class="intel-action">
                <span>查看详情</span>
                <strong>↗</strong>
              </div>

            </article>


            <article class="intel-item" @click="readNews(2)">

              <div class="intel-number">02</div>

              <div class="intel-date">
                <span>08</span>
                <small>SEP / 2026</small>
              </div>

              <div class="intel-content">

                <div class="intel-topline">
                  <span class="intel-tag recruit">RECRUIT</span>
                  <span class="intel-code">RC-0926-02</span>
                </div>

                <h3>二队狙击手 / 医疗兵招募</h3>

                <p>
                  要求段位达到钻石以上，每周在线时长不低于
                  20 小时，通过试训后正式编入作战序列。
                </p>

              </div>

              <div class="intel-action">
                <span>查看详情</span>
                <strong>↗</strong>
              </div>

            </article>


            <article class="intel-item" @click="readNews(3)">

              <div class="intel-number">03</div>

              <div class="intel-date">
                <span>05</span>
                <small>SEP / 2026</small>
              </div>

              <div class="intel-content">

                <div class="intel-topline">
                  <span class="intel-tag system">SYSTEM</span>
                  <span class="intel-code">SYS-0926-03</span>
                </div>

                <h3>俱乐部内部战术板系统上线</h3>

                <p>
                  后端技术组完成新一代战术沙盘系统部署，
                  已向内部网络开放访问权限。
                </p>

              </div>

              <div class="intel-action">
                <span>查看详情</span>
                <strong>↗</strong>
              </div>

            </article>

          </div>

        </div>

      </section>


      <!-- ==================== Join CTA ==================== -->
      <section class="join-section">

        <div class="join-grid"></div>

        <div class="join-container">

          <div class="join-left">

            <span class="join-eyebrow">
              JOIN THE UNIT / 03
            </span>

            <h2>
              下一场任务
              <br />
              <span>等你加入。</span>
            </h2>

            <p>
              如果你拥有战术意识、团队精神以及不断突破自己的欲望，
              太初俱乐部正在寻找你。
            </p>

          </div>

          <button class="join-button" @click="handleJoin">
            <span>申请加入太初</span>
            <strong>↗</strong>
          </button>

        </div>

      </section>

    </main>

    <Footer />

  </div>
</template>


<script>
import Navbar           from './Navbar.vue'
import Footer           from './Footer.vue'
import SectionHeading   from './SectionHeading.vue'
import OperatorRegister from './OperatorRegister.vue'
import request          from '@/utils/request'

export default {
  name: 'TaichuHome',

  components: {
    Navbar,
    Footer,
    SectionHeading,
    OperatorRegister
  },

  data() {
    return {
      statsLoading: true,
      stats: {
        daysActive: 0,
        operatorCount: 0,
        completedOrders: 0,
        gameCount: 0
      }
    }
  },

  async mounted() {
    try {
      const res = await request.get('/club/stats')
      const data = res?.data ?? res

      this.stats = {
        daysActive: data?.daysActive ?? 0,
        operatorCount: data?.operatorCount ?? 0,
        completedOrders: data?.completedOrders ?? 0,
        gameCount: data?.gameCount ?? 0
      }
    } catch (e) {
      console.error('拉取统计数据失败', e)
    } finally {
      this.statsLoading = false
    }
  },

  methods: {

    handleJoin() {
      console.log('点击立即加入')
    },

    handleLearnMore() {
      console.log('点击了解我们')
    },

    readNews(id) {
      console.log('查看资讯ID:', id)
    },

    handleRegister(payload) {
      console.log('提交注册:', payload)
    },

    handleInvalid(missing) {
      console.warn('请填写必填项：', missing.join('、'))
    }

  }
}
</script>



<style scoped>

/* =========================================================
   HERO
========================================================= */

.hero-section {
  position: relative;

  min-height: 690px;

  max-width: 1440px;

  margin: 0 auto;

  display: flex;
  align-items: center;

  padding:
    80px
    max(40px, 5vw)
    90px;

  overflow: hidden;

  border-bottom: 1px solid var(--line);
}


/* Background grid */

.hero-grid {
  position: absolute;
  inset: 0;

  background-image:
    linear-gradient(
      rgba(255,255,255,0.025) 1px,
      transparent 1px
    ),
    linear-gradient(
      90deg,
      rgba(255,255,255,0.025) 1px,
      transparent 1px
    );

  background-size: 50px 50px;

  mask-image:
    linear-gradient(
      to bottom,
      black,
      transparent 90%
    );

  pointer-events: none;
}


/* Scan line */

.scan-line {
  position: absolute;

  left: 0;
  right: 0;

  top: 35%;

  height: 1px;

  background:
    linear-gradient(
      90deg,
      transparent,
      rgba(255, 107, 26, 0.16),
      transparent
    );

  animation: scan 7s linear infinite;

  pointer-events: none;
}

@keyframes scan {
  0% {
    transform: translateY(-250px);
  }

  100% {
    transform: translateY(450px);
  }
}


/* Giant number */

.hero-number {
  position: absolute;

  right: 18%;

  top: 50%;

  transform: translateY(-50%);

  font-family:
    "Arial Narrow",
    Arial,
    sans-serif;

  font-size: clamp(260px, 35vw, 520px);

  font-weight: 900;

  line-height: 1;

  color: transparent;

  -webkit-text-stroke:
    1px rgba(255,255,255,0.035);

  pointer-events: none;
}


/* Hero content */

.hero-content {
  position: relative;

  z-index: 5;

  width: 55%;

  max-width: 700px;
}

.hero-status {
  display: flex;
  align-items: center;
  gap: 9px;

  color: var(--text-muted);

  font-family: monospace;

  font-size: 10px;

  letter-spacing: 2px;

  margin-bottom: 26px;
}

.status-dot {
  width: 6px;
  height: 6px;

  border-radius: 50%;

  background: var(--orange);

  box-shadow:
    0 0 0 4px rgba(255,107,26,0.08),
    0 0 12px rgba(255,107,26,0.7);

  animation: pulse 2s infinite;
}

@keyframes pulse {
  50% {
    opacity: 0.45;
  }
}

.status-line {
  width: 35px;
  height: 1px;

  background: var(--line-bright);

  margin: 0 5px;
}

.hero-badge {
  color: var(--orange);

  font-family: monospace;

  font-size: 11px;

  letter-spacing: 3px;

  margin-bottom: 20px;
}

.hero-title {
  margin: 0;

  font-size: clamp(54px, 6vw, 92px);

  line-height: 0.95;

  font-weight: 850;

  letter-spacing: -3px;
}

.hero-title span {
  color: transparent;

  -webkit-text-stroke:
    1px rgba(242,245,247,0.65);
}

.hero-description {
  margin: 28px 0 0;

  color: var(--text-soft);

  font-size: 15px;

  line-height: 1.9;

  letter-spacing: 0.5px;
}


/* Hero metadata */

.hero-meta {
  display: flex;

  margin-top: 35px;

  border-top: 1px solid var(--line);

  border-bottom: 1px solid var(--line);

  width: fit-content;
}

.hero-meta > div {
  min-width: 125px;

  padding:
    13px
    24px
    13px
    0;

  margin-right: 24px;

  border-right: 1px solid var(--line);
}

.hero-meta > div:last-child {
  border-right: none;
}

.hero-meta span {
  display: block;

  color: var(--text-muted);

  font-family: monospace;

  font-size: 9px;

  letter-spacing: 1px;

  margin-bottom: 5px;
}

.hero-meta strong {
  font-family: monospace;

  font-size: 12px;

  letter-spacing: 1px;
}

.hero-meta .online {
  color: var(--cyan);
}


/* Buttons */

.hero-btns {
  display: flex;

  gap: 12px;

  margin-top: 35px;
}

.btn-primary,
.btn-outline {
  height: 48px;

  padding: 0 23px;

  display: flex;
  align-items: center;
  justify-content: center;

  gap: 30px;

  font-size: 12px;

  letter-spacing: 1px;

  cursor: pointer;

  transition: all 0.25s ease;
}

.btn-primary {
  border: 1px solid var(--orange);

  background: var(--orange);

  color: #fff;
}

.btn-primary b,
.btn-outline b {
  font-size: 17px;
  font-weight: 400;
}

.btn-primary:hover {
  background: var(--orange-bright);

  box-shadow:
    0 8px 30px rgba(255,107,26,0.2);
}

.btn-outline {
  border: 1px solid var(--line-bright);

  background: rgba(255,255,255,0.02);

  color: var(--text);
}

.btn-outline:hover {
  border-color: var(--orange);

  color: var(--orange);

  background: rgba(255,107,26,0.04);
}


/* =========================================================
   TACTICAL DISPLAY
========================================================= */

.tactical-display {
  position: absolute;

  right: 5%;

  top: 50%;

  width: min(460px, 38vw);
  height: min(460px, 38vw);

  transform: translateY(-50%);

  opacity: 0.9;
}


/* Rings */

.hud-ring {
  position: absolute;

  left: 50%;
  top: 50%;

  border-radius: 50%;

  transform: translate(-50%, -50%);

  border: 1px solid rgba(255,255,255,0.07);
}

.ring-outer {
  width: 100%;
  height: 100%;

  border-style: dashed;

  animation: rotate 35s linear infinite;
}

.ring-middle {
  width: 72%;
  height: 72%;

  border-color:
    rgba(255,107,26,0.18);

  animation: rotateReverse 25s linear infinite;
}

.ring-inner {
  width: 46%;
  height: 46%;

  border-color:
    rgba(255,255,255,0.12);
}

@keyframes rotate {
  to {
    transform:
      translate(-50%, -50%)
      rotate(360deg);
  }
}

@keyframes rotateReverse {
  to {
    transform:
      translate(-50%, -50%)
      rotate(-360deg);
  }
}


/* Crosshair */

.crosshair {
  position: absolute;

  left: 50%;
  top: 50%;

  width: 65%;
  height: 65%;

  transform: translate(-50%, -50%);
}

.crosshair span {
  position: absolute;

  background: rgba(255,255,255,0.13);
}

.cross-top,
.cross-bottom {
  left: 50%;

  width: 1px;
  height: 13%;

  transform: translateX(-50%);
}

.cross-top {
  top: -5%;
}

.cross-bottom {
  bottom: -5%;
}

.cross-left,
.cross-right {
  top: 50%;

  width: 13%;
  height: 1px;

  transform: translateY(-50%);
}

.cross-left {
  left: -5%;
}

.cross-right {
  right: -5%;
}

.cross-center {
  position: absolute;

  width: 7px;
  height: 7px;

  left: 50%;
  top: 50%;

  transform: translate(-50%, -50%);

  border: 1px solid var(--orange);

  border-radius: 50%;

  box-shadow:
    0 0 14px rgba(255,107,26,0.6);
}


/* Center */

.hud-center {
  position: absolute;

  left: 50%;
  top: 50%;

  transform: translate(-50%, -50%);

  text-align: center;
}

.hud-code {
  font-family: monospace;

  color: var(--text-muted);

  font-size: 8px;

  letter-spacing: 3px;
}

.hud-id {
  font-size: 110px;

  line-height: 0.95;

  font-family:
    "Arial Narrow",
    Arial,
    sans-serif;

  font-weight: 900;

  color: transparent;

  -webkit-text-stroke:
    1px rgba(255,255,255,0.22);
}

.hud-label {
  color: var(--orange);

  font-family: monospace;

  font-size: 8px;

  letter-spacing: 3px;
}


/* Radar */

.radar-sweep {
  position: absolute;

  left: 50%;
  top: 50%;

  width: 45%;
  height: 1px;

  transform-origin: left center;

  background:
    linear-gradient(
      90deg,
      rgba(255,107,26,0.65),
      transparent
    );

  animation: radar 5s linear infinite;
}

@keyframes radar {
  to {
    transform: rotate(360deg);
  }
}


/* HUD info */

.coordinate {
  position: absolute;

  font-family: monospace;

  color: var(--text-muted);

  font-size: 8px;

  line-height: 1.7;

  letter-spacing: 1px;
}

.coordinate-top {
  top: 0;
  left: 0;
}

.coordinate-right {
  top: 42%;
  right: -30px;

  text-align: right;
}

.coordinate-right strong {
  display: block;

  color: var(--text-soft);

  margin-top: 4px;
}

.hud-info {
  position: absolute;

  right: 0;
  bottom: 8%;

  width: 115px;
}

.hud-info-row {
  display: flex;
  justify-content: space-between;

  border-bottom: 1px solid var(--line);

  padding: 5px 0;

  font-family: monospace;

  font-size: 8px;
}

.hud-info-row span {
  color: var(--text-muted);
}

.hud-info-row strong {
  color: var(--text-soft);

  font-weight: 400;
}


/* HUD corners */

.corner {
  position: absolute;

  width: 16px;
  height: 16px;
}

.corner::before,
.corner::after {
  content: "";

  position: absolute;

  background: var(--orange);
}

.corner-tl {
  left: 0;
  top: 0;
}

.corner-tr {
  right: 0;
  top: 0;
}

.corner-bl {
  left: 0;
  bottom: 0;
}

.corner-br {
  right: 0;
  bottom: 0;
}

.corner-tl::before,
.corner-bl::before,
.corner-tr::before,
.corner-br::before {
  width: 16px;
  height: 1px;
}

.corner-tl::after,
.corner-bl::after,
.corner-tr::after,
.corner-br::after {
  width: 1px;
  height: 16px;
}

.corner-tl::before,
.corner-tl::after {
  left: 0;
  top: 0;
}

.corner-tr::before,
.corner-tr::after {
  right: 0;
  top: 0;
}

.corner-bl::before,
.corner-bl::after {
  left: 0;
  bottom: 0;
}

.corner-br::before,
.corner-br::after {
  right: 0;
  bottom: 0;
}


/* Hero bottom */

.hero-bottom {
  position: absolute;

  left: 5%;
  right: 5%;
  bottom: 25px;

  display: flex;
  align-items: center;
  gap: 14px;

  color: var(--text-muted);

  font-family: monospace;

  font-size: 8px;

  letter-spacing: 2px;
}

.hero-bottom-line {
  flex: 1;

  height: 1px;

  background: var(--line);
}

.connection-dot {
  width: 5px;
  height: 5px;

  border-radius: 50%;

  background: var(--cyan);

  box-shadow: 0 0 8px var(--cyan);
}


/* =========================================================
   OPERATIONS
========================================================= */

.operations-section {
  padding: 90px 0;

  background: var(--bg-soft);

  border-bottom: 1px solid var(--line);
}

.operations-heading {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;

  margin-bottom: 42px;
}

.operations-heading > div {
  display: flex;
  align-items: baseline;
  gap: 14px;
}

.section-index {
  color: var(--orange);

  font-family: monospace;

  font-size: 10px;

  letter-spacing: 1px;
}

.operations-heading h2,
.intel-heading h2 {
  margin: 0;

  font-size: 21px;

  letter-spacing: 2px;

  font-weight: 700;
}

.live-status {
  display: flex;
  align-items: center;
  gap: 7px;

  color: var(--text-muted);

  font-family: monospace;

  font-size: 9px;

  letter-spacing: 1px;
}

.live-status i {
  width: 5px;
  height: 5px;

  border-radius: 50%;

  background: var(--cyan);

  box-shadow: 0 0 8px var(--cyan);
}


.operations-grid {
  display: grid;

  grid-template-columns:
    repeat(4, 1fr);

  border-top: 1px solid var(--line);

  border-bottom: 1px solid var(--line);
}

.operation-item {
  position: relative;

  min-height: 155px;

  padding: 30px 28px;

  border-right: 1px solid var(--line);

  overflow: hidden;

  transition: background 0.25s ease;
}

.operation-item:last-child {
  border-right: none;
}

.operation-item:hover {
  background: rgba(255,255,255,0.02);
}

.operation-number {
  display: block;

  font-size: 52px;

  line-height: 1;

  font-weight: 700;

  letter-spacing: -2px;

  color: var(--text);
}

.operation-detail {
  margin-top: 18px;
}

.operation-label {
  display: block;

  font-family: monospace;

  font-size: 9px;

  letter-spacing: 1.5px;

  color: var(--text-soft);
}

.operation-line {
  display: block;

  width: 40px;

  height: 1px;

  background: var(--orange);

  margin: 8px 0;
}

.operation-desc {
  color: var(--text-muted);

  font-size: 11px;
}

.operation-index {
  position: absolute;

  right: 18px;
  top: 18px;

  color: var(--text-muted);

  font-family: monospace;

  font-size: 9px;
}


/* =========================================================
   INTEL
========================================================= */

.intel-section {
  padding: 100px 0;
}

.intel-heading {
  display: flex;

  align-items: center;

  justify-content: space-between;

  margin-bottom: 40px;
}

.section-heading-left {
  display: flex;
  align-items: center;
  gap: 14px;
}

.section-heading-left > div span {
  display: block;

  color: var(--text-muted);

  font-family: monospace;

  font-size: 8px;

  letter-spacing: 1px;

  margin-top: 5px;
}

.view-all {
  border: none;

  background: none;

  color: var(--text-muted);

  font-family: monospace;

  font-size: 9px;

  letter-spacing: 1px;

  cursor: pointer;

  transition: color 0.2s ease;
}

.view-all span {
  color: var(--orange);

  margin-left: 8px;
}

.view-all:hover {
  color: var(--text);
}


/* Intel item */

.intel-list {
  border-top: 1px solid var(--line);
}

.intel-item {
  position: relative;

  display: grid;

  grid-template-columns:
    55px
    120px
    1fr
    100px;

  gap: 25px;

  align-items: center;

  min-height: 155px;

  padding: 22px 15px;

  border-bottom: 1px solid var(--line);

  cursor: pointer;

  transition:
    background 0.25s ease,
    padding 0.25s ease;
}

.intel-item::before {
  content: "";

  position: absolute;

  left: 0;
  top: 0;
  bottom: 0;

  width: 2px;

  background: var(--orange);

  transform: scaleY(0);

  transform-origin: center;

  transition: transform 0.25s ease;
}

.intel-item:hover {
  background:
    linear-gradient(
      90deg,
      rgba(255,107,26,0.05),
      transparent 60%
    );

  padding-left: 25px;
}

.intel-item:hover::before {
  transform: scaleY(1);
}

.intel-number {
  color: var(--text-muted);

  font-family: monospace;

  font-size: 10px;
}

.intel-date span {
  display: block;

  font-size: 27px;

  font-weight: 600;

  line-height: 1;
}

.intel-date small {
  display: block;

  margin-top: 6px;

  color: var(--text-muted);

  font-family: monospace;

  font-size: 8px;

  letter-spacing: 1px;
}

.intel-topline {
  display: flex;
  align-items: center;
  gap: 10px;

  margin-bottom: 10px;
}

.intel-tag {
  padding: 3px 7px;

  font-family: monospace;

  font-size: 8px;

  letter-spacing: 1px;
}

.intel-tag.match {
  color: var(--cyan);

  border: 1px solid rgba(120,215,206,0.18);

  background: rgba(120,215,206,0.04);
}

.intel-tag.recruit {
  color: var(--orange);

  border: 1px solid rgba(255,107,26,0.2);

  background: rgba(255,107,26,0.04);
}

.intel-tag.system {
  color: #a9b6c2;

  border: 1px solid var(--line-bright);

  background: rgba(255,255,255,0.025);
}

.intel-code {
  color: var(--text-muted);

  font-family: monospace;

  font-size: 8px;

  letter-spacing: 1px;
}

.intel-content h3 {
  margin: 0 0 8px;

  font-size: 18px;

  font-weight: 600;

  letter-spacing: 0.3px;
}

.intel-content p {
  margin: 0;

  max-width: 650px;

  color: var(--text-muted);

  font-size: 12px;

  line-height: 1.7;
}

.intel-action {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 10px;

  color: var(--text-muted);

  font-family: monospace;

  font-size: 9px;

  transition: color 0.25s ease;
}

.intel-action strong {
  color: var(--orange);

  font-size: 17px;

  font-weight: 400;
}

.intel-item:hover .intel-action {
  color: var(--text);
}


/* =========================================================
   JOIN
========================================================= */

.join-section {
  position: relative;

  overflow: hidden;

  border-top: 1px solid var(--line);

  border-bottom: 1px solid var(--line);

  background:
    radial-gradient(
      circle at 80% 50%,
      rgba(255,107,26,0.08),
      transparent 35%
    ),
    #080c11;
}

.join-grid {
  position: absolute;
  inset: 0;

  background-image:
    linear-gradient(
      rgba(255,255,255,0.025) 1px,
      transparent 1px
    ),
    linear-gradient(
      90deg,
      rgba(255,255,255,0.025) 1px,
      transparent 1px
    );

  background-size: 40px 40px;

  mask-image:
    linear-gradient(
      to right,
      transparent,
      black 20%,
      black 80%,
      transparent
    );
}

.join-container {
  position: relative;

  width: min(1200px, calc(100% - 56px));

  min-height: 350px;

  margin: 0 auto;

  display: flex;

  align-items: center;

  justify-content: space-between;

  gap: 60px;
}

.join-eyebrow {
  color: var(--orange);

  font-family: monospace;

  font-size: 9px;

  letter-spacing: 2px;
}

.join-left h2 {
  margin: 20px 0 18px;

  font-size: clamp(42px, 5vw, 65px);

  line-height: 0.95;

  letter-spacing: -2px;
}

.join-left h2 span {
  color: transparent;

  -webkit-text-stroke:
    1px rgba(255,255,255,0.45);
}

.join-left p {
  max-width: 540px;

  color: var(--text-muted);

  font-size: 13px;

  line-height: 1.8;
}

.join-button {
  flex-shrink: 0;

  width: 180px;
  height: 180px;

  border-radius: 50%;

  border: 1px solid rgba(255,107,26,0.45);

  background:
    radial-gradient(
      circle,
      rgba(255,107,26,0.1),
      rgba(255,107,26,0.02)
    );

  color: var(--orange);

  display: flex;

  flex-direction: column;

  align-items: center;

  justify-content: center;

  gap: 15px;

  cursor: pointer;

  font-size: 11px;

  letter-spacing: 1px;

  transition: all 0.35s ease;
}

.join-button strong {
  font-size: 25px;

  font-weight: 300;
}

.join-button:hover {
  color: #fff;

  background: var(--orange);

  border-color: var(--orange);

  box-shadow:
    0 0 50px rgba(255,107,26,0.2);

  transform: scale(1.04);
}


/* =========================================================
   RESPONSIVE
========================================================= */

@media (max-width: 1000px) {

  .hero-section {
    min-height: 720px;

    padding-left: 40px;
  }

  .hero-content {
    width: 65%;
  }

  .tactical-display {
    right: -8%;

    opacity: 0.4;
  }

  .operations-grid {
    grid-template-columns: repeat(2, 1fr);
  }

  .operation-item:nth-child(2) {
    border-right: none;
  }

  .operation-item:nth-child(-n + 2) {
    border-bottom: 1px solid var(--line);
  }

}


/* Tablet */

@media (max-width: 768px) {

  .hero-section {
    min-height: 700px;

    padding:
      70px
      24px
      80px;

    align-items: flex-start;
  }

  .hero-content {
    width: 100%;
  }

  .hero-title {
    font-size: 58px;
  }

  .hero-description {
    font-size: 13px;
  }

  .hero-meta {
    width: 100%;
  }

  .hero-meta > div {
    min-width: auto;

    flex: 1;

    margin-right: 12px;

    padding-right: 12px;
  }

  .tactical-display {
    width: 430px;
    height: 430px;

    right: 50%;

    top: auto;
    bottom: -120px;

    transform: translateX(50%);

    opacity: 0.25;
  }

  .hero-bottom {
    left: 24px;
    right: 24px;
  }

  .operations-section,
  .intel-section {
    padding: 70px 0;
  }

  .intel-item {
    grid-template-columns:
      35px
      80px
      1fr;

    gap: 12px;

    padding: 22px 0;
  }

  .intel-action {
    display: none;
  }

  .intel-content h3 {
    font-size: 16px;
  }

  .join-container {
    width: calc(100% - 40px);

    min-height: 420px;

    flex-direction: column;

    align-items: flex-start;

    justify-content: center;

    gap: 35px;
  }

  .join-button {
    width: 125px;
    height: 125px;
  }

}


/* Mobile */

@media (max-width: 520px) {

  .hero-title {
    font-size: 48px;

    letter-spacing: -2px;
  }

  .hero-meta {
    flex-wrap: wrap;

    gap: 10px;

    border-bottom: none;
  }

  .hero-meta > div {
    flex: none;

    width: calc(50% - 10px);

    border-bottom: 1px solid var(--line);

    border-right: none;
  }

  .hero-btns {
    flex-direction: column;

    width: 100%;
  }

  .btn-primary,
  .btn-outline {
    width: 100%;
  }

  .operations-grid {
    grid-template-columns: 1fr;
  }

  .operation-item {
    border-right: none;

    border-bottom: 1px solid var(--line);
  }

  .operation-item:last-child {
    border-bottom: none;
  }

  .operations-heading {
    align-items: flex-start;

    gap: 20px;

    flex-direction: column;
  }

  .intel-heading {
    align-items: flex-start;

    gap: 20px;

    flex-direction: column;
  }

  .intel-item {
    grid-template-columns:
      30px
      1fr;

    align-items: start;
  }

  .intel-date {
    display: none;
  }

  .intel-number {
    padding-top: 5px;
  }

  .intel-content p {
    font-size: 11px;
  }

}


/* =========================================================
   REDUCED MOTION
========================================================= */

@media (prefers-reduced-motion: reduce) {

  *,
  *::before,
  *::after {
    animation-duration: 0.01ms !important;

    animation-iteration-count: 1 !important;

    scroll-behavior: auto !important;

    transition-duration: 0.01ms !important;
  }

}

</style>