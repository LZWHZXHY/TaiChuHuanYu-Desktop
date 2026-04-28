<template>
  <div class="form-body">
    <div class="input-group">
      <label>昵称 / USERNAME</label>
      <input v-model="form.username" placeholder="不建议使用邮箱" />
    </div>

    <div class="input-group">
      <label>邮件地址 / EMAIL</label>
      <div class="email-input-wrapper">
        <input v-model="form.email" type="email" placeholder="email@example.com" />
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
      <label>验证代码 / VERIFICATION CODE</label>
      <input v-model="form.verificationCode" placeholder="请输入6位验证码" maxlength="6" />
    </div>

    <div class="input-group">
      <label>密钥/ PASSWORD</label>
      <input v-model="form.password" type="password" placeholder="不少于6位" />
    </div>

    <button @click="handleRegister" :disabled="loading" class="black-btn">
      {{ loading ? '认知中...' : '开始认证' }}
    </button>

    <p v-if="message" :class="['message', isError ? 'error' : 'success']">
      {{ message }}
    </p>

    <div class="footer-links">
      <span @click="$emit('switch', 'login')">已有账号？返回接入</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { authApi } from '../../api/auth'

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
  } catch (err: any) {
    isError.value = true
    message.value = err.friendlyMessage || '发送失败'
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
  } catch (err: any) {
    isError.value = true
    message.value = err.response?.data?.message || '认知失败'
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
/* 保持原有样式，增加发送按钮样式 */
.input-group { margin-bottom: 16px; }
label { font-size: 11px; color: #57606a; font-weight: 600; margin-bottom: 6px; display: block; }

.email-input-wrapper {
  display: flex;
  gap: 8px;
}

.email-input-wrapper input {
  flex: 1;
}

.send-code-btn {
  padding: 0 12px;
  background: #f6f8fa;
  border: 1px solid #d0d7de;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  white-space: nowrap;
}

.send-code-btn:disabled {
  color: #afb8c1;
  cursor: not-allowed;
}

input {
  width: 100%; padding: 8px 12px; background: #f6f8fa; border: 1px solid #d0d7de;
  border-radius: 6px; font-size: 14px;
}
input:focus { border-color: #0969da; background: #fff; outline: none; }

.black-btn {
  width: 100%; padding: 12px; background: #24292f; border-radius: 6px;
  color: white; font-weight: 600; border: none; cursor: pointer; margin-top: 10px;
}
.black-btn:hover { background: #1f2328; }

.message { margin-top: 16px; text-align: center; font-size: 12px; }
.error { color: #cf222e; background: #ffebe9; padding: 8px; border-radius: 6px; }
.success { color: #1a7f37; background: #dafbe1; padding: 8px; border-radius: 6px; }

.footer-links { margin-top: 24px; text-align: center; font-size: 13px; color: #0969da; cursor: pointer; }
</style>