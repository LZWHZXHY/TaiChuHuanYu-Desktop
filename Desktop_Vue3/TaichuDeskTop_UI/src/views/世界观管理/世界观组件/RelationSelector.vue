<template>
  <div class="relation-selector">
    <p class="relation-hint">选择条目即可关联，关系说明可以稍后补充。随本条目一起保存。</p>
    <div v-for="(relation, index) in modelValue" :key="relation.targetCardId" class="relation-item">
      <span class="rel-target">{{ title(relation.targetCardId) }}</span>
      <input :value="relation.relationType" maxlength="100" :aria-label="`${title(relation.targetCardId)}的关系说明`" placeholder="关系说明（可选，如居住于）" @input="updateType(index, ($event.target as HTMLInputElement).value)" />
      <button type="button" :aria-label="`移除与${title(relation.targetCardId)}的关系`" @click="emit('update:modelValue', modelValue.filter((_, i) => i !== index))">移除</button>
    </div>
    <el-select v-model="selectedId" filterable placeholder="搜索并关联人物、地点或组织" aria-label="选择关联设定" @change="addRelation">
      <el-option v-for="card in candidates" :key="card.id" :label="`${card.title} · ${getCardTypeLabel(card.type)}`" :value="card.id" />
    </el-select>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useWorldStore } from '@/stores/world'
import { getCardTypeLabel } from '@/utils/worldPresentation'
import type { RelationInput } from '@/api/types/world'
const props = defineProps<{ modelValue: RelationInput[]; projectId: string; excludeCardId?: string }>()
const emit = defineEmits<{ 'update:modelValue': [value: RelationInput[]] }>()
const store = useWorldStore()
const selectedId = ref('')
const candidates = computed(() => store.cards.filter(card => card.projectId === props.projectId && card.id !== props.excludeCardId && !props.modelValue.some(r => r.targetCardId === card.id)))
const title = (id: string) => store.getCardById(id, props.projectId)?.title || '条目已删除'
function addRelation(id: string) {
  if (candidates.value.some(card => card.id === id)) emit('update:modelValue', [...props.modelValue, { targetCardId: id, relationType: '' }])
  selectedId.value = ''
}
function updateType(index: number, relationType: string) {
  emit('update:modelValue', props.modelValue.map((relation, i) => i === index ? { ...relation, relationType } : relation))
}
</script>

<style scoped>
.relation-selector { display: grid; gap: 12px; }
.relation-hint { margin: 0; color: var(--text-secondary); font-size: 13px; line-height: 1.7; }
.relation-item { display: flex; align-items: center; gap: 12px; padding: 12px; background: var(--bg-main); border: 1px solid var(--border-line); border-radius: 0; }
.rel-target { flex: 1; min-width: 0; overflow-wrap: anywhere; font-size: 14px; }
.relation-item input { flex: 2; min-width: 0; padding: 8px 10px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-card); color: var(--text-primary); font: inherit; font-size: 13px; }
.relation-item button { border: 0; background: none; color: var(--text-secondary); cursor: pointer; white-space: nowrap; }
.relation-item button:hover { color: var(--danger); }
@media(max-width:600px) { .relation-item { flex-wrap: wrap; }.rel-target { flex-basis: 100%; } }
</style>
