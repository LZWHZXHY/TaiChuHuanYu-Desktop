<!-- CardOcBadge.vue：条目页标题下面的一行「来自柴圈 OC《xxx》」，点了去 OC 详情页。
     只显示看得到的 OC（已发布的，或者自己的草稿）；没有对应的 OC 就什么都不显示。 -->
<template>
  <p v-if="items.length" class="cob">
    <span v-for="p in items" :key="p.ocId" class="cob-item">
      <span aria-hidden="true">🧍</span>
      {{ p.origin === 'oc' ? '来自柴圈 OC' : '已发布成柴圈 OC' }}
      <RouterLink :to="`/ocs/${p.ocId}`">《{{ p.title }}》</RouterLink>
      <small v-if="p.status === 'draft'">（草稿，只有你看得到）</small>
      <small v-else>· 作者 {{ p.authorName }}</small>
    </span>
  </p>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { ocWorldApi, type CardOcPair } from '@/views/柴圈板块/OCs/ocWorld'

const props = defineProps<{ cardId: string }>()
const items = ref<CardOcPair[]>([])

let version = 0
watch(() => props.cardId, async id => {
  const v = ++version
  items.value = []
  if (!id) return
  try {
    const res = await ocWorldApi.cardInfo(id)
    if (v === version) items.value = res.items
  } catch { /* 读不到就不显示，不影响条目页 */ }
}, { immediate: true })
</script>

<style scoped>
.cob { display: flex; flex-wrap: wrap; gap: 4px 16px; margin: 6px 0 0; font-size: 13px; color: var(--text-secondary); }
.cob-item { display: inline-flex; flex-wrap: wrap; align-items: baseline; gap: 4px; }
.cob a { color: var(--accent-color); text-decoration: none; }
.cob a:hover { text-decoration: underline; }
.cob small { font-size: 12px; }
</style>
