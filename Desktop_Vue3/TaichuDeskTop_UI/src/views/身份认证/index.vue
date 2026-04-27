<template>
  <div class="auth-wrapper">
    <div class="auth-card">
      <component 
        :is="currentTab" 
        @switch="handleSwitch" 
        @login-success="onLoginSuccess"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import LoginForm from './LoginForm.vue'
import RegisterForm from './RegisterForm.vue'
import ForgotForm from './ForgotForm.vue'

const mode = ref('login') // login | register | forgot

// 动态匹配组件映射
const tabs: any = {
  login: LoginForm,
  register: RegisterForm,
  forgot: ForgotForm
}
const currentTab = computed(() => tabs[mode.value])

const handleSwitch = (newMode: string) => {
  mode.value = newMode
}

// 核心：登录成功后的全平台握手
const onLoginSuccess = (token: string) => {
  if ((window as any).chrome?.webview) {
    (window as any).chrome.webview.postMessage({
      cmd: "SAVE_AUTH_TOKEN",
      token: token
    });
  }
}
</script>

<style scoped>
.auth-wrapper { 
  height: 100%; display: flex; align-items: center; justify-content: center; 
  background: radial-gradient(circle, #1a1a1a 0%, #000 100%);
}
.auth-card {
  width: 350px; padding: 30px; background: #111;
  border: 1px solid #333; box-shadow: 0 0 20px rgba(0,0,0,0.5);
}

</style>