<template>
  <div class="form-body">
    <div class="input-group">
      <label>账号</label>
      <input v-model="form.identifier" @keyup.enter="submit" placeholder="用户名或邮箱" aria-label="账号" />
    </div>
    
    <div class="input-group">
      <label>密码</label>
      <input v-model="form.password" type="password" autocomplete="current-password" @keyup.enter="submit" placeholder="输入密码" aria-label="密码" />
    </div>

    <button class="black-btn" @click="submit" :disabled="isSubmitting">
      {{ isSubmitting ? '正在登录…' : '登录' }}
    </button>

    <div class="footer-links">
      <button type="button" class="text-link" @click="$emit('switch', 'register')">注册新账号</button>
      <span class="dot">·</span>
      <button type="button" class="text-link" @click="$emit('switch', 'forgot')">找回密码</button>
    </div>

    <p role="alert" v-if="errorMsg" class="error-text">{{ errorMsg }}</p>
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



<style scoped src="./auth-form.css"></style>
