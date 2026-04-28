<template>
  <div class="auth-page">
    <div class="auth-container">
      <header class="auth-header">
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
        <p>太初寰宇 · 身份校验协议 v1.0.4</p>
      </footer>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import LoginForm from './LoginForm.vue'
import RegisterForm from './RegisterForm.vue'
import ForgotForm from './ForgotForm.vue'


const mode = ref('login') 

// 标题动态映射
const modeTitle = computed(() => {
  if (mode.value === 'register') return '身份创建 / Register'
  if (mode.value === 'forgot') return '密钥寻回 / Forgot'
  return '识海接入 / Login'
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
  align-items: center;
  justify-content: center;
  background: #ffffff; 
}

.auth-container {
  width: 100%;
  max-width: 400px;
  padding: 40px 20px;
}

.auth-header {
  text-align: center;
  margin-bottom: 40px;
}

.auth-label {
  font-family: ui-monospace, SFMono-Regular, monospace;
  font-size: 12px;
  color: #8c959f;
  letter-spacing: 0.2em;
}

h1 {
  font-size: 1.75rem;
  font-weight: 600;
  color: #1f2328;
  margin-top: 12px;
  letter-spacing: -0.02em;
}

.auth-divider {
  width: 40px;
  height: 2px;
  background: #1f2328;
  margin: 20px auto 0;
}

.auth-content {
  background: #ffffff;
}

.auth-footer {
  margin-top: 60px;
  text-align: center;
  border-top: 1px solid #f0f0f0;
  padding-top: 20px;
}

.auth-footer p {
  font-size: 12px;
  color: #afb8c1;
  font-family: ui-monospace, monospace;
}

/* 适配移动端 */
@media (max-width: 768px) {
  .auth-container {
    padding: 20px;
  }
}
</style>