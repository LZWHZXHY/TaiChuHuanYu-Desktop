<template>
  <div class="op-section">
    <div class="op-toolbar">
      <div class="op-filters">
        <button
          v-for="f in filters"
          :key="f.value"
          class="filter-btn"
          :class="{ on: sort === f.value }"
          @click="sort = f.value"
        >{{ f.label }}</button>
      </div>
      <span class="op-total">
        {{ list.length }} OPERATORS
        <em v-if="onlineCount > 0" class="op-online-count">· {{ onlineCount }} ONLINE</em>
      </span>
    </div>

    <div v-if="loading" class="op-state">
      <span class="dot"></span> 正在读取打手阵列…
    </div>

    <div v-else-if="list.length === 0" class="op-state">
      当前没有已认证的打手
    </div>

    <div v-else class="op-grid">
      <article
        v-for="op in list"
        :key="op.userId"
        class="op-card"
        :class="{ 'is-offline': (op.status || 'offline') === 'offline' }"
      >
        <header class="op-head">
          <span class="op-id">{{ shortId(op.userId) }}</span>
          <OnlineStatusBadge :status="op.status || 'offline'" class="op-status-badge" />
          <span
            class="op-rank"
            :class="`rank-${(op.topLevel || 'L1').toLowerCase()}`"
          >{{ op.topLevel || 'L1' }}</span>
        </header>

        <div class="op-identity">
          <div class="op-avatar">{{ (op.name || '?').charAt(0) }}</div>
          <div class="op-name-wrap">
            <h3 class="op-name">{{ op.name }}</h3>
            <div class="op-roles">
              <span v-for="g in op.games" :key="g.gameCode">{{ g.gameName }}</span>
            </div>
          </div>
        </div>

        <p v-if="op.intro" class="op-intro">{{ op.intro }}</p>

        <div class="op-metrics">
          <div class="metric-row">
            <span class="metric-label">信誉分</span>
            <div class="metric-body">
              <span class="bar">
                <em :style="{ width: Math.min(op.reputation, 100) + '%' }"></em>
              </span>
              <b class="metric-value">{{ op.reputation }}</b>
            </div>
          </div>
          <div class="metric-row">
            <span class="metric-label">累计接单</span>
            <b class="metric-value">{{ op.totalOrders.toLocaleString() }}</b>
          </div>
          <div class="metric-row">
            <span class="metric-label">通过游戏</span>
            <b class="metric-value">{{ op.gameCount }} 款</b>
          </div>
        </div>

        <footer class="op-foot">
          <button class="op-btn ghost" @click="openDetail(op)">查看档案</button>
          <button
            class="op-btn solid"
            :disabled="(op.status || 'offline') === 'offline'"
            @click="$emit('order', op.userId)"
          >
            {{ (op.status || 'offline') === 'offline' ? '离线中' : '指定下单' }}
          </button>
        </footer>
      </article>
    </div>

    <!-- ================= 弹窗：打手档案 ================= -->
    <div v-if="detail" class="op-modal-mask" @click.self="closeDetail">
      <div class="op-modal">
        <header class="om-head">
          <div>
            <h3>
              {{ detail.name }}
              <OnlineStatusBadge
                :status="detail.status || 'offline'"
                class="om-head-status"
              />
            </h3>
            <p class="om-guid">GUID: {{ shortId(detail.userId) }}</p>
          </div>
          <button class="close-icon" @click="closeDetail">×</button>
        </header>

        <!-- 弹窗 Tab -->
        <div class="om-tabs">
          <button
            class="om-tab"
            :class="{ on: modalTab === 'radar' }"
            @click="modalTab = 'radar'"
          >能力雷达</button>
          <button
            class="om-tab"
            :class="{ on: modalTab === 'reviews' }"
            @click="switchToReviews"
          >收到的评价</button>
        </div>

        <!-- ====== Tab 1：能力雷达 ====== -->
        <template v-if="modalTab === 'radar'">
          <section class="om-block">
            <h4 class="om-title">能力雷达</h4>

            <div v-if="detail.games.length > 1" class="radar-game-tabs">
              <button
                v-for="g in detail.games"
                :key="g.gameCode"
                class="rg-tab"
                :class="{ on: radarGameCode === g.gameCode }"
                @click="switchRadarGame(g.gameCode)"
              >{{ g.gameName }}</button>
            </div>

            <div v-if="radarLoading" class="om-empty">
              <span class="dot"></span> 正在计算雷达数据…
            </div>

            <div v-else-if="radarDims.length === 0" class="om-empty">
              该游戏尚未配置任何评分维度
            </div>

            <template v-else>
              <div class="radar-wrap">
                <svg viewBox="-150 -150 300 300" class="radar-svg">
                  <polygon
                    v-for="lv in 4" :key="'g'+lv"
                    :points="radarGridPoints(lv)"
                    class="radar-grid"
                  />
                  <line
                    v-for="(ax, i) in radarAxes" :key="'a'+i"
                    x1="0" y1="0" :x2="ax.x" :y2="ax.y"
                    class="radar-axis"
                  />
                  <polygon :points="radarAvgPoints" class="radar-avg" />
                  <polygon :points="radarUserPoints" class="radar-user" />
                  <circle
                    v-for="(pt, i) in radarUserVertices" :key="'v'+i"
                    :cx="pt.x" :cy="pt.y" r="3.5" class="radar-dot"
                  />
                  <text
                    v-for="(lb, i) in radarLabels" :key="'l'+i"
                    :x="lb.x" :y="lb.y"
                    :text-anchor="lb.anchor"
                    class="radar-label"
                  >{{ lb.label }}</text>
                </svg>
              </div>

              <div class="radar-legend">
                <span class="lg-user"><i></i>个人</span>
                <span class="lg-avg"><i></i>平台平均</span>
              </div>

              <div class="radar-metrics">
                <div v-for="d in radarDims" :key="d.key" class="rm-row">
                  <span class="rm-label">{{ d.label }}</span>
                  <span class="rm-raw">{{ d.rawValue }}</span>
                  <div class="rm-bar">
                    <em :style="{ width: d.userScore + '%' }"></em>
                    <i :style="{ left: d.avgScore + '%' }"></i>
                  </div>
                  <b class="rm-score">{{ d.userScore }}</b>
                  <span class="rm-avg">均 {{ d.avgScore }}</span>
                </div>
              </div>
            </template>
          </section>

          <!-- 综合表现 -->
          <section class="om-block">
            <h4 class="om-title">综合表现</h4>
            <div class="om-grid">
              <div class="om-cell">
                <span>信誉分</span>
                <b>{{ detail.reputation }}</b>
              </div>
              <div class="om-cell">
                <span>累计接单</span>
                <b>{{ detail.totalOrders }}</b>
              </div>
              <div class="om-cell">
                <span>完成订单</span>
                <b>{{ detail.completedOrders }}</b>
              </div>
              <div class="om-cell">
                <span>取消订单</span>
                <b>{{ detail.cancelledOrders }}</b>
              </div>
            </div>
          </section>

          <!-- 认证游戏 -->
          <section class="om-block">
            <h4 class="om-title">认证游戏（{{ detail.gameCount }}）</h4>
            <div v-for="g in detail.games" :key="g.gameCode" class="om-game">
              <div class="og-head">
                <b>{{ g.gameName }}</b>
                <span class="og-level" v-if="g.level">{{ g.level }}</span>
                <span class="og-code">{{ g.code }}</span>
              </div>
              <div class="og-orders">
                接单 <b>{{ g.ordersInGame }}</b>
                · 完成 <b>{{ g.completedOrdersInGame }}</b>
                · 失败 <b>{{ g.failedOrdersInGame }}</b>
              </div>
            </div>
          </section>

          <section v-if="detail.intro" class="om-block">
            <h4 class="om-title">自我介绍</h4>
            <p class="om-intro">{{ detail.intro }}</p>
          </section>
        </template>

        <!-- ====== Tab 2：收到的评价 ====== -->
        <section v-if="modalTab === 'reviews'" class="om-block">
          <h4 class="om-title">收到的评价（{{ reviewsTotal }}）</h4>

          <div v-if="reviewsLoading" class="om-empty">
            <span class="dot"></span> 正在读取评价…
          </div>

          <div v-else-if="reviews.length === 0" class="om-empty">
            暂无评价
          </div>

          <div v-else class="rv-list">
            <div v-for="r in reviews" :key="r.id" class="rv-item">
              <div class="rv-item-head">
                <span class="rv-item-name">{{ r.customerName }}</span>
                <span class="rv-item-score">
                  {{ r.overallScore }}.0
                  <span class="rv-item-stars">
                    {{ '★'.repeat(r.overallScore) }}{{ '☆'.repeat(5 - r.overallScore) }}
                  </span>
                </span>
              </div>
              <div class="rv-item-meta">
                <span>{{ r.gameName }}</span>
                <span>{{ fmtDate(r.createdAt) }}</span>
              </div>
              <div v-if="r.comment" class="rv-item-comment">{{ r.comment }}</div>
              <div class="rv-item-dims">
                <span>技术 {{ r.skillScore }}</span>
                <span>态度 {{ r.attitudeScore }}</span>
                <span>准时 {{ r.punctualScore }}</span>
              </div>
            </div>
          </div>
        </section>

        <footer class="om-foot">
          <button class="om-btn ghost" @click="closeDetail">关闭</button>
          <button
            class="om-btn solid"
            :disabled="(detail.status || 'offline') === 'offline'"
            @click="handleOrderFromDetail"
          >
            {{ (detail.status || 'offline') === 'offline' ? '离线中' : '指定下单' }}
          </button>
        </footer>
      </div>
    </div>
  </div>
</template>

<script>
import request from '@/utils/request'
import OnlineStatusBadge from './OnlineStatusBadge.vue'

const ONLINE_RANK = { online: 0, busy: 1, resting: 2, offline: 3 }

export default {
  name: 'OperatorsSection',
  components: { OnlineStatusBadge },
  emits: ['view', 'order'],

  data() {
    return {
      loading: false,
      remoteList: [],
      detail: null,
      sort: 'reputation',
      filters: [
        { label: '信誉优先', value: 'reputation' },
        { label: '单量优先', value: 'totalOrders' },
        { label: '等级优先', value: 'topLevel' }
      ],
      // 雷达图
      radarGameCode: '',
      radarDims: [],
      radarLoading: false,
      // 弹窗 Tab
      modalTab: 'radar',
      reviews: [],
      reviewsTotal: 0,
      reviewsLoading: false
    }
  },

  computed: {
    list() {
      const cmp = {
        reputation:  (a, b) => b.reputation - a.reputation,
        totalOrders: (a, b) => b.totalOrders - a.totalOrders,
        topLevel:    (a, b) => (b.topLevel || '').localeCompare(a.topLevel || '')
      }
      return [...this.remoteList].sort((a, b) => {
        // ⭐ 在线优先
        const ra = ONLINE_RANK[a.status] ?? 9
        const rb = ONLINE_RANK[b.status] ?? 9
        if (ra !== rb) return ra - rb
        // 同状态内按所选维度排序
        return cmp[this.sort](a, b)
      })
    },

    onlineCount() {
      return this.remoteList.filter(op =>
        op.status === 'online' || op.status === 'busy'
      ).length
    },

    radarUserVertices() {
      const n = this.radarDims.length
      if (n === 0) return []
      return this.radarDims.map((d, i) => {
        const angle = (Math.PI * 2 * i) / n - Math.PI / 2
        const r = 100 * (Math.max(0, Math.min(100, d.userScore)) / 100)
        return {
          x: +(Math.cos(angle) * r).toFixed(2),
          y: +(Math.sin(angle) * r).toFixed(2)
        }
      })
    },
    radarUserPoints() {
      return this.radarUserVertices.map(p => `${p.x},${p.y}`).join(' ')
    },

    radarAvgPoints() {
      const n = this.radarDims.length
      if (n === 0) return ''
      return this.radarDims.map((d, i) => {
        const angle = (Math.PI * 2 * i) / n - Math.PI / 2
        const r = 100 * (Math.max(0, Math.min(100, d.avgScore)) / 100)
        return `${(Math.cos(angle) * r).toFixed(2)},${(Math.sin(angle) * r).toFixed(2)}`
      }).join(' ')
    },

    radarAxes() {
      const n = this.radarDims.length
      if (n === 0) return []
      return Array.from({ length: n }, (_, i) => {
        const angle = (Math.PI * 2 * i) / n - Math.PI / 2
        return {
          x: +(Math.cos(angle) * 100).toFixed(2),
          y: +(Math.sin(angle) * 100).toFixed(2)
        }
      })
    },

    radarLabels() {
      const n = this.radarDims.length
      if (n === 0) return []
      return this.radarDims.map((d, i) => {
        const angle = (Math.PI * 2 * i) / n - Math.PI / 2
        const r = 124
        const x = +(Math.cos(angle) * r).toFixed(2)
        const y = +(Math.sin(angle) * r).toFixed(2)
        const cos = Math.cos(angle)
        const anchor = Math.abs(cos) < 0.3 ? 'middle' : (cos > 0 ? 'start' : 'end')
        return { label: d.label, x, y, anchor }
      })
    }
  },

  mounted() {
    this.fetchList()
  },

  methods: {
    async fetchList() {
      this.loading = true
      try {
        const res = await request.get('/club/operators/list')
        const payload = res?.data ?? res
        this.remoteList = payload?.items ?? payload ?? []
      } catch (e) {
        console.error('拉取打手列表失败', e)
        this.remoteList = []
      } finally {
        this.loading = false
      }
    },

    shortId(uid) {
      return uid ? String(uid).substring(0, 8).toUpperCase() : '—'
    },

    openDetail(op) {
      this.detail = op
      this.modalTab = 'radar'
      this.reviews = []
      this.reviewsTotal = 0
      const first = op.games?.[0]?.gameCode
      if (first) {
        this.radarGameCode = first
        this.loadRadar()
      }
    },

    closeDetail() {
      this.detail = null
      this.radarDims = []
      this.radarGameCode = ''
      this.modalTab = 'radar'
      this.reviews = []
      this.reviewsTotal = 0
    },

    switchRadarGame(code) {
      if (code === this.radarGameCode) return
      this.radarGameCode = code
      this.loadRadar()
    },

    switchToReviews() {
      this.modalTab = 'reviews'
      if (this.reviews.length === 0 && !this.reviewsLoading) {
        this.loadReviews()
      }
    },

    async loadRadar() {
      if (!this.detail?.userId || !this.radarGameCode) return
      this.radarLoading = true
      try {
        const res = await request.get(`/club/operators/${this.detail.userId}/radar`, {
          params: { gameCode: this.radarGameCode }
        })
        const payload = res?.data ?? res
        this.radarDims = payload?.dimensions ?? []
      } catch (e) {
        console.error('拉取雷达数据失败', e)
        this.radarDims = []
      } finally {
        this.radarLoading = false
      }
    },

    async loadReviews() {
      if (!this.detail?.userId) return
      this.reviewsLoading = true
      try {
        const res = await request.get(`/club/operators/${this.detail.userId}/reviews`, {
          params: { pageSize: 20 }
        })
        const data = res?.data ?? res
        this.reviews = data?.items ?? []
        this.reviewsTotal = data?.total ?? 0
      } catch (e) {
        console.error('加载评价失败', e)
        this.reviews = []
        this.reviewsTotal = 0
      } finally {
        this.reviewsLoading = false
      }
    },

    handleOrderFromDetail() {
      if (!this.detail) return
      // ⭐ 离线打手禁止指定下单
      if ((this.detail.status || 'offline') === 'offline') return
      this.$emit('order', this.detail.userId)
      this.closeDetail()
    },

    radarGridPoints(level) {
      const n = this.radarDims.length
      if (n === 0) return ''
      const r = 100 * (level / 4)
      return Array.from({ length: n }, (_, i) => {
        const angle = (Math.PI * 2 * i) / n - Math.PI / 2
        return `${(Math.cos(angle) * r).toFixed(2)},${(Math.sin(angle) * r).toFixed(2)}`
      }).join(' ')
    },

    fmtDate(iso) {
      if (!iso) return '—'
      const d = new Date(iso)
      return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`
    }
  }
}
</script>

<style scoped>
/* ---- 工具栏 ---- */
.op-toolbar {
  display: flex; align-items: center; justify-content: space-between;
  gap: 16px; padding-bottom: 18px; margin-bottom: 26px;
  border-bottom: 1px solid var(--line);
}
.op-filters { display: flex; flex-wrap: wrap; gap: 8px; }
.filter-btn {
  padding: 6px 14px; font-size: 12px;
  color: var(--text-muted); background: transparent;
  border: 1px solid var(--line); cursor: pointer;
  transition: all 0.2s ease;
}
.filter-btn:hover { color: var(--text); border-color: var(--line-bright); }
.filter-btn.on {
  color: var(--orange);
  border-color: rgba(255, 107, 26, 0.45);
  background: rgba(255, 107, 26, 0.06);
}
.op-total {
  flex-shrink: 0; font-family: monospace;
  font-size: 12px; letter-spacing: 1px; color: var(--text-muted);
}
.op-online-count {
  font-style: normal;
  color: var(--cyan);
  margin-left: 4px;
}

.op-state {
  padding: 60px 32px; text-align: center;
  font-family: monospace; font-size: 12px; letter-spacing: 1px;
  color: var(--text-muted);
  border: 1px dashed var(--line);
  background: var(--panel);
}
.dot {
  display: inline-block; width: 6px; height: 6px;
  margin-right: 6px; border-radius: 50%;
  background: var(--orange);
  animation: pulse 1.4s infinite;
}
@keyframes pulse { 50% { opacity: 0.3; } }

.op-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 20px;
}

.op-card {
  position: relative;
  display: flex; flex-direction: column;
  padding: 20px;
  border: 1px solid var(--line);
  background: linear-gradient(160deg, var(--panel), var(--panel));
  transition: border-color 0.25s, transform 0.25s, background 0.25s, opacity 0.25s;
}
.op-card::before {
  content: ''; position: absolute; left: -1px; top: -1px;
  width: 14px; height: 14px;
  border-left: 2px solid var(--orange);
  border-top: 2px solid var(--orange);
  opacity: 0; transition: opacity 0.25s;
}
.op-card:hover {
  transform: translateY(-4px);
  border-color: rgba(255, 107, 26, 0.35);
}
.op-card:hover::before { opacity: 1; }

/* ⭐ 离线卡片淡化 */
.op-card.is-offline { opacity: 0.62; }
.op-card.is-offline:hover { opacity: 0.85; transform: none; }

.op-head {
  display: flex; align-items: center; gap: 10px;
  padding-bottom: 14px; border-bottom: 1px solid var(--line);
  font-family: monospace; font-size: 12px; letter-spacing: 1px;
}
.op-id { color: var(--text-muted); }
.op-status-badge { margin-left: auto; }
.op-rank {
  padding: 2px 7px;
  font-weight: 700; border: 1px solid;
}
.op-rank.rank-l5 { color: var(--orange); border-color: rgba(255, 107, 26, 0.5); background: rgba(255, 107, 26, 0.1); }
.op-rank.rank-l4 { color: var(--orange); border-color: rgba(255, 107, 26, 0.4); background: rgba(255, 107, 26, 0.08); }
.op-rank.rank-l3 { color: var(--cyan); border-color: rgba(120, 215, 206, 0.35); background: rgba(120, 215, 206, 0.06); }
.op-rank.rank-l2 { color: var(--text-soft); border-color: var(--line-bright); }
.op-rank.rank-l1 { color: var(--text-muted); border-color: var(--line); }

.op-identity { display: flex; align-items: center; gap: 13px; padding: 18px 0 12px; }
.op-avatar {
  flex-shrink: 0; width: 46px; height: 46px;
  display: grid; place-items: center;
  border: 1px solid var(--line-bright);
  background: var(--panel-light);
  font-size: 19px; font-weight: 700; color: var(--orange);
}
.op-name-wrap { min-width: 0; }
.op-name { margin: 0 0 6px; font-size: 17px; font-weight: 700; letter-spacing: 1px; }
.op-roles { display: flex; flex-wrap: wrap; gap: 6px; }
.op-roles span {
  padding: 2px 7px; color: var(--text-muted);
  font-family: monospace; font-size: 12px; letter-spacing: 1px;
  border: 1px solid var(--line);
}

.op-intro {
  margin: 0 0 14px; padding: 10px 12px;
  font-size: 12px; line-height: 1.7;
  color: var(--text-soft);
  background: var(--panel);
  border-left: 2px solid var(--line-bright);
}

.op-metrics {
  padding: 14px 0;
  border-top: 1px solid var(--line);
  border-bottom: 1px solid var(--line);
}
.metric-row {
  display: flex; align-items: center; justify-content: space-between;
  gap: 12px; padding: 6px 0;
}
.metric-label {
  flex-shrink: 0; color: var(--text-muted);
  font-family: monospace; font-size: 12px; letter-spacing: 1px;
}
.metric-body { display: flex; align-items: center; gap: 7px; min-width: 0; }
.metric-value { font-family: monospace; font-size: 12px; font-weight: 600; color: var(--text); }

.bar {
  display: block; width: 62px; height: 3px;
  border-radius: 2px; background: var(--bg-soft);
  overflow: hidden;
}
.bar em {
  display: block; height: 100%;
  background: linear-gradient(90deg, rgba(120, 215, 206, 0.35), var(--cyan));
  border-radius: 2px;
  transition: width 0.7s cubic-bezier(0.22, 1, 0.36, 1);
}

.op-foot { display: flex; gap: 8px; margin-top: auto; padding-top: 18px; }
.op-btn {
  flex: 1; height: 36px;
  font-size: 12px; letter-spacing: 1px;
  cursor: pointer; transition: all 0.22s ease;
}
.op-btn:disabled { opacity: 0.4; cursor: not-allowed; }
.op-btn.ghost {
  color: var(--text-soft); background: transparent;
  border: 1px solid var(--line-bright);
}
.op-btn.ghost:hover { color: var(--orange); border-color: rgba(255, 107, 26, 0.45); }
.op-btn.solid {
  color: var(--on-accent); background: var(--orange);
  border: 1px solid var(--orange);
}
.op-btn.solid:hover:not(:disabled) { background: var(--orange-bright); }
.op-btn.solid:disabled { background: var(--line); border-color: var(--line); color: var(--text-muted); }

/* ====== 弹窗 ====== */
.op-modal-mask {
  position: fixed; inset: 0; z-index: 9999;
  background: var(--overlay);
  backdrop-filter: blur(8px);
  display: grid; place-items: center;
  padding: 24px;
}
.op-modal {
  width: min(720px, 100%);
  max-height: 88vh;
  overflow-y: auto;
  border: 1px solid var(--line);
  background: var(--paper-card);
  color: var(--text-primary);
  border-radius: 4px;
  box-shadow: 0 24px 80px rgba(0, 0, 0, 0.6);
}
.om-head {
  display: flex; align-items: flex-start; justify-content: space-between;
  gap: 20px; padding: 22px 26px 14px;
}
.om-head h3 {
  margin: 0 0 6px; font-size: 20px; font-weight: 700;
  letter-spacing: 1px; color: var(--text-primary);
  display: flex; align-items: center; gap: 10px;
}
.om-head-status { transform: translateY(1px); }
.om-guid { margin: 0; font-family: monospace; font-size: 12px; letter-spacing: 1px; color: var(--text-secondary); }
.close-icon {
  background: none; border: none;
  font-size: 24px; color: var(--text-secondary);
  cursor: pointer; line-height: 1; transition: color 0.2s;
}
.close-icon:hover { color: var(--text-primary); }

/* 弹窗 Tab */
.om-tabs {
  display: flex;
  gap: 0;
  padding: 0 26px;
  border-bottom: 1px solid var(--bg-soft);
}
.om-tab {
  padding: 12px 20px;
  font-size: 13px; letter-spacing: 1px;
  color: var(--text-secondary);
  background: transparent;
  border: none;
  border-bottom: 2px solid transparent;
  cursor: pointer;
  transition: 0.2s;
  margin-bottom: -1px;
}
.om-tab:hover { color: var(--text-primary); }
.om-tab.on {
  color: var(--accent-color);
  border-bottom-color: var(--accent-color);
}

.om-block { padding: 22px 26px; border-bottom: 1px solid var(--bg-soft); }
.om-block:last-of-type { border-bottom: none; }
.om-title {
  margin: 0 0 16px;
  font-family: monospace; font-size: 12px;
  letter-spacing: 2px; color: var(--accent-color);
}

/* 游戏切换 */
.radar-game-tabs {
  display: flex; flex-wrap: wrap; gap: 6px;
  margin-bottom: 16px;
}
.rg-tab {
  padding: 6px 14px;
  font-size: 12px; letter-spacing: 1px;
  color: var(--text-secondary); background: transparent;
  border: 1px solid var(--line);
  cursor: pointer; transition: 0.2s;
  border-radius: 2px;
}
.rg-tab:hover { color: var(--text-primary); border-color: var(--line); }
.rg-tab.on {
  color: var(--accent-color);
  border-color: rgba(255, 107, 26, 0.5);
  background: rgba(255, 107, 26, 0.08);
}

/* 雷达图 */
.radar-wrap { display: grid; place-items: center; padding: 6px 0 10px; }
.radar-svg { width: 100%; max-width: 340px; height: auto; }
.radar-grid { fill: none; stroke: var(--border-line); stroke-width: 1; }
.radar-axis { stroke: var(--border-line); stroke-width: 1; }
.radar-avg {
  fill: rgba(160, 170, 180, 0.08);
  stroke: rgba(160, 170, 180, 0.6);
  stroke-width: 1.4;
  stroke-dasharray: 5 4;
}
.radar-user {
  fill: rgba(255, 107, 26, 0.22);
  stroke: var(--accent-color);
  stroke-width: 1.8;
  filter: drop-shadow(0 0 8px rgba(255, 107, 26, 0.4));
}
.radar-dot { fill: var(--accent-color); stroke: var(--paper-card); stroke-width: 1; }
.radar-label {
  fill: var(--text-secondary); font-family: monospace;
  font-size: 12px; letter-spacing: 1px; font-weight: 600;
}

/* 图例 */
.radar-legend {
  display: flex; justify-content: center; gap: 22px;
  padding: 6px 0 14px;
  font-family: monospace; font-size: 12px;
  letter-spacing: 1px; color: var(--text-secondary);
}
.radar-legend span { display: flex; align-items: center; gap: 6px; }
.radar-legend i { display: inline-block; width: 14px; height: 2px; }
.lg-user i { background: var(--accent-color); }
.lg-avg i {
  background-image: linear-gradient(90deg, var(--text-secondary) 50%, transparent 50%);
  background-size: 4px 2px;
}

/* 明细表 */
.radar-metrics {
  display: flex; flex-direction: column; gap: 8px;
  padding-top: 14px;
  border-top: 1px dashed var(--line);
}
.rm-row {
  display: grid;
  grid-template-columns: 64px 48px 1fr 36px 44px;
  align-items: center; gap: 8px;
  font-size: 12px;
}
.rm-label {
  font-family: monospace; font-size: 12px;
  letter-spacing: 1px; color: var(--text-secondary);
}
.rm-raw {
  font-family: monospace; font-size: 12px;
  color: var(--text-secondary);
  white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
}
.rm-bar {
  position: relative;
  height: 4px;
  border-radius: 2px;
  background: var(--bg-soft);
  overflow: hidden;
}
.rm-bar em {
  display: block; height: 100%;
  background: linear-gradient(90deg, rgba(255, 107, 26, 0.4), var(--accent-color));
  border-radius: 2px;
  transition: width 0.6s cubic-bezier(0.22, 1, 0.36, 1);
}
.rm-bar i {
  position: absolute; top: -2px; bottom: -2px;
  width: 2px; background: rgba(160, 170, 180, 0.9);
}
.rm-score {
  font-family: monospace; font-size: 12px;
  font-weight: 700; color: var(--text-primary);
  text-align: right;
}
.rm-avg {
  font-family: monospace; font-size: 12px;
  color: var(--text-secondary);
  text-align: right;
}

/* 综合格子 */
.om-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.om-cell {
  display: flex; flex-direction: column; gap: 6px;
  padding: 12px 14px;
  border: 1px solid var(--bg-soft);
  background: var(--paper-card);
  border-radius: 3px;
}
.om-cell span {
  font-family: monospace; font-size: 12px;
  letter-spacing: 1px; color: var(--text-secondary);
  text-transform: uppercase;
}
.om-cell b {
  font-size: 18px; font-weight: 700;
  color: var(--text-primary); font-family: monospace;
}

.om-empty {
  padding: 20px; text-align: center;
  font-family: monospace; font-size: 12px;
  letter-spacing: 1px; color: var(--text-secondary);
  border: 1px dashed var(--line);
  border-radius: 3px;
}

.om-game {
  padding: 14px 16px; margin-bottom: 10px;
  border: 1px solid var(--bg-soft);
  background: var(--paper-card);
  border-radius: 3px;
}
.om-game:last-child { margin-bottom: 0; }
.og-head {
  display: flex; align-items: center; gap: 10px;
  margin-bottom: 10px; padding-bottom: 10px;
  border-bottom: 1px dashed var(--line);
}
.og-head b { font-size: 14px; color: var(--text-primary); }
.og-level {
  padding: 2px 8px;
  font-family: monospace; font-size: 12px; font-weight: 700;
  color: var(--accent-color);
  border: 1px solid rgba(255, 107, 26, 0.45);
  background: rgba(255, 107, 26, 0.1);
  border-radius: 2px;
}
.og-code {
  margin-left: auto;
  font-family: monospace; font-size: 12px; color: var(--text-secondary);
}
.og-orders { font-size: 12px; color: var(--text-secondary); }
.og-orders b { color: var(--success); font-weight: 700; }

.om-intro {
  margin: 0; padding: 12px 14px;
  font-size: 12px; line-height: 1.7;
  color: var(--text-secondary);
  background: var(--paper-card);
  border-left: 2px solid var(--line);
  border-radius: 2px;
}

/* ====== 收到的评价 ====== */
.rv-list { display: flex; flex-direction: column; gap: 14px; }
.rv-item {
  padding: 14px 16px;
  border: 1px solid var(--bg-soft);
  background: var(--paper-card);
  border-radius: 3px;
}
.rv-item-head {
  display: flex; justify-content: space-between; align-items: center;
  margin-bottom: 8px;
}
.rv-item-name { font-size: 13px; font-weight: 600; color: var(--text-primary); }
.rv-item-score {
  display: flex; align-items: center; gap: 8px;
  font-family: monospace; font-size: 13px;
  color: var(--warning);
}
.rv-item-stars { letter-spacing: 2px; }
.rv-item-meta {
  display: flex; gap: 12px;
  font-family: monospace; font-size: 12px;
  color: var(--text-secondary);
  margin-bottom: 10px;
}
.rv-item-comment {
  font-size: 12px; line-height: 1.7;
  color: var(--text-secondary);
  padding: 10px 12px;
  background: var(--panel);
  border-left: 2px solid var(--line);
  border-radius: 2px;
  margin-bottom: 10px;
}
.rv-item-dims {
  display: flex; gap: 14px;
  font-family: monospace; font-size: 12px;
  color: var(--text-secondary);
}

.om-foot {
  display: flex; gap: 12px;
  padding: 20px 26px;
  border-top: 1px solid var(--bg-soft);
}
.om-btn {
  flex: 1; height: 42px;
  font-size: 12px; letter-spacing: 1px;
  cursor: pointer; transition: all 0.2s;
  border-radius: 3px;
}
.om-btn:disabled { opacity: 0.4; cursor: not-allowed; }
.om-btn.ghost {
  color: var(--text-secondary); background: transparent;
  border: 1px solid var(--line);
}
.om-btn.ghost:hover { color: var(--text-primary); border-color: var(--line); }
.om-btn.solid {
  color: var(--on-accent); background: var(--accent-color);
  border: 1px solid var(--accent-color);
}
.om-btn.solid:hover:not(:disabled) { background: var(--accent-color); }
.om-btn.solid:disabled { background: var(--bg-soft); border-color: var(--bg-soft); color: var(--text-secondary); }

@media (max-width: 520px) {
  .op-toolbar { flex-direction: column; align-items: flex-start; }
  .op-grid { grid-template-columns: 1fr; }
  .om-grid { grid-template-columns: 1fr; }
  .op-modal-mask { padding: 12px; }
  .op-modal { max-height: 92vh; }
}
</style>
