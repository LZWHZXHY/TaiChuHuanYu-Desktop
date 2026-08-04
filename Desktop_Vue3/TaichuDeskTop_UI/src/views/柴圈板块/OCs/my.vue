<template>
  <div class="my-ocs">
    <div class="page-header">
      <div>
        <h1 class="page-title">📂 我的 OC</h1>
        <p class="page-subtitle">共 {{ myCharacters.length }} 位角色</p>
      </div>
      <router-link to="/ocs/create" class="btn-line btn-primary">
        ＋ 投稿新 OC
      </router-link>
    </div>

    <!-- 状态筛选 -->
    <div class="filter-bar">
      <div class="filter-tabs">
        <button
          v-for="tab in statusTabs"
          :key="tab.value"
          :class="['btn-line', { active: currentStatus === tab.value }]"
          @click="switchStatus(tab.value)"
        >
          {{ tab.label }}
        </button>
      </div>
    </div>

    <div v-if="loading" class="loading-state">加载中...</div>

    <div v-else-if="!myCharacters.length" class="empty-state">
      <p>还没有 OC 角色</p>
      <router-link to="/ocs/create" class="empty-link">创建第一个</router-link>
    </div>

    <div v-else class="oc-grid">
      <CharacterCard
        v-for="char in myCharacters"
        :key="char.id"
        :character="char"
        @click="goDetail"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useStickmanStore } from '../stickman_store'
import CharacterCard from '../components/CharacterCard.vue'

const router = useRouter()
const store = useStickmanStore()

const myCharacters = computed(() => store.myCharacters)
const loading = computed(() => store.loading)

const currentStatus = ref('all')

const statusTabs = [
  { label: '全部', value: 'all' },
  { label: '已发布', value: 'published' },
  { label: '草稿', value: 'draft' },
]

function switchStatus(status: string) {
  currentStatus.value = status
  const params = status === 'all' ? undefined : { status }
  store.fetchMyCharacters(params?.status)
}

function goDetail(id: string) {
  router.push(`/ocs/${id}`)
}

onMounted(() => {
  store.fetchMyCharacters()
})
</script>

<style scoped>
.my-ocs {
  max-width: 1280px;
  margin: 0 auto;
  padding: 32px 24px 60px;
  background: var(--paper-bg);
  min-height: 100vh;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  padding-bottom: 16px;
  border-bottom: 1px solid var(--line-raw);
  margin-bottom: 24px;
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

.btn-primary {
  padding: 8px 24px;
  border-color: var(--ink-black);
}

.filter-bar {
  margin-bottom: 28px;
}

.filter-tabs {
  display: flex;
  gap: 4px;
}

.filter-tabs .btn-line {
  padding: 6px 18px;
  font-size: 13px;
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

.oc-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(190px, 1fr));
  gap: 20px;
}

@media (max-width: 768px) {
  .my-ocs {
    padding: 20px 16px 40px;
  }

  .page-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 12px;
  }

  .oc-grid {
    grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
    gap: 14px;
  }
}

@media (max-width: 480px) {
  .oc-grid {
    grid-template-columns: repeat(2, 1fr);
    gap: 12px;
  }
}
</style>