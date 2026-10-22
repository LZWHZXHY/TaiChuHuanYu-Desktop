<template>
  <div class="page-shell">
    <Navbar />

    <main>
      <!-- ============ 页面标题 ============ -->
      <section class="page-hero">
        <div class="section-container">
          <SectionHeading
            index="01"
            title="CS REGISTER"
            subtitle="客服注册 / JOIN AS CUSTOMER SERVICE"
          />
        </div>
      </section>

      <!-- ============ 表单主体 ============ -->
      <section class="section-block alt">
        <div class="section-container">
          <div class="cs-reg-wrap">

            <!-- 加载中 -->
            <div v-if="loading" class="reg-state">
              <span class="dot"></span> 正在读取数据…
            </div>

            <!-- 未登录 -->
            <div v-else-if="!isLoggedIn" class="reg-state">
              <p class="state-title">未登录</p>
              <p class="state-desc">请先登录后再申请成为客服</p>
              <router-link to="/LoginRegister" class="reg-link">前往登录 →</router-link>
            </div>

            <!-- 已申请：状态展示 -->
            <div v-else-if="hasApplied" class="applied-wrap">

              <div class="applied-bar" :class="auditStatus">
                <div class="ab-left">
                  <span class="ab-dot"></span>
                  <div class="ab-text">
                    <p class="ab-title">{{ statusTitle }}</p>
                    <p class="ab-sub">{{ statusSub }}</p>
                  </div>
                </div>
                <div class="ab-actions">
                  <router-link
                    v-if="auditStatus === 'approved'"
                    to="/taichu/cs/dashboard"
                    class="ab-btn primary"
                  >
                    前往工作台 →
                  </router-link>
                  <button
                    v-if="auditStatus === 'rejected'"
                    class="ab-btn"
                    @click="resetAndReapply"
                  >
                    重新申请
                  </button>
                </div>
              </div>

              <!-- 展示已有信息 -->
              <div class="applied-info">
                <div class="ai-row">
                  <span>客服名称</span>
                  <b>{{ profile.name }}</b>
                </div>
                <div class="ai-row">
                  <span>客服微信</span>
                  <b class="mono">{{ profile.wechat }}</b>
                </div>
                <div class="ai-row">
                  <span>工作时间</span>
                  <b>{{ profile.onlineTime || '—' }}</b>
                </div>
                <div v-if="profile.intro" class="ai-row">
                  <span>自我介绍</span>
                  <b class="ai-intro">{{ profile.intro }}</b>
                </div>
                <div v-if="auditStatus === 'approved'" class="ai-row">
                  <span>当前评分</span>
                  <b class="accent">
                    {{ profile.reviewCount > 0 ? profile.avgOverall.toFixed(1) : '暂无' }}
                    <span v-if="profile.reviewCount > 0" class="ai-count">
                      （{{ profile.reviewCount }} 条评价）
                    </span>
                  </b>
                </div>
                <div v-if="profile.auditNote" class="ai-row">
                  <span>审核备注</span>
                  <b class="warn">{{ profile.auditNote }}</b>
                </div>
              </div>
            </div>

            <!-- 未申请：表单 -->
            <form v-else class="reg-form" @submit.prevent="submit">

              <div class="form-section">
                <div class="fs-head">基础信息</div>
                <div class="reg-grid">
                  <div class="reg-field">
                    <label>客服名称 <em>*</em></label>
                    <input
                      v-model="form.name"
                      placeholder="对外展示的名字，比如「小太」"
                      maxlength="20"
                    />
                  </div>

                  <div class="reg-field">
                    <label>客服微信 <em>*</em></label>
                    <input
                      v-model="form.wechat"
                      placeholder="顾客添加你时用的微信号"
                      maxlength="50"
                    />
                  </div>

                  <div class="reg-field">
                    <label>工作时间 <em>*</em></label>
                    <select v-model="form.onlineTime">
                      <option value="" disabled>请选择</option>
                      <option value="白天为主">白天为主</option>
                      <option value="夜间为主">夜间为主</option>
                      <option value="全天在线">全天在线</option>
                      <option value="周末为主">周末为主</option>
                    </select>
                  </div>

                  <div class="reg-field full">
                    <label>自我介绍 / 从业经历</label>
                    <textarea
                      v-model="form.intro"
                      :rows="4"
                      maxlength="2000"
                      placeholder="简要描述你的客服经验、擅长领域等（可选）"
                    ></textarea>
                    <span class="field-counter">{{ form.intro.length }} / 2000</span>
                  </div>
                </div>
              </div>

              <div class="reg-foot">
                <span class="reg-hint">
                  提交后我们将在 24 小时内完成审核<br />
                  通过后即可在个人中心进入客服工作台
                </span>
                <button type="submit" class="reg-submit" :disabled="submitting">
                  {{ submitting ? '提交中…' : '提交申请' }}
                  <b>↗</b>
                </button>
              </div>
            </form>

          </div>
        </div>
      </section>
    </main>

    <Footer />
  </div>
</template>

<script>
import { useUserStore } from '@/stores/user'
import Navbar         from './Navbar.vue'
import Footer         from './Footer.vue'
import SectionHeading from './SectionHeading.vue'
import request        from '@/utils/request'

export default {
  name: 'CsRegister',
  components: { Navbar, Footer, SectionHeading },

  data() {
    return {
      loading: true,
      submitting: false,

      hasApplied: false,
      profile: {
        id: '',
        name: '',
        wechat: '',
        onlineTime: '',
        intro: '',
        capacity: 10,       // 保留：展示用，申请人不再填写
        auditStatus: 'pending',
        auditNote: '',
        reviewCount: 0,
        avgOverall: 0
      },

      form: {
        name: '',
        wechat: '',
        onlineTime: '',
        intro: ''
        // ❌ 删掉 capacity: 10
      }
    }
  },

  computed: {
    isLoggedIn() {
      return useUserStore().isLoggedIn
    },

    auditStatus() {
      return this.profile.auditStatus || 'pending'
    },

    statusTitle() {
      return {
        pending:   '申请已提交',
        approved:  '你已是太初客服',
        rejected:  '申请未通过',
        banned:    '账号已封禁'
      }[this.auditStatus] || '申请已提交'
    },

    statusSub() {
      return {
        pending:   '我们会尽快审核你的客服申请，请留意消息通知',
        approved:  '你可以前往客服工作台处理订单了',
        rejected:  '如有疑问请联系管理员，或修改信息后重新提交',
        banned:    '如需申诉请联系管理员'
      }[this.auditStatus] || ''
    }
  },

  async mounted() {
    if (!this.isLoggedIn) {
      this.loading = false
      return
    }
    await this.loadMyStatus()
    this.loading = false
  },

  methods: {
    async loadMyStatus() {
      try {
        const res = await request.get('/club/cs/me')
        const data = res?.data ?? res

        if (data?.hasApplied) {
          this.hasApplied = true
          this.profile = {
            id: data.id,
            name: data.name || '',
            wechat: data.wechat || '',
            onlineTime: data.onlineTime || '',
            intro: data.intro || '',
            capacity: data.capacity || 10,
            auditStatus: data.auditStatus || 'pending',
            auditNote: data.auditNote || '',
            reviewCount: data.reviewCount || 0,
            avgOverall: data.avgOverall || 0
          }
        } else {
          this.hasApplied = false
        }
      } catch (e) {
        console.error('读取客服状态失败', e)
      }
    },

    /** 被拒绝后，切换回表单重新申请 */
    resetAndReapply() {
      this.hasApplied = false
      // 用已有信息预填
      this.form = {
        name: this.profile.name,
        wechat: this.profile.wechat,
        onlineTime: this.profile.onlineTime,
        intro: this.profile.intro
        // ❌ 删掉 capacity
      }
    },

    async submit() {
      if (this.submitting) return

      // 前端校验
      if (!this.form.name.trim()) {
        alert('请填写客服名称')
        return
      }
      if (!this.form.wechat.trim()) {
        alert('请填写客服微信')
        return
      }
      if (!this.form.onlineTime) {
        alert('请选择工作时间')
        return
      }

      this.submitting = true
      try {
        await request.post('/club/cs/apply', {
          name: this.form.name.trim(),
          wechat: this.form.wechat.trim(),
          onlineTime: this.form.onlineTime,
          intro: this.form.intro.trim() || null
          // ❌ 删掉 capacity
        })

        // 重新拉状态
        await this.loadMyStatus()
        alert('申请已提交，请等待审核')
      } catch (e) {
        alert(e?.response?.data?.message || e.message || '提交失败')
      } finally {
        this.submitting = false
      }
    }
  }
}
</script>

<style scoped>
/* =========================================================
   页面框架
========================================================= */
.page-hero {
  padding: 120px 0 60px;
  border-bottom: 1px solid var(--line);
}

/* =========================================================
   表单主体
========================================================= */
.cs-reg-wrap { width: 100%; }

/* ===== 通用网格 ===== */
.reg-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 22px; }
.reg-field { display: flex; flex-direction: column; gap: 9px; position: relative; }
.reg-field.full { grid-column: 1 / -1; }
.reg-field label {
  font-family: monospace; font-size: 12px;
  letter-spacing: 1.5px; color: var(--text-soft);
}
.reg-field label em { font-style: normal; color: var(--orange); margin-left: 3px; }
.reg-field input,
.reg-field select,
.reg-field textarea {
  padding: 12px 14px; font-size: 13px; font-family: inherit;
  color: var(--text); background: var(--panel);
  border: 1px solid var(--line-bright); outline: none;
  resize: vertical;
  transition: border-color 0.2s ease, background 0.2s ease;
}
.reg-field input::placeholder,
.reg-field textarea::placeholder { color: var(--text-muted); }
.reg-field input:focus,
.reg-field select:focus,
.reg-field textarea:focus {
  border-color: rgba(255, 107, 26, 0.5);
  background: var(--panel);
}
.field-counter {
  position: absolute;
  right: 10px; bottom: 8px;
  font-family: monospace; font-size: 12px;
  color: var(--text-muted);
  pointer-events: none;
}

/* ===== 分区 ===== */
.form-section + .form-section {
  margin-top: 30px; padding-top: 30px;
  border-top: 1px solid var(--line);
}
.fs-head {
  margin-bottom: 18px;
  font-family: monospace; font-size: 12px;
  letter-spacing: 2px; color: var(--orange);
}

/* ===== 底部 ===== */
.reg-foot {
  display: flex; align-items: center; justify-content: space-between;
  gap: 20px; margin-top: 30px; padding-top: 24px;
  border-top: 1px solid var(--line);
}
.reg-hint {
  flex: 1; font-family: monospace; font-size: 12px;
  line-height: 1.7; letter-spacing: 0.5px; color: var(--text-muted);
}
.reg-submit {
  flex-shrink: 0;
  display: flex; align-items: center; gap: 24px;
  height: 46px; padding: 0 26px;
  font-size: 12px; letter-spacing: 1px; color: var(--on-accent);
  background: var(--orange); border: 1px solid var(--orange);
  cursor: pointer; transition: all 0.25s ease;
}
.reg-submit b { font-size: 17px; font-weight: 400; }
.reg-submit:hover:not(:disabled) {
  background: var(--orange-bright);
  box-shadow: 0 8px 30px rgba(255, 107, 26, 0.2);
}
.reg-submit:disabled { opacity: 0.55; cursor: not-allowed; }

/* ===== 状态页 ===== */
.reg-state {
  padding: 48px 32px; text-align: center;
  border: 1px solid var(--line);
  background: var(--panel);
  color: var(--text-soft);
}
.reg-state p { margin: 0 0 10px; }
.state-title {
  font-size: 16px; font-weight: 700;
  color: var(--text); letter-spacing: 1px;
}
.state-desc {
  font-family: monospace; font-size: 12px;
  color: var(--text-muted); line-height: 1.8;
}
.reg-link {
  display: inline-block; margin-top: 12px;
  color: var(--orange); text-decoration: none;
  font-family: monospace; font-size: 12px; letter-spacing: 1px;
}
.reg-link:hover { text-decoration: underline; }

/* ===== 已申请：状态条 ===== */
.applied-wrap { width: 100%; }

.applied-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 24px;
  padding: 22px 28px;
  margin-bottom: 24px;
  border: 1px solid var(--line);
  background: var(--panel);
  border-radius: 3px;
}
.applied-bar.approved   { border-color: rgba(120, 215, 206, 0.35); }
.applied-bar.pending    { border-color: rgba(255, 107, 26, 0.35); }
.applied-bar.rejected   { border-color: rgba(255, 80, 80, 0.35); }
.applied-bar.banned     { border-color: rgba(255, 80, 80, 0.45); opacity: 0.85; }

.ab-left { display: flex; align-items: center; gap: 16px; flex: 1; min-width: 0; }

.ab-dot {
  flex-shrink: 0;
  width: 10px; height: 10px; border-radius: 50%;
  background: var(--text-soft);
  box-shadow: 0 0 0 4px var(--bg-soft);
}
.applied-bar.approved  .ab-dot { background: var(--cyan); box-shadow: 0 0 0 4px rgba(120, 215, 206, 0.12); }
.applied-bar.pending   .ab-dot { background: var(--orange); box-shadow: 0 0 0 4px rgba(255, 107, 26, 0.12); }
.applied-bar.rejected  .ab-dot { background: var(--danger); box-shadow: 0 0 0 4px rgba(255, 80, 80, 0.12); }
.applied-bar.banned    .ab-dot { background: var(--danger); }

.ab-text { min-width: 0; }
.ab-title {
  margin: 0 0 4px;
  font-size: 14px; font-weight: 700;
  letter-spacing: 1px; color: var(--text);
}
.ab-sub {
  margin: 0;
  font-family: monospace; font-size: 12px;
  letter-spacing: 0.5px; color: var(--text-muted);
  line-height: 1.6;
}

.ab-actions { display: flex; gap: 10px; flex-shrink: 0; }
.ab-btn {
  padding: 9px 18px;
  font-size: 12px; letter-spacing: 1px;
  color: var(--text-soft);
  background: transparent;
  border: 1px solid var(--line-bright);
  text-decoration: none;
  cursor: pointer;
  transition: all 0.2s ease;
}
.ab-btn:hover { color: var(--text); border-color: var(--text-soft); }
.ab-btn.primary {
  color: var(--orange);
  border-color: rgba(255, 107, 26, 0.45);
  background: rgba(255, 107, 26, 0.06);
}
.ab-btn.primary:hover { color: var(--on-accent); background: var(--orange); border-color: var(--orange); }

/* ===== 已申请：信息展示 ===== */
.applied-info {
  border: 1px solid var(--line);
  background: var(--panel);
  border-radius: 3px;
}
.ai-row {
  display: flex;
  align-items: flex-start;
  gap: 20px;
  padding: 14px 22px;
  border-bottom: 1px solid var(--line);
  font-size: 13px;
}
.ai-row:last-child { border-bottom: none; }
.ai-row > span {
  flex-shrink: 0;
  width: 90px;
  font-family: monospace;
  font-size: 12px; letter-spacing: 1px;
  color: var(--text-muted);
  padding-top: 2px;
}
.ai-row > b {
  flex: 1;
  color: var(--text);
  font-weight: 600;
  word-break: break-word;
}
.ai-row > b.accent { color: var(--cyan); font-family: monospace; }
.ai-row > b.warn { color: var(--accent-color); }
.ai-intro {
  font-weight: 400 !important;
  color: var(--text-soft) !important;
  line-height: 1.7;
}
.ai-count {
  font-size: 12px;
  color: var(--text-muted);
  font-weight: 400;
  margin-left: 4px;
}
.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }

/* ===== 加载点 ===== */
.dot {
  display: inline-block;
  width: 6px; height: 6px; margin-right: 6px;
  border-radius: 50%; background: var(--orange);
  animation: pulse 1.4s infinite;
}
@keyframes pulse { 50% { opacity: 0.3; } }

/* ===== 响应式 ===== */
@media (max-width: 768px) {
  .page-hero { padding: 80px 0 40px; }
}

@media (max-width: 640px) {
  .reg-grid { grid-template-columns: 1fr; }
  .reg-foot { flex-direction: column; align-items: stretch; gap: 16px; }
  .reg-submit { justify-content: center; }
  .applied-bar { flex-direction: column; align-items: stretch; }
  .ab-actions { width: 100%; }
  .ab-btn { flex: 1; text-align: center; }
  .ai-row { flex-direction: column; gap: 6px; }
  .ai-row > span { width: auto; }
}
</style>
