<template>
  <div class="auth-page workbench-skin editor-surface community-surface tone-world">
    <!-- 左侧粒子展台：站标 → 「太初」→ 徽记循环变形，鼠标可以拨动粒子 -->
    <aside class="auth-visual" aria-hidden="true">
      <span class="visual-code">TC-HY / ACCESS NODE</span>
      <ParticleMorph class="visual-particles" :shapes="authShapes" :interval="4800" />
      <p class="visual-caption"><b>太初寰宇</b>创作 · 相遇 · 共建</p>
    </aside>
    <div class="auth-container">
      <header class="auth-header studio-heading">
        <span class="auth-label">SYSTEM / AUTHENTICATION</span>
        <h1>{{ modeTitle }}</h1>
        <div class="auth-divider"></div>
      </header>

      <main class="auth-content">
        <component 
          :is="currentTab" 
          @switch="handleSwitch" 
          @login-success="onLoginSuccess"
        />
      </main>

      <footer class="auth-footer">
        <p>太初寰宇 · 账号服务</p>
        <p class="auth-icp"><a class="icp-link" href="https://beian.miit.gov.cn/" target="_blank" rel="noopener noreferrer">京ICP备2022020268号-2</a></p>
      </footer>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import ParticleMorph from '@/components/ParticleMorph.vue'
import { diamondShape, hexShape, type ParticleShape } from '@/utils/particleShapes'
import LoginForm from './LoginForm.vue'
import RegisterForm from './RegisterForm.vue'
import ForgotForm from './ForgotForm.vue'


const authShapes: ParticleShape[] = [
  { kind: 'image', src: '/taichu-logo.png' },
  { kind: 'text', text: '太初', scale: 0.5 },
  { kind: 'draw', draw: diamondShape },
  { kind: 'text', text: '寰宇', scale: 0.5 },
  { kind: 'draw', draw: hexShape },
]

const mode = ref('login') 

// 标题动态映射
const modeTitle = computed(() => {
  if (mode.value === 'register') return '注册账号'
  if (mode.value === 'forgot') return '找回密码'
  return '登录社区'
})

// 组件映射表
const tabs: Record<string, any> = {
  login: LoginForm,
  register: RegisterForm,
  forgot: ForgotForm
}

const currentTab = computed(() => tabs[mode.value])

const handleSwitch = (newMode: string) => {
  mode.value = newMode
}

const onLoginSuccess = (token: string) => {
  console.log("认证成功");

}
</script>

<style scoped>
/* 极简明亮 MD 风格布局 */
.auth-page {
  min-height: 100%;
  display: flex;
  align-items: stretch;
  justify-content: center;
  background: var(--paper-card); 
}

/* 左侧粒子展台 */
.auth-visual { position: relative; flex: 0 1 460px; min-height: 520px; display: flex; flex-direction: column; background: var(--inverse-bg); color: var(--inverse-text); clip-path: polygon(0 0, 100% 0, 100% calc(100% - 28px), calc(100% - 28px) 100%, 0 100%); overflow: hidden; }
.auth-visual::before { content: ''; position: absolute; inset: 0; pointer-events: none; background-image: linear-gradient(color-mix(in srgb, var(--inverse-text) 7%, transparent) 1px, transparent 1px), linear-gradient(90deg, color-mix(in srgb, var(--inverse-text) 7%, transparent) 1px, transparent 1px); background-size: 28px 28px; }
.visual-code { position: relative; margin: 18px 20px 0; padding-left: 8px; border-left: 2px solid var(--signal-mint); font: 10px var(--font-mono, monospace); letter-spacing: .14em; opacity: .75; }
.visual-particles { position: relative; flex: 1; width: 100%; min-height: 0; }
.visual-caption { position: relative; display: flex; justify-content: space-between; align-items: baseline; margin: 0 20px 22px; padding-top: 12px; border-top: 1px solid color-mix(in srgb, var(--inverse-text) 18%, transparent); font-size: 12px; letter-spacing: .08em; opacity: .8; }
.visual-caption b { font-size: 16px; letter-spacing: .12em; }

.auth-container {
  width: 100%;
  max-width: 460px; box-sizing: border-box;
  padding: 40px 20px 40px 40px;
  align-self: center;
}

.auth-header {
  text-align: center;
  margin-bottom: 28px;
}

.auth-label {
  font-family: ui-monospace, SFMono-Regular, monospace;
  font-size: 12px;
  color: var(--text-secondary);
  letter-spacing: 0.2em;
}

h1 {
  font-size: 1.75rem;
  font-weight: 600;
  color: var(--text-primary);
  margin-top: 12px;
  letter-spacing: -0.02em;
}

.auth-divider {
  width: 40px;
  height: 2px;
  background: var(--text-primary);
  margin: 20px auto 0;
}

.auth-content {
  background: var(--paper-card);
}

.auth-footer {
  margin-top: 28px;
  text-align: center;
  border-top: 1px solid var(--border-line);
  padding-top: 20px;
}

.auth-footer p {
  font-size: 12px;
  color: var(--text-secondary);
  font-family: ui-monospace, monospace;
}
.auth-icp { margin-top: 6px; }
.icp-link { color: var(--text-secondary); text-decoration: none; }
.icp-link:hover, .icp-link:focus-visible { color: var(--accent-color); text-decoration: underline; text-underline-offset: 3px; }

/* 适配移动端 */
@media (max-width: 768px) {
  .auth-page { flex-direction: column; align-items: center; }
  .auth-visual { flex: none; width: 100%; min-height: 0; height: 200px; clip-path: none; }
  .visual-caption { display: none; }
  .auth-container {
    padding: 20px;
  }
}
</style>