<template>
  <div class="create-joint">
    <div class="page-header">
      <div class="header-left">
        <button class="back-btn" @click="goBack">← 返回</button>
        <div>
          <h1 class="page-title">✎ 发起联合活动</h1>
          <p class="page-subtitle">召集火柴人创作者，共同完成一场联合大作</p>
        </div>
      </div>
    </div>

    <div class="form-wrapper">
      <JointForm submit-label="发布联合" @submit="handleSubmit" @cancel="goBack" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useJointStore } from '../joint_store'
import JointForm from '../components/JointForm.vue'

const router = useRouter()
const store = useJointStore()

function goBack() {
  router.back()
}

async function handleSubmit(data: any) {
  try {
    const result = await store.create(data)
    router.push(`/joint/${result.id}`)
  } catch (error) {
    console.error('发布联合失败:', error)
    alert('发布失败，请重试')
  }
}
</script>

<style scoped>
.create-joint {
  max-width: 760px;
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

.header-left {
  display: flex;
  align-items: center;
  gap: 16px;
}

.back-btn {
  background: none;
  border: none;
  color: var(--ink-gray);
  font-size: 14px;
  letter-spacing: 0.15em;
  cursor: pointer;
  padding: 4px 8px 4px 0;
  font-family: var(--font-family);
  transition: color 0.3s;
  white-space: nowrap;
}

.back-btn:hover {
  color: var(--ink-black);
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
  .create-joint {
    padding: 20px 12px 40px;
  }
  .form-wrapper {
    padding: 20px 16px;
  }
}
</style>