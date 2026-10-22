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
import { useRouter } from 'vue-router'
import { useUserStore } from '@/stores/user'
import { getErrorMessage } from '@/utils/apiError'

const emit = defineEmits(['login-success', 'switch'])

// 定义状态
const form = ref({ identifier: '', password: '' })
const isSubmitting = ref(false)
const errorMsg = ref('')

// ✅ 获取 userStore 实例
const userStore = useUserStore()
const router = useRouter()

const submit = async () => {
  if (isSubmitting.value) return
  if (!form.value.identifier || !form.value.password) {
    errorMsg.value = '请填写完整信息'
    return
  }

  isSubmitting.value = true
  errorMsg.value = ''

  try {
    const res = await userStore.login(form.value)
    emit('login-success', res.token)
    const redirect = router.currentRoute.value.query.redirect
    const destination = typeof redirect === 'string' && /^\/(?![\/\\])/.test(redirect) && !redirect.includes('\\') ? redirect : '/'
    await router.replace(destination)
    // 保留登录后的重新加载，清理各业务模块中上一个账号的缓存。
    window.location.reload()
  } catch (error) {
    errorMsg.value = getErrorMessage(error, '接入失败，请检查账号和密码')
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
  color: var(--text-secondary);
  margin-bottom: 8px;
  font-weight: 600;
}
input {
  width: 100%;
  padding: 10px 12px;
  background: var(--bg-sub);
  border: 1px solid var(--border-line);
  border-radius: 6px;
  color: var(--text-primary);
  font-size: 14px;
  transition: all 0.2s;
}
input:focus {
  background: var(--paper-card);
  border-color: var(--accent-color);
  outline: none;
  box-shadow: 0 0 0 3px rgba(9, 105, 218, 0.1);
}
button {
  width: 100%;
  padding: 12px;
  background: var(--text-primary); /* 黑色按钮 */
  border: none;
  border-radius: 6px;
  color: var(--paper-card);
  font-weight: 600;
  cursor: pointer;
  margin-top: 10px;
  transition: background 0.2s;
}
button:hover { background: var(--text-primary); }

.footer-links {
  margin-top: 24px;
  text-align: center;
  font-size: 13px;
  color: var(--text-secondary);
}
.footer-links span { cursor: pointer; }
.footer-links span:hover { color: var(--accent-color); text-decoration: underline; }
.dot { margin: 0 8px; color: var(--border-line); }
</style>
