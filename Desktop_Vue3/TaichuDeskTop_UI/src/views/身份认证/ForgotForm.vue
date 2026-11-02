<template>
  <div class="form-body">
    <div class="input-group">
      <label>注册邮箱</label>
      <div class="email-input-wrapper">
        <input 
          v-model="form.email" 
          type="email" autocomplete="email" aria-label="邮箱" 
          placeholder="请输入注册时的邮箱" 
          :disabled="isSubmitting"
        />
        <button 
          type="button" 
          class="send-code-btn" 
          :disabled="countdown > 0 || !form.email || isSubmitting" 
          @click="handleSendCode"
        >
          {{ countdown > 0 ? `${countdown}s` : '获取验证码' }}
        </button>
      </div>
    </div>

    <div class="input-group">
      <label>邮箱验证码</label>
      <input 
        v-model="form.verificationCode" 
        placeholder="6位验证码" 
        maxlength="6" inputmode="numeric" autocomplete="one-time-code" 
        :disabled="isSubmitting"
      aria-label="邮箱验证码" />
    </div>

    <div class="input-group">
      <label>新密码</label>
      <input 
        v-model="form.newPassword" 
        type="password" autocomplete="new-password" 
        placeholder="不少于6位新密码" 
        @keyup.enter="handleReset"
        :disabled="isSubmitting"
      aria-label="新密码" />
    </div>

    <button @click="handleReset" :disabled="isSubmitting" class="black-btn">
      {{ isSubmitting ? '正在重置…' : '重置密码' }}
    </button>

    <div class="footer-links">
      <button type="button" class="text-link" @click="$emit('switch', 'login')">返回登录界面</button>
    </div>

    <p role="status" v-if="message" :class="['status-text', isError ? 'error-text' : 'success-text']">
      {{ message }}
    </p>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { getErrorMessage } from '@/utils/apiError'
import { authApi } from '../../api/auth' // 假设你已经在 auth.ts 中定义了 forgotPassword 或类似的 API

const emit = defineEmits(['switch'])

const form = ref({
  email: '',
  verificationCode: '',
  newPassword: ''
})

const isSubmitting = ref(false)
const message = ref('')
const isError = ref(false)
const countdown = ref(0)

const handleSendCode = async () => {
  if (!form.value.email) return
  
  try {

    await authApi.sendCode(form.value.email, 'reset')
    
    message.value = '验证码已发送到邮箱'
    isError.value = false
    
    countdown.value = 60
    const timer = setInterval(() => {
      countdown.value--
      if (countdown.value <= 0) clearInterval(timer)
    }, 1000)
  } catch (err) {
    isError.value = true
    message.value = getErrorMessage(err, '发送失败，请稍后重试')
  }
}

// 提交重置逻辑
const handleReset = async () => {
  // 1. 基础前端校验
  if (!form.value.email) {
    isError.value = true
    message.value = '请填写注册邮箱'
    return
  }
  if (!form.value.verificationCode || !form.value.newPassword) {
    isError.value = true
    message.value = '请填写完整的校验信息'
    return
  }
  if (form.value.newPassword.length < 6) {
    isError.value = true
    message.value = '新密码长度不能少于6位'
    return
  }

  isSubmitting.value = true
  message.value = ''
  
  try {
  
    const res = await authApi.resetPassword(form.value) 
    

    message.value = res.message || '密码重置成功，请重新登录'
    isError.value = false
    
  
    setTimeout(() => {
      emit('switch', 'login')
    }, 2000)
    
  } catch (err) {
    // 5. 错误处理
    isError.value = true
    // 优先显示后端返回的错误信息（如“验证码错误”、“用户不存在”等）
    message.value = getErrorMessage(err, '重置失败，验证码或有误')
  } finally {
    isSubmitting.value = false
  }
}
</script>

<style scoped src="./auth-form.css"></style>
