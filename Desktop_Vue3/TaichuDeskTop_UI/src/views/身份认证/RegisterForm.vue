<template>
  <div class="form-body">
    <div class="input-group">
      <label>昵称</label>
      <input v-model="form.username" placeholder="不建议使用邮箱" aria-label="昵称" />
    </div>

    <div class="input-group">
      <label>邮箱</label>
      <div class="email-input-wrapper">
        <input v-model="form.email" type="email" autocomplete="email" aria-label="邮箱" placeholder="email@example.com" />
        <button 
          type="button" 
          class="send-code-btn" 
          :disabled="countdown > 0 || !form.email" 
          @click="handleSendCode"
        >
          {{ countdown > 0 ? `${countdown}s` : '获取' }}
        </button>
      </div>
    </div>

    <div class="input-group">
      <label>邮箱验证码</label>
      <input v-model="form.verificationCode" placeholder="请输入6位验证码" maxlength="6" inputmode="numeric" autocomplete="one-time-code" aria-label="邮箱验证码" />
    </div>

    <div class="input-group">
      <label>密码</label>
      <input v-model="form.password" type="password" autocomplete="new-password" placeholder="不少于6位" aria-label="密码" />
    </div>

    <button @click="handleRegister" :disabled="loading" class="black-btn">
      {{ loading ? '正在注册…' : '注册' }}
    </button>

    <p role="status" v-if="message" :class="['message', isError ? 'error' : 'success']">
      {{ message }}
    </p>

    <div class="footer-links">
      <button type="button" class="text-link" @click="$emit('switch', 'login')">已有账号？返回登录</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { authApi } from '../../api/auth'
import { getErrorMessage } from '@/utils/apiError'

const emit = defineEmits(['switch'])

const form = reactive({
  username: '',
  password: '',
  email: '',
  verificationCode: '' // 必须与后端 DTO 字段名一致
})

const loading = ref(false)
const message = ref('')
const isError = ref(false)
const countdown = ref(0)

// 发送验证码逻辑
const handleSendCode = async () => {
  if (!form.email) return
  
  try {
    await authApi.sendCode(form.email)
    isError.value = false
    message.value = '验证码已发往你的邮箱'
    
    // 开启 60s 倒计时
    countdown.value = 60
    const timer = setInterval(() => {
      countdown.value--
      if (countdown.value <= 0) clearInterval(timer)
    }, 1000)
  } catch (err) {
    isError.value = true
    message.value = getErrorMessage(err, '发送失败')
  }
}

const handleRegister = async () => {
  if (!form.verificationCode) {
    isError.value = true
    message.value = '请填写验证码'
    return
  }

  loading.value = true
  message.value = ''
  
  try {
    await authApi.register(form)
    isError.value = false
    message.value = '认知成功，即将返回登录'
    setTimeout(() => emit('switch', 'login'), 1500)
  } catch (err) {
    isError.value = true
    message.value = getErrorMessage(err, '认知失败')
  } finally {
    loading.value = false
  }
}
</script>

<style scoped src="./auth-form.css"></style>
