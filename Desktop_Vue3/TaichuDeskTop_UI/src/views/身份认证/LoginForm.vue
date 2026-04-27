<template>
  <div class="form-body">
    <input v-model="form.username" placeholder="漫游者账号" />
    <input v-model="form.password" type="password" placeholder="密钥" />
    <button @click="submit">确认接入</button>
    <div class="footer">
      <span @click="$emit('switch', 'register')">注册账号</span>
      <span @click="$emit('switch', 'forgot')">找回密码</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
const form = ref({ username: '', password: '' })
const emit = defineEmits(['login-success', 'switch'])

const submit = async () => {

}
</script>