<template>
  <div class="reg-wrap">

    <!-- 加载中 -->
    <div v-if="loading" class="reg-state">
      <span class="dot"></span> 正在读取数据…
    </div>

    <!-- 未登录 -->
    <div v-else-if="!isLoggedIn" class="reg-state">
      <p class="state-title">未登录</p>
      <p class="state-desc">请先登录后再申请成为打手</p>
      <router-link to="/LoginRegister" class="reg-link">前往登录 →</router-link>
    </div>

    <!-- 已申请：极简状态条 -->
    <div v-else-if="hasApplied" class="applied-wrap">

      <div class="applied-bar" :class="overallStatus">
        <div class="ab-left">
          <span class="ab-dot"></span>
          <div class="ab-text">
            <p class="ab-title">{{ overallTitle }}</p>
            <p class="ab-sub">{{ overallSub }}</p>
          </div>
        </div>
        <div class="ab-actions">
          <!-- 有打手列表页面时打开这个 -->
          <!-- <router-link to="/Operators" class="ab-btn primary">查看打手列表 →</router-link> -->
          <button class="ab-btn" @click="showApplyForm = true">+ 申请新游戏</button>
        </div>
      </div>

      <!-- 追加游戏弹窗 -->
      <div v-if="showApplyForm" class="apply-modal">
        <div class="am-inner">
          <div class="am-head">
            <span>申请新游戏</span>
            <button @click="showApplyForm = false">✕</button>
          </div>

          <div class="am-body">
            <div class="reg-field">
              <label>选择游戏 <em>*</em></label>
              <select v-model="applyGameCode">
                <option value="" disabled>请选择</option>
                <option
                  v-for="g in availableGames"
                  :key="g.code"
                  :value="g.code"
                >{{ g.name }}</option>
              </select>
            </div>

            <div
              v-for="f in currentApplyFields"
              :key="f.key"
              class="reg-field"
            >
              <label>{{ f.label }} <em v-if="f.required">*</em></label>
              <select v-if="f.type === 'select'" v-model="applyMetrics[f.key]">
                <option value="" disabled>请选择</option>
                <option v-for="o in f.options" :key="o" :value="o">{{ o }}</option>
              </select>
              <input
                v-else
                v-model="applyMetrics[f.key]"
                :type="f.type"
                :placeholder="f.placeholder"
              />
            </div>
          </div>

          <div class="am-foot">
            <button class="am-cancel" @click="showApplyForm = false">取消</button>
            <button class="am-submit" :disabled="applyingGame" @click="submitGameApply">
              {{ applyingGame ? '提交中…' : '提交申请' }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- 未申请：首次申请表单 -->
    <form v-else class="reg-form" @submit.prevent="submit">
      <div class="form-section">
        <div class="fs-head">基础信息</div>
        <div class="reg-grid">
          <div class="reg-field">
            <label>打手名称 <em>*</em></label>
            <input v-model="form.nickname" placeholder="固定昵称，用于对外展示" />
          </div>
          <div class="reg-field">
            <label>联系方式 <em>*</em></label>
            <select v-model="form.contactType">
              <option value="" disabled>请选择</option>
              <option value="QQ">QQ</option>
              <option value="微信">微信</option>
              <option value="Discord">Discord</option>
              <option value="其他">其他</option>
            </select>
          </div>
          <div class="reg-field">
            <label>联系账号 <em>*</em></label>
            <input v-model="form.contactValue" placeholder="对应平台的账号 / ID" />
          </div>
          <div class="reg-field">
            <label>可在线时段 <em>*</em></label>
            <select v-model="form.onlineTime">
              <option value="" disabled>请选择</option>
              <option value="白天为主">白天为主</option>
              <option value="夜间为主">夜间为主</option>
              <option value="全天可排">全天可排</option>
              <option value="周末为主">周末为主</option>
            </select>
          </div>
          <div class="reg-field full">
            <label>自我介绍 / 战绩</label>
            <textarea v-model="form.intro" :rows="3"
              placeholder="简要描述你的竞技经历、擅长的战术风格等"></textarea>
          </div>
        </div>
      </div>

      <div class="form-section">
        <div class="fs-head">选择游戏 + 填写指标</div>
        <div class="reg-grid">
          <div class="reg-field full">
            <label>游戏 <em>*</em></label>
            <div class="game-picker">
              <button
                v-for="g in games"
                :key="g.code"
                type="button"
                :class="{ on: form.gameCode === g.code }"
                @click="form.gameCode = g.code; resetMetrics()"
              >
                {{ g.name }}
              </button>
            </div>
          </div>

          <div v-for="f in currentFields" :key="f.key" class="reg-field">
            <label>{{ f.label }} <em v-if="f.required">*</em></label>
            <select v-if="f.type === 'select'" v-model="metrics[f.key]">
              <option value="" disabled>请选择</option>
              <option v-for="o in f.options" :key="o" :value="o">{{ o }}</option>
            </select>
            <input
              v-else
              v-model="metrics[f.key]"
              :type="f.type"
              :placeholder="f.placeholder"
            />
          </div>
        </div>
      </div>

      <div class="reg-foot">
        <span class="reg-hint">
          提交后我们将在 24 小时内安排直播 / 双排考核
        </span>
        <button type="submit" class="reg-submit" :disabled="submitting">
          {{ submitting ? '提交中…' : '提交申请' }}
          <b>↗</b>
        </button>
      </div>
    </form>

  </div>
</template>

<script lang="ts">
import { defineComponent } from 'vue'
import {
  fetchGames,
  fetchMyOperatorStatus,
  submitOperatorApply,
  submitGameApply,
  type GameDef,
  type MyGameSkill
} from './api/operators'

export default defineComponent({
  name: 'OperatorRegister',

  data() {
    return {
      loading: true,
      submitting: false,
      applyingGame: false,

      games: [] as GameDef[],

      hasApplied: false,
      profile: {
        nickname: '',
        contactType: '',
        contactValue: '',
        onlineTime: '',
        intro: '',
        games: [] as MyGameSkill[]
      },

      form: {
        nickname: '',
        contactType: '',
        contactValue: '',
        onlineTime: '',
        intro: '',
        gameCode: ''
      },
      metrics: {} as Record<string, string>,

      showApplyForm: false,
      applyGameCode: '',
      applyMetrics: {} as Record<string, string>
    }
  },

  computed: {
    isLoggedIn(): boolean {
      return !!localStorage.getItem('token')
    },

    currentFields(): GameDef['fields'] {
      const g = this.games.find(x => x.code === this.form.gameCode)
      return g ? g.fields : []
    },

    currentApplyFields(): GameDef['fields'] {
      const g = this.games.find(x => x.code === this.applyGameCode)
      return g ? g.fields : []
    },

    availableGames(): GameDef[] {
      const appliedCodes = new Set(this.profile.games.map(g => g.gameCode))
      return this.games.filter(g => !appliedCodes.has(g.code))
    },

    /** 整体状态（取最"积极"的一个作为首页展示） */
    overallStatus(): string {
      const list = this.profile.games
      if (list.some(g => g.auditStatus === 'approved')) return 'approved'
      if (list.some(g => g.auditStatus === 'reviewing')) return 'reviewing'
      if (list.some(g => g.auditStatus === 'pending')) return 'pending'
      if (list.some(g => g.auditStatus === 'banned')) return 'banned'
      if (list.some(g => g.auditStatus === 'rejected')) return 'rejected'
      return 'pending'
    },

    overallTitle(): string {
      switch (this.overallStatus) {
        case 'approved':  return '你已是太初打手'
        case 'reviewing': return '考核进行中'
        case 'pending':   return '申请已提交'
        case 'rejected':  return '申请未通过'
        case 'banned':    return '账号已封禁'
        default:          return '申请已提交'
      }
    },

    overallSub(): string {
      const total = this.profile.games.length
      const approved = this.profile.games.filter(g => g.auditStatus === 'approved').length
      const pending = this.profile.games.filter(g =>
        g.auditStatus === 'pending' || g.auditStatus === 'reviewing'
      ).length

      switch (this.overallStatus) {
        case 'approved':
          return `共申报 ${total} 个游戏 · ${approved} 个已通过` +
                 (pending > 0 ? ` · ${pending} 个审核中` : '')
        case 'reviewing':
          return `正在安排考核，请留意联系方式`
        case 'pending':
          return `共申报 ${total} 个游戏，等待管理员审核`
        case 'rejected':
          return `如有疑问请联系客服，或重新提交申请`
        case 'banned':
          return `如需申诉请联系客服`
        default:
          return `共申报 ${total} 个游戏`
      }
    }
  },

  async mounted() {
    try {
      this.games = await fetchGames()
    } catch (e) {
      console.error('拉取游戏列表失败', e)
    }

    if (!this.isLoggedIn) {
      this.loading = false
      return
    }

    try {
      const data = await fetchMyOperatorStatus()
      if (data.hasApplied) {
        this.hasApplied = true
        this.profile.nickname = data.nickname || ''
        this.profile.contactType = data.contactType || ''
        this.profile.contactValue = data.contactValue || ''
        this.profile.onlineTime = data.onlineTime || ''
        this.profile.intro = data.intro || ''
        this.profile.games = data.games || []
      }
    } catch (e) {
      console.error('读取打手状态失败', e)
    } finally {
      this.loading = false
    }
  },

  methods: {
    resetMetrics() {
      this.metrics = {}
    },

    async submit() {
      if (!this.form.nickname.trim()) return this.$emit('invalid', ['打手名称'])
      if (!this.form.contactType) return this.$emit('invalid', ['联系方式'])
      if (!this.form.contactValue.trim()) return this.$emit('invalid', ['联系账号'])
      if (!this.form.onlineTime) return this.$emit('invalid', ['在线时段'])
      if (!this.form.gameCode) return this.$emit('invalid', ['游戏'])

      const missing = this.currentFields
        .filter(f => f.required && !this.metrics[f.key])
        .map(f => f.label)
      if (missing.length) return this.$emit('invalid', missing)

      this.submitting = true
      try {
        await submitOperatorApply({
          nickname: this.form.nickname.trim(),
          contactType: this.form.contactType,
          contactValue: this.form.contactValue.trim(),
          onlineTime: this.form.onlineTime,
          intro: this.form.intro?.trim() || undefined,
          gameCode: this.form.gameCode,
          metrics: { ...this.metrics }
        })

        const data = await fetchMyOperatorStatus()
        if (data.hasApplied) {
          this.hasApplied = true
          this.profile.nickname = data.nickname || ''
          this.profile.contactType = data.contactType || ''
          this.profile.contactValue = data.contactValue || ''
          this.profile.onlineTime = data.onlineTime || ''
          this.profile.intro = data.intro || ''
          this.profile.games = data.games || []
        }
        this.$emit('submitted')
      } catch (e) {
        console.error('提交失败', e)
        this.$emit('failed', e)
      } finally {
        this.submitting = false
      }
    },

    async submitGameApply() {
      if (!this.applyGameCode) return
      const missing = this.currentApplyFields
        .filter(f => f.required && !this.applyMetrics[f.key])
        .map(f => f.label)
      if (missing.length) return this.$emit('invalid', missing)

      this.applyingGame = true
      try {
        await submitGameApply({
          gameCode: this.applyGameCode,
          metrics: { ...this.applyMetrics }
        })

        const data = await fetchMyOperatorStatus()
        if (data.hasApplied) {
          this.profile.games = data.games || []
        }
        this.showApplyForm = false
        this.applyGameCode = ''
        this.applyMetrics = {}
        this.$emit('submitted')
      } catch (e) {
        console.error('追加游戏失败', e)
        this.$emit('failed', e)
      } finally {
        this.applyingGame = false
      }
    }
  }
})
</script>

<style scoped>
.reg-wrap { width: 100%; }

/* ===== 通用 ===== */
.reg-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 22px; }
.reg-field { display: flex; flex-direction: column; gap: 9px; }
.reg-field.full { grid-column: 1 / -1; }
.reg-field label {
  font-family: monospace; font-size: 9px;
  letter-spacing: 1.5px; color: var(--text-soft);
}
.reg-field label em { font-style: normal; color: var(--orange); margin-left: 3px; }
.reg-field input,
.reg-field select,
.reg-field textarea {
  padding: 12px 14px; font-size: 13px; font-family: inherit;
  color: var(--text); background: rgba(0, 0, 0, 0.32);
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
  background: rgba(0, 0, 0, 0.45);
}

/* ===== 分区 ===== */
.form-section + .form-section {
  margin-top: 30px; padding-top: 30px;
  border-top: 1px solid var(--line);
}
.fs-head {
  margin-bottom: 18px;
  font-family: monospace; font-size: 10px;
  letter-spacing: 2px; color: var(--orange);
}

/* ===== 游戏选择器 ===== */
.game-picker { display: flex; flex-wrap: wrap; gap: 8px; }
.game-picker button {
  padding: 8px 18px; font-size: 12px;
  color: var(--text-soft); background: transparent;
  border: 1px solid var(--line); cursor: pointer;
  transition: all 0.2s ease;
}
.game-picker button:hover { color: var(--text); border-color: var(--line-bright); }
.game-picker button.on {
  color: var(--orange);
  border-color: rgba(255, 107, 26, 0.5);
  background: rgba(255, 107, 26, 0.08);
}

/* ===== 底部 ===== */
.reg-foot {
  display: flex; align-items: center; justify-content: space-between;
  gap: 20px; margin-top: 30px; padding-top: 24px;
  border-top: 1px solid var(--line);
}
.reg-hint {
  flex: 1; font-family: monospace; font-size: 9px;
  line-height: 1.7; letter-spacing: 0.5px; color: var(--text-muted);
}
.reg-submit {
  flex-shrink: 0;
  display: flex; align-items: center; gap: 24px;
  height: 46px; padding: 0 26px;
  font-size: 12px; letter-spacing: 1px; color: #fff;
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
  background: rgba(16, 25, 35, 0.4);
  color: var(--text-soft);
}
.reg-state p { margin: 0 0 10px; }
.state-title {
  font-size: 16px; font-weight: 700;
  color: var(--text); letter-spacing: 1px;
}
.state-desc {
  font-family: monospace; font-size: 11px;
  color: var(--text-muted); line-height: 1.8;
}
.reg-link {
  display: inline-block; margin-top: 12px;
  color: var(--orange); text-decoration: none;
  font-family: monospace; font-size: 12px; letter-spacing: 1px;
}
.reg-link:hover { text-decoration: underline; }

/* ===== 已申请：极简状态条 ===== */
.applied-wrap { width: 100%; }

.applied-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 24px;
  padding: 22px 28px;
  border: 1px solid var(--line);
  background: rgba(16, 25, 35, 0.4);
}
.applied-bar.approved   { border-color: rgba(120, 215, 206, 0.35); }
.applied-bar.reviewing  { border-color: rgba(255, 107, 26, 0.35); }
.applied-bar.rejected   { border-color: rgba(255, 80, 80, 0.35); }
.applied-bar.banned     { border-color: rgba(255, 80, 80, 0.45); opacity: 0.85; }

.ab-left { display: flex; align-items: center; gap: 16px; flex: 1; min-width: 0; }

.ab-dot {
  flex-shrink: 0;
  width: 10px; height: 10px; border-radius: 50%;
  background: var(--text-soft);
  box-shadow: 0 0 0 4px rgba(255, 255, 255, 0.03);
}
.applied-bar.approved  .ab-dot { background: var(--cyan); box-shadow: 0 0 0 4px rgba(120, 215, 206, 0.12); }
.applied-bar.reviewing .ab-dot { background: var(--orange); box-shadow: 0 0 0 4px rgba(255, 107, 26, 0.12); }
.applied-bar.pending   .ab-dot { background: var(--text-soft); }
.applied-bar.rejected  .ab-dot { background: #ff5050; box-shadow: 0 0 0 4px rgba(255, 80, 80, 0.12); }
.applied-bar.banned    .ab-dot { background: #ff5050; }

.ab-text { min-width: 0; }
.ab-title {
  margin: 0 0 4px;
  font-size: 14px;
  font-weight: 700;
  letter-spacing: 1px;
  color: var(--text);
}
.ab-sub {
  margin: 0;
  font-family: monospace;
  font-size: 11px;
  letter-spacing: 0.5px;
  color: var(--text-muted);
  line-height: 1.6;
}

.ab-actions { display: flex; gap: 10px; flex-shrink: 0; }
.ab-btn {
  padding: 9px 18px;
  font-size: 12px;
  letter-spacing: 1px;
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
.ab-btn.primary:hover { color: #fff; background: var(--orange); border-color: var(--orange); }

/* ===== 追加游戏弹窗 ===== */
.apply-modal {
  position: fixed; inset: 0; z-index: 500;
  display: grid; place-items: center;
  background: rgba(0, 0, 0, 0.7);
  backdrop-filter: blur(6px);
}
.am-inner {
  width: min(520px, calc(100% - 40px));
  max-height: 85vh; overflow-y: auto;
  border: 1px solid var(--line);
  background: var(--panel, #0c131b);
}
.am-head {
  display: flex; align-items: center; justify-content: space-between;
  padding: 16px 22px;
  border-bottom: 1px solid var(--line);
}
.am-head span { font-size: 14px; font-weight: 700; letter-spacing: 1px; }
.am-head button {
  background: none; border: none;
  color: var(--text-muted); font-size: 16px; cursor: pointer;
}
.am-head button:hover { color: var(--text); }

.am-body {
  display: flex; flex-direction: column; gap: 18px;
  padding: 22px;
}

.am-foot {
  display: flex; gap: 12px; padding: 18px 22px;
  border-top: 1px solid var(--line);
}
.am-cancel, .am-submit {
  flex: 1; height: 42px;
  font-size: 12px; letter-spacing: 1px;
  cursor: pointer; transition: all 0.2s ease;
}
.am-cancel {
  color: var(--text-soft); background: transparent;
  border: 1px solid var(--line-bright);
}
.am-cancel:hover { color: var(--text); }
.am-submit {
  color: #fff; background: var(--orange);
  border: 1px solid var(--orange);
}
.am-submit:hover:not(:disabled) { background: var(--orange-bright); }
.am-submit:disabled { opacity: 0.55; cursor: not-allowed; }

/* ===== 加载点 ===== */
.dot {
  display: inline-block;
  width: 6px; height: 6px; margin-right: 6px;
  border-radius: 50%; background: var(--orange);
  animation: pulse 1.4s infinite;
}
@keyframes pulse { 50% { opacity: 0.3; } }

@media (max-width: 640px) {
  .reg-grid { grid-template-columns: 1fr; }
  .reg-foot { flex-direction: column; align-items: stretch; gap: 16px; }
  .reg-submit { justify-content: center; }
  .applied-bar { flex-direction: column; align-items: stretch; }
  .ab-actions { width: 100%; }
  .ab-btn { flex: 1; text-align: center; }
}
</style>