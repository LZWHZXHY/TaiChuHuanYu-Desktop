<template>
  <div class="form-body">
    <div class="input-group">
      <label>昵称</label>
      <input v-model="form.username" maxlength="20" placeholder="2–20 个字：中文、字母、数字、_ - ·" autocomplete="username" aria-label="昵称" />
    </div>

    <div class="input-group">
      <label>邮箱</label>
      <div class="email-input-wrapper">
        <input v-model="form.email" type="email" autocomplete="email" aria-label="邮箱" placeholder="email@example.com" />
        <button 
          type="button" 
          class="send-code-btn" 
          :disabled="countdown > 0 || !form.email || sending" 
          @click="handleSendCode"
        >
          {{ sending ? '发送中' : countdown > 0 ? `${countdown}s` : '获取' }}
        </button>
      </div>
    </div>

    <div class="input-group">
      <label>邮箱验证码</label>
      <input v-model="form.verificationCode" placeholder="请输入6位验证码" maxlength="6" inputmode="numeric" autocomplete="one-time-code" aria-label="邮箱验证码" />
    </div>

    <div class="input-group">
      <label>密码</label>
      <input v-model="form.password" type="password" autocomplete="new-password" maxlength="64" placeholder="不少于6位" aria-label="密码" @keyup.enter="handleRegister" />
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
const sending = ref(false)

// 和后端规则一致：名号 2–20 个字，只能中文、字母、数字和 _ - ·
const NAME_RE = /^[\p{L}\p{Nd}_\-·]+$/u
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
function checkForm(): string | null {
  const name = form.username.trim()
  if (name.length < 2 || name.length > 20) return '昵称需要 2–20 个字'
  if (!NAME_RE.test(name)) return '昵称只能包含中文、字母、数字和 _ - · 这几个符号'
  if (!EMAIL_RE.test(form.email.trim())) return '邮箱格式不对，请检查一下'
  if (!form.verificationCode.trim()) return '请填写验证码'
  if (form.password.length < 6) return '密码至少 6 位'
  return null
}

// 发送验证码逻辑
const handleSendCode = async () => {
  if (!form.email || sending.value) return
  if (!EMAIL_RE.test(form.email.trim())) { isError.value = true; message.value = '邮箱格式不对，请检查一下'; return }

  sending.value = true
  try {
    await authApi.sendCode(form.email, 'register')
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
  } finally {
    sending.value = false
  }
}

const handleRegister = async () => {
  const problem = checkForm()
  if (problem) {
    isError.value = true
    message.value = problem
    return
  }

  loading.value = true
  message.value = ''
  
  try {
    await authApi.register({ ...form, username: form.username.trim(), email: form.email.trim(), verificationCode: form.verificationCode.trim() })
    isError.value = false
    message.value = '注册成功，正在返回登录'
    setTimeout(() => emit('switch', 'login'), 1500)
  } catch (err) {
    isError.value = true
    message.value = getErrorMessage(err, '注册失败，请稍后再试')
  } finally {
    loading.value = false
  }
}
</script>

<style scoped src="./auth-form.css"></style>
