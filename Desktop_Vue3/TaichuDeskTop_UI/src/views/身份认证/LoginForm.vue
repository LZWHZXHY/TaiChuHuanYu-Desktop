<template>
  <div class="form-body">
    <div class="input-group">
      <label>账号 / IDENTITY</label>
      <input v-model="form.identifier" @keyup.enter="submit" placeholder="输入您的名号" />
    </div>
    
    <div class="input-group">
      <label>密钥 / PASSWORD</label>
      <input v-model="form.password" type="password" @keyup.enter="submit" placeholder="输入密钥" />
    </div>

    <button @click="submit" :disabled="isSubmitting">
      {{ isSubmitting ? '正在接入...' : '确认接入' }}
    </button>

    <div class="footer-links">
      <span @click="$emit('switch', 'register')">注册新账号</span>
      <span class="dot">·</span>
      <span @click="$emit('switch', 'forgot')">找回密码</span>
    </div>

    <p v-if="errorMsg" class="error-text">{{ errorMsg }}</p>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { authApi } from '../../api/auth' 

const emit = defineEmits(['login-success', 'switch'])

// 定义状态
const form = ref({ identifier: '', password: '' })
const isSubmitting = ref(false)
const errorMsg = ref('')

const submit = async () => {
  if (!form.value.identifier || !form.value.password) {
    errorMsg.value = '请填写完整信息'
    return
  }

  isSubmitting.value = true
  errorMsg.value = ''

  try {
    const res = await authApi.login(form.value)
    
    // 存储 Token 和 用户名
    localStorage.setItem('token', res.token)
    if (res.user?.username) localStorage.setItem('username', res.user.username)
    
    emit('login-success', res.token)
    

    window.location.href = '/'
  } catch (err: any) {
    console.error(err)
    errorMsg.value = err.response?.data?.message || '接入失败，密钥不匹配'
  } finally {
    isSubmitting.value = false
  }
}
</script>

<style scoped>
.input-group { margin-bottom: 20px; }
label {
  display: block;
  font-size: 11px;
  color: #57606a;
  margin-bottom: 8px;
  font-weight: 600;
}
input {
  width: 100%;
  padding: 10px 12px;
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
button {
  width: 100%;
  padding: 12px;
  background: #1f2328; /* 黑色按钮 */
  border: none;
  border-radius: 6px;
  color: #fff;
  font-weight: 600;
  cursor: pointer;
  margin-top: 10px;
  transition: background 0.2s;
}
button:hover { background: #2f363d; }

.footer-links {
  margin-top: 24px;
  text-align: center;
  font-size: 13px;
  color: #57606a;
}
.footer-links span { cursor: pointer; }
.footer-links span:hover { color: #0969da; text-decoration: underline; }
.dot { margin: 0 8px; color: #d0d7de; }
</style>