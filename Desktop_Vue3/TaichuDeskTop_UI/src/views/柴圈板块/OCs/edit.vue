<template>
  <div class="edit-page">
    <div class="page-header">
      <h1 class="page-title">✎ 编辑 OC</h1>
      <p class="page-subtitle">修改「{{ character?.name || '角色' }}」的设定</p>
    </div>

    <div v-if="loading" class="loading-state">加载中...</div>

    <div v-else-if="!character" class="empty-state">
      <p>角色不存在</p>
      <router-link to="/ocs" class="empty-link">返回 OC 画阁</router-link>
    </div>

    <div v-else class="form-wrapper">
      <CharacterForm :initial-data="character" @submit="handleUpdate" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useStickmanStore } from '../stickman_store'
import CharacterForm from '../components/CharacterForm.vue'

const router = useRouter()
const route = useRoute()
const store = useStickmanStore()

const character = computed(() => store.currentCharacter)
const loading = computed(() => store.loading)

onMounted(async () => {
  const id = route.params.id as string
  await store.fetchDetail(id)
})

async function handleUpdate(data: any) {
  if (!character.value) return
  try {
    await store.updateCharacter(character.value.id, data)
    router.push(`/ocs/${character.value.id}`)
  } catch (error) {
    console.error('更新失败:', error)
  }
}
</script>

<style scoped>
.edit-page {
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

.loading-state,
.empty-state {
  padding: 60px 0;
  text-align: center;
  color: var(--ink-gray);
  font-size: 14px;
  letter-spacing: 0.15em;
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
  .edit-page {
    padding: 20px 12px 40px;
  }

  .form-wrapper {
    padding: 20px 16px;
  }
}
</style>