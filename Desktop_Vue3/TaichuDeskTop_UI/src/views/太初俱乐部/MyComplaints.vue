<template>
  <div class="page-shell">
    <Navbar />

    <main>
      <section class="page-hero">
        <div class="section-container">
          <SectionHeading index="01" title="MY COMPLAINTS" subtitle="我的客诉 / COMPLAINT HISTORY" />
        </div>
      </section>

      <section class="section-block alt">
        <div class="section-container">

          <div v-if="!isLoggedIn" class="op-state">请先登录后查看客诉</div>
          <div v-else-if="loading" class="op-state">
            <span class="dot"></span> 正在读取客诉…
          </div>

          <div v-else-if="items.length === 0" class="op-state">
            暂无客诉记录
          </div>

          <div v-else class="cp-list">
            <article v-for="c in items" :key="c.id" class="cp-card" :class="c.status">
              <header class="cpc-head">
                <div class="cpc-left">
                  <span class="cpc-no mono">{{ c.orderNo }}</span>
                  <span class="cpc-type">{{ c.complaintTypeLabel }}</span>
                </div>
                <span :class="['cpc-status', c.status]">
                  {{ statusText(c.status) }}
                </span>
              </header>

              <div class="cpc-body">
                <div class="cpc-row">
                  <span>游戏</span>
                  <b>{{ c.gameName }} · {{ c.orderTypeName }}</b>
                </div>
                <div class="cpc-row">
                  <span>投诉对象</span>
                  <b>{{ c.targetOperatorName || '整单' }}</b>
                </div>
                <div class="cpc-row">
                  <span>投诉时间</span>
                  <b class="mono">{{ fmtTime(c.createdAt) }}</b>
                </div>
              </div>

              <!-- 描述 -->
              <div class="cpc-section">
                <div class="cps-label">投诉描述</div>
                <p class="cps-text">{{ c.description }}</p>
              </div>

              <!-- 证据 -->
              <div v-if="c.evidenceUrl" class="cpc-section">
                <div class="cps-label">证据截图</div>
                <a :href="c.evidenceUrl" target="_blank" rel="noopener" class="cps-evidence">
                  <img :src="c.evidenceUrl" alt="证据" />
                </a>
              </div>

              <!-- 客服回复 -->
              <div v-if="c.status !== 'pending'" class="cpc-reply" :class="c.status">
                <div class="cpr-head">
                  <span class="cpr-name">{{ c.handlerName || '客服' }}</span>
                  <span class="cpr-time mono">{{ fmtTime(c.handledAt) }}</span>
                </div>
                <p class="cpr-note">{{ c.handlerNote }}</p>
              </div>

              <div v-else class="cpc-pending-hint">
                ⏳ 客服正在处理中，请耐心等待
              </div>
            </article>
          </div>

          <div v-if="total > pageSize" class="cp-pager">
            <button :disabled="page <= 1" @click="changePage(page - 1)">上一页</button>
            <span class="mono">{{ page }} / {{ Math.ceil(total / pageSize) }}</span>
            <button :disabled="page >= Math.ceil(total / pageSize)" @click="changePage(page + 1)">下一页</button>
          </div>

        </div>
      </section>
    </main>

    <Footer />
  </div>
</template>

<script>
import Navbar         from './Navbar.vue'
import Footer         from './Footer.vue'
import SectionHeading from './SectionHeading.vue'
import request        from '@/utils/request'

export default {
  name: 'MyComplaints',
  components: { Navbar, Footer, SectionHeading },

  data() {
    return {
      loading: true,
      items: [],
      page: 1,
      pageSize: 20,
      total: 0
    }
  },

  computed: {
    isLoggedIn() { return !!localStorage.getItem('token') }
  },

  async mounted() {
    if (!this.isLoggedIn) { this.loading = false; return }
    await this.load()
    this.loading = false
  },

  methods: {
    async load() {
      try {
        const res = await request.get('/club/complaints/my', {
          params: { page: this.page, pageSize: this.pageSize }
        })
        const data = res?.data ?? res
        this.items = data?.items ?? []
        this.total = data?.total ?? 0
      } catch (e) {
        console.error('加载客诉失败', e)
        this.items = []
        this.total = 0
      }
    },

    changePage(p) {
      this.page = p
      this.load()
    },

    statusText(s) {
      return {
        pending:  '处理中',
        resolved: '已处理',
        rejected: '已驳回'
      }[s] || s
    },

    fmtTime(iso) {
      if (!iso) return '—'
      const d = new Date(iso)
      const pad = (n) => String(n).padStart(2, '0')
      return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
    }
  }
}
</script>

<style scoped>
.page-hero { padding: 120px 0 60px; border-bottom: 1px solid var(--line); }

.op-state {
  padding: 60px 32px; text-align: center;
  font-family: monospace; font-size: 12px;
  letter-spacing: 1px; color: var(--text-muted);
  border: 1px dashed var(--line);
  background: rgba(16, 25, 35, 0.3);
}
.dot {
  display: inline-block; width: 6px; height: 6px;
  margin-right: 6px; border-radius: 50%;
  background: var(--orange); animation: pulse 1.4s infinite;
}
@keyframes pulse { 50% { opacity: 0.3; } }

.cp-list { display: flex; flex-direction: column; gap: 16px; }

.cp-card {
  border: 1px solid var(--line);
  background: rgba(16, 25, 35, 0.5);
  transition: border-color 0.2s;
}
.cp-card:hover { border-color: var(--line-bright); }
.cp-card.pending  { border-left: 3px solid #ff8c4a; }
.cp-card.resolved { border-left: 3px solid #4ecb71; }
.cp-card.rejected { border-left: 3px solid #ff5050; }

.cpc-head {
  display: flex; justify-content: space-between; align-items: center;
  padding: 16px 20px;
  border-bottom: 1px solid var(--line);
}
.cpc-left { display: flex; align-items: center; gap: 14px; min-width: 0; }
.cpc-no { font-size: 13px; color: var(--text); }
.cpc-type {
  padding: 2px 8px;
  font-size: 11px;
  color: #ff8c4a;
  background: rgba(255, 140, 74, 0.1);
  border: 1px solid rgba(255, 140, 74, 0.3);
  border-radius: 3px;
}
.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }

.cpc-status {
  padding: 4px 12px;
  font-size: 11px; font-weight: 600;
  letter-spacing: 1px;
  border-radius: 3px;
  white-space: nowrap;
}
.cpc-status.pending  { background: rgba(255, 140, 74, 0.12); color: #ff8c4a; }
.cpc-status.resolved { background: rgba(80, 200, 120, 0.12); color: #4ecb71; }
.cpc-status.rejected { background: rgba(255, 80, 80, 0.12);  color: #ff5050; }

.cpc-body {
  padding: 14px 20px;
  display: flex; flex-direction: column; gap: 8px;
}
.cpc-row { display: flex; gap: 12px; font-size: 13px; }
.cpc-row span { color: var(--text-muted); min-width: 70px; }
.cpc-row b { color: var(--text); font-weight: 500; }

.cpc-section {
  padding: 14px 20px;
  border-top: 1px solid var(--line);
}
.cps-label {
  font-family: monospace; font-size: 10px;
  letter-spacing: 2px; color: var(--text-muted);
  margin-bottom: 8px;
}
.cps-text {
  margin: 0;
  font-size: 13px; line-height: 1.8;
  color: var(--text-soft);
  white-space: pre-wrap;
  word-break: break-word;
}
.cps-evidence {
  display: inline-block;
  border: 1px solid var(--line);
  border-radius: 3px;
  overflow: hidden;
}
.cps-evidence img {
  display: block;
  max-width: 240px;
  max-height: 180px;
  object-fit: cover;
}

.cpc-reply {
  padding: 14px 20px;
  background: rgba(0, 0, 0, 0.2);
  border-top: 1px solid var(--line);
}
.cpc-reply.resolved { background: rgba(80, 200, 120, 0.04); }
.cpc-reply.rejected { background: rgba(255, 80, 80, 0.04); }
.cpr-head {
  display: flex; justify-content: space-between; align-items: center;
  margin-bottom: 8px;
}
.cpr-name {
  font-size: 13px; font-weight: 700;
  color: var(--cyan);
}
.cpr-time { font-size: 10px; color: var(--text-muted); }
.cpr-note {
  margin: 0;
  font-size: 13px; line-height: 1.8;
  color: var(--text-soft);
  white-space: pre-wrap;
}

.cpc-pending-hint {
  padding: 14px 20px;
  font-size: 12px;
  color: #ff8c4a;
  background: rgba(255, 140, 74, 0.06);
  border-top: 1px solid rgba(255, 140, 74, 0.15);
}

.cp-pager {
  display: flex; align-items: center; justify-content: center;
  gap: 16px; margin-top: 22px;
  font-family: monospace; font-size: 12px;
  color: var(--text-muted);
}
.cp-pager button {
  padding: 6px 14px; font-size: 11px;
  color: var(--text-soft);
  background: transparent;
  border: 1px solid var(--line-bright);
  cursor: pointer;
}
.cp-pager button:hover:not(:disabled) {
  color: var(--text); border-color: var(--text-soft);
}
.cp-pager button:disabled { opacity: 0.4; cursor: not-allowed; }

@media (max-width: 520px) {
  .page-hero { padding: 80px 0 40px; }
  .cpc-head { flex-wrap: wrap; gap: 8px; }
  .cpc-body { padding: 12px 16px; }
  .cpc-section { padding: 12px 16px; }
  .cpc-reply { padding: 12px 16px; }
}
</style>