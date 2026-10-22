<template>
  <div class="page-shell club-home">

    <Navbar />

    <main>

      <!-- ==================== 太初寰宇俱乐部（页面顶部） ==================== -->
      <section class="club-hero-section">

        <div class="club-grid-bg"></div>
        <div class="scan-line"></div>

        <div class="section-container club-hero-inner">

          <!-- ---------- 俱乐部介绍 ---------- -->
          <div class="club-intro">

            <div class="club-intro-text">

              <span class="club-eyebrow">
                CLUB PROFILE / 太初寰宇俱乐部
              </span>

              <h1 class="club-title">
                太初寰宇俱乐部
              </h1>

              <p class="club-lead">
                太初寰宇俱乐部 - 一个不太一样的俱乐部。
                我们积极与社区互动，听取用户意见
              </p>

              <p class="club-lead-sub">
                从游戏陪玩，到社区服务器，再到我们正在开发的自研游戏——
                每一位板板，都可以在这个社区里里留下属于自己的痕迹。
              </p>
              <div class="club-actions"><RouterLink to="/taichu/operators" class="btn-line btn-primary">寻找陪玩</RouterLink><RouterLink to="/taichu/orders" class="btn-line">我的订单</RouterLink><a href="#club-register" class="btn-line" @click.prevent="handleJoin">加入俱乐部</a></div>
            </div>

            <div class="club-meta">

              <div class="club-meta-item">
                <span>TACTICAL</span>
                <strong>战术协同</strong>
              </div>

              <div class="club-meta-item">
                <span>MULTI-GAME</span>
                <strong>多游戏覆盖</strong>
              </div>

              <div class="club-meta-item">
                <span>COMMUNITY</span>
                <strong>社区共建</strong>
              </div>

            </div>

          </div>

          <!-- ---------- 专属福利 ---------- -->
          <div class="perk-heading">
            <span class="perk-heading-label">EARLY ACCESS PRIVILEGE</span>
            <span class="perk-heading-line"></span>
            <span class="perk-heading-count">社区早期开发福利</span>
          </div>

          <div class="perk-intro">

            <p>
              这是太初社区<strong>早期开发阶段</strong>推出的专属福利。
              累计消费满 <strong>¥2000</strong>，老板即可在下方两种彩蛋中
              <strong>任选其一</strong>，作为你在太初世界里的永久纪念。
            </p>

            <span class="perk-note">
              <i></i>
              福利前置条件后续可能调整，以最新公告为准
            </span>

          </div>

          <div class="perk-grid">

            <article class="perk-card">

              <span class="perk-card-line"></span>

              <div class="perk-top">
                <span class="perk-index">OPTION 01</span>
                <span class="perk-tag">MINECRAFT</span>
              </div>

              <h3 class="perk-title">
                社区服务器
                <br />
                MOD 彩蛋
              </h3>

              <p class="perk-desc">
                我们会在太初社区 Minecraft 服务器里，
                为老板制作一个专属 MOD 彩蛋，
                作为你在服务器中的纪念。
              </p>

              <ul class="perk-list">
                <li>社区服内专属 MOD 彩蛋</li>
                <li>可留下你的署名 / 昵称</li>
                <li>服务器内长期保留</li>
              </ul>

            </article>

            <article class="perk-card">

              <span class="perk-card-line"></span>

              <div class="perk-top">
                <span class="perk-index">OPTION 02</span>
                <span class="perk-tag">TAICHU GAME</span>
              </div>

              <h3 class="perk-title">
                自研游戏
                <br />
                游戏彩蛋
              </h3>

              <p class="perk-desc">
                也可以把这份纪念放进我们正在开发的自研游戏里，
                成为一个只属于你的游戏彩蛋。
              </p>

              <ul class="perk-list">
                <li>自研游戏内专属彩蛋</li>
                <li>可留下你的署名 / 昵称</li>
                <li>随游戏版本更新长期保留</li>
              </ul>

            </article>

          </div>

          <div class="perk-foot-bar">
            <span class="perk-cond">
              <i></i>
              累计消费 ≥ ¥2000 · 二选一
            </span>
            <span class="perk-foot-note">
              两项福利不可叠加，具体形式以实际制作为准
            </span>
          </div>

          <!-- ---------- 周边福利（研发中） ---------- -->
          <div class="merch-block">

            <div class="merch-head">

              <div>
                <span class="merch-eyebrow">IN DEVELOPMENT</span>
                <h3>周边福利</h3>
              </div>

              <span class="merch-status">
                <i></i>
                研发 / 设计阶段
              </span>

            </div>

            <p class="merch-desc">
              我们正在为太初社区设计一整套专属周边。
              目前仍处于<strong>概念设计与研发阶段</strong>，
              具体形式、获取条件与上线时间均未确定，
              完成后会第一时间在社区与官网公布。
            </p>

            <div class="merch-list">

              <div class="merch-item">
                <span class="merch-phase">CONCEPT 01</span>
                <h4>实体周边</h4>
                <p>
                  徽章、钥匙扣、贴纸等社区纪念物，
                  仍在筛选风格与材质方案。
                </p>
              </div>

              <div class="merch-item">
                <span class="merch-phase">CONCEPT 02</span>
                <h4>联名设计</h4>
                <p>
                  与社区画师、设计师联动，
                  打造太初专属视觉周边。
                </p>
              </div>

              <div class="merch-item">
                <span class="merch-phase">CONCEPT 03</span>
                <h4>获取方式</h4>
                <p>
                  可能以社区活动、累计消费或积分兑换形式发放，
                  方案仍在设计中。
                </p>
              </div>

            </div>

            <span class="merch-note">
              <i></i>
              以上内容均为设计方向示意，不代表最终方案
            </span>

          </div>

          <!-- ---------- 未来活动 ---------- -->
          <div class="event-block">

            <div class="event-head">

              <div>
                <span class="event-eyebrow">INCOMING</span>
                <h3>未来的太初活动</h3>
              </div>

              <span class="event-note">持续筹备中 · 敬请期待</span>

            </div>

            <div class="event-list">

              <div class="event-item">
                <span class="event-phase">PHASE 01</span>
                <h4>社区建造活动</h4>
                <p>
                  围绕社区 MC 服务器展开的主题建造赛，
                  玩家与打手同场协作，共建太初地图。
                </p>
                <span class="event-status is-planning">筹备中</span>
              </div>

              <div class="event-item">
                <span class="event-phase">PHASE 02</span>
                <h4>自研游戏内测</h4>
                <p>
                  优先邀请长期板板参与内测，
                  共同打磨游戏内容。
                </p>
                <span class="event-status is-dev">开发中</span>
              </div>

              <div class="event-item">
                <span class="event-phase">PHASE 03</span>
                <h4>战术对抗赛</h4>
                <p>
                  线上线下联动的俱乐部对抗赛，
                  开放观战、应援与专属奖励。
                </p>
                <span class="event-status is-planning">规划中</span>
              </div>

              <div class="event-item">
                <span class="event-phase">PHASE 04</span>
                <h4>节庆限定活动</h4>
                <p>
                  春节、周年庆等节点推出限定任务、
                  限定福利与社区联动内容。
                </p>
                <span class="event-status is-planning">规划中</span>
              </div>

              <div class="event-item">
                <span class="event-phase">PHASE 05</span>
                <h4>跨游戏联动赛</h4>
                <p>
                  多款陪玩游戏同场联动，
                  板板与打手自由组队，赢取专属奖励。
                </p>
                <span class="event-status is-planning">筹备中</span>
              </div>

              <div class="event-item">
                <span class="event-phase">PHASE 06</span>
                <h4>社区共创计划</h4>
                <p>
                  邀请板板参与太初社区建设，
                  共同决定活动与内容方向。
                </p>
                <span class="event-status is-planning">规划中</span>
              </div>

            </div>

          </div>

        </div>

        <div class="club-hero-bottom">
          <span>TAICHU CLUB PROFILE</span>
          <div class="club-hero-line"></div>
          <span>EST. 2025</span>
        </div>

      </section>

      <!-- ==================== 数据状态 ==================== -->
      <section class="operations-section">

        <div class="section-container">

          <div class="operations-heading">

            <div>
              <span class="section-index">01 /</span>
              <h2>俱乐部概况</h2>
            </div>

            <span class="live-status">
              <i></i>
              社区服务数据
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
      <section id="club-register" ref="registerSection" class="section-block">
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
      this.$refs.registerSection?.scrollIntoView({ behavior: 'smooth', block: 'start' })
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
.club-home { max-width: 1200px; margin: auto; }
.club-hero-section { padding-top: 24px; }
.club-grid-bg, .scan-line, .club-hero-bottom, .perk-card-line, .perk-heading-line, .operation-line, .operation-index { display: none; }
.club-intro { padding: 32px; border: 1px solid var(--line); border-radius: 16px; background: var(--panel); margin-bottom: 28px; }
.club-eyebrow { color: var(--orange); font-size: 12px; letter-spacing: .08em; }
.club-title { font-size: clamp(28px,3vw,40px); line-height: 1.3; margin: 12px 0; }
.club-lead, .club-lead-sub { color: var(--text-soft); font-size: 14px; line-height: 1.8; max-width: 52em; }
.club-actions { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 24px; }
.club-meta { display: flex; gap: 24px; flex-wrap: wrap; margin-top: 28px; padding-top: 20px; border-top: 1px solid var(--line); }
.club-meta-item span { display: block; font-size: 12px; color: var(--text-muted); margin-bottom: 4px; }
.club-meta-item strong { font-size: 13px; font-weight: 500; }
.perk-heading, .perk-top, .perk-foot-bar, .merch-head, .event-head, .operations-heading { display: flex; align-items: center; justify-content: space-between; gap: 16px; flex-wrap: wrap; }
.perk-heading { font-size: 13px; margin-bottom: 16px; }
.perk-heading-label, .perk-index, .merch-eyebrow, .event-eyebrow, .merch-phase, .event-phase, .section-index { font-size: 12px; color: var(--text-muted); }
.perk-intro { line-height: 1.8; font-size: 14px; color: var(--text-soft); margin-bottom: 20px; }
.perk-note, .merch-note, .event-note, .perk-foot-note { display: block; color: var(--text-muted); font-size: 12px; margin-top: 8px; }
.perk-grid { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 20px; }
.perk-card, .merch-block, .event-item, .operation-item { border: 1px solid var(--line); border-radius: 12px; padding: 24px; background: var(--panel); }
.perk-tag, .event-status, .merch-status { display: inline-block; color: var(--orange); background: var(--accent-soft); font-size: 12px; padding: 4px 8px; border-radius: 6px; }
.perk-title { font-size: 24px; margin: 20px 0 12px; line-height: 1.5; }
.perk-title br { display: none; }
.perk-desc, .perk-list, .merch-desc, .merch-item p, .event-item p { color: var(--text-soft); font-size: 13px; line-height: 1.8; }
.perk-list { padding-left: 20px; margin-top: 14px; }
.perk-foot-bar { margin: 16px 0 32px; padding: 16px; border-radius: 10px; background: var(--bg-soft); font-size: 12px; }
.merch-block, .event-block { margin-bottom: 32px; }
.merch-head h3, .event-head h3 { font-size: 22px; margin: 6px 0; }
.merch-desc { margin: 16px 0 24px; }
.merch-list, .event-list { display: grid; grid-template-columns: repeat(3,minmax(0,1fr)); gap: 20px; }
.merch-item { border-top: 1px solid var(--line); padding-top: 16px; }
.merch-item h4, .event-item h4 { margin: 8px 0; font-size: 15px; }
.event-list { margin-top: 20px; }
.event-status { margin-top: 16px; }
.operations-section { margin: 32px 0; }
.operations-heading { margin-bottom: 20px; }
.operations-heading > div { display: flex; gap: 12px; align-items: center; }
.operations-heading h2 { font-size: 22px; }
.live-status { font-size: 12px; color: var(--text-soft); }
.operations-grid { display: grid; grid-template-columns: repeat(4,minmax(0,1fr)); gap: 16px; }
.operation-number { font-size: 32px; font-weight: 650; }
.operation-detail { display: grid; gap: 6px; margin-top: 16px; color: var(--text-soft); font-size: 12px; }
.operation-label { font-size: 12px; }
@media(max-width: 700px) { .club-intro { padding: 24px; } .perk-grid, .merch-list, .event-list { grid-template-columns: minmax(0,1fr); } .operations-grid { grid-template-columns: repeat(2,minmax(0,1fr)); } }
</style>
