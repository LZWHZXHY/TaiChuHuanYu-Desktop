<template>
  <div class="edit-joint">
    <div class="page-header">
      <div class="header-left">
        <button class="back-btn" @click="goBack">← 返回</button>
        <div>
          <h1 class="page-title">✎ 编辑联合活动</h1>
          <p class="page-subtitle">修改「{{ activity?.title || '活动' }}」的设定</p>
        </div>
      </div>
    </div>

    <div v-if="loading" class="loading-state">
      <div class="spinner"></div>
      <span>加载中...</span>
    </div>

    <div v-else-if="!activity" class="empty-state">
      <p>联合活动不存在</p>
      <router-link to="/joint" class="empty-link">返回联合列表</router-link>
    </div>

    <div v-else class="form-wrapper">
      <JointForm
        :initial-data="activity"
        submit-label="保存修改"
        @submit="handleUpdate"
        @cancel="goBack"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useJointStore } from '../joint_store'
import JointForm from '../components/JointForm.vue'

const router = useRouter()
const route = useRoute()
const store = useJointStore()

const activity = computed(() => store.currentActivity)
const loading = computed(() => store.loading)

function goBack() {
  router.push('/joint')
}

onMounted(async () => {
  const id = route.params.id as string
  await store.fetchDetail(id)
})

async function handleUpdate(data: any) {
  if (!activity.value) return
  try {
    await store.update(activity.value.id, data)
    router.push(`/joint/${activity.value.id}`)
  } catch (error) {
    console.error('更新失败:', error)
    alert('更新失败，请重试')
  }
}
</script>

<style scoped>
.edit-joint {
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

.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 0;
  gap: 16px;
  color: var(--ink-gray);
}

.spinner {
  width: 32px;
  height: 32px;
  border: 2px solid var(--line-raw);
  border-top-color: var(--ink-black);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.empty-state {
  padding: 60px 0;
  text-align: center;
  color: var(--ink-gray);
}

.empty-link {
  color: var(--cinnabar);
  text-decoration: none;
  border-bottom: 1px solid var(--line-raw);
  padding-bottom: 2px;
}

.empty-link:hover {
  border-color: var(--cinnabar);
}

.form-wrapper {
  background: var(--paper-card);
  border: 1px solid var(--line-raw);
  padding: 32px;
}

@media (max-width: 600px) {
  .edit-joint {
    padding: 20px 12px 40px;
  }
  .form-wrapper {
    padding: 20px 16px;
  }
}
</style>