<template>
  <div class="community-detail edit-page workbench-skin editor-surface community-surface tone-chai">
    <div class="page-header studio-heading"><SectionTrace />
      <h1 class="page-title">✎ 编辑 OC</h1>
      <p class="page-subtitle">修改「{{ character?.title || '角色' }}」的设定</p>
    </div>

    <div v-if="loading" class="loading-state">加载中...</div>

    <div v-else-if="!character" class="empty-state">
      <p>{{ error || '角色不存在' }}</p>
      <router-link to="/ocs" class="empty-link">返回 OC 画阁</router-link>
    </div>

    <div v-else class="form-wrapper">
      <p v-if="error" role="alert">{{ error }}</p>
      <CharacterForm :initial-data="character" :loading="store.loading" @submit="handleUpdate" />
    </div>
  </div>
</template>

<script setup lang="ts">
import SectionTrace from '@/components/SectionTrace.vue'
import { computed, ref, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useStickmanStore } from '../stickman_store.ts'
import CharacterForm from '../../components/CharacterForm.vue'

const router = useRouter()
const route = useRoute()
const store = useStickmanStore()

const character = computed(() => store.currentCharacter)
const loading = ref(true)
const error = ref('')

onMounted(async () => {
  const id = route.params.id as string
  store.clearCurrent()
  try { await store.fetchDetail(id) }
  catch { error.value = '角色加载失败，请返回后重试。' }
  finally { loading.value = false }
})

async function handleUpdate(data: any) {
  if (!character.value || store.loading) return
  error.value = ''
  try {
    await store.updateCharacter(character.value.id, data)
    router.push(`/ocs/${character.value.id}`)
  } catch {
    error.value = '保存失败，请重试。已填写的内容仍保留。'
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
