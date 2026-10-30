<template>
  <div class="community-detail create-page">
    <div class="page-header">
      <h1 class="page-title">✎ 创建新 OC</h1>
      <p class="page-subtitle">为柴圈注入新的灵魂</p>
    </div>

    <div class="form-wrapper">
      <p v-if="error" role="alert">{{ error }}</p>
      <CharacterForm :loading="store.loading" @submit="handleSubmit" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import { ref } from 'vue'
import { useStickmanStore } from '../stickman_store.ts'
import CharacterForm from '../../components/CharacterForm.vue'

const router = useRouter()
const store = useStickmanStore()
const error = ref('')

async function handleSubmit(data: any) {
  if (store.loading) return
  error.value = ''
  try {
    const result = await store.createCharacter(data)
    router.push(`/ocs/${result.id}`)
  } catch {
    error.value = '保存失败，请重试。已填写的内容仍保留。'
  }
}
</script>

<style scoped>
.create-page {
  max-width: 720px;
  margin: 0 auto;
  padding: 32px 24px 60px;
  background: var(--paper-bg);
  min-height: 100vh;
}

.page-header {
  padding-bottom: 20px;
  border-bottom: 1px solid var(--line-raw);
  margin-bottom: 32px;
}

.page-title {
  font-size: 24px;
  font-weight: 400;
  letter-spacing: 0.25em;
  margin: 0 0 4px 0;
  color: var(--ink-black);
}

.page-subtitle {
  font-size: 14px;
  color: var(--ink-gray);
  letter-spacing: 0.15em;
  margin: 0;
}

.form-wrapper {
  background: var(--paper-card);
  border: 1px solid var(--line-raw);
  padding: 32px;
}

@media (max-width: 600px) {
  .create-page {
    padding: 20px 12px 40px;
  }

  .form-wrapper {
    padding: 20px 16px;
  }
}
</style>
