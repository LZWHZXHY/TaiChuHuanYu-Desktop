<template>
  <div class="form-body">
    <div class="input-group">
      <label>绑定地址 / REGISTERED EMAIL</label>
      <div class="email-input-wrapper">
        <input 
          v-model="form.email" 
          type="email" 
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
      <label>校验代码 / VERIFICATION CODE</label>
      <input 
        v-model="form.verificationCode" 
        placeholder="6位验证码" 
        maxlength="6" 
        :disabled="isSubmitting"
      />
    </div>

    <div class="input-group">
      <label>新设密钥 / NEW PASSWORD</label>
      <input 
        v-model="form.newPassword" 
        type="password" 
        placeholder="不少于6位新密钥" 
        @keyup.enter="handleReset"
        :disabled="isSubmitting"
      />
    </div>

    <button @click="handleReset" :disabled="isSubmitting" class="black-btn">
      {{ isSubmitting ? '正在重塑密钥...' : '重置身份密印' }}
    </button>

    <div class="footer-links">
      <span @click="$emit('switch', 'login')">返回接入界面</span>
    </div>

    <p v-if="message" :class="['status-text', isError ? 'error-text' : 'success-text']">
      {{ message }}
    </p>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
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

    await authApi.sendCode(form.value.email) 
    
    message.value = '验证码已发往灵觉地址'
    isError.value = false
    
    countdown.value = 60
    const timer = setInterval(() => {
      countdown.value--
      if (countdown.value <= 0) clearInterval(timer)
    }, 1000)
  } catch (err: any) {
    isError.value = true
    message.value = err.response?.data?.message || '发送失败，请稍后重试'
  }
}

// 提交重置逻辑
const handleReset = async () => {
  // 1. 基础前端校验
  if (!form.value.email) {
    isError.value = true
    message.value = '请填写绑定地址'
    return
  }
  if (!form.value.verificationCode || !form.value.newPassword) {
    isError.value = true
    message.value = '请填写完整的校验信息'
    return
  }
  if (form.value.newPassword.length < 6) {
    isError.value = true
    message.value = '新密钥长度不能少于6位'
    return
  }

  isSubmitting.value = true
  message.value = ''
  
  try {
  
    const res = await authApi.resetPassword(form.value) 
    

    message.value = res.data?.message || '密印重塑成功！请尝试重新接入'
    isError.value = false
    
  
    setTimeout(() => {
      emit('switch', 'login')
    }, 2000)
    
  } catch (err: any) {
    // 5. 错误处理
    isError.value = true
    // 优先显示后端返回的错误信息（如“验证码错误”、“用户不存在”等）
    message.value = err.response?.data?.message || '重塑失败，验证码或有误'
  } finally {
    isSubmitting.value = false
  }
}
</script>

<style scoped>
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
  transition: all 0.2s;
}

.send-code-btn:hover:not(:disabled) {
  background: #f3f4f6;
  border-color: #0969da;
}

.send-code-btn:disabled {
  color: #afb8c1;
  cursor: not-allowed;
}

input {
  width: 100%;
  padding: 8px 12px;
  background: #f6f8fa;
  border: 1px solid #d0d7de;
  border-radius: 6px;
  color: #1f2328;
  font-size: 14px;
  transition: all 0.2s;
}

input:focus {
  background: #fff;
  border-color: #0969da;
  outline: none;
  box-shadow: 0 0 0 3px rgba(9, 105, 218, 0.1);
}

.black-btn {
  width: 100%;
  padding: 10px;
  background: #21262d;
  color: #ffffff;
  border: none;
  border-radius: 6px;
  font-weight: 600;
  cursor: pointer;
  margin-top: 10px;
}

.black-btn:hover { background: #30363d; }
.black-btn:disabled { background: #8c959f; cursor: not-allowed; }

.footer-links {
  margin-top: 20px;
  text-align: center;
}

.footer-links span {
  font-size: 12px;
  color: #0969da;
  cursor: pointer;
}

.status-text {
  font-size: 12px;
  margin-top: 12px;
  text-align: center;
}

.error-text { color: #cf222e; }
.success-text { color: #1a7f37; }
</style>