<template>
  <div v-if="quotaInfo" class="quota-banner">
    <div class="quota-item">
      <span>📚 世界观配额</span>
      <span class="quota-num">{{ quotaInfo.usedWorldCount }} / {{ quotaInfo.maxWorldCount }}</span>
      <span v-if="quotaInfo.remainingWorldCount <= 0" class="quota-warning">⚠️ 已满</span>
      <span v-else class="quota-remaining">剩余 {{ quotaInfo.remainingWorldCount }} 个</span>
    </div>
    <div class="quota-item">
      <span>💰 当前经验</span>
      <span class="quota-num">{{ quotaInfo.experience }}</span>
    </div>
    <button class="quota-upgrade-btn" :disabled="upgrading" @click="$emit('upgrade')">
      {{ upgrading ? '处理中…' : `扩充名额（${quotaInfo.expCostPerWorldSlot} 经验）` }}
    </button>
  </div>
</template>

<script setup lang="ts">
import type { WorldQuota } from '@/api/types/world'
defineProps<{ quotaInfo: WorldQuota | null; upgrading?: boolean }>()
defineEmits<{ upgrade: [] }>()
</script>

<style scoped>
.quota-banner {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 16px;
  padding: 12px 20px;
  background: var(--bg-main);
  border: 1px solid var(--border-line);
  border-radius: 12px;
  margin-bottom: 24px;
  font-size: 13px;
}
.quota-item {
  display: flex;
  align-items: center;
  gap: 8px;
}
.quota-item > span:first-child {
  color: var(--text-secondary);
}
.quota-num {
  font-weight: 600;
  color: var(--text-primary);
}
.quota-remaining {
  color: var(--success);
  font-weight: 500;
}
.quota-warning {
  color: var(--danger);
  font-weight: 600;
}
.quota-upgrade-btn {
  padding: 4px 16px;
  border: 1px solid var(--danger);
  border-radius: 6px;
  background: var(--danger-soft);
  color: var(--danger);
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s;
}
.quota-upgrade-btn:hover {
  background: var(--danger);
  color: var(--on-accent);
}
.quota-link {
  color: var(--accent-color);
  text-decoration: none;
  font-size: 13px;
  margin-left: auto;
}
.quota-link:hover {
  text-decoration: underline;
}
@media (max-width: 820px) {
  .quota-banner {
    flex-direction: column;
    align-items: stretch;
    gap: 10px;
  }
  .quota-link {
    margin-left: 0;
    text-align: center;
  }
}
@media (max-width: 480px) {
  .quota-banner {
    padding: 12px 16px;
  }
  .quota-item {
    flex-wrap: wrap;
  }
}
</style>
