<template>
  <div v-if="quotaInfo" class="quota-banner">
    <div class="quota-item">
      <span>📚 世界观配额</span>
      <span class="quota-num">{{ quotaInfo.usedWorldCount }} / {{ quotaInfo.maxWorldCount }}</span>
      <span v-if="quotaInfo.remainingWorldCount <= 0" class="quota-warning">⚠️ 已满</span>
      <span v-else class="quota-remaining">剩余 {{ quotaInfo.remainingWorldCount }} 个</span>
    </div>
    <div class="quota-item" title="节点上限对你所有的世界观通用：扩容一次，每个世界观的上限都会提高">
      <span>📝 每个世界的节点上限</span>
      <span class="quota-num">{{ quotaInfo.maxCardsPerWorld }}</span>
      <span class="quota-remaining">所有世界通用</span>
    </div>
    <RouterLink class="quota-upgrade-btn" to="/trade" title="世界观数量和节点上限都在交易行扩容">去交易行扩容 →</RouterLink>
  </div>
</template>

<script setup lang="ts">
import type { WorldQuota } from '@/api/types/world'
// 扩容统一在交易行：这里只展示额度，按钮跳过去
defineProps<{ quotaInfo: WorldQuota | null }>()
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
  border-radius: 0;
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
  margin-left: auto;
  padding: 4px 16px;
  border: 1px solid var(--accent-color);
  border-radius: 0;
  color: var(--accent-color);
  font-size: 13px;
  text-decoration: none;
  transition: all 0.2s;
}
.quota-upgrade-btn:hover, .quota-upgrade-btn:focus-visible {
  background: var(--accent-color);
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
